// scripts/ledger-lena.test.mjs
//
// Wächter für `ledger-xfjcp` (ADR 0056): Lena fragen im Kassenbuch-Detail. Gemessen am echten, per
// Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (nie die echte Entwicklungsdatenbank):
// die Flag-Zeile mit "Lena fragen" bzw. "Rückfrage zurücknehmen", die gesperrte Aktion ohne Farbe,
// der Toast mit Rückgängig, die Hinweisleiste "Flag nicht in YNAB" mit Erneut senden, der Chip
// "Rückfrage Lena (n)" samt ausgegrauter Zeile nach dem Zurücknehmen und die Einstellung.
//
// Ausführen: node --test scripts/ledger-lena.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";

/// Veränderlicher Server-Zustand der Mocks.
const state = {
  lenaColor: "purple",
  suggestion: null,
  setCalls: [],
  restoreCalls: [],
  retryCalls: [],
  pageQueries: [],
  flagOf: {},
  failureOf: {},
  setWriteBack: "FlagSent",
  savedColors: [],
};

const idOf = (n) => buildEntryFixture("x", n).Id;

function entries(accountId) {
  return [1, 2, 3].map((n) =>
    buildEntryFixture(accountId, n, {
      Date: TODAY,
      LinkedToYnab: true,
      NeedsReview: false,
      Payee: ["Rewe Lena", "Bahn Lena", "Kiosk Lena"][n - 1],
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Flag: state.flagOf[n] ?? "NoFlag",
      FlagWriteBackFailure: state.failureOf[n] ?? null,
    })
  );
}

async function open(context, { onlyLenaFilter = false } = {}) {
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
  await target.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const [query] = route.request().postDataJSON();
    state.pageQueries.push(query);
    let list = entries(accountsFixture.DefaultAccountId);
    if (query.OnlyAskLena) list = list.filter((e) => e.Flag === "AskLena");
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: list,
        TotalCount: list.length,
        TotalAmount: { Amount: list.reduce((s, e) => s + e.Amount.Amount, 0), Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: 0,
        AskLenaCount: entries(accountsFixture.DefaultAccountId).filter((e) => e.Flag === "AskLena").length,
      }),
    });
  });
  await target.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ SavedColor: state.lenaColor, SuggestedColor: state.suggestion }),
    })
  );
  await target.route("**/api/SettingsApi/saveLenaFlagColor", (route) => {
    const [color] = route.request().postDataJSON();
    state.savedColors.push(color);
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: null }) });
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/setLenaFlag", (route) => {
    const [entryId, flagTarget] = route.request().postDataJSON();
    state.setCalls.push({ entryId, flagTarget });
    const assign = flagTarget === "AssignLena";
    const previous = assign ? { Color: null, Name: null } : { Color: state.lenaColor, Name: "Lena" };
    state.flagOf[Object.keys(state.flagOf).find((n) => idOf(Number(n)) === entryId) ?? entryIndex(entryId)] = assign
      ? "AskLena"
      : "NoFlag";
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: previous, Change: "FlagChanged", WriteBack: state.setWriteBack } }),
    });
  });
  await target.route("**/api/LedgerApi/restoreFlag", (route) => {
    state.restoreCalls.push(route.request().postDataJSON());
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: { Color: "purple", Name: "Lena" }, Change: "FlagChanged", WriteBack: "FlagSent" } }),
    });
  });
  await target.route("**/api/LedgerApi/retryFlagWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "FlagSent" }) });
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

function entryIndex(entryId) {
  return [1, 2, 3].find((n) => idOf(n) === entryId);
}

function resetState() {
  state.lenaColor = "purple";
  state.suggestion = null;
  state.setCalls.length = 0;
  state.restoreCalls.length = 0;
  state.retryCalls.length = 0;
  state.pageQueries.length = 0;
  state.savedColors.length = 0;
  state.flagOf = {};
  state.failureOf = {};
  state.setWriteBack = "FlagSent";
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
const flagField = (scope) => scope.locator('[data-testid="detail-flag"]');
const flagAction = (scope) => scope.locator('[data-testid="detail-flag-action"]');

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

test("PC: Lena fragen setzt das Kennzeichen, der Knopf wechselt, der Toast bietet Rückgängig", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Lena");
  assert.equal((await flagField(panel(page)).textContent()).includes("Kein Flag"), true, "Wert: Kein Flag");
  assert.equal((await flagAction(panel(page)).textContent()).trim(), "Lena fragen");
  assert.equal(await flagAction(panel(page)).getAttribute("aria-disabled"), "false");

  await flagAction(panel(page)).click();
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="detail-flag-action"]')?.textContent?.trim() === "Rückfrage zurücknehmen"
  );
  assert.deepEqual(state.setCalls, [{ entryId: idOf(1), flagTarget: "AssignLena" }]);
  assert.ok((await flagField(panel(page)).textContent()).includes("Rückfrage Lena"), "Wert: Rückfrage Lena");
  assert.ok((await row(page, "Rewe Lena").textContent()).includes("Lena?"), "Kennzeichen in der Zeile");
  const toast = page.getByText("Lena gefragt", { exact: true });
  await toast.waitFor();

  await page.getByRole("button", { name: "Rückgängig" }).click();
  await page.waitForTimeout(300);
  assert.equal(state.restoreCalls.length, 1, "restoreFlag aufgerufen");
  assert.deepEqual(state.restoreCalls[0][0], idOf(1));
  await context.close();
});

test("PC: ohne gesetzte Farbe ist Lena fragen gesperrt und nennt den Grund", async () => {
  resetState();
  state.lenaColor = null;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Lena");
  assert.equal(await flagAction(panel(page)).getAttribute("aria-disabled"), "true");
  assert.ok(
    (await panel(page).locator('[data-testid="detail-flag-lock-hint"]').textContent()).includes(
      "In den Settings die Flag-Farbe für Lena wählen"
    )
  );
  await flagAction(panel(page)).click({ force: true });
  await page.waitForTimeout(200);
  assert.equal(state.setCalls.length, 0, "kein Aufruf");
  await context.close();
});

test("PC: Flag nicht in YNAB zeigt Grund und Erneut senden", async () => {
  resetState();
  state.flagOf = { 1: "AskLena" };
  state.failureOf = { 1: "Keine Verbindung zu YNAB" };
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Lena");
  const notice = panel(page).getByText("Flag nicht in YNAB");
  await notice.waitFor();
  assert.ok(await panel(page).getByText("Keine Verbindung zu YNAB").isVisible());
  state.failureOf = {};
  await panel(page).getByRole("button", { name: "Erneut senden" }).click();
  await page.waitForTimeout(300);
  assert.deepEqual(state.retryCalls, [[idOf(1)]]);
  await context.close();
});

test("PC: Chip Rückfrage Lena (n) filtert, die zurückgenommene Zeile bleibt ausgegraut stehen", async () => {
  resetState();
  state.flagOf = { 1: "AskLena", 2: "AskLena" };
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  const chip = page.getByRole("button", { name: /Rückfrage Lena \(2\)/ });
  await chip.waitFor();
  await chip.click();
  await page.waitForFunction(() => document.querySelectorAll("table tbody tr").length === 2 || true);
  await page.waitForTimeout(500);
  assert.equal(await page.locator("table tbody tr", { hasText: "Kiosk Lena" }).count(), 0, "Zeile ohne Flag fehlt in der gefilterten Liste");
  assert.ok(state.pageQueries.some((q) => q.OnlyAskLena === true), "Seitenanfrage mit OnlyAskLena");

  await openDetail(page, "Rewe Lena");
  assert.equal((await flagAction(panel(page)).textContent()).trim(), "Rückfrage zurücknehmen");
  await flagAction(panel(page)).click();
  const note = page.locator('table [data-testid="filtered-out-note"]');
  await note.waitFor();
  assert.equal((await note.textContent()).trim(), "passt nicht mehr zum Filter");
  assert.equal(await page.locator("table tbody tr").count() >= 2, true, "Zeile bleibt in der Liste");
  await context.close();
});

test("Settings: die Flag-Farbe für Lena wird mit Vorschlag vorbelegt und gespeichert", async () => {
  resetState();
  state.lenaColor = null;
  state.suggestion = "blue";
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/SettingsApi/getSettings", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Comdirect: null, Ynab: null, Sync: { DaysToFetch: 30 } }),
    })
  );
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ SavedColor: state.lenaColor, SuggestedColor: state.suggestion }),
    })
  );
  await page.route("**/api/SettingsApi/saveLenaFlagColor", (route) => {
    const [color] = route.request().postDataJSON();
    state.savedColors.push(color);
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: null }) });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/settings`, { waitUntil: "load" });
  const card = page.locator('[data-testid="settings-lena-card"]');
  await card.waitFor();
  assert.ok((await card.textContent()).includes("Nicht gewählt"));
  assert.ok((await card.textContent()).includes("Vorschlag aus dem Spiegel: Blau"));
  await card.getByRole("button", { name: "Bearbeiten" }).click();
  const select = page.locator('[data-testid="settings-lena-form"] select');
  assert.equal(await select.inputValue(), "blue", "Vorschlag vorbelegt");
  const options = await select.locator("option").allTextContents();
  assert.deepEqual(options, ["Nicht gewählt", "Rot", "Orange", "Gelb", "Grün", "Blau", "Violett"]);
  await select.selectOption("purple");
  await page.getByRole("button", { name: "Einstellungen speichern" }).click();
  await page.waitForTimeout(300);
  assert.deepEqual(state.savedColors, ["purple"]);
  await context.close();
});
