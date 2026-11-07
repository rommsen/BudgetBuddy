// scripts/ledger-sort-chip.test.mjs
//
// Wächter für `ledger-skpcx` (ADR 0051): am Handy (390x844) steht am Ende der Filterleiste
// ein Sortier-Chip, der ein BottomSheet "Sortieren" öffnet. Hier nur die Lage: Chip am Ende der
// Leiste, das Sheet im Bild, die Optionen mit 44 px Trefferfläche. Was der Chip zeigt, welche
// Optionen das Sheet listet, was ein Tipp sortiert und dass die Liste dann flach ist, prüfen
// `LedgerSortSheetTests` und `LedgerSortClientTests` unter .NET (ledger-r8h19); das Fehlen am PC
// `ledger-sort-headers` (infrastructure-t5m2b). Gemessen am echten, per Vite gestarteten
// Fable-Client gegen gemockte Ledger-Antworten (Muster wie `ledger-sort-headers.test.mjs`).
//
// Ausführen: node --test scripts/ledger-sort-chip.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntriesFixture,
  mockLedgerApi,
} from "./ledger-status-column.mjs";

let frontend;
let frontendPort;
let browser;
let mobile;

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(3);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId, 12);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  // Die Anfrage bleibt offen, sonst startet der Spiegel-Lauf in einer Schleife.
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.locator(".ledger-account-strip").getByText("Alle Konten").filter({ visible: true }).first().click();
  return { context, page, accountsFixture };
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  mobile = await openPage(390, 844);
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const chip = (page) => page.locator(".ledger-filter-bar button").filter({ hasText: /^(Datum|Betrag|Payee|Kategorie|Konto) [↑↓]$/ });

test("[layout] Handy: Chip am Ende der Leiste, das Sheet öffnet im Bild, Optionen mit 44 px Trefferfläche", async () => {
  const page = mobile.page;
  await page.waitForFunction(() => document.querySelectorAll(".ledger-filter-bar button").length > 0);
  await chip(page).waitFor();

  // Der Chip ist der letzte Eintrag der Leiste (ohne "Alle Filter löschen", solange kein Filter aktiv ist).
  const chipIsLast = await page.evaluate(() => {
    const buttons = [...document.querySelectorAll(".ledger-filter-bar button")];
    return /^(Datum|Betrag|Payee|Kategorie|Konto) [↑↓]$/.test(buttons.at(-1).textContent);
  });
  assert.ok(chipIsLast, "Chip steht am Ende");

  await chip(page).click();
  const sheet = page.locator(".bottom-sheet.active");
  await sheet.waitFor();
  // Das Sheet fährt ein; die Bedingung ist die Aussage (Zeitüberschreitung heißt: nicht ganz im Bild).
  await page.waitForFunction(() => {
    const r = document.querySelector(".bottom-sheet.active")?.getBoundingClientRect();
    return Boolean(r) && r.height > 0 && r.top >= 0 && r.bottom <= window.innerHeight + 1;
  });
  const heights = await sheet.locator("button.category-item").evaluateAll((els) => els.map((el) => el.getBoundingClientRect().height));
  assert.ok(heights.length > 0, "Optionen stehen");
  assert.ok(heights.every((h) => h >= 44), `jede Option mindestens 44 px hoch: ${JSON.stringify(heights)}`);
});
