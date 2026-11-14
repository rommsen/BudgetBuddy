module LedgerDetailTests

// ledger-q1hw2 (ADR 0053): das Buchungs-Detail. Reducer-Pfade (Öffnen, Markieren, Panel folgt
// den Pfeiltasten, Sperren, Sortierwechsel), die reinen Entscheidungen der Ansicht (Form,
// Spalten neben dem Panel, Hinweisleiste, Zeitleiste, Split-Zeilen) und Quelltext-Zusicherungen
// für das, was `dotnet test` nicht rendern kann (Feliz-Elemente laufen nur im Browser).
// Die gerenderte Fassung prüft `scripts/ledger-detail.test.mjs`.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View
open Ledger.Events
open Ledger.QueryHandlers
open Client.DesignSystem

let private entry (n: int) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n (-10m * decimal n) true false with LinkedToYnab = true }

let private idOf (n: int) = (entry n).Id

let private rows (count: int) = [ for n in 1..count -> entry n ]

let private modelWith (loaded: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = List.length loaded
        NeedsReviewCount = List.length loaded
        HasMoreEntries = false }

let private asPanel (model: Model) = { model with DetailForm = DetailPanel }
let private asSheet (model: Model) = { model with DetailForm = DetailSheet }

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

let private historyLine (sequence: int) (origin: LedgerOrigin) (description: string) : LedgerHistoryLine =
    { Sequence = sequence
      OccurredAt = DateTime(2026, 9, 10, 17, 50 + sequence, 0, DateTimeKind.Local)
      Origin = origin
      Description = description }

// ============================================
// Öffnen, Markieren, Schließen
// ============================================

[<Tests>]
let openCloseTests =
    testList "Detail: Öffnen, Markieren, Schließen (ledger-q1hw2)" [

        testCase "ein Klick auf die Zeile markiert sie, öffnet das Detail und lädt die Historie" <| fun () ->
            let model, sent = modelWith (rows 3) |> run (OpenDetail (entry 2))
            Expect.equal model.FocusedRow (Some (idOf 2)) "die Zeile ist markiert"
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "Detail für genau sie"
            Expect.equal model.History Loading "Historie lädt"
            Expect.equal sent [ LoadHistory (idOf 2) ] "Abruf der Historie angestoßen"

        testCase "die geladene Historie landet im Modell (Loading, dann Success)" <| fun () ->
            let lines = [ historyLine 1 ImportedFromComdirect "Comdirect-Import: Buchung erfasst" ]
            let model =
                modelWith (rows 3)
                |> step (OpenDetail (entry 1))
                |> step (HistoryLoaded (idOf 1, 0, Ok lines))
            Expect.equal model.History (Success lines) "Success"

        testCase "eine Historie, die nach einem Wechsel der Buchung eintrifft, wird verworfen" <| fun () ->
            let model =
                modelWith (rows 3)
                |> asPanel
                |> step (OpenDetail (entry 1))
                |> step (OpenDetail (entry 2))
                |> step (HistoryLoaded (idOf 1, 0, Ok [ historyLine 1 ImportedFromComdirect "alt" ]))
            Expect.equal model.History Loading "die Historie der ersten Buchung überschreibt die zweite nicht"

        testCase "im Auswahl-Modus am Handy öffnet ein Tipp kein Detail" <| fun () ->
            let mode = { modelWith (rows 3) with SelectionMode = true }
            let model, sent = run (OpenDetail (entry 1)) mode
            Expect.isNone model.DetailFor "kein Detail"
            Expect.isEmpty sent "kein Abruf"
            Expect.isNone model.FocusedRow "keine Markierung"

        testCase "CloseDetail schließt das Detail und leert die Historie" <| fun () ->
            let model = modelWith (rows 3) |> step (OpenDetail (entry 1)) |> step CloseDetail
            Expect.isNone model.DetailFor "zu"
            Expect.equal model.History NotAsked "Historie leer"

        testCase "ein Kontowechsel schließt das Detail" <| fun () ->
            let model =
                modelWith (rows 3) |> step (OpenDetail (entry 1)) |> step (SelectFilter AllAccounts)
            Expect.isNone model.DetailFor "die Buchung steht gleich nicht mehr in der Liste"

        testCase "DetailFormChanged setzt die Form" <| fun () ->
            let model = modelWith (rows 1) |> step (DetailFormChanged DetailPanel)
            Expect.equal model.DetailForm DetailPanel "Panel"
            Expect.equal (step (DetailFormChanged DetailSheet) model).DetailForm DetailSheet "Sheet"

        testCase "detailEntryOf liest den aktuellen Stand aus der Liste, sonst die Momentaufnahme" <| fun () ->
            let opened = modelWith (rows 3) |> step (OpenDetail (entry 1))
            let reviewed =
                { opened with
                    Entries = Success [ { entry 1 with NeedsReview = false }; entry 2; entry 3 ] }
            Expect.equal (detailEntryOf reviewed |> Option.map (fun e -> e.NeedsReview)) (Some false) "aus der Liste"
            let gone = { opened with Entries = Success [ entry 2; entry 3 ] }
            Expect.equal (detailEntryOf gone |> Option.map (fun e -> e.Id)) (Some (idOf 1)) "Momentaufnahme"
    ]

// ============================================
// Tastatur bei offenem Panel und Sheet
// ============================================

[<Tests>]
let keyboardTests =
    testList "Detail: Tastatur (ledger-q1hw2, ADR 0053 Punkt 5)" [

        testCase "Enter auf der markierten Zeile öffnet das Detail" <| fun () ->
            let model, sent =
                { modelWith (rows 3) with FocusedRow = Some (idOf 2) } |> run OpenFocusedDetail
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "Detail der markierten Zeile"
            Expect.equal sent [ LoadHistory (idOf 2) ] "Historie wird geholt"

        testCase "Enter ohne Markierung öffnet nichts" <| fun () ->
            Expect.isNone (modelWith (rows 3) |> step OpenFocusedDetail).DetailFor "keine Markierung, kein Detail"

        testCase "bei offenem Panel bewegt Pfeil ab die Markierung, das Panel folgt und lädt die Historie" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))
            let model, sent = run (MoveFocus 1) opened
            Expect.equal model.FocusedRow (Some (idOf 2)) "Markierung bewegt"
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "Panel zeigt die neue Buchung"
            Expect.equal model.History Loading "Historie lädt neu"
            Expect.equal sent [ LoadHistory (idOf 2) ] "Abruf für die neue Buchung"

        testCase "bei offenem Panel setzt G den Prüfvermerk der Buchung im Panel, danach folgt das Panel der Markierung" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))
            let model, sent = run (ReviewKeyPressed Reviewed) opened
            Expect.contains sent (RunSetReviewMark (idOf 1, Reviewed)) "Befehl für die Buchung im Panel"
            Expect.equal model.ReviewMarkRows.[idOf 1] ReviewMarkSending "Zeile sendet"
            Expect.equal model.FocusedRow (Some (idOf 2)) "Markierung wie ohne Panel auf der nächsten Zeile"
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "Panel folgt"

        testCase "Esc schließt das Panel" <| fun () ->
            let model = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1)) |> step EscapePressed
            Expect.isNone model.DetailFor "Panel zu"
            Expect.equal model.FocusedRow (Some (idOf 1)) "die Markierung bleibt"

        testCase "Esc schließt auch ein offenes Sheet" <| fun () ->
            let model = modelWith (rows 3) |> asSheet |> step (OpenDetail (entry 1)) |> step EscapePressed
            Expect.isNone model.DetailFor "Sheet zu"

        testCase "Esc bei offenem Popover lässt das Detail stehen" <| fun () ->
            let model =
                { (modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))) with OpenPopover = Some CategoryChip }
                |> step EscapePressed
            Expect.isNone model.OpenPopover "Esc schließt das Popover im Reducer (erste Stufe der Kette)"
            Expect.isSome model.DetailFor "das Detail bleibt stehen, erst das nächste Esc schließt es"

        testCase "bei offenem Sheet bleiben die Listentasten gesperrt" <| fun () ->
            let opened = modelWith (rows 3) |> asSheet |> step (OpenDetail (entry 1))
            Expect.equal (step (MoveFocus 1) opened).FocusedRow (Some (idOf 1)) "Pfeil bewegt nichts"
            let afterG, sent = run (ReviewKeyPressed Reviewed) opened
            Expect.isEmpty sent "G löst nichts aus"
            Expect.isEmpty afterG.ReviewMarkRows "keine Zeile sendet"
            Expect.equal (step OpenFocusedDetail opened).DetailFor opened.DetailFor "Enter wechselt nichts"

        testCase "bei offenem Panel sind die Listentasten an" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))
            Expect.equal (step (MoveFocus 1) opened).FocusedRow (Some (idOf 2)) "Pfeil bewegt"
    ]

// ============================================
// Sortierwechsel, Neuladen
// ============================================

[<Tests>]
let sortTests =
    testList "Detail: Sortierwechsel und Neuladen (ledger-q1hw2, ledger-bn913)" [

        testCase "ein Sortierwechsel lässt das offene Panel mit seiner Buchung stehen" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 2))
            let sorted = opened |> step (SetSort SortByAmount)
            Expect.equal (sorted.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "dieselbe Buchung"
            Expect.equal sorted.FocusedRow (Some (idOf 2)) "die Markierung bleibt"

        testCase "steht die Buchung nach dem Neuladen nicht auf den geladenen Seiten, bleibt das Panel und keine Zeile ist markiert" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 2))
            let page : LedgerEntryPage =
                { Entries = [ entry 1; entry 3 ]
                  TotalCount = 40
                  TotalAmount = { Amount = -400m; Currency = "EUR" }
                  DeletedCount = 0
                  NeedsReviewCount = 2
                  AskLenaCount = 0; WithoutCategoryCount = 0 }
            let model = opened |> step (SetSort SortByAmount) |> (fun m -> m |> step (EntriesPageLoaded (m.EntriesGeneration, 0, Ok page)))
            Expect.equal (model.DetailFor |> Option.map (fun e -> e.Id)) (Some (idOf 2)) "Panel zeigt weiter seine Buchung"
            Expect.isNone model.FocusedRow "keine Zeile markiert"

        testCase "steht die Buchung nach dem Neuladen in der Liste, ist sie dort markiert" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 2))
            let page : LedgerEntryPage =
                { Entries = [ entry 3; entry 2; entry 1 ]
                  TotalCount = 3
                  TotalAmount = { Amount = -60m; Currency = "EUR" }
                  DeletedCount = 0
                  NeedsReviewCount = 3
                  AskLenaCount = 0; WithoutCategoryCount = 0 }
            let model = opened |> step (SetSort SortByAmount) |> (fun m -> m |> step (EntriesPageLoaded (m.EntriesGeneration, 0, Ok page)))
            Expect.equal model.FocusedRow (Some (idOf 2)) "die Markierung ist die Buchung im Panel"

        testCase "der Geprüft-Schalter und das Panel zeigen den neuen Stand, auch wenn die Zeile den Chip verlässt" <| fun () ->
            let start = { (modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))) with OnlyNeedsReview = true }
            let model = start |> step (StartSetReviewMark (idOf 1, Reviewed)) |> step (ReviewMarkSet (idOf 1, Reviewed, Ok okOutcome))
            Expect.isFalse (model.Entries |> RemoteData.withDefault [] |> List.exists (fun e -> e.Id = idOf 1)) "Zeile ist aus der Liste"
            Expect.equal (detailEntryOf model |> Option.map (fun e -> e.NeedsReview)) (Some false) "Detail zeigt geprüft"

        testCase "scheitert der Rückschrieb, zeigt das Detail den Fehlschlag wie die Zeile" <| fun () ->
            let failed = { okOutcome with WriteBack = ReviewMarkSendFailed "YNAB 500" }
            let model =
                modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))
                |> step (StartSetReviewMark (idOf 1, Reviewed))
                |> step (ReviewMarkSet (idOf 1, Reviewed, Ok failed))
            Expect.equal (detailEntryOf model |> Option.bind (fun e -> e.ReviewMarkWriteBackFailure)) (Some "YNAB 500") "Fehlschlag im Detail"
            Expect.equal (model.DetailFor |> Option.bind (fun e -> e.ReviewMarkWriteBackFailure)) (Some "YNAB 500") "auch in der Momentaufnahme"
    ]

// ============================================
// Form des Details und Spalten neben dem Panel
// ============================================

let private columnsOf (layout: TableLayout) : LedgerColumn list =
    match layout with
    | CompactList -> []
    | Columns cols -> cols |> List.map fst

[<Tests>]
let formTests =
    testList "Spalten neben dem Panel, das ab 1024 px immer steht" [

        testCase "Memo erst ab 1500 px Fensterbreite, darunter in keiner Spaltenbreite" <| fun () ->
            for account in [ true; false ] do
                for width in [ 600.0 .. 50.0 .. 3000.0 ] do
                    Expect.isFalse
                        (columnsOf (visibleColumnsBesidePanel 1499.0 width account) |> List.contains MemoColumn)
                        $"Fenster 1499 px, Spalte {width} px, Gesamtsicht {account}"
                Expect.contains (columnsOf (visibleColumnsBesidePanel 1500.0 3000.0 account)) MemoColumn $"Fenster 1500 px, Gesamtsicht {account}"

        testCase "ab 1500 px nur, wenn die Stufe mit Memo neben dem Panel passt" <| fun () ->
            // Ein Konto: Kästchen 44, Datum 96, Payee 140, Kategorie 120, Memo 120, Betrag 132 = 652 px neben 420 px Panel und 4 px Abstand.
            Expect.contains (columnsOf (visibleColumnsBesidePanel 1600.0 1076.0 false)) MemoColumn "1076 px: Memo passt"
            Expect.isFalse (columnsOf (visibleColumnsBesidePanel 1600.0 1075.0 false) |> List.contains MemoColumn) "1075 px: Memo entfällt"
            // Gesamtsicht: dazu die Konto-Spalte (110 px), also 762 px.
            Expect.contains (columnsOf (visibleColumnsBesidePanel 1600.0 1186.0 true)) MemoColumn "1186 px: Memo passt"
            Expect.isFalse (columnsOf (visibleColumnsBesidePanel 1600.0 1185.0 true) |> List.contains MemoColumn) "1185 px: Memo entfällt"

        testCase "ohne Platz für eine Stufe neben dem Panel steht die Kompaktliste" <| fun () ->
            // Kleinste Stufe ohne Memo und Konto: 532 px.
            for account in [ true; false ] do
                Expect.equal (visibleColumnsBesidePanel 1440.0 955.0 account) CompactList $"955 px, Gesamtsicht {account}"
                Expect.notEqual (visibleColumnsBesidePanel 1440.0 956.0 account) CompactList $"956 px, Gesamtsicht {account}"

        testCase "die übrigen Spalten folgen visibleColumns mit der Breite minus 424 px (Panel und Abstand)" <| fun () ->
            for width in [ 1000.0 .. 25.0 .. 2200.0 ] do
                let plain = columnsOf (visibleColumns (width - 424.0) true) |> List.filter ((<>) MemoColumn)
                let besidePanel = columnsOf (visibleColumnsBesidePanel 1440.0 width true)
                if not (List.isEmpty plain) then
                    Expect.equal besidePanel plain $"{width}px"
    ]

// ============================================
// Hinweisleiste
// ============================================

let private noticeOf (rowState: ReviewMarkRowState option) (history: RemoteData<LedgerHistoryLine list>) e =
    detailNoticeFor rowState history e

[<Tests>]
let noticeTests =
    testList "Detail: Hinweisleiste (ledger-q1hw2)" [

        let linked = entry 1

        testCase "gelöscht schlägt Noch nicht in YNAB, das schlägt Prüfvermerk nicht in YNAB" <| fun () ->
            let everything =
                { linked with
                    DeletedInYnab = true
                    LinkedToYnab = false
                    ReviewMarkWriteBackFailure = Some "YNAB 500" }
            match noticeOf None NotAsked everything with
            | Some (DeletedNotice _) -> ()
            | other -> failtestf "Erwartet DeletedNotice, war %A" other
            let notLinked = { everything with DeletedInYnab = false }
            match noticeOf None NotAsked notLinked with
            | Some (NotInYnabNotice _) -> ()
            | other -> failtestf "Erwartet NotInYnabNotice, war %A" other
            let markOnly = { notLinked with LinkedToYnab = true }
            match noticeOf None NotAsked markOnly with
            | Some (ReviewMarkNotice hint) -> Expect.equal hint.Reason "YNAB 500" "Grund des Prüfvermerks"
            | other -> failtestf "Erwartet ReviewMarkNotice, war %A" other

        testCase "eine verknüpfte, nicht gelöschte Buchung ohne Fehlschlag hat keine Leiste" <| fun () ->
            Expect.isNone (noticeOf None NotAsked linked) "keine Leiste"

        testCase "Noch nicht in YNAB mit gescheitertem Push nennt Datum, Uhrzeit und Grund und hat Erneut senden" <| fun () ->
            let failure : LedgerPushFailure =
                { Reason = "Netzwerkfehler"; FailedAt = DateTime(2026, 9, 10, 18, 2, 0, DateTimeKind.Local) }
            let text = notInYnabNoticeReason (Some failure)
            Expect.equal
                text
                "Push gescheitert am 10.09.2026 18:02: Netzwerkfehler. Erneut senden schickt die Buchung sofort."
                "Satz mit Zeitpunkt und Grund"
            let notice = noticeOf None NotAsked { linked with LinkedToYnab = false; PushToYnabFailure = Some failure }
            Expect.equal
                (notice |> Option.bind (detailNoticeActionMsg linked.Id))
                (Some (StartRetryPush linked.Id))
                "Erneut senden mit sofortigem Push"

        testCase "ein Grund, der schon mit Punkt endet, bekommt keinen doppelten Punkt" <| fun () ->
            let failure : LedgerPushFailure = { Reason = "Zeitüberschreitung."; FailedAt = DateTime(2026, 9, 10, 18, 2, 0, DateTimeKind.Local) }
            Expect.stringContains (notInYnabNoticeReason (Some failure)) "Zeitüberschreitung. Erneut senden" "ein Punkt"
            Expect.isFalse ((notInYnabNoticeReason (Some failure)).Contains "..") "kein Doppelpunkt-Paar"

        testCase "Noch nicht in YNAB ohne Fehlschlag sagt, dass noch nichts gesendet wurde" <| fun () ->
            Expect.equal (notInYnabNoticeReason None) "Noch nicht an YNAB gesendet." "Satz ohne Fehlschlag"

        testCase "die rote Leiste nennt den Löschzeitpunkt aus der Historie" <| fun () ->
            let at = DateTime(2026, 9, 11, 8, 20, 0, DateTimeKind.Local)
            let history =
                Success
                    [ { historyLine 1 ImportedFromComdirect "Comdirect-Import: Buchung erfasst" with OccurredAt = at.AddDays -1.0 }
                      { historyLine 2 MirroredFromYnab "In YNAB gelöscht" with OccurredAt = at } ]
            Expect.equal
                (deletedNoticeReason history)
                "Gelöscht in YNAB am 11.09.2026 08:20. Zählt nicht zum Kontostand."
                "Zeitpunkt aus der Historie"
            Expect.equal (deletedNoticeReason Loading) "Gelöscht in YNAB. Zählt nicht zum Kontostand." "ohne geladene Historie"

        testCase "Erneut senden in der Prüfvermerk-Leiste ruft retryReviewMarkWriteBack für diese Buchung" <| fun () ->
            let withFailure = { linked with ReviewMarkWriteBackFailure = Some "YNAB 500" }
            let notice = noticeOf None NotAsked withFailure
            let msg = notice |> Option.bind (detailNoticeActionMsg withFailure.Id)
            Expect.equal msg (Some (StartRetryReviewMark withFailure.Id)) "Nachricht für genau diese Buchung"
            let model = { modelWith [ withFailure ] with DetailFor = Some withFailure }
            let sending, sent = run (StartRetryReviewMark withFailure.Id) model
            Expect.equal sent [ RunRetryReviewMark withFailure.Id ] "der Befehl läuft"
            Expect.equal sending.ReviewMarkRows.[withFailure.Id] ReviewMarkSending "Zeile sendet"

        testCase "gelöscht hat keine Aktion, Noch nicht in YNAB hat Erneut senden" <| fun () ->
            Expect.isNone (detailNoticeActionMsg linked.Id (DeletedNotice "x")) "gelöscht"
            Expect.equal
                (detailNoticeActionMsg linked.Id (NotInYnabNotice "x"))
                (Some (StartRetryPush linked.Id))
                "Noch nicht in YNAB"

        testCase "der Geprüft-Schalter ruft setReviewMark Reviewed beziehungsweise Unreviewed" <| fun () ->
            Expect.equal (reviewedSwitchMsg linked.Id true) (StartSetReviewMark (linked.Id, Reviewed)) "an"
            Expect.equal (reviewedSwitchMsg linked.Id false) (StartSetReviewMark (linked.Id, Unreviewed)) "aus"
            let model = modelWith [ linked ] |> step (OpenDetail linked)
            let sending, sent = run (reviewedSwitchMsg linked.Id true) model
            Expect.equal sent [ RunSetReviewMark (linked.Id, Reviewed) ] "derselbe Weg wie die Schaltfläche"
            Expect.equal sending.ReviewMarkRows.[linked.Id] ReviewMarkSending "Zeile sendet"
            let done' = sending |> step (ReviewMarkSet (linked.Id, Reviewed, Ok okOutcome))
            Expect.equal (detailEntryOf done' |> Option.map (fun e -> e.NeedsReview)) (Some false) "Detail aktualisiert"
            Expect.equal (done'.Entries |> RemoteData.withDefault [] |> List.head).NeedsReview false "Listenzeile aktualisiert"
    ]

// ============================================
// Felder, Herkunft, Historie
// ============================================

let private transferSplit : TransactionSplit =
    { Target = ToTransfer (YnabAccountId (Guid.NewGuid()), "Tagesgeld")
      Amount = { Amount = -20m; Currency = "EUR" }
      Memo = None }

let private categorySplit : TransactionSplit =
    { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Lebensmittel")
      Amount = { Amount = -30.5m; Currency = "EUR" }
      Memo = None }

[<Tests>]
let contentTests =
    testList "Detail: Felder, Herkunft und Historie (ledger-q1hw2)" [

        testCase "eine Buchung mit zwei Split-Zeilen zeigt beide mit Ziel und Betrag" <| fun () ->
            let split = { entry 1 with Splits = [ categorySplit; transferSplit ] }
            Expect.equal
                (splitLineTexts split)
                [ "Lebensmittel", "-30,50 €"; "Übertrag: Tagesgeld", "-20,00 €" ]
                "Ziel und Betrag je Zeile (leere Map, nur der Blattname)"

        testCase "Split-Zeilen im Detail: Kategorie qualifiziert, Rest unverändert (ledger-b7qmx, ADR 0045)" <| fun () ->
            let (YnabCategoryId bekannt) = YnabCategoryId (Guid.NewGuid())
            let mitGruppe : TransactionSplit =
                { Target = ToCategory (YnabCategoryId bekannt, "Lebensmittel")
                  Amount = { Amount = -30.5m; Currency = "EUR" }
                  Memo = None }
            let ohneGruppe = { categorySplit with Amount = { Amount = -5m; Currency = "EUR" } }
            let offen = { transferSplit with Target = Uncategorized }
            let split =
                { entry 1 with
                    Splits = [ mitGruppe; ohneGruppe; transferSplit; offen ]
                    SplitCategoryGroupNames = Map.ofList [ bekannt, "Alltag" ] }
            Expect.equal
                (splitLineTexts split)
                [ "Alltag: Lebensmittel", "-30,50 €"
                  "Lebensmittel", "-5,00 €"
                  "Übertrag: Tagesgeld", "-20,00 €"
                  "kein Ziel", "-20,00 €" ]
                "qualifiziert nur mit Map-Eintrag, Übertrag und kein Ziel unverändert"

        testCase "die Listenzeile und die Kategorie-Feldzeile einer Buchung mit Splits bleiben unverändert" <| fun () ->
            let id = Guid.NewGuid()
            let zeile : TransactionSplit =
                { Target = ToCategory (YnabCategoryId id, "Lebensmittel")
                  Amount = { Amount = -30m; Currency = "EUR" }
                  Memo = None }
            let split =
                { entry 1 with
                    CategoryName = None
                    CategoryGroupName = None
                    Splits = [ zeile; zeile ]
                    SplitCategoryGroupNames = Map.ofList [ id, "Alltag" ] }
            Expect.equal (categoryLabel split) "2 Split-Zeilen" "Handy-Zeile"
            Expect.equal (categoryLabelQualified split) "2 Split-Zeilen" "PC-Zeile"

        testCase "ohne Splits gibt es keine Zeilen" <| fun () ->
            Expect.isEmpty (splitLineTexts (entry 1)) "keine Zeilen"

        testCase "die Herkunft-Zeile zeigt für jede Art das Wort der Domäne" <| fun () ->
            Expect.equal (originLabel ImportedFromComdirect) "Comdirect-Import" "Comdirect-Import"
            Expect.equal (originLabel DecidedByRule) "Regel" "Regel"
            Expect.equal (originLabel MirroredFromYnab) "YNAB-Spiegel" "YNAB-Spiegel"
            // Das Wort kommt aus `originLabel` (Domäne): in BB entschieden heißt dort "Roman in BB".
            Expect.equal (originLabel DecidedInBb) "Roman in BB" "In BB entschieden"

        testCase "die Historie erscheint neueste zuerst" <| fun () ->
            let lines =
                [ historyLine 1 ImportedFromComdirect "Comdirect-Import: Buchung erfasst"
                  historyLine 2 DecidedByRule "Regel: Kategorie Lebensmittel gesetzt"
                  historyLine 3 MirroredFromYnab "Kategorie in YNAB geändert" ]
            let items = historyTimelineItems lines
            Expect.equal (items |> List.map (fun i -> i.Text))
                [ "Kategorie in YNAB geändert"; "Regel: Kategorie Lebensmittel gesetzt"; "Comdirect-Import: Buchung erfasst" ]
                "neueste oben"

        testCase "die Punktfarbe folgt dem Urheber" <| fun () ->
            let dot origin description = historyDotFor (historyLine 1 origin description)
            Expect.equal (dot ImportedFromComdirect "Comdirect-Import: Buchung erfasst") Detail.DotInfo "Import blau"
            Expect.equal (dot DecidedByRule "Regel: Kategorie gesetzt") Detail.DotInfo "Regel blau"
            Expect.equal (dot MirroredFromYnab "Kategorie in YNAB geändert") Detail.DotGrey "Spiegel grau"
            Expect.equal (dot DecidedInBb "In BB als geprüft markiert") Detail.DotGreen "eigene Prüfung grün"

        testCase "Fehlschläge und die Löschung sind rot, gleich welcher Urheber" <| fun () ->
            let dot origin description = historyDotFor (historyLine 1 origin description)
            Expect.equal (dot MirroredFromYnab "In YNAB gelöscht") Detail.DotRed "Löschung"
            Expect.equal (dot DecidedInBb "Push nach YNAB gescheitert: Netzwerkfehler") Detail.DotRed "Push gescheitert"
            Expect.equal (dot DecidedInBb "Prüfvermerk nicht in YNAB: Fehler") Detail.DotRed "Prüfvermerk gescheitert"

        testCase "die rote Punktfarbe hängt an den echten Sätzen des Servers (QueryHandlers.describe)" <| fun () ->
            let dotOf body =
                historyDotFor (historyLine 1 DecidedInBb (ReadModel.describe body))
            Expect.equal (dotOf EntryDeletionObservedInYnab) Detail.DotRed "Löschung"
            Expect.equal (dotOf (EntryPushToYnabFailed { Reason = "Netzwerkfehler" })) Detail.DotRed "Push gescheitert"
            Expect.equal
                (dotOf (EntryReviewMarkWriteBackFailed { Reason = "YNAB 500" }))
                Detail.DotRed
                "Prüfvermerk nicht in YNAB"
            Expect.notEqual
                (dotOf EntryReviewMarkWrittenBackToYnab)
                Detail.DotRed
                "ein gewöhnlicher Satz ist nicht rot"
    ]

// ============================================
// Gelöschte Buchung (Lesezustand)
// ============================================

[<Tests>]
let deletedTests =
    testList "Detail: gelöschte Buchung ist ein Lesezustand (ledger-q1hw2)" [

        testCase "eine gelöschte Buchung ist ein Lesezustand, eine offene nicht" <| fun () ->
            // ledger-r8h19: ersetzt den Quelltext-Test; Betrag, Text und Geprüft-Schalter des
            // Details hängen nur an `detailReadOnly`.
            Expect.isTrue (detailReadOnly { entry 1 with DeletedInYnab = true }) "gelöscht: gedämpft, kein Schalter"
            Expect.isFalse (detailReadOnly (entry 1)) "offen: Schalter und voller Betrag"

        testCase "die gelöschte Buchung bekommt trotzdem beide Filter-Knöpfe" <| fun () ->
            let deleted = { entry 1 with DeletedInYnab = true; Payee = Some "Café Lotte"; CategoryId = Some (Guid.NewGuid()); CategoryName = Some "Restaurants" }
            Expect.isSome (payeeFilterLabel deleted) "Payee"
            Expect.isSome (categoryFilterLabel deleted) "Kategorie"
    ]

// ============================================
// Externe Links im Buchungs-Detail (ledger-rjhwp)
// ============================================

[<Tests>]
let externalLinksTests =
    let withLinks (links: ExternalLink list) = { entry 1 with ExternalLinks = links }
    let amazon : ExternalLink =
        { Label = "Bestellung 304-1234567-7654321"
          Url = "https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID=304-1234567-7654321" }
    let paypal : ExternalLink = { Label = "PayPal Activity"; Url = "https://www.paypal.com/activities" }

    testList "Detail: externe Links (ledger-rjhwp)" [
        testCase "jeder Eintrag aus ExternalLinks wird zu Beschriftung und Adresse" <| fun () ->
            Expect.equal
                (detailExternalLinks (withLinks [ amazon; paypal ]))
                [ amazon.Label, amazon.Url; paypal.Label, paypal.Url ]
                "beide Links in Reihenfolge"

        testCase "bei leerer Liste baut das Detail keine Link-Zeile" <| fun () ->
            Expect.isEmpty (detailExternalLinks (withLinks [])) "keine Links"

        testCase "die Antwort eines älteren Servers ohne das Feld (null) ergibt keine Zeile" <| fun () ->
            let old = { entry 1 with ExternalLinks = Unchecked.defaultof<ExternalLink list> }
            Expect.isEmpty (detailExternalLinks old) "null wird wie leer behandelt"
    ]

// ============================================
// Route einer Buchung (budgeting-m1bce)
// ============================================

[<Tests>]
let routeEntryTests =
    let pending (n: int) (model: Model) = { model with PendingRouteEntry = Some (idOf n) }
    let answer (n: int) = RouteEntryLoaded (idOf n, Ok (Some (entry n)))
    let detailId (model: Model) = model.DetailFor |> Option.map (fun e -> e.Id)
    let otherAccount = Guid.NewGuid()
    let accountView (id: Guid) (name: string) : LedgerAccountView =
        { Id = id; Name = name; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false }
    let accounts : LedgerAccountsView =
        { Accounts = [ accountView (entry 7).AccountId "Konto A"; accountView otherAccount "Konto B" ]
          DefaultAccountId = Some otherAccount }

    testList "Detail: Route einer Buchung" [

        testCase "eine Buchung außerhalb des geladenen Ausschnitts öffnet ihr Detail" <| fun () ->
            let model, sent = modelWith (rows 1) |> pending 7 |> run (answer 7)
            Expect.equal (detailId model) (Some (idOf 7)) "Detail dieser Buchung"
            Expect.isNone model.PendingRouteEntry "nichts mehr offen"
            Expect.contains sent (LoadHistory (idOf 7)) "Historie wird geholt"

        testCase "fremdes Konto: der Filter springt auf das Konto der Buchung und die Liste lädt neu" <| fun () ->
            let start = { modelWith (rows 1) with SelectedFilter = Some (OneAccount otherAccount) }
            let model, sent = start |> pending 7 |> run (answer 7)
            Expect.equal model.SelectedFilter (Some (OneAccount (entry 7).AccountId)) "Konto der Buchung"
            Expect.contains sent (RunLoadEntriesPage 0) "Liste lädt"
            Expect.equal (detailId model) (Some (idOf 7)) "Detail bleibt offen"

        testCase "der Sprung ändert die gemerkte Sicht des Geräts nicht" <| fun () ->
            let start =
                { modelWith (rows 1) with
                    SelectedFilter = Some (OneAccount otherAccount)
                    RememberedFilter = Some (OneAccount otherAccount) }
            let model, _ = start |> pending 7 |> run (answer 7)
            Expect.equal model.RememberedFilter (Some (OneAccount otherAccount)) "bleibt"

        testCase "Alle Konten bleibt stehen, ohne die Liste neu zu laden" <| fun () ->
            let start = { modelWith (rows 1) with SelectedFilter = Some AllAccounts }
            let model, sent = start |> pending 7 |> run (answer 7)
            Expect.equal model.SelectedFilter (Some AllAccounts) "Alle Konten"
            Expect.isFalse (List.contains LoadEntries sent) "kein Neuladen"

        testCase "das eigene Konto bleibt stehen, ohne die Liste neu zu laden" <| fun () ->
            let start = { modelWith (rows 1) with SelectedFilter = Some (OneAccount (entry 7).AccountId) }
            let _, sent = start |> pending 7 |> run (answer 7)
            Expect.isFalse (List.contains LoadEntries sent) "kein Neuladen"

        testCase "eine laufende Auswahl endet, das Detail bleibt offen" <| fun () ->
            let start = { modelWith (rows 1) with SelectedFilter = Some AllAccounts; SelectionMode = true }
            let model, _ = start |> pending 7 |> run (answer 7)
            Expect.isFalse model.SelectionMode "keine Auswahl"
            Expect.equal (detailId model) (Some (idOf 7)) "Detail offen"

        testCase "eine unbekannte Buchung meldet sich, ohne ein Detail zu öffnen" <| fun () ->
            let start = modelWith (rows 1) |> pending 7
            let model, _, external = Components.Ledger.State.update (RouteEntryLoaded (idOf 7, Ok None)) start
            Expect.isNone model.DetailFor "kein Detail"
            Expect.isNone model.PendingRouteEntry "nichts mehr offen"
            Expect.equal external (ShowToast ("Buchung nicht gefunden", ToastWarning)) "Hinweis"

        testCase "ein Fehler meldet sich, ohne ein Detail zu öffnen" <| fun () ->
            let start = modelWith (rows 1) |> pending 7
            let model, _, external = Components.Ledger.State.update (RouteEntryLoaded (idOf 7, Error "weg")) start
            Expect.isNone model.PendingRouteEntry "nichts mehr offen"
            Expect.equal external (ShowToast ("Buchung nicht geladen: weg", ToastError)) "Grund"

        testCase "eine späte Antwort nach dem Verlassen der Route öffnet nichts" <| fun () ->
            let left = modelWith (rows 1) |> pending 7 |> step (LeaveRouteEntry OtherPage)
            Expect.isNone (left |> step (answer 7)).DetailFor "bleibt zu"

        testCase "die Antwort zu einer überholten Route öffnet nichts" <| fun () ->
            let model = modelWith (rows 1) |> pending 8 |> step (answer 7)
            Expect.isNone model.DetailFor "Buchung 8 wird erwartet"

        testCase "das Verlassen der Route schließt das Detail" <| fun () ->
            let model = modelWith (rows 1) |> pending 7 |> step (answer 7) |> step (LeaveRouteEntry OtherPage)
            Expect.isNone model.DetailFor "zu"

        // F3: Die späte Antwort warf neuere Eingaben weg.
        testCase "Ein Kontowechsel überholt die Route: die späte Antwort ändert nichts mehr" <| fun () ->
            let chosen = modelWith (rows 1) |> pending 7 |> step (SelectFilter (OneAccount otherAccount))
            let model = chosen |> step (answer 7)
            Expect.equal model.SelectedFilter (Some (OneAccount otherAccount)) "der gewählte Filter bleibt"
            Expect.isNone model.DetailFor "die Buchung geht nicht auf"

        testCase "Ein offenes Detail überholt die Route: es bleibt bei der gewählten Buchung" <| fun () ->
            let model = modelWith (rows 2) |> pending 7 |> step (OpenDetail (entry 2)) |> step (answer 7)
            Expect.equal (detailId model) (Some (idOf 2)) "gewählte Buchung"

        testCase "Enter auf einer Zeile überholt die Route" <| fun () ->
            let start = { (modelWith (rows 2) |> pending 7) with FocusedRow = Some (idOf 2) }
            let model = start |> step OpenFocusedDetail |> step (answer 7)
            Expect.equal (detailId model) (Some (idOf 2)) "gewählte Buchung"

        testCase "Eine beginnende Auswahl überholt die Route: sie bleibt bestehen" <| fun () ->
            let model = modelWith (rows 2) |> pending 7 |> step (StartSelectionMode (idOf 2)) |> step (answer 7)
            Expect.isTrue model.SelectionMode "Auswahl läuft"
            Expect.isNone model.DetailFor "die Buchung geht nicht auf"

        // F4: Der Filter blieb nach dem Sprung auf dem Konto der Buchung, die gemerkte Sicht fehlte.
        testCase "Wer das Kassenbuch verlässt, lässt den Filter des Sprungs zurück" <| fun () ->
            let jumped = { modelWith (rows 1) with RememberedFilter = Some AllAccounts } |> pending 7 |> step (answer 7)
            let back = jumped |> step (LeaveRouteEntry OtherPage) |> step (AccountsLoaded (Ok accounts))
            Expect.equal back.SelectedFilter (Some AllAccounts) "die gemerkte Sicht gilt wieder"

        testCase "Von der Route in die Liste bleibt der Filter" <| fun () ->
            let jumped = modelWith (rows 1) |> pending 7 |> step (answer 7)
            let model = jumped |> step (LeaveRouteEntry LedgerList)
            Expect.equal model.SelectedFilter jumped.SelectedFilter "bleibt"
            Expect.isNone model.DetailFor "Detail zu"

        testCase "Die Antwort der Route kommt vor den Konten: Filter und Detail stehen" <| fun () ->
            let model = modelWith (rows 1) |> pending 7 |> step (answer 7) |> step (AccountsLoaded (Ok accounts))
            Expect.equal model.SelectedFilter (Some (OneAccount (entry 7).AccountId)) "Konto der Buchung"
            Expect.equal (detailId model) (Some (idOf 7)) "Detail offen"

        testCase "Die Konten kommen vor der Antwort der Route: Filter und Detail stehen" <| fun () ->
            let start = { modelWith (rows 1) with RememberedFilter = Some (OneAccount otherAccount) }
            let model = start |> pending 7 |> step (AccountsLoaded (Ok accounts)) |> step (answer 7)
            Expect.equal model.SelectedFilter (Some (OneAccount (entry 7).AccountId)) "Konto der Buchung"
            Expect.equal (detailId model) (Some (idOf 7)) "Detail offen"

        testCase "Nur eine gescheiterte Antwort der laufenden Abfrage zählt als Ladefehler" <| fun () ->
            let id = idOf 7
            Expect.isTrue (routeEntryLoadFailed (Some id) (RouteEntryLoaded (id, Ok None))) "unbekannt"
            Expect.isTrue (routeEntryLoadFailed (Some id) (RouteEntryLoaded (id, Error "weg"))) "Fehler"
            Expect.isFalse (routeEntryLoadFailed None (RouteEntryLoaded (id, Error "weg"))) "verlassen"
            Expect.isFalse (routeEntryLoadFailed (Some id) (answer 7)) "geladen"
    ]

// ============================================
// Historie an Ladevorgänge gebunden (ledger-nc708)
// ============================================

/// Ein Schritt durch das echte `update`, ohne die Befehle auszuführen: das Laden der Historie baut den
/// Aufruf an den Server, die Antwort kommt im Test über `historyAnswer`.
let private start (msg: Msg) (model: Model) : Model =
    let model', _, _ = Components.Ledger.State.update msg model
    model'

let private h1 = historyLine 1 ImportedFromComdirect "Comdirect-Import: Buchung erfasst"
let private h2 = historyLine 2 ImportedFromComdirect "Kategorie geändert"
let private h3 = historyLine 3 ImportedFromComdirect "Memo geändert"

/// Detail von Buchung 1 offen, Historie `Success [h1]`.
let private openWithHistory () : Model =
    { (modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))) with History = Success [ h1 ] }

[<Tests>]
let historyGenerationTests =
    testList "Detail: Historie an Ladevorgänge gebunden (ledger-nc708)" [

        testCase "kommt die Antwort des zweiten Ladens vor der des ersten, zählt nur die des zweiten" <| fun () ->
            let first = openWithHistory () |> start (LoadHistory (idOf 1))
            let firstAnswer = Components.Ledger.State.historyAnswer (idOf 1) first.HistoryGeneration
            let second = first |> start (LoadHistory (idOf 1))
            let secondAnswer = Components.Ledger.State.historyAnswer (idOf 1) second.HistoryGeneration
            let model = second |> step (secondAnswer (Ok [ h1; h2; h3 ])) |> step (firstAnswer (Ok [ h1; h2 ]))
            Expect.equal model.History (Success [ h1; h2; h3 ]) "die späte Antwort des ersten Ladens überschreibt nichts"
            let failedLate = second |> step (secondAnswer (Ok [ h1; h2; h3 ])) |> step (firstAnswer (Error "weg"))
            Expect.equal failedLate.History (Success [ h1; h2; h3 ]) "auch ihr Scheitern nicht"

        testCase "die Antwort bindet Buchung und Lade-Nummer beim Start" <| fun () ->
            let answer = Components.Ledger.State.historyAnswer (idOf 1) 7
            Expect.equal (answer (Ok [ h1 ])) (HistoryLoaded (idOf 1, 7, Ok [ h1 ])) "Erfolg"
            Expect.equal (answer (Error "weg")) (HistoryLoaded (idOf 1, 7, Error "weg")) "Scheitern"

        testCase "Nachladen derselben Buchung lässt die gezeigte Historie bis zur Antwort stehen" <| fun () ->
            let reloading = openWithHistory () |> start (LoadHistory (idOf 1))
            Expect.equal reloading.History (Success [ h1 ]) "kein „Historie wird geladen…“ beim Nachladen"
            let answer = Components.Ledger.State.historyAnswer (idOf 1) reloading.HistoryGeneration
            Expect.equal (reloading |> step (answer (Ok [ h1; h2 ]))).History (Success [ h1; h2 ]) "die Antwort ersetzt den Stand"
            Expect.equal (reloading |> step (answer (Error "weg"))).History (Success [ h1 ]) "ein Scheitern lässt den Stand"

        testCase "das Öffnen einer anderen Buchung lädt mit Loading, eine späte Antwort für die erste ändert nichts" <| fun () ->
            let reloading = openWithHistory () |> start (LoadHistory (idOf 1))
            let answer = Components.Ledger.State.historyAnswer (idOf 1) reloading.HistoryGeneration
            let other, sent = reloading |> run (OpenDetail (entry 2))
            Expect.equal other.History Loading "die neue Buchung lädt"
            Expect.equal sent [ LoadHistory (idOf 2) ] "Abruf für die neue Buchung"
            Expect.equal (other |> step (answer (Ok [ h1; h2 ]))).History Loading "die Antwort für die erste ist verworfen"

        testCase "ohne offenes Detail der Buchung lädt LoadHistory nichts" <| fun () ->
            let closed = openWithHistory () |> step CloseDetail
            let model, _, _ = Components.Ledger.State.update (LoadHistory (idOf 1)) closed
            Expect.equal model closed "nichts geändert"
    ]

/// Ein Ereignis der Aufzählung zur Historie.
type private HistoryEvent =
    | StartLoad of int
    | Answer of int
    | Close
    | OpenB

/// Was das Orakel weiß, ohne den getesteten Code zu fragen.
type private HistoryWalk =
    { /// Welche Buchung im Detail steht: 1 (A), 2 (B) oder keine.
      Open: int option
      /// Die Historie von A, wie sie zuletzt gezeigt wurde.
      ShownA: LedgerHistoryLine list
      /// Das zuletzt gestartete Laden von A, solange A offen ist.
      LastStarted: int option
      /// Die gestarteten Ladevorgänge (auch ohne offenes A, dann ohne Wirkung).
      Started: Set<int> }

let private answerLines (n: int) : LedgerHistoryLine list =
    if n = 1 then [ h1; h2 ] else [ h1; h2; h3 ]

let private historyOracle (w: HistoryWalk) : RemoteData<LedgerHistoryLine list> =
    match w.Open with
    | Some 1 -> Success w.ShownA
    | Some _ -> Loading
    | None -> NotAsked

let private historyNext (event: HistoryEvent) (w: HistoryWalk) : HistoryWalk =
    match event with
    | StartLoad n ->
        { w with Started = w.Started.Add n; LastStarted = (if w.Open = Some 1 then Some n else w.LastStarted) }
    | Answer n when w.Open = Some 1 && w.LastStarted = Some n -> { w with ShownA = answerLines n }
    | Answer _ -> w
    | Close -> { w with Open = None; LastStarted = None }
    | OpenB -> { w with Open = Some 2; LastStarted = None }

[<Tests>]
let historyWalkTests =
    testCase "Historie: jede Reihenfolge aus zwei Ladevorgängen von A, ihren Antworten, Schließen und Öffnen von B" <| fun () ->
        let all = [ StartLoad 1; StartLoad 2; Answer 1; Answer 2; Close; OpenB ]
        let mutable finished = 0
        let rec walk (path: string list) (model: Model) (w: HistoryWalk) (answers: Map<int, Msg>) (left: HistoryEvent list) =
            Expect.equal model.History (historyOracle w) (String.concat " → " (List.rev path))
            if List.isEmpty left then finished <- finished + 1
            for event in left do
                let ready =
                    match event with
                    | Answer n -> answers.ContainsKey n
                    | _ -> true
                if ready then
                    let model' =
                        match event with
                        | StartLoad _ -> start (LoadHistory (idOf 1)) model
                        | Answer n -> step answers.[n] model
                        | Close -> step CloseDetail model
                        | OpenB -> step (OpenDetail (entry 2)) model
                    let answers' =
                        match event with
                        | StartLoad n ->
                            let answer = Components.Ledger.State.historyAnswer (idOf 1) model'.HistoryGeneration
                            answers |> Map.add n (answer (Ok (answerLines n)))
                        | _ -> answers
                    walk ($"%A{event}" :: path) model' (historyNext event w) answers' (left |> List.filter ((<>) event))
        let w0 = { Open = Some 1; ShownA = [ h1 ]; LastStarted = None; Started = Set.empty }
        walk [] (openWithHistory ()) w0 Map.empty all
        Expect.equal finished 180 "alle gültigen Reihenfolgen (jede Antwort nach ihrem Start)"

// ============================================
// Jede Änderung an der offenen Buchung lädt ihre Historie nach (ledger-nc708)
// ============================================

let private categoryOk : CategoryChangeOutcome =
    { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }

let private flagOk : FlagOutcome =
    { Previous = { Color = None; Name = None }; Change = FlagChanged; WriteBack = FlagSent }

let private memoOk : MemoOutcome =
    { PreviousMemo = None; CurrentMemo = Some "Wocheneinkauf"; MemoChange = MemoChanged; MemoWriteBack = MemoSent }

let private eurOf (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private amountOk : AmountOutcome =
    { PreviousAmount = eurOf -10m; CurrentAmount = eurOf -12m; AmountChange = AmountChanged; AmountWriteBack = AmountSent }

let private payeeOk : PayeeOutcome =
    { PreviousPayee = Some "Bäckerei"; CurrentPayee = Some "Markt"; PayeeChange = PayeeChanged; PayeeWriteBack = PayeeSent }

let private splitLines : TransactionSplit list =
    [ { Target = ToCategory (YnabCategoryId (Guid.Parse "0195d8c0-0000-7000-8000-000000000001"), "Lebensmittel"); Amount = eurOf -6m; Memo = None }
      { Target = ToCategory (YnabCategoryId (Guid.Parse "0195d8c0-0000-7000-8000-000000000002"), "Drogerie"); Amount = eurOf -4m; Memo = None } ]

/// Jede erfolgreiche Antwort eines Einzelbefehls, einzeln genannt. Die Liste ist das Orakel.
let private changeAnswers : (string * (Guid -> Msg)) list =
    [ "EntryCategorySet", fun id -> EntryCategorySet (id, Some (YnabCategoryId (Guid.Parse "0195d8c0-0000-7000-8000-000000000001")), Ok categoryOk)
      "EntryCategoryRestored", fun id -> EntryCategoryRestored (id, None, Ok categoryOk)
      "EntryTransferSet", fun id -> EntryTransferSet (id, YnabAccountId (Guid.Parse "0195e8c0-0000-7000-8000-0000000000bb"), false, Ok categoryOk)
      "CategorizationRetried", fun id -> CategorizationRetried (id, Ok CategorySent)
      "LenaFlagSet", fun id -> LenaFlagSet (id, AssignLena, Ok flagOk)
      "FlagRestored", fun id -> FlagRestored (id, { Color = None; Name = None }, Ok flagOk)
      "FlagRetried", fun id -> FlagRetried (id, Ok FlagSent)
      "ReviewMarkSet", fun id -> ReviewMarkSet (id, Reviewed, Ok okOutcome)
      "ReviewMarkRetried", fun id -> ReviewMarkRetried (id, Ok ReviewMarkSent)
      "ReviewMarkRestored", fun id -> ReviewMarkRestored (id, okOutcome.Previous, Ok okOutcome)
      "SwipeReviewedDone", fun id -> SwipeReviewedDone (id, "Geprüft", Ok okOutcome)
      "PushRetried", fun id -> PushRetried (id, Ok PushToYnabSent)
      "MemoSet", fun id -> MemoSet (id, Ok memoOk)
      "MemoRestored", fun id -> MemoRestored (id, Ok memoOk)
      "MemoRetried", fun id -> MemoRetried (id, Ok MemoSent)
      "AmountSet", fun id -> AmountSet (id, Ok amountOk)
      "AmountRestored", fun id -> AmountRestored (id, Ok amountOk)
      "AmountRetried", fun id -> AmountRetried (id, Ok AmountSent)
      "PayeeSet", fun id -> PayeeSet (id, Ok payeeOk)
      "PayeeRestored", fun id -> PayeeRestored (id, Ok payeeOk)
      "PayeeRetried", fun id -> PayeeRetried (id, Ok PayeeSent)
      "SplitsSaved", fun id -> SplitsSaved (id, splitLines, Ok categoryOk)
      "SplitsRestored", fun id -> SplitsRestored (id, splitLines, Ok categoryOk)
      "SplitsWithAmountSaved", fun id -> SplitsWithAmountSaved (id, splitLines, eurOf -10m, Ok categoryOk) ]

/// Die Folgenachrichten eines Schritts; ein Aufruf an den Server läuft unter .NET nicht und bleibt außen vor.
let private sentBy (msg: Msg) (model: Model) : Msg list =
    let _, cmd, _ = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        try sub sent.Add with _ -> ()
    List.ofSeq sent

let private historyLoads (sent: Msg list) : Guid list =
    sent |> List.choose (function LoadHistory id -> Some id | _ -> None)

[<Tests>]
let afterChangeTests =
    testList "Detail: jede Änderung an der offenen Buchung lädt ihre Historie nach (ledger-nc708)" [
        for name, answer in changeAnswers do
            testCase $"{name}: Detail der Buchung offen lädt nach, andere oder keine Buchung offen nicht" <| fun () ->
                let opened = openWithHistory ()
                Expect.equal (historyLoads (sentBy (answer (idOf 1)) opened)) [ idOf 1 ] "Detail der Buchung offen"
                let other = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 2))
                Expect.isEmpty (historyLoads (sentBy (answer (idOf 1)) other)) "Detail einer anderen Buchung offen"
                let closed = opened |> step CloseDetail
                Expect.isEmpty (historyLoads (sentBy (answer (idOf 1)) closed)) "kein Detail offen"

        testCase "im Durchgehen lädt die Karte mit „Details“ offen nach, ohne „Details“ nicht" <| fun () ->
            let model, _ = Components.Ledger.State.init ()
            let opened = { model with Entries = Success (rows 3); DetailForm = DetailSheet } |> step ReviewOpened
            let reviewing = opened |> step (ReviewQueueLoaded (opened.ReviewGeneration, Ok [ entry 1; entry 2 ]))
            let answer = EntryCategorySet (idOf 1, Some (YnabCategoryId (Guid.Parse "0195d8c0-0000-7000-8000-000000000001")), Ok categoryOk)
            Expect.isEmpty (historyLoads (sentBy answer reviewing)) "die Karte allein zeigt keine Historie"
            let details = reviewing |> step ReviewShowDetails
            Expect.equal (historyLoads (sentBy answer details)) [ idOf 1 ] "„Details“ offen"
    ]
