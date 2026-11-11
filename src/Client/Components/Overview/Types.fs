/// Die Übersicht (design-system-k6kfw, ADR 0087 Punkt 9): Einstieg der App mit Kopf, Monat, Konten (Handy) und den
/// neuesten Buchungen (PC). „Zu tun“ und „+ Buchung“ bringt `ledger-s1weh`, „Angepinnt“ `budgeting-w9yt4`.
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
    | MonthLoaded of Result<BudgetMonth, string>
    | RecentLoaded of Result<LedgerEntryView list, string>
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
let monthKey (today: DateTime) : string = $"{today.Year:D4}-{today.Month:D2}-01"

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
