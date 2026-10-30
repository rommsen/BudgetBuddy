module Tests.MoveMoneyClientTests

// budgeting-afd8h: das Sheet "Verschieben" auf der Budget-Seite. `update` wird nur für Wege ohne
// Server-Aufruf gerufen: der Fable.Remoting-Proxy wirft unter .NET.

open System
open Expecto
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private category name (available: decimal) : BudgetCategory =
    { Id = Guid.NewGuid()
      Name = name
      Assigned = eur 0m
      Activity = eur 0m
      Available = eur available
      Underfunded = None
      Snoozed = false }

let private food = category "Lebensmittel" 80m
let private trip = category "Urlaub" -30.5m
let private month : BudgetMonth =
    { Month = "2026-11-01"
      ToBeBudgeted = eur 100m
      Groups = [ { Name = "Alltag"; Categories = [ food; trip ] } ] }

let private loaded = { init () with Month = Success month }
let private ready = { openMoveForm food with Counterpart = Some (CategorySide trip.Id); AmountText = "20,00" }
let private withForm form = { loaded with Move = Some form }

let private update msg model =
    let model', _, external = Components.Budget.State.update msg model
    model', external

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

let private directionTests =
    testList "Richtung" [
        test "Verfügbar negativ: X ist Ziel, Betrag ist der Fehlbetrag" {
            let form = openMoveForm trip
            Expect.isFalse form.IsSource "Ziel"
            Expect.equal form.AmountText "30,50" "|Verfügbar|"
        }
        test "Verfügbar positiv: X ist Quelle, Betrag ist Verfügbar" {
            let form = openMoveForm food
            Expect.isTrue form.IsSource "Quelle"
            Expect.equal form.AmountText "80,00" "Verfügbar"
        }
        test "Verfügbar 0: X ist Quelle" {
            Expect.isTrue (openMoveForm (category "Leer" 0m)).IsSource "Quelle"
        }
        test "Der Umschalter dreht Quelle und Ziel im Auftrag, der Betrag bleibt" {
            let toggled = toggleDirection ready
            Expect.equal toggled.AmountText ready.AmountText "Betrag bleibt"
            Expect.equal (moveRequest ready |> Option.map (fun r -> r.Source, r.Target))
                (Some (CategorySide food.Id, CategorySide trip.Id)) "X ist Quelle"
            Expect.equal (moveRequest toggled |> Option.map (fun r -> r.Source, r.Target))
                (Some (CategorySide trip.Id, CategorySide food.Id)) "X ist Ziel"
        }
    ]

let private pickerTests =
    testList "Gegenseite" [
        test "Der Picker bietet alle Kategorien des Monats außer X, mit Verfügbar aus dem Monat" {
            let picked = pickerCategories month food.Id
            Expect.equal (picked |> List.map (fun c -> c.Name)) [ "Urlaub" ] "X fehlt"
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
    ]

let private afterTests =
    testList "Verfügbar danach" [
        test "Stimmt für Quelle und Ziel" {
            Expect.equal (moveAfter month ready |> List.map (fun l -> l.Label, l.After))
                [ "Lebensmittel", eur 60m; "Urlaub", eur -10.5m ] "80 - 20 und -30,50 + 20"
        }
        test "Gedreht: X steigt, die Gegenseite sinkt" {
            Expect.equal (moveAfter month (toggleDirection ready) |> List.map (fun l -> l.After))
                [ eur 100m; eur -50.5m ] "umgekehrt"
        }
        test "Mit Zu verteilen als Gegenseite rechnet die zweite Zeile dort" {
            let form = { ready with Counterpart = Some ToBeBudgetedSide }
            Expect.equal (moveAfter month form |> List.map (fun l -> l.Label, l.After))
                [ "Lebensmittel", eur 60m; "Zu verteilen", eur 120m ] "Geld wandert zurück"
        }
        test "Ein negatives Ergebnis sperrt nicht" {
            let form = { ready with AmountText = "500,00" }
            Expect.equal (moveAfter month form).Head.After (eur -420m) "negativ"
            Expect.isTrue (canMove form) "keine Sperre"
        }
        test "Ohne gültigen Betrag bleiben die heutigen Werte" {
            Expect.equal (moveAfter month { ready with AmountText = "" }).Head.After (eur 80m) "unverändert"
        }
    ]

let private submitTests =
    testList "Verschieben drücken" [
        test "Betrag 0 oder leer sperrt, update schickt nichts" {
            for text in [ ""; "0"; "0,00"; "abc"; "-5" ] do
                let form = { ready with AmountText = text }
                Expect.isFalse (canMove form) $"gesperrt bei '{text}'"
                let model, _ = update ConfirmMove (withForm form)
                Expect.isFalse model.Move.Value.Sending "kein Aufruf"
        }
        test "Ohne Gegenseite ist gesperrt" {
            Expect.isFalse (canMove { ready with Counterpart = None }) "gesperrt"
        }
        test "Der erste Tipp liefert den Auftrag und setzt den Ladezustand" {
            let model, request = confirmMove (withForm ready)
            Expect.isTrue model.Move.Value.Sending "läuft"
            Expect.equal (request |> Option.map (fun r -> r.Amount)) (Some (eur 20m)) "Auftrag"
        }
        test "Ein Doppeltipp während des Aufrufs schickt keinen zweiten" {
            let sending, _ = confirmMove (withForm ready)
            let _, request = confirmMove sending
            Expect.isNone request "kein zweiter Auftrag"
            let model, _ = update ConfirmMove sending
            Expect.equal model sending "update tut nichts"
        }
        test "Während des Aufrufs bleibt das Sheet offen und die Eingabe fest" {
            let sending, _ = confirmMove (withForm ready)
            Expect.equal (closeMove sending) sending "kein Schließen"
            Expect.equal (fst (update (MoveAmountChanged "1") sending)) sending "keine Eingabe"
            Expect.equal (fst (update ToggleMoveDirection sending)) sending "kein Umschalten"
        }
    ]

let private answerTests =
    testList "Antwort" [
        let sending = fst (confirmMove (withForm ready))
        test "Ein Tipp öffnet das Sheet nur bei geladenem Monat" {
            Expect.isSome (openMove food loaded).Move "geladen"
            Expect.isNone (openMove food (init ())).Move "nicht geladen"
        }
        test "Scheitert das Lesen, bleibt das Sheet offen und nennt den Grund" {
            let model, external, reload = applyMoveAnswered (Error "YNAB antwortet nicht") sending
            Expect.equal model.Move.Value.Error (Some "YNAB antwortet nicht") "Grund"
            Expect.isFalse model.Move.Value.Sending "wieder bedienbar"
            Expect.equal external NoOp "kein Toast"
            Expect.isFalse reload "nichts geändert"
        }
        test "Nach Erfolg schließt das Sheet, der Monat lädt neu, der Toast nennt Betrag, Quelle und Ziel" {
            let r = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Written ]
            let model, external, reload = applyMoveAnswered (Ok r) sending
            Expect.isNone model.Move "zu"
            Expect.isTrue reload "neu laden"
            Expect.equal external (ShowToast ("20,00 € von Lebensmittel nach Urlaub", ToastSuccess)) "Toast"
        }
        test "Zuweisen nennt Zu verteilen, in beide Richtungen" {
            Expect.equal (moveSummaryText (report [ "Urlaub", 0m, 20m, Written ])) "20,00 € von Zu verteilen nach Urlaub" "Zuweisen"
            Expect.equal (moveSummaryText (report [ "Urlaub", 20m, 15m, Written ])) "5,00 € von Urlaub nach Zu verteilen" "Zurücknehmen"
        }
        test "Fehlgeschlagene Zeile: Toast mit Grund und Erneut senden derselben Anwendung" {
            let r = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
            match moveToast r with
            | ShowToastWithAction (text, ToastError, action) ->
                Expect.stringContains text "Rate-Limit" "Grund"
                Expect.equal action.Label "Erneut senden" "Beschriftung"
                Expect.equal action.Kind (RetryBudgetMove "app-1") "Anwendung"
            | other -> failtest $"Toast mit Aktion erwartet: {other}"
        }
        test "Hat YNAB gewonnen, warnt der Toast statt Erfolg zu melden" {
            match moveToast (report [ "Lebensmittel", 80m, 60m, ChangedInYnab; "Urlaub", 0m, 20m, ChangedInYnab ]) with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtest $"Warnung erwartet: {other}"
        }
        test "Bricht der Aufruf ab, schließt das Sheet: ein zweiter Versuch verschöbe doppelt" {
            let model, external = applyMoveCallLost "Timeout" sending
            Expect.isNone model.Move "zu"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "unklar" "ehrlich"
            | other -> failtest $"Fehler-Toast erwartet: {other}"
        }
    ]

[<Tests>]
let tests =
    testList "Verschieben, Client (budgeting-afd8h)" [ directionTests; pickerTests; afterTests; submitTests; answerTests ]
