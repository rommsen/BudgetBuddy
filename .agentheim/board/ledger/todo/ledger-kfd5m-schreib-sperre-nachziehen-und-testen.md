---
id: ledger-kfd5m
title: "Schreib-Sperre: Quick Add, Bob, Erneut senden und Prüfvermerk sperren den Spiegel nicht aus, die meisten Sperren sind ungetestet"
status: todo
type: bug
context: ledger
created: 2026-11-07
completed:
depends_on: []
blocks: [ledger-bk3q8, ledger-r8h19]
tags: [ledger, schreib-sperre, spiegel, quick-add, bot, pruefvermerk, bestandsreview]
related_adrs: [0055, 0021, 0050, 0014, 0066]
related_research: []
prior_art: [ledger-p3vq8, ledger-sfab0, ledger-h4p6c]
---

## Why
Bestandsreview ledger vom 2026-11-06, Gruppe 1 (`.agentheim/knowledge/reviews/2026-11-06-ledger.md`, Abschnitt 1, höchste Schwere hoch). Roman hat am 2026-11-07 die Option "Sperre nachziehen und alle Stellen testen" gewählt.

Die Schreib-Sperre (`Ledger.CommandHandlers.WriteGuard.exclusive`, ADR 0055 Punkt 6) hält den Spiegel-Lauf und einen Schreibbefehl auseinander. Vier Wege schreiben ein Ereignis und pushen zu YNAB, ohne sie zu nehmen:

- Quick Add und Bob (`addCashExpense`): Läuft der Spiegel zwischen Push und `EntryLinkedToYnab`, sieht er die neue YNAB-Buchung ohne Partner und legt sie ein zweites Mal ins Kassenbuch. Beispiel: Bob bucht 8,40 € Kaffee, während Roman das Kassenbuch öffnet; danach steht die Buchung doppelt da.
- Erneut senden (`retryPushToYnab`): derselbe Fall.
- Prüfvermerk (`changeReviewMark`, `retryReviewMarkWriteBack`, `changeReviewMarkBatch`): Liest ein Lauf den YNAB-Stand zwischen Ereignis und PATCH, dreht er `approved` zurück. Roman hakt ab, der Haken springt zurück.

Dazu prüft nur die Kategorie (Einzel und Auswahl) per Test, dass ihre Sperre wirkt. Ein entferntes `WriteGuard.exclusive` bei Memo, Split, Lena-Flag oder Lena-Auswahl fiele heute keinem Test auf.

## What
Am Code vom 2026-11-07 (main 7423fb0) geprüft.

**Ohne Sperre, unter `WriteGuard.exclusive` legen** (`src/Server/Ledger/CommandHandlers.fs`):
- `addCashExpense` (:671), für Quick Add (`Server.Api` :509) und Bob (`BotApi.liveDeps`, `AddCashExpense`, :566)
- `retryPushToYnab` (:749)
- `changeReviewMark` (:1204), deckt `setReviewMark` (:1250) und `restoreReviewMark` (:1260)
- `retryReviewMarkWriteBack` (:1273)
- `changeReviewMarkBatch` (:1365), deckt `setReviewMarkBatch` (:1452) und `restoreReviewMarkBatch` (:1466)

Bei `setReviewMarkBatch` gehört die Auflösung der Auswahl (`resolveReviewMarkBatchSelection`) mit unter die Sperre oder nicht: der Worker entscheidet und begründet im Kommentar. Maßgeblich ist, dass Ereignisse und PATCH im selben gesperrten Abschnitt liegen.

**Auslöser nach dem Push:** `triggerMirror` (in `addCashExpense` und `retryPushToYnab`) wird erst nach Freigabe der Sperre aufgerufen, nie aus dem gesperrten Abschnitt heraus (Empfehlung Gruppe 1).

**Heute gesperrt, ohne Sperr-Test:** je Aufrufstelle von `WriteGuard.exclusive` ein Test nach dem Vorbild `src/Tests/LedgerCategoryChangeTests.fs:624` (Lauf hängt im Abruf, Befehl startet, kein Ereignis und kein Push vor dem Ende des Laufs, danach beides):
- `retryCategorizationWriteBack` (:1794), Test in `LedgerCategoryChangeTests.fs`
- `setEntrySplits` (:2110) und `retryCategorizationOrSplitWriteBack` (:2210), Tests in `LedgerSplitWriteTests.fs`
- `changeFlag` (:2317, über `setLenaFlag`) und `retryFlagWriteBack` (:2402), Tests in `LedgerLenaFlagTests.fs`
- `setEntryMemo` (:2493) und `retryMemoWriteBack` (:2537), Tests in `LedgerMemoTests.fs`
- `changeFlagBatch` (:2576, über `setLenaFlagBatch`), Test in `LedgerLenaFlagBatchTests.fs`

Schon getestet und unverändert: `setEntryCategory` (:1602, `LedgerCategoryChangeTests.fs:624`), `setEntryCategoryBatch` (:1678, `LedgerCategoryBatchTests.fs:312`).

Ein gemeinsamer Test-Helfer für "Befehl wartet auf den Lauf" ist erwünscht, damit die 15 neuen Tests (7 für die neuen Sperren, 8 für die bestehenden) keine 15 Kopien werden.

**Doku:** README ledger, Bullet **Schreib-Sperre** (heute nur Kategorie und Split), nennt alle gesperrten Befehle. Die Key commands *BuchungÜbernehmen* (Quick Add und Bob), *PushErneutSenden*, *PrüfvermerkSetzen* und *PrüfvermerkFürAuswahlSetzen* sagen "Läuft unter der Schreib-Sperre" wie die Kategorie. ADR 0055 bekommt einen Nachtrag 2026-11-07: Die Sperre gilt für jeden Kassenbuch-Befehl, der ein Ereignis schreibt und danach zu YNAB pusht.

## Acceptance criteria
- [ ] Kaputt (Quick Add/Bob), Handler-Ebene mit Ports als Funktionswerte: GIVEN ein Spiegel-Lauf hängt im Abruf (`runMirrorGuarded` mit wartendem `FetchTransactions`), WHEN `addCashExpense` startet, THEN steht vor dem Ende des Laufs kein `CashExpenseAdded` im Log und `push` wurde nicht aufgerufen; nach dem Ende stehen `CashExpenseAdded` und `EntryLinkedToYnab`, ein Push. Vor dem Fix rot, der Kommentar nennt den Bug (zweite Buchung durch einen Lauf zwischen Push und Verknüpfung).
- [ ] Kaputt (Erneut senden): dasselbe Muster für `retryPushToYnab`, vor dem Fix rot.
- [ ] Kaputt (Prüfvermerk): dasselbe Muster für `setReviewMark`, `restoreReviewMark`, `retryReviewMarkWriteBack` (mit offenem Fehlschlag), `setReviewMarkBatch` und `restoreReviewMarkBatch`, je ein Test, vor dem Fix rot. Der Kommentar nennt den Bug (zurückgedrehtes `approved`).
- [ ] Intakt: ohne laufenden Spiegel schreiben alle fünf geänderten Befehle ihre Ereignisse und pushen sofort; die bestehenden Tests in `LedgerRetryPushTests.fs`, `LedgerReviewMarkTests.fs`, `LedgerReviewMarkBatchTests.fs` und `BotApiTests.fs` bleiben ohne inhaltliche Änderung grün.
- [ ] Intakt: Ein Spiegel-Lauf nach einem erfolgreichen `addCashExpense` findet die Buchung über die YNAB-Id und legt keine zweite an (Handler-Test, falls noch nicht vorhanden).
- [ ] `triggerMirror` läuft erst nach Freigabe der Sperre: ein Test-Auslöser, der synchron einen gesperrten Lauf (`runMirrorGuarded`) startet und abwartet, blockiert `addCashExpense` und `retryPushToYnab` nicht (Test mit Frist, rot, wenn der Auslöser im gesperrten Abschnitt sitzt).
- [ ] Je heute gesperrter Aufrufstelle ohne Test (die acht Stellen aus "What") ein Sperr-Test nach dem Vorbild `LedgerCategoryChangeTests.fs:624`.
- [ ] Gegenprobe im Outcome: Mutantenliste "je eine Aufrufstelle von `WriteGuard.exclusive` entfernt" (alle 15 Stellen der Befehle), je Mutante der Test, der rot wird. Kein Satz "kein Überlebender" ohne diese Liste (Doktrin Punkt 3).
- [ ] Die Live-Verdrahtung ist belegt: `Server.Api.addManualTransaction`, `retryPushToYnab`, die Prüfvermerk-Felder in `Api.fs` und `BotApi.liveDeps.AddCashExpense` rufen die gesperrten Funktionen (Liste im Outcome, je Stelle Verhalten oder bestehender Test als Beleg, Doktrin Punkt 3).
- [ ] Kein Verschachteln: kein gesperrter Befehl ruft einen anderen gesperrten Befehl (`SemaphoreSlim` ist nicht wiedereintrittsfähig); Bobs `bookingGate` wird weiterhin vor der Schreib-Sperre genommen, nie umgekehrt. Steht als Kommentar an `WriteGuard`.
- [ ] README ledger (**Schreib-Sperre** und die genannten Key commands) und der Nachtrag an ADR 0055 sind geschrieben; README-Delta mit `npm run check:readme-delta -- --bc ledger` geprüft.
- [ ] Bob-Grenzen (ADR 0068) und Bobs Antworten sind unverändert (`BotApiTests.fs`, `BotValidationTests.fs` grün ohne Änderung).
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Nicht dazu
- Der Fall "Push gelungen, Schreiben der Verknüpfung wirft" (ledger-p3vq8). Die Sperre schließt nur das Zeitfenster gegen einen gleichzeitigen Lauf. Wirft `appendAndApply [ linked ]`, sieht der nächste Lauf nach Freigabe die YNAB-Buchung trotzdem ohne Partner. Das braucht die Entscheidung aus ledger-p3vq8 (ADR 0021).
- Der gemeinsame Barbuchungs-Befehl für Quick Add und Bob (ledger-bk3q8). Die Sperre sitzt hier in `addCashExpense`; bk3q8 baut darauf auf.
- Der Comdirect-Import-Push (`pushComdirectImports`, :872) und Force-Import: Sie tragen eine ImportId, der Spiegel findet ihre Buchungen über `MatchedByImportId` und legt keine zweite an. Kein Befund im Review.
- Doppelklick-Sperren im Client (Gruppe 5) und die Bool-Kennzeichen der Sammelaktionen.
- N+1 in den Sammelbefehlen (ledger-r8h19).

## Notes
- Mit ledger-p3vq8 verwandt, nicht zusammengelegt. Die Empfehlung aus `arch-ledger.json` sagt "im selben Zug modellieren", weil beide `addCashExpense` betreffen. p3vq8 braucht aber eine Modellierungsentscheidung zu ADR 0021 (YNAB-Id vorab festhalten, eigener Zustand "Push-Ergebnis unbekannt" oder Abgleich durch Erneut senden und Spiegel), diese Task ist ohne Entscheidung baubar. Beim Refinement von p3vq8 gilt: die Sperre aus dieser Task ist Voraussetzung, kein Ersatz.
- Folge für den Alltag: Quick Add, Bob und der Prüfvermerk warten im seltenen Fall auf einen laufenden Spiegel (einige Sekunden). Ein Wischen auf "Geprüft" während eines Laufs braucht dann sichtbar länger; das nimmt die gewählte Option in Kauf.
- Bob: `bookPreview` hält `bookingGate` und ruft darin `addCashExpense`. Die Reihenfolge `bookingGate` vor Schreib-Sperre ist eindeutig, kein Deadlock. Das Tageslimit wird weiter unter `bookingGate` geprüft.
- Testdoktrin: Handler-Ebene mit In-Memory-SQLite, Uhr als Parameter, eindeutige Ids, `testSequenced` wie im Vorbild (die Sperre ist prozessweit). Kein Browser-Test.
- Keine Änderung an `src/Shared`, also keine Fixture-Falle.
