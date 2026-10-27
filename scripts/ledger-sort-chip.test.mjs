// scripts/ledger-sort-chip.test.mjs
//
// Wächter für `ledger-skpcx` (ADR 0051): am Handy (390x844) steht am Ende der Filterleiste
// ein Sortier-Chip, der ein BottomSheet "Sortieren" öffnet. Ein Tipp auf "Größte Ausgabe
// zuerst" sortiert die Liste flach nach Betrag aufsteigend (Datum in Zeile 2), das Sheet
// schließt und der Chip zeigt "Betrag ↑". In der Gesamtsicht stehen zehn Optionen. Am PC
// (1440px, Tabelle) gibt es den Chip nicht. Gemessen am echten, per Vite gestarteten
// Fable-Client gegen gemockte Ledger-Antworten (Muster wie `ledger-sort-headers.test.mjs`).
//
// Ausführen: node --test scripts/ledger-sort-chip.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
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
let desktop;
const sortsSeen = [];

function sortedEntries(entries, sort) {
  const dir = sort.Direction === "SortAsc" ? 1 : -1;
  const copy = [...entries];
  if (sort.Field === "SortByAmount") copy.sort((a, b) => (a.Amount.Amount - b.Amount.Amount) * dir);
  else copy.sort((a, b) => a.Date.localeCompare(b.Date) * dir || a.Id.localeCompare(b.Id) * dir);
  return copy;
}

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
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    const sort = body[0]?.Sort ?? { Field: "SortByDate", Direction: "SortDesc" };
    sortsSeen.push(sort);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ ...entriesFixture, Entries: sortedEntries(entriesFixture.Entries, sort) }),
    });
  });
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
  desktop = await openPage(1440, 900);
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const chip = (page) => page.locator(".ledger-filter-bar button").filter({ hasText: /^(Datum|Betrag|Payee|Kategorie|Konto) [↑↓]$/ });

test("Handy: Chip am Ende der Leiste, Sheet mit zehn Optionen, Betrag aufsteigend sortiert flach", async () => {
  const page = mobile.page;
  await page.waitForFunction(() => document.querySelectorAll(".ledger-filter-bar button").length > 0);
  await chip(page).waitFor();
  assert.equal(await chip(page).textContent(), "Datum ↓", "Standard zeigt Datum ↓");

  // Der Chip ist der letzte Eintrag der Leiste (ohne "Alle Filter löschen", solange kein Filter aktiv ist).
  const lastLabel = await page.evaluate(() => {
    const buttons = [...document.querySelectorAll(".ledger-filter-bar button")];
    return buttons.at(-1).textContent;
  });
  assert.equal(lastLabel, "Datum ↓", "Chip steht am Ende");

  await chip(page).click();
  const sheet = page.locator(".bottom-sheet.active");
  await sheet.waitFor();
  assert.equal(await sheet.locator("h3").textContent(), "Sortieren");
  assert.equal(await sheet.locator("button.category-item").count(), 10, "zehn Optionen in der Gesamtsicht");
  assert.equal(await sheet.locator("button[aria-pressed='true']").count(), 1, "genau eine gewählt");
  assert.match(await sheet.locator("button[aria-pressed='true']").textContent(), /Neueste zuerst/);

  await sheet.getByText("Größte Ausgabe zuerst").click();
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
  await pollUntil(() => sortsSeen.at(-1)?.Field === "SortByAmount");
  assert.deepEqual(sortsSeen.at(-1), { Field: "SortByAmount", Direction: "SortAsc" });
  await page.waitForFunction(() => [...document.querySelectorAll(".ledger-filter-bar button")].some((b) => b.textContent === "Betrag ↑"));

  const rows = await page.evaluate(() => {
    const lines = [...document.querySelectorAll("div.h-5.mt-0\\.5")];
    return {
      count: lines.length,
      dates: lines.map((l) => l.querySelector("span")?.textContent ?? ""),
      tables: document.querySelectorAll("table").length,
    };
  });
  assert.equal(rows.tables, 0, "Handy-Liste, keine Tabelle");
  assert.ok(rows.count > 0, "Zeilen stehen");
  assert.ok(rows.dates.every((d) => /\d/.test(d)), `Datum steht in Zeile 2: ${JSON.stringify(rows.dates.slice(0, 3))}`);
});

test("Handy: bei einem einzelnen Konto fehlen die Konto-Optionen", async () => {
  const page = mobile.page;
  const accounts = mobile.accountsFixture.Accounts;
  await page.locator(".ledger-account-strip").getByText(accounts[0].Name).filter({ visible: true }).first().click();
  await chip(page).waitFor();
  await chip(page).click();
  const sheet = page.locator(".bottom-sheet.active");
  await sheet.waitFor();
  assert.equal(await sheet.locator("button.category-item").count(), 8, "acht Optionen ohne Konto");
  await sheet.locator("button[aria-label='Schließen'], .sheet-close").first().click();
  await page.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);
});

test("PC: bei der Tabelle gibt es keinen Sortier-Chip", async () => {
  const page = desktop.page;
  await page.waitForSelector("table tbody tr");
  assert.equal(await chip(page).count(), 0, "Köpfe sortieren, der Chip fehlt");
});
