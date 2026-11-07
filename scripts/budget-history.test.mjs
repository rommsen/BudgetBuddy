// scripts/budget-history.test.mjs
//
// Wächter für `budgeting-h7q4v`: die allgemeine Budget-Historie `#/budget/history`. Nur der Happy Path
// des Nutzerflusses gegen eine gemockte BudgetApi. Die Regeln (Durchläufe, Filter, Aufklappen,
// Rückkehr mit Monat) liegen in `src/Tests/BudgetGeneralHistoryTests.fs` und
// `src/Tests/BudgetGeneralHistoryClientTests.fs`.
//
// Ausführen: node --test scripts/budget-history.test.mjs
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

const monthFixture = (key) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Alltag h7q4v",
      Categories: [
        { Id: guid(1), Name: "Miete h7q4v", Assigned: eur(800), Activity: eur(0), Available: eur(800), Underfunded: null, Target: null, Pause: null, OpenWriteBack: null },
      ],
    },
  ],
});

/// Ein Eintrag der Historie (`HistoryEntry`, budgeting-r5h2n): die Bewegung als Union-Fall `Movement`, so wie
/// Fable.Remoting einen Fall mit Record kodiert.
const movement = (n, group, origin, name, oldAssigned, newAssigned, latest, counterpart = null) => ({ Movement: {
  MovementId: guid(100 + n),
  ApplicationId: guid(group),
  OccurredAt: "2026-11-03T09:30:00.0000000Z",
  Origin: { Decision: origin },
  Month: "2026-11-01",
  CategoryId: guid(200 + n),
  CategoryName: name,
  OldAssigned: eur(oldAssigned),
  NewAssigned: eur(newAssigned),
  Counterpart: counterpart,
  Latest: latest,
  ReplacedBy: null,
} });

const movements = [
  movement(1, 11, "MoneyAssigned", "Urlaub h7q4v", 0, 20, "Written"),
  movement(2, 12, "TargetsApplied", "Strom h7q4v", 0, 60, { Failed: "Rate-Limit" }),
  movement(3, 12, "TargetsApplied", "Miete h7q4v", 0, 800, "Written"),
];

/// Genug Durchläufe, dass die Seite scrollt.
/// Eine Verschiebung mit langer Quelle (budgeting-a3k7m): am Handy stand nur "Verschieben, Essen/Einkaufen → ...".
const longMove = [
  movement(4, 13, "MoneyMoved", "Essen/Einkaufen", 1500, 265.44, "Written", "Urlaub"),
  movement(5, 13, "MoneyMoved", "Urlaub", 0, 1234.56, "Written", "Essen/Einkaufen"),
];
const manyMovements = [
  ...longMove,
  ...movements,
  ...Array.from({ length: 30 }, (_, i) => movement(10 + i, 20 + i, "MoneyAssigned", `Kategorie c6vn4 ${i}`, 0, 10 + i, "Written")),
];

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

/// Öffnet `#/budget/history` direkt mit gemockter BudgetApi.
const openHistory = async (width, height, history) => {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: true });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetHistory", (route) => json(route, { Ok: history }));
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget/history`, { waitUntil: "load" });
  await page.locator('[data-testid="general-history-run"]').first().waitFor();
  return { context, page };
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

test("[flow:budget-historie] Handy: Historie öffnen, Targets anwenden filtern, den Durchlauf aufklappen und zurück zur Budget-Seite", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01", "2026-12-01"], CategoryOrder: [guid(1)], MonthStartHidden: [] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key) });
  });
  await page.route("**/api/BudgetApi/getBudgetHistory", (route) => json(route, { Ok: movements }));
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/budget");
    await page.locator('[data-testid="budget-history-button"]:visible button').click();
    await page.waitForURL(/#\/budget\/history$/);
    const runs = page.locator('[data-testid="general-history-run"]');
    await runs.first().waitFor();
    assert.equal(await runs.count(), 2, "zwei Durchläufe");

    await page.locator('[data-testid="general-history-filter"]').getByRole("button", { name: "Targets anwenden" }).click();
    await page.waitForFunction(() => document.querySelectorAll('[data-testid="general-history-run"]').length === 1);
    const run = runs.first();
    assert.equal(await run.locator('[data-testid="general-history-text"]').textContent(), "Targets anwenden, 2 Kategorien");
    assert.equal(await run.locator('[data-testid="general-history-amount"]').textContent(), "+860,00 €");
    assert.match(await run.textContent(), /1 von 2 fehlgeschlagen/);

    const toggle = run.locator('[data-testid="general-history-toggle"]');
    assert.equal(await toggle.getAttribute("aria-expanded"), "false", "zu");
    await toggle.click();
    assert.equal(await toggle.getAttribute("aria-expanded"), "true", "offen");
    const lines = run.locator('[data-testid="general-history-line"]');
    await lines.first().waitFor();
    assert.equal(await lines.count(), 2, "die Einzelzeilen");
    assert.match(await lines.nth(1).textContent(), /Strom h7q4v.*\+60,00 €.*Rate-Limit/);

    await page.getByRole("button", { name: "Zurück" }).click();
    await page.waitForURL(/#\/budget$/);
    await page.locator('[data-testid="budget-to-be-budgeted"]').first().waitFor();
  } finally {
    await context.close();
  }
});
