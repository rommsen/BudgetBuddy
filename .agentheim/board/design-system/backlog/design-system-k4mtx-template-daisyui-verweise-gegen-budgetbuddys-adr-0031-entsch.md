---
id: design-system-k4mtx
title: "template/**: DaisyUI-Verweise gegen BudgetBuddys ADR-0031-Entscheidung prüfen"
status: backlog
type: decision
context: design-system
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [doku, template, daisyui]
related_adrs: [0031]
related_research: []
prior_art: [design-system-fq8dn, design-system-k2vgz]
---

## Why
ADR 0031 (`design-system-fq8dn`) hat DaisyUI aus BudgetBuddy entfernt: es war nie installiert,
sieben Stellen setzten trotzdem DaisyUI-Klassen, alle sieben sind durch echtes Tailwind
ersetzt. `design-system-k2vgz` hat danach die letzten beiden lebenden Dokument-Verweise auf
DaisyUI aus BudgetBuddys eigener Doku entfernt (`docs/DESIGN-SYSTEM.md`, `docs/README.md`
gelöscht).

`template/**` ist von beiden Aufgaben bewusst ausgenommen worden, führt DaisyUI aber weiter:

- `template/CLAUDE.md:254`: Tabelle „Styling | TailwindCSS, DaisyUI"
- `template/README.md:7`: „Frontend: Elmish.React + Feliz + TailwindCSS/DaisyUI"
- `template/docs/00-ARCHITECTURE.md:8`: Tabelle „Styling | TailwindCSS, DaisyUI"

`template/` ist ein Gerüst für neue Projekte und beschreibt seinen eigenen Stack, nicht
zwingend BudgetBuddys. Ob es die BudgetBuddy-Entscheidung (kein DaisyUI, echtes Tailwind statt
Komponenten-Bibliothek) mittragen soll, oder ob neue Projekte aus dem Template bewusst mit
DaisyUI starten dürfen, ist offen.

## What
1. Entscheiden: soll `template/**` DaisyUI weiter als Option führen, oder der BudgetBuddy-
   Entscheidung folgen und die drei Stellen auf reines Tailwind umstellen?
2. Falls „folgen": die drei Stellen korrigieren, konsistent mit ADR 0031s Diktion
   (TailwindCSS, kein DaisyUI).
3. Falls „bewusst anders": kurz begründen (z. B. als Kommentar oder README-Zeile im
   Template selbst), damit die nächste Person, die `template/**` liest, nicht denselben Fund
   noch einmal macht.

## Acceptance criteria
- [ ] Entscheidung getroffen und dokumentiert (Verbleib oder Umstellung).
- [ ] Falls Umstellung: alle drei genannten Stellen konsistent geändert, kein DaisyUI-Rest in
      `template/**` ohne Erklärung.

## Notes
Backlog-Item aus `design-system-k2vgz` heraus erfasst; die Aufgabe selbst grenzte `template/**`
explizit aus („eigene Frage, eigener Task").