// scripts/ledger-mirror-buttons.test.mjs
//
// Regressionstest für `design-system-qz7b6`: im Kassenbuch-Kopf erschienen am Handy
// beide Spiegel-Knöpfe (Symbol und Text) nebeneinander. Ursache: `Button.view` setzte
// fest `inline-flex` in die Klassenliste, der Textknopf übergab `hidden md:inline-flex`.
// Tailwind 4 ordnet Utilities im erzeugten CSS, nicht nach der Reihenfolge im
// class-Attribut; `inline-flex` stand im CSS hinter `hidden` und gewann. Der Knopf
// mit `md:hidden` ging, weil eine Media-Regel später im CSS steht.
//
// Erwartung: bei 390 x 844 genau ein sichtbarer Spiegel-Knopf (Symbol), bei
// 1440 x 900 genau einer (Text). Echter Fable-Client gegen gemockte Ledger-Antworten.
//
// Ausführen: node --test scripts/ledger-mirror-buttons.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer, waitForPageLoaded } from "./screenshot-route.mjs";
import {
  buildAccountsFixture,
  buildBalancesFixture,
  buildEntriesFixture,
  mockLedgerApi,
} from "./ledger-status-column.mjs";

let frontend;
let frontendPort;
let browser;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

async function openLedger(viewport) {
  const context = await browser.newContext({ viewport });
  const page = await context.newPage();
  page.setDefaultTimeout(30000);
  const accountsFixture = buildAccountsFixture(5);
  const balancesFixture = buildBalancesFixture(accountsFixture.Accounts);
  const entriesFixture = buildEntriesFixture(accountsFixture.DefaultAccountId);
  await page.route("**/api/**", (route) =>
    route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"not mocked in this test"}' })
  );
  await mockLedgerApi(page, { accountsFixture, balancesFixture, entriesFixture });
  await page.goto(`http://127.0.0.1:${frontendPort}/#/ledger`, { waitUntil: "load" });
  await waitForPageLoaded(page, "/ledger");
  return { context, page };
}

/// Sichtbare Spiegel-Knöpfe: Titel "Jetzt spiegeln" oder Text "Jetzt spiegeln" bzw.
/// "Spiegel läuft…" (je nachdem, ob der gemockte Lauf schon als laufend gemeldet ist).
async function visibleMirrorButtons(page) {
  return page.evaluate(() =>
    [...document.querySelectorAll("button")]
      .filter((b) => b.title === "Jetzt spiegeln" || ["Jetzt spiegeln", "Spiegel läuft…"].includes(b.textContent.trim()))
      .filter((b) => {
        const r = b.getBoundingClientRect();
        return getComputedStyle(b).display !== "none" && r.width > 0 && r.height > 0;
      })
      .map((b) => ({ title: b.title, text: b.textContent.trim() }))
  );
}

test("Handy 390x844: genau ein Spiegel-Knopf, der Symbol-Knopf", async () => {
  const { context, page } = await openLedger({ width: 390, height: 844 });
  try {
    const visible = await visibleMirrorButtons(page);
    assert.equal(visible.length, 1, `erwartet genau einen sichtbaren Spiegel-Knopf, sichtbar: ${JSON.stringify(visible)}`);
    assert.equal(visible[0].title, "Jetzt spiegeln");
    assert.equal(visible[0].text, "");
  } finally {
    await context.close();
  }
});

test("PC 1440x900: genau ein Spiegel-Knopf, der Textknopf", async () => {
  const { context, page } = await openLedger({ width: 1440, height: 900 });
  try {
    const visible = await visibleMirrorButtons(page);
    assert.equal(visible.length, 1, `erwartet genau einen sichtbaren Spiegel-Knopf, sichtbar: ${JSON.stringify(visible)}`);
    assert.match(visible[0].text, /^(Jetzt spiegeln|Spiegel läuft…)$/);
  } finally {
    await context.close();
  }
});
