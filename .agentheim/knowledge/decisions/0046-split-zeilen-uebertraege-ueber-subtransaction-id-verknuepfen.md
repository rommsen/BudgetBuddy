---
id: 0046
title: Split-Zeilen-Überträge werden über YNABs Subtransaction-Id verknüpft, nicht über den Buchungskopf
scope: ledger
status: accepted
date: 2026-10-02
supersedes: []
superseded_by: []
related_tasks: [ledger-h4nxz]
related_research: [ynab-transfer-in-split-subtransaction-2026-06-13]
---

## Context

`ledger-t7v2q` hat `EntryTransferCounterpartObservedInYnab` gebaut: zwei Buchungen desselben
Übertrags werden über YNABs `transfer_transaction_id` am Buchungskopf verknüpft (ADR 0015).
Dieser Mechanismus deckt nur Überträge ab, die YNAB als zwei vollständige Top-Level-Buchungen
führt.

YNAB kennt einen zweiten Weg: einen Übertrag als Split-Zeile innerhalb einer Buchung (das
Kassenbuch kennt dieses Muster als `SplitTarget.ToTransfer`, z. B. eine Bargeld-Abhebung als
Teil eines Supermarkt-Einkaufs). Dabei trägt laut Recherche
`ynab-transfer-in-split-subtransaction-2026-06-13` YNABs `SubTransaction`-Schema pro Split-Zeile
sowohl `transfer_account_id` als auch `transfer_transaction_id`.

Eine Prüfung der Entwicklungsdatenbank-Kopie (nicht die Live-Datenbank, nur lesend) bestätigt
das Ausmaß und eine wichtige Einschränkung:

- **197 nicht gelöschte Buchungen** tragen mindestens eine Split-Zeile mit Übertrags-Ziel (195
  auf dem Konto Girokonto, 1 ING Girokonto, 1 Kreditkarte als Quelle), mit 206 Übertrags-Zeilen
  insgesamt (190 Richtung "Bar", 16 Richtung "Depot"). **9 dieser 197 Buchungen tragen zwei
  Übertrags-Split-Zeilen gleichzeitig**, teils zum selben Zielkonto (z. B. zwei getrennte
  Zeilen von je ca. -250 EUR Richtung Depot am selben Tag). Die Kardinalität von
  Split-Zeilen-Überträgen je Buchung ist damit 0..n, nicht 0..1.
- Am konkreten Beispielpaar Girokonto-Buchung `f668dd4a-4f88-4bbb-9413-f9c1a462a540`
  (Split-Zeile -100 EUR Richtung "Bar") und der zugehörigen Bar-Buchung
  `8466b531-94a6-4a52-a14d-655113cef732` (Payee "Transfer : Girokonto", +100 EUR) zeigt sich:
  **beide Buchungsköpfe tragen `transferYnabTransactionId = null`.** Die Bar-Buchung trägt am
  Kopf zwar `transferAccountId` (zeigt auf Girokontos YNAB-Konto), aber keine
  `transfer_transaction_id`. Die Kreuz-Verknüpfung kann also, wenn YNAB sie überhaupt führt,
  ausschließlich auf der Split-Zeile selbst stehen. Der bestehende Mechanismus aus
  `ledger-t7v2q` greift hier grundsätzlich nicht, weil er ein Kopf-Feld braucht, das bei
  Split-Zeilen-Übertragungen nie gesetzt ist.
- `Server.YnabObservations.Decoders.RawSubtransaction` liest heute weder YNABs
  Subtransaction-`id` noch `transfer_transaction_id` je Split-Zeile.
- Ob die `transfer_transaction_id` einer Split-Zeile tatsächlich die YNAB-Id der ganzen
  Gegenbuchung ist (wie beim Beispielpaar zu erwarten, aber aus der Datenbank-Kopie nicht
  ableitbar, da das Feld nie dekodiert wurde), war beim Entwurf eine Annahme; am 2026-10-02 gegen die
  echte YNAB-API bestätigt (siehe Decision, Punkt 6).

ADR 0013 verlangt: ein einmal gespeichertes Ereignis ändert nie seine Bedeutung, eine neue
Payload-Form braucht einen neuen Event-Typ oder eine neue Version. `TransactionSplit` und
`SplitTarget` stecken über einen gemeinsamen Codec in vier persistierten Payloads
(`EntryObservedInYnab`, `EntryCategorizationChangeObservedInYnab`, `EntryCategorizedByRule`,
`EntryRecategorized`). `Ledger.Domain.observe` erkennt eine Kategorisierungs-Änderung über
strukturellen Vergleich der Splits (`entry.Splits <> obs.Splits`).

## Decision

1. **Der Beobachtungs-Contract bekommt eine eigene, vom Snapshot getrennte Liste.**
   `Server.YnabObservations.YnabObservation` erhält ein neues Feld `SplitTransfers:
   ObservedSplitTransfer list` mit je Übertrags-Split-Zeile: YNABs Subtransaction-Id,
   Zielkonto (`transfer_account_id`), `transfer_transaction_id` und Betrag. `RawSubtransaction`
   liest zusätzlich die Subtransaction-`id` (Pflicht) und `transfer_transaction_id`
   (optional). Die rohe Liste wird einmal dekodiert und zweimal projiziert: wie bisher auf
   `TransactionSplit list` (unverändert) und zusätzlich auf `ObservedSplitTransfer list`.
   `Shared.Domain.TransactionSplit` und `SplitTarget` bleiben **unverändert**.

   Begründung: Ein neues Feld direkt auf `TransactionSplit`/`SplitTarget` würde über
   `entry.Splits <> obs.Splits` bei jeder erneuten Beobachtung einer unveränderten
   Split-Buchung ein falsches `EntryCategorizationChangeObservedInYnab` auslösen. Es würde
   außerdem den Push-Pfad und den Client mitziehen (`buildCashbackSplit`, `SplitSheet`,
   `YnabClient`-Encoding), wo eine YNAB-Fremd-Id nichts verloren hat: BB legt Split-Zeilen an,
   bevor YNAB Ids vergibt (ADR 0006). Nach ADR 0015 sind Fremd-Ids ohnehin Verknüpfungen, kein
   Teil der Buchungsdaten.

2. **Ein additives optionales Feld in einer bestehenden persistierten Payload gilt in diesem
   Projekt als neue Schema-Version, nicht als stiller Zusatz.** Ein fehlendes Feld hieße in
   alten Payloads "damals nicht gelesen", in neuen "YNAB meldet keine Gegenbuchung": dieselbe
   Leerstelle mit zwei Bedeutungen, was ADR 0013 Punkt 7 ausschließt. Das ist dieselbe Praxis
   wie in ADR 0032 (Version 2 für `approved`/Flag-Felder). Punkt 1 vermeidet diesen Fall von
   vornherein: keine bestehende Payload-Form ändert sich, keine Version steigt.

3. **Eine Split-Zeile wird über YNABs Subtransaction-Id adressiert, nicht über ihre Position
   in der Liste.** Der Index ist brüchig: der Decoder filtert gelöschte Zeilen heraus, YNAB
   garantiert keine Reihenfolge, und es gibt (siehe Context) Buchungen mit zwei
   Übertrags-Zeilen zum selben Zielkonto, die sich über Position oder Zielkonto allein nicht
   unterscheiden lassen. `Entry` bekommt ein neues Feld `SplitTransferCounterparts:
   Map<string, EntryId>` (Schlüssel: YNABs Subtransaction-Id), Kardinalität 0..n.
   `Entry.TransferEntryId` bleibt unverändert für den Übertrag auf Buchungsebene; beide Felder
   schließen sich fachlich aus (YNAB kann einen Übertrag nicht splitten), das wird aber nicht
   als Invariante in `apply` erzwungen.

4. **Neues Beobachtungs-Ereignis `EntrySplitTransferCounterpartObservedInYnab`** (Actor
   `YnabMirror`, Schema-Version 1) auf der Buchung mit der Split-Zeile. Payload: die
   Subtransaction-Id, die Buchungs-Id der Gegenbuchung, die YNAB-Transaction-Id, über die
   aufgelöst wurde, und der Betrag der Split-Zeile (für den Historien-Satz, weil zwei
   Übertrags-Zeilen zum selben Konto sich sonst in der Historie nicht unterscheiden lassen).

   Ein optionales Adressfeld am bestehenden `EntryTransferCounterpartObservedInYnab` wäre
   abgelehnt: das wäre zugleich eine neue Payload-Form und eine neue Bedeutung unter
   demselben Namen ("diese ganze Buchung ist ein Übertrag" versus "eine Zeile dieser Buchung
   ist ein Übertrag"), was ADR 0013 Punkt 7 ausschließt.

   **Die Gegenbuchung** (z. B. die Bar-Buchung) bekommt das bestehende
   `EntryTransferCounterpartObservedInYnab` mit unveränderter Payload, denn sie ist als Ganzes
   ein Übertrag und ihre Gegenbuchung ist die Buchung mit der Split-Zeile. Das ist genau der
   bestehende Mechanismus aus `ledger-t7v2q`, keine neue Bedeutung.

5. **`apply`** übernimmt die Regeln des bestehenden Falls (kein Entstehungs-Ereignis,
   `requireExisting`; Selbstverknüpfung wirft; letztes Ereignis gewinnt), jetzt je Schlüssel
   der `Map`. Neu wegen der 0..n-Kardinalität: eine Gegenbuchung gehört zu höchstens einer
   Split-Zeile derselben Buchung; zeigt ein anderer Schlüssel bereits auf dieselbe
   Gegenbuchung, wird er beim Einfügen entfernt (deckt den Fall ab, dass YNAB eine Split-Zeile
   neu anlegt, neue Subtransaction-Id, dieselbe Gegenbuchung). Eine gelöschte Split-Zeile
   behält ihre Verknüpfung, wie beim Übertrag auf Buchungsebene (dieselbe bewusste Lücke, siehe
   ADR-Text zu `ledger-t7v2q`).

6. **Der Spiegel-Lauf verknüpft Split-Zeilen-Überträge in einem Nachlauf nach der
   Beobachtungs-Schleife**, nicht innerhalb ihr (anders als `linkTransferCounterpart`). Grund:
   nur die Split-Seite trägt die Fremd-Id; liegt die Gegenbuchung im selben Batch vor der
   Split-Buchung, fände ein Aufruf innerhalb der Schleife sie noch nicht, und es gäbe keine
   zweite Gelegenheit, weil kein künftiges Delta beide erneut liefert. Der Lookup ist der
   bestehende `Ledger.Persistence.findEntryByLink YnabSystem`, **unter der Annahme**, dass die
   `transfer_transaction_id` einer Split-Zeile die YNAB-Id der vollständigen Gegenbuchung ist
   (gestützt durch YNABs Schema-Text "the id of transaction on the other side of the transfer"
   und das Beispielpaar aus dem Context, aber nicht belegt, da nie dekodiert).

   **Geprüft am 2026-10-02** (Abruf der Girokonto-Buchung
   `f668dd4a-4f88-4bbb-9413-f9c1a462a540` gegen die echte YNAB-API): die Split-Zeile Richtung
   "Bar" trägt `transfer_transaction_id = 8466b531-94a6-4a52-a14d-655113cef732`, die Id der
   vollständigen Gegenbuchung. Kopf der Split-Buchung und Gegenbuchung tragen beide
   `transfer_transaction_id = null`. Die Annahme hält, diese Entscheidung gilt unverändert.

7. **Bestandsfälle werden einmalig über einen Vollabruf nachverknüpft**, nicht über das
   normale Delta: die 197 Buchungen stehen größtenteils seit Jahren unverändert, ein Delta seit
   dem letzten `server_knowledge` bringt sie nie wieder vorbei, und die Subtransaction-Ids
   stehen nirgends im bestehenden Log (Punkt 1), ein Nachtrag allein aus der Datenbank ist
   also ausgeschlossen. Der Vollabruf ist technisch bereits vorhanden: `server_knowledge =
   None` führt zu `since_date=1900-01-01`, nicht zur rund-12-Monate-Grenze ohne `since_date`
   (die Faustregel träfe nur einen anderen, hier nicht benutzten Pfad). Ein dauerhafter
   dauernder Vollabgleich bleibt abgelehnt (ADR 0014): das ist ein einmaliger,
   skriptgeführter Schritt nach dem Deploy, kein neues Feature.

## Consequences

### Positive
- Split-Zeilen-Überträge sind vollständig modelliert, ohne den Snapshot-Vergleich oder den
  Push-Pfad zu berühren.
- Die Subtransaction-Id als Schlüssel ist robust gegen Löschungen und Reihenfolge-Änderungen
  anderer Split-Zeilen.
- Keine bestehende Event-Payload ändert ihre Form, keine Schema-Version steigt.

### Negative
- Zwei parallele Verknüpfungs-Mechanismen im Spiegel-Lauf (`linkTransferCounterpart` in der
  Schleife, `linkSplitTransferCounterparts` im Nachlauf), statt einer einzigen Erweiterung.
- Subtransaction-Ids stehen im Log nur in den neuen Verknüpfungs-Ereignissen, nicht im
  Snapshot: eine Split-Zeile ohne aufgelöste Gegenbuchung hinterlässt keine Spur ihrer
  Fremd-Id (dieselbe Lage wie bei `EntryLinkedToYnab`).

### Neutral
- `Entry.SplitTransferCounterparts` und `Entry.TransferEntryId` sind unabhängige Felder; ihr
  fachlicher Gegensatz (ein Übertrag ist entweder Kopf oder Split-Zeile, nie beides) wird nicht
  erzwungen, weil es YNABs Regel ist, nicht die des Kassenbuchs.
- Die zusammengefasste Sicht "ein Übertrag ist eine Bewegung" bleibt v2-Arbeit, wie in ADR
  0015 festgehalten; diese Entscheidung ändert daran nichts.

## Alternatives considered

- **`transfer_transaction_id` als zusätzliches optionales Feld direkt auf
  `SplitTarget.ToTransfer`**: abgelehnt. Löst über `Ledger.Domain.observe`s strukturellen
  Splits-Vergleich bei jeder erneuten Beobachtung einer unveränderten Split-Buchung ein
  falsches `EntryCategorizationChangeObservedInYnab` aus, zieht den Push-Pfad und den Client
  unnötig mit und würde eine neue Schema-Version für vier Event-Typen erzwingen, die mit
  YNAB-Fremd-Ids nichts zu tun haben.
- **Split-Zeile über Listenindex statt Subtransaction-Id adressieren**: abgelehnt, weil der
  Decoder gelöschte Zeilen herausfiltert, YNAB keine Reihenfolge garantiert und mindestens
  9 real existierende Buchungen zwei Übertrags-Zeilen zum selben Konto tragen, die sich über
  Position oder Zielkonto nicht unterscheiden lassen.
- **Das bestehende `EntryTransferCounterpartObservedInYnab` um ein optionales
  Split-Adressfeld erweitern statt eines neuen Ereignisses**: abgelehnt, weil es eine neue
  Payload-Form und eine neue Bedeutung unter demselben Namen wäre (ADR 0013 Punkt 7).
- **Verknüpfung innerhalb der bestehenden Beobachtungs-Schleife statt in einem Nachlauf**:
  abgelehnt, weil nur die Split-Seite die Fremd-Id trägt; liegt die Gegenbuchung im selben
  Batch vor der Split-Buchung, fände ein Aufruf innerhalb der Schleife sie nicht, und kein
  künftiges Delta brächte beide erneut vorbei.
- **Rückwirkende Verknüpfung der Bestandsfälle als dauerhafter Vollabgleich pro Lauf**:
  abgelehnt (ADR 0014 lehnt das grundsätzlich ab); stattdessen ein einmaliger,
  skriptgeführter Vollabruf nach dem Deploy.
