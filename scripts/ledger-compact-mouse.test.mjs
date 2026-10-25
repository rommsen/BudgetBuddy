// scripts/ledger-compact-mouse.test.mjs
//
// Gerenderter Beleg für `ledger-m6zpd` (Kompaktliste nach Eingabegerät, ADR 0053 Punkt 6).
// Maus in einem Fenster, in dem die Kompaktliste steht (kein `table`): Kästchen, Umschalt-Klick,
// markierte Zeile (`data-keyboard-focus`), G, Leertaste, Enter als Sheet, Hover-Schaltfläche,
// Fußzeilen-Hinweis und PC-Leiste. Touch-Emulation: langes Drücken wie bisher, ohne Kästchen
// und Hinweis davor. Ein echter, per Vite gestarteter Fable-Client gegen gemockte Ledger-Antworten.
//
// Ausführen: node --test scripts/ledger-compact-mouse.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildEntryFixture } from "./ledger-status-column.mjs";

const TOTAL = 20;
const label = (n) => `Kompaktzeile m6zpd ${String(n).padStart(2, "0")}`;

let frontend;
let frontendPort;
let browser;

function rowFixtures(accountId) {
  return Array.from({ length: TOTAL }, (_, i) =>
    buildEntryFixture(accountId, i + 1, {
      Payee: label(i + 1),
      Date: "2026-03-10T00:00:00.0000000",
      Amount: { Amount: -10, Currency: "EUR" },
      NeedsReview: true,
    })
  );
}

async function openLedger(contextOptions) {
  const context = await browser.newContext(contextOptions);
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const calls = [];
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
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: all,
        TotalCount: TOTAL,
        TotalAmount: { Amount: -200, Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: TOTAL, AskLenaCount: 0,
      }),
    })
  );
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: new Date().toISOString(),
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    })
  );
  await page.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify([]) })
  );
  await page.route("**/api/LedgerApi/setReviewMark", (route) => {
    calls.push({ endpoint: "setReviewMark", body: route.request().postData() });
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Ok: {
          Previous: { IsApproved: false, ClearedState: "ReviewUncleared" },
          Change: "ReviewMarkChanged",
          WriteBack: "ReviewMarkSent",
        },
      }),
    });
  });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await page.getByText(label(1), { exact: false }).first().waitFor();
  await page.waitForTimeout(500);
  return { page, context, calls, entries: all };
}

const visibleBar = (page) => page.locator(".selection-bar").filter({ visible: true });
const box = (page, n) => page.getByRole("checkbox", { name: `${label(n)} auswählen` });
const rowOf = (page, n) => page.locator("div.group").filter({ has: box(page, n) });
const hint = (page) => page.getByText("Leertaste auswählen").filter({ visible: true });
const opacityOf = (locator) => locator.evaluate((el) => Number(getComputedStyle(el).opacity));

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("Maus 900 px: Kästchen, Umschalt-Klick, PC-Leiste statt Hinweis", async () => {
  const { page, context } = await openLedger({ viewport: { width: 900, height: 800 } });
  try {
    assert.equal(await page.locator("table").filter({ visible: true }).count(), 0, "bei dieser Breite steht die Kompaktliste");
    assert.equal(await page.evaluate(() => matchMedia("(pointer: fine)").matches), true, "die Maus meldet pointer: fine");
    await hint(page).waitFor();
    assert.ok(await box(page, 1).isVisible(), "das Kästchen steht ohne Hover da");

    await box(page, 1).click();
    const bar = visibleBar(page);
    await bar.waitFor();
    assert.match(await bar.innerText(), /1 ausgewählt/, "ein Klick wählt die Zeile");
    assert.equal(await hint(page).count(), 0, "bei Auswahl steht die Leiste statt des Hinweises");
    assert.equal(await page.getByText("Auswahl beenden").count(), 0, "kein Auswahl-Modus der Handy-Leiste");

    await box(page, 5).click({ modifiers: ["Shift"] });
    assert.match(await bar.innerText(), /5 ausgewählt/, "Umschalt-Klick wählt die Spanne");
    for (const n of [1, 2, 3, 4, 5]) {
      assert.equal(await box(page, n).getAttribute("aria-checked"), "true", `Zeile ${n} gewählt`);
    }
    assert.equal(await box(page, 6).getAttribute("aria-checked"), "false", "Zeile 6 nicht gewählt");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "kein Detail geöffnet");
  } finally {
    await context.close();
  }
});

test("Maus 900 px: Tasten, Markierung, Hover-Schaltfläche, Sheet", async () => {
  const { page, context, calls, entries } = await openLedger({ viewport: { width: 900, height: 800 } });
  try {
    assert.equal(await page.locator("table").filter({ visible: true }).count(), 0, "Kompaktliste");

    await page.keyboard.press("ArrowDown");
    const marked = page.locator("[data-keyboard-focus='true']");
    await marked.waitFor();
    assert.equal(await marked.count(), 1, "genau eine markierte Zeile");
    assert.ok((await marked.innerText()).includes(label(1)), "die erste Zeile ist markiert");
    assert.equal(await marked.getAttribute("aria-selected"), "true");
    assert.equal(await opacityOf(marked.getByRole("button", { name: "Geprüft", exact: true })), 1, "markierte Zeile zeigt die Schaltfläche");

    // Hover: eine nicht markierte Zeile zeigt die Schaltfläche erst beim Darüberfahren.
    const other = rowOf(page, 3).getByRole("button", { name: "Geprüft", exact: true });
    assert.equal(await opacityOf(other), 0, "ohne Hover unsichtbar");
    await rowOf(page, 3).hover();
    await page.waitForTimeout(300);
    assert.equal(await opacityOf(other), 1, "bei Hover sichtbar");
    await page.mouse.move(0, 0);

    await page.keyboard.press("g");
    await rowOf(page, 1).getByRole("button", { name: "Ungeprüft", exact: true }).waitFor();
    const call = calls.find((c) => c.endpoint === "setReviewMark");
    assert.ok(call, "G ruft setReviewMark");
    assert.deepEqual(JSON.parse(call.body), [entries[0].Id, "Reviewed"]);

    // G springt auf die nächste Zeile: die markierte Zeile ist jetzt Zeile 2.
    assert.ok((await marked.innerText()).includes(label(2)), "der Fokus ist auf Zeile 2 gesprungen");
    await page.keyboard.press(" ");
    await visibleBar(page).waitFor();
    assert.equal(await box(page, 2).getAttribute("aria-checked"), "true", "Leertaste wählt die markierte Zeile");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "Leertaste öffnet kein Detail");
    // Bei laufender Auswahl ruht die Schaltfläche (Platzhalter wie in der Tabelle).
    assert.equal(await rowOf(page, 3).getByRole("button", { name: "Geprüft", exact: true }).count(), 0, "Schaltfläche ruht");

    await page.keyboard.press("Escape");
    assert.equal(await visibleBar(page).count(), 0, "Esc hebt die Auswahl auf");

    await page.keyboard.press("Enter");
    await page.locator(".bottom-sheet.active").waitFor();
    await page.keyboard.press("Escape");
    await page.locator(".bottom-sheet.active").waitFor({ state: "detached" });
  } finally {
    await context.close();
  }
});

test("Maus 900 px: nach einem Klick auf die Zeile wählt die Leertaste die markierte Zeile", async () => {
  const { page, context } = await openLedger({ viewport: { width: 900, height: 800 } });
  try {
    await page.getByText(label(2), { exact: true }).first().click();
    const dialog = page.locator(".bottom-sheet.active");
    await dialog.waitFor();
    await page.keyboard.press("Escape");
    await dialog.waitFor({ state: "detached" });
    assert.notEqual(
      await page.evaluate(() => document.activeElement && document.activeElement.tagName),
      "BUTTON",
      "die Zeile hält keinen Knopf-Fokus"
    );

    await page.keyboard.press("ArrowDown");
    const marked = page.locator("[data-keyboard-focus='true']");
    await marked.waitFor();
    const markedText = await marked.innerText();
    await page.keyboard.press(" ");
    await visibleBar(page).waitFor();
    assert.match(await visibleBar(page).innerText(), /1 ausgewählt/);
    const selected = await page.getByRole("checkbox", { checked: true }).first().getAttribute("aria-label");
    assert.ok(markedText.includes(selected.replace(" auswählen", "")), "gewählt ist die markierte Zeile");
    assert.equal(await page.locator(".bottom-sheet.active").count(), 0, "kein Detail");
  } finally {
    await context.close();
  }
});

/// Langes Drücken mit Touch-Zeigerereignissen (Playwright kennt keine Haltegeste).
async function longPress(locator) {
  const b = await locator.boundingBox();
  const x = b.x + b.width / 2;
  const y = b.y + b.height / 2;
  const init = { pointerType: "touch", pointerId: 7, isPrimary: true, bubbles: true, clientX: x, clientY: y };
  await locator.dispatchEvent("pointerdown", init);
  await locator.page().waitForTimeout(700);
  await locator.dispatchEvent("pointerup", init);
  await locator.dispatchEvent("click", { bubbles: true, clientX: x, clientY: y });
}

for (const [name, viewport] of [
  ["Touch 390 px", { width: 390, height: 844 }],
  ["Touch Tablet 820 px", { width: 820, height: 1100 }],
]) {
  test(`${name}: langes Drücken wie bisher, davor kein Kästchen und kein Hinweis`, async (t) => {
    const { page, context } = await openLedger({ viewport, hasTouch: true, isMobile: true });
    try {
      const coarse = await page.evaluate(() => matchMedia("(pointer: coarse)").matches);
      if (!coarse) {
        // Die Emulation meldet pointer: fine; der Fall bleibt durch den Expecto-Test gedeckt.
        t.diagnostic(`${name}: pointer: coarse nicht gemeldet, Touch-Fall nur im Expecto-Test gedeckt`);
        return;
      }
      if ((await page.locator("table").filter({ visible: true }).count()) > 0) {
        t.diagnostic(`${name}: bei dieser Breite steht die Tabelle, Kompaktliste nicht prüfbar`);
        return;
      }
      assert.equal(await page.getByRole("checkbox").filter({ visible: true }).count(), 0, "vor dem Modus kein Kästchen");
      assert.equal(await hint(page).count(), 0, "kein Fußzeilen-Hinweis");
      const row = page.locator("button", { hasText: label(1) }).filter({ visible: true }).first();
      assert.ok(
        await page.getByRole("button", { name: "Geprüft", exact: true }).filter({ visible: true }).first().isVisible(),
        "Prüf-Schaltfläche sichtbar"
      );
      await longPress(row);
      await visibleBar(page).waitFor();
      assert.match(await visibleBar(page).innerText(), /1 ausgewählt/, "langes Drücken startet die Auswahl");
    } finally {
      await context.close();
    }
  });
}
