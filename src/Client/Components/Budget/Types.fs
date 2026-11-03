/// Modell und Nachrichten der Budget-Seite (budgeting-k7wmd, Liste und Inspektor budgeting-g8s3w).
///
/// Die Seite liest den Monat live aus YNAB und hält daneben nur, was gerade unterwegs ist (Schlange,
/// laufender Aufruf, offene Verschiebungen). Der Rahmen (`BudgetFrame`) liegt je Seitenaufruf im Modell: laufender Monat,
/// Monatsfenster und Kategorie-Reihenfolge. Eine Zeile wählt die Kategorie, alles zu ihr steht im
/// Inspektor. Ob Panel oder Sheet, entscheidet allein das Rendern.
module Components.Budget.Types

open Shared.Domain
open Shared.Budgeting
open Types
open Client.DesignSystem

/// Stand des Dialogs "Targets anwenden" (budgeting-h2zfn).
type TargetsState =
    | TargetsClosed
    /// Die Vorschau wird geholt.
    | PreviewLoading
    | PreviewFailed of message: string
    /// Die Vorschau steht. `changed` heißt: sie wurde neu geladen, weil sich YNABs Stand beim
    /// Übernehmen geändert hatte (es wurde nichts geschrieben).
    | PreviewReady of preview: TargetPreview * changed: bool
    /// Übernehmen läuft.
    | Applying of preview: TargetPreview
    /// Der Aufruf selbst scheiterte: ob etwas geschrieben wurde, ist unbekannt.
    | ApplyFailed of preview: TargetPreview * message: string
    /// Ausgang der Anwendung; `retrying` während "Fehlgeschlagene erneut senden" läuft,
    /// `retryError` wenn dieser Aufruf scheiterte.
    | ApplyDone of preview: TargetPreview * report: ApplyReport * retrying: bool * retryError: string option

/// Wo der Auftrag im Tab steht. Ob er läuft, sagt `Model.MoveInFlight`.
type Submission =
    | Editing
    /// Nichts geschrieben (Monat nicht lesbar, Auftrag abgelehnt), mit Grund. Jede Änderung der Form
    /// und jeder Moduswechsel nimmt die Absage weg.
    | Rejected of string
    /// Dieselbe Absage, nur kam sie, während die Seite verlassen war: Sie hält den Tab beim
    /// Wiederkommen einmal fest, damit Roman sie lesen kann.
    | RejectedAway of string

/// Ein Aufruf aus dem Tab. Liegt auf `Model`, denn er überlebt Kategorie, Monat und Seite: am Tab
/// hing er nur, solange der Tab lebte.
type MoveCall =
    | NoMoveCall
    | MoveCallRunning
    /// Läuft, und die Seite ist verlassen.
    | MoveCallRunningAway

/// Gilt der Monat im Modell als gelesen? Nach einer Bewegung stimmt der Fehlbetrag nicht mehr, bis ein
/// Neu-Lesen gelingt: YNAB rechnet ihn, das Fortschreiben kann es nicht (`withAssigned`).
type MonthFreshness =
    | MonthRead
    | MonthStale

/// Die drei Ansichten des Tabs Geld (budgeting-c5w2n).
type MoneyMode =
    | AssignMode
    | TakeMode
    | GiveMode

/// Getippt oder per Schritt entstanden. `Exact` schützt negative Werte davor, als Rechnung zu gelten.
type AssignText =
    | Typed of string
    | Exact of decimal

/// Die Zuweisen-Eingabe. Hält Kategorie und Monat selbst: Sie wird oft erst gespeichert, wenn beide
/// wechseln. Nicht zu verwechseln mit einem Entwurf, das ist eine Reihe von Verschiebungen.
type AssignEntry = {
    CategoryId: System.Guid
    Month: string
    /// Der Stand, zu dem die Rechnung gilt.
    BaseAssigned: decimal
    Text: AssignText
}

/// Holen und Abgeben. X kommt aus `MoneyTabState`, die Richtung aus `Mode`, Beträge aus `Model.Month`.
type TransferForm = {
    /// Die Gegenseite; `None`, solange keine gewählt ist. Sie ist nie vorbelegt.
    Counterpart: MoveSide option
    /// Eingabe im Betragsfeld; `None`, solange nichts getippt wurde: dann gilt die Vorbelegung
    /// aus dem aktuellen Monat.
    AmountInput: string option
    PickerOpen: bool
    Submission: Submission
}

/// Entwurfszustand des Tabs Geld, Schlüssel Kategorie und Monat (`syncMoneyTab`).
type MoneyTabState = {
    CategoryId: System.Guid
    Month: string
    Mode: MoneyMode
    Entry: AssignEntry option
    Transfer: TransferForm
}

type AssignWriteState =
    | WriteWaiting
    | WriteRunning

/// Eine gespeicherte Zuweisung in der Schlange (ADR 0078 Punkt 5).
type AssignWrite = {
    CategoryId: System.Guid
    Month: string
    Assigned: Money
    State: AssignWriteState
}

/// Läuft "Erneut senden" für eine Verschiebung gerade?
type MoveRetryState =
    | RetryReady
    | RetryRunning

/// Eine Verschiebung mit fehlgeschlagener Zeile. Bleibt auf der Seite sichtbar, wenn der Toast weg
/// ist, bis nichts mehr offen ist.
type OpenMoveRetry = {
    ApplicationId: string
    /// "Nicht vollständig verschoben" oder "Nicht zugewiesen".
    Headline: string
    /// Der Monat der Verschiebung, etwa "Oktober 2026": der Hinweis steht auch in anderen Monaten da.
    MonthLabel: string
    Summary: string
    Reason: string
    State: MoveRetryState
    /// Kategorie und Monat, wenn eine Zeile zugewiesen werden sollte: Eine neuere Zuweisung ersetzt
    /// den Eintrag. Eine Verschiebung hat keinen.
    AssignTarget: (System.Guid * string) option
}

/// Filter über der Liste neben der Suche.
type CategoryFilter =
    | AllCategories
    | MissingOnly
    | OverdrawnOnly

/// Die Tabs des Inspektors.
type InspectorTab =
    | MoneyTab
    | EntriesTab
    | HistoryTab
    | TargetTab

/// Die Tabs in ihrer Reihenfolge, dieselbe für Leiste und Pfeiltasten.
let inspectorTabs =
    [ "Geld", MoneyTab
      "Buchungen", EntriesTab
      "Historie", HistoryTab
      "Target", TargetTab ]

/// Richtung beim Wechsel der Kategorie ohne Schließen.
type Direction =
    | Previous
    | Next

/// Wo der Fokus steht, wenn eine Pfeiltaste fällt. `TextField` sperrt beide Richtungen,
/// `SearchField` nur Links und Rechts.
type KeyFocus =
    | TextField
    | SearchField
    | OtherFocus

/// Was eine Pfeiltaste auf der Budget-Seite auslöst.
type PageKeyAction =
    | StepRow of Direction
    | FirstRow
    | LastRow
    | StepTab of Direction
    | LeaveKey

/// Die gewählte Kategorie, der einzige Auswahl-Zustand. Hält nur die Id: Zugewiesen, Aktivität und
/// Verfügbar kommen aus `Model.Month`, also live aus YNAB. Aus dem Kassenbuch kommt allein die Liste.
type SelectedCategory = {
    CategoryId: System.Guid
    Entries: RemoteData<CategoryMonthEntry list>
    /// Tipp auf eine Buchung: die Seite ist verlassen, die Auswahl wartet auf die Rückkehr.
    ReturnPending: bool
}

type Model = {
    /// Laufender Monat, Monatsfenster und Reihenfolge. Je Seitenaufruf gelesen.
    Frame: RemoteData<BudgetFrame>
    /// Der gewählte Monat. `None` heißt laufender Monat: ein offener Tab springt am Monatsersten mit.
    ChosenMonth: string option
    /// Der geladene Monat, Gruppen und Kategorien in Romans Reihenfolge. Beim Blättern bleibt der alte
    /// Monat hier stehen, bis die Antwort kommt (`monthChanging`).
    Month: RemoteData<BudgetMonth>
    /// Der Dialog "Targets anwenden".
    Targets: TargetsState
    /// Suchtext über der Kategorie-Liste (budgeting-rxxb4). Bleibt beim Monatswechsel stehen, wird
    /// nicht gespeichert.
    CategorySearch: string
    /// Filter über der Liste. Bleibt beim Monatswechsel stehen.
    Filter: CategoryFilter
    /// Der Tab Geld der gewählten Kategorie.
    TabMoney: MoneyTabState option
    /// Gespeicherte Zuweisungen: es läuft höchstens eine, die erste (ADR 0078 Punkt 5).
    AssignQueue: AssignWrite list
    /// Der laufende Aufruf aus dem Tab.
    MoveInFlight: MoveCall
    /// Sperrt den Fehlbetrag-Knopf, sonst nichts.
    Freshness: MonthFreshness
    /// "Zuletzt verwendet" im Picker der Gegenseite.
    RecentCounterparts: RemoteData<System.Guid list>
    /// Verschiebungen, die noch erneut gesendet werden können.
    OpenRetries: OpenMoveRetry list
    /// Die gewählte Kategorie. Filter und Suche wirken nur auf die Liste, sie bleibt gewählt.
    Selected: SelectedCategory option
    /// Der Tab des Inspektors. Bleibt bei Kategorie- und Monatswechsel stehen.
    Tab: InspectorTab
    /// Zählt die Abfragen der Buchungen: eine späte Antwort ersetzt keine neuere Liste.
    EntriesGeneration: int
    /// Zählt die Abfragen des Monats: eine späte Antwort ersetzt keinen neueren Stand, auch nicht
    /// bei gleicher Monatswahl (zwei Reloads nach schnellen Verschiebungen, Wechsel A, B, A).
    MonthGeneration: int
}

type Msg =
    /// Die Seite wird betreten: Rahmen und Monat holen, bei erhaltener Seite nur den Monat (`KeptPage`).
    | PageEntered
    /// Rahmen und Monat (neu) holen: "Wiederholen".
    | Load
    /// Antwort auf den Rahmen. Danach kommt der Monat.
    | FrameLoaded of Result<BudgetFrame, string>
    /// Antwort des stillen Nachlesens, wenn das Fenster gewachsen sein kann.
    | FrameRefreshed of Result<BudgetFrame, string>
    /// `‹ ›` oder der Monatsname.
    | SelectMonth of month: string
    /// Antwort auf eine Abfrage des Monats, mit ihrer Generation.
    | Loaded of generation: int * Result<BudgetMonth, string>
    /// Antwort des stillen Neu-Lesens nach einer Bewegung oder Zuweisung.
    | Reloaded of generation: int * Result<BudgetMonth, string>
    /// Knopf "Targets anwenden": Vorschau öffnen.
    | OpenTargets
    /// Antwort der Vorschau, mit dem Monat, zu dem sie gehört.
    | PreviewLoaded of month: string * Result<TargetPreview, string>
    /// "Übernehmen".
    | ConfirmTargets
    | TargetsApplied of Result<ApplyResult, string>
    /// "Fehlgeschlagene erneut senden".
    | RetryFailed
    | TargetsRetried of Result<ApplyReport, string>
    /// "Abbrechen" oder "Schließen".
    | CloseTargets
    /// Eingabe im Suchfeld über der Kategorie-Liste.
    | CategorySearchChanged of string
    /// Das ✕ im Suchfeld.
    | ClearCategorySearch
    /// Alle, Fehlt oder Überzogen.
    | SelectFilter of CategoryFilter
    /// Der Umschalter Zuweisen, Holen, Abgeben.
    | SwitchMoneyMode of MoneyMode
    /// Eingabe im Feld Zugewiesen.
    | AssignTextChanged of string
    /// Ein Schritt am Handy, -50 bis +50.
    | AssignStepped of decimal
    /// Enter oder Fokus weg im Feld Zugewiesen.
    | AssignFieldLeft
    /// Esc im Feld Zugewiesen.
    | AssignDiscarded
    /// Der Fehlbetrag-Knopf.
    | CoverMissing
    /// Antwort auf die laufende Zuweisung der Schlange. `Error` heißt: nichts geschrieben.
    | AssignAnswered of Result<ApplyReport, string>
    /// Der Aufruf selbst brach ab: ob zugewiesen wurde, ist unbekannt.
    | AssignCallLost of string
    | TransferAmountChanged of string
    /// Esc im Betragsfeld: zurück zur Vorbelegung.
    | TransferAmountReset
    | OpenPicker
    | ClosePicker
    | PickCounterpart of MoveSide
    | RecentCounterpartsLoaded of Result<System.Guid list, string>
    /// Der Knopf in Holen und Abgeben.
    | ConfirmTransfer
    /// Antwort des Servers. `Error` heißt: nichts geschrieben.
    | MoveAnswered of Result<ApplyReport, string>
    /// Der Aufruf selbst brach ab: ob etwas geschrieben wurde, ist unbekannt.
    | MoveCallLost of string
    /// "Erneut senden" am Toast oder am Hinweis auf der Seite.
    | RetryMove of applicationId: string
    | MoveRetried of applicationId: string * Result<ApplyReport, string>
    /// Tipp auf eine Zeile.
    | SelectCategory of System.Guid
    /// `˄ ˅` im Sheet.
    | StepCategory of Direction
    /// Pfeiltaste am PC, seitenweit.
    | PageKeyPressed of PageKeyAction
    /// Das Sheet schließen.
    | CloseInspector
    | SelectInspectorTab of InspectorTab
    /// Die Route der Buchung wurde nicht per Zurück verlassen: die Auswahl wartet nicht mehr.
    | ReturnAbandoned
    /// Die Budget-Seite wird verlassen: eine offene Zuweisen-Eingabe ginge sonst verloren.
    | PageLeft
    /// Die Buchungen (neu) holen: "Wiederholen".
    | LoadCategoryEntries
    | CategoryEntriesLoaded of generation: int * Result<CategoryMonthEntry list, string>
    /// Tipp auf eine Buchung im Tab Buchungen: ins Kassenbuch springen.
    | OpenEntryInLedger of System.Guid

/// Was die Seite an die App meldet (wie im Kassenbuch).
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction
    /// Sprung ins Kassenbuch auf diese Buchung.
    | JumpToEntry of System.Guid

let init () : Model =
    { Frame = NotAsked
      ChosenMonth = None
      Month = NotAsked
      Targets = TargetsClosed
      CategorySearch = ""
      Filter = AllCategories
      TabMoney = None
      AssignQueue = []
      MoveInFlight = NoMoveCall
      Freshness = MonthRead
      RecentCounterparts = NotAsked
      OpenRetries = []
      Selected = None
      Tab = MoneyTab
      EntriesGeneration = 0
      MonthGeneration = 0 }
