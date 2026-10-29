// scripts/claude-harness-check.mjs
//
// Prüft die Harness-Dateien unter `.claude/` (infrastructure-cbr0r, Harness-Audit M10):
//
//   1. Jede Rule in `.claude/rules/*.md`: jedes Glob in `paths:` trifft mindestens eine
//      Datei im Repo. Eine Rule mit totem Pfad lädt nie und fällt nicht auf.
//   2. Jeder Agent in `.claude/agents/*.md`: Frontmatter ist lesbar und hat `name` und
//      `description`. Ein Agent mit kaputtem Frontmatter fehlt still in der Agent-Liste.
//   3. Repo-Pfade in Backticks in Rules und Agents (`src/...`, `standards/...`,
//      `scripts/...`, `.claude/...`) und jeder `standards/...`-Pfad in CLAUDE.md zeigen
//      auf etwas, das es gibt.
//
// Der Frontmatter-Leser kennt nur die Teilmenge von YAML, die diese Dateien brauchen:
// `schluessel: wert` auf Spalte 0, Listen (`  - eintrag`), eingerückte Unterblöcke,
// Block-Skalare (`|`, `>`), Werte in Anführungszeichen. Er meldet, was YAML ablehnt und
// in diesem Repo vorkam: eine Zeile auf Spalte 0 ohne Schlüssel (mehrzeilige
// Beschreibung ohne Einrückung) und `: ` in einem Wert ohne Anführungszeichen.
//
// Aufruf von Hand: `node scripts/claude-harness-check.mjs` (Exit 1 bei Funden).
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const repoRoot = path.resolve(here, "..");

const IGNORED_DIRS = ["node_modules", "bin", "obj", ".git", "dist", ".worktrees"];

/// Liefert den Frontmatter-Text zwischen der ersten Zeile `---` und der nächsten `---`,
/// oder null, wenn die Datei nicht mit `---` beginnt.
export function extractFrontmatter(text) {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  if (lines[0] !== "---") return null;
  const end = lines.indexOf("---", 1);
  if (end < 0) return { lines: lines.slice(1), closed: false };
  return { lines: lines.slice(1, end), closed: true };
}

function unquote(raw, lineNo, errors) {
  const v = raw.trim();
  if (v.startsWith('"')) {
    if (v.length < 2 || !v.endsWith('"')) {
      errors.push(`Zeile ${lineNo}: Anführungszeichen nicht geschlossen`);
      return v;
    }
    try {
      return JSON.parse(v);
    } catch {
      errors.push(`Zeile ${lineNo}: Wert in Anführungszeichen nicht lesbar`);
      return v;
    }
  }
  if (v.startsWith("'")) {
    if (v.length < 2 || !v.endsWith("'")) {
      errors.push(`Zeile ${lineNo}: Anführungszeichen nicht geschlossen`);
      return v;
    }
    return v.slice(1, -1).replace(/''/g, "'");
  }
  if (/^[[{]/.test(v)) {
    const close = v[0] === "[" ? "]" : "}";
    if (!v.endsWith(close)) errors.push(`Zeile ${lineNo}: Klammer nicht geschlossen`);
    if (v[0] === "[") return v.slice(1, -1).split(",").map((s) => s.trim()).filter(Boolean);
    return v;
  }
  if (/^[*&!%@`]/.test(v)) {
    errors.push(`Zeile ${lineNo}: Wert beginnt mit einem YAML-Sonderzeichen, Anführungszeichen nötig`);
    return v;
  }
  const noComment = v.replace(/\s+#.*$/, "");
  if (/:\s/.test(noComment) || noComment.endsWith(":")) {
    errors.push(`Zeile ${lineNo}: ": " in einem Wert ohne Anführungszeichen`);
  }
  return noComment;
}

/// Liest Frontmatter-Zeilen. Ergebnis: { data, errors }. `data[key]` ist ein String,
/// eine Liste (bei `- eintrag`-Zeilen) oder null (Unterblock).
export function parseFrontmatter(lines) {
  const data = {};
  const errors = [];
  let current = null; // { key, kind: "plain" | "block" | "nested" | "list" | "quoted" }
  lines.forEach((line, i) => {
    const lineNo = i + 2; // Zeile 1 ist das öffnende ---
    if (line.trim() === "" || /^\s*#/.test(line)) return;
    if (/^\t/.test(line)) {
      errors.push(`Zeile ${lineNo}: Tabulator als Einrückung`);
      return;
    }
    if (!/^\s/.test(line)) {
      const m = line.match(/^([A-Za-z_][\w-]*):(?:\s+(.*))?$/);
      if (!m) {
        errors.push(`Zeile ${lineNo}: kein "schlüssel: wert" auf Spalte 0: ${line.slice(0, 60)}`);
        current = null;
        return;
      }
      const [, key, rawValue] = m;
      if (key in data) errors.push(`Zeile ${lineNo}: Schlüssel "${key}" doppelt`);
      if (rawValue === undefined || rawValue.trim() === "") {
        data[key] = null;
        current = { key, kind: "nested" };
      } else if (/^[|>][+-]?$/.test(rawValue.trim())) {
        data[key] = "";
        current = { key, kind: "block" };
      } else {
        data[key] = unquote(rawValue, lineNo, errors);
        current = { key, kind: /^["'[{]/.test(rawValue.trim()) ? "quoted" : "plain" };
      }
      return;
    }
    // eingerückte Zeile
    if (!current) {
      errors.push(`Zeile ${lineNo}: eingerückte Zeile ohne Schlüssel davor`);
      return;
    }
    const content = line.trim();
    if (current.kind === "block") {
      data[current.key] = (data[current.key] ? data[current.key] + "\n" : "") + content;
    } else if (current.kind === "plain") {
      if (/:\s/.test(content)) errors.push(`Zeile ${lineNo}: ": " in einem Wert ohne Anführungszeichen`);
      data[current.key] = `${data[current.key]} ${content}`;
    } else if (current.kind === "quoted") {
      errors.push(`Zeile ${lineNo}: Fortsetzung nach einem Wert in Anführungszeichen`);
    } else {
      const item = content.match(/^-\s+(.*)$/);
      if (item && (current.kind === "list" || data[current.key] === null) && !/^\s{4,}/.test(line)) {
        if (!Array.isArray(data[current.key])) data[current.key] = [];
        current.kind = "list";
        data[current.key].push(unquote(item[1], lineNo, errors));
      }
      // sonst: Unterblock (z. B. hooks:), Inhalt wird nicht ausgewertet
    }
  });
  return { data, errors };
}

function isIgnored(rel) {
  return rel.split("/").some((seg) => IGNORED_DIRS.includes(seg));
}

/// Dateien (keine Verzeichnisse) im Repo, die das Glob treffen.
export function globFiles(root, pattern) {
  let hits;
  try {
    hits = fs.globSync(pattern, { cwd: root });
  } catch {
    return [];
  }
  return hits.filter((rel) => {
    if (isIgnored(rel.replace(/\\/g, "/"))) return false;
    try {
      return fs.statSync(path.join(root, rel)).isFile();
    } catch {
      return false;
    }
  });
}

function pathExists(root, ref) {
  if (/[*?[{]/.test(ref)) return globFiles(root, ref).length > 0;
  return fs.existsSync(path.join(root, ref));
}

function mdFiles(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs
    .readdirSync(dir)
    .filter((f) => f.endsWith(".md"))
    .sort()
    .map((f) => path.join(dir, f));
}

const REF_PREFIX = /^(?:src|standards|scripts|\.claude|docs)\//;

/// Repo-Pfade in Backticks, ohne Zeilenangabe (`datei.fs:12`), ohne Satzzeichen am Ende
/// und ohne Platzhalter in spitzen Klammern.
export function backtickPathRefs(text) {
  const refs = new Set();
  for (const m of text.matchAll(/`([^`\s]+)`/g)) {
    let ref = m[1].replace(/:\d+(?:-\d+)?$/, "").replace(/[.,;)]+$/, "");
    // Platzhalter wie `src/Client/Components/<Seite>/` sind keine Pfade.
    if (REF_PREFIX.test(ref) && !/[<>]/.test(ref)) refs.add(ref);
  }
  return [...refs];
}

/// `standards/...`-Pfade irgendwo im Text (für CLAUDE.md, dort auch ohne Backticks).
export function standardsRefs(text) {
  const refs = new Set();
  for (const m of text.matchAll(/standards\/[A-Za-z0-9_./-]*[A-Za-z0-9_-](?:\.md)?/g)) {
    refs.add(m[0]);
  }
  return [...refs];
}

/// Alle Funde unter `root` als Liste von Meldungen "<datei>: <problem>".
export function checkHarness(root = repoRoot) {
  const problems = [];
  const rel = (p) => path.relative(root, p).replace(/\\/g, "/");

  for (const file of mdFiles(path.join(root, ".claude", "rules"))) {
    const text = fs.readFileSync(file, "utf8");
    const fm = extractFrontmatter(text);
    if (fm) {
      if (!fm.closed) problems.push(`${rel(file)}: Frontmatter nicht geschlossen`);
      const { data, errors } = parseFrontmatter(fm.lines);
      errors.forEach((e) => problems.push(`${rel(file)}: ${e}`));
      const paths = data.paths;
      const list = Array.isArray(paths) ? paths : typeof paths === "string" ? [paths] : [];
      if ("paths" in data && list.length === 0) problems.push(`${rel(file)}: paths ist leer`);
      for (const p of list) {
        if (globFiles(root, p).length === 0) problems.push(`${rel(file)}: paths "${p}" trifft keine Datei`);
      }
    } else if (/^<!--/.test(text.trimStart())) {
      problems.push(`${rel(file)}: Inhalt ist auskommentiert`);
    }
    for (const ref of backtickPathRefs(text)) {
      if (!pathExists(root, ref)) problems.push(`${rel(file)}: Verweis "${ref}" zeigt ins Leere`);
    }
  }

  for (const file of mdFiles(path.join(root, ".claude", "agents"))) {
    const text = fs.readFileSync(file, "utf8");
    const fm = extractFrontmatter(text);
    if (!fm) {
      problems.push(`${rel(file)}: kein Frontmatter`);
    } else {
      if (!fm.closed) problems.push(`${rel(file)}: Frontmatter nicht geschlossen`);
      const { data, errors } = parseFrontmatter(fm.lines);
      errors.forEach((e) => problems.push(`${rel(file)}: ${e}`));
      for (const key of ["name", "description"]) {
        if (typeof data[key] !== "string" || data[key].trim() === "") {
          problems.push(`${rel(file)}: Feld "${key}" fehlt oder ist leer`);
        }
      }
    }
    for (const ref of backtickPathRefs(text)) {
      if (!pathExists(root, ref)) problems.push(`${rel(file)}: Verweis "${ref}" zeigt ins Leere`);
    }
  }

  const claudeMd = path.join(root, "CLAUDE.md");
  if (fs.existsSync(claudeMd)) {
    for (const ref of standardsRefs(fs.readFileSync(claudeMd, "utf8"))) {
      if (!pathExists(root, ref)) problems.push(`CLAUDE.md: Verweis "${ref}" zeigt ins Leere`);
    }
  }

  return problems;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const problems = checkHarness();
  problems.forEach((p) => console.log(p));
  console.log(problems.length === 0 ? "Harness-Prüfung: sauber" : `Harness-Prüfung: ${problems.length} Funde`);
  process.exit(problems.length === 0 ? 0 : 1);
}
