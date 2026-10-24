---
id: ledger-w4vte
title: "Auswahl nach ADR 0053: Leertaste wählt die markierte Zeile, eine beginnende Auswahl schließt das Detail, Esc schließt Popover, dann Detail, dann Auswahl; während einer Auswahl ruhen Prüf-Schaltfläche, G und Wisch auf PC und Handy"
status: done
type: feature
context: ledger
created: 2026-10-20
completed:
depends_on: [design-system-001, ledger-q1hw2]
blocks: [ledger-m6zpd]
tags: [frontend, kassenbuch, auswahl, tastatur, bedienkonzept]
related_adrs: [0049, 0050, 0053]
related_research: []
prior_art: [ledger-r1d2f, ledger-rjb8w, ledger-nhwrt, ledger-th36h, ledger-mhe2s]
---

## Why
ADR 0053 trennt die Bedienung in "eine Buchung, das Detail" und "viele Buchungen, die
Leiste". Heute laufen Auswahl, Fokuszeile und Prüfwege mit verschiedenen Regeln
nebeneinander: am PC bleibt die Prüf-Schaltfläche bei laufender Auswahl aktiv, `G` nicht;
die Leertaste aus allen Handoffs ist nicht belegt; Esc schließt nur das Detail, nicht
Popover-Fokus und Auswahl; am Handy reagiert das Kästchen im Auswahl-Modus nicht auf einen
Tipp, weil nur die Zeile daneben einen Handler hat (Bestandsaufnahme
`docs/ux/kassenbuch/bedienkonzept-zeile-bestandsaufnahme.md`, Reibungen 6, 7, 9, 11, 13, 14).
Stück 2 von drei (nach `ledger-q1hw2`, vor `ledger-m6zpd`).

## What
Stand des Codes (nach `ledger-q1hw2`, geprüft am 2026-10-24): `OpenDetail`, `OpenFocusedDetail`
(Enter), `EscapePressed`, `CloseDetail`, `keyboardBlocked` und der Panel-Hinweis
(`keyboardHintPanelText`) gibt es schon. Ein Klick auf eine PC-Zeile ruft `OpenDetail entry`
und setzt über `showDetail` auch `FocusedRow`. Diese Aufgabe baut darauf auf und ändert nur
die Auswahl-Regeln.

Entscheidungen Roman, 2026-10-20 (ADR 0053 Punkte 2, 4, 5, 7):

1. **Leertaste** nimmt die markierte Zeile (`FocusedRow`) in die Auswahl oder heraus, wie
   ein Klick auf ihr Kästchen (Anker setzen wie `ClickRowCheckbox (id, false)`). Neue
   Nachricht `ToggleFocusedSelected`, in `keyCommandFor` auf `" "` gelegt. Ohne markierte
   Zeile tut sie nichts. Sie wirkt nicht in Eingabefeldern und nicht mit Strg, Meta oder Alt
   (das deckt der Kopf von `keyCommandFor` schon ab) und nicht, solange `keyboardBlocked`
   gilt (offenes Popover oder Sheet). Bei offenem Seitenpanel wirkt sie; dann greift Punkt 2.
2. **Eine beginnende Auswahl schließt das Detail.** Sobald die Auswahl von leer auf nicht
   leer wechselt (`ClickRowCheckbox`, Umschalt-Klick, Leertaste, `ToggleHeaderCheckbox`,
   `ToggleSelectAll`, `StartSelectionMode`), wird ein offenes Panel oder Sheet geschlossen
   (`DetailFor = None`, `History = NotAsked`). Umsetzung als eine kleine Hilfsfunktion in
   `State.fs`, die Zustand vor und nach dem Schritt vergleicht (`hasSelection`, nicht nur
   `Selection <> NoSelection`, damit `AllExcept` mit allen Ausnahmen als leer zählt), statt
   sechs Zweige einzeln anzufassen. Solange die Auswahl nicht leer ist, öffnet ein Klick auf
   eine PC-Zeile kein Detail: `OpenDetail` setzt dann nur `FocusedRow` auf die Zeile und
   ändert sonst nichts. Dasselbe gilt für `OpenFocusedDetail` (Enter): mit nicht leerer
   Auswahl öffnet es nichts (heute sperrt nur `SelectionMode`). Am Handy wählt ein Tipp im
   Auswahl-Modus aus wie heute (`OpenDetail` tut dort nichts).
3. **Esc-Kette**, ein `Esc` schließt genau eine Stufe, im Reducer `EscapePressed`:
   1. offenes Chip-Popover (`OpenPopover.IsSome`): wie `CloseFilterPopover` schließen
      (`OpenPopover = None`). Heute tut `EscapePressed` hier nichts und verlässt sich darauf,
      dass das Popover Esc selbst behandelt. Das stimmt nur, wenn der Fokus im Popover liegt
      (`FilterPopover.fs`, `onKeyDown` mit `stopPropagation`); liegt er auf dem Chip, passiert
      nichts. Liegt er im Popover, erreicht das Ereignis das Dokument nicht, es gibt also
      keine doppelte Wirkung.
   2. offenes Detail (`DetailFor.IsSome`): schließen wie heute.
   3. Auswahl (`SelectionMode` oder `hasSelection`): wie `ClearSelection`.
   Ohne offene Stufe ändert `Esc` nichts.
4. **Sperren während einer Auswahl, auf PC und Handy gleich:** die Prüf-Schaltfläche an der
   Zeile ist nicht sichtbar und nicht bedienbar (am PC heute aktiv), `G` und `Umschalt+G`
   wirken nicht (`reviewKeyBlocked` prüft `SelectionMode || hasSelection` schon), der Wisch
   nach rechts ist aus (am Handy im Auswahl-Modus schon, weil `mobileEntryRow` dann
   `entryRowWithReviewMark` statt `swipeableEntryRow` nimmt). Der einzige Prüfweg ist
   "Geprüft" in der Leiste. Am PC: `entryTableRow` bekommt einen Parameter
   `selectionActive: bool` (aus `hasSelection model`, durchgereicht über `entryList` und
   `entryListDesktop`); die Schaltfläche wird dann durch einen unsichtbaren Platzhalter
   gleicher Größe ersetzt (`h-7 w-7 shrink-0`, `aria-hidden`), damit die Kennzeichen in der
   Statusspalte nicht springen. Die Handy-Zeile (`mobileEntryRow`) gated Wisch und
   Schaltfläche zusätzlich an `hasSelection`, nicht nur an `SelectionMode`; das trifft den
   Fall, dass das Fenster mit bestehender Auswahl von Tabelle auf Kompaktliste schrumpft.
5. **Handy-Kästchen:** In `entryRowWithReviewMark` trägt der Kästchen-Container (heute ein
   `div` ohne Handler um `MultiSelect.box`) einen Klick-Handler, der `ToggleRowSelected
   entry.Id` schickt, genau wie die Zeile daneben. Der Container ist mindestens 44 px breit
   und so hoch wie die Zeile. `MultiSelect.box` bleibt `aria-hidden`, die Zeile selbst trägt
   weiter `role="checkbox"`.
6. **Kein doppeltes Auslösen:** Nach einem Mausklick auf Kästchen oder Prüf-Schaltfläche
   liegt der Browserfokus auf dem Knopf. Festgelegte Wahl: `keyCommandFor` gibt für `" "` mit
   `isActivatable tagName` (BUTTON, A, SUMMARY) `None` zurück, genau wie schon für Enter; die
   native Knopf-Wirkung gewinnt, die Listentaste feuert dort nicht. Damit tritt genau eine
   Wirkung ein (ein Kippen). Esc bleibt auf Knöpfen belegt (bestehender Test). Zeigt der
   Playwright-Wächter, dass Chrome die Leertaste auf dem Knopf anders behandelt, ist der
   Rückfall, dem Kästchen-Knopf nach dem Klick den Fokus zu nehmen (`blur`); das Verhalten
   "genau ein Kippen" bleibt das Kriterium.
7. **Fußzeilen-Hinweis** nennt je Zustand alle belegten Tasten:
   - ohne Auswahl, ohne Panel (`keyboardHintText`): "↑ ↓ J K bewegen · Enter Detail ·
     Leertaste auswählen · G geprüft · Umschalt+G ungeprüft"
   - Panel offen (`keyboardHintPanelText`, aus `ledger-q1hw2`, bleibt): "↑ ↓ bewegen ·
     G geprüft · Esc Panel schließen"
   - mit Auswahl ersetzt die PC-Leiste (`MultiSelect.barDesktop`) den Hinweis wie heute
     (`keyboardHintVisible` ist dann `false`); die Leiste nennt zusätzlich "Esc aufheben" als
     gedämpften Mono-Text neben dem Knopf "Auswahl aufheben" (neue Konstante in `Types.fs`
     neben `selectionClearLabel`, neuer Parameter an `barDesktop`). Die Handy-Leiste
     (`barMobile`) bekommt ihn nicht, dort gibt es keine Tastatur.
   Genaue Schreibweise beim Bau nach dem Styleguide, die Belegungen sind fest.

## Acceptance criteria
- [ ] Reducer-Test (`LedgerKeyboardTests.fs`): `ToggleFocusedSelected` auf der markierten
      Zeile nimmt sie in die Auswahl und setzt den Anker, ein zweites Mal nimmt es sie
      heraus; ohne `FocusedRow` ändert es nichts; bei offenem Popover oder Sheet ändert es
      nichts.
- [ ] Tastentest (`keyCommandFor`): `" "` ergibt `Some ToggleFocusedSelected`; `None` in
      INPUT, TEXTAREA, SELECT, contenteditable, mit Strg/Meta oder Alt und auf BUTTON, A,
      SUMMARY. Der bestehende Test "Leertaste bleibt frei" in `LedgerKeyboardTests.fs`
      (Zeile 205 ff.) wird entsprechend umgeschrieben.
- [ ] Reducer-Test: der Wechsel der Auswahl von leer auf nicht leer schließt ein offenes
      Detail (Panel und Sheet), für jeden Weg: `ClickRowCheckbox` mit und ohne Umschalt,
      `ToggleFocusedSelected`, `ToggleHeaderCheckbox`, `ToggleSelectAll`,
      `StartSelectionMode`. Ein Schritt, der die Auswahl nicht von leer auf nicht leer
      ändert (etwa Abwählen, `ClearSelection`), lässt ein Detail unberührt.
- [ ] Reducer-Test: mit nicht leerer Auswahl setzt `OpenDetail` die Markierung und öffnet
      kein Detail und stößt keinen Historie-Abruf an; `OpenFocusedDetail` öffnet ebenfalls
      nichts. Der bestehende Test "im Auswahl-Modus am Handy öffnet ein Tipp kein Detail"
      (`LedgerDetailTests.fs`, erwartet dort `FocusedRow = None`) bleibt grün.
- [ ] Reducer-Test der Esc-Kette: Popover offen, Detail offen, Auswahl nicht leer: das
      erste `Esc` schließt nur das Popover, das zweite nur das Detail, das dritte hebt die
      Auswahl auf (`Selection = NoSelection`, `SelectionMode = false`); ein viertes ändert
      nichts. Der bestehende Test "Esc bei offenem Popover lässt das Detail stehen" bleibt
      grün und prüft zusätzlich `OpenPopover = None`.
- [ ] Ansichts-Test (Quelltext oder gerendert, wie `LedgerReviewButtonVisibilityTests`):
      bei nicht leerer Auswahl rendert die PC-Zeile keine Prüf-Schaltfläche (Platzhalter
      statt `button`); ohne Auswahl wie bisher. `G` und Wisch bleiben gesperrt (bestehende
      Tests grün); `mobileEntryRow` nimmt bei `hasSelection` ohne `SelectionMode` keine
      `SwipeableRowRight` und keine Prüf-Schaltfläche.
- [ ] Ansichts- oder Playwright-Test (390 px, Touch): ein Tipp auf das Kästchen im
      Auswahl-Modus wählt die Zeile aus und wieder ab; die Trefferfläche ist mindestens
      44 px breit (Playwright: `boundingBox` des Containers).
- [ ] Playwright-Wächter (PC 1440 px, ergänzt `scripts/ledger-selection.test.mjs`):
      Mausklick auf ein Kästchen, danach Leertaste: die Auswahl ändert sich genau einmal
      (Anzahl in der Leiste vorher und nachher). Dazu Leertaste auf der markierten Zeile
      ohne vorherigen Klick: Zeile ist gewählt, ein offenes Panel ist zu.
- [ ] Test des Hinweistexts je Zustand: ohne Auswahl enthält `keyboardHintText` "J K",
      "Enter", "Leertaste" und "G geprüft"; Panel offen unverändert
      (`keyboardHintTextFor`); mit Auswahl `keyboardHintVisible = false` und die Leiste
      trägt "Esc aufheben" (Test in `LedgerSelectionTests.fs`). Die bestehenden Tests, die
      `barDesktop` aufrufen (`LedgerSelectionTests.fs`, `LedgerReviewMarkBatchClientTests.fs`),
      sind an die neue Signatur angepasst.
- [ ] `dotnet build`, `dotnet test`, `npm run build`, `npm run guard:classes`,
      `npm run test:scripts` grün.
- [ ] Auswählen per Tastatur fühlt sich am PC flüssig an, ohne dass die Maus nötig ist.
      [human-eye]
- [ ] Optik nur aus Design-System und Tokens (Styleguide §3, Vorrangregel in den Handoffs):
      keine Farb- oder Maßwerte aus den Design-Tafeln; ein neuer DS-Baustein erscheint in
      `/styleguide` (`src/Client/Components/Styleguide/View.fs`); `npm run guard:tokens` grün
      (Wächter aus `design-system-wfhxj`, sobald integriert).
- [ ] Das Neue fügt sich optisch in die gebauten Screens (Kassenbuch, Buchungs-Detail,
      `/styleguide`) ein und fällt daneben nicht auf. [human-eye]

## Notes
Entstanden aus der Konzeptrunde am 2026-10-20 (ADR 0053). Roman will große Umbauten statt
Zwischenschritte; diese Aufgabe räumt die Auswahl-Regeln in einem Zug auf.

Am PC gibt es weiter keinen eigenen Auswahl-Modus; "Auswahl läuft" heißt dort: die Auswahl
ist nicht leer (`hasSelection`). Am Handy bleibt `SelectionMode` aus `ledger-r1d2f`.

Ins Detail kommt man am Handy aus dem Auswahl-Modus nur über "Auswahl aufheben"
(ADR 0053 Punkt 7, Handoff Ablauf 3, offener Punkt 3).

Betroffene Stellen (Stand 2026-10-24):
- `src/Client/Components/Ledger/Types.fs`: neue Nachricht `ToggleFocusedSelected`;
  `keyCommandFor` (Leertaste, Space-auf-Knopf-Regel); Kommentar an `EscapePressed`
  ("volle Kette kommt mit ledger-w4vte") nachziehen; Konstante für "Esc aufheben".
- `src/Client/Components/Ledger/State.fs`: `update` für `OpenDetail`, `OpenFocusedDetail`,
  `EscapePressed`, `ClickRowCheckbox`, `ToggleHeaderCheckbox`, `ToggleSelectAll`,
  `StartSelectionMode`, neuer Zweig `ToggleFocusedSelected`; `keyboardBlocked` und
  `reviewKeyBlocked` bleiben; `resetEntries` setzt die Auswahl schon zurück, schließt aber
  kein Detail (ein Kontowechsel tut das separat in `SelectFilter`), daran ändert sich nichts.
- `src/Client/Components/Ledger/View.fs`: `entryTableRow` und `entryListDesktop` (Parameter
  `selectionActive`), `reviewMarkButton` (bleibt), `entryRowWithReviewMark` (Kästchen-
  Container), `mobileEntryRow`, `keyboardHintText`, `selectionBars`.
- `src/Client/DesignSystem/MultiSelect.fs`: `barDesktop` (Hinweis "Esc aufheben").
- `src/Client/DesignSystem/FilterPopover.fs`: nur lesen; das `stopPropagation` bei Esc im
  Popover darf bleiben.
- Tests: `LedgerKeyboardTests.fs`, `LedgerDetailTests.fs`, `LedgerSelectionTests.fs`,
  `LedgerReviewMarkBatchClientTests.fs`, `scripts/ledger-selection.test.mjs`.

`ledger-m6zpd` baut auf demselben Stand auf und fasst dieselben Dateien an (`View.fs`,
`State.fs`, `Types.fs`); es startet erst nach dieser Aufgabe.

Verfeinert am 2026-10-24 (Sammel-Modeling): Funktionsnamen gegen den Code nach `ledger-q1hw2`
geprüft; Esc-Stufe Detail und Klick-öffnet-Detail als schon vorhanden vermerkt; Popover-Stufe
der Esc-Kette konkretisiert (Reducer schließt, Fokus-Lücke genannt); Leertaste auf Knöpfen
festgelegt (native Wirkung gewinnt); Enter bei Auswahl ergänzt; Platzhalter statt fehlender
Prüf-Schaltfläche; Kästchen-Handler am Handy benannt; Hinweistexte und betroffene Dateien
ausgeschrieben; Acceptance criteria testbar gemacht.

Stil-Absicherung (2026-10-24): Tafeln und Handoffs legen nur den Ablauf fest; die Optik
kommt aus `standards/frontend/styleguide.md`, `src/Client/DesignSystem/` und `/styleguide`
(Vorrangregel in `docs/ux/kassenbuch/tafeln/README.md`).

## Verifier note (iteration 1)

**REASONS:**
- Check 1, Kriterium 5 (Esc-Kette) nur teilweise erfüllt: das Kriterium verlangt, dass der bestehende Test "Esc bei offenem Popover lässt das Detail stehen" grün bleibt und zusätzlich `OpenPopover = None` prüft. `src/Tests/LedgerDetailTests.fs` ist unverändert; der Test (Zeilen 180 bis 184) prüft nur `Expect.isSome model.DetailFor "das Popover schließt sich selbst, das Detail bleibt"`, und die Meldung nennt die alte Begründung, die der neue `EscapePressed`-Zweig widerlegt (der Reducer schließt das Popover jetzt selbst).
- Klein, nicht blockierend: Kriterium 9 nennt `LedgerSelectionTests.fs` als Ort des "Esc aufheben"-Tests; er liegt in `LedgerKeyboardTests.fs`. Verhalten belegt.
- Alles andere bestanden: `dotnet build` 0 Warnungen, `dotnet test` 1721/0/7, `npm run build`, `guard:classes`, `guard:tokens`, `test:scripts` 177/177; Diff im Rahmen; konsistent mit ADR 0053; README_DELTA passt, Prosa-Absätze nicht widerlegt; Schreibregel-grep ohne Treffer.

**SUGGESTED_FIX:** Im Test "Esc bei offenem Popover lässt das Detail stehen" `Expect.isNone model.OpenPopover "..."` ergänzen und die Meldung an das neue Verhalten anpassen; `dotnet test` erneut. Optional den "Esc aufheben"-Quelltexttest nach `LedgerSelectionTests.fs` verschieben.

**ITERATION_HINT:** likely-fixable

## Outcome

Umgesetzt nach ADR 0053 (Punkte 2, 4, 5, 7), ohne Abweichung von einer ADR.

- `src/Client/Components/Ledger/Types.fs`: neue Nachricht `ToggleFocusedSelected`, `keyCommandFor` belegt `" "` (None in Feldern, mit Strg/Meta/Alt und auf BUTTON, A, SUMMARY, damit nach einem Mausklick auf ein Kästchen genau ein Kippen eintritt), Konstante `selectionClearKeyHint` ("Esc aufheben").
- `src/Client/Components/Ledger/State.fs`: der alte `update` heißt `updateCore`; der neue dünne `update` vergleicht Zustand vor und nach dem Schritt (`hasSelection`) und schließt bei leer -> nicht leer ein offenes Panel oder Sheet. `ToggleFocusedSelected` (wie `ClickRowCheckbox (id, false)`, gesperrt bei `keyboardBlocked`), `OpenDetail` und `OpenFocusedDetail` öffnen bei nicht leerer Auswahl nichts (`OpenDetail` markiert nur), `EscapePressed` mit Kette Popover, Detail, Auswahl.
- `src/Client/Components/Ledger/View.fs`: `entryTableRow`/`entryListDesktop` mit `selectionActive` (unsichtbarer 28-px-Platzhalter statt Prüf-Schaltfläche), `mobileEntryRow`/`entryListMobile` mit `selectionActive` (Auswahl ohne Modus: weder Wisch noch Schaltfläche), `entryRowWithReviewMark` mit Klick-Handler am Kästchen-Container (`min-w-11`), neuer Grundhinweis "↑ ↓ J K bewegen · Enter Detail · Leertaste auswählen · G geprüft · Umschalt+G ungeprüft", `selectionBars` reicht den Tastenhinweis an die PC-Leiste.
- `src/Client/DesignSystem/MultiSelect.fs`: `barDesktop` bekommt `clearKeyHint` (gedämpfter Mono-Text neben "Auswahl aufheben"); `barMobile` unverändert. Nur Tokens benutzt, kein neuer Baustein, daher kein Styleguide-Eintrag nötig.
- Tests: `src/Tests/LedgerKeyboardTests.fs` (16 neue Fälle: Tastentest, Reducer für Leertaste, Schließen des Details über alle sechs Wege für Panel und Sheet, Gegenprobe ohne Wechsel, OpenDetail/Enter bei Auswahl, Esc-Kette, Hinweistexte, Quelltext für Platzhalter, Handy-Zeile, Kästchen-Container, Leiste), der alte Test "Leertaste bleibt frei" ist umgeschrieben. `scripts/ledger-selection.test.mjs`: Handy-Test um Tipp auf das Kästchen und 44-px-Prüfung ergänzt, neuer PC-Test (Maus plus Leertaste kippt genau einmal, Leertaste ohne Klick schließt das Panel, Platzhalter 28x28, G und Zeilenklick bei Auswahl ohne Wirkung, "Esc aufheben" in der Leiste, Esc hebt auf).
- Läufe aus dem Worktree: `dotnet build` 0 Warnungen/0 Fehler; `dotnet test` 1721 bestanden, 0 Fehler, 7 übersprungen; `npm run build` grün; `npm run guard:classes` grün; `npm run guard:tokens` grün; `npm run test:scripts` seriell 177/177 bestanden (darunter `ledger-selection.test.mjs`, 3 Tests).
- Keine Vorab-Rotprobe durch Zurückdrehen nötig: die neuen Tests referenzieren `ToggleFocusedSelected` und `selectionClearKeyHint`, die ohne die Änderung nicht kompilieren; die Playwright-Zusicherungen (Platzhalter, Kästchen-Tipp, Leertaste) greifen nur mit dem neuen Code.
- Iteration 2: `LedgerDetailTests.fs` "Esc bei offenem Popover lässt das Detail stehen" prüft jetzt zusätzlich `OpenPopover = None` mit neuer Meldung; der Quelltext-Test "Esc aufheben" der PC-Leiste liegt jetzt in `LedgerSelectionTests.fs` (Kriterium 9). Kein Client-Code geändert, `test:scripts` daher nicht neu gelaufen (Iteration 1: 177/177). `dotnet build` 0 Warnungen, `dotnet test` 1721 bestanden.
- Der Rückfall `blur` (Task-Punkt 6) war nicht nötig: die Leertaste auf dem fokussierten Kästchen-Knopf kippt in Chrome genau einmal.
- Offen für Roman (human-eye): Auswählen per Tastatur flüssig am PC; optische Einfügung des "Esc aufheben"-Hinweises neben "Auswahl aufheben" in der PC-Leiste.

### README von Hand

Kein Prosa-Absatz mit fettem Kopf betroffen. Das Delta oben (replace am Bullet "Fokuszeile") ist mit `npm run check:readme-delta` grün geprüft.
