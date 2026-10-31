// scripts/sync-start.test.mjs
//
// Gerenderter Beleg für `banking-import-cyg94`: der Server entscheidet über die TAN. Echter
// Vite-Client gegen gemockte Sync-API-Antworten (erfundene Werte, nie die echte Datenbank, nie
// Comdirect oder YNAB). Pfad ohne TAN: `startSync` antwortet `ReviewReady`, die Prüfung erscheint
// direkt, `confirmTan` geht nie hinaus. Pfad mit TAN: `TanRequired` zeigt den TAN-Schritt, erst
// "Ich habe bestätigt" führt in die Prüfung. `initiateComdirectAuth` gibt es nicht mehr: der
// Catch-all antwortet darauf mit 500, und der Test merkt sich jeden solchen Aufruf.
//
// Ausführen: node --test scripts/sync-start.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";

const g = (n) => `00000000-0000-4000-8000-${n.toString(16).padStart(12, "0")}`;
const W = (c, v) => ({ [c]: v });
const day = () => new Date().toISOString().slice(0, 10) + "T08:00:00.0000000";
const det = { TransactionReference: "", ReferenceFoundInYnab: false, ImportIdFoundInYnab: false, FuzzyMatchDate: null, FuzzyMatchAmount: null, FuzzyMatchPayee: null };
const row = {
  Transaction: { Id: W("TransactionId", "cyg94-1"), BookingDate: day(), Amount: { Amount: -12.34, Currency: "EUR" }, Payee: "Erfundener Markt", Memo: "Kartenzahlung", Reference: "ref1", RawData: "" },
  Status: "Pending", CategoryId: null, CategoryName: null, MatchedRuleId: null, PayeeOverride: null, ExternalLinks: [], UserNotes: null, DuplicateStatus: { NotDuplicate: det },
  YnabImportStatus: "NotAttempted", Splits: null, SuggestedByOrderId: null, ReviewSteps: [], AskLena: false,
};
const session = (Status) => ({ Id: W("SyncSessionId", g(1)), StartedAt: day(), CompletedAt: null, Status, TransactionCount: 1, ImportedCount: 0, SkippedCount: 0 });
const json = (b) => ({ contentType: "application/json", body: JSON.stringify(b) });

let frontend;
let port;
let browser;

before(async () => {
  port = await getFreePort();
  frontend = await startFrontend(port);
  browser = await chromium.launch();
});
after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, port);
});

async function open(startAnswer, lost) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  const page = await ctx.newPage();
  page.setDefaultTimeout(15000);
  const calls = { start: 0, confirm: 0, unknown: [] };
  let current = null; // die Sitzung, die der Server gerade kennt
  await page.route("**/api/**", (r) => {
    calls.unknown.push(new URL(r.request().url()).pathname);
    return r.fulfill({ status: 500, body: "x" });
  });
  await page.route("**/api/SyncApi/getCurrentSession", (r) => {
    // Nach dem Start antwortet der Server der Reihe nach aus `lost.reads`.
    if (calls.start > 0 && lost?.reads?.length) {
      const next = lost.reads.shift();
      if (next === "abort") return r.abort("connectionreset");
      current = next;
    }
    return r.fulfill(json(current));
  });
  await page.route("**/api/SyncApi/getTransactions", (r) => r.fulfill(json({ Ok: [row] })));
  await page.route("**/api/SyncApi/startSync", (r) => {
    calls.start += 1;
    // Die Antwort geht verloren, der Server hat den Start aber verarbeitet (Live-Befund "Http error (0)").
    if (lost) {
      current = lost.server;
      return r.abort("connectionreset");
    }
    const [kind, status] = startAnswer;
    current = session(status);
    return r.fulfill(json({ Ok: { [kind]: current } }));
  });
  await page.route("**/api/SyncApi/confirmTan", (r) => {
    calls.confirm += 1;
    current = session("ReviewingTransactions");
    return r.fulfill(json({ Ok: [] }));
  });
  await page.goto(`http://localhost:${port}/#/`);
  await page.getByRole("button", { name: "Sync starten" }).click();
  return { ctx, page, calls };
}

test("aktive Bankverbindung: die Prüfung erscheint ohne TAN-Schritt", async () => {
  const { ctx, page, calls } = await open(["ReviewReady", "ReviewingTransactions"]);
  await page.locator(".tx-row", { hasText: "Erfundener Markt" }).waitFor();
  assert.equal(await page.getByRole("button", { name: "Ich habe bestätigt" }).count(), 0, "kein TAN-Schritt");
  assert.equal(calls.start, 1, "ein Start");
  assert.equal(calls.confirm, 0, "keine TAN-Bestätigung");
  assert.ok(!calls.unknown.some((p) => p.includes("initiateComdirectAuth")), "kein alter Auth-Aufruf");
  await ctx.close();
});

test("ohne Bankverbindung: erst der TAN-Schritt, dann die Prüfung", async () => {
  const { ctx, page, calls } = await open(["TanRequired", "AwaitingTan"]);
  const confirm = page.getByRole("button", { name: "Ich habe bestätigt" });
  await confirm.waitFor();
  assert.equal(await page.locator(".tx-row").count(), 0, "noch keine Buchungen");
  assert.equal(calls.confirm, 0, "wartet auf Roman");
  await confirm.click();
  await page.locator(".tx-row", { hasText: "Erfundener Markt" }).waitFor();
  assert.equal(calls.confirm, 1, "eine Bestätigung");
  assert.ok(!calls.unknown.some((p) => p.includes("initiateComdirectAuth")), "kein alter Auth-Aufruf");
  await ctx.close();
});

test("verlorene Antwort, der Server wartet auf die TAN: der TAN-Schritt erscheint ohne zweiten Start", async () => {
  const { ctx, page, calls } = await open(null, { server: session("AwaitingTan") });
  await page.getByRole("button", { name: "Ich habe bestätigt" }).waitFor();
  assert.equal(calls.start, 1, "kein zweiter Start");
  assert.equal(await page.getByText("Synchronisierung fehlgeschlagen").count(), 0, "kein Fehler");
  await ctx.close();
});

test("verlorene Antwort, der Server hat keine Sitzung: der Fehler bleibt sichtbar", async () => {
  const { ctx, page, calls } = await open(null, { server: null });
  await page.getByText("Synchronisierung fehlgeschlagen").waitFor();
  assert.equal(calls.start, 1, "kein automatischer Neustart");
  await ctx.close();
});

test("verlorene Antwort, der Server ruft noch ab: die Prüfung erscheint ohne zweiten Start", async () => {
  const fetching = session("FetchingTransactions");
  const { ctx, page, calls } = await open(null, { server: fetching, reads: [fetching, session("ReviewingTransactions")] });
  await page.locator(".tx-row", { hasText: "Erfundener Markt" }).waitFor();
  assert.equal(calls.start, 1, "nur gelesen, kein zweiter Start");
  await ctx.close();
});

test("verlorene Antwort, das erste Neulesen bricht ab: der nächste Versuch findet den TAN-Schritt", async () => {
  const { ctx, page, calls } = await open(null, { server: session("AwaitingTan"), reads: ["abort", session("AwaitingTan")] });
  await page.getByRole("button", { name: "Ich habe bestätigt" }).waitFor();
  assert.equal(calls.start, 1, "kein zweiter Start");
  assert.equal(await page.getByText("Synchronisierung fehlgeschlagen").count(), 0, "kein Fehler");
  await ctx.close();
});
