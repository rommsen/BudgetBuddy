// scripts/import-lena.test.mjs
//
// Gerenderter Beleg für `ynab-sync-d9e51`: "Lena fragen" an der einzelnen Importzeile in
// Import prüfen. Echter Vite-Client gegen gemockte Sync-API-Antworten (erfundene Werte,
// nie die echte Entwicklungsdatenbank, nie YNAB). Geprüft wird am echten, von der App
// gerenderten Element: die Aktion in der aufgeklappten Zeile, das Kennzeichen "Rückfrage
// Lena" nur an der gewählten Zeile, "Rückfrage zurücknehmen", die gesperrte Aktion ohne
// Flag-Farbe samt Grund, und dass der Befehl `setAskLena` nur für die gewählte Zeile
// abgeschickt wird. Dass der Create-Body für genau diese Zeile `flag_color` trägt, belegen
// die .NET-Tests (`ImportAskLenaTests.fs`): der Push läuft serverseitig und ist vom Browser
// aus nicht abzufangen.
//
// Ausführen: node --test scripts/import-lena.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
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
const cats = [["Alltag", "Lebensmittel"]].map(([GroupName, Name], i) => ({
  Id: W("YnabCategoryId", g(100 + i)), Name, GroupName, Available: { Amount: 100, Currency: "EUR" }, Hidden: false,
}));
const tx = (n, amount, payee, status, cat, ask) => ({
  Transaction: { Id: W("TransactionId", `d9e51-${n}`), BookingDate: day(0), Amount: { Amount: amount, Currency: "EUR" }, Payee: payee, Memo: "Kartenzahlung", Reference: `ref${n}`, RawData: "" },
  Status: status, CategoryId: cat === null ? null : W("YnabCategoryId", g(100 + cat)), CategoryName: cat === null ? null : cats[cat].Name,
  MatchedRuleId: null, PayeeOverride: null, ExternalLinks: [], UserNotes: null, DuplicateStatus: { NotDuplicate: det },
  YnabImportStatus: "NotAttempted", Splits: null, SuggestedByOrderId: null, ReviewSteps: [], AskLena: ask,
});
const session = { Id: W("SyncSessionId", g(1)), StartedAt: day(0), CompletedAt: null, Status: "ReviewingTransactions", TransactionCount: 2, ImportedCount: 0, SkippedCount: 0 };
const settings = { Comdirect: null, Ynab: { PersonalAccessToken: "x", DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } };
const json = (b) => ({ contentType: "application/json", body: JSON.stringify(b) });

/// Veränderlicher Zustand der Mocks je Test.
let state;
const reset = (lenaColor) => {
  state = {
    lenaColor,
    rows: [tx(1, -48.12, "Kategorisiert GmbH", "AutoCategorized", 0, false), tx(2, -12.34, "Ohne Kategorie Markt", "Pending", null, false)],
    setCalls: [],
    importCalls: 0,
  };
};

async function mock(page) {
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, body: "x" }));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => r.fulfill(json(session)));
  await page.route("**/api/SyncApi/getTransactions", (r) => r.fulfill(json({ Ok: state.rows })));
  await page.route("**/api/SyncApi/setAskLena", (r) => {
    // Fable.Remoting: ein Tupel-Argument kommt als Array im Array.
    const [[, txId, ask]] = r.request().postDataJSON();
    const id = txId.TransactionId ?? txId;
    state.setCalls.push({ txId: id, ask });
    state.rows = state.rows.map((row) => (row.Transaction.Id.TransactionId === id ? { ...row, AskLena: ask } : row));
    r.fulfill(json({ Ok: state.rows.find((row) => row.Transaction.Id.TransactionId === id) }));
  });
  await page.route("**/api/SyncApi/importToYnab", (r) => {
    state.importCalls += 1;
    r.fulfill(json({ Ok: { CreatedCount: 2, DuplicateTransactionIds: [], LenaFlagNotSentCount: 0 } }));
  });
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: state.lenaColor, SuggestedColor: null })));
  await page.route("**/api/YnabApi/getCategories", (r) => r.fulfill(json({ Ok: cats })));
  await page.route("**/api/YnabApi/getPayees", (r) => r.fulfill(json({ Ok: [] })));
  await page.route("**/api/YnabApi/getBudgetDetails", (r) => r.fulfill({ status: 500, body: "x" }));
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

async function open(vp) {
  const ctx = await browser.newContext({ viewport: vp, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.setDefaultTimeout(15000);
  await mock(page);
  await page.goto(`http://localhost:${port}/#/`);
  await page.waitForSelector(".tx-row");
  return { ctx, page };
}

for (const [name, vp] of viewports) {
  test(`Lena fragen an der Importzeile: Kennzeichen nur an der gewählten Zeile (${name})`, async () => {
    reset("purple");
    const { ctx, page } = await open(vp);
    const row = (payee) => page.locator(".tx-row", { hasText: payee });

    assert.equal(await page.getByText("Rückfrage Lena").count(), 0, "ohne Rückfrage kein Kennzeichen");

    // Zeile ohne Kategorie aufklappen und fragen.
    await row("Ohne Kategorie Markt").click();
    const open2 = page.locator(".tx-row.expanded", { hasText: "Ohne Kategorie Markt" });
    await open2.getByRole("button", { name: "Lena fragen" }).click();
    await open2.getByText("Rückfrage Lena").first().waitFor();

    assert.deepEqual(state.setCalls, [{ txId: "d9e51-2", ask: true }], "der Befehl geht nur für diese Zeile raus");
    assert.equal(await row("Kategorisiert GmbH").getByText("Rückfrage Lena").count(), 0, "andere Zeile ohne Kennzeichen");
    assert.equal(await open2.getByRole("button", { name: "Rückfrage zurücknehmen" }).count(), 1, "Aktion wechselt");

    // Zurücknehmen.
    await open2.getByRole("button", { name: "Rückfrage zurücknehmen" }).click();
    await page.waitForFunction(() => ![...document.querySelectorAll(".tx-row")].some((r) => r.innerText.includes("Rückfrage Lena")));
    assert.deepEqual(state.setCalls.at(-1), { txId: "d9e51-2", ask: false });

    // Wieder fragen und importieren: der Import wird angestoßen, die Rückfrage bleibt an der Zeile.
    await open2.getByRole("button", { name: "Lena fragen" }).click();
    await open2.getByText("Rückfrage Lena").first().waitFor();
    await page.locator(".action-bar-buttons").getByRole("button", { name: "Importieren" }).click();
    await page.waitForTimeout(300);
    assert.equal(state.importCalls, 1, "Import angestoßen");

    // Die Zeile bleibt eine einzeilige Kopfzeile: das Kennzeichen sprengt die Zeile nicht.
    const overflow = await page.evaluate(() => {
      const r = [...document.querySelectorAll(".tx-row")].find((x) => x.innerText.includes("Ohne Kategorie Markt"));
      return r.scrollWidth - r.clientWidth;
    });
    assert.ok(overflow <= 0, `Zeile läuft waagerecht über: ${overflow}`);

    if (process.env.SHOTS_DIR) {
      await page.screenshot({ path: `${process.env.SHOTS_DIR}/${name}-import-lena.png` });
    }
    await ctx.close();
  });

  test(`Lena fragen ohne Flag-Farbe ist gesperrt und nennt den Grund (${name})`, async () => {
    reset(null);
    const { ctx, page } = await open(vp);
    await page.locator(".tx-row", { hasText: "Ohne Kategorie Markt" }).click();
    const open2 = page.locator(".tx-row.expanded", { hasText: "Ohne Kategorie Markt" });
    const button = open2.getByRole("button", { name: "Lena fragen" });
    await button.waitFor();
    assert.equal(await button.isDisabled(), true, "gesperrt");
    await open2.getByText("In den Settings die Flag-Farbe für Lena wählen").first().waitFor();
    assert.equal(state.setCalls.length, 0, "kein Befehl abgeschickt");
    if (process.env.SHOTS_DIR) {
      await page.waitForTimeout(600);
      await page.screenshot({ path: `${process.env.SHOTS_DIR}/${name}-import-lena-gesperrt.png` });
    }
    await ctx.close();
  });
}
