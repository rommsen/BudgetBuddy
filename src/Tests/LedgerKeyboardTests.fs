module LedgerKeyboardTests

// ledger-rjb8w: Taste G und Tastaturfokus in der Kassenbuch-Liste (PC). Fokus und Befehle sind
// rein und laufen ohne Proxy. Der Reducer setzt `StartSetReviewMark` ab, das den Proxy erst im
// folgenden `RunSetReviewMark` anfasst; hier wird nur die Nachricht eingesammelt.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

let private entry (n: int) (needsReview: bool) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n (-10m * decimal n) needsReview false with NeedsReview = needsReview }

let private idOf (n: int) = (entry n true).Id

let private rows (count: int) = [ for n in 1..count -> entry n true ]

let private modelWith (loaded: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = List.length loaded
        NeedsReviewCount = List.length loaded
        HasMoreEntries = false }

let private focusOn (n: int) (model: Model) = { model with FocusedRow = Some (idOf n) }

let private run (msg: Msg) (model: Model) : Model * Msg list =
    let model', cmd, _ = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent

let private step (msg: Msg) (model: Model) : Model = run msg model |> fst

let private okOutcome : ReviewMarkOutcome =
    { Previous = { IsApproved = false; ClearedState = ReviewUncleared }
      Change = ReviewMarkChanged
      WriteBack = ReviewMarkSent }

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private ids (model: Model) =
    model.Entries |> RemoteData.withDefault [] |> List.map (fun e -> e.Id)

[<Tests>]
let moveTests =
    testList "Tastaturfokus: Pfeiltasten (ledger-rjb8w)" [

        testCase "ohne Fokus setzt Pfeil ab die erste Zeile" <| fun () ->
            let model = modelWith (rows 3) |> step (MoveFocus 1)
            Expect.equal model.FocusedRow (Some (idOf 1)) "erste Zeile"

        testCase "Pfeil ab und auf bewegen den Fokus um eine Zeile" <| fun () ->
            let down = modelWith (rows 3) |> focusOn 1 |> step (MoveFocus 1)
            Expect.equal down.FocusedRow (Some (idOf 2)) "ab"
            let up = down |> step (MoveFocus -1)
            Expect.equal up.FocusedRow (Some (idOf 1)) "auf"

        testCase "am Listenende und am Listenanfang bleibt der Fokus stehen" <| fun () ->
            let last = modelWith (rows 3) |> focusOn 3 |> step (MoveFocus 1)
            Expect.equal last.FocusedRow (Some (idOf 3)) "Ende"
            let first = modelWith (rows 3) |> focusOn 1 |> step (MoveFocus -1)
            Expect.equal first.FocusedRow (Some (idOf 1)) "Anfang"

        testCase "bei laufender Auswahl und im Auswahl-Modus bewegen die Pfeiltasten den Fokus weiter" <| fun () ->
            let picked = { (modelWith (rows 3) |> focusOn 1) with Selection = Picked (Set.singleton (idOf 1)) }
            Expect.equal (step (MoveFocus 1) picked).FocusedRow (Some (idOf 2)) "mit Auswahl"
            let mode = { (modelWith (rows 3) |> focusOn 1) with SelectionMode = true }
            Expect.equal (step (MoveFocus 1) mode).FocusedRow (Some (idOf 2)) "im Modus"

        testCase "bei offenem Filter-Popover bewegt sich der Fokus nicht" <| fun () ->
            let model = { (modelWith (rows 3) |> focusOn 1) with OpenPopover = Some ChipId.CategoryChip }
            Expect.equal (step (MoveFocus 1) model).FocusedRow (Some (idOf 1)) "Popover hat die Tastatur"
    ]

[<Tests>]
let reviewKeyTests =
    testList "Tastaturfokus: Taste G (ledger-rjb8w)" [

        testCase "G auf der Fokuszeile löst setReviewMark Reviewed aus und springt auf die nächste Zeile" <| fun () ->
            let model, sent = modelWith (rows 3) |> focusOn 1 |> run (ReviewKeyPressed Reviewed)
            Expect.equal sent [ RunSetReviewMark (idOf 1, Reviewed) ] "Befehl für die Fokuszeile"
            Expect.equal model.ReviewMarkRows.[idOf 1] ReviewMarkSending "Zeile sendet"
            Expect.equal model.FocusedRow (Some (idOf 2)) "Fokus auf der nächsten Zeile"

        testCase "Umschalt+G löst Unreviewed aus" <| fun () ->
            let model, sent = modelWith (rows 3) |> focusOn 2 |> run (ReviewKeyPressed Unreviewed)
            Expect.equal sent [ RunSetReviewMark (idOf 2, Unreviewed) ] "Ungeprüft"
            Expect.equal model.FocusedRow (Some (idOf 3)) "Fokus weiter"

        testCase "G verhält sich wie der Klick auf die Schaltfläche (gleicher Zeilenzustand und Befehl)" <| fun () ->
            let start = modelWith (rows 3) |> focusOn 1
            let viaKey, keySent = run (ReviewKeyPressed Reviewed) start
            let viaClick, clickSent = run (StartSetReviewMark (idOf 1, Reviewed)) start
            Expect.equal viaKey.ReviewMarkRows viaClick.ReviewMarkRows "Zeilenzustand"
            Expect.equal keySent clickSent "Befehl"

        testCase "scheitert der Rückschrieb, bleibt der Fehlschlag-Hinweis an der Zeile wie beim Klick" <| fun () ->
            let failed = { okOutcome with WriteBack = ReviewMarkSendFailed "YNAB 500" }
            let model =
                modelWith (rows 3) |> focusOn 1 |> step (ReviewKeyPressed Reviewed)
                |> step (ReviewMarkSet (idOf 1, Reviewed, Ok failed))
            let row = model.Entries |> RemoteData.withDefault [] |> List.find (fun e -> e.Id = idOf 1)
            Expect.equal row.ReviewMarkWriteBackFailure (Some "YNAB 500") "Hinweis gesetzt"

        testCase "mit aktivem Chip rückt der Fokus auf die Zeile, die nachrückt" <| fun () ->
            let start = { (modelWith (rows 3) |> focusOn 1) with OnlyNeedsReview = true }
            let model = start |> step (ReviewKeyPressed Reviewed) |> step (ReviewMarkSet (idOf 1, Reviewed, Ok okOutcome))
            Expect.equal (ids model) [ idOf 2; idOf 3 ] "Zeile 1 ist weg"
            Expect.equal model.FocusedRow (Some (idOf 2)) "Fokus auf der nachgerückten Zeile"

        testCase "Fokus auf der letzten Zeile, G, Zeile verschwindet: Fokus auf der neuen letzten Zeile" <| fun () ->
            let start = { (modelWith (rows 3) |> focusOn 3) with OnlyNeedsReview = true }
            let pressed = start |> step (ReviewKeyPressed Reviewed)
            Expect.equal pressed.FocusedRow (Some (idOf 3)) "am Ende bleibt der Fokus stehen"
            let model = pressed |> step (ReviewMarkSet (idOf 3, Reviewed, Ok okOutcome))
            Expect.equal (ids model) [ idOf 1; idOf 2 ] "Zeile 3 ist weg"
            Expect.equal model.FocusedRow (Some (idOf 2)) "neue letzte Zeile, kein Fokus ins Leere"

        testCase "verschwindet die einzige Zeile, bleibt kein Fokus ins Leere" <| fun () ->
            let start = { (modelWith (rows 1) |> focusOn 1) with OnlyNeedsReview = true }
            let model = start |> step (ReviewKeyPressed Reviewed) |> step (ReviewMarkSet (idOf 1, Reviewed, Ok okOutcome))
            Expect.equal model.FocusedRow None "leere Liste"

        testCase "bei laufender Auswahl löst G nichts aus" <| fun () ->
            let start = { (modelWith (rows 3) |> focusOn 1) with Selection = Picked (Set.singleton (idOf 2)) }
            let model, sent = run (ReviewKeyPressed Reviewed) start
            Expect.isEmpty sent "kein Befehl"
            Expect.equal model start "Modell unverändert"

        testCase "im Auswahl-Modus ohne gewählte Zeile löst G nichts aus" <| fun () ->
            let start = { (modelWith (rows 3) |> focusOn 1) with SelectionMode = true }
            let model, sent = run (ReviewKeyPressed Reviewed) start
            Expect.isEmpty sent "kein Befehl"
            Expect.equal model start "Modell unverändert"

        testCase "ohne Fokuszeile, auf einer gelöschten Zeile und während eines Aufrufs löst G nichts aus" <| fun () ->
            let noFocus = modelWith (rows 3)
            Expect.isEmpty (run (ReviewKeyPressed Reviewed) noFocus |> snd) "ohne Fokus"
            let deleted = { modelWith [ { entry 1 true with DeletedInYnab = true } ] with FocusedRow = Some (idOf 1) }
            Expect.isEmpty (run (ReviewKeyPressed Reviewed) deleted |> snd) "gelöscht"
            let sending = modelWith (rows 3) |> focusOn 1 |> step (ReviewKeyPressed Reviewed) |> focusOn 1
            Expect.isEmpty (run (ReviewKeyPressed Reviewed) sending |> snd) "läuft schon"
    ]

[<Tests>]
let reconcileTests =
    testList "Tastaturfokus: Nachladen und Filterwechsel (ledger-rjb8w)" [

        testCase "der Fokus bleibt beim Nachladen einer Seite" <| fun () ->
            let page2 : LedgerEntryPage =
                { Entries = [ entry 4 true ]; TotalCount = 4; TotalAmount = { Amount = 0m; Currency = "EUR" }
                  DeletedCount = 0; NeedsReviewCount = 4 }
            let model = { (modelWith (rows 3) |> focusOn 2) with NextPage = 1 } |> step (EntriesPageLoaded (1, Ok page2))
            Expect.equal model.FocusedRow (Some (idOf 2)) "Fokus bleibt"

        testCase "fällt die Fokuszeile nach einem Filterwechsel aus der Liste, springt er auf die erste Zeile" <| fun () ->
            let page : LedgerEntryPage =
                { Entries = [ entry 7 true; entry 8 true ]; TotalCount = 2; TotalAmount = { Amount = 0m; Currency = "EUR" }
                  DeletedCount = 0; NeedsReviewCount = 2 }
            let model =
                modelWith (rows 3) |> focusOn 2
                |> step ToggleOnlyNeedsReview
                |> step (EntriesPageLoaded (0, Ok page))
            Expect.equal model.FocusedRow (Some (idOf 7)) "erste Zeile der neuen Liste"

        testCase "ohne Fokus setzt das Laden keinen" <| fun () ->
            Expect.equal (reconcileFocus (rows 2) None) None "bleibt leer"
    ]

[<Tests>]
let keyBindingTests =
    testList "Tastaturfokus: Tastenbelegung (ledger-rjb8w)" [

        testCase "Pfeile, J und K bewegen, g und G prüfen" <| fun () ->
            let key k = keyCommandFor "BODY" false false false k
            Expect.equal (key "ArrowDown") (Some (MoveFocus 1)) "ab"
            Expect.equal (key "j") (Some (MoveFocus 1)) "j"
            Expect.equal (key "ArrowUp") (Some (MoveFocus -1)) "auf"
            Expect.equal (key "k") (Some (MoveFocus -1)) "k"
            Expect.equal (key "g") (Some (ReviewKeyPressed Reviewed)) "g"
            Expect.equal (key "G") (Some (ReviewKeyPressed Unreviewed)) "Umschalt+G"

        testCase "ein Tastenereignis aus einem Eingabefeld löst keinen Befehl aus" <| fun () ->
            for tag in [ "INPUT"; "input"; "TEXTAREA"; "SELECT" ] do
                for k in [ "g"; "G"; "j"; "ArrowDown" ] do
                    Expect.isNone (keyCommandFor tag false false false k) $"{tag} {k}"
            Expect.isNone (keyCommandFor "DIV" true false false "g") "contenteditable"

        testCase "Strg, Meta und Alt lösen keinen Befehl aus" <| fun () ->
            Expect.isNone (keyCommandFor "BODY" false true false "g") "Strg oder Meta"
            Expect.isNone (keyCommandFor "BODY" false false true "g") "Alt"

        testCase "Enter, Esc und Leertaste bleiben frei (ledger-q1hw2)" <| fun () ->
            for k in [ "Enter"; "Escape"; " "; "a" ] do
                Expect.isNone (keyCommandFor "BODY" false false false k) k

        testCase "nur die Kassenbuch-Ansicht hört auf Tasten am Dokument, kein anderes Kürzel kollidiert" <| fun () ->
            let clientDir = Path.Combine(repoRoot, "src", "Client")
            let listeners =
                Directory.GetFiles(clientDir, "*.fs", SearchOption.AllDirectories)
                |> Array.filter (fun f ->
                    let text = File.ReadAllText f
                    text.Contains "addEventListener (\"keydown\"" || text.Contains "addEventListener(\"keydown\"")
                |> Array.map Path.GetFileName
            Expect.equal listeners [| "View.fs" |] "einziger Dokument-Handler"
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "KeyboardNavigation model.FocusedRow dispatch" "in der Seite eingebunden"
    ]

[<Tests>]
let viewTests =
    testList "Tastaturfokus: Darstellung (ledger-rjb8w)" [

        testCase "die Fokuszeile trägt ein Merkmal, andere Zeilen nicht" <| fun () ->
            let focused = Table.rowClass { Table.defaultRowProps with IsFocused = true }
            let other = Table.rowClass Table.defaultRowProps
            Expect.stringContains focused "bg-surface-hover" "Grund der Fokuszeile"
            Expect.isFalse (other.Contains "bg-surface-hover") "andere Zeilen ohne Grund"
            let table = readRepo [ "src"; "Client"; "DesignSystem"; "Table.fs" ]
            Expect.stringContains table "aria-selected" "aria-selected nur an der Fokuszeile"
            Expect.stringContains table "if props.IsFocused then" "bedingt"

        testCase "die Fokuszeile ist ruhig: kein Ring, kein Schatten, keine Leuchtfarbe" <| fun () ->
            for forbidden in [ "ring"; "shadow"; "neon"; "glow" ] do
                Expect.isFalse (Table.focusedRowClass.Contains forbidden) $"{forbidden} in {Table.focusedRowClass}"

        testCase "die Liste reicht den Fokus an die PC-Zeile durch" <| fun () ->
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "IsFocused = isFocused" "Zeile"
            Expect.stringContains view "(focusedRow = Some entry.Id)" "Vergleich mit der Fokuszeile"

        testCase "der Tastaturhinweis nennt G und steht nur mit Zeilen und ohne Auswahl da" <| fun () ->
            Expect.stringContains Components.Ledger.View.keyboardHintText "G geprüft" "Text"
            let model = modelWith (rows 2)
            Expect.isTrue (Components.Ledger.View.keyboardHintVisible model) "mit Zeilen"
            Expect.isFalse (Components.Ledger.View.keyboardHintVisible { model with Selection = Picked (Set.singleton (idOf 1)) }) "mit Auswahl"
            Expect.isFalse (Components.Ledger.View.keyboardHintVisible { model with Entries = Success [] }) "ohne Zeilen"
    ]
