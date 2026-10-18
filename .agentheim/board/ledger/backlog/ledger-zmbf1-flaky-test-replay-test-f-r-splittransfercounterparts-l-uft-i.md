---
id: ledger-zmbf1
title: "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"
status: backlog
type: bug
context: ledger
created: 2026-10-18
depends_on: []
blocks: []
tags: [tests, flaky, persistence]
related_adrs: [0013, 0030, 0046]
related_research: []
prior_art: [ledger-h4nxz]
---

## Why

Nach dem Work-Lauf vom 2026-10-18 schlägt auf `feature/kassenbuch` gelegentlich ein Test fehl
(in drei Läufen einer, sonst 1148 grün):

`Ledger Domain (apply).Replay reproduziert SplitTransferCounterparts wie die Zustandstabellen (ADR 0013)`
mit `SQLite Error 1: 'no such table: ledger_entries'`.

Ursache (Conductor-Diagnose): `ledger-h4nxz` hat diesen Test in `src/Tests/LedgerTests.fs`
in die Liste `ledgerDomainTests` ("Ledger Domain (apply)") gestellt. Diese Liste ist für die
reine Faltung gedacht: nicht `testSequenced` und ohne den `testCase`-Wrapper, der
`initializeLedgerSchema ()` aufruft (ADR 0030, infra-sdad8). Der Test benutzt aber
`appendAndApply`, `loadAllEntries` und `replayAll` gegen die In-Memory-Datenbank. Er hängt
damit von der Ausführungsreihenfolge ab und kann parallel zu Tests laufen, die das Schema
neu aufsetzen. Der Verifier von `ledger-h4nxz` sah eine grüne Suite und hat das nicht erkannt.

## What

Den Test aus `ledgerDomainTests` in `ledgerPersistenceTests` ("Ledger Persistence
(appendAndApply)", `testSequenced`, mit Schema-Initialisierung) verschieben. Inhalt und
Assertions bleiben gleich. Dabei prüfen, ob `ledger-h4nxz` oder `ledger-saqsk` weitere
datenbankgestützte Tests in reine Listen gestellt haben (z. B. in `LedgerTests.fs`,
`LedgerMirrorTests.fs`, `LedgerWithoutCategoryTests.fs`).

## Acceptance criteria

- [ ] Der Test steht in einer `testSequenced`-Liste, deren Testfälle `initializeLedgerSchema ()` aufrufen.
- [ ] Kein Testfall in einer nicht sequenzierten Liste greift auf die Datenbank zu (Durchsicht der vom Lauf 2026-10-18 neu angelegten Tests).
- [ ] `dotnet test` ist in fünf aufeinanderfolgenden Läufen grün.

## Notes

Gefunden beim Abschluss-Lauf der Suite auf main am 2026-10-18 22:19 (trx im Scratchpad der Sitzung).
Kein Produktionscode betroffen, nur die Testanordnung.
