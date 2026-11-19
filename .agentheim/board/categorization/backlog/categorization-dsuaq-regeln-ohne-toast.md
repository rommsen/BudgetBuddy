---
id: categorization-dsuaq
title: "Regeln am Handy ohne Toast (R3): gelöschte Regel als Zeile am Platz mit Rückgängig, gespeichert am Ort"
status: backlog
type: feature
context: categorization
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
Regel R3 (design-system-2k7uw), Skizze Abschnitt 3 „Regeln“.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- „Regel gelöscht · Rückgängig“: an ihrem Platz eine gestrichelte Zeile „„<Name>“ gelöscht · Rückgängig“ bis zur nächsten Aktion.
- „Regel gespeichert“: Panel schließt, die Regel leuchtet kurz. Fehler (Reihenfolge, Löschen, Laden) an der Regel bzw. als Hinweis der Seite.

## Acceptance criteria
- [ ] Kein Toast auf „Regeln“ am Handy.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6.
