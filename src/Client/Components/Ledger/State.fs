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
module ListKeys = Client.DesignSystem.ListKeys

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
        BalancesGeneration = 0
        BalancesRefreshError = None
        SelectedFilter = None
        RememberedFilter = readRememberedFilter ()
        AccountSheetOpen = false
        Entries = NotAsked
        NextPage = 0
        LocallyRemoved = 0
        TotalCount = 0
        TotalAmount = { Amount = 0m; Currency = "EUR" }
        HasMoreEntries = false
        IncludeDeleted = false
        DeletedCount = 0
        OnlyNeedsReview = false
        Sort = LedgerSort.defaultSort
        SortSheetOpen = false
        NeedsReviewCount = 0
        PendingRouteEntry = None
        DetailFor = None
        DetailForm = DetailSheet
        MemoEdit = None
        MemoRows = Map.empty
        AmountEdit = None
        AmountRows = Map.empty
        PayeeEdit = None
        PayeeRows = Map.empty
        PayeeChoices = NotAsked
        PayeeChoicesGeneration = 0
        ListInteraction = TableRows
        History = NotAsked
        HistoryGeneration = 0
        HistoryOpenFor = None
        LastMirrorRun = NotAsked
        MirrorStatusGeneration = 0
        MirrorRunning = false
        MirrorInBackground = false
        BookingsScroll = None
        Categories = NotAsked
        CategoriesLoadedAt = None
        CategoriesFetch = CategoriesIdle
        CategoriesGeneration = 0
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
        SearchDraft = ""
        SearchVersion = 0
        FilterSheetOpen = false
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
        BatchRunning = None
        FocusedRow = None
        CategoryPickerFor = None
        SplitEditor = None
        QuickAddAccount = NotAsked
        TransferTargets = NotAsked
        Capture = None
        DiscardingCaptured = Set.empty
        DeletingEntries = Set.empty
        RestoringEntries = Set.empty
        DeletedRow = None
        DeleteFailure = None
        CategorySuggestions = []
        CategoryRows = Map.empty
        FlagRows = Map.empty
        LenaFlagColor = NotAsked
        AskLenaCount = 0
        WithoutCategoryCount = 0
        CategoryCard = None
        CategoryCardVersion = 0
        RuleOffer = None
        DetailNote = None
        DetailNoteVersion = 0
        FilteredOut = Set.empty
        OrderStale = false
        EntriesGeneration = 0
        RefreshGeneration = 0
        ListActivity = ListIdle
        Review = None
        ReviewGeneration = 0
        RestoringDuplicates = Set.empty
    }
    model, Cmd.none

/// Der Zustand einer frisch angestossenen Buchungsliste: Seite 0, nichts geladen.
/// Grundlage von `rebuildEntries` (Neuaufbau) und den Vorbelegungen ohne eigene Abfrage. Eine neue
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
        SwipedAway = Map.empty
        Selection = NoSelection
        SelectionAnchor = None
        SelectionMode = false
        // Die Zeile am Platz einer gelöschten Buchung gehört zur alten Liste (ledger-k68sd).
        DeletedRow = None
        // Eine neu aufgesetzte Liste kennt weder ausgegraute Zeilen noch eine veraltete
        // Reihenfolge (ledger-sfab0).
        FilteredOut = Set.empty
        OrderStale = false
        // Laufende Abfragen der alten Liste verlieren hier ihre Gültigkeit (infrastructure-r3tw7).
        // Die Antwort der neuen Liste läuft unabhängig davon.
        EntriesGeneration = model.EntriesGeneration + 1
        // „Weitere laden“ und ein laufendes oder wartendes Nachladen gehören zur alten Liste (ledger-ct3cx).
        ListActivity = ListIdle
}

/// Die Antwort von „Weitere laden“ beendet es; ein Nachladen im Hintergrund läuft weiter.
let private afterPage (activity: ListActivity) : ListActivity =
    match activity with
    | LoadingMorePage | LoadingMoreThenRefresh -> ListIdle
    | ListIdle | Refreshing _ -> activity

/// Wartete ein Nachladen auf die Seite, startet es jetzt (ledger-ct3cx).
let private refreshAfterPage (activity: ListActivity) : Cmd<Msg> =
    match activity with
    | LoadingMoreThenRefresh -> Cmd.ofMsg RefreshEntries
    | ListIdle | LoadingMorePage | Refreshing _ -> Cmd.none

/// Neuaufbau der Liste (ledger-ct3cx): Seite 0, Auswahl leer, Skeleton, genau eine Abfrage der
/// Seite 0. Nur bei geänderter Abfrage (Konto, Filter, Chip, Sortierung, „Gelöschte zeigen“), ohne
/// geladene Liste und beim Betreten der Seite; sonst lädt die Liste im Hintergrund nach.
let private rebuildEntries (model: Model) : Model * Cmd<Msg> =
    resetEntries model, Cmd.ofMsg (RunLoadEntriesPage 0)

/// `rebuildEntries` als Ergebnis eines `update`-Zweigs ohne Nachricht nach außen.
let private rebuildWith (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let rebuilt, cmd = rebuildEntries model
    rebuilt, cmd, NoOp

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

/// Der Sortier-Chip ist aktiv gefärbt, wenn die wirksame Sortierung vom Standard abweicht.
let sortChipActive (sort: LedgerSort) : bool = sort <> LedgerSort.defaultSort

/// Die wirksame Sortierung (ADR 0051 Punkt 9): die Spalte "Konto" gibt es nur in der
/// Gesamtsicht. Ist `SortByAccount` gewählt und ein einzelnes Konto aktiv, gilt
/// `LedgerSort.defaultSort`; das Modell behält die Wahl für die Rückkehr in die Gesamtsicht.
let effectiveSort (model: Model) : LedgerSort =
    match model.Sort.Field, model.SelectedFilter with
    | SortByAccount, Some (OneAccount _) -> LedgerSort.defaultSort
    | _ -> model.Sort

/// Die Sortierung in Worten für die Zeile „Sortieren“ im Filter-Sheet und den Chip (ledger-d9z3n):
/// Feld und Richtung wie im Sortier-Sheet, etwa „Datum, neueste zuerst“ oder „Payee, A bis Z“.
let sortValueText (sort: LedgerSort) : string =
    let label =
        sortOptions true
        |> List.tryFind (fun (option, _) -> option = sort)
        |> Option.map snd
        |> Option.defaultValue ""
    // „Neueste zuerst“ wird klein, „A bis Z“ bleibt, wie es ist.
    let lowered =
        if label.Length > 1 && label.[1] <> ' ' then string (Char.ToLowerInvariant label.[0]) + label.Substring 1
        else label
    $"{sortFieldName sort.Field}, {lowered}"

/// Der Suchtext als Filter: getrimmt, leer heißt keine Suche (ledger-d9z3n).
let searchOfText (text: string) : string option =
    let trimmed = if isNull text then "" else text.Trim()
    if trimmed = "" then None else Some trimmed

/// Die gesetzten Filter als Chips unter Suche und Filter-Knopf (ledger-d9z3n). Kategorienamen kommen
/// aus den geladenen Kategorien in ihrer Reihenfolge; eine Sortierung zählt nur, wenn die wirksame
/// vom Standard abweicht.
let activeFilters (model: Model) : (ActiveFilter * string) list =
    let categories = model.Categories |> RemoteData.toOption |> Option.defaultValue []
    let names = selectedCategoryNames categories model.Filters.CategoryIds
    let sort = effectiveSort model
    let sortLabel = if sort <> LedgerSort.defaultSort then Some (sortValueText sort) else None
    activeFilterChips model.OnlyNeedsReview model.IncludeDeleted sortLabel names model.Filters

/// Was das „×“ eines Chips schickt: genau dieser Filter fällt weg, die übrigen bleiben.
let removeActiveFilterMsg (filter: ActiveFilter) : Msg =
    match filter with
    | ActiveFilter.NeedsReview -> ToggleOnlyNeedsReview
    | ActiveFilter.AskLena -> ToggleOnlyAskLena
    | ActiveFilter.WithoutCategory -> ToggleWithoutCategory
    | ActiveFilter.Category -> RemoveCategoryFilter
    | ActiveFilter.Date -> RemoveDateFilter
    | ActiveFilter.Amount -> RemoveAmountFilter
    | ActiveFilter.Direction -> SetAmountDirection AllDirections
    | ActiveFilter.Sort -> SetSortExactly LedgerSort.defaultSort
    | ActiveFilter.WithDeleted -> ToggleIncludeDeleted

/// „Filter löschen“ am Chip und im Filter-Sheet (ledger-d9z3n): alle Filter fallen weg, auch „Zu prüfen“,
/// „Lena“, „Ohne Kategorie“, Sortierung, „Gelöschte zeigen“ und die Suche. Das Konto bleibt. Die
/// Versionen steigen, damit eine wartende Eingabe den Filter nicht zurückholt (ledger-ct3cx). Rein,
/// ohne Neuaufbau (den macht `update`).
let clearAllFilters (model: Model) : Model =
    { model with
        Filters = noFilters
        OnlyNeedsReview = false
        IncludeDeleted = false
        Sort = LedgerSort.defaultSort
        SearchDraft = ""
        SearchVersion = model.SearchVersion + 1
        AmountMinDraft = ""
        AmountMaxDraft = ""
        AmountVersion = model.AmountVersion + 1 }

/// Die Seitenanfrage an den Server. Rein, damit der Versatz testbar ist. Die
/// Such-Filter (Kategorie/Zeitraum/Betrag, ledger-vgfw3) und die freie Suche (ledger-d9z3n)
/// sind Server-seitig da (ADR 0036). `LocallyRemoved` stammt aus dem Modell: jede Aktion, die Zeilen lokal
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
    Search = model.Filters.Search
    AmountMin = model.Filters.AmountMin
    AmountMax = model.Filters.AmountMax
    AmountDirection = model.Filters.AmountDirection
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

/// Ergebnis von "Rückgängig" für mehrere Prüfvermerke (Wische hintereinander oder die Auswahl, ledger-gwr9j): jede
/// geänderte geladene Zeile bekommt ihren Vorzustand, weggewischte kommen an ihren Platz zurück, die zuletzt
/// gewischte zuerst. Die Liste bleibt geladen, damit sie an ihrer Stelle stehen bleibt. Rein.
let private applyUndoReviewMarkBatch
    (items: ReviewMarkRestoreItem list)
    (outcome: ReviewMarkBatchOutcome)
    (model: Model)
    : Model =
    let changeById = outcome.Changed |> List.map (fun c -> c.EntryId, c) |> Map.ofList
    items
    |> List.rev
    |> List.fold
        (fun current item ->
            match changeById |> Map.tryFind item.EntryId with
            | Some change ->
                let failureOf (entry: LedgerEntryView) =
                    match change.WriteBack with
                    | ReviewMarkSendFailed reason -> Some reason
                    | ReviewMarkSent -> None
                    | ReviewMarkNotSent -> entry.ReviewMarkWriteBackFailure
                reinsertSwipedAway item.EntryId current
                |> applyReviewMarkResult item.EntryId (Some (needsReviewOf item.Previous)) failureOf
            | None -> current)
        model

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

/// Die Antwort einer Neuholung, gebunden an die Zeile beim Start der Abfrage. Rein, weil der
/// Abfrage-Zweig unter .NET nicht läuft (infrastructure-r3tw7).
let entryRefreshAnswer (model: Model) (entryId: Guid) : Result<LedgerEntryView option, string> -> Msg =
    let basis = findEntry model entryId
    fun result -> EntryRefreshed (entryId, basis, result)

/// Die Antwort einer Summen-Abfrage, gebunden an die Liste beim Start der Abfrage.
let totalsRefreshAnswer (model: Model) : Result<LedgerEntryPage, string> -> Msg =
    let generation = model.EntriesGeneration
    fun result -> TotalsRefreshed (generation, result)

/// Die Antwort einer Seiten-Abfrage, gebunden an die Liste beim Start der Abfrage. Erfolg und
/// Scheitern tragen dieselbe Generation: eine verspätete Wiederholung der alten Abfrage ersetzt so
/// die neuere Liste nicht (infrastructure-r3tw7).
let entriesPageAnswer (model: Model) (page: int) : Result<LedgerEntryPage, string> -> Msg =
    let generation = model.EntriesGeneration
    fun result -> EntriesPageLoaded (generation, page, result)

/// Die Antwort eines Nachladens im Hintergrund (ledger-ct3cx), gebunden an die Liste und an das
/// Nachladen beim Start. Ein Neuaufbau oder ein späteres Nachladen macht sie ungültig.
let entriesRefreshAnswer (model: Model) : Result<LedgerEntryPage, string> -> Msg =
    let generation = model.EntriesGeneration
    let refresh = model.RefreshGeneration
    fun result -> EntriesRefreshed (generation, refresh, result)

let private accountIdOf (filter: AccountFilter) : Guid option =
    match filter with
    | AllAccounts -> None
    | OneAccount id -> Some id

/// Anfrage und Antwort einer Seite, wie `RunLoadEntriesPage` sie absetzt. Rein; `None` ohne Konto-Filter.
let pageRequest (model: Model) (page: int) : (LedgerEntryPageQuery * (Result<LedgerEntryPage, string> -> Msg)) option =
    model.SelectedFilter
    |> Option.map (fun filter -> buildPageQuery model (accountIdOf filter) page, entriesPageAnswer model page)

/// Anfrage und Antwort des Nachladens im Hintergrund, wie `RunRefreshEntries` sie absetzt (ledger-ct3cx):
/// dieselbe Filterung und Sortierung ab Zeile 0, so viele Zeilen wie die geladenen Seiten fassen, ohne
/// Abzug lokal entfernter Zeilen. `None` ohne Konto-Filter oder wenn kein Nachladen mehr läuft. Rein.
let refreshRequest (model: Model) : (LedgerEntryPageQuery * (Result<LedgerEntryPage, string> -> Msg)) option =
    match model.SelectedFilter with
    | Some filter when refreshRunning model ->
        let query =
            { buildPageQuery model (accountIdOf filter) 0 with
                PageSize = max 1 model.NextPage * pageSize
                LocallyRemoved = 0 }
        Some (query, entriesRefreshAnswer model)
    | _ -> None

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
        // Eine ausgegraute Buchung trägt kein Häkchen: unter `Picked` verlässt sie die Auswahl, sonst träfe eine
        // Sammelaktion sie unsichtbar. Unter `AllExcept` gehört sie nicht mehr zum Treffer; eine Ausnahme bleibt
        // stehen, damit eine Rücknahme sie nicht wieder wählt (`selectionTotals` zählt sie nicht doppelt ab).
        let selection =
            match model.Selection with
            | Picked _ -> removeFromSelection updated.Id model.Selection
            | other -> other
        let m =
            { model with
                Selection = selection
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
        // Eine Fortschreibung macht jede vorher gestartete Antwort alt (ledger-pfhdq): sie fällt weg.
        let shifted =
            match cleared.Categories with
            | Success list ->
                { cleared with
                    Categories = Success (shiftAvailable args list)
                    CategoriesGeneration = cleared.CategoriesGeneration + 1 }
            | _ -> cleared
        let version = cleared.CategoryCardVersion + 1
        let hasCard = showCard && not (List.isEmpty lines)
        let withEntry = replaceEntry updated shifted
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

/// Nach einer gelungenen Kategorie-Änderung im Detail (ledger-hg5m5): Payee, Memo und Kategorie für die Frage an den
/// Server, ob die Regeln das schon so zuordnen. Nicht ohne Kategorie, ohne Payee, bei Aufteilung oder Übertrag und
/// nicht auf der Karte des Durchgehens (dort steht kein Detail).
let ruleOfferCheckFor (model: Model) (entryId: Guid) (newId: YnabCategoryId option) : (string * string * YnabCategoryId) option =
    match findEntry model entryId, newId with
    | Some entry, Some categoryId when detailShows model entryId && ruleCategoryOf entry = Some categoryId ->
        rulePayeeOf entry |> Option.map (fun payee -> payee, entry.Memo |> Option.defaultValue "", categoryId)
    | _ -> None

/// Nach dem Weg zurück vom Übertrag zur normalen Buchung (ledger-qvjvs): die Zeile ist lokal
/// kein Übertrag mehr, den Payee aus BB liefert das Nachladen der Buchung.
let private afterTransferCleared (entryId: Guid) (outcome: CategoryChangeOutcome) (model: Model, cmd: Cmd<Msg>) : Model * Cmd<Msg> =
    match outcome.PreviousTransferAccountId, outcome.Change with
    | Some _, CategoryChanged ->
        let model' =
            match findEntry model entryId with
            | Some entry -> replaceEntry { entry with IsTransfer = false; TransferAccountId = None } model
            | None -> model
        model', Cmd.batch [ cmd; Cmd.ofMsg (RunRefreshEntry entryId) ]
    | _ -> model, cmd

/// Die Rücknahme im Toast nach einem Befehl auf der Kategorie-Zeile (ledger-qvjvs): war die
/// Buchung vorher ein Übertrag, wird sie wieder einer, sonst geht die alte Kategorie zurück.
let undoKindFor (entryId: Guid) (outcome: CategoryChangeOutcome) : ToastActionKind =
    match outcome.PreviousTransferAccountId with
    | Some account -> UndoTransfer (entryId, account)
    | None -> UndoCategory (entryId, outcome.Previous)

/// Trägt das Ergebnis von `setEntryTransfer` lokal ein (ledger-qvjvs): Übertrag auf das Konto,
/// ohne Kategorie, Payee wie YNAB ihn nennt. Das Nachladen der Buchung bestätigt den Stand.
let applyTransferResult (entryId: Guid) (YnabAccountId account) (outcome: CategoryChangeOutcome) (model: Model) : Model =
    let cleared =
        { model with
            CategoryRows = model.CategoryRows |> Map.remove entryId
            CategoryPickerFor = None
            CategorySuggestions = [] }
    match findEntry cleared entryId with
    | None -> cleared
    | Some entry ->
        let failure =
            match outcome.WriteBack with
            | CategorySendFailed reason -> Some reason
            | CategorySent -> None
            | CategoryNotSent -> entry.CategorizationWriteBackFailure
        match outcome.Change with
        | CategoryUnchanged -> replaceEntry { entry with CategorizationWriteBackFailure = failure } cleared
        | CategoryChanged ->
            let name = transferAccountName cleared account
            replaceEntry
                { entry with
                    IsTransfer = true
                    TransferAccountId = Some account
                    CategoryId = None
                    CategoryName = None
                    CategoryGroupName = None
                    Payee = name |> Option.map (fun n -> $"Transfer : {n}") |> Option.orElse entry.Payee
                    CategorizationWriteBackFailure = failure }
                cleared

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

/// Die Antwort auf das Laden der Historie: Buchung und Lade-Nummer beim Start sind gebunden
/// (ledger-nc708). Rein, damit Tests die Antworten eines bestimmten Ladens bauen können.
let historyAnswer (entryId: Guid) (generation: int) : Result<LedgerHistoryLine list, string> -> Msg =
    fun result -> HistoryLoaded (entryId, generation, result)

/// Gehört die Antwort zum zuletzt gestarteten Laden der offenen Buchung?
let private isCurrentHistory (entryId: Guid) (generation: int) (model: Model) : bool =
    generation = model.HistoryGeneration && model.DetailFor |> Option.exists (fun e -> e.Id = entryId)

/// Nach einer erfolgreichen Änderung an einer Buchung (ledger-nc708): steht sie im Detail, holt es ihre
/// Historie neu, damit die neue Zeile dort steht. Im Durchgehen zählt das Detail nur, wenn „Details“
/// offen ist; die Karte allein zeigt keine Historie.
let private afterEntryChange (entryId: Guid) (model: Model) : Cmd<Msg> =
    let shown = model.Review |> Option.forall (fun r -> r.DetailShown)
    if shown && model.DetailFor |> Option.exists (fun d -> d.Id = entryId) then Cmd.ofMsg (LoadHistory entryId)
    else Cmd.none

/// Der offene Fehlschlag nach der Antwort von `setEntryAmount` (Muster `memoFailureOf`).
let private amountFailureOf (outcome: AmountOutcome) (entry: LedgerEntryView) : string option =
    match outcome.AmountWriteBack with
    | AmountSendFailed reason -> Some reason
    | AmountSent -> None
    | AmountNotSent -> if outcome.AmountChange = AmountUnchanged then entry.AmountWriteBackFailure else None

/// Trägt das Ergebnis von `setEntryAmount` in den Zustand ein (ADR 0092): die Zeile bekommt den
/// Betrag und den Zustand des Zurückschreibens, das Sheet dieser Buchung schließt. Rein.
let private applyAmountResult (entryId: Guid) (outcome: AmountOutcome) (model: Model) : Model =
    let cleared =
        { model with
            AmountRows = model.AmountRows |> Map.remove entryId
            AmountEdit = model.AmountEdit |> Option.filter (fun edit -> edit.AmountEntryId <> entryId) }
    match findEntry cleared entryId with
    | None -> cleared
    | Some entry ->
        replaceEntry
            { entry with
                Amount = outcome.CurrentAmount
                AmountWriteBackFailure = amountFailureOf outcome entry }
            cleared

/// Der offene Fehlschlag nach der Antwort von `setEntryPayee` (Muster `memoFailureOf`).
let private payeeFailureOf (outcome: PayeeOutcome) (entry: LedgerEntryView) : string option =
    match outcome.PayeeWriteBack with
    | PayeeSendFailed reason -> Some reason
    | PayeeSent -> None
    | PayeeNotSent -> if outcome.PayeeChange = PayeeUnchanged then entry.PayeeWriteBackFailure else None

/// Trägt das Ergebnis von `setEntryPayee` in den Zustand ein (ledger-fxt09): die Zeile bekommt den
/// Payee und den Zustand des Zurückschreibens, die Auswahl dieser Buchung schließt. Nach einer
/// Änderung laden die Payees der Auswahl beim nächsten Öffnen neu. Rein.
let applyPayeeResult (entryId: Guid) (outcome: PayeeOutcome) (model: Model) : Model =
    let cleared =
        { model with
            PayeeRows = model.PayeeRows |> Map.remove entryId
            PayeeEdit = model.PayeeEdit |> Option.filter (fun edit -> edit.PayeeEntryId <> entryId)
            PayeeChoices = (if outcome.PayeeChange = PayeeChanged then NotAsked else model.PayeeChoices)
            PayeeChoicesGeneration =
                if outcome.PayeeChange = PayeeChanged then model.PayeeChoicesGeneration + 1 else model.PayeeChoicesGeneration }
    match findEntry cleared entryId with
    | None -> cleared
    | Some entry ->
        replaceEntry
            { entry with
                Payee = outcome.CurrentPayee
                PayeeWriteBackFailure = payeeFailureOf outcome entry }
            cleared

/// Die Antwort der Payee-Auswahl, gebunden an die Lade-Nummer beim Start der Abfrage. Eine Änderung
/// zählt die Nummer weiter: eine Antwort von davor setzt die alte Liste nicht zurück.
let payeeChoicesAnswer (model: Model) : Result<LedgerPayeeSuggestion list, string> -> Msg =
    let generation = model.PayeeChoicesGeneration
    fun result -> PayeeChoicesLoaded (generation, result)

/// Die Abfrage der Payee-Auswahl: das ganze Kassenbuch, ohne Filter und ohne gelöschte Buchungen.
let payeeChoicesQuery : LedgerPayeeSuggestionQuery = {
    AccountId = None
    IncludeDeleted = false
    OnlyNeedsReview = false
    OnlyAskLena = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Search = None
    AmountMin = None
    AmountMax = None
    AmountDirection = AllDirections
}

/// Nach einer Änderung des Betrags: Historie der offenen Buchung und Kontostände neu holen.
let private afterAmountChange (entryId: Guid) (model: Model) : Cmd<Msg> =
    Cmd.batch [ afterEntryChange entryId model; Cmd.ofMsg LoadBalances ]

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

/// Die Sperrgruppen je Buchung (ledger-smv1s): Befehle derselben Gruppe zur selben Buchung laufen nie
/// gleichzeitig, sonst gewinnt die zuletzt eintreffende Antwort und die Zeile zeigt einen falschen Stand.
type private RowLock =
    /// Kategorie, Übertrag und Split teilen `CategoryRows` (ein Split ändert die Kategorie).
    | CategoryLock
    /// Prüfvermerk aus Liste, Detail und Wisch, dazu „Passt, weiter“ im Durchgehen.
    | ReviewMarkLock
    | FlagLock
    | MemoLock
    | AmountLock
    /// „Erneut senden“ am Hinweis „Noch nicht in YNAB“.
    | PushLock

/// Läuft für die Buchung ein Aufruf ihrer Sperrgruppe?
let private rowBusy (lock: RowLock) (model: Model) (entryId: Guid) : bool =
    match lock with
    | CategoryLock -> (model.CategoryRows |> Map.tryFind entryId) = Some CategorySending
    | ReviewMarkLock ->
        (model.ReviewMarkRows |> Map.tryFind entryId) = Some ReviewMarkSending
        || model.Review |> Option.exists (fun r -> r.Confirming = Some entryId)
    | FlagLock -> (model.FlagRows |> Map.tryFind entryId) = Some FlagSending
    | MemoLock -> (model.MemoRows |> Map.tryFind entryId) = Some MemoSending
    | AmountLock -> (model.AmountRows |> Map.tryFind entryId) = Some AmountSending
    | PushLock -> (model.PushRetryRows |> Map.tryFind entryId) = Some PushSending

/// Führt einen Start-Zweig nur aus, wenn in der Sperrgruppe der Buchung nichts läuft. Gesperrt bleibt
/// alles still: Modell gleich, kein Befehl, kein Toast.
let private whenRowIdle
    (lock: RowLock)
    (entryId: Guid)
    (model: Model)
    (start: unit -> Model * Cmd<Msg> * ExternalMsg)
    : Model * Cmd<Msg> * ExternalMsg =
    if rowBusy lock model entryId then model, Cmd.none, NoOp else start ()

/// Nimmt den abgelehnten Speicherversuch des offenen Sheets „Betrag“ zurück; ein laufender Aufruf bleibt.
let private clearAmountSaveFailure (model: Model) : Model =
    match model.AmountEdit with
    | Some edit ->
        match model.AmountRows |> Map.tryFind edit.AmountEntryId with
        | Some (AmountCommandFailed _) -> { model with AmountRows = model.AmountRows |> Map.remove edit.AmountEntryId }
        | _ -> model
    | None -> model

/// Ändert den Entwurf des offenen Editors. Jede Eingabe nimmt einen Speicherfehler zurück (ledger-ng110).
let private editSplit (f: SplitEditor.SplitEditorState -> SplitEditor.SplitEditorState) (model: Model) : Model =
    match model.SplitEditor with
    | Some editor when not (rowBusy CategoryLock model editor.EntryId) -> { model with SplitEditor = Some (f { editor with SaveError = None }) }
    | _ -> model

/// Ein abgelehntes Speichern (ledger-ng110, §7.13): steht der Editor der Buchung noch offen, kommt die Meldung in
/// den Editor und kein Toast; ist er zur Antwort schon zu, meldet sie der Toast.
let private splitSaveFailed (entryId: Guid) (text: string) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let cleared = { model with CategoryRows = model.CategoryRows |> Map.remove entryId }
    match cleared.SplitEditor with
    | Some editor when editor.EntryId = entryId ->
        { cleared with SplitEditor = Some { editor with SaveError = Some text } }, Cmd.none, NoOp
    | _ -> cleared, Cmd.none, ShowToast (text, ToastError)

/// Setzt die Bestätigung am Feld (ersetzt eine stehende, ledger-p5y9s) und beendet ihr Leuchten nach
/// `detailNoteFlashMs`.
let private postDetailNote (note: DetailNote) (model: Model) : Model * Cmd<Msg> =
    let version = model.DetailNoteVersion + 1
    let timer =
        if note.Flashing then
            Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep detailNoteFlashMs }) () (fun () -> DetailNoteFlashEnded version)
        else
            Cmd.none
    { model with DetailNote = Some { note with Version = version }; DetailNoteVersion = version }, timer

/// Meldet nach dem Ort (`detailNoteRoute`): am Feld, in der Leiste (am PC Toast) oder auf der Karte des Durchgehens
/// wie bisher (`onCard`, ledger-pz5wv).
let private reportDetailNote
    (route: DetailNoteRoute)
    (note: DetailNote)
    (onCard: ExternalMsg)
    (model: Model, cmd: Cmd<Msg>)
    : Model * Cmd<Msg> * ExternalMsg =
    match route with
    | NoteAtField ->
        let model', timer = postDetailNote note model
        model', Cmd.batch [ cmd; timer ], NoOp
    | NoteOnReviewCard -> model, cmd, onCard
    | NoteInBar -> model, cmd, PostFeedback (detailNoteBar note)

/// Wie `reportDetailNote` für den Split-Editor, der nur in „Details“ steht: Schließt Esc im Durchgehen „Details“, bevor
/// die Antwort kommt, steht sie in der Zeile unter „Passt, weiter“ (`withDetailNoteLine`), kein Toast.
let private reportSplitNote (route: DetailNoteRoute) (note: DetailNote) (model: Model, cmd: Cmd<Msg>) : Model * Cmd<Msg> * ExternalMsg =
    match route, model.Review with
    | NoteOnReviewCard, Some review -> { model with Review = Some (withDetailNoteLine note review) }, cmd, NoOp
    | _ -> reportDetailNote route note NoOp (model, cmd)

/// Die Bestätigung dieser Buchung endet: „Rückgängig“ gelang oder eine neue Aktion auf ihrem Feld beginnt.
let private clearDetailNote (entryId: Guid) (model: Model) : Model =
    match model.DetailNote with
    | Some note when note.EntryId = entryId -> { model with DetailNote = None }
    | _ -> model

/// Das Feld der stehenden Bestätigung dieser Buchung: dort steht auch der Fehler einer Rücknahme. Ohne Bestätigung
/// „Kategorie“.
let private detailNoteFieldOf (model: Model) (entryId: Guid) : DetailNoteField =
    model.DetailNote
    |> Option.filter (fun note -> note.EntryId = entryId)
    |> Option.map (fun note -> note.Field)
    |> Option.defaultValue AtCategory

/// Nach einer gelungenen Rücknahme (Kategorie, Memo, Betrag, Payee, Lena): im offenen Detail steht der alte Wert im
/// Feld, die Bestätigung ist weg und der Rücknahme-Toast entfällt; eine YNAB-Warnung bleibt gelb am Feld (`hintTitle`
/// ist ihre obere Zeile in der Leiste). Ist das Detail zu, meldet es wie bisher (`closedText`, ledger-enjex). Auf der
/// Karte des Durchgehens meldet nur eine YNAB-Warnung (wie bisher), die gelungene Rücknahme bleibt ohne Meldung.
let private restoredAtField
    (route: DetailNoteRoute)
    (entryId: Guid)
    (field: DetailNoteField)
    (hintTitle: string)
    (sendFailure: string option)
    (changed: bool)
    (closedText: string)
    (model: Model, cmd: Cmd<Msg>)
    : Model * Cmd<Msg> * ExternalMsg =
    match sendFailure with
    | Some reason ->
        reportDetailNote
            route
            (restoredNotInYnabNoteFor hintTitle entryId field reason)
            (ShowToast ($"{hintTitle}: {reason}", ToastWarning))
            (model, cmd)
    | None when not changed -> model, cmd, NoOp
    // Auf der Karte des Durchgehens ohne Meldung, wie `reviewFieldRestored`: der alte Wert steht auf der Karte, im
    // Durchgehen gibt es keinen Toast (ledger-pz5wv Abschnitt 5).
    | None when route = NoteAtField || route = NoteOnReviewCard -> model, cmd, NoOp
    | None -> model, cmd, ShowToast (closedText, ToastSuccess)

/// Ein Befehl auf Memo, Betrag, Payee oder Lena scheiterte (ledger-enjex, Muster `categoryCommandFailed`). Im offenen
/// Detail steht der rote Fehler `note` unter dem Feld, ohne Toast und ohne zweite Zeile in der Hinweisleiste
/// (`clearRow`). Sonst trägt die Zeile den Fehler (`failRow`) und die Leiste meldet ihn (am PC der Toast); die Karte des
/// Durchgehens ohne „Details“ meldet wie bisher (`onCard`, ledger-pz5wv).
let private fieldCommandFailed
    (entryId: Guid)
    (note: DetailNote)
    (failRow: Model -> Model)
    (clearRow: Model -> Model)
    (onCard: ExternalMsg)
    (model: Model)
    : Model * Cmd<Msg> * ExternalMsg =
    match detailNoteRoute model entryId with
    | NoteAtField ->
        let model', timer = postDetailNote note (clearRow model)
        model', timer, NoOp
    | NoteOnReviewCard -> failRow model, Cmd.none, onCard
    | NoteInBar -> failRow model, Cmd.none, PostFeedback (detailNoteBar note)

/// Nach einer gelungenen Rücknahme: im offenen Detail steht der alte Wert im Feld und die Bestätigung ist weg (die
/// Rücknahme-Toasts entfallen); eine YNAB-Warnung bleibt gelb am Feld. Ist das Detail zu, meldet die Leiste wie
/// bisher (`closedText`).
let private restoredReport
    (route: DetailNoteRoute)
    (entryId: Guid)
    (field: DetailNoteField)
    (outcome: CategoryChangeOutcome)
    (closedText: string)
    (model: Model, cmd: Cmd<Msg>)
    : Model * Cmd<Msg> * ExternalMsg =
    let failure = match outcome.WriteBack with CategorySendFailed reason -> Some reason | _ -> None
    restoredAtField route entryId field categoryHintTitle failure (outcome.Change = CategoryChanged) closedText (model, cmd)

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
/// sagt, ob die Liste nachzuladen ist: bei aktivem Chip waren geänderte Buchungen nie
/// geladen (`AllExcept`), der Versatz der nächsten Seite stimmt dann nur nach dem Nachladen.
/// Rein, ohne Proxy.
let private applyReviewMarkBatch (outcome: ReviewMarkBatchOutcome) (model: Model) : Model * bool =
    let changedCount = List.length outcome.Changed
    let ended = {
        model with
            Selection = NoSelection
            SelectionAnchor = None
            SelectionMode = false
            BatchRunning = None
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
            BatchRunning = None
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
                BatchRunning = None }
        else
            { model with BatchRunning = None }
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

/// Was das Filter-Sheet unter 1024 px zeigt (ledger-npqcb): bei offenem Sheet die Sortierung, die Kategorie- oder die
/// Zeitraum-Auswahl an Stelle der Filter, sonst die Filter.
let filterSheetView (model: Model) : FilterSheetView =
    if not model.FilterSheetOpen then FilterSheetClosed
    elif model.SortSheetOpen then FilterSheetSort
    else
        match model.OpenPopover with
        | Some CategoryChip -> FilterSheetCategory
        | Some DateChip -> FilterSheetDate
        | _ -> FilterSheetFilters

/// Sind die Tasten der Liste gerade aus? Ein offenes Filter-Popover und ein offenes Sheet
/// gehören der Tastatur; ein offenes Seitenpanel nicht, es folgt der Markierung
/// (ledger-q1hw2, ADR 0053 Punkt 5).
let private keyboardBlocked (model: Model) : bool =
    model.OpenPopover.IsSome
    || model.FilterSheetOpen
    || model.BatchCategoryPickerOpen
    || model.CategoryPickerFor.IsSome
    || model.SplitEditor.IsSome
    || model.MemoEdit.IsSome
    || model.AmountEdit.IsSome
    || model.PayeeEdit.IsSome
    || (model.DetailFor.IsSome && model.DetailForm = DetailSheet)

/// Öffnet das Detail für `entry`: markiert die Zeile, setzt die Historie auf "lädt" und
/// stößt ihren Abruf an. Wechselt ein offenes Panel auf eine andere Buchung, läuft derselbe
/// Weg.
// ============================================
// Neue Buchung (ledger-at63w)
// ============================================

/// Das Konto des Kassenbuch-Filters, wenn genau eines gewählt ist.
let captureFilterAccount (model: Model) : Guid option =
    match model.SelectedFilter with
    | Some (OneAccount id) -> Some id
    | _ -> None

/// Die Konten, auf denen das Formular erfasst: gespiegelte, offene Budget-Konten ohne das
/// Import-Konto. Leer, solange Konten oder Budget-Konten fehlen.
let captureAccounts (model: Model) (defaults: LedgerBookingDefaults) : LedgerAccountView list =
    match model.Accounts, model.TransferTargets with
    | Success view, Success budget -> bookingAccounts view.Accounts budget defaults.ImportAccountId
    | _ -> []

/// Was die Konto-Auswahl zeigt (ledger-vf6z5). Ein Ladefehler geht vor dem Laden, damit die Liste nicht "lädt",
/// wenn nichts mehr lädt. Rein.
let captureAccountChoice (model: Model) (capture: Capture.CaptureState) : Capture.CaptureAccountList =
    match capture.Defaults, model.Accounts, model.TransferTargets with
    | Failure _, _, _
    | _, Failure _, _
    | _, _, Failure _ -> Capture.CaptureAccountsFailed
    | Success defaults, Success _, Success _ ->
        match captureAccounts model defaults with
        | [] -> Capture.NoBookableAccount
        | accounts -> Capture.BookableAccounts accounts
    | _ -> Capture.CaptureAccountsLoading

let private captureAccountIds (model: Model) (defaults: LedgerBookingDefaults) : Guid list =
    captureAccounts model defaults |> List.map (fun a -> a.Id)

/// Setzt das vorbelegte Konto nach, sobald Vorbelegung und Konten da sind.
let private withCaptureDefaultAccount (model: Model) : Model =
    match model.Capture with
    | Some capture ->
        match capture.Defaults with
        | Success defaults ->
            let form =
                Capture.withDefaultAccount defaults capture.FilterAccount (captureAccountIds model defaults) capture.Form
            { model with Capture = Some { capture with Form = form } }
        | _ -> model
    | None -> model

/// Ändert das offene Formular; ohne Formular bleibt alles, wie es ist.
let private updateCaptureForm (change: Capture.CaptureForm -> Capture.CaptureForm) (model: Model) : Model =
    match model.Capture with
    | Some capture -> { model with Capture = Some { capture with Form = change capture.Form } }
    | None -> model

/// Das Formular ohne Fehler, vor einem neuen Aufruf.
let private withoutCaptureErrors (capture: Capture.CaptureState) : Capture.CaptureState =
    { capture with AmountError = None; AccountError = None; ServerErrors = [] }

/// Startet "Buchung anlegen" aus dem Formular, oder zeigt die Fehler, die der Client schon kennt, am Feld
/// (Styleguide §5, ledger-vf6z5). Ein Betragsfehler holt den Fokus ins Feld und rollt die Meldung ins Bild.
let private submitCapture (capture: Capture.CaptureState) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match Capture.captureRequest capture.Form with
    | Ok request ->
        { model with Capture = Some { withoutCaptureErrors capture with Saving = true } },
        Cmd.ofMsg (RunSubmitCapture request),
        NoOp
    | Error errors ->
        { model with
            Capture =
                Some { capture with AmountError = errors.AmountMessage; AccountError = errors.AccountMessage; ServerErrors = [] } },
        (if errors.AmountMessage.IsSome then Cmd.ofMsg RevealCaptureAmountError else Cmd.none),
        NoOp

/// Die Meldung nach „Buchung löschen“, wenn die Buchung nicht in der geladenen Liste steht (über die Route geöffnet,
/// Filter, Durchgehen): am Handy die Leiste, am PC der Toast, je mit „Rückgängig“ (ledger-k68sd). Steht sie in der
/// Liste, meldet die Zeile am Platz (`DeletedRow`) und es gibt keine Meldung.
let entryDeletedFeedback (payee: string option) (entryId: Guid) : FeedbackNote =
    { Tone = FeedbackOk
      Action = "Gelöscht"
      Detail = Some (payee |> Option.defaultValue "ohne Payee")
      Act = Some { Label = undoToastLabel; Kind = UndoDeleteEntry entryId } }

/// Das Wiederherstellen ist gescheitert und es gibt keine Zeile am Platz: rot am Meldungsplatz, „Rückgängig“ bleibt
/// zum erneuten Versuch (ledger-k68sd).
let entryRestoreFailedFeedback (reason: string) (entryId: Guid) : FeedbackNote =
    { failedFeedback "Nicht wiederhergestellt" reason with
        Act = Some { Label = undoToastLabel; Kind = UndoDeleteEntry entryId } }

/// Die Zeile am Platz für diese Buchung, wenn sie in der geladenen Liste steht, sonst `None` (dann meldet die Leiste
/// bzw. der Toast). Im Durchgehen gibt es keine Liste (ledger-k68sd).
let deletedRowFor (model: Model) (entryId: Guid) : DeletedEntryRow option =
    match model.Review, model.Entries with
    | None, Success rows ->
        rows
        |> List.tryFindIndex (fun e -> e.Id = entryId)
        |> Option.map (fun index ->
            { Entry = rows.[index]
              Index = index
              Before = rows |> List.tryItem (index + 1) |> Option.map (fun e -> e.Id)
              Phase = DeletedRowShown })
    | _ -> None

/// Setzt die gelöschte Buchung an ihren Platz in der Liste (ledger-k68sd): vor die Buchung, die danach kam, sonst an
/// den alten Index. Steht sie noch in der Liste („gelöschte zeigen“), ersetzt der Stand vor dem Löschen sie, es gibt
/// keine zweite Zeile. Rein; die Ansicht zeigt an dieser Stelle die Zeile „gelöscht“, „Rückgängig“ setzt damit die
/// Buchung zurück, bis das Nachladen den Stand des Servers bringt.
let withDeletedPlaced (row: DeletedEntryRow option) (entries: LedgerEntryView list) : LedgerEntryView list =
    match row with
    | None -> entries
    | Some row ->
        let others = entries |> List.filter (fun e -> e.Id <> row.Entry.Id)
        let at =
            row.Before
            |> Option.bind (fun before -> others |> List.tryFindIndex (fun e -> e.Id = before))
            |> Option.defaultValue (min row.Index others.Length)
        List.insertAt at row.Entry others

/// Beendet diese Nachricht die Zeile am Platz einer gelöschten Buchung (ledger-k68sd, R3: bis zur nächsten Aktion)?
/// Aktionen sind Wisch, Spiegeln, Neue Buchung, Filter und Sortierung, das Öffnen einer Buchung, Prüfen, Lena, Auswahl,
/// ein weiteres Löschen und das Verlassen der Seite. Antworten, Nachladen und „Rückgängig“ selbst nicht. Ein Neuaufbau
/// der Liste (`resetEntries`) nimmt die Zeile ohnehin weg.
let endsDeletedRow (msg: Msg) : bool =
    match msg with
    | StartSwipeReviewed _
    | StartMirror
    | OpenCapture
    | OpenCaptureUnfiltered
    | OpenDetail _
    | OpenFocusedDetail
    | OpenRouteEntry _
    | StartDeleteEntry _
    | StartSetReviewMark _
    | ReviewKeyPressed _
    | LenaKeyPressed
    | StartSetLenaFlag _
    | StartSelectionMode _
    | ToggleRowSelected _
    | ClickRowCheckbox _
    | ToggleSelectAll
    | ToggleHeaderCheckbox
    | ClickRowWithModifier _
    | StartReviewMarkBatch
    | StartCategoryBatch _
    | StartLenaBatch _
    | SelectFilter _
    | PresetFilter _
    | PresetQuickFilter _
    | SetSort _
    | SetSortExactly _
    | ToggleIncludeDeleted
    | ToggleOnlyNeedsReview
    | ToggleWithoutCategory
    | ToggleOnlyAskLena
    | FilterByRowPayee _
    | FilterByRowCategory _
    | ApplyCategoryFilter
    | RemoveCategoryFilter
    | ApplyDateFilter
    | RemoveDateFilter
    | CommitSearch _
    | RemoveSearch
    | CommitAmountFilter _
    | RemoveAmountFilter
    | SetAmountDirection _
    | RemoveAmountAndDirection
    | ClearAllFilters
    | ReviewOpened
    | LedgerLeft -> true
    | _ -> false

/// Setzt alles zurück, was am offenen Detail hängt (Historie samt aufgeklappter, Memo, Betrag, Payee), lässt aber
/// `DetailFor` stehen. Das Kassenbuch schließt darüber das Detail (`closeDetail`), das Durchgehen „Details“, wo die
/// Karte `DetailFor` bleibt (ledger-hme9k).
let clearDetailEdits (model: Model) : Model =
    { model with
        History = NotAsked
        HistoryOpenFor = None
        MemoEdit = None
        AmountEdit = None
        PayeeEdit = None
        DeleteFailure = None }

/// Schließt das Detail samt allem, was daran hängt: Historie, Sheet "Notiz" und Sheet "Betrag".
/// Jede Stelle, die das Detail schließt, geht hier durch, damit kein offenes Sheet stehen bleibt.
let closeDetail (model: Model) : Model =
    { clearDetailEdits model with DetailFor = None }

/// Was Schließen im Detail dieser Buchung tut (× im Kopf von Sheet und Panel, ledger-hme9k): erst die offene
/// Payee-Auswahl, dann der Betrag, dann das Memo (am Handy Inhalt des Sheets, design-system-x045q), dann der Split-Editor (Entwurf verworfen), sonst das Detail selbst. Die
/// Kategorie-Auswahl im Sheet schließt vorher ihr eigener Rückruf. Esc geht eine eigene Kette (`KeyLeave` in
/// `update`); die Zustände schließen sich heute gegenseitig aus, ein neuer Zustand im Detail gehört in beide.
let detailCloseStep (model: Model) (entry: LedgerEntryView) : Msg =
    if model.PayeeEdit |> Option.exists (fun edit -> edit.PayeeEntryId = entry.Id) then CancelPayeeEdit
    elif model.AmountEdit |> Option.exists (fun edit -> edit.AmountEntryId = entry.Id) then CancelAmountEdit
    elif model.MemoEdit |> Option.exists (fun edit -> edit.MemoEntryId = entry.Id) then CancelMemoEdit
    elif model.SplitEditor |> Option.exists (fun editor -> editor.EntryId = entry.Id) then CloseSplitEditor
    else CloseDetail

/// Öffnet „Neue Buchung“; `filterAccount` belegt das Konto vor (der Kassenbuch-Filter, `None` ohne Filter). Ein
/// schon offenes Erfassen bleibt, wie es ist.
let private openCapture (filterAccount: Guid option) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match model.Capture with
    | Some _ -> model, Cmd.none, NoOp
    | None ->
        let loadTargets =
            match model.TransferTargets with
            | Success _ | Loading -> Cmd.none
            | _ -> Cmd.ofMsg RunLoadTransferTargets
        { closeDetail model with
            Capture = Some { Capture.opening with FilterAccount = filterAccount }
            CategoryPickerFor = None
            CategorySuggestions = []
            CategoryCard = None },
        Cmd.batch [ Cmd.ofMsg RunLoadCaptureDefaults; loadTargets; Cmd.ofMsg EnsureCategoriesFresh ],
        NoOp

/// Darf der Betrags-Knopf im Kopf das Sheet "Betrag" öffnen? Nicht bei gesperrtem Betrag, nicht
/// solange ein Aufruf zum Betrag läuft, nicht bei offenem Split-Editor (der Kopf zeigt dort den
/// Gesamtbetrag des Editors) und nicht, solange die Buchung gelöscht wird (ledger-m7mye).
let amountEditOpenable (model: Model) (entry: LedgerEntryView) : bool =
    (amountLockFor entry).IsNone
    && (model.AmountRows |> Map.tryFind entry.Id) <> Some AmountSending
    && model.SplitEditor.IsNone
    && not (model.DeletingEntries.Contains entry.Id)

/// Darf ein Tipp auf die Payee-Zeile die Payee-Auswahl öffnen? Nicht bei gesperrtem Payee, nicht
/// solange ein Aufruf zum Payee läuft, nicht bei offenem Split-Editor und nicht, solange die
/// Buchung gelöscht wird (ledger-fxt09).
let payeeEditOpenable (model: Model) (entry: LedgerEntryView) : bool =
    (payeeLockFor entry).IsNone
    && (model.PayeeRows |> Map.tryFind entry.Id) <> Some PayeeSending
    && model.SplitEditor.IsNone
    && not (model.DeletingEntries.Contains entry.Id)

/// Schließt „alle zeigen“ am Payee das Detail (ledger-zfj78)? Nur unter 1024 px, wo es ein Sheet über der Liste
/// ist, und nicht im Durchgehen (dort gehört das Detail zur Karte).
let payeeShowAllClosesDetail (model: Model) : bool =
    model.DetailFor.IsSome && model.DetailForm = DetailSheet && model.Review.IsNone

let private showDetail (entry: LedgerEntryView) (model: Model) : Model * Cmd<Msg> =
    { model with
        DetailFor = Some entry
        FocusedRow = Some entry.Id
        History = Loading
        HistoryOpenFor = None
        MemoEdit = None
        AmountEdit = None
        PayeeEdit = None
        DeleteFailure = None },
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
    || rowBusy ReviewMarkLock model entry.Id

/// Das Kassenbuch für einen Eintrag aus „Zu tun“ (ledger-s1weh): alle Konten, genau der eine Filter, die übrigen
/// Filter leer, damit die Liste die Zahl aus der Übersicht zeigt. Rein.
let presetQuickFilter (preset: QuickFilterPreset) (model: Model) : Model =
    { closeDetail (resetEntries model) with
        SelectedFilter = Some AllAccounts
        RememberedFilter = Some AllAccounts
        AccountSheetOpen = false
        Filters = { noFilters with OnlyAskLena = (preset = PresetAskLena) }
        // Eine wartende Such- oder Betragseingabe setzte sonst einen Filter in die neue Sicht (ledger-ct3cx).
        // Die Felder leeren sich wie bei „Filter löschen“; ein stehender Entwurf hielte sonst
        // `isInputActive` wahr und „Neu laden“ zurück.
        SearchDraft = ""
        SearchVersion = model.SearchVersion + 1
        FilterSheetOpen = false
        AmountMinDraft = ""
        AmountMaxDraft = ""
        AmountVersion = model.AmountVersion + 1
        OnlyNeedsReview = (preset = PresetNeedsReview) }


// ============================================
// Durchgehen Karte für Karte (ledger-v6s7c)
// ============================================

/// Zeigt die Karte an der Stelle `index` der Schlange: die Buchung steht in `DetailFor` (der letzte
/// bekannte Stand), sie wird neu gelesen, die Vorschläge zum Payee laden. Eine Buchung ohne bekannten
/// Stand wird übersprungen wie eine unbekannte beim Neulesen (`reviewSkipsEntry`): ohne Stand gibt es
/// keine Karte, und der Nachlauf in `update` stieße sonst bei jeder Nachricht ein neues Neulesen an.
/// Hinter der letzten Karte steht die Endansicht. Rein bis auf die Befehle.
let rec private showReviewCardAt (index: int) (model: Model) : Model * Cmd<Msg> =
    match model.Review, reviewQueueOf model with
    | Some review, Some ids ->
        let cleared =
            { closeDetail model with
                CategoryPickerFor = None
                CategorySuggestions = []
                CategoryCard = None
                SplitEditor = None }
        if index < List.length ids then
            let id = ids.[index]
            match findEntry model id |> Option.orElse (review.Snapshots |> Map.tryFind id) with
            | None -> showReviewCardAt (index + 1) model
            | Some entry ->
                let shown =
                    { cleared with
                        DetailFor = Some entry
                        Review = Some { review with Index = index; Suggestions = []; DetailShown = false; Confirming = None; Deciding = None; ReturnFrom = None; FieldNote = None } }
                let suggestions =
                    match entry.Payee with
                    | Some payee when not (String.IsNullOrWhiteSpace payee) -> Cmd.ofMsg (RunReviewSuggestions (id, payee))
                    | _ -> Cmd.none
                let matchLoad =
                    match entry.PossibleDuplicate |> Option.bind (fun d -> d.MatchEntryId) with
                    | Some matchId when not (review.Matches.ContainsKey matchId) -> Cmd.ofMsg (RunReviewMatch matchId)
                    | _ -> Cmd.none
                shown, Cmd.batch [ Cmd.ofMsg (RunReviewRefresh id); suggestions; matchLoad ]
        else
            { cleared with Review = Some { review with Index = List.length ids; Suggestions = []; DetailShown = false; Confirming = None; Deciding = None; ReturnFrom = None; FieldNote = None } },
            Cmd.none
    | _ -> model, Cmd.none

/// Geht zur nächsten Karte. Der Stand der verlassenen Karte bleibt gemerkt, falls „Rückgängig“ sie
/// zurückholt.
let private advanceReview (model: Model) : Model * Cmd<Msg> =
    match model.Review with
    | Some review ->
        let snapshots =
            match reviewCardOf model with
            | Some card -> review.Snapshots |> Map.add card.Id card
            | None -> review.Snapshots
        showReviewCardAt (review.Index + 1) { model with Review = Some { review with Snapshots = snapshots } }
    | None -> model, Cmd.none

/// Steht auf der gezeigten Karte eine Eingabe offen (Memo, Betrag, Payee, Split-Editor)? Ein Kartenwechsel
/// schlösse sie samt Entwurf. Rein.
let private reviewCardEditing (model: Model) : bool =
    model.MemoEdit.IsSome || model.AmountEdit.IsSome || model.PayeeEdit.IsSome || model.SplitEditor.IsSome

/// Holt die Karte einer Buchung zurück, die schon hinter der gezeigten liegt („Rückgängig“ nach Lena
/// oder Verwerfen). Liegt sie nicht in der Schlange, bleibt alles.
let private returnToReviewCard (entryId: Guid) (model: Model) : Model * Cmd<Msg> =
    match model.Review, reviewQueueOf model with
    | Some review, Some ids ->
        match ids |> List.tryFindIndex (fun id -> id = entryId) with
        | Some index when index <> review.Index || (reviewCardOf model).IsNone ->
            let snapshots =
                match reviewCardOf model with
                | Some card -> review.Snapshots |> Map.add card.Id card
                | None -> review.Snapshots
            showReviewCardAt index { model with Review = Some { review with Snapshots = snapshots } }
        | _ -> model, Cmd.none
    | _ -> model, Cmd.none

/// Ist ein Rückgängig der Zeile gerade gesperrt? Eine offene Eingabe auf der gezeigten Karte hält sie fest, und ein
/// laufender Aufruf derselben Sperrgruppe lässt keinen zweiten zu.
let private reviewUndoBlocked (undo: ReviewUndo) (model: Model) : bool =
    reviewCardEditing model
    || (match undo with
        | UndoSkipped _ -> false
        | UndoConfirmed (entryId, _) -> rowBusy ReviewMarkLock model entryId
        | UndoLenaAsked (entryId, _) -> rowBusy FlagLock model entryId
        | UndoDiscarded entryId -> model.RestoringDuplicates.Contains entryId
        | UndoPayeeChanged (entryId, _) -> (model.PayeeRows |> Map.tryFind entryId) = Some PayeeSending)

let private editReview (f: ReviewSession -> ReviewSession) (model: Model) : Model =
    { model with Review = model.Review |> Option.map f }

/// Setzt die Zeile der letzten Aktion (ledger-pz5wv); `None` zeigt wieder den Wisch-Hinweis.
let private setReviewLast (last: ReviewLast option) (model: Model) : Model =
    editReview
        (fun r ->
            match last with
            | Some _ -> { r with Last = last; Clock = r.Clock + 1; LastStamp = r.Clock + 1 }
            | None -> { r with Last = None })
        model

/// Die Zeile einer Aktion an einer Buchung der Schlange, mit ihrem Payee.
let private reviewLastFor (model: Model) (entryId: Guid) (kind: ReviewLastKind) (outcome: ReviewLastOutcome) (act: ReviewLastAct option) : ReviewLast =
    let payee =
        findEntry model entryId
        |> Option.orElse (model.Review |> Option.bind (fun r -> r.Snapshots |> Map.tryFind entryId))
        |> Option.bind (fun e -> e.Payee)
    { EntryId = entryId; Kind = kind; Payee = payee; Outcome = outcome; Act = act }

/// Merkt einen Befehl der Karte, dessen Antwort über einen gemeinsamen Handler kommt.
let private awaitReview (awaited: ReviewAwait) (model: Model) : Model =
    editReview (fun r -> { r with Awaiting = r.Awaiting.Add awaited }) model

/// Das gemerkte Warten, das zur Antwort passt, und das Modell ohne es. `None`: die Antwort gehört nicht zur Karte
/// (etwa aus „Details“ oder außerhalb des Durchgehens).
let private takeAwait (matches: ReviewAwait -> bool) (model: Model) : (ReviewAwait * Model) option =
    model.Review
    |> Option.bind (fun r -> r.Awaiting |> Seq.tryFind matches)
    |> Option.map (fun found -> found, editReview (fun r -> { r with Awaiting = r.Awaiting.Remove found }) model)

/// Meldet die Zeile unter dem Knopf eine Aktion am Feld „Kategorie“ dieser Buchung (Warnung, Fehler, gescheiterte
/// Rücknahme am Feld)?
let private isFieldLine (entryId: Guid) (last: ReviewLast) : bool =
    last.EntryId = entryId
    && (match last.Kind, last.Act with
        | (LastCategoryChanged | LastTransferSet), _ -> true
        | LastUndone, Some (LastRetry (RetryFieldUndo _)) -> true
        | _ -> false)

/// Nimmt die Bestätigung am Feld der Karte und eine Zeile zu diesem Feld weg (neue Kategorie, gelungene Rücknahme).
let private clearReviewField (entryId: Guid) (model: Model) : Model =
    editReview
        (fun r ->
            { r with
                FieldNote = r.FieldNote |> Option.filter (fun n -> n.EntryId <> entryId)
                Last = r.Last |> Option.filter (fun l -> not (isFieldLine entryId l)) })
        model

/// Die Bestätigung am Feld „Kategorie“ der Karte (ledger-mf2bg): das Feld leuchtet `detailNoteFlashMs`, darunter Text
/// und „Rückgängig“.
let private postReviewFieldNote (entryId: Guid) (text: string, available: string option) (undo: ToastActionKind) (model: Model) : Model * Cmd<Msg> =
    match model.Review with
    | Some r ->
        let stamp = r.Clock + 1
        let note = { EntryId = entryId; Text = text; Available = available; Undo = undo; Flashing = true; Stamp = stamp }
        { model with Review = Some { r with FieldNote = Some note; Clock = stamp } },
        Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep detailNoteFlashMs }) () (fun () -> ReviewFieldFlashEnded stamp)
    | None -> model, Cmd.none

/// Eine Aktion am Feld der Karte ging nach BB, aber nicht nach YNAB: gelb in der Zeile unter dem Knopf, ohne Knopf (das
/// Rückgängig steht am Feld).
let private reviewFieldNotInYnab (entryId: Guid) (kind: ReviewLastKind) (reason: string) (model: Model) : Model =
    setReviewLast (Some (reviewLastFor model entryId kind (LastNotInYnab reason) None)) model

/// Ein Befehl am Feld der Karte scheiterte: das Feld behält den alten Wert, rot in der Zeile unter dem Knopf mit
/// „Erneut“, kein Toast. Die Zeile der Buchung trägt den Befehlsfehler wie bisher (Hinweisleiste in „Details“).
let private reviewFieldFailed (entryId: Guid) (reason: string) (kind: ReviewLastKind) (retry: ReviewRetry) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let failedRow = { model with CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed reason) }
    setReviewLast (Some (reviewLastFor failedRow entryId kind (LastFailed reason) (Some (LastRetry retry)))) failedRow, Cmd.none, NoOp

/// Ein Befehl auf Kategorie, Übertrag oder Aufteilung scheiterte (`note` ist der rote Fehler). Im offenen Detail steht
/// er rot unter dem Feld, ohne Toast und ohne zweite Zeile in der Hinweisleiste. Sonst trägt die Zeile den
/// Befehlsfehler (die Hinweisleiste zeigt ihn beim nächsten Öffnen) und die Leiste meldet ihn (am PC der Toast). Auf der
/// Karte des Durchgehens ohne „Details“ steht er rot unter dem Knopf mit „Erneut“ (`onCard`, `reviewFieldFailed`), kein
/// Toast (ledger-pz5wv Abschnitt 5).
let private categoryCommandFailed
    (entryId: Guid)
    (reason: string)
    (note: DetailNote)
    (onCard: ReviewLastKind * ReviewRetry)
    (model: Model)
    : Model * Cmd<Msg> * ExternalMsg =
    let failedRow = { model with CategoryRows = model.CategoryRows |> Map.add entryId (CategoryCommandFailed reason) }
    match detailNoteRoute model entryId with
    | NoteAtField ->
        let model', timer = postDetailNote note { model with CategoryRows = model.CategoryRows |> Map.remove entryId }
        model', timer, NoOp
    | NoteOnReviewCard -> reviewFieldFailed entryId reason (fst onCard) (snd onCard) model
    | NoteInBar -> failedRow, Cmd.none, PostFeedback (detailNoteBar note)

/// Nach einer gelungenen Rücknahme am Feld der Karte: der alte Wert steht im Feld, die Bestätigung ist weg, keine
/// Meldung („… zurückgesetzt“ entfällt). Nimmt YNAB sie nicht, steht das gelb unter dem Knopf.
let private reviewFieldRestored (entryId: Guid) (outcome: CategoryChangeOutcome) (model: Model, cmd: Cmd<Msg>) : Model * Cmd<Msg> * ExternalMsg =
    let cleared = clearReviewField entryId model
    match outcome.WriteBack with
    | CategorySendFailed reason -> reviewFieldNotInYnab entryId LastUndone reason cleared, cmd, NoOp
    | _ -> cleared, cmd, NoOp

/// Die zurückgeholte Karte kommt von dieser Seite herein, falls sie jetzt gezeigt wird.
let private markReturn (undo: ReviewUndo) (entryId: Guid) (model: Model) : Model =
    if isReviewCard model entryId then editReview (fun r -> { r with ReturnFrom = Some (reviewReturnSideOf undo) }) model else model

/// Trägt die Antwort eines Nachladens im Hintergrund ein (ledger-ct3cx). Die Zeilen stehen in der
/// Reihenfolge des Servers: geänderte mit neuem Stand, gelöschte und nicht mehr passende fehlen, neue
/// stehen an ihrem Platz. Deckt die Antwort den ganzen Treffer ab, wird die Auswahl auf die noch
/// vorhandenen Buchungen beschnitten, sonst bleibt sie (bei „alle“ gehören neue dazu), der
/// Auswahl-Modus bleibt, der Anker nur mit seiner Zeile. Ein offenes Detail
/// bekommt den neuen Stand seiner Buchung, wenn sie in der Antwort steht, und lädt seine Historie neu;
/// das Durchgehen bleibt unberührt. Zähler kommen aus der Antwort, ausgegraute Zeilen, veraltete
/// Reihenfolge und lokal entfernte Zeilen enden. Rein bis auf den Befehl.
let private applyEntriesRefresh (page: LedgerEntryPage) (model: Model) : Model * Cmd<Msg> =
    let entries = page.Entries
    let present = entries |> List.map (fun e -> e.Id) |> Set.ofList
    // Die Antwort umfasst nur das Fenster der geladenen Seiten. Fehlt eine Id, kann sie gelöscht sein
    // oder hinter das Fenster gerückt (eine neue Buchung oben schiebt die letzte Zeile hinaus). Nur wenn
    // die Antwort den ganzen Treffer abdeckt, ist eine fehlende Id sicher weg; sonst bleibt sie gewählt
    // bzw. abgewählt, damit eine Sammelaktion keine abgewählte Buchung trifft.
    let coversHit = List.length entries >= page.TotalCount
    let prune (ids: Set<Guid>) = if coversHit then Set.intersect ids present else ids
    let selection =
        match model.Selection with
        | NoSelection -> NoSelection
        | Picked ids ->
            let kept = prune ids
            if Set.isEmpty kept then NoSelection else Picked kept
        | AllExcept excluded -> AllExcept (prune excluded)
    let detail =
        match model.Review, model.DetailFor with
        | None, Some open' -> Some (entries |> List.tryFind (fun e -> e.Id = open'.Id) |> Option.defaultValue open')
        | _, other -> other
    let refreshed =
        { model with
            Entries = Success entries
            Selection = selection
            SelectionAnchor = model.SelectionAnchor |> Option.filter present.Contains
            DetailFor = detail
            // Dieselbe Regel wie nach einer geladenen Seite.
            FocusedRow =
                (match detail with
                 | Some d -> if present.Contains d.Id then Some d.Id else None
                 | None -> reconcileFocus entries model.FocusedRow)
            TotalCount = page.TotalCount
            TotalAmount = page.TotalAmount
            HasMoreEntries = List.length entries < page.TotalCount
            DeletedCount = page.DeletedCount
            NeedsReviewCount = page.NeedsReviewCount
            AskLenaCount = page.AskLenaCount
            WithoutCategoryCount = page.WithoutCategoryCount
            FilteredOut = Set.empty
            OrderStale = false
            LocallyRemoved = 0
            ListActivity = ListIdle }
    let history =
        match model.Review, detail with
        | None, Some d -> afterEntryChange d.Id refreshed
        | _ -> Cmd.none
    refreshed, history

/// Gibt „Ist neu“ und „Verwerfen“ frei, wenn die Antwort zur laufenden Entscheidung gehört. Die Antwort
/// einer Entscheidung aus einem früheren Durchgehen lässt die laufende stehen (ledger-smv1s).
let private releaseDeciding (entryId: Guid) (model: Model) : Model =
    model |> editReview (fun r -> if r.Deciding = Some entryId then { r with Deciding = None } else r)

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
        // Kommen die Konten nach der Vorbelegung (Deep Link), wird das Konto des Formulars nachgesetzt.
        let model' = withCaptureDefaultAccount { model with Accounts = Success accounts; SelectedFilter = filter }
        match filter with
        | Some _ ->
            let rebuilt, cmd = rebuildEntries model'
            rebuilt, cmd, NoOp
        | None -> { resetEntries model' with Entries = NotAsked }, Cmd.none, NoOp

    | AccountsLoaded (Error message) ->
        { model with Accounts = Failure message }, Cmd.none, NoOp

    | RefreshAccounts ->
        // Wie `LoadAccounts`, nur ohne Ladezustand: die geladenen Konten bleiben stehen, bis die neuen
        // da sind. Der Aufruf liegt hinter einem Lambda, damit `update` auch unter .NET läuft.
        let cmd =
            Cmd.OfAsync.either
                (fun () -> Retry.reading Api.ledger.getLedgerAccounts ())
                ()
                (Ok >> AccountsRefreshed)
                (fun ex -> AccountsRefreshed (Error ex.Message))
        model, Cmd.batch [ cmd; Cmd.ofMsg LoadLenaFlagSettings ], NoOp

    | AccountsRefreshed (Ok accounts) ->
        // Nach einem Spiegel-Lauf (ledger-ct3cx): bleibt der aufgelöste Filter derselbe und steht eine
        // Liste, lädt sie im Hintergrund nach. Sonst (anderes Konto, keine oder noch ladende Liste)
        // gilt dasselbe wie nach einem Seitenaufruf.
        let filter = resolveInitialFilter model.SelectedFilter model.RememberedFilter accounts
        if filter.IsSome && filter = model.SelectedFilter && RemoteData.isSuccess model.Entries then
            withCaptureDefaultAccount { model with Accounts = Success accounts }, Cmd.ofMsg RefreshEntries, NoOp
        else
            updateCore (AccountsLoaded (Ok accounts)) model

    | AccountsRefreshed (Error message) ->
        { model with Accounts = RemoteData.lateFailure message model.Accounts }, Cmd.none, NoOp

    // ============================================
    // Kontostaende
    // ============================================
    | LoadBalances ->
        // Der Aufruf liegt hinter einem Lambda, damit `update` auch unter .NET läuft, wo der
        // Proxy beim ersten Zugriff wirft. Die Antwort trägt die Nummer dieses Starts (ledger-pfhdq).
        let generation = model.BalancesGeneration + 1
        let cmd =
            Cmd.OfAsync.either
                (fun () -> Retry.reading Api.ledger.getLedgerAccountBalances ())
                ()
                (fun balances -> BalancesLoaded (generation, Ok balances))
                (fun ex -> BalancesLoaded (generation, Error ex.Message))
        // Geladene Stände bleiben stehen, bis die neuen da sind (wie die Kategorien, ledger-kwwxz):
        // sonst flackern nach "Jetzt spiegeln" alle Kontostände zu Platzhaltern. Ein Hinweis auf ein
        // gescheitertes Nachladen bleibt bis zur Antwort stehen.
        let balances =
            match model.Balances with
            | Success _ -> model.Balances
            | _ -> Loading
        { model with Balances = balances; BalancesGeneration = generation }, cmd, NoOp

    | BalancesLoaded (generation, _) when generation <> model.BalancesGeneration ->
        // Antwort auf einen älteren Start (ledger-pfhdq): fällt weg, Erfolg wie Fehler.
        model, Cmd.none, NoOp

    | BalancesLoaded (_, Ok balances) ->
        { model with Balances = Success balances; BalancesRefreshError = None }, Cmd.none, NoOp

    | BalancesLoaded (_, Error message) ->
        // Stehen Stände, bleiben sie, und der Grund wird zum Hinweis über der Leiste (ledger-pfhdq).
        let refreshError =
            match model.Balances with
            | Success _ -> Some message
            | _ -> None
        { model with
            Balances = RemoteData.lateFailure message model.Balances
            BalancesRefreshError = refreshError },
        Cmd.none,
        NoOp

    // ============================================
    // Konto-Filter
    // ============================================
    | SelectFilter filter ->
        // Ein Kontowechsel schliesst ein offenes Detail: es gehoerte zu
        // einer Buchung, die gleich nicht mehr in der Liste steht. Die
        // Scroll-Position wird bewusst NICHT gemerkt (Task-Notes) — alle anderen
        // Zustaende (Kontostaende, Lauf-Status) bleiben unangetastet stehen.
        let rebuilt, load =
            rebuildEntries
                { closeDetail model with
                    SelectedFilter = Some filter
                    RememberedFilter = Some filter
                    AccountSheetOpen = false }
        rebuilt, Cmd.batch [ load; Cmd.ofEffect (fun _ -> writeRememberedFilter filter) ], NoOp

    | PresetFilter filter ->
        { closeDetail (resetEntries model) with
            SelectedFilter = Some filter
            RememberedFilter = Some filter
            AccountSheetOpen = false },
        Cmd.ofEffect (fun _ -> writeRememberedFilter filter),
        NoOp

    | PresetQuickFilter preset ->
        let filtered = presetQuickFilter preset model
        filtered, Cmd.ofEffect (fun _ -> writeRememberedFilter AllAccounts), NoOp

    | OpenAccountSheet -> { model with AccountSheetOpen = true }, Cmd.none, NoOp

    | CloseAccountSheet -> { model with AccountSheetOpen = false }, Cmd.none, NoOp

    | SetSort field ->
        // Wie ein Filterwechsel: Seite 0, Auswahl leer, Filter und Konto bleiben stehen.
        rebuildWith { model with Sort = nextSort (effectiveSort model) field }

    | SetSortExactly sort ->
        // Wie SetSort, nur mit der gewählten Kombination; das Sheet schließt.
        rebuildWith { model with Sort = sort; SortSheetOpen = false }

    | OpenSortSheet -> { model with SortSheetOpen = true }, Cmd.none, NoOp

    | CloseSortSheet -> { model with SortSheetOpen = false }, Cmd.none, NoOp

    // ============================================
    // Buchungen, seitenweise
    // ============================================
    | LoadEntries ->
        let rebuilt, cmd = rebuildEntries model
        rebuilt, cmd, NoOp

    | RunLoadEntriesPage page ->
        match pageRequest model page with
        | None -> model, Cmd.none, NoOp
        | Some (query, answer) ->
            let cmd =
                Cmd.OfAsync.either
                    (Retry.reading Api.ledger.getLedgerEntriesPage)
                    query
                    (Ok >> answer)
                    (fun ex -> answer (Error ex.Message))
            model, cmd, NoOp

    // Die Antwort auf eine Liste, die es nicht mehr gibt (Filter oder Sortierung haben gewechselt,
    // die Wiederholung kam zu spät): verwerfen, auch ihr Scheitern. Die aktuelle Abfrage läuft
    // unabhängig davon weiter und beendet den Ladezustand selbst (infrastructure-r3tw7).
    | EntriesPageLoaded (generation, _, _) when generation <> model.EntriesGeneration ->
        model, Cmd.none, NoOp

    | EntriesPageLoaded (_, page, Ok entryPage) ->
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
            WithoutCategoryCount = entryPage.WithoutCategoryCount
            ListActivity = afterPage model.ListActivity },
        // Ein Nachladen, das auf diese Seite gewartet hat, holt jetzt den ganzen Ausschnitt (ledger-ct3cx).
        refreshAfterPage model.ListActivity,
        NoOp

    | EntriesPageLoaded (_, 0, Error message) ->
        // Der Fehler bleibt an der Liste stehen (ErrorDisplay), statt als Toast
        // wegzublenden — die Seite waere sonst still leer.
        { model with Entries = Failure message; ListActivity = afterPage model.ListActivity }, Cmd.none, NoOp

    | EntriesPageLoaded (_, _, Error message) ->
        // Ein Fehler beim Nachladen loescht die bereits sichtbare Liste nicht: sie
        // bleibt stehen, nur der Hinweis erscheint als Toast.
        { model with ListActivity = afterPage model.ListActivity },
        refreshAfterPage model.ListActivity,
        PostFeedback (loadMoreFailedFeedback message)

    | LoadMoreEntries ->
        // Reiner Wächter: kein zweiter Nachlade-Aufruf waehrend einer laeuft, keiner
        // ohne weitere Seiten, keiner ohne eine erfolgreich geladene erste Seite.
        // Bei veralteter Reihenfolge ruht das Nachladen, bis "Neu laden" die Liste neu aufsetzt.
        // Während eines Nachladens im Hintergrund ruht es auch: die Seite gehörte zum alten Ausschnitt.
        if model.ListActivity <> ListIdle || model.OrderStale || not model.HasMoreEntries || not (RemoteData.isSuccess model.Entries) then
            model, Cmd.none, NoOp
        else
            { model with ListActivity = LoadingMorePage }, Cmd.ofMsg (RunLoadEntriesPage model.NextPage), NoOp

    | RefreshEntries ->
        match model.SelectedFilter, model.Entries with
        | None, _ -> model, Cmd.none, NoOp
        | Some _, Success _ when nextPageLoading model ->
            // Erst die laufende Seite abwarten, dann den ganzen Ausschnitt samt ihr holen.
            { model with ListActivity = LoadingMoreThenRefresh }, Cmd.none, NoOp
        | Some _, Success rows ->
            { model with RefreshGeneration = model.RefreshGeneration + 1; ListActivity = Refreshing rows },
            Cmd.ofMsg RunRefreshEntries,
            NoOp
        | Some _, _ ->
            // Ohne geladene Liste gibt es keinen Stand zu halten.
            let rebuilt, cmd = rebuildEntries model
            rebuilt, cmd, NoOp

    | RunRefreshEntries ->
        match refreshRequest model with
        | None -> model, Cmd.none, NoOp
        | Some (query, answer) ->
            let cmd =
                Cmd.OfAsync.either
                    (Retry.reading Api.ledger.getLedgerEntriesPage)
                    query
                    (Ok >> answer)
                    (fun ex -> answer (Error ex.Message))
            model, cmd, NoOp

    // Die Antwort eines abgelösten Nachladens oder einer neu aufgebauten Liste: verwerfen.
    | EntriesRefreshed (generation, refresh, _) when generation <> model.EntriesGeneration || refresh <> model.RefreshGeneration ->
        model, Cmd.none, NoOp

    // Ein Einzelbefehl hat seit dem Start eine Zeile geändert (ledger-ct3cx, Review a3f7eb1d..982bdf79):
    // die Antwort kann ihn noch nicht kennen und setzte Zeile, Detail und Zähler auf den alten Stand.
    // Verworfen und neu gefragt, statt Zeilen und Zähler aus zwei Ständen zu mischen.
    | EntriesRefreshed (_, _, Ok _) when (match model.ListActivity with Refreshing basis -> model.Entries <> Success basis | _ -> false) ->
        { model with ListActivity = ListIdle }, Cmd.ofMsg RefreshEntries, NoOp

    | EntriesRefreshed (_, _, Ok page) ->
        let refreshed, cmd = applyEntriesRefresh page model
        refreshed, cmd, NoOp

    | EntriesRefreshed (_, _, Error message) ->
        // Die Liste bleibt mit Auswahl stehen, nur der Hinweis erscheint.
        { model with ListActivity = ListIdle },
        Cmd.none,
        PostFeedback (refreshFailedFeedback message)

    | ToggleIncludeDeleted ->
        // Reine Zustandsänderung plus Neuaufbau über `rebuildWith` (dieselbe Teilung wie
        // beim Filterwechsel `SelectFilter`): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig (ADR 0020).
        rebuildWith { model with IncludeDeleted = not model.IncludeDeleted }

    | ToggleOnlyNeedsReview ->
        // Reine Zustandsänderung plus Neuaufbau über `rebuildWith` (derselbe Schnitt wie
        // `ToggleIncludeDeleted`/ADR 0020): der Proxy-Aufruf steckt erst hinter
        // `RunLoadEntriesPage`, nicht in diesem Zweig.
        rebuildWith { model with OnlyNeedsReview = not model.OnlyNeedsReview }

    | ToggleOnlyAskLena ->
        // Wie `ToggleWithoutCategory`: reine Zustandsänderung plus Neuaufbau (ADR 0020).
        let filters = { model.Filters with OnlyAskLena = not model.Filters.OnlyAskLena }
        rebuildWith { model with Filters = filters }

    | ToggleWithoutCategory ->
        // Wie `ToggleOnlyNeedsReview`: reine Zustandsänderung plus Neuaufbau (ADR 0020).
        let filters = { model.Filters with WithoutCategory = not model.Filters.WithoutCategory }
        rebuildWith { model with Filters = filters }

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

    // Im Durchgehen schließt „Details“ nur das Detail: die Karte bleibt und liest die Buchung neu.
    | CloseDetail when model.Review |> Option.exists (fun r -> r.DetailShown) ->
        let model' =
            { clearDetailEdits model with
                SplitEditor = None
                CategoryPickerFor = None
                CategorySuggestions = [] }
            |> editReview (fun r -> { r with DetailShown = false })
        let refresh =
            match reviewCardOf model' with
            | Some card -> Cmd.ofMsg (RunReviewRefresh card.Id)
            | None -> Cmd.none
        model', refresh, NoOp
    | CloseDetail -> closeDetail model, Cmd.none, NoOp

    | DetailFormChanged form ->
        { model with DetailForm = form }, Cmd.none, NoOp

    | ListInteractionChanged interaction ->
        { model with ListInteraction = interaction }, Cmd.none, NoOp

    // Nachladen derselben Buchung lässt die gezeigte Historie stehen, bis die Antwort da ist; das Öffnen
    // einer Buchung setzt vorher `Loading` (`showDetail`, `ReviewShowDetails`). Ohne offenes Detail der
    // Buchung gibt es nichts zu laden.
    | LoadHistory entryId when model.DetailFor |> Option.exists (fun e -> e.Id = entryId) ->
        let generation = model.HistoryGeneration + 1
        let answer = historyAnswer entryId generation
        let cmd =
            Cmd.OfAsync.either
                (fun id -> Retry.reading Api.ledger.getEntryHistory id)
                entryId
                (fun lines -> answer (Ok lines))
                (fun ex -> answer (Error ex.Message))
        let history =
            match model.History with
            | Success _ -> model.History
            | _ -> Loading
        { model with History = history; HistoryGeneration = generation }, cmd, NoOp
    | LoadHistory _ -> model, Cmd.none, NoOp

    | HistoryLoaded (entryId, generation, Ok lines) when isCurrentHistory entryId generation model ->
        { model with History = Success lines }, Cmd.none, NoOp

    | HistoryLoaded (entryId, generation, Error message) when isCurrentHistory entryId generation model ->
        { model with History = RemoteData.lateFailure message model.History }, Cmd.none, NoOp

    // Die Historie einer Buchung, die nicht mehr im Detail steht (schnelles Weiterspringen,
    // geschlossenes Detail), oder die eines überholten Ladens: verwerfen.
    | HistoryLoaded _ -> model, Cmd.none, NoOp

    | ToggleHistory ->
        let detailId = model.DetailFor |> Option.map (fun d -> d.Id)
        match detailId with
        | Some _ when model.HistoryOpenFor = detailId -> { model with HistoryOpenFor = None }, Cmd.none, NoOp
        | Some _ -> { model with HistoryOpenFor = detailId }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    // ============================================
    // Spiegel
    // ============================================
    | LoadMirrorStatus ->
        // Hinter einem Lambda, damit `update` unter .NET läuft. Ein gelesener Stand bleibt beim Nachlesen
        // stehen, „Gespiegelt …“ verschwindet nicht (ledger-pfhdq).
        let generation = model.MirrorStatusGeneration + 1
        let cmd =
            Cmd.OfAsync.either
                (fun () -> Retry.reading Api.ledger.getLastMirrorRun ())
                ()
                (fun status -> MirrorStatusLoaded (generation, Ok status))
                (fun ex -> MirrorStatusLoaded (generation, Error ex.Message))
        let lastRun =
            match model.LastMirrorRun with
            | Success _ -> model.LastMirrorRun
            | _ -> Loading
        { model with LastMirrorRun = lastRun; MirrorStatusGeneration = generation }, cmd, NoOp

    | MirrorStatusLoaded (generation, _) when generation <> model.MirrorStatusGeneration ->
        // Antwort auf einen älteren Abruf (ledger-pfhdq): fällt weg und startet keinen Spiegel-Lauf.
        model, Cmd.none, NoOp

    | MirrorStatusLoaded (_, Ok status) ->
        // Frische-Kriterium (ADR 0014): ist der geladene Lauf-Status älter als die
        // Schwelle (oder gab es noch nie einen), stößt dieselbe Seite denselben
        // Hintergrund-Lauf an wie der manuelle Knopf, nur ohne den zusätzlichen
        // Nutzer-Klick. Läuft bereits ein Lauf, greift dieselbe Doppeldruck-Sperre wie
        // bei `StartMirror`: kein zweiter Auslöser aus diesem Tab. Der serverseitige
        // Einzel-Flug-Schutz (`Ledger.CommandHandlers.runMirrorGuarded`) hält zusätzlich
        // gegen einen zweiten Tab oder einen zeitgleichen Lauf-nach-Push.
        let model' = { model with LastMirrorRun = Success status }
        if not model.MirrorRunning && isMirrorStale DateTime.UtcNow status then
            // Auch der Lauf im Hintergrund meldet sich in der Leiste, bis sein Ergebnis die Meldung ersetzt (ledger-gwr9j).
            // Er ist keine Aktion: eine stehende Meldung mit Knopf („Rückgängig“) ersetzt er nicht.
            { model' with MirrorRunning = true; MirrorInBackground = true }, Cmd.ofMsg RunMirror, PostFeedbackIfFree mirrorRunningFeedback
        else
            model', Cmd.none, NoOp

    | MirrorStatusLoaded (_, Error message) ->
        // Ein gelesener Stand bleibt ohne Hinweis stehen: „Gespiegelt …“ ist eine Auskunft, ein
        // gescheiterter Lauf meldet sich selbst per Toast und Lauf-Status (ADR 0014).
        { model with LastMirrorRun = RemoteData.lateFailure message model.LastMirrorRun }, Cmd.none, NoOp

    | StartMirror ->
        // Doppeldruck-Sperre: ein zweiter Druck waehrend eines Laufs tut nichts.
        // Die Liste bleibt stehen — sie wird erst nach dem Lauf neu geladen.
        if model.MirrorRunning then
            model, Cmd.none, NoOp
        else
            { model with MirrorRunning = true; MirrorInBackground = false }, Cmd.ofMsg RunMirror, PostFeedback mirrorRunningFeedback

    | BookingsScrollRemembered y -> { model with BookingsScroll = Some y }, Cmd.none, NoOp
    | BookingsScrollDropped -> { model with BookingsScroll = None }, Cmd.none, NoOp

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
        // Staende mitbringen. `AccountsRefreshed` zieht die Buchungen des gewaehlten
        // Filters selbst nach (Regression ledger-kt0wq): im Hintergrund, solange der Filter
        // derselbe bleibt und eine Liste steht, sonst als Neuaufbau (ledger-ct3cx).
        // „Gespiegelt“ nach einem Lauf im Hintergrund ersetzt wie sein „Spiegeln läuft …“ keine Meldung mit Knopf.
        { model with MirrorRunning = false; MirrorInBackground = false },
        Cmd.batch [ Cmd.ofMsg RefreshAccounts; Cmd.ofMsg LoadBalances; Cmd.ofMsg LoadMirrorStatus ],
        (if model.MirrorInBackground then PostFeedbackIfFree (mirrorDoneFeedback summary)
         else PostFeedback (mirrorDoneFeedback summary))

    | MirrorFinished (Error message) ->
        // Ein Fehler wird sichtbar gemacht, nie still geschluckt (ADR 0014): als
        // Toast jetzt und als Lauf-Status dauerhaft.
        { model with MirrorRunning = false; MirrorInBackground = false },
        Cmd.ofMsg LoadMirrorStatus,
        PostFeedback (mirrorFailedFeedback message)

    // ============================================
    // Filterleiste und Summenzeile (ledger-j8xq4)
    // ============================================

    // Kommt nie an: der Root (`src/Client/State.fs`) fängt `LedgerMsg LoadCategories` ab, setzt den
    // Ladezustand und ruft mit dem Budget aus seinen Settings ab.
    | LoadCategories -> model, Cmd.none, NoOp

    | CategoriesLoaded (generation, result) ->
        // Die Antwort beendet den laufenden Abruf, wenn sie zu ihm gehört. Den Stand setzt sie nur, wenn
        // seit ihrem Start weder ein neuer Abruf noch eine Fortschreibung kam (ledger-pfhdq).
        let fetch =
            match model.CategoriesFetch with
            | CategoriesFetching running when running = generation -> CategoriesIdle
            | other -> other
        let finished = { model with CategoriesFetch = fetch }
        if generation <> model.CategoriesGeneration then
            finished, Cmd.none, NoOp
        else
            match result with
            | Ok categories ->
                { finished with Categories = Success categories; CategoriesLoadedAt = Some DateTime.Now }, Cmd.none, NoOp
            | Error message ->
                // Ein vorhandener Stand bleibt sichtbar (ledger-kwwxz); nur ohne Stand wird der Fehler zum Zustand.
                { finished with Categories = RemoteData.lateFailure message model.Categories }, Cmd.none, NoOp

    // Kommt nie an: der Root (`src/Client/State.fs`) fängt `LedgerMsg EnsureCategoriesFresh` ab und leitet bei
    // altem Stand auf `LoadCategories` um.
    | EnsureCategoriesFresh -> model, Cmd.none, NoOp

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
            | AmountChip ->
                { model with
                    AmountMinDraft = model.Filters.AmountMin |> Option.map formatAmountDraft |> Option.defaultValue ""
                    AmountMaxDraft = model.Filters.AmountMax |> Option.map formatAmountDraft |> Option.defaultValue "" }
            | NeedsReviewChip -> model
            // Ein Umschalter ohne Popover wie "Zu prüfen".
            | AskLenaChip -> model
            // Ein Umschalter ohne Popover (ADR 0048): es gibt keinen Entwurf zu setzen.
            | WithoutCategoryChip -> model
        { model' with OpenPopover = Some chip }, Cmd.none, NoOp

    | CloseFilterPopover ->
        // Esc, Klick daneben oder "Schließen": der Entwurf wird verworfen, `Filters`
        // bleibt unangetastet (AC "der vorherige Filterzustand bleibt unverändert").
        { model with OpenPopover = None }, Cmd.none, NoOp

    // -------- Filter-Knöpfe im Detail (ADR 0053, löst das Zellmenü aus ledger-rfq5p ab) --------

    // Beide lassen das Detail offen und mit derselben Buchung stehen: Filtern ändert die
    // Liste, nicht die Buchung.
    | FilterByRowPayee payee ->
        // Die Suche gilt sofort und steht im Feld; die Version steigt, damit eine wartende
        // Eingabe sie nicht überschreibt (ledger-d9z3n). „alle zeigen“ am Payee schließt am Handy (Sheet)
        // das Detail, damit die gefundenen Buchungen zu sehen sind; am PC bleibt das Panel (ledger-zfj78).
        let filters = { model.Filters with Search = Some payee }
        let model = if payeeShowAllClosesDetail model then closeDetail model else model
        rebuildWith { model with Filters = filters; SearchDraft = payee; SearchVersion = model.SearchVersion + 1 }

    | FilterByRowCategory categoryId ->
        // "Ohne Kategorie" verbindet sich mit einer Kategorienauswahl per Oder (ADR 0048)
        // und würde Buchungen ohne Ziel einschließen, die Roman nicht gewählt hat.
        let filters =
            { model.Filters with CategoryIds = Set.singleton categoryId; WithoutCategory = false }
        rebuildWith { model with Filters = filters }

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
            let rebuilt, load = rebuildEntries { model with Filters = filters; OpenPopover = None; RecentCategories = recent }
            rebuilt, Cmd.batch [ load; remember ], NoOp

    | RemoveCategoryFilter ->
        let filters = { model.Filters with CategoryIds = Set.empty }
        rebuildWith { model with Filters = filters }

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
        rebuildWith { model with Filters = filters; OpenPopover = None }

    | RemoveDateFilter ->
        let filters = { model.Filters with DateRange = None }
        rebuildWith { model with Filters = filters }

    // -------- Suche (Suchfeld mit Verzögerung, ledger-d9z3n) --------

    | SetSearchDraft text ->
        let newVersion = model.SearchVersion + 1
        { model with SearchDraft = text; SearchVersion = newVersion },
        Debounce.delayedDefault (CommitSearch (text, newVersion)),
        NoOp

    | CommitSearch (text, version) ->
        if version <> model.SearchVersion then
            // Eine spätere Eingabe hat diese Verzögerung schon überholt.
            model, Cmd.none, NoOp
        else
            let search = searchOfText text
            if search = model.Filters.Search then
                model, Cmd.none, NoOp
            else
                rebuildWith { model with Filters = { model.Filters with Search = search } }

    | RemoveSearch ->
        // Die Version steigt: ein wartendes `CommitSearch` holt die entfernte Suche nicht zurück (ledger-ct3cx).
        rebuildWith
            { model with
                Filters = { model.Filters with Search = None }
                SearchDraft = ""
                SearchVersion = model.SearchVersion + 1 }

    // -------- Filter-Sheet unter 1024 px (ledger-d9z3n) --------

    | OpenFilterSheet ->
        // Die Betragsfelder zeigen den geltenden Filter, wie beim Öffnen des Betrags-Popovers. Das Sheet beginnt mit
        // den Filtern, nie in einer Auswahl (ledger-npqcb).
        { model with
            FilterSheetOpen = true
            OpenPopover = None
            SortSheetOpen = false
            AmountMinDraft = model.Filters.AmountMin |> Option.map formatAmountDraft |> Option.defaultValue ""
            AmountMaxDraft = model.Filters.AmountMax |> Option.map formatAmountDraft |> Option.defaultValue "" },
        Cmd.none,
        NoOp

    | CloseFilterSheet ->
        // Schließen schließt das ganze Sheet, auch aus einer Auswahl heraus (ledger-npqcb); ihr Entwurf verfällt.
        if model.FilterSheetOpen then
            { model with FilterSheetOpen = false; OpenPopover = None; SortSheetOpen = false }, Cmd.none, NoOp
        else
            model, Cmd.none, NoOp

    | BackToFilterSheet ->
        // „Zurück“ aus Kategorie, Zeitraum oder Sortieren: die Filter stehen wieder, der Entwurf verfällt.
        if model.FilterSheetOpen then
            { model with OpenPopover = None; SortSheetOpen = false }, Cmd.none, NoOp
        else
            model, Cmd.none, NoOp

    // -------- Betrag (Suchfeld mit Verzögerung, Von/Bis) --------

    // Ein unlesbarer Text lässt seine Grenze stehen (`amountFilterValue`, ledger-dn4vy): vorher nahm „abc“
    // einen aktiven Filter still weg.
    // Die Felder nehmen kein Vorzeichen an (ADR 0095): „-“ und „+“ fallen schon beim Tippen weg.
    | SetAmountMinDraft typed ->
        let text = withoutAmountSign typed
        let newVersion = model.AmountVersion + 1
        let filters = model.Filters
        { model with AmountMinDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (
            CommitAmountFilter (amountFilterValue filters.AmountMin text, amountFilterValue filters.AmountMax model.AmountMaxDraft, newVersion)
        ),
        NoOp

    | SetAmountMaxDraft typed ->
        let text = withoutAmountSign typed
        let newVersion = model.AmountVersion + 1
        let filters = model.Filters
        { model with AmountMaxDraft = text; AmountVersion = newVersion },
        Debounce.delayedDefault (
            CommitAmountFilter (amountFilterValue filters.AmountMin model.AmountMinDraft, amountFilterValue filters.AmountMax text, newVersion)
        ),
        NoOp

    | CommitAmountFilter (min_, max_, version) ->
        if version <> model.AmountVersion then
            model, Cmd.none, NoOp
        else
            let filters = { model.Filters with AmountMin = min_; AmountMax = max_ }
            rebuildWith { model with Filters = filters }

    | RemoveAmountFilter ->
        let filters = { model.Filters with AmountMin = None; AmountMax = None }
        // Wie `RemoveSearch`: ein wartendes `CommitAmountFilter` ist überholt (ledger-ct3cx).
        rebuildWith { model with Filters = filters; AmountMinDraft = ""; AmountMaxDraft = ""; AmountVersion = model.AmountVersion + 1 }

    | RemoveAmountAndDirection ->
        let filters = { model.Filters with AmountMin = None; AmountMax = None; AmountDirection = AllDirections }
        rebuildWith { model with Filters = filters; AmountMinDraft = ""; AmountMaxDraft = ""; AmountVersion = model.AmountVersion + 1 }

    | SetAmountDirection direction ->
        // Wirkt sofort wie ein Schalter, ohne Verzögerung (ADR 0095).
        if direction = model.Filters.AmountDirection then
            model, Cmd.none, NoOp
        else
            rebuildWith { model with Filters = { model.Filters with AmountDirection = direction } }

    | ClearAllFilters ->
        // Alle Filter, auch „Zu prüfen“, „Lena“, „Ohne Kategorie“, Sortierung, „Gelöschte zeigen“
        // und die Suche; das Konto bleibt (ledger-d9z3n). `clearAllFilters` ist rein und testbar.
        rebuildWith (clearAllFilters model)

    // ============================================
    // Prüfvermerk setzen und zurückschreiben (ledger-r777c, ADR 0050)
    // ============================================

    | StartSetReviewMark (entryId, target) ->
        whenRowIdle ReviewMarkLock entryId model (fun () ->
            { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
            Cmd.ofMsg (RunSetReviewMark (entryId, target)),
            NoOp)

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
        let model' = applyReviewMarkResult entryId needsReview (writeBackFailureOf outcome) model
        model', afterEntryChange entryId model', NoOp

    | ReviewMarkSet (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    | StartRetryReviewMark entryId ->
        whenRowIdle ReviewMarkLock entryId model (fun () ->
            { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
            Cmd.ofMsg (RunRetryReviewMark entryId),
            NoOp)

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
        let model' = applyReviewMarkResult entryId None failureOf model
        model', afterEntryChange entryId model', NoOp

    | ReviewMarkRetried (entryId, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Erneut senden am Hinweis "Noch nicht in YNAB" (ledger-cks06)
    // ============================================

    | StartRetryPush entryId ->
        whenRowIdle PushLock entryId model (fun () ->
            { model with PushRetryRows = model.PushRetryRows |> Map.add entryId PushSending },
            Cmd.ofMsg (RunRetryPush entryId),
            NoOp)

    | RunRetryPush entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryPushToYnab
                entryId
                (fun result -> PushRetried (entryId, result))
                (fun ex -> PushRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    // Im offenen Detail meldet „Erneut senden“ nichts (ledger-enjex): nach dem Erfolg verschwinden die Hinweisleiste
    // „Noch nicht in YNAB“ und die Marke, ein neuer Grund steht in der Hinweisleiste (Zustand, keine Meldung).
    | PushRetried (entryId, Ok result) ->
        let toast =
            match result with
            | _ when detailShows model entryId -> NoOp
            | PushToYnabSent -> ShowToast (pushRetrySentToastText, ToastSuccess)
            | PushToYnabFailed reason -> ShowToast ($"{pushRetryFailedToastTitle}: {reason}", ToastError)
        let model' = applyPushRetryResult entryId DateTime.UtcNow result model
        model', afterEntryChange entryId model', toast

    // Ein abgelehnter Befehl im offenen Detail: der Grund steht als neuer Grund in der Hinweisleiste.
    | PushRetried (entryId, Error message) when detailShows model entryId ->
        applyPushRetryResult entryId DateTime.UtcNow (PushToYnabFailed message) model, Cmd.none, NoOp

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
        whenRowIdle ReviewMarkLock entryId model (fun () ->
            match entry with
            | Some e when swipeAppliesTo e ->
                { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
                Cmd.ofMsg (RunSwipeReviewed entryId),
                NoOp
            | _ -> model, Cmd.none, NoOp)

    | RunSwipeReviewed entryId ->
        let payee =
            match model.Entries with
            | Success entries -> entries |> List.tryFind (fun e -> e.Id = entryId) |> Option.bind (fun e -> e.Payee)
            | _ -> None
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setReviewMark entryId)
                Reviewed
                (fun result -> SwipeReviewedDone (entryId, payee, result))
                (fun ex -> SwipeReviewedDone (entryId, payee, Error ex.Message))
        model, cmd, NoOp

    | SwipeReviewedDone (entryId, payee, Ok outcome) ->
        let model' = applySwipeReviewed entryId outcome model
        let external =
            match outcome.WriteBack with
            | ReviewMarkSendFailed reason -> PostFeedback (reviewMarkNotInYnabFeedback false reason)
            | _ when outcome.Change = ReviewMarkUnchanged -> NoOp
            | _ -> PostFeedback (swipeReviewedFeedback payee entryId outcome.Previous)
        model', afterEntryChange entryId model', external

    | SwipeReviewedDone (entryId, _, Error message) ->
        { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) },
        Cmd.none,
        PostFeedback (failedFeedback "Nicht geprüft" message)

    | StartUndoReviewMark (entryId, previous) ->
        whenRowIdle ReviewMarkLock entryId model (fun () ->
            { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId ReviewMarkSending },
            Cmd.ofMsg (RunUndoReviewMark (entryId, previous)),
            NoOp)

    | RunUndoReviewMark (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.restoreReviewMark entryId)
                previous
                (fun result -> ReviewMarkRestored (entryId, previous, result))
                (fun ex -> ReviewMarkRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkRestored (entryId, previous, Ok outcome) ->
        match takeAwait ((=) (AwaitUndo (UndoConfirmed (entryId, previous)))) model with
        // „Rückgängig“ nach „Passt“ im Durchgehen (ledger-pz5wv): die Karte steht schon wieder da, die Zeile zeigt den
        // Hinweis; nur eine YNAB-Warnung steht gelb in der Zeile, kein Toast.
        | Some (_, settled) ->
            let model' = applyUndoReviewMark entryId previous outcome settled
            let model' =
                match outcome.WriteBack with
                | ReviewMarkSendFailed reason -> setReviewLast (Some (reviewLastFor model' entryId LastUndone (LastNotInYnab reason) None)) model'
                | _ -> model'
            model', afterEntryChange entryId model', NoOp
        | None ->
            let model' = applyUndoReviewMark entryId previous outcome model
            let external =
                match outcome.WriteBack with
                | ReviewMarkSendFailed reason -> PostFeedback (reviewMarkNotInYnabFeedback true reason)
                | _ -> NoOp
            model', afterEntryChange entryId model', external

    | ReviewMarkRestored (entryId, previous, Error message) ->
        let failed = { model with ReviewMarkRows = model.ReviewMarkRows |> Map.add entryId (ReviewMarkCommandFailed message) }
        let undo = UndoConfirmed (entryId, previous)
        match takeAwait ((=) (AwaitUndo undo)) failed with
        // Die Karte bleibt stehen, die Zeile zeigt rot „Rückgängig nicht möglich“ mit „Erneut“.
        | Some (_, settled) ->
            setReviewLast (Some (reviewLastFor settled entryId LastUndone (LastFailed message) (Some (LastRetry (RetryUndo undo))))) settled,
            Cmd.none,
            NoOp
        | None -> failed, Cmd.none, PostFeedback (undoFailedFeedback message)

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
            // Die Konten des Budgets für den Abschnitt "Übertrag auf Konto" (ledger-qvjvs).
            let loadTargets =
                match model.TransferTargets with
                | Success _ | Loading -> Cmd.none
                | _ -> Cmd.ofMsg RunLoadTransferTargets
            { model with CategoryPickerFor = Some entryId; CategorySuggestions = []; CategoryCard = None },
            // Der Aufruf je Öffnung lädt die Kategorien nur bei Stand über zwei Minuten nach
            // (ledger-kwwxz) und holt die Vorschläge zum Payee.
            Cmd.batch [ Cmd.ofMsg EnsureCategoriesFresh; suggestions; loadTargets ],
            NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseCategoryPicker -> { model with CategoryPickerFor = None; CategorySuggestions = [] }, Cmd.none, NoOp

    | RunLoadCategorySuggestions (entryId, payee, current) ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading (Api.ledger.getPayeeCategorySuggestions payee))
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
        whenRowIdle CategoryLock entryId model (fun () ->
            match findEntry model entryId with
            | Some entry when categoryLockFor entry = CategoryEditable ->
                { clearDetailNote entryId (clearReviewField entryId model) with
                    CategoryRows = model.CategoryRows |> Map.add entryId CategorySending
                    CategoryPickerFor = None
                    CategorySuggestions = []
                    CategoryCard = None
                    RuleOffer = None
                    RecentCategories = rememberPickedCategory newId model.RecentCategories },
                Cmd.batch [
                    Cmd.ofMsg (RunSetEntryCategory (entryId, newId))
                    Cmd.ofEffect (fun _ -> writeRecentCategories (rememberPickedCategory newId model.RecentCategories))
                ],
                NoOp
            | _ -> model, Cmd.none, NoOp)

    | RunSetEntryCategory (entryId, newId) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryCategory entryId)
                newId
                (fun result -> EntryCategorySet (entryId, newId, result))
                (fun ex -> EntryCategorySet (entryId, newId, Error ex.Message))
        model, cmd, NoOp

    | EntryCategorySet (entryId, newId, Ok outcome) ->
        let model', cmd = applyCategoryResult entryId newId None true outcome model |> afterTransferCleared entryId outcome
        // Das Angebot „Regel anlegen?“: erst fragt der Server, ob eine Regel die Kategorie schon gibt (ledger-hg5m5).
        let model', cmd =
            match outcome.Change, ruleOfferCheckFor model' entryId newId with
            | CategoryChanged, Some (payee, memo, categoryId) ->
                { model' with RuleOffer = Some (RuleOfferChecking (entryId, categoryId)) },
                Cmd.batch [ cmd; Cmd.ofMsg (RunCheckRuleOffer (entryId, payee, memo, categoryId)) ]
            | _ -> model', cmd
        let newName = categoryOfId model newId |> Option.map (fun c -> c.Name)
        let route = detailNoteRoute model entryId
        let payee = findEntry model entryId |> Option.bind (fun e -> e.Payee)
        let cmd = Cmd.batch [ cmd; afterEntryChange entryId model' ]
        match outcome.Change, outcome.WriteBack with
        // Die Karte des Durchgehens ohne „Details“ (ledger-mf2bg): gelb unter dem Knopf statt Toast.
        | CategoryUnchanged, CategorySendFailed reason when route = NoteOnReviewCard ->
            reviewFieldNotInYnab entryId LastCategoryChanged reason model', cmd, NoOp
        | CategoryUnchanged, CategorySendFailed reason ->
            reportDetailNote
                route
                (notInYnabDetailNote entryId AtCategory reason)
                (ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning))
                (model', cmd)
        | CategoryUnchanged, _ -> model', cmd, NoOp
        | CategoryChanged, writeBack ->
            let failure = match writeBack with CategorySendFailed reason -> Some reason | _ -> None
            let undo = undoKindFor entryId outcome
            match route with
            // Die Karte des Durchgehens ohne „Details“ (ledger-mf2bg): das Feld leuchtet, darunter Name und Verfügbares
            // mit „Rückgängig“; nimmt YNAB sie nicht, steht das gelb unter dem Knopf. Kein Toast.
            | NoteOnReviewCard ->
                let text = reviewCategoryNoteText newName (categoryOfId model' newId |> Option.bind (fun c -> c.Available))
                let noted, timer = postReviewFieldNote entryId text undo model'
                let noted = failure |> Option.fold (fun m reason -> reviewFieldNotInYnab entryId LastCategoryChanged reason m) noted
                noted, Cmd.batch [ cmd; timer ], NoOp
            | _ -> reportDetailNote route (categoryChangedNote entryId payee newId.IsNone failure undo) NoOp (model', cmd)

    | EntryCategorySet (entryId, newId, Error message) ->
        categoryCommandFailed
            entryId
            message
            (failedDetailNote entryId AtCategory "Nicht geändert" "Kategorie nicht geändert" message)
            (LastCategoryChanged, RetryCategory (entryId, newId))
            model

    | StartUndoCategory (entryId, previous) ->
        whenRowIdle CategoryLock entryId model (fun () ->
            // „Rückgängig“ nimmt mit der Kategorie auch das Angebot weg (ledger-hg5m5).
            { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending; RuleOffer = None },
            Cmd.ofMsg (RunUndoCategory (entryId, previous)),
            NoOp)

    | RunUndoCategory (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryCategory entryId)
                (previous |> Option.map (fun p -> p.CategoryId))
                (fun result -> EntryCategoryRestored (entryId, previous, result))
                (fun ex -> EntryCategoryRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | EntryCategoryRestored (entryId, previous, Ok outcome) ->
        // Ohne neue Karte, mit vertauschten Kategorien in Available. Im Detail steht danach die alte Kategorie im Feld,
        // die Bestätigung verschwindet (ledger-p5y9s); eine YNAB-Warnung bleibt gelb am Feld.
        let newId = previous |> Option.map (fun p -> p.CategoryId)
        let fallback = previous |> Option.map (fun p -> p.CategoryName)
        let model', cmd = applyCategoryResult entryId newId fallback false outcome model |> afterTransferCleared entryId outcome
        let name = categoryOfId model newId |> Option.map (fun c -> c.Name) |> Option.orElse fallback
        let cmd = Cmd.batch [ cmd; afterEntryChange entryId model' ]
        if detailNoteRoute model entryId = NoteOnReviewCard then
            reviewFieldRestored entryId outcome (model', cmd)
        else
            restoredReport
                (detailNoteRoute model entryId)
                entryId
                AtCategory
                outcome
                (categoryRestoredToastText name)
                (clearDetailNote entryId model', cmd)

    | EntryCategoryRestored (entryId, previous, Error message) ->
        categoryCommandFailed
            entryId
            message
            (undoFailedDetailNote entryId AtCategory message)
            (LastUndone, RetryFieldUndo (UndoCategory (entryId, previous)))
            model

    | StartSetEntryTransfer (entryId, account, undo) ->
        whenRowIdle CategoryLock entryId model (fun () ->
            match findEntry model entryId with
            | Some entry when categoryLockFor entry = CategoryEditable ->
                let model = if undo then model else clearDetailNote entryId (clearReviewField entryId model)
                { model with
                    CategoryRows = model.CategoryRows |> Map.add entryId CategorySending
                    CategoryPickerFor = None
                    CategorySuggestions = []
                    CategoryCard = None },
                Cmd.ofMsg (RunSetEntryTransfer (entryId, account, undo)),
                NoOp
            | _ -> model, Cmd.none, NoOp)

    | RunSetEntryTransfer (entryId, account, undo) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryTransfer entryId)
                account
                (fun result -> EntryTransferSet (entryId, account, undo, result))
                (fun ex -> EntryTransferSet (entryId, account, undo, Error ex.Message))
        model, cmd, NoOp

    | EntryTransferSet (entryId, account, undo, Ok outcome) ->
        let model' = applyTransferResult entryId account outcome model
        let (YnabAccountId accountGuid) = account
        let name = transferAccountName model accountGuid
        let failed = match outcome.WriteBack with CategorySendFailed reason -> Some reason | _ -> None
        let refresh = if outcome.Change = CategoryChanged then Cmd.ofMsg (RunRefreshEntry entryId) else Cmd.none
        let cmd = Cmd.batch [ refresh; afterEntryChange entryId model' ]
        let route = detailNoteRoute model entryId
        match outcome.Change, failed with
        // Die Karte des Durchgehens ohne „Details“ (ledger-mf2bg): „Gegenbuchung angelegt“ am Feld, Warnung gelb und
        // Rücknahme ohne Meldung wie bei der Kategorie. Kein Toast.
        | CategoryUnchanged, Some reason when route = NoteOnReviewCard ->
            reviewFieldNotInYnab entryId (if undo then LastUndone else LastTransferSet) reason model', cmd, NoOp
        | CategoryChanged, _ when undo && route = NoteOnReviewCard -> reviewFieldRestored entryId outcome (model', cmd)
        | CategoryChanged, _ when route = NoteOnReviewCard ->
            let noted, timer = postReviewFieldNote entryId (reviewTransferNoteText, None) (undoKindFor entryId outcome) model'
            let noted = failed |> Option.fold (fun m reason -> reviewFieldNotInYnab entryId LastTransferSet reason m) noted
            noted, Cmd.batch [ cmd; timer ], NoOp
        | CategoryUnchanged, Some reason ->
            reportDetailNote
                route
                (notInYnabDetailNote entryId AtCategory reason)
                (ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning))
                (model', cmd)
        | CategoryUnchanged, None -> model', cmd, NoOp
        | CategoryChanged, _ when undo ->
            restoredReport route entryId AtCategory outcome (transferUndoneToastText name) (clearDetailNote entryId model', cmd)
        | CategoryChanged, _ ->
            let kind = undoKindFor entryId outcome
            let payee = findEntry model entryId |> Option.bind (fun e -> e.Payee)
            reportDetailNote route (transferSetNote entryId name payee failed kind) NoOp (model', cmd)

    | EntryTransferSet (entryId, account, undo, Error message) ->
        let note, onCard =
            if undo then undoFailedDetailNote entryId AtCategory message, (LastUndone, RetryFieldUndo (UndoTransfer (entryId, account)))
            else
                failedDetailNote entryId AtCategory "Nicht gesetzt" "Übertrag nicht gesetzt" message,
                (LastTransferSet, RetryTransfer (entryId, account))
        categoryCommandFailed entryId message note onCard model

    | StartRetryCategorization entryId ->
        whenRowIdle CategoryLock entryId model (fun () ->
            { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending },
            Cmd.ofMsg (RunRetryCategorization entryId),
            NoOp)

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
        // Erfolg beendet einen offenen Ersatz (ledger-f8bjm), wie beim Speichern der Aufteilung.
        let locksOf (entry: LedgerEntryView) =
            match writeBack with
            | CategorySent -> entry.SplitLocks |> Option.map (fun l -> { l with ReplacementPending = false })
            | _ -> entry.SplitLocks
        let model' =
            match findEntry cleared entryId with
            | Some entry ->
                replaceEntry { entry with CategorizationWriteBackFailure = failure; SplitLocks = locksOf entry } cleared
            | None -> cleared
        // Im offenen Detail zeigt die Hinweisleiste „Kategorie nicht in YNAB“ den neuen Grund (Zustand, keine
        // Meldung, ledger-p5y9s).
        let external =
            match writeBack with
            | CategorySendFailed _ when detailShows model entryId -> NoOp
            | CategorySendFailed reason -> ShowToast ($"{categoryHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', afterEntryChange entryId model', external

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
        // Eine geladene Farbe bleibt, bis die neue da ist.
        let color =
            match model.LenaFlagColor with
            | Success _ -> model.LenaFlagColor
            | _ -> Loading
        { model with LenaFlagColor = color }, cmd, NoOp

    | LenaFlagSettingsLoaded (Ok settings) ->
        { model with LenaFlagColor = Success settings.SavedColor }, Cmd.none, NoOp

    // Ohne die Farbe bleibt "Lena fragen" gesperrt. Ein Ladefehler ist kein Toast wert, nennt
    // aber einen eigenen Grund (`lenaColorLoadFailedReason`, ledger-vq2g8).
    | LenaFlagSettingsLoaded (Error message) ->
        { model with LenaFlagColor = RemoteData.lateFailure message model.LenaFlagColor }, Cmd.none, NoOp

    | StartSetLenaFlag (entryId, target) ->
        whenRowIdle FlagLock entryId model (fun () ->
            match findEntry model entryId with
            | Some entry when (lenaActionFor model.LenaFlagColor entry).DisabledReason.IsNone
                              && (lenaActionFor model.LenaFlagColor entry).Target = target ->
                // Die neue Aktion nimmt eine stehende Bestätigung im Detail weg (ledger-enjex).
                let model = clearDetailNote entryId model
                { model with FlagRows = model.FlagRows |> Map.add entryId FlagSending },
                Cmd.ofMsg (RunSetLenaFlag (entryId, target)),
                NoOp
            | _ -> model, Cmd.none, NoOp)

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
            | AssignLena -> { Color = lenaColorOf model.LenaFlagColor; Name = Some lenaFlagName }
            | WithdrawLena -> { Color = None; Name = None }
        match takeAwait ((=) (AwaitLena entryId)) model with
        // „Lena fragen“ auf der Karte (ledger-pz5wv): die Zeile unter „Passt, weiter“ statt Toast. „Rückfrage
        // zurücknehmen“ schreibt keine Zeile, der Knopf wechselt sichtbar und ist selbst das Rückgängig.
        | Some (_, settled) ->
            let model' = applyFlagResult entryId newState outcome settled
            let model' =
                match target with
                | AssignLena ->
                    let result =
                        match outcome.WriteBack with
                        | FlagSendFailed reason -> LastNotInYnab reason
                        | _ -> LastDone
                    let undo = Some (LastUndo (UndoLenaAsked (entryId, outcome.Previous)))
                    setReviewLast (Some (reviewLastFor model' entryId LastLenaAsked result undo)) model'
                | WithdrawLena -> model'
            model', afterEntryChange entryId model', NoOp
        | None ->
            let model' = applyFlagResult entryId newState outcome model
            let cmd = afterEntryChange entryId model'
            match outcome.Change, outcome.WriteBack with
            | FlagUnchanged, _ -> model', cmd, NoOp
            | FlagChanged, writeBack ->
                let failure = match writeBack with FlagSendFailed reason -> Some reason | _ -> None
                // Im offenen Detail wechselt nur der Knopf und leuchtet; bei vorherigem fremdem Flag steht darunter
                // „✓ Lena gefragt · Rückgängig“ (ledger-enjex). Sonst (Detail zu, Taste L) meldet der Toast wie
                // bisher, unter 1024 px in der Leiste.
                if detailShows model entryId then
                    let payee = findEntry model entryId |> Option.bind (fun e -> e.Payee)
                    let model'', timer = postDetailNote (lenaFlagNote entryId target outcome.Previous payee failure) model'
                    model'', Cmd.batch [ cmd; timer ], NoOp
                else
                    let text = lenaFlagToastText target
                    let action = { Label = undoToastLabel; Kind = UndoFlag (entryId, outcome.Previous) }
                    match failure with
                    | Some reason -> model', cmd, ShowToastWithAction ($"{text}. {flagHintTitle}: {reason}", ToastWarning, action)
                    | None -> model', cmd, ShowToastWithAction (text, ToastSuccess, action)

    // Im Durchgehen ist die Karte nach „Lena fragen“ schon weiter: der Fehler holt sie zurück, damit Roman
    // ihn an der Buchung sieht (ledger-nc708). Außerhalb des Durchgehens bleibt alles, wo es ist. Steht auf
    // der gezeigten Karte eine Eingabe offen, bleibt die Karte: der Sprung schlösse sie samt Entwurf
    // (Review a3f7eb1d..982bdf79). Fehler an der Zeile und Toast bleiben.
    // Im offenen Detail steht der Fehler rot unter dem Knopf, ohne Toast (ledger-enjex).
    | LenaFlagSet (entryId, _, Error message) when detailShows model entryId
                                                   && model.Review |> Option.forall (fun r -> not (r.Awaiting.Contains (AwaitLena entryId))) ->
        let model', timer =
            postDetailNote
                (failedDetailNote entryId AtLena "Nicht geändert" "Flag nicht geändert" message)
                { model with FlagRows = model.FlagRows |> Map.remove entryId }
        model', timer, NoOp

    | LenaFlagSet (entryId, _, Error message) ->
        let returned, showCmd =
            if reviewCardEditing model then model, Cmd.none else returnToReviewCard entryId model
        let failed = { returned with FlagRows = returned.FlagRows |> Map.add entryId (FlagCommandFailed message) }
        match takeAwait ((=) (AwaitLena entryId)) failed with
        // Von der Karte aus: rot in der Zeile mit „Erneut“ statt Toast (ledger-pz5wv).
        | Some (_, settled) ->
            let line = reviewLastFor settled entryId LastLenaAsked (LastFailed message) (Some (LastRetry (RetryLena entryId)))
            setReviewLast (Some line) settled, showCmd, NoOp
        | None -> failed, showCmd, ShowToast ($"Flag nicht geändert: {message}", ToastError)

    | StartUndoFlag (entryId, previous) ->
        // Im Durchgehen holt „Rückgängig“ nach „Lena fragen“ die Karte zurück (ledger-v6s7c). Gesperrt
        // bleibt auch die Karte, wo sie ist.
        whenRowIdle FlagLock entryId model (fun () ->
            let returned, showCmd = returnToReviewCard entryId model
            { returned with FlagRows = returned.FlagRows |> Map.add entryId FlagSending },
            Cmd.batch [ showCmd; Cmd.ofMsg (RunUndoFlag (entryId, previous)) ],
            NoOp)

    | RunUndoFlag (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.restoreFlag entryId)
                previous
                (fun result -> FlagRestored (entryId, previous, result))
                (fun ex -> FlagRestored (entryId, previous, Error ex.Message))
        model, cmd, NoOp

    | FlagRestored (entryId, previous, Ok outcome) ->
        match takeAwait ((=) (AwaitUndo (UndoLenaAsked (entryId, previous)))) model with
        // „Rückgängig“ aus der Zeile im Durchgehen (ledger-pz5wv): die Karte steht wieder da, kein Toast; nur eine
        // YNAB-Warnung steht gelb in der Zeile.
        | Some (_, settled) ->
            let model' = applyFlagResult entryId previous outcome settled
            let model' =
                match outcome.WriteBack with
                | FlagSendFailed reason -> setReviewLast (Some (reviewLastFor model' entryId LastUndone (LastNotInYnab reason) None)) model'
                | _ -> model'
            model', afterEntryChange entryId model', NoOp
        | None ->
            // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme); im offenen Detail
            // entfällt er, eine YNAB-Warnung steht gelb unter dem Knopf (ledger-enjex).
            let model' = applyFlagResult entryId previous outcome model
            let failure = match outcome.WriteBack with FlagSendFailed reason -> Some reason | _ -> None
            restoredAtField
                (detailNoteRoute model entryId)
                entryId
                AtLena
                flagHintTitle
                failure
                (outcome.Change = FlagChanged)
                flagRestoredToastText
                (clearDetailNote entryId model', afterEntryChange entryId model')

    | FlagRestored (entryId, previous, Error message) ->
        let undo = UndoLenaAsked (entryId, previous)
        match takeAwait ((=) (AwaitUndo undo)) model with
        | Some (_, settled) ->
            let failed = { settled with FlagRows = settled.FlagRows |> Map.add entryId (FlagCommandFailed message) }
            setReviewLast (Some (reviewLastFor failed entryId LastUndone (LastFailed message) (Some (LastRetry (RetryUndo undo))))) failed,
            Cmd.none,
            NoOp
        | None ->
            fieldCommandFailed
                entryId
                (undoFailedDetailNote entryId AtLena message)
                (fun m -> { m with FlagRows = m.FlagRows |> Map.add entryId (FlagCommandFailed message) })
                (fun m -> { m with FlagRows = m.FlagRows |> Map.remove entryId })
                (ShowToast ($"Rückgängig nicht möglich: {message}", ToastError))
                model

    | StartRetryFlag entryId ->
        whenRowIdle FlagLock entryId model (fun () ->
            { model with FlagRows = model.FlagRows |> Map.add entryId FlagSending },
            Cmd.ofMsg (RunRetryFlag entryId),
            NoOp)

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
        // Im offenen Detail zeigt die Hinweisleiste „Flag nicht in YNAB“ den neuen Grund (ledger-enjex).
        let external =
            match writeBack with
            | FlagSendFailed _ when detailShows model entryId -> NoOp
            | FlagSendFailed reason -> ShowToast ($"{flagHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', afterEntryChange entryId model', external

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
                MemoEdit = Some { edit with MemoDraft = memoDraftOfInput text }
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
        whenRowIdle MemoLock entryId model (fun () ->
            match model.MemoEdit, findEntry model entryId with
            | Some edit, Some entry when edit.MemoEntryId = entryId
                                         && (memoActionFor entry).DisabledReason.IsNone
                                         && (memoDraftProblem edit.MemoDraft).IsNone ->
                if memoDraftChangesEntry edit.MemoDraft entry then
                    // Die neue Aktion nimmt eine stehende Bestätigung im Detail weg (ledger-enjex).
                    let model = clearDetailNote entryId model
                    { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
                    Cmd.ofMsg (RunSetMemo (entryId, edit.MemoDraft)),
                    NoOp
                else
                    // Gleiches Memo: nichts zu speichern, das Feld schließt ohne Aufruf.
                    { model with MemoEdit = None }, Cmd.none, NoOp
            | _ -> model, Cmd.none, NoOp)

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
        match outcome.MemoChange with
        | MemoUnchanged -> model', Cmd.none, NoOp
        | MemoChanged ->
            let cmd = afterEntryChange entryId model'
            let failure = match outcome.MemoWriteBack with MemoSendFailed reason -> Some reason | _ -> None
            // Die Karte des Durchgehens ohne „Details“ meldet wie bisher (ledger-pz5wv).
            let onCard =
                let text =
                    if isReviewCard model' entryId then reviewMemoToastText outcome.CurrentMemo
                    else memoToastText outcome.PreviousMemo outcome.CurrentMemo
                let action = { Label = undoToastLabel; Kind = UndoMemo (entryId, outcome.PreviousMemo) }
                match failure with
                | Some reason -> ShowToastWithAction ($"{text}. {memoHintTitle}: {reason}", ToastWarning, action)
                | None -> ShowToastWithAction (text, ToastSuccess, action)
            let payee = findEntry model entryId |> Option.bind (fun e -> e.Payee)
            reportDetailNote
                (detailNoteRoute model entryId)
                (memoChangedNote entryId outcome.PreviousMemo outcome.CurrentMemo payee failure)
                onCard
                (model', cmd)

    // Das Eingabefeld bleibt mit dem Entwurf offen, der Grund steht im Editor (`Problem`), kein Toast (ledger-enjex).
    // Ist der Editor zur Antwort schon zu, steht er rot unter „Memo“, bei geschlossenem Detail in der Leiste.
    | MemoSet (entryId, Error message) ->
        let failRow (m: Model) = { m with MemoRows = m.MemoRows |> Map.add entryId (MemoCommandFailed message) }
        let editorOpen = model.MemoEdit |> Option.exists (fun e -> e.MemoEntryId = entryId)
        if editorOpen && detailShows model entryId then
            failRow model, Cmd.none, NoOp
        else
            fieldCommandFailed
                entryId
                (failedDetailNote entryId AtMemo "Nicht geändert" "Memo nicht geändert" message)
                failRow
                (fun m -> { m with MemoRows = m.MemoRows |> Map.remove entryId })
                (ShowToast ($"Memo nicht geändert: {message}", ToastError))
                model

    | StartUndoMemo (entryId, previous) ->
        whenRowIdle MemoLock entryId model (fun () ->
            { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
            Cmd.ofMsg (RunUndoMemo (entryId, previous)),
            NoOp)

    | RunUndoMemo (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryMemo entryId)
                (previous |> Option.defaultValue "")
                (fun result -> MemoRestored (entryId, result))
                (fun ex -> MemoRestored (entryId, Error ex.Message))
        model, cmd, NoOp

    | MemoRestored (entryId, Ok outcome) ->
        // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme); im offenen Detail entfällt
        // er (ledger-enjex).
        let model' = applyMemoResult entryId outcome model
        let failure = match outcome.MemoWriteBack with MemoSendFailed reason -> Some reason | _ -> None
        let changed = outcome.MemoChange = MemoChanged
        restoredAtField
            (detailNoteRoute model entryId)
            entryId
            AtMemo
            memoHintTitle
            failure
            changed
            memoRestoredToastText
            (clearDetailNote entryId model', (if changed then afterEntryChange entryId model' else Cmd.none))

    | MemoRestored (entryId, Error message) ->
        fieldCommandFailed
            entryId
            (undoFailedDetailNote entryId AtMemo message)
            (fun m -> { m with MemoRows = m.MemoRows |> Map.add entryId (MemoCommandFailed message) })
            (fun m -> { m with MemoRows = m.MemoRows |> Map.remove entryId })
            (ShowToast ($"Rückgängig nicht möglich: {message}", ToastError))
            model

    | StartRetryMemo entryId ->
        whenRowIdle MemoLock entryId model (fun () ->
            { model with MemoRows = model.MemoRows |> Map.add entryId MemoSending },
            Cmd.ofMsg (RunRetryMemo entryId),
            NoOp)

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
        // Im offenen Detail zeigt die Hinweisleiste „Memo nicht in YNAB“ den neuen Grund (ledger-enjex).
        let external =
            match writeBack with
            | MemoSendFailed _ when detailShows model entryId -> NoOp
            | MemoSendFailed reason -> ShowToast ($"{memoHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', afterEntryChange entryId model', external

    | MemoRetried (entryId, Error message) ->
        { model with MemoRows = model.MemoRows |> Map.add entryId (MemoCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Betrag bearbeiten im Detail (ledger-m7mye, ADR 0092)
    // ============================================

    | OpenAmountEdit entryId ->
        match findEntry model entryId with
        | Some entry when model.DetailFor |> Option.exists (fun d -> d.Id = entryId) && amountEditOpenable model entry ->
            { model with
                AmountEdit = Some (amountEditFor entry)
                AmountRows = model.AmountRows |> Map.remove entryId
                MemoEdit = None
                CategoryPickerFor = None
                CategorySuggestions = [] },
            Cmd.none,
            NoOp
        | _ -> model, Cmd.none, NoOp

    // Jede Eingabe nimmt einen abgelehnten Speicherversuch zurück: die rote Zeile über dem Fuß verschwindet bis zum
    // nächsten Speichern (ledger-enjex, wie `MemoEditChanged`).
    | AmountDirectionChanged isOutflow ->
        { clearAmountSaveFailure model with
            AmountEdit = model.AmountEdit |> Option.map (fun e -> { e with AmountIsOutflow = isOutflow }) },
        Cmd.none,
        NoOp

    | AmountDraftChanged text ->
        { clearAmountSaveFailure model with
            AmountEdit = model.AmountEdit |> Option.map (fun e -> { e with AmountDraft = text; AmountFieldError = None }) },
        Cmd.none,
        NoOp

    | CancelAmountEdit ->
        match model.AmountEdit with
        | Some edit when (model.AmountRows |> Map.tryFind edit.AmountEntryId) <> Some AmountSending ->
            { model with AmountEdit = None; AmountRows = model.AmountRows |> Map.remove edit.AmountEntryId }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | StartSaveAmount entryId ->
        whenRowIdle AmountLock entryId model (fun () ->
            match model.AmountEdit, findEntry model entryId with
            | Some edit, Some entry when edit.AmountEntryId = entryId
                                         && (amountLockFor entry).IsNone ->
                match amountSaveStep edit entry with
                | AmountSaveRejected ->
                    // Die Meldung steht am Feld, kein Toast (Styleguide §5, ledger-q42pg).
                    { model with AmountEdit = Some { edit with AmountFieldError = Some amountMissingText } },
                    Cmd.ofMsg RevealAmountFieldError,
                    NoOp
                | AmountSaveUnchanged -> { model with AmountEdit = None }, Cmd.none, NoOp
                | AmountSaveOpensSplitEditor newAmount ->
                    // Erst das Speichern einer gültigen Aufteilung schickt etwas (ADR 0092 Punkt 3).
                    let loadTargets =
                        match model.TransferTargets with
                        | Success _ | Loading -> Cmd.none
                        | _ -> Cmd.ofMsg RunLoadTransferTargets
                    { model with
                        AmountEdit = None
                        SplitEditor = Some (SplitEditor.startStateWithAmount entry newAmount)
                        CategoryPickerFor = None
                        CategorySuggestions = []
                        CategoryCard = None },
                    Cmd.batch [ Cmd.ofMsg EnsureCategoriesFresh; loadTargets; Cmd.ofMsg LoadQuickAddAccount ],
                    NoOp
                | AmountSaveSends request ->
                    // Die neue Aktion nimmt eine stehende Bestätigung im Detail weg (ledger-enjex).
                    let model = clearDetailNote entryId model
                    { model with AmountRows = model.AmountRows |> Map.add entryId AmountSending },
                    Cmd.ofMsg (RunSetAmount (entryId, request)),
                    NoOp
            | _ -> model, Cmd.none, NoOp)

    | RevealAmountFieldError ->
        model,
        Cmd.ofEffect (fun _ -> Client.DesignSystem.Viewport.focusAndReveal amountFieldSelector amountFieldErrorId),
        NoOp

    | RunSetAmount (entryId, request) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryAmount entryId)
                request
                (fun result -> AmountSet (entryId, result))
                (fun ex -> AmountSet (entryId, Error ex.Message))
        model, cmd, NoOp

    | AmountSet (entryId, Ok outcome) ->
        let model' = applyAmountResult entryId outcome model
        match outcome.AmountChange with
        | AmountUnchanged -> model', Cmd.none, NoOp
        | AmountChanged ->
            let cmd = afterAmountChange entryId model'
            let failure = match outcome.AmountWriteBack with AmountSendFailed reason -> Some reason | _ -> None
            let undo = UndoAmount (entryId, outcome.PreviousAmount.Amount)
            // Die Karte des Durchgehens ohne „Details“ meldet wie bisher (ledger-pz5wv).
            let onCard =
                let text = amountToastText outcome.CurrentAmount
                let action = { Label = undoToastLabel; Kind = undo }
                match failure with
                | Some reason -> ShowToastWithAction ($"{text}. {amountHintTitle}: {reason}", ToastWarning, action)
                | None -> ShowToastWithAction (text, ToastSuccess, action)
            let amountText =
                Client.DesignSystem.Money.formatAmountText false true outcome.CurrentAmount.Amount outcome.CurrentAmount.Currency
            reportDetailNote (detailNoteRoute model entryId) (amountChangedNote entryId amountText failure (Some undo)) onCard (model', cmd)

    // Das Sheet bleibt mit dem Entwurf offen, der Grund steht rot im Sheet über dem Fuß (R3 Platz 2), kein Toast
    // (ledger-enjex). Ist das Sheet zur Antwort schon zu, steht er rot am Betrag, bei geschlossenem Detail in der Leiste.
    | AmountSet (entryId, Error message) ->
        let failRow (m: Model) = { m with AmountRows = m.AmountRows |> Map.add entryId (AmountCommandFailed message) }
        if model.AmountEdit |> Option.exists (fun e -> e.AmountEntryId = entryId) then
            failRow model, Cmd.none, NoOp
        else
            fieldCommandFailed
                entryId
                (failedDetailNote entryId AtAmount "Nicht geändert" "Betrag nicht geändert" message)
                failRow
                (fun m -> { m with AmountRows = m.AmountRows |> Map.remove entryId })
                (ShowToast ($"Betrag nicht geändert: {message}", ToastError))
                model

    | StartUndoAmount (entryId, previous) ->
        whenRowIdle AmountLock entryId model (fun () ->
            { model with AmountRows = model.AmountRows |> Map.add entryId AmountSending },
            Cmd.ofMsg (RunUndoAmount (entryId, previous)),
            NoOp)

    | RunUndoAmount (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryAmount entryId)
                (amountRequestOf previous)
                (fun result -> AmountRestored (entryId, result))
                (fun ex -> AmountRestored (entryId, Error ex.Message))
        model, cmd, NoOp

    | AmountRestored (entryId, Ok outcome) ->
        // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme); im offenen Detail entfällt
        // er (ledger-enjex).
        let model' = applyAmountResult entryId outcome model
        let failure = match outcome.AmountWriteBack with AmountSendFailed reason -> Some reason | _ -> None
        let changed = outcome.AmountChange = AmountChanged
        restoredAtField
            (detailNoteRoute model entryId)
            entryId
            AtAmount
            amountHintTitle
            failure
            changed
            amountRestoredToastText
            (clearDetailNote entryId model', (if changed then afterAmountChange entryId model' else Cmd.none))

    | AmountRestored (entryId, Error message) ->
        fieldCommandFailed
            entryId
            (undoFailedDetailNote entryId AtAmount message)
            (fun m -> { m with AmountRows = m.AmountRows |> Map.add entryId (AmountCommandFailed message) })
            (fun m -> { m with AmountRows = m.AmountRows |> Map.remove entryId })
            (ShowToast ($"Rückgängig nicht möglich: {message}", ToastError))
            model

    | StartRetryAmount entryId ->
        whenRowIdle AmountLock entryId model (fun () ->
            { model with AmountRows = model.AmountRows |> Map.add entryId AmountSending },
            Cmd.ofMsg (RunRetryAmount entryId),
            NoOp)

    | RunRetryAmount entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryAmountWriteBack
                entryId
                (fun result -> AmountRetried (entryId, result))
                (fun ex -> AmountRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | AmountRetried (entryId, Ok writeBack) ->
        let failure =
            match writeBack with
            | AmountSendFailed reason -> Some reason
            | AmountSent | AmountNotSent -> None
        let cleared = { model with AmountRows = model.AmountRows |> Map.remove entryId }
        let model' =
            match findEntry cleared entryId with
            | Some entry -> replaceEntry { entry with AmountWriteBackFailure = failure } cleared
            | None -> cleared
        // Im offenen Detail zeigt die Hinweisleiste „Betrag nicht in YNAB“ den neuen Grund (ledger-enjex).
        let external =
            match writeBack with
            | AmountSendFailed _ when detailShows model entryId -> NoOp
            | AmountSendFailed reason -> ShowToast ($"{amountHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        // Eine aufgeteilte Buchung geht als Ersatz: der Abruf zeigt die neue Lage (Sperren, Kategorie-Hinweis).
        model', Cmd.batch [ Cmd.ofMsg (RunRefreshEntry entryId); afterEntryChange entryId model' ], external

    | AmountRetried (entryId, Error message) ->
        { model with AmountRows = model.AmountRows |> Map.add entryId (AmountCommandFailed message) },
        Cmd.none,
        NoOp

    // ============================================
    // Payee ändern im Detail (ledger-fxt09)
    // ============================================

    | OpenPayeeEdit entryId ->
        match findEntry model entryId with
        | Some entry when model.DetailFor |> Option.exists (fun d -> d.Id = entryId) && payeeEditOpenable model entry ->
            let load =
                match model.PayeeChoices with
                | Success _ | Loading -> Cmd.none
                | _ -> Cmd.ofMsg RunLoadPayeeChoices
            { model with
                PayeeEdit = Some { PayeeEntryId = entryId; PayeeFieldError = None }
                PayeeRows = model.PayeeRows |> Map.remove entryId
                PayeeChoices = (match model.PayeeChoices with Success _ -> model.PayeeChoices | _ -> Loading)
                MemoEdit = None
                AmountEdit = None
                CategoryPickerFor = None
                CategorySuggestions = [] },
            load,
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunLoadPayeeChoices ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.ledger.getLedgerPayeeSuggestions)
                payeeChoicesQuery
                (Ok >> payeeChoicesAnswer model)
                (fun ex -> payeeChoicesAnswer model (Error ex.Message))
        model, cmd, NoOp

    | PayeeChoicesLoaded (generation, _) when generation <> model.PayeeChoicesGeneration -> model, Cmd.none, NoOp

    | PayeeChoicesLoaded (_, Ok choices) -> { model with PayeeChoices = Success choices }, Cmd.none, NoOp

    | PayeeChoicesLoaded (_, Error message) ->
        { model with PayeeChoices = RemoteData.lateFailure message model.PayeeChoices }, Cmd.none, NoOp

    | CancelPayeeEdit ->
        match model.PayeeEdit with
        | Some edit when (model.PayeeRows |> Map.tryFind edit.PayeeEntryId) <> Some PayeeSending ->
            { model with PayeeEdit = None; PayeeRows = model.PayeeRows |> Map.remove edit.PayeeEntryId },
            Cmd.ofMsg RestorePayeeRowFocus,
            NoOp
        | _ -> model, Cmd.none, NoOp

    | BlurFocusedField ->
        model,
        Cmd.ofEffect (fun _ ->
            match Browser.Dom.document.activeElement with
            | null -> ()
            | element -> (element :?> Browser.Types.HTMLElement).blur ()),
        NoOp

    | RestorePayeeRowFocus ->
        model,
        Cmd.ofEffect (fun _ ->
            match Browser.Dom.document.querySelector payeeRowSelector with
            | null -> ()
            | element -> (element :?> Browser.Types.HTMLElement).focus ()),
        NoOp

    | PayeeSearchEdited ->
        match model.PayeeEdit with
        | Some edit when edit.PayeeFieldError.IsSome ->
            { model with PayeeEdit = Some { edit with PayeeFieldError = None } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | StartSavePayee (entryId, target) ->
        match model.PayeeEdit, findEntry model entryId with
        | Some edit, Some entry when edit.PayeeEntryId = entryId
                                     && (model.PayeeRows |> Map.tryFind entryId) <> Some PayeeSending
                                     && (payeeLockFor entry).IsNone ->
            match payeeSaveStep target entry with
            | PayeeSaveRejected reason ->
                // Die Meldung steht unter dem Suchfeld, kein Toast (Styleguide §5).
                { model with PayeeEdit = Some { edit with PayeeFieldError = Some reason } }, Cmd.none, NoOp
            | PayeeSaveUnchanged -> { model with PayeeEdit = None }, Cmd.ofMsg RestorePayeeRowFocus, NoOp
            | PayeeSaveSends payee ->
                // Der Payee auf der Karte (nicht in „Details“) meldet sich in der Zeile unter „Passt, weiter“
                // (ledger-pz5wv); er ist eine neue Aktion, die vorige Zeile geht. Im Detail nimmt die neue Aktion eine
                // stehende Bestätigung weg (ledger-enjex).
                let onCard =
                    isReviewCard model entryId
                    && model.Review |> Option.exists (fun r -> reviewEditorPlace r = ReviewEditorPlace.OverCard)
                let marked =
                    if onCard then model |> awaitReview (AwaitPayee (entryId, payee)) |> setReviewLast None
                    else clearDetailNote entryId model
                { marked with
                    PayeeRows = model.PayeeRows |> Map.add entryId PayeeSending
                    PayeeEdit = Some { edit with PayeeFieldError = None } },
                Cmd.ofMsg (RunSetPayee (entryId, payee)),
                NoOp
        | _ -> model, Cmd.none, NoOp

    | RunSetPayee (entryId, payee) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryPayee entryId)
                payee
                (fun result -> PayeeSet (entryId, result))
                (fun ex -> PayeeSet (entryId, Error ex.Message))
        model, cmd, NoOp

    | PayeeSet (entryId, Ok outcome) ->
        let awaited = takeAwait (function AwaitPayee (id, _) -> id = entryId | _ -> false) model
        let model' = applyPayeeResult entryId outcome (awaited |> Option.map snd |> Option.defaultValue model)
        let cmd =
            Cmd.batch [
                Cmd.ofMsg RestorePayeeRowFocus
                if outcome.PayeeChange = PayeeChanged then afterEntryChange entryId model'
            ]
        match outcome.PayeeChange with
        | PayeeUnchanged -> model', cmd, NoOp
        // Von der Karte aus steht die Bestätigung in der Zeile unter „Passt, weiter“ (ledger-pz5wv).
        | PayeeChanged when awaited.IsSome ->
            let result =
                match outcome.PayeeWriteBack with
                | PayeeSendFailed reason -> LastNotInYnab reason
                | _ -> LastDone
            let line =
                { reviewLastFor model' entryId LastPayeeChanged result (Some (LastUndo (UndoPayeeChanged (entryId, outcome.PreviousPayee))))
                    with Payee = outcome.CurrentPayee }
            setReviewLast (Some line) model', cmd, NoOp
        | PayeeChanged ->
            let failure = match outcome.PayeeWriteBack with PayeeSendFailed reason -> Some reason | _ -> None
            // Die Karte des Durchgehens ohne „Details“ meldet wie bisher (ledger-pz5wv).
            let onCard =
                let text =
                    match outcome.CurrentPayee with
                    | Some current -> payeeToastText current
                    | None -> payeeRemovedToastText
                // Auch ohne alten Payee gibt es „Rückgängig“: es nimmt den Payee wieder weg (ledger-pkrwd, §7.13).
                let action = { Label = undoToastLabel; Kind = UndoPayee (entryId, outcome.PreviousPayee) }
                match failure with
                | Some reason -> ShowToastWithAction ($"{text}. {payeeHintTitle}: {reason}", ToastWarning, action)
                | None -> ShowToastWithAction (text, ToastSuccess, action)
            reportDetailNote
                (detailNoteRoute model entryId)
                (payeeChangedNote entryId outcome.PreviousPayee outcome.CurrentPayee failure)
                onCard
                (model', cmd)

    // Die Auswahl bleibt offen, der Grund steht als Feldfehler unter dem Suchfeld. Ist sie inzwischen zu, steht er rot
    // unter „Payee“ im Detail, bei geschlossenem Detail in der Leiste (ledger-enjex).
    | PayeeSet (entryId, Error message) ->
        let awaited = takeAwait (function AwaitPayee (id, _) -> id = entryId | _ -> false) model
        let clearRow (m: Model) = { m with PayeeRows = m.PayeeRows |> Map.remove entryId }
        let cleared = clearRow (awaited |> Option.map snd |> Option.defaultValue model)
        match model.PayeeEdit, awaited with
        | Some edit, _ when edit.PayeeEntryId = entryId ->
            { cleared with PayeeEdit = Some { edit with PayeeFieldError = Some message } }, Cmd.none, NoOp
        // Die Auswahl über der Karte ist zu: rot in der Zeile mit „Erneut“ statt Toast (ledger-pz5wv).
        | _, Some (AwaitPayee (_, payee), _) ->
            let line = reviewLastFor cleared entryId LastPayeeChanged (LastFailed message) (Some (LastRetry (RetryPayee (entryId, payee))))
            setReviewLast (Some line) cleared, Cmd.none, NoOp
        | _ ->
            fieldCommandFailed
                entryId
                (failedDetailNote entryId AtPayee "Nicht geändert" "Payee nicht geändert" message)
                clearRow
                clearRow
                (ShowToast ($"Payee nicht geändert: {message}", ToastError))
                model

    | StartUndoPayee (entryId, previous) ->
        // Ein zweiter Tipp auf "Rückgängig" schickt nichts, solange ein Aufruf zum Payee läuft.
        if (model.PayeeRows |> Map.tryFind entryId) = Some PayeeSending then
            model, Cmd.none, NoOp
        else
            { model with PayeeRows = model.PayeeRows |> Map.add entryId PayeeSending },
            Cmd.ofMsg (RunUndoPayee (entryId, previous)),
            NoOp

    | RunUndoPayee (entryId, previous) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntryPayee entryId)
                previous
                (fun result -> PayeeRestored (entryId, result))
                (fun ex -> PayeeRestored (entryId, Error ex.Message))
        model, cmd, NoOp

    | PayeeRestored (entryId, Ok outcome) ->
        let awaited = takeAwait (function AwaitUndo (UndoPayeeChanged (id, _)) -> id = entryId | _ -> false) model
        match awaited with
        // „Rückgängig“ aus der Zeile im Durchgehen (ledger-pz5wv): kein Toast, eine YNAB-Warnung steht gelb in der Zeile.
        | Some (_, settled) ->
            let model' = applyPayeeResult entryId outcome settled
            let model' =
                match outcome.PayeeWriteBack with
                | PayeeSendFailed reason -> setReviewLast (Some (reviewLastFor model' entryId LastUndone (LastNotInYnab reason) None)) model'
                | _ -> model'
            model', (if outcome.PayeeChange = PayeeChanged then afterEntryChange entryId model' else Cmd.none), NoOp
        | None ->
            // Der Rücknahme-Toast hat kein "Rückgängig" (eine Aktion, ein Toast, eine Rücknahme); im offenen Detail
            // entfällt er (ledger-enjex).
            let model' = applyPayeeResult entryId outcome model
            let failure = match outcome.PayeeWriteBack with PayeeSendFailed reason -> Some reason | _ -> None
            let changed = outcome.PayeeChange = PayeeChanged
            restoredAtField
                (detailNoteRoute model entryId)
                entryId
                AtPayee
                payeeHintTitle
                failure
                changed
                payeeRestoredToastText
                (clearDetailNote entryId model', (if changed then afterEntryChange entryId model' else Cmd.none))

    | PayeeRestored (entryId, Error message) ->
        let clearRow (m: Model) = { m with PayeeRows = m.PayeeRows |> Map.remove entryId }
        match takeAwait (function AwaitUndo (UndoPayeeChanged (id, _)) -> id = entryId | _ -> false) (clearRow model) with
        | Some (AwaitUndo undo, settled) ->
            setReviewLast (Some (reviewLastFor settled entryId LastUndone (LastFailed message) (Some (LastRetry (RetryUndo undo))))) settled,
            Cmd.none,
            NoOp
        | _ ->
            fieldCommandFailed
                entryId
                (undoFailedDetailNote entryId AtPayee message)
                clearRow
                clearRow
                (ShowToast ($"Rückgängig nicht möglich: {message}", ToastError))
                model

    | StartRetryPayee entryId ->
        // Ein Tipp auf "Erneut senden" schickt nichts, solange ein Aufruf zum Payee läuft.
        if (model.PayeeRows |> Map.tryFind entryId) = Some PayeeSending then
            model, Cmd.none, NoOp
        else
            { model with PayeeRows = model.PayeeRows |> Map.add entryId PayeeSending },
            Cmd.ofMsg (RunRetryPayee entryId),
            NoOp

    | RunRetryPayee entryId ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.retryPayeeWriteBack
                entryId
                (fun result -> PayeeRetried (entryId, result))
                (fun ex -> PayeeRetried (entryId, Error ex.Message))
        model, cmd, NoOp

    | PayeeRetried (entryId, Ok writeBack) ->
        let failure =
            match writeBack with
            | PayeeSendFailed reason -> Some reason
            | PayeeSent | PayeeNotSent -> None
        let cleared = { model with PayeeRows = model.PayeeRows |> Map.remove entryId }
        let model' =
            match findEntry cleared entryId with
            | Some entry -> replaceEntry { entry with PayeeWriteBackFailure = failure } cleared
            | None -> cleared
        // Im offenen Detail zeigt die Hinweisleiste „Payee nicht in YNAB“ den neuen Grund (ledger-enjex).
        let external =
            match writeBack with
            | PayeeSendFailed _ when detailShows model entryId -> NoOp
            | PayeeSendFailed reason -> ShowToast ($"{payeeHintTitle}: {reason}", ToastWarning)
            | _ -> NoOp
        model', afterEntryChange entryId model', external

    | PayeeRetried (entryId, Error message) ->
        { model with PayeeRows = model.PayeeRows |> Map.add entryId (PayeeCommandFailed message) },
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
                          && not (rowBusy CategoryLock model entryId) ->
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
        | Some editor when not (rowBusy CategoryLock model editor.EntryId) -> { model with SplitEditor = None }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | OpenCapture -> openCapture (captureFilterAccount model) model
    | OpenCaptureUnfiltered -> openCapture None model

    | CloseCapture ->
        match model.Capture with
        // Während "Buchung anlegen" läuft, bleibt das Formular offen: sonst ginge ein Fehler verloren.
        | Some capture when capture.Saving -> model, Cmd.none, NoOp
        | _ -> { model with Capture = None }, Cmd.none, NoOp

    | RunLoadCaptureDefaults ->
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerBookingDefaults)
            ()
            (fun defaults -> CaptureDefaultsLoaded (Ok defaults))
            (fun ex -> CaptureDefaultsLoaded (Error ex.Message)),
        NoOp

    | RetryCaptureDefaults ->
        // Nur nach einem Fehler, und sichtbar als Laden: ein zweiter Tipp schickt keine zweite Anfrage.
        match model.Capture with
        | Some capture when RemoteData.isFailure capture.Defaults ->
            { model with Capture = Some { capture with Defaults = Loading } }, Cmd.ofMsg RunLoadCaptureDefaults, NoOp
        | _ -> model, Cmd.none, NoOp

    | CaptureDefaultsLoaded result ->
        match model.Capture, result with
        // Steht die Vorbelegung schon, ist die Antwort zu spät (Öffnen, Schließen, Öffnen): sonst
        // baute sie das Formular neu und leerte die Eingaben.
        | Some capture, _ when RemoteData.isSuccess capture.Defaults -> model, Cmd.none, NoOp
        | Some capture, Ok defaults ->
            let form = Capture.formFromDefaults defaults capture.FilterAccount (captureAccountIds model defaults)
            { model with Capture = Some { capture with Defaults = Success defaults; Form = form } }, Cmd.none, NoOp
        | Some capture, Error message ->
            { model with Capture = Some { capture with Defaults = Failure message } }, Cmd.none, NoOp
        | None, _ -> model, Cmd.none, NoOp

    | SetCaptureDirection isOutflow -> updateCaptureForm (fun f -> { f with IsOutflow = isOutflow }) model, Cmd.none, NoOp
    | SetCaptureAmount text ->
        match model.Capture with
        | Some capture ->
            { model with Capture = Some { capture with Form = { capture.Form with AmountText = text }; AmountError = None } },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp
    | SetCapturePayee text -> updateCaptureForm (fun f -> { f with Payee = text }) model, Cmd.none, NoOp
    | SetCaptureDate date -> updateCaptureForm (fun f -> { f with Date = date.Date }) model, Cmd.none, NoOp
    | SetCaptureMemo text -> updateCaptureForm (fun f -> { f with Memo = text }) model, Cmd.none, NoOp

    | CommitCapturePayee ->
        match model.Capture with
        | Some capture when not capture.Form.TargetChosen && not (String.IsNullOrWhiteSpace capture.Form.Payee) ->
            let payee = capture.Form.Payee.Trim()
            { model with Capture = Some { capture with PayeeLookup = Some payee } },
            Cmd.ofMsg (RunLoadCapturePayeeCategory payee),
            NoOp
        | _ -> model, Cmd.none, NoOp

    | RunLoadCapturePayeeCategory payee ->
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getBookingCategoryForPayee)
            payee
            (fun category -> CapturePayeeCategoryLoaded (payee, Ok category))
            (fun ex -> CapturePayeeCategoryLoaded (payee, Error ex.Message)),
        NoOp

    | CapturePayeeCategoryLoaded (payee, result) ->
        // Ein Fehler beim Nachschlagen gilt als „keine Kategorie gefunden“: das Anlegen geht ohne Kategorie weiter.
        let category = match result with Ok found -> found | Error _ -> None
        match model.Capture with
        | Some capture ->
            let lookup = if capture.PayeeLookup = Some payee then None else capture.PayeeLookup
            let capture' =
                { capture with Form = Capture.applyPayeeCategory payee category capture.Form; PayeeLookup = lookup }
            // Wartete "Buchung anlegen" auf diese Antwort, startet der Aufruf jetzt, mit der Kategorie.
            if capture'.SubmitWhenLookedUp && not (Capture.awaitsPayeeCategory capture') then
                submitCapture { capture' with Saving = false; SubmitWhenLookedUp = false } model
            else
                { model with Capture = Some capture' }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | ApplyCaptureTemplate template ->
        // Der Chip füllt Betrag und Konto: ihre Fehler gelten nicht mehr.
        match model.Capture with
        | Some capture ->
            { model with
                Capture =
                    Some { capture with Form = Capture.applyTemplate template capture.Form; AmountError = None; AccountError = None } },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp

    | OpenCapturePicker picker ->
        match model.Capture with
        | Some capture -> { model with Capture = Some { capture with Picker = Some picker } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | CloseCapturePicker ->
        match model.Capture with
        | Some capture -> { model with Capture = Some { capture with Picker = None } }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp

    | ChooseCaptureAccount accountId ->
        match model.Capture with
        | Some capture ->
            { model with
                Capture =
                    Some
                        { capture with
                            Picker = None
                            Form = { capture.Form with AccountId = Some accountId }
                            AccountError = None } },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp

    | ChooseCaptureTarget target ->
        match model.Capture with
        | Some capture ->
            { model with
                Capture =
                    Some { capture with Picker = None; Form = { capture.Form with Target = target; TargetChosen = true } } },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp

    | SubmitCapture ->
        match model.Capture with
        | Some capture when not capture.Saving ->
            match Capture.captureRequest capture.Form with
            // Lädt die Kategorie zum Payee noch (Tipp direkt nach dem Payee-Feld), wartet der Aufruf
            // auf die Antwort; der Knopf ist so lange belegt.
            | Ok _ when Capture.awaitsPayeeCategory capture ->
                { model with Capture = Some { withoutCaptureErrors capture with Saving = true; SubmitWhenLookedUp = true } },
                Cmd.none,
                NoOp
            | _ -> submitCapture capture model
        | _ -> model, Cmd.none, NoOp

    | RevealCaptureAmountError ->
        model,
        Cmd.ofEffect (fun _ ->
            Client.DesignSystem.Viewport.focusAndReveal Capture.amountFieldSelector Capture.amountErrorId),
        NoOp

    | RunSubmitCapture request ->
        model,
        Cmd.OfAsync.either
            Api.ledger.addLedgerBooking
            request
            CaptureSubmitted
            (fun ex -> CaptureSubmitted (Error ex.Message)),
        NoOp

    | CaptureSubmitted (Ok created) ->
        // Am PC zeigt das Panel danach die neue Buchung (Styleguide §7.6).
        let showsEntry = model.Capture |> Option.forall (fun c -> c.ShowsCreatedEntry)
        let amount = model.Capture |> Option.bind (fun c -> Capture.signedAmount c.Form)
        let openDetail =
            if showsEntry && model.DetailForm = DetailPanel then Cmd.ofMsg (OpenRouteEntry created.EntryId) else Cmd.none
        let payee = model.Capture |> Option.map (fun c -> c.Form.Payee)
        let bookedAccount = model.Capture |> Option.bind (fun c -> c.Form.AccountId)
        // Gezeigt ist ein Konto nur im Kassenbuch mit Filter auf genau dieses Konto; auf der Übersicht oder bei
        // „Alle Konten“ steht das Konto mit in der Meldung.
        let inShownAccount =
            match model.SelectedFilter, bookedAccount with
            | Some (OneAccount shown), Some booked -> showsEntry && shown = booked
            | _ -> false
        { model with Capture = None },
        Cmd.batch [ Cmd.ofMsg RefreshEntries; Cmd.ofMsg LoadBalances; openDetail ],
        PostFeedback (createdFeedback created payee amount inShownAccount)

    | CaptureSubmitted (Error message) ->
        match model.Capture with
        | Some capture -> { model with Capture = Some { capture with Saving = false; ServerErrors = [ message ] } }, Cmd.none, NoOp
        | None -> model, Cmd.none, PostFeedback (failedFeedback "Nicht angelegt" message)

    | StartDiscardCaptured entryId ->
        // Ein zweiter Tipp auf "Rückgängig" schickt nichts, solange die Rücknahme läuft.
        if model.DiscardingCaptured.Contains entryId then
            model, Cmd.none, NoOp
        else
            { model with DiscardingCaptured = model.DiscardingCaptured.Add entryId },
            Cmd.ofMsg (RunDiscardCaptured entryId),
            NoOp

    | RunDiscardCaptured entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.discardCapturedBooking
            entryId
            (fun result -> CapturedDiscarded (entryId, result))
            (fun ex -> CapturedDiscarded (entryId, Error ex.Message)),
        NoOp

    | CapturedDiscarded (entryId, Ok ()) ->
        let model = { model with DiscardingCaptured = model.DiscardingCaptured.Remove entryId }
        let model' =
            if model.DetailFor |> Option.exists (fun d -> d.Id = entryId) then closeDetail model
            else
                model
        // Keine Meldung: die Zeile ist weg, die Leiste ist seit dem Tipp auf „Rückgängig“ leer (ledger-gwr9j).
        model', Cmd.batch [ Cmd.ofMsg RefreshEntries; Cmd.ofMsg LoadBalances ], NoOp

    | CapturedDiscarded (entryId, Error message) ->
        { model with DiscardingCaptured = model.DiscardingCaptured.Remove entryId },
        Cmd.none, PostFeedback (undoFailedFeedback message)

    | StartDeleteEntry entryId ->
        // Ein zweiter Tipp auf "Buchung löschen" schickt nichts, solange das Löschen läuft.
        if model.DeletingEntries.Contains entryId then
            model, Cmd.none, NoOp
        else
            { model with DeletingEntries = model.DeletingEntries.Add entryId; DeleteFailure = None },
            Cmd.ofMsg (RunDeleteEntry entryId),
            NoOp

    | RunDeleteEntry entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.deleteEntry
            entryId
            (fun result -> EntryDeleteFinished (entryId, result))
            (fun ex -> EntryDeleteFinished (entryId, Error ex.Message)),
        NoOp

    | EntryDeleteFinished (entryId, Ok ()) ->
        // Nach Erfolg schließt das Detail (ADR 0092 Punkt 2). Steht die Buchung in der Liste, hinterlässt sie dort die
        // Zeile „… gelöscht · Rückgängig“, ohne Meldung, auf jeder Breite (R3, ledger-k68sd). Sonst meldet die Leiste,
        // am PC der Toast, mit „Rückgängig“.
        // Das Löschen ist die neue Aktion im Detail: es ersetzt die Bestätigung am Feld dieser Buchung, sonst ginge deren
        // „Rückgängig“ beim Schließen für die gelöschte Buchung in die Leiste (ledger-p5y9s, `handOverDetailNote`).
        let model = clearDetailNote entryId { model with DeletingEntries = model.DeletingEntries.Remove entryId }
        let payee =
            model.DetailFor
            |> Option.filter (fun d -> d.Id = entryId)
            |> Option.bind (fun d -> d.Payee)
        let row = deletedRowFor model entryId
        let model' =
            if model.DetailFor |> Option.exists (fun d -> d.Id = entryId) then closeDetail model
            else
                model
        { model' with DeletedRow = row },
        Cmd.batch [ Cmd.ofMsg RefreshEntries; Cmd.ofMsg LoadBalances ],
        (match row with
         | Some _ -> NoOp
         | None -> PostFeedback (entryDeletedFeedback payee entryId))

    | EntryDeleteFinished (entryId, Error message) ->
        // Das Detail bleibt offen, der Grund steht rot unter „Buchung löschen“ (Fehler am Ort, ledger-k68sd).
        { model with
            DeletingEntries = model.DeletingEntries.Remove entryId
            DeleteFailure = Some (entryId, message) },
        Cmd.none, NoOp

    | StartRestoreDeletedEntry entryId ->
        if model.RestoringEntries.Contains entryId then
            model, Cmd.none, NoOp
        else
            { model with
                RestoringEntries = model.RestoringEntries.Add entryId
                DeletedRow =
                    model.DeletedRow
                    |> Option.map (fun row -> if row.Entry.Id = entryId then { row with Phase = DeletedRowRestoring } else row) },
            Cmd.ofMsg (RunRestoreDeletedEntry entryId),
            NoOp

    | RunRestoreDeletedEntry entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.restoreDeletedEntry
            entryId
            (fun result -> EntryRestoreFinished (entryId, result))
            (fun ex -> EntryRestoreFinished (entryId, Error ex.Message)),
        NoOp

    | EntryRestoreFinished (entryId, Ok ()) ->
        // Die Buchung steht sofort wieder an ihrem Platz, ohne Meldung; das Nachladen bringt danach den Stand des
        // Servers (ledger-k68sd).
        let model = { model with RestoringEntries = model.RestoringEntries.Remove entryId }
        let model' =
            match model.DeletedRow with
            | Some row when row.Entry.Id = entryId ->
                { model with
                    DeletedRow = None
                    Entries = model.Entries |> RemoteData.map (withDeletedPlaced (Some row)) }
            | _ -> model
        model', Cmd.batch [ Cmd.ofMsg RefreshEntries; Cmd.ofMsg LoadBalances ], NoOp

    | EntryRestoreFinished (entryId, Error message) ->
        // Rot in der Zeile am Platz, „Rückgängig“ bleibt; ohne Zeile rot am Meldungsplatz (ledger-k68sd).
        let model = { model with RestoringEntries = model.RestoringEntries.Remove entryId }
        match model.DeletedRow with
        | Some row when row.Entry.Id = entryId ->
            { model with DeletedRow = Some { row with Phase = DeletedRowRestoreFailed message } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, PostFeedback (entryRestoreFailedFeedback message entryId)

    | LedgerLeft ->
        // Die Zeile am Platz nimmt `update` vorher über `endsDeletedRow` weg. Die Bestätigung am Feld endet still: sonst
        // schickte der nächste Schritt, der das Detail schließt (Erfassen auf der Übersicht, Durchgehen), sie samt
        // „Rückgängig“ in die Leiste (`handOverDetailNote`).
        { model with DetailNote = None }, Cmd.none, NoOp

    | RunLoadTransferTargets ->
        { model with TransferTargets = Loading },
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getBudgetAccounts)
            ()
            (fun result -> TransferTargetsLoaded result)
            (fun ex -> TransferTargetsLoaded (Error ex.Message)),
        NoOp

    | TransferTargetsLoaded (Ok accounts) ->
        withCaptureDefaultAccount { model with TransferTargets = Success accounts }, Cmd.none, NoOp
    | TransferTargetsLoaded (Error message) ->
        { model with TransferTargets = RemoteData.lateFailure message model.TransferTargets }, Cmd.none, NoOp

    | AddSplitLine -> editSplit SplitEditor.addLine model, Cmd.none, NoOp
    | RemoveSplitLine index -> editSplit (SplitEditor.removeLine index) model, Cmd.none, NoOp
    | SetSplitLineAmount (index, text) -> editSplit (SplitEditor.setAmountText index text) model, Cmd.none, NoOp
    | SetSplitLineMemo (index, text) -> editSplit (SplitEditor.setMemo index text) model, Cmd.none, NoOp
    | FillSplitLineRest index -> editSplit (SplitEditor.fillRest index) model, Cmd.none, NoOp
    | StartSplitCashback target ->
        // Ohne Ziel geht der Picker auf, der auch die Kategorien zeigt.
        let cmd = if target.IsNone then Cmd.ofMsg EnsureCategoriesFresh else Cmd.none
        editSplit (SplitEditor.startCashback target) model, cmd, NoOp

    // Kommt nie an: der Root (`src/Client/State.fs`) fängt `LedgerMsg LoadQuickAddAccount` ab und füllt
    // `QuickAddAccount` aus seinen Settings.
    | LoadQuickAddAccount -> model, Cmd.none, NoOp

    | OpenSplitTargetPicker index ->
        editSplit (fun e -> { e with Picker = SplitEditor.SplitTargetPickerFor index }) model, Cmd.ofMsg EnsureCategoriesFresh, NoOp

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
        | Some editor ->
            whenRowIdle CategoryLock editor.EntryId model (fun () ->
                match SplitEditor.requestLines editor with
                | Some lines ->
                    let run =
                        match editor.AmountFrom with
                        | Some _ -> RunSaveSplitsWithAmount (editor.EntryId, lines, editor.Total)
                        | None -> RunSaveSplits (editor.EntryId, lines)
                    { clearDetailNote editor.EntryId model with
                        CategoryRows = model.CategoryRows |> Map.add editor.EntryId CategorySending
                        SplitEditor = Some { editor with SaveError = None } },
                    Cmd.ofMsg run,
                    NoOp
                | None -> model, Cmd.none, NoOp)
        | None -> model, Cmd.none, NoOp

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
        let cmd = Cmd.batch [ cmd; afterEntryChange entryId model' ]
        let route = detailNoteRoute model entryId
        let unsplit = SplitEditor.isUnsplitResult lines
        let field = if unsplit then AtCategory else AtSplits
        match outcome.Change, outcome.WriteBack, before with
        | CategoryUnchanged, CategorySendFailed reason, _ -> reportSplitNote route (notInYnabDetailNote entryId field reason) (model', cmd)
        | CategoryUnchanged, _, _ -> model', cmd, NoOp
        | CategoryChanged, writeBack, Some entry ->
            let text = SplitEditor.savedToastText lines
            let failure = match writeBack with CategorySendFailed reason -> Some reason | _ -> None
            let undo =
                if SplitEditor.undoAvailable entry.SplitLocks lines then
                    Some (UndoSplit (entryId, SplitEditor.undoLines (SplitEditor.startState entry).Before entry.Amount))
                else
                    None
            reportSplitNote route (splitSavedNote entryId text unsplit entry.Payee failure undo) (model', cmd)
        | CategoryChanged, _, None -> model', cmd, NoOp

    | RunSaveSplitsWithAmount (entryId, lines, amount) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntrySplitsWithAmount entryId)
                { Amount = amountRequestOf amount.Amount; Lines = lines }
                (fun result -> SplitsWithAmountSaved (entryId, lines, amount, result))
                (fun ex -> SplitsWithAmountSaved (entryId, lines, amount, Error ex.Message))
        model, cmd, NoOp

    // Betrag und Aufteilung zusammen (ADR 0092 Punkt 3). Kein "Rückgängig": die alte Aufteilung
    // gehört zum alten Betrag, ein Rückweg wäre ein zweiter Ersatz (Abweichung vom Prototyp).
    | SplitsWithAmountSaved (entryId, lines, amount, Ok outcome) ->
        let model', cmd = applySplitResult entryId lines outcome model
        let changed = outcome.Change = CategoryChanged
        let model'' =
            match findEntry model' entryId with
            | Some entry when changed -> replaceEntry { entry with Amount = amount } model'
            | _ -> model'
        let follow = if changed then afterAmountChange entryId model'' else Cmd.none
        // Ein Fehlschlag holt die Buchung ohnehin neu (`applySplitResult`); sonst die Sperren nach dem Ersatz.
        let cmd = Cmd.batch [ cmd; follow ]
        let amountText = Client.DesignSystem.Money.formatAmountText false true amount.Amount amount.Currency
        let route = detailNoteRoute model entryId
        match outcome.WriteBack with
        | CategorySendFailed reason -> reportSplitNote route (amountChangedNote entryId amountText (Some reason) None) (model'', cmd)
        | _ when changed -> reportSplitNote route (amountChangedNote entryId amountText None None) (model'', cmd)
        | _ -> model'', cmd, NoOp

    // Der Editor bleibt offen: der Entwurf geht nicht verloren.
    | SplitsWithAmountSaved (entryId, _, _, Error message) ->
        splitSaveFailed entryId $"Betrag nicht geändert: {message}" model

    | SplitsSaved (entryId, _, Error message) -> splitSaveFailed entryId $"Aufteilung nicht gespeichert: {message}" model

    | StartUndoSplit (entryId, lines) ->
        whenRowIdle CategoryLock entryId model (fun () ->
            { model with CategoryRows = model.CategoryRows |> Map.add entryId CategorySending },
            Cmd.ofMsg (RunUndoSplit (entryId, lines)),
            NoOp)

    | RunUndoSplit (entryId, lines) ->
        let cmd =
            Cmd.OfAsync.either
                (Api.ledger.setEntrySplits entryId)
                lines
                (fun result -> SplitsRestored (entryId, lines, result))
                (fun ex -> SplitsRestored (entryId, lines, Error ex.Message))
        model, cmd, NoOp

    | SplitsRestored (entryId, lines, Ok outcome) ->
        // Ohne neuen Knopf: eine Aktion, eine Rücknahme. Im Detail steht die alte Aufteilung, die Bestätigung ist weg.
        let model', cmd = applySplitResult entryId lines outcome model
        restoredReport
            (detailNoteRoute model entryId)
            entryId
            (if SplitEditor.isUnsplitResult lines then AtCategory else AtSplits)
            outcome
            "Aufteilung zurückgenommen"
            (clearDetailNote entryId model', Cmd.batch [ cmd; afterEntryChange entryId model' ])

    | SplitsRestored (entryId, lines, Error message) ->
        categoryCommandFailed
            entryId
            message
            (undoFailedDetailNote entryId (detailNoteFieldOf model entryId) message)
            (LastUndone, RetryFieldUndo (UndoSplit (entryId, lines)))
            model

    | RunRefreshEntry entryId ->
        let answer = entryRefreshAnswer model entryId
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerEntry)
            entryId
            (Ok >> answer)
            (fun ex -> answer (Error ex.Message)),
        NoOp

    // Hat sich die Zeile seit dem Start der Abfrage geändert, ist die Antwort veraltet
    // (`Retry.reading` verlängert das Fenster, infrastructure-r3tw7).
    | EntryRefreshed (entryId, basis, Ok (Some view)) when findEntry model entryId = basis ->
        replaceEntry view model, Cmd.none, NoOp
    | EntryRefreshed _ -> model, Cmd.none, NoOp

    // Sprung aus dem Kategorie-Detail (budgeting-m1bce).
    | OpenRouteEntry entryId ->
        { model with PendingRouteEntry = Some entryId },
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerEntry)
            entryId
            (fun result -> RouteEntryLoaded (entryId, Ok result))
            (fun ex -> RouteEntryLoaded (entryId, Error ex.Message)),
        NoOp

    | RouteEntryLoaded (entryId, Ok (Some entry)) when model.PendingRouteEntry = Some entryId ->
        let filter = filterForEntry model.SelectedFilter entry
        let settled =
            { model with
                PendingRouteEntry = None
                Selection = NoSelection
                SelectionAnchor = None
                SelectionMode = false }
        // Nicht über `SelectFilter`: das schließt das Detail und überschreibt die gemerkte Sicht.
        // Der Sprung ist ein Abstecher, die Liste kann die Buchung ohnehin nachladen.
        let model', listCmd =
            if model.SelectedFilter = Some filter then
                settled, Cmd.none
            else
                rebuildEntries { settled with SelectedFilter = Some filter }
        let model'', historyCmd = showDetail entry model'
        model'', Cmd.batch [ listCmd; historyCmd ], NoOp

    | RouteEntryLoaded (entryId, Ok None) when model.PendingRouteEntry = Some entryId ->
        { model with PendingRouteEntry = None }, Cmd.none, PostFeedback (feedbackNote FeedbackWarning "Buchung nicht gefunden" None)

    | RouteEntryLoaded (entryId, Error message) when model.PendingRouteEntry = Some entryId ->
        { model with PendingRouteEntry = None }, Cmd.none, PostFeedback (failedFeedback "Buchung nicht geladen" message)

    // Antwort zu einer inzwischen verlassenen Route.
    | RouteEntryLoaded _ -> model, Cmd.none, NoOp

    | LeaveRouteEntry leftFor ->
        let left = { closeDetail model with PendingRouteEntry = None }
        match leftFor with
        | LedgerList -> left, Cmd.none, NoOp
        // Ohne Filter zieht `resolveInitialFilter` beim nächsten Besuch die gemerkte Sicht.
        | OtherPage -> { left with SelectedFilter = None }, Cmd.none, NoOp

    // ============================================
    // Regel aus der Buchung (ledger-hg5m5)
    // ============================================
    | RuleFromEntryRequested entryId ->
        match findEntry model entryId |> Option.bind ruleSeedFor with
        | Some (payee, categoryId) ->
            { model with RuleOffer = None; CategoryPickerFor = None; CategorySuggestions = [] },
            Cmd.none,
            OpenRuleForEntry (entryId, payee, categoryId)
        | None -> model, Cmd.none, NoOp

    | RunCheckRuleOffer (entryId, payee, memo, categoryId) ->
        let cmd =
            Cmd.OfAsync.either
                (Retry.reading Api.rules.ruleCategoryFor)
                (payee, memo)
                (fun result -> RuleOfferChecked (entryId, categoryId, Ok result))
                (fun ex -> RuleOfferChecked (entryId, categoryId, Error ex.Message))
        model, cmd, NoOp

    // Nur die Antwort auf die laufende Frage zählt. Gibt schon eine Regel die Kategorie, kein Angebot; ohne Urteil
    // (Netzfehler) auch keins: der feste Eintrag „Regel anlegen“ bleibt.
    | RuleOfferChecked (entryId, categoryId, result) when model.RuleOffer = Some (RuleOfferChecking (entryId, categoryId)) ->
        let offer =
            match result with
            | Ok (Some ruleCategory) when ruleCategory = categoryId -> None
            | Ok _ -> Some (RuleOfferShown (entryId, categoryId))
            | Error _ -> None
        { model with RuleOffer = offer }, Cmd.none, NoOp

    | RuleOfferChecked _ -> model, Cmd.none, NoOp

    | RuleOfferDismissed -> { model with RuleOffer = None }, Cmd.none, NoOp

    | DetailNoteFlashEnded version ->
        match model.DetailNote with
        | Some note when note.Version = version -> { model with DetailNote = Some { note with Flashing = false } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CategoryCardExpired version ->
        match model.CategoryCard with
        | Some card when card.Version = version -> { model with CategoryCard = None }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RunRefreshTotals ->
        match model.SelectedFilter with
        | None -> model, Cmd.none, NoOp
        | Some filter ->
            let accountId = accountIdOf filter
            let query = { buildPageQuery model accountId 0 with LocallyRemoved = 0 }
            let answer = totalsRefreshAnswer model
            let cmd =
                Cmd.OfAsync.either
                    (Retry.reading Api.ledger.getLedgerEntriesPage)
                    query
                    (Ok >> answer)
                    (fun ex -> answer (Error ex.Message))
            model, cmd, NoOp

    | TotalsRefreshed (generation, Ok page) when generation = model.EntriesGeneration && model.OrderStale ->
        // Nur Trefferzahl und Summe: die Zeilen bleiben, wie sie sind, bis "Neu laden".
        { model with
            TotalCount = page.TotalCount
            TotalAmount = page.TotalAmount
            DeletedCount = page.DeletedCount
            NeedsReviewCount = page.NeedsReviewCount
            AskLenaCount = page.AskLenaCount
            WithoutCategoryCount = page.WithoutCategoryCount },
        Cmd.none,
        NoOp

    | TotalsRefreshed _ -> model, Cmd.none, NoOp

    // ============================================
    // Auswahl mit Summe (ledger-r1d2f)
    // ============================================

    // Nur wählbare Zeilen kommen in die Auswahl (`isSelectableEntry`): nicht ausgegraut und geladen. Die Fokuszeile kann
    // eine nur über die Route geöffnete Buchung sein, die Leertaste geht über `ClickRowCheckbox` (ledger-cnaz3).
    | StartSelectionMode entryId when not (isSelectableEntry model entryId) -> model, Cmd.none, NoOp

    | ToggleRowSelected entryId when not (isSelectableEntry model entryId) -> model, Cmd.none, NoOp

    | ClickRowCheckbox (entryId, _) when not (isSelectableEntry model entryId) -> model, Cmd.none, NoOp

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
        let entries = selectableEntries model
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
        // Nur mit Auswahl und ohne laufenden Sammelaufruf; der Proxy-Aufruf steckt hinter `Run...`.
        match reviewMarkBatchSelection model with
        | Some selection when model.BatchRunning.IsNone ->
            { model with BatchRunning = Some ReviewMarkBatchRun }, Cmd.ofMsg (RunReviewMarkBatch selection), NoOp
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
        model', (if reload then Cmd.ofMsg RefreshEntries else Cmd.none), PostFeedback (reviewBatchFeedback outcome)

    | ReviewMarkBatchDone (Error message) ->
        { model with BatchRunning = None },
        Cmd.none,
        PostFeedback (failedFeedback "Nicht geprüft" message)

    | StartUndoReviewMarkBatch items ->
        // Wie jedes Sammel-Rückgängig nur ohne laufenden Sammelaufruf; gesperrt bleibt es still.
        if model.BatchRunning.IsSome then
            model, Cmd.none, NoOp
        else
            { model with BatchRunning = Some ReviewMarkBatchRun }, Cmd.ofMsg (RunUndoReviewMarkBatch items), NoOp

    | RunUndoReviewMarkBatch items ->
        let cmd =
            Cmd.OfAsync.either
                Api.ledger.restoreReviewMarkBatch
                items
                (fun result -> ReviewMarkBatchRestored (items, result))
                (fun ex -> ReviewMarkBatchRestored (items, Error ex.Message))
        model, cmd, NoOp

    | ReviewMarkBatchRestored (items, Ok outcome) ->
        // Die geladenen Zeilen stehen sofort wieder auf ihrem Vorzustand, weggewischte an ihrem Platz (ledger-gwr9j):
        // die Liste bleibt stehen, auch ihr Scrollstand. Unter `AllExcept` waren viele Buchungen nie geladen; das
        // Nachladen im Hintergrund holt sie und setzt Seitenversatz und Zähler auf den Stand des Servers.
        let sendFailed =
            outcome.Changed
            |> List.tryPick (fun c ->
                match c.WriteBack with
                | ReviewMarkSendFailed reason -> Some reason
                | _ -> None)
        let external =
            match sendFailed with
            | Some reason -> PostFeedback (reviewMarkNotInYnabFeedback true reason)
            | None -> NoOp
        applyUndoReviewMarkBatch items outcome { model with BatchRunning = None }, Cmd.ofMsg RefreshEntries, external

    | ReviewMarkBatchRestored (_, Error message) ->
        { model with BatchRunning = None }, Cmd.none, PostFeedback (undoFailedFeedback message)

    // ============================================
    // "Kategorie setzen" für die ganze Auswahl (ledger-w29y6)
    // ============================================

    | ToggleBatchCategoryPicker ->
        if model.BatchCategoryPickerOpen then
            { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp
        else
            // Nur mit nicht leerer Auswahl und ohne laufenden Sammelaufruf (einer sperrt alle
            // Sammelaktionen der Leiste). Öffnen lädt die Kategorien bei Stand über zwei Minuten nach.
            match reviewMarkBatchSelection model with
            | Some _ when model.BatchRunning.IsNone ->
                { model with BatchCategoryPickerOpen = true; OpenPopover = None },
                Cmd.ofMsg EnsureCategoriesFresh,
                NoOp
            | _ -> model, Cmd.none, NoOp

    | CloseBatchCategoryPicker -> { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp

    | StartCategoryBatch target ->
        match reviewMarkBatchSelection model with
        | Some selection when model.BatchRunning.IsNone ->
            { model with
                BatchRunning = Some CategoryBatchRun
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
        model', cmd, PostFeedback (categoryBatchFeedback (category |> Option.map (fun c -> c.Name)) outcome)

    | CategoryBatchDone (_, Error message) ->
        { model with BatchRunning = None },
        Cmd.none,
        PostFeedback (failedFeedback "Kategorie nicht geändert" message)

    | StartUndoCategoryBatch changes ->
        if model.BatchRunning.IsSome then
            model, Cmd.none, NoOp
        else
            { model with BatchRunning = Some CategoryBatchRun },
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
        let ended = { model with BatchRunning = None }
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
            | Some message, _ -> PostFeedback (undoFailedFeedback message)
            | None, Some reason -> PostFeedback (feedbackNote FeedbackWarning categoryHintTitle (Some reason))
            | None, None when restored = 0 -> NoOp
            | None, None -> PostFeedback (batchRestoredFeedback "Kategorie" restored)
        model', cmd, external

    // ============================================
    // "Lena fragen" für die ganze Auswahl (ledger-y54gr)
    // ============================================

    | StartLenaBatch target ->
        // Wie die anderen Sammelaktionen: nur mit Auswahl und ohne laufenden Sammelaufruf; "Lena
        // fragen" zusätzlich nur mit gesetzter Flag-Farbe. Der Proxy-Aufruf steckt hinter `Run...`.
        let blocked =
            model.BatchRunning.IsSome
            || (target = AssignLena && (selectionLenaDisabledReason model.LenaFlagColor).IsSome)
        match reviewMarkBatchSelection model with
        | Some selection when not blocked ->
            { model with BatchRunning = Some LenaBatchRun; BatchCategoryPickerOpen = false },
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
            | AssignLena -> { Color = lenaColorOf model.LenaFlagColor; Name = Some lenaFlagName }
            | WithdrawLena -> { Color = None; Name = None }
        let model' = applyFlagBatch (fun _ -> newState) true outcome model
        model', Cmd.none, PostFeedback (flagBatchFeedback target outcome)

    | LenaBatchDone (_, Error message) ->
        { model with BatchRunning = None },
        Cmd.none,
        PostFeedback (failedFeedback "Flag nicht geändert" message)

    | StartUndoFlagBatch changes ->
        if model.BatchRunning.IsSome then
            model, Cmd.none, NoOp
        else
            let items = changes |> List.map (fun (c: FlagBatchChange) -> ({ EntryId = c.EntryId; Previous = c.Previous }: FlagRestoreItem))
            { model with BatchRunning = Some LenaBatchRun }, Cmd.ofMsg (RunUndoFlagBatch items), NoOp

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
            | Some reason -> PostFeedback (feedbackNote FeedbackWarning flagHintTitle (Some reason))
            | None when List.isEmpty outcome.Changed -> NoOp
            | None -> PostFeedback (batchRestoredFeedback "Flag" (List.length outcome.Changed))
        model', Cmd.none, external

    | FlagBatchRestored (_, Error message) ->
        { model with BatchRunning = None },
        Cmd.none,
        PostFeedback (undoFailedFeedback message)

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

    | ClickRowWithModifier (entryId, _) when not (isSelectableEntry model entryId) -> model, Cmd.none, NoOp

    | ClickRowWithModifier (entryId, modifier) ->
        let entries = selectableEntries model
        // Ohne Auswahl zählt eine offene Buchung im Panel als erste gewählte (Prototyp `selectWithMods`): Strg-Klick
        // auf eine zweite Zeile wählt beide, Umschalt-Klick den Bereich ab ihr. Nur wenn sie wählbar in der Liste
        // steht: eine ausgegraute oder über die Route geöffnete, nicht geladene Buchung hätte kein Häkchen, und eine
        // Sammelaktion träfe sie trotzdem (Verifier-FAIL Iteration 1).
        let openId =
            model.DetailFor
            |> Option.map (fun d -> d.Id)
            |> Option.filter (fun id ->
                not (hasSelection model) && id <> entryId && entries |> List.exists (fun e -> e.Id = id))
        let anchor = model.SelectionAnchor |> Option.orElse openId
        let span =
            match modifier, anchor with
            | RangeToRow, Some a -> rangeBetween entries a entryId
            | _ -> []
        let selection =
            if not (List.isEmpty span) then addToSelection span model.Selection
            else
                let base' = match openId with Some id -> addToSelection [ id ] model.Selection | None -> model.Selection
                toggleSelected entryId base'
        { model with
            Selection = selection
            SelectionAnchor = (if List.isEmpty span then Some entryId else model.SelectionAnchor |> Option.orElse anchor)
            FocusedRow = Some entryId },
        Cmd.none,
        NoOp

    | ExtendSelection delta ->
        if keyboardBlocked model || model.SelectionMode then
            model, Cmd.none, NoOp
        else
            let entries = selectableEntries model
            let all = model.Entries |> RemoteData.withDefault []
            let selectable (e: LedgerEntryView) = not (model.FilteredOut.Contains e.Id)
            match movedSelectableFocus all selectable model.FocusedRow delta with
            | Some next ->
                // Die markierte Zeile und die nächste kommen dazu; ohne Markierung nur die erste bzw. letzte Zeile.
                let ids = [ yield! model.FocusedRow |> Option.filter (fun id -> entries |> List.exists (fun e -> e.Id = id)) |> Option.toList; next ]
                { model with
                    Selection = addToSelection ids model.Selection
                    SelectionAnchor = model.SelectionAnchor |> Option.orElse (List.tryHead ids)
                    FocusedRow = Some next },
                Cmd.none,
                NoOp
            | None -> model, Cmd.none, NoOp

    | LenaKeyPressed ->
        // Wie „Lena fragen“: mit Auswahl für die Auswahl, sonst für die markierte Zeile. Eine schon gefragte Zeile
        // bleibt, wie sie ist (die Taste nimmt keine Rückfrage zurück). Ist „Lena fragen“ gesperrt, nennt eine
        // Rückmeldung denselben Grund wie der Knopf.
        if keyboardBlocked model || model.SelectionMode then
            model, Cmd.none, NoOp
        elif hasSelection model then
            match selectionLenaDisabledReason model.LenaFlagColor with
            | Some reason -> model, Cmd.none, ShowToast (reason, ToastInfo)
            | None -> updateCore (StartLenaBatch AssignLena) model
        else
            match model.FocusedRow |> Option.bind (findEntry model) with
            | Some entry ->
                let action = lenaActionFor model.LenaFlagColor entry
                match action.Target, action.DisabledReason with
                | AssignLena, Some reason -> model, Cmd.none, ShowToast (reason, ToastInfo)
                | AssignLena, None -> updateCore (StartSetLenaFlag (entry.Id, AssignLena)) model
                | WithdrawLena, _ -> model, Cmd.none, NoOp
            | None -> model, Cmd.none, NoOp

    | FocusSearch ->
        if keyboardBlocked model then model, Cmd.none, NoOp
        else model, Cmd.ofEffect (fun _ -> Client.DesignSystem.Viewport.focusField ledgerSearchInputSelector), NoOp

    // ============================================
    // Durchgehen (ledger-v6s7c)
    // ============================================

    | ReviewOpened ->
        let generation = model.ReviewGeneration + 1
        let opened =
            { closeDetail model with
                Review = Some reviewStarting
                ReviewGeneration = generation
                CategoryPickerFor = None
                CategorySuggestions = []
                CategoryCard = None
                SplitEditor = None
                Capture = None }
        let loads =
            [ yield Cmd.ofMsg (RunLoadReviewQueue generation)
              match model.Accounts with
              | Success _ | Loading -> ()
              | _ -> yield Cmd.ofMsg LoadAccounts
              match model.LenaFlagColor with
              | Success _ | Loading -> ()
              | _ -> yield Cmd.ofMsg LoadLenaFlagSettings
              match model.Categories with
              | Success _ -> yield Cmd.ofMsg EnsureCategoriesFresh
              | Loading -> ()
              | _ -> yield Cmd.ofMsg LoadCategories ]
        opened, Cmd.batch loads, NoOp

    | ReviewLeft ->
        { closeDetail model with
            Review = None
            CategoryPickerFor = None
            CategorySuggestions = []
            SplitEditor = None },
        Cmd.none,
        NoOp

    | RunLoadReviewQueue generation ->
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getReviewQueue)
            ()
            (fun entries -> ReviewQueueLoaded (generation, Ok entries))
            (fun ex -> ReviewQueueLoaded (generation, Error ex.Message)),
        NoOp

    // Nur die Antwort des jüngsten Öffnens: eine späte frühere setzt die laufende Sitzung nicht zurück.
    | ReviewQueueLoaded (generation, Ok entries) when model.Review.IsSome && generation = model.ReviewGeneration ->
        let loaded =
            model
            |> editReview (fun r ->
                { r with
                    Queue = Success (entries |> List.map (fun e -> e.Id))
                    Snapshots = entries |> List.map (fun e -> e.Id, e) |> Map.ofList })
        let model', cmd = showReviewCardAt 0 loaded
        model', cmd, NoOp

    | ReviewQueueLoaded (generation, Error message) when model.Review.IsSome && generation = model.ReviewGeneration ->
        editReview (fun r -> { r with Queue = Failure message }) model, Cmd.none, NoOp

    | ReviewQueueLoaded _ -> model, Cmd.none, NoOp

    | RunReviewRefresh entryId ->
        let basis = findEntry model entryId
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerEntry)
            entryId
            (fun result -> ReviewEntryRefreshed (entryId, basis, Ok result))
            (fun ex -> ReviewEntryRefreshed (entryId, basis, Error ex.Message)),
        NoOp

    // Nur für die gezeigte Karte und nur, wenn sie sich seit dem Start der Abfrage nicht geändert hat.
    | ReviewEntryRefreshed (entryId, basis, Ok fresh) when isReviewCard model entryId && model.DetailFor = basis ->
        // Läuft für die Karte ein Prüfvermerk (die Rücknahme nach „Passt“, ledger-pz5wv), meldet der Server sie noch als
        // geprüft: sie wird nicht übersprungen und behält ihren Stand, bis die Antwort ihn setzt.
        if rowBusy ReviewMarkLock model entryId then
            model, Cmd.none, NoOp
        elif reviewSkipsEntry fresh then
            let model', cmd = advanceReview model
            model', cmd, NoOp
        else
            replaceEntry fresh.Value model, Cmd.none, NoOp

    // Absichtlich still: eine Antwort für eine andere oder inzwischen geänderte Karte ist veraltet, und
    // scheitert das Neulesen (`Error`), bleibt die Karte mit dem bekannten Stand stehen. „Passt“ und die
    // anderen Befehle melden ihre Fehler selbst.
    | ReviewEntryRefreshed _ -> model, Cmd.none, NoOp

    | RunReviewSuggestions (entryId, payee) ->
        model,
        Cmd.OfAsync.either
            (Retry.reading (Api.ledger.getPayeeCategorySuggestions payee))
            None
            (fun ids -> ReviewSuggestionsLoaded (entryId, Ok ids))
            (fun ex -> ReviewSuggestionsLoaded (entryId, Error ex.Message)),
        NoOp

    // Ohne Antwort bleiben die Vorschläge leer, die Karte geht ohne sie weiter.
    | ReviewSuggestionsLoaded (entryId, result) when isReviewCard model entryId ->
        let ids = match result with Ok ids -> ids | Error _ -> []
        editReview (fun r -> { r with Suggestions = ids }) model, Cmd.none, NoOp

    | ReviewSuggestionsLoaded _ -> model, Cmd.none, NoOp

    | RunReviewMatch matchId ->
        model,
        Cmd.OfAsync.either
            (Retry.reading Api.ledger.getLedgerEntry)
            matchId
            (fun found -> ReviewMatchLoaded (matchId, Ok found))
            (fun ex -> ReviewMatchLoaded (matchId, Error ex.Message)),
        NoOp

    // Ohne Antwort (Fehler oder keine Buchung) bleibt der Schnappschuss aus dem Ereignis.
    | ReviewMatchLoaded (matchId, Ok (Some found)) ->
        editReview (fun r -> { r with Matches = r.Matches |> Map.add matchId found }) model, Cmd.none, NoOp

    | ReviewMatchLoaded _ -> model, Cmd.none, NoOp

    | ReviewConfirm ->
        // „Passt, weiter“ teilt die Sperre mit dem Prüfvermerk aus „Details“ (ledger-smv1s) und wartet auf
        // eine laufende Kategorie-Änderung der Karte. Der offene Split-Editor hält die Karte fest (ledger-cyd1t).
        match model.Review, reviewCardOf model with
        | Some review, Some card when review.Confirming.IsNone && model.SplitEditor.IsNone ->
            whenRowIdle ReviewMarkLock card.Id model (fun () ->
                if reviewCardKindOf card = NormalCard && reviewHasTarget card && not (rowBusy CategoryLock model card.Id) then
                    editReview (fun r -> { r with Confirming = Some card.Id; Last = None }) model,
                    Cmd.ofMsg (RunReviewConfirm card.Id),
                    NoOp
                else
                    model, Cmd.none, NoOp)
        | _ -> model, Cmd.none, NoOp

    | RunReviewConfirm entryId ->
        model,
        Cmd.OfAsync.either
            (Api.ledger.setReviewMark entryId)
            Reviewed
            (fun result -> ReviewConfirmed (entryId, result))
            (fun ex -> ReviewConfirmed (entryId, Error ex.Message)),
        NoOp

    | ReviewConfirmed (entryId, Ok outcome) ->
        let released = model |> editReview (fun r -> if r.Confirming = Some entryId then { r with Confirming = None } else r)
        // Die Liste des Kassenbuchs zieht mit, falls die Buchung dort geladen ist.
        let listed = applyReviewMarkResult entryId (Some false) (writeBackFailureOf outcome) released
        // Im Durchgehen steht das Ergebnis in der Zeile unter „Passt, weiter“, gelb bei YNAB-Warnung (ledger-pz5wv);
        // nur eine Antwort nach dem Verlassen meldet die Warnung noch als Toast.
        let listed, warning =
            match listed.Review, outcome.WriteBack with
            | Some _, writeBack ->
                let result = match writeBack with ReviewMarkSendFailed reason -> LastNotInYnab reason | _ -> LastDone
                let line = reviewLastFor listed entryId LastConfirmed result (Some (LastUndo (UndoConfirmed (entryId, outcome.Previous))))
                setReviewLast (Some line) listed, NoOp
            | None, ReviewMarkSendFailed reason -> listed, ShowToast ($"{reviewMarkHintTitle}: {reason}", ToastWarning)
            | None, _ -> listed, NoOp
        if isReviewCard released entryId then
            let model', cmd = advanceReview listed
            model', cmd, warning
        else
            listed, Cmd.none, warning

    // Die Karte bleibt stehen (federt zurück), die Zeile nennt rot den Grund mit „Erneut“ (ledger-pz5wv).
    | ReviewConfirmed (entryId, Error message) ->
        let released = model |> editReview (fun r -> if r.Confirming = Some entryId then { r with Confirming = None } else r)
        match released.Review with
        | Some _ ->
            setReviewLast (Some (reviewLastFor released entryId LastConfirmed (LastFailed message) (Some (LastRetry (RetryConfirm entryId))))) released,
            Cmd.none,
            NoOp
        | None -> released, Cmd.none, ShowToast ($"Nicht geprüft: {message}", ToastError)

    | ReviewSkip ->
        match reviewCardOf model with
        // Der offene Split-Editor oder ein laufender Aufruf der Gruppe Kategorie hält die Karte fest: ein Kartenwechsel
        // verwürfe die Aufteilung im Entwurf bzw. ihre Antwort im Detail (ledger-cyd1t).
        | Some card when model.SplitEditor.IsSome || rowBusy CategoryLock model card.Id -> model, Cmd.none, NoOp
        | Some card ->
            let line = reviewLastFor model card.Id LastSkipped LastDone (Some (LastUndo (UndoSkipped card.Id)))
            let model', cmd = advanceReview (setReviewLast (Some line) model)
            model', cmd, NoOp
        | None -> model, Cmd.none, NoOp

    | ReviewLena ->
        match reviewCardOf model with
        | Some card when reviewCardKindOf card = NormalCard ->
            match lenaActionFor model.LenaFlagColor card with
            | action when action.DisabledReason.IsSome || rowBusy FlagLock model card.Id ->
                model, Cmd.none, NoOp
            | action ->
                let flagged, flagCmd, external = updateCore (StartSetLenaFlag (card.Id, action.Target)) model
                // Die Antwort schreibt die Zeile (ledger-pz5wv); „Lena fragen“ ist eine neue Aktion, „Rückfrage
                // zurücknehmen“ lässt die vorige Zeile stehen.
                let flagged =
                    if (flagged.FlagRows |> Map.tryFind card.Id) = Some FlagSending then
                        let marked = awaitReview (AwaitLena card.Id) flagged
                        if action.Target = AssignLena then setReviewLast None marked else marked
                    else flagged
                match action.Target with
                | AssignLena ->
                    let model', cmd = advanceReview flagged
                    model', Cmd.batch [ flagCmd; cmd ], external
                | WithdrawLena -> flagged, flagCmd, external
        | _ -> model, Cmd.none, NoOp

    | ReviewOpenCategory ->
        match reviewCardOf model with
        | Some card when reviewCardKindOf card = NormalCard && model.CategoryPickerFor.IsNone ->
            updateCore (OpenCategoryPicker card.Id) model
        | _ -> model, Cmd.none, NoOp

    | ReviewPickSuggestion categoryId ->
        match reviewCardOf model with
        | Some card when reviewCardKindOf card = NormalCard ->
            updateCore (StartSetEntryCategory (card.Id, Some categoryId)) model
        | _ -> model, Cmd.none, NoOp

    | ReviewConfirmNew ->
        match model.Review, reviewCardOf model with
        | Some review, Some card when review.Deciding.IsNone && reviewCardKindOf card = DuplicateCard ->
            editReview (fun r -> { r with Deciding = Some card.Id; Last = None }) model, Cmd.ofMsg (RunReviewConfirmNew card.Id), NoOp
        | _ -> model, Cmd.none, NoOp

    | RunReviewConfirmNew entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.confirmEntryIsNew
            entryId
            (fun result -> ReviewConfirmedNew (entryId, result))
            (fun ex -> ReviewConfirmedNew (entryId, Error ex.Message)),
        NoOp

    // Die Buchung wartet nicht mehr: dieselbe Stelle zeigt jetzt ihre normale Karte.
    | ReviewConfirmedNew (entryId, Ok writeBack) ->
        let released = releaseDeciding entryId model
        let withoutHold =
            match findEntry released entryId with
            | Some entry ->
                let linked = (writeBack = PushToYnabSent)
                replaceEntry
                    { entry with
                        PossibleDuplicate = None
                        LinkedToYnab = entry.LinkedToYnab || linked
                        PushToYnabFailure =
                            match writeBack with
                            | PushToYnabFailed reason -> Some { Reason = reason; FailedAt = DateTime.UtcNow }
                            | PushToYnabSent -> None }
                    released
            | None -> released
        // Im Durchgehen die Zeile ohne Rückgängig (Entscheidung Roman 3), gelb mit Grund, wenn YNAB sie nicht nimmt.
        let withoutHold, toast =
            match withoutHold.Review, writeBack with
            | Some _, _ ->
                let result = match writeBack with PushToYnabFailed reason -> LastNotInYnab reason | PushToYnabSent -> LastDone
                setReviewLast (Some (reviewLastFor withoutHold entryId LastConfirmedNew result None)) withoutHold, NoOp
            | None, PushToYnabSent -> withoutHold, ShowToast (reviewConfirmedNewText, ToastSuccess)
            | None, PushToYnabFailed reason ->
                withoutHold, ShowToast ($"Als neu übernommen. {pushRetryFailedToastTitle}: {reason}", ToastWarning)
        let refresh = if isReviewCard withoutHold entryId then Cmd.ofMsg (RunReviewRefresh entryId) else Cmd.none
        withoutHold, refresh, toast

    | ReviewConfirmedNew (entryId, Error message) ->
        let released = releaseDeciding entryId model
        match released.Review with
        | Some _ ->
            setReviewLast (Some (reviewLastFor released entryId LastConfirmedNew (LastFailed message) (Some (LastRetry (RetryConfirmNew entryId))))) released,
            Cmd.none,
            NoOp
        | None -> released, Cmd.none, ShowToast ($"Nicht übernommen: {message}", ToastError)

    | ReviewDiscard ->
        match model.Review, reviewCardOf model with
        | Some review, Some card when review.Deciding.IsNone && reviewCardKindOf card = DuplicateCard ->
            editReview (fun r -> { r with Deciding = Some card.Id; Last = None }) model, Cmd.ofMsg (RunReviewDiscard card.Id), NoOp
        | _ -> model, Cmd.none, NoOp

    | RunReviewDiscard entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.discardDuplicateEntry
            entryId
            (fun result -> ReviewDiscarded (entryId, result))
            (fun ex -> ReviewDiscarded (entryId, Error ex.Message)),
        NoOp

    | ReviewDiscarded (entryId, Ok ()) ->
        let released = releaseDeciding entryId model
        // Im Durchgehen die Zeile „Verworfen“ mit Rückgängig (ledger-pz5wv), nach dem Verlassen der Toast.
        let released, toast =
            match released.Review with
            | Some _ ->
                let line = reviewLastFor released entryId LastDiscarded LastDone (Some (LastUndo (UndoDiscarded entryId)))
                setReviewLast (Some line) released, NoOp
            | None ->
                released,
                ShowToastWithAction (reviewDiscardedText, ToastSuccess, { Label = undoToastLabel; Kind = UndoDiscardDuplicate entryId })
        if isReviewCard released entryId then
            let model', cmd = advanceReview released
            model', cmd, toast
        else
            released, Cmd.none, toast

    | ReviewDiscarded (entryId, Error message) ->
        let released = releaseDeciding entryId model
        match released.Review with
        | Some _ ->
            setReviewLast (Some (reviewLastFor released entryId LastDiscarded (LastFailed message) (Some (LastRetry (RetryDiscard entryId))))) released,
            Cmd.none,
            NoOp
        | None -> released, Cmd.none, ShowToast ($"Nicht verworfen: {message}", ToastError)

    | StartRestoreDiscardedDuplicate entryId ->
        if model.RestoringDuplicates.Contains entryId then
            model, Cmd.none, NoOp
        else
            { model with RestoringDuplicates = model.RestoringDuplicates.Add entryId },
            Cmd.ofMsg (RunRestoreDiscardedDuplicate entryId),
            NoOp

    | RunRestoreDiscardedDuplicate entryId ->
        model,
        Cmd.OfAsync.either
            Api.ledger.restoreDiscardedDuplicate
            entryId
            (fun result -> DiscardedDuplicateRestored (entryId, result))
            (fun ex -> DiscardedDuplicateRestored (entryId, Error ex.Message)),
        NoOp

    // Die Duplikat-Karte steht wieder da; das Neulesen bringt die Wartemarke zurück.
    // Der Toast meldet es auch außerhalb des Durchgehens, wo keine Karte zurückkommt.
    | DiscardedDuplicateRestored (entryId, Ok ()) ->
        let released = { model with RestoringDuplicates = model.RestoringDuplicates.Remove entryId }
        match takeAwait ((=) (AwaitUndo (UndoDiscarded entryId))) released with
        // „Rückgängig“ aus der Zeile (ledger-pz5wv): die Karte steht sichtbar wieder da, kein Toast.
        | Some (_, settled) ->
            let model', cmd = returnToReviewCard entryId settled
            markReturn (UndoDiscarded entryId) entryId model', cmd, NoOp
        | None ->
            match released.Review with
            | Some _ ->
                let model', cmd = returnToReviewCard entryId released
                model', cmd, ShowToast (reviewRestoredText, ToastSuccess)
            | None ->
                // Außerhalb des Durchgehens kommt keine Karte zurück: die Liste lädt im Hintergrund nach,
                // damit die Buchung wieder dasteht (ledger-k4n2s Punkt 63).
                released, Cmd.ofMsg RefreshEntries, ShowToast (reviewRestoredText, ToastSuccess)

    | DiscardedDuplicateRestored (entryId, Error message) ->
        let released = { model with RestoringDuplicates = model.RestoringDuplicates.Remove entryId }
        let undo = UndoDiscarded entryId
        match takeAwait ((=) (AwaitUndo undo)) released with
        | Some (_, settled) ->
            setReviewLast (Some (reviewLastFor settled entryId LastUndone (LastFailed message) (Some (LastRetry (RetryUndo undo))))) settled,
            Cmd.none,
            NoOp
        | None -> released, Cmd.none, ShowToast ($"Rückgängig nicht möglich: {message}", ToastError)

    | ReviewShowDetails ->
        match reviewCardOf model with
        | Some card ->
            { model with History = Loading; CategoryPickerFor = None; CategorySuggestions = [] }
            |> editReview (fun r -> { r with DetailShown = true }),
            Cmd.ofMsg (LoadHistory card.Id),
            NoOp
        | None -> model, Cmd.none, NoOp

    // Aufteilen von der Karte (ledger-cyd1t): dasselbe wie „Details“ und danach der Split-Editor, in einem Schritt. Steht
    // das Detail schon (am PC das Panel neben der Karte), öffnet nur der Editor darin. Gesperrt (dieselben Gründe wie im
    // Detail), auf der Duplikat-Karte, bei offener Auswahl oder Eingabe, während ein Aufruf der Gruppe Kategorie oder
    // „Passt“ der Karte läuft, bleibt alles, wie es ist.
    | ReviewOpenSplit ->
        match model.Review, reviewCardOf model with
        | Some review, Some card when
            review.Confirming.IsNone
            && reviewSplitLink card = SplitLinkFree
            && model.CategoryPickerFor.IsNone
            && not (reviewCardEditing model)
            && not (rowBusy CategoryLock model card.Id) ->
            let shown, showCmd, _ = if review.DetailShown then model, Cmd.none, NoOp else updateCore ReviewShowDetails model
            let opened, openCmd, external = updateCore (OpenSplitEditor card.Id) shown
            if opened.SplitEditor.IsSome then opened, Cmd.batch [ showCmd; openCmd ], external
            else model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | ReviewSwiped (dx, dy) ->
        match reviewCardOf model with
        | Some card when reviewCardKindOf card = NormalCard ->
            match reviewSwipeOf dx dy with
            | SwipeConfirm when reviewHasTarget card -> updateCore ReviewConfirm model
            // Ohne Ziel steht „Erst Kategorie wählen“ während des Wischs in der Zeile unter dem Knopf (View).
            | SwipeConfirm -> model, Cmd.none, NoOp
            | SwipeSkip -> updateCore ReviewSkip model
            | SwipeNothing -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | ReviewKey action ->
        match action with
        | KeyConfirm -> updateCore ReviewConfirm model
        | KeySkip -> updateCore ReviewSkip model
        | KeyOpenCategory -> updateCore ReviewOpenCategory model
        | KeyOpenSplit -> updateCore ReviewOpenSplit model
        | KeyLena -> updateCore ReviewLena model
        // Z nimmt die jüngste Aktion mit Rückgängig zurück: die Kategorie am Feld oder die Zeile (ledger-mf2bg).
        | KeyUndo ->
            match model.Review |> Option.bind reviewUndoTarget with
            | Some (UndoAtField _) -> updateCore ReviewUndoField model
            | _ -> updateCore ReviewUndoLast model
        // Esc schließt erst, was über der Karte offen ist, dann die Ansicht.
        | KeyLeave ->
            if model.CategoryPickerFor.IsSome then
                { model with CategoryPickerFor = None; CategorySuggestions = [] }, Cmd.none, NoOp
            elif model.PayeeEdit.IsSome then updateCore CancelPayeeEdit model
            elif model.MemoEdit.IsSome then updateCore CancelMemoEdit model
            // Der Split-Editor im Detail (ledger-cyd1t): erst seine Auswahl, dann der Editor, das Detail bleibt. Während
            // Speichern läuft, schließt `CloseSplitEditor` nicht, und Esc tut nichts.
            elif model.SplitEditor.IsSome then
                match model.SplitEditor with
                | Some editor when editor.Picker <> SplitEditor.NoSplitPicker -> updateCore CloseSplitPicker model
                | _ -> updateCore CloseSplitEditor model
            elif model.Review |> Option.exists (fun r -> r.DetailShown) then updateCore CloseDetail model
            else model, Cmd.none, LeaveReview

    // „Rückgängig“ der Zeile (ledger-pz5wv): nur die letzte Aktion, eine Stufe. Die Karte kommt von der Seite zurück, zu
    // der sie ging; eine offene Eingabe auf der gezeigten Karte hält sie fest, dann geschieht nichts.
    | ReviewUndoLast ->
        match model.Review |> Option.bind (fun r -> r.Last), model.Review |> Option.bind reviewUndoOffered with
        // Eine Bestätigung aus „Details“ in der Zeile (`handOverDetailNote`): dieselbe Rücknahme wie am Feld. Ist ihre
        // Buchung nicht die gezeigte Karte, kommt sie zuerst zurück (außer eine Eingabe auf der Karte hält sie fest).
        | Some { EntryId = entryId; Act = Some (LastFieldUndo kind) }, _ ->
            match detailNoteUndoMsg kind with
            | Some undoMsg when not (reviewCardEditing model && not (isReviewCard model entryId)) ->
                let returned, showCmd = returnToReviewCard entryId (setReviewLast None model)
                let sent, cmd, external = updateCore undoMsg returned
                sent, Cmd.batch [ showCmd; cmd ], external
            | _ -> model, Cmd.none, NoOp
        | _, Some undo when not (reviewUndoBlocked undo model) ->
            let cleared = setReviewLast None model
            let awaiting = awaitReview (AwaitUndo undo) cleared
            match undo with
            | UndoSkipped entryId ->
                let returned, cmd = returnToReviewCard entryId cleared
                markReturn undo entryId returned, cmd, NoOp
            // Die Karte kommt sofort zurück; solange die Rücknahme läuft, überspringt das Neulesen sie nicht.
            | UndoConfirmed (entryId, previous) ->
                let returned, showCmd = returnToReviewCard entryId awaiting
                let started, startCmd, external = updateCore (StartUndoReviewMark (entryId, previous)) returned
                markReturn undo entryId started, Cmd.batch [ showCmd; startCmd ], external
            // `StartUndoFlag` holt die Karte selbst zurück (ledger-v6s7c).
            | UndoLenaAsked (entryId, previous) ->
                let started, cmd, external = updateCore (StartUndoFlag (entryId, previous)) awaiting
                markReturn undo entryId started, cmd, external
            // Die Duplikat-Karte kommt mit der Antwort zurück (`DiscardedDuplicateRestored`).
            | UndoDiscarded entryId ->
                updateCore (StartRestoreDiscardedDuplicate entryId) awaiting
            | UndoPayeeChanged (entryId, previous) ->
                updateCore (StartUndoPayee (entryId, previous)) awaiting
        | _ -> model, Cmd.none, NoOp

    // „Erneut“ der Zeile: derselbe Befehl für dieselbe Buchung. Ist sie nicht die gezeigte Karte, kommt sie zuerst zurück
    // (außer eine Eingabe auf der gezeigten Karte hält sie fest).
    | ReviewRetryLast ->
        match model.Review |> Option.bind (fun r -> r.Last) with
        | Some { Act = Some (LastRetry retry) } ->
            let onCard (entryId: Guid) (msg: Msg) (start: Model) =
                if reviewCardEditing model && not (isReviewCard model entryId) then
                    model, Cmd.none, NoOp
                else
                    let returned, showCmd = returnToReviewCard entryId start
                    let sent, cmd, external = updateCore msg returned
                    sent, Cmd.batch [ showCmd; cmd ], external
            match retry with
            | RetryConfirm entryId -> onCard entryId ReviewConfirm model
            | RetryConfirmNew entryId -> onCard entryId ReviewConfirmNew model
            | RetryDiscard entryId -> onCard entryId ReviewDiscard model
            // Auch „Rückfrage zurücknehmen“ geht hierüber, das keine Zeile schreibt: die rote Zeile geht vorher.
            | RetryLena entryId -> onCard entryId ReviewLena (setReviewLast None model)
            | RetryPayee (entryId, payee) when (model.PayeeRows |> Map.tryFind entryId) <> Some PayeeSending ->
                let marked = model |> setReviewLast None |> awaitReview (AwaitPayee (entryId, payee))
                { marked with PayeeRows = marked.PayeeRows |> Map.add entryId PayeeSending },
                Cmd.ofMsg (RunSetPayee (entryId, payee)),
                NoOp
            | RetryPayee _ -> model, Cmd.none, NoOp
            // Am Feld der Karte (ledger-mf2bg): derselbe Befehl, die rote Zeile geht vorher.
            | RetryCategory (entryId, newId) -> onCard entryId (StartSetEntryCategory (entryId, newId)) (setReviewLast None model)
            | RetryTransfer (entryId, account) -> onCard entryId (StartSetEntryTransfer (entryId, account, false)) (setReviewLast None model)
            | RetryFieldUndo kind ->
                match kind, detailNoteUndoMsg kind with
                | (UndoCategory (entryId, _) | UndoTransfer (entryId, _) | UndoSplit (entryId, _)), Some msg ->
                    onCard entryId msg (setReviewLast None model)
                | _ -> model, Cmd.none, NoOp
            | RetryUndo undo ->
                let offered = model |> setReviewLast (model.Review |> Option.bind (fun r -> r.Last) |> Option.map (fun l -> { l with Act = Some (LastUndo undo) }))
                // Ist die Rücknahme gesperrt, bleibt die rote Zeile.
                if reviewUndoBlocked undo model then model, Cmd.none, NoOp else updateCore ReviewUndoLast offered
        | _ -> model, Cmd.none, NoOp

    // „Rückgängig“ an der Bestätigung unter dem Feld der Karte (ledger-mf2bg): dieselbe Rücknahme wie früher im Toast.
    // Die Bestätigung bleibt, bis die Antwort kommt; ein zweiter Tipp sendet nichts (`whenRowIdle`).
    | ReviewUndoField ->
        match model.Review |> Option.bind (fun r -> r.FieldNote) with
        | Some note when isReviewCard model note.EntryId ->
            match detailNoteUndoMsg note.Undo with
            | Some msg -> updateCore msg model
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | ReviewFieldFlashEnded stamp ->
        editReview
            (fun r -> { r with FieldNote = r.FieldNote |> Option.map (fun n -> if n.Stamp = stamp then { n with Flashing = false } else n) })
            model,
        Cmd.none,
        NoOp

    | EscapePressed
    | EscapePressedInField ->
        let fromField = (msg = EscapePressedInField)
        // Ein Esc schließt genau eine Stufe: Popover, dann Detail, dann Auswahl. Liegt der
        // Fokus im Popover, behandelt es Esc selbst und das Ereignis erreicht das Dokument
        // nicht; liegt er auf dem Chip, schließt diese Stufe es.
        if model.OpenPopover.IsSome then
            { model with OpenPopover = None }, Cmd.none, NoOp
        elif model.FilterSheetOpen && model.SortSheetOpen then
            // Die Sortierung im Filter-Sheet: Esc führt wie „Zurück“ zu den Filtern (ledger-npqcb).
            { model with SortSheetOpen = false }, Cmd.none, NoOp
        elif model.FilterSheetOpen then
            { model with FilterSheetOpen = false }, Cmd.none, NoOp
        elif model.BatchCategoryPickerOpen then
            { model with BatchCategoryPickerOpen = false }, Cmd.none, NoOp
        elif model.SplitEditor.IsSome then
            // Dieselben Zustände schließt `detailCloseStep` für das × im Kopf; ein neuer Zustand gehört in beide.
            // Erst die Auswahl über dem Editor, dann der Editor selbst (Entwurf verworfen).
            match model.SplitEditor with
            | Some editor when editor.Picker <> SplitEditor.NoSplitPicker -> updateCore CloseSplitPicker model
            | _ -> updateCore CloseSplitEditor model
        elif model.CategoryPickerFor.IsSome then
            { model with CategoryPickerFor = None; CategorySuggestions = [] }, Cmd.none, NoOp
        elif model.AmountEdit.IsSome then
            updateCore CancelAmountEdit model
        elif model.PayeeEdit.IsSome then
            updateCore CancelPayeeEdit model
        elif model.MemoEdit.IsSome then
            updateCore CancelMemoEdit model
        else
            match model.Capture with
            // „Neue Buchung“ im Panel (design-system-gcpws): dieselbe Kette wie im Dialog (`captureEscapeMsg`).
            | Some capture ->
                match captureEscapeFor fromField capture with
                | LeaveField -> model, Cmd.ofMsg BlurFocusedField, NoOp
                | CaptureStep step -> updateCore step model
            | None ->
                // Der Rest folgt der gemeinsamen Regel (`ListKeys.escapeStep`, ADR 0096 Punkt 2): erst das Detail,
                // die Auswahl bleibt, dann die Auswahl.
                match ListKeys.escapeStep model.DetailFor.IsSome (model.SelectionMode || hasSelection model) with
                | ListKeys.ClosePanel -> closeDetail model, Cmd.none, NoOp
                | ListKeys.ClearSelection -> updateCore ClearSelection model
                | _ -> model, Cmd.none, NoOp

/// Die Antwort auf „Buchung anlegen“ kommt auf dieser Seite an (ledger-s1weh): Nur im Kassenbuch zeigt das Panel danach
/// die neue Buchung, auf der Übersicht bleibt die Seite stehen. Der Rahmen kennt die Seite und setzt es vor der Antwort,
/// gleich wo das Formular geöffnet wurde.
let captureAnsweredOn (onLedgerPage: bool) (model: Model) : Model =
    { model with Capture = model.Capture |> Option.map (fun c -> { c with ShowsCreatedEntry = onLedgerPage }) }

/// Eine beginnende Auswahl (von leer auf nicht leer) schließt ein offenes Panel oder Sheet
/// (ADR 0053 Punkt 4, ledger-w4vte). Der Vergleich von Zustand vor und nach dem Schritt
/// deckt jeden Weg ab: Kästchen, Umschalt-Klick, Leertaste, Kopfzeile, "Alle" und das lange
/// Drücken am Handy.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    // Die Zeile am Platz einer gelöschten Buchung bleibt bis zur nächsten Aktion (ledger-k68sd).
    let model = if model.DeletedRow.IsSome && endsDeletedRow msg then { model with DeletedRow = None } else model
    let model', cmd, external = updateCore msg model
    // Das Durchgehen hält seine Karte in `DetailFor`. Hat ein Schritt im Detail sie geschlossen (etwa
    // „Buchung löschen“), steht die Karte wieder da; das Neulesen überspringt sie, wenn sie weg ist.
    // `showReviewCardAt` zeigt danach eine Karte oder die Endansicht (eine Buchung ohne Stand überspringt
    // es), der Haken greift also nur einmal und nicht bei jeder weiteren Nachricht.
    let model', cmd =
        match model'.Review, reviewQueueOf model' with
        | Some review, Some ids when review.Index < List.length ids && (reviewCardOf model').IsNone ->
            let shown, showCmd = showReviewCardAt review.Index model'
            shown, Cmd.batch [ cmd; showCmd ]
        | _ -> model', cmd
    // Detail und Formular teilen sich Panel und Sheet: öffnet eine Buchung, schließt das Formular.
    let model' =
        match model'.Capture, model'.DetailFor with
        | Some capture, Some detail when model.DetailFor <> Some detail && not capture.Saving -> { model' with Capture = None }
        | _ -> model'
    let selectionBegan = not (hasSelection model) && hasSelection model'
    // Am PC (Panel, kein Handy-Modus) zeigt das Panel die Auswahl (ledger-cnaz3, ADR 0087 Punkt 17): bei genau einer
    // gewählten Buchung ihr Detail, ab zwei die Zusammenfassung (das Detail schließt). Nur wenn sich die Auswahl
    // geändert hat, sonst könnte Esc das Detail einer gewählten Buchung nie schließen. Mit Sheet bleibt ADR 0053
    // Punkt 2: eine beginnende Auswahl schließt das Detail.
    let model', cmd =
        if model'.DetailForm = DetailPanel && not model'.SelectionMode then
            if model'.Selection = model.Selection then model', cmd
            else
                match singleSelectedEntry model' with
                | Some entry when model'.DetailFor |> Option.forall (fun d -> d.Id <> entry.Id) ->
                    let shown, loadCmd = showDetail entry model'
                    shown, Cmd.batch [ cmd; loadCmd ]
                | Some _ -> model', cmd
                | None when model'.DetailFor.IsSome && (selectionTotals model').Count >= 2 -> closeDetail model', cmd
                | None -> model', cmd
        elif model'.DetailFor.IsSome && selectionBegan then closeDetail model', cmd
        else model', cmd
    // Eine neuere Eingabe überholt den Sprung: die späte Antwort der Route darf sie nicht überschreiben.
    let routeOvertaken =
        match msg with
        | SelectFilter _
        | PresetFilter _
        | OpenDetail _
        | OpenFocusedDetail -> true
        | _ -> selectionBegan
    let model' = if routeOvertaken then { model' with PendingRouteEntry = None } else model'
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
    // Das Angebot „Regel anlegen?“ gehört zum offenen Detail seiner Buchung und zu ihrer Kategorie (ledger-hg5m5).
    let model' =
        match model'.RuleOffer with
        | Some offer when not (ruleOfferStays model' offer) -> { model' with RuleOffer = None }
        | _ -> model'
    // Detail schließt mit offener Bestätigung (ledger-p5y9s): mit „Rückgängig“ wandert sie in die Leiste, im Durchgehen in
    // die Zeile unter „Passt, weiter“. Hat der Schritt dort selbst eine Zeile geschrieben (Später, Passt), ist er die neue
    // Aktion: die Bestätigung endet still.
    let lastStampOf (m: Model) = m.Review |> Option.map (fun r -> r.LastStamp)
    let model' =
        match model'.DetailNote with
        | Some note when lastStampOf model' <> lastStampOf model && not (detailShows model' note.EntryId) ->
            { model' with DetailNote = None }
        | _ -> model'
    let model', external = handOverDetailNote (model', external)
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
