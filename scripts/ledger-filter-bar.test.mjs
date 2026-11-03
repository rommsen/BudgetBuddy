// scripts/ledger-filter-bar.test.mjs
//
// Die Filterleiste des Kassenbuchs im echten Fable-Client gegen gemockte Antworten
// (`scripts/ledger-filter-bar.mjs`). Hier steht nur, was der Browser allein messen kann:
// je ein Happy Path für PC (Popover) und Handy (BottomSheet), die Datumsrechnung im
// Fable-Kompilat, Schriftgrößen, der native Kalender und die sticky Leiste. Entwurf,
// Übernehmen, Sperren, Summenzeile, Leerzustand und Chip-Reihenfolge prüft `dotnet test`
// (`LedgerPageTests.fs`, `LedgerViewTests.fs`, `LedgerReviewStateTests.fs`).

import { test, before, after, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
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

/// Öffnet das Kassenbuch in einem frischen Kontext gegen die Filterleisten-Mocks.
async function openLedger(viewport, { resolveEntries, setup } = {}) {
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
    ...(resolveEntries ? { resolveEntries: () => resolveEntries(accountsFixture.DefaultAccountId) } : {}),
  });
  const extra = setup ? await setup(target) : undefined;
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return { context, page: target, extra };
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  ({ page } = await openLedger({ width: 1440, height: 900 }));
  ({ page: mobilePage } = await openLedger({ width: 390, height: 844 }));
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
const mobileCategoryChip = () => mobilePage.getByRole("button", { name: /^Kategorie/ }).first();
const mobileNeedsReviewChip = () => mobilePage.getByRole("button", { name: /^Zu prüfen/ }).first();
// `BottomSheet.view` steht immer im DOM, offen ist es nur mit der Klasse `active`.
const mobileSheet = () => mobilePage.locator(".bottom-sheet.active");

// Jeder Testfall startet ohne Filter und ohne offene Chrome.
beforeEach(async () => {
  const clearAll = page.getByRole("button", { name: "Alle Filter löschen" });
  if (await clearAll.count()) await clearAll.click();
  if (await popover().count()) await page.getByRole("heading", { name: "Kassenbuch" }).click();
  await popover().waitFor({ state: "detached" });

  const mobileClearAll = mobilePage.getByRole("button", { name: "Alle Filter löschen" });
  if (await mobileClearAll.count()) await mobileClearAll.click();
  if (await mobileSheet().count()) await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  await mobileSheet().waitFor({ state: "detached" });
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

test("[layout] Input.Small misst am Handy 16px und am PC 14px, auch in Datumsfeld und Kategorie-Suche, Medium 16px, Large 18px (btwhg, m7z9q, iOS-Zoom)", async () => {
  // Misst die Kaskade von `.input-field` gegen die Klassen aus `Input.sizeToClass`:
  // `.input-field` stand ohne Layer in styles.css und überstimmte `text-base` von Small.
  // Medium trägt zusätzlich das Inline-fontSize 16 (`Input.usesInlineFontSize`).
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
        large: make("text-lg px-4 py-3.5 min-h-[52px]", false),
      };
    });
  assert.deepEqual(await measure(page), { small: "14px", medium: "16px", large: "18px" }, "PC 1440x900");
  assert.deepEqual(await measure(mobilePage), { small: "16px", medium: "16px", large: "18px" }, "Handy 390x844");

  const size = (loc) => loc.evaluate((e) => getComputedStyle(e).fontSize);
  await page.getByRole("button", { name: /^Zeitraum/ }).first().click();
  assert.equal(await size(popover().getByPlaceholder("TT.MM.JJJJ").first()), "14px", "Datumsfeld PC");
  await page.keyboard.press("Escape");
  await mobilePage.getByRole("button", { name: /^Zeitraum/ }).first().click();
  const field = mobileSheet().getByPlaceholder("TT.MM.JJJJ").first();
  await field.waitFor();
  assert.equal(await size(field), "16px", "Datumsfeld Handy");
  await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  await mobileSheet().waitFor({ state: "detached" });

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

test("[layout] Am Handy öffnet der Chip ein BottomSheet, Tippen außerhalb filtert nicht, zwei Kategorien filtern, der aktive Chip rückt vor Zu prüfen", async () => {
  await mobileCategoryChip().click();
  await mobileSheet().getByText("Lebensmittel", { exact: true }).waitFor();
  assert.equal(await mobilePage.locator(".shadow-2xl").count(), 0, "kein Popover am Handy");
  await mobilePage.locator(".overlay.active").click({ position: { x: 5, y: 5 } });
  await mobileSheet().waitFor({ state: "detached" });
  assert.equal(await mobileCategoryChip().innerText(), "Kategorie", "Schließen filtert nicht");

  await mobileCategoryChip().click();
  await mobileSheet().getByText("Lebensmittel", { exact: true }).click();
  await mobileSheet().getByText("Bahn", { exact: true }).click();
  await mobileSheet().getByRole("button", { name: "Übernehmen (2)" }).click();
  await mobileSheet().waitFor({ state: "detached" });
  assert.equal(await mobileCategoryChip().innerText(), "Kategorie: 2");
  await mobilePage.getByText("REWE Mitte", { exact: true }).first().waitFor();
  assert.equal(await mobilePage.getByText("Stadtwerke Musterstadt", { exact: true }).count(), 0, "Nebenkosten fällt heraus");
  assert.ok(await mobilePage.getByText("3 Buchungen", { exact: true }).isVisible(), "Trefferzahl");

  const categoryHandle = await mobileCategoryChip().elementHandle();
  const needsReviewHandle = await mobileNeedsReviewChip().elementHandle();
  const inFront = await mobilePage.evaluate(
    ([a, b]) => Boolean(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING),
    [categoryHandle, needsReviewHandle]
  );
  assert.ok(inFront, "der aktive Chip steht im DOM vor Zu prüfen");
});

// ---------------------------------------------------------------------------
// Sticky: die Leiste haftet unter der Navbar, ihre Höhe misst ein `ResizeObserver` und schreibt sie
// nach `--ledger-filter-bar-height`. Eine feste Schätzung ließ bei 1440 px eine Lücke und überlappte
// bei 1024 px mit umbrechenden Chips. Wird `model.MirrorRunning` wahr, rendert die keylose Kinderliste
// einen Hinweis an Index 0 und die Leiste auf einem neuen Knoten: der Observer maß dann den Hinweis.
// ---------------------------------------------------------------------------

const manyEntries = (accountId) => Array.from({ length: 40 }, (_, i) => buildEntryFixture(accountId, i + 1));
const mirrorHint = (target) => target.getByText("Spiegel läuft — die Liste bleibt so lange stehen.");

/// Springt ohne Animation (`html { scroll-behavior: smooth }`) bis in den Haft-Zustand und wartet
/// zwei Bilder: der `ResizeObserver` schreibt die Höhe vor dem nächsten Zeichnen.
async function scrollIntoStickyState(target) {
  await target.locator("tbody tr").nth(30).waitFor();
  // Die Seite fährt beim Betreten ein (`animate-page-enter`), mitten darin steht die Leiste versetzt.
  // Endlose Animationen (Spinner, Puls) enden nie und zählen nicht.
  await target.evaluate(() =>
    Promise.all(
      document
        .getAnimations()
        .filter((a) => a.effect?.getTiming().iterations !== Infinity)
        .map((a) => a.finished.catch(() => {}))
    )
  );
  await target.evaluate(() => window.scrollTo({ top: 1200, behavior: "instant" }));
  await target.waitForFunction(() => window.scrollY >= 1199);
  await target.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

async function assertStickyWithoutCollision(target, label, expectedTop) {
  const at = await target.evaluate(() => {
    const bar = document.querySelector(".ledger-filter-bar");
    return {
      top: Math.round(bar.getBoundingClientRect().top),
      position: getComputedStyle(bar).position,
      barBottom: bar.getBoundingClientRect().bottom,
      theadTop: document.querySelector("table thead").getBoundingClientRect().top,
    };
  });
  assert.equal(at.position, "sticky", label);
  assert.equal(at.top, expectedTop, `${label}: die Leiste haftet unter der Navbar`);
  assert.ok(at.theadTop >= at.barBottom - 1, `${label}: der Tabellenkopf überlappt die Leiste nicht (${JSON.stringify(at)})`);
  assert.ok(at.theadTop - at.barBottom <= 2, `${label}: höchstens 2 px Abstand (${JSON.stringify(at)})`);
}

test("[layout] Die Leiste haftet unter der Navbar und der Tabellenkopf direkt darunter: 1440 px, umbrechend, mit und nach dem Spiegel-Lauf", async () => {
  const wide = await openLedger({ width: 1440, height: 900 }, { resolveEntries: manyEntries });
  try {
    await scrollIntoStickyState(wide.page);
    await assertStickyWithoutCollision(wide.page, "1440 px", 64);
  } finally {
    await wide.context.close();
  }

  const wrapped = await openLedger({ width: 1024, height: 900 }, { resolveEntries: manyEntries });
  try {
    // Zwei Kategorien lassen den Chip wachsen, die Leiste bricht bei 1024 px um.
    await wrapped.page.getByRole("button", { name: /^Kategorie/ }).first().click();
    await wrapped.page.locator(".shadow-2xl").getByText("Lebensmittel", { exact: true }).click();
    await wrapped.page.locator(".shadow-2xl").getByText("Bahn", { exact: true }).click();
    await wrapped.page.getByRole("button", { name: "Übernehmen (2)" }).click();
    await wrapped.page.locator(".shadow-2xl").waitFor({ state: "detached" });
    await scrollIntoStickyState(wrapped.page);
    await assertStickyWithoutCollision(wrapped.page, "1024 px, umbrechend", 64);
  } finally {
    await wrapped.context.close();
  }

  const mirrored = await openLedger({ width: 1024, height: 900 }, { resolveEntries: manyEntries, setup: mockRunningMirror });
  try {
    const hint = mirrorHint(mirrored.page);
    await hint.waitFor();
    await scrollIntoStickyState(mirrored.page);
    await assertStickyWithoutCollision(mirrored.page, "während des Spiegel-Laufs", 64);

    await mirrored.page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
    await mirrored.extra.finishMirror();
    await hint.waitFor({ state: "detached" });
    await scrollIntoStickyState(mirrored.page);
    await assertStickyWithoutCollision(mirrored.page, "nach dem Spiegel-Lauf", 64);
  } finally {
    await mirrored.context.close();
  }
});
