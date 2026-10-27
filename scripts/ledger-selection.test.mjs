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
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, pollUntil } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
} from "./ledger-status-column.mjs";
import { CATEGORY_IDS, buildCategoriesFixture, buildSettingsFixture } from "./ledger-filter-bar.mjs";

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
    // Geprüft ist die primäre Aktion (ledger-nhwrt, Orange), in Reihe 2 neben Alle und Auswahl aufheben.
    // Die Lena-Aktionen (ledger-y54gr) stehen in Reihe 3; ohne Farbe nennt die Leiste den Grund
    // (mit dem Wort Flag), deshalb steht 'Flag' hier nicht mehr in der Verbotsliste.
    for (const word of ["Kategorie ändern", "Ungeprüft"]) {
      assert.ok(!barText.includes(word), `keine weitere schreibende Aktion '${word}' in der Leiste`);
    }
    const reviewedButton = bar.getByRole("button", { name: "Geprüft", exact: true });
    assert.equal(await reviewedButton.count(), 1, "Geprüft steht in der Leiste");
    assert.ok(
      (await reviewedButton.getAttribute("class")).includes("from-neon-orange"),
      "Geprüft ist die primäre Aktion (Orange)"
    );
    // Kategorie setzen (ledger-w29y6): die zweite, sekundäre Sammelaktion neben Geprüft.
    const categoryButton = bar.getByRole("button", { name: "Kategorie setzen", exact: true });
    assert.equal(await categoryButton.count(), 1, "Kategorie setzen steht in der Leiste");
    assert.ok(
      !(await categoryButton.getAttribute("class")).includes("from-neon-orange"),
      "Kategorie setzen ist sekundär, nicht orange"
    );
    const centers = [];
    for (const name of ["Alle", "Geprüft", "Kategorie setzen", "Auswahl aufheben"]) {
      const box = await bar.getByRole("button", { name, exact: true }).boundingBox();
      centers.push({ name, x: box.x, mid: box.y + box.height / 2 });
    }
    assert.ok(
      centers.every((c) => Math.abs(c.mid - centers[0].mid) <= 3),
      `Alle, Geprüft, Kategorie setzen und Auswahl aufheben stehen in einer Reihe: ${JSON.stringify(centers)}`
    );
    assert.ok(
      centers[0].x < centers[1].x && centers[1].x < centers[2].x && centers[2].x < centers[3].x,
      "Reihenfolge: Alle, Geprüft, Kategorie setzen, Auswahl aufheben"
    );
    // Die Reihe läuft bei 390 px nicht über: kein Knopf ragt über den Rand, die Seite scrollt nicht seitwärts.
    for (const name of ["Alle", "Geprüft", "Kategorie setzen", "Auswahl aufheben"]) {
      const box = await bar.getByRole("button", { name, exact: true }).boundingBox();
      assert.ok(box.x >= 0 && box.x + box.width <= 390, `${name} liegt im Bildschirm: ${JSON.stringify(box)}`);
    }
    assert.ok(
      await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth),
      "kein seitliches Scrollen"
    );
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
    for (const word of ["Kategorie ändern", "Flag", "Ungeprüft"]) {
      assert.ok(!text.includes(word), `keine weitere schreibende Aktion '${word}'`);
    }
    // Geprüft (ledger-nhwrt): die einzige primäre Aktion, rechts in der einreihigen Leiste.
    const reviewedButton = bar.getByRole("button", { name: "Geprüft", exact: true });
    assert.equal(await reviewedButton.count(), 1, "Geprüft steht in der PC-Leiste");
    assert.ok(
      (await reviewedButton.getAttribute("class")).includes("from-neon-orange"),
      "Geprüft ist die primäre Aktion (Orange)"
    );
    const reviewedBox = await reviewedButton.boundingBox();
    const clearBox = await bar.getByRole("button", { name: "Auswahl aufheben", exact: true }).boundingBox();
    const barBox = await bar.boundingBox();
    assert.ok(reviewedBox.x > barBox.x + barBox.width / 2, "Geprüft sitzt rechts in der Leiste");
    assert.ok(Math.abs(reviewedBox.y + reviewedBox.height / 2 - (clearBox.y + clearBox.height / 2)) <= 3, "eine Reihe");
    // Kategorie setzen (ledger-w29y6): sekundär, in derselben Reihe, links von Geprüft.
    const categoryButton = bar.getByRole("button", { name: "Kategorie setzen", exact: true });
    const categoryBox = await categoryButton.boundingBox();
    assert.ok(!(await categoryButton.getAttribute("class")).includes("from-neon-orange"), "sekundär, nicht orange");
    assert.ok(categoryBox.x < reviewedBox.x && categoryBox.x > clearBox.x, "zwischen Auswahl aufheben und Geprüft");
    assert.ok(Math.abs(categoryBox.y + categoryBox.height / 2 - (clearBox.y + clearBox.height / 2)) <= 3, "Kategorie setzen in derselben Reihe");
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

const countIn = async (page) => {
  const bar = visibleBar(page);
  if ((await bar.count()) === 0) return 0;
  const match = (await bar.innerText()).match(/(\d+) ausgewählt/);
  return match ? Number(match[1]) : 0;
};

test("PC 1440x900: Leertaste wählt genau einmal, schließt das Panel, Prüf-Schaltfläche ruht, Esc-Kette (ledger-w4vte)", async () => {
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
    const placeholder = await row(3).locator("td").last().locator('span[aria-hidden="true"]').evaluate((el) => {
      const r = el.getBoundingClientRect();
      return { w: Math.round(r.width), h: Math.round(r.height) };
    });
    assert.deepEqual(placeholder, { w: 28, h: 28 }, "unsichtbarer Platzhalter in der Größe der Schaltfläche");
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
    await page.waitForTimeout(300);
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

const CATEGORY_NAME = { [CATEGORY_IDS.bahn]: "Bahn", [CATEGORY_IDS.lebensmittel]: "Lebensmittel" };

/// Mocks für Settings, Kategorien und den Sammel-Befehl. Der Befehl zählt seine Aufrufe, meldet
/// die ersten zwei gewählten Buchungen als geändert (vorher ohne Kategorie, also `Previous: null`)
/// und die übrigen als Split übersprungen; `delayMs` hält die Antwort zurück (Sperre prüfbar).
function categoryBatchSetup(calls, delayMs = 0) {
  return async (page) => {
    await page.route("**/api/SettingsApi/getSettings", (route) =>
      route.fulfill({ contentType: "application/json", body: JSON.stringify(buildSettingsFixture()) })
    );
    await page.route("**/api/YnabApi/getCategories", (route) =>
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: buildCategoriesFixture() }) })
    );
    await page.route("**/api/LedgerApi/setEntryCategoryBatch", async (route) => {
      const [selection, target] = route.request().postDataJSON();
      calls.push({ selection, target });
      if (delayMs > 0) await new Promise((resolve) => setTimeout(resolve, delayMs));
      const ids = selection.BatchPicked;
      const changed = ids.slice(0, 2).map((id) => ({ EntryId: id, Previous: null, WriteBack: "CategorySent" }));
      const skipped = ids.slice(2).map(() => "SkipSplit");
      route.fulfill({
        contentType: "application/json",
        body: JSON.stringify({ Ok: { Changed: changed, UnchangedCount: 0, Skipped: skipped } }),
      });
    });
  };
}

const rect = (locator) => locator.evaluate((el) => {
  const r = el.getBoundingClientRect();
  return { top: r.top, bottom: r.bottom, left: r.left, right: r.right };
});

test("PC 1440x900: Kategorie setzen öffnet das Popover über der Leiste ohne Haken, Esc schließt nur den Picker, Toast und Rücknahme (ledger-w29y6)", async () => {
  const calls = [];
  const { page, context } = await openLedger({ width: 1440, height: 900 }, false, categoryBatchSetup(calls, 600));
  try {
    const box = (n) =>
      page.getByRole("checkbox", { name: `Auswahlzeile ${String(n).padStart(2, "0")} auswählen` }).filter({ visible: true });
    await box(1).click();
    await box(2).click();
    await box(3).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    const categoryButton = bar.getByRole("button", { name: "Kategorie setzen", exact: true });

    // Das Popover liegt ganz im Bildschirm, über der Leiste, und markiert keine Zeile.
    await categoryButton.click();
    const listbox = page.locator('[role="listbox"]').filter({ visible: true });
    await listbox.waitFor();
    const popover = listbox.locator("xpath=ancestor::div[contains(@class,'absolute')][1]");
    const popoverRect = await rect(popover);
    const barRect = await rect(bar);
    assert.ok(popoverRect.left >= 0 && popoverRect.right <= 1440, `Popover liegt waagerecht im Bildschirm: ${JSON.stringify(popoverRect)}`);
    assert.ok(popoverRect.top >= 0, `Popover ragt nicht oben heraus: ${JSON.stringify(popoverRect)}`);
    assert.ok(popoverRect.bottom <= barRect.top + 1, `Popover liegt über der Leiste: ${popoverRect.bottom} <= ${barRect.top}`);
    assert.equal(await listbox.locator('[aria-selected="true"]').count(), 0, "keine Zeile mit Haken, auch nicht Ohne Kategorie");
    await screenshotIfRequested(page, "pc-1440x900-kategorie-setzen-popover");

    // Esc schließt nur den Picker, die Auswahl bleibt.
    await page.keyboard.press("Escape");
    await listbox.waitFor({ state: "detached" });
    assert.equal(await countIn(page), 3, "die Auswahl bleibt nach Esc");

    // Wahl der Kategorie: ein Aufruf mit den gewählten Ids; beide Aktionen sind gesperrt, bis er antwortet.
    await categoryButton.click();
    await listbox.waitFor();
    await listbox.getByText("Bahn", { exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll(".selection-bar button:disabled").length >= 2);
    assert.equal(await bar.getByRole("button", { name: "Geprüft", exact: true }).isDisabled(), true, "Geprüft gesperrt");
    await pollUntil(() => calls.length > 0);
    assert.equal(calls.length, 1, "genau ein Sammel-Aufruf");
    assert.equal(calls[0].selection.BatchPicked.length, 3, "mit den drei gewählten Ids");
    assert.deepEqual(calls[0].target, { YnabCategoryId: CATEGORY_IDS.bahn }, "Ziel-Kategorie");

    // Der Toast nennt Zahl und übersprungene Buchungen; die Auswahl ist beendet.
    const toast = page.getByText("2 Buchungen nach Bahn verschoben, 1 übersprungen (Split)", { exact: true });
    await toast.waitFor();
    assert.equal(await visibleBar(page).count(), 0, "Auswahl und Leiste enden");
    await screenshotIfRequested(page, "pc-1440x900-kategorie-setzen-toast");

    // Rückgängig: ein Sammel-Befehl für die Gruppe ohne vorherige Kategorie, Toast ohne Aktion.
    await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).click();
    await page.getByText("Kategorie bei 2 Buchungen zurückgesetzt", { exact: true }).waitFor();
    assert.equal(calls.length, 2, "ein Sammel-Aufruf für die eine Gruppe");
    assert.equal(calls[1].target, null, "zurück zu ohne Kategorie");
    assert.equal(calls[1].selection.BatchPicked.length, 2, "mit genau den geänderten Buchungen");
    assert.equal(await page.locator('[data-toast-action="true"]', { hasText: "Rückgängig" }).count(), 0, "der Rücknahme-Toast hat keine Aktion");
  } finally {
    await context.close();
  }
});

test("Handy 390x844: Kategorie setzen öffnet das Sheet mit Zahl und Summe, ohne Haken (ledger-w29y6)", async () => {
  const calls = [];
  const { page, context } = await openLedger({ width: 390, height: 844 }, true, categoryBatchSetup(calls));
  try {
    const rowButton = (n) =>
      page.locator("button", { hasText: `Auswahlzeile ${String(n).padStart(2, "0")}` }).filter({ visible: true }).first();
    await longPress(rowButton(1));
    await rowButton(2).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    await bar.getByRole("button", { name: "Kategorie setzen", exact: true }).click();
    const list = page.locator('[role="listbox"]').filter({ visible: true });
    await list.waitFor();
    // Die Unterzeile des Sheets nennt Zahl und Summe der Auswahl, nicht Payee und Betrag.
    const subtitle = page.getByText(/^2 Buchungen · -20,00/).filter({ visible: true });
    assert.equal(await subtitle.count(), 1, "Unterzeile nennt Zahl und Summe");
    assert.equal(await list.locator('[aria-selected="true"]').count(), 0, "kein Haken");
    assert.equal(await list.count(), 1, "nur eine Liste, kein zweiter Picker");
    await screenshotIfRequested(page, "handy-390x844-kategorie-setzen-sheet");
    await list.getByText("Lebensmittel", { exact: true }).click();
    await page.getByText("2 Buchungen nach Lebensmittel verschoben", { exact: true }).waitFor();
    assert.equal(calls.length, 1, "ein Sammel-Aufruf");
    assert.equal(await visibleBar(page).count(), 0, "Auswahl-Modus endet");
  } finally {
    await context.close();
  }
});
