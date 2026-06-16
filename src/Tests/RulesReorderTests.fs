module Tests.RulesReorderTests

// Tests for the rule precedence reordering logic (cat-001):
// Components.Rules.Types.reorderedIds computes the new full RuleId list (in
// precedence order, top = highest priority) when a rule is moved up/down.
// The displayed list is already sorted priority DESC, so moving "up" swaps a
// rule with its earlier neighbour and moving "down" swaps it with its later one.

open System
open Expecto
open Shared.Domain
open Components.Rules.Types

// ============================================
// Helpers
// ============================================

let private makeRule (idx: int) : Rule =
    {
        Id = RuleId (Guid.Parse(sprintf "00000000-0000-0000-0000-%012d" idx))
        Name = sprintf "Rule %d" idx
        Pattern = sprintf "pattern-%d" idx
        PatternType = Contains
        TargetField = Combined
        CategoryId = YnabCategoryId (Guid.NewGuid())
        CategoryName = "Test Category"
        PayeeOverride = None
        Priority = 0  // irrelevant for reordering; order is positional
        Enabled = true
        CreatedAt = DateTime.UtcNow
        UpdatedAt = DateTime.UtcNow
    }

let private ids (rules: Rule list) = rules |> List.map (fun r -> r.Id)

[<Tests>]
let reorderTests =
    testList "Rules.reorderedIds" [

        testCase "moving a middle rule up swaps it with the rule above" <| fun () ->
            // [r1; r2; r3] move r2 up -> [r2; r1; r3]
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Up rules.[1].Id rules
            Expect.equal result [ rules.[1].Id; rules.[0].Id; rules.[2].Id ]
                "r2 should move ahead of r1"

        testCase "moving a middle rule down swaps it with the rule below" <| fun () ->
            // [r1; r2; r3] move r2 down -> [r1; r3; r2]
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Down rules.[1].Id rules
            Expect.equal result [ rules.[0].Id; rules.[2].Id; rules.[1].Id ]
                "r2 should move behind r3"

        testCase "moving the top rule up is a no-op (edge)" <| fun () ->
            // This prevents a rule from silently dropping off the top of the list.
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Up rules.[0].Id rules
            Expect.equal result (ids rules) "top rule cannot move further up"

        testCase "moving the bottom rule down is a no-op (edge)" <| fun () ->
            // This prevents a rule from silently dropping off the bottom of the list.
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Down rules.[2].Id rules
            Expect.equal result (ids rules) "bottom rule cannot move further down"

        testCase "moving the top rule down swaps it with the second rule" <| fun () ->
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Down rules.[0].Id rules
            Expect.equal result [ rules.[1].Id; rules.[0].Id; rules.[2].Id ]
                "top rule moves to second position"

        testCase "moving the bottom rule up swaps it with the second-to-last rule" <| fun () ->
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let result = reorderedIds Up rules.[2].Id rules
            Expect.equal result [ rules.[0].Id; rules.[2].Id; rules.[1].Id ]
                "bottom rule moves to second-to-last position"

        testCase "an unknown rule id leaves the order unchanged" <| fun () ->
            let rules = [ makeRule 1; makeRule 2; makeRule 3 ]
            let unknown = RuleId (Guid.NewGuid())
            let result = reorderedIds Up unknown rules
            Expect.equal result (ids rules) "unknown id must not corrupt the order"

        testCase "the reordered list keeps every original rule id exactly once" <| fun () ->
            // Guards against drops/duplications that would desync the server priorities.
            let rules = [ makeRule 1; makeRule 2; makeRule 3; makeRule 4 ]
            let result = reorderedIds Down rules.[1].Id rules
            Expect.equal (List.sort result) (List.sort (ids rules))
                "result is a permutation of the original ids"

        testCase "a single-rule list is unchanged in either direction" <| fun () ->
            let rules = [ makeRule 1 ]
            Expect.equal (reorderedIds Up rules.[0].Id rules) (ids rules) "single rule up no-op"
            Expect.equal (reorderedIds Down rules.[0].Id rules) (ids rules) "single rule down no-op"
    ]
