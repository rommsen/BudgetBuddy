// scripts/ledger-background-refresh.test.mjs
//
// Gerenderter Beleg für ledger-ct3cx (Kassenbuch behält seinen Stand): endet ein Spiegel-Lauf, lädt die
// Liste im Hintergrund nach. Vorher baute sie neu auf (Skeleton, Sprung nach oben). Der Browser belegt nur,
// was .NET nicht kann: die gescrollte Zeile bleibt an ihrer Stelle im Viewport, und kein Skeleton erscheint.
// Auswahl und geänderter Betrag sind über die Expecto-Tests in `src/Tests/LedgerBackgroundRefreshTests.fs`
// belegt und dienen hier nur als Wartebedingung. Ein echter, per Vite gestarteter Fable-Client gegen
// gemockte Ledger-Antworten (erfundene Werte).
//
// Ausführen: node --test scripts/ledger-background-refresh.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildEntryFixture } from "./ledger-status-column.mjs";

const TOTAL = 150;
const payeeOf = (n) => `Scrollzeile ${String(n).padStart(3, "0")}`;
const CHANGED = "Scrollzeile 121 geändert";

let frontend;
let frontendPort;
let browser;

/// Öffnet das Kassenbuch. Der letzte Spiegel-Lauf ist zehn Minuten alt, also startet die Seite sofort einen
/// Lauf; dessen Antwort hält der Test zurück, bis `releaseMirror` sie freigibt. Danach meldet der Server
/// Zeile 121 geändert.
async function openLedger(viewport, hasTouch) {
  const context = await browser.newContext({ viewport, hasTouch });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accounts = buildAccountsFixture(2);
  const balances = buildBalancesFixture(accounts.Accounts);
  let mirrored = false;
  let releaseMirror;
  const mirrorHeld = new Promise((resolve) => (releaseMirror = resolve));
  const rows = () =>
    Array.from({ length: TOTAL }, (_, i) =>
      buildEntryFixture(accounts.DefaultAccountId, i + 1, {
        Payee: mirrored && i + 1 === 121 ? CHANGED : payeeOf(i + 1),
        Date: "2026-03-10T00:00:00.0000000",
        Amount: { Amount: mirrored && i + 1 === 121 ? -99 : -10, Currency: "EUR" },
      })
    );

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await page.route("**/api/LedgerApi/getLedgerAccounts", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(accounts) })
  );
  await page.route("**/api/LedgerApi/getLedgerAccountBalances", (route) =>
    route.fulfill({ contentType: "application/json", body: JSON.stringify(balances) })
  );
  await page.route("**/api/LedgerApi/getLedgerEntriesPage", (route) => {
    const query = JSON.parse(route.request().postData())[0];
    const offset = Math.max(0, query.Page * query.PageSize - query.LocallyRemoved);
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        Entries: rows().slice(offset, offset + query.PageSize),
        TotalCount: TOTAL,
        TotalAmount: { Amount: -10 * TOTAL, Currency: "EUR" },
        DeletedCount: 0,
        NeedsReviewCount: 0,
        AskLenaCount: 0, WithoutCategoryCount: 0,
      }),
    });
  });
  await page.route("**/api/LedgerApi/getLastMirrorRun", (route) =>
    route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({
        RanAt: new Date(Date.now() - (mirrored ? 0 : 10 * 60 * 1000)).toISOString(),
        Outcome: { MirrorRunSucceeded: { NewEntries: 0, ChangedEntries: 0, DeletedEntries: 0, ServerKnowledge: 1 } },
      }),
    })
  );
  await page.route("**/api/LedgerApi/mirrorYnabBudget", async (route) => {
    await mirrorHeld;
    mirrored = true;
    await route.fulfill({
      contentType: "application/json",
      body: JSON.stringify({ Ok: { NewEntries: 0, ChangedEntries: 1, DeletedEntries: 0, ServerKnowledge: 2 } }),
    });
  });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  await rowText(page, 1).waitFor();
  return { page, context, releaseMirror };
}

const rowText = (page, n) => page.getByText(payeeOf(n), { exact: true }).filter({ visible: true }).first();
const visibleBar = (page) => page.locator(".selection-bar").filter({ visible: true });

/// Wartet, bis die Position eines Elements 300 ms lang stehen bleibt (Scrollen kann animiert sein).
async function settledTop(locator) {
  let last = null;
  let since = Date.now();
  for (;;) {
    const box = await locator.boundingBox();
    const top = box ? Math.round(box.y) : null;
    if (top !== last) {
      last = top;
      since = Date.now();
    } else if (Date.now() - since >= 300) {
      return top;
    }
    await new Promise((resolve) => setTimeout(resolve, 30));
  }
}

/// Langes Drücken mit Touch-Zeigerereignissen (wie in ledger-selection.test.mjs).
async function longPress(locator) {
  const box = await locator.boundingBox();
  const init = {
    pointerType: "touch",
    pointerId: 7,
    isPrimary: true,
    bubbles: true,
    clientX: box.x + box.width / 2,
    clientY: box.y + box.height / 2,
  };
  await locator.dispatchEvent("pointerdown", init);
  await visibleBar(locator.page()).waitFor();
  await locator.dispatchEvent("pointerup", init);
  await locator.dispatchEvent("click", { bubbles: true, clientX: init.clientX, clientY: init.clientY });
}

async function scrollToRow120AndSelect(page, mobile) {
  // Bis ans Ende der ersten Seite scrollen, damit der Beobachter die zweite Seite nachlädt.
  await rowText(page, 100).scrollIntoViewIfNeeded();
  await rowText(page, 130).waitFor();
  const row = rowText(page, 120);
  await row.evaluate((el) => el.scrollIntoView({ block: "center" }));
  if (mobile) {
    await longPress(page.locator("button", { hasText: payeeOf(120) }).filter({ visible: true }).first());
  } else {
    await page.getByRole("checkbox", { name: `${payeeOf(120)} auswählen` }).filter({ visible: true }).click();
  }
  await visibleBar(page).waitFor();
  return row;
}

async function expectStandHeld(page, mobile, releaseMirror) {
  const row = await scrollToRow120AndSelect(page, mobile);
  const topBefore = await settledTop(row);
  const node = await row.elementHandle();
  // Merkt sich, ob nach dem Lauf irgendwo ein Lade-Skeleton erscheint.
  await page.evaluate(() => {
    window.__skeletonSeen = false;
    new MutationObserver(() => {
      if (document.querySelector('[aria-label="Lade Transaktionen"], .animate-pulse')) window.__skeletonSeen = true;
    }).observe(document.body, { childList: true, subtree: true });
  });
  releaseMirror();
  await page.getByText(CHANGED, { exact: true }).filter({ visible: true }).first().waitFor();
  await visibleBar(page).waitFor();
  assert.equal(await node.evaluate((el) => el.isConnected), true, "die Zeile wurde nicht neu aufgebaut");
  assert.equal(await page.evaluate(() => window.__skeletonSeen), false, "kein Skeleton erschienen");
  const topAfter = await settledTop(row);
  assert.ok(Math.abs(topAfter - topBefore) <= 1, `Zeile 120 steht an derselben Stelle (${topBefore} → ${topAfter})`);
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

test("[scroll] Handy 412x915: nach dem Spiegel-Lauf bleibt die gescrollte Zeile an ihrer Stelle, ohne Skeleton", async () => {
  const { page, context, releaseMirror } = await openLedger({ width: 412, height: 915 }, true);
  try {
    await expectStandHeld(page, true, releaseMirror);
  } finally {
    await context.close();
  }
});

test("[scroll] PC 1440x900: nach dem Spiegel-Lauf bleibt die gescrollte Zeile an ihrer Stelle, ohne Skeleton", async () => {
  const { page, context, releaseMirror } = await openLedger({ width: 1440, height: 900 }, false);
  try {
    await expectStandHeld(page, false, releaseMirror);
  } finally {
    await context.close();
  }
});
