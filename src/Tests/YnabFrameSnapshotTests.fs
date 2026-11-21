/// Der Rahmen der Budget-Seite aus BB (budgeting-yx51n, ADR 0101 Punkt 3): Monatsfenster und Platzierung der
/// Kategorien als YNAB-Stand, das Lesen des Rahmens ohne YNAB-Aufruf und das Öffnen des nächsten Monats nach einer
/// bestätigten Zuweisung im letzten Monat (ADR 0074 Punkt 4).
module YnabFrameSnapshotTests

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Domain

let private run x = Async.RunSynchronously x

let private today = DateOnly(2026, 11, 20)

let private now = DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc)

/// Zählt die YNAB-Aufrufe des Rahmens.
type private Calls = { mutable Months: int; mutable Placements: int }

/// Ein Rahmen-Port auf der Datenbank `connect` (Stand, BBs Layout), YNAB als zählender Fake mit festen Antworten.
let private countingPortOn
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (months: string list)
    (placements: (Guid * Guid) list)
    : QueryHandlers.BudgetFramePort * Calls =
    let calls = { Months = 0; Placements = 0 }
    let port : QueryHandlers.BudgetFramePort =
        { FetchMonths = fun () -> async { calls.Months <- calls.Months + 1; return Ok months }
          FetchCategoryOrder = fun () -> async { calls.Placements <- calls.Placements + 1; return Ok ([], placements) }
          LoadMonthWindow = Budgeting.Persistence.loadMonthWindowWith connect
          HoldMonthWindow = Budgeting.Persistence.holdMonthWindowWith connect
          LoadCategoryCatalog = Budgeting.Persistence.loadCategoryCatalogWith connect
          HoldCategoryCatalog = Budgeting.Persistence.holdCategoryCatalogWith connect
          LoadCategoryLayout = fun () -> Budgeting.Persistence.loadCategoryLayoutWith connect
          LoadMonthStartHidden = fun _ -> async { return Ok [] } }
    port, calls

let private frameOk (port: QueryHandlers.BudgetFramePort) : BudgetFrame =
    match QueryHandlers.frame port today now |> run with
    | Ok frame -> frame
    | Error e -> failtestf "Rahmen erwartet: %A" e

let private hold (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (months: string list) (placements: (Guid * Guid) list) =
    use conn = connect ()
    Budgeting.Persistence.holdMonthWindowOn conn { Months = months; FetchedAt = now.AddHours -1.0 }
    Budgeting.Persistence.holdCategoryCatalogOn conn { Placements = placements; FetchedAt = now.AddHours -1.0 }

// ============================================
// Reine Regel: welcher Monat öffnet sich (ADR 0074 Punkt 4)
// ============================================

let private domainTests =
    let window = [ "2026-12-01"; "2026-10-01"; "2026-11-01" ]
    testList "Eine Zuweisung öffnet den nächsten Monat" [
        testCase "Eine bestätigte Zuweisung ungleich 0 im letzten Monat öffnet den nächsten" <| fun () ->
            Expect.equal (monthOpenedBy window "2026-12-01" 50_000L Written) (Some "2027-01-01") "Januar über den Jahreswechsel"
            Expect.equal (monthOpenedBy window "2026-12-01" -10L Written) (Some "2027-01-01") "auch ein negativer Betrag"
        testCase "Eine Zuweisung von 0 im letzten Monat öffnet keinen" <| fun () ->
            Expect.equal (monthOpenedBy window "2026-12-01" 0L Written) None "0"
        testCase "Eine nicht bestätigte Zuweisung im letzten Monat öffnet keinen" <| fun () ->
            Expect.equal (monthOpenedBy window "2026-12-01" 50_000L (Failed "Rate-Limit")) None "fehlgeschlagen"
            Expect.equal (monthOpenedBy window "2026-12-01" 50_000L ChangedInYnab) None "in YNAB geändert"
            Expect.equal (monthOpenedBy window "2026-12-01" 50_000L WithdrawnInBb) None "in BB zurückgezogen"
        testCase "Eine bestätigte Zuweisung in einem früheren Monat öffnet keinen" <| fun () ->
            Expect.equal (monthOpenedBy window "2026-11-01" 50_000L Written) None "November ist nicht der letzte"
    ]

// ============================================
// Rahmen lesen (eigene Datenbank je Fall)
// ============================================

let private handlerTests =
    testList "Rahmen aus dem YNAB-Stand" [
        testCase "Mit gehaltenem Stand fragt der Rahmen YNAB nicht" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let g, a, b = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            hold connect [ "2026-11-01"; "2026-12-01" ] [ g, a; g, b ]
            let port, calls = countingPortOn connect [ "1999-01-01" ] []

            let frame = frameOk port

            Expect.equal calls.Months 0 "kein FetchMonths"
            Expect.equal calls.Placements 0 "kein FetchCategoryOrder"
            Expect.equal frame.Months [ "2026-11-01"; "2026-12-01" ] "Fenster aus dem Stand"
            Expect.equal frame.CategoryOrder [ a; b ] "Reihenfolge aus dem Stand"

        testCase "Ohne Stand liest der Rahmen YNAB je einmal und danach nicht mehr" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let g, a = Guid.NewGuid(), Guid.NewGuid()
            let port, calls = countingPortOn connect [ "2026-12-01"; "2026-11-01" ] [ g, a ]

            let first = frameOk port
            Expect.equal (calls.Months, calls.Placements) (1, 1) "je ein Aufruf"
            Expect.equal first.Months [ "2026-11-01"; "2026-12-01" ] "aus YNAB"

            let second = frameOk port
            Expect.equal (calls.Months, calls.Placements) (1, 1) "danach keiner"
            Expect.equal second first "derselbe Rahmen aus dem Stand"
            use conn = connect ()
            Expect.equal
                (Budgeting.Persistence.loadMonthWindowOn conn |> Option.map (fun w -> w.FetchedAt))
                (Some now)
                "Zeitpunkt des Lesens festgehalten"

        testCase "Ist der Stand nicht lesbar, liest der Rahmen YNAB" <| fun () ->
            let calls = ref 0
            let port =
                { BudgetEventsFixture.framePortOf [ "2026-11-01" ] [] with
                    FetchMonths = fun () -> async { calls.Value <- calls.Value + 1; return Ok [ "2026-11-01" ] }
                    LoadMonthWindow = fun () -> async { return Error Budgeting.Persistence.YnabFrameUnreadableText } }
            Expect.equal (frameOk port).Months [ "2026-11-01" ] "aus YNAB"
            Expect.equal calls.Value 1 "ein Aufruf"

        testCase "Eine neue Kategorie im Katalog steht am Ende ihrer Gruppe" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let g, h = Guid.NewGuid(), Guid.NewGuid()
            let a, b, neu, x = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            do
                use conn = connect ()
                Budgeting.Persistence.saveCategoryOrderOn conn g [ b; a ] now
            // In YNABs Folge steht die neue Kategorie vorn in ihrer Gruppe.
            hold connect [ "2026-11-01" ] [ g, neu; g, a; g, b; h, x ]
            let port, _ = countingPortOn connect [] []

            Expect.equal (frameOk port).CategoryOrder [ b; a; neu; x ] "BBs Reihenfolge, die neue am Ende der Gruppe"

        testCase "Eine bestätigte Zuweisung im letzten Monat öffnet den nächsten im gehaltenen Fenster, eine fehlgeschlagene nicht" <| fun () ->
            let anchor, connect = BudgetEventsFixture.isolatedDb ()
            use _ = anchor
            let last = "2026-12-01"
            hold connect [ "2026-11-01"; last ] []
            let line amount : Budgeting.Persistence.PlannedLine =
                { CategoryId = Guid.NewGuid(); CategoryName = "Urlaub"; OldAssigned = 0L; NewAssigned = amount }
            let recorded =
                Budgeting.Persistence.recordDecisionWith connect None now last MoneyAssigned [ line 50_000L ] [] []
                |> run
                |> BudgetEventsFixture.recordedOrFail
            let port, calls = countingPortOn connect [] []

            Budgeting.Persistence.recordWriteBackWith connect recorded.Head now (Budgeting.Events.WriteBackFailed "Rate-Limit") |> run
            Expect.equal (frameOk port).Months [ "2026-11-01"; last ] "fehlgeschlagen: kein neuer Monat"

            Budgeting.Persistence.recordWriteBackWith connect recorded.Head now Budgeting.Events.WrittenBack |> run
            Expect.equal (frameOk port).Months [ "2026-11-01"; last; "2027-01-01" ] "bestätigt: der Januar ist offen"
            Expect.equal calls.Months 0 "ohne YNAB-Aufruf"
    ]

// ============================================
// Live-Port des Rahmens (Doktrin Punkt 3)
// ============================================

let private liveTests =
    // Geteilt: die Datenbank der Anwendung. Fenster und Platzierung sind dort je einmal gehalten (Kopfzeile `id = 1`);
    // nur dieser Fall schreibt sie, mit Monaten weit in der Zukunft, die kein anderer Test zuweist.
    testSequencedGroup TestGroups.InMemoryDb
    <| testList "Live-Port des Rahmens" [
        testCase "liveFramePort hält Fenster und Platzierung in der Datenbank der Anwendung und liest BBs Reihenfolge von dort" <| fun () ->
            Budgeting.Persistence.initializeBudgetingSchema ()
            let port = Server.Api.liveFramePort "kein-token" (YnabBudgetId "kein-budget")
            let g, a, b = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let window : YnabMonthWindow = { Months = [ "2097-01-01"; "2097-02-01" ]; FetchedAt = now }
            let catalog : YnabCategoryCatalog = { Placements = [ g, a; g, b ]; FetchedAt = now }

            Expect.equal (port.HoldMonthWindow window |> run) (Ok ()) "Fenster gehalten"
            Expect.equal (port.HoldCategoryCatalog catalog |> run) (Ok ()) "Platzierung gehalten"
            do
                use conn = global.Persistence.getConnection ()
                Expect.equal (Budgeting.Persistence.loadMonthWindowOn conn) (Some window) "Fenster in der Datenbank der Anwendung"
                Expect.equal (Budgeting.Persistence.loadCategoryCatalogOn conn) (Some catalog) "Platzierung in der Datenbank der Anwendung"
                Budgeting.Persistence.saveCategoryOrderOn conn g [ b; a ] now
            Expect.equal (port.LoadMonthWindow () |> run) (Ok (Some window)) "der Port liest das Fenster zurück"
            Expect.equal (port.LoadCategoryCatalog () |> run) (Ok (Some catalog)) "der Port liest die Platzierung zurück"
            match port.LoadCategoryLayout () |> run with
            | Ok layout -> Expect.equal (layout.Orders |> Map.tryFind g) (Some [ b; a ]) "BBs Reihenfolge aus derselben Datenbank"
            | Error e -> failtest e
    ]

[<Tests>]
let tests = testList "Rahmen der Budget-Seite aus BB (yx51n)" [ domainTests; handlerTests; liveTests ]
