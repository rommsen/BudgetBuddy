/// Der Reduzierer der Kassenbuch-Seite (ledger-q5w9c, seitenweise Liste und
/// Kontostaende-Leiste ledger-b1xwe).
///
/// Alle Zustandsaenderungen laufen durch `update`, alle Seiteneffekte durch `Cmd`.
/// Die Seite blockiert nie hinter dem Spiegel: waehrend eines Laufs bleibt die
/// vorhandene Liste stehen und wird erst danach neu geladen (ADR 0014).
module Components.Ledger.State

open System
open Elmish
open Shared.Domain
open Components.Ledger.Types
open Types
open Client

module DateField = Client.DesignSystem.DateField

// ============================================
// Gemerkte Konto-Wahl (ledger-ybmqc)
// ============================================

/// Der Schlüssel im Gerätespeicher (`localStorage`).
let accountChoiceStorageKey = "ledger.selectedAccountFilter"

/// Der Speicherwert einer Sicht: "all" oder "account:<Id>".
let encodeAccountFilter (filter: AccountFilter) : string =
    match filter with
    | AllAccounts -> "all"
    | OneAccount id -> $"account:{id}"

/// Liest einen Speicherwert zurück; Unbekanntes und Kaputtes ergibt `None`.
let decodeAccountFilter (value: string) : AccountFilter option =
    if isNull value then None
    elif value = "all" then Some AllAccounts
    elif value.StartsWith "account:" then
        match Guid.TryParse(value.Substring "account:".Length) with
        | true, id -> Some (OneAccount id)
        | _ -> None
    else None

/// Die Sicht beim Öffnen: die laufende Wahl, sonst die gemerkte (nur wenn das Konto noch
/// existiert), sonst das YNAB-Standardkonto vom Server.
let resolveInitialFilter
    (current: AccountFilter option)
    (remembered: AccountFilter option)
    (accounts: LedgerAccountsView)
    : AccountFilter option =
    let exists id = accounts.Accounts |> List.exists (fun a -> a.Id = id)
    let usable choice =
        match choice with
        | Some AllAccounts -> Some AllAccounts
        | Some (OneAccount id) when exists id -> Some (OneAccount id)
        | _ -> None
    match usable current with
    | Some f -> Some f
    | None ->
        match usable remembered with
        | Some f -> Some f
        | None -> accounts.DefaultAccountId |> Option.map OneAccount

/// Liest die gemerkte Sicht; ohne Speicher (privater Modus, .NET-Tests) `None`.
let private readRememberedFilter () : AccountFilter option =
    try
        decodeAccountFilter (Browser.Dom.window.localStorage.getItem accountChoiceStorageKey)
    with _ -> None

/// Schreibt die Wahl in den Gerätespeicher; schlägt der Speicher fehl, gilt der heutige Weg.
let private writeRememberedFilter (filter: AccountFilter) : unit =
    try
        Browser.Dom.window.localStorage.setItem (accountChoiceStorageKey, encodeAccountFilter filter)
    with _ -> ()

/// Ein frisches, leeres Kassenbuch. Geladen wird beim Betreten der Seite
/// (`UrlChanged` im Haupt-State), nicht beim Start der App: das Kassenbuch soll
/// keine Abfrage ausloesen, solange niemand hineinschaut.
let init () : Model * Cmd<Msg> =
    let model = {
        Accounts = NotAsked
        Balances = NotAsked
        SelectedFilter = None
        RememberedFilter = readRememberedFilter ()
        AccountSheetOpen = false
        Entries = NotAsked
        NextPage = 0
        LocallyRemoved = 0
        TotalCount = 0
        TotalAmount = { Amount = 0m; Currency = "EUR" }
        HasMoreEntries = false
        LoadingMore = false
        IncludeDeleted = false
        DeletedCount = 0
        OnlyNeedsReview = false
        Sort = LedgerSort.defaultSort
        SortSheetOpen = false
        NeedsReviewCount = 0
        DetailFor = None
        DetailForm = DetailSheet
        MemoEdit = None
        MemoRows = Map.empty
        ListInteraction = TableRows
        History = NotAsked
        LastMirrorRun = NotAsked
        MirrorRunning = false
        Categories = NotAsked
        CategoriesLoadedAt = None
        CategoriesRefreshing = false
        CategoriesAwaitSettings = false
        Filters = noFilters
        OpenPopover = None
        CategoryDraft = Set.empty
        CategorySearch = ""
        HiddenCategoriesExpanded = false
        RecentCategories = readRecentCategories ()
        DateDraft = emptyDateRange
        DateDraftFromInvalid = false
        DateDraftToInvalid = false
        PayeeSuggestions = NotAsked
        PayeeSearch = ""
        MemoDraft = ""
        MemoVersion = 0
        AmountMinDraft = ""
        AmountMaxDraft = ""
        AmountVersion = 0
        ReviewMarkRows = Map.empty
        PushRetryRows = Map.empty
        SwipedAway = Map.empty
        Selection = NoSelection
        SelectionAnchor = None
        SelectionMode = false
        BatchCategoryPickerOpen = false
        CategoryBatchRunning = false
        FocusedRow = None
        ReviewMarkBatchRunning = false
        LenaBatchRunning = false
        CategoryPickerFor = None
        SplitEditor = None
        QuickAddAccountId = None
        TransferTargets = NotAsked
        CategorySuggestions = []
        CategoryRows = Map.empty
        FlagRows = Map.empty
        LenaFlagColor = None
        AskLenaCount = 0
        CategoryCard = None
        CategoryCardVersion = 0
        FilteredOut = Set.empty
        OrderStale = false
    }
    model, Cmd.none

/// Der Zustand einer frisch angestossenen Buchungsliste: Seite 0, nichts geladen.
/// Von `LoadEntries` und `SelectFilter` geteilt: beide starten die Liste neu. Eine neue
/// Liste ist ein neuer Treffer: die Auswahl bezog sich auf den alten und endet mit ihm
/// (Konto-, Filter- und Chipwechsel laufen alle hierdurch, ledger-r1d2f).
let private resetEntries (model: Model) : Model = {
    model with
        Entries = Loading
        NextPage = 0
        LocallyRemoved = 0
        TotalCount = 0
        TotalAmount = { model.TotalAmount with Amount = 0m }
        HasMoreEntries = false
        LoadingMore = false
        SwipedAway = Map.empty
        Selection = NoSelection
        SelectionAnchor = None
        SelectionMode = false
        // Eine neu aufgesetzte Liste kennt weder ausgegraute Zeilen noch eine veraltete
        // Reihenfolge (ledger-sfab0).
        FilteredOut = Set.empty
        OrderStale = false
}

/// Die Startrichtung beim Wechsel auf ein anderes Sortierfeld (ADR 0051 Punkt 9): Datum
/// absteigend (neueste zuerst), Betrag aufsteigend (kleinster Wert zuerst, also die größte
/// Ausgabe oben), Payee, Kategorie und Konto aufsteigend.
let sortStartDirection (field: LedgerSortField) : LedgerSortDirection =
    match field with
    | SortByDate -> SortDesc
    | SortByAmount
    | SortByPayee
    | SortByCategory
    | SortByAccount -> SortAsc

/// Die Sortierung nach einem Klick auf den Kopf `field`: das aktive Feld dreht die
/// Richtung, ein anderes beginnt mit seiner Startrichtung.
let nextSort (current: LedgerSort) (field: LedgerSortField) : LedgerSort =
    if current.Field = field then
        { current with Direction = (match current.Direction with SortAsc -> SortDesc | SortDesc -> SortAsc) }
    else
        { Field = field; Direction = sortStartDirection field }

/// Die Zeilen des Sortier-Sheets am Handy (ledger-skpcx): je Feld zwei Richtungen in fester
/// Reihenfolge, Konto nur in der Gesamtsicht.
let sortOptions (showAccount: bool) : (LedgerSort * string) list =
    let option field direction label = { Field = field; Direction = direction }, label
    [ option SortByDate SortDesc "Neueste zuerst"
      option SortByDate SortAsc "Älteste zuerst"
      option SortByAmount SortAsc "Größte Ausgabe zuerst"
      option SortByAmount SortDesc "Größte Einnahme zuerst"
      option SortByPayee SortAsc "A bis Z"
      option SortByPayee SortDesc "Z bis A"
      option SortByCategory SortAsc "A bis Z"
      option SortByCategory SortDesc "Z bis A"
      if showAccount then
          option SortByAccount SortAsc "A bis Z"
          option SortByAccount SortDesc "Z bis A" ]

/// Der Gruppenkopf einer Sortierung im Sheet.
let sortFieldName (field: LedgerSortField) : string =
    match field with
    | SortByDate -> "Datum"
    | SortByAmount -> "Betrag"
    | SortByPayee -> "Payee"
    | SortByCategory -> "Kategorie"
    | SortByAccount -> "Konto"

/// Der Text des Sortier-Chips: Feldname und Pfeil ("Datum ↓" absteigend, "Betrag ↑" aufsteigend).
let sortChipLabel (sort: LedgerSort) : string =
    let arrow = match sort.Direction with SortAsc -> "↑" | SortDesc -> "↓"
    $"{sortFieldName sort.Field} {arrow}"

/// Die wirksame Sortierung (ADR 0051 Punkt 9): die Spalte "Konto" gibt es nur in der
/// Gesamtsicht. Ist `SortByAccount` gewählt und ein einzelnes Konto aktiv, gilt
/// `LedgerSort.defaultSort`; das Modell behält die Wahl für die Rückkehr in die Gesamtsicht.
let effectiveSort (model: Model) : LedgerSort =
    match model.Sort.Field, model.SelectedFilter with
    | SortByAccount, Some (OneAccount _) -> LedgerSort.defaultSort
    | _ -> model.Sort

/// Die Seitenanfrage an den Server. Rein, damit der Versatz testbar ist. Die fünf
/// Such-Filter (Kategorie/Zeitraum/Payee/Betrag/Memo, ledger-vgfw3) sind Server-seitig
/// da (ADR 0036). `LocallyRemoved` stammt aus dem Modell: jede Aktion, die Zeilen lokal
/// aus der Liste nimmt, zählt dort hoch, und die Anfrage gleicht den Versatz damit aus
/// (ledger-be1yh), sonst fehlt die Buchung an der Seitengrenze.
let buildPageQuery (model: Model) (accountId: Guid option) (page: int) : LedgerEntryPageQuery = {
    AccountId = accountId
    Page = page
    PageSize = pageSize
    LocallyRemoved = model.LocallyRemoved
    IncludeDeleted = model.IncludeDeleted
    OnlyNeedsReview = model.OnlyNeedsReview
    OnlyAskLena = model.Filters.OnlyAskLena
    WithoutCategory = model.Filters.WithoutCategory
    CategoryIds = model.Filters.CategoryIds |> Set.toList
    DateFrom = model.Filters.DateRange |> Option.bind (fun d -> d.From)
    DateTo = model.Filters.DateRange |> Option.bind (fun d -> d.To)
    Payee = model.Filters.Payee
    Memo = model.Filters.Memo
    AmountMin = model.Filters.AmountMin
    AmountMax = model.Filters.AmountMax
    Sort = effectiveSort model
}

/// Trägt das Ergebnis eines Prüfvermerk-Befehls in die geladene Liste ein (ledger-r777c):
/// setzt `NeedsReview` (wenn `needsReview` gesetzt ist) und den offenen Fehlschlag der
/// Zeile, passt den Zähler "Zu prüfen (n)" an und nimmt die Zeile aus der Liste, wenn der
/// Chip "Zu prüfen" aktiv ist und sie nicht mehr zu prüfen ist. Der Zeilenzustand
/// (Laden/Fehler) wird dabei zurückgesetzt. Rein, ohne Proxy.
let private applyReviewMarkResult
    (entryId: Guid)
    (needsReview: bool option)
    (failureOf: LedgerEntryView -> string option)
    (model: Model)
    : Model =
    let cleared = { model with ReviewMarkRows = model.ReviewMarkRows |> Map.remove entryId }

    match cleared.Entries with
    | Success entries ->
        match entries |> List.tryFind (fun e -> e.Id = entryId) with
        | None -> cleared
        | Some entry ->
            let newNeedsReview = needsReview |> Option.defaultValue entry.NeedsReview
            let updated = { entry with NeedsReview = newNeedsReview; ReviewMarkWriteBackFailure = failureOf entry }
            // Das offene Detail zeigt dieselbe Buchung: eine Zeile, die die Liste verlässt
            // (Chip "Zu prüfen"), liest es sonst nur noch aus der Momentaufnahme.
            let cleared =
                match cleared.DetailFor with
                | Some detail when detail.Id = entryId -> { cleared with DetailFor = Some updated }
                | _ -> cleared
            let countDelta =
                match entry.NeedsReview, newNeedsReview with
                | true, false -> -1
                | false, true -> 1
                | _ -> 0
            // Eine Zeile mit offenem Fehlschlag bleibt stehen, auch wenn sie nicht mehr zu
            // prüfen ist: sie trägt den Hinweis "Prüfvermerk nicht in YNAB". Der Zähler
            // folgt trotzdem dem BB-Stand (ADR 0050, Punkt 11).
            let leavesList = cleared.OnlyNeedsReview && not newNeedsReview && updated.ReviewMarkWriteBackFailure.IsNone
            let remaining =
                if leavesList then entries |> List.filter (fun e -> e.Id <> entryId)
                else entries |> List.map (fun e -> if e.Id = entryId then updated else e)
            let totalCount = if leavesList then max 0 (cleared.TotalCount - 1) else cleared.TotalCount
            let totalAmount =
                if leavesList && not entry.DeletedInYnab then
                    { cleared.TotalAmount with Amount = cleared.TotalAmount.Amount - entry.Amount.Amount }
                else
                    cleared.TotalAmount
            { cleared with
                Entries = Success remaining
                NeedsReviewCount = max 0 (cleared.NeedsReviewCount + countDelta)
                TotalCount = totalCount
                TotalAmount = totalAmount
                LocallyRemoved = (if leavesList then cleared.LocallyRemoved + 1 else cleared.LocallyRemoved)
                // Eine Zeile, die die Liste verlässt, verlässt auch die Auswahl (ledger-r1d2f).
                Selection = (if leavesList then removeFromSelection entryId cleared.Selection else cleared.Selection)
                SelectionAnchor = (if leavesList && cleared.SelectionAnchor = Some entryId then None else cleared.SelectionAnchor)
                // Liegt der Fokus auf der Zeile, die geht, rückt er auf die nach (ledger-rjb8w).
                FocusedRow = (if leavesList then focusAfterRemoval entries entryId cleared.FocusedRow else cleared.FocusedRow)
                HasMoreEntries = List.length remaining < totalCount }
    | _ -> cleared

/// Trägt das Ergebnis von "Erneut senden" am Hinweis "Noch nicht in YNAB" in die Zeile und
/// das offene Detail ein (ledger-cks06): Erfolg verknüpft die Buchung und löscht den
/// Fehlschlag, ein neuer Fehlschlag ersetzt Grund und Zeitpunkt. Der Sende-Zustand wird
/// zurückgesetzt. Rein, ohne Proxy; `now` kommt von außen.
let applyPushRetryResult (entryId: Guid) (now: DateTime) (result: PushToYnabWriteBack) (model: Model) : Model =
    let update (entry: LedgerEntryView) =
        match result with
        | PushToYnabSent -> { entry with LinkedToYnab = true; PushToYnabFailure = None }
        | PushToYnabFailed reason ->
            { entry with PushToYnabFailure = Some { Reason = reason; FailedAt = now } }
    let cleared = { model with PushRetryRows = model.PushRetryRows |> Map.remove entryId }
    { cleared with
        Entries =
            (match cleared.Entries with
             | Success entries -> Success (entries |> List.map (fun e -> if e.Id = entryId then update e else e))
             | other -> other)
        DetailFor =
            (match cleared.DetailFor with
             | Some detail when detail.Id = entryId -> Some (update detail)
             | other -> other) }

/// Der offene Fehlschlag der Zeile nach einem Prüfvermerk-Befehl: der Grund aus der
/// Antwort, `None` nach erfolgreichem Versand; ohne Änderung und ohne Versand bleibt ein
/// bisheriger Fehlschlag stehen.
let private writeBackFailureOf (outcome: ReviewMarkOutcome) (entry: LedgerEntryView) : string option =
    match outcome.WriteBack with
    | ReviewMarkSendFailed reason -> Some reason
    | ReviewMarkSent -> None
    | ReviewMarkNotSent -> if outcome.Change = ReviewMarkUnchanged then entry.ReviewMarkWriteBackFailure else None

/// Ergebnis der Wisch-Geste (ledger-th36h): trägt den Prüfvermerk wie die Schaltfläche
/// ein und merkt sich eine Zeile, die dadurch die Liste verlässt, samt Platz, damit
/// "Rückgängig" sie zurückholen kann. Rein, ohne Proxy.
let private applySwipeReviewed (entryId: Guid) (outcome: ReviewMarkOutcome) (model: Model) : Model =
    let before =
        match model.Entries with
        | Success entries ->
            entries |> List.tryFindIndex (fun e -> e.Id = entryId) |> Option.map (fun i -> i, List.item i entries)
        | _ -> None
    let applied = applyReviewMarkResult entryId (Some false) (writeBackFailureOf outcome) model
    match before, applied.Entries with
    | Some (index, entry), Success remaining when not (remaining |> List.exists (fun e -> e.Id = entryId)) ->
        { applied with SwipedAway = applied.SwipedAway |> Map.add entryId (entry, index) }
    | _ -> applied

/// Setzt eine von der Geste entfernte Zeile an ihren Platz zurück und rechnet Anzahl,
/// Summe und Zähler "Zu prüfen" nach. Ohne gemerkte Zeile (die Liste wurde neu geladen)
/// bleibt das Modell, wie es ist. Rein.
let private reinsertSwipedAway (entryId: Guid) (model: Model) : Model =
    match model.SwipedAway |> Map.tryFind entryId, model.Entries with
    | Some (entry, index), Success entries when not (entries |> List.exists (fun e -> e.Id = entryId)) ->
        let at = min index (List.length entries)
        let before, after = List.splitAt at entries
        let restored = before @ [ entry ] @ after
        let totalCount = model.TotalCount + 1
        // Die Zeile ist wieder in der Liste: der Seitenversatz darf sie nicht mehr
        // abziehen, sonst lädt die nächste Seite diese Buchung doppelt (ledger-be1yh).
        { model with
            LocallyRemoved = max 0 (model.LocallyRemoved - 1)
            Entries = Success restored
            SwipedAway = model.SwipedAway |> Map.remove entryId
            TotalCount = totalCount
            TotalAmount =
                if entry.DeletedInYnab then model.TotalAmount
                else { model.TotalAmount with Amount = model.TotalAmount.Amount + entry.Amount.Amount }
            NeedsReviewCount = model.NeedsReviewCount + (if entry.NeedsReview then 1 else 0)
            HasMoreEntries = List.length restored < totalCount }
    | _ -> model

/// Ergebnis von "Rückgängig": holt die Zeile zurück, falls die Geste sie aus der Liste
/// nahm, und setzt dann den Vorzustand ein, den der Client mitgeschickt hat. Der Zähler
/// "Zu prüfen" folgt dem BB-Stand. Rein.
let private applyUndoReviewMark
    (entryId: Guid)
    (previous: ReviewMarkState)
    (outcome: ReviewMarkOutcome)
    (model: Model)
    : Model =
    let reinserted = reinsertSwipedAway entryId model
    applyReviewMarkResult entryId (Some (needsReviewOf previous)) (writeBackFailureOf outcome) reinserted

// ============================================
// Kategorie ändern im Detail (ledger-sfab0, ADR 0055)
// ============================================

/// Die Buchung aus der geladenen Liste, sonst die Momentaufnahme des offenen Details.
let private findEntry (model: Model) (entryId: Guid) : LedgerEntryView option =
    let loaded =
        match model.Entries with
        | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId)
        | _ -> None
    loaded |> Option.orElse (model.DetailFor |> Option.filter (fun d -> d.Id = entryId))

let private categoryOfId (model: Model) (id: YnabCategoryId option) : YnabCategory option =
    match model.Categories, id with
    | Success categories, Some wanted -> categories |> List.tryFind (fun c -> c.Id = wanted)
    | _ -> None

/// Der offene Fehlschlag der Buchung nach einem Kategorie-Befehl: der Grund aus der Antwort,
/// `None` nach erfolgreichem Versand; ohne Änderung und ohne Versand bleibt ein bisheriger
/// Fehlschlag stehen.
let private categoryFailureOf (outcome: CategoryChangeOutcome) (entry: LedgerEntryView) : string option =
    match outcome.WriteBack with
    | CategorySendFailed reason -> Some reason
    | CategorySent -> None
    | CategoryNotSent -> if outcome.Change = CategoryUnchanged then entry.CategorizationWriteBackFailure else None

/// Ersetzt die Buchung in der geladenen Liste und im offenen Detail.
let private replaceEntry (updated: LedgerEntryView) (model: Model) : Model =
    let entries =
        match model.Entries with
        | Success list -> Success (list |> List.map (fun e -> if e.Id = updated.Id then updated else e))
        | other -> other
    let detail =
        match model.DetailFor with
        | Some d when d.Id = updated.Id -> Some updated
        | other -> other
    { model with Entries = entries; DetailFor = detail }

/// Fällt die geänderte Zeile aus dem aktiven Filter, bleibt sie ausgegraut stehen und zählt
/// für den Seitenversatz wie eine entfernte Zeile; Trefferzahl und Summe gehen sofort mit.
/// Macht eine Rücknahme sie wieder zum Treffer, kehrt alles um. Rein.
let private applyFilterEffect (updated: LedgerEntryView) (model: Model) : Model =
    let matches = rowMatchesFilters model.Filters updated
    let wasOut = model.FilteredOut.Contains updated.Id
    let amount = if updated.DeletedInYnab then 0m else updated.Amount.Amount
    let loadedCount (m: Model) =
        (match m.Entries with Success list -> List.length list | _ -> 0) - Set.count m.FilteredOut
    match matches, wasOut with
    | false, false ->
        let m =
            { model with
                FilteredOut = Set.add updated.Id model.FilteredOut
                LocallyRemoved = model.LocallyRemoved + 1
                TotalCount = max 0 (model.TotalCount - 1)
                TotalAmount = { model.TotalAmount with Amount = model.TotalAmount.Amount - amount } }
        { m with HasMoreEntries = loadedCount m < m.TotalCount }
    | true, true ->
        let m =
            { model with
                FilteredOut = Set.remove updated.Id model.FilteredOut
                LocallyRemoved = max 0 (model.LocallyRemoved - 1)
                TotalCount = model.TotalCount + 1
                TotalAmount = { model.TotalAmount with Amount = model.TotalAmount.Amount + amount } }
        { m with HasMoreEntries = loadedCount m < m.TotalCount }
    | _ -> model

/// Trägt das Ergebnis von `setEntryCategory` in den Zustand ein. `newId` ist die gewählte
/// Kategorie, `fallbackName` der Name, falls die Kategorienliste sie nicht kennt (Rücknahme).
/// `showCard`: die Karte "Budget nach dem Wechsel" zeigen (nicht bei der Rücknahme). Das
/// zweite Ergebnis sind die Folgebefehle (Ablauf der Karte, Zähler neu laden). Rein.
let private applyCategoryResult
    (entryId: Guid)
    (newId: YnabCategoryId option)
    (fallbackName: string option)
    (showCard: bool)
    (outcome: CategoryChangeOutcome)
    (model: Model)
    : Model * Cmd<Msg> =
    let cleared =
        { model with
            CategoryRows = model.CategoryRows |> Map.remove entryId
            CategoryPickerFor = None
            CategorySuggestions = [] }
    match findEntry cleared entryId with
    | None -> cleared, Cmd.none
    | Some entry when outcome.Change = CategoryUnchanged ->
        replaceEntry { entry with CategorizationWriteBackFailure = categoryFailureOf outcome entry } cleared, Cmd.none
    | Some entry ->
        let oldId = entry.CategoryId |> Option.map YnabCategoryId
        let category = categoryOfId cleared newId
        let updated =
            { entry with
                CategoryId = newId |> Option.map (fun (YnabCategoryId g) -> g)
                CategoryName = category |> Option.map (fun c -> c.Name) |> Option.orElse (newId |> Option.bind (fun _ -> fallbackName))
                CategoryGroupName = category |> Option.map (fun c -> c.GroupName)
                CategorizationWriteBackFailure = categoryFailureOf outcome entry }
        let today = DateTime.Today
        let args = (entry.Date, entry.Amount.Amount, oldId, newId, today)
        let lines =
            match cleared.Categories with
            | Success categories when showCard -> categoryChangeHint args categories
            | _ -> []
        let categories =
            match cleared.Categories with
            | Success list -> Success (shiftAvailable args list)
            | other -> other
        let version = cleared.CategoryCardVersion + 1
        let hasCard = showCard && not (List.isEmpty lines)
        let withEntry = replaceEntry updated { cleared with Categories = categories }
        let withCard =
            { withEntry with
                CategoryCard = (if hasCard then Some { EntryId = entryId; Lines = lines; Version = version } else None)
                CategoryCardVersion = (if hasCard then version else cleared.CategoryCardVersion) }
        let filtered = applyFilterEffect updated withCard
        // Ändert sich der Schlüssel der aktiven Sortierung, sortiert die Zeile um: die Liste
        // bleibt stehen und bietet "Neu laden" an (ADR 0055, Punkt 9).
        let orderChanged = (effectiveSort filtered).Field = SortByCategory && oldId <> newId
        let finalModel = if orderChanged then { filtered with OrderStale = true } else filtered
        let timer =
            if hasCard then
                Cmd.OfAsync.perform
                    (fun () -> async { do! Async.Sleep categoryCardDurationMs })
                    ()
                    (fun () -> CategoryCardExpired version)
            else
                Cmd.none
        let refresh = if orderChanged then Cmd.ofMsg RunRefreshTotals else Cmd.none
        finalModel, Cmd.batch [ timer; refresh ]

/// Der offene Fehlschlag der Buchung nach einem Flag-Befehl: der Grund aus der Antwort,
/// `None` nach erfolgreichem Versand; ohne Änderung und ohne Versand bleibt ein bisheriger
/// Fehlschlag stehen.
let private flagFailureOf (outcome: FlagOutcome) (entry: LedgerEntryView) : string option =
    match outcome.WriteBack with
    | FlagSendFailed reason -> Some reason
    | FlagSent -> None
    | FlagNotSent -> if outcome.Change = FlagUnchanged then entry.FlagWriteBackFailure else None

/// Trägt das Ergebnis von `setLenaFlag` und `restoreFlag` in den Zustand ein (ADR 0056).
/// `newState` ist das Flag, das der Befehl gesetzt hat. Die Zeile bleibt an ihrem Platz; passt
/// sie bei aktivem Chip "Rückfrage Lena" nicht mehr zum Filter, bleibt sie ausgegraut stehen
/// (`applyFilterEffect`, wie bei der Kategorie), erst das nächste Laden nimmt sie heraus. Der
/// Zähler "Rückfrage Lena (n)" folgt dem BB-Stand. Rein, ohne Proxy.
let private applyFlagResult (entryId: Guid) (newState: FlagState) (outcome: FlagOutcome) (model: Model) : Model =
    let cleared = { model with FlagRows = model.FlagRows |> Map.remove entryId }
    match findEntry cleared entryId with
    | None -> cleared
    | Some entry when outcome.Change = FlagUnchanged ->
        replaceEntry { entry with FlagWriteBackFailure = flagFailureOf outcome entry } cleared
    | Some entry ->
        let updated =
            { entry with
                Flag = flagOfState newState
                FlagWriteBackFailure = flagFailureOf outcome entry }
        let isLena (e: LedgerEntryView) = if e.Flag = AskLena then 1 else 0
        let withEntry =
            replaceEntry
                updated
                { cleared with AskLenaCount = max 0 (cleared.AskLenaCount + isLena updated - isLena entry) }
        applyFilterEffect updated withEntry

/// Der offene Fehlschlag der Buchung nach einem Memo-Befehl: der Grund aus der Antwort,
/// `None` nach erfolgreichem Versand; ohne Änderung und ohne Versand bleibt ein bisheriger
/// Fehlschlag stehen.
let private memoFailureOf (outcome: MemoOutcome) (entry: LedgerEntryView) : string option =
    match outcome.MemoWriteBack with
    | MemoSendFailed reason -> Some reason
    | MemoSent -> None
    | MemoNotSent -> if outcome.MemoChange = MemoUnchanged then entry.MemoWriteBackFailure else None

/// Trägt das Ergebnis von `setEntryMemo` in den Zustand ein (ADR 0062): die Zeile bekommt das
/// gespeicherte Memo und den Zustand des Zurückschreibens, das Eingabefeld dieser Buchung
/// schließt. Rein, ohne Proxy.
let private applyMemoResult (entryId: Guid) (outcome: MemoOutcome) (model: Model) : Model =
    let cleared =
        { model with
            MemoRows = model.MemoRows |> Map.remove entryId
            MemoEdit = model.MemoEdit |> Option.filter (fun edit -> edit.MemoEntryId <> entryId) }
    match findEntry cleared entryId with
    | None -> cleared
    | Some entry when outcome.MemoChange = MemoUnchanged ->
        replaceEntry { entry with MemoWriteBackFailure = memoFailureOf outcome entry } cleared
    | Some entry ->
        replaceEntry
            { entry with
                Memo = outcome.CurrentMemo
                MemoWriteBackFailure = memoFailureOf outcome entry }
            cleared

/// Holt die Historie der offenen Buchung neu, damit die Zeile der Memo-Änderung dort steht.
let private reloadHistoryFor (entryId: Guid) (model: Model) : Cmd<Msg> =
    if model.DetailFor |> Option.exists (fun d -> d.Id = entryId) then Cmd.ofMsg (LoadHistory entryId)
    else Cmd.none

/// Trägt das Ergebnis von `setEntrySplits` in den Zustand ein (ledger-pafkp). `lines` sind die
/// geschickten Zeilen: eine Zeile hebt die Aufteilung auf (die Buchung trägt dann diese
/// Kategorie), mehrere teilen auf oder ersetzen die Zeilen. Die Zeile bleibt an ihrem Platz und
/// ungeprüft, sie wird über `LedgerEntryView.Id` ersetzt. Filter und Sortierung verhalten sich
/// wie bei der Kategorieänderung. Scheitert der Versand nach YNAB, holt der Folgebefehl die
/// Buchung neu (sichtbar wird so eine offene Neuanlage). Rein.
let private applySplitResult
    (entryId: Guid)
    (lines: TransactionSplit list)
    (outcome: CategoryChangeOutcome)
    (model: Model)
    : Model * Cmd<Msg> =
    let cleared =
        { model with
            CategoryRows = model.CategoryRows |> Map.remove entryId
            SplitEditor =
                (match model.SplitEditor with
                 | Some editor when editor.EntryId = entryId -> None
                 | other -> other) }
    match findEntry cleared entryId with
    | None -> cleared, Cmd.none
    | Some entry ->
        let refresh =
            match outcome.WriteBack with
            | CategorySendFailed _ -> Cmd.ofMsg (RunRefreshEntry entryId)
            | _ -> Cmd.none
        let failure = categoryFailureOf outcome entry
        // Erfolg beendet eine offene Neuanlage; bei einem Fehlschlag sagt erst der Abruf, was offen ist.
        let locks =
            match outcome.WriteBack with
            | CategorySent -> entry.SplitLocks |> Option.map (fun l -> { l with ReplacementPending = false })
            | _ -> entry.SplitLocks
        if outcome.Change = CategoryUnchanged then
            replaceEntry { entry with CategorizationWriteBackFailure = failure; SplitLocks = locks } cleared, refresh
        else
            let liveName (id: YnabCategoryId) (name: string) =
                categoryOfId cleared (Some id) |> Option.map (fun c -> c.Name) |> Option.defaultValue name
            let groupOf (id: YnabCategoryId) = categoryOfId cleared (Some id) |> Option.map (fun c -> c.GroupName)
            let updated =
                match lines with
                | [ single ] ->
                    let category =
                        match single.Target with
                        | ToCategory (id, name) -> Some (id, liveName id name)
                        | _ -> None
                    { entry with
                        Splits = []
                        SplitCategoryGroupNames = Map.empty
                        CategoryId = category |> Option.map (fun (YnabCategoryId g, _) -> g)
                        CategoryName = category |> Option.map snd
                        CategoryGroupName = category |> Option.bind (fun (id, _) -> groupOf id)
                        CategorizationWriteBackFailure = failure
                        SplitLocks = locks }
                | many ->
                    let renamed =
                        many
                        |> List.map (fun s ->
                            match s.Target with
                            | ToCategory (id, name) -> { s with Target = ToCategory (id, liveName id name) }
                            | _ -> s)
                    let groups =
                        renamed
                        |> List.choose (fun s ->
                            match s.Target with
                            | ToCategory (id, _) -> groupOf id |> Option.map (fun g -> (let (YnabCategoryId guid) = id in guid), g)
                            | _ -> None)
                        |> Map.ofList
                    { entry with
                        Splits = renamed
                        SplitCategoryGroupNames = groups
                        CategoryId = None
                        CategoryName = None
                        CategoryGroupName = None
                        CategorizationWriteBackFailure = failure
                        SplitLocks = locks }
            let withEntry = replaceEntry updated cleared
            let filtered = applyFilterEffect updated withEntry
            // Ändert sich der Schlüssel der aktiven Sortierung, sortiert die Zeile um (ADR 0055 Punkt 9).
            let keyChanged =
                entry.CategoryId <> updated.CategoryId || List.isEmpty entry.Splits <> List.isEmpty updated.Splits
            let orderChanged = (effectiveSort filtered).Field = SortByCategory && keyChanged
            let finalModel = if orderChanged then { filtered with OrderStale = true } else filtered
            finalModel, Cmd.batch [ refresh; (if orderChanged then Cmd.ofMsg RunRefreshTotals else Cmd.none) ]

/// Ist für diese Buchung ein Aufruf des Split-Wegs unterwegs?
let private splitBusy (model: Model) (entryId: Guid) : bool =
    (model.CategoryRows |> Map.tryFind entryId) = Some CategorySending

/// Ändert den Entwurf des offenen Editors.
let private editSplit (f: SplitEditor.SplitEditorState -> SplitEditor.SplitEditorState) (model: Model) : Model =
    match model.SplitEditor with
    | Some editor when not (splitBusy model editor.EntryId) -> { model with SplitEditor = Some (f editor) }
    | _ -> model

/// Der Name eines Übertrag-Ziels aus der Kontenliste.
let private accountNameOf (model: Model) (id: YnabAccountId) : string option =
    match model.TransferTargets with
    | Success accounts -> accounts |> List.tryFind (fun a -> a.Id = id) |> Option.map (fun a -> a.Name)
    | _ -> None

/// Die Auswahl als Stapel-Auftrag an den Server (ledger-nhwrt). `Picked`: die Ids der
/// gewählten Zeilen. `AllExcept`: der ganze Treffer als Seitenanfrage (derselbe Filter wie
/// die Liste, Seite 0, ohne lokalen Versatz) plus die abgewählten Ids; der Server löst ihn
/// auf, weil der Client die Ids nicht geladener Buchungen nicht kennt. `None` ohne Auswahl.
let reviewMarkBatchSelection (model: Model) : ReviewMarkBatchSelection option =
    let accountId =
        match model.SelectedFilter with
        | Some (OneAccount id) -> Some id
        | _ -> None
    match model.Selection with
    | NoSelection -> None
    | Picked ids when Set.isEmpty ids -> None
    | Picked ids -> Some (BatchPicked (Set.toList ids))
    | AllExcept excluded ->
        let query = { buildPageQuery model accountId 0 with LocallyRemoved = 0 }
        Some (BatchAllExcept (query, Set.toList excluded))

/// Trägt das Ergebnis von "Geprüft" für die Auswahl in die geladene Liste ein: die Auswahl
/// endet (samt Auswahl-Modus), jede geänderte geladene Zeile ist nicht mehr zu prüfen und
/// trägt ihren Fehlschlag, der Zähler "Zu prüfen" sinkt um die Zahl der geänderten
/// Buchungen. Bei aktivem Chip verlassen die Zeilen ohne Fehlschlag die Liste
/// (`LocallyRemoved` steigt um ihre Zahl, Treffer und Summe sinken). Das zweite Ergebnis
/// sagt, ob die Liste neu zu laden ist: bei aktivem Chip waren geänderte Buchungen nie
/// geladen (`AllExcept`), der Versatz der nächsten Seite stimmt dann nur nach dem Neuladen.
/// Rein, ohne Proxy.
let private applyReviewMarkBatch (outcome: ReviewMarkBatchOutcome) (model: Model) : Model * bool =
    let changedCount = List.length outcome.Changed
    let ended = {
        model with
            Selection = NoSelection
            SelectionAnchor = None
            SelectionMode = false
            ReviewMarkBatchRunning = false
            NeedsReviewCount = max 0 (model.NeedsReviewCount - changedCount)
    }
    match ended.Entries with
    | Success entries ->
        let changeById = outcome.Changed |> List.map (fun c -> c.EntryId, c) |> Map.ofList
        let failureOf (c: ReviewMarkBatchChange) =
            match c.WriteBack with
            | ReviewMarkSendFailed reason -> Some reason
            | _ -> None
        let updated =
            entries
            |> List.map (fun e ->
                match changeById |> Map.tryFind e.Id with
                | Some c -> { e with NeedsReview = false; ReviewMarkWriteBackFailure = failureOf c }
                | None -> e)
        let loadedChanged = entries |> List.filter (fun e -> changeById |> Map.containsKey e.Id) |> List.length
        let unloadedChanged = changedCount - loadedChanged
        let leaving =
            if ended.OnlyNeedsReview then
                updated |> List.filter (fun e -> changeById |> Map.containsKey e.Id && e.ReviewMarkWriteBackFailure.IsNone)
            else
                []
        let leavingIds = leaving |> List.map (fun e -> e.Id) |> Set.ofList
        let remaining = updated |> List.filter (fun e -> not (leavingIds.Contains e.Id))
        let leavingAmount = leaving |> List.filter (fun e -> not e.DeletedInYnab) |> List.sumBy (fun e -> e.Amount.Amount)
        let focus =
            leaving
            |> List.fold (fun (state: Guid option * LedgerEntryView list) e ->
                let focus', list = state
                focusAfterRemoval list e.Id focus', list |> List.filter (fun x -> x.Id <> e.Id))
                (ended.FocusedRow, entries)
            |> fst
        let totalCount =
            if ended.OnlyNeedsReview then max 0 (ended.TotalCount - List.length leaving - unloadedChanged)
            else ended.TotalCount
        let model' = {
            ended with
                Entries = Success remaining
                TotalCount = totalCount
                TotalAmount = { ended.TotalAmount with Amount = ended.TotalAmount.Amount - leavingAmount }
                LocallyRemoved = ended.LocallyRemoved + List.length leaving
                FocusedRow = focus
                HasMoreEntries = List.length remaining < totalCount
        }
        model', (ended.OnlyNeedsReview && unloadedChanged > 0)
    | _ -> ended, false

/// Trägt das Ergebnis von "Kategorie setzen" für die Auswahl (oder einer Gruppe der Rücknahme)
/// in die geladene Liste ein (ledger-w29y6). `target`: die Kategorie, auf die der Befehl
/// gesetzt hat (`None`: ohne Kategorie). Die Auswahl endet samt Auswahl-Modus und Picker.
/// Jede geänderte geladene Zeile trägt die neue Kategorie und ihren Fehlschlag; fällt sie aus
/// dem aktiven Filter, bleibt sie ausgegraut stehen (`applyFilterEffect`), eine Rücknahme macht
/// sie wieder zum Treffer. Ändert sich der Schlüssel der Sortierung (`SortByCategory`), oder
/// waren geänderte Buchungen nie geladen (`AllExcept`) und ein Kategorie-Filter ist aktiv,
/// ist die Liste veraltet (`OrderStale`) und Trefferzahl und Summe werden neu geholt
/// (`RunRefreshTotals`). Available wird nicht fortgeschrieben, die Kategorien laden danach
/// unbedingt neu. Rein, ohne Proxy.
let private applyCategoryBatch
    (target: CategoryChoice option)
    (outcome: CategoryBatchOutcome)
    (model: Model)
    : Model * Cmd<Msg> =
    let ended = {
        model with
            Selection = NoSelection
            SelectionAnchor = None
            SelectionMode = false
            BatchCategoryPickerOpen = false
            CategoryBatchRunning = false
    }
    let changedCount = List.length outcome.Changed
    let targetId = target |> Option.map (fun t -> t.CategoryId)
    let category = categoryOfId ended targetId
    let changeById = outcome.Changed |> List.map (fun c -> c.EntryId, c) |> Map.ofList
    let loaded =
        match ended.Entries with
        | Success entries -> entries |> List.filter (fun e -> changeById |> Map.containsKey e.Id)
        | _ -> []
    let updatedById =
        loaded
        |> List.map (fun e ->
            let failure =
                match changeById.[e.Id].WriteBack with
                | CategorySendFailed reason -> Some reason
                | _ -> None
            e.Id,
            { e with
                CategoryId = targetId |> Option.map (fun (YnabCategoryId g) -> g)
                CategoryName =
                    category
                    |> Option.map (fun c -> c.Name)
                    |> Option.orElse (target |> Option.map (fun t -> t.CategoryName))
                CategoryGroupName = category |> Option.map (fun c -> c.GroupName)
                CategorizationWriteBackFailure = failure })
        |> Map.ofList
    let withRows =
        match ended.Entries with
        | Success entries ->
            { ended with
                Entries = Success (entries |> List.map (fun e -> updatedById |> Map.tryFind e.Id |> Option.defaultValue e))
                DetailFor =
                    ended.DetailFor |> Option.map (fun d -> updatedById |> Map.tryFind d.Id |> Option.defaultValue d) }
        | _ -> ended
    let filtered = updatedById |> Map.toList |> List.fold (fun m (_, e) -> applyFilterEffect e m) withRows
    let unloadedChanged = changedCount - List.length loaded
    let sortKeyMoved = changedCount > 0 && (effectiveSort filtered).Field = SortByCategory
    let totalsStale = unloadedChanged > 0 && categoryFilterActive filtered.Filters
    let stale = sortKeyMoved || totalsStale
    let finalModel = if stale then { filtered with OrderStale = true } else filtered
    let refresh = if stale then Cmd.ofMsg RunRefreshTotals else Cmd.none
    finalModel, Cmd.batch [ refresh; (if changedCount > 0 then Cmd.ofMsg LoadCategories else Cmd.none) ]

/// Trägt das Ergebnis von "Lena fragen" bzw. "Rückfrage zurücknehmen" für die Auswahl (oder von
/// ihrer Rücknahme) in die geladene Liste ein (ledger-y54gr). `newStateOf` liefert das Flag, das
/// der Befehl für die Buchung gesetzt hat. `endSelection`: die Auswahl endet samt Auswahl-Modus
/// (die Aktion selbst); die Rücknahme lässt eine neue Auswahl stehen. Jede geänderte geladene
/// Zeile trägt das neue Flag und ihren Fehlschlag; fällt sie aus dem aktiven Filter, bleibt sie
/// ausgegraut stehen (`applyFilterEffect`, wie im Detail), eine Rücknahme macht sie wieder zum
/// Treffer. Der Zähler "Rückfrage Lena (n)" folgt den Vorzuständen der Antwort, auch bei
/// Buchungen, die nie geladen waren. Rein, ohne Proxy.
let private applyFlagBatch
    (newStateOf: Guid -> FlagState)
    (endSelection: bool)
    (outcome: FlagBatchOutcome)
    (model: Model)
    : Model =
    let ended =
        if endSelection then
            { model with
                Selection = NoSelection
                SelectionAnchor = None
                SelectionMode = false
                LenaBatchRunning = false }
        else
            { model with LenaBatchRunning = false }
    let isLena (state: FlagState) = if flagOfState state = AskLena then 1 else 0
    let askLenaDelta =
        outcome.Changed |> List.sumBy (fun c -> isLena (newStateOf c.EntryId) - isLena c.Previous)
    let withCount = { ended with AskLenaCount = max 0 (ended.AskLenaCount + askLenaDelta) }
    let changeById = outcome.Changed |> List.map (fun c -> c.EntryId, c) |> Map.ofList
    let update (e: LedgerEntryView) : LedgerEntryView option =
        changeById
        |> Map.tryFind e.Id
        |> Option.map (fun c ->
            let failure =
                match c.WriteBack with
                | FlagSendFailed reason -> Some reason
                | _ -> None
            { e with Flag = flagOfState (newStateOf e.Id); FlagWriteBackFailure = failure })
    let loaded =
        match withCount.Entries with
        | Success entries -> entries |> List.choose update
        | _ -> []
    let updatedById = loaded |> List.map (fun e -> e.Id, e) |> Map.ofList
    let withRows =
        match withCount.Entries with
        | Success entries ->
            { withCount with
                Entries = Success (entries |> List.map (fun e -> updatedById |> Map.tryFind e.Id |> Option.defaultValue e))
                DetailFor = withCount.DetailFor |> Option.map (fun d -> update d |> Option.defaultValue d) }
        | _ -> withCount
    updatedById |> Map.toList |> List.fold (fun m (_, e) -> applyFilterEffect e m) withRows

/// Sind die Tasten der Liste gerade aus? Ein offenes Filter-Popover und ein offenes Sheet
/// gehören der Tastatur; ein offenes Seitenpanel nicht, es folgt der Markierung
/// (ledger-q1hw2, ADR 0053 Punkt 5).
let private keyboardBlocked (model: Model) : bool =
    model.OpenPopover.IsSome
    || model.BatchCategoryPickerOpen
    || model.CategoryPickerFor.IsSome
    || model.SplitEditor.IsSome
    || model.MemoEdit.IsSome
    || (model.DetailFor.IsSome && model.DetailForm = DetailSheet)

/// Öffnet das Detail für `entry`: markiert die Zeile, setzt die Historie auf "lädt" und
/// stößt ihren Abruf an. Wechselt ein offenes Panel auf eine andere Buchung, läuft derselbe
/// Weg.
let private showDetail (entry: LedgerEntryView) (model: Model) : Model * Cmd<Msg> =
    { model with DetailFor = Some entry; FocusedRow = Some entry.Id; History = Loading; MemoEdit = None },
    Cmd.ofMsg (LoadHistory entry.Id)

/// Folgt ein offenes Panel der Markierung? Zeigt es eine andere Buchung als die markierte
/// Zeile, wechselt es auf diese und lädt deren Historie. Gleicher Stand, Sheet oder keine
/// Markierung ändern nichts.
let private followFocusWithPanel (model: Model, cmd: Cmd<Msg>, external: ExternalMsg) : Model * Cmd<Msg> * ExternalMsg =
    match model.DetailFor, model.FocusedRow, model.Entries with
    | Some detail, Some focusId, Success entries when model.DetailForm = DetailPanel && detail.Id <> focusId ->
        match entries |> List.tryFind (fun e -> e.Id = focusId) with
        | Some entry ->
            let model', loadCmd = showDetail entry model
            model', Cmd.batch [ cmd; loadCmd ], external
        | None -> model, cmd, external
    | _ -> model, cmd, external

/// Nimmt die Zeile den Prüfvermerk per Taste nicht an? In der Auswahl, ohne Schaltfläche
/// (in YNAB gelöscht) und während ein Aufruf läuft.
let private reviewKeyBlocked (model: Model) (entry: LedgerEntryView) : bool =
    model.SelectionMode
    || hasSelection model
    || entry.DeletedInYnab
    || (model.ReviewMarkRows |> Map.tryFind entry.Id) = Some ReviewMarkSending

let rec private updateCore (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with

    // ============================================
    // Konten
    // ============================================
    | LoadAccounts ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.ledger.getLedgerAccounts)
                ()
                (Ok >> AccountsLoaded)
                (fun ex -> AccountsLoaded (Error ex.Message))
        // Die Flag-Farbe für "Lena fragen" kommt mit den Konten (ADR 0056, Punkt 1).
        { model with Accounts = Loading }, Cmd.batch [ cmd; Cmd.ofMsg LoadLenaFlagSettings ], NoOp

    | AccountsLoaded (Ok accounts) ->
        // Die Vorauswahl kommt vom Server (YNAB-Standardkonto, sonst das erste) —
        // die Seite entscheidet das nicht selbst. Eine bereits getroffene Auswahl
        // (auch "Alle Konten") ueberlebt ein Neuladen, solange das gewaehlte Konto
        // noch existiert: sonst springt die Seite nach jedem Spiegel-Lauf zurueck
        // auf das Standardkonto, waehrend Roman einen anderen Filter ansieht.
        // Beim ersten Öffnen kommt die gemerkte Sicht des Geräts vor dem Standardkonto
        // (ledger-ybmqc); ist das gemerkte Konto weg, gilt das Standardkonto.
        let filter = resolveInitialFilter model.SelectedFilter model.RememberedFilter accounts
        let model' = { model with Accounts = Success accounts; SelectedFilter = filter }
        match filter with
        | Some _ -> resetEntries model', Cmd.ofMsg LoadEntries, NoOp
        | None -> { model' with Entries = NotAsked }, Cmd.none, NoOp

    | AccountsLoaded (Error message) ->
        { model with Accounts = Failure message }, Cmd.none, NoOp

    // ============================================
    // Kontostaende
    // ============================================
    | LoadBalances ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getLedgerAccountBalances
                ()
                (Ok >> BalancesLoaded)
                (fun ex -> BalancesLoaded (Error ex.Message))
        { model with Balances = Loading }, cmd, NoOp

    | BalancesLoaded (Ok balances) ->
        { model with Balances = Success balances }, Cmd.none, NoOp

    | BalancesLoaded (Error message) ->
        { model with Balances = Failure message }, Cmd.none, NoOp

    // ============================================
    // Konto-Filter
    // ============================================
    | SelectFilter filter ->
        // Ein Kontowechsel schliesst ein offenes Detail: es gehoerte zu
        // einer Buchung, die gleich nicht mehr in der Liste steht. Die
        // Scroll-Position wird bewusst NICHT gemerkt (Task-Notes) — alle anderen
        // Zustaende (Kontostaende, Lauf-Status) bleiben unangetastet stehen.
        { (resetEntries model) with
            SelectedFilter = Some filter
            RememberedFilter = Some filter
            AccountSheetOpen = false
            DetailFor = None
            MemoEdit = None
            History = NotAsked },
        Cmd.batch [ Cmd.ofMsg LoadEntries; Cmd.ofEffect (fun _ -> writeRememberedFilter filter) ],
        NoOp

    | OpenAccountSheet -> { model with AccountSheetOpen = true }, Cmd.none, NoOp

    | CloseAccountSheet -> { model with AccountSheetOpen = false }, Cmd.none, NoOp

    | SetSort field ->
        // Wie ein Filterwechsel: Seite 0, Auswahl leer, Filter und Konto bleiben stehen.
        { (resetEntries model) with Sort = nextSort (effectiveSort model) field }, Cmd.ofMsg LoadEntries, NoOp

    | SetSortExactly sort ->
        // Wie SetSort, nur mit der gewählten Kombination; das Sheet schließt.
        { (resetEntries model) with Sort = sort; SortSheetOpen = false }, Cmd.ofMsg LoadEntries, NoOp

    | OpenSortSheet -> { model with SortSheetOpen = true }, Cmd.none, NoOp

    | CloseSortSheet -> { model with SortSheetOpen = false }, Cmd.none, NoOp

    // ============================================
    // Buchungen, seitenweise
    // ============================================
    | LoadEntries -> resetEntries model, Cmd.ofMsg (RunLoadEntriesPage 0), NoOp

    | RunLoadEntriesPage page ->
        match model.SelectedFilter with
        | None -> model, Cmd.none, NoOp
        | Some filter ->
            let accountId = match filter with AllAccounts -> None | OneAccount id -> Some id
            let query = buildPageQuery model accountId page
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerEntriesPage
                    query
                    (fun result -> EntriesPageLoaded (page, Ok result))
                    (fun ex -> EntriesPageLoaded (page, Error ex.Message))
            model, cmd, NoOp

    | EntriesPageLoaded (page, Ok entryPage) ->
        let entries =
            if page = 0 then
                entryPage.Entries
            else
                (model.Entries |> RemoteData.withDefault []) @ entryPage.Entries
        { model with
            Entries = Success entries
            // Der Fokus überlebt das Nachladen; fällt seine Zeile aus der Liste, springt er
            // auf die erste (ledger-rjb8w). Mit offenem Detail ist die Markierung dessen
            // Buchung, oder keine, wenn sie auf den geladenen Seiten fehlt (nach einem
            // Sortierwechsel, ledger-q1hw2).
            FocusedRow =
                (match model.DetailFor with
                 | Some detail ->
                     if entries |> List.exists (fun e -> e.Id = detail.Id) then Some detail.Id else None
                 | None -> reconcileFocus entries model.FocusedRow)
            NextPage = page + 1
            TotalCount = entryPage.TotalCount
            TotalAmount = entryPage.TotalAmount
            // Ausgegraute Zeilen stehen in der Liste, zählen aber nicht zum Treffer.
            HasMoreEntries = List.length entries - Set.count model.FilteredOut < entryPage.TotalCount
            DeletedCount = entryPage.DeletedCount
            NeedsReviewCount = entryPage.NeedsReviewCount
            AskLenaCount = entryPage.AskLenaCount
            LoadingMore = false },
        Cmd.none,
        NoOp

    | EntriesPageLoaded (0, Error message) ->
        // Der Fehler bleibt an der Liste stehen (ErrorDisplay), statt als Toast
        // wegzublenden — die Seite waere sonst still leer.
        { model with Entries = Failure message; LoadingMore = false }, Cmd.none, NoOp

    | EntriesPageLoaded (_, Error message) ->
        // Ein Fehler beim Nachladen loescht die bereits sichtbare Liste nicht: sie
        // bleibt stehen, nur der Hinweis erscheint als Toast.
        { model with LoadingMore = false },
        Cmd.none,
        ShowToast ($"Weitere Buchungen konnten nicht geladen werden: {message}", ToastError)

    | LoadMoreEntries ->
        // Reiner Wächter: kein zweiter Nachlade-Aufruf waehrend einer laeuft, keiner
        // ohne weitere Seiten, keiner ohne eine erfolgreich geladene erste Seite.
        // Bei veralteter Reihenfolge ruht das Nachladen, bis "Neu laden" die Liste neu aufsetzt.
        if model.LoadingMore || model.OrderStale || not model.HasMoreEntries || not (RemoteData.isSuccess model.Entries) then
            model, Cmd.none, NoOp
        else
            { model with LoadingMore = true }, Cmd.ofMsg (RunLoadEntriesPage model.NextPage), NoOp

    | ToggleIncludeDeleted ->
        // Reine Zustandsänderung plus `Cmd.ofMsg LoadEntries` (dieselbe Teilung wie
        // beim Filterwechsel `SelectFilter`): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig (ADR 0020).
        { (resetEntries model) with IncludeDeleted = not model.IncludeDeleted }, Cmd.ofMsg LoadEntries, NoOp

    | ToggleOnlyNeedsReview ->
        // Reine Zustandsänderung plus `Cmd.ofMsg LoadEntries` (derselbe Schnitt wie
        // `ToggleIncludeDeleted`/ADR 0020): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig.
        { (resetEntries model) with OnlyNeedsReview = not model.OnlyNeedsReview }, Cmd.ofMsg LoadEntries, NoOp

    | ToggleOnlyAskLena ->
        // Wie `ToggleWithoutCategory`: reine Zustandsänderung plus `LoadEntries` (ADR 0020).
        let filters = { model.Filters with OnlyAskLena = not model.Filters.OnlyAskLena }
        { (resetEntries model) with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | ToggleWithoutCategory ->
        // Wie `ToggleOnlyNeedsReview`: reine Zustandsänderung plus `LoadEntries` (ADR 0020).
        let filters = { model.Filters with WithoutCategory = not model.Filters.WithoutCategory }
        { (resetEntries model) with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // ============================================
    // Historie einer Buchung
    // ============================================
    | OpenDetail entry ->
        // Im Auswahl-Modus am Handy wählt der Tipp aus (ADR 0053 Punkt 7), er öffnet nichts.
        // Am PC öffnet bei laufender Auswahl ein Klick auf die Zeile ebenfalls kein Detail,
        // er markiert die Zeile nur (ledger-w4vte).
        if model.SelectionMode then
            model, Cmd.none, NoOp
        elif hasSelection model then
            { model with FocusedRow = Some entry.Id }, Cmd.none, NoOp
        else
            let model', cmd = showDetail entry model
            model', cmd, NoOp

    | CloseDetail ->
        { model with DetailFor = None; History = NotAsked; MemoEdit = None }, Cmd.none, NoOp

    | DetailFormChanged form ->
        { model with DetailForm = form }, Cmd.none, NoOp

    | ListInteractionChanged interaction ->
        { model with ListInteraction = interaction }, Cmd.none, NoOp

    | LoadHistory entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.getEntryHistory
                entryId
                (fun lines -> HistoryLoaded (entryId, Ok lines))
                (fun ex -> HistoryLoaded (entryId, Error ex.Message))
        { model with History = Loading }, cmd, NoOp

    | HistoryLoaded (entryId, Ok lines) when model.DetailFor |> Option.exists (fun e -> e.Id = entryId) ->
        { model with History = Success lines }, Cmd.none, NoOp

    | HistoryLoaded (entryId, Error message) when model.DetailFor |> Option.exists (fun e -> e.Id = entryId) ->
        { model with History = Failure message }, Cmd.none, NoOp

    // Die Historie einer Buchung, die nicht mehr im Detail steht (schnelles Weiterspringen,
    // geschlossenes Detail): verwerfen.
    | HistoryLoaded _ -> model, Cmd.none, NoOp

    // ============================================
    // Spiegel
    // ============================================
    | LoadMirrorStatus ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.ledger.getLastMirrorRun)
                ()
                (Ok >> MirrorStatusLoaded)
                (fun ex -> MirrorStatusLoaded (Error ex.Message))
        { model with LastMirrorRun = Loading }, cmd, NoOp

    | MirrorStatusLoaded (Ok status) ->
        // Frische-Kriterium (ADR 0014): ist der geladene Lauf-Status älter als die
        // Schwelle (oder gab es noch nie einen), stößt dieselbe Seite denselben
        // Hintergrund-Lauf an wie der manuelle Knopf, nur ohne den zusätzlichen
        // Nutzer-Klick. Läuft bereits ein Lauf, greift dieselbe Doppeldruck-Sperre wie
        // bei `StartMirror`: kein zweiter Auslöser aus diesem Tab. Der serverseitige
        // Einzel-Flug-Schutz (`Ledger.CommandHandlers.runMirrorGuarded`) hält zusätzlich
        // gegen einen zweiten Tab oder einen zeitgleichen Lauf-nach-Push.
        let model' = { model with LastMirrorRun = Success status }
        if not model.MirrorRunning && isMirrorStale DateTime.UtcNow status then
            { model' with MirrorRunning = true }, Cmd.ofMsg RunMirror, NoOp
        else
            model', Cmd.none, NoOp

    | MirrorStatusLoaded (Error message) ->
        { model with LastMirrorRun = Failure message }, Cmd.none, NoOp

    | StartMirror ->
        // Doppeldruck-Sperre: ein zweiter Druck waehrend eines Laufs tut nichts.
        // Die Liste bleibt stehen — sie wird erst nach dem Lauf neu geladen.
        if model.MirrorRunning then
            model, Cmd.none, NoOp
        else
            { model with MirrorRunning = true }, Cmd.ofMsg RunMirror, NoOp

    | RunMirror ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.mirrorYnabBudget
                ()
                MirrorFinished
                (fun ex -> MirrorFinished (Error ex.Message))
        model, cmd, NoOp

    | MirrorFinished (Ok summary) ->
        // Konten und Kontostaende neu laden, nicht nur die Buchungen: der erste Lauf
        // auf einem leeren Kassenbuch legt die Konten ueberhaupt erst an, und ein
        // spaeterer Lauf kann ein in YNAB neu angelegtes Konto oder geaenderte
        // Staende mitbringen. `AccountsLoaded` zieht die Buchungen des gewaehlten
        // Filters selbst nach (Regression ledger-kt0wq).
        { model with MirrorRunning = false },
        Cmd.batch [ Cmd.ofMsg LoadAccounts; Cmd.ofMsg LoadBalances; Cmd.ofMsg LoadMirrorStatus ],
        ShowToast (mirrorSummaryText summary, ToastSuccess)

    | MirrorFinished (Error message) ->
        // Ein Fehler wird sichtbar gemacht, nie still geschluckt (ADR 0014): als
        // Toast jetzt und als Lauf-Status dauerhaft.
        { model with MirrorRunning = false },
        Cmd.ofMsg LoadMirrorStatus,
        ShowToast ($"Spiegel-Lauf gescheitert: {message}", ToastError)

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    | LoadCategories ->
        // Der eigentliche Aufruf braucht das konfigurierte YNAB-Budget und läuft
        // deshalb im Haupt-`State.fs` (analog zu `Components.Rules.State`s
        // `LoadCategories`), nicht hinter dem Proxy dieses Moduls.
        { model with Categories = Loading }, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) ->
        { model with
            Categories = Success categories
            CategoriesLoadedAt = Some DateTime.Now
            CategoriesRefreshing = false },
        Cmd.none,
        NoOp

    | CategoriesLoaded (Error message) ->
        // Ein vorhandener Stand bleibt sichtbar (ledger-kwwxz); nur ohne Stand
        // wird der Fehler zum Zustand.
        let categories =
            match model.Categories with
            | Success _ -> model.Categories
            | _ -> Failure message
        { model with Categories = categories; CategoriesRefreshing = false }, Cmd.none, NoOp

    | EnsureCategoriesFresh ->
        // Die Entscheidung und der Abruf liegen im Haupt-`State.fs`.
        model, Cmd.none, NoOp

    | OpenFilterPopover chip ->
        let model' =
            match chip with
            | CategoryChip ->
                { model with
                    CategoryDraft = model.Filters.CategoryIds
                    CategorySearch = ""
                    HiddenCategoriesExpanded = false }
            | DateChip ->
                { model with
                    DateDraft = model.Filters.DateRange |> Option.defaultValue emptyDateRange
                    DateDraftFromInvalid = false
                    DateDraftToInvalid = false }
            | PayeeChip ->
                { model with PayeeSearch = "" }
            | MemoChip ->
                { model with MemoDraft = model.Filters.Memo |> Option.defaultValue "" }
            | AmountChip ->
                { model with
                    AmountMinDraft = model.Filters.AmountMin |> Option.map formatAmountDraft |> Option.defaultValue ""
                    AmountMaxDraft = model.Filters.AmountMax |> Option.map formatAmountDraft |> Option.defaultValue "" }
            | NeedsReviewChip -> model
            // Ein Umschalter ohne Popover wie "Zu prüfen".
            | AskLenaChip -> model
            // Ein Umschalter ohne Popover (ADR 0048): es gibt keinen Entwurf zu setzen.
            | WithoutCategoryChip -> model
        let cmd = if chip = PayeeChip then Cmd.ofMsg LoadPayeeSuggestions else Cmd.none
        { model' with OpenPopover = Some chip }, cmd, NoOp

    | CloseFilterPopover ->
        // Esc, Klick daneben oder "Schließen": der Entwurf wird verworfen, `Filters`
        // bleibt unangetastet (AC "der vorherige Filterzustand bleibt unverändert").
        { model with OpenPopover = None }, Cmd.none, NoOp

    // -------- Filter-Knöpfe im Detail (ADR 0053, löst das Zellmenü aus ledger-rfq5p ab) --------

    // Beide lassen das Detail offen und mit derselben Buchung stehen: Filtern ändert die
    // Liste, nicht die Buchung.
    | FilterByRowPayee payee ->
        let filters = { model.Filters with Payee = Some payee }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | FilterByRowCategory categoryId ->
        // "Ohne Kategorie" verbindet sich mit einer Kategorienauswahl per Oder (ADR 0048)
        // und würde Buchungen ohne Ziel einschließen, die Roman nicht gewählt hat.
        let filters =
            { model.Filters with CategoryIds = Set.singleton categoryId; WithoutCategory = false }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Kategorie (Mehrfachauswahl, Click-Commit) --------

    | SetCategorySearch text ->
        { model with CategorySearch = text }, Cmd.none, NoOp

    | ToggleCategoryDraft categoryId ->
        let draft =
            if model.CategoryDraft.Contains categoryId then Set.remove categoryId model.CategoryDraft
            else Set.add categoryId model.CategoryDraft
        { model with CategoryDraft = draft }, Cmd.none, NoOp

    | ToggleHiddenCategoriesExpanded ->
        { model with HiddenCategoriesExpanded = not model.HiddenCategoriesExpanded }, Cmd.none, NoOp

    | ResetCategoryDraft ->
        { model with CategoryDraft = Set.empty }, Cmd.none, NoOp

    | ApplyCategoryFilter ->
        if categoryApplyDisabled model.CategoryDraft model.Filters.CategoryIds then
            model, Cmd.none, NoOp
        else
            let filters = { model.Filters with CategoryIds = model.CategoryDraft }
            let recent = pushRecentCategories model.CategoryDraft model.RecentCategories
            let remember = Cmd.ofEffect (fun _ -> writeRecentCategories recent)
            resetEntries { model with Filters = filters; OpenPopover = None; RecentCategories = recent },
            Cmd.batch [ Cmd.ofMsg LoadEntries; remember ],
            NoOp

    | RemoveCategoryFilter ->
        let filters = { model.Filters with CategoryIds = Set.empty }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Zeitraum (Schnellwahl schreibt Von/Bis mit) --------

    | SetDateQuick quick ->
        let from_, to_ = quickDateRange DateTime.Today quick
        { model with
            DateDraft = { Quick = Some quick; From = Some from_; To = Some to_ }
            DateDraftFromInvalid = false
            DateDraftToInvalid = false }, Cmd.none, NoOp

    | SetDateDraftFrom entry ->
        let draft = { model.DateDraft with Quick = None }
        match entry with
        | DateField.ValidDate d ->
            { model with DateDraft = { draft with From = Some d }; DateDraftFromInvalid = false }, Cmd.none, NoOp
        | DateField.EmptyDate ->
            { model with DateDraft = { draft with From = None }; DateDraftFromInvalid = false }, Cmd.none, NoOp
        | DateField.InvalidDate _ ->
            { model with DateDraft = draft; DateDraftFromInvalid = true }, Cmd.none, NoOp

    | SetDateDraftTo entry ->
        let draft = { model.DateDraft with Quick = None }
        match entry with
        | DateField.ValidDate d ->
            { model with DateDraft = { draft with To = Some d }; DateDraftToInvalid = false }, Cmd.none, NoOp
        | DateField.EmptyDate ->
            { model with DateDraft = { draft with To = None }; DateDraftToInvalid = false }, Cmd.none, NoOp
        | DateField.InvalidDate _ ->
            { model with DateDraft = draft; DateDraftToInvalid = true }, Cmd.none, NoOp

    | ResetDateDraft ->
        { model with DateDraft = emptyDateRange; DateDraftFromInvalid = false; DateDraftToInvalid = false }, Cmd.none, NoOp

    | ApplyDateFilter when dateDraftHasError model ->
        // Ungültige Eingabe in Von oder Bis: der Filter bleibt, wie er ist.
        model, Cmd.none, NoOp

    | ApplyDateFilter ->
        let range =
            if model.DateDraft.From.IsNone && model.DateDraft.To.IsNone then None
            else Some model.DateDraft
        let filters = { model.Filters with DateRange = range }
        resetEntries { model with Filters = filters; OpenPopover = None }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveDateFilter ->
        let filters = { model.Filters with DateRange = None }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Payee (Einzelauswahl, filtert sofort) --------

    | SetPayeeSearch text ->
        { model with PayeeSearch = text }, Cmd.none, NoOp

    | LoadPayeeSuggestions ->
        match model.SelectedFilter with
        | None -> { model with PayeeSuggestions = Success [] }, Cmd.none, NoOp
        | Some filter ->
            let accountId = match filter with AllAccounts -> None | OneAccount id -> Some id
            let query : LedgerPayeeSuggestionQuery = {
                AccountId = accountId
                IncludeDeleted = model.IncludeDeleted
                OnlyNeedsReview = model.OnlyNeedsReview
                OnlyAskLena = model.Filters.OnlyAskLena
                WithoutCategory = model.Filters.WithoutCategory
                CategoryIds = model.Filters.CategoryIds |> Set.toList
                DateFrom = model.Filters.DateRange |> Option.bind (fun d -> d.From)
                DateTo = model.Filters.DateRange |> Option.bind (fun d -> d.To)
                Memo = model.Filters.Memo
                AmountMin = model.Filters.AmountMin
                AmountMax = model.Filters.AmountMax
            }
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerPayeeSuggestions
                    query
                    (Ok >> PayeeSuggestionsLoaded)
                    (fun ex -> PayeeSuggestionsLoaded (Error ex.Message))
            { model with PayeeSuggestions = Loading }, cmd, NoOp

    | PayeeSuggestionsLoaded (Ok suggestions) ->
        { model with PayeeSuggestions = Success suggestions }, Cmd.none, NoOp

    | PayeeSuggestionsLoaded (Error message) ->
        { model with PayeeSuggestions = Failure message }, Cmd.none, NoOp

    | SelectPayeeFilter payee ->
        let filters = { model.Filters with Payee = Some payee }
        resetEntries { model with Filters = filters; OpenPopover = None }, Cmd.ofMsg LoadEntries, NoOp

    | RemovePayeeFilter ->
        let filters = { model.Filters with Payee = None }
        resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Memo (Suchfeld mit Verzögerung) --------

    | SetMemoDraft text ->
        let newVersion = model.MemoVersion + 1
        { model with MemoDraft = text; MemoVersion = newVersion },
        Debounce.delayedDefault (CommitMemoFilter (text, newVersion)),
        NoOp

    | CommitMemoFilter (text, version) ->
        if version <> model.MemoVersion then
            // Eine spätere Eingabe hat diese Verzögerung schon überholt.
            model, Cmd.none, NoOp
        else
            let trimmed = text.Trim()
            let filters = { model.Filters with Memo = (if trimmed = "" then None else Some trimmed) }
            resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveMemoFilter ->
        let filters = { model.Filters with Memo = None }
        resetEntries { model with Filters = filters; MemoDraft = "" }, Cmd.ofMsg LoadEntries, NoOp

    // -------- Betrag (Suchfeld mit Verzögerung, Von/Bis) --------

    | SetAmountMinDraft text ->
        let newVersion = model.AmountVersion + 1
        { model with AmountMinDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (CommitAmountFilter (parseAmountFilterText text, parseAmountFilterText model.AmountMaxDraft, newVersion)),
        NoOp

    | SetAmountMaxDraft text ->
        let newVersion = model.AmountVersion + 1
        { model with AmountMaxDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (CommitAmountFilter (parseAmountFilterText model.AmountMinDraft, parseAmountFilterText text, newVersion)),
        NoOp

    | CommitAmountFilter (min_, max_, version) ->
        if version <> model.AmountVersion then
            model, Cmd.none, NoOp
        else
            let filters = { model.Filters with AmountMin = min_; AmountMax = max_ }
            resetEntries { model with Filters = filters }, Cmd.ofMsg LoadEntries, NoOp

    | RemoveAmountFilter ->
        let filters = { model.Filters with AmountMin = None; AmountMax = None }
        resetEntries { model with Filters = filters; AmountMinDraft = ""; AmountMaxDraft = "" }, Cmd.ofMsg LoadEntries, NoOp

    | ClearAllFilters ->
        // Der Chip "Ohne Kategorie" bleibt stehen (wie "Zu prüfen"): er gehört nicht zu
        // den fünf Such-Filtern (ADR 0048).
        resetEntries
            { model with
                Filters =
                    { noFilters with
                        WithoutCategory = model.Filters.WithoutCategory
                        OnlyAskLena = model.Filters.OnlyAskLena } },
        Cmd.ofMsg LoadEntries,
        NoOp

    // ============================================
    // Prüfvermerk setzen und zurückschreiben (ledger-r777c, ADR 0050)
    // ============================================

    | StartSetReviewMark (entryId, target) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunSetReviewMark (entryId, target)),
        NoOp

    | RunSetReviewMark (entryId, target) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setReviewMark entryId)
                target
                (fun result -> ReviewMarkSet (entryId, target, result))
                (fun ex -> ReviewMarkSet (entryId, target, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkSet (entryId, target, Ok outcome) ->
        // Nach "Geprüft" ist nichts mehr zu prüfen (approved, cleared mindestens bestätigt),
        // nach "Ungeprüft" ist die Buchung wieder zu prüfen (approved = false). Ziel gleich
        // Ist ändert daran nichts. Der Fehlschlag kommt aus der Antwort; ohne Änderung und
        // ohne Versand bleibt ein offener Fehlschlag stehen.
        let needsReview = Some (target = Unreviewed)
        applyReviewMarkResult entryId needsReview (writeBackFailureOf outcome) model, Cmd.none, NoOp

    | ReviewMarkSet (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    | StartRetryReviewMark entryId ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunRetryReviewMark entryId),
        NoOp

    | RunRetryReviewMark entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryReviewMarkWriteBack
                entryId
                (fun result -> ReviewMarkRetried (entryId, result))
                (fun ex -> ReviewMarkRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkRetried (entryId, Ok writeBack) ->
        let failureOf (_: LedgerEntryView) =
            match writeBack with
            | ReviewMarkSendFailed reason -> Some reason
            | ReviewMarkSent | ReviewMarkNotSent -> None
        applyReviewMarkResult entryId None failureOf model, Cmd.none, NoOp

    | ReviewMarkRetried (entryId, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Erneut senden am Hinweis "Noch nicht in YNAB" (ledger-cks06)
    // ============================================

    | StartRetryPush entryId ->
        { model with PushRetryRows = model.PushRetryRows |> Map.add entryId PushSending },
        Cmd.ofMsg (RunRetryPush entryId),
        NoOp

    | RunRetryPush entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryPushToYnab
                entryId
                (fun result -> PushRetried (entryId, result))
                (fun ex -> PushRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | PushRetried (entryId, Ok result) ->
        let toast =
            match result with
            | PushToYnabSent -> ShowToast (pushRetrySentToastText, ToastSuccess)
            | PushToYnabFailed reason -> ShowToast ($"{pushRetryFailedToastTitle}: {reason}", ToastError)
        applyPushRetryResult entryId DateTime.UtcNow result model, Cmd.none, toast

    | PushRetried (entryId, Error message) ->
        { model with PushRetryRows = model.PushRetryRows |> Map.remove entryId },
        Cmd.none,
        ShowToast ($"{pushRetryFailedToastTitle}: {message}", ToastError)

    // ============================================
    // Wisch nach rechts: geprüft, mit Rückgängig (ledger-th36h)
    // ============================================

    | StartSwipeReviewed entryId ->
        // Nur eine zu prüfende Zeile, die nicht schon sendet. Eine geprüfte Zeile bleibt
        // bei `NoChange`: kein Aufruf, kein Toast.
        let entry =
            match model.Entries with
            | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId)
            | _ -> None
        match entry with
        | Some e when swipeAppliesTo e && (model.ReviewMarkRows |> Map.tryFind entryId) <> Some ReviewMarkSending ->
            { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
            Cmd.ofMsg (RunSwipeReviewed entryId),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSwipeReviewed entryId ->
        let payee =
            match model.Entries with
            | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId) |> Option.bind (fun e -> e.Payee)
            | _ -> None
        let text = swipeReviewedToastText payee
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setReviewMark entryId)
                Reviewed
                (fun result -> SwipeReviewedDone (entryId, text, result))
                (fun ex -> SwipeReviewedDone (entryId, text, Error ex.Message))
        model, cmd, NoOp

    | SwipeReviewedDone (entryId, text, Ok outcome) ->
        let model' = applySwipeReviewed entryId outcome model
        let external =
            match outcome.WriteBack with
            | ReviewMarkSendFailed reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | _ when outcome.Change = ReviewMarkUnchanged -> NoOp
            | _ ->
                ShowToastWithAction (
                    text,
                    ToastSuccess,
                    { Label = undoToastLabel; Kind = UndoReviewMark (entryId, outcome.Previous) }
                )
        model', Cmd.none, external

    | SwipeReviewedDone (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        ShowToast ($"Prüfvermerk nicht gesetzt: {message}", ToastError)

    | StartUndoReviewMark (entryId, previous) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
        Cmd.ofMsg (RunUndoReviewMark (entryId, previous)),
        NoOp

    | RunUndoReviewMark (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.restoreReviewMark entryId)
                previous
                (fun result -> ReviewMarkRestored (entryId, previous, result))
                (fun ex -> ReviewMarkRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkRestored (entryId, previous, Ok outcome) ->
        let model' = applyUndoReviewMark entryId previous outcome model
        let external =
            match outcome.WriteBack with
            | ReviewMarkSendFailed reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | _ -> NoOp
        model', Cmd.none, external

    | ReviewMarkRestored (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    // ============================================
    // Kategorie ändern im Detail (ledger-sfab0, ADR 0055)
    // ============================================

    | OpenCategoryPicker entryId ->
        // Nur für die Buchung im offenen Detail und nur, wenn sie änderbar ist; sonst bleibt der
        // Picker zu (gesperrte Fälle haben einen Pfeil ohne Wirkung).
        match findEntry model entryId with
        | Some entry when categoryLockFor entry = CategoryEditable
                          && model.DetailFor |> Option.exists (fun d -> d.Id = entryId) ->
            let current = entry.CategoryId |> Option.map YnabCategoryId
            let suggestions =
                match entry.Payee with
                | Some payee when not (String.IsNullOrWhiteSpace payee) ->
                    Cmd.ofMsg (RunLoadCategorySuggestions (entryId, payee, current))
                | _ -> Cmd.none
            { model with CategoryPickerFor = Some entryId; CategorySuggestions = []; CategoryCard = None },
            // Der Aufruf je Öffnung lädt die Kategorien nur bei Stand über zwei Minuten nach
            // (ledger-kwwxz) und holt die Vorschläge zum Payee.
            Cmd.batch [ Cmd.ofMsg EnsureCategoriesFresh; suggestions ],
            NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseCategoryPicker -> { model with CategoryPickerFor = None; CategorySuggestions = [] }, Cmd.none, NoOp

    | RunLoadCategorySuggestions (entryId, payee, current) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.getPayeeCategorySuggestions payee)
                current
                (fun ids -> CategorySuggestionsLoaded (entryId, Ok ids))
                (fun ex -> CategorySuggestionsLoaded (entryId, Error ex.Message))
        model, cmd, NoOp

    | CategorySuggestionsLoaded (entryId, result) when model.CategoryPickerFor = Some entryId ->
        // Ein Fehler lässt den Abschnitt weg; der Picker bleibt benutzbar.
        let ids = match result with Ok ids -> ids | Error _ -> []
        { model with CategorySuggestions = ids }, Cmd.none, NoOp

    // Die Antwort für einen Picker, der inzwischen zu ist oder zu einer anderen Buchung gehört.
    | CategorySuggestionsLoaded _ -> model, Cmd.none, NoOp

    | StartSetEntryCategory (entryId, newId) ->
        match findEntry model entryId with
        | Some entry when categoryLockFor entry = CategoryEditable
                          && (model.CategoryRows |> Map.tryFind entryId) <> Some CategorySending ->
            { model with
                CategoryRows = model.CategoryRows |> Map.add entryId CategorySending
                CategoryPickerFor = None
                CategorySuggestions = []
                CategoryCard = None
                RecentCategories = rememberPickedCategory newId model.RecentCategories },
            Cmd.batch [
                Cmd.ofMsg (RunSetEntryCategory (entryId, newId))
                Cmd.ofEffect (fun _ -> writeRecentCategories (rememberPickedCategory newId model.RecentCategories))
            ],
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSetEntryCategory (entryId, newId) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryCategory entryId)
                newId
                (fun result -> EntryCategorySet (entryId, newId, result))
                (fun ex -> EntryCategorySet (entryId, newId, Error ex.Message))
        model, cmd, NoOp

    | EntryCategorySet (entryId, newId, Ok outcome) ->
        let model', cmd = applyCategoryResult entryId newId None true outcome model
        let newName = categoryOfId model newId |> Option.map (fun c -> c.Name)
        let external =
            match outcome.Change, outcome.WriteBack with
            | CategoryUnchanged, CategorySendFailed reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | CategoryUnchanged, _ -> NoOp
            | CategoryChanged, writeBack ->
                let text = categoryChangedToastText newName outcome.Previous
                let action = { Label = undoToastLabel; Kind = UndoCategory (entryId, outcome.Previous) }
                match writeBack with
                | CategorySendFailed reason ->
                    ShowToastWithAction ($"{text}. {categoryHintTitle}: {reason}", ToastWarning, action)
                | _ -> ShowToastWithAction (text, ToastSuccess, action)
        model', cmd, external

    | EntryCategorySet (entryId, _, Error message) ->
        { model with
            CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed message) },
        Cmd.none,
        ShowToast ($"Kategorie nicht geändert: {message}", ToastError)

    | StartUndoCategory (entryId, previous) ->
        { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending },
        Cmd.ofMsg (RunUndoCategory (entryId, previous)),
        NoOp

    | RunUndoCategory (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryCategory entryId)
                (previous |> Option.map (fun p -> p.CategoryId))
                (fun result -> EntryCategoryRestored (entryId, previous, result))
                (fun ex -> EntryCategoryRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | EntryCategoryRestored (entryId, previous, Ok outcome) ->
        // Ohne neue Karte, mit vertauschten Kategorien in Available; der Rücknahme-Toast hat
        // kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme).
        let newId = previous |> Option.map (fun p -> p.CategoryId)
        let fallback = previous |> Option.map (fun p -> p.CategoryName)
        let model', cmd = applyCategoryResult entryId newId fallback false outcome model
        let name = categoryOfId model newId |> Option.map (fun c -> c.Name) |> Option.orElse fallback
        let external =
            match outcome.WriteBack with
            | CategorySendFailed reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | _ when outcome.Change = CategoryUnchanged -> NoOp
            | _ -> ShowToast (categoryRestoredToastText name, ToastSuccess)
        model', cmd, external

    | EntryCategoryRestored (entryId, _, Error message) ->
        { model with
            CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    | StartRetryCategorization entryId ->
        { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending },
        Cmd.ofMsg (RunRetryCategorization entryId),
        NoOp

    | RunRetryCategorization entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryCategorizationWriteBack
                entryId
                (fun result -> CategorizationRetried (entryId, result))
                (fun ex -> CategorizationRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | CategorizationRetried (entryId, Ok writeBack) ->
        let failure =
            match writeBack with
            | CategorySendFailed reason -> Some reason
            | CategorySent | CategoryNotSent -> None
        let cleared = { model with CategoryRows = model.CategoryRows |> Map.remove entryId }
        let model' =
            match findEntry cleared entryId with
            | Some entry -> replaceEntry { entry with CategorizationWriteBackFailure = failure } cleared
            | None -> cleared
        let external =
            match writeBack with
            | CategorySendFailed reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', Cmd.none, external

    | CategorizationRetried (entryId, Error message) ->
        { model with
            CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Lena fragen im Detail (ledger-xfjcp, ADR 0056)
    // ============================================

    | LoadLenaFlagSettings ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.settings.getLenaFlagSettings)
                ()
                (Ok >> LenaFlagSettingsLoaded)
                (fun ex -> LenaFlagSettingsLoaded (Error ex.Message))
        model, cmd, NoOp

    | LenaFlagSettingsLoaded (Ok settings) ->
        { model with LenaFlagColor = settings.SavedColor }, Cmd.none, NoOp

    // Ohne die Farbe bleibt "Lena fragen" gesperrt und nennt den Grund; ein Fehler ist kein Toast wert.
    | LenaFlagSettingsLoaded (Error _) -> model, Cmd.none, NoOp

    | StartSetLenaFlag (entryId, target) ->
        match findEntry model entryId with
        | Some entry when (lenaActionFor model.LenaFlagColor entry).DisabledReason.IsNone
                          && (lenaActionFor model.LenaFlagColor entry).Target = target
                          && (model.FlagRows |> Map.tryFind entryId) <> Some FlagSending ->
            { model with FlagRows = model.FlagRows |> Map.add entryId FlagSending },
            Cmd.ofMsg (RunSetLenaFlag (entryId, target)),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSetLenaFlag (entryId, target) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setLenaFlag entryId)
                target
                (fun result -> LenaFlagSet (entryId, target, result))
                (fun ex -> LenaFlagSet (entryId, target, Error ex.Message))
        model, cmd, NoOp

    | LenaFlagSet (entryId, target, Ok outcome) ->
        let newState =
            match target with
            | AssignLena -> { Color = model.LenaFlagColor; Name = Some lenaFlagName }
            | WithdrawLena -> { Color = None; Name = None }
        let model' = applyFlagResult entryId newState outcome model
        let external =
            match outcome.Change, outcome.WriteBack with
            | FlagUnchanged, _ -> NoOp
            | FlagChanged, writeBack ->
                let text = lenaFlagToastText target
                let action = { Label = undoToastLabel; Kind = UndoFlag (entryId, outcome.Previous) }
                match writeBack with
                | FlagSendFailed reason -> ShowToastWithAction ($"{text}. {flagHintTitle}: {reason}", ToastWarning, action)
                | _ -> ShowToastWithAction (text, ToastSuccess, action)
        model', Cmd.none, external

    | LenaFlagSet (entryId, _, Error message) ->
        { model with FlagRows = model.FlagRows |> Map.add entryId (FlagCommandFailed message) },
        Cmd.none,
        ShowToast ($"Flag nicht geändert: {message}", ToastError)

    | StartUndoFlag (entryId, previous) ->
        { model with FlagRows = model.FlagRows |> Map.add entryId FlagSending },
        Cmd.ofMsg (RunUndoFlag (entryId, previous)),
        NoOp

    | RunUndoFlag (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.restoreFlag entryId)
                previous
                (fun result -> FlagRestored (entryId, previous, result))
                (fun ex -> FlagRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | FlagRestored (entryId, previous, Ok outcome) ->
        // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme).
        let model' = applyFlagResult entryId previous outcome model
        let external =
            match outcome.WriteBack with
            | FlagSendFailed reason -> ShowToast ($"{flagHintTitle}: {reason}", ToastWarning)
            | _ when outcome.Change = FlagUnchanged -> NoOp
            | _ -> ShowToast (flagRestoredToastText, ToastSuccess)
        model', Cmd.none, external

    | FlagRestored (entryId, _, Error message) ->
        { model with FlagRows = model.FlagRows |> Map.add entryId (FlagCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    | StartRetryFlag entryId ->
        { model with FlagRows = model.FlagRows |> Map.add entryId FlagSending },
        Cmd.ofMsg (RunRetryFlag entryId),
        NoOp

    | RunRetryFlag entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryFlagWriteBack
                entryId
                (fun result -> FlagRetried (entryId, result))
                (fun ex -> FlagRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | FlagRetried (entryId, Ok writeBack) ->
        let failure =
            match writeBack with
            | FlagSendFailed reason -> Some reason
            | FlagSent | FlagNotSent -> None
        let cleared = { model with FlagRows = model.FlagRows |> Map.remove entryId }
        let model' =
            match findEntry cleared entryId with
            | Some entry -> replaceEntry { entry with FlagWriteBackFailure = failure } cleared
            | None -> cleared
        let external =
            match writeBack with
            | FlagSendFailed reason -> ShowToast ($"{flagHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', Cmd.none, external

    | FlagRetried (entryId, Error message) ->
        { model with FlagRows = model.FlagRows |> Map.add entryId (FlagCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Memo bearbeiten im Detail (ledger-fa1fp, ADR 0062)
    // ============================================

    | StartMemoEdit entryId ->
        match findEntry model entryId with
        | Some entry when (memoActionFor entry).DisabledReason.IsNone
                          && (model.MemoRows |> Map.tryFind entryId) <> Some MemoSending ->
            { model with
                MemoEdit = Some { MemoEntryId = entryId; MemoDraft = memoTextOf entry |> Option.defaultValue "" }
                MemoRows = model.MemoRows |> Map.remove entryId },
            Cmd.none,
            NoOp
        | _ -> model, Cmd.none, NoOp

    | MemoEditChanged text ->
        match model.MemoEdit with
        | Some edit ->
            { model with
                MemoEdit = Some { edit with MemoDraft = text }
                MemoRows = model.MemoRows |> Map.remove edit.MemoEntryId },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp

    | CancelMemoEdit ->
        match model.MemoEdit with
        | Some edit when (model.MemoRows |> Map.tryFind edit.MemoEntryId) <> Some MemoSending ->
            { model with MemoEdit = None; MemoRows = model.MemoRows |> Map.remove edit.MemoEntryId }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | StartSaveMemo entryId ->
        match model.MemoEdit, findEntry model entryId with
        | Some edit, Some entry when edit.MemoEntryId = entryId
                                     && (model.MemoRows |> Map.tryFind entryId) <> Some MemoSending
                                     && (memoActionFor entry).DisabledReason.IsNone
                                     && (memoDraftProblem edit.MemoDraft).IsNone ->
            if memoDraftChangesEntry edit.MemoDraft entry then
                { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
                Cmd.ofMsg (RunSetMemo (entryId, edit.MemoDraft)),
                NoOp
            else
                // Gleiches Memo: nichts zu speichern, das Feld schließt ohne Aufruf.
                { model with MemoEdit = None }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSetMemo (entryId, text) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryMemo entryId)
                text
                (fun result -> MemoSet (entryId, result))
                (fun ex -> MemoSet (entryId, Error ex.Message))
        model, cmd, NoOp

    | MemoSet (entryId, Ok outcome) ->
        let model' = applyMemoResult entryId outcome model
        let history = reloadHistoryFor entryId model'
        let external =
            match outcome.MemoChange, outcome.MemoWriteBack with
            | MemoUnchanged, _ -> NoOp
            | MemoChanged, writeBack ->
                let text = memoToastText outcome.PreviousMemo outcome.CurrentMemo
                let action = { Label = undoToastLabel; Kind = UndoMemo (entryId, outcome.PreviousMemo) }
                match writeBack with
                | MemoSendFailed reason -> ShowToastWithAction ($"{text}. {memoHintTitle}: {reason}", ToastWarning, action)
                | _ -> ShowToastWithAction (text, ToastSuccess, action)
        model', (if outcome.MemoChange = MemoChanged then history else Cmd.none), external

    // Das Eingabefeld bleibt mit dem Entwurf offen, der Hinweis nennt den Grund.
    | MemoSet (entryId, Error message) ->
        { model with MemoRows = model.MemoRows |> Map.add entryId (MemoCommandFailed message) },
        Cmd.none,
        ShowToast ($"Memo nicht geändert: {message}", ToastError)

    | StartUndoMemo (entryId, previous) ->
        { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
        Cmd.ofMsg (RunUndoMemo (entryId, previous)),
        NoOp

    | RunUndoMemo (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryMemo entryId)
                (previous |> Option.defaultValue "")
                (fun result -> MemoRestored (entryId, result))
                (fun ex -> MemoRestored (entryId, Error ex.Message))
        model, cmd, NoOp

    | MemoRestored (entryId, Ok outcome) ->
        // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme).
        let model' = applyMemoResult entryId outcome model
        let external =
            match outcome.MemoWriteBack with
            | MemoSendFailed reason -> ShowToast ($"{memoHintTitle}: {reason}", ToastWarning)
            | _ when outcome.MemoChange = MemoUnchanged -> NoOp
            | _ -> ShowToast (memoRestoredToastText, ToastSuccess)
        model', (if outcome.MemoChange = MemoChanged then reloadHistoryFor entryId model' else Cmd.none), external

    | MemoRestored (entryId, Error message) ->
        { model with MemoRows = model.MemoRows |> Map.add entryId (MemoCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    | StartRetryMemo entryId ->
        { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
        Cmd.ofMsg (RunRetryMemo entryId),
        NoOp

    | RunRetryMemo entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryMemoWriteBack
                entryId
                (fun result -> MemoRetried (entryId, result))
                (fun ex -> MemoRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | MemoRetried (entryId, Ok writeBack) ->
        let failure =
            match writeBack with
            | MemoSendFailed reason -> Some reason
            | MemoSent | MemoNotSent -> None
        let cleared = { model with MemoRows = model.MemoRows |> Map.remove entryId }
        let model' =
            match findEntry cleared entryId with
            | Some entry -> replaceEntry { entry with MemoWriteBackFailure = failure } cleared
            | None -> cleared
        let external =
            match writeBack with
            | MemoSendFailed reason -> ShowToast ($"{memoHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', Cmd.none, external

    | MemoRetried (entryId, Error message) ->
        { model with MemoRows = model.MemoRows |> Map.add entryId (MemoCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Split-Editor im Detail (ledger-pafkp, ADR 0058)
    // ============================================

    | OpenSplitEditor entryId ->
        // Nur für die Buchung im offenen Detail und nur, wenn die Sperren es erlauben; sonst bleibt
        // der Editor zu (gesperrte Fälle haben Knöpfe ohne Wirkung).
        match findEntry model entryId with
        | Some entry when model.DetailFor |> Option.exists (fun d -> d.Id = entryId)
                          && not entry.DeletedInYnab
                          && (SplitEditor.lockReasonFor entry).IsNone
                          && not (splitBusy model entryId) ->
            let loadTargets =
                match model.TransferTargets with
                | Success _ | Loading -> Cmd.none
                | _ -> Cmd.ofMsg RunLoadTransferTargets
            { model with
                SplitEditor = Some (SplitEditor.startState entry)
                CategoryPickerFor = None
                CategorySuggestions = []
                CategoryCard = None },
            Cmd.batch [ Cmd.ofMsg EnsureCategoriesFresh; loadTargets; Cmd.ofMsg LoadQuickAddAccount ],
            NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseSplitEditor ->
        match model.SplitEditor with
        | Some editor when not (splitBusy model editor.EntryId) -> { model with SplitEditor = None }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RunLoadTransferTargets ->
        { model with TransferTargets = Loading },
        Cmd.OfAsync.either
            Api.ledger.getBudgetAccounts
            ()
            (fun result -> TransferTargetsLoaded result)
            (fun ex -> TransferTargetsLoaded (Error ex.Message)),
        NoOp

    | TransferTargetsLoaded (Ok accounts) -> { model with TransferTargets = Success accounts }, Cmd.none, NoOp
    | TransferTargetsLoaded (Error message) -> { model with TransferTargets = Failure message }, Cmd.none, NoOp

    | AddSplitLine -> editSplit SplitEditor.addLine model, Cmd.none, NoOp
    | RemoveSplitLine index -> editSplit (SplitEditor.removeLine index) model, Cmd.none, NoOp
    | SetSplitLineAmount (index, text) -> editSplit (SplitEditor.setAmountText index text) model, Cmd.none, NoOp
    | SetSplitLineMemo (index, text) -> editSplit (SplitEditor.setMemo index text) model, Cmd.none, NoOp
    | FillSplitLineRest index -> editSplit (SplitEditor.fillRest index) model, Cmd.none, NoOp
    | StartSplitCashback target -> editSplit (SplitEditor.startCashback target) model, Cmd.none, NoOp

    | LoadQuickAddAccount ->
        model,
        Cmd.OfAsync.either
            Api.settings.getSettings
            ()
            (fun settings -> QuickAddAccountLoaded (Ok (settings.Ynab |> Option.bind (fun y -> y.QuickAddAccountId))))
            (fun ex -> QuickAddAccountLoaded (Error ex.Message)),
        NoOp

    | QuickAddAccountLoaded (Ok id) -> { model with QuickAddAccountId = id }, Cmd.none, NoOp
    // Ohne das Konto bleibt das Ziel der Übertrag-Zeile leer und wählbar; ein Fehler ist keinen Toast wert.
    | QuickAddAccountLoaded (Error _) -> model, Cmd.none, NoOp

    | OpenSplitCategoryPicker index ->
        editSplit (fun e -> { e with Picker = SplitEditor.SplitCategoryPickerFor index }) model, Cmd.ofMsg EnsureCategoriesFresh, NoOp

    | OpenSplitAccountPicker index ->
        editSplit (fun e -> { e with Picker = SplitEditor.SplitAccountPickerFor index }) model, Cmd.none, NoOp

    | CloseSplitPicker -> editSplit (fun e -> { e with Picker = SplitEditor.NoSplitPicker }) model, Cmd.none, NoOp

    | SelectSplitCategory (index, choice) ->
        // `None` ist "Ohne Kategorie": die Zeile bleibt als unkategorisierte Zeile.
        let target =
            match choice with
            | None -> Some Uncategorized
            | Some id -> categoryOfId model (Some id) |> Option.map (fun c -> ToCategory (c.Id, c.Name))
        match target with
        | Some t -> editSplit (SplitEditor.setTarget index t) model, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | SelectSplitAccount (index, accountId) ->
        match accountNameOf model accountId with
        | Some name -> editSplit (SplitEditor.setTarget index (ToTransfer (accountId, name))) model, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | StartSaveSplits ->
        match model.SplitEditor with
        | Some editor when not (splitBusy model editor.EntryId) ->
            match SplitEditor.requestLines editor with
            | Some lines ->
                { model with CategoryRows = model.CategoryRows |> Map.add editor.EntryId CategorySending },
                Cmd.ofMsg (RunSaveSplits (editor.EntryId, lines)),
                NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSaveSplits (entryId, lines) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntrySplits entryId)
                lines
                (fun result -> SplitsSaved (entryId, lines, result))
                (fun ex -> SplitsSaved (entryId, lines, Error ex.Message))
        model, cmd, NoOp

    | SplitsSaved (entryId, lines, Ok outcome) ->
        // Der Stand vor dem Speichern steht noch in der Liste: daraus kommt der Rückweg.
        let before = findEntry model entryId
        let model', cmd = applySplitResult entryId lines outcome model
        let external =
            match outcome.Change, outcome.WriteBack, before with
            | CategoryUnchanged, CategorySendFailed reason, _ ->
                ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | CategoryUnchanged, _, _ -> NoOp
            | CategoryChanged, writeBack, Some entry ->
                let text = SplitEditor.savedToastText lines
                let full =
                    match writeBack with
                    | CategorySendFailed reason -> $"{text}. {categoryHintTitle}: {reason}"
                    | _ -> text
                let tone = (match writeBack with CategorySendFailed _ -> ToastWarning | _ -> ToastSuccess)
                if SplitEditor.undoAvailable entry.SplitLocks lines then
                    let restore = SplitEditor.undoLines (SplitEditor.startState entry).Before entry.Amount
                    ShowToastWithAction (full, tone, { Label = SplitEditor.undoLabel; Kind = UndoSplit (entryId, restore) })
                else
                    ShowToast (full, tone)
            | CategoryChanged, _, None -> NoOp
        model', cmd, external

    | SplitsSaved (entryId, _, Error message) ->
        // Der Editor bleibt offen: der Entwurf geht nicht verloren.
        { model with CategoryRows = model.CategoryRows |> Map.remove entryId },
        Cmd.none,
        ShowToast ($"Aufteilung nicht gespeichert: {message}", ToastError)

    | StartUndoSplit (entryId, lines) ->
        { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending },
        Cmd.ofMsg (RunUndoSplit (entryId, lines)),
        NoOp

    | RunUndoSplit (entryId, lines) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntrySplits entryId)
                lines
                (fun result -> SplitsRestored (entryId, lines, result))
                (fun ex -> SplitsRestored (entryId, lines, Error ex.Message))
        model, cmd, NoOp

    | SplitsRestored (entryId, lines, Ok outcome) ->
        // Ohne neuen Toast-Knopf: eine Aktion, ein Toast, eine Rücknahme.
        let model', cmd = applySplitResult entryId lines outcome model
        let external =
            match outcome.WriteBack with
            | CategorySendFailed reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | _ when outcome.Change = CategoryUnchanged -> NoOp
            | _ -> ShowToast ("Aufteilung zurückgenommen", ToastSuccess)
        model', cmd, external

    | SplitsRestored (entryId, _, Error message) ->
        { model with CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed message) },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    | RunRefreshEntry entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.getLedgerEntry
            entryId
            (fun result -> EntryRefreshed (entryId, Ok result))
            (fun ex -> EntryRefreshed (entryId, Error ex.Message)),
        NoOp

    | EntryRefreshed (_, Ok (Some view)) -> replaceEntry view model, Cmd.none, NoOp
    | EntryRefreshed _ -> model, Cmd.none, NoOp

    | CategoryCardExpired version ->
        match model.CategoryCard with
        | Some card when card.Version = version -> { model with CategoryCard = None }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RunRefreshTotals ->
        match model.SelectedFilter with
        | None -> model, Cmd.none, NoOp
        | Some filter ->
            let accountId = match filter with AllAccounts -> None | OneAccount id -> Some id
            let query = { buildPageQuery model accountId 0 with LocallyRemoved = 0 }
            let cmd =
                Cmd.OfAsync.either
                    Api.ledger.getLedgerEntriesPage
                    query
                    (fun page -> TotalsRefreshed (Ok page))
                    (fun ex -> TotalsRefreshed (Error ex.Message))
            model, cmd, NoOp

    | TotalsRefreshed (Ok page) when model.OrderStale ->
        // Nur Trefferzahl und Summe: die Zeilen bleiben, wie sie sind, bis "Neu laden".
        { model with
            TotalCount = page.TotalCount
            TotalAmount = page.TotalAmount
            DeletedCount = page.DeletedCount
            NeedsReviewCount = page.NeedsReviewCount
            AskLenaCount = page.AskLenaCount },
        Cmd.none,
        NoOp

    | TotalsRefreshed _ -> model, Cmd.none, NoOp

    // ============================================
    // Auswahl mit Summe (ledger-r1d2f)
    // ============================================

    | StartSelectionMode entryId when model.FilteredOut.Contains entryId -> model, Cmd.none, NoOp

    | ToggleRowSelected entryId when model.FilteredOut.Contains entryId -> model, Cmd.none, NoOp

    | ClickRowCheckbox (entryId, _) when model.FilteredOut.Contains entryId -> model, Cmd.none, NoOp

    | StartSelectionMode entryId ->
        { model with
            SelectionMode = true
            Selection = Picked (Set.singleton entryId)
            SelectionAnchor = None },
        Cmd.none,
        NoOp

    | ToggleRowSelected entryId ->
        // Abwählen der letzten Zeile beendet den Modus nicht: nur "Auswahl aufheben" tut das.
        { model with Selection = toggleSelected entryId model.Selection }, Cmd.none, NoOp

    | ClickRowCheckbox (entryId, shift) ->
        let entries = model.Entries |> RemoteData.withDefault []
        let span =
            match shift, model.SelectionAnchor with
            | true, Some anchor -> rangeBetween entries anchor entryId
            | _ -> []
        if List.isEmpty span then
            // Einfacher Klick (oder Shift ohne brauchbaren Anker): wählt aus bzw. ab, setzt den Anker.
            { model with
                Selection = toggleSelected entryId model.Selection
                SelectionAnchor = Some entryId },
            Cmd.none,
            NoOp
        else
            { model with Selection = addToSelection span model.Selection }, Cmd.none, NoOp

    | ToggleSelectAll ->
        let selection = if isAllSelected model.Selection then NoSelection else AllExcept Set.empty
        { model with Selection = selection; SelectionAnchor = None }, Cmd.none, NoOp

    | ToggleHeaderCheckbox ->
        let selection =
            match headerCheckState model with
            | HeaderChecked -> NoSelection
            | HeaderEmpty | HeaderPartial -> AllExcept Set.empty
        { model with Selection = selection; SelectionAnchor = None }, Cmd.none, NoOp

    | ClearSelection ->
        { model with Selection = NoSelection; SelectionAnchor = None; SelectionMode = false }, Cmd.none, NoOp

    // ============================================
    // "Geprüft" für die ganze Auswahl (ledger-nhwrt)
    // ============================================

    | StartReviewMarkBatch ->
        // Nur mit Auswahl und ohne laufenden Aufruf; der Proxy-Aufruf steckt hinter `Run...`.
        match reviewMarkBatchSelection model with
        | Some selection when not model.ReviewMarkBatchRunning && not model.LenaBatchRunning ->
            { model with ReviewMarkBatchRunning = true }, Cmd.ofMsg (RunReviewMarkBatch selection), NoOp
        | _ -> model, Cmd.none, NoOp

    | RunReviewMarkBatch selection ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.setReviewMarkBatch
                selection
                ReviewMarkBatchDone
                (fun ex -> ReviewMarkBatchDone (Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkBatchDone (Ok outcome) ->
        let model', reload = applyReviewMarkBatch outcome model
        let toastType = reviewBatchToastType outcome
        let text = reviewBatchToastText outcome
        let external =
            if List.isEmpty outcome.Changed then
                ShowToast (text, toastType)
            else
                let items =
                    outcome.Changed |> List.map (fun c -> { EntryId = c.EntryId; Previous = c.Previous })
                ShowToastWithAction (text, toastType, { Label = undoToastLabel; Kind = UndoReviewMarkBatch items })
        model', (if reload then Cmd.ofMsg LoadEntries else Cmd.none), external

    | ReviewMarkBatchDone (Error message) ->
        { model with ReviewMarkBatchRunning = false },
        Cmd.none,
        ShowToast ($"Prüfvermerk nicht gesetzt: {message}", ToastError)

    | StartUndoReviewMarkBatch items -> model, Cmd.ofMsg (RunUndoReviewMarkBatch items), NoOp

    | RunUndoReviewMarkBatch items ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.restoreReviewMarkBatch
                items
                ReviewMarkBatchRestored
                (fun ex -> ReviewMarkBatchRestored (Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkBatchRestored (Ok outcome) ->
        // Viele der Buchungen waren unter `AllExcept` nie geladen: die Liste lädt neu, statt
        // Zeilen einzeln zurückzustecken. Das setzt auch Seitenversatz und Zähler auf den
        // Stand des Servers.
        let sendFailed =
            outcome.Changed
            |> List.tryPick (fun c ->
                match c.WriteBack with
                | ReviewMarkSendFailed reason -> Some reason
                | _ -> None)
        let external =
            match sendFailed with
            | Some reason -> ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastError)
            | None -> NoOp
        resetEntries model, Cmd.ofMsg LoadEntries, external

    | ReviewMarkBatchRestored (Error message) ->
        model, Cmd.none, ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    // ============================================
    // "Kategorie setzen" für die ganze Auswahl (ledger-w29y6)
    // ============================================

    | ToggleBatchCategoryPicker ->
        if model.BatchCategoryPickerOpen then
            { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp
        else
            // Nur mit nicht leerer Auswahl und ohne laufenden Sammelaufruf (einer sperrt beide
            // Aktionen der Leiste). Öffnen lädt die Kategorien bei Stand über zwei Minuten nach.
            match reviewMarkBatchSelection model with
            | Some _ when not model.ReviewMarkBatchRunning && not model.CategoryBatchRunning && not model.LenaBatchRunning ->
                { model with BatchCategoryPickerOpen = true; OpenPopover = None },
                Cmd.ofMsg EnsureCategoriesFresh,
                NoOp
            | _ -> model, Cmd.none, NoOp

    | CloseBatchCategoryPicker -> { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp

    | StartCategoryBatch target ->
        match reviewMarkBatchSelection model with
        | Some selection when not model.ReviewMarkBatchRunning && not model.CategoryBatchRunning && not model.LenaBatchRunning ->
            { model with
                CategoryBatchRunning = true
                BatchCategoryPickerOpen = false
                RecentCategories = rememberPickedCategory target model.RecentCategories },
            Cmd.batch [
                Cmd.ofMsg (RunCategoryBatch (selection, target))
                Cmd.ofEffect (fun _ -> writeRecentCategories (rememberPickedCategory target model.RecentCategories))
            ],
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunCategoryBatch (selection, target) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryCategoryBatch selection)
                target
                (fun result -> CategoryBatchDone (target, result))
                (fun ex -> CategoryBatchDone (target, Error ex.Message))
        model, cmd, NoOp

    | CategoryBatchDone (target, Ok outcome) ->
        let category = categoryOfId model target
        let targetChoice =
            target
            |> Option.map (fun id ->
                { CategoryId = id
                  CategoryName = category |> Option.map (fun c -> c.Name) |> Option.defaultValue "" })
        let model', cmd = applyCategoryBatch targetChoice outcome model
        let text = categoryBatchToastText (category |> Option.map (fun c -> c.Name)) outcome
        let toastType = categoryBatchToastType outcome
        let external =
            if List.isEmpty outcome.Changed then
                ShowToast (text, toastType)
            else
                ShowToastWithAction (text, toastType, { Label = undoToastLabel; Kind = UndoCategoryBatch outcome.Changed })
        model', cmd, external

    | CategoryBatchDone (_, Error message) ->
        { model with CategoryBatchRunning = false },
        Cmd.none,
        ShowToast ($"Kategorie nicht geändert: {message}", ToastError)

    | StartUndoCategoryBatch changes ->
        if model.CategoryBatchRunning || model.ReviewMarkBatchRunning || model.LenaBatchRunning then
            model, Cmd.none, NoOp
        else
            { model with CategoryBatchRunning = true },
            Cmd.ofMsg (RunUndoCategoryBatch (categoryBatchUndoGroups changes)),
            NoOp

    | RunUndoCategoryBatch groups ->
        // Ein Sammel-Befehl je Gruppe gleicher vorheriger Kategorie, nacheinander (die
        // Schreib-Sperre des Servers ordnet sie ohnehin), eine Antwort für den einen Toast.
        let run () =
            async {
                let results = ResizeArray<CategoryChoice option * Result<CategoryBatchOutcome, string>>()
                for (previous, ids) in groups do
                    let! result =
                        Api.ledger.setEntryCategoryBatch (BatchPicked ids) (previous |> Option.map (fun p -> p.CategoryId))
                    results.Add((previous, result))
                return List.ofSeq results
            }
        let cmd =
            Cmd.OfAsync.either
                run
                ()
                CategoryBatchRestored
                (fun ex -> CategoryBatchRestored (groups |> List.map (fun (previous, _) -> previous, Error ex.Message)))
        model, cmd, NoOp

    | CategoryBatchRestored results ->
        // Ohne neue Aktion: eine Aktion, ein Toast, eine Rücknahme.
        let ended = { model with CategoryBatchRunning = false }
        let model', cmd =
            results
            |> List.fold
                (fun (m: Model, cmds: Cmd<Msg> list) (previous, result) ->
                    match result with
                    | Ok outcome ->
                        let m', c = applyCategoryBatch previous outcome m
                        m', cmds @ [ c ]
                    | Error _ -> m, cmds)
                (ended, [])
            |> fun (m, cmds) -> m, Cmd.batch cmds
        let outcomes = results |> List.choose (fun (_, r) -> match r with Ok o -> Some o | Error _ -> None)
        let firstError = results |> List.tryPick (fun (_, r) -> match r with Error e -> Some e | Ok _ -> None)
        let restored = outcomes |> List.sumBy (fun o -> List.length o.Changed)
        let firstFailure = outcomes |> List.tryPick (fun o -> categoryBatchSendFailures o |> List.tryHead)
        let external =
            match firstError, firstFailure with
            | Some message, _ -> ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)
            | None, Some reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | None, None when restored = 0 -> NoOp
            | None, None ->
                let noun = if restored = 1 then "Buchung" else "Buchungen"
                ShowToast ($"Kategorie bei {Client.DesignSystem.Money.formatCount restored} {noun} zurückgesetzt", ToastSuccess)
        model', cmd, external

    // ============================================
    // "Lena fragen" für die ganze Auswahl (ledger-y54gr)
    // ============================================

    | StartLenaBatch target ->
        // Wie die anderen Sammelaktionen: nur mit Auswahl und ohne laufenden Sammelaufruf; "Lena
        // fragen" zusätzlich nur mit gesetzter Flag-Farbe. Der Proxy-Aufruf steckt hinter `Run...`.
        let blocked =
            model.ReviewMarkBatchRunning
            || model.CategoryBatchRunning
            || model.LenaBatchRunning
            || (target = AssignLena && (selectionLenaDisabledReason model.LenaFlagColor).IsSome)
        match reviewMarkBatchSelection model with
        | Some selection when not blocked ->
            { model with LenaBatchRunning = true; BatchCategoryPickerOpen = false },
            Cmd.ofMsg (RunLenaBatch (selection, target)),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunLenaBatch (selection, target) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setLenaFlagBatch selection)
                target
                (fun result -> LenaBatchDone (target, result))
                (fun ex -> LenaBatchDone (target, Error ex.Message))
        model, cmd, NoOp

    | LenaBatchDone (target, Ok outcome) ->
        let newState =
            match target with
            | AssignLena -> { Color = model.LenaFlagColor; Name = Some lenaFlagName }
            | WithdrawLena -> { Color = None; Name = None }
        let model' = applyFlagBatch (fun _ -> newState) true outcome model
        let text = flagBatchToastText target outcome
        let toastType = flagBatchToastType outcome
        let external =
            if List.isEmpty outcome.Changed then
                ShowToast (text, toastType)
            else
                ShowToastWithAction (text, toastType, { Label = undoToastLabel; Kind = UndoFlagBatch outcome.Changed })
        model', Cmd.none, external

    | LenaBatchDone (_, Error message) ->
        { model with LenaBatchRunning = false },
        Cmd.none,
        ShowToast ($"Flag nicht geändert: {message}", ToastError)

    | StartUndoFlagBatch changes ->
        if model.LenaBatchRunning || model.CategoryBatchRunning || model.ReviewMarkBatchRunning then
            model, Cmd.none, NoOp
        else
            let items = changes |> List.map (fun (c: FlagBatchChange) -> ({ EntryId = c.EntryId; Previous = c.Previous }: FlagRestoreItem))
            { model with LenaBatchRunning = true }, Cmd.ofMsg (RunUndoFlagBatch items), NoOp

    | RunUndoFlagBatch items ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.restoreFlagBatch
                items
                (fun result -> FlagBatchRestored (items, result))
                (fun ex -> FlagBatchRestored (items, Error ex.Message))
        model, cmd, NoOp

    | FlagBatchRestored (items, Ok outcome) ->
        // Ohne neue Aktion: eine Aktion, ein Toast, eine Rücknahme.
        let previousById = items |> List.map (fun i -> i.EntryId, i.Previous) |> Map.ofList
        let newStateOf id = previousById |> Map.tryFind id |> Option.defaultValue { Color = None; Name = None }
        let model' = applyFlagBatch newStateOf false outcome model
        let external =
            match flagBatchSendFailures outcome |> List.tryHead with
            | Some reason -> ShowToast ($"{flagHintTitle}: {reason}", ToastWarning)
            | None when List.isEmpty outcome.Changed -> NoOp
            | None -> ShowToast (flagBatchRestoredToastText (List.length outcome.Changed), ToastSuccess)
        model', Cmd.none, external

    | FlagBatchRestored (_, Error message) ->
        { model with LenaBatchRunning = false },
        Cmd.none,
        ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    // ============================================
    // Tastaturfokus und Taste G (ledger-rjb8w, PC)
    // ============================================

    | MoveFocus delta ->
        if keyboardBlocked model then
            model, Cmd.none, NoOp
        else
            let entries = model.Entries |> RemoteData.withDefault []
            // Ein offenes Panel folgt der Markierung und lädt die Historie der neuen Buchung.
            followFocusWithPanel (
                { model with FocusedRow = movedFocus entries model.FocusedRow delta }, Cmd.none, NoOp)

    | ReviewKeyPressed target ->
        let entries = model.Entries |> RemoteData.withDefault []
        let focused =
            model.FocusedRow
            |> Option.bind (fun id -> entries |> List.tryFind (fun e -> e.Id = id))
        match focused with
        // Im Auswahl-Modus ist G aus: unklar, ob es die Fokuszeile oder die Auswahl meint.
        | Some entry when not (keyboardBlocked model) && not (reviewKeyBlocked model entry) ->
            // Derselbe Weg wie der Klick auf die Schaltfläche (Fehlschlag-Hinweis inbegriffen);
            // der Fokus springt sofort auf die nächste Zeile.
            updateCore (StartSetReviewMark (entry.Id, target)) { model with FocusedRow = Some (nextFocusAfter entries entry.Id) }
            |> followFocusWithPanel
        | _ -> model, Cmd.none, NoOp

    | OpenFocusedDetail ->
        let entries = model.Entries |> RemoteData.withDefault []
        let focused =
            model.FocusedRow
            |> Option.bind (fun id -> entries |> List.tryFind (fun e -> e.Id = id))
        match focused with
        | Some entry when not (keyboardBlocked model) && not model.SelectionMode && not (hasSelection model) ->
            let model', cmd = showDetail entry model
            model', cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | ToggleFocusedSelected ->
        // Wie ein Klick auf das Kästchen der markierten Zeile, mit Anker. Ein offenes Popover
        // oder Sheet gehört der Tastatur; ein offenes Panel nicht (dann schließt die beginnende
        // Auswahl es in `update`).
        match model.FocusedRow with
        | Some id when not (keyboardBlocked model) -> updateCore (ClickRowCheckbox (id, false)) model
        | _ -> model, Cmd.none, NoOp

    | EscapePressed ->
        // Ein Esc schließt genau eine Stufe: Popover, dann Detail, dann Auswahl. Liegt der
        // Fokus im Popover, behandelt es Esc selbst und das Ereignis erreicht das Dokument
        // nicht; liegt er auf dem Chip, schließt diese Stufe es.
        if model.OpenPopover.IsSome then
            { model with OpenPopover = None }, Cmd.none, NoOp
        elif model.BatchCategoryPickerOpen then
            { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp
        elif model.SplitEditor.IsSome then
            // Erst die Auswahl über dem Editor, dann der Editor selbst (Entwurf verworfen).
            match model.SplitEditor with
            | Some editor when editor.Picker <> SplitEditor.NoSplitPicker -> updateCore CloseSplitPicker model
            | _ -> updateCore CloseSplitEditor model
        elif model.CategoryPickerFor.IsSome then
            { model with CategoryPickerFor = None; CategorySuggestions = [] }, Cmd.none, NoOp
        elif model.MemoEdit.IsSome then
            updateCore CancelMemoEdit model
        elif model.DetailFor.IsSome then
            { model with DetailFor = None; History = NotAsked; MemoEdit = None }, Cmd.none, NoOp
        elif model.SelectionMode || hasSelection model then
            updateCore ClearSelection model
        else
            model, Cmd.none, NoOp

/// Eine beginnende Auswahl (von leer auf nicht leer) schließt ein offenes Panel oder Sheet
/// (ADR 0053 Punkt 4, ledger-w4vte). Der Vergleich von Zustand vor und nach dem Schritt
/// deckt jeden Weg ab: Kästchen, Umschalt-Klick, Leertaste, Kopfzeile, "Alle" und das lange
/// Drücken am Handy.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let model', cmd, external = updateCore msg model
    let model' =
        if model'.DetailFor.IsSome && not (hasSelection model) && hasSelection model' then
            { model' with DetailFor = None; History = NotAsked; MemoEdit = None }
        else
            model'
    // Der Picker der Auswahl gehört zur Auswahl: endet sie (Aufheben, Esc, Konto- oder
    // Filterwechsel), endet er mit ihr (ledger-w29y6).
    let model' =
        if model'.BatchCategoryPickerOpen && not (hasSelection model') then
            { model' with BatchCategoryPickerOpen = false }
        else
            model'
    // Der Split-Editor gehört ebenfalls zur Buchung im offenen Detail; ohne sie ist der Entwurf verworfen.
    let model' =
        match model'.SplitEditor with
        | Some editor when not (model'.DetailFor |> Option.exists (fun d -> d.Id = editor.EntryId)) ->
            { model' with SplitEditor = None }
        | _ -> model'
    // Picker und Karte gehören zur Buchung im offenen Detail: schließt das Detail oder wechselt
    // es auf eine andere Buchung (Klick, Pfeiltaste, Esc, Kontowechsel), enden beide.
    let detailId = model'.DetailFor |> Option.map (fun d -> d.Id)
    let pickerStays = model'.CategoryPickerFor.IsNone || model'.CategoryPickerFor = detailId
    let cardStays = model'.CategoryCard |> Option.forall (fun c -> Some c.EntryId = detailId)
    if pickerStays && cardStays then
        model', cmd, external
    else
        { model' with
            CategoryPickerFor = (if pickerStays then model'.CategoryPickerFor else None)
            CategorySuggestions = (if pickerStays then model'.CategorySuggestions else [])
            CategoryCard = (if cardStays then model'.CategoryCard else None) },
        cmd,
        external
