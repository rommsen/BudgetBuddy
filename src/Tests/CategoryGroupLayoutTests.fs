module CategoryGroupLayoutTests

// Kategoriegruppe deaktivieren (budgeting-nyx75, Nachtrag ADR 0088 vom 2026-11-20): die reinen Regeln (wirksam
// deaktiviert, Prüfung der Gruppe, Budget-Liste), BBs Zustand in `budget_category_group_layout` über den Handler mit
// In-Memory-SQLite, Picker, Budget-Seite, Targets anwenden und die Prüfung an der Grenze. Kein Netz: YNAB kommt als
// zählender Fake.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting
open Server.Budgeting.Categories

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private run (a: Async<'T>) = Async.RunSynchronously a

let private wohnen = Guid.Parse "cccccccc-0000-0000-0000-000000000001"
let private freizeit = Guid.Parse "cccccccc-0000-0000-0000-000000000002"
let private miete = Guid.Parse "dddddddd-0000-0000-0000-000000000001"
let private strom = Guid.Parse "dddddddd-0000-0000-0000-000000000002"
let private internet = Guid.Parse "dddddddd-0000-0000-0000-000000000003"
let private kino = Guid.Parse "dddddddd-0000-0000-0000-000000000004"
let private sportverein = Guid.Parse "dddddddd-0000-0000-0000-000000000005"
let private zeitschrift = Guid.Parse "dddddddd-0000-0000-0000-000000000006"

let private november = "2026-11-01"
let private december = "2026-12-01"
let private now = DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc)

let private layoutOf (deactivated: Guid list) (groups: Guid list) : CategoryLayout =
    { emptyCategoryLayout with Deactivated = Set.ofList deactivated; DeactivatedGroups = Set.ofList groups }

// ============================================
// Rein: wirksam deaktiviert
// ============================================

[<Tests>]
let effectiveTests =
    testList "Wirksam deaktiviert: eigene Kategorie oder ihre Gruppe" [
        testCase "eigene Flags und alle Kategorien einer deaktivierten Gruppe" <| fun () ->
            let placements = [ wohnen, miete; wohnen, strom; freizeit, kino; freizeit, sportverein ]
            Expect.equal
                (effectiveDeactivated (layoutOf [ kino ] [ wohnen ]) placements)
                (Set.ofList [ miete; strom; kino ])
                "Wohnen ganz, aus Freizeit nur Kino"

        testCase "eine Kategorie, die in die deaktivierte Gruppe kommt, ist deaktiviert, eine, die sie verlässt, gilt nach ihrem Flag" <| fun () ->
            let layout = layoutOf [ strom ] [ wohnen ]
            // Vorher: Miete und Strom in Wohnen, Kino in Freizeit. Danach hat YNAB Kino nach Wohnen geschoben und Miete
            // und Strom nach Freizeit.
            Expect.isFalse ((effectiveDeactivated layout [ wohnen, miete; wohnen, strom; freizeit, kino ]).Contains kino) "vorher aktiv"
            let after = effectiveDeactivated layout [ wohnen, kino; freizeit, miete; freizeit, strom ]
            Expect.isTrue (after.Contains kino) "in die Gruppe gekommen: deaktiviert"
            Expect.isFalse (after.Contains miete) "verlassen, eigenes Flag aktiv"
            Expect.isTrue (after.Contains strom) "verlassen, eigenes Flag deaktiviert"

        testCase "eine deaktivierte Gruppe ohne Placements ändert nichts" <| fun () ->
            Expect.equal (effectiveDeactivated (layoutOf [ kino ] [ wohnen ]) [ freizeit, kino; freizeit, sportverein ]) (Set.ofList [ kino ]) "nur Kino"

        testCase "der Monat liefert Gruppe und Kategorie, ohne bekannte Gruppe fehlt die Kategorie" <| fun () ->
            let category id groupId : YnabMonthCategory =
                { Id = id; Name = "x"; GroupName = "g"; GroupId = groupId; Assigned = eur 0m; Activity = eur 0m
                  Available = eur 0m; Target = None; Hidden = false; Deleted = false; Internal = false }
            let month = { Month = november; ToBeBudgeted = eur 0m; Categories = [ category miete (Some wohnen); category kino None ] }
            Expect.equal (monthPlacements month) [ wohnen, miete ] "nur mit Gruppe"
    ]

// ============================================
// Rein: Gruppe deaktivieren erlaubt?
// ============================================

[<Tests>]
let groupCheckTests =
    testList "Gruppe deaktivieren: erlaubt nur ohne Geld in der Gruppe" [
        testCase "eine Gruppe ohne Geld und eine leere Gruppe sind erlaubt" <| fun () ->
            Expect.equal (groupDeactivationCheck [ "Miete", eur 0m, []; "Strom", eur 0m, [ december, eur 0m ] ]) (Ok ()) "ohne Geld"
            Expect.equal (groupDeactivationCheck []) (Ok ()) "leer"

        testCase "Verfügbares positiv und negativ und künftige Zuweisung: abgelehnt, jede Kategorie mit Betrag in BBs Reihenfolge" <| fun () ->
            let result =
                groupDeactivationCheck
                    [ "Restaurant", eur 24.5m, []
                      "Miete", eur 0m, []
                      "Strom", eur -12m, []
                      "Kantine", eur 0m, [ december, eur 50m ] ]
            Expect.equal
                result
                (Error
                    "Restaurant hat noch 24,50 €. Erst verschieben, dann deaktivieren. Strom ist mit 12,00 € überzogen. Erst ausgleichen, dann deaktivieren. Kantine hat im Dezember 2026 50,00 € zugewiesen. Erst zurücknehmen, dann deaktivieren.")
                "alle Gründe, Miete ohne Geld fehlt"

        testCase "die Reihenfolge der Meldung folgt der Eingabe" <| fun () ->
            Expect.equal
                (groupDeactivationCheck [ "B", eur 1m, []; "A", eur 2m, [] ])
                (Error "B hat noch 1,00 €. Erst verschieben, dann deaktivieren. A hat noch 2,00 €. Erst verschieben, dann deaktivieren.")
                "B vor A"
    ]

// ============================================
// Rein: Budget-Liste mit dem wirksamen Set
// ============================================

let private budgetCategory (id: Guid) (name: string) (activity: decimal) (available: decimal) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur activity; Available = eur available; Underfunded = None
      Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None; ActivityInYnab = None }

[<Tests>]
let pageListTests =
    testList "Budget-Liste: Kategorien einer deaktivierten Gruppe" [
        testCase "ohne Aktivität und Verfügbares fehlt die Gruppe, sonst steht sie nur mit diesen Kategorien da" <| fun () ->
            let month : BudgetMonth =
                { Month = november; ToBeBudgeted = eur 0m; CopiedAt = None
                  Groups =
                    [ { Name = "Wohnen"; Categories = [ budgetCategory miete "Miete" 0m 0m; budgetCategory strom "Strom" -30m 0m ] }
                      { Name = "Freizeit"; Categories = [ budgetCategory kino "Kino" 0m 0m; budgetCategory sportverein "Sportverein" 0m 0m ] }
                      { Name = "Alltag"; Categories = [ budgetCategory internet "Internet" 0m 0m ] } ] }
            let layout = layoutOf [] [ wohnen; freizeit ]
            let placements = [ wohnen, miete; wohnen, strom; freizeit, kino; freizeit, sportverein ]
            let shown = applyDeactivation (effectiveDeactivated layout placements) month
            Expect.equal
                (shown.Groups |> List.map (fun g -> g.Name, g.Categories |> List.map (fun c -> c.Name, c.Deactivated)))
                [ "Wohnen", [ "Strom", Some true ]; "Alltag", [ "Internet", None ] ]
                "Freizeit fehlt, Wohnen nur mit Strom, markiert"
    ]

// ============================================
// Handler mit In-Memory-SQLite
// ============================================

let private ynabGroups : Server.YnabClient.YnabCategoryGroup list =
    [ { Id = wohnen
        Name = "Wohnen"
        Categories = [ { Id = miete; Name = "Miete" }; { Id = strom; Name = "Strom" }; { Id = internet; Name = "Internet" } ] }
      { Id = freizeit
        Name = "Freizeit"
        Categories = [ { Id = kino; Name = "Kino" }; { Id = sportverein; Name = "Sportverein" } ] } ]

let private rawCategory (id: Guid) (name: string) (groupId: Guid) (assigned: decimal) (activity: decimal) (available: decimal) : YnabMonthCategory =
    { Id = id; Name = name; GroupName = (if groupId = wohnen then "Wohnen" else "Freizeit"); GroupId = Some groupId
      Assigned = eur assigned; Activity = eur activity; Available = eur available
      Target = None; Hidden = false; Deleted = false; Internal = false }

let private rawMonth (month: string) (categories: YnabMonthCategory list) : YnabMonth =
    { Month = month; ToBeBudgeted = eur 0m; Categories = categories }

let private quietMonth (month: string) =
    rawMonth month
        [ rawCategory miete "Miete" wohnen 0m 0m 0m
          rawCategory strom "Strom" wohnen 0m 0m 0m
          rawCategory internet "Internet" wohnen 0m 0m 0m
          rawCategory kino "Kino" freizeit 0m 0m 0m
          rawCategory sportverein "Sportverein" freizeit 0m 0m 0m ]

/// YNAB als Fake, der zählt: Gruppen und Monate aus den Vorgaben, jedes Schreiben ist ein Fehler.
type private Probe(months: Map<string, YnabMonth>) =
    let calls = Collections.Generic.List<string>()
    member _.Calls = List.ofSeq calls
    member _.Count (name: string) = calls |> Seq.filter ((=) name) |> Seq.length
    member _.Port : CategoryPort =
        { FetchGroups = fun () -> async { calls.Add "groups"; return Ok ynabGroups }
          CreateCategory = fun _ _ -> failwith "kein Schreiben nach YNAB"
          UpdateCategory = fun _ _ -> failwith "kein Schreiben nach YNAB"
          CreateGroup = fun _ -> failwith "kein Schreiben nach YNAB"
          UpdateGroup = fun _ _ -> failwith "kein Schreiben nach YNAB"
          RefreshMirror = fun () -> failwith "kein Spiegel"
          FetchMonth =
            fun month ->
                async {
                    calls.Add "month"
                    return months |> Map.tryFind month |> Option.map Ok |> Option.defaultValue (Error (BudgetNotFound month))
                } }

let private quietProbe () = Probe(Map.ofList [ november, quietMonth november ])

let private readGroups (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) =
    match groupsWith connect (quietProbe ()).Port |> run with
    | Ok groups -> groups
    | Error e -> failtest $"Gruppen erwartet: {e}"

let private groupIn (groups: ManagedCategoryGroup list) (id: Guid) = groups |> List.find (fun g -> g.Id = id)

let private setGroup connect (probe: Probe) (groupId: Guid) (active: bool) =
    setCategoryGroupActiveWith connect probe.Port november now { GroupId = groupId; Active = active } |> run

[<Tests>]
let handlerTests =
    testList "Gruppe deaktivieren: Handler mit In-Memory-SQLite" [
        testCase "Deaktivieren und Aktivieren überleben einen neuen Lese-Durchgang, genau ein Monat je Prüfung" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let probe = quietProbe ()
            Expect.equal (setGroup connect probe wohnen false) (Ok ()) "deaktiviert"
            Expect.equal probe.Calls [ "groups"; "month" ] "erst Gruppen, dann genau ein Monat"
            Expect.isFalse (groupIn (readGroups connect) wohnen).Active "neu gelesen deaktiviert"
            Expect.isTrue (groupIn (readGroups connect) freizeit).Active "die andere bleibt aktiv"
            let activating = quietProbe ()
            Expect.equal (setGroup connect activating wohnen true) (Ok ()) "aktiviert"
            Expect.isEmpty activating.Calls "Aktivieren prüft nichts und ruft YNAB nicht"
            Expect.isTrue (groupIn (readGroups connect) wohnen).Active "neu gelesen wieder aktiv"

        testCase "Aktivieren lässt eine einzeln deaktivierte Kategorie deaktiviert und die Reihenfolge unverändert" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setCategoryActiveWith connect (quietProbe ()).Port november now { CategoryId = strom; Active = false } |> run |> ignore
            setCategoryOrderWith connect now { GroupId = wohnen; CategoryIds = [ internet; strom; miete ] } |> run |> ignore
            setGroup connect (quietProbe ()) wohnen false |> ignore
            let off = groupIn (readGroups connect) wohnen
            Expect.equal
                (off.Active, off.Categories |> List.map (fun c -> c.Id, c.Active))
                (false, [ internet, true; strom, false; miete, true ])
                "deaktiviert, die Kategorien behalten ihren eigenen Zustand"
            setGroup connect (quietProbe ()) wohnen true |> ignore
            let back = groupIn (readGroups connect) wohnen
            Expect.equal
                (back.Active, back.Categories |> List.map (fun c -> c.Id, c.Active))
                (true, [ internet, true; strom, false; miete, true ])
                "Stand davor: Strom bleibt deaktiviert, BBs Reihenfolge bleibt"

        testCase "eine Kategorie hält Geld, auch eine einzeln deaktivierte und eine in YNAB ausgeblendete: abgelehnt, nichts geschrieben" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setCategoryActiveWith connect (quietProbe ()).Port november now { CategoryId = strom; Active = false } |> run |> ignore
            setCategoryOrderWith connect now { GroupId = wohnen; CategoryIds = [ strom; miete; internet ] } |> run |> ignore
            let hidden = Guid.NewGuid()
            let month =
                rawMonth november
                    [ rawCategory miete "Miete" wohnen 0m 0m 10m
                      rawCategory strom "Strom" wohnen 0m 0m 5m
                      rawCategory internet "Internet" wohnen 0m 0m 0m
                      // In YNAB ausgeblendet: nicht in YNABs Liste der Seite, geprüft wird sie trotzdem.
                      { rawCategory hidden "Zeitung" wohnen 0m 0m 3m with Hidden = true }
                      // Gelöscht: zählt nicht.
                      { rawCategory (Guid.NewGuid()) "Alt" wohnen 0m 0m 7m with Deleted = true }
                      rawCategory kino "Kino" freizeit 0m 0m 99m ]
            let probe = Probe(Map.ofList [ november, month ])
            Expect.equal
                (setGroup connect probe wohnen false)
                (Error
                    (RefusedByBb
                        "Strom hat noch 5,00 €. Erst verschieben, dann deaktivieren. Miete hat noch 10,00 €. Erst verschieben, dann deaktivieren. Zeitung hat noch 3,00 €. Erst verschieben, dann deaktivieren."))
                "alle Gründe in BBs Reihenfolge, die ausgeblendete dahinter, Kino aus Freizeit nicht"
            Expect.isTrue (groupIn (readGroups connect) wohnen).Active "nichts geschrieben"

        testCase "eine Zuweisung in einem künftigen Monat (BBs Soll) lehnt ab" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let decemberRaw = rawMonth december [ rawCategory internet "Internet" wohnen 50m 0m 50m ]
            let ynab : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok decemberRaw }
                  SetAssigned = fun _ _ _ -> failwith "kein Schreiben"
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            match CommandHandlers.observeMonthWith connect ynab now december |> run with
            | Ok _ -> ()
            | Error e -> failtest $"Übernahme erwartet: {e}"
            let probe = quietProbe ()
            Expect.equal
                (setGroup connect probe wohnen false)
                (Error (RefusedByBb "Internet hat im Dezember 2026 50,00 € zugewiesen. Erst zurücknehmen, dann deaktivieren."))
                "abgelehnt"
            Expect.equal (probe.Count "month") 1 "genau ein Monat aus YNAB"
            Expect.isTrue (groupIn (readGroups connect) wohnen).Active "nichts geschrieben"

        testCase "eine leere Gruppe lässt sich deaktivieren" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let empty = Probe(Map.ofList [ november, rawMonth november [] ])
            Expect.equal (setGroup connect empty freizeit false) (Ok ()) "erlaubt"

        testCase "unbekannte Gruppe: abgelehnt ohne Monat, nichts geschrieben" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let probe = quietProbe ()
            let unknown = Guid.NewGuid()
            Expect.equal (setGroup connect probe unknown false) (Error (RefusedByBb GroupNotFoundText)) "Gruppe nicht gefunden."
            Expect.equal probe.Calls [ "groups" ] "kein FetchMonth"
            match Budgeting.Persistence.loadCategoryLayoutWith connect |> run with
            | Ok layout -> Expect.isEmpty layout.DeactivatedGroups "nichts geschrieben"
            | Error message -> failtest message

        testCase "Soll künftiger Monate nicht lesbar: Meldung, nichts geschrieben" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            use conn = connect ()
            Dapper.SqlMapper.Execute(conn, "DROP TABLE events") |> ignore
            Expect.equal
                (setGroup connect (quietProbe ()) wohnen false)
                (Error (YnabError.LocalReadFailed "Künftige Monate nicht lesbar, Gruppe nicht deaktiviert."))
                "Meldung"
            Expect.isTrue (groupIn (readGroups connect) wohnen).Active "nichts geschrieben"

        testCase "der Kategorie-Spiegel überschreibt eine deaktivierte Gruppe nicht" <| fun () ->
            Ledger.Persistence.initializeLedgerSchema ()
            Budgeting.Persistence.initializeBudgetingSchema ()
            let connect = global.Persistence.getConnection
            let groupId, categoryId = Guid.NewGuid(), Guid.NewGuid()
            do
                use conn = connect ()
                Budgeting.Persistence.setCategoryGroupActiveOn conn groupId false now
            let mirrored : YnabCategory list =
                [ { Id = YnabCategoryId categoryId; Name = "Neu"; GroupName = "G"; Available = None; Hidden = false } ]
            refreshMirrorWith (fun () -> async { return Ok mirrored }) Ledger.Persistence.upsertCategories |> run
            match Budgeting.Persistence.loadCategoryLayoutWith connect |> run with
            | Ok layout -> Expect.isTrue (layout.DeactivatedGroups.Contains groupId) "bleibt deaktiviert"
            | Error message -> failtest message
    ]

// ============================================
// Abfragen: Picker und Budget-Seite
// ============================================

let private ynabCategory (id: Guid) (name: string) (group: string) : YnabCategory =
    { Id = YnabCategoryId id; Name = name; GroupName = group; Available = Some (eur 1m); Hidden = false }

[<Tests>]
let queryTests =
    testList "Gruppe deaktivieren: Picker und Budget-Seite" [
        testCase "getCategories enthält keine Kategorie einer deaktivierten Gruppe" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setGroup connect (quietProbe ()) wohnen false |> ignore
            let placed =
                [ ynabCategory miete "Miete" "Wohnen"; ynabCategory strom "Strom" "Wohnen"; ynabCategory kino "Kino" "Freizeit" ],
                [ wohnen, miete; wohnen, strom; freizeit, kino ]
            let options =
                pickerOptionsWith
                    (fun () -> async { return Ok placed })
                    (fun () -> async { return None })
                    (fun () -> Budgeting.Persistence.loadCategoryLayoutWith connect)
                |> run
            Expect.equal (options |> Result.map (List.map (fun c -> c.Name))) (Ok [ "Kino" ]) "nur Freizeit"

        testCase "die Budget-Seite lässt Kategorien einer deaktivierten Gruppe ohne Geld weg und markiert die mit Aktivität" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            setGroup connect (quietProbe ()) wohnen false |> ignore
            let raw =
                rawMonth november
                    [ rawCategory miete "Miete" wohnen 0m 0m 0m
                      rawCategory strom "Strom" wohnen 0m -30m -30m
                      rawCategory kino "Kino" freizeit 0m 0m 0m ]
            let ynab : CommandHandlers.YnabPort =
                { FetchMonth = fun _ -> async { return Ok raw }
                  SetAssigned = fun _ _ _ -> failwith "kein Schreiben"
                  WriteGoal = (fun _ _ -> async { return Ok () })
                  FetchCategoryGoals = fun () -> async { return Ok [] } }
            let ledger : CommandHandlers.LedgerActivityPort =
                { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
                  LoadFirstEntryDate = fun () -> async { return None } }
            // Erst ohne Stand (beobachtet einmal), dann aus dem gespeicherten Stand: die Gruppe muss den Stand überleben.
            for pass in [ "beobachtet"; "aus dem Stand" ] do
                match QueryHandlers.budgetMonthWithLedger ledger connect ynab now november |> run with
                | Ok month ->
                    let all = month.Groups |> List.collect (fun g -> g.Categories)
                    Expect.equal (all |> List.map (fun c -> c.Id)) [ strom; kino ] $"{pass}: Miete fehlt"
                    Expect.isTrue (all |> List.find (fun c -> c.Id = strom) |> isDeactivated) $"{pass}: Strom markiert"
                    Expect.isFalse (all |> List.find (fun c -> c.Id = kino) |> isDeactivated) $"{pass}: Kino aktiv"
                | Error e -> failtest $"Monat erwartet: {e}"

        testCase "der Monats-Decoder liest die Gruppe der Kategorie" <| fun () ->
            let json =
                $"""{{"data":{{"month":{{"month":"{november}","to_be_budgeted":0,"categories":[{{"id":"{miete}","name":"Miete","category_group_id":"{wohnen}","category_group_name":"Wohnen","budgeted":0,"activity":0,"balance":0}},{{"id":"{kino}","name":"Kino","budgeted":0,"activity":0,"balance":0}}]}}}}}}"""
            match Thoth.Json.Net.Decode.fromString Server.YnabClient.Decoders.monthDecoder json with
            | Ok month -> Expect.equal (month.Categories |> List.map (fun c -> c.GroupId)) [ Some wohnen; None ] "Gruppe oder keine"
            | Error e -> failtest e
    ]

// ============================================
// Targets anwenden
// ============================================

let private monthlyNeed : Target =
    { Goal = eur 50m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None }

/// Miete in Wohnen und Kino in Freizeit, beide mit Monatsziel und Fehlbetrag.
let private targetMonth : YnabMonth =
    { rawMonth november
        [ { rawCategory miete "Miete" wohnen 0m 0m 0m with Target = Some monthlyNeed }
          { rawCategory kino "Kino" freizeit 0m 0m 0m with Target = Some monthlyNeed } ] with
        ToBeBudgeted = eur 500m }

let private targetPort () : CommandHandlers.YnabPort * ResizeArray<Guid * int64> =
    let writes = ResizeArray<Guid * int64>()
    { FetchMonth = fun _ -> async { return Ok targetMonth }
      SetAssigned = fun _ id milliunits -> async { lock writes (fun () -> writes.Add((id, milliunits))); return Ok () }
      WriteGoal = (fun _ _ -> async { return Ok () })
      FetchCategoryGoals = fun () -> async { return Ok (BudgetEventsFixture.goalsOf targetMonth) } },
    writes

let private quietLedger : CommandHandlers.LedgerActivityPort =
    { LoadMonthlyActivity = fun _ _ -> async { return Map.empty }
      LoadFirstEntryDate = fun () -> async { return None } }

[<Tests>]
let targetsTests =
    testList "Gruppe deaktivieren: Targets anwenden" [
        testCase "Vorschau und Anwenden übergehen die Kategorien einer deaktivierten Gruppe" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            use conn = connect ()
            Budgeting.Persistence.setCategoryGroupActiveOn conn wohnen false now
            let port, writes = targetPort ()
            let shown =
                match CommandHandlers.previewWithLedger quietLedger connect port now november |> run with
                | Ok p -> p
                | Error e -> failtest $"Vorschau erwartet: {e}"
            Expect.equal (shown.Lines |> List.map (fun l -> l.CategoryId)) [ kino ] "nur Kino"
            Expect.isEmpty shown.Skipped "Miete steht nicht unter übersprungen"
            match CommandHandlers.applyWithLedger quietLedger connect port now november { shown with CommandId = Some (Guid.NewGuid()) } |> run with
            | Ok (Applied _) -> ()
            | other -> failtest $"Applied erwartet: {other}"
            Expect.equal (writes |> Seq.map fst |> List.ofSeq) [ kino ] "nur Kino geht nach YNAB"
    ]

// ============================================
// Prüfung an der Grenze
// ============================================

[<Tests>]
let validationTests =
    testList "Gruppe deaktivieren: Prüfung an der Grenze" [
        testCase "Anlegen in und Verschieben in eine deaktivierte Gruppe: abgelehnt mit Namen, ohne YNAB-Aufruf zum Schreiben" <| fun () ->
            let layout () = async { return Ok (layoutOf [] [ wohnen ]) }
            let probe = quietProbe ()
            Expect.equal
                (saveCategory layout probe.Port { CategoryId = None; Name = "Gas"; GroupId = wohnen } |> run)
                (Error (RefusedByBb "Gruppe Wohnen ist in BB deaktiviert."))
                "Anlegen"
            Expect.equal
                (saveCategory layout probe.Port { CategoryId = Some kino; Name = "Kino"; GroupId = wohnen } |> run)
                (Error (RefusedByBb "Gruppe Wohnen ist in BB deaktiviert."))
                "Verschieben"
            Expect.equal probe.Calls [ "groups"; "groups" ] "nur gelesen, nichts geschrieben"

        testCase "eine aktive Gruppe nimmt weiter an" <| fun () ->
            let groups = arrangeGroups (layoutOf [] [ wohnen ]) ynabGroups
            Expect.equal
                (Server.Validation.validateCategorySave groups { CategoryId = None; Name = "Gas"; GroupId = freizeit })
                (Ok (Server.Validation.CreateCategoryIn (freizeit, "Gas")))
                "Freizeit ist aktiv"

        testCase "ist BBs Zustand nicht lesbar, wird keine Kategorie gespeichert" <| fun () ->
            let probe = quietProbe ()
            Expect.equal
                (saveCategory (fun () -> async { return Error "kaputt" }) probe.Port { CategoryId = None; Name = "Gas"; GroupId = freizeit } |> run)
                (Error (YnabError.LocalReadFailed "kaputt"))
                "Fehler"
            Expect.isEmpty probe.Calls "YNAB nicht gefragt"

        testCase "eine leere Gruppe an Aktivieren oder Deaktivieren wird abgelehnt" <| fun () ->
            Expect.equal
                (Server.Validation.validateCategoryGroupActiveSave { GroupId = Guid.Empty; Active = false })
                (Error [ Server.Validation.GroupActiveGroupMissingText ])
                "leer"
            Expect.equal (Server.Validation.validateCategoryGroupActiveSave { GroupId = wohnen; Active = false }) (Ok ()) "genannt"
    ]

// ============================================
// Endpoint: Verdrahtung in `categoryApiWith`
// ============================================

[<Tests>]
let apiTests =
    testList "Gruppe deaktivieren: Endpoint" [
        testCase "der Endpoint prüft den Berliner Monat und hält die Gruppe auf der übergebenen Datenbank fest" <| fun () ->
            let anchor, connect = CategoryLayoutFixture.isolated ()
            use _ = anchor
            let months = Collections.Generic.List<string>()
            let probe = quietProbe ()
            let port = { probe.Port with FetchMonth = fun month -> async { months.Add month; return Ok (quietMonth month) } }
            let api =
                Server.Api.categoryApiWith
                    (fun _ -> async { return Some "eingerichtet" })
                    (fun _ _ -> port)
                    connect
                    // 23:30 UTC am 30. November ist in Berlin schon der 1. Dezember.
                    (fun () -> DateTime(2026, 11, 30, 23, 30, 0, DateTimeKind.Utc))
                    (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            Expect.equal (api.setCategoryGroupActive { GroupId = wohnen; Active = false } |> run) (Ok ()) "deaktiviert"
            Expect.equal (List.ofSeq months) [ december ] "der laufende Berliner Monat"
            match Budgeting.Persistence.loadCategoryLayoutWith connect |> run with
            | Ok layout -> Expect.equal layout.DeactivatedGroups (Set.ofList [ wohnen ]) "auf der übergebenen Datenbank"
            | Error message -> failtest message
            Expect.equal
                (api.saveCategory { CategoryId = None; Name = "Gas"; GroupId = wohnen } |> run)
                (Error "Gruppe Wohnen ist in BB deaktiviert.")
                "Anlegen liest BBs Zustand von derselben Datenbank"
    ]
