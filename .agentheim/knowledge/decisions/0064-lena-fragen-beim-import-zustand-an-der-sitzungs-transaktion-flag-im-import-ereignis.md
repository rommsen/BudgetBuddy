---
id: ADR-0064
title: Lena fragen beim Import: Zustand an der Sitzungs-Transaktion, Flag im Import-Ereignis
scope: ynab-sync
status: accepted
date: 2026-10-26
related_tasks: [ynab-sync-d9e51]
related_adrs: [0021, 0056, 0061]
---

# ADR-0064: Lena fragen beim Import, Zustand an der Sitzungs-Transaktion, Flag im Import-Ereignis

## Context

Roman will die Rückfrage Lena schon in Import prüfen setzen (ynab-sync-d9e51), bevor die
Buchung nach YNAB geht. ADR 0056 beschreibt das Setzen im Kassenbuch als eigenes Ereignis
`EntryFlagChangedInBb` mit anschließendem Einzel-Push. Beim Import gibt es noch keine Buchung:
sie entsteht erst im Import-Push (ADR 0021, Kassenbuch vor Push). Offen war, wo der Zustand
bis dahin liegt und wie die Buchung mit Flag entsteht.

## Decision

1. **Zustand:** `SyncTransaction.AskLena: bool`, gesetzt über `SyncApi.setAskLena`
   (`Server.ImportAskLena.setAskLena`, rein). Die Zeile speichert nur `AskLena`;
   die Flag-Farbe liest erst der Push aus `lena_flag_color` (`YnabClient.applyLenaFlag`).
   So gilt immer die aktuelle Einstellung; fehlt sie bis dahin, geht die Zeile ohne Flag und
   `ImportResult.LenaFlagNotSentCount` meldet das im Bericht.
2. **Sitzungsgebunden:** `AskLena` wird nicht in `sync_transactions` persistiert (wie `Splits`
   und `SuggestedByOrderId`), eine Spalte wäre ein Schema-Eingriff ohne Nutzen für den Ablauf
   "prüfen, importieren". Eine aus der Datenbank wiederhergestellte Sitzung liest `false`.
3. **Kassenbuch:** Die Buchung entsteht mit Flag. `EntryImportedFromComdirectPayload` bekommt
   die optionalen Felder `FlagColor` und `FlagName` ("Lena"), gleiche Schema-Version 2: ohne
   Flag fehlen die Schlüssel im JSON, ein altes Ereignis liest sich als "kein Flag". Es entsteht
   kein zusätzliches `EntryFlagChangedInBb`, weil kein Vorzustand existiert, den ein Ereignis
   ablösen müsste; die Historie nennt die Herkunft im Satz des Import-Ereignisses
   ("Lena gefragt beim Import").
4. **Push:** Der Create-Body trägt `flag_color` nur für Zeilen mit Rückfrage. Nur die Farbe
   geht hinaus, nie der Name (ADR 0056). Erneut senden (`retryPushToYnab`) schickt die Farbe
   des BB-Stands (`pushFlagColorOf`) im Einzel-Post mit; ebenso Force-Import.

## Consequences

- Neues Pflichtfeld `AskLena` in `SyncTransaction` und `LenaFlagNotSentCount` in `ImportResult`:
  beide fließen nur vom Server zum Client (kein Client-zu-Server-Pfad deserialisiert diese
  Records). Eine alte installierte PWA liest nur die Felder, die sie kennt, und überspringt die
  neuen Schlüssel; ein serverseitiger Rückfall für alte Clients ist deshalb nicht nötig.
- Ein Server-Neustart zwischen Setzen und Import verliert die Rückfrage (Sitzung selbst ist
  im Speicher); der Nutzer setzt sie neu.
- Der erste Push mit Flag und das Spiegeln desselben Flags aus YNAB stimmen überein
  (Farbe "Lena"-benannt), `EntryFlagChangeObservedInYnab` entsteht dann nicht.
