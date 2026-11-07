module LedgerFilterButtonTests

// ledger-q1hw2 (ADR 0053): die Filter-Knöpfe am Payee und an der Kategorie im Detail lösen
// das Zellmenü aus ledger-rfq5p ab. Die Beschriftungen und der Lesepfad stammen aus
// ledger-rfq5p; neu sind die Beschriftung der Knöpfe, der Rückbau des Zellmenüs und die
// Reduzierer-Pfade, die das Detail offen lassen. Eigene Fixture-Ids (Präfix 0195c1f0).

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.QueryHandlers
open Components.Ledger.Types
open Components.Ledger.View
open Types

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
    MemoWriteBackFailure = None
    LinkedToYnab = true
    PushToYnabFailure = None
    SplitLocks = None
    ExternalLinks = []
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
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
}

[<Tests>]
let filterButtonTests =
    testList "Filter-Knöpfe im Detail (ledger-q1hw2, ADR 0053)" [

        testList "Beschriftung der Knöpfe" [
            test "Der Payee-Knopf trägt den vollen Namen" {
                Expect.equal
                    (payeeFilterLabel (view (Some "Stadtwerke Musterstadt GmbH") None))
                    (Some "Nur Buchungen von Stadtwerke Musterstadt GmbH zeigen")
                    "voller Name, keine Kürzung"
            }
            test "Der Payee-Knopf fehlt ohne Payee, bei leerem Text und bei Leerzeichen" {
                for payee in [ None; Some ""; Some "   " ] do
                    Expect.isNone (payeeFilterLabel (view payee None)) $"kein Knopf für {payee}"
            }
            test "Der Kategorie-Knopf trägt die voll qualifizierte Kategorie" {
                let entry = view (Some "REWE") (Some categoryGuid)
                Expect.equal (categoryFilterLabel entry) (Some "Nur Buchungen in Alltag: Lebensmittel zeigen") "voller Name"
            }
            test "Der Kategorie-Knopf fehlt bei Split, ohne Kategorie und beim Übertrag ohne Kategorie" {
                Expect.isNone (categoryFilterLabel (view (Some "REWE") None)) "ohne Kategorie"
                Expect.isNone
                    (categoryFilterLabel { view (Some "REWE") (Some categoryGuid) with Splits = [ split ] })
                    "Split"
                Expect.isNone
                    (categoryFilterLabel { view (Some "Umbuchung") None with IsTransfer = true })
                    "Übertrag ohne Kategorie"
            }
            test "Auch eine in YNAB gelöschte Buchung bekommt beide Knöpfe (Filtern ändert die Buchung nicht)" {
                let entry = { view (Some "REWE") (Some categoryGuid) with DeletedInYnab = true }
                Expect.isSome (payeeFilterLabel entry) "Payee"
                Expect.isSome (categoryFilterLabel entry) "Kategorie"
            }
        ]

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
            // ledger-rjhwp: die Links kommen aus Payee und dem AKTUELLEN Memo der Buchung
            // (ein in BB bearbeitetes Memo ist die Grundlage, nicht das ursprüngliche).
            test "toEntryView füllt ExternalLinks aus Payee und aktuellem Memo" {
                let amazon = { storedEntry with Payee = Some "AMAZON PAYMENTS"; Memo = Some "01305-304-1234567-7654321 Lampe" }
                let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab amazon
                Expect.equal
                    (view.ExternalLinks |> List.map (fun l -> l.Label))
                    [ "Bestellung 304-1234567-7654321" ]
                    "Bestellnummer aus dem Memo"
                Expect.stringContains view.ExternalLinks.Head.Url "orderID=304-1234567-7654321" "Deep-Link mit Id"
            }
            test "toEntryView: ein bearbeitetes Memo ersetzt die Grundlage der Erkennung" {
                let before = { storedEntry with Payee = Some "AMAZON PAYMENTS"; Memo = Some "304-1234567-7654321" }
                let after = { before with Memo = Some "Geschenk" }
                let linksOf e = (ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e).ExternalLinks |> List.map (fun l -> l.Label)
                Expect.equal (linksOf before) [ "Bestellung 304-1234567-7654321" ] "vorher: Deep-Link"
                Expect.equal (linksOf after) [ "Amazon Orders" ] "nachher: nur die Bestellübersicht"
            }
            test "toEntryView: Buchung ohne Treffer und ohne Memo hat keine Links" {
                let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab { storedEntry with Memo = None }
                Expect.isEmpty view.ExternalLinks "REWE ist weder Amazon noch PayPal"
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
                    DetailFor = Some (view (Some "REWE") None)
                    History = Success [] }

            test "FilterByRowPayee setzt nur den Payee, lässt das Detail offen und lädt neu" {
                let updated, cmd, _ = update (FilterByRowPayee "REWE") filtered
                Expect.equal updated.Filters.Payee (Some "REWE") "Payee gesetzt"
                Expect.equal updated.Filters.CategoryIds filtered.Filters.CategoryIds "Kategorien bleiben"
                Expect.equal updated.Filters.DateRange filtered.Filters.DateRange "Zeitraum bleibt"
                Expect.equal updated.Filters.Memo filtered.Filters.Memo "Memo bleibt"
                Expect.equal updated.Filters.AmountMin filtered.Filters.AmountMin "Betrag bleibt"
                Expect.isTrue updated.Filters.WithoutCategory "Ohne Kategorie bleibt"
                Expect.isTrue updated.OnlyNeedsReview "Zu prüfen bleibt"
                Expect.equal updated.SelectedFilter filtered.SelectedFilter "Konto bleibt"
                Expect.equal updated.DetailFor filtered.DetailFor "Detail bleibt offen, dieselbe Buchung"
                Expect.equal updated.History filtered.History "Historie bleibt stehen"
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
                Expect.equal updated.DetailFor filtered.DetailFor "Detail bleibt offen, dieselbe Buchung"
                Expect.equal updated.History filtered.History "Historie bleibt stehen"
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
        ]
    ]
