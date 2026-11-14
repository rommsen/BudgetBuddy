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

    // ledger-ea70s: ohne Kontofilter ist das Quick-Add-Konto (Bar) vorbelegt; die Regel prüft LedgerCaptureClientTests.
    await sheet.locator('[data-testid="capture-account"]', { hasText: "Bar" }).waitFor();
    // ledger-sre0f: nur die Chips des gewählten Kontos; „Tankstelle“ gehört zu Tagesgeld.
    assert.equal(await sheet.getByRole("button", { name: "Tankstelle · 60,00" }).count(), 0, "kein Chip eines anderen Kontos");
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
    assert.equal(request.Memo, "Brötchen", "Memo aus dem Chip");
  } finally {
    await context.close();
  }
});

// ledger-ea70s: wie vorher Quick Add steht am PC der Fokus beim Öffnen im Betrag, am Handy nicht (die Tastatur verdeckte
// sonst Zuletzt und Konto). Nur der Browser kennt den Fokus (`autoFocus` beim Einhängen).
test("[fokus] Neue Buchung: am PC Fokus im Betrag, am Handy kein Autofokus", async () => {
  const amountFocused = (page) =>
    page.evaluate(() => document.activeElement === document.querySelector('[data-testid="capture-amount"] input'));
  {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    try {
      const page = await context.newPage();
      page.setDefaultTimeout(30000);
      await mockCapture(page);
      await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
      await waitForPageLoaded(page, "/ledger");
      await page.getByRole("button", { name: "Neue Buchung" }).locator("visible=true").first().click();
      await page.locator('[data-testid="capture-amount"] input').waitFor();
      await page.waitForFunction(
        () => document.activeElement === document.querySelector('[data-testid="capture-amount"] input')
      );
    } finally {
      await context.close();
    }
  }
  {
    const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
    try {
      const page = await context.newPage();
      page.setDefaultTimeout(30000);
      await mockCapture(page);
      await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
      await waitForPageLoaded(page, "/ledger");
      await page.locator(".ledger-capture-fab").getByRole("button").tap();
      await page.locator('.bottom-sheet.active [data-testid="capture-amount"] input').waitFor();
      assert.equal(await amountFocused(page), false, "am Handy kein Autofokus");
    } finally {
      await context.close();
    }
  }
});
