---
id: ledger-hme9k
title: "Durchgehen und Detail ohne zweite Ebene: Payee und Memo im offenen Detail öffnen kein zweites Sheet oder zweiten Dialog, das Regel-Formular ersetzt den Inhalt desselben Detail-Sheets, × im Split-Editor am PC führt ins Detail zurück, „Details“ schließen im Durchgehen über denselben Helfer wie im Kassenbuch (Components/Ledger/View.fs, State.fs)"
status: done
type: bug
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [bestandsreview, frontend, tests]
related_adrs: [0005, 0087, 0096]
related_research: []
prior_art: []
---

## Why
Bestandsreview ledger 2026-11-19, Gruppe G2 (Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`), von Roman
zur Task gewählt.

Im Durchgehen mit offenen „Details“ legt ein Tipp auf Payee oder Memo eine zweite Ebene über das Detail: am Handy ein
zweites Sheet, am PC einen zweiten Dialog (Payee) bzw. einen Dialog über dem schon offenen Memo-Feld im Panel. Das
verletzt §7.1 Punkt 4 und §7.15 (keine gestapelten Sheets) und lässt zwei Editoren mit demselben Entwurf stehen.
Öffnet Roman am Handy aus dem Detail „Regel anlegen“, verschwindet das Detail-Sheet und ein anderes steht da; nach
Schließen steht das Detail wieder oben, nicht an der Zeile, von der er kam (§7.8 Punkt 6, §7.10). Am PC schließt das ×
im Kopf des Split-Editors das ganze Detail, während Esc, „Abbrechen“ und das × im Sheet ins Detail zurückführen. Und
„Details“ schließen im Durchgehen setzt die Felder des Details von Hand und vergisst die aufgeklappte Historie.

## What
Geprüft an HEAD f98a8ce6. Die Zeilennummern des Berichts stimmen bis auf Kleinigkeiten.

**1. Payee und Memo im offenen Detail des Durchgehens (hoch).** `ReviewPage` (`View.fs:5437`) rendert
`reviewEditors` (`View.fs:5337` bis 5389) bei jeder Karte, auch bei `review.DetailShown`. Damit:
- Handy (unter 1024 px, `desk = false`): Payee ersetzt im Detail-Sheet schon den Inhalt (`detailSheetView`,
  `payeeEdit`), `reviewEditors` legt ein zweites `BottomSheet` „Payee“ darüber. Memo steht im Detail als Feld in der
  Zeile (`detailContent`, `memoEditFor`), `reviewEditors` öffnet zusätzlich ein Sheet „Memo“ mit demselben Entwurf.
- PC (ab 1024 px): `detailPanelView` rendert `payeeDialog` (`View.fs:2159`), `reviewEditors` rendert denselben
  `payeeDialog` noch einmal: zwei Dialoge übereinander. Memo steht im Panel als Feld, `reviewEditors` öffnet darüber
  den Dialog „Memo“. Am PC sind Payee und Memo der Karte neben dem Panel weiter antippbar.

Soll: `reviewEditors` steht nur ohne Detail (`not review.DetailShown`), wie `pickerOpen` der Karte (`View.fs:5176`).
Mit offenem Detail gilt, was im Kassenbuch gilt: am Handy ersetzt die Payee-Auswahl den Inhalt des Detail-Sheets, Memo
öffnet als Feld in der Detail-Zeile; am PC öffnet Payee genau einen Dialog, Memo als Feld im Panel, auch wenn Roman das
Feld auf der Karte neben dem Panel antippt. Nach Wahl, Speichern oder Schließen steht das Detail wieder da.

**2. Regel-Formular im Detail-Sheet (mittel, angepasst).** `detailSheetOrRuleForm` (`View.fs:2269`) gibt mit offenem
Formular `ruleFormSheet (Some form)` zurück (`BottomSheet.viewHeld`, Komponente `HeldSheet`) statt `detailSheetView`
(`BottomSheet.view`, Komponente `BottomSheetInternal`). React hängt das Detail-Sheet samt Abdunklung aus und ein neues
Sheet ein; beide hängen schon offen ein (`active` beim Einhängen), deshalb fährt keins ein oder aus (anders als im
Bericht: nichts fährt ein, es springt). Nach Schließen hängt das Detail neu ein und steht oben, die Scrollposition ist
weg. Das betrifft am Handy das Kassenbuch (`View.fs:4646`) und das Durchgehen (`View.fs:5444`). Am PC ersetzt das
Formular den Inhalt des Panels schon richtig (`detailPanelOrRuleForm`). Der Zweig `ruleFormSheet None` wird nur über
der Karte gebraucht (`ruleSheetOverCard`), nie aus `detailSheetOrRuleForm`.

Soll: Das Regel-Formular ist ein weiterer Inhalt von `detailSheetView`, wie Payee, Betrag, Kategorie und Aufteilen.
Titel, Fuß und Schließen kommen aus `EntryRuleForm` (`Title`, `Footer`, `OnBack`; mit offener Kategorie-Auswahl im
Formular deren Titel und Zurück, wie heute aus `Rules.View.entryForm`), Unterzeile wie heute keine. Das Sheet bleibt
dabei dasselbe offene Sheet: kein Ein- oder Ausfahren beim Wechsel (§7.10, Animation nur beim Öffnen), die Abdunklung
bleibt stehen. Nach Schließen, Abbrechen oder „Regel anlegen“ steht das Detail an derselben Scrollposition wie vor dem
Öffnen, die Zeile „Regel anlegen“ im Bild.

**3. × im Kopf des Split-Editors am PC (niedrig).** `detailPanelView` (`View.fs:2137`) gibt `splitEditorHead` als
Schließen `CloseDetail` mit; das Sheet schließt mit `CloseSplitEditor` (`View.fs:2198`), Esc ebenso
(`State.fs`, `EscapePressed`, Zweig `SplitEditor.IsSome`), „Abbrechen“ in der Fußleiste auch. Soll: × im Panel führt
zurück ins Detail derselben Buchung, der Entwurf ist verworfen wie bei Esc und „Abbrechen“. Läuft das Speichern
(`rowBusy CategoryLock`), tut × nichts, wie `CloseSplitEditor` heute.

**4. „Details“ schließen im Durchgehen (niedrig).** `CloseDetail` im Durchgehen (`State.fs:1860` bis 1875) setzt
`History`, `MemoEdit`, `AmountEdit`, `PayeeEdit` von Hand und vergisst `HistoryOpenFor`; `ReviewShowDetails`
(`State.fs:4650`) setzt es auch nicht zurück. Folge: Historie aufklappen, „Details“ schließen, „Details“ wieder öffnen,
und die Historie steht schon aufgeklappt da; im Kassenbuch öffnet das Detail immer zugeklappt (`showDetail`,
`State.fs:1383`). `DetailFor` darf im Durchgehen nicht weg (die Karte ist `DetailFor`), deshalb geht es nicht direkt
über `closeDetail`.

Entscheidungen Conductor 2026-11-19, nach Empfehlung:
- Befund 1: `ReviewPage` rendert `reviewEditors` nur bei `not review.DetailShown`. Am PC kommt der Payee-Dialog bei
  offenem Detail allein aus `detailPanelView`.
- Befund 2: `detailSheetView` bekommt den `EntryRuleFormSlot` und zeigt das Formular als Inhalt; `detailSheetOrRuleForm`
  entfällt. `ruleFormSheet` bleibt nur für das Formular über der Karte. Die Scrollposition des Detail-Inhalts merkt sich
  `detailSheetView` beim Wechsel vom Detail weg (Ref auf `.sheet-body`) und setzt sie bei der Rückkehr zum Detail
  wieder, für jeden Inhaltswechsel im Detail-Sheet (Regel, Payee, Betrag, Kategorie, Aufteilen), weil es eine Stelle ist
  und §7.8 Punkt 6 für alle gilt.
- Befund 3: eine reine Funktion `detailCloseStep` (Name frei) liefert die Nachricht für Schließen im Detail: offene
  Payee-Auswahl `CancelPayeeEdit`, offener Betrag `CancelAmountEdit`, offener Split-Editor `CloseSplitEditor`, sonst
  `CloseDetail`. Sheet (`OnClose`, nach der Kategorie-Auswahl wie heute) und Panel-Kopf rufen sie; der doppelte Weg im
  Sheet fällt weg. Der Knopf im Kopf des Editors heißt für den Screenreader „Aufteilen schließen“ (heute „Detail
  schließen“), `title` „Schließen“ und `data-testid="detail-close"` bleiben; braucht `Detail.head` dafür einen Parameter
  oder eine Variante, zeigt `/styleguide` sie.
- Befund 4: ein Helfer (etwa `clearDetailEdits`) setzt alle Felder des offenen Details zurück außer `DetailFor`
  (`History`, `HistoryOpenFor`, `MemoEdit`, `AmountEdit`, `PayeeEdit`); `closeDetail` ist dieser Helfer plus
  `DetailFor = None`, der Zweig im Durchgehen ist dieser Helfer plus `SplitEditor`, `CategoryPickerFor`,
  `CategorySuggestions` wie heute.
- Punkt 5 (aus Kann-Sammlung ledger-k4n2s Punkt 117, von Roman freigegeben 2026-11-19): das Detail-Sheet im
  Durchgehen hängt heute mit `IsOpen = true` ein und aus (`View.fs:5444`) und fährt deshalb weder ein noch aus (§6,
  §7.10). Es wird immer gerendert, `IsOpen = review.DetailShown`, wie das Detail-Sheet im Kassenbuch.
- Kommentare nachziehen: `CategoryPickerPlacement` (`View.fs:1416`, „als zweites Sheet über dem Detail-Sheet“ gilt seit
  ledger-ea70s nicht mehr) und Invariante 5 in `scripts/layout-invariants.mjs` („das zweite Sheet über dem Detail“).

## Acceptance criteria
- [ ] Regressionstest Befund 1, vor dem Fix rot (Layout-Sweep, Doktrin Punkt 8): neue Invariante 11 in
  `scripts/layout-invariants.mjs` „Höchstens eine Ebene“: sichtbar ist höchstens ein `.bottom-sheet.active` oder
  `[data-layer="dialog"]`; ein Popover in einer Ebene und der Toast zählen nicht. Gegenbeweis in
  `scripts/layout-invariants.test.mjs` (`[layout] Gegenbeweis Invariante 11: ein zweites Sheet über dem Detail meldet
  die Ebene`). Neue Zustände in `scripts/layout-states.mjs` mit Kommentar zum Bug: `durchgehen-details-payee` und
  `durchgehen-details-memo` bei 412 und 1280 px (Durchgehen, „Details“, dann Payee bzw. Memo; am PC auch über das Feld
  der Karte neben dem Panel). Vor dem Fix melden beide die Invariante, danach nicht. Meldet die Invariante einen
  bestehenden Zustand, steht er mit Breite und Selektor im Outcome; ist es ein gestapeltes Sheet im Kassenbuch, wird er
  hier behoben, sonst eine Ausnahme nach Doktrin Punkt 5 mit Grund.
- [ ] Befund 1, intakt: der Zustand `durchgehen-memo` (Memo-Sheet über der Karte ohne Details, 412 px) und
  `durchgehen-regel` bleiben grün; ohne Details öffnen Payee und Memo der Karte am Handy weiter ein Sheet, am PC einen
  Dialog.
- [ ] Regressionstest Befund 2, vor dem Fix rot (`[scroll]` in `scripts/regel-aus-buchung.test.mjs`, 412 px, Touch):
  GIVEN Detail einer Buchung im Kassenbuch, so weit gerollt, dass „Regel anlegen“ erst nach Scrollen im Bild ist (etwa
  Historie aufgeklappt), WHEN „Regel anlegen“ getippt und das Formular über × geschlossen, THEN zeigt dasselbe
  `.bottom-sheet`-Element (vor dem Tippen markiert) erst das Formular (`rule-form`), dann wieder das Detail, die
  Abdunklung war durchgehend aktiv, und `.sheet-body` steht auf derselben Scrollposition wie vor dem Tippen
  (Toleranz 2 px). Vor dem Fix ist das Element ein anderes und die Position 0.
- [ ] Befund 2, intakt: `[flow:regel-aus-buchung]` bleibt grün (nach dem Anlegen steht wieder die Buchung da); im
  Durchgehen mit „Details“ am Handy ersetzt das Formular ebenso den Inhalt des Detail-Sheets (eigener Sweep-Zustand
  `durchgehen-details-regel` bei 412 px, Invariante 11 grün); über der Karte ohne Details bleibt es ein eigenes Sheet,
  das ein- und ausfährt.
- [ ] Regressionstest Befund 3, vor dem Fix rot (Expecto, `src/Tests/LedgerSplitEditorClientTests.fs` oder
  `LedgerDetailTests.fs`): Tabelle über `detailCloseStep`, die Funktion bildet zuerst das heutige Verhalten des
  Panel-Kopfs ab (Split-Editor offen ergibt `CloseDetail`), dann der Fix. Fälle: Split-Editor offen ergibt
  `CloseSplitEditor`; Payee offen `CancelPayeeEdit`; Betrag offen `CancelAmountEdit`; nichts offen `CloseDetail`.
  Dazu über das echte `update`: GIVEN Detail von A am PC mit Split-Editor, WHEN die Nachricht aus `detailCloseStep`,
  THEN `DetailFor = Some A`, `SplitEditor = None`; während `rowBusy CategoryLock` bleibt der Editor offen.
- [ ] Regressionstest Befund 4, vor dem Fix rot (Expecto über das echte `update`,
  `src/Tests/LedgerReviewClientTests.fs`): GIVEN Durchgehen mit Karte A, `ReviewShowDetails`, `ToggleHistory`
  (`HistoryOpenFor = Some A`), WHEN `CloseDetail` und danach `ReviewShowDetails`, THEN `HistoryOpenFor = None`. Intakt:
  nach `CloseDetail` im Durchgehen bleibt `DetailFor = Some A` und Karte A sichtbar, `MemoEdit`, `AmountEdit`,
  `PayeeEdit`, `SplitEditor`, `CategoryPickerFor` sind leer und `RunReviewRefresh A` ist abgesetzt; im Kassenbuch
  setzt `CloseDetail` `DetailFor = None` wie heute.
- [ ] Kein zweiter Weg: `closeDetail` und der Zweig `CloseDetail` im Durchgehen nutzen denselben Helfer; im Sheet und
  im Panel-Kopf steht keine eigene Kaskade für Schließen mehr neben `detailCloseStep`.
- [ ] Punkt 5: im Durchgehen am Handy steht das Detail-Sheet (`.bottom-sheet` mit dem Detail der Karte) auch bei
  geschlossenen „Details“ im DOM, ohne `.active`; „Details“ setzt `.active`, Schließen nimmt es wieder weg, das Element
  bleibt dasselbe (Browser-Test `[layout]` oder Erweiterung des `[scroll]`-Tests in
  `scripts/regel-aus-buchung.test.mjs`, 412 px, vor dem Fix rot). Das Ein- und Ausfahren selbst prüft Roman im
  Blick unten.
- [ ] README ledger nachgezogen: Bullet **Split-Editor (Kassenbuch)**: „Schließen (× im Kopf, Esc, „Abbrechen“) führt
  zurück ins Detail, am Handy wie am PC.“ Bullet **Regel aus der Buchung**: im Detail-Sheet ersetzt das Formular den
  Inhalt desselben Sheets, nach Schließen steht das Detail an derselben Stelle. Bullet **Durchgehen**: mit offenen
  „Details“ öffnen Payee und Memo im Detail (keine zweite Ebene). Delta vor der Integration durch
  `npm run check:readme-delta`.
- [ ] [human-eye] Roman am Galaxy S25 Ultra (412 px, Touch), Durchgehen und Kassenbuch: „Details“, dann Payee, Memo,
  Kategorie, „Regel anlegen“ und Aufteilen je öffnen und schließen. Es steht immer nur ein Sheet da, beim Wechsel blitzt
  nichts auf und nichts fährt neu ein, nach Schließen steht das Detail dort, wo er war. Am PC ab 1024 px dasselbe mit
  Panel und Dialog: ein Dialog für Payee, Memo im Panel, × im Split-Editor zurück ins Detail.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Bestandsreview ledger 2026-11-19, Gruppe G2, Bericht `.agentheim/knowledge/reviews/2026-11-19-ledger.md`. Urteile
(geprüft an HEAD f98a8ce6):
- `View.fs:5437` Payee und Memo im offenen Detail: bestätigt, am PC zusätzlich Memo als Dialog über dem Feld im Panel.
- `View.fs:2271` Regel-Formular: angepasst. Der Wechsel ersetzt das ganze Sheet, beide Sheets hängen offen ein und
  fahren deshalb nicht (kein „fährt ein“ wie im Bericht); sichtbar ist der harte Wechsel und das Detail steht nach
  Schließen oben. Betrifft Kassenbuch und Durchgehen am Handy.
- `View.fs:2137` × im Split-Editor am PC: bestätigt.
- `State.fs:1860` `CloseDetail` im Durchgehen: bestätigt, sichtbar als schon aufgeklappte Historie beim erneuten
  Öffnen. `closeDetail` direkt geht nicht, weil es `DetailFor` leert.

Entschieden (Roman, 2026-11-19): Kann-Sammlung ledger-k4n2s Punkt 117 geht in dieser Task auf (siehe What, Punkt 5).

Hinweise für den Worker:
- `BottomSheet.view` zeigt den Inhalt nur, solange `open'` gilt (`detailSheetView`, letzter `match`); das Formular als
  Inhalt hängt sich dort ein wie `PayeePicker.view`.
- Der Fuß des Formulars am Handy kommt aus `EntryRuleForm.Footer` und ersetzt den Fuß des Details (wie `amountFooter`).
- Esc im Durchgehen (`KeyLeave`, `State.fs:4676`) schließt Payee und Memo vor dem Detail; mit Befund 1 ändert sich daran
  nichts.
- Am PC im Durchgehen ohne Details bleibt das Regel-Formular ein Dialog (`ruleFormDialog`), mit Details ersetzt es den
  Inhalt des Panels (`detailPanelOrRuleForm`), beides unverändert.

Überschneidungen:
- ledger-ef068 (Kassenbuch weicht von DS-Bausteinen ab) nennt Köpfe und Schließen-Knöpfe in `View.fs`, auch
  `Detail.titleHead`; wer zuerst baut, gibt dem anderen die geänderte Signatur von `Detail.head` mit.
- ledger-y8xrr (Nachladen) ändert `State.fs` an anderen Stellen, keine gemeinsame Zeile.
- ledger-k4n2s Punkt 67 (Durchgehen nach `Components/Review/`) verschiebt `ReviewPage` und `reviewEditors`: nicht
  gleichzeitig bauen.

## Outcome

Alle vier Befunde und Punkt 5 sind behoben. `./verify.sh` endet mit `VERIFY: PASS` (dotnet 5168 grün, node 351 grün).

**Befund 1 (Payee und Memo im offenen Detail des Durchgehens).** `ReviewPage` rendert `reviewEditors` nur noch bei `not review.DetailShown` (`src/Client/Components/Ledger/View.fs`). Mit offenem Detail gilt, was im Kassenbuch gilt. Am Handy ersetzt die Payee-Auswahl den Inhalt des Detail-Sheets, Memo öffnet als Feld. Am PC kommt genau ein Payee-Dialog aus `detailPanelView`, Memo öffnet als Feld im Panel, auch wenn der Tipp auf das Feld der Karte geht.
- Neue Invariante 11 „Höchstens eine Ebene“ in `scripts/layout-invariants.mjs`: höchstens ein sichtbares `.bottom-sheet.active` oder `[data-layer="dialog"]`. Popover (`data-layer="popover"`) und Toast zählen nicht. Gegenbeweis in `scripts/layout-invariants.test.mjs` („[layout] Gegenbeweis Invariante 11: ein zweites Sheet über dem Detail meldet die Ebene“). Er prüft dazu, dass ein Popover in der Ebene nicht zählt.
- Neue Zustände in `scripts/layout-states.mjs`: `durchgehen-details-payee` und `durchgehen-details-memo` (412, 1280), `durchgehen-details-payee-karte` und `durchgehen-details-memo-karte` (1280, über das Feld der Karte neben dem Panel), `durchgehen-details-regel` (412).
- Gegen ein Bundle mit der View von HEAD waren sie rot:
  - payee @ 412: Invariante 11, `.bottom-sheet "Payee" und .bottom-sheet "Payee"`
  - payee @ 1280 und payee-karte @ 1280: Invariante 11, zwei `[data-layer="dialog"] "Payee"`
  - memo @ 412: Invariante 11, `.bottom-sheet "Memo"` und das Detail-Sheet
- Am PC war Memo vor dem Fix ein Dialog über dem Feld im Panel. Das sind zwei Editoren, aber nur eine Ebene, deshalb meldet Invariante 11 es nicht. Die Memo-Zustände prüfen darum zusätzlich, dass genau ein `detail-memo-editor` sichtbar ist (`assertOneMemoEditor`). Vor dem Fix brachen memo @ 412, memo @ 1280 und memo-karte @ 1280 mit „Memo steht 2-mal offen“ ab. Nach dem Fix sind alle grün.
- Invariante 11 meldet keinen bestehenden Zustand: der ganze Sweep ist grün, es gibt keine Ausnahme. `durchgehen-memo`, `durchgehen-regel`, `kassenbuch-detail`, `kassenbuch-detail-kategorie`, `kassenbuch-payee-auswahl` und `kassenbuch-regel-formular` sind grün.
- Den Bereich der Ausnahme-Prüfung im Sweep-Test habe ich auf 1 bis 11 erweitert.

**Befund 2 (Regel-Formular im Detail-Sheet).**
- `detailSheetOrRuleForm` ist entfernt. `detailSheetView` ruft die neue Komponente `DetailBottomSheet` auf. Sie bekommt den `EntryRuleFormSlot` und zeigt das Formular als weiteren Inhalt: Titel, Fuß und Schließen kommen aus `EntryRuleForm`, eine Unterzeile gibt es nicht. Das Sheet bleibt dasselbe `BottomSheetInternal`.
- Die Scrollposition des Details merkt sich die Komponente über einen Ref auf `.sheet-body`. Dafür gibt es die neue Funktion `BottomSheet.viewWithBody`, eine Ref-Variante ohne sichtbare Änderung (`src/Client/DesignSystem/BottomSheet.fs`). Die Komponente hört auf `scroll`, solange das Detail steht, und setzt den Wert bei der Rückkehr zum Detail derselben Buchung wieder. Das gilt für jeden Inhaltswechsel (Regel, Payee, Betrag, Kategorie, Aufteilen). Ein anderer Inhalt beginnt oben, ein geschlossenes Sheet vergisst die Stelle.
- `ruleFormSheet` bleibt nur für das Formular über der Karte.
- Regressionstest: „[scroll] Am Handy ersetzt „Regel anlegen“ den Inhalt desselben Detail-Sheets, nach Schließen steht das Detail an derselben Stelle“ in `scripts/regel-aus-buchung.test.mjs` (412 px, Touch, Historie aufgeklappt, ganz nach unten gerollt). Gegen das HEAD-Bundle rot („dasselbe Sheet zeigt das Formular“). Mit einem Mutanten ohne das Zurücksetzen der Scrollposition ebenfalls rot.
- `[flow:regel-aus-buchung]` bleibt grün.

**Befund 3 (× im Split-Editor am PC).**
- `Components.Ledger.State.detailCloseStep model entry` gibt die Reihenfolge vor: Payee offen ergibt `CancelPayeeEdit`, dann Betrag `CancelAmountEdit`, dann Split-Editor `CloseSplitEditor`, sonst `CloseDetail`.
- Sheet (`OnClose`, nach Regel-Formular und Kategorie-Auswahl) und beide Panel-Köpfe rufen sie auf. Die eigene Kaskade im Sheet ist weg.
- `Detail.head` (nur vom Split-Editor genutzt) heißt für den Screenreader jetzt „Aufteilen schließen“. `title` „Schließen“ und `data-testid="detail-close"` bleiben. Es gibt keinen neuen Parameter und keine neue Variante, `/styleguide` bleibt deshalb unverändert (`src/Client/DesignSystem/Detail.fs`).
- Tests in `src/Tests/LedgerSplitEditorClientTests.fs`: Tabelle „Schließen im Detail schließt zuerst, was im Detail offen ist“ (zuerst mit dem Verhalten von heute, Split-Editor ergab `CloseDetail`, rot) und „× im Split-Editor am PC führt zurück ins Detail derselben Buchung, nicht während des Speicherns“ (vorher rot).

**Befund 4 („Details“ schließen im Durchgehen).**
- `clearDetailEdits` setzt `History`, `HistoryOpenFor`, `MemoEdit`, `AmountEdit` und `PayeeEdit` zurück. `closeDetail` ist dieser Helfer mit `DetailFor = None`. Der Zweig `CloseDetail` im Durchgehen ist dieser Helfer mit `SplitEditor`, `CategoryPickerFor` und `CategorySuggestions` wie bisher.
- Tests in `src/Tests/LedgerReviewClientTests.fs`:
  - „„Details“ schließen klappt die Historie zu, beim nächsten Öffnen steht sie zugeklappt da“ (vorher rot)
  - „„Details“ schließen leert alles Offene im Detail, die Karte bleibt und wird neu gelesen“ (intakt, mit `RunReviewRefresh A`)
  - „im Kassenbuch schließt CloseDetail das Detail ganz“

**Punkt 5.** Am Handy rendert das Durchgehen das Detail-Sheet immer, mit `IsOpen = card.IsSome && review.DetailShown`. Test: „[layout] Im Durchgehen am Handy steht das Detail-Sheet auch bei geschlossenen „Details“ im DOM, „Details“ und Schließen schalten nur .active“ in `scripts/regel-aus-buchung.test.mjs`, gegen das HEAD-Bundle rot.

**Kommentare nachgezogen:** `CategoryPickerPlacement` (Inhalt des Detail-Sheets statt zweites Sheet) und der Kommentar an Invariante 5 in `scripts/layout-invariants.mjs`.

**Verhaltensänderung am Rand:** am PC schloss das × im Kopf des Split-Editors während des Speicherns bisher das ganze Detail. Jetzt tut es dann nichts, wie `CloseSplitEditor` und wie in der Task verlangt.

**README-Delta:** mit `npm run check:readme-delta -- --bc ledger --delta` geprüft, Exit 0, Liste „ganz nachlesen“ leer, kein `--keep`.

**Bilder für Romans Blick** (gemockte Sweep-Zustände, Bundle nach dem Fix). `npm run screenshot:route` nimmt nur eine Route ohne Bedienung auf und braucht das echte Backend, darum habe ich die Bilder über die Sweep-Zustände gemacht:
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-payee-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-payee-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-memo-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-memo-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-memo-karte-1280.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/durchgehen-details-regel-412.png
- /private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/4b5cf62e-3174-40e3-90f3-4b5312280eb9/scratchpad/shots/kassenbuch-regel-formular-390.png

Das Ein- und Ausfahren und das Kriterium [human-eye] prüft Roman selbst.
