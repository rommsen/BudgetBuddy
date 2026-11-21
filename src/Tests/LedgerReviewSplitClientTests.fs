module LedgerReviewSplitClientTests

// Aufteilen auf der Karte des Durchgehens (ledger-cyd1t): der Textlink „Aufteilen“ unter dem Feld „Kategorie“, das
// Feld „Aufgeteilt“ und Taste A öffnen das Detail der Karte mit offenem Split-Editor, in einem Schritt.
//
//  * reine Funktionen: `reviewSplitLink` je Sperrgrund und Kartenart, Taste A in `reviewKeyAction`;
//  * `ReviewOpenSplit` durch das echte `update`: frei öffnet es Detail und Editor, sonst bleibt das Model gleich;
//  * Esc: erst der Editor, dann das Detail, dann die Ansicht (Regressionstest, Doktrin Punkt 8);
//  * die Zustandsfolge als Aufzählung aller Reihenfolgen (Doktrin Punkt 20) mit eigenem Orakel.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private account = Guid.Parse "0195e811-0000-7000-8000-0000000000aa"
let private foodId = Guid.Parse "0195d811-0000-7000-8000-0000000000f1"
let private homeId = Guid.Parse "0195d811-0000-7000-8000-0000000000f2"

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f811-0000-7000-8000-{n:D12}"
      AccountId = account
      Date = DateTime(2026, 11, n)
      Amount = eur -42.80m
      Payee = Some $"Payee cyd1t {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some foodId
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = ImportedFromComdirect
      DeletedInYnab = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None
      ExternalLinks = []
      PayeeWriteBackFailure = None
      PossibleDuplicate = None
      CategoryFromOrderId = false }

let private splitLines : TransactionSplit list =
    [ { Target = ToCategory (YnabCategoryId foodId, "Lebensmittel"); Amount = eur -30.00m; Memo = None }
      { Target = ToCategory (YnabCategoryId homeId, "Haushalt"); Amount = eur -12.80m; Memo = None } ]

/// Eine aufgeteilte Buchung: ihr Entwurf lässt sich sofort speichern.
let private splitEntry (n: int) : LedgerEntryView =
    { entryOf n with CategoryId = None; CategoryName = None; CategoryGroupName = None; Splits = splitLines }

let private held (e: LedgerEntryView) : LedgerEntryView =
    { e with
        LinkedToYnab = false
        PossibleDuplicate =
            Some { Reason = "Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere Bank-Referenz."
                   MatchDate = DateTime(2026, 11, 1)
                   MatchAmount = eur -42.80m
                   MatchPayee = Some "Markt"
                   MatchCategoryName = Some "Lebensmittel"
                   MatchEntryId = None } }

let private locks (split: string option) (change: string option) : SplitLocks option =
    Some { CannotSplitReason = split; CannotChangeReason = change; ReplacementPending = false }

let private categories : YnabCategory list =
    [ { Id = YnabCategoryId foodId; Name = "Lebensmittel"; GroupName = "Alltag"; Available = Some (eur 120m); Hidden = false }
      { Id = YnabCategoryId homeId; Name = "Haushalt"; GroupName = "Alltag"; Available = Some (eur 50m); Hidden = false } ]

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        try sub sent.Add with _ -> ()
    model', List.ofSeq sent, external

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = run msg model
    model'

/// Das Durchgehen am Handy über `rows`, Karte auf der ersten; die Liste ist geladen.
let private reviewing (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    let model =
        { model with
            Entries = Success rows
            TotalCount = List.length rows
            Categories = Success categories
            CategoriesLoadedAt = Some DateTime.Now
            LenaFlagColor = Success (Some "purple")
            DetailForm = DetailSheet }
    let opened = step ReviewOpened model
    step (ReviewQueueLoaded (opened.ReviewGeneration, Ok rows)) opened

let private cardId (model: Model) = reviewCardOf model |> Option.map (fun e -> e.Id)
let private indexOf (model: Model) = model.Review |> Option.map (fun r -> r.Index) |> Option.defaultValue -1
let private detailShown (model: Model) = model.Review |> Option.exists (fun r -> r.DetailShown)

let private reconciled = "Abgeglichene Buchungen lassen sich nicht aufteilen"
let private tracking = "Auf einem Tracking-Konto lassen sich Buchungen nicht aufteilen"
let private transfer = "Ein Übertrag zwischen Budget-Konten lässt sich nicht aufteilen"
let private replacement = "Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten."
let private foreign = "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben"

[<Tests>]
let linkTests =
    testList "Aufteilen auf der Karte: der Link (ledger-cyd1t)" [

        testCase "frei ohne Sperre, auf einer aufgeteilten Buchung ebenso" <| fun () ->
            Expect.equal (reviewSplitLink (entryOf 1)) SplitLinkFree "unaufgeteilt"
            Expect.equal (reviewSplitLink (splitEntry 1)) SplitLinkFree "aufgeteilt"
            Expect.equal (reviewSplitLink { entryOf 1 with SplitLocks = locks None None }) SplitLinkFree "Sperren ohne Grund"

        testCase "gesperrt mit genau dem Text aus dem Detail, ein Fall je Sperrgrund auf der Normalkarte" <| fun () ->
            let cases =
                [ "in YNAB gelöscht", { entryOf 1 with DeletedInYnab = true }
                  "in YNAB gelöscht, aufgeteilt", { splitEntry 1 with DeletedInYnab = true }
                  "Tracking-Konto", { entryOf 1 with SplitLocks = locks (Some tracking) (Some tracking) }
                  "Abgleich", { entryOf 1 with SplitLocks = locks (Some reconciled) (Some reconciled) }
                  "Übertrag", { entryOf 1 with SplitLocks = locks (Some transfer) (Some transfer) }
                  "Ersatz offen", { splitEntry 1 with SplitLocks = Some { CannotSplitReason = Some replacement; CannotChangeReason = Some replacement; ReplacementPending = true } }
                  "fremde import_id, aufgeteilt", { splitEntry 1 with SplitLocks = locks None (Some foreign) } ]
            for name, entry in cases do
                let text = Components.Ledger.View.splitLockTextFor entry
                Expect.isSome text $"{name}: das Detail sperrt"
                Expect.equal (reviewSplitLink entry) (SplitLinkLocked text.Value) $"{name}: derselbe Grund wie im Detail"

        testCase "fremde import_id sperrt auf einer unaufgeteilten Buchung nichts" <| fun () ->
            Expect.equal (reviewSplitLink { entryOf 1 with SplitLocks = locks None (Some foreign) }) SplitLinkFree "Aufteilen bleibt"

        testCase "auf der Duplikat-Karte versteckt, gleich welche Sperre" <| fun () ->
            let cases =
                [ "wartend", held (entryOf 1)
                  "wartend und gelöscht", held { entryOf 1 with DeletedInYnab = true }
                  "wartend auf Tracking-Konto", held { entryOf 1 with SplitLocks = locks (Some tracking) (Some tracking) }
                  "wartend, aufgeteilt", held (splitEntry 1) ]
            for name, entry in cases do
                Expect.equal (reviewSplitLink entry) SplitLinkHidden name
            // Im Detail sperrt die wartende Buchung mit ihrem eigenen Grund (ADR 0093 Punkt 6).
            Expect.equal (Components.Ledger.View.splitLockTextFor (held (entryOf 1))) (Some possibleDuplicateLockedText) "Detail"

        testCase "Taste A: auf der Normalkarte das Aufteilen, auf der Duplikat-Karte und in der Endansicht nichts" <| fun () ->
            let cases =
                [ "a", Some (NormalCard, true), Some KeyOpenSplit
                  "A", Some (NormalCard, true), Some KeyOpenSplit
                  "a", Some (NormalCard, false), Some KeyOpenSplit
                  "A", Some (NormalCard, false), Some KeyOpenSplit
                  "a", Some (DuplicateCard, false), None
                  "A", Some (DuplicateCard, false), None
                  "a", None, None
                  "A", None, None ]
            for key, card, expected in cases do
                Expect.equal (reviewKeyAction card false key) expected $"{key} auf {card}"
                Expect.equal (reviewKeyAction card true key) expected $"{key} auf {card} mit Rückgängig"

        testCase "die Tastenleiste nennt A zwischen K und L" <| fun () ->
            let keys = Components.Ledger.View.reviewKeys |> List.map fst
            Expect.equal keys [ "Enter"; "S"; "K"; "A"; "L"; "Z"; "Esc" ] "Reihenfolge"
            Expect.contains Components.Ledger.View.reviewKeys ("A", "aufteilen") "Wort"

        testCase "A aus einem Feld, mit Strg, bei offenem Detail oder über dem Regel-Formular wirkt nicht" <| fun () ->
            let model = reviewing [ entryOf 1 ]
            let key = Components.Ledger.View.reviewKeyFor
            Expect.equal (key model "BODY" false false "a") (Some KeyOpenSplit) "frei"
            Expect.equal (key model "INPUT" false false "a") None "im Feld"
            Expect.equal (key model "BODY" false true "a") None "mit Strg"
            Expect.equal (key (step ReviewShowDetails model) "BODY" false false "a") None "Detail offen"
            Expect.equal (key { model with CategoryPickerFor = cardId model } "BODY" false false "a") None "Kategorieauswahl"
            Expect.isNone (Components.Ledger.View.reviewKeyRoute model true "BODY" false false "a") "Regel-Formular"
    ]

[<Tests>]
let openTests =
    testList "Aufteilen auf der Karte: ReviewOpenSplit (ledger-cyd1t)" [

        testCase "auf einer freien Normalkarte Detail und Editor der Karte in einem Schritt" <| fun () ->
            let model = reviewing [ entryOf 1; entryOf 2 ]
            let opened, sent, external = run ReviewOpenSplit model
            Expect.isTrue (detailShown opened) "Detail offen"
            Expect.equal (opened.SplitEditor |> Option.map (fun e -> e.EntryId)) (Some (entryOf 1).Id) "Editor der Karte"
            Expect.equal (cardId opened) (Some (entryOf 1).Id) "dieselbe Karte"
            Expect.equal (indexOf opened) 0 "Stelle bleibt"
            Expect.equal external NoOp "nichts nach außen"
            Expect.contains sent (LoadHistory (entryOf 1).Id) "Historie wie bei „Details“"

        testCase "über die Taste A dasselbe" <| fun () ->
            let opened = reviewing [ entryOf 1 ] |> step (ReviewKey KeyOpenSplit)
            Expect.isTrue (detailShown opened) "Detail offen"
            Expect.isSome opened.SplitEditor "Editor offen"

        testCase "gesperrt, Duplikat-Karte, offene Auswahl oder laufender Aufruf: das Model bleibt gleich" <| fun () ->
            let normal = reviewing [ entryOf 1; entryOf 2 ]
            let card = (entryOf 1).Id
            let cases =
                [ "gelöscht", reviewing [ { entryOf 1 with DeletedInYnab = true } ]
                  "Tracking-Konto", reviewing [ { entryOf 1 with SplitLocks = locks (Some tracking) (Some tracking) } ]
                  "fremde import_id, aufgeteilt", reviewing [ { splitEntry 1 with SplitLocks = locks None (Some foreign) } ]
                  "Duplikat-Karte", reviewing [ held (entryOf 1) ]
                  "Kategorieauswahl offen", step ReviewOpenCategory normal
                  "Payee-Auswahl offen", { normal with PayeeEdit = Some { PayeeEntryId = card; PayeeFieldError = None } }
                  "Kategorie läuft", step (StartSetEntryCategory (card, Some (YnabCategoryId homeId))) normal
                  "Editor schon offen", step ReviewOpenSplit normal
                  "Endansicht", step ReviewSkip (reviewing [ entryOf 1 ]) ]
            for name, model in cases do
                let after, sent, external = run ReviewOpenSplit model
                Expect.equal after model $"{name}: Model unverändert"
                Expect.isEmpty sent $"{name}: nichts gesendet"
                Expect.equal external NoOp $"{name}: nichts nach außen"
            // Gegenprobe: die Vorbereitung selbst ist frei, nur der jeweilige Zustand sperrt.
            Expect.isSome (step ReviewOpenSplit normal).SplitEditor "frei öffnet"

        testCase "steht das Detail schon (am PC das Panel), öffnet nur der Editor darin" <| fun () ->
            let details = { reviewing [ entryOf 1 ] with DetailForm = DetailPanel } |> step ReviewShowDetails
            let opened, sent, _ = run ReviewOpenSplit details
            Expect.isTrue (detailShown opened) "Detail bleibt"
            Expect.equal (opened.SplitEditor |> Option.map (fun e -> e.EntryId)) (Some (entryOf 1).Id) "Editor der Karte"
            Expect.isFalse (sent |> List.exists (function LoadHistory _ -> true | _ -> false)) "die Historie lädt nicht neu"

        testCase "solange „Passt“ der Karte läuft, öffnet nichts" <| fun () ->
            let confirming = reviewing [ entryOf 1; entryOf 2 ] |> step ReviewConfirm
            Expect.isSome confirming.Review.Value.Confirming "Passt läuft"
            Expect.equal (step ReviewOpenSplit confirming) confirming "Model unverändert"
    ]

[<Tests>]
let escapeTests =
    testList "Aufteilen auf der Karte: Esc (ledger-cyd1t)" [

        // Regressionstest (Doktrin Punkt 8): vorher galt bei offenem Editor keine Taste des Durchgehens (Esc tat nichts),
        // und `KeyLeave` übersprang den Editor und schloss Detail samt Entwurf in einem Schritt.
        testCase "Esc schließt erst den Editor, dann das Detail, erst das dritte verlässt das Durchgehen" <| fun () ->
            let opened = reviewing [ entryOf 1 ] |> step ReviewOpenSplit
            let key = Components.Ledger.View.reviewKeyFor
            Expect.equal (key opened "BODY" false false "Escape") (Some KeyLeave) "Esc bei offenem Editor"
            Expect.equal (key opened "INPUT" false false "Escape") (Some KeyLeave) "Esc aus einem Feld des Editors"
            Expect.equal (key opened "BODY" false false "a") None "andere Tasten nicht"
            Expect.equal (key opened "BODY" false true "Escape") None "mit Strg nicht"
            let first, _, external = run (ReviewKey KeyLeave) opened
            Expect.isNone first.SplitEditor "Editor zu"
            Expect.isTrue (detailShown first) "Detail bleibt"
            Expect.equal external NoOp "Ansicht bleibt"
            let second, _, external = run (ReviewKey KeyLeave) first
            Expect.isFalse (detailShown second) "Detail zu"
            Expect.equal (cardId second) (Some (entryOf 1).Id) "Karte steht"
            Expect.equal external NoOp "Ansicht bleibt"
            let _, _, external = run (ReviewKey KeyLeave) second
            Expect.equal external LeaveReview "zurück zur Übersicht"

        testCase "Esc bei offener Auswahl im Editor schließt nur die Auswahl" <| fun () ->
            let opened = reviewing [ entryOf 1 ] |> step ReviewOpenSplit |> step (OpenSplitTargetPicker 1)
            Expect.notEqual opened.SplitEditor.Value.Picker Components.Ledger.SplitEditor.NoSplitPicker "Auswahl offen"
            let closed = step (ReviewKey KeyLeave) opened
            Expect.isSome closed.SplitEditor "Editor bleibt"
            Expect.equal closed.SplitEditor.Value.Picker Components.Ledger.SplitEditor.NoSplitPicker "Auswahl zu"

        testCase "während Speichern läuft, schließt Esc nichts" <| fun () ->
            let saving = reviewing [ splitEntry 1 ] |> step ReviewOpenSplit |> step StartSaveSplits
            Expect.equal (step (ReviewKey KeyLeave) saving) saving "Model unverändert"
    ]

/// Was die Aufzählung zustellen kann: eine Nachricht der Oberfläche oder die Antwort auf einen offenen Aufruf.
type private Pending = { Ok: Msg; Error: Msg }

let private pendingOf (msg: Msg) : Pending option =
    match msg with
    | RunSaveSplits (id, lines) ->
        Some { Ok = SplitsSaved (id, lines, Ok { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None })
               Error = SplitsSaved (id, lines, Error "weg") }
    | RunReviewConfirm id ->
        let outcome : ReviewMarkOutcome =
            { Previous = { IsApproved = false; ClearedState = ReviewUncleared }; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent }
        Some { Ok = ReviewConfirmed (id, Ok outcome); Error = ReviewConfirmed (id, Error "weg") }
    | _ -> None

let private sheetContents (model: Model) : int =
    [ model.SplitEditor.IsSome
      model.CategoryPickerFor.IsSome
      model.PayeeEdit.IsSome
      model.MemoEdit.IsSome
      model.AmountEdit.IsSome ]
    |> List.filter id
    |> List.length

[<Tests>]
let sequenceTests =
    testList "Aufteilen auf der Karte: Zustandsfolge (ledger-cyd1t)" [

        // Doktrin Punkt 20: alle Reihenfolgen aus Aufteilen, Speichern, Antworten (Ok und Fehler), Schließen von Editor
        // und Detail, Passt, Später und Wisch (rechts und links) bis zur Tiefe 7. Das Orakel führt die offenen Aufrufe
        // selbst und fragt den getesteten Code nur nach dem sichtbaren Zustand.
        testCase "höchstens ein Sheet-Inhalt, die Stelle bleibt bei offenem Editor und belegter Zeile, nach Erfolg steht das Detail" <| fun () ->
            let card = (splitEntry 1).Id
            let start = reviewing [ splitEntry 1; entryOf 2 ]
            let ui =
                [ "Aufteilen", ReviewOpenSplit
                  "Speichern", StartSaveSplits
                  "Editor schließen", CloseSplitEditor
                  "Detail schließen", CloseDetail
                  "Passt", ReviewConfirm
                  "Später", ReviewSkip
                  "Wisch rechts", ReviewSwiped (200.0, 0.0)
                  "Wisch links", ReviewSwiped (-200.0, 0.0) ]
            let visited = Collections.Generic.HashSet<Model * Msg list> HashIdentity.Structural
            let mutable savedOk = 0
            let rec go (left: int) (path: string list) (model: Model) (pending: Pending list) =
                let answers =
                    [ for i, p in List.indexed pending do
                          yield $"Antwort {i} Ok", p.Ok, Some i
                          yield $"Antwort {i} Fehler", p.Error, Some i ]
                for name, msg, answered in [ for n, m in ui -> n, m, None ] @ answers do
                    let path' = name :: path
                    let shown = String.concat ", " (List.rev path')
                    let after, sent, _ = run msg model
                    let remaining = match answered with Some i -> List.removeAt i pending | None -> pending
                    let open' = remaining @ (sent |> List.choose pendingOf)
                    Expect.isLessThanOrEqual (sheetContents after) 1 $"zwei Sheet-Inhalte: {shown}"
                    let editorOpen = model.SplitEditor.IsSome
                    let rowBusy = model.CategoryRows |> Map.tryFind card = Some CategorySending
                    if (editorOpen || rowBusy) && indexOf model = 0 then
                        Expect.equal (indexOf after) (indexOf model) $"Stelle wechselt bei offenem Editor oder belegter Zeile: {shown}"
                    match msg with
                    | SplitsSaved (_, _, Ok _) ->
                        if detailShown model then savedOk <- savedOk + 1
                        Expect.isNone after.SplitEditor $"nach Erfolg ist der Editor zu: {shown}"
                        // Wer das Detail während des Speicherns schloss, steht auf der Karte; sonst steht das Detail.
                        Expect.equal (detailShown after) (detailShown model) $"nach Erfolg steht das Detail, wenn es offen war: {shown}"
                        Expect.equal (cardId after) (Some card) $"nach Erfolg dieselbe Karte: {shown}"
                    | _ -> ()
                    let key = after, (open' |> List.map (fun p -> p.Ok))
                    if left > 1 && visited.Add key then go (left - 1) path' after open'
            go 7 [] start []
            Expect.isGreaterThan savedOk 0 "die Aufzählung stellte nie eine erfolgreiche Antwort bei offenem Detail zu"
    ]
