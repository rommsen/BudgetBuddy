// scripts/verify-lib.test.mjs
//
// Tests für die Hilfsfunktionen von verify.sh (infrastructure-dvy29): Namen roter
// und übersprungener Tests aus der Ausgabe von `node --test` und aus der trx-Datei
// von `dotnet test`, sowie das Lock um `npm run test:scripts`, das zwei
// gleichzeitige verify.sh-Läufe in verschiedenen Worktrees serialisiert.
//
//   node --test scripts/verify-lib.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import fs from "node:fs";
import { spawnSync, spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const LIB = path.join(here, "verify-lib.sh");

/// Ruft eine Funktion aus verify-lib.sh in einer frischen bash auf.
function lib(script, env = {}) {
  const r = spawnSync("bash", ["-c", `source "${LIB}"; ${script}`], {
    encoding: "utf8",
    env: { ...process.env, ...env },
    timeout: 30000,
  });
  return { code: r.status, out: r.stdout, err: r.stderr };
}

function tmpDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), "bb-verify-lib-test-"));
}

function writeTmp(dir, name, content) {
  const p = path.join(dir, name);
  fs.writeFileSync(p, content);
  return p;
}

// Ausschnitt einer echten Ausgabe von `node --test` (Node 26, spec-Reporter, nicht TTY).
const NODE_LOG_RED = `✔ grün (0.369417ms)
✖ rot eins (0.405542ms)
﹣ übersprungen (0.046625ms) # kein Browser
▶ gruppe
  ✖ innen rot (0.440417ms)
✖ gruppe (0.603667ms)
ℹ tests 5
ℹ suites 0
ℹ pass 1
ℹ fail 3
ℹ cancelled 0
ℹ skipped 1
ℹ todo 0
ℹ duration_ms 81.9125

✖ failing tests:

test at scripts/a.test.mjs:4:1
✖ rot eins (0.405542ms)
  AssertionError [ERR_ASSERTION]: 1 == 2
      at TestContext.<anonymous> (file:///x/a.test.mjs:4:30)

test at scripts/b.test.mjs:6:39
✖ innen rot (0.440417ms)
  Error: x
      at TestContext.<anonymous> (file:///x/b.test.mjs:6:60)
`;

const NODE_LOG_GREEN = `✔ obj/ und bin/ werden nicht geprüft (1.442834ms)
ℹ tests 285
ℹ suites 0
ℹ pass 285
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms 117160.82925
`;

test("vl_node_failed_tests: nennt jeden roten Test mit Datei und Zeile aus dem Abschnitt failing tests", () => {
  const d = tmpDir();
  try {
    const log = writeTmp(d, "node.log", NODE_LOG_RED);
    const r = lib(`vl_node_failed_tests "${log}"`);
    assert.equal(r.code, 0);
    assert.deepEqual(r.out.trim().split("\n"), [
      "rot eins (scripts/a.test.mjs:4:1)",
      "innen rot (scripts/b.test.mjs:6:39)",
    ]);
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("vl_node_failed_tests: grüner Lauf liefert keine Namen", () => {
  const d = tmpDir();
  try {
    const log = writeTmp(d, "node.log", NODE_LOG_GREEN);
    assert.equal(lib(`vl_node_failed_tests "${log}"`).out, "");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("vl_node_count: liest pass, fail und skipped; fehlendes Label ergibt leer statt 0", () => {
  const d = tmpDir();
  try {
    const log = writeTmp(d, "node.log", NODE_LOG_RED);
    assert.equal(lib(`vl_node_count "${log}" pass`).out, "1");
    assert.equal(lib(`vl_node_count "${log}" fail`).out, "3");
    assert.equal(lib(`vl_node_count "${log}" skipped`).out, "1");
    const empty = writeTmp(d, "leer.log", "Build abgebrochen\n");
    assert.equal(lib(`vl_node_count "${empty}" fail`).out, "");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("vl_node_skipped_tests: nennt übersprungene Tests samt Grund", () => {
  const d = tmpDir();
  try {
    const log = writeTmp(d, "node.log", NODE_LOG_RED);
    assert.equal(lib(`vl_node_skipped_tests "${log}"`).out.trim(), "übersprungen # kein Browser");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

const TRX = `<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
    <UnitTestResult testName="Domain.categorize matches rule" outcome="Passed" />
    <UnitTestResult testName="Ledger.split sums to total" outcome="Failed">
      <Output><ErrorInfo><Message>
Expected 100 but was 90
zweite Zeile</Message></ErrorInfo></Output>
    </UnitTestResult>
    <UnitTestResult testName="Import &amp; Sync.dedup keeps first" outcome="Failed" />
    <UnitTestResult testName="YNAB Integration Tests (requires .env).validates token correctly" outcome="NotExecuted" />
    <UnitTestResult testName="Persistence.roundtrip" outcome="NotExecuted" />
  </Results>
</TestRun>
`;

test("vl_trx_tests: liefert die Namen roter Tests aus der trx-Datei, Entities aufgelöst", () => {
  const d = tmpDir();
  try {
    const trx = writeTmp(d, "t.trx", TRX);
    assert.deepEqual(lib(`vl_trx_tests "${trx}" Failed`).out.trim().split("\n"), [
      "Ledger.split sums to total",
      "Import & Sync.dedup keeps first",
    ]);
    assert.deepEqual(lib(`vl_trx_tests "${trx}" NotExecuted`).out.trim().split("\n"), [
      "YNAB Integration Tests (requires .env).validates token correctly",
      "Persistence.roundtrip",
    ]);
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("vl_trx_first_message: erste nichtleere Zeile der Fehlermeldung des n-ten roten Tests", () => {
  const d = tmpDir();
  try {
    const trx = writeTmp(d, "t.trx", TRX);
    assert.equal(lib(`vl_trx_first_message "${trx}" 1`).out.trim(), "Expected 100 but was 90");
    assert.equal(lib(`vl_trx_first_message "${trx}" 2`).out.trim(), "");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("vl_is_optional_skip: nur die opt-in-Integrationstests gegen echte APIs dürfen übersprungen werden", () => {
  assert.equal(lib(`vl_is_optional_skip "YNAB Integration Tests (requires .env).validates token correctly"`).code, 0);
  assert.equal(lib(`vl_is_optional_skip "Comdirect Integration Tests (requires .env).can initiate OAuth flow"`).code, 0);
  assert.notEqual(lib(`vl_is_optional_skip "Persistence.roundtrip"`).code, 0);
});

test("vl_dotnet_count: liest Passed, Failed, Skipped und Total aus der englischen Zusammenfassung", () => {
  const d = tmpDir();
  try {
    const log = writeTmp(
      d,
      "dotnet.log",
      "Passed!  - Failed:     0, Passed:  2502, Skipped:     6, Total:  2508, Duration: 6 s - Tests.dll (net10.0)\n"
    );
    assert.equal(lib(`vl_dotnet_count "${log}" Passed`).out, "2502");
    assert.equal(lib(`vl_dotnet_count "${log}" Failed`).out, "0");
    assert.equal(lib(`vl_dotnet_count "${log}" Skipped`).out, "6");
    assert.equal(lib(`vl_dotnet_count "${log}" Total`).out, "2508");
    const de = writeTmp(d, "de.log", "Bestanden!  : 0 Fehler, 2502 erfolgreich\n");
    assert.equal(lib(`vl_dotnet_count "${de}" Passed`).out, "");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

// ── Lock ────────────────────────────────────────────────────────────────

test("Lock: freies Lock wird genommen, pid steht darin, release entfernt es", () => {
  const d = tmpDir();
  try {
    const lock = path.join(d, "x.lock");
    const r = lib(`vl_lock_acquire "${lock}" && cat "${lock}/pid" && echo "$$" && vl_lock_release "${lock}" && test ! -e "${lock}" && echo frei`);
    assert.equal(r.code, 0, r.err);
    const [pidInLock, ownPid, frei] = r.out.trim().split("\n");
    assert.equal(pidInLock, ownPid);
    assert.equal(frei, "frei");
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("Lock: ein lebender Halter blockiert; nach der Höchstwartezeit endet acquire mit Fehler und nennt den Halter", async () => {
  const d = tmpDir();
  const lock = path.join(d, "x.lock");
  const holder = spawn("bash", ["-c", `source "${LIB}"; vl_lock_acquire "${lock}" && echo bereit && sleep 30`], {
    stdio: ["ignore", "pipe", "pipe"],
  });
  try {
    await new Promise((resolve, reject) => {
      holder.stdout.on("data", (b) => b.toString().includes("bereit") && resolve());
      holder.on("exit", () => reject(new Error("Halter endete zu früh")));
    });
    const r = lib(`vl_lock_acquire "${lock}"`, {
      VERIFY_LOCK_POLL_SECONDS: "1",
      VERIFY_LOCK_MAX_WAIT_SECONDS: "2",
    });
    assert.notEqual(r.code, 0);
    assert.match(r.out + r.err, new RegExp(`PID ${holder.pid}`));
    assert.equal(fs.readFileSync(path.join(lock, "pid"), "utf8").trim(), String(holder.pid));
  } finally {
    holder.kill("SIGKILL");
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("Lock: ein Lock eines toten Prozesses wird übernommen", () => {
  const d = tmpDir();
  try {
    const lock = path.join(d, "x.lock");
    // Ein beendeter Kindprozess liefert eine sicher tote PID.
    const dead = spawnSync("bash", ["-c", "echo $$"], { encoding: "utf8" }).stdout.trim();
    fs.mkdirSync(lock);
    fs.writeFileSync(path.join(lock, "pid"), dead + "\n");
    fs.writeFileSync(path.join(lock, "started"), "Mon Jan  1 00:00:00 2001\n");
    const r = lib(`vl_lock_acquire "${lock}" && cat "${lock}/pid" && echo "$$"`, { VERIFY_LOCK_POLL_SECONDS: "1" });
    assert.equal(r.code, 0, r.err);
    const [inLock, own] = r.out.trim().split("\n").slice(-2);
    assert.equal(inLock, own);
    assert.match(r.out + r.err, /übernommen/);
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("Lock: eine wiederverwendete PID (anderer Startzeitpunkt) gilt als verwaist", () => {
  const d = tmpDir();
  try {
    const lock = path.join(d, "x.lock");
    fs.mkdirSync(lock);
    // process.pid lebt, aber der notierte Startzeitpunkt passt nicht zu ihm.
    fs.writeFileSync(path.join(lock, "pid"), process.pid + "\n");
    fs.writeFileSync(path.join(lock, "started"), "Mon Jan  1 00:00:00 2001\n");
    const r = lib(`vl_lock_acquire "${lock}" && cat "${lock}/pid" && echo "$$"`, {
      VERIFY_LOCK_POLL_SECONDS: "1",
      VERIFY_LOCK_MAX_WAIT_SECONDS: "5",
    });
    assert.equal(r.code, 0, r.out + r.err);
    const [inLock, own] = r.out.trim().split("\n").slice(-2);
    assert.equal(inLock, own);
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("Lock: release lässt ein fremdes Lock stehen", () => {
  const d = tmpDir();
  try {
    const lock = path.join(d, "x.lock");
    fs.mkdirSync(lock);
    fs.writeFileSync(path.join(lock, "pid"), process.pid + "\n");
    const r = lib(`vl_lock_release "${lock}"`);
    assert.equal(r.code, 0);
    assert.ok(fs.existsSync(path.join(lock, "pid")));
  } finally {
    fs.rmSync(d, { recursive: true, force: true });
  }
});

test("Lock: vl_lock_path liegt im gemeinsamen git-Verzeichnis, für alle Worktrees gleich", () => {
  const r = lib(`vl_lock_path "${path.resolve(here, "..")}"`);
  assert.equal(r.code, 0, r.err);
  const common = spawnSync("git", ["-C", here, "rev-parse", "--path-format=absolute", "--git-common-dir"], {
    encoding: "utf8",
  }).stdout.trim();
  assert.equal(r.out.trim(), path.join(common, "bb-verify-test-scripts.lock"));
});
