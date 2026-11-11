// scripts/navigation.test.mjs
//
// design-system-k6kfw: die Navigation mit drei Orten (Styleguide §7.4). Der eine Happy Path über alle drei Formen:
// bei 412 px die Leiste unten, bei 900 px die Rail, bei 1280 px die Seitenleiste mit Konten. Welche Seite unter
// welchem Ort hängt, wohin „Zurück“ führt und dass ein Konto aus der Seitenleiste den Filter setzt, prüfen die
// .NET-Tests (`RoutingTests.fs`, `OverviewTests.fs`); Layout und Trefferflächen der Sweep (Zustand `uebersicht`).
//
// Ausführen: npm run test:scripts -- scripts/navigation.test.mjs

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

/// Eine Seite mit den Antworten von Übersicht und Kassenbuch aus dem Layout-Sweep (rein synthetisch).
async function openAt(width, height, mobile) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: mobile, isMobile: mobile });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
  await stateNamed("uebersicht").mock(page);
  await stateNamed("kassenbuch").mock(page);
  await page.goto(`http://127.0.0.1:${port}/#/`, { waitUntil: "load" });
  await page.locator('[data-testid="overview-month"]').waitFor();
  return { context, page };
}

const places = (page) => page.locator('nav[aria-label="Orte"]').filter({ visible: true });
const current = async (nav) => nav.locator('[aria-current="page"]').innerText();

test("[flow:navigation] Leiste, Rail und Seitenleiste führen zu den Orten, ein Konto ins Kassenbuch, das Zahnrad in die Einstellungen", async () => {
  // Handy 412 px: Leiste unten mit vier Einträgen, Übersicht markiert.
  {
    const { context, page } = await openAt(412, 915, true);
    try {
      const nav = places(page);
      assert.equal(await nav.count(), 1, "genau eine Navigation sichtbar");
      assert.deepEqual((await nav.getByRole("button").allInnerTexts()).map((t) => t.trim()), ["Übersicht", "Kassenbuch", "Budget", "Quick Add"]);
      assert.equal((await current(nav)).trim(), "Übersicht");
      await nav.getByRole("button", { name: "Budget" }).click();
      await page.waitForURL(/#\/budget$/);
      assert.equal((await current(places(page))).trim(), "Budget");
      await nav.getByRole("button", { name: "Übersicht" }).click();
      await page.waitForURL(/#\/$/);
      await page.getByRole("button", { name: "Einstellungen" }).click();
      await page.waitForURL(/#\/settings$/);
      await page.getByRole("button", { name: "Zurück" }).click();
      await page.waitForURL(/#\/$/);
    } finally {
      await context.close();
    }
  }
  // Tablet 900 px: Rail links, Kassenbuch markiert nach dem Klick, unten das Zahnrad.
  {
    const { context, page } = await openAt(900, 1024, false);
    try {
      const nav = places(page);
      assert.equal(await nav.count(), 1, "nur die Rail");
      await nav.getByRole("button", { name: "Kassenbuch" }).click();
      await page.waitForURL(/#\/ledger$/);
      assert.equal((await current(places(page))).trim(), "Kassenbuch");
      await nav.getByRole("button", { name: "Einstellungen" }).click();
      await page.waitForURL(/#\/settings$/);
    } finally {
      await context.close();
    }
  }
  // PC 1280 px: Seitenleiste mit Konten; ein Konto öffnet das Kassenbuch und ist dort markiert.
  {
    const { context, page } = await openAt(1280, 900, false);
    try {
      const side = page.locator("aside").filter({ visible: true });
      const account = side.locator('[data-testid="sidebar-account"]').filter({ hasText: "Testkonto 2" });
      await account.click();
      await page.waitForURL(/#\/ledger$/);
      await page.locator('[data-testid="sidebar-account"][aria-current="true"]').filter({ hasText: "Testkonto 2" }).waitFor();
      assert.equal((await current(places(page))).trim(), "Kassenbuch");
      await side.getByRole("button", { name: "Einstellungen" }).click();
      await page.waitForURL(/#\/settings$/);
      await page.getByRole("button", { name: "Zurück" }).click();
      await page.waitForURL(/#\/ledger$/);
    } finally {
      await context.close();
    }
  }
});
