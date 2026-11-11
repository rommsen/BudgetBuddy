---
id: banking-import-asr13
title: "Kann-Sammlung banking-import"
status: backlog
type: chore
context: banking-import
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [kann-sammlung, tests]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler stehen hier als Zeile. Sie wird nie automatisch gebaut. Angelegt bei der Fahrplan-Zuordnung vom 2026-11-07 (ADR 0086).

## What
Die aufgenommenen Tickets stehen unten vollständig. Geschnitten und priorisiert wird im Refinement.

## Acceptance criteria
- [ ] Roman hat die Zeilen priorisiert und geschnitten, bevor etwas davon gebaut wird.

## Aufgenommen aus banking-import-w7k3m (Fahrplan-Zuordnung 2026-11-07)
**Integrationstest mit echten Schlüsseln und falschem Passwort entfernen** (chore, erfasst 2026-10-30)

### Why
"handles invalid credentials gracefully" in `src/Tests/ComdirectIntegrationTests.fs` meldet sich mit echten API-Schlüsseln und falschem Passwort an. Punkt 12 der Test-Doktrin verbietet das. banking-import-cx4b7 hat den Test nur auf `initOAuth` begrenzt und neben der Sonde abgeschaltet.

### What
Den Testfall entfernen. Falls das Verhalten bei abgelehnter Anmeldung abgesichert bleiben soll, einen Test gegen einen Fake-Handler mit `initOAuthWith` schreiben.

### Acceptance criteria
- [ ] GIVEN `src/Tests` THEN kein Testfall meldet sich mit echten Schlüsseln und falschem Passwort an.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Aus dem Review-Lauf nach design-system-dak16 (2026-11-08)
- Import prüfen: `src/Client/Components/SyncFlow/State.fs:569` schluckt einen Ladefehler der Lena-Flag-Farbe (`fun _ -> LenaFlagColorLoaded None`), und `SyncFlow.Types.LenaFlagColor` ist `string option` statt `RemoteData`. Das Kassenbuch macht es richtig (`RemoteData` plus `lenaColorLoadFailedReason`); hier nachziehen.
- Review-Lauf 2026-11-11 (design-system-k6kfw), Roman: Im Sync-Ablauf stehen in der Prüfung zwei Rückwege übereinander (oben „Zurück“ zur Übersicht, darunter der alte Pfeil, der die Prüfung abbricht). Den Abbrechen-Pfeil entfernen, Abbrechen als eigener neutraler Knopf in der Prüfung. Entfällt, wenn `ledger-v6s7c` (Durchgehen) den Sync-Ablauf ablöst.
