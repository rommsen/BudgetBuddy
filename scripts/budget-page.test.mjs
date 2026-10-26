// scripts/budget-page.test.mjs
//
// Wächter für `budgeting-k7wmd`: die Budget-Seite (#/budget) zeigt Monat, Umschalter dieser/
// nächster Monat, Zu verteilen und die Kategorien nach Gruppe mit Zugewiesen, Aktivität und
// Verfügbar. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte BudgetApi-
// Antworten: am Handy (390 px) Zeilen ohne horizontales Scrollen, am PC (1440 px) eine
// Tabelle, negatives Zu verteilen ohne Warnhinweis, Fehler mit Wiederholen, Erreichbarkeit
// über die Navigation.
//
// Ausführen: node --test scripts/budget-page.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";

let frontend;
let frontendPort;
let browser;

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const category = (n, name, assigned, activity, available) => ({
  Id: `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`,
  Name: name,
  Assigned: eur(assigned),
  Activity: eur(activity),
  Available: eur(available),
  Underfunded: null,
  Snoozed: false,
});

const monthFixture = (month, toBeBudgeted) => ({
  Month: month,
  ToBeBudgeted: eur(toBeBudgeted),
  Groups: [
    {
      Name: "Alltag k7wmd",
      Categories: [
        category(1, "Essen k7wmd", 1600, -921.1, 1120.09),
        category(2, "Eine Kategorie mit einem sehr langen Namen, der am Handy abgeschnitten werden muss k7wmd", 100, -10, -12.5),
      ],
    },
    { Name: "Rücklagen k7wmd", Categories: [category(3, "Urlaub k7wmd", 550, 0, 2921.6)] },
  ],
});

async function openPage(width, height, { fail = false, requestedKeys = [] } = {}) {
  const context = await browser.newContext({ viewport: { width, height } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let failNext = fail;
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    requestedKeys.push(key);
    if (failNext) {
      failNext = false;
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "YNAB rate limit exceeded. Retry after 60 seconds" }) });
      return;
    }
    const month = key === "next" ? monthFixture("2026-11-01", -12.34) : monthFixture("2026-10-01", -12.34);
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: month }) });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, requestedKeys };
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("Handy: Zu verteilen negativ ohne Warnhinweis, Gruppen mit Zeilen, kein horizontales Scrollen", async () => {
  const { context, page, requestedKeys } = await openPage(390, 844);
  try {
    assert.deepEqual(requestedKeys, ["current"], "der laufende Monat ist die Vorgabe");
    const box = page.locator('[data-testid="budget-to-be-budgeted"]');
    const text = (await box.innerText()).replace(/\s+/g, " ");
    assert.match(text, /Zu verteilen/i);
    assert.match(text, /-12,34 €/, "negativ, deutsch formatiert");
    assert.equal(await page.getByRole("alert").count(), 0, "kein Warnhinweis bei negativem Zu verteilen");
    assert.equal(await page.getByRole("heading", { name: "Budget" }).filter({ visible: true }).count(), 1);
    assert.ok(await page.getByText("Oktober 2026").isVisible(), "Monat im Kopf");

    const groups = page.locator('[data-testid="budget-group"]').filter({ visible: true });
    assert.equal(await groups.count(), 2);
    const rows = page.locator('[data-testid="budget-category-row"]').filter({ visible: true });
    assert.equal(await rows.count(), 3);
    const firstRow = (await rows.first().innerText()).replace(/\s+/g, " ");
    assert.match(firstRow, /Essen k7wmd/);
    assert.match(firstRow, /1\.120,09 €/, "Verfügbar");
    assert.match(firstRow, /Zugewiesen 1\.600,00 €/);
    assert.match(firstRow, /Aktivität -921,10 €/);

    const overflow = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
    }));
    assert.ok(overflow.scroll <= overflow.client, `kein horizontales Scrollen (scrollWidth ${overflow.scroll} > clientWidth ${overflow.client})`);
    assert.equal(
      await page.locator("table").filter({ visible: true }).count(),
      0,
      "am Handy keine Tabelle"
    );
  } finally {
    await context.close();
  }
});

test("PC: Tabelle mit Spaltenköpfen Zugewiesen, Aktivität, Verfügbar", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    const table = page.locator("table").filter({ visible: true });
    assert.equal(await table.count(), 1);
    const head = (await table.locator("thead").innerText()).replace(/\s+/g, " ").toLowerCase();
    assert.equal(await table.locator("thead tr").count(), 1, "genau eine Kopfzeile, keine verschachtelte");
    assert.equal(await table.locator("thead tr th").count(), 4, "vier Spaltenköpfe in der einen Zeile");
    for (const h of ["kategorie", "zugewiesen", "aktivität", "verfügbar"]) assert.ok(head.includes(h), `Spaltenkopf ${h}`);
    const rows = page.locator('[data-testid="budget-category-row"]').filter({ visible: true });
    assert.equal(await rows.count(), 3);
    const cells = await rows.first().locator("td").allInnerTexts();
    assert.deepEqual(cells.map((c) => c.replace(/\s+/g, " ").trim()), ["Essen k7wmd", "1.600,00 €", "-921,10 €", "1.120,09 €"]);
  } finally {
    await context.close();
  }
});

test("Umschalter: Nächster Monat lädt den nächsten Monat und zeigt ihn", async () => {
  const { context, page, requestedKeys } = await openPage(390, 844);
  try {
    await page.getByRole("button", { name: "Nächster Monat" }).click();
    await page.getByText("November 2026").waitFor();
    assert.deepEqual(requestedKeys, ["current", "next"]);
    await page.getByRole("button", { name: "Dieser Monat" }).click();
    await page.getByText("Oktober 2026").waitFor();
    assert.deepEqual(requestedKeys, ["current", "next", "current"]);
  } finally {
    await context.close();
  }
});

test("Fehler: lesbare Meldung mit Wiederholen, danach erscheint der Monat", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let calls = 0;
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    calls += 1;
    if (calls === 1) {
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "YNAB rate limit exceeded. Retry after 60 seconds" }) });
    } else {
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: monthFixture("2026-10-01", 354.5) }) });
    }
  });
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await page.getByRole("alert").waitFor();
    assert.match(await page.getByRole("alert").innerText(), /rate limit/);
    await page.getByRole("button", { name: "Try Again" }).click();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.match((await page.locator('[data-testid="budget-to-be-budgeted"]').innerText()).replace(/\s+/g, " "), /354,50 €/);
    assert.equal(calls, 2);
  } finally {
    await context.close();
  }
});

test("Navigation: die Budget-Seite ist über die Tab-Leiste am Handy erreichbar", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: monthFixture("2026-10-01", 10) }) })
  );
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/settings`, { waitUntil: "load" });
    const tab = page.locator("nav").getByText("Budget", { exact: true }).filter({ visible: true });
    await tab.waitFor();
    const bounds = await tab.boundingBox();
    assert.ok(bounds && bounds.x >= 0 && bounds.x + bounds.width <= 390, "Eintrag liegt im sichtbaren Bereich");
    await tab.click();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.match(page.url(), /#\/budget$/);
  } finally {
    await context.close();
  }
});
