---
id: budgeting-hyn77
title: "Budget-Ereignistypen mit Codec und Startmigration der fünf Budget-Tabellen nach events, Schreibwege und Leser umgestellt, Verhalten unverändert"
status: doing
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
