module Components.Rules.Types

open Shared.Domain
open Types

/// Rules-specific model state
type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    Categories: YnabCategory list

    // Rule form state
    RuleFormName: string
    RuleFormPattern: string
    RuleFormPatternType: PatternType
    RuleFormTargetField: TargetField
    RuleFormCategoryId: YnabCategoryId option
    RuleFormPayeeOverride: string
    RuleFormEnabled: bool
    RuleFormTestInput: string
    RuleFormTestResult: string option
    RuleSaving: bool

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
