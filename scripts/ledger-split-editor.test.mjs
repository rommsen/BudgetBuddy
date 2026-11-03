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
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
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
  return [
    base(1, { Payee: "Bauhaus", ...plain }),
  ];
}


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
const splitAction = (scope) => scope.locator('[data-testid="detail-split-action"]');
const line = (scope, i) => scope.locator(`[data-testid="split-line-${i}"]`);
const restOf = (scope) => scope.locator('[data-testid="split-rest"]');
const save = (scope) => scope.getByRole("button", { name: "Speichern", exact: true });

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

/// Nach der Wahl im Sheet schluckt `Viewport.swallowNextClick` bis zu 350 ms lang genau einen Klick
/// (ADR 0005). Ein geschluckter Tipp ändert nichts, also bis zur Wirkung wiederholen.
async function tapUntil(target, done) {
  for (let attempt = 0; attempt < 5 && !(await done()); attempt += 1) {
    await target.tap();
    for (let i = 0; i < 20 && !(await done()); i += 1) await new Promise((resolve) => setTimeout(resolve, 25));
  }
}

test("[flow:aufteilen] PC: Aufteilen mit Übertrag über den Picker im Panel: Tastatur, Lage des Popovers, Vorzeichen und Rückgängig", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Bauhaus");
  await splitAction(panel(page)).click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
  await panel(page).locator('[data-testid="split-amount-0"]').fill("30");

  await panel(page).locator('[data-testid="split-target-1"]').click();
  const picker = panel(page).locator('[data-testid="category-picker-accounts"]');
  await picker.waitFor();
  // Der Abschnitt steht unter den Kategorien, das Popover ganz im Panel.
  const at = await page.evaluate(() => {
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    const accounts = document.querySelector('[data-testid="category-picker-accounts"]');
    const popover = accounts.closest(".absolute").getBoundingClientRect();
    const last = [...document.querySelectorAll('[role="option"]:not([data-testid="category-picker-account-row"])')].pop().getBoundingClientRect();
    return { accounts: accounts.getBoundingClientRect().top, last: last.top, aL: aside.left, aR: aside.right, l: popover.left, r: popover.right };
  });
  assert.ok(at.accounts > at.last, "Konten unter den Kategorien");
  assert.ok(at.l >= at.aL - 0.5 && at.r <= at.aR + 0.5, "Popover liegt im Panel");

  // Die Tastatur erreicht die Konto-Zeile.
  await panel(page).getByPlaceholder("Kategorie oder Konto suchen…").fill("Bar");
  await picker.locator('[data-testid="category-picker-account-row"]', { hasText: "→ Bargeld" }).waitFor();
  await page.keyboard.press("Enter");
  await panel(page).locator('[data-testid="split-target-1"]', { hasText: "→ Bargeld" }).waitFor();

  await line(panel(page), 1).getByRole("button", { name: "Rest", exact: true }).click();
  assert.equal((await restOf(panel(page)).textContent()).trim(), "0,00 €");
  await save(panel(page)).click();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.equal(state.saveCalls.length, 1, "setEntrySplits genau einmal");
  assert.ok(state.saveCalls[0].lines[1].Target.ToTransfer, "zweite Zeile ist ein Übertrag");
  assert.equal(state.saveCalls[0].lines[1].Amount.Amount, -51.9, "Vorzeichen der Buchung");
  assert.match(await row(page, "Bauhaus").textContent(), /2 Split-Zeilen/);

  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  await pollUntil(() => state.saveCalls.length >= 2);
  assert.deepEqual(state.saveCalls[1].lines, [toCategory(CAT.lebensmittel, -81.9)]);
  await context.close();
});

test("[layout] Handy (390x844): Aufteilen im selben Sheet, Speichern klebt unten, kein Abbrechen, Auswahl als zweites Sheet", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("Bauhaus").first().tap();
  const detailSheet = page.locator(".bottom-sheet.active").first();
  await detailSheet.waitFor();
  const action = splitAction(detailSheet);
  for (const target of [action, detailSheet.locator('[data-testid="detail-memo-action"]')]) {
    const box = await target.boundingBox();
    assert.ok(box.height >= 44 - 0.5 && box.width >= 44 - 0.5, `Trefferfläche ${box.width}x${box.height} mindestens 44 px`);
  }
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
  await tapUntil(line(detailSheet, 1).getByRole("button", { name: "Rest", exact: true }), async () => save(detailSheet).isEnabled());
  assert.equal(await save(detailSheet).isDisabled(), false);
  // Übertrag-Ziel am Handy: derselbe Picker als zweites Sheet, "Bar" findet das Konto.
  await detailSheet.locator('[data-testid="split-target-1"]').tap();
  await pickerSheet.waitFor();
  const accountRows = pickerSheet.locator('[data-testid="category-picker-account-row"]');
  assert.equal(await accountRows.count(), 2, "zwei offene Budget-Konten");
  await pickerSheet.getByPlaceholder("Kategorie oder Konto suchen…").fill("Bar");
  await accountRows.filter({ hasText: "→ Bargeld" }).tap();
  assert.match(await line(detailSheet, 1).locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld/);
  await tapUntil(save(detailSheet), async () => state.saveCalls.length > 0);
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  assert.equal(state.saveCalls.length, 1);
  await context.close();
});

