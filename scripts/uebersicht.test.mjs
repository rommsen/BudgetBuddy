// scripts/uebersicht.test.mjs
//
// ledger-s1weh: die Übersicht als Einstieg mit „Zu tun“. Der eine Happy Path am Handy (412 px): mit einer ungeprüften
// Buchung steht „1 Buchung zu prüfen“ oben mit dem Knopf „Prüfen“, „+ Buchung“ ist ruhig, und „Prüfen“ öffnet das
// Kassenbuch mit Filter „Zu prüfen“. Welche Einträge in welcher Reihenfolge erscheinen, wohin sie führen, wann
// „+ Buchung“ ruhig ist und was die Zeile zum Import sagt, prüfen die .NET-Tests (`OverviewTodoTests.fs`); die
// Flag-Farbe `FarbrollenTests.fs`; Layout der Sweep (Zustand `uebersicht-zu-tun`). Feste Designwerte (Glow, Größe
// und Form der Marken) stehen im Styleguide, nicht hier.
//
// Ausführen: npm run test:scripts -- scripts/uebersicht.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { stateNamed } from "./layout-states.mjs";

let frontend;
let port;
let browser;

before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

test("[flow:uebersicht] Mit einer ungeprüften Buchung führt „Prüfen“ in Zu tun ins Kassenbuch mit Filter Zu prüfen", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
    await stateNamed("kassenbuch").mock(page);
    await stateNamed("uebersicht").mock(page);
    // Die neuesten Buchungen und das Kassenbuch: eine Buchung ist zu prüfen.
    await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) => {
      return json(r, { Entries: [], TotalCount: 0, TotalAmount: { Amount: 0, Currency: "EUR" }, DeletedCount: 0, NeedsReviewCount: 1, AskLenaCount: 1 });
    });

    await page.goto(`http://127.0.0.1:${port}/#/`, { waitUntil: "load" });
    const first = page.locator('[data-testid="overview-todo-first"]');
    await first.getByText("1 Buchung zu prüfen").waitFor();
    const fab = page.locator('[data-testid="overview-capture-fab"]');
    assert.equal(await fab.getAttribute("data-quiet"), "true", "„+ Buchung“ ist ruhig, die Hauptaktion steht in Zu tun");

    // Die Liste des Kassenbuchs lädt mit Filter „Zu prüfen“ über alle Konten.
    const ledgerLoad = page.waitForRequest((request) => {
      if (!request.url().includes("/LedgerApi/getLedgerEntriesPage")) return false;
      const [query] = JSON.parse(request.postData() ?? "[]");
      return query.OnlyNeedsReview === true && query.AccountId === null;
    });
    await first.getByRole("button", { name: "Prüfen" }).tap();
    await page.waitForURL(/#\/ledger$/);
    await ledgerLoad;
    await page.locator('[aria-pressed="true"]', { hasText: "Zu prüfen" }).first().waitFor();
  } finally {
    await context.close();
  }
});
