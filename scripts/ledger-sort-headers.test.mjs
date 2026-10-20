// scripts/ledger-sort-headers.test.mjs
//
// Wächter für `ledger-bn913` (ADR 0051): ein Klick auf einen sortierbaren Spaltenkopf der
// PC-Tabelle lädt die Liste mit der passenden Sortierung neu. Ohne Datumssortierung fallen
// die Tagesköpfe weg, der Kopf trägt `aria-sort`, Enter löst aus. Gemessen am echten, per
// Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-table-columns.test.mjs`). Der Mock sortiert die Fixture nach der gesendeten
// Sortierung, damit die Reihenfolge in der Tabelle den Weg über die Anfrage prüft.
//
// Ausführen: node --test scripts/ledger-sort-headers.test.mjs
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
let page;
const sortsSeen = [];

function sortedEntries(entries, sort) {
  const dir = sort.Direction === "SortAsc" ? 1 : -1;
  const byDate = (a, b) => a.Date.localeCompare(b.Date) * dir || a.Id.localeCompare(b.Id) * dir;
  const copy = [...entries];
  if (sort.Field === "SortByAmount") copy.sort((a, b) => (a.Amount.Amount - b.Amount.Amount) * dir);
  else copy.sort(byDate);
  return copy;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  page = await context.newPage();
  page.setDefaultTimeout(30000);

  const accountsFixture = buildAccountsFixture(3);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId, 12);

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  // Der Spiegel-Lauf beim Seitenstart würde ohne Backend scheitern und die Liste ständig neu
  // aufbauen. Die Anfrage bleibt offen, damit die Zeilen stabil stehen.
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  // Später registriert, gewinnt: antwortet nach der gesendeten Sortierung.
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
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const head = (name) => page.locator("table thead th").filter({ hasText: new RegExp(`^${name}$`, "i") });

/// Wartet, bis der Client die Anfrage mit dieser Sortierung gesendet hat und die Zeilen
/// der Antwort stehen (während des Ladens zeigt die Seite keine Tabelle).
async function waitForSort(field, direction) {
  const deadline = Date.now() + 20000;
  while (Date.now() < deadline) {
    const last = sortsSeen.at(-1);
    if (last && last.Field === field && last.Direction === direction) {
      await page.waitForSelector("table tbody tr");
      return;
    }
    await page.waitForTimeout(50);
  }
  assert.fail(`keine Anfrage mit ${field} ${direction}, zuletzt ${JSON.stringify(sortsSeen.at(-1))}`);
}

/// Beträge der Datenzeilen und Zahl der Tageskopf-Zeilen, atomar gemessen.
async function snapshot() {
  return page.evaluate(() => {
    const rows = [...document.querySelectorAll("table tbody tr")];
    const groupRows = rows.filter((tr) => tr.querySelector("td[colspan]"));
    const dataRows = rows.filter((tr) => !tr.querySelector("td[colspan]"));
    const amounts = dataRows.map((tr) => {
      const cells = [...tr.querySelectorAll("td")];
      const text = cells[cells.length - 2].textContent.replace(/[^\d,\-−+]/g, "").replace("−", "-").replace(",", ".");
      return parseFloat(text);
    });
    return { groups: groupRows.length, amounts };
  });
}

test("Klick auf Betrag sortiert aufsteigend, ohne Tagesköpfe; zweiter Klick kehrt um; Datum bringt sie zurück", async () => {
  await page.waitForSelector("table tbody tr");
  await page.waitForFunction(() => document.querySelectorAll("table tbody tr td[colspan]").length > 0);
  const before = await snapshot();
  assert.ok(before.groups > 0, "Standard: Tagesköpfe stehen");

  await head("Betrag").locator("button").click();
  await waitForSort("SortByAmount", "SortAsc");
  assert.equal(await head("Betrag").getAttribute("aria-sort"), "ascending");
  assert.equal(await head("Datum").getAttribute("aria-sort"), "none");
  const asc = await snapshot();
  assert.equal(asc.groups, 0, "kein Tageskopf bei Betragssortierung");
  assert.deepEqual(asc.amounts, [...asc.amounts].sort((a, b) => a - b), "Beträge laufen vom kleinsten Wert aufwärts");

  await head("Betrag").locator("button").click();
  await waitForSort("SortByAmount", "SortDesc");
  await page.waitForFunction(() => document.querySelector("table thead th[aria-sort='descending']") !== null);
  const desc = await snapshot();
  assert.deepEqual(desc.amounts, [...asc.amounts].reverse(), "Reihenfolge umgekehrt");

  await head("Datum").locator("button").click();
  await waitForSort("SortByDate", "SortDesc");
  await page.waitForFunction(() => document.querySelectorAll("table tbody tr td[colspan]").length > 0);
  assert.equal(await head("Datum").getAttribute("aria-sort"), "descending");
});

test("Tastatur: Fokus auf den Kopf, Enter löst die Sortierung aus; Memo und Status sind keine Schaltflächen", async () => {
  await head("Payee").locator("button").focus();
  const focused = await page.evaluate(() => document.activeElement?.closest("th")?.textContent?.trim());
  assert.match(focused, /payee/i);
  const countBefore = sortsSeen.length;
  await page.keyboard.press("Enter");
  assert.ok(countBefore > 0);
  await waitForSort("SortByPayee", "SortAsc");
  await page.waitForFunction(() => document.querySelector("table thead th[aria-sort='ascending']") !== null);
  assert.equal(await head("Status").locator("button").count(), 0);
  assert.equal(await head("Status").getAttribute("aria-sort"), null);
});
