---
id: design-system-k8s2m
title: "Kann-Sammlung design-system"
status: backlog
type: chore
context: design-system
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [review, design, a11y, styleguide]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
Geschnitten und priorisiert wird im Refinement.

## Aus dem Review-Lauf nach design-system-bcdqy und infrastructure-nx14s (2026-11-07)
- Fokusring des Schalters „Geprüft“ (`src/Client/DesignSystem/Detail.fs`, `reviewedSwitch`): am Handy liegt er auf dem unsichtbaren 48-px-Quadrat statt auf der Schiene. Abhilfe: Klasse an der Schiene und `button:focus-visible > .switch-track`.
- Styleguide §4 und §7.14 „höchstens drei Schriftgrößen je Ansicht“: die Budget-Seite hat mit `FontSizes.compact`, `microPlus`, Titel und Text wohl mehr. Prüfen und, falls ja, als Zeile in Etappe 3 des Fahrplans.
- Untere Navigation bei 320 px: zwischen den sechs Einträgen hängt der Abstand an `justify-around`, die 8 px aus §7.15 misst der Sweep nicht. Erledigt sich mit „Navigation nur mit den drei Orten“ (Etappe 3).
- `grid-cols-[1fr_2fr]` im Tab Target (`TargetFormView.fs`) ist ein freier Wert; bei 320 px bleiben dem Betragsfeld rund 90 px. Am Bildschirm prüfen.
