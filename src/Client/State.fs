module State

open System
open Elmish
open Shared.Domain
open Types
open Feliz.Router

// ============================================
// Model - Composed from child component models
// ============================================

type Model = {
    // Navigation
    CurrentPage: Page

    // Toast notifications
    Toasts: Toast list

    // Aktualisierung der installierten PWA (infrastructure-bnbdc, ADR 0057)
    Pwa: Pwa.PwaState

    // Child component models
    Settings: Components.Settings.Types.Model
    SyncFlow: Components.SyncFlow.Types.Model
    Rules: Components.Rules.Types.Model
    Ledger: Components.Ledger.Types.Model

    // Quick Add page form state — lifted out of SyncFlow (ynab-q7k3m). `None`
    // until the page is first opened; reset to a fresh form on each visit and
    // after a successful save.
    QuickAdd: QuickAddFormState option

    // Deduplicated templates from the Quick-Add account's recent bookings
    // (ynab-t4n8p), loaded once per page visit. RemoteData so a failed/empty
    // read degrades gracefully — no templates shown, form still usable.
    QuickAddTemplates: RemoteData<QuickAddTemplate list>
}

// ============================================
// Messages - Composed from child component messages
// ============================================

type Msg =
    // Navigation
    | NavigateTo of Page        // Triggers URL change (does NOT directly change state)
    | UrlChanged of string list // Handles URL changes from router or initial load

    // PWA kommt in den Vordergrund (ADR 0014, Frische-Kriterium): verhält sich auf der
    // Kassenbuch-Seite wie ein Seitenaufruf, sonst tut sie nichts (ledger-h4p6c).
    | VisibilityChanged

    // Installierte PWA übernimmt einen neuen Deploy (infrastructure-bnbdc, ADR 0057).
    // Die PWA kam in den Vordergrund; Parameter: `Date.now()` in ms.
    | PwaForegrounded of nowMs: int64
    // `onNeedRefresh` von `registerSW`: ein neuer Service Worker wartet.
    | PwaUpdateWaiting
    // Klick auf "Neu laden" oder sofortiges Neuladen: den wartenden Worker freigeben.
    | PwaApplyUpdate

    // Toast (two-phase removal: StartDismissToast marks exiting → ToastExited removes)
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction  // Toast mit Schaltfläche (ledger-th36h)
    | ToastActionClicked of Guid  // Klick auf die Aktion: Toast schließen, Aktion ausführen
    | StartDismissToast of Guid  // phase 1: mark exiting + schedule removal (auto or manual)
    | ToastExited of Guid        // phase 2: final removal after the exit animation

    // Child component messages
    | SettingsMsg of Components.Settings.Types.Msg
    | SyncFlowMsg of Components.SyncFlow.Types.Msg
    | RulesMsg of Components.Rules.Types.Msg
    | LedgerMsg of Components.Ledger.Types.Msg

    // Quick Add (manual transaction entry → YNAB), lifted from SyncFlow (ynab-q7k3m)
    | OpenQuickAdd
    | CloseQuickAdd
    | UpdateQuickAdd of QuickAddFormState
    | SubmitQuickAdd
    | QuickAddSaved of Result<unit, string>

    // Quick Add templates (recurring cash bookings, ynab-t4n8p)
    | LoadQuickAddTemplates
    | QuickAddTemplatesLoaded of Result<QuickAddTemplate list, string>
    | PrefillQuickAdd of QuickAddTemplate

// ============================================
// Helper Functions
// ============================================

/// Auto-dismiss delay before a toast starts its soft exit, in ms.
[<Literal>]
let private autoDismissAfterMs = 5000

/// Schedule a message after a delay. Used for both the auto-dismiss trigger and
/// the post-exit-animation removal. `Cmd.OfAsync.perform` is acceptable here: the
/// inner async only sleeps and cannot fail, so there is no error case to lose
/// (the usual `perform`-swallows-exceptions caveat does not apply).
let private delayed (ms: int) (msg: Msg) : Cmd<Msg> =
    Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep ms }) () (fun _ -> msg)

/// Human-readable rendering of a YNAB error for the Quick Add submit path.
/// Moved here with the Quick Add submit logic (ynab-q7k3m) — it was previously
/// the only consumer in the SyncFlow component.
let private ynabErrorToString (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized msg -> $"YNAB authorization failed: {msg}"
    | YnabError.BudgetNotFound budgetId -> $"Budget not found: {budgetId}"
    | YnabError.AccountNotFound accountId -> $"Account not found: {accountId}"
    | YnabError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | YnabError.RateLimitExceeded retryAfter -> $"YNAB rate limit exceeded. Retry after {retryAfter} seconds"
    | YnabError.NetworkError msg -> $"YNAB network error: {msg}"
    | YnabError.InvalidResponse msg -> $"Invalid YNAB response: {msg}"

/// A blank Quick Add form dated today. Used when first opening the page and to
/// reset the form after a successful save so another entry can follow at once.
let private freshQuickAddForm () : QuickAddFormState =
    {
        AmountText = ""
        IsOutflow = true
        Payee = ""
        CategoryId = ""
        DateText = DateTime.Now.ToString("yyyy-MM-dd")
        Memo = ""
        ShowCategoryPicker = false
        IsSaving = false
        Error = None
    }

/// Übersetzt die Beschreibung einer Toast-Aktion in die Nachricht des Bausteins, der
/// sie ausführt. Rein, damit "Rückgängig" ohne Proxy testbar ist.
let toastActionMsg (kind: ToastActionKind) : Msg =
    match kind with
    | UndoReviewMark (entryId, previous) ->
        LedgerMsg (Components.Ledger.Types.StartUndoReviewMark (entryId, previous))
    | UndoReviewMarkBatch items ->
        LedgerMsg (Components.Ledger.Types.StartUndoReviewMarkBatch items)
    | ReloadForUpdate -> PwaApplyUpdate

let private addToastWithAction
    (message: string)
    (toastType: ToastType)
    (action: ToastAction option)
    (model: Model)
    : Model * Cmd<Msg> =
    let toast = { Id = Guid.NewGuid(); Message = message; Type = toastType; Exiting = false; Action = action }
    // Auto-dismiss runs through the same StartDismissToast path as a manual close,
    // so both get the soft exit animation.
    let dismissCmd = delayed autoDismissAfterMs (StartDismissToast toast.Id)
    { model with Toasts = toast :: model.Toasts }, dismissCmd

let private addToast (message: string) (toastType: ToastType) (model: Model) : Model * Cmd<Msg> =
    addToastWithAction message toastType None model

// ============================================
// Seiten-Ladekommandos
// ============================================

/// Die Ladekommandos einer Seite. Eine Stelle, zwei Aufrufer: `init` beim
/// Direktaufruf (Deep-Link, F5) und `UrlChanged` beim Navigieren. Frueher stand
/// dieser `match` nur im `else`-Zweig von `UrlChanged`; weil `init` die Seite aus
/// der URL schon in `CurrentPage` schreibt und danach `UrlChanged` fuer genau
/// dieselbe Seite schickte, lief der Gleichheits-Guard sofort ins `Cmd.none` und
/// ein Direktaufruf lud nichts (Kassenbuch und Rules waren betroffen).
let pageLoadCmd (page: Page) : Cmd<Msg> =
    match page with
    | SyncFlow ->
        Cmd.batch [
            Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCurrentSession)
            Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCategories)
        ]
    | Rules ->
        Cmd.batch [
            Cmd.map RulesMsg (Cmd.ofMsg Components.Rules.Types.LoadRules)
            Cmd.map RulesMsg (Cmd.ofMsg Components.Rules.Types.LoadCategories)
        ]
    | Settings ->
        Cmd.map SettingsMsg (Cmd.ofMsg Components.Settings.Types.LoadSettings)
    | Styleguide ->
        // Presentational gallery — no data to load.
        Cmd.none
    | Ledger ->
        // Konten (die den Standard vorauswaehlen und damit die Buchungen
        // nachziehen) und der Status des letzten Spiegel-Laufs. Kein
        // automatischer Spiegel-Lauf: das Frische-Kriterium ist
        // Folge-Arbeit (ADR 0014, ledger-h4p6c).
        Cmd.batch [
            Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadAccounts)
            Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadBalances)
            Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadMirrorStatus)
            // Für die Kategorienauswahl der Filterleiste (ledger-j8xq4): derselbe
            // Proxy-Aufruf wie `Rules`s `LoadCategories`, deshalb auch hier im
            // Haupt-`State.fs` abgefangen, nicht hinter dem Ledger-Proxy.
            Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadCategories)
        ]
    | QuickAdd ->
        // Open a fresh form and make sure the category picker has data
        // (categories are loaded into SyncFlow at startup; retry in
        // case that failed or this is a deep link). Also load the
        // recent-booking templates once per visit (ynab-t4n8p).
        Cmd.batch [
            Cmd.ofMsg OpenQuickAdd
            Cmd.ofMsg LoadQuickAddTemplates
            Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCategories)
        ]

// ============================================
// Bereitschaftssignal (design-system-yxrxb)
// ============================================

/// Ist die aktuelle Seite fertig geladen? Das App-eigene Bereitschaftssignal für
/// `scripts/screenshot-route.mjs`: das Skript riet früher aus dem Verschwinden von
/// CSS-Klassen ("proto-spinner"/"animate-pulse"), was auf der /styleguide-Route
/// scheiterte, weil die dort gezeigten Spinner/Skeletons Demo-Inhalte der
/// Loading-Sektion sind und naturgemäß nie verschwinden. Diese Funktion trägt
/// stattdessen den tatsächlichen Ladezustand: eine Seite ohne eigenen Ladezustand
/// (Styleguide) ist sofort bereit, jede andere Seite ist bereit, wenn keiner der
/// Datenstränge mehr `Loading` ist, die `pageLoadCmd` für sie anstößt (direkt
/// abgefragte Felder). Bewusst nicht `NotAsked`-sensitiv: eine Seite, die noch gar
/// nichts angefordert hat, gilt als bereit (das betrifft nur Datenstränge, die erst
/// auf Nutzeraktion hin laden, z. B. `Settings.YnabBudgets` beim Verbindungstest).
/// `View.fs` rendert dieses Ergebnis als DOM-Attribut, das Skript wartet darauf.
let isPageReady (model: Model) : bool =
    let notLoading (rd: RemoteData<'a>) = not (RemoteData.isLoading rd)
    match model.CurrentPage with
    | Styleguide -> true
    | SyncFlow ->
        notLoading model.SyncFlow.CurrentSession
        && notLoading model.SyncFlow.SyncTransactions
    | Rules -> notLoading model.Rules.Rules
    | Settings -> notLoading model.Settings.Settings
    | QuickAdd -> notLoading model.QuickAddTemplates
    | Ledger ->
        notLoading model.Ledger.Accounts
        && notLoading model.Ledger.Balances
        && notLoading model.Ledger.Entries
        && notLoading model.Ledger.Categories

/// Läuft gerade eine Eingabe, die ein Neuladen verlieren würde (infrastructure-bnbdc,
/// ADR 0057)? Reiner Schnappschuss des Modells. Nicht erfasst ist ein Textfeld mit Fokus,
/// dessen Inhalt nicht im Modell steht: das gilt als "keine Eingabe läuft".
let isInputActive (model: Model) : bool =
    let ledger = model.Ledger
    let filters = ledger.Filters
    let draftDiffers (draft: string) (committed: string) = draft.Trim() <> committed
    let amountText = Option.map Components.Ledger.Types.formatAmountDraft >> Option.defaultValue ""
    let selectionActive =
        match ledger.Selection with
        | Components.Ledger.Types.NoSelection -> false
        | Components.Ledger.Types.Picked ids -> not (Set.isEmpty ids)
        | Components.Ledger.Types.AllExcept _ -> true
    let ledgerActive =
        ledger.DetailFor.IsSome
        || ledger.OpenPopover.IsSome
        || ledger.SortSheetOpen
        || ledger.SelectionMode
        || selectionActive
        || draftDiffers ledger.MemoDraft (filters.Memo |> Option.defaultValue "")
        || draftDiffers ledger.AmountMinDraft (amountText filters.AmountMin)
        || draftDiffers ledger.AmountMaxDraft (amountText filters.AmountMax)
        || ledger.ReviewMarkBatchRunning
    let rulesActive = model.Rules.EditingRule.IsSome
    let settingsActive = model.Settings.EditingSection.IsSome
    let quickAddActive =
        match model.QuickAdd with
        | None -> false
        | Some form ->
            form.IsSaving
            || [ form.AmountText; form.Payee; form.Memo; form.CategoryId ]
               |> List.exists (fun text -> not (String.IsNullOrWhiteSpace text))
    let syncActive =
        let sync = model.SyncFlow
        let sessionRunning =
            match sync.CurrentSession with
            | Success (Some session) ->
                match session.Status with
                | Completed | Failed _ -> false
                | _ -> true
            | _ -> false
        sessionRunning || sync.IsTanConfirming || sync.SplitEdit.IsSome || sync.InlineRuleForm.IsSome
    ledgerActive || rulesActive || settingsActive || quickAddActive || syncActive

/// Der Hinweis "Neue Version, neu laden": ein Info-Toast mit Aktion, der nicht nach 5 s
/// verschwindet (kein Auto-Dismiss-Befehl). Er geht nur durch Schließen oder Klick.
let private updateNoticeToast : Toast =
    { Id = Guid.NewGuid()
      Message = "Neue Version verfügbar."
      Type = ToastInfo
      Exiting = false
      Action = Some { Label = "Neu laden"; Kind = ReloadForUpdate } }

let private isUpdateNotice (toast: Toast) : bool =
    match toast.Action with
    | Some { Kind = ReloadForUpdate } -> true
    | _ -> false

/// Wird der Hinweis geschlossen (Schließen oder Klick), steht er wieder nicht da: das
/// nächste saubere Zurückholen kann ihn (oder das Neuladen) erneut auslösen.
let private noticeClosed (id: Guid) (model: Model) : Model =
    match model.Toasts |> List.tryFind (fun t -> t.Id = id) with
    | Some toast when isUpdateNotice toast -> { model with Pwa = { model.Pwa with NoticeShown = false } }
    | _ -> model

let private applyUpdateCmd : Cmd<Msg> = [ fun _ -> Pwa.applyUpdate () ]
let private checkForUpdateCmd : Cmd<Msg> = [ fun _ -> Pwa.checkForUpdate () ]

/// Wertet `Pwa.decide` über dem aktuellen Modell aus und führt das Ergebnis aus.
let private applyPwaDecision (model: Model) : Model * Cmd<Msg> =
    match Pwa.decide model.Pwa (isInputActive model) with
    | Pwa.Nothing -> model, Cmd.none
    | Pwa.ReloadNow -> { model with Pwa = { model.Pwa with Reloading = true } }, applyUpdateCmd
    | Pwa.ShowNotice ->
        let toast = { updateNoticeToast with Id = Guid.NewGuid() }
        { model with
            Toasts = toast :: model.Toasts
            Pwa = { model.Pwa with NoticeShown = true } },
        Cmd.none

// ============================================
// Init
// ============================================

let init () : Model * Cmd<Msg> =
    let settingsModel, settingsCmd = Components.Settings.State.init ()
    let syncFlowModel, syncFlowCmd = Components.SyncFlow.State.init ()
    let rulesModel, rulesCmd = Components.Rules.State.init ()
    // Das Kassenbuch laedt erst beim Betreten der Seite (UrlChanged), nicht beim
    // Start der App: es soll keine Abfrage ausloesen, solange niemand hineinschaut.
    let ledgerModel, _ = Components.Ledger.State.init ()

    // Parse initial page from current URL (enables deep linking)
    let initialPage = Routing.currentPage ()

    let model = {
        CurrentPage = initialPage
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        Ledger = ledgerModel
        QuickAdd = None
        QuickAddTemplates = NotAsked
    }

    // Trigger page-specific load commands for the initial (deep-linked) page.
    // Direkt ueber `pageLoadCmd`, nicht ueber `UrlChanged`: dessen Gleichheits-Guard
    // wuerde hier immer greifen, weil `CurrentPage` oben schon `initialPage` ist.
    let initialPageCmd = pageLoadCmd initialPage

    let cmd = Cmd.batch [
        Cmd.map SettingsMsg settingsCmd  // Load settings on startup - needed for categories
        Cmd.map SyncFlowMsg syncFlowCmd  // Load categories and payees on startup
        initialPageCmd
    ]
    model, cmd

// ============================================
// Update - Delegates to child components
// ============================================

let rec update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    // ============================================
    // Navigation
    // ============================================
    // NavigateTo now only triggers URL change - UrlChanged handles actual state change
    | NavigateTo page ->
        let segments = Routing.toUrlSegments page
        model, Cmd.navigate(segments |> List.toArray)

    // UrlChanged is the single place where page state actually changes
    | UrlChanged segments ->
        let page = Routing.parseUrl segments
        // Only update if page actually changed (avoids unnecessary re-renders)
        if page = model.CurrentPage then
            model, Cmd.none
        else
            { model with CurrentPage = page }, pageLoadCmd page

    // Die PWA kam in den Vordergrund: auf der Kassenbuch-Seite verhält sich das wie
    // ein Seitenaufruf, es wird nur der Lauf-Status neu geladen. Dessen Frische-Prüfung
    // (`MirrorStatusLoaded`, `Components.Ledger.State`) entscheidet, ob ein
    // Hintergrund-Lauf nötig ist. Auf jeder anderen Seite passiert nichts: ADR 0014
    // nennt das Frische-Kriterium für die Kassenbuch-Seite, nicht für die ganze App.
    | VisibilityChanged ->
        match model.CurrentPage with
        | Ledger ->
            model,
            Cmd.batch [
                Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadMirrorStatus)
                // Available je Kategorie (ledger-kwwxz): nur bei Stand älter als zwei Minuten.
                if Components.Ledger.Types.categoriesReloadDue model.Ledger DateTime.Now then
                    Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadCategories)
            ]
        | _ -> model, Cmd.none

    // Installierte PWA übernimmt einen neuen Deploy (ADR 0057). Geprüft wird höchstens
    // alle fünf Minuten; wartet schon ein Worker, wird zusätzlich `decide` ausgewertet,
    // damit ein früher nur gezeigter Hinweis beim nächsten sauberen Zurückholen lädt.
    | PwaForegrounded nowMs ->
        if Pwa.shouldCheck model.Pwa.LastCheckMs nowMs then
            let model' = { model with Pwa = { model.Pwa with LastCheckMs = Some nowMs } }
            let model'', decideCmd = applyPwaDecision model'
            model'', Cmd.batch [ checkForUpdateCmd; decideCmd ]
        else
            applyPwaDecision model

    | PwaUpdateWaiting ->
        applyPwaDecision { model with Pwa = { model.Pwa with UpdateWaiting = true } }

    | PwaApplyUpdate ->
        if model.Pwa.Reloading then model, Cmd.none
        else { model with Pwa = { model.Pwa with Reloading = true } }, applyUpdateCmd

    // ============================================
    // Toast
    // ============================================
    | ShowToast (message, toastType) ->
        addToast message toastType model

    | ShowToastWithAction (message, toastType, action) ->
        addToastWithAction message toastType (Some action) model

    | ToastActionClicked id ->
        // Der Klick schließt den Toast (derselbe zweistufige Abgang wie "Schließen") und
        // führt die Aktion aus. Ein schon abgehender oder entfernter Toast löst nichts
        // mehr aus: ein zweiter Klick darf nicht zweimal rückgängig machen.
        match model.Toasts |> List.tryFind (fun t -> t.Id = id && not t.Exiting) with
        | Some { Action = Some action } ->
            { noticeClosed id model with Toasts = Toast.markExiting id model.Toasts },
            Cmd.batch [ delayed Toast.exitDurationMs (ToastExited id); Cmd.ofMsg (toastActionMsg action.Kind) ]
        | _ -> model, Cmd.none

    | StartDismissToast id ->
        // Double-fire guard: if the toast is already exiting (or already gone),
        // do nothing — no second removal timer is scheduled. Otherwise mark it
        // exiting (drives the CSS exit animation) and schedule the final removal
        // after the exit duration.
        if Toast.isExiting id model.Toasts then
            model, Cmd.none
        else
            { noticeClosed id model with Toasts = Toast.markExiting id model.Toasts },
            delayed Toast.exitDurationMs (ToastExited id)

    | ToastExited id ->
        { model with Toasts = Toast.remove id model.Toasts }, Cmd.none

    // ============================================
    // Settings Component
    // ============================================
    | SettingsMsg settingsMsg ->
        let settingsModel', settingsCmd, externalMsg = Components.Settings.State.update settingsMsg model.Settings

        // Handle external messages from Settings component
        let externalCmd =
            match externalMsg with
            | Components.Settings.Types.NoOp -> Cmd.none
            | Components.Settings.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))

        // Sync categories to other components when settings change
        let syncCategoriesCmd =
            match settingsMsg with
            | Components.Settings.Types.SettingsLoaded (Ok settings) ->
                match settings.Ynab with
                | Some ynab when ynab.DefaultBudgetId.IsSome ->
                    // Die Kassenbuch-Filterleiste (ledger-j8xq4) wartet mit ihrem
                    // eigenen `LoadCategories` (ausgelöst über `pageLoadCmd Ledger`)
                    // u. U. auf genau dieses Ereignis hier: bei einem Deep-Link
                    // direkt auf `/ledger` (kein Navigations-Klick vorher, z. B. ein
                    // frischer Seitenaufruf) läuft `pageLoadCmd`s `LoadCategories`
                    // SYNCHRON im selben `Cmd.batch` wie `LoadSettings`, und findet
                    // `Settings.Settings` dabei zwingend noch nicht `Success`: JS ist
                    // single-threaded, `Cmd.OfAsync`s Fortsetzung (auch bei einer
                    // sofort auflösenden gemockten Antwort) kommt frühestens im
                    // nächsten Tick, die synchrone `LedgerMsg LoadCategories`-
                    // Verzweigung sieht also immer den alten Stand. Dieser Zweig holt
                    // das nach, sobald Settings tatsächlich da sind: nur wenn die
                    // Kassenbuch-Seite noch nichts hat (`NotAsked`), damit ein
                    // späteres erneutes Laden der Settings (z. B. nach einer Änderung
                    // auf der Settings-Seite) keinen bereits erfolgreichen oder
                    // fehlgeschlagenen Ladevorgang wiederholt.
                    if model.CurrentPage = Ledger && model.Ledger.Categories = NotAsked then
                        Cmd.map LedgerMsg (Cmd.ofMsg Components.Ledger.Types.LoadCategories)
                    else
                        Cmd.none
                | _ -> Cmd.none
            | _ -> Cmd.none

        { model with Settings = settingsModel' }, Cmd.batch [ Cmd.map SettingsMsg settingsCmd; externalCmd; syncCategoriesCmd ]

    // ============================================
    // SyncFlow Component
    // ============================================
    | SyncFlowMsg syncFlowMsg ->
        let syncFlowModel', syncFlowCmd, externalMsg = Components.SyncFlow.State.update syncFlowMsg model.SyncFlow

        // Handle external messages from SyncFlow component
        let externalCmd =
            match externalMsg with
            | Components.SyncFlow.Types.NoOp -> Cmd.none
            | Components.SyncFlow.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))

        { model with SyncFlow = syncFlowModel' }, Cmd.batch [ Cmd.map SyncFlowMsg syncFlowCmd; externalCmd ]

    // ============================================
    // Rules Component
    // ============================================
    | RulesMsg rulesMsg ->
        // Special handling for LoadCategories - parent needs to load them
        match rulesMsg with
        | Components.Rules.Types.LoadCategories ->
            // Load categories from Settings context
            match model.Settings.Settings with
            | Success settings ->
                match settings.Ynab with
                | Some ynab ->
                    match ynab.DefaultBudgetId with
                    | Some budgetId ->
                        let cmd =
                            Cmd.OfAsync.either
                                Api.ynab.getCategories
                                budgetId
                                (fun result -> RulesMsg (Components.Rules.Types.CategoriesLoaded result))
                                (fun ex -> RulesMsg (Components.Rules.Types.CategoriesLoaded (Error (YnabError.NetworkError ex.Message))))
                        model, cmd
                    | None -> model, Cmd.none
                | None -> model, Cmd.none
            | _ -> model, Cmd.none
        | _ ->
            let rulesModel', rulesCmd, externalMsg = Components.Rules.State.update rulesMsg model.Rules

            // Handle external messages from Rules component
            let externalCmd =
                match externalMsg with
                | Components.Rules.Types.NoOp -> Cmd.none
                | Components.Rules.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))

            { model with Rules = rulesModel' }, Cmd.batch [ Cmd.map RulesMsg rulesCmd; externalCmd ]

    // ============================================
    // Ledger Component (Kassenbuch)
    // ============================================
    | LedgerMsg Components.Ledger.Types.EnsureCategoriesFresh ->
        if Components.Ledger.Types.categoriesReloadDue model.Ledger DateTime.Now then
            update (LedgerMsg Components.Ledger.Types.LoadCategories) model
        else
            model, Cmd.none

    | LedgerMsg Components.Ledger.Types.LoadCategories ->
        // Die Kategorienauswahl der Filterleiste (ledger-j8xq4) braucht das
        // konfigurierte YNAB-Budget, derselbe Abfangpunkt wie `Rules`s
        // `LoadCategories` oben, aus demselben Grund (kein Budget im
        // Ledger-eigenen Modell). Anders als bei `Rules` (dort ein reines
        // `YnabCategory list` ohne Ladezustand) trägt `Ledger.Categories` ein
        // `RemoteData`, das `isPageReady` abfragt: der Ladezustand wird deshalb
        // hier gesetzt, nicht erst über den (in der Praxis unerreichbaren) inneren
        // `LoadCategories`-Zweig von `Components.Ledger.State.update`.
        match model.Settings.Settings with
        | Success settings ->
            match settings.Ynab with
            | Some ynab ->
                match ynab.DefaultBudgetId with
                | Some budgetId ->
                    let cmd =
                        Cmd.OfAsync.either
                            Api.ynab.getCategories
                            budgetId
                            (fun result -> LedgerMsg (Components.Ledger.Types.CategoriesLoaded (result |> Result.mapError ynabErrorToString)))
                            (fun ex -> LedgerMsg (Components.Ledger.Types.CategoriesLoaded (Error ex.Message)))
                    // Ein vorhandener Stand bleibt sichtbar, bis der neue da ist (ledger-kwwxz).
                    let categories =
                        match model.Ledger.Categories with
                        | Success _ -> model.Ledger.Categories
                        | _ -> Loading
                    { model with Ledger = { model.Ledger with Categories = categories; CategoriesRefreshing = true } }, cmd
                | None -> model, Cmd.none
            | None -> model, Cmd.none
        | _ -> model, Cmd.none

    | LedgerMsg ledgerMsg ->
        let ledgerModel', ledgerCmd, externalMsg = Components.Ledger.State.update ledgerMsg model.Ledger

        let externalCmd =
            match externalMsg with
            | Components.Ledger.Types.NoOp -> Cmd.none
            | Components.Ledger.Types.ShowToast (message, toastType) -> Cmd.ofMsg (ShowToast (message, toastType))
            | Components.Ledger.Types.ShowToastWithAction (message, toastType, action) ->
                Cmd.ofMsg (ShowToastWithAction (message, toastType, action))

        { model with Ledger = ledgerModel' }, Cmd.batch [ Cmd.map LedgerMsg ledgerCmd; externalCmd ]

    // ============================================
    // Quick Add (manual transaction entry → YNAB)
    // ============================================
    // Lifted out of the SyncFlow component (ynab-q7k3m). The submit behaviour is
    // unchanged: the request carries no account id, and the server pushes it onto
    // the configured Quick-Add account without an ImportId (ADR 0004).
    | OpenQuickAdd ->
        { model with QuickAdd = Some (freshQuickAddForm ()) }, Cmd.none

    | CloseQuickAdd ->
        { model with QuickAdd = None }, Cmd.none

    | UpdateQuickAdd form ->
        { model with QuickAdd = Some form }, Cmd.none

    | SubmitQuickAdd ->
        match model.QuickAdd with
        | None -> model, Cmd.none
        | Some form ->
            match buildQuickAddRequest form with
            | Error validationError ->
                { model with QuickAdd = Some { form with Error = Some validationError } }, Cmd.none
            | Ok request ->
                let cmd =
                    Cmd.OfAsync.either
                        Api.ynab.addManualTransaction
                        request
                        (fun result -> QuickAddSaved (result |> Result.mapError ynabErrorToString))
                        (fun ex -> QuickAddSaved (Error ex.Message))
                { model with QuickAdd = Some { form with IsSaving = true; Error = None } }, cmd

    | QuickAddSaved (Ok ()) ->
        // Stay on the page and reset to a fresh form so the next cash entry can
        // follow immediately; surface success as a toast.
        let model', toastCmd = addToast "Transaktion in YNAB gespeichert" ToastSuccess model
        { model' with QuickAdd = Some (freshQuickAddForm ()) }, toastCmd

    | QuickAddSaved (Error message) ->
        match model.QuickAdd with
        | Some form ->
            { model with QuickAdd = Some { form with IsSaving = false; Error = Some message } }, Cmd.none
        | None ->
            addToast message ToastError model

    // ============================================
    // Quick Add Templates (recurring cash bookings, ynab-t4n8p)
    // ============================================
    // One extra YNAB read per page visit (rate-limit conscious): load the recent
    // bookings of the Quick-Add account, dedup server-side, and offer them as
    // one-tap prefills. RemoteData so a failure/empty read just hides the chips.
    | LoadQuickAddTemplates ->
        let cmd =
            Cmd.OfAsync.either
                Api.ynab.getRecentQuickAddTemplates
                ()
                (fun result -> QuickAddTemplatesLoaded (result |> Result.mapError ynabErrorToString))
                (fun ex -> QuickAddTemplatesLoaded (Error ex.Message))
        { model with QuickAddTemplates = Loading }, cmd

    | QuickAddTemplatesLoaded (Ok templates) ->
        { model with QuickAddTemplates = Success templates }, Cmd.none

    | QuickAddTemplatesLoaded (Error message) ->
        { model with QuickAddTemplates = Failure message }, Cmd.none

    | PrefillQuickAdd template ->
        // Fill every field from the template; the date stays today (form default).
        // Nothing is posted — Roman reviews, adjusts and taps Speichern himself.
        let baseForm = model.QuickAdd |> Option.defaultValue (freshQuickAddForm ())
        { model with QuickAdd = Some (applyTemplateToForm template baseForm) }, Cmd.none
