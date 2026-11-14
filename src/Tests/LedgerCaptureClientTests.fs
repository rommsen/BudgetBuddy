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
    { AccountId = tagesgeld; Payee = "Wochenmarkt"; Target = Some (BookOnCategory food); Amount = 23m; IsOutflow = true; Memo = Some "Obst" }

let private defaults : LedgerBookingDefaults =
    { Today = today; ImportAccountId = Some giro; LastUsedAccountId = Some tagesgeld; QuickAddAccountId = None; Recent = [ template ] }

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

        // ledger-ea70s: ohne Kontofilter (Alle Konten, Übersicht) das Quick-Add-Konto, sonst das zuletzt genutzte.
        testCase "ohne Filter-Konto gilt das Quick-Add-Konto vor dem zuletzt genutzten" <| fun () ->
            let withQuickAdd = { defaults with QuickAddAccountId = Some bar }
            let form filter =
                baseModel filter |> step OpenCapture |> step (CaptureDefaultsLoaded (Ok withQuickAdd)) |> formOf
            Expect.equal (form (Some AllAccounts)).AccountId (Some bar) "Alle Konten"
            Expect.equal (form None).AccountId (Some bar) "ohne Filter"
            Expect.equal (form (Some (OneAccount tagesgeld))).AccountId (Some tagesgeld) "ein gefiltertes Konto geht vor"

        testCase "ein nicht buchbares Quick-Add-Konto belegt nichts vor, es gilt das zuletzt genutzte" <| fun () ->
            let withImport = { defaults with QuickAddAccountId = Some giro }
            let form = baseModel (Some AllAccounts) |> step OpenCapture |> step (CaptureDefaultsLoaded (Ok withImport)) |> formOf
            Expect.equal form.AccountId (Some tagesgeld) "zuletzt genutzt"

        testCase "auf der Übersicht zählt ein Kontofilter des Kassenbuchs nicht" <| fun () ->
            let withQuickAdd = { defaults with QuickAddAccountId = Some bar }
            let model =
                baseModel (Some (OneAccount tagesgeld))
                |> step OpenCaptureUnfiltered
                |> step (CaptureDefaultsLoaded (Ok withQuickAdd))
            Expect.equal (formOf model).AccountId (Some bar) "Quick-Add-Konto statt des Filters"

        testCase "kommen die Konten nach der Vorbelegung, wird auch das Quick-Add-Konto nachgesetzt" <| fun () ->
            let withQuickAdd = { defaults with QuickAddAccountId = Some bar }
            let model =
                { baseModel None with TransferTargets = NotAsked }
                |> step OpenCapture
                |> step (CaptureDefaultsLoaded (Ok withQuickAdd))
            Expect.isNone (formOf model).AccountId "noch keine wählbaren Konten"
            Expect.equal (formOf (step (TransferTargetsLoaded (Ok budget)) model)).AccountId (Some bar) "nachgesetzt"

        testCase "ein Zuletzt-Chip belegt das Konto des Chips, auch über dem Quick-Add-Konto" <| fun () ->
            let withQuickAdd = { defaults with QuickAddAccountId = Some bar }
            let model =
                baseModel None
                |> step OpenCapture
                |> step (CaptureDefaultsLoaded (Ok withQuickAdd))
                |> step (ApplyCaptureTemplate template)
            Expect.equal (formOf model).AccountId (Some tagesgeld) "Konto des Chips"

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

let private barTemplate (payee: string) (memo: string option) : LedgerBookingTemplate =
    { AccountId = bar; Payee = payee; Target = Some (BookOnCategory fuel); Amount = 5m; IsOutflow = true; Memo = memo }

/// Vorlagen auf zwei Konten, so wie der Server sie liefert: je Konto neueste zuerst (ledger-sre0f).
let private twoAccounts : LedgerBookingDefaults =
    { defaults with
        Recent =
            [ barTemplate "Anastasia" (Some "Putzen")
              barTemplate "Kiosk" None
              template
              { template with Payee = "Bäckerei Lange"; Amount = 8.60m; Memo = None } ] }

let private recentOf (model: Model) : string list =
    let capture = captureOf model
    match capture.Defaults with
    | Success d -> Components.Ledger.Capture.templatesFor capture.Form.AccountId d |> List.map (fun t -> t.Payee)
    | _ -> failtest "Vorbelegung erwartet"

[<Tests>]
let recentPerAccountTests =
    testList "Neue Buchung: Zuletzt-Chips des gewählten Kontos (ledger-sre0f)" [
        testCase "nur die Vorlagen des gewählten Kontos, in der Reihenfolge vom Server" <| fun () ->
            let shown = Components.Ledger.Capture.templatesFor (Some bar) twoAccounts |> List.map (fun t -> t.Payee)
            Expect.equal shown [ "Anastasia"; "Kiosk" ] "nur Bar"
            let other = Components.Ledger.Capture.templatesFor (Some tagesgeld) twoAccounts |> List.map (fun t -> t.Payee)
            Expect.equal other [ "Wochenmarkt"; "Bäckerei Lange" ] "nur Tagesgeld"

        testCase "ohne gewähltes Konto keine Chips" <| fun () ->
            Expect.isEmpty (Components.Ledger.Capture.templatesFor None twoAccounts) "keine"

        testCase "höchstens fünf" <| fun () ->
            let many = { defaults with Recent = [ for i in 1..7 -> barTemplate $"P{i}" None ] }
            Expect.equal (Components.Ledger.Capture.templatesFor (Some bar) many).Length 5 "fünf"

        testCase "wechselt das Konto im Formular, zeigt Zuletzt sofort dessen Vorlagen, ohne neue Anfrage" <| fun () ->
            let model =
                baseModel (Some (OneAccount bar)) |> step OpenCapture |> step (CaptureDefaultsLoaded (Ok twoAccounts))
            Expect.equal (recentOf model) [ "Anastasia"; "Kiosk" ] "Bar vorbelegt"
            let model', sent, _ = run (ChooseCaptureAccount tagesgeld) model
            Expect.equal (recentOf model') [ "Wochenmarkt"; "Bäckerei Lange" ] "Tagesgeld"
            Expect.isEmpty sent "keine neue Anfrage"

        testCase "ein Chip füllt das Memo der Vorlage, ein zweiter ohne Memo lässt kein altes stehen, das Datum bleibt" <| fun () ->
            let start = baseModel (Some (OneAccount bar)) |> step OpenCapture |> step (CaptureDefaultsLoaded (Ok twoAccounts))
            let first = start |> step (ApplyCaptureTemplate (barTemplate "Anastasia" (Some "Putzen")))
            Expect.equal (formOf first).Memo "Putzen" "Memo der Vorlage"
            let second = first |> step (ApplyCaptureTemplate (barTemplate "Kiosk" None))
            Expect.equal (formOf second).Memo "" "kein altes Memo"
            Expect.equal (formOf second).Date today "Datum bleibt"

        testCase "der Text des Chips bleibt Payee und Betrag, ohne Memo" <| fun () ->
            Expect.equal (Components.Ledger.Capture.templateLabel (barTemplate "Anastasia" (Some "Putzen"))) "Anastasia · 5,00" "ohne Memo"
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
            Expect.equal sent [ RevealCaptureAmountError ] "kein Aufruf, nur Fokus ins Betragsfeld"
            let capture = captureOf model'
            Expect.equal
                (capture.AmountError, capture.AccountError, capture.ServerErrors)
                (Some Components.Ledger.Capture.amountEmptyText, Some Components.Ledger.Capture.accountMissingText, [])
                "beide Fehler am Feld, keiner gesammelt"

        // Bug ledger-vf6z5: der Betragsfehler stand gesammelt am Formularende, das Feld bekam weder Fokus noch
        // Markierung (Styleguide §5, Vorbild Sheet "Betrag" aus ledger-q42pg).
        // Review a3f7eb1d..982bdf79: ein leeres Feld meldete „Betrag nicht lesbar, etwa 12,50“, obwohl nichts
        // getippt war. Leer heißt jetzt „Betrag fehlt“, unlesbar bleibt beim Satz mit dem Beispiel.
        testCase "ein leerer oder unlesbarer Betrag schickt keinen Aufruf, nur Fokus und die passende Meldung ins Bild" <| fun () ->
            for text, expected in
                [ "", Components.Ledger.Capture.amountEmptyText
                  "   ", Components.Ledger.Capture.amountEmptyText
                  "abc", Components.Ledger.Capture.amountInvalidText ] do
                let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount text)
                let model', sent, external = run SubmitCapture model
                Expect.equal sent [ RevealCaptureAmountError ] $"nur der Fokus-Effekt bei '{text}'"
                Expect.equal external NoOp "kein Toast"
                Expect.equal (captureOf model').AmountError (Some expected) $"Fehler am Feld bei '{text}'"
                Expect.isFalse (captureOf model').Saving "bedienbar"

        testCase "ein Betrag von 0 meldet sich am Feld mit Fokus" <| fun () ->
            let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "0")
            let model', sent, _ = run SubmitCapture model
            Expect.equal sent [ RevealCaptureAmountError ] "nur der Fokus-Effekt"
            Expect.equal (captureOf model').AmountError (Some Components.Ledger.Capture.amountZeroText) "Fehler am Feld"

        testCase "ein gültiger Betrag ohne Konto gibt keinen Fokus-Effekt" <| fun () ->
            let model =
                { opened None with
                    Capture = (opened None).Capture |> Option.map (fun c -> { c with Form = { c.Form with AccountId = None } }) }
                |> step (SetCaptureAmount "8,60")
            let model', sent, _ = run SubmitCapture model
            Expect.isEmpty sent "kein Aufruf, kein Fokus"
            let capture = captureOf model'
            Expect.equal (capture.AmountError, capture.AccountError) (None, Some Components.Ledger.Capture.accountMissingText) "nur der Kontofehler"

        testCase "Fokus und Meldung ins Bild: ein Effekt, das Modell bleibt" <| fun () ->
            let rejected = opened (Some (OneAccount bar)) |> step SubmitCapture
            let model, cmd, external = Components.Ledger.State.update RevealCaptureAmountError rejected
            Expect.equal model rejected "Modell unverändert"
            Expect.equal (List.length cmd) 1 "genau ein Effekt"
            Expect.equal external NoOp "kein Toast"

        testCase "Tippen im Betragsfeld nimmt den Betragsfehler zurück, der Kontofehler bleibt" <| fun () ->
            let rejected =
                { opened None with
                    Capture = (opened None).Capture |> Option.map (fun c -> { c with Form = { c.Form with AccountId = None } }) }
                |> step SubmitCapture
            let typed = captureOf (step (SetCaptureAmount "8") rejected)
            Expect.equal (typed.AmountError, typed.AccountError) (None, Some Components.Ledger.Capture.accountMissingText) "nur der Betragsfehler weg"

        testCase "ein Zuletzt-Chip füllt Betrag und Konto und nimmt beide Fehler zurück" <| fun () ->
            let rejected =
                { opened None with
                    Capture = (opened None).Capture |> Option.map (fun c -> { c with Form = { c.Form with AccountId = None } }) }
                |> step SubmitCapture
            let filled = captureOf (step (ApplyCaptureTemplate template) rejected)
            Expect.equal (filled.AmountError, filled.AccountError) (None, None) "keine Fehler mehr"

        testCase "die Wahl eines Kontos nimmt den Kontofehler zurück, der Betragsfehler bleibt" <| fun () ->
            let rejected =
                { opened None with
                    Capture = (opened None).Capture |> Option.map (fun c -> { c with Form = { c.Form with AccountId = None } }) }
                |> step SubmitCapture
            let chosen = captureOf (step (ChooseCaptureAccount bar) rejected)
            Expect.equal (chosen.AmountError, chosen.AccountError) (Some Components.Ledger.Capture.amountEmptyText, None) "nur der Kontofehler weg"

        testCase "ein Fehler vom Server bleibt im Formular stehen, die Eingaben auch" <| fun () ->
            let model =
                opened (Some (OneAccount bar))
                |> step (SetCaptureAmount "8,60")
                |> step SubmitCapture
                |> step (CaptureSubmitted (Error "Das Konto ist geschlossen."))
            let capture = captureOf model
            Expect.equal capture.ServerErrors [ "Das Konto ist geschlossen." ] "Fehler gesammelt über Buchung anlegen"
            Expect.equal (capture.AmountError, capture.AccountError) (None, None) "kein Feldfehler"
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
                Expect.equal text "Buchung angelegt · Bar · -1,00 €" "Konto und Betrag (§7.13)"
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
            Expect.equal (captureOf model').AmountError (Some Components.Ledger.Capture.amountEmptyText) "Fehler am Feld"
            Expect.equal sent [ RevealCaptureAmountError ] "Fokus sofort, kein Warten auf die Kategorie"
            Expect.isFalse (captureOf model').Saving "bedienbar"
    ]

/// Die Kontoliste in „Neue Buchung“ (ledger-vf6z5): sie sagte „Konten werden geladen“ bei jeder leeren Liste, auch nach
/// einem Ladefehler oder ohne wählbares Konto.
[<Tests>]
let accountListTests =
    let choice (model: Model) = Components.Ledger.State.captureAccountChoice model (captureOf model)
    testList "Neue Buchung: Kontoliste lädt, scheitert oder ist leer (ledger-vf6z5)" [
        testCase "solange die Konten laden, lädt die Liste" <| fun () ->
            let model = { opened None with Accounts = Loading }
            Expect.equal (choice model) Components.Ledger.Capture.CaptureAccountsLoading "lädt"
            let targets = { opened None with TransferTargets = Loading }
            Expect.equal (choice targets) Components.Ledger.Capture.CaptureAccountsLoading "Budget-Konten laden"

        testCase "nach einem Ladefehler meldet die Liste den Fehler" <| fun () ->
            let model = { opened None with TransferTargets = Failure "Zeitüberschreitung" }
            Expect.equal (choice model) Components.Ledger.Capture.CaptureAccountsFailed "Fehler"
            let accounts = { opened None with Accounts = Failure "Zeitüberschreitung" }
            Expect.equal (choice accounts) Components.Ledger.Capture.CaptureAccountsFailed "Fehler der Konten"

        testCase "ohne wählbares Konto steht kein Konto zum Buchen" <| fun () ->
            let model = { opened None with TransferTargets = Success [] }
            Expect.equal (choice model) Components.Ledger.Capture.NoBookableAccount "leer"

        testCase "mit Konten zeigt die Liste die wählbaren ohne das Import-Konto" <| fun () ->
            match choice (opened None) with
            | Components.Ledger.Capture.BookableAccounts accounts ->
                Expect.equal (accounts |> List.map (fun a -> a.Id) |> Set.ofList) (Set.ofList [ bar; tagesgeld ]) "ohne Girokonto"
            | other -> failtest $"Konten erwartet: {other}"
    ]

/// „Neue Buchung“ auf der Übersicht (ledger-s1weh): ab 1024 px ein Dialog in der Mitte, darunter das Sheet. Esc im
/// Dialog schließt eine Stufe, erst die Kontoliste, dann das Formular, nie während „Buchung anlegen“ läuft.
[<Tests>]
let overviewDialogTests =
    let escape (model: Model) = step (Components.Ledger.View.captureEscapeMsg (captureOf model)) model
    testList "Neue Buchung: Dialog auf der Übersicht am PC (ledger-s1weh)" [
        testCase "ab 1024 px öffnet das Formular als Dialog, darunter als Sheet" <| fun () ->
            Expect.equal (Components.Ledger.View.captureShellFor true) CaptureDialog "PC"
            Expect.equal (Components.Ledger.View.captureShellFor false) CaptureSheet "Handy und schmales Fenster"

        testCase "Esc bei offener Kontoliste schließt nur die Liste" <| fun () ->
            let model = opened None |> step (OpenCapturePicker Components.Ledger.Capture.CaptureAccountPicker) |> escape
            Expect.isNone (captureOf model).Picker "Liste zu"

        // ledger-ea70s: im Sheet ersetzen die Auswahlen den Inhalt; nach Wahl oder Schließen stehen die Eingaben noch da.
        testCase "Auswahl öffnen und schließen oder wählen lässt Betrag, Payee und Memo stehen" <| fun () ->
            let filled =
                opened None
                |> step (SetCaptureAmount "8,60")
                |> step (SetCapturePayee "Bäckerei")
                |> step (SetCaptureMemo "Brötchen")
            let kept (model: Model) =
                let f = formOf model
                f.AmountText, f.Payee, f.Memo
            let closed = filled |> step (OpenCapturePicker Components.Ledger.Capture.CaptureTargetPicker) |> step CloseCapturePicker
            Expect.equal (kept closed) ("8,60", "Bäckerei", "Brötchen") "nach Schließen"
            let chosen =
                filled
                |> step (OpenCapturePicker Components.Ledger.Capture.CaptureTargetPicker)
                |> step (ChooseCaptureTarget (Some (BookOnCategory food)))
            Expect.equal (kept chosen) ("8,60", "Bäckerei", "Brötchen") "nach der Wahl"
            Expect.isNone (captureOf chosen).Picker "zurück im Formular"
            let account =
                filled
                |> step (OpenCapturePicker Components.Ledger.Capture.CaptureAccountPicker)
                |> step (ChooseCaptureAccount bar)
            Expect.equal (kept account) ("8,60", "Bäckerei", "Brötchen") "nach der Kontowahl"
            Expect.isNone (captureOf account).Picker "zurück im Formular"

        testCase "Esc ohne offene Auswahl schließt das Formular" <| fun () ->
            Expect.isNone (escape (opened None)).Capture "Formular zu"

        testCase "Esc während Buchung anlegen läuft lässt das Formular offen" <| fun () ->
            let model = opened (Some (OneAccount bar)) |> step (SetCaptureAmount "1") |> step SubmitCapture
            Expect.isSome (escape model).Capture "bleibt offen"
    ]
