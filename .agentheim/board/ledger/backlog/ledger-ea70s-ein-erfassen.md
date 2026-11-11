---
id: ledger-ea70s
title: "Ein Erfassen: Quick Add hört auf, ein eigener Ort zu sein; „+ Buchung“ in Kassenbuch und Übersicht mit kluger Vorbelegung"
status: backlog
type: feature
context: ledger
created: 2026-11-11
completed:
depends_on: [design-system-001-styleguide, design-system-k6kfw]
blocks: []
tags: [etappe-3, frontend, erfassen]
related_adrs: [0086,0087]
related_research: []
prior_art: []
---

## Why
Quick Add und Erfassen im Kassenbuch sind zwei Wege für dasselbe. Das Versprechen von Quick Add (wenige Klicks, kluge Vorbelegung) soll für jedes Erfassen gelten.

## What
Die Seite Quick Add entfällt. „+ Buchung“ im Kassenbuch und in der Übersicht trägt Vorlagen und Vorbelegung. Der Kategorie-Picker stapelt sich nicht mehr über einem Sheet (`CategoryPicker.sheetLayered`), die Auswahl ersetzt den Inhalt des Sheets und führt zurück.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Schnitt aus Etappe 3 (Fahrplan, Roman 2026-11-11). Nimmt auf: design-system-taytp (Kategorie-Picker vereinheitlichen). Styleguide §7.15.
Vorlage: Prototyp https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17). Baut der Schnitt eine Seite um, fliegen ihre `[flow]`-, `[tastatur]`-, `[fokus]`- und `[scroll]`-Tests raus und werden gegen die Muster aus dem Styleguide neu geschrieben, je Flow einer.
