#!/usr/bin/env node
// design-system-thtpp: Wächter gegen Klassennamen, die weder eine echte
// Tailwind-Utility sind noch über einen Selektor in styles.css definiert werden.
//
// Hintergrund: ADR 0031 fand sieben Stellen, an denen der Code DaisyUI-Klassen
// setzte, obwohl DaisyUI nie installiert war: eine fehlende Klasse wirft keinen
// Fehler, sie fehlt einfach lautlos in der Ausgabe. Dieser Wächter macht aus
// diesem stillen Ausfall eine laute Meldung, analog zum Datenbank-Wächter aus
// infra-za7fq.
//
// Bewusst als Node-Skript, nicht als F#-Nachbau der Tailwind-Logik: nur
// Tailwinds eigener Compiler (@tailwindcss/node, __unstable__loadDesignSystem)
// kann autoritativ beantworten, ob ein Klassenname eine echte Utility ist. Eine
// zweite, in F# nachgebaute Kopie dieser Logik wäre genau das Drift-Risiko, das
// der Kopf-Kommentar in src/Tests/FontFaceRegressionTests.fs für einen analogen
// Fall vermeidet.
//
// Die Klassen-Extraktion und -Auflösung ist bewusst rein regelbasiert (kein
// F#-Compiler, keine Typinferenz, keine Symbolauswertung): sie versteht drei
// Formen von `prop.className`-Argumenten (Literal, `$"..."`-Interpolation,
// geklammerter Ausdruck) und löst `{...}`-Interpolationslöcher sowie nackte
// Bezeichner in zwei Stufen auf: zuerst gegen die `let`-Literale aus
// src/Client/DesignSystem/Tokens.fs, danach (falls die Definitionsstelle selbst
// nur endlich viele literale Zweige hat, `match`/`function`/`if-then-else`,
// ggf. über mehrere Weiterleitungs-Hops) gegen die Datei, in der der
// Bezeichner selbst definiert ist. Was sich so nicht auflösen lässt, muss in
// scripts/tailwind-class-guard-exceptions.json stehen oder lässt den Lauf
// scheitern (siehe Akzeptanzkriterien der Aufgabe).

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, "..");

const MAX_RESOLUTION_DEPTH = 8;

// ============================================================================
// String-/Klammer-Scanning-Grundbausteine (F#-quotingsbewusst, kein Parser)
// ============================================================================

function isWhitespace(ch) {
  return ch === " " || ch === "\t" || ch === "\n" || ch === "\r";
}

/**
 * Liest ein einfaches F#-String-Literal ab dem öffnenden `"` bei `start`.
 * Unterstützt `\"`-Escapes. Gibt den Inhalt (ohne Anführungszeichen) und den
 * Index direkt nach dem schließenden `"` zurück.
 */
function readPlainString(source, start) {
  let i = start + 1;
  let content = "";
  while (i < source.length) {
    const ch = source[i];
    if (ch === "\\" && i + 1 < source.length) {
      content += ch + source[i + 1];
      i += 2;
      continue;
    }
    if (ch === '"') {
      return { content, endIndex: i + 1 };
    }
    content += ch;
    i += 1;
  }
  return { content, endIndex: i };
}

/**
 * Liest ein interpoliertes F#-String-Literal ab dem `$` bei `start`
 * (`source[start] === "$"`, `source[start + 1] === '"'`). `{...}`-Löcher
 * bleiben wörtlich im Inhalt erhalten (samt Anführungszeichen darin): ein
 * `"` innerhalb einer `{}`-Klammerung beendet das Literal nicht.
 */
function readInterpolatedString(source, start) {
  let i = start + 2;
  let content = "";
  let depth = 0;
  while (i < source.length) {
    const ch = source[i];
    if (ch === "\\" && depth === 0 && i + 1 < source.length) {
      content += ch + source[i + 1];
      i += 2;
      continue;
    }
    if (ch === "{") {
      depth += 1;
      content += ch;
      i += 1;
      continue;
    }
    if (ch === "}") {
      depth = Math.max(0, depth - 1);
      content += ch;
      i += 1;
      continue;
    }
    if (ch === '"' && depth === 0) {
      return { content, endIndex: i + 1 };
    }
    content += ch;
    i += 1;
  }
  return { content, endIndex: i };
}

/** Liest ein String-Literal (einfach oder interpoliert) ab `start`, je nachdem, ob `$` vorsteht. */
function readAnyString(source, start) {
  if (source[start] === "$" && source[start + 1] === '"') {
    return readInterpolatedString(source, start);
  }
  return readPlainString(source, start);
}

/**
 * Findet das schließende `)` zur öffnenden Klammer bei `start`
 * (`source[start] === "("`). String-Literale (inkl. darin enthaltener
 * Klammern) werden übersprungen, statt die Tiefe zu beeinflussen.
 */
function findMatchingParen(source, start) {
  let depth = 0;
  let i = start;
  while (i < source.length) {
    const ch = source[i];
    if (ch === '"' || (ch === "$" && source[i + 1] === '"')) {
      const stringStart = ch === "$" ? i : i;
      const { endIndex } = readAnyString(source, stringStart);
      i = endIndex;
      continue;
    }
    if (ch === "(") {
      depth += 1;
      i += 1;
      continue;
    }
    if (ch === ")") {
      depth -= 1;
      if (depth === 0) return i;
      i += 1;
      continue;
    }
    i += 1;
  }
  return -1;
}

/** Findet alle String-Literale (einfach oder interpoliert) innerhalb eines Textausschnitts. */
function extractAllStringLiteralsIn(text) {
  const literals = [];
  let i = 0;
  while (i < text.length) {
    const ch = text[i];
    if (ch === '"' || (ch === "$" && text[i + 1] === '"')) {
      const { content, endIndex } = readAnyString(text, i);
      literals.push(content);
      i = endIndex;
      continue;
    }
    i += 1;
  }
  return literals;
}

// ============================================================================
// extractClassNameTokens
// ============================================================================

const CLASSNAME_CALL_RE = /prop\.className/g;
const BARE_IDENT_RE = /^[A-Za-z0-9_.']+/;

/**
 * Findet jeden `prop.className`-Aufruf in einem F#-Quelltext und liefert eine
 * flache Liste von Klassen-Tokens zurück: `{ text, dynamic }`.
 *
 * - Ein Literal (`"..."`) oder eine Interpolation (`$"..."`) wird an
 *   Whitespace in einzelne Tokens zerlegt. Ein Token, das `{`/`}` enthält
 *   (ein Interpolationsloch), wird als `dynamic: true` markiert: `text`
 *   bleibt dabei das wörtliche Token samt Klammern (z. B. `{extraClass}`).
 * - Ein geklammerter Ausdruck (`prop.className (...)`) wird nach enthaltenen
 *   String-Literalen durchsucht (deren Tokens wie oben behandelt werden);
 *   enthält der Ausdruck kein einziges Literal, gilt der ganze getrimmte
 *   Ausdruck als ein einziges dynamisches Token.
 * - Ein nackter Bezeichner (`prop.className someIdent` /
 *   `prop.className Some.Module.member`) wird als ein dynamisches Token
 *   übernommen.
 */
export function extractClassNameTokens(fsSource) {
  const tokens = [];
  CLASSNAME_CALL_RE.lastIndex = 0;
  let m;
  while ((m = CLASSNAME_CALL_RE.exec(fsSource))) {
    let i = m.index + m[0].length;
    while (i < fsSource.length && isWhitespace(fsSource[i])) i += 1;

    if (fsSource[i] === '"' || (fsSource[i] === "$" && fsSource[i + 1] === '"')) {
      const { content, endIndex } = readAnyString(fsSource, i);
      pushLiteralTokens(tokens, content);
      CLASSNAME_CALL_RE.lastIndex = endIndex;
      continue;
    }

    if (fsSource[i] === "(") {
      const close = findMatchingParen(fsSource, i);
      const end = close === -1 ? fsSource.length : close + 1;
      const inner = fsSource.slice(i + 1, close === -1 ? fsSource.length : close);
      const literals = extractAllStringLiteralsIn(inner);
      if (literals.length === 0) {
        const exprText = inner.trim();
        if (exprText.length > 0) {
          tokens.push({ text: exprText, dynamic: true });
        }
      } else {
        for (const literal of literals) {
          pushLiteralTokens(tokens, literal);
        }
      }
      CLASSNAME_CALL_RE.lastIndex = end;
      continue;
    }

    const identMatch = BARE_IDENT_RE.exec(fsSource.slice(i));
    if (identMatch) {
      tokens.push({ text: identMatch[0], dynamic: true });
      CLASSNAME_CALL_RE.lastIndex = i + identMatch[0].length;
    } else {
      CLASSNAME_CALL_RE.lastIndex = i + 1;
    }
  }
  return tokens;
}

/**
 * Zerlegt Literal-Inhalt an Whitespace in Wörter, aber NICHT innerhalb eines
 * `{...}`-Interpolationslochs (ein Loch wie `{sizeToTextClass size}` enthält
 * selbst ein Leerzeichen und darf nicht an dieser Stelle zerschnitten werden).
 */
function splitRespectingBraces(content) {
  const words = [];
  let current = "";
  let depth = 0;
  for (const ch of content) {
    if (ch === "{") {
      depth += 1;
      current += ch;
      continue;
    }
    if (ch === "}") {
      depth = Math.max(0, depth - 1);
      current += ch;
      continue;
    }
    if (isWhitespace(ch) && depth === 0) {
      if (current.length > 0) {
        words.push(current);
        current = "";
      }
      continue;
    }
    current += ch;
  }
  if (current.length > 0) words.push(current);
  return words;
}

/**
 * Zerlegt ein einzelnes Wort in seine statischen und dynamischen Segmente.
 * Ein Wort wie `{rowClasses}{expandedClass}` (zwei direkt aneinandergrenzende
 * Interpolationslöcher ohne Leerzeichen dazwischen) wird in zwei unabhängig
 * auflösbare dynamische Segmente zerlegt statt als ein einziges, unauflösbares
 * Token behandelt zu werden.
 */
function splitWordIntoSegments(word) {
  const segments = [];
  let staticBuf = "";
  let i = 0;
  while (i < word.length) {
    if (word[i] === "{") {
      if (staticBuf.length > 0) {
        segments.push({ text: staticBuf, dynamic: false });
        staticBuf = "";
      }
      let depth = 1;
      let j = i + 1;
      while (j < word.length && depth > 0) {
        if (word[j] === "{") depth += 1;
        else if (word[j] === "}") depth -= 1;
        j += 1;
      }
      segments.push({ text: word.slice(i, j), dynamic: true });
      i = j;
      continue;
    }
    staticBuf += word[i];
    i += 1;
  }
  if (staticBuf.length > 0) segments.push({ text: staticBuf, dynamic: false });
  return segments;
}

function pushLiteralTokens(tokens, content) {
  const words = splitRespectingBraces(content);
  for (const word of words) {
    for (const segment of splitWordIntoSegments(word)) {
      tokens.push(segment);
    }
  }
}

// ============================================================================
// extractTokensModuleLiterals: Client/DesignSystem/Tokens.fs als Nachschlagewerk
// ============================================================================

const MODULE_LINE_RE = /^module\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*$/;
const LET_LITERAL_LINE_RE =
  /^\s{2,}let\s+(?:private\s+)?([A-Za-z_][A-Za-z0-9_']*)\s*=\s*(\$?"[^"]*")/;

/**
 * Liest src/Client/DesignSystem/Tokens.fs (oder einen äquivalenten Text) und
 * extrahiert jede `let name = "..."`-Bindung innerhalb eines Top-Level-Moduls
 * (`module X = ...`) als `"X.name" -> literalInhalt`-Eintrag. Bindungen ohne
 * direktes String-Literal (z. B. Funktionen wie `Tokens.fs`s `forIndex`)
 * werden übersprungen, sie sind kein fester Token-Wert.
 */
export function extractTokensModuleLiterals(tokensFsSource) {
  const map = new Map();
  const lines = tokensFsSource.replace(/\r\n/g, "\n").split("\n");
  let currentModule = null;

  for (const line of lines) {
    const moduleMatch = MODULE_LINE_RE.exec(line);
    if (moduleMatch) {
      currentModule = moduleMatch[1];
      continue;
    }
    if (currentModule === null) continue;

    const letMatch = LET_LITERAL_LINE_RE.exec(line);
    if (letMatch) {
      const [, name, quoted] = letMatch;
      const isInterpolated = quoted.startsWith("$");
      const { content } = isInterpolated
        ? readInterpolatedString(quoted, 0)
        : readPlainString(quoted, 0);
      map.set(`${currentModule}.${name}`, content);
    }
  }
  return map;
}

// ============================================================================
// extractStylesheetClassNames: jeder .bezeichner in jedem Selektor
// ============================================================================

// Kein Anker auf ein vorangehendes `}`/`;` (frühere Fassung: `(^|[};])\s*([^{}]+?)\s*\{`):
// ein verschachtelter Selektor direkt nach der öffnenden Klammer eines
// `@media`/`@supports`-Blocks hat KEIN `}`/`;` unmittelbar vor sich (nur
// Whitespace): der Anker verfehlte diesen Fall, siehe die Unit-Tests dazu.
// `[^{}]+?` schließt `{`/`}` ohnehin aus, der globale Scan braucht keinen
// zusätzlichen Anker, um korrekt vor jeder öffnenden Klammer zu resynchronisieren.
const CSS_PRELUDE_RE = /([^{}]+?)\{/g;
const CSS_CLASS_IN_SELECTOR_RE = /\.-?[_a-zA-Z][_a-zA-Z0-9-]*/g;

/**
 * Findet jeden `.bezeichner` in jedem Selektor von `css`, auch in
 * zusammengesetzten Selektoren (`.a.b`), Nachfahren-Selektoren (`.a .b`) und
 * innerhalb von `@media`/`@supports`-Blöcken, nicht nur an Spalte 0
 * verankerte Top-Level-Selektoren (siehe Stolperstein 1 der Aufgabe: ein
 * naiver Spalte-0-Parser erzeugte 9 falsch-positive Funde an diesem
 * Stylesheet). Gibt ein `Set<string>` der Klassennamen OHNE führenden Punkt
 * zurück.
 */
export function extractStylesheetClassNames(css) {
  const withoutComments = css.replace(/\/\*[\s\S]*?\*\//g, "");
  const classes = new Set();

  CSS_PRELUDE_RE.lastIndex = 0;
  let m;
  while ((m = CSS_PRELUDE_RE.exec(withoutComments))) {
    const prelude = m[1];
    CSS_CLASS_IN_SELECTOR_RE.lastIndex = 0;
    let cm;
    while ((cm = CSS_CLASS_IN_SELECTOR_RE.exec(prelude))) {
      classes.add(cm[0].slice(1));
    }
  }
  return classes;
}

// ============================================================================
// isKnownMarkerClass: group/peer (und benannte Varianten) haben keine eigene CSS-Regel
// ============================================================================

const MARKER_CLASS_RE = /^(group|peer)(\/[-\w]+)?$/;

/**
 * `true` für `group`, `peer`, `group/<name>`, `peer/<name>`: Tailwind erzeugt
 * für diese bewusst keine eigene CSS-Regel (nur für die `group-*:`/`peer-*:`-
 * Varianten, die sie referenzieren), verifiziert gegen den echten Compiler
 * (`candidatesToCss(["group"])` liefert `null`, obwohl `group` korrekt ist).
 */
export function isKnownMarkerClass(token) {
  return MARKER_CLASS_RE.test(token);
}

// ============================================================================
// isPrintfFormatSpecifier: sprintf-Formatplatzhalter sind keine CSS-Klassen
// ============================================================================

const PRINTF_SPECIFIER_RE = /^%[-+ 0#]*\d*(?:\.\d+)?[sdiuxXofFeEgGcb%]$/;

/**
 * `true` für einen F#-`sprintf`-Formatplatzhalter wie `%s`. Ein Aufruf wie
 * `prop.className (sprintf "status-label %s" (if ... then "a" else "b"))`
 * (Settings/View.fs) liefert bei der reinen Literal-Extraktion `%s` als
 * eigenes Whitespace-Token: es wird zur Laufzeit durch den if/then/else-Wert
 * ersetzt (der unabhängig als eigenes Literal geprüft wird) und ist selbst
 * nie eine Klasse, die im DOM landet.
 */
export function isPrintfFormatSpecifier(token) {
  return PRINTF_SPECIFIER_RE.test(token);
}

// ============================================================================
// loadValidTailwindClasses: echter Tailwind-4-Compiler als Autorität
// ============================================================================

/**
 * Lädt `@tailwindcss/node`s `__unstable__loadDesignSystem` gegen das
 * angegebene Stylesheet und liefert eine synchrone `isValidUtility`-Prüfung
 * über `candidatesToCss`. Kein `npm run build`/`vite build` nötig.
 */
export async function loadValidTailwindClasses({ stylesCssPath, base }) {
  const { __unstable__loadDesignSystem } = await import("@tailwindcss/node");
  const css = fs.readFileSync(stylesCssPath, "utf8");
  const designSystem = await __unstable__loadDesignSystem(css, { base });

  return {
    isValidUtility(token) {
      const [result] = designSystem.candidatesToCss([token]);
      return result !== null && result !== undefined;
    },
  };
}

// ============================================================================
// loadExceptions
// ============================================================================

export function loadExceptions(exceptionsPath) {
  if (!fs.existsSync(exceptionsPath)) return [];
  const raw = fs.readFileSync(exceptionsPath, "utf8");
  const parsed = JSON.parse(raw);
  if (!Array.isArray(parsed)) {
    throw new Error(`${exceptionsPath} muss ein JSON-Array sein.`);
  }
  return parsed;
}

// ============================================================================
// Dynamische Token-Auflösung: Tokens.fs, dann same-file finite-literal lets
// ============================================================================

function stripHoleBraces(text) {
  const trimmed = text.trim();
  if (trimmed.startsWith("{") && trimmed.endsWith("}")) {
    return trimmed.slice(1, -1).trim();
  }
  return trimmed;
}

function leadingIdentifier(exprText) {
  const m = BARE_IDENT_RE.exec(exprText.trim());
  return m ? m[0] : null;
}

function escapeRegExp(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Zerlegt `text` an Kommas, die nicht innerhalb von Klammern oder
 * String-Literalen liegen (für Tupel-Muster wie `let (a, b, c) = ...` und
 * Tupel-Werte wie `(".*", "text-neon-purple", "Regex pattern")`).
 */
function splitTopLevelCommas(text) {
  const parts = [];
  let depth = 0;
  let current = "";
  let i = 0;
  while (i < text.length) {
    const ch = text[i];
    if (ch === '"' || (ch === "$" && text[i + 1] === '"')) {
      const { endIndex } = readAnyString(text, i);
      current += text.slice(i, endIndex);
      i = endIndex;
      continue;
    }
    if (ch === "(" || ch === "[") {
      depth += 1;
      current += ch;
      i += 1;
      continue;
    }
    if (ch === ")" || ch === "]") {
      depth -= 1;
      current += ch;
      i += 1;
      continue;
    }
    if (ch === "," && depth === 0) {
      parts.push(current);
      current = "";
      i += 1;
      continue;
    }
    current += ch;
    i += 1;
  }
  parts.push(current);
  return parts.map((p) => p.trim());
}

function gatherBody(lines, letLineIndex, indent, restOfLine) {
  const eqIndex = restOfLine.indexOf("=");
  if (eqIndex === -1) return null;
  let body = restOfLine.slice(eqIndex + 1).trim();
  for (let j = letLineIndex + 1; j < lines.length; j += 1) {
    const line = lines[j];
    if (line.trim() === "") continue;
    const lineIndent = line.length - line.trimStart().length;
    if (lineIndent <= indent) break;
    body += "\n" + line;
  }
  return body;
}

const SIMPLE_LET_RE_TEMPLATE = (name) =>
  new RegExp(`^(\\s*)let\\s+(?:private\\s+|rec\\s+|inline\\s+)*${escapeRegExp(name)}\\b(.*)$`);
const TUPLE_LET_RE = /^(\s*)let\s+(?:private\s+|rec\s+|inline\s+)*\(([^()]+)\)\s*(.*)$/;
// F# erlaubt Tupel-Dekonstruktion auch OHNE Klammern: `let a, b, c = expr`
// (Stats.fs: `let padding, valueSize, labelSize = sizeToClasses props.Size`).
// Erfordert mindestens ein Komma, sonst würde dies die einfache Ein-Namen-Form
// mit erfassen.
const BARE_TUPLE_LET_RE =
  /^(\s*)let\s+(?:private\s+|rec\s+|inline\s+)*([A-Za-z_][A-Za-z0-9_']*(?:\s*,\s*[A-Za-z_][A-Za-z0-9_']*)+)\b(.*)$/;

/**
 * Findet den Textblock einer `let`-Bindung `name` im Quelltext (erste
 * Fundstelle): die Zeile, die die Bindung einleitet, plus jede folgende Zeile,
 * die stärker eingerückt ist (oder leer ist), als Fortsetzung des Rumpfs.
 * Unterstützt einfache Bindungen (`let name = ...`) sowie Tupel-Muster mit
 * (`let (a, b, c) = ...`) und ohne Klammern (`let a, b, c = ...`): im
 * Tupel-Fall wird `tupleIndex` gesetzt, damit der Aufrufer weiß, welches
 * Tupel-Element `name` entspricht. `null`, wenn keine solche Bindung gefunden
 * wird oder sie kein `=` auf ihrer eigenen Zeile hat (z. B. mehrzeilige
 * Signaturen, außerhalb des Umfangs dieser rein textbasierten Auflösung,
 * siehe Stolperstein 3 der Aufgabe).
 */
/**
 * Extrahiert den Textblock eines Top-Level-Moduls (`module Name = ...`) aus
 * `source` (angedacht für Tokens.fs' flache Modul-Liste): von der
 * `module Name =`-Zeile bis, aber ohne, das nächste `module ...`. `null`, wenn
 * kein Modul dieses Namens gefunden wird.
 */
function extractModuleBlock(source, moduleName) {
  const lines = source.replace(/\r\n/g, "\n").split("\n");
  const startRe = new RegExp(`^module\\s+${escapeRegExp(moduleName)}\\s*=\\s*$`);
  let start = -1;
  for (let i = 0; i < lines.length; i += 1) {
    if (startRe.test(lines[i])) {
      start = i;
      break;
    }
  }
  if (start === -1) return null;

  let end = lines.length;
  for (let i = start + 1; i < lines.length; i += 1) {
    if (/^module\s+[A-Za-z_]/.test(lines[i])) {
      end = i;
      break;
    }
  }
  return lines.slice(start, end).join("\n");
}

function findLetBindingBody(source, name) {
  const lines = source.replace(/\r\n/g, "\n").split("\n");
  const simpleLetRe = SIMPLE_LET_RE_TEMPLATE(name);

  for (let idx = 0; idx < lines.length; idx += 1) {
    const line = lines[idx];

    const tupleMatch = TUPLE_LET_RE.exec(line);
    if (tupleMatch) {
      const names = splitTopLevelCommas(tupleMatch[2]);
      const tupleIndex = names.indexOf(name);
      if (tupleIndex !== -1) {
        const body = gatherBody(lines, idx, tupleMatch[1].length, tupleMatch[3]);
        if (body !== null) return { body, tupleIndex };
      }
      continue;
    }

    const bareTupleMatch = BARE_TUPLE_LET_RE.exec(line);
    if (bareTupleMatch) {
      const names = splitTopLevelCommas(bareTupleMatch[2]);
      const tupleIndex = names.indexOf(name);
      if (tupleIndex !== -1) {
        const body = gatherBody(lines, idx, bareTupleMatch[1].length, bareTupleMatch[3]);
        if (body !== null) return { body, tupleIndex };
      }
      continue;
    }

    const simpleMatch = simpleLetRe.exec(line);
    if (simpleMatch) {
      const body = gatherBody(lines, idx, simpleMatch[1].length, simpleMatch[2]);
      if (body !== null) return { body, tupleIndex: null };
    }
  }
  return null;
}

// `[ \t]*` statt `\s*` direkt nach `->`/`then`/`else` auf DERSELBEN Zeile: `\s`
// matcht auch `\n` und würde sonst über die Zeilengrenze hinweg lesen. Ein Arm
// OHNE Wert auf seiner eigenen Zeile (Wert folgt erst auf der nächsten Zeile,
// beide legitime Stile in diesem Code: ein mehrzeiliger Literal-Arm wie in
// Button.fs' `variantToClass`, ODER ein Arm, dessen "Wert" ein verschachteltes
// `match`/`if` ist wie in TransactionRow.fs' `getRowStateClasses`) wird separat
// über `nextNonBlankLine` behandelt und dort per `isControlFlowKeywordStart`
// unterschieden: eine Fortsetzungszeile, die selbst `match`/`if` einleitet,
// gehört NICHT dem Arm: ihre echten Blätter werden durch den (line-basierten,
// also unabhängig von Verschachtelungstiefe wirkenden) Scan an anderer Stelle
// im selben Rumpf ohnehin erfasst.
// `(?!>)` schließt `|>` (Pipe-Operator) aus: eine Zeile wie
// `|> Option.map (fun a -> ...)` beginnt zwar mit `|`, ist aber kein
// Match-Arm: ohne den Ausschluss würde das `->` der Lambda darin
// fälschlich als Arm-Pfeil gelesen (siehe Table.fs' `alignClass`).
const ARM_LINE_RE = /^\s*\|(?!>)[^\n]*?->[ \t]*(.*)$/;
const SAME_LINE_IF_THEN_ELSE_RE = /\bif\b[\s\S]*?\bthen\b\s*(.+?)\s*\belse\b\s*(.+)$/;
const THEN_RE = /\bthen\b\s*(.*)$/;
const ELSE_ONLY_RE = /^\s*else\b\s*(.*)$/;

function isControlFlowKeywordStart(text) {
  return /^(match\b|if\b)/.test(text.trim());
}

/** Nächste inhaltstragende Zeile: überspringt Leerzeilen UND reine Kommentarzeilen
 * (`// ...`), sonst würde ein erklärender Kommentar direkt nach einem wertlosen
 * `| Pattern ->`/`then`/`else` fälschlich als dessen Wert gelesen (siehe
 * TransactionRow.fs' `getCategoryBadgeClass`, dessen `NotDuplicate _ ->`-Arm von
 * einem mehrzeiligen Kommentar gefolgt wird, bevor der eigentliche verschachtelte
 * `match` beginnt). */
function nextNonBlankLine(lines, fromIndex) {
  for (let j = fromIndex + 1; j < lines.length; j += 1) {
    const trimmed = lines[j].trim();
    if (trimmed === "" || trimmed.startsWith("//")) continue;
    return trimmed;
  }
  return null;
}

/**
 * Sammelt additiv (nicht exklusiv) jeden literalen/Tupel-Zweig-Ausdruck eines
 * `let`-Rumpfs: jede `| ... -> AUSDRUCK`-Zeile (unabhängig von
 * Verschachtelungstiefe, erfasst so auch `match`-Blöcke, die selbst im
 * `else`-Zweig eines `if` stehen), plus jeden `if`/`elif`/`else`-Zweig (auf
 * derselben Zeile wie `then`/`else`, oder, wenn die Zeile mit dem Schlüsselwort
 * endet, auf der nächsten nicht-leeren Zeile). Ein Arm-/`else`-/`then`-Zweig,
 * dessen Wert selbst mit `match`/`if` beginnt oder selbst ein neuer `|`-Arm ist,
 * wird NICHT als eigener Ausdruck übernommen: seine echten Blätter werden
 * bereits durch denselben Scan an anderer Stelle im Rumpf erfasst (siehe
 * Stolperstein: `getRowStateClasses`/`getCategoryBadgeClass` in
 * TransactionRow.fs verschachteln `if` über mehrere `match`-Ebenen; Button.fs'
 * `variantToClass` schreibt jeden Arm-Wert dagegen auf die nächste Zeile).
 */
function collectArmValueTexts(body) {
  const values = [];
  const lines = body.split("\n");

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];

    const armMatch = ARM_LINE_RE.exec(line);
    if (armMatch) {
      const inline = armMatch[1].trim();
      const value = inline.length > 0 ? inline : nextNonBlankLine(lines, i);
      if (value && !isControlFlowKeywordStart(value) && !value.startsWith("|")) {
        values.push(value);
      }
      continue;
    }

    const sameLine = SAME_LINE_IF_THEN_ELSE_RE.exec(line);
    if (sameLine) {
      values.push(sameLine[1].trim());
      values.push(sameLine[2].trim());
      continue;
    }

    const thenMatch = THEN_RE.exec(line);
    if (thenMatch) {
      const inline = thenMatch[1].trim();
      const value = inline.length > 0 ? inline : nextNonBlankLine(lines, i);
      if (value && !isControlFlowKeywordStart(value)) values.push(value);
      continue;
    }

    const elseMatch = ELSE_ONLY_RE.exec(line);
    if (elseMatch) {
      const inline = elseMatch[1].trim();
      const value = inline.length > 0 ? inline : nextNonBlankLine(lines, i);
      if (value && !isControlFlowKeywordStart(value)) values.push(value);
    }
  }

  return values;
}

/**
 * Klassifiziert einen einzelnen gesammelten Zweig-Ausdruck: ein Literal
 * (`"..."`/`$"..."`) liefert dessen Inhalt direkt; ein geklammerter Ausdruck
 * `(...)` wird (falls `tupleIndex` gesetzt ist) an Kommas zerlegt und nur
 * das `tupleIndex`-te Element weiterverfolgt (sonst als reine Gruppierung
 * behandelt, ihr Inhalt wird direkt weiterverfolgt); ein nackter Bezeichner
 * ohne Anführungszeichen gilt als Weiterleitung an eine andere `let`-Bindung
 * und wird rekursiv aufgelöst. `null`, wenn nichts davon zutrifft.
 */
function classifyArmValue(text, tupleIndex, ctx, depth) {
  const trimmed = text.trim();

  if (trimmed.startsWith("(")) {
    const close = findMatchingParen(trimmed, 0);
    if (close === -1) return null;
    const inner = trimmed.slice(1, close);
    const trailing = trimmed.slice(close + 1).trim();
    if (trailing !== "") return null;

    if (tupleIndex !== null) {
      const parts = splitTopLevelCommas(inner);
      if (tupleIndex >= parts.length) return null;
      return classifyArmValue(parts[tupleIndex], null, ctx, depth);
    }
    return classifyArmValue(inner, null, ctx, depth);
  }

  // Tupel-Wert OHNE Klammern (F# erlaubt `-> v1, v2, v3` genau wie `let a, b, c = ...`).
  if (tupleIndex !== null) {
    const parts = splitTopLevelCommas(trimmed);
    if (parts.length > 1) {
      if (tupleIndex >= parts.length) return null;
      return classifyArmValue(parts[tupleIndex], null, ctx, depth);
    }
  }

  if (trimmed.startsWith('"') || trimmed.startsWith('$"')) {
    const { content } = readAnyString(trimmed, 0);
    return [content];
  }

  const ident = leadingIdentifier(trimmed);
  if (ident && !trimmed.includes('"')) {
    return resolveIdentifierToLiterals(ident, ctx, depth + 1);
  }

  return null;
}

/**
 * Löst einen Ausdruckstext (Interpolationsloch-Inhalt oder nackter
 * `prop.className`-Bezeichner) rekursiv zu einer Liste literaler
 * String-Inhalte auf. Stufe 1: Tokens.fs-Literale (mit oder ohne
 * `Tokens.`-Präfix). Stufe 2: eine `let`-Bindung mit demselben Namen im
 * übergebenen Quelltext, deren Zweige literal sind oder ihrerseits (über
 * endlich viele Hops) zu Literalen führen. `null` bei Erschöpfung der
 * Rekursionstiefe oder wenn keine der beiden Stufen greift.
 */
function resolveIdentifierToLiterals(exprText, ctx, depth = 0) {
  if (depth > MAX_RESOLUTION_DEPTH) return null;

  const ident = leadingIdentifier(exprText);
  if (!ident) return null;

  if (ctx.tokensLiteralMap.has(ident)) {
    return [ctx.tokensLiteralMap.get(ident)];
  }
  const withoutTokensPrefix = ident.startsWith("Tokens.") ? ident.slice("Tokens.".length) : ident;
  if (ctx.tokensLiteralMap.has(withoutTokensPrefix)) {
    return [ctx.tokensLiteralMap.get(withoutTokensPrefix)];
  }

  // Stufe 1.5: eine Tokens.fs-FUNKTION (kein reines `let name = "..."`-Literal,
  // z. B. `StaggerDelays.forIndex`) mit endlich vielen literalen Zweigen -
  // gesucht innerhalb des passenden Top-Level-Moduls in Tokens.fs, damit ein
  // gleichnamiger Bezeichner in einem ANDEREN Modul nicht versehentlich trifft.
  if (ctx.tokensFsSource) {
    const dotIndex = withoutTokensPrefix.indexOf(".");
    if (dotIndex !== -1) {
      const moduleName = withoutTokensPrefix.slice(0, dotIndex);
      const memberName = withoutTokensPrefix.slice(dotIndex + 1);
      const moduleBlock = extractModuleBlock(ctx.tokensFsSource, moduleName);
      if (moduleBlock !== null) {
        const tokensCtx = { tokensLiteralMap: ctx.tokensLiteralMap, fileSource: moduleBlock, tokensFsSource: ctx.tokensFsSource };
        const resolved = resolveIdentifierToLiterals(memberName, tokensCtx, depth + 1);
        if (resolved !== null) return resolved;
      }
    }
  }

  const baseName = ident.split(".")[0];
  const found = findLetBindingBody(ctx.fileSource, baseName);
  if (found === null) return null;
  const { body, tupleIndex } = found;

  let armValues = collectArmValueTexts(body);
  if (armValues.length === 0) {
    armValues = [body.trim()];
  }

  const collected = [];
  for (const raw of armValues) {
    const classified = classifyArmValue(raw, tupleIndex, ctx, depth);
    if (classified === null) return null;
    collected.push(...classified);
  }
  return collected;
}

/**
 * Löst ein dynamisches Token (mit oder ohne umschließende `{}`) rekursiv zu
 * einer flachen Liste konkreter, whitespace-getrennter Klassen-Tokens auf.
 * `null`, wenn die Auflösung an irgendeiner Stelle scheitert.
 */
function resolveDynamicTokenToStaticTokens(rawToken, ctx, depth = 0) {
  const holeExpr = stripHoleBraces(rawToken);
  const literalContents = resolveIdentifierToLiterals(holeExpr, ctx, depth);
  if (literalContents === null) return null;

  const result = [];
  for (const content of literalContents) {
    const words = content.split(/\s+/).filter((w) => w.length > 0);
    for (const word of words) {
      if (word.includes("{") || word.includes("}")) {
        const nested = resolveDynamicTokenToStaticTokens(word, ctx, depth + 1);
        if (nested === null) return null;
        result.push(...nested);
      } else {
        result.push(word);
      }
    }
  }
  return result;
}

// ============================================================================
// checkCodebase: orchestriert alle Bausteine
// ============================================================================

// Verzeichnisse, die keine erstpartei Quelldateien enthalten und deshalb vom
// Scan ausgenommen werden: `fable_modules` ist Fables lokaler Paket-Cache
// (vendorierte Kopien von Feliz/Elmish/... .fs-Dateien, von Fable selbst beim
// Build erzeugt, nicht in git getrackt: `git ls-files` bestätigt 0 Dateien
// darunter), `bin`/`obj` sind .NET-Build-Ausgaben. Ohne diesen Ausschluss
// scannt der Wächter Bibliothekscode, den dieses Projekt nicht kontrolliert,
// und dessen Funde (falls vorhanden) kein first-party-Fund wären, den dieser
// Wächter beheben soll.
const EXCLUDED_DIR_NAMES = new Set(["fable_modules", "bin", "obj"]);

function walkFsFiles(dir) {
  const results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory() && EXCLUDED_DIR_NAMES.has(entry.name)) continue;
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...walkFsFiles(fullPath));
    } else if (entry.isFile() && fullPath.endsWith(".fs")) {
      results.push(fullPath);
    }
  }
  return results;
}

/**
 * Prüft jeden `prop.className`-Klassen-Token unter `clientDir` gegen die
 * echten Tailwind-Utilities (`stylesCssPath`) und gegen jeden in `stylesCssPath`
 * selbst definierten Selektor. Gibt
 * `{ undefinedTokens, staleExceptions, unresolvedDynamic }` zurück, siehe
 * Aufgabenbeschreibung.
 */
export async function checkCodebase({ clientDir, stylesCssPath, tokensFsPath, exceptionsPath }) {
  const tokensFsSource = fs.readFileSync(tokensFsPath, "utf8");
  const tokensLiteralMap = extractTokensModuleLiterals(tokensFsSource);
  const stylesheetClasses = extractStylesheetClassNames(fs.readFileSync(stylesCssPath, "utf8"));
  const { isValidUtility } = await loadValidTailwindClasses({
    stylesCssPath,
    base: path.dirname(stylesCssPath),
  });
  const exceptions = loadExceptions(exceptionsPath);
  const seenExceptionTokens = new Set();

  const undefinedTokens = [];
  const unresolvedDynamic = [];

  function findException(token) {
    return exceptions.find((e) => e.token === token);
  }

  function recordUndefined(token, location) {
    let entry = undefinedTokens.find((e) => e.token === token);
    if (!entry) {
      entry = { token, locations: [] };
      undefinedTokens.push(entry);
    }
    entry.locations.push(location);
  }

  function checkStaticToken(token, location) {
    if (isKnownMarkerClass(token)) return;
    if (isPrintfFormatSpecifier(token)) return;
    if (stylesheetClasses.has(token)) return;
    if (isValidUtility(token)) return;

    const exception = findException(token);
    if (exception) {
      seenExceptionTokens.add(token);
      return;
    }
    recordUndefined(token, location);
  }

  const files = walkFsFiles(clientDir);
  for (const filePath of files) {
    const fileSource = fs.readFileSync(filePath, "utf8");
    const ctx = { tokensLiteralMap, fileSource, tokensFsSource };
    const tokens = extractClassNameTokens(fileSource);

    for (const token of tokens) {
      const location = path.relative(repoRoot, filePath);
      if (!token.dynamic) {
        checkStaticToken(token.text, location);
        continue;
      }

      const resolved = resolveDynamicTokenToStaticTokens(token.text, ctx);
      if (resolved === null) {
        let entry = unresolvedDynamic.find((e) => e.token === token.text);
        if (!entry) {
          entry = { token: token.text, locations: [] };
          unresolvedDynamic.push(entry);
        }
        entry.locations.push(location);

        const exception = findException(token.text);
        if (exception) {
          seenExceptionTokens.add(token.text);
        } else {
          recordUndefined(token.text, location);
        }
        continue;
      }

      for (const staticToken of resolved) {
        checkStaticToken(staticToken, location);
      }
    }
  }

  const staleExceptions = exceptions.filter((e) => !seenExceptionTokens.has(e.token));

  return { undefinedTokens, staleExceptions, unresolvedDynamic };
}

// ============================================================================
// CLI
// ============================================================================

function parseArgs(argv) {
  const args = { clientDir: path.join(repoRoot, "src", "Client"), isDefaultClientDir: true };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--client-dir" && argv[i + 1]) {
      args.clientDir = path.resolve(argv[i + 1]);
      args.isDefaultClientDir = false;
      i += 1;
    }
  }
  return args;
}

async function main() {
  const { clientDir, isDefaultClientDir } = parseArgs(process.argv.slice(2));
  const stylesCssPath = path.join(repoRoot, "src", "Client", "styles.css");
  const tokensFsPath = path.join(repoRoot, "src", "Client", "DesignSystem", "Tokens.fs");
  // Die Ausnahmeliste gehört zum ECHTEN Client-Baum (jeder Eintrag wird gegen
  // dessen tatsächlichen Scan geprüft, siehe AC "mechanisierte Durchsetzung").
  // Bei einem per --client-dir umgeleiteten (z. B. Scratch-)Scan wäre jeder
  // reale Eintrag zwangsläufig "veraltet", weil er im umgeleiteten Baum gar
  // nicht vorkommen kann: die Ausnahmeliste ist für diesen Scan schlicht nicht
  // anwendbar, also wird sie dann nicht geladen (Falsifizierbarkeitstest prüft
  // nur auf Phantom-Klassen, nicht auf die reale Ausnahmeliste).
  const exceptionsPath = isDefaultClientDir
    ? path.join(repoRoot, "scripts", "tailwind-class-guard-exceptions.json")
    : path.join(clientDir, "__tailwind-class-guard-no-exceptions-file-here__.json");

  const { undefinedTokens, staleExceptions, unresolvedDynamic } = await checkCodebase({
    clientDir,
    stylesCssPath,
    tokensFsPath,
    exceptionsPath,
  });

  console.log(`Tailwind-Klassen-Wächter: ${clientDir}`);
  console.log(`  unresolvedDynamic (informativ): ${unresolvedDynamic.length}`);
  for (const entry of unresolvedDynamic) {
    console.log(`    ${entry.token} (${entry.locations.join(", ")})`);
  }

  let exitCode = 0;

  if (undefinedTokens.length > 0) {
    exitCode = 1;
    console.log(`\nUndefinierte Klassen-Tokens (${undefinedTokens.length}):`);
    for (const entry of undefinedTokens) {
      console.log(`  ${entry.token}`);
      for (const location of entry.locations) {
        console.log(`    ${location}`);
      }
    }
  }

  if (staleExceptions.length > 0) {
    exitCode = 1;
    console.log(`\nVeraltete Ausnahmeliste-Einträge (${staleExceptions.length}):`);
    for (const entry of staleExceptions) {
      console.log(`  ${entry.token} (${entry.reason})`);
    }
  }

  if (exitCode === 0) {
    console.log("\nKeine undefinierten Klassen gefunden.");
  }

  process.exit(exitCode);
}

const isMainModule = process.argv[1] === fileURLToPath(import.meta.url);
if (isMainModule) {
  main().catch((err) => {
    console.error(err);
    process.exit(1);
  });
}
