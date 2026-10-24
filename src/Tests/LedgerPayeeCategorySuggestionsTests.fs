module LedgerPayeeCategorySuggestionsTests

// Vorschläge zum Payee im Kategorie-Picker (ledger-sfab0, ADR 0055 Nachtrag):
// `getPayeeCategorySuggestions` über die Projektion. Eigene Fixture-Ids (Guids mit Präfix
// 5f0a0a0a, Payee-Namen mit Suffix Spc), damit parallele Tasks nicht kollidieren.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.QueryHandlers

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountGuid (n: int) = Guid.Parse $"5f0a0a0a-0000-7000-8000-0000000001{n:D2}"
let private entryGuid (n: int) = Guid.Parse $"5f0a0a0a-0000-7000-8000-0000000002{n:D2}"
let private categoryOf (n: int) = YnabCategoryId (Guid.Parse $"5f0a0a0a-0000-7000-8000-0000000003{n:D2}")

let private freshAccount (n: int) : LedgerAccountId =
    let acc = LedgerAccountId (accountGuid n)
    saveAccount { Id = acc; Name = $"Konto {n} Spc"; Kind = BankAccount; Link = BbOwned; Closed = false }
    |> Async.RunSynchronously
    acc

/// Legt eine Buchung an: Nummer, Payee, Kategorie (oder keine), Datum.
let private seed (acc: LedgerAccountId) (n: int) (payee: string) (category: int option) (date: DateTime) : unit =
    let payload : EntryObservedInYnabPayload = {
        AccountId = acc
        YnabTransactionId = $"spc-{n}"
        ImportId = None
        Date = date
        Amount = eur -10m
        Payee = Some payee
        Memo = None
        Category =
            category |> Option.map (fun c -> { CategoryId = categoryOf c; CategoryName = $"Kategorie {c} Spc" })
        Splits = []
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [ { EntryId = EntryId (entryGuid n); OccurredAt = date; Actor = YnabMirror; Body = EntryObservedInYnab payload } ]
    |> Async.RunSynchronously

let private markDeleted (n: int) : unit =
    appendAndApply
        [ { EntryId = EntryId (entryGuid n)
            OccurredAt = DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
            Actor = YnabMirror
            Body = EntryDeletionObservedInYnab } ]
    |> Async.RunSynchronously

let private day (d: int) = DateTime(2026, 9, d, 0, 0, 0, DateTimeKind.Utc)

let private suggest (payee: string) (current: YnabCategoryId option) =
    getPayeeCategorySuggestions payee current |> Async.RunSynchronously

[<Tests>]
let tests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Vorschläge zum Payee (ledger-sfab0)" [
        testCase "häufigste Kategorie zuerst, höchstens drei" <| fun () ->
            let acc = freshAccount 1
            // K1 dreimal, K2 zweimal, K3 und K4 je einmal
            seed acc 1 "HaeufigSpc" (Some 1) (day 1)
            seed acc 2 "HaeufigSpc" (Some 1) (day 2)
            seed acc 3 "HaeufigSpc" (Some 1) (day 3)
            seed acc 4 "HaeufigSpc" (Some 2) (day 4)
            seed acc 5 "HaeufigSpc" (Some 2) (day 5)
            seed acc 6 "HaeufigSpc" (Some 3) (day 6)
            seed acc 7 "HaeufigSpc" (Some 4) (day 7)
            let result = suggest "HaeufigSpc" None
            Expect.equal (List.length result) 3 "höchstens drei"
            Expect.equal (List.take 2 result) [ categoryOf 1; categoryOf 2 ] "die häufigsten zuerst"
            Expect.equal (List.item 2 result) (categoryOf 4) "bei Gleichstand (K3 und K4 je einmal) gewinnt die jüngste Buchung"

        testCase "die aktuelle Kategorie ist ausgeschlossen und der Platz rückt nach" <| fun () ->
            let acc = freshAccount 2
            seed acc 11 "AusSpc" (Some 1) (day 1)
            seed acc 12 "AusSpc" (Some 1) (day 2)
            seed acc 13 "AusSpc" (Some 2) (day 3)
            seed acc 14 "AusSpc" (Some 3) (day 4)
            seed acc 15 "AusSpc" (Some 4) (day 5)
            let result = suggest "AusSpc" (Some (categoryOf 1))
            Expect.isFalse (List.contains (categoryOf 1) result) "die aktuelle Kategorie fehlt"
            Expect.equal result [ categoryOf 4; categoryOf 3; categoryOf 2 ] "drei andere, bei Gleichstand die jüngste zuerst"

        testCase "in YNAB gelöschte und kategorielose Buchungen zählen nicht" <| fun () ->
            let acc = freshAccount 3
            seed acc 21 "ZaehltSpc" (Some 1) (day 1)
            seed acc 22 "ZaehltSpc" (Some 2) (day 2)
            seed acc 23 "ZaehltSpc" (Some 2) (day 3)
            markDeleted 22
            markDeleted 23
            seed acc 24 "ZaehltSpc" None (day 4)
            seed acc 25 "ZaehltSpc" None (day 5)
            seed acc 26 "ZaehltSpc" None (day 6)
            Expect.equal (suggest "ZaehltSpc" None) [ categoryOf 1 ] "nur die lebende Buchung mit Kategorie zählt"

        testCase "der Payee wird ohne Beachtung der Groß- und Kleinschreibung gefunden" <| fun () ->
            let acc = freshAccount 4
            seed acc 31 "REWE Spc" (Some 1) (day 1)
            seed acc 32 "rewe spc" (Some 2) (day 2)
            seed acc 33 "Anderer Spc" (Some 3) (day 3)
            let result = suggest "Rewe SPC" None
            Expect.equal (Set.ofList result) (Set.ofList [ categoryOf 1; categoryOf 2 ]) "beide Schreibweisen, nicht der andere Payee"

        testCase "ein leerer Payee und ein unbekannter Payee ergeben keine Vorschläge" <| fun () ->
            Expect.isEmpty (suggest "" None) "leer"
            Expect.isEmpty (suggest "   " None) "nur Leerzeichen"
            Expect.isEmpty (suggest "GibtEsNichtSpc" None) "unbekannt"
    ]
