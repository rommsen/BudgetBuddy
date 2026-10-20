module LedgerSortSheetTests

// ledger-skpcx: Sortier-Chip und Sheet am Handy. Reine Funktionen und der Reducer ohne
// DOM. Chip und Sheet im echten Browser prüft `scripts/ledger-sort-chip.test.mjs`.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.State

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readSource (parts: string list) =
    File.ReadAllText(Path.Combine(Array.ofList (repoRoot :: parts)))

let private accountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"

let private baseModel () : Model =
    let model, _ = init ()
    { model with
        SelectedFilter = Some AllAccounts
        NextPage = 3
        LocallyRemoved = 2
        Selection = Picked (Set.singleton (Guid.Parse "0195f900-0000-7000-8000-000000000001"))
        Filters = { noFilters with Payee = Some "rewe" } }

let private dispatched (cmd: Elmish.Cmd<Msg>) : Msg list =
    let sink = ResizeArray<Msg>()
    cmd |> List.iter (fun sub -> sub sink.Add)
    List.ofSeq sink

let private step (msg: Msg) (model: Model) =
    let model', cmd, _ = update msg model
    model', dispatched cmd

let private asc field = { Field = field; Direction = SortAsc }
let private desc field = { Field = field; Direction = SortDesc }

[<Tests>]
let sortSheetTests =
    testList "Sortier-Chip und Sheet am Handy (ledger-skpcx)" [

        test "sortOptions true liefert zehn Optionen in fester Reihenfolge" {
            let options = sortOptions true
            Expect.equal
                options
                [ desc SortByDate, "Neueste zuerst"
                  asc SortByDate, "Älteste zuerst"
                  asc SortByAmount, "Größte Ausgabe zuerst"
                  desc SortByAmount, "Größte Einnahme zuerst"
                  asc SortByPayee, "A bis Z"
                  desc SortByPayee, "Z bis A"
                  asc SortByCategory, "A bis Z"
                  desc SortByCategory, "Z bis A"
                  asc SortByAccount, "A bis Z"
                  desc SortByAccount, "Z bis A" ]
                "zehn Zeilen"
            Expect.equal (options |> List.map fst |> List.distinct |> List.length) 10 "Kombinationen eindeutig"
        }

        test "sortOptions false liefert acht Optionen ohne Konto" {
            let options = sortOptions false
            Expect.equal options.Length 8 "acht Zeilen"
            Expect.isFalse (options |> List.exists (fun (s, _) -> s.Field = SortByAccount)) "kein Konto"
            Expect.equal (options |> List.map fst |> List.distinct |> List.length) 8 "Kombinationen eindeutig"
        }

        test "sortChipLabel: Standard Datum ↓, Betrag aufsteigend Betrag ↑" {
            Expect.equal (sortChipLabel LedgerSort.defaultSort) "Datum ↓" "Standard"
            Expect.equal (sortChipLabel (asc SortByAmount)) "Betrag ↑" "Betrag"
            Expect.equal (sortChipLabel (desc SortByPayee)) "Payee ↓" "Payee"
        }

        test "Sortierung bleibt von Filtern unberührt: hasAnyFilter und ClearAllFilters" {
            let model = { baseModel () with Sort = asc SortByAmount }
            Expect.isTrue (hasAnyFilter model.Filters) "Payee-Filter zählt"
            Expect.isFalse (hasAnyFilter noFilters) "Sortierung ist kein Filter"
            let cleared, _ = step ClearAllFilters model
            Expect.equal cleared.Sort (asc SortByAmount) "Sort bleibt stehen"
        }

        test "Der Sortier-Chip steht nicht in den Filter-Chips" {
            let order = orderChipsForDisplay true (chipActivityOf false noFilters)
            Expect.equal order.Length 7 "die sieben Filter-Chips, kein Sortier-Chip"
        }

        test "SetSortExactly setzt die Sortierung genau, auch wenn sie gleich bleibt" {
            let before = { baseModel () with Sort = asc SortByAmount; SortSheetOpen = true }
            let after, msgs = step (SetSortExactly (asc SortByAmount)) before
            Expect.equal after.Sort (asc SortByAmount) "ohne Umschalten"
            Expect.equal after.NextPage 0 "Seite 0"
            Expect.equal after.LocallyRemoved 0 "Versatz zurück"
            Expect.equal after.Selection NoSelection "Auswahl leer"
            Expect.equal after.Filters before.Filters "Filter unverändert"
            Expect.equal after.SelectedFilter before.SelectedFilter "Konto unverändert"
            Expect.isFalse after.SortSheetOpen "Sheet geschlossen"
            Expect.equal msgs [ LoadEntries ] "LoadEntries"
        }

        test "SetSortExactly setzt Feld und Richtung zugleich" {
            let after, _ = step (SetSortExactly (desc SortByPayee)) (baseModel ())
            Expect.equal after.Sort (desc SortByPayee) "Payee absteigend"
        }

        test "OpenSortSheet und CloseSortSheet ändern Sort und Filter nicht" {
            let before = { baseModel () with Sort = asc SortByAmount }
            let opened, openMsgs = step OpenSortSheet before
            Expect.isTrue opened.SortSheetOpen "offen"
            Expect.equal opened.Sort before.Sort "Sort gleich"
            Expect.equal opened.Filters before.Filters "Filter gleich"
            Expect.equal opened.NextPage before.NextPage "keine Neuladung"
            Expect.isEmpty openMsgs "kein Befehl"
            let closed, closeMsgs = step CloseSortSheet opened
            Expect.isFalse closed.SortSheetOpen "zu"
            Expect.equal closed.Sort before.Sort "Sort gleich"
            Expect.isEmpty closeMsgs "kein Befehl"
        }

        test "Das Sheet startet geschlossen" {
            let model, _ = init ()
            Expect.isFalse model.SortSheetOpen "zu"
        }

        test "Der Chip steht nur bei CompactList und ist nur bei Abweichung vom Standard aktiv" {
            let view = readSource [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "(visibleColumns tableWidth isAllAccounts = CompactList)" "Chip hängt an visibleColumns"
            Expect.stringContains view "IsActive = sort <> LedgerSort.defaultSort" "aktiv nur bei Abweichung"
            Expect.stringContains view "OnRemove = None" "kein Kreuz"
        }

        test "BottomSheet.optionRow existiert und steht im Styleguide" {
            let sheet = readSource [ "src"; "Client"; "DesignSystem"; "BottomSheet.fs" ]
            Expect.stringContains sheet "let optionRow (label: string) (isSelected: bool) (onClick: unit -> unit)" "Signatur"
            Expect.stringContains sheet "min-h-[44px]" "44px Trefferfläche"
            let guide = readSource [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            Expect.stringContains guide "BottomSheet.optionRow \"Neueste zuerst\" true" "gewählter Zustand"
            Expect.stringContains guide "BottomSheet.optionRow \"Älteste zuerst\" false" "ungewählter Zustand"
        }
    ]
