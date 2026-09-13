---
id: design-system-my32n
title: "DaisyUI raus: die toten Klassen durch eigene Tailwind-Klassen oder Tokens ersetzen"
status: backlog
type: refactor
context: design-system
created: 2026-09-13
completed:
depends_on: [design-system-fq8dn, design-system-001]
blocks: []
tags: [styleguide, tailwind, visuell]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
`design-system-fq8dn` hält die Entscheidung fest und liefert die belastbare Liste der
Klassen, die im Client stehen, aber nirgends definiert sind. Diese Aufgabe ersetzt sie.

Der Reiz und das Risiko liegen beide darin, dass die Klassen **wirkungslos** sind. Das
Ersetzen ändert das Aussehen also nicht überall gleich: An Stellen, wo die Komponente
ohnehin genug eigene Tailwind-Klassen mitbringt, ändert sich nichts. An Stellen wie der
Tabelle, wo die tote Klasse das Einzige war, was Abstand hätte geben sollen, ändert sich
das Bild deutlich, und zwar zum Besseren.

## What
Je betroffener Komponente: die tote Klasse entfernen und, wo sie etwas hätte tun sollen,
durch echte Tailwind-Klassen oder Tokens ersetzen. Die konkreten Kriterien je Komponente
kommen aus `design-system-fq8dn`.

**Verbindlich dabei:**
- **Was die Komponente leisten soll, steht im Styleguide**
  (`standards/frontend/styleguide.md`), nicht in DaisyUIs Vorstellung davon. Wo der
  Styleguide zu einer Komponente schweigt, ist das beim Ersetzen zu ergänzen.
- **Das visuelle Gate gilt** (design-system-README): vorher und nachher einen Screenshot
  der `/styleguide`-Route aufnehmen und vergleichen. Eine unbeabsichtigte Änderung am
  Aussehen ist ein Fehlschlag, eine beabsichtigte gehört benannt.
- **Die Kassenbuch-Tabelle hat ihr Zellen-Padding in `ledger-x0yga` bereits direkt
  gesetzt**, als Pflaster. Beim Ersetzen gehört es dorthin, wo es hingehört, und das
  Pflaster wird entfernt.

**Reihenfolge-Vorschlag**, nach Wirkung absteigend: `Table.fs` (dort war der Schaden
sichtbar), dann `Loading.fs`, dann die übrigen aus der Liste. Es spricht nichts dagegen,
das in mehreren Durchgängen zu tun; die Aufgabe muss nicht in einem Stück fallen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten am 2026-09-13 nach Romans Entscheidung „DaisyUI raus". Zusammenhang:
`design-system-fq8dn` (Bestandsaufnahme und ADR), `design-system-thtpp` (Wächter, damit es
nicht zurückkommt), `ledger-x0yga` (dort fiel es auf).

Nach dieser Aufgabe lohnt ein Blick, ob `package.json` und die Build-Kette noch Spuren
tragen, die DaisyUI erwarten.
