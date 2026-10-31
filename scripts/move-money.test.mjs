// scripts/move-money.test.mjs
//
// Wächter für `budgeting-afd8h`: das Sheet "Verschieben" auf der Budget-Seite. Gemessen am echten,
// per Vite gestarteten Fable-Client gegen eine gemockte BudgetApi: Handy-Fluss vom Tipp auf
// Verfügbar bis zu den neuen Beträgen, Trefferfläche, Dialog am PC mit dem Picker als Popover,
// Lesefehler lässt das Sheet offen, "Erneut senden" am Toast. Dazu `budgeting-nfrds`: Reload nach
// "Erneut senden", abgebrochener Aufruf, "Erneut senden" ohne Toast, Popover schließt per Auslöser,
// Sheet springt nach Seitenwechsel nicht wieder auf, Betrag für Screenreader.
//
// Ausführen: node --test scripts/move-money.test.mjs
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

const category = (n, name, available) => ({
  Id: guid(n),
  Name: name,
  Assigned: eur(available),
  Activity: eur(0),
  Available: eur(available),
  Underfunded: null,
  Snoozed: false,
});

const monthFixture = (food, trip) => ({
  Month: "2026-11-01",
  ToBeBudgeted: eur(35.45),
  Groups: [
    {
      Name: "Alltag afd8h",
      Categories: [
        category(1, "Lebensmittel afd8h", food),
        category(2, "Urlaub afd8h", trip),
        category(3, "Kleidung afd8h", 10),
      ],
    },
  ],
});

const report = (outcome) => ({
  ApplicationId: "11111111-1111-1111-1111-111111111111",
  Month: "2026-11-01",
  Lines: [
    { CategoryId: guid(1), CategoryName: "Lebensmittel afd8h", OldAssigned: eur(80), NewAssigned: eur(60), Outcome: "Written" },
    { CategoryId: guid(2), CategoryName: "Urlaub afd8h", OldAssigned: eur(-30), NewAssigned: eur(-10), Outcome: outcome },
  ],
  ActualToBeBudgeted: null,
});

async function openPage(width, height, handlers) {
  const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 768 });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const state = { food: 80, trip: -30 };
  const calls = { move: [], retry: [], month: 0 };
  const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/BudgetApi/getBudgetMonth", (route) => {
    calls.month += 1;
    json(route, { Ok: monthFixture(state.food, state.trip) });
  });
  await page.route("**/api/BudgetApi/moveMoney", (route) => {
    calls.move.push(route.request().postData());
    const answer = handlers.move(state);
    return answer === "abort" ? route.abort() : json(route, answer);
  });
  await page.route("**/api/BudgetApi/retryFailedTargets", (route) => {
    calls.retry.push(JSON.parse(route.request().postData() ?? "[]"));
    const answer = handlers.retry(state);
    return answer === "abort" ? route.abort() : json(route, answer);
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/budget`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/budget");
  await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
  return { context, page, calls };
}

const visibleRow = (page, name) =>
  page.locator('[data-testid="budget-category-row"]:visible').filter({ hasText: name });

const tapAvailable = async (page, name) => {
  await visibleRow(page, name).locator('[data-testid="budget-available"]').click();
  await page.locator('[data-testid="move-dialog"]').waitFor();
};

// Das Picker-Sheet schluckt nach der Wahl 350 ms lang den nächsten Klick (ADR 0005); ein Mensch
// tippt später, der Test wartet die Sperre ab.
const afterPicker = (page) => page.waitForTimeout(400);

const text = async (locator) => (await locator.innerText()).replace(/\s+/g, " ");

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("Handy: Verfügbar antippen, Gegenseite suchen, Betrag ändern, Verschieben; die Zeilen zeigen die neuen Beträge", async () => {
  const handlers = {
    move: (state) => {
      state.food -= 20;
      state.trip += 20;
      return { Ok: report("Written") };
    },
    retry: () => ({ Error: "nicht erwartet" }),
  };
  const { context, page, calls } = await openPage(390, 844, handlers);
  try {
    const available = visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]');
    const box = await available.boundingBox();
    assert.ok(box && box.height >= 48 && box.width >= 48, "Trefferfläche mindestens 48 px");
    assert.match(await available.getAttribute("aria-label"), /Lebensmittel afd8h verschieben, verfügbar 80,00 €/, "Screenreader hört den Betrag");

    await visibleRow(page, "Lebensmittel afd8h").getByText("Lebensmittel afd8h").click();
    assert.equal(await page.locator('[data-testid="move-dialog"]').count(), 0, "Tipp auf den Namen öffnet nichts");

    await tapAvailable(page, "Lebensmittel afd8h");
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Von Lebensmittel afd8h nach/);
    assert.equal(await page.locator('[data-testid="move-amount"] input').inputValue(), "80,00");
    const submit = page.locator('[data-testid="move-submit"] button');
    assert.ok(await submit.isDisabled(), "ohne Gegenseite gesperrt");

    await page.locator('[data-testid="move-counterpart"] button').click();
    const firstOption = page.locator('.bottom-sheet.layer-2 [role="option"]').first();
    await firstOption.waitFor();
    assert.match(await text(firstOption), /Zu verteilen.*35,45 €/, "Zu verteilen steht ganz oben, mit Betrag");
    const options = await page.locator('.bottom-sheet.layer-2 [role="option"]').allInnerTexts();
    assert.ok(!options.some((o) => o.includes("Lebensmittel afd8h")), "X ist nicht wählbar");

    await page.locator('.bottom-sheet.layer-2 input').fill("urlaub");
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();

    await page.locator('[data-testid="move-amount"] input').fill("20");
    const after = await page.locator('[data-testid="move-after-line"]').allInnerTexts();
    assert.match(after[0].replace(/\s+/g, " "), /Lebensmittel afd8h 60,00 €/);
    assert.match(after[1].replace(/\s+/g, " "), /Urlaub afd8h -10,00 €/);
    const negative = page.locator('[data-testid="move-after-line"]').nth(1).locator("span").nth(1);
    assert.match(await negative.getAttribute("class"), /text-neon-red/, "negatives Ergebnis ist rot");

    await afterPicker(page);
    await submit.click();
    await page.locator('[data-testid="move-dialog"]').waitFor({ state: "detached" });
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
    await visibleRow(page, "Lebensmittel afd8h").locator('[data-testid="budget-available"]', { hasText: "60,00 €" }).waitFor();
    assert.match(await text(visibleRow(page, "Urlaub afd8h")), /-10,00 €/);
    assert.equal(calls.move.length, 1);
    assert.ok(calls.move[0].includes(guid(1)) && calls.move[0].includes(guid(2)), "Quelle und Ziel im Auftrag");
    assert.ok(calls.move[0].includes("2026-11-01"), "der Client schickt den gezeigten Monat");
  } finally {
    await context.close();
  }
});

test("Handy: scheitert das Lesen des Monats, bleibt das Sheet offen und nennt den Grund", async () => {
  const { context, page } = await openPage(390, 844, {
    move: () => ({ Error: "YNAB antwortet nicht afd8h" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Urlaub afd8h");
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Urlaub afd8h decken aus/);
    assert.equal(await page.locator('[data-testid="move-amount"] input').inputValue(), "30,00");
    await page.locator('[data-testid="move-counterpart"] button').click();
    await page.locator('[data-testid="category-picker-to-be-budgeted"]').click();
    await afterPicker(page);
    await page.locator('[data-testid="move-submit"] button').click();
    await page.locator('[data-testid="move-error"]').getByText("YNAB antwortet nicht afd8h").waitFor();
    assert.ok(await page.locator('[data-testid="move-dialog"]').isVisible(), "Sheet bleibt offen");
  } finally {
    await context.close();
  }
});

test("Fehlgeschlagener PATCH: der Toast bietet Erneut senden an, der Klick sendet dieselbe Anwendung", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Ok: report("Written") }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    await page.locator('[data-testid="move-counterpart"] button').click();
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
    await afterPicker(page);
    await page.locator('[data-testid="move-submit"] button').click();
    await page.getByText(/Nicht vollständig verschoben: Rate-Limit/).first().waitFor();
    await page.locator('button:has-text("Erneut senden"):not([data-testid="move-retry"] button)').click();
    await page.locator('[data-testid="move-retry"]').waitFor({ state: "detached" });
    await page.getByText("20,00 € von Lebensmittel afd8h nach Urlaub afd8h").first().waitFor();
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
  } finally {
    await context.close();
  }
});

const failMove = async (page) => {
  await tapAvailable(page, "Lebensmittel afd8h");
  await page.locator('[data-testid="move-counterpart"] button').click();
  await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
  await afterPicker(page);
  await page.locator('[data-testid="move-submit"] button').click();
  await page.locator('[data-testid="move-retry"]').waitFor();
};

test("Erneut senden scheitert: ein Toast nennt den Grund, der Monat lädt neu, der Hinweis bleibt", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Error: "YNAB antwortet nicht nfrds" }),
  });
  try {
    await failMove(page);
    await page.locator('[data-testid="move-retry"] button').click();
    await page.getByText(/Erneut senden gescheitert: YNAB antwortet nicht nfrds/).first().waitFor();
    const loaded = calls.month;
    await page.waitForFunction(() => document.querySelector('[data-testid="move-retry"] button:not([disabled])'));
    assert.ok(loaded >= 3, `Monat neu gelesen: Seite, nach Verschieben, nach Erneut senden (${loaded})`);
    assert.equal(await page.locator('[data-testid="move-retry"]').count(), 1, "bleibt sendbar");
  } finally {
    await context.close();
  }
});

test("Erneut senden bleibt erreichbar, nachdem der Toast weg ist", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => ({ Ok: report({ Failed: "Rate-Limit" }) }),
    retry: () => ({ Ok: report("Written") }),
  });
  try {
    await failMove(page);
    const toast = page.getByRole("button", { name: "Erneut senden" });
    await page.waitForFunction(
      () => ![...document.querySelectorAll("button")].filter((b) => b.textContent.includes("Erneut senden")).some((b) => !b.closest('[data-testid="move-retry"]')),
      undefined,
      { timeout: 15000 }
    );
    assert.equal(await toast.count(), 1, "nur noch der Knopf auf der Seite");
    await toast.click();
    await page.locator('[data-testid="move-retry"]').waitFor({ state: "detached" });
    assert.deepEqual(calls.retry, [["11111111-1111-1111-1111-111111111111"]]);
  } finally {
    await context.close();
  }
});

test("Bricht der Aufruf ab, schließt das Sheet, nennt die Unklarheit und liest den Monat neu", async () => {
  const { context, page, calls } = await openPage(390, 844, {
    move: () => "abort",
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    await page.locator('[data-testid="move-counterpart"] button').click();
    await page.locator('.bottom-sheet.layer-2 [role="option"]').filter({ hasText: "Urlaub afd8h" }).click();
    await afterPicker(page);
    const before = calls.month;
    await page.locator('[data-testid="move-submit"] button').click();
    await page.getByText(/Ob verschoben wurde, ist unklar/).first().waitFor();
    await page.locator('[data-testid="move-dialog"]').waitFor({ state: "detached" });
    await page.locator('[data-testid="budget-category-row"]:visible').first().waitFor();
    for (let i = 0; i < 100 && calls.month <= before; i++) await page.evaluate(() => new Promise(requestAnimationFrame));
    assert.ok(calls.month > before, "Monat neu gelesen");
    assert.equal(calls.move.length, 1, "kein zweiter Versuch");
  } finally {
    await context.close();
  }
});

test("Ein offenes Sheet springt nach dem Seitenwechsel nicht wieder auf", async () => {
  const { context, page } = await openPage(390, 844, {
    move: () => ({ Error: "nicht erwartet" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    await page.evaluate(() => { location.hash = "#/ledger"; });
    await page.locator('[data-testid="budget-page"]').waitFor({ state: "detached" });
    await page.evaluate(() => { location.hash = "#/budget"; });
    await page.locator('[data-testid="budget-to-be-budgeted"]').waitFor();
    assert.equal(await page.locator('[data-testid="move-dialog"]').count(), 0, "Sheet bleibt zu");
  } finally {
    await context.close();
  }
});

test("PC: der Auslöser schließt das offene Popover, es steht bei 1280×720 ohne Überstand im Dialog", async () => {
  const { context, page } = await openPage(1280, 720, {
    move: () => ({ Error: "nicht erwartet" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    const trigger = page.locator('[data-testid="move-counterpart"] button');
    await trigger.click();
    const hint = page.locator('[data-testid="category-picker-footer-hint"]');
    await hint.waitFor();
    const dialog = await page.locator(".max-w-lg").boundingBox();
    const popover = await page.locator(".rounded-2xl.bg-surface-elevated.shadow-2xl").last().boundingBox();
    assert.ok(dialog && popover, "beide da");
    assert.ok(popover.x >= dialog.x && popover.x + popover.width <= dialog.x + dialog.width, "seitlich im Dialog");
    // Bug budgeting-nfrds: der Mausdruck zählte als Klick außerhalb und schloss das Popover, der
    // Klick öffnete es wieder. Ohne Sucheintrag verdeckt das eine Layout-Verschiebung: das volle
    // Popover scrollt den Dialog, beim Schließen springt der Auslöser unter dem Zeiger weg, der
    // Klick trifft ihn nicht. Der Filter kürzt das Popover, der Auslöser bleibt liegen.
    await page.keyboard.type("zzzz");
    await page.locator('[data-testid="category-picker-to-be-budgeted"]').waitFor({ state: "detached" });
    await trigger.click();
    await page.evaluate(() => new Promise((done) => requestAnimationFrame(() => requestAnimationFrame(done))));
    assert.equal(await hint.count(), 0, "bleibt zu");
  } finally {
    await context.close();
  }
});

test("PC: Tipp auf Verfügbar öffnet den Dialog, der Picker hängt als Popover daran und wird nicht abgeschnitten", async () => {
  const { context, page } = await openPage(1440, 900, {
    move: () => ({ Error: "nicht erwartet" }),
    retry: () => ({ Error: "nicht erwartet" }),
  });
  try {
    await tapAvailable(page, "Lebensmittel afd8h");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "kein Sheet am PC");
    await page.locator('[data-testid="move-counterpart"] button').click();
    const top = page.locator('[data-testid="category-picker-to-be-budgeted"]');
    await top.waitFor();
    const options = page.locator('[role="option"]');
    assert.equal(await options.count(), 3, "Zu verteilen und zwei Kategorien, X fehlt");
    const last = await options.last().boundingBox();
    assert.ok(last && last.y + last.height <= 900, "die Liste liegt im Bild");
    await options.last().click();
    assert.match(await text(page.locator('[data-testid="move-headline"]')), /Von Lebensmittel afd8h nach Kleidung afd8h/);
    assert.equal(await page.locator('[role="option"]').count(), 0, "Popover ist zu");
  } finally {
    await context.close();
  }
});
