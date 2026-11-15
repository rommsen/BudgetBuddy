// scripts/panel-width-guard.test.mjs
//
// Wächter gegen feste Panelbreiten (design-system-gcpws, §7.6): der echte `src/Client/` ist sauber, und eine
// eingepflanzte Panelbreite macht ihn rot (Gegenbeweis).

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { EXCEPTIONS, findPanelWidths, scanClient } from "./panel-width-guard.mjs";

const clientDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "src", "Client");

test("src/Client hat keine feste Panelbreite, jede Ausnahme hat einen Treffer und einen Grund", () => {
  const { violations, stale } = scanClient(clientDir);
  assert.deepEqual(violations, [], "feste Panelbreite: die Breite kommt aus Tokens.PanelWidths (--panel-w)");
  assert.deepEqual(stale, [], "veraltete Ausnahme");
  for (const e of EXCEPTIONS) assert.ok(e.reason && e.reason.length > 10, `${e.file} ${e.cls}: Grund fehlt`);
});

test("erkennt w-, pr- und md:/lg:pr- von 300 bis 600 px, nicht max-w, min-w und Breiten außerhalb", () => {
  const text = [
    'prop.className "hidden lg:pr-[444px] md:pr-[420px]"',
    'prop.className $"w-[388px] pr-[420px] {x}"',
    'prop.className "w-[300px] w-[600px]"',
    'prop.className "max-w-[400px] min-w-[380px] w-[299px] w-[601px] w-[620px] pl-[420px] w-full"',
  ].join("\n");
  assert.deepEqual(
    findPanelWidths(text).map((h) => `${h.line} ${h.cls}`),
    ["1 lg:pr-[444px]", "1 md:pr-[420px]", "2 w-[388px]", "2 pr-[420px]", "3 w-[300px]", "3 w-[600px]"]
  );
});

test("Gegenbeweis: eine eingepflanzte Panelbreite in einer Kopie von src/Client macht den Wächter rot", () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "panel-width-guard-"));
  try {
    const copy = path.join(root, "Client");
    fs.cpSync(clientDir, copy, { recursive: true, filter: (src) => !/node_modules|[\\/](bin|obj)([\\/]|$)/.test(src) });
    assert.deepEqual(scanClient(copy).violations, [], "die Kopie ist sauber");
    fs.writeFileSync(path.join(copy, "Components", "Budget", "Planted.fs"), 'let x = Html.div [ prop.className "lg:pr-[444px] w-[388px]" ]\n');
    assert.deepEqual(scanClient(copy).violations, ["Components/Budget/Planted.fs:1: lg:pr-[444px]", "Components/Budget/Planted.fs:1: w-[388px]"]);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test("Gegenbeweis: eine Ausnahme ohne Treffer ist veraltet", () => {
  const { stale } = scanClient(clientDir, [...EXCEPTIONS, { file: "Components/Budget/View.fs", cls: "w-[400px]", reason: "nur für den Test" }]);
  assert.deepEqual(stale, ["Components/Budget/View.fs: w-[400px] (Ausnahme ohne Treffer)"]);
});
