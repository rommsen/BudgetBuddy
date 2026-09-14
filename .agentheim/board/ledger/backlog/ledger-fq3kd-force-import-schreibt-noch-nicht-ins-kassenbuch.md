---
id: ledger-fq3kd
title: Force-Import (Duplikat erneut pushen) schreibt noch nicht ins Kassenbuch
status: backlog
type: feature
context: ledger
created: 2026-09-11
depends_on: []
blocks: []
tags: [ledger, ynab-sync, push, integration]
related_adrs: [0021]
related_research: []
prior_art: [ledger-qvt84]
---

## Why
`ledger-qvt84` hat den normalen Import-Push (`importToYnab`) auf "erst Kassenbuch, dann
YNAB, dann Verknüpfung" umgestellt (ADR 0021). `forceImportDuplicates` (der
"trotzdem importieren"-Knopf für YNAB-Duplikate) ruft weiterhin direkt
`YnabClient.createTransactions` auf und schreibt keine Kassenbuch-Buchung. Eine per
Force-Import angelegte YNAB-Buchung taucht damit erst über den nächsten Spiegel-Lauf mit
falscher Herkunft ("YNAB-Spiegel" statt "Comdirect-Import") im Kassenbuch auf, genau das
Muster, das ADR 0021 für den normalen Import bereits vermeidet.

`ledger-qvt84`s Task-Text ("Was") nennt ausdrücklich nur `importToYnab`; Force-Import blieb
deshalb bewusst außerhalb des Scopes dieses Tasks.

## What
`forceImportDuplicates` (`src/Server/Api.fs`) muss die schon vorhandene Kassenbuch-Buchung
**wiederverwenden und neu verknüpfen**, nicht `pushComdirectImports` mit einer neuen
ImportId aufrufen (siehe Notes, Verifier-Fund Iteration 2 von `ledger-qvt84`): den
Comdirect-Import-Push-Pfad ist bereits so gebaut, dass die Kassenbuch-Buchung mit der
**alten, deterministischen** ImportId (`Shared.Domain.generateImportId txId`) schon vor
YNABs Duplikat-Meldung entstand (Kassenbuch zuerst, ADR 0021). Force-Import muss also:
1. die bestehende Buchung über `findEntryByLink ImportIdSystem` (alte, deterministische
   ImportId) finden,
2. mit `YnabClient.prepareTransactions ... forceNewImportId = true` senden (neue ImportId
   nur für YNABs Dedup, nicht für die Kassenbuch-Suche),
3. bei Erfolg die **gefundene** Buchung mit `EntryLinkedToYnab` verknüpfen (kein neues
   `EntryImportedFromComdirect`, keine neue Buchungs-Id).

## Acceptance criteria
- [ ] Ein Force-Import verknüpft die schon bestehende Kassenbuch-Buchung (über die alte,
      deterministische ImportId gefunden) mit der neuen YNAB-Id, statt eine zweite
      "Comdirect-Import"-Buchung anzulegen.
- [ ] Existiert (Randfall) noch keine Kassenbuch-Buchung für die alte ImportId, entsteht
      genau eine, wie beim normalen Import-Push.
- [ ] Ein zweiter Force-Import-Versuch erzeugt keine zweite Buchung.
- [ ] Test mit injiziertem Push, in-memory SQLite.
- [ ] `dotnet build`, `dotnet test` grün.

## Notes
**Wichtig (Verifier-Fund, `ledger-qvt84` Iteration 2):** der normale Push hat, bevor YNAB
die Buchung als Duplikat meldet, bereits eine unverknüpfte Kassenbuch-Buchung mit der
**alten** ImportId geschrieben (`pushComdirectImports` schreibt immer zuerst, ADR 0021).
Ruft Force-Import `pushComdirectImports` einfach mit einer **neuen** ImportId auf (wie
`YnabClient.prepareTransactions ... forceNewImportId = true` sie erzeugt), findet
`findEntryByLink ImportIdSystem neueImportId` nichts (andere ImportId als die alte!) und
legt eine **zweite** "Comdirect-Import"-Buchung an, obwohl die erste schon existiert. Der
Folgetask darf `pushComdirectImports` deshalb nicht unverändert wiederverwenden; er muss
die bestehende Buchung über die alte ImportId finden und nur nachträglich verknüpfen
(neue Fassade oder ein Parameter, der eine externe "diese Buchung existiert schon"-Suche
statt der eingebauten ImportId-basierten Suche erlaubt). Auch die Review-Spur ist beim
Force-Import-Fall leer (die Buchung war ja schon vorher kategorisiert und abgelehnt); das
ist beim Wiederverwenden zu berücksichtigen (kein zweiter Kategorisierungs-Versuch nötig,
die Kategorie steht schon in der bestehenden Buchung).
