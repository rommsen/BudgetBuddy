---
id: design-system-x045q
title: "Detail: Memo als Block mit Bezeichnung darüber und ganzem Text, Memo-Editor mehrzeilig"
status: backlog
type: feature
context: design-system
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: [ledger-wg42w]
tags: [design-exploration, memo, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Roman am 2026-11-19: Das Memo steht in Karte und Detail als einzige Zeile mit der Bezeichnung mittig links davor
(`Detail.fieldRowWith` setzt Bezeichnung und Wert nebeneinander, `items-center`); bei zwei Zeilen Text „hässlich, auch an
anderen Detail-Sheets“. Nach `›` zeigt der Editor den Text wieder abgeschnitten, weil `Detail.memoEditor` ein
einzeiliges `Input.text` ist.

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Neue Variante für mehrzeilige Felder im Detail: Block, oben „Memo“ und rechts „Bearbeiten ›“ (ohne Memo „hinzufügen ›“),
  darunter der ganze Text über die volle Breite. Einzeilige Zeilen bleiben „Bezeichnung links, Wert rechts“.
- `Detail.memoEditor` mit mitwachsendem mehrzeiligem Feld (Textarea), Enter speichert weiter (YNAB-Memos sind
  einzeilig, das Feld bricht nur um), Zähler, Abbrechen, Speichern wie heute; „Speichern“ bleibt bei offener Tastatur im Bild.
- Gilt im Detail des Kassenbuchs und im Detail des Durchgehens.

## Acceptance criteria
- [ ] Memo im Detail als Block nach der Skizze, auf `/styleguide` gezeigt, §5 nachgezogen.
- [ ] Editor mehrzeilig mit ganzem Text; Sweep-Zustände `kassenbuch-detail` und `durchgehen-details-memo` mit langem Memo.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6 (Teil Memo, Spike-Regel: der mehrzeilige Editor war klar und billig).
