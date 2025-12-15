module Components.SyncFlow.Views.InlineRuleForm

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Inline Rule Form Component
// ============================================

let inlineRuleForm
    (formState: InlineRuleFormState)
    (dispatch: Msg -> unit) =

    let patternTypeToString pt =
        match pt with
        | Contains -> "Contains"
        | Exact -> "Exact"
        | PatternType.Regex -> "Regex"

    let stringToPatternType s =
        match s with
        | "Exact" -> Exact
        | "Regex" -> PatternType.Regex
        | _ -> Contains

    let targetFieldToString tf =
        match tf with
        | Combined -> "Combined"
        | Payee -> "Payee"
        | Memo -> "Memo"

    let stringToTargetField s =
        match s with
        | "Payee" -> Payee
        | "Memo" -> Memo
        | _ -> Combined

    Html.div [
        prop.className "border-t border-white/5 bg-base-200/50 p-4 animate-fade-in"
        prop.children [
            // Header
            Html.div [
                prop.className "flex items-center justify-between mb-4"
                prop.children [
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            Icons.rules Icons.SM Icons.NeonTeal
                            Html.span [
                                prop.className "text-sm font-medium text-base-content"
                                prop.text "Create Categorization Rule"
                            ]
                        ]
                    ]
                    Html.button [
                        prop.className "p-1 rounded hover:bg-white/10 text-base-content/50 hover:text-base-content"
                        prop.onClick (fun _ -> dispatch CloseInlineRuleForm)
                        prop.children [ Icons.x Icons.SM Icons.Default ]
                    ]
                ]
            ]

            // Form content - responsive grid
            Html.div [
                prop.className "space-y-3"
                prop.children [
                    // Row 1: Rule name + Category display
                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-2 gap-3"
                        prop.children [
                            // Rule name input
                            Html.div [
                                prop.className "space-y-1.5"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-base-content/80"
                                        prop.children [
                                            Html.text "Rule Name "
                                            Html.span [ prop.className "text-neon-red"; prop.text "*" ]
                                        ]
                                    ]
                                    Input.textSimple
                                        formState.RuleName
                                        (UpdateInlineRuleName >> dispatch)
                                        "e.g., Amazon Purchases"
                                ]
                            ]
                            // Category display (read-only)
                            Html.div [
                                prop.className "space-y-1.5"
                                prop.children [
                                    Html.label [
                                        prop.className "block text-sm font-medium text-base-content/80"
                                        prop.text "Category"
                                    ]
                                    Html.div [
                                        prop.className "flex items-center gap-2 px-3 py-2 bg-neon-teal/10 border border-neon-teal/30 rounded-lg text-neon-teal"
                                        prop.children [
                                            Icons.check Icons.SM Icons.NeonTeal
                                            Html.span [ prop.text formState.CategoryName ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Row 2: Pattern + Type + Field
                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-12 gap-3"
                        prop.children [
                            // Pattern (spans 6 cols on desktop)
                            Html.div [
                                prop.className "md:col-span-6"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.children [
                                                    Html.text "Pattern "
                                                    Html.span [ prop.className "text-neon-red"; prop.text "*" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.type' "text"
                                                prop.className "w-full px-3 py-2 bg-base-200 border border-white/10 rounded-lg text-base-content font-mono text-sm focus:border-neon-teal focus:ring-1 focus:ring-neon-teal/50 outline-none transition-all placeholder:text-base-content/30"
                                                prop.value formState.Pattern
                                                prop.onChange (UpdateInlineRulePattern >> dispatch)
                                                prop.placeholder "Text to match..."
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                            // Pattern Type (spans 3 cols)
                            Html.div [
                                prop.className "md:col-span-3"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.text "Type"
                                            ]
                                            Input.selectSimple
                                                (patternTypeToString formState.PatternType)
                                                (fun value -> dispatch (UpdateInlineRulePatternType (stringToPatternType value)))
                                                [
                                                    ("Contains", "Contains")
                                                    ("Exact", "Exact Match")
                                                    ("Regex", "Regex")
                                                ]
                                        ]
                                    ]
                                ]
                            ]
                            // Target Field (spans 3 cols)
                            Html.div [
                                prop.className "md:col-span-3"
                                prop.children [
                                    Html.div [
                                        prop.className "space-y-1.5"
                                        prop.children [
                                            Html.label [
                                                prop.className "block text-sm font-medium text-base-content/80"
                                                prop.text "Match In"
                                            ]
                                            Input.selectSimple
                                                (targetFieldToString formState.TargetField)
                                                (fun value -> dispatch (UpdateInlineRuleTargetField (stringToTargetField value)))
                                                [
                                                    ("Combined", "Payee & Memo")
                                                    ("Payee", "Payee only")
                                                    ("Memo", "Memo only")
                                                ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Row 3: Actions
                    Html.div [
                        prop.className "flex flex-col sm:flex-row justify-end gap-2 pt-2"
                        prop.children [
                            Button.ghost "Cancel" (fun () -> dispatch CloseInlineRuleForm)
                            Form.submitButton
                                "Create Rule"
                                (fun () -> dispatch SaveInlineRule)
                                formState.IsSaving
                                [
                                    ("Rule Name", formState.RuleName)
                                    ("Pattern", formState.Pattern)
                                ]
                        ]
                    ]
                ]
            ]
        ]
    ]
