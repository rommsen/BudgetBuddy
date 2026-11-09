module LedgerEntryDeletionTests

// Buchung löschen im Detail und Rückgängig (ledger-z6sya, ADR 0092).
//
// Gruppen:
//  * reine Domäne: Codec, `apply`, `linksFrom`, Historien-Sätze, Neuanlage aus dem Ereignis;
//  * die Befehle `deleteEntry` und `restoreDeletedEntry` gegen In-Memory-SQLite mit injizierten
//    YNAB-Aufrufen, die nie YNAB erreichen und ihre Aufrufe aufzeichnen (Vorbild
//    `LedgerBookingCaptureTests`, Rückgängig nach dem Erfassen);
//  * der Spiegel nach dem Löschen und nach dem Wiederherstellen;
//  * die API-Wege `deleteEntryWith` und `restoreDeletedEntryWith` mit Fake-Ports;
//  * der JSON-Vertrag der Neuanlage eines Übertrags (`payee_id` statt `payee_name`).

open System

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
let private at = DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc)

let private catRef (id: string) (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.Parse id); CategoryName = name }

let private food = catRef "0195c000-0000-7000-8000-00000000e601" |> fun f -> f "Lebensmittel"
let private drug = catRef "0195c000-0000-7000-8000-00000000e602" |> fun f -> f "Drogerie"

let private line (c: CategoryRef) (amount: decimal) : TransactionSplit =
    { Target = ToCategory (c.CategoryId, c.CategoryName); Amount = eur amount; Memo = None }

/// Ein Kassenbuch-Konto mit YNAB-Konto, je Testfall neu.
type private Account = { Ledger: LedgerAccountId; Ynab: YnabAccountId }

let private freshAccount () : Account =
    let account = { Ledger = LedgerAccountId (Guid.NewGuid()); Ynab = YnabAccountId (Guid.NewGuid()) }
    saveAccount { Id = account.Ledger; Name = "Girokonto"; Kind = BankAccount; Link = YnabMirrored account.Ynab; Closed = false }
    |> Async.RunSynchronously
    account

let private freshYnabId () = $"ynab-z6sya-{Guid.NewGuid()}"

let private observed (account: Account) (ynabId: string) (importId: string option) : EntryObservedInYnabPayload = {
    AccountId = account.Ledger
    YnabTransactionId = ynabId
    ImportId = importId
    Date = DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -42.80m
    Payee = Some "Rewe Markt"
    Memo = Some "Wocheneinkauf"
    Category = Some food
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = Some "purple"
    FlagName = None
}

let private seed (payload: EntryObservedInYnabPayload) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [ { EntryId = entryId; OccurredAt = at; Actor = YnabMirror; Body = EntryObservedInYnab payload } ]
    |> Async.RunSynchronously
    entryId

let private entryOf (entryId: EntryId) : Entry = loadEntry entryId |> Async.RunSynchronously |> Option.get

let private eventsOf (entryId: EntryId) : RecordedLedgerEvent list = loadEventsForEntry entryId |> Async.RunSynchronously

let private balanceOf (account: Account) : decimal =
    let (LedgerAccountId id) = account.Ledger
    Ledger.QueryHandlers.getLedgerAccountBalances ()
    |> Async.RunSynchronously
    |> List.find (fun b -> b.AccountId = id)
    |> fun b -> b.Balance.Amount

/// Die YNAB-Aufrufe von Löschen und Wiederherstellen: zeichnen auf und liefern feste Ergebnisse.
type private Probe() =
    member val Deletes : string list = [] with get, set
    member val Creates : Ynab.SplitReplacement list = [] with get, set
    member val DeleteResult : YnabResult<unit> = Ok () with get, set
    /// Ohne festes Ergebnis liefert der POST je Aufruf eine neue YNAB-Id (Verknüpfungen sind eindeutig).
    member val CreateResult : YnabResult<string> option = None with get, set
    member val MirrorTriggers = 0 with get, set
    member this.Delete (id: string) =
        async {
            this.Deletes <- this.Deletes @ [ id ]
            return this.DeleteResult
        }
    member this.Create (request: Ynab.SplitReplacement) =
        async {
            this.Creates <- this.Creates @ [ request ]
            return this.CreateResult |> Option.defaultWith (fun () -> Ok (freshYnabId ()))
        }
    member this.Trigger () = this.MirrorTriggers <- this.MirrorTriggers + 1

let private delete (probe: Probe) (entryId: EntryId) =
    deleteEntry at probe.Delete probe.Trigger entryId |> Async.RunSynchronously

let private restore (probe: Probe) (entryId: EntryId) =
    restoreDeletedEntry at probe.Create probe.Trigger entryId |> Async.RunSynchronously

let private expectOk (label: string) (result: Result<unit, string>) =
    match result with
    | Ok () -> ()
    | Error e -> failtest $"{label} sollte gelingen, war: {e}"

// ============================================
// Reine Domäne
// ============================================

let private deletedPayload : EntryDeletedInBbPayload = {
    YnabTransactionId = "ynab-alt"
    Date = DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -42.80m
    Payee = Some "Rewe Markt"
    Memo = Some "Wocheneinkauf"
    Category = Some food
    Splits = []
    TransferAccountId = None
    FlagColor = Some "purple"
    Cleared = Cleared
    Approved = true
    ImportId = Some "BB:abc"
}

[<Tests>]
let deletionDomainTests =
    testList "Buchung löschen (Domäne, ledger-z6sya)" [
        testCase "Codec-Rundreise von EntryDeletedInBb und EntryRestoredInBb" <| fun () ->
            let transferAccount = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000e6a1")
            let bodies = [
                EntryDeletedInBb deletedPayload
                EntryDeletedInBb
                    { deletedPayload with
                        Payee = None; Memo = None; Category = None; Splits = [ line food -20m; line drug -22.80m ]
                        TransferAccountId = Some transferAccount; FlagColor = None; Cleared = Uncleared
                        Approved = false; ImportId = None }
                EntryRestoredInBb { OldYnabTransactionId = "alt"; NewYnabTransactionId = "neu"; NewImportId = Some "BB:abc:r1" }
                EntryRestoredInBb { OldYnabTransactionId = "alt"; NewYnabTransactionId = "neu"; NewImportId = None }
            ]
            for body in bodies do
                Expect.equal (schemaVersion body) 1 $"Version von {eventTypeName body}"
                Expect.equal (decodePayload (eventTypeName body) 1 (encodePayload body)) (Ok body) $"Rundreise {eventTypeName body}"
            Expect.equal (eventTypeName (List.head bodies)) "EntryDeletedInBb" "Name Löschen"
            Expect.equal (eventTypeName (List.last bodies)) "EntryRestoredInBb" "Name Wiederherstellen"

        testCase "linksFrom von EntryRestoredInBb stiftet neue YNAB-Id und Ersatz-import_id und hält die alte als abgelöst fest" <| fun () ->
            let entryId = newEntryId ()
            let links =
                linksFrom {
                    EntryId = entryId; OccurredAt = at; Actor = UserInBb
                    Body = EntryRestoredInBb { OldYnabTransactionId = "alt"; NewYnabTransactionId = "neu"; NewImportId = Some "BB:abc:r1" }
                }
            Expect.equal
                (links |> List.map (fun l -> l.System, l.Value) |> Set.ofList)
                (Set.ofList [ YnabSystem, "neu"; YnabSupersededSystem, "alt"; ImportIdSystem, "BB:abc:r1" ])
                "drei Verknüpfungen"
            Expect.isEmpty (linksFrom { EntryId = entryId; OccurredAt = at; Actor = UserInBb; Body = EntryDeletedInBb deletedPayload }) "Löschen stiftet nichts"

        testCase "Historie: In BB gelöscht und In BB wiederhergestellt, Actor Roman" <| fun () ->
            Expect.equal (Ledger.QueryHandlers.ReadModel.describe (EntryDeletedInBb deletedPayload)) "In BB gelöscht" "Löschen"
            Expect.equal
                (Ledger.QueryHandlers.ReadModel.describe (
                    EntryRestoredInBb { OldYnabTransactionId = "a"; NewYnabTransactionId = "b"; NewImportId = None }
                ))
                "In BB wiederhergestellt"
                "Wiederherstellen"
            let line =
                Ledger.QueryHandlers.ReadModel.toHistoryLine {
                    Sequence = 2; EventType = "EntryDeletedInBb"; SchemaVersion = 1
                    Event = { EntryId = newEntryId (); OccurredAt = at; Actor = UserInBb; Body = EntryDeletedInBb deletedPayload }
                }
            Expect.equal line.Origin DecidedInBb "Actor Roman (in BB)"

        testCase "pendingDeletionIn: nur ein Löschen ohne folgendes Wiederherstellen zählt" <| fun () ->
            let entryId = newEntryId ()
            let ev body = { EntryId = entryId; OccurredAt = at; Actor = UserInBb; Body = body }
            let restored = EntryRestoredInBb { OldYnabTransactionId = "a"; NewYnabTransactionId = "b"; NewImportId = None }
            Expect.equal (pendingDeletionIn [ ev (EntryDeletedInBb deletedPayload) ]) (Some deletedPayload) "offen"
            Expect.isNone (pendingDeletionIn [ ev (EntryDeletedInBb deletedPayload); ev restored ]) "zurückgenommen"
            Expect.isNone (pendingDeletionIn [ ev EntryDeletionObservedInYnab ]) "in YNAB gelöscht ist kein BB-Löschen"

        testCase "nach dem Wiederherstellen ist kein Fehlschlag beim Zurückschreiben mehr offen" <| fun () ->
            // Bug (Review ledger-z6sya): die Neuanlage schickt den ganzen BB-Stand. Blieb ein alter
            // Kategorie-/Split-Fehlschlag mit gelungenem DELETE stehen, legte "Erneut senden" danach
            // per POST eine zweite Buchung in YNAB an; die übrigen Fehlschläge zeigten einen
            // Hinweis "nicht in YNAB" für einen Stand, den YNAB schon hat.
            let entryId = newEntryId ()
            let ev actor body = { EntryId = entryId; OccurredAt = at; Actor = actor; Body = body }
            let observedPayload : EntryObservedInYnabPayload = {
                AccountId = LedgerAccountId (Guid.NewGuid()); YnabTransactionId = "ynab-alt"; ImportId = None
                Date = deletedPayload.Date; Amount = eur -42.80m; Payee = Some "Rewe Markt"; Memo = None
                Category = None; Splits = [ line food -20m; line drug -22.80m ]; Cleared = Cleared
                TransferAccountId = None; TransferYnabTransactionId = None
                Approved = true; FlagColor = None; FlagName = None
            }
            let entry =
                replay [
                    ev YnabMirror (EntryObservedInYnab observedPayload)
                    ev UserInBb (EntryCategorizationWriteBackFailed {
                        Reason = "POST gescheitert"; BaselineCategory = None; BaselineSplits = observedPayload.Splits
                        YnabDeletedTransactionId = Some "ynab-alt"; BaselineTransferAccountId = None })
                    ev UserInBb (EntryMemoWriteBackFailed { Reason = "weg"; BaselineMemo = None })
                    ev UserInBb (EntryAmountWriteBackFailed { Reason = "weg"; BaselineAmount = eur -42.80m })
                    ev UserInBb (EntryFlagWriteBackFailed { Reason = "weg"; BaselineFlagColor = None; BaselineFlagName = None })
                    ev UserInBb (EntryReviewMarkWriteBackFailed { Reason = "weg" })
                    ev UserInBb (EntryDeletedInBb deletedPayload)
                    ev UserInBb (EntryRestoredInBb { OldYnabTransactionId = "ynab-alt"; NewYnabTransactionId = "ynab-neu"; NewImportId = None })
                ]
                |> Option.get
            Expect.isNone entry.CategorizationWriteBackFailure "Kategorie/Split"
            Expect.isNone entry.MemoWriteBackFailure "Memo"
            Expect.isNone entry.AmountWriteBackFailure "Betrag"
            Expect.isNone entry.FlagWriteBackFailure "Flag"
            Expect.isNone entry.ReviewMarkWriteBackFailure "Prüfvermerk"
    ]

// ============================================
// Befehle (In-Memory-SQLite)
// ============================================

[<Tests>]
let deletionCommandTests =
    let testCase name body =
        testCase name (fun () ->
            global.Persistence.initializeDatabase ()
            initializeLedgerSchema ()
            body ())
    testSequenced
    <| testList "Buchung löschen und Rückgängig (Befehle, ledger-z6sya)" [
        testCase "deleteEntry ruft genau ein DELETE mit der aktuellen YNAB-Id und hält EntryDeletedInBb fest" <| fun () ->
            let account = freshAccount ()
            let ynabId = freshYnabId ()
            let entryId = seed (observed account ynabId (Some "BB:z6sya-1"))
            let before = balanceOf account
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            Expect.equal probe.Deletes [ ynabId ] "genau ein DELETE mit der YNAB-Id"
            let last = eventsOf entryId |> List.last
            Expect.equal last.Event.Actor UserInBb "Actor"
            Expect.equal
                last.Event.Body
                (EntryDeletedInBb { deletedPayload with YnabTransactionId = ynabId; ImportId = Some "BB:z6sya-1" })
                "Ereignis mit dem Stand vor dem Löschen"
            Expect.isTrue (entryOf entryId).DeletedInYnab "gilt als gelöscht"
            Expect.equal before -42.80m "vorher im Kontostand"
            Expect.equal (balanceOf account) 0m "zählt nicht mehr im Kontostand"
            Expect.equal probe.MirrorTriggers 1 "Spiegel angestoßen"

        testCase "deleteEntry nimmt nach einem Ersatz die aktuelle YNAB-Id" <| fun () ->
            let account = freshAccount ()
            let oldId = freshYnabId ()
            let entryId = seed (observed account oldId None)
            appendAndApply [
                { EntryId = entryId; OccurredAt = at; Actor = UserInBb
                  Body = EntryReplacedInYnab { OldYnabTransactionId = oldId; NewYnabTransactionId = "ynab-ersatz"; NewImportId = None } }
            ]
            |> Async.RunSynchronously
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            Expect.equal probe.Deletes [ "ynab-ersatz" ] "die aktuelle Id"

        testCase "bei halbem Split-Ersatz geht kein DELETE an die in YNAB schon gelöschte Id, das Löschen gelingt" <| fun () ->
            // Bug (Review ledger-z6sya): beim Ersatz einer Split-Buchung war das DELETE gelungen,
            // das POST nicht (`YnabDeletedTransactionId` am offenen Fehlschlag). Das Löschen schickte
            // trotzdem ein DELETE an die schon gelöschte Id und scheiterte dann an YNABs 404.
            let account = freshAccount ()
            let ynabId = freshYnabId ()
            let splits = [ line food -20m; line drug -22.80m ]
            let entryId = seed { observed account ynabId None with Category = None; Splits = splits }
            appendAndApply [
                { EntryId = entryId; OccurredAt = at; Actor = UserInBb
                  Body =
                    EntryCategorizationWriteBackFailed {
                        Reason = "POST gescheitert"; BaselineCategory = None; BaselineSplits = splits
                        YnabDeletedTransactionId = Some ynabId; BaselineTransferAccountId = None } }
            ]
            |> Async.RunSynchronously
            let probe = Probe(DeleteResult = Error (YnabError.InvalidResponse "404 schon gelöscht"))
            delete probe entryId |> expectOk "Löschen"
            Expect.isEmpty probe.Deletes "kein DELETE"
            match (eventsOf entryId |> List.last).Event.Body with
            | EntryDeletedInBb p -> Expect.equal p.YnabTransactionId ynabId "gelöschte Id im Ereignis"
            | other -> failtest $"EntryDeletedInBb erwartet, war {eventTypeName other}"
            Expect.isTrue (entryOf entryId).DeletedInYnab "gilt als gelöscht"
            Expect.equal probe.MirrorTriggers 1 "Spiegel angestoßen"
            // Rückgängig legt den BB-Stand genau einmal an und beendet den alten Fehlschlag.
            restore probe entryId |> expectOk "Wiederherstellen"
            Expect.equal (List.exactlyOne probe.Creates).Splits splits "Neuanlage mit den BB-Zeilen"
            Expect.isNone (entryOf entryId).CategorizationWriteBackFailure "Fehlschlag beendet"

        testCase "scheitert das DELETE, gibt es kein Ereignis und die Buchung bleibt unverändert" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            let before = eventsOf entryId |> List.length
            let entryBefore = entryOf entryId
            let probe = Probe(DeleteResult = Error (YnabError.NetworkError "Zeitüberschreitung"))
            match delete probe entryId with
            | Error reason -> Expect.stringContains reason "Zeitüberschreitung" "Grund im Fehler"
            | Ok () -> failtest "Fehler erwartet"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"
            Expect.equal (entryOf entryId) entryBefore "unverändert"
            Expect.equal probe.MirrorTriggers 0 "kein Spiegel"

        testCase "abgeglichen: abgelehnt ohne Aufruf und ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let entryId = seed { observed account (freshYnabId ()) None with Cleared = Reconciled }
            let before = eventsOf entryId |> List.length
            let probe = Probe()
            Expect.equal (delete probe entryId) (Error entryDeleteReconciledText) "abgelehnt"
            Expect.isEmpty probe.Deletes "kein DELETE"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"

        testCase "schon gelöscht: abgelehnt ohne Aufruf und ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            appendAndApply [ { EntryId = entryId; OccurredAt = at; Actor = YnabMirror; Body = EntryDeletionObservedInYnab } ]
            |> Async.RunSynchronously
            let before = eventsOf entryId |> List.length
            let probe = Probe()
            Expect.equal (delete probe entryId) (Error entryDeleteDeletedText) "abgelehnt"
            Expect.isEmpty probe.Deletes "kein DELETE"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"

        testCase "ohne YNAB-Verknüpfung (Noch nicht in YNAB): abgelehnt ohne Aufruf und ohne Ereignis" <| fun () ->
            let account = freshAccount ()
            let entryId = newEntryId ()
            let input : CashExpenseInput =
                { AccountId = account.Ledger; Date = DateTime(2026, 11, 9); Amount = eur -3m; Payee = Some "Kiosk"; Memo = None; Category = None }
            addCashExpense UserInBb at entryId (fun () -> async.Return (Error (YnabError.NetworkError "weg"))) ignore input
            |> Async.RunSynchronously
            |> ignore
            let before = eventsOf entryId |> List.length
            let probe = Probe()
            Expect.equal (delete probe entryId) (Error entryDeleteNotLinkedText) "abgelehnt"
            Expect.isEmpty probe.Deletes "kein DELETE"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"

        testCase "restoreDeletedEntry legt eine einfache Buchung mit allen Feldern und Ersatz-import_id neu an" <| fun () ->
            let account = freshAccount ()
            let oldId = freshYnabId ()
            let entryId = seed (observed account oldId (Some "BB:z6sya-2"))
            let probe = Probe(CreateResult = Some (Ok "ynab-wieder-1"))
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            let expected : Ynab.SplitReplacement = {
                AccountId = account.Ynab
                Date = DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -42.80m
                Payee = Some "Rewe Markt"
                Memo = Some "Wocheneinkauf"
                FlagColor = Some "purple"
                Cleared = "cleared"
                Approved = true
                ImportId = Some "BB:z6sya-2:r1"
                CategoryId = Some food.CategoryId
                Splits = []
                TransferAccountId = None
            }
            Expect.equal probe.Creates [ expected ] "POST mit allen Feldern"
            let entry = entryOf entryId
            Expect.isFalse entry.DeletedInYnab "nicht mehr gelöscht"
            Expect.equal (balanceOf account) -42.80m "zählt wieder im Kontostand"
            let last = eventsOf entryId |> List.last
            Expect.equal last.Event.Actor UserInBb "Actor"
            Expect.equal
                last.Event.Body
                (EntryRestoredInBb { OldYnabTransactionId = oldId; NewYnabTransactionId = "ynab-wieder-1"; NewImportId = Some "BB:z6sya-2:r1" })
                "Ereignis"
            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal (currentYnabTransactionId links) (Some "ynab-wieder-1") "an die neue YNAB-Id verknüpft"
            Expect.equal (findEntryByLink YnabSupersededSystem oldId |> Async.RunSynchronously) (Some entryId) "alte Id ynabsuperseded"
            Expect.equal (currentImportId links) (Some "BB:z6sya-2:r1") "Ersatz-import_id verknüpft"
            Expect.equal probe.MirrorTriggers 2 "Spiegel nach beiden Befehlen"

        testCase "restoreDeletedEntry ohne import_id legt ohne import_id an" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            Expect.isNone (List.exactlyOne probe.Creates).ImportId "ohne import_id"

        testCase "eine lange import_id einer Barbuchung wird zur gekürzten Ersatz-import_id" <| fun () ->
            let account = freshAccount ()
            let longId = "BB:" + Guid.NewGuid().ToString("N")
            let entryId = seed (observed account (freshYnabId ()) (Some longId))
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            let sent = (List.exactlyOne probe.Creates).ImportId |> Option.get
            Expect.equal sent (replacementImportId (Some longId) |> Option.get) "wie replacementImportId"
            Expect.isLessThanOrEqual sent.Length ImportIdMaxLength "höchstens 36 Zeichen"

        testCase "Wiederherstellen einer aufgeteilten Buchung schickt die Split-Zeilen samt Übertrags-Zeile" <| fun () ->
            let account = freshAccount ()
            let cash = YnabAccountId (Guid.NewGuid())
            let splits = [ line food -20.00m; { Target = ToTransfer (cash, "Bargeld"); Amount = eur -22.80m; Memo = Some "Bar" } ]
            let entryId = seed { observed account (freshYnabId ()) None with Category = None; Splits = splits }
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            let sent = List.exactlyOne probe.Creates
            Expect.equal sent.Splits splits "Zeilen samt Übertrags-Zeile"
            Expect.isNone sent.CategoryId "keine Kategorie an einer Split-Buchung"
            Expect.isNone sent.TransferAccountId "kein Übertrags-Ziel auf Buchungsebene"

        testCase "Wiederherstellen eines Übertrags schickt das Übertrags-Ziel" <| fun () ->
            let account = freshAccount ()
            let target = YnabAccountId (Guid.NewGuid())
            let entryId =
                seed { observed account (freshYnabId ()) None with
                        Category = None; Payee = Some "Transfer : Tagesgeld"; TransferAccountId = Some target }
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            let sent = List.exactlyOne probe.Creates
            Expect.equal sent.TransferAccountId (Some target) "Übertrags-Ziel"
            Expect.isNone sent.CategoryId "ohne Kategorie"

        testCase "scheitert der POST beim Wiederherstellen, bleibt die Buchung gelöscht, kein Ereignis" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            let probe = Probe(CreateResult = Some (Error (YnabError.NetworkError "Zeitüberschreitung")))
            delete probe entryId |> expectOk "Löschen"
            let before = eventsOf entryId |> List.length
            match restore probe entryId with
            | Error reason -> Expect.stringContains reason "Zeitüberschreitung" "Grund im Fehler"
            | Ok () -> failtest "Fehler erwartet"
            Expect.isTrue (entryOf entryId).DeletedInYnab "bleibt gelöscht"
            Expect.equal (eventsOf entryId |> List.length) before "kein Ereignis"

        testCase "eine in YNAB gelöschte Buchung lässt sich nicht wiederherstellen, ohne Aufruf" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            appendAndApply [ { EntryId = entryId; OccurredAt = at; Actor = YnabMirror; Body = EntryDeletionObservedInYnab } ]
            |> Async.RunSynchronously
            let probe = Probe()
            Expect.equal (restore probe entryId) (Error entryRestoreNotDeletedInBbText) "abgelehnt"
            Expect.isEmpty probe.Creates "kein POST"

        testCase "eine schon wiederhergestellte Buchung wird nicht ein zweites Mal angelegt" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            Expect.equal (restore probe entryId) (Error entryRestoreNotDeletedInBbText) "abgelehnt"
            Expect.equal probe.Creates.Length 1 "kein zweiter POST"
    ]

// ============================================
// Schreib-Sperre gegen den Spiegel-Lauf (ADR 0055 Punkt 6, ledger-kfd5m)
// ============================================
//
// Ohne Sperre sähe ein Spiegel-Lauf zwischen DELETE und Ereignis die Löschung in YNAB und
// schriebe `EntryDeletionObservedInYnab` (Actor `YnabMirror`) statt des BB-Ereignisses, bzw. zwischen
// POST und Ereignis die neue YNAB-Buchung ohne Partner und legte eine zweite Buchung an.

[<Tests>]
let deletionWriteGuardTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    let waitsForMirror (entryId: EntryId) (probe: Probe) (command: Async<'a>) : 'a =
        let before = eventsOf entryId |> List.map (fun r -> r.EventType)
        LedgerWriteGuardProbe.commandWaitsForMirror
            (fun () ->
                Expect.equal (eventsOf entryId |> List.map (fun r -> r.EventType)) before "noch kein Ereignis während des Laufs"
                Expect.isEmpty probe.Deletes "noch kein DELETE"
                Expect.isEmpty probe.Creates "noch kein POST")
            command

    testSequenced
    <| testList "Buchung löschen: Schreib-Sperre" [
        testCase "Löschen während eines Spiegel-Laufs ruft DELETE und schreibt EntryDeletedInBb erst nach dem Lauf" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            let probe = Probe()
            waitsForMirror entryId probe (deleteEntry at probe.Delete probe.Trigger entryId) |> expectOk "Löschen"
            Expect.equal probe.Deletes.Length 1 "ein DELETE nach dem Lauf"
            Expect.equal (eventsOf entryId |> List.last).EventType "EntryDeletedInBb" "Ereignis nach dem Lauf"

        testCase "Wiederherstellen während eines Spiegel-Laufs ruft POST und schreibt EntryRestoredInBb erst nach dem Lauf" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account (freshYnabId ()) None)
            delete (Probe()) entryId |> expectOk "Löschen"
            let probe = Probe()
            waitsForMirror entryId probe (restoreDeletedEntry at probe.Create probe.Trigger entryId) |> expectOk "Wiederherstellen"
            Expect.equal probe.Creates.Length 1 "ein POST nach dem Lauf"
            Expect.equal (eventsOf entryId |> List.last).EventType "EntryRestoredInBb" "Ereignis nach dem Lauf"
    ]

// ============================================
// Spiegel nach Löschen und Wiederherstellen
// ============================================

[<Tests>]
let deletionMirrorTests =
    let testCase name body =
        testCase name (fun () ->
            global.Persistence.initializeDatabase ()
            initializeLedgerSchema ()
            body ())
    let deletion (account: Account) (ynabId: string) : YnabObservation = {
        YnabTransactionId = ynabId
        AccountId = account.Ynab
        ImportId = None
        Date = DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -42.80m
        Payee = Some "Rewe Markt"
        Memo = Some "Wocheneinkauf"
        Category = Some food
        Splits = []
        SplitTransfers = []
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Deleted = true
        Approved = true
        FlagColor = Some "purple"
        FlagName = None
    }
    let runWith (account: Account) (observations: YnabObservation list) =
        let fetchers : MirrorFetchers = {
            emptyMirrorFetchers with
                FetchAccounts =
                    fun _ ->
                        async { return Ok { Accounts = [ { AccountId = account.Ynab; Name = "Girokonto"; Closed = false; Deleted = false } ]; ServerKnowledge = 1L } }
                FetchTransactions = fun _ -> async { return Ok { Observations = observations; ServerKnowledge = 1L } }
        }
        match runMirror at fetchers (YnabBudgetId "budget-z6sya") |> Async.RunSynchronously with
        | Ok summary -> summary
        | Error e -> failtest $"Spiegel sollte gelingen, war: {e}"
    testSequenced
    <| testList "Buchung löschen: Spiegel (ledger-z6sya)" [
        testCase "nach EntryDeletedInBb ändert die Beobachtung der Löschung den Zustand nicht" <| fun () ->
            let account = freshAccount ()
            let ynabId = freshYnabId ()
            let entryId = seed (observed account ynabId None)
            delete (Probe()) entryId |> expectOk "Löschen"
            let stateBefore = entryOf entryId
            let eventsBefore = eventsOf entryId |> List.length
            let summary = runWith account [ deletion account ynabId ]
            Expect.equal summary.DeletedEntries 0 "keine neue Löschung"
            Expect.equal (eventsOf entryId |> List.length) eventsBefore "kein Ereignis"
            Expect.equal (entryOf entryId) stateBefore "Zustand gleich"

        testCase "nach dem Wiederherstellen setzt die Löschung der alten Id DeletedInYnab nicht" <| fun () ->
            let account = freshAccount ()
            let oldId = freshYnabId ()
            let entryId = seed (observed account oldId None)
            let probe = Probe()
            delete probe entryId |> expectOk "Löschen"
            restore probe entryId |> expectOk "Wiederherstellen"
            runWith account [ deletion account oldId ] |> ignore
            Expect.isFalse (entryOf entryId).DeletedInYnab "DeletedInYnab bleibt aus"
            Expect.isFalse
                (eventsOf entryId |> List.exists (fun r -> r.Event.Body = EntryDeletionObservedInYnab))
                "kein Lösch-Ereignis"
    ]

// ============================================
// API-Wege mit Fake-Ports
// ============================================

[<Tests>]
let deletionApiTests =
    let ports (calls: ResizeArray<string>) : Server.Api.EntryDeletionPorts = {
        Setting = fun key -> async.Return (Map.tryFind key (Map [ "ynab_token", "tok"; "ynab_default_budget_id", "bud" ]))
        Delete = fun token (YnabBudgetId budget) id -> async { calls.Add $"DELETE {token} {budget} {id}"; return Ok () }
        Create = fun token (YnabBudgetId budget) _ -> async { calls.Add $"POST {token} {budget}"; return Ok "ynab-api-neu" }
        TriggerMirror = ignore
    }
    let testCase name body =
        testCase name (fun () ->
            global.Persistence.initializeDatabase ()
            initializeLedgerSchema ()
            body ())
    testSequenced
    <| testList "Buchung löschen: API (ledger-z6sya)" [
        testCase "deleteEntryWith und restoreDeletedEntryWith reichen Token und Budget aus den Settings weiter" <| fun () ->
            let account = freshAccount ()
            let entryId = seed (observed account "ynab-api-alt" None)
            let (EntryId id) = entryId
            let calls = ResizeArray()
            Server.Api.deleteEntryWith (ports calls) at id |> Async.RunSynchronously |> expectOk "Löschen"
            Server.Api.restoreDeletedEntryWith (ports calls) at id |> Async.RunSynchronously |> expectOk "Wiederherstellen"
            Expect.equal (List.ofSeq calls) [ "DELETE tok bud ynab-api-alt"; "POST tok bud" ] "Aufrufe"

        testCase "ohne YNAB-Einrichtung: Fehler ohne Aufruf" <| fun () ->
            let calls = ResizeArray()
            let noSettings = { ports calls with Setting = fun _ -> async.Return None }
            match Server.Api.deleteEntryWith noSettings at (Guid.NewGuid()) |> Async.RunSynchronously with
            | Error reason -> Expect.equal reason Server.Api.bookingYnabMissingText "Satz"
            | Ok () -> failtest "Fehler erwartet"
            Expect.isEmpty calls "kein Aufruf"
    ]

// ============================================
// JSON-Vertrag der Neuanlage eines Übertrags
// ============================================

[<Tests>]
let restoreBodyTests =
    let target = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000e6b1")
    let payeeGuid = Guid.Parse "0195c000-0000-7000-8000-00000000e6c1"
    let request : Ynab.SplitReplacement = {
        AccountId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000e6d1")
        Date = DateTime(2026, 11, 3)
        Amount = eur -500m
        Payee = Some "Transfer : Tagesgeld"
        Memo = None
        FlagColor = None
        Cleared = "cleared"
        Approved = true
        ImportId = None
        CategoryId = None
        Splits = []
        TransferAccountId = Some target
    }
    testList "Buchung löschen: Neuanlage eines Übertrags (ledger-z6sya)" [
        testCase "ein Übertrag geht mit der Übertrags-Payee als payee_id hinaus, ohne payee_name" <| fun () ->
            match Ynab.buildReplacementBody (Map [ target, YnabPayeeId payeeGuid ]) request with
            | Error _ -> failtest "Body erwartet"
            | Ok json ->
                let tx = Decode.fromString (Decode.field "transaction" Decode.value) json |> Result.toOption |> Option.get
                Expect.equal (Decode.fromValue "$" (Decode.field "payee_id" Decode.guid) tx) (Ok payeeGuid) "payee_id"
                Expect.isError (Decode.fromValue "$" (Decode.field "payee_name" Decode.string) tx) "kein payee_name"
                Expect.equal (Decode.fromValue "$" (Decode.field "amount" Decode.int) tx) (Ok -500000) "Milliunits als Zahl"

        testCase "ohne Übertrags-Payee für das Ziel: Fehler mit dem Zielkonto" <| fun () ->
            Expect.equal (Ynab.buildReplacementBody Map.empty request) (Error target) "Zielkonto"
    ]
