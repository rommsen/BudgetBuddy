// design-system-thtpp: Unit-Tests für die reinen Extraktions-/
// Klassifizierungsfunktionen aus tailwind-class-guard.mjs. Die Autorität über
// "ist das eine echte Tailwind-Utility?" bleibt beim echten Compiler
// (@tailwindcss/node, siehe TailwindClassGuardTests.fs für den Subprozess-
// Aufruf des ganzen Skripts gegen den echten src/Client-Baum): hier geht es
// nur um die textbasierte Extraktion/Auflösung selbst.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import {
  extractClassNameTokens,
  extractTokensModuleLiterals,
  extractStylesheetClassNames,
  isKnownMarkerClass,
  isPrintfFormatSpecifier,
  loadExceptions,
  checkCodebase,
} from "./tailwind-class-guard.mjs";

// ============================================================================
// extractClassNameTokens
// ============================================================================

test("extractClassNameTokens: einfaches Literal wird an Whitespace zerlegt", () => {
  const source = 'Html.div [ prop.className "flex items-center gap-2" ]';
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => t.text),
    ["flex", "items-center", "gap-2"]
  );
  assert.ok(tokens.every((t) => t.dynamic === false));
});

test("extractClassNameTokens: statische Nachbar-Tokens neben einem dynamischen Interpolationsloch bleiben einzeln geprüft", () => {
  // Regressionsfall aus der Aufgabenbeschreibung: ein naiver Ansatz, der den
  // ganzen String-Literal als EIN Stück behandelt, würde die drei statischen
  // Tokens neben {Tokens.Borders.subtle} mit ausnehmen.
  const source = 'prop.className $"md:w-[268px] md:shrink-0 {Tokens.Borders.subtle}"';
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => ({ text: t.text, dynamic: t.dynamic })),
    [
      { text: "md:w-[268px]", dynamic: false },
      { text: "md:shrink-0", dynamic: false },
      { text: "{Tokens.Borders.subtle}", dynamic: true },
    ]
  );
});

test("extractClassNameTokens: ein Interpolationsloch mit eigenem Leerzeichen (Funktionsaufruf) bleibt EIN Token", () => {
  const source = 'prop.className $"{sizeToTextClass size} {colorToClass color}"';
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => t.text),
    ["{sizeToTextClass size}", "{colorToClass color}"]
  );
  assert.ok(tokens.every((t) => t.dynamic));
});

test("extractClassNameTokens: zwei direkt aneinandergrenzende Interpolationslöcher ohne Leerzeichen werden als zwei Segmente erkannt", () => {
  const source = 'prop.className $"{rowClasses}{expandedClass}"';
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => t.text),
    ["{rowClasses}", "{expandedClass}"]
  );
});

test("extractClassNameTokens: geklammerter Ausdruck mit mehreren Literalen (if/then/else) erfasst beide Zweige", () => {
  const source =
    'prop.className (if props.Value = "" then "text-text-muted/70" else "text-text-primary truncate")';
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => t.text),
    ["text-text-muted/70", "text-text-primary", "truncate"]
  );
});

test("extractClassNameTokens: geklammerter Ausdruck ohne jedes Literal wird als ein dynamisches Token übernommen", () => {
  const source = "prop.className (sizeToClass size)";
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(tokens, [{ text: "sizeToClass size", dynamic: true }]);
});

test("extractClassNameTokens: nackter Bezeichner (kein Literal, keine Klammer) wird als dynamisches Token übernommen", () => {
  const source = "prop.className Presets.pageHeader";
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(tokens, [{ text: "Presets.pageHeader", dynamic: true }]);
});

test("extractClassNameTokens: mehrere prop.className-Aufrufe in derselben Quelle werden alle gefunden", () => {
  const source = [
    'Html.div [ prop.className "a b" ]',
    'Html.span [ prop.className "c" ]',
  ].join("\n");
  const tokens = extractClassNameTokens(source);
  assert.deepEqual(
    tokens.map((t) => t.text),
    ["a", "b", "c"]
  );
});

// ============================================================================
// extractStylesheetClassNames
// ============================================================================

test("extractStylesheetClassNames: einfacher Top-Level-Selektor", () => {
  const css = ".foo { color: red; }";
  assert.ok(extractStylesheetClassNames(css).has("foo"));
});

test("extractStylesheetClassNames: zusammengesetzter Selektor (.a.b) liefert BEIDE Klassen", () => {
  const css = ".test-result.match { color: green; }";
  const classes = extractStylesheetClassNames(css);
  assert.ok(classes.has("test-result"));
  assert.ok(classes.has("match"));
});

test("extractStylesheetClassNames: Nachfahren-Selektor (.a .b) liefert BEIDE Klassen", () => {
  const css = ".filter-btn .count { font-weight: bold; }";
  const classes = extractStylesheetClassNames(css);
  assert.ok(classes.has("filter-btn"));
  assert.ok(classes.has("count"));
});

test("extractStylesheetClassNames: Klassen innerhalb eines @media-Blocks werden gefunden", () => {
  const css = "@media (min-width: 768px) {\n  .desktop-only { display: block; }\n}";
  assert.ok(extractStylesheetClassNames(css).has("desktop-only"));
});

test("extractStylesheetClassNames: Kommentare werden ignoriert (keine Fund aus auskommentiertem Selektor allein zählt gegen die Erwartung)", () => {
  const css = "/* .commented-out { color: red; } */\n.real { color: blue; }";
  const classes = extractStylesheetClassNames(css);
  assert.ok(classes.has("real"));
});

test("extractStylesheetClassNames: eine Dezimalzahl in einer @media-Bedingung erzeugt keinen falschen Klassenfund", () => {
  // Ein Punkt gefolgt von einer Ziffer (z. B. in "47.9375em") ist keine
  // CSS-Klasse: die Erkennung verlangt einen Buchstaben/Unterstrich direkt
  // nach dem Punkt.
  const css = "@media (min-width: 47.9375em) {\n  .real { color: blue; }\n}";
  const classes = extractStylesheetClassNames(css);
  assert.ok(classes.has("real"));
  assert.equal(classes.size, 1);
});

// ============================================================================
// isKnownMarkerClass
// ============================================================================

test("isKnownMarkerClass: group/peer und benannte Varianten sind bekannte Marker-Klassen", () => {
  assert.ok(isKnownMarkerClass("group"));
  assert.ok(isKnownMarkerClass("peer"));
  assert.ok(isKnownMarkerClass("group/item"));
  assert.ok(isKnownMarkerClass("peer/label"));
});

test("isKnownMarkerClass: eine beliebige andere Klasse ist keine Marker-Klasse", () => {
  assert.equal(isKnownMarkerClass("flex"), false);
  assert.equal(isKnownMarkerClass("grouping"), false);
});

// ============================================================================
// isPrintfFormatSpecifier
// ============================================================================

test("isPrintfFormatSpecifier: %s ist ein Formatplatzhalter, keine Klasse", () => {
  assert.ok(isPrintfFormatSpecifier("%s"));
});

test("isPrintfFormatSpecifier: eine echte Klasse ist kein Formatplatzhalter", () => {
  assert.equal(isPrintfFormatSpecifier("status-label"), false);
});

// ============================================================================
// extractTokensModuleLiterals
// ============================================================================

test("extractTokensModuleLiterals: extrahiert let-Literale je Top-Level-Modul, qualifiziert mit dem Modulnamen", () => {
  const fakeTokens = [
    "module Client.DesignSystem.Tokens",
    "",
    "module Colors =",
    '    let neonGreen = "text-neon-green"',
    "",
    "module FontSizes =",
    '    let micro = "text-[10px]"',
  ].join("\n");

  const map = extractTokensModuleLiterals(fakeTokens);
  assert.equal(map.get("Colors.neonGreen"), "text-neon-green");
  assert.equal(map.get("FontSizes.micro"), "text-[10px]");
});

test("extractTokensModuleLiterals: eine Funktion (kein direktes String-Literal) wird nicht als Literal aufgenommen", () => {
  const fakeTokens = [
    "module Client.DesignSystem.Tokens",
    "",
    "module StaggerDelays =",
    '    let stagger1 = "stagger-1"',
    "",
    "    let forIndex (i: int) =",
    "        match i % 10 with",
    "        | 0 -> stagger1",
    "        | _ -> stagger1",
  ].join("\n");

  const map = extractTokensModuleLiterals(fakeTokens);
  assert.equal(map.get("StaggerDelays.stagger1"), "stagger-1");
  assert.equal(map.has("StaggerDelays.forIndex"), false);
});

// ============================================================================
// loadExceptions
// ============================================================================

test("loadExceptions: liefert ein leeres Array, wenn die Datei nicht existiert", () => {
  const missingPath = path.join(os.tmpdir(), `does-not-exist-${Date.now()}.json`);
  assert.deepEqual(loadExceptions(missingPath), []);
});

test("loadExceptions: liest ein vorhandenes JSON-Array", () => {
  const tmpPath = path.join(os.tmpdir(), `tailwind-guard-exceptions-test-${Date.now()}.json`);
  fs.writeFileSync(tmpPath, JSON.stringify([{ token: "foo", reason: "test", locations: [] }]));
  try {
    const exceptions = loadExceptions(tmpPath);
    assert.equal(exceptions.length, 1);
    assert.equal(exceptions[0].token, "foo");
  } finally {
    fs.rmSync(tmpPath);
  }
});

test("loadExceptions: wirft, wenn die Datei kein JSON-Array ist", () => {
  const tmpPath = path.join(os.tmpdir(), `tailwind-guard-exceptions-bad-${Date.now()}.json`);
  fs.writeFileSync(tmpPath, JSON.stringify({ notAnArray: true }));
  try {
    assert.throws(() => loadExceptions(tmpPath));
  } finally {
    fs.rmSync(tmpPath);
  }
});

// ============================================================================
// checkCodebase: veraltete Ausnahmeliste-Einträge (mechanisierte Durchsetzung)
// ============================================================================

function makeScratchDir(prefix) {
  return fs.mkdtempSync(path.join(os.tmpdir(), prefix));
}

test("checkCodebase: ein Ausnahmeliste-Eintrag, dessen Token der aktuelle Scan nicht mehr findet, wird als veraltet gemeldet", async () => {
  const repoRoot = path.resolve(import.meta.dirname, "..");
  const scratchClientDir = makeScratchDir("tailwind-guard-stale-exception-");
  const exceptionsPath = path.join(scratchClientDir, "exceptions.json");

  try {
    fs.writeFileSync(
      path.join(scratchClientDir, "Clean.fs"),
      'module Clean\n\nopen Feliz\n\nlet view () = Html.div [ prop.className "flex" ]\n'
    );
    fs.writeFileSync(
      exceptionsPath,
      JSON.stringify([{ token: "this-token-never-appears", reason: "test", locations: [] }])
    );

    const result = await checkCodebase({
      clientDir: scratchClientDir,
      stylesCssPath: path.join(repoRoot, "src", "Client", "styles.css"),
      tokensFsPath: path.join(repoRoot, "src", "Client", "DesignSystem", "Tokens.fs"),
      exceptionsPath,
    });

    assert.equal(result.undefinedTokens.length, 0);
    assert.equal(result.staleExceptions.length, 1);
    assert.equal(result.staleExceptions[0].token, "this-token-never-appears");
  } finally {
    fs.rmSync(scratchClientDir, { recursive: true, force: true });
  }
});

test("checkCodebase: eine per Ausnahmeliste erlaubte undefinierte Klasse erscheint nicht in undefinedTokens", async () => {
  const repoRoot = path.resolve(import.meta.dirname, "..");
  const scratchClientDir = makeScratchDir("tailwind-guard-allowed-exception-");
  const exceptionsPath = path.join(scratchClientDir, "exceptions.json");

  try {
    fs.writeFileSync(
      path.join(scratchClientDir, "Excepted.fs"),
      'module Excepted\n\nopen Feliz\n\nlet view () = Html.div [ prop.className "flex bb-phantom-nicht-real" ]\n'
    );
    fs.writeFileSync(
      exceptionsPath,
      JSON.stringify([{ token: "bb-phantom-nicht-real", reason: "test", locations: [] }])
    );

    const result = await checkCodebase({
      clientDir: scratchClientDir,
      stylesCssPath: path.join(repoRoot, "src", "Client", "styles.css"),
      tokensFsPath: path.join(repoRoot, "src", "Client", "DesignSystem", "Tokens.fs"),
      exceptionsPath,
    });

    assert.equal(result.undefinedTokens.length, 0);
    assert.equal(result.staleExceptions.length, 0);
  } finally {
    fs.rmSync(scratchClientDir, { recursive: true, force: true });
  }
});

test("checkCodebase: eine echte Phantom-Klasse ohne Ausnahmeliste-Eintrag wird als undefiniert gemeldet", async () => {
  const repoRoot = path.resolve(import.meta.dirname, "..");
  const scratchClientDir = makeScratchDir("tailwind-guard-phantom-");

  try {
    fs.writeFileSync(
      path.join(scratchClientDir, "Phantom.fs"),
      'module Phantom\n\nopen Feliz\n\nlet view () = Html.div [ prop.className "flex bb-phantom-nicht-real" ]\n'
    );

    const result = await checkCodebase({
      clientDir: scratchClientDir,
      stylesCssPath: path.join(repoRoot, "src", "Client", "styles.css"),
      tokensFsPath: path.join(repoRoot, "src", "Client", "DesignSystem", "Tokens.fs"),
      exceptionsPath: path.join(scratchClientDir, "no-such-exceptions.json"),
    });

    assert.equal(result.undefinedTokens.length, 1);
    assert.equal(result.undefinedTokens[0].token, "bb-phantom-nicht-real");
  } finally {
    fs.rmSync(scratchClientDir, { recursive: true, force: true });
  }
});
