---
id: banking-import-w7k3m
title: "Integrationstest mit echten Schlüsseln und falschem Passwort entfernen"
status: backlog
type: chore
context: banking-import
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [backend, comdirect, tests]
related_adrs: [0071]
related_research: []
prior_art: [banking-import-cx4b7]
---

## Why
"handles invalid credentials gracefully" in `src/Tests/ComdirectIntegrationTests.fs` meldet sich mit echten API-Schlüsseln und falschem Passwort an. Punkt 12 der Test-Doktrin verbietet das. banking-import-cx4b7 hat den Test nur auf `initOAuth` begrenzt und neben der Sonde abgeschaltet.

## What
Den Testfall entfernen. Falls das Verhalten bei abgelehnter Anmeldung abgesichert bleiben soll, einen Test gegen einen Fake-Handler mit `initOAuthWith` schreiben.

## Acceptance criteria
- [ ] GIVEN `src/Tests` THEN kein Testfall meldet sich mit echten Schlüsseln und falschem Passwort an.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.