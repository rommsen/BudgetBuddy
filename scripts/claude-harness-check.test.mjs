// scripts/claude-harness-check.test.mjs
//
// Wächter für die Harness-Dateien (infrastructure-cbr0r, Harness-Audit M10): Rule-Pfade
// treffen Dateien, Agents haben lesbares Frontmatter mit name und description, Verweise
// zeigen nicht ins Leere, in Agents und standards/ steht keine DaisyUI- oder
// Tailwind-3-Syntax. Die Gegenbeweise laufen gegen synthetische Fixtures in einem
// temporären Verzeichnis, nie gegen den Live-Baum.
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  repoRoot,
  checkHarness,
  extractFrontmatter,
  parseFrontmatter,
} from "./claude-harness-check.mjs";

function fixture(files) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "bb-harness-"));
  for (const [rel, content] of Object.entries(files)) {
    const p = path.join(root, rel);
    fs.mkdirSync(path.dirname(p), { recursive: true });
    fs.writeFileSync(p, content);
  }
  return root;
}

const validAgent = "---\nname: test-reviewer\ndescription: Prüft Tests gegen die Doktrin.\ntools: Read, Grep\n---\n\nText.\n";

test("Live-Baum: Rules, Agents und CLAUDE.md ohne Funde", () => {
  const problems = checkHarness(repoRoot);
  assert.deepEqual(problems, []);
});

test("Live-Baum: jeder Agent unter .claude/agents hat name und description", () => {
  const dir = path.join(repoRoot, ".claude", "agents");
  const files = fs.readdirSync(dir).filter((f) => f.endsWith(".md"));
  assert.ok(files.length > 0, "keine Agents gefunden");
  for (const f of files) {
    const fm = extractFrontmatter(fs.readFileSync(path.join(dir, f), "utf8"));
    assert.ok(fm && fm.closed, `${f}: kein geschlossenes Frontmatter`);
    const { data, errors } = parseFrontmatter(fm.lines);
    assert.deepEqual(errors, [], f);
    assert.equal(data.name, f.replace(/\.md$/, ""), `${f}: name passt nicht zum Dateinamen`);
    assert.ok(data.description.length > 20, `${f}: description zu kurz`);
  }
});

test("Live-Baum: keine DaisyUI-Klassen und keine Tailwind-3-Syntax in Agents und standards", () => {
  const forbidden = /btn-|card-body|badge-|@tailwind|tailwind\.config|bg-opacity-|daisyui/i;
  // Erlaubt: die Feststellung "kein DaisyUI" (styleguide.md, ADR 0031).
  const allowed = /kein DaisyUI/g;
  const files = [];
  const walk = (d) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const p = path.join(d, e.name);
      if (e.isDirectory()) walk(p);
      else if (e.name.endsWith(".md")) files.push(p);
    }
  };
  walk(path.join(repoRoot, ".claude", "agents"));
  walk(path.join(repoRoot, "standards"));
  const hits = [];
  for (const f of files) {
    fs.readFileSync(f, "utf8")
      .split("\n")
      .forEach((line, i) => {
        if (forbidden.test(line.replace(allowed, ""))) hits.push(`${path.relative(repoRoot, f)}:${i + 1}`);
      });
  }
  assert.deepEqual(hits, []);
});

test("Gegenbeweis: Rule mit totem paths-Eintrag wird gemeldet, gültiger nicht", () => {
  const root = fixture({
    "src/Server/Ledger/Domain.fs": "module X\n",
    ".claude/rules/tot.md": "---\npaths: src/Server/Domain.fs\n---\n\n# Tot\n",
    ".claude/rules/liste.md": '---\npaths:\n  - "src/Server/**"\n  - "src/Tests/E2E/**"\n---\n\n# Liste\n',
    ".claude/rules/gut.md": "---\npaths: src/Server/**\n---\n\n# Gut\n",
  });
  const problems = checkHarness(root);
  assert.deepEqual(problems.sort(), [
    '.claude/rules/liste.md: paths "src/Tests/E2E/**" trifft keine Datei',
    '.claude/rules/tot.md: paths "src/Server/Domain.fs" trifft keine Datei',
  ]);
  fs.rmSync(root, { recursive: true, force: true });
});

test("Gegenbeweis: auskommentierte Rule wird gemeldet", () => {
  const root = fixture({ ".claude/rules/aus.md": "<!--\n---\npaths: src/**\n---\n-->\n" });
  assert.deepEqual(checkHarness(root), [".claude/rules/aus.md: Inhalt ist auskommentiert"]);
  fs.rmSync(root, { recursive: true, force: true });
});

test("Gegenbeweis: Agent mit mehrzeiliger Beschreibung ohne Einrückung (alter Stand) wird gemeldet", () => {
  // So sahen backend-code-quality, frontend-architecture und design-quality vor M10 aus.
  const broken =
    "---\nname: backend-code-quality\ndescription: Use this agent to review code.\n\nExamples:\n\n<example>\nuser: \"Review\"\n</example>\ntools: Read\n---\n";
  const root = fixture({ ".claude/agents/backend-code-quality.md": broken, ".claude/agents/test-reviewer.md": validAgent });
  const problems = checkHarness(root);
  assert.ok(problems.length > 0);
  assert.ok(problems.every((p) => p.startsWith(".claude/agents/backend-code-quality.md:")), problems.join("\n"));
  assert.ok(problems.some((p) => /Spalte 0/.test(p)), problems.join("\n"));
  fs.rmSync(root, { recursive: true, force: true });
});

test("Gegenbeweis: Agent ohne name, ohne Frontmatter und mit ': ' im Wert wird gemeldet", () => {
  const root = fixture({
    ".claude/agents/ohne-name.md": "---\ndescription: Prüft etwas.\n---\n",
    ".claude/agents/ohne-fm.md": "# Nur Text\n",
    ".claude/agents/doppelpunkt.md": "---\nname: doppelpunkt\ndescription: Prüft: Tests\n---\n",
    ".claude/agents/zitiert.md": '---\nname: zitiert\ndescription: "Prüft: Tests"\n---\n',
  });
  assert.deepEqual(checkHarness(root).sort(), [
    '.claude/agents/doppelpunkt.md: Zeile 3: ": " in einem Wert ohne Anführungszeichen',
    ".claude/agents/ohne-fm.md: kein Frontmatter",
    '.claude/agents/ohne-name.md: Feld "name" fehlt oder ist leer',
  ]);
  fs.rmSync(root, { recursive: true, force: true });
});

test("Gegenbeweis: Verweis auf fehlende Datei in Agent und CLAUDE.md wird gemeldet", () => {
  const root = fixture({
    "standards/frontend/styleguide.md": "# S\n",
    "src/Server/Api.fs": "module A\n",
    ".claude/agents/test-reviewer.md": validAgent + "Lies `standards/frontend/styleguide.md` und `src/Server/Api.fs:12`, nicht `standards/testing/overview.md`.\n",
    "CLAUDE.md": "| Tests | `standards/testing/overview.md` |\n| UI | standards/frontend/styleguide.md |\n",
  });
  assert.deepEqual(checkHarness(root).sort(), [
    '.claude/agents/test-reviewer.md: Verweis "standards/testing/overview.md" zeigt ins Leere',
    'CLAUDE.md: Verweis "standards/testing/overview.md" zeigt ins Leere',
  ]);
  fs.rmSync(root, { recursive: true, force: true });
});

test("Frontmatter-Leser: Listen, Block-Skalar und Unterblock", () => {
  const { data, errors } = parseFrontmatter([
    "paths:",
    '  - "src/Server/**"',
    "  - src/Shared/Api.fs",
    "description: >",
    "  zwei: Zeilen",
    "  Text",
    "hooks:",
    "  Stop:",
    "    - hooks:",
    "        - type: command",
  ]);
  assert.deepEqual(errors, []);
  assert.deepEqual(data.paths, ["src/Server/**", "src/Shared/Api.fs"]);
  assert.equal(data.description, "zwei: Zeilen\nText");
  assert.equal(data.hooks, null);
});
