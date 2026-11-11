module Tests.LedgerCategoriesAwaitSettingsTests

// Regression zum Befund vom 2026-10-24 (ledger-k9tq7): Am PC blieb der Kategorie-Picker
// im Kassenbuch leer, weil das Laden der Settings beim Seitenaufruf gescheitert war.
// `LedgerMsg LoadCategories` gab bei Settings ungleich `Success` still auf
// (`| _ -> model, Cmd.none`), und auch das Öffnen des Pickers
// (`EnsureCategoriesFresh`) holte nie etwas nach. Jetzt holt `LoadCategories` die
// Settings nach und merkt sich, dass die Kategorien auf sie warten.
//
// Der Abruf `getCategories` selbst läuft über den Fable.Remoting-Proxy und ist unter
// .NET nicht ausführbar. Geprüft wird deshalb, dass nach `SettingsLoaded (Ok …)` ein
// `LedgerMsg LoadCategories` abgeschickt wird, und dass dieser Zweig bei Settings
// `Success` mit Budget zum Abruf führt (Quelltext-Zusicherung unten).

open Expecto
open Types
open Shared.Domain
open State

let private baseModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = page
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        Ledger = ledgerModel
        Budget = Components.Budget.State.init ()
        QuickAdd = None
        QuickAddTemplates = NotAsked; Overview = Components.Overview.Types.init; SettingsReturn = Types.Page.Overview
    }

let private settingsWithBudget : AppSettingsView = {
    Comdirect = None
    Ynab =
        Some {
            HasToken = true
            DefaultBudgetId = Some (YnabBudgetId "fake-budget-id")
            DefaultAccountId = None
            QuickAddAccountId = None
        }
    Sync = { DaysToFetch = 30 }
}

/// Nur für `Cmd.ofMsg`/`Cmd.batch`/`Cmd.map` zulässig, kein `Cmd.OfAsync`.
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private settingsWithoutYnab : AppSettingsView = { settingsWithBudget with Ynab = None }

let private settingsWithoutBudget : AppSettingsView =
    { settingsWithBudget with Ynab = settingsWithBudget.Ynab |> Option.map (fun y -> { y with DefaultBudgetId = None }) }

let private expectFailure (expected: string) (m: State.Model) =
    Expect.equal m.Ledger.Categories (Failure expected) "Fehler mit Grund statt Loading"
    Expect.isFalse m.Ledger.CategoriesAwaitSettings "Warten beendet"

let private withSettings (settings: RemoteData<AppSettingsView>) (model: State.Model) =
    { model with Settings = { model.Settings with Settings = settings } }

let private loadSettingsMsg = SettingsMsg Components.Settings.Types.LoadSettings
let private loadCategoriesMsg = LedgerMsg Components.Ledger.Types.LoadCategories

let private requestCategories settings =
    baseModel Ledger
    |> withSettings settings
    |> State.update loadCategoriesMsg

[<Tests>]
let ledgerCategoriesAwaitSettingsTests =
    testList "State: Kategorien laden holt fehlende Settings nach (ledger-k9tq7)" [

        test "Settings Failure: LoadCategories setzt Loading, merkt das Warten und löst LoadSettings aus" {
            let m, cmd = requestCategories (Failure "Http error (0)")
            Expect.equal m.Ledger.Categories Loading "Kategorien laden"
            Expect.isTrue m.Ledger.CategoriesAwaitSettings "wartet auf Settings"
            Expect.contains (dispatched cmd) loadSettingsMsg "LoadSettings wird ausgelöst"
        }

        test "Settings NotAsked verhält sich wie Failure" {
            let m, cmd = requestCategories NotAsked
            Expect.equal m.Ledger.Categories Loading "Kategorien laden"
            Expect.isTrue m.Ledger.CategoriesAwaitSettings "wartet auf Settings"
            Expect.contains (dispatched cmd) loadSettingsMsg "LoadSettings wird ausgelöst"
        }

        test "Settings Loading: Kategorien Loading und wartend, aber kein zweites LoadSettings" {
            let m, cmd = requestCategories Loading
            Expect.equal m.Ledger.Categories Loading "Kategorien laden"
            Expect.isTrue m.Ledger.CategoriesAwaitSettings "wartet auf Settings"
            Expect.isEmpty (dispatched cmd) "Die laufende Abfrage wird nicht verdoppelt"
        }

        test "Nach dem Warten löst SettingsLoaded (Ok) mit Budget ein LoadCategories aus, auch wenn Categories Loading ist" {
            let waiting, _ = requestCategories (Failure "x")
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) waiting
            Expect.contains (dispatched cmd) loadCategoriesMsg "LoadCategories startet"
        }

        test "Mit Settings Success und Budget führt LoadCategories zum Abruf getCategories und beendet das Warten" {
            // ledger-r8h19: vorher am Quelltext von State.fs geprüft. Jetzt über den Port
            // `GetCategories` (Cmd über Lambda, Doktrin Punkt 1).
            let calls = System.Collections.Concurrent.ConcurrentQueue<YnabBudgetId>()
            let category : YnabCategory =
                { Id = YnabCategoryId (System.Guid.Parse "0195e900-0000-7000-8000-0000000a8190")
                  Name = "Lebensmittel r8h19"
                  GroupName = "Alltag"
                  Available = None
                  Hidden = false }
            let ports : UpdatePorts =
                { GetCategories = fun budgetId -> async { calls.Enqueue budgetId; return Ok [ category ] } }
            let b = baseModel Ledger
            let waiting = { b with Ledger = { b.Ledger with Categories = Loading; CategoriesAwaitSettings = true } }
            let m, cmd = waiting |> withSettings (Success settingsWithBudget) |> State.updateWith ports loadCategoriesMsg
            Expect.isFalse m.Ledger.CategoriesAwaitSettings "Warten endet mit dem Abruf"
            Expect.equal m.Ledger.Categories Loading "Kategorien laden"
            let msg, _ = Tests.CmdTiming.firstDispatchedAfter 3000 cmd
            Expect.equal msg (LedgerMsg (Components.Ledger.Types.CategoriesLoaded (Ok [ category ]))) "die Antwort des Abrufs kommt an"
            Expect.equal (List.ofSeq calls) [ YnabBudgetId "fake-budget-id" ] "getCategories genau einmal mit dem Standard-Budget"
        }

        test "Nach dem Warten setzt SettingsLoaded (Error) die Kategorien auf Failure" {
            let waiting, _ = requestCategories NotAsked
            let m, _ = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Error "Http error (0)"))) waiting
            match m.Ledger.Categories with
            | Failure text -> Expect.stringContains text "Einstellungen konnten nicht geladen werden" "Fehlertext"
            | other -> failtestf "Failure erwartet, war %A" other
            Expect.isFalse m.Ledger.CategoriesAwaitSettings "Warten beendet"
        }

        // Regression ledger-vq2g8 (Bestandsreview 2026-11-06): kamen die Settings ohne YNAB oder
        // ohne Standard-Budget an, während die Kategorien warteten, blieb `Ledger.Categories`
        // für immer `Loading`, die Kategorienauswahl drehte sich.
        test "Nach dem Warten setzen Settings ohne YNAB die Kategorien auf Failure mit Grund" {
            let waiting, _ = requestCategories NotAsked
            let m, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithoutYnab))) waiting
            expectFailure "YNAB ist in den Settings nicht eingerichtet" m
            Expect.isFalse (List.contains loadCategoriesMsg (dispatched cmd)) "kein Abruf"
        }

        test "Nach dem Warten setzen Settings mit YNAB, aber ohne Budget die Kategorien auf Failure mit Grund" {
            let waiting, _ = requestCategories NotAsked
            let m, _ = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithoutBudget))) waiting
            expectFailure "Kein Standard-Budget in den Settings gewählt" m
        }

        // Regression ledger-vq2g8: derselbe Fall direkt in `LoadCategories` (State.fs, die
        // Zweige ohne YNAB und ohne Budget gaben das Modell unverändert zurück).
        test "LoadCategories mit Settings ohne YNAB oder ohne Budget endet in Failure mit Grund" {
            let m1, _ = requestCategories (Success settingsWithoutYnab)
            expectFailure "YNAB ist in den Settings nicht eingerichtet" m1
            let waiting = { baseModel Ledger with Ledger = { (baseModel Ledger).Ledger with Categories = Loading; CategoriesAwaitSettings = true } }
            let m2, _ = waiting |> withSettings (Success settingsWithoutBudget) |> State.update loadCategoriesMsg
            expectFailure "Kein Standard-Budget in den Settings gewählt" m2
        }

        test "Ein SettingsLoaded (Ok), während die Kategorien Success sind und nicht warten, löst kein LoadCategories aus" {
            let b = baseModel Ledger
            let loaded = { b with Ledger = { b.Ledger with Categories = Success [] } }
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) loaded
            Expect.isEmpty (dispatched cmd) "kein erneuter Abruf"
        }

        test "SettingsLoaded (Error) ohne wartende Kategorien lässt sie unverändert" {
            let m, _ = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Error "x"))) (baseModel Ledger)
            Expect.equal m.Ledger.Categories NotAsked "unverändert"
        }
    ]
