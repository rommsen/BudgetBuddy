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
async function openLedger(viewport, hasTouch) {
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
        NeedsReviewCount: 0,
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

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.waitForTimeout(500);
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
  await locator.page().waitForTimeout(700);
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

test("Handy 390x844: langes Drücken, Tippen, Alle und Auswahl aufheben", async () => {
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
    for (const word of ["Geprüft", "Kategorie ändern", "Flag"]) {
      assert.ok(!barText.includes(word), `keine schreibende Aktion '${word}' in der Leiste`);
    }
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
    assert.equal(await page.getByRole("dialog").filter({ visible: true }).count(), 0, "kein Tipp öffnet die Historie");

    // Alle: 34 von 34 mit der Summe des Treffers, obwohl nur zwölf geladen sind.
    await visibleBar(page).getByRole("button", { name: "Alle", exact: true }).click();
    const all = await visibleBar(page).innerText();
    assert.match(all, /34 ausgewählt/, "Anzahl des ganzen Treffers");
    assert.ok(all.includes(TOTAL_TEXT), `Summe des ganzen Treffers, war: ${all}`);
    assert.equal(await page.getByText("Auswahl: 34 von 34 Buchungen").filter({ visible: true }).count(), 1, "Summenzeile");
    const allButtonClass = await visibleBar(page).getByRole("button", { name: "Alle", exact: true }).evaluate((el) => el.parentElement.className);
    assert.ok(allButtonClass.includes("border-neon-teal"), `Alle trägt den Teal-Aktivzustand: ${allButtonClass}`);
    await screenshotIfRequested(page, "handy-390x844-3.3-alle");

    // Alle noch einmal leert, der Modus bleibt.
    await visibleBar(page).getByRole("button", { name: "Alle", exact: true }).click();
    assert.match(await visibleBar(page).innerText(), /0 ausgewählt/, "geleert");

    // Die Leiste verdeckt das Listenende nicht dauerhaft: am Ende lässt sich die letzte Zeile über die Leiste scrollen.
    for (let i = 0; i < 6; i++) {
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      await page.waitForTimeout(600);
    }
    assert.equal(await page.locator("button", { hasText: "Auswahlzeile" }).filter({ visible: true }).count(), TOTAL, "alle Seiten sind nachgeladen");
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

test("PC 1440x900: Kästchen, Shift-Spanne mit Anker, Kopfzeilen-Kästchen und Leiste", async () => {
  const { page, context } = await openLedger({ width: 1440, height: 900 }, false);
  try {
    const box = (n) =>
      page.getByRole("checkbox", { name: `Auswahlzeile ${String(n).padStart(2, "0")} auswählen` }).filter({ visible: true });
    const header = page.getByRole("checkbox", { name: "Alle Buchungen des Treffers auswählen" }).filter({ visible: true });
    assert.equal(await visibleBar(page).count(), 0, "ohne Auswahl keine Leiste");
    assert.equal(await header.getAttribute("aria-checked"), "false", "Kopfzeilen-Kästchen leer");

    const size = await box(1).locator("span").first().evaluate((el) => el.getBoundingClientRect().width);
    assert.equal(Math.round(size), 17, "Kästchen 17px");

    await box(1).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    const text = await bar.innerText();
    assert.match(text, /1 ausgewählt/);
    assert.ok(text.includes("Auswahl aufheben"), "Ausstieg");
    assert.ok(!text.includes("Alle"), "kein Alle in der PC-Leiste");
    for (const word of ["Geprüft", "Kategorie ändern", "Flag"]) {
      assert.ok(!text.includes(word), `keine schreibende Aktion '${word}'`);
    }
    assert.equal(await header.getAttribute("aria-checked"), "mixed", "Teilauswahl: Strich");

    await box(5).click({ modifiers: ["Shift"] });
    assert.match(await visibleBar(page).innerText(), /5 ausgewählt/, "Spanne von 1 bis 5");

    // Der Anker (Zeile 1) trägt die Teal-Kante, die anderen gewählten Zeilen nicht.
    const shadowOf = (n) =>
      page.locator("tr", { hasText: `Auswahlzeile ${String(n).padStart(2, "0")}` }).filter({ visible: true }).first().evaluate((el) => getComputedStyle(el).boxShadow);
    assert.ok((await shadowOf(1)).includes("rgb(0, 212, 170)"), `Anker mit Teal-Kante, war: ${await shadowOf(1)}`);
    assert.ok(!(await shadowOf(3)).includes("rgb(0, 212, 170)"), "eine andere gewählte Zeile ohne Kante");
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
