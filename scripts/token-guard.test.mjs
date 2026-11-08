// design-system-wfhxj: Tests für den Token-Wächter. Die Fixture-Bäume liegen in
// temporären Verzeichnissen, nur der Live-Test läuft gegen den echten src/Client.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import {
  findCssColorLiterals,
  findHtmlColorLiterals,
  findViolations,
  offlineTokenMismatches,
  run,
} from "./token-guard.mjs";

const scriptPath = path.join(path.dirname(fileURLToPath(import.meta.url)), "token-guard.mjs");

/** Baut ein Fixture-Repo: <root>/src/Client/<relPath> mit Inhalt, plus Ausnahmeliste. */
function makeFixture(files, exceptions = []) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "token-guard-"));
  for (const [rel, content] of Object.entries(files)) {
    const full = path.join(root, "src", "Client", rel);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    fs.writeFileSync(full, content);
  }
  const exceptionsPath = path.join(root, "exceptions.json");
  fs.writeFileSync(exceptionsPath, typeof exceptions === "string" ? exceptions : JSON.stringify(exceptions));
  return { root, clientDir: path.join(root, "src", "Client"), exceptionsPath };
}

function runGuard(fx) {
  const out = [];
  const code = run(["--client-dir", fx.clientDir, "--exceptions", fx.exceptionsPath], (l) => out.push(l));
  return { code, out: out.join("\n") };
}

const CASES = [
  ["Hex-Arbitrary", 'prop.className "bg-[#0a0a0f]/80 p-2"', "hex-arbitrary"],
  ["rgba-Arbitrary im Schatten", 'prop.className "shadow-[0_0_25px_rgba(255,107,44,0.5)]"', "rgb-arbitrary"],
  ["Paletten-Farbe", 'prop.className "text-red-500"', "palette-farbe"],
  ["style.color", "prop.style [ style.color \"red\" ]", "inline-style"],
  ["style.fontSize", "prop.style [ style.fontSize 16 ]", "inline-style"],
  ["text-[13px]", 'prop.className "text-[13px] font-medium"', "text-arbitrary-size"],
];

for (const [label, line, pattern] of CASES) {
  test(`außerhalb von DesignSystem/: ${label} scheitert mit Exit 1 und nennt Datei und Zeile`, () => {
    const fx = makeFixture({ "Components/Foo/View.fs": `module Foo\n\nlet v =\n    ${line}\n` });
    const { code, out } = runGuard(fx);
    assert.equal(code, 1);
    assert.match(out, /src\/Client\/Components\/Foo\/View\.fs:4/);
    assert.match(out, new RegExp(`\\[${pattern}\\]`));
    assert.match(out, /Optik kommt aus standards\/frontend\/styleguide\.md und src\/Client\/DesignSystem\//);
  });

  test(`unter DesignSystem/: ${label} scheitert ohne Ausnahme und besteht mit Eintrag`, () => {
    const files = { "DesignSystem/Thing.fs": `module Thing\n\nlet v =\n    ${line}\n` };
    const without = runGuard(makeFixture(files));
    assert.equal(without.code, 1);
    assert.match(without.out, /src\/Client\/DesignSystem\/Thing\.fs:4/);

    const withEntry = runGuard(
      makeFixture(files, [
        { file: "src/Client/DesignSystem/Thing.fs", pattern, count: 1, reason: "Definitionsstelle im Test" },
      ])
    );
    assert.equal(withEntry.code, 0, withEntry.out);
  });
}

test("Ausnahme ohne passenden Treffer ist veraltet und scheitert", () => {
  const fx = makeFixture({ "DesignSystem/Thing.fs": 'let v = "p-2"\n' }, [
    { file: "src/Client/DesignSystem/Thing.fs", pattern: "hex-arbitrary", count: 1, reason: "alt" },
  ]);
  const { code, out } = runGuard(fx);
  assert.equal(code, 1);
  assert.match(out, /veraltete Ausnahme/);
});

test("Ausnahme mit zu kleiner Anzahl lässt weitere Treffer nicht durch", () => {
  const fx = makeFixture(
    { "DesignSystem/Thing.fs": 'let a = "bg-[#111111]"\nlet b = "bg-[#222222]"\n' },
    [{ file: "src/Client/DesignSystem/Thing.fs", pattern: "hex-arbitrary", count: 1, reason: "nur einer" }]
  );
  assert.equal(runGuard(fx).code, 1);
});

test("Token-Klassen und nicht-farbige Arbitrary-Werte lösen keinen Treffer aus", () => {
  const source = [
    'prop.className "text-accent bg-surface from-neon-orange shadow-glow-orange"',
    'prop.className "min-h-[36px] w-[420px] md:w-[268px] tracking-[0.16em] h-[42px]"',
    'prop.className "text-xs text-text-muted md:text-sm"',
    'prop.className "bg-success/10 border-border-default"',
  ].join("\n");
  assert.deepEqual(findViolations(source), []);
});

test("Kommentarzeilen zählen nicht", () => {
  assert.deepEqual(findViolations('// früher: bg-[#0a0a0f] und style.fontSize 16\n'), []);
});

test("Ausnahmeliste ungültig: Exit 2 (der Wächter kann nicht urteilen)", () => {
  const fx = makeFixture({ "Components/A.fs": "let x = 1\n" }, "{kein json");
  assert.equal(runGuard(fx).code, 2);
  const fx2 = makeFixture({ "Components/A.fs": "let x = 1\n" }, [{ file: "x.fs", pattern: "gibt-es-nicht", count: 1, reason: "r" }]);
  assert.equal(runGuard(fx2).code, 2);
  const fx3 = makeFixture({ "Components/A.fs": "let x = 1\n" }, [{ file: "x.fs", pattern: "hex-arbitrary", count: 1, reason: "" }]);
  assert.equal(runGuard(fx3).code, 2);
});

// ADR 0087: die entfallenen Farben stehen nirgends unter src/Client/, auch nicht im DesignSystem und auch nicht
// über die Ausnahmeliste. Regression für Fix 5850313 (Pink als Warnung auf der Budget-Seite über
// `Badge.warning`/`ErrorDisplay.warning`): der Fall "DS-Baustein setzt Pink" ist die Zeile in DesignSystem/Badge.fs.
// Gegenbeweis: dieselbe Zeile als Kommentar besteht.
const REMOVED_COLOR_LINES = [
  "prop.className Tokens.Text.neonPink",
  'prop.className "text-neon-pink"',
  "Icons.view Icons.Alert Icons.NeonPink",
  "Stats.statCard { Accent = Stats.Pink }",
  '| Warning -> "bg-neon-pink/10 text-neon-pink border-neon-pink/30"',
  'prop.className "bg-neon-orange"',
  "Tokens.Colors.neonPurple",
  'prop.className "text-calm-violet"',
];
for (const line of REMOVED_COLOR_LINES) {
  test(`überall unter src/Client: ${line} scheitert als entfallene-farbe, als Kommentar nicht`, () => {
    for (const rel of ["Components/Budget/View.fs", "Components/Ledger/View.fs", "DesignSystem/Badge.fs"]) {
      const hit = runGuard(makeFixture({ [rel]: `module View\n\nlet v =\n    ${line}\n` }));
      assert.equal(hit.code, 1, hit.out);
      assert.match(hit.out, new RegExp(`src/Client/${rel.replace(/[./]/g, "\\$&")}:4\\s+\\[entfallene-farbe\\]`));
    }
    const comment = runGuard(makeFixture({ "DesignSystem/Badge.fs": `module View\n\n// ${line}\n` }));
    assert.equal(comment.code, 0, comment.out);
  });
}

test("entfallene-farbe lässt sich im DesignSystem nicht per Ausnahmeliste erlauben", () => {
  const fx = makeFixture(
    { "DesignSystem/Badge.fs": 'let v = "text-neon-pink"\n' },
    [{ file: "src/Client/DesignSystem/Badge.fs", pattern: "entfallene-farbe", count: 1, reason: "r" }]
  );
  assert.equal(runGuard(fx).code, 2, "die Ausnahmeliste kennt das Muster nicht");
});

// design-system-dak16: Farbliterale in styles.css außerhalb von @theme und :root. Regression für den Fund aus
// Iteration 1: `.info-banner.warning` zeichnete mit `rgba(255, 138, 36, …)` Orange als Warnung, obwohl es seit
// ADR 0087 kein Orange mehr gibt.
test("css-farbliteral: ein Orange in einer Klasse wird gemeldet, in @theme und :root nicht", () => {
  const css = [
    "@theme {",
    "  --color-attention: #cdb27a;",
    "}",
    ":root {",
    "  --bg-app: #08081a;",
    "}",
    "@media (min-width: 768px) {",
    "  :root { --x: #ffffff; }",
    "}",
    ".info-banner.warning {",
    "  background: rgba(255, 138, 36, 0.08);",
    "  border-color: #ff8a24;",
    "  color: hsl(30, 100%, 50%);",
    "}",
  ].join("\n");
  assert.deepEqual(findCssColorLiterals(css).map((h) => [h.line, h.match]), [
    [11, "rgba(255, 138, 36, 0.08)"],
    [12, "#ff8a24"],
    [13, "hsl(30, 100%, 50%)"],
  ]);
});

test("css-farbliteral: Tokens, color-mix, Schwarz für Schatten, Kommentare und Daten-URLs sind erlaubt", () => {
  const css = [
    "/* früher: rgba(255, 138, 36, 0.08) und #ff6b2c */",
    ".a { background: color-mix(in srgb, var(--color-attention) 8%, transparent); }",
    ".b { box-shadow: 0 2px 8px rgba(0, 0, 0, 0.45); background: rgba(0,0,0,.6); }",
    ".c { background-image: url(\"data:image/svg+xml,%3Csvg stroke='%23a3a3bd'/%3E\"); color: currentColor; }",
    ".d { color: var(--text-primary); }",
  ].join("\n");
  assert.deepEqual(findCssColorLiterals(css), []);
});

test("css-farbliteral: der Lauf scheitert mit Datei und Zeile, wenn styles.css ein Farbliteral trägt", () => {
  const fx = makeFixture({ "styles.css": ":root { --a: #000; }\n.x { color: rgba(255, 138, 36, 0.25); }\n" });
  const r = runGuard(fx);
  assert.equal(r.code, 1, r.out);
  assert.match(r.out, /src\/Client\/styles\.css:2\s+\[css-farbliteral\]/);
});

// Review 88e6e9e3: Farbnamen und die neueren Farbfunktionen fielen durch `css-farbliteral`.
test("css-farbliteral: Farbnamen und oklch/oklab/lab/lch/hwb/color() werden gemeldet", () => {
  const css = [
    ".info-banner.warning { color: orange }",
    ".a { background: color-mix(in oklab, Red 10%, transparent); }",
    ".b { color: oklch(0.7 0.1 30); border-color: oklab(0.5 0.1 0.1); }",
    ".c { fill: lab(50 20 30); stroke: lch(50 20 30); outline-color: hwb(10 20% 30%); }",
    ".d { --x: color(display-p3 1 0 0); }",
    ".e { color: white; }",
  ].join("\n");
  assert.deepEqual(findCssColorLiterals(css).map((h) => [h.line, h.match]), [
    [1, "orange"],
    [2, "Red"],
    [3, "oklch(0.7 0.1 30)"],
    [3, "oklab(0.5 0.1 0.1)"],
    [4, "lab(50 20 30)"],
    [4, "lch(50 20 30)"],
    [4, "hwb(10 20% 30%)"],
    [5, "color(display-p3 1 0 0)"],
    [6, "white"],
  ]);
});

test("css-farbliteral: Schlüsselwörter, Druckblock und Wörter außerhalb von Farbwerten sind erlaubt", () => {
  const css = [
    ".red-banner, #orange > .pink:hover { color: transparent; fill: currentColor; stroke: inherit; }",
    ".a { border-color: initial; background: unset; }",
    "@media print { body { background: white; color: black; } }",
    ".b { content: \"red\"; font-family: 'Orange Sans', sans-serif; animation: tomato 1s; grid-area: navy; }",
    ".c { background-image: url(\"data:image/svg+xml,%3Csvg fill='red'/%3E\"); }",
    "/* früher: color: orange */",
    ".d { color-scheme: dark; transition: color 0.2s, background-color 0.2s; }",
    "@keyframes gold { from { opacity: 0 } }",
  ].join("\n");
  assert.deepEqual(findCssColorLiterals(css), []);
});

test("css-farbliteral: white und black außerhalb des Druckblocks werden gemeldet", () => {
  const css = "@media screen { body { color: black; } }\n.x { background: white; }\n";
  assert.deepEqual(findCssColorLiterals(css).map((h) => h.match), ["black", "white"]);
});

// Review 88e6e9e3: offline.html hatte eigene Hex-Werte und wurde von keinem Wächter gelesen.
const STYLES = [
  "@theme { --color-accent: #28d6df; --color-success: #5be479; }",
  ":root { --bg-app: #08081a; --text-primary: #e8e8f0; }",
].join("\n");
const offline = (rootBody, rest = "") =>
  [
    '<meta name="theme-color" content="#08081a">',
    "<!-- Farben: --accent #ff0000 -->",
    "<style>",
    `  :root { ${rootBody} }`,
    `  ${rest}`,
    "</style>",
  ].join("\n");

test("offline.html: Literale außerhalb ihres :root werden gemeldet, Kommentare nicht", () => {
  const html = offline("--accent: #28d6df;", ".retry { color: #ff6b2c; background: var(--accent); }") +
    '\n<p style="color: orange">x</p>';
  assert.deepEqual(findHtmlColorLiterals(html).map((h) => [h.line, h.match]), [
    [5, "#ff6b2c"],
    [7, "orange"],
  ]);
});

test("offline.html: :root und theme-color stimmen mit styles.css überein, eine Abweichung wird gemeldet", () => {
  const same = offline("--bg-app: #08081a; --text-primary: #E8E8F0; --accent: #28d6df; --success: #5be479; --font-mono: 'X';");
  assert.deepEqual(offlineTokenMismatches(same, STYLES), []);

  const drift = offline("--accent: #ff6b2c; --lila: #c9a7ff;").replace('content="#08081a"', 'content="#000000"');
  assert.deepEqual(offlineTokenMismatches(drift, STYLES).map((h) => [h.line, h.match]), [
    [4, "--accent: #ff6b2c (styles.css --color-accent: #28d6df)"],
    [4, "--lila: #c9a7ff (kein --color-lila in styles.css)"],
    [1, "theme-color: #000000 (styles.css --bg-app: #08081a)"],
  ]);
});

test("offline.html: der Lauf scheitert mit Datei und Zeile", () => {
  const fx = makeFixture({ "styles.css": STYLES, "public/offline.html": offline("--accent: #ff6b2c;") });
  const r = runGuard(fx);
  assert.equal(r.code, 1, r.out);
  assert.match(r.out, /src\/Client\/public\/offline\.html:4\s+\[offline-abgleich\]\s+--accent/);
});

// Review 88e6e9e3: die Flag-Farben waren als Rolle frei nutzbar. Erlaubt sind sie nur dort, wo das
// Flag-Symbol gezeichnet wird (ADR 0087 Punkt 8).
test("flag-farbe: in einer Budget-View gemeldet, in Badge.lenaMark nicht, in einer anderen Badge-Funktion schon", () => {
  const budget = runGuard(makeFixture({ "Components/Budget/View.fs": 'module V\n\nlet v =\n    prop.className "text-flag-orange"\n' }));
  assert.equal(budget.code, 1, budget.out);
  assert.match(budget.out, /src\/Client\/Components\/Budget\/View\.fs:4\s+\[flag-farbe\]\s+text-flag-orange/);

  const badge = [
    "module Badge",
    "",
    "let lenaMark (text: string) =",
    '    Html.span [ prop.className "text-flag-purple bg-(--color-flag-red)" ]',
    "",
    "let warning text =",
    '    Html.span [ prop.className "bg-flag-purple" ]',
  ].join("\n");
  const ds = runGuard(makeFixture({ "DesignSystem/Badge.fs": badge }));
  assert.equal(ds.code, 1, ds.out);
  assert.match(ds.out, /Badge\.fs:7\s+\[flag-farbe\]\s+bg-flag-purple/);
  assert.doesNotMatch(ds.out, /Badge\.fs:4/);
});

test("flag-farbe: in styles.css nur in @theme und den Regeln für data-lena-flag und .lena-flag-icon", () => {
  const css = [
    "@theme { --color-flag-red: #ff6b6b; }",
    '[data-lena-flag="red"] { --lena-flag: var(--color-flag-red); }',
    ".lena-flag-icon { color: var(--color-flag-red); }",
    ".budget-warn { color: var(--color-flag-red); }",
    ".x { @apply text-flag-red; }",
  ].join("\n");
  assert.deepEqual(findCssColorLiterals(css).map((h) => [h.line, h.pattern, h.match]), [
    [4, "flag-farbe", "--color-flag-red"],
    [5, "flag-farbe", "text-flag-red"],
  ]);
});

test("obj/ und bin/ werden nicht geprüft", () => {
  const fx = makeFixture({ "obj/Gen.fs": 'let v = "text-red-500"\n', "bin/Gen.fs": 'let v = "text-red-500"\n' });
  assert.equal(runGuard(fx).code, 0);
});

test("live: der echte src/Client-Baum ist sauber (Exit 0)", () => {
  const res = spawnSync("node", [scriptPath], { encoding: "utf8" });
  assert.equal(res.status, 0, res.stdout + res.stderr);
});
