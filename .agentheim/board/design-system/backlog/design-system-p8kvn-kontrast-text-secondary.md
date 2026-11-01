---
id: design-system-p8kvn
title: "Badge.neutral und setting-description liegen bei 12 px knapp unter 4,5:1 Kontrast"
status: backlog
type: bug
context: design-system
created: 2026-11-01
completed:
depends_on: []
blocks: []
tags: [kontrast, styleguide]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Aus dem Design-Review zu `banking-import-bftq0`: `Badge.neutral` erreicht rund 4,2:1, `.setting-description` 4,46:1, beide bei 12 px. Nötig sind 4,5:1. Das betrifft das ganze Design-System, nicht nur die Bankverbindung.

## What
Den Tonwert von `text-secondary` oder die betroffenen Flächen so anpassen, dass beide die Grenze erreichen. Werte im Styleguide festlegen, nicht je Komponente.

## Acceptance criteria
- [ ] `Badge.neutral` und `.setting-description` erreichen rechnerisch mindestens 4,5:1.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Die Texte wirken auf `/styleguide` im Tonwert stimmig. [human-eye]
