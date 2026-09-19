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
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = page
        Toasts = []
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        Ledger = ledgerModel
        QuickAdd = None
        QuickAddTemplates = NotAsked
    }

[<Tests>]
let appReadinessTests =
    testList "State.isPageReady" [

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

        testCase "Ledger ist bereit, wenn Accounts, Balances und Entries geladen sind (Tabelle und Liste gefüllt)" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Success { Accounts = []; DefaultAccountId = None }
                            Balances = Success []
                            Entries = Success [] } }
            Expect.isTrue (State.isPageReady model) "Alle drei Datenstränge sind fertig"

        testCase "Ledger ist bereit, auch wenn ein Datenstrang fehlgeschlagen ist (kein Skeleton mehr, aber kein Laden mehr)" <| fun () ->
            let model =
                { baseModel Ledger with
                    Ledger =
                        { (baseModel Ledger).Ledger with
                            Accounts = Failure "kaputt"
                            Balances = Success []
                            Entries = Success [] } }
            Expect.isTrue (State.isPageReady model) "Failure ist kein Ladezustand mehr"

        testCase "Rules ist NICHT bereit, solange Rules noch lädt" <| fun () ->
            let model =
                { baseModel Rules with
                    Rules = { (baseModel Rules).Rules with Rules = Loading } }
            Expect.isFalse (State.isPageReady model) "Rules lädt noch"

        testCase "Rules ist bereit, sobald Rules geladen sind" <| fun () ->
            let model =
                { baseModel Rules with
                    Rules = { (baseModel Rules).Rules with Rules = Success [] } }
            Expect.isTrue (State.isPageReady model) "Rules ist fertig geladen"

        testCase "Settings ist NICHT bereit, solange Settings noch lädt (YnabBudgets zählt nicht: on-demand)" <| fun () ->
            let model =
                { baseModel Settings with
                    Settings =
                        { (baseModel Settings).Settings with
                            Settings = Loading
                            YnabBudgets = Loading } }
            Expect.isFalse (State.isPageReady model) "Settings lädt noch"

        testCase "SyncFlow ist NICHT bereit, solange CurrentSession noch lädt" <| fun () ->
            let model =
                { baseModel SyncFlow with
                    SyncFlow = { (baseModel SyncFlow).SyncFlow with CurrentSession = Loading } }
            Expect.isFalse (State.isPageReady model) "CurrentSession lädt noch"

        testCase "SyncFlow ist NICHT bereit, wenn CurrentSession fertig ist, aber die Transaktionen anschliessend noch laden" <| fun () ->
            let model =
                { baseModel SyncFlow with
                    SyncFlow =
                        { (baseModel SyncFlow).SyncFlow with
                            CurrentSession = Success None
                            SyncTransactions = Loading } }
            Expect.isFalse (State.isPageReady model) "SyncTransactions lädt noch, auch nach CurrentSession"

        testCase "SyncFlow ist bereit, wenn CurrentSession und SyncTransactions fertig sind" <| fun () ->
            let model =
                { baseModel SyncFlow with
                    SyncFlow =
                        { (baseModel SyncFlow).SyncFlow with
                            CurrentSession = Success None
                            SyncTransactions = NotAsked } }
            Expect.isTrue (State.isPageReady model) "Kein Datenstrang lädt mehr"

        testCase "QuickAdd ist NICHT bereit, solange QuickAddTemplates noch lädt" <| fun () ->
            let model = { baseModel QuickAdd with QuickAddTemplates = Loading }
            Expect.isFalse (State.isPageReady model) "QuickAddTemplates lädt noch"

        testCase "QuickAdd ist bereit, wenn QuickAddTemplates geladen sind" <| fun () ->
            let model = { baseModel QuickAdd with QuickAddTemplates = Success [] }
            Expect.isTrue (State.isPageReady model) "QuickAddTemplates ist fertig geladen"
    ]
