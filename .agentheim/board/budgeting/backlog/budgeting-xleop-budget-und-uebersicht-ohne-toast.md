---
id: budgeting-xleop
title: "Budget und Übersicht am Handy ohne Toast (R3), dazu „Rückgängig“ nach einer Verschiebung"
status: backlog
type: feature
context: budgeting
created: 2026-11-19
completed:
depends_on: [design-system-2k7uw]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Regel R3 (design-system-2k7uw), Skizze Abschnitt 3. Roman am 2026-11-19: nach einer Verschiebung soll es „Rückgängig“ geben.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Sheet „Geld“: Ergebnis in der Zeile über dem Knopf („✓ 50,00 € nach Reise“, „Rückgängig“), „Verfügbar“ leuchtet kurz;
  Fehler rot an derselben Stelle mit „Erneut senden“, wo es das heute gibt. Rückgängig ist eine Verschiebung in die
  Gegenrichtung (Ereignis, ADR 0083).
- „Beträge/Monate/Historie nicht neu gelesen“: Hinweis unter „Zu verteilen“ mit „Neu laden“, bleibt bis es klappt.
- Targets: am Target im Sheet bzw. in der Zeile der Prüfung. Kategorien verwalten: neue Zeile leuchtet, Fehler am Feld.
- Übersicht: Leiste mit „+ Buchung“ wie im Kassenbuch; „angepinnt/gelöst“ ohne Meldung, die Kategorie leuchtet im Bereich „Angepinnt“.

## Acceptance criteria
- [ ] Kein Toast in Budget und Übersicht am Handy; Tabelle „Übrige Seiten“ der Skizze abgearbeitet.
- [ ] Rückgängig nach Verschiebung mit Test (Domäne und Client).
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Vermutlich beim Verfeinern teilen (Rückgängig der Verschiebung ist eine eigene Fähigkeit).
