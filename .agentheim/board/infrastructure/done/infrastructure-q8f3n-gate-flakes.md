---
id: infrastructure-q8f3n
title: "Gate ohne Flakes: Ledger-Replay mit fremden Perf-Buchungen, AC14-Zeittest, Design-System-Export und regeln-panel werden deterministisch"
status: done
type: bug
context: infrastructure
created: 2026-11-21
completed:
depends_on: []
blocks: []
tags: [gate, flake, tests, verify]
related_adrs: [0100]
related_research: []
prior_art: [infrastructure-x7p2e]
---

## Why
Roman (2026-11-21): nach dem Work-Lauf in der Nacht vom 20. auf den 21.11. („Hat wieder die ganze Nacht gedauert. Warum?“)
soll das Gate ohne Flakes laufen. In dieser Nacht war etwa die Hälfte von rund 25 Gate-Läufen nur an Flakes rot; jeder
rote Lauf kostet einen weiteren (6 bis 8 min). Der häufigste Flake (Layout-Zustand `targets-pruefen-nicht-neu-gelesen`)
ist schon behoben (118819ea, ein Wettlauf zwischen Test und App). Übrig sind die vier unten, bisher Zeile in der
Kann-Sammlung infrastructure, jetzt eigenes Ticket, weil sie jeden Lauf bremsen.

## What
Jeder Flake wird reproduziert oder seine Ursache am Code belegt, dann deterministisch gemacht. Kein Wiederholen, kein
Hochsetzen von Zeitgrenzen ohne Begründung, kein Ausschluss aus dem Gate.

1. **Ledger-Replay mit fremden „Perf“-Buchungen.** Rot waren unter anderem „Ledger Persistence (appendAndApply).Replay
   rekonstruiert Buchungen aus allen drei Entstehungsarten …“ (`src/Tests/LedgerTests.fs`), „… Replay reproduziert
   SplitTransferCounterparts …“ und „Ledger Spiegel-Lauf (Split-Übertrag, Nachlauf).Replay reproduziert die Verknüpfung
   eines Spiegel-Laufs“ (`src/Tests/LedgerMirrorTests.fs`). Die Meldungen zeigen Buchungen mit Payee „Perf <n>“, die
   `src/Tests/LedgerPageTests.fs` ~3228 (AC14, 1000 Buchungen) anlegt. Alle stehen in `TestGroups.InMemoryDb` (ADR 0100).
   Hypothese: AC14 schreibt Zustand ohne passende Ereignisse (oder umgekehrt), und ein Replay-Test, der danach läuft,
   vergleicht alle Buchungen der geteilten DB. Klären und beheben, etwa AC14 auf eine eigene DB (Muster
   `BudgetEventsFixture.isolatedDb`) oder die Replay-Tests auf ihre eigenen Buchungen eingrenzen.
2. **AC14-Zeittest** („1000 Buchungen … in angemessener Zeit“, Grenze 2 s, unter Last 3,7 s gemessen): die Aussage
   (Abfrage skaliert) so prüfen, dass Last auf der Maschine sie nicht kippt, etwa über die Zahl der Abfragen oder den
   Plan statt der Wanduhr, oder begründet trennen.
3. **`[flow:design-system-export]`** (`scripts/export-design-system-run.test.mjs`) unter Last rot: Ursache klären
   (verify.sh nennt `vite preview` mit fest 5 s Start), Start abwarten statt fester Zeit.
4. **`regeln-panel @ 1440 px`** (Layout-Sweep, einmal rot: `[data-testid="rule-form"]` ragt unten aus dem Bild,
   1001,493 bis 1400,905 bei 1440 × 900): prüfen, ob Wettlauf im Zustand (wie 118819ea) oder echter Layout-Fehler.

## Acceptance criteria
- [ ] Je Flake die Ursache im Outcome, belegt (Reproduktion, Log oder Codestelle).
- [ ] Replay-Tests sehen keine Buchungen anderer Tests mehr; Gegenprobe: AC14 vor den Replay-Tests laufen lassen
      (Reihenfolge erzwingen) ist danach grün, vorher rot.
- [ ] AC14 prüft seine Aussage ohne Wanduhr-Grenze, die unter Last kippt, oder die Abweichung ist im Outcome begründet.
- [ ] Design-System-Export und `regeln-panel` ohne feste Wartezeiten, die unter Last zu kurz sind.
- [ ] Testzahlen je Kennung im Gate unverändert oder Abweichung begründet (kein Test still entfernt).
- [ ] `./verify.sh` fünfmal hintereinander `VERIFY: PASS`, davon zweimal mit einem zweiten `./verify.sh` parallel in
      einem anderen Worktree (die Last eines Work-Laufs); die Läufe im Outcome.

## Notes
- Gefunden im Work- und Review-Lauf 2026-11-20/21 (Protokoll, Fahrplan „Stand“ 2026-11-21).
- ADR 0100 (DB-Testlisten als eine Expecto-Gruppe) ist der vermutete Auslöser für Punkt 1; die Gruppe selbst bleibt.
- Kein Produktcode außer, falls Punkt 4 ein echter Layout-Fehler ist; dann dort mit Regressionstest.

## Outcome

Alle vier Flakes sind reproduziert oder an einer Codestelle belegt und deterministisch gemacht. Es gibt keine Wiederholung, kein angehobenes Zeitlimit und keinen Ausschluss aus dem Gate. Produktcode ist nicht geändert (`src/Server`, `src/Client` und `src/Shared` sind unberührt).

### 1. Ledger-Replay mit fremden „Perf“-Buchungen

- **Ursache:** Die Hypothese aus der Task (AC14) stimmt nicht. AC14 schreibt über `appendAndApply` Ereignisse und Zustand gemeinsam, und es läuft in derselben Gruppe. Die „Perf“-Buchungen standen nur deshalb in den Meldungen, weil Expecto bei einer langen Liste die ersten 100 Einträge zeigt und die 1000 Perf-Buchungen mit ihren Zufalls-Ids diese Plätze füllen. Der Unterschied selbst kam erst mit einer Diagnose ans Licht (vorübergehend eingebaut, wieder entfernt): Das Log hatte 3233 Buchungen, die Zustandstabellen 3230. Die drei zusätzlichen hatten Payee „Markt“ und Memo „Karte, Ref: ref-…“. Das ist die Standard-Antwort `FetchAnswer` der `Probe` in `src/Tests/BankImportTests.fs`. Deren Abruf bucht über `Ledger.CommandHandlers.pushComdirectImports` ins echte Kassenbuch der gemeinsamen In-Memory-DB. Die Listen dazu standen aber nicht in `TestGroups.InMemoryDb` und liefen deshalb parallel zur DB-Gruppe. Zwischen `loadAllEntries` und `replayAll` eines Replay-Tests kamen so neue Buchungen dazu. Das verstößt gegen ADR 0100 Punkt 1 und Doktrin Punkt 16.
- **Behebung:** Sechs Listen stehen jetzt in `testSequencedGroup TestGroups.InMemoryDb`, jeweils mit einem Kommentar „Geteilt: …“ darüber: „startSync: der Server entscheidet über die TAN“, „confirmTan: nach der TAN derselbe Abruf“, „startSync nach dem Neustart: ein gespeicherter Wert kostet keine TAN“, „startSync nach RetryLater: …“, „Gespeicherter Wert auf den Sync-Wegen: Challenge und Abbruch“ und „Bank trennen: die Sync-Sitzung“. Zusammen dauern sie weniger als 2 s.
- **Gegenprobe:** Die Reihenfolge „AC14 vor den Replay-Tests“ belegt nichts, weil AC14 nicht die Ursache war. Die Gegenprobe zur echten Ursache lief so: In die vier Replay-Tests von `LedgerTests.fs` kam vorübergehend ein Fenster von 300 ms zwischen den beiden Lesezugriffen. Dann liefen nur die Replay-Fälle und die Fälle von startSync, confirmTan und Bank trennen (57 Fälle, `--run`). Vor der Behebung war das dreimal von drei rot, jeweils „Replay aus dem Log …“ und „Replay rekonstruiert … allen drei Entstehungsarten“. Mit der Behebung war es dreimal von drei grün. Dazu kamen drei volle Expecto-Läufe gleichzeitig mit demselben Fenster, alle grün (5.583 bestanden). Ohne Fenster und ohne Behebung war vorher einer von drei gleichzeitigen Läufen rot. Die Replay-Tests selbst sind der Regressionstest: Sie werden rot, sobald eine Liste außerhalb der Gruppe Buchungen schreibt.

### 2. AC14-Zeittest

- **Ursache:** Die Grenze von 2 s maß Seeding und Abfrage zusammen, also fast nur das Schreiben der 1000 Ereignisse. Unter Last waren es 3,7 s, obwohl die Abfrage sich nicht verändert hatte.
- **Behebung:** In `src/Tests/LedgerPageTests.fs` gibt es keine Stoppuhr mehr. AC14 prüft weiter TotalCount = 3 unter 1000 Buchungen. Dazu prüft der Test den Zugriffsplan genau dieser Abfrage (Konto und Split-Kategorie, `explainEntryFilterPlan`): `SEARCH ledger_entries USING INDEX` und `SEARCH ledger_entry_split_categories`, keine Zeile `SCAN` und kein `CORRELATED`. Der gemessene Plan war: SEARCH über `idx_ledger_entries_account`, LIST SUBQUERY und SEARCH über den Index der Split-Kategorien. Der Fall heißt jetzt „AC14: 1000 Buchungen, drei mit der gesuchten Split-Kategorie, liefern TotalCount = 3 über Indizes ohne Tabellen-Scan“. Die Zahl der Fälle bleibt gleich. Ein Kommentar erklärt, warum die Wanduhr weg ist.

### 3. `[flow:design-system-export]`

- **Ursache:** Der Start von `vite preview` war es nicht. Seit infrastructure-r5k2w wartet das Skript auf den Port (höchstens 60 s), und der Kommentar in `verify.sh` war veraltet. Die drei roten Läufe der Nacht (Logs bb-verify.X7XjRu, jLRmVM und 9AsR3z) scheiterten alle an derselben Stelle. Sie liefen nach `components-categorypicker.html` in `waitForSelector('.fixed.inset-0.z-50.overflow-hidden')` und brachen nach 30 s ab. Der Grund steht in `CategoryPicker.fs` (`sheetWith`): Das Schließen des Pickers ruft `Viewport.swallowNextClick 350` auf, den Geisterklick-Schutz nach ADR 0005. Der nächste Klick auf der Seite innerhalb von 350 ms wird verschluckt. Danach klickte das Skript den Auslöser des Modals. Reproduziert: Klick sofort nach dem Schließen, Modal nicht offen (zweimal). Klick nach 600 ms, Modal offen.
- **Behebung:** In `scripts/export-design-system.mjs` kommt das Modal jetzt vor BottomSheet und Kategorienauswahl. Die Kategorienauswahl ist die letzte Karte, nach ihr folgt kein Klick mehr. Ein Kommentar erklärt die Reihenfolge. Eine feste Wartezeit gibt es nicht. Vier Export-Läufe gleichzeitig waren grün. In `verify.sh` steht jetzt im Kommentar zum Lock, dass das Skript seit r5k2w auf den Port wartet. Das Lock selbst bleibt.

### 4. `regeln-panel @ 1440 px`

- **Ursache:** Das ist ein Wettlauf im Sweep und kein Layout-Fehler. Das Popover rückt beim Öffnen per `scrollIntoView` (FilterPopover `viewRevealed`) ins Bild. Das Panel scrollt sofort um 87 px. Die letzten 5 px scrollt das Dokument, das hier 937 px hoch ist. Dieses Scrollen ist wegen `scroll-behavior: smooth` am `html` (`styles.css`) animiert, und `settle` erkennt es nicht, weil es keine Web-Animation ist. Messung im Leerlauf: direkt nach dem Öffnen steht das Popover bei 493 bis 905, genau der Wert aus der Meldung im Gate. Danach steht es bei 488 bis 900. Mit `scroll-behavior: auto` steht es sofort bei 488 bis 900. Der Endzustand ist ganz im Bild.
- **Gleiche Ursache, zweiter Zustand:** Im Gate-Lauf 1 dieser Task war `targets-pruefen-nicht-neu-gelesen @ 412 px` rot („Neu laden“ bei y −219, also mitten in `window.scrollTo(0, 0)`, das weich scrollt). Im Last-Lauf 4 auf main-HEAD ohne diese Änderung war derselbe Zustand wieder rot (y −312). Die Behebung aus 118819ea reichte also nicht.
- **Behebung:** In `runState` (`scripts/layout-invariants.mjs`) setzt der Sweep nach dem Laden der Seite `html { scroll-behavior: auto !important; }` für alle Zustände. Wohin die App scrollt, entscheidet weiter die App. Nur die Animation fällt weg, und Bewegung prüft der Sweep ohnehin nicht. Der Sweep allein (`layout-invariants.test.mjs`): pass 28, fail 0.

### Testzahlen

Expecto 5.583 Fälle wie vorher. Browser-Tests je Kennung unverändert: „layout 32, fokus 14, tastatur 24, scroll 7, fable 8, flow 37, Summe 122“, gleich wie in den Läufen auf main. Kein Test ist entfernt.

### Gate-Läufe

1. allein: `VERIFY: FAIL (rote Schritte: test:scripts)`. Sweep-Fehler `targets-pruefen-nicht-neu-gelesen @ 412 px`, danach behoben (siehe 4).
2. allein: `VERIFY: PASS (dotnet 5583 grün, node 358 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`
3. parallel zu einem `./verify.sh` im Last-Worktree `.worktrees/q8f3n-load`: `VERIFY: PASS (dotnet 5583 grün, node 358 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`. Der Last-Lauf war ebenfalls PASS.
4. parallel zu einem `./verify.sh` im Last-Worktree: `VERIFY: PASS (dotnet 5583 grün, node 358 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`. Der Last-Lauf auf main-HEAD war FAIL am Sweep (`targets-pruefen-nicht-neu-gelesen @ 412 px`, ohne diese Änderung).
5. allein: abgebrochen durch die Unterbrechung der Sitzung (`verify.sh abgebrochen (TERM)` in Schritt 6), kein Ergebnis.
6. allein: `VERIFY: PASS (dotnet 5583 grün, node 358 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`
7. allein: `VERIFY: PASS (dotnet 5583 grün, node 358 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`

Damit gibt es fünf PASS ab Lauf 2 ohne einen roten Lauf dazwischen (2, 3, 4, 6, 7), davon zwei unter paralleler Last. Lauf 5 hat kein Urteil. Insgesamt liefen sieben Gates in diesem Worktree und zwei im Last-Worktree. Die Schritte 6 der beiden Gates laufen über das Lock nacheinander, parallel liefen Build, Expecto und Client-Build. Auch der Gate-Lauf des Geschwister-Workers budgeting-ffjk9 lief zeitweise gleichzeitig. Den Last-Worktree hat `scripts/worktree-teardown.sh` wieder abgebaut.

### Für die Kann-Sammlung infrastructure

- Ein Laufzeit-Wächter, der Schreibzugriffe auf die gemeinsame DB von Listen außerhalb von `TestGroups.InMemoryDb` meldet. Heute fängt das nur ein roter Replay-Test.
- `BankImportTests.fs` „Automatisch abrufen: der jüngste Abruf aus der Historie“ schreibt `sync_sessions` in der gemeinsamen DB außerhalb der Gruppe. Das ist heute kein Flake, gehört aber laut ADR 0100 in die Gruppe.
- AC14 schreibt weiter 1000 Buchungen in die gemeinsame DB (ADR 0100 Punkt 3). Eine eigene DB bräuchte eine injizierbare Verbindung für `appendAndApply` und `getLedgerEntriesPage`.
- Der Workaround aus 118819ea in `targets-pruefen-nicht-neu-gelesen` (zwei Frames, dann nach oben scrollen) ist mit Punkt 4 vielleicht entbehrlich und kann geprüft werden.
