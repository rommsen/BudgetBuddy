---
id: ledger-h4p6c
title: Spiegel-Auslöser: Frische-Kriterium beim Seitenaufruf und Lauf nach jedem Push
status: todo
type: feature
context: ledger
created: 2026-09-06
depends_on: [ledger-m8r2v, ledger-q5w9c]
blocks: []
tags: [backend, frontend, integration]
related_adrs: [0014, 0021]
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
ADR 0014 nennt drei Auslöser für den Spiegel-Lauf. `ledger-m8r2v` hat nur den dritten
gebaut (manuell, "Jetzt spiegeln"); `ledger-kt0wq` hält ausdrücklich fest, dass das
Frische-Kriterium Folge-Task ist. Ohne die automatischen Auslöser muss Roman den Spiegel
von Hand anstoßen, damit das Kassenbuch stimmt — genau die Handarbeit, die BB abnehmen soll.

## What
1. **Frische-Kriterium:** beim Öffnen der Kassenbuch-Seite und beim Wechsel der PWA in den
   Vordergrund prüfen, ob der letzte Lauf älter als 5 Minuten ist (Zeitpunkt steht im
   Lauf-Status, `LedgerApi.getLastMirrorRun`). Wenn ja, den Lauf **im Hintergrund**
   anstoßen; die Seite zeigt sofort den vorhandenen Stand und aktualisiert danach.
2. **Nach jedem Push:** nach einem erfolgreichen Import nach YNAB einen Lauf anstoßen,
   damit die frisch gepushten Buchungen mit ihrer YNAB-Id im Kassenbuch landen.
3. **Kein Server-Timer** (ADR 0014 verwirft ihn ausdrücklich) und kein paralleler
   Doppel-Lauf: ein bereits laufender Spiegel darf nicht ein zweites Mal gestartet werden.
4. Frontend-Zustand über RemoteData; ein Hintergrund-Lauf blockiert die Seite nicht.

## Acceptance criteria
- [ ] Seitenaufruf mit letztem Lauf älter als 5 Minuten löst genau einen Hintergrund-Lauf
      aus; jüngerer Lauf löst keinen aus (Test).
- [ ] PWA-Vordergrund verhält sich wie der Seitenaufruf.
- [ ] Nach erfolgreichem Push läuft der Spiegel genau einmal (Test).
- [ ] Zwei gleichzeitige Auslöser ergeben nur einen Lauf (Test).
- [ ] Die Seite zeigt während des Hintergrund-Laufs den vorhandenen Stand, nicht leer.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Der Lauf selbst existiert bereits (`Ledger.CommandHandlers.runMirror` / `mirrorYnabBudget`);
dieser Task baut nur die Auslöser und die Doppel-Lauf-Sperre.

Nachtrag 2026-09-11 (aus ADR 0021 und `ledger-tdnpk`): Nach einem Quick Add darf der
Spiegel-Lauf erst starten, wenn `EntryLinkedToYnab` geschrieben ist. Quick-Add-Buchungen
haben keine ImportId (ADR 0004); ein Lauf dazwischen sieht die YNAB-Buchung ungematcht
und legt eine zweite Buchung an. Gilt sinngemäß für den Import-Push (`ledger-qvt84`), dort
fängt die ImportId den Fall aber ab.
