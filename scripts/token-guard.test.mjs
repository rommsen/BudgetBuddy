// design-system-wfhxj: Tests für den Token-Wächter. Die Fixture-Bäume liegen in
// temporären Verzeichnissen, nur der Live-Test läuft gegen den echten src/Client.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

import { findViolations, run } from "./token-guard.mjs";

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
    'prop.className "text-neon-teal bg-surface from-neon-orange shadow-glow-orange"',
    'prop.className "min-h-[36px] w-[420px] md:w-[268px] tracking-[0.16em] h-[42px]"',
    'prop.className "text-xs text-text-muted md:text-sm"',
    'prop.className "bg-neon-green/10 border-border-default"',
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

test("obj/ und bin/ werden nicht geprüft", () => {
  const fx = makeFixture({ "obj/Gen.fs": 'let v = "text-red-500"\n', "bin/Gen.fs": 'let v = "text-red-500"\n' });
  assert.equal(runGuard(fx).code, 0);
});

test("live: der echte src/Client-Baum ist sauber (Exit 0)", () => {
  const res = spawnSync("node", [scriptPath], { encoding: "utf8" });
  assert.equal(res.status, 0, res.stdout + res.stderr);
});
