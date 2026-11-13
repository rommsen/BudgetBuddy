---
id: ledger-ay81j
title: "In BB erfasste Buchungen gehen ohne import_id an YNAB, der Spiegel verknüpft offene Erfassungen nach Konto, Tag, Betrag und Payee"
status: done
type: bug
context: ledger
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [laufend, money, ynab-sync]
related_adrs: [0004, 0015, 0021, 0058, 0066, 0092]
related_research: []
prior_art: [ledger-xqqnv, ledger-f8bjm, ledger-nkwqb, ledger-nnd2f, ledger-p3vq8]
---

## Why
Probelauf zu ledger-nkwqb (2026-11-13): Bob buchte eine Barzahlung. YNAB glich die Buchung mit einer eine Woche älteren,
von Hand erfassten Zahlung gleichen Payees und Betrags ab und verbarg die BB-Buchung (404, nie als gelöscht gemeldet, die
überlebende trägt `matched_transaction_id`). In YNAB fehlte eine echte Zahlung, der Bar-Stand war zu hoch. Ursache: BB
schickt Barbuchungen (Quick Add, Bob, Neue Buchung) mit `import_id` `BB:<EntryId:N>` (ADR 0004 Nachtrag 2026-11-08).
YNAB behandelt jede Buchung mit `import_id` wie einen Bankimport und gleicht sie in einem Fenster von etwa 10 Tagen mit
Handbuchungen gleichen Betrags ab. Wiederkehrende Barzahlungen gleicher Höhe trifft das immer wieder. ledger-nnd2f hält BB
richtig, YNAB bleibt falsch, bis Roman den Abgleich von Hand aufhebt. Roman: „Ich verstehe überhaupt nicht, warum YNAB
die zusammenlegt, wenn die von mehreren Tagen kommt. Das macht gar keinen Sinn.“

Entschieden von Roman (2026-11-13), ausgearbeitet mit `tactical-modeler`:
1. In BB erfasste Buchungen und ihre erneuten Sendungen gehen ohne `import_id` an YNAB. Comdirect-Importe behalten sie
   (Dubletten, Zusammenlegung von Überträgen).
2. Ersatz für die Absicherung einer verlorenen Antwort (409 heißt angekommen, ledger-xqqnv): Der Spiegel verknüpft eine
   neue YNAB-Beobachtung ohne `import_id` mit gleichem Konto, Tag, Betrag und Payee mit der offenen BB-Buchung, statt eine
   neue Buchung anzulegen. „Erneut senden“ lässt vorher den Spiegel laufen und kann so nicht doppelt buchen.
3. Kandidatin ist jede offene Erfassung, auch mit offenem Fehlschlag, weil eine Zeitüberschreitung heute als Fehlschlag im
   Log steht. Restrisiko angenommen: eine nicht gesendete Zahlung und eine am selben Tag von Hand in YNAB erfasste zweite,
   gleiche Zahlung (Konto, Betrag, Payee) würden verknüpft.
4. Ersatz und Wiederherstellen einer Erfassung mit verlorener Antwort legen im Spiegel eine zweite Buchung an, wie heute bei
   Buchungen ohne `import_id`; die Ausdehnung der Regel ist ein Kann-Punkt.

## What

### Begriffe
- **Erfasste Buchung:** Ihr erstes Ereignis ist `CashExpenseAdded` (Quick Add, Bob, Neue Buchung, jeder Actor). Den
  Ursprung liefert `Ledger.Persistence.loadEntryOrigin` (`Persistence.fs:2066`). Eine reine Regel `isCapturedInBb`
  entscheidet. Sie ist die einzige Stelle, die über die `import_id` beim Senden entscheidet.
- **Offene Erfassung:** eine erfasste Buchung mit `LinkedToYnab = false`, `DeletedInYnab = false` und
  `PossibleDuplicate = None`. Ein offener Fehlschlag (`EntryPushToYnabFailed`) schließt sie nicht aus, denn eine
  Zeitüberschreitung steht heute als Fehlschlag im Log (`YnabClient.fs:1250-1257` fängt die Ausnahme,
  `singlePushArrival` macht daraus `PushFailedBecause`).

### 1. Schreibwege

| Weg | Herkunft | Heute | Neu |
|---|---|---|---|
| Quick Add, erster Push (`addCashExpenseWith`, `CommandHandlers.fs:1105`, `Api.fs:433`) | erfasst | `BB:<id>` | ohne `import_id`, `CashExpenseAdded.ImportId = None` |
| Bob-Commit (`BotApi.fs:405`, `:567`) | erfasst | `BB:<id>` | ohne |
| Neue Buchung, auch mit Übertrag (`CommandHandlers.fs:2513`, `Api.fs:514`) | erfasst | `BB:<id>` | ohne |
| Erneut senden (`retryPushToYnab`, `pushEntryAndRecord`, `planEntryPush` `:2955`) | erfasst | `BB:<id>` (alte ohne) | ohne, auch bei alter `BB:`-Verknüpfung. Vorher der Spiegel (Punkt 3) |
| Erneut senden, Ist neu (`confirmEntryIsNew`), Sammelpush, `SendImportAgain` | Comdirect | `BB:<Reference>` | unverändert, 409 heißt weiter angekommen |
| Split- und Betrags-Ersatz, auch `retryAmountWriteBack`, `retryCategorizationWriteBack` (`:3294-3331`) | erfasst | `BB:<id>:r<n>` | ohne, dann auch kein `EntryReplacementSentToYnab`. Ein schon offener Ersatz (`PendingReplacement`) sendet weiter mit `pending.NewImportId` |
| Split- und Betrags-Ersatz | Comdirect | `:r<n>` (`replacementImportId` `:2988`) | unverändert |
| Wiederherstellen nach Löschen, ADR 0092 (`restoreRequestOf` `:3681`, `:3789-3808`) | erfasst | `BB:<id>:r1` | ohne, kein `EntryRestoreSentToYnab`. Ein offener Restore behält `pending.NewImportId`. `EntryDeletedInBb` hält die echte `import_id` weiter als Historie fest |
| Wiederherstellen | Comdirect | `:r<n>` | unverändert |

Die Push-Ports der erfassten Buchungen verlieren den `import_id`-Parameter (`addCashBooking`/`addCashBookingWith`,
`LedgerBookingTools.Push`, `QuickAddPorts.Push`, `LedgerBookingPorts.Push`, `BotDeps.Push`). Der Typ verhindert dann eine
`import_id` beim ersten Push. `cashEntryImportId` fällt weg, `ImportIdPrefix` bleibt für Comdirect. `planEntryPush`
bekommt den Ursprung als Parameter, `loadEntryPushPlan` (`:2963`) lädt dazu die Ereignisse einmal.

### 2. Spiegel: dritte Match-Stufe „offene Erfassung“
- `EntryMatch` (`:216`) bekommt den Fall `MatchedByOpenPush of EntryId`. `resolveEntry` (`:317`) prüft in dieser
  Reihenfolge: YNAB-Id, ImportId, offene Erfassung, sonst `NewEntry`.
- Reine Planung in `Ledger.Domain` (etwa `planOpenPushLinks`), aufgerufen einmal vor der Schleife in
  `MirrorBatchState.load` (neues Feld `OpenPushLinks: Map<string, EntryId>`, `load` bekommt `resolution`). Die offenen
  Erfassungen kommen aus einer festen Abfrage, nie aus einer Abfrage je Buchung.
- Zulässig ist eine Beobachtung mit `Deleted = false`, `ImportId = None` und `MatchedYnabTransactionId = None`, deren
  YNAB-Id BB weder über `ynab` noch über `ynabsuperseded` kennt.
- Eine Buchung passt zu einer Beobachtung, wenn alles gleich ist:
  - Konto (über `resolution` aufgelöst)
  - Tag (`Date.Date`)
  - Betrag (decimal, ohne Vergleich der Währung)
  - Payee über `Shared.Domain.payeeKey` (getrimmt, Kleinschreibung, leer gleich `None`), und beide ohne Übertrag. Bei
    einem Übertrag zählt stattdessen das Zielkonto (`TransferAccountId`).

  Kategorie und Memo zählen nicht.
- Verknüpft wird nur ein Paar, bei dem die Beobachtung genau eine Kandidatin hat und die Buchung genau eine passende
  Beobachtung im Lauf. Sonst gibt es keine Verknüpfung: Die Beobachtung wird wie heute eine neue Buchung, die Erfassung
  bleibt offen, und es kommt eine Log-Zeile:
  - „Offene Erfassung nicht verknüpft: mehrere Buchungen in BB passen zu YNAB-Buchung %s“
  - „Offene Erfassung nicht verknüpft: mehrere YNAB-Buchungen passen zu Buchung %O“
- Ereignis: `EntryLinkedToYnab { YnabTransactionId }` mit Actor `YnabMirror`, wie im Standardzweig von
  `importIdMatchEvents`. Es beendet einen offenen Fehlschlag. `observe` vergleicht danach gegen den Stand nach der
  Verknüpfung, wie bei `MatchedByImportId` (`:647-662`). `survivorBeforeRun` (`:541`) bekommt den Fall der
  Vollständigkeit halber.
- Die Planung der Zusammenlegung (ledger-nkwqb, `planMergesObservedInYnab`) und die Ablehnung aus ledger-nnd2f bleiben
  unverändert. Beobachtungen mit `matched_transaction_id` sind ausgeschlossen.

### 3. Erneut senden einer offenen Erfassung
In `retryPushToYnab` kommen zwei neue Ports dazu: `origin` und `runMirrorNow` (live `mirrorYnabBudget`).
1. Ohne Sperre lesen. Die bisherigen Ablehnungen bleiben.
2. Bei einer erfassten Buchung `runMirrorNow ()` **außerhalb** von `WriteGuard`, weil der Lauf dieselbe Sperre nimmt.
   Scheitert der Lauf: `Error`, kein Push, kein Ereignis. Der Text erscheint im bestehenden Toast „Noch nicht in YNAB: …“:
   „BB konnte YNAB gerade nicht abfragen. Nichts gesendet, bitte später erneut senden.“
3. Unter `WriteGuard` neu laden. Ist die Buchung jetzt verknüpft: `Ok PushToYnabSent` ohne Push. Sonst ein Push ohne
   `import_id`.
4. Comdirect-Buchungen laufen ohne Spiegel wie heute.

### 4. Bob
Verhalten, Texte und Grenzen (ADR 0068) bleiben. Der Fall „409, also `pushed_to_ynab: true`“ entsteht für neue
Bob-Buchungen nicht mehr. Bei verlorener Antwort meldet der Commit `pushed_to_ynab: false` mit `push_error`. Nach dem
nächsten Spiegel-Lauf ist die Buchung verknüpft. Ein erneuter Commit derselben `preview_id` liest den Stand aus dem
Kassenbuch (`bookedFromLedger`, `BotApi.fs:366-379`) und pusht nicht.

### 5. Bestand
Nichts zu migrieren. Alte Erfassungen behalten ihre `importid`-Verknüpfung `BB:<id>`. Eine noch offene, die mit
`import_id` angekommen ist, findet der Spiegel weiter über `MatchedByImportId`.

## Acceptance criteria
- [ ] Quick Add, Bob-Commit und Neue Buchung (auch mit Übertrag) senden ohne `import_id`. Belegt ist das am Request-Rumpf
      des echten `YnabClient`-Adapters mit FakeHandler. Der Test war vor dem Fix rot.
- [ ] Erneut senden, Split- und Betrags-Ersatz und Wiederherstellen einer erfassten Buchung senden ohne `import_id`, auch
      bei alter `BB:`-Verknüpfung, und schreiben kein `EntryReplacementSentToYnab` oder `EntryRestoreSentToYnab`. Ein schon
      offener Ersatz oder Restore sendet mit `pending.NewImportId`.
- [ ] Comdirect-Wege (Erneut senden, Ist neu, Sammelpush, Ersatz, Wiederherstellen) senden unverändert mit `import_id`.
      409 und `duplicate_import_ids` heißen dort weiter angekommen.
- [ ] Verlorene Antwort: Der Push einer Quick-Add-Buchung scheitert mit Netzfehler, YNAB hat sie aber angelegt. Der
      nächste Spiegel-Lauf schreibt `EntryLinkedToYnab` (Actor `YnabMirror`) an dieser Buchung, legt keine zweite an und
      beendet den Fehlschlag. Dasselbe gilt für `PushOutcomeUnknown`.
- [ ] Die Regel aus What 2 gilt Kriterium für Kriterium, mit je einem Gegenfall. Payee ohne Rücksicht auf Groß- und
      Kleinschreibung und Leerraum. Bei einem Übertrag zählt das Zielkonto.
- [ ] Uneindeutig (zwei Kandidatinnen oder zwei Beobachtungen im Lauf): keine Verknüpfung, die Log-Zeile aus What 2, die
      Erfassung bleibt offen.
- [ ] Eine alte offene Erfassung mit `BB:`-Verknüpfung wird weiter über die ImportId verknüpft.
- [ ] Erneut senden einer offenen Erfassung: Verknüpft der Spiegel, gibt es keinen Push und `Ok PushToYnabSent`. Scheitert
      der Spiegel, kommt kein Push, kein Ereignis und der Satz aus What 3. Findet er nichts, gibt es genau einen Push ohne
      `import_id`. Comdirect ruft den Spiegel nicht auf. Die Live-Verdrahtung (`Api.fs`, `mirrorYnabBudget`) ist per
      Verhalten belegt.
- [ ] Bob: Texte und Grenzen unverändert, `pushed_to_ynab` wie in What 4.
- [ ] README-Delta deckt die Bullets aus Notes ab, die ersetzten Begriffe sind per grep nachgezogen. Die ADR-Nachträge zu
      0004, 0015, 0021, 0058, 0066 und 0092 sind beim Verfeinern geschrieben (2026-11-13); weicht die Umsetzung davon ab,
      steht die Abweichung im OUTCOME.
- [ ] `./verify.sh` meldet `VERIFY: PASS`.

## Notes

### Testplan (Doktrin: Regressionstest vor dem Fix rot)
Vorab ein Umbau ohne Verhaltensänderung: `YnabClient.createTransactionFor` baut heute selbst `new HttpClient()`
(`YnabClient.fs:1240`). Es bekommt eine Fassung mit injiziertem Client (etwa `createTransactionOn`). Die Live-Ports
werden Fabriken (`quickAddPortsOn http`, `ledgerBookingPortsOn http`, `BotApi.liveDepsOn zone http`, `entryPushWith …`),
`live*` ist die Fabrik mit dem echten Client. Fake-Ports allein belegen den Live-Adapter nicht.
1. Regression in `src/Tests/LedgerCashBookingTests.fs`, mit FakeHandler nach dem Vorbild in
   `LedgerTransferTests.fs:584`: Quick Add, Neue Buchung (auch mit Übertrag), Bob-Commit und Erneut senden einer alten
   Erfassung haben keinen Schlüssel `import_id` im Rumpf. Der intakte Fall: Comdirect Erneut senden behält
   `BB:<Reference>`. Die bisherigen Tests, die `import_id` verlangen (etwa `:485`, `:619`), werden ersetzt.
2. Reine Regel: neue Datei `src/Tests/LedgerOpenPushLinkTests.fs`, ein Treffer und je Kriterium ein Gegenfall,
   Mehrdeutigkeit in beide Richtungen.
3. Verlorene Antwort, erst kaputt, dann intakt: Liste `lostPushAnswerTests` (`:678`) umbauen. Die Fälle Netzfehler und
   `PushOutcomeUnknown`, über `runMirror` in-memory.
4. Bestand: alte offene Erfassung mit `BB:` wird über die ImportId verknüpft.
5. Uneindeutig im Lauf: `LedgerMirrorBatchTests.fs`.
6. Erneut senden, Fälle (a) bis (d) aus What 3: `LedgerRetryPushTests.fs`.
7. Ersatz: `LedgerSplitWriteTests.fs`. Erfasst gibt `ImportId = None`, ein offener Ersatz sendet mit `NewImportId`,
   Comdirect gibt `:r1`.
8. Wiederherstellen: `LedgerEntryDeletionTests.fs`, erfasst und Comdirect.
9. Comdirect-Sammelpush: Die bestehenden Tests in `BankImportTests.fs` und `YnabClientTests.fs` decken ihn ab.

Fixtures: `BotDeps.Push` ändert seinen Typ, also `BotApiTests.fs` und `LedgerCashBookingTests.fs:571-584` mitziehen. Kein
Feld in `src/Shared` ändert sich, die Playwright-Fixtures sind nicht betroffen.

### README-Bullets (`contexts/ledger/README.md`)
- **Feste import_id (Barbuchung)** wird **Versand ohne import_id (in BB erfasst)**.
- Neu: **Offene Erfassung**, mit der Regel aus What 2.
- **Fremd-Id-Verknüpfung** und **Spiegel-Lauf**: Match-Reihenfolge YNAB-Id, ImportId, offene Erfassung.
- **Versand-Ausgang**: „angekommen ohne Id“ gilt nur noch für Comdirect und Ersatz mit `import_id`. Dazu ein Satz zur
  Zeitüberschreitung.
- **Erneut senden (Noch nicht in YNAB)**, **Bot-Commit**, **Buchung erfassen (Neue Buchung)**, Key command
  *BuchungÜbernehmen*: 409 und feste `import_id` raus, Spiegel vorab.
- **Offener Ersatz**, **Buchung löschen (im Detail)**, **Betrag bearbeiten**: bei einer erfassten Buchung ohne
  `import_id`.
- **In YNAB zusammengelegt**: „auch BBs Barbuchungen“ gilt nur für die, die vor ledger-ay81j gesendet wurden.
- **Nachgetragene YNAB-Verknüpfung**: auch über die offene Erfassung.
- Per grep nachziehen: `cashEntryImportId`, „feste import_id“, `409`, `ImportIdAlreadyInYnab`.

### Kommentare
`ArrivedWithoutId` und den 409-Zweig nicht entfernen, nur die Kommentare an `PushArrival` (`:878-886`) und
`addCashExpenseWith` (`:929-949`, `:1009`) anpassen.

### Bekannte Grenzen
- Ersatz oder Wiederherstellen einer Erfassung mit verlorener Antwort: Der Spiegel legt eine Buchung „YNAB-Spiegel“ an,
  wie heute bei Buchungen ohne `import_id` (Roman, 2026-11-13: angenommen, Kann-Punkt).
- Alte Barbuchungen mit `import_id` in YNAB kann YNAB im Fenster noch abgleichen. ledger-nnd2f hält BB richtig.

### Kann-Punkte
In die Kann-Sammlung ledger-k4n2s übernommen (Punkte 108 bis 112).

## Verifier note (iteration 1)
REASONS:
- Kriterium „README-Delta deckt die Bullets aus Notes ab, die ersetzten Begriffe sind per grep nachgezogen“ nicht erfüllt: Der Bullet **Schreib-Sperre** (README :232) sagt weiter „neue Barbuchungen fängt seit ledger-p3vq8 zusätzlich die feste `import_id` ab“. Das ist nach dieser Änderung falsch. Kein Delta-Op deckt ihn ab, die Hand-Liste im OUTCOME nennt ihn nicht, die Vollständigkeitsaussage im OUTCOME stimmt nicht.
- Sonst geprüft und in Ordnung: Gate PASS, Delta-Grammatik Exit 0, Comdirect-Wege unverändert, Bob-Grenzen unverändert, Bob-Abweichung (gecachtes Ergebnis bei erneutem Commit) als dokumentiert akzeptiert.
SUGGESTED_FIX: `replace`-Op für **Schreib-Sperre** ergänzen (seit ledger-ay81j fängt der Abgleich **Offene Erfassung** sie ab, alte Erfassungen weiter über die ImportId). Danach README komplett per grep nach „feste import_id“, `409`, `cashEntryImportId`, `ImportIdAlreadyInYnab`, `createTransactionWith` durchsuchen und die Vollständigkeitsaussage im OUTCOME korrigieren. Keine Code-Änderung.
ITERATION_HINT: likely-fixable

## Verifier note (iteration 2)
REASONS:
- FAIL-Grund aus Iteration 1 behoben (Schreib-Sperre). Code unverändert, Gate aus Iteration 1 gilt.
- Weiter nicht erfüllt: „die ersetzten Begriffe sind per grep nachgezogen“. Key command *PushErneutSenden* sagt weiter „lädt die Buchung und pusht sie … (trägt die Buchung eine `import_id`, geht sie mit, sonst keine)“. Falsch: eine erfasste Buchung lässt vorher den Spiegel laufen und geht ohne `import_id`, auch mit alter `BB:`-Verknüpfung.
- Nachrangig: Key events *EntryReplacedInYnab* („neue `import_id`“) und *EntryDeletedInBb / EntryRestoredInBb* („stiftet … die neue `import_id`“) brauchen den Zusatz, dass eine erfasste Buchung keine neue `import_id` bekommt.
SUGGESTED_FIX: Hand-Liste um *PushErneutSenden* ergänzen, Key events qualifizieren, README Satz für Satz gegen jeden ersetzten Bullet greppen. Keine Code-Änderung.
ITERATION_HINT: likely-fixable

## Outcome

In BB erfasste Buchungen (erstes Ereignis `CashExpenseAdded`) gehen ohne `import_id` an YNAB, auf allen Wegen; der Spiegel verknüpft eine offene Erfassung als dritte Match-Stufe; „Erneut senden“ lässt für sie vorher den Spiegel laufen. Comdirect-Wege sind unverändert.

- Einzel-Post mit injiziertem Client: `YnabClient.createTransactionOn http …` (Token je Anfrage im Header), `createTransactionFor` nutzt `YnabClient.sharedHttpClient`; `createTransactionWith`/`createTransactionWithFlag` sind entfallen (`src/Server/YnabClient.fs`).
- Live-Ports als Fabriken: `Server.Api.quickAddPortsOn`, `Server.Api.ledgerBookingPortsOn`, `BotApi.liveDepsOn` mit `BotApi.botPushOn`; `live*` ist die Fabrik mit dem gemeinsamen Client. Die Push-Ports der Erfassung haben keinen `import_id`-Parameter mehr (`addCashBookingWith`, `addCashBooking`, `addQuickAdd`, `LedgerBookingTools.Push`, `QuickAddPorts.Push`, `LedgerBookingPorts.Push`, `BotDeps.Push`). `cashEntryImportId` ist entfallen, `CashExpenseAdded.ImportId = None`.
- Reine Regeln in `src/Server/Ledger/Domain.fs`: `isCapturedInBb`, `isOpenForPushLink`, `isOpenPushLinkCandidate`, `matchesOpenCapture`, `planOpenPushLinks` (`OpenPushLinkPlan`).
- `planEntryPush accounts origin links events entry` (erfasst: keine `import_id`), `loadEntryPushPlan` lädt das Log einmal. `replacementImportIdFor` (offen vor erfasst vor `replacementImportId`) für Split- und Betrags-Ersatz (`pushSplitState`) und `restoreRequestOf capturedInBb …`; für eine Erfassung kein `EntryReplacementSentToYnab`/`EntryRestoreSentToYnab`.
- Spiegel (`src/Server/Ledger/CommandHandlers.fs`): `EntryMatch.MatchedByOpenPush`, `resolveEntry` prüft YNAB-Id, ImportId, offene Erfassung (nur solange die Buchung im Lauf noch offen ist); `MirrorBatchState.load observations resolution` lädt die offenen Erfassungen über eine feste Abfrage (`Ledger.Persistence.loadOpenCaptures`, zwei Abfragen je Lauf), die abgelösten Ids der zulässigen Beobachtungen gebündelt, und schreibt die beiden Log-Zeilen aus What 2. `openPushMatchEvents` (EntryLinkedToYnab, Actor `YnabMirror`), `survivorBeforeRun` kennt den Fall.
- `retryPushToYnab now origin runMirrorNow push triggerMirror entryId`: Ablehnungen ohne Sperre, bei einer Erfassung `runMirrorNow` außerhalb von `WriteGuard` (Fehler oder Ausnahme: `Error retryMirrorUnavailableText`, kein Push, kein Ereignis), dann unter der Sperre neu laden; verknüpft: `Ok PushToYnabSent` ohne Push und ohne Auslöser. Live: `Server.Api.retryPushToYnabWith entryPush Ledger.CommandHandlers.mirrorYnabBudget`.
- Kommentare an `PushArrival`, `addCashExpenseWith`, `addCashBooking*` nachgezogen, `ArrivedWithoutId` und der 409-Zweig bleiben.

Tests (`./verify.sh`: VERIFY: PASS, dotnet 4985 grün, node 322 grün), alle Daten erfunden:
- Testplan 1, `src/Tests/LedgerCashBookingTests.fs` „Ledger Versand ohne import_id (echter Einzel-Post, ledger-ay81j)“ mit Fake-Handler (`PostRecorder`) am echten Adapter: Quick Add über `quickAddPortsOn`, Neue Buchung mit und ohne Übertrag über `ledgerBookingPortsOn`, Erneut senden einer alten Erfassung mit `BB:`-Verknüpfung über `entryPushWith … (createTransactionOn http)`, intakt Comdirect Erneut senden mit `BB:<Reference>` ohne Spiegel. Bob-Commit über `botPushOn` im Test „Bobs Commit pusht den Request …“. Pure Planung: „eine in BB erfasste Buchung geht ohne import_id, auch mit alter BB:-Verknüpfung“. Ersetzt: der Quick-Add-Test mit fester `import_id` und die Bob-Prüfung auf `BB:<EntryId>`; `QuickAddTests.fs` prüft den Rumpf jetzt mit und ohne `import_id`.
- Testplan 2, neue Datei `src/Tests/LedgerOpenPushLinkTests.fs` (13 Fälle): Treffer, je Kriterium ein Gegenfall (Payee, Konto, Tag, Betrag ohne Währung, Übertrag mit Zielkonto in drei Richtungen, gelöscht, mit `import_id`, mit Abgleich, bekannte Id), Mehrdeutigkeit in beide Richtungen, zwei Paare im Lauf, `isCapturedInBb` je Actor, `isOpenForPushLink`.
- Testplan 3 und 4, `lostPushAnswerTests` umgebaut: Kaputt Netzfehler (Payee anders geschrieben) und Kaputt Ausgang unbekannt (`PushOutcomeUnknown`), je über `runMirror` in-memory mit `EntryLinkedToYnab` durch `YnabMirror`, keine zweite Buchung, Fehlschlag beendet; intakt anderer Payee; Bestand alte Erfassung mit `BB:` über die ImportId. Entfallen: „Erneut senden mit derselben import_id, 409“ und „Erneut senden ohne import_id in CashExpenseAdded“ (abgedeckt durch die Live-Tests und `LedgerRetryPushTests`).
- Testplan 5, `src/Tests/LedgerMirrorBatchTests.fs` „Spiegel-Lauf: offene Erfassung“: eindeutiges Paar ohne Änderungsereignis, zwei Erfassungen zu einer Beobachtung und zwei Beobachtungen zu einer Erfassung, je mit der Log-Zeile.
- Testplan 6, `src/Tests/LedgerRetryPushTests.fs` „Ledger Erneut senden einer offenen Erfassung“: (a) echter `runMirror` verknüpft, kein Push, kein Auslöser; (b) Spiegel-Fehler und Ausnahme, kein Push, kein Ereignis, Wortlaut; (c) genau ein Push ohne `import_id`; (d) Comdirect ruft den Spiegel nicht; Live über `Server.Api.ledgerApi.retryPushToYnab` (Test-DB ohne Token: der echte `mirrorYnabBudget` scheitert, also kein `EntryPushToYnabFailed`).
- Testplan 7, `src/Tests/LedgerSplitWriteTests.fs` „Ledger Split-Ersatz einer erfassten Buchung“: erfasst mit und ohne alte Verknüpfung ohne `import_id` und ohne `EntryReplacementSentToYnab`, mit Betrag, offener Ersatz mit `pending.NewImportId`, intakt `:r1`, `replacementImportIdFor`.
- Testplan 8, `src/Tests/LedgerEntryDeletionTests.fs` „Buchung wiederherstellen: in BB erfasst ohne import_id“: erfasst ohne `import_id` und ohne `EntryRestoreSentToYnab`, `EntryDeletedInBb` mit der alten; offenes Wiederherstellen mit `pending.NewImportId`; Comdirect mit `:r1`.
- Testplan 9: `BankImportTests.fs`, `YnabClientTests.fs` unverändert grün.
- Bob (`src/Tests/BotApiTests.fs`): der 409-Test ist ersetzt durch „verlorene Antwort: push_error, der Spiegel verknüpft, eine Buchung, zählt einmal, kein zweiter Push“.
- Strukturwächter (`src/Tests/StructureGuardTests.fs`): Muster und Gegenbeweise auf die Fabriken umgestellt (`quickAddPortsOn`, `ledgerBookingPortsOn`, `liveQuickAddPorts`/`liveLedgerBookingPorts` mit `sharedHttpClient`, `entryPush`, `isBotPushWired` für `liveDepsOn`/`liveDeps`). Mitgezogen: `LedgerBookingCaptureTests.fs`, `LedgerHeldDuplicateTests.fs`.

Rot vor dem Fix: Die Tests entstanden nach dem Produktionscode (die alten Signaturen ließen sich nach dem Umbau nicht mehr aufrufen). Gegenproben, je lokal gebaut, volle Suite, danach wiederhergestellt:
1. `planEntryPush` mit `currentImportId` auch für Erfassungen: rot „eine in BB erfasste Buchung geht ohne import_id …“ und „Erneut senden einer alten Erfassung …“.
2. `quickAddPortsOn` mit `(Some "BB:x")`: rot „Quick Add sendet über den echten Einzel-Post ohne import_id“ und der Strukturwächter.
3. `botPushOn` mit `(Some "BB:x")`: rot „Bobs Commit pusht den Request …“.
4. `resolveEntry` ohne dritte Stufe: rot beide Kaputt-Tests der verlorenen Antwort, Bob verlorene Antwort, Retry (a), Batch eindeutiges Paar.
5. `retryPushToYnab` ohne `runMirrorNow`: rot Retry (a), (b) und der Live-Test.
6. `planOpenPushLinks` ohne Eindeutigkeit: rot die vier Mehrdeutigkeits-Tests (rein und Lauf).
7. `replacementImportIdFor` ohne den Zweig „erfasst“: rot Split erfasst, Split mit Betrag, `replacementImportIdFor`, Wiederherstellen erfasst.
Kein Überlebender gilt nur für diese sieben Mutanten.

Abweichungen von der Task:
- What 4, „ein erneuter Commit derselben `preview_id` liest den Stand aus dem Kassenbuch“: Innerhalb der Prozesslaufzeit liefert `BotPreviewStore.CommitOnce` das zwischengespeicherte erste Ergebnis (`pushed_to_ynab: false` mit `push_error`), auch nach der Verknüpfung durch den Spiegel; `bookedFromLedger` greift nur nach einem abgebrochenen Versuch oder für einen neuen Slot. Es gibt keinen zweiten Push und keine zweite Buchung. Das Verhalten ist unverändert (What 4: Verhalten bleibt), der Test prüft keinen zweiten Push, nicht `pushed_to_ynab` der Wiederholung.
- Ein Erneut senden, das der Spiegel gerade verknüpft hat, stößt keinen weiteren Spiegel-Lauf an.
- Die ADR-Nachträge aus dem Verfeinern decken die Umsetzung ab, kein neuer ADR.

README ledger, von Hand zu platzieren (Bullets ohne fetten Kopf, per Delta nicht erreichbar):
- `## Key commands`, *BuchungÜbernehmen*: „(`YnabClient.createTransactionWith` mit der festen `import_id` `BB:` plus EntryId, ledger-p3vq8; liefert die YNAB-Transaction-Id aus der Antwort des Einzel-Posts)“ ersetzen durch „(Einzel-Post `YnabClient.createTransactionOn` ohne `import_id`, ledger-ay81j; liefert die YNAB-Transaction-Id aus der Antwort)“, und „Kennt YNAB die `import_id` schon (409), ist die Buchung angekommen: kein Fehlschlag, `Ok entryId`, der Spiegel verknüpft (**Versand-Ausgang**, ledger-xqqnv).“ ersetzen durch „Geht die Antwort verloren, verknüpft der nächste Spiegel-Lauf die Buchung als **Offene Erfassung** (ledger-ay81j); den 409-Zweig (ledger-xqqnv) erreicht eine Buchung ohne `import_id` nicht mehr.“
- `## Ereignisse`, `CashExpenseAdded`: „Seit Schema-Version 3 (ledger-p3vq8) mit `importId` (`BB:` plus EntryId), Version 1 und 2 ohne.“ ersetzen durch „Seit Schema-Version 3 (ledger-p3vq8) mit dem Feld `importId` (`BB:` plus EntryId), Version 1 und 2 ohne; seit ledger-ay81j schreibt BB es leer, die Buchung geht ohne `import_id` an YNAB.“
- `## Ereignisse`, **Verknüpfung**: „oder vom Spiegel über die ImportId (Actor `YnabMirror`, ADR 0021)“ ergänzen zu „oder vom Spiegel über die ImportId oder als offene Erfassung (Actor `YnabMirror`, ADR 0021, ledger-ay81j)“.
- `## Key events`, *EntryReplacementSentToYnab* / *EntryRestoreSentToYnab*: Satz anhängen „Für eine in BB erfasste Buchung entfallen beide, sie geht ohne `import_id` (ledger-ay81j).“
- `## Key commands`, *Lesen für eine Auswahl*: nach „(YNAB-Id, Übertrags-Gegenbuchungen, ImportId, abgelöste Id; `findEntriesByLinks`)“ einfügen „, dazu die offenen Erfassungen über eine feste Abfrage (`loadOpenCaptures`, ledger-ay81j)“.
Das README_DELTA (17 Ops, Iteration 2 neu: **Schreib-Sperre**, der Satz zur festen `import_id` beschreibt jetzt den Abgleich **Offene Erfassung**, dazu der Spiegel vor der Sperre beim Erneut senden) ist mit `npm run check:readme-delta -- --bc ledger --delta <datei>` geprüft (Exit 0). Vollständigkeit: Ich habe das Delta auf eine Kopie der README angewendet und die ganze Datei (Bullets, `## Ereignisse`, Key commands, Key events, Open questions) per grep nach „feste/festen import_id“, `409`, `cashEntryImportId`, `ImportIdAlreadyInYnab`, `createTransactionWith` und „BB:<“ durchsucht. Falsch bleiben danach nur die Stellen der Hand-Liste oben (*BuchungÜbernehmen* mit `createTransactionWith`, „festen `import_id`“ und dem 409-Satz; `CashExpenseAdded` und **Verknüpfung** in `## Ereignisse`). Die übrigen Treffer sind richtig: `409` unter **Push-Fehlschlag**, **Ersatz-import_id**, **Erneut senden**, **Versand-Ausgang** und **Offener Ersatz** meint Comdirect und den offenen Ersatz, **Bot-Commit** sagt, dass kein 409 mehr entsteht, „BB:<Reference>“ unter **Ist neu** ist ein Comdirect-Import, „BB:<EntryId>“ nennt alte Erfassungen, und „feste `import_id`“ steht im neuen Bullet nur als abgelöster Begriff.

Für die Kann-Sammlung ledger:
- `BotPreviewStore.CommitOnce` liefert nach einer verlorenen Antwort bis zum Neustart `pushed_to_ynab: false`, obwohl der Spiegel die Buchung inzwischen verknüpft hat; eine Wiederholung könnte bei einem `BotBooked` mit `PushedToYnab = false` den Stand neu aus dem Kassenbuch lesen.
- `loadOpenCaptures` lädt je Lauf alle offenen Erfassungen ohne Zeitfenster; bei vielen dauerhaft offenen Buchungen ließe sich auf die Tage der Beobachtungen einschränken.

Conductor bei der Integration (Iteration 3): Die Hand-Liste und die drei Funde des Verifiers aus Iteration 2 (*PushErneutSenden*, *EntryReplacedInYnab*, *EntryDeletedInBb / EntryRestoredInBb*) hat der Conductor selbst in die README gesetzt, der Verifier hat die integrierte README geprüft (PASS). Probelauf gegen eine frische Kopie der Produktion (Delta-Lauf, nur GET): keine Änderung, kein Fehler. Gate auf main: VERIFY: PASS (dotnet 4985, node 322).
