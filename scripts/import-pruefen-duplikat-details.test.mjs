// scripts/import-pruefen-duplikat-details.test.mjs
//
// Gerenderter Beleg für `ynab-sync-e4gm3`: Import prüfen zeigt die Duplikat-Prüfung
// aufgeklappt sofort, wenn ein Verdacht besteht, und sonst erst nach Klick auf
// "Details". Echter Vite-Client gegen gemockte Sync-API-Antworten (erfundene Werte).
//
// Ausführen: node --test scripts/import-pruefen-duplikat-details.test.mjs
// Mit SHOTS_DIR=<Ordner> schreibt der Lauf zusätzlich die Nachher-Bilder dorthin.

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import fs from "node:fs";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const g = (n) => `00000000-0000-4000-8000-${n.toString(16).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const day = (d) => {
  const x = new Date();
  x.setDate(x.getDate() - d);
  return x.toISOString().slice(0, 10) + "T08:00:00.0000000";
};
const det = { TransactionReference: "", ReferenceFoundInYnab: false, ImportIdFoundInYnab: false, FuzzyMatchDate: null, FuzzyMatchAmount: null, FuzzyMatchPayee: null };
const fuzzy = { ...det, TransactionReference: "ref2", FuzzyMatchDate: "2026-10-07T00:00:00.0000000", FuzzyMatchAmount: -12.34, FuzzyMatchPayee: "Rewe" };
const cats = [["Alltag", "Lebensmittel"], ["Fix", "Miete"]].map(([GroupName, Name], i) => ({
  Id: W("YnabCategoryId", g(100 + i)), Name, GroupName, Available: { Amount: 100, Currency: "EUR" }, Hidden: false,
}));
const tx = (n, d, amount, payee, status, cat, dup, ynab = "NotAttempted") => ({
  Transaction: { Id: W("TransactionId", `t${n}`), BookingDate: day(d), Amount: { Amount: amount, Currency: "EUR" }, Payee: payee, Memo: "Kartenzahlung", Reference: `ref${n}`, RawData: "" },
  Status: status, CategoryId: cat === null ? null : W("YnabCategoryId", g(100 + cat)), CategoryName: cat === null ? null : cats[cat].Name,
  MatchedRuleId: null, PayeeOverride: null, ExternalLinks: [], UserNotes: null, DuplicateStatus: dup ?? { NotDuplicate: det },
  YnabImportStatus: ynab, Splits: null, SuggestedByOrderId: null, ReviewSteps: [], AskLena: false,
});
const txs = [
  tx(1, 0, -48.12, "Unauffällig GmbH", "AutoCategorized", 0),
  tx(2, 0, -12.34, "Verdacht Markt", "Pending", null, { PossibleDuplicate: ["Similar transaction found: Rewe on 2026-10-07 for -12.34", fuzzy] }),
];
const session = { Id: W("SyncSessionId", g(1)), StartedAt: day(0), CompletedAt: null, Status: "ReviewingTransactions", TransactionCount: 2, ImportedCount: 0, SkippedCount: 0 };
const settings = { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } };
const json = (b) => ({ contentType: "application/json", body: JSON.stringify(b) });

async function mock(page) {
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, body: "x" }));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => r.fulfill(json(session)));
  await page.route("**/api/SyncApi/getTransactions", (r) => r.fulfill(json({ Ok: txs })));
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/YnabApi/getCategories", (r) => r.fulfill(json({ Ok: cats })));
  await page.route("**/api/YnabApi/getPayees", (r) => r.fulfill(json({ Ok: [] })));
  await page.route("**/api/LedgerApi/getBudgetAccounts", (r) => r.fulfill(json({ Ok: [] })));
}

let frontend, port, browser;
before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port, undefined);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

const viewports = [
  ["pc-1440x900", { width: 1440, height: 900 }],
  ["handy-390x844", { width: 390, height: 844 }],
];

for (const [name, vp] of viewports) {
  test(`Duplikat-Details: Verdacht sofort, sonst hinter "Details" (${name})`, async () => {
    const ctx = await browser.newContext({ viewport: vp, deviceScaleFactor: 1 });
    const page = await ctx.newPage();
    page.setDefaultTimeout(15000);
    await mock(page);
    await page.goto(`http://localhost:${port}/#/`);
    await page.waitForSelector(".tx-row");

    const rowOf = (payee) => page.locator(".tx-row", { hasText: payee });

    // Verdachtszeile: aufgeklappt steht die Prüfung ohne weiteren Klick da.
    await rowOf("Verdacht Markt").click();
    const verdacht = page.locator(".tx-row.expanded", { hasText: "Verdacht Markt" });
    await verdacht.getByText("Duplikat-Prüfung").waitFor();
    assert.equal(await verdacht.getByRole("button", { name: "Details" }).count(), 0);
    assert.match(await verdacht.innerText(), /Gefunden: Rewe am 07\.10\.2026 über 12,34 €/);
    assert.doesNotMatch(await verdacht.innerText(), /Similar transaction/);

    // Unauffällige Zeile: "Details", die Prüfung erst nach Klick.
    await rowOf("Unauffällig GmbH").click();
    const ruhig = page.locator(".tx-row.expanded", { hasText: "Unauffällig GmbH" });
    await ruhig.getByRole("button", { name: "Details" }).waitFor();
    assert.equal(await ruhig.getByText("Duplikat-Prüfung").count(), 0);
    await ruhig.getByRole("button", { name: "Details" }).click();
    await ruhig.getByText("Duplikat-Prüfung").waitFor();
    assert.match(await ruhig.innerText(), /Nicht in YNAB/);

    if (process.env.SHOTS_DIR) {
      fs.mkdirSync(process.env.SHOTS_DIR, { recursive: true });
      await page.waitForTimeout(500);
      await page.screenshot({ path: path.join(process.env.SHOTS_DIR, `${name}-import-pruefen-duplikat-details.png`) });
      // Zweites Bild: die Verdachtszeile mit Hinweis-Zeile und sofort sichtbarer Prüfung im Blick.
      await verdacht.scrollIntoViewIfNeeded();
      await page.evaluate(() => {
        const row = [...document.querySelectorAll(".tx-row.expanded")].find((r) => r.innerText.includes("Verdacht Markt"));
        row.scrollIntoView({ block: "start" });
        window.scrollBy(0, -80);
      });
      await page.waitForTimeout(400);
      await page.screenshot({ path: path.join(process.env.SHOTS_DIR, `${name}-import-pruefen-duplikat-verdacht.png`) });
    }
    await ctx.close();
  });
}
