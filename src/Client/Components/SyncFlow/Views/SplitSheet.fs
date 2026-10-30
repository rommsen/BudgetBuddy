module Components.SyncFlow.Views.SplitSheet

// Split-Review sheet (ynab-002): split a single reviewed transaction into ≥2
// lines — each a YNAB category OR a transfer to another account (the cashback
// case: one category + one transfer to the cash account).
//
// Structurally a FORM sheet (explicit Speichern/Abbrechen), like Quick Add.
// The shared target picker (category or account) opens as `.layer-2` OVER it — the proven
// "Picker über Quick-Add-Formular" precedent, one level deep (ADR 0005 §4).
// Click-Commit applies only to the picker ITEMS; a tap in the picker must not
// commit/close the split form (ADR 0005). All arithmetic/validation flows
// through the shared ynab-001 domain helpers via Types.fs (ADR 0006).

open Feliz
open Components.SyncFlow.Types
open Shared.Domain
open Client.DesignSystem

/// Betrag im Aufteilen-Kopf und in der Restzeile (ADR 0049): neutral, Mono, 500.
/// `warn` färbt nur den Text rot (Speichern ist wegen eines Rests gesperrt).
let splitAmountClass (warn: bool) : string =
    let color = if warn then "text-neon-red" else "text-text-primary"
    $"font-mono font-medium tabular-nums whitespace-nowrap {color}"

let private splitAmount (warn: bool) (amount: decimal) (currency: string) =
    Html.span [
        prop.className (splitAmountClass warn)
        prop.text (Money.formatAmountText false true amount currency)
    ]

/// Human label for a draft line's target (or a prompt to choose one).
let private targetLabel (line: SplitDraftLine) : string =
    match line.Target with
    | Some (ToCategory (_, name)) -> name
    | Some (ToTransfer (_, name)) -> $"→ {name}"
    // BBs Editor erzeugt keine unkategorisierte Zeile; gespiegelte Buchungen können
    // eine tragen, deshalb hat sie hier ein Label statt eines leeren Feldes.
    | Some Uncategorized -> "Ohne Kategorie"
    | None -> "Kategorie oder Konto…"

let private lineCategory (line: SplitDraftLine) : YnabCategoryId option =
    match line.Target with
    | Some (ToCategory (id, _)) -> Some id
    | _ -> None

let private lineAccount (line: SplitDraftLine) : YnabAccountId option =
    match line.Target with
    | Some (ToTransfer (id, _)) -> Some id
    | _ -> None

/// One editable split line: target field, amount, remove.
/// `autoAmount` is the live remainder shown (read-only) for an auto-remainder
/// ("Rest") line — the cashback category line the user does not type into.
let private splitLineRow (index: int) (line: SplitDraftLine) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "split-line"
        prop.key (string index)
        prop.children [
            // The one target field: opens the shared picker for THIS line.
            Html.button [
                prop.className (
                    match line.Target with
                    | Some _ -> "qa-cat-field selected"
                    | None -> "qa-cat-field")
                prop.custom ("data-testid", $"import-split-target-{index}")
                prop.onClick (fun _ -> dispatch (OpenSplitTargetPicker index))
                prop.text (targetLabel line)
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

        // Which line the picker is open for (so the commit targets the right line).
        let pickerLine =
            match state.ActivePicker with
            | TargetPickerFor i -> state.Lines |> List.tryItem i |> Option.map (fun line -> i, line)
            | NoPicker -> None

        // Transfer targets: ONLY open on-budget accounts (AC 4), via the shared
        // `openOnBudgetAccounts` filter (no client reimplementation).
        let pickerProps : CategoryPicker.CategoryPickerProps =
            { Categories = model.Categories
              Current = pickerLine |> Option.bind (snd >> lineCategory)
              Suggested = []
              Recent = model.RecentlyUsedCategoryIds
              Accounts = openOnBudgetAccounts model.Accounts
              CurrentAccount = pickerLine |> Option.bind (snd >> lineAccount)
              OfferWithout = false
              StandLabel = None
              LoadState = CategoryPicker.PickerReady
              OnRetry = ignore
              OnSelect =
                fun choice ->
                    match pickerLine, choice with
                    | Some (i, _), CategoryPicker.ChoseCategory (Some id) -> dispatch (SelectSplitCategory (i, id))
                    | Some (i, _), CategoryPicker.ChoseAccount id -> dispatch (SelectSplitAccount (i, id))
                    | _ -> dispatch CloseSplitPicker
              OnClose = fun () -> dispatch CloseSplitPicker }

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
                                splitAmount false state.Total.Amount state.Currency
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
                            prop.className "split-remainder"
                            prop.children [
                                Html.span [
                                    prop.className "split-remainder-label"
                                    prop.text (if balanced then "Stimmt" else "Rest")
                                ]
                                splitAmount (not balanced && not saveEnabled) remainder state.Currency
                            ]
                        ]
                ]

            // Layered target picker (over the split form, one level deep)
            CategoryPicker.sheetLayered pickerLine.IsSome None pickerProps
        ]
