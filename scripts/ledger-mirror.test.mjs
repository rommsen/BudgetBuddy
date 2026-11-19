// scripts/ledger-mirror.test.mjs
//
// „Jetzt spiegeln“ im Kopf des Kassenbuchs (ledger-gy77x): solange der Spiegel läuft, sperrt der Knopf nur über
// ARIA (`IsBusy`) und behält den Fokus. Vorher setzte er `IsLoading` und damit `disabled`, der Fokus fiel auf `body`
// (Kann-Sammlung ledger-k4n2s Punkt 82). Ein echter, per Vite gestarteter Fable-Client gegen gemockte Antworten
// (erfundene Werte); `mirrorYnabBudget` bleibt unbeantwortet, bis der Test den Lauf freigibt.
//
// Ausführen: node --test scripts/ledger-mirror.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockLedgerApi,
} from "./ledger-status-column.mjs";

let frontend;
let frontendPort;
let browser;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

function buildEntries(accountId) {
  const entries = [buildEntryFixture(accountId, 1, { Payee: "Spiegel-Probe", NeedsReview: false })];
  return {
    Entries: entries,
    TotalCount: entries.length,
    TotalAmount: { Amount: entries[0].Amount.Amount, Currency: "EUR" },
    DeletedCount: 0,
    NeedsReviewCount: 0,
    AskLenaCount: 0,
    WithoutCategoryCount: 0,
  };
}

test("[fokus] PC: der Fokus bleibt an Jetzt spiegeln, solange der Spiegel läuft", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  try {
    const accounts = buildAccountsFixture(2);
    await page.route("**/api/**", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
    );
    await mockLedgerApi(page, {
      accountsFixture: accounts,
      balancesFixture: buildBalancesFixture(accounts.Accounts),
      entriesFixture: buildEntries(accounts.DefaultAccountId),
    });
    // Ein frischer Lauf: beim Öffnen startet kein Spiegel von selbst, nur der Druck auf den Knopf.
    await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({
          RanAt: new Date().toISOString(),
          Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
        }),
      })
    );
    let pending = null;
    let mirrorCalls = 0;
    await page.route("**/api/LedgerApi/mirrorYnabBudget", (route) => {
      mirrorCalls += 1;
      pending = route;
    });

    await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/ledger");

    const button = page.getByRole("button", { name: "Jetzt spiegeln", exact: true }).filter({ visible: true }).first();
    await button.waitFor();
    await button.focus();
    const sent = page.waitForRequest("**/api/LedgerApi/mirrorYnabBudget");
    await page.keyboard.press("Enter");
    await sent;
    await page.locator('button[aria-busy="true"]', { hasText: "Spiegel läuft" }).filter({ visible: true }).waitFor();

    const state = await page.evaluate(() => {
      const el = document.activeElement;
      return {
        tag: el?.tagName,
        text: el?.textContent?.trim(),
        busy: el?.getAttribute("aria-busy"),
        ariaDisabled: el?.getAttribute("aria-disabled"),
        disabled: el instanceof HTMLButtonElement ? el.disabled : null,
      };
    });
    assert.equal(state.tag, "BUTTON", `der Fokus liegt auf ${state.tag}`);
    assert.equal(state.text, "Spiegel läuft…", "der Fokus bleibt am Knopf des Spiegels");
    assert.equal(state.busy, "true", "aria-busy");
    assert.equal(state.ariaDisabled, "true", "aria-disabled");
    assert.equal(state.disabled, false, "kein disabled");
    assert.equal(mirrorCalls, 1, "ein Lauf");

    await pending.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 2 } }),
    });
  } finally {
    await context.close();
  }
});
