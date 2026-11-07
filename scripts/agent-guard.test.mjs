// scripts/agent-guard.test.mjs
//
// Hängt den Selbsttest des Agent-Wächters (.claude/hooks/agent-guard.test.sh) an
// `npm run test:scripts`, damit er in verify.sh mitläuft. Der Wächter lehnt Agents mit
// `name` ab: sie liefen als Teammate weiter und blieben nach ihrer Arbeit als Prozess stehen.
// Der Selbsttest füttert den Hook nur mit simulierten Eingaben.
import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

test("agent-guard.test.sh ist grün (Agent mit name abgelehnt, ohne name frei)", () => {
  const r = spawnSync("bash", [path.join(repoRoot, ".claude/hooks/agent-guard.test.sh")], {
    encoding: "utf8",
    timeout: 60000,
  });
  assert.equal(r.status, 0, `${r.stdout}\n${r.stderr}`);
  assert.match(r.stdout, /0 fehlgeschlagen/);
});
