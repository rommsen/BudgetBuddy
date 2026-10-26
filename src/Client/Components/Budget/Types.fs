/// Modell und Nachrichten der Budget-Seite (budgeting-k7wmd).
///
/// Die Seite liest den Monat live aus YNAB und merkt sich nichts außer dem, was gerade
/// angezeigt wird. "Targets anwenden" (budgeting-h2zfn) setzt auf diesem Modell auf: es
/// braucht den gewählten Monat (`Choice`) und die geladenen Kategorien mit Fehlbetrag und
/// Pausiert (`Month`).
module Components.Budget.Types

open Shared.Budgeting
open Types

type Model = {
    /// Welcher Monat gewählt ist. Vorgabe: der laufende.
    Choice: BudgetMonthChoice
    /// Der geladene Monat zur Wahl `Choice`.
    Month: RemoteData<BudgetMonth>
}

type Msg =
    /// Den gewählten Monat (neu) holen: Seitenaufruf und "Wiederholen".
    | Load
    /// Umschalter dieser/nächster Monat.
    | SelectMonth of BudgetMonthChoice
    /// Antwort des Servers, mit der Wahl, zu der sie gehört.
    | Loaded of BudgetMonthChoice * Result<BudgetMonth, string>

let init () : Model =
    { Choice = CurrentMonth; Month = NotAsked }

/// Antwort einnehmen. Eine Antwort zu einer inzwischen abgewählten Monatswahl wird
/// verworfen, damit ein langsamer Aufruf den gerade gezeigten Monat nicht überschreibt.
let applyLoaded (choice: BudgetMonthChoice) (result: Result<BudgetMonth, string>) (model: Model) : Model =
    if choice <> model.Choice then
        model
    else
        match result with
        | Ok month -> { model with Month = Success month }
        | Error message -> { model with Month = Failure message }

/// Monat wählen. Dieselbe Wahl noch einmal ändert nichts (`false`); eine andere setzt den
/// Monat auf `Loading` (`true` heißt: der Aufrufer muss laden).
let select (choice: BudgetMonthChoice) (model: Model) : Model * bool =
    if choice = model.Choice && model.Month <> NotAsked then
        model, false
    else
        { model with Choice = choice; Month = Loading }, true

let private monthNames =
    [| "Januar"; "Februar"; "März"; "April"; "Mai"; "Juni"
       "Juli"; "August"; "September"; "Oktober"; "November"; "Dezember" |]

/// "2026-10-01" als "Oktober 2026". Ein unlesbarer Wert bleibt, wie er ist.
let monthLabel (month: string) : string =
    match month.Split '-' with
    | [| year; mm; _ |] ->
        match System.Int32.TryParse mm with
        | true, m when m >= 1 && m <= 12 -> $"{monthNames.[m - 1]} {year}"
        | _ -> month
    | _ -> month

/// Die Kategorien eines Monats als flache Zählung, für die Leer-Anzeige.
let categoryCount (month: BudgetMonth) : int =
    month.Groups |> List.sumBy (fun g -> g.Categories.Length)
