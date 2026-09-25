---
id: ledger-na3rq
title: "Import-Push-Klebecode testbar machen; Force-Import schließt Session trotz gescheitertem Kassenbuch-Schreiben ab"
status: todo
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, push, tests, refactor]
related_adrs: [0013, 0021]
related_research: []
prior_art: [ledger-tpbe7, ledger-qvt84, ledger-w6tbq]
---

## Why

`ledger-w6tbq` hat den doppelten Kandidatenbau des Comdirect-Import-Pushs bereits in eine
getestete, reine Funktion (`buildComdirectImportCandidates`) gezogen. Die Schicht danach,
die Outcome-Klassifikation (welche Buchung gilt als importiert, Duplikat oder gescheitert,
und ob die Session abgeschlossen werden darf), liegt weiterhin inline im `IEntityApi`-Record
in `src/Server/Api.fs` und ist unter `dotnet test` nicht erreichbar. Bei der Verfeinerung
von `ledger-tpbe7` ist zusätzlich aufgefallen, dass `forceImportDuplicates` die Session
abschließen kann, obwohl eine Buchung wegen eines gescheiterten Kassenbuch-Schreibens
offen geblieben ist, ein Verstoß gegen ADR 0021 ("Kassenbuch zuerst").

## What

1. Neue reine Funktion (zum Beispiel `classifyPushOutcomes`) mit einem Ergebnis-Record,
   der mindestens `UpdatedTransactions: SyncTransaction list`,
   `DuplicateTransactionIds: TransactionId list`,
   `LedgerWriteFailureSummary: (int * string) option` und `SessionMayComplete: bool`
   trägt. `SessionMayComplete` ist ein Feld dieses Records, keine an der Aufrufstelle
   wiederholte Bedingung, damit niemand die Klassifikation nutzt und die Sperre vergisst.
2. Die Logik aus `src/Server/Api.fs` (normaler Push, Zeile 1213 bis 1288:
   `importIdByTxId`, `duplicateImportIdSet`, `futureDatedSet`, `rejectedTransferTxIdSet`,
   `duplicateTransactionIds`, `ledgerWriteFailureSummary`, das `updatedTransactions`-
   Mapping) wandert in diese Funktion.
3. `importToYnab` und `forceImportDuplicates` (Zeile 1418 bis 1445) rufen dieselbe
   Funktion mit ihren jeweiligen Eingaben auf und lesen nur noch `SessionMayComplete` für
   den `completeSession()`-Aufruf. Das behebt den Force-Import-Bug als Nebenwirkung der
   Vereinheitlichung: heute prüft `forceImportDuplicates` nur `allCategorizedImported`
   (Zeile 1435 bis 1442) ohne `LedgerWriteFailed`-Outcomes zu berücksichtigen.

## Acceptance criteria

- [ ] Neue Testdatei (zum Beispiel `src/Tests/PushOutcomeClassificationTests.fs`) deckt
      die Klassifikationsfunktion ohne HTTP, ohne `SyncSessionManager`, ohne
      Fable.Remoting ab.
- [ ] Regressionstest: ein Ergebnis mit mindestens einem `LedgerWriteFailed`-Outcome
      ergibt `SessionMayComplete = false`, auch wenn `DuplicateTransactionIds` leer ist.
- [ ] Regressionstest (Force-Import-Bug, rot vor dem Fix): eine simulierte
      Force-Import-Klassifikation mit einem `LedgerWriteFailed`-Outcome unter sonst
      vollständig importierten oder kategorisierten Transaktionen ergibt
      `SessionMayComplete = false`.
- [ ] Beide Aufrufstellen (`importToYnab`, `forceImportDuplicates`) in
      `src/Server/Api.fs` nutzen dieselbe Klassifikationsfunktion; die bisherige Logik
      kommt an keiner der beiden Stellen mehr dupliziert vor. Per Diff belegt.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Dieser Task ändert beobachtbares Verhalten (Force-Import bricht jetzt korrekt ab, statt
die Session fälschlich abzuschließen). Als Bugfix behandeln, nicht als reine
Aufräumarbeit, damit die Verifikation den Verhaltensunterschied ernst nimmt und prüft.

`buildComdirectImportCandidates` (`ledger-w6tbq`) bleibt unverändert; dieser Task betrifft
nur die Schicht danach, die Outcome-Klassifikation.

Ein Fable.Remoting-API-Handler ist unter `dotnet test` nicht direkt aufrufbar (siehe
`ledger-w6tbq`-Notes); deshalb die Extraktion in eine reine Funktion außerhalb des
`IEntityApi`-Records.

Der Doc-Kommentar zu `ComdirectPushInput.Payee`/`Memo` (`src/Server/Ledger/Domain.fs:340-343`)
gehört zu `ledger-jkb0f`, nicht hierher. `ledger-jkb0f` fasst `Api.fs` nur an der Stelle an,
die `ComdirectPushInput` befüllt; dieser Task die Outcome-Klassifikation danach. Der
Conductor lässt beide wegen der gemeinsamen Datei nacheinander laufen.
