---
id: ledger-rynk5
title: "Gescheiterter Push nach YNAB als Ereignis mit Grund und Zeit, YNAB-Verknüpfung und Push-Fehlschlag im Lesemodell"
status: doing
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: []
blocks: [ledger-q1hw2]
tags: [backend, kassenbuch, push, ereignis]
related_adrs: [0013, 0021, 0050]
related_research: []
prior_art: [ledger-r777c, ledger-fgw2q]
---

## Why
Das Buchungs-Detail (`ledger-q1hw2`) soll bei einer Buchung ohne YNAB-Gegenstück sagen,
warum sie fehlt und seit wann (Handoff Ablauf 7, Screen 7.2: "Push gescheitert am 10.09.
18:02, Netzwerkfehler"). Das Kassenbuch kennt heute nur die gelungene Verknüpfung
(`EntryLinkedToYnab`, ADR 0021 Punkt 4). Scheitert der Push, bleibt die Buchung
unverknüpft stehen (ADR 0021 Punkt 2), aber Grund und Zeitpunkt stehen nirgends im
Ereignis-Log. Roman hat am 2026-10-19 entschieden, das Ereignis jetzt zu bauen statt die
Leiste ohne Grund zu zeigen.

## What
1. **Neues Ereignis** `EntryPushToYnabFailed { Reason: string }` in
   `src/Server/Ledger/Events.fs`, gebaut wie `EntryReviewMarkWriteBackFailed` (ADR 0050):
   Namens-Abbildung, Schema-Version 1, Codec, Decoder. Der Zeitpunkt ist der
   Ereignis-Zeitstempel, der Actor der des Push-Wegs (`ComdirectImport` bzw. `QuickAdd`).
2. **Wo es entsteht** (`src/Server/Ledger/CommandHandlers.fs`): in `pushComdirectImports`
   für jede geschriebene oder wiedergefundene, aber nicht verknüpfte Buchung eines
   gescheiterten Sende-Versuchs (Netz, Timeout, YNAB-Fehler, Duplikat, nicht auswertbare
   Antwort), und in `addCashExpense`, wenn der Push scheitert. Der Grund ist ein kurzer,
   lesbarer Satz aus dem Fehler (Fehlerart, bei HTTP-Fehlern der Status). Kein Rollback,
   die Reihenfolge aus ADR 0021 bleibt.
3. **Zustand der Buchung** (`Domain.fs` `apply`): `Entry.PushToYnabFailure` (Grund und
   Zeitpunkt), aufgelöst durch `EntryLinkedToYnab` (gleich ob aus der Push-Antwort oder
   vom Spiegel über die ImportId). Ein zweiter Fehlschlag ersetzt den ersten. Spalte samt
   Migration und Upsert in `Persistence.fs`, Replay bleibt vollständig (ADR 0013 Punkt 6).
4. **Lesemodell** (`src/Shared/Domain.fs`, `LedgerEntryView`): zwei neue Felder am Ende
   des Records:
   - `LinkedToYnab: bool`: die Buchung trägt eine YNAB-Verknüpfung (vom Spiegel gesehene
     Buchungen immer, BB-geführte nach `EntryLinkedToYnab`).
   - `PushToYnabFailure: LedgerPushFailure option` mit `Reason: string` und
     `FailedAt: DateTime`.
   Bestehende Record-Literale in Tests und Mock-Fixtures werden ergänzt.
5. **Historie** (`QueryHandlers.fs`): ein sprechender Satz "Push nach YNAB gescheitert:
   <Grund>".

Nicht Teil: ein "Erneut senden" für den Push (schreibend, eigener Weg), Anzeige im Client
(`ledger-q1hw2`).

## Acceptance criteria
- [ ] Codec-Rundreise und Decoder für `EntryPushToYnabFailed` (Test analog zu den
      Prüfvermerk-Ereignissen in `LedgerReviewMarkTests.fs`).
- [ ] `pushComdirectImports` mit einem Push-Stub, der mit Netzfehler scheitert: jede
      geschriebene Buchung des Versuchs trägt genau ein `EntryPushToYnabFailed` mit dem
      Grund, keine `EntryLinkedToYnab` (DB-Test, In-Memory-SQLite).
- [ ] Dasselbe für HTTP 429 und für eine nicht auswertbare Antwort (je ein Test); der Grund
      nennt die Fehlerart.
- [ ] Eine bereits verknüpfte Buchung (`AlreadyImported`) bekommt kein
      `EntryPushToYnabFailed`.
- [ ] `addCashExpense` mit scheiterndem Push schreibt `CashExpenseAdded` und danach
      `EntryPushToYnabFailed`; mit gelingendem Push keins.
- [ ] `apply`: `EntryLinkedToYnab` nach einem Fehlschlag leert `PushToYnabFailure`, auch
      mit Actor `YnabMirror`; ein zweiter Fehlschlag ersetzt Grund und Zeitpunkt des ersten.
- [ ] Replay aus dem Log ergibt denselben Zustand wie der laufende Zustand (Fehlschlag
      offen und Fehlschlag aufgelöst, je ein Test).
- [ ] `LedgerEntryView`: eine vom Spiegel gesehene Buchung hat `LinkedToYnab = true`, eine
      Comdirect-Buchung vor der Verknüpfung `false` und nach `EntryLinkedToYnab` `true`;
      `PushToYnabFailure` trägt Grund und Zeitpunkt des Ereignisses.
- [ ] `getEntryHistory` liefert für das Ereignis den Satz "Push nach YNAB gescheitert:
      <Grund>".
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Abgespalten am 2026-10-19 beim Verfeinern von `ledger-q1hw2` (Entscheidung Roman: das
Ereignis gleich mitbauen). Das Muster (Fehlschlag als Ereignis, aufgelöst durch das
Erfolgsereignis, kein Rollback) ist dasselbe wie beim Prüfvermerk (ADR 0050 Punkt 11).
Erweitert ADR 0021 Punkt 2 um das Festhalten des Grundes; ob das einen Nachtrag in ADR
0021 braucht, entscheidet der Worker (ein Satz genügt).

Zu prüfen für den Worker: ob die Comdirect-Session in `ynab-sync` einen eigenen
Fehlerzustand führt, der dasselbe schon zeigt. Die Session bleibt unverändert; das
Ereignis gehört ins Kassenbuch, weil das Detail die Buchung zeigt, nicht die Session.
