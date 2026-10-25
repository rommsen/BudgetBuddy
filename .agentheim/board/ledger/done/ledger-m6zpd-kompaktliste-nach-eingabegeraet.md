---
id: ledger-m6zpd
title: "Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6): mit Maus bekommt die schmale PC-Liste Kästchen, Umschalt-Klick, Prüf-Schaltfläche bei Hover, eine sichtbare Markierung und alle Tasten; mit Finger bleibt sie wie heute"
status: done
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
Voraussetzung ist `ledger-w4vte` (erledigt): Leertaste (`ToggleFocusedSelected`), Esc-Kette
(`EscapePressed`: Popover, Detail, Auswahl), `OpenDetail`/`OpenFocusedDetail` öffnen bei
nicht leerer Auswahl nichts, eine beginnende Auswahl schließt Panel oder Sheet,
`selectionActive` an `entryTableRow`, `mobileEntryRow`, `entryListMobile` und
`entryRowWithReviewMark`, Grundhinweis mit "Leertaste auswählen", "Esc aufheben" in
`barDesktop`. Diese Aufgabe gibt der Kompaktliste mit Maus dieselbe Bedienung wie der
Tabelle und setzt dort auf diesen Regeln auf.
Stand des Codes am 2026-10-25: Die Kompaktliste ist `entryListMobile` mit `mobileEntryRow`
und `entryRowWithReviewMark` in `View.fs`; sie wird unter `md` immer gezeigt und ab `md`,
wenn `visibleColumns` `CompactList` liefert (`alwaysVisible`). `mobileEntryRow` kennt heute
drei Fälle: Normalbetrieb (`swipeableEntryRow`), `SelectionMode` (Kästchen-Container mit
`MultiSelect.box`, Tipp schickt `ToggleRowSelected`) und seit `w4vte` "Auswahl ohne Modus"
(`selectionActive` ohne `SelectionMode`: weder Wisch noch Prüf-Schaltfläche, `LongPressRow`
bleibt an). `KeyboardNavigation` ist eine Komponente in `View.fs`, einmal pro Seite
eingehängt (`KeyboardNavigation model.FocusedRow dispatch`), und hört auf dem Dokument in
beiden Layouts; die Tasten wirken in der Kompaktliste also blind, weil die Zeile keine
Markierung zeigt.

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
   (heute `selected: bool option` plus `selectionActive: bool`) wird ein kleiner Union-Typ
   mit den Fällen Normal (Touch, Wisch), Auswahl-Modus (Touch, wie heute), Auswahl ohne
   Modus (Touch, wie heute aus `w4vte`) und Maus (mit `selected`, `isAnchor`, `isFocused`,
   `selectionActive`). Ob Normal und "Auswahl ohne Modus" ein Fall mit Parameter werden,
   entscheidet der Bau. Für Maus gilt:
   - Links in jeder Zeile steht ein Kästchen, sichtbar ohne Hover, als
     `MultiSelect.boxButton` (Rolle `checkbox`, Klick stoppt die Ausbreitung); Klick schickt
     `ClickRowCheckbox (entry.Id, shift)` mit Umschalt-Klick als Spanne, wie
     `selectBoxCell` in der Tabelle. Es entsteht kein `SelectionMode`.
   - Ein Klick auf die Zeile selbst schickt `OpenDetail entry` (Markierung und Detail als
     Sheet); mit nicht leerer Auswahl nur die Markierung (Regel aus `w4vte`, im Reducer schon
     vorhanden). Die Zeile trägt in diesem Modus nicht `role="checkbox"`.
   - Die Zeile ist heute ein `Html.button` (`entryRow`). Auf einem Knopf bleiben Enter und
     Leertaste dessen eigene Aktivierung (`keyCommandFor`, `isActivatable`); nach einem
     Mausklick auf die Zeile behält sie den DOM-Fokus, und die Leertaste öffnete dann das
     Detail dieser Zeile, statt die markierte Zeile zu wählen. Im Maus-Modus darf die Zeile
     deshalb keinen Knopf-Fokus halten (etwa ein `div` mit Klick wie die `tr` der Tabelle);
     den Weg entscheidet der Bau, der Playwright-Wächter deckt ihn ab.
   - Die Prüf-Schaltfläche erscheint bei Hover und bei markierter Zeile
     (`reviewMarkButton dispatch true isFocused rowState entry`, Signatur
     `dispatch hoverOnly rowFocused rowState entry`; der Container der Zeile bekommt dafür
     die Klasse `group`) und ruht während einer Auswahl wie in der Tabelle: dort steht seit
     `w4vte` ein unsichtbarer 28-px-Platzhalter statt der Schaltfläche (`entryTableRow`); die
     Touch-Zeile lässt sie bei Auswahl ganz weg, die Maus-Zeile nimmt den Platzhalter.
   - Die markierte Zeile ist sichtbar hervorgehoben: dieselbe Fokus-Darstellung wie die
     Tabellenzeile (`Table.focusedRowClass`, `aria-selected="true"` und
     `data-keyboard-focus="true"` am Zeilenelement; `Table.row` setzt das über `IsFocused`,
     die Kompaktzeile ist kein `Table.row` und setzt es selbst), damit der vorhandene Scroll-Effekt in
     `KeyboardNavigation` (`querySelector "[data-keyboard-focus='true']"`) die Zeile in den
     Blick holt. Gewählte Zeilen tragen wie bisher `entryRowSelectedClassName`, der Anker
     zusätzlich die Teal-Kante (`selectableRowClassName`-Regel aus der Tabelle).
   - Alle Tasten aus ADR 0053 wirken wie in der Tabelle; das Detail öffnet als Sheet, und
     solange das Sheet offen ist, sind die Listentasten gesperrt (`keyboardBlocked` in
     `State.fs`, `ledger-q1hw2`); `Esc` schließt es (`EscapePressed`). Am Reducer ist dafür
     nichts zu ändern.
   - Langes Drücken und Wisch bleiben aus: `LongPressRow` bekommt `enabled = false`, die
     Zeile wird nicht in `swipeableEntryRow` gehüllt. Das gilt auch für einen Touchscreen am
     Laptop, der `pointer: fine` meldet.
   - Die Leiste ist die PC-Leiste (`barDesktop`, erscheint bei `hasSelection`); die
     Handy-Leiste und `SelectionMode` kommen nicht vor.
3. **Kompaktliste mit Finger (`CompactTouch`):** unverändert, langes Drücken, Wisch,
   Prüf-Schaltfläche immer sichtbar, `SelectionMode`.
4. **Fußzeilen-Hinweis:** In der Kompaktliste nur mit Maus (`CompactMouse`); bei
   `CompactTouch` ab `md` (Tablet) nicht. Die Tabelle (`TableRows`) behält den Hinweis wie
   heute. `selectionBars` wird im Haupt-`View.fs` außerhalb von `FilterBarColumn` gerendert,
   wo die Tabellenbreite gemessen wird; `keyboardHintVisible` ist eine reine Funktion des
   `Model` (getestet in `LedgerKeyboardTests.fs` und `LedgerDetailTests.fs`) und kennt das
   Layout nicht. `selectionBarSpacers` steht in `FilterBarColumn`, liest aber ebenfalls nur
   `keyboardHintVisible`. Lösung nach dem Vorbild von `DetailFormChanged`: der Effekt in
   `FilterBarColumn` berechnet `listInteractionFor` (Layout wie `filterChipBar`:
   `visibleColumns tableWidth isAllAccounts`, dazu `Viewport.isFinePointer ()` und
   `Viewport.isDesktopViewport ()`) und schickt eine Nachricht (etwa
   `ListInteractionChanged of ListInteraction`), die ein Modellfeld setzt;
   `keyboardHintVisible` und damit `selectionBarSpacers` lesen nur dieses Feld.
   `Viewport.isFinePointer ()` gehört nicht in `keyboardHintVisible`: es ruft
   `window.matchMedia`, und die Funktion wird in den .NET-Tests aufgerufen. Der Typ
   `ListInteraction` liegt deshalb in `Types.fs` (Modellfeld), `listInteractionFor` neben
   `visibleColumns` in `View.fs`. `model.DetailForm` taugt nicht als Ersatz: `DetailSheet`
   gilt auch bei Tabelle, wenn neben dem Panel keine Tabellenstufe mehr passt
   (`detailFormFor`).

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
      `CompactMouse` und bei `TableRows` (ohne Auswahl, mit Zeilen; das Modellfeld aus "What"
      Punkt 4 ist im Test gesetzt, kein Browser nötig). Ein Reducer-Test zeigt, dass
      `ListInteractionChanged` (oder der gewählte Name) das Feld setzt.
- [ ] Playwright-Wächter (Maus, Fenster zwischen 768 und der Breite, ab der die Tabelle
      greift, etwa 900 px; der Test prüft zuerst, dass keine `table` steht): Kästchen
      sichtbar und klickbar, Umschalt-Klick wählt eine Spanne, `↓` hebt eine Zeile sichtbar
      hervor (`data-keyboard-focus`), `G` prüft sie, Leertaste wählt sie, Enter öffnet das
      Detail als Sheet, Hover zeigt die Prüf-Schaltfläche, Fußzeilen-Hinweis ist da, bei
      Auswahl steht die PC-Leiste statt des Hinweises. Dazu ein Schritt nach einem
      Mausklick auf die Zeile: Sheet mit Esc schließen, `↓`, Leertaste wählt die markierte
      Zeile und öffnet kein Detail (Knopf-Fokus aus "What" Punkt 2). Die Datei wird
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
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Entstanden aus der Konzeptrunde am 2026-10-20 (ADR 0053). Ändert die Folge aus ADR 0052
"Die kompakte Liste hat keine Kästchen-Spalte und keine Tabellen-Interaktionen": sie gilt
nur noch für Touch (ADR 0053, Consequences).

Der Sortier-Chip aus `ledger-skpcx` erscheint in der Kompaktliste unabhängig vom
Eingabegerät; daran ändert diese Aufgabe nichts.

Geräte mit beidem (Laptop mit Touchscreen) melden in Chrome in der Regel `pointer: fine`
als primären Zeiger und bekommen damit die Maus-Bedienung. `any-pointer: coarse` wird
bewusst nicht ausgewertet.

Betroffene Stellen (Stand 2026-10-25):
- `src/Client/Components/Ledger/View.fs`: `entryRow` und `entryRowWithReviewMark` (neuer
  Zeilenmodus, Kästchen, `group`, Fokus-Attribute), `mobileEntryRow` (Maus: weder
  `LongPressRow` aktiv noch Wisch), `entryListMobile` und `entryList` (Modus durchreichen),
  `FilterBarColumn` (Layout melden), `keyboardHintVisible`, `selectionBarSpacers`,
  `selectionBars`; neue reine Funktion `listInteractionFor` neben `visibleColumns`.
- `src/Client/Components/Ledger/Types.fs` und `State.fs`: Typ `ListInteraction`, Modellfeld
  und Nachricht (Muster `DetailFormChanged`), Initialwert in `State.init`.
- `src/Client/DesignSystem/Viewport.fs`: nur lesen (`isFinePointer`).
- Tests: `LedgerVisibleColumnsTests.fs` oder eine neue `Ledger...Tests.fs` für die
  Entscheidungsfunktion, `LedgerKeyboardTests.fs` (Hinweis), `scripts/ledger-selection.test.mjs`.

Dieselben Quelldateien wie `ledger-w4vte` (`View.fs`, `State.fs`, `Types.fs`), deshalb
`depends_on`. `w4vte` ist gebaut und hat `selectionActive` an `entryTableRow`,
`mobileEntryRow`, `entryListMobile` und `entryRowWithReviewMark`, die Leertaste
(`ToggleFocusedSelected`) und die Esc-Kette (`EscapePressed`) geliefert; die Namen sind
gegen den Code geprüft (siehe Abgleich unten).

Verfeinert am 2026-10-24 (Sammel-Modeling): vorhandenes `Viewport.isFinePointer` statt
neuer `matchMedia`-Zugriff; Entscheidungsfunktion mit drei Eingaben und Drei-Fall-Ergebnis
festgelegt; Annahme "unter `md` bleibt es Touch-Bedienung" ausgewiesen (PC-Leiste und Hinweis
sind erst ab `md` sichtbar); Zeilenmodus als Union statt `bool option`; Fokus-Darstellung über
`Table.focusedRowClass` und `data-keyboard-focus`; Hinweis-Sichtbarkeit braucht das Layout im
Modell (Muster `DetailFormChanged`); fehlenden "Alle"-Weg als bekannte Lücke vermerkt;
Acceptance criteria um Spanne, Leertaste, Hinweis und Tablet-Touch ergänzt.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

Abgleich mit dem gebauten Code (2026-10-25): Geprüft gegen `View.fs`, `State.fs`,
`Types.fs`, `Viewport.fs`, `Table.fs`, `MultiSelect.fs`, die drei Testdateien,
`scripts/ledger-selection.test.mjs` und `package.json`. Bestätigt: `Viewport.isFinePointer`,
`isDesktopViewport`, `visibleColumns`, `TableLayout` (`CompactList`, `Columns`),
`alwaysVisible`, `entryListMobile`, `mobileEntryRow`, `entryRow`, `entryTableRow`,
`selectBoxCell`, `MultiSelect.boxButton`, `ClickRowCheckbox (id, shift)` (setzt keinen
`SelectionMode`), `Table.focusedRowClass`, `entryRowSelectedClassName`,
`selectableRowClassName`, `LongPressRow (enabled)`, `swipeableEntryRow`, `barDesktop`,
`hasSelection`, `keyboardBlocked`, `keyboardHintVisible`, `selectionBarSpacers`,
`selectionBars`, `FilterBarColumn`, `DetailFormChanged`, `ToggleFocusedSelected`,
`EscapePressed`, `guard:tokens`, `guard:classes`, `test:scripts`. Geändert: `w4vte` als
geliefert ausgewiesen; `entryRowWithReviewMark` hat neben `selected: bool option` jetzt
`selectionActive`, und die Touch-Zeile kennt den Fall "Auswahl ohne Modus", der in den
Zeilenmodus-Union gehört; volle Signatur von `reviewMarkButton` eingesetzt; Platzhalter
(Tabelle) und Weglassen (Touch) unterschieden; Knopf-Fokus der Zeile als Fallstrick mit
Playwright-Schritt ergänzt; Punkt 4 korrigiert: `selectionBarSpacers` liegt in
`FilterBarColumn`, `Viewport.isFinePointer ()` wandert aus `keyboardHintVisible` in den Effekt,
das Modellfeld trägt das ganze Ergebnis (`ListInteraction`), `DetailForm` ist kein Ersatz;
"sobald integriert" beim Token-Wächter gestrichen.

## Outcome

Die Kompaktliste (`entryListMobile`) unterscheidet jetzt drei Bedienungen (`ListInteraction` in `Types.fs`: `TableRows`, `CompactMouse`, `CompactTouch`). `listInteractionFor layout finePointer desktopViewport` (View.fs, neben `visibleColumns`) entscheidet rein; `listInteractionTraits` gibt je Modus Kästchen, Hover-Schaltfläche, Markierung, Gesten und Fußzeilen-Hinweis an. `FilterBarColumn` berechnet das Ergebnis (mit `Viewport.isFinePointer ()` und `Viewport.isDesktopViewport ()`) und meldet es per `ListInteractionChanged` in das Modellfeld `ListInteraction` (Anfangswert `TableRows`); `keyboardHintVisible` und damit `selectionBarSpacers` lesen nur dieses Feld und bleiben rein.

Maus-Zeile (`entryRowWithReviewMark` mit neuem Parameter `mouse: MouseRowInfo option`): links `MultiSelect.boxButton` (Klick schickt `ClickRowCheckbox (id, shift)`, kein `SelectionMode`); die Zeile selbst ist ein `div` mit Klick (`OpenDetail`) statt eines Knopfes, damit sie keinen Knopf-Fokus hält und Leertaste und Enter bei der markierten Zeile ankommen; der Container (`mouseRowContainerClassName`) trägt `group`, die Fokus-Darstellung `Table.focusedRowClass` samt `aria-selected` und `data-keyboard-focus`, blasses Teal für gewählte Zeilen, die Teal-Kante für den Anker (gewinnt gegen die Statuskante); die Prüf-Schaltfläche erscheint bei Hover oder markierter Zeile, bei laufender Auswahl steht ein unsichtbarer 28-px-Platzhalter. `mobileEntryRow` hat einen eigenen Zweig (weder `swipeableEntryRow` noch `LongPressRow`). Abweichung von der Verfeinerung: statt eines Zeilenmodus-Unions bekam `entryRowWithReviewMark` einen `MouseRowInfo option`-Parameter, weil mehrere Quelltext-Tests die bestehenden Zweige (`selected.IsNone && not selectionActive`, `elif selectionActive then`) festnageln. Kein neuer DS-Baustein, `/styleguide` unverändert.

Tests: `src/Tests/LedgerCompactInteractionTests.fs` (9 Expecto-Tests: acht Kombinationen, Merkmale je Modus, Hinweis, `ListInteractionChanged`, `ClickRowCheckbox` ohne Modus, Quelltext-Zusicherungen zu Kästchen, div statt Knopf, Gesten aus, Klassen des Containers). `scripts/ledger-compact-mouse.test.mjs` (5 Playwright-Tests): Maus bei 900 px (zuerst kein `table`, `pointer: fine`): Kästchen sichtbar, Umschalt-Klick wählt Spanne, PC-Leiste statt Hinweis, `↓` setzt `data-keyboard-focus`, Hover zeigt die Schaltfläche (Opazität 0 zu 1), `G` ruft `setReviewMark` und springt weiter, Leertaste wählt die markierte Zeile, Esc hebt auf, Enter öffnet das Sheet; nach einem Mausklick auf die Zeile und Esc wählt die Leertaste die markierte Zeile, öffnet kein Detail und der aktive Knoten ist kein BUTTON. Touch (390 px und 820 px, `hasTouch`, `isMobile`): `pointer: coarse` wird gemeldet (der Test prüft es), kein Kästchen, kein Hinweis, Prüf-Schaltfläche sichtbar, langes Drücken startet den Auswahl-Modus. Angepasst: `scripts/ledger-detail.test.mjs` (die Kompaktzeile ist mit Maus kein Knopf mehr, Suche per Text) und `LedgerSelectionTests.fs` (Quelltext-Fenster 900 auf 1500 Zeichen, weil `mobileEntryRow` länger wurde).

Läufe aus dem Worktree: `dotnet build` 0 Warnungen, 0 Fehler; `dotnet test` 1947 bestanden, 7 übersprungen (Integration ohne .env), 0 Fehler; `npm run build` grün; `npm run guard:classes` grün; `npm run guard:tokens` grün; `npm run test:scripts` 205 Tests: im ersten Lauf 2 Fehler (ledger-detail, angepasst und danach grün; ledger-review-mark Timeout beim Seitenladen), im zweiten Lauf 12 Fehler durch Ladetimeouts unter paralleler Last anderer Worker (ledger-status-column, ledger-table-columns); beide Dateien einzeln wiederholt grün (12 von 12), ledger-detail und ledger-review-mark zusammen 19 von 19.

[human-eye] Screenshots (einmalig, außerhalb der Tests): /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/883da6b7-8bd8-4383-85fb-89b4ad0ac73f/scratchpad/ledger-m6zpd/mouse-selection.png (Maus 900 px, drei gewählte Zeilen, Anker mit Teal-Kante, markierte Zeile, PC-Leiste), mouse-hint.png (Hinweis in der Fußzeile), touch.png (390 px unverändert). Die Liste wirkt wie eine schmale Tabelle (Kästchen links, Kanten, Markierung); die Optik kommt nur aus Tokens und `Table.focusedRowClass`.

Bekannte Lücke wie in der Task: die Kompaktliste mit Maus hat keinen "Alle"-Weg (keine Kopfzeile); ob er nötig ist, entscheidet Roman. Annahme bleibt: unter 768 px gilt Touch-Bedienung auch mit Maus; ein Gerätewechsel im Betrieb wird nicht nachgeführt. Das Raster der Maus-Kompaktzeile bei Wanderung des Tabellen-Schwellwerts (Layout wechselt zwischen Kompaktliste und Tabelle) wurde nicht gesondert gemessen.

### README von Hand
Nicht nötig, README_DELTA reicht (per `npm run check:readme-delta` geprüft). Der Satz "Die kompakte Liste hat keine Kästchen-Spalte und keine Tabellen-Interaktionen" aus ADR 0052 steht nicht in der BC-README.
