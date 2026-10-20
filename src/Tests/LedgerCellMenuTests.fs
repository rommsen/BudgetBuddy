module LedgerCellMenuTests

// ledger-rfq5p: Zellmenü "Nach X filtern" an der Payee- und Kategorie-Zelle einer Zeile.
// Reine Beschriftungsfunktionen, die Id der Kategorie im Lesepfad und die Reduzierer-Pfade.
// Eigene Fixture-Ids (Präfix 0195c1f0).

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.QueryHandlers
open Components.Ledger.Types
open Components.Ledger.View

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private categoryGuid = Guid.Parse "0195c1f0-0000-7000-8000-000000000001"
let private otherCategoryGuid = Guid.Parse "0195c1f0-0000-7000-8000-000000000002"

let private view (payee: string option) (categoryId: Guid option) : LedgerEntryView = {
    Id = Guid.Parse "0195c1f0-0000-7000-8000-000000000100"
    AccountId = Guid.Parse "0195c1f0-0000-7000-8000-000000000200"
    Date = DateTime(2026, 10, 1)
    Amount = eur -10m
    Payee = payee
    Memo = None
    CategoryName = categoryId |> Option.map (fun _ -> "Lebensmittel")
    CategoryId = categoryId
    CategoryGroupName = categoryId |> Option.map (fun _ -> "Alltag")
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = false
    Flag = NoFlag
    IsTransfer = false
    ReviewMarkWriteBackFailure = None
    LinkedToYnab = true
    PushToYnabFailure = None
}

let private split : TransactionSplit =
    { Target = ToCategory (YnabCategoryId categoryGuid, "Lebensmittel"); Amount = eur -5m; Memo = None }

let private freshModel () : Model =
    let model, _ = Components.Ledger.State.init ()
    model

let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private update msg model = Components.Ledger.State.update msg model

let private storedEntry : Entry = {
    Id = EntryId (Guid.Parse "0195c1f0-0000-7000-8000-000000000100")
    AccountId = LedgerAccountId (Guid.Parse "0195c1f0-0000-7000-8000-000000000200")
    Date = DateTime(2026, 10, 1)
    Amount = eur -10m
    Payee = Some "REWE"
    Memo = None
    Category = Some { CategoryId = YnabCategoryId categoryGuid; CategoryName = "Lebensmittel" }
    Splits = []
    Cleared = Uncleared
    TransferEntryId = None
    TransferAccountId = None
    SplitTransferCounterparts = Map.empty
    DeletedInYnab = false
    Approved = true
    FlagColor = None
    FlagName = None
    ReviewMarkWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
}

let private repoRoot =
    let rec up (dir: IO.DirectoryInfo) =
        if IO.File.Exists(IO.Path.Combine(dir.FullName, "BudgetBuddy.sln")) || IO.Directory.Exists(IO.Path.Combine(dir.FullName, "src", "Client"))
        then dir.FullName
        else up dir.Parent
    up (IO.DirectoryInfo AppContext.BaseDirectory)

let private source (relative: string) = IO.File.ReadAllText(IO.Path.Combine(repoRoot, relative))

[<Tests>]
let cellMenuTests =
    testList "Zellmenü Nach X filtern (ledger-rfq5p)" [

        testList "Beschriftung der Einträge" [
            test "payeeMenuLabel nennt den Payee" {
                Expect.equal (payeeMenuLabel (view (Some "REWE") None)) (Some "Nach REWE filtern") "Eintrag mit Payee"
            }
            test "payeeMenuLabel liefert None ohne Payee, bei leerem Text und bei Leerzeichen" {
                for payee in [ None; Some ""; Some "   " ] do
                    Expect.isNone (payeeMenuLabel (view payee None)) $"kein Eintrag für {payee}"
            }
            test "categoryMenuLabel nimmt den Text der PC-Zelle" {
                let entry = view (Some "REWE") (Some categoryGuid)
                Expect.equal (categoryMenuLabel entry) (Some "Nach Alltag: Lebensmittel filtern") "voll qualifiziert"
                Expect.equal (categoryMenuLabel entry) (Some $"Nach {categoryLabelQualified entry} filtern") "gleicht der Zelle"
            }
            test "categoryMenuLabel liefert None bei Buchung ohne Kategorie" {
                Expect.isNone (categoryMenuLabel (view (Some "REWE") None)) "keine CategoryId"
            }
            test "categoryMenuLabel liefert None bei Split, auch mit einer einzigen Kategorie (ADR 0038)" {
                let entry = { view (Some "REWE") None with Splits = [ split ] }
                Expect.isNone (categoryMenuLabel entry) "Split zeigt keine einzelne Kategorie"
                let withId = { view (Some "REWE") (Some categoryGuid) with Splits = [ split ] }
                Expect.isNone (categoryMenuLabel withId) "auch mit gesetzter Id"
            }
            test "categoryMenuLabel liefert None bei Übertrag ohne Kategorie" {
                let entry = { view (Some "Umbuchung") None with IsTransfer = true }
                Expect.isNone (categoryMenuLabel entry) "ein Übertrag ohne Kategorie hat nichts zu filtern"
            }
        ]

        testList "Lesepfad" [
            test "toEntryView füllt CategoryId aus der Kategorie der Buchung" {
                let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab storedEntry
                Expect.equal view.CategoryId (Some categoryGuid) "die Id der Kategorie, nicht ein Nachschlagen über den Namen"
            }
            test "toEntryView lässt CategoryId bei Buchung ohne Kategorie und bei Split leer" {
                let ohne = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab { storedEntry with Category = None }
                Expect.isNone ohne.CategoryId "ohne Kategorie"
                let aufgeteilt =
                    ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab
                        { storedEntry with Category = None; Splits = [ split ] }
                Expect.isNone aufgeteilt.CategoryId "Split"
            }
        ]

        testList "Reduzierer" [
            let filtered =
                { (freshModel ()) with
                    Filters =
                        { noFilters with
                            CategoryIds = Set.ofList [ otherCategoryGuid; Guid.NewGuid() ]
                            DateRange = Some { Quick = Some ThisYear; From = Some (DateTime(2026, 1, 1)); To = None }
                            Payee = Some "Alt"
                            Memo = Some "Kuchen"
                            AmountMin = Some 5m
                            WithoutCategory = true }
                    OnlyNeedsReview = true
                    SelectedFilter = Some (OneAccount (Guid.Parse "0195c1f0-0000-7000-8000-000000000200"))
                    NextPage = 3
                    Selection = Picked (Set.ofList [ Guid.NewGuid() ])
                    CellMenu = Some { EntryId = Guid.NewGuid(); Kind = PayeeCell }
                    HistoryFor = Some (view (Some "REWE") None) }

            test "FilterByRowPayee setzt nur den Payee, schließt Menü und Historie und lädt neu" {
                let updated, cmd, _ = update (FilterByRowPayee "REWE") filtered
                Expect.equal updated.Filters.Payee (Some "REWE") "Payee gesetzt"
                Expect.equal updated.Filters.CategoryIds filtered.Filters.CategoryIds "Kategorien bleiben"
                Expect.equal updated.Filters.DateRange filtered.Filters.DateRange "Zeitraum bleibt"
                Expect.equal updated.Filters.Memo filtered.Filters.Memo "Memo bleibt"
                Expect.equal updated.Filters.AmountMin filtered.Filters.AmountMin "Betrag bleibt"
                Expect.isTrue updated.Filters.WithoutCategory "Ohne Kategorie bleibt"
                Expect.isTrue updated.OnlyNeedsReview "Zu prüfen bleibt"
                Expect.equal updated.SelectedFilter filtered.SelectedFilter "Konto bleibt"
                Expect.isNone updated.CellMenu "Menü zu"
                Expect.isNone updated.HistoryFor "Historie zu"
                Expect.contains (dispatched cmd) LoadEntries "lädt neu"
            }

            test "FilterByRowCategory ersetzt die Auswahl, schaltet Ohne Kategorie aus und lässt den Rest stehen" {
                let updated, cmd, _ = update (FilterByRowCategory categoryGuid) filtered
                Expect.equal updated.Filters.CategoryIds (Set.singleton categoryGuid) "genau diese Id"
                Expect.isFalse updated.Filters.WithoutCategory "Ohne Kategorie aus (ADR 0048)"
                Expect.equal updated.Filters.DateRange filtered.Filters.DateRange "Zeitraum bleibt"
                Expect.equal updated.Filters.AmountMin filtered.Filters.AmountMin "Betrag bleibt"
                Expect.equal updated.Filters.Memo filtered.Filters.Memo "Memo bleibt"
                Expect.equal updated.Filters.Payee (Some "Alt") "Payee bleibt"
                Expect.isTrue updated.OnlyNeedsReview "Zu prüfen bleibt"
                Expect.equal updated.SelectedFilter filtered.SelectedFilter "Konto bleibt"
                Expect.isNone updated.CellMenu "Menü zu"
                Expect.isNone updated.HistoryFor "Historie zu"
                Expect.contains (dispatched cmd) LoadEntries "lädt neu"
            }

            test "Beide Filter-Nachrichten leeren die Auswahl und setzen die Seite auf 0" {
                for msg in [ FilterByRowPayee "REWE"; FilterByRowCategory categoryGuid ] do
                    let updated, _, _ = update msg filtered
                    Expect.equal updated.NextPage 0 $"Seite 0 nach {msg}"
                    Expect.equal updated.Selection NoSelection $"Auswahl leer nach {msg}"
            }

            test "Nach FilterByRowPayee trägt die Seitenanfrage den Payee" {
                let updated, _, _ = update (FilterByRowPayee "REWE") filtered
                let query = Components.Ledger.State.buildPageQuery updated None 0
                Expect.equal query.Payee (Some "REWE") "Anfrage mit Payee"
                Expect.equal query.Page 0 "ab Seite 0"
            }

            test "Nach FilterByRowCategory trägt die Seitenanfrage genau die Kategorie ohne Ohne-Kategorie" {
                let updated, _, _ = update (FilterByRowCategory categoryGuid) filtered
                let query = Components.Ledger.State.buildPageQuery updated None 0
                Expect.equal query.CategoryIds [ categoryGuid ] "Anfrage mit einer Kategorie"
                Expect.isFalse query.WithoutCategory "ohne Ohne-Kategorie"
            }

            test "CloseCellMenu schließt das Menü und ändert Filter und Payee nicht" {
                let updated, cmd, _ = update CloseCellMenu filtered
                Expect.isNone updated.CellMenu "Menü zu"
                Expect.equal updated.Filters filtered.Filters "Filter unverändert"
                Expect.equal updated.Filters.Payee (Some "Alt") "Payee unverändert"
                Expect.isEmpty (dispatched cmd) "kein Neuladen"
            }

            test "OpenCellMenu schließt einen offenen Chip-Popover" {
                let model = { filtered with CellMenu = None; OpenPopover = Some PayeeChip }
                let id = Guid.NewGuid()
                let updated, _, _ = update (OpenCellMenu (id, CategoryCell)) model
                Expect.equal updated.CellMenu (Some { EntryId = id; Kind = CategoryCell }) "Menü offen"
                Expect.isNone updated.OpenPopover "Popover zu"
            }

            test "OpenFilterPopover schließt das Zellmenü" {
                let updated, _, _ = update (OpenFilterPopover CategoryChip) filtered
                Expect.isNone updated.CellMenu "Zellmenü zu"
                Expect.equal updated.OpenPopover (Some CategoryChip) "Popover offen"
            }
        ]

        testList "Oberfläche (Quelltext)" [
            test "Der Auslöser stoppt die Weitergabe, damit die Zeile nicht die Historie öffnet" {
                let text = source "src/Client/Components/Ledger/View.fs"
                let start = text.IndexOf "let private cellMenuTrigger"
                Expect.isGreaterThan start 0 "cellMenuTrigger vorhanden"
                let body = text.Substring(start, 900)
                Expect.stringContains body "e.stopPropagation ()" "stopPropagation im Klick"
                Expect.stringContains body "aria-haspopup" "Menü angekündigt"
                Expect.stringContains body "aria-expanded" "Zustand angekündigt"
            }
            test "Der erste Menüeintrag nimmt den Fokus, damit Esc im Popover greift" {
                let text = source "src/Client/DesignSystem/FilterPopover.fs"
                Expect.stringContains text "prop.autoFocus true" "autoFocus"
                Expect.stringContains text "prop.role \"menuitem\"" "role menuitem"
                Expect.stringContains text "menuWidthClass = \"w-[min(92vw,240px)]\"" "Menübreite"
            }
            test "Der Handy-Block der Aktionen steht im historySheet vor der Historie" {
                let text = source "src/Client/Components/Ledger/View.fs"
                let sheet = text.IndexOf "let private historySheet"
                let actions = text.IndexOf("row-filter-actions", sheet)
                let history = text.IndexOf("| NotAsked -> Html.none", sheet)
                Expect.isGreaterThan actions sheet "Aktionsblock im Sheet"
                Expect.isLessThan actions history "Aktionen vor der Historie"
            }
            test "Der Styleguide zeigt eine Karte mit menuItem im Popover" {
                let text = source "src/Client/Components/Styleguide/View.fs"
                Expect.stringContains text "FilterPopover.menuItem" "menuItem in der Karte"
                Expect.stringContains text "components-filterpopover-menuitem" "eigene Karte"
            }
        ]
    ]
