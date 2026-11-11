module RoutingTests

open Expecto
open Types

// design-system-k6kfw: `/` ist die Übersicht, der Sync-Ablauf liegt unter `/sync`, unbekannte Pfade landen auf der
// Übersicht. Je Seite ein Weg hin und zurück, dazu die Zuordnung Seite → Ort und das Ziel von „Zurück“ aus den
// Einstellungen.

let private entryId = System.Guid.Parse "00000000-0000-0000-0000-000000000042"

/// Jede Seite mit ihren Segmenten. Ein neuer Fall in `Page` ohne Zeile hier fällt in `allPagesCovered` auf.
let private routes : (Page * string list) list =
    [ Overview, []
      SyncFlow, [ "sync" ]
      Rules, [ "rules" ]
      Settings, [ "settings" ]
      Styleguide, [ "styleguide" ]
      QuickAdd, [ "quickadd" ]
      Ledger, [ "ledger" ]
      LedgerEntry entryId, [ "ledger"; "entry"; string entryId ]
      Budget, [ "budget" ]
      BudgetHistory, [ "budget"; "history" ]
      BudgetTargetCheck, [ "budget"; "target-check" ]
      BudgetCategories, [ "budget"; "categories" ] ]

let private caseName (page: Page) = (Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(page, typeof<Page>) |> fst).Name

[<Tests>]
let routingTests =
    testList "Routing" [
        testList "parseUrl und toUrlSegments je Seite" [
            for page, segments in routes do
                testCase $"{caseName page}: Segmente {segments} hin und zurück" <| fun () ->
                    Expect.equal (Routing.toUrlSegments page) segments "toUrlSegments"
                    Expect.equal (Routing.parseUrl segments) page "parseUrl"
        ]

        testCase "Jeder Fall von Page steht in der Tabelle" <| fun () ->
            let all =
                Microsoft.FSharp.Reflection.FSharpType.GetUnionCases typeof<Page> |> Array.map (fun c -> c.Name) |> Set.ofArray
            let covered = routes |> List.map (fst >> caseName) |> Set.ofList
            Expect.equal covered all "jede Seite hat eine Route"

        testCase "/ ist die Übersicht und /sync der Sync-Ablauf, nicht vertauscht" <| fun () ->
            Expect.equal (Routing.parseUrl []) Overview "/"
            Expect.equal (Routing.parseUrl [ "sync" ]) SyncFlow "/sync"
            Expect.equal (Routing.toUrlSegments Overview) [] "Übersicht"
            Expect.equal (Routing.toUrlSegments SyncFlow) [ "sync" ] "Sync-Ablauf"

        testCase "Ein unbekannter Pfad landet auf der Übersicht" <| fun () ->
            Expect.equal (Routing.parseUrl [ "unbekannt" ]) Overview "ein Segment"
            Expect.equal (Routing.parseUrl [ "foo"; "bar"; "baz" ]) Overview "mehrere Segmente"
    ]

[<Tests>]
let placeTests =
    testList "Ort einer Seite" [
        testCase "Jede Seite hängt unter ihrem Ort, Einstellungen und Styleguide unter keinem" <| fun () ->
            let expected =
                [ Overview, Some OverviewPlace
                  SyncFlow, Some OverviewPlace
                  Ledger, Some LedgerPlace
                  LedgerEntry entryId, Some LedgerPlace
                  Rules, Some LedgerPlace
                  Budget, Some BudgetPlace
                  BudgetHistory, Some BudgetPlace
                  BudgetTargetCheck, Some BudgetPlace
                  BudgetCategories, Some BudgetPlace
                  QuickAdd, Some QuickAddPlace
                  Settings, None
                  Styleguide, None ]
            for page, place in expected do
                Expect.equal (placeOf page) place (caseName page)

        testCase "Ein Ort führt zu seiner Seite und markiert sich dort selbst" <| fun () ->
            for place in [ OverviewPlace; LedgerPlace; BudgetPlace; QuickAddPlace ] do
                Expect.equal (placeOf (placePage place)) (Some place) (string place)
    ]

[<Tests>]
let settingsReturnTests =
    testList "Zurück aus den Einstellungen" [
        testCase "Wer aus dem Budget in die Einstellungen geht, kommt ins Budget zurück" <| fun () ->
            Expect.equal (settingsReturn Budget Settings Overview) Budget "Budget gemerkt"

        testCase "Der Styleguide unter den Einstellungen ändert das Ziel nicht" <| fun () ->
            let afterSettings = settingsReturn (LedgerEntry entryId) Settings Overview
            let afterStyleguide = settingsReturn Settings Styleguide afterSettings
            let backToSettings = settingsReturn Styleguide Settings afterStyleguide
            Expect.equal backToSettings (LedgerEntry entryId) "die Buchung bleibt das Ziel"

        testCase "Ein Wechsel zwischen anderen Seiten lässt das Ziel stehen" <| fun () ->
            Expect.equal (settingsReturn Budget Ledger Rules) Rules "unverändert"
    ]
