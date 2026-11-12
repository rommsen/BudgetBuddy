---
id: ledger-f8bjm
title: "Ersatz-POST mit verlorener Antwort: Wiederherstellen, Split- und Betrags-Ersatz scheitern danach dauerhaft an 409, der Spiegel legt die Ersatzbuchung als neue Buchung „YNAB-Spiegel“ an; die Ersatz-import_id muss vor dem POST im Kassenbuch stehen (Ledger/CommandHandlers.fs)"
status: backlog
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [ledger-xqqnv]
blocks: []
tags: [bestandsreview, server, tests]
related_adrs: [0021, 0058, 0092]
related_research: []
prior_art: [ledger-p3vq8, ledger-z6sya]
---

## Why
Fund beim Verfeinern von ledger-xqqnv (Bestandsreview ledger 2026-11-12, Gruppe G4, Bericht
`.agentheim/knowledge/reviews/2026-11-12-ledger.md`), nicht im Bericht selbst.

Die Ersatz-POSTs schicken eine neue Buchung mit der Ersatz-`import_id` (Stamm plus `:r<n>`, ADR 0058 Punkt 2):
`restoreDeletedEntry` (Rückgängig nach dem Löschen, `CommandHandlers.fs:3434`) und der Ersatz in `pushSplitState`
(`:3024`, Aufteilen und Betrag einer aufgeteilten Buchung). BB schreibt das Ergebnis-Ereignis (`EntryRestoredInBb`
bzw. `EntryReplacedInYnab`, beide mit `NewImportId`) erst nach der Antwort. Geht die Antwort verloren (Zeitüberschreitung,
nachdem YNAB angelegt hat), passiert nach Code-Lesart:

- Jeder weitere Versuch rechnet dieselbe Ersatz-`import_id` (`replacementImportId` ist deterministisch über die
  Verknüpfungen bzw. `EntryDeletedInBb.ImportId`) und bekommt 409. „Rückgängig“ bzw. „Erneut senden“ scheitert dauerhaft
  mit „Die Buchung steht schon in YNAB (gleiche import_id, HTTP 409)“.
- Der Spiegel sieht die Ersatzbuchung in YNAB, findet weder ihre YNAB-Id noch ihre `import_id` in den Verknüpfungen und
  legt sie als neue Buchung mit Herkunft „YNAB-Spiegel“ an.
- Beim Split-Ersatz bleibt die alte Buchung dabei sichtbar: ihr DELETE gilt als abgelöst
  (`YnabDeletedTransactionId` am offenen Fehlschlag, `isSupersededDeletion` `:427`), sie wird nicht als gelöscht markiert.
  BB zeigt die Buchung dann **zweimal** (alte mit „Aufteilung nicht in YNAB“, neue aus dem Spiegel), der Kontostand in BB
  weicht von YNAB ab. Beim Wiederherstellen bleibt die alte gelöscht, die Buchung steht einmal da, aber mit falscher
  Herkunft und ohne BB-Historie.

ADR 0021 löst dasselbe Problem für Neuanlagen, indem die `import_id` vor dem Push im Kassenbuch steht. Für Ersatz-POSTs
fehlt das.

## What
- Die Ersatz-`import_id` steht vor dem POST im Kassenbuch (als Ereignis, damit Replay sie herstellt, ADR 0021 Punkt 4),
  so dass der Spiegel die Ersatzbuchung über die `import_id` der bestehenden Buchung zuordnet und die alte YNAB-Id als
  abgelöst gilt.
- Ein 409 auf den Ersatz-POST heißt angekommen (wie ledger-xqqnv für Neuanlagen): kein Fehlschlag, Spiegel anstoßen, der
  Spiegel verknüpft die neue YNAB-Id. Den reinen Einordnungs-Helfer aus ledger-xqqnv wiederverwenden.
- Betroffene Stellen: `restoreDeletedEntry` (`src/Server/Ledger/CommandHandlers.fs:3412`), der Ersatz-Zweig in
  `pushSplitState` (`:2934`, Ersatz ab `:2993`; über `changeSplits` aus `setEntrySplits`, `setEntrySplitsWithAmount`,
  `retryCategorizationOrSplitWriteBack`), Spiegel-Zuordnung `resolveEntry`/`MirrorBatchState` und
  `isSupersededDeletion`.
- Form des neuen Ereignisses (etwa „Ersatz an YNAB gesendet“ mit `NewImportId` und alter YNAB-Id) mit dem
  `tactical-modeler` klären, bevor der Worker schreibt; ADR 0058 und 0092 bekommen einen Nachtrag.

## Acceptance criteria
- [ ] Kaputt Split-Ersatz (Regressionstest, vor dem Fix rot): GIVEN eine verknüpfte Buchung, deren Aufteilung per Ersatz
      geht; das DELETE gelingt, der POST legt in YNAB an, liefert aber `NetworkError`. WHEN danach ein Spiegel-Lauf die
      Ersatzbuchung (neue YNAB-Id, Ersatz-`import_id`) und die Löschung der alten Id beobachtet, THEN gibt es keine neue
      Kassenbuch-Buchung, die bestehende ist mit der neuen YNAB-Id verknüpft, nicht als gelöscht markiert, und die Zahl
      der sichtbaren Buchungen des Kontos ist unverändert. Handler-Test mit Ports in `src/Tests/LedgerSplitWriteTests.fs`.
- [ ] Kaputt Split-Ersatz, Erneut senden: im selben Ausgangszustand liefert „Erneut senden“ mit einem POST, der 409
      antwortet, keinen neuen Fehlschlag mit dem 409-Text, stößt den Spiegel an, und nach dem Spiegel-Lauf ist die
      Buchung verknüpft. Am ausgehenden Aufruf geprüft: dieselbe Ersatz-`import_id`, kein zweites DELETE. Vor dem Fix rot.
- [ ] Kaputt Wiederherstellen (vor dem Fix rot): GIVEN eine in BB gelöschte Buchung, deren `restoreDeletedEntry`-POST in
      YNAB anlegte, aber `NetworkError` lieferte. WHEN ein Spiegel-Lauf die neue YNAB-Buchung beobachtet, THEN entsteht
      keine neue Kassenbuch-Buchung, die bestehende ist wieder sichtbar und mit der neuen YNAB-Id verknüpft, ihre
      Historie zeigt das Wiederherstellen in BB. Ein zweites „Rückgängig“ mit 409 gilt als angekommen, nicht als Fehler.
      Handler-Test in `src/Tests/LedgerEntryDeletionTests.fs`.
- [ ] Intakt: Ersatz und Wiederherstellen mit erfolgreicher Antwort schreiben weiter `EntryReplacedInYnab` bzw.
      `EntryRestoredInBb` mit neuer YNAB-Id; ein POST, der scheitert, ohne in YNAB anzulegen (400), lässt die Buchung im
      bisherigen Fehlschlag- bzw. Gelöscht-Zustand; eine Buchung ohne `import_id` sendet weiter ohne (bestehende Tests in
      `LedgerSplitWriteTests.fs`, `LedgerEntryDeletionTests.fs` bleiben grün).
- [ ] Replay: aus dem Log neu aufgebaut ergibt der Zustand dieselben Verknüpfungen (Ersatz-`import_id`, abgelöste Id)
      wie vor dem Replay. Test auf der Domain-Ebene (`linksFrom`, `apply`) für das neue Ereignis samt Codec-Rundreise.
- [ ] `./verify.sh` mit `VERIFY: PASS`.

## Notes
- Abhängig von ledger-xqqnv: dort entsteht der reine Helfer, der „YNAB kennt die `import_id`“ als angekommen einordnet.
- Der Befund beruht auf Code-Lesart (`replacementImportId` `:2772`, `restoreRequestOf` `:3323`, `isSupersededDeletion`
  `:427`, Spiegel-Zuordnung `:494`). Der erste Kaputt-Test bestätigt ihn; fällt er grün aus, Task mit Befund zurück an den
  Conductor.
- ADR 0058 Probelauf 3: eine in YNAB gelöschte Buchung sperrt ihre `import_id` weiter. Ein 409 auf die Ersatz-`import_id`
  kann also auch heißen, dass die Ersatzbuchung in YNAB schon wieder gelöscht wurde; der Spiegel klärt das, BB erfindet
  keine YNAB-Id.
- Die Reihenfolge „YNAB zuerst“ aus ADR 0091 und 0092 für Löschen und Wiederherstellen bleibt für das DELETE. Neu ist nur,
  dass die geplante Ersatz-`import_id` vor dem POST im Log steht; das ist eine Ergänzung, keine Umkehr, und braucht den
  Nachtrag.
- Überschneidung ledger-vfr4h: dieselbe Funktion `restoreDeletedEntry` (dort nur der erste Zweig mit der Wartesperre).
  Nicht gleichzeitig bauen.
- Live-Aufrufe gegen YNAB nur hinter `RUN_INTEGRATION_TESTS`; die Kaputt-Fälle laufen mit Ports.
