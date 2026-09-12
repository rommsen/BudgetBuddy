---
id: ledger-vgfw3
title: "Kassenbuch-Filter mit Summenzeile: Kategorie (Mehrfachauswahl), Zeitraum, Payee, Betrag, Memo"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001]
blocks: [ledger-r1d2f]
tags: [frontend, backend, kassenbuch, scheibe]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Zweite lesende Scheibe. Deckt Romans häufigsten Arbeitsablauf am Kassenbuch ab: Buchungen
finden. Verteilung aus seiner Beschreibung (2026-09-11): Kategorie etwa 70 bis 80 %,
Zeitraum etwa 15 %, Payee, Betrag und Memo je etwa 5 %.

Zwei Dinge sollen besser sein als in YNAB-Web: mehrere Kategorien auf einmal (YNAB kann
nur eine), und der Filter muss am Handy genauso gut gehen. Roman nutzt ihn dort heute nie,
weil er zu umständlich ist.

## What
Sticky Filterleiste aus Chips über der Liste, gleich auf beiden Geräten: Kategorie,
Zeitraum, Payee, Betrag, Memo. Serverseitig gefiltert, nie im Client über die geladene
Seite.

- **Kategorie** öffnet die Kategorienauswahl mit **Mehrfachauswahl**. Versteckte
  Kategorien stehen eingeklappt am Ende, damit sie nicht versehentlich getroffen werden
  (in YNAB passiert Roman genau das). Am Handy als BottomSheet, am PC als Popover.
- **Zeitraum** mit Schnellwahl (dieser Monat, letzter Monat, dieses Jahr, letzte zwölf
  Monate) und Von/Bis.
- **Payee**, **Memo**, **Betrag** als Suchfelder mit kurzer Verzögerung beim Tippen.
- **Summenzeile** über der Liste, sobald ein Filter aktiv ist: Trefferzahl und
  Gesamtbetrag über den **ganzen** Treffer. Damit ist Romans Steuererklärungs-Fall ohne
  Ankreuzen erledigt.
- Filterwechsel springt an den Listenanfang, die Chips bleiben sichtbar (Styleguide 7.3).
- Jeder Chip trägt ein "×", am Ende der Leiste "Alle Filter löschen".

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief mit Schrittfolge:
`docs/ux/kassenbuch/briefs.md`, Ablauf 2 und 3.

Der Chip "Zu prüfen" gehört nicht hierher: er braucht den Prüfvermerk aus dem Spiegel
(`ledger-kdnnz`). Der Available-Betrag je Kategorie in der Auswahl kommt aus
`ledger-kwwxz`; ohne ihn zeigt der Picker die Kategorien ohne Betrag.
