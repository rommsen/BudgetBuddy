---
id: ledger-v6s7c
title: "Durchgehen neuer Buchungen Karte für Karte von der Übersicht aus, mit Amazon-Link und Duplikat-Karte"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, ledger-s1weh, banking-import-d0fey]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Nach dem Import direkt sind neue Buchungen „zu prüfen“. Roman bestätigt sie heute im Sync-Ablauf.

## What
Von der Übersicht aus geht man neue Buchungen Karte für Karte durch und bestätigt sie. Amazon nur als Link mit Bestellnummer (keine API). Unsichere Duplikate als eigene Karte. Ersetzt den Sync-Ablauf als Übergangsziel von „Zu tun“.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Prototyp Version 15 bis 17: Amazon nur als Link mit Bestellnummer.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
