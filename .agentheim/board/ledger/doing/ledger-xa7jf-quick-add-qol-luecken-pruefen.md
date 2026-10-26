---
id: ledger-xa7jf
title: "Spike: Quick Add und Import-Seite nach Komfortfunktionen durchsehen, die dem Kassenbuch fehlen; Ergebnis ist eine Liste mit Begründung, aus der Roman die Backlog-Tasks auswählt"
status: doing
type: spike
context: ledger
created: 2026-10-25
completed:
depends_on: []
blocks: []
tags: [captured, quick-add, import, kassenbuch, komfort]
related_adrs: ["0021"]
related_research: []
prior_art: []
---

## Why
Im neuen Kassenbuch fehlen Quality-of-Life-Funktionen. Quick Add und die Import-Seite
(SyncFlow) sind älter und haben im Alltag schon Komfort angesammelt (z. B. die Vorlagen
in Quick Add, ynab-t4n8p). Ein Vergleich zeigt, was davon im Kassenbuch fehlt und helfen
würde.

## What
Quick Add (`Navigation.QuickAdd`, Client-State `QuickAdd*`) und die Import-Seite
(`src/Client/Components/SyncFlow/`) durchsehen und jede Funktion notieren, die das
Kassenbuch (`src/Client/Components/Ledger/`) nicht hat. Zu jeder Lücke: was sie tut,
wo sie heute steckt, ob und wie sie im Kassenbuch helfen würde.

Ausdrücklich nicht im Umfang:
- Die Frage, ob die Quick-Add-Seite ganz im Kassenbuch aufgehen kann (Entscheid Roman,
  2026-10-26: nur einzelne Funktionen vergleichen).
- Umsetzung. Der Spike ändert keinen Code.
- Follow-up-Tasks direkt anlegen. Roman wählt aus der Liste aus, erfasst wird danach per
  `modeling`.

## Acceptance criteria
- [ ] Das Outcome enthält eine Tabelle der Lücken mit den Spalten Funktion, Quelle
      (Quick Add oder Import-Seite, mit Datei bzw. Symbol), Nutzen im Kassenbuch und
      Einschätzung (sinnvoll / fraglich / nicht übertragbar) samt einem Satz Begründung.
- [ ] Funktionen, die das Kassenbuch schon in anderer Form hat, stehen nicht als Lücke in
      der Tabelle, sondern in einer kurzen Liste "schon abgedeckt" mit Verweis auf die
      Kassenbuch-Stelle oder die erledigte Task.
- [ ] Jede als sinnvoll eingeschätzte Lücke wird gegen das offene ledger-Backlog geprüft;
      Überschneidungen (z. B. ledger-hg5m5 Regel-Angebot) sind in der Tabelle vermerkt.
- [ ] Es entstehen keine Code-Änderungen und keine neuen Backlog-Tasks.

## Notes
Wörtlich vom Nutzer: "man müsste noch mal rüber schauen, ob es aus dem Quick Think noch
andere Quality of Life Dinge gibt, die helfen könnten." Im Refine am 2026-10-26 geklärt:
gemeint sind Quick Add und zusätzlich die Import-Seite.

Hintergrund: seit ledger-tdnpk (ADR 0021) schreibt Quick Add zuerst ins Kassenbuch und
pusht danach. Quick Add ist im Kassenbuch eine Herkunft, die Oberfläche liegt auf einer
eigenen Seite. Concept-Page zu Quick Add: siehe ynab-t4n8p.

Stop-loss (ADR-0065): if, mid-spike, the mitigation is already known and cheap, record it and stop.
