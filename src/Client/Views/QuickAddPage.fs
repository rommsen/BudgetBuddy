module Views.QuickAddPage

// Quick Add as a standalone, first-class page (ynab-q7k3m).
//
// Phase 0 of the YNAB-replacement vision (ADR 0003): cash expenses entered on
// the phone, no bank sync session required. Previously a bottom sheet inside the
// SyncFlow component; now a top-level page reachable from the main navigation.
// The form state lives in the top-level Model; this view only renders it and
// dispatches the lifted Quick Add messages.
//
// The submit pushes onto the configured Quick-Add account, without an ImportId
// (ADR 0004) — that behaviour is unchanged by the lift; it lives in State.update.
// The field markup reuses the established `qa-*` styles (already styleguide-
// conformant); the only change is the page container + page header in place of
// the sheet chrome. The category picker stays an elevated sheet layer.

open Feliz
open Types
open Shared.Domain
open Client.DesignSystem
open State

let private emptyForm : QuickAddFormState = {
    AmountText = ""
    IsOutflow = true
    Payee = ""
    CategoryId = ""
    DateText = ""
    Memo = ""
    ShowCategoryPicker = false
    IsSaving = false
    Error = None
}

/// One tappable template chip: payee (or category) + signed amount. Tapping it
/// prefills the whole form (ynab-t4n8p) — nothing is posted until Speichern.
let private templateChip (dispatch: Msg -> unit) (template: QuickAddTemplate) : ReactElement =
    let primaryLabel =
        match template.PayeeName with
        | Some p when p.Trim() <> "" -> p.Trim()
        | _ -> template.CategoryName |> Option.defaultValue "Buchung"

    let amountLabel = $"{formatAmountForInput template.Amount} €"

    Html.button [
        prop.className (
            if template.IsOutflow then "qa-template-chip outflow" else "qa-template-chip inflow"
        )
        prop.onClick (fun _ -> dispatch (PrefillQuickAdd template))
        prop.children [
            Html.span [ prop.className "qa-template-payee"; prop.text primaryLabel ]
            Html.span [ prop.className "qa-template-amount"; prop.text amountLabel ]
        ]
    ]

/// The recent-bookings template row, rendered only when there is something to
/// show. Loading / failure / empty all collapse to nothing — the form stays
/// fully usable on its own (e.g. a brand-new Quick-Add account).
let private templatesSection (templates: RemoteData<QuickAddTemplate list>) (dispatch: Msg -> unit) : ReactElement =
    match templates with
    | Success ts when not (List.isEmpty ts) ->
        Html.div [
            prop.className "qa-templates"
            prop.children [
                Html.span [ prop.className "qa-templates-label"; prop.text "Letzte Buchungen" ]
                Html.div [
                    prop.className "qa-templates-chips"
                    prop.children (ts |> List.map (templateChip dispatch))
                ]
            ]
        ]
    | _ -> Html.none

let view
    (formOpt: QuickAddFormState option)
    (categories: YnabCategory list)
    (recentCategoryIds: YnabCategoryId list)
    (templates: RemoteData<QuickAddTemplate list>)
    (dispatch: Msg -> unit)
    : ReactElement =

    let form = formOpt |> Option.defaultValue emptyForm
    let update (newForm: QuickAddFormState) = dispatch (UpdateQuickAdd newForm)

    let categoryOptions =
        categories
        |> List.map (fun cat ->
            let (YnabCategoryId id) = cat.Id
            (id.ToString(), $"{cat.GroupName}: {cat.Name}"))

    let selectedCategoryName =
        categoryOptions
        |> List.tryFind (fun (id, _) -> id = form.CategoryId)
        |> Option.map snd

    let recentCats =
        recentCategoryIds
        |> List.map (fun (YnabCategoryId guid) -> guid.ToString())

    React.fragment [
        Html.div [
            prop.className "max-w-lg mx-auto animate-fade-in"
            prop.children [
                PageHeader.gradientWithSubtitle "Quick Add" "Bar-Ausgabe direkt in YNAB erfassen"

                // Recurring-booking templates: one tap prefills the whole form.
                templatesSection templates dispatch

                Html.div [
                    prop.className "qa-page space-y-4"
                    prop.children [
                        // Direction toggle (expense is the overwhelmingly common case)
                        Html.div [
                            prop.className "qa-direction"
                            prop.children [
                                Html.button [
                                    prop.className (if form.IsOutflow then "qa-direction-btn active outflow" else "qa-direction-btn")
                                    prop.onClick (fun _ -> update { form with IsOutflow = true })
                                    prop.text "Ausgabe"
                                ]
                                Html.button [
                                    prop.className (if not form.IsOutflow then "qa-direction-btn active inflow" else "qa-direction-btn")
                                    prop.onClick (fun _ -> update { form with IsOutflow = false })
                                    prop.text "Einnahme"
                                ]
                            ]
                        ]

                        // Amount (decimal keyboard on mobile, 16px+ to avoid iOS zoom)
                        Html.div [
                            prop.className "qa-amount-wrap"
                            prop.children [
                                Html.input [
                                    prop.className "qa-amount"
                                    prop.type' "text"
                                    prop.custom ("inputMode", "decimal")
                                    prop.placeholder "0,00"
                                    prop.value form.AmountText
                                    prop.autoFocus (Viewport.isFinePointer ())
                                    prop.onChange (fun (v: string) -> update { form with AmountText = v })
                                ]
                                Html.span [ prop.className "qa-currency"; prop.text "€" ]
                            ]
                        ]

                        // Category — opens the full category picker (suggestions,
                        // recents, search) on the elevated sheet layer
                        Html.div [
                            prop.className "qa-field"
                            prop.children [
                                Html.label [ prop.text "Kategorie" ]
                                Html.div [
                                    prop.className "qa-cat-row"
                                    prop.children [
                                        Html.button [
                                            prop.className (
                                                match selectedCategoryName with
                                                | Some _ -> "qa-cat-field selected"
                                                | None -> "qa-cat-field"
                                            )
                                            prop.onClick (fun _ -> update { form with ShowCategoryPicker = true })
                                            prop.text (selectedCategoryName |> Option.defaultValue "Kategorie wählen…")
                                        ]
                                        if selectedCategoryName.IsSome then
                                            Html.button [
                                                prop.className "qa-cat-clear"
                                                prop.ariaLabel "Kategorie entfernen"
                                                prop.onClick (fun _ -> update { form with CategoryId = "" })
                                                prop.text "✕"
                                            ]
                                    ]
                                ]
                            ]
                        ]

                        Html.div [
                            prop.className "qa-field"
                            prop.children [
                                Html.label [ prop.text "Payee (optional)" ]
                                Html.input [
                                    prop.className "qa-input"
                                    prop.type' "text"
                                    prop.placeholder "z. B. Bäcker"
                                    prop.value form.Payee
                                    prop.onChange (fun (v: string) -> update { form with Payee = v })
                                ]
                            ]
                        ]

                        Html.div [
                            prop.className "qa-row"
                            prop.children [
                                Html.div [
                                    prop.className "qa-field"
                                    prop.children [
                                        Html.label [ prop.text "Datum" ]
                                        Html.input [
                                            prop.className "qa-input"
                                            prop.type' "date"
                                            prop.value form.DateText
                                            prop.onChange (fun (v: string) -> update { form with DateText = v })
                                        ]
                                    ]
                                ]
                                Html.div [
                                    prop.className "qa-field"
                                    prop.children [
                                        Html.label [ prop.text "Memo (optional)" ]
                                        Html.input [
                                            prop.className "qa-input"
                                            prop.type' "text"
                                            prop.placeholder ""
                                            prop.value form.Memo
                                            prop.onChange (fun (v: string) -> update { form with Memo = v })
                                        ]
                                    ]
                                ]
                            ]
                        ]

                        match form.Error with
                        | Some error ->
                            Html.div [
                                prop.className "qa-error"
                                prop.text error
                            ]
                        | None -> ()

                        // Submit — full-width primary DS button (no Cancel: a page is
                        // left by navigating away, not by dismissing a sheet).
                        Button.view {
                            Button.defaultProps with
                                Text = "Speichern"
                                Variant = Button.Primary
                                FullWidth = true
                                IsLoading = form.IsSaving
                                OnClick = fun () -> dispatch SubmitQuickAdd
                        }
                    ]
                ]
            ]
        ]

        // Category picker on the elevated layer, above the page
        BottomSheet.categoryPickerLayered
            form.ShowCategoryPicker
            (form.Payee.Trim())
            categoryOptions
            []
            recentCats
            (fun catId -> update { form with CategoryId = catId; ShowCategoryPicker = false })
            (fun () -> update { form with ShowCategoryPicker = false })
    ]
