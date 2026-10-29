// scripts/git-guard.test.mjs
//
// Hängt den Selbsttest des git-Wächters (.claude/hooks/git-guard.test.sh,
// infrastructure-dvy29) an `npm run test:scripts`, damit er in verify.sh mitläuft.
// Der Selbsttest füttert den Hook nur mit simulierten Eingaben und führt kein git aus.
import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("git-guard.test.sh ist grün (Sammel-Staging und commit -a abgelehnt, push fragt nach)", () => {
  const r = spawnSync("bash", [path.join(repoRoot, ".claude/hooks/git-guard.test.sh")], {
    encoding: "utf8",
    timeout: 120000,
  });
  assert.equal(r.status, 0, `${r.stdout}\n${r.stderr}`);
  assert.match(r.stdout, /0 fehlgeschlagen/);
});
