---
id: ledger-mshd8
title: "Client lädt die Sync-Session nach gescheitertem YNAB-Import nicht neu"
status: done
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, client, syncflow]
related_adrs: [0021]
related_research: []
prior_art: [ledger-qvt84]
---

## Why

Seit `ledger-qvt84` Iteration 3 kann `importToYnab` bei einem Teilfehlschlag
(`LedgerWriteFailed` für einzelne Buchungen im Batch) `Error` zurückgeben, obwohl andere
Buchungen im selben Batch bereits erfolgreich importiert und in der Datenbank gespeichert
wurden (`src/Server/Api.fs:1297-1301`, Kommentar "Persisted regardless of a ledger write
failure"). `src/Client/Components/SyncFlow/State.fs`s `ImportCompleted (Error err)`
(Zeile 727 bis 728) lädt die Session nicht neu, daher zeigt der Client diese erfolgreich
importierten Buchungen und gegebenenfalls neue Duplikat-Ids weiterhin mit ihrem alten
Stand an, bis Roman manuell neu lädt.

## What

`src/Client/Components/SyncFlow/State.fs`, `ImportCompleted (Error err)` (aktuell nur
`Cmd.none`, Zeile 728): zusätzlich `Cmd.ofMsg LoadCurrentSession` dispatchen, analog zum
Erfolgspfad (`ImportCompleted (Ok result)`, Zeile 725) und zu `ForceImportCompleted`s
Erfolgsfall (Zeile 744, `Cmd.batch [Cmd.ofMsg LoadCurrentSession; Cmd.ofMsg
LoadTransactions]`).

## Acceptance criteria

- [ ] Test gegen die `update`-Funktion (`src/Client/Components/SyncFlow/State.fs:113`,
      `update: Msg -> Model -> Model * Cmd<Msg> * ExternalMsg`, direkt aufrufbar ohne
      Browser), rot vor dem Fix: `ImportCompleted (Error ...)` liefert ein `Cmd`, das
      beim Ausführen die Nachricht `LoadCurrentSession` dispatcht.
- [ ] `dotnet build` und `npm run build` grün.

## Notes

Kleiner, eigenständiger Fix, keine fachliche Abhängigkeit zu `ledger-jkb0f` oder
`ledger-na3rq`, auch wenn die Ursache serverseitig aus `ledger-qvt84` stammt.

Für `SyncFlow`s `update`-Funktion gibt es noch keinen Test (nur
`src/Tests/SyncFlowViewTests.fs` für die Ansicht). Für das Kassenbuch prüfen
`src/Tests/LedgerTests.fs` und `LedgerPageTests.fs` bereits `Cmd`-Inhalte unter .NET
(ADR 0020: Reduzierer testbar); dasselbe Muster hier anwenden, also die von `Cmd`
gelieferten Funktionen mit einem erfassenden `dispatch` ausführen und die Nachrichten
sammeln.

## Outcome

`src/Client/Components/SyncFlow/State.fs`s `ImportCompleted (Error err)` branch (was
`model, Cmd.none, ShowToast (...)`) now dispatches `Cmd.ofMsg LoadCurrentSession`, matching
the already-existing pattern in `ImportCompleted (Ok result)` and `ForceImportCompleted (Ok
count)`. Since `ledger-qvt84` iteration 3, `Server.Api.importToYnab` can return `Error` for a
partial batch failure (`LedgerWriteFailed` per transaction) while other transactions in the
same batch were already written to the ledger ("Persisted regardless of a ledger write
failure"). Without reloading, the client kept showing those already-imported transactions
and any newly-discovered duplicate ids at their stale pre-import state until a manual reload.

New test file `src/Tests/SyncFlowStateTests.fs` (registered in `src/Tests/Tests.fsproj`
right after `SyncFlowViewTests.fs`), following the `dispatched`-collector pattern already
used in `LedgerPageTests.fs` / `LedgerTests.fs` (ADR 0020: reducers testable without
touching the Fable.Remoting proxy). Calling `Components.SyncFlow.State.init()` /
`Components.SyncFlow.State.update` directly under .NET is safe here: every `Api.*` call in
`State.fs` lives inside a function body (`init`, `update`), there is no top-level
non-function `let` that eagerly touches `Client.Api`, so referencing the module does not
trigger the Fable-only `Remoting.buildProxy` type initializer. Two tests: one reproducing
the bug (observed red before the fix: `ImportCompleted (Error ...)` produced no
`LoadCurrentSession` dispatch), one guarding the pre-existing success-path behavior
(`ImportCompleted (Ok ...)` must keep reloading, unaffected by this change).

Diary note (not written to `diary/development.md` per this run's instructions, to avoid
merge conflicts with parallel `ledger-jkb0f`/`ledger-na3rq` workers):
- What: fixed SyncFlow not reloading the session after a partial YNAB-import error.
- Files: `src/Client/Components/SyncFlow/State.fs` (1 branch, +5/-1 lines),
  `src/Tests/SyncFlowStateTests.fs` (new, 2 tests), `src/Tests/Tests.fsproj` (+1 line).
- Rationale: ADR 0021's "no rollback on push failure" means a batch `Error` can still carry
  successfully-written ledger entries; the client must re-fetch to reflect them.
- Tests: 974 passed, 7 skipped (pre-existing, `.env`-gated integration tests), 0 failed
  (`dotnet test src/Tests/Tests.fsproj`). `dotnet build` and `npm run build` both green.
