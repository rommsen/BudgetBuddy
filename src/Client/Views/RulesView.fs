module Views.RulesView

open Feliz
open State
open Types
open Shared.Domain

// ============================================
// Helper Functions
// ============================================

let private patternTypeText (patternType: PatternType) =
    match patternType with
    | PatternType.Regex -> "Regex"
    | Contains -> "Contains"
    | Exact -> "Exact"

let private patternTypeBadge (patternType: PatternType) =
    let (color, icon) =
        match patternType with
        | PatternType.Regex -> ("bg-purple-100 text-purple-700 border-purple-200", ".*")
        | Contains -> ("bg-blue-100 text-blue-700 border-blue-200", "~")
        | Exact -> ("bg-emerald-100 text-emerald-700 border-emerald-200", "=")
    Html.span [
        prop.className $"inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-xs font-medium border {color}"
        prop.children [
            Html.span [ prop.className "font-mono"; prop.text icon ]
            Html.span [ prop.text (patternTypeText patternType) ]
        ]
    ]

let private targetFieldText (targetField: TargetField) =
    match targetField with
    | Payee -> "Payee"
    | Memo -> "Memo"
    | Combined -> "Combined"

// ============================================
// Rule Card Component (Mobile-first)
// ============================================

let private ruleCard (rule: Rule) (dispatch: Msg -> unit) =
    let opacityClass = if not rule.Enabled then "opacity-50" else ""
    Html.div [
        prop.className $"card bg-base-100 shadow-sm hover:shadow-md transition-all {opacityClass}"
        prop.children [
            Html.div [
                prop.className "card-body p-4"
                prop.children [
                    // Header row
                    Html.div [
                        prop.className "flex items-start justify-between gap-3"
                        prop.children [
                            Html.div [
                                prop.className "flex-1 min-w-0"
                                prop.children [
                                    Html.div [
                                        prop.className "flex items-center gap-2 flex-wrap"
                                        prop.children [
                                            Html.h3 [
                                                prop.className "font-semibold text-base-content truncate"
                                                prop.text rule.Name
                                            ]
                                            patternTypeBadge rule.PatternType
                                        ]
                                    ]
                                    Html.p [
                                        prop.className "text-sm text-base-content/60 mt-1 font-mono truncate"
                                        prop.title rule.Pattern
                                        prop.text rule.Pattern
                                    ]
                                ]
                            ]
                            Html.input [
                                prop.type'.checkbox
                                prop.className "toggle toggle-primary"
                                prop.isChecked rule.Enabled
                                prop.onChange (fun (_: bool) -> dispatch (ToggleRuleEnabled rule.Id))
                            ]
                        ]
                    ]

                    // Category and target field
                    Html.div [
                        prop.className "flex items-center justify-between mt-3 pt-3 border-t border-base-200"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2"
                                prop.children [
                                    Html.div [
                                        prop.className "w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center"
                                        prop.children [
                                            Html.span [ prop.text "🏷️" ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.children [
                                            Html.p [
                                                prop.className "text-sm font-medium"
                                                prop.text rule.CategoryName
                                            ]
                                            Html.p [
                                                prop.className "text-xs text-base-content/50"
                                                prop.text $"Match in: {targetFieldText rule.TargetField}"
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex gap-1"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-ghost btn-sm btn-square"
                                        prop.onClick (fun _ -> dispatch (EditRule rule.Id))
                                        prop.children [
                                            Html.span [ prop.text "✏️" ]
                                        ]
                                    ]
                                    Html.button [
                                        prop.className "btn btn-ghost btn-sm btn-square text-error"
                                        prop.onClick (fun _ -> dispatch (DeleteRule rule.Id))
                                        prop.children [
                                            Html.span [ prop.text "🗑️" ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Empty State Component
// ============================================

let private emptyState (dispatch: Msg -> unit) =
    Html.div [
        prop.className "flex flex-col items-center justify-center py-16 text-center animate-scale-in"
        prop.children [
            Html.div [
                prop.className "w-24 h-24 rounded-full bg-gradient-to-br from-primary/20 to-secondary/20 flex items-center justify-center mb-6"
                prop.children [
                    Html.span [ prop.className "text-5xl"; prop.text "📋" ]
                ]
            ]
            Html.h3 [
                prop.className "text-xl font-bold mb-2"
                prop.text "No Rules Yet"
            ]
            Html.p [
                prop.className "text-base-content/60 max-w-sm mb-6"
                prop.text "Create categorization rules to automatically categorize your transactions when syncing."
            ]
            Html.button [
                prop.className "btn btn-primary gap-2"
                prop.onClick (fun _ -> dispatch OpenNewRuleModal)
                prop.children [
                    Html.span [ prop.text "+" ]
                    Html.span [ prop.text "Add Your First Rule" ]
                ]
            ]
        ]
    ]

// ============================================
// Rule Edit Modal
// ============================================

let private ruleEditModal (model: Model) (dispatch: Msg -> unit) =
    let isNew = model.IsNewRule
    let title = if isNew then "Create New Rule" else "Edit Rule"
    let subtitle = if isNew then "Set up automatic categorization" else "Modify rule settings"

    // Fixed position modal overlay
    Html.div [
        prop.className "fixed inset-0 z-50 flex items-center justify-center p-4"
        prop.children [
            // Backdrop
            Html.div [
                prop.className "absolute inset-0 bg-black/60 backdrop-blur-sm"
                prop.onClick (fun _ -> dispatch CloseRuleModal)
            ]
            // Modal content - Card style matching the rest of the app
            Html.div [
                prop.className "card relative z-10 w-full max-w-2xl max-h-[90vh] overflow-y-auto bg-base-100 shadow-2xl"
                prop.children [
                    Html.div [
                        prop.className "card-body p-6"
                        prop.children [
                            // Header with icon (matching Settings style)
                            Html.div [
                                prop.className "flex items-start justify-between mb-6"
                                prop.children [
                                    Html.div [
                                        prop.className "flex items-start gap-4"
                                        prop.children [
                                            Html.div [
                                                prop.className "w-12 h-12 rounded-xl bg-gradient-to-br from-primary/20 to-secondary/20 flex items-center justify-center"
                                                prop.children [
                                                    Html.span [ prop.className "text-2xl"; prop.text "📋" ]
                                                ]
                                            ]
                                            Html.div [
                                                prop.children [
                                                    Html.h3 [
                                                        prop.className "text-lg font-bold text-base-content"
                                                        prop.text title
                                                    ]
                                                    Html.p [
                                                        prop.className "text-sm text-base-content/60"
                                                        prop.text subtitle
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.button [
                                        prop.className "btn btn-sm btn-circle btn-ghost"
                                        prop.onClick (fun _ -> dispatch CloseRuleModal)
                                        prop.children [
                                            Html.span [ prop.className "text-lg"; prop.text "✕" ]
                                        ]
                                    ]
                                ]
                            ]

                            // Form
                            Html.div [
                                prop.className "space-y-4"
                                prop.children [
                                    // Name input
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Rule Name" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-200 text-base-content"
                                                prop.type'.text
                                                prop.placeholder "e.g., Amazon Purchases"
                                                prop.value model.RuleFormName
                                                prop.onChange (UpdateRuleFormName >> dispatch)
                                            ]
                                        ]
                                    ]

                                    // Pattern type and target field row
                                    Html.div [
                                        prop.className "grid grid-cols-1 sm:grid-cols-2 gap-4"
                                        prop.children [
                                            // Pattern type
                                            Html.div [
                                                prop.className "form-control w-full"
                                                prop.children [
                                                    Html.label [
                                                        prop.className "label py-1"
                                                        prop.children [
                                                            Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Pattern Type" ]
                                                        ]
                                                    ]
                                                    Html.select [
                                                        prop.className "select select-bordered w-full bg-base-200 text-base-content"
                                                        prop.value (patternTypeText model.RuleFormPatternType)
                                                        prop.onChange (fun (value: string) ->
                                                            let patternType =
                                                                match value with
                                                                | "Regex" -> PatternType.Regex
                                                                | "Exact" -> Exact
                                                                | _ -> Contains
                                                            dispatch (UpdateRuleFormPatternType patternType))
                                                        prop.children [
                                                            Html.option [ prop.value "Contains"; prop.text "Contains - Match substring" ]
                                                            Html.option [ prop.value "Exact"; prop.text "Exact - Match full text" ]
                                                            Html.option [ prop.value "Regex"; prop.text "Regex - Regular expression" ]
                                                        ]
                                                    ]
                                                ]
                                            ]

                                            // Target field
                                            Html.div [
                                                prop.className "form-control w-full"
                                                prop.children [
                                                    Html.label [
                                                        prop.className "label py-1"
                                                        prop.children [
                                                            Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Match Field" ]
                                                        ]
                                                    ]
                                                    Html.select [
                                                        prop.className "select select-bordered w-full bg-base-200 text-base-content"
                                                        prop.value (targetFieldText model.RuleFormTargetField)
                                                        prop.onChange (fun (value: string) ->
                                                            let targetField =
                                                                match value with
                                                                | "Payee" -> Payee
                                                                | "Memo" -> Memo
                                                                | _ -> Combined
                                                            dispatch (UpdateRuleFormTargetField targetField))
                                                        prop.children [
                                                            Html.option [ prop.value "Combined"; prop.text "Combined - Payee & Memo" ]
                                                            Html.option [ prop.value "Payee"; prop.text "Payee only" ]
                                                            Html.option [ prop.value "Memo"; prop.text "Memo only" ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]

                                    // Pattern input
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Pattern" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full font-mono bg-base-200 text-base-content"
                                                prop.type'.text
                                                prop.placeholder (
                                                    match model.RuleFormPatternType with
                                                    | PatternType.Regex -> "e.g., AMAZON\\.\\w+"
                                                    | Contains -> "e.g., amazon"
                                                    | Exact -> "e.g., AMAZON MARKETPLACE"
                                                )
                                                prop.value model.RuleFormPattern
                                                prop.onChange (UpdateRuleFormPattern >> dispatch)
                                            ]
                                            Html.label [
                                                prop.className "label"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "label-text-alt text-base-content/50"
                                                        prop.text (
                                                            match model.RuleFormPatternType with
                                                            | PatternType.Regex -> "Use regular expressions for complex patterns"
                                                            | Contains -> "Matches if the text contains this substring (case-insensitive)"
                                                            | Exact -> "Matches only if the entire text equals this (case-insensitive)"
                                                        )
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]

                                    // Category dropdown
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Category" ]
                                                ]
                                            ]
                                            Html.select [
                                                prop.className "select select-bordered w-full bg-base-200 text-base-content"
                                                prop.value (
                                                    match model.RuleFormCategoryId with
                                                    | Some (YnabCategoryId id) -> id.ToString()
                                                    | None -> ""
                                                )
                                                prop.onChange (fun (value: string) ->
                                                    if System.String.IsNullOrWhiteSpace(value) then
                                                        dispatch (UpdateRuleFormCategoryId None)
                                                    else
                                                        dispatch (UpdateRuleFormCategoryId (Some (YnabCategoryId (System.Guid.Parse value)))))
                                                prop.children [
                                                    Html.option [
                                                        prop.value ""
                                                        prop.text "Select a category..."
                                                    ]
                                                    for category in model.Categories do
                                                        Html.option [
                                                            let (YnabCategoryId id) = category.Id
                                                            prop.value (id.ToString())
                                                            prop.text $"{category.GroupName}: {category.Name}"
                                                        ]
                                                ]
                                            ]
                                            if model.Categories.IsEmpty then
                                                Html.div [
                                                    prop.className "flex items-center gap-2 mt-2 px-3 py-2 bg-warning/10 text-warning rounded-lg text-sm"
                                                    prop.children [
                                                        Html.span [ prop.text "⚠️" ]
                                                        Html.span [ prop.text "No categories loaded. Please configure YNAB first." ]
                                                    ]
                                                ]
                                        ]
                                    ]

                                    // Payee override (optional)
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium text-base-content"; prop.text "Payee Override" ]
                                                    Html.span [ prop.className "label-text-alt badge badge-ghost badge-sm"; prop.text "Optional" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-200 text-base-content"
                                                prop.type'.text
                                                prop.placeholder "Override payee name in YNAB"
                                                prop.value model.RuleFormPayeeOverride
                                                prop.onChange (UpdateRuleFormPayeeOverride >> dispatch)
                                            ]
                                            Html.label [
                                                prop.className "label"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "label-text-alt text-base-content/50"
                                                        prop.text "Leave empty to use original payee"
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]

                                    // Enabled toggle (only for editing)
                                    if not isNew then
                                        Html.div [
                                            prop.className "form-control"
                                            prop.children [
                                                Html.label [
                                                    prop.className "label cursor-pointer justify-start gap-4"
                                                    prop.children [
                                                        Html.input [
                                                            prop.type'.checkbox
                                                            prop.className "toggle toggle-primary"
                                                            prop.isChecked model.RuleFormEnabled
                                                            prop.onChange (fun (checked': bool) -> dispatch (UpdateRuleFormEnabled checked'))
                                                        ]
                                                        Html.span [
                                                            prop.className "label-text font-medium text-base-content"
                                                            prop.text "Rule enabled"
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]

                                    // Test pattern section
                                    Html.div [
                                        prop.className "pt-4 border-t border-base-300"
                                        prop.children [
                                            Html.div [
                                                prop.className "flex items-center gap-2 mb-4"
                                                prop.children [
                                                    Html.span [ prop.className "text-lg"; prop.text "🧪" ]
                                                    Html.span [ prop.className "font-medium text-base-content"; prop.text "Test Your Pattern" ]
                                                ]
                                            ]
                                            Html.div [
                                                prop.className "form-control w-full"
                                                prop.children [
                                                    Html.div [
                                                        prop.className "flex flex-col sm:flex-row gap-2"
                                                        prop.children [
                                                            Html.input [
                                                                prop.className "input input-bordered flex-1 bg-base-200 text-base-content"
                                                                prop.type'.text
                                                                prop.placeholder "Enter sample text to test pattern"
                                                                prop.value model.RuleFormTestInput
                                                                prop.onChange (UpdateRuleFormTestInput >> dispatch)
                                                            ]
                                                            Html.button [
                                                                prop.className "btn btn-outline"
                                                                prop.onClick (fun _ -> dispatch TestRulePattern)
                                                                prop.disabled (System.String.IsNullOrWhiteSpace(model.RuleFormPattern) || System.String.IsNullOrWhiteSpace(model.RuleFormTestInput))
                                                                prop.text "Test"
                                                            ]
                                                        ]
                                                    ]
                                                    match model.RuleFormTestResult with
                                                    | Some result ->
                                                        Html.div [
                                                            let (bgClass, textClass) =
                                                                if result.StartsWith("✅") then ("bg-success/10", "text-success")
                                                                elif result.StartsWith("❌") then ("bg-error/10", "text-error")
                                                                else ("bg-warning/10", "text-warning")
                                                            prop.className $"mt-3 p-3 rounded-lg {bgClass} {textClass} text-sm"
                                                            prop.text result
                                                        ]
                                                    | None -> Html.none
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Actions
                            Html.div [
                                prop.className "flex justify-end gap-3 pt-6 mt-6 border-t border-base-300"
                                prop.children [
                                    Html.button [
                                        prop.className "btn btn-ghost"
                                        prop.onClick (fun _ -> dispatch CloseRuleModal)
                                        prop.text "Cancel"
                                    ]
                                    Html.button [
                                        prop.className "btn btn-primary gap-2"
                                        prop.onClick (fun _ -> dispatch SaveRule)
                                        prop.disabled (
                                            model.RuleSaving ||
                                            System.String.IsNullOrWhiteSpace(model.RuleFormName) ||
                                            System.String.IsNullOrWhiteSpace(model.RuleFormPattern) ||
                                            model.RuleFormCategoryId.IsNone
                                        )
                                        prop.children [
                                            if model.RuleSaving then
                                                Html.span [ prop.className "loading loading-spinner loading-sm" ]
                                            else
                                                Html.span [ prop.text "💾" ]
                                            Html.span [ prop.text (if isNew then "Create Rule" else "Save Changes") ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-6"
        prop.children [
            // Header
            Html.div [
                prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 animate-fade-in"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.h1 [
                                prop.className "text-2xl md:text-4xl font-bold"
                                prop.text "Categorization Rules"
                            ]
                            Html.p [
                                prop.className "text-base-content/60 mt-1"
                                prop.text "Automate transaction categorization."
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary gap-2"
                                prop.onClick (fun _ -> dispatch OpenNewRuleModal)
                                prop.children [
                                    Html.span [ prop.text "+" ]
                                    Html.span [ prop.text "Add Rule" ]
                                ]
                            ]
                            Html.div [
                                prop.className "dropdown dropdown-end"
                                prop.children [
                                    Html.label [
                                        prop.className "btn btn-ghost btn-square"
                                        prop.tabIndex 0
                                        prop.children [
                                            Html.span [ prop.text "⋮" ]
                                        ]
                                    ]
                                    Html.ul [
                                        prop.className "dropdown-content z-[1] menu p-2 shadow-lg bg-base-100 rounded-xl w-52"
                                        prop.tabIndex 0
                                        prop.children [
                                            Html.li [
                                                Html.a [
                                                    prop.className "flex items-center gap-2"
                                                    prop.onClick (fun _ -> dispatch ExportRules)
                                                    prop.children [
                                                        Html.span [ prop.text "⬇️" ]
                                                        Html.span [ prop.text "Export Rules" ]
                                                    ]
                                                ]
                                            ]
                                            Html.li [
                                                Html.label [
                                                    prop.className "flex items-center gap-2 cursor-pointer"
                                                    prop.children [
                                                        Html.span [ prop.text "⬆️" ]
                                                        Html.span [ prop.text "Import Rules" ]
                                                        Html.input [
                                                            prop.type'.file
                                                            prop.accept ".json"
                                                            prop.className "hidden"
                                                            prop.onChange (fun (e: Browser.Types.Event) ->
                                                                let input = e.target :?> Browser.Types.HTMLInputElement
                                                                if input.files.length > 0 then
                                                                    let file = input.files.[0]
                                                                    let reader = Browser.Dom.FileReader.Create()
                                                                    reader.onload <- fun _ ->
                                                                        let content = reader.result :?> string
                                                                        dispatch (ImportRules content)
                                                                    reader.readAsText(file)
                                                            )
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Info tip
            Html.div [
                prop.className "flex items-start gap-3 p-4 bg-primary/5 border border-primary/10 rounded-xl animate-slide-up"
                prop.children [
                    Html.div [
                        prop.className "w-8 h-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center flex-shrink-0"
                        prop.children [
                            Html.span [ prop.text "ℹ️" ]
                        ]
                    ]
                    Html.p [
                        prop.className "text-sm text-base-content/70"
                        prop.text "Rules are applied in priority order. The first matching rule will be used to categorize a transaction."
                    ]
                ]
            ]

            // Rules content
            match model.Rules with
            | NotAsked ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16"
                    prop.children [
                        Html.div [ prop.className "loading loading-spinner loading-lg text-primary" ]
                        Html.p [ prop.className "mt-4 text-base-content/60"; prop.text "Loading rules..." ]
                    ]
                ]
            | Loading ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16"
                    prop.children [
                        Html.div [ prop.className "loading loading-spinner loading-lg text-primary" ]
                    ]
                ]
            | Success rules when rules.IsEmpty ->
                emptyState dispatch
            | Success rules ->
                Html.div [
                    prop.className "grid gap-3 animate-slide-up"
                    prop.children [
                        for rule in rules do
                            ruleCard rule dispatch
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.children [
                        Html.span [ prop.text "⚠️" ]
                        Html.span [ prop.text error ]
                        Html.button [
                            prop.className "btn btn-sm"
                            prop.text "Retry"
                            prop.onClick (fun _ -> dispatch LoadRules)
                        ]
                    ]
                ]

            // Edit/Create modal
            if model.IsNewRule || model.EditingRule.IsSome then
                ruleEditModal model dispatch
        ]
    ]
