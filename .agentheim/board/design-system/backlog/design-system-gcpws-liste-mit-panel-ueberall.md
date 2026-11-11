---
id: design-system-gcpws
title: "Liste mit Panel überall: Panel rund 30 % ab 1024 px, auch für Regeln, Erfassen, Targets prüfen und Kategorien, ein generischer Tastentest"
status: backlog
type: feature
context: design-system
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, panel, tastatur]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Das Panel ist heute fest 340, 360 oder 420 px breit, nicht jede Liste hat eins, und die Tastenregeln sind je Seite getestet.

## What
Panel rund 30 % der Breite (min 380, max 520 px) ab 1024 px auf jeder Liste mit Detail. Tastenregeln aus §7.8 und §7.9 als ein generischer Test über alle Listen mit Panel, wie der Layout-Sweep. Auswahl-Dialoge mit Fokus in der Suche.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Styleguide §7.6, §7.8, §7.9.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
