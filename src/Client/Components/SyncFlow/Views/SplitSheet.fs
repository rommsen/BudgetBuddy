module Components.SyncFlow.Views.SplitSheet

// Split-Review sheet (ynab-002): split a single reviewed transaction into ≥2
// lines — each a YNAB category OR a transfer to another account (the cashback
// case: one category + one transfer to the cash account).
//
// Structurally a FORM sheet (explicit Speichern/Abbrechen), like Quick Add.
// The category/account picker opens as `.layer-2` OVER it — the proven
// "Picker über Quick-Add-Formular" precedent, one level deep (ADR 0005 §4).
// Click-Commit applies only to the picker ITEMS; a tap in the picker must not
// commit/close the split form (ADR 0005). All arithmetic/validation flows
// through the shared ynab-001 domain helpers via Types.fs (ADR 0006).

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

/// Human label for a draft line's target (or a prompt to choose one).
let private targetLabel (line: SplitDraftLine) : string =
    match line.Target with
    | Some (ToCategory (_, name)) -> name
    | Some (ToTransfer (_, name)) -> $"→ {name}"
    | None -> "Kategorie oder Konto…"

let private isTransfer (line: SplitDraftLine) : bool =
    match line.Target with
    | Some (ToTransfer _) -> true
    | _ -> false

/// One editable split line: target picker buttons, amount, remove.
/// `autoAmount` is the live remainder shown (read-only) for an auto-remainder
/// ("Rest") line — the cashback category line the user does not type into.
let private splitLineRow (index: int) (line: SplitDraftLine) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "split-line"
        prop.key (string index)
        prop.children [
            // Target chooser: a category button + a transfer button. Tapping
            // either opens the matching layered picker for THIS line.
            Html.div [
                prop.className "split-target-row"
                prop.children [
                    Html.button [
                        prop.className (
                            match line.Target with
                            | Some _ -> "qa-cat-field selected"
                            | None -> "qa-cat-field")
                        prop.onClick (fun _ -> dispatch (OpenSplitCategoryPicker index))
                        prop.text (targetLabel line)
                    ]
                    Html.button [
                        prop.className (if isTransfer line then "split-transfer-btn active" else "split-transfer-btn")
                        prop.title "Transfer-Konto wählen"
                        prop.ariaLabel "Transfer-Konto wählen"
                        prop.onClick (fun _ -> dispatch (OpenSplitAccountPicker index))
                        prop.children [ Icons.sync Icons.SM Icons.NeonTeal ]
                    ]
                ]
            ]
            // Amount (positive magnitude, editable) + a per-line "Rest" button
            // that fills the leftover so the split balances (ynab-003) + remove.
            Html.div [
                prop.className "split-amount-row"
                prop.children [
                    Html.input [
                        prop.className "qa-input split-amount-input"
                        prop.type' "text"
                        prop.custom ("inputMode", "decimal")
                        prop.placeholder "0,00"
                        prop.value line.AmountText
                        prop.onChange (fun (v: string) -> dispatch (UpdateSplitAmountText (index, v)))
                    ]
                    Html.span [ prop.className "split-currency"; prop.text "€" ]
                    Html.button [
                        prop.className "split-rest-btn"
                        prop.title "Restbetrag einsetzen"
                        prop.ariaLabel "Restbetrag einsetzen"
                        prop.onClick (fun _ -> dispatch (FillSplitRemainder index))
                        prop.text "Rest"
                    ]
                    Html.button [
                        prop.className "split-remove-btn"
                        prop.ariaLabel "Zeile entfernen"
                        prop.title "Zeile entfernen"
                        prop.onClick (fun _ -> dispatch (RemoveSplit index))
                        prop.text "✕"
                    ]
                ]
            ]
        ]
    ]

/// The split sheet (always mounted; visibility via IsOpen so the entrance
/// animation plays). `state` is the active SplitEditState (None when closed).
let splitSheet (model: Model) (dispatch: Msg -> unit) =
    let isOpen = model.SplitEdit.IsSome

    match model.SplitEdit with
    | None ->
        // Keep the sheet mounted (closed) so the close animation can play.
        BottomSheet.view
            { IsOpen = false
              OnClose = fun () -> dispatch CancelSplitEdit
              Title = "Aufteilen"
              Subtitle = None
              Footer = None }
            []
    | Some state ->
        let remainder = splitEditRemainder state
        let balanced = remainder = 0m
        let saveEnabled = canSaveSplits state

        let categoryOptions : BottomSheet.CategoryPickerOption list =
            model.Categories
            |> List.map (fun cat ->
                let (YnabCategoryId id) = cat.Id
                { Id = id.ToString()
                  Label = $"{cat.GroupName}: {cat.Name}"
                  Available = cat.Available })

        // Transfer-target picker: ONLY open on-budget accounts (AC 4), via the
        // shared `openOnBudgetAccounts` filter (no client reimplementation).
        let accountOptions =
            openOnBudgetAccounts model.Accounts
            |> List.map (fun acc ->
                let (YnabAccountId id) = acc.Id
                (id.ToString(), acc.Name))

        let recentCats =
            model.RecentlyUsedCategoryIds
            |> List.map (fun (YnabCategoryId guid) -> guid.ToString())

        // Which line a picker is open for (so the commit targets the right line).
        let pickerLineIndex =
            match state.ActivePicker with
            | CategoryPickerFor i | AccountPickerFor i -> Some i
            | NoPicker -> None

        let categoryPickerOpen =
            match state.ActivePicker with CategoryPickerFor _ -> true | _ -> false
        let accountPickerOpen =
            match state.ActivePicker with AccountPickerFor _ -> true | _ -> false

        React.fragment [
            BottomSheet.view
                {
                    IsOpen = isOpen
                    OnClose = fun () -> dispatch CancelSplitEdit
                    Title = "Aufteilen"
                    Subtitle = Some "Kategorie und/oder Transfer (z. B. Barabhebung)"
                    Footer = Some [
                        Html.button [
                            prop.className "btn-cancel"
                            prop.text "Abbrechen"
                            prop.onClick (fun _ -> dispatch CancelSplitEdit)
                        ]
                        Html.button [
                            prop.className "qa-submit"
                            prop.disabled (not saveEnabled)
                            prop.onClick (fun _ -> dispatch SaveSplits)
                            prop.text "Speichern"
                        ]
                    ]
                }
                [
                    if isOpen then
                        // Total being split
                        Html.div [
                            prop.className "split-total"
                            prop.children [
                                Html.span [ prop.className "split-total-label"; prop.text "Gesamt" ]
                                Money.simple state.Total.Amount state.Currency
                            ]
                        ]

                        // Editable lines
                        Html.div [
                            prop.className "split-lines"
                            prop.children [
                                for i, line in state.Lines |> List.indexed do
                                    splitLineRow i line dispatch
                            ]
                        ]

                        // Add line
                        Html.button [
                            prop.className "split-add-line"
                            prop.onClick (fun _ -> dispatch AddSplitLine)
                            prop.text "+ Zeile hinzufügen"
                        ]

                        // Live remainder preview: the still-unallocated magnitude
                        // (0 = balanced). Tap a line's "Rest" button to fill it.
                        Html.div [
                            prop.className (if balanced then "split-remainder balanced" else "split-remainder")
                            prop.children [
                                Html.span [
                                    prop.className "split-remainder-label"
                                    prop.text (if balanced then "Stimmt" else "Rest")
                                ]
                                Money.simple remainder state.Currency
                            ]
                        ]
                ]

            // Layered category picker (over the split form, one level deep)
            BottomSheet.categoryPickerLayered
                categoryPickerOpen
                ""
                categoryOptions
                []
                recentCats
                (fun catId ->
                    match pickerLineIndex, System.Guid.TryParse catId with
                    | Some i, (true, guid) -> dispatch (SelectSplitCategory (i, YnabCategoryId guid))
                    | _ -> dispatch CloseSplitPicker)
                (fun () -> dispatch CloseSplitPicker)

            // Layered transfer-account picker (open on-budget accounts only)
            BottomSheet.accountPickerLayered
                accountPickerOpen
                "Transfer-Konto wählen"
                accountOptions
                (fun accId ->
                    match pickerLineIndex, System.Guid.TryParse accId with
                    | Some i, (true, guid) -> dispatch (SelectSplitAccount (i, YnabAccountId guid))
                    | _ -> dispatch CloseSplitPicker)
                (fun () -> dispatch CloseSplitPicker)
        ]
