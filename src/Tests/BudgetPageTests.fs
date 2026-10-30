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
            let waiting = { Choice = NextMonth; Month = Loading; Targets = TargetsClosed; CategorySearch = "" }
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
                  Budget = { Components.Budget.Types.init () with Month = month }
                  QuickAdd = None
                  QuickAddTemplates = NotAsked }
            Expect.isFalse (State.isPageReady (model Loading)) "lädt"
            Expect.isTrue (State.isPageReady (model (Success monthK7wmd))) "geladen"
            Expect.isTrue (State.isPageReady (model (Failure "x"))) "Fehler ist auch fertig"
        }
    ]

// budgeting-rxxb4: Kategorie-Suche. Die Filterfunktion ist rein, das Suchfeld hängt am Modell.
let private rxxb4Money = eur 0m

let private cat (n: int) (name: string) : BudgetCategory =
    { Id = System.Guid(n, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)
      Name = name
      Assigned = rxxb4Money
      Activity = rxxb4Money
      Available = rxxb4Money
      Underfunded = None
      Snoozed = false }

let private groupsRxxb4 : BudgetGroup list =
    [ { Name = "Alltag"; Categories = [ cat 1 "Lebensmittel"; cat 2 "Kino" ] }
      { Name = "Wohnen"; Categories = [ cat 3 "Strom"; cat 4 "Internet" ] }
      { Name = "Rücklagen"; Categories = [ cat 5 "Urlaub" ] } ]

let private names (groups: BudgetGroup list) =
    groups |> List.map (fun g -> g.Name, g.Categories |> List.map (fun c -> c.Name))

let private filterTests =
    testList "Kategorie-Suche filtert (rxxb4)" [
        test "Teilstring im Kategorie-Namen: nur die Treffer und ihre Gruppe" {
            Expect.equal (names (filterGroups "ebens" groupsRxxb4)) [ "Alltag", [ "Lebensmittel" ] ] "Treffer"
        }
        test "Treffer im Gruppennamen zeigt die ganze Gruppe" {
            Expect.equal (names (filterGroups "ohn" groupsRxxb4)) [ "Wohnen", [ "Strom"; "Internet" ] ] "ganze Gruppe"
        }
        test "Groß- und Kleinschreibung ist egal, in beide Richtungen" {
            Expect.equal (names (filterGroups "LEBENS" groupsRxxb4)) [ "Alltag", [ "Lebensmittel" ] ] "groß gesucht"
            Expect.equal (names (filterGroups "wohnen" groupsRxxb4)) [ "Wohnen", [ "Strom"; "Internet" ] ] "klein gesucht"
        }
        test "Führende und folgende Leerzeichen sind egal" {
            Expect.equal (names (filterGroups "  kino  " groupsRxxb4)) [ "Alltag", [ "Kino" ] ] "getrimmt"
        }
        test "Leere Suche und reine Leerzeichen zeigen alles" {
            Expect.equal (filterGroups "" groupsRxxb4) groupsRxxb4 "leer"
            Expect.equal (filterGroups "   " groupsRxxb4) groupsRxxb4 "Leerzeichen"
        }
        test "Kein Treffer ergibt eine leere Liste" {
            Expect.isEmpty (filterGroups "xyz" groupsRxxb4) "keine Gruppe"
        }
        test "Reihenfolge der Gruppen und Kategorien bleibt" {
            Expect.equal (names (filterGroups "n" groupsRxxb4))
                [ "Alltag", [ "Lebensmittel"; "Kino" ]
                  "Wohnen", [ "Strom"; "Internet" ]
                  "Rücklagen", [ "Urlaub" ] ]
                "Treffer in YNAB-Reihenfolge"
        }
    ]

let private searchStateTests =
    testList "Kategorie-Suche im Modell (rxxb4)" [
        test "Die Seite startet ohne Suche" {
            Expect.equal (init ()).CategorySearch "" "leer"
        }
        test "Tippen setzt die Suche, ✕ leert sie" {
            let typed, _ = Components.Budget.State.update (CategorySearchChanged "leb") (init ())
            Expect.equal typed.CategorySearch "leb" "getippt"
            let cleared, _ = Components.Budget.State.update ClearCategorySearch typed
            Expect.equal cleared.CategorySearch "" "geleert"
        }
        test "Monatswechsel und die Antwort darauf behalten die Suche" {
            let searching = { init () with Month = Success monthK7wmd; CategorySearch = "leb" }
            let switched, _ = select NextMonth searching
            Expect.equal switched.CategorySearch "leb" "beim Wechsel"
            let loaded = applyLoaded NextMonth (Ok monthK7wmd) switched
            Expect.equal loaded.CategorySearch "leb" "nach der Antwort"
        }
        test "Betreten der Seite leert die Suche und holt den Monat" {
            let searching = { init () with Month = Success monthK7wmd; CategorySearch = "leb" }
            let entered, cmd = Components.Budget.State.update PageEntered searching
            Expect.equal entered.CategorySearch "" "neuer Seitenaufruf ist leer"
            let mutable followUps = []
            cmd |> List.iter (fun sub -> sub (fun msg -> followUps <- msg :: followUps))
            Expect.equal followUps [ Load ] "der Monat wird weiter geladen"
        }
        test "Der Seitenaufruf läuft über das Betreten, nicht direkt über das Laden" {
            let mutable msgs = []
            State.pageLoadCmd Budget |> List.iter (fun sub -> sub (fun msg -> msgs <- msg :: msgs))
            Expect.equal msgs [ State.BudgetMsg PageEntered ] "Suche wird beim Betreten geleert"
        }
    ]

[<Tests>]
let tests = testList "Budget-Seite (k7wmd)" [ selectTests; loadedTests; labelTests; routingTests; filterTests; searchStateTests ]
