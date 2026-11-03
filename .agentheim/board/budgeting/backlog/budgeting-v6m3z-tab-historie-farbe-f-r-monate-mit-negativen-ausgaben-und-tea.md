---
id: budgeting-v6m3z
title: "Tab Historie: Farbe für Monate mit negativen Ausgaben und Teal-Betrag neben dem Stand entscheiden"
status: backlog
type: decision
context: budgeting
created: 2026-11-03
completed:
depends_on: [budgeting-x4k7p]
blocks: []
tags: [budget, historie, design, entscheidung]
related_adrs: [0049, 0075]
related_research: []
prior_art: [budgeting-dyn8g]
---

## Why
design-quality hat am 2026-11-03 zwei Punkte im Tab Historie gefunden, die eine Auslegung von Roman brauchen.

## What
1. **Monat mit negativen Ausgaben** (Erstattungen überwiegen): heute rot, weil die Task sagt "Rot nur für Beträge im Minus". Fachlich ist das ein guter Monat, Ruhige Listen sagen "keine roten Ausgaben". Vorschlag des Reviews: neutral mit Vorzeichen "+85". Oder bewusst rot lassen und im README budgeting festhalten.
2. **Zwei Farbakzente in einer Bewegungszeile**: positiver Betrag Teal (`Money.listAmount`) plus rotes Fehler-Badge. Ruhige Listen erlauben einen Akzent, der Styleguide sagt aber auch "Herkunfts- oder Spiegel-Badges gehören in Detail und Historie". Gilt die Bewegungsliste als ruhige Liste? Wenn ja: Beträge neutral über `detailAmount` mit Vorzeichen.

## Acceptance criteria
- [ ] Roman hat beide Punkte entschieden; die Entscheidung steht im README budgeting oder als ADR.
- [ ] Falls sich etwas ändert: umgesetzt, `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Bewusst nicht in budgeting-x4k7p umgesetzt.
