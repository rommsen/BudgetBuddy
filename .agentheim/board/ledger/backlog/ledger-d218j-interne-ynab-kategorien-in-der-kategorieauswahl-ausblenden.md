---
id: ledger-d218j
title: Interne YNAB-Kategorien in der Kategorieauswahl ausblenden, Ready to Assign behalten
status: backlog
type: feature
context: ledger
created: 2026-10-19
completed:
depends_on: []
blocks: []
tags: [captured, kassenbuch, kategorie]
related_adrs: []
related_research: []
prior_art: []
---

## Why
In der Kategorieauswahl der Kassenbuch-Filterleiste erscheint die YNAB-Gruppe "Internal
Master Category" mit "Inflow: Ready to Assign", "Deferred Income SubCategory" und
"Uncategorized". Die beiden letzten sind YNAB-Interna, die Roman nie filtern will; sie
machen die Auswahl länger und verwirren. Screenshot von Roman am 2026-10-19.

## What
Entscheidung Roman (2026-10-19): "Deferred Income SubCategory" und "Uncategorized" aus
der Kategorieauswahl ausblenden, "Inflow: Ready to Assign" bleibt wählbar, weil Einnahmen
dorthin gehen.

Offen für die Verfeinerung:
- Woran die internen Kategorien sicher erkannt werden (ist der Gruppenname "Internal
  Master Category" stabil, oder gibt es eine feste Kennung? per lesendem `curl` gegen
  `/budgets/<id>/categories` klären; Hinweise in `src/Server/YnabClient.fs`
  `categoryGroupsDecoder` und `src/Server/Api.fs` um Zeile 330).
- Wo gefiltert wird: an der Quelle (Server, für alle Auswahlen) oder nur in der
  Kassenbuch-Auswahl. Andere Kategorieauswahlen (Regeln, Import prüfen, Split) prüfen.
- Buchungen, die in YNAB auf "Uncategorized" stehen, deckt der Chip "Ohne Kategorie"
  bereits ab (ADR 0048); das bleibt so.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured on 2026-10-19 aus dem Gespräch zu Romans Screenshot des Kategorie-Popovers.
Raw, unrefined; braucht einen `modeling`-Durchgang. Die Gestaltung des Popovers selbst
(Schriftgröße, Betragsfarben, Knopf) gehört nicht hierher, sondern zur Bestandsaufnahme
gegen ADR 0049.
