// scripts/durchgehen.test.mjs
//
// ledger-v6s7c: Durchgehen Karte für Karte. Im Browser steht nur, was nur der Browser zeigt:
//   * der eine Happy Path am Handy (412 px, Touch): ein Duplikat verwerfen, ohne Kategorie nach rechts wischen (die
//     Karte bleibt, die Zeile unter dem Knopf sagt es), eine Kategorie wählen, per Wisch bestätigen, bei „Alles geprüft“
//     ankommen. Der Wisch läuft über echte Touch-Ereignisse (CDP), die auch Pointer-Ereignisse erzeugen;
//   * am Handy die Seite ohne Scrollen, langer Inhalt scrollt in der Karte, die Zeile unter dem Knopf mit fester Höhe
//     (ledger-s2cg3);
//   * die Tasten am PC im echten DOM: K öffnet den Picker, Esc schließt erst ihn, Enter passt, L fragt Lena, S geht
//     weiter, Esc führt zurück zur Übersicht;
//   * Taste A öffnet das Detail mit dem Split-Editor, Esc schließt je eine Stufe (ledger-cyd1t);
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
import { settle } from "./layout-invariants.mjs";

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

/// Ein Wisch mit dem Finger über die Mitte des Elements, `dx` Pixel weit, in kleinen Schritten. Der Finger bleibt
/// liegen; `release()` hebt ihn.
async function touchHold(page, locator, dx) {
  const box = await locator.boundingBox();
  const cdp = await page.context().newCDPSession(page);
  const y = box.y + box.height / 2;
  const x0 = box.x + box.width / 2 - dx / 2;
  const point = (x) => [{ x, y, id: 1 }];
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: point(x0) });
  for (let i = 1; i <= 8; i++) {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: point(x0 + (dx * i) / 8) });
  }
  return async () => {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
    await cdp.detach();
  };
}

/// Ein ganzer Wisch: hinlegen, ziehen, loslassen.
async function swipe(page, locator, dx) {
  await (await touchHold(page, locator, dx))();
}

test("[flow:durchgehen] Am Handy ein Duplikat verwerfen, ohne Kategorie nach rechts wischen, eine Kategorie wählen, per Wisch bestätigen und bei Alles geprüft ankommen", async () => {
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

    // 2 von 2: ohne Kategorie ist „Passt“ gesperrt. Ein Wisch nach rechts lässt die Karte stehen, die Zeile unter dem
    // Knopf sagt es, solange der Finger liegt (ledger-s2cg3, kein Toast).
    const card = page.locator('[data-testid="review-card"]');
    const foot = page.locator('[data-testid="review-foot"]');
    await card.getByText("Markt ohne Kategorie").waitFor();
    await page.getByRole("button", { name: "Erst Kategorie wählen" }).waitFor();
    // Der Wisch beginnt auf dem Feld „Kategorie“ und öffnet es nicht.
    const release = await touchHold(page, page.locator('[data-testid="review-category"]'), 200);
    await foot.getByText("Erst Kategorie wählen").waitFor();
    await release();
    // Nach dem Finger steht wieder die letzte Aktion in der Zeile (ledger-pz5wv).
    await foot.getByText("Verworfen", { exact: true }).waitFor();
    await card.getByText("Markt ohne Kategorie").waitFor();
    assert.equal(await page.getByText("Erst eine Kategorie wählen").count(), 0, "kein Toast");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "der Wisch öffnete das Feld nicht");
    // Die Kategorie kommt aus dem Sheet.
    await page.locator('[data-testid="review-category"]').tap();
    const sheet = page.locator(".bottom-sheet.active").first();
    await sheet.getByText("Lebensmittel", { exact: true }).first().tap();
    await page.getByRole("button", { name: "Passt, weiter" }).waitFor();
    // Bestätigt am Feld mit „Rückgängig“, ohne Meldung in der Leiste (ledger-mf2bg).
    const fieldNote = card.locator('[data-testid="review-field-note"]');
    await fieldNote.getByText("Lebensmittel", { exact: false }).waitFor();
    await fieldNote.getByRole("button", { name: "Rückgängig" }).waitFor();
    const barsAfterCategory = await page.locator('[data-testid="feedback-bar"]').filter({ visible: true }).allInnerTexts();
    assert.ok(!barsAfterCategory.some((t) => /Kategorie|Lebensmittel/.test(t)), `keine Meldung zur Kategorie in der Leiste: ${barsAfterCategory.join(" | ")}`);

    // Wisch nach rechts: Prüfvermerk, danach die Endansicht.
    await swipe(page, card, 200);
    const end = page.locator('[data-testid="review-end"]');
    await end.getByText("Alles geprüft").waitFor();

    // Prüfvermerk und Kategorie der Karte melden sich nicht in der Meldungsleiste (ledger-mf2bg).
    const bars = await page.locator('[data-testid="feedback-bar"]').filter({ visible: true }).allInnerTexts();
    assert.ok(!bars.some((t) => /Passt|geprüft|Rückgängig nicht|Kategorie/.test(t)), `keine Meldung zu Passt oder Kategorie in der Leiste: ${bars.join(" | ")}`);
    assert.deepEqual(
      state.calls.map((c) => c.endpoint),
      ["discardDuplicateEntry", "setEntryCategory", "setReviewMark"],
      "verwerfen, Kategorie, Prüfvermerk"
    );
  } finally {
    await context.close();
  }
});

// ledger-s2cg3: Die Seite passt am Handy ohne Scrollen, die Karte nimmt den Rest, langer Inhalt scrollt in ihr. Die Zeile
// unter dem Hauptknopf hat eine feste Höhe: mit Hinweis, mit „Erst Kategorie wählen“ und mit der letzten Aktion unter der
// Duplikat-Karte (ledger-pz5wv)
// stehen Knöpfe und Zeile an derselben Stelle.
test("[layout] Am Handy scrollt das Durchgehen nicht, langer Inhalt scrollt in der Karte, und die Zeile unter dem Knopf lässt die Knöpfe nicht springen", async () => {
  const longMemo = { ...REVIEW_QUEUE.amazon, Memo: "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke und Kohle. ".repeat(12) };
  for (const width of [412, 320]) {
    const context = await browser.newContext({ viewport: { width, height: 844 }, hasTouch: true, isMobile: true });
    const page = await context.newPage();
    page.setDefaultTimeout(20000);
    try {
      await unmocked(page);
      await mockReview(page, [longMemo]);
      await page.goto(`http://127.0.0.1:${port}/#/review`, { waitUntil: "load" });
      await page.locator('[data-testid="review-suggestions"]').waitFor();
      // Die Einblendung der Seite (`pageEnter`, 8 px tiefer) zählte sonst zur Höhe.
      await settle(page, { where: `durchgehen @ ${width} px` });
      const sizes = await page.evaluate(() => {
        const card = document.querySelector('[data-testid="review-card"]');
        return { page: document.scrollingElement.scrollHeight, view: innerHeight, content: card.scrollHeight, box: card.clientHeight };
      });
      assert.ok(sizes.page <= sizes.view, `${width} px: Seite ${sizes.page} px höher als ${sizes.view} px`);
      assert.ok(sizes.content > sizes.box, `${width} px: Karte scrollt nicht innen (${sizes.content} zu ${sizes.box} px)`);
    } finally {
      await context.close();
    }
  }

  const context = await browser.newContext({ viewport: { width: 412, height: 844 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await unmocked(page);
    await mockReview(page, [REVIEW_QUEUE.open, REVIEW_QUEUE.held]);
    await page.goto(`http://127.0.0.1:${port}/#/review`, { waitUntil: "load" });
    const card = page.locator('[data-testid="review-card"]');
    const foot = page.locator('[data-testid="review-foot"]');
    await card.getByText("Markt ohne Kategorie").waitFor();
    await settle(page, { where: "durchgehen @ 412 px" });
    const where = () =>
      page.evaluate(() => {
        const top = (el) => Math.round(el.getBoundingClientRect().top);
        const buttons = [...document.querySelectorAll('[data-testid="review-page"] button')];
        const main = buttons.find((b) => /Erst Kategorie wählen|Ist doppelt, verwerfen/.test(b.textContent));
        return { main: top(main), foot: top(document.querySelector('[data-testid="review-foot"]')), height: document.querySelector('[data-testid="review-foot"]').offsetHeight };
      });
    const hint = await where();
    const release = await touchHold(page, card, 200);
    await foot.getByText("Erst Kategorie wählen").waitFor();
    assert.deepEqual(await where(), hint, "„Erst Kategorie wählen“ in der Zeile: nichts springt");
    await release();
    await page.getByRole("button", { name: "Später", exact: true }).tap();
    await page.locator('[data-testid="review-duplicate-card"]').waitFor();
    await settle(page, { where: "durchgehen-duplikat @ 412 px" });
    // Unter der Duplikat-Karte steht die letzte Aktion („Später“, ledger-pz5wv); Knopf und Zeile springen nicht.
    const last = await where();
    await foot.getByText("Später", { exact: true }).waitFor();
    assert.equal(last.foot, hint.foot, "die Zeile steht an derselben Stelle");
    assert.equal(last.height, hint.height, "gleiche Höhe");
  } finally {
    await context.close();
  }
});

test("[tastatur] Am PC passen Enter, S, K, L, Z und Esc; Esc schließt erst den Picker, dann die Ansicht, Z holt die letzte Karte zurück", async () => {
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

    const at = (text) => page.waitForFunction((t) => document.querySelector('[data-testid="review-position"]')?.textContent === t, text);

    // Z nimmt „Später“ zurück (ledger-pz5wv); die Tastenleiste nennt es.
    await page.keyboard.press("s");
    await at("3 von 3");
    assert.match(await page.locator('[data-testid="review-keys"]').innerText(), /Z\s*rückgängig/);
    await page.keyboard.press("z");
    await at("2 von 3");

    const lena = page.waitForRequest("**/api/LedgerApi/setLenaFlag");
    await page.keyboard.press("l");
    await at("3 von 3");
    await lena;

    await page.keyboard.press("s");
    const end = page.locator('[data-testid="review-end"]');
    await end.waitFor();

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

// ledger-cyd1t: A öffnet das Detail-Panel mit offenem Split-Editor. Esc schließt je eine Stufe: erst den Editor (das
// Panel bleibt), dann das Panel, dann die Ansicht. Im echten DOM, weil der Editor Felder mit Fokus hat und am
// Dokument mehrere Tasten-Hörer hängen könnten; die Kette selbst prüft `LedgerReviewSplitClientTests.fs`.
test("[tastatur] Am PC öffnet A das Detail mit dem Split-Editor, Esc schließt erst den Editor, dann das Detail, dann die Ansicht", async () => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await unmocked(page);
    await stateNamed("uebersicht").mock(page);
    await mockReview(page, [REVIEW_QUEUE.open]);

    await page.goto(`http://127.0.0.1:${port}/#/review`, { waitUntil: "load" });
    await page.locator('[data-testid="review-card"]').waitFor();
    assert.match(await page.locator('[data-testid="review-keys"]').innerText(), /K\s*Kategorie\s*A\s*aufteilen\s*L\s*Lena/);

    const panel = page.locator('[data-testid="ledger-detail-panel"]');
    const editor = page.locator('[data-testid="split-summary"]');
    await page.keyboard.press("a");
    await editor.waitFor();
    assert.equal(await panel.count(), 1, "das Detail-Panel steht");

    // Aus einem Feld des Editors: Esc schließt den Editor, nicht das Panel und nicht die Ansicht.
    await page.locator('[data-testid="split-amount-1"] input').focus();
    await page.keyboard.press("Escape");
    await editor.waitFor({ state: "detached" });
    await page.locator('[data-testid="detail-payee"]').filter({ visible: true }).first().waitFor();
    assert.equal(await panel.count(), 1, "Esc schloss nur den Editor");
    assert.match(page.url(), /#\/review$/);

    await page.keyboard.press("Escape");
    await panel.waitFor({ state: "detached" });
    await page.locator('[data-testid="review-card"]').waitFor();
    assert.match(page.url(), /#\/review$/, "Esc schloss nur das Detail");

    await page.keyboard.press("Escape");
    await page.waitForURL(/#\/$/);
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
