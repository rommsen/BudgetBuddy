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
                                                    prop.onClick (fun _ -> dispatch (ShowToast ("Export will be implemented in Milestone 9", ToastInfo)))
                                                    prop.children [
                                                        Html.span [ prop.text "⬇️" ]
                                                        Html.span [ prop.text "Export Rules" ]
                                                    ]
                                                ]
                                            ]
                                            Html.li [
                                                Html.a [
                                                    prop.className "flex items-center gap-2"
                                                    prop.onClick (fun _ -> dispatch (ShowToast ("Import will be implemented in Milestone 9", ToastInfo)))
                                                    prop.children [
                                                        Html.span [ prop.text "⬆️" ]
                                                        Html.span [ prop.text "Import Rules" ]
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

            // Edit modal placeholder
            match model.EditingRule with
            | Some rule ->
                Html.div [
                    prop.className "modal modal-open"
                    prop.children [
                        Html.div [
                            prop.className "modal-box max-w-lg"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center justify-between mb-4"
                                    prop.children [
                                        Html.h3 [
                                            prop.className "text-lg font-bold"
                                            prop.text $"Edit: {rule.Name}"
                                        ]
                                        Html.button [
                                            prop.className "btn btn-ghost btn-sm btn-square"
                                            prop.onClick (fun _ -> dispatch CloseRuleModal)
                                            prop.children [
                                                Html.span [ prop.text "✕" ]
                                            ]
                                        ]
                                    ]
                                ]
                                Html.div [
                                    prop.className "flex flex-col items-center py-8 text-center"
                                    prop.children [
                                        Html.div [
                                            prop.className "w-16 h-16 rounded-full bg-base-200 flex items-center justify-center mb-4"
                                            prop.children [
                                                Html.span [ prop.className "text-3xl opacity-30"; prop.text "✏️" ]
                                            ]
                                        ]
                                        Html.p [
                                            prop.className "text-base-content/60"
                                            prop.text "Full rule editing form will be available in a future update."
                                        ]
                                    ]
                                ]
                                Html.div [
                                    prop.className "modal-action"
                                    prop.children [
                                        Html.button [
                                            prop.className "btn"
                                            prop.text "Close"
                                            prop.onClick (fun _ -> dispatch CloseRuleModal)
                                        ]
                                    ]
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "modal-backdrop bg-black/50"
                            prop.onClick (fun _ -> dispatch CloseRuleModal)
                        ]
                    ]
                ]
            | None -> Html.none
        ]
    ]
