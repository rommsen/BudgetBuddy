module Components.SyncFlow.Views.InlineRuleForm

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Inline Rule Form Component
// ============================================

[<ReactComponent>]
let inlineRuleForm (formState: InlineRuleFormState) (dispatch: Msg -> unit) =
    let advancedOpen, setAdvancedOpen = React.useState(false)

    let displayPayee =
        if System.String.IsNullOrWhiteSpace formState.Pattern then "..."
        else formState.Pattern

    let patternTypeLabel =
        match formState.PatternType with
        | Contains -> "Enthält"
        | Exact -> "Exakt"
        | PatternType.Regex -> "Regex"

    let targetFieldLabel =
        match formState.TargetField with
        | Combined -> "Payee & Memo"
        | Payee -> "Nur Payee"
        | Memo -> "Nur Memo"

    let subtitleText =
        let s = sprintf "Für \"%s\"" displayPayee
        if s.Length > 30 then s.[..29] + "…" else s

    let footerChildren = [
        Html.button [
            prop.className "btn-cancel"
            prop.text "Abbrechen"
            prop.onClick (fun _ -> dispatch CloseInlineRuleForm)
        ]
        Html.button [
            prop.className "btn-import"
            prop.disabled (formState.IsSaving || System.String.IsNullOrWhiteSpace formState.Pattern)
            prop.onClick (fun _ -> dispatch SaveInlineRule)
            prop.children [
                Html.span [ prop.text (if formState.IsSaving then "Speichern…" else "Regel erstellen") ]
                Html.span [ prop.className "btn-import-icon"; prop.text "\u2192" ]
            ]
        ]
    ]

    BottomSheet.view
        { IsOpen = true
          OnClose = fun () -> dispatch CloseInlineRuleForm
          Title = "Regel erstellen"
          Subtitle = Some subtitleText
          Footer = Some footerChildren }
        [
            Html.div [
                prop.className "rule-flow"
                prop.children [
                    // Step 1: Pattern
                    Html.div [
                        prop.className "flow-step"
                        prop.children [
                            Html.div [
                                prop.className "flow-label"
                                prop.children [
                                    Html.span [ prop.className "flow-label-icon step1"; prop.text "1" ]
                                    Html.text "Wenn Transaktion enthält"
                                ]
                            ]
                            Html.input [
                                prop.className "pattern-input"
                                prop.type'.text
                                prop.value formState.Pattern
                                prop.onChange (fun (v: string) -> dispatch (UpdateInlineRulePattern v))
                            ]
                        ]
                    ]

                    // Step 2: Category (read-only)
                    Html.div [
                        prop.className "flow-step"
                        prop.children [
                            Html.div [
                                prop.className "flow-label"
                                prop.children [
                                    Html.span [ prop.className "flow-label-icon step2"; prop.text "2" ]
                                    Html.text "Dann kategorisiere als"
                                ]
                            ]
                            Html.div [
                                prop.className "rule-category-display"
                                prop.children [
                                    Html.span [
                                        prop.className "rule-category-check"
                                        prop.children [
                                            Svg.svg [
                                                svg.viewBox (0, 0, 24, 24)
                                                svg.custom ("fill", "none")
                                                svg.custom ("stroke", "#08081a")
                                                svg.custom ("strokeWidth", "3")
                                                svg.custom ("strokeLinecap", "round")
                                                svg.custom ("strokeLinejoin", "round")
                                                svg.custom ("width", "12")
                                                svg.custom ("height", "12")
                                                svg.children [
                                                    Svg.path [ svg.d "M20 6L9 17l-5-5" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.span [
                                        prop.className "rule-category-name"
                                        prop.text formState.CategoryName
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Step 3: Rule name
                    Html.div [
                        prop.className "flow-step"
                        prop.children [
                            Html.div [
                                prop.className "flow-label"
                                prop.children [
                                    Html.span [ prop.className "flow-label-icon step3"; prop.text "3" ]
                                    Html.text "Regelname"
                                ]
                            ]
                            Html.input [
                                prop.className "rule-name-input"
                                prop.type'.text
                                prop.value formState.RuleName
                                prop.onChange (fun (v: string) -> dispatch (UpdateInlineRuleName v))
                            ]
                        ]
                    ]

                    // Preview
                    Html.div [
                        prop.className "rule-preview"
                        prop.children [
                            Html.div [ prop.className "rule-preview-title"; prop.text "Vorschau" ]
                            Html.div [
                                prop.className "rule-preview-text"
                                prop.children [
                                    Html.text "Transaktionen die "
                                    Html.span [
                                        prop.className "rule-preview-highlight"
                                        prop.text (if System.String.IsNullOrWhiteSpace formState.Pattern then "..." else formState.Pattern)
                                    ]
                                    Html.text " enthalten werden automatisch als "
                                    Html.span [
                                        prop.className "rule-preview-category"
                                        prop.text formState.CategoryName
                                    ]
                                    Html.text " kategorisiert."
                                ]
                            ]
                        ]
                    ]

                    // Advanced Options
                    Html.div [
                        prop.children [
                            Html.button [
                                prop.className (sprintf "rule-advanced-toggle%s" (if advancedOpen then " open" else ""))
                                prop.onClick (fun _ -> setAdvancedOpen (not advancedOpen))
                                prop.children [
                                    Html.span [ prop.className "rule-advanced-chevron"; prop.text "\u203A" ]
                                    Html.text "Erweiterte Optionen"
                                    Html.span [
                                        prop.className "rule-advanced-defaults"
                                        prop.text (sprintf "%s \u00B7 %s" patternTypeLabel targetFieldLabel)
                                    ]
                                ]
                            ]
                            Html.div [
                                prop.className (sprintf "rule-advanced-panel%s" (if advancedOpen then " open" else ""))
                                prop.children [
                                    Html.div [
                                        prop.className "rule-advanced-panel-inner"
                                        prop.children [
                                            Html.div [
                                                prop.className "rule-advanced-content"
                                                prop.children [
                                                    Html.div [
                                                        prop.className "rule-advanced-grid"
                                                        prop.children [
                                                            // Pattern Type
                                                            Html.div [
                                                                prop.className "rule-advanced-row"
                                                                prop.children [
                                                                    Html.label [ prop.className "rule-advanced-label"; prop.text "Mustertyp" ]
                                                                    Html.select [
                                                                        prop.className "rule-select"
                                                                        prop.value (
                                                                            match formState.PatternType with
                                                                            | Contains -> "Contains"
                                                                            | Exact -> "Exact"
                                                                            | PatternType.Regex -> "Regex")
                                                                        prop.onChange (fun (v: string) ->
                                                                            let pt =
                                                                                match v with
                                                                                | "Exact" -> Exact
                                                                                | "Regex" -> PatternType.Regex
                                                                                | _ -> Contains
                                                                            dispatch (UpdateInlineRulePatternType pt))
                                                                        prop.children [
                                                                            Html.option [ prop.value "Contains"; prop.text "Enthält" ]
                                                                            Html.option [ prop.value "Exact"; prop.text "Exakt" ]
                                                                            Html.option [ prop.value "Regex"; prop.text "Regex" ]
                                                                        ]
                                                                    ]
                                                                ]
                                                            ]
                                                            // Target Field
                                                            Html.div [
                                                                prop.className "rule-advanced-row"
                                                                prop.children [
                                                                    Html.label [ prop.className "rule-advanced-label"; prop.text "Suche in" ]
                                                                    Html.select [
                                                                        prop.className "rule-select"
                                                                        prop.value (
                                                                            match formState.TargetField with
                                                                            | Combined -> "Combined"
                                                                            | Payee -> "Payee"
                                                                            | Memo -> "Memo")
                                                                        prop.onChange (fun (v: string) ->
                                                                            let tf =
                                                                                match v with
                                                                                | "Payee" -> Payee
                                                                                | "Memo" -> Memo
                                                                                | _ -> Combined
                                                                            dispatch (UpdateInlineRuleTargetField tf))
                                                                        prop.children [
                                                                            Html.option [ prop.value "Combined"; prop.text "Payee & Memo" ]
                                                                            Html.option [ prop.value "Payee"; prop.text "Nur Payee" ]
                                                                            Html.option [ prop.value "Memo"; prop.text "Nur Memo" ]
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
                        ]
                    ]
                ]
            ]
        ]
