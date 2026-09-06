---
id: ynab-r9k4t
title: "Bug: since_date-Transaktions-Decoder verschiebt YNAB-Buchungstage um den lokalen Zeitzonen-Offset"
status: backlog
type: bug
context: ynab-sync
created: 2026-09-06
depends_on: []
blocks: []
tags: [backend, bug]
related_adrs: []
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
`Server.YnabClient.Decoders.transactionDecoder` liest YNABs `date` mit
`Decode.datetimeUtc`. YNAB liefert einen reinen Kalendertag ohne Zeitzone ("2026-02-03");
`Decode.datetimeUtc` interpretiert ihn als **Ortszeit** und rechnet in UTC um. In MEZ wird
daraus der 2. Februar, 23:00 — der Buchungstag ist um einen Tag zu früh.

Gefunden beim Bau des Spiegels (`ledger-m8r2v`): der dortige Decoder-Test schlug genau
deshalb fehl. Der Spiegel hat einen eigenen, korrekten `ynabDateDecoder` bekommen
(`src/Server/YnabObservations.fs`, `TryParseExact` mit `DateTimeKind.Utc`). Der alte Pfad
blieb bewusst unangetastet, weil er außerhalb des Slice-Scopes lag.

Betroffen sind die Nutzer von `getAccountTransactions`: die Duplikat-Erkennung
(`DuplicateDetection`, Datumsfenster-Vergleich) und die Quick-Add-Vorlagen.

## What
1. Den Datumsteil von `transactionDecoder` auf denselben Kalendertag-Decoder umstellen wie
   der Spiegel (gemeinsame Funktion statt zweiter Kopie).
2. Prüfen, ob die Duplikat-Erkennung sich auf den Versatz eingestellt hat (Toleranzfenster,
   Off-by-one-Kompensation); solche Kompensationen mit entfernen, sonst kippt der Fehler
   nur auf die andere Seite.

## Acceptance criteria
- [ ] Regressions-Test: `"2026-02-03"` dekodiert zu `2026-02-03 00:00:00 Utc`, unabhängig
      von der Zeitzone der Maschine (Test schlägt vor dem Fix fehl).
- [ ] Duplikat-Erkennung und Quick-Add-Vorlagen bleiben mit korrigierten Datumswerten grün;
      etwaige Off-by-one-Kompensationen sind entfernt statt verdoppelt.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Der Fix ist klein, die Wirkung liegt in den Nutzern des Datums. Vorbild und korrekter
Decoder: `Server.YnabObservations.Decoders.ynabDateDecoder`.
Id-Präfix beim Materialisieren von `ledger-r9k4t` auf `ynab-r9k4t` korrigiert: der Task
gehört nach `ynab-sync`, dessen Ids `ynab-` tragen (ynab-czk34, ynab-b7q2m).
