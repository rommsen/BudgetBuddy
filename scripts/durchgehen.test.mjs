// scripts/durchgehen.test.mjs
//
// ledger-v6s7c: Durchgehen Karte für Karte. Im Browser steht nur, was nur der Browser zeigt:
//   * der eine Happy Path am Handy (412 px, Touch): ein Duplikat verwerfen, eine Kategorie wählen, per Wisch
//     bestätigen, bei „Alles geprüft“ ankommen. Der Wisch läuft über echte Touch-Ereignisse (CDP);
//   * die Tasten am PC im echten DOM: K öffnet den Picker, Esc schließt erst ihn, Enter passt, L fragt Lena, S geht
//     weiter, Esc führt zurück zur Übersicht;
//   * Datum und Betrag des Treffers auf der Duplikat-Karte im Fable-Kompilat (die Aussage aus dem in d0fey
//     entfernten `scripts/import-pruefen-duplikat-details.test.mjs`).
// Reihenfolge, Sperren, Toasts und die Folge der Karten prüfen die .NET-Tests (`LedgerReviewClientTests.fs`), das
// Layout der Sweep (Zustände `durchgehen-*` in `layout-states.mjs`).
//
// Ausführen: npm run test:scripts -- scripts/durchgehen.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { mockReview, REVIEW_QUEUE, stateNamed } from "./layout-states.mjs";

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
const unmocked = (page) => page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));

/// Ein Wisch mit dem Finger über die Mitte des Elements, `dx` Pixel weit, in kleinen Schritten.
async function swipe(page, locator, dx) {
  const box = await locator.boundingBox();
  const cdp = await page.context().newCDPSession(page);
  const y = box.y + box.height / 2;
  const x0 = box.x + box.width / 2 - dx / 2;
  const point = (x) => [{ x, y, id: 1 }];
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: point(x0) });
  for (let i = 1; i <= 8; i++) {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: point(x0 + (dx * i) / 8) });
  }
  await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  await cdp.detach();
}

test("[flow:durchgehen] Am Handy ein Duplikat verwerfen, eine Kategorie wählen, per Wisch bestätigen und bei Alles geprüft ankommen", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await unmocked(page);
    await stateNamed("uebersicht").mock(page);
    const state = await mockReview(page, [REVIEW_QUEUE.held, REVIEW_QUEUE.open]);
    await page.route("**/api/LedgerApi/getLedgerEntriesPage", (r) =>
      json(r, { Entries: [], TotalCount: 0, TotalAmount: { Amount: 0, Currency: "EUR" }, DeletedCount: 0, NeedsReviewCount: 2, AskLenaCount: 0, WithoutCategoryCount: 0 })
    );

    await page.goto(`http://127.0.0.1:${port}/#/`, { waitUntil: "load" });
    await page.locator('[data-testid="overview-todo-first"]').getByRole("button", { name: "Jetzt durchgehen" }).tap();
    await page.waitForURL(/#\/review$/);

    // 1 von 2: das Duplikat verwerfen.
    await page.locator('[data-testid="review-duplicate-card"]').waitFor();
    await page.getByRole("button", { name: "Ist doppelt, verwerfen" }).tap();

    // 2 von 2: ohne Kategorie ist „Passt“ gesperrt; die Kategorie kommt aus dem Sheet.
    const card = page.locator('[data-testid="review-card"]');
    await card.getByText("Markt ohne Kategorie").waitFor();
    await page.getByRole("button", { name: "Erst Kategorie wählen" }).waitFor();
    await page.locator('[data-testid="review-category"]').tap();
    const sheet = page.locator(".bottom-sheet.active").first();
    await sheet.getByText("Lebensmittel", { exact: true }).first().tap();
    await page.getByRole("button", { name: "Passt, weiter" }).waitFor();

    // Wisch nach rechts: Prüfvermerk, danach die Endansicht.
    await swipe(page, card, 200);
    await page.locator('[data-testid="review-end"]').getByText("Alles geprüft").waitFor();
    assert.deepEqual(
      state.calls.map((c) => c.endpoint),
      ["discardDuplicateEntry", "setEntryCategory", "setReviewMark"],
      "verwerfen, Kategorie, Prüfvermerk"
    );
  } finally {
    await context.close();
  }
});

test("[tastatur] Am PC passen Enter, S, K, L und Esc; Esc schließt erst den Picker, dann die Ansicht", async () => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await unmocked(page);
    await stateNamed("uebersicht").mock(page);
    const second = { ...REVIEW_QUEUE.plain, Id: "00000000-0000-0000-0000-000000006201", Payee: "Zweite Karte" };
    const third = { ...REVIEW_QUEUE.plain, Id: "00000000-0000-0000-0000-000000006202", Payee: "Dritte Karte" };
    const state = await mockReview(page, [REVIEW_QUEUE.plain, second, third]);

    await page.goto(`http://127.0.0.1:${port}/#/review`, { waitUntil: "load" });
    const position = page.locator('[data-testid="review-position"]');
    await page.locator('[data-testid="review-card"]').waitFor();
    await page.locator('[data-testid="review-keys"]').waitFor();
    assert.equal(await position.textContent(), "1 von 3");

    await page.keyboard.press("Enter");
    await page.getByText("Zweite Karte").first().waitFor();
    assert.equal(await position.textContent(), "2 von 3");

    await page.keyboard.press("k");
    const search = page.getByPlaceholder(/suchen…$/).first();
    await search.waitFor();
    await page.keyboard.press("Escape");
    await search.waitFor({ state: "detached" });
    assert.equal(await position.textContent(), "2 von 3", "Esc schloss nur den Picker");

    const lena = page.waitForRequest("**/api/LedgerApi/setLenaFlag");
    await page.keyboard.press("l");
    await page.getByText("Dritte Karte").first().waitFor();
    await lena;

    await page.keyboard.press("s");
    await page.locator('[data-testid="review-end"]').waitFor();

    await page.keyboard.press("Escape");
    await page.waitForURL(/#\/$/);
    assert.deepEqual(
      state.calls.map((c) => c.endpoint),
      ["setReviewMark", "setLenaFlag"],
      "Enter prüft, L fragt Lena, S schickt nichts"
    );
  } finally {
    await context.close();
  }
});

test("[fable] Die Duplikat-Karte zeigt Datum und Betrag des Treffers im Fable-Kompilat", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await unmocked(page);
    await mockReview(page, [REVIEW_QUEUE.held]);
    await page.goto(`http://127.0.0.1:${port}/#/review`, { waitUntil: "load" });
    await page.locator('[data-testid="review-duplicate-card"]').waitFor();
    // Der Treffer aus dem Ereignis: 10.03.2025, -12.345,67 €.
    const meta = await page.locator('[data-testid="review-match-meta"]').textContent();
    assert.match(meta, /^10\.03\. · /, "Datum des Treffers");
    const amount = await page.locator('[data-testid="review-match-amount"]').textContent();
    assert.equal(amount.replace(/\s/g, " "), "-12.345,67 €", "Betrag des Treffers");
  } finally {
    await context.close();
  }
});
