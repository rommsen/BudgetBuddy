// scripts/ledger-table-columns.test.mjs
//
// Wächter für `ledger-qh9qx`: die PC-Tabelle des Kassenbuchs blendet in schmalen
// Fenstern Spalten stufenweise aus (Memo, dann Konto, dann kompakte Liste), statt
// Payee, Kategorie und Memo auf null zu drücken. Gemessen wird am echten, per Vite
// gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie
// `ledger-status-column.test.mjs`, nie die echte Entwicklungsdatenbank).
//
// Ausführen: node --test scripts/ledger-table-columns.test.mjs
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

const WIDTHS = [800, 1024, 1280, 1440];
// Mindestbreiten aus `Components.Ledger.View` (payeeMinWidth usw.), nach Kopftext.
const MIN_WIDTH = { Konto: 72, Payee: 140, Kategorie: 120, Memo: 120, Datum: 96, Betrag: 132, Status: 128 };

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  page = await context.newPage();
  page.setDefaultTimeout(30000);

  const accountsFixture = buildAccountsFixture(5);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId, 10);
  // Echte Werte aus der Aufgabe: lange Namen und ein Memo.
  entriesFixture.Entries[0].Payee = "Transfer : Tagesgeld ING";
  entriesFixture.Entries[0].CategoryName = "Alltag: Essen/Einkaufen";
  entriesFixture.Entries[0].Memo = "Ein längeres Memo für die Breitenprüfung";

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

/// Misst atomar in einem `evaluate`: Kopftexte, `scrollWidth <= clientWidth` je `th`
/// und die Breite je `th`. Wartet vorher, bis die Messung zweimal hintereinander gleich ist.
async function measure(width) {
  await page.setViewportSize({ width, height: 900 });
  await page.waitForFunction(
    () => {
      const ths = [...document.querySelectorAll("table thead th")];
      const sig = ths.map((th) => Math.round(th.getBoundingClientRect().width)).join(",") + "|" + document.querySelectorAll("table").length;
      const stable = window.__colsSig === sig;
      window.__colsSig = sig;
      return stable;
    },
    undefined,
    { polling: 100, timeout: 30000 }
  );
  return page.evaluate(() => {
    const ths = [...document.querySelectorAll("table thead th")];
    const dataRow = [...document.querySelectorAll("table tbody tr")].find((tr) => tr.querySelectorAll("td").length >= 5);
    const hasAccountColumn = ths.some((th) => th.textContent.trim().toUpperCase() === "KONTO");
    const firstRowPayee = dataRow ? dataRow.querySelectorAll("td")[hasAccountColumn ? 3 : 2] : null;
    return {
      tableCount: document.querySelectorAll("table").length,
      headers: ths.map((th) => ({
        text: th.textContent.trim(),
        width: th.getBoundingClientRect().width,
        scrollWidth: th.scrollWidth,
        clientWidth: th.clientWidth,
      })),
      payeeCellText: firstRowPayee ? firstRowPayee.textContent : null,
    };
  });
}

function assertNoOverlap(label, result) {
  for (const h of result.headers) {
    assert.ok(h.scrollWidth <= h.clientWidth, `${label}: Kopf ${h.text}: scrollWidth ${h.scrollWidth} > clientWidth ${h.clientWidth}`);
    const key = Object.keys(MIN_WIDTH).find((k) => k.toUpperCase() === h.text.toUpperCase());
    if (key) assert.ok(h.width >= MIN_WIDTH[key] - 1, `${label}: ${h.text} ist ${h.width}px, Minimum ${MIN_WIDTH[key]}px`);
  }
}

for (const mode of ["Gesamtsicht", "Ein Konto"]) {
  test(`${mode}: kein Spaltenkopf überlappt und keine Spalte unter ihrer Mindestbreite (800, 1024, 1280, 1440)`, async () => {
    await page.setViewportSize({ width: 1440, height: 900 });
    const card = page.locator(".ledger-account-strip").getByText(mode === "Gesamtsicht" ? "Alle Konten" : "Testkonto 1").filter({ visible: true }).first();
    await card.click();
    const table = [];
    for (const width of WIDTHS) {
      const r = await measure(width);
      table.push({ mode, width, tables: r.tableCount, heads: r.headers.map((h) => h.text).join("/"), widths: r.headers.map((h) => Math.round(h.width)).join("/") });
      assertNoOverlap(`${mode} ${width}px`, r);
    }
    console.log(JSON.stringify(table, null, 1));
  });
}

test("Gesamtsicht ohne Konto-Spalte: Kontokürzel steht in der Payee-Zelle", async () => {
  await page.locator(".ledger-account-strip").getByText("Alle Konten").filter({ visible: true }).first().click();
  // Eine Breite, bei der die Konto-Spalte fehlt: schrittweise schmaler, bis "KONTO" fehlt.
  let found = null;
  for (let width = 1440; width >= 800; width -= 20) {
    const r = await measure(width);
    if (r.tableCount > 0 && !r.headers.some((h) => h.text.toUpperCase() === "KONTO")) {
      found = { width, r };
      break;
    }
  }
  assert.ok(found, "Es gibt eine Breite zwischen 800 und 1440, bei der die Tabelle ohne Konto-Spalte steht");
  assert.match(found.r.payeeCellText, /\S+\s*Transfer|\S+Transfer/, `Kürzel vor dem Payee: ${found.r.payeeCellText}`);
});
