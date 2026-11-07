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
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(route.request().postData());
    const answer = handlers.move(state);
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

test("[flow:geld-verschieben] Handy: Zeile wählen, Abgeben, Gegenseite suchen, Betrag tippen, abgeben; die Zeilen zeigen die neuen Beträge", async () => {
  const handlers = {
    move: (state) => {
      state.food -= 20;
      state.trip += 20;
      return { Ok: report("Written") };
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
    const firstOption = page.locator('.bottom-sheet.layer-2 [role="option"]').first();
    await firstOption.waitFor();
    assert.match(await text(firstOption), /Zu verteilen.*35,45 €/, "Zu verteilen steht ganz oben, mit Betrag");
    const options = await page.locator('.bottom-sheet.layer-2 [role="option"]').allInnerTexts();
    assert.ok(!options.some((o) => o.includes("Lebensmittel afd8h")), "X ist nicht wählbar");

    await page.locator('.bottom-sheet.layer-2 input').fill("urlaub");
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();

    await page.locator('[data-testid="transfer-amount"] input').fill("20");
    assert.match(await text(page.locator('[data-testid="transfer-from"]')), /von Lebensmittel afd8h 60,00 €/);
    assert.match(await text(page.locator('[data-testid="transfer-to"]')), /nach Urlaub afd8h -10,00 €/);
    assert.equal(await text(submit), "20,00 € an Urlaub afd8h abgeben");

    await afterPicker(page);
    await submit.click();
    await formReset(page);
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
    await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]', { hasText: "60,00 €" }).waitFor();
    assert.match(await text(visibleRow(page, "Urlaub afd8h")), /-10,00 €/);
    assert.equal(calls.move.length, 1);
    assert.ok(calls.move[0].includes(guid(1)) && calls.move[0].includes(guid(2)), "Quelle und Ziel im Auftrag");
    assert.ok(calls.move[0].includes("2026-11-01"), "der Client schickt den gezeigten Monat");
    assert.ok(await page.locator(".bottom-sheet.fixed-height.active").isVisible(), "der Inspektor bleibt offen");
    assert.equal(await page.locator('[data-testid="money-mode"][aria-pressed="true"]').innerText(), "Abgeben", "der Modus bleibt");
  } finally {
    await context.close();
  }
});

test("[flow:verschiebung-erneut-senden] Fehlgeschlagener PATCH: der Hinweis steht in jedem Tab, der Toast bietet Erneut senden an, der Klick sendet dieselbe Anwendung", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Ok: report("Written") }),
  });
  try {
    await openMoveFor(page, "Lebensmittel afd8h");
    await amountOf20(page);
    await page.locator('[data-testid="transfer-to"]').click();
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
    await afterPicker(page);
    await page.locator('[data-testid="transfer-submit"] button').click();
    await page.getByText(/Nicht vollständig verschoben: Rate-Limit/).first().waitFor();
    // Der Hinweis der offenen Verschiebung steht in jedem Tab des Sheets (aus dem früheren Layout-Test in
    // budget-money-tab.test.mjs hierher gezogen).
    const hint = page.locator('.bottom-sheet.fixed-height.active [data-testid="move-retry"]');
    const tabs = await page.locator(".bottom-sheet.fixed-height.active").getByRole("tab").all();
    for (const tab of [...tabs, tabs[0]]) {
      await tab.click();
      await hint.waitFor();
    }
    await page.locator('button:has-text("Erneut senden"):not([data-testid="move-retry"] button)').click();
    await page.locator('[data-testid="move-retry"]').waitFor({ state: "detached" });
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
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

