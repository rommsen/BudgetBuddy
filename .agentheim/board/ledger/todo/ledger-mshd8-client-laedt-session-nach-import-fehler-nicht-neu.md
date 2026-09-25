---
id: ledger-mshd8
title: "Client lädt die Sync-Session nach gescheitertem YNAB-Import nicht neu"
status: todo
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
