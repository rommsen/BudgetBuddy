---
id: infra-za7fq
title: "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt"
status: todo
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
