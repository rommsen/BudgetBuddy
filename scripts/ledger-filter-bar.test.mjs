// scripts/ledger-filter-bar.test.mjs
//
// Die Filterleiste des Kassenbuchs im echten Fable-Client gegen gemockte Antworten
// (`scripts/ledger-filter-bar.mjs`). Hier steht nur, was der Browser allein messen kann:
// der Happy Path am PC (Popover), die Datumsrechnung im Fable-Kompilat und der native Kalender.
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
async function openLedger(viewport) {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const context = await browser.newContext({ viewport });
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
// Die Handy-Liste bleibt per `md:hidden` im DOM, Payee-Texte kämen sonst doppelt vor.
const table = () => page.locator("table");

// Jeder Testfall startet ohne Filter und ohne offene Chrome.
beforeEach(async () => {
  const clearAll = page.getByRole("button", { name: "Alle Filter löschen" });
  if (await clearAll.count()) await clearAll.click();
  if (await popover().count()) await page.getByRole("heading", { name: "Kassenbuch" }).click();
  await popover().waitFor({ state: "detached" });
});

test("[flow:kassenbuch-filtern] PC: Kategorien im Popover wählen, Übernehmen filtert Liste und Summe, × nimmt den Filter zurück", async () => {
  await categoryChip().click();
  await popover().getByText("Lebensmittel", { exact: true }).last().waitFor();
  assert.ok((await popover().innerText()).includes("120,50"), "Available steht neben der Kategorie");
  assert.equal(await popover().getByText("Umzug 2023", { exact: true }).count(), 0, "die versteckte Kategorie ist eingeklappt");
  await popover().getByText("Versteckt (1)", { exact: true }).click();
  assert.ok(await popover().getByText("Umzug 2023", { exact: true }).isVisible(), "nach dem Antippen ist sie erreichbar");

  await popover().getByText("Lebensmittel", { exact: true }).last().click();
  await popover().getByText("Bahn", { exact: true }).last().click();
  assert.equal(await categoryChip().innerText(), "Kategorie", "vor Übernehmen ändert sich der Chip nicht");
  await popover().getByRole("button", { name: "Übernehmen (2)" }).click();
  await popover().waitFor({ state: "detached" });
  assert.equal(await categoryChip().innerText(), "Kategorie: 2");

  await table().getByText("REWE Mitte", { exact: true }).waitFor();
  assert.equal(await table().getByText("Stadtwerke Musterstadt", { exact: true }).count(), 0, "Nebenkosten fällt heraus");
  assert.ok((await page.getByTestId("ledger-summary-text").innerText()).startsWith("3 Buchungen"), "Trefferzahl");
  assert.match(await page.getByTestId("ledger-summary-text").locator("xpath=..").innerText(), /36,00/, "Summe über den Treffer");

  await page.getByRole("button", { name: "Kategorie: 2 entfernen" }).click();
  await table().getByText("Stadtwerke Musterstadt", { exact: true }).waitFor();
  assert.equal(await categoryChip().innerText(), "Kategorie");
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
