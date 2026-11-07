// scripts/layout-invariants.test.mjs
//
// Der Layout-Sweep (infrastructure-nx14s): jeder Zustand aus `layout-states.mjs` bei jeder seiner Breiten, für
// jeden dieselben Invarianten aus `layout-invariants.mjs`. Ein Layoutbug bekommt hier einen neuen Zustand oder
// eine neue Invariante, keinen eigenen Test je Komponente (Doktrin Punkt 5 und 8). Feste Designwerte (Größen,
// Abstände, Farben, Reihenfolge, Animation) prüft der Sweep nicht.
//
// Je Invariante ein Gegenbeweis: eine absichtlich kaputte Seite (injiziertes CSS) zeigt, dass der Sweep die
// Verletzung mit Zustand, Breite, Selektor und Messwert meldet.
//
// Ausführen: npm run test:scripts -- scripts/layout-invariants.test.mjs

import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { chromium } from "playwright";
import { getFreePort, startFrontend, stopServer } from "./screenshot-route.mjs";
import { formatViolation, runState, sweep } from "./layout-invariants.mjs";
import { STATES, stateNamed } from "./layout-states.mjs";

let frontend;
let frontendPort;
let browser;
let baseUrl;

before(async () => {
  frontendPort = await getFreePort();
  frontend = await startFrontend(frontendPort, undefined);
  browser = await chromium.launch();
  baseUrl = `http://127.0.0.1:${frontendPort}`;
});

after(async () => {
  if (browser) await browser.close();
  await stopServer(frontend, frontendPort);
});

test("[layout] Sweep: jeder Zustand hält bei jeder Breite alle Layout-Invarianten", async () => {
  const { violations, errors, runs } = await sweep(browser, baseUrl, STATES);
  assert.deepEqual(errors, [], "jeder Zustand ließ sich herstellen");
  assert.ok(runs >= STATES.length, `${runs} Läufe`);
  assert.deepEqual(violations.map(formatViolation), [], `${violations.length} Verletzungen in ${runs} Läufen`);
});

test("[layout] Sweep: Ausnahmen sind einzeln benannt, heute nur die Schritte im Tab Geld bei 320 px", async () => {
  const exceptions = STATES.flatMap((s) => (s.exceptions ?? []).map((e) => `${s.name} @ ${e.width} px, Invariante ${e.inv}: ${e.selector}`));
  assert.deepEqual(exceptions, ['budget-geld-zuweisen @ 320 px, Invariante 2: button[data-testid="assign-step"]']);
  // Bei 390 px gilt dieselbe Invariante für die Schritte ohne Ausnahme.
  await brokenFinds("budget-geld-zuweisen", 390, 2, '[data-testid="assign-steps"] { width: 200px; }');
});

/// Ein Zustand mit injiziertem CSS, gemessen nur für eine Invariante. Die Verletzung trägt Zustand, Breite,
/// Selektor und Messwert.
async function brokenFinds(stateName, width, inv, css) {
  const found = await runState(browser, baseUrl, stateNamed(stateName), width, { inject: css, checks: [inv] });
  const hits = found.filter((v) => v.inv === inv);
  assert.ok(hits.length > 0, `Invariante ${inv} meldet die kaputte Seite (${stateName} @ ${width})`);
  for (const v of hits) {
    assert.equal(v.state, stateName);
    assert.equal(v.width, width);
    assert.ok(v.selector && v.value, `Selektor und Messwert: ${formatViolation(v)}`);
  }
  // Ohne das CSS ist derselbe Zustand grün: gemeldet wird die Verletzung, nicht der Zustand.
  const clean = await runState(browser, baseUrl, stateNamed(stateName), width, { checks: [inv] });
  assert.deepEqual(clean.map(formatViolation), [], "ohne das CSS keine Meldung");
  return hits;
}

const transitionsOff = "*, *::before, *::after { transition: none !important; animation: none !important; }";

test("[layout] Gegenbeweis Invariante 1: eine zu breite Seite meldet seitliches Scrollen", async () => {
  const hits = await brokenFinds("budget", 390, 1, '[data-testid="budget-page"] { min-width: 150vw; }');
  assert.ok(hits.some((v) => v.selector === "html"), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 2: ein Knopf unter 48 px meldet die Trefferfläche", async () => {
  const hits = await brokenFinds(
    "regeln",
    390,
    2,
    '[data-testid="rules-menu-button"] button { min-width: 0 !important; min-height: 0 !important; width: 24px !important; height: 24px !important; padding: 0 !important; }'
  );
  assert.ok(hits.some((v) => v.selector.includes("rules-menu-button")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 3: ein Feld unter 16 px Schrift meldet die Schrift", async () => {
  const hits = await brokenFinds("kassenbuch-filter-kategorie", 390, 3, "input { font-size: 12px !important; }");
  assert.ok(hits.some((v) => v.value === "font-size 12px"), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 4: ein gekürzter Text ohne title meldet das Abschneiden", async () => {
  const hits = await brokenFinds(
    "budget",
    1440,
    4,
    '[data-testid="budget-to-be-budgeted"] { display: inline-block; width: 8px; overflow: hidden; white-space: nowrap; }'
  );
  assert.ok(hits.some((v) => v.selector.includes("budget-to-be-budgeted")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 5: ein Sheet, das unten aus dem Bild ragt, meldet die Ebene", async () => {
  const hits = await brokenFinds("kassenbuch-detail", 390, 5, `${transitionsOff} .bottom-sheet.active { transform: translateY(-40%) !important; }`);
  assert.ok(hits.some((v) => v.value.includes("nicht ganz im Bild")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 6: ein Tabellenkopf, der unter die Navigation rutscht, meldet die Überlappung", async () => {
  const hits = await brokenFinds("kassenbuch", 1440, 6, "thead, thead th { top: 0 !important; }");
  assert.ok(hits.some((v) => v.value.startsWith("überlappen")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 7: ein Primärknopf unter dem Rand des Sheets meldet Scrollen", async () => {
  const hits = await brokenFinds("budget-target", 390, 7, ".bottom-sheet.active .sheet-body { padding-top: 2000px; }");
  assert.ok(hits.some((v) => v.selector.includes("target-save")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 8: ein Spaltenkopf mit zu breitem Text meldet die Überlappung", async () => {
  const hits = await brokenFinds("kassenbuch", 1024, 8, "thead th { letter-spacing: 40px !important; }");
  assert.ok(hits.some((v) => v.selector.startsWith("th")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 9: eine Zeile, die erst nach der Antwort wächst, meldet den Sprung", async () => {
  const hits = await brokenFinds("sync-start", 390, 9, 'p[title="Nächster Sync ohne TAN"] { display: block; height: 80px; }');
  assert.ok(hits.some((v) => v.selector.includes("Sync starten")), hits.map(formatViolation).join("\n"));
});
