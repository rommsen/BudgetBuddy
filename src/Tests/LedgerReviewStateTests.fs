module LedgerReviewStateTests

// Liste im Prüf-Zustand (ledger-v9rq3): Summenzeile auch bei den Chips, Filter im Klartext,
// Leerzustände nach dem letzten Prüfen, Zuletzt verwendet im Kategorie-Filter, Übernehmen
// erst mit Änderung. Fixtures tragen das Kürzel v9rq3 (Guid-Präfix 9a9a9a9a).

open System
open Expecto
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private catGuid (n: int) = Guid.Parse $"9a9a9a9a-0000-7000-8000-0000000009{n:D2}"
let private freshModel () = fst (Components.Ledger.State.init ())

[<Tests>]
let tests =
    testList "Ledger Prüf-Zustand (v9rq3)" [

        testCase "Summenzeile sichtbar bei nur Zu prüfen" <| fun () ->
            let m = { freshModel () with OnlyNeedsReview = true; Entries = Success [] }
            Expect.equal (summaryState m) SummaryState.Ready "Zu prüfen allein zeigt Trefferzahl und Summe"
            Expect.equal (summaryState { m with Entries = Loading }) SummaryState.Loading "Laden"

        testCase "Summenzeile sichtbar bei nur Ohne Kategorie" <| fun () ->
            let m = { freshModel () with Entries = Success []; Filters = { noFilters with WithoutCategory = true } }
            Expect.equal (summaryState m) SummaryState.Ready "Ohne Kategorie allein"

        testCase "Summenzeile bleibt ohne jeden Filter versteckt, hasAnyFilter unverändert" <| fun () ->
            Expect.equal (summaryState { freshModel () with Entries = Success [] }) SummaryState.Hidden "nichts aktiv"
            Expect.isFalse (hasAnyFilter { noFilters with WithoutCategory = true }) "Chip zählt nicht zu hasAnyFilter"

        testCase "Leerzustand: nur Chip aktiv ist Alles geprüft bzw. Alles kategorisiert" <| fun () ->
            Expect.equal (emptyListKind true noFilters) EmptyListKind.AllReviewed "Zu prüfen"
            Expect.equal (emptyListKind false { noFilters with WithoutCategory = true }) EmptyListKind.AllCategorized "Ohne Kategorie"
            Expect.equal (emptyListKind false noFilters) EmptyListKind.NoEntries "kein Filter"

        testCase "Leerzustand: Chip mit weiterem Filter oder beide Chips ist Kein Treffer" <| fun () ->
            Expect.equal (emptyListKind true { noFilters with Payee = Some "x" }) EmptyListKind.NoMatch "Chip plus Payee"
            Expect.equal (emptyListKind true { noFilters with WithoutCategory = true }) EmptyListKind.NoMatch "beide Chips"
            Expect.equal (emptyListKind false { noFilters with Memo = Some "x" }) EmptyListKind.NoMatch "nur Memo"

        testCase "Klartext: Konto allein und Gesamtsicht" <| fun () ->
            Expect.equal (filterPlainText "ING Extra" false [] noFilters) "ING Extra" "Konto"
            Expect.equal (filterPlainText "alle Konten" false [] noFilters) "alle Konten" "Gesamtsicht"

        testCase "Klartext: Chip" <| fun () ->
            Expect.equal (filterPlainText "ING Extra" true [] noFilters) "ING Extra · zu prüfen" "Tafel 1.1"
            Expect.equal
                (filterPlainText "ING Extra" false [] { noFilters with WithoutCategory = true })
                "ING Extra · ohne Kategorie"
                "Ohne Kategorie"

        testCase "Klartext: mehrere Kategorien und Zeitraum in fester Reihenfolge" <| fun () ->
            let f =
                { noFilters with
                    CategoryIds = Set.ofList [ catGuid 1; catGuid 2 ]
                    DateRange = Some { Quick = Some Last12Months; From = None; To = None } }
            Expect.equal
                (filterPlainText "alle Konten" false [ "Lebensmittel"; "Restaurants" ] f)
                "alle Konten · Lebensmittel, Restaurants · letzte 12 Monate"
                "Tafel 2"

        testCase "Klartext: Kombination Chip, Payee, Memo, Betrag" <| fun () ->
            let f = { noFilters with Payee = Some "Café Lotte"; Memo = Some "Kuchen"; AmountMin = Some 5m; AmountMax = Some 10.5m }
            Expect.equal
                (filterPlainText "ING Extra" true [] f)
                "ING Extra · zu prüfen · Café Lotte · Memo: Kuchen · Betrag 5 bis 10,5"
                "Reihenfolge Konto, Chip, Payee, Memo, Betrag"

        testCase "Klartext: freier Zeitraum" <| fun () ->
            let f = { noFilters with DateRange = Some { Quick = None; From = Some (DateTime(2026, 3, 1)); To = Some (DateTime(2026, 3, 31)) } }
            Expect.equal (filterPlainText "alle Konten" false [] f) "alle Konten · 01.03.2026 bis 31.03.2026" "von bis"

        testCase "Zuletzt verwendet: neueste zuerst, ohne Doppelte, höchstens fünf" <| fun () ->
            let recent = [ catGuid 1; catGuid 2; catGuid 3; catGuid 4; catGuid 5 ]
            let pushed = pushRecentCategories (Set.ofList [ catGuid 3; catGuid 6 ]) recent
            Expect.equal (List.length pushed) 5 "gekappt"
            Expect.equal (List.length (List.distinct pushed)) 5 "ohne Doppelte"
            Expect.contains pushed (catGuid 6) "neu dabei"
            Expect.isFalse (List.contains (catGuid 5) pushed) "älteste fällt heraus"
            Expect.equal (pushRecentCategories Set.empty recent) recent "leere Auswahl ändert nichts"

        testCase "Zuletzt verwendet: Speicherform hin und zurück, kaputter Text wird ignoriert" <| fun () ->
            let ids = [ catGuid 1; catGuid 2 ]
            Expect.equal (parseRecentCategories (serializeRecentCategories ids)) ids "Roundtrip"
            Expect.equal (parseRecentCategories "kaputt,,") [] "kein Guid"
            Expect.equal (parseRecentCategories null) [] "null"

        testCase "Übernehmen merkt die Kategorien und ist bei unveränderter Auswahl gesperrt" <| fun () ->
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) (freshModel ())
            Expect.isTrue (categoryApplyDisabled opened.CategoryDraft opened.Filters.CategoryIds) "unverändert"
            let toggled, _, _ = Components.Ledger.State.update (ToggleCategoryDraft (catGuid 7)) opened
            Expect.isFalse (categoryApplyDisabled toggled.CategoryDraft toggled.Filters.CategoryIds) "nach Änderung aktiv"
            let applied, _, _ = Components.Ledger.State.update ApplyCategoryFilter toggled
            Expect.equal applied.RecentCategories [ catGuid 7 ] "gemerkt"
            let again, cmd, _ = Components.Ledger.State.update ApplyCategoryFilter { applied with CategoryDraft = applied.Filters.CategoryIds }
            Expect.equal again.NextPage applied.NextPage "unverändert"
            Expect.isTrue (List.isEmpty cmd) "kein Neuladen ohne Änderung"
    ]
