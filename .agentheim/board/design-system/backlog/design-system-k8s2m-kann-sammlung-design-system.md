---
id: design-system-k8s2m
title: "Kann-Sammlung design-system"
status: backlog
type: chore
context: design-system
created: 2026-11-07
completed:
depends_on: []
blocks: []
tags: [review, design, a11y, styleguide]
related_adrs: [0087]
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
Geschnitten und priorisiert wird im Refinement.

## Aus dem Review-Lauf nach design-system-bcdqy und infrastructure-nx14s (2026-11-07)
- Fokusring des Schalters „Geprüft“ (`src/Client/DesignSystem/Detail.fs`, `reviewedSwitch`): am Handy liegt er auf dem unsichtbaren 48-px-Quadrat statt auf der Schiene. Abhilfe: Klasse an der Schiene und `button:focus-visible > .switch-track`.
- Styleguide §4 und §7.14 „höchstens drei Schriftgrößen je Ansicht“: die Budget-Seite hat mit `FontSizes.compact`, `microPlus`, Titel und Text wohl mehr. Prüfen und, falls ja, als Zeile in Etappe 3 des Fahrplans.
- Untere Navigation bei 320 px: zwischen den sechs Einträgen hängt der Abstand an `justify-around`, die 8 px aus §7.15 misst der Sweep nicht. Erledigt sich mit „Navigation nur mit den drei Orten“ (Etappe 3).
- `grid-cols-[1fr_2fr]` im Tab Target (`TargetFormView.fs`) ist ein freier Wert; bei 320 px bleiben dem Betragsfeld rund 90 px. Am Bildschirm prüfen.
- `src/Client/DesignSystem/ErrorDisplay.fs:355` zeigt den sichtbaren Text „Dismiss“, englisch (Styleguide §8). Aufgefallen beim Review-Lauf, außerhalb seines Rahmens.
- Rohe 48-px-Klassen außerhalb von `TouchTargets` bestehen noch in `Button.fs:75`, `:76`, `:489`, `Input.fs:36` und `Components/Budget/TargetsView.fs:34` (`min-h-12 md:min-h-10`). Auf `TouchTargets.mobileMinHeight` umstellen (Review-Lauf nach jc1y1).

## Aus design-system-dak16 (2026-11-08, Palette 2)
- PC-Statusspalte im Kassenbuch: „zu prüfen“ plus ein zweites Kennzeichen stößt an den Betrag (Zeilen „Rückfrage und Prüfung“, „Markiert und zu prüfen“ im Sweep-Zustand `kassenbuch` bei 1440); der Sweep meldet es nicht. Schon vorher so eng.
- `Loading.neonPulse` und `Loading.progressBar` glühen noch; nach ADR 0087 Punkt 6 nur Hauptaktion und Rückmeldung.
- Styleguide §1 („Glow … positive Beträge, fokussierte Karten“, „Neon-Akzente“) auf ADR 0087 bringen.
- App-Icons (`src/Client/public/icon-master.svg`, `favicon.svg`, Raster unter `public/icons/`) tragen noch den alten Verlauf Teal→Grün→Orange.
- `Detail.amount` und `MultiSelect.amountClassName` haben keinen eigenen Test auf die neutrale Betragsfarbe (Verifier dak16).

## Aus dem Review-Lauf nach design-system-dak16 (2026-11-08)
- Settings „Nicht konfiguriert“: roter Punkt mit gelbem Text, zwei Rollen für einen Zustand (ADR 0087).
- Settings: Ist Comdirect nicht eingerichtet, steht dessen Formular offen, und „Zugangsdaten speichern“ ist primär, auch wenn eine andere Karte bearbeitet wird (zwei Hauptaktionen).
- `TextGlowTokenTests.fs` ist mit dem Modul `TextGlows` gelöscht (Bugbezug design-system-tg2wn); ein Rückdreh-Beleg nach Doktrin Punkt 8 fehlt, weil es den Code nicht mehr gibt.
