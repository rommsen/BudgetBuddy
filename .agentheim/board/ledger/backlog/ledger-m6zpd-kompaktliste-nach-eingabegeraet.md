---
id: ledger-m6zpd
title: "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute"
status: backlog
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-w4vte]
blocks: []
tags: [frontend, kassenbuch, kompaktliste, auswahl, bedienkonzept]
related_adrs: [0043, 0049, 0052, 0053]
related_research: []
prior_art: [ledger-qh9qx, ledger-r1d2f, ledger-rjb8w, ledger-th36h]
---

## Why
Seit ADR 0052 zeigt der PC in schmalen Fenstern die Handy-Liste. Mit ihr kommen die
Handy-Gesten (langes Drücken, Wisch), und die gehen nur per Touch: mit der Maus gibt es dort
keine Auswahl, die Markierung ist unsichtbar, `G` wirkt blind (Bestandsaufnahme
`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`, Reibung 10). ADR 0053 Punkt 6:
die Breite wählt nur das Layout, die Gesten folgen dem Eingabegerät. Stück 3 von drei.

## What
Voraussetzung ist `ledger-w4vte` (Leertaste, Esc-Kette, Sperren während der Auswahl,
`selectionActive`-Parameter an der Zeile, Hinweistexte). Diese Aufgabe gibt der Kompaktliste
mit Maus dieselbe Bedienung wie der Tabelle und setzt dort auf den Regeln aus `w4vte` auf.
Stand des Codes am 2026-10-24: Die Kompaktliste ist `entryListMobile` mit `mobileEntryRow`
und `entryRowWithReviewMark` in `View.fs`; sie wird unter `md` immer gezeigt und ab `md`,
wenn `visibleColumns` `CompactList` liefert (`alwaysVisible`). `KeyboardNavigation` hört
auf dem Dokument schon in beiden Layouts, die Tasten wirken dort also blind, weil die Zeile
keine Markierung zeigt.

1. **Eingabegerät erkennen:** `Viewport.isFinePointer ()` gibt es schon
   (`src/Client/DesignSystem/Viewport.fs`, `matchMedia("(pointer: fine)")`, zur Renderzeit
   gelesen wie `isDesktopViewport()`); es wird wiederverwendet, kein zweiter `matchMedia`-
   Aufruf. Eine reine Funktion in `View.fs` neben `visibleColumns`, etwa
   `listInteractionFor (layout: TableLayout) (finePointer: bool) (desktopViewport: bool)`,
   liefert `TableRows` (Layout `Columns`, ab `md`), `CompactMouse` oder `CompactTouch`.
   `CompactMouse` gilt nur, wenn die Kompaktliste gezeigt wird, `finePointer` und
   `desktopViewport` (ab 768 px) zutreffen. Unter 768 px bleibt es `CompactTouch`, auch mit
   Maus: die PC-Leiste und der Hinweis sind per CSS erst ab `md` da (`hidden md:flex`), die
   Maus-Bedienung bräuchte dort eine zweite Leiste. Das ist eine Annahme dieser
   Verfeinerung, keine Produktentscheidung. Ein Wechsel des Geräts im laufenden Betrieb muss
   nicht live nachgeführt werden.
2. **Kompaktliste mit Maus (`CompactMouse`):** Der Zeilenmodus von `entryRowWithReviewMark`
   (heute `selected: bool option`) wird ein kleiner Union-Typ mit drei Fällen: Normal (Touch,
   Wisch), Auswahl-Modus (Touch, wie heute) und Maus (mit `selected`, `isAnchor`,
   `isFocused`). Für Maus gilt:
   - Links in jeder Zeile steht ein Kästchen, sichtbar ohne Hover, als
     `MultiSelect.boxButton` (Rolle `checkbox`, Klick stoppt die Ausbreitung); Klick schickt
     `ClickRowCheckbox (entry.Id, shift)` mit Umschalt-Klick als Spanne, wie
     `selectBoxCell` in der Tabelle. Es entsteht kein `SelectionMode`.
   - Ein Klick auf die Zeile selbst schickt `OpenDetail entry` (Markierung und Detail als
     Sheet); mit nicht leerer Auswahl nur die Markierung (Regel aus `w4vte`). Die Zeile trägt
     in diesem Modus nicht `role="checkbox"`.
   - Die Prüf-Schaltfläche erscheint bei Hover und bei markierter Zeile
     (`reviewMarkButton dispatch true isFocused ...`, der Container der Zeile bekommt dafür
     die Klasse `group`) und ruht während einer Auswahl wie in der Tabelle (`selectionActive`
     aus `w4vte`, Platzhalter statt Schaltfläche).
   - Die markierte Zeile ist sichtbar hervorgehoben: dieselbe Fokus-Darstellung wie die
     Tabellenzeile (`Table.focusedRowClass`, `aria-selected="true"` und
     `data-keyboard-focus="true"` am Zeilenelement), damit der vorhandene Scroll-Effekt in
     `KeyboardNavigation` (`querySelector "[data-keyboard-focus='true']"`) die Zeile in den
     Blick holt. Gewählte Zeilen tragen wie bisher `entryRowSelectedClassName`, der Anker
     zusätzlich die Teal-Kante (`selectableRowClassName`-Regel aus der Tabelle).
   - Alle Tasten aus ADR 0053 wirken wie in der Tabelle; das Detail öffnet als Sheet, und
     solange das Sheet offen ist, sind die Listentasten gesperrt (`keyboardBlocked`,
     `ledger-q1hw2`); `Esc` schließt es.
   - Langes Drücken und Wisch bleiben aus: `LongPressRow` bekommt `enabled = false`, die
     Zeile wird nicht in `swipeableEntryRow` gehüllt. Das gilt auch für einen Touchscreen am
     Laptop, der `pointer: fine` meldet.
   - Die Leiste ist die PC-Leiste (`barDesktop`, erscheint bei `hasSelection`); die
     Handy-Leiste und `SelectionMode` kommen nicht vor.
3. **Kompaktliste mit Finger (`CompactTouch`):** unverändert, langes Drücken, Wisch,
   Prüf-Schaltfläche immer sichtbar, `SelectionMode`.
4. **Fußzeilen-Hinweis:** In der Kompaktliste nur mit Maus (`CompactMouse`); bei
   `CompactTouch` ab `md` (Tablet) nicht. Die Tabelle (`TableRows`) behält den Hinweis wie
   heute. `selectionBars` und `keyboardHintVisible` stehen außerhalb von `FilterBarColumn`,
   wo die Tabellenbreite gemessen wird, und kennen das Layout nicht. Lösung nach dem Vorbild
   von `DetailFormChanged`: eine Nachricht (etwa `ListLayoutChanged of compact: bool`) setzt
   ein Modellfeld aus demselben Effekt in `FilterBarColumn`; `keyboardHintVisible` (und der
   Abstandshalter in `selectionBarSpacers`) nehmen das Feld und `Viewport.isFinePointer ()`
   hinzu.

Bekannte Lücke, keine neue Entscheidung: Die Kompaktliste mit Maus hat keine Kopfzeile und
damit weder Kopfzeilen-Kästchen noch "Alle"-Weg (die PC-Leiste hat kein "Alle"). Ein
Auswählen per Umschalt-Klick-Spanne bleibt der Weg zu vielen Zeilen. Ob die Maus-Kompaktliste
einen "Alle"-Weg braucht, entscheidet Roman nach dem Bau.

## Acceptance criteria
- [ ] Expecto-Test der reinen Entscheidungsfunktion: Layout (`Columns` oder `CompactList`)
      kreuz `finePointer` kreuz `desktopViewport` ergibt `TableRows`, `CompactMouse` oder
      `CompactTouch` wie in "What" Punkt 1 (alle acht Kombinationen, darunter: `CompactList`
      mit Maus ab `md` ist `CompactMouse`, `CompactList` mit Maus unter `md` ist
      `CompactTouch`). Eine zweite reine Funktion oder Felder des Ergebnisses geben je Modus
      an: Kästchen sichtbar, Hover-Schaltfläche, Markierung sichtbar, Gesten an oder aus,
      Fußzeilen-Hinweis ja oder nein, und der Test prüft sie.
- [ ] Reducer-Test: `ClickRowCheckbox (id, false)` und `(id, true)` (Spanne) auf einer
      Liste ohne `SelectionMode` schalten die Zeile bzw. nehmen die Spanne, und
      `SelectionMode` bleibt `false`. Das deckt `LedgerSelectionTests.fs` für die Tabelle
      schon ab; der neue Fall ist die Zusicherung für die Kompaktliste (Quelltext- oder
      Render-Test, dass `CompactMouse` dieselbe Nachricht schickt).
- [ ] Test des Hinweises: `keyboardHintVisible` ist `false` bei `CompactTouch`, `true` bei
      `CompactMouse` und bei `TableRows` (ohne Auswahl, mit Zeilen).
- [ ] Playwright-Wächter (Maus, Fenster zwischen 768 und der Breite, ab der die Tabelle
      greift, etwa 900 px; der Test prüft zuerst, dass keine `table` steht): Kästchen
      sichtbar und klickbar, Umschalt-Klick wählt eine Spanne, `↓` hebt eine Zeile sichtbar
      hervor (`data-keyboard-focus`), `G` prüft sie, Leertaste wählt sie, Enter öffnet das
      Detail als Sheet, Hover zeigt die Prüf-Schaltfläche, Fußzeilen-Hinweis ist da, bei
      Auswahl steht die PC-Leiste statt des Hinweises. Die Datei wird
      `scripts/ledger-selection.test.mjs` oder ein neuer Test daneben.
- [ ] Playwright-Wächter (Touch-Emulation, `hasTouch` und `isMobile`, einmal 390 px und
      einmal Tablet-Breite etwa 820 px; der Test prüft zuerst `matchMedia('(pointer: coarse)')`):
      langes Drücken startet den Auswahl-Modus wie bisher, vor dem Modus kein Kästchen, kein
      Fußzeilen-Hinweis, Prüf-Schaltfläche sichtbar. Meldet die Emulation `pointer: fine`,
      steht das in der Ergebnis-Notiz und der Fall bleibt durch den Expecto-Test gedeckt.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Die Kompaktliste mit Maus wirkt wie eine schmale Tabelle und nicht wie ein Handy auf
      dem Schreibtisch. [human-eye]

## Notes
Entstanden aus der Konzeptrunde am 2026-10-20 (ADR 0053). Ändert die Folge aus ADR 0052
"Die kompakte Liste hat keine Kästchen-Spalte und keine Tabellen-Interaktionen": sie gilt
nur noch für Touch (ADR 0053, Consequences).

Der Sortier-Chip aus `ledger-skpcx` erscheint in der Kompaktliste unabhängig vom
Eingabegerät; daran ändert diese Aufgabe nichts.

Geräte mit beidem (Laptop mit Touchscreen) melden in Chrome in der Regel `pointer: fine`
als primären Zeiger und bekommen damit die Maus-Bedienung. `any-pointer: coarse` wird
bewusst nicht ausgewertet.

Betroffene Stellen (Stand 2026-10-24):
- `src/Client/Components/Ledger/View.fs`: `entryRow` und `entryRowWithReviewMark` (neuer
  Zeilenmodus, Kästchen, `group`, Fokus-Attribute), `mobileEntryRow` (Maus: weder
  `LongPressRow` aktiv noch Wisch), `entryListMobile` und `entryList` (Modus durchreichen),
  `FilterBarColumn` (Layout melden), `keyboardHintVisible`, `selectionBarSpacers`,
  `selectionBars`; neue reine Funktion neben `visibleColumns`.
- `src/Client/Components/Ledger/Types.fs` und `State.fs`: Modellfeld und Nachricht für das
  Layout (Muster `DetailFormChanged`), Initialwert in `State.init`.
- `src/Client/DesignSystem/Viewport.fs`: nur lesen (`isFinePointer`).
- Tests: `LedgerVisibleColumnsTests.fs` oder eine neue `Ledger...Tests.fs` für die
  Entscheidungsfunktion, `LedgerKeyboardTests.fs` (Hinweis), `scripts/ledger-selection.test.mjs`.

Dieselben Quelldateien wie `ledger-w4vte` (`View.fs`, `State.fs`, `Types.fs`), deshalb
`depends_on`. Die Verfeinerung geht davon aus, dass `w4vte` den Parameter `selectionActive`
an `entryTableRow` und `mobileEntryRow`, die Leertaste (`ToggleFocusedSelected`) und die
Esc-Kette schon geliefert hat; Namen nach dem Bau von `w4vte` gegenprüfen.

Verfeinert am 2026-10-24 (Sammel-Modeling): vorhandenes `Viewport.isFinePointer` statt
neuer `matchMedia`-Zugriff; Entscheidungsfunktion mit drei Eingaben und Drei-Fall-Ergebnis
festgelegt; Annahme "unter `md` bleibt es Touch-Bedienung" ausgewiesen (PC-Leiste und Hinweis
sind erst ab `md` sichtbar); Zeilenmodus als Union statt `bool option`; Fokus-Darstellung über
`Table.focusedRowClass` und `data-keyboard-focus`; Hinweis-Sichtbarkeit braucht das Layout im
Modell (Muster `DetailFormChanged`); fehlenden "Alle"-Weg als bekannte Lücke vermerkt;
Acceptance criteria um Spanne, Leertaste, Hinweis und Tablet-Touch ergänzt.
