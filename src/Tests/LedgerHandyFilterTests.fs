module LedgerHandyFilterTests

// ledger-d9z3n: Kassenbuch unter 1024 px. Reine Funktionen und der Reducer ohne DOM: die gesetzten
// Filter als Chips (Text, Reihenfolge, „×“), „Filter löschen“, die Zahl am Filter-Knopf, das Filter-Sheet
// und die Suche. Das offene Sheet im Browser prüfen der Layout-Sweep und die Flows in
// scripts/ledger-filter-bar.test.mjs und scripts/ledger-account-switch.test.mjs.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.State

let private accountId = Guid.Parse "0195e9d0-0000-7000-8000-000000000001"
let private food = Guid.Parse "0195e9d0-0000-7000-8000-000000000011"
let private rent = Guid.Parse "0195e9d0-0000-7000-8000-000000000012"

let private category (id: Guid) (name: string) : YnabCategory =
    { Id = YnabCategoryId id; Name = name; GroupName = "Alltag"; Available = None; Hidden = false }

let private baseModel () : Model =
    let model, _ = init ()
    { model with
        SelectedFilter = Some (OneAccount accountId)
        Categories = Success [ category food "Lebensmittel"; category rent "Miete" ] }

/// Alle Filter gesetzt, die als Chip erscheinen, dazu eine Suche (die keinen Chip bekommt).
let private fullModel () : Model =
    { baseModel () with
        OnlyNeedsReview = true
        IncludeDeleted = true
        Sort = { Field = SortByAmount; Direction = SortAsc }
        SearchDraft = "rewe"
        Filters =
            { CategoryIds = Set.singleton food
              DateRange = Some { Quick = Some ThisMonth; From = Some (DateTime(2026, 11, 1)); To = Some (DateTime(2026, 11, 14)) }
              Search = Some "rewe"
              AmountMin = Some 5m
              AmountMax = Some 50m
              AmountDirection = OnlyOutflows
              WithoutCategory = true
              OnlyAskLena = true } }

let private dispatched (cmd: Elmish.Cmd<Msg>) : Msg list =
    let sink = ResizeArray<Msg>()
    cmd |> List.iter (fun sub -> sub sink.Add)
    List.ofSeq sink

let private step (msg: Msg) (model: Model) =
    let model', cmd, _ = update msg model
    model', dispatched cmd

let private labels (model: Model) = activeFilters model |> List.map snd

[<Tests>]
let handyFilterTests =
    testList "Kassenbuch unter 1024 px: gesetzte Filter und Filter-Sheet (ledger-d9z3n)" [

        test "Chips: Text und Reihenfolge aus dem Filterzustand" {
            Expect.equal
                (labels (fullModel ()))
                [ "Zu prüfen"
                  "Lena"
                  "Ohne Kategorie"
                  "Kategorie: Lebensmittel"
                  "Dieser Monat"
                  "Betrag 5 bis 50 €"
                  "Ausgaben"
                  "Betrag, größte Ausgabe zuerst"
                  "mit Gelöschten" ]
                "feste Reihenfolge, die Suche steht im Feld und nicht als Chip"
        }

        test "Chips: ohne gesetzten Filter keine Zeile, auch mit Suche" {
            let model = { baseModel () with Filters = { noFilters with Search = Some "rewe" } }
            Expect.isEmpty (activeFilters model) "keine Chips"
        }

        test "Chips: mehrere Kategorien als Anzahl, freier Zeitraum und offene Betragsgrenzen kurz" {
            let model =
                { baseModel () with
                    Filters =
                        { noFilters with
                            CategoryIds = Set.ofList [ food; rent ]
                            DateRange = Some { Quick = None; From = Some (DateTime(2026, 11, 1)); To = Some (DateTime(2026, 11, 14)) }
                            AmountMax = Some 12.5m } }
            Expect.equal (labels model) [ "Kategorie: 2"; "01.11.26 bis 14.11.26"; "Betrag bis 12,5 €" ] "Kurzformen"
            let from = { model with Filters = { noFilters with DateRange = Some { Quick = None; From = Some (DateTime(2026, 11, 1)); To = None }; AmountMin = Some 5m } }
            Expect.equal (labels from) [ "ab 01.11.26"; "Betrag ab 5 €" ] "nur eine Grenze"
        }

        // ADR 0095: die Richtung ist ein eigener Chip hinter dem Betrag, auch ohne Betrag.
        test "Chips: eine gesetzte Richtung steht als „Ausgaben“ bzw. „Einnahmen“ und zählt zu „Filter (n)“" {
            let inflows = { baseModel () with Filters = { noFilters with AmountDirection = OnlyInflows } }
            Expect.equal (labels inflows) [ "Einnahmen" ] "Richtung allein ohne Betrag"
            Expect.equal (filterButtonText (List.length (activeFilters inflows))) "Filter (1)" "zählt mit"
            let both = { baseModel () with Filters = { noFilters with AmountMin = Some 5m; AmountDirection = OnlyOutflows } }
            Expect.equal (labels both) [ "Betrag ab 5 €"; "Ausgaben" ] "hinter dem Betrag-Chip"
            Expect.isEmpty (activeFilters { baseModel () with Filters = { noFilters with AmountDirection = AllDirections } }) "„Alle“ ist kein Chip"
        }

        test "Richtung: der Umschalter wirkt sofort, die Anfrage trägt sie, „Alle“ nimmt sie zurück" {
            let outflows, load = step (SetAmountDirection OnlyOutflows) (baseModel ())
            Expect.equal outflows.Filters.AmountDirection OnlyOutflows "gesetzt"
            Expect.contains load (RunLoadEntriesPage 0) "lädt ohne Verzögerung neu"
            Expect.equal (buildPageQuery outflows (Some accountId) 0).AmountDirection OnlyOutflows "die Anfrage trägt die Richtung"
            let same, noLoad = step (SetAmountDirection OnlyOutflows) outflows
            Expect.equal same.Filters.AmountDirection OnlyOutflows "unverändert"
            Expect.isEmpty noLoad "gleiche Richtung, keine neue Abfrage"
            let all, _ = step (SetAmountDirection AllDirections) outflows
            Expect.isEmpty (activeFilters all) "kein Chip mehr"
        }

        test "Betragsfelder nehmen kein Vorzeichen an: „-“ und „+“ fallen beim Tippen weg" {
            let typedMin, _ = step (SetAmountMinDraft "-5") (baseModel ())
            Expect.equal typedMin.AmountMinDraft "5" "Minus fällt weg"
            let typedMax, _ = step (SetAmountMaxDraft "+50,00") typedMin
            Expect.equal typedMax.AmountMaxDraft "50,00" "Plus fällt weg"
            Expect.equal (withoutAmountSign "\u221212,5") "12,5" "auch das typografische Minus"
        }

        test "„×“ am PC-Chip „Betrag“ nimmt Betrag und Richtung zurück" {
            let model = { baseModel () with Filters = { noFilters with AmountMin = Some 5m; AmountDirection = OnlyInflows } }
            let after, cmd = step RemoveAmountAndDirection model
            Expect.equal after.Filters noFilters "Betrag und Richtung weg"
            Expect.contains cmd (RunLoadEntriesPage 0) "lädt neu"
        }

        test "Chips: die Standard-Sortierung und Konto-Sortierung bei einem Konto ergeben keinen Chip" {
            Expect.isEmpty (activeFilters (baseModel ())) "Datum, neueste zuerst"
            let byAccount = { baseModel () with Sort = { Field = SortByAccount; Direction = SortAsc } }
            Expect.isEmpty (activeFilters byAccount) "bei einem Konto gilt die Standard-Sortierung"
            Expect.equal
                (labels { byAccount with SelectedFilter = Some AllAccounts })
                [ "Konto, A bis Z" ]
                "in „Alle Konten“ zählt sie"
        }

        test "„×“ nimmt genau diesen Filter zurück, die übrigen bleiben" {
            let full = fullModel ()
            let all = activeFilters full
            for (filter, label) in all do
                let after, cmd = step (removeActiveFilterMsg filter) full
                Expect.equal
                    (labels after)
                    (all |> List.filter (fun (f, _) -> f <> filter) |> List.map snd)
                    $"nur „{label}“ fällt weg"
                Expect.equal after.Filters.Search (Some "rewe") $"die Suche bleibt bei „{label}“"
                Expect.equal after.SelectedFilter full.SelectedFilter $"das Konto bleibt bei „{label}“"
                Expect.contains cmd (RunLoadEntriesPage 0) $"lädt neu nach „{label}“"
        }

        test "Filter löschen nimmt alle Filter und die Suche zurück, das Konto bleibt" {
            let cleared, cmd = step ClearAllFilters (fullModel ())
            Expect.isEmpty (activeFilters cleared) "keine Chips mehr"
            Expect.equal cleared.Filters noFilters "keine Such-Filter, keine Schalter"
            Expect.isFalse cleared.OnlyNeedsReview "Zu prüfen aus"
            Expect.isFalse cleared.IncludeDeleted "Gelöschte zeigen aus"
            Expect.equal cleared.Sort LedgerSort.defaultSort "Standard-Sortierung"
            Expect.equal cleared.SearchDraft "" "Suchfeld leer"
            Expect.equal cleared.SelectedFilter (Some (OneAccount accountId)) "das Konto bleibt"
            Expect.contains cmd (RunLoadEntriesPage 0) "lädt neu"
        }

        test "Eine wartende Sucheingabe holt die gelöschte Suche nicht zurück" {
            let typing, _ = step (SetSearchDraft "edeka") (fullModel ())
            let cleared, _ = step ClearAllFilters typing
            let after, cmd = step (CommitSearch ("edeka", typing.SearchVersion)) cleared
            Expect.isNone after.Filters.Search "keine Suche"
            Expect.isEmpty cmd "keine neue Abfrage"
        }

        test "Filter-Knopf: die Zahl ist die Zahl der Chips, ohne die Suche" {
            Expect.equal (filterButtonText 0) "Filter" "ohne Filter keine Zahl"
            let three =
                { baseModel () with
                    OnlyNeedsReview = true
                    Filters = { noFilters with OnlyAskLena = true; AmountMin = Some 5m; Search = Some "rewe" } }
            Expect.equal (filterButtonText (List.length (activeFilters three))) "Filter (3)" "drei Chips, die Suche zählt nicht"
            Expect.equal (filterButtonText (List.length (activeFilters (fullModel ())))) "Filter (9)" "alle neun"
        }

        test "Suche: gilt nach der Verzögerung getrimmt, leer ist keine Suche, gleicher Text lädt nicht neu" {
            let typing, cmd = step (SetSearchDraft "  Rewe ") (baseModel ())
            Expect.equal typing.SearchDraft "  Rewe " "der Text steht im Feld, wie getippt"
            Expect.isNone typing.Filters.Search "noch keine Suche"
            Expect.isEmpty cmd "erst nach der Verzögerung"
            let committed, load = step (CommitSearch ("  Rewe ", typing.SearchVersion)) typing
            Expect.equal committed.Filters.Search (Some "Rewe") "getrimmt"
            Expect.contains load (RunLoadEntriesPage 0) "lädt neu"
            Expect.equal (buildPageQuery committed (Some accountId) 0).Search (Some "Rewe") "die Anfrage trägt die Suche"
            let again, noLoad = step (CommitSearch ("Rewe", committed.SearchVersion)) committed
            Expect.equal again.Filters.Search (Some "Rewe") "unverändert"
            Expect.isEmpty noLoad "gleiche Suche, keine neue Abfrage"
            let emptied, _ = step (CommitSearch ("   ", committed.SearchVersion)) committed
            Expect.isNone emptied.Filters.Search "leer heißt keine Suche"
        }

        test "Filter-Sheet: öffnet mit den geltenden Beträgen im Feld, schließt ohne die Filter zu ändern" {
            let model = { baseModel () with Filters = { noFilters with AmountMin = Some 5m; AmountMax = Some 12.5m } }
            let opened, cmd = step OpenFilterSheet model
            Expect.isTrue opened.FilterSheetOpen "offen"
            Expect.equal (opened.AmountMinDraft, opened.AmountMaxDraft) ("5", "12.5") "Felder zeigen den Filter"
            Expect.isEmpty cmd "keine Abfrage"
            let closed, _ = step CloseFilterSheet opened
            Expect.isFalse closed.FilterSheetOpen "zu"
            Expect.equal closed.Filters model.Filters "Filter unverändert"
        }

        test "Filter-Sheet: ein Schalter wirkt sofort auf die Liste, das Sheet bleibt offen" {
            let opened, _ = step OpenFilterSheet (baseModel ())
            let toggled, cmd = step ToggleOnlyNeedsReview opened
            Expect.isTrue toggled.OnlyNeedsReview "Zu prüfen an"
            Expect.isTrue toggled.FilterSheetOpen "Sheet bleibt offen"
            Expect.contains cmd (RunLoadEntriesPage 0) "lädt sofort"
        }

        test "Filter-Sheet: Esc schließt erst die Auswahl darüber, dann das Sheet" {
            let opened, _ = step OpenFilterSheet (baseModel ())
            let picking, _ = step (OpenFilterPopover CategoryChip) opened
            let first, _ = step EscapePressed picking
            Expect.isNone first.OpenPopover "die Kategorie-Auswahl schließt"
            Expect.isTrue first.FilterSheetOpen "das Sheet steht wieder"
            let second, _ = step EscapePressed first
            Expect.isFalse second.FilterSheetOpen "dann schließt das Sheet"
        }

        test "Filter-Sheet: „n zeigen“ mit Trefferzahl, beim Laden nur „Zeigen“" {
            Expect.equal (Components.Ledger.View.filterSheetShowLabel (Success []) 1234) "1.234 zeigen" "Treffer mit Tausenderpunkt"
            Expect.equal (Components.Ledger.View.filterSheetShowLabel Loading 1234) "Zeigen" "beim Laden"
        }

        test "Filter-Sheet: Werte der Zeilen Kategorie, Zeitraum und Sortieren" {
            Expect.equal (Components.Ledger.View.filterSheetCategoryValue [] 0) "alle" "ohne Kategorie-Filter"
            Expect.equal (Components.Ledger.View.filterSheetCategoryValue [ "Lebensmittel"; "Miete" ] 2) "Lebensmittel, Miete" "Namen"
            Expect.equal (Components.Ledger.View.filterSheetDateValue noFilters) "alle" "ohne Zeitraum"
            Expect.equal
                (Components.Ledger.View.filterSheetDateValue { noFilters with DateRange = Some { Quick = Some LastMonth; From = None; To = None } })
                "Letzter Monat"
                "Schnellwahl"
            Expect.equal (sortValueText LedgerSort.defaultSort) "Datum, neueste zuerst" "Standard"
            Expect.equal (sortValueText { Field = SortByPayee; Direction = SortDesc }) "Payee, Z bis A" "„Z bis A“ bleibt groß"
        }

        test "Die Anzahl hinter „Ohne Kategorie“ kommt mit der Seite vom Server" {
            let page : LedgerEntryPage =
                { Entries = []; TotalCount = 0; TotalAmount = { Amount = 0m; Currency = "EUR" }; DeletedCount = 0
                  NeedsReviewCount = 0; AskLenaCount = 0; WithoutCategoryCount = 7 }
            let model = baseModel ()
            let loaded, _ = step (entriesPageAnswer model 0 (Ok page)) model
            Expect.equal loaded.WithoutCategoryCount 7 "Zahl vom Server"
        }

        test "Kopf auf „Buchungen“: „Abrufen“ steht vor „Jetzt spiegeln“" {
            let actions = Components.Ledger.View.headerActionsFor false 0
            Expect.equal
                (actions |> List.filter (fun a -> a = Components.Ledger.View.FetchIcon || a = Components.Ledger.View.MirrorIcon))
                [ Components.Ledger.View.FetchIcon; Components.Ledger.View.MirrorIcon ]
                "Handy: erst „Abrufen“, dann „Jetzt spiegeln“"
            Expect.isFalse
                (Components.Ledger.View.headerActionsFor true 0 |> List.contains Components.Ledger.View.FetchIcon)
                "auf „Regeln“ kein „Abrufen“"
        }
    ]
