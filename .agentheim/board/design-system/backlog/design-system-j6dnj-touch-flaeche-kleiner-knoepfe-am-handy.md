---
id: design-system-j6dnj
title: "Touch-Fläche der kleinen Knöpfe am Handy auf mindestens 48 px bringen: die DS-Größe Button.Small wird mobil 48 px hoch (Split-Editor: Barabhebung, Zeile hinzufügen und alle übrigen Small-Knöpfe)"
status: backlog
type: bug
context: design-system
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: []
tags: [captured, touch, button, mobile]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Der Verifier von ledger-s8t3s hat gemessen: "Barabhebung" und "Zeile hinzufügen" im Split-Editor des Kassenbuchs sind am Handy nur 36 px hoch. Der Styleguide (`standards/frontend/styleguide.md` §4, `TouchTargets.minSize`) verlangt mobil mindestens 48 px für alle Tappables.

## What
Die Höhe kommt aus der DS-Größe `Button.Small` in `src/Client/DesignSystem/Button.fs` (`text-xs px-3 py-1.5 min-h-[36px] md:min-h-0`). Entscheidung Roman (2026-10-27): die Korrektur geht in die DS-Größe selbst, nicht nur in den Split-Editor. Mobil wird Small mindestens 48 px hoch, ab `md` bleibt es wie heute (`md:min-h-0`). Das betrifft alle Verwendungen von `Button.Small`, auch `Button.iconButton` (Bearbeiten, Löschen) und die rund 26 Stellen in DesignSystem, Kassenbuch und Import.

Icon-only-Knöpfe (`iconButton`) brauchen am Handy auch 48 px Breite, sonst bleibt die Fläche schmal.

## Acceptance criteria
- [ ] `Button.Small` trägt mobil eine Mindesthöhe von 48 px und ab `md` keine (Klassen-String in `Button.fs`; Unit-Test oder Browser-Wächter prüft die Klassen).
- [ ] Icon-only-Knöpfe aus `Button.iconButton` sind mobil mindestens 48 × 48 px.
- [ ] Browser-Wächter bei 390 px Breite: "Barabhebung" und "Zeile hinzufügen" im Split-Editor des Kassenbuchs messen per `getBoundingClientRect` jeweils mindestens 48 px Höhe.
- [ ] Browser-Wächter bei Desktop-Breite (≥ 768 px): dieselben Knöpfe behalten ihre bisherige Höhe (kein Anwachsen auf 48 px).
- [ ] Alle Verwendungen von `Button.Small` sind am Handy durchgesehen; Stellen, an denen 48 px das Layout sprengen (Tabellenzeilen, Modals, Badges), sind im Outcome genannt und behoben oder als Folge-Task gemeldet.
- [ ] Der lebende Styleguide (`/styleguide`) zeigt die Small-Größe weiterhin; die bestehenden Wächter bleiben grün (`npm run test:scripts`).

## Notes
Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s. Verfeinert am 2026-10-27.
Verwandt: design-system-y2bdj (Styleguide zeigt `SplitEditor.lineActions`), design-system-bym4m (Split-Ziel-Picker). Alle drei liegen im Split-Editor; j6dnj ändert nur `Button.fs` und kann parallel laufen.
