module Tests.BudgetPageTests

// budgeting-k7wmd: Zustandsübergänge der Budget-Seite, Routing und Navigation. Der Aufruf an
// den Server (Fable.Remoting-Proxy) ist unter .NET nicht testbar; die Übergänge liegen deshalb
// als reine Funktionen in `Components.Budget.Types`.

open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private monthK7wmd : BudgetMonth =
    { Month = "2026-10-01"
      ToBeBudgeted = eur -12.34m
      Groups = [] }

let private selectTests =
    testList "Monat wählen (k7wmd)" [
        test "Die Seite startet mit dem laufenden Monat und lädt noch nichts" {
            let m = init ()
            Expect.equal m.Choice CurrentMonth "Vorgabe: dieser Monat"
            Expect.equal m.Month NotAsked "noch nichts angefordert"
        }
        test "Erste Wahl des laufenden Monats lädt (Seite war noch leer)" {
            let _, load = select CurrentMonth (init ())
            Expect.isTrue load "NotAsked muss laden"
        }
        test "Wechsel auf den nächsten Monat setzt Loading und verlangt das Laden" {
            let loaded = { init () with Month = Success monthK7wmd }
            let m, load = select NextMonth loaded
            Expect.equal m.Choice NextMonth "Wahl"
            Expect.equal m.Month Loading "Loading"
            Expect.isTrue load "laden"
        }
        test "Dieselbe Wahl bei geladenem Monat ändert nichts" {
            let loaded = { init () with Month = Success monthK7wmd }
            let m, load = select CurrentMonth loaded
            Expect.equal m loaded "unverändert"
            Expect.isFalse load "kein Laden"
        }
    ]

let private loadedTests =
    testList "Antwort einnehmen (k7wmd)" [
        test "Eine Antwort zur gewählten Wahl wird gezeigt" {
            let m = applyLoaded CurrentMonth (Ok monthK7wmd) { init () with Month = Loading }
            Expect.equal m.Month (Success monthK7wmd) "Success"
        }
        test "Ein negatives Zu verteilen bleibt negativ, ohne Sonderzustand" {
            let m = applyLoaded CurrentMonth (Ok monthK7wmd) { init () with Month = Loading }
            match m.Month with
            | Success month -> Expect.equal month.ToBeBudgeted.Amount -12.34m "negativ wie geliefert"
            | other -> failtestf "Success erwartet, war %A" other
        }
        test "Eine späte Antwort zu einem abgewählten Monat wird verworfen" {
            let waiting = { Choice = NextMonth; Month = Loading }
            let m = applyLoaded CurrentMonth (Ok monthK7wmd) waiting
            Expect.equal m waiting "bleibt Loading für den nächsten Monat"
        }
        test "Ein YNAB-Fehler (auch 429) wird als lesbare Meldung zu Failure" {
            let message = "YNAB rate limit exceeded. Retry after 60 seconds"
            let m = applyLoaded CurrentMonth (Error message) { init () with Month = Loading }
            Expect.equal m.Month (Failure message) "Meldung bleibt erhalten"
        }
    ]

let private labelTests =
    testList "Monatsname (k7wmd)" [
        test "Oktober, März und Dezember" {
            Expect.equal (monthLabel "2026-10-01") "Oktober 2026" "Oktober"
            Expect.equal (monthLabel "2027-03-01") "März 2027" "März mit Umlaut"
            Expect.equal (monthLabel "2026-12-01") "Dezember 2026" "Dezember"
        }
        test "Ein unlesbarer Wert bleibt, wie er ist" {
            Expect.equal (monthLabel "kaputt") "kaputt" "kein Absturz"
            Expect.equal (monthLabel "2026-13-01") "2026-13-01" "Monat 13"
        }
        test "Kategorien werden über alle Gruppen gezählt" {
            let cat n : BudgetCategory =
                { Id = System.Guid.NewGuid(); Name = n; Assigned = eur 0m; Activity = eur 0m
                  Available = eur 0m; Underfunded = None; Snoozed = false }
            let month =
                { monthK7wmd with
                    Groups = [ { Name = "A"; Categories = [ cat "a"; cat "b" ] }; { Name = "B"; Categories = [ cat "c" ] } ] }
            Expect.equal (categoryCount month) 3 "drei"
        }
    ]

let private routingTests =
    testList "Routing und Navigation (k7wmd)" [
        test "#/budget führt zur Budget-Seite und zurück" {
            Expect.equal (Routing.parseUrl [ "budget" ]) Budget "parse"
            Expect.equal (Routing.toUrlSegments Budget) [ "budget" ] "toUrl"
        }
        test "Die Navigation führt einen Eintrag Budget" {
            let labels = Client.DesignSystem.Navigation.navItems |> List.map (fun i -> i.Label)
            Expect.contains labels "Budget" "Navigationseintrag"
        }
        test "Die Budget-Seite gilt als bereit, sobald der Monat nicht mehr lädt" {
            let model (month: RemoteData<BudgetMonth>) : State.Model =
                let settingsModel, _ = Components.Settings.State.init ()
                let syncFlowModel, _ = Components.SyncFlow.State.init ()
                let rulesModel, _ = Components.Rules.State.init ()
                let ledgerModel, _ = Components.Ledger.State.init ()
                { CurrentPage = Budget
                  Toasts = []
                  Pwa = Pwa.initialState
                  Settings = settingsModel
                  SyncFlow = syncFlowModel
                  Rules = rulesModel
                  Ledger = ledgerModel
                  Budget = { Choice = CurrentMonth; Month = month }
                  QuickAdd = None
                  QuickAddTemplates = NotAsked }
            Expect.isFalse (State.isPageReady (model Loading)) "lädt"
            Expect.isTrue (State.isPageReady (model (Success monthK7wmd))) "geladen"
            Expect.isTrue (State.isPageReady (model (Failure "x"))) "Fehler ist auch fertig"
        }
    ]

[<Tests>]
let tests = testList "Budget-Seite (k7wmd)" [ selectTests; loadedTests; labelTests; routingTests ]
