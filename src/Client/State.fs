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

    // Child component models
    Settings: Components.Settings.Types.Model
    SyncFlow: Components.SyncFlow.Types.Model
    Rules: Components.Rules.Types.Model

    // Quick Add page form state — lifted out of SyncFlow (ynab-q7k3m). `None`
    // until the page is first opened; reset to a fresh form on each visit and
    // after a successful save.
    QuickAdd: QuickAddFormState option
}

// ============================================
// Messages - Composed from child component messages
// ============================================

type Msg =
    // Navigation
    | NavigateTo of Page        // Triggers URL change (does NOT directly change state)
    | UrlChanged of string list // Handles URL changes from router or initial load

    // Toast (two-phase removal: StartDismissToast marks exiting → ToastExited removes)
    | ShowToast of string * ToastType
    | StartDismissToast of Guid  // phase 1: mark exiting + schedule removal (auto or manual)
    | ToastExited of Guid        // phase 2: final removal after the exit animation

    // Child component messages
    | SettingsMsg of Components.Settings.Types.Msg
    | SyncFlowMsg of Components.SyncFlow.Types.Msg
    | RulesMsg of Components.Rules.Types.Msg

    // Quick Add (manual transaction entry → YNAB), lifted from SyncFlow (ynab-q7k3m)
    | OpenQuickAdd
    | CloseQuickAdd
    | UpdateQuickAdd of QuickAddFormState
    | SubmitQuickAdd
    | QuickAddSaved of Result<unit, string>

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

let private addToast (message: string) (toastType: ToastType) (model: Model) : Model * Cmd<Msg> =
    let toast = { Id = Guid.NewGuid(); Message = message; Type = toastType; Exiting = false }
    // Auto-dismiss runs through the same StartDismissToast path as a manual close,
    // so both get the soft exit animation.
    let dismissCmd = delayed autoDismissAfterMs (StartDismissToast toast.Id)
    { model with Toasts = toast :: model.Toasts }, dismissCmd

// ============================================
// Init
// ============================================

let init () : Model * Cmd<Msg> =
    let settingsModel, settingsCmd = Components.Settings.State.init ()
    let syncFlowModel, syncFlowCmd = Components.SyncFlow.State.init ()
    let rulesModel, rulesCmd = Components.Rules.State.init ()

    // Parse initial page from current URL (enables deep linking)
    let initialPage = Routing.currentPage ()

    let model = {
        CurrentPage = initialPage
        Toasts = []
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        QuickAdd = None
    }

    // Trigger page-specific load commands for the initial (deep-linked) page
    let initialPageCmd = Cmd.ofMsg (UrlChanged (Routing.toUrlSegments initialPage))

    let cmd = Cmd.batch [
        Cmd.map SettingsMsg settingsCmd  // Load settings on startup - needed for categories
        Cmd.map SyncFlowMsg syncFlowCmd  // Load categories and payees on startup
        initialPageCmd
    ]
    model, cmd

// ============================================
// Update - Delegates to child components
// ============================================

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
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
            let extraCmds =
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
                | QuickAdd ->
                    // Open a fresh form and make sure the category picker has data
                    // (categories are loaded into SyncFlow at startup; retry in
                    // case that failed or this is a deep link).
                    Cmd.batch [
                        Cmd.ofMsg OpenQuickAdd
                        Cmd.map SyncFlowMsg (Cmd.ofMsg Components.SyncFlow.Types.LoadCategories)
                    ]
            { model with CurrentPage = page }, extraCmds

    // ============================================
    // Toast
    // ============================================
    | ShowToast (message, toastType) ->
        addToast message toastType model

    | StartDismissToast id ->
        // Double-fire guard: if the toast is already exiting (or already gone),
        // do nothing — no second removal timer is scheduled. Otherwise mark it
        // exiting (drives the CSS exit animation) and schedule the final removal
        // after the exit duration.
        if Toast.isExiting id model.Toasts then
            model, Cmd.none
        else
            { model with Toasts = Toast.markExiting id model.Toasts },
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
                    // Could reload categories for SyncFlow and Rules if needed
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
