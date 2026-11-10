---
id: design-system-ak97z
title: "Segmented mit KeepsFocus: Ausgabe/Einnahme im Sheet Betrag schließt die Tastatur nicht mehr"
status: todo
type: bug
context: design-system
created: 2026-11-10
completed:
depends_on: []
blocks: []
tags: [laufend, frontend, tastatur, handy, betrag]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Im Sheet "Betrag" (ledger-m7mye, ledger-q42pg) nimmt ein Tipp auf "Ausgabe" oder "Einnahme" (`Segmented`) dem
Betragsfeld den Fokus, am Handy schließt sich die Tastatur. Das verletzt die Regel aus CLAUDE.md, dass der Wechsel
zwischen zwei Feldern die Tastatur nicht schließen darf. Gefunden im Review-Lauf 2026-11-10; Roman: eigene Task.

## What
- `Segmented` bekommt eine Option wie `Button.KeepsFocus` (onMouseDown preventDefault am Segment), Vorgabe aus.
- Das Sheet bzw. der Dialog "Betrag" im Kassenbuch nutzt sie, das Betragsfeld behält den Fokus beim Umschalten.
- Ebenso "Ausgabe | Einnahme" in "Neue Buchung" im Kassenbuch (`src/Client/Components/Ledger/View.fs` um Zeile 3650,
  ledger-at63w), falls dort ein Betragsfeld daneben den Fokus haben kann.
- Form: ein Feld `KeepsFocus: bool` am `Segment`-Record (`src/Client/DesignSystem/Segmented.fs`) oder ein Parameter von
  `Segmented.view`; der Worker wählt die Form, die zu `Button.KeepsFocus` passt, und zieht alle Nutzer nach.
- Andere Nutzer von `Segmented` (Tab Geld, Rhythmus im Tab Target) bleiben unverändert, außer dort steht ebenfalls
  ein Betragsfeld daneben; das prüft der Worker und nennt es im Outcome.
- Beispiel unter /styleguide, Zeile im Inventar `Segmented` (Styleguide §5).

## Acceptance criteria
- [ ] Layout-Sweep `kassenbuch-betrag-feldfehler` (oder eigener Zustand): Fokus im Betragsfeld, Tipp auf "Einnahme",
      kein `blur` am Feld; Gegenprobe ohne die Option wird rot.
- [ ] /styleguide zeigt `Segmented` mit der Option, Styleguide §5 nennt sie.
- [ ] Am Handy bei 412 px mit Touch: Umschalten lässt die Tastatur offen. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Vorbild: `Button.KeepsFocus` (Commit 5444edd7). Sichtbares Verhalten gegen den Styleguide geprüft
(`.claude/rules/task-ui-vorgaben.md`): keine Abweichung.
