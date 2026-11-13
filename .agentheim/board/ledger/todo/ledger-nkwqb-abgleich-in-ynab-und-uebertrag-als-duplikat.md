---
id: ledger-nkwqb
title: "Von YNAB abgeglichene Buchung bleibt im Kassenbuch doppelt: der Spiegel liest matched_transaction_id nicht und legt die zusammengelegte Buchung nicht still (Ledger/Domain.fs, Ledger/CommandHandlers.fs)"
status: todo
type: bug
context: ledger
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [laufend, money, ynab-sync, server, tests]
related_adrs: [0014, 0015, 0083, 0092, 0093]
related_research: [ing-fints-anbindung-2026-11-06]
prior_art: [ledger-t7v2q, ledger-r7h2w, ledger-z6sya, ledger-f8bjm]
---

## Why
Romans Test nach dem Deploy 57461f98 (2026-11-13): Das Girokonto zeigt in BB um den Betrag einer Buchung zu wenig,
YNAB stimmt.

Ablauf, belegt an einer Kopie der Produktions-DB und lesend an der YNAB-API (Ids und Rohdaten nur im Scratchpad):
1. YNAB ruft die ING automatisch ab und legt auf dem Girokonto selbst einen Übertrag an. Der Spiegel übernimmt ihn als
   Buchung E1 (`EntryObservedInYnab`, Verknüpfung `ynab` = A).
2. Tage später holt der Comdirect-Import (ADR 0093) dieselbe Bankbuchung, bucht sie als E2 (`importid` `BB:<ref>`,
   `comdirect` `<ref>`) und schickt sie an YNAB (`EntryLinkedToYnab`, `ynab` = B). Weil der Payee nicht passte, gab es
   kein mögliches Duplikat (banking-import-pknvp).
3. YNAB gleicht B mit A ab. Danach liefert `GET /transactions/A` die Buchung A mit `matched_transaction_id = B`,
   Kategorie aus YNABs Payee-Zuordnung und `approved: false`. `GET /transactions/B` antwortet 404, und das Delta meldet
   B nie als gelöscht.
4. `YnabObservation` kennt `matched_transaction_id` nicht. E2 bleibt eine eigene Zeile und zählt im Kontostand.
   Löschen in BB scheitert am 404 von YNAB und würde die Comdirect-Referenz verlieren.

Da das `server_knowledge` seit dem Match weitergerückt ist, sieht ein normaler Delta-Lauf A nicht wieder, solange A sich
in YNAB nicht ändert.

## What
Entschieden von Roman (2026-11-13), Ereignisse abgestimmt mit `tactical-modeler`, Nachholen mit `architect`:

- **Beobachtung:** `YnabObservation` bekommt `MatchedYnabTransactionId: string option` (YNABs
  `matched_transaction_id`).
- **Regel:** Trägt eine ungelöschte Beobachtung O die Matched-Id M und zeigt M über die aktuelle `ynab`-Verknüpfung
  (nicht `ynabsuperseded`) auf eine andere Buchung als die von O, dann legt der Spiegel zusammen. Die Buchung von O
  (die YNAB behält) bleibt, die Buchung von M wird aufgenommen.
- **Zwei Ereignisse, ein Anhängen,** Actor `YnabMirror`, Version 1, je im Strom ihrer Buchung (ADR 0083), gleicher
  `OccurredAt`, Muster wie beim Übertrags-Paar `EntryTransferCounterpartObservedInYnab`:
  - `EntryMergedIntoOtherObservedInYnab` auf der aufgenommenen Buchung. Payload: `SurvivorEntryId`,
    `SurvivorYnabTransactionId`, `AbsorbedYnabTransactionId`, `SurvivorPayee: string option`, `SurvivorDate`.
    `apply`: `DeletedInYnab = true`, neue Marke `MergedIntoEntryId = Some SurvivorEntryId`. Alle offenen Fehlschläge
    enden (Kategorie, Flag, Memo, Betrag, Payee, Prüfvermerk, Push), weil jedes Erneut senden auf B nur noch ein 404
    bekäme. `LinkedToYnab` bleibt. `invalidOp`, wenn die Buchung als unsicheres Duplikat wartet oder wenn
    `SurvivorEntryId` die eigene Id ist.
  - `EntryMergedFromOtherObservedInYnab` auf der bleibenden Buchung. Payload: `AbsorbedEntryId`,
    `AbsorbedYnabTransactionId`, `AbsorbedComdirectReference: string option` (aus der `comdirect`-Verknüpfung der
    aufgenommenen Buchung), `AbsorbedPayee: string option`, `AbsorbedDate`, `AbsorbedAmount`. `apply` ändert keinen
    Zustand. `invalidOp`, wenn `AbsorbedEntryId` die eigene Id ist.
  - `linksFrom` liefert für beide `[]`. Keine Verknüpfung wandert: `ynab` = B, `importid` und `comdirect` bleiben auf
    der aufgenommenen Buchung.
- **Zustand:** `Entry.MergedIntoEntryId: EntryId option`, Spalte `merged_into_entry_id TEXT NULL` über
  `initializeDatabase`. Mit dabei: `rowToEntry`, `entryParams` und der Abgleich von Replay und Tabelle.
- **Reine Regel** `mergeObservedInYnab` in `Ledger/Domain.fs`: aus Beobachtung, bleibender und aufgenommener Buchung
  die beiden Ereignisse (erst aufgenommene, dann bleibende) oder `[]`.
- **Spiegel-Lauf** (`runMirror`):
  - `MirrorBatchState.load` lädt die Matched-Ids mit `findEntriesByLinks YnabSystem` und die `comdirect`-Verknüpfungen
    der gefundenen Buchungen einmal gebündelt (kein N+1).
  - Vor der Schleife werden die Merge-Paare bestimmt. Zeigen zwei ungelöschte Beobachtungen im Batch gegenseitig
    aufeinander, oder zeigen mehrere auf dieselbe Id, gibt es keinen Merge, nur eine Log-Zeile.
  - Die Merge-Ereignisse gehen nach dem `observe` der bleibenden Buchung ins Log. Die bleibende Buchung existiert dann
    auch, wenn sie in diesem Lauf erst entsteht.
  - Für eine Beobachtung, deren Buchung in diesem Batch aufgenommen wird oder schon `MergedIntoEntryId` trägt, entfällt
    `observe` (eine Log-Zeile, kein Ereignis). Grund: `observe` prüft `DeletedInYnab` bei ungelöschten Beobachtungen
    nicht, im Vorfall kam B mit geändertem Payee im selben Delta.
- **Kategorie und Prüfvermerk** setzt BB auf der bleibenden Buchung nicht zurück. Die normalen Beobachtungen ziehen
  YNABs Stand nach, sie steht dann als „zu prüfen“ da (Roman).
- **Folgen für andere Regeln:**
  - `knownImportDecision` (ADR 0093 Punkt 7): ein neuer Zweig `MergedIntoEntryId.IsSome` ergibt `SkipKnownImport`,
    direkt nach der Wartesperre und vor `LinkedToYnab`. Ein erneuter Abruf derselben Reference bucht und sendet also
    nichts.
  - `restoreDeletedEntry`: ein ausdrücklicher Zweig nach der Wartesperre lehnt mit „Die Buchung ist in YNAB mit einer
    anderen zusammengelegt.“ ab, kein YNAB-Aufruf.
  - Die übrigen schreibenden Befehle sperren schon über `DeletedInYnab`. `deleteEntry` antwortet „schon gelöscht“
    ohne DELETE, das 404 aus dem Bug ist damit weg.
  - Kontostand, Liste, Budget-Aktivität und „zu prüfen“ schließen sie über `deleted_in_ynab` aus.
- **Historien-Sätze** (`QueryHandlers.describe`, Datum `dd.MM.yyyy`, Betrag über `formatMoney`):
  - aufgenommen: „In YNAB mit <SurvivorPayee> vom <Datum> zusammengelegt“, ohne Payee „In YNAB mit der Buchung vom
    <Datum> zusammengelegt“.
  - bleibend: „Import <Comdirect-Referenz> in YNAB hierher zusammengelegt“. Ohne Referenz „Buchung <Payee> vom <Datum>
    über <Betrag> in YNAB hierher zusammengelegt“, ohne Payee „Buchung vom <Datum> über <Betrag> in YNAB hierher
    zusammengelegt“.
- **Nachholen (Vollabruf-Generation, ADR 0014 Nachtrag),** automatisch beim ersten Lauf nach dem Deploy (Roman):
  - Settings-Schlüssel `ledger_mirror_full_read_generation_<budget>` und Konstante `currentFullReadGeneration = 1` in
    `CommandHandlers.fs`.
  - Weicht der gespeicherte Wert ab (oder fehlt er), holt der Lauf die Buchungen mit `FetchTransactions None`.
  - Der Marker wird nur nach einem gelungenen Lauf geschrieben, neben `saveServerKnowledge`. Auch die Erstbefüllung
    schreibt ihn.
  - Der erste Lauf nach dem Deploy liest einmal alles, sieht A mit Matched-Id B und legt E1 und E2 zusammen. Danach läuft
    wieder das Delta.

## Acceptance criteria
- [ ] 1. Decoder: `observationDecoder` liest `matched_transaction_id` in `MatchedYnabTransactionId`. Fehlt das Feld
      oder ist es `null`, ergibt das `None` (Decoder-Test).
- [ ] 2. Regressionstest, rot vor dem Fix (Doktrin 8 und 9, Kommentar nennt ledger-nkwqb), als Handler-Test von
      `runMirror` mit In-Memory-SQLite und Fake-Fetchern, nur erfundene Ids, Beträge, Payees und Referenzen. Ablauf:
      GIVEN ein Übertrag A auf einem Konto, vom Spiegel als E1 beobachtet, und ein Comdirect-Import E2 mit `importid`
      und `comdirect`, verknüpft mit B. WHEN ein Lauf A mit `matched_transaction_id = B` liefert (dazu B mit geändertem
      Payee, wie im Vorfall). THEN:
      - E2 trägt `EntryMergedIntoOtherObservedInYnab` und E1 `EntryMergedFromOtherObservedInYnab`, beide im selben
        Anhängen.
      - E2 zählt nicht im Kontostand, der Kontostand ist die Summe ohne E2.
      - Auf E2 steht kein Kopfdaten-Ereignis aus der Beobachtung von B.
      - Die Verknüpfungen von E2 (`ynab`, `importid`, `comdirect`) sind unverändert.
      - Ein zweiter Lauf mit demselben Batch schreibt kein Ereignis.
- [ ] 3. Intakt-Szenario: dieselbe Lage, A ohne `matched_transaction_id`. Es gibt keine Zusammenlegung, E2 zählt weiter.
- [ ] 4. Domain-Tests auf `mergeObservedInYnab` und `apply`, je Randfall ein Test (Doktrin 4):
      - Matched-Id unbekannt ergibt `[]`.
      - Matched-Id auf dieselbe Buchung ergibt `[]`.
      - Matched-Id nur als `ynabsuperseded` bekannt ergibt `[]`.
      - Schon aufgenommen ergibt `[]`.
      - Aufgenommene Buchung schon gelöscht (in YNAB, in BB, verworfen) ergibt `[]`.
      - Aufgenommene Buchung mit `PendingReplacement` ergibt `[]`.
      - Bleibende Buchung schon gelöscht oder aufgenommen ergibt `[]`.
      - Gelöschte Beobachtung ergibt `[]`.
      - Abgeglichen (`Reconciled`) auf einer Seite: die Zusammenlegung gilt trotzdem.
      - Offene Fehlschläge der aufgenommenen Buchung enden.
      - Die `invalidOp`-Zweige werfen: Wartemarke, eigene Id.
- [ ] 5. Lauf-Test: Zeigen zwei ungelöschte Beobachtungen im Batch gegenseitig aufeinander, gibt es keinen Merge und
      kein Merge-Ereignis.
- [ ] 6. `knownImportDecision` liefert für eine aufgenommene Buchung `SkipKnownImport`. Ein Abruf mit derselben Reference
      (Test in `BankImportTests` oder bei `planImport`) bucht nichts und sendet nichts.
- [ ] 7. `restoreDeletedEntry` und `deleteEntry` lehnen eine aufgenommene Buchung ohne YNAB-Aufruf und ohne Ereignis ab
      (Port-Aufrufe gezählt), `restoreDeletedEntry` mit dem neuen Satz.
- [ ] 8. Vollabruf-Generation, Handler-Tests mit aufzeichnenden Fake-Fetchern und eindeutiger Budget-Id:
      - Knowledge gespeichert, Marker fehlt: Der Lauf ruft `FetchTransactions None` auf, der Folgelauf
        `Some <ServerKnowledge des ersten>`.
      - Der erste Lauf scheitert: Der Marker bleibt leer, der nächste Lauf ruft wieder `None` auf.
      - Erstbefüllung: Danach steht der Marker, der Folgelauf arbeitet mit dem Delta.
      - Gegenprobe: Ohne den Generationsvergleich wird der erste Test rot.
- [ ] 9. Historie: `describe` liefert die Sätze aus What für beide Seiten samt den Rückfällen ohne Referenz und ohne
      Payee (Test auf `describe`, keine Feldkopie).
- [ ] 10. Ereignis-Codec: Rundlauf für beide neuen Ereignisse, Namens- und Versionstabelle ergänzt.
- [ ] 11. README `ledger` nachgezogen (Bullets in Notes) und vor dem Integrieren mit `npm run check:readme-delta` geprüft.
      Ein `grep` nach den alten Sätzen der ersetzten Bullets ist gemacht (CLAUDE.md, README-Deltas).
- [ ] 12. `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
- **README-Delta (ledger):**
  - **Beobachtung:** Ergänzung „Der Snapshot trägt `matched_transaction_id` (`MatchedYnabTransactionId`).“
  - **Spiegel-Lauf:** Ergänzung zu Merge-Paaren vor der Schleife, Merge nach `observe` der bleibenden Buchung und keinem
    `observe` auf einer aufgenommenen.
  - **Fremd-Id-Verknüpfung:** „Bei einer Zusammenlegung in YNAB wandert keine Verknüpfung; die verdeckte YNAB-Id bleibt
    auf der aufgenommenen Buchung.“
  - **Kontostand:** der Halbsatz „auch eine in YNAB zusammengelegte Buchung zählt nicht“.
  - Neuer Bullet **In YNAB zusammengelegt** nach „Als doppelt verworfen“.
  - In `## Ereignisse` und `## Key events` je ein Eintrag zu beiden Ereignissen mit dem Hinweis „zwei Ereignisse, ein
    Anhängen“.
- **ADR-Nachträge** zu 0015, 0014 und 0093 sind geschrieben (2026-11-13, beim Verfeinern).
- **Vor dem Deploy (Conductor, nicht Worker, nicht im Gate):** Probelauf.
  - Ein lesender Vollabruf bei YNAB, dann `runMirror` gegen eine beschreibbare Kopie der Prod-Kopie im Scratchpad.
  - Gezählt wird nach Ereignisart. Erwartet sind wenige Merges, darunter der bekannte Fall, und kaum andere Ereignisse.
  - Die Salden der betroffenen Konten weichen vorher um genau den Betrag ab und stimmen danach mit YNAB überein.
  - Bei vielen Kategorie- oder Flag-Ereignissen erst klären, dann deployen.
  - Ids und Rohdaten bleiben im Scratchpad.
- Der Deploy-Auftrag ist Romans Zustimmung zum einmaligen Vollabruf (Roman, 2026-11-13: automatisch nach dem Deploy).
- Offen und nicht geprüft: ob B im Delta des Match-Laufs selbst ein `matched_transaction_id` trug. Für die Regel ändert
  sich nichts (gegenseitiges Zeigen wird übersprungen).
- Kann-Punkte (in die Kann-Sammlung ledger-k4n2s übernommen):
  - Hebt Roman den Match in YNAB auf, wird B sichtbar. Heute gibt es dafür nur eine Log-Zeile (Roman, 2026-11-13: reicht).
  - Die Sperrtexte sagen bei einer aufgenommenen Buchung „in YNAB gelöscht“ statt „zusammengelegt“.
  - Ein Zähler `MergedEntries` in der Lauf-Zusammenfassung.
  - Eine Historienzeile auch für eine schon gelöschte aufgenommene Buchung.
  - Optional ein GET auf M als Bestätigung von „verdeckt“, nur wenn der Probelauf unerwartete Kandidaten zeigt.
- Duplikaterkennung für Überträge: eigene Task banking-import-pknvp (Roman, 2026-11-13: zwei Tasks).
- Langfristig verwandt: FinTS für alle Konten (banking-import-r28hx).
