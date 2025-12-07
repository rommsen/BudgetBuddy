#!/usr/bin/env dotnet fsi

// ============================================
// Legacy Rules Import Script
// ============================================
//
// Imports rules from rules.yml (legacy format) into the BudgetBuddy database.
// Fetches YNAB categories to match category names to IDs.
//
// Usage:
//   dotnet fsi scripts/import-rules.fsx                    # Uses first budget
//   dotnet fsi scripts/import-rules.fsx "My Budget"        # Uses specific budget
//   dotnet fsi scripts/import-rules.fsx --list             # Lists available budgets
//   dotnet fsi scripts/import-rules.fsx --clear            # Deletes all rules before import
//   dotnet fsi scripts/import-rules.fsx --clear "My Budget"  # Clear + import with budget
//
// Requirements:
//   - .env with YNAB_TOKEN
//   - rules.yml in project root

#r "nuget: FsHttp, 14.5.1"
#r "nuget: Thoth.Json.Net, 12.0.0"
#r "nuget: Microsoft.Data.Sqlite, 8.0.0"
#r "nuget: Dapper, 2.1.35"

#load "EnvLoader.fsx"
#load "../src/Shared/Domain.fs"
#load "../src/Server/YnabClient.fs"

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Data.Sqlite
open Dapper
open EnvLoader
open Shared.Domain
open Server.YnabClient

// ============================================
// YAML Parsing (Simple regex-based)
// ============================================

type YamlRule = { Match: string; Category: string }

let parseYamlRules (yamlContent: string) : YamlRule list =
    let pattern = @"-\s*match:\s*""([^""]+)""\s*\n\s*category:\s*""([^""]+)"""
    let matches = Regex.Matches(yamlContent, pattern, RegexOptions.Multiline)

    matches
    |> Seq.cast<Match>
    |> Seq.map (fun m ->
        { Match = m.Groups.[1].Value
          Category = m.Groups.[2].Value })
    |> Seq.toList

// ============================================
// Category Matching
// ============================================

let matchCategory (categoryName: string) (categories: YnabCategory list) : YnabCategory option =
    // Strategy 1: Exact match on Name
    let exactMatch = categories |> List.tryFind (fun c -> c.Name = categoryName)
    match exactMatch with
    | Some cat -> Some cat
    | None ->
        // Strategy 2: Match on "GroupName/Name" format
        let slashMatch =
            categories
            |> List.tryFind (fun c -> $"{c.GroupName}/{c.Name}" = categoryName)
        match slashMatch with
        | Some cat -> Some cat
        | None ->
            // Strategy 3: Case-insensitive exact match
            let caseInsensitiveMatch =
                categories
                |> List.tryFind (fun c ->
                    String.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase))
            match caseInsensitiveMatch with
            | Some cat -> Some cat
            | None ->
                // Strategy 4: Case-insensitive "GroupName/Name" match
                categories
                |> List.tryFind (fun c ->
                    String.Equals($"{c.GroupName}/{c.Name}", categoryName, StringComparison.OrdinalIgnoreCase))

// ============================================
// Database Operations
// ============================================

let getDbPath () =
    let dataDir =
        match Environment.GetEnvironmentVariable("DATA_DIR") with
        | null | "" -> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "heimeshoff", "budgetbuddy")
        | path -> path
    Path.Combine(dataDir, "budgetbuddy.db")

let getConnection () =
    let dbPath = getDbPath()
    let connStr = $"Data Source={dbPath}"
    let conn = new SqliteConnection(connStr)
    conn.Open()
    conn

let ruleExists (conn: SqliteConnection) (pattern: string) : bool =
    let count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM rules WHERE pattern = @Pattern", {| Pattern = pattern |})
    count > 0

let deleteAllRules (conn: SqliteConnection) : int =
    conn.Execute("DELETE FROM rules")

let insertRule (conn: SqliteConnection) (rule: Rule) : unit =
    let (RuleId ruleId) = rule.Id
    let (YnabCategoryId categoryId) = rule.CategoryId
    conn.Execute(
        """INSERT INTO rules
           (id, name, pattern, pattern_type, target_field, category_id, category_name,
            payee_override, priority, enabled, created_at, updated_at)
           VALUES (@id, @name, @pattern, @pattern_type, @target_field, @category_id,
                   @category_name, @payee_override, @priority, @enabled, @created_at, @updated_at)""",
        {|
            id = ruleId.ToString()
            name = rule.Name
            pattern = rule.Pattern
            pattern_type = "Contains"
            target_field = "Combined"
            category_id = categoryId.ToString()
            category_name = rule.CategoryName
            payee_override = (rule.PayeeOverride |> Option.defaultValue null)
            priority = rule.Priority
            enabled = if rule.Enabled then 1 else 0
            created_at = rule.CreatedAt.ToString("O")
            updated_at = rule.UpdatedAt.ToString("O")
        |}
    ) |> ignore

// ============================================
// Main Script
// ============================================

// Parse command line arguments
let args = fsi.CommandLineArgs |> Array.skip 1 // Skip script name
let listOnly = args |> Array.contains "--list"
let clearFirst = args |> Array.contains "--clear"
let budgetNameArg =
    args
    |> Array.filter (fun a -> not (a.StartsWith("--")))
    |> Array.tryHead

printfn ""
printfn "==================================================="
printfn "Legacy Rules Import"
printfn "==================================================="
printfn ""

// 1. Load environment
let env = EnvLoader.loadProjectEnv()
let token = EnvLoader.getRequired env "YNAB_TOKEN"
let projectRoot = EnvLoader.getProjectRoot()

// 2. Load and parse rules.yml
let rulesPath = Path.Combine(projectRoot, "rules.yml")
if not (File.Exists rulesPath) then
    printfn "ERROR: rules.yml not found at %s" rulesPath
    exit 1

printfn "Loading rules.yml..."
let yamlContent = File.ReadAllText(rulesPath)
let yamlRules = parseYamlRules yamlContent
printfn "   Found %d rules in YAML" yamlRules.Length
printfn ""

// 3. Fetch budgets and categories from YNAB
printfn "Connecting to YNAB..."
let budgetsResult = getBudgets token |> Async.RunSynchronously

match budgetsResult with
| Error err ->
    printfn "ERROR: Could not fetch budgets: %A" err
    exit 1
| Ok budgets ->
    if budgets.IsEmpty then
        printfn "ERROR: No budgets found in YNAB"
        exit 1

    // List mode: just show budgets and exit
    if listOnly then
        printfn "Available budgets:"
        for b in budgets do
            let (YnabBudgetId id) = b.Id
            printfn "   - %s (ID: %s)" b.Name id
        printfn ""
        printfn "Usage: dotnet fsi scripts/import-rules.fsx \"Budget Name\""
        exit 0

    // Filter out archived budgets (they have "(Archived" in name)
    let activeBudgets = budgets |> List.filter (fun b -> not (b.Name.Contains("(Archived")))

    // Select budget
    let budget =
        match budgetNameArg with
        | Some name ->
            // First try active budgets
            match activeBudgets |> List.tryFind (fun b -> b.Name.Contains(name)) with
            | Some b -> b
            | None ->
                // Then try all budgets
                match budgets |> List.tryFind (fun b -> b.Name.Contains(name)) with
                | Some b -> b
                | None ->
                    printfn "ERROR: Budget '%s' not found. Available budgets:" name
                    for b in budgets do printfn "   - %s" b.Name
                    exit 1
        | None ->
            if activeBudgets.IsEmpty then budgets.[0]
            else activeBudgets.[0]

    let (YnabBudgetId budgetIdStr) = budget.Id
    printfn "   Using budget: %s" budget.Name

    let categoriesResult = getCategories token budget.Id |> Async.RunSynchronously

    match categoriesResult with
    | Error err ->
        printfn "ERROR: Could not fetch categories: %A" err
        exit 1
    | Ok categories ->
        printfn "   Found %d categories" categories.Length
        printfn ""

        // 4. Connect to database
        let dbPath = getDbPath()
        printfn "Database: %s" dbPath
        if not (File.Exists dbPath) then
            printfn "ERROR: Database not found. Please run the app first to initialize the database."
            exit 1

        use conn = getConnection()
        printfn ""

        // 5. Clear existing rules if requested
        if clearFirst then
            let deleted = deleteAllRules conn
            printfn "Cleared %d existing rules from database" deleted
            printfn ""

        // 6. Import rules
        printfn "Importing rules..."
        printfn "---------------------------------------------------"

        let mutable imported = 0
        let mutable skippedExists = 0
        let mutable skippedNoMatch = 0
        let mutable priority = yamlRules.Length  // Start with high priority, descend

        for yamlRule in yamlRules do
            // Check if rule already exists
            if ruleExists conn yamlRule.Match then
                printfn "   [skip] %s (already exists)" yamlRule.Match
                skippedExists <- skippedExists + 1
            else
                // Try to match category
                match matchCategory yamlRule.Category categories with
                | None ->
                    printfn "   [WARN] %s -> %s (category not found)" yamlRule.Match yamlRule.Category
                    skippedNoMatch <- skippedNoMatch + 1
                | Some cat ->
                    let (YnabCategoryId catId) = cat.Id
                    let rule : Rule = {
                        Id = RuleId (Guid.NewGuid())
                        Name = yamlRule.Match  // Use match pattern as name
                        Pattern = yamlRule.Match
                        PatternType = Contains
                        TargetField = Combined
                        CategoryId = cat.Id
                        CategoryName = cat.Name
                        PayeeOverride = None
                        Priority = priority
                        Enabled = true
                        CreatedAt = DateTime.UtcNow
                        UpdatedAt = DateTime.UtcNow
                    }

                    insertRule conn rule
                    printfn "   [OK]   %s -> %s (%s/%s)" yamlRule.Match yamlRule.Category cat.GroupName cat.Name
                    imported <- imported + 1
                    priority <- priority - 1

        printfn ""
        printfn "==================================================="
        printfn "Import Summary"
        printfn "==================================================="
        printfn ""
        printfn "   Imported:              %d" imported
        printfn "   Skipped (exists):      %d" skippedExists
        printfn "   Skipped (no category): %d" skippedNoMatch
        printfn ""

        if skippedNoMatch > 0 then
            printfn "Categories not found in YNAB:"
            let notFound =
                yamlRules
                |> List.filter (fun r -> matchCategory r.Category categories |> Option.isNone)
                |> List.map (fun r -> r.Category)
                |> List.distinct
            for cat in notFound do
                printfn "   - %s" cat
            printfn ""

        printfn "Done!"
