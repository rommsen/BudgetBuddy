module Components.Rules.Types

open Shared.Domain
open Types

/// State for the rule editor form
type RuleFormState = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId option
    PayeeOverride: string
    Enabled: bool
    TestInput: string
    TestResult: string option
    IsSaving: bool
}

module RuleFormState =
    /// Empty form state for new rules
    let empty : RuleFormState = {
        Name = ""
        Pattern = ""
        PatternType = Contains
        TargetField = Combined
        CategoryId = None
        PayeeOverride = ""
        Enabled = true
        TestInput = ""
        TestResult = None
        IsSaving = false
    }

    /// Populate form from an existing rule for editing
    let fromRule (rule: Rule) : RuleFormState = {
        Name = rule.Name
        Pattern = rule.Pattern
        PatternType = rule.PatternType
        TargetField = rule.TargetField
        CategoryId = Some rule.CategoryId
        PayeeOverride = rule.PayeeOverride |> Option.defaultValue ""
        Enabled = rule.Enabled
        TestInput = ""
        TestResult = None
        IsSaving = false
    }

/// Direction in which a rule is moved within the precedence-ordered list.
/// "Up" raises a rule's precedence (it then wins earlier); "Down" lowers it.
type MoveDirection =
    | Up
    | Down

/// Compute the new full RuleId list (in precedence order, top = highest
/// priority = wins first) after moving `ruleId` one position in `direction`.
///
/// The displayed list is already sorted `priority DESC`, so a move just swaps
/// the rule with its neighbour. Edge moves (top rule up / bottom rule down)
/// and an unknown id are no-ops — the original order is returned unchanged so
/// the server's priorities never desync. The result is always a permutation of
/// the input ids, suitable to hand to `reorderRules` verbatim.
let reorderedIds (direction: MoveDirection) (ruleId: RuleId) (rules: Rule list) : RuleId list =
    let ids = rules |> List.map (fun r -> r.Id)
    match List.tryFindIndex (fun (r: Rule) -> r.Id = ruleId) rules with
    | None -> ids
    | Some index ->
        let swapWith =
            match direction with
            | Up -> index - 1
            | Down -> index + 1
        if swapWith < 0 || swapWith >= List.length ids then
            // Already at the relevant edge — nothing to do.
            ids
        else
            ids
            |> List.mapi (fun i id ->
                if i = index then ids.[swapWith]
                elif i = swapWith then ids.[index]
                else id)

/// Rules-specific model state
type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    Categories: YnabCategory list

    // Consolidated form state
    Form: RuleFormState

    // Delete confirmation state
    ConfirmingDeleteRuleId: RuleId option
}

/// Rules-specific messages
type Msg =
    | LoadRules
    | RulesLoaded of Result<Rule list, string>
    | OpenNewRuleModal
    | EditRule of RuleId
    | CloseRuleModal
    | ConfirmDeleteRule of RuleId    // First click on trash - shows confirm button
    | CancelConfirmDelete            // Timeout expired - hide confirm button
    | DeleteRule of RuleId           // Second click - actually delete
    | RuleDeleted of Result<RuleId, RulesError>
    | ToggleRuleEnabled of RuleId
    | RuleToggled of Result<Rule, RulesError>
    | MoveRule of RuleId * MoveDirection   // ▲/▼ — change a rule's precedence
    | RulesReordered of Result<unit, string>
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, YnabError>

    // Rule form messages
    | UpdateRuleFormName of string
    | UpdateRuleFormPattern of string
    | UpdateRuleFormPatternType of PatternType
    | UpdateRuleFormTargetField of TargetField
    | UpdateRuleFormCategoryId of YnabCategoryId option
    | UpdateRuleFormPayeeOverride of string
    | UpdateRuleFormEnabled of bool
    | UpdateRuleFormTestInput of string
    | TestRulePattern
    | RulePatternTested of Result<bool, RulesError>
    | SaveRule
    | RuleSaved of Result<Rule, RulesError>
    | ExportRules
    | RulesExported of Result<string, RulesError>
    | ImportRulesStart
    | ImportRules of string
    | RulesImported of Result<int, RulesError>

/// External message to notify parent of events
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
