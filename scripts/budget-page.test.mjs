// scripts/budget-page.test.mjs
//
// Wächter für `budgeting-k7wmd`: die Budget-Seite (#/budget) zeigt Monat, Umschalter dieser/
// nächster Monat, Zu verteilen und die Kategorien nach Gruppe mit Zugewiesen, Aktivität und
// Verfügbar. Gemessen am echten, per Vite gestarteten Fable-Client gegen gemockte BudgetApi-
// Antworten: am Handy (390 px) Zeilen ohne horizontales Scrollen, am PC (1440 px) eine
// Tabelle, negatives Zu verteilen ohne Warnhinweis, Fehler mit Wiederholen, Erreichbarkeit
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

async function openPage(width, height, { fail = false, requestedKeys = [], fixture = monthFixture } = {}) {
  const context = await browser.newContext({ viewport: { width, height } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  let failNext = fail;
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    requestedKeys.push(key);
    if (failNext) {
      failNext = false;
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Error: "YNAB rate limit exceeded. Retry after 60 seconds" }) });
      return;
    }
    const firstOfMonth = { last: "2026-09-01", current: "2026-10-01", next: "2026-11-01" }[key];
    const month = fixture(firstOfMonth, -12.34);
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: month }) });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, requestedKeys };
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
    assert.deepEqual(requestedKeys, ["current"], "der laufende Monat ist die Vorgabe");
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

test("Handy: dritte Zeile nur mit Target, mit fehlt, erfüllt und pausiert, ohne horizontales Scrollen", async () => {
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
    "1.600,00 €/Monat · fehlt 1.600,00 €",
    "1.600,00 €/Monat · erfüllt",
    "1.600,00 €/Monat · pausiert",
    null,
    "121.600,00 € alle 11 Monate · fehlt 121.600,00 €",
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
    const card = table.closest(".overflow-hidden").getBoundingClientRect();
    const lastCell = table.querySelector("tbody tr[data-testid='budget-category-row'] td:last-child").getBoundingClientRect();
    const names = [...table.querySelectorAll('[data-testid="budget-category-name"]')].map((el) => el.getBoundingClientRect().width);
    return { tableRight: table.getBoundingClientRect().right, cardRight: card.right, lastCellRight: lastCell.right, longName: Math.max(...names) };
  });
  assert.ok(measured.tableRight <= measured.cardRight + 0.5, `die Tabelle bleibt in der Karte (${JSON.stringify(measured)})`);
  assert.ok(measured.lastCellRight <= measured.cardRight + 0.5, `die Spalte Verfügbar bleibt sichtbar (${JSON.stringify(measured)})`);
  assert.ok(measured.longName >= MIN_NAME_WIDTH, `der lange Name behält mindestens ${MIN_NAME_WIDTH} px (${JSON.stringify(measured)})`);
  await context.close();
});

test("Umschalter: Nächster Monat lädt den nächsten Monat und zeigt ihn", async () => {
  const { context, page, requestedKeys } = await openPage(390, 844);
  try {
    await page.getByRole("button", { name: "Nächster Monat" }).click();
    await page.getByText("November 2026").waitFor();
    assert.deepEqual(requestedKeys, ["current", "next"]);
    await page.getByRole("button", { name: "Dieser Monat" }).click();
    await page.getByText("Oktober 2026").waitFor();
    assert.deepEqual(requestedKeys, ["current", "next", "current"]);
  } finally {
    await context.close();
  }
});

test("Letzter Monat: fordert 'last' an, Targets anwenden fehlt dort und kommt beim Zurückwechseln wieder", async () => {
  const { context, page, requestedKeys } = await openPage(1440, 900);
  const targets = page.getByRole("button", { name: "Targets anwenden" });
  try {
    await targets.waitFor();
    await page.getByRole("button", { name: "Letzter Monat" }).click();
    await page.getByText("September 2026").waitFor();
    assert.deepEqual(requestedKeys, ["current", "last"]);
    assert.equal(await targets.count(), 0, "im letzten Monat kein Targets anwenden");
    await page.getByRole("button", { name: "Dieser Monat" }).click();
    await page.getByText("Oktober 2026").waitFor();
    await targets.waitFor();
  } finally {
    await context.close();
  }
});

test("Handy: die drei Monatsknöpfe stehen in zeitlicher Folge vollständig im Viewport, die Seite scrollt nicht seitlich", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    const switcher = page.locator('[data-testid="budget-month-switcher"]');
    assert.deepEqual(await switcher.getByRole("button").allInnerTexts(), ["Letzter Monat", "Dieser Monat", "Nächster Monat"]);
    for (const button of await switcher.getByRole("button").all()) {
      const box = await button.boundingBox();
      assert.ok(box && box.x >= 0 && box.x + box.width <= 390, `Knopf liegt im Viewport (${JSON.stringify(box)})`);
    }
    const overflow = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
    }));
    assert.ok(overflow.scroll <= overflow.client, `kein horizontales Scrollen (scrollWidth ${overflow.scroll} > clientWidth ${overflow.client})`);
  } finally {
    await context.close();
  }
});

// Bug D1: die Mindesthöhe saß am Rahmen, der Klick am inneren Knopf, die Trefferfläche war 32 px.
test("Handy: jeder Monatsknopf hat 48 px Trefferfläche und meldet, ob er aktiv ist", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    const buttons = page.locator('[data-testid="budget-month-switcher"]').getByRole("button");
    for (const button of await buttons.all()) {
      const box = await button.boundingBox();
      assert.ok(box.height >= 48, `Trefferfläche mindestens 48 px (${box.height})`);
    }
    assert.deepEqual(await buttons.evaluateAll((all) => all.map((b) => b.getAttribute("aria-pressed"))), ["false", "true", "false"]);
  } finally {
    await context.close();
  }
});

// Bug D3: die Leiste ist sticky, ihr Wrapper aber genau so hoch, also lief sie nicht mit.
test("Handy: nach dem Scrollen bleibt der Monatsumschalter unter der Navbar sichtbar", async () => {
  const { context, page } = await openPage(390, 844, { fixture: longFixture });
  try {
    await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
    await page.waitForFunction(() => window.scrollY > 300);
    const box = await page.locator('[data-testid="budget-month-switcher"]').getByRole("button").first().boundingBox();
    assert.ok(box.y >= 0 && box.y < 150, `Umschalter im Viewport (y ${box.y})`);
  } finally {
    await context.close();
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
    assert.ok(await page.locator('[data-testid="budget-month-switcher"]').isVisible(), "Umschalter bleibt");
    assert.ok(await page.getByRole("button", { name: "Targets anwenden" }).isVisible(), "Targets anwenden bleibt");

    await page.getByRole("button", { name: "Nächster Monat" }).click();
    await page.getByText("November 2026").waitFor();
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

test("Navigation: die Budget-Seite ist über die Tab-Leiste am Handy erreichbar", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
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
