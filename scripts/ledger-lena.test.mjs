// scripts/ledger-lena.test.mjs
//
// Wächter für `ledger-xfjcp` (ADR 0056): Lena fragen im Kassenbuch-Detail. Gemessen am echten, per
// Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (nie die echte Entwicklungsdatenbank):
// der Knopf "Lena fragen" bzw. "Rückfrage zurücknehmen" in der Knopfzeile (ledger-zfj78), die gesperrte Aktion ohne Farbe,
// der gewechselte Knopf ohne Toast (ledger-enjex), die Hinweisleiste "Flag nicht in YNAB" mit Erneut senden, der Chip
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
        AskLenaCount: entries(accountsFixture.DefaultAccountId).filter((e) => e.Flag === "AskLena").length, WithoutCategoryCount: 0,
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
const flagAction = (scope) => scope.locator('[data-testid="detail-flag-action"]');

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

test("[flow:lena-fragen] PC: Lena fragen setzt das Kennzeichen, der Knopf wechselt und nimmt die Rückfrage zurück, kein Toast", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Lena");
  // ledger-zfj78: die Flag-Zeile ist entfallen, den Stand zeigen der Knopf der Knopfzeile und die Marke in der Zeile.
  assert.equal(await row(page, "Rewe Lena").getByTestId("lena-mark").count(), 0, "Stand: keine Rückfrage");
  assert.equal((await flagAction(panel(page)).textContent()).trim(), "Lena fragen");
  assert.equal(await flagAction(panel(page)).getAttribute("aria-disabled"), "false");

  await flagAction(panel(page)).click();
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="detail-flag-action"]')?.textContent?.trim() === "Rückfrage zurücknehmen"
  );
  assert.deepEqual(state.setCalls, [{ entryId: idOf(1), flagTarget: "AssignLena" }]);
  assert.ok((await row(page, "Rewe Lena").getByTestId("lena-mark").textContent()).includes("Lena"), "Kennzeichen in der Zeile");
  // ADR 0087 Punkt 8 (design-system-dak16): das Kennzeichen ist Flag-Symbol und Name. Welche Farbe das Symbol
  // trägt, prüft der .NET-Test der Abbildung lena_flag_color -> data-lena-flag.
  const mark = row(page, "Rewe Lena").getByTestId("lena-mark");
  await mark.getByRole("img", { name: "Flag" }).waitFor();
  assert.ok((await mark.textContent()).includes("Lena"), "Name am Flag");
  // ledger-enjex (ADR 0098, Entscheidung Roman 3): der gewechselte Knopf ist die Bestätigung, kein Text, kein Toast.
  // Ohne vorheriges Flag gibt es kein eigenes „Rückgängig“: „Rückfrage zurücknehmen“ ist es.
  assert.equal(await panel(page).locator('[data-testid="detail-note"]').count(), 0, "kein Text unter dem Knopf");
  assert.equal(await page.locator(".toast-layer > *").count(), 0, "kein Toast");

  await flagAction(panel(page)).click();
  await page.waitForFunction(() =>
    document.querySelector('[data-testid="detail-flag-action"]')?.textContent?.trim() === "Lena fragen"
  );
  assert.deepEqual(state.setCalls[1], { entryId: idOf(1), flagTarget: "WithdrawLena" }, "der Knopf nimmt die Rückfrage zurück");
  assert.equal(await page.locator(".toast-layer > *").count(), 0, "auch danach kein Toast");
  assert.equal(state.restoreCalls.length, 0, "kein restoreFlag ohne fremdes Flag");
  await context.close();
});

