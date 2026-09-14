---
id: ledger-qvt84
title: "Import-Push schreibt zuerst die Kassenbuch-Buchung (Comdirect-Import + Review-Spur), dann YNAB, dann Verknüpfung; Push liefert YNAB-Ids und behebt drei Push-Bugs"
status: done
type: feature
context: ledger
created: 2026-09-11
completed: 2026-09-11
depends_on: [ledger-cscw1, categorization-gdrx8]
blocks: []
tags: [ledger, ynab-sync, push, integration, identity]
related_adrs: [0006, 0013, 0015, 0019, 0021, 0025]
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
Die Vision verlangt, dass das Kassenbuch BB-Entscheidungen kennt (v1-Kriterium). Heute
endet `importToYnab` (`src/Server/Api.fs`) ohne jede Wirkung auf das Kassenbuch; eine
gepushte Buchung taucht erst über den nächsten Spiegel-Lauf mit falscher Herkunft auf
("YNAB-Spiegel" statt "Comdirect-Import" bzw. "Regel"). ADR 0021 legt Reihenfolge und
Fehlerverhalten fest: Kassenbuch zuerst, dann Push, dann Verknüpfung; kein Rollback bei
Push-Fehler.

Dafür muss der Push YNABs Antwort auswerten. `TransactionCreateResult` in
`src/Server/YnabClient.fs` trägt heute nur `CreatedCount`, `DuplicateImportIds` und
`RejectedTransferTransactionIds`, keine Transaction-Ids. Bei der Analyse (architect,
2026-09-11, am Code bestätigt) sind drei Bugs an derselben Stelle aufgefallen, die hier
mitbehoben werden:
- **Zukunftsdatum:** `createTransactions` filtert Buchungen mit Datum in der Zukunft still
  heraus, `importToYnab` markiert sie danach trotzdem als `Imported`. Eine solche Buchung
  kommt nie in YNAB an und gilt als erledigt.
- **Force-Import:** die neue ImportId wird nur innerhalb von `createTransactions` erzeugt
  und nie zurückgegeben; der Aufrufer kann nicht verknüpfen.
- **Nicht lesbare Antwort:** kann BB die 201-Antwort nicht dekodieren, gelten pauschal
  alle gesendeten Buchungen als angelegt.

Dieser Task nimmt den früheren Task `ynab-b7q2m` (Antwort auswerten, Import-Teil) in sich
auf (Entscheidung Roman, 2026-09-11).

## What
**Teil 1: Push in `ynab-sync` (Conformist-Rand)**
1. `createTransactions` in **Vorbereiten** (Skipped- und Zukunfts-Filter,
   Transfer-Payee-Auflösung und Rejects, ImportId-Zuweisung einschließlich Force-Import)
   und **Senden** (postet genau die vorbereitete Liste) aufteilen. Vorbereiten liefert die
   tatsächlich zu sendenden Buchungen samt ihrer ImportId zurück.
2. Die Antwort wird ausgewertet: `TransactionCreateResult` (oder ein neuer Rückgabetyp)
   trägt die YNAB-Transaction-Ids je angelegter Buchung, zugeordnet über die ImportId.
3. Force-Import: die neu erzeugte ImportId ist für den Aufrufer auslesbar.
4. Zukunftsdatum: eine nicht gesendete Buchung wird nicht als `Imported` geführt.
5. Nicht auswertbare Antwort: die Buchungen bekommen einen erkennbaren Zustand
   "unbekannt", keinen pauschalen Erfolg.

**Teil 2: Kassenbuch vor dem Push (`ledger`)**
6. Reine Funktion (z. B. `recordComdirectPush` in `Ledger/Domain.fs`) baut aus
   (AccountId, ImportId, ComdirectReference, Date, Amount, Payee, Memo, Cleared) plus der
   `ReviewSteps`-Liste (`categorization-gdrx8`) eine geordnete `LedgerEvent list`:
   `EntryImportedFromComdirect`, dann je Review-Schritt `EntryCategorizedByRule` (Actor
   `Rule`) bzw. `EntryRecategorized` (Actor `UserInBb`), mit den ursprünglichen
   Zeitpunkten aus `ReviewSteps`, nicht dem Push-Zeitpunkt.
7. Neue Fassade in `Ledger/CommandHandlers.fs`, aufgerufen aus `importToYnab` **vor** dem
   Senden, je zu importierender Buchung:
   a. Prüfen, ob die ImportId schon verknüpft ist (`findEntryByLink` für `importid`);
      wenn ja, überspringen (ADR 0021 Punkt 3, keine zweite Buchung).
   b. Sonst `appendAndApply` mit den Ereignissen aus Schritt 6. Scheitert das Schreiben
      für eine Buchung, wird sie nicht an YNAB gesendet.
8. Gesendet wird nur, was vorher erfolgreich ins Kassenbuch geschrieben wurde.
   Übersprungene Buchungen, Buchungen mit Zukunftsdatum und Transfer-Rejects (ADR 0006)
   erzeugen keine Kassenbuch-Buchung.
9. Nach der Antwort: für jede angelegte Buchung `EntryLinkedToYnab` schreiben (Zuordnung
   über die ImportId aus Teil 1). Ein gescheiterter Push, ein Timeout oder eine
   Duplikat-Ablehnung durch YNAB rollt die Kassenbuch-Buchung nicht zurück (ADR 0021
   Punkt 2); sie bleibt ohne `ynab`-Verknüpfung, bis ein neuer Versuch oder der Spiegel
   sie über die ImportId verknüpft.
10. Split mit Transfer-Zeile (ADR 0006, 0019): das Transfer-Ziel steht als
    `YnabAccountId` plus Name im Payload, eine `LedgerAccountId` ist dafür nicht nötig. Das
    Import-Konto selbst muss als `LedgerAccountId` vorliegen; ist es unbekannt, vorher
    Konten nachziehen oder mit klarer Fehlermeldung abbrechen (Entscheidung beim
    Implementieren, im Diary begründen).

## Acceptance criteria
- [x] `TransactionCreateResult` trägt YNAB-Transaction-Ids je gesendeter Buchung,
      zuordenbar über die ImportId (Test).
- [x] Force-Import: die neu erzeugte ImportId ist vom Aufrufer auslesbar
      (Regressionstest, rot vor dem Fix).
- [x] Buchungen mit Zukunftsdatum werden nicht als `Imported` geführt, wenn sie nicht
      gesendet wurden (Regressionstest, rot vor dem Fix).
- [x] Eine nicht auswertbare YNAB-Antwort markiert Buchungen nicht pauschal als
      importiert (Regressionstest, rot vor dem Fix).
- [x] Nach erfolgreichem Import-Push zeigt die Historie der neuen Buchung:
      Import-Ereignis, gegebenenfalls Regel-Ereignis, gegebenenfalls Korrektur-Ereignis
      (mit den ursprünglichen Review-Zeitpunkten), Verknüpfungs-Ereignis.
- [x] Übersprungene Buchungen und von YNAB als Duplikat abgelehnte Buchungen erzeugen
      keine zweite Kassenbuch-Buchung.
- [x] Ein simulierter Push-Fehler (Netzwerkfehler, Timeout) lässt die bereits
      geschriebene Kassenbuch-Buchung unverändert stehen, ohne `ynab`-Verknüpfung (Test).
- [x] Ein zweiter Versuch für dieselbe ImportId erzeugt keine zweite Buchung (Test).
- [x] Der nächste Spiegel-Lauf trifft eine so angelegte Buchung (über YNAB-Id oder
      ImportId) und legt keine zweite an (Test).
- [x] Eine Split-Buchung mit Transfer-Zeile landet korrekt im Kassenbuch (Kategorie-Zeile
      und Transfer-Zeile, Summe stimmt).
- [x] Replay-Test bleibt grün; Tests nur gegen In-Memory-SQLite.
- [x] `dotnet build` und `dotnet test` grün.

## Notes
- Reihenfolge (Kassenbuch vor Push) und "kein Rollback bei Push-Fehler" sind in ADR 0021
  entschieden und von Roman bestätigt (2026-09-11), nicht mehr offen für den Worker.
- Die Markierung "noch nicht in YNAB" für unverknüpfte Buchungen ist gewollt (Roman,
  2026-09-11), gehört aber in die Oberfläche (`ledger-xbrht`), nicht in diesen Task.
- Der größte Task dieser Reihe. Teil 1 zuerst mit seinen Regressionstests grün machen,
  dann Teil 2 darauf setzen. `ledger-tdnpk` ändert parallel `createManualTransaction` in
  derselben Datei (`YnabClient.fs`), aber eine andere Funktion.
- Die ImportId-Rekonstruktion in `importToYnab` (`BB:{txId ohne Bindestriche}` im
  Duplikat-Zweig) muss nach der Umstellung dieselbe ImportId ergeben wie
  `Shared.Domain.generateImportId`; im Force-Fall stimmt sie heute nicht.
- **Nachtrag 2026-09-11 (Entscheidung Roman zu `categorization-gdrx8`):** `ReviewActor` hat
  einen dritten Fall für Kategorien aus dem Order-ID-Abgleich beim Abruf (Vorschlag aus der
  YNAB-Historie derselben Amazon-Bestellung), Anzeigename "Order-ID-Abgleich". Die
  Übertragung auf Geschwister in der Review ist `ByUser`. Für Schritt 6 heißt das: das
  Kassenbuch braucht einen passenden Urheber (neuer `Actor`-Fall, z. B. `OrderIdMatch`)
  und ein Ereignis dafür (`EntryRecategorized` mit diesem Actor oder ein eigenes Ereignis,
  Wahl beim Implementieren, im Ereignis-Katalog des README festhalten). Die Historie zeigt
  dann z. B. "Order-ID-Abgleich: Büro". Akzeptanz: ein Test mit einer Buchung, deren
  Kategorie aus dem Order-ID-Abgleich stammt, belegt diesen Urheber in der Historie.

## Outcome

**Teil 1 (`src/Server/YnabClient.fs`):** `createTransactions` ist in **Vorbereiten**
(`prepareTransactions`, pur: filtert Skipped und zukunftsdatierte Buchungen heraus,
löst Splits gegen die übergebene Transfer-Payee-Map auf, vergibt die ImportId) und
**Senden** (`sendPrepared`: postet genau die vorbereitete Liste, wertet die Antwort über
die neue, pure `parseCreateTransactionsResponse` aus) aufgeteilt. `resolveTransferPayees`
trägt den bisherigen "nur bei Transfer-Zeile ein `GET /payees`"-Teil. `createTransactions`
bleibt als dünne Komposition aus allen dreien für `forceImportDuplicates` bestehen.
`TransactionCreateResult` trägt jetzt `Created: CreatedTransactionResult list` (Transaction-
Id, ImportId, YNAB-Id je angelegter Buchung, über die ImportId aus der Antwort
zugeordnet, nicht über die Reihenfolge), `FutureDatedTransactionIds` und
`ResponseUnparseable`. Die drei benannten Bugs sind damit strukturell behoben: eine
zukunftsdatierte Buchung landet nie in `ToSend` und damit nie in `Created`; die
Force-Import-ImportId steht an `PreparedTransaction.ImportId` und fließt bis in
`CreatedTransactionResult.ImportId` durch, der Aufrufer kann sie lesen; eine nicht
auswertbare Antwort liefert `ResponseUnparseable = true` mit leerem `Created`, nie einen
geratenen Erfolg.

**Teil 2 (`Ledger.Domain`/`Ledger.CommandHandlers`, ADR 0021):** `recordComdirectPush`
(rein) baut aus einer Comdirect-Buchung und ihrer Review-Spur (`categorization-gdrx8`)
die Ereignisfolge: `EntryImportedFromComdirect` (Actor `ComdirectImport`, immer
`Uncleared`), dann je Review-Schritt `EntryCategorizedByRule` (Actor `Rule`, RuleName
direkt aus dem Schritt) oder `EntryRecategorized` (Actor `UserInBb` bzw. `OrderIdMatch`
für den Order-ID-Abgleich, ADR 0025), jeweils mit dem ursprünglichen Zeitpunkt aus
`ReviewStep.At`. `Old*`-Felder verfolgen den Stand des vorherigen Schritts;
`NewPayee` übernimmt `PayeeOverride`, sonst bleibt der vorherige Payee erhalten (kein
stilles Wischen auf `None`).

`Ledger.CommandHandlers.pushComdirectImports` orchestriert Kassenbuch-zuerst-dann-
Push-dann-Verknüpfung mit injiziertem `push` (Muster wie `addCashExpense`): prüft je
Buchung über `findEntryByLink` (System `importid`), ob sie schon existiert (kein
zweites Entstehen), schreibt sonst über `appendAndApply`, sendet nur, was geschrieben
wurde, und verknüpft nach Erfolg mit `EntryLinkedToYnab`. Ein Push-Fehler, ein YNAB-
Duplikat oder eine nicht auswertbare Antwort lassen die geschriebene Buchung
unverändert und unverknüpft stehen (kein Rollback, ADR 0021 Punkt 2); das rohe
Sende-Ergebnis (`ComdirectPushResult.SendResult`) reicht der Aufrufer für seine eigene
Fehlermeldung durch.

`Server.Api.importToYnab` löst zuerst das Kassenbuch-Konto zum Import-Konto auf
(`Ledger.Persistence.findAccountByYnabAccountId`); fehlt es (Spiegel nie gelaufen),
lehnt der Handler mit einer klaren Fehlermeldung ab, statt ein zweites Konto zu
erfinden (Entscheidung konsistent mit Quick Add, `ledger-tdnpk`, keine eigene ADR-Datei
nötig). Danach `resolveTransferPayees`, `prepareTransactions`, `pushComdirectImports`
mit `push = sendPrepared`. Duplikate werden jetzt direkt über die selbst gesetzte
ImportId erkannt (`prepared.ToSend`-Map), die fragile `"BB:{txId ohne Bindestriche}"`-
Rekonstruktion aus dem Task-Notes-Hinweis ist entfallen.

**Actor/Origin (ADR 0025):** `Ledger.Events.Actor` und `Shared.Domain.LedgerOrigin`
bekamen je einen vierten Fall (`OrderIdMatch of orderId` / `MatchedByOrderId`,
Anzeigename "Order-ID-Abgleich"); Persistenz nutzt die schon vorhandene freie
Text-Spalte `actor_rule_id` für die Bestell-Id, keine Schema-Änderung. Der Client-Badge
(`Components/Ledger/View.fs`) bekam den passenden Fall (`Badge.Warning`).

**Tests** (TDD; die drei Bug-Regressionstests rot vor dem Fix beobachtet, per
Sabotage-Check auch für `pushComdirectImports`s Doppelbuchungs-Schutz verifiziert,
danach jeweils wiederhergestellt): `src/Tests/YnabClientTests.fs` (`prepareTransactions`:
Zukunftsdatum, Skipped, Force-Import-ImportId auslesbar; `parseCreateTransactionsResponse`:
Zuordnung über import_id trotz Umsortierung, Duplikate, nicht lesbare Antwort),
`src/Tests/LedgerTests.fs` (`recordComdirectPush`: leere Spur, Regel+Korrektur mit
eigenen Zeitpunkten, Order-ID-Abgleich; `pushComdirectImports`: volle Historie
Import→Regel→Korrektur→Verknüpfung, Push-Fehler ohne Verknüpfung, zweiter Versuch ohne
zweite Buchung, YNAB-Duplikat ohne Verknüpfung, Split mit Transfer-Zeile),
`src/Tests/LedgerMirrorTests.fs` (nächster Spiegel-Lauf trifft eine per Import-Push
angelegte Buchung über die YNAB-Id, keine zweite Buchung), `src/Tests/LedgerPageTests.fs`
(`originOf`/`originLabel` für `OrderIdMatch`/`MatchedByOrderId`).

**Nicht in diesem Task:** `forceImportDuplicates` schreibt weiterhin nicht ins
Kassenbuch (Task-Text nennt nur `importToYnab`); als Backlog-Item `ledger-fq3kd`
erfasst.

**Verifikation:** `dotnet build` (Solution) grün, 0 Warnungen. `dotnet test`
(`src/Tests/Tests.fsproj`) grün: 770 bestanden (16 neu), 7 übersprungen (extern), 0
Fehler. `npm run build` grün.

### Nachtrag Iteration 2: Retry nach Push-Fehler sendet wieder, drei weitere Verifier-Funde behoben

Der Verifier fand einen echten Regressions-Bug auf dem normalen Weg: `EntryImportedFromComdirect`
schreibt die `importid`-Verknüpfung sofort beim Entstehen. Ein zweiter Push-Versuch nach
Netzwerkfehler, Timeout, 429 oder 400 fand die Buchung deshalb über
`findEntryByLink ImportIdSystem` und behandelte sie als `AlreadyImported`, ohne sie erneut
zu senden; ein Klick auf "Import" schloss die Session ab, ohne dass die Buchung je YNAB
erreichte.

**Fix** (`src/Server/Ledger/CommandHandlers.fs`, `pushComdirectImports`): findet die Suche
eine Buchung für die ImportId, prüft sie zusätzlich über `linksForEntry`, ob eine
`ynab`-Verknüpfung existiert. Nur dann ist sie `AlreadyImported` (kein erneuter Versand).
Ohne `ynab`-Verknüpfung (voriger Versuch scheiterte zwischen Schreiben und Verknüpfen)
läuft dieselbe Buchungs-Id wie ein frischer Schreibvorgang weiter: kein zweites Entstehen,
aber Senden und Verknüpfen werden nachgeholt. `Server.Api.importToYnab` markiert eine so
schon verknüpfte `AlreadyImported`-Buchung jetzt als `Imported` (vorher blieb sie
unverändert, auch wenn sie längst verknüpft war).

**Zwei Stellen ohne Testabdeckung ergänzt** (Verifier-Fund): die Abbildung von der
geparsten YNAB-Antwort auf `TransactionCreateResult` (ImportId-Zuordnung, der
"nicht lesbar"-Zweig) lag inline in der HTTP-Funktion `sendPrepared`, unerreichbar für
Tests. Herausgezogen in die reine Funktion `YnabClient.buildCreateResult`, mit Test für
die ImportId-Zuordnung (trotz Umsortierung durch YNAB) und einem echten Regressionstest
für Bug 3 (nicht lesbare Antwort ergibt `Created = []` / `ResponseUnparseable = true`,
Kommentar nennt den Bug). Ein neuer `pushComdirectImports`-Test füttert direkt ein
Ergebnis mit `ResponseUnparseable = true` und prüft, dass nichts verknüpft wird.

**`forceImportDuplicates`** markierte bisher jede gesendete Buchung pauschal als
`Imported`, unabhängig von `result.Created`. Die Zuordnung ist jetzt in der reinen
Funktion `YnabClient.markCreatedAsImported` (nur `result.Created` wird markiert), mit
Regressionstest, ohne HTTP erreichbar.

**Zwei schwache Zusicherungen** in `src/Tests/LedgerTests.fs` riefen
`findEntryByLink YnabSystem importId` auf (die ImportId ist aber kein `ynab`-Wert, liefert
also immer `None`, unabhängig vom Verhalten). Beide durch
`linksForEntry entryId |> List.tryFind (fun l -> l.System = YnabSystem)` ersetzt, das die
Buchung tatsächlich prüft.

**Stil:** zwei verbliebene ae/oe/ue-Umschreibungen korrigiert
(`YnabClient.fs`: "Über die ImportId" statt "Ueber", `LedgerMirrorTests.fs`: "Änderungs-Ereignis"
statt "Aenderungs-Ereignis", jeweils in Iteration-1-Zeilen).

`ledger-fq3kd`s Notes ergänzt: der normale Push schreibt die Kassenbuch-Buchung schon vor
YNABs Duplikat-Meldung mit der alten, deterministischen ImportId; ein Folgetask, der
`pushComdirectImports` mit einer neuen Force-Import-ImportId aufruft, würde eine zweite
"Comdirect-Import"-Buchung anlegen. Der Folgetask muss die bestehende Buchung über die
alte ImportId finden und nur nachträglich verknüpfen.

**Tests** (TDD, rot vor dem Fix beobachtet, danach wiederhergestellt): ein neuer
`pushComdirectImports`-Test "erster Push scheitert, zweiter Push gelingt: dieselbe
Buchung, jetzt Linked"; zwei neue `buildCreateResult`-Tests (ImportId-Zuordnung, nicht
lesbare Antwort); ein neuer `markCreatedAsImported`-Test; ein neuer
`pushComdirectImports`-Test für `ResponseUnparseable = true`.

**Verifikation nach dem Fix:** `dotnet build` (Solution) grün, 0 Warnungen. `dotnet test`
grün: 775 bestanden (5 mehr als Iteration 1), 7 übersprungen (extern), 0 Fehler.
`npm run build` grün.

### Nachtrag Iteration 3: Split+Kategorie, gescheitertes Schreiben sichtbar, Payee/Memo wie gesendet

Drei weitere Funde des Verifiers behoben, alle auf dieselbe Leitlinie zurückgeführt: die
Kassenbuch-Buchung muss zu dem passen, was YNAB tatsächlich bekommt, und ein gescheitertes
Kassenbuch-Schreiben darf nie still bleiben.

**Split plus Kategorie** (`src/Server/Ledger/Domain.fs`, `recordComdirectPush`): Roman kann
eine Buchung aufteilen und danach über den Kategorie-Chip "Aufgeteilt" eine Kategorie
wählen; `categorizeTransaction` lässt `Splits` dabei stehen, der Review-Schritt trägt dann
Kategorie UND mindestens zwei Split-Zeilen zugleich. `apply`s Kategorie/Splits-Ausschluss
lehnte das Ereignis ab, `pushComdirectImports` lieferte `LedgerWriteFailed`, die Buchung
erreichte YNAB nie. Neue Funktion `resolveCategoryVsSplits` wendet dieselbe Regel wie
`YnabClient.buildTransactionRequest` an (ab zwei Split-Zeilen gewinnen die Splits, die
Kategorie wird verworfen), bevor `toEvent` die Ereignis-Payloads baut.

**`LedgerWriteFailed` sichtbar machen** (`src/Server/Ledger/CommandHandlers.fs`,
`src/Server/Api.fs`): neue reine Funktion `summarizeLedgerWriteFailures` fasst die
gescheiterten Buchungen aus einem `ComdirectPushResult` zusammen (Anzahl, Meldungen).
`importToYnab` schließt die Session nicht mehr ab, solange eine Buchung so offen ist, und
meldet stattdessen einen Fehler (`SyncError.YnabImportFailed`, bestehendes Feld, kein
Eingriff in den Shared-API-Vertrag); erfolgreich importierte Buchungen im selben Batch
werden trotzdem gespeichert, bevor der Fehler zurückgegeben wird.

**Payee/Memo wie gesendet** (`src/Server/Api.fs`): die Kandidaten für
`pushComdirectImports` tragen jetzt `Payee`/`Memo` aus dem vorbereiteten
`PreparedTransaction.Request` (PayeeName mit "Unknown"-Fallback, Memo über
`buildMemoWithReference`, jetzt non-private für Tests), nicht mehr die rohen Bank-Felder.
Sonst sah der erste Spiegel-Lauf nach jedem Push ein falsches
`EntryDetailsChangeObservedInYnab` ("Memo in YNAB geändert"), weil YNAB den formatierten
Memo-Text speichert, das Kassenbuch aber das rohe Bank-Memo. Die rohe Comdirect-Reference
bleibt unverändert in ihrem eigenen Feld. Der Spiegel-Test in
`src/Tests/LedgerMirrorTests.fs` ist umgestellt: die Beobachtung wird jetzt aus demselben
`YnabTransactionRequest` abgeleitet wie der Push-Kandidat, nicht mehr von Hand passend
nachgebaut; eine neue, explizite Zusicherung prüft das Ereignis-Log auf Abwesenheit von
`EntryDetailsChangeObservedInYnab`.

**README** (`.agentheim/contexts/ledger/README.md`, `BuchungÜbernehmen`): Absatz zum
Comdirect-Import-Push auf den aktuellen Stand gebracht (Retry-Verhalten aus Iteration 2,
Split+Kategorie-Regel, Payee/Memo-Quelle, sichtbares `LedgerWriteFailed`).

**Tests** (TDD, rot vor dem Fix beobachtet, danach wiederhergestellt): ein neuer
`pushComdirectImports`-Test "eine Spur 'erst Split, dann Kategorie' wird geschrieben,
gesendet und verknüpft"; zwei neue `summarizeLedgerWriteFailures`-Tests; der
Spiegel-Test in `LedgerMirrorTests.fs` umgeschrieben plus eine neue, explizite
Zusicherung gegen `EntryDetailsChangeObservedInYnab`.

**Verifikation nach dem Fix:** `dotnet build` (Solution) grün, 0 Warnungen. `dotnet test`
grün: 778 bestanden (3 mehr als Iteration 2), 7 übersprungen (extern), 0 Fehler.
`npm run build` grün.

## Diary entry

## 2026-09-11 17:33 - Import-Push: Kassenbuch zuerst, dann YNAB, dann Verknüpfung; drei Push-Bugs behoben

**What I did:**
`YnabClient.createTransactions` in Vorbereiten (`prepareTransactions`) und Senden
(`sendPrepared`) aufgeteilt und drei Bugs behoben: zukunftsdatierte Buchungen wurden
trotz Nicht-Versand als importiert markiert, die neue ImportId eines Force-Imports war
für den Aufrufer nie auslesbar, eine nicht lesbare YNAB-Antwort galt pauschal als voller
Erfolg. `TransactionCreateResult` trägt jetzt die YNAB-Ids je Buchung über die ImportId
zugeordnet. Darauf aufbauend schreibt der Comdirect-Import-Push jetzt zuerst die
Kassenbuch-Buchung (`Ledger.Domain.recordComdirectPush` aus der Review-Spur,
`Ledger.CommandHandlers.pushComdirectImports` für Kassenbuch-zuerst-Push-Verknüpfung,
ADR 0021), bevor er nach YNAB sendet. Ein neuer Actor `OrderIdMatch` (ADR 0025) gibt
Kategorien aus dem Order-ID-Abgleich einen eigenen Urheber in der Historie.

**Files Added:**
- `.agentheim/knowledge/decisions/0025-order-id-abgleich-als-eigener-actor-kein-eigenes-ereignis.md`
- `.agentheim/contexts/ledger/backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md`

**Files Modified:**
- `src/Server/YnabClient.fs` - `createTransactions` aufgeteilt in `prepareTransactions`,
  `parseCreateTransactionsResponse`, `resolveTransferPayees`, `sendPrepared`;
  `TransactionCreateResult` neu geformt, `emptyCreateResult`.
- `src/Server/Ledger/Domain.fs` - `recordComdirectPush` (rein, baut die Ereignisfolge
  aus der Review-Spur).
- `src/Server/Ledger/CommandHandlers.fs` - `pushComdirectImports` (Kassenbuch zuerst,
  dann Push, dann Verknüpfung, injizierter `push`).
- `src/Server/Ledger/Events.fs` - `Actor.OrderIdMatch`, Persistenzform.
- `src/Server/Ledger/QueryHandlers.fs` - `originOf` um `OrderIdMatch` ergänzt.
- `src/Shared/Domain.fs` - `LedgerOrigin.MatchedByOrderId`, `originLabel`.
- `src/Client/Components/Ledger/View.fs` - Badge für `MatchedByOrderId`.
- `src/Server/Api.fs` - `importToYnab` (Kassenbuch-Konto auflösen, Kassenbuch-zuerst-
  Push-Verknüpfung verdrahten), `forceImportDuplicates` (`.Created.Length`).
- `src/Tests/YnabClientTests.fs`, `src/Tests/LedgerTests.fs`,
  `src/Tests/LedgerMirrorTests.fs`, `src/Tests/LedgerPageTests.fs` - neue Testlisten.
- `.agentheim/contexts/ledger/README.md` - Actor-Liste, Entscheidungen, Herkunfts-Badge,
  `BuchungÜbernehmen` um den Comdirect-Import-Push-Ablauf ergänzt.

**Files Deleted:**
- keine

**Rationale:**
Die Vision verlangt, dass das Kassenbuch BB-Entscheidungen kennt; ADR 0021 legt dafür
Reihenfolge und Fehlerverhalten fest. Die drei Push-Bugs saßen an derselben Stelle wie
die geforderte Antwort-Auswertung und wurden deshalb im selben Task mitbehoben.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 770 bestanden (16 neu), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Die drei Bug-Regressionstests und der Doppelbuchungs-Schutz in
  `pushComdirectImports` wurden per temporärer Sabotage rot beobachtet, danach
  wiederhergestellt.

## 2026-09-11 17:53 - Iteration 2: Retry nach Push-Fehler sendet wieder, drei weitere Verifier-Funde behoben

**What I did:**
Den vom Verifier gefundenen Regressions-Bug behoben: ein zweiter Push-Versuch nach
gescheitertem ersten (Netzwerkfehler, Timeout, 429, 400) sendete nie erneut, weil die
schon geschriebene, aber noch unverknüpfte Buchung über die ImportId als
`AlreadyImported` galt. `pushComdirectImports` prüft jetzt zusätzlich, ob eine
`ynab`-Verknüpfung existiert; ohne sie wird dieselbe Buchung erneut gesendet und
verknüpft. Außerdem: die Abbildung von der geparsten YNAB-Antwort auf
`TransactionCreateResult` in die reine Funktion `buildCreateResult` gezogen und
getestet (inkl. echtem Regressionstest für Bug 3); `forceImportDuplicates` markiert nur
noch `result.Created` als importiert (`markCreatedAsImported`, extrahiert und getestet);
zwei schwache Testzusicherungen korrigiert; zwei Umlaut-Stellen korrigiert;
`ledger-fq3kd`s Notes um den Hinweis ergänzt, dass ein Folgetask die bestehende Buchung
neu verknüpfen muss statt eine zweite anzulegen.

**Files Modified (Iteration 2):**
- `src/Server/Ledger/CommandHandlers.fs` - `pushComdirectImports` prüft `linksForEntry`
  vor `AlreadyImported`; Kommentare korrigiert.
- `src/Server/Api.fs` - `AlreadyImported` markiert jetzt `Imported`;
  `forceImportDuplicates` nutzt `YnabClient.markCreatedAsImported`.
- `src/Server/YnabClient.fs` - `buildCreateResult` (rein, aus `sendPrepared` gezogen),
  `markCreatedAsImported` (rein, für `forceImportDuplicates`); Umlaut-Fix.
- `src/Tests/LedgerTests.fs` - neuer Regressionstest, zwei korrigierte Zusicherungen,
  neuer `ResponseUnparseable`-Test.
- `src/Tests/YnabClientTests.fs` - neue Testlisten `buildCreateResultTests`,
  `markCreatedAsImportedTests`.
- `src/Tests/LedgerMirrorTests.fs` - Umlaut-Fix.
- `.agentheim/contexts/ledger/backlog/ledger-fq3kd-force-import-schreibt-noch-nicht-ins-kassenbuch.md` -
  Notes um die ImportId-Kollision ergänzt.

**Files Added/Deleted:** keine.

**Rationale:**
Der Regressions-Bug hätte jede Buchung, deren erster Push-Versuch fehlschlägt, dauerhaft
unimportiert gelassen, sobald die Session als abgeschlossen galt: genau das, was ADR 0021
("bis ein erneuter Push ... sie über die ImportId verknüpft") verhindern soll.

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 775 bestanden (5 mehr als Iteration 1), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Regressionstest und die beiden Bug-3/Force-Import-Regressionstests
  wurden per temporärer Sabotage rot beobachtet, danach wiederhergestellt.

## 2026-09-11 18:12 - Iteration 3: Split+Kategorie, gescheitertes Schreiben sichtbar, Payee/Memo wie gesendet

**What I did:**
Drei Verifier-Funde behoben. `recordComdirectPush` wendet jetzt dieselbe Regel wie
`YnabClient.buildTransactionRequest` an, wenn ein Review-Schritt Kategorie UND
mindestens zwei Split-Zeilen zugleich trägt (Splits gewinnen), sonst lehnte `apply` das
Ereignis ab und die Buchung erreichte YNAB nie, still. `importToYnab` schließt die
Session nicht mehr ab und meldet einen Fehler, wenn das Kassenbuch-Schreiben einer
Buchung scheitert (`Ledger.CommandHandlers.summarizeLedgerWriteFailures`), statt es
stillschweigend zu ignorieren. Die Kandidaten für `pushComdirectImports` tragen
Payee/Memo jetzt aus dem tatsächlich gesendeten Request, nicht aus den rohen
Bank-Feldern, damit der erste Spiegel-Lauf nach einem Push kein falsches
Kopfdaten-Ereignis mehr erzeugt.

**Files Modified (Iteration 3):**
- `src/Server/Ledger/Domain.fs` - `resolveCategoryVsSplits` in `recordComdirectPush`.
- `src/Server/Ledger/CommandHandlers.fs` - `summarizeLedgerWriteFailures`.
- `src/Server/Api.fs` - `importToYnab` meldet gescheitertes Kassenbuch-Schreiben;
  Candidate-Payee/Memo aus `PreparedTransaction.Request`.
- `src/Server/YnabClient.fs` - `buildMemoWithReference` non-private (Testbarkeit).
- `src/Tests/LedgerTests.fs` - neuer Split+Kategorie-Test, zwei neue
  `summarizeLedgerWriteFailures`-Tests.
- `src/Tests/LedgerMirrorTests.fs` - Spiegel-Test umgeschrieben (Beobachtung aus dem
  gesendeten Request abgeleitet), neue explizite Zusicherung gegen
  `EntryDetailsChangeObservedInYnab`.
- `.agentheim/contexts/ledger/README.md` - Absatz zum Comdirect-Import-Push aktualisiert.

**Files Added/Deleted:** keine.

**Rationale:**
Alle drei Funde teilen dieselbe Ursache: die Kassenbuch-Ereignisse müssen zu dem passen,
was YNAB tatsächlich bekommt (Split-Regel, Payee/Memo), und ein Fehler beim
Kassenbuch-Schreiben darf nie stillschweigend verschwinden (Append-Only-Log, ADR 0013,
Auditierbarkeit).

**Outcomes:**
- Build: grün, 0 Warnungen (`dotnet build BudgetBuddy.sln`).
- Tests: 778 bestanden (3 mehr als Iteration 2), 7 übersprungen (extern), 0 Fehler
  (`dotnet test src/Tests/Tests.fsproj`). `npm run build` grün.
- Issues: keine. Der Split+Kategorie-Test und die
  `summarizeLedgerWriteFailures`-Regressionstests wurden per temporärer Sabotage rot
  beobachtet, danach wiederhergestellt.

## Verifier note (iteration 1)

**REASONS:**
- Check 1, AC "TransactionCreateResult trägt YNAB-Transaction-Ids je gesendeter Buchung, zuordenbar über die ImportId (Test)" und AC "Eine nicht auswertbare YNAB-Antwort markiert Buchungen nicht pauschal als importiert (Regressionstest, rot vor dem Fix)": Die Tests in `src/Tests/YnabClientTests.fs` (`parseCreateTransactionsResponseTests`) prüfen nur den JSON-Parser (`ParsedCreateResponse`), nie `TransactionCreateResult`. Die Zuordnung ImportId zu TransactionId (`Map.tryFind importId importIdToTxId`, `src/Server/YnabClient.fs` etwa Zeile 760) und der Zweig "nicht lesbar" (`Created = []`, `ResponseUnparseable = true`, etwa Zeile 777) liegen inline in der HTTP-Funktion `sendPrepared` und sind von keinem Test erreicht. Käme der alte Rückfall "alle gesendeten gelten als angelegt" zurück, blieben alle Tests grün. Kein `pushComdirectImports`-Test füttert ein Ergebnis mit `ResponseUnparseable = true`.
- Check 6b (ADR 0021 Punkt 2, What Punkt 9: die Buchung bleibt unverknüpft, "bis ein erneuter Push oder der nächste Spiegel-Lauf sie über die ImportId verknüpft"): Ein zweiter Versuch nach gescheitertem Push sendet nie erneut. `EntryImportedFromComdirect` schreibt die `importid`-Verknüpfung; beim zweiten Versuch trifft `findEntryByLink ImportIdSystem` (`src/Server/Ledger/CommandHandlers.fs` etwa Zeile 427), die Buchung wird `AlreadyImported` und fällt aus `toSend`. In `src/Server/Api.fs` (etwa Zeile 1221) lässt `AlreadyImported` die SyncTransaction unverändert; da `duplicateTransactionIds` leer ist, läuft `completeSession()` (etwa Zeile 1237) mit `CreatedCount = 0`. Folge auf dem normalen Weg: nach Netzwerkfehler, Timeout, 429 oder 400 schließt der nächste Klick auf "Import" die Session ab, und die Buchung erreicht YNAB nie. Vorher hat ein zweiter Versuch einfach erneut gepusht. Der Test "ein zweiter Versuch für dieselbe ImportId erzeugt keine zweite Buchung" deckt nur den Fall ab, dass der erste Versuch schon `Linked` war.
- Verwandt: Zeigt `AlreadyImported` auf eine Buchung, die bereits eine `ynab`-Verknüpfung hat (früherer Push erfolgreich, Session-Speichern gescheitert), lässt Api.fs die SyncTransaction nicht importiert; sie sollte `Imported` werden.
- `forceImportDuplicates` (`src/Server/Api.fs` etwa Zeile 1300), vom Diff berührt, markiert weiterhin jede per Force-Import gesendete Buchung `Imported`, unabhängig von `result.Created`, `result.ResponseUnparseable` und `result.FutureDatedTransactionIds`. Nur die Buchungen in `result.Created` sollen markiert werden.
- Schwache Zusicherungen: `src/Tests/LedgerTests.fs` Zeilen 1101 und 1146 rufen `findEntryByLink YnabSystem importId` auf, suchen also den ImportId-Wert unter dem System `ynab`. Das liefert immer None. Stattdessen `linksForEntry entryId` auf das Fehlen einer `YnabSystem`-Verknüpfung prüfen.
- Stil (echte Umlaute): `src/Server/YnabClient.fs:375` "Ueber die ImportId", `src/Tests/LedgerMirrorTests.fs:818` "Aenderungs-Ereignis" (neue Zeile).
- Scope-Urteil, nicht blockierend: Force-Import über das Kassenbuch ist ein berechtigter Folgetask. `ledger-fq3kd` ist sauber, seine Notes übersehen aber einen Punkt: der normale Push hat vor der Duplikat-Meldung von YNAB bereits eine unverknüpfte Buchung mit der alten ImportId geschrieben. Wer `pushComdirectImports` mit neuer ImportId wiederverwendet, legt eine zweite "Comdirect-Import"-Buchung an; der Folgetask sollte die bestehende Buchung neu verknüpfen. In die Notes von `ledger-fq3kd` aufnehmen.

**SUGGESTED_FIX:** In `pushComdirectImports`: existiert die ImportId schon, hat aber keine `ynab`-Verknüpfung, das Schreiben überspringen, aber trotzdem senden und verknüpfen. Nur überspringen, wenn eine `ynab`-Verknüpfung existiert (und Api markiert diesen Fall `Imported`). Test "erster Push Error, zweiter Push erfolgreich, dieselbe Buchung, Linked" ergänzen. Die Abbildung von der geparsten Antwort auf `TransactionCreateResult` aus `sendPrepared` in eine reine Funktion ziehen und die ImportId-Zuordnung sowie den Pfad "nicht lesbar ergibt `Created = []`" testen. In `forceImportDuplicates` nur `result.Created` als Imported markieren. Die zwei schwachen Zusicherungen und die Umlaute korrigieren.

**ITERATION_HINT:** likely-fixable

## Verifier note (iteration 2)

**REASONS:**
- Check 1, Kriterium "Nach erfolgreichem Import-Push zeigt die Historie der neuen Buchung: Import-Ereignis, ggf. Regel-Ereignis, ggf. Korrektur-Ereignis ..., Verknüpfungs-Ereignis": Bei einer normalen Review-Spur scheitert das Kassenbuch-Schreiben, und die Buchung verschwindet still. Ablauf: Roman teilt eine Buchung auf, tippt danach auf den Kategorie-Chip "Aufgeteilt" (öffnet den Picker, `src/Client/Components/SyncFlow/Views/TransactionRow.fs:294-297`) und wählt eine Kategorie. `categorizeTransaction` (`src/Server/Api.fs` etwa Zeilen 930 bis 941) setzt `CategoryId`, lässt `Splits` aber stehen; der ByUser-Schritt trägt beides. `recordComdirectPush` (`src/Server/Ledger/Domain.fs:333-360`) übernimmt beides in `EntryRecategorized`, `apply` lehnt ab ("Kategorie und Splits schliessen sich aus"), `pushComdirectImports` liefert `LedgerWriteFailed`. `importToYnab` lässt die SyncTransaction unverändert (`Api.fs:1229`), meldet keinen Fehler, zählt sie nicht und schließt die Session trotzdem ab (`Api.fs:1240`, weil `duplicateTransactionIds` leer ist). Vorher ging dieselbe Buchung als Split an YNAB (`buildTransactionRequest` bevorzugt Splits); jetzt erreicht sie YNAB nie, still. Kein Test deckt `LedgerWriteFailed` ab, weder in `pushComdirectImports` noch in der Behandlung durch Api.
- Check 1, Kriterium "Der nächste Spiegel-Lauf trifft eine so angelegte Buchung ... und legt keine zweite an (Test)": Der Test in `src/Tests/LedgerMirrorTests.fs` (etwa Zeilen 806 bis 812) baut die Beobachtung von Hand passend zum Kassenbuch (`Memo = None`). YNAB speichert aber, was gesendet wurde: `buildMemoWithReference` ergibt immer "<Memo komprimiert>, Ref: <Referenz>", das Kassenbuch schreibt das rohe Bank-Memo (`Api.fs:1143-1144`). Folge: Beim ersten Spiegel-Lauf nach jedem Comdirect-Push vergleicht `observe` (`Ledger/Domain.fs:442`) die beiden, und es entsteht ein falsches `EntryDetailsChangeObservedInYnab` (Actor `YnabMirror`, "Memo in YNAB geändert"). Gleiches beim Payee, wenn der Bank-Payee `None` ist (YNAB bekommt "Unknown"). Das Log ist append-only (ADR 0013), jede falsche Historien-Zeile bleibt für immer.
- Check 5 (README-Sync, geringer): `.agentheim/contexts/ledger/README.md:144` beschreibt noch das Verhalten aus Iteration 1; das Retry-Verhalten aus Iteration 2 fehlt (gefundene, aber nicht mit YNAB verknüpfte Buchung wird erneut gesendet und verknüpft; übersprungen wird nur eine schon verknüpfte, `AlreadyImported` wird dann `Imported`).
- Die sechs Funde aus Iteration 1 sind behoben. Build und Tests grün (775 bestanden, 7 übersprungen), `npm run build` grün.

**SUGGESTED_FIX:** Die Ereignisse in `recordComdirectPush` sollen zu dem passen, was YNAB tatsächlich bekommt: bei einem Schritt mit Kategorie und mindestens zwei Split-Zeilen die Split-Regel von `buildTransactionRequest` anwenden (Kategorie verwerfen); Memo und Payee-Fallback so schreiben, wie sie gesendet werden (z. B. aus `PreparedTransaction.Request`). Den Spiegel-Test auf eine Beobachtung aus den gesendeten Request-Daten umstellen. `LedgerWriteFailed` in `importToYnab` sichtbar machen (Session nicht als abgeschlossen melden, Fehler durchreichen oder zählen) und mit Test belegen (Spur mit Split und danach Kategorie wird geschrieben und gesendet; ein Schreibfehler bleibt nicht still). README-Zeile zum Retry nachziehen.

**ITERATION_HINT:** likely-fixable
