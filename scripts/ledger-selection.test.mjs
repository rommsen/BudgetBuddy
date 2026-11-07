// scripts/ledger-selection.test.mjs
//
// Gerenderter Beleg für `ledger-r1d2f` (Auswahl-Modus mit Summe): am Handy (390x844) langes
// Drücken, Kästchen, "Alle", die zweireihige Leiste statt der Tab-Bar; am PC (1440x900) die
// Kästchen-Spalte, das Kopfzeilen-Kästchen, die Spanne per Shift-Klick mit sichtbarem Anker
// und die einreihige Leiste. Ein echter, per Vite gestarteter Fable-Client gegen gemockte
// Ledger-Antworten (erfundene Werte, nie die Entwicklungsdatenbank). Die Liste meldet 34
// Treffer, geladen werden aber nur zwölf je Seite, damit "Alle" über den ganzen Treffer
// geprüft wird.
//
// Optional: SELECTION_SCREENSHOTS=<verzeichnis> legt je Zustand ein Bild ab.
//
// Ausführen: node --test scripts/ledger-selection.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import path from "node:path";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
} from "./ledger-status-column.mjs";
import { buildCategoriesFixture, buildSettingsFixture } from "./ledger-filter-bar.mjs";

const TOTAL = 34;
const PAGE = 12;
const TOTAL_TEXT = "-2.140,00";

let frontend;
let frontendPort;
let browser;

function rowFixtures(accountId) {
  return Array.from({ length: TOTAL }, (_, i) =>
    buildEntryFixture(accountId, i + 1, {
      Payee: `Auswahlzeile ${String(i + 1).padStart(2, "0")}`,
      Date: "2026-03-10T00:00:00.0000000",
      Amount: { Amount: -10, Currency: "EUR" },
    })
  );
}

/// Öffnet die Seite im gegebenen Viewport; die Mock-Antwort kennt Seiten (zwölf je Seite).
/// `setup` (optional) registriert weitere Mocks vor dem Laden der Seite.
async function openLedger(viewport, hasTouch, setup) {
  const context = await browser.newContext({ viewport, hasTouch });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accounts = buildAccountsFixture(2);
  const balances = buildBalancesFixture(accounts.Accounts);
  const all = rowFixtures(accounts.DefaultAccountId);

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(accounts) })
  );
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(balances) })
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const query = JSON.parse(route.request().postData())[0];
    const entries = all.slice(query.Page * PAGE, query.Page * PAGE + PAGE);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: entries,
        TotalCount: TOTAL,
        TotalAmount: { Amount: -2140, Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: 0, AskLenaCount: 0,
      }),
    });
  });
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: new Date().toISOString(),
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    })
  );

  if (setup) await setup(page);

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.getByText("Auswahlzeile 01").filter({ visible: true }).first().waitFor();
  return { page, context };
}

async function screenshotIfRequested(page, name) {
  const dir = process.env.SELECTION_SCREENSHOTS;
  if (!dir) return;
  mkdirSync(dir, { recursive: true });
  await page.screenshot({ path: path.join(dir, `${name}.png`) });
}

/// Langes Drücken mit Touch-Zeigerereignissen (Playwright kennt keine Haltegeste).
async function longPress(locator) {
  const box = await locator.boundingBox();
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  const init = { pointerType: "touch", pointerId: 7, isPrimary: true, bubbles: true, clientX: x, clientY: y };
  await locator.dispatchEvent("pointerdown", init);
  await visibleBar(locator.page()).waitFor();
  await locator.dispatchEvent("pointerup", init);
  await locator.dispatchEvent("click", { bubbles: true, clientX: x, clientY: y });
}

const visibleBar = (page) => page.locator(".selection-bar").filter({ visible: true });

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[flow:mehrfachauswahl] Handy 390x844: langes Drücken, Tippen, Alle und Auswahl aufheben", async () => {
  const { page, context } = await openLedger({ width: 390, height: 844 }, true);
  try {
    const rowButton = (n) =>
      // Im Modus wird die Zeile zum Kästchen (role=checkbox), deshalb über Tag und Text suchen.
      page.locator("button", { hasText: `Auswahlzeile ${String(n).padStart(2, "0")}` }).filter({ visible: true }).first();
    assert.equal(await visibleBar(page).count(), 0, "ohne Modus keine Leiste");
    assert.ok(await page.locator("nav").filter({ visible: true }).count() >= 1, "die Tab-Bar steht");

    await longPress(rowButton(1));
    const bar = visibleBar(page);
    await bar.waitFor();
    const barText = await bar.innerText();
    assert.match(barText, /1 ausgewählt/, "Reihe 1: Anzahl");
    assert.ok(barText.includes("-10,00"), `Reihe 1: Summe der Zeile, war: ${barText}`);
    assert.ok(barText.includes("Alle") && barText.includes("Auswahl aufheben"), "Reihe 2: Alle und Auswahl aufheben");
    // Geprüft ist die primäre Aktion (ledger-nhwrt), daneben Alle und Auswahl aufheben.
    // Die Lena-Aktionen (ledger-y54gr): ohne Farbe nennt die Leiste den Grund
    // (mit dem Wort Flag), deshalb steht 'Flag' hier nicht mehr in der Verbotsliste.
    for (const word of ["Kategorie ändern", "Ungeprüft"]) {
      assert.ok(!barText.includes(word), `keine weitere schreibende Aktion '${word}' in der Leiste`);
    }
    const reviewedButton = bar.getByRole("button", { name: "Geprüft", exact: true });
    assert.equal(await reviewedButton.count(), 1, "Geprüft steht in der Leiste");
    // Kategorie setzen (ledger-w29y6): die zweite Sammelaktion neben Geprüft.
    const categoryButton = bar.getByRole("button", { name: "Kategorie setzen", exact: true });
    assert.equal(await categoryButton.count(), 1, "Kategorie setzen steht in der Leiste");
    // Dass die Leiste bei 390 px nicht überläuft, prüft der Layout-Sweep (Zustand kassenbuch-auswahl-handy).
    assert.equal(await page.locator("nav.fixed").filter({ visible: true }).count(), 0, "die Tab-Bar ist weg");
    assert.ok(
      (await page.getByText("Auswahl: 1 von 34 Buchungen").filter({ visible: true }).count()) === 1,
      "Summenzeile zeigt die Auswahl"
    );
    await screenshotIfRequested(page, "handy-390x844-3.2-eine-zeile");

    // Tipp wählt ab, zweiter Tipp wieder aus; die letzte abzuwählen lässt den Modus an.
    await rowButton(1).click();
    assert.match(await visibleBar(page).innerText(), /0 ausgewählt/, "abgewählt, Leiste bleibt");
    await rowButton(1).click();
    await rowButton(2).click();
    assert.match(await visibleBar(page).innerText(), /2 ausgewählt/, "zwei Zeilen");
    // Der Tipp auf das Kästchen selbst (ledger-w4vte) wählt ab und wieder an; der Container
    // um das Kästchen ist mindestens 44 px breit.
    const boxContainer = rowButton(2).locator("xpath=preceding-sibling::div[1]");
    const containerBox = await boxContainer.boundingBox();
    assert.ok(containerBox.width >= 44, `Kästchen-Trefferfläche mindestens 44 px breit, war: ${containerBox.width}`);
    await boxContainer.tap();
    assert.match(await visibleBar(page).innerText(), /1 ausgewählt/, "Tipp auf das Kästchen wählt ab");
    await boxContainer.tap();
    assert.match(await visibleBar(page).innerText(), /2 ausgewählt/, "und wieder an");
    assert.equal(await page.getByRole("dialog").filter({ visible: true }).count(), 0, "kein Tipp öffnet die Historie");

    // Alle: 34 von 34 mit der Summe des Treffers, obwohl nur zwölf geladen sind.
    await visibleBar(page).getByRole("button", { name: "Alle", exact: true }).click();
    const all = await visibleBar(page).innerText();
    assert.match(all, /34 ausgewählt/, "Anzahl des ganzen Treffers");
    assert.ok(all.includes(TOTAL_TEXT), `Summe des ganzen Treffers, war: ${all}`);
    assert.equal(await page.getByText("Auswahl: 34 von 34 Buchungen").filter({ visible: true }).count(), 1, "Summenzeile");
    await screenshotIfRequested(page, "handy-390x844-3.3-alle");

    // Alle noch einmal leert, der Modus bleibt.
    await visibleBar(page).getByRole("button", { name: "Alle", exact: true }).click();
    assert.match(await visibleBar(page).innerText(), /0 ausgewählt/, "geleert");

    // Die Leiste verdeckt das Listenende nicht dauerhaft: am Ende lässt sich die letzte Zeile über die Leiste scrollen.
    await page.waitForFunction((total) => {
      window.scrollTo({ top: document.body.scrollHeight, behavior: "instant" });
      const rows = [...document.querySelectorAll("button")].filter((el) => el.offsetParent !== null && el.textContent.includes("Auswahlzeile"));
      return rows.length === total && window.scrollY + window.innerHeight >= document.body.scrollHeight - 1;
    }, TOTAL);
    const barTop = (await visibleBar(page).boundingBox()).y;
    const lastRows = await page.locator("button", { hasText: "Auswahlzeile" }).filter({ visible: true }).all();
    const lastBox = await lastRows[lastRows.length - 1].boundingBox();
    assert.ok(lastBox.y + lastBox.height <= barTop + 1, `letzte Zeile unter der Leiste frei: ${lastBox.y + lastBox.height} <= ${barTop}`);

    // Auswahl aufheben beendet den Modus: Tab-Bar zurück, Summenzeile wieder der Treffer.
    await visibleBar(page).getByRole("button", { name: "Auswahl aufheben" }).click();
    assert.equal(await visibleBar(page).count(), 0, "Leiste weg");
    assert.ok(await page.locator("nav.fixed").filter({ visible: true }).count() >= 1, "Tab-Bar zurück");
  } finally {
    await context.close();
  }
});

test("[tastatur] PC 1440x900: Kästchen, Shift-Spanne mit Anker, Kopfzeilen-Kästchen und Leiste", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 }, false);
  try {
    const box = (n) =>
      page.getByRole("checkbox", { name: `Auswahlzeile ${String(n).padStart(2, "0")} auswählen` }).filter({ visible: true });
    const header = page.getByRole("checkbox", { name: "Alle Buchungen des Treffers auswählen" }).filter({ visible: true });
    assert.equal(await visibleBar(page).count(), 0, "ohne Auswahl keine Leiste");
    assert.equal(await header.getAttribute("aria-checked"), "false", "Kopfzeilen-Kästchen leer");

    await box(1).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    const text = await bar.innerText();
    assert.match(text, /1 ausgewählt/);
    assert.ok(text.includes("Auswahl aufheben"), "Ausstieg");
    assert.ok(!text.includes("Alle"), "kein Alle in der PC-Leiste");
    for (const word of ["Kategorie ändern", "Flag", "Ungeprüft"]) {
      assert.ok(!text.includes(word), `keine weitere schreibende Aktion '${word}'`);
    }
    // Geprüft (ledger-nhwrt): die einzige primäre Aktion, rechts in der einreihigen Leiste.
    const reviewedButton = bar.getByRole("button", { name: "Geprüft", exact: true });
    assert.equal(await reviewedButton.count(), 1, "Geprüft steht in der PC-Leiste");
    assert.equal(await bar.getByRole("button", { name: "Kategorie setzen", exact: true }).count(), 1, "Kategorie setzen steht in der PC-Leiste");
    assert.equal(await header.getAttribute("aria-checked"), "mixed", "Teilauswahl: Strich");

    await box(5).click({ modifiers: ["Shift"] });
    assert.match(await visibleBar(page).innerText(), /5 ausgewählt/, "Spanne von 1 bis 5");

    await screenshotIfRequested(page, "pc-1440x900-3.7-spanne");

    // Kopfzeilen-Kästchen: Strich wählt den ganzen Treffer, Haken leert.
    await header.click();
    assert.match(await visibleBar(page).innerText(), /34 ausgewählt/, "ganzer Treffer");
    assert.equal(await header.getAttribute("aria-checked"), "true", "Haken");
    assert.equal(await page.getByText("Auswahl: 34 von 34 Buchungen").filter({ visible: true }).count(), 1, "Summenzeile");
    assert.ok((await visibleBar(page).innerText()).includes(TOTAL_TEXT), "Summe des ganzen Treffers");
    await screenshotIfRequested(page, "pc-1440x900-3.6-alle");
    await header.click();
    assert.equal(await visibleBar(page).count(), 0, "Haken leert, Leiste weg");

    // Auswahl aufheben.
    await box(2).click();
    await visibleBar(page).getByRole("button", { name: "Auswahl aufheben" }).click();
    assert.equal(await visibleBar(page).count(), 0, "Leiste weg nach Auswahl aufheben");
    assert.equal(await header.getAttribute("aria-checked"), "false", "Kopfzeilen-Kästchen leer");
  } finally {
    await context.close();
  }
});

const countIn = async (page) => {
  const bar = visibleBar(page);
  if ((await bar.count()) === 0) return 0;
  const match = (await bar.innerText()).match(/(\d+) ausgewählt/);
  return match ? Number(match[1]) : 0;
};

test("[tastatur] PC 1440x900: Leertaste wählt genau einmal, schließt das Panel, Prüf-Schaltfläche ruht, Esc-Kette (ledger-w4vte)", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 }, false);
  try {
    const box = (n) =>
      page.getByRole("checkbox", { name: `Auswahlzeile ${String(n).padStart(2, "0")} auswählen` }).filter({ visible: true });
    const row = (n) => page.locator("tr", { hasText: `Auswahlzeile ${String(n).padStart(2, "0")}` }).filter({ visible: true }).first();
    const panel = page.locator('[data-testid="ledger-detail-panel"]');
    const reviewButtons = (n) => row(n).locator('button[title^="Als "]');

    // Mausklick auf ein Kästchen, danach Leertaste: der Fokus liegt auf dem Knopf, die native
    // Wirkung gewinnt und die Auswahl ändert sich genau einmal.
    assert.ok((await reviewButtons(3).count()) >= 1, "ohne Auswahl trägt die Zeile die Prüf-Schaltfläche");
    await box(1).click();
    await box(2).click();
    assert.equal(await countIn(page), 2, "zwei gewählt");
    const heightWithBar = await row(3).evaluate((el) => el.getBoundingClientRect().height);
    assert.equal(await reviewButtons(3).count(), 0, "bei laufender Auswahl keine Prüf-Schaltfläche in der Zeile");
    // Ein unsichtbarer Platzhalter hält die Stelle der Schaltfläche, die Zeile springt nicht.
    assert.equal(await row(3).locator("td").last().locator('span[aria-hidden="true"]').count(), 1, "unsichtbarer Platzhalter statt der Schaltfläche");
    assert.ok(heightWithBar > 0, "Zeile steht");
    await page.keyboard.press("Space");
    assert.equal(await countIn(page), 1, "Maus plus Leertaste kippt genau einmal (nicht zweimal)");
    await page.keyboard.press("Space");
    assert.equal(await countIn(page), 2, "und wieder genau einmal zurück");
    await visibleBar(page).getByRole("button", { name: "Auswahl aufheben" }).click();
    assert.equal(await countIn(page), 0, "Auswahl aufgehoben");

    // Leertaste auf der markierten Zeile ohne vorherigen Klick auf ein Kästchen: Zeile gewählt,
    // ein offenes Panel ist zu.
    await row(3).getByText("Auswahlzeile 03").click();
    await panel.waitFor();
    await page.keyboard.press("Space");
    await panel.waitFor({ state: "detached" });
    assert.equal(await countIn(page), 1, "eine Zeile gewählt");
    assert.equal(await box(3).getAttribute("aria-checked"), "true", "es ist die markierte Zeile");
    assert.ok(
      (await visibleBar(page).innerText()).includes("Esc aufheben"),
      "die PC-Leiste nennt Esc aufheben"
    );

    // Bei laufender Auswahl öffnet ein Klick auf die Zeile kein Detail.
    await row(5).getByText("Auswahlzeile 05").click();
    assert.equal(await panel.count(), 0, "kein Panel bei laufender Auswahl");
    // G ruht bei laufender Auswahl: kein Aufruf, die Auswahl bleibt.
    let reviewCalls = 0;
    page.on("request", (request) => {
      if (request.url().includes("setReviewMark")) reviewCalls++;
    });
    await page.keyboard.press("g");
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    assert.equal(reviewCalls, 0, "G schreibt bei laufender Auswahl nichts");

    // Esc hebt die Auswahl auf (kein Popover, kein Detail offen).
    await page.keyboard.press("Escape");
    assert.equal(await countIn(page), 0, "Esc hebt die Auswahl auf");
  } finally {
    await context.close();
  }
});

// ============================================
// Kategorie setzen für die Auswahl (ledger-w29y6)
// ============================================

/// Settings und Kategorien, damit der Picker Zeilen hat.
async function categorySetup(page) {
  await page.route("**/api/SettingsApi/getSettings", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(buildSettingsFixture()) })
  );
  await page.route("**/api/YnabApi/getCategories", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: buildCategoriesFixture() }) })
  );
}

// Die Lage des Popovers prüft der Layout-Sweep (Zustand kassenbuch-auswahl-popover, ledger-w29y6).
test("[tastatur] PC 1440x900: bei offenem Popover Kategorie setzen schließt Esc nur den Picker, die Auswahl bleibt", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 }, false, categorySetup);
  try {
    const box = (n) =>
      page.getByRole("checkbox", { name: `Auswahlzeile ${String(n).padStart(2, "0")} auswählen` }).filter({ visible: true });
    await box(1).click();
    await box(2).click();
    await box(3).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    await bar.getByRole("button", { name: "Kategorie setzen", exact: true }).click();
    const listbox = page.locator('[role="listbox"]').filter({ visible: true });
    await listbox.waitFor();
    assert.equal(await listbox.locator('[aria-selected="true"]').count(), 0, "keine Zeile mit Haken, auch nicht Ohne Kategorie");
    await screenshotIfRequested(page, "pc-1440x900-kategorie-setzen-popover");

    // Esc schließt nur den Picker, die Auswahl bleibt.
    await page.keyboard.press("Escape");
    await listbox.waitFor({ state: "detached" });
    assert.equal(await countIn(page), 3, "die Auswahl bleibt nach Esc");
  } finally {
    await context.close();
  }
});
