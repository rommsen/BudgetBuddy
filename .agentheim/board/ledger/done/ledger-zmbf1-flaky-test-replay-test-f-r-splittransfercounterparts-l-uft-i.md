---
id: ledger-zmbf1
title: "Flaky Test: Replay-Test für SplitTransferCounterparts läuft in der reinen Domain-Liste ohne Schema-Initialisierung"
status: done
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

## Outcome

Der Test "Replay reproduziert SplitTransferCounterparts wie die Zustandstabellen (ADR 0013)" wurde unverändert aus `ledgerDomainTests` an das Ende von `ledgerPersistenceTests` (testSequenced, Schema-Init pro Testfall) in `src/Tests/LedgerTests.fs` verschoben. Kein Produktivcode geändert.

Audit der Tests vom 2026-10-18 (Dateien aus `git log --since=2026-10-18 -- src/Tests`): LedgerTests, LedgerMirrorTests, LedgerWithoutCategoryTests, LedgerPageTests, LedgerViewTests, AuswahlTests, RulesListTests, SyncFlowViewTests, PushOutcomeClassificationTests, MoneyFormatTests, RuhigeListenTests, TextGlowTokenTests. Jede `testList` wurde auf DB-Zugriff (appendAndApply, loadEntry, loadAllEntries, replayAll, Persistence.*, SqliteConnection, mirrorYnabBudget) geprüft. Befund: Keine weitere 10-18-Testliste greift in einer nicht sequenzierten Liste auf die DB zu. LedgerWithoutCategoryTests (domainTests, clientTests), LedgerPageTests-Reducer/Read-Model und LedgerViewTests sind rein; deren DB-Tests liegen in testSequenced-Listen. Einziger Randfund: `runMirrorInBackgroundTests` in LedgerMirrorTests.fs (seit 2026-09-13, ledger-h4p6c) ist eine nicht sequenzierte `testList`, schreibt/liest aber über `loadLastMirrorRun`; sie hat Schema-Init pro Testfall, ist also nicht vom Typ dieses Flakes, und wurde nicht angefasst (außerhalb des Auftrags). Optional als eigener Chore: in `testSequenced` einwickeln.

Läufe: `dotnet build` einmal (0 Fehler, 1 Warnung = alte FS0025 in Ledger/State.fs, keine neue), dann `dotnet test --no-build` fünfmal: je 0 Fehler, 1279 erfolgreich, 7 übersprungen (Integrationstests ohne .env), 1286 gesamt. `npm run build`, `guard:classes`, `test:scripts` nicht ausgeführt: eine Testdatei-Verschiebung berührt sie nicht.
