// Tests für scripts/run-guards.mjs (infrastructure-eq27s).
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { runWithDist, resolveTestFiles } from "./run-guards.mjs";

const makeDir = () => fs.mkdtempSync(path.join(os.tmpdir(), "bb-run-guards-test-"));

test("setzt BB_GUARD_DIST auf das gebaute Verzeichnis und baut genau einmal", async () => {
  let builds = 0;
  let seen;
  await runWithDist({
    files: ["a.test.mjs"],
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
    files: [], makeDir,
    build: (d) => { dist = d; },
    run: async () => 3,
  });
  assert.equal(code, 3);
  assert.equal(fs.existsSync(dist), false);
});

test("räumt auch auf, wenn der Build oder der Lauf wirft", async () => {
  let dist;
  await assert.rejects(runWithDist({
    files: [], makeDir,
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
