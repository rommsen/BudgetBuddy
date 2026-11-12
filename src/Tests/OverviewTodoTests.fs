module OverviewTodoTests

// ledger-s1weh: „Zu tun“ auf der Übersicht. Welche Einträge in welcher Reihenfolge erscheinen, rechnet `todos` rein aus
// Budgetmonat, Kassenbuch-Zählern und Lena-Zahl; die Ziele laufen durch das echte Root-`update`, das Budget verbraucht
// den Eintrag beim Laden des Monats (`takeEntryAction`), „+ Buchung“ bleibt auf der Übersicht.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Overview.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private foodId = Guid.Parse "00000000-0000-0000-0000-0000000b1001"
let private drugstoreId = Guid.Parse "00000000-0000-0000-0000-0000000b1002"
let private clothesId = Guid.Parse "00000000-0000-0000-0000-0000000b1003"
let private entryId = Guid.Parse "00000000-0000-0000-0000-0000000b2001"

let private category (id: Guid) (name: string) (available: decimal) (underfunded: decimal option) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur 0m; Available = eur available
      Underfunded = underfunded |> Option.map eur; Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

let private monthOf (toBeBudgeted: decimal) (categories: BudgetCategory list) : BudgetMonth =
    { Month = "2026-11-01"; ToBeBudgeted = eur toBeBudgeted; Groups = [ { Name = "Laufend"; Categories = categories } ] }

/// Ein Monat ohne Aufgabe: nichts im Minus, kein Fehlbetrag, nichts zu verteilen.
let private quietMonth = monthOf 0m [ category clothesId "Kleidung" 143m None ]

let private notInYnab : EntryNotInYnab =
    { EntryId = entryId; AccountId = Guid.NewGuid(); Payee = Some "Zinsen"; Amount = eur 3.12m; Date = DateTime(2026, 11, 6)
      Reason = PushFailed { Reason = "YNAB antwortet nicht"; FailedAt = DateTime(2026, 11, 7) } }

let private inputs : TodoInputs =
    { Month = Some quietMonth; NeedsReview = 0; AskLena = 0; NotInYnab = []; ImportStand = Some "vor 2 Std." }

let private targetsOf (list: Todo list) = list |> List.map (fun t -> t.Target)

[<Tests>]
let todoTests =
    testList "Zu tun der Übersicht" [
        testCase "Ohne Bedingung steht nichts da" <| fun () ->
            Expect.isEmpty (todos inputs) "leer"

        testCase "1: ungeprüfte Buchungen, Zeile 2 nennt den Import, eine in der Einzahl" <| fun () ->
            match todos { inputs with NeedsReview = 4 } with
            | [ todo ] ->
                Expect.equal todo.Target TodoReview "Ziel"
                Expect.equal todo.Title "4 neue Buchungen durchgehen" "Text"
                Expect.equal todo.Detail "Import vor 2 Std." "Zeile 2"
            | other -> failtestf "ein Eintrag erwartet: %A" other
            Expect.equal (todos { inputs with NeedsReview = 1 }).Head.Title "1 neue Buchung durchgehen" "Einzahl"
            Expect.equal (todos { inputs with NeedsReview = 1; ImportStand = None }).Head.Detail "Im Kassenbuch" "ohne bekannten Import"

        testCase "2: je Kategorie im Minus ein Eintrag mit dem Minus und Geld holen" <| fun () ->
            let month = monthOf 0m [ category foodId "Lebensmittel" -24.10m None; category drugstoreId "Drogerie" -0.10m None ]
            match todos { inputs with Month = Some month } with
            | [ food; drugstore ] ->
                Expect.equal food.Target (TodoCover foodId) "Ziel Lebensmittel"
                Expect.equal food.Title "Lebensmittel ist im Minus" "Text"
                Expect.equal food.Detail "-24,10 € · Geld aus einer anderen Kategorie holen" "Zeile 2"
                Expect.equal drugstore.Target (TodoCover drugstoreId) "Ziel Drogerie"
            | other -> failtestf "zwei Einträge erwartet: %A" other

        testCase "2: Verfügbar genau null ist nicht im Minus" <| fun () ->
            let month = monthOf 0m [ category foodId "Lebensmittel" 0m None; category drugstoreId "Drogerie" -0.01m None ]
            Expect.equal (todos { inputs with Month = Some month } |> targetsOf) [ TodoCover drugstoreId ] "nur die unter null"

        testCase "3: Targets anwenden, wenn einer Kategorie Geld zum Target fehlt, Summe und 1 Kategorie" <| fun () ->
            let one = monthOf 0m [ category foodId "Lebensmittel" 10m (Some 60m); category clothesId "Kleidung" 143m (Some 0m) ]
            match todos { inputs with Month = Some one } with
            | [ todo ] ->
                Expect.equal todo.Target TodoApplyTargets "Ziel"
                Expect.equal todo.Title "Targets anwenden" "Text"
                Expect.equal todo.Detail "60,00 € für 1 Kategorie" "Einzahl"
            | other -> failtestf "ein Eintrag erwartet: %A" other
            let two = monthOf 0m [ category foodId "Lebensmittel" 10m (Some 60m); category clothesId "Kleidung" 1m (Some 40.5m) ]
            Expect.equal (todos { inputs with Month = Some two }).Head.Detail "100,50 € für 2 Kategorien" "Mehrzahl"

        testCase "3: ein pausiertes oder deaktiviertes Target zählt nicht" <| fun () ->
            let paused = { category foodId "Lebensmittel" 10m (Some 60m) with Pause = Some PausedInBb }
            let deactivated = { category clothesId "Kleidung" 10m (Some 60m) with Deactivated = Some true }
            Expect.isEmpty (todos { inputs with Month = Some (monthOf 0m [ paused; deactivated ]) }) "nichts"

        testCase "4: Rückfragen an Lena, eine in der Einzahl" <| fun () ->
            match todos { inputs with AskLena = 2 } with
            | [ todo ] ->
                Expect.equal todo.Target TodoAskLena "Ziel"
                Expect.equal todo.Title "2 Rückfragen an Lena offen" "Text"
                Expect.equal todo.Detail "Lena sieht sie über den Filter" "Zeile 2"
            | other -> failtestf "ein Eintrag erwartet: %A" other
            Expect.equal (todos { inputs with AskLena = 1 }).Head.Title "1 Rückfrage an Lena offen" "Einzahl"

        testCase "5: je Buchung nicht in YNAB ein Eintrag mit Payee und Betrag" <| fun () ->
            match todos { inputs with NotInYnab = [ notInYnab; { notInYnab with EntryId = Guid.NewGuid(); Payee = None } ] } with
            | [ todo; noPayee ] ->
                Expect.equal todo.Target (TodoNotInYnab entryId) "Ziel"
                Expect.equal todo.Title "Buchung nicht in YNAB" "Text"
                Expect.equal todo.Detail "Zinsen · 3,12 € · erneut senden" "Zeile 2"
                Expect.stringStarts noPayee.Detail "ohne Payee · " "klein wie im Kassenbuch"
            | other -> failtestf "zwei Einträge erwartet: %A" other

        testCase "6: Zu verteilen über null ohne Eintrag 3" <| fun () ->
            match todos { inputs with Month = Some (monthOf 120m [ category clothesId "Kleidung" 143m None ]) } with
            | [ todo ] ->
                Expect.equal todo.Target TodoDistribute "Ziel"
                Expect.equal todo.Title "120,00 € verteilen" "Text"
                Expect.equal todo.Detail "Noch keiner Kategorie zugewiesen" "Zeile 2"
            | other -> failtestf "ein Eintrag erwartet: %A" other
            Expect.isEmpty (todos { inputs with Month = Some (monthOf -5m [ category clothesId "Kleidung" 143m None ]) }) "unter null nicht"

        testCase "6 fehlt, solange Targets anwenden dasteht" <| fun () ->
            let month = monthOf 120m [ category foodId "Lebensmittel" 10m (Some 60m) ]
            Expect.equal (targetsOf (todos { inputs with Month = Some month })) [ TodoApplyTargets ] "nur 3"

        testCase "Die Reihenfolge ist 1, 2, 3, 4, 5, 6" <| fun () ->
            let month =
                monthOf 120m [ category foodId "Lebensmittel" -24.10m (Some 60m); category clothesId "Kleidung" 143m None ]
            let all = todos { inputs with Month = Some month; NeedsReview = 3; AskLena = 1; NotInYnab = [ notInYnab ] }
            Expect.equal (targetsOf all) [ TodoReview; TodoCover foodId; TodoApplyTargets; TodoAskLena; TodoNotInYnab entryId ] "1 bis 5"
            let withDistribute =
                todos { inputs with Month = Some (monthOf 120m [ category foodId "Lebensmittel" -1m None ]); NeedsReview = 1; AskLena = 1; NotInYnab = [ notInYnab ] }
            Expect.equal (targetsOf withDistribute) [ TodoReview; TodoCover foodId; TodoAskLena; TodoNotInYnab entryId; TodoDistribute ] "6 am Ende"

        testCase "Ohne Monat entfallen 2, 3 und 6, der Rest steht" <| fun () ->
            Expect.equal (targetsOf (todos { inputs with Month = None; NeedsReview = 1; AskLena = 1 })) [ TodoReview; TodoAskLena ] "1 und 4"

        testCase "Marken und Knöpfe: Gelb für 1, 3, 5 und 6, Rot für 2, das Lena-Flag für 4" <| fun () ->
            for target, mark, label in
                [ TodoReview, MarkAttention, "Jetzt durchgehen"
                  TodoCover foodId, MarkError, "Geld holen"
                  TodoApplyTargets, MarkAttention, "Anwenden"
                  TodoAskLena, MarkLenaFlag, "Öffnen"
                  TodoNotInYnab entryId, MarkAttention, "Öffnen"
                  TodoDistribute, MarkAttention, "Öffnen" ] do
                Expect.equal (todoMark target) mark $"Marke {target}"
                Expect.equal (todoActionLabel target) label $"Knopf {target}"
    ]

let private loaded : Model =
    { init with
        Month = Success quietMonth
        Recent = Success []
        NotInYnab = Success [] }

[<Tests>]
let todoStateTests =
    testList "Was der Block Zu tun zeigt" [
        testCase "Er wartet auf Monat, neueste Buchungen und Buchungen nicht in YNAB" <| fun () ->
            for name, model in
                [ "Monat", { loaded with Month = Loading }
                  "Neueste", { loaded with Recent = NotAsked }
                  "nicht in YNAB", { loaded with NotInYnab = Loading } ] do
                Expect.equal (todoState model) TodosLoading name

        testCase "Ohne Eintrag steht Alles erledigt, „+ Buchung“ ist dann gefüllt" <| fun () ->
            Expect.equal (todoState loaded) (TodosShown []) "leer"
            Expect.isFalse (Components.Overview.View.captureQuiet loaded) "gefüllt"

        testCase "Mit Eintrag ist „+ Buchung“ ruhig, beim Laden auch" <| fun () ->
            let withReview = { loaded with NeedsReviewCount = 1 }
            Expect.equal (todoState withReview |> function TodosShown l -> targetsOf l | _ -> []) [ TodoReview ] "Eintrag"
            Expect.isTrue (Components.Overview.View.captureQuiet withReview) "ruhig"
            Expect.isTrue (Components.Overview.View.captureQuiet { loaded with Month = Loading }) "beim Laden"

        testCase "Scheitern die Zähler, steht der Fehler statt eines falschen Alles erledigt" <| fun () ->
            Expect.equal (todoState { loaded with Recent = Failure "weg" }) (TodosFailed "weg") "Zähler"
            Expect.equal (todoState { loaded with NotInYnab = Failure "weg" }) (TodosFailed "weg") "nicht in YNAB"
            Expect.equal (todoState { loaded with Month = Failure "weg" }) (TodosFailed "weg") "Monat, sonst nichts"
            Expect.equal
                (todoState { loaded with Month = Failure "weg"; NeedsReviewCount = 2 } |> function TodosShown l -> targetsOf l | _ -> [])
                [ TodoReview ]
                "Monat fehlt, der Rest steht"

        testCase "Die Antwort der neuesten Buchungen bringt die Zähler mit, die Zahl an der Übersicht folgt" <| fun () ->
            let page : LedgerEntryPage =
                { Entries = []; TotalCount = 0; TotalAmount = eur 0m; DeletedCount = 0; NeedsReviewCount = 4; AskLenaCount = 2 }
            let answered = Components.Overview.State.applyAnswer (RecentLoaded (init.RecentRequest, Ok page)) init
            Expect.equal (answered.NeedsReviewCount, answered.AskLenaCount) (4, 2) "Zähler"
            Expect.equal (reviewCount answered) 4 "Zahl"
            Expect.equal (reviewCount init) 0 "unbekannt: keine Zahl"
            Expect.equal (Client.DesignSystem.Navigation.reviewCountLabel 4) "4 zu prüfen" "Wort zum Gelb"

        testCase "Der Zähler steht nur an der Übersicht und nur über null" <| fun () ->
            let navCount = Client.DesignSystem.Navigation.navCount
            Expect.equal (navCount 3 Client.DesignSystem.Navigation.Overview) (Some 3) "Übersicht"
            Expect.equal (navCount 1 Client.DesignSystem.Navigation.Overview) (Some 1) "eine"
            Expect.equal (navCount 0 Client.DesignSystem.Navigation.Overview) None "null: kein Zähler"
            for page in [ Client.DesignSystem.Navigation.Ledger; Client.DesignSystem.Navigation.Budget; Client.DesignSystem.Navigation.QuickAdd ] do
                Expect.equal (navCount 3 page) None (string page)

        // Review-Fund: Antworten waren nicht an ihre Anfrage gebunden. Überlappten Betreten und `CountsNeeded` (Start,
        // Import) oder `ReloadRequested` nach dem Erfassen, setzte eine langsame alte Antwort Zähler und Liste zurück.
        testCase "Eine Antwort auf eine überholte Anfrage setzt Zähler und Liste nicht zurück" <| fun () ->
            let page (needsReview: int) : LedgerEntryPage =
                { Entries = []; TotalCount = 0; TotalAmount = eur 0m; DeletedCount = 0; NeedsReviewCount = needsReview; AskLenaCount = 0 }
            let apply msg model = Components.Overview.State.applyAnswer msg model
            let entered = Components.Overview.State.beginEnter (DateTime(2026, 11, 7)) init
            let counting = Components.Overview.State.beginCounts entered
            let afterNew = apply (RecentLoaded (counting.RecentRequest, Ok (page 5))) counting
            let afterOld = apply (RecentLoaded (entered.RecentRequest, Ok (page 2))) afterNew
            Expect.equal (reviewCount afterOld) 5 "die alte Antwort nach der neuen ändert nichts"
            let failedOld = apply (RecentLoaded (entered.RecentRequest, Error "weg")) counting
            Expect.equal failedOld.Recent counting.Recent "ein altes Scheitern auch nicht"
            let again = Components.Overview.State.beginEnter (DateTime(2026, 11, 7)) entered
            let fresh = apply (NotInYnabLoaded (again.NotInYnabRequest, Ok [])) again
            let stale = apply (NotInYnabLoaded (entered.NotInYnabRequest, Ok [ notInYnab ])) fresh
            Expect.equal stale.NotInYnab (Success []) "Buchungen nicht in YNAB: die alte Liste kommt nicht zurück"
            // `CountsNeeded` lässt die Buchungen nicht in YNAB in Ruhe: deren laufende Antwort zählt weiter.
            let notInYnabAnswered = apply (NotInYnabLoaded (entered.NotInYnabRequest, Ok [ notInYnab ])) counting
            Expect.equal notInYnabAnswered.NotInYnab (Success [ notInYnab ]) "eigene Nummer je Strang"

        testCase "Der Stand des Imports: die jüngste abgeschlossene Sitzung, relativ zu jetzt" <| fun () ->
            let session (minutesAgo: float) (status: SyncSessionStatus) : SyncSession =
                let at = DateTime(2026, 11, 7, 12, 0, 0).AddMinutes(-minutesAgo)
                { Id = SyncSessionId (Guid.NewGuid()); StartedAt = at.AddMinutes(-1.0); CompletedAt = Some at; Status = status
                  Trigger = Manual; Counts = emptyFetchCounts }
            let now = DateTime(2026, 11, 7, 12, 0, 0)
            let last = lastImportAt [ session 300.0 SyncSessionStatus.Completed; session 120.0 SyncSessionStatus.Completed; session 5.0 (SyncSessionStatus.Failed "x") ]
            Expect.equal (last |> Option.map (importStandText now)) (Some "vor 2 Std.") "jüngste abgeschlossene"
            Expect.equal (lastImportAt []) None "ohne Sitzung"
            Expect.equal (importStandText now (now.AddSeconds -20.0)) "gerade eben" "Sekunden"
            Expect.equal (importStandText now (now.AddMinutes -5.0)) "vor 5 Min." "Minuten"
            Expect.equal (importStandText now (now.AddDays -1.5)) "vor 1 Tag" "ein Tag"
            Expect.equal (importStandText now (now.AddDays -3.0)) "vor 3 Tagen" "Tage"

        testCase "Die Zeile unter „Übersicht“ nennt den Stand des Imports, ohne bekannten Import fehlt sie" <| fun () ->
            Expect.equal (importLine (Some "vor 2 Std.")) (Some "Comdirect · Import vor 2 Std.") "Prototyp `homeHead`"
            Expect.equal (importLine None) None "kein Import bekannt: keine Zeile, wie Eintrag 1 nichts behauptet"
    ]

// ============================================
// Ziele der Einträge und „+ Buchung“ durch das Root-`update`
// ============================================

let private rootModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = page
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      Fetch = Components.Fetch.Types.init; QuickAddCategories = NotAsked
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

/// Das Budget beim Betreten: Rahmen da, der laufende Monat wird mit Generation 3 geholt.
let private budgetEntering : Components.Budget.Types.Model =
    { Components.Budget.Types.init () with
        Frame =
            Success
                { CurrentMonth = "2026-11-01"; Months = [ "2026-11-01" ]; CategoryOrder = [ foodId; clothesId ]
                  MonthStartHidden = [] }
        Month = Loading
        MonthGeneration = 3 }

let private budgetMonth = monthOf 0m [ category foodId "Lebensmittel" -24.10m (Some 60m); category clothesId "Kleidung" 143m None ]

let private december = "2026-12-01"

let private monthFor (month: string) : BudgetMonth = { budgetMonth with Month = month }

/// Der Rahmen mit November als laufendem Monat und Dezember als Folgemonat.
let private twoMonthFrame : RemoteData<BudgetFrame> =
    Success
        { CurrentMonth = "2026-11-01"; Months = [ "2026-11-01"; december ]; CategoryOrder = [ foodId; clothesId ]
          MonthStartHidden = [] }

/// Betritt die Budget-Seite und antwortet wie der Server: beim frischen Betreten erst der Rahmen, dann der gezeigte
/// Monat auf dem Weg, den die Abfrage nimmt (`Reloaded` beim stillen Neu-Lesen, sonst `Loaded`).
let private enterAndAnswer (budget: Components.Budget.Types.Model) : Components.Budget.Types.Model =
    let update msg model =
        let next, _, _ = Components.Budget.State.update msg model
        next
    let fresh = snd (Components.Budget.Inspector.enterPage budget) = Components.Budget.Inspector.FreshEntry
    let entered = update Components.Budget.Types.PageEntered budget
    let framed =
        match entered.Frame with
        | Success frame when fresh -> update (Components.Budget.Types.FrameLoaded (Ok frame)) entered
        | _ -> entered
    let shown = Components.Budget.Month.shownMonth framed |> Option.get
    let answer = Ok (monthFor shown)
    let quiet =
        match framed.Month with
        | Success _ -> not (Components.Budget.Month.monthChanging framed)
        | _ -> false
    let msg =
        if quiet then Components.Budget.Types.Reloaded (framed.MonthGeneration, answer)
        else Components.Budget.Types.Loaded (framed.MonthGeneration, answer)
    update msg framed

[<Tests>]
let todoTargetTests =
    testList "Ziele aus Zu tun" [
        testCase "1 öffnet das Durchgehen, der Filter des Kassenbuchs bleibt" <| fun () ->
            let start = rootModel Overview
            let model, cmd = State.update (State.OpenOverviewTodo TodoReview) start
            Expect.equal (dispatched cmd) [ State.NavigateTo Review ] "ins Durchgehen"
            Expect.equal model.Ledger.OnlyNeedsReview start.Ledger.OnlyNeedsReview "kein Filter Zu prüfen"
            Expect.equal model.Ledger.SelectedFilter start.Ledger.SelectedFilter "Konto unverändert"

        testCase "Jetzt durchgehen im Sheet Abrufen führt ins Durchgehen" <| fun () ->
            let model, cmd = State.update (State.FetchMsg Components.Fetch.Types.ReviewRequested) (rootModel Overview)
            let follow = dispatched cmd |> List.filter (function State.OpenOverviewTodo _ -> true | _ -> false)
            Expect.equal follow [ State.OpenOverviewTodo TodoReview ] "über Eintrag 1"
            let _, next = State.update follow.Head model
            Expect.equal (dispatched next) [ State.NavigateTo Review ] "ins Durchgehen"

        testCase "4 öffnet das Kassenbuch mit Filter Lena, die übrigen Filter leer" <| fun () ->
            let start = rootModel Overview
            let filtered =
                { start with
                    Ledger =
                        { start.Ledger with
                            OnlyNeedsReview = true
                            Filters = { start.Ledger.Filters with Payee = Some "Rewe" } } }
            let model, cmd = State.update (State.OpenOverviewTodo TodoAskLena) filtered
            Expect.isTrue model.Ledger.Filters.OnlyAskLena "Lena"
            Expect.isFalse model.Ledger.OnlyNeedsReview "nicht Zu prüfen"
            Expect.equal model.Ledger.Filters.Payee None "Payee-Filter leer"
            Expect.contains (dispatched cmd) (State.NavigateTo Ledger) "ins Kassenbuch"

        testCase "5 öffnet die Buchung, 6 das Budget" <| fun () ->
            let _, cmd = State.update (State.OpenOverviewTodo (TodoNotInYnab entryId)) (rootModel Overview)
            Expect.contains (dispatched cmd) (State.NavigateTo (LedgerEntry entryId)) "die Buchung"
            let _, cmd = State.update (State.OpenOverviewTodo TodoDistribute) (rootModel Overview)
            Expect.contains (dispatched cmd) (State.NavigateTo Budget) "Budget"

        testCase "2 öffnet nach dem Laden die Kategorie im Tab Geld mit Holen, vorbelegt mit dem Minus" <| fun () ->
            let model, cmd = State.update (State.OpenOverviewTodo (TodoCover foodId)) (rootModel Overview)
            Expect.contains (dispatched cmd) (State.NavigateTo Budget) "Budget"
            let budget = { model.Budget with Frame = budgetEntering.Frame; Month = Loading; MonthGeneration = 3 }
            let loaded, _, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (3, Ok budgetMonth)) budget
            Expect.equal (loaded.Selected |> Option.map (fun s -> s.CategoryId)) (Some foodId) "die Kategorie"
            Expect.equal loaded.Tab Components.Budget.Types.MoneyTab "Tab Geld"
            match loaded.TabMoney, loaded.Month with
            | Some tab, Success month ->
                Expect.equal tab.Mode Components.Budget.Types.TakeMode "Holen"
                Expect.equal (Components.Budget.Moves.transferAmount month tab) (Some 24.10m) "das Minus vorbelegt"
            | _ -> failtest "Tab Geld erwartet"
            Expect.isNone loaded.EntryAction "verbraucht"
            let again, _, _ = Components.Budget.State.update (Components.Budget.Types.Reloaded (3, Ok budgetMonth)) { loaded with Selected = None }
            Expect.isNone again.Selected "das nächste Laden wählt nichts"

        testCase "3 öffnet nach dem Laden den Dialog Targets anwenden" <| fun () ->
            let model, _ = State.update (State.OpenOverviewTodo TodoApplyTargets) (rootModel Overview)
            let budget = { model.Budget with Frame = budgetEntering.Frame; Month = Loading; MonthGeneration = 3 }
            Expect.equal budget.Targets Components.Budget.Types.TargetsClosed "vor dem Laden zu"
            let loaded, cmd, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (3, Ok budgetMonth)) budget
            Expect.notEqual loaded.Targets Components.Budget.Types.TargetsClosed "Dialog offen"
            Expect.isNonEmpty cmd "die Vorschau wird geholt"
            Expect.isNone loaded.EntryAction "verbraucht"

        testCase "Solange der Monat veraltet ist, wartet der Eintrag" <| fun () ->
            let stale =
                { budgetEntering with
                    Month = Success budgetMonth
                    Freshness = Components.Budget.Types.MonthStale
                    EntryAction = Some (Components.Budget.Types.CoverFromOverview foodId) }
            let waited, _ = Components.Budget.State.takeEntryAction stale
            Expect.isSome waited.EntryAction "wartet"
            Expect.isNone waited.Selected "noch nichts gewählt"

        // Bug (Review ledger-s1weh): stand eine Rückkehr an (Buchung aus dem Budget angetippt), nahm das Betreten den Weg
        // `KeptPage` und behielt den gewählten Folgemonat; „Targets anwenden“ aus „Zu tun“ öffnete den Dialog im Dezember.
        testCase "3 aus Zu tun öffnet den Dialog im laufenden Monat, auch wenn das Budget auf eine Rückkehr in den Dezember wartet" <| fun () ->
            let waiting =
                { budgetEntering with
                    Frame = twoMonthFrame
                    ChosenMonth = Some december
                    Month = Success (monthFor december)
                    Freshness = Components.Budget.Types.MonthRead }
                |> Components.Budget.Inspector.selectCategory foodId
                |> Components.Budget.Inspector.leaveForEntry
            let start = { rootModel Overview with Budget = waiting }
            let model, _ = State.update (State.OpenOverviewTodo TodoApplyTargets) start
            let opened = enterAndAnswer model.Budget
            Expect.equal (Components.Budget.Month.shownMonth opened) (Some "2026-11-01") "der laufende Monat"
            Expect.equal opened.Targets Components.Budget.Types.PreviewLoading "Dialog offen"
            Expect.isNone opened.EntryAction "verbraucht"

        // Bug (Review ledger-s1weh): `takeEntryAction` handelte in jedem gezeigten Monat.
        testCase "Zeigt das Budget einen anderen als den laufenden Monat, verfällt der Eintrag ohne Wirkung" <| fun () ->
            let december' =
                { budgetEntering with
                    Frame = twoMonthFrame
                    ChosenMonth = Some december
                    Month = Success (monthFor december)
                    Freshness = Components.Budget.Types.MonthRead
                    EntryAction = Some Components.Budget.Types.ApplyTargetsFromOverview }
            let taken, _ = Components.Budget.State.takeEntryAction december'
            Expect.equal taken.Targets Components.Budget.Types.TargetsClosed "kein Dialog im Dezember"
            Expect.isNone taken.EntryAction "verbraucht"

        // Bug (Review ledger-s1weh): scheiterte der Monat, blieb der Eintrag liegen und sprang beim nächsten Laden auf.
        testCase "Scheitert der Monat, verfällt der Eintrag, das nächste Laden öffnet nichts" <| fun () ->
            let entering = { budgetEntering with EntryAction = Some Components.Budget.Types.ApplyTargetsFromOverview }
            let failed, _, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (3, Error "YNAB antwortet nicht")) entering
            Expect.isNone failed.EntryAction "verbraucht"
            let retried = { failed with Month = Loading; MonthGeneration = 4 }
            let loaded, _, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (4, Ok budgetMonth)) retried
            Expect.equal loaded.Targets Components.Budget.Types.TargetsClosed "kein Dialog"

        // Bug (Review ledger-s1weh): scheiterte das stille Neu-Lesen, blieb der Eintrag liegen.
        testCase "Scheitert das stille Neu-Lesen, verfällt der Eintrag" <| fun () ->
            let quiet =
                { budgetEntering with
                    Month = Success budgetMonth
                    Freshness = Components.Budget.Types.MonthStale
                    EntryAction = Some (Components.Budget.Types.CoverFromOverview foodId) }
            let failed, _, _ = Components.Budget.State.update (Components.Budget.Types.Reloaded (3, Error "YNAB antwortet nicht")) quiet
            Expect.isNone failed.EntryAction "verbraucht"
            Expect.isNone failed.Selected "nichts gewählt"

        // Bug (Review ledger-s1weh): scheiterte schon der Rahmen, kam nie ein Monat, der Eintrag blieb liegen.
        testCase "Scheitert der Rahmen, verfällt der Eintrag" <| fun () ->
            let entering =
                { budgetEntering with Frame = Loading; EntryAction = Some Components.Budget.Types.ApplyTargetsFromOverview }
            let failed, _, _ = Components.Budget.State.update (Components.Budget.Types.FrameLoaded (Error "YNAB antwortet nicht")) entering
            Expect.isNone failed.EntryAction "verbraucht"

        // Bug (Review ledger-s1weh): verließ Roman die Seite vor der Antwort, sprang der Eintrag beim nächsten Betreten auf.
        testCase "Wer die Seite vor der Antwort verlässt, findet den Eintrag beim nächsten Betreten nicht mehr" <| fun () ->
            let entering = { budgetEntering with EntryAction = Some Components.Budget.Types.ApplyTargetsFromOverview }
            let left, _, _ = Components.Budget.State.update Components.Budget.Types.PageLeft entering
            Expect.isNone left.EntryAction "gelöscht"

        // Bug (Review ledger-s1weh): eine Antwort, die vor dem Tipp auf „Zu tun“ angefragt war, verbrauchte den Eintrag.
        testCase "Eine vor dem Tipp angefragte Antwort verbraucht den Eintrag nicht, eine verworfene auch nicht" <| fun () ->
            let model, _ = State.update (State.OpenOverviewTodo TodoApplyTargets) { rootModel Overview with Budget = budgetEntering }
            let older, _, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (3, Ok budgetMonth)) model.Budget
            Expect.equal older.Targets Components.Budget.Types.TargetsClosed "kein Dialog aus der alten Antwort"
            Expect.isSome older.EntryAction "wartet"
            let discarded, _, _ =
                Components.Budget.State.update (Components.Budget.Types.Loaded (older.MonthGeneration - 1, Error "alt")) older
            Expect.isSome discarded.EntryAction "wartet weiter"

        // Gewinnt `Reloaded` den Lauf gegen `Loaded`, ist der `Reloaded`-Zweig die einzige Stelle, die den Eintrag nimmt.
        testCase "2 öffnet die Kategorie auch, wenn Reloaded vor Loaded ankommt" <| fun () ->
            let entering = { budgetEntering with EntryAction = Some (Components.Budget.Types.CoverFromOverview foodId) }
            let loaded, _, _ = Components.Budget.State.update (Components.Budget.Types.Reloaded (3, Ok budgetMonth)) entering
            Expect.equal (loaded.Selected |> Option.map (fun s -> s.CategoryId)) (Some foodId) "die Kategorie"
            Expect.equal (loaded.TabMoney |> Option.map (fun t -> t.Mode)) (Some Components.Budget.Types.TakeMode) "Holen"
            Expect.isNone loaded.EntryAction "verbraucht"

        testCase "3 öffnet den Dialog auch, wenn Reloaded vor Loaded ankommt" <| fun () ->
            let entering = { budgetEntering with EntryAction = Some Components.Budget.Types.ApplyTargetsFromOverview }
            let loaded, cmd, _ = Components.Budget.State.update (Components.Budget.Types.Reloaded (3, Ok budgetMonth)) entering
            Expect.equal loaded.Targets Components.Budget.Types.PreviewLoading "Dialog offen"
            Expect.isNonEmpty cmd "die Vorschau wird geholt"
            Expect.isNone loaded.EntryAction "verbraucht"
    ]

[<Tests>]
let captureTests =
    testList "+ Buchung auf der Übersicht" [
        testCase "Öffnet das Formular des Kassenbuchs und lädt Konten und Stände, die noch fehlen" <| fun () ->
            let model, cmd = State.update State.CaptureFromOverview (rootModel Overview)
            let messages = dispatched cmd
            Expect.isSome model.Ledger.Capture "Formular offen"
            Expect.contains messages (State.LedgerMsg Components.Ledger.Types.LoadAccounts) "Konten laden"
            Expect.contains messages (State.LedgerMsg Components.Ledger.Types.LoadBalances) "Stände laden"
            Expect.equal model.CurrentPage Overview "bleibt auf der Übersicht"
            Expect.isFalse (messages |> List.exists (function State.NavigateTo _ -> true | _ -> false)) "kein Seitenwechsel"
            let loaded = { rootModel Overview with Ledger = { (rootModel Overview).Ledger with Accounts = Success { Accounts = []; DefaultAccountId = None } } }
            let _, again = State.update State.CaptureFromOverview loaded
            Expect.isFalse (dispatched again |> List.contains (State.LedgerMsg Components.Ledger.Types.LoadAccounts)) "geladene Konten nicht noch einmal"

        testCase "Nach dem Anlegen bleibt man auf der Übersicht, auch am PC; die Rückmeldung nennt Konto und Betrag" <| fun () ->
            let opened, _ = State.update State.CaptureFromOverview (rootModel Overview)
            let capture = opened.Ledger.Capture.Value
            let filled =
                { opened with
                    Ledger =
                        { opened.Ledger with
                            DetailForm = Components.Ledger.Types.DetailPanel
                            Capture = Some { capture with Form = { capture.Form with AmountText = "8,60"; AccountId = Some (Guid.NewGuid()) } } } }
            let created : LedgerBookingCreated = { EntryId = entryId; AccountName = "Bar"; PushFailure = None }
            let model, cmd = State.update (State.LedgerMsg (Components.Ledger.Types.CaptureSubmitted (Ok created))) filled
            let messages = dispatched cmd
            Expect.isNone model.Ledger.Capture "Formular zu"
            Expect.isFalse
                (messages |> List.contains (State.LedgerMsg (Components.Ledger.Types.OpenRouteEntry entryId)))
                "kein Detail der neuen Buchung"
            Expect.contains messages (State.OverviewMsg ReloadRequested) "Zu tun zieht nach"
            let toast =
                messages
                |> List.tryPick (function
                    | State.ShowToastWithAction (text, _, _) -> Some text
                    | _ -> None)
            Expect.equal toast (Some "Buchung angelegt · Bar · -8,60 €") "Konto und Betrag"

        // Bug (Review ledger-s1weh): das Formular trug beim Öffnen fest, ob nach dem Anlegen das Detail kommt. Am PC im
        // Kassenbuch begonnen und auf der Übersicht gespeichert, lud es das Detail im Kassenbuch; aus der Übersicht
        // begonnen und im Kassenbuch gespeichert, fehlte es. Es entscheidet die Seite, auf der gespeichert wird.
        testCase "Nach dem Anlegen entscheidet die Seite, auf der gespeichert wird, ob das Detail der Buchung kommt" <| fun () ->
            let created : LedgerBookingCreated = { EntryId = entryId; AccountName = "Bar"; PushFailure = None }
            let savedOn (opening: State.Model -> State.Model) (page: Page) =
                let opened = opening (rootModel page)
                let capture = opened.Ledger.Capture.Value
                let filled =
                    { opened with
                        CurrentPage = page
                        Ledger =
                            { opened.Ledger with
                                DetailForm = Components.Ledger.Types.DetailPanel
                                Capture = Some { capture with Form = { capture.Form with AmountText = "8,60"; AccountId = Some (Guid.NewGuid()) } } } }
                let _, cmd = State.update (State.LedgerMsg (Components.Ledger.Types.CaptureSubmitted (Ok created))) filled
                dispatched cmd |> List.contains (State.LedgerMsg (Components.Ledger.Types.OpenRouteEntry entryId))
            let inLedger (model: State.Model) =
                fst (State.update (State.LedgerMsg Components.Ledger.Types.OpenCapture) { model with CurrentPage = Ledger })
            let fromOverview (model: State.Model) =
                fst (State.update State.CaptureFromOverview { model with CurrentPage = Overview })
            Expect.isFalse (savedOn inLedger Overview) "im Kassenbuch begonnen, auf der Übersicht gespeichert: kein Detail"
            Expect.isTrue (savedOn fromOverview Ledger) "auf der Übersicht begonnen, im Kassenbuch gespeichert: Detail"
            Expect.isTrue (savedOn inLedger Ledger) "im Kassenbuch begonnen und gespeichert: Detail"
    ]
