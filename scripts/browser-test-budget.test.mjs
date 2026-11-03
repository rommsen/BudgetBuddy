// scripts/browser-test-budget.test.mjs
//
// Wächter für die Test-Doktrin Punkt 1 und 5 (infrastructure-t5m2b): Browser-Tests sind die teuerste
// Ebene und die Quelle des Flackerns. Logik gehört unter .NET (Client-`update`, reine Funktionen), im
// Browser bleibt, was nur dort messbar ist.
//
// Die Kennungen der Browser-Tests prüft run-guards.mjs zur Laufzeit (infrastructure-b3t9k): jeder
// gelaufene Blatt-Test einer Datei, die einen Browser oder ein Frontend gestartet hat, trägt am Anfang
// seines Namens genau eine Kennung aus `TAGS`, ein `[flow:<name>]` nennt einen Flow aus `FLOWS`, und
// je Flow läuft höchstens ein Test (browser-usage.mjs). Eine feste Obergrenze gibt es nicht, die Zahl
// je Kennung wird gemeldet. Diese Datei belegt die Regel mit Gegenbeweisen je Testform und prüft
// statisch das Verbot fester Wartezeiten.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { execFileSync } from "node:child_process";
import { runCounted, countingEnv, gateExitCode } from "./run-guards.mjs";
import { leafTests, browserLeaves, testTag, tagViolations, tagSummary, summaryLine, FLOWS, usageOwner, isBrowserCommand, OWNER_ENV } from "./browser-usage.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const SELF = path.basename(fileURLToPath(import.meta.url));

/// `waitForTimeout` ist verboten (Doktrin Punkt 5): auf Bedingungen warten.
export const MAX_FIXED_WAITS = 0;
const FIXED_WAIT = /\bwaitForTimeout\s*\(/g;

/// Feste Wartezeiten in den Quelltexten `{ name, source }`.
export function fixedWaits(sources) {
  return sources.reduce((sum, f) => sum + (f.source.match(FIXED_WAIT) ?? []).length, 0);
}

const realSources = () =>
  fs
    .readdirSync(here)
    .filter((f) => f.endsWith(".test.mjs") && f !== SELF)
    .map((name) => ({ name, source: fs.readFileSync(path.join(here, name), "utf8") }));

test("Kein waitForTimeout in scripts/*.test.mjs", () => {
  assert.equal(fixedWaits(realSources()), MAX_FIXED_WAITS);
});

test("Gegenbeweis: eine feste Wartezeit in einer echten Testdatei wird gezählt", () => {
  const extra = { name: "neu.test.mjs", source: "await page.waitForTimeout(100);\n" };
  assert.equal(fixedWaits([...realSources(), extra]), MAX_FIXED_WAITS + 1);
});

const line = (file, nesting, name, kind = "test") => JSON.stringify({ file: `/x/${file}`, nesting, name, kind });

test("Blatt-Tests: Untertests stehen statt ihres Elterntests, Suiten nicht", () => {
  const leaves = leafTests([
    line("a.test.mjs", 1, "[layout] u1"), line("a.test.mjs", 1, "[fokus] u2"), line("a.test.mjs", 0, "suite", "suite"),
    line("b.test.mjs", 1, "[scroll] b1"), line("a.test.mjs", 0, "[fable] neben der Suite"), line("b.test.mjs", 0, "eltern"),
  ]);
  assert.deepEqual(leaves, [
    { file: "/x/a.test.mjs", name: "[layout] u1" },
    { file: "/x/a.test.mjs", name: "[fokus] u2" },
    { file: "/x/b.test.mjs", name: "[scroll] b1" },
    { file: "/x/a.test.mjs", name: "[fable] neben der Suite" },
  ]);
  assert.deepEqual(browserLeaves(leaves, new Set(["/x/a.test.mjs"])).map((l) => l.name), ["[layout] u1", "[fokus] u2", "[fable] neben der Suite"]);
});

test("Kennung: jede der sechs Kennungen wird am Anfang des Namens erkannt", () => {
  assert.deepEqual(testTag("[layout] Breiten von 320 bis 1920 px"), { tag: "layout" });
  assert.deepEqual(testTag("[fokus] Der Fokus bleibt im Feld"), { tag: "fokus" });
  assert.deepEqual(testTag("[tastatur] Pfeile wechseln die Zeile"), { tag: "tastatur" });
  assert.deepEqual(testTag("[scroll] Die Liste scrollt mit"), { tag: "scroll" });
  assert.deepEqual(testTag("[fable] Beträge stehen deutsch"), { tag: "fable" });
  assert.deepEqual(testTag("[flow:geld-zuweisen] Zeile wählen, Betrag tippen"), { tag: "flow", flow: "geld-zuweisen" });
});

test("Kennung: ohne Kennung, unbekannt, nicht am Anfang, Flow ohne Namen oder zwei Kennungen ist ungültig", () => {
  for (const name of [
    "Breiten von 320 bis 1920 px",
    "[optik] Farben stimmen",
    "Breiten [layout] von 320 px",
    " [layout] mit Leerzeichen davor",
    "[flow:] ohne Namen",
    "[flow] ohne Doppelpunkt",
    "[layout]ohne Abstand",
    "[layout] [fokus] zwei Kennungen",
    "[Layout] groß geschrieben",
    undefined,
  ])
    assert.equal(testTag(name), null, String(name));
});

const leaf = (file, name) => ({ file: `/x/${file}`, name });

test("Prüfung: ein Test ohne Kennung ist rot und nennt Datei und Testname", () => {
  const violations = tagViolations([leaf("a.test.mjs", "[layout] gut"), leaf("a.test.mjs", "Breiten ohne Kennung")], { x: "." });
  assert.equal(violations.length, 1);
  assert.match(violations[0], /a\.test\.mjs: "Breiten ohne Kennung" hat keine gültige Kennung/);
});

test("Prüfung: ein Flow außerhalb der Liste ist rot", () => {
  const violations = tagViolations([leaf("a.test.mjs", "[flow:unbekannt] Ablauf")], { bekannt: "." });
  assert.equal(violations.length, 1);
  assert.match(violations[0], /Flow "unbekannt" steht nicht in FLOWS/);
});

test("Prüfung: zwei Tests für denselben Flow sind rot, auch in verschiedenen Dateien, mit beiden Namen", () => {
  const violations = tagViolations([leaf("a.test.mjs", "[flow:x] Handy"), leaf("b.test.mjs", "[flow:x] PC")], { x: "." });
  assert.equal(violations.length, 1);
  assert.match(violations[0], /Flow "x" hat 2 Tests/);
  assert.match(violations[0], /a\.test\.mjs: "\[flow:x\] Handy"/);
  assert.match(violations[0], /b\.test\.mjs: "\[flow:x\] PC"/);
});

test("Prüfung: nur gültige Kennungen und je Flow ein Test sind grün", () => {
  const leaves = [leaf("a.test.mjs", "[layout] a"), leaf("a.test.mjs", "[fable] b"), leaf("b.test.mjs", "[flow:x] c"), leaf("b.test.mjs", "[flow:y] d")];
  assert.deepEqual(tagViolations(leaves, { x: ".", y: "." }), []);
});

test("Prüfung: Tests in Dateien ohne Browser-Marke brauchen keine Kennung", () => {
  const leaves = leafTests([line("a.test.mjs", 0, "ohne Kennung")]);
  assert.equal(tagViolations(browserLeaves(leaves, new Set(["/x/a.test.mjs"]))).length, 1, "mit Marke rot");
  assert.deepEqual(tagViolations(browserLeaves(leaves, new Set())), [], "dieselbe Datei ohne Marke grün");
});

test("Zusammenfassung: [layout], [layout], [flow:a] ergibt layout 2, flow 1, Summe 3", () => {
  const summary = tagSummary([leaf("a.test.mjs", "[layout] eins"), leaf("a.test.mjs", "[layout] zwei"), leaf("a.test.mjs", "[flow:a] drei")]);
  assert.deepEqual(summary, { counts: { layout: 2, flow: 1 }, total: 3 });
  assert.equal(summaryLine(summary), "Browser-Tests je Kennung: layout 2, flow 1, Summe 3");
});

test("Das Gate wird rot bei einem Verstoß, auch wenn alle Tests grün sind, und bleibt rot bei rotem Test", () => {
  assert.equal(gateExitCode(0, []), 0);
  assert.equal(gateExitCode(0, ["Verstoß"]), 1);
  assert.equal(gateExitCode(1, []), 1);
});

test("Jeder Flow der Liste trägt einen gültigen Namen und einen Satz", () => {
  for (const [flow, sentence] of Object.entries(FLOWS)) {
    assert.deepEqual(testTag(`[flow:${flow}] x`), { tag: "flow", flow }, flow);
    assert.match(sentence, /^\S.*\.$/, flow);
  }
});

test("Als Browserstart gilt die ausführbare Datei eines Browsers, nicht ein Werkzeug", () => {
  for (const command of [
    "/Users/x/Library/Caches/ms-playwright/chromium-1194/chrome-mac/Chromium.app/Contents/MacOS/Chromium",
    "/c/ms-playwright/chromium_headless_shell-1194/chrome-mac/headless_shell",
    "/c/ms-playwright/firefox-1495/firefox/Nightly.app/Contents/MacOS/firefox",
    "/c/ms-playwright/webkit-2182/pw_run.sh",
  ])
    assert.equal(isBrowserCommand(command), true, command);
  for (const command of ["npx", "/opt/homebrew/bin/node", "dotnet", undefined]) assert.equal(isBrowserCommand(command), false, String(command));
});

test("Ein Werkzeug, das der Test startet, wird der Testdatei zugerechnet", () => {
  assert.equal(usageOwner({ NODE_TEST_CONTEXT: "child-v8" }, ["node", "/x/a.test.mjs"]), "/x/a.test.mjs");
  assert.equal(usageOwner({ [OWNER_ENV]: "/x/a.test.mjs" }, ["node", "/x/werkzeug.mjs"]), "/x/a.test.mjs");
  assert.equal(usageOwner({}, ["node", "/x/a.test.mjs"]), null, "ohne Testlauf keine Zuordnung");
});

// Gegenbeweis je Form, die eine Zählung über den Quelltext übersah. Jede Datei startet über ein
// Hilfsmodul mit umbenanntem Import wirklich einen Browser; eine Datei ohne Browser zählt nicht.
const FORMS = {
  "it.test.mjs": ['import { it } from "node:test";', 'it("[layout] eins", () => {});', 'it("[fokus] zwei", () => {});'],
  "describe.test.mjs": ['import { describe, it } from "node:test";', 'describe("s", () => { it("[tastatur] eins", () => {}); it("[scroll] zwei", () => {}); });'],
  "schleife.test.mjs": ['import { test } from "node:test";', 'for (const n of [1, 2, 3]) test(`[layout] n${n}`, () => {});'],
  "foreach.test.mjs": ['import { test } from "node:test";', '[1, 2].forEach((n) => test(`[fable] n${n}`, () => {}));'],
  "untertest.test.mjs": ['import { test } from "node:test";', 'test("eltern", async (t) => { await t.test("[layout] a", () => {}); await t.test("[flow:probe] b", () => {}); });'],
  "umbenannt.test.mjs": ['import { test as pruefe } from "node:test";', 'pruefe("[layout] eins", () => {});'],
};
const EXPECTED = 2 + 2 + 3 + 2 + 2 + 1 + 1 + 1 + 1;
const EXPECTED_COUNTS = { layout: 9, fokus: 1, tastatur: 1, scroll: 1, fable: 2, flow: 1 };
const PROBE_FLOWS = { probe: "Ein Flow nur für diesen Gegenbeweis." };

/// Starts über andere Pakete: jede Kopie von `playwright-core` hat ihr eigenes `chromium`-Objekt.
const OTHER_PACKAGES = {
  "core.test.mjs": "playwright-core",
  "playwright-test.test.mjs": "@playwright/test",
};

test("Gegenbeweis: jede Testform in einer Browser-Datei wird zur Laufzeit geprüft und gezählt, eine Datei ohne Browser nicht, ein falscher Name macht das Gate rot", async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "bb-browser-budget-"));
  try {
    const playwright = path.join(here, "..", "node_modules", "playwright", "index.mjs");
    fs.writeFileSync(
      path.join(dir, "hilfe.mjs"),
      `import * as pw from ${JSON.stringify(playwright)};\nexport async function oeffne() { const b = await pw.chromium.launch(); await b.close(); }\n`
    );
    const files = [];
    for (const [name, lines] of Object.entries(FORMS)) {
      fs.writeFileSync(path.join(dir, name), [...lines, 'import { oeffne } from "./hilfe.mjs";', "await oeffne();", ""].join("\n"));
      files.push(path.join(dir, name));
    }
    for (const [name, pkg] of Object.entries(OTHER_PACKAGES)) {
      const entry = path.join(here, "..", "node_modules", ...pkg.split("/"), "index.mjs");
      fs.writeFileSync(
        path.join(dir, name),
        [
          'import { test } from "node:test";',
          `import { chromium } from ${JSON.stringify(entry)};`,
          "const b = await chromium.launch();",
          "await b.close();",
          'test("[layout] eins", () => {});',
          "",
        ].join("\n")
      );
      files.push(path.join(dir, name));
    }
    // Ein gestartetes Frontend ohne Browser zählt auch (startFrontend, statisch ausgeliefert).
    fs.mkdirSync(path.join(dir, "dist"));
    fs.writeFileSync(path.join(dir, "dist", "index.html"), "<!doctype html>");
    const tool = path.join(here, "screenshot-route.mjs");
    fs.writeFileSync(
      path.join(dir, "frontend.test.mjs"),
      [
        'import { test } from "node:test";',
        `import { getFreePort, startFrontend, stopServer } from ${JSON.stringify(tool)};`,
        `process.env.BB_GUARD_DIST = ${JSON.stringify(path.join(dir, "dist"))};`,
        "const port = await getFreePort();",
        "await stopServer(await startFrontend(port, undefined), port);",
        'test("[layout] eins", () => {});',
        "",
      ].join("\n")
    );
    files.push(path.join(dir, "frontend.test.mjs"));
    fs.writeFileSync(path.join(dir, "ohne.test.mjs"), 'import { test } from "node:test";\nfor (let i = 0; i < 5; i += 1) test(`t${i}`, () => {});\n');
    files.push(path.join(dir, "ohne.test.mjs"));

    const valid = await runCounted(files, process.env, { flows: PROBE_FLOWS, stdio: "ignore" });
    assert.equal(valid.code, 0, "die Beispieldateien laufen grün");
    assert.equal(valid.summary.total, EXPECTED, `gezählt: ${JSON.stringify(valid.summary)}`);
    assert.deepEqual(valid.summary.counts, EXPECTED_COUNTS, "je Kennung, ohne.test.mjs zählt nicht");
    assert.deepEqual(valid.violations, [], "gültige Kennungen, ohne.test.mjs braucht keine");
    assert.equal(valid.gateCode, 0);

    fs.writeFileSync(
      path.join(dir, "falsch.test.mjs"),
      ['import { test } from "node:test";', 'import { oeffne } from "./hilfe.mjs";', "await oeffne();", 'test("Breiten ohne Kennung", () => {});', 'test("[flow:probe] zweiter Happy Path", () => {});', ""].join("\n")
    );
    const wrong = await runCounted([path.join(dir, "untertest.test.mjs"), path.join(dir, "falsch.test.mjs")], process.env, { flows: PROBE_FLOWS, stdio: "ignore" });
    assert.equal(wrong.code, 0, "die Tests selbst sind grün");
    assert.equal(wrong.gateCode, 1, "das Gate ist rot");
    assert.equal(wrong.violations.length, 2, JSON.stringify(wrong.violations));
    assert.match(wrong.violations[0], /falsch\.test\.mjs: "Breiten ohne Kennung" hat keine gültige Kennung/);
    assert.match(wrong.violations[1], /Flow "probe" hat 2 Tests/);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

// Regression: die Hülle verlor `util.promisify.custom`, `promisify(exec)` lieferte dann den String statt
// `{ stdout, stderr }`. Über NODE_OPTIONS traf das jeden Node-Prozess im Lauf, auch vite.
test("Mit Hook liefern promisify(exec) und promisify(execFile) dieselbe Form wie ohne", () => {
  const probe = [
    'import { exec, execFile } from "node:child_process";',
    'import { promisify } from "node:util";',
    'const a = await promisify(exec)("echo hi");',
    'const b = await promisify(execFile)("echo", ["hi"]);',
    "console.log(JSON.stringify([a, b].map((r) => (typeof r === \"object\" ? Object.keys(r).sort() : typeof r))));",
  ].join("\n");
  const shape = (env) => execFileSync(process.execPath, ["--input-type=module", "-e", probe], { env, encoding: "utf8" }).trim();
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "bb-promisify-"));
  try {
    const plain = shape({ PATH: process.env.PATH });
    assert.equal(plain, JSON.stringify([["stderr", "stdout"], ["stderr", "stdout"]]));
    assert.equal(shape(countingEnv({ PATH: process.env.PATH }, dir)), plain);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test("Nur das erste Wort eines Befehls ist die ausführbare Datei", () => {
  assert.equal(isBrowserCommand("npx playwright install chromium"), false);
  assert.equal(isBrowserCommand("chromium --headless"), true);
  assert.equal(isBrowserCommand("/c/ms-playwright/chromium-1194/chrome-mac/Chromium.app/Contents/MacOS/Chromium --headless"), true);
});
