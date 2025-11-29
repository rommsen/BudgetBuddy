#!/usr/bin/env dotnet fsi

// ============================================
// YNAB API Integration Test Script
// ============================================
//
// Usage:
//   1. Copy .env.example to .env and fill in YNAB_TOKEN
//   2. Run: dotnet fsi scripts/test-ynab.fsx
//
// This script will:
//   - Load YNAB token from .env
//   - Test getBudgets
//   - Test getBudgetDetails for first budget
//   - Test getCategories
//   - Display results

#r "nuget: FsHttp, 14.5.1"
#r "nuget: Thoth.Json.Net, 12.0.0"

#load "EnvLoader.fsx"
#load "../src/Shared/Domain.fs"
#load "../src/Server/YnabClient.fs"

open System
open EnvLoader
open Shared.Domain
open Server.YnabClient

// ============================================
// Load Environment
// ============================================

printfn "==================================================="
printfn "YNAB API Integration Test"
printfn "==================================================="
printfn ""

let env = EnvLoader.loadProjectEnv()
EnvLoader.printEnvInfo env

let token = EnvLoader.getRequired env "YNAB_TOKEN"

printfn "Starting YNAB API tests...\n"

// ============================================
// Test 1: Get Budgets
// ============================================

printfn "Test 1: Fetching budgets..."
let budgetsResult = getBudgets token |> Async.RunSynchronously

match budgetsResult with
| Ok budgets ->
    printfn $"✅ SUCCESS: Found {budgets.Length} budget(s)"
    for budget in budgets do
        let (YnabBudgetId id) = budget.Id
        printfn $"   - {budget.Name} (ID: {id})"
    printfn ""

    // ============================================
    // Test 2: Get Budget Details (first budget)
    // ============================================

    if budgets.Length > 0 then
        let firstBudget = budgets.[0]
        printfn $"Test 2: Fetching details for budget '{firstBudget.Name}'..."

        let detailsResult = getBudgetWithAccounts token firstBudget.Id |> Async.RunSynchronously

        match detailsResult with
        | Ok details ->
            printfn $"✅ SUCCESS: Budget details loaded"
            printfn $"   Budget: {details.Budget.Name}"
            printfn $"   Accounts: {details.Accounts.Length}"
            for account in details.Accounts do
                printfn $"      - {account.Name}: {account.Balance.Amount} {account.Balance.Currency}"
            printfn $"   Categories: {details.Categories.Length}"

            // Group categories by group name
            let categoryGroups =
                details.Categories
                |> List.groupBy (fun c -> c.GroupName)
                |> List.sortBy fst

            for (groupName, cats) in categoryGroups do
                printfn $"      [{groupName}]"
                for cat in cats do
                    printfn $"         - {cat.Name}"
            printfn ""

            // ============================================
            // Test 3: Get Categories
            // ============================================

            printfn $"Test 3: Fetching categories for budget '{firstBudget.Name}'..."
            let categoriesResult = getCategories token firstBudget.Id |> Async.RunSynchronously

            match categoriesResult with
            | Ok categories ->
                printfn $"✅ SUCCESS: Found {categories.Length} categories"
                printfn ""

                // ============================================
                // Test 4: Validate Token
                // ============================================

                printfn "Test 4: Validating YNAB token..."
                let validateResult = validateToken token |> Async.RunSynchronously

                match validateResult with
                | Ok () ->
                    printfn "✅ SUCCESS: Token is valid"
                    printfn ""

                    // ============================================
                    // Summary
                    // ============================================

                    printfn "==================================================="
                    printfn "ALL TESTS PASSED ✅"
                    printfn "==================================================="
                    printfn ""
                    printfn "Summary:"
                    printfn $"  - Budgets: {budgets.Length}"
                    printfn $"  - Accounts in first budget: {details.Accounts.Length}"
                    printfn $"  - Categories in first budget: {categories.Length}"
                    printfn ""
                    printfn "Your YNAB integration is working correctly!"
                    printfn ""

                | Error err ->
                    printfn $"❌ FAILED: Token validation failed"
                    printfn $"   Error: {err}"

            | Error err ->
                printfn $"❌ FAILED: Could not fetch categories"
                printfn $"   Error: {err}"

        | Error err ->
            printfn $"❌ FAILED: Could not fetch budget details"
            printfn $"   Error: {err}"
    else
        printfn "⚠️  No budgets found - cannot continue with further tests"

| Error err ->
    printfn $"❌ FAILED: Could not fetch budgets"
    printfn $"   Error: {err}"
    printfn ""
    printfn "Please check:"
    printfn "  1. Your YNAB_TOKEN in .env is correct"
    printfn "  2. You have an active YNAB account"
    printfn "  3. Your internet connection is working"
