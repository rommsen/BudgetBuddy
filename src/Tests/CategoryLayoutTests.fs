module CategoryLayoutTests

// Kategorie deaktivieren und eigene Reihenfolge (budgeting-sn2sn, ADR 0088 Punkte 2 und 3): die reinen Regeln
// (Deaktivieren erlaubt, Sichtbarkeit je Monat, Reihenfolge je Gruppe), BBs Zustand in `budget_category_layout`
// über die Handler mit In-Memory-SQLite, die Optionen der Kategorie-Picker und der Rahmen der Budget-Seite.
// Kein Netz: YNAB kommt als Fake.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Categories

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private run (a: Async<'T>) = Async.RunSynchronously a

let private alltag = Guid.Parse "aaaaaaaa-0000-0000-0000-000000000001"
let private freizeit = Guid.Parse "aaaaaaaa-0000-0000-0000-000000000002"
let private lebensmittel = Guid.Parse "bbbbbbbb-0000-0000-0000-000000000001"
let private drogerie = Guid.Parse "bbbbbbbb-0000-0000-0000-000000000002"
let private apotheke = Guid.Parse "bbbbbbbb-0000-0000-0000-000000000003"
let private restaurant = Guid.Parse "bbbbbbbb-0000-0000-0000-000000000004"
let private hobby = Guid.Parse "bbbbbbbb-0000-0000-0000-000000000005"

let private november = "2026-11-01"
let private december = "2026-12-01"
let private now = DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc)

// ============================================
// Rein: Deaktivieren erlaubt?
// ============================================

[<Tests>]
let deactivationCheckTests =
    testList "Kategorie deaktivieren: erlaubt nur ohne Geld (ADR 0088 Punkt 2)" [
        testCase "0 verfügbar und keine künftige Zuweisung: erlaubt" <| fun () ->
            Expect.equal (deactivationCheck "Restaurant" (eur 0m) []) (Ok ()) "erlaubt"
            Expect.equal (deactivationCheck "Restaurant" (eur 0m) [ december, eur 0m ]) (Ok ()) "0 im Dezember zählt nicht"

        testCase "verfügbares Geld: abgelehnt mit Betrag" <| fun () ->
            Expect.equal
                (deactivationCheck "Restaurant" (eur 24.5m) [])
                (Error "Restaurant hat noch 24,50 €. Erst verschieben, dann deaktivieren.")
                "Meldung wie im Prototyp"

        testCase "überzogen: abgelehnt mit Betrag" <| fun () ->
            Expect.equal
                (deactivationCheck "Restaurant" (eur -12m) [])
                (Error "Restaurant ist mit 12,00 € überzogen. Erst ausgleichen, dann deaktivieren.")
                "negativ"

        testCase "Zuweisung in einem künftigen Monat: abgelehnt mit Monat und Betrag" <| fun () ->
            Expect.equal
                (deactivationCheck "Restaurant" (eur 0m) [ december, eur 50m ])
                (Error "Restaurant hat im Dezember 2026 50,00 € zugewiesen. Erst zurücknehmen, dann deaktivieren.")
                "künftig"

        testCase "mehrere Gründe stehen alle in der Meldung" <| fun () ->
            match deactivationCheck "Restaurant" (eur 1m) [ "2027-01-01", eur 5m; december, eur 3m ] with
            | Error message ->
                Expect.stringContains message "1,00 €" "verfügbar"
                Expect.stringContains message "Dezember 2026 3,00 €" "Dezember"
                Expect.stringContains message "Januar 2027 5,00 €" "Januar"
                Expect.isLessThan (message.IndexOf "Dezember") (message.IndexOf "Januar") "nach Monat"
            | Ok () -> failtest "abgelehnt erwartet"
    ]

// ============================================
// Rein: Sichtbarkeit in der Budget-Liste
// ============================================

let private budgetCategory (id: Guid) (name: string) (activity: decimal) (available: decimal) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur activity; Available = eur available
      Underfunded = None; Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

let private monthOf (groups: (string * BudgetCategory list) list) : BudgetMonth =
    { Month = november; ToBeBudgeted = eur 0m; Groups = groups |> List.map (fun (name, cs) -> { Name = name; Categories = cs }) }

let private shownIds (month: BudgetMonth) =
    month.Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> c.Id))

[<Tests>]
let visibilityTests =
    testList "Budget-Liste: deaktivierte Kategorien je Monat" [
        testCase "deaktiviert ohne Aktivität und ohne Verfügbares fehlt, die leere Gruppe auch" <| fun () ->
            let month =
                monthOf [ "Alltag", [ budgetCategory lebensmittel "Lebensmittel" -10m 5m ]
                          "Freizeit", [ budgetCategory restaurant "Restaurant" 0m 0m ] ]
            let shown = applyDeactivation (Set.ofList [ restaurant ]) month
            Expect.equal (shownIds shown) [ lebensmittel ] "nur Lebensmittel"
            Expect.equal (shown.Groups |> List.map (fun g -> g.Name)) [ "Alltag" ] "Freizeit ohne Kategorie fällt weg"

        testCase "deaktiviert mit Aktivität oder mit Verfügbarem erscheint, markiert als deaktiviert" <| fun () ->
            let month =
                monthOf [ "Freizeit", [ budgetCategory restaurant "Restaurant" -8m 0m; budgetCategory hobby "Hobby" 0m 3m ]
                          "Alltag", [ budgetCategory lebensmittel "Lebensmittel" 0m 0m ] ]
            let shown = applyDeactivation (Set.ofList [ restaurant; hobby ]) month
            let all = shown.Groups |> List.collect (fun g -> g.Categories)
            Expect.equal (all |> List.map (fun c -> c.Id)) [ restaurant; hobby; lebensmittel ] "alle drei"
            Expect.isTrue (all |> List.filter (fun c -> c.Id <> lebensmittel) |> List.forall isDeactivated) "markiert"
            Expect.isFalse (all |> List.find (fun c -> c.Id = lebensmittel) |> isDeactivated) "aktive ohne Marke"
            Expect.equal (all |> List.find (fun c -> c.Id = lebensmittel)).Deactivated None "aktiv bleibt None"
    ]

// ============================================
// Rein: Reihenfolge je Gruppe
// ============================================

[<Tests>]
let orderTests =
    testList "Reihenfolge je Gruppe (ADR 0088 Punkt 3)" [
        testCase "hoch oben und runter unten ohne Wirkung, sonst tauschen Nachbarn" <| fun () ->
            let order = [ lebensmittel; drogerie; apotheke ]
            Expect.equal (moveWithin StepUp lebensmittel order) order "oben hoch"
            Expect.equal (moveWithin StepDown apotheke order) order "unten runter"
            Expect.equal (moveWithin StepUp drogerie order) [ drogerie; lebensmittel; apotheke ] "hoch"
            Expect.equal (moveWithin StepDown drogerie order) [ lebensmittel; apotheke; drogerie ] "runter"
            Expect.equal (moveWithin StepDown restaurant order) order "unbekannt"

        testCase "neue Kategorien aus YNAB stehen am Ende ihrer Gruppe" <| fun () ->
            Expect.equal
                (orderWithin [ drogerie; lebensmittel ] [ lebensmittel; apotheke; drogerie ])
                [ drogerie; lebensmittel; apotheke ]
                "Apotheke ist neu"

        testCase "unbekannte Ids der gespeicherten Reihenfolge werden ignoriert" <| fun () ->
            Expect.equal
                (orderWithin [ restaurant; drogerie; Guid.NewGuid(); lebensmittel; drogerie ] [ lebensmittel; drogerie ])
                [ drogerie; lebensmittel ]
                "nur was in der Gruppe steht, ohne Dubletten"

        testCase "Gruppenwechsel: die Kategorie steht am Ende der neuen Gruppe und fehlt in der alten" <| fun () ->
            // Restaurant war in Alltag gemerkt, steht in YNAB jetzt in Freizeit.
            let layout =
                { emptyCategoryLayout with
                    Orders = Map.ofList [ alltag, [ restaurant; drogerie; lebensmittel ]; freizeit, [ hobby ] ] }
            let placements = [ alltag, lebensmittel; alltag, drogerie; freizeit, restaurant; freizeit, hobby ]
            Expect.equal (flatCategoryOrder layout placements) [ drogerie; lebensmittel; hobby; restaurant ] "flach"

        testCase "die Seite bekommt die Gruppen in BBs Reihenfolge mit Aktiv" <| fun () ->
            let layout =
                { Deactivated = Set.ofList [ drogerie ]
                  Orders = Map.ofList [ alltag, [ drogerie; lebensmittel ] ] }
            let groups : ManagedCategoryGroup list =
                [ { Id = alltag
                    Name = "Alltag"
                    Categories = [ { Id = lebensmittel; Name = "Lebensmittel"; Active = true }; { Id = drogerie; Name = "Drogerie"; Active = true } ] } ]
            Expect.equal
                (arrangeGroups layout groups)
                [ { Id = alltag
                    Name = "Alltag"
                    Categories = [ { Id = drogerie; Name = "Drogerie"; Active = false }; { Id = lebensmittel; Name = "Lebensmittel"; Active = true } ] } ]
                "Drogerie oben und deaktiviert"
    ]

// ============================================
// Handler mit In-Memory-SQLite
// ============================================

let private ynabGroups : ManagedCategoryGroup list =
    [ { Id = alltag
        Name = "Alltag"
        Categories =
          [ { Id = lebensmittel; Name = "Lebensmittel"; Active = true }
            { Id = drogerie; Name = "Drogerie"; Active = true }
            { Id = apotheke; Name = "Apotheke"; Active = true } ] }
      { Id = freizeit; Name = "Freizeit"; Categories = [ { Id = restaurant; Name = "Restaurant"; Active = true } ] } ]

let private rawCategory (id: Guid) (name: string) (group: string) (assigned: decimal) (activity: decimal) (available: decimal) : YnabMonthCategory =
    { Id = id; Name = name; GroupName = group; Assigned = eur assigned; Activity = eur activity; Available = eur available
      Target = None; Hidden = false; Deleted = false; Internal = false }

let private rawMonth (month: string) (categories: YnabMonthCategory list) : YnabMonth =
    { Month = month; ToBeBudgeted = eur 0m; Categories = categories }

let private quietMonth (month: string) =
    rawMonth month
        [ rawCategory lebensmittel "Lebensmittel" "Alltag" 0m 0m 0m
          rawCategory drogerie "Drogerie" "Alltag" 0m 0m 0m
          rawCategory apotheke "Apotheke" "Alltag" 0m 0m 0m
          rawCategory restaurant "Restaurant" "Freizeit" 0m 0m 0m ]

/// Ein Port, der YNAB nur liest: Gruppen und Monate aus den Vorgaben, jedes Schreiben ist ein Fehler.
let private portWith (months: Map<string, YnabMonth>) : CategoryPort =
    { FetchGroups = fun () -> async { return Ok ynabGroups }
      CreateCategory = fun _ _ -> failwith "kein Schreiben nach YNAB"
      UpdateCategory = fun _ _ -> failwith "kein Schreiben nach YNAB"
      CreateGroup = fun _ -> failwith "kein Schreiben nach YNAB"
      UpdateGroup = fun _ _ -> failwith "kein Schreiben nach YNAB"
      RefreshMirror = fun () -> failwith "kein Spiegel"
      FetchMonth = fun month -> async { return months |> Map.tryFind month |> Option.map Ok |> Option.defaultValue (Error (BudgetNotFound month)) } }

let private quietPort = portWith (Map.ofList [ november, quietMonth november ])

let private readGroups (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) =
    match groupsWith connect quietPort |> run with
    | Ok groups -> groups
    | Error e -> failtest $"Gruppen erwartet: {e}"

let private categoryIn (groups: ManagedCategoryGroup list) (id: Guid) =
    groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Id = id)

let private idsOf (groups: ManagedCategoryGroup list) (groupId: Guid) =
    (groups |> List.find (fun g -> g.Id = groupId)).Categories |> List.map (fun c -> c.Id)

[<Tests>]
let handlerTests =
    testList "Deaktivieren und Reihenfolge: Handler mit In-Memory-SQLite" [
        testCase "Deaktivieren und Aktivieren überleben einen neuen Lese-Durchgang" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            Expect.isTrue (categoryIn (readGroups connect) restaurant).Active "vorher aktiv"
            let deactivated = setCategoryActiveWith connect quietPort november now { CategoryId = restaurant; Active = false } |> run
            Expect.equal deactivated (Ok ()) "deaktiviert"
            Expect.isFalse (categoryIn (readGroups connect) restaurant).Active "neu gelesen deaktiviert"
            Expect.isTrue (categoryIn (readGroups connect) lebensmittel).Active "die anderen bleiben aktiv"
            let activated = setCategoryActiveWith connect quietPort november now { CategoryId = restaurant; Active = true } |> run
            Expect.equal activated (Ok ()) "aktiviert"
            Expect.isTrue (categoryIn (readGroups connect) restaurant).Active "neu gelesen wieder aktiv"

        testCase "Verfügbares im laufenden Monat: abgelehnt mit Betrag, nichts gespeichert" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let port =
                portWith (Map.ofList [ november, rawMonth november [ rawCategory restaurant "Restaurant" "Freizeit" 30m -5.5m 24.5m ] ])
            let result = setCategoryActiveWith connect port november now { CategoryId = restaurant; Active = false } |> run
            Expect.equal result (Error (RefusedByBb "Restaurant hat noch 24,50 €. Erst verschieben, dann deaktivieren.")) "abgelehnt"
            Expect.isTrue (categoryIn (readGroups connect) restaurant).Active "bleibt aktiv"

        testCase "Zuweisung in einem künftigen Monat (BBs Soll): abgelehnt mit Monat und Betrag" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            // Der Dezember ist übernommen, darin hat Restaurant 50 € zugewiesen.
            let decemberRaw = rawMonth december [ rawCategory restaurant "Restaurant" "Freizeit" 50m 0m 50m ]
            let ynab : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok decemberRaw }
                  SetAssigned = fun _ _ _ -> failwith "kein Schreiben"
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            match CommandHandlers.observeMonthWith connect ynab now december |> run with
            | Ok _ -> ()
            | Error e -> failtest $"Übernahme erwartet: {e}"
            let result = setCategoryActiveWith connect quietPort november now { CategoryId = restaurant; Active = false } |> run
            Expect.equal
                result
                (Error (RefusedByBb "Restaurant hat im Dezember 2026 50,00 € zugewiesen. Erst zurücknehmen, dann deaktivieren."))
                "abgelehnt"
            Expect.isTrue (categoryIn (readGroups connect) restaurant).Active "bleibt aktiv"

        testCase "YNAB nicht lesbar: kein Deaktivieren" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let result = setCategoryActiveWith connect (portWith Map.empty) november now { CategoryId = restaurant; Active = false } |> run
            Expect.equal result (Error (BudgetNotFound november)) "YNAB-Fehler"
            Expect.isTrue (categoryIn (readGroups connect) restaurant).Active "bleibt aktiv"

        testCase "die Reihenfolge überlebt einen neuen Lese-Durchgang, Deaktivieren bleibt dabei" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            Expect.equal (idsOf (readGroups connect) alltag) [ lebensmittel; drogerie; apotheke ] "vorher YNAB"
            setCategoryActiveWith connect quietPort november now { CategoryId = drogerie; Active = false } |> run |> ignore
            let saved = setCategoryOrderWith connect now { GroupId = alltag; CategoryIds = [ apotheke; drogerie; lebensmittel ] } |> run
            Expect.equal saved (Ok ()) "gemerkt"
            let groups = readGroups connect
            Expect.equal (idsOf groups alltag) [ apotheke; drogerie; lebensmittel ] "neu gelesen in BBs Reihenfolge"
            Expect.isFalse (categoryIn groups drogerie).Active "Drogerie bleibt deaktiviert"
            // Eine zweite Reihenfolge ersetzt die erste.
            setCategoryOrderWith connect now { GroupId = alltag; CategoryIds = [ lebensmittel; apotheke ] } |> run |> ignore
            Expect.equal (idsOf (readGroups connect) alltag) [ lebensmittel; apotheke; drogerie ] "nicht genannt steht am Ende"

        testCase "eine ungültige Reihenfolge wird abgelehnt und nicht gespeichert" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let result = setCategoryOrderWith connect now { GroupId = alltag; CategoryIds = [ apotheke; apotheke ] } |> run
            Expect.isError result "doppelt"
            Expect.equal (idsOf (readGroups connect) alltag) [ lebensmittel; drogerie; apotheke ] "unverändert"

        testCase "der Kategorie-Spiegel überschreibt Deaktivieren und Reihenfolge nicht" <| fun () ->
            Ledger.Persistence.initializeLedgerSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            let connect = global.Persistence.getConnection
            let groupId = Guid.NewGuid()
            let first, second = Guid.NewGuid(), Guid.NewGuid()
            setCategoryOrderWith connect now { GroupId = groupId; CategoryIds = [ second; first ] } |> run |> ignore
            setCategoryActiveWith connect quietPort november now { CategoryId = first; Active = true } |> run |> ignore
            setCategoryActiveWith connect (portWith (Map.ofList [ november, rawMonth november [] ])) november now { CategoryId = second; Active = false }
            |> run
            |> ignore
            let mirrored : YnabCategory list =
                [ { Id = YnabCategoryId first; Name = "Erste neu"; GroupName = "G"; Available = None; Hidden = false }
                  { Id = YnabCategoryId second; Name = "Zweite neu"; GroupName = "G"; Available = None; Hidden = false } ]
            refreshMirrorWith (fun () -> async { return Ok mirrored }) Ledger.Persistence.upsertCategories |> run
            match Budgeting.Persistence.loadCategoryLayoutWith connect |> run with
            | Ok layout ->
                Expect.equal (layout.Orders |> Map.tryFind groupId) (Some [ second; first ]) "Reihenfolge bleibt"
                Expect.isTrue (layout.Deactivated.Contains second) "deaktiviert bleibt"
                Expect.isFalse (layout.Deactivated.Contains first) "aktiv bleibt"
            | Error message -> failtest message

        testCase "die Budget-Seite lässt eine deaktivierte Kategorie ohne Geld weg und zeigt eine mit Aktivität markiert" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setCategoryActiveWith connect quietPort november now { CategoryId = restaurant; Active = false } |> run |> ignore
            setCategoryActiveWith connect quietPort november now { CategoryId = drogerie; Active = false } |> run |> ignore
            let raw =
                rawMonth november
                    [ rawCategory lebensmittel "Lebensmittel" "Alltag" 100m -20m 80m
                      rawCategory drogerie "Drogerie" "Alltag" 0m -4m -4m
                      rawCategory restaurant "Restaurant" "Freizeit" 0m 0m 0m ]
            let ynab : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> failwith "kein Schreiben"
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            let ledger : CommandHandlers.LedgerActivityPort =
                { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
                  LoadFirstEntryDate = fun () -> async { return None } }
            match QueryHandlers.budgetMonthWithLedger ledger connect ynab now november |> run with
            | Ok month ->
                let all = month.Groups |> List.collect (fun g -> g.Categories)
                Expect.equal (all |> List.map (fun c -> c.Id)) [ lebensmittel; drogerie ] "Restaurant fehlt"
                Expect.isTrue (all |> List.find (fun c -> c.Id = drogerie) |> isDeactivated) "Drogerie markiert"
            | Error e -> failtest $"Monat erwartet: {e}"
    ]

// ============================================
// Abfragen: Picker und Rahmen
// ============================================

let private ynabCategory (id: Guid) (name: string) (group: string) : YnabCategory =
    { Id = YnabCategoryId id; Name = name; GroupName = group; Available = Some (eur 1m); Hidden = false }

let private placed : YnabCategory list * (Guid * Guid) list =
    [ ynabCategory lebensmittel "Lebensmittel" "Alltag"
      ynabCategory drogerie "Drogerie" "Alltag"
      ynabCategory apotheke "Apotheke" "Alltag"
      ynabCategory restaurant "Restaurant" "Freizeit"
      { ynabCategory hobby "Hobby" "Freizeit" with Hidden = true } ],
    [ alltag, lebensmittel; alltag, drogerie; alltag, apotheke; freizeit, restaurant; freizeit, hobby ]

[<Tests>]
let queryTests =
    testList "Kategorie-Picker und Rahmen in BBs Reihenfolge ohne deaktivierte" [
        testCase "die Picker-Optionen enthalten keine deaktivierte Kategorie und folgen der BB-Reihenfolge" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setCategoryActiveWith connect quietPort november now { CategoryId = drogerie; Active = false } |> run |> ignore
            setCategoryOrderWith connect now { GroupId = alltag; CategoryIds = [ apotheke; drogerie; lebensmittel ] } |> run |> ignore
            setCategoryOrderWith connect now { GroupId = freizeit; CategoryIds = [ hobby; restaurant ] } |> run |> ignore
            let options =
                pickerOptionsWith
                    (fun () -> async { return Ok placed })
                    (fun () -> async { return None })
                    (fun () -> Budgeting.Persistence.loadCategoryLayoutWith connect)
                |> run
            match options with
            | Ok categories ->
                Expect.equal
                    (categories |> List.map (fun c -> c.Name))
                    [ "Apotheke"; "Lebensmittel"; "Hobby"; "Restaurant" ]
                    "ohne Drogerie, BBs Reihenfolge, die in YNAB versteckte bleibt für Versteckt"
            | Error e -> failtest $"Optionen erwartet: {e}"

        testCase "ist BBs Zustand nicht lesbar, kommen die Optionen wie aus YNAB" <| fun () ->
            let options =
                pickerOptionsWith
                    (fun () -> async { return Ok placed })
                    (fun () -> async { return None })
                    (fun () -> async { return Error "kaputt" })
                |> run
            Expect.equal
                (options |> Result.map (List.map (fun c -> c.Name)))
                (Ok [ "Lebensmittel"; "Drogerie"; "Apotheke"; "Restaurant"; "Hobby" ])
                "YNABs Folge, nichts weg"

        testCase "der Rahmen der Budget-Seite trägt BBs Reihenfolge" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setCategoryOrderWith connect now { GroupId = alltag; CategoryIds = [ drogerie; lebensmittel ] } |> run |> ignore
            let order =
                bbCategoryOrderWith (fun () -> async { return Ok placed }) (fun () -> Budgeting.Persistence.loadCategoryLayoutWith connect)
                |> run
            Expect.equal order (Ok [ drogerie; lebensmittel; apotheke; restaurant; hobby ]) "Apotheke neu am Ende von Alltag"

        testCase "der YNAB-Decoder liefert Gruppe und Kategorie in YNABs Folge, auch ausgeblendete" <| fun () ->
            let body =
                $"""{{"data":{{"category_groups":[{{"id":"{alltag}","name":"Alltag","categories":[{{"id":"{lebensmittel}"}},{{"id":"{drogerie}","hidden":true}}]}},{{"id":"{freizeit}","name":"Leer"}}]}}}}"""
            Expect.equal
                (Thoth.Json.Net.Decode.fromString Server.YnabClient.categoryPlacementsDecoder body)
                (Ok [ alltag, lebensmittel; alltag, drogerie ])
                "Paare"
    ]
