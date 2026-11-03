// scripts/ledger-split-cashback.test.mjs
//
// Wächter für `ledger-s8t3s` (ADR 0004, ADR 0006): der Knopf "Barabhebung" im Split-Editor des
// Kassenbuchs. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten:
// am Handy steht der Knopf im Editor-Sheet so hoch wie "Zeile hinzufügen", der Tipp setzt den
// Übertrag aufs Quick-Add-Konto. Belegung, Rest, Speichern und den Fall ohne Quick-Add-Konto prüft
// `src/Tests/LedgerSplitEditorClientTests.fs` (infrastructure-t5m2b).
//
// Ausführen: node --test scripts/ledger-split-cashback.test.mjs
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
  buildSettingsFixture,
  fixtureGuid,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";
const state = { saveCalls: [] };
const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const CASH_ID = { YnabAccountId: fixtureGuid(7202) };

const accountsList = () => [
  { Id: { YnabAccountId: fixtureGuid(7201) }, Name: "Tagesgeld s8t3s", Balance: eur(0), OnBudget: true, Closed: false },
  { Id: CASH_ID, Name: "Bargeld s8t3s", Balance: eur(0), OnBudget: true, Closed: false },
];

async function open(context, quickAddAccountId) {
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
    resolveEntries: () => [
      buildEntryFixture(accountsFixture.DefaultAccountId, 1, {
        Date: TODAY,
        LinkedToYnab: true,
        NeedsReview: true,
        Amount: eur(-81.9),
        CategorizationWriteBackFailure: null,
        Payee: "Bauhaus s8t3s",
        CategoryId: CATEGORY_IDS.lebensmittel,
        CategoryName: "Lebensmittel",
        CategoryGroupName: "Alltag",
      }),
    ],
  });
  const settings = buildSettingsFixture();
  settings.Ynab.QuickAddAccountId = quickAddAccountId;
  await target.route("**/api/SettingsApi/getSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(settings) })
  );
  for (const name of ["getEntryHistory", "getPayeeCategorySuggestions"]) {
    await target.route(`**/api/LedgerApi/${name}`, (route) => route.fulfill({ contentType: "application/json", body: "[]" }));
  }
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/getBudgetAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: accountsList() }) })
  );
  await target.route("**/api/LedgerApi/getLedgerEntry", (route) => route.fulfill({ contentType: "application/json", body: "null" }));
  await target.route("**/api/LedgerApi/setEntrySplits", (route) => {
    const [entryId, lines] = route.request().postDataJSON();
    state.saveCalls.push({
      entryId,
      lines: lines.map((l) => ({ ...l, Amount: { ...l.Amount, Amount: Number(l.Amount.Amount) } })),
    });
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: null, Change: "CategoryChanged", WriteBack: "CategorySent" } }),
    });
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[flow:barabhebung-aufteilen] Handy (390x844): Barabhebung im Editor-Sheet, gleich hoch wie Zeile hinzufügen, der Tipp setzt den Übertrag aufs Bar-Konto", async () => {
  state.saveCalls.length = 0;
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context, CASH_ID);
  await page.getByText("Bauhaus s8t3s").first().tap();
  const sheet = page.locator(".bottom-sheet.active").first();
  await sheet.waitFor();
  // Das Öffnen des Editors holt Quick-Add-Konto und Konten; der Knopf löst das Ziel erst beim Tipp auf.
  const loaded = Promise.all([
    page.waitForResponse("**/api/SettingsApi/getSettings"),
    page.waitForResponse("**/api/LedgerApi/getBudgetAccounts"),
  ]);
  await sheet.locator('[data-testid="detail-split-action"]').tap();
  await sheet.locator('[data-testid="split-summary"]').waitFor();
  await loaded;
  const button = sheet.getByRole("button", { name: "Barabhebung", exact: true });
  await button.scrollIntoViewIfNeeded();
  const box = await button.boundingBox();
  const add = await sheet.getByRole("button", { name: "Zeile hinzufügen" }).boundingBox();
  assert.equal(box.height, add.height, "gleiche Höhe wie der Nachbarknopf");
  assert.ok(box.x + box.width <= 390, "Knopf liegt im Sheet");
  await button.tap();
  assert.match(await sheet.locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld s8t3s/);
  await context.close();
});
