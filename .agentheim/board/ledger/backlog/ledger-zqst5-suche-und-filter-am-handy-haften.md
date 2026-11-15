---
id: ledger-zqst5
title: "Kassenbuch am Handy: Suche, Filter-Knopf und Chips haften oben beim Scrollen"
status: backlog
type: feature
context: ledger
created: 2026-11-15
completed:
depends_on: []
blocks: []
tags: [frontend, handy, kassenbuch]
related_adrs: [0087]
related_research: []
prior_art: [ledger-d9z3n]
---

## Why
Am Handy scrollen Suche, Filter-Knopf und Filter-Chips mit der Liste weg. Styleguide §7.3 verlangt, dass Filter unter dem
Kopf haften, damit ein Filterwechsel kein Hochscrollen braucht. Entschieden (Roman, 2026-11-15, BB Prüfliste q-sticky):
haften bleiben.

## What
Unter 1024 px haftet die Zeile mit Suche und Filter-Knopf samt der Chip-Zeile unter dem Seitenkopf (Kassenbuch, Tab
Buchungen); Tageskopfzeilen verankern sich darunter.

## Acceptance criteria
- [ ] GIVEN Kassenbuch am Handy (412 px) mit langer Liste, WHEN weit nach unten gescrollt, THEN stehen Suche, Filter-Knopf
      und gesetzte Chips oben im Bild, mit Hintergrund (kein Durchscheinen der Zeilen), wie §7.3.
- [ ] Die Tageskopfzeilen haften darunter, nicht dahinter.
- [ ] Konto-Feld und Tabs scrollen weg (nur Suche, Filter, Chips haften); fehlt die Chip-Zeile, ist die haftende Zone
      entsprechend niedriger.
- [ ] Tastatur offen in der Suche: die haftende Zone verdeckt das Suchfeld nicht, die Liste darunter bleibt bedienbar.
- [ ] Layout-Sweep: ein Zustand „kassenbuch-handy-gescrollt“ bei 412 px mit Invariante „Suche im Bild nach Scrollen“, mit
      Gegenbeweis.
- [ ] Ab 1024 px unverändert.

## Notes
Prototyp Version 17 (Kassenbuch am Handy). Positionierung in `src/Client/styles.css` wie die Sticky-Filter der Budget-Seite.
