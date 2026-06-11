module Components.SyncFlow.Views.QuickAdd

// Quick Add: manual transaction entry pushed directly to YNAB.
// Phase 0 of the YNAB-replacement vision (docs/idea-ynab-replacement.md):
// cash expenses entered on the phone, no bank sync session required.

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
    IsSaving = false
    Error = None
}

/// Floating action button (bottom-right, above the mobile nav) opening Quick Add
let quickAddFab (dispatch: Msg -> unit) =
    Html.button [
        prop.className "quick-add-fab"
        prop.ariaLabel "Transaktion hinzufügen"
        prop.onClick (fun _ -> dispatch OpenQuickAdd)
        prop.children [
            Svg.svg [
                svg.width 22
                svg.height 22
                svg.viewBox (0, 0, 24, 24)
                svg.custom ("fill", "none")
                svg.custom ("stroke", "currentColor")
                svg.custom ("strokeWidth", "2.5")
                svg.custom ("strokeLinecap", "round")
                svg.children [
                    Svg.path [ svg.d "M12 5v14M5 12h14" ]
                ]
            ]
        ]
    ]

/// The Quick Add bottom sheet (always mounted; visibility via IsOpen so the
/// entrance animation plays)
let quickAddSheet (model: Model) (dispatch: Msg -> unit) =
    let isOpen = model.QuickAdd.IsSome
    let form = model.QuickAdd |> Option.defaultValue emptyForm

    let update (newForm: QuickAddFormState) = dispatch (UpdateQuickAdd newForm)

    let categoriesByGroup =
        model.Categories
        |> List.groupBy (fun cat -> cat.GroupName)

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

                Html.div [
                    prop.className "qa-field"
                    prop.children [
                        Html.label [ prop.text "Payee" ]
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
                    prop.className "qa-field"
                    prop.children [
                        Html.label [ prop.text "Kategorie" ]
                        Html.select [
                            prop.className "qa-select"
                            prop.value form.CategoryId
                            prop.onChange (fun (v: string) -> update { form with CategoryId = v })
                            prop.children [
                                Html.option [ prop.value ""; prop.text "Ohne Kategorie" ]
                                for (groupName, cats) in categoriesByGroup do
                                    Html.optgroup [
                                        prop.custom ("label", groupName)
                                        prop.children [
                                            for cat in cats do
                                                let (YnabCategoryId id) = cat.Id
                                                Html.option [
                                                    prop.value (id.ToString())
                                                    prop.text cat.Name
                                                ]
                                        ]
                                    ]
                            ]
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

/// FAB + sheet, rendered together from the SyncFlow page
let quickAddView (model: Model) (dispatch: Msg -> unit) =
    React.fragment [
        quickAddFab dispatch
        quickAddSheet model dispatch
    ]
