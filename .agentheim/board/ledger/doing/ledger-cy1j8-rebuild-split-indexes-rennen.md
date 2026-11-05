---
id: ledger-cy1j8
title: "Bug: rebuildSplitIndexes löscht und füllt die Split-Indextabellen ohne Transaktion, parallele initializeLedgerSchema-Aufrufe werfen einen UNIQUE-Fehler"
status: doing
type: bug
context: ledger
created: 2026-11-05
completed:
depends_on: []
blocks: []
tags: [ledger, persistence, tests, race]
related_adrs: [0038]
related_research: []
prior_art: []
---

## Why
In budgeting-p2zr6 war der erste `./verify.sh`-Lauf rot: ein UNIQUE-Fehler aus `rebuildSplitIndexes`, als parallele Expecto-Tests `initializeLedgerSchema` auf der geteilten In-Memory-DB zur selben Zeit aufriefen. Der zweite Lauf war grün, laut QA-Review vom 2026-11-05 Glück. Das Rennen gab es schon vorher und kann jeden Gate-Lauf zufällig rot machen. Roman hat die Umsetzung am 2026-11-05 freigegeben.

## What
`src/Server/Ledger/Persistence.fs:767-808`: `rebuildSplitIndexes` führt `DELETE FROM ledger_entry_split_categories`, `DELETE FROM ledger_entry_split_transfers` und die INSERTs ohne Transaktion aus. Zwei gleichzeitige Aufrufe (oder ein Aufruf neben einem parallelen `appendAndApply`, das Indexzeilen schreibt) verschränken sich, dann trifft ein INSERT eine schon vorhandene Zeile.

Fix: Löschen und Neuaufbau in einer Transaktion auf der übergebenen Connection, so dass ein zweiter Aufruf erst nach dem ersten läuft oder dessen Zwischenstand nicht sieht. Alternativ (wenn die Transaktion allein nicht reicht, etwa wegen SQLite-Sperrverhalten bei `:memory:` mit Shared Cache) eine einmalige Initialisierung je Prozess. Der Worker entscheidet nach dem roten Test und begründet es im Outcome.

## Acceptance criteria
- [ ] Regressionstest (Doktrin: Bugfix nur mit Test, der vor dem Fix rot ist): mehrere parallele Aufrufe von `initializeLedgerSchema` bzw. `rebuildSplitIndexes` auf der geteilten Test-DB mit vorhandenen Split-Buchungen; vor dem Fix reproduzierbar rot (im Outcome mit Zahl der Läufe belegt, etwa 20 von 20 oder ein deterministischer Aufbau über zwei Connections), nach dem Fix grün. Ist ein deterministisch roter Test nicht erreichbar, den Weg und die Rate im Outcome nennen statt einen wackligen Test einzuchecken.
- [ ] Nach dem Fix stimmen die Indextabellen nach parallelen Aufrufen mit einem einzelnen Aufruf überein (gleiche Zeilen).
- [ ] `rebuildSplitIndexes` bleibt idempotent und liefert weiter die Zahl der geschriebenen Indexzeilen.
- [ ] Kein Verhalten beim Serverstart ändert sich (`Program.fs` ruft `initializeLedgerSchema` einmal).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Die Umgehung in `src/Tests/TargetCheckQueryTests.fs` (Handler-Tests initialisieren das Kassenbuch-Schema nicht) darf bleiben.

Läuft parallel zu budgeting-gv74q und budgeting-p8nd0.
