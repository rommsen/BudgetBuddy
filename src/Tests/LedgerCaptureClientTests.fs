module LedgerCaptureClientTests

// "Neue Buchung" im Kassenbuch, Client-`update` unter .NET (ledger-at63w): Vorbelegung, Zuletzt-Chips,
// Kategorie zum Payee, Sperre während "Buchung anlegen" (alle Reihenfolgen, Doktrin Punkt 20),
// sichtbare Fehler und die Rückmeldung mit "Rückgängig". Die `Run*`-Nachrichten mit `Client.Api`
// werden nur als ausgehende Nachricht geprüft, nie ausgeführt.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private bar = Guid.Parse "0195c000-0000-7000-8000-0000000000b1"
let private tagesgeld = Guid.Parse "0195c000-0000-7000-8000-0000000000b2"
let private giro = Guid.Parse "0195c000-0000-7000-8000-0000000000b3"
let private barY = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c1")
let private tagesgeldY = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c2")
let private giroY = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c3")
let private food = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000d1")
let private fuel = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000d2")
let private today = DateTime(2026, 11, 8)

let private template : LedgerBookingTemplate =
    { AccountId = tagesgeld; Payee = "Wochenmarkt"; Target = Some (BookOnCategory food); Amount = 23m; IsOutflow = true }

let private defaults : LedgerBookingDefaults =
    { Today = today; ImportAccountId = Some giro; LastUsedAccountId = Some tagesgeld; Recent = [ template ] }

let private account id name ynab : LedgerAccountView =
    { Id = id; Name = name; Kind = LedgerBankAccount; YnabAccountId = Some ynab; Closed = false }

let private budget : YnabAccount list =
    [ for id, name in [ barY, "Bar"; tagesgeldY, "Tagesgeld ING"; giroY, "Girokonto" ] ->
        { Id = id; Name = name; Balance = { Amount = 0m; Currency = "EUR" }; OnBudget = true; Closed = false } ]

let private baseModel (filter: AccountFilter option) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Accounts =
            Success
                { Accounts = [ account bar "Bar" barY; account tagesgeld "Tagesgeld ING" tagesgeldY; account giro "Girokonto" giroY ]
                  DefaultAccountId = Some giro }
        TransferTargets = Success budget
        SelectedFilter = filter }

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = run msg model
    model'

/// Das Formular nach "+ Buchung" und der geladenen Vorbelegung.
let private opened (filter: AccountFilter option) : Model =
    baseModel filter |> step OpenCapture |> step (CaptureDefaultsLoaded (Ok defaults))

let private formOf (model: Model) : Components.Ledger.Capture.CaptureForm =
    match model.Capture with
    | Some c -> c.Form
    | None -> failtest "Formular erwartet"

let private captureOf (model: Model) : Components.Ledger.Capture.CaptureState =
    match model.Capture with
    | Some c -> c
    | None -> failtest "Formular erwartet"

/// Alle Reihenfolgen einer Liste (für die Zustandsregeln, Doktrin Punkt 20).
let rec private permutations (xs: 'a list) : 'a list list =
    match xs with
    | [] -> [ [] ]
    | _ ->
        xs
        |> List.indexed
        |> List.collect (fun (i, x) ->
            let rest = xs |> List.indexed |> List.filter (fun (j, _) -> j <> i) |> List.map snd
            permutations rest |> List.map (fun p -> x :: p))

let private submitCalls (sent: Msg list) : LedgerBookingRequest list =
    sent |> List.choose (function RunSubmitCapture r -> Some r | _ -> None)

let private created (pushFailure: string option) : LedgerBookingCreated =
    { EntryId = Guid.Parse "0195c000-0000-7000-8000-0000000000e1"; AccountName = "Bar"; PushFailure = pushFailure }

[<Tests>]
let openTests =
    testList "Neue Buchung: Öffnen und Vorbelegung (ledger-at63w)" [
        testCase "+ Buchung öffnet das Formular, schließt das Detail und lädt die Vorbelegung" <| fun () ->
            let model, sent, _ = run OpenCapture (baseModel None)
            Expect.isSome model.Capture "offen"
            Expect.isNone model.DetailFor "kein Detail"
            Expect.contains sent RunLoadCaptureDefaults "Vorbelegung"

        testCase "das Konto kommt aus dem Kassenbuch-Filter, das Datum ist heute" <| fun () ->
            let form = formOf (opened (Some (OneAccount bar)))
            Expect.equal form.AccountId (Some bar) "Filter-Konto"
            Expect.equal form.Date today "heute"

        testCase "ohne Filter-Konto gilt das zuletzt genutzte" <| fun () ->
            Expect.equal (formOf (opened (Some AllAccounts))).AccountId (Some tagesgeld) "zuletzt genutzt"

        testCase "steht der Filter auf dem Import-Konto, wird es nicht vorbelegt" <| fun () ->
            Expect.equal (formOf (opened (Some (OneAccount giro)))).AccountId (Some tagesgeld) "nie das Import-Konto"

        testCase "kommen die Budget-Konten nach der Vorbelegung, wird das Konto nachgesetzt" <| fun () ->
            let model =
                { baseModel (Some (OneAccount bar)) with TransferTargets = NotAsked }
                |> step OpenCapture
                |> step (CaptureDefaultsLoaded (Ok defaults))
            Expect.isNone (formOf model).AccountId "noch keine wählbaren Konten"
            let model' = step (TransferTargetsLoaded (Ok budget)) model
            Expect.equal (formOf model').AccountId (Some bar) "nachgesetzt"

        // Regression (Review ledger-at63w): kamen die Kassenbuch-Konten zuletzt (Deep Link), blieb das Konto leer.
        testCase "kommen die Kassenbuch-Konten nach der Vorbelegung, wird das Konto nachgesetzt" <| fun () ->
            let accounts = (baseModel None).Accounts
            let model =
                { baseModel (Some (OneAccount bar)) with Accounts = NotAsked }
                |> step OpenCapture
                |> step (CaptureDefaultsLoaded (Ok defaults))
            Expect.isNone (formOf model).AccountId "noch keine Konten"
            match accounts with
            | Success view ->
                let model' = step (AccountsLoaded (Ok view)) model
                Expect.equal (formOf model').AccountId (Some bar) "nachgesetzt"
            | _ -> failtest "Konten erwartet"

        testCase "ein Detail schließt das Formular" <| fun () ->
            let entry = LedgerSelectionTests.entryOf 1 -5m false false
            let model = step (OpenDetail entry) (opened None)
            Expect.isNone model.Capture "Formular zu"
    ]

[<Tests>]
let fillTests =
    testList "Neue Buchung: Zuletzt-Chip und Kategorie zum Payee (ledger-at63w)" [
        testCase "ein Zuletzt-Chip füllt Payee, Kategorie, Betrag und Konto" <| fun () ->
            let form = formOf (opened (Some (OneAccount bar)) |> step (ApplyCaptureTemplate template))
            Expect.equal form.Payee "Wochenmarkt" "Payee"
            Expect.equal form.Target (Some (BookOnCategory food)) "Kategorie"
            Expect.equal form.AmountText "23,00" "Betrag"
            Expect.equal form.AccountId (Some tagesgeld) "Konto"
            Expect.isTrue form.IsOutflow "Ausgabe"

        testCase "die Kategorie zum Payee wird übernommen, solange kein Ziel gewählt ist" <| fun () ->
            let model =
                opened None
                |> step (SetCapturePayee "Tankstelle")
            let _, sent, _ = run CommitCapturePayee model
            Expect.equal sent [ RunLoadCapturePayeeCategory "Tankstelle" ] "Abfrage"
            let form = formOf (step (CapturePayeeCategoryLoaded ("Tankstelle", Some fuel)) model)
            Expect.equal form.Target (Some (BookOnCategory fuel)) "vorbelegt"

        testCase "ein gewähltes Ziel überschreibt die Kategorie zum Payee nicht" <| fun () ->
            let model =
                opened None
                |> step (ChooseCaptureTarget (Some (BookAsTransfer barY)))
                |> step (SetCapturePayee "Tankstelle")
            let _, sent, _ = run CommitCapturePayee model
            Expect.isEmpty sent "keine Abfrage"
            let form = formOf (step (CapturePayeeCategoryLoaded ("Tankstelle", Some fuel)) model)
            Expect.equal form.Target (Some (BookAsTransfer barY)) "Ziel bleibt"

        testCase "eine Antwort zu einem alten Payee wird verworfen" <| fun () ->
            let model = opened None |> step (SetCapturePayee "Bäckerei")
            let form = formOf (step (CapturePayeeCategoryLoaded ("Tankstelle", Some fuel)) model)
            Expect.isNone form.Target "kein Ziel"
    ]

[<Tests>]
let submitTests =
    testList "Neue Buchung: Buchung anlegen (ledger-at63w)" [
        testCase "Buchung anlegen schickt den Aufruf mit Konto, Betrag ohne Vorzeichen und Richtung" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step (SetCapturePayee "Bäckerei Lange")
                |> step (SetCaptureDirection false)
            let model', sent, _ = run SubmitCapture model
            Expect.isTrue (captureOf model').Saving "läuft"
            match sent with
            | [ RunSubmitCapture request ] ->
                Expect.equal (request.AccountId, request.Amount, request.IsOutflow, request.PayeeName) (bar, 8.60m, false, "Bäckerei Lange") "Aufruf"
                Expect.equal request.Date today "Datum"
            | other -> failtest $"genau ein Aufruf erwartet: {other}"

        testCase "ohne Betrag und Konto bleiben die Fehler im Formular, kein Aufruf" <| fun () ->
            let model = { opened None with Capture = (opened None).Capture |> Option.map (fun c -> { c with Form = { c.Form with AccountId = None } }) }
            let model', sent, _ = run SubmitCapture model
            Expect.isEmpty sent "kein Aufruf"
            Expect.equal
                (captureOf model').Errors
                [ Components.Ledger.Capture.amountInvalidText; Components.Ledger.Capture.accountMissingText ]
                "beide Fehler sichtbar"

        testCase "ein Fehler vom Server bleibt im Formular stehen, die Eingaben auch" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step SubmitCapture
                |> step (CaptureSubmitted (Error "Das Konto ist geschlossen."))
            let capture = captureOf model
            Expect.equal capture.Errors [ "Das Konto ist geschlossen." ] "Fehler sichtbar"
            Expect.isFalse capture.Saving "wieder bedienbar"
            Expect.equal capture.Form.AmountText "8,60" "Eingabe bleibt"

        testCase "Schließen ist gesperrt, solange der Aufruf läuft" <| fun () ->
            let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "1") |> step SubmitCapture
            Expect.isSome (step CloseCapture model).Capture "bleibt offen"

        testCase "nach dem Anlegen schließt das Formular, die Rückmeldung bietet Rückgängig" <| fun () ->
            let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "1") |> step SubmitCapture
            let model', sent, external = run (CaptureSubmitted (Ok (created None))) model
            Expect.isNone model'.Capture "zu"
            Expect.contains sent LoadEntries "Liste neu"
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "Buchung angelegt · Bar" "Text"
                Expect.equal action.Kind (UndoCapturedBooking (created None).EntryId) "Rückgängig"
            | other -> failtest $"Rückmeldung mit Rückgängig erwartet: {other}"

        testCase "am PC zeigt das Panel danach die neue Buchung" <| fun () ->
            let model = { (opened (Some (OneAccount bar))) with DetailForm = DetailPanel } |> step (SetCaptureAmount "1") |> step SubmitCapture
            let _, sent, _ = run (CaptureSubmitted (Ok (created None))) model
            Expect.contains sent (OpenRouteEntry (created None).EntryId) "Detail der neuen Buchung"

        testCase "am Handy öffnet danach kein Detail" <| fun () ->
            let model = { (opened (Some (OneAccount bar))) with DetailForm = DetailSheet } |> step (SetCaptureAmount "1") |> step SubmitCapture
            let _, sent, _ = run (CaptureSubmitted (Ok (created None))) model
            Expect.isFalse (sent |> List.exists (function OpenRouteEntry _ -> true | _ -> false)) "kein Detail"

        testCase "ein gescheiterter Push meldet sich als Warnung ohne Rückgängig" <| fun () ->
            let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "1") |> step SubmitCapture
            match run (CaptureSubmitted (Ok (created (Some "YNAB nicht erreichbar")))) model with
            | _, _, ShowToast (text, ToastWarning) ->
                Expect.stringContains text "noch nicht in YNAB" "Hinweis"
            | _, _, other -> failtest $"Warnung erwartet: {other}"

        testCase "Rückgängig an der Rückmeldung startet das Verwerfen der Buchung" <| fun () ->
            let id = (created None).EntryId
            Expect.equal (State.toastActionMsg (UndoCapturedBooking id)) (State.LedgerMsg (StartDiscardCaptured id)) "Nachricht"
            let _, sent, _ = run (StartDiscardCaptured id) (baseModel None)
            Expect.equal sent [ RunDiscardCaptured id ] "Aufruf"

        testCase "nach dem Verwerfen lädt die Liste neu und das Detail der Buchung schließt" <| fun () ->
            let entry = { LedgerSelectionTests.entryOf 1 -5m false false with Id = (created None).EntryId }
            let model = { baseModel None with DetailFor = Some entry }
            let model', sent, external = run (CapturedDiscarded (entry.Id, Ok ())) model
            Expect.isNone model'.DetailFor "Detail zu"
            Expect.contains sent LoadEntries "Liste neu"
            Expect.equal external (ShowToast (Components.Ledger.State.capturedDiscardedText, ToastSuccess)) "Rückmeldung"
    ]

/// Alle Reihenfolgen von zwei Tipps auf "Buchung anlegen" und der Antwort (Erfolg oder Fehler),
/// gegen ein eigenes Orakel: ein Aufruf startet nur, wenn keiner läuft und das Formular offen ist;
/// ein Erfolg schließt das Formular, ein Fehler gibt es wieder frei (Doktrin Punkt 20).
[<Tests>]
let lockOrderTests =
    let ways =
        [ for answer in [ CaptureSubmitted (Ok (created None)); CaptureSubmitted (Error "kaputt") ] do
              yield! permutations [ SubmitCapture; SubmitCapture; answer ] ]
        |> List.distinct
    testList "Neue Buchung: Sperre während Buchung anlegen (ledger-at63w)" [
        testCase "Jede Reihenfolge aus zwei Tipps und der Antwort sendet höchstens einen Aufruf" <| fun () ->
            for way in ways do
                let start = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "8,60")
                // Orakel ohne den getesteten Code.
                let mutable isOpen, sending, expectedCalls = true, false, 0
                let mutable model, calls = start, 0
                for msg in way do
                    match msg with
                    | SubmitCapture ->
                        if isOpen && not sending then
                            sending <- true
                            expectedCalls <- expectedCalls + 1
                    | CaptureSubmitted (Ok _) ->
                        isOpen <- false
                        sending <- false
                    | CaptureSubmitted (Error _) -> sending <- false
                    | _ -> ()
                    let model', sent, _ = run msg model
                    model <- model'
                    calls <- calls + List.length (submitCalls sent)
                Expect.equal calls expectedCalls $"Aufrufe bei {way}"
                Expect.equal model.Capture.IsSome isOpen $"offen oder zu bei {way}"
                Expect.equal (model.Capture |> Option.exists (fun c -> c.Saving)) sending $"Sperre bei {way}"
    ]

/// Regression (Review ledger-at63w): eine späte Antwort der Vorbelegung baute das Formular neu und
/// leerte Romans Eingaben (Öffnen, Schließen, Öffnen; die erste Antwort kommt, Roman tippt, die zweite
/// kommt). Dazu zeigte "Erneut versuchen" kein Laden, und zwei Tipps schickten zwei Anfragen.
[<Tests>]
let defaultsOrderTests =
    testList "Neue Buchung: späte Antworten der Vorbelegung (ledger-at63w)" [
        testCase "eine zweite Antwort der Vorbelegung lässt die Eingaben stehen" <| fun () ->
            let model =
                baseModel (Some (OneAccount bar))
                |> step OpenCapture
                |> step CloseCapture
                |> step OpenCapture
                |> step (CaptureDefaultsLoaded (Ok defaults))
                |> step (SetCaptureAmount "8,60")
                |> step (ChooseCaptureAccount tagesgeld)
                |> step (CaptureDefaultsLoaded (Ok defaults))
            Expect.equal (formOf model).AmountText "8,60" "Betrag bleibt"
            Expect.equal (formOf model).AccountId (Some tagesgeld) "Konto bleibt"

        testCase "ein später Fehler nach geladener Vorbelegung lässt das Formular stehen" <| fun () ->
            let model = opened None |> step (SetCaptureAmount "8,60") |> step (CaptureDefaultsLoaded (Error "weg"))
            Expect.isTrue (RemoteData.isSuccess (captureOf model).Defaults) "Vorbelegung bleibt"
            Expect.equal (formOf model).AmountText "8,60" "Betrag bleibt"

        testCase "Jede Reihenfolge aus zwei Tipps auf Erneut versuchen und der Antwort lädt höchstens einmal" <| fun () ->
            let failed = baseModel None |> step OpenCapture |> step (CaptureDefaultsLoaded (Error "kaputt"))
            let ways =
                [ for answer in [ CaptureDefaultsLoaded (Ok defaults); CaptureDefaultsLoaded (Error "wieder kaputt") ] do
                      yield! permutations [ RetryCaptureDefaults; RetryCaptureDefaults; answer ] ]
                |> List.distinct
            for way in ways do
                // Orakel: "fehlgeschlagen", "lädt" oder "geladen"; nur aus "fehlgeschlagen" startet ein Aufruf.
                let mutable oracle, expectedCalls = "fehlgeschlagen", 0
                let mutable model, calls = failed, 0
                for msg in way do
                    match msg with
                    | RetryCaptureDefaults when oracle = "fehlgeschlagen" ->
                        oracle <- "lädt"
                        expectedCalls <- expectedCalls + 1
                    | CaptureDefaultsLoaded (Ok _) when oracle <> "geladen" -> oracle <- "geladen"
                    | CaptureDefaultsLoaded (Error _) when oracle <> "geladen" -> oracle <- "fehlgeschlagen"
                    | _ -> ()
                    let model', sent, _ = run msg model
                    model <- model'
                    calls <- calls + (sent |> List.filter ((=) RunLoadCaptureDefaults) |> List.length)
                let actual =
                    match (captureOf model).Defaults with
                    | Success _ -> "geladen"
                    | Loading -> "lädt"
                    | Failure _ -> "fehlgeschlagen"
                    | NotAsked -> "nicht angefragt"
                Expect.equal calls expectedCalls $"Aufrufe bei {way}"
                Expect.equal actual oracle $"Zustand bei {way}"
    ]

/// Regression (Review ledger-at63w): ein zweiter Tipp auf "Rückgängig" schickte die Rücknahme noch
/// einmal, die zweite scheiterte und meldete "Rückgängig nicht möglich".
[<Tests>]
let discardOrderTests =
    let id = (created None).EntryId
    let ways =
        [ for answer in [ CapturedDiscarded (id, Ok ()); CapturedDiscarded (id, Error "kaputt") ] do
              yield! permutations [ StartDiscardCaptured id; StartDiscardCaptured id; answer ] ]
        |> List.distinct
    testList "Neue Buchung: Sperre während Rückgängig (ledger-at63w)" [
        testCase "Jede Reihenfolge aus zwei Tipps auf Rückgängig und der Antwort nimmt höchstens einmal zurück" <| fun () ->
            for way in ways do
                // Orakel: eine Rücknahme startet nur, wenn keine läuft; die Antwort gibt frei.
                let mutable running, expectedCalls = false, 0
                let mutable model, calls = baseModel None, 0
                for msg in way do
                    match msg with
                    | StartDiscardCaptured _ when not running ->
                        running <- true
                        expectedCalls <- expectedCalls + 1
                    | CapturedDiscarded _ -> running <- false
                    | _ -> ()
                    let model', sent, _ = run msg model
                    model <- model'
                    calls <- calls + (sent |> List.filter (function RunDiscardCaptured _ -> true | _ -> false) |> List.length)
                Expect.equal calls expectedCalls $"Aufrufe bei {way}"
    ]

/// Regression (Review ledger-at63w): die Kategorie zum Payee kommt erst nach dem Verlassen des Felds.
/// Wer danach direkt "Buchung anlegen" tippte, legte ohne die vorgeschlagene Kategorie an.
[<Tests>]
let payeeLookupOrderTests =
    testList "Neue Buchung: Buchung anlegen wartet auf die Kategorie zum Payee (ledger-at63w)" [
        testCase "Jede Reihenfolge aus zwei Tipps und der Antwort zum Payee legt einmal mit der Kategorie an" <| fun () ->
            let start =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step (SetCapturePayee "Tankstelle")
                |> step CommitCapturePayee
            let ways = permutations [ SubmitCapture; SubmitCapture; CapturePayeeCategoryLoaded ("Tankstelle", Some fuel) ] |> List.distinct
            for way in ways do
                let mutable model, requests = start, []
                for msg in way do
                    let model', sent, _ = run msg model
                    model <- model'
                    requests <- requests @ submitCalls sent
                match requests with
                | [ request ] -> Expect.equal request.Target (Some (BookOnCategory fuel)) $"Kategorie im Aufruf bei {way}"
                | other -> failtest $"genau ein Aufruf erwartet bei {way}: {other}"
                Expect.isTrue (captureOf model).Saving $"läuft bei {way}"

        testCase "solange die Kategorie zum Payee lädt, ist Buchung anlegen belegt, aber startet nichts" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step (SetCapturePayee "Tankstelle")
                |> step CommitCapturePayee
            let model', sent, _ = run SubmitCapture model
            Expect.isEmpty (submitCalls sent) "kein Aufruf vor der Antwort"
            Expect.isTrue (captureOf model').Saving "Knopf belegt"

        testCase "eine Abfrage zu einem alten Payee hält Buchung anlegen nicht auf" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step (SetCapturePayee "Tankstelle")
                |> step CommitCapturePayee
                |> step (SetCapturePayee "Bäckerei")
            let _, sent, _ = run SubmitCapture model
            Expect.equal (submitCalls sent |> List.map (fun r -> r.PayeeName)) [ "Bäckerei" ] "sofort angelegt"

        testCase "ein falscher Betrag meldet sich sofort, auch wenn die Kategorie noch lädt" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCapturePayee "Tankstelle")
                |> step CommitCapturePayee
            let model', sent, _ = run SubmitCapture model
            Expect.isEmpty sent "kein Aufruf"
            Expect.equal (captureOf model').Errors [ Components.Ledger.Capture.amountInvalidText ] "Fehler sichtbar"
            Expect.isFalse (captureOf model').Saving "bedienbar"
    ]
