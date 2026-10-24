---
id: ledger-ayjzq
title: "Sicht 'Für mich' für Lena in BB: alles, was ihr zugewiesen ist"
status: backlog
type: feature
context: ledger
created: 2026-10-24
completed:
depends_on: [ledger-xfjcp]
blocks: []
tags: [frontend, kassenbuch, flags, lena, ux]
related_adrs: [0056]
related_research: []
prior_art: [ledger-xfjcp]
---
Ziel-Lage: backlog. Offen und zu klären beim Verfeinern: braucht BB dafür eine Anmeldung
oder reicht ein Einstieg "Ich bin Lena" ohne Konto (BB läuft hinter Tailscale und ist nur
für Roman und Lena)? Wie sieht die Sicht aus (gepinnt wie die Startseite in YNAB, eigene
Route, Voreinstellung des Filters)? Kann Lena die Rückfrage in BB beantworten (Kategorie
setzen, Rückfrage zurücknehmen), oder bleibt es bei Ansehen? Roman: "man müsste da mal ein
bisschen überlegen, was da gutes UX wäre."

## Why
Aus `ledger-xfjcp`, Punkt 2: Lena soll alles sehen, was ihr zugewiesen ist.

## What
Zu verfeinern. Grundlage ist der Filter "Rückfrage Lena" aus `ledger-xfjcp`.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Brainstorm mit Roman nötig vor der Verfeinerung.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).
