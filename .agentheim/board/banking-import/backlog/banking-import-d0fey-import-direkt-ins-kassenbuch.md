---
id: banking-import-d0fey
title: "Import direkt ins Kassenbuch: abgerufene Umsätze sofort in Kassenbuch und YNAB als „zu prüfen“, nur unsichere Duplikate warten"
status: backlog
type: feature
context: banking-import
created: 2026-11-11
completed:
depends_on: []
blocks: []
tags: [etappe-3, backend, frontend, sync]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Import und Durchsicht sind heute eine eigene Insel (Sync). Neue Umsätze landen erst nach dem Durchgehen im Kassenbuch.

## What
Abgerufene Umsätze gehen sofort ins Kassenbuch und nach YNAB, markiert als „zu prüfen“. Nur unsichere Duplikate warten und erscheinen beim Durchgehen als eigene Karte. Abrufen läuft automatisch und geht jederzeit von Hand.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Nimmt auf: banking-import-t4raw (zeitgesteuertes Vorbereiten), categorization-hz6vj (Kategorisieren im Import). Beim Verfeinern klären, ob diese beiden aufgehen oder eigene Kinder bleiben.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
