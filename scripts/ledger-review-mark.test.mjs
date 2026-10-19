// scripts/ledger-review-mark.test.mjs
//
// Gerenderter Beleg für `ledger-r777c` (Prüfvermerk setzen und nach YNAB zurückschreiben):
// die Schaltfläche "Geprüft"/"Ungeprüft" an der Zeile und der Hinweis "Prüfvermerk nicht
// in YNAB" mit "Erneut senden", am PC (1440x900) und am Handy (390x844). Ein echter,
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
    NeedsReviewCount: entries.filter((e) => e.NeedsReview).length,
  };
}

/// Öffnet die Seite im gegebenen Viewport mit gemockter API. `calls` sammelt die Anfragen
/// an die schreibenden Endpunkte (Pfad und Body).
async function openLedger(viewport) {
  const context = await browser.newContext({ viewport });
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
  await page.waitForTimeout(500);
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

for (const [label, viewport, isDesktop] of [
  ["PC 1440x900", { width: 1440, height: 900 }, true],
  ["Handy 390x844", { width: 390, height: 844 }, false],
]) {
  test(`${label}: zu prüfende Zeile zeigt Geprüft, geprüfte Zeile Ungeprüft, Fehlschlag den Hinweis mit Erneut senden`, async () => {
    const { page, context } = await openLedger(viewport);
    try {
      const toReview = rowOf(page, isDesktop, PAYEES.toReview);
      const reviewed = rowOf(page, isDesktop, PAYEES.reviewed);
      const failed = rowOf(page, isDesktop, PAYEES.failed);

      assert.equal(await toReview.getByRole("button", { name: "Geprüft", exact: true }).count(), 1, "zu prüfen: Geprüft");
      assert.equal(await reviewed.getByRole("button", { name: "Ungeprüft", exact: true }).count(), 1, "geprüft: Ungeprüft");

      // Der Hinweis steht an der Zeile (Handy: darunter im selben Container, PC: eigene Zeile darunter).
      const hint = page.getByText("Prüfvermerk nicht in YNAB", { exact: true }).filter({ visible: true });
      assert.equal(await hint.count(), 1, "genau ein Hinweis für genau einen Fehlschlag");
      assert.ok(
        (await page.getByText(FAILURE_REASON).filter({ visible: true }).count()) === 1,
        "der Grund steht neben dem Hinweis"
      );
      assert.equal(
        await page.getByRole("button", { name: "Erneut senden" }).filter({ visible: true }).count(),
        1,
        "Erneut senden"
      );
      assert.equal(await failed.count(), 1, "die Fehlschlag-Zeile ist da");

      // Die Schaltfläche ist eine eigene Aktion, kein Teil der Zeile: sie öffnet die Historie nicht.
      const buttonBox = await toReview.getByRole("button", { name: "Geprüft", exact: true }).boundingBox();
      assert.ok(buttonBox, "Schaltfläche hat eine Fläche");
      const minSide = isDesktop ? 24 : 40;
      assert.ok(
        buttonBox.width >= minSide && buttonBox.height >= minSide,
        `Trefferfläche mindestens ${minSide}px, war ${buttonBox.width}x${buttonBox.height}`
      );

      await screenshotIfRequested(page, `${isDesktop ? "pc-1440x900" : "handy-390x844"}-review-mark`);
    } finally {
      await context.close();
    }
  });

  test(`${label}: Klick auf Geprüft ruft setReviewMark mit Reviewed auf und die Zeile wechselt auf Ungeprüft`, async () => {
    const { page, context, calls, entries } = await openLedger(viewport);
    try {
      const toReview = rowOf(page, isDesktop, PAYEES.toReview);
      await toReview.getByRole("button", { name: "Geprüft", exact: true }).click();
      await rowOf(page, isDesktop, PAYEES.toReview)
        .getByRole("button", { name: "Ungeprüft", exact: true })
        .waitFor();

      const call = calls.find((c) => c.endpoint === "setReviewMark");
      assert.ok(call, "setReviewMark wurde aufgerufen");
      assert.deepEqual(JSON.parse(call.body), [entries.Entries[0].Id, "Reviewed"], "Eintrags-Id und Ziel Reviewed");
      assert.equal(
        await page.locator("dialog, [role=dialog]").filter({ visible: true }).count(),
        0,
        "der Klick öffnet nicht die Historie"
      );
    } finally {
      await context.close();
    }
  });

  test(`${label}: Erneut senden ruft retryReviewMarkWriteBack auf und der Hinweis verschwindet`, async () => {
    const { page, context, calls, entries } = await openLedger(viewport);
    try {
      await page.getByRole("button", { name: "Erneut senden" }).filter({ visible: true }).click();
      await page.getByText("Prüfvermerk nicht in YNAB", { exact: true }).filter({ visible: true }).waitFor({ state: "detached" });

      const call = calls.find((c) => c.endpoint === "retryReviewMarkWriteBack");
      assert.ok(call, "retryReviewMarkWriteBack wurde aufgerufen");
      assert.deepEqual(JSON.parse(call.body), [entries.Entries[2].Id], "Eintrags-Id");
    } finally {
      await context.close();
    }
  });

  test(`${label}: in YNAB gelöschte Zeile hat keine Prüfvermerk-Schaltfläche`, async () => {
    // Verhindert die Rückkehr des Fehlers, bei dem am Handy auch gelöschte Buchungen
    // die Schaltfläche bekamen und ein Klick in der Ablehnung des Servers endete.
    const { page, context } = await openLedger(viewport);
    try {
      const deleted = rowOf(page, isDesktop, PAYEES.deleted);
      assert.equal(await deleted.count(), 1, "die gelöschte Zeile ist sichtbar");
      assert.equal(
        await deleted.getByRole("button", { name: /^(Geprüft|Ungeprüft)$/ }).count(),
        0,
        "keine Schaltfläche Geprüft oder Ungeprüft"
      );
    } finally {
      await context.close();
    }
  });
}
