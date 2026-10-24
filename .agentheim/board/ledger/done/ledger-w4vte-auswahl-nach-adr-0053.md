

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
