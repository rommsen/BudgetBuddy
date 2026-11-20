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
import { formatViolation, runState, sweep, withoutExceptions } from "./layout-invariants.mjs";
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

// Der Sweep ist ein Test mit rund 300 Browser-Läufen (design-system-x045q: 294). Unter der Last paralleler Worktrees
// brauchte er knapp über die 120 s, die `run-guards.mjs` je Test setzt, und brach ohne Verletzung ab. Er bekommt deshalb
// eine eigene Höchstdauer; ein hängender Lauf wird weiter rot.
test("[layout] Sweep: jeder Zustand hält bei jeder Breite alle Layout-Invarianten", { timeout: 360000 }, async () => {
  const { violations, errors, runs } = await sweep(browser, baseUrl, STATES);
  assert.deepEqual(errors, [], "jeder Zustand ließ sich herstellen");
  assert.deepEqual(violations.map(formatViolation), [], `${violations.length} Verletzungen in ${runs} Läufen`);
});

test("[layout] Sweep: eine Ausnahme nennt Breite, Invariante und Selektor und unterdrückt nur genau diese Meldung", async () => {
  // Jede Ausnahme ist einzeln benannt (Doktrin Punkt 5): eine Breite des Zustands, eine Invariante, ein Selektor.
  for (const state of STATES) {
    for (const e of state.exceptions ?? []) {
      const label = `${state.name}: ${JSON.stringify(e)}`;
      assert.ok(state.widths.includes(e.width), `Breite des Zustands (${label})`);
      assert.ok(Number.isInteger(e.inv) && e.inv >= 1 && e.inv <= 12, `Invariante 1 bis 12 (${label})`);
      assert.ok(typeof e.selector === "string" && e.selector.trim() !== "", `Selektor nicht leer (${label})`);
    }
  }
  // Bei 390 px gilt Invariante 2 für die Schritte ohne Ausnahme.
  const hits = await brokenFinds("budget-geld-zuweisen", 390, 2, '[data-testid="assign-steps"] { width: 200px; }');
  const steps = hits.filter((v) => v.selector.startsWith('button[data-testid="assign-step"]'));
  assert.ok(steps.length > 0, hits.map(formatViolation).join("\n"));
  // Genau passend unterdrückt sie die Meldung; mit anderer Invariante, anderem Selektor oder anderer Breite nicht.
  const exception = { width: 390, inv: 2, selector: 'button[data-testid="assign-step"]' };
  assert.deepEqual(withoutExceptions([exception], 390, steps), [], "die passende Ausnahme greift");
  assert.equal(withoutExceptions([{ ...exception, inv: 3 }], 390, steps).length, steps.length, "andere Invariante");
  assert.equal(withoutExceptions([{ ...exception, selector: 'button[data-testid="assign-missing"]' }], 390, steps).length, steps.length, "anderer Selektor");
  assert.equal(withoutExceptions([{ ...exception, width: 320 }], 390, steps).length, steps.length, "andere Breite");
});

/// Ein Zustand mit injiziertem CSS (und optional `prepare`), gemessen nur für eine Invariante.
async function brokenRun(stateName, width, inv, css, prepare = null) {
  const found = await runState(browser, baseUrl, stateNamed(stateName), width, { inject: css, prepare, checks: [inv] });
  return found.filter((v) => v.inv === inv);
}

/// Wie `brokenRun`, dazu: die Verletzung trägt Zustand, Breite, Selektor und Messwert, und ohne das CSS ist
/// derselbe Zustand grün.
async function brokenFinds(stateName, width, inv, css) {
  const hits = await brokenRun(stateName, width, inv, css);
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

test("[layout] Gegenbeweis Invariante 1: ein zu breiter Inhalt im Sheet meldet seitliches Scrollen im Sheet", async () => {
  const hits = await brokenFinds("kassenbuch-detail", 390, 1, ".bottom-sheet.active > .sheet-body > * { min-width: 150vw; }");
  // Das Sheet liegt fest über der Seite: gemeldet wird es selbst, nicht html oder body.
  assert.ok(!hits.some((v) => v.selector === "html" || v.selector === "body"), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 2: ein Knopf mit 46 px meldet die Trefferfläche", async () => {
  // 46 px liegt knapp unter der Schwelle 48: eine auf 44 gesenkte Schwelle machte den Test rot.
  const hits = await brokenFinds(
    "regeln",
    390,
    2,
    '[data-testid="rule-move-down"] { min-width: 0 !important; min-height: 0 !important; width: 46px !important; height: 46px !important; padding: 0 !important; }'
  );
  assert.ok(hits.some((v) => v.selector.includes("rule-move-down") && v.value === "46 × 46 px"), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 3: ein Feld mit 15 px Schrift meldet die Schrift", async () => {
  const hits = await brokenFinds("kassenbuch-filter-kategorie", 390, 3, "input { font-size: 15px !important; }");
  assert.ok(hits.some((v) => v.value === "font-size 15px"), hits.map(formatViolation).join("\n"));
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

test("[layout] Gegenbeweis Invariante 4: gekürzter Text braucht Ellipse und title zugleich", async () => {
  // Drei gekürzte Zeilen auf der Budgetseite: nur Ellipse, Ellipse mit title, nur title.
  const cut = "width: 40px; overflow: hidden; white-space: nowrap;";
  const hits = await brokenRun("budget", 1440, 4, "", (page) =>
    page.locator('[data-testid="budget-page"]').evaluate((root, cut) => {
      for (const [id, style, title] of [
        ["sweep-ellipse", `${cut} text-overflow: ellipsis;`, null],
        ["sweep-ellipse-title", `${cut} text-overflow: ellipsis;`, "Ein langer Text"],
        ["sweep-title", cut, "Ein langer Text"],
      ]) {
        const el = document.createElement("div");
        el.setAttribute("data-testid", id);
        el.setAttribute("style", style);
        if (title) el.title = title;
        el.textContent = "Ein langer Text";
        root.append(el);
      }
    }, cut)
  );
  const report = hits.map(formatViolation).join("\n");
  assert.ok(hits.some((v) => v.selector.startsWith('div[data-testid="sweep-ellipse"]')), `nur Ellipse wird gemeldet\n${report}`);
  assert.ok(hits.some((v) => v.selector.startsWith('div[data-testid="sweep-title"]')), `nur title wird gemeldet\n${report}`);
  assert.ok(!hits.some((v) => v.selector.startsWith('div[data-testid="sweep-ellipse-title"]')), `Ellipse mit title bleibt still\n${report}`);
});

test("[layout] Gegenbeweis Invariante 4: Text in einer Grafik zählt nicht als abgeschnitten", async () => {
  // Kontrolle in derselben Seite: dieselbe Zeile als HTML wird gemeldet.
  const hits = await brokenRun("budget", 1440, 4, "", (page) =>
    page.locator('[data-testid="budget-page"]').evaluate((root) => {
      root.insertAdjacentHTML(
        "beforeend",
        '<svg data-testid="sweep-svg" width="20" height="20"><text x="0" y="15">Ein langer Text in der Grafik</text></svg>' +
          '<div data-testid="sweep-html" style="width: 20px; overflow: hidden; white-space: nowrap;">Ein langer Text als HTML</div>'
      );
    })
  );
  const report = hits.map(formatViolation).join("\n");
  assert.ok(hits.some((v) => v.selector.startsWith('div[data-testid="sweep-html"]')), `HTML wird gemeldet\n${report}`);
  assert.ok(!hits.some((v) => v.selector.includes("sweep-svg")), `Grafik bleibt still\n${report}`);
});

test("[layout] Gegenbeweis Invariante 4: ein Platzhalter, der nicht ins Datumsfeld passt, meldet das Abschneiden", async () => {
  // budgeting-g2ty9: der Platzhalter des Datums stand halb im Feld.
  const hits = await brokenFinds("budget-target-anlegen", 320, 4, ".bottom-sheet.active input[placeholder] { width: 40px !important; min-width: 0 !important; }");
  assert.ok(hits.some((v) => v.value.startsWith("Platzhalter")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 5: ein Sheet, das unten aus dem Bild ragt, meldet die Ebene", async () => {
  const hits = await brokenFinds("kassenbuch-detail", 390, 5, `${transitionsOff} .bottom-sheet.active { transform: translateY(-40%) !important; }`);
  assert.ok(hits.some((v) => v.value.includes("nicht ganz im Bild")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 5: ein Toast hinter dem offenen Dialog meldet seinen Knopf als verdeckt", async () => {
  // Regression c819eeb: der Toast nach dem Kategoriewechsel lag hinter dem Detail-Sheet. Seit design-system-gkded steht
  // der Toast nur ab 1024 px; dort liegt er über einem offenen Dialog (Pins speichern scheitert im Dialog „Angepinnt“).
  const hits = await brokenFinds("uebersicht-meldung", 1280, 5, ".toast-layer { z-index: 0 !important; }");
  assert.ok(hits.some((v) => v.selector.startsWith("button") && v.value.startsWith("verdeckt")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 5: ein Betrag, der über den Payee rückt, meldet die Überdeckung", async () => {
  // budgeting-m1bce (Fix 66a7c32): Payee und Betrag einer Buchung überlappten am Handy.
  const hits = await brokenFinds("budget-buchungen", 320, 5, '[data-testid="detail-entry"] .font-mono { margin-left: -60px; }');
  assert.ok(hits.some((v) => v.value.startsWith("Text überdeckt") && v.value.includes("Ein Empfänger")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 5: eine Liste, deren Ende unter der Auswahl-Leiste liegt, meldet das Verdecken", async () => {
  // Ohne den Innenabstand von main reicht der Abstandshalter (h-52) nicht für die Leiste mit Grund.
  const hits = await brokenFinds("kassenbuch-auswahl-handy", 390, 5, "main { padding-bottom: 0 !important; }");
  assert.ok(hits.some((v) => v.value.startsWith("am Ende gescrollt verdeckt") && v.value.includes("selection-bar")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 12 (a): ohne den freigehaltenen Platz der Leiste meldet sie die letzte Zeile als verdeckt", async () => {
  // Review ledger-at63w: „+ Buchung“ verdeckte am Handy Betrag und Symbol der letzten Zeile, weil unter der Liste nur
  // der Platz für die Navigation frei war. Seit design-system-gkded steht der Knopf in der Meldungsleiste, deren Höhe
  // die Seitenhülle über `--feedback-bar-height` freihält; ohne sie tritt das wieder auf.
  const hits = await brokenFinds("kassenbuch", 390, 12, ":root { --feedback-bar-height: 0px !important; }");
  assert.ok(hits.some((v) => v.value.startsWith("am Ende gescrollt: von der Meldungsleiste verdeckt")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 12 (b): ein am Handy sichtbarer Toast meldet „Toast am Handy“", async () => {
  const hits = await brokenFinds("kassenbuch-leiste-geprueft", 390, 12, ".toast-layer { display: flex !important; bottom: 200px; }");
  assert.ok(hits.some((v) => v.value.startsWith("Toast am Handy")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 12 (c): eine Meldung über dem runden + meldet den Knopf als verdeckt", async () => {
  // Regression design-system-vf94m: der Toast mit Aktion stand am Handy unten über dem schwebenden „+ Buchung“.
  const hits = await brokenFinds(
    "kassenbuch-leiste-geprueft",
    412,
    12,
    '[data-testid="feedback-message"] { margin-right: -80px; position: relative; z-index: 1; }'
  );
  assert.ok(
    hits.some((v) => v.selector.includes("ledger-capture-fab") && v.value.startsWith("Knopf der Leiste verdeckt")),
    hits.map(formatViolation).join("\n")
  );
});

test("[layout] Gegenbeweis Invariante 6: ein Sheet, das als Ganzes scrollt, meldet Kopf und Tabs als mitgerückt", async () => {
  // Früher in budget-page.test.mjs: Kopf und Tabs des Sheets bleiben stehen, wenn der Inhalt scrollt.
  const hits = await brokenFinds(
    "budget-buchungen",
    390,
    6,
    ".bottom-sheet.active { overflow-y: auto !important; } .bottom-sheet.active > .sheet-body { overflow: visible !important; flex: none !important; height: auto !important; max-height: none !important; }"
  );
  const report = hits.map(formatViolation).join("\n");
  assert.ok(hits.some((v) => v.selector.includes("sheet-header") && v.value.startsWith("rückt beim Scrollen")), report);
  assert.ok(hits.some((v) => v.selector.includes('[role="tablist"]')), report);
});

test("[layout] Gegenbeweis Invariante 6: ein Tabellenkopf, der unter die Navigation rutscht, meldet die Überlappung", async () => {
  const hits = await brokenFinds("kassenbuch", 1440, 6, "thead, thead th { top: 0 !important; }");
  assert.ok(hits.some((v) => v.value.startsWith("überlappen")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 7: ein Primärknopf unter dem Rand des Sheets meldet Scrollen", async () => {
  const hits = await brokenFinds("budget-target", 390, 7, ".bottom-sheet.active .sheet-body { padding-top: 2000px; }");
  assert.ok(hits.some((v) => v.selector.includes("target-save")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 7: ein Feld, das erst bei offener Tastatur aus dem Bild rutscht, meldet die Tastatur", async () => {
  // Nur im halbierten Viewport (422 px hoch) rutscht das Sheet nach unten aus dem Bild, mit ihm das Betragsfeld.
  const hits = await brokenFinds(
    "budget-geld-zuweisen",
    390,
    7,
    "@media (max-height: 500px) { .bottom-sheet.active { transform: translateY(80%) !important; transition: none !important; } }"
  );
  assert.ok(hits.every((v) => v.value.startsWith("bei offener Tastatur: ")), hits.map(formatViolation).join("\n"));
  assert.ok(hits.some((v) => v.selector.includes("assign-field")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 8: ein Spaltenkopf mit zu breitem Text meldet die Überlappung", async () => {
  // Bei 1024 px steht neben dem Panel die Kompaktliste ohne Spaltenköpfe (ledger-cnaz3): gemessen bei 1440 px.
  const hits = await brokenFinds("kassenbuch", 1440, 8, "thead th { letter-spacing: 40px !important; }");
  assert.ok(hits.some((v) => v.selector.startsWith("th")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 9: eine Zeile, die erst nach der Antwort wächst, meldet den Sprung", async () => {
  // Früher am Zustand `sync-start` (die Seite Sync entfiel mit banking-import-d0fey): jetzt wächst die Karte Monat
  // der Übersicht erst nach ihrer Antwort. Gemeldet wird, was darunter rutscht: am PC die neuesten Buchungen.
  const hits = await brokenFinds("uebersicht-ohne-minus", 1280, 9, '[data-testid="overview-month"] { padding-bottom: 400px !important; }');
  assert.ok(hits.some((v) => v.selector.includes("overview-recent")), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 10: Text mit halber Deckkraft meldet den Kontrast", async () => {
  // Wie die ausgegraute Zeile vor ledger-dn4vy (`opacity-50`): `text-muted` fiel auf 2,23:1.
  const hits = await brokenFinds("kassenbuch-ausgegraut", 1280, 10, ".filtered-out-text { opacity: .5 !important; }");
  assert.ok(hits.some((v) => v.selector.includes('"Bauhaus"')), hits.map(formatViolation).join("\n"));
  assert.ok(hits.every((v) => /^Kontrast \d/.test(v.value)), hits.map(formatViolation).join("\n"));
});

test("[layout] Gegenbeweis Invariante 10: ein gesperrtes Element zählt nicht, ein freies mit derselben Farbe schon", async () => {
  // WCAG 1.4.3 nimmt gesperrte Bedienelemente aus. Gleiches CSS, einmal mit, einmal ohne `aria-disabled`.
  const css = '[data-testid="filtered-out-note"] { opacity: .5 !important; }';
  const note = (v) => v.selector.includes("passt nicht mehr zum Filter");
  const free = await brokenRun("kassenbuch-ausgegraut", 1280, 10, css);
  assert.ok(free.some(note), free.map(formatViolation).join("\n"));
  const locked = await brokenRun("kassenbuch-ausgegraut", 1280, 10, css, (page) =>
    page.locator('[data-testid="filtered-out-note"]').evaluateAll((els) => els.forEach((el) => el.setAttribute("aria-disabled", "true")))
  );
  assert.deepEqual(locked.filter(note).map(formatViolation), []);
});

test("[layout] Gegenbeweis Invariante 11: ein zweites Sheet über dem Detail meldet die Ebene", async () => {
  // ledger-hme9k: ein Abbild des offenen Detail-Sheets liegt als zweites Sheet darüber, wie es Payee und Memo im
  // Durchgehen mit offenen „Details“ taten.
  const hits = await brokenRun("kassenbuch-detail", 390, 11, transitionsOff, (page) =>
    page.evaluate(() => {
      const sheet = document.querySelector(".bottom-sheet.active");
      const copy = sheet.cloneNode(true);
      copy.querySelector(".sheet-header h3").textContent = "Payee";
      sheet.parentElement.appendChild(copy);
    })
  );
  assert.equal(hits.length, 1, hits.map(formatViolation).join("\n"));
  assert.equal(hits[0].state, "kassenbuch-detail");
  assert.equal(hits[0].width, 390);
  assert.match(hits[0].selector, /^\.bottom-sheet ".+" und \.bottom-sheet "Payee"$/);
  assert.equal(hits[0].value, "2 Ebenen offen");
  // Ein Popover in einer Ebene zählt nicht: ein Dialog-Popover (`data-layer="popover"`) über dem Sheet bleibt grün.
  const popover = await brokenRun("kassenbuch-detail", 390, 11, transitionsOff, (page) =>
    page.evaluate(() => {
      const el = document.createElement("div");
      el.setAttribute("data-layer", "popover");
      el.textContent = "Popover";
      document.querySelector(".bottom-sheet.active .sheet-body").appendChild(el);
    })
  );
  assert.deepEqual(popover.map(formatViolation), [], "ein Popover in der Ebene zählt nicht");
  const clean = await runState(browser, baseUrl, stateNamed("kassenbuch-detail"), 390, { checks: [11] });
  assert.deepEqual(clean.map(formatViolation), [], "ohne zweites Sheet keine Meldung");
});

test("[layout] settle wartet nicht auf eine pausierte Animation", { timeout: 30000 }, async () => {
  const css = '[data-testid="budget-page"] { animation: sweep-paused 1s paused; } @keyframes sweep-paused { to { opacity: 0.5; } }';
  assert.deepEqual(await brokenRun("budget", 390, 1, css), []);
});

test("[layout] settle wirft, wenn die Lage nicht zur Ruhe kommt", { timeout: 30000 }, async () => {
  const css = "body { animation: sweep-grow 0.2s linear infinite alternate; } @keyframes sweep-grow { from { padding-bottom: 0; } to { padding-bottom: 2000px; } }";
  await assert.rejects(brokenRun("budget", 390, 1, css), (error) => {
    assert.match(error.message, /budget @ 390 px/);
    assert.match(error.message, /Lage steht nach 300 Frames nicht/);
    assert.match(error.message, /sweep-grow/);
    return true;
  });
});
