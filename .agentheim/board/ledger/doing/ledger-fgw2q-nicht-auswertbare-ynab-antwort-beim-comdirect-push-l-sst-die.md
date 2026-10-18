---
id: ledger-fgw2q
title: "Nicht auswertbare YNAB-Antwort beim Comdirect-Push lässt die Session ohne Verknüpfung abschließen"
status: doing
type: bug
context: ledger
created: 2026-09-25
completed:
depends_on: []
blocks: []
tags: [ledger, push, session]
related_adrs: [0021]
related_research: []
prior_art: [ledger-na3rq]
---

## Why

`Server.PushOutcomeClassification.classifyPushOutcomes` (ledger-na3rq) hat sichtbar
gemacht, aber bewusst nicht behoben: ein `WrittenNotLinked`-Outcome ohne gemeldete
Duplikat-ImportId (das heißt, YNABs Antwort auf den Push war nicht auswertbar,
`TransactionCreateResult.ResponseUnparseable = true`) zählt weder als Duplikat
(`DuplicateTransactionIds`) noch als gescheitertes Kassenbuch-Schreiben
(`LedgerWriteFailureSummary`). Auf dem NORMALEN Import-Push (`importToYnab`,
`forceImport = false`) wird `SessionMayComplete` in diesem Fall also weiterhin `true`,
obwohl keine der Buchungen dieses Versuchs bestätigt mit YNAB verknüpft wurde. Die
Buchungen bleiben zwar unverändert stehen (kein Rollback, ADR 0021 Punkt 2) und der
nächste Spiegel-Lauf verknüpft sie nach, aber die Session schließt bereits jetzt ab, ohne
dass Roman erfährt, dass die Antwort nicht ausgewertet werden konnte. (Auf dem Force-Pfad
ist dieser konkrete Fall seit einer zweiten Iteration von ledger-na3rq bereits behoben:
dort blockiert eine im Force-Batch selbst unverändert gebliebene Buchung den Abschluss.)

## What

**Entscheidung Romans (2026-10-02, nicht mehr offen):** eine nicht auswertbare Antwort
(`WrittenNotLinked` ohne gemeldete Duplikat-ImportId) ergibt auf dem NORMALEN
Import-Push künftig ebenfalls `SessionMayComplete = false`, wie schon auf dem
Force-Pfad, mit einer für Roman sichtbaren Fehlermeldung (analog zum
`LedgerWriteFailureSummary`-Pfad, nicht als neue Client-Komponente).

### Wo genau

1. **`Server.PushOutcomeClassification` (`src/Server/PushOutcomeClassification.fs`):**
   `PushOutcomeClassification` bekommt ein neues Feld
   `UnresolvedWrittenNotLinkedTransactionIds: TransactionId list`: die Ids aller
   Buchungen dieses Versuchs mit einem `WrittenNotLinked`-Outcome, zu denen
   `importIdByTxId` zwar eine ImportId kennt, die aber NICHT in
   `duplicateImportIdSet` steht (also: nicht auswertbare Antwort, kein gemeldetes
   Duplikat). Dieselbe Fallunterscheidung wie in der bestehenden
   `duplicateTransactionIds`-Berechnung (`classifyPushOutcomes`, Zeile ~85-94),
   nur der jeweils andere Zweig des `match`. `SessionMayComplete` bekommt eine
   vierte Bedingung:

   ```fsharp
   SessionMayComplete =
       duplicateTransactionIds.IsEmpty
       && ledgerWriteFailureSummary.IsNone
       && unresolvedWrittenNotLinkedTransactionIds.IsEmpty
       && not sessionWideOpenTransactionBlocksForceImport
   ```

   Bewusst OHNE `forceImport`-Unterscheidung: die neue Bedingung gilt auf beiden
   Pfaden gleich. Auf dem Force-Pfad ändert sich dadurch nichts am Ergebnis (die
   betroffene Buchung bleibt ohnehin schon "offen kategorisiert" und blockiert
   `sessionWideOpenTransactionBlocksForceImport` bereits heute, siehe Notes); auf
   dem normalen Pfad (`forceImport = false`) ist das genau die heute fehlende
   Bedingung.

2. **`Server.Api.importToYnab` (`src/Server/Api.fs:1262-1281`):** der bestehende
   `match classification.LedgerWriteFailureSummary with` erweitern, damit auch
   `UnresolvedWrittenNotLinkedTransactionIds` einen sichtbaren Fehler ergibt, wenn
   kein `LedgerWriteFailureSummary` vorliegt (Reihenfolge: ein echtes
   Kassenbuch-Schreib-Scheitern bleibt vorrangig, weil es schwerwiegender ist):

   ```fsharp
   match classification.LedgerWriteFailureSummary with
   | Some (failedCount, messages) ->
       return Error (SyncError.YnabImportFailed (failedCount, $"Kassenbuch-Schreiben fehlgeschlagen für {failedCount} Buchung(en): {messages}"))
   | None when not classification.UnresolvedWrittenNotLinkedTransactionIds.IsEmpty ->
       let n = classification.UnresolvedWrittenNotLinkedTransactionIds.Length
       return Error (SyncError.YnabImportFailed (n, $"YNAB-Antwort für {n} Buchung(en) war nicht auswertbar, sie bleiben ohne Verknüpfung stehen und werden beim nächsten Spiegel-Lauf automatisch nachgezogen."))
   | None ->
       return Ok { CreatedCount = createdCount; DuplicateTransactionIds = classification.DuplicateTransactionIds }
   ```

   Genaue Formulierung der Meldung ist Geschmackssache, der Worker darf sie
   anpassen; wichtig ist nur: eigener, von `LedgerWriteFailureSummary` unterscheidbarer
   Text, `SyncError.YnabImportFailed` als Fehlertyp (kein neuer `SyncError`-Fall
   nötig). Der Client braucht dafür KEINE Änderung: `YnabImportFailed` wird bereits
   generisch zu einer Toast-Meldung formatiert
   (`src/Client/Components/SyncFlow/State.fs:18`, `errorMessage`) und löst bereits
   ein Neuladen der Session aus (`ImportCompleted (Error ...)`, siehe
   `SyncFlowStateTests.fs`). Das erfüllt "sichtbare Meldung für Roman" ohne
   Client-Code.

3. **`Server.Api.forceImportDuplicates` (`src/Server/Api.fs:1290-1418`):** KEINE
   Änderung nötig, KEIN Teil dieses Tasks. Dort gibt es schon heute keine
   Fehlermeldung für einen blockierten Abschluss (auch nicht für
   `LedgerWriteFailureSummary`, das ist eine vorbestehende, hier nicht zu
   schließende Lücke); `SessionMayComplete` war für diesen Fall auf dem Force-Pfad
   schon vorher `false` (siehe Notes). Nur `classifyPushOutcomes` selbst bekommt
   das neue Feld, die Aufrufstelle in `forceImportDuplicates` muss es nicht
   gesondert auswerten.

## Acceptance criteria

- [ ] `PushOutcomeClassification` trägt `UnresolvedWrittenNotLinkedTransactionIds:
      TransactionId list`.
- [ ] `SessionMayComplete` ist `false`, wenn diese Liste nicht leer ist, auf BEIDEN
      Pfaden (`forceImport = true` und `forceImport = false`).
- [ ] Regressionstest (muss vor dem Fix rot sein, danach grün), ersetzt/erweitert den
      bestehenden Test "WrittenNotLinked ohne gemeldete Duplikat-ImportId (nicht
      auswertbare Antwort) bleibt unverändert und blockiert den Abschluss nicht"
      (`src/Tests/PushOutcomeClassificationTests.fs:115-134`): derselbe Aufbau
      (`forceImport = false`, ein `WrittenNotLinked`-Outcome ohne
      `DuplicateImportIds`-Eintrag), aber neu erwartet:
      - `Expect.isFalse result.SessionMayComplete` (vorher `isTrue`, das war der
        Fehler),
      - `Expect.equal result.UnresolvedWrittenNotLinkedTransactionIds [ tx.Transaction.Id ]`,
      - `updated.YnabImportStatus` bleibt `NotAttempted` (unverändert, diese
        Zeile bleibt).
      Testname und Kommentar entsprechend anpassen (nicht mehr "blockiert den
      Abschluss nicht", sondern "blockiert den Abschluss jetzt auch auf dem
      normalen Import-Push").
- [ ] Intakt-Szenario, nicht neu zu schreiben, nur zu prüfen: der Test
      "WrittenNotLinked mit gemeldeter Duplikat-ImportId markiert die Buchung als
      Duplikat und blockiert den Abschluss" (Zeile 97) bleibt unverändert grün: ein
      echtes Duplikat ist nicht "unresolved", `UnresolvedWrittenNotLinkedTransactionIds`
      bleibt für diesen Fall leer.
- [ ] Neuer oder erweiterter Test für `Server.Api.importToYnab` (oder, falls der
      Fable.Remoting-Proxy das unter `dotnet test` verhindert, mindestens ein klar
      benannter, nicht lauffähiger `TDD_SKIPPED`-Platzhalter mit Begründung, nach
      dem Muster, das das Projekt für Proxy-Pfade schon kennt): eine nicht
      auswertbare Antwort auf dem normalen Push liefert `Error
      (SyncError.YnabImportFailed (...))`, nicht `Ok`.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes

Gefunden bei der Extraktion in `ledger-na3rq`; siehe dort den Test "WrittenNotLinked ohne
gemeldete Duplikat-ImportId (nicht auswertbare Antwort) bleibt unverändert und blockiert
den Abschluss nicht" (jetzt umzuschreiben, siehe Acceptance criteria), sowie den
Force-Pfad-Test in derselben Datei, der zeigt, dass genau dieser Fall auf dem Force-Pfad
bereits blockiert: dort bleibt die betroffene Buchung mit ihrem ursprünglichen,
kategorisierten Status stehen (`tx.Status` unverändert, z. B. `AutoCategorized`), und
`sessionWideOpenTransactionBlocksForceImport` erkennt sie als weiterhin offen
(`isOpenCategorized`), unabhängig vom neuen Feld. Der Force-Pfad braucht die neue
Bedingung also nicht, um in diesem Fall schon richtig zu blockieren; sie macht das
Verhalten nur explizit und konsistent mit dem normalen Pfad, falls sich die
Status-Zuordnung einmal ändert.

Verwandt, aber NICHT Teil dieses Tasks: `forceImportDuplicates` meldet generell keinen
sichtbaren Fehler zurück, wenn der Abschluss blockiert bleibt (auch nicht bei
`LedgerWriteFailureSummary`). Das betrifft diesen Fall nur am Rand und ist eine
vorbestehende Lücke, kein neuer Fund dieses Tasks.