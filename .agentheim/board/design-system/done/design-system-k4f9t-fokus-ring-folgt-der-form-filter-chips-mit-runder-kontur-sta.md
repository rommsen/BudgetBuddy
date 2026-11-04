---
id: design-system-k4f9t
title: "Fokus-Ring folgt der Form: Filter-Chips mit runder Kontur statt eckigem Rahmen, Tabs mit Ring nach innen statt abgeschnitten im Inspektor"
status: done
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

## Builder note (2026-11-04)

Roman: die beiden `[layout]`-Regressionstests (Chip-Ring rund, Tab-Ring innen) zu einem Browser-Test zusammenlegen. Beide Aussagen bleiben, der Test muss weiter vor dem Fix rot sein.

## Outcome

Der Fokus-Ring folgt der Form der Komponente. Beide Fehler stecken im Design-System, die Feature-Views sind unverändert.

- **Filter-Chips** (`src/Client/DesignSystem/ChipBar.fs`): Der Label-Knopf und der Entfernen-Knopf "×" tragen jetzt selbst `rounded-full`, bisher saß die Rundung nur am äußeren Rahmen. Der globale Ring `*:focus-visible` folgt der Rundung des fokussierten Knopfs und ist dadurch rund. Jeder Knopf bekommt seinen eigenen runden Ring, so bleibt beim aktiven Chip sichtbar, ob das Label oder das "×" den Fokus hat. Das gilt für die Budget-Filter, die Kassenbuch-Filterleiste, MultiSelect und `/styleguide`, weil alle `ChipBar.chip` nutzen.
- **Tabs** (`src/Client/styles.css`): neue Regel `[role="tab"]:focus-visible { outline-offset: -2px; }`, nach dem Vorbild von `nav a:focus-visible`. Der Ring liegt damit ganz innerhalb des Tabs und wird vom `overflow-hidden` des Inspektors nicht mehr abgeschnitten. Farbe und Breite kommen weiter aus der globalen Regel. Die Regel steht in `styles.css`, weil eine Tailwind-Klasse (`@layer utilities`) gegen die ungelayerte globale Regel verlieren würde.
- **Unverändert:** Wechseln Links und Rechts aus dem Suchfeld den Tab, erscheint kein Ring, weil der Fokus im Suchfeld bleibt.
- **Regressionstest** in `scripts/budget-page.test.mjs`: ein Test `[layout] Fokus-Ring am PC: per Tastatur liegt er am Tab Geld innen und ganz im Inspektor, am Chip Alle in Pillenform, nach einem Mausklick fehlt er` (Iteration 2 auf Wunsch von Roman: die zwei Tests aus Iteration 1 zusammengelegt, ein Browser-Start, eine Seite). Er prüft zuerst den Tab (Mausklick ohne Ring, dann Rechts und Links: Ring sichtbar, outline-offset negativ, Ringkasten im Panel), danach den Chip (Mausklick auf Fehlt ohne Ring, Shift+Tab auf Alle: Ring sichtbar, Rundung mindestens halbe Höhe). Vor dem Fix waren beide Aussagen rot.
- **Gegenprobe, einzeln:** ohne `rounded-full` an den Chip-Knöpfen rot mit "Chip: Rundung wie die Pille (0 px bei 28 px Höhe)"; ohne die Regel `[role="tab"]:focus-visible` rot mit "Tab: der Ring liegt innen (outline-offset 2 px)". Beide Fixes danach wiederhergestellt, Test grün. Der bestehende `[tastatur]`-Test zu Links und Rechts aus dem Suchfeld bleibt grün.
- `./verify.sh`: `VERIFY: PASS`. Offen bleibt der Blick auf PC und Handy: wirkt der Ring am Chip rund und am Tab sauber innen? [human-eye]
