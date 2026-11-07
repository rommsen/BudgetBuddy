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
open Components.Budget.TargetFormCore

/// Die Vorschau im Dialog "Targets anwenden" mit der Auswahl (budgeting-m3x7f).
type PreviewShown = {
    /// Die Vorschau des Servers mit allen Zeilen.
    Shown: TargetPreview
    /// Sie wurde neu geladen, weil sich der Stand beim Übernehmen geändert hatte (es wurde nichts geschrieben).
    Changed: bool
    /// Die abgewählten Kategorien. Beim Öffnen leer: alle Zeilen sind gewählt. Nach "Stand hat sich geändert" bleibt die
    /// Abwahl für jede Kategorie, die in der neuen Vorschau noch steht.
    Excluded: Set<System.Guid>
}

/// Ausgang einer Anwendung im Dialog (budgeting-m3x7f, vorher ein Tupel).
type AppliedTargets = {
    /// Die gesendete Vorschau, nur mit den gewählten Zeilen.
    Sent: TargetPreview
    Report: ApplyReport
    /// "Fehlgeschlagene erneut senden" läuft.
    Retrying: bool
    /// Der Aufruf von "Fehlgeschlagene erneut senden" scheiterte, mit Meldung.
    RetryError: string option
}

/// Stand des Dialogs "Targets anwenden" (budgeting-h2zfn).
type TargetsState =
    | TargetsClosed
    /// Die Vorschau wird geholt.
    | PreviewLoading
    | PreviewFailed of message: string
    /// Die Vorschau steht, mit Auswahl.
    | PreviewReady of PreviewShown
    /// Übernehmen läuft, mit der Vorschau und ihrer Auswahl. Gesendet sind nur die gewählten Zeilen (`selectedPreview`).
    | Applying of PreviewShown
    /// Der Aufruf selbst scheiterte: ob etwas geschrieben wurde, ist unbekannt.
    | ApplyFailed of preview: TargetPreview * message: string
    /// Ausgang der Anwendung.
    | ApplyDone of AppliedTargets

/// Wie die Leiste Monatserster gerade steht (budgeting-m3x7f). Der Kopfknopf Monatserster überstimmt die Regel, bis die
/// Seite verlassen oder "Ausblenden" gedrückt wird. Er schreibt nichts auf den Server.
type MonthStartToggle =
    /// Die Regel entscheidet (`monthStartStands`).
    | MonthStartByRule
    /// Über den Kopfknopf geöffnet: steht auch ausgeblendet oder fertig.
    | MonthStartOpened
    /// Über den Kopfknopf geschlossen.
    | MonthStartClosed

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

/// Der Aufruf Target pausieren oder Pause aufheben (budgeting-t6p8z). Trägt Kategorie und Monat: die
/// Antwort wirkt nur auf sie, auch wenn inzwischen eine andere Kategorie oder ein anderer Monat gezeigt
/// wird. Er sperrt weder Auswahl noch Monatswechsel.
type PauseCall =
    | PauseIdle
    /// `generation` ist `MonthGeneration` beim Start: hat sie sich bis zur Antwort geändert, kann ein
    /// Lesen des Monats die Pause verpasst haben (budgeting-r3wq8).
    | PauseRunning of categoryId: System.Guid * month: string * generation: int
    /// Der Server lehnte ab oder der Aufruf brach ab, mit Meldung. Gilt, solange Kategorie und Monat
    /// gezeigt werden.
    | PauseRejected of categoryId: System.Guid * month: string * message: string

/// Wo ein Target-Aufruf ausgelöst wurde (budgeting-q4v8n): nur das Formular der Herkunft nimmt die Antwort
/// ein, nur ihr Knopf dreht.
type TargetOrigin =
    /// Das Formular im Tab Target.
    | FromTargetTab
    /// "<Betrag> übernehmen" an einer Zeile der Seite Targets prüfen.
    | FromCheckRow
    /// Das Formular im Panel der Seite Targets prüfen ("Anpassen …").
    | FromCheckPanel

/// Der Aufruf Target ändern oder entfernen. Trägt Kategorie, Monat und Herkunft: die Antwort wirkt nur auf
/// Kategorie und Monat, ins Formular kommt sie nur bei der Herkunft. Er sperrt weder Auswahl noch
/// Monatswechsel. Es läuft höchstens einer, für Tab und Seite Targets prüfen gemeinsam (budgeting-n7h3x).
type TargetCall =
    | TargetIdle
    | TargetSaving of categoryId: System.Guid * month: string * action: TargetAction * origin: TargetOrigin

/// Der Filter nach Art in der allgemeinen Historie (budgeting-h7q4v).
type RunFilter =
    | AllRuns
    | RunsOf of HistoryOrigin

/// Die allgemeine Historie `#/budget/history`. Liegt auf der Budget-Seite, damit die Rückkehr den
/// Monat behält.
type GeneralHistory = {
    /// Die Durchläufe, einmal je Laden aus den Bewegungen gebildet, neueste zuerst.
    Runs: RemoteData<HistoryRun list>
    Filter: RunFilter
    /// Die aufgeklappten Durchläufe Targets anwenden.
    Expanded: Set<System.Guid>
    /// Über den Knopf Historie geöffnet: die Rückkehr zur Budget-Seite behält Monat und Auswahl.
    ReturnPending: bool
    /// Zählt je Betreten und je "Wiederholen" hoch. Eine Antwort mit fremder Generation ist überholt und
    /// wird verworfen (budgeting-c6vn4, A1; Vorbild `HistoryGeneration`).
    Generation: int
}

/// Die Seite Targets prüfen `#/budget/target-check` (budgeting-q4v8n, ADR 0082). Liegt auf der Budget-Seite
/// wie `GeneralHistory`, damit die Rückkehr Monat und Auswahl behält.
type TargetCheckPage = {
    /// Die Rohdaten des laufenden Monats. Eingeordnet wird beim Rendern mit `targetCheckGroups`.
    Data: RemoteData<TargetCheckData>
    /// Die aufgeklappten Gruppen (budgeting-c4ve4). Beim Betreten alle außer "Passt".
    OpenGroups: Set<CheckGroup>
    /// Das Formular von "Anpassen …", `None`: das Panel ist zu. Hält Kategorie und Monat der Zeile.
    Panel: TargetForm option
    /// Über den Knopf "Targets prüfen" geöffnet: die Rückkehr zur Budget-Seite behält Monat und Auswahl.
    ReturnPending: bool
    /// Zählt je Betreten, je "Wiederholen" und je stillem Neuladen hoch. Eine Antwort mit fremder Generation
    /// ist überholt und wird verworfen (Vorbild `GeneralHistory.Generation`).
    Generation: int
    /// Läuft ein Laden oder stilles Neuladen? Seine Antwort kann älter sein als eine Schreib-Antwort, die vor
    /// ihr kommt; dann lädt die Prüfung noch einmal (`applyCheckAnswered`).
    LoadPending: bool
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

/// Wo der Fokus steht, wenn eine Pfeiltaste fällt. `TextField` sperrt alle Richtungen,
/// `SearchField` Links und Rechts nur ohne Auswahl.
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
/// Verfügbar kommen aus `Model.Month`, also live aus YNAB. Aus der Datenbank kommen die Tab-Daten
/// (Buchungen, Historie), je für Kategorie und gezeigten Monat.
type SelectedCategory = {
    CategoryId: System.Guid
    Entries: RemoteData<CategoryMonthEntry list>
    /// Der Tab Historie (budgeting-dyn8g): Bewegungen und Ausgaben der letzten 6 Monate.
    History: RemoteData<CategoryHistory>
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
    /// "Erneut senden" an der Markierung "noch nicht in YNAB" läuft für diese Gruppen-Id, ohne Hinweis in
    /// `OpenRetries` (budgeting-dgrp7).
    WriteBackRetry: string option
    /// Target pausieren oder Pause aufheben.
    PauseCall: PauseCall
    /// Das Formular im Tab Target, nur im laufenden Monat und mit Target (budgeting-k8wq3).
    TargetForm: TargetForm option
    /// Target ändern oder entfernen.
    TargetCall: TargetCall
    /// Die gewählte Kategorie. Filter und Suche wirken nur auf die Liste, sie bleibt gewählt.
    Selected: SelectedCategory option
    /// Der Tab des Inspektors. Bleibt bei Kategorie- und Monatswechsel stehen.
    Tab: InspectorTab
    /// Die allgemeine Historie (budgeting-h7q4v).
    GeneralHistory: GeneralHistory
    /// Die Seite Targets prüfen (budgeting-q4v8n).
    TargetCheck: TargetCheckPage
    /// Zählt die Stände der Buchungen zu Kategorie und Monat: eine späte Antwort zu einem älteren
    /// Stand ersetzt nichts. Jeder Tab hat seine eigene, damit die Historie nach einer Bewegung neu
    /// kommt, ohne laufende Buchungen zu verwerfen (budgeting-x4k7p).
    EntriesGeneration: int
    /// Zählt die Stände der Historie, wie `EntriesGeneration`.
    HistoryGeneration: int
    /// Zählt die Abfragen des Monats: eine späte Antwort ersetzt keinen neueren Stand, auch nicht
    /// bei gleicher Monatswahl (zwei Reloads nach schnellen Verschiebungen, Wechsel A, B, A).
    MonthGeneration: int
    /// Der Kopfknopf Monatserster (budgeting-m3x7f).
    MonthStartToggle: MonthStartToggle
    /// Monate, in denen "Ausblenden" auf dieser Seite gedrückt wurde. Dazu kommen die aus dem Rahmen
    /// (`BudgetFrame.MonthStartHidden`), gespeichert auf dem Server.
    MonthStartHidden: Set<string>
    /// Die Rohdaten von Targets prüfen für das Feld Targets prüfen der Leiste, im Hintergrund je Betreten der Seite
    /// geladen. Ein Fehler macht nur das Feld leer.
    MonthStartCheck: RemoteData<TargetCheckData>
    /// Zählt die Abfragen von `MonthStartCheck`: eine späte Antwort ersetzt keinen neueren Stand.
    MonthStartCheckGeneration: int
    /// Zählt das Öffnen der Vorschau von Targets anwenden (budgeting-gvajj): nur die Antwort der jüngsten Öffnung
    /// füllt den Dialog.
    TargetsPreviewGeneration: int
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
    /// Feld Targets anwenden der Leiste, oder "Wiederholen" nach gescheiterter Vorschau: Vorschau öffnen.
    | OpenTargets
    /// Die Checkbox einer Zeile der Vorschau: ab- oder wieder anwählen (budgeting-m3x7f).
    | ToggleTargetLine of categoryId: System.Guid
    /// Der Kopfknopf Monatserster: die Leiste öffnen oder schließen (budgeting-m3x7f).
    | ToggleMonthStart
    /// "Ausblenden" an der Leiste: für den gezeigten Monat ausblenden, auf dem Server gespeichert.
    | HideMonthStart
    /// Antwort auf das Speichern von "Ausblenden".
    | MonthStartHideAnswered of Result<unit, string>
    /// Feld Überzogene decken: Filter Überzogen, am PC (`panel`) die erste überzogene Zeile mit Tab Geld.
    | CoverOverdrawn of panel: bool
    /// Antwort auf das Laden der Rohdaten für das Feld Targets prüfen, mit der Generation beim Start.
    | MonthStartCheckLoaded of generation: int * Result<TargetCheckData, string>
    /// Antwort der Vorschau, mit dem Monat, zu dem sie gehört, und der Generation der Öffnung.
    | PreviewLoaded of month: string * generation: int * Result<TargetPreview, string>
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
    /// "Erneut senden" am Toast, am Hinweis auf der Seite oder an der Markierung "noch nicht in YNAB" einer
    /// Kategorie (budgeting-dgrp7): die Gruppen-Id der fehlgeschlagenen oder offenen Rückschreibung.
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
    /// Der Knopf Historie im Seitenkopf (budgeting-h7q4v).
    | OpenGeneralHistory
    /// Die Seite `#/budget/history` wird betreten: die Bewegungen holen.
    | GeneralHistoryEntered
    /// "Wiederholen" nach einem Fehler: nur neu laden, Filter und Aufklappen bleiben (budgeting-c6vn4, A2).
    | RetryGeneralHistory
    /// Die Antwort, mit der Generation beim Start des Ladens.
    | GeneralHistoryLoaded of generation: int * Result<HistoryMovement list, string>
    | SelectRunFilter of RunFilter
    /// Tipp auf einen Durchlauf Targets anwenden: auf- oder zuklappen.
    | ToggleRun of System.Guid
    /// Der Knopf Zurück der Historie.
    | LeaveGeneralHistory
    /// Die Historie wurde auf eine andere Seite als die Budget-Seite verlassen: keine Rückkehr.
    | GeneralHistoryAbandoned
    /// Die Buchungen (neu) holen: "Wiederholen".
    | LoadCategoryEntries
    | CategoryEntriesLoaded of generation: int * Result<CategoryMonthEntry list, string>
    /// Die Historie (neu) holen: "Wiederholen".
    | LoadCategoryHistory
    | CategoryHistoryLoaded of generation: int * Result<CategoryHistory, string>
    /// Antwort des stillen Nachladens der Historie nach einer Bewegung: die geladene Historie blieb
    /// stehen, ein Fehler wird zum Warn-Toast.
    | CategoryHistoryReloaded of generation: int * Result<CategoryHistory, string>
    /// Tipp auf eine Buchung im Tab Buchungen: ins Kassenbuch springen.
    | OpenEntryInLedger of System.Guid
    /// Der Knopf "Target pausieren" oder "Pause aufheben".
    | SetTargetPause of PauseIntent
    /// Antwort auf Pausieren oder Aufheben, mit Kategorie und Monat des Aufrufs. `Error` ist die echte
    /// Ablehnung des Servers.
    | TargetPauseAnswered of categoryId: System.Guid * month: string * Result<PauseSource option, string>
    /// Der Aufruf selbst brach ab: ob die Pause gespeichert ist, ist unbekannt (Vorbild `MoveCallLost`).
    | TargetPauseCallLost of categoryId: System.Guid * month: string * message: string
    /// Eingabe im Betragsfeld des Tabs Target.
    | TargetAmountChanged of string
    /// Eingabe im Datumsfeld des Tabs Target.
    | TargetDateChanged of DateField.DateEntry
    /// Rhythmus im `Segmented` gewählt (budgeting-xp46a).
    | TargetRhythmChanged of TargetFrequency
    /// Wochentag eines wöchentlichen Targets gewählt, 0 Sonntag bis 6 Samstag (budgeting-eh8xe).
    | TargetWeekdayChanged of int
    /// Die Warnung vor dem Wechsel der Art bestätigt: jetzt senden.
    | ConfirmTargetChange
    /// Die Warnung abgebrochen: nichts gesendet, die Eingabe bleibt.
    | CancelTargetChange
    /// Der Knopf "Target speichern".
    | SaveTarget
    /// Der Knopf "Target entfernen": die Bestätigung öffnen.
    | AskRemoveTarget
    /// Entfernen bestätigt.
    | ConfirmRemoveTarget
    /// Entfernen abgebrochen.
    | CancelRemoveTarget
    /// Antwort auf Ändern oder Entfernen, mit Kategorie und Monat des Aufrufs.
    | TargetChangeAnswered of categoryId: System.Guid * month: string * Result<TargetChangeResult, string>
    /// Der Aufruf von Target ändern brach auch im zweiten Versuch ab: ob das Target gespeichert ist, ist
    /// unbekannt (budgeting-eh8xe, Vorbild `TargetPauseCallLost`).
    | TargetChangeCallLost of categoryId: System.Guid * month: string * message: string
    /// Das Feld Targets prüfen der Leiste (budgeting-m3x7f, vorher ein Knopf im Seitenkopf).
    | OpenTargetCheck
    /// Die Seite `#/budget/target-check` wird betreten: die Rohdaten holen.
    | TargetCheckEntered
    /// "Wiederholen" nach einem Fehler: nur neu laden, das Aufklappen bleibt.
    | RetryTargetCheck
    /// Die Antwort, mit der Generation beim Start des Ladens.
    | TargetCheckLoaded of generation: int * Result<TargetCheckData, string>
    /// Antwort des stillen Neuladens nach einem abgebrochenen Aufruf: die Liste blieb stehen.
    | TargetCheckReloaded of generation: int * Result<TargetCheckData, string>
    /// Tipp auf den Kopf einer Gruppe: auf- oder zuklappen (budgeting-c4ve4).
    | ToggleCheckGroup of CheckGroup
    /// Der Knopf Zurück der Seite Targets prüfen.
    | LeaveTargetCheck
    /// Die Seite Targets prüfen wurde auf eine andere Seite als die Budget-Seite verlassen: keine Rückkehr.
    | TargetCheckAbandoned
    /// "<Betrag> übernehmen" an einer Zeile.
    | TakeSuggestion of categoryId: System.Guid
    /// "Anpassen …" an einer Zeile: das Panel öffnen oder seinen Inhalt ersetzen.
    | AdjustTarget of categoryId: System.Guid
    /// "Abbrechen" im Panel oder das Sheet schließen: zu, ohne Senden.
    | CloseCheckPanel
    /// Eingaben und Knöpfe im Panel, wie im Tab Target.
    | CheckAmountChanged of string
    | CheckDateChanged of DateField.DateEntry
    | CheckRhythmChanged of TargetFrequency
    | CheckWeekdayChanged of int
    | SaveCheckTarget
    | ConfirmCheckTargetChange
    | CancelCheckTargetChange
    | AskRemoveCheckTarget
    | ConfirmRemoveCheckTarget
    | CancelRemoveCheckTarget

/// Was die Seite an die App meldet (wie im Kassenbuch).
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | ShowToastWithAction of string * ToastType * ToastAction
    /// Sprung ins Kassenbuch auf diese Buchung.
    | JumpToEntry of System.Guid
    /// Zur allgemeinen Historie `#/budget/history`.
    | ShowGeneralHistory
    /// Zur Seite Targets prüfen `#/budget/target-check` (budgeting-q4v8n).
    | ShowTargetCheck
    /// Zur Budget-Seite als neuer Eintrag im Browser-Verlauf: die Historie wurde per Link geöffnet.
    | ShowBudgetPage
    /// Ein Schritt zurück im Browser-Verlauf, wie der Zurück-Knopf des Browsers: die Historie kam über den
    /// Knopf Historie, die Budget-Seite liegt direkt davor (budgeting-c6vn4, A9; Vorbild die Rückkehr aus
    /// dem Kassenbuch, `BackToBudget`).
    | StepBackToBudgetPage

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
      WriteBackRetry = None
      PauseCall = PauseIdle
      TargetForm = None
      TargetCall = TargetIdle
      Selected = None
      Tab = MoneyTab
      GeneralHistory = { Runs = NotAsked; Filter = AllRuns; Expanded = Set.empty; ReturnPending = false; Generation = 0 }
      TargetCheck = { Data = NotAsked; OpenGroups = Set.empty; Panel = None; ReturnPending = false; Generation = 0; LoadPending = false }
      EntriesGeneration = 0
      HistoryGeneration = 0
      MonthGeneration = 0
      MonthStartToggle = MonthStartByRule
      MonthStartHidden = Set.empty
      MonthStartCheck = NotAsked
      MonthStartCheckGeneration = 0
      TargetsPreviewGeneration = 0 }
