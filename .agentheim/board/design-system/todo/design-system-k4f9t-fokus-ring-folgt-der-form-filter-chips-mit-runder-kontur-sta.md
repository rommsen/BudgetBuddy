---
id: design-system-k4f9t
title: "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"
status: todo
type: bug
context: design-system
created: 2026-11-04
completed:
depends_on: [design-system-001]
blocks: []
tags: [fokus, tastatur, chipbar, tabs, a11y]
related_adrs: []
related_research: []
prior_art: [design-system-bz0gn]
---

## Why
Roman (2026-11-04, Screenshots): Mit Tab durch die Filter-Chips der Budget-Seite steht ein eckiger teal Rahmen um die runde Pille. Nach einem Mausklick auf einen Tab im Inspektor und Links/Rechts ist der Fokus-Ring größer als der Tab und beim ersten Tab "Geld" links abgeschnitten.

## What
1. **Filter-Chips** (`ChipBar.chip`, `src/Client/DesignSystem/ChipBar.fs:35`): Die runde Form (`rounded-full`) sitzt am äußeren `div`, den Fokus bekommt der innere `button` ohne Rundung. Der globale Ring `*:focus-visible` (`src/Client/styles.css:224`, `outline: 2px`, `outline-offset: 2px`) zeichnet deshalb ein Rechteck. Der Ring soll der Pillenform folgen. Betrifft die Budget-Filter (Alle, Fehlt, Überzogen) und die Filterleiste im Kassenbuch, weil beide `ChipBar.chip` nutzen. Der Entfernen-Knopf "×" eines aktiven Chips bekommt einen Ring, der zur Pille passt.
2. **Tabs** (`src/Client/DesignSystem/Tabs.fs`): Der Ring liegt 2 px außerhalb des Tabs. Das Inspektor-Panel hat `overflow-hidden` (`src/Client/Components/Budget/InspectorView.fs:401`), deshalb wird er am ersten Tab abgeschnitten. Der Ring liegt künftig innen (`outline-offset: -2px`, wie `nav a:focus-visible` in `styles.css:256`), vollständig innerhalb des Tabs.
3. **Unverändert:** Wechseln Links und Rechts den Tab als Seiten-Kürzel, ohne dass der Fokus auf dem Tab liegt (budgeting-c5w2n, auch aus dem Suchfeld), erscheint kein Ring. Das ist gewollt, der Fokus bleibt, wo er ist.

## Acceptance criteria
**kaputt (Regressionstests, vor dem Fix rot)**
- [ ] `[layout]` GIVEN Budget-Seite am PC WHEN per Tastatur auf den Chip "Alle" fokussiert THEN das fokussierte Element hat eine berechnete Rundung wie die Pille (border-radius mindestens halbe Höhe) und trägt den Ring (outline-style nicht none).
- [ ] `[layout]` GIVEN Inspektor offen am PC, Fokus auf dem Tab "Geld" per Tastatur WHEN der Ring sichtbar ist THEN er liegt vollständig innerhalb des Inspektor-Panels (berechneter outline-offset negativ bzw. Ringkasten innerhalb des Panel-Rechtecks).

**intakt**
- [ ] Ein Mausklick auf Chip oder Tab zeigt weiter keinen Ring (`:focus-visible`), Links und Rechts aus dem Suchfeld wechseln den Tab weiter ohne Ring.
- [ ] Bestehende Tests zu ChipBar, Tabs und Filterleiste bleiben grün; neue Browser-Tests tragen eine gültige Kennung (Doktrin Punkt 5).
- [ ] `/styleguide` zeigt Chip und Tabs mit dem neuen Fokus-Ring unverändert sonst.
- [ ] Der Ring wirkt am Chip rund und am Tab sauber innerhalb, am PC und am Handy. [human-eye]
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Erfasst, verfeinert und promotet am 2026-11-04 in einem Zug, Inhalt von Roman freigegeben (Diagnose im Gespräch: Ursache 1 eckiger Button im runden Container, Ursache 2 Offset außen plus overflow-hidden; Punkt 3 bewusst so gelassen).

Werte aus Styleguide und `styles.css` nachschlagen, keine neuen Farben. Der Fix gehört in die DS-Komponenten bzw. in `styles.css`, nicht in die Feature-Views.
