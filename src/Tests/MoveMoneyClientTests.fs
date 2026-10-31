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
let private ready = { openMoveForm food with Counterpart = Some (CategorySide trip.Id); AmountInput = Some "20,00" }
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
            Expect.equal (moveAmountText month form) "30,50" "|Verfügbar|"
        }
        test "Verfügbar positiv: X ist Quelle, Betrag ist Verfügbar" {
            let form = openMoveForm food
            Expect.isTrue form.IsSource "Quelle"
            Expect.equal (moveAmountText month form) "80,00" "Verfügbar"
        }
        test "Verfügbar 0: X ist Quelle" {
            Expect.isTrue (openMoveForm (category "Leer" 0m)).IsSource "Quelle"
        }
        test "Der Umschalter dreht Quelle und Ziel im Auftrag, der Betrag bleibt" {
            let toggled = toggleDirection ready
            Expect.equal (moveAmountText month toggled) "20,00" "Betrag bleibt"
            Expect.equal (moveRequest month ready |> Option.map (fun r -> r.Source, r.Target))
                (Some (CategorySide food.Id, CategorySide trip.Id)) "X ist Quelle"
            Expect.equal (moveRequest month toggled |> Option.map (fun r -> r.Source, r.Target))
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
            let form = { ready with AmountInput = Some "500,00" }
            Expect.equal (moveAfter month form).Head.After (eur -420m) "negativ"
            Expect.isTrue (canMove month form) "keine Sperre"
        }
        test "Ohne gültigen Betrag bleiben die heutigen Werte" {
            Expect.equal (moveAfter month { ready with AmountInput = Some "" }).Head.After (eur 80m) "unverändert"
        }
    ]

let private submitTests =
    testList "Verschieben drücken" [
        test "Betrag 0 oder leer sperrt, update schickt nichts" {
            for text in [ ""; "0"; "0,00"; "abc"; "-5" ] do
                let form = { ready with AmountInput = Some text }
                Expect.isFalse (canMove month form) $"gesperrt bei '{text}'"
                let model, _ = update ConfirmMove (withForm form)
                Expect.equal model.Move.Value.Submission Editing "kein Aufruf"
        }
        test "Ohne Gegenseite ist gesperrt" {
            Expect.isFalse (canMove month { ready with Counterpart = None }) "gesperrt"
        }
        test "Der erste Tipp liefert den Auftrag und setzt den Ladezustand" {
            let model, request = confirmMove (withForm ready)
            Expect.equal model.Move.Value.Submission Sending "läuft"
            Expect.equal (request |> Option.map (fun (_, r) -> r.Amount)) (Some (eur 20m)) "Auftrag"
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
            Expect.isSome (openMove food.Id loaded).Move "geladen"
            Expect.isNone (openMove food.Id (init ())).Move "nicht geladen"
            Expect.isNone (openMove (Guid.NewGuid ()) loaded).Move "unbekannte Kategorie"
        }
        test "Scheitert das Lesen, bleibt das Sheet offen und nennt den Grund" {
            let model, external, reload = applyMoveAnswered (Error "YNAB antwortet nicht") sending
            Expect.equal model.Move.Value.Submission (Rejected "YNAB antwortet nicht") "Grund"
            Expect.isTrue (canMove month model.Move.Value) "wieder bedienbar"
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

// budgeting-nfrds: das Sheet hielt eine Kopie der Kategorie und rechnete nach dem Neu-Lesen falsch.
let private currentMonthTests =
    let foodAt (available: decimal) =
        { month with Groups = [ { Name = "Alltag"; Categories = [ { food with Available = eur available }; trip ] } ] }
    let afterFirstMove = foodAt 50m
    let reloaded model m = applyLoaded CurrentMonth (Ok m) model
    testList "Aktueller Monat" [
        test "Zwei Verschiebungen kurz hintereinander: das zweite Sheet zeigt Vorbelegung und Verfügbar danach aus dem aktuellen Monat" {
            let second = openMove food.Id loaded
            let model = reloaded second afterFirstMove
            let form = { model.Move.Value with Counterpart = Some (CategorySide trip.Id) }
            Expect.equal (moveAmountText afterFirstMove form) "50,00" "Vorbelegung folgt dem neuen Stand"
            Expect.equal (moveAfter afterFirstMove form |> List.map (fun l -> l.After)) [ eur 0m; eur 19.5m ] "gerechnet vom neuen Stand"
            let _, request = confirmMove { model with Move = Some form }
            Expect.equal (request |> Option.map (fun (_, r) -> r.Amount)) (Some (eur 50m)) "der Auftrag trägt den aktuellen Betrag"
        }
        test "Getipptes bleibt, auch wenn der Monat neu kommt" {
            let model = reloaded (withForm ready) afterFirstMove
            Expect.equal (moveAmountText afterFirstMove model.Move.Value) "20,00" "Eingabe bleibt"
            Expect.equal (moveAfter afterFirstMove model.Move.Value).Head.After (eur 30m) "gerechnet vom neuen Stand"
        }
        test "Der Auftrag trägt den Monat, den die Seite zeigt" {
            let _, request = confirmMove (withForm ready)
            Expect.equal (request |> Option.map fst) (Some "2026-11-01") "BudgetMonth.Month"
        }
        test "Fehlt X im neuen Stand, schließt das Sheet" {
            let without = { month with Groups = [ { Name = "Alltag"; Categories = [ trip ] } ] }
            Expect.isNone (reloaded (withForm ready) without).Move "zu"
        }
        test "Während des Aufrufs bleibt das Sheet auch dann offen" {
            let sending = fst (confirmMove (withForm ready))
            let without = { month with Groups = [ { Name = "Alltag"; Categories = [ trip ] } ] }
            Expect.isSome (reloaded sending without).Move "die Antwort steht noch aus"
        }
    ]

let private pageChangeTests =
    testList "Seitenwechsel" [
        // budgeting-nfrds: das Formular blieb im Modell und sprang beim Zurückkehren wieder auf.
        test "Ein offenes Sheet überlebt das Betreten der Seite nicht" {
            let model, _ = update PageEntered (withForm ready)
            Expect.isNone model.Move "zu"
        }
        test "Kommt die Absage erst nach dem Seitenwechsel, nennt ein Toast den Grund" {
            let sending = fst (confirmMove (withForm ready))
            let model, _ = update PageEntered sending
            let _, external, _ = applyMoveAnswered (Error "abgelehnt") model
            Expect.equal external (ShowToast ("Nicht verschoben: abgelehnt", ToastError)) "sichtbar"
        }
    ]

let private reloadTests =
    testList "Stilles Neu-Lesen" [
        // budgeting-nfrds: ein gescheiterter Reload setzte `Failure` und nahm die Liste weg.
        test "Scheitert es, bleibt die Liste stehen und ein Hinweis erscheint" {
            let model, external = applyReloaded CurrentMonth (Error "Timeout") loaded
            Expect.equal model.Month (Success month) "Liste bleibt"
            match external with
            | ShowToast (text, ToastWarning) -> Expect.stringContains text "Timeout" "Grund"
            | other -> failtest $"Hinweis erwartet: {other}"
        }
        test "Gelingt es, kommt der neue Stand ohne Hinweis" {
            let fresh = { month with ToBeBudgeted = eur 5m }
            let model, external = applyReloaded CurrentMonth (Ok fresh) loaded
            Expect.equal model.Month (Success fresh) "neu"
            Expect.equal external NoOp "still"
        }
        test "Eine Antwort zur abgewählten Monatswahl ändert nichts" {
            let model, external = applyReloaded NextMonth (Error "Timeout") loaded
            Expect.equal model loaded "verworfen"
            Expect.equal external NoOp "still"
        }
        test "Ohne geladene Liste zeigt ein Fehler wie sonst den Fehlerzustand" {
            let model, _ = applyReloaded CurrentMonth (Error "Timeout") (init ())
            Expect.equal model.Month (Failure "Timeout") "nichts zu erhalten"
        }
    ]

let private retryTests =
    let failing = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Failed "Rate-Limit" ]
    let done' = report [ "Lebensmittel", 80m, 60m, Written; "Urlaub", 0m, 20m, Written ]
    let afterFailure = noteMoveReport failing loaded
    testList "Erneut senden bleibt erreichbar" [
        // budgeting-nfrds: nur der Toast trug die Aktion. Nach fünf Sekunden war sie weg.
        test "Eine fehlgeschlagene Verschiebung bleibt ohne Toast im Modell" {
            let model, _, _ = applyMoveAnswered (Ok failing) (fst (confirmMove (withForm ready)))
            Expect.equal
                (model.OpenRetries |> List.map (fun r -> r.ApplicationId, r.Reason, r.State))
                [ "app-1", "Rate-Limit", RetryReady ] "offen"
        }
        test "Eine gelungene Verschiebung hinterlässt nichts" {
            Expect.isEmpty (noteMoveReport done' loaded).OpenRetries "nichts offen"
        }
        test "Zwei fehlgeschlagene Verschiebungen verdrängen einander nicht" {
            let model = noteMoveReport { failing with ApplicationId = "app-2" } afterFailure
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

[<Tests>]
let tests =
    testList
        "Verschieben, Client (budgeting-afd8h)"
        [ directionTests; pickerTests; afterTests; submitTests; answerTests; currentMonthTests; pageChangeTests; reloadTests; retryTests ]
