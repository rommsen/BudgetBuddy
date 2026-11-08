---
id: ledger-p3vq8
title: "addCashExpense: gelingt der Push und scheitert danach das Schreiben der Verknüpfung, legt der Spiegel eine zweite Buchung an"
status: done
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

## Outcome

Quick Add und Bob pushen jetzt mit der festen `import_id` aus ADR 0004 Nachtrag. Eine verlorene Push-Antwort ergibt keine zweite Buchung mehr, weder im Kassenbuch noch in YNAB.

- `src/Server/Ledger/Events.fs`: `CashExpenseAddedPayload.ImportId: string option`, Schema-Version 3 (Schlüssel `importId`). Version 1 und 2 dekodieren mit `ImportId = None` (`decodeCashExpenseAddedV2` neu, `decodeCashExpenseAddedV1` unverändert bis auf das Feld).
- `src/Server/Ledger/Domain.fs`: `linksFrom` stiftet aus `CashExpenseAdded` mit `import_id` die ImportId-Verknüpfung. Damit trifft der vorhandene Weg `MirrorBatchState`/`resolveEntry` die Buchung als `MatchedByImportId` und hängt `EntryLinkedToYnab` mit Actor `YnabMirror` an. Am Spiegel selbst ist nichts geändert.
- `src/Server/Ledger/CommandHandlers.fs`: `cashEntryImportId` (`BB:` plus Guid im Format `N`, 35 Zeichen). `addCashExpense` schreibt sie in `CashExpenseAdded`, Signatur und Port `AddCashExpense` sind unverändert. `addCashBookingWith`, `addCashBooking` und `addQuickAdd` geben dem Push `import_id` und Request (`string -> ManualTransactionRequest -> ...`). `retryPushToYnab` behandelt `Error ImportIdAlreadyInYnab` als `PushToYnabSent`: kein Ereignis, der Spiegel wird angestoßen. Das Neusenden nimmt die `import_id` wie bisher über `planEntryPush`/`currentImportId` aus den Verknüpfungen. Eine Buchung ohne `import_id` im Ereignis hat keine solche Verknüpfung und sendet ohne.
- `src/Server/YnabClient.fs`: `interpretCreateTransactionResponse` bildet 409 auf `YnabError.ImportIdAlreadyInYnab` ab (neuer Fall in `src/Shared/Domain.fs` mit festem Satz in `ynabErrorText`). `createManualTransaction` ist entfernt, weil sie keinen Aufrufer mehr hat. Quick Add (`Server.Api.liveQuickAddPorts`, `QuickAddPorts.Push` mit `string option` vorne) und Bob (`BotDeps.Push` mit `YnabAccountId * string * ManualTransactionRequest`) gehen über `createTransactionWith (Some importId)`. Die Struktur-Wächter in `src/Tests/StructureGuardTests.fs` sind nachgezogen, dazu zwei neue Gegenbeweise für Bob: ohne `import_id` und mit anderer `import_id`.
- YNABs Antwort auf ein Einzel-Post mit einer `import_id`, die auf dem Konto schon existiert: HTTP 409, "a transaction with the same import_id already exists on the specified account". Quelle ist der Probelauf gegen das Wegwerf-Budget vom 2026-10-24 in ADR 0058 (Tabelle "Probelauf", Fall 3: DELETE, danach POST mit derselben `import_id`). Die YNAB-OpenAPI listet 409 für `createTransaction` mit derselben Bedeutung. `duplicate_import_ids` kommt nur in der Batch-Antwort vor. Ein Live-Aufruf lief in dieser Task nicht.
- Bobs `push_error`-Text "Ob der Push zu YNAB gelang, ist unbekannt; der Spiegel gleicht ab" (`unknownPushStateText`) bleibt. Er stimmt jetzt auch im Verhalten, denn der nächste Spiegel verknüpft die Buchung über die `import_id`.
- Tests (in `src/Tests/LedgerCashBookingTests.fs`, Liste "Ledger Barbuchung (feste import_id, ledger-p3vq8)", GIVEN/WHEN/THEN):
  - Kaputt 1: Der Push gelingt, danach wirft das Schreiben von `EntryLinkedToYnab`. Eine fremde Zeile in `ledger_entry_links` belegt dafür die YNAB-Id. Danach verknüpft der Spiegel ohne zweite Buchung, Actor `YnabMirror`.
  - Kaputt 2: Zeitüberschreitung mit `EntryPushToYnabFailed`, danach "Erneut senden" über das echte `Server.Api.entryPushWith`. Beide ausgehenden Aufrufe tragen dieselbe `import_id`, die 409-Antwort ergibt kein neues `EntryPushToYnabFailed`, und nach dem Spiegel ist die Buchung verknüpft und der Fehlschlag aufgelöst.
  - Intakt: "Erneut senden" ohne `import_id` im Ereignis sendet `None`, und "Erneut senden" mit einem anderen Fehler schreibt weiter `EntryPushToYnabFailed`.
  - Vor dem Fix rot: Mit `linksFrom` ohne den neuen Fall fallen beide Kaputt-Tests und der Quick-Add-Test durch. Ohne die 409-Behandlung fällt Kaputt 2 durch.
  - Vertragstests am JSON-Rumpf: Der Quick-Add-Adapter baut den Rumpf wie der Live-Push und trägt `import_id` = `BB:` plus EntryId. Der Bob-Adaptertest prüft dasselbe am Rumpf. In `src/Tests/QuickAddTests.fs` ersetzt "Quick Add und Bob senden die feste import_id" die Erwartung "import_id abwesend". "ohne Schlüssel" deckt die alten Buchungen ab.
  - Weitere Tests: 409-Abbildung in `src/Tests/YnabClientTests.fs`, in `src/Tests/LedgerTests.fs` die Dekodierung von Version 2 ohne `import_id`, der Roundtrip von Version 3 und die Verknüpfung, und die Schema-Version 3.
  - Die übrigen Tests sind nur an das neue Pflichtfeld angepasst (`ImportId = None`). Die Intakt-Fälle "Push gelingt und verknüpft" und "Push scheitert mit `EntryPushToYnabFailed`" sind unverändert grün.
- `./verify.sh`: VERIFY: PASS (dotnet 4296 grün, 13 opt-in-Integrationstests übersprungen, node 309 grün).
- README: Zwei Stellen ließen sich per Delta nicht erreichen, weil die Abschnitte "Key commands" und "Ereignisse" keine fetten Bullet-Köpfe haben. Der Conductor muss sie von Hand nachziehen:
  - Unter *BuchungÜbernehmen* steht noch `YnabClient.createManualTransaction`. Heute ist es `createTransactionWith` mit der festen `import_id`.
  - Bei `CashExpenseAdded` unter "Ereignisse" fehlt: "Seit Schema-Version 3 (ledger-p3vq8) mit `importId` (`BB:` plus EntryId), Version 1 und 2 ohne."
- Folgefund mit eigenem Ticket (blockiert ein Feature): ledger-m7k2d. Ein Split einer neuen Barbuchung lässt sich nicht mehr ändern oder aufheben, weil `BB:` plus 32 Zeichen plus `:r1` die 36 Zeichen übersteigt.
- Kann-Sammlung ledger: Nach einem 409 bei "Erneut senden" meldet BB `PushToYnabSent`, bevor der angestoßene Spiegel verknüpft hat. Bis der Lauf fertig ist, kann die Hinweisleiste "Noch nicht in YNAB" noch kurz stehen.
