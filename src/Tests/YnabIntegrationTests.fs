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
    ]
