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
open Components.Budget.InspectorView

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private category name (assigned: decimal) (available: decimal) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur assigned
      Activity = eur 0m
      Available = eur available
      Underfunded = None
      Target = None
      Pause = None; OpenWriteBack = None; TargetInYnab = None; Deactivated = None }

let private food = category "Lebensmittel" 900m 80m
let private trip = category "Urlaub" 0m -30.5m
let private clothes = { category "Kleidung" 50m 100m with Underfunded = Some (eur 70m); Target = Some { Goal = eur 100m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None } }
let private monthKey = "2026-11-01"
let private monthOf (toBeBudgeted: decimal) (cats: BudgetCategory list) : BudgetMonth =
    { Month = monthKey
      ToBeBudgeted = eur toBeBudgeted
      Groups = [ { Name = "Alltag"; Categories = cats } ] }
let private month = monthOf 100m [ food; trip; clothes ]
let private frame : BudgetFrame =
    { CurrentMonth = monthKey
      Months = [ "2026-10-01"; monthKey; "2026-12-01" ]
      CategoryOrder = []
      MonthStartHidden = [] }

let private loaded = { init () with Frame = Success frame; Month = Success month }

let private update msg model =
    let model', _, external = Components.Budget.State.update msg model
    model', external

/// Ton und Text der Meldung einer Antwort, gleich an welchem Platz (budgeting-gtrgk): die Zeile im Sheet oder, bei
/// geschlossenem Sheet, die Leiste. Wo sie steht, prüft `sheetLineRouteTests`.
let private notice (model: Model, external: ExternalMsg) : (SheetLineTone * string) option =
    match external, model.SheetLine with
    | ShowToast (text, ToastWarning), _ -> Some (LineWarning, text)
    | ShowToast (text, ToastError), _
    | ShowToastWithAction (text, ToastError, _), _ -> Some (LineError, text)
    | ShowToast (text, ToastSuccess), _ -> Some (LineOk, text)
    | NoOp, Some line -> Some (line.Tone, line.Text)
    | _ -> None

/// Führt Nachrichten nacheinander aus und gibt das Modell zurück.
let private run (msgs: Msg list) (model: Model) : Model =
    msgs |> List.fold (fun current msg -> fst (update msg current)) model

/// Wie viele Aufrufe `update` anstößt. Ausführen lassen sie sich unter .NET nicht.
let private commands (msg: Msg) (model: Model) : int =
    let _, cmd, _ = Components.Budget.State.update msg model
    List.length cmd

let private selected (c: BudgetCategory) (m: Model) = run [ SelectCategory c.Id ] m

/// Die Taste wie in der View (`pageListAction`, dann `pageKeyOf`), ohne Umschalt, Zusatztaste und genommene Taste.
let private keyActionOf (model: Model) (key: string) (focus: KeyFocus) : PageKeyAction =
    pageKeyOf (pageListAction model key focus false false false)

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
        // budgeting-gtrgk: bleibt das Sheet offen, steht die Warnung in der Zeile, sonst in der Leiste.
        test "Ungültiger Text beim Verlassen: nichts geschrieben, Eingabe verworfen, gelbe Zeile oder Leiste" {
            let invalid = run [ typed "abc" ] onFood
            let warning = { Tone = LineWarning; Text = "Zugewiesen nicht gespeichert: keine gültige Rechnung"; Retry = None; Undo = None }
            for name, msg in
                [ "Tab", SelectInspectorTab EntriesTab
                  "Kategorie", SelectCategory trip.Id
                  "Monat", SelectMonth "2026-10-01"
                  "Modus", SwitchMoneyMode TakeMode ] do
                let model, external = update msg invalid
                Expect.isEmpty model.AssignQueue name
                Expect.equal (model.TabMoney |> Option.bind (fun t -> t.Entry)) None name
                Expect.equal (model.SheetLine, external) (Some warning, NoOp) $"{name}: in der Zeile"
            let closed, external = update CloseInspector invalid
            Expect.isEmpty closed.AssignQueue "Schließen"
            Expect.equal (closed.SheetLine, external) (None, ShowToast (warning.Text, ToastWarning)) "Schließen: in der Leiste"
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
        test "Fällt die Kategorie beim Neu-Lesen weg, ist die Eingabe verworfen und die Leiste nennt das, das Sheet ist zu" {
            let without = monthOf 100m [ trip ]
            let model, external = update (Loaded (edited.MonthGeneration, Ok without)) edited
            Expect.isNone model.TabMoney "weg"
            Expect.isEmpty model.AssignQueue "nichts geschrieben"
            Expect.equal external (ShowToast ("Zugewiesen nicht gespeichert: die Kategorie gibt es nicht mehr", ToastWarning)) "Leiste"
        }
        test "Während eines Monatswechsels nimmt das Feld keine Eingabe an, Schritte und Knöpfe tun nichts" {
            // Über `update`: seit budgeting-xp46a hat auch eine Kategorie ohne Target im laufenden Monat ein
            // Target-Formular, das der Monatswechsel erst in `update` verwirft.
            let changing = run [ SelectMonth "2026-12-01" ] onFood
            Expect.isTrue (monthChanging changing) "Vorbedingung"
            Expect.equal (run [ typed "+5"; AssignStepped 10m ] changing) changing "keine Eingabe"
            Expect.isTrue (assignFieldReadOnly changing) "readOnly, der Fokus bleibt"
            let red = { loaded with Month = Success month } |> selected trip
            let redChanging = run [ SelectMonth "2026-12-01" ] red
            Expect.equal (update CoverMissing redChanging |> fst) redChanging "kein Fehlbetrag"
        }
    ]

let private queueTests =
    let saved (c: BudgetCategory) text (model: Model) = run [ SelectCategory c.Id; typed text; AssignFieldLeft ] model
    let write (c: BudgetCategory) amount state : AssignWrite =
        { CategoryId = c.Id; Month = monthKey; Assigned = eur amount; State = state }
    let answer (model: Model) = update (AssignAnswered (Ok { ApplicationId = "a"; Month = monthKey; Lines = []; ActualToBeBudgeted = None })) model
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
            Expect.equal (commands ConfirmTransfer waiting) 0 "Holen"
            let red = { selected trip loaded with AssignQueue = [ write food 1m WriteRunning ] }
            Expect.equal (commands CoverMissing { red with TabMoney = red.TabMoney |> Option.map (fun t -> { t with Mode = AssignMode }) }) 0 "Fehlbetrag"
        }
        test "Error: die rote Zeile nennt den Grund, die Schlange läuft weiter, der Monat wird neu gelesen" {
            let model = onFood |> saved food "+1" |> saved trip "1"
            let next, external = update (AssignAnswered (Error "Monat nicht offen")) model
            Expect.equal (notice (next, external)) (Some (LineError, "Nicht zugewiesen: Monat nicht offen")) "Grund"
            Expect.equal (next.AssignQueue |> List.map (fun w -> w.State)) [ WriteRunning ] "weiter"
            let last, _ = update (AssignAnswered (Error "x")) next
            Expect.equal last.MonthGeneration (next.MonthGeneration + 1) "neu gelesen"
        }
        test "Aufruf abgebrochen: rot Ob zugewiesen wurde ist unklar, Monat neu lesen" {
            let model = onFood |> saved food "+1"
            let next, external = update (AssignCallLost "Timeout") model
            Expect.equal (notice (next, external)) (Some (LineError, "Ob zugewiesen wurde, ist unklar: Timeout. Der Stand wird gleich neu gelesen.")) "ehrlich"
            Expect.isEmpty next.AssignQueue "leer"
            Expect.equal next.MonthGeneration (model.MonthGeneration + 1) "neu gelesen"
        }
        test "Bericht ohne Zeilen und Erfolg zeigen keinen Toast" {
            let model = onFood |> saved food "+1"
            Expect.equal (snd (answer model)) NoOp "ohne Zeilen"
            let written = report [ "Lebensmittel", 900m, 901m, Written ]
            Expect.equal (snd (update (AssignAnswered (Ok written)) model)) NoOp "Erfolg"
        }
        test "Eine Zuweisung im letzten Monat zieht den Rahmen still nach" {
            // Der Cmd läuft unter .NET nicht, gezählt wird: Neu-Lesen des Monats, dazu der Rahmen.
            let model = onFood |> saved food "+1"
            let last = { (report [ "Lebensmittel", 900m, 901m, Written ]) with Month = "2026-12-01" }
            let inner = report [ "Lebensmittel", 900m, 901m, Written ]
            Expect.equal (commands (AssignAnswered (Ok last)) model) 2 "letzter Monat"
            Expect.equal (commands (AssignAnswered (Ok inner)) model) 1 "Monat davor"
            Expect.equal (commands (AssignAnswered (Error "x")) model) 1 "nichts geschrieben"
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

/// Miete mit offener Rückschreibung (budgeting-dgrp7): Zugewiesen ist BBs Soll, YNAB hat es noch nicht.
let private markedGuid = Guid "6d1f0c2a-0000-4000-8000-0000000d6a07"
let private markedId = string markedGuid
let private markedRent =
    let marked = { rent with OpenWriteBack = Some markedGuid }
    run [ SelectCategory rent.Id; SwitchMoneyMode AssignMode ] { loaded with Month = Success (monthOf 100m [ food; trip; marked ]) }

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
            let tried = run [ RetryMove "app-1"; ConfirmTransfer ] sendingWithEntry
            Expect.equal { tried with SheetLine = None } sendingWithEntry "kein Start, nichts gespeichert"
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
            Expect.equal (commands CoverMissing retrying) 0 "Fehlbetrag"
            Expect.isTrue (inputActive retrying) "ein Neuladen zerstörte den Lauf"
        }
        test "Erneut senden startet nicht, solange die Schlange läuft oder ein Aufruf aus dem Tab" {
            let queued = run [ SelectCategory food.Id; typed "+1"; AssignFieldLeft; RetryMove "app-2" ] failedCover
            Expect.equal (queued.OpenRetries |> List.map (fun r -> r.State)) [ RetryReady ] "Schlange"
            let sending = run [ Reloaded (failedCover.MonthGeneration, Ok rentMonth); CoverMissing; RetryMove "app-2" ] failedCover
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
            Expect.equal (commands CoverMissing lost) 0 "kein zweiter Knopf"
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
        { Month = monthKey; Lines = [ targetLine ]; Skipped = []; Total = eur 12.3m; ToBeBudgeted = eur 100m; ToBeBudgetedAfter = eur 87.7m; CommandId = None }
    let failedReport = { reportFrom 900m with Lines = [ line rent 900m 912.3m (Failed "weg") ] }
    // Zwei Zeilen: Eine Verschiebung bleibt offen, wenn dieselbe Kategorie neu zugewiesen wird (E1).
    [ { Name = "Erneut senden"
        Base = run [ CoverMissing; answered [ line food 900m 900m Written; line rent 900m 912.3m (Failed "weg") ] ] onRent
        Arm = id
        Start = RetryMove "app-2"
        Landed = fun stand -> MoveRetried ("app-2", Ok (reportFrom stand))
        Lost = MoveRetried ("app-2", Error "weg") }
      { Name = "Übernehmen"
        Base = onRent
        Arm = fun model -> { model with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
        Start = ConfirmTargets
        Landed = fun stand -> TargetsApplied (Ok (Applied (reportFrom stand)))
        Lost = TargetsApplied (Error "weg") }
      { Name = "Fehlgeschlagene erneut senden"
        Base = onRent
        Arm = fun model -> { model with Targets = ApplyDone { Sent = preview; Report = failedReport; Retrying = false; RetryError = None } }
        Start = RetryFailed
        Landed = fun stand -> TargetsRetried (Ok (reportFrom stand))
        Lost = TargetsRetried (Error "weg") }
      { Name = "Holen"
        Base = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "12,30"; PickCounterpart ToBeBudgetedSide ] onRent
        Arm = id
        Start = ConfirmTransfer
        Landed = fun stand -> MoveAnswered (Ok (reportFrom stand))
        Lost = MoveCallLost "weg" }
      // budgeting-dgrp7, Review-Fund: "Erneut senden" an der Markierung "noch nicht in YNAB" lief ohne
      // Laufzustand. Zuweisen, Verschieben und Fehlbetrag blieben während des Aufrufs offen (ADR 0078 Punkt 5),
      // ein Doppeltipp sendete zweimal.
      { Name = "Erneut senden an der Markierung"
        Base = markedRent
        Arm = id
        Start = RetryMove markedId
        Landed = fun stand -> MoveRetried (markedId, Ok { reportFrom stand with ApplicationId = markedId })
        Lost = MoveRetried (markedId, Error "weg") } ]

/// Der Stand in YNAB als Monat.
let private monthWith (stand: decimal) : BudgetMonth =
    monthOf (100m - (stand - 900m)) [ food; trip; { rent with Assigned = eur stand; Available = eur (stand - 912.3m) } ]

/// Der Fehlbetrag-Knopf startet auch, verwirft aber die Eingabe statt sie zu speichern. Die Targets-Wege
/// stehen eine Stufe tiefer.
let private coverWay : MoveWay =
    { Name = "Fehlbetrag"
      Base = onRent
      Arm = id
      Start = CoverMissing
      Landed = fun stand ->
        MoveAnswered (Ok { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent stand (stand + 12.3m) Written ]; ActualToBeBudgeted = None })
      Lost = MoveCallLost "weg" }

let private assignDone (_: Model) : Msg =
    AssignAnswered (Ok { ApplicationId = "a"; Month = monthKey; Lines = []; ActualToBeBudgeted = None })

/// Je Weg die Tiefe der Aufzählung und wie viele Zuweisungen sie mindestens erreichen muss.
let private moveWalks : (MoveWay * int * int) list =
    [ coverWay, 6, 400
      moveWays[0], 6, 400
      moveWays[1], 7, 60
      moveWays[2], 7, 60
      moveWays[3], 6, 400
      moveWays[4], 6, 400 ]

/// Was in YNAB steht, wie die Aufzählung es kennt.
type private Stand = { Rent: decimal; Food: decimal }

/// Wo die Aufzählung steht: Stand in YNAB und ob eine Bewegung auf ihre Antwort wartet. Der Merker ist
/// eigen: Der Test soll nicht dem Prädikat glauben, das er prüft.
type private WalkState = { Stand: Stand; Pending: bool }

/// Der Monat, wie YNAB ihn liefert.
let private monthAt (stand: Stand) : BudgetMonth =
    let rentNow = { rent with Assigned = eur stand.Rent; Available = eur (stand.Rent - 912.3m) }
    monthOf (100m - (stand.Rent - 900m) - (stand.Food - 900m)) [ { food with Assigned = eur stand.Food }; trip; rentNow ]

/// Alle Ereignisse, die in diesem Zustand kommen können, mit dem Stand danach.
let private events (way: MoveWay) (model: Model) (state: WalkState) : (string * Msg * Stand) list =
    let stand = state.Stand
    let landed = { stand with Rent = stand.Rent + 12.3m }
    let withoutRent = monthOf 100m [ { food with Assigned = eur stand.Food }; trip ]
    [ "Schritt", AssignStepped 10m, stand
      "Tippen", typed "+5", stand
      "Verlassen", AssignFieldLeft, stand
      "Esc", AssignDiscarded, stand
      "Start", way.Start, stand
      "Seite weg", PageLeft, stand
      "Seite da", PageEntered, stand
      "Wählen", SelectCategory rent.Id, stand
      "Andere Kategorie", SelectCategory food.Id, stand
      "Schließen", CloseInspector, stand
      "Modus Zuweisen", SwitchMoneyMode AssignMode, stand
      "Modus Holen", SwitchMoneyMode TakeMode, stand
      "Neu-Lesen", Reloaded (model.MonthGeneration, Ok (monthAt stand)), stand
      "Neu-Lesen ohne Kategorie", Reloaded (model.MonthGeneration, Ok withoutRent), stand
      if model.Targets <> TargetsClosed then
          "Dialog zu", CloseTargets, stand
      if state.Pending then
          "Antwort", way.Landed stand.Rent, landed
          // Ein abgebrochener Aufruf gilt als angekommen.
          "Abbruch", way.Lost, landed
      match model.AssignQueue with
      | head :: _ ->
          let written =
              if head.CategoryId = rent.Id then { stand with Rent = head.Assigned.Amount }
              else { stand with Food = head.Assigned.Amount }
          "Zuweisung fertig", assignDone model, written
      | [] -> () ]

/// Prüft einen Schritt und gibt zurück, wie viele neue Zuweisungen nach der Bewegung er brachte.
let private check (way: MoveWay) (path: string list) (before: Model) (after: Model) (state: WalkState) (started: bool) : int =
    let shown = String.concat ", " (List.rev path)
    if started then
        Expect.isEmpty after.AssignQueue $"{way.Name}: Schlange beim Start: {shown}"
        Expect.isTrue (after.TabMoney |> Option.forall (fun tab -> tab.Entry.IsNone)) $"{way.Name}: Eingabe beim Start: {shown}"
    let isNew (w: AssignWrite) =
        not (before.AssignQueue |> List.exists (fun o -> o.CategoryId = w.CategoryId && o.Assigned = w.Assigned))
    let fresh = after.AssignQueue |> List.filter isNew
    for write in fresh do
        Expect.isFalse state.Pending $"{way.Name}: {write.Assigned.Amount} während der Bewegung: {shown}"
        // Schritte und Eingaben sind relativ, in Fünfern: Alles andere rechnet von einem alten Stand.
        let stand = if write.CategoryId = rent.Id then state.Stand.Rent else state.Stand.Food
        Expect.equal ((write.Assigned.Amount - stand) % 5m) 0m $"{way.Name}: {write.Assigned.Amount} bei Stand {stand}: {shown}"
    fresh.Length

/// Geht alle Folgen bis `depth` ab und zählt die Zuweisungen nach einer Bewegung. Ein Zustand, der
/// schon mit gleicher oder größerer Resttiefe besucht wurde, bringt nichts Neues.
let private walk (way: MoveWay) (depth: int) : int =
    let visited = Collections.Generic.Dictionary<Model * WalkState * bool, int> HashIdentity.Structural
    let mutable writes = 0
    let rec go (left: int) (path: string list) (model: Model) (state: WalkState) (moved: bool) =
        for name, msg, stand' in events way model state do
            let after, cmd, _ = Components.Budget.State.update msg model
            let started = name = "Start" && not state.Pending && not cmd.IsEmpty && List.isEmpty after.AssignQueue
            let answered = name = "Antwort" || name = "Abbruch"
            let state' = { Stand = stand'; Pending = started || (state.Pending && not answered) }
            let moved' = moved || started
            let path' = name :: path
            let fresh = check way path' model after state started
            if moved' then writes <- writes + fresh
            let key = after, state', moved'
            let seen, deepest = visited.TryGetValue key
            if left > 1 && not (seen && deepest >= left - 1) then
                visited[key] <- left - 1
                go (left - 1) path' after state' moved'
    let start = way.Arm way.Base
    go depth [] start { Stand = { Rent = 900m; Food = 900m }; Pending = false } false
    writes

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
        test "Läuft die Schlange oder eine Bewegung, bleibt alles stehen und eine gelbe Meldung nennt es" {
            let blocked = Some (LineWarning, moveBlockedText)
            forEachWay (fun way ->
                let busy = armedWith [ typed "+1"; AssignFieldLeft ] way
                let model, external = update way.Start busy
                Expect.equal { model with SheetLine = None } { busy with SheetLine = None } way.Name
                Expect.equal (notice (model, external)) blocked $"{way.Name}: Schlange"
                let running = run [ way.Start ] (armedWith [] way)
                let again, repeated = update way.Start running
                Expect.equal { again with SheetLine = None } { running with SheetLine = None } way.Name
                Expect.equal (notice (again, repeated)) blocked $"{way.Name}: Bewegung")
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
                Expect.equal (notice (update way.Start waiting)) (Some (LineWarning, moveBlockedText)) way.Name)
        }
        test "Ohne Eingabe und mit leerer Schlange startet die Bewegung" {
            forEachWay (fun way ->
                let running = run [ way.Start ] (armedWith [] way)
                Expect.isTrue (moveRunning running) way.Name
                Expect.isFalse (canStartMove running) $"{way.Name}: Knopf gesperrt, solange sie läuft")
        }
        // budgeting-dgrp7, Review-Fund: "Erneut senden" an der Markierung sendete bei jedem Tipp, der Knopf zeigte
        // keinen Lauf, und ein offener Hinweis derselben Gruppe blieb sendbar.
        test "Erneut senden an der Markierung: ein zweiter Tipp sendet nichts, der Knopf zeigt den Lauf, die Antwort gibt frei" {
            let start = RetryMove markedId
            Expect.equal (commands start markedRent) 1 "der erste Tipp sendet"
            let running = run [ start ] markedRent
            Expect.equal (commands start running) 0 "Doppeltipp"
            Expect.isTrue (writeBackRetryRunning markedId running) "der Knopf zeigt den Lauf"
            let answered = run [ MoveRetried (markedId, Error "weg") ] running
            Expect.isFalse (writeBackRetryRunning markedId answered) "die Antwort setzt ihn zurück"
            Expect.isFalse (moveRunning answered) "frei"
            let hint =
                { ApplicationId = markedId
                  Headline = "Nicht zugewiesen"
                  MonthLabel = "November 2026"
                  Summary = "Miete"
                  Reason = "weg"
                  State = RetryReady
                  AssignTarget = Some (rent.Id, monthKey) }
            let withHint = run [ start ] { markedRent with OpenRetries = [ hint ] }
            Expect.equal (withHint.OpenRetries |> List.map (fun r -> r.State)) [ RetryRunning ] "der Hinweis derselben Gruppe läuft mit"
            Expect.equal (commands (RetryMove markedId) withHint) 0 "sein Knopf sendet nicht noch einmal"
            Expect.equal (commands start (run [ Reloaded (markedRent.MonthGeneration, Ok rentMonth) ] markedRent)) 0 "ohne Markierung nichts zu senden"
        }
        test "Eine Eingabe ohne Änderung fällt weg, die Bewegung startet im selben Tipp" {
            forEachWay (fun way -> Expect.isTrue (moveRunning (run [ way.Start ] (armedWith [ typed "+0" ] way))) way.Name)
        }
        test "Eine ungültige Eingabe fällt mit Warnung weg, die Bewegung startet nicht" {
            forEachWay (fun way ->
                let model, external = update way.Start (armedWith [ typed "abc" ] way)
                Expect.equal (notice (model, external)) (Some (LineWarning, invalidEntryText)) way.Name
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
            // Die Auswahl wartete auf das Ende des Aufrufs, das Sheet ist zu: die Leiste nennt beide Seiten.
            Expect.equal (model.SheetLine, external) (None, ShowToast ("12,30 € von Zu verteilen nach Miete", ToastSuccess)) "Leiste"
            let free = run [ Reloaded (model.MonthGeneration, Ok (monthWith 912.3m)); SelectCategory rent.Id; SwitchMoneyMode AssignMode; AssignStepped 10m; AssignFieldLeft ] model
            Expect.equal (queued free) [ rent.Id, monthKey, 922.3m ] "auf dem neuen Stand"
        }
        // A2: Der Merker des Aufrufs hing am Tab. Fiel die Kategorie beim Neu-Lesen weg, sprang `moveRunning`
        // auf falsch, das Feld war frei, und 910 hätte die 12,30 zurückgenommen.
        test "Fällt die Kategorie während des Aufrufs weg, sperrt der Aufruf weiter" {
            let covering = run [ CoverMissing ] onRent
            let gone = run [ Reloaded (covering.MonthGeneration, Ok (monthOf 100m [ food ])) ] covering
            Expect.isNone gone.TabMoney "Vorbedingung: der Tab ist weg"
            let back = run [ Reloaded (gone.MonthGeneration, Ok rentMonth); SelectCategory rent.Id; AssignStepped 10m; AssignFieldLeft ] gone
            Expect.isEmpty back.AssignQueue "nichts geschrieben"
            Expect.isFalse (moveRunning (run [ answered [ line rent 900m 912.3m Written ] ] back)) "die Antwort beendet ihn"
        }
        test "Ohne laufenden Aufruf beginnt die Seite beim Betreten neu" {
            let model = run [ PageEntered ] onRent
            Expect.isNone model.Selected "Auswahl weg"
            Expect.isNone model.TabMoney "Tab weg"
        }
        test "Keine Reihenfolge schreibt einen Wert, der von einem Stand vor der Bewegung rechnet" {
            // Aufzählung über alle Ereignisfolgen, auch über Seitenwechsel und eine fehlende Kategorie. Die
            // Targets-Wege brauchen eine Stufe mehr, ihr Bericht muss erst geschlossen und der Monat gelesen sein.
            for way, depth, minimum in moveWalks do
                let writes = walk way depth
                Expect.isGreaterThanOrEqual writes minimum $"{way.Name}: so viele Zuweisungen nach der Bewegung"
        }
    ]

/// Die Review-Funde zu budgeting-c5w2n, je Fund ein kaputt- und ein intakt-Szenario.
let private reviewTests =
    /// Verfügbar 0, Fehlbetrag 100, 500 zu verteilen. Der Fehlbetrag kommt von YNAB.
    let gap = { category "Reparatur" 200m 0m with Underfunded = Some (eur 100m); Target = Some { Goal = eur 300m; Rhythm = Months 1; Kind = Need; Date = None; NeedsWholeAmount = None; Day = None; CreatedIn = None } }
    let gapMonth = monthOf 500m [ gap; food ]
    let onGap = run [ SelectCategory gap.Id ] { loaded with Month = Success gapMonth }
    let gapReport = { ApplicationId = "gap"; Month = monthKey; Lines = [ line gap 200m 300m Written ]; ActualToBeBudgeted = None }
    let gapFilled = monthOf 400m [ { gap with Assigned = eur 300m; Available = eur 100m; Underfunded = Some (eur 0m) }; food ]
    let failedAssign (c: BudgetCategory) (appId: string) (oldA: decimal) (newA: decimal) : ApplyReport =
        { ApplicationId = appId; Month = monthKey; Lines = [ line c oldA newA (Failed "Rate-Limit") ]; ActualToBeBudgeted = None }
    let rejected = run [ ConfirmTransfer; MoveAnswered (Error "abgelehnt") ] takeReady
    let submission (model: Model) = (tabOf model).Transfer.Submission
    testList "Review-Funde" [
        // A1, Doppelbuchung: `withAssigned` lässt `Underfunded` stehen, der Knopf bot nach dem Erfolg den
        // alten Betrag an, ein zweiter Tipp sendete `moveMoney` noch einmal und buchte doppelt.
        test "Fehlbetrag 100, Knopf, Antwort da: ein zweiter Tipp sendet nichts, bis das Neu-Lesen da ist" {
            let covered = run [ CoverMissing; MoveAnswered (Ok gapReport) ] onGap
            Expect.equal (commands CoverMissing covered) 0 "veraltet"
            let free = run [ Reloaded (covered.MonthGeneration, Ok gapMonth) ] covered
            Expect.equal (commands CoverMissing free) 1 "gelesen: wirkt wieder"
        }
        test "Nach dem Speichern und bei leerer Schlange ist der Fehlbetrag veraltet, bis das Neu-Lesen da ist" {
            let saved = run [ typed "+50"; AssignFieldLeft ] onGap
            let idle = run [ AssignAnswered (Ok gapReport) ] saved
            Expect.isEmpty idle.AssignQueue "Vorbedingung"
            Expect.equal (commands CoverMissing idle) 0 "veraltet"
            let lost = run [ typed "+50"; AssignFieldLeft; AssignCallLost "Timeout" ] onGap
            Expect.equal (commands CoverMissing lost) 0 "unklar"
        }
        test "Scheitert das Neu-Lesen, bleibt der Knopf gesperrt, bis eines gelingt" {
            let covered = run [ CoverMissing; MoveAnswered (Ok gapReport) ] onGap
            let failed = run [ Reloaded (covered.MonthGeneration, Error "weg") ] covered
            Expect.equal (commands CoverMissing failed) 0 "gesperrt"
            let older = run [ Reloaded (failed.MonthGeneration - 1, Ok gapMonth) ] failed
            Expect.equal (commands CoverMissing older) 0 "eine ältere Antwort gibt nicht frei"
            let read = run [ Reloaded (failed.MonthGeneration, Ok gapMonth) ] failed
            Expect.equal (commands CoverMissing read) 1 "frei"
        }
        test "Veraltet heißt nur Fehlbetrag: Holen, Feld und Liste bleiben" {
            let covered = run [ CoverMissing; MoveAnswered (Ok gapReport) ] onGap
            Expect.isFalse (assignFieldReadOnly covered) "Feld"
            Expect.equal (categoryIn covered gap).Assigned (eur 300m) "Liste"
            let ready = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5"; PickCounterpart ToBeBudgetedSide ] covered
            Expect.isTrue (canTransfer ready (monthOfModel ready) (tabOf ready)) "Holen"
        }
        // A3: ein zweiter Eintrag nähme beim Erneut senden die jüngere Zuweisung zurück.
        test "Zwei gescheiterte Zuweisungen 200 und 250 bei Stand 100: nur 250 ist offen" {
            let low = category "Bahn" 100m 100m
            let start = run [ SelectCategory low.Id ] { loaded with Month = Success (monthOf 500m [ low ]) }
            let first = run [ typed "200"; AssignFieldLeft ] start
            let failedFirst = run [ AssignAnswered (Ok (failedAssign low "a" 100m 200m)) ] first
            let reread = run [ Reloaded (failedFirst.MonthGeneration, Ok (monthOf 500m [ low ])) ] failedFirst
            let second = run [ typed "250"; AssignFieldLeft ] reread
            Expect.isEmpty second.OpenRetries "der alte Hinweis fällt beim Speichern weg"
            let failedSecond = run [ AssignAnswered (Ok (failedAssign low "b" 100m 250m)) ] second
            Expect.equal (failedSecond.OpenRetries |> List.map (fun r -> r.ApplicationId)) [ "b" ] "nur der jüngste"
        }
        test "Beide unterwegs: die ältere Absage hinterlässt keinen Hinweis" {
            let low = category "Bahn" 100m 100m
            let start = run [ SelectCategory low.Id ] { loaded with Month = Success (monthOf 500m [ low ]) }
            let both = run [ typed "200"; AssignFieldLeft; typed "250"; AssignFieldLeft ] start
            let older = run [ AssignAnswered (Ok (failedAssign low "a" 100m 200m)) ] both
            Expect.isEmpty older.OpenRetries "eine neuere wartet"
        }
        test "Den alten Stand tippen schreibt nichts, der Hinweis bleibt, andere Kategorien bleiben" {
            let low = category "Bahn" 100m 100m
            let start = run [ SelectCategory low.Id ] { loaded with Month = Success (monthOf 500m [ low; food ]) }
            let first = run [ typed "200"; AssignFieldLeft ] start
            let failed = run [ AssignAnswered (Ok (failedAssign low "a" 100m 200m)) ] first
            let reread = run [ Reloaded (failed.MonthGeneration, Ok (monthOf 500m [ low; food ])) ] failed
            let same = run [ typed "100"; AssignFieldLeft ] reread
            Expect.isEmpty same.AssignQueue "keine Zuweisung"
            Expect.equal (same.OpenRetries |> List.map (fun r -> r.ApplicationId)) [ "a" ] "der Hinweis bleibt"
            let other = run [ SelectCategory food.Id; typed "+1"; AssignFieldLeft ] reread
            Expect.equal (other.OpenRetries |> List.map (fun r -> r.ApplicationId)) [ "a" ] "andere Kategorie"
        }
        test "Scheitert der Fehlbetrag-Knopf nach einer gescheiterten Zuweisung, bleibt nur der jüngste Hinweis" {
            let first = run [ typed "950"; AssignFieldLeft ] onRent
            let failed = run [ AssignAnswered (Ok (failedAssign rent "a" 900m 950m)) ] first
            let reread = run [ Reloaded (failed.MonthGeneration, Ok rentMonth) ] failed
            let cover (outcome: LineOutcome) =
                MoveAnswered (Ok { ApplicationId = "b"; Month = monthKey; Lines = [ line rent 900m 912.3m outcome ]; ActualToBeBudgeted = None })
            let again = run [ CoverMissing; cover (Failed "weg") ] reread
            Expect.equal (again.OpenRetries |> List.map (fun r -> r.ApplicationId)) [ "b" ] "ersetzt"
            Expect.isEmpty (run [ CoverMissing; cover Written ] reread).OpenRetries "erledigt"
        }
        test "Eine Verschiebung bleibt offen, auch wenn eine ihrer Kategorien neu zugewiesen wird" {
            let failedMove = report [ "Lebensmittel", 900m, 880m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
            let model = noteMoveReport monthKey failedMove onFood
            Expect.equal (run [ typed "+1"; AssignFieldLeft ] model).OpenRetries.Length 1 "bleibt"
        }
        // A4: der Monat rückte am Monatsersten weiter, die Eingabe fiel weg, und der Toast behauptete,
        // die Kategorie gebe es nicht mehr.
        test "Rückt das Nachlesen den Monat weiter, wird die offene Eingabe in den alten Monat gespeichert" {
            let edited = run [ typed "+20" ] onFood
            let model, external = update (FrameRefreshed (Ok { frame with CurrentMonth = "2026-12-01" })) edited
            Expect.equal (queued model) [ food.Id, monthKey, 920m ] "gespeichert"
            Expect.equal external NoOp "kein falscher Grund"
            Expect.isNone (tabOf model).Entry "die Eingabe ist weg"
        }
        test "Rückt das Nachlesen den Monat weiter, nennt die gelbe Meldung die ungültige Eingabe" {
            let moved = update (FrameRefreshed (Ok { frame with CurrentMonth = "2026-12-01" })) (run [ typed "abc" ] onFood)
            Expect.equal (notice moved) (Some (LineWarning, invalidEntryText)) "der echte Grund"
        }
        test "Ändert das Nachlesen den Monat nicht, bleibt die Eingabe offen" {
            let edited = run [ typed "+20" ] onFood
            let model = run [ FrameRefreshed (Ok frame) ] edited
            Expect.isEmpty model.AssignQueue "nichts gespeichert"
            Expect.isSome (tabOf model).Entry "steht"
        }
        // A5: `Rejected` hatte keinen Ausgang, die Karte blieb stehen, auch im Modus Zuweisen und
        // über jeden Seitenwechsel: `keepsTab` hielt die Seite an einer längst gelesenen Absage fest.
        test "Jede Änderung der Form und jeder Moduswechsel nimmt die Absage weg" {
            Expect.equal (submission rejected) (Rejected "abgelehnt") "Vorbedingung"
            for msg in [ TransferAmountChanged "21"; TransferAmountReset; PickCounterpart ToBeBudgetedSide; SwitchMoneyMode AssignMode; SwitchMoneyMode GiveMode ] do
                Expect.equal (submission (run [ msg ] rejected)) Editing $"{msg}"
        }
        test "Ein erneuter Tipp auf den Fehlbetrag-Knopf nimmt die Absage weg" {
            let refused = run [ CoverMissing; MoveAnswered (Error "abgelehnt") ] onRent
            Expect.equal (submission refused) (Rejected "abgelehnt") "Vorbedingung"
            Expect.equal (submission (run [ CoverMissing ] refused)) Editing "der Knopf läuft wieder"
        }
        test "Eine gelesene Absage hält die Seite beim Wiederkommen nicht fest" {
            let model = run [ PageLeft; PageEntered ] rejected
            Expect.isNone model.TabMoney "weg"
            Expect.isNone model.Selected "die Seite beginnt neu"
        }
        test "Eine Absage, die kam, während die Seite verlassen war, steht beim Wiederkommen einmal da" {
            let once = run [ ConfirmTransfer; PageLeft; MoveAnswered (Error "abgelehnt"); PageEntered ] takeReady
            Expect.equal (submission once) (Rejected "abgelehnt") "im Tab"
            Expect.isSome once.Selected "zu sehen"
            Expect.isNone (run [ PageLeft; PageEntered ] once).TabMoney "beim nächsten Mal nicht mehr"
        }
        // A6: `ReturnAbandoned` fiel unter die Sperre, die Auswahl wartete für immer auf die Rückkehr.
        test "ReturnAbandoned speichert die Eingabe" {
            let model = run [ typed "+20"; ReturnAbandoned ] onFood
            Expect.equal (queued model) [ food.Id, monthKey, 920m ] "gespeichert"
            Expect.isNone model.Selected "die Auswahl wartet nicht mehr"
        }
        test "ReturnAbandoned wirkt auch, solange ein Aufruf läuft" {
            let model = run [ CoverMissing; OpenEntryInLedger (Guid.NewGuid()); ReturnAbandoned ] onRent
            Expect.isNone model.Selected "wartet nicht mehr"
        }
        // A7: `applyLoaded` kannte die Schlange nicht. Die Antwort brachte 900 zurück, während das PATCH
        // für 920 lief, und +10 wurde 910 statt 930.
        test "Eine Monatsantwort während des Schreibens trägt die Schlange darüber" {
            let saved = run [ typed "+20"; AssignFieldLeft ] onFood
            let back = run [ PageLeft; PageEntered; FrameLoaded (Ok frame) ] saved
            let read = run [ Loaded (back.MonthGeneration, Ok month); SelectCategory food.Id ] back
            Expect.equal (categoryIn read food).Assigned (eur 920m) "Zugewiesen"
            Expect.equal (categoryIn read food).Available (eur 100m) "Verfügbar"
            Expect.equal (monthOfModel read).ToBeBudgeted (eur 80m) "Zu verteilen"
            let added = run [ typed "+10"; AssignFieldLeft ] read
            Expect.equal (queued added) [ food.Id, monthKey, 920m; food.Id, monthKey, 930m ] "930, nicht 910"
        }
        test "Auch eine wartende Zuweisung bleibt stehen, eine Antwort für einen anderen Monat bleibt unberührt" {
            let saved = run [ typed "+20"; AssignFieldLeft; typed "+10"; AssignFieldLeft ] onFood
            let read = run [ Loaded (saved.MonthGeneration, Ok month) ] saved
            Expect.equal (categoryIn read food).Assigned (eur 930m) "die letzte zählt"
            let other = { read with ChosenMonth = Some "2026-12-01" }
            let december = { month with Month = "2026-12-01" }
            Expect.equal (categoryIn (run [ Loaded (other.MonthGeneration, Ok december) ] other) food).Assigned (eur 900m) "anderer Monat"
        }
        test "Nach der Bestätigung zählt wieder allein die Antwort" {
            let saved = run [ typed "+20"; AssignFieldLeft ] onFood
            let confirmed = run [ AssignAnswered (Ok { gapReport with Lines = [] }) ] saved
            let read = run [ Reloaded (confirmed.MonthGeneration, Ok month) ] confirmed
            Expect.equal (categoryIn read food).Assigned (eur 900m) "der gelesene Stand"
        }
        // A8: die Eingabe fiel still weg, Roman erfuhr nicht, dass sein +5 nie gespeichert wurde.
        test "Der Fehlbetrag-Knopf verwirft eine offene Eingabe und meldet das" {
            let model, external = update CoverMissing (run [ typed "+5" ] onRent)
            Expect.isNone (tabOf model).Entry "verworfen"
            Expect.isEmpty model.AssignQueue "nicht gespeichert"
            Expect.equal (model.SheetLine, external) (Some { Tone = LineWarning; Text = coverDroppedEntryText; Retry = None; Undo = None }, NoOp) "in der Zeile gemeldet"
        }
        test "Ohne Änderung im Feld und ohne gesendeten Auftrag gibt es keine Meldung" {
            Expect.equal (snd (update CoverMissing (run [ typed "+0" ] onRent))) NoOp "nichts verloren"
            Expect.equal (snd (update CoverMissing onRent)) NoOp "ohne Eingabe"
            let poor = run [ SelectCategory rent.Id; SwitchMoneyMode AssignMode; typed "+5" ] { loaded with Month = Success (monthOf 1m [ rent ]) }
            let model, external = update CoverMissing poor
            Expect.equal external NoOp "kein Auftrag"
            Expect.isSome (tabOf model).Entry "die Eingabe steht noch"
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
                Expect.equal request { Source = ToBeBudgetedSide; Target = CategorySide minus.Id; Amount = eur 12.3m; CommandId = None } "Auftrag"
            | None -> failtest "Auftrag erwartet"
        }
        test "Das Größere von Minus und Fehlbetrag zählt" {
            let c = { minus with Available = eur -5m; Underfunded = Some (eur 20m) }
            Expect.equal (missingActionText (actionOf 100m c)) "20,00 zuweisen" "Fehlbetrag"
        }
        test "Pausiertes Target: der Fehlbetrag zählt nicht" {
            let paused = { minus with Available = eur 10m; Underfunded = Some (eur 20m); Pause = Some PausedInBb }
            Expect.equal (actionOf 100m paused) NothingMissing "kein Knopf"
            Expect.equal (missingActionText (actionOf 100m { paused with Available = eur -5m })) "5,00 zuweisen" "nur das Minus"
            Expect.equal (actionOf 100m { minus with Available = eur 10m }) NothingMissing "nichts fehlt"
        }
        test "Reicht Zu verteilen nicht, steht der Rest da, ohne Auftrag" {
            let c = { minus with Available = eur -80m }
            Expect.equal (missingActionText (actionOf 60.2m c)) "Nur noch 60,20 zu verteilen" "Text"
            Expect.equal (commands CoverMissing (onCategory 60.2m c)) 0 "kein Auftrag"
            Expect.equal (actionOf 80m c) (AssignMissing (eur 80m)) "genau gleich wirkt"
            Expect.equal (missingActionText (actionOf -5m c)) "Nur noch 0,00 zu verteilen" "negativ"
        }
        test "Offene Eingabe plus Knopf: die Eingabe fällt weg, zwei Tipps ergeben einen Auftrag" {
            let model = run [ SwitchMoneyMode AssignMode; typed "+5" ] (onCategory 100m minus)
            Expect.equal (commands CoverMissing model) 1 "ein Auftrag"
            let sent = run [ CoverMissing ] model
            Expect.isNone (tabOf sent).Entry "Eingabe weg"
            Expect.isEmpty sent.AssignQueue "kein Zuweisen"
            Expect.equal (commands CoverMissing sent) 0 "kein zweiter"
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
            let noSide = run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5" ] onFood
            Expect.isFalse (canTransfer noSide month (tabOf noSide)) "ohne Gegenseite"
            for text in [ ""; "abc"; "0"; "0,00"; "0-5" ] do
                let model = run [ TransferAmountChanged text ] takeReady
                Expect.isFalse (canTransfer model month (tabOf model)) text
                Expect.equal (commands ConfirmTransfer model) 0 text
            let busy = sending ()
            Expect.isFalse (canTransfer busy month (tabOf busy)) "Aufruf"
        }
        test "Das Betragsfeld meldet nur, was keine Rechnung ist" {
            let invalid text = transferAmountInvalid month (tabOf (run [ TransferAmountChanged text ] takeReady))
            for text in [ "abc"; "5+"; "/0" ] do
                Expect.isTrue (invalid text) text
            for text in [ ""; "  "; "0"; "0-5"; "20" ] do
                Expect.isFalse (invalid text) text
            Expect.isFalse (transferAmountInvalid month (tabOf takeReady |> fun t -> { t with Transfer = { t.Transfer with AmountInput = None } })) "unberührt"
        }
        test "Keine Gegenseite ist vorbelegt" {
            Expect.isNone (tabOf (selected trip loaded)).Transfer.Counterpart "rot"
        }
        test "Das Betragsfeld rechnet, der Stand ist die Vorbelegung, sonst 0" {
            let red = run [ TransferAmountChanged "*2" ] (selected trip loaded)
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
        // budgeting-gtrgk: die Zeile spricht aus Sicht der gewählten Kategorie, „Verfügbar“ leuchtet.
        test "Nach Erfolg bleibt der Modus, Betrag und Gegenseite sind zurückgesetzt, die Zeile nennt die Verschiebung" {
            let r = { report [] with Lines = [ line trip 0m -20m Written; line food 900m 920m Written ] }
            let model, external, reload = applyMoveAnswered (Ok r) (sending ())
            Expect.equal (tabOf model).Mode TakeMode "Modus"
            Expect.isNone (tabOf model).Transfer.AmountInput "Betrag"
            Expect.isNone (tabOf model).Transfer.Counterpart "Gegenseite"
            Expect.equal (tabOf model).Transfer.Submission Editing "wieder bereit"
            Expect.isTrue reload "neu lesen"
            let offer = Some { Month = monthKey; Request = { Source = CategorySide trip.Id; Target = CategorySide food.Id; Amount = eur 20m; CommandId = None } }
            Expect.equal external (ShowLine { Tone = LineOk; Text = "20,00 € von Urlaub"; Retry = None; Undo = offer }) "aus Sicht von Lebensmittel, mit Rückgängig"
            Expect.isTrue model.AvailableFlash.On "Verfügbar leuchtet"
        }
        test "Error: der Grund steht im Tab, die Eingaben bleiben" {
            let model, external, reload = applyMoveAnswered (Error "YNAB antwortet nicht") (sending ())
            Expect.equal (tabOf model).Transfer.Submission (Rejected "YNAB antwortet nicht") "Grund"
            Expect.equal (tabOf model).Transfer.AmountInput (Some "20,00") "Betrag"
            Expect.isTrue (canTransfer model month (tabOf model)) "wieder bedienbar"
            Expect.equal external NoOp "kein Toast"
            Expect.isFalse reload "nichts geändert"
        }
        test "Aufruf abgebrochen: Formular zurückgesetzt, rote Zeile, Monat neu lesen" {
            let busy = sending ()
            let model, external = applyMoveCallLost "Timeout" busy
            Expect.isNone (tabOf model).Transfer.AmountInput "zurückgesetzt"
            Expect.equal external (ShowLine { Tone = LineError; Text = "Ob verschoben wurde, ist unklar: Timeout. Der Stand wird gleich neu gelesen."; Retry = None; Undo = None }) "ehrlich"
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
            Expect.equal (commands ConfirmTransfer busy) 0 "kein zweiter"
            let tapped = fst (update ConfirmTransfer busy)
            Expect.equal { tapped with SheetLine = None } busy "update tut nichts"
            Expect.equal tapped.SheetLine (Some { Tone = LineWarning; Text = moveBlockedText; Retry = None; Undo = None }) "die Zeile nennt die Sperre"
        }
        test "Während eines Monatswechsels ist der Knopf gesperrt, kein Auftrag geht in den alten Monat" {
            let changing = fst (select "2026-12-01" takeReady)
            Expect.isFalse (canTransfer changing month (tabOf changing)) "gesperrt"
            Expect.equal (commands ConfirmTransfer changing) 0 "kein Auftrag"
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
                Expect.equal (keyActionOf model key OtherFocus) LeaveKey key
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
                Expect.equal (keyActionOf onFood key TextField) LeaveKey key
        }
        test "Ohne Fokus in einem Feld wechseln Links und Rechts die Tabs" {
            Expect.equal (keyActionOf onFood "ArrowRight" OtherFocus) (StepTab Next) "rechts"
            Expect.equal (keyActionOf onFood "ArrowLeft" OtherFocus) (StepTab Previous) "links"
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
            let next, external = update (AssignAnswered (Ok failed)) model
            Expect.equal (next.OpenRetries |> List.map (fun r -> r.Headline)) [ "Nicht zugewiesen" ] "Hinweis"
            Expect.equal
                (next.SheetLine, external)
                (Some { Tone = LineError; Text = "Nicht zugewiesen: Rate-Limit"; Retry = Some "app-1"; Undo = None }, NoOp)
                "rote Zeile mit Erneut senden"
        }
        test "Ein fehlgeschlagener Fehlbetrag-Knopf heißt ebenso Nicht zugewiesen" {
            let red = selected trip loaded
            let sent, _ = confirmMissing red
            let failed = report [ "Urlaub", 0m, 30.5m, Failed "Rate-Limit" ]
            let model, external, _ = applyMoveAnswered (Ok failed) sent
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.Headline)) [ "Nicht zugewiesen" ] "Hinweis"
            Expect.equal external (ShowLine { Tone = LineError; Text = "Nicht zugewiesen: Rate-Limit"; Retry = Some "app-1"; Undo = None }) "Text"
        }
    ]

let private pageChangeTests =
    testList "Seitenwechsel" [
        // Ohne `PageLeft` ginge eine offene Eingabe beim Rücksprung per Browser verloren.
        test "Verlässt man die Seite, erfährt es die Budget-Seite, sonst nicht" {
            let ledger, _ = Components.Ledger.State.init ()
            let leaves (current: Page) (target: Page) =
                let seen = ResizeArray<State.Msg> ()
                for sub in State.urlChangedCmd ledger current target do
                    sub seen.Add
                seen |> Seq.exists (function
                    | State.BudgetMsg PageLeft -> true
                    | _ -> false)
            Expect.isTrue (leaves Page.Budget Page.Settings) "weg"
            Expect.isFalse (leaves Page.Budget Page.Budget) "bleibt"
            Expect.isFalse (leaves Page.Settings Page.Budget) "kommt"
        }
        // budgeting-nfrds: das Formular blieb im Modell und sprang beim Zurückkehren wieder auf.
        test "Der Tab überlebt das Betreten der Seite nicht" {
            let model, _ = enterPage takeReady
            Expect.isNone model.TabMoney "weg"
        }
        test "Kommt die Absage nach der Rückkehr, nennt ein Toast den Grund: die Form ist fort" {
            let model, external = update (MoveAnswered (Error "abgelehnt")) (run [ PageLeft; PageEntered ] (sending ()))
            Expect.isNone model.TabMoney "weg"
            Expect.equal external (ShowToast ("Nicht verschoben: abgelehnt", ToastError)) "sichtbar"
        }
        test "Ist der Tab weg, weil die Kategorie wegfiel, nennt die rote Meldung die Absage" {
            let _, external, _ = applyMoveAnswered (Error "abgelehnt") { sending () with TabMoney = None }
            Expect.equal external (ShowLine { Tone = LineError; Text = "Nicht verschoben: abgelehnt"; Retry = None; Undo = None }) "sichtbar"
        }
    ]

let private summaryTests =
    testList "Berichte" [
        test "Zuweisen nennt Zu verteilen, in beide Richtungen" {
            Expect.equal (moveSummaryText (report [ "Urlaub", 0m, 20m, Written ])) "20,00 € von Zu verteilen nach Urlaub" "Zuweisen"
            Expect.equal (moveSummaryText (report [ "Urlaub", 20m, 15m, Written ])) "5,00 € von Urlaub nach Zu verteilen" "Zurücknehmen"
        }
        // budgeting-gtrgk: die Zeile im Sheet spricht aus Sicht der gewählten Kategorie (Skizze ledger-vf5h6).
        test "Die Zeile nennt die Verschiebung aus Sicht der gewählten Kategorie: nach bei Abgeben, von bei Holen" {
            let r = report [ "Essen", 80m, 30m, Written; "Reise", 0m, 50m, Written ]
            let source, target = r.Lines.[0].CategoryId, r.Lines.[1].CategoryId
            Expect.equal (moveLine (Some source) r) (ShowLine { Tone = LineOk; Text = "50,00 € nach Reise"; Retry = None; Undo = None }) "Abgeben"
            Expect.equal (moveLine (Some target) r) (ShowLine { Tone = LineOk; Text = "50,00 € von Essen"; Retry = None; Undo = None }) "Holen"
        }
        test "Mit Zu verteilen auf der anderen Seite: von Zu verteilen beim Holen und Fehlbetrag, nach Zu verteilen beim Abgeben" {
            let taken = report [ "Essen", 900m, 912.3m, Written ]
            let given = report [ "Essen", 100m, 50m, Written ]
            Expect.equal (moveLineText (Some taken.Lines.Head.CategoryId) taken) "12,30 € von Zu verteilen" "Holen, Fehlbetrag"
            Expect.equal (moveLineText (Some given.Lines.Head.CategoryId) given) "50,00 € nach Zu verteilen" "Abgeben"
        }
        test "Steht die gewählte Kategorie nicht im Bericht, nennt die Zeile beide Seiten" {
            let r = report [ "Essen", 80m, 30m, Written; "Reise", 0m, 50m, Written ]
            Expect.equal (moveLineText (Some (Guid.NewGuid ())) r) "50,00 € von Essen nach Reise" "andere Kategorie"
            Expect.equal (moveLineText None r) "50,00 € von Essen nach Reise" "keine Auswahl"
        }
        test "Fehlgeschlagene Zeile: rote Zeile mit Grund und Erneut senden derselben Anwendung" {
            let r = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
            Expect.equal
                (moveLine None r)
                (ShowLine { Tone = LineError; Text = "Nicht vollständig verschoben: Rate-Limit"; Retry = Some "app-1"; Undo = None })
                "rot mit Erneut senden"
        }
        test "Hat YNAB gewonnen, warnt die Zeile gelb statt Erfolg zu melden, ohne Aktion" {
            Expect.equal
                (moveLine None (report [ "Lebensmittel", 80m, 60m, ChangedInYnab; "Urlaub", 0m, 20m, ChangedInYnab ]))
                (ShowLine { Tone = LineWarning; Text = "In YNAB geändert, nichts überschrieben"; Retry = None; Undo = None })
                "gelb"
        }
    ]

let private reloadTests =
    testList "Stilles Neu-Lesen" [
        // budgeting-nfrds: ein gescheiterter Reload setzte `Failure` und nahm die Liste weg.
        // budgeting-gtrgk: der Hinweis ist Zustand, kein Toast.
        test "Scheitert es, bleibt die Liste stehen und der Hinweis Beträge nicht neu gelesen nennt den Grund" {
            let model = applyReloaded loaded.MonthGeneration (Error "Timeout") loaded
            Expect.equal model.Month (Success month) "Liste bleibt"
            Expect.equal
                (staleHint model)
                (Some { Text = "Beträge nicht neu gelesen, sie können veraltet sein."; Reasons = [ "Timeout" ]; Running = false })
                "Hinweis mit Grund"
        }
        test "Gelingt es, kommt der neue Stand ohne Hinweis" {
            let fresh = { month with ToBeBudgeted = eur 5m }
            let model = applyReloaded loaded.MonthGeneration (Ok fresh) loaded
            Expect.equal (monthOfModel model).ToBeBudgeted (eur 5m) "neu"
            Expect.isNone (staleHint model) "still"
        }
        test "Eine Antwort zur abgewählten Monatswahl ändert nichts" {
            let model = applyReloaded (loaded.MonthGeneration - 1) (Error "Timeout") loaded
            Expect.equal model loaded "verworfen"
        }
        test "Ohne geladene Liste zeigt ein Fehler wie sonst den Fehlerzustand" {
            let model = applyReloaded 0 (Error "Timeout") (init ())
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
        // infrastructure-t5m2b: aus dem Browser gezogen, genau eine Stelle trägt den Hinweis.
        test "Der Hinweis steht am PC auf der Seite, am Handy bei offenem Sheet nur dort, ohne Auswahl auf der Seite" {
            let open' = { afterFailure with Selected = onFood.Selected; TabMoney = onFood.TabMoney }
            Expect.equal (pageRetries true open' |> List.length) 1 "PC, Panel daneben"
            Expect.isEmpty (pageRetries false open') "offenes Sheet"
            Expect.equal (pageRetries false afterFailure |> List.length) 1 "Sheet zu"
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
        // budgeting-my8p8: Erneut senden geht auch hinaus, wenn der Monat der Verschiebung inzwischen vergangen ist.
        test "Im Folgemonat sendet eine offene Verschiebung des Vormonats erneut, über den Hinweis und über die Markierung" {
            let december = Success { frame with CurrentMonth = "2026-12-01" }
            let pastFailure = { afterFailure with Frame = december }
            Expect.equal (displayedMonthKind pastFailure) (Some PastMonth) "der November ist vergangen"
            Expect.isTrue (snd (startMoveRetry "app-1" pastFailure)) "über den Hinweis"
            let pastMarked = { markedRent with Frame = december }
            Expect.equal (displayedMonthKind pastMarked) (Some PastMonth) "auch hier vergangen"
            Expect.isTrue (snd (startMoveRetry markedId pastMarked)) "über die Markierung"
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
            | ShowLine { Tone = LineOk; Retry = None; Undo = None } -> ()
            | other -> failtest $"grüne Zeile erwartet: {other}"
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
        test "Scheitert der Aufruf, bleibt der Hinweis sendbar und die rote Zeile nennt den Grund mit Erneut senden" {
            let running, _ = startMoveRetry "app-1" afterFailure
            let model, external = applyMoveRetried "app-1" (Error "Timeout") running
            Expect.equal (model.OpenRetries |> List.map (fun r -> r.State)) [ RetryReady ] "sendbar"
            Expect.equal external (ShowLine { Tone = LineError; Text = "Erneut senden gescheitert: Timeout"; Retry = Some "app-1"; Undo = None }) "sichtbar"
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
    ]

// ============================================
// Rückmeldung am Ort ohne Toast (Regel R3, ADR 0098, budgeting-gtrgk)
// ============================================

/// Das Abgeben aus `givingToTrip`, wie gesendet: das Angebot „Rückgängig“ der Zeile (budgeting-y1kkr).
let private gaveOffer : SentMove =
    { Month = monthKey; Request = { Source = CategorySide food.Id; Target = CategorySide trip.Id; Amount = eur 5m; CommandId = None } }

/// Die grüne Zeile nach `afterGive`, mit „Rückgängig“.
let private okLine text = Some { Tone = LineOk; Text = text; Retry = None; Undo = Some gaveOffer }

/// Abgeben von Lebensmittel nach Urlaub, Antwort steht aus.
let private givingToTrip =
    run [ SwitchMoneyMode GiveMode; TransferAmountChanged "5"; PickCounterpart (CategorySide trip.Id); ConfirmTransfer ] onFood

/// Die Antwort auf `givingToTrip`: 5,00 € geschrieben.
let private gaveToTrip = answered [ line food 900m 895m Written; line trip 0m 5m Written ]

/// Nach dem Abgeben steht die Zeile „✓ 5,00 € nach Urlaub“.
let private afterGive = run [ gaveToTrip ] givingToTrip

let private sheetLineRouteTests =
    testList "Zeile im Sheet: Platz" [
        test "Bei offenem Sheet steht das Ergebnis in der Zeile über dem Hauptknopf, kein Toast, am PC im Panel genauso" {
            let model, external = update gaveToTrip givingToTrip
            Expect.equal (model.SheetLine, external) (okLine "5,00 € nach Urlaub", NoOp) "Zeile, kein Toast"
        }
        test "Bei geschlossenem Sheet geht dieselbe Meldung in die Leiste, am PC als Toast (Meldung ohne Ort)" {
            // Während des Aufrufs bleibt das Sheet stehen; zu ist es etwa, wenn die Seite neu betreten wurde.
            let model, external = update gaveToTrip { givingToTrip with Selected = None }
            Expect.equal (model.SheetLine, external) (None, ShowToast ("5,00 € von Lebensmittel nach Urlaub", ToastSuccess)) "Leiste"
        }
        test "Eine rote Zeile mit offenem Bericht bietet Erneut senden an, ohne Sheet trägt die Leiste es weiter" {
            let failed = answered [ line food 900m 895m Written; line trip 0m 5m (Failed "Rate-Limit") ]
            let model, external = update failed givingToTrip
            Expect.equal
                model.SheetLine
                (Some { Tone = LineError; Text = "Nicht vollständig verschoben: Rate-Limit"; Retry = Some "app-2"; Undo = None })
                "Zeile"
            Expect.equal external NoOp "kein Toast"
            let _, closed = update failed { givingToTrip with Selected = None }
            Expect.equal
                closed
                (ShowToastWithAction ("Nicht vollständig verschoben: Rate-Limit", ToastError, { Label = "Erneut senden"; Kind = RetryBudgetMove "app-2" }))
                "Leiste mit Erneut senden"
        }
        test "Erneut senden an der Zeile sendet die Verschiebung erneut" {
            let failed = run [ answered [ line food 900m 895m Written; line trip 0m 5m (Failed "Rate-Limit") ] ] givingToTrip
            let retry = failed.SheetLine |> Option.bind (fun l -> l.Retry) |> Option.get
            let retrying = run [ RetryMove retry ] failed
            Expect.equal (retrying.OpenRetries |> List.map (fun r -> r.State)) [ RetryRunning ] "läuft"
            Expect.isNone retrying.SheetLine "der Tipp beendet die Zeile"
        }
        test "Wartet die Auswahl auf die Rückkehr aus dem Kassenbuch, gilt das Sheet als zu" {
            let away = { givingToTrip with Selected = givingToTrip.Selected |> Option.map (fun s -> { s with ReturnPending = true }) }
            let model, external = update gaveToTrip away
            Expect.isNone model.SheetLine "keine Zeile"
            Expect.equal external (ShowToast ("5,00 € von Lebensmittel nach Urlaub", ToastSuccess)) "Leiste"
        }
    ]

let private sheetLineLifeTests =
    testList "Zeile im Sheet: Lebensdauer" [
        test "Die nächste Aktion im Sheet beendet die Zeile: Tippen, Schritt, Betrag, Gegenseite, Knöpfe" {
            for name, msg in
                [ "Betrag", TransferAmountChanged "7"
                  "Betrag zurück", TransferAmountReset
                  "Gegenseite öffnen", OpenPicker
                  "Gegenseite wählen", PickCounterpart ToBeBudgetedSide
                  "Knopf", ConfirmTransfer
                  "Erneut senden", RetryMove "app-9" ] do
                Expect.isNone (run [ msg ] afterGive).SheetLine name
            let assigning = run [ SwitchMoneyMode AssignMode ] afterGive
            Expect.equal assigning.SheetLine (okLine "5,00 € nach Urlaub") "Vorbedingung im Modus Zuweisen"
            for name, msg in [ "Tippen", typed "+1"; "Schritt", AssignStepped 10m; "Fehlbetrag", CoverMissing ] do
                Expect.isNone (run [ msg ] assigning).SheetLine name
        }
        test "Kategorie- und Monatswechsel und Schließen beenden die Zeile" {
            for name, msg in
                [ "Kategorie", SelectCategory trip.Id
                  "Pfeil", StepCategory Next
                  "Monat", SelectMonth "2026-10-01"
                  "Schließen", CloseInspector
                  "Seite verlassen", PageLeft ] do
                Expect.isNone (run [ msg ] afterGive).SheetLine name
        }
        test "Der Wechsel zwischen Zuweisen, Holen und Abgeben, der Tabwechsel und Antworten im Hintergrund lassen die Zeile stehen" {
            for name, msg in
                [ "Zuweisen", SwitchMoneyMode AssignMode
                  "Holen", SwitchMoneyMode TakeMode
                  "Tab", SelectInspectorTab EntriesTab
                  "Pfeiltaste Tab", PageKeyPressed (StepTab Next)
                  "Neu-Lesen", Reloaded (afterGive.MonthGeneration, Ok month)
                  "Zuletzt verwendet", RecentCounterpartsLoaded (Ok []) ] do
                Expect.equal (run [ msg ] afterGive).SheetLine (okLine "5,00 € nach Urlaub") name
        }
        test "Ein Fehler bleibt ebenso bis zur nächsten Aktion" {
            let failed = run [ answered [ line food 900m 895m (Failed "weg"); line trip 0m 5m (Failed "weg") ] ] givingToTrip
            let waited = run [ Reloaded (failed.MonthGeneration, Ok month); SwitchMoneyMode TakeMode ] failed
            Expect.equal (waited.SheetLine |> Option.map (fun l -> l.Tone)) (Some LineError) "bleibt"
            Expect.isNone (run [ TransferAmountChanged "1" ] waited).SheetLine "geht mit der nächsten Eingabe"
        }
    ]

let private availableFlashTests =
    testList "Verfügbar leuchtet" [
        test "Nach einem Bericht mit geschriebener Zeile der gewählten Kategorie leuchtet Verfügbar, nach Ablauf nicht mehr" {
            let flashing, cmd = (fun (m, c, _) -> m, c) (Components.Budget.State.update gaveToTrip givingToTrip)
            Expect.isTrue flashing.AvailableFlash.On "leuchtet"
            Expect.isNonEmpty cmd "Zeitgeber und Neu-Lesen"
            let ended = run [ AvailableFlashEnded flashing.AvailableFlash.Count ] flashing
            Expect.isFalse ended.AvailableFlash.On "aus"
        }
        test "Zuweisen, Fehlbetrag und Erneut senden leuchten ebenso" {
            let assigned = run [ typed "+1"; AssignFieldLeft; AssignAnswered (Ok { ApplicationId = "a"; Month = monthKey; Lines = [ line food 900m 901m Written ]; ActualToBeBudgeted = None }) ] onFood
            Expect.isTrue assigned.AvailableFlash.On "Zuweisen"
            let covered = run [ CoverMissing; answered [ line rent 900m 912.3m Written ] ] onRent
            Expect.isTrue covered.AvailableFlash.On "Fehlbetrag"
            let failedCover = run [ CoverMissing; answered [ line rent 900m 912.3m (Failed "weg") ] ] onRent
            Expect.isFalse failedCover.AvailableFlash.On "ein Fehler leuchtet nicht"
            let retried =
                run [ RetryMove "app-2"; MoveRetried ("app-2", Ok { ApplicationId = "app-2"; Month = monthKey; Lines = [ line rent 900m 912.3m Written ]; ActualToBeBudgeted = None }) ] failedCover
            Expect.isTrue retried.AvailableFlash.On "Erneut senden"
        }
        test "Nach einem Fehler, einer Absage oder einer Zeile für eine andere Kategorie leuchtet nichts" {
            Expect.isFalse (run [ MoveAnswered (Error "abgelehnt") ] givingToTrip).AvailableFlash.On "Absage"
            Expect.isFalse (run [ MoveCallLost "weg" ] givingToTrip).AvailableFlash.On "abgebrochen"
            Expect.isFalse (run [ answered [ line clothes 50m 55m Written ] ] givingToTrip).AvailableFlash.On "andere Kategorie"
        }
        test "Das Ende eines älteren Leuchtens nimmt ein neueres nicht ab" {
            let first = run [ gaveToTrip ] givingToTrip
            let second = { first with AvailableFlash = { Count = first.AvailableFlash.Count + 1; On = true } }
            Expect.isTrue (run [ AvailableFlashEnded first.AvailableFlash.Count ] second).AvailableFlash.On "bleibt"
        }
        test "Der Zeitgeber beendet das Leuchten nach 900 ms" {
            CmdTiming.expectAfter Components.Budget.State.availableFlashMs (AvailableFlashEnded 3) (Components.Budget.State.availableFlashCmd 3)
        }
    ]

let private staleHintTests =
    let failedRead = run [ Reloaded (loaded.MonthGeneration, Error "Timeout") ] loaded
    testList "Hinweis nicht neu gelesen" [
        test "Der Hinweis bleibt bei Kategorie-, Tab-, Modus- und Sheet-Wechsel stehen" {
            let onFoodFailed = run [ Reloaded (onFood.MonthGeneration, Error "Timeout") ] onFood
            for name, msg in
                [ "Kategorie", SelectCategory trip.Id
                  "Tab", SelectInspectorTab HistoryTab
                  "Modus", SwitchMoneyMode GiveMode
                  "Schließen", CloseInspector ] do
                Expect.equal (run [ msg ] onFoodFailed).StaleAmounts (Some (ReadFailed "Timeout")) name
        }
        test "Neu laden liest still neu und steht als laufend, ein zweiter Tipp startet keinen zweiten Aufruf" {
            let rereading, cmd, _ = Components.Budget.State.update RereadStale failedRead
            Expect.equal rereading.StaleAmounts (Some (Rereading "Timeout")) "läuft, der Grund bleibt"
            Expect.isTrue (staleHint rereading).Value.Running "laufend"
            Expect.isGreaterThan rereading.MonthGeneration failedRead.MonthGeneration "eine Abfrage"
            Expect.equal rereading.Month failedRead.Month "die Liste bleibt"
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal (commands RereadStale rereading) 0 "kein zweiter Aufruf"
            Expect.equal (run [ RereadStale ] rereading) rereading "nichts geändert"
        }
        test "Gelingt das Neu-Lesen, ist der Hinweis weg; scheitert es, bleibt er mit dem neuen Grund" {
            let rereading = run [ RereadStale ] failedRead
            let read = run [ Reloaded (rereading.MonthGeneration, Ok month) ] rereading
            Expect.isNone (staleHint read) "weg"
            let failedAgain = run [ Reloaded (rereading.MonthGeneration, Error "YNAB 503") ] rereading
            Expect.equal failedAgain.StaleAmounts (Some (ReadFailed "YNAB 503")) "neuer Grund, wieder tippbar"
        }
        test "Ein Monatswechsel liest den Monat ohnehin neu: der Hinweis ist weg" {
            Expect.isNone (run [ SelectMonth "2026-10-01" ] failedRead).StaleAmounts "weg"
        }
        test "Stehen Beträge und Monate aus, steht ein Hinweis, Neu laden liest beides" {
            let both = { failedRead with StaleMonths = Some (ReadFailed "Rahmen weg") }
            Expect.equal
                (staleHint both)
                (Some { Text = "Beträge und Monate nicht neu gelesen, sie können veraltet sein."; Reasons = [ "Timeout"; "Rahmen weg" ]; Running = false })
                "ein Hinweis"
            let rereading, cmd, _ = Components.Budget.State.update RereadStale both
            Expect.equal (rereading.StaleAmounts, rereading.StaleMonths) (Some (Rereading "Timeout"), Some (Rereading "Rahmen weg")) "beides läuft"
            Expect.equal (List.length cmd) 2 "Monat und Rahmen"
            let framed = run [ FrameRefreshed (Ok frame) ] rereading
            Expect.isNone framed.StaleMonths "Rahmen gelesen"
            let frameFailed = run [ FrameRefreshed (Error "wieder weg") ] rereading
            Expect.equal frameFailed.StaleMonths (Some (ReadFailed "wieder weg")) "neuer Grund"
        }
        test "Ein stilles Nachlesen des Rahmens ohne Hinweis meldet weiter nichts" {
            Expect.isNone (run [ FrameRefreshed (Error "weg") ] loaded).StaleMonths "still"
        }
    ]

// ============================================
// Rückgängig nach einer Verschiebung (budgeting-y1kkr, ADR 0099)
// ============================================

/// Die Antwort auf das Rückgängig von `afterGive`: 5,00 € von Urlaub zurück nach Lebensmittel.
let private undidGive (outcome: LineOutcome) =
    answered [ line trip 5m 0m outcome; line food 895m 900m Written ]

/// Das Angebot der Zeile, wenn eine steht.
let private offerOf (model: Model) : SentMove option =
    model.SheetLine |> Option.bind (fun l -> l.Undo)

let private undoRequestTests =
    let a = Guid.NewGuid()
    let b = Guid.NewGuid()
    let id = Guid.NewGuid()
    let sent source target = { Month = "2026-10-01"; Request = { Source = source; Target = target; Amount = eur 50m; CommandId = None } }
    testList "Rückgängig: Auftrag" [
        test "Kategorie zu Kategorie: Quelle und Ziel vertauscht, Betrag und Monat bleiben, die Befehls-Id ist die übergebene" {
            Expect.equal
                (undoRequest id (sent (CategorySide a) (CategorySide b)))
                ("2026-10-01", { Source = CategorySide b; Target = CategorySide a; Amount = eur 50m; CommandId = Some id })
                "Gegenrichtung"
        }
        test "Kategorie nach Zu verteilen: zurück aus Zu verteilen in die Kategorie" {
            Expect.equal
                (undoRequest id (sent (CategorySide a) ToBeBudgetedSide))
                ("2026-10-01", { Source = ToBeBudgetedSide; Target = CategorySide a; Amount = eur 50m; CommandId = Some id })
                "Gegenrichtung"
        }
        test "Zu verteilen an Kategorie (Fehlbetrag-Knopf): zurück nach Zu verteilen" {
            Expect.equal
                (undoRequest id (sent ToBeBudgetedSide (CategorySide a)))
                ("2026-10-01", { Source = CategorySide a; Target = ToBeBudgetedSide; Amount = eur 50m; CommandId = Some id })
                "Gegenrichtung"
        }
    ]

let private undoOfferTests =
    let written = { report [ "Lebensmittel", 900m, 895m, Written; "Urlaub", 0m, 5m, Written ] with ApplicationId = "app-2" }
    let withSecond outcome = { written with Lines = [ written.Lines.Head; { written.Lines[1] with Outcome = outcome } ] }
    let forward = Some (ForwardMove gaveOffer)
    testList "Rückgängig: Angebot" [
        test "Nach Holen, Abgeben oder Fehlbetrag mit jeder Zeile geschrieben steht das Angebot" {
            Expect.equal (undoOffer forward written) (Some gaveOffer) "angeboten"
        }
        test "Eine fehlgeschlagene Zeile: kein Angebot, es gilt Erneut senden" {
            Expect.isNone (undoOffer forward (withSecond (Failed "Rate-Limit"))) "Failed"
        }
        test "In YNAB geändert: kein Angebot" {
            Expect.isNone (undoOffer forward (withSecond ChangedInYnab)) "ChangedInYnab"
        }
        test "In BB zurückgezogen: kein Angebot" {
            Expect.isNone (undoOffer forward (withSecond WithdrawnInBb)) "WithdrawnInBb"
        }
        test "Ein Bericht ohne Zeilen: kein Angebot" {
            Expect.isNone (undoOffer forward { written with Lines = [] }) "leer"
        }
        test "Nach dem Rückgängig selbst und ohne Auftrag aus dem Tab: kein Angebot" {
            Expect.isNone (undoOffer (Some UndoingMove) written) "kein Rückgängig des Rückgängig"
            Expect.isNone (undoOffer None written) "ohne Auftrag"
        }
        test "Die Zeile nach Abgeben trägt das Angebot mit dem gesendeten Auftrag" {
            Expect.equal afterGive.SheetLine (okLine "5,00 € nach Urlaub") "✓ 5,00 € nach Urlaub · Rückgängig"
            Expect.equal (sheetLineAction afterGive.SheetLine.Value) (Some ("Rückgängig", UndoMove)) "Aktion rechts"
        }
        test "Der Fehlbetrag-Knopf merkt sich den Auftrag von Zu verteilen an die Kategorie" {
            let covered = run [ CoverMissing; answered [ line rent 900m 912.3m Written ] ] onRent
            Expect.equal
                (offerOf covered)
                (Some { Month = monthKey; Request = { Source = ToBeBudgetedSide; Target = CategorySide rent.Id; Amount = eur 12.3m; CommandId = None } })
                "Fehlbetrag"
        }
        test "Das Feld Zugewiesen bietet kein Rückgängig an" {
            let assigned = run [ typed "+20"; AssignFieldLeft ] onFood
            Expect.isNonEmpty assigned.AssignQueue "gesendet"
            let answeredAssign =
                run [ AssignAnswered (Ok { ApplicationId = "a"; Month = monthKey; Lines = [ line food 900m 920m Written ]; ActualToBeBudgeted = None }) ] assigned
            Expect.isNone (offerOf answeredAssign) "kein Angebot"
        }
        test "Targets anwenden bietet kein Rückgängig an" {
            let targetLine : TargetLine =
                { CategoryId = rent.Id; CategoryName = rent.Name; GroupName = "Alltag"; OldAssigned = eur 900m
                  Underfunded = eur 12.3m; NewAssigned = eur 912.3m; Target = None }
            let preview : TargetPreview =
                { Month = monthKey; Lines = [ targetLine ]; Skipped = []; Total = eur 12.3m; ToBeBudgeted = eur 100m; ToBeBudgetedAfter = eur 87.7m; CommandId = None }
            let applying = run [ ConfirmTargets ] { onRent with Targets = PreviewReady { Shown = preview; Changed = false; Excluded = Set.empty } }
            let applied =
                run [ TargetsApplied (Ok (Applied { ApplicationId = "t"; Month = monthKey; Lines = [ line rent 900m 912.3m Written ]; ActualToBeBudgeted = None })) ] applying
            Expect.isNone (offerOf applied) "kein Angebot"
            Expect.isNone applied.TabSend "kein Auftrag aus dem Tab"
        }
        test "Am PC steht Rückgängig in der Zeile im Panel, ohne Toast; ohne Sheet trägt die Leiste kein Rückgängig" {
            // Panel und Sheet unterscheidet nur das Rendern: beide zeigen `SheetLine` mit `sheetLineAction`.
            let model, external = update gaveToTrip givingToTrip
            Expect.equal external NoOp "kein Toast"
            Expect.equal (model.SheetLine |> Option.bind sheetLineAction) (Some ("Rückgängig", UndoMove)) "im Panel"
            let _, closed = update gaveToTrip { givingToTrip with Selected = None }
            Expect.equal closed (ShowToast ("5,00 € von Lebensmittel nach Urlaub", ToastSuccess)) "Leiste ohne Rückgängig"
        }
    ]

let private undoFlowTests =
    testList "Rückgängig: Ablauf" [
        test "Rückgängig sendet genau einen Auftrag, die Zeile endet, ein Doppeltipp sendet nicht noch einmal" {
            let undoing, cmd, _ = Components.Budget.State.update UndoMove afterGive
            Expect.isNonEmpty cmd "gesendet"
            Expect.equal undoing.TabSend (Some UndoingMove) "Rückgängig läuft"
            Expect.isTrue (undoing.SheetLine |> Option.forall (fun l -> l.Undo.IsNone)) "kein Angebot mehr"
            Expect.equal (commands UndoMove undoing) 0 "der zweite Tipp sendet nicht"
            Expect.isNone (offerOf (run [ UndoMove ] undoing)) "auch danach kein Angebot"
        }
        test "Das Rückgängig geht an den Monat der Verschiebung, auch wenn inzwischen ein anderer Monat gezeigt würde" {
            let october = { gaveOffer with Month = "2026-10-01" }
            let shown = { afterGive with SheetLine = afterGive.SheetLine |> Option.map (fun l -> { l with Undo = Some october }) }
            let _, sent = confirmUndo shown
            Expect.equal sent (Some october) "der gemerkte Auftrag, nicht der gezeigte Monat"
            let seen = Collections.Concurrent.ConcurrentQueue<string * MoveRequest>()
            let call (month: string) (request: MoveRequest) : Async<Result<ApplyReport, string>> =
                async {
                    seen.Enqueue((month, request))
                    return Ok { ApplicationId = "u"; Month = month; Lines = []; ActualToBeBudgeted = None }
                }
            let msg, _ = Tests.CmdTiming.firstDispatchedAfter 3000 (Components.Budget.State.undoCmdWith call october)
            Expect.equal msg (MoveAnswered (Ok { ApplicationId = "u"; Month = "2026-10-01"; Lines = []; ActualToBeBudgeted = None })) "Antwort"
            let month, request = Seq.exactlyOne seen
            Expect.equal month "2026-10-01" "Monat der Verschiebung"
            Expect.equal
                { request with CommandId = None }
                { Source = CategorySide trip.Id; Target = CategorySide food.Id; Amount = eur 5m; CommandId = None }
                "vertauscht, derselbe Betrag"
            Expect.isSome request.CommandId "mit Befehls-Id"
        }
        test "Nach dem Rückgängig steht „✓ Rückgängig: …“ ohne Aktion, Verfügbar leuchtet" {
            let undoing = run [ UndoMove ] afterGive
            let model, external = update (undidGive Written) undoing
            Expect.equal external NoOp "kein Toast"
            Expect.equal model.SheetLine (Some { Tone = LineOk; Text = "Rückgängig: 5,00 € zurück von Urlaub"; Retry = None; Undo = None }) "ohne Aktion"
            Expect.isNone (sheetLineAction model.SheetLine.Value) "kein Rückgängig des Rückgängig"
            Expect.isNone model.TabSend "nichts mehr unterwegs"
            Expect.isTrue model.AvailableFlash.On "Verfügbar leuchtet"
        }
        test "Aus Sicht von Holen: „Rückgängig: … zurück nach …“" {
            let taking =
                run [ SwitchMoneyMode TakeMode; TransferAmountChanged "5"; PickCounterpart (CategorySide trip.Id); ConfirmTransfer ] onFood
            let took = run [ answered [ line trip 0m -5m Written; line food 900m 905m Written ] ] taking
            Expect.equal (offerOf took |> Option.map (fun o -> o.Request.Source, o.Request.Target)) (Some (CategorySide trip.Id, CategorySide food.Id)) "Angebot"
            let undone = run [ UndoMove; answered [ line food 905m 900m Written; line trip -5m 0m Written ] ] took
            Expect.equal (undone.SheetLine |> Option.map (fun l -> l.Text)) (Some "Rückgängig: 5,00 € zurück nach Urlaub") "Text"
        }
        test "Scheitert das Rückgängig, steht die rote Zeile wie bei jeder Verschiebung" {
            let undoing = run [ UndoMove ] afterGive
            let failed = run [ undidGive (Failed "Rate-Limit") ] undoing
            Expect.equal
                failed.SheetLine
                (Some { Tone = LineError; Text = "Nicht vollständig verschoben: Rate-Limit"; Retry = Some "app-2"; Undo = None })
                "Erneut senden"
            let rejected = run [ MoveAnswered (Error Server.Budgeting.Domain.CategoryNotVisibleText) ] undoing
            Expect.equal
                (rejected.SheetLine |> Option.map (fun l -> l.Tone, l.Text, l.Undo))
                (Some (LineError, $"Nicht verschoben: {Server.Budgeting.Domain.CategoryNotVisibleText}", None))
                "Ablehnung des Servers"
            Expect.equal (tabOf rejected).Transfer.Submission Editing "keine Absage im Formular"
            let lost = run [ MoveCallLost "Timeout" ] undoing
            Expect.equal (lost.SheetLine |> Option.map (fun l -> l.Tone, l.Undo)) (Some (LineError, None)) "Abbruch"
            Expect.isNone lost.TabSend "nichts mehr unterwegs"
        }
    ]

/// Ein Schritt der Aufzählung zur Lebensdauer des Angebots.
type private UndoStep =
    /// Beendet das Angebot (nächstes Senden, Kategorie, Monat, Schließen, Seite verlassen).
    | Ends
    /// Lässt es stehen (Moduswechsel, Antworten im Hintergrund).
    | Keeps
    /// „Rückgängig“.
    | Undo
    /// Eine Antwort auf den laufenden Auftrag.
    | Answer

/// Was die Aufzählung selbst weiß, ohne den Prädikaten des Clients zu glauben.
type private UndoWalk =
    { /// Der laufende Auftrag: eine Verschiebung mit ihrem Auftrag oder ein Rückgängig.
      Pending: SentMove option option
      /// Wurde für das jetzige Angebot schon Rückgängig gesendet?
      Used: bool }

let private undoEvents (model: Model) (state: UndoWalk) : (string * Msg list * UndoStep) list =
    let give = [ SwitchMoneyMode GiveMode; TransferAmountChanged "5"; PickCounterpart (CategorySide trip.Id); ConfirmTransfer ]
    let take = [ SwitchMoneyMode TakeMode; TransferAmountChanged "5"; PickCounterpart (CategorySide trip.Id); ConfirmTransfer ]
    [ "Rückgängig", [ UndoMove ], Undo
      "Abgeben", give, Ends
      "Holen", take, Ends
      "Fehlbetrag", [ CoverMissing ], Ends
      "Zuweisen", [ SwitchMoneyMode AssignMode; typed "+5"; AssignFieldLeft ], Ends
      "Pause", [ SetTargetPause PauseTarget ], Ends
      "Kategorie", [ SelectCategory trip.Id ], Ends
      "Zurück zu Lebensmittel", [ SelectCategory food.Id ], Ends
      "Monat", [ SelectMonth "2026-10-01" ], Ends
      "Schließen", [ CloseInspector ], Ends
      "Seite verlassen", [ PageLeft ], Ends
      "Modus Zuweisen", [ SwitchMoneyMode AssignMode ], Keeps
      "Modus Holen", [ SwitchMoneyMode TakeMode ], Keeps
      "Modus Abgeben", [ SwitchMoneyMode GiveMode ], Keeps
      "Neu-Lesen", [ Reloaded (model.MonthGeneration, Ok month) ], Keeps
      match state.Pending with
      | Some (Some _) -> "Antwort Verschiebung", [ gaveToTrip ], Answer
      | Some None ->
          "Antwort Rückgängig", [ undidGive Written ], Answer
          "Abbruch Rückgängig", [ MoveCallLost "weg" ], Answer
      | None -> ()
      match model.AssignQueue with
      | _ :: _ -> "Zuweisung fertig", [ AssignAnswered (Ok { ApplicationId = "a"; Month = monthKey; Lines = []; ActualToBeBudgeted = None }) ], Keeps
      | [] -> () ]

/// Der Auftrag eines Wegs, wie der Test ihn kennt.
let private sentBy (name: string) : SentMove option =
    let request source target = { Month = monthKey; Request = { Source = source; Target = target; Amount = eur 5m; CommandId = None } }
    match name with
    | "Abgeben" -> Some (request (CategorySide food.Id) (CategorySide trip.Id))
    | "Holen" -> Some (request (CategorySide trip.Id) (CategorySide food.Id))
    | _ -> None

let private undoLifeTests =
    testList "Rückgängig: Lebensdauer" [
        test "Aufzählung der Reihenfolgen: Senden, Kategorie, Monat, Schließen und die Antwort beenden das Angebot, Moduswechsel nicht, nie zweimal zurück" {
            let visited = Collections.Generic.HashSet<Model * UndoWalk> HashIdentity.Structural
            let mutable undos = 0
            let rec go (left: int) (path: string list) (model: Model) (state: UndoWalk) =
                for name, msgs, step in undoEvents model state do
                    let shown = String.concat ", " (List.rev (name :: path))
                    let after, sent =
                        msgs
                        |> List.fold
                            (fun (current, sent) msg ->
                                let next, cmd, _ = Components.Budget.State.update msg current
                                next, sent + (if msg = UndoMove && not cmd.IsEmpty then 1 else 0))
                            (model, 0)
                    let before = offerOf model
                    let now = offerOf after
                    let mutable state' = state
                    match step with
                    | Ends -> Expect.isNone now $"endet: {shown}"
                    | Keeps -> Expect.equal now before $"bleibt: {shown}"
                    | Undo ->
                        Expect.isLessThanOrEqual sent 1 $"höchstens ein Auftrag: {shown}"
                        if sent = 1 then
                            Expect.isSome before $"nur mit Angebot: {shown}"
                            Expect.isFalse state.Used $"nie zweimal zurück: {shown}"
                            Expect.isNone state.Pending $"nichts läuft: {shown}"
                            undos <- undos + 1
                            state' <- { Pending = Some None; Used = true }
                        Expect.isNone now $"das Angebot ist verbraucht: {shown}"
                    | Answer ->
                        match state.Pending with
                        | Some (Some forward) when Components.Budget.State.sheetOpen after ->
                            Expect.equal now (Some forward) $"neues Angebot für die letzte Verschiebung: {shown}"
                        | _ -> Expect.isNone now $"kein Angebot: {shown}"
                        state' <- { Pending = None; Used = false }
                    match step, sentBy name with
                    | Ends, Some forward when after.TabSend = Some (ForwardMove forward) ->
                        state' <- { state' with Pending = Some (Some forward) }
                    | _ -> ()
                    if left > 1 && visited.Add((after, state')) then go (left - 1) (name :: path) after state'
            go 4 [] afterGive { Pending = None; Used = false }
            Expect.isGreaterThan undos 10 "die Aufzählung erreicht Rückgängig"
        }
        test "Der Wechsel zwischen Zuweisen, Holen und Abgeben lässt das Angebot stehen" {
            let switched = run [ SwitchMoneyMode AssignMode; SwitchMoneyMode TakeMode; SwitchMoneyMode GiveMode ] afterGive
            Expect.equal (offerOf switched) (Some gaveOffer) "steht"
        }
    ]

[<Tests>]
let tests =
    testList
        "Tab Geld, Client"
        [ fieldTests; leaveTests; queueTests; busyTests; unclearTests; settledTests; reviewTests; activeTests; missingTests; modeTests; transferTests; pickerTests
          keyTests; fieldKeyTests; openRetryTests; pageChangeTests; summaryTests; reloadTests; retryTests
          sheetLineRouteTests; sheetLineLifeTests; availableFlashTests; staleHintTests
          undoRequestTests; undoOfferTests; undoFlowTests; undoLifeTests ]
