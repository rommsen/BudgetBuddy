---
id: design-system-zahv9
title: Ruhige Bausteine für Auswahlen: Picker-Zeile, Money.available, Suchfeld, Footer, searchableSelect und Form auf Deutsch
status: backlog
type: feature
context: design-system
created: 2026-10-19
completed:
depends_on: []
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
Gemeinsame Ursachen der Kategorie-Auswahlen (Kassenbuch-Filter, Import prüfen, Aufteilen, Quick Add, Styleguide) beheben:
- `Money.available` (`Money.fs:220`) bzw. eine Listenvariante: Mono 500, `text-primary`, `text-muted` bei 0, nur negativ Rot, kein Grün (K2, P1).
- Picker-Zeile 14px PC / 15px Handy mit `truncate`, Betrag in fester Breite; `Input.checkbox` (`Input.fs:230`) mit Größenvariante (K1, P2).
- Kompaktes Suchfeld am PC (36px/14px), am Handy 16px wegen iOS-Zoom (K4, P3; `Input.fs:31`, `styles.css:1760`).
- Picker-Footer ohne Leuchten, Beschriftung ohne Umbruch, keine "(0)" ohne Auswahl (K3).
- Gruppenkopf im Mono-10px-Stil (`ListRoles.groupHeader`), kein doppelter Kopf im Bottom-Sheet (P2).
- Eine gemeinsame Optik für Popover-Auswahl (`CategoryMultiSelect`) und Bottom-Sheet-Auswahl (`BottomSheet.categoryPicker`) (P4).
- `searchableSelect` (`Input.fs:705`) 14px mit Gruppenköpfen, deutsche Texte, Scrollen im Modal; `Form.fs` deutsche Pflichtfeld-Texte (R5).
- Styleguide 8a um "Auswahl" ergänzen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured on 2026-10-19 aus der Bestandsaufnahme. Raw, unrefined; braucht einen
`modeling`-Durchgang. Befund-Nummern (K, P, R, S, E) beziehen sich auf den Bericht.
