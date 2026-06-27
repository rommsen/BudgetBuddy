module Types

open System

/// Represents the state of a remote data fetch
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

/// Utility functions for working with RemoteData values
[<RequireQualifiedAccess>]
module RemoteData =

    /// Map a function over the success value
    let map (f: 'a -> 'b) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success x -> Success (f x)
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    /// Bind a function that returns RemoteData over the success value
    let bind (f: 'a -> RemoteData<'b>) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success x -> f x
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    /// Check if the value is Loading
    let isLoading (rd: RemoteData<'a>) : bool =
        match rd with
        | Loading -> true
        | _ -> false

    /// Check if the value is Success
    let isSuccess (rd: RemoteData<'a>) : bool =
        match rd with
        | Success _ -> true
        | _ -> false

    /// Check if the value is Failure
    let isFailure (rd: RemoteData<'a>) : bool =
        match rd with
        | Failure _ -> true
        | _ -> false

    /// Check if the value is NotAsked
    let isNotAsked (rd: RemoteData<'a>) : bool =
        match rd with
        | NotAsked -> true
        | _ -> false

    /// Convert to Option, returning None for non-Success states
    let toOption (rd: RemoteData<'a>) : 'a option =
        match rd with
        | Success x -> Some x
        | _ -> None

    /// Get the success value or a default
    let withDefault (defaultValue: 'a) (rd: RemoteData<'a>) : 'a =
        match rd with
        | Success x -> x
        | _ -> defaultValue

    /// Map a function over the error message
    let mapError (f: string -> string) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure err -> Failure (f err)
        | other -> other

    /// Recover from an error with a default value
    let recover (defaultValue: 'a) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure _ -> Success defaultValue
        | other -> other

    /// Recover from an error with a function that produces a value from the error
    let recoverWith (f: string -> 'a) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure err -> Success (f err)
        | other -> other

    /// Combine two RemoteData values, succeeding only if both succeed
    let map2 (f: 'a -> 'b -> 'c) (rd1: RemoteData<'a>) (rd2: RemoteData<'b>) : RemoteData<'c> =
        match rd1, rd2 with
        | Success a, Success b -> Success (f a b)
        | Failure e, _ -> Failure e
        | _, Failure e -> Failure e
        | Loading, _ -> Loading
        | _, Loading -> Loading
        | NotAsked, _ -> NotAsked
        | _, NotAsked -> NotAsked

    /// Get the error message if in Failure state
    let toError (rd: RemoteData<'a>) : string option =
        match rd with
        | Failure err -> Some err
        | _ -> None

    /// Fold over the RemoteData value
    let fold (onNotAsked: 'b) (onLoading: 'b) (onSuccess: 'a -> 'b) (onFailure: string -> 'b) (rd: RemoteData<'a>) : 'b =
        match rd with
        | NotAsked -> onNotAsked
        | Loading -> onLoading
        | Success x -> onSuccess x
        | Failure err -> onFailure err

    /// Convert a Result to RemoteData
    let fromResult (result: Result<'a, string>) : RemoteData<'a> =
        match result with
        | Ok x -> Success x
        | Error err -> Failure err

    /// Convert an Option to RemoteData (None becomes NotAsked)
    let fromOption (opt: 'a option) : RemoteData<'a> =
        match opt with
        | Some x -> Success x
        | None -> NotAsked

    /// Convert an Option to RemoteData (None becomes Failure with given message)
    let fromOptionWithError (errorMsg: string) (opt: 'a option) : RemoteData<'a> =
        match opt with
        | Some x -> Success x
        | None -> Failure errorMsg

/// Application pages/routes
type Page =
    | SyncFlow
    | Rules
    | Settings
    | Styleguide
    /// Manual transaction entry (Quick Add) as a first-class, sync-flow-independent
    /// page reachable from the main navigation (ynab-q7k3m).
    | QuickAdd


/// URL routing helpers for hash-based navigation
module Routing =
    open Feliz.Router

    /// Parse URL segments to Page
    let parseUrl (segments: string list) : Page =
        match segments with
        | [] -> SyncFlow
        | ["rules"] -> Rules
        | ["settings"] -> Settings
        | ["styleguide"] -> Styleguide
        | ["quickadd"] -> QuickAdd
        | _ -> SyncFlow  // Fallback to SyncFlow for unknown routes

    /// Convert Page to URL segments (for navigation)
    let toUrlSegments (page: Page) : string list =
        match page with
        | SyncFlow -> []
        | Rules -> ["rules"]
        | Settings -> ["settings"]
        | Styleguide -> ["styleguide"]
        | QuickAdd -> ["quickadd"]

    /// Get current page from URL (for initialization)
    let currentPage () : Page =
        Router.currentUrl () |> parseUrl

/// Toast notification types
type ToastType =
    | ToastSuccess
    | ToastError
    | ToastInfo
    | ToastWarning

/// Toast notification.
/// `Exiting` drives the two-phase removal: a toast is first marked exiting (which
/// triggers the CSS exit animation in the view), then removed from the list after
/// the exit duration has elapsed. This is what gives toasts a soft fade-out instead
/// of vanishing abruptly (design-system-004).
type Toast = {
    Id: Guid
    Message: string
    Type: ToastType
    Exiting: bool
}

/// Toast lifecycle helpers — pure, so the exiting→removed transition is unit-testable
/// without an Elmish runtime (design-system-004). The exit duration MUST match the
/// CSS `animate-toast-out` keyframe duration in `styles.css`.
module Toast =

    /// Duration of the exit animation in ms. Kept in lock-step with the
    /// `.animate-toast-out` keyframe in `styles.css`. The MVU timer that finally
    /// removes a toast waits this long after marking it exiting.
    [<Literal>]
    let exitDurationMs = 220

    /// Mark the toast with `id` as exiting. Idempotent: re-marking an already-exiting
    /// toast leaves the list unchanged, so a rapid second dismiss (auto + manual, or a
    /// double click) does not restart the lifecycle or schedule a duplicate timer.
    let markExiting (id: Guid) (toasts: Toast list) : Toast list =
        toasts
        |> List.map (fun t -> if t.Id = id then { t with Exiting = true } else t)

    /// True if a (still-present) toast with `id` is already marked exiting. Used as the
    /// double-fire guard before scheduling the removal timer.
    let isExiting (id: Guid) (toasts: Toast list) : bool =
        toasts |> List.exists (fun t -> t.Id = id && t.Exiting)

    /// Final removal of the toast with `id` from the list.
    let remove (id: Guid) (toasts: Toast list) : Toast list =
        toasts |> List.filter (fun t -> t.Id <> id)

// ============================================
// Quick Add (manual transaction entry → YNAB)
// ============================================
// Lifted out of the SyncFlow component (ynab-q7k3m): Quick Add is now a
// top-level page, so its form state and pure helpers live in the shared client
// Types module. `parseAmountInput` is also used by the SyncFlow split editor
// (`parseSplitAmount`), so this is its natural shared home. Submit behaviour is
// unchanged — the request carries no account id; the server pushes onto the
// configured Quick-Add account, without an ImportId (ADR 0004).

/// Form state for the Quick Add page (manual transaction entry → YNAB)
type QuickAddFormState = {
    /// Raw user input; parsed leniently (both "4,50" and "4.50" are valid)
    AmountText: string
    /// true = expense (default), false = income
    IsOutflow: bool
    Payee: string
    /// Selected category id as string; "" = no category
    CategoryId: string
    /// ISO date (yyyy-MM-dd), as produced by <input type="date">
    DateText: string
    Memo: string
    /// Category picker (elevated sheet layer) open on top of the page
    ShowCategoryPicker: bool
    IsSaving: bool
    Error: string option
}

/// Parses a user-entered amount, accepting German comma decimals ("4,50")
/// as well as "4.50". Hand-rolled to behave identically under .NET and Fable
/// (TryParse overloads are culture-dependent on .NET but not in JS).
/// Accepts at most 2 decimal places and amounts below 1 billion.
let parseAmountInput (text: string) : decimal option =
    let normalized = (text: string).Trim().Replace(" ", "").Replace(",", ".")

    let isDigitsOnly (s: string) =
        s.Length > 0 && s |> Seq.forall System.Char.IsDigit

    match normalized.Split('.') with
    | [| whole |] when isDigitsOnly whole && whole.Length <= 9 ->
        Some (decimal (int whole))
    | [| whole; frac |] when isDigitsOnly whole && whole.Length <= 9 && isDigitsOnly frac && frac.Length <= 2 ->
        let fracPadded = frac.PadRight(2, '0')
        Some (decimal (int whole) + decimal (int fracPadded) / 100m)
    | _ -> None

/// Builds the API request from the Quick Add form. Pure — unit-testable.
let buildQuickAddRequest (form: QuickAddFormState) : Result<Shared.Domain.ManualTransactionRequest, string> =
    match parseAmountInput form.AmountText with
    | None -> Error "Bitte einen gültigen Betrag eingeben"
    | Some amount when amount <= 0m -> Error "Der Betrag muss größer als 0 sein"
    | Some amount ->
        // Payee is optional — YNAB allows payee-less transactions
        match System.DateTime.TryParse(form.DateText) with
        | false, _ -> Error "Bitte ein gültiges Datum wählen"
        | true, date ->
            let categoryId =
                match System.Guid.TryParse(form.CategoryId) with
                | true, guid -> Some (Shared.Domain.YnabCategoryId guid)
                | false, _ -> None

            let request : Shared.Domain.ManualTransactionRequest = {
                Amount = amount
                IsOutflow = form.IsOutflow
                PayeeName = form.Payee.Trim()
                CategoryId = categoryId
                Date = date
                Memo =
                    if System.String.IsNullOrWhiteSpace form.Memo then None
                    else Some (form.Memo.Trim())
            }
            Ok request
