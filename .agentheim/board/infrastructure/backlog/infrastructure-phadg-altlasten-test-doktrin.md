---
id: infrastructure-phadg
title: "Altlasten nach der Test-Doktrin: nachgebaute Tests und Quelltext-Tests auf View-Verdrahtung ersetzen"
status: backlog
type: chore
context: infrastructure
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview, aufraeumen, tests, ynab, test, doktrin, harness, security]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Tests, die Produktionslogik nachbauen oder Quelltext per Teilstring lesen, verstoßen gegen die Test-Doktrin (Punkte 6 und 7). Sie bleiben bei echten Fehlern grün und werden bei gleichwertigen Refactorings rot. Die Triage vom 2026-11-06 hat sechs Einzeltickets hier gebündelt. Kein Fehler im Alltag, deshalb niedrige Priorität.

## What
Die aufgenommenen Tickets stehen unten vollständig. Geschnitten und priorisiert wird im Refinement.

## Aufgenommen aus categorization-mznx3 (Triage 2026-11-06)
**Testlücken und nachgebaute Tests** (chore, erfasst 2026-10-30)

### Why
Gruppe 7 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Tests rechnen Produktionslogik im Test nach oder fehlen für Ablehnungen, Gegenproben und deaktivierte Regeln.

### What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Tests/RulesEngineTests.fs:157` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Payee- und Memo-Regeln haben keine Gegenprobe gegen das jeweils andere Feld.
- `src/Server/Api.fs:535 und :604` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule (bei Regex kompilierbar); .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 4 | mittel | Ablehnung eines kaputten Regex beim Anlegen und Ändern einer Regel ist ungetestet.
- `src/Tests/PersistenceTypeConversionTests.fs:81` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Eine deaktivierte Regel ist im Persistenz-Rundlauf nicht abgesichert.
- `src/Client/Components/Rules/State.fs:187` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/board/categorization/done/cat-001-rule-reorder-ui.md, AC "Bei Fehler: Toast + Reload" | mittel | Fehlgeschlagenes Umsortieren der Regeln hat keinen Client-update-Test.
- `src/Tests/YnabClientTests.fs:690` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests rechnet den PayeeOverride im Test nach.
- `src/Tests/YnabClientTests.fs:1100` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests baut den Transaktions-Encoder im Test nach.
- `src/Tests/YnabClientTests.fs:448` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | Der Kategorien-Decoder ist im Test kopiert statt aus dem Produktionscode aufgerufen.
- `src/Tests/RulesEngineTests.fs:215` | test-doctrine.md, Stil Punkt 19, Kein Test für Punkt 6 | niedrig | Test heißt High Priority, prüft aber nur die Listenreihenfolge; Daten und Namen auf "erste Regel der Liste gewinnt".
- `src/Client/Components/Rules/State.fs:303` | test-doctrine.md, Heimat-Ebene Punkt 1 | niedrig | update-Test für `RuleSaved (Ok _)`: neu vorn, bearbeitet an derselben Stelle, Dialog zu.
- `src/Tests/RulesEngineTests.fs:601` | test-doctrine.md, Heimat-Ebene Punkt 2 | niedrig | `textBasedSpecialLinkTests` doppelt `specialPatternTests` (303 bis 356); zusammenlegen.
- `src/Tests/RulesListTests.fs:104` | test-doctrine.md, Kein Test für Punkt 6 (Konstanten) | niedrig | Wortlaut-Vergleich auf `priorityHint` streichen.
- `src/Tests/RulesEngineTests.fs:60` | test-doctrine.md, Stil Punkt 19 | niedrig | englische Testnamen (auch OrderIdMatcherTests.fs:64ff., RulesReorderTests.fs:40ff.) beim nächsten Anfassen als deutsche fachliche Sätze.

### Acceptance criteria
- [ ] To be defined during refinement.

### Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.

## Aufgenommen aus infrastructure-k3w8n (Triage 2026-11-06)
**Nachgebaute Tests in YnabClientTests gegen Produktionscode stellen** (chore, erfasst 2026-11-05)

### Why
Beim Entfernen von `createTransactions` (infrastructure-w7d2c) fiel auf: weitere Tests in `src/Tests/YnabClientTests.fs` bilden Produktionslogik im Test nach, statt sie aufzurufen (Test-Doktrin Punkt 6). Betroffen sind in "Transaction Conversion Tests" etwa "uses PayeeOverride when provided", "memo with reference fits within 300 char limit", "whitespace in memo is compressed", "filters out skipped transactions", "uncategorized transactions pass filter" und "transaction dated today is not filtered out", dazu die "Milliunits Conversion Tests", die nur Arithmetik im Test rechnen.

### What
Jeden dieser Tests prüfen: ist das Verhalten schon gegen `prepareTransactions`, `buildTransactionRequest`, `buildMemoWithReference`, `resolvePayeeName` oder `Shared.Budgeting.toMilliunits` getestet, fliegt er raus; sonst wird er auf die Produktionsfunktion umgestellt.

### Acceptance criteria
- [ ] Kein Test in `YnabClientTests.fs` bildet Produktionslogik nach, ohne Produktionscode aufzurufen.
- [ ] Das Outcome nennt je entferntem Test den Test, der sein Verhalten abdeckt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aufgenommen aus infrastructure-q7r4m (Triage 2026-11-06)
**Strukturtests auf View-Verdrahtung in CategoryPickerTests und RulesListTests nach Doktrin Punkt 7 ablösen** (chore, erfasst 2026-11-03)

### Why
Bei infrastructure-t5m2b aufgefallen: `src/Tests/CategoryPickerTests.fs` (etwa "Zeilen kommen aus PickerParts.row") und die Quelltext-Liste in `src/Tests/RulesListTests.fs` lesen Quelltext, um View-Verdrahtung zu prüfen. Doktrin Punkt 7 verbietet neue View-Verdrahtung per Teilstring.

### What
Je Test prüfen: Verhaltenszugang über eine reine Funktion oder Client-`update` schaffen und den Strukturtest löschen, oder begründen, warum er eine repo-weite Invariante ist.

### Acceptance criteria
- [ ] Kein Strukturtest auf View-Verdrahtung mehr in beiden Dateien, oder je verbleibendem Test eine Begründung nach Doktrin 7.
- [ ] Je gelöschtem Test eine Mutante, die den Ersatz rot macht.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aufgenommen aus infrastructure-q7v2m (Triage 2026-11-06)
**Übrige Quelltext-Tests auf View-Verdrahtung durch update- oder Browser-Tests ersetzen** (refactor, erfasst 2026-10-29)

### Why
Bericht D (Abschnitt 3) zählt rund 140 Tests, die View-Quelltext per Teilstring lesen, vor allem in `src/Tests/LedgerViewTests.fs`, `LedgerDetailTests.fs`, `LedgerSplitEditorClientTests.fs`, `LedgerCategoryChangeClientTests.fs`, `LedgerFilterButtonTests.fs` und `LedgerKeyboardTests.fs`. Sie fixieren Bezeichner und Formatierung: ein gleichwertiges Refactoring macht sie rot, ein Fehler mit gleichem Text lässt sie grün. infrastructure-hdf7j hat nur die namentlich genannten Fälle bearbeitet.

### What
Je Datei die Quelltext-Tests sichten. Was ein Browser-Wächter oder ein `update`-Test schon abdeckt, löschen. Was nicht abgedeckt ist, auf die billigste Verhaltensebene heben (Doktrin Punkt 1). Repo-weite Invarianten mit Gegenprobe bleiben.

### Acceptance criteria
- [ ] Für jede der genannten Dateien ist jeder Quelltext-Test gelöscht, ersetzt oder mit einer Zeile Begründung behalten.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aufgenommen aus infrastructure-c4r8n (Triage 2026-11-06)
**CORS-Abwesenheit als Host-Test statt Quelltext-Suche** (refactor, erfasst 2026-10-29)

### Why
`src/Tests/CorsTests.fs` sucht Zeichenketten wie `Cors` in den Server-Quellen (Audit B1 N8, C B10). Eine Policy über Konfiguration oder ein eigenes Middleware-Lambda, das `Access-Control-Allow-Origin` setzt, bliebe unentdeckt.

### What
Die Middleware-Kette aus `src/Server/Program.fs` in eine Funktion ziehen (etwa `configureApp`) und in einem TestServer prüfen, dass ein Preflight `OPTIONS` mit `Origin` keinen `Access-Control-Allow-Origin` liefert. Danach die Quelltext-Suche löschen oder als Zusatz behalten.

### Acceptance criteria
- [ ] Ein Test schickt ein Preflight mit `Origin` durch die echte Middleware-Kette und erwartet keinen CORS-Header.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aufgenommen aus design-system-fpem2 (Triage 2026-11-06)
**Wächter: .NET-Tests prüfen keine Tailwind-Größen per Klassenstring** (chore, erfasst 2026-10-30)

### Why
Muster: Bei UI-Tasks mit Größenvorgaben sichern Worker die Trefferfläche im .NET-Test über Klassenstrings ab, weil .NET kein Layout messen kann. Der Test bleibt grün, auch wenn der Knopf die Klasse nie bekommt. Vorgekommen in 897b13c (`ButtonSizeTests.fs`, Audit B1 N7, C B19, in infrastructure-hdf7j gelöscht) und im Nachspiel von design-system-j6dnj bei der Messung nach M12 (`baseline.md`). Die Prosa-Regel `.claude/rules/test-doctrine.md` Punkt 6 hat nicht gegriffen.

### What
Ein Wächter in `src/Tests` sucht in `src/Tests/*.fs` nach Tailwind-Größen als Literal (etwa `min-h-[48px]`, `w-[..px]`), mit eingebautem Gegenbeweis (Doktrin Punkt 7). Bestandsstellen ersetzen oder mit Begründung auf eine Ausnahmeliste setzen: `AuswahlTests.fs:99`, `ButtonSizeTests.fs:17` (`iconOnlyClass` prüft eine Verzweigung, bleibt), `LedgerSortSheetTests.fs:138`, `LedgerViewTests.fs:1257`, `RuhigeListenTests.fs:122`.

### Acceptance criteria
- [ ] To be defined during refinement.

### Notes
Captured via `quick-capture` on 2026-10-30 aus dem `learn`-Skill, raw, unrefined. Needs a `modeling` refine pass before it can be promoted.
