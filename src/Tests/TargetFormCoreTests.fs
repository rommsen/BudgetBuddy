/// Der Kern des Target-Formulars ohne das Modell der Budget-Seite (budgeting-r7k2m). Hier steht nur, was die
/// Tests des Tabs Target nicht erreichen: dort schließt `syncTargetForm` eine offene Bestätigung, sobald
/// kein Target mehr gezeigt wird, und ohne Target steht kein Knopf "Target entfernen". Ein zweiter Nutzer
/// (Seite Targets prüfen, ADR 0082 Punkt 11) verlässt sich auf die Sperre im Kern selbst.
module Tests.TargetFormCoreTests

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Components.Budget.TargetFormCore

let private categoryId = Guid.Parse "00000000-0000-0000-0000-0000000a8401"

let private free : TargetFormLocks = { Waits = false; Running = None; WaitHint = None }

let private saving : TargetFormLocks = { free with Running = Some SavingTarget }

let private weeklyOn (day: int option) : Target =
    { Goal = { Amount = 25m; Currency = "EUR" }
      Rhythm = Weeks 1
      Kind = Need
      Date = None
      NeedsWholeAmount = None
      Day = day
      CreatedIn = None }

let private monthly : Target = { weeklyOn (Some 15) with Rhythm = Months 1 }

let private blank = emptyTargetForm categoryId "2026-11-01"

[<Tests>]
let tests =
    testList "Target-Formular-Kern" [
        testCase "Ohne gezeigtes Target öffnet Entfernen keine Bestätigung" <| fun () ->
            let form = emptyTargetForm categoryId "2026-11-01"
            Expect.isFalse (formAskRemove None free form).ConfirmingRemove "keine Bestätigung"

        testCase "Ist das Target weg, während die Bestätigung offen steht, sendet Entfernen nichts" <| fun () ->
            let form = { emptyTargetForm categoryId "2026-11-01" with ConfirmingRemove = true }
            let _, send = formConfirmRemove None free form
            Expect.isNone send "kein Aufruf"

        // budgeting-h2brj: das Feld Wochentag statt sieben Knöpfen.
        testCase "Das Feld Wochentag zeigt den Tag ausgeschrieben, ohne Tag den gelben Platzhalter, gesperrt während des Speicherns" <| fun () ->
            Expect.equal
                (targetWeekdayField (Some (weeklyOn (Some 1))) free blank)
                (Some { Day = Some "Montag"; Locked = false; Open = false })
                "Montag gelesen"
            Expect.equal
                (targetWeekdayField (Some (weeklyOn (Some 1))) free { blank with WeekdayInput = Some 0 })
                (Some { Day = Some "Sonntag"; Locked = false; Open = false })
                "Wahl vor dem gelesenen Tag"
            Expect.equal
                (targetWeekdayField (Some (weeklyOn None)) free blank)
                (Some { Day = None; Locked = false; Open = false })
                "ohne Tag kein stiller Vorschlag"
            Expect.equal
                (targetWeekdayField (Some (weeklyOn (Some 3))) saving blank |> Option.map (fun f -> f.Locked))
                (Some true)
                "gesperrt wie der Rhythmus"
            Expect.isNone (targetWeekdayField (Some monthly) free blank) "monatlich ohne Feld"
            Expect.isSome (targetWeekdayField (Some monthly) free { blank with RhythmInput = Some Weekly }) "nach Wahl Woche"

        testCase "Die Auswahl öffnet nur bei stehendem, freiem Feld und schließt mit der Wahl" <| fun () ->
            let weekly = Some (weeklyOn None)
            let opened = formOpenWeekdayChoice weekly free blank
            Expect.isTrue opened.WeekdayChoiceOpen "offen"
            Expect.equal (targetWeekdayField weekly free opened |> Option.map (fun f -> f.Open)) (Some true) "Feld weiß es"
            Expect.isFalse (formOpenWeekdayChoice weekly saving blank).WeekdayChoiceOpen "gesperrt: nicht antippbar"
            Expect.isFalse (formOpenWeekdayChoice (Some monthly) free blank).WeekdayChoiceOpen "ohne Feld keine Auswahl"
            let chosen = formChooseWeekday 3 free opened
            Expect.equal (chosen.WeekdayInput, chosen.WeekdayChoiceOpen) (Some 3, false) "Mittwoch, zu"
            Expect.isFalse (formCloseWeekdayChoice opened).WeekdayChoiceOpen "Schließen ohne Wahl"
            Expect.isNone (formCloseWeekdayChoice opened).WeekdayInput "nichts gewählt"
    ]
