---
id: ledger-p3vq8
title: "addCashExpense: gelingt der Push und scheitert danach das Schreiben der Verknüpfung, legt der Spiegel eine zweite Buchung an"
status: doing
type: bug
context: ledger
created: 2026-11-06
completed:
depends_on: []
blocks: []
tags: [etappe-2, ynab, quick-add, bot]
related_adrs: [0021, 0004, 0015, 0032]
related_research: []
prior_art: [ledger-z71ka]
---

## Why
Fund aus dem Review-Lauf zu ledger-z71ka (backend-code-quality, Warnung 2). `Ledger.CommandHandlers.addCashExpense`
schreibt `CashExpenseAdded`, pusht und schreibt danach `EntryLinkedToYnab`. Geht die Antwort auf den Push verloren
(Zeitüberschreitung, nachdem YNAB die Buchung schon angelegt hat) oder wirft das Schreiben der Verknüpfung, steht im
Stream nur `CashExpenseAdded`. Der Spiegel findet die YNAB-Buchung dann nicht (Quick Add und Bob tragen keine
ImportId, ADR 0004) und legt eine zweite Kassenbuch-Buchung an; "Erneut senden" bucht in YNAB doppelt. Betrifft Quick
Add und Bob gleich und besteht seit ADR 0021 (dort unter Negative hingenommen). Erster Schritt von Etappe 2 im
Fahrplan, weil er Geld doppelt zeigen kann.

## What
Entschieden (Roman, 2026-11-08, Nachtrag ADR 0004): Jede Buchung, die BB anlegt und pusht, geht mit einer festen
`import_id` nach YNAB: `BB:` plus die EntryId als Guid ohne Bindestriche (35 Zeichen). Die EntryId legt der Aufrufer
schon vor dem Push fest (ledger-z71ka), die `import_id` ist darum über Wiederholungen gleich.

- `CashExpenseAdded` hält die `import_id` fest (das Ereignis trägt, was der Push und der Spiegel brauchen). Ältere
  Ereignisse ohne sie bleiben gültig und lesbar (ADR 0013 Punkt 7, Versionierung nach ADR 0032).
- Der Spiegel findet eine YNAB-Buchung mit dieser `import_id` über den vorhandenen ImportId-Weg
  (`MatchedByImportId`) und hängt `EntryLinkedToYnab` an (Actor `YnabMirror`, wie ADR 0021 Nachtrag ledger-r6wn3).
- "Erneut senden" (`retryPushToYnab`) schickt bei einer Buchung mit `import_id` genau diese mit. Meldet YNAB sie als
  schon vorhanden, ist das kein Fehlschlag: BB stößt den Spiegel an, der verknüpft.
- Buchungen ohne `import_id` im Ereignis (vor diesem Schritt gepusht) senden weiter ohne sie (Roman: nur neue).

Nicht Teil: der Abgleich einer von Hand erfassten Buchung mit einem späteren Comdirect-Import (Etappe 2, "Buchung
erfassen für jedes Konto").

## Acceptance criteria
- [ ] Kaputt: Push gelingt, das Schreiben von `EntryLinkedToYnab` wirft; danach läuft der Spiegel mit der
      YNAB-Buchung, die die `import_id` trägt. Es entsteht keine zweite Kassenbuch-Buchung, die vorhandene ist
      verknüpft (`EntryLinkedToYnab`, Actor `YnabMirror`). Test rot vor dem Fix.
- [ ] Kaputt: Push legt in YNAB an, liefert aber einen Fehler (Zeitüberschreitung), BB schreibt
      `EntryPushToYnabFailed`; danach "Erneut senden". Der Push geht mit derselben `import_id` hinaus, eine Antwort
      "schon vorhanden" führt zu keinem neuen `EntryPushToYnabFailed`, und nach dem Spiegel ist die Buchung verknüpft.
      In YNAB entsteht keine zweite Buchung (geprüft am ausgehenden Aufruf: dieselbe `import_id`). Test rot vor dem Fix.
- [ ] Der Push von Quick Add und Bob trägt `import_id` = `BB:` plus EntryId ohne Bindestriche (Vertragstest am
      JSON-Rumpf, ersetzt die Erwartung "import_id abwesend" in `src/Tests/QuickAddTests.fs`).
- [ ] Intakt: Push gelingt und Verknüpfung wird geschrieben, Push scheitert mit `EntryPushToYnabFailed`; beides
      unverändert.
- [ ] Intakt: "Erneut senden" für eine Buchung, deren `CashExpenseAdded` keine `import_id` trägt, schickt keine
      `import_id`.
- [ ] Wie YNAB auf ein Einzel-Post mit schon vorhandener `import_id` antwortet (409, `duplicate_import_ids` oder
      anderes), steht mit Quelle im Outcome; die Behandlung folgt dieser Antwort.

## Notes
- ADR 0004 Nachtrag 2026-11-08 (die Entscheidung), ADR 0021 (Kassenbuch zuerst, Negative zum Fenster zwischen Push und
  Verknüpfung), ADR 0015 (Match-Reihenfolge YNAB-Id vor ImportId), ADR 0032 (Ereignis-Versionen).
- Stellen: `addCashExpense` und `retryPushToYnab` in `src/Server/Ledger/CommandHandlers.fs`,
  `buildManualTransactionBodyWithFlag` in `src/Server/YnabClient.fs` (kann `import_id` beim Neusenden schon setzen,
  ledger-cks06), `MirrorBatchState`/`EntryMatch` für den Spiegel, `src/Server/BotApi.fs` für Bob.
- Der Bot meldet den Zustand seit ledger-z71ka als "Push-Ergebnis unbekannt" in `push_error`. Ob der Text nach dem Fix
  so bleibt, entscheidet der Worker am Verhalten (die Buchung wird mit dem nächsten Spiegel verknüpft).
- Test-Doktrin Punkt 3 und 9: Handler-Tests mit `push` als Port, Kaputt- und Intakt-Szenario als GIVEN/WHEN/THEN.
  Live-Aufrufe gegen YNAB nur hinter `RUN_INTEGRATION_TESTS`.
