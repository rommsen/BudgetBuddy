module Tests

open System
open Expecto
open Comdirect2YNAB // Access to RulesEngine and YamlConfig
open Comdirect2YNAB.RulesEngine // To use CompiledRule, CategoryInfo etc.
open Comdirect2YNAB.YamlConfig // To use Rule, RulesConfig

// Helper to create a category map for tests
let private normalizeCategoryNameTest (name: string) = // Renamed to avoid conflict if RulesEngine.normalizeCategoryName is not public
    name.Trim().ToLowerInvariant()
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")

let private createCategoryMap (categories: (string * Guid) list) : Map<string, Guid> =
    categories
    |> List.map (fun (name, id) -> (normalizeCategoryNameTest name, id))
    |> Map.ofList

// Sample Category IDs
let groceriesId = Guid.NewGuid()
let travelId = Guid.NewGuid()
let shoppingId = Guid.NewGuid()

// IDs for JSON categories
let readyToAssignId             = Guid.NewGuid()
let readyToAssignName         = "Inflow: Ready to Assign"

let essenEinkaufenId            = Guid.NewGuid()
let essenEinkaufenName         = "Essen/Einkaufen"
let essenArbeitId               = Guid.NewGuid()
let essenArbeitName             = "Essen Arbeit"
let essenSchuleId               = Guid.NewGuid()
let essenSchuleName             = "Essen Schule"
let tilgungBausparZinsenId      = Guid.NewGuid()
let tilgungBausparZinsenName    = "Tilgung Bausparzinsen"
let grundsteuernAbfallAbwasserId = Guid.NewGuid()
let grundsteuernAbfallAbwasserName = "Grundsteuern, Abfall, Abwasser"
let stromUndWaermeId            = Guid.NewGuid()
let stromUndWaermeName          = "Strom und Wärme"

let categories_json = $"""
{{
  "data": {{
    "category_groups": [
      {{
        "id": "300eefb2-f934-4a8a-99c3-3dad585b5da4",
        "name": "Internal Master Category",
        "hidden": false,
        "deleted": false,
        "categories": [
          {{
            "id": "{readyToAssignId}",
            "category_group_id": "300eefb2-f934-4a8a-99c3-3dad585b5da4",
            "category_group_name": "Internal Master Category",
            "name": "{readyToAssignName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": null,
            "goal_needs_whole_amount": null,
            "goal_day": null,
            "goal_cadence": null,
            "goal_cadence_frequency": null,
            "goal_creation_month": null,
            "goal_target": 0,
            "goal_target_month": null,
            "goal_percentage_complete": null,
            "goal_months_to_budget": null,
            "goal_under_funded": null,
            "goal_overall_funded": null,
            "goal_overall_left": null,
            "deleted": false
          }}
        ]
      }},
      {{
        "id": "48959dad-16df-4012-9dff-bc9e602cae03",
        "name": "Alltag",
        "hidden": false,
        "deleted": false,
        "categories": [
          {{
            "id": "{essenEinkaufenId}",
            "category_group_id": "48959dad-16df-4012-9dff-bc9e602cae03",
            "category_group_name": "Alltag",
            "name": "{essenEinkaufenName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": "",
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": null,
            "goal_cadence": 1,
            "goal_cadence_frequency": 1,
            "goal_creation_month": "2024-09-01",
            "goal_target": 1400000,
            "goal_target_month": null,
            "goal_percentage_complete": 100,
            "goal_months_to_budget": 1,
            "goal_under_funded": 0,
            "goal_overall_funded": 1400000,
            "goal_overall_left": 0,
            "deleted": false
          }},
          {{
            "id": "{essenArbeitId}",
            "category_group_id": "48959dad-16df-4012-9dff-bc9e602cae03",
            "category_group_name": "Alltag",
            "name": "{essenArbeitName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": 1,
            "goal_cadence": 1,
            "goal_cadence_frequency": 1,
            "goal_creation_month": "2025-03-01",
            "goal_target": 100000,
            "goal_target_month": null,
            "goal_percentage_complete": 100,
            "goal_months_to_budget": 1,
            "goal_under_funded": 0,
            "goal_overall_funded": 127160,
            "goal_overall_left": 0,
            "deleted": false
          }},
          {{
            "id": "{essenSchuleId}",
            "category_group_id": "48959dad-16df-4012-9dff-bc9e602cae03",
            "category_group_name": "Alltag",
            "name": "Essen Schule",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": 1,
            "goal_cadence": 1,
            "goal_cadence_frequency": 1,
            "goal_creation_month": "2025-03-01",
            "goal_target": 84000,
            "goal_target_month": null,
            "goal_percentage_complete": 100,
            "goal_months_to_budget": 1,
            "goal_under_funded": 0,
            "goal_overall_funded": 84000,
            "goal_overall_left": 0,
            "deleted": false
          }}
        ]
      }},
      {{
        "id": "5a22fa0a-e462-4d5d-a25f-9ffc34917c77",
        "name": "Wohnen",
        "hidden": false,
        "deleted": false,
        "categories": [
          {{
            "id": "{tilgungBausparZinsenId}",
            "category_group_id": "5a22fa0a-e462-4d5d-a25f-9ffc34917c77",
            "category_group_name": "Wohnen",
            "name": "{tilgungBausparZinsenName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": null,
            "goal_cadence": 1,
            "goal_cadence_frequency": 1,
            "goal_creation_month": "2024-09-01",
            "goal_target": 1885000,
            "goal_target_month": null,
            "goal_percentage_complete": 100,
            "goal_months_to_budget": 1,
            "goal_under_funded": 0,
            "goal_overall_funded": 1885000,
            "goal_overall_left": 0,
            "deleted": false
          }},
          {{
            "id": "{grundsteuernAbfallAbwasserId}",
            "category_group_id": "5a22fa0a-e462-4d5d-a25f-9ffc34917c77",
            "category_group_name": "Wohnen",
            "name": "{grundsteuernAbfallAbwasserName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": null,
            "goal_cadence": 1,
            "goal_cadence_frequency": 3,
            "goal_creation_month": "2024-11-01",
            "goal_target": 365000,
            "goal_target_month": "2024-11-01",
            "goal_percentage_complete": 33,
            "goal_months_to_budget": 3,
            "goal_under_funded": 0,
            "goal_overall_funded": 121670,
            "goal_overall_left": 243330,
            "deleted": false
          }},
          {{
            "id": "{stromUndWaermeId}",
            "category_group_id": "5a22fa0a-e462-4d5d-a25f-9ffc34917c77",
            "category_group_name": "Wohnen",
            "name": "{stromUndWaermeName}",
            "hidden": false,
            "original_category_group_id": null,
            "note": null,
            "budgeted": 0,
            "activity": 0,
            "balance": 0,
            "goal_type": "NEED",
            "goal_needs_whole_amount": true,
            "goal_day": null,
            "goal_cadence": 1,
            "goal_cadence_frequency": 1,
            "goal_creation_month": "2025-05-01",
            "goal_target": 392000,
            "goal_target_month": null,
            "goal_percentage_complete": 100,
            "goal_months_to_budget": 1,
            "goal_under_funded": 0,
            "goal_overall_funded": 392000,
            "goal_overall_left": 0,
            "deleted": false
          }}
        ]
      }}
    ],
    "server_knowledge": 38880
  }}
}}
"""

[<Tests>]
let tests =
    testList "RulesEngine Tests" [
        testCase "Exact Memo Matching" <| fun _ ->
            let rulesConfig = [{ Match = "REWE"; Category = "Groceries" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Groceries", groceriesId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            Expect.isOk compiledResult "Rule compilation should succeed"
            let compiledRules =
                match compiledResult with
                | Ok res -> res
                | Error e -> failwith $"Expected Ok but got Error: {e}"

            let memo = "Shopping at REWE"
            let classifiedCategoryId = RulesEngine.classify compiledRules (Some memo)
            Expect.equal classifiedCategoryId (Some groceriesId) "Should classify 'REWE' memo to Groceries"

        testCase "Regex Memo Matching" <| fun _ ->
            let rulesConfig = [{ Match = ".*Amazon.*"; Category = "Shopping" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Shopping", shoppingId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            Expect.isOk compiledResult "Rule compilation should succeed"
            let compiledRules =
                match compiledResult with
                | Ok res -> res
                | Error e -> failwith $"Expected Ok but got Error: {e}"

            let memo = "Order from Amazon Marketplace"
            let classifiedCategoryId = RulesEngine.classify compiledRules (Some memo)
            Expect.equal classifiedCategoryId (Some shoppingId) "Should classify 'Amazon' memo using regex to Shopping"

        testCase "No Match - Returns None" <| fun _ ->
            let rulesConfig = [{ Match = "NON_MATCHING_RULE"; Category = "Shopping" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Shopping", shoppingId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            Expect.isOk compiledResult "Rule compilation should succeed"
            let compiledRules =
                match compiledResult with
                | Ok res -> res
                | Error e -> failwith $"Expected Ok but got Error: {e}"

            let memo = "Some other transaction"
            let classifiedCategoryId = RulesEngine.classify compiledRules (Some memo)
            Expect.isNone classifiedCategoryId "Should return None when no rule matches"

        testCase "No Memo - Returns None" <| fun _ ->
            let rulesConfig = [{ Match = "REWE"; Category = "Groceries" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Groceries", groceriesId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            Expect.isOk compiledResult "Rule compilation should succeed"
            let compiledRules =
                match compiledResult with
                | Ok res -> res
                | Error e -> failwith $"Expected Ok but got Error: {e}"

            let classifiedCategoryId = RulesEngine.classify compiledRules None // No memo
            Expect.isNone classifiedCategoryId "Should return None when no memo provided"

        testCase "Category Name Resolution Error - Category Not Found" <| fun _ ->
            let rulesConfig = [{ Match = "REWE"; Category = "NonExistentCategory" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Groceries", groceriesId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            
            match compiledResult with
            | Ok _ -> failwith "Rule compilation should fail due to missing category"
            | Error errorMsg ->
                Expect.stringContains errorMsg "NonExistentCategory' not found" "Error message should indicate missing category"

        testCase "Invalid Regex in Rule" <| fun _ ->
            let rulesConfig = [{ Match = "["; Category = "Groceries" }] |> YamlConfig.createRulesConfig
            let categoryMap = createCategoryMap [("Groceries", groceriesId)]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap

            match compiledResult with
            | Ok _ -> failwith "Rule compilation should fail due to invalid regex"
            | Error errorMsg ->
                Expect.stringContains errorMsg "Invalid regex '['" "Error message should indicate invalid regex"

        testCase "Umlaut and Case Insensitive Category Name Resolution" <| fun _ ->
            let rules = [{ Match = "Test"; Category = "Reisen · Bahn" }] // User input for rule
            let rulesConfig = YamlConfig.createRulesConfig rules
            // YNAB category names
            let categoryMap = createCategoryMap [
                ("Reisen · Bahn", travelId);
                ("lebensmittel", groceriesId)
            ]

            let compiledResult = RulesEngine.compileRules rulesConfig categoryMap
            Expect.isOk compiledResult "Rule compilation with umlaut/case variant should succeed"
            let compiledRules =
                match compiledResult with
                | Ok res -> res
                | Error e -> failwith $"Expected Ok but got Error: {e}"

            let classifiedCategoryId = RulesEngine.classify compiledRules (Some "Test")
            Expect.equal classifiedCategoryId (Some travelId) "Should classify 'Test' to Reisen · Bahn"

        testCase "Expected Category Parsing" <| fun _ ->
          let parsed_categories = 
            match RulesEngine.parseCategoriesResponse categories_json with
            | Ok categories -> categories |> Map.toList
            | Error e -> failwith $"Failed to parse categories: {e}"

          let categories_to_check = 
            [
              readyToAssignName, readyToAssignId;
              essenEinkaufenName, essenEinkaufenId;
              essenArbeitName, essenArbeitId;
              essenSchuleName, essenSchuleId;
              tilgungBausparZinsenName, tilgungBausparZinsenId;
              grundsteuernAbfallAbwasserName, grundsteuernAbfallAbwasserId;
              stromUndWaermeName, stromUndWaermeId
            ] 
            |> createCategoryMap
            |> Map.toList
          
          Expect.containsAll parsed_categories categories_to_check "Parsed categories should contain all expected categories from JSON"
           
        testCase "Invalid Category Parsing" <| fun _ ->
            let invalid_json = "{ \"invalid\": \"json\" }"
            match RulesEngine.parseCategoriesResponse invalid_json with
            | Ok _ -> failwith "Expected parsing to fail with invalid JSON"
            | Error errorMsg ->
                Expect.stringContains errorMsg "Failed to parse categories" "Error message should indicate parsing failure"

        testCase "Invalid Category Parsing - Empty JSON" <| fun _ ->
            let empty_json = "{}"
            match RulesEngine.parseCategoriesResponse empty_json with
            | Ok _ -> failwith "Expected parsing to fail with empty JSON"
            | Error errorMsg ->
                Expect.stringContains errorMsg "Failed to parse categories" "Error message should indicate parsing failure"

        // Placeholder for YamlConfig specific tests - these would ideally be in their own file
        // and require file I/O mocking or temporary file creation.
        testCase "Placeholder: YamlConfig - Invalid YAML Structure" <| fun _ ->
            Expect.isTrue true "This test would validate YamlConfig.parseRulesFile with bad YAML."

        testCase "Placeholder: YamlConfig - Rule Validation (e.g. missing 'match')" <| fun _ ->
            Expect.isTrue true "This test would validate YamlConfig.parseRulesFile schema checks."
    ]