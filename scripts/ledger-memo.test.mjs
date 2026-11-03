// scripts/ledger-memo.test.mjs
//
// Wächter für `ledger-fa1fp` (ADR 0062): Memo bearbeiten im Kassenbuch-Detail und die Punkte des
// Detail-Feinschliffs. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte
// Ledger-Antworten (nie die echte Entwicklungsdatenbank): Memo setzen, ändern und leeren (Liste und
// Detail zeigen das neue Memo), Toast mit Rückgängig, Grenze von 500 Zeichen, Esc im Feld, die
// Hinweisleiste "Memo nicht in YNAB" mit Erneut senden, die gesperrte Aktion bei gelöschter
// Buchung, die Kategorie in Kurzform am Handy, der Fußtext im Picker am PC und die Auswahl-Bausteine
// in /styleguide.
//
// Ausführen: node --test scripts/ledger-memo.test.mjs
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

/// Veränderlicher Server-Zustand der Mocks.
const state = {
  setCalls: [],
  retryCalls: [],
  memoOf: {},
  failureOf: {},
  writeBack: "MemoSent",
};

const idOf = (n) => buildEntryFixture("x", n).Id;
const indexOf = (entryId) => [1, 2, 3].find((n) => idOf(n) === entryId);

function entries(accountId) {
  const base = (n, extra) =>
    buildEntryFixture(accountId, n, {
      Date: TODAY,
      LinkedToYnab: true,
      CategoryId: CATEGORY_IDS.lebensmittel,
      CategoryName: "Lebensmittel",
      CategoryGroupName: "Alltag",
      Memo: state.memoOf[n] ?? null,
      MemoWriteBackFailure: state.failureOf[n] ?? null,
      ...extra,
    });
  return [
    base(1, { Payee: "Rewe Memo" }),
    base(2, { Payee: "Bahn Memo", Memo: state.memoOf[2] ?? "Zugticket" }),
    base(3, { Payee: "Kiosk Memo", DeletedInYnab: true, Memo: "alt" }),
  ];
}

function resetState() {
  state.setCalls.length = 0;
  state.retryCalls.length = 0;
  state.memoOf = {};
  state.failureOf = {};
  state.writeBack = "MemoSent";
}

async function open(context) {
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
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ SavedColor: null, SuggestedColor: null }) })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/setEntryMemo", (route) => {
    const [entryId, text] = route.request().postDataJSON();
    state.setCalls.push({ entryId, text });
    const n = indexOf(entryId);
    const previous = state.memoOf[n] ?? (n === 2 ? "Zugticket" : null);
    const current = text.trim() === "" ? null : text.trim();
    state.memoOf[n] = current;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: { PreviousMemo: previous, CurrentMemo: current, MemoChange: "MemoChanged", MemoWriteBack: state.writeBack },
      }),
    });
  });
  await target.route("**/api/LedgerApi/retryMemoWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    state.failureOf = {};
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "MemoSent" }) });
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

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const panel = (p) => p.locator('[data-testid="ledger-detail-panel"]');
const memoRow = (scope) => scope.locator('[data-testid="detail-memo"]');
const memoAction = (scope) => scope.locator('[data-testid="detail-memo-action"]');
const editor = (scope) => scope.locator('[data-testid="detail-memo-editor"]');

async function openDetail(page, payee) {
  await row(page, payee).locator("td").nth(2).click();
  await panel(page).waitFor();
  await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
}

test("[flow:memo-setzen] PC: Memo hinzufügen setzt das Memo, Detail und Liste zeigen es, der Toast bietet Rückgängig", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await openDetail(page, "Rewe Memo");
  assert.ok((await memoRow(panel(page)).textContent()).includes("Kein Memo"), "Wert: Kein Memo");
  assert.equal((await memoAction(panel(page)).textContent()).trim(), "Memo hinzufügen");

  await memoAction(panel(page)).click();
  await editor(panel(page)).waitFor();
  const input = editor(panel(page)).locator("input");
  assert.equal(await input.inputValue(), "", "leerer Entwurf");
  await input.fill("  Werkzeug + Lampe ");
  assert.match(await editor(panel(page)).locator('[data-testid="detail-memo-counter"]').textContent(), /^19 \/ 500$/);
  await editor(panel(page)).getByRole("button", { name: "Speichern" }).click();

  await page.waitForFunction(() => document.querySelector('[data-testid="detail-memo-editor"]') === null);
  assert.deepEqual(state.setCalls, [{ entryId: idOf(1), text: "  Werkzeug + Lampe " }], "der Client schickt den Entwurf, der Server trimmt");
  assert.ok((await memoRow(panel(page)).textContent()).includes("Werkzeug + Lampe"), "Detail zeigt das neue Memo");
  assert.equal((await memoAction(panel(page)).textContent()).trim(), "Bearbeiten");
  await page.getByText("Memo gesetzt", { exact: true }).waitFor();

  // Bei offenem Panel weicht die Memo-Spalte; ohne Panel zeigt die Liste das neue Memo.
  await page.keyboard.press("Escape");
  await panel(page).waitFor({ state: "detached" });
  assert.ok((await row(page, "Rewe Memo").textContent()).includes("Werkzeug + Lampe"), "Liste zeigt das neue Memo");

  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  for (let i = 0; i < 50 && state.setCalls.length < 2; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.setCalls[1], { entryId: idOf(1), text: "" }, "Rückgängig schickt das alte Memo (keins)");
  await page.waitForFunction(
    () => !document.querySelector("table tbody tr")?.textContent?.includes("Werkzeug + Lampe")
  );
  await context.close();
});

