// scripts/ledger-category-change.test.mjs
//
// Wächter für `ledger-sfab0` (ADR 0055 und Nachtrag): Kategorie ändern im Kassenbuch-Detail.
// Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten
// (Muster wie `ledger-detail.test.mjs`, nie die echte Entwicklungsdatenbank): der Pfeil an der
// Kategorie, der Picker als Popover im Panel und als zweites Sheet über dem Detail-Sheet, die
// Karte "Budget nach dem Wechsel", der Toast mit Rückgängig, die gesperrten Fälle, die
// Hinweisleiste "Kategorie nicht in YNAB", die ausgegraute Zeile und die Leiste "Neu laden".
//
// Ausführen: node --test scripts/ledger-category-change.test.mjs
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
  entriesPage,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

// Berliner Kalendertag: mit dem UTC-Tag lag die Buchung in der ersten Stunde eines Monats im Vormonat,
// und die Karte zeigte keine Beträge.
const TODAY = new Date().toLocaleDateString("sv-SE", { timeZone: "Europe/Berlin" }) + "T00:00:00.0000000";

/// Veränderlicher Server-Zustand der Mocks, damit die Tests Antworten umschalten können.
const state = {
  setCalls: [],
  suggestionCalls: [],
  retryCalls: [],
  queries: [],
  writeBack: "CategorySent",
  categoryOf: {},
  failureOf: {},
};

const CAT = {
  lebensmittel: { id: CATEGORY_IDS.lebensmittel, name: "Lebensmittel" },
  bahn: { id: CATEGORY_IDS.bahn, name: "Bahn" },
  nebenkosten: { id: CATEGORY_IDS.nebenkosten, name: "Nebenkosten" },
};
const GROUP = { [CATEGORY_IDS.lebensmittel]: "Alltag", [CATEGORY_IDS.bahn]: "Mobilität", [CATEGORY_IDS.nebenkosten]: "Wohnen" };

function entries(accountId) {
  const base = (n, extra) =>
    buildEntryFixture(accountId, n, { Date: TODAY, LinkedToYnab: true, CategorizationWriteBackFailure: null, ...extra });
  const withCategory = (n, key, extra = {}) => {
    const categoryId = state.categoryOf[n] === undefined ? CAT[key].id : state.categoryOf[n];
    const name = categoryId === null ? null : Object.values(CAT).find((c) => c.id === categoryId).name;
    return base(n, {
      CategoryId: categoryId,
      CategoryName: name,
      CategoryGroupName: categoryId === null ? null : GROUP[categoryId],
      CategorizationWriteBackFailure: state.failureOf[n] ?? null,
      ...extra,
    });
  };
  return [
    withCategory(1, "lebensmittel", { Payee: "REWE Mitte" }),
    withCategory(2, "bahn", { Payee: "Deutsche Bahn" }),
    base(3, { Payee: "Girokonto Übertrag", CategoryId: null, CategoryName: null, CategoryGroupName: null, IsTransfer: true }),
    base(4, { Payee: "Café Lotte", CategoryId: CAT.bahn.id, CategoryName: "Bahn", CategoryGroupName: "Mobilität", DeletedInYnab: true }),
    base(5, {
      Payee: "Kaufland Split",
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Splits: [
        { Target: { ToCategory: [{ YnabCategoryId: CAT.lebensmittel.id }, "Lebensmittel"] }, Amount: { Amount: -10, Currency: "EUR" }, Memo: null },
        { Target: "Uncategorized", Amount: { Amount: -5, Currency: "EUR" }, Memo: null },
      ],
    }),
  ];
}

const idOf = (n) => buildEntryFixture("x", n).Id;

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
    resolveEntries: (query) => {
      state.queries.push(query);
      let list = entries(accountsFixture.DefaultAccountId);
      if (query.CategoryIds && query.CategoryIds.length > 0) {
        list = list.filter((e) => query.CategoryIds.includes(e.CategoryId));
      }
      return list;
    },
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) => {
    state.suggestionCalls.push(route.request().postDataJSON());
    route.fulfill({ contentType: "application/json", body: JSON.stringify([{ YnabCategoryId: CAT.bahn.id }]) });
  });
  await target.route("**/api/LedgerApi/setEntryCategory", (route) => {
    const [entryId, choice] = route.request().postDataJSON();
    state.setCalls.push({ entryId, choice });
    const n = [1, 2, 3, 4, 5].find((k) => idOf(k) === entryId);
    const previousId = state.categoryOf[n] === undefined ? (n === 2 ? CAT.bahn.id : CAT.lebensmittel.id) : state.categoryOf[n];
    const previous = previousId === null ? null : (({ id, name }) => ({ CategoryId: { YnabCategoryId: id }, CategoryName: name }))(Object.values(CAT).find((c) => c.id === previousId));
    state.categoryOf[n] = choice === null ? null : choice.YnabCategoryId;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: previous, Change: "CategoryChanged", WriteBack: state.writeBack } }),
    });
  });
  await target.route("**/api/LedgerApi/retryCategorizationWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    state.failureOf = {};
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "CategorySent" }) });
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

function resetState() {
  state.setCalls.length = 0;
  state.suggestionCalls.length = 0;
  state.retryCalls.length = 0;
  state.queries.length = 0;
  state.writeBack = "CategorySent";
  state.categoryOf = {};
  state.failureOf = {};
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
const categoryRow = (scope) => scope.locator('[data-testid="detail-category"]');
const arrow = (scope) => scope.locator('[data-testid="detail-category-arrow"]');

test("[flow:kategorie-aendern] PC (1440): Pfeil öffnet den Picker im Panel, Auswahl ändert, Karte und Toast erscheinen, Rückgängig stellt zurück", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  assert.match(await categoryRow(panel(page)).textContent(), /Alltag: Lebensmittel/);

  await arrow(panel(page)).click();
  const listbox = panel(page).locator('[role="listbox"]');
  await listbox.waitFor();
  // Vorschläge: genau ein Aufruf mit Payee und aktueller Kategorie.
  await listbox.getByText(/Vorgeschlagen/i).first().waitFor();
  assert.equal(state.suggestionCalls.length, 1, "ein Aufruf je Öffnung");
  assert.deepEqual(state.suggestionCalls[0], ["REWE Mitte", { YnabCategoryId: CAT.lebensmittel.id }]);
  assert.match(await listbox.textContent(), /Vorgeschlagen/i, "Abschnitt VORGESCHLAGEN");

  // Das Popover liegt ganz im Panel, nicht abgeschnitten.
  const geometry = await page.evaluate(() => {
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    const box = document.querySelector('[data-testid="ledger-detail-panel"] [role="listbox"]').closest(".absolute").getBoundingClientRect();
    return { asideLeft: aside.left, asideRight: aside.right, left: box.left, right: box.right };
  });
  assert.ok(geometry.left >= geometry.asideLeft - 0.5 && geometry.right <= geometry.asideRight + 0.5, `Popover (${geometry.left}..${geometry.right}) im Panel (${geometry.asideLeft}..${geometry.asideRight})`);

  await listbox.locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  await page.locator('[data-testid="category-change-card"]').waitFor();
  assert.deepEqual(state.setCalls.map((c) => c.choice), [{ YnabCategoryId: CAT.nebenkosten.id }]);
  assert.equal(await panel(page).locator('[role="listbox"]').count(), 0, "Picker zu");
  assert.match(await categoryRow(panel(page)).textContent(), /Wohnen: Nebenkosten/, "Detail zeigt die neue Kategorie");
  assert.match(await row(page, "REWE Mitte").textContent(), /Wohnen: Nebenkosten/, "Liste ebenso");
  const card = panel(page).locator('[data-testid="category-change-card"]');
  assert.match(await card.textContent(), /Budget nach dem Wechsel/);
  assert.match(await card.textContent(), /120,50/, "alter Wert der alten Kategorie");
  // Toast mit Rückgängig, ohne Beträge.
  const toastText = page.getByText("Kategorie geändert: Nebenkosten", { exact: true });
  await toastText.waitFor();
  assert.doesNotMatch(await toastText.textContent(), /\d+,\d\d/, "keine Beträge im Toast");
  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  for (let i = 0; i < 50 && state.setCalls.length < 2; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.setCalls[1].choice, { YnabCategoryId: CAT.lebensmittel.id }, "Rückgängig sendet die alte Kategorie");
  await page.waitForFunction(() => /Alltag: Lebensmittel/.test(document.querySelector('[data-testid="detail-category"]')?.textContent ?? ""));
  assert.equal(await page.locator('[data-testid="category-change-card"]').count(), 0, "Rücknahme zeigt keine neue Karte");
  await context.close();
});
