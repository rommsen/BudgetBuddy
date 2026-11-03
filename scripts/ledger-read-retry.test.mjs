// scripts/ledger-read-retry.test.mjs
//
// Regression zu infrastructure-r3tw7. Bug: Bricht der Browser die Abfrage der Buchungsliste
// ab (Firefox, "Http error (0)", Status 0, Befund 2026-10-31), zeigte das Kassenbuch sofort
// die Fehlerseite "Buchungen konnten nicht geladen werden", die nach dem Tipp auf den
// Knopf wieder weg war. infrastructure-y6atr hatte nur die Start-Abfragen geschützt, die
// Buchungsliste selbst nicht. Der Wächter läuft im Browser, weil der Fable.Remoting-Proxy
// unter .NET nicht ausführbar ist und nur der echte Client Status 0 erzeugt.
//
// Zweiter Bug, aus dem ersten Anlauf derselben Task: Die Wiederholung verlängert das Fenster, in
// dem eine Antwort unterwegs ist, auf bis zu 1,6 Sekunden. Ohne Generationszähler ersetzte die
// verspätete Antwort der alten Abfrage die neuere Liste, ihr Scheitern zeigte die Fehlerseite.
// Die Fälle je Antwort und dass jede Abfrage ihre Generation mitgibt (`entriesPageAnswer`,
// `payeeSuggestionsAnswer`) stehen auf Client-`update`-Ebene (`src/Tests/LedgerPageTests.fs`,
// "Verspätete Antworten", infrastructure-b3t9k). Hier steht, was nur der echte Client belegt: der
// Ablauf mit Status 0.
//
// Im Browser läuft hier nur der Ladeweg von getLedgerEntriesPage. Die Verdrahtung je Aufrufstelle
// sichert das Inventar in `src/Tests/RetryTests.fs`.
//
// Ausführen: npm run test:scripts -- scripts/ledger-read-retry.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
} from "./ledger-status-column.mjs";

const ERROR_TITLE = "Buchungen konnten nicht geladen werden";
const PAYEE = "Wiederholungszeile";

let frontend;
let frontendPort;
let browser;

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

/// Öffnet das Kassenbuch. `abortEntries(n)` entscheidet je Aufruf (ab 1 gezählt) von
/// `getLedgerEntriesPage`, ob der Browser die Verbindung abbricht (Status 0).
async function openLedger(abortEntries) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accounts = buildAccountsFixture(2);
  const entry = buildEntryFixture(accounts.DefaultAccountId, 1, { Payee: PAYEE });
  const calls = { entries: 0 };

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) => json(route, accounts));
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    json(route, buildBalancesFixture(accounts.Accounts))
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    calls.entries += 1;
    if (abortEntries(calls.entries)) return route.abort("connectionreset");
    return json(route, {
      Entries: [entry],
      TotalCount: 1,
      TotalAmount: { Amount: -10, Currency: "EUR" },
      DeletedCount: 0,
      NeedsReviewCount: 0,
      AskLenaCount: 0,
    });
  });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return { page, context, calls };
}

const visibleText = (page, text) => page.getByText(text).filter({ visible: true }).first();

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[fable] Abgebrochene Verbindung (Status 0): ein Abbruch wird still wiederholt, nach drei erscheint die Fehlerseite und ihr Knopf lädt", async () => {
  const once = await openLedger((n) => n === 1);
  try {
    await visibleText(once.page, PAYEE).waitFor();
    assert.equal(await once.page.getByText(ERROR_TITLE).count(), 0, "keine Fehlerseite");
    assert.equal(once.calls.entries, 2, "ein Abbruch, eine Wiederholung");
  } finally {
    await once.context.close();
  }

  let online = false;
  const { page, context, calls } = await openLedger(() => !online);
  try {
    await visibleText(page, ERROR_TITLE).waitFor();
    assert.equal(calls.entries, 3, "ein Versuch und zwei Wiederholungen");

    online = true;
    await page.getByRole("button", { name: "Erneut versuchen" }).filter({ visible: true }).first().click();
    await visibleText(page, PAYEE).waitFor();
    assert.equal(await page.getByText(ERROR_TITLE).count(), 0, "Fehlerseite ist weg");
  } finally {
    await context.close();
  }
});
