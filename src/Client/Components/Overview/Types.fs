/// Die Übersicht (design-system-k6kfw, ADR 0087 Punkt 9): Einstieg der App mit Kopf, „Zu tun“ (ledger-s1weh), Monat,
/// Konten (Handy) und den neuesten Buchungen (PC), dazu „Angepinnt“ (budgeting-w9yt4).
module Components.Overview.Types

open System
open Shared.Domain
open Shared.Budgeting
open Types

/// Wie viele Buchungen unter „Neueste Buchungen“ stehen (Prototyp `homeDesk`).
let recentCount = 6

/// Das Sheet „Angepinnt“ (budgeting-w9yt4): Suchbegriff und die markierte Zeile der Liste (Pfeile am PC).
type PinSheet = {
    Query: string
    /// Stelle in der gefilterten Liste aller aktiven Kategorien (`pinSheetRows`).
    Marked: int
}

/// Das Leuchten neu angepinnter Kategorien im Block „Angepinnt“ (budgeting-p3sk0, Skizze `.flash-row`). `Count` zählt
/// jedes Leuchten: das Ende eines älteren nimmt ein neueres nicht ab, und ein neues baut die Zeilen neu auf.
type PinFlash = { Ids: Set<Guid>; Count: int }

type Model = {
    /// Der laufende Monat für den Block „Monat“.
    Month: RemoteData<BudgetMonth>
    /// Die jüngsten Buchungen über alle Konten, neueste zuerst.
    Recent: RemoteData<LedgerEntryView list>
    /// Die zu prüfenden Buchungen über alle Konten, aus derselben Antwort wie `Recent` (ledger-s1weh).
    NeedsReviewCount: int
    /// Die Buchungen mit dem Lena-Flag über alle Konten, aus derselben Antwort wie `Recent` (ADR 0056).
    AskLenaCount: int
    /// Die Buchungen, die nicht in YNAB sind (ledger-s1weh).
    NotInYnab: RemoteData<EntryNotInYnab list>
    /// Die Nummer der jüngsten Anfrage der neuesten Buchungen samt Zählern. Jede Auslösestelle zählt hoch, eine
    /// Antwort mit älterer Nummer wird verworfen: sonst setzte eine langsame alte Antwort den Stand zurück.
    RecentRequest: int
    /// Dasselbe für die Buchungen nicht in YNAB.
    NotInYnabRequest: int
    /// Dasselbe für den Monat: Betreten und „Erneut laden“ zählen hoch, das Lesen und sein Nachziehen tragen die
    /// Nummer. Sonst überschriebe die Antwort eines älteren Nachziehens einen neuer gelesenen Monat.
    MonthRequest: int
    /// Wie lange der letzte abgeschlossene Import her ist, etwa „vor 2 Std.“. `None` ohne bekannten Import.
    ImportStand: string option
    /// Die Lena-Farbe aus den Einstellungen (ADR 0056) für die Marke in „Zu tun“.
    LenaFlagColor: string option
    /// Die Konten der Seitenleiste und des Abschnitts „Konten“. Sie laden beim Start der App, weil die
    /// Seitenleiste am PC auf jeder Seite steht.
    Accounts: RemoteData<LedgerAccountsView>
    /// Die Kontostände dazu. Lädt das Kassenbuch neue Stände, zieht die Wurzel sie hier nach.
    Balances: RemoteData<LedgerAccountBalanceView list>
    /// Der Kalendertag (Europe/Berlin) beim letzten Betreten. Monat und neueste Buchungen rechnen damit, die View
    /// liest keine Uhr. Vor dem ersten Betreten zeigt die Übersicht nichts, das ihn braucht.
    Today: DateTime
    /// Die angepinnten Kategorien in BBs gespeicherter Folge (budgeting-w9yt4), auch deaktivierte und gelöschte: die
    /// Anzeige blendet sie aus, die Id behält ihren Platz. Das Kategorie-Detail im Budget liest sie mit.
    Pins: RemoteData<Guid list>
    /// Die Nummer des jüngsten Lesens der Pins. Jedes Lesen und jede Änderung zählt hoch: eine Antwort mit älterer
    /// Nummer ist überholt und überschreibt keinen Pin, den Roman inzwischen umgeschaltet hat.
    PinsRead: int
    /// Der zuletzt bestätigte Stand: gelesen oder gespeichert. Scheitert das Speichern, springt die Anzeige hierhin.
    PinsConfirmed: Guid list
    /// Die Liste, deren Speichern gerade läuft. Es läuft höchstens eines: eine Änderung währenddessen wartet und geht
    /// nach der Antwort als ganze Liste hinterher, so kommen die Listen in ihrer Folge beim Server an.
    PinsSaving: Guid list option
    /// Das Sheet „Angepinnt“, `None` solange es zu ist.
    PinSheet: PinSheet option
    /// BBs Reihenfolge der Kategorien (ADR 0088) für die Liste im Sheet, geholt beim ersten Öffnen. Ohne sie gilt
    /// die Folge des Monats.
    CategoryOrder: Guid list option
    /// Die Pins, wie die Übersicht sie zuletzt gezeigt hat (beim Betreten oder beim Schließen des Sheets). `None`, bis
    /// sie einmal geladen sind: ohne Vergleich leuchtet nichts.
    PinsSeen: Guid list option
    /// Welche Zeilen im Block „Angepinnt“ gerade leuchten.
    PinFlash: PinFlash
    /// Der Satz, wenn das Speichern der Pins scheiterte und die Anzeige zurücksprang (budgeting-p3sk0). Er steht rot im
    /// Sheet „Angepinnt“ oder im Block, bis zur nächsten Aktion an den Pins.
    PinError: string option
}

let init : Model =
    { Month = NotAsked
      Recent = NotAsked
      NeedsReviewCount = 0
      AskLenaCount = 0
      NotInYnab = NotAsked
      RecentRequest = 0
      NotInYnabRequest = 0
      MonthRequest = 0
      ImportStand = None
      LenaFlagColor = None
      Accounts = NotAsked
      Balances = NotAsked
      Today = DateTime.MinValue
      Pins = NotAsked
      PinsRead = 0
      PinsConfirmed = []
      PinsSaving = None
      PinSheet = None
      CategoryOrder = None
      PinsSeen = None
      PinFlash = { Ids = Set.empty; Count = 0 }
      PinError = None }

/// Jeder Auslöser, der den Remoting-Proxy anfasst, ist geteilt: die reine Antwort setzt nur den Zustand.
type Msg =
    /// Die Übersicht wird betreten: Monat, neueste Buchungen, Konten und Stände laden.
    | Entered of today: DateTime
    /// „Erneut laden“ nach einem Ladefehler: wie das Betreten, mit dem Tag von dort.
    | ReloadRequested
    /// Die Seitenleiste braucht Konten und Stände: beim Start der App, nach einem Import, „Erneut laden“.
    | SidebarNeedsAccounts
    /// Die Zahl an der Übersicht in Rail und Seitenleiste braucht die zu prüfenden Buchungen: beim Start der App auf
    /// einer anderen Seite und nach einem Import (ledger-s1weh).
    | CountsNeeded
    /// Der gelesene Monat, mit der Nummer der Anfrage.
    | MonthLoaded of request: int * Result<BudgetMonth, string>
    /// Antwort des stillen Nachziehens des Monats mit YNAB (budgeting-ffjk9), mit der Nummer der Anfrage, deren
    /// Lesen es gestartet hat, und dem Monat beim Start.
    | MonthSynced of request: int * month: string * Result<BudgetMonthSync, string>
    /// Die neuesten Buchungen samt den Zählern „zu prüfen“ und „Lena“ über alle Konten, mit der Nummer der Anfrage.
    | RecentLoaded of request: int * Result<LedgerEntryPage, string>
    /// Die Buchungen nicht in YNAB, mit der Nummer der Anfrage.
    | NotInYnabLoaded of request: int * Result<EntryNotInYnab list, string>
    /// Die letzten Sync-Sitzungen, mit dem Zeitpunkt der Antwort (UTC) für „Import vor …“.
    | SyncHistoryLoaded of now: DateTime * Result<SyncSession list, string>
    | LenaFlagLoaded of Result<LenaFlagSettings, string>
    | AccountsLoaded of Result<LedgerAccountsView, string>
    | BalancesLoaded of Result<LedgerAccountBalanceView list, string>
    /// Die angepinnten Kategorien werden gebraucht: beim Start der App auf einer anderen Seite (Pin-Knopf im
    /// Kategorie-Detail).
    | PinsNeeded
    /// Die gelesenen Pins, mit der Nummer des Lesens.
    | PinsLoaded of read: int * Result<Guid list, string>
    /// Anpinnen oder lösen, aus dem Sheet oder dem Kategorie-Detail. Neu angepinnte kommen ans Ende.
    | PinToggled of categoryId: Guid
    /// Pfeil hoch im Sheet: eine Stelle nach oben unter den gezeigten Pins.
    | PinMovedUp of categoryId: Guid
    /// Die Antwort auf das laufende Speichern (`PinsSaving`).
    | PinsSaved of Result<unit, string>
    | PinSheetOpened
    | PinSheetClosed
    | PinSearchChanged of string
    /// ↑ (-1) oder ↓ (+1) aus der Suche: die Markierung wandert.
    | PinMarkMoved of delta: int
    /// Enter aus der Suche: die markierte Zeile umschalten.
    | PinMarkedToggled
    | CategoryOrderLoaded of Result<BudgetFrame, string>
    /// Das Leuchten neu angepinnter Zeilen endet, mit seiner Zählung.
    | PinFlashEnded of count: int

/// Was Seitenleiste und Abschnitt „Konten“ zeigen. Die Views rufen es nur auf.
type AccountsListState =
    | AccountsListLoading
    /// Ladefehler: eine kompakte Fehlerzeile mit „Erneut laden“.
    | AccountsListFailed of message: string
    /// Noch kein Konto: ein kurzer leerer Zustand (Styleguide §7.12).
    | AccountsListEmpty
    | AccountsListShown of LedgerAccountView list

let accountsListState (accounts: RemoteData<LedgerAccountsView>) : AccountsListState =
    match accounts with
    | NotAsked | Loading -> AccountsListLoading
    | Failure message -> AccountsListFailed message
    | Success view when List.isEmpty view.Accounts -> AccountsListEmpty
    | Success view -> AccountsListShown view.Accounts

/// Kopf und Satz des leeren Zustands der Konten.
let noAccountsTitle = "Keine Konten"
let noAccountsText = "Konten erscheinen hier nach dem ersten Spiegeln."

/// Der Monat ("YYYY-MM-01"), zu dem `today` gehört.
/// Den Monatsersten als "JJJJ-MM-01". Ohne Formatangabe wie `:D2`: die übersetzt Fable nicht, im Client käme
/// "%P(D4)-%P(D2)-01" an (Bug nach design-system-k6kfw, Test `[fable]` in scripts/navigation.test.mjs).
let monthKey (today: DateTime) : string =
    (string today.Year).PadLeft(4, '0') + "-" + (string today.Month).PadLeft(2, '0') + "-01"

let private monthNames =
    [| "Januar"; "Februar"; "März"; "April"; "Mai"; "Juni"; "Juli"; "August"; "September"; "Oktober"; "November"; "Dezember" |]

/// Was der Block „Monat“ zeigt. Rein, „heute“ kommt als Parameter (Kalendertag in Europe/Berlin).
type MonthSummary = {
    /// Der Monatsname, etwa „November“.
    Name: string
    /// Volle Tage nach heute bis zum Monatsende: am Monatsletzten 0.
    DaysLeft: int
    /// Ausgegeben: die Summe der Abflüsse je Kategorie (Aktivität unter null), als positiver Betrag.
    Spent: decimal
    /// Zugewiesen: die Summe über alle Kategorien.
    Assigned: decimal
    /// Füllung des Balkens, 0 bis 1.
    SpentShare: float
    /// Lage der Marke „▲ heute“ auf dem Balken, 0 bis 1: das Ende des heutigen Tags im Monat.
    TodayShare: float
    ToBeBudgeted: Money
    /// Kategorien mit Verfügbar unter null, dieselbe Zahl wie der Filter „Überzogen“ der Budget-Seite.
    OverdrawnCount: int
}

let monthSummary (month: BudgetMonth) (today: DateTime) : MonthSummary =
    let categories = month.Groups |> List.collect (fun g -> g.Categories)
    let spent = categories |> List.sumBy (fun c -> -(min 0m c.Activity.Amount))
    let assigned = categories |> List.sumBy (fun c -> c.Assigned.Amount)
    let monthStart =
        match DateTime.TryParse month.Month with
        | true, d -> d
        | _ -> DateTime(today.Year, today.Month, 1)
    let days = DateTime.DaysInMonth(monthStart.Year, monthStart.Month)
    let day =
        if today.Year = monthStart.Year && today.Month = monthStart.Month then today.Day
        elif today < monthStart then 0
        else days
    let spentShare =
        if assigned > 0m then min 1.0 (float (spent / assigned))
        elif spent > 0m then 1.0
        else 0.0
    { Name = monthNames.[monthStart.Month - 1]
      DaysLeft = days - day
      Spent = spent
      Assigned = assigned
      SpentShare = spentShare
      TodayShare = float day / float days
      ToBeBudgeted = month.ToBeBudgeted
      OverdrawnCount = Components.Budget.Month.filterCount Components.Budget.Types.OverdrawnOnly month.Groups }

/// „noch N Tage“ rechts im Kopf des Monats.
let daysLeftText (daysLeft: int) : string =
    match daysLeft with
    | 0 -> "letzter Tag"
    | 1 -> "noch 1 Tag"
    | n -> $"noch {n} Tage"

/// Die Abfrage der neuesten Buchungen: alle Konten, Seite 0, ohne gelöschte, nach Datum absteigend.
let recentQuery : LedgerEntryPageQuery =
    { AccountId = None
      Page = 0
      PageSize = recentCount
      LocallyRemoved = 0
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
      Sort = LedgerSort.defaultSort }

// ============================================
// Zu tun (ledger-s1weh, Prototyp Version 17 `todos`, `todoList`)
// ============================================

/// Wohin ein Eintrag in „Zu tun“ führt. Die Reihenfolge der Fälle ist die Reihenfolge der Liste.
type TodoTarget =
    /// 1: das Durchgehen Karte für Karte (ledger-v6s7c).
    | TodoReview
    /// 2: das Detail dieser Kategorie im Budget, Bereich Geld, Holen, Betrag vorbelegt mit dem Minus.
    | TodoCover of categoryId: Guid
    /// 3: der Dialog Targets anwenden im Budget.
    | TodoApplyTargets
    /// 4: das Kassenbuch über alle Konten mit Filter Lena.
    | TodoAskLena
    /// 5: diese Buchung im Kassenbuch, dort steht „Erneut senden“.
    | TodoNotInYnab of entryId: Guid
    /// 6: das Budget.
    | TodoDistribute

/// Die Marke vor einem Eintrag: „braucht dich“ (Gelb), Fehler (Rot) oder das Lena-Flag in der Flag-Farbe.
type TodoMark =
    | MarkAttention
    | MarkError
    | MarkLenaFlag

type Todo = {
    Target: TodoTarget
    Title: string
    Detail: string
}

/// Woraus „Zu tun“ rechnet. Rein, ohne Uhr: der Stand des Imports kommt fertig als Text.
type TodoInputs = {
    /// Der laufende Budgetmonat; `None`, solange er fehlt (dann entfallen die Einträge 2, 3 und 6).
    Month: BudgetMonth option
    NeedsReview: int
    AskLena: int
    NotInYnab: EntryNotInYnab list
    /// „vor 2 Std.“; `None` ohne bekannten Import.
    ImportStand: string option
}

let private moneyText (m: Money) = Components.Budget.Month.moneyText m

/// Kategorien, denen im Monat Geld zum Target fehlt: Fehlbetrag über null, nicht pausiert, nicht deaktiviert.
let private targetGaps (month: BudgetMonth) : Money list =
    month.Groups
    |> List.collect (fun g -> g.Categories)
    |> List.filter (fun c -> not (isDeactivated c))
    |> List.choose (fun c -> Components.Budget.Month.targetUnderfunded c)
    |> List.filter (fun m -> m.Amount > 0m)

/// „Zu tun“ in der festen Reihenfolge, jeder Eintrag nur, wenn seine Bedingung gilt (Task ledger-s1weh).
let todos (inputs: TodoInputs) : Todo list =
    let review =
        if inputs.NeedsReview > 0 then
            [ { Target = TodoReview
                Title =
                  (if inputs.NeedsReview = 1 then "1 neue Buchung durchgehen"
                   else $"{inputs.NeedsReview} neue Buchungen durchgehen")
                Detail =
                  match inputs.ImportStand with
                  | Some stand -> $"Import {stand}"
                  | None -> "Im Kassenbuch" } ]
        else []
    let categories =
        inputs.Month |> Option.map (fun m -> m.Groups |> List.collect (fun g -> g.Categories)) |> Option.defaultValue []
    let overdrawn =
        categories
        |> List.filter (fun c -> c.Available.Amount < 0m)
        |> List.map (fun c ->
            { Target = TodoCover c.Id
              Title = $"{c.Name} ist im Minus"
              Detail = $"{moneyText c.Available} · Geld aus einer anderen Kategorie holen" })
    let gaps = inputs.Month |> Option.map targetGaps |> Option.defaultValue []
    let targets =
        match gaps with
        | [] -> []
        | first :: _ ->
            let sum = { first with Amount = gaps |> List.sumBy (fun m -> m.Amount) }
            let count = if gaps.Length = 1 then "1 Kategorie" else $"{gaps.Length} Kategorien"
            [ { Target = TodoApplyTargets; Title = "Targets anwenden"; Detail = $"{moneyText sum} für {count}" } ]
    let lena =
        if inputs.AskLena > 0 then
            [ { Target = TodoAskLena
                Title = (if inputs.AskLena = 1 then "1 Rückfrage an Lena offen" else $"{inputs.AskLena} Rückfragen an Lena offen")
                Detail = "Lena sieht sie über den Filter" } ]
        else []
    let notInYnab =
        inputs.NotInYnab
        |> List.map (fun e ->
            let payee = e.Payee |> Option.filter (fun p -> p.Trim() <> "") |> Option.defaultValue "ohne Payee"
            { Target = TodoNotInYnab e.EntryId
              Title = "Buchung nicht in YNAB"
              Detail = $"{payee} · {moneyText e.Amount} · erneut senden" })
    let distribute =
        match inputs.Month with
        | Some month when month.ToBeBudgeted.Amount > 0m && List.isEmpty targets ->
            [ { Target = TodoDistribute
                Title = $"{moneyText month.ToBeBudgeted} verteilen"
                Detail = "Noch keiner Kategorie zugewiesen" } ]
        | _ -> []
    review @ overdrawn @ targets @ lena @ notInYnab @ distribute

/// Ein Schlüssel je Eintrag für React.
let todoKey (target: TodoTarget) : string =
    match target with
    | TodoReview -> "review"
    | TodoCover id -> "cover-" + string id
    | TodoApplyTargets -> "targets"
    | TodoAskLena -> "lena"
    | TodoNotInYnab id -> "not-in-ynab-" + string id
    | TodoDistribute -> "distribute"

/// Die Marke je Ziel. Eintrag 5 trägt Gelb: ein gescheiterter Push bleibt Gelb (Nachtrag ADR 0087).
let todoMark (target: TodoTarget) : TodoMark =
    match target with
    | TodoCover _ -> MarkError
    | TodoAskLena -> MarkLenaFlag
    | TodoReview
    | TodoApplyTargets
    | TodoNotInYnab _
    | TodoDistribute -> MarkAttention

/// Die Beschriftung des Hauptknopfs, wenn der Eintrag oben steht.
let todoActionLabel (target: TodoTarget) : string =
    match target with
    | TodoReview -> "Jetzt durchgehen"
    | TodoCover _ -> "Geld holen"
    | TodoApplyTargets -> "Anwenden"
    | TodoAskLena
    | TodoNotInYnab _
    | TodoDistribute -> "Öffnen"

/// Was der Block „Zu tun“ zeigt.
type TodoState =
    | TodosLoading
    /// Eine Quelle scheiterte und es bliebe nichts zu zeigen: kein falsches „Alles erledigt“.
    | TodosFailed of message: string
    | TodosShown of Todo list

let private answered (rd: RemoteData<'a>) =
    match rd with
    | NotAsked | Loading -> false
    | Success _ | Failure _ -> true

/// „Zu tun“ aus dem Modell. Es steht erst, wenn Monat, neueste Buchungen und „nicht in YNAB“ geantwortet haben,
/// damit die Liste nicht Eintrag für Eintrag wächst. Ohne Zähler gibt es kein „Alles erledigt“.
let todoState (model: Model) : TodoState =
    if not (answered model.Month && answered model.Recent && answered model.NotInYnab) then
        TodosLoading
    else
        match model.Recent, model.NotInYnab with
        | Failure message, _
        | _, Failure message -> TodosFailed message
        | _ ->
            let list =
                todos
                    { Month = (match model.Month with Success m -> Some m | _ -> None)
                      NeedsReview = model.NeedsReviewCount
                      AskLena = model.AskLenaCount
                      NotInYnab = (match model.NotInYnab with Success l -> l | _ -> [])
                      ImportStand = model.ImportStand }
            match list, model.Month with
            | [], Failure message -> TodosFailed message
            | _ -> TodosShown list

/// Die Zahl an der Übersicht in Rail und Seitenleiste: die zu prüfenden Buchungen, 0 solange unbekannt.
let reviewCount (model: Model) : int =
    match model.Recent with
    | Success _ -> model.NeedsReviewCount
    | _ -> 0

/// Wann der letzte Import abgeschlossen wurde: die jüngste abgeschlossene Sync-Sitzung.
let lastImportAt (sessions: SyncSession list) : DateTime option =
    sessions
    |> List.filter (fun s -> s.Status = SyncSessionStatus.Completed)
    |> List.map (fun s -> s.CompletedAt |> Option.defaultValue s.StartedAt)
    |> List.sortDescending
    |> List.tryHead

/// Die Zeile unter dem Titel „Übersicht“ (Prototyp `homeHead`): „Comdirect · Import <Stand>“. Ohne bekannten Import
/// (keine abgeschlossene Sitzung, Ladefehler oder noch nicht geladen) keine Zeile: BB behauptet keinen Stand, den es
/// nicht kennt, wie Eintrag 1 in „Zu tun“ (Abweichung b).
let importLine (stand: string option) : string option =
    stand |> Option.map (fun s -> $"Comdirect · Import {s}")

/// „vor 5 Min.“, „vor 2 Std.“, „vor 3 Tagen“. `now` und `at` in derselben Zeitbasis.
let importStandText (now: DateTime) (at: DateTime) : string =
    let span = now - at
    if span.TotalMinutes < 1.0 then "gerade eben"
    elif span.TotalHours < 1.0 then $"vor {int span.TotalMinutes} Min."
    elif span.TotalDays < 1.0 then $"vor {int span.TotalHours} Std."
    elif int span.TotalDays = 1 then "vor 1 Tag"
    else $"vor {int span.TotalDays} Tagen"

// ============================================
// Angepinnt (budgeting-w9yt4, Prototyp Version 17 `pinBlock`, Sheet `pins`)
// ============================================

/// Anteil, „ausgegeben“ und „von“ einer angepinnten Kategorie. Ausgegeben ist die Aktivität mit umgekehrtem Vorzeichen
/// (eine Erstattung mindert), „von“ ist Verfügbar minus Aktivität, also was im Monat zur Verfügung stand.
type PinSpend = {
    Spent: decimal
    Pool: decimal
    /// Füllung des Balkens, 0 bis 1.
    Share: float
}

/// Rein: ausgegeben durch „von“, begrenzt auf 0 bis 1. Ist „von“ höchstens null, ist der Balken mit Ausgaben voll,
/// ohne leer.
let pinSpend (activity: decimal) (available: decimal) : PinSpend =
    let spent = -activity
    let pool = available - activity
    let share =
        if pool <= 0m then (if spent > 0m then 1.0 else 0.0)
        else max 0.0 (min 1.0 (float (spent / pool)))
    { Spent = spent; Pool = pool; Share = share }

/// Eine Zeile im Block „Angepinnt“.
type PinnedLine = {
    Category: BudgetCategory
    /// Der Fehlbetrag zum Target im Monat, nur über null und ohne Pause.
    Missing: Money option
    Spend: PinSpend
}

/// Die aktiven Kategorien des Monats nach Id: in YNAB versteckte und gelöschte fehlen im Monat schon, in BB
/// deaktivierte fallen hier weg (auch wenn sie wegen Aktivität im Monat stehen).
let private activeCategories (month: BudgetMonth) : Map<Guid, BudgetCategory> =
    month.Groups
    |> List.collect (fun g -> g.Categories)
    |> List.filter (isDeactivated >> not)
    |> List.map (fun c -> c.Id, c)
    |> Map.ofList

/// Die gezeigten Pins: die gespeicherte Folge, nur aktive Kategorien des Monats.
let visiblePins (pins: Guid list) (month: BudgetMonth) : BudgetCategory list =
    let active = activeCategories month
    pins |> List.choose (fun id -> Map.tryFind id active)

/// Die Zeilen des Blocks „Angepinnt“ aus dem laufenden Monat.
let pinnedLines (pins: Guid list) (month: BudgetMonth) : PinnedLine list =
    visiblePins pins month
    |> List.map (fun c ->
        { Category = c
          Missing = Components.Budget.Month.targetUnderfunded c |> Option.filter (fun m -> m.Amount > 0m)
          Spend = pinSpend c.Activity.Amount c.Available.Amount })

/// Was der Block „Angepinnt“ zeigt.
type PinBlockState =
    /// Pins oder Monat laden; `rows` ist die Zahl der Zeilen des Skeletons (die gespeicherten Pins, mindestens 1).
    | PinsLoading of rows: int
    | PinsFailed
    | PinsEmpty
    | PinsShown of PinnedLine list

let pinBlockState (model: Model) : PinBlockState =
    match model.Pins, model.Month with
    | Failure _, _ -> PinsFailed
    // Ohne Pins braucht die Karte den Monat nicht: sie steht gleich, darunter springt nichts.
    | Success [], _ -> PinsEmpty
    | Success _, Failure _ -> PinsFailed
    | Success pins, Success month ->
        match pinnedLines pins month with
        | [] -> PinsEmpty
        | lines -> PinsShown lines
    | Success pins, _ -> PinsLoading (max 1 pins.Length)
    | _ -> PinsLoading 1

/// „Bearbeiten“ steht nur, wenn etwas angepinnt ist.
let pinEditVisible (state: PinBlockState) : bool =
    match state with
    | PinsShown _ -> true
    | _ -> false

/// Zeile 3 einer Pin-Zeile: „<ausgegeben> von <Betrag> ausgegeben“.
let pinSpendText (currency: string) (spend: PinSpend) : string =
    let text amount = Components.Budget.Month.moneyText { Amount = amount; Currency = currency }
    $"{text spend.Spent} von {text spend.Pool} ausgegeben"

/// Die Leer-Karte des Blocks.
let pinEmptyText = "Kategorien anpinnen, die du hier immer sehen willst"
/// Der Ladefehler des Blocks.
let pinLoadFailedText = "Angepinnte Kategorien konnten nicht geladen werden"

/// Anpinnen oder lösen: eine angepinnte fällt heraus, eine neue kommt ans Ende.
let togglePin (categoryId: Guid) (pins: Guid list) : Guid list =
    if List.contains categoryId pins then pins |> List.filter ((<>) categoryId)
    else pins @ [ categoryId ]

/// Eine Stelle nach oben unter den gezeigten Pins (`shown`): tauscht mit dem nächsten gezeigten davor. Ausgeblendete
/// Ids bleiben an ihrem Platz. Der erste gezeigte bleibt, wo er ist.
let movePinUp (categoryId: Guid) (shown: Set<Guid>) (pins: Guid list) : Guid list =
    let arr = List.toArray pins
    match Array.tryFindIndex ((=) categoryId) arr with
    | Some index ->
        let before = [ index - 1 .. -1 .. 0 ] |> List.tryFind (fun i -> Set.contains arr.[i] shown)
        match before with
        | Some other ->
            let copy = Array.copy arr
            copy.[other] <- arr.[index]
            copy.[index] <- arr.[other]
            List.ofArray copy
        | None -> pins
    | None -> pins

/// Die Liste im Sheet: alle aktiven Kategorien nach Gruppe in BBs Folge (ADR 0088), gefiltert nach dem Suchbegriff
/// (Teil des Namens, ohne Groß und Klein). Gruppen ohne Treffer fallen weg.
let pinSheetGroups (order: Guid list option) (query: string) (month: BudgetMonth) : (string * BudgetCategory list) list =
    let needle = query.Trim().ToLowerInvariant()
    let groups =
        match order with
        | Some o -> orderGroups o month.Groups
        | None -> month.Groups
    groups
    |> List.map (fun g ->
        g.Name,
        g.Categories
        |> List.filter (isDeactivated >> not)
        |> List.filter (fun c -> needle = "" || c.Name.ToLowerInvariant().Contains needle))
    |> List.filter (fun (_, cs) -> not cs.IsEmpty)

/// Die Zeilen des Sheets flach, in der Folge der Anzeige: die Markierung zählt in dieser Liste.
let pinSheetRows (order: Guid list option) (query: string) (month: BudgetMonth) : BudgetCategory list =
    pinSheetGroups order query month |> List.collect snd

/// Die markierte Zeile des offenen Sheets: Enter schaltet sie um, das Suchfeld zeigt auf sie (`aria-activedescendant`).
let pinSheetMarked (model: Model) : BudgetCategory option =
    match model.PinSheet, model.Month with
    | Some sheet, Success month -> pinSheetRows model.CategoryOrder sheet.Query month |> List.tryItem sheet.Marked
    | _ -> None

/// Der Abschnitt „Angepinnt (n)“ steht nur ohne Suchbegriff und mit gezeigten Pins.
let pinSheetPinnedSection (query: string) (pins: Guid list) (month: BudgetMonth) : BudgetCategory list =
    if query.Trim() <> "" then [] else visiblePins pins month

/// Der Satz, wenn das Speichern scheitert. Er steht am Ort (budgeting-p3sk0, ADR 0098), nie als Toast.
let pinsNotSavedText (reason: string) : string = $"Nicht gespeichert: {reason}"

/// Neu angepinnt seit dem letzten Anzeigen: was jetzt angepinnt ist und damals nicht. Gelöste, erst angepinnte und
/// wieder gelöste und nur umgeordnete Kategorien leuchten nicht.
let newlyPinned (seen: Guid list) (now: Guid list) : Set<Guid> =
    Set.difference (Set.ofList now) (Set.ofList seen)

/// Wo der Satz „Nicht gespeichert: …“ auf der Übersicht steht.
type PinErrorPlace =
    /// Im Sheet „Angepinnt“ unter der Liste „Angepinnt (n)“.
    | PinErrorInSheet
    /// Im Block „Angepinnt“ der Übersicht.
    | PinErrorInBlock

/// Der Satz zum gescheiterten Speichern und sein Ort: bei offenem Sheet dort, sonst im Block.
let pinErrorShown (model: Model) : (PinErrorPlace * string) option =
    model.PinError |> Option.map (fun text -> (if model.PinSheet.IsSome then PinErrorInSheet else PinErrorInBlock), text)

/// Ist die Kategorie angepinnt? Ohne geladene Pins nie.
let isPinned (model: Model) (categoryId: Guid) : bool =
    match model.Pins with
    | Success pins -> List.contains categoryId pins
    | _ -> false
