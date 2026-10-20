// scripts/ledger-cell-menu.test.mjs
//
// Wächter für `ledger-rfq5p`: Klick auf die Payee- oder Kategorie-Zelle einer Zeile öffnet
// ein kleines Menü "Nach X filtern", das den bestehenden Such-Filter setzt (PC), und das
// Handy-Sheet der Zeile trägt dieselben Aktionen. Gemessen wird am echten, per Vite
// gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-filter-bar.test.mjs`, nie die echte Entwicklungsdatenbank).
//
// Ausführen: node --test scripts/ledger-cell-menu.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  CATEGORY_IDS,
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;
let mobilePage;
const queries = [];

const PAYEE = "REWE Mitte";
const NO_PAYEE_ENTRY_N = 2;

function entries(accountId) {
  return [
    buildEntryFixture(accountId, 1, {
      Payee: PAYEE,
      CategoryName: "Lebensmittel",
      CategoryId: CATEGORY_IDS.lebensmittel,
    }),
    // Nichts zu filtern: kein Payee, keine Kategorie.
    buildEntryFixture(accountId, NO_PAYEE_ENTRY_N, { Payee: null, CategoryName: null, CategoryId: null }),
    buildEntryFixture(accountId, 3, { Payee: "Deutsche Bahn", CategoryName: "Bahn", CategoryId: CATEGORY_IDS.bahn }),
  ];
}

async function open(context) {
  const target = await context.newPage();
  target.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await target.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(target, {
    accountsFixture,
    balancesFixture,
    resolveEntries: (query) => {
      queries.push(query);
      return entries(accountsFixture.DefaultAccountId);
    },
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  page = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
  mobilePage = await open(
    await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true })
  );
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const payeeTrigger = (p, text) => row(p, text).locator('button[aria-haspopup="menu"]', { hasText: text });
const menu = (p) => p.locator('[role="menuitem"]');

test("PC: Klick auf die Payee-Zelle öffnet das Menü und nicht die Historie; es wird nicht abgeschnitten", async () => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await payeeTrigger(page, PAYEE).waitFor();
  await payeeTrigger(page, PAYEE).click();
  const item = menu(page).filter({ hasText: `Nach ${PAYEE} filtern` });
  await item.waitFor();
  assert.equal(await page.getByText("· Historie").count(), 0, "die Historie ist nicht aufgegangen");
  assert.equal(await payeeTrigger(page, PAYEE).getAttribute("aria-expanded"), "true");
  // Der Eintrag liegt sichtbar obenauf: an seiner Mitte trifft ein Hit-Test genau ihn,
  // also schneidet weder die gekürzte Zelle noch die Tabelle das Menü ab.
  const hit = await item.evaluate((el) => {
    const r = el.getBoundingClientRect();
    const top = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
    return { onTop: !!top && el.contains(top), width: r.width, height: r.height };
  });
  assert.ok(hit.onTop, "der Menüeintrag ist nicht verdeckt oder abgeschnitten");
  assert.ok(hit.width > 100 && hit.height > 20, `Eintrag hat Größe: ${hit.width}x${hit.height}`);
  assert.equal(await item.evaluate((el) => document.activeElement === el), true, "der Fokus liegt beim Öffnen im Menü");
});

test("PC: Esc schließt das Menü ohne Filteränderung", async () => {
  const before = queries.length;
  await page.keyboard.press("Escape");
  await menu(page).first().waitFor({ state: "detached" });
  assert.equal(queries.length, before, "kein Neuladen");
  assert.equal(await page.getByText("· Historie").count(), 0);
});

test("PC: Klick daneben schließt das Menü ohne Filteränderung", async () => {
  const before = queries.length;
  await payeeTrigger(page, PAYEE).click();
  await menu(page).first().waitFor();
  await page.mouse.click(5, 5);
  await menu(page).first().waitFor({ state: "detached" });
  assert.equal(queries.length, before, "kein Neuladen");
});

test("PC: Eine Zelle ohne Eintrag öffnet die Historie wie bisher", async () => {
  const noPayeeRow = page.locator("table tbody tr", { hasText: "ohne Payee" }).first();
  assert.equal(await noPayeeRow.locator('button[aria-haspopup="menu"]').count(), 0, "ohne Payee ist die Zelle kein Auslöser");
  await noPayeeRow.locator("td").nth(3).click();
  await page.getByText("· Historie").first().waitFor();
  // Das Sheet schließt über seinen Hintergrund (Esc gehört dem Sheet nur bei Fokus darin).
  await page.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  await page.getByText("· Historie").first().waitFor({ state: "detached" });
});

test("PC: Der Payee-Eintrag filtert, die Anfrage trägt den Payee", async () => {
  await payeeTrigger(page, PAYEE).click();
  const before = queries.length;
  await menu(page).filter({ hasText: `Nach ${PAYEE} filtern` }).click();
  await page.waitForFunction((n) => true, before);
  await menu(page).first().waitFor({ state: "detached" });
  const last = queries[queries.length - 1];
  assert.ok(queries.length > before, "ein Neuladen lief");
  assert.equal(last.Payee, PAYEE);
  assert.equal(last.Page, 0);
  assert.equal(await page.getByText("· Historie").count(), 0, "die Historie ist nicht aufgegangen");
});

test("PC: Der Kategorie-Eintrag setzt genau diese Kategorie und schaltet Ohne Kategorie aus", async () => {
  const trigger = row(page, PAYEE).locator('button[aria-haspopup="menu"]', { hasText: "Lebensmittel" });
  await trigger.click();
  const before = queries.length;
  await menu(page).filter({ hasText: "Lebensmittel filtern" }).click();
  await menu(page).first().waitFor({ state: "detached" });
  await page.waitForFunction(() => true);
  assert.ok(queries.length > before, "ein Neuladen lief");
  const last = queries[queries.length - 1];
  assert.deepEqual(last.CategoryIds, [CATEGORY_IDS.lebensmittel]);
  assert.equal(last.WithoutCategory, false);
  assert.equal(last.Payee, PAYEE, "der Payee-Filter bleibt stehen (Und-Verknüpfung)");
});

test("Handy (390x844): Tipp auf die Zeile öffnet das Sheet mit den Aktionen oberhalb der Historie", async () => {
  await mobilePage.getByText("Deutsche Bahn").first().tap();
  const actions = mobilePage.locator('[data-testid="row-filter-actions"]');
  await actions.waitFor();
  const labels = await actions.locator('[role="menuitem"]').allTextContents();
  assert.deepEqual(labels, ["Nach Deutsche Bahn filtern", "Nach Alltag: Bahn filtern"]);
  const above = await mobilePage.evaluate(() => {
    const a = document.querySelector('[data-testid="row-filter-actions"]');
    const next = a.nextElementSibling;
    return !!next && a.getBoundingClientRect().bottom <= next.getBoundingClientRect().top + 1;
  });
  assert.ok(above, "die Aktionen stehen vor der Historie");
  const before = queries.length;
  await actions.locator('[role="menuitem"]', { hasText: "Nach Deutsche Bahn filtern" }).tap();
  await actions.waitFor({ state: "detached" });
  assert.ok(queries.length > before, "ein Neuladen lief");
  assert.equal(queries[queries.length - 1].Payee, "Deutsche Bahn");
});

test("Handy: Eine Zeile ohne Payee und Kategorie hat keinen Aktionsblock", async () => {
  await mobilePage.getByText("ohne Payee").first().tap();
  await mobilePage.getByText("· Historie").first().waitFor();
  assert.equal(await mobilePage.locator('[data-testid="row-filter-actions"]').count(), 0);
});

test("/styleguide: die Karte mit menuItem im Popover ist da", async () => {
  const styleguide = await (await browser.newContext({ viewport: { width: 1440, height: 900 } })).newPage();
  await styleguide.goto(`http://127.0.0.1:${frontendPort}/#/styleguide`, { waitUntil: "load" });
  const card = styleguide.locator('[data-ds-card="components-filterpopover-menuitem"]');
  await card.waitFor();
  assert.ok((await card.locator('[role="menuitem"]').count()) >= 2);
});
