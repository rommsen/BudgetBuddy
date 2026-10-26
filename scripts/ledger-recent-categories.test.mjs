// scripts/ledger-recent-categories.test.mjs
//
// Wächter für `ledger-d4msf`: Abschnitt "Zuletzt verwendet" im Kategorie-Picker des Kassenbuchs.
// Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster
// wie `ledger-category-change.test.mjs`): am PC (Popover im Detail-Panel) und am Handy (Sheet
// über dem Detail) steht die zuletzt vergebene Kategorie beim nächsten Öffnen oben im Abschnitt;
// ohne bisherige Kategorien fehlt er; "Kategorie setzen" für die Auswahl zeigt dieselbe Liste.
//
// Ausführen: node --test scripts/ledger-recent-categories.test.mjs
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
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";
const NAMES = { [CATEGORY_IDS.lebensmittel]: "Lebensmittel", [CATEGORY_IDS.bahn]: "Bahn", [CATEGORY_IDS.nebenkosten]: "Nebenkosten" };
const GROUPS = { [CATEGORY_IDS.lebensmittel]: "Alltag", [CATEGORY_IDS.bahn]: "Mobilität", [CATEGORY_IDS.nebenkosten]: "Wohnen" };

const calls = { set: [], batch: [] };
let categoryOf = {};

function entries(accountId) {
  const make = (n, payee, fallback) => {
    const id = categoryOf[n] ?? fallback;
    return buildEntryFixture(accountId, n, {
      Date: TODAY,
      Payee: payee,
      LinkedToYnab: true,
      CategorizationWriteBackFailure: null,
      CategoryId: id,
      CategoryName: NAMES[id],
      CategoryGroupName: GROUPS[id],
    });
  };
  return [
    make(1, "REWE Mitte", CATEGORY_IDS.lebensmittel),
    make(2, "Deutsche Bahn", CATEGORY_IDS.bahn),
    make(3, "Stadtwerke", CATEGORY_IDS.bahn),
  ];
}

const idOf = (n) => buildEntryFixture("x", n).Id;

async function open(context, seededRecent) {
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  if (seededRecent) {
    await page.addInitScript((value) => {
      if (!window.sessionStorage.getItem("d4msf-seeded")) {
        window.localStorage.setItem("bb.ledger.recentCategoryFilter", value);
        window.sessionStorage.setItem("d4msf-seeded", "1");
      }
    }, seededRecent);
  }
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, {
    accountsFixture,
    balancesFixture,
    resolveEntries: () => entries(accountsFixture.DefaultAccountId),
  });
  await page.route("**/api/LedgerApi/getEntryHistory", (route) => route.fulfill({ contentType: "application/json", body: "[]" }));
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await page.route("**/api/LedgerApi/setEntryCategory", (route) => {
    const [entryId, choice] = route.request().postDataJSON();
    calls.set.push({ entryId, choice });
    const n = [1, 2, 3].find((k) => idOf(k) === entryId);
    categoryOf[n] = choice === null ? null : choice.YnabCategoryId;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: null, Change: "CategoryChanged", WriteBack: "CategorySent" } }),
    });
  });
  await page.route("**/api/LedgerApi/setEntryCategoryBatch", (route) => {
    const [selection, target] = route.request().postDataJSON();
    calls.batch.push({ selection, target });
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Changed: [], UnchangedCount: 0, Skipped: [] } }),
    });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return page;
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
const arrow = (scope) => scope.locator('[data-testid="detail-category-arrow"]');
const recentIn = (scope) => scope.locator('[data-testid="category-picker-recent"]');

test("PC (1440): ohne bisherige Kategorien fehlt der Abschnitt, nach dem Vergeben steht die Kategorie oben unter Zuletzt verwendet", async () => {
  categoryOf = {};
  calls.set.length = 0;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  await arrow(panel(page)).click();
  const listbox = panel(page).locator('[role="listbox"]');
  await listbox.waitFor();
  assert.equal(await recentIn(listbox).count(), 0, "ohne bisherige Kategorien kein Abschnitt");
  await listbox.locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  await listbox.waitFor({ state: "detached" });
  assert.deepEqual(calls.set.map((c) => c.choice), [{ YnabCategoryId: CATEGORY_IDS.nebenkosten }]);

  // Nächste Buchung: die gerade vergebene Kategorie steht oben im Abschnitt.
  await row(page, "Deutsche Bahn").locator("td").nth(2).click();
  await arrow(panel(page)).click();
  await listbox.waitFor();
  const recent = recentIn(listbox);
  await recent.waitFor();
  assert.match(await recent.textContent(), /Zuletzt verwendet/);
  const options = recent.locator('[role="option"]');
  assert.match(await options.first().textContent(), /Wohnen: Nebenkosten/, "neueste zuerst");

  // Nach einer weiteren Wahl rückt diese an die Spitze, die ältere folgt.
  await listbox.locator('[role="option"]', { hasText: "Lebensmittel" }).last().click();
  await listbox.waitFor({ state: "detached" });
  await row(page, "Stadtwerke").locator("td").nth(2).click();
  await arrow(panel(page)).click();
  await listbox.waitFor();
  const texts = await recentIn(listbox).locator('[role="option"]').allTextContents();
  assert.match(texts[0], /Lebensmittel/, "neueste an der Spitze");
  assert.match(texts[1], /Nebenkosten/, "ältere dahinter");
  await context.close();
});

test("PC (1440): Kategorie setzen für die Auswahl zeigt dieselbe Liste und merkt die Wahl", async () => {
  categoryOf = {};
  calls.batch.length = 0;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context, CATEGORY_IDS.bahn);
  const box = (payee) => page.getByRole("checkbox", { name: `${payee} auswählen` }).filter({ visible: true });
  await box("REWE Mitte").click();
  await box("Deutsche Bahn").click();
  const bar = page.locator(".selection-bar").filter({ visible: true });
  await bar.waitFor();
  await bar.getByRole("button", { name: "Kategorie setzen", exact: true }).click();
  const listbox = page.locator('[role="listbox"]').filter({ visible: true });
  await listbox.waitFor();
  const recent = recentIn(listbox);
  await recent.waitFor();
  assert.match(await recent.locator('[role="option"]').first().textContent(), /Mobilität: Bahn/, "gespeicherte Kategorie oben");
  await listbox.locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  for (let i = 0; i < 50 && calls.batch.length === 0; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.equal(calls.batch.length, 1);
  const stored = await page.evaluate(() => window.localStorage.getItem("bb.ledger.recentCategoryFilter"));
  assert.equal(stored.split(",")[0], CATEGORY_IDS.nebenkosten, "die Auswahl-Wahl steht an der Spitze der gemeinsamen Liste");
  assert.equal(stored.split(",")[1], CATEGORY_IDS.bahn);
  await context.close();
});

test("Handy (390x844): im Sheet über dem Detail steht die vergebene Kategorie beim nächsten Öffnen oben unter Zuletzt verwendet", async () => {
  categoryOf = {};
  calls.set.length = 0;
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("REWE Mitte").first().tap();
  const detailSheet = page.locator(".bottom-sheet.active").first();
  await detailSheet.waitFor();
  await arrow(detailSheet).tap();
  const pickerSheet = page.locator(".bottom-sheet.active", { hasText: "Kategorie wählen" });
  await pickerSheet.waitFor();
  assert.equal(await recentIn(pickerSheet).count(), 0, "ohne bisherige Kategorien kein Abschnitt");
  await pickerSheet.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 1);
  assert.equal(calls.set.length, 1);
  // Detail schließen und eine andere Buchung öffnen.
  await page.keyboard.press("Escape");
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
  await page.waitForTimeout(500);
  await page.getByText("Deutsche Bahn").first().tap();
  const second = page.locator(".bottom-sheet.active").first();
  await second.waitFor();
  await arrow(second).tap();
  const picker2 = page.locator(".bottom-sheet.active", { hasText: "Kategorie wählen" });
  await picker2.waitFor();
  const recent = recentIn(picker2);
  await recent.waitFor();
  assert.match(await recent.locator('[role="option"]').first().textContent(), /Wohnen: Nebenkosten/);
  await context.close();
});
