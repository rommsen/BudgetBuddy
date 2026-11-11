// scripts/budget-page.test.mjs
//
// Wächter für `budgeting-k7wmd`, `budgeting-g8s3w` und `budgeting-h3k9d`: was nur der Browser an der
// Budget-Seite (#/budget) messen kann: echte Tastatur, Fokus, Scrollen und das Fable-Kompilat. Text,
// Zustand und Fehlerwege liegen in `src/Tests/BudgetPageTests.fs`. Seitliches Scrollen, Abschneiden,
// sticky Leisten und Trefferflächen prüft der Layout-Sweep (`scripts/layout-states.mjs`, Zustände budget*).
//
// Ausführen: node --test scripts/budget-page.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";

let frontend;
let frontendPort;
let browser;

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const category = (n, name, assigned, activity, available) => ({
  Id: `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`,
  Name: name,
  Assigned: eur(assigned),
  Activity: eur(activity),
  Available: eur(available),
  Underfunded: null,
  Target: null,
  Pause: null,
  OpenWriteBack: null,
});

const monthFixture = (month, toBeBudgeted) => ({
  Month: month,
  ToBeBudgeted: eur(toBeBudgeted),
  Groups: [
    {
      Name: "Alltag k7wmd",
      Categories: [
        category(1, "Essen k7wmd", 1600, -921.1, 1120.09),
        category(2, "Eine Kategorie mit einem sehr langen Namen, der am Handy abgeschnitten werden muss k7wmd", 100, -10, -12.5),
      ],
    },
    { Name: "Rücklagen k7wmd", Categories: [category(3, "Urlaub k7wmd", 550, 0, 2921.6)] },
  ],
});

// budgeting-kjsht: je eine Kategorie mit fehlendem, erfülltem und pausiertem Target, dazu eine
// ohne Target und eine mit dem längsten Text, den die Zeile am Handy tragen muss.
const withTarget = (base, target, underfunded, paused = false) => ({
  ...base,
  Target: target,
  Underfunded: eur(underfunded),
  Pause: paused ? "PausedInBb" : null,
  OpenWriteBack: null,
});
const monthly = { Goal: eur(1600), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null };
const targetFixture = (month) => ({
  Month: month,
  ToBeBudgeted: eur(10),
  Groups: [
    {
      Name: "Alltag",
      Categories: [
        withTarget(category(1, "Essen", 0, 0, 0), monthly, 1600),
        withTarget(category(2, "Kleidung", 1600, 0, 1600), monthly, 0),
        withTarget(category(3, "Reise", 0, 0, 0), monthly, 1600, true),
        category(4, "Macbook", 10, 0, 10),
        withTarget(
          category(5, "Eine Kategorie mit langem Namen und langem Target", 0, 0, 0),
          { Goal: eur(121600), Rhythm: { Months: 11 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null, CreatedIn: null },
          121600
        ),
      ],
    },
  ],
});

// Genug Zeilen, damit die Seite am Handy scrollt.
const longFixture = (month) => ({
  Month: month,
  ToBeBudgeted: eur(10),
  Groups: [{ Name: "Alltag", Categories: Array.from({ length: 40 }, (_, i) => category(i + 1, `Kategorie ${i + 1}`, 10, 0, 10)) }],
});

const CURRENT = "2026-10-01";
const WINDOW = ["2026-09-01", CURRENT, "2026-11-01"];

/// Der Rahmen zu einer Fixture: `CategoryOrder` nicht leer, sonst sortierte die Seite nach Name.
const frameFor = (fixture, months = WINDOW, current = CURRENT) => ({
  CurrentMonth: current,
  Months: months,
  CategoryOrder: fixture(current, 0).Groups.flatMap((g) => g.Categories.map((c) => c.Id)),
  MonthStartHidden: [],
});

const mockFrame = (page, frame, onCall = () => {}) =>
  page.route("**/api/BudgetApi/getBudgetFrame", (route) => {
    onCall();
    return route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: frame }) });
  });

async function openPage(width, height, { fail = false, requestedKeys = [], fixture = monthFixture, months = WINDOW, current = CURRENT, hasTouch = false } = {}) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let failNext = fail;
  const frames = { count: 0 };
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/getCategoryMonthEntries", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: [] }) })
  );
  await mockFrame(page, frameFor(fixture, months, current), () => {
    frames.count += 1;
  });
  await page.route("**/api/BudgetApi/getRecentMoveCategories", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: [] }) })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    requestedKeys.push(key);
    if (failNext) {
      failNext = false;
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "YNAB rate limit exceeded. Retry after 60 seconds" }) });
      return;
    }
    const month = fixture(key, -12.34);
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: month }) });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, requestedKeys, frames };
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

// Welche Beträge in welcher Zeile stehen und dass ein negatives Zu verteilen kein Fehler ist, prüft
// BudgetPageTests ("Unter Zugewiesen, Aktivität und Verfügbar …", "Ein negatives Zu verteilen …").
// Hier bleiben die Anfrage über den echten Proxy, das Format im Fable-Kompilat und das Layout.
test("[fable] Handy: der laufende Monat wird angefragt, Beträge deutsch formatiert, Gruppen mit Zeilen, kein horizontales Scrollen", async () => {
  const { context, page, requestedKeys } = await openPage(390, 844);
  try {
    assert.deepEqual(requestedKeys, [CURRENT], "der laufende Monat ist die Vorgabe");
    const box = page.locator('[data-testid="budget-to-be-budgeted"]');
    const text = (await box.innerText()).replace(/\s+/g, " ");
    // Am Handy ohne € wie die Skizze (budgeting-m3x7f), so passt der Kopf in eine Zeile.
    assert.match(text, /-12,34$/, "negativ, deutsch formatiert, ohne €");
    assert.equal(await page.getByRole("heading", { name: "Budget" }).filter({ visible: true }).count(), 1);
    assert.ok(await page.getByText("Okt 2026").isVisible(), "Monat im Kopf, am Handy kurz");

    const groups = page.locator('[data-testid="budget-group"]').filter({ visible: true });
    assert.equal(await groups.count(), 2);
    const rows = page.locator('[data-testid="budget-category-row"]').filter({ visible: true });
    assert.equal(await rows.count(), 3);
    const firstRow = (await rows.first().innerText()).replace(/\s+/g, " ");
    assert.match(firstRow, /1\.120,09 €/, "Tausenderpunkt und Komma im Kompilat");

    const overflow = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
    }));
    assert.ok(overflow.scroll <= overflow.client, `kein horizontales Scrollen (scrollWidth ${overflow.scroll} > clientWidth ${overflow.client})`);
    assert.equal(
      await page.locator("table").filter({ visible: true }).count(),
      0,
      "am Handy keine Tabelle"
    );
  } finally {
    await context.close();
  }
});

const monthName = (page) => page.locator('[data-testid="budget-month-name"]');
const previousArrow = (page) => page.locator('[data-testid="budget-month-previous"]');

// budgeting-g8s3w: eine Zeile mit Rot und Gelb, eine nur mit fehlt, eine ohne alles.
const filterFixture = (month) => ({
  Month: month,
  ToBeBudgeted: eur(10),
  Groups: [
    {
      Name: "Alltag",
      Categories: [
        withTarget(category(1, "Essen", 0, -30, -30), monthly, 1600),
        withTarget(category(2, "Kleidung", 0, 0, 0), monthly, 50),
        category(3, "Macbook", 10, 0, 10),
      ],
    },
    { Name: "Rücklagen", Categories: [category(4, "Urlaub", 10, 0, 10)] },
  ],
});

const tabsOf = (scope) => scope.locator('[data-testid="inspector-tabs"]').getByRole("tab");

const pickRow = (page, name) => visibleRows(page).filter({ has: page.getByText(name, { exact: true }) }).click();

const sheet = (page) => page.locator(".bottom-sheet.fixed-height");

test("[tastatur] Tastatur am PC: Pfeile wechseln die Kategorie und die Liste scrollt mit", async () => {
  const { context, page } = await openPage(1440, 700, { fixture: longFixture });
  try {
    const panel = page.locator('[data-testid="budget-inspector"]');
    await pickRow(page, "Kategorie 1");
    await panel.getByRole("heading", { name: "Kategorie 1", exact: true }).waitFor();
    await page.locator('[aria-label="Kategorien"]').focus();
    for (let i = 2; i <= 30; i += 1) {
      await page.keyboard.press("ArrowDown");
      await panel.getByRole("heading", { name: `Kategorie ${i}`, exact: true }).waitFor();
    }
    await page.waitForFunction(() => {
      const row = document.querySelector("tr[aria-current='true']").getBoundingClientRect();
      return row.top >= 0 && row.bottom <= window.innerHeight;
    });
    assert.ok((await page.evaluate(() => window.scrollY)) > 0, "die Seite ist mitgescrollt");

    await page.keyboard.press("ArrowUp");
    await panel.getByRole("heading", { name: "Kategorie 29", exact: true }).waitFor();
  } finally {
    await context.close();
  }
});

const searchInput = (page) => page.getByPlaceholder("Kategorie suchen…");
const visibleRows = (page) => page.locator('[data-testid="budget-category-row"]').filter({ visible: true });

// ---------------- budgeting-p4n6r: Review-Korrekturen ----------------

/// Hält die nächsten Antworten auf `getBudgetMonth` zurück, bis `release` läuft.
const holdMonths = async (page, fixture) => {
  let release;
  const gate = new Promise((resolve) => {
    release = resolve;
  });
  await page.route("**/api/BudgetApi/getBudgetMonth", async (route) => {
    await gate;
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    await route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: fixture(key, 99) }) });
  });
  return release;
};

/// Wartet, bis `scrollY` 300 ms lang stehen bleibt.
const settledScroll = (page) =>
  page.evaluate(
    () =>
      new Promise((resolve) => {
        let last = window.scrollY;
        let since = performance.now();
        const check = () => {
          if (window.scrollY !== last) {
            last = window.scrollY;
            since = performance.now();
          }
          if (performance.now() - since >= 300) resolve();
          else setTimeout(check, 30);
        };
        check();
      })
  );

// Bug 2: `select` setzte `Loading`, Liste, Filter, Zu verteilen und Panel verschwanden bis zur Antwort.
test("[scroll] Blättern am PC: Liste, Inspektor und Tabs bleiben stehen, die Liste behält Fokus und Scrollposition, kein Spinner", async () => {
  const { context, page } = await openPage(1440, 700, { fixture: longFixture });
  try {
    const panel = page.locator('[data-testid="budget-inspector"]');
    await pickRow(page, "Kategorie 30");
    await panel.getByRole("heading", { name: "Kategorie 30", exact: true }).waitFor();
    // Die gewählte Zeile rückt per `scrollIntoView` ins Bild, und das Scrollen ist animiert. Mitten in der
    // Animation gelesen, sähe es aus, als spränge die Seite beim Blättern.
    await settledScroll(page);
    const list = page.locator('[aria-label="Kategorien"]');
    await list.focus();
    await settledScroll(page);
    const scrollBefore = await page.evaluate(() => window.scrollY);
    assert.ok(scrollBefore > 0, `die Seite ist vor dem Blättern gescrollt (${scrollBefore})`);
    const release = await holdMonths(page, longFixture);

    // Per Skript geklickt: ein echter Klick nähme der Liste den Fokus.
    await previousArrow(page).evaluate((el) => el.click());
    await monthName(page).getByText("September 2026").waitFor();
    const area = page.locator('[data-testid="budget-list-area"]');
    assert.equal(await area.getAttribute("aria-busy"), "true", "der Wechsel läuft");
    assert.equal(await page.getByText("Lade Budget").count(), 0, "kein Spinner");
    assert.equal(await tabsOf(panel).count(), 4, "die Tabs bleiben");
    assert.ok(await panel.getByRole("heading", { name: "Kategorie 30", exact: true }).isVisible(), "der Inspektor bleibt");
    assert.ok(await page.locator('[data-testid="budget-to-be-budgeted"]').isVisible(), "Zu verteilen bleibt");
    assert.ok(await searchInput(page).isVisible(), "die Suche bleibt");
    await page.waitForFunction(() => Number(getComputedStyle(document.querySelector('[data-testid="budget-list-area"]')).opacity) < 1);
    // Am PC stehen Suche und Chips im Kopf, außerhalb der Liste. Gemessen wird der Weg bis zum Dokument.
    await page.waitForFunction(
      () =>
        ["budget-category-search", "budget-filter"].every((id) => {
          let opacity = 1;
          for (let el = document.querySelector(`[data-testid="${id}"]`); el; el = el.parentElement) opacity *= Number(getComputedStyle(el).opacity);
          return opacity < 1;
        }),
      undefined,
      { timeout: 3000 }
    );
    assert.ok(await searchInput(page).isEnabled(), "die Suche bleibt bedienbar");

    release();
    await page.waitForFunction(() => document.querySelector('[data-testid="budget-list-area"]').getAttribute("aria-busy") === "false");
    assert.equal(await list.evaluate((el) => el === document.activeElement), true, "die Liste hat den Fokus");
    await settledScroll(page);
    assert.equal(await page.evaluate(() => window.scrollY), scrollBefore, "die Seite springt nicht");
  } finally {
    await context.close();
  }
});

test("[scroll] Panel: 30 Buchungen bei 700 px Höhe scrollen im Tab, das Panel bleibt in der Sichthöhe", async () => {
  const { context, page } = await openPage(1440, 700, { fixture: longFixture });
  try {
    const lines = Array.from({ length: 30 }, (_, i) => ({
      EntryId: `00000000-0000-0000-0001-${String(i + 1).padStart(12, "0")}`,
      AccountId: "00000000-0000-0000-0002-000000000001",
      AccountName: "Testkonto",
      Date: "2026-10-05T00:00:00.0000000",
      Payee: `Empfänger ${i + 1}`,
      Amount: eur(-1),
      IsSplitShare: false,
    }));
    await page.route("**/api/LedgerApi/getCategoryMonthEntries", (route) =>
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: lines }) })
    );
    const panel = page.locator('[data-testid="budget-inspector"]');
    await pickRow(page, "Kategorie 1");
    const empty = await panel.boundingBox();
    await tabsOf(panel).filter({ hasText: "Buchungen" }).click();
    await panel.getByText("Empfänger 30").waitFor({ state: "attached" });
    await page.evaluate(() => window.scrollTo(0, 400));
    await page.waitForFunction(() => window.scrollY >= 400);
    const box = await panel.boundingBox();
    assert.equal(Math.round(box.height), Math.round(empty.height), "die Höhe bleibt");
    assert.ok(box.y + box.height <= 700, `das Panel endet in der Sichthöhe (${JSON.stringify(box)})`);
    const scrolls = await panel.locator(".overflow-y-auto").evaluate((el) => el.scrollHeight > el.clientHeight);
    assert.ok(scrolls, "die Liste scrollt im Panel");
  } finally {
    await context.close();
  }
});

// Fix 5850313 (budgeting-p4n6r): beim Schließen verschwand der Inhalt sofort, während das Sheet noch
// ausfuhr, und das ausfahrende Sheet blieb bedienbar. Die Ausfahrt selbst ist Animation (Doktrin Punkt 5);
// hier bleibt, dass das Sheet sofort aus der Fokusfolge fällt (`inert`) und dabei seinen Inhalt behält.
// Dass danach die Auswahl weg ist, prüft BudgetPageTests ("Schließen beendet die Auswahl").
test("[fokus] Sheet: beim Schließen ist es sofort inert und behält seinen Inhalt bis zum Ende der Ausfahrt", async () => {
  const { context, page } = await openPage(390, 844, { fixture: filterFixture });
  try {
    await pickRow(page, "Essen");
    await sheet(page).getByRole("heading", { name: "Essen" }).waitFor();
    await page.locator(".bottom-sheet.active .sheet-close").click();
    const closing = await page.waitForFunction(() => {
      const el = document.querySelector(".bottom-sheet.fixed-height");
      return el.hasAttribute("inert") ? { title: el.querySelector("h3")?.textContent ?? "" } : false;
    });
    assert.deepEqual(await closing.jsonValue(), { title: "Essen" }, "inert, der Titel steht noch");
    await page.locator('[data-testid="budget-inspector"]').waitFor({ state: "detached" });
  } finally {
    await context.close();
  }
});

test("[scroll] Sheet: viele offene Verschiebungen scrollen im festen Bereich, der Inhalt behält Platz", async () => {
  const { context, page } = await openPage(390, 844, { fixture: filterFixture });
  try {
    await pickRow(page, "Essen");
    await page.locator('[data-testid="budget-inspector"]').waitFor();
    const measured = await sheet(page).evaluate((el) => {
      const pinned = el.querySelector(".sheet-pinned");
      const filler = document.createElement("div");
      filler.style.height = "800px";
      pinned.prepend(filler);
      const result = { body: el.querySelector(".sheet-body").getBoundingClientRect().height, scrolls: pinned.scrollHeight > pinned.clientHeight };
      filler.remove();
      return result;
    });
    assert.ok(measured.body >= 120, `der Inhalt behält Platz (${JSON.stringify(measured)})`);
    assert.ok(measured.scrolls, "der feste Bereich scrollt in sich");
  } finally {
    await context.close();
  }
});

test("[tastatur] Barrierefreiheit: Handy-Zeile mit aria-current, Monat und Suche mit Namen, Tabs mit Panel und Pfeiltasten", async () => {
  const { context, page } = await openPage(390, 844, { fixture: filterFixture });
  try {
    assert.equal(await searchInput(page).getAttribute("aria-label"), "Kategorie suchen");
    assert.equal(await monthName(page).getAttribute("aria-label"), "Oktober 2026, zum laufenden Monat");
    await pickRow(page, "Essen");
    const tabs = tabsOf(sheet(page));
    await tabs.first().waitFor();
    assert.equal(await visibleRows(page).filter({ hasText: "Essen" }).getAttribute("aria-current"), "true");

    assert.deepEqual(
      await tabs.evaluateAll((all) => all.map((el) => el.tabIndex)),
      [0, -1, -1, -1],
      "nur der aktive Tab liegt in der Tab-Folge"
    );
    const panelId = await tabs.first().getAttribute("aria-controls");
    const tabpanel = sheet(page).getByRole("tabpanel");
    assert.equal(await tabpanel.getAttribute("id"), panelId);
    assert.equal(await tabpanel.getAttribute("aria-labelledby"), await tabs.first().getAttribute("id"));

    await tabs.first().focus();
    await page.keyboard.press("ArrowRight");
    await page.waitForFunction(() => document.activeElement.textContent === "Buchungen" && document.activeElement.getAttribute("aria-selected") === "true");
    await page.keyboard.press("ArrowLeft");
    await page.keyboard.press("ArrowLeft");
    await page.waitForFunction(() => document.activeElement.textContent === "Geld" && document.activeElement.tabIndex === 0);
    assert.equal(await tabpanel.getAttribute("aria-labelledby"), await tabs.first().getAttribute("id"), "links am Rand bleibt es stehen");

    for (let i = 0; i < 4; i += 1) await page.keyboard.press("ArrowRight");
    await page.waitForFunction(() => document.activeElement.textContent === "Target" && document.activeElement.tabIndex === 0);
    await page.keyboard.press("ArrowRight");
    await page.evaluate(() => new Promise(requestAnimationFrame));
    await page.evaluate(() => new Promise(requestAnimationFrame));
    assert.equal(await tabpanel.getAttribute("aria-labelledby"), await tabs.nth(3).getAttribute("id"), "rechts am Rand bleibt es stehen");
  } finally {
    await context.close();
  }
});

// ---------------- budgeting-h3k9d: dichter, Tastatur seitenweit, Überzogen, ˄ ˅ ----------------

const selectedRowText = (page) => page.locator("tr[aria-current='true']").innerText();
const activeTab = (panel) => panel.getByRole("tab", { selected: true }).innerText();
/// Zwei Bilder Zeit: eine Taste, die etwas auslöste, hätte bis dahin gerendert.
const twoFrames = async (page) => {
  await page.evaluate(() => new Promise(requestAnimationFrame));
  await page.evaluate(() => new Promise(requestAnimationFrame));
};

test("[tastatur] Tastatur seitenweit: nach einem Klick auf einen Filter wählt Runter die erste sichtbare, Hoch die letzte Zeile", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await page.locator('[data-testid="budget-filter"]').getByRole("button", { name: /^Fehlt/ }).click();
    await page.getByText("Macbook").waitFor({ state: "hidden" });
    await page.keyboard.press("ArrowDown");
    await panel.getByRole("heading", { name: "Essen", exact: true }).waitFor();
    await page.keyboard.press("ArrowDown");
    await panel.getByRole("heading", { name: "Kleidung", exact: true }).waitFor();

    // Ohne fokussiertes Element (Klick auf eine Zeile fokussiert im `tr` nichts) gelten die Tasten auch.
    await page.evaluate(() => document.activeElement.blur());
    await page.keyboard.press("ArrowUp");
    await panel.getByRole("heading", { name: "Essen", exact: true }).waitFor();

    // Rechts wechselt genau einen Tab, auch mit Fokus auf einem Tab: `Tabs` nimmt die Taste zuerst.
    await page.keyboard.press("ArrowRight");
    await panel.getByRole("tab", { name: "Buchungen", selected: true }).waitFor();
    await panel.getByRole("tab", { name: "Buchungen" }).focus();
    await page.keyboard.press("ArrowRight");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Historie", "ein Druck, ein Tab");
    assert.equal(await page.evaluate(() => document.activeElement.textContent), "Historie", "der Fokus geht mit");

    // Mit Alt springt der Browser zurück: die Seite darf die Taste nicht nehmen.
    await page.keyboard.press("Alt+ArrowLeft");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Historie", "Alt und Pfeil gehören dem Browser");
  } finally {
    await context.close();
  }

  const fresh = await openPage(1440, 900, { fixture: filterFixture });
  try {
    await fresh.page.keyboard.press("ArrowRight");
    await twoFrames(fresh.page);
    assert.match(await fresh.page.locator('aside[data-testid="budget-inspector"]').innerText(), /Kategorie wählen/, "ohne Auswahl tut Rechts nichts");
    await fresh.page.locator('[data-testid="budget-filter"]').getByRole("button", { name: /^Überzogen/ }).click();
    await fresh.page.getByText("Macbook").waitFor({ state: "hidden" });
    await fresh.page.keyboard.press("ArrowUp");
    await fresh.page.locator('aside[data-testid="budget-inspector"]').getByRole("heading", { name: "Essen", exact: true }).waitFor();
  } finally {
    await fresh.context.close();
  }

  // Die Tasten hängen am Panel: unter 1024 px wirken sie nicht.
  const tablet = await openPage(768, 900, { fixture: filterFixture });
  try {
    await tablet.page.keyboard.press("ArrowDown");
    await twoFrames(tablet.page);
    assert.equal(await tablet.page.locator(".bottom-sheet.fixed-height.active").count(), 0, "kein Sheet öffnet sich");
  } finally {
    await tablet.context.close();
  }
});

// Der Tastendruck im echten DOM: der Fokus bleibt im Suchfeld, der Cursor steht still, der Tab wechselt.
// Die Regel selbst prüft `pageKeyAction` unter .NET.
test("[tastatur] Tastatur in Feldern: im Suchfeld wechseln Hoch und Runter die Kategorie, Links und Rechts den Tab", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await searchInput(page).click();
    await page.keyboard.type("e");
    await visibleRows(page).filter({ hasText: "Essen" }).waitFor();
    const shown = await visibleRows(page).count();
    await page.keyboard.press("ArrowDown");
    await panel.getByRole("heading", { name: "Essen", exact: true }).waitFor();
    assert.equal(await searchInput(page).inputValue(), "e", "der Text bleibt");
    assert.equal(await page.evaluate(() => document.activeElement.getAttribute("aria-label")), "Kategorie suchen", "der Fokus bleibt");
    assert.equal(await visibleRows(page).count(), shown);

    const caret = () => searchInput(page).evaluate((el) => el.selectionStart);
    assert.equal(await caret(), 1);
    await page.keyboard.press("ArrowRight");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Buchungen", "Rechts wechselt den Tab");
    assert.equal(await caret(), 1, "der Cursor steht still");
    assert.equal(await page.evaluate(() => document.activeElement.getAttribute("aria-label")), "Kategorie suchen", "der Fokus bleibt in der Suche");
    await page.keyboard.press("ArrowLeft");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Geld", "Links wechselt zurück");
    assert.equal(await caret(), 1, "der Cursor steht still");

    // Tippen ohne Klick in die Suche: das Zeichen landet im echten DOM im Feld. Ob eine Taste
    // dorthin gehört, prüft `typesIntoSearch` unter .NET.
    await page.evaluate(() => document.activeElement.blur());
    await page.keyboard.type("S");
    await twoFrames(page);
    assert.equal(await page.evaluate(() => document.activeElement.getAttribute("aria-label")), "Kategorie suchen", "das Zeichen holt den Fokus in die Suche");
    assert.equal(await searchInput(page).inputValue(), "eS", "das Zeichen steht am Ende der Suche");
  } finally {
    await context.close();
  }
});

/// Nach jeder einzelnen Taste geprüft: Hin und Zurück höben sich sonst auf.
const stayOnEssen = async (page, panel, label) => {
  for (const key of ["ArrowDown", "ArrowUp", "ArrowRight", "ArrowLeft"]) {
    await page.keyboard.press(key);
    await twoFrames(page);
    assert.match(await selectedRowText(page), /Essen/, `${label}, ${key}: die Zeile bleibt`);
    assert.equal(await activeTab(panel), "Geld", `${label}, ${key}: der Tab bleibt`);
  }
};

test("[tastatur] Tastatur: bei offenem Dialog passiert auf der Seite nichts, die Felder darin behalten ihre Tasten", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await pickRow(page, "Essen");
    await panel.getByRole("heading", { name: "Essen", exact: true }).waitFor();

    const nothingHappens = (label) => stayOnEssen(page, panel, label);

    // Fokus auf dem Umschalter wechselt die Tabs: das prüft der Tab-Wächter. Hier zählen die Felder.
    await page.locator('[data-testid="money-mode"]').filter({ hasText: "Abgeben" }).click();
    await page.locator('[data-testid="transfer-amount"] input').click();
    await nothingHappens("Betragsfeld");

    await page.locator('[data-testid="transfer-to"]').click();
    await page.getByPlaceholder(/suchen/i).filter({ visible: true }).last().waitFor();
    await nothingHappens("Picker");
  } finally {
    await context.close();
  }

  const second = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const { page } = second;
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await pickRow(page, "Essen");
    await panel.getByRole("heading", { name: "Essen", exact: true }).waitFor();
    // budgeting-m3x7f: Targets anwenden öffnet sich über das Feld der Leiste Monatserster.
    await page.locator('[data-testid="month-start-apply"]').click();
    await page.getByRole("heading", { name: "Targets anwenden" }).waitFor();
    await stayOnEssen(page, panel, "Targets anwenden");
  } finally {
    await second.context.close();
  }
});

// budgeting-v6m3z: Über den Ausgaben-Balken steht eine Wertzeile mit Monat und genauem Betrag des gewählten
// Balkens. Welcher Text dort steht, prüft BudgetHistoryClientTests ("Die Wertzeile je Balken …"); hier nur,
// was der Browser entscheidet: Fokus per Tipp und Tab-Taste, Maße am Handy.
const SPENDING_MONTHS = ["2026-05-01", "2026-06-01", "2026-07-01", "2026-08-01", "2026-09-01", CURRENT];

async function openSpending(width, { hasTouch = false } = {}) {
  const opened = await openPage(width, 844, { hasTouch });
  const { page } = opened;
  const history = {
    Entries: [],
    Spending: SPENDING_MONTHS.map((month, i) => ({ Month: month, Activity: eur(i === 3 ? 85 : -1234.56 * (i + 1)) })),
  };
  await page.route("**/api/BudgetApi/getCategoryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: history }) })
  );
  await pickRow(page, "Essen k7wmd");
  await tabsOf(sheet(page)).filter({ hasText: "Historie" }).click();
  await sheet(page).locator('[data-testid="month-bar"]').first().waitFor();
  return opened;
}

/// Die Texte der sichtbaren Werte in der Wertzeile, ohne Auswahl keiner.
const shownValues = (page) =>
  sheet(page).evaluate((el) =>
    [...el.querySelectorAll('[data-testid="month-bar-value"]')]
      .filter((v) => getComputedStyle(v).visibility === "visible")
      .map((v) => v.textContent)
  );

test("[fokus] Ausgaben-Balken: Tipp und Tab-Taste setzen den Fokus, die Wertzeile zeigt den gewählten Balken, ein Tipp daneben leert sie", async () => {
  const { context, page } = await openSpending(390, { hasTouch: true });
  try {
    const bars = sheet(page).locator('[data-testid="month-bar"]');
    assert.deepEqual(await shownValues(page), [], "ohne Auswahl leer");

    await bars.nth(1).tap();
    assert.equal(await bars.nth(1).evaluate((el) => el === document.activeElement), true, "der Tipp setzt den Fokus");
    // budgeting-r3wq8: den Fokus trägt das innere Element mit Rolle und Namen, das `li` bleibt ein reines
    // Listenelement ohne tabindex. Der Wortlaut des Namens steht im .NET-Test (`monthBarAriaLabel`), hier
    // nur, dass er da ist und kein "·" trägt (budgeting-c6vn4, Q5).
    const focused = await page.evaluate(() => {
      const el = document.activeElement;
      return { role: el.getAttribute("role"), label: el.getAttribute("aria-label"), parent: el.parentElement.tagName, parentTab: el.parentElement.hasAttribute("tabindex") };
    });
    assert.deepEqual({ ...focused, label: undefined }, { role: "img", label: undefined, parent: "LI", parentTab: false }, "Fokus am inneren Element");
    assert.ok(focused.label && !focused.label.includes("·"), `ein Name ohne "·" (${focused.label})`);
    assert.deepEqual(await shownValues(page), ["Juni 2026 · 2.469,12 €"], "Wert des angetippten Balkens");

    await bars.nth(3).tap();
    assert.deepEqual(await shownValues(page), ["August 2026 · +85,00 €"], "ein anderer Balken wechselt den Wert");

    await sheet(page).locator('[data-testid="history-spending"]').getByText("Ausgaben der letzten 6 Monate").tap();
    assert.equal(await bars.nth(3).evaluate((el) => el === document.activeElement), false, "der Tipp daneben nimmt den Fokus");
    assert.deepEqual(await shownValues(page), [], "Fokus weg, die Zeile ist leer");
  } finally {
    await context.close();
  }

  // Tastatur ohne Maus über den Balken: die Maus steht auf der Überschrift, Tab geht von dort weiter.
  const keyboard = await openSpending(390);
  try {
    const kpage = keyboard.page;
    const bars = sheet(kpage).locator('[data-testid="month-bar"]');
    const heading = sheet(kpage).locator('[data-testid="history-spending"]').getByText("Ausgaben der letzten 6 Monate");
    await heading.click();
    await kpage.keyboard.press("Tab");
    assert.equal(await bars.nth(0).evaluate((el) => el === document.activeElement), true, "Tab erreicht den ersten Balken");
    assert.deepEqual(await shownValues(kpage), ["Mai 2026 · 1.234,56 €"], "Wert des fokussierten Balkens");
    await kpage.keyboard.press("Tab");
    assert.deepEqual(await shownValues(kpage), ["Juni 2026 · 2.469,12 €"], "Tab wechselt den Wert");
    await heading.click();
    assert.deepEqual(await shownValues(kpage), [], "Fokus weg, die Zeile ist leer");
  } finally {
    await keyboard.context.close();
  }
});
