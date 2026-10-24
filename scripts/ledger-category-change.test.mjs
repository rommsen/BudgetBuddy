// scripts/ledger-category-change.test.mjs
//
// Wächter für `ledger-sfab0` (ADR 0055 und Nachtrag): Kategorie ändern im Kassenbuch-Detail.
// Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte Ledger-Antworten
// (Muster wie `ledger-detail.test.mjs`, nie die echte Entwicklungsdatenbank): der Pfeil an der
// Kategorie, der Picker als Popover im Panel und als zweites Sheet über dem Detail-Sheet, die
// Karte "Budget nach dem Wechsel", der Toast mit Rückgängig, die gesperrten Fälle, die
// Hinweisleiste "Kategorie nicht in YNAB", die ausgegraute Zeile und die Leiste "Neu laden".
//
// Ausführen: node --test scripts/ledger-category-change.test.mjs
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
  entriesPage,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";

/// Veränderlicher Server-Zustand der Mocks, damit die Tests Antworten umschalten können.
const state = {
  setCalls: [],
  suggestionCalls: [],
  retryCalls: [],
  queries: [],
  writeBack: "CategorySent",
  categoryOf: {},
  failureOf: {},
};

const CAT = {
  lebensmittel: { id: CATEGORY_IDS.lebensmittel, name: "Lebensmittel" },
  bahn: { id: CATEGORY_IDS.bahn, name: "Bahn" },
  nebenkosten: { id: CATEGORY_IDS.nebenkosten, name: "Nebenkosten" },
};
const GROUP = { [CATEGORY_IDS.lebensmittel]: "Alltag", [CATEGORY_IDS.bahn]: "Mobilität", [CATEGORY_IDS.nebenkosten]: "Wohnen" };

function entries(accountId) {
  const base = (n, extra) =>
    buildEntryFixture(accountId, n, { Date: TODAY, LinkedToYnab: true, CategorizationWriteBackFailure: null, ...extra });
  const withCategory = (n, key, extra = {}) => {
    const categoryId = state.categoryOf[n] === undefined ? CAT[key].id : state.categoryOf[n];
    const name = categoryId === null ? null : Object.values(CAT).find((c) => c.id === categoryId).name;
    return base(n, {
      CategoryId: categoryId,
      CategoryName: name,
      CategoryGroupName: categoryId === null ? null : GROUP[categoryId],
      CategorizationWriteBackFailure: state.failureOf[n] ?? null,
      ...extra,
    });
  };
  return [
    withCategory(1, "lebensmittel", { Payee: "REWE Mitte" }),
    withCategory(2, "bahn", { Payee: "Deutsche Bahn" }),
    base(3, { Payee: "Girokonto Übertrag", CategoryId: null, CategoryName: null, CategoryGroupName: null, IsTransfer: true }),
    base(4, { Payee: "Café Lotte", CategoryId: CAT.bahn.id, CategoryName: "Bahn", CategoryGroupName: "Mobilität", DeletedInYnab: true }),
    base(5, {
      Payee: "Kaufland Split",
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Splits: [
        { Target: { ToCategory: [{ YnabCategoryId: CAT.lebensmittel.id }, "Lebensmittel"] }, Amount: { Amount: -10, Currency: "EUR" }, Memo: null },
        { Target: "Uncategorized", Amount: { Amount: -5, Currency: "EUR" }, Memo: null },
      ],
    }),
  ];
}

const idOf = (n) => buildEntryFixture("x", n).Id;

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
    resolveEntries: (query) => {
      state.queries.push(query);
      let list = entries(accountsFixture.DefaultAccountId);
      if (query.CategoryIds && query.CategoryIds.length > 0) {
        list = list.filter((e) => query.CategoryIds.includes(e.CategoryId));
      }
      return list;
    },
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.route("**/api/LedgerApi/getPayeeCategorySuggestions", (route) => {
    state.suggestionCalls.push(route.request().postDataJSON());
    route.fulfill({ contentType: "application/json", body: JSON.stringify([{ YnabCategoryId: CAT.bahn.id }]) });
  });
  await target.route("**/api/LedgerApi/setEntryCategory", (route) => {
    const [entryId, choice] = route.request().postDataJSON();
    state.setCalls.push({ entryId, choice });
    const n = [1, 2, 3, 4, 5].find((k) => idOf(k) === entryId);
    const previousId = state.categoryOf[n] === undefined ? (n === 2 ? CAT.bahn.id : CAT.lebensmittel.id) : state.categoryOf[n];
    const previous = previousId === null ? null : (({ id, name }) => ({ CategoryId: { YnabCategoryId: id }, CategoryName: name }))(Object.values(CAT).find((c) => c.id === previousId));
    state.categoryOf[n] = choice === null ? null : choice.YnabCategoryId;
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Previous: previous, Change: "CategoryChanged", WriteBack: state.writeBack } }),
    });
  });
  await target.route("**/api/LedgerApi/retryCategorizationWriteBack", (route) => {
    state.retryCalls.push(route.request().postDataJSON());
    state.failureOf = {};
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "CategorySent" }) });
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

function resetState() {
  state.setCalls.length = 0;
  state.suggestionCalls.length = 0;
  state.retryCalls.length = 0;
  state.queries.length = 0;
  state.writeBack = "CategorySent";
  state.categoryOf = {};
  state.failureOf = {};
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
const categoryRow = (scope) => scope.locator('[data-testid="detail-category"]');
const arrow = (scope) => scope.locator('[data-testid="detail-category-arrow"]');

test("PC (1440): Pfeil öffnet den Picker im Panel, Auswahl ändert, Karte und Toast erscheinen, Rückgängig stellt zurück", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  assert.match(await categoryRow(panel(page)).textContent(), /Alltag: Lebensmittel/);

  await arrow(panel(page)).click();
  const listbox = panel(page).locator('[role="listbox"]');
  await listbox.waitFor();
  // Vorschläge: genau ein Aufruf mit Payee und aktueller Kategorie.
  await page.waitForFunction(() => true);
  for (let i = 0; i < 50 && state.suggestionCalls.length === 0; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.equal(state.suggestionCalls.length, 1, "ein Aufruf je Öffnung");
  assert.deepEqual(state.suggestionCalls[0], ["REWE Mitte", { YnabCategoryId: CAT.lebensmittel.id }]);
  assert.match(await listbox.textContent(), /Vorgeschlagen/i, "Abschnitt VORGESCHLAGEN");

  // Das Popover liegt ganz im Panel, nicht abgeschnitten.
  const geometry = await page.evaluate(() => {
    const aside = document.querySelector('[data-testid="ledger-detail-panel"]').getBoundingClientRect();
    const box = document.querySelector('[data-testid="ledger-detail-panel"] [role="listbox"]').closest(".absolute").getBoundingClientRect();
    return { asideLeft: aside.left, asideRight: aside.right, left: box.left, right: box.right };
  });
  assert.ok(geometry.left >= geometry.asideLeft - 0.5 && geometry.right <= geometry.asideRight + 0.5, `Popover (${geometry.left}..${geometry.right}) im Panel (${geometry.asideLeft}..${geometry.asideRight})`);

  await listbox.locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  await page.locator('[data-testid="category-change-card"]').waitFor();
  assert.deepEqual(state.setCalls.map((c) => c.choice), [{ YnabCategoryId: CAT.nebenkosten.id }]);
  assert.equal(await panel(page).locator('[role="listbox"]').count(), 0, "Picker zu");
  assert.match(await categoryRow(panel(page)).textContent(), /Wohnen: Nebenkosten/, "Detail zeigt die neue Kategorie");
  assert.match(await row(page, "REWE Mitte").textContent(), /Wohnen: Nebenkosten/, "Liste ebenso");
  // Die Karte steht unter dem Kategorie-Feld, mit durchgestrichenem altem Wert.
  const card = panel(page).locator('[data-testid="category-change-card"]');
  const order = await page.evaluate(() => {
    const row = document.querySelector('[data-testid="ledger-detail-panel"] [data-testid="detail-category"]').getBoundingClientRect();
    const card = document.querySelector('[data-testid="category-change-card"]').getBoundingClientRect();
    return { rowBottom: row.bottom, cardTop: card.top };
  });
  assert.ok(order.cardTop >= order.rowBottom - 1, "Karte unter dem Kategorie-Feld");
  assert.match(await card.textContent(), /Budget nach dem Wechsel/);
  assert.equal(await card.locator(".line-through").count(), 2, "alter Wert je Zeile durchgestrichen");
  assert.match(await card.textContent(), /120,50/, "alter Wert der alten Kategorie");
  // Toast mit Rückgängig, ohne Beträge.
  const toastText = page.getByText("Kategorie geändert: Nebenkosten", { exact: true });
  await toastText.waitFor();
  assert.doesNotMatch(await toastText.textContent(), /\d+,\d\d/, "keine Beträge im Toast");
  await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
  for (let i = 0; i < 50 && state.setCalls.length < 2; i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.deepEqual(state.setCalls[1].choice, { YnabCategoryId: CAT.lebensmittel.id }, "Rückgängig sendet die alte Kategorie");
  await page.waitForFunction(() => /Alltag: Lebensmittel/.test(document.querySelector('[data-testid="detail-category"]')?.textContent ?? ""));
  assert.equal(await page.locator('[data-testid="category-change-card"]').count(), 0, "Rücknahme zeigt keine neue Karte");
  await context.close();
});

test("PC: Esc schließt zuerst den Picker, dann das Detail; ein zweiter Klick auf den Pfeil schließt den Picker", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').waitFor();
  await page.keyboard.press("Escape");
  await panel(page).locator('[role="listbox"]').waitFor({ state: "detached" });
  assert.equal(await panel(page).count(), 1, "das Detail bleibt");
  // Ein Klick auf die Zeile (nicht nur auf den Pfeil) öffnet, ein zweiter schließt.
  await categoryRow(panel(page)).locator("span").first().click();
  await panel(page).locator('[role="listbox"]').waitFor();
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').waitFor({ state: "detached" });
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').waitFor();
  await page.keyboard.press("Escape");
  await page.keyboard.press("Escape");
  await panel(page).waitFor({ state: "detached" });
  await context.close();
});

test("PC: Gesperrte Fälle (Übertrag ohne Kategorie, gelöscht, Split) zeigen den Pfeil gedämpft mit Hinweistext und öffnen nichts", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  const cases = [
    ["Girokonto Übertrag", /Übertrag zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu/],
    ["Café Lotte", /In YNAB gelöscht/],
    ["Kaufland Split", /Split bearbeiten folgt/],
  ];
  for (const [payee, hint] of cases) {
    await row(page, payee).locator("td").nth(2).click();
    await page.waitForFunction((p) => document.querySelector('[data-testid="detail-title"]')?.textContent === p, payee);
    const button = arrow(panel(page));
    assert.equal(await button.getAttribute("aria-disabled"), "true", `${payee}: gesperrt`);
    assert.match(await button.getAttribute("title"), hint, `${payee}: Hinweistext`);
    await button.click({ force: true });
    await categoryRow(panel(page)).click({ position: { x: 20, y: 10 } });
    assert.equal(await panel(page).locator('[role="listbox"]').count(), 0, `${payee}: Picker bleibt zu`);
  }
  assert.equal(state.suggestionCalls.length, 0, "keine Abfrage für gesperrte Fälle");
  await context.close();
});

test("PC: Schlägt der Versand fehl, steht die Hinweisleiste mit Grund und Erneut senden; nach dem Neuladen noch da", async () => {
  resetState();
  state.writeBack = { CategorySendFailed: "YNAB nicht erreichbar" };
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "Deutsche Bahn").locator("td").nth(2).click();
  await panel(page).waitFor();
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  state.failureOf[2] = "YNAB nicht erreichbar";
  const notice = panel(page).locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  assert.match(await notice.textContent(), /Kategorie nicht in YNAB/);
  assert.match(await notice.textContent(), /YNAB nicht erreichbar/);
  await notice.getByRole("button", { name: "Erneut senden" }).click();
  await notice.waitFor({ state: "detached" });
  assert.equal(state.retryCalls.length, 1, "retryCategorizationWriteBack lief");

  // Nach einem Neuladen der Seite steht der offene Fehlschlag weiter an der Buchung.
  state.failureOf[2] = "YNAB nicht erreichbar";
  await page.reload({ waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await row(page, "Deutsche Bahn").locator("td").nth(2).click();
  await page.locator('[data-testid="detail-notice"]', { hasText: "Kategorie nicht in YNAB" }).waitFor();
  await context.close();
});

test("PC: Fällt die Zeile aus dem Kategorie-Filter, bleibt sie ausgegraut mit Vermerk stehen; Trefferzahl geht sofort mit", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  await panel(page).locator('[data-testid="detail-category"] [data-testid="detail-filter"]').click();
  await page.waitForFunction(() => document.querySelectorAll("table tbody tr").length <= 3);
  const before = state.queries.length;
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  const note = page.locator('tr[data-testid="filtered-out-note"]');
  await note.waitFor();
  assert.match(await note.textContent(), /passt nicht mehr zum Filter/);
  const opacity = await row(page, "REWE Mitte").evaluate((el) => getComputedStyle(el).opacity);
  assert.ok(Number(opacity) < 1, `die Zeile ist gedämpft (${opacity})`);
  assert.equal(state.queries.length, before, "kein Neuladen der Liste");
  assert.match(await page.locator("body").innerText(), /\b0 Buchungen\b/, "Trefferzahl sofort gesunken");
  // Die ausgegraute Zeile ist von "alle auswählen" ausgenommen.
  await page.getByRole("checkbox", { name: "Alle Buchungen des Treffers auswählen" }).filter({ visible: true }).click();
  const checked = await row(page, "REWE Mitte").locator('[role="checkbox"]').first().getAttribute("aria-checked");
  assert.notEqual(checked, "true", "ausgegraut: nicht gewählt");
  await context.close();
});

test("PC: Bei Sortierung nach Kategorie erscheint die Leiste Neu laden, Neu laden lädt ab Seite 1", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  await page.locator("table thead th", { hasText: "Kategorie" }).first().getByText("Kategorie").click();
  for (let i = 0; i < 50 && !state.queries.some((q) => q.Sort.Field === "SortByCategory"); i += 1) await new Promise((r) => setTimeout(r, 40));
  assert.ok(state.queries.some((q) => q.Sort.Field === "SortByCategory"), "die Sortierung nach Kategorie ist aktiv");
  await row(page, "REWE Mitte").locator("td").nth(2).click();
  await panel(page).waitFor();
  assert.equal(await page.locator('[data-testid="order-stale-bar"]').count(), 0, "vorher keine Leiste");
  await arrow(panel(page)).click();
  await panel(page).locator('[role="listbox"]').locator('[role="option"]', { hasText: "Nebenkosten" }).last().click();
  const bar = page.locator('[data-testid="order-stale-bar"]');
  await bar.waitFor();
  assert.match(await bar.textContent(), /Reihenfolge oder Treffer haben sich geändert\./);
  const before = state.queries.length;
  await bar.getByRole("button", { name: "Neu laden" }).click();
  await bar.waitFor({ state: "detached" });
  const last = state.queries[state.queries.length - 1];
  assert.ok(state.queries.length > before, "Neu laden fragt den Server");
  assert.equal(last.Page, 0, "ab Seite 1");
  assert.equal(last.Sort.Field, "SortByCategory");
  await context.close();
});

test("Handy (390x844): Der Picker öffnet als zweites Sheet über dem Detail, die Karte steht unter dem Kategorie-Feld", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const page = await open(context);
  await page.getByText("REWE Mitte").first().tap();
  const detailSheet = page.locator(".bottom-sheet.active").first();
  await detailSheet.waitFor();
  await arrow(detailSheet).tap();
  const pickerSheet = page.locator(".bottom-sheet.active", { hasText: "Kategorie wählen" });
  await pickerSheet.waitFor();
  assert.equal(await page.locator(".bottom-sheet.active").count(), 2, "beide Sheets stehen");
  const z = await page.evaluate(() => {
    const sheets = [...document.querySelectorAll(".bottom-sheet.active")];
    const rank = (el) => {
      let node = el;
      while (node && node !== document.body) {
        const z = parseInt(getComputedStyle(node).zIndex, 10);
        if (!Number.isNaN(z)) return z;
        node = node.parentElement;
      }
      return 0;
    };
    return sheets.map((s) => ({ text: s.textContent.includes("Kategorie wählen"), z: rank(s), top: s.getBoundingClientRect().top }));
  });
  const picker = z.find((s) => s.text);
  const detail = z.find((s) => !s.text);
  assert.ok(picker.z > detail.z, `Picker liegt über dem Detail (${picker.z} gegen ${detail.z})`);
  await pickerSheet.locator('[role="option"]', { hasText: "Nebenkosten" }).last().tap();
  await page.locator('[data-testid="category-change-card"]').waitFor();
  const order = await page.evaluate(() => {
    const row = document.querySelector('.bottom-sheet.active [data-testid="detail-category"]').getBoundingClientRect();
    const card = document.querySelector('.bottom-sheet.active [data-testid="category-change-card"]').getBoundingClientRect();
    return { rowBottom: row.bottom, cardTop: card.top };
  });
  assert.ok(order.cardTop >= order.rowBottom - 1, "Karte unter dem Kategorie-Feld im Detail-Sheet");
  assert.equal(await page.locator(".bottom-sheet.active", { hasText: "Kategorie wählen" }).count(), 0, "Picker-Sheet zu");
  await context.close();
});
