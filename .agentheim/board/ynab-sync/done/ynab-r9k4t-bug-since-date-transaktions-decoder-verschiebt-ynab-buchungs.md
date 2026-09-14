---
id: ynab-r9k4t
title: "Bug: since_date-Transaktions-Decoder verschiebt YNAB-Buchungstage um den lokalen Zeitzonen-Offset"
status: done
completed: 2026-09-13
type: bug
context: ynab-sync
created: 2026-09-06
depends_on: []
blocks: []
tags: [backend, bug]
related_adrs: []
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
`Server.YnabClient.Decoders.transactionDecoder` liest YNABs `date` mit
`Decode.datetimeUtc`. YNAB liefert einen reinen Kalendertag ohne Zeitzone ("2026-02-03");
`Decode.datetimeUtc` interpretiert ihn als **Ortszeit** und rechnet in UTC um. In MEZ wird
daraus der 2. Februar, 23:00 — der Buchungstag ist um einen Tag zu früh.

Gefunden beim Bau des Spiegels (`ledger-m8r2v`): der dortige Decoder-Test schlug genau
deshalb fehl. Der Spiegel hat einen eigenen, korrekten `ynabDateDecoder` bekommen
(`src/Server/YnabObservations.fs`, `TryParseExact` mit `DateTimeKind.Utc`). Der alte Pfad
blieb bewusst unangetastet, weil er außerhalb des Slice-Scopes lag.

Betroffen sind die Nutzer von `getAccountTransactions`: die Duplikat-Erkennung
(`DuplicateDetection`, Datumsfenster-Vergleich) und die Quick-Add-Vorlagen.

## What
1. Den Datumsteil von `transactionDecoder` auf denselben Kalendertag-Decoder umstellen wie
   der Spiegel (gemeinsame Funktion statt zweiter Kopie).
2. Prüfen, ob die Duplikat-Erkennung sich auf den Versatz eingestellt hat (Toleranzfenster,
   Off-by-one-Kompensation); solche Kompensationen mit entfernen, sonst kippt der Fehler
   nur auf die andere Seite.

## Acceptance criteria
- [ ] Regressions-Test: `"2026-02-03"` dekodiert zu `2026-02-03 00:00:00 Utc`, unabhängig
      von der Zeitzone der Maschine (Test schlägt vor dem Fix fehl).
- [ ] Duplikat-Erkennung und Quick-Add-Vorlagen bleiben mit korrigierten Datumswerten grün;
      etwaige Off-by-one-Kompensationen sind entfernt statt verdoppelt.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Der Fix ist klein, die Wirkung liegt in den Nutzern des Datums. Vorbild und korrekter
Decoder: `Server.YnabObservations.Decoders.ynabDateDecoder`.
Id-Präfix beim Materialisieren von `ledger-r9k4t` auf `ynab-r9k4t` korrigiert: der Task
gehört nach `ynab-sync`, dessen Ids `ynab-` tragen (ynab-czk34, ynab-b7q2m).

## Outcome
`Server.YnabClient.Decoders.transactionDecoder` nutzt jetzt denselben Kalendertag-Decoder
wie der Spiegel (`Server.YnabObservations.Decoders.ynabDateDecoder`), statt einer zweiten
Kopie. `YnabObservations.fs` kompiliert vor `YnabClient.fs` (siehe `Server.fsproj`), daher
kann `YnabClient` direkt qualifiziert darauf verweisen; keine dritte Datei nötig.

Geprüft und **keine** Off-by-one-Kompensation gefunden, die entfernt werden musste:
- `DuplicateDetection.DateToleranceDays = 1` stammt aus dem allerersten Commit der
  Duplikat-Erkennung (vor dem Spiegel-Bug entdeckt wurde) und ist ein generisches
  Fuzzy-Toleranzfenster für Buchungstag-Abweichungen zwischen Bank und YNAB, keine
  Kompensation dieses Bugs. Bleibt unverändert.
- `QuickAddTemplate` trägt gar kein Datum (bewusst, siehe Doc-Kommentar in
  `Shared/Domain.fs`); `recentQuickAddTemplates` sortiert nur nach `tx.Date` für die
  Reihenfolge, ohne Datumskompensation.

Regressionstest: `src/Tests/YnabClientTests.fs`, `transactionDecoderTests` (neue
`[<Tests>]`-Liste direkt nach `readyToAssignTests`). Prüft sowohl den Kalendertag als
auch `tx.Date.Kind = DateTimeKind.Utc`.

Der Test allein fängt den Bug nur auf einer Maschine östlich von UTC (MEZ verschiebt,
UTC-Offset 0 verschiebt nichts). Damit der Regressionsschutz unabhängig vom Host
funktioniert, setzt `src/Tests/test.runsettings` (über `RunSettingsFilePath` in
`Tests.fsproj` bereits eingebunden) jetzt zusätzlich `TZ=Europe/Berlin` für den
gesamten Testlauf (`RunConfiguration/EnvironmentVariables`, von VSTest selbst
unterstützt, kein Custom-Code). .NET liest `TZ` unter macOS und Linux beim
Prozessstart; unter Windows wird die Variable ignoriert, dort gilt die
Systemzeitzone.

Probe (siehe Diary für die genauen Befehle): mit temporär auf `Decode.datetimeUtc`
zurückgesetztem Decoder schlägt der Regressionstest selbst unter einer Host-Shell mit
`TZ=UTC` fehl, weil `test.runsettings` die Zeitzone des Testprozesses auf
`Europe/Berlin` erzwingt. Mit dem Fix ist der Test unter `TZ=UTC` und
`TZ=Europe/Berlin` grün. Der volle Suite-Lauf (`dotnet test src/Tests`, Default-TZ aus
`test.runsettings`) bleibt mit `TZ=Europe/Berlin` für den gesamten Prozess grün: 820
bestanden, 7 übersprungen (.env-Integrationstests), 0 Fehler.

Geänderte Dateien:
- `src/Server/YnabClient.fs`: Datumsteil von `transactionDecoder` auf
  `Server.YnabObservations.Decoders.ynabDateDecoder` umgestellt.
- `src/Server/YnabObservations.fs`: Doc-Kommentar von `ynabDateDecoder` ergänzt; der
  Decoder ist jetzt zwischen Spiegel und Legacy-Pfad geteilt.
- `src/Tests/YnabClientTests.fs`: Regressionstest `transactionDecoderTests` ergänzt,
  inklusive `DateTimeKind`-Prüfung; Kommentar korrigiert (Mechanismus ist
  `test.runsettings`, nicht ein nie existierendes TZ-Env in `Server.fsproj`/CI).
- `src/Tests/test.runsettings`: `TZ=Europe/Berlin` zu den bereits vorhandenen
  `RunConfiguration/EnvironmentVariables` ergänzt, damit der Regressionstest
  unabhängig von der Host-Zeitzone greift.

## Diary

## 2026-09-13 - Fix: since_date-Transaktions-Decoder verschiebt YNAB-Buchungstage

### What
`Server.YnabClient.Decoders.transactionDecoder` las YNABs reinen Kalendertag mit
`Decode.datetimeUtc`, was ihn um den lokalen Zeitzonen-Offset verschob (in MEZ einen
Tag zu früh). Auf denselben Kalendertag-Decoder umgestellt, den der Spiegel bereits
nutzt (`Server.YnabObservations.Decoders.ynabDateDecoder`), statt einer zweiten Kopie.
Duplikat-Erkennung und Quick-Add-Vorlagen auf Kompensationen des Versatzes geprüft:
keine gefunden, die eigens für diesen Bug eingeführt wurden.

### Files Changed
- `src/Server/YnabClient.fs`: Datumsteil des Decoders auf den geteilten Decoder
  umgestellt.
- `src/Server/YnabObservations.fs`: Doc-Kommentar ergänzt (geteilte Nutzung).
- `src/Tests/YnabClientTests.fs`: Regressionstest für den Kalendertag-Decoder,
  inklusive `DateTimeKind`-Prüfung.
- `src/Tests/test.runsettings`: `TZ=Europe/Berlin` für den gesamten Testlauf
  ergänzt, damit der Regressionstest unabhängig von der Host-Zeitzone greift.

### Rationale
Buchungstage der Duplikat-Erkennung und der Quick-Add-Sortierung dürfen nicht vom
lokalen Zeitzonen-Offset der Maschine abhängen; das brachte den falschen Tag in beide
Nutzer von `getAccountTransactions`. Der Regressionstest allein prüft das nur auf
einer Nicht-UTC-Maschine; `test.runsettings` erzwingt diese Zeitzone jetzt für jeden
Testlauf, auch auf einem UTC-Host (Docker, CI).

### Verification (Iteration 1)
- Build: PASSED (`dotnet build src/Server`)
- Tests: 820 bestanden, 7 übersprungen, 0 Fehler, unter `TZ=Europe/Berlin` und
  `TZ=UTC` (`dotnet test src/Tests`)

### Verification (Iteration 2, nach Verifier-Feedback)
Beweis, dass der Regressionstest jetzt unabhängig von der Host-Zeitzone greift:

1. `Decode.datetimeUtc` temporär wieder in `transactionDecoder` eingesetzt (Bug
   reproduziert).
2. `TZ=UTC dotnet test src/Tests/Tests.fsproj --filter "FullyQualifiedName~2026-02-03"`
   von einer Shell mit `TZ=UTC` gestartet, ohne den Fix. Ergebnis: **rot**
   (`expected: 03.02.2026 00:00:00, actual: 02.02.2026 23:00:00`), weil
   `test.runsettings` die Zeitzone des Testprozesses unabhängig von der Shell auf
   `Europe/Berlin` setzt.
3. Fix zurückgesetzt (`Server.YnabObservations.Decoders.ynabDateDecoder` wieder
   eingesetzt), `git diff --stat -- src/Server/YnabClient.fs` bestätigt: keine
   verbleibende Abweichung.
4. Derselbe gefilterte Testlauf unter `TZ=UTC dotnet test ...` und
   `TZ=Europe/Berlin dotnet test ...`: **beide grün**
   (`Bestanden!: Fehler: 0, erfolgreich: 1`).
5. Volle Suite: `dotnet build` PASSED, `dotnet test src/Tests` (Default-TZ aus
   `test.runsettings`, also `Europe/Berlin` für den gesamten Prozess): 820 bestanden,
   7 übersprungen (.env-Integrationstests), 0 Fehler.

## Verifier note (iteration 1)

**REASONS:**
- Kriterium 1 nicht erfüllt: "unabhängig von der Zeitzone der Maschine (Test schlägt vor dem Fix fehl)". Der neue Test in `src/Tests/YnabClientTests.fs` (`transactionDecoderTests`, ca. Z. 499 bis 514) schlägt vor dem Fix nur auf einer Maschine östlich von UTC fehl; der eigene Kommentar räumt ein, dass eine UTC-Maschine den Bug nicht fängt.
- Der Kommentar (`src/Tests/YnabClientTests.fs:507`) behauptet "Der Regressionsschutz kommt aus dem TZ-Env in Server.fsproj/CI". Das existiert nicht: eine Suche über den ganzen Worktree (fsproj, props, runsettings, Dockerfile, compose, `.github/workflows`) findet `TZ` nur in diesem Kommentar. `Server.fsproj` und `Tests.fsproj` setzen keine Umgebung, die einzige CI-Workflow-Datei ist `deploy-diary.yml`. Der Kommentar ist falsch, und auf einem UTC-Host (Docker, CI) würde das alte `Decode.datetimeUtc` diesen Test wieder bestehen.
- Der Test prüft `DateTimeKind` nicht, obwohl Kriterium 1 `... 00:00:00 Utc` nennt. `Expect.equal` auf DateTime ignoriert Kind, `tx.Date.Kind = DateTimeKind.Utc` wird nie geprüft (der Code selbst setzt `SpecifyKind(..., Utc)` in `YnabObservations.fs:109`, der Test nagelt es nur nicht fest).
- Die neuen Kommentare und der Testname verletzen die Schreibregeln (echte Umlaute, keine Gedankenstriche): "oestlich", "frueh", "faellt", "unabhaengig" im Testnamen, ein Gedankenstrich in `YnabClientTests.fs`, "fuehrt" in der geänderten Doc-Comment-Zeile in `YnabObservations.fs`. Outcome und Diary in der Task-Datei schreiben ebenfalls "noetig", "Geprueft", "gruen" und nutzen Gedankenstriche.
- Geprüft und in Ordnung: `dotnet build` grün; `dotnet test src/Tests` grün unter `TZ=Europe/Berlin` und `TZ=UTC` (820 bestanden, 7 übersprungen, 0 fehlgeschlagen); Kriterium 2 erfüllt (`DuplicateDetection.matchesByDateAmountPayee` vergleicht `.Date` mit einer allgemeinen `DateToleranceDays = 1`, keine Kompensation dieses Bugs; Quick-Add-Vorlagen tragen kein Datum); Scope sauber; Outcome und Diary vorhanden.

**SUGGESTED_FIX:** Den Regressionstest unabhängig von der Maschinen-Zeitzone vor dem Fix scheitern lassen, z. B. `TZ` für den Testprozess auf eine Nicht-UTC-Zone setzen (`EnvironmentVariables` in einer `.runsettings`, eingebunden in `Tests.fsproj`) oder im Test in einem Kindprozess mit `TZ=Europe/Berlin` dekodieren. Den falschen Kommentar "TZ-Env in Server.fsproj/CI" korrigieren oder entfernen. `Expect.equal tx.Date.Kind DateTimeKind.Utc` ergänzen. Kommentare, Testname und Task-Datei-Text auf echte Umlaute ohne Gedankenstriche umstellen.

**ITERATION_HINT:** likely-fixable
