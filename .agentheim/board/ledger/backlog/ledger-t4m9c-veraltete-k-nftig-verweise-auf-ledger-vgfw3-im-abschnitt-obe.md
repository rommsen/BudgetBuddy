---
id: ledger-t4m9c
title: "Veraltete 'künftig'-Verweise auf ledger-vgfw3 im Abschnitt Oberfläche korrigieren"
status: backlog
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: []
tags: [doku, kassenbuch, readme]
related_adrs: [0036]
related_research: []
prior_art: [ledger-vgfw3]
---

## Why
`ledger-vgfw3` (Filter-Chips und Summenzeile über der Kassenbuch-Liste) ist erledigt.
Der Abschnitt `## Oberfläche` im ledger-README spricht an zwei Stellen noch so, als
stehe das aus:
- "Der heutige Schalter ist eine schlichte Zeile über der Liste, kein Filter-Chip (die
  Filter-Chip-Leiste selbst ist weiter `ledger-vgfw3`)." — im Absatz
  `**Gelöschte Buchungen**`.
- Der Bullet "**Filter-Chips und Summenzeile über dem Treffer.**
  `getLedgerEntriesPage` liefert Trefferzahl und Summe bereits (ledger-b1xwe), aber die
  Seite zeigt heute weder Filter-Chips noch die Summenzeile an — das ist
  `ledger-vgfw3`." unter der Überschrift "Was das Konzept voraussetzt und heute nicht
  gibt (Scheiben-Material für `ledger-s8m3w`)".

Beide Stellen behaupten einen Zustand ("gibt es noch nicht", "ist offen"), der seit
`ledger-vgfw3` nicht mehr stimmt. Wer diese Stellen liest, ohne den Task-Status von
`ledger-vgfw3` zu kennen, bekommt ein falsches Bild vom aktuellen Stand der Oberfläche.

`ledger-ezrw0`s Verifier hat dieselbe Beobachtung auch für den Satz "Fundament für die
künftigen Filter (`ledger-vgfw3`)" im `## Sichten`-Eintrag zu `getLedgerEntriesPage`
gemacht, aber ausdrücklich entschieden, dass dessen Korrektur eine Erweiterung der
Akzeptanzkriterien jener Aufgabe gewesen wäre und daher hier gesondert zu behandeln ist.
Diese Aufgabe deckt beide oben genannten Stellen in `## Oberfläche` ab; ob der
`## Sichten`-Satz mit hinein soll, entscheidet der Builder beim Verfeinern.

## What
Die beiden oben zitierten Stellen im Abschnitt `## Oberfläche` so korrigieren, dass sie
den erledigten Stand von `ledger-vgfw3` wiedergeben (Filter-Chips und Summenzeile über
der Liste existieren), statt ihn weiterhin als offen zu beschreiben. Der
"Scheiben-Material"-Bullet gehört dabei entweder ganz heraus (das Vorausgesetzte gibt es
jetzt) oder wird, falls ein Rest offen ist, präzisiert, was genau noch fehlt.

## Acceptance criteria
- [ ] Die Stelle im Absatz `**Gelöschte Buchungen**` nennt `ledger-vgfw3` nicht mehr als
      offen ("weiter `ledger-vgfw3`"), sondern spiegelt den erledigten Stand wider.
- [ ] Der Bullet "Filter-Chips und Summenzeile über dem Treffer" unter "Was das Konzept
      voraussetzt und heute nicht gibt" ist entfernt oder auf einen tatsächlich noch
      offenen Rest präzisiert — nicht länger pauschal als fehlend beschrieben.
- [ ] Beide Korrekturen decken sich mit dem Stand von `ledger-vgfw3` in `done/`.
- [ ] Keine anderen Stellen im README werden angefasst.

## Notes
Erfasst am 2026-09-19 aus der Verifikation von `ledger-ezrw0`, Iteration 1. Reine
Doku-Aufgabe: am Code ändert sich nichts.