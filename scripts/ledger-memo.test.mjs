// scripts/ledger-memo.test.mjs
//
// Wächter für `ledger-fa1fp` (ADR 0062): Memo bearbeiten im Kassenbuch-Detail und die Punkte des
// Detail-Feinschliffs. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte
// Ledger-Antworten (nie die echte Entwicklungsdatenbank): Memo setzen, ändern und leeren (Liste und
// Detail zeigen das neue Memo), Toast mit Rückgängig, Grenze von 500 Zeichen, Esc im Feld, die
// Hinweisleiste "Memo nicht in YNAB" mit Erneut senden, die gesperrte Aktion bei gelöschter
// Buchung, die Kategorie in Kurzform am Handy, der Fußtext im Picker am PC und die Auswahl-Bausteine
// in /styleguide.
//
// Ausführen: node --test scripts/ledger-memo.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  CATEGORY_IDS,
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";

/// Veränderlicher Server-Zustand der Mocks.
const state = {
  setCalls: [],
  retryCalls: [],
  memoOf: {},
  failureOf: {},
  writeBack: "MemoSent",
};

const idOf = (n) => buildEntryFixture("x", n).Id;
const indexOf = (entryId) => [1, 2, 3].find((n) => idOf(n) === entryId);

function entries(accountId) {
  const base = (n, extra) =>
    buildEntryFixture(accountId, n, {
      Date: TODAY,
      LinkedToYnab: true,
      CategoryId: CATEGORY_IDS.lebensmittel,
      CategoryName: "Lebensmittel",
      CategoryGroupName: "Alltag",
      Memo: state.memoOf[n] ?? null,
      MemoWriteBackFailure: state.failureOf[n] ?? null,
      ...extra,
    });
  return [
    base(1, { Payee: "Rewe Memo" }),
    base(2, { Payee: "Bahn Memo", Memo: state.memoOf[2] ?? "Zugticket" }),
    base(3, { Payee: "Kiosk Memo", DeletedInYnab: true, Memo: "alt" }),
  ];
}

function resetState() {
  state.setCalls.length = 0;
  state.retryCalls.length = 0;
  state.memoOf = {};
  state.failureOf = {};
  state.writeBack = "MemoSent";
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
  await target.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ SavedColor: null, SuggestedColor: null }) })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/setEntryMemo", (route) => {
    const [entryId, text] = route.request().postDataJSON();
    state.setCalls.push({ entryId, text });
    const n = indexOf(entryId);
    const previous = state.memoOf[n] ?? (n === 2 ? "Zugticket" : null);
    const current = text.trim() === "" ? null : text.trim();
    state.memoOf[n] = current;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: { PreviousMemo: previous, CurrentMemo: current, MemoChange: "MemoChanged", MemoWriteBack: state.writeBack },
      }),
    });
  });
  await target.route("**/api/LedgerApi/retryMemoWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    state.failureOf = {};
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "MemoSent" }) });
  });
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

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');
const sheet = (p) => p.locator(".bottom-sheet.active");
const memoRow = (scope) => scope.locator('[data-testid="detail-memo"]');
const memoAction = (scope) => scope.locator('[data-testid="detail-memo-action"]');
const editor = (scope) => scope.locator('[data-testid="detail-memo-editor"]');

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

test("PC: Memo hinzufügen setzt das Memo, Detail und Liste zeigen es, der Toast bietet Rückgängig", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Memo");
  assert.ok((await memoRow(panel(page)).textContent()).includes("Kein Memo"), "Wert: Kein Memo");
  assert.equal((await memoAction(panel(page)).textContent()).trim(), "Memo hinzufügen");

  await memoAction(panel(page)).click();
  await editor(panel(page)).waitFor();
  const input = editor(panel(page)).locator("input");
  assert.equal(await input.inputValue(), "", "leerer Entwurf");
  await input.fill("  Werkzeug + Lampe ");
  assert.match(await editor(panel(page)).locator('[data-testid="detail-memo-counter"]').textContent(), /^19 \/ 500$/);
  await editor(panel(page)).getByRole("button", { name: "Speichern" }).click();

  await page.waitForFunction(() => document.querySelector('[data-testid="detail-memo-editor"]') === null);
  assert.deepEqual(state.setCalls, [{ entryId: idOf(1), text: "  Werkzeug + Lampe " }], "der Client schickt den Entwurf, der Server trimmt");
  assert.ok((await memoRow(panel(page)).textContent()).includes("Werkzeug + Lampe"), "Detail zeigt das neue Memo");
  assert.equal((await memoAction(panel(page)).textContent()).trim(), "Bearbeiten");
  await page.getByText("Memo gesetzt", { exact: true }).waitFor();

  // Bei offenem Panel weicht die Memo-Spalte; ohne Panel zeigt die Liste das neue Memo.
  await page.keyboard.press("Escape");
  await panel(page).waitFor({ state: "detached" });
  assert.ok((await row(page, "Rewe Memo").textContent()).includes("Werkzeug + Lampe"), "Liste zeigt das neue Memo");

  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  for (let i = 0; i < 50 && state.setCalls.length < 2; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.setCalls[1], { entryId: idOf(1), text: "" }, "Rückgängig schickt das alte Memo (keins)");
  await page.waitForFunction(
    () => !document.querySelector("table tbody tr")?.textContent?.includes("Werkzeug + Lampe")
  );
  await context.close();
});

test("PC: Bearbeiten ändert, Leeren entfernt das Memo", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Bahn Memo");
  assert.equal((await memoAction(panel(page)).textContent()).trim(), "Bearbeiten");
  await memoAction(panel(page)).click();
  const input = editor(panel(page)).locator("input");
  assert.equal(await input.inputValue(), "Zugticket", "der Entwurf beginnt mit dem heutigen Memo");
  await input.fill("Zugticket Hamburg");
  await input.press("Enter");
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-memo-editor"]') === null);
  assert.ok((await memoRow(panel(page)).textContent()).includes("Zugticket Hamburg"), "Enter speichert");
  await page.getByText("Memo geändert", { exact: true }).waitFor();

  await memoAction(panel(page)).click();
  await editor(panel(page)).locator("input").fill("   ");
  await editor(panel(page)).getByRole("button", { name: "Speichern" }).click();
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-memo-editor"]') === null);
  assert.ok((await memoRow(panel(page)).textContent()).includes("Kein Memo"), "leerer Text heißt kein Memo");
  assert.equal(state.setCalls.at(-1).text, "   ");
  await context.close();
});

test("PC: ein Memo über 500 Zeichen lässt sich nicht speichern und nennt den Grund; Esc schließt nur das Feld", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Memo");
  await memoAction(panel(page)).click();
  const input = editor(panel(page)).locator("input");
  await input.fill("x".repeat(501));
  const save = editor(panel(page)).getByRole("button", { name: "Speichern" });
  await editor(panel(page)).locator('[data-testid="detail-memo-problem"]').waitFor();
  assert.match(await editor(panel(page)).locator('[data-testid="detail-memo-problem"]').textContent(), /höchstens 500 Zeichen/);
  assert.equal(await save.isDisabled(), true, "Speichern gesperrt");
  await input.press("Enter");
  await page.waitForTimeout(200);
  assert.equal(state.setCalls.length, 0, "Enter speichert nichts");

  await input.fill("x".repeat(500));
  assert.equal(await save.isDisabled(), false, "genau 500 gehen");

  await input.press("Escape");
  await editor(panel(page)).waitFor({ state: "detached" });
  assert.equal(await panel(page).count(), 1, "das Detail bleibt offen");
  assert.equal(state.setCalls.length, 0, "nichts gespeichert");
  await context.close();
});

test("PC: Memo nicht in YNAB zeigt Grund und Erneut senden", async () => {
  resetState();
  state.memoOf = { 1: "Notiz" };
  state.failureOf = { 1: "Keine Verbindung zu YNAB" };
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Memo");
  const notice = panel(page).locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  assert.match(await notice.textContent(), /Memo nicht in YNAB/);
  assert.match(await notice.textContent(), /Keine Verbindung zu YNAB/);
  await notice.getByRole("button", { name: "Erneut senden" }).click();
  for (let i = 0; i < 50 && state.retryCalls.length === 0; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.retryCalls, [[idOf(1)]]);
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-notice"]') === null);
  await context.close();
});

test("PC: bei einer in YNAB gelöschten Buchung ist die Memo-Aktion gesperrt und nennt den Grund", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "Kiosk Memo").locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-title"]')?.textContent === "Kiosk Memo");
  assert.equal(await memoAction(panel(page)).getAttribute("aria-disabled"), "true");
  assert.ok(
    (await panel(page).locator('[data-testid="detail-memo-lock-hint"]').textContent()).includes("In YNAB gelöscht")
  );
  await memoAction(panel(page)).click({ force: true });
  await page.waitForTimeout(200);
  assert.equal(await editor(panel(page)).count(), 0, "kein Feld");
  assert.equal(state.setCalls.length, 0, "kein Aufruf");
  await context.close();
});

test("Kategorie im Detail: PC voll qualifiziert, Handy kurz; der Picker trägt den Fußtext nur am PC", async () => {
  resetState();
  const pcContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const pc = await open(pcContext);
  await openDetail(pc, "Rewe Memo");
  const pcValue = await panel(pc).locator('[data-testid="detail-category"] span.min-w-0, [data-testid="detail-category"]').first().textContent();
  assert.ok(pcValue.includes("Alltag: Lebensmittel"), `PC: voll qualifiziert (${pcValue})`);
  await panel(pc).locator('[data-testid="detail-category-arrow"]').click();
  const hint = panel(pc).locator('[data-testid="category-picker-footer-hint"]');
  await hint.waitFor();
  assert.equal((await hint.textContent()).trim(), "Esc schließt ohne zu ändern · Klick wählt sofort");
  await pcContext.close();

  const mobileContext = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const mobile = await open(mobileContext);
  await mobile.getByText("Rewe Memo").first().tap();
  await sheet(mobile).waitFor();
  const text = await sheet(mobile).locator('[data-testid="detail-category"]').textContent();
  assert.ok(text.includes("Lebensmittel"), "Handy: der Blattname");
  assert.ok(!text.includes("Alltag"), `Handy: ohne Gruppe (${text})`);
  await sheet(mobile).locator('[data-testid="detail-category-arrow"]').tap();
  await mobile.locator('[role="listbox"]').first().waitFor();
  assert.equal(await mobile.locator('[data-testid="category-picker-footer-hint"]').count(), 0, "Handy: kein Fußtext");
  await mobileContext.close();
});

test("Handy: Memo im Sheet setzen, die Zeile in der Liste zeigt es", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("Rewe Memo").first().tap();
  await sheet(page).waitFor();
  await memoAction(sheet(page)).tap();
  await editor(sheet(page)).locator("input").fill("Mitbringsel");
  await editor(sheet(page)).getByRole("button", { name: "Speichern" }).tap();
  await page.waitForFunction(() => document.querySelector('[data-testid="detail-memo-editor"]') === null);
  assert.ok((await memoRow(sheet(page)).textContent()).includes("Mitbringsel"), "Sheet zeigt das Memo");
  const sheetBox = await memoAction(sheet(page)).boundingBox();
  assert.ok(sheetBox.height >= 44, `Trefferfläche mindestens 44 px (${sheetBox.height})`);
  await page.keyboard.press("Escape");
  await sheet(page).waitFor({ state: "detached" });
  assert.ok((await page.getByText("Mitbringsel").count()) >= 1, "die Zeile in der Liste zeigt das neue Memo");
  await context.close();
});

test("/styleguide: die Auswahl-Bausteine und das Memo haben Galerie-Karten", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const styleguide = await context.newPage();
  await styleguide.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  for (const id of [
    "components-multiselect-checkbox",
    "components-multiselect-summary",
    "components-multiselect-bar-mobile",
    "components-multiselect-bar-desktop",
    "components-detail-memo",
  ]) {
    await styleguide.locator(`[data-ds-card="${id}"]`).waitFor({ state: "attached" });
  }
  const checkbox = styleguide.locator('[data-ds-card="components-multiselect-checkbox"]');
  assert.equal(await checkbox.locator('[role="checkbox"]').getAttribute("aria-checked"), "mixed", "Kopfzeilen-Kästchen: Teilauswahl");
  assert.equal(await checkbox.locator("span.border").count() >= 6, true, "drei Zustände in zwei Größen");
  const summary = styleguide.locator('[data-ds-card="components-multiselect-summary"] [role="status"]');
  assert.match(await summary.textContent(), /Auswahl: 3 von 34 Buchungen/);

  // Die Leiste am PC bleibt in ihrer Karte (Rahmen mit `transform`) und zeigt ihre Aktionen.
  const desktop = styleguide.locator('[data-ds-card="components-multiselect-bar-desktop"]');
  await desktop.scrollIntoViewIfNeeded();
  const bar = desktop.locator(".selection-bar");
  assert.equal(await bar.isVisible(), true, "Leiste am PC sichtbar");
  const geometry = await bar.evaluate((el) => {
    const card = el.closest("[data-ds-card]").getBoundingClientRect();
    const rect = el.getBoundingClientRect();
    return { top: rect.top, bottom: rect.bottom, cardTop: card.top, cardBottom: card.bottom };
  });
  assert.ok(geometry.top >= geometry.cardTop - 1 && geometry.bottom <= geometry.cardBottom + 1, "Leiste liegt in der Karte");
  const names = await bar.locator("button").allTextContents();
  assert.ok(names.some((t) => t.includes("Geprüft")) && names.some((t) => t.includes("Kategorie setzen")), `Aktionen (${names})`);
  assert.equal(await styleguide.locator('[data-ds-card="components-multiselect-bar-mobile"] .selection-bar').isVisible(), false, "Leiste am Handy erst unter md");
  await context.close();

  const phone = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const phonePage = await phone.newPage();
  await phonePage.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  const mobileCard = phonePage.locator('[data-ds-card="components-multiselect-bar-mobile"]');
  await mobileCard.waitFor({ state: "attached" });
  await mobileCard.scrollIntoViewIfNeeded();
  const mobileBar = mobileCard.locator(".selection-bar");
  assert.equal(await mobileBar.isVisible(), true, "Leiste am Handy sichtbar");
  const inside = await mobileBar.evaluate((el) => {
    const card = el.closest("[data-ds-card]").getBoundingClientRect();
    const rect = el.getBoundingClientRect();
    return rect.top >= card.top - 1 && rect.bottom <= card.bottom + 1;
  });
  assert.equal(inside, true, "Leiste liegt in der Karte");
  const mobileNames = await mobileBar.locator("button").allTextContents();
  assert.ok(mobileNames.some((t) => t.includes("Auswahl aufheben")), `Aktionen (${mobileNames})`);
  await phone.close();
});
