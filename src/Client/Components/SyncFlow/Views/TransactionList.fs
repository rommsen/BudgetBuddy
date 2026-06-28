module Components.SyncFlow.Views.TransactionList

open Feliz
open Components.SyncFlow.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Components.SyncFlow.Views.TransactionRow

// ============================================
// Transaction Filter Helper
// ============================================

let filterTransactions (filter: TransactionFilter) (transactions: SyncTransaction list) =
    match filter with
    | AllTransactions -> transactions
    | ToBeImported ->
        transactions
        |> List.filter (fun tx ->
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | CategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsSome &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | UncategorizedTransactions ->
        transactions
        |> List.filter (fun tx ->
            tx.CategoryId.IsNone &&
            tx.Status <> Skipped &&
            tx.Status <> Imported)
    | SkippedTransactions ->
        transactions
        |> List.filter (fun tx -> tx.Status = Skipped)
    | ConfirmedDuplicates ->
        transactions
        |> List.filter (fun tx ->
            match tx.DuplicateStatus with
            | ConfirmedDuplicate _ -> true
            | _ -> false)

/// Renders a single filter pill button with label, count, active state, and attention indicator.
let private filterPill (label: string) (count: int) (isActive: bool) (hasAttention: bool) (onClick: unit -> unit) =
    Html.button [
        prop.className (
            "filter-btn"
            + (if isActive then " active" else "")
            + (if hasAttention then " has-attention" else ""))
        prop.onClick (fun _ -> onClick ())
        prop.children [
            Html.text label
            Html.text " "
            Html.span [ prop.className "count"; prop.text (string count) ]
        ]
    ]

// ============================================
// Transaction List View
// ============================================

[<ReactComponent>]
let transactionListView (model: Model) (dispatch: Msg -> unit) =
    // Local React state for BottomSheet
    let categorySheetState, setCategorySheetState =
        React.useState<{| TxId: TransactionId; PayeeName: string |} option>(None)

    // Callback for TransactionRow to open category picker
    let onOpenCategoryPicker (txId: TransactionId) (payeeName: string) =
        setCategorySheetState (Some {| TxId = txId; PayeeName = payeeName |})

    Html.div [
        prop.className "flex flex-col relative"
        prop.children [
            match model.SyncTransactions with
            | Success transactions ->
                let counts = ViewHelpers.calculateImportCounts transactions
                let progress = ViewHelpers.calculateProgressSegments counts

                // === FILTER PILLS ===
                Html.div [
                    prop.className "filters"
                    prop.children [
                        filterPill "Alle" counts.Total (model.ActiveFilter = AllTransactions) false (fun () -> dispatch (SetFilter AllTransactions))
                        filterPill "Import" counts.ToImport (model.ActiveFilter = ToBeImported) false (fun () -> dispatch (SetFilter ToBeImported))
                        filterPill "Pr\u00FCfen" counts.NeedCategory (model.ActiveFilter = UncategorizedTransactions) (counts.NeedCategory > 0) (fun () -> dispatch (SetFilter UncategorizedTransactions))
                        filterPill "Duplikate" counts.Duplicates (model.ActiveFilter = ConfirmedDuplicates) false (fun () -> dispatch (SetFilter ConfirmedDuplicates))
                        filterPill "\u00DCbersprungen" counts.Skipped (model.ActiveFilter = SkippedTransactions) false (fun () -> dispatch (SetFilter SkippedTransactions))
                    ]
                ]

                // === TOP ACTION BAR (compact CTA + bulk actions) ===
                Html.div [
                    prop.className "top-action-bar"
                    prop.children [
                        Html.div [
                            prop.className "top-action-bar-row"
                            prop.children [
                                Html.span [
                                    prop.className "top-action-bar-count"
                                    prop.text (sprintf "%d/%d importieren" counts.ToImport counts.Total)
                                ]
                                Html.button [
                                    prop.className (if counts.NeedCategory = 0 then "btn-import-sm ready" else "btn-import-sm")
                                    prop.disabled (counts.ToImport = 0)
                                    prop.onClick (fun _ -> dispatch ImportToYnab)
                                    prop.children [
                                        Html.span [ prop.text "Importieren" ]
                                        Html.span [ prop.className "btn-import-icon"; prop.text "\u2191" ]
                                    ]
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "top-action-bar-bulk"
                            prop.children [
                                Html.button [
                                    prop.className "bulk-action-link"
                                    prop.onClick (fun _ -> dispatch SkipAllVisible)
                                    prop.text "Alle \u00FCberspringen"
                                ]
                                Html.span [ prop.className "bulk-action-dot"; prop.text "\u00B7" ]
                                Html.button [
                                    prop.className "bulk-action-link"
                                    prop.onClick (fun _ -> dispatch UnskipAllVisible)
                                    prop.text "Alle einschlie\u00DFen"
                                ]
                            ]
                        ]
                    ]
                ]

                // === INFO BANNER (duplicates) ===
                if counts.Duplicates > 0 then
                    Html.div [
                        prop.className "info-banner"
                        prop.children [
                            Html.span [ prop.className "info-banner-icon"; prop.text "i" ]
                            Html.span [
                                prop.children [
                                    Html.strong [ prop.text (sprintf "%d Duplikate" counts.Duplicates) ]
                                    Html.text " automatisch erkannt und \u00FCbersprungen"
                                ]
                            ]
                        ]
                    ]

                // === REJECTED-BY-YNAB BANNER ===
                // Shown after an import where YNAB rejected transactions by import_id
                // (e.g. user previously imported then deleted them in YNAB \u2014 YNAB never
                // releases import_ids, so a normal re-import always fails).
                if not model.DuplicateTransactionIds.IsEmpty then
                    let rejectedCount = model.DuplicateTransactionIds.Length
                    Html.div [
                        prop.className "info-banner warning"
                        prop.children [
                            Html.span [ prop.className "info-banner-icon warning"; prop.text "!" ]
                            Html.span [
                                prop.className "info-banner-text"
                                prop.children [
                                    Html.strong [ prop.text (sprintf "%d Transaktion(en)" rejectedCount) ]
                                    Html.text " von YNAB als Duplikat abgelehnt"
                                ]
                            ]
                            Html.button [
                                prop.className "info-banner-action"
                                prop.onClick (fun _ -> dispatch ForceImportDuplicates)
                                prop.text "Erneut importieren \u2192"
                            ]
                        ]
                    ]

                // === TRANSACTION LIST (scrollable) ===
                let filteredTransactions = filterTransactions model.ActiveFilter transactions
                let groupedTransactions = ViewHelpers.groupTransactionsByDate filteredTransactions

                let categoryOptions =
                    model.Categories
                    |> List.map (fun cat ->
                        let (YnabCategoryId id) = cat.Id
                        (id.ToString(), $"{cat.GroupName}: {cat.Name}"))

                let payeeOptions : Input.ComboBoxOption list =
                    let transfers, regularPayees =
                        model.Payees |> List.partition (fun p -> p.TransferAccountId.IsSome)
                    [
                        if not transfers.IsEmpty then
                            yield Input.sectionHeader "Transfers"
                            for p in transfers |> List.sortBy (fun p -> p.Name) do
                                let (YnabPayeeId id) = p.Id
                                yield { Input.ComboBoxOption.Id = id.ToString(); Label = p.Name; IsDisabled = false }

                        if not regularPayees.IsEmpty then
                            if not transfers.IsEmpty then yield Input.sectionHeader "Payees"
                            for p in regularPayees |> List.sortBy (fun p -> p.Name) do
                                let (YnabPayeeId id) = p.Id
                                yield { Input.ComboBoxOption.Id = id.ToString(); Label = p.Name; IsDisabled = false }
                    ]

                if filteredTransactions.IsEmpty then
                    Card.emptyState
                        (Icons.search Icons.XL Icons.Default)
                        "Keine passenden Transaktionen"
                        "Versuche einen anderen Filter auszuw\u00E4hlen."
                        (Some (Button.ghost "Alle anzeigen" (fun () -> dispatch (SetFilter AllTransactions))))
                else
                    Html.div [
                        prop.className "tx-list"
                        prop.children [
                            for (date, txGroup) in groupedTransactions do
                                Html.div [
                                    prop.className "date-group"
                                    prop.children [
                                        Html.div [
                                            prop.className "date-header"
                                            prop.children [
                                                Html.span [
                                                    prop.text (date.ToString("dd. MMMM yyyy", System.Globalization.CultureInfo("de-DE")))
                                                ]
                                                Html.span [
                                                    prop.className "date-total"
                                                    let total = ViewHelpers.sumDailyMilliunits txGroup
                                                    let totalDecimal = decimal total / 1000m
                                                    prop.text (sprintf "%s \u20AC" (totalDecimal.ToString("N2", System.Globalization.CultureInfo("de-DE"))))
                                                ]
                                            ]
                                        ]
                                        for tx in txGroup do
                                            let (TransactionId id) = tx.Transaction.Id
                                            let isPendingCategorySave =
                                                model.PendingCategoryVersions |> Map.containsKey tx.Transaction.Id
                                            Html.div [
                                                prop.key id
                                                prop.children [
                                                    TransactionRow.transactionRow {
                                                        Transaction = tx
                                                        CategoryOptions = categoryOptions
                                                        PayeeOptions = payeeOptions
                                                        ExpandedIds = model.ExpandedTransactionIds
                                                        InlineRuleFormState = model.InlineRuleForm
                                                        ManuallyCategorizedIds = model.ManuallyCategorizedIds
                                                        IsPendingCategorySave = isPendingCategorySave
                                                        Dispatch = dispatch
                                                        OnOpenCategoryPicker = onOpenCategoryPicker
                                                    }
                                                ]
                                            ]
                                    ]
                                ]
                        ]
                    ]

                // === BOTTOM ACTION BAR ===
                Html.div [
                    prop.className "action-bar"
                    prop.children [
                        Html.div [
                            prop.className "action-bar-info"
                            prop.children [
                                Html.span [
                                    prop.className "action-bar-label"
                                    prop.text (sprintf "%d von %d importieren" counts.ToImport counts.Total)
                                ]
                                if counts.NeedCategory > 0 then
                                    Html.span [
                                        prop.className "action-bar-attention"
                                        prop.text (sprintf "%d ohne Kategorie" counts.NeedCategory)
                                    ]
                            ]
                        ]
                        Html.div [
                            prop.className "progress-track"
                            prop.children [
                                Html.div [
                                    prop.className "progress-segment progress-ready"
                                    prop.style [ style.width (length.percent progress.ReadyPct) ]
                                ]
                                Html.div [
                                    prop.className "progress-segment progress-attention"
                                    prop.style [ style.width (length.percent progress.AttentionPct) ]
                                ]
                                Html.div [
                                    prop.className "progress-segment progress-skipped"
                                    prop.style [ style.width (length.percent progress.SkippedPct) ]
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "action-bar-buttons"
                            prop.children [
                                Html.button [
                                    prop.className "btn-cancel"
                                    prop.text "Abbrechen"
                                    prop.onClick (fun _ -> dispatch CancelSync)
                                ]
                                Html.button [
                                    prop.className (if counts.NeedCategory = 0 then "btn-import ready" else "btn-import")
                                    prop.disabled (counts.ToImport = 0)
                                    prop.onClick (fun _ -> dispatch ImportToYnab)
                                    prop.children [
                                        Html.span [ prop.text "Importieren" ]
                                        Html.span [ prop.className "btn-import-icon"; prop.text "\u2191" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]

                // === CATEGORY BOTTOM SHEET ===
                let sheetOpen = categorySheetState.IsSome
                let sheetPayee =
                    categorySheetState
                    |> Option.map (fun s -> s.PayeeName)
                    |> Option.defaultValue ""

                let suggestedCats : string list = []
                let recentCats =
                    model.RecentlyUsedCategoryIds
                    |> List.map (fun (YnabCategoryId guid) -> guid.ToString())

                // Picker options carry each category's current Available (YNAB
                // balance) so it shows right-aligned and colour-coded per row.
                let pickerCategoryOptions : BottomSheet.CategoryPickerOption list =
                    model.Categories
                    |> List.map (fun cat ->
                        let (YnabCategoryId id) = cat.Id
                        { Id = id.ToString()
                          Label = $"{cat.GroupName}: {cat.Name}"
                          Available = cat.Available })

                // Key forces remount on new transaction → resets search text (MVU-friendly)
                let pickerKey =
                    categorySheetState
                    |> Option.map (fun s -> let (TransactionId id) = s.TxId in id)
                    |> Option.defaultValue ""

                Html.div [
                    prop.key pickerKey
                    prop.children [
                        BottomSheet.categoryPicker
                            sheetOpen
                            sheetPayee
                            pickerCategoryOptions
                            suggestedCats
                            recentCats
                            (fun catId ->
                                match categorySheetState with
                                | Some state ->
                                    match System.Guid.TryParse(catId) with
                                    | true, guid -> dispatch (CategorizeTransaction(state.TxId, Some (YnabCategoryId guid)))
                                    | false, _ -> ()
                                | None -> ()
                                setCategorySheetState None)
                            (fun () -> setCategorySheetState None)
                    ]
                ]

            | NotAsked ->
                Card.emptyState
                    (Icons.creditCard Icons.XL Icons.Default)
                    "Keine Transaktionen geladen"
                    "Starte eine Synchronisierung, um Transaktionen von deiner Bank abzurufen."
                    None
            | Loading ->
                Loading.txListSkeleton 6
            | Failure error ->
                ErrorDisplay.cardCompact error None
        ]
    ]
