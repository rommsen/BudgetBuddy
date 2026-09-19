---
id: infra-sdad8
title: "Modul-Level-initializeDatabase()-Aufrufe in Testcase-Setup verschieben"
status: backlog
type: refactor
context: infrastructure
created: 2026-09-18
completed:
depends_on: []
blocks: []
tags: [test-infra, sicherheit]
related_adrs: [0030]
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
