module LedgerVisibleColumnsTests

// ledger-qh9qx: Welche Spalten die PC-Tabelle bei welcher Tabellenbreite zeigt, ist eine
// reine Funktion (`visibleColumns`) und läuft ohne Browser. Die Köpfe im echten Browser
// misst `scripts/ledger-table-columns.test.mjs`.

open System.IO
open Expecto
open Components.Ledger.View

let private columnsOf (layout: TableLayout) : LedgerColumn list =
    match layout with
    | Columns cols -> cols |> List.map fst
    | CompactList -> []

let private minSum (cols: LedgerColumn list) = cols |> List.sumBy minColumnWidth |> float

let private allAccountsFull =
    [ SelectColumn; DateColumn; AccountColumn; PayeeColumn; CategoryColumn; MemoColumn; AmountColumn; StatusColumn ]

let private allAccountsNoMemo = allAccountsFull |> List.filter ((<>) MemoColumn)
let private allAccountsNoMemoNoAccount = allAccountsNoMemo |> List.filter ((<>) AccountColumn)
let private singleFull = allAccountsFull |> List.filter ((<>) AccountColumn)
let private singleNoMemo = singleFull |> List.filter ((<>) MemoColumn)

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

[<Tests>]
let visibleColumnsTests =
    testList "visibleColumns (ledger-qh9qx)" [

        test "Gesamtsicht, breit: alle Spalten" {
            Expect.equal (columnsOf (visibleColumns 1090.0 true)) allAccountsFull "alle acht Spalten"
        }

        test "Gesamtsicht: Grenzwert für Memo liegt bei der Summe der Mindestbreiten" {
            let edge = minSum allAccountsFull
            Expect.equal (columnsOf (visibleColumns edge true)) allAccountsFull "genau auf der Grenze: Memo bleibt"
            Expect.equal (columnsOf (visibleColumns (edge - 1.0) true)) allAccountsNoMemo "einen Pixel schmaler: Memo fällt weg"
        }

        test "Gesamtsicht: Grenzwert für die Konto-Spalte" {
            let edge = minSum allAccountsNoMemo
            Expect.equal (columnsOf (visibleColumns edge true)) allAccountsNoMemo "Konto bleibt"
            Expect.equal
                (columnsOf (visibleColumns (edge - 1.0) true))
                allAccountsNoMemoNoAccount
                "einen Pixel schmaler: Konto fällt weg"
        }

        test "Gesamtsicht: Grenzwert für die Handy-Liste" {
            let edge = minSum allAccountsNoMemoNoAccount
            Expect.equal (columnsOf (visibleColumns edge true)) allAccountsNoMemoNoAccount "Tabelle ohne Memo und Konto"
            Expect.equal (visibleColumns (edge - 1.0) true) CompactList "darunter greift die Liste"
        }

        test "ein Konto: keine Konto-Spalte, erst Memo, dann Liste" {
            let memoEdge = minSum singleFull
            Expect.equal (columnsOf (visibleColumns 1090.0 false)) singleFull "breit: alle Spalten ohne Konto"
            Expect.equal (columnsOf (visibleColumns memoEdge false)) singleFull "Grenze: Memo bleibt"
            Expect.equal (columnsOf (visibleColumns (memoEdge - 1.0) false)) singleNoMemo "Memo fällt weg"
            let listEdge = minSum singleNoMemo
            Expect.equal (columnsOf (visibleColumns listEdge false)) singleNoMemo "Grenze: Tabelle ohne Memo"
            Expect.equal (visibleColumns (listEdge - 1.0) false) CompactList "darunter die Liste"
        }

        test "Breite null und Minusbreite ergeben die Liste" {
            Expect.equal (visibleColumns 0.0 true) CompactList "null"
            Expect.equal (visibleColumns -5.0 false) CompactList "negativ"
        }

        test "in keiner Stufe ist eine sichtbare Spalte schmaler als ihre Mindestbreite" {
            for showAccount in [ true; false ] do
                for width in [ 300.0 .. 7.0 .. 1600.0 ] do
                    match visibleColumns width showAccount with
                    | CompactList -> ()
                    | Columns cols ->
                        for (column, w) in cols do
                            let px = columnPixelWidth width cols column |> Option.get
                            Expect.isGreaterThanOrEqual
                                px
                                (float (minColumnWidth column))
                                $"{column} bei {width}px (Konto={showAccount}) hat {px}px"
                            match w with
                            | FixedPx fixedPx -> Expect.equal (float fixedPx) px "feste Breite ist die Pixelbreite"
                            | Rest -> ()
        }

        test "genau eine Restspalte, für sie bleibt Platz" {
            for showAccount in [ true; false ] do
                for width in [ 600.0 .. 13.0 .. 1600.0 ] do
                    match visibleColumns width showAccount with
                    | CompactList -> ()
                    | Columns cols ->
                        let rest = cols |> List.filter (fun (_, w) -> w = Rest) |> List.length
                        Expect.equal rest 1 $"genau eine Restspalte bei {width}px"
                        let fixedSum = cols |> List.sumBy (fun (_, w) -> match w with FixedPx p -> p | Rest -> 0)
                        Expect.isLessThan (float fixedSum) width "für die Restspalte bleibt Platz"
        }

        test "die Konto-Spalte gibt es in einem Konto nie" {
            for width in [ 300.0 .. 10.0 .. 1600.0 ] do
                Expect.isFalse
                    (columnsOf (visibleColumns width false) |> List.contains AccountColumn)
                    $"ein Konto bei {width}px"
        }

        test "ohne Konto-Spalte steht das Kürzel in der Payee-Zelle (Quelltext-Wächter)" {
            let view = File.ReadAllText(Path.Combine(repoRoot, "src", "Client", "Components", "Ledger", "View.fs"))
            Expect.stringContains view "if isAllAccounts && not (List.contains AccountColumn columns)"
                "Das Kürzel erscheint in der Gesamtsicht, wenn die Konto-Spalte fehlt."
            Expect.stringContains view "Html.span [ prop.text payee ]"
                "Payee-Text und Kürzel sind getrennte Elemente."
        }
    ]
