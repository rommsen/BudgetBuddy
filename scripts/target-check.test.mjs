// scripts/target-check.test.mjs
//
// Wächter für `budgeting-q4v8n`: die Seite Targets prüfen `#/budget/target-check`. Nur der Happy Path des
// Nutzerflusses und was nur der Browser messen kann (Überlauf, Trefferflächen, Panel oder Sheet), gegen eine
// gemockte BudgetApi. Die Regeln (Gruppen, übernehmen, Panel, Sperre, Rückkehr) liegen in
// `src/Tests/TargetCheckClientTests.fs`. Namen und Beträge sind erfunden.
//
// Ausführen: node --test scripts/target-check.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";

let frontend;
let frontendPort;
let browser;

const eur = (amount) => ({ Amount: amount, Currency: "EUR" });
const guid = (n) => `00000000-0000-0000-0000-${String(n).padStart(12, "0")}`;

const target = (goal, rhythm = { Months: 1 }) => ({
  Goal: eur(goal),
  Rhythm: rhythm,
  Kind: "Need",
  Date: null,
  NeedsWholeAmount: true,
  Day: null,
  CreatedIn: null,
});

/// Die 12 vollen Monate vor November 2026, je Monat dieselben Ausgaben.
const spending = (amount) =>
  Array.from({ length: 12 }, (_, i) => {
    const d = new Date(Date.UTC(2025, 10 + i, 1));
    return { Month: `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, "0")}-01`, Activity: eur(-amount) };
  });

const source = (n, name, goal, spent, rhythm) => ({
  CategoryId: guid(n),
  Name: name,
  GroupName: "Haushalt q4v8n",
  Target: goal === null ? null : target(goal, rhythm),
  Spending: spending(spent),
  LastChange: null,
});

/// "Strom q4v8n": Target 100 €, Ausgaben 150 €, steht bei "Gibst du mehr aus?" mit "150,00 €/Monat übernehmen".
/// "Versicherung q4v8n": 600 € im Jahr, Ausgaben 105 € je Monat, Vorschlag "1.260,00 €/Jahr übernehmen".
/// "Kantine q4v8n": 50 € je Woche, Ausgaben 100 € je Monat, Vorschlag 100 × 12 ÷ 52 = 23,08, auf volle Euro 23 €.
/// "Urlaub q4v8n": ohne Target, Ausgaben 1.234 € je Monat: vierstellige Beträge unter den Balken.
const checkData = {
  Month: "2026-11-01",
  Today: "2026-11-06",
  Categories: [
    source(1, "Strom q4v8n", 100, 150),
    source(2, "Versicherung q4v8n", 600, 105, { Years: 1 }),
    source(3, "Internet q4v8n", 100, 100),
    source(4, "Kantine q4v8n", 50, 100, { Weeks: 1 }),
    source(5, "Urlaub q4v8n", null, 1234),
  ],
};

const monthFixture = (key) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Haushalt q4v8n",
      Categories: checkData.Categories.map((c) => ({
        Id: c.CategoryId,
        Name: c.Name,
        Assigned: eur(0),
        Activity: eur(0),
        Available: eur(0),
        Underfunded: null,
        Target: c.Target,
        Pause: null,
        OpenWriteBack: null,
      })),
    },
  ],
});

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

/// Mockt die BudgetApi. `calls.change` sammelt die Aufträge an `changeCategoryTarget`.
async function mockedPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { change: [] };
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01"], CategoryOrder: checkData.Categories.map((c) => c.CategoryId) } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key) });
  });
  await page.route("**/api/BudgetApi/getTargetCheck", (route) => json(route, { Ok: checkData }));
  await page.route("**/api/BudgetApi/changeCategoryTarget", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    calls.change.push(body);
    const [, request] = body;
    json(route, { Ok: { TargetWritten: { ...request.Shown, Goal: request.Wanted.NewGoal } } });
  });
  return { context, page, calls };
}

const openCheck = async (width, height) => {
  const opened = await mockedPage(width, height);
  await opened.page.goto(`http://127.0.0.1:${frontendPort}/#/budget/target-check`, { waitUntil: "load" });
  await opened.page.locator('[data-testid="target-check-row"]').first().waitFor();
  return opened;
};

const rowOf = (page, name) => page.locator('[data-testid="target-check-row"]').filter({ hasText: name });

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[flow:targets-pruefen] Handy: über den Knopf im Kopf die Seite öffnen, bei Gibst du mehr aus übernehmen, die Zeile steht danach bei Passt, Zurück führt zur Budget-Seite", async () => {
  const { context, page, calls } = await mockedPage(375, 812);
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/budget");
    await page.locator('[data-testid="budget-target-check-button"]:visible button').click();
    await page.waitForURL(/#\/budget\/target-check$/);
    const more = page.locator('[data-testid="target-check-group"]').filter({ hasText: "Gibst du mehr aus?" });
    await more.waitFor();
    await rowOf(page, "Strom q4v8n").getByRole("button", { name: "150,00 €/Monat übernehmen" }).click();

    const fits = page.locator('[data-testid="target-check-group"]').filter({ hasText: "Passt" });
    await fits.locator('[data-testid="target-check-fits-toggle"]').click();
    const moved = fits.locator('[data-testid="target-check-row"]').filter({ hasText: "Strom q4v8n" });
    await moved.waitFor();
    assert.match(await moved.locator('[data-testid="target-check-note"]').textContent(), /^neu gesetzt am 06\.11\.2026$/);
    assert.equal(calls.change.length, 1, "ein Aufruf");
    assert.equal(String(calls.change[0][1].Wanted.NewGoal.Amount), "150", "der Vorschlag");

    await page.getByRole("button", { name: "Zurück" }).click();
    await page.waitForURL(/#\/budget$/);
    await page.locator('[data-testid="budget-to-be-budgeted"]').first().waitFor();
  } finally {
    await context.close();
  }
});

test("[layout] Handy 375 px: der Kopf der Budget-Seite trägt Historie und Targets prüfen ohne Überlauf mit 48 px, jede Zeile zeigt Werte, 12 Balken und beide Knöpfe ungekürzt, auch mit vierstelligen Beträgen", async () => {
  const { context, page } = await mockedPage(375, 812);
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
    await waitForPageLoaded(page, "/budget");
    const head = await page.evaluate(() => {
      const bar = [...document.querySelectorAll(".budget-month-bar")].find((el) => el.getBoundingClientRect().height > 0);
      const box = (testId) => document.querySelector(`[data-testid="${testId}"] button`).getBoundingClientRect();
      const history = box("budget-history-button");
      const check = box("budget-target-check-button");
      return {
        barRight: bar.getBoundingClientRect().right,
        scrollWidth: document.documentElement.scrollWidth,
        history: { w: history.width, h: history.height, right: history.right },
        check: { w: check.width, h: check.height, right: check.right, top: check.top },
        historyTop: history.top,
      };
    });
    const facts = JSON.stringify(head);
    assert.ok(head.scrollWidth <= 375, `kein waagerechter Überlauf (${facts})`);
    for (const key of ["history", "check"]) {
      assert.ok(head[key].w >= 47.5 && head[key].h >= 47.5, `${key}: 48 px (${facts})`);
      assert.ok(head[key].right <= 375, `${key}: im Bild (${facts})`);
    }
    assert.equal(Math.round(head.check.top), Math.round(head.historyTop), `nebeneinander in einer Zeile (${facts})`);

    await page.locator('[data-testid="budget-target-check-button"]:visible button').click();
    await page.locator('[data-testid="target-check-row"]').first().waitFor();
    // Jede offene Zeile, auch mit vierstelligen Beträgen ("Urlaub q4v8n").
    const rows = await page.evaluate(() =>
      [...document.querySelectorAll('[data-testid="target-check-row"]')].map((el) => {
        const clipped = (node) => node.scrollWidth > node.clientWidth + 0.5;
        const within = (node, box) => {
          const b = node.getBoundingClientRect();
          return b.left >= box.left - 0.5 && b.right <= box.right + 0.5;
        };
        const rowBox = el.getBoundingClientRect();
        const bars = [...el.querySelectorAll('[data-testid="month-bar"]')];
        // Jeder sichtbare Text unter einem Balken bleibt in seiner Spalte. Die Wertzeile liegt über der ganzen Breite.
        const outside = bars.flatMap((bar) =>
          [...bar.querySelectorAll("span")]
            .filter((t) => t.dataset.testid !== "month-bar-value" && t.getBoundingClientRect().width > 0 && t.textContent !== "")
            .filter((t) => !within(t, bar.getBoundingClientRect()))
            .map((t) => `${t.textContent} ${Math.round(t.getBoundingClientRect().width)}/${Math.round(bar.getBoundingClientRect().width)}`)
        );
        const barTextsInColumn = outside.length === 0;
        return {
          name: el.querySelector('[data-testid="target-check-name"]').textContent,
          bars: bars.length,
          barTextsInColumn,
          outside,
          factsInside: [...el.querySelectorAll('[data-testid="target-check-facts"] span')].every((t) => within(t, rowBox)),
          buttons: [...el.querySelectorAll("button")].map((b) => ({
            text: b.textContent,
            clipped: clipped(b),
            inside: within(b, rowBox),
            h: b.getBoundingClientRect().height,
          })),
        };
      })
    );
    const rowFacts = JSON.stringify(rows);
    assert.ok(rows.length >= 4, `die offenen Zeilen (${rowFacts})`);
    for (const row of rows) {
      assert.equal(row.bars, 12, `${row.name}: 12 Balken (${rowFacts})`);
      assert.ok(row.barTextsInColumn, `${row.name}: die Texte unter den Balken bleiben in ihrer Spalte (${rowFacts})`);
      assert.ok(row.factsInside, `${row.name}: die Werte stehen in der Zeile (${rowFacts})`);
      for (const b of row.buttons) {
        assert.ok(!b.clipped && b.inside && b.h >= 47.5, `${row.name}, ${b.text}: ungekürzt, im Bild, 48 px (${rowFacts})`);
      }
    }
    const yearly = rows.find((r) => r.name === "Versicherung q4v8n");
    assert.deepEqual(yearly.buttons.map((b) => b.text), ["1.260,00 €/Jahr übernehmen", "Anpassen …"], "beide Knöpfe");
    assert.ok((await page.evaluate(() => document.documentElement.scrollWidth)) <= 375, "kein waagerechter Überlauf");
  } finally {
    await context.close();
  }
});

test("[layout] Anpassen öffnet ab 1024 px ein Panel rechts neben der Liste, bei 375 px ein BottomSheet", async () => {
  const wide = await openCheck(1280, 900);
  try {
    await rowOf(wide.page, "Strom q4v8n").getByRole("button", { name: "Anpassen …" }).click();
    const panel = wide.page.locator('[data-testid="target-check-panel"]');
    await panel.waitFor();
    const at = await wide.page.evaluate(() => {
      const list = document.querySelector('[data-testid="target-check-list"]').getBoundingClientRect();
      const box = document.querySelector('[data-testid="target-check-panel"]').getBoundingClientRect();
      return { listRight: list.right, panelLeft: box.left, panelWidth: box.width };
    });
    assert.ok(at.panelLeft >= at.listRight, `rechts neben der Liste (${JSON.stringify(at)})`);
    assert.equal(Math.round(at.panelWidth), 340, "340 px wie der Inspektor");
    assert.equal(await wide.page.locator(".bottom-sheet.active").count(), 0, "kein Sheet");
  } finally {
    await wide.context.close();
  }
  const phone = await openCheck(375, 812);
  try {
    await rowOf(phone.page, "Strom q4v8n").getByRole("button", { name: "Anpassen …" }).click();
    const sheet = phone.page.locator(".bottom-sheet.fixed-height.active");
    await sheet.locator('[data-testid="target-amount"] input').waitFor();
    // Das Sheet fährt ein: warten, bis es unten anliegt.
    await phone.page.waitForFunction(() => {
      const el = document.querySelector(".bottom-sheet.fixed-height.active");
      return el && Math.abs(el.getBoundingClientRect().bottom - window.innerHeight) <= 1;
    });
    const box = await sheet.evaluate((el) => {
      const b = el.getBoundingClientRect();
      return { bottom: b.bottom, width: b.width, height: window.innerHeight };
    });
    assert.ok(Math.abs(box.bottom - box.height) <= 1 && box.width >= 374, `unten über die volle Breite (${JSON.stringify(box)})`);
    assert.equal(await phone.page.locator('[data-testid="target-check-panel"]').count(), 0, "kein Panel");
  } finally {
    await phone.context.close();
  }
});

// Verhindert den Befund aus der Durchsicht am 2026-11-06: `Math.Round(x, MidpointRounding.AwayFromZero)` rundet im
// Fable-Kompilat auf eine Nachkommastelle (der Modus galt als Stellenzahl), der wöchentliche Vorschlag stand mit
// "23,10 €/Woche" statt auf vollen Euro (ADR 0082 Punkt 9). Unter .NET rechnet dieselbe Funktion richtig.
//
// Verhindert außerdem den Befund aus der Durchsicht von e86f8d2: `Math.Round(x, 0, MidpointRounding.AwayFromZero)`
// auf decimal rundet im Fable-Kompilat zur geraden Zahl (fable-library `Decimal.round` verwirft den Modus). Ein
// Balkenbetrag von 84,50 € stand als "84" statt "85" unter dem Balken (`barAmountText`). "Tanken q4v8n" gibt es
// nur in diesem Test, die anderen Tests sehen die Fixture ohne die Zeile.
test("[fable] Ein wöchentlicher Vorschlag steht auf vollen Euro, ein Betrag auf 50 Cent wird aufgerundet", async () => {
  const { context, page } = await mockedPage(1280, 900);
  const withHalf = { ...checkData, Categories: [...checkData.Categories, source(6, "Tanken q4v8n", null, 84.5)] };
  // Die später angelegte Route hat Vorrang vor der aus `mockedPage`.
  await page.route("**/api/BudgetApi/getTargetCheck", (route) => json(route, { Ok: withHalf }));
  try {
    await page.goto(`http://127.0.0.1:${frontendPort}/#/budget/target-check`, { waitUntil: "load" });
    await rowOf(page, "Kantine q4v8n").getByRole("button", { name: "23,00 €/Woche übernehmen" }).waitFor();
    const amounts = await rowOf(page, "Tanken q4v8n")
      .locator('[data-testid="month-bar"] > span:last-child')
      .allTextContents();
    assert.equal(amounts.length, 12, `12 Balkenbeträge (${JSON.stringify(amounts)})`);
    assert.deepEqual([...new Set(amounts)], ["85"], "84,50 € steht kaufmännisch gerundet als 85");
  } finally {
    await context.close();
  }
});

// Die enge Balkenreihe ist ein Tab-Stopp je Zeile (Review budgeting-q4v8n): Pfeiltasten wählen den Nachbarmonat,
// Tab verlässt die Reihe zu den Knöpfen der Zeile, statt durch zwölf Balken zu laufen.
test("[tastatur] In einer Zeile ist die Balkenreihe ein Tab-Stopp, Pfeil links wählt den Vormonat, Tab führt zum Knopf der Zeile", async () => {
  const { context, page } = await openCheck(1280, 900);
  try {
    const row = rowOf(page, "Kantine q4v8n");
    const bars = row.locator('[data-testid="month-bar"]');
    assert.equal(await bars.count(), 12, "12 Balken in der Zeile");
    const stops = row.locator('[data-testid="month-bar"][tabindex="0"]');
    assert.equal(await stops.count(), 1, "genau ein Balken liegt in der Tab-Folge");
    await stops.first().focus();
    const focusedIndex = () =>
      bars.evaluateAll((els) => els.findIndex((el) => el === document.activeElement));
    assert.equal(await focusedIndex(), 11, "der Tab-Stopp ist der letzte Monat");
    await page.keyboard.press("ArrowLeft");
    assert.equal(await focusedIndex(), 10, "Pfeil links fokussiert den Vormonat");
    await page.keyboard.press("Tab");
    const active = await page.evaluate(() => ({
      tag: document.activeElement?.tagName,
      inRow: !!document.activeElement?.closest('[data-testid="target-check-row"]')?.textContent?.includes("Kantine q4v8n"),
      text: document.activeElement?.textContent ?? "",
    }));
    assert.equal(active.tag, "BUTTON", `nach Tab steht der Fokus auf einem Knopf (${JSON.stringify(active)})`);
    assert.ok(active.inRow, "der Knopf gehört zur selben Zeile");
    assert.match(active.text, /übernehmen|Anpassen/, "es ist übernehmen oder Anpassen …");
  } finally {
    await context.close();
  }
});
