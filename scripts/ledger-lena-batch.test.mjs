// scripts/ledger-lena-batch.test.mjs
//
// Wächter für `ledger-y54gr` (ADR 0053 Punkt 2, ADR 0056): "Lena fragen" und "Rückfrage
// zurücknehmen" in der Auswahl-Leiste. Gemessen am echten, per Vite gestarteten Fable-Client
// gegen gemockte Ledger-Antworten (erfundene Werte, nie die Entwicklungsdatenbank): der
// Sammelaufruf mit den gewählten Ids und der Toast mit einem Rückgängig für alle. Sperre und Grund
// prüft `LedgerLenaFlagBatchClientTests` (infrastructure-t5m2b), die Leiste am Handy der
// Layout-Sweep (Zustand kassenbuch-auswahl-handy).
//
// Ausführen: node --test scripts/ledger-lena-batch.test.mjs
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
const PAYEES = ["Rewe y54gr", "Bahn y54gr", "Kiosk y54gr", "Bäcker y54gr"];

const state = {
  lenaColor: "purple",
  setCalls: [],
  restoreCalls: [],
  flagOf: {},
  writeBack: "FlagSent",
};

const idOf = (n) => buildEntryFixture("x", n).Id;

function entries(accountId) {
  return PAYEES.map((payee, i) =>
    buildEntryFixture(accountId, i + 1, {
      Date: TODAY,
      LinkedToYnab: true,
      NeedsReview: false,
      Payee: payee,
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Flag: state.flagOf[i + 1] ?? "NoFlag",
      FlagWriteBackFailure: null,
    })
  );
}

async function open(context) {
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
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
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const list = entries(accountsFixture.DefaultAccountId);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: list,
        TotalCount: list.length,
        TotalAmount: { Amount: list.reduce((s, e) => s + e.Amount.Amount, 0), Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: 0,
        AskLenaCount: list.filter((e) => e.Flag === "AskLena").length,
      }),
    });
  });
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ SavedColor: state.lenaColor, SuggestedColor: null }),
    })
  );
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.route("**/api/LedgerApi/setLenaFlagBatch", (route) => {
    const [selection, flagTarget] = route.request().postDataJSON();
    state.setCalls.push({ selection, flagTarget });
    const ids = selection.BatchPicked ?? selection.BatchPicked?.[0] ?? [];
    const changes = ids.map((id) => ({
      EntryId: id,
      Previous: { Color: null, Name: null },
      WriteBack: state.writeBack,
    }));
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Changed: changes, UnchangedCount: 0, FailedCount: 0 } }),
    });
  });
  await page.route("**/api/LedgerApi/restoreFlagBatch", (route) => {
    const [items] = route.request().postDataJSON();
    state.restoreCalls.push(items);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: {
          Changed: items.map((i) => ({
            EntryId: i.EntryId,
            Previous: { Color: "purple", Name: "Lena" },
            WriteBack: "FlagSent",
          })),
          UnchangedCount: 0,
          FailedCount: 0,
        },
      }),
    });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return page;
}

function resetState() {
  state.lenaColor = "purple";
  state.setCalls.length = 0;
  state.restoreCalls.length = 0;
  state.flagOf = {};
  state.writeBack = "FlagSent";
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

const visibleBar = (page) => page.locator(".selection-bar").filter({ visible: true });
const box = (page, payee) =>
  page.getByRole("checkbox", { name: `${payee} auswählen` }).filter({ visible: true });

test("[flow:lena-fragen-mehrere] PC: Lena fragen für zwei Buchungen, ein Aufruf, ein Toast mit Rückgängig für beide", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  try {
    await box(page, PAYEES[0]).click();
    await box(page, PAYEES[1]).click();
    const bar = visibleBar(page);
    await bar.waitFor();

    const ask = bar.getByRole("button", { name: "Lena fragen (2)", exact: true });
    const withdraw = bar.getByRole("button", { name: "Rückfrage zurücknehmen (2)", exact: true });
    assert.equal(await ask.count(), 1, "Lena fragen mit Anzahl");
    assert.equal(await withdraw.count(), 1, "Rückfrage zurücknehmen mit Anzahl");

    await ask.click();
    await page.getByText("2 Buchungen an Lena gefragt", { exact: true }).waitFor();
    assert.equal(state.setCalls.length, 1, "genau ein Sammelaufruf");
    assert.equal(state.setCalls[0].flagTarget, "AssignLena");
    assert.deepEqual([...state.setCalls[0].selection.BatchPicked].sort(), [idOf(1), idOf(2)].sort(), "die gewählten Ids");
    assert.equal(await visibleBar(page).count(), 0, "die Auswahl endet");
    const lenaMark = async (payee) => {
      const mark = page.locator("table tbody tr", { hasText: payee }).first().getByTestId("lena-mark");
      return (await mark.count()) > 0 ? await mark.textContent() : "";
    };
    assert.ok((await lenaMark(PAYEES[0])).includes("Lena"), "Kennzeichen in Zeile 1");
    assert.ok((await lenaMark(PAYEES[1])).includes("Lena"), "Kennzeichen in Zeile 2");
    assert.ok(!(await lenaMark(PAYEES[2])).includes("Lena"), "Zeile 3 unberührt");

    await page.getByRole("button", { name: "Rückgängig" }).click();
    await page.getByText("Flag bei 2 Buchungen zurückgesetzt", { exact: true }).waitFor();
    assert.equal(state.restoreCalls.length, 1, "ein Rückgängig-Aufruf für alle");
    assert.deepEqual(state.restoreCalls[0].map((i) => i.EntryId).sort(), [idOf(1), idOf(2)].sort());
    assert.ok((await lenaMark(PAYEES[0])).includes("Lena") === false, "Zeile 1 wieder ohne Kennzeichen");
  } finally {
    await context.close();
  }
});
