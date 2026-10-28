// scripts/touch-target-small-button.test.mjs
//
// Wächter für `design-system-j6dnj`: Button.Small ist am Handy (390 px) mindestens 48 px hoch,
// ab Desktop-Breite (>= 768 px) wächst er nicht auf 48 px. Gemessen per getBoundingClientRect an den
// echten Knöpfen "Zeile hinzufügen" und "Barabhebung" im Split-Editor des Kassenbuchs
// (echter Fable-Client, gemockte Ledger-Antworten).
//
// Ausführen: node --test scripts/touch-target-small-button.test.mjs
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
const eur = (amount) => ({ Amount: amount, Currency: "EUR" });

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
    resolveEntries: () => [
      buildEntryFixture(accountsFixture.DefaultAccountId, 1, {
        Payee: "Bauhaus",
        Date: TODAY,
        LinkedToYnab: true,
        NeedsReview: true,
        Amount: eur(-81.9),
        CategorizationWriteBackFailure: null,
      }),
    ],
  });
  for (const [name, body] of [
    ["getEntryHistory", "[]"],
    ["getPayeeCategorySuggestions", "[]"],
    ["getLedgerEntry", "null"],
  ]) {
    await target.route(`**/api/LedgerApi/${name}`, (route) => route.fulfill({ contentType: "application/json", body }));
  }
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/getBudgetAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: [] }) })
  );
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
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

/// Misst beide Knöpfe atomar im geöffneten Split-Editor.
async function measure(page, scopeSelector) {
  return page.evaluate((sel) => {
    const scope = document.querySelector(sel);
    const find = (label) => {
      const b = [...scope.querySelectorAll("button")].find((x) => x.textContent.trim() === label);
      const r = b.getBoundingClientRect();
      return { height: r.height, width: r.width };
    };
    return { add: find("Zeile hinzufügen"), cashback: find("Barabhebung") };
  }, scopeSelector);
}

test("Handy (390 px): Zeile hinzufügen und Barabhebung sind mindestens 48 px hoch", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("Bauhaus").first().tap();
  const sheet = page.locator(".bottom-sheet.active").first();
  await sheet.waitFor();
  await sheet.locator('[data-testid="detail-split-action"]').tap();
  await sheet.locator('[data-testid="split-summary"]').waitFor();
  const m = await measure(page, ".bottom-sheet.active");
  assert.ok(m.add.height >= 48 - 0.5, `Zeile hinzufügen ${m.add.height} px hoch`);
  assert.ok(m.cashback.height >= 48 - 0.5, `Barabhebung ${m.cashback.height} px hoch`);
  await context.close();
});

test("Desktop (1440 px): dieselben Knöpfe wachsen nicht auf 48 px", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await page.locator("table tbody tr", { hasText: "Bauhaus" }).first().locator("td").nth(2).click();
  const panel = page.locator('[data-testid="ledger-detail-panel"]');
  await panel.waitFor();
  await panel.locator('[data-testid="detail-split-action"]').click();
  await panel.locator('[data-testid="split-summary"]').waitFor();
  const m = await measure(page, '[data-testid="ledger-detail-panel"]');
  assert.ok(m.add.height < 44, `Zeile hinzufügen ${m.add.height} px hoch, bleibt unter 44 px`);
  assert.ok(m.cashback.height < 44, `Barabhebung ${m.cashback.height} px hoch, bleibt unter 44 px`);
  await context.close();
});
