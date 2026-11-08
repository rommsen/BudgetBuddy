module Tests.TargetToYnabClientTests

// budgeting-e2z7v: Targets nach YNAB im Client. Die Hinweise am Target kommen aus dem Zustand der Kategorie, "Erneut
// senden" und der einmalige Lauf gehen durch das echte `update`. Die Cmds werden unter .NET nur gebaut, nie
// ausgeführt: der Proxy liegt hinter einem Lambda.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.TargetEdit

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private current = "2026-11-01"
let private tripId = Guid.Parse "00000000-0000-0000-0000-0000000e2701"
let private holidayId = Guid.Parse "00000000-0000-0000-0000-0000000e2702"

let private weekly : Target =
    { Goal = eur 20m; Rhythm = Weeks 1; Kind = Need; Date = None; NeedsWholeAmount = Some true; Day = Some 3; CreatedIn = None }

let private category (id: Guid) (state: TargetYnabState option) : BudgetCategory =
    { Id = id
      Name = "Ausflug"
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur 0m
      Underfunded = Some (eur 0m)
      Target = Some weekly
      Pause = None
      OpenWriteBack = None
      TargetInYnab = state
      Deactivated = None }

let private notInYnab = Some { NotInYnab = Some "Rate-Limit"; Gaps = [] }

let private pageWith (categories: BudgetCategory list) : Model =
    { Components.Budget.Types.init () with
        Frame = Success { CurrentMonth = current; Months = [ current ]; CategoryOrder = categories |> List.map (fun c -> c.Id); MonthStartHidden = [] }
        ChosenMonth = Some current
        Month = Success { Month = current; ToBeBudgeted = eur 0m; Groups = [ { Name = "Alltag"; Categories = categories } ] }
        Tab = TargetTab }

let private update (msg: Msg) (model: Model) = Components.Budget.State.update msg model

let private step (msg: Msg) (model: Model) : Model =
    let m, _, _ = update msg model
    m

let private sends (msg: Msg) (model: Model) : bool =
    let _, cmd, _ = update msg model
    not (List.isEmpty cmd)

let private hintTests =
    testList "Targets nach YNAB im Client: Hinweise am Target" [
        testCase "Nicht in YNAB steht mit Grund aus dem Zustand der Kategorie" <| fun () ->
            let hints = targetYnabHints (category tripId notInYnab)
            Expect.equal hints.NotInYnab (Some "Rate-Limit") "Grund"
            Expect.isEmpty hints.Gaps "keine Lücken"

        testCase "In YNAB ohne Wochentag erscheint aus den Lücken" <| fun () ->
            let hints = targetYnabHints (category tripId (Some { NotInYnab = None; Gaps = [ WeekdayMissing ] }))
            Expect.equal hints.Gaps [ "In YNAB ohne Wochentag" ] "Lücke"
            Expect.isNone hints.NotInYnab "in YNAB"

        testCase "In YNAB nicht gesnoozt erscheint nur an einem pausierten Target" <| fun () ->
            let paused = { category tripId None with Pause = Some PausedInBb }
            Expect.isTrue (targetYnabHints paused).NotSnoozed "pausiert"
            Expect.isFalse (targetYnabHints (category tripId None)).NotSnoozed "nicht pausiert"
            Expect.isFalse (targetYnabHints { paused with Target = None }).NotSnoozed "ohne Target"
            Expect.equal TargetNotSnoozedText "In YNAB nicht gesnoozt" "Wortlaut"

        testCase "Ohne Zustand gibt es keinen Hinweis" <| fun () ->
            let hints = targetYnabHints (category tripId None)
            Expect.equal (hints.NotInYnab, hints.Gaps, hints.NotSnoozed) (None, [], false) "nichts"
    ]

let private retryTests =
    testList "Targets nach YNAB im Client: Erneut senden" [
        testCase "Erneut senden sendet einmal und ist gesperrt, solange es läuft" <| fun () ->
            let page = pageWith [ category tripId notInYnab; category holidayId notInYnab ]
            Expect.isTrue (sends (RetryTargetToYnab tripId) page) "sendet"
            let running = step (RetryTargetToYnab tripId) page
            Expect.isTrue (targetRetryRunning tripId running) "läuft"
            Expect.isFalse (sends (RetryTargetToYnab tripId) running) "kein zweites Senden"
            Expect.isFalse (sends (RetryTargetToYnab holidayId) running) "auch keine andere Kategorie zugleich"

        testCase "Ohne Nicht in YNAB sendet Erneut senden nichts" <| fun () ->
            Expect.isFalse (sends (RetryTargetToYnab tripId) (pageWith [ category tripId None ])) "nichts offen"

        testCase "Die Antwort gibt den Knopf frei und liest den Monat still neu; ein Fehler nennt den Grund" <| fun () ->
            let running = step (RetryTargetToYnab tripId) (pageWith [ category tripId notInYnab ])
            let answered, cmd, external = update (TargetToYnabRetried (tripId, Error "HTTP 500")) running
            Expect.isFalse (targetRetryRunning tripId answered) "frei"
            Expect.isNonEmpty cmd "Monat neu lesen"
            Expect.equal external (ShowToast ("Target weiter nicht in YNAB: HTTP 500", ToastError)) "Grund"
            let _, _, ok = update (TargetToYnabRetried (tripId, Ok ())) running
            Expect.equal ok (ShowToast ("Target in YNAB geschrieben", ToastSuccess)) "Erfolg"

        // Regeln über mehrere Nachrichten (Doktrin Punkt 20): jede Reihenfolge bis Tiefe 4 aus Tipp auf A, Tipp auf B
        // und den Antworten. Orakel: gesendet wird nur ohne laufenden Aufruf, eine Antwort gibt nur ihren frei.
        testCase "Jede Reihenfolge aus Tipps und Antworten sendet höchstens einen Aufruf zugleich" <| fun () ->
            let page = pageWith [ category tripId notInYnab; category holidayId notInYnab ]
            let msgs =
                [ RetryTargetToYnab tripId
                  RetryTargetToYnab holidayId
                  TargetToYnabRetried (tripId, Ok ())
                  TargetToYnabRetried (holidayId, Error "x") ]
            let rec walk (depth: int) (model: Model) (running: Guid option) =
                if depth > 0 then
                    for msg in msgs do
                        let next, cmd, _ = update msg model
                        let expected =
                            match msg, running with
                            | RetryTargetToYnab id, None -> Some id
                            | TargetToYnabRetried (id, _), Some r when r = id -> None
                            | _ -> running
                        Expect.equal next.TargetYnabRetry expected $"läuft nach {msg}"
                        match msg with
                        | RetryTargetToYnab _ -> Expect.equal (not (List.isEmpty cmd)) running.IsNone $"gesendet bei {msg}"
                        | _ -> ()
                        walk (depth - 1) next expected
            walk 4 page None
    ]

let private report : TargetsToYnabReport =
    { Lines =
        [ { CategoryId = Guid.NewGuid(); CategoryName = "A"; Outcome = GoalAlreadyInYnab }
          { CategoryId = Guid.NewGuid(); CategoryName = "B"; Outcome = GoalWritten [] }
          { CategoryId = Guid.NewGuid(); CategoryName = "C"; Outcome = GoalWritten [ WeekdayMissing ] }
          { CategoryId = Guid.NewGuid(); CategoryName = "D"; Outcome = GoalWriteFailed "Rate-Limit" }
          { CategoryId = Guid.NewGuid(); CategoryName = "E"; Outcome = GoalNotAttempted } ]
      RateLimited = true }

let private runTests =
    testList "Targets nach YNAB im Client: einmaliger Lauf" [
        testCase "Erst bestätigen, dann einmal senden; die Antwort zeigt den Bericht und liest den Monat neu" <| fun () ->
            let page = { pageWith [ category tripId None ] with MenuOpen = true }
            let asking = step AskWriteAllTargets page
            Expect.equal asking.TargetsToYnab TargetsToYnabAsking "Bestätigung"
            Expect.isFalse asking.MenuOpen "Menü zu"
            Expect.isFalse (sends ConfirmWriteAllTargets page) "ohne Bestätigung kein Senden"
            Expect.isTrue (sends ConfirmWriteAllTargets asking) "sendet"
            let running = step ConfirmWriteAllTargets asking
            Expect.isFalse (sends ConfirmWriteAllTargets running) "kein zweites Senden"
            Expect.equal (step CloseWriteAllTargets running).TargetsToYnab TargetsToYnabRunning "läuft bleibt sichtbar"
            Expect.equal (step AskWriteAllTargets running).TargetsToYnab TargetsToYnabRunning "kein neuer Lauf"
            let done', cmd, _ = update (AllTargetsWritten (Ok report)) running
            Expect.equal done'.TargetsToYnab (TargetsToYnabDone report) "Bericht"
            Expect.isNonEmpty cmd "Monat neu lesen"
            Expect.equal (step CloseWriteAllTargets done').TargetsToYnab TargetsToYnabClosed "geschlossen"
            Expect.equal (step (AllTargetsWritten (Error "kaputt")) running).TargetsToYnab (TargetsToYnabFailed "kaputt") "Fehler"

        testCase "Der Bericht nach Gruppen: Geschrieben, Mit Abweichung, Fehlgeschlagen, Nicht versucht" <| fun () ->
            let groups = targetsToYnabGroups report
            Expect.equal groups.Written [ "B" ] "geschrieben"
            Expect.equal groups.WithGaps [ "C", [ "In YNAB ohne Wochentag" ] ] "mit Abweichung"
            Expect.equal groups.Failed [ "D", "Rate-Limit" ] "fehlgeschlagen"
            Expect.equal groups.NotAttempted [ "E" ] "offen"
            Expect.equal groups.Unchanged 1 "schon gleich"
    ]

[<Tests>]
let tests = testList "Targets nach YNAB im Client" [ hintTests; retryTests; runTests ]
