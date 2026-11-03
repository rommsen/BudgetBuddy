// scripts/browser-usage.mjs
//
// Laufzeitzählung der Browser-Tests (infrastructure-t5m2b, Iteration 2). Eine statische Zählung über
// den Quelltext ließ sich umgehen (`it(`, `describe`, Schleifen, `forEach`, `t.test(`, umbenannter
// Import). Gezählt wird deshalb, was wirklich lief:
//   - `browser-usage-hook.mjs` wird über NODE_OPTIONS in jeden Testprozess und dessen Kindprozesse
//     geladen. Startet ein Prozess einen Browser (über `child_process`, gleich aus welchem Paket) oder
//     ruft `startFrontend` auf, legt er eine Marke für die Testdatei ab, die ihn gestartet hat.
//   - `test-count-reporter.mjs` schreibt je gelaufenem Test eine Zeile mit Datei und Verschachtelung.
//   - `run-guards.mjs` zählt die Blatt-Tests der markierten Dateien und vergleicht mit der Obergrenze.

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

/// Höchstzahl der Tests, die in einer Datei mit Browser oder gestartetem Frontend laufen. Gezählt
/// werden Blatt-Tests zur Laufzeit, jede Form (`test`, `it`, Schleife, Untertest) zählt einzeln.
/// Vorher 360, nach der Durchsicht infrastructure-t5m2b 116. Die Vorgabe des
/// Owners ist 100; sie sinkt, sie steigt nicht nebenbei.
export const MAX_BROWSER_TESTS = 116;

export const USAGE_DIR_ENV = "BB_BROWSER_USAGE_DIR";
export const OWNER_ENV = "BB_BROWSER_USAGE_OWNER";

/// Ein Dateipfad in seiner echten Form: Reporter und `argv` nennen dieselbe Datei sonst verschieden
/// (macOS: /var ist ein Verweis auf /private/var).
export function canonical(file) {
  const resolved = path.resolve(file);
  try {
    return fs.realpathSync(resolved);
  } catch {
    return resolved;
  }
}

/// Die Testdatei, der dieser Prozess zugerechnet wird: geerbt vom Testprozess oder die eigene Datei.
export function usageOwner(env = process.env, argv = process.argv) {
  if (env[OWNER_ENV]) return env[OWNER_ENV];
  if (env.NODE_TEST_CONTEXT && /\.test\.mjs$/.test(argv[1] ?? "")) return canonical(argv[1]);
  return null;
}

/// Startet dieser Befehl einen Browser? Playwright startet Chromium (auch `headless_shell`), Firefox
/// und WebKit (`pw_run.sh`) aus seinem Cache; erkannt wird am Namen der ausführbaren Datei, bei einer
/// Befehlszeile am ersten Wort.
export function isBrowserCommand(command) {
  if (typeof command !== "string") return false;
  // `exec` bekommt eine ganze Befehlszeile: die Datei ist dann das erste Wort. Ein Pfad mit Leerzeichen,
  // den es so gibt, bleibt ganz.
  const executable = fs.existsSync(command) ? command : command.trim().split(/\s+/)[0];
  const name = path.basename(executable);
  return /chrom(e|ium)|headless_shell|firefox|webkit|pw_run|msedge/i.test(name) || /ms-playwright/.test(executable);
}

/// Legt eine Marke ab: die Testdatei hat einen Browser oder ein Frontend gestartet.
export function markBrowserUsage(kind, env = process.env, argv = process.argv) {
  const dir = env[USAGE_DIR_ENV];
  const owner = usageOwner(env, argv);
  if (!dir || !owner) return;
  const id = crypto.createHash("sha1").update(owner).digest("hex");
  fs.writeFileSync(path.join(dir, `${id}-${kind}`), owner);
}

/// Die Testdateien mit Marke.
export function markedFiles(dir) {
  return new Set(fs.readdirSync(dir).map((f) => canonical(fs.readFileSync(path.join(dir, f), "utf8"))));
}

/// Blatt-Tests je Datei aus den Zeilen des Reporters. Ein Test mit Untertests zählt nicht selbst,
/// seine Untertests zählen. Suiten (`describe`) zählen nicht, ihre Tests schon.
export function leafTestsPerFile(lines) {
  const counts = new Map();
  const lastNesting = new Map();
  for (const line of lines) {
    if (!line.trim()) continue;
    const event = JSON.parse(line);
    const file = canonical(event.file ?? "");
    const parent = lastNesting.get(file) === event.nesting + 1;
    lastNesting.set(file, event.nesting);
    if (event.kind !== "test" || parent) continue;
    counts.set(file, (counts.get(file) ?? 0) + 1);
  }
  return counts;
}

/// Summe der Blatt-Tests in markierten Dateien, dazu die Dateien selbst.
export function browserTestCount(counts, marked) {
  const files = [...counts.keys()].filter((f) => marked.has(f)).sort();
  return { files, total: files.reduce((sum, f) => sum + counts.get(f), 0) };
}

/// Meldung bei Überschreitung, sonst null.
export function budgetViolation(result, max = MAX_BROWSER_TESTS) {
  if (result.total <= max) return null;
  return `${result.total} Browser-Tests, erlaubt sind ${max}: Logik gehört unter .NET (Doktrin Punkt 1 und 5). Dateien: ${result.files.map((f) => path.basename(f)).join(", ")}`;
}
