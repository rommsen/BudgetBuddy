// Gesamtlauf der Wächter (npm run test:scripts), infrastructure-eq27s.
//
// Baut den Client einmal (vite build in ein temporäres Verzeichnis), setzt
// BB_GUARD_DIST darauf und startet `node --test`. screenshot-route.mjs
// (startFrontend) liefert dann statisch aus, statt je Wächter einen Vite mit
// Fable-Kompilierung zu starten. Argumente nach `--` (Dateien) gehen an
// `node --test` durch; ohne Argumente laufen alle scripts/*.test.mjs.
// Das Build-Verzeichnis wird auch bei rotem Lauf, Fehler und Abbruch entfernt.
import { spawn, execFileSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  USAGE_DIR_ENV,
  OWNER_ENV,
  markedFiles,
  leafTests,
  browserLeaves,
  tagViolations,
  tagSummary,
  summaryLine,
} from "./browser-usage.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "..");

/// Test-Parallelität des Gesamtlaufs. Gemessen (infrastructure-eq27s, 278 Tests):
/// seriell rund 6,5 Minuten, mit 4 Dateien parallel rund 1,9 Minuten, dreimal grün.
/// Parallel ist sicher, weil der statische Server keine Fable-Kompilierung startet.
/// BB_GUARD_CONCURRENCY=<n> überschreibt den Wert (1 = seriell).
export const TEST_CONCURRENCY = Number(process.env.BB_GUARD_CONCURRENCY) || 4;

/// Höchstdauer je Test in Millisekunden. Ohne sie hing ein Lauf drei Stunden ohne Ausgabe
/// (infrastructure-t5m2b): ein hängender Test wird damit rot, statt das Gate anzuhalten.
export const TEST_TIMEOUT_MS = 120000;

/// Die Argumente für `node --test`. Mit `countFile` schreibt ein zweiter Reporter je Test eine
/// Zeile dorthin (Zählung der Browser-Tests), die Ausgabe auf stdout bleibt `spec`.
export function nodeTestArgs(files, countFile) {
  const reporters = countFile
    ? ["--test-reporter=spec", "--test-reporter-destination=stdout",
       `--test-reporter=${path.join(here, "test-count-reporter.mjs")}`, `--test-reporter-destination=${countFile}`]
    : [];
  return ["--test", `--test-concurrency=${TEST_CONCURRENCY}`, `--test-timeout=${TEST_TIMEOUT_MS}`, ...reporters, ...files];
}

/// Umgebung für einen gezählten Lauf: der Hook landet in jedem Node-Prozess darunter.
export function countingEnv(env, usageDir) {
  const hook = `--import=${path.join(here, "browser-usage-hook.mjs")}`;
  const nodeOptions = [env.NODE_OPTIONS, hook].filter(Boolean).join(" ");
  const counted = { ...env, NODE_OPTIONS: nodeOptions, [USAGE_DIR_ENV]: usageDir };
  // Ein Lauf innerhalb eines Tests (Gegenbeweis) beginnt eine eigene Zuordnung.
  delete counted[OWNER_ENV];
  delete counted.NODE_TEST_CONTEXT;
  return counted;
}

/// Lässt `node --test` laufen, prüft die Kennungen der Browser-Tests und zählt sie je Kennung. Gibt
/// Exit-Code, Zusammenfassung und Verstöße zurück.
export async function runCounted(files, env, { flows, stdio = "inherit" } = {}) {
  const work = fs.mkdtempSync(path.join(os.tmpdir(), "bb-browser-usage-"));
  const usageDir = path.join(work, "marks");
  const countFile = path.join(work, "tests.jsonl");
  fs.mkdirSync(usageDir);
  try {
    const code = await runNodeTest(files, countingEnv(env, usageDir), { countFile, stdio });
    const lines = fs.existsSync(countFile) ? fs.readFileSync(countFile, "utf8").split("\n") : [];
    const leaves = browserLeaves(leafTests(lines), markedFiles(usageDir));
    const summary = tagSummary(leaves);
    const violations = tagViolations(leaves, flows);
    return { code, summary, violations, gateCode: gateExitCode(code, violations) };
  } finally {
    fs.rmSync(work, { recursive: true, force: true });
  }
}

/// Exit-Code des Gates: rot, wenn ein Test rot ist oder ein Browser-Test gegen die Kennungsregel
/// verstößt (Doktrin Punkt 5). Die Zahl der Browser-Tests macht das Gate nicht rot.
export function gateExitCode(code, violations) {
  return code || (violations.length > 0 ? 1 : 0);
}

/// Der Lauf im Gate: meldet die Zahl je Kennung und jeden Verstoß.
async function runGate(files, env) {
  const { summary, violations, gateCode } = await runCounted(files, env);
  console.log(summaryLine(summary));
  for (const violation of violations) console.error(`Browser-Wächter rot: ${violation}`);
  return gateCode;
}

export function resolveTestFiles(args, dir = here) {
  if (args.length > 0) return args;
  return fs
    .readdirSync(dir)
    .filter((f) => f.endsWith(".test.mjs"))
    .sort()
    .map((f) => path.join("scripts", f));
}

export function buildClient(outDir) {
  execFileSync("npx", ["vite", "build", "--outDir", outDir, "--emptyOutDir"], {
    cwd: repoRoot,
    stdio: "inherit",
    timeout: 300000,
  });
}

function runNodeTest(files, env, { countFile, stdio = "inherit" } = {}) {
  return new Promise((resolve) => {
    const child = spawn(
      process.execPath,
      nodeTestArgs(files, countFile),
      { cwd: repoRoot, env, stdio }
    );
    const forward = (sig) => () => child.kill(sig);
    const onInt = forward("SIGINT");
    const onTerm = forward("SIGTERM");
    process.on("SIGINT", onInt);
    process.on("SIGTERM", onTerm);
    child.on("error", () => resolve(1));
    child.on("exit", (code, signal) => {
      process.off("SIGINT", onInt);
      process.off("SIGTERM", onTerm);
      resolve(code ?? (signal ? 1 : 0));
    });
  });
}

/// Baut, lässt `run` mit BB_GUARD_DIST laufen, räumt immer auf, gibt den
/// Exit-Code von `run` zurück. Die Funktionen sind austauschbar (Test).
export async function runWithDist({
  build = buildClient,
  run = runGate,
  files,
  baseEnv = process.env,
  makeDir = () => fs.mkdtempSync(path.join(os.tmpdir(), "bb-guard-dist-")),
}) {
  const dist = makeDir();
  try {
    build(dist);
    return await run(files, { ...baseEnv, BB_GUARD_DIST: dist });
  } finally {
    fs.rmSync(dist, { recursive: true, force: true });
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2).filter((a) => a !== "--");
  const files = resolveTestFiles(args);
  let code = 1;
  try {
    code = await runWithDist({ files });
  } catch (err) {
    console.error(err.message);
  }
  process.exit(code);
}
