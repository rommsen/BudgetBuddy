module Tests.AppReadinessTests

// State.isPageReady (design-system-yxrxb): das App-eigene Bereitschaftssignal, das
// scripts/screenshot-route.mjs seit dieser Aufgabe abfragt, statt auf das
// Verschwinden von CSS-Klassen ("proto-spinner"/"animate-pulse") zu raten. Eine
// Route ohne eigenen Ladezustand ist sofort bereit; eine Route mit Ladezustand ist
// bereit, wenn keiner ihrer von `pageLoadCmd` geladenen Datenstränge mehr
// `Loading` ist (weder direkt noch, im Fall von SyncFlow, nach dem Anschluss-Ladevorgang
// für die Transaktionen).
//
// Baut das Modell ohne `State.init` (das die Browser-URL liest und Fable-only ist),
// nach dem Muster von `QuickAddTests.modelWithForm`.

open Expecto
open Types

let private baseModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = page
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        Fetch = Components.Fetch.Types.init; QuickAddCategories = NotAsked
        Rules = rulesModel
        Ledger = ledgerModel
        Budget = Components.Budget.State.init ()
        QuickAdd = None
        QuickAddTemplates = NotAsked; Overview = Components.Overview.Types.init; SettingsReturn = Types.Page.Overview
    }

[<Tests>]
let appReadinessTests =
    testList "State.isPageReady" [

        // budgeting-w9yt4 (Review): die Übersicht galt als bereit, während die angepinnten Kategorien noch luden; ein
        // Screenshot zeigte dann das Skeleton des Blocks „Angepinnt“.
        testCase "Die Übersicht ist nicht bereit, solange die angepinnten Kategorien laden" <| fun () ->
            let loaded =
                { Components.Overview.Types.init with
                    Month = NotAsked; Accounts = NotAsked; Balances = NotAsked; Recent = NotAsked; NotInYnab = NotAsked }
            let model = { baseModel Overview with Overview = { loaded with Pins = Loading } }
            Expect.isFalse (State.isPageReady model) "Pins laden noch"
            Expect.isTrue (State.isPageReady { model with Overview = { loaded with Pins = Success [] } }) "geladen ist bereit"

        testCase "Styleguide ist immer bereit: keine eigenen Datenstränge, rein präsentational" <| fun () ->
            let model = baseModel Styleguide
            Expect.isTrue (State.isPageReady model) "Styleguide hat keinen Ladezustand"

        testCase "Ledger ist NICHT bereit, solange Accounts noch lädt" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger = { (baseModel Ledger).Ledger with Accounts = Loading } }
            Expect.isFalse (State.isPageReady model) "Accounts lädt noch"

        testCase "Ledger ist NICHT bereit, solange Balances noch lädt" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Success { Accounts = []; DefaultAccountId = None }
                            Balances = Loading } }
            Expect.isFalse (State.isPageReady model) "Balances lädt noch"

        testCase "Ledger ist NICHT bereit, solange Entries noch lädt" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Success { Accounts = []; DefaultAccountId = None }
                            Balances = Success []
                            Entries = Loading } }
            Expect.isFalse (State.isPageReady model) "Entries lädt noch"

        testCase "Ledger ist NICHT bereit, solange Categories (Filterleiste, ledger-j8xq4) noch lädt" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Success { Accounts = []; DefaultAccountId = None }
                            Balances = Success []
                            Entries = Success []
                            Categories = Loading } }
            Expect.isFalse (State.isPageReady model) "Categories lädt noch"

        testCase "Ledger ist bereit, wenn Accounts, Balances, Entries und Categories geladen sind (Tabelle und Liste gefüllt)" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Success { Accounts = []; DefaultAccountId = None }
                            Balances = Success []
                            Entries = Success []
                            Categories = Success [] } }
            Expect.isTrue (State.isPageReady model) "Alle vier Datenstränge sind fertig"

        testCase "Die Route einer Buchung ist nicht bereit, solange die Buchung noch geholt wird" <| fun () ->
            let page = LedgerEntry (System.Guid.NewGuid())
            let ready =
                { (baseModel page).Ledger with
                    Accounts = Success { Accounts = []; DefaultAccountId = None }
                    Balances = Success []
                    Entries = Success []
                    Categories = Success [] }
            let waiting = { baseModel page with Ledger = { ready with PendingRouteEntry = Some (System.Guid.NewGuid()) } }
            Expect.isFalse (State.isPageReady waiting) "Buchung fehlt noch"
            Expect.isTrue (State.isPageReady { waiting with Ledger = ready }) "Buchung ist da"

        testCase "Ledger ist bereit, auch wenn ein Datenstrang fehlgeschlagen ist (kein Skeleton mehr, aber kein Laden mehr)" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Failure "kaputt"
                            Balances = Success []
                            Entries = Success []
                            Categories = Success [] } }
            Expect.isTrue (State.isPageReady model) "Failure ist kein Ladezustand mehr"

        testCase "Der Tab Regeln ist NICHT bereit, solange Rules noch lädt" <| fun () ->
            let model =
                { baseModel LedgerRules with
                    Rules = { (baseModel LedgerRules).Rules with Rules = Loading } }
            Expect.isFalse (State.isPageReady model) "Rules lädt noch"

        testCase "Der Tab Regeln ist bereit, sobald Rules geladen sind" <| fun () ->
            let model =
                { baseModel LedgerRules with
                    Rules = { (baseModel LedgerRules).Rules with Rules = Success [] } }
            Expect.isTrue (State.isPageReady model) "Rules ist fertig geladen"

        testCase "Settings ist NICHT bereit, solange Settings noch lädt (YnabBudgets zählt nicht: on-demand)" <| fun () ->
            let model =
                { baseModel Settings with
                    Settings =
                        { (baseModel Settings).Settings with
                            Settings = Loading
                            YnabBudgets = Loading } }
            Expect.isFalse (State.isPageReady model) "Settings lädt noch"

        testCase "QuickAdd ist NICHT bereit, solange QuickAddTemplates noch lädt" <| fun () ->
            let model = { baseModel QuickAdd with QuickAddTemplates = Loading }
            Expect.isFalse (State.isPageReady model) "QuickAddTemplates lädt noch"

        testCase "QuickAdd ist bereit, wenn QuickAddTemplates geladen sind" <| fun () ->
            let model = { baseModel QuickAdd with QuickAddTemplates = Success [] }
            Expect.isTrue (State.isPageReady model) "QuickAddTemplates ist fertig geladen"
    ]
