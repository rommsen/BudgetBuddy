// scripts/target-change.test.mjs
//
// Wächter für `budgeting-k8wq3`: Target ändern im Tab Target. Der Hauptablauf gegen eine gemockte
// BudgetApi und, was nur der Browser messen kann: "Target speichern" liegt im 500-px-Sheet ohne Scrollen
// im Bild. Die Regeln selbst liegen in `src/Tests/TargetChangeClientTests.fs`.
//
// Ausführen: node --test scripts/target-change.test.mjs
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

const target = (goal) => ({
  Goal: eur(goal),
  Rhythm: { Months: 1 },
  Kind: "Need",
  Date: null,
  NeedsWholeAmount: true,
  Day: 15,
});

const monthFixture = (key, goal) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Rücklagen k8wq3",
      Categories: [
        { Id: guid(1), Name: "Urlaub k8wq3", Assigned: eur(0), Activity: eur(0), Available: eur(0), Underfunded: eur(goal), Target: target(goal), Pause: null },
      ],
    },
  ],
});

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { change: [] };
  let goal = 50;
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01", "2026-12-01"], CategoryOrder: [guid(1)] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key, goal) });
  });
  await page.route("**/api/BudgetApi/changeCategoryTarget", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    calls.change.push(body);
    goal = 60;
    json(route, { Ok: { TargetWritten: target(60) } });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const row = (page) => page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: "Urlaub k8wq3" });
const sheet = (page) => page.locator(".bottom-sheet.fixed-height.active");
const openTargetTab = async (page) => {
  await row(page).click();
  await sheet(page).getByRole("tab", { name: "Target" }).click();
  await sheet(page).locator('[data-testid="target-amount"] input').waitFor();
};
/// Wartet, bis der Aufruf beim Mock angekommen ist.
const until = async (page, condition) => {
  for (let i = 0; i < 600 && !condition(); i++) await page.evaluate(() => new Promise(requestAnimationFrame));
  assert.ok(condition(), "die Bedingung tritt ein");
};

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[flow:target-aendern] Handy: im Tab Target 60 tippen und speichern, der Auftrag trägt den gezeigten Stand", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await openTargetTab(page);
    await sheet(page).locator('[data-testid="target-amount"] input').fill("60");
    await sheet(page).locator('[data-testid="target-save"] button').click();
    await until(page, () => calls.change.length === 1);
    const [[month, request]] = calls.change;
    assert.equal(month, "2026-11-01");
    assert.equal(request.CategoryId, guid(1));
    assert.equal(String(request.Shown.Goal.Amount), "50");
    assert.equal(String(request.Wanted.NewGoal.Amount), "60");
    await page.waitForFunction(() => document.querySelector('.bottom-sheet.fixed-height.active [data-testid="target-amount"] input')?.value === "60,00");
  } finally {
    await context.close();
  }
});

test("[layout] Handy: Target speichern liegt im 500-px-Sheet ohne Scrollen im Bild", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await openTargetTab(page);
    const button = await sheet(page).locator('[data-testid="target-save"] button').boundingBox();
    const body = await sheet(page).locator(".sheet-body").boundingBox();
    assert.ok(button && body, "Knopf und Sheet sind da");
    assert.ok(button.y + button.height <= body.y + body.height, `Knopf im Bild: ${button.y + button.height} gegen ${body.y + body.height}`);
  } finally {
    await context.close();
  }
});

test("[fokus] Handy: Target entfernen öffnet die Rückfrage im Sheet, Abbrechen hat den Fokus", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await openTargetTab(page);
    await sheet(page).locator('[data-testid="target-remove"] button').click();
    const dialog = sheet(page).getByRole("alertdialog", { name: "Target entfernen?" });
    await dialog.waitFor();
    await page.waitForFunction(() => document.activeElement?.textContent === "Abbrechen");
    assert.equal(
      await page.evaluate(() => document.activeElement.closest('[role="alertdialog"]') !== null),
      true,
      "der Fokus liegt im Dialog auf Abbrechen"
    );
  } finally {
    await context.close();
  }
});
