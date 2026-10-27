// scripts/ledger-external-links.test.mjs
//
// Wächter für `ledger-rjhwp`: das Buchungs-Detail zeigt die externen Links der Buchung
// (Amazon-Bestellung, PayPal-Aktivitäten) als eigene Feldzeile unter dem Memo, im Seitenpanel am
// PC wie im Sheet am Handy. Die Kassenbuch-Liste zeigt keinen Link. Gemessen wird am echten, per
// Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-detail.test.mjs`).
//
// Ausführen: node --test scripts/ledger-external-links.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildEntryFixture, mockFilterBarApi } from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;
let mobilePage;

const ORDER_URL = "https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID=304-1234567-7654321";

function entries(accountId) {
  return [
    buildEntryFixture(accountId, 1, {
      Payee: "AMAZON PAYMENTS",
      Memo: "304-1234567-7654321 Lampe",
      ExternalLinks: [{ Label: "Bestellung 304-1234567-7654321", Url: ORDER_URL }],
    }),
    buildEntryFixture(accountId, 2, {
      Payee: "PayPal Shop",
      ExternalLinks: [{ Label: "PayPal Activity", Url: "https://www.paypal.com/activities" }],
    }),
    buildEntryFixture(accountId, 3, { Payee: "REWE Mitte", ExternalLinks: [] }),
  ];
}

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
  // Offen lassen: sonst startet die Seite den Spiegel-Lauf in einer Schleife.
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  page = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
  mobilePage = await open(
    await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true })
  );
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();

test("PC: das Seitenpanel zeigt den Bestell-Link unter dem Memo, öffnet in neuem Tab, die Liste zeigt keinen Link", async () => {
  await row(page, "AMAZON PAYMENTS").waitFor();
  assert.equal(await page.locator('table a[href*="amazon"]').count(), 0, "die Liste trägt keinen Link");
  await row(page, "AMAZON PAYMENTS").locator("td").nth(2).click();
  const panel = page.locator('[data-testid="ledger-detail-panel"]');
  await panel.waitFor();
  const link = panel.locator('[data-testid="detail-external-link"]');
  assert.equal(await link.count(), 1);
  assert.equal(await link.getAttribute("href"), ORDER_URL);
  assert.equal(await link.getAttribute("target"), "_blank");
  assert.equal(await link.getAttribute("rel"), "noopener noreferrer");
  assert.match(await link.textContent(), /Bestellung 304-1234567-7654321/);
  assert.equal(await link.locator("svg").count(), 1, "Symbol externalLink");
  const order = await panel.evaluate((el) => {
    const y = (id) => el.querySelector(`[data-testid="${id}"]`).getBoundingClientRect().top;
    return { memo: y("detail-memo"), links: y("detail-external-links") };
  });
  assert.ok(order.links > order.memo, "die Link-Zeile steht unter dem Memo");
});

test("PC: eine Buchung ohne Treffer zeigt keine Link-Zeile", async () => {
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await page.locator('[data-testid="detail-title"]', { hasText: "REWE Mitte" }).waitFor();
  assert.equal(await page.locator('[data-testid="detail-external-links"]').count(), 0);
});

test("Handy (390): das Sheet zeigt den PayPal-Link mit mindestens 44 px Trefferfläche", async () => {
  await mobilePage.getByText("PayPal Shop").first().tap();
  const sheet = mobilePage.locator(".bottom-sheet.active");
  await sheet.waitFor();
  const link = sheet.locator('[data-testid="detail-external-link"]');
  await link.waitFor();
  assert.equal(await link.getAttribute("href"), "https://www.paypal.com/activities");
  assert.equal(await link.getAttribute("target"), "_blank");
  const box = await link.boundingBox();
  assert.ok(box.height >= 44, `Trefferfläche ${box.height}px`);
});
