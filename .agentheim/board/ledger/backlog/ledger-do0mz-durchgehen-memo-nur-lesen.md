---
id: ledger-do0mz
title: "Durchgehen: Memo auf der Karte nur lesen (C3), so lang, wie Platz ist; Bearbeiten nur in „Details“"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-x045q, ledger-3pq7c]
blocks: []
tags: [design-exploration, durchgehen, memo, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Roman am 2026-11-19: Memo `›` auf der Karte zeigt den Text nicht ganz, und Bearbeiten gibt es auf der Karte und in
„Details“: „irgendwie doppelt“. Entscheidung C3; Roman streicht damit ausdrücklich das Bearbeiten auf der Karte.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Die Karte zeigt ein vorhandenes Memo nur zum Lesen: „Memo“ darüber, der Text über die volle Breite, ohne `›`, so lang,
  wie Platz ist. Nur wenn die Karte sonst überliefe, endet es mit „…“ in der letzten Zeile, die passt.
- Ohne Memo fehlt die Zeile, „hinzufügen“ entfällt. Bearbeiten über „Details“ (Memo-Block aus design-system-x045q).
- Memo-Sheet und -Dialog der Karte (`reviewEditors`) entfallen.

## Acceptance criteria
- [ ] Memo auf der Karte nach der Skizze, gemessen statt fester Zeilenzahl; Test mit kurzem und langem Memo bei 412 und 320 px.
- [ ] Abgleich: „Memo bearbeiten auf der Karte“ steht als gestrichen im Outcome (Roman, 2026-11-19).
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6.
