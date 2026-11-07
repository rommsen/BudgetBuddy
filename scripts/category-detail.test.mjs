// scripts/category-detail.test.mjs
//
// Wächter für `budgeting-m1bce` und `budgeting-g8s3w`: das Kategorie-Detail lebt im Tab Buchungen
// des Inspektors. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte BudgetApi und
// LedgerApi: am Handy von der Zeile über den Tab und die Buchung ins Kassenbuch-Detail und zurück,
// per Browser-Knopf und per Schließen; dazu "Verschieben" aus dem Tab Geld, Panel am PC.
//
// Ausführen: node --test scripts/category-detail.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;

const CATEGORY = guid(1);
const accounts = buildAccountsFixture(2);
const SECOND_ACCOUNT = accounts.Accounts[1].Id;

// Liegt nicht auf der geladenen Seite des Kassenbuchs und nicht auf dessen Standardkonto.
const jumpedEntry = buildEntryFixture(SECOND_ACCOUNT, 77, { Payee: "Sprung m1bce", LinkedToYnab: true });

const month = {
  Month: "2026-11-01",
  ToBeBudgeted: eur(35.45),
  Groups: [
    {
      Name: "Alltag m1bce",
      Categories: [
        { Id: CATEGORY, Name: "Lebensmittel m1bce", Assigned: eur(100), Activity: eur(-50), Available: eur(50), Underfunded: null, Target: null, Pause: null, OpenWriteBack: null },
        { Id: guid(2), Name: "Urlaub m1bce", Assigned: eur(10), Activity: eur(0), Available: eur(10), Underfunded: null, Target: null, Pause: null, OpenWriteBack: null },
      ],
    },
  ],
};

const detailLines = [
  { EntryId: jumpedEntry.Id, AccountId: SECOND_ACCOUNT, AccountName: "Testkonto 2", Date: "2026-11-20T00:00:00.0000000", Payee: "Sprung m1bce", Amount: eur(-30), IsSplitShare: false },
  { EntryId: guid(9), AccountId: accounts.DefaultAccountId, AccountName: null, Date: "2026-11-02T00:00:00.0000000", Payee: "Rewe m1bce", Amount: eur(-10), IsSplitShare: true },
];

async function openPage(width, height, { lines = detailLines, entry = () => jumpedEntry } = {}) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { lists: [], accounts: 0, frames: 0 };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, {
    accountsFixture: accounts,
    balancesFixture: buildBalancesFixture(accounts.Accounts),
    resolveEntries: () => [buildEntryFixture(accounts.DefaultAccountId, 1, { LinkedToYnab: true })],
  });
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) => {
    calls.accounts += 1;
    return route.fallback();
  });
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) => {
    calls.frames += 1;
    return json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: [CATEGORY, guid(2)], MonthStartHidden: [] } });
  });
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => json(route, { Ok: month }));
  await page.route("**/api/LedgerApi/getCategoryMonthEntries", (route) => {
    calls.lists.push(route.request().postDataJSON()[0]);
    return json(route, { Ok: lines });
  });
  await page.route("**/api/LedgerApi/getLedgerEntry", (route) => json(route, entry()));
  await page.route("**/api/LedgerApi/getEntryHistory", (route) => json(route, []));
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const row = (page, name) => page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });

const openTab = (page, name) => page.locator('[data-testid="inspector-tabs"]').getByRole("tab", { name }).click();

/// Zeile wählen, Tab Buchungen öffnen.
const openDetail = async (page) => {
  await row(page, "Lebensmittel m1bce").click();
  await openTab(page, "Buchungen");
  await page.locator('[data-testid="detail-entry"]').first().waitFor();
};

const text = async (locator) => (await locator.innerText()).replace(/\s+/g, " ");

const expectBackInDetail = async (page) => {
  await page.waitForURL(/#\/budget$/);
  await page.locator('[data-testid="detail-entry"]').first().waitFor();
  assert.match(await text(page.locator('[data-testid="category-detail"]')), /Sprung m1bce/);
};

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  await browser?.close();
  if (frontend) await stopServer(frontend);
});

test("[flow:budget-zur-buchung] Handy: Zeile tippen, Tab Buchungen, Buchung tippen, Kassenbuch-Detail ist offen, zurück steht der Tab wieder", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await openDetail(page);
    assert.deepEqual(calls.lists, [{ CategoryId: CATEGORY, Month: "2026-11-01" }]);
    const detail = await text(page.locator('[data-testid="category-detail"]'));
    assert.match(detail, /Sprung m1bce/);
    assert.match(detail, /Testkonto 2/);
    assert.equal(
      await page.locator('[data-testid="detail-entry"]').first().getAttribute("aria-label"),
      await page.evaluate(() => `Sprung m1bce, ${document.querySelector('[data-testid="detail-entry"] .font-mono').textContent}, 20.11.2026, im Kassenbuch öffnen`)
    );

    // Die zweite Zeile ist ein Split-Anteil auf einem unbekannten Konto: kein Trenner ohne Namen.
    const shareLine = await text(page.locator('[data-testid="detail-entry"]').nth(1));
    assert.match(shareLine, /Split/);
    assert.match(shareLine, /10,00/);
    assert.doesNotMatch(shareLine, /·/);

    // Summe -40, YNAB meldet -50: der Hinweis nennt beide.
    assert.match(await text(page.locator('[data-testid="detail-deviation"]')), /^Kassenbuch: .*40,00.*, YNAB: .*50,00/);

    await page.locator('[data-testid="detail-entry"]').first().click();
    await page.waitForURL(/#\/ledger\/entry\//);
    await page.locator(".bottom-sheet.active").filter({ hasText: "Sprung m1bce" }).waitFor();

    await page.goBack();
    await expectBackInDetail(page);
    assert.equal(calls.lists.length, 2, "die Liste wird nach der Rückkehr neu geholt");
    assert.equal(calls.frames, 1, "die Rückkehr liest den Rahmen nicht neu (ADR 0076)");
  } finally {
    await context.close();
  }
});

// Der Sprung selbst ist `[flow:budget-zur-buchung]`, dass ein Kontowechsel im Kassenbuch nicht neu lädt, prüft
// `src/Tests/LedgerPageTests.fs`. Hier bleibt die Taste: Esc führt aus der Buchung zurück ins Panel, und
// Browser-Zurück öffnet die Buchung danach nicht erneut (infrastructure-b3t9k, infrastructure-jc1y1).
test("[tastatur] PC: aus dem Panel zur Buchung im Kassenbuch, Esc führt zurück und Browser-Zurück öffnet die Buchung nicht erneut", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel m1bce").click();
    await openTab(page, "Buchungen");
    await page.locator('[data-testid="detail-entry"]').first().waitFor();
    await page.locator("aside").getByRole("heading", { name: "Lebensmittel m1bce" }).waitFor();
    await page.locator('[data-testid="detail-entry"]').first().click();
    await page.waitForURL(/#\/ledger\/entry\//);
    await page.getByLabel("Detail Sprung m1bce").waitFor();
    await page.keyboard.press("Escape");
    await expectBackInDetail(page);

    await page.goBack();
    await page.waitForURL((url) => !/#\/budget$/.test(url.href));
    assert.doesNotMatch(page.url(), /#\/ledger\/entry\//, "Browser-Zurück öffnet die Buchung nicht erneut");
  } finally {
    await context.close();
  }
});
