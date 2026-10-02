---
id: ledger-h4nxz
title: "Split-Zeilen-Überträge: Gegenbuchung über YNABs Subtransaction-Id verknüpfen"
status: backlog
type: feature
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [ledger, spiegel, uebertrag, split]
related_adrs: [0006, 0013, 0014, 0015, 0046]
related_research: [ynab-transfer-in-split-subtransaction-2026-06-13]
prior_art: [ledger-t7v2q]
---

## Why
`ledger-t7v2q` hat `EntryTransferCounterpartObservedInYnab` gebaut: die Verknüpfung zweier
Buchungen desselben Übertrags über YNABs `transfer_transaction_id` **am Buchungskopf**
(`YnabObservation.TransferYnabTransactionId`, `Server.YnabObservations`). Der Modellierer
hatte als "Offener Punkt 2" markiert: YNAB kann einen Übertrag auch als Zeile innerhalb einer
Split-Buchung führen (Kassenbuch-Pattern `SplitTarget.ToTransfer`, z. B. eine
Bargeld-Abhebung als Teil eines Supermarkt-Einkaufs). Dafür fehlt die Fremd-Id:
`Server.YnabObservations.Decoders.RawSubtransaction` dekodiert pro Split-Zeile nur
`transfer_account_id`, nicht `transfer_transaction_id`.

Das ist jetzt mit ADR 0046 modelliert und mit Romans ausdrücklicher Entscheidung versehen:
**bauen.** Befund aus einer Prüfung der Entwicklungsdatenbank-Kopie (siehe ADR 0046, Context,
für die Details): **197 nicht gelöschte Buchungen** tragen mindestens eine
Übertrags-Split-Zeile (195 Girokonto, 1 ING Girokonto, 1 Kreditkarte als Quelle; 206
Übertrags-Zeilen insgesamt, 190 Richtung "Bar", 16 Richtung "Depot"). **9 dieser Buchungen
tragen zwei Übertrags-Split-Zeilen gleichzeitig**, teils zum selben Zielkonto. Am konkreten
Beispielpaar Girokonto-Buchung `f668dd4a-4f88-4bbb-9413-f9c1a462a540` (Split-Zeile -100 EUR
Richtung "Bar") und Bar-Buchung `8466b531-94a6-4a52-a14d-655113cef732` (Payee "Transfer :
Girokonto", +100 EUR) zeigt sich: **beide Buchungsköpfe tragen `transferYnabTransactionId =
null`.** Der bestehende Mechanismus aus `ledger-t7v2q` greift hier grundsätzlich nicht, egal
wie oft der Spiegel läuft.

## What

Umsetzung von ADR 0046 (bitte vollständig lesen, dort stehen alle Entscheidungen mit
Begründung). Zusammengefasst:

### Vorab geprüft: die Annahme aus ADR 0046 Punkt 6 hält

Am 2026-10-02 vom Conductor gegen die echte YNAB-API abgerufen
(`GET /budgets/{id}/transactions/f668dd4a-4f88-4bbb-9413-f9c1a462a540`, nur lesend):

- Kopf der Girokonto-Buchung: `transfer_transaction_id = null`.
- Split-Zeile `9152e5f1-…` (-92,09 EUR, Kategorie): `transfer_account_id` und
  `transfer_transaction_id` leer.
- Split-Zeile `f585c0f4-c211-4217-adb6-6be0f6b619f9` (-100,00 EUR): `transfer_account_id =
  dbcbf086-…` (Bar), `transfer_transaction_id = 8466b531-94a6-4a52-a14d-655113cef732`.
- Gegenbuchung `8466b531-…` im Konto Bar (+100,00 EUR): `transfer_transaction_id = null`.

Die `transfer_transaction_id` einer Split-Zeile ist also die YNAB-Id der vollständigen
Gegenbuchung. Die Gegenbuchung trägt keine Rückreferenz, nur die Split-Seite kann verknüpfen
(wie in ADR 0046 angenommen). Kein Schritt 0 mehr für den Worker. Die Werte eignen sich als
Vorlage für den Decoder-Test (Subtransaction-Id `f585c0f4-…` als Adresse der Split-Zeile).

### 1. `Server.YnabObservations.fs`

- `RawSubtransaction` (Zeile 128-136) bekommt zwei neue Felder: die Subtransaction-`id`
  (Pflichtfeld, String) und `transfer_transaction_id` (optional, String).
- Neuer Typ `ObservedSplitTransfer`: `YnabSubtransactionId: string`, `TransferAccountId:
  YnabAccountId`, `TransferYnabTransactionId: string option`, `Amount: Money`.
- `YnabObservation` bekommt ein neues Feld `SplitTransfers: ObservedSplitTransfer list`.
- Die rohe Subtransaction-Liste wird einmal dekodiert und zweimal projiziert: wie bisher auf
  `TransactionSplit list` (unverändert, `Shared.Domain.TransactionSplit`/`SplitTarget` bleiben
  unangetastet) und zusätzlich auf `ObservedSplitTransfer list` (gelöschte Zeilen raus, nur
  Zeilen mit `transfer_account_id`).
- Rund 19 `YnabObservation`-Record-Literale in `LedgerMirrorTests.fs`, `LedgerPageTests.fs`,
  `LedgerTests.fs` brauchen das neue Pflichtfeld (`SplitTransfers = []` als Standardwert, wo
  nicht gebraucht).

### 2. `src/Server/Ledger/Events.fs`

- Neuer Payload-Record `EntrySplitTransferCounterpartObservedInYnabPayload`:
  `YnabSubtransactionId: string`, `CounterpartEntryId: EntryId`,
  `CounterpartYnabTransactionId: string`, `SplitAmount: Money`.
- Neuer DU-Fall `EntrySplitTransferCounterpartObservedInYnab` in `LedgerEventBody`, direkt
  hinter `EntryTransferCounterpartObservedInYnab`, Beobachtungs-Familie, Actor `YnabMirror`.
- `eventTypeName` = `"EntrySplitTransferCounterpartObservedInYnab"`, `schemaVersion` = 1.
- Codec-Paar (`encodeSplitTransferCounterpart`/`decodeSplitTransferCounterpart`), Feldnamen
  `ynabSubtransactionId`, `counterpartEntryId`, `counterpartYnabTransactionId`, `splitAmount`,
  nach dem Muster von `encodeTransferCounterpart`/`decodeTransferCounterpart`.
- Zweige in `encodePayload`/`decodePayload`. `Ledger.Domain.linksFrom`: keine Änderung (keine
  neue Fremd-Id in der Verknüpfungstabelle).
- **Keine bestehende Payload ändert ihre Form, keine bestehende Schema-Version steigt** (ADR
  0046 Punkt 1/2).

### 3. `src/Server/Ledger/Domain.fs`

- `Entry` bekommt ein neues Feld `SplitTransferCounterparts: Map<string, EntryId>` (Schlüssel:
  YNABs Subtransaction-Id). An allen Entstehungs-Stellen in `apply` (drei Stellen laut
  Modellierung) mit `Map.empty` initialisiert.
- Neuer `apply`-Fall:
  ```fsharp
  | EntrySplitTransferCounterpartObservedInYnab p ->
      let e = requireExisting ()
      if p.CounterpartEntryId = event.EntryId then
          let (EntryId id) = event.EntryId
          invalidOp $"Split-Übertrags-Verknüpfung für Buchung {id} zeigt auf sich selbst"
      let others =
          e.SplitTransferCounterparts
          |> Map.filter (fun _ counterpart -> counterpart <> p.CounterpartEntryId)
      { e with SplitTransferCounterparts = others |> Map.add p.YnabSubtransactionId p.CounterpartEntryId }
  ```
  Regeln: kein Entstehungs-Ereignis (`requireExisting`), Selbstverknüpfung wirft, letztes
  Ereignis gewinnt je Schlüssel (Einfügen statt Ersetzen für andere Schlüssel), eine
  Gegenbuchung gehört zu höchstens einer Split-Zeile derselben Buchung (ein anderer
  bestehender Schlüssel mit derselben Gegenbuchung wird beim Einfügen entfernt).

### 4. `src/Server/Ledger/CommandHandlers.fs`

- Neue private Funktion `linkSplitTransferCounterparts now entryId obs`, parallel zu
  `linkTransferCounterpart`, **nicht** als deren Erweiterung.
- Aufruf **in einem Nachlauf nach der Beobachtungs-Schleife** in `runMirror` (vor
  `saveServerKnowledge`), nicht innerhalb der Schleife: `runMirror` sammelt während der
  Schleife je Beobachtung mit nicht leerer `SplitTransfers`-Liste das Paar `(entryId, obs)`
  und ruft danach für jedes Paar die neue Funktion auf. Begründung in ADR 0046 Punkt 6: nur
  die Split-Seite trägt die Fremd-Id, ein Aufruf innerhalb der Schleife fände die Gegenseite
  nicht, wenn sie im selben Batch dahinter liegt, und es gäbe keine zweite Gelegenheit.
- Je `ObservedSplitTransfer` mit `TransferYnabTransactionId = Some tid`:
  1. `Ledger.Persistence.findEntryByLink YnabSystem tid` liefert `Some counterpartId`. Kein
     Treffer ist der Normalfall, kein Fehler.
  2. `counterpartId <> entryId`, sonst stilles Überspringen mit Log-Zeile (wie bei
     `linkTransferCounterpart`, `ledger-v5hkt`).
  3. Ereignis auf der Split-Buchung nur, wenn `Map.tryFind subId e.SplitTransferCounterparts
     <> Some counterpartId`. Ereignis auf der Gegenbuchung (bestehendes
     `EntryTransferCounterpartObservedInYnab`) nur, wenn deren `TransferEntryId <> Some
     entryId`. Heilt den halbseitigen Fall.
  4. Alle Ereignisse einer Beobachtung (bis zu zwei je Übertrags-Zeile) in einen
     `appendAndApply`-Aufruf.
  5. Zählt nicht in `newEntries`/`changedEntries`/`deletedEntries`, wie bei
     `linkTransferCounterpart`.

### 5. `src/Server/Ledger/Persistence.fs`

- Neue Spalte `split_transfer_counterparts TEXT` auf `ledger_entries` per `migrateColumn`
  (Muster wie `transfer_entry_id`), JSON-Liste von `{ ynabSubtransactionId,
  counterpartEntryId }`, `NULL`/fehlend liest sich als leere `Map`.
- `replayAll` muss die neue Spalte aus dem Log rekonstruieren (Pflichttest, ADR 0013).

### 6. `src/Server/Ledger/QueryHandlers.fs`

- Neuer `describe`-Zweig `describeSplitTransferCounterpart`, Satz in der Form "Split-Zeile
  über {Betrag} als Übertrag mit einer anderen Buchung verbunden". Ohne diesen Zweig ist der
  Pattern-Match auf `LedgerEventBody` unvollständig (FS0025).

### 7. `.agentheim/knowledge/contexts/ledger/README.md`

**Nicht vom Worker selbst einspielen** (README ist `.agentheim/`-Pfad, Worker dürfen den
nicht schreiben, ADR 0032 Fassung ghcaj). Der Worker meldet den README-Delta im
RESULT-Block, der Conductor materialisiert ihn. Inhalt (siehe ADR 0046 "Ubiquitous language
additions" für den vollen Wortlaut-Vorschlag, im Umbruchstil der Datei, siehe `ledger-v8n4t`
für das Gebot "keine Zeile über 100 Zeichen"):

- Ubiquitous language: "Split-Zeilen-Übertrag" und "Split-Übertrags-Verknüpfung".
- Katalog-Eintrag Beobachtungen, nach `EntryTransferCounterpartObservedInYnab`:
  `EntrySplitTransferCounterpartObservedInYnab` mit Payload-Beschreibung.
- Ergänzung im Absatz **Übertrags-Verknüpfung**: Nachlauf-Zeitpunkt, 0..n-Kardinalität,
  gelöschte Split-Zeilen behalten ihre Verknüpfung.

## Acceptance criteria
- [ ] `RawSubtransaction`/`subtransactionsDecoder` liest YNABs Subtransaction-`id` und
      `transfer_transaction_id` und lässt gelöschte Zeilen weiterhin weg (Regressionstest: ein
      Decoder-Test mit einer gelöschten Transfer-Subtransaction liefert diese Zeile nicht in
      `ObservedSplitTransfer list`).
- [ ] `Ledger.Domain.observe` liefert für eine **unverändert erneut beobachtete**
      Split-Buchung mit Übertrags-Zeile **kein** Ereignis (Regressionsschutz: das neue Feld auf
      `YnabObservation` darf `observe`s Splits-Vergleich nicht verändern).
- [ ] `apply` auf `EntrySplitTransferCounterpartObservedInYnab`, Feld für Feld geprüft:
      Einfügen eines neuen Schlüssels, Überschreiben desselben Schlüssels mit anderer
      Gegenbuchung (kein Fehler), eine zweite, andere Split-Zeile derselben Buchung bleibt
      beim Einfügen unberührt, ein neuer Schlüssel mit einer bereits verknüpften Gegenbuchung
      entfernt den alten Schlüssel, Selbstverknüpfung wirft, Ereignis ohne vorangehende
      Erstbeobachtung wirft.
- [ ] Codec-Roundtrip für die neue Payload in Schema-Version 1.
- [ ] Ein Spiegel-Lauf mit der Split-Buchung **vor** ihrer Gegenbuchung im selben
      Beobachtungs-Batch verknüpft am Ende beide Seiten (belegt den Nachlauf-Mechanismus:
      innerhalb der Schleife allein würde das nicht gelingen).
- [ ] Ein Spiegel-Lauf mit zwei Übertrags-Zeilen derselben Buchung zum selben Zielkonto
      erzeugt zwei getrennte Einträge in `SplitTransferCounterparts` mit unterschiedlichen
      Subtransaction-Ids als Schlüssel.
- [ ] Ein zweiter, identischer Lauf erzeugt keine weiteren Verknüpfungs-Ereignisse (Idempotenz).
- [ ] Fehlt die Gegenbuchung im Kassenbuch (kein `findEntryByLink`-Treffer), entsteht kein
      Ereignis und kein Fehler.
- [ ] `replayAll` reproduziert `SplitTransferCounterparts` identisch zur Zustandstabelle
      (Pflichttest, ADR 0013).
- [ ] `describe` liefert für das neue Ereignis einen lesbaren Satz (kein Typname), Test in
      `LedgerPageTests.fs` analog zum bestehenden Fall für `EntryTransferCounterpartObservedInYnab`.
- [ ] Tests ausschließlich gegen In-Memory-SQLite und Fake-Fetcher (ADR 0030), nie gegen die
      Entwicklungsdatenbank oder die Live-Datenbank.
- [ ] `dotnet build` und `dotnet test` grün.
- [ ] README-Delta für `.agentheim/knowledge/contexts/ledger/README.md` im RESULT-Block
      gemeldet (Worker schreibt den Pfad nicht selbst), Inhalt wie oben unter "7." beschrieben.

## Notes

Modelliert am 2026-10-02 vom tactical-modeler, siehe ADR 0046 für die vollständige Begründung
und die geprüften Alternativen. Empirische Grundlage: eine Prüfung der
Entwicklungsdatenbank-**Kopie** (nie der Live-Datenbank), Details in ADR 0046 Context.

**Bewusst nicht Teil dieses Tasks:** die rückwirkende Verknüpfung der 197 historischen
Bestandsfälle auf der Live-Datenbank (ADR 0046 Punkt 7, braucht einen einmaligen Vollabruf
nach Deploy und ist als eigener Folge-Task mit `depends_on: [ledger-h4nxz]` vorgesehen, vom
Conductor aus dem Scratchpad-Entwurf `draft-split-transfer-backfill.md` anzulegen).

**Randfund, nicht Teil dieses Tasks:** die zusammengefasste Sicht "ein Übertrag ist eine
Bewegung" bleibt v2-Arbeit (ADR 0015). Für Split-Zeilen-Überträge heißt das zusätzlich: eine
Seite ist eine Split-Zeile, spiegelbildlich sind Zeilen-Betrag und Betrag der Gegenbuchung,
nicht zwei ganze Buchungsbeträge. Für die v2-Modellierung festzuhalten, nicht jetzt zu bauen.