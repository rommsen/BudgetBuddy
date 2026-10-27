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

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, "..");

/// Test-Parallelität des Gesamtlaufs. Gemessen (infrastructure-eq27s, 278 Tests):
/// seriell rund 6,5 Minuten, mit 4 Dateien parallel rund 1,9 Minuten, dreimal grün.
/// Parallel ist sicher, weil der statische Server keine Fable-Kompilierung startet.
/// BB_GUARD_CONCURRENCY=<n> überschreibt den Wert (1 = seriell).
export const TEST_CONCURRENCY = Number(process.env.BB_GUARD_CONCURRENCY) || 4;

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

function runNodeTest(files, env) {
  return new Promise((resolve) => {
    const child = spawn(
      process.execPath,
      ["--test", `--test-concurrency=${TEST_CONCURRENCY}`, ...files],
      { cwd: repoRoot, env, stdio: "inherit" }
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
  run = runNodeTest,
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
