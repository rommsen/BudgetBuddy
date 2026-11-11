---
id: design-system-k6kfw
title: "Navigation mit drei Orten: Übersicht, Kassenbuch, Budget, Einstellungen hinter dem Zahnrad, ab 768 px links"
status: backlog
type: feature
context: design-system
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, navigation]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Die Navigation zeigt heute sechs Punkte, darunter Aktionen und Inseln (Sync, Quick Add, Regeln). ADR 0086 und ADR 0087 legen drei Orte fest.

## What
Leiste unten am Handy und Seitenleiste oder Rail links ab 768 px zeigen nur Übersicht, Kassenbuch und Budget. Einstellungen hinter einem Zahnrad. Unterseiten (etwa Targets prüfen) haben im Kopf „Zurück“. Die Übersicht darf einen Zähler tragen. Sync und Quick Add sind kein Eintrag mehr, bleiben aber als Ablauf erreichbar.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Fahrplan-Zeilen: „Navigation nur mit den drei Orten“, „Navigation ab 768 px links“ (Styleguide §7.4, §7.15). Geht mit einer schlichten Übersicht live (Schnitt 2), Roman 2026-11-11.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
