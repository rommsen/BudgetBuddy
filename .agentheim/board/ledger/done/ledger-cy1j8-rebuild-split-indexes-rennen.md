---
id: ledger-cy1j8
title: "Bug: rebuildSplitIndexes löscht und füllt die Split-Indextabellen ohne Transaktion, parallele initializeLedgerSchema-Aufrufe werfen einen UNIQUE-Fehler"
status: done
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

## Outcome

`rebuildSplitIndexes` (`src/Server/Ledger/Persistence.fs`) führt das Löschen von `ledger_entry_split_categories` und `ledger_entry_split_transfers`, das Lesen der aufgeteilten Buchungen und alle INSERTs jetzt in einer Transaktion auf der übergebenen Connection aus. Wirft ein Schritt, rollt das Dispose der nicht bestätigten Transaktion alles zurück. Davor sitzt eine prozessweite Sperre (`rebuildSplitIndexesGate`). Signatur, Idempotenz und Rückgabewert (Zahl der geschriebenen Indexzeilen) bleiben gleich. `initializeLedgerSchema` und `Program.fs` sind unverändert; beim Serverstart läuft ein einziger Aufruf, die Sperre ist dort wirkungslos.

**Regressionstest** in `src/Tests/LedgerWithoutCategoryTests.fs` (Liste "Ledger Persistence (Ohne Kategorie, ADR 0048)"): "rebuildSplitIndexes: parallele Aufrufe auf eigenen Connections werfen nicht und hinterlassen denselben Index wie ein einzelner". Er legt sechs Split-Buchungen mit je zwei Kategorien und einem Zielkonto an (eigene Ids 60 bis 65, Konto 6), baut einmal neu auf und merkt sich die eigenen Indexzeilen (12 Kategorie-, 6 Übertragszeilen). Danach laufen 20 Runden mit je 8 parallelen Aufrufen auf jeweils eigener Connection. Er prüft, dass kein Aufruf wirft, jeder mindestens 18 Zeilen meldet und die eigenen Zeilen danach dieselben sind wie nach dem einzelnen Aufruf. Verglichen werden nur die eigenen Zeilen, weil parallele Testlisten auf der geteilten DB weitere Split-Buchungen schreiben.

**Rot vor dem Fix:** 20 von 20 Läufen (`dotnet Tests.dll --filter-test-case "rebuildSplitIndexes: parallele"`), jedes Mal `SQLite Error 19: 'UNIQUE constraint failed: ledger_entry_split_categories.category_id, ledger_entry_split_categories.entry_id'` (einmal dieselbe Meldung für `ledger_entry_split_transfers`). **Grün nach dem Fix:** 20 von 20, zusammen 14 s.

**Warum Transaktion und Sperre:** Die Transaktion allein machte den Test schon grün (die Korrektheit kommt aus ihr, und sie schützt den Neuaufbau auch gegen ein paralleles `appendAndApply` auf anderer Connection). Im Shared-Cache-Modus der Test-DB (`Mode=Memory;Cache=Shared`) bekamen die konkurrierenden Aufrufe aber SQLITE_LOCKED und warteten über die Wiederholungsschleife von Microsoft.Data.Sqlite: ein Lauf des Tests dauerte 13,8 s statt etwa 0,1 s. Die Sperre reiht Neuaufbauten im Prozess vorher ein. Eine einmalige Initialisierung je Prozess habe ich verworfen: die Tests AC9 und "ledger_entry_split_transfers ... rebuildSplitIndexes baut sie neu" rufen den Neuaufbau absichtlich erneut auf.

`./verify.sh` im Worktree: `VERIFY: PASS (dotnet 3857 grün, node 310 grün, dotnet build, npm run build, guard:classes, guard:tokens grün)`.
