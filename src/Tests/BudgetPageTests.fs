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
            let waiting = { Choice = NextMonth; Month = Loading; Targets = TargetsClosed; CategorySearch = ""; Move = None; OpenRetries = []; Detail = None; DetailGeneration = 0 }
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
            let typed, _, _ = Components.Budget.State.update (CategorySearchChanged "leb") (init ())
            Expect.equal typed.CategorySearch "leb" "getippt"
            let cleared, _, _ = Components.Budget.State.update ClearCategorySearch typed
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
            let entered, cmd, _ = Components.Budget.State.update PageEntered searching
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

// ---------------- Kategorie-Detail (budgeting-m1bce) ----------------

let private categoryId = System.Guid.Parse "00000000-0000-0000-0000-00000000d001"
let private entryId = System.Guid.Parse "00000000-0000-0000-0000-00000000e001"

let private detailCategoryFixture : BudgetCategory =
    { Id = categoryId
      Name = "Lebensmittel"
      Assigned = eur 100m
      Activity = eur -30m
      Available = eur 70m
      Underfunded = None
      Snoozed = false }

let private detailMonth : BudgetMonth =
    { Month = "2026-10-01"
      ToBeBudgeted = eur 0m
      Groups = [ { Name = "Alltag"; Categories = [ detailCategoryFixture ] } ] }

let private withMonth = { init () with Month = Success detailMonth }

let private line (amount: decimal) : Shared.Domain.CategoryMonthEntry =
    { EntryId = entryId
      AccountId = entryId
      AccountName = Some "Giro"
      Date = System.DateTime(2026, 10, 5)
      Payee = Some "Rewe"
      Amount = eur amount
      IsSplitShare = false }

let private opened = openCategoryDetail categoryId withMonth

let private detailTests =
    testList "Kategorie-Detail" [
        test "Tipp auf den Namen öffnet das Detail und holt die Liste des Monats" {
            let model, month = opened
            Expect.equal model.Detail (Some { CategoryId = categoryId; Entries = Loading; ReturnPending = false }) "offen"
            Expect.equal month (Some "2026-10-01") "Monat der Liste"
        }
        test "Der Kopf kennt Kategorie und Gruppe aus dem geladenen Monat" {
            let detail = (fst opened).Detail |> Option.get
            Expect.equal (detailCategory detailMonth detail) (Some (detailCategoryFixture, "Alltag")) "Beträge von YNAB"
        }
        test "Eine unbekannte Kategorie öffnet nichts" {
            let model, month = openCategoryDetail (System.Guid.NewGuid()) withMonth
            Expect.isNone model.Detail "zu"
            Expect.isNone month "keine Abfrage"
        }
        test "Ohne geladenen Monat öffnet nichts" {
            let model, _ = openCategoryDetail categoryId (init ())
            Expect.isNone model.Detail "zu"
        }
        test "Die Antwort zur laufenden Abfrage füllt die Liste" {
            let model, _ = opened
            let answered = applyCategoryEntries model.DetailGeneration (Ok [ line -30m ]) model
            Expect.equal (answered.Detail |> Option.map (fun d -> d.Entries)) (Some (Success [ line -30m ])) "Liste"
        }
        test "Ein Fehler der Abfrage steht im Detail" {
            let model, _ = opened
            let answered = applyCategoryEntries model.DetailGeneration (Error "kaputt") model
            Expect.equal (answered.Detail |> Option.map (fun d -> d.Entries)) (Some (Failure "kaputt")) "Fehler"
        }
        test "Eine späte Antwort einer älteren Abfrage wird verworfen" {
            let model, _ = opened
            let stale = model.DetailGeneration
            let reloaded, _ = reloadCategoryEntries model
            let answered = applyCategoryEntries stale (Ok [ line -1m ]) reloaded
            Expect.equal answered reloaded "ältere Antwort zählt nicht"
        }
        test "Eine späte Antwort zu einem geschlossenen Detail wird verworfen" {
            let model, _ = opened
            let closed = { model with Detail = None }
            Expect.equal (applyCategoryEntries model.DetailGeneration (Ok []) closed) closed "bleibt zu"
        }
        test "Ein Monatswechsel schließt das Detail" {
            let model, _ = select NextMonth (fst opened)
            Expect.isNone model.Detail "zu"
        }
        test "Schließen schließt das Detail" {
            let model, _, _ = Components.Budget.State.update CloseCategoryDetail (fst opened)
            Expect.isNone model.Detail "zu"
        }
        test "Fehlt die Kategorie im neu gelesenen Monat, schließt das Detail" {
            let model = applyLoaded CurrentMonth (Ok { detailMonth with Groups = [] }) (fst opened)
            Expect.isNone model.Detail "zu"
        }
    ]

let private totalTests =
    testList "Summe und Abweichung" [
        test "Die Summe zählt alle Zeilen" {
            Expect.equal (entriesTotal (eur -30m) [ line -10m; line -20m ]) (eur -30m) "Summe"
        }
        test "Eine leere Liste ergibt 0 in der Währung der Kategorie" {
            Expect.equal (entriesTotal (eur -30m) []) (eur 0m) "leer"
        }
        test "Gleiche Beträge ergeben keinen Hinweis" {
            Expect.isNone (deviationHint (eur -30m) (eur -30m)) "gleich"
        }
        test "Ungleiche Beträge nennen beide, ohne Bewertung" {
            let text (amount: decimal) = Client.DesignSystem.Money.formatAmountText false true amount "EUR"
            Expect.equal
                (deviationHint (eur -30m) (eur -35.5m))
                (Some $"Kassenbuch: {text -30m}, YNAB: {text -35.5m}")
                "beide Beträge"
        }
    ]

let private returnTests =
    testList "Sprung ins Kassenbuch und zurück" [
        test "Tipp auf eine Buchung springt, das Detail wartet auf die Rückkehr" {
            let model, _, external = Components.Budget.State.update (OpenEntryInLedger entryId) (fst opened)
            Expect.equal external (JumpToEntry entryId) "Sprung"
            Expect.isTrue (returnPending model) "wartet"
        }
        test "Zurück auf der Seite steht das Detail wieder da, die Liste wird neu geholt" {
            let left = leaveForEntry (fst opened)
            let model, month = enterPage left
            Expect.equal (model.Detail |> Option.map (fun d -> d.CategoryId)) (Some categoryId) "wieder offen"
            Expect.isFalse (returnPending model) "angekommen"
            Expect.equal (model.Detail |> Option.map (fun d -> d.Entries)) (Some Loading) "lädt neu"
            Expect.equal month (Some "2026-10-01") "Monat der Liste"
            Expect.isGreaterThan model.DetailGeneration left.DetailGeneration "neue Abfrage"
        }
        test "Ein gewöhnliches Betreten der Seite schließt das Detail" {
            let model, month = enterPage (fst opened)
            Expect.isNone model.Detail "zu"
            Expect.isNone month "keine Abfrage"
        }
        // F2: Nach einem Umweg über die Tab-Leiste ging das Detail wieder auf.
        test "Ist die Rückkehr hinfällig, bleibt das Detail beim nächsten Betreten zu" {
            let left = leaveForEntry (fst opened)
            let abandoned, _, _ = Components.Budget.State.update ReturnAbandoned left
            let model, month = enterPage abandoned
            Expect.isNone model.Detail "zu"
            Expect.isNone month "keine Abfrage"
        }
        test "Wer die Route auf eine andere Seite verlässt, gibt die Rückkehr auf" {
            for target in [ Budget; Settings; Ledger ] do
                Expect.isTrue (State.routeNavigation (LedgerEntry entryId) target).AbandonsReturn $"{target}"
        }
        test "Auf anderen Seiten und beim Wechsel der Buchung wartet die Rückkehr weiter" {
            Expect.isFalse (State.routeNavigation Ledger Budget).AbandonsReturn "keine Route"
            Expect.isFalse (State.routeNavigation (LedgerEntry entryId) (LedgerEntry categoryId)).AbandonsReturn "Route bleibt"
        }
        // F5: Der Eintrag der Route blieb in der Historie, Browser-Zurück öffnete die Buchung erneut.
        test "Von der Route in die Liste ersetzt den Eintrag der Historie" {
            Expect.isTrue (State.routeNavigation (LedgerEntry entryId) Ledger).ReplacesHistory "Liste"
            Expect.isTrue (State.routeNavigation (LedgerEntry entryId) (LedgerEntry categoryId)).ReplacesHistory "Panel-Wechsel"
        }
        test "Der Sprung und der Weg über die Tab-Leiste legen einen Eintrag an" {
            Expect.isFalse (State.routeNavigation Budget (LedgerEntry entryId)).ReplacesHistory "Sprung"
            Expect.isFalse (State.routeNavigation (LedgerEntry entryId) Budget).ReplacesHistory "Tab"
        }
        test "Liste und Route sind dieselbe Seite für React" {
            Expect.equal (pageKey (LedgerEntry entryId)) (pageKey Ledger) "gleicher Schlüssel"
            Expect.notEqual (pageKey Budget) (pageKey Ledger) "andere Seite"
        }
        test "Die Route einer Buchung wird gelesen und erzeugt" {
            let page = Routing.parseUrl [ "ledger"; "entry"; string entryId ]
            Expect.equal page (LedgerEntry entryId) "parse"
            Expect.equal (Routing.toUrlSegments page) [ "ledger"; "entry"; string entryId ] "toUrl"
        }
        test "Eine unlesbare Buchungs-Id führt ins Kassenbuch" {
            Expect.equal (Routing.parseUrl [ "ledger"; "entry"; "kaputt" ]) Ledger "parse"
        }
        test "Die Route zählt als Kassenbuch-Seite" {
            Expect.isTrue (isLedgerPage (LedgerEntry entryId)) "Route"
            Expect.isFalse (isLedgerPage Budget) "Budget nicht"
        }
        test "Schließen nach dem Sprung geht zurück ins Budget" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true false true true false) State.BackToBudget "zurück"
        }
        test "Schließen nach einem Direktaufruf bleibt im Kassenbuch" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true false true false false) State.ToLedgerList "Liste"
        }
        test "Ein Kontowechsel schließt das Detail, ohne ins Budget zu springen" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true false false true false) State.ToLedgerList "Liste"
        }
        test "Bleibt das Detail offen oder war es zu, bleibt die Route" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true true true true false) State.StayOnRoute "offen"
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) false false true true false) State.StayOnRoute "noch nicht offen"
        }
        // F6: Nach einem Ladefehler blieb die URL auf der Route, und Schließen griff nicht.
        test "Scheitert das Laden nach dem Sprung, geht es zurück ins Budget" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true false false true true) State.BackToBudget "zurück"
        }
        test "Scheitert das Laden nach einem Direktaufruf, bleibt die Liste" {
            Expect.equal (State.entryRouteExit (LedgerEntry entryId) true false false false true) State.ToLedgerList "Liste"
        }
        // F8: Am PC wechselte das Panel, die URL blieb auf der ersten Buchung.
        test "Wechselt das Panel die Buchung, folgt die URL" {
            Expect.equal (State.entryRouteFollow (LedgerEntry entryId) (Some entryId) (Some categoryId) None) (Some categoryId) "folgt"
        }
        test "Ohne Wechsel, außerhalb der Route und bei laufender Abfrage bleibt die URL" {
            let follow = State.entryRouteFollow
            Expect.isNone (follow (LedgerEntry entryId) (Some entryId) (Some entryId) None) "gleiche Buchung"
            Expect.isNone (follow Ledger (Some entryId) (Some categoryId) None) "keine Route"
            Expect.isNone (follow (LedgerEntry entryId) (Some entryId) (Some categoryId) (Some entryId)) "Abfrage läuft"
            Expect.isNone (follow (LedgerEntry categoryId) (Some entryId) (Some categoryId) None) "URL stimmt schon"
        }
        test "Auf der gewöhnlichen Kassenbuch-Seite gibt es keine Route zu verlassen" {
            Expect.equal (State.entryRouteExit Ledger true false true true false) State.StayOnRoute "Liste"
        }
    ]

[<Tests>]
let tests =
    testList "Budget-Seite (k7wmd)" [
        selectTests
        loadedTests
        labelTests
        routingTests
        filterTests
        searchStateTests
        detailTests
        totalTests
        returnTests
    ]
