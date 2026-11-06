// scripts/target-change.test.mjs
//
// Wächter für `budgeting-k8wq3`, `budgeting-xp46a` und `budgeting-g2ty9`: Target ändern und anlegen im Tab
// Target. Die Hauptabläufe gegen eine gemockte BudgetApi und, was nur der Browser messen kann: "Target
// speichern", "Target anlegen" und die Warnung vor dem Wechsel der Art liegen im 500-px-Sheet ohne Scrollen
// im Bild, der Fokus nach einer Rückfrage im Sheet, das Datumsfeld im 340-px-Panel am PC.
// Die Regeln selbst liegen in `src/Tests/TargetChangeClientTests.fs`.
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
  CreatedIn: null,
});

const monthFixture = (key, goal) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Rücklagen k8wq3",
      Categories: [
        {
          Id: guid(1),
          Name: "Urlaub k8wq3",
          Assigned: eur(0),
          Activity: eur(0),
          Available: eur(0),
          Underfunded: goal === null ? null : eur(goal),
          Target: goal === null ? null : target(goal),
          Pause: null,
          OpenWriteBack: null,
        },
      ],
    },
  ],
});

/// `initialGoal = null`: die Kategorie hat kein Target (budgeting-xp46a).
async function openPage(width, height, initialGoal = 50) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { change: [] };
  let goal = initialGoal;
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
    goal = initialGoal === null ? 50 : 60;
    json(route, { Ok: { TargetWritten: target(goal) } });
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

// budgeting-g2ty9 (F3): nach Abbrechen und nach dem Bestätigen lag der Fokus auf `body`, Escape schloss
// die Rückfrage nicht.
test("[fokus] Handy: die Rückfrage zu Target entfernen fokussiert Abbrechen, danach geht der Fokus zurück auf Target entfernen", async () => {
  const { context, page, calls } = await openPage(390, 844);
  let release;
  // Die Antwort auf das Entfernen bleibt aus, solange der Test den Knopf mit dem laufenden Aufruf prüft.
  await page.route("**/api/BudgetApi/changeCategoryTarget", async (route) => {
    calls.change.push(JSON.parse(route.request().postData() ?? "[]"));
    await new Promise((resolve) => (release = resolve));
    await route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"Ende des Tests"}' });
  });
  const removeFocused = () =>
    page.waitForFunction(() => document.activeElement?.closest('[data-testid="target-remove"]') !== null);
  try {
    await openTargetTab(page);
    const remove = sheet(page).locator('[data-testid="target-remove"] button');
    const dialog = sheet(page).getByRole("alertdialog", { name: "Target entfernen?" });
    await remove.click();
    await dialog.waitFor();
    await page.waitForFunction(() => document.activeElement?.textContent === "Abbrechen");
    assert.equal(
      await page.evaluate(() => document.activeElement.closest('[role="alertdialog"]') !== null),
      true,
      "der Fokus liegt im Dialog auf Abbrechen"
    );
    await dialog.getByRole("button", { name: "Abbrechen" }).click();
    await removeFocused();
    await remove.click();
    await dialog.waitFor();
    await page.keyboard.press("Escape");
    await dialog.waitFor({ state: "detached" });
    await removeFocused();
    await remove.click();
    await dialog.getByRole("button", { name: "Entfernen" }).click();
    await until(page, () => calls.change.length === 1);
    await removeFocused();
    assert.equal(await remove.getAttribute("aria-busy"), "true", "der fokussierte Knopf trägt den laufenden Aufruf");
  } finally {
    release?.();
    await context.close();
  }
});

// budgeting-f4k9n (M1): bei offener Warnung schloss das erste getippte Zeichen sie, der Fokus sprang auf
// "Target speichern", die Tastatur ging zu und weitere Zeichen gingen verloren.
test("[fokus] Handy: bei offener Warnung bleibt der Fokus beim Tippen im Betragsfeld, ein mehrstelliger Betrag kommt ganz an", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await openTargetTab(page);
    await sheet(page).getByRole("button", { name: "Jahr" }).click();
    await sheet(page).getByRole("button", { name: "Target speichern" }).click();
    const dialog = sheet(page).getByRole("alertdialog", { name: "Rhythmus wechseln?" });
    await dialog.waitFor();
    const amount = sheet(page).locator('[data-testid="target-amount"] input');
    await amount.click();
    await amount.selectText();
    await page.keyboard.type("75", { delay: 50 });
    await dialog.waitFor({ state: "detached" });
    // Die Rückgabe käme per setTimeout 0 nach dem Render: zwei Frames abwarten.
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await page.keyboard.type("5", { delay: 50 });
    assert.equal(
      await page.evaluate(() => document.activeElement?.closest('[data-testid="target-amount"]') !== null),
      true,
      "der Fokus liegt im Betragsfeld"
    );
    assert.equal(await amount.inputValue(), "755", "alle Zeichen sind im Feld");
    assert.equal(calls.change.length, 0, "nichts gesendet");
  } finally {
    await context.close();
  }
});

// budgeting-g2ty9 (X6): im 340-px-Panel schnitt das Datumsfeld neben dem Betrag den Platzhalter ab.
test("[layout] PC: im Panel steht der Platzhalter des Datumsfelds beim Anlegen ganz im Feld", async () => {
  const { context, page } = await openPage(1440, 900, null);
  try {
    await row(page).click();
    const panel = page.locator('[data-testid="budget-inspector"]');
    await panel.getByRole("tab", { name: "Target" }).click();
    const input = panel.locator('[data-testid="target-date"] input[placeholder]');
    await input.waitFor();
    const measured = await input.evaluate((el) => {
      const style = getComputedStyle(el);
      const ctx = document.createElement("canvas").getContext("2d");
      ctx.font = style.font;
      return {
        text: ctx.measureText(el.placeholder).width,
        room: el.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight),
        scrollWidth: el.scrollWidth,
        clientWidth: el.clientWidth,
      };
    });
    assert.ok(measured.scrollWidth <= measured.clientWidth, `kein Überlauf: ${measured.scrollWidth} gegen ${measured.clientWidth}`);
    assert.ok(measured.text <= measured.room, `Platzhalter passt: ${measured.text} gegen ${measured.room}`);
  } finally {
    await context.close();
  }
});

test("[flow:target-anlegen] Handy: für eine Kategorie ohne Target 50 € monatlich anlegen", async () => {
  const { context, page, calls } = await openPage(390, 844, null);
  try {
    await openTargetTab(page);
    await sheet(page).locator('[data-testid="target-amount"] input').fill("50");
    await sheet(page).getByRole("button", { name: "Target anlegen" }).click();
    await until(page, () => calls.change.length === 1);
    const [[month, request]] = calls.change;
    assert.equal(month, "2026-11-01");
    assert.equal(request.Shown, null);
    assert.equal(String(request.Wanted.NewGoal.Amount), "50");
    assert.deepEqual(request.Wanted.NewRhythm, { SetRhythm: "Monthly" });
    await sheet(page).getByRole("button", { name: "Target speichern" }).waitFor();
  } finally {
    await context.close();
  }
});

test("[layout] Handy: Target anlegen und die Warnung vor dem Wechsel der Art liegen im 500-px-Sheet ohne Scrollen im Bild", async () => {
  const inView = async (page, locator, what) => {
    const box = await locator.boundingBox();
    const body = await sheet(page).locator(".sheet-body").boundingBox();
    assert.ok(box && body, `${what} und Sheet sind da`);
    assert.ok(box.y >= body.y, `${what} nicht über dem Bild: ${box.y} gegen ${body.y}`);
    assert.ok(box.y + box.height <= body.y + body.height, `${what} im Bild: ${box.y + box.height} gegen ${body.y + body.height}`);
  };
  const created = await openPage(390, 844, null);
  try {
    await openTargetTab(created.page);
    await inView(created.page, sheet(created.page).getByRole("button", { name: "Target anlegen" }), "Target anlegen");
  } finally {
    await created.context.close();
  }
  const changed = await openPage(390, 844);
  try {
    await openTargetTab(changed.page);
    await sheet(changed.page).getByRole("button", { name: "Jahr" }).click();
    await sheet(changed.page).getByRole("button", { name: "Target speichern" }).click();
    const dialog = sheet(changed.page).getByRole("alertdialog", { name: "Rhythmus wechseln?" });
    await dialog.waitFor();
    await inView(changed.page, dialog.getByRole("button", { name: "Wechseln" }), "Wechseln");
  } finally {
    await changed.context.close();
  }
});
