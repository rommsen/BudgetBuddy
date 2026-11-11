module OverviewTests

// design-system-k6kfw: die schlichte Übersicht. Die Werte des Monatsblocks rechnet `monthSummary` rein aus dem
// Budgetmonat und dem heutigen Tag; die Übergänge aus Übersicht und Seitenleiste laufen durch das echte Root-`update`.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Shared.Api
open Types
open Components.Overview.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private category (assigned: decimal) (activity: decimal) (available: decimal) : BudgetCategory =
    { Id = Guid.NewGuid(); Name = "K"; Assigned = eur assigned; Activity = eur activity; Available = eur available
      Underfunded = None; Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

let private monthOf (key: string) (categories: BudgetCategory list) : BudgetMonth =
    { Month = key; ToBeBudgeted = eur 120m; Groups = [ { Name = "Alltag"; Categories = categories } ] }

let private november = monthOf "2026-11-01" [ category 1400m -747.90m 652.10m; category 80m -80.10m -0.10m; category 60m 12m 72m ]

[<Tests>]
let monthSummaryTests =
    testList "Block Monat der Übersicht" [
        testCase "Ausgegeben zählt die Abflüsse je Kategorie, Zugewiesen die Summe aller Kategorien" <| fun () ->
            let summary = monthSummary november (DateTime(2026, 11, 7))
            Expect.equal summary.Spent 828.00m "eine Erstattung (+12) senkt Ausgegeben nicht"
            Expect.equal summary.Assigned 1540m "Zugewiesen"
            Expect.floatClose Accuracy.high summary.SpentShare (828.0 / 1540.0) "Füllung"
            Expect.equal summary.Name "November" "Monatsname"

        testCase "Am 7. November bleiben 23 Tage, die Marke steht bei 7 von 30" <| fun () ->
            let summary = monthSummary november (DateTime(2026, 11, 7))
            Expect.equal summary.DaysLeft 23 "Tage übrig"
            Expect.floatClose Accuracy.high summary.TodayShare (7.0 / 30.0) "Marke"

        testCase "Am Monatsersten steht die Marke am Anfang des Balkens" <| fun () ->
            let summary = monthSummary november (DateTime(2026, 11, 1))
            Expect.equal summary.DaysLeft 29 "Tage übrig"
            Expect.floatClose Accuracy.high summary.TodayShare (1.0 / 30.0) "Marke"

        testCase "Am Monatsletzten bleibt kein Tag und die Marke steht am Ende" <| fun () ->
            let summary = monthSummary november (DateTime(2026, 11, 30))
            Expect.equal summary.DaysLeft 0 "Tage übrig"
            Expect.floatClose Accuracy.high summary.TodayShare 1.0 "Marke"
            Expect.equal (daysLeftText summary.DaysLeft) "letzter Tag" "Text"

        testCase "Im Februar zählt der Monat 28 Tage, im Schaltjahr 29" <| fun () ->
            let february = monthSummary (monthOf "2027-02-01" []) (DateTime(2027, 2, 14))
            Expect.equal february.DaysLeft 14 "2027"
            Expect.floatClose Accuracy.high february.TodayShare 0.5 "Mitte"
            let leap = monthSummary (monthOf "2028-02-01" []) (DateTime(2028, 2, 28))
            Expect.equal leap.DaysLeft 1 "2028"
            Expect.equal (daysLeftText leap.DaysLeft) "noch 1 Tag" "Einzahl"

        testCase "Im Minus zählt die Kategorien mit Verfügbar unter null" <| fun () ->
            let summary = monthSummary november (DateTime(2026, 11, 7))
            Expect.equal summary.OverdrawnCount 1 "die Kategorie mit -0,10"
            Expect.equal (monthSummary (monthOf "2026-11-01" [ category 10m 0m 10m ]) (DateTime(2026, 11, 7))).OverdrawnCount 0 "keine"

        testCase "Mehr ausgegeben als zugewiesen füllt den Balken ganz, nicht darüber" <| fun () ->
            let over = monthSummary (monthOf "2026-11-01" [ category 100m -150m -50m ]) (DateTime(2026, 11, 7))
            Expect.equal over.SpentShare 1.0 "gekappt"

        testCase "Vor dem Monat steht die Marke am Anfang, danach am Ende" <| fun () ->
            let before = monthSummary november (DateTime(2026, 10, 31))
            Expect.equal before.TodayShare 0.0 "Vormonat"
            Expect.equal before.DaysLeft 30 "der ganze Monat bleibt"
            let after = monthSummary november (DateTime(2026, 12, 1))
            Expect.equal after.TodayShare 1.0 "Folgemonat"
            Expect.equal after.DaysLeft 0 "nichts bleibt"

        testCase "Ohne Zugewiesen ist der Balken leer, mit Ausgaben voll" <| fun () ->
            Expect.equal (monthSummary (monthOf "2026-11-01" [ category 0m 0m 0m ]) (DateTime(2026, 11, 7))).SpentShare 0.0 "leer"
            Expect.equal (monthSummary (monthOf "2026-11-01" [ category 0m -5m -5m ]) (DateTime(2026, 11, 7))).SpentShare 1.0 "voll"

        testCase "Der Monat zu heute ist der Erste des Kalendermonats" <| fun () ->
            Expect.equal (monthKey (DateTime(2026, 1, 31))) "2026-01-01" "Januar"
    ]

let private rootModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = page
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      SyncFlow = syncFlowModel
      Rules = rulesModel
      Ledger = ledgerModel
      Budget = Components.Budget.Types.init ()
      QuickAdd = None
      QuickAddTemplates = NotAsked
      Overview = Components.Overview.Types.init
      SettingsReturn = Overview }

let private dispatched (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let collected = ResizeArray<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private accountId = Guid.Parse "00000000-0000-0000-0000-00000000a001"

[<Tests>]
let transitionTests =
    testList "Übergänge aus Übersicht und Seitenleiste" [
        testCase "Ein Konto aus der Übersicht setzt und merkt den Filter des Kassenbuchs vor dem Betreten" <| fun () ->
            let model, _ = State.update (State.OpenLedgerAccount (Components.Ledger.Types.OneAccount accountId)) (rootModel Overview)
            Expect.equal model.Ledger.SelectedFilter (Some (Components.Ledger.Types.OneAccount accountId)) "Filter"
            Expect.equal model.Ledger.RememberedFilter (Some (Components.Ledger.Types.OneAccount accountId)) "gemerkt"
            // Das Betreten lädt die Konten; deren Antwort behält die Wahl (resolveInitialFilter).
            let accounts : LedgerAccountsView =
                { Accounts = [ { Id = accountId; Name = "Giro"; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false } ]
                  DefaultAccountId = Some (Guid.NewGuid()) }
            let entered, _ = State.update (State.LedgerMsg (Components.Ledger.Types.AccountsLoaded (Ok accounts))) { model with CurrentPage = Ledger }
            Expect.equal entered.Ledger.SelectedFilter (Some (Components.Ledger.Types.OneAccount accountId)) "nicht das Standardkonto"

        testCase "Im Kassenbuch wechselt ein Konto aus der Seitenleiste den Filter wie die Kontenliste und lädt neu" <| fun () ->
            let model, cmd = State.update (State.OpenLedgerAccount Components.Ledger.Types.AllAccounts) (rootModel Ledger)
            Expect.equal model.Ledger.SelectedFilter (Some Components.Ledger.Types.AllAccounts) "Filter"
            // Das Kassenbuch steht schon offen, kein Betreten lädt nach: ohne `LoadEntries` blieb die alte Liste stehen.
            Expect.contains (dispatched cmd) (State.LedgerMsg Components.Ledger.Types.LoadEntries) "lädt die Buchungen"

        // Review-Fund: „Abrufen“ startete den Sync zugleich mit dem Seitenwechsel; das Lesen der Sitzung beim Betreten
        // zeigte dann den Startknopf, obwohl der Start lief. Jetzt geht ein Auftrag an den Sync-Ablauf
        // (Reihenfolgen in SyncStartOrderTests).
        testCase "Abrufen öffnet den Sync-Ablauf und gibt ihm den Auftrag, statt selbst zu starten" <| fun () ->
            let _, cmd = State.update State.FetchFromBank (rootModel Overview)
            let messages = dispatched cmd
            Expect.contains messages (State.NavigateTo SyncFlow) "zum Sync-Ablauf"
            Expect.contains messages (State.SyncFlowMsg Components.SyncFlow.Types.StartRequested) "Auftrag"
            Expect.isFalse (messages |> List.contains (State.SyncFlowMsg Components.SyncFlow.Types.StartSync)) "kein blinder Start"

        testCase "Im Minus öffnet das Budget mit dem Filter Überzogen" <| fun () ->
            let model, _ = State.update State.OpenBudgetOverdrawn (rootModel Overview)
            let entered, _ = Components.Budget.Inspector.enterPage model.Budget
            Expect.equal entered.Filter Components.Budget.Types.OverdrawnOnly "Filter beim Betreten"
            Expect.equal entered.EntryFilter None "verbraucht"
            let again, _ = Components.Budget.Inspector.enterPage { entered with Filter = Components.Budget.Types.AllCategories }
            Expect.equal again.Filter Components.Budget.Types.AllCategories "das nächste Betreten beginnt von vorn"

        testCase "Lädt das Kassenbuch Konten und Stände, zieht die Seitenleiste mit" <| fun () ->
            let accounts : LedgerAccountsView = { Accounts = []; DefaultAccountId = None }
            let balances : LedgerAccountBalanceView list = [ { AccountId = accountId; Balance = eur 5m } ]
            let model, _ = State.update (State.LedgerMsg (Components.Ledger.Types.AccountsLoaded (Ok accounts))) (rootModel Ledger)
            let model, _ = State.update (State.LedgerMsg (Components.Ledger.Types.BalancesLoaded (Ok balances))) model
            Expect.equal model.Overview.Accounts (Success accounts) "Konten"
            Expect.equal model.Overview.Balances (Success balances) "Stände"

        // Review-Fund: `syncNavAccounts` spiegelte nur `Ok`. Scheiterte das Kassenbuch beim Laden, blieb die
        // Seitenleiste beim Platzhalter stehen; ein geladener Stand bleibt aber stehen (`lateFailure`).
        testCase "Scheitert das Kassenbuch beim Laden, zeigt die Seitenleiste den Fehler statt ewig zu laden" <| fun () ->
            let loading = { rootModel Ledger with Overview = Components.Overview.State.beginEnter (DateTime(2026, 11, 7)) Components.Overview.Types.init }
            let model, _ = State.update (State.LedgerMsg (Components.Ledger.Types.AccountsLoaded (Error "weg"))) loading
            let model, _ = State.update (State.LedgerMsg (Components.Ledger.Types.BalancesLoaded (Error "weg"))) model
            Expect.equal model.Overview.Accounts (Failure "weg") "Konten"
            Expect.equal model.Overview.Balances (Failure "weg") "Stände"
            let accounts : LedgerAccountsView = { Accounts = []; DefaultAccountId = None }
            let loaded = { loading with Overview = { loading.Overview with Accounts = Success accounts } }
            let late, _ = State.update (State.LedgerMsg (Components.Ledger.Types.AccountsLoaded (Error "weg"))) loaded
            Expect.equal late.Overview.Accounts (Success accounts) "ein geladener Stand bleibt"

        // Review-Fund: nach dem Import standen in der Seitenleiste die Salden von vorher.
        testCase "Nach dem Import lädt die Seitenleiste die Konten neu, auch nach einem Teilfehlschlag" <| fun () ->
            let result : ImportResult = { CreatedCount = 2; DuplicateTransactionIds = []; LenaFlagNotSentCount = 0 }
            let reloads (msg: Components.SyncFlow.Types.Msg) =
                let _, cmd = State.update (State.SyncFlowMsg msg) (rootModel SyncFlow)
                dispatched cmd |> List.contains (State.OverviewMsg SidebarNeedsAccounts)
            Expect.isTrue (reloads (Components.SyncFlow.Types.ImportCompleted (Ok result))) "Import fertig"
            Expect.isTrue (reloads (Components.SyncFlow.Types.ImportCompleted (Error (SyncError.YnabImportFailed (1, "x"))))) "Teilfehlschlag"
            Expect.isTrue (reloads (Components.SyncFlow.Types.ForceImportCompleted (Ok 1))) "Duplikate nachgeschoben"
            Expect.isFalse (reloads (Components.SyncFlow.Types.SetFilter Components.SyncFlow.Types.AllTransactions)) "sonst nicht"

        // Review-Fund: beim Start auf einer Kassenbuch-Seite luden Übersicht und Kassenbuch die Konten doppelt.
        testCase "Der Start lädt die Konten der Seitenleiste nur, wo die Seite sie nicht selbst lädt" <| fun () ->
            for page, loads in
                [ Overview, false; Ledger, false; LedgerEntry accountId, false
                  Budget, true; SyncFlow, true; Settings, true; Rules, true; QuickAdd, true ] do
                Expect.equal (State.initialLoadsSidebarAccounts page) loads (string page)

        testCase "Die Seiten-Navigation merkt die Seite vor den Einstellungen" <| fun () ->
            let model, _ = State.update (State.UrlChanged [ "settings" ]) (rootModel Budget)
            Expect.equal model.SettingsReturn Budget "Budget"
            let model, _ = State.update (State.UrlChanged [ "styleguide" ]) model
            let model, _ = State.update (State.UrlChanged [ "settings" ]) model
            Expect.equal model.SettingsReturn Budget "über den Styleguide hinweg"

        testCase "Die Übersicht ist nicht bereit, solange einer ihrer Stränge lädt" <| fun () ->
            let settled : Components.Overview.Types.Model =
                { Components.Overview.Types.init with
                    Month = Success november
                    Recent = Success []
                    Accounts = Failure "x"
                    Balances = Success [] }
            Expect.isTrue (State.isPageReady { rootModel Overview with Overview = settled }) "alles da"
            for name, loading in
                [ "Monat", { settled with Month = Loading }
                  "Konten", { settled with Accounts = Loading }
                  "Stände", { settled with Balances = Loading }
                  "Neueste", { settled with Recent = Loading } ] do
                Expect.isFalse (State.isPageReady { rootModel Overview with Overview = loading }) name

        testCase "Die Übersicht ist bereit, wenn Monat, Konten und Buchungen nicht mehr laden" <| fun () ->
            let loading = { rootModel Overview with Overview = Components.Overview.State.beginEnter (DateTime(2026, 11, 7)) Components.Overview.Types.init }
            Expect.isFalse (State.isPageReady loading) "lädt"
            let answered =
                [ MonthLoaded (Ok november); RecentLoaded (Ok []); AccountsLoaded (Error "x"); BalancesLoaded (Ok []) ]
                |> List.fold (fun m msg -> Components.Overview.State.applyAnswer msg m) loading.Overview
            Expect.isTrue (State.isPageReady { loading with Overview = answered }) "fertig, auch mit Fehler"

        // Review-Fund: bei einem Ladefehler stand unter „Konten“ nichts, ohne Konto ebenso.
        testCase "Die Konten zeigen Laden, Fehler, einen leeren Zustand oder die Liste" <| fun () ->
            let giro : LedgerAccountView = { Id = accountId; Name = "Giro"; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false }
            Expect.equal (accountsListState NotAsked) AccountsListLoading "noch nicht angefragt"
            Expect.equal (accountsListState Loading) AccountsListLoading "lädt"
            Expect.equal (accountsListState (Failure "weg")) (AccountsListFailed "weg") "Fehler"
            Expect.equal (accountsListState (Success { Accounts = []; DefaultAccountId = None })) AccountsListEmpty "leer"
            Expect.equal (accountsListState (Success { Accounts = [ giro ]; DefaultAccountId = None })) (AccountsListShown [ giro ]) "Liste"

        testCase "Beim erneuten Betreten bleiben geladene Werte stehen, der Rest lädt" <| fun () ->
            let accounts : LedgerAccountsView = { Accounts = []; DefaultAccountId = None }
            let shown =
                { Components.Overview.Types.init with
                    Month = Success november
                    Accounts = Success accounts
                    Recent = Failure "weg" }
            let again = Components.Overview.State.beginEnter (DateTime(2026, 11, 8)) shown
            Expect.equal again.Month (Success november) "Monat bleibt"
            Expect.equal again.Accounts (Success accounts) "Konten bleiben"
            Expect.equal again.Recent Loading "ein Fehler lädt neu"
            Expect.equal again.Balances Loading "noch nicht geladen"

        testCase "Ein spätes Scheitern ersetzt einen geladenen Monat nicht" <| fun () ->
            let loaded = Components.Overview.State.applyAnswer (MonthLoaded (Ok november)) Components.Overview.Types.init
            let late = Components.Overview.State.applyAnswer (MonthLoaded (Error "weg")) loaded
            Expect.equal late.Month (Success november) "bleibt"
    ]
