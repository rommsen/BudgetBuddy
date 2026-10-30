// scripts/ledger-split-editor.test.mjs
//
// Wächter für `ledger-pafkp` (ADR 0058, ADR 0006): der Split-Editor im Kassenbuch-Detail. Gemessen
// am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-category-change.test.mjs`, nie die echte Entwicklungsdatenbank): das Feld Split mit dem
// festen Knopf "Aufteilen" bzw. "Ändern", der Editor im Panel (PC) und im Sheet (Handy), Rest und
// Speichern-Sperre, die Auswahl je Zeile, das Übertrag-Ziel, der Toast mit Rückgängig, die
// gesperrten Fälle aus `SplitLocks` und die Hinweisleiste "Neuanlage offen". Die Fixtures tragen
// `SplitLocks` nur dort, wo ein Fall gesperrt ist: ihr Fehlen ist der Fall "älterer Server".
//
// Ausführen: node --test scripts/ledger-split-editor.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  CATEGORY_IDS,
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  fixtureGuid,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";

const CAT = {
  lebensmittel: { id: CATEGORY_IDS.lebensmittel, name: "Lebensmittel", group: "Alltag" },
  bahn: { id: CATEGORY_IDS.bahn, name: "Bahn", group: "Mobilität" },
  nebenkosten: { id: CATEGORY_IDS.nebenkosten, name: "Nebenkosten", group: "Wohnen" },
};

const RECONCILED = "Abgeglichene Buchungen lassen sich nicht aufteilen";
const FOREIGN = "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben";

/// Veränderlicher Server-Zustand der Mocks.
const state = { saveCalls: [], retryCalls: [], accountCalls: 0, writeBack: "CategorySent", saveResult: null };

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const toCategory = (cat, amount) => ({
  Target: { ToCategory: [{ YnabCategoryId: cat.id }, cat.name] },
  Amount: eur(amount),
  Memo: null,
});

function entries(accountId) {
  const base = (n, extra) =>
    buildEntryFixture(accountId, n, {
      Date: TODAY,
      LinkedToYnab: true,
      NeedsReview: true,
      Amount: eur(-81.9),
      CategorizationWriteBackFailure: null,
      ...extra,
    });
  const plain = { CategoryId: CAT.lebensmittel.id, CategoryName: "Lebensmittel", CategoryGroupName: "Alltag" };
  const split = {
    CategoryId: null,
    CategoryName: null,
    CategoryGroupName: null,
    SplitCategoryGroupNames: { [CAT.nebenkosten.id]: "Wohnen", [CAT.lebensmittel.id]: "Alltag" },
    Splits: [toCategory(CAT.nebenkosten, -23.4), toCategory(CAT.lebensmittel, -58.5)],
  };
  return [
    base(1, { Payee: "Bauhaus", ...plain }),
    base(2, { Payee: "Kaufland Split", ...split }),
    base(3, {
      Payee: "Abgleich Markt",
      ...plain,
      SplitLocks: { CannotSplitReason: RECONCILED, CannotChangeReason: RECONCILED, ReplacementPending: false },
    }),
    base(4, {
      Payee: "Fremd Split",
      ...split,
      SplitLocks: { CannotSplitReason: null, CannotChangeReason: FOREIGN, ReplacementPending: false },
    }),
    base(5, {
      Payee: "Fremd Einzeln",
      ...plain,
      SplitLocks: { CannotSplitReason: null, CannotChangeReason: FOREIGN, ReplacementPending: false },
    }),
    base(6, {
      Payee: "Neuanlage Markt",
      ...split,
      CategorizationWriteBackFailure: "YNAB nicht erreichbar",
      SplitLocks: { CannotSplitReason: null, CannotChangeReason: null, ReplacementPending: true },
    }),
  ];
}

const idOf = (n) => buildEntryFixture("x", n).Id;

const accountsList = () => [
  { Id: { YnabAccountId: fixtureGuid(7001) }, Name: "Tagesgeld", Balance: eur(0), OnBudget: true, Closed: false },
  { Id: { YnabAccountId: fixtureGuid(7002) }, Name: "Bargeld", Balance: eur(0), OnBudget: true, Closed: false },
  { Id: { YnabAccountId: fixtureGuid(7003) }, Name: "Altes Konto", Balance: eur(0), OnBudget: true, Closed: true },
  { Id: { YnabAccountId: fixtureGuid(7004) }, Name: "Depot Tracking", Balance: eur(0), OnBudget: false, Closed: false },
];

async function open(context) {
  const target = await context.newPage();
  target.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await target.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(target, {
    accountsFixture,
    balancesFixture,
    resolveEntries: () => entries(accountsFixture.DefaultAccountId),
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/getBudgetAccounts", (route) => {
    state.accountCalls += 1;
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: accountsList() }) });
  });
  await target.route("**/api/LedgerApi/getLedgerEntry", (route) =>
    route.fulfill({ contentType: "application/json", body: "null" })
  );
  await target.route("**/api/LedgerApi/setEntrySplits", (route) => {
    const [entryId, lines] = route.request().postDataJSON();
    // Beträge reisen als exakter Dezimaltext (ADR 0017): fürs Vergleichen in Zahlen wandeln.
    state.saveCalls.push({
      entryId,
      lines: lines.map((l) => ({ ...l, Amount: { ...l.Amount, Amount: Number(l.Amount.Amount) } })),
    });
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify(
        state.saveResult ?? { Ok: { Previous: null, Change: "CategoryChanged", WriteBack: state.writeBack } }
      ),
    });
  });
  await target.route("**/api/LedgerApi/retryCategorizationWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "CategorySent" }) });
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

function resetState() {
  state.saveCalls.length = 0;
  state.retryCalls.length = 0;
  state.accountCalls = 0;
  state.writeBack = "CategorySent";
  state.saveResult = null;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');
const splitField = (scope) => scope.locator('[data-testid="detail-split"]');
const splitAction = (scope) => scope.locator('[data-testid="detail-split-action"]');
const line = (scope, i) => scope.locator(`[data-testid="split-line-${i}"]`);
const restOf = (scope) => scope.locator('[data-testid="split-rest"]');
const save = (scope) => scope.getByRole("button", { name: "Speichern", exact: true });

async function rowIndex(page, text) {
  const rows = await page.locator("table tbody tr").allTextContents();
  return rows.findIndex((t) => t.includes(text));
}

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

async function pickCategory(page, scope, index, name) {
  await scope.locator(`[data-testid="split-target-${index}"]`).click();
  await page.locator('[role="listbox"]').locator('[role="option"]', { hasText: name }).last().click();
}

test("PC: Kein Split mit festem Knopf Aufteilen; Editor mit Rest, Speichern erst bei Rest 0,00 €, Zeile hinzufügen dann ausgegraut", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Bauhaus");
  const rowBefore = await rowIndex(page, "Bauhaus");
  assert.match(await splitField(panel(page)).textContent(), /Kein Split/);
  assert.equal((await splitAction(panel(page)).textContent()).trim(), "Aufteilen");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
  assert.equal((await panel(page).locator('[data-testid="split-total"]').textContent()).trim(), "81,90 €");
  assert.equal(await save(panel(page)).isDisabled(), true, "Speichern gesperrt bei Rest 81,90 €");
  assert.equal(await panel(page).getByRole("button", { name: "Abbrechen" }).count(), 1, "PC: Abbrechen da");

  await panel(page).locator('[data-testid="split-amount-0"]').fill("23,40");
  assert.equal((await restOf(panel(page)).textContent()).trim(), "58,50 €");
  assert.match(await restOf(panel(page)).getAttribute("class"), /text-neon-red/, "Rest ungleich null: Warnfarbe");
  assert.equal(await save(panel(page)).isDisabled(), true);

  await pickCategory(page, panel(page), 1, "Nebenkosten");
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /Nebenkosten/);
  assert.equal(await panel(page).getByRole("button", { name: "Zeile hinzufügen" }).isDisabled(), false, "Rest offen: Hinzufügen aktiv");
  await line(panel(page), 1).getByRole("button", { name: "Rest", exact: true }).click();
  assert.equal(await panel(page).locator('[data-testid="split-amount-1"]').inputValue(), "58,50");
  assert.equal((await restOf(panel(page)).textContent()).trim(), "0,00 €");
  assert.match(await restOf(panel(page)).getAttribute("class"), /text-text-primary/, "Rest 0,00 €: neutral");
  assert.equal((await panel(page).locator('[data-testid="split-summary"] span').nth(2).textContent()).trim(), "Stimmt");
  assert.equal(await panel(page).getByRole("button", { name: "Zeile hinzufügen" }).isDisabled(), true, "Rest 0,00: Hinzufügen ausgegraut");
  assert.equal(await save(panel(page)).isDisabled(), false, "Speichern aktiv");

  await save(panel(page)).click();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.equal(state.saveCalls.length, 1, "setEntrySplits genau einmal");
  assert.equal(state.saveCalls[0].entryId, idOf(1));
  assert.deepEqual(state.saveCalls[0].lines.map((l) => l.Amount.Amount), [-23.4, -58.5], "Vorzeichen der Buchung");
  assert.equal(await panel(page).locator('[data-testid="split-summary"]').count(), 0, "Editor zu");
  // Detail und Liste zeigen die neuen Splits, die Zeile bleibt an ihrem Platz und ungeprüft.
  assert.match(await splitField(panel(page)).textContent(), /Wohnen: Nebenkosten/);
  assert.equal((await splitAction(panel(page)).textContent()).trim(), "Ändern");
  assert.match(await row(page, "Bauhaus").textContent(), /2 Split-Zeilen/);
  assert.equal(await rowIndex(page, "Bauhaus"), rowBefore, "Platz in der Liste");
  assert.equal(await panel(page).locator('[role="switch"]').getAttribute("aria-checked"), "false", "ungeprüft");

  // Rückgängig: zweiter Befehl, eine Zeile der alten Kategorie über den vollen Betrag.
  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  for (let i = 0; i < 50 && state.saveCalls.length < 2; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.saveCalls[1].lines, [toCategory(CAT.lebensmittel, -81.9)]);
  await page.waitForFunction(() => /Kein Split/.test(document.querySelector('[data-testid="detail-split"]')?.textContent ?? ""));
  await context.close();
});

test("PC: Ändern öffnet die Zeilen, Zeile entfernen und Rest macht daraus eine Kategorie (Aufteilung aufgehoben)", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Kaufland Split");
  assert.match(await splitField(panel(page)).textContent(), /-23,40/, "Zeilen mit Vorzeichen");
  assert.equal((await splitAction(panel(page)).textContent()).trim(), "Ändern");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
  assert.equal(await panel(page).locator('[data-testid="split-amount-0"]').inputValue(), "23,40", "positiv im Editor");
  assert.equal(await save(panel(page)).isDisabled(), false, "unverändert speicherbar");
  await line(panel(page), 0).getByRole("button", { name: "Zeile entfernen" }).click();
  assert.equal(await save(panel(page)).isDisabled(), true, "Rest offen");
  await line(panel(page), 0).getByRole("button", { name: "Rest", exact: true }).click();
  assert.equal(await panel(page).locator('[data-testid="split-amount-0"]').inputValue(), "81,90");
  await save(panel(page)).click();
  await page.getByText("Aufteilung aufgehoben", { exact: true }).waitFor();
  assert.equal(state.saveCalls[0].lines.length, 1, "eine Zeile hebt auf");
  assert.equal(state.saveCalls[0].lines[0].Amount.Amount, -81.9, "voller Betrag");
  await page.waitForFunction(() => /Kein Split/.test(document.querySelector('[data-testid="detail-split"]')?.textContent ?? ""));
  assert.match(await row(page, "Kaufland Split").textContent(), /Alltag: Lebensmittel|Wohnen: Nebenkosten/);
  await context.close();
});

test("PC: Der Pfeil an der Kategorie einer Split-Buchung öffnet den Editor, Esc schließt erst die Auswahl, dann den Editor", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Kaufland Split");
  const arrow = panel(page).locator('[data-testid="detail-category-arrow"]');
  assert.equal(await arrow.getAttribute("aria-disabled"), "false", "Pfeil aktiv");
  await arrow.click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
  assert.equal(await page.locator("body").innerText().then((t) => t.includes("Split bearbeiten folgt")), false, "Hinweis entfernt");
  await panel(page).locator('[data-testid="split-target-0"]').click();
  await panel(page).locator('[role="listbox"]').waitFor();
  await page.keyboard.press("Escape");
  await panel(page).locator('[role="listbox"]').waitFor({ state: "detached" });
  assert.equal(await panel(page).locator('[data-testid="split-summary"]').count(), 1, "Editor bleibt");
  await page.keyboard.press("Escape");
  await panel(page).locator('[data-testid="split-summary"]').waitFor({ state: "detached" });
  assert.equal(await panel(page).count(), 1, "Detail bleibt");
  assert.equal(state.saveCalls.length, 0, "nichts gespeichert");
  await context.close();
});

test("PC: Übertrag-Ziel über den einen Picker, nur offene Budget-Konten, kein Sync-Knopf", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Bauhaus");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
  for (let i = 0; i < 50 && state.accountCalls === 0; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.equal(state.accountCalls, 1, "die Kontenliste wird einmal geholt");
  assert.equal(await line(panel(page), 1).getByRole("button", { name: "Übertrag-Ziel wählen" }).count(), 0, "kein Sync-Knopf");
  await panel(page).locator('[data-testid="split-target-1"]').click();
  const picker = panel(page).locator('[data-testid="category-picker-accounts"]');
  await picker.waitFor();
  const names = await picker.locator('[data-testid="category-picker-account-row"]').allTextContents();
  assert.deepEqual(names.map((n) => n.trim()), ["→ Tagesgeld", "→ Bargeld"], "kein geschlossenes, kein Tracking-Konto");
  // Der Abschnitt steht unter den Kategorien.
  const order = await page.evaluate(() => {
    const accounts = document.querySelector('[data-testid="category-picker-accounts"]').getBoundingClientRect();
    const last = [...document.querySelectorAll('[role="option"]:not([data-testid="category-picker-account-row"])')].pop().getBoundingClientRect();
    return { accounts: accounts.top, last: last.top };
  });
  assert.ok(order.accounts > order.last, "Konten unter den Kategorien");
  // Suche über Kategorien und Konten, Tastatur erreicht die Konto-Zeile.
  await panel(page).getByPlaceholder("Kategorie oder Konto suchen…").fill("Bar");
  await picker.locator('[data-testid="category-picker-account-row"]', { hasText: "→ Bargeld" }).waitFor();
  await page.keyboard.press("Enter");
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld/);
  await panel(page).locator('[data-testid="split-target-1"]').click();
  await panel(page).getByPlaceholder("Kategorie oder Konto suchen…").fill("Konto: Tages");
  await picker.locator('[data-testid="category-picker-account-row"]').waitFor();
  assert.equal(await panel(page).locator('[role="option"]').count(), 1, "nur das passende Konto");
  const box = await page.evaluate(() => {
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    const el = document.querySelector('[data-testid="category-picker-accounts"]').closest(".absolute").getBoundingClientRect();
    return { aL: aside.left, aR: aside.right, l: el.left, r: el.right };
  });
  assert.ok(box.l >= box.aL - 0.5 && box.r <= box.aR + 0.5, "Popover liegt im Panel");
  await picker.locator('[data-testid="category-picker-account-row"]', { hasText: "Tagesgeld" }).click();
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /→ Tagesgeld/);
  // Übertrag nach dem Speichern: ToTransfer in der Anfrage.
  await panel(page).locator('[data-testid="split-amount-0"]').fill("30");
  await line(panel(page), 1).getByRole("button", { name: "Rest", exact: true }).click();
  await save(panel(page)).click();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.ok(state.saveCalls[0].lines[1].Target.ToTransfer, "zweite Zeile ist ein Übertrag");
  assert.equal(state.saveCalls[0].lines[1].Amount.Amount, -51.9);
  await context.close();
});

test("PC: Abbrechen verwirft den Entwurf ohne Aufruf", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Bauhaus");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-amount-0"]').fill("10");
  await panel(page).getByRole("button", { name: "Abbrechen" }).click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor({ state: "detached" });
  assert.equal(state.saveCalls.length, 0);
  await splitAction(panel(page)).click();
  assert.equal(await panel(page).locator('[data-testid="split-amount-0"]').inputValue(), "", "neuer Entwurf");
  await context.close();
});

test("PC: Gesperrte Fälle (abgeglichen, fremde import_id) haben inaktive Knöpfe mit Hinweistext im Detail", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);

  await openDetail(page, "Abgleich Markt");
  assert.equal(await splitAction(panel(page)).getAttribute("aria-disabled"), "true", "Aufteilen inaktiv");
  assert.match(await panel(page).locator('[data-testid="detail-split-lock-hint"]').textContent(), /Abgeglichene Buchungen/);
  await splitAction(panel(page)).click({ force: true });
  assert.equal(await panel(page).locator('[data-testid="split-summary"]').count(), 0, "kein Editor");

  await openDetail(page, "Fremd Split");
  assert.equal(await splitAction(panel(page)).getAttribute("aria-disabled"), "true", "Ändern inaktiv");
  assert.equal(await panel(page).locator('[data-testid="detail-category-arrow"]').getAttribute("aria-disabled"), "true", "Pfeil inaktiv");
  assert.match(await panel(page).locator('[data-testid="detail-split-lock-hint"]').textContent(), /import_id aus YNAB/);
  await panel(page).locator('[data-testid="detail-category-arrow"]').click({ force: true });
  assert.equal(await panel(page).locator('[data-testid="split-summary"]').count(), 0, "kein Editor");

  // Nur Ändern ist gesperrt: Aufteilen einer unaufgeteilten Buchung bleibt aktiv, der Toast hat dann kein Rückgängig.
  await openDetail(page, "Fremd Einzeln");
  assert.equal(await splitAction(panel(page)).getAttribute("aria-disabled"), "false", "Aufteilen aktiv");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-amount-0"]').fill("20");
  await pickCategory(page, panel(page), 1, "Bahn");
  await line(panel(page), 1).getByRole("button", { name: "Rest", exact: true }).click();
  await save(panel(page)).click();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.equal(await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).count(), 0, "Rückgängig fehlt");
  await context.close();
});

test("PC: Neuanlage offen hat eine eigene Hinweisleiste, Erneut senden ruft retryCategorizationWriteBack", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Neuanlage Markt");
  const notice = panel(page).locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  assert.match(await notice.textContent(), /In YNAB gelöscht, Neuanlage offen/);
  assert.match(await notice.textContent(), /YNAB nicht erreichbar/);
  await notice.getByRole("button", { name: "Erneut senden" }).click();
  for (let i = 0; i < 50 && state.retryCalls.length === 0; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.equal(state.retryCalls.length, 1);
  assert.deepEqual(state.retryCalls[0], [idOf(6)]);
  await context.close();
});

test("Handy (390x844): Aufteilen im selben Sheet, Speichern klebt unten, kein Abbrechen, Auswahl als zweites Sheet", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("Bauhaus").first().tap();
  const detailSheet = page.locator(".bottom-sheet.active").first();
  await detailSheet.waitFor();
  const action = splitAction(detailSheet);
  const box = await action.boundingBox();
  assert.ok(box.height >= 44 - 0.5 && box.width >= 44 - 0.5, `Trefferfläche ${box.width}x${box.height} mindestens 44 px`);
  await action.tap();
  await detailSheet.locator('[data-testid="split-summary"]').waitFor();
  assert.match(await detailSheet.locator("h3").first().textContent(), /Aufteilen/);
  assert.match(await detailSheet.textContent(), /Bauhaus · \d\d\.\d\d\.\d{4}/, "Unterzeile Payee · Datum");
  assert.equal(await page.locator(".bottom-sheet.active").getByRole("button", { name: "Abbrechen" }).count(), 0, "kein Abbrechen");
  const footerButtons = await detailSheet.locator(".rule-sheet-footer button").allTextContents();
  assert.deepEqual(footerButtons.map((t) => t.trim()), ["Speichern"], "nur Speichern");
  assert.equal(await save(detailSheet).isDisabled(), true);
  // Das Sheet liegt im Bild, Speichern steht am unteren Rand.
  const geometry = await page.evaluate(() => {
    const sheet = document.querySelector(".bottom-sheet.active");
    const footer = sheet.querySelector(".rule-sheet-footer").getBoundingClientRect();
    return { footerBottom: footer.bottom, viewport: window.innerHeight };
  });
  assert.ok(geometry.footerBottom <= geometry.viewport + 1 && geometry.footerBottom >= geometry.viewport - 40, "Speichern klebt unten");

  await detailSheet.locator('[data-testid="split-amount-0"]').fill("23,40");
  await detailSheet.locator('[data-testid="split-target-1"]').tap();
  const pickerSheet = page.locator(".bottom-sheet.layer-2.active", { hasText: "Ziel wählen" });
  await pickerSheet.waitFor();
  assert.equal(await page.locator(".bottom-sheet.active").count(), 2, "zwei Sheets");
  await pickerSheet.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
  // Nach der Wahl im Sheet schluckt `Viewport.swallowNextClick` 350 ms lang einen späteren Klick (ADR 0005).
  await page.waitForTimeout(450);
  await line(detailSheet, 1).getByRole("button", { name: "Rest", exact: true }).tap();
  assert.equal(await save(detailSheet).isDisabled(), false);
  // Übertrag-Ziel am Handy: derselbe Picker als zweites Sheet, "Bar" findet das Konto.
  await detailSheet.locator('[data-testid="split-target-1"]').tap();
  await pickerSheet.waitFor();
  const accountRows = pickerSheet.locator('[data-testid="category-picker-account-row"]');
  assert.equal(await accountRows.count(), 2, "zwei offene Budget-Konten");
  await pickerSheet.getByPlaceholder("Kategorie oder Konto suchen…").fill("Bar");
  await accountRows.filter({ hasText: "→ Bargeld" }).tap();
  assert.match(await line(detailSheet, 1).locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld/);
  await page.waitForTimeout(450);
  await save(detailSheet).tap();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.equal(state.saveCalls.length, 1);
  await context.close();
});

test("/styleguide: der Split-Editor zeigt den Picker mit Konto-Abschnitt", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  await page.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  const demo = page.locator('[data-ds-demo="split-editor"]');
  await demo.waitFor();
  assert.equal(await demo.locator('[data-testid^="split-line-"]').count(), 2, "zwei Zeilen");
  assert.equal(await demo.locator('[data-testid="split-summary"]').count(), 1, "Summe");
  assert.equal(await page.locator('[data-ds-demo="split-target-picker-sheet-trigger"]').count(), 1, "Ziel-Picker als Sheet");
  await demo.locator('[data-testid="split-target-0"]').click();
  await demo.locator('[data-testid="category-picker-accounts"]').getByText("→ Bargeld").waitFor();
  assert.equal(await page.locator('[data-ds-card="components-detail-fields"] [data-testid="detail-split-action"]').count(), 3, "Knöpfe im Feld Split");
  await context.close();
});
