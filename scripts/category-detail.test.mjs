// scripts/category-detail.test.mjs
//
// Wächter für `budgeting-m1bce`: das Kategorie-Detail auf der Budget-Seite. Gemessen am echten, per
// Vite gestarteten Fable-Client gegen gemockte BudgetApi und LedgerApi: am Handy vom Tipp auf den
// Kategorie-Namen über die Buchung ins Kassenbuch-Detail und zurück, per Browser-Knopf und per
// Schließen; dazu Tipp auf Verfügbar, "Verschieben" aus dem Detail, Trefferfläche, Dialog am PC.
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
        { Id: CATEGORY, Name: "Lebensmittel m1bce", Assigned: eur(100), Activity: eur(-50), Available: eur(50), Underfunded: null, Snoozed: false },
        { Id: guid(2), Name: "Urlaub m1bce", Assigned: eur(10), Activity: eur(0), Available: eur(10), Underfunded: null, Snoozed: false },
      ],
    },
  ],
};

const detailLines = [
  { EntryId: jumpedEntry.Id, AccountId: SECOND_ACCOUNT, AccountName: "Testkonto 2", Date: "2026-11-20T00:00:00.0000000", Payee: "Sprung m1bce", Amount: eur(-30), IsSplitShare: false },
  { EntryId: guid(9), AccountId: accounts.DefaultAccountId, AccountName: "Testkonto 1", Date: "2026-11-02T00:00:00.0000000", Payee: "Split m1bce", Amount: eur(-10), IsSplitShare: true },
];

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { lists: [] };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, {
    accountsFixture: accounts,
    balancesFixture: buildBalancesFixture(accounts.Accounts),
    resolveEntries: () => [buildEntryFixture(accounts.DefaultAccountId, 1, { LinkedToYnab: true })],
  });
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => json(route, { Ok: month }));
  await page.route("**/api/LedgerApi/getCategoryMonthEntries", (route) => {
    calls.lists.push(route.request().postDataJSON()[0]);
    return json(route, { Ok: detailLines });
  });
  await page.route("**/api/LedgerApi/getLedgerEntry", (route) => json(route, jumpedEntry));
  await page.route("**/api/LedgerApi/getEntryHistory", (route) => json(route, []));
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const nameButton = (page, name) =>
  page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name }).locator('[data-testid="budget-category-name"]');

const openDetail = async (page) => {
  await nameButton(page, "Lebensmittel m1bce").click();
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

test("Handy: Namen tippen, Buchung tippen, Kassenbuch-Detail ist offen, zurück zeigt das Kategorie-Detail", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await openDetail(page);
    assert.deepEqual(calls.lists, [{ CategoryId: CATEGORY, Month: "2026-11-01" }]);
    const detail = await text(page.locator('[data-testid="category-detail"]'));
    assert.match(detail, /Sprung m1bce/);
    assert.match(detail, /Split/);
    assert.match(detail, /Testkonto 2/);

    // Summe -40, YNAB meldet -50: der Hinweis nennt beide.
    assert.match(await text(page.locator('[data-testid="detail-deviation"]')), /^Kassenbuch: .*40,00.*, YNAB: .*50,00/);

    await page.locator('[data-testid="detail-entry"]').first().click();
    await page.waitForURL(/#\/ledger\/entry\//);
    await page.locator(".bottom-sheet.active").filter({ hasText: "Sprung m1bce" }).waitFor();

    await page.goBack();
    await expectBackInDetail(page);
    assert.equal(calls.lists.length, 2, "die Liste wird nach der Rückkehr neu geholt");
  } finally {
    await context.close();
  }
});

test("Handy: Schließen im Kassenbuch-Detail führt ebenfalls zurück ins Kategorie-Detail", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await openDetail(page);
    await page.locator('[data-testid="detail-entry"]').first().click();
    const sheet = page.locator(".bottom-sheet.active").filter({ hasText: "Sprung m1bce" });
    await sheet.waitFor();
    await sheet.locator(".sheet-close").click();
    await expectBackInDetail(page);
  } finally {
    await context.close();
  }
});

test("Handy: Kategorie-Name hat 48 px Trefferfläche, Verfügbar öffnet weiter das Verschieben-Sheet", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    const box = await nameButton(page, "Lebensmittel m1bce").boundingBox();
    assert.ok(box.height >= 48, `Höhe ${box.height}`);
    await page
      .locator('[data-testid="budget-category-row"]:visible')
      .filter({ hasText: "Lebensmittel m1bce" })
      .locator('[data-testid="budget-available"]')
      .click();
    await page.locator('[data-testid="move-dialog"]').waitFor();
    assert.equal(await page.locator('[data-testid="category-detail"]').count(), 0);
  } finally {
    await context.close();
  }
});

test("Handy: 'Verschieben' im Detail öffnet das Sheet dieser Kategorie, danach steht das Detail wieder da", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await openDetail(page);
    await page.locator('[data-testid="detail-move"] button').click();
    await page.locator('[data-testid="move-dialog"]').waitFor();
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Lebensmittel m1bce/);
    await page.locator(".bottom-sheet.active .sheet-close").click();
    await page.locator('[data-testid="detail-entry"]').first().waitFor();
  } finally {
    await context.close();
  }
});

test("PC: das Detail steht im Dialog, der Sprung geht zum Panel im Kassenbuch und zurück", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await openDetail(page);
    // Fixture: Zugewiesen 100, Aktivität -50, Verfügbar 50. Jeder Betrag steht unter seinem Titel.
    assert.match(await text(page.locator('[data-testid="detail-assigned"]')), /^Zugewiesen 100,00/i);
    assert.match(await text(page.locator('[data-testid="detail-activity"]')), /^Aktivität -50,00/i);
    assert.match(await text(page.locator('[data-testid="detail-available"]')), /^Verfügbar 50,00/i);
    await page.locator('[data-testid="detail-entry"]').first().click();
    await page.waitForURL(/#\/ledger\/entry\//);
    await page.getByLabel("Detail Sprung m1bce").waitFor();
    await page.keyboard.press("Escape");
    await expectBackInDetail(page);
  } finally {
    await context.close();
  }
});

test("Direktaufruf der Route öffnet die Buchung, Schließen bleibt im Kassenbuch", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger/entry/${jumpedEntry.Id}`);
    await page.reload();
    const sheet = page.locator(".bottom-sheet.active").filter({ hasText: "Sprung m1bce" });
    await sheet.waitFor();
    await sheet.locator(".sheet-close").click();
    await page.waitForURL(/#\/ledger$/);
  } finally {
    await context.close();
  }
});
