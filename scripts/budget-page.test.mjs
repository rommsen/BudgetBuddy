// scripts/budget-page.test.mjs
//
// Wächter für `budgeting-k7wmd`, `budgeting-g8s3w` und `budgeting-h3k9d`: was nur der Browser an der
// Budget-Seite (#/budget) messen kann. Am Handy Zeilen ohne horizontales Scrollen, am PC eine Tabelle
// neben dem Panel, feste Höhen, sticky Leisten, echte Tastatur, Fokus und Trefferflächen. Text,
// Zustand und Fehlerwege liegen in `src/Tests/BudgetPageTests.fs`.
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
const withTarget = (base, target, underfunded, snoozed = false) => ({
  ...base,
  Target: target,
  Underfunded: eur(underfunded),
  Pause: snoozed ? "PausedInYnab" : null,
});
const monthly = { Goal: eur(1600), Rhythm: { Months: 1 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null };
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
          { Goal: eur(121600), Rhythm: { Months: 11 }, Kind: "Need", Date: null, NeedsWholeAmount: true, Day: null },
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
    assert.match(text, /-12,34 €/, "negativ, deutsch formatiert");
    assert.equal(await page.getByRole("heading", { name: "Budget" }).filter({ visible: true }).count(), 1);
    assert.ok(await page.getByText("Oktober 2026").isVisible(), "Monat im Kopf");

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

// Darunter ist der Name auch gekürzt nicht mehr zu erkennen.
const MIN_NAME_WIDTH = 80;

const targetTexts = (page) =>
  page.evaluate(() =>
    [...document.querySelectorAll('[data-testid="budget-category-row"]')]
      .filter((row) => row.offsetParent !== null)
      .map((row) => row.querySelector('[data-testid="budget-target"]')?.innerText.replace(/\s*\n\s*(·\s*)?/g, " · ") ?? null)
  );

// Bug F3: `truncate` auf der ganzen Zeile schnitt zuerst "fehlt …" ab, also genau die Aussage.
test("[layout] Handy: dritte Zeile mit Target, fehlt oder pausiert, nichts abgeschnitten, bei 320 px bleibt der Status ganz sichtbar", async () => {
  const { page, context } = await openPage(390, 844, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  assert.deepEqual(await targetTexts(page), [
    "Target 1.600,00 €/Monat · fehlt 1.600,00 €",
    "Target 1.600,00 €/Monat",
    "Target 1.600,00 €/Monat · pausiert",
    null,
    "Target 121.600,00 € alle 11 Monate · fehlt 121.600,00 €",
  ]);
  const clipped = await page.evaluate(() => {
    const lines = [...document.querySelectorAll('[data-testid="budget-target"]')].filter((el) => el.offsetParent !== null);
    const spans = lines.flatMap((el) => [...el.querySelectorAll("span")]);
    return spans.filter((el) => el.scrollWidth > el.clientWidth).map((el) => el.innerText);
  });
  assert.deepEqual(clipped, [], "keine Target-Zeile wird abgeschnitten");
  await context.close();

  const narrow = await openPage(320, 700, { fixture: targetFixture });
  await narrow.page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  const status = await narrow.page.evaluate(() => {
    const line = [...document.querySelectorAll('[data-testid="budget-target"]')].filter((el) => el.offsetParent !== null).at(-1);
    const carrier = [...line.parentElement.querySelectorAll("span")].find((el) => !el.children.length && el.innerText.includes("fehlt"));
    return { clipped: carrier.scrollWidth > carrier.clientWidth, right: carrier.getBoundingClientRect().right, width: window.innerWidth };
  });
  assert.equal(status.clipped, false, "der Status wird nicht abgeschnitten");
  assert.ok(status.right <= status.width, `der Status liegt im Viewport (${JSON.stringify(status)})`);
  await narrow.context.close();
});

test("[layout] PC: Spalte Target direkt hinter Kategorie zeigt denselben Inhalt, Gruppenzeile spannt alle Spalten", async () => {
  const { page, context } = await openPage(1440, 900, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  const headers = await page.locator("table thead th").allInnerTexts();
  assert.equal(headers[1].toLowerCase(), "target");
  assert.equal(headers.length, 5);
  assert.equal(await page.locator("table thead tr").count(), 1, "genau eine Kopfzeile, keine verschachtelte");
  assert.deepEqual(await targetTexts(page), [
    "1.600,00 €/Monat",
    "1.600,00 €/Monat",
    "1.600,00 €/Monat · pausiert",
    null,
    "121.600,00 € alle 11 Monate",
  ]);
  assert.equal(await page.locator("table tbody tr td[colspan='5']").count(), 1);
  const fonts = await page.locator('[data-testid="budget-target"]:visible').first().evaluate((cell) => {
    const [amount, rhythm] = cell.querySelectorAll("span span");
    return [amount, rhythm].map((el) => getComputedStyle(el).fontFamily.toLowerCase());
  });
  assert.match(fonts[0], /mono/, "der Betrag steht in Mono");
  assert.doesNotMatch(fonts[1], /mono/, "der Rhythmus steht nicht in Mono");
  await context.close();
});

/// Der berechnete Wert eines Farb-Tokens, zum Vergleich mit `getComputedStyle(...).color`.
const tokenColor = (page, token) =>
  page.evaluate((name) => {
    const probe = document.createElement("span");
    probe.style.color = `var(${name})`;
    document.body.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  }, token);

const monthName = (page) => page.locator('[data-testid="budget-month-name"]');
const previousArrow = (page) => page.locator('[data-testid="budget-month-previous"]');
const nextArrow = (page) => page.locator('[data-testid="budget-month-next"]');

// Welcher Pfeil am Rand fehlt, wann der Name zurückführt und dass Blättern den Rahmen nicht neu liest,
// prüft BudgetPageTests ("Am Rand fehlt der Pfeil …", "Blättern liest nur den Monat …").
test("[layout] Monate: am Rand bleibt der Platz des Pfeils frei, der Name springt nicht, ist teal und führt per Klick zurück", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    const atStart = await monthName(page).boundingBox();
    const teal = await tokenColor(page, "--color-neon-teal");
    const colorOf = () => monthName(page).evaluate((el) => getComputedStyle(el).color);
    assert.notEqual(await colorOf(), teal, "im laufenden Monat ist der Name nicht teal");

    await previousArrow(page).click();
    await monthName(page).getByText("September 2026").waitFor();
    const atEdge = await monthName(page).boundingBox();
    assert.equal(Math.round(atEdge.x), Math.round(atStart.x), "der Name springt nicht");
    assert.equal(await colorOf(), teal, "der Name ist teal, solange ein anderer Monat gezeigt wird");

    await monthName(page).click();
    await monthName(page).getByText("Oktober 2026").waitFor();
  } finally {
    await context.close();
  }
});

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

test("[layout] Filter: fehlt steht in Gelb, Verfügbar im Minus in Rot", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    await page.locator('[data-testid="budget-filter"]').getByRole("button").nth(2).click();
    await page.getByText("Macbook").waitFor({ state: "hidden" });
    const colors = await visibleRows(page).first().evaluate((row) => ({
      missing: getComputedStyle(row.querySelector('[data-testid="budget-missing"]')).color,
      available: getComputedStyle(row.querySelector('[data-testid="budget-available"] span')).color,
    }));
    assert.equal(colors.missing, await tokenColor(page, "--color-calm-yellow"), "fehlt steht in Gelb");
    assert.equal(colors.available, await tokenColor(page, "--color-neon-red"), "Verfügbar im Minus steht in Rot");
  } finally {
    await context.close();
  }
});

const tabsOf = (scope) => scope.locator('[data-testid="inspector-tabs"]').getByRole("tab");

const pickRow = (page, name) => visibleRows(page).filter({ has: page.getByText(name, { exact: true }) }).click();

test("[layout] Inspektor am PC: gleiche Höhe in allen vier Tabs, die gewählte Zeile ist markiert", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('[data-testid="budget-inspector"]');
    assert.match(await panel.innerText(), /Kategorie wählen/);
    const empty = await panel.boundingBox();
    await pickRow(page, "Essen");
    await panel.getByRole("heading", { name: "Essen" }).waitFor();
    assert.equal(await visibleRows(page).filter({ hasText: "Essen" }).getAttribute("aria-current"), "true");
    const heights = [empty.height];
    for (const tab of await tabsOf(panel).all()) {
      await tab.click();
      heights.push((await panel.boundingBox()).height);
    }
    const rounded = heights.map(Math.round);
    assert.equal(new Set(rounded).size, 1, `gleiche Höhe (${rounded})`);
    assert.equal(rounded[0], 640, "feste Höhe");
    assert.equal((await panel.boundingBox()).width, 340, "feste Breite");
  } finally {
    await context.close();
  }
});

/// Der Fokus-Ring des fokussierten Elements: Rundung, Ringstil, Versatz und der Abstand des Knopfs
/// zum Rand der Pille (`gap`, je Seite: Abstand zwischen der Außenkante des Knopfs und der
/// Innenkante des Pillenrands, 0 heißt: der Ring liegt direkt auf dem Rand).
const focusRing = (page) =>
  page.evaluate(() => {
    const el = document.activeElement;
    const style = getComputedStyle(el);
    const rect = el.getBoundingClientRect();
    const pill = el.parentElement;
    const pillRect = pill.getBoundingClientRect();
    const border = parseFloat(getComputedStyle(pill).borderTopWidth);
    return {
      text: el.textContent,
      focusVisible: el.matches(":focus-visible"),
      radius: parseFloat(style.borderTopLeftRadius),
      height: rect.height,
      outlineStyle: style.outlineStyle,
      outlineOffset: parseFloat(style.outlineOffset),
      gap: Math.max(
        rect.left - pillRect.left - border,
        rect.top - pillRect.top - border,
        pillRect.bottom - border - rect.bottom
      ),
    };
  });

/// Wie der Tab aussieht: Ring, Fläche, Schatten. Die Schriftfarbe bleibt außen vor, sie blendet
/// über `transition-colors` ein.
const tabLook = (tab) =>
  tab.evaluate((el) => {
    const style = getComputedStyle(el);
    return { outline: style.outlineStyle, background: style.backgroundColor, shadow: style.boxShadow };
  });

// Regression (design-system-k4f9t, design-system-w7c2h): am Tab lag erst ein Ring außen, dann ein
// Kasten innen, je nach Klickfolge sah der Tab verschieden aus. Am aktiven Chip standen der teal
// Rand und der Ring mit Abstand als zwei Linien da.
test("[layout] Fokus am PC: der Tab Geld sieht per Pfeiltaste aus wie per Seiten-Kürzel gewählt, am Chip Überzogen liegt der Ring auf dem Rand der Pille, nach einem Mausklick fehlt er", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('[data-testid="budget-inspector"]');
    const geld = tabsOf(panel).first();
    await pickRow(page, "Essen");
    await panel.getByRole("heading", { name: "Essen" }).waitFor();

    await searchInput(page).click();
    await page.keyboard.type("e");
    await page.keyboard.press("ArrowRight");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Buchungen", "Rechts aus der Suche wechselt den Tab");
    await page.keyboard.press("ArrowLeft");
    await twoFrames(page);
    assert.equal(await activeTab(panel), "Geld", "Links aus der Suche wechselt zurück");
    const viaShortcut = await tabLook(geld);

    await geld.click();
    assert.equal((await focusRing(page)).focusVisible, false, "nach dem Mausklick auf den Tab kein Ring");
    await page.keyboard.press("ArrowRight");
    await page.waitForFunction(() => document.activeElement.textContent === "Buchungen");
    await page.keyboard.press("ArrowLeft");
    await page.waitForFunction(() => document.activeElement.textContent === "Geld");
    assert.equal(await geld.evaluate((el) => el.matches(":focus-visible")), true, "Tab: der Fokus kommt von der Tastatur");
    const viaArrow = await tabLook(geld);
    assert.equal(viaArrow.outline, "none", "Tab: kein Ring, der Strich zeigt Auswahl und Fokus");
    assert.deepEqual(viaArrow, viaShortcut, "Tab: per Pfeiltaste wie per Seiten-Kürzel, keine eigene Fläche");

    const chips = page.locator('[data-testid="budget-filter"]').getByRole("button");
    await chips.nth(2).click();
    assert.equal((await focusRing(page)).focusVisible, false, "nach dem Mausklick auf den Chip kein Ring");
    await page.keyboard.press("Shift+Tab");
    await page.keyboard.press("Tab");
    const chipRing = await focusRing(page);
    assert.match(chipRing.text, /^Überzogen/, "Chip: der Fokus steht auf Überzogen");
    assert.equal(await chips.nth(2).getAttribute("aria-pressed"), "true", "Chip: Überzogen ist aktiv");
    assert.equal(chipRing.focusVisible, true, "Chip: per Tastatur ist der Ring sichtbar");
    assert.notEqual(chipRing.outlineStyle, "none", "Chip: der Ring wird gezeichnet");
    assert.ok(chipRing.radius >= chipRing.height / 2, `Chip: Rundung wie die Pille (${chipRing.radius} px bei ${chipRing.height} px Höhe)`);
    assert.equal(chipRing.outlineOffset, 0, "Chip: kein Abstand zwischen Knopf und Ring");
    assert.ok(chipRing.gap <= 0.5, `Chip: der Ring liegt auf dem Rand der Pille, eine Linie (Abstand ${chipRing.gap} px)`);
  } finally {
    await context.close();
  }
});

const sheet = (page) => page.locator(".bottom-sheet.fixed-height");

test("[layout] Sheet: 500 px hoch in allen Tabs mit 48 px Tabs, ˄ ˅ wechselt ohne Schließen, Kopf und Tabs bleiben beim Scrollen stehen, mit kleinerem --vvh schrumpft es", async () => {
  const { context, page } = await openPage(390, 844, { fixture: longFixture });
  try {
    await pickRow(page, "Kategorie 2");
    await sheet(page).getByRole("heading", { name: "Kategorie 2", exact: true }).waitFor();
    for (const tab of await tabsOf(sheet(page)).all()) {
      await tab.click();
      assert.equal((await sheet(page).boundingBox()).height, 500, "feste Höhe");
      assert.ok((await tab.boundingBox()).height >= 48, "Trefferfläche");
    }
    await page.locator('[data-testid="inspector-next"]').click();
    await sheet(page).getByRole("heading", { name: "Kategorie 3", exact: true }).waitFor();
    await page.locator('[data-testid="inspector-previous"]').click();
    await page.locator('[data-testid="inspector-previous"]').click();
    await sheet(page).getByRole("heading", { name: "Kategorie 1", exact: true }).waitFor();
    assert.equal(await page.locator('[data-testid="inspector-previous"]').count(), 0, "am Rand fehlt der Pfeil");

    const scrolled = await sheet(page).evaluate((el) => {
      const body = el.querySelector(".sheet-body");
      const tops = () => ({
        head: el.querySelector(".sheet-header").getBoundingClientRect().top,
        tabs: el.querySelector('[role="tablist"]').getBoundingClientRect().top,
      });
      const filler = document.createElement("div");
      filler.style.height = "2000px";
      body.append(filler);
      const before = tops();
      body.scrollTop = 500;
      const after = tops();
      const scrollTop = body.scrollTop;
      filler.remove();
      return { before, after, scrollTop };
    });
    assert.ok(scrolled.scrollTop > 0, `der Inhalt ist wirklich gescrollt (${scrolled.scrollTop})`);
    assert.equal(scrolled.after.head, scrolled.before.head, "der Kopf steht fest");
    assert.equal(scrolled.after.tabs, scrolled.before.tabs, "die Tabs stehen fest");

    // Wie die Tastatur: der sichtbare Bereich wird kleiner, `Viewport` schreibt daraus `--vvh`.
    await page.setViewportSize({ width: 390, height: 412 });
    await page.waitForFunction(() => document.querySelector(".bottom-sheet.fixed-height").getBoundingClientRect().height === 400);
  } finally {
    await context.close();
  }
});

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

test("[layout] Trefferflächen am Handy: Monatspfeile, Filter und ˄ ˅ messen 48 px, bei 320 px kein seitliches Scrollen", async () => {
  const { context, page } = await openPage(320, 700, { fixture: longFixture });
  try {
    const targets = [
      previousArrow(page),
      nextArrow(page),
      ...(await page.locator('[data-testid="budget-filter"]').getByRole("button").all()),
    ];
    for (const target of targets) {
      const box = await target.boundingBox();
      assert.ok(box.height >= 48, `48 px hoch (${box.height})`);
    }
    for (const arrow of [previousArrow(page), nextArrow(page)]) {
      assert.ok((await arrow.boundingBox()).width >= 48, "48 px breit");
    }
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    assert.equal(overflow, false, "kein horizontales Scrollen");

    await pickRow(page, "Kategorie 2");
    await page.locator('[data-testid="inspector-next"]').waitFor();
    for (const id of ["inspector-previous", "inspector-next"]) {
      const box = await page.locator(`[data-testid="${id}"]`).boundingBox();
      assert.ok(box.height >= 48 && box.width >= 48, `${id} 48 px`);
    }
    const close = page.locator(".bottom-sheet.active .sheet-close");
    const closeBox = await close.boundingBox();
    assert.ok(closeBox.height >= 48 && closeBox.width >= 48, `Schließen 48 px (${JSON.stringify(closeBox)})`);
    const round = await close.evaluate((el) => getComputedStyle(el, "::before").width);
    assert.equal(round, "32px", "das sichtbare Rund bleibt 32 px");
    await close.click();
    await searchInput(page).fill("Kategorie");
    const clear = await page.getByRole("button", { name: "Suche leeren" }).boundingBox();
    assert.ok(clear.width >= 48 && clear.height >= 48, "Trefferfläche des ✕ mindestens 48 px");
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

/// Unterkante der festen Kopfzeile, Monatsleiste, Chips und was gerade an der Stelle der Chips liegt.
const stickyRects = (page) =>
  page.evaluate(() => {
    const rect = (el) => {
      const { top, bottom, height } = el.getBoundingClientRect();
      return { top, bottom, height };
    };
    const chips = document.querySelector('[data-testid="budget-filter"]');
    const box = chips.getBoundingClientRect();
    const hit = document.elementFromPoint(box.left + 8, box.top + box.height / 2);
    return {
      navBottom: [...document.querySelectorAll("header")].find((el) => el.getBoundingClientRect().height > 0)?.getBoundingClientRect().bottom ?? 0,
      bar: rect(document.querySelector('[data-testid="budget-month-switcher"]')),
      chips: rect(chips),
      chipsOnTop: chips === hit || chips.contains(hit),
      monthOnTop: (() => {
        const name = document.querySelector('[data-testid="budget-month-name"]');
        const at = name.getBoundingClientRect();
        const top = document.elementFromPoint(at.left + at.width / 2, at.top + at.height / 2);
        return name === top || name.contains(top);
      })(),
      search: rect(document.querySelector('[data-testid="budget-category-search"]')),
    };
  });

const scrollToEnd = async (page) => {
  await page.addStyleTag({ content: ".toast-layer { display: none; }" });
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await page.waitForFunction(() => window.scrollY > 300);
};

// Bug D3: die Leiste ist sticky, ihr Wrapper aber genau so hoch, also lief sie nicht mit.
test("[layout] Handy: nach dem Scrollen bleibt die Monatsleiste unter der Navbar, die Chips haften darunter, die Suche scrollt mit", async (t) => {
  for (const [width, height] of [[390, 844], [320, 700]]) {
    const { context, page } = await openPage(width, height, { fixture: longFixture });
    try {
      await scrollToEnd(page);
      const at = await stickyRects(page);
      assert.ok(at.navBottom > 0, `${width}: die Navbar steht im Bild (${JSON.stringify(at)})`);
      assert.ok(at.bar.top >= at.navBottom - 0.5, `${width}: die Leiste liegt unter der Navbar (${JSON.stringify(at)})`);
      assert.ok(at.monthOnTop, `${width}: nichts verdeckt den Monat (${JSON.stringify(at)})`);
      assert.ok(at.chips.top >= at.bar.bottom - 0.5, `${width}: die Chips liegen unter der Leiste (${JSON.stringify(at)})`);
      assert.ok(at.chips.bottom <= at.bar.bottom + at.chips.height + 0.5, `${width}: die Chips haften direkt darunter (${JSON.stringify(at)})`);
      assert.ok(at.chipsOnTop, `${width}: nichts verdeckt die Chips`);
      t.diagnostic(`${width} px: Leiste ${at.bar.height} px, Chips ${at.chips.height} px`);
      assert.ok(at.search.bottom <= at.navBottom, `${width}: die Suche ist weggescrollt (${JSON.stringify(at)})`);
    } finally {
      await context.close();
    }
  }
});

test("[layout] PC: nach dem Scrollen haftet der Kopf mit Suche und Chips, der Spaltenkopf direkt darunter, auch bei großer Schrift", async () => {
  for (const rootFont of [null, "20px"]) {
    const { context, page } = await openPage(1440, 900, { fixture: longFixture });
    try {
      if (rootFont) await page.addStyleTag({ content: `html { font-size: ${rootFont}; }` });
      await pickRow(page, "Kategorie 1");
      await page.locator('aside[data-testid="budget-inspector"]').getByRole("heading", { name: "Kategorie 1", exact: true }).waitFor();
      await scrollToEnd(page);
      const at = await stickyRects(page);
      const head = await page.locator("table thead th").first().boundingBox();
      const panel = await page.locator('aside[data-testid="budget-inspector"]').boundingBox();
      const facts = JSON.stringify({ rootFont, at, head, panel });
      assert.ok(at.bar.top >= at.navBottom - 0.5, `der Kopf liegt unter der Navigation (${facts})`);
      assert.ok(at.chips.top >= at.bar.top - 0.5 && at.chips.bottom <= at.bar.bottom + 0.5, `die Chips liegen im Kopf (${facts})`);
      assert.ok(at.search.top >= at.bar.top - 0.5 && at.search.bottom <= at.bar.bottom + 0.5, `die Suche liegt im Kopf (${facts})`);
      assert.ok(at.chipsOnTop, `nichts verdeckt die Chips (${facts})`);
      assert.ok(head.y >= at.bar.bottom - 0.5, `der Spaltenkopf liegt unter dem Kopf (${facts})`);
      assert.ok(head.y < at.bar.bottom + 24, `der Spaltenkopf haftet direkt darunter (${facts})`);
      assert.ok(panel.y >= at.bar.bottom - 0.5, `Leiste und Panel überlappen nicht (${facts})`);
    } finally {
      await context.close();
    }
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

test("[layout] Sheet: Schließen fährt es sichtbar aus, der Inhalt hält sich dabei, danach ist die Auswahl weg", async () => {
  const { context, page } = await openPage(390, 844, { fixture: filterFixture });
  try {
    assert.equal(await sheet(page).count(), 1, "das Sheet steht schon vor dem Öffnen im DOM");
    // Der Beginn des Übergangs zählt: Frames kommen im Headless-Browser zu selten, um ihn zu sehen.
    await page.evaluate(() => {
      const el = document.querySelector(".bottom-sheet.fixed-height");
      window.sheetRuns = [];
      el.addEventListener("transitionrun", (event) => {
        if (event.target !== el || event.propertyName !== "transform") return;
        window.sheetRuns.push({ opening: el.classList.contains("active"), title: el.querySelector("h3").textContent, inert: el.hasAttribute("inert") });
      });
    });
    await pickRow(page, "Essen");
    await sheet(page).getByRole("heading", { name: "Essen" }).waitFor();
    await page.waitForFunction(() => window.sheetRuns.length === 1);
    await page.waitForFunction(() => document.querySelector(".bottom-sheet.fixed-height").getAnimations().length === 0);
    await page.locator(".bottom-sheet.active .sheet-close").click();
    await page.waitForFunction(() => window.sheetRuns.length === 2);
    assert.deepEqual(await page.evaluate(() => window.sheetRuns), [
      { opening: true, title: "Essen", inert: false },
      { opening: false, title: "Essen", inert: true },
    ]);
    await page.locator('[data-testid="budget-inspector"]').waitFor({ state: "detached" });
    assert.equal(await page.locator("[aria-current='true']").count(), 0, "die Auswahl ist weg");
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

test("[flow:budget-oeffnen] Navigation: die Budget-Seite ist über die Tab-Leiste am Handy erreichbar", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFrame(page, frameFor(monthFixture));
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: monthFixture("2026-10-01", 10) }) })
  );
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/settings`, { waitUntil: "load" });
    const tab = page.locator("nav").getByText("Budget", { exact: true }).filter({ visible: true });
    await tab.waitFor();
    const bounds = await tab.boundingBox();
    assert.ok(bounds && bounds.x >= 0 && bounds.x + bounds.width <= 390, "Eintrag liegt im sichtbaren Bereich");
    await tab.click();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.match(page.url(), /#\/budget$/);
  } finally {
    await context.close();
  }
});

// ---------------- budgeting-h3k9d: dichter, Tastatur seitenweit, Überzogen, ˄ ˅ ----------------

/// Die Teile des Kopfs: Monat, Suche, Filter, Zu verteilen, Targets anwenden, Historie.
const headParts = (page) =>
  page.evaluate(() => {
    const box = (el) => {
      const { top, bottom, left, right } = el.getBoundingClientRect();
      return { top, bottom, left, right };
    };
    const button = [...document.querySelectorAll("button")].find((el) => el.textContent === "Targets anwenden");
    return {
      bar: box(document.querySelector('[data-testid="budget-month-switcher"]')),
      month: box(document.querySelector('[data-testid="budget-month-name"]')),
      search: box(document.querySelector('[data-testid="budget-category-search"]')),
      filter: box(document.querySelector('[data-testid="budget-filter"]')),
      assign: box(document.querySelector('[data-testid="budget-to-be-budgeted"]')),
      targets: box(button),
      history: box(document.querySelector('[data-testid="budget-history-button"]')),
    };
  });

/// Zwei Teile stehen in derselben Zeile, wenn ihre Höhen sich überlappen.
const sharesLine = (a, b) => a.top < b.bottom && b.top < a.bottom;

const noSideScroll = (page) => page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth);

/// Viele Zeilen, die ersten mit Target, fehlt, pausiert und einem Namen, der auch bei 1440 px nicht neben `fehlt` passt.
const denseFixture = (month) => {
  const base = targetFixture(month);
  const long = "Eine Kategorie mit einem wirklich sehr langen Namen, der neben dem Betrag keinen Platz mehr findet und kürzen muss";
  base.Groups[0].Categories[0] = { ...base.Groups[0].Categories[0], Name: long };
  const rest = Array.from({ length: 30 }, (_, i) => category(i + 10, `Kategorie ${i + 10}`, 10, 0, 10));
  base.Groups[0].Categories.push(...rest);
  return base;
};

test("[layout] PC: 1440 × 900 zeigt mindestens 16 Kategorien, die erste Zeile im oberen Viertel, jede höchstens 40 px und einzeilig", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: denseFixture });
  try {
    await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
    const measured = await page.evaluate(() => {
      const rows = [...document.querySelectorAll("tr[data-testid='budget-category-row']")];
      const boxes = rows.map((el) => el.getBoundingClientRect());
      return {
        inView: boxes.filter((r) => r.top >= 0 && r.bottom <= window.innerHeight).length,
        firstTop: boxes[0].top,
        tallest: Math.max(...boxes.map((r) => r.height)),
        groupHead: document.querySelector("tbody td[colspan='5']").getBoundingClientRect().height,
        tallestTarget: Math.max(...rows.map((row) => row.querySelector('[data-testid="budget-target"]')?.getBoundingClientRect().height ?? 0)),
        missingBeside: rows.every((row) => {
          const missing = row.querySelector('[data-testid="budget-missing"]');
          return !missing || missing.getBoundingClientRect().left > row.querySelector('[data-testid="budget-category-name"]').getBoundingClientRect().left;
        }),
      };
    });
    const facts = JSON.stringify(measured);
    assert.ok(measured.inView >= 16, `mindestens 16 Kategorien im Blick (${facts})`);
    assert.ok(measured.firstTop <= 900 * 0.25, `die erste Zeile beginnt in den oberen 25 % (${facts})`);
    assert.ok(measured.tallest <= 40, `eine Zeile misst höchstens 40 px (${facts})`);
    assert.ok(measured.groupHead <= 32, `ein Gruppenkopf misst höchstens 32 px (${facts})`);
    assert.ok(measured.tallestTarget <= 24, `Target mit Rhythmus in einer Zeile (${facts})`);
    assert.ok(measured.missingBeside, `fehlt steht neben dem Namen (${facts})`);
  } finally {
    await context.close();
  }
});

// budgeting-c6vn4 (M1): der Knopf Historie stand in der Suchzeile, die Suche schrumpfte bei 375 px auf etwa
// 70 px. Vor budgeting-h7q4v teilte sie die Zeile nur mit Targets anwenden: so breit muss sie wieder sein.
test("[layout] Handy 375 px: die Suche teilt ihre Zeile nur mit Targets anwenden, der Knopf Historie steht im Kopf mit 48 px", async () => {
  const { context, page } = await openPage(375, 800, { fixture: targetFixture });
  try {
    const at = await page.evaluate(() => {
      const box = (el) => {
        const { top, bottom, left, right, width, height } = el.getBoundingClientRect();
        return { top, bottom, left, right, width, height };
      };
      const search = document.querySelector('[data-testid="budget-category-search"]');
      const button = [...document.querySelectorAll("button")].find((el) => el.textContent === "Targets anwenden" && el.getBoundingClientRect().width > 0);
      const history = [...document.querySelectorAll('[data-testid="budget-history-button"] button')].find((el) => el.getBoundingClientRect().width > 0);
      return {
        row: box(search.parentElement),
        search: box(search),
        targets: box(button),
        history: box(history),
        bar: box(document.querySelector('[data-testid="budget-month-switcher"]')),
      };
    });
    const facts = JSON.stringify(at);
    const gap = at.targets.left - at.search.right;
    assert.ok(at.search.width >= at.row.width - at.targets.width - gap - 1, `die Suche nimmt den Rest der Zeile (${facts})`);
    assert.ok(at.search.width >= 160, `die Suche bleibt benutzbar (${facts})`);
    assert.ok(at.history.top >= at.bar.top && at.history.bottom <= at.bar.bottom, `Historie steht im Kopf (${facts})`);
    // budgeting-r7h2q: bei gebrochener Lage misst der Knopf mal 47,99999 px. Gerundet wie die anderen
    // [layout]-Tests, ein Knopf mit 44 px bleibt rot.
    assert.ok(Math.round(at.history.width) >= 48 && Math.round(at.history.height) >= 48, `Historie hat 48 px Trefferfläche (${facts})`);
  } finally {
    await context.close();
  }
});

/// Kopf mit Werten wie im echten Budget (budgeting-a3k7m): Zu verteilen fünfstellig mit Nachkommastellen,
/// beide Zähler zweistellig, der längste Monatsname. Mit den kurzen Werten von `targetFixture` war der Test
/// grün, während der Kopf mit echten Daten bei 1024 px umbrach.
const realisticHeadFixture = (month) => ({
  Month: month,
  ToBeBudgeted: eur(12345.67),
  Groups: [
    {
      Name: "Alltag",
      Categories: [
        ...Array.from({ length: 12 }, (_, i) => withTarget(category(i + 1, `Fehlt ${i + 1}`, 0, 0, 0), monthly, 1600)),
        ...Array.from({ length: 11 }, (_, i) => category(i + 20, `Überzogen ${i + 1}`, 0, -50, -50)),
      ],
    },
  ],
});

test("[layout] Kopf: ab 1024 px eine Zeile in der verlangten Reihenfolge, bei 768 px zwei Zeilen", async () => {
  for (const width of [1024, 1440]) {
    const { context, page } = await openPage(width, 800, { fixture: realisticHeadFixture, current: "2026-09-01", months: ["2026-08-01", "2026-09-01", "2026-10-01"] });
    try {
      await monthName(page).getByText("September 2026").waitFor();
      const shown = await page.evaluate(() => ({
        filter: document.querySelector('[data-testid="budget-filter"]').textContent,
        assign: document.querySelector('[data-testid="budget-to-be-budgeted"]').textContent,
      }));
      assert.ok(/Fehlt \(\d\d\)/.test(shown.filter) && /Überzogen \(\d\d\)/.test(shown.filter), `zweistellige Zähler (${JSON.stringify(shown)})`);
      assert.ok(shown.assign.includes("12.345,67"), `Zu verteilen fünfstellig (${JSON.stringify(shown)})`);
      const at = await headParts(page);
      const facts = JSON.stringify({ width, at });
      // budgeting-c6vn4 (M1): mit dem Knopf Historie brach der Kopf bei 1024 px in zwei Zeilen um.
      for (const part of ["search", "filter", "assign", "targets", "history"]) {
        assert.ok(sharesLine(at.month, at[part]), `${part} steht in der Zeile des Monats (${facts})`);
      }
      assert.ok(
        at.month.right <= at.search.left &&
          at.search.right <= at.filter.left &&
          at.filter.right <= at.assign.left &&
          at.assign.right <= at.targets.left &&
          at.targets.right <= at.history.left,
        `Reihenfolge Monat, Suche, Filter, Zu verteilen, Targets anwenden, Historie (${facts})`
      );
      assert.ok(at.search.right - at.search.left >= 120, `die Suche bleibt benutzbar (${facts})`);
    } finally {
      await context.close();
    }
  }
  const { context, page } = await openPage(768, 800, { fixture: targetFixture });
  try {
    const at = await headParts(page);
    const facts = JSON.stringify(at);
    const lines = new Set(Object.entries(at).filter(([k]) => k !== "bar").map(([, b]) => Math.round((b.top + b.bottom) / 8)));
    assert.ok(lines.size >= 2, `mehr als eine Zeile (${facts})`);
    assert.ok(at.bar.bottom - at.bar.top <= 130, `höchstens zwei Zeilen (${facts})`);
    assert.ok(await noSideScroll(page), "kein seitliches Scrollen");
  } finally {
    await context.close();
  }
});

test("[layout] Breiten von 320 bis 1920 px: kein seitliches Scrollen, die Tabelle bleibt in ihrer Karte, Panel ab 1024 px, darunter das Sheet", async () => {
  for (const width of [320, 390, 768, 1024, 1280, 1440, 1920]) {
    const { context, page } = await openPage(width, 800, { fixture: targetFixture });
    try {
      await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
      assert.ok(await noSideScroll(page), `${width}: kein seitliches Scrollen`);
      if (width >= 768) {
        const measured = await page.evaluate(() => {
          const table = document.querySelector("table");
          const card = table.closest(".overflow-clip").getBoundingClientRect();
          const last = table.querySelector("tbody tr[data-testid='budget-category-row'] td:last-child").getBoundingClientRect();
          const names = [...table.querySelectorAll('[data-testid="budget-category-name"]')].map((el) => el.getBoundingClientRect().width);
          return { table: table.getBoundingClientRect().right, last: last.right, card: card.right, longName: Math.max(...names) };
        });
        const facts = `${width}: ${JSON.stringify(measured)}`;
        assert.ok(measured.table <= measured.card + 0.5, `Tabelle in der Karte (${facts})`);
        assert.ok(measured.last <= measured.card + 0.5, `Verfügbar sichtbar (${facts})`);
        if (width === 768) assert.ok(measured.longName >= MIN_NAME_WIDTH, `der lange Name behält mindestens ${MIN_NAME_WIDTH} px (${facts})`);
      }
      const panels = await page.locator('aside[data-testid="budget-inspector"]').count();
      assert.equal(panels, width >= 1024 ? 1 : 0, `${width}: Panel ab 1024 px`);
      if (width === 768) {
        await pickRow(page, "Macbook");
        await sheet(page).getByRole("heading", { name: "Macbook" }).waitFor();
      }
    } finally {
      await context.close();
    }
  }
});

test("[layout] Volle Breite: bei 1920 px ist die Liste breiter als bei 1280 px, das Panel bleibt 340 px, der Seitenrand bleibt", async () => {
  const widths = {};
  for (const width of [1280, 1920]) {
    const { context, page } = await openPage(width, 900, { fixture: longFixture });
    try {
      const list = await page.locator("table").locator("xpath=ancestor::div[contains(@class,'overflow-clip')]").boundingBox();
      const panel = await page.locator('aside[data-testid="budget-inspector"]').boundingBox();
      widths[width] = { list: list.width, panel: panel.width, left: list.x, right: width - panel.x - panel.width };
    } finally {
      await context.close();
    }
  }
  const facts = JSON.stringify(widths);
  assert.ok(widths[1920].list >= widths[1280].list + 600, `der ganze Zuwachs geht an die Liste (${facts})`);
  assert.equal(widths[1920].panel, 340, `Panel bei 1920 px (${facts})`);
  assert.equal(widths[1280].panel, 340, `Panel bei 1280 px (${facts})`);
  assert.equal(widths[1920].left, 24, `der übliche Seitenrand links (${facts})`);
  assert.equal(widths[1920].right, 24, `der übliche Seitenrand rechts (${facts})`);
});

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
    await page.getByRole("button", { name: "Targets anwenden" }).click();
    await page.getByRole("heading", { name: /Targets anwenden/ }).waitFor();
    await stayOnEssen(page, panel, "Targets anwenden");
  } finally {
    await second.context.close();
  }
});

test("[layout] Sheet: ˄ ˅ stehen rechts neben Schließen, je 48 px, keine ‹ ›-Zeile, am Rand bleibt der Platz frei", async () => {
  for (const [width, height] of [[320, 700], [390, 844], [768, 900]]) {
    const { context, page } = await openPage(width, height, { fixture: longFixture });
    try {
      await pickRow(page, "Kategorie 2");
      await sheet(page).getByRole("heading", { name: "Kategorie 2", exact: true }).waitFor();
      await sheet(page).evaluate((el) => Promise.all(el.getAnimations().map((a) => a.finished)));

      const previous = page.getByRole("button", { name: "Vorherige Kategorie" });
      const next = page.getByRole("button", { name: "Nächste Kategorie" });
      const close = sheet(page).locator(".sheet-close");
      const [p, n, c, title] = await Promise.all([previous.boundingBox(), next.boundingBox(), close.boundingBox(), sheet(page).locator("h3").boundingBox()]);
      const facts = JSON.stringify({ width, p, n, c, title });
      for (const box of [p, n]) assert.ok(box.width >= 48 && box.height >= 48, `48 px (${facts})`);
      assert.ok(title.x + title.width <= p.x && p.x + p.width <= n.x && n.x + n.width <= c.x, `Titel, ˄, ˅, Schließen von links nach rechts (${facts})`);
      assert.equal(Math.round(p.y), Math.round(c.y), `auf einer Höhe (${facts})`);

      const text = await sheet(page).innerText();
      assert.doesNotMatch(text, /[‹›]/, "keine ‹ ›-Zeile im Sheet");
      assert.match(text, /Alltag · Oktober 2026/, "Gruppe und Monat stehen als Text unter dem Titel");
      assert.equal(await sheet(page).getByRole("button", { name: /Oktober 2026/ }).count(), 0, "der Monat ist kein Knopf");

      await previous.click();
      await sheet(page).getByRole("heading", { name: "Kategorie 1", exact: true }).waitFor();
      assert.equal(await previous.count(), 0, "am Rand fehlt der Pfeil");
      const titleAtEdge = await sheet(page).locator("h3").boundingBox();
      assert.equal(titleAtEdge.width, title.width, `sein Platz bleibt frei (${facts})`);
    } finally {
      await context.close();
    }
  }
});

test("[layout] Sheet: ein langer Name kürzt im Kopf, die Knöpfe bleiben im Bild", async () => {
  const { context, page } = await openPage(320, 700);
  try {
    await pickRow(page, "Eine Kategorie mit einem sehr langen Namen, der am Handy abgeschnitten werden muss k7wmd");
    const close = sheet(page).locator(".sheet-close");
    await close.waitFor();
    await sheet(page).evaluate((el) => Promise.all(el.getAnimations().map((a) => a.finished)));
    const measured = await sheet(page).evaluate((el) => ({
      overflow: el.scrollWidth > el.clientWidth,
      headHeight: el.querySelector(".sheet-header").getBoundingClientRect().height,
      closeRight: el.querySelector(".sheet-close").getBoundingClientRect().right,
    }));
    assert.equal(measured.overflow, false, "nichts läuft seitlich aus dem Sheet");
    assert.ok(measured.closeRight <= 320, `Schließen liegt im Bild (${JSON.stringify(measured)})`);
    assert.ok(measured.headHeight <= 80, `der Kopf wächst nicht mit dem Namen (${JSON.stringify(measured)})`);
  } finally {
    await context.close();
  }
});

test("[layout] Handy: Targets anwenden steht in der Zeile der Suche, im vergangenen Monat bleibt der Platz frei", async () => {
  const { context, page } = await openPage(390, 844, { fixture: longFixture });
  try {
    const before = await headParts(page);
    const facts = JSON.stringify(before);
    assert.ok(sharesLine(before.search, before.targets), `Suche und Targets anwenden in einer Zeile (${facts})`);
    assert.ok(sharesLine(before.month, before.assign), `Monat und Zu verteilen in einer Zeile (${facts})`);
    // Fehler-Toasts der nicht gemockten Routen liegen über den Pfeilen, Playwright scrollte dann für den Klick.
    await page.addStyleTag({ content: ".toast-layer { display: none; }" });
    const firstRow = () => visibleRows(page).first().evaluate((el) => el.getBoundingClientRect().top);
    // Die Seite fährt beim Betreten ein (`animate-page-enter`), mitten darin gemessen sähe es nach Sprung aus.
    await page.evaluate(() => Promise.all(document.getAnimations().map((a) => a.finished)));
    const rowBefore = await firstRow();

    await previousArrow(page).click();
    await monthName(page).getByText("September 2026").waitFor();
    await page.locator('[data-testid="budget-list-area"][aria-busy="false"]').waitFor();
    // Der Knopf blendet per Übergang aus, `visibility` wechselt erst an dessen Ende.
    await page.getByRole("button", { name: "Targets anwenden" }).waitFor({ state: "hidden" });
    assert.equal(await firstRow(), rowBefore, "die Seite springt nicht");
    const after = await headParts(page);
    assert.equal(after.search.right, before.search.right, "die Suche behält ihre Breite");
  } finally {
    await context.close();
  }
});

// budgeting-x4k7p D2: Im Tab Historie stand der Fehlgrund im Badge, das Datum brach am Handy um.
test("[layout] Tab Historie am Handy: langer Fehlgrund kürzt mit Ellipse, Datum bleibt einzeilig, der Betrag steht neutral mit Vorzeichen", async () => {
  const { context, page } = await openPage(320, 700);
  const reason = "YNAB rate limit exceeded, die Zuweisung wurde nicht geschrieben und kann erneut gesendet werden";
  const essen = category(1, "Essen k7wmd", 0, 0, 0).Id;
  const history = {
    Entries: [
      {
        Movement: {
        MovementId: "00000000-0000-0000-0000-00000000d201",
        ApplicationId: "00000000-0000-0000-0000-00000000d202",
        OccurredAt: "2026-10-03T09:30:00.0000000Z",
        Origin: "MoneyAssigned",
        Month: CURRENT,
        CategoryId: essen,
        CategoryName: "Essen k7wmd",
        OldAssigned: eur(1550),
        NewAssigned: eur(1600),
        Counterpart: null,
        Latest: { Failed: reason },
        },
      },
    ],
    Spending: ["2026-05-01", "2026-06-01", "2026-07-01", "2026-08-01", "2026-09-01", CURRENT].map((month, i) => ({
      Month: month,
      Activity: eur(-1234.5 * (i + 1)),
    })),
  };
  await page.route("**/api/BudgetApi/getCategoryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: history }) })
  );
  try {
    await pickRow(page, "Essen k7wmd");
    await tabsOf(sheet(page)).filter({ hasText: "Historie" }).click();
    const reasonText = sheet(page).locator('[data-testid="history-failure-reason"]');
    await reasonText.waitFor();
    assert.equal(await reasonText.getAttribute("title"), reason, "der ganze Grund steht im title");
    const measured = await sheet(page).evaluate((el) => {
      const box = (selector) => el.querySelector(selector).getBoundingClientRect();
      const reasonEl = el.querySelector('[data-testid="history-failure-reason"]');
      const time = el.querySelector('[data-testid="history-time"]');
      return {
        sheetOverflow: el.scrollWidth > el.clientWidth,
        reasonCut: reasonEl.scrollWidth > reasonEl.clientWidth,
        ellipsis: getComputedStyle(reasonEl).textOverflow,
        timeHeight: box('[data-testid="history-time"]').height,
        timeLineHeight: parseFloat(getComputedStyle(time).lineHeight),
        timeRight: box('[data-testid="history-time"]').right,
        reasonLeft: box('[data-testid="history-failure-reason"]').left,
        reasonRight: box('[data-testid="history-failure-reason"]').right,
        rowRight: box('[data-testid="history-movement"]').right,
      };
    });
    const facts = JSON.stringify(measured);
    assert.equal(measured.sheetOverflow, false, `nichts läuft seitlich aus dem Sheet (${facts})`);
    assert.equal(measured.reasonCut, true, `der Grund ist gekürzt (${facts})`);
    assert.equal(measured.ellipsis, "ellipsis", `mit Ellipse (${facts})`);
    assert.ok(measured.timeHeight <= measured.timeLineHeight + 1, `das Datum bleibt einzeilig (${facts})`);
    assert.ok(measured.timeRight <= measured.reasonLeft, `das Datum steht vor dem Grund (${facts})`);
    assert.ok(measured.reasonRight <= measured.rowRight, `der Grund bleibt in der Zeile (${facts})`);
    // budgeting-h7q4v H2: der Betrag kommt aus historyChangeAmount, positiv neutral in text-primary mit Vorzeichen.
    // Mit Money.listAmount stünde er ohne Plus in Teal.
    const amount = sheet(page).locator('[data-testid="history-movement"]').getByText("+50,00 €", { exact: true });
    assert.equal(await amount.count(), 1, "der Betrag steht mit Vorzeichen");
    assert.equal(
      await amount.evaluate((el) => getComputedStyle(el).color),
      await tokenColor(page, "--color-text-primary"),
      "der positive Betrag steht neutral in text-primary"
    );
  } finally {
    await context.close();
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
    assert.equal(
      await bars.nth(1).evaluate((el) => getComputedStyle(el).outlineStyle),
      "none",
      "kein eigener Fokus-Ring, die Wahl zeigt den Fokus"
    );
    await heading.click();
    assert.deepEqual(await shownValues(kpage), [], "Fokus weg, die Zeile ist leer");
  } finally {
    await keyboard.context.close();
  }
});

test("[layout] Ausgaben-Balken am Handy: die Wertzeile hält ihre Höhe und bleibt beim ersten und letzten Balken im Sheet", async () => {
  const { context, page } = await openSpending(390, { hasTouch: true });
  try {
    const bars = sheet(page).locator('[data-testid="month-bar"]');
    const measure = () =>
      sheet(page).evaluate((el) => {
        const rect = (node) => node.getBoundingClientRect();
        const value = [...el.querySelectorAll('[data-testid="month-bar-value"]')].find(
          (v) => getComputedStyle(v).visibility === "visible"
        );
        const sheetRect = rect(el);
        const row = rect(el.querySelector('[data-testid="month-bar-value-row"]'));
        return {
          bars: rect(el.querySelector('[data-testid="month-bars"]')).height,
          row: row.height,
          rowTop: row.top,
          rowBottom: row.bottom,
          sheetLeft: sheetRect.left,
          sheetRight: sheetRect.right,
          value: value ? { left: rect(value).left, right: rect(value).right, top: rect(value).top, bottom: rect(value).bottom } : null,
          overflow: el.scrollWidth > el.clientWidth,
        };
      });
    const empty = await measure();
    assert.equal(empty.value, null, "ohne Auswahl kein Wert");
    assert.ok(empty.row >= 16, `die leere Zeile hält ihre Höhe (${JSON.stringify(empty)})`);
    for (const index of [0, 5]) {
      await bars.nth(index).tap();
      const chosen = await measure();
      const facts = JSON.stringify({ index, empty, chosen });
      assert.notEqual(chosen.value, null, `Wert sichtbar (${facts})`);
      assert.equal(chosen.bars, empty.bars, `die Balken springen nicht (${facts})`);
      assert.equal(chosen.row, empty.row, `die Zeile bleibt gleich hoch (${facts})`);
      assert.ok(chosen.value.top >= chosen.rowTop - 0.5 && chosen.value.bottom <= chosen.rowBottom + 0.5, `der Wert steht in der Wertzeile (${facts})`);
      assert.ok(chosen.value.left >= chosen.sheetLeft && chosen.value.right <= chosen.sheetRight, `der Wert bleibt im Sheet (${facts})`);
      assert.equal(chosen.overflow, false, `nichts läuft seitlich aus dem Sheet (${facts})`);
    }
  } finally {
    await context.close();
  }
});
