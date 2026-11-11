---
id: categorization-n3sxw
title: "Regeln ins Kassenbuch: Regeln als Tab neben den Buchungen statt eigener Seite"
status: backlog
type: feature
context: categorization
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: []
tags: [etappe-3, frontend, regeln]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Regeln sind heute ein eigener Ort. ADR 0087 legt sie als Tab ins Kassenbuch.

## What
Regeln erscheinen als Tab im Kassenbuch neben den Buchungen, mit Liste und Panel nach Styleguide §7.6.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Nimmt auf: categorization-krscs (Regel-Dialog: Ladezustände, Fehler, Seiteneffekte), ledger-hg5m5 (Regel-Angebot nach Kategorie-Änderung). Beim Verfeinern klären, was davon aufgeht.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
