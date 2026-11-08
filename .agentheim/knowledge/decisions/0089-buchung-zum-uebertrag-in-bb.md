---
id: 0089
title: Buchung in BB zum Übertrag machen und zurück, zwei Ereignisse, Payee vor dem Übertrag, Baseline mit Ziel
scope: ledger
status: accepted
date: 2026-11-08
supersedes: []
superseded_by: []
related_tasks: [ledger-qvjvs]
related_adrs: [0021, 0048, 0055, 0087]
related_research: []
---

# ADR 0089: Buchung in BB zum Übertrag machen und zurück

## Context
ADR 0055 Punkt 7 sperrt Überträge zwischen Budget-Konten für den Kategorie-Picker. Etappe 2 verlangt, dass Roman eine einzelne Buchung in BB zum Übertrag machen und wieder zurückholen kann (Prototyp Version 17). `EntryRecategorized` setzt Kategorie, Splits und Payee, kennt aber das Übertrags-Ziel nicht; es gibt Bestandsereignisse dieses Typs auf Überträgen in Tracking-Konten mit Kategorie, deshalb darf seine Faltung keine Übertrags-Felder leeren. `observe` zieht `TransferAccountId` auf bestehenden Buchungen nicht nach. YNAB macht eine Buchung per PUT mit `payee_id` = `transfer_payee_id` des Zielkontos zum Übertrag und nennt den Payee dann "Transfer : <Konto>"; zurück geht es nur mit einem Payee, der kein Übertrags-Payee ist (`payee_id` null plus `payee_name`). Befragt: tactical-modeler (direkt, ADR 0035).

## Decision
1. Zwei neue Entscheidungs-Ereignisse, Actor `UserInBb`, Version 1: `EntryTransferTargetSetInBb` (zum Übertrag oder Zielwechsel; trägt `NewTransferAccountId`, `NewTransferAccountName`, `TransferPayeeId`, `NewPayee` = "Transfer : <Name>" und die alten Werte) und `EntryTransferTargetClearedInBb` (zurück; `NewCategory` als Option, damit auch "ohne Kategorie" als Rückgängig geht, `NewPayee` = Payee aus BB). `EntryRecategorized` bleibt unverändert.
2. `NewPayee` folgt YNABs Namen, damit der Spiegel nach dem Push keine Payee-Änderung beobachtet.
3. `Entry.PayeeBeforeTransfer` (Spalte `payee_before_transfer`) merkt den Payee aus BB vor dem ersten Übertrag; ein Zielwechsel behält ihn. Ein Übertrag aus YNAB hat keinen; dann lehnt der Weg zurück ab ("BB kennt den Payee vor dem Übertrag nicht, bitte in YNAB ändern"), ein neues Ziel geht.
4. Der Weg zurück ist `setEntryCategory` (der Client schickt bei einer Kategorie weiter diesen Befehl); bei einem Übertrag zwischen Budget-Konten schreibt er `EntryTransferTargetClearedInBb` und sendet `category_id`, `payee_id` null und `payee_name`. ADR 0055 Punkt 7 wird per Nachtrag (2026-11-08) eingeschränkt: die Sperre gilt nur noch für die Sammelaktion.
5. Fehlschlag wie ADR 0055 Punkt 4 über `EntryCategorizationWriteBackFailed`, mit dem optionalen Feld `BaselineTransferAccountId` (nur geschrieben, wenn gesetzt; Version 1 bleibt lesbar). `observe` vergleicht bei offenem Fehlschlag das Ziel mit, aber nur wenn die Baseline ein Ziel trägt oder der BB-Stand ein in BB gesetzter Übertrag ist; ohne offenen Fehlschlag bleibt der Vergleich wie bisher.
6. Die YNAB-Seite ist ein Port `TransferWriteTools` (Konten mit `transfer_payee_id`, PUT). Ein 200 ohne `transfer_account_id` gilt als Fehlschlag.

## Consequences
- Gut: Replay und Bestandsereignisse bleiben unberührt; der Spiegel verknüpft die Gegenbuchung über den vorhandenen Weg; Rückgängig geht in beide Richtungen.
- Schlecht oder offen: eine neue Spalte; ein Übertrag aus YNAB lässt sich in BB nicht zurück auf eine Kategorie holen; `observe` zieht ein in YNAB geändertes Ziel auf einer bestehenden Buchung weiter nicht nach.

## Alternatives considered
- `EntryRecategorized` erweitern: verworfen, die Faltung bestehender Ereignisse auf Tracking-Überträgen würde sich ändern.
- Payee vor dem Übertrag aus der Historie lesen statt als Feld: verworfen, `apply` soll den Zustand allein tragen.
- `category_id: null` beim Übertrag mitsenden: verworfen, die Task verlangt den Body wie bei Split-Zielen (nur die Payee).
