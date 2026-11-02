// scripts/budget-page.test.mjs
//
// Wächter für `budgeting-k7wmd` und `budgeting-g8s3w`: die Budget-Seite (#/budget) zeigt Monat,
// Zu verteilen und die Kategorien nach Gruppe, eine Zeile wählt die Kategorie für den Inspektor.
// Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte BudgetApi-Antworten: am Handy
// (390 px) Zeilen ohne horizontales Scrollen, am PC (1440 px) eine Tabelle neben dem Panel,
// `‹ Monat ›`, Filter, feste Höhen, Tastatur, Trefferflächen, Fehler mit Wiederholen, Erreichbarkeit
// über die Navigation. Dazu die Kategorie-Suche (`budgeting-rxxb4`).
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
  Snoozed: false,
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
  Snoozed: snoozed,
});
const monthly = { Goal: eur(1600), Rhythm: { Months: 1 } };
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
          { Goal: eur(121600), Rhythm: { Months: 11 } },
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

// budgeting-rxxb4: "leb" trifft zwei Kategorien in zwei Gruppen und eine ganze Gruppe.
const searchFixture = (month) => ({
  Month: month,
  ToBeBudgeted: eur(10),
  Groups: [
    { Name: "Alltag", Categories: [category(1, "Lebensmittel", 100, 0, 100), category(2, "Kino", 10, 0, 10)] },
    { Name: "Wohnen", Categories: [category(3, "Strom", 50, 0, 50)] },
    { Name: "Rücklagen", Categories: [category(4, "Urlaub", 50, 0, 50), category(5, "Lebensversicherung", 20, 0, 20)] },
    { Name: "Beleben", Categories: [category(6, "Sport", 30, 0, 30), category(7, "Musik", 30, 0, 30)] },
  ],
});

const CURRENT = "2026-10-01";
const WINDOW = ["2026-09-01", CURRENT, "2026-11-01"];

/// Der Rahmen zu einer Fixture: `CategoryOrder` nicht leer, sonst sortierte die Seite nach Name.
const frameFor = (fixture, months = WINDOW) => ({
  CurrentMonth: CURRENT,
  Months: months,
  CategoryOrder: fixture(CURRENT, 0).Groups.flatMap((g) => g.Categories.map((c) => c.Id)),
});

const mockFrame = (page, frame, onCall = () => {}) =>
  page.route("**/api/BudgetApi/getBudgetFrame", (route) => {
    onCall();
    return route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: frame }) });
  });

async function openPage(width, height, { fail = false, requestedKeys = [], fixture = monthFixture, months = WINDOW } = {}) {
  const context = await browser.newContext({ viewport: { width, height } });
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
  await mockFrame(page, frameFor(fixture, months), () => {
    frames.count += 1;
  });
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

test("Handy: Zu verteilen negativ ohne Warnhinweis, Gruppen mit Zeilen, kein horizontales Scrollen", async () => {
  const { context, page, requestedKeys } = await openPage(390, 844);
  try {
    assert.deepEqual(requestedKeys, [CURRENT], "der laufende Monat ist die Vorgabe");
    const box = page.locator('[data-testid="budget-to-be-budgeted"]');
    const text = (await box.innerText()).replace(/\s+/g, " ");
    assert.match(text, /Zu verteilen/i);
    assert.match(text, /-12,34 €/, "negativ, deutsch formatiert");
    assert.equal(await page.getByRole("alert").count(), 0, "kein Warnhinweis bei negativem Zu verteilen");
    assert.equal(await page.getByRole("heading", { name: "Budget" }).filter({ visible: true }).count(), 1);
    assert.ok(await page.getByText("Oktober 2026").isVisible(), "Monat im Kopf");

    const groups = page.locator('[data-testid="budget-group"]').filter({ visible: true });
    assert.equal(await groups.count(), 2);
    const rows = page.locator('[data-testid="budget-category-row"]').filter({ visible: true });
    assert.equal(await rows.count(), 3);
    const firstRow = (await rows.first().innerText()).replace(/\s+/g, " ");
    assert.match(firstRow, /Essen k7wmd/);
    assert.match(firstRow, /1\.120,09 €/, "Verfügbar");
    assert.match(firstRow, /Zugewiesen 1\.600,00 €/);
    assert.match(firstRow, /Aktivität -921,10 €/);

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

test("PC: Tabelle mit Spaltenköpfen Zugewiesen, Aktivität, Verfügbar", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    const table = page.locator("table").filter({ visible: true });
    assert.equal(await table.count(), 1);
    const head = (await table.locator("thead").innerText()).replace(/\s+/g, " ").toLowerCase();
    assert.equal(await table.locator("thead tr").count(), 1, "genau eine Kopfzeile, keine verschachtelte");
    assert.equal(await table.locator("thead tr th").count(), 5, "fünf Spaltenköpfe in der einen Zeile");
    for (const h of ["kategorie", "zugewiesen", "aktivität", "verfügbar"]) assert.ok(head.includes(h), `Spaltenkopf ${h}`);
    const rows = page.locator('[data-testid="budget-category-row"]').filter({ visible: true });
    assert.equal(await rows.count(), 3);
    const cells = await rows.first().locator("td").allInnerTexts();
    assert.deepEqual(cells.map((c) => c.replace(/\s+/g, " ").trim()), ["Essen k7wmd", "", "1.600,00 €", "-921,10 €", "1.120,09 €"]);
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

test("Handy: dritte Zeile mit Target oder fehlt, mit fehlt, erfüllt und pausiert, ohne horizontales Scrollen", async () => {
  const { page, context } = await openPage(390, 844, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  assert.deepEqual(await targetTexts(page), [
    "Target 1.600,00 €/Monat · fehlt 1.600,00 €",
    "Target 1.600,00 €/Monat · erfüllt",
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
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
  assert.equal(overflow, false, "kein horizontales Scrollen");
  await context.close();
});

// Bug F3: `truncate` auf der ganzen Zeile schnitt zuerst "fehlt …" ab, also genau die Aussage.
test("Schmales Handy (320 px): bei langem Target bleibt der Status vollständig sichtbar", async () => {
  const { page, context } = await openPage(320, 700, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  const status = await page.evaluate(() => {
    const line = [...document.querySelectorAll('[data-testid="budget-target"]')].filter((el) => el.offsetParent !== null).at(-1);
    const carrier = [...line.parentElement.querySelectorAll("span")].find((el) => !el.children.length && el.innerText.includes("fehlt"));
    return { clipped: carrier.scrollWidth > carrier.clientWidth, right: carrier.getBoundingClientRect().right, width: window.innerWidth };
  });
  assert.equal(status.clipped, false, "der Status wird nicht abgeschnitten");
  assert.ok(status.right <= status.width, `der Status liegt im Viewport (${JSON.stringify(status)})`);
  await context.close();
});

test("PC: Spalte Target direkt hinter Kategorie zeigt denselben Inhalt, Gruppenzeile spannt alle Spalten", async () => {
  const { page, context } = await openPage(1440, 900, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  const headers = await page.locator("table thead th").allInnerTexts();
  assert.equal(headers[1].toLowerCase(), "target");
  assert.equal(headers.length, 5);
  assert.deepEqual(await targetTexts(page), [
    "1.600,00 €/Monat",
    "1.600,00 €/Monat · erfüllt",
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

test("Schmalster PC (768 px): die Tabelle mit Spalte Target läuft nicht aus ihrer Karte, der Name bleibt lesbar", async () => {
  const { page, context } = await openPage(768, 900, { fixture: targetFixture });
  await page.locator('[data-testid="budget-target"]:visible').first().waitFor();
  const measured = await page.evaluate(() => {
    const table = document.querySelector("table");
    const card = table.closest(".overflow-clip").getBoundingClientRect();
    const lastCell = table.querySelector("tbody tr[data-testid='budget-category-row'] td:last-child").getBoundingClientRect();
    const names = [...table.querySelectorAll('[data-testid="budget-category-name"]')].map((el) => el.getBoundingClientRect().width);
    return { tableRight: table.getBoundingClientRect().right, cardRight: card.right, lastCellRight: lastCell.right, longName: Math.max(...names) };
  });
  assert.ok(measured.tableRight <= measured.cardRight + 0.5, `die Tabelle bleibt in der Karte (${JSON.stringify(measured)})`);
  assert.ok(measured.lastCellRight <= measured.cardRight + 0.5, `die Spalte Verfügbar bleibt sichtbar (${JSON.stringify(measured)})`);
  assert.ok(measured.longName >= MIN_NAME_WIDTH, `der lange Name behält mindestens ${MIN_NAME_WIDTH} px (${JSON.stringify(measured)})`);
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

test("Monate: bis zum Rand blättern, der Pfeil fehlt, der Name bleibt stehen und ist teal, der Name führt zurück", async () => {
  const { context, page, requestedKeys, frames } = await openPage(1440, 900);
  const targets = page.getByRole("button", { name: "Targets anwenden" });
  try {
    await targets.waitFor();
    const atStart = await monthName(page).boundingBox();
    const teal = await tokenColor(page, "--color-neon-teal");
    const colorOf = () => monthName(page).evaluate((el) => getComputedStyle(el).color);
    assert.notEqual(await colorOf(), teal, "im laufenden Monat ist der Name nicht teal");

    // Der Name steht sofort da, die Anfrage folgt: auf die Antwort warten.
    const previous = page.waitForResponse("**/api/BudgetApi/getBudgetMonth");
    await previousArrow(page).click();
    await monthName(page).getByText("September 2026").waitFor();
    await previous;
    assert.deepEqual(requestedKeys, [CURRENT, "2026-09-01"]);
    assert.equal(await previousArrow(page).count(), 0, "am Rand fehlt der Pfeil");
    const atEdge = await monthName(page).boundingBox();
    assert.equal(Math.round(atEdge.x), Math.round(atStart.x), "der Name springt nicht");
    await targets.waitFor({ state: "hidden" });
    assert.equal(await targets.count(), 0, "im vergangenen Monat kein Targets anwenden");
    assert.equal(await colorOf(), teal, "der Name ist teal, solange ein anderer Monat gezeigt wird");

    const back = page.waitForResponse("**/api/BudgetApi/getBudgetMonth");
    await monthName(page).click();
    await monthName(page).getByText("Oktober 2026").waitFor();
    await back;
    await targets.waitFor();
    assert.deepEqual(requestedKeys, [CURRENT, "2026-09-01", CURRENT]);

    await nextArrow(page).click();
    await monthName(page).getByText("November 2026").waitFor();
    assert.equal(await nextArrow(page).count(), 0, "am anderen Rand auch");
    await targets.waitFor();
    assert.equal(frames.count, 1, "Blättern liest den Rahmen nicht neu (ADR 0076)");
  } finally {
    await context.close();
  }
});

test("Monat nicht offen: die Meldung steht da, die Pfeile bleiben, der Vormonat lädt", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFrame(page, frameFor(monthFixture, ["2026-08-01", "2026-09-01"]));
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    const body = key === CURRENT ? { Error: "Monat nicht offen" } : { Ok: monthFixture(key, 10) };
    route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  });
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await page.getByRole("alert").waitFor();
    assert.match(await page.getByRole("alert").innerText(), /Monat nicht offen/);
    await previousArrow(page).click();
    await monthName(page).getByText("September 2026").waitFor();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  } finally {
    await context.close();
  }
});

// Bug D3: die Leiste ist sticky, ihr Wrapper aber genau so hoch, also lief sie nicht mit.
test("Handy: nach dem Scrollen bleibt die Monatsleiste unter der Navbar sichtbar", async () => {
  const { context, page } = await openPage(390, 844, { fixture: longFixture });
  try {
    await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
    await page.waitForFunction(() => window.scrollY > 300);
    // Die nicht gemockten Routen melden Fehler-Toasts, die oben liegen und nicht zur Leiste gehören.
    await page.addStyleTag({ content: ".toast-layer { display: none; }" });
    const measured = await page.evaluate(() => {
      const header = [...document.querySelectorAll("header")].find((el) => el.getBoundingClientRect().height > 0);
      const name = document.querySelector('[data-testid="budget-month-name"]');
      const box = name.getBoundingClientRect();
      const hit = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
      return {
        headerBottom: header.getBoundingClientRect().bottom,
        barTop: document.querySelector('[data-testid="budget-month-switcher"]').getBoundingClientRect().top,
        nameOnTop: name === hit || name.contains(hit),
      };
    });
    assert.ok(measured.headerBottom > 0, `die Navbar steht im Bild (${JSON.stringify(measured)})`);
    assert.ok(measured.barTop >= measured.headerBottom - 0.5, `die Leiste liegt unter der Navbar (${JSON.stringify(measured)})`);
    assert.ok(measured.nameOnTop, `nichts verdeckt den Monat (${JSON.stringify(measured)})`);
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

test("Filter: Rot zeigt nur rote Zeilen, eine Zeile mit Rot und Gelb zeigt beides, die Zahlen bleiben", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const chips = page.locator('[data-testid="budget-filter"]').getByRole("button");
    assert.deepEqual(await chips.allInnerTexts(), ["Alle", "Fehlt (2)", "Rot (1)"]);
    await chips.nth(2).click();
    await page.getByText("Macbook").waitFor({ state: "hidden" });
    assert.equal(await visibleRows(page).count(), 1);
    const colors = await visibleRows(page).first().evaluate((row) => ({
      missing: getComputedStyle(row.querySelector('[data-testid="budget-missing"]')).color,
      available: getComputedStyle(row.querySelector('[data-testid="budget-available"] span')).color,
    }));
    const yellow = await tokenColor(page, "--color-calm-yellow");
    const red = await tokenColor(page, "--color-neon-red");
    assert.equal(colors.missing, yellow, "fehlt steht in Gelb");
    assert.equal(colors.available, red, "Verfügbar im Minus steht in Rot");

    await searchInput(page).fill("xyz");
    await page.getByText("Keine Kategorie passt zu „xyz“").waitFor();
    assert.deepEqual(await chips.allInnerTexts(), ["Alle", "Fehlt (2)", "Rot (1)"], "die Suche ändert die Zahlen nicht");
    await searchInput(page).fill("");
    await chips.nth(1).click();
    await visibleRows(page).filter({ hasText: "Kleidung" }).waitFor();
    assert.equal(await visibleRows(page).count(), 2);
  } finally {
    await context.close();
  }
});

const tabsOf = (scope) => scope.locator('[data-testid="inspector-tabs"]').getByRole("tab");

const pickRow = (page, name) => visibleRows(page).filter({ has: page.getByText(name, { exact: true }) }).click();

test("Inspektor am PC: gleiche Höhe in allen vier Tabs, die gewählte Zeile ist markiert", async () => {
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
    assert.match(await page.locator('[data-testid="inspector-target"]').innerText(), /Fehlbetrag/i);
  } finally {
    await context.close();
  }
});

const sheet = (page) => page.locator(".bottom-sheet.fixed-height");

test("Sheet am Handy: 500 px hoch in allen vier Tabs, 48 px Tabs", async () => {
  const { context, page } = await openPage(390, 844, { fixture: filterFixture });
  try {
    await pickRow(page, "Essen");
    await page.locator('[data-testid="budget-inspector"]').waitFor();
    for (const tab of await tabsOf(sheet(page)).all()) {
      await tab.click();
      const box = await sheet(page).boundingBox();
      assert.equal(box.height, 500, "feste Höhe");
      assert.ok((await tab.boundingBox()).height >= 48, "Trefferfläche");
    }
  } finally {
    await context.close();
  }
});

test("Sheet: ‹ › wechselt ohne Schließen, Kopf und Tabs bleiben beim Scrollen stehen, mit kleinerem --vvh schrumpft es", async () => {
  const { context, page } = await openPage(390, 844, { fixture: longFixture });
  try {
    await pickRow(page, "Kategorie 2");
    await sheet(page).getByRole("heading", { name: "Kategorie 2", exact: true }).waitFor();
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

    await page.evaluate(() => document.documentElement.style.setProperty("--vvh", "412px"));
    await page.waitForFunction(() => document.querySelector(".bottom-sheet.fixed-height").getBoundingClientRect().height === 400);
  } finally {
    await context.close();
  }
});

test("Tastatur am PC: Pfeile wechseln die Kategorie und die Liste scrollt mit, im Suchfeld passiert nichts", async () => {
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

    await searchInput(page).focus();
    await page.keyboard.press("ArrowDown");
    await page.evaluate(() => new Promise(requestAnimationFrame));
    await page.evaluate(() => new Promise(requestAnimationFrame));
    assert.match(await page.locator("tr[aria-current='true']").innerText(), /Kategorie 29/, "die Auswahl bleibt");
  } finally {
    await context.close();
  }
});

test("Trefferflächen am Handy: Monatspfeile, Filter und ‹ › messen 48 px, bei 320 px kein seitliches Scrollen", async () => {
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
  } finally {
    await context.close();
  }
});

test("Breite: bei 1024 px läuft die Tabelle neben dem Panel nicht aus der Karte, bei 768 px öffnet das Sheet", async () => {
  const wide = await openPage(1024, 900, { fixture: targetFixture });
  try {
    await wide.page.locator('[data-testid="budget-target"]:visible').first().waitFor();
    assert.ok(await wide.page.locator('aside[data-testid="budget-inspector"]').isVisible(), "Panel");
    const measured = await wide.page.evaluate(() => {
      const table = document.querySelector("table");
      const card = table.closest(".overflow-clip").getBoundingClientRect();
      const last = table.querySelector("tbody tr[data-testid='budget-category-row'] td:last-child").getBoundingClientRect();
      return { tableRight: table.getBoundingClientRect().right, lastRight: last.right, cardRight: card.right };
    });
    assert.ok(measured.tableRight <= measured.cardRight + 0.5, `Tabelle in der Karte (${JSON.stringify(measured)})`);
    assert.ok(measured.lastRight <= measured.cardRight + 0.5, `Verfügbar sichtbar (${JSON.stringify(measured)})`);
  } finally {
    await wide.context.close();
  }

  const tablet = await openPage(768, 900, { fixture: targetFixture });
  try {
    await tablet.page.locator('[data-testid="budget-target"]:visible').first().waitFor();
    assert.equal(await tablet.page.locator("aside[data-testid='budget-inspector']").count(), 0, "kein Panel");
    await pickRow(tablet.page, "Macbook");
    await sheet(tablet.page).getByRole("heading", { name: "Macbook" }).waitFor();
  } finally {
    await tablet.context.close();
  }
});

test("Fehler: lesbare Meldung mit Wiederholen, danach erscheint der Monat", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let calls = 0;
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFrame(page, frameFor(monthFixture));
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    calls += 1;
    if (calls === 1) {
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "YNAB rate limit exceeded. Retry after 60 seconds" }) });
    } else {
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: monthFixture("2026-10-01", 354.5) }) });
    }
  });
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await page.getByRole("alert").waitFor();
    assert.match(await page.getByRole("alert").innerText(), /rate limit/);
    await page.getByRole("button", { name: "Erneut versuchen" }).click();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.match((await page.locator('[data-testid="budget-to-be-budgeted"]').innerText()).replace(/\s+/g, " "), /354,50 €/);
    assert.equal(calls, 2);
  } finally {
    await context.close();
  }
});

const visibleTexts = (locator) => locator.filter({ visible: true }).allInnerTexts();
const searchInput = (page) => page.getByPlaceholder("Kategorie suchen…");
const visibleRows = (page) => page.locator('[data-testid="budget-category-row"]').filter({ visible: true });

test("Handy-Suche: leb zeigt nur Treffer und ihre Gruppen, ✕ leert, Monatswechsel behält", async () => {
  const { context, page } = await openPage(390, 844, { fixture: searchFixture });
  try {
    assert.equal(await visibleRows(page).count(), 7);
    await searchInput(page).fill("leb");
    await page.getByText("Kino").waitFor({ state: "hidden" });

    const groups = (await visibleTexts(page.locator('[data-testid="budget-group"] h2'))).map((t) => t.toLowerCase());
    assert.deepEqual(groups, ["alltag", "rücklagen", "beleben"]);
    const rows = (await visibleTexts(page.locator('[data-testid="budget-category-name"]'))).join(" ");
    for (const hit of ["Lebensmittel", "Lebensversicherung", "Sport", "Musik"]) assert.match(rows, new RegExp(hit));
    assert.equal(await visibleRows(page).count(), 4);

    assert.ok(await page.locator('[data-testid="budget-to-be-budgeted"]').isVisible(), "Zu verteilen bleibt");
    assert.ok(await page.locator('[data-testid="budget-month-switcher"]').isVisible(), "Monatsleiste bleibt");
    assert.ok(await page.getByRole("button", { name: "Targets anwenden" }).isVisible(), "Targets anwenden bleibt");

    await nextArrow(page).click();
    await monthName(page).getByText("November 2026").waitFor();
    assert.equal(await searchInput(page).inputValue(), "leb", "die Suche bleibt beim Monatswechsel");
    assert.equal(await visibleRows(page).count(), 4, "der neue Monat ist gefiltert");

    const clear = page.getByRole("button", { name: "Suche leeren" });
    const box = await clear.boundingBox();
    assert.ok(box.width >= 48 && box.height >= 48, "Trefferfläche des ✕ mindestens 48 px");
    await clear.click();
    assert.equal(await searchInput(page).inputValue(), "");
    assert.equal(await visibleRows(page).count(), 7);
  } finally {
    await context.close();
  }
});

test("PC-Suche: Feld über der Tabelle, kein Treffer zeigt den Hinweis mit dem Suchtext", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: searchFixture });
  try {
    await searchInput(page).fill("strom");
    await page.getByText("Kino").waitFor({ state: "hidden" });
    assert.equal(await visibleRows(page).count(), 1);
    const field = await searchInput(page).boundingBox();
    const table = await page.locator("table").filter({ visible: true }).boundingBox();
    assert.ok(field.y + field.height <= table.y, "Suchfeld steht über der Tabelle");

    await searchInput(page).fill("  xyz ");
    await page.getByText("Keine Kategorie passt zu „xyz“").waitFor();
    assert.equal(await visibleRows(page).count(), 0);
    assert.ok(await page.locator('[data-testid="budget-to-be-budgeted"]').isVisible(), "Zu verteilen bleibt");
  } finally {
    await context.close();
  }
});

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
test("Blättern am PC: Liste, Inspektor und Tabs bleiben stehen, die Liste behält Fokus und Scrollposition, kein Spinner", async () => {
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

    release();
    await page.waitForFunction(() => document.querySelector('[data-testid="budget-list-area"]').getAttribute("aria-busy") === "false");
    assert.equal(await list.evaluate((el) => el === document.activeElement), true, "die Liste hat den Fokus");
    await settledScroll(page);
    assert.equal(await page.evaluate(() => window.scrollY), scrollBefore, "die Seite springt nicht");
  } finally {
    await context.close();
  }
});

test("Scheitert das Blättern, steht die Fehlerkarte da, die Pfeile bleiben, das Panel lädt nicht weiter", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: filterFixture });
  try {
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await pickRow(page, "Essen");
    await panel.getByRole("heading", { name: "Essen" }).waitFor();
    await page.route("**/api/BudgetApi/getBudgetMonth", (route) =>
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "Monat kaputt p4n6r" }) })
    );
    await previousArrow(page).click();
    await page.getByRole("alert").filter({ hasText: "Monat kaputt p4n6r" }).waitFor();
    assert.ok(await nextArrow(page).isVisible(), "die Pfeile bleiben");
    // Das Skeleton verspräche Inhalt, der ohne Monat nicht kommt: das Laden ist zu Ende.
    await panel.locator("[class*='animate-pulse']").waitFor({ state: "detached" });
    assert.equal((await panel.innerText()).trim(), "", "das Panel bleibt leer, Kategorie wählen gilt nur ohne Auswahl");
  } finally {
    await context.close();
  }
});

test("Wiederholen nach dem ersten Rahmen-Fehler lädt Rahmen und Monat", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let frames = 0;
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) => {
    frames += 1;
    const body = frames === 1 ? { Error: "Rahmen kaputt p4n6r" } : { Ok: frameFor(monthFixture) };
    return route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  });
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: monthFixture(CURRENT, 10) }) })
  );
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await page.getByRole("alert").filter({ hasText: "Rahmen kaputt p4n6r" }).waitFor();
    await page.getByRole("button", { name: "Erneut versuchen" }).click();
    await monthName(page).getByText("Oktober 2026").waitFor();
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.equal(frames, 2, "der Rahmen kommt ein zweites Mal");
  } finally {
    await context.close();
  }
});

test("Filter ohne Treffer: Keine Kategorie in dieser Ansicht", async () => {
  const { context, page } = await openPage(1440, 900, { fixture: longFixture });
  try {
    await page.locator('[data-testid="budget-filter"]').getByRole("button", { name: "Rot (0)" }).click();
    await page.getByText("Keine Kategorie in dieser Ansicht.").waitFor();
    assert.equal(await visibleRows(page).count(), 0);
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
      search: rect(document.querySelector('[data-testid="budget-category-search"]')),
    };
  });

const scrollToEnd = async (page) => {
  await page.addStyleTag({ content: ".toast-layer { display: none; }" });
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await page.waitForFunction(() => window.scrollY > 300);
};

test("Handy: nach dem Scrollen bleiben die Chips unter der Leiste, die Suche scrollt mit", async (t) => {
  for (const [width, height] of [[390, 844], [320, 700]]) {
    const { context, page } = await openPage(width, height, { fixture: longFixture });
    try {
      await scrollToEnd(page);
      const at = await stickyRects(page);
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

test("PC: nach dem Scrollen haften Suche und Chips unter der Leiste, der Spaltenkopf darunter, auch bei großer Schrift", async () => {
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
      assert.ok(at.chips.top >= at.bar.bottom - 0.5, `die Chips liegen unter der Leiste (${facts})`);
      assert.ok(at.search.top >= at.bar.bottom - 0.5, `die Suche haftet mit (${facts})`);
      assert.ok(at.chipsOnTop, `nichts verdeckt die Chips (${facts})`);
      assert.ok(head.y >= at.chips.bottom - 0.5, `der Spaltenkopf liegt unter den Chips (${facts})`);
      assert.ok(head.y < at.chips.bottom + 24, `der Spaltenkopf haftet direkt darunter (${facts})`);
      assert.ok(panel.y >= at.bar.bottom - 0.5, `Leiste und Panel überlappen nicht (${facts})`);
    } finally {
      await context.close();
    }
  }
});

test("Panel: 30 Buchungen bei 700 px Höhe scrollen im Tab, das Panel bleibt in der Sichthöhe", async () => {
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

test("Sheet: Schließen fährt es sichtbar aus, der Inhalt hält sich dabei, danach ist die Auswahl weg", async () => {
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

test("Sheet: viele offene Verschiebungen scrollen im festen Bereich, der Inhalt behält Platz", async () => {
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

test("Barrierefreiheit: Handy-Zeile mit aria-current, Monat und Suche mit Namen, Tabs mit Panel und Pfeiltasten", async () => {
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
    await page.waitForFunction(() => document.activeElement.textContent === "Target" && document.activeElement.tabIndex === 0);
    assert.equal(await tabpanel.getAttribute("aria-labelledby"), await tabs.nth(3).getAttribute("id"));
  } finally {
    await context.close();
  }
});

test("Navigation: die Budget-Seite ist über die Tab-Leiste am Handy erreichbar", async () => {
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
