// scripts/kategorie-anpinnen.test.mjs
//
// budgeting-w9yt4: angepinnte Kategorien. Der eine Happy Path am Handy (412 px): im Budget das Kategorie-Detail öffnen
// und über den Pin-Knopf anpinnen, auf der Übersicht steht die Kategorie im Block „Angepinnt“, im Sheet „Angepinnt“
// wird sie gelöst, danach steht die Leer-Karte da. Welche Zeilen in welcher Folge erscheinen, die Rechnung von Anteil,
// ausgegeben und von, Ordnen, Suche, Markierung und der Rücksprung bei einem Speicherfehler prüfen die .NET-Tests
// (`OverviewPinsTests.fs`, `PinnedCategoriesTests.fs`); das Layout der Sweep (Zustände `uebersicht-angepinnt` und
// `uebersicht-angepinnt-sheet`).
//
// Ausführen: npm run test:scripts -- scripts/kategorie-anpinnen.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { stateNamed } from "./layout-states.mjs";

let frontend;
let port;
let browser;

before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

test("[flow:kategorie-anpinnen] Am Handy eine Kategorie im Detail anpinnen, auf der Übersicht sehen und im Sheet lösen", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
    await stateNamed("uebersicht").mock(page);
    // Die Pins wie der Server: gelesen wird, was zuletzt gesetzt wurde.
    let pins = [];
    await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: pins }));
    await page.route("**/api/BudgetApi/setPinnedCategories", (r) => {
      [pins] = JSON.parse(r.request().postData() ?? "[[]]");
      return json(r, { Ok: null });
    });

    await page.goto(`http://127.0.0.1:${port}/#/budget`, { waitUntil: "load" });
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    await page.locator('[data-testid="budget-category-row"]:visible').filter({ has: page.getByText("Kleidung", { exact: true }) }).tap();
    const sheet = page.locator(".bottom-sheet.fixed-height.active");
    const pin = sheet.locator('[data-testid="inspector-pin"]');
    await pin.waitFor();
    assert.equal(await pin.getAttribute("aria-label"), "Auf der Übersicht anpinnen");
    const pinned = page.waitForRequest("**/api/BudgetApi/setPinnedCategories");
    await pin.tap();
    // Am Handy steht die Rückmeldung in der Meldungsleiste (design-system-gkded).
    await page.getByTestId("feedback-bar").getByTestId("feedback-action").filter({ hasText: "Kleidung angepinnt" }).waitFor();
    assert.equal(await pin.getAttribute("aria-label"), "Von der Übersicht lösen", "der Knopf zeigt den neuen Stand");
    assert.equal(JSON.parse((await pinned).postData())[0].length, 1, "die eine Kategorie gespeichert");

    await sheet.getByRole("button", { name: "Schließen" }).first().tap();
    await page.getByRole("button", { name: "Übersicht", exact: true }).locator("visible=true").first().tap();
    const row = page.locator('[data-testid="overview-pinned"]');
    await row.filter({ hasText: "Kleidung" }).waitFor();
    assert.equal(await row.count(), 1, "genau die angepinnte Kategorie");

    await page.locator('[data-testid="overview-pins-edit"]').tap();
    const pinSheet = page.locator(".bottom-sheet.active");
    await pinSheet.getByText("Angepinnt (1)").waitFor();
    const released = page.waitForRequest("**/api/BudgetApi/setPinnedCategories");
    await pinSheet.getByRole("button", { name: "Kleidung lösen" }).tap();
    assert.deepEqual(JSON.parse((await released).postData())[0], [], "gelöst gespeichert");
    await pinSheet.getByRole("button", { name: "Fertig", exact: true }).tap();
    await page.locator('[data-testid="overview-pins-empty"]').waitFor();
  } finally {
    await context.close();
  }
});

test("[tastatur] Am PC steht der Fokus in der Suche des Sheets, Pfeile markieren, Enter schaltet um, Esc schließt", async () => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  try {
    await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
    await stateNamed("uebersicht").mock(page);
    let pins = [];
    await page.route("**/api/BudgetApi/getPinnedCategories", (r) => json(r, { Ok: pins }));
    await page.route("**/api/BudgetApi/setPinnedCategories", (r) => {
      [pins] = JSON.parse(r.request().postData() ?? "[[]]");
      return json(r, { Ok: null });
    });

    await page.goto(`http://127.0.0.1:${port}/#/`, { waitUntil: "load" });
    await page.locator('[data-testid="overview-pins-empty"]').click();
    const dialog = page.locator('[data-layer="dialog"]');
    const rows = dialog.locator('[data-testid="pin-sheet-row"]');
    await rows.first().waitFor();
    const search = dialog.getByRole("combobox", { name: "Kategorie suchen" });
    await page.waitForFunction(() => document.activeElement?.getAttribute("aria-label") === "Kategorie suchen");

    await search.press("ArrowDown");
    await rows.nth(1).and(page.locator('[data-marked="true"]')).waitFor();
    assert.equal(await search.getAttribute("aria-activedescendant"), await rows.nth(1).getAttribute("id"), "die Suche zeigt auf die Markierung");
    const save = page.waitForRequest("**/api/BudgetApi/setPinnedCategories");
    await search.press("Enter");
    await rows.nth(1).and(page.locator('[aria-checked="true"]')).waitFor();
    const [sent] = JSON.parse((await save).postData() ?? "[[]]");
    assert.equal(sent.length, 1, "die markierte Zeile ist angepinnt und gespeichert");
    assert.equal(await search.evaluate((el) => el === document.activeElement), true, "der Fokus bleibt in der Suche");

    await page.keyboard.press("Escape");
    await dialog.waitFor({ state: "detached" });
    await page.locator('[data-testid="overview-pinned"]').first().waitFor();
  } finally {
    await context.close();
  }
});
