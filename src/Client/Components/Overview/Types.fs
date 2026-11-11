/// Die Übersicht (design-system-k6kfw, ADR 0087 Punkt 9): Einstieg der App mit Kopf, „Zu tun“ (ledger-s1weh), Monat,
/// Konten (Handy) und den neuesten Buchungen (PC). „Angepinnt“ bringt `budgeting-w9yt4`.
module Components.Overview.Types

open System
open Shared.Domain
open Shared.Budgeting
open Types

/// Wie viele Buchungen unter „Neueste Buchungen“ stehen (Prototyp `homeDesk`).
let recentCount = 6

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
}

let init : Model =
    { Month = NotAsked
      Recent = NotAsked
      NeedsReviewCount = 0
      AskLenaCount = 0
      NotInYnab = NotAsked
      ImportStand = None
      LenaFlagColor = None
      Accounts = NotAsked
      Balances = NotAsked
      Today = DateTime.MinValue }

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
    | MonthLoaded of Result<BudgetMonth, string>
    /// Die neuesten Buchungen samt den Zählern „zu prüfen“ und „Lena“ über alle Konten.
    | RecentLoaded of Result<LedgerEntryPage, string>
    | NotInYnabLoaded of Result<EntryNotInYnab list, string>
    /// Die letzten Sync-Sitzungen, mit dem Zeitpunkt der Antwort (UTC) für „Import vor …“.
    | SyncHistoryLoaded of now: DateTime * Result<SyncSession list, string>
    | LenaFlagLoaded of Result<LenaFlagSettings, string>
    | AccountsLoaded of Result<LedgerAccountsView, string>
    | BalancesLoaded of Result<LedgerAccountBalanceView list, string>

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
      Payee = None
      Memo = None
      AmountMin = None
      AmountMax = None
      Sort = LedgerSort.defaultSort }

// ============================================
// Zu tun (ledger-s1weh, Prototyp Version 17 `todos`, `todoList`)
// ============================================

/// Wohin ein Eintrag in „Zu tun“ führt. Die Reihenfolge der Fälle ist die Reihenfolge der Liste.
type TodoTarget =
    /// 1: das Kassenbuch über alle Konten mit Filter „Zu prüfen“ (bis `ledger-v6s7c` das Durchgehen bringt).
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
                Title = (if inputs.NeedsReview = 1 then "1 Buchung zu prüfen" else $"{inputs.NeedsReview} Buchungen zu prüfen")
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
            let payee = e.Payee |> Option.filter (fun p -> p.Trim() <> "") |> Option.defaultValue "Ohne Payee"
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
    | TodoReview -> "Prüfen"
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

/// Der `aria-label` am Zähler: Gelb braucht ein Wort (Styleguide §2).
let reviewCountLabel (count: int) : string = $"{count} zu prüfen"

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
