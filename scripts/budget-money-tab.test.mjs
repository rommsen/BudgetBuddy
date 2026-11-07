// scripts/budget-money-tab.test.mjs
//
// Wächter für `budgeting-c5w2n`: was nur der Browser messen kann am Tab Geld. Fokus, Markieren,
// Fensterwechsel und der Hauptablauf gegen eine gemockte BudgetApi. Feld und Knopf ohne Scrollen
// (auch bei offener Tastatur) und lange Namen prüft der Layout-Sweep (Zustände budget-geld-*,
// budget-picker), den Hinweis einer offenen Verschiebung in jedem Tab der Flow
// verschiebung-erneut-senden in `move-money.test.mjs`. Die Regeln selbst liegen in
// `src/Tests/MoveMoneyClientTests.fs`.
//
// Ausführen: node --test scripts/budget-money-tab.test.mjs
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
const LONG = "Eine Kategorie mit einem sehr langen Namen, genau sechzig Ze.";

const category = (n, name, assigned, available) => ({
  Id: guid(n),
  Name: name,
  Assigned: eur(assigned),
  Activity: eur(0),
  Available: eur(available),
  Underfunded: null,
  Target: null,
  Pause: null,
  OpenWriteBack: null,
});

const monthFixture = (key) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Alltag c5w2n",
      Categories: [
        category(1, "Lebensmittel c5w2n", 900, 80),
        // Mit Target: der Pause-Knopf steht im Modus Zuweisen (budgeting-r3wq8).
        { ...category(2, "Rot c5w2n", 0, -12.3), Target: { Goal: eur(20), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null } },
        category(3, LONG, 10, 10),
      ],
    },
  ],
});

const emptyReport = (key) => ({ ApplicationId: guid(9), Month: key, Lines: [], ActualToBeBudgeted: null });

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { assign: [], move: [], recent: 0, pause: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01", "2026-12-01"], CategoryOrder: [guid(1), guid(2), guid(3)], MonthStartHidden: [] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key) });
  });
  await page.route("**/api/BudgetApi/getRecentMoveCategories", (route) => {
    calls.recent += 1;
    json(route, { Ok: [guid(3), guid(1)] });
  });
  await page.route("**/api/BudgetApi/assignMoney", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    calls.assign.push(body);
    json(route, { Ok: emptyReport(body[0]) });
  });
  await page.route("**/api/BudgetApi/setTargetPause", (route) => {
    calls.pause.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, { Ok: "PausedInBb" });
  });
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, { Ok: emptyReport("2026-11-01") });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const row = (page, name) => page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });
const field = (page) => page.locator('[data-testid="assign-field"] input');
const mode = (page, name) => page.locator('[data-testid="money-mode"]').filter({ hasText: name });
const step = (page, label) => page.locator('[data-testid="assign-step"]').filter({ hasText: label }).first();
const isFieldActive = (page) =>
  page.evaluate(() => document.activeElement === document.querySelector('[data-testid="assign-field"] input'));
/// Wartet, bis der Aufruf beim Mock angekommen ist.
const until = async (page, condition) => {
  for (let i = 0; i < 600 && !condition(); i++) await page.evaluate(() => new Promise(requestAnimationFrame));
  assert.ok(condition(), "die Bedingung tritt ein");
};
const twoFrames = (page) =>
  page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[fokus] Beim Fokus ist der Inhalt des Felds markiert", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).click();
    await page.waitForFunction(() => {
      const input = document.querySelector('[data-testid="assign-field"] input');
      return input.selectionStart === 0 && input.selectionEnd === input.value.length;
    });
    await page.keyboard.type("+20");
    assert.equal(await field(page).inputValue(), "+20", "das Tippen ersetzt den Inhalt");
  } finally {
    await context.close();
  }
});

test("[fable] Das Feld rechnet im echten Client wie unter .NET", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("100/3");
    await page.locator('[data-testid="assign-preview"]').getByText(/Ergebnis 33,33 €/).waitFor();
    await field(page).fill("0,05/2+1.240,50");
    await page.locator('[data-testid="assign-preview"]').getByText(/Ergebnis 1\.240,53 €/).waitFor();
    await field(page).fill("5/0");
    await page.locator('[data-testid="assign-preview"]').getByText("Keine gültige Rechnung").waitFor();
  } finally {
    await context.close();
  }
});

test("[fokus] Der Wechsel in ein anderes Programm speichert nicht: das Feld bleibt das aktive Element", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("+20");
    // So kommt der Wechsel beim Feld an: Blur ohne neues aktives Element.
    await field(page).dispatchEvent("focusout");
    await twoFrames(page);
    assert.ok(await isFieldActive(page), "Vorbedingung: das Feld bleibt aktiv");
    assert.equal(await field(page).inputValue(), "+20", "die Eingabe steht");
    assert.equal(calls.assign.length, 0, "nichts gesendet");
    await page.locator("h2, h1").first().evaluate(() => document.activeElement.blur());
    await page.locator('[data-testid="assign-field"] input[value="920,00"]').waitFor();
    assert.equal(calls.assign.length, 1, "ein echtes Blur speichert");
  } finally {
    await context.close();
  }
});

test("[flow:geld-zuweisen] Handy, Hauptablauf: Zeile wählen, +20 tippen, weiter mit dem Pfeil; die Zuweisung trägt Monat und Wert der Eingabe", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("+20");
    await page.locator('[data-testid="inspector-next"]').click();
    await page.locator(".bottom-sheet.fixed-height.active").getByRole("heading", { name: "Rot c5w2n" }).waitFor();
    await until(page, () => calls.assign.length === 1);
    // Die Befehls-Id ist je Absicht beliebig, sie bleibt hier außen vor. Dass sie gesetzt ist und eine
    // Wiederholung dieselbe trägt, prüft src/Tests/BudgetCommandCmdTests.fs.
    const [[month, { CommandId: _commandId, ...request }]] = calls.assign;
    assert.deepEqual([month, request], ["2026-11-01", { CategoryId: guid(1), Assigned: { Amount: "920", Currency: "EUR" } }]);
  } finally {
    await context.close();
  }
});

// Trefferflächen, Feldschrift und dass die Schritte passen, prüft der Layout-Sweep (Zustände budget-geld-*).
test("[fokus] Handy: ein Tipp auf Schritt, Fehlbetrag- oder Pause-Knopf lässt den Fokus im Feld", async () => {
  const { context, page, calls } = await openPage(360, 800);
  try {
    await row(page, "Rot c5w2n").click();
    await mode(page, "Zuweisen").click();
    await field(page).focus();
    await step(page, "+10").tap();
    assert.ok(await isFieldActive(page), "Schritt: der Fokus bleibt");
    assert.equal(await field(page).inputValue(), "10,00", "der Schritt wirkt");

    await page.locator('[data-testid="assign-missing"] button').tap();
    await until(page, () => calls.move.length === 1);
    assert.ok(await isFieldActive(page), "Knopf: der Fokus bleibt");

    await page.locator('[data-testid="target-pause"] button').tap();
    await until(page, () => calls.pause.length === 1);
    assert.ok(await isFieldActive(page), "Pause-Knopf: der Fokus bleibt");
  } finally {
    await context.close();
  }
});

test("[tastatur] PC, Panel: Feld tippen, Enter, Pfeil runter wählt die nächste Zeile, keine Schritte", async () => {
  const { context, page, calls } = await openPage(1280, 800);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await panel.getByRole("heading", { name: "Lebensmittel c5w2n" }).waitFor();
    assert.equal(await page.locator('[data-testid="assign-step"]').count(), 0, "keine Schritte im Panel");
    await field(page).click();
    await page.keyboard.type("+20");
    await page.keyboard.press("Enter");
    await page.waitForFunction(() => document.activeElement.tagName !== "INPUT");
    await page.keyboard.press("ArrowDown");
    await panel.getByRole("heading", { name: "Rot c5w2n" }).waitFor();
    await until(page, () => calls.assign.length === 1);

    const fieldLeft = () => page.waitForFunction(() => document.activeElement.tagName !== "INPUT");
    await mode(page, "Zuweisen").click();
    await field(page).click();
    await page.keyboard.type("+5");
    await page.keyboard.press("Escape");
    await fieldLeft();
    await mode(page, "Holen").click();
    await page.locator('[data-testid="transfer-amount"] input').click();
    await page.keyboard.press("Enter");
    await fieldLeft();
  } finally {
    await context.close();
  }
});
