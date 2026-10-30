// scripts/import-split-target.test.mjs
//
// design-system-bym4m: das Ziel einer Split-Zeile im Import läuft über den gemeinsamen Picker.
// Echter Vite-Client gegen gemockte API-Antworten (erfundene Werte, nie YNAB). Geprüft am
// gerenderten Sheet: genau ein Ziel-Feld, kein Sync-Knopf, die Suche "Bar" findet das Konto als
// "→ Bar", die Wahl setzt den Übertrag, und das Speichern schickt ein Transfer-Ziel.
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
const settings = { Comdirect: null, Ynab: { PersonalAccessToken: "x", DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } };
const budget = { Budget: { Id: W("YnabBudgetId", "b1"), Name: "Haushalt" }, Accounts: accounts, Categories: cats };
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

async function open(viewport) {
  const ctx = await browser.newContext({ viewport, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.setDefaultTimeout(15000);
  const saved = [];
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, body: "x" }));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => r.fulfill(json(session)));
  await page.route("**/api/SyncApi/getTransactions", (r) => r.fulfill(json({ Ok: [row] })));
  await page.route("**/api/SyncApi/splitTransaction", (r) => {
    saved.push(r.request().postData());
    r.fulfill(json({ Ok: row }));
  });
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/YnabApi/getCategories", (r) => r.fulfill(json({ Ok: cats })));
  await page.route("**/api/YnabApi/getPayees", (r) => r.fulfill(json({ Ok: [] })));
  await page.route("**/api/YnabApi/getBudgetDetails", (r) => r.fulfill(json({ Ok: budget })));
  await page.goto(`http://localhost:${port}/#/`);
  await page.waitForSelector(".tx-row");
  return { ctx, page, saved };
}

for (const [name, viewport] of [["pc-1440x900", { width: 1440, height: 900 }], ["handy-390x844", { width: 390, height: 844 }]]) {
  test(`Import-Split: "Bar" tippen und "→ Bar" wählen, kein Sync-Knopf (${name})`, async () => {
    const { ctx, page, saved } = await open(viewport);
    await page.locator(".tx-row", { hasText: "Markt bym4m" }).click();
    await page.locator(".tx-row.expanded").getByRole("button", { name: "Aufteilen", exact: true }).click();
    const form = page.locator(".bottom-sheet.active").first();
    const target = form.locator('[data-testid="import-split-target-1"]');
    await target.waitFor();
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
    await ctx.close();
  });
}
