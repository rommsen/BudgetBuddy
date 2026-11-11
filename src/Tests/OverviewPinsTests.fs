module OverviewPinsTests

// budgeting-w9yt4: angepinnte Kategorien auf der Übersicht. Die Rechnung einer Zeile (Anteil, ausgegeben, von), welche
// Pins erscheinen (deaktivierte fehlen, kommen an ihrem Platz zurück), der Zustand des Blocks, Anpinnen, Lösen und Ordnen
// samt Rücksprung bei einem Speicherfehler, das Sheet mit Suche und Markierung, und der Weg ins Budget.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Overview.Types
open Components.Overview.State

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private food = Guid.Parse "00000000-0000-0000-0000-0000000c1001"
let private restaurant = Guid.Parse "00000000-0000-0000-0000-0000000c1002"
let private kids = Guid.Parse "00000000-0000-0000-0000-0000000c1003"
let private drugstore = Guid.Parse "00000000-0000-0000-0000-0000000c1004"
let private car = Guid.Parse "00000000-0000-0000-0000-0000000c1005"

let private category (id: Guid) (name: string) (activity: decimal) (available: decimal) : BudgetCategory =
    { Id = id; Name = name; Assigned = eur 0m; Activity = eur activity; Available = eur available
      Underfunded = None; Target = None; Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

let private month (groups: (string * BudgetCategory list) list) : BudgetMonth =
    { Month = "2026-11-01"; ToBeBudgeted = eur 0m; Groups = groups |> List.map (fun (n, cs) -> { Name = n; Categories = cs }) }

let private november =
    month
        [ "Alltag", [ category food "Lebensmittel" -747.90m 652.10m; category drugstore "Drogerie" -80.10m -0.10m ]
          "Kinder", [ category kids "Kinderkram" -52.90m 109.50m ]
          "Mobilität", [ category car "Auto" -64.20m 95.80m ]
          "Freizeit", [ category restaurant "Restaurant" -35.50m 24.50m ] ]

let private deactivate (id: Guid) (m: BudgetMonth) : BudgetMonth =
    { m with
        Groups =
            m.Groups
            |> List.map (fun g ->
                { g with Categories = g.Categories |> List.map (fun c -> if c.Id = id then { c with Deactivated = Some true } else c) }) }

let private loaded (pins: Guid list) : Model =
    { init with Month = Success november; Pins = Success pins; PinsConfirmed = pins }

let private step msg model = pinStep msg model

[<Tests>]
let spendTests =
    testList "Angepinnt: Anteil, ausgegeben und von" [
        testCase "Ausgegeben ist die Aktivität mit umgekehrtem Vorzeichen, von ist Verfügbar minus Aktivität" <| fun () ->
            let spend = pinSpend -747.90m 652.10m
            Expect.equal spend.Spent 747.90m "ausgegeben"
            Expect.equal spend.Pool 1400m "von"
            Expect.floatClose Accuracy.medium spend.Share (747.90 / 1400.0) "Anteil"
            Expect.equal (pinSpendText "EUR" spend) "747,90 € von 1.400,00 € ausgegeben" "Zeile 3"

        testCase "Eine Erstattung mindert das Ausgegebene, mehr Erstattung als Ausgaben ergibt 0 %" <| fun () ->
            // 100 € ausgegeben, 30 € erstattet: Aktivität -70.
            let refunded = pinSpend -70m 130m
            Expect.equal refunded.Spent 70m "gemindert"
            Expect.floatClose Accuracy.medium refunded.Share 0.35 "Anteil"
            let surplus = pinSpend 20m 220m
            Expect.equal surplus.Spent -20m "Erstattung über den Ausgaben"
            Expect.equal surplus.Share 0.0 "nie unter 0 %"

        testCase "Aktivität 0 ergibt 0 %" <| fun () ->
            Expect.equal (pinSpend 0m 50m).Share 0.0 "leer"
            Expect.equal (pinSpend 0m 0m).Share 0.0 "auch ohne Geld"

        testCase "Mehr ausgegeben als da war wird 100 %" <| fun () ->
            // 100 € standen zur Verfügung, 150 € ausgegeben: Verfügbar -50.
            Expect.equal (pinSpend -150m -50m).Share 1.0 "überzogen ist voll"

        testCase "Von höchstens null mit Ausgaben ergibt 100 %" <| fun () ->
            let spend = pinSpend -20m -20m
            Expect.equal spend.Pool 0m "von null"
            Expect.equal spend.Share 1.0 "voll"
            Expect.equal (pinSpend -20m -25m).Share 1.0 "von unter null"
    ]

[<Tests>]
let linesTests =
    testList "Angepinnt: welche Zeilen" [
        testCase "Die Zeilen stehen in der gespeicherten Folge mit Werten aus dem laufenden Monat" <| fun () ->
            let lines = pinnedLines [ food; restaurant; kids ] november
            Expect.equal (lines |> List.map (fun l -> l.Category.Name)) [ "Lebensmittel"; "Restaurant"; "Kinderkram" ] "Folge"
            Expect.equal lines.Head.Category.Available (eur 652.10m) "Verfügbar"
            Expect.equal lines.Head.Spend.Spent 747.90m "ausgegeben"

        testCase "Fehlbetrag zum Target: fehlt steht da, nicht bei pausiertem Target und nicht bei null" <| fun () ->
            let withGap =
                { november with
                    Groups =
                        november.Groups
                        |> List.map (fun g ->
                            { g with
                                Categories =
                                    g.Categories
                                    |> List.map (fun c ->
                                        if c.Id = restaurant then { c with Underfunded = Some (eur 60m) }
                                        elif c.Id = kids then
                                            { c with
                                                Underfunded = Some (eur 40m)
                                                Pause = Some PausedInBb }
                                        elif c.Id = food then { c with Underfunded = Some (eur 0m) }
                                        else c) }) }
            let lines = pinnedLines [ food; restaurant; kids ] withGap
            Expect.equal (lines |> List.map (fun l -> l.Missing)) [ None; Some (eur 60m); None ] "nur Restaurant"

        testCase "Deaktiviert fehlt, nach dem Reaktivieren steht sie wieder an ihrem Platz" <| fun () ->
            let pins = [ food; restaurant; kids ]
            let names m = pinnedLines pins m |> List.map (fun l -> l.Category.Name)
            Expect.equal (names (deactivate restaurant november)) [ "Lebensmittel"; "Kinderkram" ] "deaktiviert fehlt"
            Expect.equal (names november) [ "Lebensmittel"; "Restaurant"; "Kinderkram" ] "wieder an ihrem Platz"

        testCase "Eine in YNAB versteckte oder gelöschte Kategorie (nicht im Monat) fehlt" <| fun () ->
            let gone = Guid.NewGuid()
            Expect.equal (pinnedLines [ gone; kids ] november |> List.map (fun l -> l.Category.Id)) [ kids ] "nur bekannte"
    ]

[<Tests>]
let blockStateTests =
    testList "Angepinnt: Zustand des Blocks" [
        testCase "Ohne Pins die Leer-Karte und kein Bearbeiten" <| fun () ->
            let state = pinBlockState (loaded [])
            Expect.equal state PinsEmpty "leer"
            Expect.isFalse (pinEditVisible state) "kein Bearbeiten"

        testCase "Nur deaktivierte Pins: ebenfalls leer" <| fun () ->
            Expect.equal (pinBlockState { loaded [ restaurant ] with Month = Success (deactivate restaurant november) }) PinsEmpty "leer"

        testCase "Mit Pins die Zeilen und Bearbeiten" <| fun () ->
            match pinBlockState (loaded [ food ]) with
            | PinsShown [ line ] as state ->
                Expect.equal line.Category.Id food "Zeile"
                Expect.isTrue (pinEditVisible state) "Bearbeiten"
            | other -> failtestf "eine Zeile erwartet: %A" other

        testCase "Beim Laden ein Skeleton mit so vielen Zeilen wie gespeicherte Pins" <| fun () ->
            Expect.equal (pinBlockState { init with Month = Loading; Pins = Loading }) (PinsLoading 1) "Pins laden"
            Expect.equal (pinBlockState { init with Month = Loading; Pins = Success [ food; kids ] }) (PinsLoading 2) "Monat lädt"

        testCase "Ladefehler der Pins oder des Monats: Fehler im Block" <| fun () ->
            Expect.equal (pinBlockState { loaded [] with Pins = Failure "kaputt" }) PinsFailed "Pins"
            Expect.equal (pinBlockState { loaded [ food ] with Month = Failure "YNAB" }) PinsFailed "Monat"

        testCase "Ein Ladefehler nach geladenen Pins behält den Stand" <| fun () ->
            let model, _ = step (PinsLoaded (0, Error "kaputt")) (loaded [ food ])
            Expect.equal model.Pins (Success [ food ]) "bleibt"
    ]

[<Tests>]
let pinChangeTests =
    testList "Angepinnt: anpinnen, lösen, ordnen" [
        testCase "Anpinnen hängt ans Ende und speichert die ganze Liste" <| fun () ->
            let model, effect = step (PinToggled kids) (loaded [ food; restaurant ])
            Expect.equal model.Pins (Success [ food; restaurant; kids ]) "am Ende"
            Expect.equal effect (SavePins [ food; restaurant; kids ]) "gespeichert wird alles"

        testCase "Lösen nimmt heraus" <| fun () ->
            let model, effect = step (PinToggled restaurant) (loaded [ food; restaurant; kids ])
            Expect.equal model.Pins (Success [ food; kids ]) "gelöst"
            Expect.equal effect (SavePins [ food; kids ]) "gespeichert"

        testCase "Ohne geladene Pins geschieht nichts: die gespeicherte Liste bleibt unberührt" <| fun () ->
            let model, effect = step (PinToggled kids) { loaded [] with Pins = Failure "kaputt" }
            Expect.equal effect NoPinEffect "kein Speichern"
            Expect.equal model.Pins (Failure "kaputt") "unverändert"

        testCase "Nach oben tauscht mit dem gezeigten davor, ein deaktivierter dazwischen bleibt an seinem Platz" <| fun () ->
            let start = { loaded [ food; drugstore; kids ] with Month = Success (deactivate drugstore november) }
            let model, _ = step (PinMovedUp kids) start
            Expect.equal model.Pins (Success [ kids; drugstore; food ]) "über den ausgeblendeten hinweg"

        testCase "Der erste gezeigte bleibt oben, ohne Speichern" <| fun () ->
            let model, effect = step (PinMovedUp food) (loaded [ food; kids ])
            Expect.equal model.Pins (Success [ food; kids ]) "bleibt"
            Expect.equal effect NoPinEffect "nichts zu speichern"

        testCase "Rückmeldung des Pin-Knopfs" <| fun () ->
            Expect.equal (pinToggleNotice "Restaurant" false) "Restaurant angepinnt" "anpinnen"
            Expect.equal (pinToggleNotice "Restaurant" true) "Restaurant gelöst" "lösen"
            Expect.isTrue (isPinned (loaded [ restaurant ]) restaurant) "angepinnt"
            Expect.isFalse (isPinned { loaded [ restaurant ] with Pins = Loading } restaurant) "ohne Stand nie"
    ]

/// Ein fester Ablauf aus Umschaltern und Antworten auf das Speichern.
type private SaveStep =
    | Toggle of Guid
    | Answer of ok: bool

/// Zwei Umschalter schnell hintereinander und die Antworten auf das Speichern in jeder Lage und mit jedem Ausgang
/// (Doktrin Punkt 20, budgeting-w9yt4 Review: vorher sprang ein Scheitern auf einen nie gespeicherten Stand zurück).
/// Orakel ist ein Server, der jede angekommene Liste in ihrer Folge übernimmt, unabhängig vom getesteten Code: es läuft
/// höchstens ein Speichern zugleich, jedes trägt die ganze angezeigte Liste, am Ende zeigt die Übersicht, was der
/// Server hat, und ein Scheitern meldet sich genau dann, wenn seit dem Senden kein Umschalter kam.
[<Tests>]
let saveOrderTests =
    testList "Angepinnt: Antworten auf das Speichern in jeder Reihenfolge" [
        let scripts =
            [ for firstOk in [ true; false ] do
                  for secondOk in [ true; false ] do
                      // Der zweite Umschalter vor oder nach der ersten Antwort.
                      yield [ Toggle kids; Toggle restaurant; Answer firstOk; Answer secondOk ]
                      yield [ Toggle kids; Answer firstOk; Toggle restaurant; Answer secondOk ] ]
        let describe (script: SaveStep list) =
            script
            |> List.map (function
                | Toggle id when id = kids -> "Kinderkram umschalten"
                | Toggle _ -> "Restaurant umschalten"
                | Answer true -> "gespeichert"
                | Answer false -> "gescheitert")
            |> String.concat ", "
        for script in scripts do
            testCase (describe script) <| fun () ->
                let mutable model = loaded [ food ]
                let mutable server = [ food ]
                let mutable inFlight : (Guid list * bool) option = None // gesendete Liste, kam seither ein Umschalter
                let notices = ResizeArray<string>()
                let expectedNotices = ResizeArray<string>()
                let send effect =
                    match effect with
                    | SavePins pins ->
                        Expect.isNone inFlight "höchstens ein Speichern zugleich"
                        Expect.equal (Success pins) model.Pins "gesendet wird die angezeigte Liste"
                        inFlight <- Some (pins, false)
                    | NoPinEffect -> ()
                    | other -> failtestf "unerwartet: %A" other
                let run msg =
                    pinSaveNotice msg model |> Option.iter notices.Add
                    let next, effect = step msg model
                    model <- next
                    send effect
                let rec drain (remaining: SaveStep list) =
                    match remaining with
                    | [] -> ()
                    | Toggle id :: rest ->
                        inFlight <- inFlight |> Option.map (fun (pins, _) -> pins, true)
                        run (PinToggled id)
                        drain rest
                    | Answer ok :: rest ->
                        match inFlight with
                        | None -> drain rest
                        | Some (pins, toggledSince) ->
                            inFlight <- None
                            if ok then server <- pins
                            elif not toggledSince then expectedNotices.Add "Nicht gespeichert: Platte voll"
                            run (PinsSaved (if ok then Ok () else Error "Platte voll"))
                            drain rest
                drain script
                // Was nach dem Ablauf noch gesendet wurde, beantwortet der Server mit Erfolg.
                while inFlight.IsSome do
                    drain [ Answer true ]
                Expect.equal model.Pins (Success server) "die Übersicht zeigt, was gespeichert ist"
                Expect.equal (List.ofSeq notices) (List.ofSeq expectedNotices) "Toast nur, wenn die Anzeige zurückspringt"
    ]

/// budgeting-w9yt4 Review: eine späte Antwort auf das Lesen (etwa nach jedem Abruf über ReloadRequested) überschrieb
/// einen Pin, den Roman inzwischen umgeschaltet hatte; der nächste Umschalter speicherte dann die alte Liste ganz.
[<Tests>]
let lateReadTests =
    testList "Angepinnt: späte Antworten auf das Lesen" [
        testCase "Lesen startet, Roman schaltet um, die alte Antwort kommt an: der Umschalter bleibt" <| fun () ->
            let reading = beginEnter (DateTime(2026, 11, 11)) (loaded [ food ])
            let toggled, _ = step (PinToggled kids) reading
            let late, _ = step (PinsLoaded (reading.PinsRead, Ok [ food ])) toggled
            Expect.equal late.Pins (Success [ food; kids ]) "der Umschalter bleibt"

        testCase "Lesen startet während eines Speicherns: die Antwort zählt nicht" <| fun () ->
            let saving, _ = step (PinToggled kids) (loaded [ food ])
            let reading = beginPins saving
            let late, _ = step (PinsLoaded (reading.PinsRead, Ok [ food ])) reading
            Expect.equal late.Pins (Success [ food; kids ]) "die gesendete Liste bleibt"

        testCase "Ohne Umschalter gilt die Antwort des jüngsten Lesens, eine ältere nicht" <| fun () ->
            let first = beginPins (loaded [ food ])
            let second = beginPins first
            let stale, _ = step (PinsLoaded (first.PinsRead, Ok [ car ])) second
            Expect.equal stale.Pins (Success [ food ]) "überholt"
            let fresh, _ = step (PinsLoaded (second.PinsRead, Ok [ kids ])) stale
            Expect.equal fresh.Pins (Success [ kids ]) "jüngstes Lesen"
            let saving, _ = step (PinToggled kids) fresh
            let failed, _ = step (PinsSaved (Error "Platte voll")) saving
            Expect.equal failed.Pins (Success [ kids ]) "der gelesene Stand ist der bestätigte"
    ]

[<Tests>]
let sheetTests =
    testList "Angepinnt: das Sheet" [
        testCase "Öffnen setzt Suche und Markierung zurück und holt BBs Reihenfolge einmal" <| fun () ->
            let opened, effect = step PinSheetOpened (loaded [ food ])
            Expect.equal opened.PinSheet (Some { Query = ""; Marked = 0 }) "offen"
            Expect.equal effect LoadCategoryOrder "Reihenfolge holen"
            let withOrder, _ =
                step
                    (CategoryOrderLoaded (Ok { CurrentMonth = "2026-11-01"; Months = []; CategoryOrder = [ drugstore; food ]; MonthStartHidden = [] }))
                    opened
            let _, again = step PinSheetOpened (fst (step PinSheetClosed withOrder))
            Expect.equal again NoPinEffect "schon bekannt"
            Expect.equal
                (pinSheetRows withOrder.CategoryOrder "" november |> List.take 2 |> List.map (fun c -> c.Name))
                [ "Drogerie"; "Lebensmittel" ]
                "BBs Reihenfolge in der Gruppe"

        testCase "Ohne Suchbegriff steht der Abschnitt Angepinnt, mit Suchbegriff nicht und die Liste filtert" <| fun () ->
            Expect.equal (pinSheetPinnedSection "" [ restaurant; food ] november |> List.map (fun c -> c.Id)) [ restaurant; food ] "Folge"
            Expect.isEmpty (pinSheetPinnedSection "le" [ restaurant; food ] november) "mit Suche weg"
            Expect.equal
                (pinSheetGroups None "KIND" november |> List.map (fun (g, cs) -> g, cs |> List.map (fun c -> c.Name)))
                [ "Kinder", [ "Kinderkram" ] ]
                "nur passende, ohne Groß und Klein"

        testCase "Deaktivierte stehen weder in der Liste noch im Abschnitt" <| fun () ->
            let m = deactivate drugstore november
            Expect.isFalse (pinSheetRows None "" m |> List.exists (fun c -> c.Id = drugstore)) "Liste"
            Expect.isEmpty (pinSheetPinnedSection "" [ drugstore ] m) "Abschnitt"

        testCase "Pfeile bewegen die Markierung in den Grenzen, Enter schaltet die markierte um" <| fun () ->
            let opened, _ = step PinSheetOpened (loaded [])
            let up, _ = step (PinMarkMoved -1) opened
            Expect.equal (up.PinSheet |> Option.map (fun s -> s.Marked)) (Some 0) "nicht über den Anfang"
            let down = [ 1 .. 10 ] |> List.fold (fun m _ -> fst (step (PinMarkMoved 1) m)) opened
            Expect.equal (down.PinSheet |> Option.map (fun s -> s.Marked)) (Some 4) "nicht über das Ende (5 Zeilen)"
            let second, _ = step (PinMarkMoved -3) down
            let toggled, effect = step PinMarkedToggled second
            Expect.equal toggled.Pins (Success [ drugstore ]) "die zweite Zeile: Drogerie"
            match effect with
            | SavePins _ -> ()
            | other -> failtestf "Speichern erwartet: %A" other
            Expect.equal (toggled.PinSheet |> Option.map (fun s -> s.Marked)) (Some 1) "die Markierung bleibt"

        testCase "Die Suche setzt die Markierung auf die erste Zeile" <| fun () ->
            let opened, _ = step PinSheetOpened (loaded [])
            let moved, _ = step (PinMarkMoved 2) opened
            let searched, _ = step (PinSearchChanged "au") moved
            Expect.equal searched.PinSheet (Some { Query = "au"; Marked = 0 }) "zurückgesetzt"
            let toggled, _ = step PinMarkedToggled searched
            Expect.equal toggled.Pins (Success [ car ]) "Auto ist die einzige Zeile"

        testCase "Schließen und neues Betreten schließen das Sheet" <| fun () ->
            let opened, _ = step PinSheetOpened (loaded [])
            Expect.isNone (fst (step PinSheetClosed opened)).PinSheet "Fertig, Esc, Schließen"
            Expect.isNone (beginEnter (DateTime(2026, 11, 11)) opened).PinSheet "Betreten"

        // budgeting-w9yt4 Review: „Erneut versuchen“ im Sheet, wenn der Monat fehlt, lädt neu und lässt das Sheet offen.
        testCase "Erneut laden lässt ein offenes Sheet offen und lädt den Monat neu" <| fun () ->
            let opened, _ = step PinSheetOpened { loaded [] with Month = Failure "YNAB"; Today = DateTime(2026, 11, 11) }
            let reloading = beginReload opened
            Expect.equal reloading.PinSheet opened.PinSheet "bleibt offen"
            Expect.equal reloading.Month Loading "Monat lädt"
            Expect.equal reloading.Today opened.Today "derselbe Tag"
    ]

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

/// Nur die Toasts eines Cmds. Das Speichern darin läuft im Hintergrund an und scheitert unter .NET am
/// Remoting-Proxy; seine Antwort kommt später und zählt hier nicht.
let private toastsOf (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let collected = System.Collections.Concurrent.ConcurrentQueue<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Enqueue)
    collected.ToArray()
    |> Array.filter (function State.ShowToast _ -> true | _ -> false)
    |> List.ofArray

[<Tests>]
let openPinnedTests =
    testList "Angepinnt: eine Zeile öffnet das Budget" [
        testCase "Die Zeile öffnet nach dem Laden die Kategorie im Tab Geld des laufenden Monats" <| fun () ->
            let model, cmd = State.update (State.OpenPinnedCategory restaurant) (rootModel Overview)
            Expect.contains (dispatched cmd) (State.NavigateTo Budget) "Budget"
            let budget =
                { model.Budget with
                    Frame =
                        Success
                            { CurrentMonth = "2026-11-01"; Months = [ "2026-11-01" ]; CategoryOrder = [ food; restaurant ]
                              MonthStartHidden = [] }
                    Month = Loading
                    MonthGeneration = 3 }
            let opened, _, _ = Components.Budget.State.update (Components.Budget.Types.Loaded (3, Ok november)) budget
            Expect.equal (opened.Selected |> Option.map (fun s -> s.CategoryId)) (Some restaurant) "die Kategorie"
            Expect.equal opened.Tab Components.Budget.Types.MoneyTab "Tab Geld"
            match opened.TabMoney with
            | Some tab -> Expect.equal tab.Mode Components.Budget.Types.AssignMode "Zuweisen bei Verfügbar über null"
            | None -> failtest "Tab Geld erwartet"
            Expect.isNone opened.EntryAction "verbraucht"

        testCase "Ein Speicherfehler kommt als Toast „Nicht gespeichert: <Grund>“, die Anzeige springt zurück" <| fun () ->
            let start = rootModel Overview
            let saving =
                { start with
                    Overview =
                        { start.Overview with Pins = Success [ food; kids ]; PinsConfirmed = [ food ]; PinsSaving = Some [ food; kids ] } }
            let model, cmd = State.update (State.OverviewMsg (PinsSaved (Error "Datenbank gesperrt"))) saving
            Expect.equal model.Overview.Pins (Success [ food ]) "zurück auf den vorigen Stand"
            Expect.contains (dispatched cmd) (State.ShowToast ("Nicht gespeichert: Datenbank gesperrt", ToastError)) "Toast"

        // budgeting-w9yt4 Review: der Toast muss den Stand vor dem Umschalten lesen, sonst sagte er „gelöst“ beim
        // Anpinnen. Gegenprobe: `wasPinned` nach dem Umschalten gelesen macht diesen Test rot.
        testCase "Der Pin-Knopf im Kategorie-Detail pinnt ans Ende an und löst wieder, je mit Toast und gespeichert" <| fun () ->
            let start = rootModel Budget
            let withPins = { start with Overview = { start.Overview with Pins = Success [ food ]; PinsConfirmed = [ food ] } }
            let pinned, pinCmd = State.update (State.TogglePinFromInspector (restaurant, "Restaurant")) withPins
            Expect.equal pinned.Overview.Pins (Success [ food; restaurant ]) "am Ende"
            Expect.equal pinned.Overview.PinsSaving (Some [ food; restaurant ]) "gespeichert wird die ganze Liste"
            Expect.equal (toastsOf pinCmd) [ State.ShowToast ("Restaurant angepinnt", ToastSuccess) ] "Toast anpinnen"
            let saved, _ = State.update (State.OverviewMsg (PinsSaved (Ok ()))) pinned
            let loosened, looseCmd = State.update (State.TogglePinFromInspector (restaurant, "Restaurant")) saved
            Expect.equal loosened.Overview.Pins (Success [ food ]) "gelöst"
            Expect.equal loosened.Overview.PinsSaving (Some [ food ]) "gespeichert"
            Expect.equal (toastsOf looseCmd) [ State.ShowToast ("Restaurant gelöst", ToastSuccess) ] "Toast lösen"

        testCase "Der Pin-Knopf ohne geladene Pins tut nichts" <| fun () ->
            let model, cmd = State.update (State.TogglePinFromInspector (restaurant, "Restaurant")) (rootModel Budget)
            Expect.equal model.Overview.Pins NotAsked "unberührt"
            Expect.isEmpty (dispatched cmd) "kein Toast, kein Speichern"
    ]

