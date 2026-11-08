module CategoryAdminTests

// Kategorien verwalten (budgeting-pscgb, ADR 0088 Punkt 1): Encoder und Decoder des YNAB-Clients, die
// Validierung an der Grenze und der Befehl mit Fake-Port. Kein Netz: die Schreibwege laufen über die
// `*With`-Nähte, der Port zählt seine Aufrufe.

open System
open Expecto
open Thoth.Json.Net
open Shared.Domain
open Shared.Budgeting
open Server.Validation
open Server.Budgeting.Categories

let private alltag = Guid.Parse "11111111-0000-0000-0000-000000000001"
let private kinder = Guid.Parse "11111111-0000-0000-0000-000000000002"
let private lebensmittel = Guid.Parse "22222222-0000-0000-0000-000000000001"
let private drogerie = Guid.Parse "22222222-0000-0000-0000-000000000002"
let private kinderkram = Guid.Parse "22222222-0000-0000-0000-000000000003"

let private groupsFixture : ManagedCategoryGroup list =
    [ { Id = alltag
        Name = "Alltag"
        Categories = [ { Id = lebensmittel; Name = "Lebensmittel" }; { Id = drogerie; Name = "Drogerie" } ] }
      { Id = kinder; Name = "Kinder"; Categories = [ { Id = kinderkram; Name = "Kinderkram" } ] } ]

let private run (a: Async<'T>) = Async.RunSynchronously a

/// Ein Fake-Port, der jeden Aufruf mitschreibt. `answer` liefert die Antwort jedes Schreibens.
type private Probe(answer: unit -> YnabResult<Guid>, ?fetched: YnabResult<ManagedCategoryGroup list>) =
    let calls = Collections.Generic.List<string>()
    member _.Calls = List.ofSeq calls
    member _.Count(prefix: string) = calls |> Seq.filter (fun c -> c.StartsWith prefix) |> Seq.length
    member _.Port : CategoryPort =
        { FetchGroups = fun () -> async { calls.Add "fetch"; return defaultArg fetched (Ok groupsFixture) }
          CreateCategory = fun groupId name -> async { calls.Add $"create-category {groupId} {name}"; return answer () }
          UpdateCategory =
            fun categoryId patch -> async { calls.Add $"patch-category {categoryId} {patch.Name} {patch.GroupId}"; return answer () }
          CreateGroup = fun name -> async { calls.Add $"create-group {name}"; return answer () }
          UpdateGroup = fun groupId name -> async { calls.Add $"patch-group {groupId} {name}"; return answer () }
          RefreshMirror = fun () -> async { calls.Add "mirror" } }

let private newId = Guid.Parse "33333333-0000-0000-0000-000000000001"
let private okProbe () = Probe(fun () -> Ok newId)

[<Tests>]
let encoderTests =
    testList "Kategorien verwalten: Rümpfe und Pfade nach YNAB" [
        testCase "Kategorie anlegen schickt Name und Gruppe unter category" <| fun () ->
            let body = Server.YnabClient.buildCreateCategoryBody alltag "Haustier"
            Expect.equal body $"""{{"category":{{"name":"Haustier","category_group_id":"{alltag}"}}}}""" "Rumpf"

        testCase "Bearbeiten schickt nur die geänderten Felder" <| fun () ->
            let only patch = Server.YnabClient.buildPatchCategoryBody patch
            Expect.equal (only { Name = Some "Essen"; GroupId = None }) """{"category":{"name":"Essen"}}""" "nur Name"
            Expect.equal (only { Name = None; GroupId = Some kinder }) $"""{{"category":{{"category_group_id":"{kinder}"}}}}""" "nur Gruppe"
            Expect.equal
                (only { Name = Some "Essen"; GroupId = Some kinder })
                $"""{{"category":{{"name":"Essen","category_group_id":"{kinder}"}}}}"""
                "beides"

        testCase "Gruppe anlegen und umbenennen schicken den Namen unter category_group" <| fun () ->
            Expect.equal (Server.YnabClient.buildCategoryGroupBody "Freizeit") """{"category_group":{"name":"Freizeit"}}""" "Rumpf"

        testCase "die Pfade liegen wie der übrige Client unter budgets" <| fun () ->
            // Nur das Ende des Pfads: der Host steht als Text nicht in Tests (Netz-Wächter).
            Expect.stringEnds (Server.YnabClient.categoriesUrl "bud" None) "/v1/budgets/bud/categories" "anlegen"
            Expect.stringEnds (Server.YnabClient.categoriesUrl "bud" (Some drogerie)) $"/v1/budgets/bud/categories/{drogerie}" "bearbeiten"
            Expect.stringEnds (Server.YnabClient.categoryGroupsUrl "bud" None) "/v1/budgets/bud/category_groups" "Gruppe anlegen"
            Expect.stringEnds (Server.YnabClient.categoryGroupsUrl "bud" (Some kinder)) $"/v1/budgets/bud/category_groups/{kinder}" "Gruppe umbenennen"

        testCase "die Schreibwege senden Methode, Pfad und Rumpf und lesen die Id der Antwort" <| fun () ->
            let sent = Collections.Generic.List<string * string * string>()
            let send status field (m: string) (url: string) (body: string) =
                async {
                    sent.Add((m, url, body))
                    return status, $"""{{"data":{{"{field}":{{"id":"{newId}"}}}}}}"""
                }
            let created = Server.YnabClient.createCategoryWith (send 201 "category") "bud" alltag "Haustier" |> run
            let patched = Server.YnabClient.patchCategoryWith (send 200 "category") "bud" drogerie { Name = Some "Essen"; GroupId = None } |> run
            let group = Server.YnabClient.createCategoryGroupWith (send 201 "category_group") "bud" "Freizeit" |> run
            let renamed = Server.YnabClient.updateCategoryGroupWith (send 200 "category_group") "bud" kinder "Familie" |> run
            for r in [ created; patched; group; renamed ] do
                Expect.equal r (Ok newId) "Id aus data"
            Expect.equal (sent |> Seq.map (fun (m, _, _) -> m) |> List.ofSeq) [ "POST"; "PATCH"; "POST"; "PATCH" ] "Methoden"
            Expect.stringEnds (let (_, url, _) = sent.[1] in url) $"/categories/{drogerie}" "PATCH auf die Kategorie"
            Expect.stringEnds (let (_, url, _) = sent.[3] in url) $"/category_groups/{kinder}" "PATCH auf die Gruppe"

        testCase "400, 429 und ein Netzfehler werden zu Fehlern mit deutscher Meldung" <| fun () ->
            let answer status = fun _ _ _ -> async { return status, """{"error":{"detail":"x"}}""" }
            let create send = Server.YnabClient.createCategoryWith send "bud" alltag "Haustier" |> run
            match create (answer 400) with
            | Error e -> Expect.equal (ynabErrorText e) "YNAB hat abgelehnt: Kategorie ungültig (HTTP 400)" "400"
            | Ok _ -> failtest "400 ist ein Fehler"
            Expect.equal (create (answer 429)) (Error (RateLimitExceeded 60)) "429"
            match create (fun _ _ _ -> async { return failwith "Verbindung weg" }) with
            | Error e -> Expect.equal (ynabErrorText e) "Netzwerkfehler: YNAB nicht erreichbar" "Netz"
            | Ok _ -> failtest "Netzfehler ist ein Fehler"
    ]

[<Tests>]
let decoderTests =
    testList "Kategorien verwalten: Gruppen aus YNAB lesen" [
        testCase "gelöschte, ausgeblendete und interne Gruppen und Kategorien fallen weg" <| fun () ->
            let category (id: Guid) name extra = $"""{{"id":"{id}","name":"{name}"{extra}}}"""
            let group (id: Guid) name extra (categories: string list) =
                $"""{{"id":"{id}","name":"{name}"{extra},"categories":[{String.Join(",", categories)}]}}"""
            let body =
                let groups =
                    [ group alltag "Alltag" "" [
                          category lebensmittel "Lebensmittel" ""
                          category drogerie "Alt" ",\"deleted\":true"
                          category kinderkram "Versteckt" ",\"hidden\":true" ]
                      group kinder "Weg" ",\"deleted\":true" []
                      group (Guid.NewGuid()) "Aus" ",\"hidden\":true" []
                      group (Guid.NewGuid()) "Internal Master Category" "" [ category (Guid.NewGuid()) "Inflow: Ready to Assign" "" ]
                      group (Guid.NewGuid()) "Credit Card Payments" "" [ category (Guid.NewGuid()) "Visa" "" ] ]
                $"""{{"data":{{"category_groups":[{String.Join(",", groups)}]}}}}"""
            let expected : ManagedCategoryGroup list =
                [ { Id = alltag; Name = "Alltag"; Categories = [ { Id = lebensmittel; Name = "Lebensmittel" } ] } ]
            Expect.equal (Server.YnabClient.interpretCategoryGroupsResponse "bud" 200 body) (Ok expected) "nur das Sichtbare"

        testCase "Fehler beim Lesen kommen als YNAB-Fehler" <| fun () ->
            Expect.equal (Server.YnabClient.interpretCategoryGroupsResponse "bud" 429 "") (Error (RateLimitExceeded 60)) "429"
            Expect.isError (Server.YnabClient.interpretCategoryGroupsResponse "bud" 200 "kein json") "unlesbar"
    ]

[<Tests>]
let validationTests =
    testList "Kategorien verwalten: Validierung" [
        testCase "leerer Name wird abgelehnt, für Kategorie und Gruppe" <| fun () ->
            Expect.equal (validateCategoryName "  ") (Error [ CategoryNameMissingText ]) "Kategorie"
            Expect.equal (validateCategoryGroupName "") (Error [ CategoryNameMissingText ]) "Gruppe"

        testCase "ein Gruppenname hat höchstens 50 Zeichen" <| fun () ->
            Expect.equal (validateCategoryGroupName (String('a', 50))) (Ok (String('a', 50))) "50 geht"
            Expect.equal (validateCategoryGroupName (String('a', 51))) (Error [ GroupNameTooLongText ]) "51 nicht"

        testCase "ein Name, den die Gruppe schon hat, wird abgelehnt, auch in anderer Schreibweise" <| fun () ->
            let save = { CategoryId = None; Name = " drogerie "; GroupId = alltag }
            Expect.equal
                (validateCategorySave groupsFixture save)
                (Error [ "In der Gruppe Alltag gibt es schon eine Kategorie drogerie." ])
                "Duplikat"
            Expect.equal
                (validateCategorySave groupsFixture { save with GroupId = kinder })
                (Ok (CreateCategoryIn (kinder, "drogerie")))
                "in einer anderen Gruppe geht es"

        testCase "unbekannte Gruppe und unbekannte Kategorie werden zusammen gemeldet" <| fun () ->
            let save = { CategoryId = Some (Guid.NewGuid()); Name = ""; GroupId = Guid.NewGuid() }
            Expect.equal
                (validateCategorySave groupsFixture save)
                (Error [ CategoryNameMissingText; UnknownCategoryText; UnknownGroupText ])
                "alle Gründe"

        testCase "Bearbeiten nennt nur, was sich geändert hat" <| fun () ->
            let edit name group = validateCategorySave groupsFixture { CategoryId = Some drogerie; Name = name; GroupId = group }
            Expect.equal (edit "Drogerie" alltag) (Ok (CategoryUnchanged drogerie)) "nichts"
            Expect.equal (edit "Bad" alltag) (Ok (PatchCategory (drogerie, Some "Bad", None))) "nur Name"
            Expect.equal (edit "Drogerie" kinder) (Ok (PatchCategory (drogerie, None, Some kinder))) "nur Gruppe"
            Expect.equal (edit "drogerie" alltag) (Ok (PatchCategory (drogerie, Some "drogerie", None))) "eigene Schreibweise ist kein Duplikat"

        testCase "Gruppen: Duplikat, unbekannte Gruppe und Länge" <| fun () ->
            Expect.equal
                (validateCategoryGroupSave groupsFixture { GroupId = None; Name = "kinder" })
                (Error [ "Eine Gruppe kinder gibt es schon." ])
                "Duplikat"
            Expect.equal
                (validateCategoryGroupSave groupsFixture { GroupId = Some (Guid.NewGuid()); Name = String('a', 51) })
                (Error [ GroupNameTooLongText; UnknownGroupText ])
                "alle Gründe"
            Expect.equal (validateCategoryGroupSave groupsFixture { GroupId = Some kinder; Name = "Kinder" }) (Ok (GroupUnchanged kinder)) "unverändert"
            Expect.equal (validateCategoryGroupSave groupsFixture { GroupId = None; Name = " Freizeit " }) (Ok (CreateGroup "Freizeit")) "anlegen"
    ]

[<Tests>]
let handlerTests =
    testList "Kategorien verwalten: Befehl nach YNAB" [
        testCase "Kategorie anlegen schickt POST mit Name und Gruppe und stößt danach den Spiegel an" <| fun () ->
            let probe = okProbe ()
            let result = saveCategory probe.Port { CategoryId = None; Name = "Haustier"; GroupId = kinder } |> run
            Expect.equal result (Ok newId) "Id der neuen Kategorie"
            Expect.equal probe.Calls [ "fetch"; $"create-category {kinder} Haustier"; "mirror" ] "Aufrufe in Reihenfolge"

        testCase "Umbenennen und Gruppe wechseln schicken PATCH nur mit den geänderten Feldern" <| fun () ->
            let patched name group =
                let probe = okProbe ()
                saveCategory probe.Port { CategoryId = Some drogerie; Name = name; GroupId = group } |> run |> ignore
                probe.Calls
            Expect.equal (patched "Bad" alltag) [ "fetch"; $"patch-category {drogerie} Some(Bad) "; "mirror" ] "nur Name"
            Expect.equal (patched "Drogerie" kinder) [ "fetch"; $"patch-category {drogerie}  Some({kinder})"; "mirror" ] "nur Gruppe"
            Expect.equal (patched "Drogerie" alltag) [ "fetch" ] "nichts geändert: kein Schreiben, kein Spiegel"

        testCase "Gruppe anlegen und umbenennen schicken POST und PATCH auf category_groups" <| fun () ->
            let probe = okProbe ()
            Expect.equal (saveCategoryGroup probe.Port { GroupId = None; Name = "Freizeit" } |> run) (Ok newId) "anlegen"
            Expect.equal (saveCategoryGroup probe.Port { GroupId = Some kinder; Name = "Familie" } |> run) (Ok newId) "umbenennen"
            Expect.equal
                probe.Calls
                [ "fetch"; "create-group Freizeit"; "mirror"; "fetch"; $"patch-group {kinder} Familie"; "mirror" ]
                "Aufrufe"

        testCase "eine Ablehnung der Validierung ruft YNAB nicht zum Schreiben auf" <| fun () ->
            let refused (call: CategoryPort -> Async<Result<Guid, YnabError>>) (expected: string) (fetches: int) =
                let probe = okProbe ()
                match call probe.Port |> run with
                | Error (RefusedByBb message) -> Expect.stringContains message expected "konkrete Meldung"
                | other -> failtestf "Ablehnung erwartet, bekam %A" other
                Expect.equal probe.Calls (List.replicate fetches "fetch") "kein Schreiben, kein Spiegel"
            refused (fun p -> saveCategory p { CategoryId = None; Name = " "; GroupId = alltag }) CategoryNameMissingText 0
            refused (fun p -> saveCategoryGroup p { GroupId = None; Name = String('x', 51) }) GroupNameTooLongText 0
            refused (fun p -> saveCategory p { CategoryId = None; Name = "Drogerie"; GroupId = alltag }) "schon eine Kategorie Drogerie" 1
            refused (fun p -> saveCategory p { CategoryId = None; Name = "Neu"; GroupId = Guid.NewGuid() }) UnknownGroupText 1
            refused (fun p -> saveCategory p { CategoryId = Some (Guid.NewGuid()); Name = "Neu"; GroupId = alltag }) UnknownCategoryText 1
            refused (fun p -> saveCategoryGroup p { GroupId = Some (Guid.NewGuid()); Name = "Neu" }) UnknownGroupText 1

        testCase "YNAB antwortet mit Fehler: Meldung an den Aufrufer, kein Spiegel-Lauf" <| fun () ->
            for error in [ YnabError.InvalidResponse "Kategorie ungültig (HTTP 400)"; YnabError.RateLimitExceeded 60; YnabError.NetworkError "YNAB nicht erreichbar" ] do
                let probe = Probe(fun () -> Error error)
                Expect.equal (saveCategory probe.Port { CategoryId = None; Name = "Haustier"; GroupId = kinder } |> run) (Error error) "Fehler durchgereicht"
                Expect.equal (saveCategoryGroup probe.Port { GroupId = Some kinder; Name = "Familie" } |> run) (Error error) "Fehler bei Gruppe"
                Expect.equal (probe.Count "mirror") 0 "kein Spiegel, die Liste bleibt"

        testCase "die Liste nicht lesbar: nichts geschrieben" <| fun () ->
            let probe = Probe((fun () -> Ok newId), Error (RateLimitExceeded 60))
            Expect.equal (saveCategory probe.Port { CategoryId = None; Name = "Haustier"; GroupId = kinder } |> run) (Error (RateLimitExceeded 60)) "Fehler"
            Expect.equal probe.Calls [ "fetch" ] "nur gelesen"

        testCase "der Spiegel nach dem Schreiben scheitert nie und schreibt die gelesenen Kategorien" <| fun () ->
            let written = Collections.Generic.List<YnabCategory list>()
            let upsert cs = async { written.Add cs }
            let one : YnabCategory = { Id = YnabCategoryId drogerie; Name = "Drogerie"; GroupName = "Alltag"; Available = None; Hidden = false }
            refreshMirrorWith (fun () -> async { return Ok [ one ] }) upsert |> run
            refreshMirrorWith (fun () -> async { return Error (RateLimitExceeded 60) }) upsert |> run
            refreshMirrorWith (fun () -> async { return failwith "weg" }) upsert |> run
            Expect.equal (List.ofSeq written) [ [ one ] ] "nur der gelungene Abruf schreibt"
    ]

[<Tests>]
let apiTests =
    testList "Kategorien verwalten: Endpoint" [
        testCase "ein leerer Name wird an der Grenze abgelehnt, ohne Settings und ohne YNAB" <| fun () ->
            let api =
                Server.Api.categoryApiWith
                    (fun _ -> failwith "Settings nicht fragen")
                    (fun _ _ -> failwith "kein Port")
            Expect.equal (api.saveCategory { CategoryId = None; Name = ""; GroupId = alltag } |> run) (Error CategoryNameMissingText) "Kategorie"
            Expect.equal (api.saveCategoryGroup { GroupId = None; Name = String('x', 51) } |> run) (Error GroupNameTooLongText) "Gruppe"

        testCase "mit Einrichtung geht der Auftrag an den Port, der YNAB-Fehler kommt als Satz zurück" <| fun () ->
            let probe = Probe(fun () -> Error (RateLimitExceeded 60))
            let api = Server.Api.categoryApiWith (fun _ -> async { return Some "eingerichtet" }) (fun _ _ -> probe.Port)
            Expect.equal (api.getCategoryGroups () |> run) (Ok groupsFixture) "Liste"
            Expect.equal
                (api.saveCategory { CategoryId = None; Name = "Haustier"; GroupId = kinder } |> run)
                (Error "Rate-Limit (HTTP 429), erneut in 60 Sekunden")
                "Meldung"
    ]
