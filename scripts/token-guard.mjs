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

/** Alle Treffer einer Quelle: [{ line, pattern, match }]. Kommentarzeilen (`//`) zählen nicht. */
export function findViolations(source) {
  const hits = [];
  const lines = source.split(/\r?\n/);
  lines.forEach((line, idx) => {
    if (line.trimStart().startsWith("//")) return;
    for (const { name, re } of PATTERNS) {
      re.lastIndex = 0;
      for (const m of line.matchAll(re)) {
        hits.push({ line: idx + 1, pattern: name, match: m[0] });
      }
    }
  });
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
    for (const hit of findViolations(fs.readFileSync(file, "utf8"))) {
      if (inDs) {
        const key = `${rel}|${hit.pattern}`;
        seen.set(key, (seen.get(key) ?? 0) + 1);
        violations.push({ file: rel, ...hit, inDs: true });
      } else {
        violations.push({ file: rel, ...hit, inDs: false });
      }
    }
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
