// scripts/uebersicht.test.mjs
//
// ledger-s1weh: die Übersicht als Einstieg mit „Zu tun“. Der eine Happy Path am Handy (412 px): mit einer ungeprüften
// Buchung steht „1 Buchung zu prüfen“ oben mit dem Knopf „Prüfen“ (ohne Glow), „+ Buchung“ ist ruhig, unter dem Titel
// steht der Stand des Imports, die Rückfrage an Lena trägt das gefüllte Flag, und „Prüfen“ öffnet das Kassenbuch mit
// Filter „Zu prüfen“. Welche Einträge in welcher Reihenfolge erscheinen, wohin sie führen und wann
// „+ Buchung“ ruhig ist, prüfen die .NET-Tests (`OverviewTodoTests.fs`); Layout der Sweep (Zustand `uebersicht-zu-tun`).
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
    // Der letzte Import vor gut zwei Stunden: die Zeile unter „Übersicht“ nennt ihn.
    const importedAt = new Date(Date.now() - 2 * 3600 * 1000 - 60000).toISOString();
    await page.route("**/api/SyncApi/getSyncHistory", (r) =>
      json(r, [{ Id: { SyncSessionId: "00000000-0000-0000-0000-000000000077" }, StartedAt: importedAt, CompletedAt: importedAt, Status: "Completed", TransactionCount: 1, ImportedCount: 1, SkippedCount: 0 }])
    );

    await page.goto(`http://127.0.0.1:${port}/#/`, { waitUntil: "load" });
    const first = page.locator('[data-testid="overview-todo-first"]');
    await first.getByText("1 Buchung zu prüfen").waitFor();
    const fab = page.locator('[data-testid="overview-capture-fab"]');
    assert.equal(await fab.getAttribute("data-quiet"), "true", "„+ Buchung“ ist ruhig, die Hauptaktion steht in Zu tun");
    await page.locator('[data-testid="page-top"]').getByText("Comdirect · Import vor 2 Std.").waitFor();
    // Prototyp `.btn.primary.block`: der Knopf über die volle Breite leuchtet nicht.
    const shadow = await first.getByRole("button", { name: "Prüfen" }).evaluate((el) => getComputedStyle(el).boxShadow);
    assert.equal(shadow, "none", "„Prüfen“ ohne Glow");
    // Die Marke der Rückfrage an Lena ist das gefüllte Flag (Prototyp `I.flag`), 15 px.
    const lenaMark = page.locator('[data-testid="overview-todo"]', { hasText: "1 Rückfrage an Lena offen" }).locator('[aria-label="Lena-Flag"] svg');
    assert.equal(await lenaMark.getAttribute("fill"), "currentColor", "Flag gefüllt");
    assert.equal(await lenaMark.locator('path[d="M5 4h12l-2.5 4.5L17 13H5z"]').count(), 1, "Fahne des Prototyps");
    assert.equal((await lenaMark.boundingBox()).width, 15, "15 px");

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
