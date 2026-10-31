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
// Die Fälle je Antwort stehen auf Client-`update`-Ebene (`src/Tests/LedgerPageTests.fs`,
// "Verspätete Antworten"). Hier steht, was nur der echte Client belegt: der Ablauf mit Status 0
// und dass jede Abfrage ihre Generation wirklich mitgibt.
//
// Tabelle "Aufrufstellen im Browser": getLedgerAccountBalances, getCategories (Kassenbuch),
// getAllRules, getRecentQuickAddTemplates, getEntryHistory, getLedgerPayeeSuggestions. Dazu oben
// getLedgerEntriesPage (Ladeweg). Nur im Inventar von `src/Tests/RetryTests.fs` stehen
// getBudgetAccounts, getSettings (Quick-Add-Konto), getPayeeCategorySuggestions, getLedgerEntry,
// getLedgerEntriesPage (Summen) und getCategories (Regeln): ihr Auslöser braucht hier einen
// langen Klickweg.
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
import { mockFilterBarApi, buildCategoriesFixture } from "./ledger-filter-bar.mjs";

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

test("Ein abgebrochener erster Abruf der Buchungsliste zeigt die Liste ohne Fehlerseite", async () => {
  const { page, context, calls } = await openLedger((n) => n === 1);
  try {
    await visibleText(page, PAYEE).waitFor();
    assert.equal(await page.getByText(ERROR_TITLE).count(), 0, "keine Fehlerseite");
    assert.equal(calls.entries, 2, "ein Abbruch, eine Wiederholung");
  } finally {
    await context.close();
  }
});

test("Scheitern alle drei Versuche, erscheint die Fehlerseite, und ihr Knopf lädt die Liste", async () => {
  let online = false;
  const { page, context, calls } = await openLedger(() => !online);
  try {
    await visibleText(page, ERROR_TITLE).waitFor();
    assert.equal(calls.entries, 3, "ein Versuch und zwei Wiederholungen");

    online = true;
    await page.getByRole("button", { name: "Try Again" }).filter({ visible: true }).first().click();
    await visibleText(page, PAYEE).waitFor();
    assert.equal(await page.getByText(ERROR_TITLE).count(), 0, "Fehlerseite ist weg");
  } finally {
    await context.close();
  }
});

// --- Verspätete Antwort: Klick auf "Betrag", die Anfrage bricht ab, sofort danach Klick aufs Konto. ---

const entriesPageOf = (entry) => ({
  Entries: [entry],
  TotalCount: 1,
  TotalAmount: { Amount: -10, Currency: "EUR" },
  DeletedCount: 0,
  NeedsReviewCount: 0,
  AskLenaCount: 0,
});

/// Öffnet das Kassenbuch auf "Alle Konten". Die Zeile nennt die Abfrage, die sie geliefert hat.
async function openWithLateAnswer() {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accounts = buildAccountsFixture(2);
  const late = { aborts: 0, attempts: 0, last: null };

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) => json(route, accounts));
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    json(route, buildBalancesFixture(accounts.Accounts))
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", async (route) => {
    const [query] = route.request().postDataJSON();
    const allAccounts = !query.AccountId;
    const entry = buildEntryFixture(accounts.DefaultAccountId, 1, { Payee: `Zeile-${allAccounts ? "alle" : "konto"}` });
    if (!allAccounts || late.aborts === 0) return json(route, entriesPageOf(entry));
    late.attempts += 1;
    const abort = late.attempts <= late.aborts;
    await (abort ? route.abort("connectionreset") : json(route, entriesPageOf(entry)));
    if (late.attempts === 3 || !abort) late.finish();
  });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.locator(".ledger-account-strip").getByText("Alle Konten").filter({ visible: true }).first().click();
  await visibleText(page, "Zeile-alle").waitFor();
  return { page, context, late };
}

/// Drei Bilder abwarten: die letzte Antwort der alten Abfrage ist dann im Modell und im DOM.
const framesRendered = (page) =>
  page.evaluate(async () => {
    for (let i = 0; i < 3; i += 1) await new Promise((resolve) => requestAnimationFrame(resolve));
  });

async function clickSortThenAccount(aborts) {
  const { page, context, late } = await openWithLateAnswer();
  const finished = new Promise((resolve) => { late.finish = resolve; });
  late.aborts = aborts;
  await page.locator("table thead th").filter({ hasText: /^Betrag$/i }).locator("button").click();
  await page.locator(".ledger-account-strip").getByText("Testkonto 1").filter({ visible: true }).first().click();
  await visibleText(page, "Zeile-konto").waitFor();
  await finished;
  await framesRendered(page);
  return { page, context };
}

test("Eine späte erfolgreiche Wiederholung der alten Abfrage ersetzt die neuere Liste nicht", async () => {
  const { page, context } = await clickSortThenAccount(2);
  try {
    assert.equal(await page.getByText("Zeile-alle").filter({ visible: true }).count(), 0, "keine Zeile der alten Abfrage");
    assert.ok(await visibleText(page, "Zeile-konto").isVisible(), "die neuere Liste steht");
  } finally {
    await context.close();
  }
});

test("Das späte Scheitern der alten Abfrage zeigt über der neueren Liste keine Fehlerseite", async () => {
  const { page, context } = await clickSortThenAccount(3);
  try {
    assert.equal(await page.getByText(ERROR_TITLE).count(), 0, "keine Fehlerseite");
    assert.ok(await visibleText(page, "Zeile-konto").isVisible(), "die neuere Liste steht");
  } finally {
    await context.close();
  }
});

// --- Tabelle: Aufrufstellen im Browser. Der erste Aufruf bricht ab, der zweite liefert. ---

const clickFirstRow = (page) => page.locator("tbody tr", { hasText: "REWE Mitte" }).filter({ visible: true }).first().locator("td").nth(2).click();
const openPayeeChip = (page) => page.getByRole("button", { name: /^Payee/ }).filter({ visible: true }).first().click();

/// Etwas unter der ersten Pause von `Retry.defaultDelaysMs` (400 ms).
const RETRY_PAUSE_MS = 300;

const CALL_SITES = [
  { route: "/ledger", api: "LedgerApi", method: "getLedgerAccountBalances", answer: (a) => buildBalancesFixture(a.Accounts) },
  // Die Seite ruft `getCategories` am Start doppelt: SyncFlow ungeschützt (gehört zu
  // ynab-sync-k3vq8), das Kassenbuch über den Helfer. Das Fenster bricht beide ab.
  { route: "/ledger", api: "YnabApi", method: "getCategories", answer: () => ({ Ok: buildCategoriesFixture() }), abortWindowMs: 200 },
  { route: "/rules", api: "RulesApi", method: "getAllRules", answer: () => [] },
  { route: "/quickadd", api: "YnabApi", method: "getRecentQuickAddTemplates", answer: () => ({ Ok: [] }) },
  { route: "/ledger", api: "LedgerApi", method: "getEntryHistory", answer: () => [], act: clickFirstRow },
  { route: "/ledger", api: "LedgerApi", method: "getLedgerPayeeSuggestions", answer: () => [], act: openPayeeChip },
];

for (const { route: path, api, method, answer, act, abortWindowMs = 0 } of CALL_SITES) {
  test(`${method} (${path}) übersteht einen Abbruch`, async () => {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    page.setDefaultTimeout(15000);
    const accounts = buildAccountsFixture(2);
    await page.route("**/api/**", (route) =>
      route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
    );
    await mockFilterBarApi(page, { accountsFixture: accounts, balancesFixture: buildBalancesFixture(accounts.Accounts) });
    await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
    // Beleg ist nur ein Aufruf nach der Pause des Helfers, ein zweiter Start-Aufruf zählt nicht.
    let abortedAt = null;
    let retried;
    const retry = new Promise((resolve, reject) => {
      retried = resolve;
      setTimeout(() => reject(new Error(`${method}: keine Wiederholung`)), 5000);
    });
    await page.route(`**/api/${api}/${method}`, async (route) => {
      abortedAt ??= Date.now();
      if (Date.now() - abortedAt <= abortWindowMs) return route.abort("connectionreset");
      await json(route, answer(accounts));
      if (Date.now() - abortedAt >= RETRY_PAUSE_MS) retried();
    });
    try {
      await page.goto(`http://127.0.0.1:${frontendPort}/#${path}`, { waitUntil: "load" });
      if (act) {
        await waitForPageLoaded(page, path);
        await act(page);
      }
      await retry;
    } finally {
      await context.close();
    }
  });
}

// Beide Ausgänge der Abfrage geben ihre Generation mit: sonst gälte die Antwort als alt, und das
// offene Popover fragte ohne Ende neu.
async function openPayeePopover(answerSuggestions) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const accounts = buildAccountsFixture(2);
  const calls = { suggestions: 0 };
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, { accountsFixture: accounts, balancesFixture: buildBalancesFixture(accounts.Accounts) });
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (route) => {
    calls.suggestions += 1;
    return answerSuggestions(route);
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await openPayeeChip(page);
  return { page, context, calls };
}

test("Der Payee-Filter zeigt die geladenen Vorschläge", async () => {
  const { page, context } = await openPayeePopover((route) => json(route, [{ Payee: "Vorschlag Lotte", Count: 3 }]));
  try {
    await visibleText(page, "Vorschlag Lotte").waitFor();
  } finally {
    await context.close();
  }
});

test("Ein Fehler der Vorschläge beendet das Laden ohne neue Abfrage", async () => {
  const { page, context, calls } = await openPayeePopover((route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"kaputt"}' })
  );
  try {
    await page.getByText("Vorschläge werden geladen").waitFor({ state: "hidden" });
    await framesRendered(page);
    assert.equal(calls.suggestions, 1, "genau eine Abfrage");
  } finally {
    await context.close();
  }
});
