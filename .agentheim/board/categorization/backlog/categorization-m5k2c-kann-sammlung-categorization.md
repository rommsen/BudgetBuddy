---
id: categorization-m5k2c
title: "Kann-Sammlung categorization"
status: backlog
type: chore
context: categorization
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [review, ui, regeln]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
Geschnitten und priorisiert wird im Refinement.

## Aus dem Review-Lauf nach infrastructure-jc1y1 (2026-11-08)
- Regel-Dialog: Öffnet man das Kategorie-Popover, während der Dialog noch per `animate-scale-in` einfährt, rechnet `scrollIntoView` mit der skalierten Lage und das Popover steht halb unter dem Rand. Der Sweep-Zustand `regeln-dialog-picker` wartet deshalb per `settle` (`scripts/layout-states.mjs`). Abhilfe im Client: Popover erst nach dem Ende der Einfahrt positionieren oder neu messen.
