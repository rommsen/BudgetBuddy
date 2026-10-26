// scripts/ledger-split-cashback.test.mjs
//
// Wächter für `ledger-s8t3s` (ADR 0004, ADR 0006): der Knopf "Barabhebung" im Split-Editor des
// Kassenbuchs. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten
// (Muster wie `ledger-split-editor.test.mjs`, nie die echte Entwicklungsdatenbank): frisch geöffnet
// ergibt der Knopf die Kategorie der Buchung plus einen Übertrag aufs Quick-Add-Konto, die Beträge
// bleiben leer, der Ausgleich geht über "Rest", gespeichert wird über `setEntrySplits`. Ohne
// Quick-Add-Konto bleibt das Ziel leer und die Konto-Auswahl geht auf.
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

const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');

async function openEditorAtPc(page) {
  await page.locator("table tbody tr", { hasText: "Bauhaus s8t3s" }).first().locator("td").nth(2).click();
  await panel(page).waitFor();
  await panel(page).locator('[data-testid="detail-split-action"]').click();
  await panel(page).locator('[data-testid="split-summary"]').waitFor();
}

// Der Abruf des Quick-Add-Kontos und der Konten läuft beim Öffnen; der Knopf löst das Ziel erst
// beim Klick auf, deshalb wird hier auf beide Antworten gewartet (Konto-Auswahl zeigt die Liste).
async function settle(page) {
  await page.waitForTimeout(400);
}

test("PC: Barabhebung frisch geöffnet: Kategorie plus Übertrag aufs Quick-Add-Konto, Beträge leer, Ausgleich über Rest", async () => {
  state.saveCalls.length = 0;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context, CASH_ID);
  await openEditorAtPc(page);
  await settle(page);
  const button = panel(page).getByRole("button", { name: "Barabhebung", exact: true });
  assert.equal(await button.count(), 1, "Knopf Barabhebung da");
  assert.equal(await button.isDisabled(), false);
  await button.click();
  assert.match(await panel(page).locator('[data-testid="split-target-0"]').textContent(), /Lebensmittel/, "bisherige Kategorie");
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld s8t3s/, "Übertrag aufs Bar-Konto");
  assert.equal(await panel(page).locator('[data-testid="split-amount-0"]').inputValue(), "", "Betrag 0 leer");
  assert.equal(await panel(page).locator('[data-testid="split-amount-1"]').inputValue(), "", "Betrag 1 leer");
  assert.equal(await panel(page).locator('[data-testid^="split-line-"]').count(), 2, "keine dritte Zeile");

  await panel(page).locator('[data-testid="split-amount-1"]').fill("50");
  await panel(page).locator('[data-testid="split-line-0"]').getByRole("button", { name: "Rest", exact: true }).click();
  assert.equal(await panel(page).locator('[data-testid="split-amount-0"]').inputValue(), "31,90");
  await panel(page).getByRole("button", { name: "Speichern", exact: true }).click();
  await page.getByText("Aufgeteilt in 2 Zeilen", { exact: true }).waitFor();
  const lines = state.saveCalls[0].lines;
  assert.deepEqual(lines.map((l) => l.Amount.Amount), [-31.9, -50]);
  assert.ok(lines[1].Target.ToTransfer, "zweite Zeile ist ein Übertrag");
  assert.deepEqual(lines[1].Target.ToTransfer[0], CASH_ID, "aufs Quick-Add-Konto");
  await context.close();
});

test("PC: Barabhebung belegt die erste Zeile ohne Ziel, eine weitere Betätigung hängt eine neue Zeile an", async () => {
  state.saveCalls.length = 0;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context, CASH_ID);
  await openEditorAtPc(page);
  await settle(page);
  const button = panel(page).getByRole("button", { name: "Barabhebung", exact: true });
  await button.click();
  await button.click();
  assert.equal(await panel(page).locator('[data-testid^="split-line-"]').count(), 3, "zweiter Klick: neue Zeile");
  assert.match(await panel(page).locator('[data-testid="split-target-0"]').textContent(), /Lebensmittel/, "Zeile 0 unverändert");
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /→ Bargeld s8t3s/, "Zeile 1 unverändert");
  assert.match(await panel(page).locator('[data-testid="split-target-2"]').textContent(), /→ Bargeld s8t3s/, "neue Übertrag-Zeile");
  await context.close();
});

test("PC: Ohne Quick-Add-Konto bleibt das Ziel leer und die Konto-Auswahl geht auf", async () => {
  state.saveCalls.length = 0;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context, null);
  await openEditorAtPc(page);
  await settle(page);
  await panel(page).getByRole("button", { name: "Barabhebung", exact: true }).click();
  assert.match(await panel(page).locator('[data-testid="split-target-1"]').textContent(), /wählen/, "Ziel leer");
  await panel(page).locator('[data-testid="account-picker"]').waitFor();
  await context.close();
});

test("Handy (390x844): Barabhebung im Editor-Sheet, gleich hoch wie Zeile hinzufügen", async () => {
  state.saveCalls.length = 0;
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context, CASH_ID);
  await page.getByText("Bauhaus s8t3s").first().tap();
  const sheet = page.locator(".bottom-sheet.active").first();
  await sheet.waitFor();
  await sheet.locator('[data-testid="detail-split-action"]').tap();
  await sheet.locator('[data-testid="split-summary"]').waitFor();
  await settle(page);
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
