// scripts/worktree-scripts.test.mjs
//
// Prüft scripts/worktree-prep.sh und scripts/worktree-teardown.sh (infrastructure-jcb2m) an
// echten Wegwerf-Worktrees unter <Haupt-Repo>/.worktrees/ auf Branches aw-test/jcb2m-*.
// Jeder Test räumt seinen Worktree und Branch auch im Fehlerfall wieder ab.
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const prepScript = path.join(here, "worktree-prep.sh");
const teardownScript = path.join(here, "worktree-teardown.sh");

function git(args, cwd = here) {
  const r = spawnSync("git", args, { cwd, encoding: "utf8" });
  return { status: r.status, out: (r.stdout || "").trim(), err: (r.stderr || "").trim() };
}

const mainRoot = fs.realpathSync(
  path.dirname(git(["rev-parse", "--path-format=absolute", "--git-common-dir"]).out)
);
const worktreesDir = path.join(mainRoot, ".worktrees");
const mainNodeModules = path.join(mainRoot, "node_modules");

function bash(script, args) {
  const r = spawnSync("bash", [script, ...args], { encoding: "utf8", timeout: 120000 });
  return { status: r.status, output: `${r.stdout}\n${r.stderr}` };
}

function nodeModulesCount() {
  return fs.readdirSync(mainNodeModules).length;
}

function scratchName(tag) {
  return `jcb2mtest-${tag}-${process.pid}-${Math.random().toString(36).slice(2, 7)}`;
}

/// Legt einen Wegwerf-Worktree auf einem eigenen aw-test/-Branch an und räumt danach ab,
/// ohne die Skripte unter Test zu benutzen (Symlinks zuerst, dann remove, dann Branch).
function withScratchWorktree(tag, fn) {
  const name = scratchName(tag);
  const wt = path.join(worktreesDir, name);
  const branch = `aw-test/${name}`;
  fs.mkdirSync(worktreesDir, { recursive: true });
  const add = git(["worktree", "add", "-q", "-b", branch, wt, "HEAD"], mainRoot);
  assert.equal(add.status, 0, add.err);
  try {
    fn({ name, wt, branch });
  } finally {
    const link = path.join(wt, "node_modules");
    try {
      if (fs.lstatSync(link).isSymbolicLink()) fs.unlinkSync(link);
    } catch {}
    if (fs.existsSync(wt)) git(["worktree", "remove", "--force", wt], mainRoot);
    git(["worktree", "prune"], mainRoot);
    git(["branch", "-D", branch], mainRoot);
  }
}

test("prep legt den node_modules-Symlink und .env an, teardown entfernt den Worktree und lässt node_modules stehen", () => {
  const before = nodeModulesCount();
  withScratchWorktree("roundtrip", ({ wt, branch }) => {
    const prep = bash(prepScript, [wt]);
    assert.equal(prep.status, 0, prep.output);

    const link = path.join(wt, "node_modules");
    assert.ok(fs.lstatSync(link).isSymbolicLink(), "node_modules ist kein Symlink");
    assert.equal(fs.readlinkSync(link), mainNodeModules);
    if (fs.existsSync(path.join(mainRoot, ".env"))) {
      assert.ok(fs.existsSync(path.join(wt, ".env")), ".env wurde nicht kopiert");
    }

    // Zweiter Lauf ist harmlos.
    const again = bash(prepScript, [wt]);
    assert.equal(again.status, 0, again.output);

    const down = bash(teardownScript, [wt]);
    assert.equal(down.status, 0, down.output);
    // Symlink-Schritt vor remove.
    assert.ok(down.output.indexOf("[1/4]") < down.output.indexOf("worktree remove"), down.output);
    assert.ok(!fs.existsSync(wt), "Worktree-Ordner existiert noch");
    assert.ok(!git(["worktree", "list", "--porcelain"], mainRoot).out.includes(`worktree ${wt}\n`));
    // Ohne --delete-branch bleibt der Branch stehen.
    assert.equal(git(["show-ref", "--verify", "--quiet", `refs/heads/${branch}`], mainRoot).status, 0);
  });
  assert.equal(nodeModulesCount(), before, "Haupt-node_modules hat Einträge verloren");
});

test("teardown --delete-branch löscht auch den aw/-Branch des Worktrees", () => {
  const name = scratchName("branch");
  const wt = path.join(worktreesDir, name);
  const branch = `aw/${name}`;
  const add = git(["worktree", "add", "-q", "-b", branch, wt, "HEAD"], mainRoot);
  assert.equal(add.status, 0, add.err);
  try {
    assert.equal(bash(prepScript, [wt]).status, 0);
    const down = bash(teardownScript, [wt, "--delete-branch"]);
    assert.equal(down.status, 0, down.output);
    assert.ok(!fs.existsSync(wt));
    assert.notEqual(git(["show-ref", "--verify", "--quiet", `refs/heads/${branch}`], mainRoot).status, 0);
  } finally {
    try {
      fs.unlinkSync(path.join(wt, "node_modules"));
    } catch {}
    if (fs.existsSync(wt)) git(["worktree", "remove", "--force", wt], mainRoot);
    git(["worktree", "prune"], mainRoot);
    git(["branch", "-D", branch], mainRoot);
  }
});

test("teardown verweigert Pfade außerhalb von .worktrees/ und rührt sie nicht an", () => {
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), "jcb2m-outside-"));
  fs.writeFileSync(path.join(outside, "keep.txt"), "x");
  fs.symlinkSync(mainNodeModules, path.join(outside, "node_modules"));
  try {
    for (const target of [outside, mainRoot, worktreesDir, path.join(worktreesDir, ".results"), `.worktrees/../src`]) {
      const r = bash(teardownScript, [target]);
      assert.equal(r.status, 1, `${target}: ${r.output}`);
      assert.match(r.output, /verweigert/);
    }
    assert.ok(fs.existsSync(path.join(outside, "keep.txt")));
    assert.ok(fs.lstatSync(path.join(outside, "node_modules")).isSymbolicLink());
    assert.ok(fs.existsSync(path.join(mainRoot, "package.json")));
  } finally {
    fs.rmSync(outside, { recursive: true, force: true });
  }
});

test("teardown löscht einen nicht mehr eingetragenen Rest-Ordner mit nur obj/- und bin/-Stubs", () => {
  const wt = path.join(worktreesDir, scratchName("objonly"));
  fs.mkdirSync(path.join(wt, "src", "Server", "obj", "Debug"), { recursive: true });
  fs.mkdirSync(path.join(wt, "src", "Tests", "bin"), { recursive: true });
  fs.writeFileSync(path.join(wt, "src", "Server", "obj", "Debug", "project.assets.json"), "{}");
  fs.writeFileSync(path.join(wt, "src", "Tests", "bin", "x.dll"), "");
  try {
    const r = bash(teardownScript, [wt]);
    assert.equal(r.status, 0, r.output);
    assert.ok(!fs.existsSync(wt), "obj/-Rest wurde nicht gelöscht");
  } finally {
    fs.rmSync(wt, { recursive: true, force: true });
  }
});

test("teardown lässt einen Rest-Ordner mit Dateien außerhalb von obj/ und bin/ stehen", () => {
  const wt = path.join(worktreesDir, scratchName("foreign"));
  fs.mkdirSync(path.join(wt, "src", "Server", "obj"), { recursive: true });
  fs.writeFileSync(path.join(wt, "src", "Server", "obj", "stub"), "");
  fs.writeFileSync(path.join(wt, "notes.md"), "ungesichert");
  try {
    const r = bash(teardownScript, [wt]);
    assert.equal(r.status, 1, r.output);
    assert.ok(fs.existsSync(path.join(wt, "notes.md")), "fremde Datei wurde gelöscht");
    assert.match(r.output, /außerhalb von obj\/ und bin\//);
  } finally {
    fs.rmSync(wt, { recursive: true, force: true });
  }
});

test("prep bricht ab, wenn im Worktree ein echter node_modules-Ordner liegt", () => {
  withScratchWorktree("realdir", ({ wt }) => {
    fs.mkdirSync(path.join(wt, "node_modules"));
    fs.writeFileSync(path.join(wt, "node_modules", "marker"), "");
    const r = bash(prepScript, [wt]);
    assert.equal(r.status, 1, r.output);
    assert.ok(fs.existsSync(path.join(wt, "node_modules", "marker")));
    fs.rmSync(path.join(wt, "node_modules"), { recursive: true });
  });
});
