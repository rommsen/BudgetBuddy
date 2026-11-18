// scripts/ledger-filter-bar.test.mjs
//
// Die Filterleiste des Kassenbuchs im echten Fable-Client gegen gemockte Antworten
// (`scripts/ledger-filter-bar.mjs`). Hier steht nur, was der Browser allein messen kann:
// der Happy Path am Handy (Filter-Sheet und Chips, ledger-d9z3n), die Datumsrechnung im Fable-Kompilat und der
// native Kalender am PC.
// Sheet am Handy, Feldschrift und die sticky Leiste prüft der Layout-Sweep (`scripts/layout-states.mjs`,
// Zustände kassenbuch, kassenbuch-filter-*, kassenbuch-spiegel-*, kassenbuch-gefiltert). Entwurf,
// Übernehmen, Sperren, Summenzeile, Leerzustand und Chip-Reihenfolge prüft `dotnet test`
// (`LedgerPageTests.fs`, `LedgerViewTests.fs`, `LedgerReviewStateTests.fs`).

import { test, before, after, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;

/// Öffnet das Kassenbuch in einem frischen Kontext gegen die Filterleisten-Mocks.
async function openLedger(viewport, options = {}) {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const context = await browser.newContext({ viewport, ...options });
  const target = await context.newPage();
  target.setDefaultTimeout(15000);
  await target.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(target, {
    accountsFixture,
    balancesFixture,
  });
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return { context, page: target };
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  ({ page } = await openLedger({ width: 1440, height: 900 }));
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

// Wartet, bis ein Textfeld den erwarteten Wert zeigt (React rendert nach dem Klick nach).
async function waitForValue(locator, expected) {
  await locator.page().waitForFunction(
    ([el, value]) => el.value === value,
    [await locator.elementHandle(), expected]
  );
}

const categoryChip = () => page.getByRole("button", { name: /^Kategorie/ }).first();
// `.shadow-2xl`: die einzige Klasse aus `FilterPopover.view`, die auf der Seite sonst nirgends vorkommt.
const popover = () => page.locator(".shadow-2xl");

// Jeder Testfall startet ohne Filter und ohne offene Chrome.
beforeEach(async () => {
  const clearAll = page.getByRole("button", { name: "Alle Filter löschen" });
  if (await clearAll.count()) await clearAll.click();
  if (await popover().count()) await page.getByRole("heading", { name: "Kassenbuch" }).click();
  await popover().waitFor({ state: "detached" });
});

// ledger-d9z3n: unter 1024 px öffnet der Knopf „Filter“ ein Sheet; gesetzte Filter stehen als Chips mit „×“.
test("[flow:kassenbuch-filtern] Handy: Filter-Sheet, Kategorien wählen, „n zeigen“, Chip mit „×“ nimmt den Filter zurück", async () => {
  const { context, page: phone } = await openLedger({ width: 412, height: 915 }, { hasTouch: true, isMobile: true });
  try {
    const active = () => phone.locator(".bottom-sheet.active");
    await phone.getByText("Stadtwerke Musterstadt", { exact: true }).first().waitFor();
    assert.equal(await phone.getByTestId("ledger-active-filters").count(), 0, "ohne Filter keine Chip-Zeile");

    await phone.getByRole("button", { name: "Filter", exact: true }).click();
    await active().getByTestId("ledger-filter-sheet").waitFor();

    await active().getByTestId("filter-category").click();
    await active().getByText("Lebensmittel", { exact: true }).last().waitFor();
    await active().getByText("Lebensmittel", { exact: true }).last().click();
    await active().getByText("Bahn", { exact: true }).last().click();
    await active().getByRole("button", { name: "Übernehmen (2)" }).click();

    // Zurück im Filter-Sheet: die Liste ist schon gefiltert, „3 zeigen“ schließt.
    await active().getByTestId("ledger-filter-sheet").waitFor();
    await active().getByRole("button", { name: "3 zeigen" }).click();
    await phone.waitForFunction(() => document.querySelectorAll(".bottom-sheet.active").length === 0);

    const chips = phone.getByTestId("ledger-active-filters");
    assert.match(await chips.innerText(), /Kategorie: 2/);
    assert.equal(await phone.getByRole("button", { name: "Filter (1)", exact: true }).count(), 1, "Filter-Knopf mit Zahl");
    await phone.getByText("REWE Mitte", { exact: true }).first().waitFor();
    assert.equal(await phone.getByText("Stadtwerke Musterstadt", { exact: true }).count(), 0, "Nebenkosten fällt heraus");

    await phone.getByRole("button", { name: "Kategorie: 2 entfernen" }).click();
    await phone.getByText("Stadtwerke Musterstadt", { exact: true }).first().waitFor();
    assert.equal(await phone.getByTestId("ledger-active-filters").count(), 0, "die Chip-Zeile ist weg");
  } finally {
    await context.close();
  }
});

// Datum und ISO-Text laufen hier durch das Fable-Kompilat, unter .NET rechnet eine andere Laufzeit.
test("[fable] PC: Zeitraum mit Schnellwahl, ungültigem Datum und Eingabe geht als ISO-Datum an den Server und kommt deutsch zurück", async () => {
  const queries = [];
  const spy = (route) => {
    queries.push(route.request().postDataJSON()[0]);
    route.fallback();
  };
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", spy);
  try {
    await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
    const dateInputs = popover().getByPlaceholder("TT.MM.JJJJ");
    await popover().getByRole("button", { name: "Dieser Monat" }).click();
    await page.waitForFunction(() => /^01\.\d\d\.\d{4}$/.test(document.querySelector('.shadow-2xl input[placeholder="TT.MM.JJJJ"]')?.value ?? ""));

    await dateInputs.nth(0).fill("31.02.2026");
    assert.ok(await popover().getByText("Datum als TT.MM.JJJJ").isVisible(), "der Hinweis steht da");
    assert.ok(await popover().getByRole("button", { name: "Übernehmen" }).isDisabled(), "Übernehmen ist gesperrt");
    assert.equal(await dateInputs.nth(0).inputValue(), "31.02.2026", "der Text bleibt beim Tippen stehen");

    await popover().getByRole("button", { name: "Zurücksetzen" }).click();
    await waitForValue(dateInputs.nth(0), "");
    await dateInputs.nth(0).fill("01.10.2026");
    await dateInputs.nth(1).fill("15.10.2026");
    await popover().getByRole("button", { name: "Übernehmen" }).click();
    await popover().waitFor({ state: "detached" });

    await pollUntil(() => queries.some((q) => q.DateFrom));
    const filtered = queries.find((q) => q.DateFrom);
    assert.ok(filtered, "die Liste wird mit Zeitraum nachgeladen");
    assert.match(filtered.DateFrom, /^2026-10-01/, `Von: ${filtered.DateFrom}`);
    assert.match(filtered.DateTo, /^2026-10-15/, `Bis: ${filtered.DateTo}`);

    await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
    await waitForValue(popover().getByPlaceholder("TT.MM.JJJJ").nth(0), "01.10.2026");
    await waitForValue(popover().getByPlaceholder("TT.MM.JJJJ").nth(1), "15.10.2026");
    await page.keyboard.press("Escape");
  } finally {
    await page.unroute("**/api/LedgerApi/getLedgerEntriesPage", spy);
  }
});

test("[fokus] Der Kalender-Knopf wirft keinen Konsolenfehler und lässt das Textfeld bedienbar (m7z9q)", async () => {
  const errors = [];
  const onConsole = (m) => { if (m.type() === "error") errors.push(m.text()); };
  const onPageError = (e) => errors.push(String(e));
  page.on("console", onConsole);
  page.on("pageerror", onPageError);

  await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
  await popover().getByRole("button", { name: "Von: Kalender öffnen" }).click();
  await page.keyboard.press("Escape");
  const von = popover().getByPlaceholder("TT.MM.JJJJ").nth(0);
  await von.fill("03.04.2026");
  assert.equal(await von.inputValue(), "03.04.2026", "Das Textfeld bleibt bedienbar");

  page.off("console", onConsole);
  page.off("pageerror", onPageError);
  const relevant = errors.filter((e) => !/not mocked in this test|Failed to load resource/.test(e));
  assert.deepEqual(relevant, [], "Kein Konsolenfehler");
  await page.keyboard.press("Escape");
});
