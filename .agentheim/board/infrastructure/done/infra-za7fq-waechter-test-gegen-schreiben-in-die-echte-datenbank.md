---
id: infra-za7fq
title: "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt"
status: done
type: feature
context: infrastructure
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [test-infra, sicherheit]
related_adrs: [0030]
related_research: []
prior_art: []
---

## Why
Befund des Verifiers bei `ledger-b1xwe` (2026-09-12). ADR 0030 hat den Fehler behoben,
durch den `dotnet test` in Romans echte Entwicklungsdatenbank schrieb: `test.runsettings`
setzt `USE_MEMORY_DB` jetzt im Test-Host, weil VSTest den Einstiegspunkt in `Main.fs` nie
aufruft.

Der Schutz hängt aber daran, dass die Tests auf dem üblichen Weg gestartet werden. Zwei
Wege umgehen ihn **stillschweigend**:
- `dotnet test --settings andere.runsettings` überschreibt die `RunSettingsFilePath`-Angabe
  aus der `.fsproj`.
- ein direkter `dotnet vstest`-Aufruf liest die Projekt-Einstellung gar nicht.

In beiden Fällen fällt der Lauf lautlos auf die Datei-Datenbank zurück und schreibt wieder
in echte Daten. Genau diese Lautlosigkeit hat den ursprünglichen Fehler monatelang
unsichtbar gemacht: 106 Test-Konten und 365 Test-Buchungen sind so in eine Datenbank mit
rund 14.000 echten Buchungen gelangt, ohne dass irgendetwas gemeldet hat (Aufräumen:
`ledger-vm3dk`).

## What
Festgelegt mit Roman am 2026-09-18: **ein normaler Expecto-Test**, kein Eingriff in den
Produktivpfad. Beide Umgehungswege laufen durch die Testsuite, der Wächter greift also
genau dort, wo er greifen muss, und kostet im Betrieb nichts.

Der Test prüft den **tatsächlich aktiven** Connection-String. Zeigt er nicht auf eine
In-Memory-Datenbank, scheitert er laut und nennt den Pfad, auf den er zeigen würde:

```fsharp
testCase "Testlauf zeigt auf eine In-Memory-Datenbank" <| fun () ->
    let cs = Persistence.activeConnectionString ()
    Expect.stringContains cs ":memory:"
        $"Testlauf zeigt auf eine Datei-Datenbank: {cs}"
```

Dafür braucht es einen Lesezugang: `dbConfig` in `src/Server/Persistence.fs` ist heute
`private` und `lazy`, `getConnection()` ruft `dbConfig.Force()`. Neu kommt eine schmale
öffentliche Funktion `activeConnectionString ()` dazu, die denselben `Force()`-Pfad nutzt
und nur den String zurückgibt. Der Name ist im Projekt frei. Eine Connection darf der
Wächter nicht öffnen: das Ziel ist zu prüfen, worauf gezeigt wird, nicht dort anzuklopfen.

Damit wird aus einem stillen Datenverlust ein roter Test. Der Schutz liegt dann nicht mehr
allein in einer Konfigurationsdatei, die man umgehen kann, sondern in einer Zusicherung,
die mitläuft.

## Acceptance criteria
- [ ] `Persistence.activeConnectionString ()` ist öffentlich, gibt den aktiven
      Connection-String zurück und öffnet dabei keine Connection.
- [ ] Ein neuer Test in `src/Tests/` scheitert, wenn der aktive Connection-String nicht auf
      eine In-Memory-Datenbank zeigt. Die Fehlermeldung enthält den Pfad wörtlich.
- [ ] Der Wächter scheitert nachweislich bei einem Lauf ohne `USE_MEMORY_DB` (belegt durch
      einen Lauf mit abweichenden Runsettings oder gesetztem `DATA_DIR`, dokumentiert im
      Verifikationsprotokoll), und ist im normalen `dotnet test` grün.
- [ ] Der Wächter läuft früh: er steht in der Compile-Reihenfolge der `Tests.fsproj` vor den
      Tests, die die Datenbank anfassen.
- [ ] Der Wächter hängt nicht an `test.runsettings`: er liest den Zustand, den
      `Persistence` tatsächlich aufgelöst hat, nicht die Umgebungsvariable selbst.
- [ ] Der Test trägt einen Kommentar, der ADR 0030 und den Vorfall vom 2026-09-12 nennt.
- [ ] `dotnet build` und `dotnet test` sind grün.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-b1xwe`, verfeinert am 2026-09-18
mit Roman. Zusammenhang: ADR 0030 (der Mechanismus), `ledger-vm3dk` (die Nachwirkung in
der Datenbank).

Die Alternative, eine Zusicherung beim ersten Zugriff auf `dbConfig`, wurde verworfen: sie
griffe früher, kostet aber eine Prüfung im Produktivpfad und müsste erkennen, ob gerade ein
Testlauf läuft. Der Test deckt beide bekannten Umgehungswege ab, weil beide durch die
Testsuite gehen.

Dateien: `src/Server/Persistence.fs` (eine neue Funktion), `src/Tests/`. Keine
Überschneidung mit `ledger-kdnnz`, das `Persistence.fs` zwar anfasst, aber in den
Kassenbuch-Tabellen und Migrationen, nicht in der Konfiguration.

## Verifier note (iteration 1)

**VERDICT: FAIL** — `ITERATION_HINT: likely-fixable`

**REASONS:**
- Verstoß gegen die verbindliche Schreibregel des Projekts (echte Umlaute statt ae/oe/ue-Umschrift), die im Dispatch-Prompt des Workers wörtlich stand. Betroffen sind alle drei geänderten Dateien: `src/Server/Persistence.fs:96-102` ("tatsaechlich", "aufgeloesten", "zurueck", "oeffnen", "loest"); `src/Tests/TestDatabaseGuardTests.fs:3-21` ("Waechter", "ueber", "ueberschreibt", "Faellen", "faellt", "zurueck", "prueft", "aufgeloest", "oeffnet", "wuerde", "schuetzen"); `src/Tests/Tests.fsproj:13-15` ("Waechter", "Rueckfall", "auffaellt").
- Besonders sichtbar: der Testlisten-Name `"Test-Datenbank-Waechter"` in `TestDatabaseGuardTests.fs:26` steht so in jeder Testausgabe.
- Die Drift ist im selben Commit inkonsistent: `Persistence.fs:100` schreibt korrekt "Wächter", vier Zeilen darüber steht "tatsaechlich". Gedankenstriche wurden keine gefunden, dieser Teil der Regel ist eingehalten.
- Nicht blockierend, aber relevant: der Wächter meldet, er verhindert nicht. Im reproduzierten Lauf mit umgangenen Runsettings entstand trotzdem eine vollständige Datei-Datenbank mit Schema und 5 Zeilen in `ledger_accounts`, geschrieben beim Auswerten der Modul-Bindings anderer Testmodule (`src/Tests/LedgerMirrorTests.fs:428`, `src/Tests/PersistenceTypeConversionTests.fs:71` rufen `initializeDatabase()` außerhalb eines `testCase`). Die Compile-Reihenfolge schützt davor nicht. Das verletzt kein Akzeptanzkriterium (gefordert ist lautes Scheitern, nicht Verhinderung), gehört aber als Folgeaufgabe ins Backlog.
- Die sieben Akzeptanzkriterien sind sonst erfüllt und belegt: `dotnet test` im Worktree endete mit Exit-Status 0 (850 erfolgreich, 0 Fehler, 7 übersprungen); der Wächter scheiterte im Umgehungslauf mit Exit-Status 1 und nannte den Datei-Pfad wörtlich in der Fehlermeldung.

**SUGGESTED_FIX:** Die aufgeführten Wörter in `src/Server/Persistence.fs`, `src/Tests/TestDatabaseGuardTests.fs` und `src/Tests/Tests.fsproj` auf echte Umlaute umstellen (Wächter, über, überschreibt, Fällen, fällt, zurück, prüft, aufgelöst, öffnet/öffnen, würde, schützen, tatsächlich, löst, Rückfall, auffällt) einschließlich des Testlisten-Namens "Test-Datenbank-Wächter", danach `dotnet test` erneut laufen lassen. Zusätzlich als Backlog-Item aufnehmen: Testmodule, die `initializeDatabase()` auf Modulebene aufrufen, in `testCase`-Setup verschieben, damit ein umgangener Lauf die Datei-Datenbank gar nicht erst anlegt.

## Outcome

`Persistence.activeConnectionString ()` (`src/Server/Persistence.fs`) ist jetzt öffentlich
und gibt den durch `dbConfig.Force()` aufgelösten Connection-String zurück, ohne selbst
eine `SqliteConnection` zu erzeugen.

Ein neuer Test (`src/Tests/TestDatabaseGuardTests.fs`, als erste Compile-Unit in
`Tests.fsproj` noch vor `RemoteDataTests.fs` gelistet) prüft diesen String gegen
`:memory:` und scheitert laut mit dem wörtlichen Datei-Pfad, falls der Testlauf auf eine
Datei-Datenbank zeigt. Der Test setzt `USE_MEMORY_DB` selbst nicht und hängt damit nicht
an `test.runsettings` (ADR 0030), sondern am tatsächlich von `Persistence` aufgelösten
Zustand.

Verifikation (Worker):
- Normaler `dotnet test` (saubere Umgebung, `test.runsettings` greift automatisch):
  850 erfolgreich, 0 Fehler, 7 übersprungen (Integrationstests ohne `.env`).
- Umgehung nachgestellt mit `DATA_DIR` auf ein Scratch-Temp-Verzeichnis (nie der reale
  `~/my_apps/budgetbuddy-dev/`-Pfad): der Wächter scheitert erwartungsgemäss mit
  `Testlauf zeigt auf eine Datei-Datenbank: Data Source=.../fake-datadir/budgetbuddy.db`.

Unabhängig nachgestellt (Verifier, Iteration 2):
- Lauf A, `dotnet test --no-build --filter "FullyQualifiedName~In-Memory"`: Exit-Status 0,
  erfolgreich 1.
- Lauf B, `env -u USE_MEMORY_DB DATA_DIR=<temp> dotnet vstest src/Tests/bin/Debug/net9.0/Tests.dll
  --TestCaseFilter:"FullyQualifiedName~In-Memory"`: Exit-Status 1, Wächter rot, Meldung
  `Testlauf zeigt auf eine Datei-Datenbank: Data Source=<temp>/guard-probe-datadir/budgetbuddy.db`.
  Der direkte `vstest`-Aufruf ist genau einer der beiden im Task benannten Umgehungswege.
- Gesamtlauf im Worktree: `Fehler: 0, erfolgreich: 850, übersprungen: 7, gesamt: 857`.

Iteration 2 hat ausschliesslich die Schreibregel-Verstösse der ersten Iteration behoben
(echte Umlaute statt ae/oe/ue-Umschrift, einschliesslich des Testlisten-Namens
"Test-Datenbank-Wächter"); die funktionale Umsetzung blieb unverändert.

**Bekannte, bewusst nicht behobene Grenze:** der Wächter meldet, er verhindert nicht. Im
Umgehungslauf entsteht trotz rotem Test eine vollständige Datei-Datenbank, weil
`src/Tests/LedgerMirrorTests.fs:428` und `src/Tests/PersistenceTypeConversionTests.fs:71`
`initializeDatabase()` auf Modulebene aufrufen und F# Modul-Bindings auswertet, sobald das
Modul erstmals berührt wird, unabhängig von der Compile-Reihenfolge. Das verletzt kein
Akzeptanzkriterium dieser Aufgabe (gefordert war lautes Scheitern, nicht Verhinderung) und
ist als `infra-sdad8` im Backlog erfasst.

Dateien: `src/Server/Persistence.fs`, `src/Tests/TestDatabaseGuardTests.fs`,
`src/Tests/Tests.fsproj`.
