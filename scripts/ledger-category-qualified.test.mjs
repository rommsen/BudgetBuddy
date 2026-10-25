// scripts/ledger-category-qualified.test.mjs
//
// Gerenderte Belege für `ledger-h6ptn` ("Kassenbuch-Kategorie voll qualifizieren"):
// die PC-Tabelle zeigt "Gruppe: Name" (Handoff 1.9 Zeile 260), die Handy-Zeile
// bleibt beim Blattnamen (Handoff 1.1 Zeile 104-112). Muster übernommen aus
// `scripts/ledger-status-column.mjs`/`.test.mjs` (ledger-qns4j): ein echter, per
// Vite gestarteter Fable-Client (echtes Tailwind-CSS, echtes kompiliertes Bundle)
// gegen gemockte Ledger-API-Antworten, rein erfundene Werte, nie die echte
// Entwicklungsdatenbank, weder Original noch Kopie, kein Backend nötig.
//
// Ausführen: node --test scripts/ledger-category-qualified.test.mjs
// (Teil von `npm run test:scripts`, das alle scripts/*.test.mjs läuft.)
//
// Screenshot-Ziel: standardmäßig ein `mkdtemp`-Verzeichnis unter `os.tmpdir()`,
// das nach dem Lauf wieder gelöscht wird. `npm run test:scripts` darf den
// Arbeitsbaum nie verändern (die beiden committeten PNGs unter
// `docs/ux/kassenbuch/screenshots/` sind einmalig für die [human-eye]-Prüfung
// entstanden und werden von diesem Testlauf nicht mehr angefasst). Nur mit
// explizitem Opt-in `LEDGER_SCREENSHOTS_TO_DOCS=1` schreibt der Lauf wieder
// nach `docs/ux/kassenbuch/screenshots/`, um die committeten Bilder bei Bedarf
// bewusst neu zu erzeugen.

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import os from "node:os";
import path from "node:path";
import { mkdtemp, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded, captureAtDocumentHeight } from "./screenshot-route.mjs";
import { buildAccountsFixture, buildBalancesFixture, buildEntryFixture, mockLedgerApi } from "./ledger-status-column.mjs";

// Eigener ROOT statt Import aus screenshot-route.mjs (nicht exportiert dort, und
// diese Datei fasst screenshot-route.mjs bewusst nicht an: design-system-w4n9r
// arbeitet im selben Batch an genau dieser Datei).
const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const DOCS_SCREENSHOT_DIR = path.join(ROOT, "docs", "ux", "kassenbuch", "screenshots");
const WRITE_TO_DOCS = process.env.LEDGER_SCREENSHOTS_TO_DOCS === "1";

let SCREENSHOT_DIR = DOCS_SCREENSHOT_DIR;
let tempScreenshotDir;

// Zwei Buchungen, wie im Handoff: eine mit gespiegelter Gruppe (1.9 Zeile 260,
// "Alltag: Lebensmittel"), eine mit einer Kategorie, die (noch) keine Gruppe
// trägt (1.1 Zeile 104-112 zeigt "Bahn" als Blattname), genau der Rückfall, den
// `categoryLabelQualified` für `CategoryGroupName = None` vorsieht.
const PAYEE_QUALIFIED = "Fixture Kategorie Qualifiziert";
const PAYEE_NO_GROUP = "Fixture Kategorie Ohne Gruppe";

function buildEntriesFixture(accountId) {
  const entries = [
    buildEntryFixture(accountId, 1, {
      Payee: PAYEE_QUALIFIED,
      CategoryName: "Lebensmittel",
      CategoryGroupName: "Alltag",
    }),
    buildEntryFixture(accountId, 2, {
      Payee: PAYEE_NO_GROUP,
      CategoryName: "Bahn",
      CategoryGroupName: null,
    }),
  ];
  const totalAmount = entries.reduce((sum, e) => sum + e.Amount.Amount, 0);
  return {
    Entries: entries,
    TotalCount: entries.length,
    TotalAmount: { Amount: totalAmount, Currency: "EUR" },
    DeletedCount: 0,
    NeedsReviewCount: 0, AskLenaCount: 0,
  };
}

let frontend;
let frontendPort;
let browser;

before(async () => {
  if (WRITE_TO_DOCS) {
    SCREENSHOT_DIR = DOCS_SCREENSHOT_DIR;
  } else {
    tempScreenshotDir = await mkdtemp(path.join(os.tmpdir(), "bb-ledger-category-qualified-"));
    SCREENSHOT_DIR = tempScreenshotDir;
  }
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
  if (tempScreenshotDir) {
    await rm(tempScreenshotDir, { recursive: true, force: true });
  }
});

test("PC-Tabelle (1440x900): die Kategorie steht voll qualifiziert 'Alltag: Lebensmittel', der Blattname allein steht ohne Gruppe (AC, Handoff 1.9 Zeile 260)", async () => {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);

  const accountsFixture = buildAccountsFixture(1);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId);

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");

  const rowQualified = page.locator("table tbody tr", { hasText: PAYEE_QUALIFIED });
  const rowNoGroup = page.locator("table tbody tr", { hasText: PAYEE_NO_GROUP });

  await assert.doesNotReject(rowQualified.waitFor({ state: "visible" }));

  const qualifiedText = await rowQualified.innerText();
  assert.ok(
    qualifiedText.includes("Alltag: Lebensmittel"),
    `Die PC-Tabelle muss die volle Qualifizierung 'Alltag: Lebensmittel' zeigen, Zeilentext war: ${qualifiedText}`
  );

  const noGroupText = await rowNoGroup.innerText();
  assert.ok(
    noGroupText.includes("Bahn"),
    `Ohne gespiegelte Gruppe muss weiterhin der Blattname 'Bahn' stehen, Zeilentext war: ${noGroupText}`
  );
  assert.ok(
    !noGroupText.includes(": Bahn"),
    `Ohne gespiegelte Gruppe darf kein führendes 'Gruppe: ' erscheinen, Zeilentext war: ${noGroupText}`
  );

  await captureAtDocumentHeight(
    page,
    { width: 1440, height: 900 },
    path.join(SCREENSHOT_DIR, "pc-1440x900-kategorie-voll-qualifiziert.png")
  );

  await context.close();
});

test("Handy-Zeile (390x844): die Kategorie bleibt beim Blattnamen 'Lebensmittel', keine Qualifizierung (AC, Handoff 1.1 Zeile 104-112)", async () => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);

  const accountsFixture = buildAccountsFixture(1);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId);

  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });

  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");

  // Die Handy-Zeile ist kein `<table>`, sondern ein `Html.button` (`entryRow`,
  // View.fs); auffindbar über ihren Payee-Text.
  const rowQualified = page.locator("button", { hasText: PAYEE_QUALIFIED }).first();
  await assert.doesNotReject(rowQualified.waitFor({ state: "visible" }));

  const text = await rowQualified.innerText();
  assert.ok(text.includes("Lebensmittel"), `Die Handy-Zeile muss den Blattnamen 'Lebensmittel' zeigen, Zeilentext war: ${text}`);
  assert.ok(
    !text.includes("Alltag: Lebensmittel"),
    `Die Handy-Zeile darf NICHT voll qualifizieren (das ist der PC-Tabelle vorbehalten), Zeilentext war: ${text}`
  );

  await captureAtDocumentHeight(
    page,
    { width: 390, height: 844 },
    path.join(SCREENSHOT_DIR, "handy-390x844-kategorie-blattname.png")
  );

  await context.close();
});
