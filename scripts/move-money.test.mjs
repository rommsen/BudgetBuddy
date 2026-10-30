// scripts/move-money.test.mjs
//
// Wächter für `budgeting-afd8h`: das Sheet "Verschieben" auf der Budget-Seite. Gemessen am echten,
// per Vite gestarteten Fable-Client gegen eine gemockte BudgetApi: Handy-Fluss vom Tipp auf
// Verfügbar bis zu den neuen Beträgen, Trefferfläche, Dialog am PC mit dem Picker als Popover,
// Lesefehler lässt das Sheet offen, "Erneut senden" am Toast.
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
  Snoozed: false,
});

const monthFixture = (food, trip) => ({
  Month: "2026-11-01",
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
  const calls = { move: [], retry: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => json(route, { Ok: monthFixture(state.food, state.trip) }));
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(route.request().postData());
    json(route, handlers.move(state));
  });
  await page.route("**/api/BudgetApi/retryFailedTargets", (route) => {
    calls.retry.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, handlers.retry(state));
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const visibleRow = (page, name) =>
  page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });

const tapAvailable = async (page, name) => {
  await visibleRow(page, name).locator('[data-testid="budget-available"]').click();
  await page.locator('[data-testid="move-dialog"]').waitFor();
};

// Das Picker-Sheet schluckt nach der Wahl 350 ms lang den nächsten Klick (ADR 0005); ein Mensch
// tippt später, der Test wartet die Sperre ab.
const afterPicker = (page) => page.waitForTimeout(400);

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

test("Handy: Verfügbar antippen, Gegenseite suchen, Betrag ändern, Verschieben; die Zeilen zeigen die neuen Beträge", async () => {
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
    const box = await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]').boundingBox();
    assert.ok(box && box.height >= 48 && box.width >= 48, "Trefferfläche mindestens 48 px");

    await visibleRow(page, "Lebensmittel afd8h").getByText("Lebensmittel afd8h").click();
    assert.equal(await page.locator('[data-testid="move-dialog"]').count(), 0, "Tipp auf den Namen öffnet nichts");

    await tapAvailable(page, "Lebensmittel afd8h");
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Von Lebensmittel afd8h nach/);
    assert.equal(await page.locator('[data-testid="move-amount"] input').inputValue(), "80,00");
    const submit = page.locator('[data-testid="move-submit"] button');
    assert.ok(await submit.isDisabled(), "ohne Gegenseite gesperrt");

    await page.locator('[data-testid="move-counterpart"] button').click();
    const firstOption = page.locator('.bottom-sheet.layer-2 [role="option"]').first();
    await firstOption.waitFor();
    assert.match(await text(firstOption), /Zu verteilen.*35,45 €/, "Zu verteilen steht ganz oben, mit Betrag");
    const options = await page.locator('.bottom-sheet.layer-2 [role="option"]').allInnerTexts();
    assert.ok(!options.some((o) => o.includes("Lebensmittel afd8h")), "X ist nicht wählbar");

    await page.locator('.bottom-sheet.layer-2 input').fill("urlaub");
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();

    await page.locator('[data-testid="move-amount"] input').fill("20");
    const after = await page.locator('[data-testid="move-after-line"]').allInnerTexts();
    assert.match(after[0].replace(/\s+/g, " "), /Lebensmittel afd8h 60,00 €/);
    assert.match(after[1].replace(/\s+/g, " "), /Urlaub afd8h -10,00 €/);
    const negative = page.locator('[data-testid="move-after-line"]').nth(1).locator("span").nth(1);
    assert.match(await negative.getAttribute("class"), /text-neon-red/, "negatives Ergebnis ist rot");

    await afterPicker(page);
    await submit.click();
    await page.locator('[data-testid="move-dialog"]').waitFor({ state: "detached" });
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
    await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]', { hasText: "60,00 €" }).waitFor();
    assert.match(await text(visibleRow(page, "Urlaub afd8h")), /-10,00 €/);
    assert.equal(calls.move.length, 1);
    assert.ok(calls.move[0].includes(guid(1)) && calls.move[0].includes(guid(2)), "Quelle und Ziel im Auftrag");
  } finally {
    await context.close();
  }
});

test("Handy: scheitert das Lesen des Monats, bleibt das Sheet offen und nennt den Grund", async () => {
  const { context, page } = await openPage(390, 844, {
    move: () => ({ Error: "YNAB antwortet nicht afd8h" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Urlaub afd8h");
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Urlaub afd8h decken aus/);
    assert.equal(await page.locator('[data-testid="move-amount"] input').inputValue(), "30,00");
    await page.locator('[data-testid="move-counterpart"] button').click();
    await page.locator('[data-testid="category-picker-to-be-budgeted"]').click();
    await afterPicker(page);
    await page.locator('[data-testid="move-submit"] button').click();
    await page.locator('[data-testid="move-error"]').getByText("YNAB antwortet nicht afd8h").waitFor();
    assert.ok(await page.locator('[data-testid="move-dialog"]').isVisible(), "Sheet bleibt offen");
  } finally {
    await context.close();
  }
});

test("Fehlgeschlagener PATCH: der Toast bietet Erneut senden an, der Klick sendet dieselbe Anwendung", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Ok: report("Written") }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    await page.locator('[data-testid="move-counterpart"] button').click();
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
    await afterPicker(page);
    await page.locator('[data-testid="move-submit"] button').click();
    await page.getByText(/Nicht vollständig verschoben: Rate-Limit/).first().waitFor();
    await page.getByRole("button", { name: "Erneut senden" }).first().click();
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
  } finally {
    await context.close();
  }
});

test("PC: Tipp auf Verfügbar öffnet den Dialog, der Picker hängt als Popover daran und wird nicht abgeschnitten", async () => {
  const { context, page } = await openPage(1440, 900, {
    move: () => ({ Error: "nicht erwartet" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "kein Sheet am PC");
    await page.locator('[data-testid="move-counterpart"] button').click();
    const top = page.locator('[data-testid="category-picker-to-be-budgeted"]');
    await top.waitFor();
    const options = page.locator('[role="option"]');
    assert.equal(await options.count(), 3, "Zu verteilen und zwei Kategorien, X fehlt");
    const last = await options.last().boundingBox();
    assert.ok(last && last.y + last.height <= 900, "die Liste liegt im Bild");
    await options.last().click();
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Von Lebensmittel afd8h nach Kleidung afd8h/);
    assert.equal(await page.locator('[role="option"]').count(), 0, "Popover ist zu");
  } finally {
    await context.close();
  }
});
