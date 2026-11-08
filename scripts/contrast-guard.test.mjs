// scripts/contrast-guard.test.mjs
//
// Kontrast-Wächter für design-system-dak16 (ADR 0087 Punkt 6). Liest die Farbwerte aus
// src/Client/styles.css und rechnet nach WCAG 2 (relative Leuchtdichte, (L1 + 0,05) / (L2 + 0,05)):
//   - jede Text- und Rollenfarbe mindestens 4,5:1 gegen bg-card, bg-elevated und bg-hover,
//   - border-strong (Rand eines Bedienelements, Fokusring) mindestens 3:1 gegen dieselben Flächen,
//   - der Text auf dem Akzent (`on-accent`, also bg-app) mindestens 4,5:1 auf dem Akzent,
//   - die Flag-Farben (nur im Flag-Symbol, ADR 0087 Punkt 8) mindestens 3:1 wie ein Bedienelement.
// Ein neuer Token, der eine dieser Grenzen unterschreitet, macht den Lauf rot.
//
//   node --test scripts/contrast-guard.test.mjs
//
// Braucht keinen Browser und keinen Vite-Build.

import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const stylesPath = path.join(here, "..", "src", "Client", "styles.css");

/** Alle Deklarationen `--name: wert;` aus `@theme` und `:root`, spätere überschreiben frühere nicht. */
export function readCustomProperties(css) {
  const props = new Map();
  for (const m of css.matchAll(/(--[a-z0-9-]+)\s*:\s*([^;{}]+);/gi)) {
    if (!props.has(m[1])) props.set(m[1], m[2].trim());
  }
  return props;
}

/** Löst `var(--x)` auf, bis ein Hex-Wert dasteht. Wirft, wenn keiner herauskommt. */
export function resolveHex(props, name) {
  let value = props.get(name);
  const seen = new Set([name]);
  while (value !== undefined) {
    const hex = value.match(/^#([0-9a-f]{6})$/i);
    if (hex) return `#${hex[1].toLowerCase()}`;
    const ref = value.match(/^var\((--[a-z0-9-]+)\)$/i);
    if (!ref || seen.has(ref[1])) break;
    seen.add(ref[1]);
    value = props.get(ref[1]);
  }
  throw new Error(`${name} hat keinen sechsstelligen Hex-Wert in styles.css (gefunden: ${props.get(name)})`);
}

const channel = (c) => {
  const s = c / 255;
  return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
};

/** Relative Leuchtdichte nach WCAG 2. */
export function luminance(hex) {
  const n = parseInt(hex.slice(1), 16);
  const [r, g, b] = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map(channel);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** Kontrastverhältnis zweier Hex-Farben nach WCAG 2. */
export function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

/** Alle Paare unter `min`: [{ fg, bg, ratio }]. */
export function failures(props, foregrounds, backgrounds, min) {
  const out = [];
  for (const fg of foregrounds) {
    for (const bg of backgrounds) {
      const ratio = contrast(resolveHex(props, fg), resolveHex(props, bg));
      if (ratio < min) out.push({ fg, bg, ratio: Math.round(ratio * 100) / 100 });
    }
  }
  return out;
}

const SURFACES = ["--bg-card", "--bg-elevated", "--bg-hover"];
const TEXT_AND_ROLES = [
  "--text-primary",
  "--text-secondary",
  "--text-muted",
  "--color-accent",
  "--color-attention",
  "--color-error",
  "--color-success",
  "--color-info",
];
const FLAGS = ["red", "orange", "yellow", "green", "blue", "purple"].map((c) => `--color-flag-${c}`);

const props = readCustomProperties(fs.readFileSync(stylesPath, "utf8"));
const show = (list) => list.map((f) => `${f.fg} auf ${f.bg}: ${f.ratio}:1`).join("\n");

test("Text- und Rollenfarben haben mindestens 4,5:1 gegen bg-card, bg-elevated und bg-hover", () => {
  const bad = failures(props, TEXT_AND_ROLES, SURFACES, 4.5);
  assert.deepEqual(bad, [], show(bad));
});

test("border-strong hat mindestens 3:1 gegen bg-card, bg-elevated und bg-hover", () => {
  const bad = failures(props, ["--color-border-strong"], SURFACES, 3);
  assert.deepEqual(bad, [], show(bad));
});

test("Der Text auf dem Akzent (on-accent = bg-app) hat mindestens 4,5:1", () => {
  assert.equal(resolveHex(props, "--color-on-accent"), resolveHex(props, "--bg-app"), "on-accent ist bg-app");
  const bad = failures(props, ["--color-on-accent"], ["--color-accent"], 4.5);
  assert.deepEqual(bad, [], show(bad));
});

test("Die Flag-Farben haben im Flag-Symbol mindestens 3:1 gegen die Flächen", () => {
  const bad = failures(props, FLAGS, SURFACES, 3);
  assert.deepEqual(bad, [], show(bad));
});

// Gegenprobe: der Wächter rechnet richtig und meldet einen zu dunklen Wert.
test("Gegenprobe: border-strong als Textfarbe fällt auf bg-hover unter 4,5:1 und wird gemeldet", () => {
  const probe = new Map(props);
  probe.set("--color-probe", "#6a6c86");
  const bad = failures(probe, ["--color-probe"], ["--bg-hover"], 4.5);
  assert.equal(bad.length, 1, "der zu dunkle Wert muss gemeldet werden");
  assert.ok(bad[0].ratio < 4.5 && bad[0].ratio > 3, `Verhältnis ${bad[0].ratio}`);
});

test("Gegenprobe: bekannte Verhältnisse nach WCAG 2", () => {
  assert.equal(Math.round(contrast("#ffffff", "#000000") * 100) / 100, 21);
  // Weiß auf dem alten neon-orange (#ff6b2c): 2,84:1 laut Recherche farbschema-neon-dark-2026-11-07.
  assert.equal(Math.round(contrast("#ffffff", "#ff6b2c") * 100) / 100, 2.84);
  assert.throws(() => resolveHex(new Map([["--x", "var(--y)"]]), "--x"), /keinen sechsstelligen Hex-Wert/);
});
