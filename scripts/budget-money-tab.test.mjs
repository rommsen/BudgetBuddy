// scripts/budget-money-tab.test.mjs
//
// Wächter für `budgeting-c5w2n`: was nur der Browser messen kann am Tab Geld. Fokus, Markieren,
// Fensterwechsel, Höhe und Trefferflächen, sichtbarer Bereich mit offener Tastatur, lange Namen,
// der Picker über dem Inspektor und der Hauptablauf gegen eine gemockte BudgetApi. Die Regeln
// selbst liegen in `src/Tests/MoveMoneyClientTests.fs`.
//
// Ausführen: node --test scripts/budget-money-tab.test.mjs
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
const LONG = "Eine Kategorie mit einem sehr langen Namen, genau sechzig Ze.";

const category = (n, name, assigned, available) => ({
  Id: guid(n),
  Name: name,
  Assigned: eur(assigned),
  Activity: eur(0),
  Available: eur(available),
  Underfunded: null,
  Target: null,
  Snoozed: false,
});

const monthFixture = (key) => ({
  Month: key,
  ToBeBudgeted: eur(500),
  Groups: [
    {
      Name: "Alltag c5w2n",
      Categories: [
        category(1, "Lebensmittel c5w2n", 900, 80),
        category(2, "Rot c5w2n", 0, -12.3),
        category(3, LONG, 10, 10),
      ],
    },
  ],
});

const emptyReport = (key) => ({ ApplicationId: guid(9), Month: key, Lines: [], ActualToBeBudgeted: null });

async function openPage(width, height) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = { assign: [], move: [], recent: 0 };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetFrame", (route) =>
    json(route, { Ok: { CurrentMonth: "2026-11-01", Months: ["2026-10-01", "2026-11-01", "2026-12-01"], CategoryOrder: [guid(1), guid(2), guid(3)] } })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    const [key] = JSON.parse(route.request().postData() ?? "[]");
    json(route, { Ok: monthFixture(key) });
  });
  await page.route("**/api/BudgetApi/getRecentMoveCategories", (route) => {
    calls.recent += 1;
    json(route, { Ok: [guid(3), guid(1)] });
  });
  await page.route("**/api/BudgetApi/assignMoney", (route) => {
    const body = JSON.parse(route.request().postData() ?? "[]");
    calls.assign.push(body);
    json(route, { Ok: emptyReport(body[0]) });
  });
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(JSON.parse(route.request().postData() ?? "[]"));
    json(route, { Ok: emptyReport("2026-11-01") });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const row = (page, name) => page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });
const field = (page) => page.locator('[data-testid="assign-field"] input');
const mode = (page, name) => page.locator('[data-testid="money-mode"]').filter({ hasText: name });
const step = (page, label) => page.locator('[data-testid="assign-step"]').filter({ hasText: label }).first();
const isFieldActive = (page) =>
  page.evaluate(() => document.activeElement === document.querySelector('[data-testid="assign-field"] input'));
/// Wartet, bis der Aufruf beim Mock angekommen ist.
const until = async (page, condition) => {
  for (let i = 0; i < 600 && !condition(); i++) await page.evaluate(() => new Promise(requestAnimationFrame));
  assert.ok(condition(), "die Bedingung tritt ein");
};
const twoFrames = (page) =>
  page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
const sheetBody = (page) => page.locator(".bottom-sheet.fixed-height.active .sheet-body");

/// Weder das Element noch ein Kind darin kürzt seinen Text: ein Kind kann in sich überlaufen.
const nothingCut = (el) =>
  [el, ...el.querySelectorAll("span")]
    .filter((e) => getComputedStyle(e).display !== "inline")
    .every((e) => e.scrollWidth <= e.clientWidth);

/// Liegt das Element ganz im sichtbaren Teil von `.sheet-body`, ohne zu scrollen?
const insideBody = async (page, locator, label) => {
  const box = await locator.boundingBox();
  const body = await sheetBody(page).boundingBox();
  assert.ok(box && body, `${label} ist da`);
  assert.ok(box.y >= body.y && box.y + box.height <= body.y + body.height, `${label} liegt im Bild: ${box.y + box.height} gegen ${body.y + body.height}`);
};

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("Beim Fokus ist der Inhalt des Felds markiert", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).click();
    await page.waitForFunction(() => {
      const input = document.querySelector('[data-testid="assign-field"] input');
      return input.selectionStart === 0 && input.selectionEnd === input.value.length;
    });
    await page.keyboard.type("+20");
    assert.equal(await field(page).inputValue(), "+20", "das Tippen ersetzt den Inhalt");
  } finally {
    await context.close();
  }
});

test("Das Feld rechnet im echten Client wie unter .NET", async () => {
  const { context, page } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("100/3");
    await page.locator('[data-testid="assign-preview"]').getByText(/Ergebnis 33,33 €/).waitFor();
    await field(page).fill("0,05/2+1.240,50");
    await page.locator('[data-testid="assign-preview"]').getByText(/Ergebnis 1\.240,53 €/).waitFor();
    await field(page).fill("5/0");
    await page.locator('[data-testid="assign-preview"]').getByText("Keine gültige Rechnung").waitFor();
  } finally {
    await context.close();
  }
});

test("Der Wechsel in ein anderes Programm speichert nicht: das Feld bleibt das aktive Element", async () => {
  const { context, page, calls } = await openPage(1440, 900);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("+20");
    // So kommt der Wechsel beim Feld an: Blur ohne neues aktives Element.
    await field(page).dispatchEvent("focusout");
    await twoFrames(page);
    assert.ok(await isFieldActive(page), "Vorbedingung: das Feld bleibt aktiv");
    assert.equal(await field(page).inputValue(), "+20", "die Eingabe steht");
    assert.equal(calls.assign.length, 0, "nichts gesendet");
    await page.locator("h2, h1").first().evaluate(() => document.activeElement.blur());
    await page.locator('[data-testid="assign-field"] input[value="920,00"]').waitFor();
    assert.equal(calls.assign.length, 1, "ein echtes Blur speichert");
  } finally {
    await context.close();
  }
});

test("Handy: das Sheet ist in allen drei Modi 500 px hoch, Zuweisen zeigt Feld, sechs Schritte und Knopf ohne Scrollen", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await row(page, "Rot c5w2n").click();
    const sheet = page.locator(".bottom-sheet.fixed-height.active");
    await mode(page, "Zuweisen").click();
    await field(page).waitFor();
    await insideBody(page, field(page), "Feld");
    assert.equal(await page.locator('[data-testid="assign-step"]').count(), 6, "sechs Schritte");
    for (const label of ["-50", "+50"]) await insideBody(page, step(page, label), `Schritt ${label}`);
    await insideBody(page, page.locator('[data-testid="assign-missing"] button'), "Knopf");
    for (const name of ["Zuweisen", "Holen", "Abgeben"]) {
      await mode(page, name).click();
      assert.equal((await sheet.boundingBox()).height, 500, name);
    }
  } finally {
    await context.close();
  }
});

test("Handy, Hauptablauf: Zeile wählen, +20 tippen, weiter mit dem Pfeil; die Zuweisung trägt Monat und Wert der Eingabe", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("+20");
    await page.locator('[data-testid="inspector-next"]').click();
    await page.locator(".bottom-sheet.fixed-height.active").getByRole("heading", { name: "Rot c5w2n" }).waitFor();
    await until(page, () => calls.assign.length === 1);
    assert.deepEqual(calls.assign, [["2026-11-01", { CategoryId: guid(1), Assigned: { Amount: "920", Currency: "EUR" } }]]);
  } finally {
    await context.close();
  }
});

test("Handy mit offener Tastatur: das Sheet schrumpft, Feld und Verfügbar danach liegen im sichtbaren Bereich", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await field(page).fill("+20");
    // Wie die Tastatur: der sichtbare Bereich wird kleiner. `Viewport` schreibt daraus `--vvh`.
    await page.setViewportSize({ width: 390, height: 420 });
    await page.waitForFunction(() => document.querySelector(".bottom-sheet.fixed-height.active").getBoundingClientRect().height === 408);
    await insideBody(page, field(page), "Feld");
    await insideBody(page, page.locator('[data-testid="assign-available-after"]'), "Verfügbar danach");
  } finally {
    await context.close();
  }
});

test("Handy: ein Tipp auf Schritt oder Fehlbetrag-Knopf lässt den Fokus im Feld, alles misst 48 px, die Schritte passen in 360 px", async () => {
  const { context, page, calls } = await openPage(360, 800);
  try {
    await row(page, "Rot c5w2n").click();
    await mode(page, "Zuweisen").click();
    await field(page).focus();
    await step(page, "+10").tap();
    assert.ok(await isFieldActive(page), "Schritt: der Fokus bleibt");
    assert.equal(await field(page).inputValue(), "10,00", "der Schritt wirkt");

    const sizes = [
      ...(await page.locator('[data-testid="money-mode"]').all()),
      ...(await page.locator('[data-testid="assign-step"]').all()),
      page.locator('[data-testid="assign-missing"] button'),
      field(page),
    ];
    for (const element of sizes) {
      assert.ok((await element.boundingBox()).height >= 48, "48 px");
    }
    const steps = await Promise.all((await page.locator('[data-testid="assign-step"]').all()).map((s) => s.boundingBox()));
    assert.ok(steps.at(-1).x + steps.at(-1).width <= 360, "die Schritte passen");
    assert.ok(await page.locator('[data-testid="assign-steps"]').evaluate((el) => el.scrollWidth <= el.clientWidth), "nichts läuft über");
    assert.equal(await field(page).evaluate((el) => getComputedStyle(el).fontSize), "16px", "Feldschrift");

    await page.locator('[data-testid="assign-missing"] button').tap();
    await until(page, () => calls.move.length === 1);
    assert.ok(await isFieldActive(page), "Knopf: der Fokus bleibt");

    await mode(page, "Holen").click();
    const amountSize = await page.locator('[data-testid="transfer-amount"] input').evaluate((el) => getComputedStyle(el).fontSize);
    assert.ok(parseFloat(amountSize) >= 16, "Betrag: iOS zoomt nicht");
    assert.ok((await page.locator('[data-testid="transfer-from"]').boundingBox()).height >= 48, "von");
  } finally {
    await context.close();
  }
});

test("PC, Panel: Feld tippen, Enter, Pfeil runter wählt die nächste Zeile, keine Schritte", async () => {
  const { context, page, calls } = await openPage(1280, 800);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    const panel = page.locator('aside[data-testid="budget-inspector"]');
    await panel.getByRole("heading", { name: "Lebensmittel c5w2n" }).waitFor();
    assert.equal(await page.locator('[data-testid="assign-step"]').count(), 0, "keine Schritte im Panel");
    await field(page).click();
    await page.keyboard.type("+20");
    await page.keyboard.press("Enter");
    await page.waitForFunction(() => document.activeElement.tagName !== "INPUT");
    await page.keyboard.press("ArrowDown");
    await panel.getByRole("heading", { name: "Rot c5w2n" }).waitFor();
    await until(page, () => calls.assign.length === 1);

    const fieldLeft = () => page.waitForFunction(() => document.activeElement.tagName !== "INPUT");
    await mode(page, "Zuweisen").click();
    await field(page).click();
    await page.keyboard.type("+5");
    await page.keyboard.press("Escape");
    await fieldLeft();
    await mode(page, "Holen").click();
    await page.locator('[data-testid="transfer-amount"] input').click();
    await page.keyboard.press("Enter");
    await fieldLeft();
  } finally {
    await context.close();
  }
});

for (const [label, width, height] of [["390 px", 390, 844], ["Panel", 1280, 800]]) {
  test(`Lange Kategorienamen stehen in von, nach und im Knopf vollständig (${label})`, async () => {
    const { context, page } = await openPage(width, height);
    try {
      await row(page, "Lebensmittel c5w2n").click();
      await mode(page, "Abgeben").click();
      await page.locator('[data-testid="transfer-amount"] input').fill("5");
      await page.locator('[data-testid="transfer-to"]').click();
      await page.locator('[role="option"]:visible').filter({ hasText: LONG }).last().click();
      await page.locator('[data-testid="transfer-to"]').getByText(LONG).waitFor();
      for (const selector of ['[data-testid="transfer-to"]', '[data-testid="transfer-submit"] button']) {
        assert.ok(await page.locator(selector).evaluate(nothingCut), `${selector} kürzt nichts`);
        assert.ok((await page.locator(selector).innerText()).includes(LONG), `${selector} zeigt den Namen ganz`);
      }
      // Das Sheet des Pickers schluckt den nächsten Klick (ADR 0005), am PC gibt es die Sperre nicht.
      await page.locator('[data-testid="detail-activity"]').first().click();
      await mode(page, "Holen").click();
      const from = page.locator('[data-testid="transfer-from"]');
      await from.getByText(LONG).waitFor();
      assert.ok(await from.evaluate(nothingCut), "von kürzt nichts");
    } finally {
      await context.close();
    }
  });
}

test("Handy: der Picker geht über dem Inspektor auf, zeigt Zuletzt verwendet, schließt ohne Ghost-Click, das Sheet bleibt offen", async () => {
  const { context, page, calls } = await openPage(390, 844);
  try {
    await row(page, "Lebensmittel c5w2n").click();
    await mode(page, "Holen").click();
    await page.locator('[data-testid="transfer-from"]').click();
    const layered = page.locator(".bottom-sheet.layer-2");
    await layered.getByText("Zuletzt verwendet").waitFor();
    const inspector = page.locator(".bottom-sheet.fixed-height.active");
    assert.ok(await inspector.isVisible(), "der Inspektor steht darunter");
    await layered.locator('[data-testid="category-picker-to-be-budgeted"]').tap();
    await page.locator('[data-testid="transfer-from"]').getByText("Zu verteilen").waitFor();
    await twoFrames(page);
    assert.ok(await inspector.isVisible(), "der Inspektor bleibt offen");
    assert.equal(await mode(page, "Holen").getAttribute("aria-pressed"), "true", "kein Ghost-Click");
  } finally {
    await context.close();
  }
});

test("Handy: der Hinweis einer Offenen Verschiebung steht im festen Bereich des Sheets", async () => {
  const { context, page } = await openPage(390, 844);
  try {
    const failed = {
      ApplicationId: guid(8),
      Month: "2026-11-01",
      Lines: [{ CategoryId: guid(2), CategoryName: "Rot c5w2n", OldAssigned: eur(0), NewAssigned: eur(12.3), Outcome: { Failed: "Rate-Limit" } }],
      ActualToBeBudgeted: null,
    };
    await page.route("**/api/BudgetApi/moveMoney", (route) =>
      route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: failed }) })
    );
    await row(page, "Rot c5w2n").click();
    await mode(page, "Zuweisen").click();
    await page.locator('[data-testid="assign-missing"] button').click();
    const hint = page.locator('.bottom-sheet.fixed-height.active .sheet-pinned [data-testid="move-retry"]');
    await hint.waitFor();
  } finally {
    await context.close();
  }
});
