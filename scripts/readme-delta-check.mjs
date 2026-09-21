#!/usr/bin/env node
// scripts/readme-delta-check.mjs
//
// Repo-lokales Gate gegen leise danebengehende README-Deltas (ADR 0040,
// infrastructure-b6kzp). Nimmt ein Paar aus README (oder context-map.md) und
// einem README_DELTA-Block (references/worker-return-format.md), wendet das
// Delta über das ECHTE Plugin-Modul (lib/readme-delta.mjs aus dem
// Agentheim-Plugin-Cache) an, prüft das Ergebnis gegen sechs Kriterien und
// urteilt. Schreibt nie: das Schreiben bleibt beim Conductor (ADR 0054,
// compute-then-write).
//
// Warum das echte Modul und keine Kopie der Grammatik: lib/readme-delta.mjs
// liegt außerhalb dieses Repos, im Plugin-Cache
// (<home>/.claude/plugins/cache/agentheim/agentheim/<version>/lib/readme-delta.mjs)
// und wird beim nächsten Plugin-Update überschrieben. Eine nachgebaute
// Grammatik im Repo würde davon unbemerkt abdriften, genau der Fehler, den
// dieses Gate aufdecken soll (drei stille Fehlschläge am 2026-09-19). Die
// Versionsauflösung folgt derselben Regel wie lib/resolve-plugin-file.mjs:
// Cache-Wurzel aus os.homedir() (nie aus rohen Env-Variablen), darin das
// Semver-Maximum NUMERISCH je Feld (0.8.10 > 0.8.9 > 0.8.3, nicht lexikalisch),
// Nicht-Semver-Verzeichnisse übersprungen, keine hart kodierte Version.
//
// Exit 0: sauber. Exit 1: Delta inhaltlich abgelehnt (Prüfung + Grund
// benannt). Exit 2: das Gate konnte nicht urteilen (Plugin-Modul nicht
// gefunden, Eingabe ungültig), bewusst verschieden von Exit 1, damit ein
// Aufrufer "Gate sagt nein" von "Gate konnte nicht urteilen" unterscheiden
// kann. Kein stiller Durchlauf, kein Skip, kein Rückfall auf eine eigene
// Kopie der Grammatik.
//
// Aufruf:
//   node scripts/readme-delta-check.mjs --bc <kontext> --delta <datei|->
//
// `--delta -` liest den README_DELTA-Block (ein JSON-Array aus
// `{document, section, ops}`) von stdin. `document: "README.md"` löst auf
// `.agentheim/knowledge/contexts/<kontext>/README.md` auf (relativ zum
// aktuellen Arbeitsverzeichnis), `document: "context-map.md"` auf
// `.agentheim/knowledge/context-map.md`. Es gibt kein `--write` und keinen
// Schreibpfad in diesem Skript.

import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { pathToFileURL } from "node:url";

const SEMVER_RE = /^(\d+)\.(\d+)\.(\d+)$/;
const SECTION_HEADER_RE = /^## (.+)$/;
const COLUMN_ZERO_BULLET_RE = /^- /;
const MARKER_RE = /^<!--/;

// ---------------------------------------------------------------------------
// Plugin-Modul-Auflösung (mirrors lib/resolve-plugin-file.mjs's rule)
// ---------------------------------------------------------------------------

/** <home>/.claude/plugins/cache/agentheim/agentheim (nie aus rohen Env-Variablen). */
export function cacheRoot(homedir) {
  return path.join(homedir, ".claude", "plugins", "cache", "agentheim", "agentheim");
}

function compareSemver(a, b) {
  const pa = a.match(SEMVER_RE);
  const pb = b.match(SEMVER_RE);
  for (let i = 1; i <= 3; i++) {
    const d = Number(pa[i]) - Number(pb[i]);
    if (d !== 0) return d;
  }
  return 0;
}

/** Semver-Maximum NUMERISCH je Feld; Nicht-Semver-Namen werden übersprungen. */
export function pickNewestVersion(dirNames) {
  const versions = dirNames.filter((n) => SEMVER_RE.test(n));
  if (versions.length === 0) return null;
  return versions.reduce((best, v) => (compareSemver(v, best) > 0 ? v : best));
}

function listDirNames(root) {
  try {
    return readdirSync(root).filter((n) => {
      try {
        return statSync(path.join(root, n)).isDirectory();
      } catch {
        return false;
      }
    });
  } catch {
    return [];
  }
}

/**
 * Findet `<root>/<version>/lib/readme-delta.mjs` für die höchste Semver-
 * Version, die diese Datei tatsächlich enthält (ein halb geschriebenes
 * neueres Versionsverzeichnis kann die Auflösung so nicht brechen). Liefert
 * NIE eine eigene Kopie der Grammatik als Rückfall, nur den gefundenen Pfad
 * oder `null` mit Diagnose (durchsuchte Wurzel, gefundene Versionsverzeichnisse).
 */
export function resolveReadmeDeltaModule(root) {
  const names = listDirNames(root);
  const versions = names.filter((n) => SEMVER_RE.test(n)).sort((a, b) => compareSemver(b, a));
  for (const v of versions) {
    const candidate = path.join(root, v, "lib", "readme-delta.mjs");
    if (existsSync(candidate)) {
      return { modulePath: candidate, version: v, root, searchedVersions: versions };
    }
  }
  return { modulePath: null, version: null, root, searchedVersions: versions };
}

// ---------------------------------------------------------------------------
// Markdown-Strukturhelfer: eigenständig, unabhängig von der geladenen
// Anwend-Entscheidung des Plugin-Moduls (die kommt IMMER aus dem echten
// `applyReadmeDelta`; diese Helfer liefern nur die zusätzliche Diagnose, die
// das Modul selbst nicht exportiert).
// ---------------------------------------------------------------------------

function collapseWs(s) {
  return String(s).replace(/\s+/g, " ").trim();
}

/** `[start, end)` der Body-Zeilen eines `## `-Abschnitts, `null` wenn er fehlt. */
export function findSectionRange(lines, sectionName) {
  let start = -1;
  let end = lines.length;
  for (let i = 0; i < lines.length; i++) {
    const m = lines[i].match(SECTION_HEADER_RE);
    if (!m) continue;
    if (start === -1) {
      if (m[1].trim() === sectionName) start = i + 1;
    } else {
      end = i;
      break;
    }
  }
  return start === -1 ? null : { start, end };
}

export function allSectionHeaders(lines) {
  const heads = [];
  for (const l of lines) {
    const m = l.match(SECTION_HEADER_RE);
    if (m) heads.push(m[1].trim());
  }
  return heads;
}

/** Abschnittsende ohne abschließende Leerzeilen (der Einfügepunkt eines `append`). */
export function trimTrailingBlank(lines, start, end) {
  let at = end;
  while (at > start && lines[at - 1].trim() === "") at--;
  return at;
}

/** Zahl der Spalte-0-`- `-Zeilen in `[start, end)`. */
export function columnZeroBulletCount(lines, start, end) {
  let n = 0;
  for (let i = start; i < end; i++) if (COLUMN_ZERO_BULLET_RE.test(lines[i])) n++;
  return n;
}

/** Menge der Einrückungsbreiten aller `- `-Zeilen (jede Tiefe) in `[start, end)`. */
export function indentWidths(lines, start, end) {
  const widths = new Set();
  for (let i = start; i < end; i++) {
    const m = lines[i].match(/^(\s*)- /);
    if (m) widths.add(m[1].length);
  }
  return widths;
}

/** Erste Zeile des letzten zusammenhängenden Nicht-Leer-Blocks direkt vor `insertionPoint`. */
export function lastBlockFirstLine(lines, start, insertionPoint) {
  let i = insertionPoint - 1;
  if (i < start) return null;
  if (lines[i].trim() === "") return null;
  while (i > start && lines[i - 1].trim() !== "") i--;
  return lines[i];
}

/**
 * Spalte-0-Bullet-Blöcke in `[start, end)`: ein Bullet läuft von seiner
 * eigenen `- `-Zeile bis (ausschließlich) zur nächsten Spalte-0-`- `-Zeile,
 * einem `#`-Header, einem `<!--`-Marker oder `end`. Nur für die Diagnose in
 * Prüfung 6 (erreichbare termHeads) gebraucht, die tatsächliche
 * Anwend-Entscheidung trifft immer das geladene Plugin-Modul, nie dieser
 * Helfer.
 */
export function findBulletBlocks(lines, start, end) {
  const bullets = [];
  let i = start;
  while (i < end) {
    if (!COLUMN_ZERO_BULLET_RE.test(lines[i])) {
      i++;
      continue;
    }
    const bstart = i;
    i++;
    while (i < end && !COLUMN_ZERO_BULLET_RE.test(lines[i]) && !lines[i].startsWith("#") && !MARKER_RE.test(lines[i])) {
      i++;
    }
    let bend = i;
    while (bend > bstart + 1 && lines[bend - 1].trim() === "") bend--;
    bullets.push({ start: bstart, end: bend });
  }
  return bullets;
}

/** Der fette Begriffskopf eines Bullets, an der ersten `(` trunkiert, Leerraum kollabiert. `null` ohne fetten Kopf. */
export function termHeadOf(text) {
  const m = collapseWs(text).match(/^- \*\*(.+?)\*\*/);
  if (!m) return null;
  let head = m[1];
  const parenIdx = head.indexOf("(");
  if (parenIdx !== -1) head = head.slice(0, parenIdx);
  return collapseWs(head);
}

/** Alle über `replace` erreichbaren termHeads eines Abschnitts (nur Bullets mit fettem Kopf). */
export function reachableTermHeads(lines, start, end) {
  const heads = [];
  for (const b of findBulletBlocks(lines, start, end)) {
    const h = termHeadOf(lines.slice(b.start, b.end).join("\n"));
    if (h) heads.push(h);
  }
  return heads;
}

/** Knapper, lesbarer Diff (Präfix/Suffix-getrimmt), [human-eye] only, keine Anwend-Entscheidung. */
export function simpleUnifiedDiff(label, beforeLines, afterLines) {
  let i = 0;
  const n = Math.min(beforeLines.length, afterLines.length);
  while (i < n && beforeLines[i] === afterLines[i]) i++;
  let bEnd = beforeLines.length;
  let aEnd = afterLines.length;
  while (bEnd > i && aEnd > i && beforeLines[bEnd - 1] === afterLines[aEnd - 1]) {
    bEnd--;
    aEnd--;
  }
  const removed = beforeLines.slice(i, bEnd);
  const added = afterLines.slice(i, aEnd);
  const out = [];
  out.push(`--- ${label} (vorher)`);
  out.push(`+++ ${label} (nachher)`);
  out.push(`@@ -${i + 1},${removed.length} +${i + 1},${added.length} @@`);
  for (const l of removed) out.push(`-${l}`);
  for (const l of added) out.push(`+${l}`);
  return out.join("\n");
}

// ---------------------------------------------------------------------------
// Die sechs Prüfungen
// ---------------------------------------------------------------------------

/**
 * Wertet EINEN Op aus (die Disposition kommt vom echten Plugin-Modul, alles
 * andere ist Diagnose obendrauf). Reihenfolge folgt der Aufgabenstellung:
 * 1 Disposition, 2 Abschnitt vorhanden, 3 Einfügepunkt bei append,
 * 4 Bullet-Budget, 5 Einrückungsprofil bei replace, 6 Anker-Erreichbarkeit.
 */
export function evaluateOp({ op, disposition, section, linesBefore, rangeBefore, linesAfter }) {
  if (disposition === "merged") {
    return {
      ok: false,
      check: 1,
      reason: `Prüfung 1 (Disposition) verletzt: "merged" für Op "${op.op}" in Abschnitt "${section}": das "expected" des Ops passt nicht mehr auf den aktuellen Bullet-Extent.`,
    };
  }

  if (disposition === "appended-fallback") {
    const headers = allSectionHeaders(linesBefore);
    const list = headers.length ? headers.map((h) => `## ${h}`).join(", ") : "(keine Abschnitte gefunden)";
    return {
      ok: false,
      check: 2,
      reason: `Prüfung 2 (Abschnitt vorhanden) verletzt: Abschnitt "${section}" existiert nicht in der Zieldatei. Vorhandene Abschnitte: ${list}`,
    };
  }

  if (disposition === "anchor-missing") {
    if (!rangeBefore) {
      return {
        ok: false,
        check: 6,
        reason: `Prüfung 6 (Anker-Erreichbarkeit) verletzt: Anker "${op.anchor}" nicht erreichbar, Abschnitt "${section}" wurde nicht gefunden.`,
      };
    }
    const heads = reachableTermHeads(linesBefore, rangeBefore.start, rangeBefore.end);
    const detail = heads.length
      ? `erreichbare termHeads in "${section}": ${heads.join(", ")}`
      : `Abschnitt "${section}" ist per replace grundsätzlich nicht erreichbar (kein Bullet mit fettem Begriffskopf).`;
    return {
      ok: false,
      check: 6,
      reason: `Prüfung 6 (Anker-Erreichbarkeit) verletzt: Anker "${op.anchor}" trägt kein Bullet in "${section}". ${detail}`,
    };
  }

  if (disposition !== "applied" && disposition !== "noop-already") {
    return {
      ok: false,
      check: 1,
      reason: `Prüfung 1 (Disposition) verletzt: unbekannte Disposition "${disposition}" für Op "${op.op}" in Abschnitt "${section}".`,
    };
  }

  if (disposition === "noop-already") {
    return { ok: true };
  }

  // disposition === "applied": Prüfung 3/4/5, je nach Op-Art.
  if (!rangeBefore) {
    return {
      ok: false,
      check: 2,
      reason: `Prüfung 2 (Abschnitt vorhanden) verletzt: Abschnitt "${section}" wurde vor dem Anwenden nicht gefunden, obwohl die Disposition "applied" lautet.`,
    };
  }
  const rangeAfter = findSectionRange(linesAfter, section);
  if (!rangeAfter) {
    return {
      ok: false,
      check: 1,
      reason: `Prüfung 1 (Disposition) verletzt: Abschnitt "${section}" ist nach dem Anwenden nicht mehr auffindbar.`,
    };
  }

  if (op.op === "append") {
    const insertionPoint = trimTrailingBlank(linesBefore, rangeBefore.start, rangeBefore.end);
    const blockFirstLine = lastBlockFirstLine(linesBefore, rangeBefore.start, insertionPoint);
    if (blockFirstLine === null || !COLUMN_ZERO_BULLET_RE.test(blockFirstLine)) {
      return {
        ok: false,
        check: 3,
        reason: `Prüfung 3 (Einfügepunkt bei append) verletzt: der Einfügepunkt in "${section}" liegt hinter Fließtext, nicht hinter einem Bullet.`,
      };
    }
    const before = columnZeroBulletCount(linesBefore, rangeBefore.start, rangeBefore.end);
    const after = columnZeroBulletCount(linesAfter, rangeAfter.start, rangeAfter.end);
    if (after !== before + 1) {
      return {
        ok: false,
        check: 4,
        reason: `Prüfung 4 (Bullet-Budget) verletzt: ${before} Spalte-0-Bullets vor "append", ${after} danach (erwartet ${before + 1}) in "${section}".`,
      };
    }
    return { ok: true };
  }

  if (op.op === "replace") {
    const before = columnZeroBulletCount(linesBefore, rangeBefore.start, rangeBefore.end);
    const after = columnZeroBulletCount(linesAfter, rangeAfter.start, rangeAfter.end);
    if (after !== before) {
      return {
        ok: false,
        check: 4,
        reason: `Prüfung 4 (Bullet-Budget) verletzt: ${before} Spalte-0-Bullets vor "replace", ${after} danach (erwartet unverändert) in "${section}".`,
      };
    }
    const widthsBefore = indentWidths(linesBefore, rangeBefore.start, rangeBefore.end);
    const widthsAfter = indentWidths(linesAfter, rangeAfter.start, rangeAfter.end);
    const lost = [...widthsBefore].filter((w) => !widthsAfter.has(w));
    if (lost.length > 0) {
      return {
        ok: false,
        check: 5,
        reason: `Prüfung 5 (Einrückungsprofil) verletzt: Breite(n) [${lost.join(", ")}] in "${section}" vor "replace" vorhanden, danach verschwunden.`,
      };
    }
    return { ok: true };
  }

  // Kein Fallback für unbekannte op.op hier: main() ruft validateOpShape()
  // VOR jedem applyReadmeDelta()-Aufruf auf und weist ein unbekanntes op.op
  // (oder ein fehlendes body/anchor/expected) bereits mit Exit 2 zurück.
  // evaluateOp() sieht folglich nie eine unbekannte Op-Art; ein eigener
  // Rückfallzweig hier wäre unerreichbarer, ungetesteter Code.
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

export function resolveDocumentPath(repoRoot, bc, document) {
  if (document === "README.md") {
    return path.join(repoRoot, ".agentheim", "knowledge", "contexts", bc, "README.md");
  }
  if (document === "context-map.md") {
    return path.join(repoRoot, ".agentheim", "knowledge", "context-map.md");
  }
  return null;
}

function parseArgs(argv) {
  const args = { bc: null, delta: null, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--help" || a === "-h") args.help = true;
    else if (a === "--bc") args.bc = argv[++i];
    else if (a === "--delta") args.delta = argv[++i];
  }
  return args;
}

const HELP_TEXT = `readme-delta-check: Gate gegen leise danebengehende README-Deltas (ADR 0040)

Aufruf:
  node scripts/readme-delta-check.mjs --bc <kontext> --delta <datei|->
  npm run check:readme-delta -- --bc <kontext> --delta <datei|->

Optionen:
  --bc <kontext>     Bounded-Context-Name (löst "README.md" auf
                      .agentheim/knowledge/contexts/<kontext>/README.md auf)
  --delta <datei|->  Pfad zur README_DELTA-JSON-Datei, oder "-" für stdin
  --help             diese Hilfe

Exit-Codes:
  0  Delta sauber, kann integriert werden
  1  Delta inhaltlich abgelehnt (Prüfung + Grund in der Meldung)
  2  Gate konnte nicht urteilen (Plugin-Modul nicht gefunden, Eingabe ungültig)

Schreibt nie. Lädt IMMER das echte lib/readme-delta.mjs aus dem
Plugin-Cache (Semver-höchste Version), nie eine eigene Kopie der Grammatik.
`;

function fail2(message) {
  process.stderr.write(`${message}\n`);
  process.exit(2);
}

/**
 * Prüft die Form EINES Ops, BEVOR er an das echte applyReadmeDelta()
 * gereicht wird. Das Plugin-Modul wirft bei einer unbekannten op.op
 * (`throw new Error(...)`, lib/readme-delta.mjs) und bei fehlendem
 * body/anchor/expected (TypeError auf undefined.split, ebenda). Unabgefangen
 * wäre das ein Exit 1 mit Stack-Trace statt des benannten Exit 2, den
 * ADR 0040 für "Gate konnte nicht urteilen" vorschreibt.
 * Gibt bei ungültiger Form eine Grund-Meldung zurück, sonst null.
 */
export function validateOpShape(op, section) {
  if (!op || typeof op !== "object" || Array.isArray(op)) {
    return `Op in Abschnitt "${section}" ist kein Objekt.`;
  }
  if (op.op !== "append" && op.op !== "replace") {
    return `unbekannte Op "${op.op}" in Abschnitt "${section}" (erwartet "append" oder "replace").`;
  }
  if (typeof op.body !== "string") {
    return `Op "${op.op}" in Abschnitt "${section}" hat kein "body" (String erwartet).`;
  }
  if (op.op === "replace") {
    if (typeof op.anchor !== "string") {
      return `Op "replace" in Abschnitt "${section}" hat kein "anchor" (String erwartet).`;
    }
    if (typeof op.expected !== "string") {
      return `Op "replace" in Abschnitt "${section}" hat kein "expected" (String erwartet).`;
    }
  }
  return null;
}

async function main(argv) {
  const args = parseArgs(argv);
  if (args.help) {
    process.stdout.write(HELP_TEXT);
    process.exit(0);
  }
  if (!args.bc || !args.delta) {
    fail2(`Grund: fehlende Pflichtargumente --bc und/oder --delta.\n\n${HELP_TEXT}`);
    return;
  }

  const root = cacheRoot(os.homedir());
  const resolved = resolveReadmeDeltaModule(root);
  if (!resolved.modulePath) {
    fail2(
      `Grund: kein installiertes Agentheim-Plugin gefunden, kein "lib/readme-delta.mjs" unter ` +
        `${resolved.root} in Versionsverzeichnis(sen) [${resolved.searchedVersions.join(", ") || "keine"}]. ` +
        `Kein stiller Durchlauf, kein Skip, keine eigene Kopie der Grammatik als Rückfall.`
    );
    return;
  }

  let moduleExports;
  try {
    moduleExports = await import(pathToFileURL(resolved.modulePath).href);
  } catch (err) {
    fail2(`Grund: Plugin-Modul ${resolved.modulePath} konnte nicht geladen werden: ${err.message}`);
    return;
  }
  const applyReadmeDelta = moduleExports.applyReadmeDelta;
  if (typeof applyReadmeDelta !== "function") {
    fail2(`Grund: ${resolved.modulePath} exportiert kein "applyReadmeDelta".`);
    return;
  }
  const fallbackSection = moduleExports.FALLBACK_SECTION || "Ubiquitous language";

  let deltaText;
  try {
    deltaText = args.delta === "-" ? readFileSync(0, "utf8") : readFileSync(args.delta, "utf8");
  } catch (err) {
    fail2(`Grund: README_DELTA konnte nicht gelesen werden (${args.delta}): ${err.message}`);
    return;
  }

  let entries;
  try {
    entries = JSON.parse(deltaText);
  } catch (err) {
    fail2(`Grund: README_DELTA ist kein gültiges JSON: ${err.message}`);
    return;
  }
  if (!Array.isArray(entries)) {
    fail2(`Grund: README_DELTA muss ein JSON-Array aus {document, section, ops} sein.`);
    return;
  }

  const repoRoot = process.cwd();
  const currentContent = new Map(); // docPath -> content
  const diffBlocks = [];

  for (const entry of entries) {
    const { document, section, ops } = entry || {};
    const docPath = resolveDocumentPath(repoRoot, args.bc, document);
    if (!docPath) {
      fail2(`Grund: unbekanntes document "${document}" (erwartet "README.md" oder "context-map.md").`);
      return;
    }
    if (!currentContent.has(docPath)) {
      if (!existsSync(docPath)) {
        fail2(`Grund: Zieldatei nicht gefunden: ${docPath}`);
        return;
      }
      currentContent.set(docPath, readFileSync(docPath, "utf8"));
    }
    const relDoc = path.relative(repoRoot, docPath);

    for (const op of ops || []) {
      const shapeError = validateOpShape(op, section);
      if (shapeError) {
        fail2(`Grund: ${shapeError}`);
        return;
      }

      const before = currentContent.get(docPath);
      const linesBefore = before.split("\n");
      const rangeBefore = findSectionRange(linesBefore, section);

      let after, dispositions;
      try {
        ({ content: after, dispositions } = applyReadmeDelta(before, { section, ops: [op] }));
      } catch (err) {
        // Sicherheitsnetz zusätzlich zu validateOpShape(): lib/readme-delta.mjs
        // liegt außerhalb dieses Repos und kann sich mit einer künftigen
        // Plugin-Version ändern; ein Wurf von dort darf trotzdem nie
        // unabgefangen als Exit 1 mit Stack-Trace nach außen dringen.
        fail2(
          `Grund: Plugin-Modul lehnte Op "${op.op}" in Abschnitt "${section}" ab: ${err.message}`
        );
        return;
      }
      const disposition = dispositions[0];
      const linesAfter = after.split("\n");

      // Diff des berührten Abschnitts (oder, wenn der Abschnitt fehlte, des
      // Fallback-Abschnitts bzw. notfalls des ganzen Dokuments), [human-eye].
      let diffRange = rangeBefore;
      let diffLabel = section;
      if (!diffRange) {
        diffRange = findSectionRange(linesBefore, fallbackSection);
        diffLabel = fallbackSection;
      }
      if (diffRange) {
        const afterRange = findSectionRange(linesAfter, diffLabel) || { start: 0, end: linesAfter.length };
        diffBlocks.push(
          simpleUnifiedDiff(
            `${relDoc} :: ## ${diffLabel}`,
            linesBefore.slice(diffRange.start, diffRange.end),
            linesAfter.slice(afterRange.start, afterRange.end)
          )
        );
      } else {
        diffBlocks.push(simpleUnifiedDiff(`${relDoc} (kein Abschnitt gefunden)`, linesBefore, linesAfter));
      }

      const result = evaluateOp({ op, disposition, section, linesBefore, rangeBefore, linesAfter });
      if (!result.ok) {
        process.stdout.write(`Plugin-Version: ${resolved.version}\n\n`);
        process.stdout.write(diffBlocks.join("\n\n") + "\n");
        process.stderr.write(`${result.reason}\n`);
        process.exit(1);
        return;
      }

      currentContent.set(docPath, after);
    }
  }

  process.stdout.write(`Plugin-Version: ${resolved.version}\n\n`);
  process.stdout.write(diffBlocks.join("\n\n") + "\n");
  process.exit(0);
}

const isMain = (() => {
  try {
    return pathToFileURL(process.argv[1]).href === import.meta.url;
  } catch {
    return false;
  }
})();

if (isMain) {
  main(process.argv.slice(2));
}
