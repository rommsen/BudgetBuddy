---
id: budgeting-r5h2n
title: "Allgemeine Historie zeigt alles, was das Budget betrifft, auch Target-Änderungen und Pausen"
status: done
type: feature
context: budgeting
created: 2026-11-07
completed:
depends_on: [budgeting-my8p8]
blocks: []
tags: [budget, historie, targets, pause]
related_adrs: [0080, 0084]
related_research: []
prior_art: [budgeting-h7q4v, budgeting-c6vn4, budgeting-t6p8z, budgeting-k8wq3, budgeting-eh8xe, budgeting-dgrp7]
---

## Why
Roman am 2026-11-07: Er hat eine Target-Änderung in der allgemeinen Historie (`#/budget/history`) gesucht und nicht gefunden. Dort stehen nach Design (budgeting-h7q4v) nur Budget-Bewegungen; Target-Änderungen und Pausen stehen nur im Tab Historie je Kategorie, und dort im Monat ihres `effectiveFrom`.

Romans Wunsch: "es soll alles was mit budgets zu tun hat da rein".

## Entscheidung
Roman, 2026-11-07 (AskUserQuestion):
- Die allgemeine Historie zeigt alles, was das Budget betrifft: zusätzlich Target-Änderungen (`TargetSet`, `TargetRemoved`, Altbestand `YnabGoalChangedFromBb` mit Stand) und Pausen (`TargetPaused`, `TargetPauseLifted`).
- Filter: ein neuer Chip "Targets" für Target-Änderungen und Pausen zusammen. Unter "Alle" stehen sie gemischt mit den Bewegungen, neueste zuerst.
- `TargetTakenOverFromYnab` (Startstand bei der Übernahme) erscheint nicht.
- Keine Skizze. Die bestehenden Zeilenbausteine und Texte des Tabs Historie (`targetChangeText`, "Target pausiert", "Pause aufgehoben") werden genutzt, je eine eigene Zeile mit Kategorie, ohne Betrag, mit Zeitpunkt in Berliner Zeit und Monat. Eine Target-Änderung hat ihren Stand wie im Tab Historie, eine Pause keinen.

Vom Conductor am Code entschieden (Begründung in Notes): Laden in drei Abfragen über alle Streams, Mischen auf dem Server mit `mergeHistory`, `getBudgetHistory` liefert `HistoryEntry list`, Text "Art, Kategorie" wie bei den Durchläufen, keine Grenze.

## What

### Server: lesen über alle Streams, mischen
- `Budgeting.Persistence` bekommt zwei Leser über alle Streams, je eine Abfrage (`loadBudgetEventsOn` mit `OfTypes`), keine Abfrage je Kategorie oder Monat:
  - **Pausen**: `OfTypes [ EventTypes.paused; EventTypes.pauseLifted ]` über alle Monats-Streams. Je Ereignis ein `HistoryEntry.TargetPaused` oder `HistoryEntry.PauseLifted` wie in `queryPauseEvents`, ohne Filter auf Kategorie und Monat, neueste zuerst.
  - **Target-Änderungen**: `OfTypes (EventTypes.targetRules @ EventTypes.goalChanges)` über alle Target-Streams. Die Übernahme wird mitgelesen, weil sie der Startstand der Faltung ist ("vorher" der ersten Änderung), sie wird keine Zeile. Gruppiert nach `stream_id` (die Kategorie aus `categoryOfTargetStream`), je Stream dieselbe Ableitung wie heute in `queryTargetChanges`: `TargetRules.withStates`, `changeKindBetween`, Altbestand mit `latestGoalSteps`, `categoryNameIn`, nur ohne Monatsfilter.
  - Die Ableitung je Stream wird aus `queryTargetChanges` herausgezogen und von beiden Lesern genutzt; `queryTargetChanges` filtert danach nur noch auf den Monat. Der Tab Historie bleibt unverändert.
- `HistoryPort` bekommt zwei Felder (`LoadAllPauseEvents`, `LoadAllTargetChanges`, Namen frei), `liveHistoryPort` schließt die beiden Leser an.
- `budgetHistory` liest Bewegungen (wie bisher, `MovementFilter` ohne Einschränkung), Pausen und Target-Änderungen und mischt mit `mergeHistory` (stabil, neueste zuerst, bei gleichem Zeitpunkt Bewegung vor Pause vor Target-Änderung). Ein Lesefehler einer Quelle macht die ganze Antwort zum `Error` mit dem Text der Quelle (`Budget-Bewegungen nicht lesbar`, `PauseEventsUnreadableText`, `TargetChangesUnreadableText`), die Ursache steht im Server-Log, wie bei `categoryHistory`.

### Vertrag
- `getBudgetHistory: unit -> Async<Result<HistoryEntry list, string>>` statt `HistoryMovement list`. `HistoryEntry` gibt es schon (Tab Historie), es kommt kein Pflichtfeld in einen bestehenden Record. Der Vertragskommentar in `src/Shared/Api.fs` nennt Bewegungen, Pausen und Target-Änderungen und streicht den Verweis auf die Folgeaufgabe budgeting-q8m2v (verworfen am 2026-11-06).
- Die Fixtures in `scripts/budget-history.test.mjs` (`movements`, `manyMovements`) werden im selben Schritt auf `HistoryEntry` umgestellt: jede Bewegung als Fall `Movement`, so wie Fable.Remoting einen Union-Fall mit Record kodiert (Muster wie `Origin: { Decision: … }` im selben Fixture).

### Shared: Zeilen der allgemeinen Historie
- Ein neuer Typ für eine Zeile der allgemeinen Historie mit zwei Fällen: ein Durchlauf (`HistoryRun` wie bisher) und eine Target-Zeile (Pause oder Target-Änderung). Die Target-Zeile trägt Id, Zeitpunkt, Monat, Text und `HistoryStatus option`.
- Eine reine Funktion (etwa `generalHistoryRows: HistoryEntry list -> GeneralHistoryRow list`) bildet aus den Bewegungen die Durchläufe (`historyRuns`, unverändert) und aus jeder Pause und Target-Änderung eine eigene Zeile, sortiert neueste zuerst. Ein Durchlauf zählt wie bisher mit seiner jüngsten Bewegung. Bei gleichem Zeitpunkt steht der Durchlauf vor der Pause und die Pause vor der Target-Änderung (wie `mergeHistory`).
- Text der Target-Zeile nach dem Muster "Art, Kategorie" der Durchläufe ("Zuweisen, Urlaub"), mit dem Text aus `historyEntryLine`: "Target pausiert, Urlaub", "Pause aufgehoben, Urlaub", "Target 50,00 € → 60,00 €, Urlaub", "Target entfernt, Urlaub", "Target angelegt, 50,00 € monatlich, Urlaub". Ein leerer Kategoriename (ein Target-Stream ohne Übernahme und ohne Altbestand trägt keinen Namen) wird zu "unbekannte Kategorie" (`UnknownCategoryText`).
- Stand: der aus `historyEntryLine` (`HistoryLine.Status`). Pause und Änderung nur in BB: `None`. Altbestand: `targetChangeStatus`, also auch `HistoryPartlyWritten` oder `HistoryFailed` mit Grund.
- Monat: bei der Pause `HistoryPause.Month`, bei der Änderung `HistoryTargetChange.Month` (`effectiveFrom` bzw. Monat des Altbestands).

### Client
- `RunFilter` bekommt einen Fall für Target-Änderungen und Pausen. `runFilters` in dieser Reihenfolge: Alle, Targets anwenden, Verschieben, Zuweisen, **Targets**, In YNAB zugewiesen, Aus YNAB übernommen (der neue Chip steht bei dem, was in BB entschieden wurde). `runFilterLabel` "Targets".
- `shownRuns` (bzw. sein Nachfolger für Zeilen): Alle zeigt alles, "Targets" nur die Target-Zeilen, jeder andere Chip nur Durchläufe seiner Herkunft, keine Target-Zeilen.
- `emptyText`: "Targets" sagt "Keine Target-Änderungen oder Pausen". Der Leertext ohne Filter nennt Target-Änderungen und Pausen mit ("Noch keine Einträge. Targets anwenden, Verschieben, Zuweisen, Target-Änderungen, Pausen und Änderungen aus YNAB erscheinen hier.").
- `GeneralHistory.Runs` trägt die neuen Zeilen (Feldname darf `Rows` werden). `applyGeneralHistoryLoaded` nimmt `Result<HistoryEntry list, string>` und ruft die reine Funktion. Generation, Wiederholen (Filter und Aufklappen bleiben, auch der Filter "Targets"), erneutes Betreten (von vorn) und Rückkehr bleiben wie heute.
- `HistoryView`: eine Target-Zeile nutzt die Bausteine der Durchlauf-Zeile (Zeile 1 Text, Zeile 2 Zeit mit `historyTimeText`, Monat am Handy kurz mit `shortMonthYearLabel`, am PC `monthLabel`, Stand mit `historyStatusView` und `historyFailureView`). Kein Betrag, kein Chevron, nicht aufklappbar. Ohne Stand kein Badge (wie `NoBadge`). Am Handy bricht der Text der Target-Zeile ohne Grenze um, damit die Kategorie am Ende einer langen Änderung ("Rhythmus wöchentlich, Wochentag Montag, Target …, Urlaub") nicht abgeschnitten wird; am PC `truncate` mit dem ganzen Text im `title`.

## Acceptance criteria
- [x] Abfrage-Ebene (In-Memory-SQLite, über `budget.getBudgetHistory` im Stil von `BudgetGeneralHistoryTests.fs`): eine Pause und ihre Aufhebung in zwei verschiedenen Monaten und eine Target-Änderung einer anderen Kategorie erscheinen zusammen mit den Bewegungen, gemischt und neueste zuerst; Monat und Kategorie stimmen je Eintrag.
- [x] Abfrage-Ebene: eine Kategorie mit `TargetTakenOverFromYnab` (50 €), danach `TargetSet` (60 €) und `TargetRemoved`: die Antwort hat genau zwei Target-Änderungen, "Target 50,00 € → 60,00 €" (`TargetEdited`, vorher aus der Übernahme) und "Target entfernt"; die Übernahme selbst steht nicht darin.
- [x] Abfrage-Ebene: ein Altbestand `YnabGoalChangedFromBb` mit geschriebenem Schritt 1 und gescheitertem Schritt 2 kommt mit `SentToYnab = true` und beiden Schritt-Ergebnissen, wie im Tab Historie.
- [x] Abfrage-Ebene: der Tab Historie (`getCategoryHistory`) liefert nach dem Umbau dieselben Target-Änderungen und Pausen je Kategorie und Monat wie vorher; die bestehenden Tests dazu bleiben ohne Änderung grün.
- [x] Handler-Ebene mit Port als Funktionswerten: scheitert der Leser der Pausen oder der Target-Änderungen (Ausnahme oder `Error`), ist die Antwort `Error` mit `PauseEventsUnreadableText` bzw. `TargetChangesUnreadableText`; ein Lesefehler der Bewegungen bleibt "Budget-Bewegungen nicht lesbar". Bei gleichem Zeitpunkt steht die Bewegung vor der Pause und die Pause vor der Target-Änderung.
- [x] `liveHistoryPort` schließt beide neuen Leser an; belegt durch die Abfrage-Tests oben, die über den echten Endpoint lesen (Doktrin Punkt 3). Die Endpoint-Negativzeile `BudgetApi.getBudgetHistory` bleibt grün.
- [x] Domain-Ebene (`generalHistoryRows` o. ä. in `src/Shared/Budgeting.fs`): Durchläufe und Target-Zeilen gemischt neueste zuerst; bei gleichem Zeitpunkt Durchlauf vor Pause vor Target-Änderung; ein Durchlauf zählt mit seiner jüngsten Bewegung.
- [x] Domain-Ebene: Texte "Target pausiert, Urlaub", "Pause aufgehoben, Urlaub", "Target 50,00 € → 60,00 €, Urlaub", "Target entfernt, Urlaub"; ein leerer Name wird "unbekannte Kategorie"; Stand `None` bei Pause und Änderung in BB, beim Altbestand mit gescheitertem Datum `HistoryPartlyWritten` mit Grund; Monat aus Pause bzw. Änderung.
- [x] Client-`update` unter .NET: der Filter "Targets" zeigt nur Target-Zeilen, "Alle" alles gemischt, "Zuweisen" keine Target-Zeile; `runFilters` hat "Targets" an fünfter Stelle; `emptyText` für "Targets" und ohne Filter wie unter What.
- [x] Client-`update` unter .NET: Wiederholen mit gewähltem Filter "Targets" behält ihn, erneutes Betreten setzt auf Alle zurück, eine Antwort mit alter Generation ersetzt nichts (bestehende Tests auf `HistoryEntry` umgestellt).
- [x] Fixtures in `scripts/budget-history.test.mjs` auf `HistoryEntry` (Fall `Movement`) umgestellt; `[flow:budget-historie]` und der `[layout]`-Test laufen grün. Kein neuer Browser-Test: der Chip passt in die waagerecht scrollbare Leiste (`overflow-x: auto`, schon geprüft), Texte, Filter und Reihenfolge sind unter .NET geprüft, das Dekodieren von `HistoryEntry` im Fable-Kompilat nutzt schon der Tab Historie.
- [x] Seite mit `npm run screenshot:route` bei 390 px und am PC angesehen, mit Fixture-Daten, die eine lange Target-Änderung enthalten: die Kategorie steht am Handy ganz da, kein Betrag, Stand-Badge nur beim Altbestand. Screenshots im Outcome genannt.
- [x] README budgeting: **Allgemeine Historie** streicht "Pausen und Target-Änderungen stehen nicht darin, sie bleiben im Tab Historie je Kategorie", beschreibt die Target-Zeilen (Text "Art, Kategorie", ohne Betrag, Stand wie im Tab Historie), nennt die Filter vollständig (Alle, Targets anwenden, Verschieben, Zuweisen, Targets, In YNAB zugewiesen, Aus YNAB übernommen) und sagt, dass `getBudgetHistory` Bewegungen, Pausen und Target-Änderungen ohne Grenze liest und der Server mit `mergeHistory` mischt. **Pause in der Historie** und **Target-Änderung** nennen die allgemeine Historie mit. Delta vorher mit `npm run check:readme-delta -- --bc budgeting` geprüft.
- [x] `./verify.sh` zeigt `VERIFY: PASS`.

## Abgrenzung
- `TargetTakenOverFromYnab` erscheint nicht (Roman), auch nicht unter "Aus YNAB übernommen".
- Der Chip "Targets" zeigt keine Durchläufe "Targets anwenden"; die bleiben unter ihrem eigenen Chip.
- Das Ausblenden der Leiste Monatserster ist Zustand, kein Ereignis (ADR 0083), und erscheint nicht.
- Keine Grenze und keine Seiten für `getBudgetHistory` (siehe Notes).
- Der Tab Historie je Kategorie ändert sich nicht.
- Keine neue Skizze, keine neue DS-Komponente.

## Notes
- **Laden ohne N+1** (`.claude/rules/query-safety.md`): `getBudgetHistory` macht drei Abfragen auf `events` (Bewegungen wie heute, Pausen per Typfilter, Target-Streams per Typfilter), unabhängig von der Zahl der Kategorien und Monate. Die Target-Ableitung braucht den Stream je Kategorie vollständig in Reihenfolge; `loadBudgetEventsOn` sortiert schon `ORDER BY stream_id, stream_version`, gruppiert wird im Code.
- **"Art, vorher, nachher" ohne Kategorie-Kontext**: je Target-Stream dieselbe Faltung wie im Tab Historie (`TargetRules.withStates` liefert je Ereignis den Stand davor und danach, `changeKindBetween` die Art). Dafür muss der Typfilter die Übernahme einschließen, sonst wäre die erste Änderung nach der Übernahme fälschlich "angelegt". `EventTypes.targetRules @ EventTypes.goalChanges` deckt Übernahme, `TargetSet`, `TargetRemoved`, Altbestand und Schritt-Ergebnisse ab; der Altbestand geht nicht in die Faltung (`step` ignoriert ihn).
- **Rückgabetyp**: `HistoryEntry` hat die Fälle schon und wird vom Tab Historie über Fable.Remoting gelesen. Ein neues Pflichtfeld in `HistoryMovement` oder `HistoryRun` entfällt so. Die Laufzeitfalle bleibt: die Fixtures liefern heute rohe `HistoryMovement`-Objekte und würden nach der Änderung still falsch dekodiert, deshalb das Kriterium zu den Fixtures. Ein im Browser offener alter Client bekommt nach dem Deploy einen Fehler beim Dekodieren und braucht ein Neuladen (Single-User, hingenommen). Andere Fixtures mocken `getBudgetHistory` nicht (`scripts/screenshot-route.mjs` kennt nur die Route).
- **Wo gemischt wird**: Server mit `mergeHistory` wie beim Tab Historie, der Vertrag ist damit "neueste zuerst". Das Zusammenfassen zu Durchläufen bleibt im Client (`historyRuns`), deshalb sortiert die reine Funktion die Zeilen nach dem Gruppieren noch einmal (ein Durchlauf zählt mit seiner jüngsten Bewegung), mit derselben Gleichstandsregel.
- **Text mit Kategorie hinten**: so lesen sich alle Zeilen der Liste gleich ("Zuweisen, Urlaub"). Weil die Kategorie dadurch am Ende einer langen Änderung steht, bricht die Target-Zeile am Handy ohne `line-clamp` um. Ein `[layout]`-Test dafür entfällt nach Vorgabe; geprüft wird per Screenshot.
- **Menge, keine Grenze**: eine Zeile je Pause- oder Target-Ereignis. Ein Haushalt mit einigen Dutzend Kategorien pausiert und ändert Targets selten, das sind eher Dutzende als Hunderte Ereignisse im Jahr. Die Bewegungen bleiben der größte Teil (jedes Targets anwenden und jede Monatsübernahme bringt je Kategorie eine Zeile, die der Server schon heute liefert). Mitgelesen wird zusätzlich je Kategorie ein `TargetTakenOverFromYnab`, ohne eigene Zeile. Die Begrenzung hat Roman am 2026-11-06 verworfen (budgeting-q8m2v: "Historie begrenzen lohnt bei einem Haushalt nicht"); die neuen Ereignisse ändern die Größenordnung nicht. Deshalb keine Frage an Roman.
- Eine Pause ohne Target (wirkt nicht, budgeting-r3wq8) steht trotzdem in der Historie, wie im Tab Historie.
- Keine echten Beträge oder Kategorienamen in Tests und Doku (öffentliches Repo); Beispiele hier sind erfunden.

## Outcome

Die allgemeine Historie `#/budget/history` zeigt alles, was das Budget betrifft: Durchläufe wie bisher, dazu je Pause, Aufhebung und Target-Änderung (in BB und Altbestand) eine eigene Zeile. `TargetTakenOverFromYnab` erscheint nicht.

- **Server** (`src/Server/Budgeting/Persistence.fs`): `targetChangesIn` ist die Ableitung je Target-Stream (Faltung `TargetRules.withStates`, `changeKindBetween`, Altbestand mit `latestGoalSteps`, `categoryNameIn`), aus `queryTargetChanges` herausgezogen. `queryTargetChanges` filtert danach nur noch auf den Monat, der Tab Historie bleibt gleich. Neu sind `queryAllPauseEvents` (`OfTypes [paused; pauseLifted]`) und `queryAllTargetChanges` (`OfTypes (targetRules @ goalChanges)`, gruppiert nach `stream_id`), je eine Abfrage, kein N+1, beide neueste zuerst. Dazu `loadAll*With` und `loadAll*` über `readWith` mit `PauseEventsUnreadableText` bzw. `TargetChangesUnreadableText`. `pauseEntryOf` wird von beiden Pause-Lesern genutzt.
- **Handler** (`src/Server/Budgeting/QueryHandlers.fs`): `HistoryPort` hat `LoadAllPauseEvents` und `LoadAllTargetChanges`. `budgetHistory` liest Bewegungen, Pausen und Target-Änderungen und mischt mit `mergeHistory`. Eine Ausnahme wird über `readSource` zum Text der Quelle (gemeinsam mit `categoryHistory`), ein `Error` des Lesers bleibt sein Text. `liveHistoryPort` schließt `loadAllPauseEvents` und `loadAllTargetChanges` an. Die einzige Live-Anschlussstelle ist `getBudgetHistory` in `src/Server/Api.fs` (unverändert, über `liveHistoryPort`), belegt durch die Abfrage-Tests über `budget.getBudgetHistory`.
- **Vertrag** (`src/Shared/Api.fs`): `getBudgetHistory: unit -> Async<Result<HistoryEntry list, string>>`, der Kommentar nennt Bewegungen, Pausen und Target-Änderungen; der Verweis auf budgeting-q8m2v ist gestrichen.
- **Shared** (`src/Shared/Budgeting.fs`): `HistoryTargetRow` (Id, Zeitpunkt, Monat, Text, `HistoryStatus option`), `GeneralHistoryRow = RunRow | TargetRow` und `generalHistoryRows`. Der Text kommt aus `historyEntryLine` plus ", Kategorie", ein leerer Name wird `UnknownCategoryText`. Sortiert neueste zuerst, bei Gleichstand Durchlauf vor Pause vor Target-Änderung.
- **Client**: `RunFilter.TargetChanges` (Label "Targets", fünfte Stelle in `runFilters`), `shownRows` statt `shownRuns`, `emptyText` mit den neuen Sätzen, `GeneralHistory.Rows` statt `Runs`, `applyGeneralHistoryLoaded` nimmt `HistoryEntry list` (`History.fs`, `Types.fs`, `State.fs`). `HistoryView.fs`: `rowMeta` und `statusParts` sind die gemeinsamen Bausteine von Durchlauf und Target-Zeile. Die Target-Zeile (`data-testid` `general-history-target`) hat keinen Betrag und keinen Chevron, bricht am Handy ohne Grenze um, hat am PC `truncate` mit `title`, ohne Stand gibt es kein Badge.
- **Fixtures**: `scripts/budget-history.test.mjs` liefert jede Bewegung als `{ Movement: {...} }`. `[flow:budget-historie]` und `[layout]` sind grün, ein neuer Browser-Test entfällt.

Tests (11 neu):
- `src/Tests/BudgetGeneralHistoryTests.fs`: `generalRowTests` (Mischen und Gleichstand, ein Durchlauf zählt mit seiner jüngsten Bewegung; Texte, unbekannte Kategorie, Monat, Stand `None`; Altbestand `HistoryPartlyWritten "Rate-Limit"`), `allStreamsQueryTests` über `budget.getBudgetHistory` (Pause und Aufhebung in zwei Monaten mit Target-Änderung einer anderen Kategorie und Bewegungen gemischt, Monat und Kategorie je Eintrag; Übernahme 50 €, dann `TargetSet` 60 € und `TargetRemoved` ergeben genau zwei Änderungen, `getCategoryHistory` zeigt dieselben; Altbestand mit `SentToYnab`, Schritt 1 geschrieben und Schritt 2 gescheitert), `handlerTests` mit Port als Funktionswerten (Ausnahme je Quelle; ein unlesbares `TargetPaused` bzw. `TargetSet` über `loadAll*With` auf isolierter DB ergibt den Text der Quelle; Gleichstand Bewegung vor Pause vor Änderung).
- `src/Tests/BudgetGeneralHistoryClientTests.fs`: die Fixtures sind auf `HistoryEntry` umgestellt, mit einer Pause und einer Target-Änderung. Neu: "Der Filter Targets zeigt nur Pausen und Target-Änderungen, Alle alles gemischt, Zuweisen keine Target-Zeile" und "Wiederholen mit dem Filter Targets behält ihn, erneutes Betreten setzt auf Alle zurück". Leertexte und Chip-Reihenfolge sind angepasst. Der Test zur alten Generation läuft unverändert auf `Rows`.
- `src/Tests/BudgetEventMigrationTests.fs`: die beiden neuen Leser lassen Ereignisse eines fremden Kontexts und unbekannte Typen aus. `BudgetLeadTests.fs` nimmt die Bewegungen aus der neuen Antwort.
- Die Tests zum Tab Historie (`TargetChangeTests`, `TargetPauseTests`, `BudgetHistoryTests`) sind ohne Änderung grün. Die Endpoint-Negativzeile `BudgetApi.getBudgetHistory` ist grün.

Gegenprobe: drei Mutanten, danach zurückgesetzt. Diese Liste gilt nur für sie.
- `liveHistoryPort.LoadAllPauseEvents` liefert `Ok []`: der Abfrage-Test "Pause und Aufhebung ..." wird rot.
- Rang der Pause in `generalHistoryRows` hinter der Target-Änderung: der Domain-Test zum Mischen wird rot.
- `shownRows TargetChanges` zeigt alles: beide neuen Client-Tests werden rot.

Screenshots mit gemockten Daten (Skript `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/r5h2n/shot.mjs`, Frontend mit `page.route`, keine Datenbank; das Skript blendet Toasts der nicht gemockten Endpoints aus). `npm run screenshot:route` habe ich bewusst nicht genutzt, weil es eine Kopie der Entwicklungsdatenbank liest:
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/r5h2n/general-history-phone.png` (390 px)
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/r5h2n/general-history-pc.png` (1280 px)
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/r5h2n/general-history-phone-filter-targets.png`
- `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/01e5b14d-920c-4666-a681-f51df6913f4b/scratchpad/r5h2n/general-history-pc-filter-targets.png`

Befund: Die lange Änderung "Rhythmus wöchentlich, Wochentag Montag, Target 50,00 € → 60,00 €, Datum 01.12.2026, Urlaub und Reisen" bricht am Handy auf drei Zeilen um, die Kategorie steht ganz da. Laut DOM-Messung ist bei 390 px und am PC kein Text abgeschnitten. Keine Target-Zeile hat einen Betrag. Ein Badge trägt nur der Altbestand ("teilweise geschrieben", daneben der Grund). Am PC steht der Chip "Targets" an fünfter Stelle. Am Handy kürzt der Grund neben dem Badge auf "Rate…", so wie bei den Durchläufen bisher (`historyFailureView`).

README-Delta mit `npm run check:readme-delta -- --bc budgeting` geprüft, Exit 0. `./verify.sh`: `VERIFY: PASS` (dotnet 4305 grün, node 316 grün). Ein früherer Lauf war rot, weil zwei meiner `verify.sh`-Läufe parallel im selben Worktree liefen (`BadImageFormatException` beim Laden der Tests.dll). Den alten Lauf habe ich beendet, der saubere Einzellauf ist grün.

Der Kann-Punkt steht in budgeting-h9c3w.
