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
        { Id: CATEGORY, Name: "Lebensmittel m1bce", Assigned: eur(100), Activity: eur(-50), Available: eur(50), Underfunded: null, Target: null, Snoozed: false },
        { Id: guid(2), Name: "Urlaub m1bce", Assigned: eur(10), Activity: eur(0), Available: eur(10), Underfunded: null, Target: null, Snoozed: false },
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
    return json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: [CATEGORY, guid(2)] } });
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

test("Handy: Zeile tippen, Tab Buchungen, Buchung tippen, Kassenbuch-Detail ist offen, zurück steht der Tab wieder", async () => {
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

test("Buchungen kommen erst im Tab: die Auswahl allein fragt das Kassenbuch nicht", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await row(page, "Lebensmittel m1bce").click();
    await page.locator('[data-testid="inspector-money"]').waitFor();
    assert.equal(calls.lists.length, 0, "noch keine Abfrage");
    await openTab(page, "Buchungen");
    await page.locator('[data-testid="detail-entry"]').first().waitFor();
    assert.equal(calls.lists.length, 1, "genau eine Abfrage");
  } finally {
    await context.close();
  }
});

test("Kategoriewechsel im Tab Buchungen: am PC per Zeile, im Sheet per Pfeil, je eine neue Abfrage", async () => {
  const pc = await openPage(1440, 900);
  try {
    await openDetail(pc.page);
    await row(pc.page, "Urlaub m1bce").click();
    await pc.page.waitForFunction(() => document.querySelector("aside h2")?.textContent.startsWith("Urlaub"));
    await pc.page.locator('[data-testid="detail-entry"]').first().waitFor();
    assert.deepEqual(pc.calls.lists.map((l) => l.CategoryId), [CATEGORY, guid(2)]);
  } finally {
    await pc.context.close();
  }
  const phone = await openPage(390, 844);
  try {
    await openDetail(phone.page);
    await phone.page.locator('[data-testid="inspector-next"]').click();
    await phone.page.locator(".bottom-sheet.active").getByRole("heading", { name: "Urlaub m1bce" }).waitFor();
    await phone.page.locator('[data-testid="detail-entry"]').first().waitFor();
    assert.deepEqual(phone.calls.lists.map((l) => l.CategoryId), [CATEGORY, guid(2)]);
  } finally {
    await phone.context.close();
  }
});

test("PC: der Inspektor steht als Panel, der Sprung geht zum Panel im Kassenbuch und zurück", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel m1bce").click();
    // Fixture: Zugewiesen 100, Aktivität -50, Verfügbar 50. Jeder Betrag steht unter seinem Titel.
    assert.equal(await page.locator('[data-testid="assign-field"] input').inputValue(), "100,00");
    assert.match(await text(page.locator('[data-testid="detail-activity"]')), /^Aktivität -50,00/i);
    assert.match(await text(page.locator('[data-testid="detail-available"]')), /^Verfügbar 50,00/i);
    await openTab(page, "Buchungen");
    await page.locator('[data-testid="detail-entry"]').first().waitFor();
    assert.match(await text(page.locator('[data-testid="detail-total"]')), /-40,00/);
    await page.locator("aside").getByRole("heading", { name: "Lebensmittel m1bce" }).waitFor();
    await page.getByText(/Alltag m1bce · .*2026/).waitFor();
    await page.locator('[data-testid="detail-entry"]').first().click();
    await page.waitForURL(/#\/ledger\/entry\//);
    await page.getByLabel("Detail Sprung m1bce").waitFor();
    await page.keyboard.press("Escape");
    await expectBackInDetail(page);
  } finally {
    await context.close();
  }
});

test("Handy: der Rand im Sheet ist einfach, ein großer Betrag überlappt bei 360 px nicht", async () => {
  const lines = [{ ...detailLines[0], Payee: "Ein sehr langer Empfängername, der abgeschnitten wird", Amount: eur(-11234.56) }];
  const { context, page } = await openPage(360, 740, { lines });
  try {
    await openDetail(page);
    const row = page.locator('[data-testid="detail-entry"]').first();
    const measured = await row.evaluate((el) => {
      const [payee, amount] = el.querySelector("span").children;
      const box = (n) => n.getBoundingClientRect();
      return {
        rowLeft: box(el).left,
        rowRight: box(el).right,
        payeeLeft: box(payee).left,
        payeeRight: box(payee).right,
        amountLeft: box(amount).left,
        amountRight: box(amount).right,
        width: window.innerWidth,
      };
    });
    assert.equal(measured.rowLeft, 0, "die Zeile reicht bis an den Rand");
    assert.equal(measured.rowRight, measured.width);
    assert.equal(measured.payeeLeft, 16, "ein Rand von 16 px");
    assert.ok(measured.payeeRight <= measured.amountLeft, "Empfänger und Betrag überlappen nicht");
    assert.ok(measured.amountRight <= measured.width - 16, "der Betrag bleibt im Rand");
    assert.match(await text(row), /-11\.234,56/);
  } finally {
    await context.close();
  }
});

const jumpToPanel = async (page) => {
  await openDetail(page);
  await page.locator('[data-testid="detail-entry"]').first().click();
  await page.waitForURL(/#\/ledger\/entry\//);
  await page.getByLabel("Detail Sprung m1bce").waitFor();
};

test("Tab-Leiste nach dem Sprung: das Budget zeigt das Detail nicht wieder", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await jumpToPanel(page);
    await page.locator("nav").getByText("Budget", { exact: true }).filter({ visible: true }).click();
    await page.waitForURL(/#\/budget$/);
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.equal(await page.locator('[data-testid="category-detail"]').count(), 0);
  } finally {
    await context.close();
  }
});

test("Kontowechsel auf der Route: die Liste lädt nicht neu, Browser-Zurück öffnet die Buchung nicht erneut", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await jumpToPanel(page);
    const accountLoads = calls.accounts;
    await page.locator(".ledger-account-strip").getByText("Alle Konten").filter({ visible: true }).first().click();
    await page.waitForURL(/#\/ledger$/);
    assert.equal(calls.accounts, accountLoads, "der Schritt in die Liste lädt die Seite nicht neu");
    await page.goBack();
    await page.waitForURL(/#\/budget$/);
  } finally {
    await context.close();
  }
});

test("PC: wechselt das Panel die Buchung, zieht die URL nach, ohne die Seite neu zu laden", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await jumpToPanel(page);
    const accountLoads = calls.accounts;
    const shown = buildEntryFixture(accounts.DefaultAccountId, 1, { LinkedToYnab: true });
    await page.locator("table tbody tr", { hasText: shown.Payee }).first().click();
    await page.getByLabel(`Detail ${shown.Payee}`).waitFor();
    await page.waitForURL(new RegExp(`#/ledger/entry/${shown.Id}$`));
    assert.equal(calls.accounts, accountLoads, "keine Schleife, kein Neuladen");
    await page.getByLabel(`Detail ${shown.Payee}`).waitFor();
  } finally {
    await context.close();
  }
});

test("Unbekannte Buchung nach dem Sprung: zurück im Kategorie-Detail", async () => {
  const { context, page } = await openPage(390, 844, { entry: () => null });
  try {
    await openDetail(page);
    await page.locator('[data-testid="detail-entry"]').first().click();
    await expectBackInDetail(page);
  } finally {
    await context.close();
  }
});

test("Unbekannte Buchung per Direktaufruf: die Route führt in die Liste", async () => {
  const { context, page } = await openPage(390, 844, { entry: () => null });
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger/entry/${jumpedEntry.Id}`);
    await page.reload();
    await page.waitForURL(/#\/ledger$/);
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
