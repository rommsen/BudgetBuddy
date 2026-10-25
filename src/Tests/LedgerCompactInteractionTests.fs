module LedgerCompactInteractionTests

// ledger-m6zpd: Kompaktliste nach Eingabegerät (ADR 0053 Punkt 6). Die Entscheidung ist rein
// und läuft ohne Browser; die Bedienung im echten Browser prüfen
// `scripts/ledger-compact-mouse.test.mjs` (Maus und Touch).

open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readView () =
    File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Components", "Ledger", "View.fs"))

let private entryOf (n: int) : LedgerEntryView =
    LedgerSelectionTests.entryOf n (-10m * decimal n) true false

let private modelWith (count: int) : Model =
    let model, _ = Components.Ledger.State.init ()
    let loaded = [ for n in 1..count -> entryOf n ]
    { model with Entries = Success loaded; TotalCount = count; HasMoreEntries = false }

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = Components.Ledger.State.update msg model
    model'

let private columns : TableLayout = Columns [ PayeeColumn, Rest ]

[<Tests>]
let decisionTests =
    testList "Kompaktliste nach Eingabegerät: Entscheidung (ledger-m6zpd)" [
        testCase "alle acht Kombinationen aus Layout, Zeiger und Fensterbreite" <| fun () ->
            for finePointer in [ true; false ] do
                for desktop in [ true; false ] do
                    Expect.equal (listInteractionFor columns finePointer desktop) TableRows $"Tabelle, fine={finePointer}, md={desktop}"
            Expect.equal (listInteractionFor CompactList true true) CompactMouse "Kompaktliste, Maus, ab md"
            Expect.equal (listInteractionFor CompactList true false) CompactTouch "Kompaktliste, Maus unter md bleibt Touch"
            Expect.equal (listInteractionFor CompactList false true) CompactTouch "Kompaktliste, Finger, ab md (Tablet)"
            Expect.equal (listInteractionFor CompactList false false) CompactTouch "Kompaktliste, Finger, Handy"

        testCase "Merkmale je Modus: Kästchen, Hover-Schaltfläche, Markierung, Gesten, Hinweis" <| fun () ->
            let mouse = listInteractionTraits CompactMouse
            Expect.isTrue mouse.CheckboxVisible "Maus: Kästchen"
            Expect.isTrue mouse.HoverReviewButton "Maus: Prüf-Schaltfläche bei Hover"
            Expect.isTrue mouse.FocusMarkVisible "Maus: sichtbare Markierung"
            Expect.isFalse mouse.GesturesEnabled "Maus: keine Gesten"
            Expect.isTrue mouse.FooterHint "Maus: Fußzeilen-Hinweis"
            let touch = listInteractionTraits CompactTouch
            Expect.isFalse touch.CheckboxVisible "Finger: kein Kästchen"
            Expect.isFalse touch.HoverReviewButton "Finger: Schaltfläche immer sichtbar, kein Hover"
            Expect.isFalse touch.FocusMarkVisible "Finger: keine Markierung"
            Expect.isTrue touch.GesturesEnabled "Finger: Gesten"
            Expect.isFalse touch.FooterHint "Finger: kein Hinweis"
            Expect.equal (listInteractionTraits TableRows) mouse "Tabelle wie Maus"
    ]

[<Tests>]
let hintAndReducerTests =
    testList "Kompaktliste nach Eingabegerät: Hinweis und Reducer (ledger-m6zpd)" [
        testCase "keyboardHintVisible: aus bei CompactTouch, an bei CompactMouse und TableRows" <| fun () ->
            let model = modelWith 2
            Expect.isFalse (keyboardHintVisible { model with ListInteraction = CompactTouch }) "Touch"
            Expect.isTrue (keyboardHintVisible { model with ListInteraction = CompactMouse }) "Maus"
            Expect.isTrue (keyboardHintVisible { model with ListInteraction = TableRows }) "Tabelle"

        testCase "ListInteractionChanged setzt das Feld; der Anfangswert ist TableRows" <| fun () ->
            let model = modelWith 1
            Expect.equal model.ListInteraction TableRows "Anfangswert"
            Expect.equal (step (ListInteractionChanged CompactMouse) model).ListInteraction CompactMouse "Maus"
            Expect.equal (step (ListInteractionChanged CompactTouch) model).ListInteraction CompactTouch "Touch"

        testCase "ClickRowCheckbox wählt und nimmt die Spanne ohne SelectionMode" <| fun () ->
            let id n = (entryOf n).Id
            let single = modelWith 12 |> step (ClickRowCheckbox (id 1, false))
            Expect.isTrue (isSelected single.Selection (id 1)) "Zeile gewählt"
            Expect.isFalse single.SelectionMode "kein Auswahl-Modus"
            let spanned = single |> step (ClickRowCheckbox (id 5, true))
            for n in 1..5 do
                Expect.isTrue (isSelected spanned.Selection (id n)) $"Zeile {n} in der Spanne"
            Expect.isFalse spanned.SelectionMode "kein Auswahl-Modus nach der Spanne"
    ]

[<Tests>]
let sourceTests =
    testList "Kompaktliste mit Maus: Quelltext (ledger-m6zpd)" [
        testCase "das Kästchen der Maus-Zeile schickt ClickRowCheckbox mit Umschalt-Information" <| fun () ->
            let view = readView ()
            let from = view.IndexOf "let private entryRowWithReviewMark"
            let body = view.Substring(from, view.IndexOf("let private dateGroupHeader", from) - from)
            Expect.stringContains body "MultiSelect.boxButton" "Kästchen"
            Expect.stringContains body "ClickRowCheckbox (entry.Id, shift)" "Nachricht wie die Tabelle"

        testCase "die Maus-Zeile ist kein Knopf und setzt die Fokus-Attribute" <| fun () ->
            let view = readView ()
            Expect.stringContains view "if mouse.IsSome then (fun props -> Html.div props)" "div statt button"
            Expect.stringContains view "data-keyboard-focus" "Fokus-Attribut"
            Expect.stringContains view "Table.focusedRowClass" "Fokus-Darstellung der Tabelle"

        testCase "langes Drücken ist mit Maus aus und die Zeile wird nicht gewischt" <| fun () ->
            let view = readView ()
            let from = view.IndexOf "let private mobileEntryRow"
            let body = view.Substring(from, view.IndexOf("let private entryListMobile", from) - from)
            Expect.stringContains body "LongPressRow (not selectionMode && mouse.IsNone)" "kein langes Drücken"
            let mouseAt = body.IndexOf "elif mouse.IsSome then"
            let swipeAt = body.IndexOf "swipeableEntryRow"
            Expect.isGreaterThan swipeAt mouseAt "Wisch erst nach dem Maus-Zweig"

        testCase "der Container der Maus-Zeile: Markierung, Anker und Statuskante" <| fun () ->
            let plain = mouseRowContainerClassName { Selected = false; IsAnchor = false; IsFocused = false } false
            Expect.stringContains plain "group" "group für den Hover der Schaltfläche"
            let focused = mouseRowContainerClassName { Selected = false; IsAnchor = false; IsFocused = true } false
            Expect.stringContains focused Client.DesignSystem.Table.focusedRowClass "Fokus-Darstellung"
            let selected = mouseRowContainerClassName { Selected = true; IsAnchor = false; IsFocused = false } false
            Expect.stringContains selected "bg-neon-teal/[0.07]" "gewählt"
            let anchorReview = mouseRowContainerClassName { Selected = true; IsAnchor = true; IsFocused = false } true
            Expect.stringContains anchorReview "inset_2px_0_0_0_var(--color-neon-teal)" "Anker"
            Expect.isFalse (anchorReview.Contains Client.DesignSystem.Tokens.ListRoles.statusEdge) "Anker gewinnt gegen die Statuskante"
            let review = mouseRowContainerClassName { Selected = false; IsAnchor = false; IsFocused = false } true
            Expect.stringContains review Client.DesignSystem.Tokens.ListRoles.statusEdge "Statuskante"
    ]
