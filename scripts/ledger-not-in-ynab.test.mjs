// scripts/ledger-not-in-ynab.test.mjs
//
// Wächter für `ledger-cks06`: "Noch nicht in YNAB" vollständig. Eine Buchung mit gescheitertem
// Push trägt das Kennzeichen "Nicht in YNAB" in der Handy-Zeile und in der PC-Statusspalte
// (eine gelöschte Buchung bleibt "gelöscht", eine verknüpfte trägt keines), und die Leiste im
// Detail bietet "Erneut senden", das retryPushToYnab ruft. Gemessen wird am echten, per Vite
// gestarteten Fable-Client gegen gemockte Ledger-Antworten (Muster wie `ledger-detail.test.mjs`).
//
// Ausführen: node --test scripts/ledger-not-in-ynab.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildEntryFixture, mockFilterBarApi } from "./ledger-filter-bar.mjs";

let frontend;
let frontendPort;
let browser;
let page;
let mobilePage;
const retryCalls = [];
let retryAnswer = { Ok: "PushToYnabSent" };

const PAYEE_FAILED = "Kiosk cks06 gescheitert";
const PAYEE_LINKED = "Kiosk cks06 verknüpft";
const PAYEE_DELETED = "Kiosk cks06 gelöscht";
const PAYEE_REVIEW = "Kiosk cks06 zu prüfen";

// Zustand je Buchung, wie ihn der Server nach einem Retry liefern würde.
const linkedNow = new Set();

function entries(accountId) {
  const failed = buildEntryFixture(accountId, 1, {
    Payee: PAYEE_FAILED,
    CategoryName: null,
    CategoryId: null,
    LinkedToYnab: linkedNow.has(1),
    PushToYnabFailure: { Reason: "Netzwerkfehler: Timeout", FailedAt: "2026-10-25T09:00:00Z" },
  });
  return [
    failed,
    buildEntryFixture(accountId, 2, { Payee: PAYEE_LINKED, LinkedToYnab: true }),
    buildEntryFixture(accountId, 3, { Payee: PAYEE_DELETED, LinkedToYnab: false, DeletedInYnab: true }),
    buildEntryFixture(accountId, 4, { Payee: PAYEE_REVIEW, LinkedToYnab: true, NeedsReview: true }),
  ];
}

async function open(context) {
  const target = await context.newPage();
  target.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(2);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  await target.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockFilterBarApi(target, {
    accountsFixture,
    balancesFixture,
    resolveEntries: () => entries(accountsFixture.DefaultAccountId),
  });
  await target.route("**/api/LedgerApi/getEntryHistory", (route) =>
    route.fulfill({ contentType: "application/json", body: "[]" })
  );
  await target.route("**/api/LedgerApi/retryPushToYnab", (route) => {
    retryCalls.push(route.request().postDataJSON());
    route.fulfill({ contentType: "application/json", body: JSON.stringify(retryAnswer) });
  });
  // Offen lassen: sonst startet die Seite den Spiegel-Lauf in einer Schleife.
  await target.route("**/api/LedgerApi/mirrorYnabBudget", () => {});
  await target.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(target, "/ledger");
  return target;
}

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  page = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
  mobilePage = await open(
    await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true })
  );
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

const row = (p, text) => p.locator("table tbody tr", { hasText: text }).first();
const statusCell = (p, text) => row(p, text).locator("td").last();

test("PC: die Statusspalte nennt Nicht in YNAB, ohne Umbruch und ohne breitere Zeile", async () => {
  await row(page, PAYEE_FAILED).waitFor();
  assert.match(await statusCell(page, PAYEE_FAILED).textContent(), /Nicht in YNAB/);
  assert.doesNotMatch(await statusCell(page, PAYEE_LINKED).textContent(), /Nicht in YNAB/, "verknüpft: kein Kennzeichen");
  const measure = await page.evaluate(
    ([a, b]) => {
      const rowOf = (text) => [...document.querySelectorAll("table tbody tr")].find((r) => r.textContent.includes(text));
      const cell = (text) => rowOf(text).querySelector("td:last-child");
      const failed = cell(a);
      return {
        scrollWidth: failed.scrollWidth,
        clientWidth: failed.clientWidth,
        failedHeight: rowOf(a).getBoundingClientRect().height,
        linkedHeight: rowOf(b).getBoundingClientRect().height,
      };
    },
    [PAYEE_FAILED, PAYEE_LINKED]
  );
  assert.ok(measure.scrollWidth <= measure.clientWidth, `Statuszelle läuft nicht über (${measure.scrollWidth} gegen ${measure.clientWidth})`);
  assert.equal(measure.failedHeight, measure.linkedHeight, "die Zeile wird nicht höher");
});

test("PC: eine gelöschte Buchung bleibt gelöscht (Symbol), auch ohne Verknüpfung", async () => {
  const cell = statusCell(page, PAYEE_DELETED);
  assert.doesNotMatch(await cell.textContent(), /Nicht in YNAB/);
  assert.equal(await cell.locator('[aria-label="in YNAB gelöscht"]').count(), 1);
});

test("PC: ein Eintrag Zu prüfen ohne Verknüpfungsproblem zeigt weiter zu prüfen", async () => {
  assert.match(await statusCell(page, PAYEE_REVIEW).textContent(), /zu prüfen/);
});

test("Handy: die Zeile trägt das Kennzeichen Nicht in YNAB nur bei der nicht verknüpften Buchung", async () => {
  await mobilePage.getByText(PAYEE_FAILED).first().waitFor();
  const badges = mobilePage.getByText("Nicht in YNAB", { exact: true });
  assert.equal(await badges.count(), 1, "ein Kennzeichen, nur an der nicht verknüpften Buchung");
  // Das Kennzeichen sitzt in der Zeile der gescheiterten Buchung: der engste Vorfahr, der
  // einen Payee enthält, nennt genau diesen.
  const owner = await badges.first().evaluate((el) => {
    let node = el.parentElement;
    while (node && !node.textContent.includes("Kiosk cks06")) node = node.parentElement;
    return node ? node.textContent : "";
  });
  assert.match(owner, new RegExp(PAYEE_FAILED));
  assert.doesNotMatch(owner, new RegExp(PAYEE_LINKED));
});

test("PC: das Detail bietet Erneut senden, der Klick ruft retryPushToYnab und die Leiste verschwindet", async () => {
  await row(page, PAYEE_FAILED).locator("td").nth(2).click();
  const notice = page.locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  assert.match(await notice.textContent(), /Push gescheitert am .*Netzwerkfehler: Timeout/);
  const button = notice.locator("button", { hasText: "Erneut senden" });
  assert.equal(await button.count(), 1);
  linkedNow.add(1);
  await button.click();
  await page.locator('[data-testid="detail-notice"]').waitFor({ state: "detached" });
  assert.equal(retryCalls.length, 1, "genau ein Aufruf");
  assert.match(JSON.stringify(retryCalls[0]), /03e9/, "mit der Buchungs-Id");
  assert.doesNotMatch(await statusCell(page, PAYEE_FAILED).textContent(), /Nicht in YNAB/, "Kennzeichen weg");
});

test("PC: scheitert der Push erneut, zeigt die Leiste den neuen Grund", async () => {
  linkedNow.clear();
  retryAnswer = { Ok: { PushToYnabFailed: "YNAB bremst gerade (HTTP 429)" } };
  const fresh = await open(await browser.newContext({ viewport: { width: 1440, height: 900 } }));
  await row(fresh, PAYEE_FAILED).locator("td").nth(2).click();
  const notice = fresh.locator('[data-testid="detail-notice"]');
  await notice.waitFor();
  await notice.locator("button", { hasText: "Erneut senden" }).click();
  await notice.filter({ hasText: "YNAB bremst gerade (HTTP 429)" }).waitFor();
  assert.match(await statusCell(fresh, PAYEE_FAILED).textContent(), /Nicht in YNAB/, "weiter nicht verknüpft");
});
