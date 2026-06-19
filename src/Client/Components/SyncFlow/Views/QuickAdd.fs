module Components.SyncFlow.Views.QuickAdd

// Quick Add: manual transaction entry pushed directly to YNAB.
// Phase 0 of the YNAB-replacement vision (docs/idea-ynab-replacement.md):
// cash expenses entered on the phone, no bank sync session required.
// Target account is the configurable Quick-Add account (Settings → YNAB),
// NOT the bank-import account.

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

let private emptyForm = {
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

/// Compact icon button for the review header (matches .back-btn styling)
let quickAddHeaderButton (dispatch: Msg -> unit) =
    Html.button [
        prop.className "back-btn qa-header-btn"
        prop.ariaLabel "Transaktion eintragen"
        prop.title "Transaktion eintragen"
        prop.onClick (fun _ -> dispatch OpenQuickAdd)
        prop.children [
            Icons.plus Icons.SM Icons.Default
        ]
    ]

/// Full-width secondary button for the start/completed screens
let quickAddEntryButton (dispatch: Msg -> unit) =
    Button.view {
        Button.defaultProps with
            Text = "Transaktion eintragen"
            Variant = Button.Secondary
            FullWidth = true
            Icon = Some (Icons.plus Icons.SM Icons.NeonTeal)
            OnClick = fun () -> dispatch OpenQuickAdd
    }

/// The Quick Add bottom sheet (always mounted; visibility via IsOpen so the
/// entrance animation plays)
let quickAddSheet (model: Model) (dispatch: Msg -> unit) =
    let isOpen = model.QuickAdd.IsSome
    let form = model.QuickAdd |> Option.defaultValue emptyForm

    let update (newForm: QuickAddFormState) = dispatch (UpdateQuickAdd newForm)

    let categoryOptions =
        model.Categories
        |> List.map (fun cat ->
            let (YnabCategoryId id) = cat.Id
            (id.ToString(), $"{cat.GroupName}: {cat.Name}"))

    let selectedCategoryName =
        categoryOptions
        |> List.tryFind (fun (id, _) -> id = form.CategoryId)
        |> Option.map snd

    let recentCats =
        model.RecentlyUsedCategoryIds
        |> List.map (fun (YnabCategoryId guid) -> guid.ToString())

    React.fragment [
        BottomSheet.view
            {
                IsOpen = isOpen
                OnClose = fun () -> dispatch CloseQuickAdd
                Title = "Neue Transaktion"
                Subtitle = Some "Direkt in YNAB erfassen"
                Footer = Some [
                    Html.button [
                        prop.className "btn-cancel"
                        prop.text "Abbrechen"
                        prop.disabled form.IsSaving
                        prop.onClick (fun _ -> dispatch CloseQuickAdd)
                    ]
                    Html.button [
                        prop.className "qa-submit"
                        prop.disabled form.IsSaving
                        prop.onClick (fun _ -> dispatch SubmitQuickAdd)
                        prop.text (if form.IsSaving then "Speichern…" else "Speichern")
                    ]
                ]
            }
            [
                if isOpen then
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
                                prop.autoFocus (isOpen && Viewport.isFinePointer ())
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
            ]

        // Category picker on the elevated layer, above the Quick Add sheet
        BottomSheet.categoryPickerLayered
            (isOpen && form.ShowCategoryPicker)
            (form.Payee.Trim())
            categoryOptions
            []
            recentCats
            (fun catId -> update { form with CategoryId = catId; ShowCategoryPicker = false })
            (fun () -> update { form with ShowCategoryPicker = false })
    ]
