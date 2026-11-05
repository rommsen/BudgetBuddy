/// Der Kern des Target-Formulars ohne das Modell der Budget-Seite (budgeting-r7k2m). Hier steht nur, was die
/// Tests des Tabs Target nicht erreichen: dort schließt `syncTargetForm` eine offene Bestätigung, sobald
/// kein Target mehr gezeigt wird, und ohne Target steht kein Knopf "Target entfernen". Ein zweiter Nutzer
/// (Seite Targets prüfen, ADR 0082 Punkt 11) verlässt sich auf die Sperre im Kern selbst.
module Tests.TargetFormCoreTests

open System
open Expecto
open Components.Budget.TargetFormCore

let private categoryId = Guid.Parse "00000000-0000-0000-0000-0000000a8401"

let private free : TargetFormLocks = { Waits = false; Running = None; WaitHint = None }

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
    ]
