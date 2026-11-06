/// Die Leiste Monatserster (budgeting-m3x7f, Skizze Gesamt-Prototyp Zeile 45 bis 52): vier Felder über der Liste,
/// Targets prüfen, Targets anwenden, Überzogene decken und verteilen. Reine Regeln: was jedes Feld zeigt, wann die
/// Leiste steht, Kopfknopf, Ausblenden und Überzogene decken. Die View ruft sie nur auf.
module Components.Budget.MonthStart

open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Moves
open Components.Budget.Inspector
open Components.Budget.TargetCheck

/// Die vier Felder in ihrer Reihenfolge.
type MonthStartField =
    | CheckField
    | ApplyField
    | OverdrawnField
    | DistributeField

/// Der Zustand eines Felds wie in der Skizze: aktiv (Filter oder Vorschau gerade offen), erledigt oder offen.
type FieldState =
    | FieldOpen
    | FieldDone
    | FieldActive

/// Ein Feld, wie es gezeigt wird.
type MonthStartFieldView = {
    Field: MonthStartField
    /// Oben, in Mono.
    Value: string
    /// Darunter, gedämpft.
    Label: string
    State: FieldState
    /// Der Tooltip des Felds: bei nicht lesbarer Prüfung die Meldung, sonst keiner.
    Title: string option
}

/// Die Hinweise von Targets prüfen für das Feld Targets prüfen.
type CheckHints =
    /// Noch nicht geladen.
    | HintsPending
    /// Nicht lesbar, mit Meldung: das Feld bleibt leer, die Leiste bedienbar.
    | HintsUnreadable of message: string
    | HintsKnown of int

/// Was die Felder brauchen, aus Monat und Prüfung.
type MonthStartFigures = {
    Hints: CheckHints
    /// Was Targets anwenden zuweisen würde (`targetApplicationTotal`, dieselbe Quelle wie die Vorschau).
    ApplyTotal: Money
    /// Zahl der überzogenen Kategorien, wie der Filter Überzogen zählt.
    Overdrawn: int
    ToBeBudgeted: Money
}

[<Literal>]
let DoneMark = "✓"

/// Der Wert eines Felds, dessen Zahl gerade nicht lesbar ist.
[<Literal>]
let UnknownMark = "–"

/// Die Hinweise aus den Rohdaten: Zeilen außerhalb von "Passt", wie die Zähler der Seite Targets prüfen (`checkCounts`).
let hintsOf (check: RemoteData<TargetCheckData>) : CheckHints =
    match check with
    | Success data -> HintsKnown (checkCounts (checkGroups data)).Hints
    | Failure message -> HintsUnreadable message
    | NotAsked
    | Loading -> HintsPending

let figuresOf (check: RemoteData<TargetCheckData>) (month: BudgetMonth) : MonthStartFigures =
    { Hints = hintsOf check
      ApplyTotal = targetApplicationTotal month
      Overdrawn = filterCount OverdrawnOnly month.Groups
      ToBeBudgeted = month.ToBeBudgeted }

/// Ein Betrag im Feld, ohne Währungszeichen wie in der Skizze: vier Felder teilen sich am Handy 390 px.
let fieldAmountText (m: Money) : string =
    Client.DesignSystem.Money.formatAmountText false false m.Amount m.Currency

/// Die vier Felder. `active` sind die Felder, deren Filter oder Vorschau gerade offen ist; aktiv geht vor erledigt.
/// Erledigt zeigt "✓". Verteilen ist nur Anzeige, erledigt ab Zu verteilen 0 oder darunter.
let monthStartFields (figures: MonthStartFigures) (active: Set<MonthStartField>) : MonthStartFieldView list =
    let field kind label (value: string) isDone =
        let state =
            if active.Contains kind then FieldActive
            elif isDone then FieldDone
            else FieldOpen
        { Field = kind; Value = (if isDone then DoneMark else value); Label = label; State = state; Title = None }
    let checkValue, checkDone =
        match figures.Hints with
        | HintsKnown 0 -> "", true
        | HintsKnown n -> string n, false
        | HintsPending
        | HintsUnreadable _ -> UnknownMark, false
    let checkTitle =
        match figures.Hints with
        | HintsUnreadable message -> Some message
        | _ -> None
    [ { field CheckField "Targets prüfen" checkValue checkDone with Title = checkTitle }
      field ApplyField "Targets anwenden" (fieldAmountText figures.ApplyTotal) (figures.ApplyTotal.Amount <= 0m)
      field OverdrawnField "Überzogene decken" (string figures.Overdrawn) (figures.Overdrawn = 0)
      field DistributeField "verteilen" (fieldAmountText figures.ToBeBudgeted) (figures.ToBeBudgeted.Amount <= 0m) ]

/// Ist eines der Felder 1 bis 3 offen? Verteilen zählt nicht. Targets prüfen zählt nur mit bekannter Zahl über 0:
/// solange sie lädt oder nicht lesbar ist, hält sie die Leiste nicht auf (sonst stünde sie mitten im Monat nur, weil
/// das Laden scheiterte).
let anyFieldOpen (figures: MonthStartFigures) : bool =
    (match figures.Hints with
     | HintsKnown n -> n > 0
     | HintsPending
     | HintsUnreadable _ -> false)
    || figures.ApplyTotal.Amount > 0m
    || figures.Overdrawn > 0

/// Steht die Leiste? Im vergangenen Monat nie. Über den Kopfknopf geöffnet steht sie, geschlossen nicht. Sonst steht
/// sie von selbst nur im laufenden Monat, solange eines der Felder 1 bis 3 offen und sie für diesen Monat nicht
/// ausgeblendet ist (Skizze Zeile 361).
let monthStartStands (kind: MonthKind) (hidden: bool) (anyOpen: bool) (toggle: MonthStartToggle) : bool =
    match kind, toggle with
    | PastMonth, _ -> false
    | _, MonthStartOpened -> true
    | _, MonthStartClosed -> false
    | CurrentMonth, MonthStartByRule -> not hidden && anyOpen
    | FutureMonth, MonthStartByRule -> false

/// Ist die Leiste für den Monat ausgeblendet, auf dieser Seite oder laut Server?
let monthStartHiddenFor (model: Model) (month: string) : bool =
    model.MonthStartHidden.Contains month
    || (match model.Frame with
        | Success frame -> List.contains month frame.MonthStartHidden
        | _ -> false)

/// Die aktiven Felder: Überzogene decken bei Filter Überzogen, Targets anwenden bei offener Vorschau.
let activeFields (model: Model) : Set<MonthStartField> =
    Set.ofList [
        if model.Filter = OverdrawnOnly then OverdrawnField
        match model.Targets with
        | TargetsClosed -> ()
        | _ -> ApplyField
    ]

/// Die Felder der Leiste, wenn sie im dargestellten Monat steht; sonst `None`.
let monthStartBar (model: Model) : MonthStartFieldView list option =
    match model.Month, displayedMonthKind model with
    | Success month, Some kind ->
        let figures = figuresOf model.MonthStartCheck month
        if monthStartStands kind (monthStartHiddenFor model month.Month) (anyFieldOpen figures) model.MonthStartToggle then
            Some (monthStartFields figures (activeFields model))
        else
            None
    | _ -> None

/// Gibt es den Kopfknopf Monatserster? Im laufenden und in kommenden Monaten, im vergangenen nicht.
let monthStartButtonVisible (model: Model) : bool =
    displayedMonthKind model |> Option.exists targetsAllowed

/// Der Kopfknopf: steht die Leiste, schließt er sie, sonst öffnet er sie. Er schreibt nichts.
let toggleMonthStart (model: Model) : Model =
    if not (monthStartButtonVisible model) then model
    else
        let toggle = if (monthStartBar model).IsSome then MonthStartClosed else MonthStartOpened
        { model with MonthStartToggle = toggle }

/// "Ausblenden": für den dargestellten Monat ausgeblendet, der Kopfknopf gilt nicht mehr, der Filter steht auf Alle
/// (wie die Skizze). Gibt den Monat zurück, den der Server festhalten soll.
let hideMonthStart (model: Model) : Model * string option =
    match model.Month with
    | Success month ->
        { model with
            MonthStartHidden = model.MonthStartHidden.Add month.Month
            MonthStartToggle = MonthStartByRule
            Filter = AllCategories },
        Some month.Month
    | _ -> model, None

/// Was ein Feld tut: Targets prüfen öffnet die Seite, Targets anwenden die Vorschau, Überzogene decken setzt den Filter
/// (am PC, `panel`, mit erster Zeile und Tab Geld), verteilen ist nur Anzeige und setzt den Filter Alle.
let fieldMsg (panel: bool) (field: MonthStartField) : Msg =
    match field with
    | CheckField -> OpenTargetCheck
    | ApplyField -> OpenTargets
    | OverdrawnField -> CoverOverdrawn panel
    | DistributeField -> SelectFilter AllCategories

/// Die Seite wird verlassen: der Kopfknopf gilt nicht mehr.
let leaveMonthStart (model: Model) : Model =
    { model with MonthStartToggle = MonthStartByRule }

/// Feld Überzogene decken: Filter Überzogen, die Suche wird geleert, damit die Liste so viele Zeilen zeigt, wie das Feld
/// zählt. Am PC (`panel`) zusätzlich die erste überzogene Zeile, wie die Liste sie zeigt, mit Tab Geld
/// (`leiste-pc-rot.png`); am Handy nur der Filter. Während eines Monatswechsels nur der Filter: die erste Zeile stammte
/// sonst aus dem alten Monat. Ohne überzogene Zeile bleibt die leere Ansicht des Filters.
let coverOverdrawn (panel: bool) (model: Model) : Model =
    let filtered = { model with Filter = OverdrawnOnly; CategorySearch = "" }
    match model.Month with
    | Success month when panel && not (monthChanging model) ->
        match shownGroups OverdrawnOnly filtered.CategorySearch month.Groups |> List.collect (fun g -> g.Categories) with
        // Wie leiste-pc-rot.png im Modus Zuweisen ("900,00 zuweisen"), eine rote Kategorie startete sonst auf Holen.
        | first :: _ -> selectCategory first.Id { filtered with Tab = MoneyTab } |> syncMoneyTab |> switchMode AssignMode
        | [] -> filtered
    | _ -> filtered

/// Das Laden der Prüfung für das Feld beginnt: eine neue Generation, ein geladener Stand bleibt bis zur Antwort.
let beginMonthStartCheck (model: Model) : Model =
    let data =
        match model.MonthStartCheck with
        | Success data -> Success data
        | _ -> Loading
    { model with MonthStartCheck = data; MonthStartCheckGeneration = model.MonthStartCheckGeneration + 1 }

/// Die Antwort; eine fremde Generation ist überholt. Ein Fehler macht nur das Feld leer.
let applyMonthStartCheck (generation: int) (result: Result<TargetCheckData, string>) (model: Model) : Model =
    if generation <> model.MonthStartCheckGeneration then model
    else
        match result with
        | Ok data -> { model with MonthStartCheck = Success data }
        | Error message -> { model with MonthStartCheck = Failure message }
