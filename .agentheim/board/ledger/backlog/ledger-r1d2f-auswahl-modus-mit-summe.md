---
id: ledger-r1d2f
title: "Auswahl-Modus mit Summe der Auswahl (Handy und PC)"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-vgfw3, design-system-001]
blocks: []
tags: [frontend, kassenbuch, scheibe]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Dritte lesende Scheibe. Roman markiert heute in YNAB-Web einzelne Zeilen, um ihre Summe zu
sehen (Beispiel Steuererklärung: Kategorie wählen, die letzten zwölf Buchungen markieren,
Gesamtbetrag ablesen). Am Handy geht das gar nicht, er hätte es aber gern.

Der häufige Fall ist mit der Summenzeile aus `ledger-vgfw3` bereits abgedeckt. Diese
Scheibe ergänzt die Teilmenge.

## What
- Auswahl starten: langes Drücken auf eine Zeile (Handy), Kästchen oder Shift-Klick (PC).
- Auswahl-Leiste unten mit Anzahl, **Summe der Auswahl**, Aktion "Auswahl aufheben" und
  "Alle". Am Handy über der Bottom-Navigation, am PC über dem Fensterrand.
- Solange eine Auswahl aktiv ist, zeigt die Summenzeile die Auswahl statt des Treffers.
- Auswahl übersteht Konto- und Filterwechsel nicht zwingend; Verhalten bei der
  Verfeinerung festlegen.
- Schreibende Aktionen in der Leiste (geprüft, Kategorie, Lena fragen) gehören **nicht**
  hierher, sie hängen an der v2-Entscheidung.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 3.
