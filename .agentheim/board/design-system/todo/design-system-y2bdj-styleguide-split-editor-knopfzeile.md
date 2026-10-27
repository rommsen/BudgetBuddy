---
id: design-system-y2bdj
title: "Lebender Styleguide zeigt die Knopfzeile des Split-Editors mit Barabhebung (SplitEditor.lineActions statt nur addLineButton)"
status: todo
type: chore
context: design-system
created: 2026-10-26
completed:
depends_on: [design-system-001]
blocks: [design-system-bym4m]
tags: [captured, styleguide, split-editor]
related_adrs: []
related_research: []
prior_art: [design-system-003]
---

## Why
Mit ledger-s8t3s kam die DS-Funktion `SplitEditor.lineActions` (Zeile hinzufügen plus Barabhebung) dazu. Der lebende Styleguide `/styleguide` rendert aber nur `SplitEditor.addLineButton` und bildet die echte Knopfzeile damit nicht ab.

## What
In `src/Client/Components/Styleguide/View.fs` (Galerie-Karte des Split-Editors, um Zeile 1159) die Knopfzeile `SplitEditor.lineActions` zeigen statt nur `addLineButton`. Die Galerie ruft die echte DS-Funktion auf, damit sie nicht vom Kassenbuch abweicht. Die Callbacks bleiben in der Galerie wirkungslos (`ignore`), wie bei den anderen Beispielen dort.

Prüfen, ob `addLineButton` danach noch eine Verwendung hat. Hat es keine mehr, entfällt die Funktion (keine toten DS-Funktionen).

## Acceptance criteria
- [ ] Die Split-Editor-Karte in `/styleguide` enthält beide Knöpfe der Knopfzeile, "Zeile hinzufügen" und "Barabhebung" (Browser-Wächter oder bestehender Styleguide-Test prüft beide Texte).
- [ ] Die Karte rendert `SplitEditor.lineActions` selbst, keine nachgebaute Knopfzeile.
- [ ] `SplitEditor.addLineButton` hat entweder eine weitere Verwendung oder ist entfernt.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Herkunft: nicht blockierender Hinweis des Verifiers von ledger-s8t3s. Verfeinert am 2026-10-27.
Blockiert design-system-bym4m, weil beide die Split-Editor-Karte in `Styleguide/View.fs` ändern und parallel kollidieren würden. y2bdj ist klein und geht zuerst.
