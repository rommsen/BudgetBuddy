// ledger-mhe2s: Wächter für die Prüf-Schaltfläche der PC-Zeile, am echten gerenderten
// Element gemessen. Ohne Hover und Fokus unsichtbar (Breite bleibt), bei Zeilen-Hover und
// bei Tastaturfokus auf der Schaltfläche sichtbar, immer neutral grau, mit title.
// Dieselbe Fixtur und dasselbe Mocking wie `ledger-status-column.test.mjs`.
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntriesFixture,
  mockLedgerApi,
  SIX_CASE_PAYEES,
} from "./ledger-status-column.mjs";

let frontend, frontendPort, browser, page;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(5);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  // Der Spiegel-Lauf beim Seitenstart würde ohne Backend scheitern und die Liste ständig neu
  // aufbauen. Die Anfrage bleibt offen, damit die Zeilen stabil stehen.
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.locator("table tbody tr").first().waitFor();
  await page.waitForTimeout(1500);
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

function rowByPayee(text) {
  return page.locator("table tbody tr").filter({ has: page.locator("td", { hasText: new RegExp(`^${text}$`) }) }).first();
}

const measure = (payee) =>
  page.evaluate((payee) => {
    const tr = [...document.querySelectorAll("table tbody tr")].find((r) =>
      [...r.querySelectorAll("td")].some((td) => td.textContent === payee));
    const el = tr.querySelector("td:last-child button");
    const cs = getComputedStyle(el);
    const svg = el.querySelector("svg");
    const r = el.getBoundingClientRect();
    return { opacity: Number(cs.opacity), width: r.width, color: svg ? getComputedStyle(svg).color : cs.color, title: el.title };
  }, payee);

test("Prüf-Schaltfläche: unsichtbar mit Breite, bei Zeilen-Hover sichtbar, neutral, mit title", async () => {
  const row = rowByPayee(SIX_CASE_PAYEES.needsReview);
  const button = row.locator("td").last().locator("button");
  await page.mouse.move(2, 2);
  await page.waitForTimeout(300);
  const idle = await measure(SIX_CASE_PAYEES.needsReview);
  assert.equal(idle.opacity, 0, "ohne Hover unsichtbar");
  assert.ok(idle.width >= 27, `behält ihre Breite (war ${idle.width})`);
  await row.hover();
  await page.waitForTimeout(400);
  const hovered = await measure(SIX_CASE_PAYEES.needsReview);
  assert.equal(hovered.opacity, 1, "bei Zeilen-Hover sichtbar");
  assert.equal(hovered.title, "Als geprüft markieren");
  // neutral: Teal hätte einen großen Abstand zwischen Rot und Grün/Blau
  const [r, g, b] = hovered.color.match(/\d+(\.\d+)?/g).map(Number);
  assert.ok(Math.max(r, g, b) - Math.min(r, g, b) < 60, `neutrale Farbe erwartet, war ${hovered.color}`);
});

test("Prüf-Schaltfläche: Tastaturfokus macht sie sichtbar", async () => {
  await page.mouse.move(2, 2);
  const button = rowByPayee(SIX_CASE_PAYEES.needsReview).locator("td").last().locator("button");
  await button.focus();
  await page.waitForTimeout(400);
  assert.equal((await measure(SIX_CASE_PAYEES.needsReview)).opacity, 1, "bei Fokus sichtbar");
});
