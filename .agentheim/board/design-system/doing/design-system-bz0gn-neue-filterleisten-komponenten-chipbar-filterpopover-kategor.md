---
id: design-system-bz0gn
title: "Neue Filterleisten-Komponenten (ChipBar, FilterPopover, Kategorienauswahl mit Mehrfachauswahl) in die /styleguide-Route aufnehmen"
status: doing
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

Geprüft: `Client.DesignSystem.ChipBar`/`FilterPopover` (`src/Client/DesignSystem/
ChipBar.fs`, `.../FilterPopover.fs`) sind bereits saubere DS-Komponenten (Props-Records,
keine Abhängigkeit von `Components.Ledger.Types.Model`/`Msg`) und lassen sich direkt mit
lokalem `React.useState` demonstrieren, wie `CategoryPickerDemo`/`ToastDemo` es für
andere interaktive Komponenten schon tun. Die Kategorienauswahl mit Mehrfachauswahl
(`categoryPickerBody`, `categoryRow`, `categoryMatchesSearch`, `categoryGuidOf` in
`src/Client/Components/Ledger/View.fs`, Zeilen ~867-954) ist dagegen NICHT so geschnitten:
sie nimmt `dispatch: Msg -> unit` und `model: Model` direkt entgegen und liest
`model.CategoryDraft`/`model.CategorySearch`/`model.HiddenCategoriesExpanded`, dispatcht
`ToggleCategoryDraft`/`SetCategorySearch`/`ToggleHiddenCategoriesExpanded`/
`ResetCategoryDraft`/`ApplyCategoryFilter` (`Components/Ledger/Types.fs`). Ohne
Herausziehen ist sie außerhalb des Kassenbuch-Models nicht aufrufbar; das Herausziehen ist
deshalb Teil dieses Tasks, nicht optional.

Neue Karten/Sektionen in `src/Client/Components/Styleguide/View.fs` (nächste freie
Sektionsnummern 23-25, Muster: bestehende nummerierte Sektionen):

1. **"23. ChipBar"**: `ChipBar.bar` mit mehreren `ChipBar.chip` (aktiv/inaktiv, mit/ohne
   "×") und `ChipBar.clearAllButton`. `data-ds-card="components-chipbar"`,
   `data-ds-group="Components"`.
2. **"24. FilterPopover"**: ein Chip über `ChipBar.chipWithPopover`, Popover offen (lokaler
   `React.useState`, Trigger zum Umschalten für die Demo) mit Platzhalter-Inhalt.
   Esc-Verhalten bleibt `[human-eye]`/manueller Test (keine automatisierte Tastatur-
   Simulation im Export nötig). `data-ds-card="components-filterpopover"`,
   `data-ds-group="Components"`.
3. **"25. Kategorienauswahl mit Mehrfachauswahl"**: neue, aus `Components.Ledger.View`
   herausgezogene DS-Komponente `Client.DesignSystem.CategoryMultiSelect`
   (neue Datei `src/Client/DesignSystem/CategoryMultiSelect.fs`, in `Client.fsproj`
   unmittelbar nach `DesignSystem/FilterPopover.fs`, Zeile 29, einfügen, vor allen
   `Components/...`-Einträgen):
   ```fsharp
   type CategoryMultiSelectProps = {
       Categories: YnabCategory list
       Selected: Guid Set
       Search: string
       HiddenExpanded: bool
       OnToggle: Guid -> unit
       OnSearchChange: string -> unit
       OnToggleHiddenExpanded: unit -> unit
       OnReset: unit -> unit
       OnApply: unit -> unit
   }
   let view (props: CategoryMultiSelectProps) : ReactElement = ...
   ```
   Verhalten 1:1 wie das heutige `categoryPickerBody` (versteckte Kategorien eingeklappt
   mit "Versteckt (n)", Available rechtsbündig grün/rot/gedämpft, Click-Commit über
   "Übernehmen"/"Zurücksetzen"). `Components.Ledger.View`s `categoryPickerBody`-Aufrufstelle
   wird zu einem dünnen Wrapper, der aus `model`/`dispatch` den Props-Record baut und
   `CategoryMultiSelect.view` aufruft; Kassenbuch-Verhalten bleibt unverändert. Die neue
   Styleguide-Sektion füllt den Props-Record mit lokalem `React.useState` (gleiches Muster
   wie `CategoryPickerDemo`); die dort schon vorhandene Beispiel-Kategorienliste (fünf
   Kategorien, zwei Gruppen) wiederverwenden statt ein zweites Fixture zu pflegen.
   `data-ds-card="components-categorymultiselect"`, `data-ds-group="Components"`,
   `data-ds-card-name="Kategorienauswahl (Mehrfachauswahl)"`, bewusst nicht einfach
   "Kategorienauswahl", das ist schon Sektion 19s Einzelauswahl-Picker
   (`components-categorypicker`), beide Karten müssen im Bundle unterscheidbar bleiben.

4. Alle drei neuen Karten-IDs in `scripts/export-design-system.mjs`s `COMPONENT_CARDS`
   eintragen (Gruppe "Components") und in `scripts/export-design-system.test.mjs`s
   `KNOWN_COMPONENT_CARD_IDS`-Regressionsanker (aktuell 16 Einträge, danach 19) mitziehen.

## Acceptance criteria
- [ ] `/styleguide` zeigt je eine Karte für ChipBar (`components-chipbar`), FilterPopover
      (`components-filterpopover`) und die Kategorienauswahl mit Mehrfachauswahl
      (`components-categorymultiselect`), mit `@dsCard`-Marker wie die bestehenden Karten.
- [ ] Die Kategorienauswahl mit Mehrfachauswahl ist als `Client.DesignSystem.
      CategoryMultiSelect` herausgezogen, ohne Abhängigkeit von
      `Components.Ledger.Types.Model`/`Msg`; das Kassenbuch ruft sie über einen aus
      `model`/`dispatch` gebauten Props-Record auf, Filterleisten-Verhalten im Kassenbuch
      bleibt unverändert (bestehende Ledger-Tests/Smoke-Pfad weiterhin grün).
- [ ] `scripts/export-design-system.mjs`s `COMPONENT_CARDS` und
      `scripts/export-design-system.test.mjs`s `KNOWN_COMPONENT_CARD_IDS` enthalten die
      drei neuen Karten-IDs.
- [ ] `npm run export:design-system` und dessen eigener Test
      (`design-system-w4n9r`) bleiben grün.
- [ ] `npm run build` sauber, `dotnet test` grün.
- [ ] `[human-eye]` Die neuen Karten entsprechen optisch der bestehenden
      Kassenbuch-Filterleiste (Chips, Popover, Kategorienauswahl aus `ledger-j8xq4`).

## Notes

**Überschneidung mit `design-system-an22r`:** beide Tasks ändern
`src/Client/Components/Styleguide/View.fs` (hier: drei neue Karten/Sektionen; dort: neue
`data-ds-card-name`/`data-ds-card-subtitle`-Attribute an allen bestehenden Karten) und
`scripts/export-design-system.mjs` (hier: drei neue `COMPONENT_CARDS`-Einträge; dort:
Marker-Format/Viewport-Messung/`thumbnail.html`). Kein `depends_on` zwischen den beiden,
der Conductor serialisiert den Dateikonflikt. Reihenfolge-Hinweis: landet `an22r` zuerst,
bekommen die drei hier neuen Karten direkt `data-ds-card-name`/`data-ds-card-subtitle`
statt nur `group`; landet dieser Task zuerst, trägt `an22r` die beiden Attribute
nachträglich auch an diesen drei neuen Karten nach.