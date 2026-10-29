---
id: infrastructure-hdf7j
title: "Tests aufräumen nach der Test-Doktrin: Tautologien, schwache Quelltext-Tests, geteilte Kalendertage"
status: done
type: refactor
context: infrastructure
created: 2026-10-29
completed: 2026-10-29
depends_on: [infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Rund 15 Tautologien gehäuft in alten Comdirect-/YNAB-Dateien, schwache Teilstring-Prüfungen auf View-Verdrahtung und Klassenkonstanten, Tagesgrenzen-Tests teilen Kalendertage (Audit D, B1 N7 bis N9, C B10, B11, B19).

## What
Tautologien löschen, schwache Quelltext-Tests durch Verhaltenstests ersetzen oder löschen, Tagesgrenzen-Tests mit eigener DB oder injizierter Uhr.

## Acceptance criteria
- [x] Die im Bericht D gelisteten Tautologien sind entfernt oder begründet behalten.
- [x] Tagesgrenzen-Tests laufen unabhängig vom Kalendertag anderer Tests.

## Notes
Migrationsschritt M14. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.


## Outcome

Aufgeräumt nach `.claude/rules/test-doctrine.md`. Gate in der Worktree: `VERIFY: PASS (dotnet 2569 grün, node 319 grün, ...)`. Vorher 2605 dotnet, jetzt 2569: 37 Tests gelöscht, 1 neu (Gegenprobe im CORS-Wächter), 5 umgeschrieben. Node unverändert 319. Keine Testdatei entfernt, `Tests.fsproj` unverändert. Die Wächter von heute sind nicht angefasst.

### Gelöscht (Grund je Zeile)
- `YnabClientTests.fs`, Liste "Integration Test Documentation" (3: "documents how to run integration tests", "documents YNAB API rate limits", "documents YNAB API response structure"): Rumpf nur Kommentar und `()`, immer grün.
- `ComdirectIntegrationTests.fs` "documents manual testing procedure": nur `printfn`, "Always pass". Der Hinweis auf `scripts/test-comdirect.fsx` steht weiter als Kommentar.
- `ComdirectIntegrationTests.fs` "ApiKeys stores credentials", "Challenge validates P_TAN_PUSH type", "AuthSession contains all required fields": Record bauen und Felder zurücklesen.
- `ComdirectIntegrationTests.fs` "RequestInfo encodes correctly": dieselbe Aussage wie `ComdirectClientTests.fs` "RequestInfo.Encode produces valid JSON" (Doktrin Punkt 2). Der verbliebene Test prüft echtes `Encode`.
- `ComdirectClientTests.fs` "RequestInfo can be created with timestamp-based request ID", "Request ID should be 9 characters from timestamp", "Session ID should be a GUID", "Challenge type should be P_TAN_PUSH": rechnen den Wert im Test selbst aus (BCL-Verhalten, `Guid.NewGuid`), kein Produktionscode.
- `ComdirectClientTests.fs` "ComdirectError types are correctly defined", "Error messages are descriptive": Länge einer selbst gebauten Liste und Pattern-Match auf gerade gebaute Werte.
- `SplitTransactionTests.fs`, Listen "Split Transaction Types" (6), "Split Amount Validation" (4), "Split Transaction Import Ready Check" (4), "Split Currency Consistency" (2): bauen Records und rechnen Summe, Währung und Import-Bereitschaft im Test nach. Summe, Währung und Mindestzahl prüfen die `mkSplits`-Tests in derselben Datei gegen Produktionscode. Die unbenutzten Helfer `createTestTransaction` und `categoryNameOf` sind mit entfernt.
- `LedgerViewTests.fs` "Beide Zeilen der Handy-Zeile haben dieselbe feste Höhe ...": Konstante `ListRoles.mobileLine1/2` gegen ihren eigenen Inhalt (`h-5`).
- `ButtonSizeTests.fs` "Small: mobil 48 px Mindesthöhe, ab md keine", "Medium und Large behalten ihre Mindesthöhe": Klassenstring-Abbildung (B1 N7, C B19); die Höhe misst `scripts/touch-target-small-button.test.mjs`. `Button.sizeToClass` ist wieder `private`, wie vor 897b13c.
- `LedgerSplitEditorClientTests.fs` "Verdrahtung: Zeile hinzufügen nur bei offenem Rest, Speichern nach canSave, am Handy kein Abbrechen": View-Verdrahtung per Teilstring; das Verhalten prüft `scripts/ledger-split-editor.test.mjs` ("PC: Kein Split mit festem Knopf Aufteilen; ... Speichern erst bei Rest 0,00 €, Zeile hinzufügen dann ausgegraut" und "Handy (390x844): ... kein Abbrechen").
- `LedgerFilterButtonTests.fs` "Das Zellmenü ist zurückgebaut", "Das alte Historie-Sheet und sein Filter-Block sind zurückgebaut": Rückbau-Wächter (Doktrin Punkt 7).
- `SyncFlowViewTests.fs` "Styleguide §2 nennt Import prüfen nicht mehr als Vorbild": Rückbau-Wächter auf einen Satz in der Doku.

### Umgeschrieben auf Produktionscode
- `DuplicateDetectionTests.fs` "Memo With Reference (Regression Tests)" (9 Fälle): prüften eine Kopie von `buildMemoWithReference` im Test. Sie laufen jetzt gegen `Server.YnabClient.buildMemoWithReference`, die Grenze 300 steht bewusst als Literal. Gegenprobe: mit `memoLimit = 310` in `src/Server/MemoFormatting.fs` wird ein Fall rot.
- `YnabClientTests.fs` "memo with reference respects 300 char limit": rechnete die Kürzung selbst nach. Jetzt Aufruf des echten `buildMemoWithReference`, mit wiederholtem Text, damit Memos über der Grenze vorkommen. Gegenprobe mit 310: rot.
- `YnabClientTests.fs` "milliunits conversion roundtrip": rechnete beide Richtungen selbst. Jetzt Rundlauf Milliunits → Euro → `Shared.Budgeting.toMilliunits`, gestreut bis in fünfstellige Beträge. Gegenprobe mit einem verfälschten `toMilliunits`: rot. Die Namen beider Properties bleiben, weil Doktrin Punkt 18 sie als Vorbild nennt.
- `CorsTests.fs` (B1 N8, C B10): die Suche ist eine Funktion `corsCalls`, mit neuer Gegenprobe ("erkennt einen CORS-Aufruf und übergeht einen Kommentar"), wie Doktrin Punkt 7 sie verlangt. Die `obj`/`bin`-Filter nutzen `Path.DirectorySeparatorChar` statt `/`.

### Begründet behalten
- `SyncSessionManagerTests.fs` "updateSessionStatus changes status correctly" (D: "nahezu tautologisch"): Setter und Getter sind beide Produktionscode mit Zustand. Schreibt `updateSessionStatus` nicht, bleibt der Status `AwaitingBankAuth` und der Test wird rot.
- `SyncFlowViewTests.fs` "splitAmountClass" (D: 399-405, Klassenstring): enthält Verbote (`neon-green`, `glow`, `font-bold`), sichert also eine Designregel ab, wie D es als sinnvoll nennt.
- `ButtonSizeTests.fs` iconOnlyClass-Tests: prüfen eine Verzweigung (Größe und Text), keine Konstante.
- `CorsTests.fs`: Middleware ist eine repo-weite Invariante ohne Verhaltenszugang (Doktrin Punkt 7); ein Host-Test bräuchte einen Umbau von `Program.fs` (siehe Backlog).
- `LedgerViewTests.fs:1041` `Expect.isTrue true` nach erzwungenem Typinitialisierer: D nennt ihn berechtigt.

### Tagesgrenzen-Tests (B1 N9, C B11)
`src/Tests/BotApiTests.fs`: Die Tagesgrenze zählt alle Bob-Buchungen eines Berliner Tags in der geteilten In-Memory-DB. Jede `Env` bekommt jetzt aus einem Zähler (`nextTestDay`) einen eigenen Tag D ab 2030-01-01 mit vier Tagen Abstand und darf D-1 bis D+2 nutzen. Die Uhr startet um 10:00 UTC an D. Kein Test nennt mehr ein festes Datum: `bodyOf` nimmt den Berliner Tag der injizierten Uhr (`todayText`), die Grenz- und Datumstests nutzen `env.SetUtc(offset, h, m)` und `env.DateText offset`. Das betrifft auch die frühen Commit-Tests, die vorher alle auf 2026-10-28 buchten. Der Test zum Berliner Tag arbeitet relativ zu D und gilt in Winter- und Sommerzeit. `BotDomainTests.fs` ist rein (kein DB-Zugriff) und brauchte keine Änderung.
Nachweis: Jeder der 12 tagesabhängigen Tests läuft einzeln grün (`--filter-test-case`). Ein `--stress 0.3`-Lauf der Liste (zufällige Reihenfolge, Wiederholung im selben Prozess) ergab 1624 von 1624 grün. Derselbe Stress-Lauf gegen den alten Build auf `main` ergab 15 407 von 26 350 rot, weil wiederholte Läufe denselben festen Tag trafen.

### Nicht in diesem Task
Rund 140 weitere Quelltext-Tests auf View-Verdrahtung, vor allem in `LedgerViewTests.fs`, `LedgerDetailTests.fs` und `LedgerSplitEditorClientTests.fs` (Bericht D, Abschnitt 3). Bearbeitet sind die Fälle, die die Berichte namentlich nennen. Der Rest steht als Backlog-Eintrag unten.
