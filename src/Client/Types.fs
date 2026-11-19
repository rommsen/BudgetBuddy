module Types

open System

/// Represents the state of a remote data fetch
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string

/// Das Regel-Formular aus einer Buchung (ledger-hg5m5) in Teilen. Es gehört zum Tab „Regeln“
/// (`Components.Rules.View.entryForm` baut es), das Kassenbuch setzt es dorthin, wo die Buchung steht: Panel, Sheet
/// oder Dialog. Der Typ steht hier, damit die Geschwister Regeln und Kassenbuch einander nicht importieren.
type EntryRuleForm = {
    Title: string
    /// Zurück zur Buchung (am Handy der Schließen-Knopf des Sheets; ist die Auswahl der Kategorie offen, zurück ins
    /// Formular).
    OnBack: unit -> unit
    Body: Feliz.ReactElement list
    /// Am Handy der Fuß des Sheets, am PC stehen die Knöpfe im Inhalt.
    Footer: Feliz.ReactElement list option
}

/// Meldung, wenn der Muster-Test den Server nicht erreicht hat. Das ist kein Urteil über das Muster.
let ruleTestLostText = "Muster konnte nicht geprüft werden, keine Verbindung zum Server"

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

    /// Der Zustand nach einer gescheiterten Abfrage: ein vorhandener Stand (`Success`) bleibt
    /// stehen, sonst wird der Fehler zum Zustand (infrastructure-r3tw7). Ob der Fehler trotz
    /// stehendem Stand sichtbar wird, entscheidet der Aufrufer (etwa die Kontostände, ledger-pfhdq).
    let lateFailure (message: string) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Success _ -> rd
        | _ -> Failure message

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
    /// Die Übersicht, Einstieg der App unter `/` (design-system-k6kfw, ADR 0087 Punkt 9).
    | Overview
    /// Das Kassenbuch mit dem Tab „Regeln“ (categorization-n3sxw, ADR 0087 Punkt 9), `#/ledger/rules`.
    | LedgerRules
    | Settings
    | Styleguide
    /// Das Kassenbuch: Buchungen und Stand je Konto, Historie je Buchung
    /// (ledger-q5w9c).
    | Ledger
    /// Das Kassenbuch mit offenem Detail genau dieser Buchung (budgeting-m1bce).
    | LedgerEntry of entryId: System.Guid
    /// Das Budget: Monat, Zu verteilen und Kategorien, live aus YNAB (budgeting-k7wmd).
    | Budget
    /// Die allgemeine Budget-Historie: alle Durchläufe, neueste zuerst (budgeting-h7q4v).
    | BudgetHistory
    /// Targets prüfen: passt das Target zu den Ausgaben, für den laufenden Monat (budgeting-q4v8n, ADR 0082).
    | BudgetTargetCheck
    /// Kategorien verwalten: anlegen, umbenennen, Gruppe wechseln, Gruppen (budgeting-pscgb, ADR 0088).
    | BudgetCategories
    /// Durchgehen Karte für Karte unter der Übersicht (ledger-v6s7c, Styleguide §7.4).
    | Review


/// Zeigt die Seite das Kassenbuch, mit oder ohne offene Buchung?
let isLedgerPage (page: Page) : bool =
    match page with
    | Ledger
    | LedgerEntry _ -> true
    | _ -> false

/// React-Schlüssel der Seite. Liste, Route einer Buchung und Tab „Regeln“ sind dieselbe Kassenbuch-Seite: ein
/// eigener Schlüssel würde sie beim Wechsel neu aufbauen.
let pageKey (page: Page) : string =
    if isLedgerPage page || page = LedgerRules then "Ledger" else page.ToString()

/// Die Orte der Navigation (Styleguide §7.4, ADR 0087 Punkt 9): Übersicht, Kassenbuch und Budget.
type Place =
    | OverviewPlace
    | LedgerPlace
    | BudgetPlace

/// Der Ort, unter dem eine Seite hängt und der in der Navigation markiert ist. Einstellungen und
/// Styleguide hängen an keinem Ort, sie liegen hinter dem Zahnrad. Die Regeln sind ein Tab im Kassenbuch.
let placeOf (page: Page) : Place option =
    match page with
    | Overview
    | Review -> Some OverviewPlace
    | Ledger
    | LedgerEntry _
    | LedgerRules -> Some LedgerPlace
    | Budget
    | BudgetHistory
    | BudgetTargetCheck
    | BudgetCategories -> Some BudgetPlace
    | Settings
    | Styleguide -> None

/// Die Seite eines Orts.
let placePage (place: Place) : Page =
    match place with
    | OverviewPlace -> Overview
    | LedgerPlace -> Ledger
    | BudgetPlace -> Budget

/// Die Seite, zu der „Zurück“ aus den Einstellungen führt: die zuletzt offene Seite außerhalb der
/// Einstellungen. Der Styleguide hängt unter den Einstellungen und ändert das Ziel nicht.
let settingsReturn (current: Page) (target: Page) (previous: Page) : Page =
    match current, target with
    | (Settings | Styleguide), _ -> previous
    | _, (Settings | Styleguide) -> current
    | _ -> previous

/// Wohin „Zurück“ im Kopf einer Unterseite führt; `Label` ist das `aria-label` und nennt das Ziel.
type BackTarget = { Target: Page; Label: string }

/// Das `aria-label` für „Zurück“ zu dieser Seite.
let backLabel (page: Page) : string =
    match page with
    | Overview -> "Zurück zur Übersicht"
    | Review -> "Zurück zum Durchgehen"
    | Ledger
    | LedgerEntry _ -> "Zurück zum Kassenbuch"
    | LedgerRules -> "Zurück zu den Regeln"
    | Budget -> "Zurück zum Budget"
    | BudgetHistory -> "Zurück zur Budget-Historie"
    | BudgetTargetCheck -> "Zurück zu Targets prüfen"
    | BudgetCategories -> "Zurück zu den Kategorien"
    | Settings -> "Zurück zu den Einstellungen"
    | Styleguide -> "Zurück zum Styleguide"

/// Das Ziel von „Zurück“ einer Unterseite: der Styleguide zu den Einstellungen und die Einstellungen zur Seite davor (`settingsReturn`). Orte haben
/// kein „Zurück“. Die Budget-Unterseiten führen ihr „Zurück“ selbst (ein Schritt im Verlauf oder das Budget).
let backTarget (page: Page) (settingsReturnPage: Page) : BackTarget option =
    let toPage target = Some { Target = target; Label = backLabel target }
    match page with
    | Styleguide -> toPage Settings
    | Settings -> toPage settingsReturnPage
    | Overview | Review | Ledger | LedgerRules | LedgerEntry _ | Budget | BudgetHistory | BudgetTargetCheck | BudgetCategories -> None

/// URL routing helpers for hash-based navigation
module Routing =
    open Feliz.Router

    /// Eine unlesbare Id führt ins Kassenbuch ohne offene Buchung.
    let private parseEntryRoute (id: string) : Page =
        match System.Guid.TryParse id with
        | true, entryId -> LedgerEntry entryId
        | _ -> Ledger

    /// Parse URL segments to Page
    let parseUrl (segments: string list) : Page =
        match segments with
        | [] -> Overview
        // Der alte Pfad der Regelseite führt in den Tab (Lesezeichen gehen nicht ins Leere).
        | ["rules"] -> LedgerRules
        | ["ledger"; "rules"] -> LedgerRules
        | ["settings"] -> Settings
        | ["styleguide"] -> Styleguide
        // Quick Add ist kein Ort mehr (ledger-ea70s): ein altes Lesezeichen führt auf die Übersicht, wo
        // `opensCapture` die Neue Buchung öffnet.
        | ["quickadd"] -> Overview
        | ["ledger"] -> Ledger
        | ["ledger"; "entry"; id] -> parseEntryRoute id
        | ["budget"] -> Budget
        | ["budget"; "history"] -> BudgetHistory
        | ["budget"; "target-check"] -> BudgetTargetCheck
        | ["budget"; "categories"] -> BudgetCategories
        | ["review"] -> Review
        // Ein unbekannter Pfad landet auf der Übersicht.
        | _ -> Overview

    /// Convert Page to URL segments (for navigation)
    let toUrlSegments (page: Page) : string list =
        match page with
        | Overview -> []
        | LedgerRules -> ["ledger"; "rules"]
        | Settings -> ["settings"]
        | Styleguide -> ["styleguide"]
        | Ledger -> ["ledger"]
        | LedgerEntry entryId -> ["ledger"; "entry"; string entryId]
        | Budget -> ["budget"]
        | BudgetHistory -> ["budget"; "history"]
        | BudgetTargetCheck -> ["budget"; "target-check"]
        | BudgetCategories -> ["budget"; "categories"]
        | Review -> ["review"]

    /// Öffnet der Pfad "Neue Buchung" auf der Übersicht? Nur der alte Pfad von Quick Add (ledger-ea70s).
    let opensCapture (segments: string list) : bool =
        segments = ["quickadd"]

    /// Die Segmente der aktuellen URL.
    let currentSegments () : string list =
        Router.currentUrl ()

    /// Get current page from URL (for initialization)
    let currentPage () : Page =
        Router.currentUrl () |> parseUrl

/// Toast notification types
type ToastType =
    | ToastSuccess
    | ToastError
    | ToastInfo
    | ToastWarning

/// Was ein Klick auf die Aktion eines Toasts auslöst. Eine Beschreibung statt einer
/// Funktion, damit der Toast vergleichbar und testbar bleibt; die Wurzel (`State.fs`)
/// übersetzt sie in die Nachricht des Bausteins.
type ToastActionKind =
    /// "Rückgängig" nach einer Wisch-Geste auf dem Prüfvermerk (ledger-th36h, ADR 0050
    /// Punkt 10): der Vorzustand kommt aus der Antwort des Befehls.
    | UndoReviewMark of entryId: Guid * previous: Shared.Domain.ReviewMarkState
    /// "Rückgängig" nach "Geprüft" für eine ganze Auswahl (ledger-nhwrt): ein Klick stellt
    /// den Vorzustand jeder geänderten Buchung wieder her.
    | UndoReviewMarkBatch of items: Shared.Domain.ReviewMarkRestoreItem list
    /// "Rückgängig" nach einer Kategorieänderung im Detail (ledger-sfab0, ADR 0055 Nachtrag):
    /// die vorherige Kategorie kommt aus der Antwort des Befehls (`None`: ohne Kategorie).
    | UndoCategory of entryId: Guid * previous: Shared.Domain.CategoryChoice option
    /// "Rückgängig" nach einer Änderung, vor der die Buchung ein Übertrag war (ledger-qvjvs):
    /// sie wird wieder ein Übertrag auf das Konto von damals.
    | UndoTransfer of entryId: Guid * account: Shared.Domain.YnabAccountId
    /// "Rückgängig" nach dem Aufteilen, Ändern oder Aufheben (ledger-pafkp, ADR 0058): die Zeilen
    /// vor dem Speichern gehen über `setEntrySplits` zurück (eine Zeile der alten Kategorie, wenn
    /// die Buchung unaufgeteilt war).
    | UndoSplit of entryId: Guid * restore: Shared.Domain.TransactionSplit list
    /// "Rückgängig" nach "Lena fragen" oder "Rückfrage zurücknehmen" (ledger-xfjcp, ADR 0056):
    /// Farbe und Name des Vorzustands kommen aus der Antwort des Befehls.
    | UndoFlag of entryId: Guid * previous: Shared.Domain.FlagState
    /// "Rückgängig" nach dem Speichern eines Memos (ledger-fa1fp, ADR 0062): das Memo vor dem
    /// Speichern kommt aus der Antwort des Befehls (`None`: kein Memo).
    | UndoMemo of entryId: Guid * previous: string option
    /// "Rückgängig" nach dem Ändern des Betrags (ledger-m7mye, ADR 0092): der Betrag vor dem
    /// Speichern (mit Vorzeichen) aus der Antwort des Befehls. Nicht bei einer aufgeteilten Buchung.
    | UndoAmount of entryId: Guid * previous: decimal
    /// "Rückgängig" nach dem Ändern des Payees (ledger-fxt09): der Payee vor dem Ändern aus der
    /// Antwort des Befehls. Fehlt er (die Buchung hatte keinen), gibt es kein "Rückgängig".
    | UndoPayee of entryId: Guid * previous: string option
    /// "Rückgängig" nach "Buchung anlegen" im Kassenbuch (ledger-at63w): die Buchung wird in YNAB
    /// gelöscht und im Kassenbuch verworfen.
    | UndoCapturedBooking of entryId: Guid
    /// "Rückgängig" nach "Buchung löschen" im Detail (ledger-z6sya, ADR 0092): die Buchung wird in
    /// YNAB neu angelegt und gilt im Kassenbuch nicht mehr als gelöscht.
    | UndoDeleteEntry of entryId: Guid
    /// "Rückgängig" nach "Regel löschen" (categorization-n3sxw): die Regel kommt mit allen Feldern an ihre Stelle
    /// zurück (`restoreRule`).
    | UndoDeleteRule of rule: Shared.Domain.Rule * position: int
    /// "Neu laden" am Hinweis "Neue Version" der installierten PWA (infrastructure-bnbdc,
    /// ADR 0057): gibt den wartenden Service Worker frei, die Seite lädt neu.
    | ReloadForUpdate
    /// "Rückgängig" nach "Kategorie setzen" für eine ganze Auswahl (ledger-w29y6): ein Klick
    /// stellt die vorherige Kategorie jeder geänderten Buchung wieder her.
    | UndoCategoryBatch of changes: Shared.Domain.CategoryBatchChange list
    /// "Rückgängig" nach "Lena fragen" bzw. "Rückfrage zurücknehmen" für eine ganze Auswahl
    /// (ledger-y54gr): ein Klick stellt Farbe und Name jeder geänderten Buchung wieder her.
    | UndoFlagBatch of changes: Shared.Domain.FlagBatchChange list
    /// "Rückgängig" nach "Ist doppelt, verwerfen" im Durchgehen (ledger-v6s7c): die Buchung wartet
    /// wieder, ihre Duplikat-Karte steht wieder da.
    | UndoDiscardDuplicate of entryId: Guid
    /// "Erneut senden" nach einer teilweise gescheiterten Verschiebung (budgeting-afd8h):
    /// die Anwendungs-Id kommt aus dem Bericht des Servers.
    | RetryBudgetMove of applicationId: string

/// Die optionale Aktion eines Toasts: Beschriftung und was der Klick auslöst.
type ToastAction = {
    Label: string
    Kind: ToastActionKind
}

/// Toast notification.
/// `Action`: optionale Schaltfläche am Toast (ledger-th36h); ohne Aktion verhält sich
/// der Toast wie bisher.
/// `Exiting` drives the two-phase removal: a toast is first marked exiting (which
/// triggers the CSS exit animation in the view), then removed from the list after
/// the exit duration has elapsed. This is what gives toasts a soft fade-out instead
/// of vanishing abruptly (design-system-004).
type Toast = {
    Id: Guid
    Message: string
    Type: ToastType
    Exiting: bool
    Action: ToastAction option
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
// Beträge im Eingabefeld
// ============================================

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

/// Formats a decimal amount as a 2-decimal, comma-separated string ("4,50").
/// Hand-rolled to be culture-independent (identical under .NET and Fable) and to
/// round-trip through `parseAmountInput`, which accepts both "," and ".".
/// Ein halber Cent wird kaufmännisch aufgerundet (`Shared.Domain.roundHalfUp`, infrastructure-k84s2).
let formatAmountForInput (amount: decimal) : string =
    let cents = int (Shared.Domain.roundHalfUp (abs amount * 100m))
    sprintf "%d,%02d" (cents / 100) (cents % 100)

