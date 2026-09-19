---
id: infra-sdad8
title: "Modul-Level-initializeDatabase()-Aufrufe in Testcase-Setup verschieben"
status: done
type: refactor
context: infrastructure
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [test-infra, sicherheit]
related_adrs: [0030, 0033]
related_research: []
prior_art: [infra-za7fq]
---

## Why
Der Wächter-Test aus `infra-za7fq` (`src/Tests/TestDatabaseGuardTests.fs`) meldet lautstark,
wenn ein Testlauf `test.runsettings` umgeht und dadurch auf die echte Datei-Datenbank zeigt.
Er **meldet**, verhindert aber nicht, dass in genau diesem Fall bereits Daten geschrieben
wurden, bevor der Wächter überhaupt läuft: `src/Tests/LedgerMirrorTests.fs:428` und
`src/Tests/PersistenceTypeConversionTests.fs:71` rufen `initializeDatabase()` auf Modulebene
auf, ausserhalb eines `testCase`. F# wertet Modul-Level-Bindings aus, sobald das Modul zum
ersten Mal berührt wird, unabhängig von der Compile-Reihenfolge in `Tests.fsproj`. Im
reproduzierten Umgehungslauf (Verifikation von `infra-za7fq`, 2026-09-18) entstand dadurch
trotz des roten Wächter-Tests eine vollständige Datei-Datenbank mit Schema und fünf Zeilen
in `ledger_accounts`; der Verifier hat das in Iteration 2 erneut bestätigt (106 KB
`budgetbuddy.db` im Temp-Verzeichnis, obwohl der Lauf auf den einen Wächter-Test gefiltert war).

Das verletzt kein Akzeptanzkriterium von `infra-za7fq` (dort war lautes Scheitern gefordert,
nicht Verhinderung), ist aber eine Lücke: der Schaden vom 2026-09-12 (siehe ADR 0030) bestand
genau darin, dass Testdaten in eine echte Datenbank geschrieben wurden. Ein Wächter, der erst
meldet, nachdem bereits geschrieben wurde, schützt nicht vollständig davor.

## What
`initializeDatabase()`-Aufrufe, die aktuell auf Modulebene stehen (`LedgerMirrorTests.fs:428`,
`PersistenceTypeConversionTests.fs:71`), in das Setup der jeweiligen `testCase`s bzw. in eine
`before`/Setup-Funktion verschieben, die erst innerhalb der Testausführung läuft. Dadurch
entsteht die Datei-Datenbank in einem umgangenen Lauf gar nicht erst, bevor der
Wächter-Test (`TestDatabaseGuardTests.fs`, erste Compile-Unit) scheitern kann.

Bei der Verfeinerung zu prüfen: ob weitere Testmodule dasselbe Muster tragen. Es lohnt ein
Suchlauf über `src/Tests/` nach Modul-Level-Bindings, die Persistenz anfassen. Verwandtes
Muster mit anderer Wirkung: `design-system-bq4tz` (eager Top-Level-Werte im Design-System,
die beim Modul-Load rendern und unter .NET werfen). Dieselbe F#-Eigenheit, zwei
verschiedene Schäden.

## Acceptance criteria
- [ ] Kein Testmodul unter `src/Tests/` ruft `initializeDatabase()` (oder
      `initializeLedgerSchema()`) noch auf Modulebene auf. Der Suchlauf vom 2026-09-19 fand
      **fünf** solcher Stellen, nicht zwei: drei in `LedgerMirrorTests.fs`
      (`mirrorRunTests`, `mirrorGuardTests`, `runMirrorInBackgroundTests`) und zwei in
      `PersistenceTypeConversionTests.fs` (`typeConversionTests`,
      `connectionDisposalRegressionTests`). Alle fünf stehen im Muster
      `[<Tests>] let xyz = initializeDatabase()`, also im Wert selbst und damit im
      Modul-Startup.
- [ ] Die Umstellung wird durch einen Suchlauf belegt, nicht durch die Liste oben:
      nach der Änderung findet eine Suche über `src/Tests/` keine Persistenz-Initialisierung
      mehr außerhalb einer Testausführung. Zeilennummern sind kein Nachweis — die im
      ursprünglichen Fund genannten (`LedgerMirrorTests.fs:428`,
      `PersistenceTypeConversionTests.fs:71`) sind bereits gedriftet.
- [ ] In einem reproduzierten Umgehungslauf (abweichende Runsettings oder gesetztes
      `DATA_DIR`, wie in der Verifikation von `infra-za7fq` demonstriert) entsteht **keine**
      Datei-Datenbank mehr, bevor der Wächter-Test scheitert. Belegt durch einen
      tatsächlichen Lauf mit Blick ins Zielverzeichnis, nicht durch Lesen des Codes.
- [ ] Der Wächter-Test `TestDatabaseGuardTests.fs` bleibt unverändert und weiterhin die
      erste Compile-Unit in `Tests.fsproj`.
- [ ] `dotnet build` und `dotnet test` sind grün, mit unveränderter Testzahl.

## Notes
Gefunden bei der Verifikation von `infra-za7fq` (Iteration 1, 2026-09-18), bestätigt in
Iteration 2. Der Wächter-Test selbst bleibt unverändert; diese Aufgabe betrifft nur die
genannten Testmodule.

## Verifier note (iteration 1)

**VERDICT: FAIL** — ITERATION_HINT: likely-fixable

**REASONS:**

- Check 6c (mechanize-or-drop, ADR-0059) fails. Checks 1 through 6b all pass (details below),
  but this task establishes a forward-looking convention without shipping either enforcement or
  a "prose-only, unenforced" marker. The convention is explicit in two places: the
  `README_DELTA` append into `## Invariants / conventions` of
  `.agentheim/knowledge/contexts/infrastructure/README.md` ("`initializeDatabase()`/
  `initializeLedgerSchema()` (und jeder andere DB-Setup-Aufruf) dürfen in `[<Tests>]`-Bindings
  nur innerhalb einer `testCase`-Closure laufen, nie direkt im `let`-Body davor"), and
  ADR 0033's Consequences ("Das Muster ist für künftige Testdateien mit DB-Setup
  wiederverwendbar"). That is a structural rule future test files are expected to follow, not a
  one-off choice scoped to this diff.
- No enforcement exists for it. `TESTS_ADDED: 0`, and nothing in the diff would catch a future
  module-level `initializeDatabase()`/`initializeLedgerSchema()`. The existing guard
  `src/Tests/TestDatabaseGuardTests.fs` does not cover it: under the normal `dotnet test` run
  `test.runsettings` sets `USE_MEMORY_DB`, so the guard stays green even if someone reintroduces
  a module-level init. The very premise of this task is that the guard reports too late.
- No "prose-only, unenforced" marker either. The task file contains no such note in `## Notes`
  or as a criterion, and the parsed `outcome` block does not add one. The gap is therefore
  silent rather than recorded. House precedent for exactly this shape:
  `.agentheim/board/protocol.md:1337` (ADR 0020) and
  `.agentheim/board/ledger/done/ledger-q5w9c-ledger-page.md:77`.
- Mechanization is demonstrably feasible here and has direct house precedent for the same F#
  module-load family of bug: `src/Tests/DesignSystemStartupTests.fs` (from `design-system-bq4tz`,
  the "verwandtes Muster" this task's own `## What` names) forces every `<StartupCode$...>` type
  initializer by reflection instead of relying on a grep, and `src/Tests/FontFaceRegressionTests.fs`
  shows the source-scanning variant. Either shape would catch a future violation of this
  convention.
- Secondary, cheap to fix in the same pass: the ADR body's frontmatter uses `id: ADR-0033`, while
  every sibling ADR uses the bare number (`id: 0030`, `id: 0031`, `id: 0032`). The conductor has
  not materialized the file yet, so this is correctable before it lands.
- Secondary, writing rules: the ADR body and the `README_DELTA` body use the ae/ss transcription
  instead of real umlauts ("ausserhalb" twice in the ADR Context and once in the delta, "grosse"
  in the ADR Context) and lean on Gedankenstriche throughout. The diff's own code comments are
  clean on umlauts. The pre-existing test list name "Kassenbuch-Seite: seitenweise Liste und
  Kontostaende (Persistence)" (`src/Tests/LedgerPageTests.fs:1060`) is untouched by this diff and
  out of scope here.

**What already passed, so the next iteration does not redo it:**

- Criteria 1 and 2 verified by the verifier's own search over `src/Tests/`
  (`grep -rn --include="*.fs" "initializeDatabase\|initializeLedgerSchema\|seedAccounts"`): every
  surviving call sits inside a `testCase` closure at `LedgerMirrorTests.fs:545/546, 1015/1016,
  1122/1123`, `LedgerPageTests.fs:1055/1056`, `LedgerTests.fs:739`,
  `PersistenceTypeConversionTests.fs:75` and `406`. No `testCaseAsync`/`ptestCase`/`ftestCase`
  exists in any of the four files, so the shadowing covers every case in each binding, including
  those nested under `testSequenced <| testList`, and the non-recursive `let` resolves to
  Expecto's `testCase` as claimed.
- Criterion 3 verified by the verifier's own bypass run from the worktree
  (`env -u USE_MEMORY_DB DATA_DIR=<scratch> dotnet vstest src/Tests/bin/Debug/net9.0/Tests.dll
  --TestCaseFilter:"FullyQualifiedName~In-Memory"`, scratch temp dir only, never
  `~/my_apps/budgetbuddy-dev/`): the scratch directory is empty afterwards, no `budgetbuddy.db`,
  and the guard test even stays green because the module-level
  `do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")` lines now win the race against
  `dbConfig`.
- Criterion 4 verified: `git show --name-only HEAD` touches only the four test files,
  `TestDatabaseGuardTests.fs` and `Tests.fsproj` are untouched versus the parent commit, and
  `TestDatabaseGuardTests.fs` is still line 16, the first `<Compile Include=...>`.
- Criterion 5 verified by running `dotnet test` from the worktree: exit code 0,
  "Fehler: 0, erfolgreich: 874, übersprungen: 7, gesamt: 881".
- Checks 3, 4, 5, 6, 6b and 7 also pass (scope is source and tests only, no `.agentheim/` path in
  the diff, no new domain terms, the delta's target section `## Invariants / conventions` really
  exists in the BC README, ADR 0033's number is free and its body carries two rejected
  alternatives, and nothing contradicts ADR 0030). Check 8 skipped, this BC declares no runtime
  surface.

**SUGGESTED_FIX:**

Add a live enforcement test in `src/Tests/` that catches a future module-level DB init, modelled
on `DesignSystemStartupTests.fs` (reflection over the test assembly's `<StartupCode$Tests>` type
initializers, so a nested or renamed module cannot slip past a grep) or on
`FontFaceRegressionTests.fs` (source scan of `src/Tests/*.fs`), and cover it with an acceptance
criterion; alternatively, if mechanization is judged not worth it, add an explicit "prose-only,
unenforced" note to the task file's `## Notes` so the gap is a recorded decision. While
reworking, also fix the ADR frontmatter to `id: 0033` and the umlaut transcriptions in the ADR
and README delta bodies.

## Outcome

Iteration 2 schließt die von der Verifikation (Iteration 1) benannte Lücke: die Konvention
"DB-Setup nur in `testCase`-Closures" war nur in Prosa festgehalten, ohne Durchsetzung.

**Enforcement statt reiner Konvention.** Neu: `src/Tests/ModuleLevelDbInitGuardTests.fs`
(in `Tests.fsproj` direkt nach `TestDatabaseGuardTests.fs` einsortiert). Der Test durchsucht
jeden `[<Tests>]`-Block unter `src/Tests/` textbasiert über einen Einrücktiefen-Stack (nicht
über eine feste Spalte) auf `initializeDatabase()`/`initializeLedgerSchema()`-Aufrufe außerhalb
jeder `fun ... ->`-Closure. Vier synthetische Testfälle in derselben Datei prüfen die
Erkennungsfunktion direkt gegen das historische Bug-Muster, das reparierte Muster und ein
verschachteltes Modul (letzteres belegt, dass kein Spalte-0-Anker verwendet wird, der Fallstrick
aus der Design-System-Variante desselben Wächter-Musters in `design-system-bq4tz`).

**Falsifizierbarkeit real geprüft, nicht nur behauptet:** das historische Bug-Muster wurde
testweise in `LedgerMirrorTests.fs` zurückgespielt (`git diff` danach leer, sauber
zurückgesetzt). Mit dem Bug-Muster im Code schlug der neue Wächter-Test rot fehl und nannte
exakt Datei und Zeile (`LedgerMirrorTests.fs:539: initializeLedgerSchema ()`), die
Gesamt-Testzahl fiel von 879 bestanden auf 878 bestanden/1 Fehler. Nach dem Zurücksetzen war
der volle Lauf wieder grün mit 879 bestanden, 7 übersprungen, 886 gesamt (874 aus Iteration 1
plus die 5 neuen Testfälle dieser Iteration).

**Bekannte, bewusst dokumentierte Lücke** (siehe ADR 0033, Consequences/Negative): der Scan
deckt namentlich nur `initializeDatabase`/`initializeLedgerSchema` ab, nicht `seedAccounts()`,
obwohl `LedgerPageTests.fs` `seedAccounts()` derselben Konvention folgend in eine Closure
verschoben hat. Ein künftiger Rückfall bei `seedAccounts()` bliebe unentdeckt, bis die
Namensliste im Scan erweitert wird. Diese Grenze ist bewusst so belassen und nicht verschwiegen,
analog zur bereits im Repo etablierten Form solcher Marker (`infra-za7fq`s "meldet, verhindert
nicht").

**Sekundäre Korrekturen aus dem Verifier-Hinweis:** ADR-Frontmatter nutzt jetzt `id: 0033` (bare
Nummer wie alle Geschwister-ADRs, nicht `ADR-0033`). ADR-Body und README-Delta wurden auf echte
Umlaute geprüft (keine ae/oe/ue/ss-Transkription, kein Gedankenstrich) und sind sauber.

**Geänderte/neue Dateien in diesem Worktree gegenüber dem Batch-Start (440086b):**
- `src/Tests/LedgerMirrorTests.fs`, `src/Tests/LedgerPageTests.fs`, `src/Tests/LedgerTests.fs`,
  `src/Tests/PersistenceTypeConversionTests.fs` (Iteration 1: DB-Setup in geschattete
  `testCase`-Closures verschoben)
- `src/Tests/Tests.fsproj` (Iteration 1: keine funktionale Änderung nötig gewesen außer der
  neuen Compile-Einträge; Iteration 2: `ModuleLevelDbInitGuardTests.fs` direkt nach
  `TestDatabaseGuardTests.fs` ergänzt)
- `src/Tests/ModuleLevelDbInitGuardTests.fs` (Iteration 2, neu: der mechanisierte Wächter)

`dotnet build` und `dotnet test` sind grün: 879 bestanden, 7 übersprungen, 886 gesamt.
`TestDatabaseGuardTests.fs` bleibt unverändert und weiterhin die erste Compile-Unit in
`Tests.fsproj`.
