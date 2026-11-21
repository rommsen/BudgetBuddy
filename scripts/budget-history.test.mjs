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
  CopiedAt: null,
  Groups: [
    {
      Name: "Alltag h7q4v",
      Categories: [
        { Id: guid(1), Name: "Miete h7q4v", Assigned: eur(800), Activity: eur(0), Available: eur(800), Underfunded: null, Target: null, Pause: null, OpenWriteBack: null, ActivityInYnab: null },
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

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

// budgeting-yarty: die Seite nach §7.11, der Filter über das Feld „Zeigen“ statt der Chipleiste.
test("[flow:budget-historie] Handy: Historie öffnen, über Zeigen Targets anwenden wählen, den Durchlauf aufklappen und zurück zur Budget-Seite", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true });
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
    const rows = page.locator('[data-testid="general-history-rows"] > [data-testid="history-row"]');
    await rows.first().waitFor();
    assert.equal(await rows.count(), 2, "zwei Durchläufe");

    const field = page.locator('[data-testid="general-history-filter-field"]');
    assert.match(await field.innerText(), /Zeigen\s+Alle/);
    await field.click();
    const options = page.locator('[data-testid="general-history-filter-options"]');
    await options.getByRole("button", { name: "Targets anwenden" }).click();
    await options.waitFor({ state: "detached" });
    await page.waitForFunction(() => document.querySelectorAll('[data-testid="general-history-rows"] > [data-testid="history-row"]').length === 1);
    assert.match(await field.innerText(), /Zeigen\s+Targets anwenden/);
    const run = rows.first();
    const toggle = run.locator('[data-testid="history-toggle"]');
    assert.equal(await toggle.locator('[data-testid="history-text"]').textContent(), "Targets anwenden, 2 Kategorien");
    assert.equal(await toggle.locator('[data-testid="history-amount"]').textContent(), "+860,00 €");

    assert.equal(await toggle.getAttribute("aria-expanded"), "false", "zu");
    await toggle.click();
    assert.equal(await toggle.getAttribute("aria-expanded"), "true", "offen");
    const lines = run.locator('[data-testid="history-subrow"]');
    await lines.first().waitFor();
    assert.equal(await lines.count(), 2, "die Einzelzeilen");

    await page.getByRole("button", { name: "Zurück" }).click();
    await page.waitForURL(/#\/budget$/);
    await page.locator('[data-testid="budget-to-be-budgeted"]').first().waitFor();
  } finally {
    await context.close();
  }
});
