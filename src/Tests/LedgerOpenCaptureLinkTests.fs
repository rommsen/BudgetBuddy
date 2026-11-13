module LedgerOpenCaptureLinkTests

// Dritte Match-Stufe des Spiegels (ledger-ay81j, ADR 0015 und 0021 Nachtrag 2026-11-13), rein: eine in BB
// erfasste, offene Buchung wird mit einer YNAB-Beobachtung ohne import_id verknüpft, wenn Konto, Tag, Betrag
// und Payee gleich sind (bei einem Übertrag das Zielkonto) und das Paar im Lauf eindeutig ist. Je Kriterium
// ein Treffer und ein Gegenfall, Mehrdeutigkeit in beide Richtungen. Alle Daten sind erfunden.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Server.YnabObservations

let private ledgerAccount = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a8101")
let private ynabAccount = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a8102")
let private otherLedgerAccount = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a8103")
let private otherYnabAccount = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a8104")
let private target = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000a8105")
let private resolution = Map [ ynabAccount, ledgerAccount; otherYnabAccount, otherLedgerAccount ]
let private day = DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc)

let private capture (payee: string option) : Entry = {
    Id = newEntryId ()
    AccountId = ledgerAccount
    Date = day
    Amount = { Amount = -12.50m; Currency = "EUR" }
    Payee = payee
    Memo = Some "Brötchen"
    Category = None
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
    AmountWriteBackFailure = None
    PayeeWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = Some { Reason = "Zeitüberschreitung"; FailedAt = day }
    PayeeBeforeTransfer = None
    PossibleDuplicate = None; PendingReplacement = None; MergedIntoEntryId = None
}

let private observation (ynabId: string) (payee: string option) : YnabObservation = {
    YnabTransactionId = ynabId
    AccountId = ynabAccount
    ImportId = None
    Date = day.AddHours 15.0
    Amount = { Amount = -12.50m; Currency = "EUR" }
    Payee = payee
    Memo = Some "anderes Memo"
    Category = Some { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = "Kaffee" }
    Splits = []
    SplitTransfers = []
    Cleared = Uncleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    MatchedYnabTransactionId = None
    Deleted = false
    Approved = false
    FlagColor = None
    FlagName = None
}

let private plan observations captures = planOpenCaptureLinks observations resolution Set.empty captures

let private linksOf observations captures = (plan observations captures).Links

/// Kein Treffer: die Beobachtung passt nicht zur Erfassung.
let private expectNoLink (label: string) (obs: YnabObservation) (entry: Entry) =
    Expect.isEmpty (linksOf [ obs ] [ entry ]) label

[<Tests>]
let openCaptureLinkTests =
    testList "Ledger offene Erfassung verknüpfen (rein, ledger-ay81j)" [
        testCase "Treffer: gleiches Konto, Tag, Betrag und Payee; Kategorie und Memo zählen nicht" <| fun () ->
            let entry = capture (Some "Kiosk")
            Expect.equal (linksOf [ observation "y-1" (Some "Kiosk") ] [ entry ]) (Map [ "y-1", entry.Id ]) "verknüpft"

        testCase "Payee ohne Rücksicht auf Groß- und Kleinschreibung und Leerraum, leer gleich keiner" <| fun () ->
            let entry = capture (Some "Kiosk")
            Expect.equal (linksOf [ observation "y-1" (Some "  KIOSK ") ] [ entry ]) (Map [ "y-1", entry.Id ]) "normalisiert gleich"
            let none = capture None
            Expect.equal (linksOf [ observation "y-2" (Some "   ") ] [ none ]) (Map [ "y-2", none.Id ]) "leer gleich kein Payee"

        testCase "Gegenfall Payee: ein anderer Payee verknüpft nicht" <| fun () ->
            expectNoLink "anderer Payee" (observation "y-1" (Some "Bäcker")) (capture (Some "Kiosk"))
            expectNoLink "kein Payee gegen einen" (observation "y-1" None) (capture (Some "Kiosk"))

        testCase "Gegenfall Konto: ein anderes Konto verknüpft nicht, ein unbekanntes auch nicht" <| fun () ->
            let entry = capture (Some "Kiosk")
            expectNoLink "anderes Konto" { observation "y-1" (Some "Kiosk") with AccountId = otherYnabAccount } entry
            expectNoLink "unbekanntes Konto" { observation "y-1" (Some "Kiosk") with AccountId = YnabAccountId (Guid.NewGuid()) } entry

        testCase "Gegenfall Tag: ein anderer Kalendertag verknüpft nicht" <| fun () ->
            expectNoLink "Folgetag" { observation "y-1" (Some "Kiosk") with Date = day.AddDays 1.0 } (capture (Some "Kiosk"))

        testCase "Gegenfall Betrag: ein anderer Betrag verknüpft nicht, die Währung zählt nicht" <| fun () ->
            let entry = capture (Some "Kiosk")
            expectNoLink "anderer Betrag" { observation "y-1" (Some "Kiosk") with Amount = { Amount = -12.51m; Currency = "EUR" } } entry
            Expect.equal
                (linksOf [ { observation "y-1" (Some "Kiosk") with Amount = { Amount = -12.50m; Currency = "XXX" } } ] [ entry ])
                (Map [ "y-1", entry.Id ])
                "ohne Vergleich der Währung"

        testCase "Übertrag: das Zielkonto zählt statt des Payees" <| fun () ->
            let entry = { capture (Some "Transfer : Tagesgeld") with TransferAccountId = Some target }
            let obs = { observation "y-1" (Some "Transfer : Sparen") with TransferAccountId = Some target }
            Expect.equal (linksOf [ obs ] [ entry ]) (Map [ "y-1", entry.Id ]) "gleiches Ziel, Payee egal"
            expectNoLink "anderes Ziel" { obs with TransferAccountId = Some otherYnabAccount } entry
            expectNoLink "Beobachtung ohne Übertrag" { obs with TransferAccountId = None; Payee = entry.Payee } entry
            expectNoLink "Erfassung ohne Übertrag" obs { entry with TransferAccountId = None; Payee = obs.Payee }

        testCase "Gegenfall Beobachtung: gelöscht, mit import_id, mit Abgleich oder mit bekannter YNAB-Id verknüpft nicht" <| fun () ->
            let entry = capture (Some "Kiosk")
            let obs = observation "y-1" (Some "Kiosk")
            expectNoLink "gelöscht" { obs with Deleted = true } entry
            expectNoLink "mit import_id" { obs with ImportId = Some "YNAB:-12500:2026-11-12:1" } entry
            expectNoLink "mit matched_transaction_id" { obs with MatchedYnabTransactionId = Some "y-alt" } entry
            Expect.isEmpty (planOpenCaptureLinks [ obs ] resolution (Set.ofList [ "y-1" ]) [ entry ]).Links "YNAB-Id als ynab oder ynabsuperseded bekannt"

        testCase "Mehrdeutig: zwei passende Erfassungen zu einer Beobachtung, keine Verknüpfung" <| fun () ->
            let first, second = capture (Some "Kiosk"), capture (Some "kiosk")
            let result = plan [ observation "y-1" (Some "Kiosk") ] [ first; second ]
            Expect.isEmpty result.Links "keine Verknüpfung"
            Expect.equal result.ObservationsWithManyCaptures [ "y-1" ] "die Beobachtung für die Log-Zeile"

        testCase "Mehrdeutig: zwei passende Beobachtungen zu einer Erfassung, keine Verknüpfung" <| fun () ->
            let entry = capture (Some "Kiosk")
            let result = plan [ observation "y-1" (Some "Kiosk"); observation "y-2" (Some "Kiosk") ] [ entry ]
            Expect.isEmpty result.Links "keine Verknüpfung"
            Expect.equal result.CapturesWithManyObservations [ entry.Id ] "die Buchung für die Log-Zeile"

        testCase "eindeutige Paare im selben Lauf werden je für sich verknüpft" <| fun () ->
            let kiosk, baecker = capture (Some "Kiosk"), capture (Some "Bäcker")
            Expect.equal
                (linksOf [ observation "y-1" (Some "Kiosk"); observation "y-2" (Some "Bäcker") ] [ kiosk; baecker ])
                (Map [ "y-1", kiosk.Id; "y-2", baecker.Id ])
                "zwei Paare"

        testCase "isCapturedInBb: erstes Ereignis CashExpenseAdded, jeder Actor; sonst nicht" <| fun () ->
            let added : LedgerEventBody =
                CashExpenseAdded {
                    AccountId = ledgerAccount; Date = day; Amount = { Amount = -1m; Currency = "EUR" }; Payee = None; Memo = None
                    Category = None; Cleared = Uncleared; Approved = false; ImportId = None }
            for actor in [ QuickAdd; Bob; UserInBb ] do
                Expect.isTrue (isCapturedInBb (Some (actor, added))) $"{actor}"
            Expect.isFalse (isCapturedInBb None) "unbekannte Buchung"
            Expect.isFalse (isCapturedInBb (Some (YnabMirror, EntryLinkedToYnab { YnabTransactionId = "y" }))) "anderes erstes Ereignis"

        testCase "isOpenCapture: verknüpft, gelöscht oder als Duplikat gehalten zählt nicht, ein Fehlschlag schon" <| fun () ->
            let entry = capture (Some "Kiosk")
            Expect.isTrue (isOpenCapture entry) "offen mit Fehlschlag"
            Expect.isFalse (isOpenCapture { entry with LinkedToYnab = true }) "verknüpft"
            Expect.isFalse (isOpenCapture { entry with DeletedInYnab = true }) "gelöscht"
            let held : EntryHeldAsPossibleDuplicatePayload =
                { Reason = "gleicher Betrag"; MatchYnabTransactionId = "y-treffer"; MatchEntryId = None
                  Match = { Date = day; Amount = entry.Amount; Payee = entry.Payee; CategoryName = None } }
            Expect.isFalse (isOpenCapture { entry with PossibleDuplicate = Some held }) "als Duplikat gehalten"
    ]
