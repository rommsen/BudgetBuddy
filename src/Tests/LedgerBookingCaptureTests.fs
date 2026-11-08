module LedgerBookingCaptureTests

// Buchung im Kassenbuch erfassen (ledger-at63w).
//
// Gruppen:
//  * Validierung an der API-Grenze (rein): dieselben Regeln wie Quick Add, dazu Konto und Ziel;
//  * Konto und Ziel gegen den Bestand (`resolveLedgerBookingAccounts`, rein);
//  * der Befehl `addLedgerBooking` gegen In-Memory-SQLite mit injizierten Werkzeugen, die nie
//    YNAB erreichen und ihre Aufrufe aufzeichnen (Vorbild `LedgerTransferTests`);
//  * "Erneut senden" eines Übertrags über das echte `Server.Api.entryPushWith`;
//  * "Rückgängig" (`discardCapturedBooking`): YNAB zuerst, dann `EntryDiscardedInBb`;
//  * die Vorbelegung: Konto, heute im Berliner Kalender, Zuletzt-Chips, Kategorie zum Payee.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private now = DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc)
let private today = DateTime(2026, 11, 8)

let private food : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-00000000ab01"); CategoryName = "Lebensmittel" }

/// Konten je Testfall: Bar (wählbar), Tagesgeld (wählbar, Ziel), das Import-Konto (Girokonto),
/// ein geschlossenes, ein Tracking-Konto und ein BB-eigenes Konto.
type private Accounts = {
    Bar: LedgerAccountId
    BarYnab: YnabAccountId
    Tagesgeld: LedgerAccountId
    TagesgeldYnab: YnabAccountId
    Import: LedgerAccountId
    ImportYnab: YnabAccountId
    Closed: LedgerAccountId
    Tracking: LedgerAccountId
    TrackingYnab: YnabAccountId
    BbOwned: LedgerAccountId
    Ledger: LedgerAccount list
    Budget: TransferAccount list
}

let private freshAccounts () : Accounts =
    let ynab () = YnabAccountId (Guid.NewGuid())
    let ledger () = LedgerAccountId (Guid.NewGuid())
    let barY, tagY, impY, closedY, trackY = ynab (), ynab (), ynab (), ynab (), ynab ()
    let bar, tag, imp, closed, track, own = ledger (), ledger (), ledger (), ledger (), ledger (), ledger ()
    let account id name link isClosed : LedgerAccount =
        { Id = id; Name = name; Kind = BankAccount; Link = link; Closed = isClosed }
    let ledgerAccounts = [
        account bar "Bar" (YnabMirrored barY) false
        account tag "Tagesgeld ING" (YnabMirrored tagY) false
        account imp "Girokonto" (YnabMirrored impY) false
        account closed "Altes Konto" (YnabMirrored closedY) true
        account track "Depot" (YnabMirrored trackY) false
        account own "Sparschwein" BbOwned false
    ]
    for a in ledgerAccounts do
        saveAccount a |> Async.RunSynchronously
    let budget (id: YnabAccountId) name onBudget isClosed payee : TransferAccount =
        { Id = id; Name = name; OnBudget = onBudget; Closed = isClosed; TransferPayeeId = payee }
    {
        Bar = bar
        BarYnab = barY
        Tagesgeld = tag
        TagesgeldYnab = tagY
        Import = imp
        ImportYnab = impY
        Closed = closed
        Tracking = track
        TrackingYnab = trackY
        BbOwned = own
        Ledger = ledgerAccounts
        Budget = [
            budget barY "Bar" true false (Some "payee-bar")
            budget tagY "Tagesgeld ING" true false (Some "payee-tagesgeld")
            budget impY "Girokonto" true false (Some "payee-giro")
            budget closedY "Altes Konto" true true (Some "payee-alt")
            budget trackY "Depot" false false (Some "payee-depot")
        ]
    }

let private guidOf (LedgerAccountId id) = id

let private request (account: LedgerAccountId) : LedgerBookingRequest = {
    AccountId = guidOf account
    Amount = 8.60m
    IsOutflow = true
    PayeeName = "Bäckerei Lange"
    Target = Some (BookOnCategory food.CategoryId)
    Date = today
    Memo = Some "Brötchen"
}

/// Die Werkzeuge des Befehls: zeichnen Pushs auf und liefern feste Ergebnisse.
type private BookingProbe(accounts: Accounts, pushResult: YnabResult<string>) =
    member val Pushes : (string * YnabAccountId * string option * ManualTransactionRequest) list = [] with get, set
    member val Mirrors = 0 with get, set
    member val BudgetResult : YnabResult<TransferAccount list> = Ok accounts.Budget with get, set
    member val CategoryResult : Result<CategoryRef, string> = Ok food with get, set
    member this.Tools : LedgerBookingTools =
        { LedgerAccounts = fun () -> async.Return accounts.Ledger
          ImportAccount = fun () -> async.Return (Some accounts.ImportYnab)
          BudgetAccounts = fun () -> async.Return this.BudgetResult
          ResolveCategory = fun _ -> async.Return this.CategoryResult
          Push =
            fun importId account transferPayee request ->
                async {
                    this.Pushes <- this.Pushes @ [ importId, account, transferPayee, request ]
                    return pushResult
                }
          TriggerMirror = fun () -> this.Mirrors <- this.Mirrors + 1 }

let private eventsOf (entryId: EntryId) : RecordedLedgerEvent list =
    loadEventsForEntry entryId |> Async.RunSynchronously

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private book (probe: BookingProbe) (entryId: EntryId) (req: LedgerBookingRequest) =
    addLedgerBooking now entryId probe.Tools req |> Async.RunSynchronously

let private setup () =
    global.Persistence.initializeDatabase ()
    initializeLedgerSchema ()

// ============================================
// Validierung an der API-Grenze
// ============================================

let private errorsOf (req: LedgerBookingRequest) : string list =
    match Server.Validation.validateLedgerBooking today req with
    | Ok _ -> []
    | Error errors -> errors

[<Tests>]
let validationTests =
    let valid = request (LedgerAccountId (Guid.NewGuid()))
    testList "Buchung erfassen: Prüfung an der API-Grenze (ledger-at63w)" [
        testCase "eine vollständige Buchung ist gültig" <| fun () ->
            Expect.isEmpty (errorsOf valid) "keine Fehler"

        testCase "ohne Konto: konkrete Meldung" <| fun () ->
            Expect.contains (errorsOf { valid with AccountId = Guid.Empty }) Server.Validation.bookingAccountMissingText "Konto"

        testCase "ein Ziel ohne Id: konkrete Meldung" <| fun () ->
            Expect.contains
                (errorsOf { valid with Target = Some (BookAsTransfer (YnabAccountId Guid.Empty)) })
                Server.Validation.bookingTargetMissingText
                "Zielkonto"
            Expect.contains
                (errorsOf { valid with Target = Some (BookOnCategory (YnabCategoryId Guid.Empty)) })
                Server.Validation.bookingTargetMissingText
                "Kategorie"

        // Dieselben Regeln wie Quick Add (Doktrin Punkt 4): jeder Fall mit der Meldung von
        // `validateManualTransaction`.
        testCase "Betrag 0 wird abgelehnt wie bei Quick Add" <| fun () ->
            Expect.contains (errorsOf { valid with Amount = 0m }) "Amount must be greater than zero" "Betrag 0"

        testCase "ein negativer Betrag wird abgelehnt, die Richtung kommt aus Ausgabe oder Einnahme" <| fun () ->
            Expect.contains (errorsOf { valid with Amount = -5m }) "Amount must be greater than zero" "negativ"

        testCase "ein unvernünftig großer Betrag wird abgelehnt" <| fun () ->
            Expect.contains (errorsOf { valid with Amount = 1_000_001m }) "Amount is unreasonably large" "zu groß"

        testCase "ein Payee über 200 Zeichen wird abgelehnt" <| fun () ->
            Expect.contains
                (errorsOf { valid with PayeeName = String('x', 201) })
                "Payee muss zwischen 1 und 200 Zeichen lang sein"
                "Payee zu lang"

        testCase "ein Datum in der Zukunft wird abgelehnt" <| fun () ->
            Expect.contains (errorsOf { valid with Date = today.AddDays 1.0 }) "Date cannot be in the future" "Zukunft"

        testCase "ein Datum mehr als fünf Jahre zurück wird abgelehnt" <| fun () ->
            Expect.contains (errorsOf { valid with Date = today.AddYears(-5).AddDays(-1.0) }) "Date is too far in the past" "zu alt"

        testCase "ein Memo über 500 Zeichen wird abgelehnt" <| fun () ->
            Expect.contains
                (errorsOf { valid with Memo = Some (String('m', 501)) })
                "Memo muss zwischen 0 und 500 Zeichen lang sein"
                "Memo zu lang"

        testCase "alle Fehler werden gesammelt" <| fun () ->
            Expect.equal (errorsOf { valid with AccountId = Guid.Empty; Amount = 0m }).Length 2 "zwei Fehler"

        testCase "ein leerer Payee ist erlaubt" <| fun () ->
            Expect.isEmpty (errorsOf { valid with PayeeName = "" }) "ohne Payee"
    ]

[<Tests>]
let resolveTests =
    // Konten ohne Datenbank: nur die reine Prüfung.
    let ynab () = YnabAccountId (Guid.NewGuid())
    let barY, tagY, impY = ynab (), ynab (), ynab ()
    let bar = LedgerAccountId (Guid.NewGuid())
    let ledgerAccounts : LedgerAccount list =
        [ { Id = bar; Name = "Bar"; Kind = BankAccount; Link = YnabMirrored barY; Closed = false } ]
    let budget : TransferAccount list =
        [ { Id = barY; Name = "Bar"; OnBudget = true; Closed = false; TransferPayeeId = Some "p-bar" }
          { Id = tagY; Name = "Tagesgeld"; OnBudget = true; Closed = false; TransferPayeeId = None }
          { Id = impY; Name = "Giro"; OnBudget = true; Closed = false; TransferPayeeId = Some "p-giro" } ]
    let resolve target =
        Server.Validation.resolveLedgerBookingAccounts ledgerAccounts budget (Some impY) { request bar with Target = target }
    testList "Buchung erfassen: Konto und Ziel gegen den Bestand (ledger-at63w)" [
        testCase "ein Ziel-Konto ohne Übertrags-Payee wird abgelehnt" <| fun () ->
            Expect.equal (resolve (Some (BookAsTransfer tagY))) (Error Server.Validation.bookingTransferNoPayeeText) "ohne Payee"

        testCase "ein unbekanntes Ziel-Konto wird abgelehnt" <| fun () ->
            Expect.equal (resolve (Some (BookAsTransfer (ynab ())))) (Error Server.Validation.bookingTransferTargetText) "unbekannt"

        testCase "eine Kategorie als Ziel liefert das Konto ohne Übertrag" <| fun () ->
            match resolve (Some (BookOnCategory food.CategoryId)) with
            | Ok (account, None) -> Expect.equal account.YnabAccountId barY "YNAB-Konto"
            | other -> failtest $"Konto ohne Übertrag erwartet: {other}"

        testCase "das Import-Konto als Ziel eines Übertrags ist erlaubt" <| fun () ->
            match resolve (Some (BookAsTransfer impY)) with
            | Ok (_, Some transfer) -> Expect.equal transfer.TransferPayeeId "p-giro" "Übertrags-Payee"
            | other -> failtest $"Übertrag erwartet: {other}"
    ]

// ============================================
// Befehl addLedgerBooking
// ============================================

[<Tests>]
let commandTests =
    testSequenced
    <| testList "Buchung erfassen: Befehl (ledger-at63w)" [
        testCase "eine Ausgabe auf Bar schreibt CashExpenseAdded mit UserInBb und pusht mit der festen import_id" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Ok "ynab-at63w-1")
            let entryId = newEntryId ()
            let result = book probe entryId (request accounts.Bar)

            Expect.equal result (Ok { EntryId = (let (EntryId g) = entryId in g); AccountName = "Bar"; PushFailure = None }) "angelegt"
            let events = eventsOf entryId
            Expect.equal (events |> List.map (fun r -> r.EventType)) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "Ereignisse"
            Expect.isTrue (events |> List.forall (fun r -> r.Event.Actor = UserInBb)) "Actor UserInBb"
            match events.Head.Event.Body with
            | CashExpenseAdded p ->
                Expect.equal p.AccountId accounts.Bar "gewähltes Konto"
                Expect.equal p.Amount (eur -8.60m) "Ausgabe negativ im Kassenbuch"
                Expect.equal p.Category (Some food) "Kategorie mit Namen"
                Expect.equal p.ImportId (Some (cashEntryImportId entryId)) "feste import_id im Ereignis"
            | other -> failtest $"CashExpenseAdded erwartet: {other}"
            match probe.Pushes with
            | [ importId, account, transferPayee, req ] ->
                Expect.equal importId (cashEntryImportId entryId) "feste import_id im Push"
                Expect.equal account accounts.BarYnab "YNAB-Konto von Bar"
                Expect.isNone transferPayee "kein Übertrag"
                Expect.equal req.CategoryId (Some food.CategoryId) "Kategorie im Push"
                Expect.equal (req.Amount, req.IsOutflow) (8.60m, true) "Betrag ohne Vorzeichen, Ausgabe"
            | other -> failtest $"genau ein Push erwartet: {other}"
            Expect.equal probe.Mirrors 1 "Spiegel nach dem Push angestoßen"

        testCase "eine Einnahme steht positiv im Kassenbuch" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Ok "ynab-at63w-2")
            let entryId = newEntryId ()
            book probe entryId { request accounts.Tagesgeld with IsOutflow = false; Amount = 50m } |> ignore
            Expect.equal (entryOf entryId).Amount (eur 50m) "Einnahme positiv"
            Expect.equal (entryOf entryId).AccountId accounts.Tagesgeld "Konto"

        testCase "ein Konto als Ziel bucht einen Übertrag: payee_id ist die Übertrags-Payee, keine category_id" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Ok "ynab-at63w-3")
            let entryId = newEntryId ()
            book probe entryId { request accounts.Bar with Target = Some (BookAsTransfer accounts.TagesgeldYnab) } |> ignore

            let events = eventsOf entryId
            Expect.equal
                (events |> List.map (fun r -> r.EventType))
                [ "CashExpenseAdded"; "EntryTransferTargetSetInBb"; "EntryLinkedToYnab" ]
                "Übertrag vor dem Push im Log"
            let entry = entryOf entryId
            Expect.equal entry.TransferAccountId (Some accounts.TagesgeldYnab) "Übertrags-Ziel"
            Expect.isNone entry.Category "ohne Kategorie"
            Expect.equal entry.Payee (Some "Transfer : Tagesgeld ING") "Payee wie YNAB ihn nennt"
            Expect.equal entry.PayeeBeforeTransfer (Some "Bäckerei Lange") "Payee davor bleibt"
            match probe.Pushes with
            | [ importId, account, transferPayee, req ] ->
                Expect.equal transferPayee (Some "payee-tagesgeld") "Übertrags-Payee"
                Expect.isNone req.CategoryId "keine Kategorie im Request"
                let body = buildNewTransactionBody (Some importId) None transferPayee account req
                Expect.stringContains body "\"payee_id\": \"payee-tagesgeld\"" "payee_id im Rumpf"
                Expect.isFalse (body.Contains "category_id") "keine category_id im Rumpf"
                Expect.isFalse (body.Contains "payee_name") "kein payee_name im Rumpf"
            | other -> failtest $"genau ein Push erwartet: {other}"

        testCase "ein gescheiterter Push lässt die Buchung stehen und nennt den Grund" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Error (YnabError.NetworkError "weg"))
            let entryId = newEntryId ()
            match book probe entryId (request accounts.Bar) with
            | Ok created -> Expect.isSome created.PushFailure "Grund gemeldet"
            | Error e -> failtest $"Buchung erwartet: {e}"
            Expect.equal
                (eventsOf entryId |> List.map (fun r -> r.EventType))
                [ "CashExpenseAdded"; "EntryPushToYnabFailed" ]
                "Noch nicht in YNAB"
            Expect.isFalse (entryOf entryId).LinkedToYnab "nicht verknüpft"

        // Jede Ablehnung ohne Ereignis und ohne Push, mit dem Satz aus Validation.fs.
        for label, change, expected in
            [ "das Import-Konto", (fun (a: Accounts) (r: LedgerBookingRequest) -> { r with AccountId = guidOf a.Import }), Server.Validation.bookingImportAccountText
              "ein geschlossenes Konto", (fun a r -> { r with AccountId = guidOf a.Closed }), Server.Validation.bookingAccountClosedText
              "ein unbekanntes Konto", (fun _ r -> { r with AccountId = Guid.NewGuid() }), Server.Validation.bookingAccountUnknownText
              "ein Tracking-Konto", (fun a r -> { r with AccountId = guidOf a.Tracking }), Server.Validation.bookingAccountNotBudgetText
              "ein BB-eigenes Konto", (fun a r -> { r with AccountId = guidOf a.BbOwned }), Server.Validation.bookingAccountNotInYnabText
              "ein Übertrag auf das eigene Konto", (fun a r -> { r with Target = Some (BookAsTransfer a.BarYnab) }), Server.Validation.bookingTransferOwnAccountText
              "ein Übertrag auf ein Tracking-Konto", (fun a r -> { r with Target = Some (BookAsTransfer a.TrackingYnab) }), Server.Validation.bookingTransferTargetText ] do
            testCase $"Ablehnung ohne Ereignis und ohne Push: {label}" <| fun () ->
                setup ()
                let accounts = freshAccounts ()
                let probe = BookingProbe(accounts, Ok "nie")
                let entryId = newEntryId ()
                let result = book probe entryId (change accounts (request accounts.Bar))
                Expect.equal result (Error expected) "konkrete Meldung"
                Expect.isEmpty (eventsOf entryId) "kein Ereignis"
                Expect.isEmpty probe.Pushes "kein Push"

        testCase "Ablehnung ohne Ereignis und ohne Push: Konten nicht ladbar" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Ok "nie")
            probe.BudgetResult <- Error (YnabError.NetworkError "weg")
            let entryId = newEntryId ()
            match book probe entryId (request accounts.Bar) with
            | Error message -> Expect.stringStarts message "Die Konten konnten nicht geladen werden" "Satz"
            | Ok _ -> failtest "Ablehnung erwartet"
            Expect.isEmpty (eventsOf entryId) "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "Ablehnung ohne Ereignis und ohne Push: Kategorie unbekannt" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Ok "nie")
            probe.CategoryResult <- Error Ledger.QueryHandlers.categoryUnknownText
            let entryId = newEntryId ()
            Expect.equal (book probe entryId (request accounts.Bar)) (Error Ledger.QueryHandlers.categoryUnknownText) "Satz"
            Expect.isEmpty (eventsOf entryId) "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "API-Adapter: ungültige Eingabe wird ohne YNAB und ohne Ereignis abgelehnt" <| fun () ->
            setup ()
            let entryId = newEntryId ()
            let ports : Server.Api.LedgerBookingPorts = {
                Setting = fun _ -> async.Return (Some "x")
                LoadYnabCategories = fun _ _ -> failwith "kein YNAB"
                BudgetAccounts = fun _ _ -> failwith "kein YNAB"
                Push = fun _ _ _ _ _ _ _ -> failwith "kein YNAB"
                Delete = fun _ _ _ -> failwith "kein YNAB"
                NewEntryId = fun () -> entryId
                TriggerMirror = ignore
            }
            let result =
                Server.Api.addLedgerBookingWith ports today now { request (LedgerAccountId (Guid.NewGuid())) with Amount = 0m }
                |> Async.RunSynchronously
            Expect.equal result (Error "Amount must be greater than zero") "Satz aus Validation.fs"
            Expect.isEmpty (eventsOf entryId) "kein Ereignis"

        testCase "API-Adapter: der Push geht mit Token, Budget, Konto und Übertrags-Payee hinaus" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            let calls = ResizeArray()
            let settings =
                Map [ "ynab_token", "tok"; "ynab_default_budget_id", "bud"
                      Ledger.QueryHandlers.importAccountSettingKey, (let (YnabAccountId g) = accounts.ImportYnab in string g) ]
            let ports : Server.Api.LedgerBookingPorts = {
                Setting = fun key -> async.Return (Map.tryFind key settings)
                LoadYnabCategories = fun _ _ -> failwith "nicht gebraucht"
                BudgetAccounts = fun token budget -> async { calls.Add $"konten {token} {budget}"; return Ok accounts.Budget }
                Push =
                    fun importId flag transferPayee token budget account _ ->
                        async {
                            calls.Add $"push {importId} {flag} {transferPayee} {token} {budget} {account}"
                            return Ok "ynab-at63w-api"
                        }
                Delete = fun _ _ _ -> failwith "nicht gebraucht"
                NewEntryId = fun () -> entryId
                TriggerMirror = ignore
            }
            let result =
                Server.Api.addLedgerBookingWith
                    ports today now { request accounts.Bar with Target = Some (BookAsTransfer accounts.TagesgeldYnab) }
                |> Async.RunSynchronously
            Expect.isOk result "angelegt"
            let budget = YnabBudgetId "bud"
            let importId = Some (cashEntryImportId entryId)
            let noFlag : string option = None
            let payee = Some "payee-tagesgeld"
            Expect.equal
                (List.ofSeq calls)
                [ $"konten tok {budget}"
                  $"push {importId} {noFlag} {payee} tok {budget} {accounts.BarYnab}" ]
                "Aufrufe"
            // Das Import-Konto aus den Settings wird abgelehnt.
            let refused =
                Server.Api.addLedgerBookingWith ports today now (request accounts.Import) |> Async.RunSynchronously
            Expect.equal refused (Error Server.Validation.bookingImportAccountText) "Import-Konto aus den Settings"
    ]

// ============================================
// Erneut senden eines erfassten Übertrags
// ============================================

[<Tests>]
let retryTransferTests =
    testSequenced
    <| testList "Buchung erfassen: Erneut senden eines Übertrags (ledger-at63w)" [
        testCase "Erneut senden schickt den Übertrag wieder mit der Übertrags-Payee und ohne Kategorie" <| fun () ->
            // Ohne die Übertrags-Payee legte "Erneut senden" in YNAB einen Payee "Transfer : …" an,
            // ohne Geld auf das Zielkonto zu buchen.
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Error (YnabError.NetworkError "weg"))
            let entryId = newEntryId ()
            book probe entryId { request accounts.Bar with Target = Some (BookAsTransfer accounts.TagesgeldYnab) } |> ignore

            let sent = ResizeArray()
            let create importId _ transferPayee _ _ account (req: ManualTransactionRequest) =
                async {
                    sent.Add((importId, transferPayee, account, req.CategoryId))
                    return Ok "ynab-retry"
                }
            let settings = Map [ "ynab_token", "tok"; "ynab_default_budget_id", "bud" ]
            let push = Server.Api.entryPushWith (fun k -> async.Return (Map.tryFind k settings)) create |> Async.RunSynchronously
            let result = retryPushToYnab now push ignore entryId |> Async.RunSynchronously
            Expect.equal result (Ok PushToYnabSent) "gesendet"
            Expect.equal
                (List.ofSeq sent)
                [ Some (cashEntryImportId entryId), Some "payee-tagesgeld", accounts.BarYnab, None ]
                "Übertrags-Payee, keine Kategorie"

        testCase "Erneut senden einer Buchung mit Kategorie sendet keine Übertrags-Payee" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let probe = BookingProbe(accounts, Error (YnabError.NetworkError "weg"))
            let entryId = newEntryId ()
            book probe entryId (request accounts.Bar) |> ignore
            let sent = ResizeArray()
            let create _ _ transferPayee _ _ _ (req: ManualTransactionRequest) =
                async {
                    sent.Add((transferPayee, req.CategoryId))
                    return Ok "ynab-retry-2"
                }
            let settings = Map [ "ynab_token", "tok"; "ynab_default_budget_id", "bud" ]
            let push = Server.Api.entryPushWith (fun k -> async.Return (Map.tryFind k settings)) create |> Async.RunSynchronously
            retryPushToYnab now push ignore entryId |> Async.RunSynchronously |> ignore
            Expect.equal (List.ofSeq sent) [ None, Some food.CategoryId ] "mit Kategorie"
    ]

// ============================================
// Rückgängig: discardCapturedBooking
// ============================================

[<Tests>]
let discardTests =
    let discardWith (deletes: ResizeArray<string>) (result: YnabResult<unit>) (entryId: EntryId) =
        discardCapturedBooking
            now
            loadEntryOrigin
            (fun ynabId -> async { deletes.Add ynabId; return result })
            ignore
            entryId
        |> Async.RunSynchronously
    testSequenced
    <| testList "Buchung erfassen: Rückgängig (ledger-at63w)" [
        testCase "Rückgängig löscht zuerst in YNAB und verwirft die Buchung danach im Kassenbuch" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            book (BookingProbe(accounts, Ok "ynab-discard-1")) entryId (request accounts.Bar) |> ignore
            let deletes = ResizeArray()
            Expect.equal (discardWith deletes (Ok ()) entryId) (Ok ()) "verworfen"
            Expect.equal (List.ofSeq deletes) [ "ynab-discard-1" ] "DELETE mit der YNAB-Id"
            let last = eventsOf entryId |> List.last
            Expect.equal last.Event.Body (EntryDiscardedInBb { YnabTransactionId = "ynab-discard-1" }) "Ereignis"
            Expect.equal last.Event.Actor UserInBb "Actor"
            Expect.isTrue (entryOf entryId).DeletedInYnab "gilt als gelöscht"

        testCase "scheitert das Löschen in YNAB, bleibt die Buchung ohne Ereignis stehen" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            book (BookingProbe(accounts, Ok "ynab-discard-2")) entryId (request accounts.Bar) |> ignore
            let before = eventsOf entryId |> List.length
            match discardWith (ResizeArray()) (Error (YnabError.NetworkError "weg")) entryId with
            | Error _ -> ()
            | Ok () -> failtest "Fehler erwartet"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"
            Expect.isFalse (entryOf entryId).DeletedInYnab "steht noch"

        testCase "eine nicht verknüpfte Buchung lässt sich nicht verwerfen, YNAB wird nicht gefragt" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            book (BookingProbe(accounts, Error (YnabError.NetworkError "weg"))) entryId (request accounts.Bar) |> ignore
            let deletes = ResizeArray()
            Expect.equal (discardWith deletes (Ok ()) entryId) (Error discardNotLinkedText) "abgelehnt"
            Expect.isEmpty deletes "kein DELETE"

        testCase "eine über Quick Add entstandene Buchung lässt sich so nicht verwerfen" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            let input : CashExpenseInput =
                { AccountId = accounts.Bar; Date = today; Amount = eur -3m; Payee = Some "Kiosk"; Memo = None; Category = None }
            addCashExpense QuickAdd now entryId (fun () -> async.Return (Ok "ynab-qa")) ignore input
            |> Async.RunSynchronously |> ignore
            let deletes = ResizeArray()
            Expect.equal (discardWith deletes (Ok ()) entryId) (Error discardNotCapturedText) "abgelehnt"
            Expect.isEmpty deletes "kein DELETE"

        testCase "eine schon verworfene Buchung wird nicht ein zweites Mal gelöscht" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let entryId = newEntryId ()
            book (BookingProbe(accounts, Ok "ynab-discard-3")) entryId (request accounts.Bar) |> ignore
            discardWith (ResizeArray()) (Ok ()) entryId |> ignore
            let deletes = ResizeArray()
            Expect.equal (discardWith deletes (Ok ()) entryId) (Error discardDeletedText) "abgelehnt"
            Expect.isEmpty deletes "kein zweites DELETE"
    ]

// ============================================
// Vorbelegung
// ============================================

let private entryWith (account: Guid) (payee: string) (amount: decimal) (category: CategoryRef option) : Entry =
    { Id = newEntryId ()
      AccountId = LedgerAccountId account
      Date = today
      Amount = eur amount
      Payee = Some payee
      Memo = None
      Category = category
      Splits = []
      Cleared = Uncleared
      TransferEntryId = None
      TransferAccountId = None
      SplitTransferCounterparts = Map.empty
      DeletedInYnab = false
      Approved = false
      FlagColor = None
      FlagName = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None
      PayeeBeforeTransfer = None }

[<Tests>]
let defaultsTests =
    let bar = Guid.NewGuid()
    let tag = Guid.NewGuid()
    let imp = Guid.NewGuid()
    testList "Buchung erfassen: Vorbelegung (ledger-at63w)" [
        testCase "das Konto kommt aus dem Kassenbuch-Filter" <| fun () ->
            Expect.equal (defaultBookingAccount (Some tag) (Some bar) [ bar; tag ]) (Some tag) "Filter"

        testCase "ohne wählbares Filter-Konto gilt das zuletzt genutzte" <| fun () ->
            Expect.equal (defaultBookingAccount None (Some bar) [ bar; tag ]) (Some bar) "ohne Filter"
            Expect.equal (defaultBookingAccount (Some imp) (Some bar) [ bar; tag ]) (Some bar) "Filter auf dem Import-Konto"

        testCase "das Import-Konto wird nie vorbelegt" <| fun () ->
            Expect.equal (defaultBookingAccount (Some imp) (Some imp) [ bar; tag ]) None "nie das Import-Konto"

        testCase "wählbar sind offene Budget-Konten mit YNAB ohne das Import-Konto" <| fun () ->
            let y () = YnabAccountId (Guid.NewGuid())
            let barY, impY, trackY, closedY = y (), y (), y (), y ()
            let view id name ynab closed : LedgerAccountView =
                { Id = id; Name = name; Kind = LedgerBankAccount; YnabAccountId = ynab; Closed = closed }
            let closedId, trackId, ownId = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
            let accounts =
                [ view bar "Bar" (Some barY) false
                  view imp "Giro" (Some impY) false
                  view trackId "Depot" (Some trackY) false
                  view closedId "Alt" (Some closedY) true
                  view ownId "Eigen" None false ]
            let budget : YnabAccount list =
                [ { Id = barY; Name = "Bar"; Balance = eur 0m; OnBudget = true; Closed = false }
                  { Id = impY; Name = "Giro"; Balance = eur 0m; OnBudget = true; Closed = false }
                  { Id = trackY; Name = "Depot"; Balance = eur 0m; OnBudget = false; Closed = false }
                  { Id = closedY; Name = "Alt"; Balance = eur 0m; OnBudget = true; Closed = false } ]
            Expect.equal (bookingAccounts accounts budget (Some imp) |> List.map (fun a -> a.Id)) [ bar ] "nur Bar"

        testCase "die Zuletzt-Chips: drei, neueste zuerst, ohne Doppelte" <| fun () ->
            let newestFirst =
                [ entryWith bar "Bäckerei Lange" -8.60m (Some food)
                  entryWith bar "bäckerei lange " -8.60m (Some food)
                  entryWith tag "Wochenmarkt" -23m None
                  { entryWith bar "Tankstelle" -60m None with DeletedInYnab = true }
                  entryWith bar "Tankstelle" -60m None
                  entryWith bar "Kiosk" -2m None ]
            let defaults = Ledger.QueryHandlers.bookingDefaultsOf today (Some imp) newestFirst
            Expect.equal
                (defaults.Recent |> List.map (fun t -> t.Payee, t.Amount, t.IsOutflow))
                [ "Bäckerei Lange", 8.60m, true; "Wochenmarkt", 23m, true; "Tankstelle", 60m, true ]
                "drei Kombinationen"
            Expect.equal defaults.Recent.Head.Target (Some (BookOnCategory food.CategoryId)) "Kategorie im Chip"
            Expect.equal defaults.LastUsedAccountId (Some bar) "zuletzt genutztes Konto"
            Expect.equal defaults.ImportAccountId (Some imp) "Import-Konto"

        testCase "ein Übertrag im Chip trägt das Zielkonto und den Payee davor" <| fun () ->
            let target = YnabAccountId (Guid.NewGuid())
            let transfer =
                { entryWith bar "Transfer : Tagesgeld" -100m None with
                    TransferAccountId = Some target
                    PayeeBeforeTransfer = Some "Sparen" }
            let defaults = Ledger.QueryHandlers.bookingDefaultsOf today None [ transfer ]
            Expect.equal
                (defaults.Recent |> List.map (fun t -> t.Payee, t.Target))
                [ "Sparen", Some (BookAsTransfer target) ]
                "Zielkonto"

        testCase "heute ist der Berliner Kalendertag, nicht der UTC-Tag" <| fun () ->
            initializeLedgerSchema ()
            let zone = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
            let lateUtc = DateTime(2026, 11, 7, 23, 30, 0, DateTimeKind.Utc)
            let defaults =
                Ledger.QueryHandlers.getLedgerBookingDefaultsWith (fun _ -> async.Return None) zone lateUtc
                |> Async.RunSynchronously
            Expect.equal defaults.Today (DateTime(2026, 11, 8)) "schon der 8. in Berlin"
            Expect.isNone defaults.ImportAccountId "ohne Einstellung kein Import-Konto"
    ]

[<Tests>]
let payeeCategoryTests =
    let rule (pattern: string) (category: YnabCategoryId) : Rule =
        { Id = RuleId (Guid.NewGuid())
          Name = "Regel"
          Pattern = pattern
          PatternType = Contains
          TargetField = TargetField.Payee
          CategoryId = category
          CategoryName = "Regel-Kategorie"
          PayeeOverride = None
          Priority = 1
          Enabled = true
          CreatedAt = now
          UpdatedAt = now }
    let ruleCategory = YnabCategoryId (Guid.NewGuid())
    let categoryFor last rules payee =
        Ledger.QueryHandlers.getBookingCategoryForPayeeWith (fun _ -> async.Return last) (fun () -> async.Return rules) payee
        |> Async.RunSynchronously
    let (YnabCategoryId foodId) = food.CategoryId
    testList "Buchung erfassen: Kategorie zum Payee (ledger-at63w)" [
        testCase "die letzte Kategorie dieses Payees geht vor der Regel" <| fun () ->
            Expect.equal (categoryFor (Some foodId) [ rule "Bäcker" ruleCategory ] "Bäckerei Lange") (Some food.CategoryId) "letzte Kategorie"

        testCase "ohne Kategorie in der Historie gilt die passende Regel" <| fun () ->
            Expect.equal (categoryFor None [ rule "Bäcker" ruleCategory ] "Bäckerei Lange") (Some ruleCategory) "Regel"

        testCase "ohne Historie und ohne passende Regel bleibt die Kategorie leer" <| fun () ->
            Expect.isNone (categoryFor None [ rule "Tankstelle" ruleCategory ] "Bäckerei Lange") "leer"

        testCase "eine abgeschaltete Regel zählt nicht" <| fun () ->
            Expect.isNone (categoryFor None [ { rule "Bäcker" ruleCategory with Enabled = false } ] "Bäckerei Lange") "leer"

        testCase "die letzte Kategorie ist die der jüngsten Buchung dieses Payees" <| fun () ->
            setup ()
            let accounts = freshAccounts ()
            let payee = $"Payee-at63w-{Guid.NewGuid():N}"
            let other : CategoryRef = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Alt" }
            let add (date: DateTime) (category: CategoryRef) =
                let input : CashExpenseInput =
                    { AccountId = accounts.Bar; Date = date; Amount = eur -1m; Payee = Some payee; Memo = None; Category = Some category }
                addCashExpense UserInBb now (newEntryId ()) (fun () -> async.Return (Ok $"y-{Guid.NewGuid()}")) ignore input
                |> Async.RunSynchronously |> ignore
            add (today.AddDays -1.0) food
            add (today.AddDays -10.0) other
            Expect.equal (loadLastPayeeCategory (payee.ToUpperInvariant()) |> Async.RunSynchronously) (Some foodId) "jüngste"
    ]
