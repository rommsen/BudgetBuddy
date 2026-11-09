module LedgerAmountTests

// Betrag ändern im Detail (ledger-m7mye, ADR 0092):
//
//  * Prüfung an der API-Grenze (`validateAmountChange`), alle Fehler gesammelt;
//  * Ereignisse: Codec, Namen, Schema-Version;
//  * Befehle gegen In-Memory-SQLite (`testSequenced`, Schema-Init je Testfall) mit einem
//    injizierten Push und Split-Werkzeugen, die nie YNAB erreichen und ihre Aufrufe zählen;
//  * `observe` mit Baseline (der Spiegel dreht die BB-Entscheidung nicht zurück);
//  * Historie und Lesepfad (Sperrgrund);
//  * PUT an YNAB gegen einen Fake-`HttpMessageHandler` (Betrag als JSON-Zahl in Milliunits).

open System
open System.Net
open System.Net.Http

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Ledger.QueryHandlers
open Server.YnabObservations

module Ynab = Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private at = DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc)
let private bookingDay = DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc)
let private monthStart = DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)

let private catRef (id: string) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.Parse id); CategoryName = name }

let private catA = catRef "0195c000-0000-7000-8000-0000000a0a01" "Lebensmittel"
let private catB = catRef "0195c000-0000-7000-8000-0000000a0a02" "Drogerie"
let private cashAccount = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a0ad2")

let private line (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

let private transferLine (amount: decimal) : TransactionSplit =
    { Target = ToTransfer (cashAccount, "Bargeld"); Amount = eur amount; Memo = None }

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

/// Ein frisches Kassenbuch-Konto, gespiegelt auf ein frisches YNAB-Konto.
let private freshAccount () : LedgerAccountId =
    let id = LedgerAccountId (Guid.NewGuid())
    saveAccount {
        Id = id
        Name = "Girokonto m7mye"
        Kind = BankAccount
        Link = YnabMirrored (YnabAccountId (Guid.NewGuid()))
        Closed = false
    }
    |> Async.RunSynchronously
    id

let private observedPayload (account: LedgerAccountId) : EntryObservedInYnabPayload = {
    AccountId = account
    YnabTransactionId = $"ynab-m7mye-{Guid.NewGuid()}"
    ImportId = Some ("BB:" + Guid.NewGuid().ToString("N").Substring(0, 20))
    Date = bookingDay
    Amount = eur -12.00m
    Payee = Some "Rewe m7mye"
    Memo = None
    Category = Some catA
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private seedWith (payload: EntryObservedInYnabPayload) : EntryId * string =
    let entryId = newEntryId ()
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId, payload.YnabTransactionId

/// Eine gewöhnliche, mit YNAB verknüpfte Buchung über −12,00 € auf Lebensmittel.
let private seedPlain (account: LedgerAccountId) = seedWith (observedPayload account)

let private seedSplit (account: LedgerAccountId) (splits: TransactionSplit list) =
    seedWith { observedPayload account with Category = None; Splits = splits }

/// Ein Comdirect-Import, nach dem Push mit YNAB verknüpft.
let private seedImported (account: LedgerAccountId) : EntryId * string * string * string =
    let entryId = newEntryId ()
    let importId = "BB:ref-m7mye-" + Guid.NewGuid().ToString("N").Substring(0, 8)
    let reference = "ref-m7mye-" + Guid.NewGuid().ToString("N").Substring(0, 8)
    let ynabId = $"ynab-m7mye-{Guid.NewGuid()}"
    appendAndApply [
        eventOn entryId ComdirectImport (
            EntryImportedFromComdirect {
                AccountId = account
                ImportId = importId
                ComdirectReference = reference
                Date = bookingDay
                Amount = eur -30.00m
                Payee = Some "Bäckerei m7mye"
                Memo = None
                Cleared = Cleared
                Approved = false
                FlagColor = None
                FlagName = None
            }
        )
        eventOn entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = ynabId })
    ]
    |> Async.RunSynchronously
    entryId, ynabId, importId, reference

/// Eine in BB erfasste Barbuchung, deren Push noch aussteht (Noch nicht in YNAB).
let private seedUnlinked (account: LedgerAccountId) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        eventOn entryId QuickAdd (
            CashExpenseAdded {
                AccountId = account
                Date = bookingDay
                Amount = eur -3.00m
                Payee = Some "Kiosk m7mye"
                Memo = None
                Category = None
                Cleared = Uncleared
                Approved = false
                ImportId = None
            }
        )
    ]
    |> Async.RunSynchronously
    entryId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

let private lastEvent (entryId: EntryId) : LedgerEvent =
    (loadEventsForEntry entryId |> Async.RunSynchronously |> List.last).Event

/// Ein Push, der jeden Aufruf aufzeichnet und ein festes Ergebnis liefert.
type private AmountProbe(result: YnabResult<unit>) =
    member val Calls : (string * Money) list = [] with get, set
    member this.Push : AmountPush =
        fun ynabId amount ->
            async {
                this.Calls <- this.Calls @ [ ynabId, amount ]
                return result
            }

/// Die Split-Werkzeuge, aufzeichnend und mit festen Ergebnissen.
type private SplitProbe() =
    member val Deletes : string list = [] with get, set
    member val Creates : Ynab.SplitReplacement list = [] with get, set
    member val Puts = 0 with get, set
    member val CreateResult : YnabResult<string> option = None with get, set
    member this.WriteCalls = this.Deletes.Length + this.Creates.Length + this.Puts
    member this.Tools : SplitWriteTools = {
        IsTrackingAccount = fun _ -> async { return Ok false }
        PutCategory =
            fun _ _ ->
                async {
                    this.Puts <- this.Puts + 1
                    return Ok ()
                }
        PutSplits =
            fun _ _ ->
                async {
                    this.Puts <- this.Puts + 1
                    return Ok ()
                }
        Delete =
            fun id ->
                async {
                    this.Deletes <- this.Deletes @ [ id ]
                    return Ok ()
                }
        Create =
            fun request ->
                async {
                    this.Creates <- this.Creates @ [ request ]
                    return this.CreateResult |> Option.defaultWith (fun () -> Ok $"ynab-m7mye-{Guid.NewGuid()}")
                }
    }

let private okProbe () = AmountProbe(Ok ())

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private setAmount (push: AmountPush) (entryId: EntryId) (amount: decimal) =
    setEntryAmount at push entryId amount |> Async.RunSynchronously

let private seedCategories () =
    upsertCategories [
        for c in [ catA; catB ] ->
            { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
    ]
    |> Async.RunSynchronously

// ============================================
// Prüfung an der API-Grenze
// ============================================

let private request (amount: decimal option) (direction: string) : AmountChangeRequest =
    { Amount = amount; Direction = direction }

[<Tests>]
let validationTests =
    testList "Betrag: Prüfung an der API-Grenze (ledger-m7mye)" [

        testCase "Ausgabe wird negativ, Einnahme positiv" <| fun () ->
            Expect.equal (Server.Validation.validateAmountChange (request (Some 14.5m) AmountDirectionOutflow)) (Ok -14.5m) "Ausgabe"
            Expect.equal (Server.Validation.validateAmountChange (request (Some 14.5m) AmountDirectionInflow)) (Ok 14.5m) "Einnahme"

        testCase "leer, 0, negativ, drei Nachkommastellen und unbekannte Richtung werden abgelehnt" <| fun () ->
            let rejected (r: AmountChangeRequest) (expected: string) =
                match Server.Validation.validateAmountChange r with
                | Error errors -> Expect.contains errors expected $"{r}"
                | Ok v -> failtest $"{r} muss abgelehnt werden, war {v}"
            rejected (request None AmountDirectionOutflow) Server.Validation.AmountMissingText
            rejected (request (Some 0m) AmountDirectionOutflow) Server.Validation.AmountNotPositiveText
            rejected (request (Some -5m) AmountDirectionInflow) Server.Validation.AmountNotPositiveText
            rejected (request (Some 1.234m) AmountDirectionOutflow) Server.Validation.AmountTooManyDecimalsText
            rejected (request (Some 5m) "sideways") Server.Validation.AmountDirectionUnknownText
            rejected (request (Some 2_000_000m) AmountDirectionOutflow) Server.Validation.AmountTooLargeText

        testCase "alle Fehler gesammelt: drei Nachkommastellen und unbekannte Richtung zugleich" <| fun () ->
            match Server.Validation.validateAmountChange (request (Some 1.234m) "") with
            | Error errors ->
                Expect.equal
                    errors
                    [ Server.Validation.AmountTooManyDecimalsText; Server.Validation.AmountDirectionUnknownText ]
                    "beide Gründe"
            | Ok _ -> failtest "muss abgelehnt werden"

        testCase "leer und unbekannte Richtung zugleich: beide Gründe" <| fun () ->
            match Server.Validation.validateAmountChange (request None "x") with
            | Error errors -> Expect.equal errors.Length 2 "zwei Gründe"
            | Ok _ -> failtest "muss abgelehnt werden"
    ]

// ============================================
// Ereignisse
// ============================================

let private changedBody = EntryAmountChangedInBb { OldAmount = eur -12.00m; NewAmount = eur -14.50m }
let private failedBody = EntryAmountWriteBackFailed { Reason = "Verbindung weg"; BaselineAmount = eur -12.00m }

[<Tests>]
let eventTests =
    testList "Betrag: Ereignisse" [

        testCase "Rundreise des Codecs, Namen und Schema-Version 1 für alle drei Typen" <| fun () ->
            for body, name in
                [ changedBody, "EntryAmountChangedInBb"
                  failedBody, "EntryAmountWriteBackFailed"
                  EntryAmountWrittenBackToYnab, "EntryAmountWrittenBackToYnab" ] do
                Expect.equal (eventTypeName body) name "Typname"
                Expect.equal (schemaVersion body) 1 $"Version von {name}"
                Expect.equal (decodePayload name 1 (encodePayload body)) (Ok body) $"Rundreise {name}"

        testCase "apply: die Änderung setzt den Betrag, der Fehlschlag behält die erste Baseline" <| fun () ->
            let entryId = newEntryId ()
            let entry =
                [ eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (LedgerAccountId (Guid.NewGuid()))))
                  eventOn entryId UserInBb changedBody
                  eventOn entryId UserInBb failedBody
                  eventOn entryId UserInBb (EntryAmountWriteBackFailed { Reason = "zwei"; BaselineAmount = eur -14.50m }) ]
                |> replay
                |> Option.get
            Expect.equal entry.Amount (eur -14.50m) "neuer Betrag"
            Expect.equal entry.AmountWriteBackFailure (Some { Reason = "zwei"; BaselineAmount = eur -12.00m }) "erste Baseline"
    ]

// ============================================
// Befehle: gewöhnliche Buchung
// ============================================

[<Tests>]
let commandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Betrag: Befehl setEntryAmount (ADR 0092)" [

        testCase "gewöhnliche Buchung: ein Ereignis mit Alt und Neu, ein Push mit dem neuen Betrag" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedPlain account
            let probe = okProbe ()
            let outcome = setAmount probe.Push entryId -14.50m |> unwrap

            Expect.equal outcome.AmountChange AmountChanged "geändert"
            Expect.equal outcome.AmountWriteBack AmountSent "gesendet"
            Expect.equal outcome.PreviousAmount (eur -12.00m) "Vorzustand für Rückgängig"
            Expect.equal outcome.CurrentAmount (eur -14.50m) "neuer Betrag"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab"; "EntryAmountChangedInBb" ] "ein Ereignis"
            match lastEvent entryId with
            | { Actor = UserInBb; Body = EntryAmountChangedInBb p } ->
                Expect.equal (p.OldAmount, p.NewAmount) (eur -12.00m, eur -14.50m) "Alt und Neu"
            | other -> failtest $"erwartet EntryAmountChangedInBb von UserInBb, war {other}"
            Expect.equal (entryOf entryId).Amount (eur -14.50m) "Zustand"
            Expect.equal probe.Calls [ ynabId, eur -14.50m ] "genau ein Push"

        testCase "Kontostand und Aktivität der Kategorie folgen dem neuen Betrag" <| fun () ->
            let account = freshAccount ()
            // Eine eigene Kategorie: die In-Memory-DB ist mit den anderen Tests geteilt.
            let category = { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Eigene m7mye" }
            let entryId, _ = seedWith { observedPayload account with Category = Some category }
            let (LedgerAccountId accountGuid) = account
            let balance () =
                getLedgerAccountBalances ()
                |> Async.RunSynchronously
                |> List.find (fun b -> b.AccountId = accountGuid)
                |> fun b -> b.Balance
            let activity () =
                getCategoryMonthlyActivity category.CategoryId [ monthStart ] |> Async.RunSynchronously
            Expect.equal (balance ()) (eur -12.00m) "Kontostand vorher"
            Expect.equal (activity ()) [ monthStart, eur -12.00m ] "Aktivität vorher"
            setAmount (okProbe ()).Push entryId -14.50m |> unwrap |> ignore
            Expect.equal (balance ()) (eur -14.50m) "Kontostand nachher"
            Expect.equal (activity ()) [ monthStart, eur -14.50m ] "Aktivität nachher"

        testCase "Comdirect-Import: gelingt, import_id und Bank-Referenz bleiben unverändert" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId, importId, reference = seedImported account
            let linksBefore = linksForEntry entryId |> Async.RunSynchronously
            let probe = okProbe ()
            let outcome = setAmount probe.Push entryId -31.20m |> unwrap
            Expect.equal outcome.AmountWriteBack AmountSent "gesendet"
            Expect.equal probe.Calls [ ynabId, eur -31.20m ] "Update an die verknüpfte YNAB-Buchung"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (Set.ofList links) (Set.ofList linksBefore) "Verknüpfungen unverändert"
            Expect.equal (currentImportId links) (Some importId) "import_id bleibt"
            Expect.isTrue (links |> List.exists (fun l -> l.System = ComdirectSystem && l.Value = reference)) "Bank-Referenz bleibt"

        testCase "Rückgängig ist derselbe Befehl mit dem alten Betrag" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedPlain account
            let probe = okProbe ()
            let outcome = setAmount probe.Push entryId 5.00m |> unwrap
            let undone = setAmount probe.Push entryId outcome.PreviousAmount.Amount |> unwrap
            Expect.equal undone.AmountChange AmountChanged "zurückgesetzt"
            Expect.equal (entryOf entryId).Amount (eur -12.00m) "alter Betrag"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryAmountChangedInBb"; "EntryAmountChangedInBb" ]
                "zweites EntryAmountChangedInBb"
            Expect.equal probe.Calls [ ynabId, eur 5.00m; ynabId, eur -12.00m ] "zwei Pushes"

        testCase "unveränderter Betrag: kein Ereignis, kein Push" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            let probe = okProbe ()
            let outcome = setAmount probe.Push entryId -12.00m |> unwrap
            Expect.equal (outcome.AmountChange, outcome.AmountWriteBack) (AmountUnchanged, AmountNotSent) "unverändert"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "Push scheitert: der Betrag bleibt in BB, der Fehlschlag mit Grund und Baseline ist offen" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            let probe = AmountProbe(Error (YnabError.NetworkError "Verbindung weg"))
            let outcome = setAmount probe.Push entryId -14.50m |> unwrap
            let reason =
                match outcome.AmountWriteBack with
                | AmountSendFailed reason -> reason
                | other -> failtest $"erwartet AmountSendFailed, war {other}"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryAmountChangedInBb"; "EntryAmountWriteBackFailed" ]
                "Änderung zuerst, dann der Fehlschlag"
            let entry = entryOf entryId
            Expect.equal entry.Amount (eur -14.50m) "BB behält den Betrag"
            Expect.equal entry.AmountWriteBackFailure (Some { Reason = reason; BaselineAmount = eur -12.00m }) "Fehlschlag"
            let view = getLedgerEntry (let (EntryId g) = entryId in g) |> Async.RunSynchronously |> Option.get
            Expect.equal view.AmountWriteBackFailure (Some reason) "der Lesepfad zeigt den Grund"

        testCase "eine weitere Änderung bei offenem Fehlschlag behält die erste Baseline" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            setAmount (AmountProbe(Error (YnabError.NetworkError "eins"))).Push entryId -14.50m |> unwrap |> ignore
            setAmount (AmountProbe(Error (YnabError.NetworkError "zwei"))).Push entryId -20.00m |> unwrap |> ignore
            let failure = (entryOf entryId).AmountWriteBackFailure |> Option.get
            Expect.equal failure.BaselineAmount (eur -12.00m) "erste Baseline bleibt"

        testCase "eine neue Änderung mit Erfolg beendet den offenen Fehlschlag" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            setAmount (AmountProbe(Error (YnabError.NetworkError "weg"))).Push entryId -14.50m |> unwrap |> ignore
            setAmount (okProbe ()).Push entryId -15.00m |> unwrap |> ignore
            Expect.isNone (entryOf entryId).AmountWriteBackFailure "beendet"
            Expect.contains (eventTypesOf entryId) "EntryAmountWrittenBackToYnab" "Erfolg nach Fehlschlag festgehalten"

        testCase "Erneut senden schickt den aktuellen Betrag nach und beendet den Fehlschlag" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedPlain account
            setAmount (AmountProbe(Error (YnabError.NetworkError "weg"))).Push entryId -14.50m |> unwrap |> ignore
            let probe = okProbe ()
            let result = retryAmountWriteBack at probe.Push (SplitProbe()).Tools entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result AmountSent "gesendet"
            Expect.equal probe.Calls [ ynabId, eur -14.50m ] "der aktuelle Betrag"
            match lastEvent entryId with
            | { Actor = UserInBb; Body = EntryAmountWrittenBackToYnab } -> ()
            | other -> failtest $"erwartet EntryAmountWrittenBackToYnab, war {other}"
            Expect.isNone (entryOf entryId).AmountWriteBackFailure "beendet"

        testCase "Erneut senden ohne offenen Fehlschlag tut nichts" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            let probe = okProbe ()
            let result = retryAmountWriteBack at probe.Push (SplitProbe()).Tools entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result AmountNotSent "nichts zu senden"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "abgelehnt ohne Ereignis und ohne Aufruf: abgeglichen, gelöscht, Noch nicht in YNAB, Übertrag, Split mit Übertrags-Zeile" <| fun () ->
            let account = freshAccount ()
            let reconciled, _ = seedWith { observedPayload account with Cleared = Reconciled }
            let deleted, _ = seedPlain account
            appendAndApply [ eventOn deleted YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            let unlinked = seedUnlinked account
            let transfer, _ =
                seedWith { observedPayload account with Category = None; TransferAccountId = Some cashAccount }
            let splitTransfer, _ = seedSplit account [ line catA -5.00m; transferLine -7.00m ]
            for entryId, expected in
                [ reconciled, amountReconciledReason
                  deleted, amountDeletedReason
                  unlinked, amountNotInYnabReason
                  transfer, amountTransferReason
                  splitTransfer, amountSplitTransferReason ] do
                let before = eventTypesOf entryId
                let probe = okProbe ()
                Expect.equal (setAmount probe.Push entryId -20.00m) (Error expected) expected
                Expect.equal (eventTypesOf entryId) before $"{expected}: kein Ereignis"
                Expect.isEmpty probe.Calls $"{expected}: kein Push"
                let (EntryId guid) = entryId
                let view = getLedgerEntry guid |> Async.RunSynchronously |> Option.get
                Expect.equal view.AmountLockReason (Some expected) $"{expected}: der Lesepfad nennt den Grund"

        testCase "eine aufgeteilte Buchung geht nicht über setEntryAmount" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedSplit account [ line catA -5.00m; line catB -7.00m ]
            let probe = okProbe ()
            Expect.equal (setAmount probe.Push entryId -20.00m) (Error amountSplitRouteReason) "abgelehnt"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.isEmpty probe.Calls "kein Push"

        testCase "unbekannte Buchung wird abgelehnt" <| fun () ->
            Expect.equal (setAmount (okProbe ()).Push (newEntryId ()) -1m) (Error "Buchung nicht gefunden") "unbekannt"

        testCase "der Lesepfad: eine gewöhnliche Buchung ist änderbar" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            let (EntryId guid) = entryId
            let view = getLedgerEntry guid |> Async.RunSynchronously |> Option.get
            Expect.isNone view.AmountLockReason "kein Sperrgrund"
            Expect.isNone view.AmountWriteBackFailure "nichts offen"
    ]

[<Tests>]
let writeGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Betrag: Schreib-Sperre" [

        testCase "Betrag ändern während eines Spiegel-Laufs schreibt und pusht erst nach dem Lauf" <| fun () ->
            let entryId, _ = seedPlain (freshAccount ())
            let probe = okProbe ()
            let before = eventTypesOf entryId
            LedgerWriteGuardProbe.commandWaitsForMirror
                (fun () ->
                    Expect.equal (eventTypesOf entryId) before "noch kein Ereignis während des Laufs"
                    Expect.isEmpty probe.Calls "noch kein Push")
                (setEntryAmount at probe.Push entryId -14.50m)
            |> unwrap
            |> ignore
            Expect.equal probe.Calls.Length 1 "ein Push nach dem Lauf"
    ]

// ============================================
// Befehle: aufgeteilte Buchung (Ersatz)
// ============================================

[<Tests>]
let splitCommandTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            seedCategories ()
            body ())

    let setWithAmount (probe: SplitProbe) (entryId: EntryId) (lines: TransactionSplit list) (amount: decimal) =
        setEntrySplitsWithAmount at probe.Tools entryId lines amount |> Async.RunSynchronously

    testSequenced
    <| testList "Betrag: aufgeteilte Buchung als ein Ersatz (ADR 0092 Punkt 3)" [

        testCase "neuer Betrag mit gültiger Aufteilung: DELETE, POST mit neuem Betrag und neuen Zeilen, Ersatz-import_id" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedSplit account [ line catA -5.00m; line catB -7.00m ]
            let oldImportId = currentImportId (linksForEntry entryId |> Async.RunSynchronously)
            let probe = SplitProbe()
            let newLines = [ line catA -5.00m; line catB -9.50m ]
            let outcome = setWithAmount probe entryId newLines -14.50m |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal probe.Deletes [ ynabId ] "ein DELETE der alten Buchung"
            let created = Expect.wantSome (List.tryExactlyOne probe.Creates) "ein POST"
            Expect.equal created.Amount (eur -14.50m) "POST mit neuem Betrag"
            Expect.equal created.Splits newLines "POST mit neuen Zeilen"
            Expect.equal created.ImportId (replacementImportId oldImportId) "Ersatz-import_id"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryAmountChangedInBb"; "EntryRecategorized"; "EntryReplacedInYnab" ]
                "Betrag, Aufteilung, Ersatz"
            let entry = entryOf entryId
            Expect.equal entry.Amount (eur -14.50m) "Betrag in BB"
            Expect.equal entry.Splits newLines "Zeilen in BB"

        testCase "eine Aufteilung, deren Summe nicht zum neuen Betrag passt, wird abgelehnt" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedSplit account [ line catA -5.00m; line catB -7.00m ]
            let probe = SplitProbe()
            match setWithAmount probe entryId [ line catA -5.00m; line catB -7.00m ] -14.50m with
            | Error message -> Expect.stringContains message "weicht" "Summe weicht ab"
            | Ok _ -> failtest "muss abgelehnt werden"
            Expect.equal (eventTypesOf entryId) [ "EntryObservedInYnab" ] "kein Ereignis"
            Expect.equal probe.WriteCalls 0 "kein Aufruf"

        testCase "Split mit Übertrags-Zeile bleibt gesperrt" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedSplit account [ line catA -5.00m; transferLine -7.00m ]
            let probe = SplitProbe()
            let result = setWithAmount probe entryId [ line catA -5.00m; transferLine -9.50m ] -14.50m
            Expect.equal result (Error amountSplitTransferReason) "gesperrt"
            Expect.equal probe.WriteCalls 0 "kein Aufruf"

        testCase "POST scheitert: Betrag und Zeilen bleiben in BB, der Betrags-Fehlschlag ist offen; Erneut senden legt nur an" <| fun () ->
            let account = freshAccount ()
            let entryId, ynabId = seedSplit account [ line catA -5.00m; line catB -7.00m ]
            let failing = SplitProbe(CreateResult = Some (Error (YnabError.NetworkError "weg")))
            let outcome = setWithAmount failing entryId [ line catA -5.00m; line catB -9.50m ] -14.50m |> unwrap
            match outcome.WriteBack with
            | CategorySendFailed _ -> ()
            | other -> failtest $"erwartet CategorySendFailed, war {other}"
            let entry = entryOf entryId
            Expect.equal entry.Amount (eur -14.50m) "BB behält den Betrag"
            Expect.equal (entry.AmountWriteBackFailure |> Option.map (fun f -> f.BaselineAmount)) (Some (eur -12.00m)) "Baseline"

            let probe = SplitProbe()
            let result = retryAmountWriteBack at (okProbe ()).Push probe.Tools entryId |> Async.RunSynchronously |> unwrap
            Expect.equal result AmountSent "gesendet"
            Expect.isEmpty probe.Deletes "kein zweites DELETE"
            Expect.equal (probe.Creates |> List.map (fun c -> c.Amount)) [ eur -14.50m ] "nur das POST mit dem neuen Betrag"
            let after = entryOf entryId
            Expect.isNone after.AmountWriteBackFailure "Betrags-Fehlschlag beendet"
            Expect.isNone after.CategorizationWriteBackFailure "Kategorie-Fehlschlag beendet"
            Expect.isFalse (failing.Deletes |> List.isEmpty) $"das erste DELETE traf {ynabId}"

        testCase "eine unaufgeteilte Buchung geht nicht über setEntrySplitsWithAmount" <| fun () ->
            let account = freshAccount ()
            let entryId, _ = seedPlain account
            let probe = SplitProbe()
            match setWithAmount probe entryId [ line catA -5.00m; line catB -9.50m ] -14.50m with
            | Error _ -> ()
            | Ok _ -> failtest "muss abgelehnt werden"
            Expect.equal probe.WriteCalls 0 "kein Aufruf"
    ]

// ============================================
// Spiegel: observe mit Baseline
// ============================================

let private observationOf (entry: Entry) (amount: Money) : YnabObservation = {
    YnabTransactionId = "ynab-x"
    AccountId = YnabAccountId (Guid.NewGuid())
    ImportId = None
    Date = entry.Date
    Amount = amount
    Payee = entry.Payee
    Memo = entry.Memo
    Category = entry.Category
    Splits = entry.Splits
    SplitTransfers = []
    Cleared = entry.Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = entry.Approved
    FlagColor = entry.FlagColor
    FlagName = entry.FlagName
}

let private contextOf (entry: Entry) : ObservationContext =
    { EntryId = entry.Id; AccountId = entry.AccountId; ObservedAt = at }

let private entryAfter (events: LedgerEventBody list) : Entry =
    let entryId = newEntryId ()
    (eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload (LedgerAccountId (Guid.NewGuid())))))
    :: (events |> List.map (eventOn entryId UserInBb))
    |> replay
    |> Option.get

let private replayWith (entry: Entry) (events: LedgerEvent list) : Entry =
    events |> List.fold (fun state ev -> Some (apply state ev)) (Some entry) |> Option.get

[<Tests>]
let observeTests =
    testList "Betrag: Spiegel mit Baseline (ADR 0092, wie ADR 0055)" [

        testCase "nach dem Push zeigt YNAB den neuen Betrag: kein Ereignis, die BB-Entscheidung bleibt" <| fun () ->
            let entry = entryAfter [ changedBody ]
            let events = observe (contextOf entry) (Some entry) (observationOf entry (eur -14.50m))
            Expect.isEmpty events "nichts zu erzählen"

        testCase "offener Fehlschlag, YNAB zeigt noch die Baseline: kein Ereignis, der Betrag bleibt" <| fun () ->
            let entry = entryAfter [ changedBody; failedBody ]
            let events = observe (contextOf entry) (Some entry) (observationOf entry (eur -12.00m))
            Expect.isEmpty events "YNAB hat den Push nie gesehen"
            Expect.equal (replayWith entry events).Amount (eur -14.50m) "BB-Betrag bleibt"

        testCase "offener Fehlschlag, andere Kopfdaten ändern sich: der BB-Betrag wird nicht überschrieben" <| fun () ->
            let entry = entryAfter [ changedBody; failedBody ]
            let obs = { observationOf entry (eur -12.00m) with Payee = Some "Anderer Payee" }
            let after = replayWith entry (observe (contextOf entry) (Some entry) obs)
            Expect.equal after.Payee (Some "Anderer Payee") "Payee gespiegelt"
            Expect.equal after.Amount (eur -14.50m) "Betrag bleibt der BB-Stand"
            Expect.isSome after.AmountWriteBackFailure "Fehlschlag bleibt"

        testCase "offener Fehlschlag, YNAB zeigt den BB-Stand: der Fehlschlag endet, Actor YnabMirror" <| fun () ->
            let entry = entryAfter [ changedBody; failedBody ]
            let events = observe (contextOf entry) (Some entry) (observationOf entry (eur -14.50m))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body, e.Actor)) [ "EntryAmountWrittenBackToYnab", YnabMirror ] "nur das Ende"
            Expect.isNone (replayWith entry events).AmountWriteBackFailure "beendet"

        testCase "ein später in YNAB geänderter Betrag wird übernommen, auch mit offenem Fehlschlag" <| fun () ->
            let entry = entryAfter [ changedBody ]
            let after = replayWith entry (observe (contextOf entry) (Some entry) (observationOf entry (eur -20.00m)))
            Expect.equal after.Amount (eur -20.00m) "YNAB gewinnt"

            let failing = entryAfter [ changedBody; failedBody ]
            let events = observe (contextOf failing) (Some failing) (observationOf failing (eur -20.00m))
            Expect.equal (events |> List.map (fun e -> eventTypeName e.Body)) [ "EntryDetailsChangeObservedInYnab" ] "Beobachtung"
            let afterFailing = replayWith failing events
            Expect.equal afterFailing.Amount (eur -20.00m) "YNABs Stand"
            Expect.isNone afterFailing.AmountWriteBackFailure "Fehlschlag beendet"
    ]

// ============================================
// Historie
// ============================================

[<Tests>]
let historyTests =
    testList "Betrag: Historie" [

        testCase "Betrag geändert, mit neuem und altem Betrag" <| fun () ->
            Expect.equal (ReadModel.describe changedBody) "Betrag geändert auf -14,50 EUR, vorher -12,00 EUR" "Satz"

        testCase "Fehlschlag und Ankunft" <| fun () ->
            Expect.equal (ReadModel.describe failedBody) "Betrag nicht in YNAB: Verbindung weg" "Fehlschlag"
            Expect.equal (ReadModel.describe EntryAmountWrittenBackToYnab) "Betrag in YNAB angekommen" "angekommen"
    ]

// ============================================
// PUT an YNAB (Fake-Handler, kein echter Aufruf)
// ============================================

type private FakeHandler(status: HttpStatusCode) =
    inherit HttpMessageHandler()
    member val Method : HttpMethod = null with get, set
    member val Url : string = "" with get, set
    member val Body : string = "" with get, set
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            this.Method <- request.Method
            this.Url <- request.RequestUri.ToString()
            let! body = request.Content.ReadAsStringAsync()
            this.Body <- body
            return new HttpResponseMessage(status, Content = new StringContent("{}"))
        }

[<Tests>]
let ynabClientTests =
    testList "YnabClient Betrag (Fake-Handler)" [

        testCase "Body trägt nur amount, in Milliunits als JSON-Zahl mit Vorzeichen" <| fun () ->
            let body = Ynab.buildAmountBody (eur -14.50m)
            let keys =
                Thoth.Json.Net.Decode.fromString
                    (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value))
                    body
                |> Result.map (List.map fst)
            Expect.equal keys (Ok [ "amount" ]) "nur amount"
            let amount =
                Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction"; "amount" ] Thoth.Json.Net.Decode.int) body
            Expect.equal amount (Ok -14500) "Milliunits als Zahl"
            Expect.isFalse (body.Contains "\"-14500\"") "nie als Text"

        testCase "Methode, Pfad und Fehlerzuordnung" <| fun () ->
            let send status =
                let handler = new FakeHandler(status)
                use client = new HttpClient(handler)
                handler, Ynab.sendAmount client (YnabBudgetId "budget-1") "tx-1" (eur 3.00m) |> Async.RunSynchronously
            let handler, result = send HttpStatusCode.OK
            Expect.equal result (Ok ()) "200 ist Erfolg"
            Expect.equal handler.Method HttpMethod.Put "PUT"
            Expect.stringEnds handler.Url "/budgets/budget-1/transactions/tx-1" "Pfad"
            match send HttpStatusCode.BadRequest |> snd with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"400: {other}"
            match send HttpStatusCode.TooManyRequests |> snd with
            | Error (YnabError.RateLimitExceeded _) -> ()
            | other -> failtest $"429: {other}"
    ]
