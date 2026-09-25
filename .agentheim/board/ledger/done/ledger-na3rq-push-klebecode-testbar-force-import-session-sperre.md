---
id: ledger-na3rq
title: "Import-Push-Klebecode testbar machen; Force-Import schließt Session trotz gescheitertem Kassenbuch-Schreiben ab"
status: done
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, push, tests, refactor]
related_adrs: [0013, 0021]
related_research: []
prior_art: [ledger-qvt84, ledger-w6tbq]
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

## Verifier note (iteration 1)

REASONS:
- Behavior regression in `forceImportDuplicates` (src/Server/Api.fs, around line 1325 to 1334 of the new file). The old completion gate `allCategorizedImported` looked at ALL session transactions after the update (`SyncSessionManager.getTransactions()`). The new gate `classification.SessionMayComplete` only looks at the forced batch (`transactionsToForce`). Nothing in the diff replaces the session-wide check. Concrete failing case: a normal `importToYnab` returns both duplicates and a `LedgerWriteFailed` tx, so the session stays open and the failed tx stays AutoCategorized/ManualCategorized/NeedsAttention. The client then force-imports only `model.DuplicateTransactionIds` (src/Client/Components/SyncFlow/State.fs:730-737). The new code sees no failure in that batch, so SessionMayComplete = true and it calls `completeSession()` while the earlier ledger-failed tx is still open. The old code would not have completed the session there. The task's Why names exactly this invariant (the session must not complete while such a Buchung is open), and the diff breaks it on a path that used to work. The same widening applies to any categorized tx outside the forced set, and to forced txs that stay unchanged (future-dated, or WrittenNotLinked with an unparseable response). The old code blocked completion in those cases because the tx stayed categorized. The new code completes. No test covers the cross-batch case. The Force-Import regression test only puts txs from the same batch into the classifier, so it cannot catch this.
- Smaller behavior changes on the force path, not mentioned or tested for force-import: rejected transfer lines are now marked `RejectedByYnab (UnknownRejection ...)`, and duplicates reported by YNAB are now marked `RejectedByYnab (DuplicateImportId ...)`. The old force code left both unchanged. These are probably fine, but they are unreviewed changes beyond the fix.
- The diff breaks the project writing rule (real umlauts and ß, no transliteration) in src/Tests/PushOutcomeClassificationTests.fs: "abschliessen" in the test name at diff line 542 and in the messages at 558, 635 and 671, and "liess" in the comment at 634. They should read "abschließen" and "ließ".

SUGGESTED_FIX: Keep the session-wide "no open categorized transaction remains" condition inside the classification, so the call site still reads only `SessionMayComplete` (for example, also pass the full post-update session transaction list and AND the old `allCategorizedImported` rule into `SessionMayComplete`). Add a regression test where a failed or unimported tx outside the forced batch stays open and SessionMayComplete must be false. Replace "abschliessen"/"liess" with "abschließen"/"ließ".

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- Check 1, criterion "Regressionstest (Force-Import-Bug, rot vor dem Fix)": the test "Force-Import-Bug (rot vor dem Fix): ein LedgerWriteFailed-Outcome blockiert den Abschluss, ..." (src/Tests/PushOutcomeClassificationTests.fs, diff lines 541 to 581) would not fail under the pre-fix force rule. The failing tx is `Fixtures.makeTx "tx-force-3" NeedsAttention`. `LedgerWriteFailed` leaves it unchanged, so it stays `NeedsAttention`. The old `allCategorizedImported` rule treats `NeedsAttention` as open, so it already blocked completion here. In the new code that same rule is `sessionWideOpenTransactionBlocksForceImport` (`updatedTransactions |> List.exists isOpenCategorized`). If you remove `ledgerWriteFailureSummary.IsNone` from `SessionMayComplete` in src/Server/PushOutcomeClassification.fs (diff line 366), which restores the old force behavior, `SessionMayComplete` is still false and the test still passes. So the test does not tell the bug apart from the fix. That breaks the criterion's explicit "rot vor dem Fix" and the project rule that a bug-fix regression test must fail before the fix. The old force code missed a LedgerWriteFailed outcome only when the failed tx was not in a categorized status (`Pending`, `Skipped`). The new `ledgerWriteFailureSummary.IsNone` term matters on the force path only in that case, and no test covers it. The worker's mutation check was run only for the two iteration-2 tests, not for this one.
- Checked and fine (nothing to fix): iteration-1 reason 1 is resolved (`transactionsToForce` and `otherSessionTransactions` are exact complements over `allTransactions`; the old `importToYnab` completion was batch-local and stays so). Reason 2: marking changes do not touch `Status`, covered by a test. Reason 3 fixed. No dashes in the code diff. Logic appears only once. Build and tests green (983 passed, 7 skipped).

SUGGESTED_FIX: Make the force-import regression test actually red under the old rule. Give the LedgerWriteFailed tx a non-categorized status (for example `Pending` or `Skipped`), keep the other batch txs imported, call with `forceImport = true` and no open tx in `otherSessionTransactions`, and assert `SessionMayComplete = false`. Then confirm by mutation that dropping `ledgerWriteFailureSummary.IsNone` turns it red.

ITERATION_HINT: likely-fixable

## Outcome

Neue reine Funktion `Server.PushOutcomeClassification.classifyPushOutcomes` (Datei
`src/Server/PushOutcomeClassification.fs`, im `Server.fsproj` unmittelbar vor `Api.fs`
eingehängt) mit dem Ergebnis-Record `PushOutcomeClassification` (`UpdatedTransactions`,
`DuplicateTransactionIds`, `LedgerWriteFailureSummary`, `SessionMayComplete`). Sie trägt
genau den Block, der vorher inline im `IEntityApi`-Record in `Server.Api` stand
(`importIdByTxId`, `duplicateImportIdSet`, `futureDatedSet`, `rejectedTransferTxIdSet`,
die Duplikat-Erkennung, `summarizeLedgerWriteFailures`, das `updatedTransactions`-Mapping)
und ist ohne HTTP, ohne `SyncSessionManager`, ohne Fable.Remoting testbar.

`Server.Api.importToYnab` und `forceImportDuplicates` rufen jetzt beide dieselbe Funktion
mit ihren jeweiligen Eingaben (`toImport`/`transactionsToForce`, `prepared`, `pushResult`)
auf und lesen nur noch `classification.SessionMayComplete` für den
`completeSession()`-Aufruf, `classification.UpdatedTransactions` für
`SyncSessionManager.updateTransactions` und `classification.LedgerWriteFailureSummary` /
`classification.DuplicateTransactionIds` für ihre jeweilige Rückgabe. Die alte, zweifach
vorhandene Logik kommt an keiner der beiden Stellen mehr dupliziert vor (`src/Server/Api.fs`
netto kürzer, siehe Diff gegen den Batch-Start).

**Bug-Fix (ADR 0021), Iteration 1:** `forceImportDuplicates` prüfte bisher nur
`allCategorizedImported` (alle Session-Transaktionen ohne offene Kategorisierung), ohne
`LedgerWriteFailed`-Outcomes dieses Versuchs zu berücksichtigen, und konnte die Session
dadurch fälschlich abschließen, obwohl eine Buchung wegen eines gescheiterten
Kassenbuch-Schreibens offen geblieben ist.

**Verifier-Fund und Fix, Iteration 2:** Iteration 1s erste Fassung von `SessionMayComplete`
(`duplicateTransactionIds.IsEmpty && ledgerWriteFailureSummary.IsNone`, rein batch-lokal)
war für `importToYnab` korrekt (dessen Batch ist immer schon "alles gerade Offene" der
Session), führte aber auf dem Force-Pfad zu einer echten Regression gegenüber dem alten
Verhalten: der alte Force-Code prüfte `allCategorizedImported` über die GANZE, bereits
aktualisierte Session, also auch über Buchungen außerhalb des Force-Batches und über im
Batch selbst unverändert gebliebene (zukunftsdatierte, nicht auswertbare) Buchungen. Die
schmale, rein batch-lokale Bedingung aus Iteration 1 sah beides nicht.

`classifyPushOutcomes` bekam deshalb zwei neue Parameter, `forceImport: bool` und
`otherSessionTransactions: SyncTransaction list` (Session-Buchungen außerhalb des
übergebenen Batches). `SessionMayComplete` ist jetzt:

```
duplicateTransactionIds.IsEmpty
&& ledgerWriteFailureSummary.IsNone
&& not (forceImport && (otherSessionTransactions |> List.exists isOpenCategorized
                         || updatedTransactions |> List.exists isOpenCategorized))
```

`isOpenCategorized` bildet dieselbe Fallunterscheidung nach, die `allCategorizedImported`
vor der Extraktion nutzte (`AutoCategorized`/`ManualCategorized`/`NeedsAttention` blockieren,
`Pending`/`Imported`/`Skipped` nicht). Die beiden Pfade unterscheiden sich bewusst, weil ihre
Batches fachlich Verschiedenes bedeuten: `importToYnab`s Batch ist "alles gerade Offene", eine
darin verbliebene zukunftsdatierte oder nicht auswertbare Buchung ist erwartetes Verhalten
(nächster Lauf greift erneut) und blockiert bewusst nicht (unverändertes, mit Test gepinntes
Verhalten). `forceImportDuplicates`s Batch ist dagegen eine von Roman gezielt ausgewählte
Teilmenge; bleibt eine davon nach dem gezielten Versuch offen, oder bleibt anderswo in der
Session noch etwas Kategorisiertes offen, darf die Session nicht abschließen, exakt die
Regel, die `allCategorizedImported` vor der Extraktion über die ganze Session prüfte.
`Server.Api.importToYnab` ruft mit `forceImport = false, otherSessionTransactions = []` auf
(ungenutzt, da die Force-Regel dort nicht greift); `forceImportDuplicates` berechnet
`otherSessionTransactions` aus `allTransactions` (vor diesem Versuch geholt) abzüglich der
per `transactionIds` geforcten Buchungen und ruft mit `forceImport = true` auf.

**Deliberate Verhaltensänderung, dokumentiert und getestet (Verifier-Punkt 2):** vor der
Extraktion ließ der Force-Pfad eine abgelehnte Transfer-Zeile und ein von YNAB gemeldetes
Duplikat unmarkiert (nur `Linked`/`AlreadyImported` wurden umgesetzt, alles andere blieb
`NotAttempted`). Die vereinheitlichte Funktion markiert beide Fälle jetzt wie beim normalen
Push (`RejectedByYnab (UnknownRejection ...)` bzw. `RejectedByYnab (DuplicateImportId ...)`).
Bewusst beibehalten (nicht zurückgebaut): Roman sieht auf dem Force-Pfad denselben Grund wie
auf dem normalen Pfad, statt eines stillen `NotAttempted`; `Status` (und damit, ob die
Buchung als "offen" gilt) bleibt von dieser Markierung unberührt, der Abschluss bleibt in
beiden Fällen ohnehin blockiert (Duplikat bzw. weiterhin kategorisiert). Eigener Test
("Force-Import markiert eine abgelehnte Transfer-Zeile und ein gemeldetes Duplikat wie der
normale Push ...") pinnt das.

**Verifier-Fund und Fix, Iteration 3:** der zentrale Bug-Regressionstest ("Force-Import-Bug
(rot vor dem Fix): ein LedgerWriteFailed-Outcome blockiert den Abschluss, ...") war entgegen
seinem eigenen Namen nicht rot vor dem Fix. Die gescheiterte Buchung trug den Status
`NeedsAttention`; `LedgerWriteFailed` lässt eine Buchung unverändert, sie blieb also
`NeedsAttention`. Genau dieser Status galt schon in der ALTEN Regel (`isOpenCategorized`,
vor der Extraktion `allCategorizedImported`) als offen und blockierte den Abschluss allein
deshalb, unabhängig vom neuen `ledgerWriteFailureSummary.IsNone`-Term. Ohne diesen Term
(Mutation, simuliert Iteration 1s Bug) blieb der Test grün, konnte den Bug von der
Fehlerbehebung also nicht unterscheiden.

Fix: die gescheiterte Buchung trägt jetzt den Status `Pending` statt `NeedsAttention`.
`isOpenCategorized` behandelt `Pending` bewusst nicht als offen ("frisch geholt, noch nicht
kategorisiert"), der Status ist aber für `classifyPushOutcomes` erreichbar:
`importToYnab`s `toImport`-Filter lässt `Pending` ausdrücklich zu (Kategorie ist optional,
`Server.Api.fs`), und `forceImportDuplicates`s `transactionsToForce` filtert
`allTransactions` nur über die übergebenen `TransactionId`, ohne Status-Einschränkung; eine
`Pending`-Buchung, die beim Kassenbuch-Schreiben gescheitert ist, kann also unverändert in
einem Force-Batch landen. Mit `Pending` fängt NUR die neue `ledgerWriteFailureSummary.IsNone`-
Bedingung den Fall, die alte Regel nicht mehr: der Test ist jetzt rot vor dem Fix. Per erneuter
Mutation bestätigt: `ledgerWriteFailureSummary.IsNone` aus `SessionMayComplete` entfernt macht
genau diesen Test (und den bereits bestehenden nicht-Force-Test "LedgerWriteFailed blockiert
den Abschluss auch ohne Duplikate") rot, alle übrigen 981 Tests bleiben grün; nach
Wiederherstellung wieder alle 983 grün. Testname, Kommentar und Assertion-Nachrichten sind
entsprechend aktualisiert; die übrigen Assertions (beide anderen Buchungen bereits importiert,
`LedgerWriteFailureSummary` gesetzt) sind unverändert. Die beiden Force-Blockier-Tests aus
Iteration 2 ("außerhalb des Force-Batches" und "im Batch selbst zukunftsdatiert") waren von
diesem Fund nicht betroffen und bleiben unverändert.

**Tests** (TDD; `src/Tests/PushOutcomeClassificationTests.fs`, im `Tests.fsproj` unmittelbar
nach `ForceImportCandidateTests.fs` eingehängt): elf Fälle insgesamt (acht aus Iteration 1,
drei neu aus Iteration 2, einer davon in Iteration 3 präzisiert wie oben beschrieben):
- "Force-Import: eine offen gebliebene Buchung außerhalb des Force-Batches blockiert den
  Abschluss (Verifier-Fund, Iteration 1)": der zentrale, vom Verifier verlangte
  Regressionstest für das Cross-Batch-Szenario.
- "Force-Import: eine im Batch selbst offen gebliebene zukunftsdatierte Buchung blockiert
  den Abschluss (bestehendes Force-Verhalten wiederhergestellt)": pinnt, dass eine
  zukunftsdatierte Buchung IM Force-Batch selbst weiterhin blockiert, anders als beim
  normalen Push (eigener, umbenannter Test daneben: "... (normaler Import-Push)").
- "Force-Import markiert eine abgelehnte Transfer-Zeile und ein gemeldetes Duplikat wie der
  normale Push, blockiert aber den Abschluss (deliberate Verhaltensänderung, siehe
  Task-Outcome)".

`dotnet build` und `dotnet test`: 983 erfolgreich, 7 übersprungen (nur .env-abhängige
Integrationstests), 0 Fehler.

**Schreibstil-Fix (Verifier-Punkt 3, Iteration 1):** die beiden Stellen mit
"abschliessen"/"liess" in `src/Tests/PushOutcomeClassificationTests.fs` sind auf
"abschließen"/"ließ" korrigiert; der komplette Diff gegen den Batch-Start
(`git diff 66d41a5`) wurde in jeder Iteration erneut gegen ae/oe/ue/ss-Transliterationen und
Gedankenstriche geprüft, keine weiteren Treffer.

**Fund, nicht behoben (siehe Backlog-Item, unverändert gültig):** eine nicht auswertbare
YNAB-Antwort (`ResponseUnparseable`) führt auf dem NORMALEN Import-Push weiterhin zu
`WrittenNotLinked` ohne gemeldete Duplikat-ImportId; das zählt weder als Duplikat noch als
`LedgerWriteFailed` und blockiert `SessionMayComplete` daher nicht, obwohl keine der
Buchungen dieses Versuchs bestätigt verknüpft wurde. Auf dem FORCE-Pfad blockiert derselbe
Fall jetzt (seit Iteration 2, siehe oben), auf dem normalen Pfad ist das Verhalten
unverändert und bleibt der offene Backlog-Punkt `ledger-fgw2q`.
