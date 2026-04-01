module Components.SyncFlow.Views.InlineRuleForm

open Feliz
open Fable.Core.JsInterop
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.Icons

// ============================================
// Inline Rule Form Component
// ============================================

[<ReactComponent>]
let inlineRuleForm (formState: InlineRuleFormState) (dispatch: Msg -> unit) =
    // Auto-test: check if pattern matches the transaction
    let testResult, setTestResult = React.useState<bool option>(None)
    let isTesting, setIsTesting = React.useState(false)

    // Debounced auto-test effect
    React.useEffect(
        (fun () ->
            if System.String.IsNullOrWhiteSpace formState.Pattern then
                setTestResult None
                setIsTesting false
            else
                setIsTesting true
                let timer = Browser.Dom.window.setTimeout(
                    (fun () ->
                        async {
                            try
                                let! matches = Api.rules.testRule (formState.Pattern, formState.PatternType, formState.TargetField, formState.TransactionText)
                                setTestResult (Some matches)
                            with _ ->
                                setTestResult (Some false)
                            setIsTesting false
                        } |> Async.StartImmediate
                    ), 300)
                { new System.IDisposable with member _.Dispose() = Browser.Dom.window.clearTimeout timer }
                |> Some
                |> Option.iter ignore
        ),
        [| box formState.Pattern; box formState.PatternType; box formState.TargetField |]
    )

    let displayPayee =
        if System.String.IsNullOrWhiteSpace formState.Pattern then "..."
        else formState.Pattern

    let subtitleText =
        let s = sprintf "Für \"%s\"" displayPayee
        if s.Length > 30 then s.[..29] + "\u2026" else s

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
                Html.span [ prop.text (if formState.IsSaving then "Speichern\u2026" else "Regel erstellen") ]
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
                                    Html.text "Wenn Transaktion enth\u00E4lt"
                                ]
                            ]
                            Html.input [
                                prop.className "pattern-input"
                                prop.type'.text
                                prop.value formState.Pattern
                                prop.onChange (fun (v: string) -> dispatch (UpdateInlineRulePattern v))
                            ]
                            // Auto-test result
                            match isTesting, testResult with
                            | true, _ ->
                                Html.div [
                                    prop.className "test-result"
                                    prop.style [ style.marginTop 6 ]
                                    prop.children [
                                        Html.span [ prop.className "text-text-muted text-xs"; prop.text "Teste\u2026" ]
                                    ]
                                ]
                            | false, Some true ->
                                Html.div [
                                    prop.className "test-result match"
                                    prop.style [ style.marginTop 6 ]
                                    prop.children [
                                        check XS NeonGreen
                                        Html.span [ prop.text "Passt auf diese Transaktion" ]
                                    ]
                                ]
                            | false, Some false ->
                                Html.div [
                                    prop.className "test-result no-match"
                                    prop.style [ style.marginTop 6 ]
                                    prop.children [
                                        x XS NeonRed
                                        Html.span [ prop.text "Passt nicht auf diese Transaktion" ]
                                    ]
                                ]
                            | false, None -> ()
                        ]
                    ]

                    // Pattern Type + Target Field (always visible)
                    Html.div [
                        prop.className "rule-advanced-grid"
                        prop.children [
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
                                            Html.option [ prop.value "Contains"; prop.text "Enth\u00E4lt" ]
                                            Html.option [ prop.value "Exact"; prop.text "Exakt" ]
                                            Html.option [ prop.value "Regex"; prop.text "Regex" ]
                                        ]
                                    ]
                                ]
                            ]
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
                                                svg.custom ("stroke", "currentColor")
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
                ]
            ]
        ]
