// scripts/sync-start.test.mjs
//
// „Abrufen“ (banking-import-d0fey, ADR 0093): ein Sheet über der Übersicht statt der Seite Sync. Echter Vite-Client
// gegen gemockte Antworten (erfundene Werte, nie die echte Datenbank, nie Comdirect oder YNAB).
//
// Hier bleiben der eine Happy Path am Handy (412 px, Touch: TAN bestätigen, Ergebnis lesen) und eine verlorene
// Antwort mit abgebrochenem Neulesen (Status 0 gibt es nur im Fable-Kompilat). Zustände und Reihenfolgen prüfen
// `FetchSheetTests.fs` und `SyncStartOrderTests.fs` unter .NET, das Layout der Sweep (Zustände `abrufen-tan` und
// `abrufen-fertig`), das Inventar der lesenden Aufrufe `RetryTests.fs`.
//
// Ausführen: node --test scripts/sync-start.test.mjs
// (Teil von `npm run test:scripts`.)

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { stateNamed, syncSession, FETCH_RESULT } from "./layout-states.mjs";

const json = (route, body) => route.fulfill({ contentType: "application/json", body: JSON.stringify(body) });

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

/// Die Übersicht am Handy mit der API eines Abrufs. `lost`: die Antwort von `startSync` geht verloren, danach
/// antwortet `getCurrentSession` der Reihe nach aus `lost.reads` ("abort" bricht die Verbindung ab).
async function open({ lost } = {}) {
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  const calls = { start: 0, confirm: 0 };
  let current = null; // die Sitzung, die der Server gerade kennt
  await page.route("**/api/**", (r) => r.fulfill({ status: 500, contentType: "application/json", body: '{"error":"nicht gemockt"}' }));
  await stateNamed("abrufen-tan").mock(page);
  await page.route("**/api/SyncApi/getCurrentSession", (r) => {
    if (calls.start > 0 && lost?.reads?.length) {
      const next = lost.reads.shift();
      if (next === "abort") return r.abort("connectionreset");
      current = next;
    }
    return json(r, current);
  });
  await page.route("**/api/SyncApi/startSync", (r) => {
    calls.start += 1;
    if (lost) {
      // Die Antwort geht verloren, der Server hat den Start aber verarbeitet (Live-Befund "Http error (0)").
      current = lost.server;
      return r.abort("connectionreset");
    }
    current = syncSession("AwaitingTan");
    return json(r, { Ok: { TanRequired: current } });
  });
  await page.route("**/api/SyncApi/confirmTan", (r) => {
    calls.confirm += 1;
    current = syncSession("Completed", FETCH_RESULT);
    return json(r, { Ok: current });
  });
  await page.goto(`http://localhost:${port}/`);
  await page.locator('[data-testid="overview-month"], [data-testid="overview-month-loading"]').first().waitFor();
  await page.getByRole("button", { name: "Abrufen" }).locator("visible=true").first().tap();
  return { context, page, calls };
}

const sheet = (page) => page.locator(".bottom-sheet.active").first();

test("[flow:abrufen] Über Abrufen mit Push-TAN abrufen und das Ergebnis lesen", async () => {
  const { context, page, calls } = await open();
  try {
    const confirm = sheet(page).getByRole("button", { name: "Ich habe bestätigt" });
    await confirm.waitFor();
    assert.equal(calls.confirm, 0, "wartet auf Roman");
    await confirm.tap();
    await sheet(page).getByText("6 neue Buchungen").waitFor();
    assert.equal(calls.start, 1, "ein Start");
    assert.equal(calls.confirm, 1, "eine Bestätigung");
    await sheet(page).getByRole("button", { name: "Jetzt durchgehen" }).waitFor();
  } finally {
    await context.close();
  }
});

test("[fable] verlorene Antwort, das erste Neulesen bricht ab: der nächste Versuch findet den TAN-Schritt", async () => {
  const { context, page, calls } = await open({ lost: { server: syncSession("AwaitingTan"), reads: ["abort", syncSession("AwaitingTan")] } });
  try {
    await sheet(page).getByRole("button", { name: "Ich habe bestätigt" }).waitFor();
    assert.equal(calls.start, 1, "kein zweiter Start");
    assert.equal(await sheet(page).locator('[data-testid="fetch-error"]').count(), 0, "kein Fehler");
  } finally {
    await context.close();
  }
});
