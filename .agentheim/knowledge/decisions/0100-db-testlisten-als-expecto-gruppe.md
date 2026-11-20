---
id: 0100
title: DB-Testlisten als eine Expecto-Gruppe, große DB-Aufzählungen auf eigener Datenbank
scope: infrastructure
status: accepted
date: 2026-11-20
supersedes: []
superseded_by: []
related_tasks: [infrastructure-x7p2e, infrastructure-d1dwb, ledger-zmbf1, ledger-kfd5m]
related_adrs: [0030, 0033, 0083]
related_research: []
---

# ADR-0100: DB-Testlisten als eine Expecto-Gruppe, große DB-Aufzählungen auf eigener Datenbank

## Context

Alle Tests teilen eine In-Memory-DB (`Mode=Memory;Cache=Shared`, ADR 0030). Bis November 2026 stand jede DB-Liste
unter `testSequenced`. Expecto 10.2.1 führt solche Fälle erst aus, wenn alle parallelen Fälle fertig sind, und dann
einzeln: rund 930 Fälle mit 34 s, die Hälfte der 65 s von Expecto.

Probeläufe (infrastructure-x7p2e):

- Alle `testSequenced` entfernt: 140 s und 11 rote Fälle. Gleichzeitige Zugriffe sperren sich im Shared Cache je
  Tabelle, Microsoft.Data.Sqlite wiederholt eine gesperrte Anweisung erst nach einer Pause. Dazu lesen mehrere
  Listen über alle Zeilen einer Tabelle (Sortierung, Kontostände, Zähler „Zu prüfen“, `lena_flag_color`).
- Die Listen der Schreib-Sperre in einer eigenen Gruppe neben der DB-Gruppe: zwei von zwei Läufen rot (die
  Sortierung zählte deren Buchungen mit), und nicht schneller, weil die Befehle der DB-Gruppe an der Sperre warteten.
- Die Aufzählung „Jede Folge aus drei Schritten“ (1.539 Folgen, je ein eigener Monat) schrieb rund 1.500
  Monats-Streams in die gemeinsame DB. Budget-Abfragen über alle Monate wurden danach etwa zehnmal langsamer. Nur
  diesen Fall übersprungen: Expecto 37 s statt 65 s.

## Decision

1. Jede Liste auf der gemeinsamen DB steht in `testSequencedGroup TestGroups.InMemoryDb` (`src/Tests/TestGroups.fs`).
   Die Fälle der Gruppe laufen nacheinander, die Gruppe als Ganzes parallel zu allen anderen Tests. Das gilt auch für
   die Listen der Schreib-Sperre.
2. `testSequenced` ohne Gruppe bleibt für Fälle, die wirklich allein laufen müssen (heute `Console.SetOut` in
   `ComdirectHttpTests` und die Modulzähler in `SyncStartOrderTests`, zusammen unter 0,1 s).
3. Eine Aufzählung mit viel Datenbankarbeit läuft auf eigenen Datenbanken (`BudgetEventsFixture.isolatedDb`) und ist
   in Fälle geteilt, die Expecto verteilt. Vorbild: `orderTests` in `src/Tests/BudgetLeadTests.fs`.
4. Über jeder Markierung steht als Kommentar, was die Fälle teilen. `TestSequencingGuardTests` prüft das.

## Consequences

- Expecto rund 29,5 s statt 65 s. Die DB-Gruppe (rund 28 s) ist jetzt die längste Kette. Ein Drittel davon sind
  die festen 300-ms-Fenster von `LedgerWriteGuardProbe.commandWaitsForMirror`.
- Eine neue DB-Liste ohne Gruppe läuft parallel zur Gruppe und bremst sie aus. Das fängt kein Wächter, nur die
  Laufzeit und Punkt 16 der Test-Doktrin.
- Ein Tippfehler im Gruppennamen machte eine zweite Gruppe auf; deshalb steht der Name als Konstante in `TestGroups`.
