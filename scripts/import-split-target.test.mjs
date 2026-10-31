// scripts/import-split-target.test.mjs
//
// design-system-bym4m: das Ziel einer Split-Zeile im Import läuft über den gemeinsamen Picker.
// Echter Vite-Client gegen gemockte API-Antworten (erfundene Werte, nie YNAB). Geprüft am
// gerenderten Sheet: genau ein Ziel-Feld, kein Sync-Knopf, die Suche "Bar" findet das Konto als
// "→ Bar", die Wahl setzt den Übertrag, und das Speichern schickt ein Transfer-Ziel.
//
// ynab-sync-k3vq8: der Import lädt die Konten über `getBudgetAccounts`, der langsame Abruf
// `getBudgetDetails` scheitert in jedem Testfall. Lesende Abrufe überstehen einzelne Abbrüche
// (Firefox, "Http error (0)"), schreibende werden nicht wiederholt.
//
// Ausführen: node --test scripts/import-split-target.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const g = (n) => `00000000-0000-4000-8000-${n.toString(16).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const day = () => new Date().toISOString().slice(0, 10) + "T08:00:00.0000000";
const det = { TransactionReference: "", ReferenceFoundInYnab: false, ImportIdFoundInYnab: false, FuzzyMatchDate: null, FuzzyMatchAmount: null, FuzzyMatchPayee: null };
const cats = [
  { Id: W("YnabCategoryId", g(100)), Name: "Lebensmittel", GroupName: "Alltag", Available: { Amount: 100, Currency: "EUR" }, Hidden: false },
  { Id: W("YnabCategoryId", g(101)), Name: "Bar-Auslagen", GroupName: "Alltag", Available: { Amount: 20, Currency: "EUR" }, Hidden: false },
];
const account = (n, Name, OnBudget, Closed) => ({ Id: W("YnabAccountId", g(n)), Name, Balance: { Amount: 0, Currency: "EUR" }, OnBudget, Closed });
const accounts = [account(200, "Bar", true, false), account(201, "Tagesgeld", true, false), account(202, "Depot", false, false), account(203, "Altkonto", true, true)];
const row = {
  Transaction: { Id: W("TransactionId", "bym4m-1"), BookingDate: day(), Amount: { Amount: -48.12, Currency: "EUR" }, Payee: "Markt bym4m", Memo: "Kartenzahlung", Reference: "ref1", RawData: "" },
  Status: "AutoCategorized", CategoryId: W("YnabCategoryId", g(100)), CategoryName: "Lebensmittel",
  MatchedRuleId: null, PayeeOverride: null, ExternalLinks: [], UserNotes: null, DuplicateStatus: { NotDuplicate: det },
  YnabImportStatus: "NotAttempted", Splits: null, SuggestedByOrderId: null, ReviewSteps: [], AskLena: false,
};
const session = { Id: W("SyncSessionId", g(1)), StartedAt: day(), CompletedAt: null, Status: "ReviewingTransactions", TransactionCount: 1, ImportedCount: 0, SkippedCount: 0 };
const settings = { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } };
const withQuickAdd = { ...settings, Ynab: { ...settings.Ynab, QuickAddAccountId: W("YnabAccountId", g(200)) } };
const json = (b) => ({ contentType: "application/json", body: JSON.stringify(b) });

let frontend, port, browser;
before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port, undefined);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

const pc = { width: 1440, height: 900 };

/// `abort` nennt je Endpoint, wie viele der ersten Aufrufe abbrechen (Status 0, `Infinity` für alle).
/// `accounts` ist die Antwort auf den n-ten `getBudgetAccounts`-Aufruf (Vorgabe: die Kontenliste).
/// `categories` ersetzt die Antwort auf `getCategories`, `route` die Startseite.
async function open(viewport, { appSettings = settings, abort = {}, accounts: accountsAnswer, categories = { Ok: cats }, route = "#/" } = {}) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.setDefaultTimeout(15000);
  const saved = [];
  const calls = {};
  const answer = (name, respond) => (r) => {
    calls[name] = (calls[name] ?? 0) + 1;
    if (calls[name] <= (abort[name] ?? 0)) return r.abort("connectionreset");
    return respond(r, calls[name]);
  };
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, body: "x" }));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => r.fulfill(json(session)));
  await page.route("**/api/SyncApi/getTransactions", answer("getTransactions", (r) => r.fulfill(json({ Ok: [row] }))));
  await page.route("**/api/SyncApi/skipTransaction", answer("skipTransaction", (r) => r.fulfill(json({ Ok: row }))));
  await page.route("**/api/SyncApi/splitTransaction", (r) => {
    saved.push(r.request().postData());
    r.fulfill(json({ Ok: row }));
  });
  await page.route("**/api/SettingsApi/getSettings", answer("getSettings", (r) => r.fulfill(json(appSettings))));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/YnabApi/getCategories", answer("getCategories", (r) => r.fulfill(json(categories))));
  await page.route("**/api/YnabApi/getPayees", answer("getPayees", (r) => r.fulfill(json({ Ok: [] }))));
  // Bug ynab-sync-k3vq8: der langsame Abruf darf das Sheet nicht mehr speisen.
  await page.route("**/api/YnabApi/getBudgetDetails", answer("getBudgetDetails", (r) => r.fulfill({ status: 500, body: "x" })));
  await page.route("**/api/LedgerApi/getBudgetAccounts", answer("getBudgetAccounts", (r, n) => r.fulfill(json(accountsAnswer ? accountsAnswer(n) : { Ok: accounts }))));
  await page.goto(`http://localhost:${port}/${route}`);
  return { ctx, page, saved, calls };
}

async function openReady(viewport, options) {
  const opened = await open(viewport, options);
  await opened.page.waitForSelector(".tx-row");
  return opened;
}

async function tapChip(page, name) {
  await page.locator(".tx-row", { hasText: "Markt bym4m" }).click();
  await page.locator(".tx-row.expanded").getByRole("button", { name, exact: true }).click();
  const form = page.locator(".bottom-sheet.active").first();
  const line2 = form.locator('[data-testid="import-split-target-1"]');
  await line2.waitFor();
  return { form, line2 };
}

/// Wartet auf eine Bedingung außerhalb der Seite (die Zähler der Mocks).
async function until(condition, label) {
  const deadline = Date.now() + 5000;
  while (!condition()) {
    assert.ok(Date.now() < deadline, label);
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

const errorToasts = (page) => page.locator(".animate-toast-in");
const categoriesFailed = { Error: { NetworkError: "weg" } };

/// Wartet, bis keine Anfrage mehr läuft und die letzte Antwort gezeichnet ist.
async function settled(page) {
  await page.waitForLoadState("networkidle");
  await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
}

const goTo = (page, hash) => page.evaluate((target) => { location.hash = target; }, hash);

for (const [name, viewport] of [["pc-1440x900", { width: 1440, height: 900 }], ["handy-390x844", { width: 390, height: 844 }]]) {
  test(`Import-Split: "Bar" tippen und "→ Bar" wählen, kein Sync-Knopf (${name})`, async () => {
    const { ctx, page, saved, calls } = await openReady(viewport);
    const { form, line2: target } = await tapChip(page, "Aufteilen");
    assert.equal(await page.locator(".split-transfer-btn").count(), 0, "kein Sync-Knopf");
    assert.equal(await form.getByRole("button", { name: "Transfer-Konto wählen" }).count(), 0, "kein Konto-Knopf");
    assert.equal(await form.locator(".split-line").first().locator('[data-testid^="import-split-target-"]').count(), 1, "genau ein Ziel-Feld");

    await target.click();
    const picker = page.locator(".bottom-sheet.layer-2.active");
    await picker.getByText("Ziel wählen").waitFor();
    const accountRows = picker.locator('[data-testid="category-picker-account-row"]');
    assert.deepEqual((await accountRows.allTextContents()).map((n) => n.trim()), ["→ Bar", "→ Tagesgeld"], "offene Budget-Konten");
    assert.equal(await picker.getByText("Ohne Kategorie", { exact: true }).count(), 0, "keine Zeile Ohne Kategorie im Import");

    await picker.getByPlaceholder("Kategorie oder Konto suchen…").fill("Bar");
    await picker.getByText("Bar-Auslagen").waitFor();
    assert.equal(await accountRows.count(), 1, "nur das passende Konto");
    await accountRows.click();

    await picker.waitFor({ state: "hidden" });
    assert.match(await target.textContent(), /→ Bar/, "Ziel-Feld zeigt den Übertrag");

    // Eine Kategorie geht weiter wie bisher.
    // Das Sheet schluckt nach dem Schließen kurz den nächsten Klick (ADR 0005): erneut tippen,
    // bis der Picker wieder steht.
    for (let i = 0; i < 20 && !(await picker.isVisible()); i += 1) {
      await form.locator('[data-testid="import-split-target-0"]').click();
      await picker.waitFor({ timeout: 200 }).catch(() => {});
    }
    await picker.getByPlaceholder("Kategorie oder Konto suchen…").fill("Lebens");
    await picker.getByRole("option", { name: /Lebensmittel/ }).click();
    await picker.waitFor({ state: "hidden" });
    assert.match(await form.locator('[data-testid="import-split-target-0"]').textContent(), /Lebensmittel/);

    // Speichern schickt den Übertrag.
    await form.locator(".split-amount-input").nth(0).fill("40");
    await form.locator(".split-amount-input").nth(1).fill("8,12");
    const submit = form.locator(".qa-submit");
    // Auch hier kann der Nachklick-Schutz des Pickers den ersten Klick schlucken.
    for (let i = 0; i < 20 && saved.length === 0; i += 1) {
      if (await submit.isVisible()) await submit.click();
      await new Promise((r) => setTimeout(r, 100));
    }
    assert.ok(saved.length > 0, "Speichern ruft die API");
    assert.match(saved[0], /ToTransfer/, "Übertrag in der Anfrage");
    assert.equal(calls.getBudgetDetails, undefined, "der Import ruft den Budget-Vollabruf nicht mehr");
    await ctx.close();
  });
}

// Bug ynab-sync-k3vq8: der Import lud die Konten über `getBudgetDetails`, ein Fehlschlag verschwand
// still. Zeile 2 blieb leer, obwohl das Quick-Add-Konto gesetzt war, und der Picker hatte keine Konten.
test('Kaputt: getBudgetDetails scheitert, der Chip "Barabhebung" belegt Zeile 2 trotzdem mit "→ Bar"', async () => {
  const { ctx, page } = await openReady(pc, { appSettings: withQuickAdd });
  const { line2 } = await tapChip(page, "Barabhebung");
  await line2.getByText("→ Bar").waitFor();
  await ctx.close();
});

test('Intakt: ohne Quick-Add-Konto bleibt Zeile 2 leer, das Konto ist im Picker wählbar', async () => {
  const { ctx, page } = await openReady(pc);
  const { line2 } = await tapChip(page, "Barabhebung");
  assert.doesNotMatch(await line2.textContent(), /→/, "kein vorbelegtes Konto");
  await line2.click();
  const picker = page.locator(".bottom-sheet.layer-2.active");
  await picker.getByText("Übertrag auf Konto").waitFor();
  await picker.locator('[data-testid="category-picker-account-row"]').first().click();
  await picker.waitFor({ state: "hidden" });
  assert.match(await line2.textContent(), /→ Bar/);
  await ctx.close();
});

// Bug ynab-sync-k3vq8: Firefox bricht lesende Abfragen beim Laden mit "Http error (0)" ab
// (`src/Client/Retry.fs`). Der Import fing das nicht ab: je nach Lauf fehlten Buchungen, Kategorien
// oder Konten, bis Roman neu lud. Die Zahl je Zeile trifft alle Abrufe der ersten Welle des Endpoints.
for (const [label, abort] of [
  ["getSettings", { getSettings: 4 }],
  ["getTransactions", { getTransactions: 2 }],
  ["getCategories", { getCategories: 1 }],
  ["getPayees", { getPayees: 1 }],
  ["getBudgetAccounts", { getBudgetAccounts: 1 }],
]) {
  test(`Kaputt: ein Abbruch bei ${label} beim Laden heilt sich ohne Neuladen und ohne Fehler`, async () => {
    const { ctx, page, calls } = await openReady(pc, { appSettings: withQuickAdd, abort });
    // Das Sheet lädt selbst nach und würde einen Leser ohne Wiederholung verdecken: alle vier
    // Leser von `getSettings` wiederholen, bevor der Chip fällt.
    if (abort.getSettings) await until(() => calls.getSettings >= abort.getSettings * 2, "jeder Leser wiederholt");
    const { line2 } = await tapChip(page, "Barabhebung");
    await line2.getByText("→ Bar").waitFor();
    await line2.click();
    const picker = page.locator(".bottom-sheet.layer-2.active");
    await picker.getByText("Lebensmittel").waitFor();
    await picker.getByText("Übertrag auf Konto").waitFor();
    // Jeder Lesevorgang hat sein Ziel erreicht, auch die Payees, die nichts Sichtbares zeigen.
    for (const endpoint of ["getSettings", "getTransactions", "getCategories", "getPayees", "getBudgetAccounts"]) {
      await until(() => (calls[endpoint] ?? 0) > (abort[endpoint] ?? 0), `${endpoint} kam durch`);
    }
    assert.equal(await errorToasts(page).count(), 0, "kein Fehler-Toast");
    await ctx.close();
  });
}

test("Ein schreibender Aufruf wird nach einem Abbruch nicht wiederholt", async () => {
  const { ctx, page, calls } = await openReady(pc, { abort: { skipTransaction: Infinity } });
  await page.locator(".tx-row", { hasText: "Markt bym4m" }).click();
  await page.locator(".tx-row.expanded").getByRole("button", { name: "Überspringen", exact: true }).click();
  await errorToasts(page).first().waitFor();
  assert.equal(calls.skipTransaction, 1, "genau ein Versuch");
  await ctx.close();
});

test("Scheitern Kategorien und Konten dauerhaft, erscheint je ein Toast", async () => {
  const { ctx, page } = await openReady(pc, { abort: { getCategories: Infinity, getBudgetAccounts: Infinity } });
  await errorToasts(page).filter({ hasText: "Kategorien konnten nicht geladen werden" }).waitFor();
  await errorToasts(page).filter({ hasText: "Konten konnten nicht geladen werden" }).waitFor();
  assert.equal(await errorToasts(page).count(), 2, "je ein Toast, nicht je Ladeauftrag einer");
  await ctx.close();
});

test("Scheitert der erste Konten-Abruf, zeigt ein Toast den Fehlertext, und das Sheet lädt die Konten nach", async () => {
  const { ctx, page, calls } = await openReady(pc, {
    appSettings: withQuickAdd,
    accounts: (n) => (n === 1 ? { Error: "YNAB antwortet nicht" } : { Ok: accounts }),
  });
  await errorToasts(page).filter({ hasText: "YNAB antwortet nicht" }).waitFor();
  const { line2 } = await tapChip(page, "Barabhebung");
  await line2.getByText("→ Bar").waitFor();
  assert.equal(calls.getBudgetAccounts, 2, "genau ein Nachladen");
  await ctx.close();
});

// Bug ynab-sync-k3vq8, Verifier iteration 1: die Kategorien des SyncFlow laden auf jeder Seite, ihr
// Fehler-Toast lag deshalb auch über Kassenbuch, Regeln und Settings und fing dort Klicks ab.
for (const [label, options] of [
  ["#/ledger", { route: "#/ledger", categories: categoriesFailed }],
  ["#/rules", { route: "#/rules", categories: categoriesFailed }],
  ["#/settings", { route: "#/settings", categories: categoriesFailed }],
  ["#/settings ohne YNAB-Einrichtung", { route: "#/settings", appSettings: { ...settings, Ynab: null } }],
]) {
  test(`Scheitern die Kategorien, bleibt ${label} ohne Kategorie-Toast, der Import meldet es`, async () => {
    const { ctx, page } = await open(pc, options);
    await settled(page);
    const failure = errorToasts(page).filter({ hasText: "Kategorien konnten nicht geladen werden" });
    assert.equal(await failure.count(), 0, "kein Kategorie-Toast außerhalb des Imports");
    await goTo(page, "#/");
    await failure.waitFor();
    await ctx.close();
  });
}

test("Das Betreten von Quick-Add lädt die Kategorien neu und meldet den Fehler", async () => {
  const { ctx, page } = await open(pc, { route: "#/settings", categories: categoriesFailed });
  await settled(page);
  await goTo(page, "#/quickadd");
  await errorToasts(page).filter({ hasText: "Kategorien konnten nicht geladen werden" }).waitFor();
  await ctx.close();
});

// Bug ynab-sync-k3vq8, Verifier iteration 1: sechs schnelle Wechsel stapelten 14 Toasts.
test("Schnelles Wechseln bei dauerhaftem Fehler stapelt keine Toasts", async () => {
  const { ctx, page, calls } = await openReady(pc, { categories: categoriesFailed, accounts: () => ({ Error: "YNAB antwortet nicht" }) });
  for (let visit = 1; visit <= 6; visit += 1) {
    await goTo(page, "#/settings");
    await goTo(page, "#/");
    await until(() => calls.getBudgetAccounts > visit, "der Besuch lud die Konten");
  }
  await settled(page);
  assert.equal(await errorToasts(page).filter({ hasText: "Kategorien konnten nicht geladen werden" }).count(), 1, "ein Kategorie-Toast");
  assert.equal(await errorToasts(page).filter({ hasText: "Konten konnten nicht geladen werden" }).count(), 1, "ein Konten-Toast");
  await ctx.close();
});
