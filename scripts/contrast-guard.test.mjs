// scripts/contrast-guard.test.mjs
//
// Kontrast-Wächter für design-system-dak16 (ADR 0087 Punkt 6). Liest die Farbwerte aus
// src/Client/styles.css und rechnet nach WCAG 2 (relative Leuchtdichte, (L1 + 0,05) / (L2 + 0,05)).
// Jeder `--color-*` aus `@theme` steht in genau einer Klasse (CLASSES unten), sonst ist der Lauf rot:
//   - Fläche (`--color-surface-*`): Hintergrund, gegen den alle anderen Klassen geprüft werden,
//   - Text/Rolle (`--color-text-*` und die Rollenfarben): mindestens 4,5:1 gegen jede Fläche,
//   - Rand (border-strong, Rand eines Bedienelements, Fokusring): mindestens 3:1 gegen jede Fläche,
//   - Flag (`--color-flag-*`, nur im Flag-Symbol, ADR 0087 Punkt 8): mindestens 3:1 wie ein Bedienelement,
//   - bewusst ausgenommen, je Token mit Grund.
// Dazu: der Text auf dem Akzent (`on-accent`, also bg-app) hat mindestens 4,5:1 auf dem Akzent, und jeder
// geprüfte Wert (auch die `:root`-Werte, auf die ein Token per `var()` zeigt) ist genau einmal deklariert.
// Eine zweite Deklaration (etwa in `@media` oder `[data-…]`) würde der Wächter sonst nicht sehen.
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

const stripComments = (css) => css.replace(/\/\*[\s\S]*?\*\//g, "");

/** Alle Deklarationen `--name: wert;` aus `@theme` und `:root`, die erste zählt (weitere meldet `redeclared`). */
export function readCustomProperties(css) {
  const props = new Map();
  for (const m of stripComments(css).matchAll(/(--[a-z0-9-]+)\s*:\s*([^;{}]+);/gi)) {
    if (!props.has(m[1])) props.set(m[1], m[2].trim());
  }
  return props;
}

/** Die `--color-*`-Namen aus dem `@theme`-Block. */
export function themeColorTokens(css) {
  const body = stripComments(css).match(/@theme\b[^{]*\{([^}]*)\}/);
  if (!body) throw new Error("styles.css hat keinen @theme-Block");
  return [...body[1].matchAll(/(--color-[a-z0-9-]+)\s*:/gi)].map((m) => m[1]);
}

/** Die Kette der Namen, über die `name` per `var()` aufgelöst wird, einschließlich `name`. */
export function resolutionChain(props, name) {
  const chain = [name];
  for (;;) {
    const ref = props.get(chain.at(-1))?.match(/^var\((--[a-z0-9-]+)\)$/i);
    if (!ref || chain.includes(ref[1])) return chain;
    chain.push(ref[1]);
  }
}

/** Namen aus `names`, die in `css` mehr als einmal deklariert sind: [{ name, count }]. */
export function redeclared(css, names) {
  const counts = new Map();
  for (const m of stripComments(css).matchAll(/(?<![\w-])(--[a-z0-9-]+)\s*:/gi)) {
    counts.set(m[1], (counts.get(m[1]) ?? 0) + 1);
  }
  return [...new Set(names)].filter((n) => (counts.get(n) ?? 0) > 1).map((n) => ({ name: n, count: counts.get(n) }));
}

/**
 * Ordnet jeden Token einer Klasse zu. Eine Klasse hat `prefix` (alle Tokens mit dem Präfix) oder `tokens`
 * (feste Liste). Ergebnis: { byClass: Map<klasse, token[]>, unclassified: token[], ambiguous: token[] }.
 */
export function classify(tokens, classes) {
  const byClass = new Map(Object.keys(classes).map((k) => [k, []]));
  const unclassified = [];
  const ambiguous = [];
  for (const t of tokens) {
    const hits = Object.entries(classes).filter(
      ([, c]) => (c.prefix && t.startsWith(c.prefix)) || (c.tokens && t in c.tokens)
    );
    if (hits.length === 0) unclassified.push(t);
    else if (hits.length > 1) ambiguous.push(`${t} (${hits.map(([k]) => k).join(", ")})`);
    else byClass.get(hits[0][0]).push(t);
  }
  return { byClass, unclassified, ambiguous };
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

// Ein `tokens`-Eintrag ist { token: Grund }. Der Grund steht nur bei den Ausnahmen in der Meldung, er hält
// aber auch fest, warum ein Token in seiner Klasse steht.
const CLASSES = {
  flaeche: { prefix: "--color-surface-" },
  textRolle: {
    prefix: "--color-text-",
    tokens: {
      "--color-accent": "Akzent: Hauptaktion und Auswahl",
      "--color-attention": "Aufmerksamkeit",
      "--color-error": "Fehler",
      "--color-success": "Erfolg",
      "--color-info": "Hinweis",
    },
  },
  rand: { tokens: { "--color-border-strong": "Rand eines Bedienelements und Fokusring (WCAG 1.4.11)" } },
  flag: { prefix: "--color-flag-" },
  ausgenommen: {
    tokens: {
      "--color-border-default": "Trennlinie zwischen Flächen, kein Bedienelement (WCAG 1.4.11 gilt nicht)",
      "--color-border-subtle": "Trennlinie zwischen Flächen, kein Bedienelement (WCAG 1.4.11 gilt nicht)",
      "--color-on-accent": "steht nur auf dem Akzent, eigener Test unten",
    },
  },
};

const css = fs.readFileSync(stylesPath, "utf8");
const props = readCustomProperties(css);
const themeTokens = themeColorTokens(css);
const { byClass, unclassified, ambiguous } = classify(themeTokens, CLASSES);
const SURFACES = byClass.get("flaeche");
const show = (list) => list.map((f) => `${f.fg} auf ${f.bg}: ${f.ratio}:1`).join("\n");

test("Jeder --color-* aus @theme steht in genau einer Klasse", () => {
  assert.deepEqual(unclassified, [], `unklassifiziert (in CLASSES eintragen): ${unclassified.join(", ")}`);
  assert.deepEqual(ambiguous, [], `in mehr als einer Klasse: ${ambiguous.join(", ")}`);
  const stale = Object.values(CLASSES).flatMap((c) => Object.keys(c.tokens ?? {})).filter((t) => !themeTokens.includes(t));
  assert.deepEqual(stale, [], "in CLASSES, aber nicht in @theme");
  for (const k of ["flaeche", "textRolle", "rand", "flag"]) assert.ok(byClass.get(k).length > 0, `Klasse ${k} ist leer`);
});

test("Jeder geprüfte Farbwert ist in styles.css genau einmal deklariert", () => {
  const checked = themeTokens.flatMap((t) => resolutionChain(props, t));
  const twice = redeclared(css, checked);
  assert.deepEqual(twice, [], twice.map((d) => `${d.name} ${d.count}-mal deklariert`).join("\n"));
});

test("Text- und Rollenfarben haben mindestens 4,5:1 gegen jede Fläche", () => {
  const bad = failures(props, byClass.get("textRolle"), SURFACES, 4.5);
  assert.deepEqual(bad, [], show(bad));
});

test("Ränder von Bedienelementen haben mindestens 3:1 gegen jede Fläche", () => {
  const bad = failures(props, byClass.get("rand"), SURFACES, 3);
  assert.deepEqual(bad, [], show(bad));
});

test("Der Text auf dem Akzent (on-accent = bg-app) hat mindestens 4,5:1", () => {
  assert.equal(resolveHex(props, "--color-on-accent"), resolveHex(props, "--bg-app"), "on-accent ist bg-app");
  const bad = failures(props, ["--color-on-accent"], ["--color-accent"], 4.5);
  assert.deepEqual(bad, [], show(bad));
});

test("Die Flag-Farben haben im Flag-Symbol mindestens 3:1 gegen jede Fläche", () => {
  const bad = failures(props, byClass.get("flag"), SURFACES, 3);
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

test("Gegenprobe: ein neuer Token ohne Klasse und eine zweite Deklaration in @media werden gefunden", () => {
  const probe = [
    "@theme { --color-surface-card: var(--bg-card); --color-text-muted: var(--text-muted); --color-probe: #ff6b2c; }",
    "/* --text-muted: #000; zählt nicht */",
    ":root { --bg-card: #111128; --text-muted: #8787a5; }",
    "@media (min-width: 1px) { :root { --text-muted: #555; } }",
  ].join("\n");
  assert.deepEqual(classify(themeColorTokens(probe), CLASSES).unclassified, ["--color-probe"]);
  const p = readCustomProperties(probe);
  assert.deepEqual(resolutionChain(p, "--color-text-muted"), ["--color-text-muted", "--text-muted"]);
  assert.deepEqual(redeclared(probe, themeColorTokens(probe).flatMap((t) => resolutionChain(p, t))), [
    { name: "--text-muted", count: 2 },
  ]);
});
