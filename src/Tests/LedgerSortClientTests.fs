module LedgerSortClientTests

// ledger-bn913 (ADR 0051): Sortierzustand im Client. Reine Funktionen und der Reducer ohne
// DOM. Die Kopfzellen im echten Browser prüft `scripts/ledger-sort-headers.test.mjs`.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.State
open Components.Ledger.View

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readSource (parts: string list) =
    File.ReadAllText(Path.Combine(Array.ofList (repoRoot :: parts)))

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"

let private entryOn (n: int) (date: DateTime) (amount: decimal) : LedgerEntryView = {
    Id = Guid.Parse $"0195f900-0000-7000-8000-0000000000{n:D2}"
    AccountId = accountId
    Date = date
    Amount = eur amount
    Payee = Some $"Payee {n}"
    Memo = None
    CategoryName = None
    CategoryId = None
    CategoryGroupName = None
    SplitCategoryGroupNames = Map.empty
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = false
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
}

let private baseModel () : Model =
    let model, _ = init ()
    { model with
        SelectedFilter = Some AllAccounts
        Entries = Success [ entryOn 1 (DateTime(2026, 10, 1)) -5m ]
        NextPage = 3
        LocallyRemoved = 2
        Selection = Picked (Set.singleton (Guid.Parse "0195f900-0000-7000-8000-000000000001"))
        Filters = { noFilters with Payee = Some "rewe" } }

/// Die Nachrichten, die ein Cmd abschickt.
let private dispatched (cmd: Elmish.Cmd<Msg>) : Msg list =
    let sink = ResizeArray<Msg>()
    cmd |> List.iter (fun sub -> sub sink.Add)
    List.ofSeq sink

let private step (msg: Msg) (model: Model) =
    let model', cmd, _ = update msg model
    model', dispatched cmd

[<Tests>]
let sortClientTests =
    testList "Sortierzustand im Client (ledger-bn913)" [

        test "Model.Sort startet mit defaultSort" {
            let model, _ = init ()
            Expect.equal model.Sort LedgerSort.defaultSort "Datum absteigend"
        }

        test "buildPageQuery trägt die wirksame Sortierung" {
            let model = { baseModel () with Sort = { Field = SortByAmount; Direction = SortAsc } }
            let query = buildPageQuery model None 0
            Expect.equal query.Sort { Field = SortByAmount; Direction = SortAsc } "Betrag aufsteigend"
        }

        test "Konto-Sortierung bei einem einzelnen Konto liefert defaultSort" {
            let model =
                { baseModel () with
                    Sort = { Field = SortByAccount; Direction = SortDesc }
                    SelectedFilter = Some (OneAccount accountId) }
            Expect.equal (effectiveSort model) LedgerSort.defaultSort "Spalte gibt es dort nicht"
            Expect.equal (buildPageQuery model (Some accountId) 0).Sort LedgerSort.defaultSort "Anfrage trägt defaultSort"
            Expect.equal model.Sort { Field = SortByAccount; Direction = SortDesc } "Modell behält die Wahl"
        }

        test "Konto-Sortierung in der Gesamtsicht bleibt wirksam" {
            let model = { baseModel () with Sort = { Field = SortByAccount; Direction = SortAsc } }
            Expect.equal (effectiveSort model) { Field = SortByAccount; Direction = SortAsc } "Gesamtsicht"
        }

        test "sortStartDirection: Datum absteigend, Betrag aufsteigend, Rest aufsteigend" {
            Expect.equal (sortStartDirection SortByDate) SortDesc "Datum"
            Expect.equal (sortStartDirection SortByAmount) SortAsc "Betrag"
            Expect.equal (sortStartDirection SortByPayee) SortAsc "Payee"
            Expect.equal (sortStartDirection SortByCategory) SortAsc "Kategorie"
            Expect.equal (sortStartDirection SortByAccount) SortAsc "Konto"
        }

        test "SetSort auf ein anderes Feld setzt Feld und Startrichtung und lädt neu" {
            let before = baseModel ()
            let after, msgs = step (SetSort SortByAmount) before
            Expect.equal after.Sort { Field = SortByAmount; Direction = SortAsc } "Betrag aufsteigend"
            Expect.equal after.NextPage 0 "Seite 0"
            Expect.equal after.LocallyRemoved 0 "Versatz zurück"
            Expect.equal after.Selection NoSelection "Auswahl leer"
            Expect.equal after.Filters before.Filters "Filter unverändert"
            Expect.equal after.SelectedFilter before.SelectedFilter "Konto unverändert"
            Expect.equal msgs [ LoadEntries ] "LoadEntries"
        }

        test "SetSort auf das aktive Feld dreht die Richtung" {
            let before = { baseModel () with Sort = { Field = SortByAmount; Direction = SortAsc } }
            let after, msgs = step (SetSort SortByAmount) before
            Expect.equal after.Sort { Field = SortByAmount; Direction = SortDesc } "gedreht"
            Expect.equal after.NextPage 0 "Seite 0"
            Expect.equal after.LocallyRemoved 0 "Versatz zurück"
            Expect.equal after.Selection NoSelection "Auswahl leer"
            Expect.equal after.Filters before.Filters "Filter unverändert"
            Expect.equal msgs [ LoadEntries ] "LoadEntries"
            let back, _ = step (SetSort SortByAmount) after
            Expect.equal back.Sort { Field = SortByAmount; Direction = SortAsc } "wieder aufsteigend"
        }

        test "Datum: Klick auf den Standard dreht auf aufsteigend" {
            let after, _ = step (SetSort SortByDate) (baseModel ())
            Expect.equal after.Sort { Field = SortByDate; Direction = SortAsc } "Datum aufsteigend"
        }

        test "Klick auf Datum bei einzelnem Konto mit Konto-Wahl dreht das sichtbare Datum" {
            let before =
                { baseModel () with
                    Sort = { Field = SortByAccount; Direction = SortAsc }
                    SelectedFilter = Some (OneAccount accountId) }
            let after, _ = step (SetSort SortByDate) before
            Expect.equal after.Sort { Field = SortByDate; Direction = SortAsc } "gedreht gegenüber dem gezeigten Datum absteigend"
        }

        test "Kontowechsel und Filterwechsel lassen Sort stehen" {
            let sort = { Field = SortByPayee; Direction = SortDesc }
            let before = { baseModel () with Sort = sort }
            let afterAccount, _ = step (SelectFilter (OneAccount accountId)) before
            Expect.equal afterAccount.Sort sort "SelectFilter"
            let afterToggle, _ = step ToggleOnlyNeedsReview before
            Expect.equal afterToggle.Sort sort "Chip"
            let afterClear, _ = step ClearAllFilters before
            Expect.equal afterClear.Sort sort "Alle Filter löschen"
        }

        test "usesDayGroups ist nur für Datum wahr, in beiden Richtungen" {
            Expect.isTrue (usesDayGroups { Field = SortByDate; Direction = SortDesc }) "Datum absteigend"
            Expect.isTrue (usesDayGroups { Field = SortByDate; Direction = SortAsc }) "Datum aufsteigend"
            for field in [ SortByAmount; SortByPayee; SortByCategory; SortByAccount ] do
                for dir in [ SortAsc; SortDesc ] do
                    Expect.isFalse (usesDayGroups { Field = field; Direction = dir }) $"{field} {dir}"
        }

        test "groupByDate gruppiert eine aufsteigend gelieferte Liste mit Tagessumme je Gruppe" {
            let d1, d2 = DateTime(2026, 10, 1), DateTime(2026, 10, 2)
            let entries = [ entryOn 1 d1 -10m; entryOn 2 d1 -5m; entryOn 3 d2 20m ]
            let groups = groupByDate entries
            Expect.equal (groups |> List.map fst) [ d1; d2 ] "aufsteigende Reihenfolge bleibt"
            Expect.equal (groups |> List.map (snd >> List.length)) [ 2; 1 ] "Größen"
            Expect.equal (groups |> List.map (fun (_, g) -> (daySum false g).Amount)) [ -15m; 20m ] "Tagessummen"
        }

        test "mobileRowDateText: flache Liste zeigt das Datum, gruppierte nicht" {
            let today = DateTime(2026, 10, 20)
            let entry = entryOn 1 (DateTime(2026, 10, 3)) -1m
            Expect.equal (mobileRowDateText today true entry) (Some "03.10.") "flach"
            Expect.equal (mobileRowDateText today true (entryOn 2 today -1m)) (Some "Heute") "Heute"
            Expect.equal (mobileRowDateText today false entry) None "gruppiert"
        }

        test "headerSortDirection: nur der aktive Kopf trägt eine Richtung" {
            let sort = { Field = SortByAmount; Direction = SortAsc }
            Expect.equal (headerSortDirection sort SortByAmount) Client.DesignSystem.Table.Ascending "aktiv aufsteigend"
            Expect.equal (headerSortDirection { sort with Direction = SortDesc } SortByAmount) Client.DesignSystem.Table.Descending "aktiv absteigend"
            Expect.equal (headerSortDirection sort SortByDate) Client.DesignSystem.Table.Unsorted "inaktiv"
        }

        test "aria-sort je Richtung" {
            Expect.equal (Client.DesignSystem.Table.ariaSortValue Client.DesignSystem.Table.Ascending) "ascending" "auf"
            Expect.equal (Client.DesignSystem.Table.ariaSortValue Client.DesignSystem.Table.Descending) "descending" "ab"
            Expect.equal (Client.DesignSystem.Table.ariaSortValue Client.DesignSystem.Table.Unsorted) "none" "keine"
        }

        test "Kopfzellen: sortierbare über die Sortier-Funktion, Memo und Status nicht" {
            let view = readSource [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "sortHeader \"Konto\" None SortByAccount" "Konto"
            Expect.stringContains view "sortHeader \"Payee\" None SortByPayee" "Payee"
            Expect.stringContains view "sortHeader \"Kategorie\" None SortByCategory" "Kategorie"
            Expect.stringContains view "sortHeader \"Datum\" None SortByDate" "Datum"
            Expect.stringContains view "sortHeader \"Betrag\" (Some \"right\") SortByAmount" "Betrag"
            Expect.stringContains view "Table.calmHeaderCell \"Memo\" None" "Memo bleibt einfach"
            Expect.stringContains view "Table.calmHeaderCell \"Status\" (Some \"right\")" "Status bleibt einfach"
            Expect.isFalse (view.Contains "sortHeader \"Memo\"") "Memo nicht sortierbar"
            Expect.isFalse (view.Contains "sortHeader \"Status\"") "Status nicht sortierbar"
        }

        test "Styleguide zeigt die sortierbare Kopfzelle in drei Zuständen" {
            let guide = readSource [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            Expect.stringContains guide "calmSortableHeaderCell \"Betrag aufsteigend\" None Table.Ascending" "aufsteigend"
            Expect.stringContains guide "Table.Descending" "absteigend"
            Expect.stringContains guide "Table.Unsorted" "ohne"
        }
    ]
