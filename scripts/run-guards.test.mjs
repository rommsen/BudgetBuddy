// Tests für scripts/run-guards.mjs (infrastructure-eq27s).
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { runWithDist, resolveTestFiles, nodeTestArgs, TEST_TIMEOUT_MS } from "./run-guards.mjs";

const makeDir = () => fs.mkdtempSync(path.join(os.tmpdir(), "bb-run-guards-test-"));

test("setzt BB_GUARD_DIST auf das gebaute Verzeichnis und baut genau einmal", async () => {
  let builds = 0;
  let seen;
  await runWithDist({
    files: ["a.test.mjs"], baseEnv: {},
    makeDir,
    build: () => { builds++; },
    run: async (files, env) => { seen = { files, dist: env.BB_GUARD_DIST }; return 0; },
  });
  assert.equal(builds, 1);
  assert.deepEqual(seen.files, ["a.test.mjs"]);
  assert.match(seen.dist, /bb-run-guards-test-/);
});

test("gibt den Exit-Code des Testlaufs zurück und räumt bei rotem Lauf auf", async () => {
  let dist;
  const code = await runWithDist({
    files: [], makeDir, baseEnv: {},
    build: (d) => { dist = d; },
    run: async () => 3,
  });
  assert.equal(code, 3);
  assert.equal(fs.existsSync(dist), false);
});

test("räumt auch auf, wenn der Build oder der Lauf wirft", async () => {
  let dist;
  await assert.rejects(runWithDist({
    files: [], makeDir, baseEnv: {},
    build: (d) => { dist = d; throw new Error("build kaputt"); },
    run: async () => 0,
  }), /build kaputt/);
  assert.equal(fs.existsSync(dist), false);
});

test("reicht Dateiargumente durch, sonst alle *.test.mjs unter scripts/", () => {
  assert.deepEqual(resolveTestFiles(["scripts/x.test.mjs"]), ["scripts/x.test.mjs"]);
  const all = resolveTestFiles([]);
  assert.ok(all.includes(path.join("scripts", "run-guards.test.mjs")));
  assert.ok(all.every((f) => f.endsWith(".test.mjs")));
});

test("jeder Test hat eine Zeitgrenze, ein hängender Test hält das Gate nicht an", () => {
  const args = nodeTestArgs(["scripts/x.test.mjs"]);
  assert.ok(args.includes(`--test-timeout=${TEST_TIMEOUT_MS}`));
  assert.ok(TEST_TIMEOUT_MS > 0 && TEST_TIMEOUT_MS <= 300000);
  assert.equal(args.at(-1), "scripts/x.test.mjs");
});

// infrastructure-shsvf: ein fertiges Bundle aus `npm run build` wird übernommen, nicht neu gebaut.
const makeBundle = () => {
  const dir = makeDir();
  fs.writeFileSync(path.join(dir, "index.html"), "<!doctype html>");
  return dir;
};

test("nimmt ein fertiges Bundle aus BB_GUARD_DIST an: kein Build, kein Löschen", async () => {
  const bundle = makeBundle();
  try {
    let builds = 0;
    let seen;
    const code = await runWithDist({
      files: ["a.test.mjs"],
      baseEnv: { BB_GUARD_DIST: bundle },
      makeDir: () => { throw new Error("makeDir darf nicht laufen"); },
      build: () => { builds++; },
      run: async (files, env) => { seen = env.BB_GUARD_DIST; return 0; },
    });
    assert.equal(code, 0);
    assert.equal(builds, 0);
    assert.equal(seen, bundle);
    assert.equal(fs.existsSync(path.join(bundle, "index.html")), true);
  } finally {
    fs.rmSync(bundle, { recursive: true, force: true });
  }
});

test("ein fremdes Bundle bleibt auch bei rotem Lauf und bei Wurf liegen", async () => {
  const bundle = makeBundle();
  try {
    const opts = { files: [], baseEnv: { BB_GUARD_DIST: bundle }, build: () => { throw new Error("kein Build"); } };
    assert.equal(await runWithDist({ ...opts, run: async () => 3 }), 3);
    assert.equal(fs.existsSync(path.join(bundle, "index.html")), true);
    await assert.rejects(runWithDist({ ...opts, run: async () => { throw new Error("lauf kaputt"); } }), /lauf kaputt/);
    assert.equal(fs.existsSync(path.join(bundle, "index.html")), true);
  } finally {
    fs.rmSync(bundle, { recursive: true, force: true });
  }
});

test("BB_GUARD_DIST ohne index.html ist ein Fehler, der den Pfad nennt; kein Test läuft", async () => {
  const empty = makeDir();
  try {
    let ran = false;
    await assert.rejects(
      runWithDist({ files: [], baseEnv: { BB_GUARD_DIST: empty }, build: () => {}, run: async () => { ran = true; return 0; } }),
      (err) => err.message.includes(empty) && /index\.html/.test(err.message),
    );
    assert.equal(ran, false);
    assert.equal(fs.existsSync(empty), true);
  } finally {
    fs.rmSync(empty, { recursive: true, force: true });
  }
});

test("run-guards.mjs bricht bei BB_GUARD_DIST ohne index.html mit Exit ungleich 0 und Pfad in der Meldung ab", () => {
  const empty = makeDir();
  try {
    const result = spawnSync(process.execPath, [path.join(import.meta.dirname, "run-guards.mjs"), "--", "scripts/gibt-es-nicht.test.mjs"], {
      env: { ...process.env, BB_GUARD_DIST: empty },
      encoding: "utf8",
      timeout: 30000,
    });
    assert.notEqual(result.status, 0);
    assert.ok(result.stderr.includes(empty), result.stderr);
    assert.doesNotMatch(result.stdout, /# tests/);
  } finally {
    fs.rmSync(empty, { recursive: true, force: true });
  }
});

test("verify.sh reicht das Bundle aus Schritt 3 nur bei grünem Build an Schritt 6 weiter", () => {
  const source = fs.readFileSync(path.join(import.meta.dirname, "..", "verify.sh"), "utf8");
  const step3 = source.slice(source.indexOf("# ── 3."), source.indexOf("# ── 4./5."));
  const step6 = source.slice(source.indexOf("# ── 6."), source.indexOf("# ── Ergebnis"));
  // Das Flag wird nur im grünen Zweig von Schritt 3 gesetzt.
  const okBranch = step3.slice(step3.indexOf("then"), step3.indexOf("else"));
  assert.match(step3, /BUILD_DIST=""/);
  assert.match(okBranch, /BUILD_DIST="\$ROOT\/dist\/public"/);
  // Schritt 6 setzt BB_GUARD_DIST nur mit dem Flag, sonst ohne Variable.
  assert.match(step6, /if \[ -n "\$BUILD_DIST" \]/);
  assert.match(step6, /env BB_GUARD_DIST="\$BUILD_DIST" npm run test:scripts/);
  assert.match(step6, /env -u BB_GUARD_DIST npm run test:scripts/);
  // build.outDir in vite.config.js zeigt auf dasselbe Verzeichnis.
  const vite = fs.readFileSync(path.join(import.meta.dirname, "..", "vite.config.js"), "utf8");
  assert.match(vite, /outDir:\s*['"]\.\.\/\.\.\/dist\/public['"]/);
});
