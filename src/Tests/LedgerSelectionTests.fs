module LedgerSelectionTests

// ledger-r1d2f: Auswahl-Modus mit Summe. Rechnen und Reducer sind rein und laufen ohne
// Proxy. Feliz-Elemente lassen sich unter .NET nicht inspizieren, deshalb prüfen die
// Ansichts-Tests öffentliche reine Funktionen und lesen den Quelltext.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let entryOf (n: int) (amount: decimal) (needsReview: bool) (deleted: bool) : LedgerEntryView = {
    Id = Guid.Parse $"0195f900-0000-7000-8000-{n:D12}"
    AccountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"
    Date = DateTime(2026, 10, 1)
    Amount = eur amount
    Payee = Some $"Payee {n}"
    Memo = None
    CategoryName = None
    CategoryId = None
    CategoryGroupName = None
    SplitCategoryGroupNames = Map.empty
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = deleted
    NeedsReview = needsReview
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
}

/// Zeile n mit dem Betrag -10 * n.
let private entry (n: int) : LedgerEntryView = entryOf n (-10m * decimal n) false false

let private idOf (n: int) = (entry n).Id

let private rows (count: int) = [ for n in 1..count -> entry n ]

/// Ein Modell, das `loaded` Zeilen geladen hat, aber einen Treffer mit `total` Buchungen
/// und der Summe `totalAmount` meldet.
let private modelWith (loaded: LedgerEntryView list) (total: int) (totalAmount: decimal) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = total
        TotalAmount = eur totalAmount
        HasMoreEntries = List.length loaded < total }

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = Components.Ledger.State.update msg model
    model'

let private amountOf (model: Model) = (selectionTotals model).Amount.Amount
let private countOf (model: Model) = (selectionTotals model).Count

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

[<Tests>]
let totalsTests =
    testList "Auswahl: selectionTotals (ledger-r1d2f)" [

        testCase "Picked: Anzahl und Summe der gewählten geladenen Zeilen" <| fun () ->
            let model = { modelWith (rows 5) 50 -1500m with Selection = Picked (Set.ofList [ idOf 1; idOf 3 ]) }
            Expect.equal (countOf model) 2 "zwei Zeilen"
            Expect.equal (amountOf model) -40m "-10 und -30"

        testCase "Picked: eine in YNAB gelöschte Buchung zählt nicht zur Summe, aber zur Anzahl" <| fun () ->
            let rows' = [ entry 1; entryOf 2 -500m false true ]
            let model = { modelWith rows' 2 -10m with Selection = Picked (Set.ofList [ idOf 1; idOf 2 ]) }
            Expect.equal (countOf model) 2 "beide gewählt"
            Expect.equal (amountOf model) -10m "die gelöschte Buchung bleibt außen vor"

        testCase "AllExcept: TotalCount und TotalAmount minus die Ausnahmen" <| fun () ->
            let model = { modelWith (rows 5) 50 -1500m with Selection = AllExcept (Set.ofList [ idOf 2; idOf 4 ]) }
            Expect.equal (countOf model) 48 "50 minus 2"
            Expect.equal (amountOf model) -1440m "-1500 minus (-20 und -40)"

        testCase "AllExcept: eine gelöschte Ausnahme verändert die Summe nicht" <| fun () ->
            let rows' = [ entry 1; entryOf 2 -500m false true ]
            let model = { modelWith rows' 2 -10m with Selection = AllExcept (Set.singleton (idOf 2)) }
            Expect.equal (countOf model) 1 "eine Zeile bleibt"
            Expect.equal (amountOf model) -10m "TotalAmount zählt die gelöschte Buchung schon nicht"

        testCase "Ohne Auswahl ist alles null" <| fun () ->
            let model = modelWith (rows 3) 3 -60m
            Expect.equal (countOf model) 0 "keine Zeile"
            Expect.equal (amountOf model) 0m "keine Summe"
            Expect.isFalse (hasSelection model) "keine Auswahl"
    ]

[<Tests>]
let reducerTests =
    testList "Auswahl: Reducer (ledger-r1d2f)" [

        testCase "Langes Drücken startet den Modus mit genau dieser Zeile" <| fun () ->
            let model = modelWith (rows 5) 5 -150m |> step (StartSelectionMode (idOf 3))
            Expect.isTrue model.SelectionMode "Modus an"
            Expect.equal model.Selection (Picked (Set.singleton (idOf 3))) "nur Zeile 3"
            Expect.equal (countOf model) 1 "eine Zeile"

        testCase "Tipp wählt ab, zweiter Tipp wählt wieder aus, die letzte abzuwählen lässt den Modus an" <| fun () ->
            let started = modelWith (rows 5) 5 -150m |> step (StartSelectionMode (idOf 3))
            let deselected = started |> step (ToggleRowSelected (idOf 3))
            Expect.equal (countOf deselected) 0 "abgewählt"
            Expect.isTrue deselected.SelectionMode "der Modus bleibt an"
            let again = deselected |> step (ToggleRowSelected (idOf 3))
            Expect.equal (countOf again) 1 "wieder gewählt"
            Expect.isTrue again.SelectionMode "Modus an"

        testCase "Alle: AllExcept leer mit TotalCount und TotalAmount, auch bei einer geladenen Seite" <| fun () ->
            let model = modelWith (rows 12) 34 -2140m |> step (StartSelectionMode (idOf 1)) |> step ToggleSelectAll
            Expect.equal model.Selection (AllExcept Set.empty) "ganzer Treffer"
            Expect.equal (countOf model) 34 "TotalCount"
            Expect.equal (amountOf model) -2140m "TotalAmount"
            Expect.isTrue (isAllSelected model.Selection) "Alle trägt den Aktivzustand"

        testCase "Alle noch einmal leert die Auswahl, der Modus bleibt" <| fun () ->
            let model =
                modelWith (rows 12) 34 -2140m
                |> step (StartSelectionMode (idOf 1))
                |> step ToggleSelectAll
                |> step ToggleSelectAll
            Expect.equal model.Selection NoSelection "leer"
            Expect.isTrue model.SelectionMode "Modus bleibt"

        testCase "Alle bei einer Teilauswahl wählt den ganzen Treffer" <| fun () ->
            let model =
                modelWith (rows 12) 34 -2140m
                |> step (StartSelectionMode (idOf 1))
                |> step ToggleSelectAll
                |> step (ToggleRowSelected (idOf 2))
                |> step ToggleSelectAll
            Expect.equal model.Selection (AllExcept Set.empty) "wieder der ganze Treffer"

        testCase "Unter AllExcept gelten nachgeladene Zeilen als gewählt, eine abgewählte senkt um ihren Betrag" <| fun () ->
            let first = modelWith (rows 3) 6 -210m |> step (StartSelectionMode (idOf 1)) |> step ToggleSelectAll
            let page : LedgerEntryPage = {
                Entries = [ entry 4; entry 5; entry 6 ]
                TotalCount = 6
                TotalAmount = eur -210m
                DeletedCount = 0
                NeedsReviewCount = 0
                AskLenaCount = 0
            }
            let loaded = first |> step (EntriesPageLoaded (1, Ok page))
            Expect.isTrue (isSelected loaded.Selection (idOf 5)) "eine nachgeladene Zeile gilt als gewählt"
            Expect.equal (countOf loaded) 6 "alle sechs"
            let deselected = loaded |> step (ToggleRowSelected (idOf 5))
            Expect.equal (countOf deselected) 5 "eine weniger"
            Expect.equal (amountOf deselected) (-210m + 50m) "um genau ihren Betrag (-50) höher"

        testCase "Shift-Klick vom Anker (Zeile 1) auf Zeile 12 wählt zwölf Zeilen, der Anker bleibt" <| fun () ->
            let anchored = modelWith (rows 20) 34 -2140m |> step (ClickRowCheckbox (idOf 1, false))
            Expect.equal anchored.SelectionAnchor (Some (idOf 1)) "Anker gesetzt"
            let spanned = anchored |> step (ClickRowCheckbox (idOf 12, true))
            Expect.equal (countOf spanned) 12 "zwölf Zeilen"
            Expect.equal spanned.SelectionAnchor (Some (idOf 1)) "der Anker bleibt Zeile 1"
            Expect.equal (amountOf spanned) -780m "die Summe der zwölf"

        testCase "Shift-Klick unter AllExcept nimmt die Spanne aus den Ausnahmen" <| fun () ->
            let model =
                modelWith (rows 20) 20 -2100m
                |> step ToggleHeaderCheckbox
                |> step (ClickRowCheckbox (idOf 2, false))
                |> step (ClickRowCheckbox (idOf 3, false))
            Expect.equal (countOf model) 18 "zwei abgewählt"
            let spanned = model |> step (ClickRowCheckbox (idOf 3, false)) |> step (ClickRowCheckbox (idOf 5, true))
            Expect.isTrue (isSelected spanned.Selection (idOf 4)) "Zeile 4 liegt in der Spanne"
            Expect.isTrue (isSelected spanned.Selection (idOf 5)) "Zeile 5 ebenfalls"

        testCase "Ein einfacher Klick setzt den Anker neu" <| fun () ->
            let model =
                modelWith (rows 20) 34 -2140m
                |> step (ClickRowCheckbox (idOf 1, false))
                |> step (ClickRowCheckbox (idOf 7, false))
            Expect.equal model.SelectionAnchor (Some (idOf 7)) "neuer Anker"
            Expect.equal (countOf model) 2 "beide gewählt"

        testCase "Shift-Klick ohne Anker verhält sich wie ein einfacher Klick" <| fun () ->
            let model = modelWith (rows 20) 34 -2140m |> step (ClickRowCheckbox (idOf 4, true))
            Expect.equal (countOf model) 1 "nur diese Zeile"
            Expect.equal model.SelectionAnchor (Some (idOf 4)) "sie wird zum Anker"

        testCase "Kopfzeilen-Kästchen: leer, Strich, Haken und was ein Klick daraus macht" <| fun () ->
            let empty = modelWith (rows 12) 34 -2140m
            Expect.equal (headerCheckState empty) HeaderEmpty "leer"
            let partial = empty |> step (ClickRowCheckbox (idOf 1, false))
            Expect.equal (headerCheckState partial) HeaderPartial "Teal-Strich"
            let fromPartial = partial |> step ToggleHeaderCheckbox
            Expect.equal fromPartial.Selection (AllExcept Set.empty) "Strich wählt den ganzen Treffer"
            Expect.equal (headerCheckState fromPartial) HeaderChecked "Haken"
            let fromEmpty = empty |> step ToggleHeaderCheckbox
            Expect.equal fromEmpty.Selection (AllExcept Set.empty) "leer wählt den ganzen Treffer"
            let cleared = fromPartial |> step ToggleHeaderCheckbox
            Expect.equal cleared.Selection NoSelection "Haken leert"
            Expect.equal (headerCheckState cleared) HeaderEmpty "wieder leer"

        testCase "Auswahl aufheben leert die Auswahl und beendet den Modus" <| fun () ->
            let model =
                modelWith (rows 5) 5 -150m
                |> step (StartSelectionMode (idOf 1))
                |> step (ToggleRowSelected (idOf 2))
                |> step ClearSelection
            Expect.equal model.Selection NoSelection "leer"
            Expect.isFalse model.SelectionMode "Modus aus"
            Expect.equal model.SelectionAnchor None "kein Anker"

        testList "Wechsel leert die Auswahl und beendet den Modus" [
            let account = Guid.Parse "0195e900-0000-7000-8000-000000000001"
            for name, msg in
                [ "Kontowechsel", SelectFilter (OneAccount account)
                  "Chip Zu prüfen", ToggleOnlyNeedsReview
                  "Chip Ohne Kategorie", ToggleWithoutCategory
                  "Gelöschte zeigen", ToggleIncludeDeleted
                  "Kategorie-Filter entfernt", RemoveCategoryFilter
                  "Payee-Filter gewählt", SelectPayeeFilter "Payee 1"
                  "Alle Filter löschen", ClearAllFilters
                  "Neu laden", LoadEntries ] do
                testCase name <| fun () ->
                    let selected =
                        modelWith (rows 5) 5 -150m
                        |> step (StartSelectionMode (idOf 1))
                        |> step (ClickRowCheckbox (idOf 2, false))
                    let after = selected |> step msg
                    Expect.equal after.Selection NoSelection "Auswahl leer"
                    Expect.isFalse after.SelectionMode "Modus aus"
                    Expect.equal after.SelectionAnchor None "kein Anker"
        ]

        testCase "Chip Zu prüfen: eine gewählte Zeile verlässt nach dem Prüfvermerk die Auswahl (Picked)" <| fun () ->
            let a, b = entryOf 1 -10m true false, entryOf 2 -20m true false
            let model =
                { modelWith [ a; b ] 2 -30m with OnlyNeedsReview = true; NeedsReviewCount = 2 }
                |> step (StartSelectionMode a.Id)
                |> step (ToggleRowSelected b.Id)
            Expect.equal (countOf model) 2 "beide gewählt"
            let outcome : ReviewMarkOutcome = {
                Previous = { IsApproved = false; ClearedState = ReviewUncleared }
                Change = ReviewMarkChanged
                WriteBack = ReviewMarkSent
            }
            let after = model |> step (ReviewMarkSet (a.Id, Reviewed, Ok outcome))
            Expect.equal after.Selection (Picked (Set.singleton b.Id)) "Zeile a ist aus der Auswahl"
            Expect.equal (countOf after) 1 "eine Zeile"
            Expect.equal (amountOf after) -20m "ihre Summe"
            Expect.equal after.LocallyRemoved 1 "der Seitenversatz zählt weiter"

        testCase "Chip Zu prüfen: eine gewählte Zeile verlässt nach dem Prüfvermerk die Auswahl (AllExcept)" <| fun () ->
            let a, b, c = entryOf 1 -10m true false, entryOf 2 -20m true false, entryOf 3 -30m true false
            let model =
                { modelWith [ a; b ] 3 -60m with OnlyNeedsReview = true; NeedsReviewCount = 3 }
                |> step (StartSelectionMode a.Id)
                |> step ToggleSelectAll
                |> step (ToggleRowSelected b.Id)
            Expect.equal (countOf model) 2 "drei minus eine Ausnahme"
            Expect.equal (amountOf model) -40m "-60 minus (-20)"
            let outcome : ReviewMarkOutcome = {
                Previous = { IsApproved = false; ClearedState = ReviewUncleared }
                Change = ReviewMarkChanged
                WriteBack = ReviewMarkSent
            }
            let afterSelected = model |> step (ReviewMarkSet (a.Id, Reviewed, Ok outcome))
            Expect.equal (countOf afterSelected) 1 "die gewählte Zeile a ist weg, c bleibt"
            Expect.equal (amountOf afterSelected) -30m "Summe stimmt"
            // Verlässt die abgewählte Zeile b die Liste, ändert sich an der Auswahl nichts.
            let afterExcluded = model |> step (ReviewMarkSet (b.Id, Reviewed, Ok outcome))
            Expect.equal (countOf afterExcluded) 2 "b war schon abgewählt"
            Expect.equal (amountOf afterExcluded) -40m "Summe unverändert"
            Expect.isFalse (match afterExcluded.Selection with AllExcept ex -> ex.Contains b.Id | _ -> true) "b ist keine Ausnahme mehr"
            ignore c
    ]

[<Tests>]
let viewTests =
    testList "Auswahl: Ansicht (ledger-r1d2f)" [

        testCase "Summenzeile nennt 'Auswahl: n von N Buchungen'" <| fun () ->
            Expect.equal (selectionSummaryText 12 34) "Auswahl: 12 von 34 Buchungen" "zwölf von 34"
            Expect.equal (selectionSummaryText 1 1) "Auswahl: 1 von 1 Buchung" "Einzahl"
            Expect.equal (selectionSummaryText 1234 12116) "Auswahl: 1.234 von 12.116 Buchungen" "mit Tausenderpunkt"

        testCase "Summenzeile zeigt bei leerer Auswahl den Treffer, sonst die Auswahl (Quelltext)" <| fun () ->
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "if hasSelection model then selectionSummaryRow model else summaryRow accounts categories model"
                "Auswahl statt Treffer, solange die Auswahl nicht leer ist"
            Expect.stringContains view "summaryOrSelectionRow accounts categories model" "die Spalte benutzt diese Umschaltung"

        testCase "Leiste: 'n ausgewählt', Alle und Auswahl aufheben" <| fun () ->
            Expect.equal (selectionBarCountText 12) "12 ausgewählt" "Anzahl"
            Expect.equal selectionAllLabel "Alle" "Alle"
            Expect.equal selectionClearLabel "Auswahl aufheben" "Ausstieg"

        testCase "Handy-Leiste hat Alle, PC-Leiste nicht; als schreibende Aktion nur Geprüft (Quelltext)" <| fun () ->
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "MultiSelect.fs" ]
            let slice (header: string) =
                let start = ds.IndexOf header
                Expect.isGreaterThan start -1 header
                let rest = ds.Substring start
                let next = rest.IndexOf("\nlet ", 5)
                if next < 0 then rest else rest.Substring(0, next)
            let mobile = slice "let barMobile"
            let desktop = slice "let barDesktop"
            Expect.stringContains mobile "ChipBar.chip" "Alle ist ein Chip mit Aktivzustand"
            Expect.stringContains mobile "allLabel" "Alle in der Handy-Leiste"
            Expect.isFalse (desktop.Contains "allLabel") "kein Alle in der PC-Leiste"
            for bar in [ mobile; desktop ] do
                for word in [ "Kategorie ändern"; "Flag"; "Ungeprüft" ] do
                    Expect.isFalse (bar.Contains word) $"'{word}' gehört nicht in die Leiste"
                Expect.stringContains bar "reviewedButton" "Geprüft ist die primäre Aktion der Leiste"
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            let from = view.IndexOf "let selectionBars"
            let barView = view.Substring(from, view.IndexOf("let private selectionBarSpacers") - from)
            for word in [ "\"Kategorie ändern\""; "\"Flag\""; "\"Ungeprüft\"" ] do
                Expect.isFalse (barView.Contains word) $"{word} wird in der Leiste nicht gesetzt"

        testCase "Die Tab-Bar weicht der Handy-Leiste im Auswahl-Modus (Quelltext)" <| fun () ->
            let view = readRepo [ "src"; "Client"; "View.fs" ]
            Expect.stringContains view "model.CurrentPage = Ledger && model.Ledger.SelectionMode" "Tab-Bar ausgeblendet"
            Expect.stringContains view "Components.Ledger.View.selectionBars"
                "die fixe Leiste steht außerhalb der Seitenhülle mit dem Transform (animate-page-enter)"

        testCase "Der Anker trägt die Teal-Kante, andere gewählte Zeilen nicht" <| fun () ->
            let edge = "shadow-[inset_2px_0_0_0_var(--color-neon-teal)]"
            let anchor = Components.Ledger.View.selectableRowClassName true true
            let picked = Components.Ledger.View.selectableRowClassName true false
            let plain = Components.Ledger.View.selectableRowClassName false false
            Expect.stringContains anchor edge "der Anker"
            Expect.isFalse (picked.Contains edge) "eine gewählte Zeile ohne Anker"
            Expect.isFalse (plain.Contains edge) "eine ungewählte Zeile"
            Expect.stringContains picked "bg-neon-teal/[0.07]" "gewählt trägt blasses Teal"
            Expect.isFalse (plain.Contains "bg-neon-teal") "ungewählt nicht"

        testCase "Kästchen: drei Zustände, 20px am Handy und 17px am PC" <| fun () ->
            let cls = Client.DesignSystem.MultiSelect.boxClassName
            Expect.stringContains (cls Client.DesignSystem.MultiSelect.Checked true) "bg-neon-teal" "gesetzt in Teal"
            Expect.stringContains (cls Client.DesignSystem.MultiSelect.Partial false) "bg-neon-teal/20" "Teilauswahl auf blassem Teal"
            Expect.isFalse ((cls Client.DesignSystem.MultiSelect.Unchecked true).Contains "neon-teal") "leer ohne Teal"
            Expect.stringContains (cls Client.DesignSystem.MultiSelect.Unchecked true) "size-5" "Handy 20px"
            Expect.stringContains (cls Client.DesignSystem.MultiSelect.Unchecked false) "size-[17px]" "PC 17px"

        testCase "Im Auswahl-Modus ist die Wisch-Geste aus (Quelltext)" <| fun () ->
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            let start = view.IndexOf "let private mobileEntryRow"
            let body = view.Substring(start, 1500)
            Expect.stringContains body "if selectionMode then" "der Modus entscheidet"
            let selectionBranch = body.Substring(0, body.IndexOf "else")
            Expect.isFalse (selectionBranch.Contains "swipeableEntryRow") "im Modus keine Wisch-Zeile"
    ]

[<Tests>]
let escHintTests =
    testList "PC-Leiste: Esc aufheben (ledger-w4vte)" [
        testCase "die PC-Leiste trägt 'Esc aufheben', die Handy-Leiste nicht (Quelltext)" <| fun () ->
            Expect.equal selectionClearKeyHint "Esc aufheben" "Text"
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "MultiSelect.fs" ]
            let from = ds.IndexOf "let barDesktop"
            let desktop = ds.Substring from
            Expect.stringContains desktop "clearKeyHint" "PC-Leiste setzt den Hinweis"
            let mobileFrom = ds.IndexOf "let barMobile"
            let mobile = ds.Substring(mobileFrom, from - mobileFrom)
            Expect.isFalse (mobile.Contains "clearKeyHint") "kein Hinweis am Handy"
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "selectionClearKeyHint" "die Seite reicht den Text an die Leiste"
    ]
