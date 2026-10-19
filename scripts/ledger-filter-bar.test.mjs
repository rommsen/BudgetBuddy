// scripts/ledger-filter-bar.test.mjs
//
// Gerenderte Belege für `ledger-j8xq4` ("Kassenbuch-Filterleiste und Summenzeile:
// Chips, Kategorienauswahl mit Mehrfachauswahl, Popover am PC"): ein echter, per
// Vite gestarteter Fable-Client (echtes Tailwind-CSS, echtes kompiliertes Bundle)
// gegen gemockte Ledger-/Settings-/YNAB-API-Antworten (`scripts/ledger-filter-bar.mjs`,
// rein erfundene Werte). Kein Backend nötig: Playwright fängt jede Anfrage ab, bevor
// sie das Netzwerk erreicht.
//
// PC-Viewport (1440x900): prüft das Popover-Muster (AC "Popover unter seinem
// Chip", "Esc schließt ohne zu filtern", Click-Commit, Summenzeile, Leerzustand,
// "×" und "Alle Filter löschen"). Die Chip-Reihenfolge selbst
// (`orderChipsForDisplay`, "aktive Chips wandern am Handy nach vorn") ist bereits
// über den reinen Reduzierer in `dotnet test` belegt (`LedgerViewTests.fs`), hier
// nicht erneut per Screenshot geprüft, siehe OUTCOME.
//
// Handy-Viewport (390x844, zweite Iteration, ledger-j8xq4): ein zweiter
// Browser-Kontext gegen denselben laufenden Frontend-Server prüft die andere
// Chrome-Hälfte, die der PC-Kontext strukturell nie erreichen kann
// (`filterChrome` entscheidet über `Viewport.isDesktopViewport()`, ADR 0043):
// der Kategorie-Chip öffnet ein `BottomSheet`, kein `FilterPopover`, zwei
// übernommene Kategorien zeigen dieselbe Vereinigung wie am PC, und der aktive
// Chip steht danach im DOM vor "Zu prüfen" (`orderChipsForDisplay`, "aktive
// Chips wandern am Handy nach vorn").
//
// Alle Texte, die auch als Kategorie-Kennzeichnung in der (am Handy sichtbaren,
// hier per `md:hidden` nur DOM-versteckten) Buchungsliste vorkommen können
// ("Lebensmittel", "Bahn", "Nebenkosten", "Apotheke"), werden im Popover über
// `.shadow-2xl` (`FilterPopover.view`s eigene Klasse, sonst nirgends auf der
// Kassenbuch-Seite verwendet) skopiert, sonst verletzt `getByText(...)` Playwrights
// Strict Mode (zwei Treffer: Popover-Zeile UND versteckte Handy-Zeile).
//
// Ausführen: node --test scripts/ledger-filter-bar.test.mjs
// (Teil von `npm run test:scripts`, das alle scripts/*.test.mjs läuft.)

import { test, before, after, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
  mockRunningMirror,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;
let mobilePage;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);

  browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  page = await context.newPage();
  page.setDefaultTimeout(15000);

  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, { accountsFixture, balancesFixture });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");

  // Zweiter Kontext, derselbe Fixture, derselbe laufende Vite-Server: eigene
  // Viewport-Breite (390x844) reicht, `Viewport.isDesktopViewport()` liest bei
  // jedem Render `window.matchMedia("(min-width: 768px)")` neu, kein eigener
  // Frontend-Prozess nötig.
  const mobileContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
  mobilePage = await mobileContext.newPage();
  mobilePage.setDefaultTimeout(15000);

  await mobilePage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(mobilePage, { accountsFixture, balancesFixture });

  await mobilePage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(mobilePage, "/ledger");
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

// Wartet, bis ein Textfeld den erwarteten Wert zeigt (React rendert nach dem Klick nach).
async function waitForValue(locator, expected) {
  const deadline = Date.now() + 5000;
  let last;
  while (Date.now() < deadline) {
    last = await locator.inputValue();
    if (last === expected) return;
    await new Promise((r) => setTimeout(r, 50));
  }
  assert.equal(last, expected, "Wert im Feld");
}

const categoryChip = () => page.getByRole("button", { name: /^Kategorie/ }).first();
// `.shadow-2xl`: die einzige Klasse aus `FilterPopover.view`, die auf der
// Kassenbuch-Seite sonst nirgends vorkommt (Modal.fs teilt sie sich, aber kein
// Modal ist hier je offen).
const popover = () => page.locator(".shadow-2xl");
// Die PC-Tabelle: die Handy-Liste (`entryListMobile`) bleibt per `md:hidden` immer
// im DOM (nur CSS-versteckt), Payee-Texte kämen sonst doppelt vor.
const table = () => page.locator("table");

// Handy-Gegenstücke (zweiter Kontext, `mobilePage`, siehe `before` oben).
const mobileCategoryChip = () => mobilePage.getByRole("button", { name: /^Kategorie/ }).first();
const mobileNeedsReviewChip = () => mobilePage.getByRole("button", { name: /^Zu prüfen/ }).first();
// `BottomSheet.view` steht immer im DOM (Sicht/Chrome-Umschaltung nur über die
// `active`-Klasse, siehe `DesignSystem/BottomSheet.fs`), anders als das
// `FilterPopover`, das beim Schließen tatsächlich unmountet (`Html.none`):
// `.bottom-sheet.active` als Klassen-Selektor verhält sich für `waitFor`
// trotzdem wie ein "detached, wenn zu"-Locator, weil er ohne die Klasse auf
// keinem Element matcht.
const mobileSheet = () => mobilePage.locator(".bottom-sheet.active");

// Jeder Testfall startet vom unveränderten Ausgangszustand: kein Filter aktiv,
// keine Chrome offen. Statt eines vollen Seiten-Reloads je Test (langsam) räumt
// dies über die App selbst auf, falls ein vorheriger Test einen Filter gesetzt hat.
beforeEach(async () => {
  const clearAll = page.getByRole("button", { name: "Alle Filter löschen" });
  if (await clearAll.count()) {
    await clearAll.click();
  }
  if (await popover().count()) {
    // Ein offenes Popover schließen (Klick auf den Seitentitel, weit weg von jedem Chip).
    await page.getByRole("heading", { name: "Kassenbuch" }).click();
  }
  await popover().waitFor({ state: "detached" });

  const mobileClearAll = mobilePage.getByRole("button", { name: "Alle Filter löschen" });
  if (await mobileClearAll.count()) {
    await mobileClearAll.click();
  }
  if (await mobileSheet().count()) {
    // Oben links auf das Overlay tippen, sicher außerhalb des von unten
    // hochgezogenen Sheets (kein Escape-Handling im BottomSheet).
    await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  }
  await mobileSheet().waitFor({ state: "detached" });
});

test("Die Kategorienauswahl öffnet als Popover unter dem Chip, mit den geladenen Kategorien (AC5)", async () => {
  await categoryChip().click();
  await popover().getByText("Lebensmittel", { exact: true }).waitFor();
  assert.ok(await popover().getByText("Bahn", { exact: true }).isVisible(), "Bahn steht in der Liste");
  assert.ok(await popover().getByText("Nebenkosten", { exact: true }).isVisible(), "Nebenkosten steht in der Liste");

  // AC5: Available rechtsbündig, wenn vorhanden (Lebensmittel: 120,50 €), sonst leer (Bahn: kein Available in der Fixture).
  const lebensmittelText = await popover().innerText();
  assert.ok(lebensmittelText.includes("120,50"), `Available muss neben der Kategorie stehen, war: ${lebensmittelText}`);

  // AC3: versteckte Kategorie ist eingeklappt, "Umzug 2023" ist NICHT direkt sichtbar.
  assert.equal(await popover().getByText("Umzug 2023", { exact: true }).count(), 0, "Versteckte Kategorie ist eingeklappt");
  assert.ok(await popover().getByText("Versteckt (1)", { exact: true }).isVisible(), "Der Einklapp-Hinweis zeigt die Zahl");
  await popover().getByText("Versteckt (1)", { exact: true }).click();
  assert.ok(await popover().getByText("Umzug 2023", { exact: true }).isVisible(), "Nach dem Antippen ist sie erreichbar");

  await page.keyboard.press("Escape");
});

test("Click-Commit: Antippen wirkt erst auf 'Übernehmen', der Filter bleibt bis dahin unverändert (AC)", async () => {
  await categoryChip().click();
  await popover().getByText("Lebensmittel", { exact: true }).click();
  assert.ok(await popover().getByRole("button", { name: "Übernehmen (1)" }).isVisible(), "Der Zähler steht auf 1");
  // Vor 'Übernehmen' bleibt der Chip inaktiv (Filter noch nicht committet).
  assert.equal(await categoryChip().innerText(), "Kategorie", "Der Chip zeigt noch keinen Zähler");
  await page.keyboard.press("Escape");
  assert.equal(await categoryChip().innerText(), "Kategorie", "Escape hat nichts committet");
});

test("Die Zeitraum-Schnellwahl trägt Von und Bis deutsch in die Datumsfelder ein (AC)", async () => {
  const dateChip = page.getByRole("button", { name: /^Zeitraum/ }).first();
  await dateChip.click();
  await popover().getByRole("button", { name: "12 Monate" }).waitFor();

  const dateInputs = popover().getByPlaceholder("TT.MM.JJJJ");
  assert.equal(await dateInputs.nth(0).inputValue(), "", "Von ist zu Beginn leer");
  assert.equal(await dateInputs.nth(1).inputValue(), "", "Bis ist zu Beginn leer");

  await popover().getByRole("button", { name: "Dieser Monat" }).click();

  // "Dieser Monat" läuft vom Monatsanfang bis heute (`quickDateRange`). Das Feld zieht
  // den Text nach dem Render nach, deshalb warten statt sofort lesen.
  const now = new Date();
  const pad = (n) => String(n).padStart(2, "0");
  const expectedVon = `01.${pad(now.getMonth() + 1)}.${now.getFullYear()}`;
  const expectedBis = `${pad(now.getDate())}.${pad(now.getMonth() + 1)}.${now.getFullYear()}`;
  await waitForValue(dateInputs.nth(0), expectedVon);
  await waitForValue(dateInputs.nth(1), expectedBis);

  await page.keyboard.press("Escape");
});

test("Ungültiges Datum sperrt Übernehmen und zeigt den Hinweis; Zurücksetzen hebt es auf (m7z9q)", async () => {
  await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
  const dateInputs = popover().getByPlaceholder("TT.MM.JJJJ");
  await dateInputs.nth(0).fill("31.02.2026");

  assert.ok(await popover().getByText("Datum als TT.MM.JJJJ").isVisible(), "Der Hinweis steht da");
  assert.ok(await popover().getByRole("button", { name: "Übernehmen" }).isDisabled(), "Übernehmen ist gesperrt");
  assert.equal(await dateInputs.nth(0).inputValue(), "31.02.2026", "Der Text bleibt beim Tippen stehen");

  await popover().getByRole("button", { name: "Zurücksetzen" }).click();
  assert.equal(await popover().getByText("Datum als TT.MM.JJJJ").count(), 0, "Der Hinweis ist weg");
  await waitForValue(dateInputs.nth(0), "");
  assert.ok(await popover().getByRole("button", { name: "Übernehmen" }).isEnabled(), "Übernehmen ist frei");

  await page.keyboard.press("Escape");
});

test("Von und Bis eintippen und Übernehmen filtert die Liste auf den Zeitraum (m7z9q, PC)", async () => {
  // Die zuletzt registrierte Route gewinnt; `fallback()` reicht an den Mock der Filterleiste weiter.
  const queries = [];
  const spy = (route) => {
    queries.push(route.request().postDataJSON()[0]);
    route.fallback();
  };
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", spy);
  try {
    await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
    const dateInputs = popover().getByPlaceholder("TT.MM.JJJJ");
    await dateInputs.nth(0).fill("01.10.2026");
    await dateInputs.nth(1).fill("15.10.2026");
    await popover().getByRole("button", { name: "Übernehmen" }).click();
    await popover().waitFor({ state: "detached" });

    const deadline = Date.now() + 5000;
    while (!queries.some((q) => q.DateFrom) && Date.now() < deadline) {
      await new Promise((r) => setTimeout(r, 50));
    }
    const filtered = queries.find((q) => q.DateFrom);
    assert.ok(filtered, "Die Liste wird mit Zeitraum nachgeladen");
    assert.match(filtered.DateFrom, /^2026-10-01/, `Von: ${filtered.DateFrom}`);
    assert.match(filtered.DateTo, /^2026-10-15/, `Bis: ${filtered.DateTo}`);

    // Wieder geöffnet zeigt der Entwurf den übernommenen Zeitraum deutsch.
    await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
    await waitForValue(popover().getByPlaceholder("TT.MM.JJJJ").nth(0), "01.10.2026");
    await waitForValue(popover().getByPlaceholder("TT.MM.JJJJ").nth(1), "15.10.2026");
    await page.keyboard.press("Escape");
  } finally {
    await page.unroute("**/api/LedgerApi/getLedgerEntriesPage", spy);
  }
});

test("Die Datumsfelder haben am Handy 16px, am PC 14px Schrift (iOS-Zoom, m7z9q)", async () => {
  const size = (loc) => loc.evaluate((e) => getComputedStyle(e).fontSize);

  await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
  assert.equal(await size(popover().getByPlaceholder("TT.MM.JJJJ").first()), "14px", "PC: 14px");
  await page.keyboard.press("Escape");

  await mobilePage.getByRole("button", { name: /^Zeitraum/ }).first().click();
  const field = mobileSheet().getByPlaceholder("TT.MM.JJJJ").first();
  await field.waitFor();
  assert.equal(await size(field), "16px", "Handy: 16px");
  await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
});

test("Input.Small misst am Handy 16px und am PC 14px, Medium und Large bleiben bei 16px (btwhg)", async () => {
  // Misst die Kaskade von `.input-field` gegen die Klassen aus `Input.sizeToClass`:
  // `.input-field` stand ohne Layer in styles.css und überstimmte `text-base` von Small.
  // Medium und Large tragen zusätzlich das Inline-fontSize 16 (`Input.usesInlineFontSize`).
  const measure = (p) =>
    p.evaluate(() => {
      const make = (classes, inline) => {
        const el = document.createElement("input");
        el.className = "input-field " + classes;
        if (inline) el.style.fontSize = "16px";
        document.body.appendChild(el);
        const size = getComputedStyle(el).fontSize;
        el.remove();
        return size;
      };
      return {
        small: make("text-base md:text-sm px-3 py-2 min-h-[36px]", false),
        medium: make("text-base px-4 py-3 min-h-[48px] md:min-h-[44px]", true),
        large: make("text-lg px-4 py-3.5 min-h-[52px]", true),
      };
    });
  assert.deepEqual(await measure(page), { small: "14px", medium: "16px", large: "16px" }, "PC 1440x900");
  assert.deepEqual(await measure(mobilePage), { small: "16px", medium: "16px", large: "16px" }, "Handy 390x844");

  // Ein echtes Input.Small-Feld: die Kategorie-Suche im Popover am PC.
  await categoryChip().click();
  const search = popover().getByPlaceholder("Kategorie suchen…");
  assert.equal(await search.evaluate((e) => getComputedStyle(e).fontSize), "14px", "Kategorie-Suche PC: 14px");
  await page.keyboard.press("Escape");

  // Dasselbe echte Feld am Handy (BottomSheet): 16px gegen den iOS-Zoom (ADR 0005).
  await mobileCategoryChip().click();
  const mobileSearch = mobileSheet().getByPlaceholder("Kategorie suchen…");
  await mobileSearch.waitFor();
  assert.equal(await mobileSearch.evaluate((e) => getComputedStyle(e).fontSize), "16px", "Kategorie-Suche Handy: 16px");
  await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
});

test("Der Kalender-Knopf wirft keinen Konsolenfehler und lässt das Textfeld bedienbar (m7z9q)", async () => {
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

test("Zwei Kategorien gewählt und übernommen zeigen die Vereinigung beider Kategorien in der Liste und der Summenzeile (AC)", async () => {
  assert.equal(await page.getByText(/^\d+ Buchungen$/).count(), 0, "Ohne aktiven Filter steht keine Summenzeile");

  await categoryChip().click();
  await popover().getByText("Lebensmittel", { exact: true }).click();
  await popover().getByText("Bahn", { exact: true }).click();
  await popover().getByRole("button", { name: "Übernehmen (2)" }).click();

  assert.equal(await categoryChip().innerText(), "Kategorie: 2", "Der Chip zeigt die Anzahl (AC 'Kategorie: 2')");
  await popover().waitFor({ state: "detached" });

  // Vereinigung: REWE Mitte + Bäckerei Krause (Lebensmittel) + Deutsche Bahn (Bahn) = 3,
  // Stadtwerke Musterstadt (Nebenkosten) fällt raus.
  await table().getByText("REWE Mitte", { exact: true }).waitFor();
  assert.ok(await table().getByText("Deutsche Bahn", { exact: true }).isVisible(), "Bahn-Treffer ist da");
  assert.equal(await table().getByText("Stadtwerke Musterstadt", { exact: true }).count(), 0, "Nebenkosten ist NICHT im Treffer");

  assert.ok(await page.getByText("3 Buchungen", { exact: true }).isVisible(), "Trefferzahl über den ganzen Treffer (AC)");
  // Summe: -(10+1) + -(10+2) + -(10+3) = -36,00 €.
  const summaryText = await page.getByText("3 Buchungen", { exact: true }).locator("xpath=..").innerText();
  assert.match(summaryText, /36,00/, `Die Summe über den ganzen Treffer muss -36,00 € zeigen, war: ${summaryText}`);
});

test("'×' am Kategorie-Chip entfernt genau diesen Filter, die Summenzeile verschwindet wieder (AC)", async () => {
  await categoryChip().click();
  await popover().getByText("Lebensmittel", { exact: true }).click();
  await popover().getByRole("button", { name: "Übernehmen (1)" }).click();
  assert.equal(await categoryChip().innerText(), "Kategorie: 1");

  await page.getByRole("button", { name: "Kategorie: 1 entfernen" }).click();
  assert.equal(await categoryChip().innerText(), "Kategorie", "Der Filter ist weg");
  assert.equal(await page.getByText(/^\d+ Buchungen$/).count(), 0, "Die Summenzeile verschwindet wieder");
  await table().getByText("Stadtwerke Musterstadt", { exact: true }).waitFor();
});

test("'Alle Filter löschen' erscheint nur bei aktivem Filter und setzt ihn zurück", async () => {
  assert.equal(await page.getByRole("button", { name: "Alle Filter löschen" }).count(), 0, "Ohne Filter fehlt der Knopf");

  await categoryChip().click();
  await popover().getByText("Bahn", { exact: true }).click();
  await popover().getByRole("button", { name: "Übernehmen (1)" }).click();
  assert.ok(await page.getByRole("button", { name: "Alle Filter löschen" }).isVisible(), "Mit aktivem Filter steht der Knopf da");

  await page.getByRole("button", { name: "Alle Filter löschen" }).click();
  assert.equal(await categoryChip().innerText(), "Kategorie", "Filter ist zurückgesetzt");
  assert.equal(await page.getByRole("button", { name: "Alle Filter löschen" }).count(), 0, "Der Knopf verschwindet wieder");
});

test("Ohne Treffer steht ein Leerzustand mit einer nächsten Handlung da (AC)", async () => {
  await categoryChip().click();
  await popover().getByText("Apotheke", { exact: true }).click();
  await popover().getByRole("button", { name: "Übernehmen (1)" }).click();

  await page.getByText("Kein Treffer", { exact: true }).waitFor();
  assert.ok(
    await page.getByText("Filter lockern oder alle Filter löschen.", { exact: true }).isVisible(),
    "Der Leerzustand nennt eine nächste Handlung"
  );

  await page.getByRole("button", { name: "Alle Filter löschen" }).first().click();
  await table().getByText("Stadtwerke Musterstadt", { exact: true }).waitFor();
});

// ---------------------------------------------------------------------------
// Handy-Viewport (390x844): BottomSheet-Chrome statt Popover (ADR 0043).
// ---------------------------------------------------------------------------

test("Am Handy öffnet der Kategorie-Chip ein BottomSheet, kein Popover (AC)", async () => {
  await mobileCategoryChip().click();
  await mobileSheet().getByText("Lebensmittel", { exact: true }).waitFor();

  assert.equal(await mobileSheet().count(), 1, "Genau ein offenes BottomSheet");
  assert.equal(await mobilePage.locator(".shadow-2xl").count(), 0, "Kein Popover am Handy");

  // Außerhalb tippen schließt, ohne zu filtern (kein Escape im BottomSheet, siehe
  // `DesignSystem/BottomSheet.fs`).
  await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  await mobileSheet().waitFor({ state: "detached" });
  assert.equal(await mobileCategoryChip().innerText(), "Kategorie", "Schließen ohne Auswahl filtert nicht");
});

test("Am Handy zeigen zwei übernommene Kategorien die Vereinigung in Liste und Summenzeile, der aktive Chip steht vor 'Zu prüfen' (AC)", async () => {
  await mobileCategoryChip().click();
  await mobileSheet().getByText("Lebensmittel", { exact: true }).click();
  await mobileSheet().getByText("Bahn", { exact: true }).click();
  await mobileSheet().getByRole("button", { name: "Übernehmen (2)" }).click();

  assert.equal(await mobileCategoryChip().innerText(), "Kategorie: 2", "Der Chip zeigt die Anzahl (AC 'Kategorie: 2')");
  await mobileSheet().waitFor({ state: "detached" });

  // Vereinigung wie am PC: REWE Mitte + Bäckerei Krause (Lebensmittel) + Deutsche
  // Bahn (Bahn) = 3, Stadtwerke Musterstadt (Nebenkosten) fällt raus. `.first()`
  // greift zuverlässig die Handy-Zeile: `entryListMobile` steht im Markup vor
  // `entryListDesktop`, dieselbe Payee-Kollision wie im PC-Test, nur umgekehrt.
  await mobilePage.getByText("REWE Mitte", { exact: true }).first().waitFor();
  assert.ok(
    await mobilePage.getByText("Deutsche Bahn", { exact: true }).first().isVisible(),
    "Bahn-Treffer ist da"
  );
  assert.equal(
    await mobilePage.getByText("Stadtwerke Musterstadt", { exact: true }).count(),
    0,
    "Nebenkosten ist NICHT im Treffer"
  );

  assert.ok(
    await mobilePage.getByText("3 Buchungen", { exact: true }).isVisible(),
    "Trefferzahl über den ganzen Treffer (AC)"
  );

  // DOM-Reihenfolge: der aktive Kategorie-Chip steht vor "Zu prüfen"
  // (`orderChipsForDisplay`, "aktive Chips wandern am Handy nach vorn").
  const categoryHandle = await mobileCategoryChip().elementHandle();
  const needsReviewHandle = await mobileNeedsReviewChip().elementHandle();
  const categoryBeforeNeedsReview = await mobilePage.evaluate(
    ([a, b]) => Boolean(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING),
    [categoryHandle, needsReviewHandle]
  );
  assert.ok(categoryBeforeNeedsReview, "Der aktive Kategorie-Chip steht im DOM vor 'Zu prüfen'");
});

// ---------------------------------------------------------------------------
// Sticky Filterleiste (Styleguide 7.3, `## What`: "auf beiden Geräten").
// ---------------------------------------------------------------------------

test("Die Filterleiste bleibt beim Scrollen unter der PC-Navbar sichtbar (sticky, AC)", async () => {
  // Eigener, kurzlebiger Kontext mit 40 Buchungen statt der vier Standard-
  // Fixtures: nur damit die Seite überhaupt hoch genug ist, um sinnvoll zu
  // scrollen. Kein Kategorie-Filter nötig, deshalb `resolveEntries` statt der
  // Standard-Kategorie-Auflösung.
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const accountId = accountsFixture.DefaultAccountId;
  const manyEntries = Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));

  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const scrollPage = await context.newPage();
  scrollPage.setDefaultTimeout(15000);

  await scrollPage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(scrollPage, { accountsFixture, balancesFixture, resolveEntries: () => manyEntries });

  try {
    await scrollPage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(scrollPage, "/ledger");

    // Ein atomarer `evaluate`-Aufruf nach dem Scrollen: eine einzelne
    // synchrone JS-Runde liest Scroll-Offset und `getBoundingClientRect()` im
    // selben Layout, kein Wettlauf zwischen zwei einzelnen
    // Playwright-Roundtrips.
    const beforeScroll = await scrollPage.evaluate(() => {
      const bar = document.querySelector(".ledger-filter-bar");
      return bar ? { top: Math.round(bar.getBoundingClientRect().top), scrollY: window.scrollY } : null;
    });
    assert.ok(beforeScroll, "Die Filterleiste steht im DOM");

    // Maus über die Liste (rechte Spalte, hinter der 268px breiten
    // Konten-Spalte) bewegen, bevor gescrollt wird: sonst könnte das
    // Scroll-Ereignis die Konten-Spalte treffen, die selbst `overflow-y: auto`
    // trägt (`.ledger-account-strip`, `ledger-qns4j`).
    await scrollPage.mouse.move(900, 500);
    await scrollPage.mouse.wheel(0, 1200);
    // Layout/Scroll beruhigen lassen, kein fester Ersatz für einen echten
    // Zustand, sondern eine kurze Pause vor dem einen entscheidenden Read.
    await scrollPage.waitForTimeout(200);

    const afterScroll = await scrollPage.evaluate(() => {
      const bar = document.querySelector(".ledger-filter-bar");
      const style = bar ? window.getComputedStyle(bar) : null;
      return bar && style
        ? {
            top: Math.round(bar.getBoundingClientRect().top),
            position: style.position,
            scrollY: window.scrollY,
          }
        : null;
    });

    assert.ok(afterScroll, "Die Filterleiste steht nach dem Scrollen weiterhin im DOM");
    assert.ok(afterScroll.scrollY > 0, "Die Seite hat tatsächlich gescrollt");
    assert.equal(afterScroll.position, "sticky", "Die Filterleiste ist per CSS sticky positioniert");
    // 64px PC-Navbar (`Navigation.desktopNav`, `h-16`): die Leiste bleibt
    // exakt darunter stehen, statt mit dem Inhalt wegzuscrollen.
    assert.equal(afterScroll.top, 64, `Die Leiste muss bei top:64px kleben, war ${afterScroll.top}`);
  } finally {
    await context.close();
  }
});

// ---------------------------------------------------------------------------
// Filterleiste und sticky Tabellenkopf kollidieren nicht (Verifier-Fund,
// dritte Iteration): die zweite Iteration hatte den Zuschlag auf eine feste
// Schätzung (44px) gesetzt, die weder bei 1440px (Leiste tatsächlich 38px
// hoch, Lücke unter der Leiste) noch bei 1024px mit umbrechenden Chips
// (Leiste tatsächlich 62px hoch, Überlappung) stimmte. Der Fix misst die
// Leiste per `ResizeObserver` und schreibt ihre echte Höhe in
// `--ledger-filter-bar-height` (`Components.Ledger.View.FilterBarColumn`).
// ---------------------------------------------------------------------------

/// Bewegt die Maus über die Liste und scrollt, bis Leiste und Tabellenkopf
/// beide in ihrem sticky-Zustand angekommen sind (dieselbe Vorgehensweise wie
/// im Sticky-Test oben).
async function scrollListIntoStickyState(targetPage) {
  targetPage.setDefaultTimeout(15000);
  // Sofortiger Sprung statt Mausrad (ledger-n4vte): `html { scroll-behavior: smooth }`
  // lässt das Mausrad animiert scrollen, und die Animation brach je nach Lauf nach
  // 123 bis 543px ab (Messung per scrollY-Verlauf), bevor der Tabellenkopf seine
  // Haft-Position erreichte. Geprüft wird die Lage von Leiste und Kopf im
  // Haft-Zustand, nicht die Scroll-Mechanik.
  await targetPage.locator("tbody tr").nth(30).waitFor();
  await targetPage.evaluate(() => window.scrollTo({ top: 1200, behavior: "instant" }));
  await targetPage.waitForTimeout(200);
}

/// Liest Leiste und Tabellenkopf in EINEM atomaren `evaluate`-Aufruf: beide
/// `getBoundingClientRect()`-Werte stammen aus demselben Layout-Durchlauf,
/// kein Wettlauf zwischen zwei einzelnen Playwright-Roundtrips.
async function barTheadGap(targetPage) {
  return targetPage.evaluate(() => {
    const bar = document.querySelector(".ledger-filter-bar");
    const thead = document.querySelector("table thead");
    if (!bar || !thead) return null;
    return {
      barBottom: bar.getBoundingClientRect().bottom,
      theadTop: thead.getBoundingClientRect().top,
    };
  });
}

function assertNoCollisionAndTightGap(gap) {
  assert.ok(gap, "Leiste und Tabellenkopf stehen im DOM");
  assert.ok(
    gap.theadTop >= gap.barBottom - 1,
    `Der Tabellenkopf darf die Leiste nicht überlappen: theadTop=${gap.theadTop}, barBottom=${gap.barBottom}`
  );
  assert.ok(
    gap.theadTop - gap.barBottom <= 2,
    `Der Abstand zwischen Leiste und Tabellenkopf darf höchstens 2px sein, war ${gap.theadTop - gap.barBottom}`
  );
}

test("Die Filterleiste überlappt den sticky Tabellenkopf nicht, bei 1440px ohne umbrechende Chips (AC)", async () => {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const accountId = accountsFixture.DefaultAccountId;
  const manyEntries = Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));

  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const gapPage = await context.newPage();

  await gapPage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(gapPage, { accountsFixture, balancesFixture, resolveEntries: () => manyEntries });

  try {
    await gapPage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(gapPage, "/ledger");

    await scrollListIntoStickyState(gapPage);

    assertNoCollisionAndTightGap(await barTheadGap(gapPage));
  } finally {
    await context.close();
  }
});

test("Die Filterleiste überlappt den sticky Tabellenkopf nicht, bei 1024px mit umbrechenden Chips und aktivem Kategorie-Filter (AC)", async () => {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const accountId = accountsFixture.DefaultAccountId;
  const manyEntries = Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));

  const context = await browser.newContext({ viewport: { width: 1024, height: 900 } });
  const gapPage = await context.newPage();

  await gapPage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  // `resolveEntries` ignoriert die Kategorie-Auswahl absichtlich (wie im
  // Sticky-Test oben): dieser Test prüft die Kollision, nicht die
  // Filter-Korrektheit, die schon andere Tests in dieser Datei belegen.
  await mockFilterBarApi(gapPage, { accountsFixture, balancesFixture, resolveEntries: () => manyEntries });

  try {
    await gapPage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(gapPage, "/ledger");

    // Zwei Kategorien übernehmen, damit der Kategorie-Chip auf "Kategorie: 2"
    // wächst und die Leiste bei 1024px tatsächlich umbricht (`md:flex-wrap`,
    // `ChipBar.bar`): genau der Fall, den die feste 44px-Schätzung der
    // zweiten Iteration nicht abdeckte.
    await gapPage.getByRole("button", { name: /^Kategorie/ }).first().click();
    await gapPage.locator(".shadow-2xl").getByText("Lebensmittel", { exact: true }).click();
    await gapPage.locator(".shadow-2xl").getByText("Bahn", { exact: true }).click();
    await gapPage.getByRole("button", { name: "Übernehmen (2)" }).click();
    await gapPage.locator(".shadow-2xl").waitFor({ state: "detached" });

    await scrollListIntoStickyState(gapPage);

    assertNoCollisionAndTightGap(await barTheadGap(gapPage));
  } finally {
    await context.close();
  }
});

// ---------------------------------------------------------------------------
// Kollision während eines laufenden Spiegel-Laufs (Verifier-Fund, vierte
// Iteration): der `ResizeObserver` aus der dritten Iteration hing an einem
// `useEffect` mit leeren Abhängigkeiten am Knoten, den `barRef` beim ERSTEN
// Mount hatte. Wird `model.MirrorRunning` wahr, rendert die keylose
// Kinderliste der Spalte den Hinweis "Spiegel läuft" an Index 0 und die
// Leiste erst an Index 1: React verwendet den alten Leisten-`div` für den
// Hinweis weiter und legt für die Leiste einen NEUEN Knoten an, der Observer
// maß danach den Hinweis statt der Leiste. Der Fix (`ChipBar.bar`s neuer
// Callback-Ref-Parameter, `Components.Ledger.View.FilterBarColumn`) trennt
// und beobachtet bei jedem Knotenwechsel neu, unabhängig vom Mount-Zeitpunkt.
// ---------------------------------------------------------------------------

const mirrorHint = (targetPage) =>
  targetPage.locator("span", { hasText: "Spiegel läuft — die Liste bleibt so lange stehen." });

test("Die Filterleiste überlappt den sticky Tabellenkopf nicht, während ein Spiegel-Lauf läuft, bei 1024px (AC)", async () => {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const accountId = accountsFixture.DefaultAccountId;
  const manyEntries = Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));

  const context = await browser.newContext({ viewport: { width: 1024, height: 900 } });
  const mirrorPage = await context.newPage();

  await mirrorPage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(mirrorPage, { accountsFixture, balancesFixture, resolveEntries: () => manyEntries });
  await mockRunningMirror(mirrorPage);

  try {
    await mirrorPage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(mirrorPage, "/ledger");

    // Der veraltete `getLastMirrorRun`-Stand stößt den automatischen Lauf an
    // (ADR 0014), ohne Klick auf "Jetzt spiegeln": der Hinweis steht, sobald
    // `model.MirrorRunning` wahr wird.
    await mirrorHint(mirrorPage).waitFor();

    await scrollListIntoStickyState(mirrorPage);

    assertNoCollisionAndTightGap(await barTheadGap(mirrorPage));
  } finally {
    await context.close();
  }
});

test("Der Abstand zwischen Leiste und Tabellenkopf stimmt weiterhin, nachdem der Spiegel-Lauf endet und der Hinweis verschwindet (AC)", async () => {
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const accountId = accountsFixture.DefaultAccountId;
  const manyEntries = Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));

  const context = await browser.newContext({ viewport: { width: 1024, height: 900 } });
  const mirrorPage = await context.newPage();

  await mirrorPage.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(mirrorPage, { accountsFixture, balancesFixture, resolveEntries: () => manyEntries });
  const mirror = await mockRunningMirror(mirrorPage);

  try {
    await mirrorPage.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
    await waitForPageLoaded(mirrorPage, "/ledger");

    const hint = mirrorHint(mirrorPage);
    await hint.waitFor();

    await mirror.finishMirror();
    await hint.waitFor({ state: "detached" });

    await scrollListIntoStickyState(mirrorPage);

    assertNoCollisionAndTightGap(await barTheadGap(mirrorPage));
  } finally {
    await context.close();
  }
});
