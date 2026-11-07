// scripts/target-check.test.mjs
//
// Wächter für `budgeting-q4v8n` und `budgeting-c4ve4` (Form nach der Skizze): die Seite Targets prüfen
// `#/budget/target-check`. Nur der Happy Path des Nutzerflusses und die Rundung im Fable-Kompilat, gegen eine
// gemockte BudgetApi. Überlauf und Trefferflächen prüft der Layout-Sweep (Zustände targets-pruefen*), die Regeln
// (Gruppen, Töne der Balken, übernehmen, Panel, Sperre, Rückkehr) liegen in `src/Tests/TargetCheckClientTests.fs`.
// Namen und Beträge sind erfunden.
//
// Ausführen: node --test scripts/target-check.test.mjs
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

const target = (goal, rhythm = { Months: 1 }) => ({
  Goal: eur(goal),
  Rhythm: rhythm,
  Kind: "Need",
  Date: null,
  NeedsWholeAmount: true,
  Day: null,
  CreatedIn: null,
});

/// Die 12 vollen Monate vor November 2026, je Monat dieselben Ausgaben.
const spending = (amount) =>
  Array.from({ length: 12 }, (_, i) => {
    const d = new Date(Date.UTC(2025, 10 + i, 1));
    return { Month: `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}-01`, Activity: eur(-amount) };
  });

const source = (n, name, goal, spent, rhythm, lastChange = null) => ({
  CategoryId: guid(n),
  Name: name,
  GroupName: "Haushalt q4v8n",
  Target: goal === null ? null : target(goal, rhythm),
  Spending: spending(spent),
  LastChange: lastChange,
  // budgeting-k7r2w: Tag der ersten Buchung im Kassenbuch, null ohne Buchung. Die Fixtures zählen alle 12 Monate.
  FirstBooking: null,
});

/// "Strom q4v8n": Target 100 €, Ausgaben 150 €, steht bei "Gibst du mehr aus?" mit "150 € übernehmen".
/// "Versicherung q4v8n": 600 € im Jahr, Ausgaben 105 € je Monat, Vorschlag "1.260 € jährlich übernehmen".
/// "Kantine q4v8n": 50 € je Woche, Ausgaben 100 € je Monat, Vorschlag 100 × 12 ÷ 52 = 23,08, auf volle Euro 23 €.
/// "Urlaub q4v8n": ohne Target, Ausgaben 1.234 € je Monat, Vorschlag "1.235 € monatlich anlegen".
/// "Rücklage q4v8n": Target 60 €, keine Ausgaben, steht bei "Gibst du nie aus?".
/// "Reparatur q4v8n": Target 100 €, Ausgaben 150 €, Target am 10.06.2026 gesetzt: November bis Juni zählen nicht.
const checkData = {
  Month: "2026-11-01",
  Today: "2026-11-06",
  Categories: [
    source(1, "Strom q4v8n", 100, 150),
    source(2, "Versicherung q4v8n", 600, 105, { Years: 1 }),
    source(3, "Internet q4v8n", 100, 100),
    source(4, "Kantine q4v8n", 50, 100, { Weeks: 1 }),
    source(5, "Urlaub q4v8n", null, 1234),
    source(7, "Rücklage q4v8n", 60, 0),
    source(8, "Reparatur q4v8n", 100, 150, undefined, { TargetSetOn: "2026-06-10" }),
  ],
};

const monthFixture = (key) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Haushalt q4v8n",
      Categories: checkData.Categories.map((c) => ({
        Id: c.CategoryId,
        Name: c.Name,
        Assigned: eur(0),
        Activity: eur(0),
        Available: eur(0),
        Underfunded: null,
        Target: c.Target,
        Pause: null,
        OpenWriteBack: null,
      })),
    },
  ],
});

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

/// Mockt die BudgetApi. `calls.change` sammelt die Aufträge an `changeCategoryTarget`.
async function mockedPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { change: [] };
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: checkData.Categories.map((c) => c.CategoryId), MonthStartHidden: [] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key) });
  });
  await page.route("**/api/BudgetApi/getTargetCheck", (route) => json(route, { Ok: checkData }));
  await page.route("**/api/BudgetApi/changeCategoryTarget", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    calls.change.push(body);
    const [, request] = body;
    json(route, { Ok: { TargetWritten: { ...request.Shown, Goal: request.Wanted.NewGoal } } });
  });
  return { context, page, calls };
}

const rowOf = (page, name) => page.locator('[data-testid="target-check-row"]').filter({ hasText: name });

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[flow:targets-pruefen] Handy: über das Feld Targets prüfen der Leiste Monatserster die Seite öffnen, bei Gibst du mehr aus übernehmen, die Zeile steht danach bei Passt, Zurück führt zur Budget-Seite", async () => {
  const { context, page, calls } = await mockedPage(375, 812);
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/budget");
    await page.locator('[data-testid="month-start-check"]').click();
    await page.waitForURL(/#\/budget\/target-check$/);
    const more = page.locator('[data-testid="target-check-group"]').filter({ hasText: "Gibst du mehr aus?" });
    await more.waitFor();
    await rowOf(page, "Strom q4v8n").getByRole("button", { name: "150 € übernehmen" }).click();
    await page.getByText("Strom q4v8n: Target jetzt 150 € monatlich.").waitFor();

    const fits = page.locator('[data-testid="target-check-group"]').filter({ hasText: "Passt" });
    await fits.locator('[data-testid="target-check-group-toggle"]').click();
    const moved = fits.locator('[data-testid="target-check-row"]').filter({ hasText: "Strom q4v8n" });
    await moved.waitFor();
    assert.match(await moved.locator('[data-testid="target-check-note"]').textContent(), /^neu gesetzt am 06\.11\.2026$/);
    assert.equal(calls.change.length, 1, "ein Aufruf");
    assert.equal(String(calls.change[0][1].Wanted.NewGoal.Amount), "150", "der Vorschlag");

    await page.getByRole("button", { name: "Zurück zum Budget" }).click();
    await page.waitForURL(/#\/budget$/);
    await page.locator('[data-testid="budget-to-be-budgeted"]').first().waitFor();
  } finally {
    await context.close();
  }
});

// Verhindert den Befund aus der Durchsicht am 2026-11-06: `Math.Round(x, MidpointRounding.AwayFromZero)` rundet im
// Fable-Kompilat auf eine Nachkommastelle (der Modus galt als Stellenzahl), der wöchentliche Vorschlag stand mit
// "23,10 €/Woche" statt auf vollen Euro (ADR 0082 Punkt 9). Unter .NET rechnet dieselbe Funktion richtig.
//
// Verhindert außerdem den Befund aus der Durchsicht von e86f8d2: `Math.Round(x, 0, MidpointRounding.AwayFromZero)`
// auf decimal rundet im Fable-Kompilat zur geraden Zahl (fable-library `Decimal.round` verwirft den Modus). Seit
// budgeting-c4ve4 stehen die Beträge der Zeile in ganzen Euro (`wholeEuroText`): ein Ø von 84,50 € muss als
// "Ø 85 € im Monat" stehen, nicht "84". "Tanken q4v8n" gibt es nur in diesem Test.
test("[fable] Ein wöchentlicher Vorschlag steht auf vollen Euro, ein Ø auf 50 Cent wird aufgerundet", async () => {
  const { context, page } = await mockedPage(1280, 900);
  const withHalf = { ...checkData, Categories: [...checkData.Categories, source(6, "Tanken q4v8n", null, 84.5)] };
  // Die später angelegte Route hat Vorrang vor der aus `mockedPage`.
  await page.route("**/api/BudgetApi/getTargetCheck", (route) => json(route, { Ok: withHalf }));
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget/target-check`, { waitUntil: "load" });
    await rowOf(page, "Kantine q4v8n").getByRole("button", { name: "23 € wöchentlich übernehmen" }).waitFor();
    const average = await rowOf(page, "Tanken q4v8n").locator('[data-testid="target-check-average"]').innerText();
    assert.match(average, /^Ø 85 € im Monat/, `84,50 € steht kaufmännisch gerundet als 85 (${average})`);
  } finally {
    await context.close();
  }
});
