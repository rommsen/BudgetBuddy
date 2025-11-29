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

let private targetFieldText (targetField: TargetField) =
    match targetField with
    | Payee -> "Payee"
    | Memo -> "Memo"
    | Combined -> "Combined"

// ============================================
// Rule Row Component
// ============================================

let private ruleRow (rule: Rule) (dispatch: Msg -> unit) =
    Html.tr [
        prop.className (if not rule.Enabled then "opacity-50" else "")
        prop.children [
            Html.td [
                Html.div [
                    prop.className "font-medium"
                    prop.text rule.Name
                ]
            ]
            Html.td [
                prop.className "font-mono text-sm"
                prop.text rule.Pattern
            ]
            Html.td [
                Html.span [
                    prop.className "badge badge-ghost badge-sm"
                    prop.text (patternTypeText rule.PatternType)
                ]
            ]
            Html.td [
                Html.span [
                    prop.className "badge badge-ghost badge-sm"
                    prop.text (targetFieldText rule.TargetField)
                ]
            ]
            Html.td [ prop.text rule.CategoryName ]
            Html.td [
                Html.input [
                    prop.type'.checkbox
                    prop.className "toggle toggle-primary toggle-sm"
                    prop.isChecked rule.Enabled
                    prop.onChange (fun (_: bool) -> dispatch (ToggleRuleEnabled rule.Id))
                ]
            ]
            Html.td [
                Html.div [
                    prop.className "flex gap-2"
                    prop.children [
                        Html.button [
                            prop.className "btn btn-ghost btn-xs"
                            prop.text "Edit"
                            prop.onClick (fun _ -> dispatch (EditRule rule.Id))
                        ]
                        Html.button [
                            prop.className "btn btn-ghost btn-xs text-error"
                            prop.text "Delete"
                            prop.onClick (fun _ -> dispatch (DeleteRule rule.Id))
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Rules Table Component
// ============================================

let private rulesTable (rules: Rule list) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "overflow-x-auto"
        prop.children [
            Html.table [
                prop.className "table table-zebra"
                prop.children [
                    Html.thead [
                        Html.tr [
                            Html.th [ prop.text "Name" ]
                            Html.th [ prop.text "Pattern" ]
                            Html.th [ prop.text "Type" ]
                            Html.th [ prop.text "Field" ]
                            Html.th [ prop.text "Category" ]
                            Html.th [ prop.text "Enabled" ]
                            Html.th [ prop.text "Actions" ]
                        ]
                    ]
                    Html.tbody [
                        for rule in rules do
                            ruleRow rule dispatch
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
        prop.className "text-center py-12"
        prop.children [
            Html.div [
                prop.className "text-6xl mb-4"
                prop.text "📋"
            ]
            Html.h3 [
                prop.className "text-xl font-medium mb-2"
                prop.text "No Rules Yet"
            ]
            Html.p [
                prop.className "text-gray-600 mb-4"
                prop.text "Create categorization rules to automatically categorize your transactions."
            ]
            Html.button [
                prop.className "btn btn-primary"
                prop.text "Add Your First Rule"
                prop.onClick (fun _ -> dispatch OpenNewRuleModal)
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
            // Header with buttons
            Html.div [
                prop.className "flex justify-between items-center"
                prop.children [
                    Html.h1 [
                        prop.className "text-3xl font-bold"
                        prop.text "Categorization Rules"
                    ]
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "Add Rule"
                                prop.onClick (fun _ -> dispatch OpenNewRuleModal)
                            ]
                            // Export/Import buttons - placeholder for Milestone 9
                            Html.div [
                                prop.className "dropdown dropdown-end"
                                prop.children [
                                    Html.label [
                                        prop.className "btn btn-ghost"
                                        prop.tabIndex 0
                                        prop.text "..."
                                    ]
                                    Html.ul [
                                        prop.className "dropdown-content z-[1] menu p-2 shadow bg-base-100 rounded-box w-52"
                                        prop.tabIndex 0
                                        prop.children [
                                            Html.li [
                                                Html.a [
                                                    prop.text "Export Rules"
                                                    prop.onClick (fun _ ->
                                                        dispatch (ShowToast ("Export will be implemented in Milestone 9", ToastInfo))
                                                    )
                                                ]
                                            ]
                                            Html.li [
                                                Html.a [
                                                    prop.text "Import Rules"
                                                    prop.onClick (fun _ ->
                                                        dispatch (ShowToast ("Import will be implemented in Milestone 9", ToastInfo))
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

            // Info banner
            Html.div [
                prop.className "alert alert-info"
                prop.children [
                    Html.span [
                        prop.text "Rules are applied in priority order. The first matching rule will be used to categorize a transaction."
                    ]
                ]
            ]

            // Rules content
            Html.div [
                prop.className "card bg-base-100 shadow-xl"
                prop.children [
                    Html.div [
                        prop.className "card-body"
                        prop.children [
                            match model.Rules with
                            | NotAsked ->
                                Html.div [
                                    prop.className "text-center p-4"
                                    prop.children [
                                        Html.button [
                                            prop.className "btn btn-primary"
                                            prop.text "Load Rules"
                                            prop.onClick (fun _ -> dispatch LoadRules)
                                        ]
                                    ]
                                ]
                            | Loading ->
                                Html.div [
                                    prop.className "flex justify-center p-8"
                                    prop.children [
                                        Html.span [ prop.className "loading loading-spinner loading-lg" ]
                                    ]
                                ]
                            | Success rules when rules.IsEmpty ->
                                emptyState dispatch
                            | Success rules ->
                                rulesTable rules dispatch
                            | Failure error ->
                                Html.div [
                                    prop.className "alert alert-error"
                                    prop.children [
                                        Html.span [ prop.text error ]
                                        Html.button [
                                            prop.className "btn btn-sm"
                                            prop.text "Retry"
                                            prop.onClick (fun _ -> dispatch LoadRules)
                                        ]
                                    ]
                                ]
                        ]
                    ]
                ]
            ]

            // Edit modal placeholder - full implementation in Milestone 9
            match model.EditingRule with
            | Some rule ->
                Html.div [
                    prop.className "modal modal-open"
                    prop.children [
                        Html.div [
                            prop.className "modal-box"
                            prop.children [
                                Html.h3 [
                                    prop.className "font-bold text-lg"
                                    prop.text $"Edit Rule: {rule.Name}"
                                ]
                                Html.p [
                                    prop.className "py-4 text-gray-600"
                                    prop.text "Full rule editing form will be implemented in Milestone 9"
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
                            prop.className "modal-backdrop"
                            prop.onClick (fun _ -> dispatch CloseRuleModal)
                        ]
                    ]
                ]
            | None -> Html.none
        ]
    ]
