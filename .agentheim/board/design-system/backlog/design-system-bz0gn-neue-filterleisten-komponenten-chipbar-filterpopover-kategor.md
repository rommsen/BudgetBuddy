---
id: design-system-bz0gn
title: "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"
status: backlog
type: chore
context: design-system
created: 2026-09-25
depends_on: [ledger-j8xq4]
blocks: []
tags: [frontend, design-system, kassenbuch]
related_adrs: []
related_research: []
prior_art: [ledger-j8xq4]
---

## Why

`ledger-j8xq4` hat zwei neue Design-System-Module gebaut, `Client.DesignSystem
.ChipBar` (Chip, "Alle Filter löschen", Chip-mit-Popover-Wrapper) und
`Client.DesignSystem.FilterPopover` (PC-Popover, Esc/Klick außerhalb schließen),
plus mehrere neue Bausteine direkt in `Components.Ledger.View`
(Kategorienauswahl mit Mehrfachauswahl, Zeitraum-Schnellwahl, Payee-
Vorschlagsliste). Der Task-Text von `ledger-j8xq4` selbst hält fest, dass die neuen
Komponenten "als echte Komponenten ins Design-System" gehören und als Karten in die
`/styleguide`-Route sollen (`design-system-t3vqr` ist das Vorbild für "neue
Money-Varianten in Styleguide und Route" nach demselben Muster). Das ist bei
`ledger-j8xq4` bewusst nicht mitgemacht worden, um den Umfang der ohnehin schon
großen Aufgabe nicht weiter auszudehnen. Ursprünglich fälschlich als `ledger-h2vtc`
(Kontext `ledger`) erfasst; das ist Design-System-Arbeit, nicht Kassenbuch-Arbeit,
deshalb hier mit neuer Id und `context: design-system` neu aufgelegt.

## What

Neue Karten in `src/Client/Components/Styleguide/View.fs` (Muster: bestehende
Karten wie `CategoryPickerDemo`) für:
- `Client.DesignSystem.ChipBar` (aktiv/inaktiv, mit/ohne "×", "Alle Filter
  löschen").
- `Client.DesignSystem.FilterPopover` (offen/geschlossen, Esc-Verhalten).
- Die Kategorienauswahl mit Mehrfachauswahl (versteckte Kategorien eingeklappt,
  Available rechtsbündig): ggf. als eigene, aus `Components.Ledger.View`
  herausgezogene, wiederverwendbare Funktion, falls sie nicht bereits so
  geschnitten ist, dass sie ohne das Ledger-Modell aufrufbar ist.

## Acceptance criteria
- [ ] `/styleguide` zeigt je eine Karte für ChipBar, FilterPopover und die
      Kategorienauswahl mit Mehrfachauswahl, mit `@dsCard`-Marker wie die
      bestehenden Karten.
- [ ] `npm run export:design-system` und dessen eigener Test
      (`design-system-w4n9r`) bleiben grün.
- [ ] `npm run build` sauber.