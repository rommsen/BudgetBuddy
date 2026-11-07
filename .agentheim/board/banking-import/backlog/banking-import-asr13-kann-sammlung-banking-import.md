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
