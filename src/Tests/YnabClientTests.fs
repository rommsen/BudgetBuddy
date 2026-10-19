module YnabClientTests

open System
open Expecto
open Thoth.Json.Net
open Shared.Domain
open Server.YnabClient

// ============================================
// Sample JSON Responses (from YNAB API documentation)
// ============================================

module SampleData =
    let budgetsJson = """
    {
        "data": {
            "budgets": [
                {
                    "id": "budget-123",
                    "name": "My Budget"
                },
                {
                    "id": "budget-456",
                    "name": "Test Budget"
                }
            ]
        }
    }
    """

    // Budget detail JSON with category_groups (matches real YNAB API format)
    // Note: Internal Master Category has no "categories" field - this matches real YNAB API behavior
    let budgetDetailJson = """
    {
        "data": {
            "budget": {
                "id": "budget-123",
                "name": "My Budget",
                "accounts": [
                    {
                        "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                        "name": "Checking Account",
                        "balance": 250000
                    },
                    {
                        "id": "f1e2d3c4-b5a6-7890-1234-567890abcdef",
                        "name": "Savings Account",
                        "balance": 1500000
                    }
                ],
                "category_groups": [
                    {
                        "id": "group-internal",
                        "name": "Internal Master Category",
                        "hidden": false,
                        "deleted": false
                    },
                    {
                        "id": "group-1",
                        "name": "Essential Expenses",
                        "categories": [
                            {
                                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                                "name": "Groceries"
                            },
                            {
                                "id": "d1e2f3a4-b5c6-7890-1234-567890abcdef",
                                "name": "Rent"
                            }
                        ]
                    },
                    {
                        "id": "group-2",
                        "name": "Fun Money",
                        "categories": [
                            {
                                "id": "e1f2a3b4-c5d6-7890-1234-567890fedcba",
                                "name": "Entertainment"
                            }
                        ]
                    }
                ]
            }
        }
    }
    """

    let categoriesJson = """
    {
        "data": {
            "category_groups": [
                {
                    "id": "group-1",
                    "name": "Essential Expenses",
                    "categories": [
                        {
                            "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                            "name": "Groceries",
                            "category_group_name": "Essential Expenses"
                        },
                        {
                            "id": "d1e2f3a4-b5c6-7890-1234-567890abcdef",
                            "name": "Rent",
                            "category_group_name": "Essential Expenses"
                        }
                    ]
                },
                {
                    "id": "group-2",
                    "name": "Fun Money",
                    "categories": [
                        {
                            "id": "e1f2a3b4-c5d6-7890-1234-567890fedcba",
                            "name": "Entertainment",
                            "category_group_name": "Fun Money"
                        }
                    ]
                }
            ]
        }
    }
    """

    let invalidJsonResponse = """
    {
        "error": {
            "id": "401",
            "name": "unauthorized",
            "detail": "Unauthorized"
        }
    }
    """

    let malformedJson = """
    {
        "data": {
            "budgets": [
                {
                    "id": "budget-123"
                    // Missing "name" field
                }
            ]
        }
    """

// ============================================
// JSON Decoder Tests
// ============================================

[<Tests>]
let budgetDecoderTests =
    testList "YNAB Budget Decoder Tests" [
        testCase "decodes budget correctly" <| fun () ->
            let json = """{"id": "budget-123", "name": "My Budget"}"""
            let result = Decode.fromString Decoders.budgetDecoder json

            match result with
            | Ok budget ->
                let (YnabBudgetId id) = budget.Id
                Expect.equal id "budget-123" "Budget ID should match"
                Expect.equal budget.Name "My Budget" "Budget name should match"
            | Error err ->
                failtest $"Failed to decode budget: {err}"

        testCase "decodes budget list from API response" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budgets" (Decode.list Decoders.budgetDecoder))
            let result = Decode.fromString decoder SampleData.budgetsJson

            match result with
            | Ok budgets ->
                Expect.hasLength budgets 2 "Should have 2 budgets"
                let (YnabBudgetId id1) = budgets.[0].Id
                Expect.equal id1 "budget-123" "First budget ID should match"
                Expect.equal budgets.[0].Name "My Budget" "First budget name should match"
            | Error err ->
                failtest $"Failed to decode budgets: {err}"

        testCase "fails on malformed budget JSON" <| fun () ->
            let json = """{"id": "budget-123"}"""  // Missing "name"
            let result = Decode.fromString Decoders.budgetDecoder json

            match result with
            | Ok _ -> failtest "Should have failed on missing field"
            | Error _ -> ()  // Expected
    ]

[<Tests>]
let accountDecoderTests =
    testList "YNAB Account Decoder Tests" [
        testCase "decodes account with milliunits conversion" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Checking Account",
                "balance": 250000
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                let (YnabAccountId id) = account.Id
                Expect.equal (id.ToString()) "a1b2c3d4-e5f6-7890-abcd-ef1234567890" "Account ID should match"
                Expect.equal account.Name "Checking Account" "Account name should match"
                Expect.equal account.Balance.Amount 250m "Balance should be converted from milliunits (250000 / 1000 = 250)"
                Expect.equal account.Balance.Currency "EUR" "Currency should be EUR"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        testCase "handles negative balance correctly" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Credit Card",
                "balance": -150000
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.equal account.Balance.Amount -150m "Negative balance should be converted correctly"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        testCase "handles zero balance" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Empty Account",
                "balance": 0,
                "on_budget": true,
                "closed": false
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.equal account.Balance.Amount 0m "Zero balance should be handled correctly"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        // ynab-002: the transfer-target picker filters to open On-Budget accounts,
        // which requires the decoder to surface YNAB's on_budget/closed flags.
        testCase "decodes on_budget and closed flags from the YNAB account payload" <| fun () ->
            let json = """
            {
                "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Bargeld",
                "balance": 5000,
                "on_budget": true,
                "closed": false
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.isTrue account.OnBudget "on_budget true should decode to OnBudget = true"
                Expect.isFalse account.Closed "closed false should decode to Closed = false"
            | Error err ->
                failtest $"Failed to decode account: {err}"

        testCase "decodes an off-budget, closed account preserving both flags" <| fun () ->
            let json = """
            {
                "id": "b1b2c3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Altes Sparbuch",
                "balance": 0,
                "on_budget": false,
                "closed": true
            }
            """
            let result = Decode.fromString Decoders.accountDecoder json

            match result with
            | Ok account ->
                Expect.isFalse account.OnBudget "on_budget false should decode to OnBudget = false"
                Expect.isTrue account.Closed "closed true should decode to Closed = true"
            | Error err ->
                failtest $"Failed to decode account: {err}"
    ]

[<Tests>]
let categoryDecoderTests =
    testList "YNAB Category Decoder Tests" [
        testCase "decodes category correctly" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "category_group_name": "Essential Expenses"
            }
            """
            let result = Decode.fromString Decoders.categoryDecoder json

            match result with
            | Ok category ->
                let (YnabCategoryId id) = category.Id
                Expect.equal (id.ToString()) "c1b2a3d4-e5f6-7890-abcd-ef1234567890" "Category ID should match"
                Expect.equal category.Name "Groceries" "Category name should match"
                Expect.equal category.GroupName "Essential Expenses" "Category group name should match"
            | Error err ->
                failtest $"Failed to decode category: {err}"

        // --- Regression: YNAB `balance` -> YnabCategory.Available (milliunits/1000) ---
        // These prevent the category Available column in the picker from silently
        // showing a wrong value (e.g. raw milliunits, or a dropped sign). Every
        // field of the decoded record is asserted (incl. Available.Currency).
        testCase "decodes positive balance into Available" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "category_group_name": "Essential Expenses",
                "balance": 250000
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category ->
                let (YnabCategoryId id) = category.Id
                Expect.equal (id.ToString()) "c1b2a3d4-e5f6-7890-abcd-ef1234567890" "Id should match"
                Expect.equal category.Name "Groceries" "Name should match"
                Expect.equal category.GroupName "Essential Expenses" "GroupName should match"
                Expect.equal category.Available (Some { Amount = 250m; Currency = "EUR" }) "Positive balance 250000 milliunits should decode to Some 250 EUR"
            | Error err -> failtest $"Failed to decode category: {err}"

        testCase "decodes negative balance into Available" <| fun () ->
            let json = """
            {
                "id": "d1e2f3a4-b5c6-7890-1234-567890abcdef",
                "name": "Rent",
                "category_group_name": "Essential Expenses",
                "balance": -150000
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category ->
                let (YnabCategoryId id) = category.Id
                Expect.equal (id.ToString()) "d1e2f3a4-b5c6-7890-1234-567890abcdef" "Id should match"
                Expect.equal category.Name "Rent" "Name should match"
                Expect.equal category.GroupName "Essential Expenses" "GroupName should match"
                Expect.equal category.Available (Some { Amount = -150m; Currency = "EUR" }) "Negative balance -150000 milliunits should decode to Some -150 EUR"
            | Error err -> failtest $"Failed to decode category: {err}"

        testCase "decodes zero balance into Available" <| fun () ->
            let json = """
            {
                "id": "e1f2a3b4-c5d6-7890-1234-567890fedcba",
                "name": "Entertainment",
                "category_group_name": "Fun Money",
                "balance": 0
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category ->
                let (YnabCategoryId id) = category.Id
                Expect.equal (id.ToString()) "e1f2a3b4-c5d6-7890-1234-567890fedcba" "Id should match"
                Expect.equal category.Name "Entertainment" "Name should match"
                Expect.equal category.GroupName "Fun Money" "GroupName should match"
                Expect.equal category.Available (Some { Amount = 0m; Currency = "EUR" }) "Zero balance should decode to Some 0 EUR"
            | Error err -> failtest $"Failed to decode category: {err}"

        testCase "missing balance decodes Available to None" <| fun () ->
            // Conformist-safe: a category shipped without a balance field decodes to
            // None (no value shown) rather than a misleading 0 or a failed load.
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "category_group_name": "Essential Expenses"
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category ->
                Expect.equal category.Available None "Missing balance should decode Available to None"
            | Error err -> failtest $"Failed to decode category: {err}"

        // --- Regression: YNAB `hidden` -> YnabCategory.Hidden (ledger-j8xq4 AC3) ---
        // Drives the collapsed "Versteckt (n)" group in the Kassenbuch category
        // picker; must come from the field itself, never a naming convention.
        testCase "categoryDecoder: hidden true decodes to Hidden = true" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Umzug 2023",
                "category_group_name": "Alltag",
                "hidden": true
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category -> Expect.isTrue category.Hidden "hidden: true must decode to Hidden = true"
            | Error err -> failtest $"Failed to decode category: {err}"

        testCase "categoryDecoder: missing hidden field decodes to Hidden = false" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "category_group_name": "Essential Expenses"
            }
            """
            match Decode.fromString Decoders.categoryDecoder json with
            | Ok category -> Expect.isFalse category.Hidden "a missing hidden field must decode to Hidden = false, conformist-safe"
            | Error err -> failtest $"Failed to decode category: {err}"

        testCase "categoryInGroupDecoder: hidden true decodes to Hidden = true" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Umzug 2023",
                "hidden": true
            }
            """
            match Decode.fromString (Decoders.categoryInGroupDecoder "Alltag") json with
            | Ok category -> Expect.isTrue category.Hidden "hidden: true must decode to Hidden = true in the nested-group path too"
            | Error err -> failtest $"Failed to decode category in group: {err}"

        testCase "categoryInGroupDecoder: missing hidden field decodes to Hidden = false" <| fun () ->
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries"
            }
            """
            match Decode.fromString (Decoders.categoryInGroupDecoder "Essential Expenses") json with
            | Ok category -> Expect.isFalse category.Hidden "a missing hidden field must decode to Hidden = false"
            | Error err -> failtest $"Failed to decode category in group: {err}"

        testCase "categoryInGroupDecoder reads balance into Available" <| fun () ->
            // The /budgets/{id} path decodes nested categories via categoryInGroupDecoder;
            // it must read balance too (else the split/budget-detail load loses Available).
            let json = """
            {
                "id": "c1b2a3d4-e5f6-7890-abcd-ef1234567890",
                "name": "Groceries",
                "balance": 42000
            }
            """
            match Decode.fromString (Decoders.categoryInGroupDecoder "Essential Expenses") json with
            | Ok category ->
                Expect.equal category.Name "Groceries" "Name should match"
                Expect.equal category.GroupName "Essential Expenses" "GroupName comes from the group"
                Expect.equal category.Available (Some { Amount = 42m; Currency = "EUR" }) "balance 42000 milliunits should decode to Some 42 EUR"
            | Error err -> failtest $"Failed to decode category in group: {err}"

        testCase "decodes category groups and flattens correctly" <| fun () ->
            let decoder =
                Decode.field "data" (
                    Decode.field "category_groups" (
                        Decode.list (
                            Decode.object (fun get ->
                                let groupName = get.Required.Field "name" Decode.string
                                let categories = get.Required.Field "categories" (Decode.list Decoders.categoryDecoder)
                                categories |> List.map (fun cat -> { cat with GroupName = groupName })
                            )
                        )
                    )
                )

            let result = Decode.fromString decoder SampleData.categoriesJson

            match result with
            | Ok categoryGroups ->
                let allCategories = List.concat categoryGroups
                Expect.hasLength allCategories 3 "Should have 3 categories total"

                // Verify group names are correctly assigned
                let essentialCategories = allCategories |> List.filter (fun c -> c.GroupName = "Essential Expenses")
                Expect.hasLength essentialCategories 2 "Should have 2 Essential Expenses categories"

                let funCategories = allCategories |> List.filter (fun c -> c.GroupName = "Fun Money")
                Expect.hasLength funCategories 1 "Should have 1 Fun Money category"
            | Error err ->
                failtest $"Failed to decode category groups: {err}"
    ]

[<Tests>]
let budgetDetailDecoderTests =
    testList "YNAB Budget Detail Decoder Tests" [
        testCase "decodes complete budget with accounts and categories" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budget" Decoders.budgetDetailDecoder)
            let result = Decode.fromString decoder SampleData.budgetDetailJson

            match result with
            | Ok budgetDetail ->
                // Verify budget
                let (YnabBudgetId id) = budgetDetail.Budget.Id
                Expect.equal id "budget-123" "Budget ID should match"
                Expect.equal budgetDetail.Budget.Name "My Budget" "Budget name should match"

                // Verify accounts
                Expect.hasLength budgetDetail.Accounts 2 "Should have 2 accounts"
                let checkingAccount = budgetDetail.Accounts |> List.find (fun a -> a.Name = "Checking Account")
                Expect.equal checkingAccount.Balance.Amount 250m "Checking account balance should be 250"

                // Verify categories
                Expect.hasLength budgetDetail.Categories 3 "Should have 3 categories"
                let groceriesCategory = budgetDetail.Categories |> List.find (fun c -> c.Name = "Groceries")
                Expect.equal groceriesCategory.GroupName "Essential Expenses" "Groceries should be in Essential Expenses group"
            | Error err ->
                failtest $"Failed to decode budget detail: {err}"
    ]

[<Tests>]
let readyToAssignTests =
    testList "YNAB Ready to Assign (regression: internal Inflow balance is garbage)" [
        // Regression for the picker bug: GET /budgets/{id}/categories reports an
        // internal accumulator as the "Inflow: Ready to Assign" balance (observed
        // 787955.35 in the wild), NOT the real Ready-to-Assign. The real value is the
        // month's to_be_budgeted; applyReadyToAssign must replace the Inflow row with
        // it and leave every normal category untouched.
        let mkCat name group avail : YnabCategory =
            { Id = YnabCategoryId (Guid.NewGuid()); Name = name; GroupName = group; Available = avail; Hidden = false }

        testCase "applyReadyToAssign overrides only the Inflow: Ready to Assign row" <| fun () ->
            let normal = mkCat "Groceries" "Essential Expenses" (Some { Amount = 173.10m; Currency = "EUR" })
            let rta = mkCat "Inflow: Ready to Assign" "Internal Master Category" (Some { Amount = 787955.35m; Currency = "EUR" })
            let realRta = Some { Amount = -6.27m; Currency = "EUR" }

            let result = applyReadyToAssign realRta [ normal; rta ]
            let resultRta = result |> List.find (fun c -> c.Name = "Inflow: Ready to Assign")
            let resultNormal = result |> List.find (fun c -> c.Name = "Groceries")

            Expect.equal resultRta.Available realRta "Inflow row must show the month's to_be_budgeted, not its garbage balance"
            Expect.equal resultNormal.Available (Some { Amount = 173.10m; Currency = "EUR" }) "Normal categories must pass through unchanged"

        testCase "applyReadyToAssign with None clears the Inflow row rather than showing garbage" <| fun () ->
            let rta = mkCat "Inflow: Ready to Assign" "Internal Master Category" (Some { Amount = 787955.35m; Currency = "EUR" })
            let result = applyReadyToAssign None [ rta ]
            Expect.equal (List.head result).Available None "When to_be_budgeted is unavailable the Inflow row shows no number, never the garbage balance"

        testCase "applyReadyToAssign ignores a same-named category outside the Internal Master group" <| fun () ->
            // Belt-and-suspenders: only the internal group's Inflow row is special.
            let lookalike = mkCat "Inflow: Ready to Assign" "Some Other Group" (Some { Amount = 5m; Currency = "EUR" })
            let result = applyReadyToAssign (Some { Amount = -6.27m; Currency = "EUR" }) [ lookalike ]
            Expect.equal (List.head result).Available (Some { Amount = 5m; Currency = "EUR" }) "Only the Internal Master Category Inflow row is overridden"

        testCase "withoutInternalCategories behält nur Ready to Assign aus der internen Gruppe" <| fun () ->
            let rta = mkCat "Inflow: Ready to Assign" "Internal Master Category" None
            let unc = mkCat "Uncategorized" "Internal Master Category" None
            let def = mkCat "Deferred Income SubCategory" "Internal Master Category" None
            let normal = mkCat "Groceries" "Essential Expenses" None
            let result = withoutInternalCategories [ unc; rta; def; normal ]
            Expect.equal (result |> List.map (fun c -> c.Name)) [ "Inflow: Ready to Assign"; "Groceries" ] "Nur Ready to Assign und die normale Kategorie bleiben, Reihenfolge unverändert"

        testCase "withoutInternalCategories erkennt über die Gruppe, nicht über den Namen allein" <| fun () ->
            let other = mkCat "Uncategorized" "Some Other Group" None
            let result = withoutInternalCategories [ other ]
            Expect.equal result [ other ] "Eine Kategorie Uncategorized außerhalb der internen Gruppe bleibt erhalten"

        testCase "categoriesForPicker setzt den Monatswert und blendet Interna aus" <| fun () ->
            let rta = mkCat "Inflow: Ready to Assign" "Internal Master Category" (Some { Amount = 787955.35m; Currency = "EUR" })
            let unc = mkCat "Uncategorized" "Internal Master Category" None
            let realRta = Some { Amount = -6.27m; Currency = "EUR" }
            let result = categoriesForPicker realRta [ unc; rta ]
            Expect.equal (result |> List.map (fun c -> c.Name)) [ "Inflow: Ready to Assign" ] "Uncategorized fällt weg"
            Expect.equal (List.head result).Available realRta "Ready to Assign trägt den überschriebenen Monatswert"

        testCase "monthToBeBudgetedDecoder reads to_be_budgeted milliunits as Money" <| fun () ->
            let json = """{ "data": { "month": { "to_be_budgeted": -6270 } } }"""
            match Decode.fromString Decoders.monthToBeBudgetedDecoder json with
            | Ok m ->
                Expect.equal m.Amount -6.27m "to_be_budgeted -6270 milliunits should decode to -6.27"
                Expect.equal m.Currency "EUR" "Currency should be EUR"
            | Error err -> failtest $"Failed to decode month to_be_budgeted: {err}"
    ]

[<Tests>]
let transactionDecoderTests =
    testList "YNAB Transaction Decoder Tests (regression: since_date-Versatz, ynab-r9k4t)" [
        // Bug: transactionDecoder las YNABs reinen Kalendertag ("2026-02-03") mit
        // Decode.datetimeUtc. Der interpretiert die Ziffern als Ortszeit und rechnet
        // nach UTC um; östlich von UTC (z. B. MEZ) landet der Buchungstag dadurch
        // auf dem 2. Februar, 23:00 statt dem 3. Februar, 00:00, einen Kalendertag
        // zu früh. Auf einer UTC-Maschine (Offset 0) fällt der alte, fehlerhafte
        // Code mit diesem Test nicht auf: dort verschiebt Decode.datetimeUtc nichts.
        // Der Regressionsschutz kommt deshalb nicht aus diesem Test allein, sondern
        // aus TZ=Europe/Berlin in src/Tests/test.runsettings (RunConfiguration
        // EnvironmentVariables, siehe Kommentar dort), das den gesamten Testlauf
        // auf eine von UTC verschiedene Zeitzone festlegt.
        testCase "\"2026-02-03\" dekodiert zu 2026-02-03 00:00:00 Utc" <| fun () ->
            let json = """{ "id": "tx-1", "date": "2026-02-03", "amount": -1000, "cleared": "cleared" }"""
            match Decode.fromString Decoders.transactionDecoder json with
            | Ok tx ->
                Expect.equal tx.Date (DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc))
                    "Der Buchungstag darf nicht um den lokalen Zeitzonen-Offset verschoben werden"
                Expect.equal tx.Date.Kind DateTimeKind.Utc
                    "Das dekodierte Datum muss als Utc gekennzeichnet sein"
            | Error err -> failtest $"Failed to decode transaction: {err}"
    ]

// ============================================
// Data Transformation Tests
// ============================================

[<Tests>]
let milliunitsConversionTests =
    testList "Milliunits Conversion Tests" [
        testCase "converts positive milliunits correctly" <| fun () ->
            let milliunits = 250000L
            let amount = decimal milliunits / 1000m
            Expect.equal amount 250m "250000 milliunits should be 250"

        testCase "converts negative milliunits correctly" <| fun () ->
            let milliunits = -150000L
            let amount = decimal milliunits / 1000m
            Expect.equal amount -150m "-150000 milliunits should be -150"

        testCase "converts zero milliunits" <| fun () ->
            let milliunits = 0L
            let amount = decimal milliunits / 1000m
            Expect.equal amount 0m "0 milliunits should be 0"

        testCase "converts decimal amounts to milliunits for API" <| fun () ->
            let amount = 123.45m
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits 123450L "123.45 should be 123450 milliunits"

        testCase "converts negative decimal amounts to milliunits" <| fun () ->
            let amount = -67.89m
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits -67890L "-67.89 should be -67890 milliunits"

        testCase "handles fractional cents correctly" <| fun () ->
            let amount = 12.345m  // 3 decimal places
            let milliunits = int64 (amount * 1000m)
            Expect.equal milliunits 12345L "12.345 should be 12345 milliunits"
    ]

[<Tests>]
let transactionConversionTests =
    testList "Transaction Conversion Tests" [
        testCase "converts SyncTransaction to YNAB format correctly" <| fun () ->
            let transactionId = TransactionId "tx-123"
            let categoryId = YnabCategoryId (Guid.Parse("c1b2a3d4-e5f6-7890-abcd-ef1234567890"))
            let accountId = YnabAccountId (Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"))

            let bankTransaction = {
                Id = transactionId
                BookingDate = DateTime(2025, 11, 29)
                Amount = { Amount = -50.25m; Currency = "EUR" }
                Payee = Some "Test Store"
                Memo = "Test purchase"
                Reference = "REF123"
                RawData = "{}"
            }

            let syncTransaction = {
                Transaction = bankTransaction
                Status = ManualCategorized
                CategoryId = Some categoryId
                CategoryName = Some "Groceries"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF123")
                YnabImportStatus = NotAttempted
                Splits = None
                SuggestedByOrderId = None
                ReviewSteps = []
            }

            // Simulate the conversion logic from createTransactions
            // Use Domain.generateImportId to match actual production code
            let (YnabCategoryId categoryIdGuid) = syncTransaction.CategoryId.Value

            let ynabTransaction = {|
                account_id = accountId.ToString()
                date = syncTransaction.Transaction.BookingDate.ToString("yyyy-MM-dd")
                amount = int64 (syncTransaction.Transaction.Amount.Amount * 1000m)
                payee_name = syncTransaction.Transaction.Payee |> Option.defaultValue "Unknown"
                category_id = categoryIdGuid.ToString()
                memo = syncTransaction.Transaction.Memo
                cleared = "uncleared"
                import_id = generateImportId syncTransaction.Transaction.Id
            |}

            Expect.equal ynabTransaction.date "2025-11-29" "Date should be formatted correctly"
            Expect.equal ynabTransaction.amount -50250L "Amount should be converted to milliunits"
            Expect.equal ynabTransaction.payee_name "Test Store" "Payee should match"
            Expect.equal ynabTransaction.memo "Test purchase" "Memo should match"
            Expect.equal ynabTransaction.cleared "uncleared" "Should be marked as uncleared"
            Expect.stringStarts ynabTransaction.import_id $"{ImportIdPrefix}:" "Import ID should use correct prefix"
            Expect.stringContains ynabTransaction.import_id "tx123" "Import ID should contain transaction ID without dashes"

        testCase "uses PayeeOverride when provided" <| fun () ->
            let transactionId = TransactionId "tx-456"
            let categoryId = YnabCategoryId (Guid.Parse("c1b2a3d4-e5f6-7890-abcd-ef1234567890"))

            let bankTransaction = {
                Id = transactionId
                BookingDate = DateTime(2025, 11, 29)
                Amount = { Amount = -30m; Currency = "EUR" }
                Payee = Some "Original Payee"
                Memo = "Test"
                Reference = "REF456"
                RawData = "{}"
            }

            let syncTransaction = {
                Transaction = bankTransaction
                Status = ManualCategorized
                CategoryId = Some categoryId
                CategoryName = Some "Dining"
                MatchedRuleId = None
                PayeeOverride = Some "Custom Payee"
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF456")
                YnabImportStatus = NotAttempted
                Splits = None
                SuggestedByOrderId = None
                ReviewSteps = []
            }

            let payeeName =
                syncTransaction.PayeeOverride
                |> Option.orElse syncTransaction.Transaction.Payee
                |> Option.defaultValue "Unknown"

            Expect.equal payeeName "Custom Payee" "Should use PayeeOverride when provided"

        testCase "memo with reference fits within 300 char limit" <| fun () ->
            // New behavior: memo + ", Ref: <reference>" is constructed
            // Limit is 300 chars, truncation happens from the beginning
            let shortMemo = "Short memo"
            let reference = "REF123"
            let expectedMemo = $"{shortMemo}, Ref: {reference}"

            Expect.isLessThanOrEqual expectedMemo.Length 300 "Memo with reference should fit within limit"
            Expect.stringEnds expectedMemo $", Ref: {reference}" "Memo should end with reference"

        testCase "whitespace in memo is compressed" <| fun () ->
            // Multiple whitespaces are compressed to single space
            let memoWithSpaces = "Payment   from    John"
            let compressed = System.Text.RegularExpressions.Regex.Replace(memoWithSpaces, @"\s+", " ").Trim()

            Expect.equal compressed "Payment from John" "Multiple spaces should be compressed"

        testCase "filters out skipped transactions" <| fun () ->
            let transactions = [
                {
                    Transaction = {
                        Id = TransactionId "tx-1"
                        BookingDate = DateTime(2025, 11, 29)
                        Amount = { Amount = -10m; Currency = "EUR" }
                        Payee = Some "Store"
                        Memo = "Test"
                        Reference = "REF1"
                        RawData = "{}"
                    }
                    Status = Skipped
                    CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                    CategoryName = Some "Category"
                    MatchedRuleId = None
                    PayeeOverride = None
                    ExternalLinks = []
                    UserNotes = None
                    DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF1")
                    YnabImportStatus = NotAttempted
                    Splits = None
                    SuggestedByOrderId = None
                    ReviewSteps = []
                }
            ]

            let filtered =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped && tx.CategoryId.IsSome)

            Expect.hasLength filtered 0 "Skipped transactions should be filtered out"

        testCase "uncategorized transactions pass filter (category is optional)" <| fun () ->
            // This test ensures uncategorized transactions are NOT filtered out.
            // Uncategorized transactions should be importable to YNAB's Uncategorized view.
            let transactions = [
                {
                    Transaction = {
                        Id = TransactionId "tx-2"
                        BookingDate = DateTime(2025, 11, 29)
                        Amount = { Amount = -20m; Currency = "EUR" }
                        Payee = Some "Store"
                        Memo = "Test"
                        Reference = "REF2"
                        RawData = "{}"
                    }
                    Status = Pending
                    CategoryId = None
                    CategoryName = None
                    MatchedRuleId = None
                    PayeeOverride = None
                    ExternalLinks = []
                    UserNotes = None
                    DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF2")
                    YnabImportStatus = NotAttempted
                    Splits = None
                    SuggestedByOrderId = None
                    ReviewSteps = []
                }
            ]

            // New filter: only skip Skipped transactions, category is optional
            let filtered =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped)

            Expect.hasLength filtered 1 "Uncategorized transactions should pass the filter"

        testCase "future-dated transactions are filtered out before YNAB import" <| fun () ->
            // This test prevents regression of the bug where Comdirect pre-notified transactions
            // (Vormerkbuchungen) had future booking dates, causing YNAB to reject the entire batch
            // with "date must not be in the future or over 5 years ago".
            let today = DateTime.Today
            let makeTx id date = {
                Transaction = {
                    Id = TransactionId id
                    BookingDate = date
                    Amount = { Amount = -10m; Currency = "EUR" }
                    Payee = Some "Store"
                    Memo = "Test"
                    Reference = id
                    RawData = "{}"
                }
                Status = AutoCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Category"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails id)
                YnabImportStatus = NotAttempted
                Splits = None
                SuggestedByOrderId = None
                ReviewSteps = []
            }

            let transactions = [
                makeTx "tx-today" today
                makeTx "tx-yesterday" (today.AddDays(-1.0))
                makeTx "tx-tomorrow" (today.AddDays(1.0))
                makeTx "tx-next-week" (today.AddDays(7.0))
            ]

            // Replicate the filter logic from createTransactions
            let futureSkipped, valid =
                transactions
                |> List.filter (fun tx -> tx.Status <> Skipped)
                |> List.partition (fun tx -> tx.Transaction.BookingDate.Date > today)

            Expect.hasLength valid 2 "Only today and past transactions should pass"
            Expect.hasLength futureSkipped 2 "Future-dated transactions should be filtered out"
            Expect.all valid (fun tx -> tx.Transaction.BookingDate.Date <= today) "All valid transactions should have date <= today"

        testCase "transaction dated today is not filtered out" <| fun () ->
            // Edge case: a transaction booked today should be accepted
            let today = DateTime.Today
            let tx = {
                Transaction = {
                    Id = TransactionId "tx-today-edge"
                    BookingDate = today
                    Amount = { Amount = -25m; Currency = "EUR" }
                    Payee = Some "Today Store"
                    Memo = "Today purchase"
                    Reference = "REF-TODAY"
                    RawData = "{}"
                }
                Status = ManualCategorized
                CategoryId = Some (YnabCategoryId (Guid.NewGuid()))
                CategoryName = Some "Groceries"
                MatchedRuleId = None
                PayeeOverride = None
                ExternalLinks = []
                UserNotes = None
                DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-TODAY")
                YnabImportStatus = NotAttempted
                Splits = None
                SuggestedByOrderId = None
                ReviewSteps = []
            }

            let isFuture = tx.Transaction.BookingDate.Date > today
            Expect.isFalse isFuture "Transaction dated today should not be considered future"
    ]

// ============================================
// Import ID Generation Tests
// ============================================

[<Tests>]
let importIdGenerationTests =
    testList "Import ID Generation Tests" [
        testCase "generateImportId produces correct format with BB prefix" <| fun () ->
            let txId = TransactionId "TX-123-456"
            let importId = generateImportId txId

            Expect.stringStarts importId $"{ImportIdPrefix}:" "Import ID should start with BB:"
            Expect.stringContains importId "TX123456" "Import ID should contain transaction ID without dashes"

        testCase "generateImportId removes dashes from transaction ID" <| fun () ->
            let txId = TransactionId "abc-def-ghi"
            let importId = generateImportId txId

            Expect.equal importId "BB:abcdefghi" "Dashes should be removed from transaction ID"

        testCase "generateImportId generates unique IDs for different transactions" <| fun () ->
            let txId1 = TransactionId "tx-1"
            let txId2 = TransactionId "tx-2"

            let importId1 = generateImportId txId1
            let importId2 = generateImportId txId2

            Expect.notEqual importId1 importId2 "Different transactions should have different import IDs"

        testCase "generateImportId generates consistent IDs for same transaction" <| fun () ->
            let txId = TransactionId "tx-123"

            let importId1 = generateImportId txId
            let importId2 = generateImportId txId

            Expect.equal importId1 importId2 "Same transaction should always generate same import ID"

        testCase "matchesImportId correctly matches generated import ID" <| fun () ->
            // This test ensures generateImportId and matchesImportId are in sync
            let txId = TransactionId "TX-789-ABC"
            let generatedImportId = generateImportId txId

            let matches = matchesImportId txId generatedImportId

            Expect.isTrue matches "matchesImportId must match IDs created by generateImportId"

        testCase "matchesImportId returns false for non-matching transaction" <| fun () ->
            let txId1 = TransactionId "TX-111"
            let txId2 = TransactionId "TX-222"
            let importIdForTx1 = generateImportId txId1

            let matches = matchesImportId txId2 importIdForTx1

            Expect.isFalse matches "Should not match import ID from different transaction"

        testCase "matchesImportId returns false for wrong prefix" <| fun () ->
            let txId = TransactionId "TX-123"
            let wrongPrefixImportId = "WRONG:TX123"

            let matches = matchesImportId txId wrongPrefixImportId

            Expect.isFalse matches "Should not match import ID with wrong prefix"
    ]

// ============================================
// Error Handling Tests
// ============================================

[<Tests>]
let errorHandlingTests =
    testList "YNAB Error Handling Tests" [
        testCase "handles invalid JSON response" <| fun () ->
            let decoder = Decode.field "data" (Decode.field "budgets" (Decode.list Decoders.budgetDecoder))
            let result = Decode.fromString decoder SampleData.malformedJson

            match result with
            | Ok _ -> failtest "Should fail on malformed JSON"
            | Error err ->
                Expect.isNotNull err "Should return error message"
    ]


// ============================================
// Integration Test Documentation
// ============================================

[<Tests>]
let integrationTestDocumentation =
    testList "Integration Test Documentation" [
        testCase "documents how to run integration tests" <| fun () ->
            // This test documents the approach for integration testing
            // To run real integration tests:
            // 1. Set environment variable YNAB_TOKEN with a valid Personal Access Token
            // 2. Uncomment the integration test code below
            // 3. Run: dotnet test --filter Category=Integration

            // Example integration test (commented out to avoid requiring a real token):
            (*
            let token = Environment.GetEnvironmentVariable("YNAB_TOKEN")
            if String.IsNullOrEmpty(token) then
                Tests.skiptest "YNAB_TOKEN not set"
            else
                async {
                    let! result = YnabClient.getBudgets token
                    match result with
                    | Ok budgets ->
                        Expect.isGreaterThan budgets.Length 0 "Should have at least one budget"
                    | Error err ->
                        failtest $"Integration test failed: {err}"
                } |> Async.RunSynchronously
            *)

            // This test always passes and is just for documentation
            ()

        testCase "documents YNAB API rate limits" <| fun () ->
            // YNAB API Rate Limits (from documentation):
            // - 200 requests per hour per token
            // - Rate limit info in response headers:
            //   - X-Rate-Limit: 200
            //   - X-Rate-Limit-Remaining: 150
            // - When rate limited, returns 429 with Retry-After header

            // Our implementation handles this by:
            // 1. Returning YnabError.RateLimitExceeded with retry-after seconds
            // 2. Client code should implement exponential backoff

            ()

        testCase "documents YNAB API response structure" <| fun () ->
            // All YNAB API responses follow this structure:
            // Success: { "data": { ... } }
            // Error: { "error": { "id": "...", "name": "...", "detail": "..." } }

            // Our decoders always expect the "data" wrapper
            // Error responses are handled by HTTP status codes, not by parsing error JSON

            ()
    ]

// ============================================
// Property-Based Tests
// ============================================

[<Tests>]
let propertyBasedTests =
    testList "Property-Based YNAB Tests" [
        testProperty "milliunits conversion roundtrip" <| fun (amount: decimal) ->
            // Skip extreme values that might overflow
            if abs amount < 1000000000m then
                let milliunits = int64 (amount * 1000m)
                let converted = decimal milliunits / 1000m
                // Allow for rounding differences due to decimal precision
                abs (converted - amount) < 0.001m
            else
                true

        testProperty "import ID uniqueness for different transaction IDs" <| fun (id1: string) (id2: string) ->
            // Test that different transaction IDs produce different import IDs
            // Uses the actual Domain.generateImportId function
            if id1 <> id2 && not (String.IsNullOrEmpty id1) && not (String.IsNullOrEmpty id2) then
                let txId1 = TransactionId id1
                let txId2 = TransactionId id2
                let importId1 = generateImportId txId1
                let importId2 = generateImportId txId2
                importId1 <> importId2
            else
                true

        testProperty "memo with reference respects 300 char limit" <| fun (memo: string) ->
            if not (String.IsNullOrEmpty(memo)) then
                let memoLimit = 300
                let reference = "REF123"
                let compressed = System.Text.RegularExpressions.Regex.Replace(memo, @"\s+", " ").Trim()
                let suffix = $", Ref: {reference}"
                let fullMemo = $"{compressed}{suffix}"

                let result =
                    if fullMemo.Length <= memoLimit then
                        fullMemo
                    else
                        let availableForMemo = memoLimit - suffix.Length - 3
                        if availableForMemo <= 0 then
                            fullMemo.Substring(fullMemo.Length - memoLimit)
                        else
                            let truncatedMemo = compressed.Substring(compressed.Length - availableForMemo)
                            $"...{truncatedMemo}{suffix}"

                result.Length <= memoLimit && result.EndsWith(suffix)
            else
                true
    ]

// ============================================
// JSON Encoding Tests (Critical - prevents regression of amount serialization bug)
// ============================================

[<Tests>]
let jsonEncodingTests =
    testList "YNAB JSON Encoding Tests" [
        testCase "amount is serialized as JSON number, not string" <| fun () ->
            // This test prevents regression of the bug where Encode.int64 serialized
            // amounts as strings (e.g., "-50250" instead of -50250), causing YNAB
            // to silently reject transactions.
            let testTx : YnabTransactionRequest = {
                AccountId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                Date = "2025-12-07"
                Amount = -50250  // -50.25 EUR in milliunits
                PayeeName = "Test Store"
                Memo = "Test purchase"
                Cleared = "uncleared"
                ImportId = "BB:tx123"
                CategoryId = Some "c1b2a3d4-e5f6-7890-abcd-ef1234567890"
                Subtransactions = None
            }

            let requestBody =
                Encode.object [
                    "transactions", Encode.list ([testTx] |> List.map (fun tx ->
                        Encode.object [
                            "account_id", Encode.string tx.AccountId
                            "date", Encode.string tx.Date
                            "amount", Encode.int tx.Amount  // Must be Encode.int, NOT Encode.int64!
                            "payee_name", Encode.string tx.PayeeName
                            "memo", Encode.string tx.Memo
                            "cleared", Encode.string tx.Cleared
                            "import_id", Encode.string tx.ImportId
                            match tx.CategoryId with
                            | Some catId -> "category_id", Encode.string catId
                            | None -> ()
                        ]
                    ))
                ]
                |> Encode.toString 0

            // Critical: Amount must NOT have quotes around it
            Expect.stringContains requestBody "\"amount\":-50250" "Amount must be a JSON number, not a string"
            Expect.isFalse (requestBody.Contains("\"amount\":\"-50250\"")) "Amount must NOT be serialized as string"

        testCase "subtransaction amount is serialized as JSON number" <| fun () ->
            // Routes through the real encoder so the regression guard tracks the
            // production encoding (DU-shaped after ADR 0006).
            let testSub = CategorySub (-25000, "c1b2a3d4-e5f6-7890-abcd-ef1234567890", Some "Split 1")
            let json = encodeSubtransaction testSub |> Encode.toString 0

            Expect.stringContains json "\"amount\":-25000" "Subtransaction amount must be a JSON number"
            Expect.isFalse (json.Contains("\"amount\":\"-25000\"")) "Subtransaction amount must NOT be serialized as string"

        testCase "transaction is encoded with cleared=uncleared" <| fun () ->
            // This test ensures all transactions are imported as "uncleared" to YNAB,
            // allowing users to review and clear them manually in YNAB.
            let testTx : YnabTransactionRequest = {
                AccountId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                Date = "2025-12-07"
                Amount = -50250
                PayeeName = "Test Store"
                Memo = "Test purchase"
                Cleared = "uncleared"
                ImportId = "BB:tx123"
                CategoryId = Some "c1b2a3d4-e5f6-7890-abcd-ef1234567890"
                Subtransactions = None
            }

            let json =
                Encode.object [
                    "cleared", Encode.string testTx.Cleared
                ]
                |> Encode.toString 0

            Expect.stringContains json "\"cleared\":\"uncleared\"" "Transaction must be encoded as uncleared"
            Expect.isFalse (json.Contains("\"cleared\":\"cleared\"")) "Transaction must NOT be encoded as cleared"

        testCase "uncategorized transaction is encoded without category_id" <| fun () ->
            // This test ensures transactions without a category are encoded correctly -
            // the category_id field should be omitted entirely (not set to null or empty).
            // This allows importing uncategorized transactions to YNAB's Uncategorized view.
            let testTx : YnabTransactionRequest = {
                AccountId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                Date = "2025-12-07"
                Amount = -50250
                PayeeName = "Test Store"
                Memo = "Test purchase"
                Cleared = "uncleared"
                ImportId = "BB:tx123"
                CategoryId = None  // No category
                Subtransactions = None
            }

            let json =
                Encode.object [
                    "account_id", Encode.string testTx.AccountId
                    "date", Encode.string testTx.Date
                    "amount", Encode.int testTx.Amount
                    "payee_name", Encode.string testTx.PayeeName
                    "memo", Encode.string testTx.Memo
                    "cleared", Encode.string testTx.Cleared
                    "import_id", Encode.string testTx.ImportId
                    match testTx.CategoryId with
                    | Some catId -> "category_id", Encode.string catId
                    | None -> ()  // Omit field entirely
                ]
                |> Encode.toString 0

            Expect.isFalse (json.Contains("category_id")) "JSON should NOT contain category_id when None"
            Expect.stringContains json "\"cleared\":\"uncleared\"" "Uncategorized transaction should still be uncleared"
    ]

// ============================================
// YNAB Response Parsing Tests (Critical - prevents false success reports)
// ============================================

[<Tests>]
let ynabResponseParsingTests =
    testList "YNAB Response Parsing Tests" [
        testCase "correctly counts created transactions from YNAB response" <| fun () ->
            // This test prevents regression of the bug where we reported success
            // based on sent transaction count instead of actual YNAB response.
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 },
                        { "id": "tx-2", "amount": -30000 }
                    ],
                    "duplicate_import_ids": []
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 2 "Should report 2 created transactions"
                Expect.isEmpty duplicates "Should have no duplicates"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "correctly identifies duplicate transactions from YNAB response" <| fun () ->
            // This test ensures we detect when YNAB silently rejects transactions
            // as duplicates (which it reports in duplicate_import_ids).
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 }
                    ],
                    "duplicate_import_ids": ["BB:abc123", "BB:def456"]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 1 "Should report only 1 created transaction"
                Expect.hasLength duplicates 2 "Should have 2 duplicate import IDs"
                Expect.contains duplicates "BB:abc123" "Should contain first duplicate ID"
                Expect.contains duplicates "BB:def456" "Should contain second duplicate ID"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "handles response with all transactions rejected as duplicates" <| fun () ->
            // When all transactions are duplicates, YNAB returns empty transactions array
            let ynabResponse = """
            {
                "data": {
                    "transactions": [],
                    "duplicate_import_ids": ["BB:abc123", "BB:def456", "BB:ghi789"]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 0 "Should report 0 created transactions when all are duplicates"
                Expect.hasLength duplicates 3 "Should have 3 duplicate import IDs"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"

        testCase "handles response missing duplicate_import_ids field" <| fun () ->
            // Older YNAB API responses might not include duplicate_import_ids
            let ynabResponse = """
            {
                "data": {
                    "transactions": [
                        { "id": "tx-1", "amount": -50000 }
                    ]
                }
            }
            """

            let createdCountDecoder =
                Decode.field "data" (
                    Decode.object (fun get ->
                        let transactions = get.Optional.Field "transactions" (Decode.list (Decode.succeed ())) |> Option.defaultValue []
                        let duplicates = get.Optional.Field "duplicate_import_ids" (Decode.list Decode.string) |> Option.defaultValue []
                        (transactions.Length, duplicates)
                    )
                )

            match Decode.fromString createdCountDecoder ynabResponse with
            | Ok (createdCount, duplicates) ->
                Expect.equal createdCount 1 "Should report 1 created transaction"
                Expect.isEmpty duplicates "Should default to empty list when field is missing"
            | Error err ->
                failtest $"Failed to parse YNAB response: {err}"
    ]

// ============================================
// Quick Add: YNAB-Transaction-Id aus der Antwort des Einzel-Posts (ADR 0015, ADR 0021)
// ============================================
//
// `createManualTransaction` selbst ruft YNAB per HTTP auf und ist nicht .NET-testbar
// (Fable.Remoting-Proxy-Grenze); `manualTransactionIdDecoder` ist die reine
// Antwort-Auswertung und wird hier gegen eine repraesentative, aufgezeichnete
// YNAB-Antwort getestet (`POST /transactions` mit einem einzelnen "transaction",
// nicht dem Batch-"transactions"-Array).

[<Tests>]
let manualTransactionIdDecoderTests =
    testList "manualTransactionIdDecoder (Quick Add, Einzel-Post-Antwort)" [
        testCase "liest die YNAB-Transaction-Id aus der Antwort eines Einzel-Posts" <| fun () ->
            let ynabResponse = """
            {
                "data": {
                    "transaction_ids": ["b1d4c2e0-5f2a-4b8e-9c1a-000000000001"],
                    "transaction": {
                        "id": "b1d4c2e0-5f2a-4b8e-9c1a-000000000001",
                        "date": "2026-09-11",
                        "amount": -4500,
                        "memo": null,
                        "cleared": "uncleared",
                        "approved": true,
                        "account_id": "a1000000-0000-0000-0000-000000000001",
                        "payee_id": "p1000000-0000-0000-0000-000000000001",
                        "category_id": null
                    },
                    "server_knowledge": 12345
                }
            }
            """

            match Decode.fromString manualTransactionIdDecoder ynabResponse with
            | Ok id -> Expect.equal id "b1d4c2e0-5f2a-4b8e-9c1a-000000000001" "YNAB-Transaction-Id aus data.transaction.id"
            | Error err -> failtest $"Failed to parse YNAB response: {err}"

        testCase "eine Antwort ohne transaction liefert einen Decode-Fehler statt einer falschen Id" <| fun () ->
            // Die Batch-Antwort (data.transactions, kein data.transaction) darf nicht
            // stillschweigend als Einzel-Post-Antwort durchgehen.
            let batchResponse = """
            {
                "data": {
                    "transactions": [ { "id": "tx-1", "amount": -4500 } ],
                    "duplicate_import_ids": []
                }
            }
            """

            match Decode.fromString manualTransactionIdDecoder batchResponse with
            | Ok id -> failtest $"sollte scheitern, lieferte stattdessen {id}"
            | Error _ -> ()
    ]

// ============================================
// Regression: Erstbefuellung des Spiegels muss wirklich alles holen
// ============================================
//
// Abnahme-Fund vom 2026-09-06 (ledger-kt0wq, Human-Gate). Der erste Spiegel-Lauf holte
// 2508 Buchungen ab 2025-09-08; drei von sieben Kontostaenden standen im Kassenbuch
// falsch (Girokonto -238,42 statt 5.033,12; Tagesgeld Consors -31.834,02 statt 0,00;
// Bar 58,61 statt 73,72). Ursache: `GET /budgets/{id}/transactions` liefert **ohne**
// `since_date` nicht alles, sondern nur ein rollendes Fenster der letzten rund zwoelf
// Monate. Am selben Budget gemessen: ohne Parameter 2508 Buchungen, mit
// `since_date=2000-01-01` 13863 Buchungen ab 2018-12-29 — und dann stimmen alle sieben
// Kontostaende. Die Anfangssalden der aelteren Konten lagen ausserhalb des Fensters.
//
// ADR 0014 fordert fuer die Erstbefuellung einen Vollabruf; "ohne last_knowledge" allein
// ist keiner.

[<Tests>]
let mirrorInitialFetchTests =
    testList "Spiegel: Erstbefuellung holt alles" [

        testCase "Ohne gemerkte Marke wird ein since_date mitgeschickt" <| fun () ->
            let query = Server.YnabClient.transactionsQuery None
            Expect.stringContains query "since_date=" "Die Erstbefuellung braucht ein since_date, sonst liefert YNAB nur zwoelf Monate"
            Expect.isFalse (query.Contains "last_knowledge_of_server")
                "Ohne Marke gibt es kein Delta zu holen"

        testCase "Das since_date der Erstbefuellung liegt vor jedem denkbaren Budget" <| fun () ->
            let query = Server.YnabClient.transactionsQuery None
            let datePart = query.Substring(query.IndexOf "since_date=" + "since_date=".Length)
            let parsed = System.DateTime.Parse(datePart, System.Globalization.CultureInfo.InvariantCulture)
            Expect.isLessThan parsed (System.DateTime(2000, 1, 1))
                "Muss vor dem aeltesten realistischen Buchungsdatum liegen"

        testCase "Mit gemerkter Marke wird das Delta geholt, ohne since_date" <| fun () ->
            let query = Server.YnabClient.transactionsQuery (Some 4711L)
            Expect.stringContains query "last_knowledge_of_server=4711" "Die Marke muss mit"
            Expect.isFalse (query.Contains "since_date")
                "Das Delta braucht kein Datumsfenster — es wuerde die Marke nur verwaessern"
    ]

// ============================================
// Teil 1 (ledger-qvt84): Vorbereiten/Senden getrennt, drei Push-Bugs behoben
// ============================================
//
// `createTransactions` postet per HTTP und ist nicht .NET-testbar. Die drei Bugs aus
// dem Task (Zukunftsdatum still importiert, Force-Import-ImportId nie auslesbar,
// nicht lesbare Antwort als pauschaler Erfolg) stecken jetzt in den reinen Funktionen
// `prepareTransactions` und `parseCreateTransactionsResponse`; hier direkt getestet.

module private PrepareFixtures =
    let categoryId = YnabCategoryId (Guid.Parse "c1b2a3d4-e5f6-7890-abcd-ef1234567890")
    let accountId = YnabAccountId (Guid.Parse "a1b2c3d4-e5f6-7890-abcd-ef1234567890")

    let makeTx (id: string) (status: TransactionStatus) (date: DateTime) : SyncTransaction = {
        Transaction = {
            Id = TransactionId id
            BookingDate = date
            Amount = { Amount = -10m; Currency = "EUR" }
            Payee = Some "Laden"
            Memo = "Test"
            Reference = id
            RawData = "{}"
        }
        Status = status
        CategoryId = Some categoryId
        CategoryName = Some "Kategorie"
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails id)
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
        ReviewSteps = []
    }

[<Tests>]
let prepareTransactionsTests =
    testList "prepareTransactions (Vorbereiten)" [

        testCase "zukunftsdatierte Buchungen landen nicht in ToSend, sondern in FutureDatedTransactionIds" <| fun () ->
            // Regressionstest für Bug 1: vorher wurden zukunftsdatierte Buchungen zwar
            // nicht an YNAB gesendet, aber vom Aufrufer (importToYnab) trotzdem als
            // `Imported` markiert, weil die Filterung für ihn unsichtbar war.
            // `PrepareResult` macht sie jetzt als eigenes Feld sichtbar.
            let today = DateTime(2026, 9, 11)
            let due = PrepareFixtures.makeTx "tx-due" AutoCategorized today
            let future = PrepareFixtures.makeTx "tx-future" AutoCategorized (today.AddDays 1.0)

            let result =
                prepareTransactions today PrepareFixtures.accountId Map.empty false [ due; future ]

            Expect.equal (result.ToSend |> List.map (fun p -> p.TransactionId)) [ due.Transaction.Id ]
                "nur die fällige Buchung wird vorbereitet"
            Expect.equal result.FutureDatedTransactionIds [ future.Transaction.Id ]
                "die zukunftsdatierte Buchung steht erkennbar in FutureDatedTransactionIds"

        testCase "Skipped-Buchungen werden nie vorbereitet" <| fun () ->
            let today = DateTime(2026, 9, 11)
            let skipped = PrepareFixtures.makeTx "tx-skip" Skipped today

            let result = prepareTransactions today PrepareFixtures.accountId Map.empty false [ skipped ]

            Expect.isEmpty result.ToSend "eine übersprungene Buchung wird nicht gesendet"
            Expect.isEmpty result.FutureDatedTransactionIds "sie ist auch keine zukunftsdatierte Buchung"

        testCase "Force-Import erzeugt eine neue ImportId, die der Aufrufer auslesen kann" <| fun () ->
            // Regressionstest für Bug 2: vorher wurde die neue ImportId nur innerhalb
            // von createTransactions erzeugt und nie zurückgegeben; der Aufrufer
            // (forceImportDuplicates) konnte nicht verknüpfen. Jetzt steht sie direkt
            // an `PreparedTransaction.ImportId`.
            let today = DateTime(2026, 9, 11)
            let tx = PrepareFixtures.makeTx "tx-force" NeedsAttention today

            let normal = prepareTransactions today PrepareFixtures.accountId Map.empty false [ tx ]
            let forced = prepareTransactions today PrepareFixtures.accountId Map.empty true [ tx ]

            let deterministicImportId = Shared.Domain.generateImportId tx.Transaction.Id
            Expect.equal normal.ToSend.Head.ImportId deterministicImportId
                "ohne Force-Import ist die ImportId deterministisch"
            Expect.stringStarts forced.ToSend.Head.ImportId $"{Shared.Domain.ImportIdPrefix}:"
                "die neue ImportId trägt weiterhin das BB-Präfix"
            Expect.notEqual forced.ToSend.Head.ImportId deterministicImportId
                "Force-Import erzeugt eine NEUE ImportId, nicht die deterministische"
    ]

[<Tests>]
let parseCreateTransactionsResponseTests =
    testList "parseCreateTransactionsResponse (Senden, Antwort auswerten)" [

        testCase "liest YNAB-Ids über import_id zugeordnet, nicht über die Reihenfolge" <| fun () ->
            let response = """
            {
                "data": {
                    "transactions": [
                        { "id": "ynab-2", "import_id": "BB:tx2" },
                        { "id": "ynab-1", "import_id": "BB:tx1" }
                    ],
                    "duplicate_import_ids": []
                }
            }
            """
            match parseCreateTransactionsResponse response with
            | ParsedCreateResponse (created, duplicates) ->
                Expect.isEmpty duplicates "keine Duplikate gemeldet"
                Expect.contains created (Some "BB:tx1", "ynab-1") "tx1 über import_id zugeordnet"
                Expect.contains created (Some "BB:tx2", "ynab-2") "tx2 über import_id zugeordnet, trotz Umsortierung"
            | UnparseableCreateResponse err -> failtest $"sollte lesbar sein, war: {err}"

        testCase "meldet Duplikate unverändert weiter" <| fun () ->
            let response = """
            {
                "data": {
                    "transactions": [],
                    "duplicate_import_ids": ["BB:tx1", "BB:tx2"]
                }
            }
            """
            match parseCreateTransactionsResponse response with
            | ParsedCreateResponse (created, duplicates) ->
                Expect.isEmpty created "nichts angelegt"
                Expect.equal duplicates [ "BB:tx1"; "BB:tx2" ] "beide Duplikate gemeldet"
            | UnparseableCreateResponse err -> failtest $"sollte lesbar sein, war: {err}"

        testCase "eine nicht lesbare Antwort liefert UnparseableCreateResponse, nie einen geratenen Erfolg" <| fun () ->
            // Regressionstest für Bug 3: vorher galten bei einem Parse-Fehler pauschal
            // alle gesendeten Buchungen als angelegt (Fallback-Zeile im alten Code).
            let malformed = """{ "data": { "transactions": "nicht eine Liste" } }"""

            match parseCreateTransactionsResponse malformed with
            | ParsedCreateResponse (created, _) ->
                failtest $"sollte als nicht lesbar erkannt werden, lieferte stattdessen {created.Length} angelegte Buchungen"
            | UnparseableCreateResponse _ -> ()
    ]

// ============================================
// buildCreateResult (Senden, Antwort -> TransactionCreateResult): aus `sendPrepared`
// in eine reine Funktion gezogen (Verifier-Fund, ledger-qvt84 Iteration 2), damit die
// ImportId-Zuordnung und der "nicht lesbar"-Pfad ohne HTTP testbar sind.
// ============================================

[<Tests>]
let buildCreateResultTests =
    testList "buildCreateResult (Senden, Antwort -> TransactionCreateResult)" [

        testCase "ordnet YNAB-Ids über die ImportId zu, nicht über die Reihenfolge im Batch" <| fun () ->
            let today = DateTime(2026, 9, 11)
            let tx1 = PrepareFixtures.makeTx "tx-1" AutoCategorized today
            let tx2 = PrepareFixtures.makeTx "tx-2" AutoCategorized today
            let prepared =
                (prepareTransactions today PrepareFixtures.accountId Map.empty false [ tx1; tx2 ]).ToSend
            let importId1 = (prepared |> List.find (fun p -> p.TransactionId = tx1.Transaction.Id)).ImportId
            let importId2 = (prepared |> List.find (fun p -> p.TransactionId = tx2.Transaction.Id)).ImportId

            // YNAB liefert die Buchungen absichtlich in vertauschter Reihenfolge.
            let parsed = ParsedCreateResponse ([ (Some importId2, "ynab-2"); (Some importId1, "ynab-1") ], [])

            let result = buildCreateResult prepared parsed

            Expect.isFalse result.ResponseUnparseable "die Antwort war lesbar"
            Expect.contains
                result.Created
                { TransactionId = tx1.Transaction.Id; ImportId = importId1; YnabTransactionId = "ynab-1" }
                "tx1 über die ImportId zugeordnet, trotz Umsortierung"
            Expect.contains
                result.Created
                { TransactionId = tx2.Transaction.Id; ImportId = importId2; YnabTransactionId = "ynab-2" }
                "tx2 über die ImportId zugeordnet, trotz Umsortierung"

        testCase "eine nicht auswertbare Antwort liefert Created = [] und ResponseUnparseable = true" <| fun () ->
            // Regressionstest für Bug 3 (Teil 1, "nicht auswertbare Antwort"): vorher
            // galten bei einem Parse-Fehler pauschal alle gesendeten Buchungen als
            // angelegt (Fallback-Zeile im alten Code). `buildCreateResult` ist jetzt die
            // eine Stelle, die diese Abbildung entscheidet, ohne HTTP testbar.
            let today = DateTime(2026, 9, 11)
            let tx = PrepareFixtures.makeTx "tx-unparseable" AutoCategorized today
            let prepared = (prepareTransactions today PrepareFixtures.accountId Map.empty false [ tx ]).ToSend

            let result = buildCreateResult prepared (UnparseableCreateResponse "kaputtes JSON")

            Expect.isEmpty result.Created "kein geratener Erfolg bei nicht lesbarer Antwort"
            Expect.isTrue result.ResponseUnparseable "der Zustand ist erkennbar unbekannt, nicht pauschal erfolgreich"
    ]

// ============================================
// markCreatedAsImported (forceImportDuplicates): nur tatsächlich angelegte Buchungen
// markieren, extrahiert damit sie ohne HTTP testbar ist (Verifier-Fund Iteration 2).
// ============================================

[<Tests>]
let markCreatedAsImportedTests =
    testList "markCreatedAsImported (Force-Import, nur tatsächlich Angelegte markieren)" [

        testCase "markiert nur Buchungen aus result.Created, nicht pauschal alle gesendeten" <| fun () ->
            // Regressionstest: forceImportDuplicates markierte vorher jede gesendete
            // Buchung als Imported, unabhängig von result.Created,
            // result.ResponseUnparseable und result.FutureDatedTransactionIds.
            let today = DateTime(2026, 9, 11)
            let created = PrepareFixtures.makeTx "tx-created" NeedsAttention today
            let notCreated = PrepareFixtures.makeTx "tx-not-created" NeedsAttention today

            let result = {
                emptyCreateResult with
                    Created =
                        [ { TransactionId = created.Transaction.Id; ImportId = "BB:txcreated"; YnabTransactionId = "ynab-1" } ]
            }

            let updated = markCreatedAsImported result [ created; notCreated ]

            let updatedCreated = updated |> List.find (fun t -> t.Transaction.Id = created.Transaction.Id)
            let updatedNotCreated = updated |> List.find (fun t -> t.Transaction.Id = notCreated.Transaction.Id)

            Expect.equal updatedCreated.Status Imported "die tatsächlich angelegte Buchung gilt als importiert"
            Expect.equal updatedCreated.YnabImportStatus YnabImported "YnabImportStatus gesetzt"
            Expect.equal updatedNotCreated.Status notCreated.Status "eine nicht angelegte Buchung bleibt unverändert"
            Expect.equal updatedNotCreated.YnabImportStatus NotAttempted "kein pauschaler Erfolg"
    ]
