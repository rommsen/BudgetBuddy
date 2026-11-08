// scripts/ledger-capture.test.mjs
//
// Wächter für `ledger-at63w`: "Neue Buchung" im Kassenbuch, der eine Happy Path am Handy (412 px, Touch).
// Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Antworten (nie die echte
// Entwicklungsdatenbank, nie YNAB): "+ Buchung" öffnet das Sheet, ein Zuletzt-Chip füllt das Formular,
// der Betrag wird ohne Vorzeichen geändert, "Buchung anlegen" schickt die Buchung, die Rückmeldung
// bietet "Rückgängig". Vorbelegung, Sperre und Fehler prüft `src/Tests/LedgerCaptureClientTests.fs`
// unter .NET, das Layout der Sweep (`kassenbuch-erfassen*` in `scripts/layout-states.mjs`).
//
// Ausführen: node --test scripts/ledger-capture.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { mockCapture } from "./ledger-capture-fixture.mjs";

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

test("[flow:buchung-erfassen] Handy: ein Zuletzt-Chip füllt das Formular, Buchung anlegen bucht auf Bar, die Rückmeldung bietet Rückgängig", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  try {
    const page = await context.newPage();
    page.setDefaultTimeout(30000);
    const calls = await mockCapture(page);
    await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/ledger");

    await page.locator(".ledger-capture-fab").getByRole("button").tap();
    const sheet = page.locator(".bottom-sheet.active", { hasText: "Neue Buchung" });
    await sheet.locator('[data-testid="capture-recent"]').waitFor();

    await sheet.getByRole("button", { name: "Bäckerei Lange · 8,60" }).tap();
    const amount = sheet.locator('[data-testid="capture-amount"] input');
    await amount.fill("9,20");
    await sheet.getByRole("button", { name: "Buchung anlegen" }).tap();

    await page.getByText("Buchung angelegt · Bar").first().waitFor();
    await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).first().waitFor();
    assert.equal(calls.add.length, 1, "genau ein Aufruf");
    const [request] = calls.add[0];
    // Fable.Remoting schickt `decimal` als Text.
    assert.equal(String(request.Amount), "9.2", "Betrag ohne Vorzeichen");
    assert.equal(request.IsOutflow, true, "Ausgabe");
    assert.equal(request.PayeeName, "Bäckerei Lange", "Payee aus dem Chip");
    assert.deepEqual(request.Target, { BookOnCategory: { YnabCategoryId: calls.categoryId } }, "Kategorie aus dem Chip");
    assert.equal(request.AccountId, calls.barId, "Konto aus dem Chip");
  } finally {
    await context.close();
  }
});
