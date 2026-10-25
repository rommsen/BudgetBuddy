// scripts/ledger-lena-batch.test.mjs
//
// Wächter für `ledger-y54gr` (ADR 0053 Punkt 2, ADR 0056): "Lena fragen" und "Rückfrage
// zurücknehmen" in der Auswahl-Leiste. Gemessen am echten, per Vite gestarteten Fable-Client
// gegen gemockte Ledger-Antworten (erfundene Werte, nie die Entwicklungsdatenbank): die beiden
// Aktionen mit der Zahl der Auswahl (PC einreihig, Handy dritte Reihe), sekundär statt Orange,
// der Sammelaufruf mit den gewählten Ids, der Toast mit einem Rückgängig für alle, und die
// gesperrte Aktion ohne Flag-Farbe mit dem Grund.
//
// Ausführen: node --test scripts/ledger-lena-batch.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockFilterBarApi,
} from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;

const TODAY = new Date().toISOString().slice(0, 10) + "T00:00:00.0000000";
const PAYEES = ["Rewe y54gr", "Bahn y54gr", "Kiosk y54gr", "Bäcker y54gr"];

const state = {
  lenaColor: "purple",
  setCalls: [],
  restoreCalls: [],
  flagOf: {},
  writeBack: "FlagSent",
};

const idOf = (n) => buildEntryFixture("x", n).Id;

function entries(accountId) {
  return PAYEES.map((payee, i) =>
    buildEntryFixture(accountId, i + 1, {
      Date: TODAY,
      LinkedToYnab: true,
      NeedsReview: false,
      Payee: payee,
      CategoryId: null,
      CategoryName: null,
      CategoryGroupName: null,
      Flag: state.flagOf[i + 1] ?? "NoFlag",
      FlagWriteBackFailure: null,
    })
  );
}

async function open(context) {
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(page, {
    accountsFixture,
    balancesFixture,
    resolveEntries: () => entries(accountsFixture.DefaultAccountId),
  });
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const list = entries(accountsFixture.DefaultAccountId);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: list,
        TotalCount: list.length,
        TotalAmount: { Amount: list.reduce((s, e) => s + e.Amount.Amount, 0), Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: 0,
        AskLenaCount: list.filter((e) => e.Flag === "AskLena").length,
      }),
    });
  });
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ SavedColor: state.lenaColor, SuggestedColor: null }),
    })
  );
  await page.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await page.route("**/api/LedgerApi/setLenaFlagBatch", (route) => {
    const [selection, flagTarget] = route.request().postDataJSON();
    state.setCalls.push({ selection, flagTarget });
    const ids = selection.BatchPicked ?? selection.BatchPicked?.[0] ?? [];
    const changes = ids.map((id) => ({
      EntryId: id,
      Previous: { Color: null, Name: null },
      WriteBack: state.writeBack,
    }));
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { Changed: changes, UnchangedCount: 0, FailedCount: 0 } }),
    });
  });
  await page.route("**/api/LedgerApi/restoreFlagBatch", (route) => {
    const [items] = route.request().postDataJSON();
    state.restoreCalls.push(items);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: {
          Changed: items.map((i) => ({
            EntryId: i.EntryId,
            Previous: { Color: "purple", Name: "Lena" },
            WriteBack: "FlagSent",
          })),
          UnchangedCount: 0,
          FailedCount: 0,
        },
      }),
    });
  });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return page;
}

function resetState() {
  state.lenaColor = "purple";
  state.setCalls.length = 0;
  state.restoreCalls.length = 0;
  state.flagOf = {};
  state.writeBack = "FlagSent";
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

const visibleBar = (page) => page.locator(".selection-bar").filter({ visible: true });
const box = (page, payee) =>
  page.getByRole("checkbox", { name: `${payee} auswählen` }).filter({ visible: true });

test("PC: Lena fragen für zwei Buchungen, ein Aufruf, ein Toast mit Rückgängig für beide", async () => {
  resetState();
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  try {
    await box(page, PAYEES[0]).click();
    await box(page, PAYEES[1]).click();
    const bar = visibleBar(page);
    await bar.waitFor();

    const ask = bar.getByRole("button", { name: "Lena fragen (2)", exact: true });
    const withdraw = bar.getByRole("button", { name: "Rückfrage zurücknehmen (2)", exact: true });
    assert.equal(await ask.count(), 1, "Lena fragen mit Anzahl");
    assert.equal(await withdraw.count(), 1, "Rückfrage zurücknehmen mit Anzahl");
    for (const button of [ask, withdraw]) {
      assert.ok(!(await button.getAttribute("class")).includes("from-neon-orange"), "sekundär, nicht orange");
    }
    const reviewed = bar.getByRole("button", { name: "Geprüft", exact: true });
    assert.ok((await reviewed.getAttribute("class")).includes("from-neon-orange"), "Geprüft bleibt die primäre Aktion");
    // Eine Reihe, alle Knöpfe im Bild.
    const names = ["Auswahl aufheben", "Rückfrage zurücknehmen (2)", "Lena fragen (2)", "Kategorie setzen", "Geprüft"];
    const boxes = [];
    for (const name of names) boxes.push(await bar.getByRole("button", { name, exact: true }).boundingBox());
    assert.ok(boxes.every((b) => Math.abs(b.y + b.height / 2 - (boxes[0].y + boxes[0].height / 2)) <= 3), "eine Reihe");
    assert.ok(boxes.every((b, i) => i === 0 || boxes[i - 1].x + boxes[i - 1].width <= b.x + 1), "Reihenfolge ohne Überlappung");

    await ask.click();
    await page.getByText("2 Buchungen an Lena gefragt", { exact: true }).waitFor();
    assert.equal(state.setCalls.length, 1, "genau ein Sammelaufruf");
    assert.equal(state.setCalls[0].flagTarget, "AssignLena");
    assert.deepEqual([...state.setCalls[0].selection.BatchPicked].sort(), [idOf(1), idOf(2)].sort(), "die gewählten Ids");
    assert.equal(await visibleBar(page).count(), 0, "die Auswahl endet");
    const lenaMark = (payee) => page.locator("table tbody tr", { hasText: payee }).first().textContent();
    assert.ok((await lenaMark(PAYEES[0])).includes("Lena?"), "Kennzeichen in Zeile 1");
    assert.ok((await lenaMark(PAYEES[1])).includes("Lena?"), "Kennzeichen in Zeile 2");
    assert.ok(!(await lenaMark(PAYEES[2])).includes("Lena?"), "Zeile 3 unberührt");

    await page.getByRole("button", { name: "Rückgängig" }).click();
    await page.getByText("Flag bei 2 Buchungen zurückgesetzt", { exact: true }).waitFor();
    assert.equal(state.restoreCalls.length, 1, "ein Rückgängig-Aufruf für alle");
    assert.deepEqual(state.restoreCalls[0].map((i) => i.EntryId).sort(), [idOf(1), idOf(2)].sort());
    assert.ok((await lenaMark(PAYEES[0])).includes("Lena?") === false, "Zeile 1 wieder ohne Kennzeichen");
  } finally {
    await context.close();
  }
});

test("PC: ohne gesetzte Farbe ist Lena fragen gesperrt mit Grund im Tooltip, Zurücknehmen bleibt frei", async () => {
  resetState();
  state.lenaColor = null;
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await open(context);
  try {
    await box(page, PAYEES[0]).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    const ask = bar.getByRole("button", { name: "Lena fragen (1)", exact: true });
    assert.equal(await ask.isDisabled(), true, "gesperrt");
    assert.equal(await ask.getAttribute("title"), "In den Settings die Flag-Farbe für Lena wählen", "Grund als Tooltip");
    assert.equal(await bar.getByRole("button", { name: "Rückfrage zurücknehmen (1)", exact: true }).isDisabled(), false);
    assert.equal(state.setCalls.length, 0, "kein Aufruf");
  } finally {
    await context.close();
  }
});

test("Handy 390x844: dritte Reihe mit beiden Aktionen, der Grund als Zeile, nichts läuft über", async () => {
  resetState();
  state.lenaColor = null;
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true });
  const page = await open(context);
  try {
    // Auswahl-Modus per langem Drücken (wie in ledger-selection.test.mjs).
    const row = page.locator("button", { hasText: PAYEES[0] }).filter({ visible: true }).first();
    const b = await row.boundingBox();
    const init = { pointerType: "touch", pointerId: 7, isPrimary: true, bubbles: true, clientX: b.x + b.width / 2, clientY: b.y + b.height / 2 };
    await row.dispatchEvent("pointerdown", init);
    await page.waitForTimeout(700);
    await row.dispatchEvent("pointerup", init);
    await row.dispatchEvent("click", { bubbles: true, clientX: init.clientX, clientY: init.clientY });
    const bar = visibleBar(page);
    await bar.waitFor();

    const ask = bar.getByRole("button", { name: "Lena fragen (1)", exact: true });
    const withdraw = bar.getByRole("button", { name: "Rückfrage zurücknehmen (1)", exact: true });
    const reviewedBtn = bar.getByRole("button", { name: "Geprüft", exact: true });
    const askBox = await ask.boundingBox();
    const withdrawBox = await withdraw.boundingBox();
    const reviewedBox = await reviewedBtn.boundingBox();
    assert.ok(askBox.y > reviewedBox.y + reviewedBox.height - 2, "Reihe 3 unter Reihe 2");
    assert.ok(Math.abs(askBox.y - withdrawBox.y) <= 3, "beide in einer Reihe");
    for (const [name, rect] of [["Lena fragen", askBox], ["Rückfrage zurücknehmen", withdrawBox]]) {
      assert.ok(rect.x >= 0 && rect.x + rect.width <= 390, `${name} liegt im Bildschirm: ${JSON.stringify(rect)}`);
    }
    const reason = bar.locator('[data-testid="selection-lena-reason"]');
    assert.equal(await reason.textContent(), "In den Settings die Flag-Farbe für Lena wählen");
    assert.ok(await ask.isDisabled(), "ohne Farbe gesperrt");
    assert.ok(
      await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth),
      "kein seitliches Scrollen"
    );
    // Die Leiste verdeckt die letzte Zeile nicht dauerhaft: der Abstandshalter ist so hoch wie die Leiste.
    const barHeight = (await bar.boundingBox()).height;
    const spacerHeight = await page.evaluate(() => {
      const el = [...document.querySelectorAll("div.md\\:hidden")].find((d) => d.getAttribute("aria-hidden") === "true");
      return el ? el.getBoundingClientRect().height : 0;
    });
    assert.ok(spacerHeight >= barHeight, `Abstandshalter ${spacerHeight} >= Leiste ${barHeight}`);
  } finally {
    await context.close();
  }
});
