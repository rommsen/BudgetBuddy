module Tests.MoveMoneyClientTests

// budgeting-afd8h, budgeting-c5w2n: der Tab Geld der Budget-Seite. Die Aufrufe hinter `update` liegen
// in Lambdas und laufen unter .NET nie, geprüft werden Modell, Schlange und Meldung.

open System
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

let private category name (assigned: decimal) (available: decimal) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur assigned
      Activity = eur 0m
      Available = eur available
      Underfunded = None
      Target = None
      Snoozed = false }

let private food = category "Lebensmittel" 900m 80m
let private trip = category "Urlaub" 0m -30.5m
let private clothes = { category "Kleidung" 50m 100m with Underfunded = Some (eur 70m); Target = Some { Goal = eur 100m; Rhythm = Months 1 } }
let private monthKey = "2026-11-01"
let private monthOf (toBeBudgeted: decimal) (cats: BudgetCategory list) : BudgetMonth =
    { Month = monthKey
      ToBeBudgeted = eur toBeBudgeted
      Groups = [ { Name = "Alltag"; Categories = cats } ] }
let private month = monthOf 100m [ food; trip; clothes ]
let private frame : BudgetFrame =
    { CurrentMonth = monthKey
      Months = [ "2026-10-01"; monthKey; "2026-12-01" ]
      CategoryOrder = [] }

let private loaded = { init () with Frame = Success frame; Month = Success month }

let private update msg model =
    let model', _, external = Components.Budget.State.update msg model
    model', external

/// Führt Nachrichten nacheinander aus und gibt das Modell und die letzte Meldung zurück.
let private run (msgs: Msg list) (model: Model) : Model =
    msgs |> List.fold (fun current msg -> fst (update msg current)) model

/// Wie viele Aufrufe `update` anstößt. Ausführen lassen sie sich unter .NET nicht.
let private commands (msg: Msg) (model: Model) : int =
    let _, cmd, _ = Components.Budget.State.update msg model
    List.length cmd

let private selected (c: BudgetCategory) (m: Model) = run [ SelectCategory c.Id ] m
let private onFood = selected food loaded
let private tabOf (model: Model) = model.TabMoney.Value
let private queued (model: Model) =
    model.AssignQueue |> List.map (fun w -> w.CategoryId, w.Month, w.Assigned.Amount)
let private monthOfModel (model: Model) =
    match model.Month with
    | Success m -> m
    | _ -> failtest "Monat erwartet"
let private categoryIn (model: Model) (c: BudgetCategory) =
    monthOfModel model |> fun m -> m.Groups.Head.Categories |> List.find (fun x -> x.Id = c.Id)
let private field (c: BudgetCategory) (model: Model) =
    assignFieldText (categoryIn model c) (tabOf model)
let private previewOf (c: BudgetCategory) (model: Model) =
    assignPreview (monthOfModel model) (categoryIn model c) (tabOf model)
let private typed text = AssignTextChanged text

/// Holen in Lebensmittel aus Urlaub mit 20,00.
let private takeReady =
    run [ SwitchMoneyMode TakeMode; TransferAmountChanged "20,00"; PickCounterpart (CategorySide trip.Id) ] onFood
let private sending () = fst (confirmTransfer takeReady)

let private report (lines: (string * decimal * decimal * LineOutcome) list) : ApplyReport =
    { ApplicationId = "app-1"
      Month = "2026-11-01"
      Lines =
        lines
        |> List.map (fun (name, oldA, newA, outcome) ->
            { CategoryId = Guid.NewGuid()
              CategoryName = name
              OldAssigned = eur oldA
              NewAssigned = eur newA
              Outcome = outcome })
      ActualToBeBudgeted = None }

let private fieldTests =
    testList "Feld Zugewiesen" [
        test "+20 zeigt Verfügbar danach, Ergebnis und Zu verteilen danach" {
            let model = run [ typed "+20" ] onFood
            Expect.equal (previewOf food model) (AssignAfter (eur 100m, eur 920m, eur 80m)) "Vorschau"
        }
        test "Bei +0 oder dem Stand steht keine Vorschau" {
            for text in [ "+0"; "900"; "900,00" ] do
                Expect.equal (previewOf food (run [ typed text ] onFood)) NoPreview text
        }
        test "Zu verteilen darf negativ werden, nichts sperrt, gespeichert wird beim Verlassen" {
            let poor = { loaded with Month = Success (monthOf 10m [ food ]) } |> selected food
            let model = run [ typed "+20"; AssignFieldLeft ] poor
            Expect.equal (queued model) [ food.Id, monthKey, 920m ] "gespeichert"
            Expect.equal (monthOfModel model).ToBeBudgeted (eur -10m) "negativ"
        }
        // Regression: `formatAmountForInput` nimmt `abs`, und `-40,00` als Text wäre eine Rechnung.
        test "Bei negativem Zugewiesen behält das Feld das Vorzeichen, ein Schritt rechnet nicht relativ" {
            let debt = category "Schulden" -50m 0m
            let model = { loaded with Month = Success (monthOf 100m [ debt ]) } |> selected debt
            Expect.equal (field debt model) "-50,00" "Stand"
            let stepped = run [ AssignStepped 10m ] model
            Expect.equal (field debt stepped) "-40,00" "nach dem Schritt"
            Expect.equal (queued (run [ AssignFieldLeft ] stepped)) [ debt.Id, monthKey, -40m ] "der Wert, nicht -90"
        }
        test "abc ist keine gültige Rechnung, Enter schreibt nichts und das Feld behält den Text rot" {
            let model = run [ typed "abc"; AssignFieldLeft ] onFood
            Expect.equal (previewOf food model) InvalidCalculation "Vorschau"
            Expect.isEmpty model.AssignQueue "nichts geschrieben"
            Expect.equal (field food model) "abc" "Text bleibt"
            Expect.isTrue (assignFieldInvalid (tabOf model)) "rot"
        }
        test "Ungültiger Text beim Verlassen: nichts geschrieben, Eingabe verworfen, Warn-Toast" {
            let invalid = run [ typed "abc" ] onFood
            for name, msg in
                [ "Tab", SelectInspectorTab EntriesTab
                  "Kategorie", SelectCategory trip.Id
                  "Monat", SelectMonth "2026-10-01"
                  "Modus", SwitchMoneyMode TakeMode
                  "Schließen", CloseInspector ] do
                let model, external = update msg invalid
                Expect.isEmpty model.AssignQueue name
                Expect.equal (model.TabMoney |> Option.bind (fun t -> t.Entry)) None name
                Expect.equal external (ShowToast ("Zugewiesen nicht gespeichert: keine gültige Rechnung", ToastWarning)) name
        }
    ]

let private leaveTests =
    let edited = run [ typed "+20" ] onFood
    let leaving : (string * Msg) list =
        [ "Enter oder Fokus weg", AssignFieldLeft
          "Tab per Klick", SelectInspectorTab EntriesTab
          "Tab per Pfeiltaste", PageKeyPressed (StepTab Next)
          "Pfeil im Sheet", StepCategory Next
          "Zeile wählen", SelectCategory trip.Id
          "Pfeiltaste Runter", PageKeyPressed (StepRow Next)
          "Pfeiltaste Hoch", PageKeyPressed (StepRow Previous)
          "Monat blättern", SelectMonth "2026-12-01"
          "Monatsname", SelectMonth "2026-10-01"
          "Sheet schließen", CloseInspector
          "Moduswechsel", SwitchMoneyMode GiveMode
          "Seite verlassen", PageLeft ]
    testList "Speichern beim Verlassen" [
        test "Jeder Weg ergibt genau eine Zuweisung mit Kategorie und Monat der Eingabe" {
            for name, msg in leaving do
                Expect.equal (queued (fst (update msg edited))) [ food.Id, monthKey, 920m ] name
        }
        test "Fokus weg gefolgt vom Klick, der ihn auslöste, ergibt genau eine Zuweisung" {
            let model = run [ AssignFieldLeft; SelectInspectorTab EntriesTab ] edited
            Expect.equal (queued model) [ food.Id, monthKey, 920m ] "eine"
        }
        test "Zuweisen nach Holen speichert, Holen nach Zuweisen schreibt nichts und die Form kommt wieder" {
            let form = run [ SwitchMoneyMode GiveMode; TransferAmountChanged "20"; PickCounterpart ToBeBudgetedSide ] onFood
            let back = run [ SwitchMoneyMode AssignMode ] form
            Expect.isEmpty back.AssignQueue "schreibt nichts"
            let again = run [ SwitchMoneyMode GiveMode ] back
            Expect.equal (tabOf again).Transfer.AmountInput (Some "20") "Betrag"
            Expect.equal (tabOf again).Transfer.Counterpart (Some ToBeBudgetedSide) "Gegenseite"
        }
        test "Die Form endet mit Kategorie oder Monat" {
            let form = run [ SwitchMoneyMode GiveMode; TransferAmountChanged "20" ] onFood
            let other = run [ SelectCategory clothes.Id ] form
            Expect.equal (tabOf other).Transfer.AmountInput None "Kategorie"
            let otherMonth = run [ SelectMonth "2026-12-01" ] form
            let arrived = applyLoaded otherMonth.MonthGeneration (Ok { month with Month = "2026-12-01" }) otherMonth
            Expect.equal (tabOf (syncMoneyTab arrived)).Transfer.AmountInput None "Monat"
        }
        test "Zwei Schritte +50, +50, dann weiter: eine Zuweisung auf alt plus 100" {
            let model = run [ AssignStepped 50m; AssignStepped 50m; SelectCategory trip.Id ] onFood
            Expect.equal (queued model) [ food.Id, monthKey, 1000m ] "eine"
        }
        test "Verlassen ohne Änderung schreibt nichts" {
            let unchanged =
                [ [ AssignFieldLeft ]
                  [ typed "900,00"; AssignFieldLeft ]
                  [ typed "+0"; AssignFieldLeft ]
                  [ typed "900"; AssignFieldLeft ]
                  [ AssignStepped 10m; AssignStepped -10m; AssignFieldLeft ] ]
            for msgs in unchanged do
                Expect.isEmpty (run msgs onFood).AssignQueue $"{msgs}"
        }
        test "Esc verwirft: das Feld zeigt den Stand, der folgende Blur schreibt nichts" {
            let model = run [ typed "+20"; AssignDiscarded; AssignFieldLeft ] onFood
            Expect.equal (field food model) "900,00" "Stand"
            Expect.isEmpty model.AssignQueue "nichts"
        }
        test "Der gespeicherte Wert steht sofort im Monat, die Generation steigt, eine ältere Antwort überschreibt ihn nicht" {
            let model = run [ AssignFieldLeft ] edited
            let food' = categoryIn model food
            Expect.equal food'.Assigned (eur 920m) "Zugewiesen"
            Expect.equal food'.Available (eur 100m) "Verfügbar"
            Expect.equal (monthOfModel model).ToBeBudgeted (eur 80m) "Zu verteilen"
            Expect.isGreaterThan model.MonthGeneration edited.MonthGeneration "Generation"
            let stale = applyLoaded edited.MonthGeneration (Ok month) model
            Expect.equal (categoryIn stale food).Assigned (eur 920m) "die alte Antwort fällt weg"
        }
        test "Fällt die Kategorie beim Neu-Lesen weg, ist die Eingabe verworfen und ein Warn-Toast nennt das" {
            let without = monthOf 100m [ trip ]
            let model, external = update (Loaded (edited.MonthGeneration, Ok without)) edited
            Expect.isNone model.TabMoney "weg"
            Expect.isEmpty model.AssignQueue "nichts geschrieben"
            match external with
            | ShowToast (text, ToastWarning) -> Expect.stringContains text "Zugewiesen nicht gespeichert" "Text"
            | other -> failtest $"Warnung erwartet: {other}"
        }
        test "Während eines Monatswechsels nimmt das Feld keine Eingabe an, Schritte und Knöpfe tun nichts" {
            let changing = fst (select "2026-12-01" onFood)
            Expect.isTrue (monthChanging changing) "Vorbedingung"
            Expect.equal (run [ typed "+5"; AssignStepped 10m ] changing) changing "keine Eingabe"
            let red = { loaded with Month = Success month } |> selected trip
            let redChanging = fst (select "2026-12-01" red)
            Expect.equal (update CoverMissing redChanging |> fst) redChanging "kein Fehlbetrag"
        }
    ]

let private queueTests =
    let saved (c: BudgetCategory) text (model: Model) = run [ SelectCategory c.Id; typed text; AssignFieldLeft ] model
    let write (c: BudgetCategory) amount state : AssignWrite =
        { CategoryId = c.Id; Month = monthKey; Assigned = eur amount; State = state }
    let answer (model: Model) = update (AssignAnswered (model.AssignQueue.Head, Ok { ApplicationId = "a"; Month = monthKey; Lines = []; ActualToBeBudgeted = None })) model
    testList "Schlange" [
        test "Es läuft eine, weitere warten in Reihenfolge" {
            let model = onFood |> saved food "+1" |> saved trip "1" |> saved clothes "1"
            Expect.equal (model.AssignQueue |> List.map (fun w -> w.CategoryId, w.State))
                [ food.Id, WriteRunning; trip.Id, WriteWaiting; clothes.Id, WriteWaiting ] "Reihenfolge"
        }
        test "Eine wartende für dieselbe Kategorie und denselben Monat wird ersetzt, keine laufende" {
            let queue = [ write food 1m WriteRunning; write trip 2m WriteWaiting ]
            Expect.equal (enqueueAssign (write trip 3m WriteWaiting) queue |> List.map (fun w -> w.Assigned.Amount)) [ 1m; 3m ] "ersetzt"
            Expect.equal (enqueueAssign (write food 3m WriteWaiting) queue |> List.map (fun w -> w.Assigned.Amount)) [ 1m; 2m; 3m ] "die laufende bleibt"
        }
        test "Nach der Antwort startet die nächste, kein Neu-Lesen, nach der letzten einmal" {
            let model = onFood |> saved food "+1" |> saved trip "1"
            let afterFirst, _ = answer model
            Expect.equal (afterFirst.AssignQueue |> List.map (fun w -> w.CategoryId, w.State)) [ trip.Id, WriteRunning ] "die nächste läuft"
            Expect.equal afterFirst.MonthGeneration model.MonthGeneration "noch kein Neu-Lesen"
            let afterLast, _ = answer afterFirst
            Expect.isEmpty afterLast.AssignQueue "leer"
            Expect.equal afterLast.MonthGeneration (afterFirst.MonthGeneration + 1) "einmal neu gelesen"
        }
        test "Holen, Abgeben und der Fehlbetrag-Knopf sind gesperrt, solange die Schlange nicht leer ist" {
            let waiting = { takeReady with AssignQueue = [ write food 1m WriteRunning ] }
            Expect.isFalse (canTransfer waiting month (tabOf waiting)) "Knopf"
            Expect.isNone (snd (confirmTransfer waiting)) "Holen"
            let red = { selected trip loaded with AssignQueue = [ write food 1m WriteRunning ] }
            Expect.isNone (snd (confirmMissing red)) "Fehlbetrag"
        }
        test "Error: der Toast nennt den Grund, die Schlange läuft weiter, der Monat wird neu gelesen" {
            let model = onFood |> saved food "+1" |> saved trip "1"
            let next, external = update (AssignAnswered (model.AssignQueue.Head, Error "Monat nicht offen")) model
            Expect.equal external (ShowToast ("Nicht zugewiesen: Monat nicht offen", ToastError)) "Grund"
            Expect.equal (next.AssignQueue |> List.map (fun w -> w.State)) [ WriteRunning ] "weiter"
            let last, _ = update (AssignAnswered (next.AssignQueue.Head, Error "x")) next
            Expect.equal last.MonthGeneration (next.MonthGeneration + 1) "neu gelesen"
        }
        test "Aufruf abgebrochen: Toast Ob zugewiesen wurde ist unklar, Monat neu lesen" {
            let model = onFood |> saved food "+1"
            let next, external = update (AssignCallLost "Timeout") model
            Expect.equal external (ShowToast ("Ob zugewiesen wurde, ist unklar: Timeout", ToastError)) "ehrlich"
            Expect.isEmpty next.AssignQueue "leer"
            Expect.equal next.MonthGeneration (model.MonthGeneration + 1) "neu gelesen"
        }
        test "Bericht ohne Zeilen und Erfolg zeigen keinen Toast" {
            let model = onFood |> saved food "+1"
            Expect.equal (snd (answer model)) NoOp "ohne Zeilen"
            let written = report [ "Lebensmittel", 900m, 901m, Written ]
            Expect.equal (snd (update (AssignAnswered (model.AssignQueue.Head, Ok written)) model)) NoOp "Erfolg"
        }
        test "Eine Zuweisung im letzten Monat zieht den Rahmen still nach" {
            // Der Cmd läuft unter .NET nicht, gezählt wird: Neu-Lesen des Monats, dazu der Rahmen.
            let model = onFood |> saved food "+1"
            let last = { (report [ "Lebensmittel", 900m, 901m, Written ]) with Month = "2026-12-01" }
            let inner = report [ "Lebensmittel", 900m, 901m, Written ]
            Expect.equal (commands (AssignAnswered (model.AssignQueue.Head, Ok last)) model) 2 "letzter Monat"
            Expect.equal (commands (AssignAnswered (model.AssignQueue.Head, Ok inner)) model) 1 "Monat davor"
            Expect.equal (commands (AssignAnswered (model.AssignQueue.Head, Error "x")) model) 1 "nichts geschrieben"
        }
        test "Ein abgebrochener Aufruf zieht den Rahmen nach, wenn der gezeigte Monat der letzte ist" {
            let model = onFood |> saved food "+1"
            let atEnd = { model with Frame = Success { frame with Months = [ "2026-10-01"; monthKey ] } }
            Expect.equal (commands (AssignCallLost "Timeout") atEnd) 2 "letzter Monat"
            Expect.equal (commands (AssignCallLost "Timeout") model) 1 "Monat davor"
        }
    ]

/// Fehlt 12,30 bei Zugewiesen 900 und 100 zu verteilen.
let private rent = category "Miete" 900m -12.3m
let private rentMonth = monthOf 100m [ food; trip; rent ]
let private onRent =
    run [ SelectCategory rent.Id; SwitchMoneyMode AssignMode ] { loaded with Month = Success rentMonth }

let private line (c: BudgetCategory) (oldA: decimal) (newA: decimal) (outcome: LineOutcome) : ApplyLineResult =
    { CategoryId = c.Id; CategoryName = c.Name; OldAssigned = eur oldA; NewAssigned = eur newA; Outcome = outcome }

let private answered (lines: ApplyLineResult list) : Msg =
    MoveAnswered (Ok { ApplicationId = "app-2"; Month = monthKey; Lines = lines; ActualToBeBudgeted = None })

/// Der Monat, wie YNAB ihn nach dem Knopf liefert.
let private reloadedRent (model: Model) : Msg =
    let filled = { rent with Assigned = eur 912.3m; Available = eur 0m }
    Reloaded (model.MonthGeneration, Ok (monthOf 87.7m [ food; trip; filled ]))

let private busyTests =
    // budgeting-c5w2n, Verifier-Fund: Das Feld blieb während des Fehlbetrag-Aufrufs bedienbar. Eine
    // Zuweisung ist absolut und überschrieb die laufende Verschiebung (ADR 0078 Punkt 5).
    let covering = run [ CoverMissing ] onRent
    testList "Feld Zugewiesen während eines Aufrufs" [
        test "Tippen, Schritt, Enter und Esc ändern nichts, die Schlange bleibt leer" {
            Expect.isTrue (inspectorBusy covering) "Vorbedingung"
            for msg in [ typed "+5"; AssignStepped 10m; AssignFieldLeft; AssignDiscarded ] do
                Expect.equal (fst (update msg covering)) covering $"{msg}"
        }
        test "Stand 900, Knopf 12,30, Schritt während des Aufrufs: das Verlassen schickt 922,30" {
            let duringCall = run [ AssignStepped 10m; AssignFieldLeft ] covering
            let afterAnswer = run [ answered [ line rent 900m 912.3m Written ] ] duringCall
            let model = run [ reloadedRent afterAnswer; AssignStepped 10m; AssignFieldLeft ] afterAnswer
            Expect.equal (queued model) [ rent.Id, monthKey, 922.3m ] "auf dem neuen Stand"
        }
        test "Das Feld ist schreibgeschützt, solange ein Aufruf läuft, und danach wieder frei" {
            Expect.isFalse (assignFieldReadOnly onRent) "Ruhe"
            Expect.isTrue (assignFieldReadOnly covering) "Aufruf"
            Expect.isFalse (assignFieldReadOnly (run [ answered [] ] covering)) "Antwort da"
        }
        test "Nach der Antwort rechnet das Feld vom neuen Stand, auch wenn das Neu-Lesen noch läuft" {
            let afterAnswer = run [ answered [ line rent 900m 912.3m Written ] ] covering
            Expect.equal (categoryIn afterAnswer rent).Assigned (eur 912.3m) "Zugewiesen"
            Expect.equal (categoryIn afterAnswer rent).Available (eur 0m) "Verfügbar"
            Expect.equal (monthOfModel afterAnswer).ToBeBudgeted (eur 87.7m) "Zu verteilen"
            Expect.equal afterAnswer.MonthGeneration (covering.MonthGeneration + 1) "einmal neu gelesen"
            let model = run [ AssignStepped 10m; AssignFieldLeft ] afterAnswer
            Expect.equal (queued model) [ rent.Id, monthKey, 922.3m ] "auf dem neuen Stand"
        }
        test "Eine gescheiterte Zeile ändert den Stand nicht, Holen zwischen Kategorien lässt Zu verteilen stehen" {
            let failed = run [ answered [ line rent 900m 912.3m (Failed "weg") ] ] covering
            Expect.equal (categoryIn failed rent).Assigned (eur 900m) "gescheitert"
            let moved = run [ answered [ line food 900m 880m Written; line rent 900m 920m Written ] ] covering
            Expect.equal (categoryIn moved food).Assigned (eur 880m) "Quelle"
            Expect.equal (categoryIn moved rent).Assigned (eur 920m) "Ziel"
            Expect.equal (monthOfModel moved).ToBeBudgeted (eur 100m) "Zu verteilen"
            let other =
                MoveAnswered (Ok { ApplicationId = "app-3"; Month = "2026-12-01"; Lines = [ line rent 900m 912.3m Written ]; ActualToBeBudgeted = None })
            Expect.equal (categoryIn (run [ other ] covering) rent).Assigned (eur 900m) "anderer Monat"
        }
        test "Eine vorhandene Eingabe bleibt beim Verlassen während des Aufrufs stehen und wird danach gespeichert" {
            // Von Hand gebaut: Über `update` startet keine Bewegung mit offener Eingabe.
            let sendingWithEntry = fst (confirmTransfer (run [ typed "+5" ] takeReady))
            Expect.isTrue (inspectorBusy sendingWithEntry) "Vorbedingung"
            Expect.equal (run [ RetryMove "app-1"; ConfirmTransfer ] sendingWithEntry) sendingWithEntry "kein Start, nichts gespeichert"
            let left = run [ AssignFieldLeft ] sendingWithEntry
            Expect.isEmpty left.AssignQueue "nichts in der Schlange"
            Expect.equal (field food left) "+5" "die Eingabe steht"
            let answer = MoveAnswered (Error "abgelehnt")
            let saved = run [ answer; AssignFieldLeft ] left
            Expect.equal (queued saved) [ food.Id, monthKey, 905m ] "danach gespeichert"
        }
    ]

let private unclearTests =
    // budgeting-c5w2n, Verifier-Fund Iteration 2: "Erneut senden" lief am Feld vorbei, und nach einem
    // abgebrochenen Aufruf rechnete das Feld vom alten Stand. Beides nahm 12,30 zurück (ADR 0078 Punkt 5).
    let failedCover = run [ CoverMissing; answered [ line rent 900m 912.3m (Failed "weg") ] ] onRent
    let retrying = run [ RetryMove "app-2" ] failedCover
    let retried result = MoveRetried ("app-2", result)
    let written = { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent 900m 912.3m Written ]; ActualToBeBudgeted = None }
    let lost = run [ CoverMissing; MoveCallLost "Timeout" ] onRent
    let stepped = [ AssignStepped 10m; AssignFieldLeft ]
    testList "Feld Zugewiesen bei unklarem Stand" [
        test "Fehlbetrag scheitert bei 900, Erneut senden läuft, Schritt +10 sendet nichts und reiht nichts ein" {
            Expect.equal (run stepped retrying) retrying "nichts geändert"
            Expect.isEmpty (run stepped retrying).AssignQueue "leer"
            Expect.isTrue (assignFieldReadOnly retrying) "schreibgeschützt"
        }
        test "Erneut senden sperrt auch ohne gewählte Kategorie, Holen und Fehlbetrag warten" {
            let closed = run [ CloseInspector; RetryMove "app-2" ] failedCover
            Expect.isTrue (inspectorBusy closed) "ohne Tab"
            Expect.isNone (snd (confirmMissing retrying)) "Fehlbetrag"
            Expect.isTrue (inputActive retrying) "ein Neuladen zerstörte den Lauf"
        }
        test "Erneut senden startet nicht, solange die Schlange läuft oder ein Aufruf aus dem Tab" {
            let queued = run [ typed "+1"; AssignFieldLeft; RetryMove "app-2" ] failedCover
            Expect.equal (queued.OpenRetries |> List.map (fun r -> r.State)) [ RetryReady ] "Schlange"
            let sending = run [ CoverMissing; RetryMove "app-2" ] failedCover
            Expect.equal (sending.OpenRetries |> List.map (fun r -> r.State)) [ RetryReady ] "Aufruf"
        }
        test "Nach Erneut senden rechnet das Feld vom neuen Stand, auch wenn das Neu-Lesen noch läuft" {
            let model = run [ retried (Ok written) ] retrying
            Expect.equal (categoryIn model rent).Assigned (eur 912.3m) "eingetragen"
            Expect.equal model.MonthGeneration (retrying.MonthGeneration + 1) "einmal neu gelesen"
            Expect.equal (queued (run stepped model)) [ rent.Id, monthKey, 922.3m ] "auf dem neuen Stand"
        }
        test "Scheitert Erneut senden, bleibt das Feld gesperrt, bis das Neu-Lesen den Stand bringt" {
            let model = run [ retried (Error "Timeout") ] retrying
            Expect.isEmpty (run stepped model).AssignQueue "gesperrt"
            let read = run [ reloadedRent model ] model
            Expect.equal (queued (run stepped read)) [ rent.Id, monthKey, 922.3m ] "frei auf dem echten Stand"
        }
        test "Scheitert Erneut senden während eines Monatswechsels, bleibt die alte Liste gedimmt stehen" {
            let model = run [ SelectMonth "2026-12-01"; RetryMove "app-2"; retried (Error "Timeout") ] failedCover
            Expect.isTrue (monthChanging model) "der Wechsel läuft weiter"
        }
        test "Aufruf abgebrochen: das Feld nimmt nichts an, bis das Neu-Lesen da ist" {
            Expect.isEmpty (run (typed "+5" :: stepped) lost).AssignQueue "gesperrt"
            Expect.isNone (tabOf (run (typed "+5" :: stepped) lost)).Entry "keine Eingabe"
            Expect.isNone (snd (confirmMissing lost)) "kein zweiter Knopf"
        }
        test "Aufruf abgebrochen: eine ältere Monatsantwort gibt das Feld nicht frei" {
            let old = Reloaded (lost.MonthGeneration - 1, Ok rentMonth)
            Expect.isEmpty (run (old :: stepped) lost).AssignQueue "gesperrt"
        }
        test "Aufruf abgebrochen: nach dem Neu-Lesen zählt der gelesene Stand, angekommen oder nicht" {
            let landed = run (reloadedRent lost :: stepped) lost
            Expect.equal (queued landed) [ rent.Id, monthKey, 922.3m ] "angekommen"
            let missed = run (Reloaded (lost.MonthGeneration, Ok rentMonth) :: stepped) lost
            Expect.equal (queued missed) [ rent.Id, monthKey, 910m ] "nicht angekommen"
        }
        test "Aufruf abgebrochen: scheitert das Neu-Lesen, steht die Fehlerkarte, Wiederholen gibt das Feld frei" {
            let failed = run [ Reloaded (lost.MonthGeneration, Error "weg") ] lost
            Expect.equal failed.Month (Failure "weg") "Fehlerkarte mit Wiederholen"
            let again = run [ Load; FrameLoaded (Ok frame) ] failed
            let free = run (Loaded (again.MonthGeneration, Ok rentMonth) :: stepped) again
            Expect.equal (queued free) [ rent.Id, monthKey, 910m ] "wieder frei"
        }
    ]

/// Ein Weg, der eine Bewegung startet: Der Bericht hebt Miete um 12,30 gegenüber dem Stand in YNAB.
type private MoveWay =
    { Name: string
      Base: Model
      /// Macht den Start möglich. Der Dialog deckt das Feld ab, eine Eingabe entsteht davor.
      Arm: Model -> Model
      Start: Msg
      Landed: decimal -> Msg
      Lost: Msg }

let private moveWays : MoveWay list =
    let reportFrom (stand: decimal) : ApplyReport =
        { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent stand (stand + 12.3m) Written ]; ActualToBeBudgeted = None }
    let targetLine : TargetLine =
        { CategoryId = rent.Id
          CategoryName = rent.Name
          GroupName = "Alltag"
          OldAssigned = eur 900m
          Underfunded = eur 12.3m
          NewAssigned = eur 912.3m
          Target = None }
    let preview : TargetPreview =
        { Month = monthKey; Lines = [ targetLine ]; Skipped = []; Total = eur 12.3m; ToBeBudgeted = eur 100m; ToBeBudgetedAfter = eur 87.7m }
    let failedReport = { reportFrom 900m with Lines = [ line rent 900m 912.3m (Failed "weg") ] }
    [ { Name = "Erneut senden"
        Base = run [ CoverMissing; answered [ line rent 900m 912.3m (Failed "weg") ] ] onRent
        Arm = id
        Start = RetryMove "app-2"
        Landed = fun stand -> MoveRetried ("app-2", Ok (reportFrom stand))
        Lost = MoveRetried ("app-2", Error "weg") }
      { Name = "Übernehmen"
        Base = onRent
        Arm = fun model -> { model with Targets = PreviewReady (preview, false) }
        Start = ConfirmTargets
        Landed = fun stand -> TargetsApplied (Ok (Applied (reportFrom stand)))
        Lost = TargetsApplied (Error "weg") }
      { Name = "Fehlgeschlagene erneut senden"
        Base = onRent
        Arm = fun model -> { model with Targets = ApplyDone (preview, failedReport, false, None) }
        Start = RetryFailed
        Landed = fun stand -> TargetsRetried (Ok (reportFrom stand))
        Lost = TargetsRetried (Error "weg") }
      { Name = "Holen"
        Base = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "12,30"; PickCounterpart ToBeBudgetedSide ] onRent
        Arm = id
        Start = ConfirmTransfer
        Landed = fun stand -> MoveAnswered (Ok (reportFrom stand))
        Lost = MoveCallLost "weg" } ]

/// Der Stand in YNAB als Monat.
let private monthWith (stand: decimal) : BudgetMonth =
    monthOf (100m - (stand - 900m)) [ food; trip; { rent with Assigned = eur stand; Available = eur (stand - 912.3m) } ]

/// Der Fehlbetrag-Knopf startet auch, verwirft aber die Eingabe statt sie zu speichern.
let private coverWay : MoveWay =
    { Name = "Fehlbetrag"
      Base = onRent
      Arm = id
      Start = CoverMissing
      Landed = fun stand ->
        MoveAnswered (Ok { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent stand (stand + 12.3m) Written ]; ActualToBeBudgeted = None })
      Lost = MoveCallLost "weg" }

let private assignDone (model: Model) : Msg =
    AssignAnswered (model.AssignQueue.Head, Ok { ApplicationId = "a"; Month = monthKey; Lines = []; ActualToBeBudgeted = None })

let private settledTests =
    // budgeting-c5w2n, Verifier-Fund Iteration 3: Am Handy setzt ein Schritt eine Eingabe ohne Blur. Eine
    // Bewegung lief vorbei, beim Verlassen ging der Wert von vor der Bewegung hinaus (ADR 0078 Punkt 5).
    let forEachWay (check: MoveWay -> unit) = moveWays |> List.iter check
    let armedWith (msgs: Msg list) (way: MoveWay) : Model = way.Arm (run msgs way.Base)
    testList "Bewegung startet nur bei ruhendem Zuweisen" [
        test "Offene Eingabe: der Start speichert sie, die Bewegung startet nicht, nach dem Leerlauf geht der nächste Tipp" {
            forEachWay (fun way ->
                let saved = run [ way.Start ] (armedWith [ AssignStepped 10m ] way)
                Expect.isFalse (moveRunning saved) way.Name
                Expect.equal (queued saved) [ rent.Id, monthKey, 910m ] way.Name
                Expect.isNone (tabOf saved).Entry way.Name
                Expect.isFalse (canStartMove saved) $"{way.Name}: Knopf gesperrt"
                Expect.isFalse (moveRunning (run [ way.Start ] saved)) $"{way.Name}: zweiter Tipp"
                let idle = run [ assignDone saved ] saved
                Expect.isTrue (canStartMove idle) $"{way.Name}: Knopf frei"
                Expect.isFalse (moveRunning idle) $"{way.Name}: kein Nachholen"
                Expect.isTrue (moveRunning (run [ way.Start ] idle)) $"{way.Name}: startet")
        }
        // budgeting-c5w2n, Verifier-Fund Iteration 4: Der Toast "Erneut senden" schloss sich und nichts geschah.
        test "Läuft die Schlange oder eine Bewegung, bleibt alles stehen und ein Toast nennt es" {
            let blocked = ShowToast (moveBlockedText, ToastWarning)
            forEachWay (fun way ->
                let busy = armedWith [ typed "+1"; AssignFieldLeft ] way
                let model, external = update way.Start busy
                Expect.equal model busy way.Name
                Expect.equal external blocked $"{way.Name}: Schlange"
                let running = run [ way.Start ] (armedWith [] way)
                let again, repeated = update way.Start running
                Expect.equal again running way.Name
                Expect.equal repeated blocked $"{way.Name}: Bewegung")
        }
        test "Wird die offene Eingabe gerade gespeichert, zeigt das der Knopf, kein Toast" {
            forEachWay (fun way ->
                let _, external = update way.Start (armedWith [ AssignStepped 10m ] way)
                Expect.equal external NoOp way.Name
                let waiting = armedWith [ typed "+1"; AssignFieldLeft; AssignStepped 10m ] way
                let saved, secondWrite = update way.Start waiting
                Expect.equal secondWrite NoOp $"{way.Name}: zweite Eingabe"
                Expect.equal (List.length saved.AssignQueue) 2 way.Name)
        }
        test "Eine Eingabe ohne Änderung bei laufender Schlange fällt weg, der Toast nennt die Sperre" {
            forEachWay (fun way ->
                let waiting = armedWith [ typed "+1"; AssignFieldLeft; typed "+0" ] way
                let _, external = update way.Start waiting
                Expect.equal external (ShowToast (moveBlockedText, ToastWarning)) way.Name)
        }
        test "Ohne Eingabe und mit leerer Schlange startet die Bewegung" {
            forEachWay (fun way ->
                let running = run [ way.Start ] (armedWith [] way)
                Expect.isTrue (moveRunning running) way.Name
                Expect.isFalse (canStartMove running) $"{way.Name}: Knopf gesperrt, solange sie läuft")
        }
        test "Eine Eingabe ohne Änderung fällt weg, die Bewegung startet im selben Tipp" {
            forEachWay (fun way -> Expect.isTrue (moveRunning (run [ way.Start ] (armedWith [ typed "+0" ] way))) way.Name)
        }
        test "Eine ungültige Eingabe fällt mit Warnung weg, die Bewegung startet nicht" {
            forEachWay (fun way ->
                let model, external = update way.Start (armedWith [ typed "abc" ] way)
                Expect.equal external (ShowToast (invalidEntryText, ToastWarning)) way.Name
                Expect.isNone (tabOf model).Entry way.Name
                Expect.isFalse (moveRunning model) way.Name)
        }
        // budgeting-c5w2n, Verifier-Fund Iteration 4: `enterPage` räumte den Tab und mit ihm den Merker des
        // laufenden Aufrufs. Das Feld war frei und 910 hätte die 12,30 zurückgenommen (ADR 0078 Punkt 5).
        test "Fehlbetrag läuft, Seite weg und zurück, Kategorie wählen, Schritt: nichts geschrieben, die Antwort kommt an" {
            forEachWay (fun way ->
                let away = run [ way.Start; PageLeft; PageEntered ] (armedWith [] way)
                Expect.isTrue (moveRunning away) $"{way.Name}: läuft weiter"
                let tried = run [ SelectCategory rent.Id; AssignStepped 10m; AssignFieldLeft ] away
                Expect.isEmpty tried.AssignQueue way.Name
                Expect.isFalse (moveRunning (run [ way.Landed 900m ] tried)) $"{way.Name}: Antwort")
            let covered = run [ CoverMissing; PageLeft; PageEntered; SelectCategory rent.Id; AssignStepped 10m; AssignFieldLeft ] onRent
            Expect.isEmpty covered.AssignQueue "Fehlbetrag"
            let model, external = update (coverWay.Landed 900m) covered
            Expect.isFalse (moveRunning model) "beendet"
            Expect.equal external (moveToast { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent 900m 912.3m Written ]; ActualToBeBudgeted = None }) "Toast"
            let free = run (Reloaded (model.MonthGeneration, Ok (monthWith 912.3m)) :: [ AssignStepped 10m; AssignFieldLeft ]) model
            Expect.equal (queued free) [ rent.Id, monthKey, 922.3m ] "auf dem neuen Stand"
        }
        test "Ohne laufenden Aufruf beginnt die Seite beim Betreten neu" {
            let model = run [ PageEntered ] onRent
            Expect.isNone model.Selected "Auswahl weg"
            Expect.isNone model.TabMoney "Tab weg"
        }
        test "Für jede Reihenfolge liegt nie eine Zuweisung auf einem Wert von vor der Bewegung im Versand" {
            // Aufzählung bis Tiefe 6, auch über den Seitenwechsel. Die Bewegung hebt den Stand um 12,30, Schritte und Eingaben sind
            // relativ und ganzzahlig: Ein Wert ohne ,30 in der Schlange rechnet vom Stand davor. Absolute
            // Eingaben wie `950` fehlen, sie sind gewollt. Ein abgebrochener Aufruf gilt als angekommen.
            coverWay :: moveWays
            |> List.iter (fun way ->
                let mutable writesAfterMove = 0
                let rec explore (depth: int) (path: string list) (model: Model) (stand: decimal) (started: bool) =
                    let next (name: string) (msg: Msg) (stand': decimal) =
                        let after = fst (update msg model)
                        let path' = name :: path
                        let shown = String.concat ", " (List.rev path')
                        let startedNow = moveRunning after && not (moveRunning model) && not started
                        if startedNow then
                            Expect.isEmpty after.AssignQueue $"{way.Name}: Schlange beim Start: {shown}"
                            Expect.isTrue (after.TabMoney |> Option.forall (fun tab -> tab.Entry.IsNone)) $"{way.Name}: Eingabe beim Start: {shown}"
                        let started' = started || startedNow
                        if started' then
                            for write in after.AssignQueue do
                                writesAfterMove <- writesAfterMove + 1
                                Expect.notEqual (write.Assigned.Amount % 5m) 0m $"{way.Name}: {write.Assigned.Amount} nach {shown}"
                        if depth > 1 then explore (depth - 1) path' after stand' started'
                    next "Schritt" (AssignStepped 10m) stand
                    next "Tippen" (typed "+5") stand
                    next "Verlassen" AssignFieldLeft stand
                    next "Start" way.Start stand
                    next "Seite weg" PageLeft stand
                    next "Seite da" PageEntered stand
                    next "Wählen" (SelectCategory rent.Id) stand
                    next "Neu-Lesen" (Reloaded (model.MonthGeneration, Ok (monthWith stand))) stand
                    if model.Targets <> TargetsClosed then next "Schließen" CloseTargets stand
                    if moveRunning model then
                        next "Antwort" (way.Landed stand) (stand + 12.3m)
                        next "Abbruch" way.Lost (stand + 12.3m)
                    match model.AssignQueue with
                    | head :: _ -> next "Zuweisung fertig" (assignDone model) head.Assigned.Amount
                    | [] -> ()
                explore 6 [] (armedWith [] way) 900m false
                explore 5 [] (armedWith [ AssignStepped 10m ] way) 900m false
                Expect.isGreaterThan writesAfterMove 0 $"{way.Name}: die Aufzählung erreicht Zuweisungen nach der Bewegung")
        }
    ]

let private activeTests =
    testList "Eingabe aktiv" [
        test "Falsch im Ruhezustand und bei bloßer Vorbelegung" {
            Expect.isFalse (inputActive onFood) "Ruhe"
            Expect.isFalse (inputActive (selected trip loaded)) "Vorbelegung"
        }
        test "Wahr bei Eingabe, Betrag, Gegenseite, laufendem Aufruf und nicht leerer Schlange" {
            Expect.isTrue (inputActive (run [ typed "1" ] onFood)) "Feld"
            Expect.isTrue (inputActive (run [ SwitchMoneyMode TakeMode; TransferAmountChanged "1" ] onFood)) "Betrag"
            Expect.isTrue (inputActive (run [ SwitchMoneyMode TakeMode; PickCounterpart ToBeBudgetedSide ] onFood)) "Gegenseite"
            Expect.isTrue (inputActive (sending ())) "Aufruf"
            Expect.isTrue (inputActive (run [ typed "+1"; AssignFieldLeft ] onFood)) "Schlange"
        }
    ]

let private missingTests =
    let onCategory (toBeBudgeted: decimal) (c: BudgetCategory) =
        { loaded with Month = Success (monthOf toBeBudgeted [ c ]) } |> selected c
    let actionOf (toBeBudgeted: decimal) (c: BudgetCategory) =
        missingAction (monthOf toBeBudgeted [ c ]) c
    let minus = category "Rot" 0m -12.3m
    testList "Fehlbetrag-Knopf" [
        test "Verfügbar -12,30 ohne Target: 12,30 zuweisen, Auftrag von Zu verteilen an X" {
            Expect.equal (missingActionText (actionOf 100m minus)) "12,30 zuweisen" "Text"
            match snd (confirmMissing (onCategory 100m minus)) with
            | Some (key, request) ->
                Expect.equal key monthKey "Monat"
                Expect.equal request { Source = ToBeBudgetedSide; Target = CategorySide minus.Id; Amount = eur 12.3m } "Auftrag"
            | None -> failtest "Auftrag erwartet"
        }
        test "Das Größere von Minus und Fehlbetrag zählt" {
            let c = { minus with Available = eur -5m; Underfunded = Some (eur 20m) }
            Expect.equal (missingActionText (actionOf 100m c)) "20,00 zuweisen" "Fehlbetrag"
        }
        test "Pausiertes Target: der Fehlbetrag zählt nicht" {
            let paused = { minus with Available = eur 10m; Underfunded = Some (eur 20m); Snoozed = true }
            Expect.equal (actionOf 100m paused) NothingMissing "kein Knopf"
            Expect.equal (missingActionText (actionOf 100m { paused with Available = eur -5m })) "5,00 zuweisen" "nur das Minus"
            Expect.equal (actionOf 100m { minus with Available = eur 10m }) NothingMissing "nichts fehlt"
        }
        test "Reicht Zu verteilen nicht, steht der Rest da, ohne Auftrag" {
            let c = { minus with Available = eur -80m }
            Expect.equal (missingActionText (actionOf 60.2m c)) "Nur noch 60,20 zu verteilen" "Text"
            Expect.isNone (snd (confirmMissing (onCategory 60.2m c))) "kein Auftrag"
            Expect.equal (actionOf 80m c) (AssignMissing (eur 80m)) "genau gleich wirkt"
            Expect.equal (missingActionText (actionOf -5m c)) "Nur noch 0,00 zu verteilen" "negativ"
        }
        test "Offene Eingabe plus Knopf: die Eingabe fällt weg, zwei Tipps ergeben einen Auftrag" {
            let model = run [ SwitchMoneyMode AssignMode; typed "+5" ] (onCategory 100m minus)
            let sent, first = confirmMissing model
            Expect.isSome first "ein Auftrag"
            Expect.isNone (tabOf sent).Entry "Eingabe weg"
            Expect.isEmpty sent.AssignQueue "kein Zuweisen"
            Expect.isNone (snd (confirmMissing sent)) "kein zweiter"
        }
    ]

let private modeTests =
    testList "Modi" [
        test "Verfügbar im Minus startet auf Holen mit dem Minus als Betrag, sonst Zuweisen ohne Betrag" {
            let red = selected trip loaded
            Expect.equal (tabOf red).Mode TakeMode "rot"
            Expect.equal (transferAmountText month (tabOf red)) "30,50" "Vorbelegung"
            Expect.equal (tabOf onFood).Mode AssignMode "sonst"
            Expect.equal (transferAmountText month (tabOf (run [ SwitchMoneyMode TakeMode ] onFood))) "" "leer"
        }
        test "Der Startmodus gilt bei Schritt, Pfeiltaste und nach dem Monatswechsel" {
            let stepped = run [ StepCategory Next ] onFood
            Expect.equal (tabOf stepped).Mode TakeMode "˅ auf die rote Kategorie"
            let keyed = run [ PageKeyPressed (StepRow Next) ] onFood
            Expect.equal (tabOf keyed).Mode TakeMode "Pfeiltaste"
            let switched = run [ SwitchMoneyMode GiveMode; SelectMonth "2026-12-01" ] onFood
            Expect.equal (tabOf switched).Mode GiveMode "während des Wechsels steht er"
            let arrived, _ = update (Loaded (switched.MonthGeneration, Ok { month with Month = "2026-12-01" })) switched
            Expect.equal (tabOf arrived).Mode AssignMode "danach beginnt er neu"
        }
        test "Reload bei gleicher Kategorie und gleichem Monat ändert den Modus nicht, auch wenn nicht mehr rot" {
            let red = selected trip loaded
            let fixedTrip = { trip with Available = eur 5m }
            let model, _ = update (Loaded (red.MonthGeneration, Ok (monthOf 100m [ food; fixedTrip ]))) red
            Expect.equal (tabOf model).Mode TakeMode "bleibt"
        }
        test "Ändert ein Reload das Minus, folgt die Vorbelegung, Getipptes bleibt" {
            let red = selected trip loaded
            let later = monthOf 100m [ food; { trip with Available = eur -10m } ]
            let model, _ = update (Loaded (red.MonthGeneration, Ok later)) red
            Expect.equal (transferAmountText later (tabOf model)) "10,00" "folgt"
            let typedModel = run [ TransferAmountChanged "7" ] red
            let kept, _ = update (Loaded (typedModel.MonthGeneration, Ok later)) typedModel
            Expect.equal (transferAmountText later (tabOf kept)) "7" "bleibt"
        }
    ]

let private transferTests =
    let give = run [ SwitchMoneyMode GiveMode; TransferAmountChanged "20,00"; PickCounterpart (CategorySide trip.Id) ] onFood
    let requestOf (model: Model) = transferRequest month (tabOf model)
    let endsOf (model: Model) = transferEnds month (tabOf model)
    testList "Holen und Abgeben" [
        test "Holen in X aus Y: Quelle Y, Ziel X. Abgeben von X an Y: Quelle X, Ziel Y" {
            Expect.equal (requestOf takeReady |> Option.map (fun r -> r.Source, r.Target)) (Some (CategorySide trip.Id, CategorySide food.Id)) "Holen"
            Expect.equal (requestOf give |> Option.map (fun r -> r.Source, r.Target)) (Some (CategorySide food.Id, CategorySide trip.Id)) "Abgeben"
        }
        test "von und nach zeigen je den Stand danach" {
            let source, target = endsOf takeReady
            Expect.equal source (Some { Label = "Urlaub"; After = eur -50.5m }) "von, negativ ist gültig"
            Expect.equal target (Some { Label = "Lebensmittel"; After = eur 100m }) "nach"
        }
        test "Die Gegenseite Zu verteilen zeigt Zu verteilen danach" {
            let model = run [ PickCounterpart ToBeBudgetedSide ] takeReady
            Expect.equal (fst (endsOf model)) (Some { Label = "Zu verteilen"; After = eur 80m }) "danach"
        }
        test "Ohne gültigen Betrag stehen die heutigen Stände" {
            let model = run [ TransferAmountChanged "abc" ] takeReady
            Expect.equal (fst (endsOf model) |> Option.map (fun e -> e.After)) (Some (eur -30.5m)) "heute"
        }
        test "Der Knopftext nennt Betrag und Gegenseite" {
            let onClothes c = run [ PickCounterpart (CategorySide clothes.Id) ] c
            Expect.equal (transferButtonText month (tabOf (onClothes takeReady))) "20,00 € aus Kleidung holen" "Holen"
            Expect.equal (transferButtonText month (tabOf (onClothes give))) "20,00 € an Kleidung abgeben" "Abgeben"
            Expect.equal (transferButtonText month (tabOf (run [ PickCounterpart ToBeBudgetedSide ] takeReady))) "20,00 € aus Zu verteilen holen" "Zu verteilen"
        }
        test "Gesperrt ohne Gegenseite, ohne gültigen Betrag, bei 0 oder negativ, während eines Aufrufs" {
            Expect.isFalse (canTransfer (run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5" ] onFood) month (tabOf (run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5" ] onFood))) "ohne Gegenseite"
            for text in [ ""; "abc"; "0"; "0,00"; "0-5" ] do
                let model = run [ TransferAmountChanged text ] takeReady
                Expect.isFalse (canTransfer model month (tabOf model)) text
                Expect.isNone (snd (confirmTransfer model)) text
            let busy = sending ()
            Expect.isFalse (canTransfer busy month (tabOf busy)) "Aufruf"
        }
        test "Keine Gegenseite ist vorbelegt" {
            Expect.isNone (tabOf (selected trip loaded)).Transfer.Counterpart "rot"
        }
        test "Das Betragsfeld rechnet, der Stand ist die Vorbelegung, sonst 0" {
            let red = run [ TransferAmountChanged "*2" ] (selected { trip with Id = trip.Id } loaded)
            Expect.equal (transferAmount month (tabOf red)) (Some 61m) "vorbelegt"
            let plain = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "+5" ] onFood
            Expect.equal (transferAmount month (tabOf plain)) (Some 5m) "sonst 0"
            let cents = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "100/3" ] onFood
            Expect.equal (transferAmount month (tabOf cents)) (Some 33.33m) "auf den Cent"
        }
        test "Die Warnung nennt den freien Betrag der Quelle, in Holen und Abgeben, ohne Sperre" {
            let fromClothes = run [ PickCounterpart (CategorySide clothes.Id); TransferAmountChanged "31" ] takeReady
            Expect.equal (transferWarning month (tabOf fromClothes)) (Some "Kleidung hat 30,00 € frei") "Holen"
            Expect.isTrue (canTransfer fromClothes month (tabOf fromClothes)) "keine Sperre"
            let exact = run [ TransferAmountChanged "30" ] fromClothes
            Expect.isNone (transferWarning month (tabOf exact)) "gleich frei"
            let giving = run [ TransferAmountChanged "81" ] give
            Expect.equal (transferWarning month (tabOf giving)) (Some "Lebensmittel hat 80,00 € frei") "Abgeben"
            let fromBudget = run [ PickCounterpart ToBeBudgetedSide; TransferAmountChanged "999" ] takeReady
            Expect.isNone (transferWarning month (tabOf fromBudget)) "Zu verteilen"
        }
        test "Negativer freier Betrag erscheint als 0,00 €" {
            let model = run [ TransferAmountChanged "1" ] takeReady
            Expect.equal (transferWarning month (tabOf model)) (Some "Urlaub hat 0,00 € frei") "nie negativ"
        }
        test "Nach Erfolg bleibt der Modus, Betrag und Gegenseite sind zurückgesetzt, der Toast nennt die Verschiebung" {
            let r = report [ "Urlaub", 0m, -20m, Written; "Lebensmittel", 900m, 920m, Written ]
            let model, external, reload = applyMoveAnswered (Ok r) (sending ())
            Expect.equal (tabOf model).Mode TakeMode "Modus"
            Expect.isNone (tabOf model).Transfer.AmountInput "Betrag"
            Expect.isNone (tabOf model).Transfer.Counterpart "Gegenseite"
            Expect.equal (tabOf model).Transfer.Submission Editing "wieder bereit"
            Expect.isTrue reload "neu lesen"
            Expect.equal external (ShowToast ("20,00 € von Urlaub nach Lebensmittel", ToastSuccess)) "Toast"
        }
        test "Error: der Grund steht im Tab, die Eingaben bleiben" {
            let model, external, reload = applyMoveAnswered (Error "YNAB antwortet nicht") (sending ())
            Expect.equal (tabOf model).Transfer.Submission (Rejected "YNAB antwortet nicht") "Grund"
            Expect.equal (tabOf model).Transfer.AmountInput (Some "20,00") "Betrag"
            Expect.isTrue (canTransfer model month (tabOf model)) "wieder bedienbar"
            Expect.equal external NoOp "kein Toast"
            Expect.isFalse reload "nichts geändert"
        }
        test "Aufruf abgebrochen: Formular zurückgesetzt, Toast, Monat neu lesen" {
            let busy = sending ()
            let model, external = applyMoveCallLost "Timeout" busy
            Expect.isNone (tabOf model).Transfer.AmountInput "zurückgesetzt"
            Expect.equal external (ShowToast ("Ob verschoben wurde, ist unklar: Timeout", ToastError)) "ehrlich"
            Expect.equal model.MonthGeneration (busy.MonthGeneration + 1) "neu lesen"
        }
        test "Solange ein Aufruf läuft, wird der Inspektor nicht verlassen" {
            let busy = sending ()
            for msg in
                [ StepCategory Next; SelectCategory trip.Id; PageKeyPressed (StepRow Next); SelectMonth "2026-12-01"
                  SelectInspectorTab EntriesTab; SwitchMoneyMode GiveMode; CloseInspector ] do
                Expect.equal (fst (update msg busy)) busy $"{msg}"
        }
        test "Ein Doppeltipp ergibt einen Auftrag" {
            let busy = sending ()
            Expect.isNone (snd (confirmTransfer busy)) "kein zweiter"
            Expect.equal (fst (update ConfirmTransfer busy)) busy "update tut nichts"
        }
        test "Esc setzt den Betrag auf die Vorbelegung zurück, die Gegenseite bleibt" {
            let model = run [ TransferAmountReset ] takeReady
            Expect.isNone (tabOf model).Transfer.AmountInput "Vorbelegung"
            Expect.isSome (tabOf model).Transfer.Counterpart "Gegenseite"
        }
        test "Während eines Monatswechsels ist der Knopf gesperrt, kein Auftrag geht in den alten Monat" {
            let changing = fst (select "2026-12-01" takeReady)
            Expect.isFalse (canTransfer changing month (tabOf changing)) "gesperrt"
            Expect.isNone (snd (confirmTransfer changing)) "kein Auftrag"
        }
        test "Der Auftrag trägt BudgetMonth.Month, in jeder Monatsart" {
            for key in [ "2026-10-01"; monthKey; "2026-12-01" ] do
                let shown = { loaded with ChosenMonth = Some key; Month = Success { month with Month = key } } |> selected food
                let model = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5"; PickCounterpart ToBeBudgetedSide ] shown
                Expect.equal (snd (confirmTransfer model) |> Option.map fst) (Some key) key
        }
    ]

let private pickerTests =
    testList "Gegenseite" [
        test "Der Picker bietet alle Kategorien des Monats außer X, mit Verfügbar aus dem Monat" {
            let picked = pickerCategories month food.Id
            Expect.equal (picked |> List.map (fun c -> c.Name)) [ "Urlaub"; "Kleidung" ] "X fehlt"
            Expect.equal picked.Head.Available (Some (eur -30.5m)) "Verfügbar des Monats"
            Expect.equal picked.Head.GroupName "Alltag" "Gruppe"
        }
        test "Aus der Wahl wird die Gegenseite" {
            Expect.equal (counterpartOf Client.DesignSystem.CategoryPicker.ChoseToBeBudgeted) (Some ToBeBudgetedSide) "Zu verteilen"
            Expect.equal
                (counterpartOf (Client.DesignSystem.CategoryPicker.ChoseCategory (Some (YnabCategoryId trip.Id))))
                (Some (CategorySide trip.Id)) "Kategorie"
            Expect.equal (counterpartOf (Client.DesignSystem.CategoryPicker.ChoseCategory None)) None "ohne Kategorie gibt es nicht"
        }
        test "Zuletzt verwendet: X fehlt, unbekannte Kategorien fallen weg, höchstens fünf" {
            let many = List.init 7 (fun i -> category $"K{i}" 0m 0m)
            let m = monthOf 0m (food :: many)
            let recent = Guid.NewGuid() :: food.Id :: (many |> List.map (fun c -> c.Id))
            let model = { (run [ OpenPicker ] onFood) with RecentCounterparts = Success recent }
            let shown =
                Client.DesignSystem.CategoryPicker.recentCategories (pickerCategories m food.Id) None (recentCounterparts model)
            Expect.equal (shown |> List.map (fun c -> c.Name)) [ "K0"; "K1"; "K2"; "K3"; "K4" ] "die ersten fünf"
        }
        test "Scheitert die Abfrage, gibt es keinen Abschnitt und keine Meldung" {
            let model, external = update (RecentCounterpartsLoaded (Error "kaputt")) (run [ OpenPicker ] onFood)
            Expect.isEmpty (recentCounterparts model) "ohne Abschnitt"
            Expect.equal external NoOp "still"
        }
        test "Beim Öffnen wird neu gelesen, das Ergebnis geht an den Picker" {
            let opened = fst (update OpenPicker onFood)
            Expect.equal opened.RecentCounterparts Loading "liest"
            let model, _ = update (RecentCounterpartsLoaded (Ok [ trip.Id ])) opened
            Expect.equal (recentCounterparts model) [ YnabCategoryId trip.Id ] "Ergebnis"
            let _, cmd, _ = Components.Budget.State.update OpenPicker model
            Expect.isNonEmpty cmd "auch beim zweiten Öffnen"
        }
        test "Ein offener Picker sperrt die Pfeiltasten der Seite" {
            let model = run [ OpenPicker ] onFood
            for key in [ "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight" ] do
                Expect.equal (pageKeyActionFor model key OtherFocus) LeaveKey key
        }
        test "Beim Wählen schließt der Picker" {
            let model = run [ OpenPicker; PickCounterpart ToBeBudgetedSide ] onFood
            Expect.isFalse (pickerOpen model) "zu"
        }
    ]

let private keyTests =
    testList "Tastatur" [
        test "Mit Fokus im Feld bleiben alle vier Pfeiltasten im Feld" {
            for key in [ "ArrowUp"; "ArrowDown"; "ArrowLeft"; "ArrowRight" ] do
                Expect.equal (pageKeyActionFor onFood key TextField) LeaveKey key
        }
        test "Auf dem Umschalter wechseln Links und Rechts die Tabs" {
            Expect.equal (pageKeyActionFor onFood "ArrowRight" OtherFocus) (StepTab Next) "rechts"
            Expect.equal (pageKeyActionFor onFood "ArrowLeft" OtherFocus) (StepTab Previous) "links"
        }
    ]

let private openRetryTests =
    testList "Offene Verschiebung im Tab" [
        test "Der Hinweis bleibt bei Tab-, Kategorie-, Monats- und Moduswechsel stehen" {
            let failing = report [ "Urlaub", 0m, -20m, Written; "Lebensmittel", 900m, 920m, Failed "Rate-Limit" ]
            let model, _, _ = applyMoveAnswered (Ok failing) (sending ())
            let after =
                run [ SelectInspectorTab EntriesTab; SelectInspectorTab MoneyTab; SwitchMoneyMode GiveMode
                      SelectCategory trip.Id; SelectMonth "2026-10-01" ] model
            Expect.equal (after.OpenRetries |> List.map (fun r -> r.Headline, r.MonthLabel))
                [ "Nicht vollständig verschoben", "November 2026" ] "steht"
        }
        test "Eine fehlgeschlagene Zuweisung erscheint als Nicht zugewiesen, mit Erneut senden" {
            let model = run [ typed "+20"; AssignFieldLeft ] onFood
            let failed = report [ "Lebensmittel", 900m, 920m, Failed "Rate-Limit" ]
            let next, external = update (AssignAnswered (model.AssignQueue.Head, Ok failed)) model
            Expect.equal (next.OpenRetries |> List.map (fun r -> r.Headline)) [ "Nicht zugewiesen" ] "Hinweis"
            match external with
            | ShowToastWithAction (text, ToastError, action) ->
                Expect.equal text "Nicht zugewiesen: Rate-Limit" "Text"
                Expect.equal action.Kind (RetryBudgetMove "app-1") "Anwendung"
            | other -> failtest $"Toast mit Aktion erwartet: {other}"
        }
        test "Ein fehlgeschlagener Fehlbetrag-Knopf heißt ebenso Nicht zugewiesen" {
            let red = selected trip loaded
            let sent, _ = confirmMissing red
            let failed = report [ "Urlaub", 0m, 30.5m, Failed "Rate-Limit" ]
            let model, external, _ = applyMoveAnswered (Ok failed) sent
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.Headline)) [ "Nicht zugewiesen" ] "Hinweis"
            match external with
            | ShowToastWithAction (text, _, _) -> Expect.equal text "Nicht zugewiesen: Rate-Limit" "Text"
            | other -> failtest $"Toast erwartet: {other}"
        }
    ]

let private pageChangeTests =
    testList "Seitenwechsel" [
        // budgeting-nfrds: das Formular blieb im Modell und sprang beim Zurückkehren wieder auf.
        test "Der Tab überlebt das Betreten der Seite nicht" {
            let model, _ = enterPage takeReady
            Expect.isNone model.TabMoney "weg"
        }
        test "Kommt die Absage nach der Rückkehr, steht der Grund im Tab" {
            let model = run [ PageEntered; MoveAnswered (Error "abgelehnt") ] (sending ())
            Expect.equal (tabOf model).Transfer.Submission (Rejected "abgelehnt") "im Tab"
            Expect.isSome model.Selected "der Tab ist zu sehen"
        }
        test "Kommt die Absage auf der anderen Seite, steht der Grund nach der Rückkehr im Tab" {
            let model = run [ MoveAnswered (Error "abgelehnt"); PageEntered ] (sending ())
            Expect.equal (tabOf model).Transfer.Submission (Rejected "abgelehnt") "im Tab"
            Expect.isSome model.Selected "der Tab ist zu sehen"
        }
        test "Ist der Tab weg, weil die Kategorie wegfiel, nennt ein Toast die Absage" {
            let _, external, _ = applyMoveAnswered (Error "abgelehnt") { sending () with TabMoney = None }
            Expect.equal external (ShowToast ("Nicht verschoben: abgelehnt", ToastError)) "sichtbar"
        }
    ]

let private summaryTests =
    testList "Berichte" [
        test "Zuweisen nennt Zu verteilen, in beide Richtungen" {
            Expect.equal (moveSummaryText (report [ "Urlaub", 0m, 20m, Written ])) "20,00 € von Zu verteilen nach Urlaub" "Zuweisen"
            Expect.equal (moveSummaryText (report [ "Urlaub", 20m, 15m, Written ])) "5,00 € von Urlaub nach Zu verteilen" "Zurücknehmen"
        }
        test "Fehlgeschlagene Zeile: Toast mit Grund und Erneut senden derselben Anwendung" {
            let r = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
            match moveToast r with
            | ShowToastWithAction (text, ToastError, action) ->
                Expect.equal text "Nicht vollständig verschoben: Rate-Limit" "Text"
                Expect.equal action.Label "Erneut senden" "Beschriftung"
                Expect.equal action.Kind (RetryBudgetMove "app-1") "Anwendung"
            | other -> failtest $"Toast mit Aktion erwartet: {other}"
        }
        test "Hat YNAB gewonnen, warnt der Toast statt Erfolg zu melden" {
            match moveToast (report [ "Lebensmittel", 80m, 60m, ChangedInYnab; "Urlaub", 0m, 20m, ChangedInYnab ]) with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtest $"Warnung erwartet: {other}"
        }
    ]

let private reloadTests =
    testList "Stilles Neu-Lesen" [
        // budgeting-nfrds: ein gescheiterter Reload setzte `Failure` und nahm die Liste weg.
        test "Scheitert es, bleibt die Liste stehen und ein Hinweis erscheint" {
            let model, external = applyReloaded loaded.MonthGeneration (Error "Timeout") loaded
            Expect.equal model.Month (Success month) "Liste bleibt"
            match external with
            | ShowToast (text, ToastWarning) -> Expect.stringContains text "Timeout" "Grund"
            | other -> failtest $"Hinweis erwartet: {other}"
        }
        test "Gelingt es, kommt der neue Stand ohne Hinweis" {
            let fresh = { month with ToBeBudgeted = eur 5m }
            let model, external = applyReloaded loaded.MonthGeneration (Ok fresh) loaded
            Expect.equal (monthOfModel model).ToBeBudgeted (eur 5m) "neu"
            Expect.equal external NoOp "still"
        }
        test "Eine Antwort zur abgewählten Monatswahl ändert nichts" {
            let model, external = applyReloaded (loaded.MonthGeneration - 1) (Error "Timeout") loaded
            Expect.equal model loaded "verworfen"
            Expect.equal external NoOp "still"
        }
        test "Ohne geladene Liste zeigt ein Fehler wie sonst den Fehlerzustand" {
            let model, _ = applyReloaded 0 (Error "Timeout") (init ())
            Expect.equal model.Month (Failure "Timeout") "nichts zu erhalten"
        }
    ]

let private retryTests =
    let failing = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
    let done' = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Written ]
    let afterFailure = noteMoveReport "2026-11-01" failing loaded
    testList "Erneut senden bleibt erreichbar" [
        // budgeting-nfrds: nur der Toast trug die Aktion. Nach fünf Sekunden war sie weg.
        test "Eine fehlgeschlagene Verschiebung bleibt ohne Toast im Modell" {
            let model, _, _ = applyMoveAnswered (Ok failing) (sending ())
            Expect.equal
                (model.OpenRetries |> List.map (fun r -> r.ApplicationId, r.Reason, r.State))
                [ "app-1", "Rate-Limit", RetryReady ] "offen"
        }
        test "Der offene Hinweis nennt den Monat der Verschiebung" {
            let model = noteMoveReport "2026-10-01" failing loaded
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.MonthLabel)) [ "Oktober 2026" ] "Monat"
        }
        test "Eine gelungene Verschiebung hinterlässt nichts" {
            Expect.isEmpty (noteMoveReport "2026-11-01" done' loaded).OpenRetries "nichts offen"
        }
        test "Zwei fehlgeschlagene Verschiebungen verdrängen einander nicht" {
            let model = noteMoveReport "2026-11-01" { failing with ApplicationId = "app-2" } afterFailure
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.ApplicationId)) [ "app-1"; "app-2" ] "beide"
        }
        test "Ein zweiter Klick während des Laufs sendet nicht noch einmal" {
            let running, first = startMoveRetry "app-1" afterFailure
            Expect.isTrue first "der erste sendet"
            Expect.isFalse (snd (startMoveRetry "app-1" running)) "der zweite nicht"
        }
        test "Gelingt das erneute Senden, verschwindet der Hinweis" {
            let running, _ = startMoveRetry "app-1" afterFailure
            let model, external = applyMoveRetried "app-1" (Ok done') running
            Expect.isEmpty model.OpenRetries "erledigt"
            match external with
            | ShowToast (_, ToastSuccess) -> ()
            | other -> failtest $"Erfolg erwartet: {other}"
        }
        test "Bleibt eine Zeile fehlgeschlagen, bleibt der Hinweis mit dem neuen Grund" {
            let running, _ = startMoveRetry "app-1" afterFailure
            let still = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Netzwerkfehler: weg" ]
            let model, _ = applyMoveRetried "app-1" (Ok still) running
            Expect.equal
                (model.OpenRetries |> List.map (fun r -> r.Reason, r.State))
                [ "Netzwerkfehler: weg", RetryReady ] "wieder sendbar"
        }
        // budgeting-nfrds: `MoveRetried (Error _)` löste keinen Reload aus, der Knopf blieb tot.
        test "Scheitert der Aufruf, bleibt der Hinweis sendbar und ein Toast nennt den Grund" {
            let running, _ = startMoveRetry "app-1" afterFailure
            let model, external = applyMoveRetried "app-1" (Error "Timeout") running
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.State)) [ RetryReady ] "sendbar"
            Expect.equal external (ShowToast ("Erneut senden gescheitert: Timeout", ToastError)) "sichtbar"
        }
    ]

let private fieldKeyTests =
    let press (action: string -> FieldKeyAction) (key: string) (model: Model) =
        match action key with
        | KeepField
        | LeaveField None -> model
        | LeaveField (Some msg)
        | CancelField msg -> run [ msg ] model
    testList "Tasten in den Feldern" [
        test "Feld Zugewiesen: Enter speichert und geht, Esc verwirft und geht, der Rest bleibt beim Feld" {
            Expect.equal (assignKeyAction "Enter") (LeaveField (Some AssignFieldLeft)) "Enter"
            Expect.equal (assignKeyAction "Escape") (CancelField AssignDiscarded) "Esc"
            for key in [ "ArrowUp"; "ArrowDown"; "Tab"; "2" ] do
                Expect.equal (assignKeyAction key) KeepField key
        }
        test "Betragsfeld: Enter geht ohne Nachricht, Esc setzt zurück und geht, der Rest bleibt beim Feld" {
            Expect.equal (amountKeyAction "Enter") (LeaveField None) "Enter"
            Expect.equal (amountKeyAction "Escape") (CancelField TransferAmountReset) "Esc"
            for key in [ "ArrowUp"; "ArrowDown"; "Tab"; "2" ] do
                Expect.equal (amountKeyAction key) KeepField key
        }
        test "Enter im Feld Zugewiesen speichert die Eingabe" {
            let model = onFood |> run [ typed "+20" ] |> press assignKeyAction "Enter"
            Expect.equal (queued model) [ food.Id, monthKey, 920m ] "gespeichert"
        }
        test "Esc im Feld Zugewiesen zeigt den Stand, und der folgende Blur schreibt nichts" {
            let model = onFood |> run [ typed "+20" ] |> press assignKeyAction "Escape" |> run [ AssignFieldLeft ]
            Expect.equal (field food model) "900,00" "Stand"
            Expect.isEmpty model.AssignQueue "nichts geschrieben"
        }
        test "Esc im Betragsfeld setzt auf die Vorbelegung zurück, die Gegenseite bleibt" {
            let red = run [ TransferAmountChanged "*2"; PickCounterpart (CategorySide food.Id) ] (selected trip loaded)
            let model = press amountKeyAction "Escape" red
            Expect.equal (transferAmountText (monthOfModel model) (tabOf model)) "30,50" "Vorbelegung"
            Expect.equal (tabOf model).Transfer.Counterpart (Some (CategorySide food.Id)) "Gegenseite"
        }
        test "Enter im Betragsfeld sendet nichts" {
            let model = press amountKeyAction "Enter" takeReady
            Expect.equal model takeReady "unverändert"
        }
    ]

[<Tests>]
let tests =
    testList
        "Tab Geld, Client (budgeting-c5w2n)"
        [ fieldTests; leaveTests; queueTests; busyTests; unclearTests; settledTests; activeTests; missingTests; modeTests; transferTests; pickerTests
          keyTests; fieldKeyTests; openRetryTests; pageChangeTests; summaryTests; reloadTests; retryTests ]
