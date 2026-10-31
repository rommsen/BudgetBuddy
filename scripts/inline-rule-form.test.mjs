// scripts/inline-rule-form.test.mjs
//
// Wächter für `categorization-f955d`: das Regelformular an der Importzeile. Echter Client gegen
// gemockte API (erfundene Werte). Die Reihenfolge später Antworten prüft der Reduzierer in
// `src/Tests/SyncFlowStateTests.fs`. Hier steht, was nur der Browser zeigt: der Timer feuert
// wirklich einmal je Tippfolge, und jede Rückmeldung kommt in der Ansicht an.
//
// Ausführen: npm run test:scripts -- scripts/inline-rule-form.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const g = (n) => `00000000-0000-4000-8000-${n.toString(16).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const today = () => new Date().toISOString().slice(0, 10) + "T08:00:00.0000000";
const json = (body) => ({ contentType: "application/json", body: JSON.stringify(body) });

const category = { Id: W("YnabCategoryId", g(100)), Name: "Lebensmittel", GroupName: "Alltag", Available: null, Hidden: false };
const detection = { TransactionReference: "", ReferenceFoundInYnab: false, ImportIdFoundInYnab: false, FuzzyMatchDate: null, FuzzyMatchAmount: null, FuzzyMatchPayee: null };
const row = {
  Transaction: { Id: W("TransactionId", "f955d-1"), BookingDate: today(), Amount: { Amount: -12.34, Currency: "EUR" }, Payee: "Testmarkt", Memo: "Kartenzahlung", Reference: "ref1", RawData: "" },
  Status: "Pending", CategoryId: null, CategoryName: null,
  MatchedRuleId: null, PayeeOverride: null, ExternalLinks: [], UserNotes: null, DuplicateStatus: { NotDuplicate: detection },
  YnabImportStatus: "NotAttempted", Splits: null, SuggestedByOrderId: null, ReviewSteps: [], AskLena: false,
};
const session = { Id: W("SyncSessionId", g(1)), StartedAt: today(), CompletedAt: null, Status: "ReviewingTransactions", TransactionCount: 1, ImportedCount: 0, SkippedCount: 0 };
const settings = { Comdirect: null, Ynab: { HasToken: true, DefaultBudgetId: W("YnabBudgetId", "b1"), DefaultAccountId: null, QuickAddAccountId: null }, Sync: { DaysToFetch: 14 } };

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

/// `answer` entscheidet je Anfrage, was der Server auf testRule antwortet.
async function openForm(answer) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  const patterns = [];
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, body: "x" }));
  await page.route("**/api/SyncApi/getCurrentSession", (r) => r.fulfill(json(session)));
  await page.route("**/api/SyncApi/getTransactions", (r) => r.fulfill(json({ Ok: [row] })));
  await page.route("**/api/SettingsApi/getSettings", (r) => r.fulfill(json(settings)));
  await page.route("**/api/SettingsApi/getLenaFlagSettings", (r) => r.fulfill(json({ SavedColor: null, SuggestedColor: null })));
  await page.route("**/api/YnabApi/getCategories", (r) => r.fulfill(json({ Ok: [category] })));
  await page.route("**/api/YnabApi/getPayees", (r) => r.fulfill(json({ Ok: [] })));
  await page.route("**/api/SyncApi/categorizeTransaction", (r) =>
    r.fulfill(json({ Ok: { ...row, Status: "ManualCategorized", CategoryId: category.Id, CategoryName: category.Name } }))
  );
  await page.route("**/api/RulesApi/testRule", (r) => {
    const [[pattern]] = r.request().postDataJSON();
    patterns.push(pattern);
    return answer(r, pattern);
  });
  await page.goto(`http://localhost:${port}/#/`);
  // Das Formular gibt es erst an einer Zeile, die Roman selbst kategorisiert hat.
  await page.locator(".tx-row", { hasText: "Testmarkt" }).click();
  await page.getByRole("button", { name: "Kategorie wählen" }).click();
  await page.locator(".bottom-sheet.active").getByText("Lebensmittel").click();
  // Das Sheet schluckt nach dem Schließen kurz den nächsten Klick (ADR 0005): erneut tippen, bis
  // das Formular steht.
  const form = page.locator(".pattern-input");
  for (let i = 0; i < 20 && !(await form.isVisible()); i += 1) {
    await page.locator(".tx-create-rule-btn").click();
    await form.waitFor({ timeout: 200 }).catch(() => {});
  }
  await form.waitFor();
  return { context, page, patterns };
}

const matches = (r) => r.fulfill(json({ Ok: true }));

test("Öffnen prüft das vorbelegte Muster, eine Tippfolge löst genau eine Anfrage aus", async () => {
  const { context, page, patterns } = await openForm(matches);
  try {
    await page.getByText("Passt auf diese Transaktion").waitFor();
    assert.deepEqual(patterns, ["Testmarkt"]);

    await page.locator(".pattern-input").fill("");
    await page.locator(".pattern-input").pressSequentially("rewe", { delay: 20 });
    await page.getByText("Passt auf diese Transaktion").waitFor();
    assert.deepEqual(patterns, ["Testmarkt", "rewe"]);
  } finally {
    await context.close();
  }
});

test("Kein Treffer zeigt passt nicht", async () => {
  const { context, page } = await openForm((r) => r.fulfill(json({ Ok: false })));
  try {
    await page.getByText("Passt nicht auf diese Transaktion").waitFor();
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: ein Netzfehler erschien als "passt nicht".
test("Netzfehler zeigt eine eigene Meldung und nicht passt nicht", async () => {
  const { context, page } = await openForm((r) => r.abort());
  try {
    await page.getByText("Muster konnte nicht geprüft werden").waitFor();
    assert.equal(await page.getByText("Passt nicht auf diese Transaktion").count(), 0);
  } finally {
    await context.close();
  }
});

// ynab-sync-k3vq8: der Mustertest liest nur, ein einzelner Abbruch heilt sich wie jeder Lesevorgang im Import.
test("Ein einzelner Abbruch des Mustertests wird wiederholt", async () => {
  let attempts = 0;
  const { context, page } = await openForm((r) => {
    attempts += 1;
    return attempts === 1 ? r.abort("connectionreset") : r.fulfill(json({ Ok: true }));
  });
  try {
    await page.getByText("Passt auf diese Transaktion").waitFor();
    assert.equal(await page.getByText("Muster konnte nicht geprüft werden").count(), 0);
  } finally {
    await context.close();
  }
});

// Bug categorization-f955d: ein ungültiges Muster zeigte gar keine Rückmeldung.
test("Ungültiges Muster zeigt eine eigene Meldung und nicht passt nicht", async () => {
  const { context, page } = await openForm((r) => r.fulfill(json({ Error: { InvalidPattern: ["(", "Klammer fehlt"] } })));
  try {
    await page.getByText("Ungültiges Muster").waitFor();
    assert.equal(await page.getByText("Passt nicht auf diese Transaktion").count(), 0);
  } finally {
    await context.close();
  }
});
