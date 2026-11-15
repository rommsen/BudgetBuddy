// scripts/ledger-review-mark.test.mjs
//
// Gerenderter Beleg für `ledger-r777c` (Prüfvermerk setzen und nach YNAB zurückschreiben): am PC
// (1440x900) die Taste G auf der markierten Zeile (seit ledger-cnaz3 ohne Knopf an der Zeile) und am
// Handy (412x915) der Wisch nach rechts (ohne Knopf an der Zeile seit ledger-d9z3n). Ein echter,
// per Vite gestarteter Fable-Client gegen gemockte Ledger-Antworten (erfundene Werte, nie
// die Entwicklungsdatenbank). Die beiden schreibenden Endpunkte werden abgefangen,
// bevor sie das Netzwerk erreichen: es gibt keinen Backend-Prozess und damit keinen
// YNAB-Aufruf.
//
// Optional: REVIEW_MARK_SCREENSHOTS=<verzeichnis> legt je Viewport ein Bild ab.
//
// Ausführen: node --test scripts/ledger-review-mark.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { mkdirSync } from "node:fs";
import path from "node:path";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntryFixture,
  mockLedgerApi,
} from "./ledger-status-column.mjs";

const PAYEES = {
  toReview: "Prüfvermerk offen",
  reviewed: "Prüfvermerk erledigt",
  failed: "Prüfvermerk Fehlschlag",
  deleted: "Prüfvermerk gelöscht",
};
const FAILURE_REASON = "Keine Verbindung zu YNAB: Timeout";

let frontend;
let frontendPort;
let browser;

function buildEntries(accountId) {
  const entries = [
    buildEntryFixture(accountId, 1, { Payee: PAYEES.toReview, NeedsReview: true }),
    buildEntryFixture(accountId, 2, { Payee: PAYEES.reviewed, NeedsReview: false }),
    buildEntryFixture(accountId, 3, {
      Payee: PAYEES.failed,
      NeedsReview: false,
      ReviewMarkWriteBackFailure: FAILURE_REASON,
      LinkedToYnab: false,
      PushToYnabFailure: null,
    }),
    // In YNAB gelöscht: der Server lehnt jeden Prüfvermerk ab, also keine Schaltfläche.
    buildEntryFixture(accountId, 4, { Payee: PAYEES.deleted, NeedsReview: false, DeletedInYnab: true }),
  ];
  return {
    Entries: entries,
    TotalCount: entries.length,
    TotalAmount: { Amount: entries.reduce((s, e) => s + e.Amount.Amount, 0), Currency: "EUR" },
    DeletedCount: entries.filter((e) => e.DeletedInYnab).length,
    NeedsReviewCount: entries.filter((e) => e.NeedsReview).length, AskLenaCount: 0, WithoutCategoryCount: 0,
  };
}

/// Öffnet die Seite im gegebenen Viewport mit gemockter API. `calls` sammelt die Anfragen
/// an die schreibenden Endpunkte (Pfad und Body).
async function openLedger(viewport) {
  return openLedgerWith(viewport, {});
}

async function openLedgerWith(viewport, options) {
  const context = await browser.newContext({ viewport, ...options });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accounts = buildAccountsFixture(2);
  const balances = buildBalancesFixture(accounts.Accounts);
  const entries = buildEntries(accounts.DefaultAccountId);
  const calls = [];

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture: accounts, balancesFixture: balances, entriesFixture: entries });
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
  await page.route("**/api/LedgerApi/retryReviewMarkWriteBack", (route) => {
    calls.push({ endpoint: "retryReviewMarkWriteBack", body: route.request().postData() });
    route.fulfill({ contentType: "application/json", body: JSON.stringify({ Ok: "ReviewMarkSent" }) });
  });

  // Ohne bekannten oder bei einem alten Lauf stößt die Seite beim Öffnen den Spiegel
  // an (Frische-Kriterium, ADR 0014) und lädt danach die Liste neu: das ließe die Zeilen
  // zwischen Messung und Klick verschwinden. Ein frischer, erfolgreicher Lauf als
  // gemockte Antwort hält die Seite ruhig.
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: new Date().toISOString(),
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    })
  );

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return { page, context, calls, entries };
}

/// Die sichtbare Zeile eines Payees: am PC die Tabellenzeile, am Handy der Container
/// aus Zeilen-Button und Schaltfläche. Beide Darstellungen stehen im DOM, nur eine ist sichtbar.
function rowOf(page, isDesktop, payee) {
  return isDesktop
    ? page.locator("tr", { hasText: payee }).filter({ visible: true }).first()
    : page.locator("div.flex.items-stretch", { hasText: payee }).filter({ visible: true }).first();
}

async function screenshotIfRequested(page, name) {
  const dir = process.env.REVIEW_MARK_SCREENSHOTS;
  if (!dir) return;
  mkdirSync(dir, { recursive: true });
  await page.screenshot({ path: path.join(dir, `${name}.png`) });
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

/// Wischt per Touch über `locator` (CDP, wie `scripts/durchgehen.test.mjs`): `dx` Pixel nach rechts.
async function swipe(page, locator, dx) {
  const box = await locator.boundingBox();
  const cdp = await page.context().newCDPSession(page);
  const y = box.y + box.height / 2;
  const x0 = box.x + 24;
  const point = (x) => [{ x, y, id: 1 }];
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: point(x0) });
  for (let i = 1; i <= 8; i++) {
    await cdp.send("Input.dispatchTouchEvent", { type: "touchMove", touchPoints: point(x0 + (dx * i) / 8) });
  }
  await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  await cdp.detach();
}

// ledger-d9z3n: unter 1024 px hat die Zeile keinen Prüfknopf mehr, geprüft wird per Wisch nach rechts (ledger-th36h).
// ledger-cnaz3: auch am PC hat die Zeile keinen Knopf mehr, geprüft wird mit G auf der markierten Zeile.
test("[flow:buchung-pruefen] PC: G auf der markierten Zeile schickt den Aufruf; Handy: Wisch nach rechts prüft", async () => {
  {
    const { page, context, calls, entries } = await openLedger({ width: 1440, height: 900 });
    try {
      const row = rowOf(page, true, PAYEES.toReview);
      await row.waitFor();
      // Die erste Zeile ist die zu prüfende: ↓ markiert sie, G prüft.
      await page.keyboard.press("ArrowDown");
      await page.locator('tr[data-keyboard-focus="true"]', { hasText: PAYEES.toReview }).waitFor();
      const sent = page.waitForResponse((response) => response.url().includes("setReviewMark"));
      await page.keyboard.press("g");
      await sent;
      const call = calls.find((c) => c.endpoint === "setReviewMark");
      assert.ok(call, "PC: G schickt den Prüfvermerk");
      assert.deepEqual(JSON.parse(call.body), [entries.Entries[0].Id, "Reviewed"], "PC: Id und Ziel");
      await screenshotIfRequested(page, "pc-1440x900-review-mark");
    } finally {
      await context.close();
    }
  }
  {
    const { page, context: phone, calls, entries } = await openLedgerWith({ width: 412, height: 915 }, { hasTouch: true, isMobile: true });
    try {
      const row = page.locator("button", { hasText: PAYEES.toReview }).filter({ visible: true }).first();
      await row.waitFor();
      await swipe(page, row, 260);
      await page.waitForFunction(() => document.body.innerText.includes("geprüft"));
      const call = calls.find((c) => c.endpoint === "setReviewMark");
      assert.ok(call, "Handy: der Wisch schickt den Prüfvermerk");
      assert.deepEqual(JSON.parse(call.body), [entries.Entries[0].Id, "Reviewed"], "Handy: Id und Ziel");
      await screenshotIfRequested(page, "handy-412x915-review-swipe");
    } finally {
      await phone.close();
    }
  }
});
