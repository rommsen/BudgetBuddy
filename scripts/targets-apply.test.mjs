// scripts/targets-apply.test.mjs
//
// Wächter für `budgeting-h2zfn`: der Dialog "Targets anwenden" auf der Budget-Seite, seit budgeting-m3x7f
// geöffnet über das Feld Targets anwenden der Leiste Monatserster, mit abwählbaren Zeilen. Gemessen am echten,
// per Vite gestarteten Fable-Client gegen gemockte BudgetApi-Antworten: der Flow über die Leiste mit einer
// abgewählten Zeile bis zum Bericht. Summe und Anwenden ohne Scrollen prüft der Layout-Sweep (Zustand
// budget-targets-anwenden), "kein Pink auf der Budget-Seite" (ADR 0075) die Farbregel desselben Sweeps.
//
// Ausführen: node --test scripts/targets-apply.test.mjs
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

const monthFixture = () => ({
  Month: "2026-11-01",
  ToBeBudgeted: eur(100),
  Groups: [
    {
      Name: "Alltag h2zfn",
      Categories: [
        { Id: guid(1), Name: "Essen h2zfn", Assigned: eur(0), Activity: eur(0), Available: eur(0), Underfunded: eur(300), Target: { Goal: eur(300), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null }, Pause: null, OpenWriteBack: null },
      ],
    },
  ],
});

const line = (n, name, oldA, under) => ({
  CategoryId: guid(n),
  CategoryName: name,
  GroupName: "Alltag h2zfn",
  OldAssigned: eur(oldA),
  Underfunded: eur(under),
  NewAssigned: eur(oldA + under),
  Target: { Goal: eur(1600), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null },
});

const previewFixture = (count, toBeBudgeted = 5000) => {
  const lines = Array.from({ length: count }, (_, i) => line(i + 1, `Kategorie ${i + 1} h2zfn`, 0, 10));
  const total = count * 10;
  return {
    Month: "2026-11-01",
    Lines: lines,
    Skipped: [{ CategoryId: guid(900), CategoryName: "Ausflug pausiert h2zfn", Underfunded: eur(25.5) }],
    Total: eur(total),
    ToBeBudgeted: eur(toBeBudgeted),
    ToBeBudgetedAfter: eur(toBeBudgeted - total),
    // Die Vorschau ist kein Befehl; die Befehls-Id setzt der Client beim Übernehmen (ADR 0084 Punkt 8).
    CommandId: null,
  };
};

const reportFixture = (preview, outcomes, actual) => ({
  ApplicationId: "11111111-1111-1111-1111-111111111111",
  Month: "2026-11-01",
  Lines: preview.Lines.map((l, i) => ({
    CategoryId: l.CategoryId,
    CategoryName: l.CategoryName,
    OldAssigned: l.OldAssigned,
    NewAssigned: l.NewAssigned,
    Outcome: outcomes[i],
  })),
  ActualToBeBudgeted: actual,
});

async function openPage(width, height, handlers) {
  const context = await browser.newContext({ viewport: { width, height } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { month: 0, preview: [], apply: [], retry: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: handlers.order ?? [guid(1)], MonthStartHidden: [] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    calls.month += 1;
    json(route, { Ok: monthFixture() });
  });
  await page.route("**/api/BudgetApi/previewTargets", (route) => {
    calls.preview.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, { Ok: handlers.preview });
  });
  await page.route("**/api/BudgetApi/applyTargets", (route) => {
    calls.apply.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, handlers.apply(calls.apply.length));
  });
  await page.route("**/api/BudgetApi/retryFailedTargets", (route) => {
    calls.retry.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, handlers.retry(calls.retry.length));
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

/// Die Vorschau über das Feld Targets anwenden der Leiste Monatserster öffnen (budgeting-m3x7f).
const open = async (page) => {
  await page.locator('[data-testid="month-start-apply"]').click();
  await page.locator('[data-testid="targets-summary"]').waitFor();
};

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

test("[flow:targets-anwenden] Handy: über die Leiste die Vorschau öffnen, eine Zeile abwählen, anwenden, X von N, erneutes Senden holt den Rest", async () => {
  const preview = previewFixture(3);
  // Die zweite Zeile wird abgewählt: gesendet und berichtet werden nur die erste und dritte.
  const sent = { ...preview, Lines: [preview.Lines[0], preview.Lines[2]] };
  const failed = reportFixture(sent, ["Written", { Failed: "Rate-Limit" }], eur(4990));
  const healed = reportFixture(sent, ["Written", "Written"], eur(4980));
  const { context, page, calls } = await openPage(390, 844, {
    preview,
    apply: () => ({ Ok: { Applied: failed } }),
    retry: () => ({ Ok: healed }),
  });
  try {
    await open(page);
    await page.locator('[data-testid="targets-line"]').nth(1).locator("input").click();
    await page.getByRole("button", { name: "Anwenden", exact: true }).click();
    await page.locator('[data-testid="targets-count"]').waitFor();
    assert.equal(calls.apply.length, 1);
    assert.equal(calls.apply[0][0], "2026-11-01");
    assert.deepEqual(calls.apply[0][1].Lines.map((l) => l.CategoryId), sent.Lines.map((l) => l.CategoryId), "nur die gewählten Zeilen gehen mit");
    assert.match(await text(page.locator('[data-testid="targets-count"]')), /1 von 2 angewendet/);
    const lines = await page.locator('[data-testid="targets-result-line"]').allInnerTexts();
    assert.match(lines[0].replace(/\s+/g, " "), /Rate-Limit/, "Fehlgeschlagene stehen oben, mit Grund");

    await page.getByRole("button", { name: "Fehlgeschlagene erneut senden" }).click();
    await page.getByText("2 von 2 angewendet").waitFor();
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
    assert.equal(await page.locator('[data-testid="targets-differs"]').count(), 0, "kein Unterschied zur gesendeten Vorschau");

    const reread = page.waitForResponse("**/api/BudgetApi/getBudgetMonth");
    await page.getByRole("button", { name: "Schließen" }).click();
    await page.locator('[data-testid="targets-count"]').waitFor({ state: "detached" });
    await reread;
    assert.equal(calls.month, 2, "nach der Anwendung wird der Monat neu gelesen");
  } finally {
    await context.close();
  }
});
