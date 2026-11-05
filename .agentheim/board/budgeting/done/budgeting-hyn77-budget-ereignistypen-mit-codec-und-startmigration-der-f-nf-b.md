---
id: budgeting-hyn77
title: "Budget-Ereignistypen mit Codec und Startmigration der fünf Budget-Tabellen nach events, Schreibwege und Leser umgestellt, Verhalten unverändert"
status: done
type: feature
context: budgeting
created: 2026-11-05
completed:
depends_on: [ledger-k9tm2]
blocks: [budgeting-qm5x9, budgeting-dgrp7, budgeting-eh8xe]
tags: [event-sourcing, migration, fuehrung]
related_adrs: [0013, 0065, 0069, 0079, 0080, 0083, 0084]
related_research: []
prior_art: [budgeting-h2zfn, budgeting-afd8h, budgeting-c5w2n, budgeting-t6p8z, budgeting-k8wq3]
---

## Why
ADR 0084 legt die Budget-Ereignisse in die gemeinsame Tabelle `events` (ADR 0083). Bevor BB im
Budget führt, müssen die heutigen Aufzeichnungen dort liegen, mit englischen Typnamen, und alle
Schreibwege und Leser müssen aus `events` arbeiten. YNAB führt in diesem Schritt noch; für Client
und YNAB ändert sich nichts.

## What

### 1. Ereignistypen und Codec (`src/Server/Budgeting/`)
Nur die Typen, die dieser Schritt schreibt oder migriert. `TargetSet`, `TargetRemoved`,
`AssignmentChangeObservedInYnab`, `BudgetTakenOverFromYnab` und `TargetTakenOverFromYnab` kommen
mit budgeting-dgrp7 und budgeting-eh8xe, nicht hier (kein Typ ohne Schreiber).

| Typ | Stream | Actor | Payload (camelCase, `schema_version` 1) |
|---|---|---|---|
| `AssignmentChangedInBb` | month | `UserInBb` | `month`, `line` |
| `MoneyMoved` | month | `UserInBb` | `month`, `from` (`line`), `to` (`line`), `amount` |
| `TargetsApplied` | month | `UserInBb` | `month`, `lines`, `skippedPaused` |
| `TargetPaused`, `TargetPauseLifted` | month | `UserInBb` | `month`, `categoryId`, `categoryName` |
| `AssignmentWrittenBackToYnab` | month | `YnabMirror` | `decisionEventId`, `categoryId` |
| `AssignmentWriteBackFailed` | month | `YnabMirror` | `decisionEventId`, `categoryId`, `reason` |
| `AssignmentWriteBackSupersededInYnab` | month | `YnabMirror` | `decisionEventId`, `categoryId` |
| `AssignmentWriteBackWithdrawn` | month | `UserInBb` | `decisionEventId`, `categoryId` |
| `YnabGoalChangedFromBb` (Altbestand-Familie) | target | `UserInBb` | `month`, `categoryName`, `changeKind`, Teilwerte wie heute |
| `YnabGoalStepWrittenToYnab`, `YnabGoalStepWriteFailed` (`reason`), `YnabGoalStepSupersededInYnab` | target | `YnabMirror` | `decisionEventId`, `step` |

- Eine `line` ist `lineId`, `categoryId`, `categoryName`, `previousAssigned`, `assigned` (Beträge
  in Milliunits). `lineId` ist bei migrierten Zeilen die alte `budget_movements.id`, bei neuen
  eine frische Guid; sie füllt `HistoryMovement.MovementId`. `categoryName` ist der Name zum
  Zeitpunkt der Entscheidung (die Historie zeigt ihn heute so).
- Monats-Id `YYYY-MM` (ADR 0084 Punkt 1), gebaut von **einer** Funktion aus dem gespeicherten
  `YYYY-MM-01`; Stream-Namen `budgeting:month:<YYYY-MM>` und `budgeting:target:<categoryId>`
  (Format D) ebenfalls an je einer Stelle, über `EventStore.streamIdOf`.
- **Gruppen-Id = `event_id` des Entscheidungsereignisses** (ADR 0084 Punkt 2). Bei migrierten
  Entscheidungen ist das die alte `application_id`, bei Pausen die alte Pause-Id, bei
  Target-Änderungen die alte Change-Id. So bleiben `ApplicationId`, `EventId` und die Verweise im
  Client unverändert, und "erneut senden" findet alte wie neue Anwendungen über dieselbe Id.
  Neue Entscheidungen nehmen die `event_id`, die `EventStore.appendOn` zurückgibt.
- Ergebnisse von Target-Änderungen werden eigene Ereignisse im Target-Stream (drei Typen oben),
  nicht in das Änderungsereignis gefaltet. Die Familie `YnabGoal*` schreibt der Zwei-Schritt-Weg
  bis budgeting-eh8xe weiter.

### 2. Startmigration (`migrateBudgetEventsOn conn`)
- Läuft in `initializeBudgetingSchema` nach dem Anlegen von `events` (über
  `EventStore.createSchemaOn` auf derselben Connection) und nur, wenn mindestens eine der fünf
  Tabellen existiert. Eine Transaktion; danach sind alle fünf Tabellen samt Indizes und Triggern
  entfernt. `createBudgetingSchema…` legt sie nicht mehr an.
- Kopie per SQL-Parameter wie bei ledger-k9tm2: `occurred_at` geht als **unveränderter Text**
  nach `events`, ohne `EventStore.validate`; das Payload baut der F#-Code aus den Spalten.
  Ein Execute mit Parameter-Array, kein INSERT je Zeile in eigener Abfrage.
- Abbildung nach ADR 0084 Punkt 4: Bewegungen einer `application_id` werden **ein**
  Entscheidungsereignis nach Herkunft (`Targets anwenden` → `TargetsApplied` mit
  `skippedPaused = []`, `Verschieben` → `MoneyMoved` mit Quelle = Zeile, deren Zugewiesen sinkt,
  `Zuweisen` → `AssignmentChangedInBb`). Jede Ergebniszeile wird ein Ergebnis-Ereignis
  (`written`, `failed`, `changed-in-ynab`). Pausen nach `kind` (`Pausiert`, `Aufgehoben`,
  Altwert `Fortgesetzt` → `TargetPauseLifted`). `target_changes` ohne `step` gilt als Schritt 1.
- Altbestand `changed-in-ynab`: Ein ungesendet geschlossenes Ziel einer Verschiebung ist im
  Altbestand nicht von einem echten "in YNAB geändert" zu unterscheiden. Alle migrierten
  `changed-in-ynab` werden `AssignmentWriteBackSupersededInYnab`. Neue ungesendete Schließungen
  in `retryFailed` (die Quelle über `sourceBlocked`, `CommandHandlers.fs:597`, und das Ziel nach
  verlorener Quelle, `:632`) schreiben `AssignmentWriteBackWithdrawn`; ein echtes `YnabWins`
  schreibt `AssignmentWriteBackSupersededInYnab`. Leser bilden beide auf das heutige
  `ChangedInYnab` ab: die Historie zeigt wie heute "in YNAB geändert", und beide schließen die
  Zeile für "erneut senden".
- Reihenfolge je Stream: `occurred_at`, bei Gleichstand Entscheidung vor Ergebnis, dann `rowid`;
  `stream_version` je Stream lückenlos ab 1; in dieser Reihenfolge eingefügt, sodass `position`
  ihr folgt und hinter dem Kassenbuch liegt.
- Abbruch (Transaktion zurück, BB startet nicht, Meldung nennt Tabelle, Zeile und Wert):
  unbekannte Herkunft, Art, Change-Art oder Status; eine Id, die keine Guid im Format D ist; ein
  Ergebnis ohne seine Bewegung oder Änderung; eine Anwendung über mehrere Monate oder Herkünfte;
  eine `Verschieben`-Gruppe, die nicht genau zwei Zeilen hat.
- Eine Probe-Funktion (`probeBudgetEventMigrationOn`) wie bei ledger-k9tm2 meldet nur Zahlen und
  Wahrheitswerte: Zeilen vorher je Tabelle, Ereignisse nachher je Typ, Versionen lückenlos, Leser
  vorher und nachher gleich, alte Tabellen entfernt. Opt-in-Test mit
  `BUDGET_MIGRATION_PROBE_DB` auf einer Temp-Kopie, Liste "(requires .env)".

### 3. Schreibwege und Leser auf `events`
- Schreiben über `EventStore.appendOn`/`append` mit `context = "budgeting"`, unter den
  bestehenden Sperren (`MonthGuard`, `TargetGuard`). Neue Ereignisse brauchen einen UTC-Zeitpunkt
  (`EventStore.validate`); die Uhr kommt wie heute aus `budgetApiWith` (`DateTime.UtcNow`).
- Schreibwege: Zuweisen, Verschieben, Targets anwenden, erneut senden, Pausieren, Aufheben,
  Target ändern samt Schritt-Ergebnissen.
- Leser: Historie je Kategorie, allgemeine Historie (inklusive `Counterpart` einer Verschiebung),
  "Zuletzt verwendet", Pausen je Monat, Pause-Ereignisse, Target-Änderungen je Kategorie, letzte
  Target-Änderung (ADR 0082 Punkt 7), Zustand einer Anwendung für "erneut senden" und ihr Bericht.
  Jeder filtert auf `context = 'budgeting'` und nach Bedarf auf `event_type` oder `stream_id`;
  sortiert wird fachlich (`occurred_at`, `stream_version`), nie nach `position`.
- `underfunded_comparisons` bleibt Zustand und unberührt.

## Acceptance criteria
- [ ] Je Ereignistyp ein Round-Trip-Test Encoder/Decoder; ein unbekannter `event_type` im Kontext
      `budgeting` wird beim Laden als Fehler gemeldet, nicht übersprungen.
- [ ] GIVEN eine Test-Datenbank mit Zeilen aller fünf Tabellen (jede Herkunft, jeder Status, je
      Pause-Art samt Altwert `Fortgesetzt`, eine `target_changes`-Zeile ohne `step`, eine
      Entscheidung und ihr Ergebnis mit gleichem `occurred_at`) und einem Kassenbuch-Ereignis in
      `events`, WHEN der Startschritt läuft, THEN stehen die Ereignisse in der Reihenfolge aus
      ADR 0084 Punkt 4 in `events`, `stream_version` ist je Stream lückenlos, `position` liegt
      hinter dem Kassenbuch-Ereignis, `occurred_at` ist je Ereignis der unveränderte Text, und
      die fünf Tabellen existieren nicht mehr.
- [ ] Vorher-nachher-Vergleich im Test: jeder Leser aus Abschnitt 3 liefert vor und nach der
      Migration dieselben Werte, einschließlich `ApplicationId`, `MovementId`, `EventId`,
      `Counterpart` und Kategorienamen.
- [ ] Replay: die Leser liefern aus migrierten Ereignissen dasselbe wie aus neu über die
      Schreibwege angehängten Ereignissen gleichen Inhalts.
- [ ] "Erneut senden" findet nach der Migration eine migrierte Anwendung über ihre alte
      `application_id` und eine neue über die `event_id` ihres Entscheidungsereignisses.
- [ ] Für jede Abbruchregel aus Abschnitt 2 ein Test: Meldung nennt Tabelle und Wert, `events` und
      alle fünf Tabellen sind unverändert. Ein weiterer Test lässt das Schreiben erst bei einer
      späteren Zeile scheitern (`UNIQUE` auf `stream_id, stream_version`) und zeigt dasselbe;
      ohne Transaktion wird er rot.
- [ ] Ein zweiter Start nach erfolgreicher Migration tut nichts und legt keine Tabelle neu an.
- [ ] Ein Test mit Kassenbuch-Ereignissen und Ereignissen eines fremden Kontexts in `events` zeigt,
      dass kein Budget-Leser sie liest.
- [ ] Bestehende Budget-Tests bleiben grün; `src/Shared` ändert sich nicht. Muss es sich doch
      ändern, werden die Fixtures in `scripts/*.test.mjs` (etwa `scripts/budget-history.test.mjs`)
      im selben Schritt nachgezogen.
- [ ] Kein Code und kein Test in `src/` nennt die fünf Tabellennamen außer der Migration, der
      Probe und ihren Tests.
- [ ] Probe gegen eine Kopie der Produktions-Datenbank (Conductor, nach PASS, vor dem Deploy):
      alle Wahrheitswerte true, im Outcome ohne Zahlen oder Daten aus dem Budget vermerkt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- Quelle: ADR 0084 Punkte 1, 2 und 4; Umschlag und Anhängen aus ADR 0083 und
  infrastructure-e4vq7; Vorbild für Migration, Probe und Tests ist ledger-k9tm2 (Outcome lesen,
  `src/Server/Ledger/Persistence.fs` `migrateLedgerEventsOn`, `probeLedgerEventMigrationOn`,
  `src/Tests/LedgerEventMigrationTests.fs`). `EventStore.StoredEventRow` und `toRecordedEvent`
  sind öffentlich.
- Stand der Produktion (Refinement 2026-11-05, Kopie danach gelöscht, keine Zahlen): Befüllt sind
  nur die beiden Bewegungstabellen, mit allen drei Herkünften; Ergebnisse nur `written`; keine
  Pausen, keine Target-Änderungen. Zeitpunkte enden auf `Z`. Ergebnisse tragen oft denselben
  Zeitpunkt wie ihre Entscheidung, die Gleichstandsregel greift also in Produktion. Monate stehen
  als `YYYY-MM-01`.
- `target_changes` ohne `step`: `src/Server/Budgeting/Persistence.fs:569-570`; Spalten-Migration
  `migrateTargetChangeColumns` entfällt mit den Tabellen.
- Die alten Tabellen haben append-only-Trigger; Tests, die sie heute direkt beschreiben, bauen den
  Altbestand für die Migrationstests gezielt auf (Vorbild `toLegacyLog` in ledger-k9tm2).
- Der Zustand (Soll, Regeln, Pausen) beginnt erst mit der Übernahme (budgeting-dgrp7,
  budgeting-eh8xe); migrierte Ereignisse sind Historie.
- Nach dem Deploy greift ein Rollback nur über das Backup (restic, nächtlich).

## Outcome

Die Budget-Aufzeichnungen liegen in `events` (Kontext `budgeting`, ADR 0083/0084). `./verify.sh` im Worktree: `VERIFY: PASS` (dotnet 3976 grün, 13 opt-in übersprungen, nur Listen "(requires .env)"; node 310 grün).

**Ereignistypen und Codec** (`src/Server/Budgeting/Events.fs`): die 13 Typen aus der Task-Tabelle als `BudgetEvent` (qualifiziert), Thoth-Codec in camelCase, Milliunits als JSON-Zahl, Schema-Version 1, Actor je Typ. Eine Funktion `monthIdOf` baut `YYYY-MM` aus `YYYY-MM-01`, `streamIdOf` baut `budgeting:month:<YYYY-MM>` und `budgeting:target:<categoryId>` über `EventStore.streamIdOf`. `WriteBackOutcome` (WrittenBack, WriteBackFailed, SupersededInYnab, Withdrawn), Leser bilden Superseded und Withdrawn auf `ChangedInYnab` ab.

**Startmigration** (`src/Server/Budgeting/EventMigration.fs`, `migrateBudgetEventsOn`): läuft in `initializeBudgetingSchema` nach `EventStore.createSchemaOn` auf derselben Connection, nur wenn eine der fünf Tabellen existiert; eine Transaktion, ein Execute mit Parameter-Array, `occurred_at` als unveränderter Text, danach `DROP TABLE` der fünf (Indizes und Trigger gehen mit). Gruppen-Id = alte `application_id`, `lineId` = alte Bewegungs-Id, Pause- und Change-Id = `event_id`. Reihenfolge je Stream: `occurred_at` (ordinal als Text), Entscheidung vor Ergebnis, dann eine feste Tabellenfolge, dann rowid. Abbruchregeln wie im Task, dazu fünf zusätzliche, die die Abbildung sonst verfälschen würden: Zeilen einer Anwendung mit verschiedenem `occurred_at`, eine Kategorie zweimal in einer Anwendung, Zuweisen mit mehr als einer Zeile, Verschieben ohne genau eine sinkende Quelle, Monat nicht im Format `YYYY-MM-01`. `initializeBudgetingSchema` legt die Tabellen nicht mehr an, `migrateTargetChangeColumns` ist entfallen.

**Schreibwege und Leser** (`src/Server/Budgeting/Persistence.fs`, `CommandHandlers.fs`): `recordDecision`, `recordWriteBack`, `recordPause`, `recordTargetChange`, `recordTargetChangeStepResult` (je mit `...On conn`) hängen über `EventStore.appendOn` an; die Gruppen-Id neuer Entscheidungen ist die zurückgegebene `event_id`. `retryFailed` schreibt bei der Quelle vor verlorenem Ziel und beim Ziel nach verlorener Quelle `AssignmentWriteBackWithdrawn`, beim echten `YnabWins` `AssignmentWriteBackSupersededInYnab`. Alle Leser (`queryMovements`, `queryRecentCategories`, `queryPausedCategories`, `queryPauseEvents`, `queryTargetChanges`, `queryLastTargetChanges`, `queryApplication`) lesen über `loadBudgetEventsOn` mit `context = 'budgeting'` und optional `stream_id`, decodieren jedes Ereignis (unbekannter Typ wird `Error`) und sortieren nach `occurred_at`/`stream_version`. `queryRecentCategories` liefert jetzt `Result`. Für Tests mit einem nicht lesbaren Ereignis auf eigener Datenbank kamen `loadPausedCategoriesWith`, `loadPauseEventsWith`, `loadTargetChangesWith`, `loadMovementsWith`, `QueryHandlers.budgetMonthOfWith` und `CommandHandlers.setTargetPauseWith` dazu; die bisherigen Namen sind Einzeiler auf die Datenbank der Anwendung (ein kaputtes Ereignis in der geteilten Testdatenbank träfe sonst jeden Leser über den ganzen Kontext).

**Probe** (`src/Server/Budgeting/EventMigrationProbe.fs`, `probeBudgetEventMigrationOn`): liest jeden Leser vorher mit den früheren SQL-Abfragen, migriert, liest nachher aus `events`; Ergebnis nur Zahlen (Zeilen je Tabelle, Ereignisse je Typ) und Wahrheitswerte (StreamVersionsGapless, OccurredAtUnchanged, HistoryUnchanged, CategoryHistoriesUnchanged, RecentCategoriesUnchanged, PausesUnchanged, LastTargetChangesUnchanged, ApplicationsUnchanged, LegacyTablesGone). Gegen eine Kopie der lokalen Dev-Datenbank lief sie mit allen Wahrheitswerten true.

**Probe gegen die Produktion (Conductor, nach PASS, vor dem Deploy)** auf einer Kopie der Produktions-Datenbank, außerhalb des Repos; der Test kopiert die Datei samt `-wal`/`-shm` selbst ins Temp-Verzeichnis und löscht die Kopie danach:

```
dotnet build src/Tests/Tests.fsproj
BUDGET_MIGRATION_PROBE_DB=/pfad/zur/kopie/budgetbuddy.db USE_MEMORY_DB=true TZ=Europe/Berlin \
  dotnet src/Tests/bin/Debug/net10.0/Tests.dll --filter "Budget-Migration: Probe gegen eine Kopie (requires .env)"
```

Erwartet: alle neun Wahrheitswerte `true`, der Test grün. Ins Outcome nur die Wahrheitswerte, keine Zahlen.

**Tests** (`src/Tests/BudgetEventMigrationTests.fs`, Hilfen in `src/Tests/BudgetEventsFixture.fs`): Round-Trip je Typ, unbekannter Typ/Version/Monat; Migration eines Altbestands mit jeder Herkunft, jedem Status, jeder Pause-Art samt "Fortgesetzt", Ergebnis ohne Schritt und gleichem Zeitpunkt von Entscheidung und Ergebnis (Reihenfolge je Stream, lückenlose Versionen, `position` hinter dem Kassenbuch, Text-Zeitpunkt, Tabellen samt Indizes und Triggern weg); Ids bleiben (ApplicationId, MovementId, EventId, ChangeId, Counterpart, Kategorienamen); Vorher-nachher über die Probe mit Gegenbeweis; Replay migriert gegen neu geschrieben; zweiter Start; der Start legt keine Tabelle an; je Abbruchregel ein Test (Meldung mit Tabelle und Wert, `events` und alle fünf Tabellen unverändert) und der UNIQUE-Test auf einer späteren Zeile; Kassenbuch- und Fremdkontext-Ereignisse werden von keinem Leser gelesen; "erneut senden" findet eine migrierte Anwendung über die alte `application_id` und eine neue über die `event_id`; Wächter, dass außer Migration, Probe und ihren Tests keine Datei in `src/` die Tabellennamen nennt (mit Gegenbeweis); opt-in Probe. Bestehende Budget-Tests schreiben über die neuen Schreibwege; entfallen sind Trigger-Tests auf den alten Tabellen (append-only von `events` prüft `EventStoreTests`) und der Spaltentest von `migrateTargetChangeColumns`; "Fortgesetzt" und "Ergebnis ohne Schritt" prüft jetzt die Migration.

`src/Shared/Budgeting.fs`: nur drei Doc-Kommentare, die die Tabellennamen nannten; kein Typ und kein Feld geändert, die Fixtures in `scripts/*.test.mjs` bleiben gültig.

### Für den Conductor

- ADR 0084 Punkt 4, Nachtrag vorschlagen: (a) zusätzliche Abbruchregeln der Migration: Anwendung mit mehreren Zeitpunkten, Kategorie doppelt in einer Anwendung, Zuweisen mit mehr als einer Zeile, Verschieben ohne eindeutige sinkende Quelle, Monat nicht `YYYY-MM-01`; (b) bei gleichem `occurred_at` und gleichem Rang ordnet eine feste Tabellenfolge vor der rowid; (c) die Rasterprüfung aus Punkt 3 läuft in der Migration nicht, weil migrierte Ereignisse Historie sind und nicht in die Faltung gehen (Punkt 4, "Der Zustand beginnt beim Übernahme-Ereignis").
- ADR 0084 Punkt 2: `applicationId` steht nicht im Payload; die Gruppen-Id migrierter Entscheidungen ist ihre `event_id` (wie im Task festgelegt), alte Verweise bleiben so lesbar.
- Override (C): Reviews vorschlagen: `qa-milestone-reviewer` (Tests geändert), `backend-code-quality` (src/Server).

### Probe gegen die Produktionskopie (Conductor, 2026-11-05 18:08, vor dem Deploy)
Kopie der Produktions-Datenbank im Session-Scratchpad, Opt-in-Test mit `BUDGET_MIGRATION_PROBE_DB`,
Kopie danach gelöscht; die Eingabedatei blieb unverändert (gleiche Prüfsumme vorher und nachher).
StreamVersionsGapless, OccurredAtUnchanged, HistoryUnchanged, CategoryHistoriesUnchanged,
RecentCategoriesUnchanged, PausesUnchanged, LastTargetChangesUnchanged, ApplicationsUnchanged,
LegacyTablesGone: alle true. Keine Abbruchregel schlug an.
