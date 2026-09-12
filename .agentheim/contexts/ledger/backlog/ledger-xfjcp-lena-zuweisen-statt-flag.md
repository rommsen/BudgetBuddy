---
id: ledger-xfjcp
title: "Buchung an Lena zuweisen statt nur ein Flag setzen; später Sicht 'Für mich' und Benachrichtigung"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [captured, ux, kassenbuch, flags]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman beim Beschreiben seines YNAB-Ablaufs (2026-09-12, UX-Session `ledger-xbrht`): er
setzt heute in YNAB ein Flag, das er "Lena" genannt hat, für Buchungen, zu denen er nichts
findet. Eigentlich will er nicht "ein Flag setzen", sondern "Lena zuweisen". Das Flag ist
nur die technische Umsetzung. Lena nutzt YNAB über den Familien-Account mit eigenem Login
und sieht die Flags dort.

## What
1. Im Kassenbuch eine Aktion "Lena fragen" / "Lena zuweisen" (Detail, Auswahl-Leiste),
   Kennzeichen "Rückfrage Lena" in der Zeile, Filter danach. Im Hintergrund wird das
   YNAB-Flag "Lena" gesetzt (Rückschreiben nach YNAB, Vision-Stufe v2), solange Lena YNAB
   nutzt.
2. Spätere Iteration (Romans Ideen, nicht entschieden): Lena bekommt eine
   Push-Benachrichtigung; oder eine Sicht "Für mich" in BB, in der Lena alles sieht, was
   ihr zugewiesen ist (wie gepinnte Kategorien auf der YNAB-Startseite). Offen, ob dafür
   eine Anmeldung nötig ist: BB läuft hinter Tailscale und ist nur für Roman und Lena.
   Roman: "man müsste da mal ein bisschen überlegen, was da gutes UX wäre."

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-09-12 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Der Prototyp aus `ledger-xbrht` zeigt Punkt 1 als "Lena fragen" (Brief
5 in `docs/ux/kassenbuch/briefs.md`); ob und wann Punkt 1 gebaut wird, entscheidet
`ledger-s8m3w` zusammen mit den übrigen schreibenden Scheiben.
