---
id: ledger-ng110
title: "Split-Editor nach Styleguide: offener Rest gelb, zu viel verteilt rot, Zeilenfelder mit Fehlerzustand, Entfernen als removeIcon mit Sperre, Speicherfehler im offenen Editor statt Toast"
status: done
type: bug
context: ledger
created: 2026-11-12
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0049, 0058, 0087]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-12, Gruppe G5, Teil 1 von 3 (Bericht `.agentheim/knowledge/reviews/2026-11-12-ledger.md`).
Der Split-Editor meldet einen offenen Rest in Rot, obwohl Styleguide §2 Rot für „Fehler und Minus“ reserviert und Gelb
„braucht dich, kein Fehler: … fehlt … Immer mit Wort daneben“ heißt. Ein unlesbarer Zeilenbetrag fällt nicht auf, und
ein abgelehntes Speichern steht nur im Toast, während der Editor offen bleibt.

## What
- Rest > 0 (offen) in Gelb (`Colors.attention`), Rest < 0 (zu viel verteilt) bleibt Rot (`text-error`), Rest 0 neutral
  (`DesignSystem/SplitEditor.fs:19`, `Ledger/View.fs:2028`). Entscheidung Roman 2026-11-12.
- Betrag und Memo einer Zeile über `Input.text`; Betrag mit `Input.amountFieldClass false invalid`, `InputMode "decimal"`,
  `AriaInvalid`. Eine Zeile mit nicht leerem, unlesbarem Betrag gilt als ungültig (`DesignSystem/SplitEditor.fs:124`,
  `:136`; `Ledger/SplitEditor.fs:61`).
- „Zeile entfernen“ als `Button.removeIcon` mit gesperrtem Zustand (neue Variante, etwa `removeIconWhen disabled`), Name
  über `aria-label` (`DesignSystem/SplitEditor.fs:145`).
- Ein fehlgeschlagenes Speichern („Aufteilung nicht gespeichert“, „Betrag nicht geändert“) steht im offenen Editor als
  `ErrorDisplay.inline'` über dem Fuß; der Toast kommt nur, wenn der Editor zur Antwort schon zu ist
  (`Ledger/State.fs:3295`, `:3301`).

Betroffene Dateien: `src/Client/DesignSystem/SplitEditor.fs`, `src/Client/DesignSystem/Button.fs`,
`src/Client/Components/Ledger/SplitEditor.fs`, `src/Client/Components/Ledger/View.fs` (`splitSummaryProps`,
`splitEditorBody`, Split-Fuß, etwa 2015 bis 2350), `src/Client/Components/Ledger/State.fs` (`SplitsSaved`,
`SplitsWithAmountSaved` mit `Error`), `src/Client/Components/Styleguide/View.fs` (Beispiel ab 1794),
`src/Tests/LedgerSplitEditorClientTests.fs`, `scripts/layout-states.mjs`, `standards/frontend/styleguide.md` (§5 Zeile
„Button“ für die neue Variante).

## Acceptance criteria
- [ ] Rest > 0 zeigt den Betrag in `Colors.attention`, Rest < 0 in `text-error`, Rest 0 neutral. Beleg: Test
  „Rest-Farbe …“ in `src/Tests/LedgerSplitEditorClientTests.fs:658` umgeschrieben, je Fall eine Erwartung; der Fall
  Rest > 0 ist vor der Änderung rot.
- [ ] Reine Funktion `lineAmountInvalid` (Name frei) in `Components/Ledger/SplitEditor.fs`: leer ist gültig, „12,50“ und
  „12.50“ gültig, „abc“ und „1,2,3“ ungültig. Beleg: Expecto in `LedgerSplitEditorClientTests.fs`. Die View übergibt das
  Ergebnis an `LineProps`; kein Klassenstring-Test.
- [ ] Ein ungültiger Zeilenbetrag trägt `aria-invalid="true"` und rote Ziffern über `amountFieldClass` (`input-invalid`).
  Beleg: neuer Zustand `kassenbuch-aufteilen-ungueltig` in `scripts/layout-states.mjs` (Handy-Breiten), der „abc“
  eintippt und `aria-invalid` am Feld prüft (Muster `kassenbuch-betrag-feldfehler`); die Invarianten 2 und 3 laufen auf
  den neuen Feldern.
- [ ] „Zeile entfernen“ hat am Handy 48 × 48 px, ist bei einer einzigen Zeile gesperrt und heißt per `aria-label`
  „Zeile entfernen“. Beleg: Invariante 2 im Zustand `kassenbuch-aufteilen` mit zwei Zeilen (hat der Mock eine Zeile,
  öffnet der Zustand zusätzlich „Zeile hinzufügen“); der bestehende `[flow:aufteilen]` in
  `scripts/ledger-split-editor.test.mjs` findet den Knopf per `getByRole("button", { name: "Zeile entfernen" })`.
- [ ] Speichern abgelehnt bei offenem Editor: `SplitsSaved (_, _, Error m)` und `SplitsWithAmountSaved (_, _, _, Error m)`
  setzen einen Speicherfehler im Editor-Zustand und schicken keinen Toast. Ist der Editor zur Antwort zu, kommt der
  Toast wie heute. Die nächste Eingabe oder das nächste Speichern nimmt den Fehler zurück. Beleg: Aufzählung aller
  Reihenfolgen durch das echte `update` nach Doktrin Punkt 20 (Vorbild `moveWays` in `src/Tests/MoveMoneyClientTests.fs`)
  über Speichern, Schließen, Eingabe und Antwort (Erfolg, Fehler); Orakel: „Editor offen bei der Antwort ⇒ Fehler im
  Editor, kein Toast; sonst Toast“. Vor dem Fix rot (heute Toast in jedem Fall).
- [ ] Der Speicherfehler steht als `ErrorDisplay.inline'` direkt über dem Fuß (Panel und Sheet) und ist am Handy ohne
  Scrollen sichtbar. Beleg: neuer Zustand `kassenbuch-aufteilen-speicherfehler` (Handy, Mock antwortet mit Fehler),
  Invarianten 5 und 7.
- [ ] `/styleguide` zeigt den Editor mit offenem Rest in Gelb und einer ungültigen Zeile; das Beispiel kompiliert mit
  den neuen `LineProps`. `[human-eye]`
- [ ] Split-Editor neben dem heutigen Stand verglichen (Screenshot vorher und nachher, `npm run screenshot:route`),
  Abweichungen nur die genannten. `[human-eye]`
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Urteile zu den Befunden dieses Teils:
- `DesignSystem/SplitEditor.fs:19` Rest rot: gilt. `amountClass true` setzt `text-error`, `splitSummaryProps` setzt
  `RestWarn = rest <> 0m`; der Test `LedgerSplitEditorClientTests.fs:668` schreibt `text-error` fest. `RestWarn: bool`
  reicht für drei Zustände nicht mehr, also eine kleine DU (offen, zu viel, ausgeglichen).
- `DesignSystem/SplitEditor.fs:124` rohe Inputs: gilt. Folge: ein unlesbarer Zeilenbetrag zählt still als 0
  (`magnitudeOf`, `Ledger/SplitEditor.fs:61`), niemand sagt, welche Zeile schuld ist.
- `DesignSystem/SplitEditor.fs:145` „Zeile entfernen“: gilt teilweise. Ein Name kommt schon über `title`, aber
  `ButtonProps.AriaLabel` ist bei reinem Symbolknopf Pflicht, und `Button.removeIcon` (`Button.fs:478`) kennt keine
  Sperre; die Zeile braucht sie (`CanRemove`, `Disabled`).
- `Ledger/State.fs:3301` und `:3295` Toast bei offenem Editor: gilt (§7.13 „Blockierende Fehler gehören in
  `ErrorDisplay`“, `.claude/rules/task-ui-vorgaben.md`).
- Verworfen: `DesignSystem/SplitEditor.fs:95` Ziel-Feld auf `Input.pickerField`. Grund: `pickerField` zeigt immer eine
  Bezeichnung oben, jede Zeile würde etwa 16 px höher; Entscheidung Conductor 2026-11-12, nach Empfehlung. Das Ziel
  bleibt der heutige Knopf.

Hinweise für den Worker:
- `SplitEditor.fs` liegt unter `DesignSystem/`, wird aber nur vom Kassenbuch und von `/styleguide` benutzt
  (`Styleguide/View.fs:1794` bis `1822`). Das Beispiel im selben Schritt mitziehen, sonst bricht der Build.
- Kommentar „folgt der Klasse des Import-Editors“ in `splitSummaryProps` mitziehen. `.split-remainder` in
  `styles.css:2897` ist ungenutzt; hier nicht aufräumen.
- `Input.text` mit `Size = Small` trägt `text-base md:text-sm` selbst; `min-h-[36px]` fällt weg, die Höhe muss
  Invariante 2 am Handy bestehen.
- Der Speicherfehler gehört in `SplitEditorState` (etwa `SaveError: string option`), kein Bool.
- `removeIcon` ist laut §5 für „aus einer selbst geführten Liste herausnehmen“ gedacht, eine Split-Zeile passt; die
  Variante in §5 nennen.

Überschneidungen: Teil 2 (ledger-vf6z5, „Neue Buchung“) und Teil 3 (ledger-dn4vy, Filter und Liste) arbeiten in
`Ledger/View.fs` und `Ledger/State.fs` in anderen Funktionen. ledger-smv1s (Doppelaufruf sperren) nennt `StartUndoSplit`;
das ist ein anderer Zweig als die hier geänderten `Error`-Zweige.

## Outcome

Der Split-Editor im Kassenbuch folgt jetzt dem Styleguide. `./verify.sh` meldet `VERIFY: PASS` (dotnet 4776 grün, node 316 grün, beide Wächter grün).

- Rest: `SummaryProps.Rest: RestState` (`RestOpen`, `RestOver`, `RestBalanced`) ersetzt `RestWarn`/`Balanced`. `restAmountClass` färbt offen in `Colors.attention`, zu viel in `text-error`, ausgeglichen neutral (`src/Client/DesignSystem/SplitEditor.fs`, `splitSummaryProps` in `src/Client/Components/Ledger/View.fs`). Der Rest unter null behält sein Minus wie bisher.
- Zeilenfelder: Betrag und Memo laufen über `Input.text` (Small). Der Betrag nutzt `Input.amountFieldClass false invalid`, `InputMode "decimal"` und `AriaInvalid`. Die reine Funktion `lineAmountInvalid` steht in `src/Client/Components/Ledger/SplitEditor.fs`, die View übergibt sie als `LineProps.AmountInvalid`. `data-testid="split-amount-<i>"` und `split-memo-<i>` sitzen jetzt auf einem Wrapper (Selektor `… input`, im Sweep und in `[flow:aufteilen]` nachgezogen).
- "Zeile entfernen": neue Variante `Button.removeIconWhen disabled` (`removeIcon` = ohne Sperre), Name über `aria-label`, `data-testid="split-remove-<i>"`, in §5 Zeile „Button“ von `standards/frontend/styleguide.md` genannt.
- Speicherfehler: `SplitEditorState.SaveError: string option`. `splitSaveFailed` in `src/Client/Components/Ledger/State.fs` setzt ihn bei offenem Editor derselben Buchung und schickt dann keinen Toast; ist der Editor zu, kommt der Toast wie bisher. `editSplit` (jede Eingabe) und `StartSaveSplits` nehmen ihn zurück. Angezeigt wird er per `SplitEditor.saveError` (`ErrorDisplay.inline'`) über „Speichern“: im Panel in `footer`, am Handy im Sheet-Fuß über `sheetFooter`.
- Tests (`src/Tests/LedgerSplitEditorClientTests.fs`):
  - „Rest-Farbe: offen gelb, zu viel verteilt rot, ausgeglichen neutral“ ist umgeschrieben und war mit `RestOpen -> text-error` rot.
  - „lineAmountInvalid …“ ist neu.
  - `saveErrorTests` zählt je Schreibweg (Aufteilen, Betrag mit Aufteilung) alle Reihenfolgen aus Speichern, Eingabe, Editor schließen, Detail schließen und Antwort (Erfolg, Fehler) bis Tiefe 6 durch das echte `update` auf, mit eigenem Orakel. Mit dem alten Verhalten (immer Toast) war der Test rot.
  - Der alte Test „Befehlsfehler: … Toast“ entfällt, weil die Aufzählung ihn ersetzt.
- Layout-Sweep (`scripts/layout-states.mjs`):
  - Neu ist `kassenbuch-aufteilen-ungueltig`: „abc“ eintippen, `aria-invalid` und `input-invalid` am Feld prüfen, ein leeres Feld bleibt ohne `aria-invalid`.
  - Neu ist `kassenbuch-aufteilen-speicherfehler`: Mock `setEntrySplits` antwortet mit Error, der Zustand prüft, dass die Meldung genau einmal erscheint (kein Toast), und `reach` verlangt sie ohne Scrollen.
  - `kassenbuch-aufteilen` misst mit zwei Zeilen beide Entfernen-Knöpfe (Invariante 2).
  - `[flow:aufteilen]` in `scripts/ledger-split-editor.test.mjs` findet „Zeile entfernen“ per `getByRole`, fügt eine Zeile hinzu und entfernt sie wieder.
- `/styleguide`, Abschnitt 27: Das Beispiel zeigt einen offenen Rest in Gelb und die Zeile „12,5o“ als ungültig. Darunter steht der Handy-Fuß mit Speicherfehler. Der Hinweistext ist angepasst.
- Vergleich vorher/nachher (eigene Playwright-Aufnahmen mit gemockter API, Skript `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/ng110/tools/shots.mjs`). Die Dateien liegen unter `/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/20e05553-37dc-4f2f-bcee-5ef520140bf9/scratchpad/e3/ledger-ng110/`:
  - Vorher: `vorher-handy-412-offen.png`, `vorher-handy-412-ungueltig.png`, `vorher-handy-412-zu-viel.png`, `vorher-styleguide-split-editor.png`, `vorher-styleguide-split-sheet-fuss.png`.
  - Nachher: dieselben Namen mit `nachher-` statt `vorher-`.
  - Abweichungen sind nur die genannten. Dazu kommt eine kleine sichtbare Folge von `amountFieldClass`: der Platzhalter „0,00“ im Betragsfeld steht jetzt in Mono (wie in „Neue Buchung“).

Für die Kann-Sammlung:
- `.split-remainder` in `styles.css` ist weiter ungenutzt (laut Task hier nicht aufgeräumt).
