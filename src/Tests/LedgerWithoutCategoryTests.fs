module LedgerWithoutCategoryTests

// Chip "Ohne Kategorie" (ledger-saqsk, ADR 0048): Domain (Projektion des rohen
// Übertrags-Kontos, Split-Übertrags-Konten), Persistence (Spalte, Indextabelle,
// Filterklausel, Backfill) und Client-Zustand. Eigene Fixture-Ids (Guids mit Präfix
// 5a5a5a5a, Namen mit Suffix Sqk), damit parallele Tasks nicht kollidieren.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.QueryHandlers
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountGuid (n: int) = Guid.Parse $"5a5a5a5a-0000-7000-8000-0000000001{n:D2}"
let private accountOf (n: int) = LedgerAccountId (accountGuid n)
let private entryGuid (n: int) = Guid.Parse $"5a5a5a5a-0000-7000-8000-0000000002{n:D2}"
let private entryIdOf (n: int) = EntryId (entryGuid n)
let private ynabCategory (n: int) = YnabCategoryId (Guid.Parse $"5a5a5a5a-0000-7000-8000-0000000003{n:D2}")
let private ynabAccount (n: int) = YnabAccountId (Guid.Parse $"5a5a5a5a-0000-7000-8000-0000000004{n:D2}")

let private payload (accountId: LedgerAccountId) (n: int) : EntryObservedInYnabPayload = {
    AccountId = accountId
    YnabTransactionId = $"sqk-{n}"
    ImportId = None
    Date = DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -10m
    Payee = Some "PayeeSqk"
    Memo = None
    Category = None
    Splits = []
    Cleared = Cleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Approved = true
    FlagColor = None
    FlagName = None
}

let private observedEvent (n: int) (p: EntryObservedInYnabPayload) : LedgerEvent = {
    EntryId = entryIdOf n
    OccurredAt = p.Date
    Actor = YnabMirror
    Body = EntryObservedInYnab p
}

let private split (target: SplitTarget) (amount: decimal) : TransactionSplit =
    { Target = target; Amount = eur amount; Memo = None }

let private toCategory (n: int) = ToCategory (ynabCategory n, $"Kategorie {n} Sqk")
let private toTransfer (n: int) = ToTransfer (ynabAccount n, $"Konto {n} Sqk")

let private append (n: int) (p: EntryObservedInYnabPayload) : unit =
    appendAndApply [ observedEvent n p ] |> Async.RunSynchronously

let private appendDeleted (n: int) (p: EntryObservedInYnabPayload) : unit =
    append n p
    appendAndApply [ { EntryId = entryIdOf n; OccurredAt = p.Date; Actor = YnabMirror; Body = EntryDeletionObservedInYnab } ]
    |> Async.RunSynchronously

let private splitPayload (acc: LedgerAccountId) (n: int) (splits: TransactionSplit list) =
    { payload acc n with
        Splits = splits
        Amount = eur (splits |> List.sumBy (fun s -> s.Amount.Amount)) }

let private baseQuery (acc: LedgerAccountId) : LedgerEntryPageQuery =
    let (LedgerAccountId raw) = acc
    {
        AccountId = Some raw
        Page = 0
        PageSize = 100
        LocallyRemoved = 0
        IncludeDeleted = false
        OnlyNeedsReview = false
        WithoutCategory = false
        CategoryIds = []
        DateFrom = None
        DateTo = None
        Payee = None
        Memo = None
        AmountMin = None
        AmountMax = None
    }

let private withoutCategoryIds (acc: LedgerAccountId) (query: LedgerEntryPageQuery) : Set<Guid> =
    let page = getLedgerEntriesPage query |> Async.RunSynchronously
    page.Entries |> List.map (fun e -> e.Id) |> Set.ofList

let private freshAccount (n: int) : LedgerAccountId =
    let acc = accountOf n
    saveAccount { Id = acc; Name = $"Konto {n} Sqk"; Kind = BankAccount; Link = BbOwned; Closed = false }
    |> Async.RunSynchronously
    acc

let private scalar (sql: string) (id: Guid) : obj =
    use conn = new Microsoft.Data.Sqlite.SqliteConnection(Persistence.activeConnectionString ())
    conn.Open()
    use cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn)
    cmd.Parameters.AddWithValue("@id", id.ToString()) |> ignore
    cmd.ExecuteScalar()

// ============================================
// Domain
// ============================================

[<Tests>]
let domainTests =
    let entryWith (splits: TransactionSplit list) : Entry =
        apply None (observedEvent 1 { payload (accountOf 1) 1 with Splits = splits })

    testList "Ledger Domain (Ohne Kategorie, ADR 0048)" [

        testCase "splitTransferAccountIds liefert die entdoppelten Konto-Ids der ToTransfer-Zeilen" <| fun () ->
            let e =
                entryWith [
                    split (toTransfer 1) -3m
                    split (toTransfer 1) -2m
                    split (toTransfer 2) -1m
                    split (toCategory 1) -4m
                    split Uncategorized -1m ]
            let (YnabAccountId a1) = ynabAccount 1
            let (YnabAccountId a2) = ynabAccount 2
            Expect.equal (splitTransferAccountIds e) [ a1; a2 ] "genau die zwei verschiedenen Zielkonten"

        testCase "splitTransferAccountIds ist leer ohne ToTransfer-Zeile" <| fun () ->
            Expect.isEmpty (splitTransferAccountIds (entryWith [ split (toCategory 1) -5m; split Uncategorized -5m ])) "keine Übertrags-Zeile"
            Expect.isEmpty (splitTransferAccountIds (entryWith [])) "keine Splits"

        testCase "apply übernimmt TransferAccountId aus EntryObservedInYnab, alle übrigen Felder unverändert" <| fun () ->
            let p = { payload (accountOf 1) 2 with TransferAccountId = Some (ynabAccount 3) }
            let e = apply None (observedEvent 2 p)
            let expected : Entry = {
                Id = entryIdOf 2
                AccountId = p.AccountId
                Date = p.Date
                Amount = p.Amount
                Payee = p.Payee
                Memo = p.Memo
                Category = p.Category
                Splits = p.Splits
                Cleared = p.Cleared
                TransferEntryId = None
                TransferAccountId = Some (ynabAccount 3)
                SplitTransferCounterparts = Map.empty
                DeletedInYnab = false
                Approved = p.Approved
                FlagColor = p.FlagColor
                FlagName = p.FlagName
                ReviewMarkWriteBackFailure = None
            }
            Expect.equal e expected "Feld für Feld"

        testCase "ein späteres Ereignis (Löschung) ändert TransferAccountId nicht" <| fun () ->
            let p = { payload (accountOf 1) 3 with TransferAccountId = Some (ynabAccount 3) }
            let e = apply None (observedEvent 3 p)
            let deleted =
                apply (Some e) { EntryId = entryIdOf 3; OccurredAt = p.Date; Actor = YnabMirror; Body = EntryDeletionObservedInYnab }
            Expect.equal deleted.TransferAccountId (Some (ynabAccount 3)) "bleibt erhalten"
    ]

// ============================================
// Persistence
// ============================================

[<Tests>]
let persistenceTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            body ())

    testSequenced
    <| testList "Ledger Persistence (Ohne Kategorie, ADR 0048)" [

        testCase "transfer_account_id steht in der Spalte, rowToEntry liest denselben Wert (Replay)" <| fun () ->
            let acc = freshAccount 1
            append 10 { payload acc 10 with TransferAccountId = Some (ynabAccount 4) }
            let (YnabAccountId a) = ynabAccount 4
            let stored = scalar "SELECT transfer_account_id FROM ledger_entries WHERE id = @id" (entryGuid 10)
            Expect.equal (string stored) (a.ToString()) "Spalte gefüllt"
            let loaded = loadEntry (entryIdOf 10) |> Async.RunSynchronously
            Expect.equal (loaded |> Option.bind (fun e -> e.TransferAccountId)) (Some (ynabAccount 4)) "zurückgelesen"
            let replayed = replayAll () |> Async.RunSynchronously |> List.find (fun e -> e.Id = entryIdOf 10)
            Expect.equal replayed.TransferAccountId (Some (ynabAccount 4)) "Replay stimmt überein"

        testCase "ledger_entry_split_transfers: eine Zeile je Zielkonto, keine Dublette; rebuildSplitIndexes baut sie neu" <| fun () ->
            let acc = freshAccount 2
            append 11 (splitPayload acc 11 [ split (toTransfer 5) -3m; split (toTransfer 5) -2m; split Uncategorized -1m ])
            let (YnabAccountId a) = ynabAccount 5
            let count () =
                Convert.ToInt32(scalar "SELECT COUNT(*) FROM ledger_entry_split_transfers WHERE entry_id = @id" (entryGuid 11))
            Expect.equal (count ()) 1 "genau eine Zeile trotz zwei ToTransfer-Zeilen auf dasselbe Konto"
            Expect.equal
                (string (scalar "SELECT transfer_account_id FROM ledger_entry_split_transfers WHERE entry_id = @id" (entryGuid 11)))
                (a.ToString())
                "die Konto-Id"

            use conn = new Microsoft.Data.Sqlite.SqliteConnection(Persistence.activeConnectionString ())
            conn.Open()
            use clear = new Microsoft.Data.Sqlite.SqliteCommand("DELETE FROM ledger_entry_split_transfers", conn)
            clear.ExecuteNonQuery() |> ignore
            Expect.equal (count ()) 0 "gelöscht"
            rebuildSplitIndexes conn |> ignore
            Expect.equal (count ()) 1 "aus dem Bestand neu aufgebaut"

        testCase "Filterklausel: genau die Treffer der Definition (ein Fall je Zeile)" <| fun () ->
            let acc = freshAccount 3
            append 20 (payload acc 20)                                                            // Treffer
            append 21 { payload acc 21 with TransferAccountId = Some (ynabAccount 6) }            // Übertrag, roh
            append 22 (payload acc 22)
            appendAndApply [
                { EntryId = entryIdOf 22; OccurredAt = DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc); Actor = YnabMirror
                  Body = EntryTransferCounterpartObservedInYnab { CounterpartEntryId = entryIdOf 21; CounterpartYnabTransactionId = "sqk-21" } } ]
            |> Async.RunSynchronously                                                              // nur transfer_entry_id
            append 23 { payload acc 23 with Category = Some { CategoryId = ynabCategory 7; CategoryName = "K7 Sqk" } }
            append 24 (splitPayload acc 24 [ split Uncategorized -5m; split Uncategorized -5m ])  // Treffer
            append 25 (splitPayload acc 25 [ split (toCategory 8) -5m; split Uncategorized -5m ])
            append 26 (splitPayload acc 26 [ split (toTransfer 9) -5m; split Uncategorized -5m ])
            appendDeleted 27 (payload acc 27)

            let q = { baseQuery acc with WithoutCategory = true }
            Expect.equal (withoutCategoryIds acc q) (Set.ofList [ entryGuid 20; entryGuid 24 ])
                "einfach ohne Kategorie und komplett unkategorisierter Split; Übertrag, Verknüpfung, Kategorie, Teil-Splits und Gelöschte nicht"
            Expect.equal
                (withoutCategoryIds acc { q with IncludeDeleted = true })
                (Set.ofList [ entryGuid 20; entryGuid 24; entryGuid 27 ])
                "mit IncludeDeleted kommt die gelöschte Buchung ohne Kategorie dazu"

        testCase "WithoutCategory zusammen mit CategoryIds liefert die Vereinigung" <| fun () ->
            let acc = freshAccount 4
            append 30 { payload acc 30 with Category = Some { CategoryId = ynabCategory 10; CategoryName = "K10 Sqk" } } // nur Kategorie
            append 31 (payload acc 31)                                                                                   // nur ohne
            append 32 { payload acc 32 with Category = Some { CategoryId = ynabCategory 11; CategoryName = "K11 Sqk" } } // keines
            let (YnabCategoryId c10) = ynabCategory 10
            let q = { baseQuery acc with WithoutCategory = true; CategoryIds = [ c10 ] }
            Expect.equal (withoutCategoryIds acc q) (Set.ofList [ entryGuid 30; entryGuid 31 ]) "Vereinigung beider Mengen"
            Expect.equal
                (withoutCategoryIds acc { q with WithoutCategory = false })
                (Set.ofList [ entryGuid 30 ])
                "ohne den Chip nur die Kategorie"

        testCase "Zählwege und Payee-Vorschläge wenden WithoutCategory an" <| fun () ->
            let acc = freshAccount 5
            append 40 { payload acc 40 with Payee = Some "OhneSqk"; Cleared = Uncleared }        // Treffer, zu prüfen
            appendDeleted 41 { payload acc 41 with Payee = Some "OhneSqk" }                        // Treffer, gelöscht
            append 42 { payload acc 42 with Payee = Some "MitSqk"; Category = Some { CategoryId = ynabCategory 12; CategoryName = "K12 Sqk" }; Cleared = Uncleared }
            appendDeleted 43 { payload acc 43 with Payee = Some "MitSqk"; Category = Some { CategoryId = ynabCategory 12; CategoryName = "K12 Sqk" } }

            let off = getLedgerEntriesPage (baseQuery acc) |> Async.RunSynchronously
            let on = getLedgerEntriesPage { baseQuery acc with WithoutCategory = true } |> Async.RunSynchronously
            Expect.equal off.TotalCount 2 "ohne Chip: die zwei nicht gelöschten"
            Expect.equal on.TotalCount 1 "mit Chip: TotalCount (countEntries)"
            Expect.equal off.DeletedCount 2 "ohne Chip: zwei gelöschte"
            Expect.equal on.DeletedCount 1 "mit Chip: DeletedCount (countDeletedEntries)"
            Expect.equal off.TotalAmount (eur -20m) "ohne Chip: Summe über beide"
            Expect.equal on.TotalAmount (eur -10m) "mit Chip: Summe (loadAmountsForQuery)"
            // Der Zähler "Zu prüfen (n)" ist bewusst ein fester Zähler ohne Such-Filter (ADR 0032).
            Expect.equal on.NeedsReviewCount off.NeedsReviewCount "NeedsReviewCount ignoriert die Such-Filter, auch den Chip"

            let (LedgerAccountId raw) = acc
            let suggestionQuery wc : LedgerPayeeSuggestionQuery = {
                AccountId = Some raw
                IncludeDeleted = false
                OnlyNeedsReview = false
                WithoutCategory = wc
                CategoryIds = []
                DateFrom = None
                DateTo = None
                Memo = None
                AmountMin = None
                AmountMax = None
            }
            let names wc =
                getLedgerPayeeSuggestions (suggestionQuery wc) |> Async.RunSynchronously |> List.map (fun s -> s.Payee) |> Set.ofList
            Expect.equal (names false) (Set.ofList [ "OhneSqk"; "MitSqk" ]) "ohne Chip beide Payees"
            Expect.equal (names true) (Set.ofList [ "OhneSqk" ]) "mit Chip nur der Payee ohne Kategorie"

        testCase "Backfill: transfer_account_id wird einmalig aus dem Log nachgetragen, ein zweiter Lauf wiederholt ihn nicht" <| fun () ->
            use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:")
            conn.Open()
            let exec (sql: string) =
                use cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn)
                cmd.ExecuteNonQuery() |> ignore
            // Zustandstabelle und Log in der Form vor dieser Änderung (ohne die neue Spalte).
            exec "CREATE TABLE ledger_entries (id TEXT PRIMARY KEY, account_id TEXT NOT NULL, deleted_in_ynab INTEGER NOT NULL DEFAULT 0)"
            exec "CREATE TABLE ledger_entry_events (entry_id TEXT NOT NULL, sequence INTEGER NOT NULL, event_type TEXT NOT NULL, schema_version INTEGER NOT NULL, payload TEXT NOT NULL)"
            let p = { payload (accountOf 6) 50 with TransferAccountId = Some (ynabAccount 13) }
            let body = EntryObservedInYnab p
            let insertEvent (sql: string) =
                use cmd = new Microsoft.Data.Sqlite.SqliteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@id", (entryGuid 50).ToString()) |> ignore
                cmd.Parameters.AddWithValue("@version", schemaVersion body) |> ignore
                cmd.Parameters.AddWithValue("@payload", encodePayload body) |> ignore
                cmd.ExecuteNonQuery() |> ignore
            insertEvent "INSERT INTO ledger_entry_events VALUES (@id, 1, 'EntryObservedInYnab', @version, @payload)"
            exec $"INSERT INTO ledger_entries (id, account_id) VALUES ('{entryGuid 50}', 'konto')"

            let read () =
                use cmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT transfer_account_id FROM ledger_entries", conn)
                match cmd.ExecuteScalar() with
                | :? DBNull | null -> None
                | v -> Some (string v)

            migrateLedgerEntryColumns conn
            let (YnabAccountId a) = ynabAccount 13
            Expect.equal (read ()) (Some (a.ToString())) "Bestandsbuchung bekommt das rohe Übertrags-Konto aus dem Log"

            // Zweiter Start: Spalte existiert, der Backfill läuft nicht erneut.
            exec "UPDATE ledger_entries SET transfer_account_id = NULL"
            migrateLedgerEntryColumns conn
            Expect.equal (read ()) None "kein zweiter Backfill"
    ]

// ============================================
// Client
// ============================================

[<Tests>]
let clientTests =
    let model () = fst (Components.Ledger.State.init ())
    testList "Ledger Client (Chip Ohne Kategorie)" [

        testCase "ToggleWithoutCategory kippt Filters.WithoutCategory und lädt neu" <| fun () ->
            let m1, cmd1, _ = Components.Ledger.State.update ToggleWithoutCategory (model ())
            Expect.isTrue m1.Filters.WithoutCategory "an"
            Expect.isFalse (List.isEmpty cmd1) "lädt ab Seite 0 neu"
            let m2, _, _ = Components.Ledger.State.update ToggleWithoutCategory m1
            Expect.isFalse m2.Filters.WithoutCategory "aus"

        testCase "der Chip steht in fixedChipOrder hinter CategoryChip" <| fun () ->
            let order = fixedChipOrder
            Expect.equal (List.findIndex ((=) WithoutCategoryChip) order) (List.findIndex ((=) CategoryChip) order + 1) "direkt hinter Kategorie"

        testCase "am Handy wandert der aktive Chip nach vorn" <| fun () ->
            let activity = chipActivityOf false { noFilters with WithoutCategory = true }
            Expect.equal (List.head (orderChipsForDisplay true activity)) WithoutCategoryChip "aktiv zuerst"

        testCase "WithoutCategory zählt nicht zu hasAnyFilter, ClearAllFilters lässt ihn stehen" <| fun () ->
            let filters = { noFilters with WithoutCategory = true }
            Expect.isFalse (hasAnyFilter filters) "kein Teil von hasAnyFilter"
            let m = { model () with Filters = { filters with Payee = Some "x" } }
            let cleared, _, _ = Components.Ledger.State.update ClearAllFilters m
            Expect.isTrue cleared.Filters.WithoutCategory "bleibt"
            Expect.isNone cleared.Filters.Payee "die fünf Such-Filter sind zurückgesetzt"
    ]
