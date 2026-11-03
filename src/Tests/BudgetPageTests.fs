module Tests.BudgetPageTests

// budgeting-k7wmd: Zustandsübergänge der Budget-Seite, Routing und Navigation. Der Aufruf an
// den Server (Fable.Remoting-Proxy) ist unter .NET nicht testbar; die Übergänge liegen deshalb
// als reine Funktionen in `Components.Budget`.

open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private monthK7wmd : BudgetMonth =
    { Month = "2026-10-01"
      ToBeBudgeted = eur -12.34m
      Groups = [] }

let private frameK7wmd : BudgetFrame =
    { CurrentMonth = "2026-10-01"
      Months = [ "2026-09-01"; "2026-10-01"; "2026-11-01" ]
      CategoryOrder = [] }

/// Der Rahmen ist gelesen, der laufende Monat geladen.
let private framed = { init () with Frame = Success frameK7wmd; Month = Success monthK7wmd }

let private guardCategory : BudgetCategory =
    { Id = System.Guid.Parse "00000000-0000-0000-0000-00000000f001"
      Name = "Essen"
      Assigned = eur 1m
      Activity = eur 0m
      Available = eur 1m
      Underfunded = None
      Target = None
      Snoozed = false }

let private detailMonthForGuard : BudgetMonth =
    { monthK7wmd with Groups = [ { Name = "Alltag"; Categories = [ guardCategory ] } ] }

let private selectTests =
    testList "Monat wählen" [
        test "Die Seite startet ohne Monat und lädt noch nichts" {
            let m = init ()
            Expect.isNone (shownMonth m) "ohne Rahmen kein Monat"
            Expect.equal m.Month NotAsked "noch nichts angefordert"
        }
        test "Ohne Wahl zeigt die Seite den laufenden Monat des Rahmens, ein offener Tab springt so mit" {
            Expect.equal (shownMonth framed) (Some "2026-10-01") "laufend"
            let nextDay = { framed with Frame = Success { frameK7wmd with CurrentMonth = "2026-11-01" } }
            Expect.equal (shownMonth nextDay) (Some "2026-11-01") "am Monatsersten"
        }
        // Bug 2 aus budgeting-p4n6r: Der Wechsel setzte `Loading`, Liste, Suche, Zu verteilen und
        // Inspektor verschwanden bis zur Antwort, die Seite sprang.
        test "Wechsel lässt den alten Monat bis zur Antwort lesbar und verlangt das Laden" {
            let m, load = select "2026-09-01" framed
            Expect.equal m.ChosenMonth (Some "2026-09-01") "Wahl"
            Expect.equal m.Month (Success monthK7wmd) "der alte Monat bleibt"
            Expect.isTrue (monthChanging m) "der Wechsel läuft"
            Expect.isTrue load "laden"
        }
        test "Nach der Antwort ist der Wechsel zu Ende" {
            let m, _ = select "2026-09-01" framed
            let answered = applyLoaded m.MonthGeneration (Ok { monthK7wmd with Month = "2026-09-01" }) m
            Expect.isFalse (monthChanging answered) "fertig"
        }
        test "Scheitert der Wechsel, steht der Fehler da und der Monat bleibt gewählt" {
            let m, _ = select "2026-09-01" framed
            let failed = applyLoaded m.MonthGeneration (Error "kaputt") m
            Expect.equal failed.Month (Failure "kaputt") "Fehlerkarte"
            Expect.equal (shownMonth failed) (Some "2026-09-01") "die Pfeile bleiben beim Ziel"
        }
        test "Wechsel ohne geladenen Monat zeigt das Laden wie der erste Aufruf" {
            let m, _ = select "2026-09-01" { framed with Month = Failure "kaputt" }
            Expect.equal m.Month Loading "kein alter Monat zum Stehenlassen"
        }
        test "Während des Wechsels gehen Targets nicht in den alten Monat" {
            let m, _ = select "2026-11-01" { framed with Month = Success detailMonthForGuard }
            Expect.isFalse (snd (openTargets m)) "Targets"
        }
        test "Der laufende Monat wird zu keiner Wahl" {
            let elsewhere = { framed with ChosenMonth = Some "2026-09-01" }
            let m, load = select "2026-10-01" elsewhere
            Expect.isNone m.ChosenMonth "keine Wahl"
            Expect.isTrue load "laden"
        }
        test "Derselbe Monat bei geladenem Monat ändert nichts" {
            let m, load = select "2026-10-01" framed
            Expect.equal m framed "unverändert"
            Expect.isFalse load "kein Laden"
        }
        test "Blättern arbeitet per Vergleich, auch über den Jahreswechsel" {
            let months = [ "2025-11-01"; "2025-12-01"; "2026-01-01"; "2026-02-01" ]
            Expect.equal (previousMonth months "2026-01-01") (Some "2025-12-01") "zurück über Silvester"
            Expect.equal (nextMonth months "2025-12-01") (Some "2026-01-01") "vor über Silvester"
        }
        test "Am Rand gibt es keinen Monat mehr" {
            let months = [ "2026-09-01"; "2026-10-01" ]
            Expect.isNone (previousMonth months "2026-09-01") "vorn"
            Expect.isNone (nextMonth months "2026-10-01") "hinten"
        }
        test "Ein Monat außerhalb des Fensters blättert ins Fenster zurück" {
            let months = [ "2026-09-01"; "2026-10-01" ]
            Expect.equal (previousMonth months "2026-12-01") (Some "2026-10-01") "dahinter"
            Expect.isNone (nextMonth months "2026-12-01") "nichts weiter"
            Expect.equal (nextMonth months "2026-01-01") (Some "2026-09-01") "davor"
        }
        // ADR 0074: Fehlt der laufende Monat im Fenster, bleibt die Navigation trotzdem da.
        test "Monat nicht offen: der Pfeil lädt den Vormonat, auch außerhalb des Fensters" {
            let outside = { CurrentMonth = "2026-12-01"; Months = [ "2026-09-01"; "2026-10-01" ]; CategoryOrder = [] }
            let failed = { init () with Frame = Success outside; Month = Failure "Monat nicht offen" }
            let target = shownMonth failed |> Option.bind (previousMonth outside.Months)
            Expect.equal target (Some "2026-10-01") "Pfeil bleibt"
            let m, load = select "2026-10-01" failed
            Expect.isTrue load "lädt"
            Expect.equal (shownMonth m) (Some "2026-10-01") "Vormonat"
        }
    ]

let private categoryIdForFrame = System.Guid.Parse "00000000-0000-0000-0000-00000000f002"

let private frameLoadTests =
    testList "Rahmen lesen" [
        test "Erst der Rahmen, dann der Monat: die Antwort verlangt das Laden und setzt die Reihenfolge" {
            let m, external, loadMonth = applyFrameLoaded (Ok frameK7wmd) (beginLoad (init ()))
            Expect.equal m.Frame (Success frameK7wmd) "Rahmen"
            Expect.equal m.Month Loading "Monat lädt"
            Expect.isTrue loadMonth "Monat holen"
            Expect.equal external NoOp "still"
        }
        test "Der erste Rahmen-Fehler zeigt die Fehlerkarte und lädt keinen Monat" {
            let m, external, loadMonth = applyFrameLoaded (Error "kaputt") (beginLoad (init ()))
            Expect.equal m.Month (Failure "kaputt") "Karte"
            Expect.isFalse loadMonth "kein Monat"
            Expect.equal external NoOp "kein Toast"
        }
        test "Gibt es schon einen Rahmen, bleibt er stehen, ein Toast nennt den Grund, der Monat kommt" {
            let m, external, loadMonth = applyFrameLoaded (Error "kaputt") (beginLoad framed)
            Expect.equal m.Frame framed.Frame "bleibt"
            Expect.isTrue loadMonth "Monat holen"
            match external with
            | ShowToast (text, _) -> Expect.stringContains text "kaputt" "Grund"
            | other -> failtestf "Toast erwartet: %A" other
        }
        test "Scheitert das stille Nachlesen, bleibt das alte Fenster ohne Meldung" {
            let model, cmd, external = Components.Budget.State.update (FrameRefreshed (Error "kaputt")) framed
            Expect.equal model framed "unverändert"
            Expect.isEmpty cmd "keine Anfrage"
            Expect.equal external NoOp "kein Toast"
        }
        test "Das stille Nachlesen übernimmt das gewachsene Fenster und lädt keinen Monat" {
            let grown = { frameK7wmd with Months = frameK7wmd.Months @ [ "2026-12-01" ] }
            let model, load = applyFrameRefreshed (Ok grown) framed
            Expect.equal model.Frame (Success grown) "gewachsen"
            Expect.isFalse load "der gezeigte Monat ist derselbe"
        }
        // Bug 3 aus budgeting-p4n6r: Am Monatsersten änderte das Nachlesen `shownMonth`, die Pfeile
        // standen im neuen Monat und die Liste im alten.
        test "Ändert das stille Nachlesen den gezeigten Monat, wird er neu geladen" {
            let next = { frameK7wmd with CurrentMonth = "2026-11-01" }
            let model, load = applyFrameRefreshed (Ok next) framed
            Expect.isTrue load "Monat holen"
            Expect.equal (shownMonth model) (Some "2026-11-01") "neuer Monat"
            Expect.equal model.MonthGeneration (framed.MonthGeneration + 1) "ältere Antworten zählen nicht"
        }
        // Bug 4 aus budgeting-p4n6r: `beginLoad` zählte die Generation nicht hoch, eine späte
        // Monatsantwort setzte den Monat mitten im Wiederholen.
        test "Beim Wiederholen wird eine späte Monatsantwort verworfen" {
            let loading = beginLoad framed
            let late = applyLoaded framed.MonthGeneration (Ok monthK7wmd) loading
            Expect.equal late.Month Loading "die alte Antwort zählt nicht"
        }
        test "Ein abgebrochener Aufruf liest den Rahmen nur nach, wenn der gezeigte Monat der letzte ist" {
            Expect.isFalse (frameDueAfterLostCall framed) "Oktober ist nicht der letzte"
            Expect.isTrue (frameDueAfterLostCall { framed with ChosenMonth = Some "2026-11-01" }) "November schon"
        }
        // Bug 5 aus budgeting-p4n6r: Der gescheiterte Aufruf kann YNAB erreicht haben, das Fenster
        // blieb trotzdem alt.
        test "Scheitert erneutes Senden, zählt wie bei einem abgebrochenen Aufruf der gezeigte Monat" {
            Expect.isFalse (frameDueAfterRetry (Error "weg") framed) "nicht der letzte Monat"
            Expect.isTrue (frameDueAfterRetry (Error "weg") { framed with ChosenMonth = Some "2026-11-01" }) "der letzte"
        }
        // Punkt 21: Der Client-Teil der Regel aus ADR 0076 Punkt 6, der Rest liegt in `windowMayHaveGrown`.
        test "Ein Bericht mit geschriebener Zeile im letzten Monat macht den Rahmen fällig" {
            let line : ApplyLineResult =
                { CategoryId = categoryIdForFrame; CategoryName = "x"; OldAssigned = eur 0m; NewAssigned = eur 5m; Outcome = Written }
            let inMonth month : ApplyReport = { ApplicationId = "a"; Month = month; Lines = [ line ]; ActualToBeBudgeted = None }
            Expect.isTrue (frameDueAfterReport (inMonth "2026-11-01") framed) "letzter Monat"
            Expect.isFalse (frameDueAfterReport (inMonth "2026-10-01") framed) "davor"
            Expect.isTrue (frameDueAfterRetry (Ok (inMonth "2026-11-01")) framed) "auch nach erneutem Senden"
            Expect.isFalse (frameDueAfterReport (inMonth "2026-11-01") (init ())) "ohne Rahmen nichts"
        }
    ]

let private loadedTests =
    testList "Antwort einnehmen (k7wmd)" [
        test "Eine Antwort zur gewählten Wahl wird gezeigt" {
            let m = applyLoaded 0 (Ok monthK7wmd) { init () with Month = Loading }
            Expect.equal m.Month (Success monthK7wmd) "Success"
        }
        test "Ein negatives Zu verteilen bleibt negativ, ohne Sonderzustand" {
            let m = applyLoaded 0 (Ok monthK7wmd) { init () with Month = Loading }
            match m.Month with
            | Success month -> Expect.equal month.ToBeBudgeted.Amount -12.34m "negativ wie geliefert"
            | other -> failtestf "Success erwartet, war %A" other
        }
        test "Eine späte Antwort zu einem abgewählten Monat wird verworfen" {
            let shown = { init () with Month = Success monthK7wmd }
            let waiting, _ = select "2026-11-01" { shown with Frame = Success frameK7wmd }
            let m = applyLoaded shown.MonthGeneration (Ok monthK7wmd) waiting
            Expect.equal m waiting "bleibt Loading für den nächsten Monat"
        }
        // Bug F2: die Prüfung kannte nur die Monatswahl. Von zwei Reloads nach schnellen
        // Verschiebungen konnte der ältere den neueren Stand überschreiben.
        test "Von zwei Verschiebungen zählt der Reload der zweiten, auch wenn der erste später ankommt" {
            let report : ApplyReport = { ApplicationId = "a"; Month = "2026-10-01"; Lines = []; ActualToBeBudgeted = None }
            let first, _, _ = applyMoveAnswered (Ok report) { init () with Month = Success monthK7wmd }
            let second, _, _ = applyMoveAnswered (Ok report) first
            let newest = { monthK7wmd with ToBeBudgeted = eur 5m }
            let answered, _ = applyReloaded second.MonthGeneration (Ok newest) second
            let late, external = applyReloaded first.MonthGeneration (Ok monthK7wmd) answered
            Expect.equal late.Month (Success newest) "der neuere Stand bleibt"
            Expect.equal external NoOp "kein Hinweis"
        }
        test "Ein später Fehler eines älteren Reloads meldet nichts" {
            let older = requestMonth { init () with Month = Success monthK7wmd }
            let newer = requestMonth older
            let late, external = applyReloaded older.MonthGeneration (Error "Timeout") newer
            Expect.equal late newer "unverändert"
            Expect.equal external NoOp "kein Hinweis"
        }
        // Bug F2: nach dem Wechsel A, B, A stimmte die Wahl wieder, die späte Fehler-Antwort des
        // ersten A ersetzte den gezeigten Monat durch die Fehlerkarte.
        // Das stille Neu-Lesen darf den alten Monat nicht als gültig stehen lassen, sonst bliebe die
        // Seite gedimmt: die Antwort auf den Wechsel selbst ist da schon verworfen.
        test "Scheitert das Neu-Lesen während eines Wechsels, steht die Fehlerkarte da" {
            let changing, _ = select "2026-09-01" framed
            let failed, external = applyReloaded changing.MonthGeneration (Error "Timeout") changing
            Expect.equal failed.Month (Failure "Timeout") "Karte"
            Expect.equal external NoOp "kein Toast"
        }
        test "Wechsel A, B, A: eine späte Fehler-Antwort des ersten A lässt den Monat stehen" {
            let firstA, _ = select "2026-11-01" framed
            let b, _ = select "2026-10-01" firstA
            let secondA, _ = select "2026-11-01" b
            let shown = applyLoaded secondA.MonthGeneration (Ok monthK7wmd) secondA
            let late = applyLoaded firstA.MonthGeneration (Error "Timeout") shown
            Expect.equal late.Month (Success monthK7wmd) "der Monat bleibt"
        }
        test "Ein YNAB-Fehler (auch 429) wird als lesbare Meldung zu Failure" {
            let message = "YNAB rate limit exceeded. Retry after 60 seconds"
            let m = applyLoaded 0 (Error message) { init () with Month = Loading }
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
                  Available = eur 0m; Underfunded = None; Target = None; Snoozed = false }
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
      Target = None
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
            let searching = { framed with CategorySearch = "leb" }
            let switched, _ = select "2026-09-01" searching
            Expect.equal switched.CategorySearch "leb" "beim Wechsel"
            let loaded = applyLoaded switched.MonthGeneration (Ok monthK7wmd) switched
            Expect.equal loaded.CategorySearch "leb" "nach der Antwort"
        }
        test "Betreten der Seite leert die Suche" {
            let entered, how = enterPage { framed with CategorySearch = "leb" }
            Expect.equal entered.CategorySearch "" "neuer Seitenaufruf ist leer"
            Expect.equal how FreshEntry "Rahmen und Monat"
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
      Target = None
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

let private selectedOf (model: Model) = model.Selected |> Option.get
let private entriesOf (model: Model) = (selectedOf model).Entries
let private opened = selectCategory categoryId { withMonth with Frame = Success frameK7wmd }
let private onEntries = { opened with Tab = EntriesTab }

let private selectionTests =
    testList "Auswahl und Inspektor" [
        test "Eine Zeile wählt die Kategorie, die Buchungen sind noch nicht gefragt" {
            Expect.equal opened.Selected (Some { CategoryId = categoryId; Entries = NotAsked; ReturnPending = false }) "gewählt"
        }
        test "Dieselbe Kategorie noch einmal ändert nichts" {
            let loaded = { opened with Selected = Some { selectedOf opened with Entries = Success [ line -1m ] } }
            Expect.equal (selectCategory categoryId loaded) loaded "idempotent"
        }
        test "Der Inspektor kennt Kategorie und Gruppe aus dem geladenen Monat" {
            Expect.equal (selectedCategory detailMonth (selectedOf opened)) (Some (detailCategoryFixture, "Alltag")) "Beträge von YNAB"
        }
        test "Eine unbekannte Kategorie wählt nichts" {
            Expect.isNone (selectCategory (System.Guid.NewGuid()) withMonth).Selected "keine Auswahl"
        }
        test "Ohne geladenen Monat wählt nichts" {
            Expect.isNone (selectCategory categoryId (init ())).Selected "keine Auswahl"
        }
        test "Bei laufender Vorschau ändert die Wahl nichts" {
            let previewing = { withMonth with Targets = PreviewLoading }
            Expect.isNone (selectCategory categoryId previewing).Selected "Vorschau"
        }
        test "Schließen beendet die Auswahl" {
            let model, _, _ = Components.Budget.State.update CloseInspector opened
            Expect.isNone model.Selected "keine Auswahl"
        }
        test "Monatswechsel: Id, Tab und Buchungen stehen bis zur Antwort, dann sind sie wieder ungefragt" {
            let shownEntries = { onEntries with Selected = Some { selectedOf onEntries with Entries = Success [ line -1m ] } }
            let model, _ = select "2026-09-01" shownEntries
            Expect.equal (selectedOf model).CategoryId categoryId "Id"
            Expect.equal model.Tab EntriesTab "Tab"
            Expect.equal (entriesOf model) (Success [ line -1m ]) "bleiben stehen"
            Expect.isNone (entriesDue model) "nicht fällig, solange der neue Monat fehlt"
            let answered = applyLoaded model.MonthGeneration (Ok { detailMonth with Month = "2026-09-01" }) model
            Expect.equal (entriesOf answered) NotAsked "kommen neu"
            Expect.equal (entriesDue answered) (Some (categoryId, "2026-09-01")) "für den neuen Monat"
        }
        test "Monatswechsel bei laufender Abfrage: ihre Antwort fällt weg, die Buchungen kommen trotzdem" {
            let asked, _ = requestEntries onEntries
            let model, _ = select "2026-09-01" asked
            let late = applyCategoryEntries asked.EntriesGeneration (Ok [ line -1m ]) model
            Expect.equal (entriesOf late) NotAsked "die Antwort zum alten Monat zählt nicht"
            let back, _ = select "2026-10-01" late
            let answered = applyLoaded back.MonthGeneration (Ok detailMonth) back
            Expect.equal (entriesDue answered) (Some (categoryId, "2026-10-01")) "A, B, A: nicht hängen geblieben"
        }
        // Bug 1 aus budgeting-p4n6r: Überholt die Antwort auf "Erneut senden" den Monatswechsel, kommt
        // der Monat über `Reloaded`, die Antwort auf den Wechsel selbst fällt weg.
        test "Gewinnt das Neu-Lesen gegen den Wechsel, sind die Buchungen des neuen Monats fällig" {
            let shownEntries = { onEntries with Selected = Some { selectedOf onEntries with Entries = Success [ line -1m ] } }
            let changing, _ = select "2026-09-01" shownEntries
            let retried, _ = applyMoveRetried "a" (Error "weg") changing
            let reloaded, _ = applyReloaded retried.MonthGeneration (Ok { detailMonth with Month = "2026-09-01" }) retried
            Expect.equal (entriesDue reloaded) (Some (categoryId, "2026-09-01")) "fällig"
        }
        test "Fehlt die Kategorie im neuen Monat, endet die Auswahl" {
            let model = applyLoaded 0 (Ok { detailMonth with Groups = [] }) opened
            Expect.isNone model.Selected "keine Auswahl"
        }
        test "Fällt die gewählte Kategorie aus dem Filter, bleibt sie gewählt" {
            let model, _, _ = Components.Budget.State.update (SelectFilter OverdrawnOnly) opened
            Expect.equal model.Selected opened.Selected "bleibt"
            let searched, _, _ = Components.Budget.State.update (CategorySearchChanged "xyz") opened
            Expect.equal searched.Selected opened.Selected "bleibt auch bei der Suche"
        }
    ]

let private filterGroupsFixture =
    let cat name available underfunded : BudgetCategory =
        { detailCategoryFixture with
            Id = System.Guid.NewGuid()
            Name = name
            Available = eur available
            Underfunded = underfunded |> Option.map eur }
    [ { Name = "Alltag"; Categories = [ cat "Essen" -5m None; cat "Kino" 10m (Some 20m); cat "Friseur" 10m None ] }
      { Name = "Wohnen"; Categories = [ cat "Strom" 3m None ] } ]

let private stepTests =
    let a, b, c, d = System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid()
    let all = [ a; b; c; d ]
    let withFilter =
        let groups = filterGroupsFixture
        let id name = groups |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Name = name) |> fun c -> c.Id
        let month = { detailMonth with Groups = groups }
        let at name filter =
            { init () with
                Month = Success month
                Filter = filter
                Selected = Some { CategoryId = id name; Entries = NotAsked; ReturnPending = false } }
        at, id
    testList "Kategorie wechseln" [
        test "Mit aktivem Filter überspringt der Schritt ausgefilterte Zeilen, am Rand bleibt er stehen" {
            let at, id = withFilter
            let stepped = stepSelection Next (at "Essen" MissingOnly)
            Expect.equal (selectedOf stepped).CategoryId (id "Kino") "Essen, dann Kino, beide fehlt"
            let atEdge = at "Kino" MissingOnly
            Expect.equal (stepSelection Next atEdge) atEdge "Friseur und Strom sind ausgefiltert"
        }
        test "Die nächste sichtbare Zeile, sichtbar hat Vorrang vor der direkten Nachbarin" {
            Expect.equal (stepCategory Next all [ a; c ] a) (Some c) "b ist ausgefiltert"
            Expect.equal (stepCategory Previous all [ a; c ] c) (Some a) "zurück"
        }
        test "Am Rand gibt es keinen Schritt" {
            Expect.isNone (stepCategory Next all all d) "unten"
            Expect.isNone (stepCategory Previous all all a) "oben"
            Expect.isNone (stepCategory Next all [ a; b ] b) "letzte sichtbare"
        }
        test "Eine herausgefallene Kategorie läuft ab ihrer Position" {
            Expect.equal (stepCategory Next all [ a; d ] b) (Some d) "vor"
            Expect.equal (stepCategory Previous all [ a; d ] c) (Some a) "zurück"
        }
    ]

let private pageKeyTests =
    let press key focus (model: Model) =
        let pressed, _, _ =
            Components.Budget.State.update (PageKeyPressed (pageKeyActionFor model key focus)) model
        pressed
    let filtered filter =
        { init () with Month = Success { detailMonth with Groups = filterGroupsFixture }; Filter = filter }
    let idOf name =
        filterGroupsFixture |> List.collect (fun g -> g.Categories) |> List.find (fun c -> c.Name = name) |> fun c -> c.Id
    testList "Pfeiltasten am PC" [
        test "Hoch und Runter wechseln die Zeile, auch im Suchfeld" {
            for focus in [ SearchField; OtherFocus ] do
                Expect.equal (pageKeyAction true "ArrowDown" focus) (StepRow Next) "Runter"
                Expect.equal (pageKeyAction true "ArrowUp" focus) (StepRow Previous) "Hoch"
        }
        test "Links und Rechts wechseln den Tab, im Suchfeld gehören sie dem Cursor" {
            Expect.equal (pageKeyAction true "ArrowRight" OtherFocus) (StepTab Next) "Rechts"
            Expect.equal (pageKeyAction true "ArrowLeft" OtherFocus) (StepTab Previous) "Links"
            Expect.equal (pageKeyAction true "ArrowRight" SearchField) LeaveKey "Rechts im Suchfeld"
            Expect.equal (pageKeyAction true "ArrowLeft" SearchField) LeaveKey "Links im Suchfeld"
        }
        test "Ohne Auswahl wählt Runter die erste und Hoch die letzte Zeile, Links und Rechts tun nichts" {
            for focus in [ SearchField; OtherFocus ] do
                Expect.equal (pageKeyAction false "ArrowDown" focus) FirstRow "Runter"
                Expect.equal (pageKeyAction false "ArrowUp" focus) LastRow "Hoch"
            Expect.equal (pageKeyAction false "ArrowRight" OtherFocus) LeaveKey "Rechts"
            Expect.equal (pageKeyAction false "ArrowLeft" OtherFocus) LeaveKey "Links"
        }
        test "Ein Textfeld sperrt alle vier Richtungen" {
            for hasSelection in [ true; false ] do
                for key in [ "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight" ] do
                    Expect.equal (pageKeyAction hasSelection key TextField) LeaveKey key
        }
        test "Andere Tasten lässt die Seite in Ruhe" {
            Expect.equal (pageKeyAction true "Enter" OtherFocus) LeaveKey "Enter"
        }
        test "Bei offenem Dialog bekommt die Seite die Tasten nicht" {
            let withTargets = { opened with Targets = PreviewLoading }
            for key in [ "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight" ] do
                Expect.equal (pageKeyActionFor withTargets key OtherFocus) LeaveKey key
        }
        test "Ohne Dialog gilt die Regel mit der Auswahl aus dem Modell" {
            Expect.equal (pageKeyActionFor opened "ArrowDown" OtherFocus) (StepRow Next) "mit Auswahl"
            Expect.equal (pageKeyActionFor (init ()) "ArrowDown" OtherFocus) FirstRow "ohne Auswahl"
        }
        test "Mit aktivem Filter und ohne Auswahl wählt Runter die erste sichtbare Zeile" {
            // Wohnen steht vorn: ohne Filter wäre Strom die erste Zeile.
            let reversed = { detailMonth with Groups = List.rev filterGroupsFixture }
            let model = press "ArrowDown" OtherFocus { filtered MissingOnly with Month = Success reversed }
            Expect.equal (selectedOf model).CategoryId (idOf "Essen") "die erste Zeile mit Fehlbetrag"
        }
        test "Mit aktivem Filter und ohne Auswahl wählt Hoch die letzte sichtbare Zeile" {
            let model = press "ArrowUp" OtherFocus (filtered OverdrawnOnly)
            Expect.equal (selectedOf model).CategoryId (idOf "Essen") "die einzige überzogene"
            let all = press "ArrowUp" OtherFocus (filtered AllCategories)
            Expect.equal (selectedOf all).CategoryId (idOf "Strom") "die letzte der ganzen Liste"
        }
        test "Ohne sichtbare Zeile wählt die Taste nichts" {
            let model = press "ArrowDown" OtherFocus { filtered AllCategories with CategorySearch = "xyz" }
            Expect.isNone model.Selected "keine Zeile"
        }
        test "Tab Geld, dann Rechts, Rechts, Links ergibt Buchungen, Target bleibt am Rand stehen" {
            let right = press "ArrowRight" OtherFocus
            let left = press "ArrowLeft" OtherFocus
            // Buchungen schon geladen: sonst holte der Tab sie, und der Proxy wirft unter .NET.
            let start = { opened with Selected = Some { selectedOf opened with Entries = Success [] } }
            let onEntriesTab = start |> right |> right |> left
            Expect.equal onEntriesTab.Tab EntriesTab "Buchungen"
            let atEnd = start |> right |> right |> right
            Expect.equal atEnd.Tab TargetTab "Target"
            Expect.equal (right atEnd).Tab TargetTab "bleibt stehen"
            Expect.equal (left opened).Tab MoneyTab "Geld bleibt am linken Rand stehen"
        }
    ]

let private entriesDueTests =
    testList "Buchungen im Inspektor" [
        test "Fällig nur im Tab Buchungen, bei geladenem Monat und NotAsked" {
            Expect.equal (entriesDue onEntries) (Some (categoryId, "2026-10-01")) "fällig"
            Expect.isNone (entriesDue opened) "anderer Tab"
            Expect.isNone (entriesDue { onEntries with Month = Loading }) "Monat lädt"
            let requested, _ = requestEntries onEntries
            Expect.isNone (entriesDue requested) "läuft schon"
        }
        test "Die Antwort zur laufenden Abfrage füllt die Liste" {
            let model, _ = requestEntries onEntries
            let answered = applyCategoryEntries model.EntriesGeneration (Ok [ line -30m ]) model
            Expect.equal (entriesOf answered) (Success [ line -30m ]) "Liste"
        }
        test "Ein Fehler der Abfrage steht im Tab" {
            let model, _ = requestEntries onEntries
            let answered = applyCategoryEntries model.EntriesGeneration (Error "kaputt") model
            Expect.equal (entriesOf answered) (Failure "kaputt") "Fehler"
        }
        test "Eine späte Antwort einer älteren Abfrage wird verworfen" {
            let model, _ = requestEntries onEntries
            let reloaded = resetEntries model
            let answered = applyCategoryEntries model.EntriesGeneration (Ok [ line -1m ]) reloaded
            Expect.equal answered reloaded "ältere Antwort zählt nicht"
        }
        test "Eine späte Antwort ohne Auswahl wird verworfen" {
            let model, _ = requestEntries onEntries
            let closed = { model with Selected = None }
            Expect.equal (applyCategoryEntries model.EntriesGeneration (Ok []) closed) closed "bleibt zu"
        }
    ]

let private listFilterTests =
    testList "Filter über der Liste" [
        test "Fehlt plus Suche: nur Zeilen, die beides erfüllen, Gruppen ohne Treffer fallen weg" {
            Expect.equal (names (shownGroups MissingOnly "kin" filterGroupsFixture)) [ "Alltag", [ "Kino" ] ] "beides"
            Expect.equal (names (shownGroups OverdrawnOnly "" filterGroupsFixture)) [ "Alltag", [ "Essen" ] ] "rot"
        }
        test "Ein Treffer im Gruppennamen hebt den Filter nicht auf" {
            Expect.equal (names (shownGroups OverdrawnOnly "alltag" filterGroupsFixture)) [ "Alltag", [ "Essen" ] ] "Filter gilt weiter"
        }
        test "Die Zahlen bleiben die des ganzen Monats" {
            Expect.equal (filterCount MissingOnly filterGroupsFixture) 2 "fehlt"
            Expect.equal (filterCount OverdrawnOnly filterGroupsFixture) 1 "rot"
        }
        test "Der Filter bleibt beim Monatswechsel, Betreten setzt ihn zurück" {
            let red = { framed with Filter = OverdrawnOnly }
            let switched, _ = select "2026-09-01" red
            Expect.equal switched.Filter OverdrawnOnly "bleibt"
            let entered, _ = enterPage red
            Expect.equal entered.Filter AllCategories "Alle"
        }
    ]

let private monthOrderTests =
    let groupA = { Name = "A"; Categories = [ detailCategoryFixture ] }
    let other = { detailCategoryFixture with Id = System.Guid.NewGuid(); Name = "Zweite" }
    let groupB = { Name = "B"; Categories = [ other ] }
    let frame = { frameK7wmd with CategoryOrder = [ detailCategoryFixture.Id; other.Id ] }
    let model = { framed with Frame = Success frame }
    let groupNames (m: Model) =
        match m.Month with
        | Success month -> month.Groups |> List.map (fun g -> g.Name)
        | _ -> []
    testList "Reihenfolge im Client" [
        test "Zwei Monate mit verschiedener YNAB-Folge stehen beide Male gleich, auch nach Reloaded" {
            let first = { monthK7wmd with Groups = [ groupB; groupA ] }
            let second = { monthK7wmd with Groups = [ groupA; groupB ] }
            let loadedFirst = applyLoaded 0 (Ok first) model
            let loadedSecond = applyLoaded 0 (Ok second) loadedFirst
            let reloaded, _ = applyReloaded 0 (Ok first) loadedSecond
            Expect.equal (groupNames loadedFirst) [ "A"; "B" ] "erster Monat"
            Expect.equal (groupNames loadedSecond) [ "A"; "B" ] "zweiter Monat"
            Expect.equal (groupNames reloaded) [ "A"; "B" ] "nach dem Neu-Lesen"
        }
        test "Die Vorschau-Anzeige folgt dem Rang, das Modell behält die Folge des Servers" {
            let lineOf (id: System.Guid) name : TargetLine =
                { CategoryId = id; CategoryName = name; GroupName = "G"; OldAssigned = eur 0m
                  Underfunded = eur 1m; NewAssigned = eur 1m; Target = None }
            let serverOrder = [ lineOf other.Id "Zweite"; lineOf detailCategoryFixture.Id "Erste" ]
            let preview : TargetPreview =
                { Month = "2026-10-01"; Lines = serverOrder; Skipped = []
                  Total = eur 2m; ToBeBudgeted = eur 2m; ToBeBudgetedAfter = eur 0m }
            let shown = orderedPreviewLines frame.CategoryOrder preview.Lines
            Expect.equal (shown |> List.map (fun l -> l.CategoryName)) [ "Erste"; "Zweite" ] "Anzeige"
            let ready = { model with Targets = PreviewReady (preview, false) }
            let _, sent = confirmTargets ready
            Expect.equal (sent |> Option.map (fun p -> p.Lines)) (Some serverOrder) "Übernehmen sendet unverändert"
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
    let onEntriesLoaded = { onEntries with Selected = Some { selectedOf onEntries with Entries = Success [ line -1m ] } }
    testList "Sprung ins Kassenbuch und zurück" [
        test "Tipp auf eine Buchung springt, die Auswahl wartet auf die Rückkehr" {
            let model, _, external = Components.Budget.State.update (OpenEntryInLedger entryId) onEntriesLoaded
            Expect.equal external (JumpToEntry entryId) "Sprung"
            Expect.isTrue (returnPending model) "wartet"
        }
        test "Zurück stehen Auswahl, Tab, Filter und Suche wieder, kein Rahmen wird gelesen" {
            let left = leaveForEntry { onEntriesLoaded with Filter = OverdrawnOnly; CategorySearch = "leb" }
            let model, how = enterPage left
            Expect.equal how KeptPage "kein Rahmen"
            Expect.equal (selectedOf model).CategoryId categoryId "wieder gewählt"
            Expect.equal model.Tab EntriesTab "Tab"
            Expect.equal model.Filter OverdrawnOnly "kein Reset"
            Expect.equal model.CategorySearch "leb" "kein Reset"
            Expect.isFalse (returnPending model) "angekommen"
        }
        test "Ein gewöhnliches Betreten der Seite beginnt von vorn" {
            let model, how = enterPage { onEntriesLoaded with Filter = OverdrawnOnly }
            Expect.isNone model.Selected "keine Auswahl"
            Expect.equal model.Tab MoneyTab "Tab Geld"
            Expect.equal how FreshEntry "Rahmen wird gelesen"
        }
        // Bug 6 aus budgeting-p4n6r: Der gewählte Monat überlebte das Verlassen der Seite. Die Vorgabe
        // beim Betreten ist der laufende Monat.
        test "Betreten der Seite setzt den gewählten Monat zurück, die Rückkehr aus dem Kassenbuch nicht" {
            let elsewhere = { onEntriesLoaded with ChosenMonth = Some "2026-09-01" }
            let entered, _ = enterPage elsewhere
            Expect.isNone entered.ChosenMonth "laufender Monat"
            let returned, how = enterPage (leaveForEntry elsewhere)
            Expect.equal returned.ChosenMonth (Some "2026-09-01") "bleibt"
            Expect.equal how KeptPage "Rückkehr"
        }
        // F2: Nach einem Umweg über die Tab-Leiste ging das Detail wieder auf.
        test "Ist die Rückkehr hinfällig, bleibt die Auswahl beim nächsten Betreten leer" {
            let left = leaveForEntry onEntriesLoaded
            let abandoned, _, _ = Components.Budget.State.update ReturnAbandoned left
            let model, how = enterPage abandoned
            Expect.isNone model.Selected "keine Auswahl"
            Expect.equal how FreshEntry "normaler Aufruf"
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

// ---------------- Targets sichtbar (budgeting-kjsht) ----------------

let private targetOf (amount: decimal) rhythm : Target = { Goal = eur amount; Rhythm = rhythm }

let private targetCategory (target: Target) (under: decimal option) (snoozed: bool) : BudgetCategory =
    { detailCategoryFixture with
        Target = Some target
        Underfunded = under |> Option.map eur
        Snoozed = snoozed }

let private targetTextTests =
    testList "Rhythmus-Text (kjsht)" [
        for name, amount, rhythm, expected in
            [ "wöchentlich", 50m, Weeks 1, "50,00 €/Woche"
              "alle 2 Wochen", 50m, Weeks 2, "50,00 € alle 2 Wochen"
              "monatlich", 1600m, Months 1, "1.600,00 €/Monat"
              "alle 3 Monate", 120m, Months 3, "120,00 € alle 3 Monate"
              "jährlich", 330m, Years 1, "330,00 €/Jahr"
              "alle 2 Jahre", 120m, Years 2, "120,00 € alle 2 Jahre"
              "ohne Rhythmus nur der Betrag", 100m, NoRhythm, "100,00 €" ] do
            test name {
                Expect.equal (targetText (targetOf amount rhythm)) expected name
            }
    ]

let private targetStatusTests =
    let target = targetOf 1600m (Months 1)
    testList "Zusatz zum Target (kjsht)" [
        test "Fehlbetrag größer 0 zeigt fehlt, der Zusatz schweigt" {
            let c = targetCategory target (Some 1600m) false
            Expect.equal (missingText c) (Some "fehlt 1.600,00 €") "fehlt"
            Expect.equal (targetStatusText c) None "kein Zusatz"
        }
        test "Fehlbetrag 0 zeigt erfüllt" {
            Expect.equal (targetStatusText (targetCategory target (Some 0m) false)) (Some "erfüllt") "Zusatz"
        }
        test "Pausiert geht vor, auch mit Fehlbetrag" {
            let c = targetCategory target (Some 1600m) true
            Expect.equal (targetStatusText c) (Some "pausiert") "Zusatz"
            Expect.equal (missingText c) None "der Fehlbetrag zählt nicht"
        }
        test "Pausiert gilt auch ohne Fehlbetrag von YNAB" {
            Expect.equal (targetStatusText (targetCategory target None true)) (Some "pausiert") "Zusatz"
        }
        test "Ohne Fehlbetrag von YNAB gibt es keinen Zusatz" {
            Expect.equal (targetStatusText (targetCategory target None false)) None "kein Zusatz"
        }
        test "Vorschau nennt Target, Zugewiesen und Fehlbetrag" {
            let line : TargetLine =
                { CategoryId = categoryId
                  CategoryName = "Essen"
                  GroupName = "Alltag"
                  OldAssigned = eur 0m
                  Underfunded = eur 1600m
                  NewAssigned = eur 1600m
                  Target = Some target }
            Expect.equal
                (previewDetailText line)
                "Target 1.600,00 €/Monat, Zugewiesen 0,00 €, Fehlbetrag 1.600,00 €"
                "mit Target"
            Expect.equal
                (previewDetailText { line with Target = None })
                "Zugewiesen 0,00 €, Fehlbetrag 1.600,00 €"
                "ohne Target"
        }
    ]

[<Tests>]
let tests =
    testList "Budget-Seite (k7wmd)" [
        targetTextTests
        targetStatusTests
        selectTests
        frameLoadTests
        loadedTests
        labelTests
        routingTests
        filterTests
        searchStateTests
        selectionTests
        stepTests
        pageKeyTests
        entriesDueTests
        listFilterTests
        monthOrderTests
        totalTests
        returnTests
    ]
