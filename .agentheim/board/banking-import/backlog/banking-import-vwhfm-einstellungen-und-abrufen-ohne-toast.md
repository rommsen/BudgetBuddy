---
id: banking-import-vwhfm
title: "Einstellungen und Abrufen am Handy ohne Toast (R3), englische Toasts entfallen"
status: backlog
type: feature
context: banking-import
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Regel R3 (design-system-ytgnc), Skizze Abschnitt 3 „Einstellungen“. Mehrere Toasts in `Settings/State.fs` sind noch
englisch („Default budget set“, „Failed to load Lena flag colour“, Kann-Sammlung ledger Punkt 152).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Gespeichert: der Abschnitt schließt und zeigt den Wert, daneben kurz „✓ Gespeichert“. Fehler rot unter dem Feld, Abschnitt bleibt offen.
- Bankverbindung: Status steht im Abschnitt, „Push-TAN bestätigen“ als gelber Hinweis dort.
- Abrufen-Sheet: Meldungen im Sheet, solange es offen ist; sonst die Leiste der Seite.

## Acceptance criteria
- [ ] Kein Toast in Einstellungen und Abrufen am Handy; alle Texte deutsch nach §8.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Erledigt Kann-Sammlung ledger Punkt 152 mit.
