module LedgerSplitWriteTests

// Split-Schreibweg (ledger-kxme4, ADR 0058).
//
// Gruppen:
//  * reine Domäne: Codec, `apply`, `linksFrom`, `import_id`-Ableitung, Prüfung der Zeilen;
//  * der Befehl `setEntrySplits` und "Erneut senden" gegen In-Memory-SQLite
//    (`testSequenced`, Schema-Init je Testfall, ADR 0030), mit einem injizierten Satz
//    YNAB-Aufrufe, der nie YNAB erreicht;
//  * der Spiegel nach einem Ersatz (abgelöste Id, Split-Übertrags-Verknüpfung);
//  * die JSON-Verträge und HTTP-Wege des YnabClient gegen einen Fake-`HttpMessageHandler`.

open System
open System.Net
open System.Net.Http

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Thoth.Json.Net
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabObservations

module Ynab = Server.YnabClient

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

let private accountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000c1")
let private ynabAccountGuid = Guid.Parse "0195c000-0000-7000-8000-0000000000d1"
let private ynabAccount = YnabAccountId ynabAccountGuid
let private cashAccountGuid = Guid.Parse "0195c000-0000-7000-8000-0000000000d2"
let private at = DateTime(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: Guid) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId id; CategoryName = name }

let private catA = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca01") "Lebensmittel"
let private catB = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca02") "Drogerie"
let private catC = catRef (Guid.Parse "0195c000-0000-7000-8000-00000000ca03") "Freizeit"

let private line (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

let private transferLine (amount: decimal) : TransactionSplit =
    { Target = ToTransfer (YnabAccountId cashAccountGuid, "Bargeld"); Amount = eur amount; Memo = Some "Bar" }

let private twoLines = [ line catA -5.00m; line catB -7.00m ]

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent =
    { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }

let private observedPayload (ynabTxId: string) (importId: string option) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = ynabTxId
    ImportId = importId
    Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -12.00m
    Payee = Some "Rewe"
    Memo = Some "Wocheneinkauf"
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = Some "blue"
    FlagName = None
}

// ============================================
// Reine Domäne
// ============================================

let private unwrapOk (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"sollte gelingen, war: {e}"

[<Tests>]
let splitDomainTests =
    testList "Ledger Split-Schreibweg (Domäne)" [

        testCase "Codec-Rundreise von EntryReplacedInYnab und des Fehlschlags mit gelöschter Id" <| fun () ->
            let bodies = [
                EntryReplacedInYnab {
                    OldYnabTransactionId = "alt"
                    NewYnabTransactionId = "neu"
                    NewImportId = Some "BB:abc:r1"
                }
                EntryReplacedInYnab { OldYnabTransactionId = "alt"; NewYnabTransactionId = "neu"; NewImportId = None }
                EntryCategorizationWriteBackFailed {
                    Reason = "POST gescheitert"
                    BaselineCategory = None
                    BaselineSplits = twoLines
                    YnabDeletedTransactionId = Some "alt"
                }
            ]
            for body in bodies do
                Expect.equal (schemaVersion body) 1 $"Version von {eventTypeName body}"
                Expect.equal (decodePayload (eventTypeName body) 1 (encodePayload body)) (Ok body) $"Rundreise {eventTypeName body}"
            Expect.equal (eventTypeName (List.head bodies)) "EntryReplacedInYnab" "Name"

        testCase "ein gespeicherter Fehlschlag ohne ynabDeletedTransactionId liest sich als None" <| fun () ->
            let legacy = """{"reason":"Timeout","baselineCategory":null,"baselineSplits":[]}"""
            match decodePayload "EntryCategorizationWriteBackFailed" 1 legacy with
            | Ok (EntryCategorizationWriteBackFailed p) -> Expect.isNone p.YnabDeletedTransactionId "alte Ereignisse bleiben lesbar"
            | other -> failtest $"erwartet Fehlschlag, war {other}"

        testCase "linksFrom stiftet neue YNAB-Id und neue import_id und hält die alte Id als abgelöst fest" <| fun () ->
            let entryId = newEntryId ()
            let links =
                linksFrom (
                    eventOn entryId UserInBb (
                        EntryReplacedInYnab {
                            OldYnabTransactionId = "alt"
                            NewYnabTransactionId = "neu"
                            NewImportId = Some "BB:abc:r1"
                        }
                    )
                )
            let asTriples = links |> List.map (fun l -> l.System, l.Value) |> Set.ofList
            Expect.equal
                asTriples
                (Set.ofList [ YnabSystem, "neu"; YnabSupersededSystem, "alt"; ImportIdSystem, "BB:abc:r1" ])
                "drei Verknüpfungen"

        testCase "apply von EntryReplacedInYnab leert die Zuordnung der Split-Übertrags-Gegenbuchungen" <| fun () ->
            let entryId = newEntryId ()
            let observed = apply None (eventOn entryId YnabMirror (EntryObservedInYnab (observedPayload "alt" None)))
            let withCounterpart =
                { observed with SplitTransferCounterparts = Map.ofList [ "sub-1", newEntryId () ] }
            let after =
                apply
                    (Some withCounterpart)
                    (eventOn entryId UserInBb (EntryReplacedInYnab { OldYnabTransactionId = "alt"; NewYnabTransactionId = "neu"; NewImportId = None }))
            Expect.isEmpty (Map.toList after.SplitTransferCounterparts) "alte Subtransaction-Ids gelten nicht mehr"

        testCase "replacementImportId hängt :r<n> an, n zählt die bisherigen Ersätze" <| fun () ->
            Expect.equal (replacementImportId (Some "BB:abc")) (Some "BB:abc:r1") "erster Ersatz"
            Expect.equal (replacementImportId (Some "BB:abc:r1")) (Some "BB:abc:r2") "zweiter Ersatz"
            Expect.equal (replacementImportId (Some "BB:abc:r9")) (Some "BB:abc:r10") "zehnter Ersatz"
            Expect.equal (replacementImportId None) None "ohne import_id keine"

        testCase "currentImportId wählt die mit der höchsten Ersatz-Zahl" <| fun () ->
            let entryId = newEntryId ()
            let link v = { EntryId = entryId; System = ImportIdSystem; Value = v }
            Expect.equal (currentImportId [ link "BB:abc:r1"; link "BB:abc"; link "BB:abc:r2" ]) (Some "BB:abc:r2") "r2 gewinnt"
            Expect.equal (currentImportId [ { link "x" with System = YnabSystem } ]) None "nur import_id-Verknüpfungen"

        testCase "currentYnabTransactionId überspringt abgelöste Ids" <| fun () ->
            let entryId = newEntryId ()
            let link system v = { EntryId = entryId; System = system; Value = v }
            Expect.equal
                (currentYnabTransactionId [ link YnabSystem "alt"; link YnabSystem "neu"; link YnabSupersededSystem "alt" ])
                (Some "neu")
                "die neue Id gilt"

        testCase "classifySplitLines: Prüfung gegen den Buchungsbetrag" <| fun () ->
            let total = eur -12.00m
            let isError r = match r with Error _ -> true | Ok _ -> false
            Expect.isTrue (isError (classifySplitLines total [])) "leere Liste"
            Expect.isTrue (isError (classifySplitLines total [ transferLine -12.00m ])) "einzelne Übertrags-Zeile"
            Expect.isTrue (isError (classifySplitLines total [ line catA -5.00m; line catB -6.00m ])) "Summe ungleich Betrag"
            Expect.isTrue (isError (classifySplitLines total [ line catA -5.00m ])) "eine Zeile mit Teilbetrag"
            Expect.equal (classifySplitLines total [ line catA -12.00m ]) (Ok (SingleLine (Some catA))) "eine Zeile mit Kategorie"
            Expect.equal
                (classifySplitLines total [ { Target = Uncategorized; Amount = total; Memo = None } ])
                (Ok (SingleLine None))
                "eine unkategorisierte Zeile"
            Expect.equal (classifySplitLines total twoLines) (Ok (MultipleLines twoLines)) "zwei Zeilen"
            match classifySplitLines total [ line catA -5.00m; line catB -6.00m ] with
            | Error msg -> Expect.stringContains msg "weicht" "Meldung nennt die Abweichung"
            | Ok _ -> failtest "Summe ungleich Betrag muss abgelehnt werden"

        testCase "Historien-Satz des Ersatzes" <| fun () ->
            Expect.equal
                (Ledger.QueryHandlers.ReadModel.describe (
                    EntryReplacedInYnab { OldYnabTransactionId = "a"; NewYnabTransactionId = "b"; NewImportId = None }
                ))
                "In YNAB neu angelegt (Split-Buchung ersetzt)"
                "Satz"
    ]

// ============================================
// Befehle (In-Memory-SQLite)
// ============================================

let private seedCategories () =
    upsertCategories [
        for c in [ catA; catB; catC ] ->
            { Id = c.CategoryId; Name = c.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }
    ]
    |> Async.RunSynchronously

let private seedAccount () =
    saveAccount {
        Id = accountId
        Name = "Girokonto"
        Kind = BankAccount
        Link = YnabMirrored ynabAccount
        Closed = false
    }
    |> Async.RunSynchronously

let private freshYnabId () = $"ynab-split-{Guid.NewGuid()}"
let private freshImportId () =
    let hex = Guid.NewGuid().ToString("N").Substring(0, 20)
    "BB:" + hex

let private seed (payload: EntryObservedInYnabPayload) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [ eventOn entryId YnabMirror (EntryObservedInYnab payload) ] |> Async.RunSynchronously
    entryId

/// Eine unaufgeteilte, mit YNAB verknüpfte Buchung (import_id `BB:…`).
let private seedPlain () : EntryId * string * string =
    let ynabId = freshYnabId ()
    let importId = freshImportId ()
    seed (observedPayload ynabId (Some importId)), ynabId, importId

/// Eine aufgeteilte Buchung (Lebensmittel und Drogerie), import_id `BB:…`.
let private seedSplit () : EntryId * string * string =
    let ynabId = freshYnabId ()
    let importId = freshImportId ()
    seed { observedPayload ynabId (Some importId) with Splits = twoLines }, ynabId, importId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventTypesOf (entryId: EntryId) : string list =
    loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.EventType)

/// Der Satz YNAB-Aufrufe, der aufzeichnet und feste Ergebnisse liefert.
type private ToolsProbe() =
    member val Puts : (string * TransactionSplit list) list = [] with get, set
    member val CategoryPuts : (string * YnabCategoryId option) list = [] with get, set
    member val Deletes : string list = [] with get, set
    member val Creates : Ynab.SplitReplacement list = [] with get, set
    member val TrackingCalls = 0 with get, set
    member val Tracking : YnabResult<bool> = Ok false with get, set
    member val PutResult : YnabResult<unit> = Ok () with get, set
    member val DeleteResult : YnabResult<unit> = Ok () with get, set
    member val CreateResult : YnabResult<string> option = None with get, set
    member val OnPut : unit -> unit = ignore with get, set
    member this.WriteCalls = this.Puts.Length + this.CategoryPuts.Length + this.Deletes.Length + this.Creates.Length
    member this.Tools : SplitWriteTools = {
        IsTrackingAccount =
            fun _ ->
                async {
                    this.TrackingCalls <- this.TrackingCalls + 1
                    return this.Tracking
                }
        PutCategory =
            fun id category ->
                async {
                    this.CategoryPuts <- this.CategoryPuts @ [ id, category ]
                    return this.PutResult
                }
        PutSplits =
            fun id splits ->
                async {
                    this.OnPut ()
                    this.Puts <- this.Puts @ [ id, splits ]
                    return this.PutResult
                }
        Delete =
            fun id ->
                async {
                    this.Deletes <- this.Deletes @ [ id ]
                    return this.DeleteResult
                }
        Create =
            fun request ->
                async {
                    this.Creates <- this.Creates @ [ request ]
                    return this.CreateResult |> Option.defaultWith (fun () -> Ok (freshYnabId ()))
                }
    }

let private setSplits (probe: ToolsProbe) (entryId: EntryId) (lines: TransactionSplit list) =
    setEntrySplits at probe.Tools entryId lines |> Async.RunSynchronously

let private unwrap (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failtest $"Befehl sollte gelingen, war: {e}"

let private expectRejected (probe: ToolsProbe) (entryId: EntryId) (lines: TransactionSplit list) (what: string) =
    let before = eventTypesOf entryId
    match setSplits probe entryId lines with
    | Error _ -> ()
    | Ok _ -> failtest $"{what} muss abgelehnt werden"
    Expect.equal (eventTypesOf entryId) before $"{what}: kein Ereignis"
    Expect.equal probe.WriteCalls 0 $"{what}: kein schreibender YNAB-Aufruf"

[<Tests>]
let splitCommandTests =
    // Schema erst beim Ausführen eines Testfalls anlegen, nie beim Modul-Laden (ADR 0030).
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            seedCategories ()
            seedAccount ()
            body ())

    testSequenced
    <| testList "Ledger Split-Schreibweg (Befehle)" [

        testCase "Validierung: Summe ungleich Betrag, leere Liste, einzelne Übertrags-Zeile, unbekannte Kategorie" <| fun () ->
            let entryId, _, _ = seedPlain ()
            let probe = ToolsProbe()
            expectRejected probe entryId [ line catA -5.00m; line catB -6.00m ] "Summe ungleich Betrag"
            expectRejected probe entryId [] "leere Liste"
            expectRejected probe entryId [ transferLine -12.00m ] "einzelne Übertrags-Zeile"
            expectRejected
                probe
                entryId
                [ line catA -5.00m; line (catRef (Guid.NewGuid()) "Unbekannt") -7.00m ]
                "unbekannte Kategorie"
            expectRejected probe entryId [ line (catRef (Guid.NewGuid()) "Unbekannt") -12.00m ] "unbekannte Kategorie (eine Zeile)"
            expectRejected probe (newEntryId ()) twoLines "unbekannte Buchung"

        testCase "Sperren: in YNAB gelöscht, nicht verknüpft, Übertrag zwischen Budget-Konten, Reconciled" <| fun () ->
            let probe = ToolsProbe()

            let deletedId, _, _ = seedPlain ()
            appendAndApply [ eventOn deletedId YnabMirror EntryDeletionObservedInYnab ] |> Async.RunSynchronously
            expectRejected probe deletedId twoLines "in YNAB gelöschte Buchung"

            let unlinkedId = newEntryId ()
            appendAndApply [
                eventOn unlinkedId QuickAdd (
                    CashExpenseAdded {
                        AccountId = accountId
                        Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                        Amount = eur -12.00m
                        Payee = Some "Kiosk"
                        Memo = None
                        Category = None
                        Cleared = Uncleared
                        Approved = false
                    }
                )
            ]
            |> Async.RunSynchronously
            expectRejected probe unlinkedId twoLines "nicht verknüpfte Buchung"

            let transferId =
                seed {
                    observedPayload (freshYnabId ()) (Some (freshImportId ())) with
                        TransferAccountId = Some (YnabAccountId cashAccountGuid)
                }
            expectRejected probe transferId twoLines "Übertrag zwischen Budget-Konten"

            let reconciledId = seed { observedPayload (freshYnabId ()) (Some (freshImportId ())) with Cleared = Reconciled }
            expectRejected probe reconciledId twoLines "abgeglichene Buchung"
            Expect.equal probe.TrackingCalls 0 "die Konto-Prüfung kommt erst nach den lokalen Sperren"

        testCase "Sperre: Tracking-Konto" <| fun () ->
            let entryId, _, _ = seedPlain ()
            let probe = ToolsProbe()
            probe.Tracking <- Ok true
            expectRejected probe entryId twoLines "Tracking-Konto"
            Expect.equal probe.TrackingCalls 1 "das Konto wurde geprüft"

        testCase "Sperre: Ändern und Aufheben einer Split-Buchung mit import_id YNAB:" <| fun () ->
            let ynabId = freshYnabId ()
            let entryId =
                seed { observedPayload ynabId (Some "YNAB:-12000:2026-10-01:1") with Splits = twoLines }
            let probe = ToolsProbe()
            expectRejected probe entryId [ line catA -4.00m; line catC -8.00m ] "Ändern mit fremder import_id"
            expectRejected probe entryId [ line catA -12.00m ] "Aufheben mit fremder import_id"

        testCase "Aufteilen einer unaufgeteilten Buchung mit import_id YNAB: ist erlaubt" <| fun () ->
            let ynabId = freshYnabId ()
            let entryId = seed (observedPayload ynabId (Some "YNAB:-12000:2026-10-01:2"))
            let probe = ToolsProbe()
            let outcome = setSplits probe entryId twoLines |> unwrap
            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal probe.Puts [ ynabId, twoLines ] "PUT mit den Zeilen"

        testCase "eine Split-Buchung ohne import_id lässt sich ändern" <| fun () ->
            let ynabId = freshYnabId ()
            let entryId = seed { observedPayload ynabId None with Splits = twoLines }
            let probe = ToolsProbe()
            let outcome = setSplits probe entryId [ line catA -4.00m; line catC -8.00m ] |> unwrap
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal (probe.Creates |> List.map (fun c -> c.ImportId)) [ None ] "ohne alte import_id keine neue"

        testCase "Ziel gleich Ist ergibt CategoryUnchanged ohne Ereignis und ohne Push" <| fun () ->
            let splitId, _, _ = seedSplit ()
            let plainId, _, _ = seedPlain ()
            let probe = ToolsProbe()
            let sameSplit = setSplits probe splitId twoLines |> unwrap
            Expect.equal sameSplit.Change CategoryUnchanged "Split unverändert"
            Expect.equal sameSplit.WriteBack CategoryNotSent "nichts gesendet"
            Expect.equal (eventTypesOf splitId) [ "EntryObservedInYnab" ] "kein Ereignis (Split)"
            let sameCategory = setSplits probe plainId [ { Target = Uncategorized; Amount = eur -12.00m; Memo = None } ] |> unwrap
            Expect.equal sameCategory.Change CategoryUnchanged "ohne Kategorie bleibt ohne Kategorie"
            Expect.equal probe.WriteCalls 0 "kein Push"

        testCase "Aufteilen: ein Recategorized mit NewSplits vor dem Push, danach genau ein PUT, YNAB-Id unverändert" <| fun () ->
            let entryId, ynabId, _ = seedPlain ()
            let probe = ToolsProbe()
            let mutable typesAtPush : string list = []
            probe.OnPut <- fun () -> typesAtPush <- eventTypesOf entryId
            let outcome = setSplits probe entryId twoLines |> unwrap

            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal typesAtPush [ "EntryObservedInYnab"; "EntryRecategorized" ] "Ereignis liegt vor dem Push"
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            let changed = recorded |> List.last
            Expect.equal changed.EventType "EntryRecategorized" "ohne offenen Fehlschlag kein weiteres Ereignis (ADR 0055, Punkt 2)"
            Expect.equal changed.Event.Actor UserInBb "Actor"
            match changed.Event.Body with
            | EntryRecategorized p ->
                Expect.equal p.NewSplits twoLines "NewSplits"
                Expect.isNone p.NewCategory "keine Kategorie neben den Splits"
                Expect.isEmpty p.OldSplits "vorher kein Split"
            | other -> failtest $"erwartet Recategorized, war {other}"
            Expect.equal (entryOf entryId).Splits twoLines "Zustand"
            Expect.equal probe.Puts [ ynabId, twoLines ] "genau ein PUT"
            Expect.isEmpty probe.Deletes "kein DELETE"
            Expect.isEmpty probe.Creates "kein POST"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (currentYnabTransactionId links) (Some ynabId) "YNAB-Id unverändert"

        testCase "Aufteilen: PUT scheitert (auch: Antwort ohne subtransactions) ergibt den Fehlschlag, Erneut senden beendet ihn" <| fun () ->
            let entryId, ynabId, _ = seedPlain ()
            let probe = ToolsProbe()
            probe.PutResult <-
                Error (YnabError.InvalidResponse "Die Antwort enthält keine subtransactions")
            let outcome = setSplits probe entryId twoLines |> unwrap
            match outcome.WriteBack with
            | CategorySendFailed reason ->
                Expect.stringContains reason "Aufteilung abgelehnt" "Grund"
                let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
                Expect.equal failure.BaselineSplits [] "Baseline: in YNAB nicht aufgeteilt"
                Expect.isNone failure.YnabDeletedTransactionId "nichts gelöscht"
            | other -> failtest $"erwartet Fehlschlag, war {other}"
            Expect.equal (entryOf entryId).Splits twoLines "BB behält den Split"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryRecategorized"; "EntryCategorizationWriteBackFailed" ]
                "Log"

            probe.PutResult <- Ok ()
            let retry = retryCategorizationOrSplitWriteBack at (fun _ _ -> async { return Ok () }) probe.Tools entryId |> Async.RunSynchronously |> unwrap
            Expect.equal retry CategorySent "erneut gesendet"
            Expect.equal probe.Puts [ ynabId, twoLines; ynabId, twoLines ] "zweiter PUT"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag beendet"
            Expect.equal (eventTypesOf entryId |> List.last) "EntryCategorizationWrittenBackToYnab" "WrittenBack"

        testCase "Ändern: DELETE, danach POST mit den Feldern des Eintrags und import_id :r1, ein zweiter Ersatz :r2" <| fun () ->
            let entryId, oldId, importId = seedSplit ()
            let probe = ToolsProbe()
            probe.CreateResult <- Some (Ok "ynab-new-1")
            let newLines = [ line catA -4.00m; line catC -8.00m ]
            let outcome = setSplits probe entryId newLines |> unwrap

            Expect.equal outcome.Change CategoryChanged "geändert"
            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal probe.Deletes [ oldId ] "DELETE der alten Id"
            Expect.isEmpty probe.Puts "kein PUT"
            let request = List.exactlyOne probe.Creates
            Expect.equal request.AccountId ynabAccount "Konto"
            Expect.equal request.Date (DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)) "Datum"
            Expect.equal request.Amount (eur -12.00m) "Betrag"
            Expect.equal request.Payee (Some "Rewe") "Payee"
            Expect.equal request.Memo (Some "Wocheneinkauf") "Memo"
            Expect.equal request.FlagColor (Some "blue") "Flag"
            Expect.equal request.Cleared "cleared" "cleared"
            Expect.isTrue request.Approved "approved"
            Expect.equal request.ImportId (Some $"{importId}:r1") "import_id mit Suffix"
            Expect.equal request.Splits newLines "neue Zeilen"
            Expect.isNone request.CategoryId "keine Kategorie neben den Splits"
            Expect.equal
                (eventTypesOf entryId)
                [ "EntryObservedInYnab"; "EntryRecategorized"; "EntryReplacedInYnab" ]
                "Ereignisse"
            Expect.equal (entryOf entryId).Splits newLines "Zustand"

            probe.CreateResult <- Some (Ok "ynab-new-2")
            setSplits probe entryId twoLines |> unwrap |> ignore
            Expect.equal probe.Deletes [ oldId; "ynab-new-1" ] "der zweite Ersatz löscht die neue Id"
            Expect.equal (probe.Creates |> List.map (fun c -> c.ImportId) |> List.last) (Some $"{importId}:r2") "zweiter Ersatz sendet :r2"

        testCase "Aufheben (eine Zeile): DELETE, POST mit category_id und ohne subtransactions, Eintrag trägt die Kategorie" <| fun () ->
            let entryId, oldId, importId = seedSplit ()
            let probe = ToolsProbe()
            let outcome = setSplits probe entryId [ line catC -12.00m ] |> unwrap

            Expect.equal outcome.WriteBack CategorySent "gesendet"
            Expect.equal probe.Deletes [ oldId ] "DELETE"
            let request = List.exactlyOne probe.Creates
            Expect.equal request.CategoryId (Some catC.CategoryId) "category_id"
            Expect.isEmpty request.Splits "ohne subtransactions"
            Expect.equal request.ImportId (Some $"{importId}:r1") "import_id"
            let e = entryOf entryId
            Expect.equal e.Category (Some catC) "Kategorie"
            Expect.isEmpty e.Splits "keine Splits"
            match (loadEventsForEntry entryId |> Async.RunSynchronously |> List.item 1).Event.Body with
            | EntryRecategorized p ->
                Expect.equal p.OldSplits twoLines "alte Zeilen"
                Expect.equal p.NewCategory (Some catC) "NewCategory"
                Expect.isEmpty p.NewSplits "NewSplits leer"
            | other -> failtest $"erwartet Recategorized, war {other}"

        testCase "Aufheben auf 'ohne Kategorie' (unkategorisierte Zeile)" <| fun () ->
            let entryId, _, _ = seedSplit ()
            let probe = ToolsProbe()
            setSplits probe entryId [ { Target = Uncategorized; Amount = eur -12.00m; Memo = None } ] |> unwrap |> ignore
            Expect.isNone (entryOf entryId).Category "ohne Kategorie"
            Expect.isNone (List.exactlyOne probe.Creates).CategoryId "kein category_id im POST"

        testCase "nach einem Ersatz zeigen neue YNAB-Id und neue import_id auf denselben Eintrag, die Lösch-Beobachtung der alten Id setzt DeletedInYnab nicht" <| fun () ->
            let entryId, oldId, importId = seedSplit ()
            let newId = freshYnabId ()
            let probe = ToolsProbe()
            probe.CreateResult <- Some (Ok newId)
            setSplits probe entryId [ line catA -4.00m; line catC -8.00m ] |> unwrap |> ignore

            Expect.equal (findEntryByLink YnabSystem newId |> Async.RunSynchronously) (Some entryId) "neue YNAB-Id"
            Expect.equal (findEntryByLink ImportIdSystem $"{importId}:r1" |> Async.RunSynchronously) (Some entryId) "neue import_id"
            Expect.equal (findEntryByLink YnabSystem oldId |> Async.RunSynchronously) (Some entryId) "die alte Id findet den Eintrag weiter"
            Expect.equal (findEntryByLink YnabSupersededSystem oldId |> Async.RunSynchronously) (Some entryId) "alte Id als abgelöst festgehalten"

            let deletion (ynabId: string) : YnabObservation = {
                YnabTransactionId = ynabId
                AccountId = ynabAccount
                ImportId = None
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -12.00m
                Payee = Some "Rewe"
                Memo = Some "Wocheneinkauf"
                Category = None
                Splits = []
                SplitTransfers = []
                Cleared = Cleared
                TransferAccountId = None
                TransferYnabTransactionId = None
                Deleted = true
                Approved = true
                FlagColor = Some "blue"
                FlagName = None
            }
            let runWith (observations: YnabObservation list) =
                let fetchers : MirrorFetchers = {
                    emptyMirrorFetchers with
                        FetchAccounts =
                            fun _ ->
                                async { return Ok { Accounts = [ { AccountId = ynabAccount; Name = "Girokonto"; Closed = false; Deleted = false } ]; ServerKnowledge = 1L } }
                        FetchTransactions = fun _ -> async { return Ok { Observations = observations; ServerKnowledge = 1L } }
                }
                runMirror at fetchers (YnabBudgetId "budget-kxme4") |> Async.RunSynchronously |> unwrap

            let summary = runWith [ deletion oldId ]
            Expect.equal summary.DeletedEntries 0 "die abgelöste Id zählt nicht als gelöscht"
            Expect.isFalse (entryOf entryId).DeletedInYnab "DeletedInYnab bleibt aus"
            Expect.isFalse (List.contains "EntryDeletionObservedInYnab" (eventTypesOf entryId)) "kein Lösch-Ereignis"

            runWith [ deletion newId ] |> ignore
            Expect.isTrue (entryOf entryId).DeletedInYnab "die Lösch-Beobachtung der neuen Id gilt weiter"

        testCase "POST scheitert nach gelungenem DELETE: Fehlschlag 'Neuanlage offen', Erneut senden ruft nur POST" <| fun () ->
            let entryId, oldId, importId = seedSplit ()
            let probe = ToolsProbe()
            probe.CreateResult <- Some (Error (YnabError.NetworkError "Timeout"))
            let outcome = setSplits probe entryId [ line catA -4.00m; line catC -8.00m ] |> unwrap

            match outcome.WriteBack with
            | CategorySendFailed _ -> ()
            | other -> failtest $"erwartet Fehlschlag, war {other}"
            let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
            Expect.equal failure.YnabDeletedTransactionId (Some oldId) "gelöschte Id gemerkt"
            Expect.equal failure.BaselineSplits twoLines "Baseline: der alte Split"
            Expect.isFalse (List.contains "EntryReplacedInYnab" (eventTypesOf entryId)) "noch kein Ersatz"

            // Der Spiegel sieht das Löschen der alten Id und darf die Buchung nicht als gelöscht führen.
            let fetchers : MirrorFetchers = {
                emptyMirrorFetchers with
                    FetchAccounts =
                        fun _ ->
                            async { return Ok { Accounts = [ { AccountId = ynabAccount; Name = "Girokonto"; Closed = false; Deleted = false } ]; ServerKnowledge = 1L } }
                    FetchTransactions =
                        fun _ ->
                            async {
                                return
                                    Ok {
                                        Observations = [
                                            {
                                                YnabTransactionId = oldId
                                                AccountId = ynabAccount
                                                ImportId = Some importId
                                                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                                                Amount = eur -12.00m
                                                Payee = Some "Rewe"
                                                Memo = Some "Wocheneinkauf"
                                                Category = None
                                                Splits = []
                                                SplitTransfers = []
                                                Cleared = Cleared
                                                TransferAccountId = None
                                                TransferYnabTransactionId = None
                                                Deleted = true
                                                Approved = true
                                                FlagColor = Some "blue"
                                                FlagName = None
                                            }
                                        ]
                                        ServerKnowledge = 1L
                                    }
                            }
            }
            runMirror at fetchers (YnabBudgetId "budget-kxme4") |> Async.RunSynchronously |> unwrap |> ignore
            Expect.isFalse (entryOf entryId).DeletedInYnab "DeletedInYnab bleibt aus"

            let retryProbe = ToolsProbe()
            retryProbe.DeleteResult <- Error (YnabError.NetworkError "darf nicht aufgerufen werden")
            retryProbe.CreateResult <- Some (Ok "ynab-recreated")
            let retry =
                retryCategorizationOrSplitWriteBack at (fun _ _ -> async { return Ok () }) retryProbe.Tools entryId
                |> Async.RunSynchronously
                |> unwrap
            Expect.equal retry CategorySent "gesendet"
            Expect.isEmpty retryProbe.Deletes "kein erneutes DELETE"
            let request = List.exactlyOne retryProbe.Creates
            Expect.equal request.ImportId (Some $"{importId}:r1") "import_id wie beim ersten Versuch"
            Expect.equal request.Splits [ line catA -4.00m; line catC -8.00m ] "BB-Stand"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag beendet"
            Expect.equal
                (eventTypesOf entryId |> List.skip 2)
                [ "EntryCategorizationWriteBackFailed"; "EntryReplacedInYnab"; "EntryCategorizationWrittenBackToYnab" ]
                "Log nach dem Ereignis"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (currentYnabTransactionId links) (Some "ynab-recreated") "neue Id gilt"

        testCase "DELETE scheitert: Fehlschlag wie bei der Kategorie, kein POST; Erneut senden wiederholt das DELETE" <| fun () ->
            let entryId, oldId, _ = seedSplit ()
            let probe = ToolsProbe()
            probe.DeleteResult <- Error (YnabError.RateLimitExceeded 60)
            let outcome = setSplits probe entryId [ line catA -4.00m; line catC -8.00m ] |> unwrap
            match outcome.WriteBack with
            | CategorySendFailed reason -> Expect.stringContains reason "Limit" "Grund"
            | other -> failtest $"erwartet Fehlschlag, war {other}"
            Expect.isEmpty probe.Creates "kein POST"
            let failure = (entryOf entryId).CategorizationWriteBackFailure |> Option.get
            Expect.isNone failure.YnabDeletedTransactionId "nichts gelöscht"
            Expect.equal failure.BaselineSplits twoLines "Baseline"

            probe.DeleteResult <- Ok ()
            let retry = retryCategorizationOrSplitWriteBack at (fun _ _ -> async { return Ok () }) probe.Tools entryId |> Async.RunSynchronously |> unwrap
            Expect.equal retry CategorySent "gesendet"
            Expect.equal probe.Deletes [ oldId; oldId ] "DELETE wiederholt"
            Expect.equal probe.Creates.Length 1 "ein POST"

        testCase "setEntryCategory lehnt ab, solange YNAB noch den alten Split hält" <| fun () ->
            let entryId, _, _ = seedSplit ()
            let probe = ToolsProbe()
            probe.DeleteResult <- Error (YnabError.NetworkError "x")
            setSplits probe entryId [ line catC -12.00m ] |> unwrap |> ignore
            Expect.isEmpty (entryOf entryId).Splits "BB hat keinen Split mehr"
            let result =
                setEntryCategory at (fun _ _ -> async { return Ok () }) entryId (Some catA.CategoryId)
                |> Async.RunSynchronously
            match result with
            | Error msg -> Expect.stringContains msg "erneut senden" "Hinweis"
            | Ok _ -> failtest "muss abgelehnt werden"

        testCase "Ersatz mit Übertrags-Zeile: SplitTransferCounterparts zeigt auf die neue Subtransaction-Id und die neue Gegenbuchung" <| fun () ->
            let oldId = freshYnabId ()
            let importId = freshImportId ()
            let withTransfer = [ line catA -5.00m; transferLine -7.00m ]
            let entryId = seed { observedPayload oldId (Some importId) with Splits = withTransfer }

            // Die Gegenbuchung vor dem Ersatz und ihre Verknüpfung (ADR 0046).
            let oldCounterpartYnab = freshYnabId ()
            let oldCounterpart = seed { observedPayload oldCounterpartYnab None with Amount = eur 7.00m; AccountId = accountId }
            appendAndApply [
                eventOn entryId YnabMirror (
                    EntrySplitTransferCounterpartObservedInYnab {
                        YnabSubtransactionId = "sub-old"
                        CounterpartEntryId = oldCounterpart
                        CounterpartYnabTransactionId = oldCounterpartYnab
                        SplitAmount = eur -7.00m
                    }
                )
            ]
            |> Async.RunSynchronously
            Expect.equal (entryOf entryId).SplitTransferCounterparts (Map.ofList [ "sub-old", oldCounterpart ]) "vorher verknüpft"

            let newId = freshYnabId ()
            let probe = ToolsProbe()
            probe.CreateResult <- Some (Ok newId)
            let newLines = [ line catB -5.00m; transferLine -7.00m ]
            setSplits probe entryId newLines |> unwrap |> ignore
            Expect.isEmpty (Map.toList (entryOf entryId).SplitTransferCounterparts) "nach dem Ersatz leer"

            // YNAB hat die Gegenbuchung neu angelegt; der Spiegel sieht sie und die neue Buchung.
            let newCounterpartYnab = freshYnabId ()
            let newCounterpart = seed { observedPayload newCounterpartYnab None with Amount = eur 7.00m }
            let observation : YnabObservation = {
                YnabTransactionId = newId
                AccountId = ynabAccount
                ImportId = Some $"{importId}:r1"
                Date = DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -12.00m
                Payee = Some "Rewe"
                Memo = Some "Wocheneinkauf"
                Category = None
                Splits = newLines
                SplitTransfers = [
                    {
                        YnabSubtransactionId = "sub-new"
                        TransferAccountId = YnabAccountId cashAccountGuid
                        TransferYnabTransactionId = Some newCounterpartYnab
                        Amount = eur -7.00m
                    }
                ]
                Cleared = Cleared
                TransferAccountId = None
                TransferYnabTransactionId = None
                Deleted = false
                Approved = true
                FlagColor = Some "blue"
                FlagName = None
            }
            let fetchers : MirrorFetchers = {
                emptyMirrorFetchers with
                    FetchAccounts =
                        fun _ ->
                            async { return Ok { Accounts = [ { AccountId = ynabAccount; Name = "Girokonto"; Closed = false; Deleted = false } ]; ServerKnowledge = 1L } }
                    FetchTransactions = fun _ -> async { return Ok { Observations = [ observation ]; ServerKnowledge = 1L } }
            }
            runMirror at fetchers (YnabBudgetId "budget-kxme4") |> Async.RunSynchronously |> unwrap |> ignore
            Expect.equal
                (entryOf entryId).SplitTransferCounterparts
                (Map.ofList [ "sub-new", newCounterpart ])
                "neue Subtransaction-Id, neue Gegenbuchung"
            Expect.isFalse (List.contains "EntryCategorizationChangeObservedInYnab" (eventTypesOf entryId)) "der Spiegel meldet keine Änderung: BB und YNAB stimmen überein"

        testCase "Zustand aus der Datenbank gleich Replay des Logs nach Ersatz und Fehlschlag" <| fun () ->
            let entryId, _, _ = seedSplit ()
            let probe = ToolsProbe()
            probe.CreateResult <- Some (Error (YnabError.NetworkError "x"))
            setSplits probe entryId [ line catA -4.00m; line catC -8.00m ] |> unwrap |> ignore
            let fromDb = entryOf entryId
            let replayed =
                loadEventsForEntry entryId |> Async.RunSynchronously |> List.map (fun r -> r.Event) |> replay |> Option.get
            Expect.equal fromDb replayed "gelesener Zustand gleich Replay des Logs"
    ]

// ============================================
// JSON-Verträge und HTTP-Wege des YnabClient
// ============================================

let private payeeMap = Map.ofList [ YnabAccountId cashAccountGuid, YnabPayeeId (Guid.Parse "0195c000-0000-7000-8000-0000000000e1") ]

let private replacement : Ynab.SplitReplacement = {
    AccountId = ynabAccount
    Date = DateTime(2026, 10, 1)
    Amount = eur -12.00m
    Payee = Some "Rewe"
    Memo = Some "Wocheneinkauf"
    FlagColor = Some "blue"
    Cleared = "cleared"
    Approved = true
    ImportId = Some "BB:abc:r1"
    CategoryId = None
    Splits = [ line catA -5.00m; transferLine -7.00m ]
}

let private fieldOf (path: string list) (decoder: Decoder<'a>) (json: string) : 'a =
    match Decode.fromString (Decode.at path decoder) json with
    | Ok v -> v
    | Error e -> failtest $"Pfad {path}: {e}"

type private FakeHandler(status: HttpStatusCode, responseBody: string) =
    inherit HttpMessageHandler()
    member val Method : HttpMethod = null with get, set
    member val Url : string = "" with get, set
    member val Body : string = "" with get, set
    override this.SendAsync(request: HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            this.Method <- request.Method
            this.Url <- request.RequestUri.ToString()
            let! body = if isNull request.Content then task { return "" } else request.Content.ReadAsStringAsync()
            this.Body <- body
            return new HttpResponseMessage(status, Content = new StringContent(responseBody))
        }

[<Tests>]
let splitYnabClientTests =
    let budget = YnabBudgetId "budget-1"
    testList "YnabClient Split-Schreibweg (Fake-Handler)" [

        testCase "Aufteilen: category_id null, Zeilen als subtransactions, Übertrag mit payee_id und ohne category_id, Milliunits als Zahl" <| fun () ->
            let body =
                match Ynab.buildSplitBody payeeMap replacement.Splits with
                | Ok b -> b
                | Error _ -> failtest "sollte gelingen"
            Expect.stringContains body "\"category_id\": null" "category_id wörtlich null"
            let subs = fieldOf [ "transaction"; "subtransactions" ] (Decode.list (Decode.keyValuePairs Decode.value)) body
            Expect.equal (subs |> List.map (List.map fst)) [ [ "amount"; "category_id" ]; [ "amount"; "payee_id"; "memo" ] ] "Schlüssel je Zeile"
            let amounts = fieldOf [ "transaction"; "subtransactions" ] (Decode.list (Decode.field "amount" Decode.int)) body
            Expect.equal amounts [ -5000; -7000 ] "Beträge in Milliunits als Zahl"
            let payeeIds = fieldOf [ "transaction"; "subtransactions" ] (Decode.list (Decode.object (fun get -> get.Optional.Field "payee_id" Decode.string))) body
            Expect.equal payeeIds [ None; Some "0195c000-0000-7000-8000-0000000000e1" ] "transfer_payee_id des Zielkontos"

        testCase "Aufteilen: ein Zielkonto ohne Übertrags-Payee wird als Fehler gemeldet" <| fun () ->
            Expect.equal (Ynab.buildSplitBody Map.empty replacement.Splits) (Error (YnabAccountId cashAccountGuid)) "Konto genannt"

        testCase "Neuanlage: Kopfdaten des Eintrags, import_id, Zeilen; ohne Zeilen category_id und keine subtransactions" <| fun () ->
            let body =
                match Ynab.buildReplacementBody payeeMap replacement with
                | Ok b -> b
                | Error _ -> failtest "sollte gelingen"
            let str name = fieldOf [ "transaction"; name ] Decode.string body
            Expect.equal (str "account_id") (ynabAccountGuid.ToString()) "account_id"
            Expect.equal (str "date") "2026-10-01" "date"
            Expect.equal (fieldOf [ "transaction"; "amount" ] Decode.int body) -12000 "amount in Milliunits als Zahl"
            Expect.equal (str "payee_name") "Rewe" "payee_name"
            Expect.equal (str "memo") "Wocheneinkauf" "memo"
            Expect.equal (str "cleared") "cleared" "cleared"
            Expect.isTrue (fieldOf [ "transaction"; "approved" ] Decode.bool body) "approved"
            Expect.equal (str "flag_color") "blue" "flag_color"
            Expect.equal (str "import_id") "BB:abc:r1" "import_id"
            Expect.stringContains body "subtransactions" "Zeilen"
            let topKeys = fieldOf [ "transaction" ] (Decode.keyValuePairs Decode.value) body |> List.map fst
            Expect.isFalse (List.contains "category_id" topKeys) "keine category_id auf der Buchung neben den Zeilen"

            let lifted =
                match Ynab.buildReplacementBody payeeMap { replacement with Splits = []; CategoryId = Some catC.CategoryId; ImportId = None; Payee = None; Memo = None; FlagColor = None } with
                | Ok b -> b
                | Error _ -> failtest "sollte gelingen"
            Expect.equal (fieldOf [ "transaction"; "category_id" ] Decode.string lifted) (catC.CategoryId |> fun (YnabCategoryId g) -> g.ToString()) "category_id beim Aufheben"
            Expect.isFalse (lifted.Contains "subtransactions") "keine subtransactions beim Aufheben"
            Expect.isFalse (lifted.Contains "import_id") "ohne alte import_id keine neue"
            Expect.isFalse (lifted.Contains "payee_name") "ohne Payee kein payee_name"

        testCase "PUT: Antwort mit subtransactions ist Erfolg, ohne subtransactions ein Fehlschlag" <| fun () ->
            let ok = new FakeHandler(HttpStatusCode.OK, """{"data":{"transaction":{"id":"t","subtransactions":[{"id":"s1"},{"id":"s2"}]}}}""")
            use okClient = new HttpClient(ok)
            Expect.equal (Ynab.sendSplits okClient budget "tx-1" "{}" |> Async.RunSynchronously) (Ok ()) "Erfolg"
            Expect.equal ok.Method HttpMethod.Put "PUT"
            Expect.stringEnds ok.Url "/budgets/budget-1/transactions/tx-1" "Pfad"

            let ignored = new FakeHandler(HttpStatusCode.OK, """{"data":{"transaction":{"id":"t","subtransactions":[]}}}""")
            use ignoredClient = new HttpClient(ignored)
            match Ynab.sendSplits ignoredClient budget "tx-1" "{}" |> Async.RunSynchronously with
            | Error (YnabError.InvalidResponse msg) -> Expect.stringContains msg "subtransactions" "Grund"
            | other -> failtest $"erwartet Fehlschlag, war {other}"

            let bad = new FakeHandler(HttpStatusCode.BadRequest, "{}")
            use badClient = new HttpClient(bad)
            match Ynab.sendSplits badClient budget "tx-1" "{}" |> Async.RunSynchronously with
            | Error (YnabError.InvalidResponse _) -> ()
            | other -> failtest $"400: {other}"

        testCase "DELETE: Methode, Pfad und Fehlerzuordnung" <| fun () ->
            let ok = new FakeHandler(HttpStatusCode.OK, """{"data":{}}""")
            use okClient = new HttpClient(ok)
            Expect.equal (Ynab.sendDeleteTransaction okClient budget "tx-9" |> Async.RunSynchronously) (Ok ()) "200 ist Erfolg"
            Expect.equal ok.Method HttpMethod.Delete "DELETE"
            Expect.stringEnds ok.Url "/budgets/budget-1/transactions/tx-9" "Pfad"

            let limited = new FakeHandler(enum<HttpStatusCode> 429, "{}")
            use limitedClient = new HttpClient(limited)
            match Ynab.sendDeleteTransaction limitedClient budget "tx-9" |> Async.RunSynchronously with
            | Error (YnabError.RateLimitExceeded _) -> ()
            | other -> failtest $"429: {other}"

        testCase "POST: Methode, Pfad, neue Id aus der Antwort, 409 als Fehler" <| fun () ->
            let ok = new FakeHandler(HttpStatusCode.Created, """{"data":{"transaction_ids":["neu-1"],"transaction":{"id":"neu-1"}}}""")
            use okClient = new HttpClient(ok)
            Expect.equal (Ynab.sendCreateTransaction okClient budget "{\"transaction\":{}}" |> Async.RunSynchronously) (Ok "neu-1") "Id"
            Expect.equal ok.Method HttpMethod.Post "POST"
            Expect.stringEnds ok.Url "/budgets/budget-1/transactions" "Pfad"
            Expect.stringContains ok.Body "transaction" "Body"

            let conflict = new FakeHandler(HttpStatusCode.Conflict, """{"error":{"detail":"same import_id"}}""")
            use conflictClient = new HttpClient(conflict)
            match Ynab.sendCreateTransaction conflictClient budget "{}" |> Async.RunSynchronously with
            | Error (YnabError.InvalidResponse msg) -> Expect.stringContains msg "import_id" "Grund"
            | other -> failtest $"409: {other}"

        testCase "parseCreatedTransactionId liest data.transaction.id, sonst data.transaction_ids[0]" <| fun () ->
            Expect.equal (Ynab.parseCreatedTransactionId """{"data":{"transaction":{"id":"a"}}}""") (Some "a") "transaction.id"
            Expect.equal (Ynab.parseCreatedTransactionId """{"data":{"transaction_ids":["b"]}}""") (Some "b") "transaction_ids"
            Expect.equal (Ynab.parseCreatedTransactionId """{"data":{}}""") None "keine Id"

        testCase "parseAccountsResponse liest on_budget" <| fun () ->
            let body =
                $"""{{"data":{{"accounts":[{{"id":"{ynabAccountGuid}","name":"Giro","balance":0,"on_budget":false,"closed":false}}]}}}}"""
            match Ynab.parseAccountsResponse body with
            | Ok [ account ] -> Expect.isFalse account.OnBudget "Tracking-Konto"
            | other -> failtest $"erwartet ein Konto, war {other}"
    ]
