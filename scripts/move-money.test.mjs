// scripts/move-money.test.mjs
//
// Wächter für `budgeting-afd8h`, `budgeting-c5w2n` und `budgeting-nfrds`: Holen und Abgeben im Tab
// Geld. Der Handy-Fluss von der Zeile bis zu den neuen Beträgen, "Erneut senden" am Toast und das
// Popover im Panel. Die Regeln liegen in `src/Tests/MoveMoneyClientTests.fs`.
//
// Ausführen: node --test scripts/move-money.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";

let frontend;
let frontendPort;
let browser;

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;

const category = (n, name, available) => ({
  Id: guid(n),
  Name: name,
  Assigned: eur(available),
  Activity: eur(0),
  Available: eur(available),
  Underfunded: null,
  Target: null,
  Pause: null,
  OpenWriteBack: null,
});

const monthFixture = (food, trip, month = "2026-11-01") => ({
  Month: month,
  ToBeBudgeted: eur(35.45),
  Groups: [
    {
      Name: "Alltag afd8h",
      Categories: [
        category(1, "Lebensmittel afd8h", food),
        category(2, "Urlaub afd8h", trip),
        category(3, "Kleidung afd8h", 10),
      ],
    },
  ],
});

const report = (outcome) => ({
  ApplicationId: "11111111-1111-1111-1111-111111111111",
  Month: "2026-11-01",
  Lines: [
    { CategoryId: guid(1), CategoryName: "Lebensmittel afd8h", OldAssigned: eur(80), NewAssigned: eur(60), Outcome: "Written" },
    { CategoryId: guid(2), CategoryName: "Urlaub afd8h", OldAssigned: eur(-30), NewAssigned: eur(-10), Outcome: outcome },
  ],
  ActualToBeBudgeted: null,
});

async function openPage(width, height, handlers) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const state = { food: 80, trip: -30 };
  const calls = { move: [], retry: [], month: 0, monthKeys: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: [guid(1), guid(2), guid(3)], MonthStartHidden: [] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    calls.month += 1;
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    calls.monthKeys.push(key);
    json(route, { Ok: monthFixture(state.food, state.trip, key) });
  });
  await page.route("**/api/BudgetApi/getRecentMoveCategories", (route) => json(route, { Ok: [] }));
  // Ohne diese Antworten meldete die App beim Start Fehler, die als Toast neben der Zeile im Sheet stünden.
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (route) => json(route, { SavedColor: null, SuggestedColor: null }));
  await page.route("**/api/SettingsApi/getSettings", (route) =>
    json(route, { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: { YnabBudgetId: "fake-budget-id" }, DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14, AutoFetchEnabled: true, AutoFetchIntervalMinutes: 360 } })
  );
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(route.request().postData());
    const [, sent] = JSON.parse(route.request().postData() ?? "[]");
    const answer = handlers.move(state, sent);
    return answer === "abort" ? route.abort() : json(route, answer);
  });
  await page.route("**/api/BudgetApi/retryFailedTargets", (route) => {
    calls.retry.push(JSON.parse(route.request().postData() ?? "[]"));
    const answer = handlers.retry(state);
    return answer === "abort" ? route.abort() : json(route, answer);
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const visibleRow = (page, name) =>
  page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });

/// Zeile wählen, im Tab Geld den Modus wählen. Abgeben hat die Gegenseite in `nach`.
const openMoveFor = async (page, name, mode = "Abgeben") => {
  await visibleRow(page, name).click();
  await page.locator('[data-testid="money-mode"]').filter({ hasText: mode }).first().click();
  await page.locator('[data-testid="transfer-amount"] input').waitFor();
};

// Das Picker-Sheet schluckt nach der Wahl den nächsten Klick (ADR 0005). Ein Klick auf eine Stelle
// ohne Wirkung nimmt die Sperre weg, statt ihre Dauer abzuwarten.
const afterPicker = (page) => page.locator('.bottom-sheet.active [data-testid="detail-activity"]').click();

/// Die Form ist zurückgesetzt: die Antwort ist da.
const formReset = (page) =>
  page.waitForFunction(() => document.querySelector('[data-testid="transfer-amount"] input')?.value === "");

const amountOf20 = (page) => page.locator('[data-testid="transfer-amount"] input').fill("20");

const text = async (locator) => (await locator.innerText()).replace(/\s+/g, " ");

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

/// Die Antwort auf das Rückgängig: 20,00 € von Urlaub zurück nach Lebensmittel (budgeting-y1kkr).
const undoReport = () => ({
  ApplicationId: "22222222-2222-2222-2222-222222222222",
  Month: "2026-11-01",
  Lines: [
    { CategoryId: guid(2), CategoryName: "Urlaub afd8h", OldAssigned: eur(-10), NewAssigned: eur(-30), Outcome: "Written" },
    { CategoryId: guid(1), CategoryName: "Lebensmittel afd8h", OldAssigned: eur(60), NewAssigned: eur(80), Outcome: "Written" },
  ],
  ActualToBeBudgeted: null,
});

test("[flow:geld-verschieben] Handy: Zeile wählen, Abgeben, Gegenseite suchen, Betrag tippen, abgeben, Rückgängig; die Zeilen zeigen erst die neuen, dann wieder die alten Beträge", async () => {
  const handlers = {
    // Relativ wie der Server: die Gegenrichtung bucht denselben Betrag zurück.
    move: (state, sent) => {
      const back = sent.Source.CategorySide === guid(2);
      const delta = (back ? -1 : 1) * Number(sent.Amount.Amount);
      state.food -= delta;
      state.trip += delta;
      return { Ok: back ? undoReport() : report("Written") };
    },
    retry: () => ({ Error: "nicht erwartet" }),
  };
  const { context, page, calls } = await openPage(390, 844, handlers);
  try {
    await openMoveFor(page, "Lebensmittel afd8h");
    assert.equal(await page.locator('[data-testid="transfer-amount"] input').inputValue(), "");
    const submit = page.locator('[data-testid="transfer-submit"] button');
    assert.ok(await submit.isDisabled(), "ohne Gegenseite gesperrt");

    await page.locator('[data-testid="transfer-to"]').click();
    const firstOption = page.locator('.bottom-sheet.active [role="option"]').first();
    await firstOption.waitFor();
    assert.match(await text(firstOption), /Zu verteilen.*35,45 €/, "Zu verteilen steht ganz oben, mit Betrag");
    const options = await page.locator('.bottom-sheet.active [role="option"]').allInnerTexts();
    assert.ok(!options.some((o) => o.includes("Lebensmittel afd8h")), "X ist nicht wählbar");

    await page.locator('.bottom-sheet.active input').fill("urlaub");
    await page.locator('.bottom-sheet.active [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();

    await page.locator('[data-testid="transfer-amount"] input').fill("20");
    assert.match(await text(page.locator('[data-testid="transfer-from"]')), /von Lebensmittel afd8h 60,00 €/);
    assert.match(await text(page.locator('[data-testid="transfer-to"]')), /nach Urlaub afd8h -10,00 €/);
    assert.equal(await text(submit), "20,00 € an Urlaub afd8h abgeben");

    await afterPicker(page);
    await submit.click();
    await formReset(page);
    // Regel R3 (budgeting-gtrgk): das Ergebnis steht als Zeile über dem Hauptknopf im Sheet, aus Sicht der Kategorie,
    // kein Toast und keine Meldung in der Leiste.
    const line = page.locator('.bottom-sheet.active [data-testid="feedback-sheet-line"]');
    await line.filter({ hasText: "20,00 € nach Urlaub afd8h" }).waitFor();
    assert.match(await text(line), /^✓ 20,00 € nach Urlaub afd8h Rückgängig$/, "✓, Text und Rückgängig (budgeting-y1kkr)");
    assert.equal(await page.locator(".toast-layer > *").count(), 0, "kein Toast im DOM");
    assert.equal(await page.getByTestId("feedback-message").count(), 0, "keine Meldung in der Leiste");
    await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]', { hasText: "60,00 €" }).waitFor();
    assert.match(await text(visibleRow(page, "Urlaub afd8h")), /-10,00 €/);
    assert.equal(calls.move.length, 1);
    assert.ok(calls.move[0].includes(guid(1)) && calls.move[0].includes(guid(2)), "Quelle und Ziel im Auftrag");
    assert.ok(calls.move[0].includes("2026-11-01"), "der Client schickt den gezeigten Monat");
    assert.ok(await page.locator(".bottom-sheet.fixed-height.active").isVisible(), "der Inspektor bleibt offen");
    assert.equal(await page.locator('[data-testid="money-mode"][aria-pressed="true"]').innerText(), "Abgeben", "der Modus bleibt");

    // budgeting-y1kkr, ADR 0099: Rückgängig ist dieselbe Verschiebung in die Gegenrichtung, mit eigener Befehls-Id.
    await line.locator('[data-feedback-action="true"]', { hasText: "Rückgängig" }).click();
    await line.filter({ hasText: "Rückgängig: 20,00 € zurück von Urlaub afd8h" }).waitFor();
    assert.match(await text(line), /^✓ Rückgängig: 20,00 € zurück von Urlaub afd8h$/, "Ergebnis ohne Aktion");
    assert.equal(await line.locator('[data-feedback-action="true"]').count(), 0, "kein Rückgängig des Rückgängig");
    assert.equal(await page.locator(".toast-layer > *").count(), 0, "kein Toast im DOM");
    assert.equal(calls.move.length, 2, "genau ein zweiter Auftrag");
    const [firstMonth, first] = JSON.parse(calls.move[0]);
    const [secondMonth, second] = JSON.parse(calls.move[1]);
    assert.deepEqual(second.Source, first.Target, "Quelle ist das alte Ziel");
    assert.deepEqual(second.Target, first.Source, "Ziel ist die alte Quelle");
    assert.deepEqual(second.Amount, first.Amount, "derselbe Betrag");
    assert.equal(secondMonth, firstMonth, "derselbe Monat");
    assert.ok(second.CommandId && first.CommandId && second.CommandId !== first.CommandId, "eine andere Befehls-Id");
    await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]', { hasText: "80,00 €" }).waitFor();
    assert.match(await text(visibleRow(page, "Urlaub afd8h")), /-30,00 €/);
  } finally {
    await context.close();
  }
});

test("[flow:verschiebung-erneut-senden] Fehlgeschlagener PATCH: der Hinweis steht in jedem Tab, die Zeile im Sheet bietet Erneut senden an, der Klick sendet dieselbe Anwendung", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Ok: report("Written") }),
  });
  try {
    await openMoveFor(page, "Lebensmittel afd8h");
    await amountOf20(page);
    await page.locator('[data-testid="transfer-to"]').click();
    await page.locator('.bottom-sheet.active [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
    await afterPicker(page);
    await page.locator('[data-testid="transfer-submit"] button').click();
    // Regel R3 (budgeting-gtrgk): bei offenem Sheet steht der Fehler rot in der Zeile über dem Hauptknopf, mit
    // Erneut senden.
    const line = page.locator('.bottom-sheet.active [data-testid="feedback-sheet-line"]');
    await line.filter({ hasText: "Nicht vollständig verschoben: Rate-Limit" }).waitFor();
    assert.equal(await line.getAttribute("data-tone"), "error", "rot");
    // Der Hinweis der offenen Verschiebung steht in jedem Tab des Sheets (aus dem früheren Layout-Test in
    // budget-money-tab.test.mjs hierher gezogen).
    const hint = page.locator('.bottom-sheet.fixed-height.active [data-testid="move-retry"]');
    const tabs = await page.locator(".bottom-sheet.fixed-height.active").getByRole("tab").all();
    for (const tab of [...tabs, tabs[0]]) {
      await tab.click();
      await hint.waitFor();
    }
    await line.locator('[data-feedback-action="true"]', { hasText: "Erneut senden" }).click();
    await line.filter({ hasText: "20,00 € nach Urlaub afd8h" }).waitFor();
    // Nach dem gelungenen Erneut senden ist der Hinweis der offenen Verschiebung weg.
    await page.locator('[data-testid="move-retry"]').waitFor({ state: "detached" });
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
  } finally {
    await context.close();
  }
});

// Bug budgeting-nfrds: der Mausdruck zählte als Klick außerhalb und schloss das Popover, der Klick öffnete
// es wieder. Der Filter kürzt das Popover, der Auslöser bleibt liegen. Dass das Popover ganz im Panel liegt
// und sein Fuß nicht verdeckt ist, prüft der Layout-Sweep (Zustand budget-picker).
test("[tastatur] Panel 1280 × 800: ein Mausklick auf den Auslöser schließt das offene Kategorie-Popover", async () => {
  const { context, page } = await openPage(1280, 800, {
    move: () => ({ Error: "nicht erwartet" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await openMoveFor(page, "Lebensmittel afd8h");
    const trigger = page.locator('[data-testid="transfer-to"]');
    await trigger.click();
    const hint = page.locator('[data-testid="category-picker-footer-hint"]');
    await hint.waitFor();
    await page.keyboard.type("zzzz");
    await page.locator('[data-testid="category-picker-to-be-budgeted"]').waitFor({ state: "detached" });
    // Gewartet wird auf das Ende des Klicks und zwei Bilder danach: ein Wiederöffnen wäre dann gerendert.
    const clicked = page.evaluate(() => new Promise((done) => window.addEventListener("click", () => requestAnimationFrame(() => requestAnimationFrame(done)), { once: true, capture: true })));
    await trigger.click();
    await clicked;
    assert.equal(await hint.count(), 0, "bleibt zu");
  } finally {
    await context.close();
  }
});

