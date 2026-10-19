---
id: categorization-wnxd6
title: Regeln-Seite als ruhige Liste (schlanker Kopf, volle Breite, Tabelle statt Karten)
status: backlog
type: feature
context: categorization
created: 2026-10-19
completed:
depends_on: [design-system-zahv9]
blocks: []
tags: [captured, frontend, ruhige-listen]
related_adrs: [0049]
related_research: []
prior_art: [design-system-q7rkm, ledger-n4vte]
---

## Why
Bestandsaufnahme gegen ADR 0049 (Ruhige Listen) am 2026-10-19. Bericht und Screenshots:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/ce62b2b7-5777-4228-8b31-afa8d3e4749a/scratchpad/audit/report.md` und `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/ce62b2b7-5777-4228-8b31-afa8d3e4749a/scratchpad/audit/shots/` (Session-Ablage; die für die Umsetzung nötigen Bilder bei
der Verfeinerung ins Repo unter `docs/ux/` übernehmen, Regel "Zielbild liegt im Repo").

## What
- `PageHeader.calm` statt `gradientWithActions`, volle Breite über `pageShell` in `src/Client/View.fs` (R1, R2).
- `Table.calmHeaderCell`/`calmRow` am PC, zweizeilige Liste am Handy statt einer Karte pro Regel (R2, `Rules/View.fs:76-190`).
- "Aktiv" ist der stille Normalzustand; "inaktiv" steht als Wort, keine gefüllte Teal-Checkbox in jeder Zeile (R3).
- Löschen nicht als dauerhaft sichtbarer roter Papierkorb (R4).
- Kategorie am Handy lesbar (R4).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured on 2026-10-19 aus der Bestandsaufnahme. Raw, unrefined; braucht einen
`modeling`-Durchgang. Befund-Nummern (K, P, R, S, E) beziehen sich auf den Bericht.
