// scripts/readme-delta-check.test.mjs
//
// Tests für das Gate gegen leise danebengehende README-Deltas (ADR 0040,
// infrastructure-b6kzp). Läuft über Node's eingebauten Test-Runner:
//
//   node --test scripts/readme-delta-check.test.mjs
//
// Alle Fixtures sind SYNTHETISCH (in einem temporären Verzeichnis
// zusammengebaut, nie gegen den Live-Baum unter .agentheim/), genau wie
// `lib/test/readme-delta.test.mjs` es selbst hält, damit eine legitime
// README-Änderung diese Tests nicht rot macht. T1 bis T6, T8 verlassen sich
// auf das ECHTE, auf dieser Maschine installierte Plugin-Modul (kein Mock,
// keine eigene Kopie der Grammatik, genau das ist der Punkt des Gates); T7
// und T9 zeigen das Lade-/Auflösungsverhalten über eine eigens präparierte
// (leere bzw. doppelt versionierte) Fake-Cache-Wurzel, per $HOME überschrieben.

import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync, cpSync, existsSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  cacheRoot,
  pickNewestVersion,
  resolveReadmeDeltaModule,
  findSectionRange,
  evaluateOp,
} from "./readme-delta-check.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SCRIPT = path.join(__dirname, "readme-delta-check.mjs");
const REPO_ROOT = path.join(__dirname, "..");

const DEMO_README = `# demo

## Ereignisse
- **AlphaEvent** passiert wenn etwas.
- **BetaEvent** passiert wenn anderes.

Das ist Fließtext am Ende des Abschnitts.

## BacktickEreignisse
- \`GammaEvent\` passiert wenn drittens.
- \`DeltaEvent\` passiert wenn viertens.

## Sichten
- **Foo:** macht foo.
- **Bar:** macht bar.
  - Unterbullet mit Einrückung
- **Baz:** macht baz.
`;

/** Baut ein frisches, synthetisches Repo-Verzeichnis mit .agentheim/knowledge/contexts/<bc>/README.md. */
function makeFixtureRepo(bc = "demo", readmeContent = DEMO_README) {
  const dir = mkdtempSync(path.join(os.tmpdir(), "readme-delta-check-fixture-"));
  const contextDir = path.join(dir, ".agentheim", "knowledge", "contexts", bc);
  mkdirSync(contextDir, { recursive: true });
  writeFileSync(path.join(contextDir, "README.md"), readmeContent, "utf8");
  return dir;
}

function runGate({ cwd, bc = "demo", delta, homedir }) {
  const deltaPath = path.join(cwd, "delta.json");
  writeFileSync(deltaPath, JSON.stringify(delta), "utf8");
  const env = { ...process.env };
  if (homedir) env.HOME = homedir;
  const result = spawnSync(process.execPath, [SCRIPT, "--bc", bc, "--delta", deltaPath], {
    cwd,
    env,
    encoding: "utf8",
  });
  return { status: result.status, stdout: result.stdout, stderr: result.stderr };
}

// ---------------------------------------------------------------------------
// T1: ledger-t7v2q nachgestellt: append in einen Abschnitt, der auf
// Fließtext endet, muss Exit 1 mit Grund "hinter Fließtext" ergeben.
// ---------------------------------------------------------------------------
test("T1: append hinter Fließtext wird abgelehnt (ledger-t7v2q)", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Ereignisse",
          ops: [{ op: "append", body: "- **EpsilonEvent** passiert wenn fünftens." }],
        },
      ],
    });
    assert.equal(res.status, 1);
    assert.match(res.stderr, /hinter Fließtext/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T2: design-system-m4kp7 nachgestellt: replace, dessen expected nur einen
// Teil des Extents deckt, muss Exit 1 mit Grund "merged" ergeben.
// ---------------------------------------------------------------------------
test("T2: replace mit partiellem expected wird als merged abgelehnt (design-system-m4kp7)", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [
            {
              op: "replace",
              anchor: "Bar:",
              // Nur die erste Zeile, der echte Extent umfasst auch den Unterbullet.
              expected: "- **Bar:** macht bar.",
              body: "- **Bar:** macht bar neu.",
            },
          ],
        },
      ],
    });
    assert.equal(res.status, 1);
    assert.match(res.stderr, /merged/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T3: ledger-ezrw0 nachgestellt: replace mit flachgeklopftem Body,
// Disposition applied, muss trotzdem Exit 1 ergeben (Prüfung 4 und 5).
// ---------------------------------------------------------------------------
test("T3: replace mit applied-Disposition, aber verlorener Einrückung, wird trotzdem abgelehnt (ledger-ezrw0, der Kern)", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [
            {
              op: "replace",
              anchor: "Bar:",
              expected: "- **Bar:** macht bar.\n  - Unterbullet mit Einrückung",
              // Voller Extent als expected (das Werkzeug meldet "applied"), aber
              // der Unterbullet wird auf Spalte 0 flachgeklopft, genau der Bug.
              body: "- **Bar:** macht bar neu.\n- Unterbullet ohne Einrückung mehr",
            },
          ],
        },
      ],
    });
    assert.equal(res.status, 1);
    // Prüfung 4 (Bullet-Budget) greift hier zuerst, weil das Flachklopfen einen
    // zusätzlichen Spalte-0-Bullet erzeugt, der Punkt: das Werkzeug selbst hat
    // "applied" gemeldet, das Gate widerspricht trotzdem.
    assert.match(res.stderr, /Prüfung 4|Prüfung 5/);
    assert.doesNotMatch(res.stderr, /^merged$/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("T3b: reine Einrückungs-Verletzung (Budget bleibt gleich) greift eigenständig über Prüfung 5", () => {
  const readme = `# demo

## Sichten
- **Qux:** macht qux.
    - tief eingerückter Unterbullet
`;
  const dir = makeFixtureRepo("demo", readme);
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [
            {
              op: "replace",
              anchor: "Qux:",
              expected: "- **Qux:** macht qux.\n    - tief eingerückter Unterbullet",
              body: "- **Qux:** macht qux neu.\n  - weniger tief eingerückter Unterbullet",
            },
          ],
        },
      ],
    });
    assert.equal(res.status, 1);
    assert.match(res.stderr, /Prüfung 5/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T4: falscher Abschnittsname: Exit 1, Meldung nennt die vorhandenen
// Überschriften.
// ---------------------------------------------------------------------------
test("T4: unbekannter Abschnitt nennt die vorhandenen Überschriften", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [{ op: "append", body: "- **Neu:** x." }],
        },
        {
          document: "README.md",
          section: "NichtVorhanden",
          ops: [{ op: "append", body: "- **Y:** y." }],
        },
      ],
    });
    assert.equal(res.status, 1);
    assert.match(res.stderr, /Ereignisse/);
    assert.match(res.stderr, /Sichten/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T5: replace auf ein backtick-geführtes Bullet: Exit 1, Meldung nennt die
// erreichbaren termHeads.
// ---------------------------------------------------------------------------
test("T5: replace auf ein backtick-geführtes Bullet nennt die erreichbaren termHeads", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "BacktickEreignisse",
          ops: [
            {
              op: "replace",
              anchor: "GammaEvent",
              expected: "- `GammaEvent` passiert wenn drittens.",
              body: "- `GammaEvent` passiert jetzt anders.",
            },
          ],
        },
      ],
    });
    assert.equal(res.status, 1);
    assert.match(res.stderr, /nicht erreichbar/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T6: Gegenprobe: ein sauberes append ans Listenende und ein sauberer
// replace über den vollen Extent ergeben Exit 0, einen nicht leeren Diff und
// die Versionszeile.
// ---------------------------------------------------------------------------
test("T6: sauberes append und sauberer replace ergeben Exit 0 mit Diff und Versionszeile", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [
            { op: "append", body: "- **Qux:** macht qux." },
            {
              op: "replace",
              anchor: "Bar:",
              expected: "- **Bar:** macht bar.\n  - Unterbullet mit Einrückung",
              body: "- **Bar:** macht bar neu.\n  - Unterbullet mit Einrückung, angepasst",
            },
          ],
        },
      ],
    });
    assert.equal(res.status, 0);
    assert.match(res.stdout, /^Plugin-Version: \d+\.\d+\.\d+/);
    assert.ok(res.stdout.includes("+- **Qux:** macht qux."));
    assert.ok(res.stdout.includes("+- **Bar:** macht bar neu."));
    assert.notEqual(res.stdout.trim().length, 0);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T7: Lader, Fehlerfall: leere oder fehlende Cache-Wurzel ergibt Exit 2 mit
// benannter Ursache, kein Skip, kein Exit 0.
// ---------------------------------------------------------------------------
test("T7: leere Cache-Wurzel ergibt Exit 2 mit benannter Ursache, kein stiller Durchlauf", () => {
  const dir = makeFixtureRepo();
  const fakeHome = mkdtempSync(path.join(os.tmpdir(), "readme-delta-check-emptyhome-"));
  try {
    const res = runGate({
      cwd: dir,
      homedir: fakeHome,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [{ op: "append", body: "- **Neu:** x." }],
        },
      ],
    });
    assert.equal(res.status, 2);
    assert.match(res.stderr, /Grund:/);
    assert.match(res.stderr, new RegExp(fakeHome.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
    assert.equal(res.stdout, "");
  } finally {
    rmSync(dir, { recursive: true, force: true });
    rmSync(fakeHome, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T8: Kopplung an die Verfahrensregel: CLAUDE.md enthält den Aufrufbefehl
// und den Skriptpfad wörtlich.
// ---------------------------------------------------------------------------
test("T8: CLAUDE.md enthält den Aufrufbefehl und den Skriptpfad wörtlich", () => {
  const claudeMd = readFileSync(path.join(REPO_ROOT, "CLAUDE.md"), "utf8");
  assert.ok(claudeMd.includes("npm run check:readme-delta"), 'CLAUDE.md muss "npm run check:readme-delta" enthalten');
  assert.ok(
    claudeMd.includes("scripts/readme-delta-check.mjs"),
    'CLAUDE.md muss den Pfad "scripts/readme-delta-check.mjs" enthalten'
  );
});

// ---------------------------------------------------------------------------
// T9: Versionsauflösung: bei Fake-Verzeichnissen 0.9.4 und 0.10.0 wird
// 0.10.0 gewählt (numerischer Semver-Vergleich, nicht lexikalisch).
// ---------------------------------------------------------------------------
test("T9: Versionsauflösung wählt 0.10.0 vor 0.9.4 (numerisch, nicht lexikalisch)", () => {
  // Unit-Ebene: die reine Auflösungsfunktion.
  assert.equal(pickNewestVersion(["0.9.4", "0.10.0"]), "0.10.0");
  assert.equal(pickNewestVersion(["0.10.0", "0.9.4"]), "0.10.0");
  assert.equal(pickNewestVersion(["0.8.10", "0.8.9", "0.8.3"]), "0.8.10");
  assert.equal(pickNewestVersion(["nicht-semver", "auch-nicht"]), null);

  // End-to-End: eine echte Fake-Cache-Wurzel mit zwei funktionsfähigen
  // Versionsverzeichnissen (beide eine Kopie des echten Moduls, nur die
  // Verzeichnisnamen unterscheiden sich), damit die Auflösung selbst,
  // nicht die Modul-Funktionalität, geprüft wird.
  const realRoot = cacheRoot(os.homedir());
  const realResolved = resolveReadmeDeltaModule(realRoot);
  assert.ok(realResolved.modulePath, "dieser Test braucht ein installiertes Agentheim-Plugin auf der Testmaschine");

  const fakeHome = mkdtempSync(path.join(os.tmpdir(), "readme-delta-check-fakehome-"));
  const fakeRoot = cacheRoot(fakeHome);
  for (const v of ["0.9.4", "0.10.0"]) {
    const libDir = path.join(fakeRoot, v, "lib");
    mkdirSync(libDir, { recursive: true });
    cpSync(realResolved.modulePath, path.join(libDir, "readme-delta.mjs"));
  }

  const dir = makeFixtureRepo();
  try {
    assert.equal(resolveReadmeDeltaModule(fakeRoot).version, "0.10.0");

    const res = runGate({
      cwd: dir,
      homedir: fakeHome,
      delta: [
        {
          document: "README.md",
          section: "Sichten",
          ops: [{ op: "append", body: "- **Neu:** x." }],
        },
      ],
    });
    assert.equal(res.status, 0);
    assert.match(res.stdout, /^Plugin-Version: 0\.10\.0/);
  } finally {
    rmSync(dir, { recursive: true, force: true });
    rmSync(fakeHome, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// T10: ein missgebildeter Op darf nie unabgefangen an applyReadmeDelta()
// durchgereicht werden (Verifier-Fund, Iteration 1 → 2). lib/readme-delta.mjs
// wirft in allen drei Fällen (unbekannte op.op, fehlendes body, fehlendes
// anchor/expected bei replace); ohne Absicherung wäre das ein Exit 1 mit
// Stack-Trace statt des benannten Exit 2. Drei Fälle, weil jeder einen
// anderen Zweig von validateOpShape() trifft: unbekannte op.op, fehlendes
// body bei append, fehlendes body/anchor/expected bei replace.
// ---------------------------------------------------------------------------
test("T10a: unbekannte op.op ergibt Exit 2, nicht Exit 1 mit Stack-Trace", () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [{ document: "README.md", section: "Sichten", ops: [{ op: "bogus", body: "- **Neu:** x." }] }],
    });
    assert.equal(res.status, 2);
    assert.match(res.stderr, /Grund:/);
    assert.match(res.stderr, /unbekannte Op "bogus"/);
    assert.doesNotMatch(res.stderr, /at applyReadmeDelta/);
    assert.equal(res.stdout, "");
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('T10b: append ohne "body" ergibt Exit 2, nicht Exit 1 mit Stack-Trace', () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [{ document: "README.md", section: "Sichten", ops: [{ op: "append" }] }],
    });
    assert.equal(res.status, 2);
    assert.match(res.stderr, /Grund:/);
    assert.match(res.stderr, /"body"/);
    assert.equal(res.stdout, "");
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('T10c: replace ohne "body"/"expected" ergibt Exit 2, nicht Exit 1 mit Stack-Trace', () => {
  const dir = makeFixtureRepo();
  try {
    const res = runGate({
      cwd: dir,
      delta: [{ document: "README.md", section: "Sichten", ops: [{ op: "replace", anchor: "Foo:" }] }],
    });
    assert.equal(res.status, 2);
    assert.match(res.stderr, /Grund:/);
    assert.match(res.stderr, /"body"/);
    assert.equal(res.stdout, "");
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

// ---------------------------------------------------------------------------
// Ergänzend: ein paar reine Helfer-Tests gegen die exportierten Bausteine,
// unabhängig vom CLI-Aufruf (schneller, präziser für Grenzfälle).
// ---------------------------------------------------------------------------

test("cacheRoot leitet sich rein aus dem Homedir ab", () => {
  const root = cacheRoot("/home/roman");
  assert.equal(root, path.join("/home/roman", ".claude", "plugins", "cache", "agentheim", "agentheim"));
});

test("resolveReadmeDeltaModule liefert null mit Diagnose, wenn kein Versionsverzeichnis das Modul enthält", () => {
  const dir = mkdtempSync(path.join(os.tmpdir(), "readme-delta-check-halfwritten-"));
  try {
    mkdirSync(path.join(dir, "0.5.0", "lib"), { recursive: true }); // kein readme-delta.mjs drin
    mkdirSync(path.join(dir, "nicht-semver"), { recursive: true });
    const result = resolveReadmeDeltaModule(dir);
    assert.equal(result.modulePath, null);
    assert.deepEqual(result.searchedVersions, ["0.5.0"]);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("findSectionRange findet einen Abschnitt namentlich und grenzt ihn am nächsten Header ab", () => {
  const lines = DEMO_README.split("\n");
  const range = findSectionRange(lines, "BacktickEreignisse");
  assert.ok(range);
  assert.ok(lines.slice(range.start, range.end).some((l) => l.includes("GammaEvent")));
  assert.ok(!lines.slice(range.start, range.end).some((l) => l.startsWith("## ")));
});

test("evaluateOp: noop-already ist immer ok, unabhängig von Op-Art", () => {
  const result = evaluateOp({
    op: { op: "append", body: "- **Foo:** macht foo." },
    disposition: "noop-already",
    section: "Sichten",
    linesBefore: ["## Sichten", "- **Foo:** macht foo."],
    rangeBefore: { start: 1, end: 2 },
    linesAfter: ["## Sichten", "- **Foo:** macht foo."],
  });
  assert.deepEqual(result, { ok: true });
});

test("evaluateOp: appended-fallback wird als Prüfung 2 mit vorhandenen Überschriften abgelehnt", () => {
  const linesBefore = ["## Eins", "- **A:** a.", "## Zwei", "- **B:** b."];
  const result = evaluateOp({
    op: { op: "append", body: "- **C:** c." },
    disposition: "appended-fallback",
    section: "Drei",
    linesBefore,
    rangeBefore: null,
    linesAfter: [...linesBefore, "- **C:** c."],
  });
  assert.equal(result.ok, false);
  assert.equal(result.check, 2);
  assert.match(result.reason, /Eins/);
  assert.match(result.reason, /Zwei/);
});

if (existsSync(REPO_ROOT)) {
  test("das Gate-Skript selbst existiert unter scripts/readme-delta-check.mjs", () => {
    assert.ok(existsSync(SCRIPT));
  });
}
