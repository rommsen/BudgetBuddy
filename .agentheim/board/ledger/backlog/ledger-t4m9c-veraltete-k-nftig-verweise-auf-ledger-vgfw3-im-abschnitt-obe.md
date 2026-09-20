---
id: ledger-t4m9c
title: "Veraltete 'künftig'-Verweise auf ledger-vgfw3 im ledger-README korrigieren (Oberfläche und Sichten)"
status: backlog
type: chore
context: ledger
created: 2026-09-19
completed:
depends_on: []
blocks: [ledger-f8q2x]
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

Eine dritte Stelle liegt im Abschnitt `## Sichten`, im Eintrag zu `getLedgerEntriesPage`:
- "Fundament für die künftigen Filter (`ledger-vgfw3`) und die Summenzeile über der Liste."

`ledger-ezrw0`s Verifier hatte empfohlen, diese dritte Stelle gleich beim Neu-Ausgeben des
Blocks mitzukorrigieren, weil sie ohnehin gerade von Hand neu geschrieben wurde. Der Worker
hat das korrekt nicht getan, weil es eine Erweiterung der Akzeptanzkriterien jener Aufgabe
gewesen wäre. **Roman hat am 2026-09-20 entschieden, dass sie hierher gehört**, also deckt
diese Aufgabe alle drei Stellen ab.

Zu beachten beim Umsetzen: die `## Sichten`-Stelle sitzt in einem mehrzeilig umbrochenen
Unterbullet auf Einrückung 2. Die `README_DELTA`-Grammatik erreicht nur Bullets auf Spalte 0
mit fettem Anfang, weshalb `ledger-ezrw0` auf das Eltern-Bullet `**Sichten**` ausweichen und
den gesamten 32-Zeilen-Block ersetzen musste — mit erhaltener Einrückung in `expected` UND
`body`, sonst wird der Block flachgeklopft. Siehe `ledger-ezrw0`s Outcome und den dortigen
Verifier-Befund aus Iteration 1; derselbe Weg ist hier nötig.

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
- [ ] Der Satz "Fundament für die künftigen Filter (`ledger-vgfw3`)" im `## Sichten`-Eintrag
      zu `getLedgerEntriesPage` nennt `ledger-vgfw3` nicht mehr als künftig.
- [ ] Alle drei Korrekturen decken sich mit dem Stand von `ledger-vgfw3` in `done/`.
- [ ] Die Einrückung des `## Sichten`-Blocks ist unverändert: die sechs Unterbullets stehen
      weiterhin auf Einrückung 2, ihre Fortsetzungszeilen auf 4.
- [ ] Keine anderen Stellen im README werden angefasst.

## Notes
Erfasst am 2026-09-19 aus der Verifikation von `ledger-ezrw0`, Iteration 1. Reine
Doku-Aufgabe: am Code ändert sich nichts.