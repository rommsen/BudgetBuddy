module YnabIntegrationTests

open System
open System.IO
open Expecto
open Shared.Domain
open Server.YnabClient

// ============================================
// .env File Loader
// ============================================

let private loadEnv () =
    let projectRoot =
        let testDir = __SOURCE_DIRECTORY__
        Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

    let envPath = Path.Combine(projectRoot, ".env")

    if not (File.Exists envPath) then
        Map.empty
    else
        File.ReadAllLines(envPath)
        |> Array.filter (fun line ->
            not (String.IsNullOrWhiteSpace(line)) &&
            not (line.TrimStart().StartsWith("#"))
        )
        |> Array.choose (fun line ->
            match line.Split('=', 2) with
            | [| key; value |] -> Some (key.Trim(), value.Trim())
            | _ -> None
        )
        |> Map.ofArray

let private getEnvVar key =
    let env = loadEnv()
    env.TryFind key

let private shouldRunIntegrationTests () =
    match getEnvVar "RUN_INTEGRATION_TESTS" with
    | Some value when value.ToLower() = "true" -> true
    | _ -> false

// ============================================
// Integration Tests (require YNAB_TOKEN in .env)
// ============================================

[<Tests>]
let integrationTests =
    testList "YNAB Integration Tests (requires .env)" [
        // budgeting-g8s3w, Doktrin Punkt 3: Beleg für `liveFramePort`. Rein lesend.
        testCase "Der echte Rahmen liefert das Fenster aufsteigend und die Reihenfolge" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None -> Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                let (YnabBudgetId budgetId) =
                    match getBudgets token |> Async.RunSynchronously with
                    | Ok budgets ->
                        match budgets |> List.tryFind (fun b -> b.Name = "Test") with
                        | Some b -> b.Id
                        | None -> Tests.skiptest "Budget 'Test' nicht vorhanden - skipping integration test"
                    | Error e -> failtest $"Budgets nicht lesbar: {e}"
                let setting key =
                    async {
                        return
                            match key with
                            | "ynab_token" -> Some token
                            | "ynab_default_budget_id" -> Some budgetId
                            | _ -> None
                    }
                let berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
                match (Server.Api.liveBudgetApi setting berlin).getBudgetFrame () |> Async.RunSynchronously with
                | Ok frame ->
                    Expect.equal frame.Months (List.sort frame.Months) "aufsteigend"
                    Expect.isNonEmpty frame.Months "Fenster"
                    Expect.isNonEmpty frame.CategoryOrder "Reihenfolge"
                | Error message -> failtest message

        // budgeting-afd8h, ADR 0069: nimmt YNAB ein negatives Zugewiesen an? Ohne das liefe eine
        // Kategorie, die vom Vortrag lebt, beim Verschieben auf einen Fehler. Schreibt nur in das
        // Budget "Test" und stellt den alten Wert wieder her.
        testCase "YNAB nimmt ein negatives Zugewiesen an (Budget Test)" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None -> Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                let budget =
                    match getBudgets token |> Async.RunSynchronously with
                    | Ok budgets ->
                        match budgets |> List.tryFind (fun b -> b.Name = "Test") with
                        | Some b -> b.Id
                        | None -> Tests.skiptest "Budget 'Test' nicht vorhanden - skipping integration test"
                    | Error e -> failtest $"Budgets nicht lesbar: {e}"

                let month = DateTime.Today.ToString("yyyy-MM-01", Globalization.CultureInfo.InvariantCulture)
                let read () =
                    match getMonth token budget month |> Async.RunSynchronously with
                    | Ok m -> m
                    | Error e -> failtest $"Monat nicht lesbar: {e}"

                let category =
                    read().Categories
                    |> List.tryFind (fun c -> not c.Hidden && not c.Deleted && not c.Internal)
                    |> Option.defaultWith (fun () -> failtest "Keine Kategorie im Budget Test")
                let original = Shared.Budgeting.toMilliunits category.Assigned

                try
                    let result = setAssigned token budget month category.Id -50000L |> Async.RunSynchronously
                    Expect.isOk result "YNAB muss ein negatives Zugewiesen annehmen"
                    let after = read().Categories |> List.find (fun c -> c.Id = category.Id)
                    Expect.equal (Shared.Budgeting.toMilliunits after.Assigned) -50000L "YNAB führt den negativen Wert"
                finally
                    setAssigned token budget month category.Id original
                    |> Async.RunSynchronously
                    |> ignore

        // budgeting-eh8xe, ADR 0084 Punkt 5: die Quelle der Target-Übernahme. Die Goals aus /categories gleichen den
        // goal_*-Feldern des laufenden Monats (Art, Betrag, Rhythmus, Datum, Tag). Liest nur das Budget "Test".
        testCase "Die Goals aus /categories gleichen den Targets des laufenden Monats (Budget Test)" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None -> Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                let budget =
                    match getBudgets token |> Async.RunSynchronously with
                    | Ok budgets ->
                        match budgets |> List.tryFind (fun b -> b.Name = "Test") with
                        | Some b -> b.Id
                        | None -> Tests.skiptest "Budget 'Test' nicht vorhanden - skipping integration test"
                    | Error e -> failtest $"Budgets nicht lesbar: {e}"
                let berlin = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
                let month = Server.Budgeting.Domain.currentMonthKey (Server.Budgeting.Domain.berlinToday berlin DateTime.UtcNow)
                let monthTargets =
                    match getMonth token budget month |> Async.RunSynchronously with
                    | Ok m -> m.Categories |> List.map (fun c -> c.Id, c.Target) |> Map.ofList
                    | Error e -> failtest $"Monat nicht lesbar: {ynabErrorText e}"
                match getCategoryGoals token budget |> Async.RunSynchronously with
                | Error e -> failtest $"Goals nicht lesbar: {ynabErrorText e}"
                | Ok goals ->
                    let mutable compared = 0
                    for goal in goals |> List.filter (fun g -> g.Target.IsSome) do
                        match monthTargets |> Map.tryFind goal.Id with
                        | Some target ->
                            compared <- compared + 1
                            Expect.equal goal.Target target $"Goal von {goal.Name}"
                        | None -> ()
                    // Review-Fund eh8xe (QA D11): ohne einen Vergleich bewiese der Test nichts.
                    Expect.isGreaterThan compared 0 "mindestens ein Goal verglichen"

        testCase "can fetch budgets with real token" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None ->
                Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                async {
                    let! result = getBudgets token

                    match result with
                    | Ok budgets ->
                        Expect.isGreaterThan budgets.Length 0 "Should have at least one budget"

                        // Verify budget structure
                        let firstBudget = budgets.[0]
                        let (YnabBudgetId id) = firstBudget.Id
                        Expect.isNotNull id "Budget ID should not be null"
                        Expect.isNotEmpty firstBudget.Name "Budget name should not be empty"

                    | Error err ->
                        failtestf "Failed to fetch budgets: %A" err
                } |> Async.RunSynchronously

        testCase "can fetch budget details with real token" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None ->
                Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                async {
                    // First get budgets to find a valid budget ID
                    let! budgetsResult = getBudgets token

                    match budgetsResult with
                    | Error err ->
                        failtestf "Failed to fetch budgets: %A" err
                    | Ok budgets when budgets.IsEmpty ->
                        Tests.skiptest "No budgets available for testing"
                    | Ok budgets ->
                        let firstBudget = budgets.[0]

                        // Now fetch budget details
                        let! detailsResult = getBudgetWithAccounts token firstBudget.Id

                        match detailsResult with
                        | Ok details ->
                            Expect.equal details.Budget.Id firstBudget.Id "Budget IDs should match"
                            Expect.isGreaterThanOrEqual details.Accounts.Length 0 "Should have 0 or more accounts"
                            Expect.isGreaterThanOrEqual details.Categories.Length 0 "Should have 0 or more categories"

                            // Verify account structure if any accounts exist
                            if not details.Accounts.IsEmpty then
                                let firstAccount = details.Accounts.[0]
                                let (YnabAccountId accountId) = firstAccount.Id
                                Expect.isNotEmpty (accountId.ToString()) "Account ID should not be empty"
                                Expect.isNotEmpty firstAccount.Name "Account name should not be empty"

                            // Verify category structure if any categories exist
                            if not details.Categories.IsEmpty then
                                let firstCategory = details.Categories.[0]
                                let (YnabCategoryId categoryId) = firstCategory.Id
                                Expect.isNotEmpty (categoryId.ToString()) "Category ID should not be empty"
                                Expect.isNotEmpty firstCategory.Name "Category name should not be empty"
                                Expect.isNotEmpty firstCategory.GroupName "Category group name should not be empty"

                        | Error err ->
                            failtestf "Failed to fetch budget details: %A" err
                } |> Async.RunSynchronously

        testCase "can fetch categories with real token" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None ->
                Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                async {
                    // First get budgets
                    let! budgetsResult = getBudgets token

                    match budgetsResult with
                    | Error err ->
                        failtestf "Failed to fetch budgets: %A" err
                    | Ok budgets when budgets.IsEmpty ->
                        Tests.skiptest "No budgets available for testing"
                    | Ok budgets ->
                        let firstBudget = budgets.[0]

                        // Now fetch categories
                        let! categoriesResult = getCategories token firstBudget.Id

                        match categoriesResult with
                        | Ok categories ->
                            Expect.isGreaterThanOrEqual categories.Length 0 "Should have 0 or more categories"

                            // Verify all categories have group names (from flattening)
                            for category in categories do
                                Expect.isNotEmpty category.GroupName "All categories should have group names after flattening"

                        | Error err ->
                            failtestf "Failed to fetch categories: %A" err
                } |> Async.RunSynchronously

        testCase "validates token correctly" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None ->
                Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                async {
                    let! result = validateToken token

                    match result with
                    | Ok () ->
                        () // Success
                    | Error err ->
                        failtestf "Token validation failed: %A" err
                } |> Async.RunSynchronously

        testCase "handles invalid token gracefully" <| fun () ->
            // Ruft die echte YNAB-API auf, lief bis infrastructure-am805 ohne Schalter bei
            // jedem Testlauf (Fund des Netz-Wächters, NetworkCallGuardTests.fs).
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            let invalidToken = "invalid-token-12345"

            async {
                let! result = getBudgets invalidToken

                match result with
                | Ok _ ->
                    failtest "Should have failed with invalid token"
                | Error (YnabError.Unauthorized _) ->
                    () // Expected error
                | Error err ->
                    failtestf "Unexpected error type: %A" err
            } |> Async.RunSynchronously

        testCase "handles non-existent budget gracefully" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None ->
                Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                async {
                    let fakeBudgetId = YnabBudgetId "non-existent-budget-id"
                    let! result = getBudgetWithAccounts token fakeBudgetId

                    match result with
                    | Ok _ ->
                        failtest "Should have failed with non-existent budget"
                    | Error (YnabError.BudgetNotFound _) ->
                        () // Expected error
                    | Error (YnabError.InvalidResponse _) ->
                        () // Also acceptable (depending on YNAB API response)
                    | Error err ->
                        failtestf "Unexpected error type: %A" err
                } |> Async.RunSynchronously

        // ledger-m7a9q, ADR 0092 Punkt 4: zieht YNAB die Gegenbuchung mit, wenn BB den Betrag eines
        // Übertrags ändert? Teil 1: PUT `amount` (der Schreibweg `updateAmount`) auf einen Übertrag
        // zwischen zwei Budget-Konten, von beiden Seiten. Teil 2: der Ersatz (DELETE und POST,
        // ADR 0058) einer Split-Buchung mit Übertrags-Zeile und neuem Betrag, dazu ein PUT `amount`
        // auf die Gegenbuchung einer Übertrags-Zeile. Schreibt nur in das Budget "Test" und löscht
        // alles, was es anlegt.
        testCase "Probelauf: YNAB zieht die Gegenbuchung beim Betrag eines Übertrags mit (Budget Test)" <| fun () ->
            if not (shouldRunIntegrationTests()) then
                Tests.skiptest "RUN_INTEGRATION_TESTS not set to 'true' - skipping integration test (set RUN_INTEGRATION_TESTS=true in .env to enable)"

            match getEnvVar "YNAB_TOKEN" with
            | None -> Tests.skiptest "YNAB_TOKEN not set in .env - skipping integration test"
            | Some token ->
                let budget =
                    match getBudgets token |> Async.RunSynchronously with
                    | Ok budgets ->
                        match budgets |> List.tryFind (fun b -> b.Name = "Test") with
                        | Some b -> b.Id
                        | None -> Tests.skiptest "Budget 'Test' nicht vorhanden - skipping integration test"
                    | Error e -> failtest $"Budgets nicht lesbar: {e}"
                let (YnabBudgetId budgetIdText) = budget

                let accounts =
                    match fetchBudgetAccounts token budget |> Async.RunSynchronously with
                    | Ok accounts -> accounts |> List.filter (fun a -> a.OnBudget && not a.Closed)
                    | Error e -> failtest $"Konten nicht lesbar: {e}"
                let accountA, accountB =
                    match accounts with
                    | a :: b :: _ -> a, b
                    | _ -> failtest "Das Budget Test braucht zwei offene Budget-Konten"
                let category =
                    match getCategories token budget |> Async.RunSynchronously with
                    | Ok categories ->
                        categories
                        |> List.tryFind (fun c -> not c.Hidden && not (c.Name.StartsWith "Inflow") && c.GroupName <> "Credit Card Payments" && c.GroupName <> "Internal Master Category")
                        |> Option.defaultWith (fun () -> failtest "Keine Kategorie im Budget Test")
                    | Error e -> failtest $"Kategorien nicht lesbar: {e}"

                let eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

                // Liest eine Buchung roh (GET .../transactions/{id}): Betrag, Gegenbuchung, gelöscht,
                // Split-Zeilen (Betrag, Gegenbuchung).
                let readTransaction (id: string) =
                    use client = new System.Net.Http.HttpClient()
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}")
                    let response =
                        client.GetAsync($"https://api.youneedabudget.com/v1/budgets/{budgetIdText}/transactions/{id}")
                        |> Async.AwaitTask |> Async.RunSynchronously
                    let body = response.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
                    if int response.StatusCode = 404 then None
                    else
                        if int response.StatusCode <> 200 then failtest $"GET {id}: HTTP {int response.StatusCode}"
                        use doc = System.Text.Json.JsonDocument.Parse body
                        let tx = doc.RootElement.GetProperty("data").GetProperty("transaction")
                        let text (e: System.Text.Json.JsonElement) (name: string) =
                            match e.TryGetProperty name with
                            | true, v when v.ValueKind = System.Text.Json.JsonValueKind.String -> Some (v.GetString())
                            | _ -> None
                        let subs =
                            [ for s in tx.GetProperty("subtransactions").EnumerateArray() do
                                if not (s.GetProperty("deleted").GetBoolean()) then
                                    yield s.GetProperty("amount").GetInt64(), text s "transfer_transaction_id" ]
                        Some {| Amount = tx.GetProperty("amount").GetInt64()
                                Transfer = text tx "transfer_transaction_id"
                                Deleted = tx.GetProperty("deleted").GetBoolean()
                                Subs = subs |}
                let readLive id =
                    readTransaction id |> Option.filter (fun t -> not t.Deleted)

                let replacement amount transferTo splits : SplitReplacement =
                    { AccountId = accountA.Id
                      Date = DateTime.Today
                      Amount = eur amount
                      Payee = (if Option.isNone transferTo then Some "Probelauf ledger-m7a9q" else None)
                      Memo = Some "Probelauf ledger-m7a9q, wird gelöscht"
                      FlagColor = None
                      Cleared = "uncleared"
                      Approved = true
                      ImportId = None
                      CategoryId = None
                      Splits = splits
                      TransferAccountId = transferTo }

                let created = Collections.Generic.List<string>()
                try
                    // Teil 1: ein Übertrag A -> B über -12,34 €.
                    let transferId =
                        match createReplacement token budget (replacement -12.34m (Some accountB.Id) []) |> Async.RunSynchronously with
                        | Ok id -> id
                        | Error e -> failtest $"Übertrag nicht angelegt: {e}"
                    created.Add transferId
                    let before = readLive transferId |> Option.get
                    let counterpartId = before.Transfer |> Option.defaultWith (fun () -> failtest "Übertrag ohne Gegenbuchung")
                    printfn "[Probelauf] Übertrag %s: %d, Gegenbuchung %s: %d" transferId before.Amount counterpartId (readLive counterpartId |> Option.get).Amount

                    let putA = updateAmount token budget transferId (eur -20m) |> Async.RunSynchronously
                    printfn "[Probelauf] PUT amount -20000 auf %s: %A" transferId putA
                    Expect.isOk putA "YNAB nimmt den Betrag eines Übertrags an"
                    let afterA = readLive transferId |> Option.get
                    let counterpartAfterA = readLive counterpartId
                    printfn "[Probelauf] danach Übertrag %d, Gegenbuchung %A" afterA.Amount counterpartAfterA
                    Expect.equal afterA.Amount -20000L "der Übertrag trägt den neuen Betrag"
                    Expect.equal afterA.Transfer (Some counterpartId) "die Gegenbuchung behält ihre Id"
                    Expect.equal (counterpartAfterA |> Option.map (fun c -> c.Amount)) (Some 20000L) "YNAB zieht die Gegenbuchung mit"

                    let putB = updateAmount token budget counterpartId (eur 7.5m) |> Async.RunSynchronously
                    printfn "[Probelauf] PUT amount 7500 auf die Gegenbuchung %s: %A" counterpartId putB
                    Expect.isOk putB "YNAB nimmt den Betrag der Gegenbuchung an"
                    let afterB = readLive transferId
                    printfn "[Probelauf] danach Übertrag %A, Gegenbuchung %A" afterB (readLive counterpartId)
                    Expect.equal (afterB |> Option.map (fun t -> t.Amount)) (Some -7500L) "YNAB zieht auch von der Gegenseite mit"

                    // Teil 2: Split auf A über -15 €: -10 € Kategorie, -5 € Übertrag nach B.
                    let splitLines (categoryPart: decimal) (transferPart: decimal) : TransactionSplit list =
                        [ { Target = ToCategory (category.Id, category.Name); Amount = eur categoryPart; Memo = None }
                          { Target = ToTransfer (accountB.Id, accountB.Name); Amount = eur transferPart; Memo = None } ]
                    let splitId =
                        match createReplacement token budget (replacement -15m None (splitLines -10m -5m)) |> Async.RunSynchronously with
                        | Ok id -> id
                        | Error e -> failtest $"Split nicht angelegt: {e}"
                    created.Add splitId
                    let split = readLive splitId |> Option.get
                    let oldLineCounterpart =
                        split.Subs |> List.choose snd |> List.tryHead
                        |> Option.defaultWith (fun () -> failtest "Übertrags-Zeile ohne Gegenbuchung")
                    printfn "[Probelauf] Split %s: %A, Gegenbuchung der Zeile %A" splitId split (readLive oldLineCounterpart)

                    let newSplitId =
                        match replaceSplitTransaction token budget splitId (replacement -25m None (splitLines -10m -15m)) |> Async.RunSynchronously with
                        | Ok id -> id
                        | Error e -> failtest $"Ersatz gescheitert: {e}"
                    created.Remove splitId |> ignore
                    created.Add newSplitId
                    let newSplit = readLive newSplitId |> Option.get
                    let newLineCounterpart =
                        newSplit.Subs |> List.choose snd |> List.tryHead
                        |> Option.defaultWith (fun () -> failtest "Ersatz: Übertrags-Zeile ohne Gegenbuchung")
                    let newCounterpart = readLive newLineCounterpart
                    printfn "[Probelauf] Ersatz %s: %A, alte Gegenbuchung %A, neue Gegenbuchung %s: %A"
                        newSplitId newSplit (readTransaction oldLineCounterpart) newLineCounterpart newCounterpart
                    Expect.isNone (readLive oldLineCounterpart) "die alte Gegenbuchung ist gelöscht"
                    Expect.equal (newCounterpart |> Option.map (fun c -> c.Amount)) (Some 15000L) "die neue Gegenbuchung trägt den neuen Zeilenbetrag"

                    // Die Gegenbuchung einer Übertrags-Zeile selbst per PUT ändern: was macht YNAB mit der Zeile?
                    let putLine = updateAmount token budget newLineCounterpart (eur 3m) |> Async.RunSynchronously
                    printfn "[Probelauf] PUT amount 3000 auf die Gegenbuchung %s der Zeile: %A, danach Split %A, Gegenbuchung %A"
                        newLineCounterpart putLine (readTransaction newSplitId) (readTransaction newLineCounterpart)
                    Expect.isError putLine "YNAB lehnt den Betrag der Gegenbuchung einer Übertrags-Zeile ab (400)"
                    Expect.equal (readLive newSplitId |> Option.map (fun t -> t.Subs)) (Some newSplit.Subs) "die Split-Zeilen bleiben"
                    Expect.equal (readLive newLineCounterpart |> Option.map (fun c -> c.Amount)) (Some 15000L) "die Gegenbuchung bleibt"
                finally
                    for id in created do
                        let result = deleteTransaction token budget id |> Async.RunSynchronously
                        printfn "[Probelauf] Aufräumen DELETE %s: %A" id result
    ]
