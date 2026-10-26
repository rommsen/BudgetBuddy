// scripts/targets-apply.test.mjs
//
// Wächter für `budgeting-h2zfn`: der Dialog "Targets anwenden" auf der Budget-Seite. Gemessen
// am echten, per Vite gestarteten Fable-Client gegen gemockte BudgetApi-Antworten: Vorschau mit
// Summe und "Zu verteilen danach" (am Handy ohne Scrollen sichtbar, Übernehmen erreichbar auch
// bei langer Liste), negatives Zu verteilen ohne Warnung und Sperre, Übernehmen schickt die
// gezeigte Vorschau, "X von N übernommen", erneutes Senden, "Stand hat sich geändert".
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
        { Id: guid(1), Name: "Essen h2zfn", Assigned: eur(0), Activity: eur(0), Available: eur(0), Underfunded: eur(300), Snoozed: false },
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

const open = async (page) => {
  await page.getByRole("button", { name: "Targets anwenden" }).click();
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

test("Handy: Summe und Zu verteilen danach sichtbar, Übernehmen ohne Scrollen erreichbar, auch bei langer Liste", async () => {
  const preview = previewFixture(40);
  const { context, page, calls } = await openPage(390, 844, { preview, apply: () => ({ Error: "nicht erwartet" }), retry: () => ({ Error: "x" }) });
  try {
    await open(page);
    assert.deepEqual(calls.preview, [["current"]], "Vorschau für den laufenden Monat");
    assert.match(await text(page.locator('[data-testid="targets-total"]')), /400,00 €/);
    assert.match(await text(page.locator('[data-testid="targets-after"]')), /4\.600,00 €/);
    for (const id of ["targets-total", "targets-after"]) {
      const box = await page.locator(`[data-testid="${id}"]`).boundingBox();
      assert.ok(box && box.y >= 0 && box.y + box.height <= 844, `${id} liegt im sichtbaren Bereich`);
    }
    const apply = page.getByRole("button", { name: "Übernehmen" });
    const box = await apply.boundingBox();
    assert.ok(box && box.y >= 0 && box.y + box.height <= 844, "Übernehmen liegt ohne Scrollen im Bild");
    assert.equal(await page.locator('[data-testid="targets-line"]').count(), 40);
    assert.match(await text(page.locator('[data-testid="targets-skipped"]')), /Ausflug pausiert h2zfn.*pausiert/);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);
    assert.ok(overflow, "kein horizontales Scrollen");
    assert.equal(calls.apply.length, 0, "die Vorschau schreibt nicht");
  } finally {
    await context.close();
  }
});

test("Negatives Zu verteilen danach erscheint ohne Warnung, Übernehmen bleibt aktiv", async () => {
  const preview = previewFixture(3, 10);
  const { context, page } = await openPage(1440, 900, { preview, apply: () => ({ Error: "x" }), retry: () => ({ Error: "x" }) });
  try {
    await open(page);
    assert.match(await text(page.locator('[data-testid="targets-after"]')), /-20,00 €/);
    assert.equal(await page.getByRole("alert").count(), 0, "keine Warnung");
    assert.ok(await page.getByRole("button", { name: "Übernehmen" }).isEnabled(), "Übernehmen aktiv");
  } finally {
    await context.close();
  }
});

test("Abbrechen schließt den Dialog, ohne zu schreiben", async () => {
  const preview = previewFixture(2);
  const { context, page, calls } = await openPage(390, 844, { preview, apply: () => ({ Error: "x" }), retry: () => ({ Error: "x" }) });
  try {
    await open(page);
    await page.getByRole("button", { name: "Abbrechen" }).click();
    await page.locator('[data-testid="targets-summary"]').waitFor({ state: "detached" });
    assert.equal(calls.apply.length, 0);
    assert.equal(calls.month, 1, "kein Neuladen nach Abbrechen");
  } finally {
    await context.close();
  }
});

test("Übernehmen schickt die gezeigte Vorschau, zeigt X von N, erneutes Senden holt den Rest", async () => {
  const preview = previewFixture(3);
  const failed = reportFixture(preview, ["Written", { Failed: "Rate-Limit" }, { Failed: "Rate-Limit" }], eur(4980));
  const healed = reportFixture(preview, ["Written", "Written", "Written"], eur(4970));
  const { context, page, calls } = await openPage(390, 844, {
    preview,
    apply: () => ({ Ok: { Applied: failed } }),
    retry: () => ({ Ok: healed }),
  });
  try {
    await open(page);
    await page.getByRole("button", { name: "Übernehmen" }).click();
    await page.locator('[data-testid="targets-count"]').waitFor();
    assert.equal(calls.apply.length, 1);
    assert.equal(calls.apply[0][0], "current");
    assert.deepEqual(calls.apply[0][1].Lines.map((l) => l.CategoryId), preview.Lines.map((l) => l.CategoryId), "die gezeigte Vorschau geht mit");
    assert.match(await text(page.locator('[data-testid="targets-count"]')), /1 von 3 übernommen/);
    assert.match(await text(page.locator('[data-testid="targets-actual"]')), /4\.980,00 €/);
    const lines = await page.locator('[data-testid="targets-result-line"]').allInnerTexts();
    assert.match(lines[0].replace(/\s+/g, " "), /Rate-Limit/, "Fehlgeschlagene stehen oben, mit Grund");
    assert.match(await text(page.locator('[data-testid="targets-differs"]')), /weicht von der Vorschau ab/i, "Unterschied zur Vorschau (4.970,00 €) angezeigt");

    await page.getByRole("button", { name: "Fehlgeschlagene erneut senden" }).click();
    await page.getByText("3 von 3 übernommen").waitFor();
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
    assert.equal(await page.getByRole("button", { name: "Fehlgeschlagene erneut senden" }).count(), 0, "nichts mehr zu senden");
    assert.equal(await page.locator('[data-testid="targets-differs"]').count(), 0, "kein Unterschied mehr");

    await page.getByRole("button", { name: "Schließen" }).click();
    await page.locator('[data-testid="targets-count"]').waitFor({ state: "detached" });
    for (let i = 0; i < 50 && calls.month < 2; i++) await page.waitForTimeout(100);
    assert.equal(calls.month, 2, "nach der Anwendung wird der Monat neu gelesen");
  } finally {
    await context.close();
  }
});

test("Hat sich YNABs Stand geändert, steht die neue Vorschau mit Hinweis da und es wurde nichts geschrieben", async () => {
  const preview = previewFixture(2);
  const fresh = previewFixture(1);
  const { context, page, calls } = await openPage(390, 844, {
    preview,
    apply: () => ({ Ok: { PreviewChanged: fresh } }),
    retry: () => ({ Error: "x" }),
  });
  try {
    await open(page);
    await page.getByRole("button", { name: "Übernehmen" }).click();
    await page.locator('[data-testid="targets-changed"]').waitFor();
    assert.match(await text(page.locator('[data-testid="targets-changed"]')), /Stand hat sich geändert/);
    assert.equal(await page.locator('[data-testid="targets-line"]').count(), 1, "neue Vorschau");
    assert.ok(await page.getByRole("button", { name: "Übernehmen" }).isEnabled());
    assert.equal(calls.apply.length, 1);
  } finally {
    await context.close();
  }
});
