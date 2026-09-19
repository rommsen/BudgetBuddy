---
id: ledger-h4nxz
title: "Split-Zeilen-Überträge: transfer_transaction_id auf Subtransaction-Ebene fehlt im Beobachtungs-Contract"
status: backlog
type: decision
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [ledger, spiegel, uebertrag, split]
related_adrs: [0013, 0015]
related_research: [ynab-transfer-in-split-subtransaction-2026-06-13]
prior_art: [ledger-t7v2q]
---

## Why
`ledger-t7v2q` hat `EntryTransferCounterpartObservedInYnab` gebaut: die Verknüpfung zweier
Buchungen desselben Übertrags über YNABs `transfer_transaction_id`. Das Ereignis liest die
Fremd-Id ausschließlich aus dem Buchungs-Kopf (`YnabObservation.TransferYnabTransactionId`,
`Server.YnabObservations`), nicht aus einer Split-Zeile.

Der Modellierer hatte das als "Offener Punkt 2" markiert: YNAB kann einen Übertrag auch als
Zeile innerhalb einer Split-Buchung führen (das Kassenbuch kennt dieses Muster bereits als
Ziel `ToTransfer` in `TransactionSplit`, z. B. eine Bargeld-Abhebung als Teil eines
Supermarkt-Einkaufs). Die Recherche `ynab-transfer-in-split-subtransaction-2026-06-13`
bestätigt für den *Push*-Pfad, dass YNABs Subtransaction-Read-Modell pro Split-Zeile sowohl
`transfer_account_id` als auch `transfer_transaction_id` führt (Quelle: offizielles Ruby-SDK,
`SubTransaction`-Schema).

Der Lese-Contract des Spiegels holt das nicht ab: `Server.YnabObservations.Decoders.RawSubtransaction`
dekodiert nur `transfer_account_id` je Split-Zeile (für `ToTransfer` im Snapshot), aber kein
`transfer_transaction_id`. Ein Übertrag, der als Split-Zeile geführt wird, kann also heute
weder von `EntryTransferCounterpartObservedInYnab` noch von irgendeinem anderen Mechanismus
mit seiner Gegenbuchung verknüpft werden — nicht, weil das Ereignis das ausschließt, sondern
weil die Fremd-Id auf dem Weg vom YNAB-Delta zum Snapshot verloren geht.

## What
Zu klären, mit dem tactical-modeler (laut `ledger-t7v2q` "eine eigene Modellierungsfrage und
nicht in diesem Ereignis zu verstecken"):

1. Wie oft/ob Split-Zeilen-Überträge für Romans tatsächliche Konten vorkommen (die
   Bargeld-Abhebung beim Einkauf ist der bekannte Fall aus ADR 0015s Kontext).
2. Ob `YnabObservation`/`TransactionSplit` um eine Split-Zeilen-`transfer_transaction_id`
   erweitert wird (Contract-Änderung in `Server.YnabObservations`, `Shared.Domain`) und wie
   `EntryTransferCounterpartObservedInYnab` (oder ein Geschwister-Ereignis) dann eine
   Split-Zeile statt eine ganze Buchung als Gegenbuchungs-Ziel adressiert — die heutige
   Payload trägt nur `CounterpartEntryId`, keine Split-Zeilen-Adresse.
3. Oder ob eine bewusste, dokumentierte Beschränkung auf Überträge auf Buchungsebene für v1
   genügt (das ist der Status quo nach `ledger-t7v2q`) und dieser Task nur die Lücke im
   README/ADR festhält, ohne Code zu ändern.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Randfund aus `ledger-t7v2q` (Offener Punkt 2, Task-Notes). Kein Bug im gebauten Ereignis:
`EntryTransferCounterpartObservedInYnab` deckt genau das ab, was der heutige
Beobachtungs-Contract liefert (Überträge auf Buchungsebene), und tut das korrekt.