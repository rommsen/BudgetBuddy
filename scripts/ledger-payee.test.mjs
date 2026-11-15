// scripts/ledger-payee.test.mjs
//
// Wächter für `ledger-fxt09`: Payee ändern im Kassenbuch-Detail. Gemessen am echten, per Vite
// gestarteten Fable-Client gegen gemockte Ledger-Antworten (nie die echte Entwicklungsdatenbank).
// Nur, was der Browser allein zeigen kann: der eine Happy Path am Handy mit Touch (die Auswahl
// ersetzt den Inhalt des Sheets, die Tastatur bleibt beim Tippen und Übernehmen offen, Rückgängig im
// Toast, dazu „Payee entfernen“ mit Toast und Rückgängig, ledger-pkrwd) und der Fokus am PC (Fokus in der Suche, Esc zurück auf die Payee-Zeile). Texte, Sperren und
// Zustände prüfen `src/Tests/LedgerPayeeClientTests.fs` und `src/Tests/LedgerPayeeTests.fs`.
//
// Ausführen: node --test scripts/ledger-payee.test.mjs
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
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";
const BANK_PAYEE = "REWE SAGT DANKE 4711";

/// Veränderlicher Server-Zustand der Mocks.
const state = { setCalls: [], payee: BANK_PAYEE };

const idOf = (n) => buildEntryFixture("x", n).Id;

function entries(accountId) {
  return [
    buildEntryFixture(accountId, 1, {
      Date: TODAY,
      LinkedToYnab: true,
      CategoryId: CATEGORY_IDS.lebensmittel,
      CategoryName: "Lebensmittel",
      CategoryGroupName: "Alltag",
      Payee: state.payee,
    }),
    buildEntryFixture(accountId, 2, { Date: TODAY, LinkedToYnab: true, Payee: "Bahn Payee" }),
  ];
}

async function open(context) {
  state.setCalls.length = 0;
  state.payee = BANK_PAYEE;
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
    resolveEntries: () => entries(accountsFixture.DefaultAccountId),
  });
  for (const name of ["getEntryHistory", "getPayeeCategorySuggestions"]) {
    await target.route(`**/api/LedgerApi/${name}`, (route) => route.fulfill({ contentType: "application/json", body: "[]" }));
  }
  await target.route("**/api/LedgerApi/getLedgerPayeeSuggestions", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify([
        { Payee: "Rewe", Count: 12 },
        { Payee: BANK_PAYEE, Count: 3 },
        { Payee: "Bahn Payee", Count: 2 },
      ]),
    })
  );
  await target.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ SavedColor: null, SuggestedColor: null }) })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/setEntryPayee", (route) => {
    const [entryId, text] = route.request().postDataJSON();
    state.setCalls.push({ entryId, text });
    const previous = state.payee;
    // `None` („Payee entfernen“, ledger-pkrwd) kommt als null.
    state.payee = text === null ? null : text.trim();
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: { PreviousPayee: previous, CurrentPayee: state.payee, PayeeChange: "PayeeChanged", PayeeWriteBack: "PayeeSent" },
      }),
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

const detailTitle = (scope) => scope.locator('[data-testid="detail-title"]');
// ledger-zfj78: im Sheet steht der Payee als Titel im Kopf des Sheets, der Inhalt beginnt mit dem Betrag.
const detailContent = (scope) => scope.locator('[data-testid="detail-payee"]');

test("[flow:payee-aendern] Handy: Payee im Detail ändern und entfernen, beides im Toast rückgängig machen, die Tastatur bleibt bis zum Schließen offen", async () => {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText(BANK_PAYEE, { exact: true }).first().tap();
  const sheet = page.locator(".bottom-sheet.active").first();
  await detailContent(sheet).waitFor();

  await sheet.locator('[data-testid="detail-payee-arrow"]').tap();
  const picker = sheet.locator('[data-testid="payee-picker"]');
  await picker.locator('[data-testid="payee-picker-option"]').first().waitFor();
  assert.equal(await detailContent(sheet).count(), 0, "die Auswahl ersetzt den Inhalt des Detail-Sheets");
  const field = picker.locator('[data-testid="payee-picker-search"] input');
  assert.equal(await field.inputValue(), BANK_PAYEE, "die Suche ist mit dem heutigen Payee vorbelegt");
  assert.ok(!(await field.evaluate((el) => document.activeElement === el)), "am Handy kein Autofokus");

  await field.tap();
  await field.evaluate((el) => {
    window.__payeeBlurs = 0;
    el.addEventListener("blur", () => window.__payeeBlurs++);
  });
  await field.fill("Rewe Markt");
  const take = picker.locator('[data-testid="payee-picker-take"]');
  assert.equal((await take.textContent()).trim(), "„Rewe Markt“ übernehmen");
  await take.tap();
  assert.equal(await page.evaluate(() => window.__payeeBlurs), 0, "Tippen und Übernehmen nehmen dem Suchfeld den Fokus nicht");

  await page.waitForFunction(
    (p) => document.querySelector('.bottom-sheet.active .sheet-header h3')?.textContent === p,
    "Rewe Markt"
  );
  assert.deepEqual(state.setCalls[0], { entryId: idOf(1), text: "Rewe Markt" }, "der neue Payee geht an den Befehl");
  await page.getByText("Payee geändert · Rewe Markt").first().waitFor();

  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).tap();
  await page.waitForFunction(
    (p) => document.querySelector('.bottom-sheet.active .sheet-header h3')?.textContent === p,
    BANK_PAYEE
  );
  assert.deepEqual(state.setCalls[1], { entryId: idOf(1), text: BANK_PAYEE }, "Rückgängig schickt den alten Payee über denselben Befehl");
  // Der getippte Toast geht in seinem Abgang (`Types.Toast.exitDurationMs`) noch mit Knopf aus dem DOM, ein
  // zweiter Tipp darauf wirkt nicht mehr (`ToastActionClicked`). Erst wenn er weg ist, steht nur noch der nächste da.
  await page.getByText("Payee geändert · Rewe Markt").waitFor({ state: "detached" });

  // ledger-pkrwd: „Payee entfernen“ antippen übernimmt sofort, der Toast bietet Rückgängig.
  await sheet.locator('[data-testid="detail-payee-arrow"]').tap();
  const remove = picker.locator('[data-testid="payee-picker-remove"]');
  await remove.waitFor();
  await remove.tap();
  await page.waitForFunction(
    (p) => document.querySelector('.bottom-sheet.active .sheet-header h3')?.textContent === p,
    "ohne Payee"
  );
  assert.deepEqual(state.setCalls[2], { entryId: idOf(1), text: null }, "entfernen schickt den Befehl ohne Payee");
  await page.getByText("Payee entfernt", { exact: true }).first().waitFor();
  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).tap();
  await page.waitForFunction(
    (p) => document.querySelector('.bottom-sheet.active .sheet-header h3')?.textContent === p,
    BANK_PAYEE
  );
  assert.deepEqual(state.setCalls[3], { entryId: idOf(1), text: BANK_PAYEE }, "Rückgängig setzt den alten Payee mit demselben Befehl");
  await context.close();
});

test("[fokus] PC: beim Öffnen steht der Fokus in der Suche, ↓ macht eine Zeile zum aktiven Nachfahren, Esc führt zurück ins Detail auf die Payee-Zeile", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await page.locator("table tbody tr", { hasText: BANK_PAYEE }).first().locator("td").nth(2).click();
  const panel = page.locator('[data-testid="ledger-detail-panel"]');
  await detailTitle(panel).waitFor();

  await panel.locator('[data-testid="detail-payee-arrow"]').click();
  const field = page.locator('[data-layer="dialog"] [data-testid="payee-picker-search"] input');
  await field.waitFor();
  await page.waitForFunction(
    () => document.activeElement === document.querySelector('[data-layer="dialog"] [data-testid="payee-picker-search"] input')
  );

  // Der Fokus bleibt in der Suche, ↓ markiert eine Zeile: das Suchfeld nennt sie als aktiven Nachfahren
  // der Listbox, die es steuert (Review-Fund zu ledger-fxt09).
  await page.keyboard.press("ArrowDown");
  const combobox = await field.evaluate((el) => {
    const active = document.getElementById(el.getAttribute("aria-activedescendant") ?? "");
    const listbox = document.getElementById(el.getAttribute("aria-controls") ?? "");
    return {
      role: el.getAttribute("role"),
      focused: document.activeElement === el,
      listboxRole: listbox?.getAttribute("role") ?? null,
      activeSelected: active?.getAttribute("aria-selected") ?? null,
      activeInListbox: !!(active && listbox?.contains(active)),
      onlyOptions: listbox ? [...listbox.children].every((c) => c.getAttribute("role") === "option") : false,
    };
  });
  assert.deepEqual(
    combobox,
    { role: "combobox", focused: true, listboxRole: "listbox", activeSelected: "true", activeInListbox: true, onlyOptions: true },
    "Combobox: aria-controls auf die Listbox, aria-activedescendant auf die markierte Zeile"
  );

  await page.keyboard.press("Escape");
  await page.locator('[data-layer="dialog"] [data-testid="payee-picker"]').waitFor({ state: "detached" });
  await page.waitForFunction(
    () => document.activeElement === document.querySelector('[data-testid="ledger-detail-panel"] [data-testid="detail-payee-arrow"]')
  );
  assert.equal(await detailTitle(panel).textContent(), BANK_PAYEE, "das Detail bleibt offen");
  assert.equal(state.setCalls.length, 0, "Esc ändert nichts");
  await context.close();
});
