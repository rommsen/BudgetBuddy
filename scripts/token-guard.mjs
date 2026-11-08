#!/usr/bin/env node
// design-system-wfhxj: Token-Wächter. Feature-Code nutzt keine Farb- oder
// Schriftwerte außerhalb der Tokens (standards/frontend/styleguide.md, §2/§3).
//
// Geprüft werden alle .fs unter src/Client/ (ohne obj/ und bin/). Verboten sind:
//   - Arbitrary-Werte mit Farbe (`bg-[#0a0a0f]/80`, `shadow-[0_0_25px_rgba(...)]`)
//   - Standard-Tailwind-Paletten (`text-red-500`, `bg-gray-100`)
//   - Inline-Stile `style.color`, `style.backgroundColor`, `style.borderColor`,
//     `style.fontSize`
//   - Arbitrary-Schriftgrößen `text-[13px]`, `text-[1.2rem]`
//   - überall unter src/Client/, auch im DesignSystem und ohne Ausnahme, die entfallenen Farben (ADR 0087):
//     Pink, Orange, Lila und calm-violet (`neon-pink`, `neonPink`, `NeonPink`, `Stats.Pink`, `neon-orange`, …).
//     Es gibt dafür keinen Token mehr; jede Farbe hat eine Bedeutung auf jeder Seite. Das deckt auch den Bug
//     aus Fix 5850313: damals kam Pink als Warnung über `Badge.warning` und `ErrorDisplay.warning` auf die
//     Budget-Seite, ohne dass deren Quelltext "pink" nannte. Heute ist Pink in keinem DS-Baustein erlaubt.
//   - in src/Client/styles.css Farbliterale (Hex, rgb/rgba, hsl/hsla, oklch, oklab, lab, lch, hwb, color()
//     und CSS-Farbnamen wie `orange`) außerhalb der Token-Definitionen `@theme { … }` und `:root { … }`
//     (Muster `css-farbliteral`, design-system-dak16): dort laufen Farben über `var(--…)` oder `color-mix`
//     auf einen Token. Erlaubt bleiben reines Schwarz mit Deckkraft (`rgba(0, 0, 0, a)`) für Schatten und
//     Scrims, das keine Farbrolle ist, und `white`/`black` im `@media print`-Block. Keine Ausnahmeliste.
//   - dieselben Literale in src/Client/public/offline.html außerhalb ihres eigenen `:root`; dessen Farben
//     und `<meta name="theme-color">` müssen den gleichnamigen Werten aus styles.css entsprechen
//     (`--x` gleich `--x` oder `--color-x`, theme-color gleich `--bg-app`; Muster `offline-abgleich`).
//   - Flag-Farben (`text-flag-orange`, `--color-flag-red`, Muster `flag-farbe`, ADR 0087 Punkt 8) außer dort,
//     wo das Flag-Symbol gezeichnet wird: `Badge.lenaMark` und die styles.css-Regeln für `[data-lena-flag]`
//     und `.lena-flag-icon`.
//
// Außerhalb von src/Client/DesignSystem/ gilt null Toleranz. Innerhalb (der
// Definitionsschicht) sind Treffer nur erlaubt, wenn sie in
// scripts/token-guard-exceptions.json mit Datei, Muster, Anzahl und Grund stehen.
// Ein Eintrag ohne passenden Treffer (oder mit abweichender Anzahl) ist veraltet
// und lässt den Lauf scheitern.
//
// Exit 0 = sauber, 1 = Treffer oder veraltete Ausnahme, 2 = der Wächter kann
// nicht urteilen (Ausnahmeliste ungültig, Verzeichnis fehlt).

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..");

export const HINT =
  "Optik kommt aus standards/frontend/styleguide.md und src/Client/DesignSystem/, nicht aus Tafel-Werten";

const PALETTE =
  "red|green|blue|gray|slate|zinc|neutral|stone|orange|amber|yellow|lime|emerald|teal|cyan|sky|indigo|violet|purple|fuchsia|pink|rose";
const PREFIX =
  "text|bg|border|ring|from|to|via|fill|stroke|shadow|outline|divide|placeholder|accent|caret|decoration";

// Flag-Farben (ADR 0087 Punkt 8): als Klasse (`text-flag-orange`) oder Variable (`--color-flag-orange`).
const FLAG_TOKEN = new RegExp(
  `(?:--color-|(?<![\\w-])(?:${PREFIX})-)flag-(?:red|orange|yellow|green|blue|purple)(?![\\w-])`,
  "g"
);

// Reihenfolge ist die Reihenfolge der Meldung. Jedes Muster hat einen Namen,
// über den die Ausnahmeliste es anspricht.
export const PATTERNS = [
  {
    name: "hex-arbitrary",
    re: /-\[[^\]]*#[0-9a-fA-F]{3,8}[^\]]*\]/g,
  },
  {
    name: "rgb-arbitrary",
    re: /-\[[^\]]*(?:rgba?|hsla?)\([^\]]*\]/g,
  },
  {
    name: "palette-farbe",
    re: new RegExp(`(?<![\\w-])(?:${PREFIX})-(?:${PALETTE})-\\d{2,3}(?![\\w-])`, "g"),
  },
  {
    name: "inline-style",
    re: /\bstyle\.(?:color|backgroundColor|borderColor|fontSize)\b/g,
  },
  {
    name: "text-arbitrary-size",
    re: /(?<![\w-])text-\[\d*\.?\d+(?:px|rem)\]/g,
  },
];

// Muster, die nur unter einem Ordner von src/Client/ gelten (`dir: ""` heißt überall). Sie haben keine
// Ausnahmen, auch nicht im DesignSystem: die Ausnahmeliste kennt nur die Namen aus PATTERNS.
export const SCOPED_PATTERNS = [
  {
    name: "entfallene-farbe",
    dir: "",
    re: /neon-(?:pink|orange|purple)|[Nn]eon(?:Pink|Orange|Purple)|calm-violet|[Cc]almViolet|\b(?:Stats\.)?Pink\b/g,
  },
  {
    // ADR 0087 Punkt 8: die Flag-Farben gibt es nur im Flag-Symbol. Im F#-Code also nur in `Badge.lenaMark`,
    // das das Symbol zeichnet; in styles.css nur in den Regeln für `[data-lena-flag]` und `.lena-flag-icon`
    // (findCssColorLiterals). Sonst wäre `text-flag-orange` eine frei nutzbare siebte Rollenfarbe.
    name: "flag-farbe",
    dir: "",
    re: FLAG_TOKEN,
    allowedIn: (rel, binding) => rel === "DesignSystem/Badge.fs" && binding === "lenaMark",
  },
];

/** Die Muster für eine Datei, `rel` relativ zu src/Client/ mit `/`. */
export const patternsFor = (rel) => [...PATTERNS, ...SCOPED_PATTERNS.filter((p) => rel.startsWith(p.dir))];

const TOP_LEVEL_LET = /^let\s+(?:(?:private|inline|rec|mutable)\s+)*([A-Za-z_][\w']*)/;

/**
 * Alle Treffer einer Quelle: [{ line, pattern, match }]. Kommentarzeilen (`//`) zählen nicht.
 * `rel` (relativ zu src/Client/) braucht nur ein Muster mit `allowedIn`; es bekommt die Datei und den Namen
 * der umgebenden Bindung auf Spalte 0 (`let lenaMark …`).
 */
export function findViolations(source, patterns = PATTERNS, rel = "") {
  const hits = [];
  const lines = source.split(/\r?\n/);
  let binding = null;
  lines.forEach((line, idx) => {
    const top = line.match(TOP_LEVEL_LET);
    if (top) binding = top[1];
    else if (/^(?:module|type|and|\[<|#)/.test(line)) binding = null;
    if (line.trimStart().startsWith("//")) return;
    for (const { name, re, allowedIn } of patterns) {
      if (allowedIn && allowedIn(rel, binding)) continue;
      re.lastIndex = 0;
      for (const m of line.matchAll(re)) {
        hits.push({ line: idx + 1, pattern: name, match: m[0] });
      }
    }
  });
  return hits;
}

// CSS-Farbnamen (CSS Color 4) ohne `transparent` und `currentColor`, die keine Farbe setzen.
const CSS_NAMED_COLORS = (
  "aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue blueviolet brown " +
  "burlywood cadetblue chartreuse chocolate coral cornflowerblue cornsilk crimson cyan darkblue darkcyan " +
  "darkgoldenrod darkgray darkgreen darkgrey darkkhaki darkmagenta darkolivegreen darkorange darkorchid darkred " +
  "darksalmon darkseagreen darkslateblue darkslategray darkslategrey darkturquoise darkviolet deeppink " +
  "deepskyblue dimgray dimgrey dodgerblue firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite gold " +
  "goldenrod gray green greenyellow grey honeydew hotpink indianred indigo ivory khaki lavender lavenderblush " +
  "lawngreen lemonchiffon lightblue lightcoral lightcyan lightgoldenrodyellow lightgray lightgreen lightgrey " +
  "lightpink lightsalmon lightseagreen lightskyblue lightslategray lightslategrey lightsteelblue lightyellow lime " +
  "limegreen linen magenta maroon mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen " +
  "mediumslateblue mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream mistyrose moccasin " +
  "navajowhite navy oldlace olive olivedrab orange orangered orchid palegoldenrod palegreen paleturquoise " +
  "palevioletred papayawhip peachpuff peru pink plum powderblue purple rebeccapurple red rosybrown royalblue " +
  "saddlebrown salmon sandybrown seagreen seashell sienna silver skyblue slateblue slategray slategrey snow " +
  "springgreen steelblue tan teal thistle tomato turquoise violet wheat white whitesmoke yellow yellowgreen"
).split(" ");
const CSS_COLOR_LITERAL =
  /#[0-9a-fA-F]{3,8}(?![0-9a-zA-Z_-])|(?<![\w-])(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb|color)\([^)]*\)/gi;
const CSS_COLOR_NAME = new RegExp(`(?<![\\w-])(?:${CSS_NAMED_COLORS.join("|")})(?![\\w-])`, "gi");
const NEUTRAL_BLACK = /^rgba?\(\s*0\s*,?\s*0\s*,?\s*0\s*(?:[,/]\s*[\d.]+%?\s*)?\)$/i;
// Eigenschaften, deren Werte Namen sind (Schrift, Animation, Gitterflächen, Zähler) und keine Farben.
const NAME_VALUED_PROPERTY =
  /^(?:font|font-family|font-feature-settings|font-variation-settings|animation|animation-name|content|quotes|grid-template-areas|grid-template|grid-area|grid-row|grid-column|transition|transition-property|will-change|counter-reset|counter-increment|counter-set|list-style-type|container|container-name|view-transition-name)$/i;
const TOKEN_BLOCK = /^(?:@theme\b[\s\S]*|:root)$/;
const PRINT_BLOCK = /^@media\s+print\b/i;
const FLAG_BLOCK = /\[data-lena-flag\b|\.lena-flag-icon(?![\w-])/;

/** Ersetzt jedes Zeichen außer Zeilenumbrüchen durch ein Leerzeichen (Offsets und Zeilen bleiben gleich). */
const blank = (s) => s.replace(/[^\n]/g, " ");

/**
 * Zerlegt CSS in Anweisungen (Deklaration oder @-Anweisung) mit den Köpfen der umgebenden Blöcke.
 * Kommentare sind vorher zu leeren; Zeichenketten und Klammern trennen nicht.
 * @returns {{ prop: string|null, value: string, valueOffset: number, frames: string[] }[]}
 */
function cssStatements(css) {
  const out = [];
  const frames = [];
  let start = 0;
  let quote = null;
  let parens = 0;
  const emit = (end) => {
    const text = css.slice(start, end);
    if (text.trim() === "") return;
    const lead = text.length - text.trimStart().length;
    const colon = text.indexOf(":");
    if (text.trimStart().startsWith("@") || colon < 0) {
      out.push({ prop: null, value: text, valueOffset: start, frames: [...frames] });
    } else {
      out.push({
        prop: text.slice(lead, colon).trim(),
        value: text.slice(colon + 1),
        valueOffset: start + colon + 1,
        frames: [...frames],
      });
    }
  };
  for (let i = 0; i < css.length; i++) {
    const ch = css[i];
    if (quote) {
      if (ch === "\\") i++;
      else if (ch === quote) quote = null;
    } else if (ch === '"' || ch === "'") quote = ch;
    else if (ch === "(") parens++;
    else if (ch === ")") parens = Math.max(0, parens - 1);
    else if (parens === 0 && ch === "{") {
      frames.push(css.slice(start, i).trim().replace(/\s+/g, " "));
      start = i + 1;
    } else if (parens === 0 && ch === ";") {
      emit(i);
      start = i + 1;
    } else if (parens === 0 && ch === "}") {
      emit(i);
      frames.pop();
      start = i + 1;
    }
  }
  emit(css.length);
  return out;
}

/** Leert Zeichenketten und `url(…)` in einem Wert, damit Daten-URLs und `content:`-Texte nicht zählen. */
const stripStringsAndUrls = (value) =>
  value.replace(/url\((?:[^()"']|"[^"]*"|'[^']*')*\)|"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'/gi, blank);

function lineAt(source) {
  const starts = [0];
  for (let i = 0; i < source.length; i++) if (source[i] === "\n") starts.push(i + 1);
  return (offset) => {
    let lo = 0;
    let hi = starts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (starts[mid] <= offset) lo = mid;
      else hi = mid - 1;
    }
    return lo + 1;
  };
}

/** Prüft schon kommentarfreies CSS (gleiche Länge wie die Quelle). */
function scanCss(cleaned) {
  const hits = [];
  const line = lineAt(cleaned);
  for (const st of cssStatements(cleaned)) {
    const inToken = st.frames.some((f) => TOKEN_BLOCK.test(f));
    const inTheme = st.frames.some((f) => /^@theme\b/.test(f));
    const inPrint = st.frames.some((f) => PRINT_BLOCK.test(f));
    const value = stripStringsAndUrls(st.value);
    const add = (pattern, m) => hits.push({ line: line(st.valueOffset + m.index), pattern, match: m[0] });
    if (!inToken) {
      for (const m of value.matchAll(CSS_COLOR_LITERAL)) {
        if (!NEUTRAL_BLACK.test(m[0])) add("css-farbliteral", m);
      }
      if (st.prop !== null && !NAME_VALUED_PROPERTY.test(st.prop)) {
        for (const m of value.matchAll(CSS_COLOR_NAME)) {
          if (inPrint && /^(?:white|black)$/i.test(m[0])) continue;
          add("css-farbliteral", m);
        }
      }
    }
    if (!inTheme && !st.frames.some((f) => FLAG_BLOCK.test(f))) {
      for (const m of value.matchAll(FLAG_TOKEN)) add("flag-farbe", m);
    }
  }
  return hits.sort((a, b) => a.line - b.line);
}

/**
 * Farbliterale in einem Stylesheet außerhalb von `@theme { … }` und `:root { … }`: [{ line, pattern, match }].
 * Gemeldet werden Hex, rgb/rgba, hsl/hsla, oklch, oklab, lab, lch, hwb, color() und CSS-Farbnamen in Werten.
 * Kommentare, Zeichenketten, `url(…)`, Selektoren und Eigenschaften mit Namenswerten (Schrift, Animation …)
 * zählen nicht. Erlaubt bleiben reines Schwarz mit Deckkraft (Schatten, Scrims) und `white`/`black` im
 * `@media print`-Block. Dazu meldet es Flag-Farben (`flag-farbe`) außerhalb von `@theme` und den Regeln für
 * `[data-lena-flag]` und `.lena-flag-icon`.
 */
export function findCssColorLiterals(css) {
  return scanCss(css.replace(/\/\*[\s\S]*?(?:\*\/|$)/g, blank));
}

/**
 * Dasselbe für eine HTML-Seite (src/Client/public/offline.html): geprüft werden die `<style>`-Blöcke und
 * `style="…"`-Attribute, HTML-Kommentare zählen nicht. Die Farbe in `<meta name="theme-color">` prüft
 * `offlineTokenMismatches`.
 */
export function findHtmlColorLiterals(html) {
  const noComments = html.replace(/<!--[\s\S]*?(?:-->|$)/g, blank);
  const chars = blank(noComments).split("");
  const keep = (from, to) => {
    for (let i = from; i < to; i++) chars[i] = noComments[i];
  };
  for (const m of noComments.matchAll(/(<style\b[^>]*>)([\s\S]*?)<\/style>/gi)) {
    const from = m.index + m[1].length;
    keep(from, from + m[2].length);
  }
  for (const m of noComments.matchAll(/\sstyle\s*=\s*(["'])([\s\S]*?)\1/gi)) {
    const open = m.index + m[0].indexOf(m[1]);
    keep(open + 1, open + 1 + m[2].length);
    chars[open] = "{";
    chars[open + 1 + m[2].length] = "}";
  }
  return findCssColorLiterals(chars.join(""));
}

/** Erste Deklaration je Custom Property in `@theme` und `:root` (ohne Medienblöcke) eines Stylesheets. */
function tokenValues(css) {
  const cleaned = css.replace(/\/\*[\s\S]*?(?:\*\/|$)/g, blank);
  const props = new Map();
  for (const st of cssStatements(cleaned)) {
    if (st.frames.length !== 1 || !TOKEN_BLOCK.test(st.frames[0]) || !st.prop?.startsWith("--")) continue;
    if (!props.has(st.prop)) props.set(st.prop, st.value.trim());
  }
  return props;
}

function resolveToken(props, name) {
  let value = props.get(name);
  const seen = new Set([name]);
  for (;;) {
    const ref = value?.match(/^var\((--[\w-]+)\)$/);
    if (!ref || seen.has(ref[1])) return value;
    seen.add(ref[1]);
    value = props.get(ref[1]);
  }
}

const IS_COLOR_VALUE = new RegExp(
  `^(?:#[0-9a-f]{3,8}|(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb|color)\\(.*\\)|${CSS_NAMED_COLORS.join("|")})$`,
  "i"
);

/**
 * Gleicht die Farbwerte im `:root` der Offline-Seite mit styles.css ab: `--x` entspricht `--x` oder
 * `--color-x` dort, `<meta name="theme-color">` entspricht `--bg-app`. [{ line, pattern, match }]
 */
export function offlineTokenMismatches(html, css) {
  const props = tokenValues(css);
  const noComments = html.replace(/<!--[\s\S]*?(?:-->|$)/g, blank);
  const line = lineAt(noComments);
  const hits = [];
  const check = (offset, name, value, target) => {
    const expected = resolveToken(props, target);
    if (expected === undefined) {
      hits.push({ line: line(offset), pattern: "offline-abgleich", match: `${name}: ${value} (kein ${target} in styles.css)` });
    } else if (expected.toLowerCase() !== value.toLowerCase()) {
      hits.push({ line: line(offset), pattern: "offline-abgleich", match: `${name}: ${value} (styles.css ${target}: ${expected})` });
    }
  };
  for (const m of noComments.matchAll(/(<style\b[^>]*>)([\s\S]*?)<\/style>/gi)) {
    const base = m.index + m[1].length;
    for (const st of cssStatements(m[2])) {
      if (st.frames.length !== 1 || st.frames[0] !== ":root" || !st.prop?.startsWith("--")) continue;
      const value = st.value.trim();
      if (!IS_COLOR_VALUE.test(value)) continue;
      const target = props.has(st.prop) ? st.prop : `--color-${st.prop.slice(2)}`;
      check(base + st.valueOffset, st.prop, value, target);
    }
  }
  for (const m of noComments.matchAll(/<meta\b[^>]*name\s*=\s*["']theme-color["'][^>]*>/gi)) {
    const content = m[0].match(/content\s*=\s*["']([^"']*)["']/i);
    if (content) check(m.index, "theme-color", content[1].trim(), "--bg-app");
  }
  return hits;
}

function listFsFiles(dir) {
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === "obj" || entry.name === "bin") continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...listFsFiles(full));
    else if (entry.isFile() && entry.name.endsWith(".fs")) out.push(full);
  }
  return out.sort();
}

export class GuardError extends Error {}

/** Lädt und validiert die Ausnahmeliste. Wirft GuardError (Exit 2). */
export function loadExceptions(exceptionsPath) {
  let raw;
  try {
    raw = fs.readFileSync(exceptionsPath, "utf8");
  } catch (err) {
    throw new GuardError(`Ausnahmeliste nicht lesbar: ${exceptionsPath} (${err.message})`);
  }
  let parsed;
  try {
    parsed = JSON.parse(raw);
  } catch (err) {
    throw new GuardError(`Ausnahmeliste ist kein gültiges JSON: ${exceptionsPath} (${err.message})`);
  }
  if (!Array.isArray(parsed)) throw new GuardError("Ausnahmeliste muss ein Array sein");
  const known = new Set(PATTERNS.map((p) => p.name));
  for (const [i, e] of parsed.entries()) {
    const ok =
      e &&
      typeof e.file === "string" &&
      known.has(e.pattern) &&
      Number.isInteger(e.count) &&
      e.count > 0 &&
      typeof e.reason === "string" &&
      e.reason.trim().length > 0;
    if (!ok) {
      throw new GuardError(
        `Ausnahmeliste Eintrag ${i} ungültig: erwartet { file, pattern (${[...known].join("|")}), count > 0, reason }`
      );
    }
  }
  return parsed;
}

/**
 * Prüft einen Client-Baum.
 * @returns {{violations: object[], staleExceptions: object[]}}
 */
export function checkTree({ clientDir, exceptions, relativeTo = clientDir }) {
  if (!fs.existsSync(clientDir) || !fs.statSync(clientDir).isDirectory()) {
    throw new GuardError(`Client-Verzeichnis nicht gefunden: ${clientDir}`);
  }
  const dsDir = path.join(clientDir, "DesignSystem") + path.sep;
  const violations = [];
  const seen = new Map(); // "file|pattern" -> count (nur DesignSystem)

  for (const file of listFsFiles(clientDir)) {
    const rel = path.relative(relativeTo, file).split(path.sep).join("/");
    const inDs = file.startsWith(dsDir);
    const inClient = path.relative(clientDir, file).split(path.sep).join("/");
    for (const hit of findViolations(fs.readFileSync(file, "utf8"), patternsFor(inClient), inClient)) {
      if (inDs) {
        const key = `${rel}|${hit.pattern}`;
        seen.set(key, (seen.get(key) ?? 0) + 1);
        violations.push({ file: rel, ...hit, inDs: true });
      } else {
        violations.push({ file: rel, ...hit, inDs: false });
      }
    }
  }

  const relOf = (file) => path.relative(relativeTo, file).split(path.sep).join("/");
  const stylesPath = path.join(clientDir, "styles.css");
  const styles = fs.existsSync(stylesPath) ? fs.readFileSync(stylesPath, "utf8") : null;
  if (styles !== null) {
    for (const hit of findCssColorLiterals(styles)) violations.push({ file: relOf(stylesPath), ...hit, inDs: false });
  }
  // Die Offline-Seite kann styles.css nicht laden (sie erscheint ohne Server) und kopiert die Farben in ihr
  // eigenes :root. Literale außerhalb davon sind verboten, die Kopien müssen styles.css entsprechen.
  const offlinePath = path.join(clientDir, "public", "offline.html");
  if (fs.existsSync(offlinePath)) {
    const html = fs.readFileSync(offlinePath, "utf8");
    const hits = [...findHtmlColorLiterals(html), ...(styles === null ? [] : offlineTokenMismatches(html, styles))];
    for (const hit of hits) violations.push({ file: relOf(offlinePath), ...hit, inDs: false });
  }

  const allowed = new Map(exceptions.map((e) => [`${e.file}|${e.pattern}`, e]));
  const reported = violations.filter((v) => {
    if (!v.inDs) return true;
    const exc = allowed.get(`${v.file}|${v.pattern}`);
    return !(exc && seen.get(`${v.file}|${v.pattern}`) === exc.count);
  });

  const staleExceptions = exceptions.filter((e) => {
    const n = seen.get(`${e.file}|${e.pattern}`) ?? 0;
    return n !== e.count;
  }).map((e) => ({ ...e, actual: seen.get(`${e.file}|${e.pattern}`) ?? 0 }));

  return { violations: reported, staleExceptions };
}

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === "--client-dir") args.clientDir = argv[++i];
    else if (argv[i] === "--exceptions") args.exceptions = argv[++i];
  }
  return args;
}

export function run(argv, log = console.log) {
  const args = parseArgs(argv);
  const clientDir = path.resolve(args.clientDir ?? path.join(repoRoot, "src", "Client"));
  const exceptionsPath = path.resolve(args.exceptions ?? path.join(repoRoot, "scripts", "token-guard-exceptions.json"));
  let result;
  try {
    const exceptions = loadExceptions(exceptionsPath);
    result = checkTree({ clientDir, exceptions, relativeTo: repoRootFor(clientDir) });
  } catch (err) {
    if (err instanceof GuardError) {
      log(`Token-Wächter kann nicht urteilen: ${err.message}`);
      return 2;
    }
    throw err;
  }
  const { violations, staleExceptions } = result;
  let code = 0;
  if (violations.length > 0) {
    code = 1;
    log(`Token-Wächter: ${violations.length} Treffer`);
    for (const v of violations) {
      log(`  ${v.file}:${v.line}  [${v.pattern}]  ${v.match}`);
    }
    log(`\n${HINT}.`);
  }
  if (staleExceptions.length > 0) {
    code = 1;
    log(`Token-Wächter: ${staleExceptions.length} veraltete Ausnahme(n) (Anzahl weicht ab oder kein Treffer mehr)`);
    for (const e of staleExceptions) {
      log(`  ${e.file} [${e.pattern}] erwartet ${e.count}, gefunden ${e.actual}`);
    }
  }
  if (code === 0) log("Token-Wächter: keine Farb- oder Schriftwerte außerhalb der Tokens.");
  return code;
}

// Pfade in Meldung und Ausnahmeliste sind relativ zum Verzeichnis, das `src/Client` enthält
// (Repo-Wurzel), damit sie dem Eintrag `src/Client/DesignSystem/...` entsprechen.
function repoRootFor(clientDir) {
  return path.resolve(clientDir, "..", "..");
}

const isMainModule = process.argv[1] === fileURLToPath(import.meta.url);
if (isMainModule) {
  process.exit(run(process.argv.slice(2)));
}
