---
id: ledger-f8bjm
title: "Ersatz-POST mit verlorener Antwort: Wiederherstellen, Split- und Betrags-Ersatz scheitern danach dauerhaft an 409, der Spiegel legt die Ersatzbuchung als neue Buchung „YNAB-Spiegel“ an; die Ersatz-import_id muss vor dem POST im Kassenbuch stehen (Ledger/CommandHandlers.fs)"
status: done
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

## Verifier note (iteration 1)

REASONS:
- Datenfehler auf dem neuen 409-Weg: eine erneute Änderung der Aufteilung bei offenem Ersatz wird als Erfolg gemeldet und die neuere Entscheidung danach still überschrieben. Ablauf: Aufteilung mit Stand A, Roman setzt B, DELETE gelingt, der POST landet in YNAB, Antwort NetworkError. Die Buchung hat `PendingReplacement` (r1) und einen offenen Kategorie-Fehlschlag. Roman setzt C (`changeSplits` sperrt diesen Fall nicht, `splitNotChangeableReason`). `pushSplitState` nimmt `pending.NewImportId` wieder, POSTet C mit r1, YNAB antwortet 409, weil B unter r1 steht, Ergebnis `ArrivedWithoutId -> CategorySent`: Roman sieht Erfolg. Danach schreibt der Spiegel in `importIdMatchEvents` `EntryCategorizationWrittenBackToYnab` ohne Inhaltsprüfung, `observe` läuft ohne offenen Fehlschlag, C weicht vom beobachteten B ab, „YNAB gewinnt“, BB übernimmt das alte B. Die Entscheidung C geht ohne Fehlschlag verloren. Kein Test deckt das.
- Doku dazu: der README_DELTA-Satz „Solange der Ersatz offen ist, schickt jeder Versuch dieselbe Ersatz-`import_id` … ein 409 darauf heißt angekommen“ und der Kommentar „YNAB hält jetzt den BB-Stand“ gelten nur, solange sich der Inhalt seit dem Sende-Ereignis nicht geändert hat.
- Neuer Nutzertext `entryDeleteReplacementPendingText` („…, bitte erneut senden oder den Spiegel abwarten“) verstößt gegen Styleguide §8 („Keine Höflichkeitsfloskeln“).

SUGGESTED_FIX: Eine geänderte Aufteilung bei offenem Ersatz nicht als angekommen melden. Vorgabe des Conductors (2026-11-12): sperren. Solange ein Ersatz der Art `Replace` offen ist, lehnt `changeSplits` (und jeder andere Weg, der Aufteilung oder Betrag ändert, `setEntrySplitsWithAmount`, `setEntryAmount` auf einer aufgeteilten Buchung) einen anderen Inhalt mit einem Satz ab, der sagt, was zu tun ist (Erneut senden oder den Spiegel abwarten); nur „Erneut senden“ mit dem gesendeten Inhalt läuft. Achte auf den Fall nach einer 400 (der Ersatz-POST wurde abgelehnt, YNAB kennt r1 nicht): dort darf die Buchung nicht hängen bleiben, ein neuer Inhalt muss danach möglich sein. Dafür den gesendeten Inhalt nicht neu erfinden; prüfe, ob eine 400 den offenen Ersatz beenden darf, und belege beides. Dazu ein Kaputt-Test „verlorene Antwort, Aufteilung erneut ändern, 409: kein falscher Erfolg, C bleibt erhalten bzw. wird abgelehnt“, rot vor dem Fix. Den Sperrtext beim Löschen ohne „bitte“ (etwa „Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten.“), README_DELTA nachziehen.

ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)

REASONS:
- Kriterien 2 und 3 gelten in der Produktion nicht, dazu ein neuer Datenfehler aus Iteration 2. Der echte Port des Ersatz-POST liefert nie `ImportIdAlreadyInYnab`: Split-Ersatz (`Api.fs:1156`) und Wiederherstellen (`Api.fs:599`) gehen über `YnabClient.createReplacement` -> `sendCreateTransaction` (`YnabClient.fs:2122`) -> `mapSplitStatus` (`YnabClient.fs:2052ff`), dort wird 409 zu `YnabError.InvalidResponse $"Conflict: {bodyText}"`; der bestehende Test `LedgerSplitWriteTests.fs:1334` („POST: … 409 als Fehler“) hält genau das fest. Nur `interpretCreateTransactionResponse` (Einzelbuchung) macht aus 409 `ImportIdAlreadyInYnab`. Die neuen Handler-Tests setzen `ImportIdAlreadyInYnab` am Fake-Port und prüfen einen Vertrag, den der Live-Adapter nicht hält.
- Folge beim Split-Ersatz: verlorene Antwort, dann „Erneut senden“: 409 -> `InvalidResponse "Conflict: …"` -> `singlePushArrival` gibt `PushFailedBecause` (Fehlschlag mit 409-Text) -> `replacementRejected` (`CommandHandlers.fs:2990`) ist true -> `EntryReplacementRejectedByYnab` beendet den offenen Ersatz. Weil `:r1` schon als ImportId-Verknüpfung steht, nimmt der nächste Versuch `:r2`, und der POST legt die Buchung in YNAB ein zweites Mal an. Das ist der normale Weg nach jeder verlorenen Antwort: Geld und Kontostand in YNAB falsch.
- Weitere Fehler, die als „abgelehnt“ zählen, obwohl YNAB nicht sicher nichts angelegt hat: ein 201 ohne lesbare Id (`InvalidResponse "Die Antwort nennt keine Id der neuen Buchung"`, `YnabClient.fs:2146`; Buchung steht unter r1, Ersatz endet, nächster Versuch mit r2) und Fehler vor dem POST bei einem Wiederholversuch mit offenem r1 (`getPayees` „Failed to parse payees“, `noTransferPayee`).
- Wiederherstellen: Kriterium 3 (zweites Rückgängig mit 409 gilt als angekommen) fällt in der Produktion aus demselben Grund; Roman sieht einen Fehler mit rohem Antworttext.
- Kosmetik: in `YnabClient.fs:326ff` stehen zwei `///`-Blöcke übereinander (alter Kommentar von `unreadableResponseOf` über `unreadableResponseText`).

SUGGESTED_FIX: 409 im Ersatz-POST auf `ImportIdAlreadyInYnab` abbilden (in `mapSplitStatus` oder `sendCreateTransaction`, gleiche Regel wie `interpretCreateTransactionResponse`), den Test bei `LedgerSplitWriteTests.fs:1334` anpassen und einen Test gegen das echte Antwort-Mapping hinzufügen (FakeHandler 409 durch `sendCreateTransaction`), rot vor dem Fix. `replacementRejected` so eng fassen, dass nur ein tatsächlich gesendeter POST mit Antwort 400/404 den offenen Ersatz beendet: kein 201 ohne Id, keine Fehler vor dem POST (Payees, Übertrags-Payee). Belegen: verlorene Antwort, Wiederholung mit echtem 409 (über das echte Mapping), danach kein `EntryReplacementRejectedByYnab` und kein POST mit `:r2`. Wo ein Handler-Test einen Port mit festgelegtem Vertrag nutzt, den Vertrag am Live-Adapter belegen (Doktrin Punkt 3: jede Stelle, die den Live-Port anschließt, per Verhalten).

ITERATION_HINT: likely-fixable

## Outcome

Befund bestätigt: die vier Kaputt-Tests der Iteration 1 waren vor dem Fix rot (Spiegel legte je eine neue Buchung an, „Erneut senden“ meldete den 409-Text als Fehlschlag).

Umsetzung Iteration 1 (Form mit dem `tactical-modeler` geklärt):
- Zwei neue Ereignisse `EntryReplacementSentToYnab` und `EntryRestoreSentToYnab` (Payload `OldYnabTransactionId`, `NewImportId`, Version 1, Actor `UserInBb`), geschrieben unter der Schreib-Sperre direkt vor dem Ersatz-POST, nur bei vorhandener `import_id` (`src/Server/Ledger/Events.fs`).
- Zustand `Entry.PendingReplacement` (`ReplacementKind` `Replace`/`Restore`), gesetzt von den Sende-Ereignissen, geleert von `EntryReplacedInYnab`/`EntryRestoredInBb`; `linksFrom` stiftet nur die Ersatz-`import_id` (`src/Server/Ledger/Domain.fs`). Neue Spalte `pending_replacement` (JSON) mit Migration; dieselbe `import_id`-Verknüpfung auf dieselbe Buchung ist beim Anhängen kein Konflikt mehr, jede andere Doppelung scheitert wie bisher (`src/Server/Ledger/Persistence.fs`).
- `pushSplitState`: bei offenem Ersatz dieselbe Ersatz-`import_id` und kein DELETE, 409 heißt angekommen (`singlePushArrival`), kein neuer Fehlschlag; der Spiegel wird nach Freigabe der Sperre angestoßen (`withMirrorAfterArrival`, neues Feld `SplitWriteTools.TriggerMirror`, in `src/Server/Api.fs` `triggerMirrorInBackground`). Gilt für `setEntrySplits`, `setEntrySplitsWithAmount`, `retryCategorizationOrSplitWriteBack`, `retryAmountWriteBack`.
- `restoreDeletedEntry`: Sende-Ereignis vor dem POST, ein 409 ist `Ok` und stößt den Spiegel an. `deleteEntry` lehnt bei offenem Ersatz ab.
- Spiegel: `importIdMatchEvents` schließt einen offenen Ersatz ab (`EntryReplacedInYnab` plus Ende offener Kategorie- und Betrags-Fehlschläge bzw. `EntryRestoredInBb`, Actor `YnabMirror`); `observe` vergleicht gegen den Stand nach dem Abschluss. `isSupersededDeletion` zählt die alte Id eines offenen Ersatzes als abgelöst (`src/Server/Ledger/CommandHandlers.fs`).
- Historie: „Ersatz an YNAB gesendet“, „Wiederherstellen an YNAB gesendet“ (`src/Server/Ledger/QueryHandlers.fs`).

Korrektur Iteration 2 (Verifier-FAIL: falscher Erfolg nach 409, Vorgabe des Conductors: sperren):
- Kaputt-Test „Kaputt: verlorene Antwort, Aufteilung erneut ändern; abgelehnt statt falscher Erfolg nach 409“ (`src/Tests/LedgerSplitWriteTests.fs`): vor der Korrektur rot mit „eine andere Aufteilung bei offenem Ersatz muss abgelehnt werden, war CategorySent“. Dazu „Kaputt: offener Ersatz ohne Fehlschlag, Betrag und Aufteilung ändern wird abgelehnt“ (`setEntrySplitsWithAmount` ohne Fehlschlag, ebenfalls vor der Korrektur rot).
- Sperre: `changeSplits` (also `setEntrySplits` und `setEntrySplitsWithAmount`) lehnt bei offenem Ersatz der Art `Replace` jeden anderen Inhalt ab, ohne Ereignis und ohne YNAB-Aufruf, mit `replacementPendingText` „Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten.“ (ohne „bitte“, Styleguide §8). Derselbe Inhalt gilt wie bisher als unverändert. Derselbe Text gilt beim Löschen (ersetzt `entryDeleteReplacementPendingText`). `setEntryAmount` auf einer aufgeteilten Buchung war schon gesperrt (`amountSplitRouteReason`), `setEntrySplitsWithAmount` mit offenem Fehlschlag ebenfalls (`amountSplitPendingReason`). „Erneut senden“ (`retryCategorizationOrSplitWriteBack`, `retryAmountWriteBack`) schickt den BB-Stand, der wegen der Sperre der gesendete ist.
- Nach einer Ablehnung: ein gesendeter Ersatz-POST, den YNAB mit 400 oder 404 ablehnt, heißt, YNAB hat unter der Ersatz-`import_id` nichts angelegt (Probelauf 3 aus ADR 0058 zeigt, dass nur eine angelegte Buchung ihre `import_id` sperrt; Einordnung seit Iteration 3 im Adapter, siehe unten). Dann endet der offene Split-Ersatz mit dem neuen Ereignis `EntryReplacementRejectedByYnab` (Payload wie die Sende-Ereignisse, Actor `UserInBb`, Historie „Ersatz von YNAB abgelehnt“, `apply` leert nur einen offenen `Replace` derselben `import_id`, sonst laut). Der Fehlschlag trägt die gelöschte alte Id, ein neuer Inhalt geht danach nur per POST mit der nächsten Ersatz-`import_id` (`:r2`). Eine unlesbare Antwort (auch ein 201 ohne Id), Netzfehler und 5xx lassen den Ersatz offen. Den gesendeten Inhalt erfindet BB nicht neu, er bleibt der BB-Stand. Belegt in „POST mit 400: YNAB hat abgelehnt, der offene Ersatz endet; eine neue Aufteilung geht mit :r2 ohne DELETE“ (auch Zustand gleich Replay) und „POST mit unlesbarer Antwort: YNAB kann angelegt haben, der offene Ersatz bleibt“. Der frühere 400-Test heißt jetzt „POST mit Netzfehler …“ und prüft mit `NetworkError` dasselbe wie vorher (dieselbe `import_id`, kein zweites DELETE).
- Domain: Codec-Rundreise, `linksFrom` (leer) und `apply` der Ablehnung im Test „Offener Ersatz: Codec-Rundreise, linksFrom und apply …“. Die Aufzählung aller Ereignisse in `src/Tests/LedgerReviewQueueTests.fs` bekam eine Ausgangsbuchung mit offenem Ersatz, weil die Ablehnung nur dort gilt.
- Client und Endpoint-Negativtabelle: kein neuer Endpoint, also keine neue Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`. Der Sperrsatz kommt als gewöhnliche Fehlermeldung von `setEntrySplits` bzw. `setEntrySplitsWithAmount` an und erscheint im Split-Editor als Speicherfehler (ledger-ng110), ohne neue Client-Logik; kein Client-Test nötig.

Tests der Iterationen 1 und 2: `src/Tests/LedgerSplitWriteTests.fs` (Kaputt Split-Ersatz, Kaputt Erneut senden mit 409, die zwei Kaputt-Tests der Sperre, Domäne mit Codec-Rundreise, `linksFrom` und `apply`, Replay von Zustand und Verknüpfungen, Lösch-Beobachtung bei offenem Ersatz ohne Fehlschlag, POST mit 400, POST mit unlesbarer Antwort, POST mit Netzfehler), `src/Tests/LedgerEntryDeletionTests.fs` (Kaputt Wiederherstellen, zweites Rückgängig mit 409, Wiederherstellen mit 400 und gleicher `import_id`, Löschsperre bei offenem Ersatz). Drei bestehende Log-Reihenfolge-Prüfungen (`LedgerSplitWriteTests` zwei, `LedgerAmountTests` eine) tragen `EntryReplacementSentToYnab` vor dem Ergebnis; sonst haben bestehende Tests nur das neue Feld in Entry- und `SplitWriteTools`-Konstruktionen bekommen. Skizzenvergleich entfällt (nur Server).

Korrektur Iteration 3 (Verifier-FAIL: der Live-Adapter hielt den Vertrag der Handler-Tests nicht):
- Befund: `YnabClient.createReplacement` -> `sendCreateTransaction` ging über `mapSplitStatus` und machte aus 409 `InvalidResponse "Conflict: …"`, aus einem 201 ohne Id `InvalidResponse "Die Antwort nennt keine Id der neuen Buchung"`. Die Handler-Tests setzten am Fake-Port `ImportIdAlreadyInYnab`. In der Produktion beendete `replacementRejected` deshalb nach verlorener Antwort und „Erneut senden“ den offenen Ersatz, und der nächste Versuch legte mit `:r2` doppelt an; ein zweites Rückgängig mit 409 meldete den rohen Antworttext als Fehler.
- Adapter (`src/Server/YnabClient.fs`): neue reine Zuordnung `interpretReplacementResponse` für den Ersatz-POST, `sendCreateTransaction` nutzt sie statt `mapSplitStatus`. 200/201 mit Id ist Erfolg, ohne Id unlesbar; 409 wird wie bei `interpretCreateTransactionResponse` `ImportIdAlreadyInYnab`; 400 und 404 bekommen eigene feste Sätze („Ersatzbuchung ungültig (HTTP 400)“, „Budget oder Konto der Ersatzbuchung nicht gefunden (HTTP 404)“), der Rumpf geht nur ins Log und nicht mehr in `events`; 401, 429 und sonstige Status wie bisher bzw. über `statusFailureOf`. `replacementRejectedByYnab` erkennt genau diese zwei Ablehnungen. `mapSplitStatus` bleibt für PUT und DELETE unverändert.
- `replacementRejected` (`src/Server/Ledger/CommandHandlers.fs`) liest nur noch `Ynab.replacementRejectedByYnab`: ein 409, ein 201 ohne Id, Netzfehler, 5xx, 401, 429 und Fehler vor dem POST (Payees, Übertrags-Payee, fehlende Settings in `Api.splitWriteTools`) lassen den offenen Ersatz offen. Im Zweifel schließt der Spiegel ab.
- Jede Stelle, die den Live-Port anschließt, geht über `createReplacement` -> `sendCreateTransaction`: Split-Ersatz `Api.splitWriteTools` (`Create = YnabClient.createReplacement token budget`) und Wiederherstellen `Api.liveEntryDeletionPorts` (`Create = YnabClient.createReplacement`). Beide bekommen die neue Zuordnung, ohne dass `Api.fs` sich in dieser Iteration ändert. Die Ersatzwerte ohne Settings (`Unauthorized`, `BudgetNotFound`) zählen nicht als Ablehnung.
- Tests am echten Mapping: die Probes in `src/Tests/LedgerSplitWriteTests.fs` und `src/Tests/LedgerEntryDeletionTests.fs` haben `CreateHttp`; ist es gesetzt, geht der POST über `Ynab.buildReplacementBody` und `Ynab.sendCreateTransaction` mit einem `FakeHandler`, der Handler sieht also genau den Fehler des Live-Ports. Darüber laufen jetzt „Kaputt (ledger-f8bjm): Erneut senden nach verlorener Antwort, 409 heißt angekommen“ (zusätzlich geprüft: kein `EntryReplacementRejectedByYnab`, der offene Ersatz bleibt, ein weiterer Versuch schickt wieder `:r1`, kein `:r2`), „Kaputt (ledger-f8bjm): ein zweites Rückgängig nach verlorener Antwort, 409 heißt angekommen“, die Sperr-Tests mit 409, „POST mit 400 …“ und „POST mit unlesbarer Antwort …“ (jetzt ein echtes 201 ohne Id). Gegen den Stand vor der Korrektur (Produktionsdateien der Iteration 2 eingesetzt) waren rot: „Erneut senden … 409“ (CategorySendFailed statt CategorySent), „zweites Rückgängig … 409“ („YNAB hat abgelehnt: Conflict: …“), „POST: Methode, Pfad, neue Id aus der Antwort, 409 heißt YNAB kennt die import_id“ (früher „409 als Fehler“, erwartet jetzt `ImportIdAlreadyInYnab`), der neue Adaptertest „POST einer Ersatzbuchung (ledger-f8bjm): nur ein gesendeter POST mit 400 oder 404 gilt als abgelehnt“ (409 galt als abgelehnt) und „POST mit unlesbarer Antwort“ (das 201 ohne Id beendete den Ersatz).
- Aufgeräumt: die zwei gestapelten `///`-Blöcke über `unreadableResponseText` in `YnabClient.fs` sind weg; die Konstante ist nicht mehr nötig, `unreadableResponseOf` steht wieder wie auf `main`.
- `./verify.sh`: VERIFY: PASS (dotnet 4880, node 318).

Nachtrag für ADR 0058 (zu Punkt 2, 3 und 5), 2026-11-12, ledger-f8bjm:
Die Ersatz-`import_id` steht vor dem POST im Kassenbuch (`EntryReplacementSentToYnab`, Actor `UserInBb`, nach gelungenem DELETE) und bleibt bis zum Abschluss die einzige gültige Ersatz-Id der Buchung (offener Ersatz, `Entry.PendingReplacement`). Solange der Ersatz offen ist, lehnt BB jede andere Aufteilung, jeden anderen Betrag und das Löschen ab („Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten.“), damit der gesendete Inhalt der BB-Stand bleibt. Ein 409 auf den Ersatz-POST heißt dann angekommen (wie ledger-xqqnv): kein Fehlschlag, Spiegel anstoßen. Die Zuordnung der Antwort hält der Adapter (`interpretReplacementResponse`): 409 wird dort `ImportIdAlreadyInYnab` wie bei der Einzelbuchung. Der Spiegel ordnet die Ersatzbuchung über die `import_id` zu und schreibt `EntryReplacedInYnab` mit Actor `YnabMirror`, das beendet auch die offenen Fehlschläge. Nur wenn YNAB einen gesendeten Ersatz-POST mit 400 oder 404 ablehnt (`replacementRejectedByYnab`), endet der offene Ersatz mit `EntryReplacementRejectedByYnab`; der nächste Versuch nimmt die nächste Ersatz-`import_id`. Alles andere lässt ihn offen (409, 201 ohne Id, Netzfehler, 5xx, 401, 429, Fehler vor dem POST), weil eine neue Ersatz-`import_id` in YNAB doppelt anlegen könnte; im Zweifel schließt der Spiegel ab. Eine Lösch-Beobachtung der alten Id bleibt bei offenem Ersatz ohne Wirkung. Ohne `import_id` gibt es kein Sende-Ereignis, die Lücke aus ADR 0021 (Quick Add) bleibt dort hingenommen.

Nachtrag für ADR 0092 (zu Punkt 2 und 3), 2026-11-12, ledger-f8bjm:
Rückgängig schreibt vor dem POST `EntryRestoreSentToYnab` mit der Ersatz-`import_id`. Das ergänzt „YNAB zuerst“ für das DELETE und kehrt es nicht um. Geht die Antwort verloren oder kommt ein 409, bleibt die Buchung gelöscht, bis der Spiegel die neue Buchung über die `import_id` findet und `EntryRestoredInBb` mit Actor `YnabMirror` schreibt (Historie „In BB wiederhergestellt“ mit Herkunft Spiegel, darüber „Wiederherstellen an YNAB gesendet“ mit Herkunft BB). Ein zweites Rückgängig mit 409 (vom Adapter als `ImportIdAlreadyInYnab` gemeldet) meldet Erfolg und stößt den Spiegel an; ein 400 lässt das Wiederherstellen offen, ein weiteres Rückgängig nimmt dieselbe `import_id`. Für den Betrags-Ersatz einer aufgeteilten Buchung (Punkt 3) gilt die Sperre aus dem Nachtrag zu ADR 0058: bei offenem Ersatz kein anderer Betrag.

Für die Kann-Sammlung ledger:
- Memo, Flag, Prüfvermerk und Payee schreiben bei offenem Split-Ersatz weiter auf die alte, in YNAB gelöschte Id (`tryYnabTransactionId`); das galt schon vorher für den halben Split-Ersatz.
- Ein offener Ersatz ohne Kategorie-Fehlschlag (Absturz zwischen Sende-Ereignis und Fehlschlag) lässt sich nicht per „Erneut senden“ wiederholen, nur der Spiegel schließt ihn ab; Aufteilung und Betrag sind solange gesperrt.
- Der Lesepfad (`splitLocksOf`, `AmountLockReason`) kennt die Sperre bei offenem Ersatz nicht eigens: der Split-Editor lässt die Änderung zu, der Server lehnt sie mit dem Satz ab.
- Kommt nach verlorener Antwort beim nächsten Versuch mit demselben Inhalt ein 400 (YNAB müsste den Inhalt dann anders werten als beim ersten Mal), endet der Ersatz, obwohl die Buchung unter `:r1` in YNAB stehen kann; der nächste Versuch mit `:r2` legte sie dort ein zweites Mal an.
- Die Key-events-Bullets zu *EntryReplacedInYnab* und *EntryDeletedInBb* / *EntryRestoredInBb* nennen nur Actor `UserInBb`; seit dieser Task schreibt auch der Spiegel beide (im neuen Bullet erwähnt).
- `amountSplitPendingReason` enthält noch „bitte zuerst erneut senden“ (Styleguide §8), älter als diese Task.
