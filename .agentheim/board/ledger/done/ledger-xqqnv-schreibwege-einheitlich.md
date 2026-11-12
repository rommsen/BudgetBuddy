---
id: ledger-xqqnv
title: "Vorhandene import_id heißt angekommen: Barbuchung, Erneut senden, Ist neu und Import-Sammelpush schreiben keinen Fehlschlag, wenn YNAB die import_id schon kennt; Sperre und Auslöser des Import-Pushs belegt (Ledger/CommandHandlers.fs)"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: []
blocks: [ledger-h7de3]
tags: [bestandsreview, server, tests]
related_adrs: [0021, 0058, 0066, 0068, 0093]
related_research: []
prior_art: [ledger-p3vq8]
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G4, Teil 1 von 2 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).

Eine `import_id`, die YNAB schon kennt, heißt: ein früherer Versand kam an, nur die Antwort nicht (ledger-p3vq8).
„Erneut senden“ und „Ist neu“ behandeln das so, der Import-Sammelpush und die Barbuchung schreiben dagegen einen
Fehlschlag. Beim erneuten Abruf einer Comdirect-Buchung mit verlorener Antwort steht dann „YNAB hat die Buchung als
Duplikat abgelehnt“ als Fehlschlag in der Historie, obwohl die Buchung in YNAB ist. Geld wird nicht doppelt gebucht,
aber Grund und Historie sind falsch.

## What
- Ein reiner Helfer ordnet den Ausgang eines Versands für eine Buchung ein: verknüpft (YNAB-Id), angekommen ohne Id
  (`import_id` schon vorhanden) oder gescheitert (Grund). Einzel-Post: `Ok id`, `Error ImportIdAlreadyInYnab` (409),
  sonstiger `Error`. Sammel-Post: Id in `Created`, `import_id` in `DuplicateImportIds`, sonst nicht angelegt, nicht
  auswertbar oder Sende-Fehler.
- Auf allen drei Wegen dieselbe Folge je Ausgang: verknüpft schreibt `EntryLinkedToYnab`; angekommen schreibt **kein**
  Ereignis (kein `EntryPushToYnabFailed`), die Buchung bleibt unverknüpft, der Spiegel verknüpft sie über die
  `import_id`; gescheitert schreibt `EntryPushToYnabFailed` mit Grund. Der Spiegel-Auslöser läuft nach der Freigabe der
  Sperre bei verknüpft und angekommen, nie bei gescheitert.
- Ergebnis an den Aufrufer bei „angekommen“ (entschieden von Roman, 2026-11-12): „Erneut senden“ und „Ist neu“ wie
  heute `PushToYnabSent`. Die Barbuchung (Quick Add, Bob, Neue Buchung) meldet angekommen wie „Erneut senden“:
  `addCashExpense` liefert `Ok entryId`, der Toast sagt „Buchung angelegt“, Bob bekommt `pushed_to_ynab: true`. Import:
  `WrittenNotLinked` wie heute; die Sync-Historie zählt die Buchung weiter als „nicht in YNAB“, bis der Spiegel verknüpft.
- Befunde:
  - `src/Server/Ledger/CommandHandlers.fs:705` | ADR 0021, ledger-p3vq8 | mittel | Vorhandene `import_id` in
    `pushEntryAndRecord` (`:973`), `addCashExpenseWith` (`:789`) und Import-Sammelpush (`describeNotCreatedReason`,
    Zweig in `pushComdirectImports`) verschieden behandelt.
  - `src/Server/Ledger/CommandHandlers.fs:1276` | test-doctrine.md Punkt 4; README *BuchungÜbernehmen* | mittel |
    Schreib-Sperre um `pushComdirectImports` per Verhalten über den echten Lauf belegen.
  - `src/Tests/LedgerEntryDeletionTests.fs:518` (Anteil `pushComdirectImports`) | test-doctrine.md Punkt 4 | mittel |
    Spiegel-Auslöser erst nach Freigabe (`SyncMirrorTrigger`).

## Acceptance criteria
- [ ] Kaputt (Regressionstest, vor dem Fix rot): GIVEN eine Comdirect-Buchung, deren erster Sammelpush scheiterte
      (`EntryPushToYnabFailed`), WHEN ein erneuter Abruf sie sendet und YNAB ihre `import_id` in `duplicate_import_ids`
      meldet, THEN kommt kein zweites `EntryPushToYnabFailed` dazu, der Ausgang ist `WrittenNotLinked`, der
      Spiegel-Auslöser feuert genau einmal, und nach einem Spiegel-Lauf mit der YNAB-Buchung (dieselbe `import_id`) ist
      sie verknüpft (`EntryLinkedToYnab`, Actor `YnabMirror`), ohne zweite Buchung. Handler-Test in
      `src/Tests/LedgerTests.fs`, Liste „Ledger CommandHandlers (pushComdirectImports)“.
- [ ] Der Test „eine von YNAB als Duplikat abgelehnte Buchung bleibt ohne Verknüpfung stehen“ (`LedgerTests.fs:2305`)
      prüft zusätzlich: die Ereignisse sind genau `EntryImportedFromComdirect`, kein `EntryPushToYnabFailed`. Vor dem
      Fix rot.
- [ ] Intakt im Sammelpush: eine nicht angelegte Buchung ohne Eintrag in `duplicate_import_ids` und eine nicht
      auswertbare Antwort schreiben weiter `EntryPushToYnabFailed` mit ihrem Grund (der Test `:2323` prüft das Ereignis
      zusätzlich); ein Sende-Fehler schreibt ihn für alle gesendeten Buchungen und löst keinen Spiegel aus (`:2497`
      bleibt grün).
- [ ] Gemischte Antwort: zwei Kandidaten in einem Sammelpush, einer angelegt, einer in `duplicate_import_ids`: der erste
      `Linked` mit `EntryLinkedToYnab`, der zweite `WrittenNotLinked` ohne Ereignis nach der Entstehung, genau ein
      Auslöser. Gleiche Liste in `LedgerTests.fs`.
- [ ] Kaputt (vor dem Fix rot): `addCashExpense` mit einem Push, der `Error ImportIdAlreadyInYnab` liefert, schreibt nur
      `CashExpenseAdded`, kein `EntryPushToYnabFailed`, liefert `Ok entryId` und ruft den Auslöser genau einmal.
      Handler-Test in `src/Tests/LedgerCashBookingTests.fs`, Liste „Ledger Barbuchung (feste import_id, ledger-p3vq8)“.
      Intakt daneben: ein Netzfehler schreibt weiter `EntryPushToYnabFailed`, kein Auslöser.
- [ ] Neue Buchung (`addLedgerBooking`) mit 409: `PushFailure = None` im Ergebnis, kein `EntryPushToYnabFailed`. Test in
      `src/Tests/LedgerBookingCaptureTests.fs`, Liste „Buchung erfassen: Befehl“.
- [ ] Bob: ein Commit, dessen Push 409 liefert, antwortet mit `pushed_to_ynab: true` ohne `push_error`, ergibt genau eine
      Bob-Buchung im Kassenbuch und zählt in der Tagesnutzung genau einmal (Summe und Anzahl); ein zweiter Commit
      derselben Vorschau pusht nicht erneut. Commit-Test mit Ports (`LedgerCashBookingTests.fs` „Bot Barbuchung (Commit)“
      oder `src/Tests/BotApiTests.fs`). Grenzwerte und ihre Tests bleiben unverändert.
- [ ] „Ist neu“ (`confirmEntryIsNew`) mit 409: `EntryConfirmedAsNewInBb`, kein `EntryPushToYnabFailed`, Ergebnis
      `PushToYnabSent`, ein Auslöser. Test in `src/Tests/LedgerDuplicateDecisionTests.fs` (Befehlsliste, nicht die
      Sperre-Liste). Der 409-Test für „Erneut senden“ (`LedgerCashBookingTests.fs:716`) bleibt grün.
- [ ] Der Helfer ist rein und hat einen Test je Ausgang (Einzel: Ok, 409, anderer Fehler; Sammel: angelegt, Duplikat,
      nicht angelegt, nicht auswertbar, Sende-Fehler).
- [ ] Schreib-Sperre: der Test `LedgerTests.fs:2546` läuft über `LedgerWriteGuardProbe.commandWaitsForMirror`: während
      der Lauf hängt, gibt es zur ImportId keine Buchung und keinen Push-Aufruf; danach `EntryImportedFromComdirect` und
      `EntryLinkedToYnab` (Actor `ComdirectImport`). Der selbstgebaute Lauf entfällt.
- [ ] Auslöser nach der Freigabe: `pushComdirectImports` mit `LedgerWriteGuardProbe.SyncMirrorTrigger(2000)` unter
      `LedgerWriteGuardProbe.serialized`, `FinishedInTime = Some true` (Vorbild `src/Tests/LedgerRetryPushTests.fs:193`).
      Gegenprobe im Outcome: Auslöser in den gesperrten Abschnitt verschoben, Test rot.
- [ ] `./verify.sh` mit `VERIFY: PASS`.

## Notes
- Urteile: `CommandHandlers.fs:705` gilt. Am Code: `pushEntryAndRecord` gibt bei 409 `PushToYnabSent` ohne Ereignis;
  `addCashExpenseWith` schreibt `EntryPushToYnabFailed` „Die Buchung steht schon in YNAB (gleiche import_id, HTTP 409)“
  und gibt `Error` ohne Auslöser, praktisch kaum erreichbar (frische EntryId, `CashExpenseAdded` vor dem Push, Bob prüft
  vor jeder Wiederholung `bookedFromLedger`, `src/Server/BotApi.fs:427`); der Sammelpush schreibt „als Duplikat
  abgelehnt“, genau im Fall `SendImportAgain` (ADR 0093 Punkt 7). `CommandHandlers.fs:1276` gilt teilweise: belegt durch
  `LedgerTests.fs:2546`, aber mit selbstgebautem Lauf statt `runMirrorGuarded`. Auslöser nach Freigabe für den Import
  gilt: `LedgerTests.fs:2463` prüft nur, dass `EntryLinkedToYnab` beim Auslösen schon steht.
- Doppelte Buchung ist auf keinem der drei Wege möglich: YNAB legt bei vorhandener `import_id` nichts an, BB schreibt
  keine zweite Buchung. Bobs Tagesgrenzen (ADR 0068) sind nicht berührt: `loadDailyUsage` (`BotApi.fs:133`) zählt Bobs
  `CashExpenseAdded` aus dem Log, unabhängig vom Push-Ausgang. Grenzen nicht anfassen.
- YNABs Sammel-POST antwortet bei vorhandener `import_id` mit 201 und `duplicate_import_ids`, der Einzel-POST mit 409
  (ADR 0058 Probelauf, Outcome ledger-p3vq8). Der Helfer bildet beide ab; im Sammelpush kommt `ImportIdAlreadyInYnab`
  nie vor.
- ADR 0058 Probelauf 3: auch eine in YNAB gelöschte Buchung sperrt ihre `import_id`. „Angekommen“ heißt also „YNAB kennt
  die `import_id`“; ob die Buchung dort noch lebt, klärt der Spiegel. Nicht als verknüpft behandeln, keine YNAB-Id
  erfinden.
- „Rückgängig“ im Toast nach einer Neuen Buchung mit 409 kann in den Sekunden bis zum Spiegel „Die Buchung ist noch nicht
  in YNAB“ antworten, ohne etwas zu löschen (ADR 0091 Punkt 3). Von Roman so hingenommen.
- ADR 0021 Punkt 2 nennt „lehnt YNAB die Buchung als Duplikat ab“ als Fehlschlag. Die Buchung bleibt unverknüpft und
  damit „noch nicht in YNAB“, nur ohne Fehlschlag-Ereignis (Grund dann `PushOutcomeUnknown`, Bob-Text
  `unknownPushStateText`). Als Nachtrag zu ADR 0021 im RESULT melden.
- Kommentare nachziehen: `ComdirectPushOutcome.WrittenNotLinked`, Doc-Kommentare von `pushComdirectImports` und
  `addCashExpenseWith`. README ledger: *BuchungÜbernehmen (Comdirect-Import-Push)* und der Bullet zur Barbuchung; Delta
  mit `npm run check:readme-delta` prüfen.
- Überschneidung ledger-h7de3 (Fehlertexte in Ereignissen): dieselbe Funktion `pushComdirectImports` (`WriteFailed
  ex.Message`, `:1347`) und die YNAB-Fehlertexte an der Push-Grenze (`ynabErrorText`, `describeNotCreatedReason`). Darum
  läuft diese Task vor h7de3. Diese Task fasst `src/Server/Api.fs` nicht an.
- Überschneidung ledger-vfr4h (Teil 2): beide berühren `src/Tests/LedgerDuplicateDecisionTests.fs`, diese Task nur die
  Befehlsliste, vfr4h nur die Sperre-Liste. Parallel möglich, beim Mergen auf Konflikt achten. Alles zu
  `pushComdirectImports` liegt hier, vfr4h fasst die Funktion nicht an.
- Kann-Sammlung ledger Punkt 16 (N+1 in `pushComdirectImports`) ist im Code schon erledigt (`findEntriesByLinks` und
  `loadEntries`, `CommandHandlers.fs:1290`).
- Der Befund `LedgerBatchReadTests.fs:54` (Lesezahl der Sammelbefehle) geht nach Romans Entscheidung in die Kann-Sammlung.
- Die Ersatz-POSTs mit `import_id` plus `:r<n>` haben eine verwandte Lücke: eigene Task ledger-f8bjm.
- Test-Hilfen: `LedgerWriteGuardProbe.commandWaitsForMirror` und `SyncMirrorTrigger` in
  `src/Tests/LedgerWriteGuardProbe.fs`. `SyncMirrorTrigger` zählt nicht, zum Zählen ein eigener Zähler im Test.

## Outcome

Ein reiner Helfer ordnet den Ausgang eines Versands ein (`src/Server/Ledger/CommandHandlers.fs`): `PushArrival` mit `ArrivedLinked id`, `ArrivedWithoutId` und `PushFailedBecause reason`. `singlePushArrival` deckt den Einzel-Post ab (`Ok id`, 409 `ImportIdAlreadyInYnab`, anderer Fehler), `batchPushArrival` den Sammel-Post (in `Created`, in `DuplicateImportIds`, nicht angelegt, nicht auswertbar, Sende-Fehler). `arrivalEvent` liefert das Ereignis, `arrivalTriggersMirror` sagt, ob der Spiegel angestoßen wird. `describeNotCreatedReason` und der Satz „YNAB hat die Buchung als Duplikat abgelehnt“ sind entfallen.

- `addCashExpenseWith` (Quick Add, Bob, Neue Buchung) und `pushEntryAndRecord` (Erneut senden, Ist neu) nutzen den Helfer. Bei 409 schreibt die Barbuchung nur `CashExpenseAdded`, liefert `Ok entryId` und stößt den Spiegel an. Daraus folgt `LedgerBookingCreated.PushFailure = None` (der Toast sagt „Buchung angelegt“, am Client war nichts zu ändern, `Capture.fs` wertet schon `PushFailure` aus), und Bob bekommt `pushed_to_ynab: true` ohne `push_error` (`BotApi.fs` blieb unverändert, die Grenzen auch).
- `pushComdirectImports`: je gesendeter Buchung `batchPushArrival`. Eine import_id in `duplicate_import_ids` ergibt `WrittenNotLinked` ohne Ereignis. Der Auslöser läuft nach der Freigabe, wenn mindestens eine Buchung ankam (verknüpft oder ohne Id). Verhaltensänderung: eine nur nicht auswertbare Antwort stößt den Spiegel nicht mehr an (vorher bei jedem `Ok`), nach der Regel „nie bei gescheitert“.
- Doc-Kommentare von `WrittenNotLinked`, `pushComdirectImports` und `addCashExpenseWith` sind nachgezogen.

Tests (vollständiger Lauf 4768 grün, `./verify.sh`: `VERIFY: PASS`):
- `src/Tests/LedgerTests.fs`: neue Liste „Ledger Ausgang eines Versands (rein, ledger-xqqnv)“ mit 8 Fällen. In „Ledger CommandHandlers (pushComdirectImports)“ neu: der Regressionstest (erster Push scheitert, erneuter Abruf mit Duplikat, kein zweiter Fehlschlag, ein Auslöser, danach Spiegel-Lauf mit `EntryLinkedToYnab` durch `YnabMirror`, keine zweite Buchung), die gemischte Antwort, „nicht angelegte Buchung ohne Eintrag in duplicate_import_ids“ und der Auslöser nach Freigabe (`SyncMirrorTrigger(2000)` unter `serialized`, `FinishedInTime = Some true`). Der Duplikat-Test prüft jetzt genau `EntryImportedFromComdirect`, der Test zur nicht auswertbaren Antwort prüft das Ereignis mit Grund. Der Sperre-Test läuft über `LedgerWriteGuardProbe.commandWaitsForMirror`: während der Lauf hängt, gibt es keine Buchung zur ImportId und keinen Push, danach Entstehung und Verknüpfung mit Actor `ComdirectImport`. Der selbstgebaute Lauf ist entfallen.
- `src/Tests/LedgerCashBookingTests.fs`: 409 bei der Barbuchung (kaputt), Netzfehler (intakt).
- `src/Tests/LedgerBookingCaptureTests.fs`: Neue Buchung mit 409.
- `src/Tests/LedgerDuplicateDecisionTests.fs` (Befehlsliste): Ist neu mit 409.
- `src/Tests/BotApiTests.fs`: Commit mit 409, eine Buchung, kein zweiter Push, Tagesnutzung zählt Summe (1 Euro: 499 geht, 499,01 ergibt 422) und Anzahl (nach neun Commits geht ein zehnter, nach zehn 422) genau einmal.
- `src/Tests/LedgerPushFailureTests.fs`: der alte Test „Duplikat: der Grund nennt das Duplikat“ ist auf das neue Verhalten umgestellt (kein Fehlschlag).

Gegenproben:
1. Duplikat und 409 im Helfer wieder als Fehlschlag: 11 Tests rot, darunter Regressionstest, Duplikat-Test, gemischte Antwort, Barbuchung 409, Neue Buchung 409, Bob 409, Ist neu 409 und Erneut senden 409.
2. Auslöser in den gesperrten Abschnitt verschoben: genau „der Spiegel-Auslöser nach dem Import-Push läuft erst nach Freigabe der Sperre“ rot.
3. `WriteGuard.exclusive` um `pushComdirectImports` entfernt: genau „ein Spiegel-Lauf hält den Import-Push an, bis er fertig ist“ rot.

Für den Conductor, README ledger von Hand: der Bullet *BuchungÜbernehmen* unter `## Key commands` ist per replace nicht erreichbar (kursiver Kopf, Prüfung 6). Dort ist der Satz „Push-Fehler, YNAB-Duplikat-Ablehnung oder eine nicht auswertbare Antwort lassen die Kassenbuch-Buchung unverknüpft stehen, kein Rollback; jede geschriebene oder wiedergefundene, nicht verknüpfte Buchung des Versuchs bekommt ein `EntryPushToYnabFailed` (Grund, Actor `ComdirectImport`), `AlreadyImported` keins.“ zu ersetzen durch: „Den Ausgang je gesendeter Buchung ordnet `batchPushArrival` ein (ledger-xqqnv, **Versand-Ausgang**): angelegt verknüpft mit `EntryLinkedToYnab`; steht ihre `import_id` in `duplicate_import_ids`, ist sie angekommen ohne Id (`WrittenNotLinked`, kein Ereignis, der Spiegel verknüpft); ein Push-Fehler, eine nicht angelegte Buchung oder eine nicht auswertbare Antwort lassen sie unverknüpft stehen, kein Rollback, mit `EntryPushToYnabFailed` (Grund, Actor `ComdirectImport`), `AlreadyImported` keins. Der Spiegel-Auslöser läuft nach der Freigabe der Sperre, wenn mindestens eine Buchung ankam (verknüpft oder ohne Id), nie, wenn keine ankam.“ Das README_DELTA ist mit `npm run check:readme-delta -- --bc ledger` geprüft (Exit 0). Zusätzlich korrigiert es den veralteten Satz „Nicht gesperrt ist der Comdirect-Import-Push“ im Bullet **Schreib-Sperre**, denn der Code sperrt ihn.

Nachtrag zu ADR 0021 (zum Übernehmen als Abschnitt „Nachtrag 2026-11-12, vorhandene import_id (ledger-xqqnv)“): Punkt 2 nannte „lehnt YNAB die Buchung als Duplikat ab“ als Fehlschlag. Kennt YNAB die `import_id` schon (Einzel-Post 409, Sammel-Post `duplicate_import_ids`), kam ein früherer Versand an, nur seine Antwort nicht (ledger-p3vq8). Auf allen Wegen (Barbuchung, Erneut senden, Ist neu, Import-Sammelpush) schreibt BB dann kein `EntryPushToYnabFailed`. Die Buchung bleibt unverknüpft und damit „noch nicht in YNAB“ (Grund `PushOutcomeUnknown`, Bob-Text `unknownPushStateText` bei einer Wiederholung aus dem Log), bis der Spiegel sie über die `import_id` verknüpft. BB erfindet keine YNAB-Id, denn auch eine in YNAB gelöschte Buchung sperrt ihre `import_id` (ADR 0058 Probelauf 3). Kein Rollback bleibt (Punkt 2).

Für die Kann-Sammlung ledger:
- `LedgerEntryDeletionTests.fs:518` prüft für `pushComdirectImports` weiter mit eigenem Muster. Der neue Test in `LedgerTests.fs` deckt den Auslöser nach Freigabe jetzt ab, der alte Anteil könnte entfallen.
- *WartendeÜbernehmen* im README sagt „der Spiegel-Auslöser … nur bei Erfolg“. Gemeint ist `PushToYnabSent`, also mit 409. Bei Gelegenheit präzisieren.
