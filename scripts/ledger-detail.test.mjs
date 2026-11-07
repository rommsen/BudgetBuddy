// scripts/ledger-detail.test.mjs
//
// Wächter für `ledger-q1hw2` (ADR 0053): das Buchungs-Detail im Kassenbuch. Ein Klick auf die
// Zeile (auch auf Payee oder Kategorie) markiert sie und öffnet das Detail: Seitenpanel am PC,
// Sheet am Handy und in der Kompaktliste. Das Zellmenü aus `ledger-rfq5p` gibt es nicht mehr;
// die Filter-Knöpfe sitzen an den Feldern im Detail und lassen es offen (`LedgerFilterButtonTests`,
// das Sheet am Handy im Layout-Sweep, Zustand kassenbuch-detail). Gemessen wird am
// echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-filter-bar.test.mjs`, nie die echte Entwicklungsdatenbank).
//
// Ausführen: node --test scripts/ledger-detail.test.mjs
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
const queries = [];

const PAYEE = "REWE Mitte";

function entries(accountId) {
  return [
    buildEntryFixture(accountId, 1, {
      Payee: PAYEE,
      CategoryName: "Lebensmittel",
      CategoryId: CATEGORY_IDS.lebensmittel,
      LinkedToYnab: true,
      NeedsReview: true,
      Memo: "Wocheneinkauf",
    }),
    buildEntryFixture(accountId, 2, {
      Payee: "Deutsche Bahn",
      CategoryName: "Bahn",
      CategoryId: CATEGORY_IDS.bahn,
      LinkedToYnab: true,
    }),
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
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  // Offen lassen: sonst startet die Seite den Spiegel-Lauf in einer Schleife.
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  page = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');
const title = (p) => p.locator('[data-testid="detail-title"]');

test("[flow:buchung-oeffnen] PC (1440): Klick auf die Payee-Zelle öffnet das Panel, kein Menü; Liste bleibt, Memo fehlt, Zeile markiert", async () => {
  await row(page, PAYEE).waitFor();
  await row(page, PAYEE).locator("td").nth(2).click();
  await panel(page).waitFor();
  assert.equal(await page.locator('[role="menuitem"]').count(), 0, "kein Zellmenü");
  assert.equal(await title(page).textContent(), PAYEE);
  assert.equal(await page.locator("table thead th", { hasText: "Memo" }).count(), 0, "die Memo-Spalte fehlt");
  assert.equal(await row(page, PAYEE).getAttribute("data-keyboard-focus"), "true", "die Zeile ist markiert");
  const geometry = await page.evaluate(() => {
    const table = document.querySelector("table").getBoundingClientRect();
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    return { tableRight: table.right, panelLeft: aside.left, rows: document.querySelectorAll("table tbody tr").length };
  });
  assert.ok(geometry.rows >= 4, "die Liste bleibt sichtbar");
  assert.ok(geometry.tableRight <= geometry.panelLeft + 1, `Tabelle (${geometry.tableRight}) überlappt das Panel (${geometry.panelLeft}) nicht`);
  assert.equal(await page.locator('table tbody tr button[aria-haspopup="menu"]').count(), 0, "keine Auslöser in den Zellen");
});

