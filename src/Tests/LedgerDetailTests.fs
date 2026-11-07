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
                |> step (HistoryLoaded (idOf 1, Ok lines))
            Expect.equal model.History (Success lines) "Success"

        testCase "eine Historie, die nach einem Wechsel der Buchung eintrifft, wird verworfen" <| fun () ->
            let model =
                modelWith (rows 3)
                |> asPanel
                |> step (OpenDetail (entry 1))
                |> step (OpenDetail (entry 2))
                |> step (HistoryLoaded (idOf 1, Ok [ historyLine 1 ImportedFromComdirect "alt" ]))
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

        testCase "der Tastaturhinweis nennt bei offenem Panel Pfeile, G und Esc" <| fun () ->
            let opened = modelWith (rows 3) |> asPanel |> step (OpenDetail (entry 1))
            Expect.equal (keyboardHintTextFor opened) "↑ ↓ bewegen · G geprüft · Esc Panel schließen" "Panel-Hinweis"
            Expect.equal (keyboardHintTextFor (modelWith (rows 3))) keyboardHintText "sonst der Grundtext"
            Expect.stringContains keyboardHintText "Enter" "Enter ist belegt und steht im Grundtext"
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
                  AskLenaCount = 0 }
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
                  AskLenaCount = 0 }
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
    testList "Detail: Form und Spalten neben dem Panel (ledger-q1hw2)" [

        testCase "bei offenem Panel ist Memo in keiner Breite sichtbar" <| fun () ->
            for width in [ 600.0 .. 10.0 .. 2400.0 ] do
                for account in [ true; false ] do
                    Expect.isFalse
                        (columnsOf (visibleColumnsWithPanel width account) |> List.contains MemoColumn)
                        $"{width}px, Konto-Spalte erlaubt: {account}"

        testCase "die übrigen Spalten folgen visibleColumns mit der Breite minus 420 px" <| fun () ->
            for width in [ 1000.0 .. 25.0 .. 2200.0 ] do
                let plain = columnsOf (visibleColumns (width - 420.0) true) |> List.filter ((<>) MemoColumn)
                let withPanel = columnsOf (visibleColumnsWithPanel width true)
                if not (List.isEmpty plain) then
                    Expect.equal withPanel plain $"{width}px"

        testCase "Grenzwerte: ein Konto braucht 660 px neben dem Panel, also 1080 px ohne" <| fun () ->
            Expect.equal (detailFormFor 1079.0 false) DetailSheet "1079 px: Restbreite 659 reicht für keine Stufe"
            Expect.equal (detailFormFor 1080.0 false) DetailPanel "1080 px: Restbreite 660 reicht"
            Expect.equal (detailFormFor 1090.0 false) DetailPanel "die angenommene Breite bei 1440 px Fenster"

        testCase "Grenzwerte: in der Gesamtsicht weicht die Konto-Spalte zuerst" <| fun () ->
            Expect.contains (columnsOf (visibleColumnsWithPanel 1152.0 true)) AccountColumn "1152 px: mit Konto-Spalte"
            Expect.isFalse (columnsOf (visibleColumnsWithPanel 1151.0 true) |> List.contains AccountColumn) "1151 px: ohne Konto-Spalte"
            Expect.equal (detailFormFor 1151.0 true) DetailPanel "auch ohne Konto-Spalte ein Panel"
            Expect.equal (detailFormFor 1079.0 true) DetailSheet "darunter ein Sheet"

        testCase "zeigt der PC schon die Kompaktliste, ist die Form ein Sheet" <| fun () ->
            for width in [ 0.0; 300.0; 659.0 ] do
                Expect.equal (visibleColumns width false) CompactList $"{width}px: Kompaktliste"
                Expect.equal (detailFormFor width false) DetailSheet $"{width}px: Sheet"
                Expect.equal (detailFormFor width true) DetailSheet $"{width}px, Gesamtsicht: Sheet"
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
            Expect.equal (dot ImportedFromComdirect "Comdirect-Import: Buchung erfasst") Detail.DotTeal "Import teal"
            Expect.equal (dot DecidedByRule "Regel: Kategorie gesetzt") Detail.DotPurple "Regel violett"
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
            Expect.contains sent LoadEntries "Liste lädt"
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
