module LedgerSplitEditorClientTests

// ledger-pafkp: Split-Editor im Kassenbuch-Detail, Client-Seite (ADR 0058, ADR 0006).
// Gruppen: die reinen Rechen- und Prüffunktionen des Entwurfs, die Sperren aus
// `LedgerEntryView.SplitLocks`, der Reducer ohne Proxy (`Start...` rein, `Run...` unrein, ADR 0020)
// und die Verdrahtung der Ansicht. Feliz-Elemente lassen sich unter .NET nicht inspizieren; die
// Ansicht prüfen reine Funktionen und Quelltext-Zusicherungen, die gerenderten Elemente das
// Playwright-Skript `scripts/ledger-split-editor.test.mjs`.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.SplitEditor
open Client.DesignSystem

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private catId (n: int) = YnabCategoryId (Guid.Parse $"0195f6a0-0000-7000-8000-{n:D12}")
let private catGuid (n: int) = Guid.Parse $"0195f6a0-0000-7000-8000-{n:D12}"

let private category (n: int) (group: string) (name: string) : YnabCategory =
    { Id = catId n; Name = name; GroupName = group; Available = Some (eur 100m); Hidden = false }

let private categories =
    [ category 1 "Alltag" "Haushalt"; category 2 "Wohnen" "Möbel"; category 3 "Alltag" "Lebensmittel" ]

let private unlocked : SplitLocks option = None

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f6a1-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195f6a2-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -81.90m
      Payee = Some $"Bauhaus {n}"
      Memo = Some "Werkzeug + Lampe"
      CategoryName = Some "Haushalt"
      CategoryId = Some (catGuid 1)
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
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
      PayeeWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = unlocked
      ExternalLinks = []
      PossibleDuplicate = None; CategoryFromOrderId = false }

let private idOf (n: int) = (entryOf n).Id

let private moebel : TransactionSplit =
    { Target = ToCategory (catId 2, "Möbel"); Amount = eur -23.40m; Memo = Some "Lampe" }

let private haushalt : TransactionSplit =
    { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -58.50m; Memo = Some "Werkzeug" }

let private splitEntry (n: int) : LedgerEntryView =
    { entryOf n with
        CategoryId = None
        CategoryName = None
        CategoryGroupName = None
        Splits = [ moebel; haushalt ]
        SplitCategoryGroupNames = Map.ofList [ catGuid 2, "Wohnen"; catGuid 1, "Alltag" ] }

let private locks (split: string option) (change: string option) : SplitLocks option =
    Some { CannotSplitReason = split; CannotChangeReason = change; ReplacementPending = false }

let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        Categories = Success categories
        CategoriesLoadedAt = Some DateTime.Now
        DetailFor = rows |> List.tryHead
        DetailForm = DetailPanel }

let private update = Components.Ledger.State.update

/// Führt nur reine Folgebefehle aus (`Cmd.ofMsg`) und sammelt ihre Nachrichten.
let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private entriesOf (model: Model) =
    match model.Entries with
    | Success entries -> entries
    | _ -> failtest "Entries sollte geladen sein"

let private rowOf (n: int) (model: Model) = entriesOf model |> List.find (fun e -> e.Id = idOf n)

let private changedOutcome (previous: CategoryChoice option) (writeBack: CategoryWriteBack) : CategoryChangeOutcome =
    { Previous = previous; Change = CategoryChanged; WriteBack = writeBack; PreviousTransferAccountId = None }

let private haushaltChoice : CategoryChoice = { CategoryId = catId 1; CategoryName = "Haushalt" }

let private editorOf (model: Model) : SplitEditorState =
    match model.SplitEditor with
    | Some editor -> editor
    | None -> failtest "Der Editor sollte offen sein"

/// Ein offener Editor für die Buchung 1 (unaufgeteilt), Entwurf nach `edit`.
let private opened (entry: LedgerEntryView) : Model =
    let model, _, _ = update (OpenSplitEditor entry.Id) (modelWith [ entry; entryOf 2 ])
    model

let private lineWith (target: SplitTarget option) (amount: string) : SplitDraftLine =
    { Target = target; AmountText = amount; Memo = "" }

let private stateOf (total: decimal) (lines: SplitDraftLine list) : SplitEditorState =
    { EntryId = idOf 1
      Total = eur total
      Lines = lines
      Picker = NoSplitPicker
      Before = WasPlain (Some haushaltChoice)
      AmountFrom = None
      SaveError = None }

let private toMoebel = Some (ToCategory (catId 2, "Möbel"))
let private toHaushalt = Some (ToCategory (catId 1, "Haushalt"))

// ============================================
// Reine Rechen- und Prüffunktionen
// ============================================

[<Tests>]
let arithmeticTests =
    testList "Split-Editor: Rechnen und Prüfen (ledger-pafkp)" [

        testCase "amountText rundet kaufmännisch, leer bei null, ohne Vorzeichen" <| fun () ->
            // Läuft über `formatAmountForInput`, denselben Cent-Weg wie die Vorbelegung (infrastructure-k84s2).
            Expect.equal (amountText 12.345m) "12,35" "halber Cent aufgerundet"
            Expect.equal (amountText 0m) "" "null bleibt leer"
            Expect.equal (amountText -23.4m) "23,40" "negativ ohne Minus"

        testCase "Rest gleich Gesamtbetrag minus Summe der positiv eingegebenen Beträge" <| fun () ->
            let state = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith None "" ]
            Expect.equal (remainder state) 58.50m "81,90 minus 23,40"
            let over = stateOf -81.90m [ lineWith toMoebel "90"; lineWith toHaushalt "" ]
            Expect.equal (remainder over) -8.10m "zu viel verteilt ist negativ"
            let income = stateOf 50m [ lineWith toMoebel "20" ]
            Expect.equal (remainder income) 30m "positiver Gesamtbetrag gleich gerechnet"

        testCase "ein Minuszeichen in der Eingabe zählt als positiver Betrag" <| fun () ->
            let state = stateOf -10m [ lineWith toMoebel "-4,50" ]
            Expect.equal (remainder state) 5.50m "4,50 abgezogen"

        testCase "Rest füllt die Zeile mit dem Rest, nie unter null" <| fun () ->
            let state = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "" ]
            Expect.equal (restFor 1 state) 58.50m "Zeile 2 bekommt den Rest"
            let filled = fillRest 1 state
            Expect.equal (filled.Lines.[1].AmountText) "58,50" "Text mit Komma"
            Expect.equal (remainder filled) 0m "danach verteilt"
            let over = stateOf -10m [ lineWith toMoebel "20"; lineWith toHaushalt "" ]
            Expect.equal (restFor 1 over) 0m "nie ein negativer Betrag"
            Expect.equal (restFor 0 state) 81.90m "die erste Zeile ersetzt ihren eigenen Betrag"

        testCase "Speichern nur bei Rest 0,00 € und wenn jede Zeile ein Ziel hat" <| fun () ->
            let open' = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "" ]
            Expect.isFalse (canSave open') "Rest 58,50 sperrt"
            let balanced = fillRest 1 open'
            Expect.isTrue (canSave balanced) "Rest 0,00 erlaubt"
            let noTarget = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith None "58,50" ]
            Expect.isFalse (canSave noTarget) "Zeile ohne Ziel"
            let zeroLine = stateOf -81.90m [ lineWith toMoebel "81,90"; lineWith toHaushalt "0" ]
            Expect.isFalse (canSave zeroLine) "eine Zeile über null Euro"
            let junk = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "abc" ]
            Expect.isFalse (canSave junk) "unlesbarer Betrag"

        testCase "die Zeilen gehen mit dem Vorzeichen des Gesamtbetrags an setEntrySplits" <| fun () ->
            let state = fillRest 1 (stateOf -81.90m [ { lineWith toMoebel "23,40" with Memo = " Lampe " }; lineWith toHaushalt "" ])
            match requestLines state with
            | Some lines ->
                Expect.equal (lines |> List.map (fun l -> l.Amount.Amount)) [ -23.40m; -58.50m ] "negativ wie die Buchung"
                Expect.equal (lines |> List.map (fun l -> l.Memo)) [ Some "Lampe"; None ] "Memo getrimmt, leer ist None"
                Expect.isOk (mkSplits state.Total lines |> Result.mapError string) "besteht mkSplits"
            | None -> failtest "sollte speicherbar sein"
            let income = fillRest 1 (stateOf 50m [ lineWith toMoebel "20"; lineWith toHaushalt "" ])
            Expect.equal (requestLines income |> Option.map (List.map (fun l -> l.Amount.Amount))) (Some [ 20m; 30m ]) "Einnahme positiv"

        testCase "bleibt genau eine Zeile übrig, geht sie als einzelne Zeile mit vollem Betrag hin" <| fun () ->
            let single = fillRest 0 (stateOf -81.90m [ lineWith toHaushalt "" ])
            Expect.equal
                (requestLines single)
                (Some [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ])
                "Kategorie über den vollen Betrag"
            let uncategorized = fillRest 0 (stateOf -81.90m [ lineWith (Some Uncategorized) "" ])
            Expect.equal
                (requestLines uncategorized)
                (Some [ { Target = Uncategorized; Amount = eur -81.90m; Memo = None } ])
                "Uncategorized über den vollen Betrag"
            Expect.isFalse (canSave (stateOf -81.90m [ lineWith toHaushalt "10" ])) "eine Zeile ohne vollen Betrag"

        testCase "eine leere Liste und eine einzelne Übertrags-Zeile werden nie geschickt" <| fun () ->
            Expect.isFalse (canSave (stateOf -81.90m [])) "leere Liste"
            let transfer = Some (ToTransfer (YnabAccountId (Guid.NewGuid()), "Tagesgeld"))
            Expect.isNone (requestLines (fillRest 0 (stateOf -81.90m [ lineWith transfer "" ]))) "einzelne Übertrags-Zeile"
            let withTransfer = fillRest 1 (stateOf -81.90m [ lineWith toMoebel "30"; lineWith transfer "" ])
            Expect.isTrue (canSave withTransfer) "Übertrag neben einer Kategorie ist erlaubt"

        testCase "Zeile hinzufügen ist bei Rest 0,00 € gesperrt" <| fun () ->
            let open' = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "" ]
            Expect.isTrue (canAddLine open') "Rest offen"
            Expect.equal (addLine open').Lines.Length 3 "hängt eine leere Zeile an"
            let balanced = fillRest 1 open'
            Expect.isFalse (canAddLine balanced) "Rest 0,00"
            Expect.equal (addLine balanced).Lines.Length 2 "ändert nichts"

        testCase "Toast-Text: Aufgeteilt in n Zeilen, bei einer Zeile Aufteilung aufgehoben" <| fun () ->
            Expect.equal (savedToastText [ moebel; haushalt ]) "Aufgeteilt in 2 Zeilen" "zwei Zeilen"
            Expect.equal (savedToastText [ moebel; haushalt; moebel ]) "Aufgeteilt in 3 Zeilen" "drei Zeilen"
            Expect.equal (savedToastText [ haushalt ]) "Aufteilung aufgehoben" "eine Zeile"

        testCase "Rückgängig stellt über denselben Schreibweg den alten Stand her" <| fun () ->
            let total = eur -81.90m
            Expect.equal
                (undoLines (WasPlain (Some haushaltChoice)) total)
                [ { Target = ToCategory (catId 1, "Haushalt"); Amount = total; Memo = None } ]
                "eine Zeile der alten Kategorie über den vollen Betrag"
            Expect.equal
                (undoLines (WasPlain None) total)
                [ { Target = Uncategorized; Amount = total; Memo = None } ]
                "Uncategorized ohne Kategorie"
            Expect.equal (undoLines (WasSplit [ moebel; haushalt ]) total) [ moebel; haushalt ] "die alten Zeilen"

        testCase "Rückgängig fehlt, wenn der Rückweg gesperrt wäre (fremde import_id)" <| fun () ->
            let foreign = locks None (Some "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben")
            Expect.isFalse (undoAvailable foreign [ moebel; haushalt ]) "Aufteilen: der Rückweg wäre ein Ersatz"
            Expect.isTrue (undoAvailable foreign [ haushalt ]) "Aufheben: der Rückweg ist ein Aufteilen"
            Expect.isTrue (undoAvailable None [ moebel; haushalt ]) "ohne Sperre"
            Expect.isTrue (undoAvailable (locks None None) [ moebel; haushalt ]) "Sperren ohne Änderungs-Sperre"

        testCase "startState: unaufgeteilt mit der Kategorie in Zeile 1, aufgeteilt mit den Zeilen" <| fun () ->
            let plain = startState (entryOf 1)
            Expect.equal plain.Lines.Length 2 "zwei Zeilen"
            Expect.equal plain.Lines.[0].Target toHaushalt "Kategorie der Buchung in Zeile 1"
            Expect.isNone plain.Lines.[1].Target "Zeile 2 leer"
            Expect.equal plain.Before (WasPlain (Some haushaltChoice)) "Stand davor"
            let split = startState (splitEntry 1)
            Expect.equal (split.Lines |> List.map (fun l -> l.AmountText)) [ "23,40"; "58,50" ] "positive Beträge"
            Expect.equal (split.Lines |> List.map (fun l -> l.Memo)) [ "Lampe"; "Werkzeug" ] "Memo"
            Expect.equal split.Before (WasSplit [ moebel; haushalt ]) "Stand davor"
            Expect.isTrue (isBalanced split) "verteilt"
            Expect.isTrue (canSave split) "unverändert speicherbar"
    ]

// ============================================
// Übertrag-Ziele
// ============================================

[<Tests>]
let transferTargetTests =
    let account (n: int) (name: string) (onBudget: bool) (closed: bool) : YnabAccount =
        { Id = YnabAccountId (Guid.Parse $"0195f6a3-0000-7000-8000-{n:D12}")
          Name = name
          Balance = eur 0m
          OnBudget = onBudget
          Closed = closed }

    testList "Split-Editor: Übertrag-Ziele (ledger-pafkp)" [

        testCase "die Kontenliste enthält nur offene Budget-Konten, ohne das eigene Konto der Buchung" <| fun () ->
            let own = account 1 "Girokonto" true false
            let accounts =
                [ own
                  account 2 "Tagesgeld" true false
                  account 3 "Altes Konto" true true
                  account 4 "Depot (Tracking)" false false
                  account 5 "Bargeld" true false ]
            let ledgerAccounts : LedgerAccountsView =
                { Accounts =
                    [ { Id = (entryOf 1).AccountId
                        Name = "Girokonto"
                        Kind = LedgerBankAccount
                        YnabAccountId = Some own.Id
                        Closed = false } ]
                  DefaultAccountId = None }
            let model = { modelWith [ entryOf 1 ] with Accounts = Success ledgerAccounts; TransferTargets = Success accounts }
            let names = Components.Ledger.View.transferTargetsFor model (entryOf 1) |> List.map (fun a -> a.Name)
            Expect.equal names [ "Tagesgeld"; "Bargeld" ] "offen, auf dem Budget, nicht das eigene Konto"

        testCase "solange die Liste nicht geladen ist, gibt es keine Ziele" <| fun () ->
            let model = { modelWith [ entryOf 1 ] with TransferTargets = Loading }
            Expect.isEmpty (Components.Ledger.View.transferTargetsFor model (entryOf 1)) "leer"

        // Regression ledger-vq2g8: bei `TransferTargets = Failure` lieferte der Split-Editor
        // eine stille leere Liste, ohne Hinweis. Jetzt meldet die Entscheidung den Fehler.
        testCase "gescheiterte Übertrag-Ziele melden den Fehler, ladende den Ladezustand" <| fun () ->
            Expect.equal (Components.Ledger.View.transferTargetsLoadState (Failure "Http error (0)")) (CategoryPicker.PickerFailed None) "Fehler"
            Expect.equal (Components.Ledger.View.transferTargetsLoadState Loading) CategoryPicker.PickerLoading "lädt"
            Expect.equal (Components.Ledger.View.transferTargetsLoadState NotAsked) CategoryPicker.PickerLoading "noch nicht angefragt"
            Expect.equal (Components.Ledger.View.transferTargetsLoadState (Success [])) CategoryPicker.PickerReady "geladen"

        testCase "der Ziel-Picker filtert Konten nach dem Namen ohne Beachtung der Schreibweise" <| fun () ->
            let accounts = [ account 1 "Tagesgeld" true false; account 2 "Bargeld" true false ]
            Expect.equal (CategoryPicker.filterAccounts "" accounts |> List.length) 2 "leer: alle"
            Expect.equal (CategoryPicker.filterAccounts "GELD" accounts |> List.length) 2 "beide enthalten geld"
            Expect.equal (CategoryPicker.filterAccounts "tages" accounts |> List.map (fun a -> a.Name)) [ "Tagesgeld" ] "Teilwort"
            Expect.isEmpty (CategoryPicker.filterAccounts "xyz" accounts) "kein Treffer"
    ]

// ============================================
// Sperren aus dem Lesepfad
// ============================================

[<Tests>]
let lockTests =
    let reconciled = "Abgeglichene Buchungen lassen sich nicht aufteilen"
    let tracking = "Auf einem Tracking-Konto lassen sich Buchungen nicht aufteilen"
    let foreign = "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben"

    testList "Split-Editor: Sperren (ledger-pafkp)" [

        testCase "ohne SplitLocks (älterer Server) ist nichts gesperrt" <| fun () ->
            Expect.isNone (lockReasonFor (entryOf 1)) "unaufgeteilt"
            Expect.isNone (lockReasonFor (splitEntry 1)) "aufgeteilt"
            Expect.isTrue (Components.Ledger.View.splitActionMsg (entryOf 1)).IsSome "Aufteilen wirkt"

        testCase "abgeglichen, Tracking-Konto und Übertrag sperren Aufteilen, Ändern und den Pfeil, mit Hinweis" <| fun () ->
            for reason in [ reconciled; tracking; "Ein Übertrag zwischen Budget-Konten lässt sich nicht aufteilen" ] do
                let l = locks (Some reason) (Some reason)
                let plain = { entryOf 1 with SplitLocks = l }
                let split = { splitEntry 1 with SplitLocks = l }
                Expect.equal (lockReasonFor plain) (Some reason) "Aufteilen gesperrt"
                Expect.equal (lockReasonFor split) (Some reason) "Ändern gesperrt"
                Expect.isNone (Components.Ledger.View.splitActionMsg plain) "Aufteilen ohne Wirkung"
                Expect.isNone (Components.Ledger.View.splitActionMsg split) "Ändern ohne Wirkung"
                Expect.equal (splitLockReason plain) (Some reason) "Hinweistext im Detail"
                Expect.equal (categoryLockHint (categoryLockFor split)) (Some reason) "Pfeil mit Tooltip"
                Expect.isNone (Components.Ledger.View.categoryArrowMsg (modelWith [ split ]) split) "Pfeil inaktiv"

        testCase "in YNAB gelöscht sperrt alles mit eigenem Hinweis" <| fun () ->
            let deleted = { entryOf 1 with DeletedInYnab = true }
            Expect.isNone (Components.Ledger.View.splitActionMsg deleted) "gesperrt"
            Expect.isSome (splitLockReason deleted) "Hinweis"
            let deletedSplit = { splitEntry 1 with DeletedInYnab = true }
            Expect.isNone (Components.Ledger.View.splitActionMsg deletedSplit) "aufgeteilt und gelöscht"

        testCase "fremde import_id sperrt nur Ändern, nicht Aufteilen" <| fun () ->
            let l = locks None (Some foreign)
            let plain = { entryOf 1 with SplitLocks = l }
            let split = { splitEntry 1 with SplitLocks = l }
            Expect.isSome (Components.Ledger.View.splitActionMsg plain) "Aufteilen bleibt aktiv"
            Expect.isNone (Components.Ledger.View.splitActionMsg split) "Ändern inaktiv"
            Expect.equal (splitLockReason split) (Some foreign) "Hinweistext"
            Expect.isNone (splitLockReason plain) "kein Hinweis beim Aufteilen"

        testCase "der Pfeil an der Kategorie öffnet bei einer Split-Buchung den Editor" <| fun () ->
            let model = modelWith [ splitEntry 1 ]
            Expect.equal (categoryLockFor (splitEntry 1)) CategoryOpensSplitEditor "Sperre entfällt"
            Expect.equal
                (Components.Ledger.View.categoryArrowMsg model (splitEntry 1))
                (Some (OpenSplitEditor (idOf 1)))
                "Pfeil öffnet den Editor"

        testCase "die Hinweisleiste nennt für Neuanlage offen einen eigenen Text und Erneut senden" <| fun () ->
            let pending =
                { entryOf 1 with
                    CategorizationWriteBackFailure = Some "YNAB nicht erreichbar"
                    SplitLocks = Some { CannotSplitReason = None; CannotChangeReason = None; ReplacementPending = true } }
            match categoryHintFor None pending with
            | Some hint ->
                Expect.equal hint.Title categoryReplacementTitle "eigener Titel"
                Expect.equal hint.Title "In YNAB gelöscht, Neuanlage offen" "Wortlaut"
                Expect.equal hint.Reason "YNAB nicht erreichbar" "Grund"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Knopf"
                let notice = Components.Ledger.View.detailNoticeWithCategory None None NotAsked pending
                Expect.equal
                    (notice |> Option.bind (Components.Ledger.View.detailNoticeActionMsg pending.Id))
                    (Some (StartRetryCategorization pending.Id))
                    "Erneut senden schickt StartRetryCategorization"
            | None -> failtest "Hinweis erwartet"
            let plainFailure = { entryOf 1 with CategorizationWriteBackFailure = Some "HTTP 500" }
            Expect.equal (categoryHintFor None plainFailure |> Option.map (fun h -> h.Title)) (Some categoryHintTitle) "sonst der bisherige Titel"
            let model, sent, _ = run (StartRetryCategorization pending.Id) (modelWith [ pending; entryOf 2 ])
            Expect.equal sent [ RunRetryCategorization pending.Id ] "genau ein Aufruf"
            Expect.equal (model.CategoryRows |> Map.tryFind pending.Id) (Some CategorySending) "sendet"

        // ledger-f8bjm: nach einem Absturz zwischen Senden und Fehlschlag meldet der Server den offenen
        // Ersatz ohne Fehlschlag; "Erneut senden" schickt dann nur das POST, der Client bot es nicht an.
        testCase "ein offener Ersatz ohne Fehlschlag zeigt die Hinweisleiste mit Erneut senden" <| fun () ->
            let pending =
                { entryOf 1 with
                    CategorizationWriteBackFailure = None
                    SplitLocks = Some { CannotSplitReason = None; CannotChangeReason = None; ReplacementPending = true } }
            match categoryHintFor None pending with
            | Some hint ->
                Expect.equal hint.Title categoryReplacementTitle "Titel des halben Ersatzes"
                Expect.equal
                    hint.Reason
                    "Der Ersatz ist in YNAB noch nicht bestätigt. Erneut senden oder den Spiegel abwarten."
                    "Grund wie die Sperre des Servers"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Knopf"
            | None -> failtest "Hinweis erwartet"
            let notice = Components.Ledger.View.detailNoticeWithCategory None None NotAsked pending
            Expect.equal
                (notice |> Option.bind (Components.Ledger.View.detailNoticeActionMsg pending.Id))
                (Some (StartRetryCategorization pending.Id))
                "Erneut senden schickt StartRetryCategorization"
            let model, sent, _ = run (StartRetryCategorization pending.Id) (modelWith [ pending; entryOf 2 ])
            Expect.equal sent [ RunRetryCategorization pending.Id ] "genau ein Aufruf"
            // Ein gelungenes Senden beendet den offenen Ersatz, die Leiste verschwindet.
            let after, _, _ = update (CategorizationRetried (pending.Id, Ok CategorySent)) model
            Expect.isNone (categoryHintFor None (rowOf 1 after)) "nach dem Senden kein Hinweis mehr"
    ]

// ============================================
// Reducer
// ============================================

[<Tests>]
let reducerTests =
    testList "Split-Editor: Reducer (ledger-pafkp)" [

        testCase "Aufteilen öffnet den Editor mit dem Entwurf und lädt Konten und Kategorien" <| fun () ->
            let model, sent, _ = run (OpenSplitEditor (idOf 1)) (modelWith [ entryOf 1; entryOf 2 ])
            let editor = editorOf model
            Expect.equal editor.EntryId (idOf 1) "Editor der Buchung im Detail"
            Expect.equal editor.Lines.Length 2 "zwei Zeilen"
            Expect.contains sent RunLoadTransferTargets "Kontenliste wird geladen"
            Expect.contains sent EnsureCategoriesFresh "Kategorien frisch halten"
            let again, sent2, _ =
                run (OpenSplitEditor (idOf 1)) { model with TransferTargets = Success []; SplitEditor = None }
            Expect.isSome again.SplitEditor "öffnet wieder"
            Expect.isFalse (sent2 |> List.contains RunLoadTransferTargets) "geladene Liste wird nicht neu geholt"

        testCase "Ändern öffnet den Editor mit den Zeilen der Split-Buchung" <| fun () ->
            let model = opened (splitEntry 1)
            Expect.equal (editorOf model).Lines.Length 2 "zwei Zeilen"
            Expect.equal (editorOf model).Before (WasSplit [ moebel; haushalt ]) "Stand davor"

        testCase "gesperrte Fälle öffnen den Editor nicht" <| fun () ->
            let blocked (e: LedgerEntryView) =
                let model, sent, _ = run (OpenSplitEditor e.Id) (modelWith [ e; entryOf 2 ])
                Expect.isNone model.SplitEditor "zu"
                Expect.isEmpty sent "kein Folgebefehl"
            blocked { entryOf 1 with SplitLocks = locks (Some "abgeglichen") (Some "abgeglichen") }
            blocked { splitEntry 1 with SplitLocks = locks None (Some "fremde import_id") }
            blocked { entryOf 1 with DeletedInYnab = true }
            // Eine Buchung, die nicht im Detail steht, öffnet nichts.
            let model, _, _ = run (OpenSplitEditor (idOf 2)) (modelWith [ entryOf 1; entryOf 2 ])
            Expect.isNone model.SplitEditor "nicht die Buchung im Detail"

        testCase "Entwurf bearbeiten: Betrag, Rest, Zeile hinzufügen und entfernen, Memo, Ziel" <| fun () ->
            let mutable model = opened (entryOf 1)
            let step msg =
                let m, _, _ = update msg model
                model <- m
            step (SetSplitLineAmount (0, "23,40"))
            Expect.equal (remainder (editorOf model)) 58.50m "Rest nach der ersten Zeile"
            step (SelectSplitCategory (1, Some (catId 3)))
            Expect.equal (editorOf model).Lines.[1].Target (Some (ToCategory (catId 3, "Lebensmittel"))) "Ziel aus der Kategorienliste"
            step (FillSplitLineRest 1)
            Expect.isTrue (canSave (editorOf model)) "Rest 0,00 erlaubt Speichern"
            step AddSplitLine
            Expect.equal (editorOf model).Lines.Length 2 "bei Rest 0,00 keine weitere Zeile"
            step (SetSplitLineMemo (0, "Lampe"))
            Expect.equal (editorOf model).Lines.[0].Memo "Lampe" "Memo"
            step (SelectSplitCategory (0, None))
            Expect.equal (editorOf model).Lines.[0].Target (Some Uncategorized) "Ohne Kategorie ist Uncategorized"
            step (RemoveSplitLine 0)
            Expect.equal (editorOf model).Lines.Length 1 "Zeile entfernt"

        testCase "Übertrag-Ziel: SelectSplitAccount trägt den Namen aus der Kontenliste ein" <| fun () ->
            let target = YnabAccountId (Guid.NewGuid())
            let accounts : YnabAccount list =
                [ { Id = target; Name = "Tagesgeld"; Balance = eur 0m; OnBudget = true; Closed = false } ]
            let model = { opened (entryOf 1) with TransferTargets = Success accounts }
            let after, _, _ = update (SelectSplitAccount (1, target)) model
            Expect.equal (editorOf after).Lines.[1].Target (Some (ToTransfer (target, "Tagesgeld"))) "Übertrag"
            let unknown, _, _ = update (SelectSplitAccount (1, YnabAccountId (Guid.NewGuid()))) model
            Expect.isNone (editorOf unknown).Lines.[1].Target "unbekanntes Konto ändert nichts"

        testCase "die Auswahl über dem Editor öffnet und schließt, Wahl schließt sie" <| fun () ->
            let model = opened (entryOf 1)
            let opened1, _, _ = update (OpenSplitTargetPicker 1) model
            Expect.equal (editorOf opened1).Picker (SplitTargetPickerFor 1) "Ziel-Picker"
            let picked, _, _ = update (SelectSplitCategory (1, Some (catId 2))) opened1
            Expect.equal (editorOf picked).Picker NoSplitPicker "Wahl schließt"
            let closed, _, _ = update CloseSplitPicker opened1
            Expect.equal (editorOf closed).Picker NoSplitPicker "zu"

        // ledger-hme9k Befund 3: das × im Kopf des Split-Editors am PC schloss das ganze Detail, während Esc,
        // „Abbrechen“ und das × im Sheet ins Detail zurückführen. `detailCloseStep` ist der eine Weg für Schließen.
        testCase "Schließen im Detail schließt zuerst, was im Detail offen ist" <| fun () ->
            let entry = entryOf 1
            let plain = modelWith [ entry; entryOf 2 ]
            let cases =
                [ "Split-Editor offen", opened entry, CloseSplitEditor
                  "Payee offen", { plain with PayeeEdit = Some { PayeeEntryId = entry.Id; PayeeFieldError = None } }, CancelPayeeEdit
                  "Betrag offen",
                  { plain with AmountEdit = Some { AmountEntryId = entry.Id; AmountIsOutflow = true; AmountDraft = "1"; AmountFieldError = None } },
                  CancelAmountEdit
                  // design-system-x045q: am Handy ersetzt der Memo-Editor den Inhalt des Detail-Sheets.
                  "Memo offen", { plain with MemoEdit = Some { MemoEntryId = entry.Id; MemoDraft = "x" } }, CancelMemoEdit
                  "nichts offen", plain, CloseDetail ]
            for name, model, expected in cases do
                Expect.equal (Components.Ledger.State.detailCloseStep model entry) expected name

        testCase "× im Memo-Editor führt zurück ins Detail derselben Buchung" <| fun () ->
            let entry = entryOf 1
            let model = { (modelWith [ entry ]) with MemoEdit = Some { MemoEntryId = entry.Id; MemoDraft = "x" } }
            let closed, _, _ = update (Components.Ledger.State.detailCloseStep model entry) model
            Expect.isNone closed.MemoEdit "Editor zu"
            Expect.equal (closed.DetailFor |> Option.map (fun d -> d.Id)) (Some entry.Id) "Detail bleibt"

        testCase "× im Split-Editor am PC führt zurück ins Detail derselben Buchung, nicht während des Speicherns" <| fun () ->
            let entry = entryOf 1
            let model = opened entry
            Expect.equal model.DetailForm DetailPanel "am PC"
            let closed, _, _ = update (Components.Ledger.State.detailCloseStep model entry) model
            Expect.equal (closed.DetailFor |> Option.map (fun d -> d.Id)) (Some entry.Id) "Detail von A bleibt"
            Expect.isNone closed.SplitEditor "Editor zu, Entwurf verworfen"
            let saving = { model with CategoryRows = Map.ofList [ entry.Id, CategorySending ] }
            let stays, _, _ = update (Components.Ledger.State.detailCloseStep saving entry) saving
            Expect.isSome stays.SplitEditor "während des Speicherns bleibt der Editor"
            Expect.isSome stays.DetailFor "und das Detail"

        testCase "Esc schließt erst die Auswahl, dann den Editor" <| fun () ->
            let model, _, _ = update (OpenSplitTargetPicker 1) (opened (entryOf 1))
            let first, _, _ = update EscapePressed model
            Expect.isSome first.SplitEditor "Editor bleibt"
            Expect.equal (editorOf first).Picker NoSplitPicker "Auswahl zu"
            let second, _, _ = update EscapePressed first
            Expect.isNone second.SplitEditor "Editor zu"
            Expect.isSome second.DetailFor "Detail bleibt"

        testCase "Speichern ruft setEntrySplits genau einmal, mit den Zeilen im Vorzeichen der Buchung" <| fun () ->
            let model0 = opened (entryOf 1)
            let ready =
                let m1, _, _ = update (SetSplitLineAmount (0, "23,40")) model0
                let m2, _, _ = update (SelectSplitCategory (0, Some (catId 2))) m1
                let m3, _, _ = update (SelectSplitCategory (1, Some (catId 1))) m2
                let m4, _, _ = update (FillSplitLineRest 1) m3
                m4
            let started, sent, _ = run StartSaveSplits ready
            let expected =
                [ { Target = ToCategory (catId 2, "Möbel"); Amount = eur -23.40m; Memo = None }
                  { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -58.50m; Memo = None } ]
            Expect.equal sent [ RunSaveSplits (idOf 1, expected) ] "genau ein Aufruf"
            Expect.equal (started.CategoryRows |> Map.tryFind (idOf 1)) (Some CategorySending) "sendet"
            let _, again, _ = run StartSaveSplits started
            Expect.isEmpty again "ein zweiter Klick löst keinen zweiten Aufruf aus"
            let _, notReady, _ = run StartSaveSplits model0
            Expect.isEmpty notReady "ein nicht speicherbarer Entwurf wird nie geschickt"
            let busyEdit, _, _ = update (SetSplitLineAmount (0, "1")) started
            Expect.equal (editorOf busyEdit) (editorOf started) "während des Versands ändert sich der Entwurf nicht"
            let closeBusy, _, _ = update CloseSplitEditor started
            Expect.isSome closeBusy.SplitEditor "und der Editor bleibt"

        testCase "Aufgeteilt: Bestätigung mit Rückgängig unter den Split-Zeilen, Zeile bleibt, ungeprüft, Liste und Detail zeigen die Splits" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let lines = [ moebel; haushalt ]
            let after, cmd, external = update (SplitsSaved (idOf 1, lines, Ok (changedOutcome (Some haushaltChoice) CategorySent))) model
            let sent = ResizeArray<Msg>()
            for sub in cmd do
                sub sent.Add
            // Das Detail ist offen: es holt die Historie mit der neuen Zeile (ledger-nc708), sonst nichts.
            // Das Ende des Leuchtens (ledger-p5y9s) kommt erst nach 0,7 s.
            Expect.equal (List.ofSeq sent) [ LoadHistory (idOf 1) ] "bei Erfolg nur das Neuladen der Historie"
            let row = rowOf 1 after
            Expect.equal row.Splits lines "Splits in der Liste"
            Expect.isNone row.CategoryId "keine Einzelkategorie mehr"
            Expect.isTrue row.NeedsReview "bleibt ungeprüft (4.7)"
            Expect.equal (entriesOf after |> List.map (fun e -> e.Id)) [ idOf 1; idOf 2 ] "Platz in der Liste"
            Expect.equal (Components.Ledger.Types.detailEntryOf after |> Option.map (fun e -> e.Splits)) (Some lines) "Detail"
            Expect.equal row.SplitCategoryGroupNames (Map.ofList [ catGuid 2, "Wohnen"; catGuid 1, "Alltag" ]) "Gruppen für die Anzeige"
            Expect.isNone after.SplitEditor "Editor zu"
            Expect.isFalse (after.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand weg"
            Expect.equal external NoOp "kein Toast (ledger-p5y9s)"
            match after.DetailNote with
            | Some note ->
                Expect.equal (note.Field, note.Tone, note.Text) (AtSplits, FeedbackOk, "Aufgeteilt in 2 Zeilen") "unter den Split-Zeilen"
                Expect.equal
                    note.Undo
                    (Some (UndoSplit (idOf 1, [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ])))
                    "Rückweg: eine Zeile der alten Kategorie über den vollen Betrag"
            | None -> failtest "Bestätigung am Platz der Aufteilung erwartet"

        testCase "Rückgängig stellt die Kategorie wieder her, ein zweiter Befehl ohne Sonderlogik" <| fun () ->
            let restore = [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ]
            let model = { modelWith [ splitEntry 1; entryOf 2 ] with Categories = Success categories }
            let started, sent, _ = run (StartUndoSplit (idOf 1, restore)) model
            Expect.equal sent [ RunUndoSplit (idOf 1, restore) ] "genau ein Aufruf mit den alten Zeilen"
            Expect.equal (started.CategoryRows |> Map.tryFind (idOf 1)) (Some CategorySending) "sendet"
            let after, _, external = update (SplitsRestored (idOf 1, restore, Ok (changedOutcome None CategorySent))) started
            let row = rowOf 1 after
            Expect.isEmpty row.Splits "keine Splits mehr"
            Expect.equal row.CategoryId (Some (catGuid 1)) "Kategorie zurück"
            Expect.equal row.CategoryName (Some "Haushalt") "Name"
            // ledger-p5y9s: im offenen Detail entfällt der Rücknahme-Toast, bei geschlossenem bleibt er.
            Expect.equal external NoOp "kein Rücknahme-Toast im Detail"
            Expect.isNone after.DetailNote "keine Bestätigung"
            let _, _, closedExternal = update (SplitsRestored (idOf 1, restore, Ok (changedOutcome None CategorySent))) { started with DetailFor = None }
            Expect.equal closedExternal (ShowToast ("Aufteilung zurückgenommen", ToastSuccess)) "Detail zu: ohne neuen Knopf"

        testCase "Rückgängig nach Ändern schickt die alten Zeilen" <| fun () ->
            let entry = splitEntry 1
            let model = { opened entry with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let changedLines = [ { moebel with Amount = eur -40.00m }; { haushalt with Amount = eur -41.90m } ]
            let after, _, _ = update (SplitsSaved (idOf 1, changedLines, Ok (changedOutcome None CategorySent))) model
            Expect.equal (after.DetailNote |> Option.bind (fun n -> n.Undo)) (Some (UndoSplit (idOf 1, [ moebel; haushalt ]))) "die Zeilen vor dem Speichern"

        testCase "Aufheben: eine Zeile, „Aufteilung aufgehoben“ unter Kategorie, die Buchung trägt die Kategorie" <| fun () ->
            let model = { opened (splitEntry 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let single = [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ]
            let after, _, external = update (SplitsSaved (idOf 1, single, Ok (changedOutcome None CategorySent))) model
            let row = rowOf 1 after
            Expect.isEmpty row.Splits "Split aufgehoben"
            Expect.equal row.CategoryId (Some (catGuid 1)) "Kategorie"
            Expect.equal row.CategoryGroupName (Some "Alltag") "Gruppe"
            Expect.equal external NoOp "kein Toast"
            match after.DetailNote with
            | Some note ->
                Expect.equal (note.Field, note.Text) (AtCategory, "Aufteilung aufgehoben") "unter Kategorie, es gibt keine Split-Zeilen mehr"
                Expect.equal note.Undo (Some (UndoSplit (idOf 1, [ moebel; haushalt ]))) "Rückweg: die alten Zeilen"
            | None -> failtest "Bestätigung erwartet"

        testCase "Rückgängig fehlt, wenn der Rückweg gesperrt wäre (import_id ohne BB:)" <| fun () ->
            let foreign = { entryOf 1 with SplitLocks = locks None (Some "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben") }
            let model = { opened foreign with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let after, _, _ = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) CategorySent))) model
            Expect.equal (after.DetailNote |> Option.map (fun n -> n.Text, n.Undo)) (Some ("Aufgeteilt in 2 Zeilen", None)) "nur der Text"

        testCase "Fehlschlag beim Versand nach YNAB: Hinweis an der Buchung, gelb am Platz, Buchung wird neu geholt" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let after, sent, external =
                run (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) (CategorySendFailed "HTTP 500")))) model
            Expect.equal (rowOf 1 after).CategorizationWriteBackFailure (Some "HTTP 500") "Grund an der Buchung"
            Expect.contains sent (RunRefreshEntry (idOf 1)) "die Buchung wird neu geholt (Neuanlage offen?)"
            Expect.equal external NoOp "kein Toast"
            Expect.equal
                (after.DetailNote |> Option.map (fun n -> n.Tone, n.Text))
                (Some (FeedbackWarning, "Aufgeteilt in 2 Zeilen, nicht in YNAB: HTTP 500"))
                "gelb am Platz"
            let refreshed = { rowOf 1 after with SplitLocks = Some { CannotSplitReason = None; CannotChangeReason = None; ReplacementPending = true } }
            let final, _, _ = update (EntryRefreshed (idOf 1, Some (rowOf 1 after), Ok (Some refreshed))) after
            Expect.equal (rowOf 1 final).SplitLocks refreshed.SplitLocks "Neuanlage offen sichtbar"

        testCase "Filter-Nichttreffer: die Zeile bleibt ausgegraut an ihrem Platz, Trefferzahl und Summe gehen mit" <| fun () ->
            let filters = { noFilters with CategoryIds = Set.singleton (catGuid 1) }
            let model0 = { opened (entryOf 1) with Filters = filters; CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let after, _, _ = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) CategorySent))) model0
            Expect.contains after.FilteredOut (idOf 1) "ausgegraut"
            Expect.equal (entriesOf after |> List.map (fun e -> e.Id)) [ idOf 1; idOf 2 ] "Platz"
            Expect.equal after.TotalCount (model0.TotalCount - 1) "Trefferzahl"
            Expect.equal after.LocallyRemoved 1 "Seitenversatz"

        testCase "OrderStale bei Sortierung nach Kategorie, sonst nicht" <| fun () ->
            let sortByCategory = { Field = SortByCategory; Direction = SortAsc }
            let model0 = { opened (entryOf 1) with Sort = sortByCategory; CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let stale, sent, _ =
                run (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome None CategorySent))) model0
            Expect.isTrue stale.OrderStale "Reihenfolge veraltet"
            Expect.contains sent RunRefreshTotals "Zähler neu holen"
            let normal, _, _ =
                run (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome None CategorySent)))
                    { model0 with Sort = LedgerSort.defaultSort }
            Expect.isFalse normal.OrderStale "andere Sortierung bleibt stehen"

        testCase "Ziel gleich Ist: keine Änderung, Editor zu, kein Toast" <| fun () ->
            let model = { opened (splitEntry 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let unchanged : CategoryChangeOutcome = { Previous = None; Change = CategoryUnchanged; WriteBack = CategoryNotSent; PreviousTransferAccountId = None }
            let after, _, external = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok unchanged)) model
            Expect.isNone after.SplitEditor "zu"
            Expect.equal (rowOf 1 after).Splits [ moebel; haushalt ] "unverändert"
            Expect.equal external NoOp "kein Toast"

        testCase "Rückgängig gescheitert: rot am Platz der Bestätigung (ledger-p5y9s)" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let saved, _, _ = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) CategorySent))) model
            let restore = saved.DetailNote.Value.Undo |> Option.bind detailNoteUndoMsg |> Option.get
            let started, _, _ = update restore saved
            let failed, _, external = update (SplitsRestored (idOf 1, [], Error "YNAB weg")) started
            Expect.equal external NoOp "kein Toast"
            Expect.equal
                (failed.DetailNote |> Option.map (fun n -> n.Field, n.Tone, n.Text))
                (Some (AtSplits, FeedbackError, "Rückgängig nicht möglich: YNAB weg"))
                "rot unter den Split-Zeilen"

        testCase "Speicherfehler bei schon geschlossenem Editor: die Leiste (am PC der Toast) meldet ihn" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let editorClosed = { model with SplitEditor = None }
            let _, _, external = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Error "YNAB weg")) editorClosed
            Expect.equal external (ShowToast ("Aufteilung nicht gespeichert: YNAB weg", ToastError)) "rot in der Leiste"

        testCase "wechselt das Detail auf eine andere Buchung, endet der Editor" <| fun () ->
            let model = opened (entryOf 1)
            let switched, _, _ = update (OpenDetail (entryOf 2)) model
            Expect.isNone switched.SplitEditor "Entwurf verworfen"
            let closed, _, _ = update CloseDetail model
            Expect.isNone closed.SplitEditor "mit dem Detail zu"

        testCase "bei offenem Editor sind die Listentasten aus" <| fun () ->
            let model = opened (entryOf 1)
            let moved, _, _ = update (MoveFocus 1) model
            Expect.equal moved.FocusedRow model.FocusedRow "Pfeiltaste bewegt nichts"
            let reviewed, sent, _ = run (ReviewKeyPressed Reviewed) model
            Expect.isEmpty sent "G prüft nicht"
            Expect.equal reviewed.ReviewMarkRows model.ReviewMarkRows "keine Zeilenänderung"

        testCase "TransferTargetsLoaded setzt die Liste, auch den Fehler" <| fun () ->
            let ok, _, _ = update (TransferTargetsLoaded (Ok [])) (opened (entryOf 1))
            Expect.equal ok.TransferTargets (Success []) "geladen"
            let failed, _, _ = update (TransferTargetsLoaded (Error "Netz")) (opened (entryOf 1))
            Expect.equal failed.TransferTargets (Failure "Netz") "Fehler"
    ]

// ============================================
// Ansicht
// ============================================

[<Tests>]
let viewTests =
    testList "Split-Editor: Ansicht (ledger-pafkp)" [

        // ledger-ng110: Gelb heißt „braucht dich, kein Fehler“ (Styleguide §2, ADR 0087 Punkt 3), Rot „Fehler und
        // Minus“. Ein offener Rest ist kein Fehler, zu viel verteilt schon.
        testCase "Rest-Farbe: offen gelb, zu viel verteilt rot, ausgeglichen neutral" <| fun () ->
            let balanced = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "58,50" ]
            let open' = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "" ]
            let over = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "60,00" ]
            let b = Components.Ledger.View.splitSummaryProps balanced
            let o = Components.Ledger.View.splitSummaryProps open'
            let v = Components.Ledger.View.splitSummaryProps over
            Expect.equal b.Rest SplitEditor.RestBalanced "ausgeglichen"
            Expect.equal o.Rest SplitEditor.RestOpen "Rest offen"
            Expect.equal v.Rest SplitEditor.RestOver "zu viel verteilt"
            Expect.stringContains (SplitEditor.restAmountClass b.Rest) "text-text-primary" "ausgeglichen neutral"
            Expect.stringContains (SplitEditor.restAmountClass o.Rest) Tokens.Colors.attention "offen gelb"
            Expect.isFalse ((SplitEditor.restAmountClass o.Rest).Contains "text-error") "offen nicht rot"
            Expect.stringContains (SplitEditor.restAmountClass v.Rest) "text-error" "zu viel rot"
            Expect.equal o.RestText "58,50 €" "Rest als Betrag"
            Expect.equal v.RestText "-1,50 €" "zu viel mit Minus"
            Expect.equal o.TotalText "81,90 €" "Gesamtbetrag ohne Vorzeichen"
            Expect.equal (SplitEditor.restRowLabel SplitEditor.RestBalanced) "Stimmt" "ausgeglichen"
            Expect.equal (SplitEditor.restRowLabel SplitEditor.RestOpen) "Rest" "offen"
            Expect.equal (SplitEditor.restRowLabel SplitEditor.RestOver) "Rest" "zu viel"

        // ledger-ng110: ein unlesbarer Zeilenbetrag zählte still als 0, niemand sagte, welche Zeile schuld ist.
        testCase "lineAmountInvalid: leer und lesbar gültig, Unlesbares ungültig" <| fun () ->
            Expect.isFalse (lineAmountInvalid "") "leer"
            Expect.isFalse (lineAmountInvalid "  ") "nur Leerzeichen"
            Expect.isFalse (lineAmountInvalid "12,50") "Komma"
            Expect.isFalse (lineAmountInvalid "12.50") "Punkt"
            Expect.isTrue (lineAmountInvalid "abc") "Buchstaben"
            Expect.isTrue (lineAmountInvalid "1,2,3") "zwei Trenner"

        testCase "der Knopf heißt Aufteilen ohne Split und Ändern mit Split" <| fun () ->
            // ledger-r8h19: die 44 px misst `[layout] Handy (390x844): Aufteilen im selben Sheet, …`.
            Expect.equal (splitActionLabel (entryOf 1)) "Aufteilen" "unaufgeteilt"
            Expect.equal (splitActionLabel (splitEntry 1)) "Ändern" "aufgeteilt"
    ]

/// Ein Root-Modell mit gegebenem Stand der Settings, für das Quick-Add-Konto (ledger-pz028).
let private rootWith (settings: RemoteData<AppSettingsView>) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = Page.Ledger
      Feedback = None
      Pwa = Pwa.initialState
      Settings = { settingsModel with Settings = settings }
      Fetch = Components.Fetch.Types.init
      Rules = rulesModel
      Ledger = ledgerModel
      Budget = Components.Budget.State.init ()
      Overview = Components.Overview.Types.init
      SettingsReturn = Page.Overview }

let private settingsWithQuickAdd (account: YnabAccountId option) : AppSettingsView =
    { Comdirect = None
      Ynab = Some { HasToken = true; DefaultBudgetId = Some (YnabBudgetId "budget"); DefaultAccountId = None; QuickAddAccountId = account }
      Sync = { DaysToFetch = 30; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 360 } }

let private quickAddMsg = State.LedgerMsg LoadQuickAddAccount

/// Nur für `Cmd.ofMsg`/`Cmd.batch`/`Cmd.map`, kein `Cmd.OfAsync`.
let private rootSent (cmd: Elmish.Cmd<State.Msg>) : State.Msg list =
    let collected = ResizeArray<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

// ledger-s8t3s: Knopf "Barabhebung" im Split-Editor des Kassenbuchs.
[<Tests>]
let cashbackTests =
    let cashAccountId = YnabAccountId (Guid.Parse "0195f6a3-0000-7000-8000-0000000058a1")
    let cashAccount : YnabAccount =
        { Id = cashAccountId; Name = "Bargeld s8t3s"; Balance = eur 0m; OnBudget = true; Closed = false }
    let otherAccount : YnabAccount =
        { Id = YnabAccountId (Guid.Parse "0195f6a3-0000-7000-8000-0000000058a2")
          Name = "Tagesgeld s8t3s"
          Balance = eur 0m
          OnBudget = true
          Closed = false }
    let toCash = Some (ToTransfer (cashAccountId, "Bargeld s8t3s"))

    testList "Split-Editor: Barabhebung (ledger-s8t3s)" [

        testCase "frisch geöffnet: bisherige Kategorie plus Übertrag aufs Bar-Konto, Beträge leer" <| fun () ->
            let state = startState (entryOf 1) |> startCashback toCash
            Expect.equal
                state.Lines
                [ lineWith toHaushalt ""; lineWith toCash "" ]
                "Kategorie der Buchung und Übertrag, ohne Beträge (-81,90 € bzw. -81900 Milliunits bleibt als Rest offen)"
            Expect.equal (remainder state) 81.90m "kein automatischer Ausgleich"

        testCase "die erste Zeile ohne Ziel wird zum Übertrag, andere Zeilen bleiben unangetastet" <| fun () ->
            let state =
                stateOf -81.90m [ lineWith toMoebel "20,00"; lineWith None ""; lineWith None "" ]
            let result = startCashback toCash state
            Expect.equal
                result.Lines
                [ lineWith toMoebel "20,00"; lineWith toCash ""; lineWith None "" ]
                "genau die erste leere Zeile, die zweite leere bleibt leer"

        testCase "ohne Zeile ohne Ziel kommt eine neue Übertrag-Zeile dazu, auch bei Rest null" <| fun () ->
            let state = stateOf -81.90m [ lineWith toMoebel "20,00"; lineWith toHaushalt "61,90" ]
            Expect.isFalse (canAddLine state) "Vorbedingung: Zeile hinzufügen ist ausgegraut"
            let result = startCashback toCash state
            Expect.equal
                result.Lines
                [ lineWith toMoebel "20,00"; lineWith toHaushalt "61,90"; lineWith toCash "" ]
                "bestehende Zeilen und Beträge unverändert, neue Zeile hinten"

        testCase "ohne Quick-Add-Konto bleibt das Ziel leer und der Ziel-Picker der Zeile geht auf" <| fun () ->
            let result = startCashback None (startState (entryOf 1))
            Expect.equal result.Lines [ lineWith toHaushalt ""; lineWith None "" ] "Zeile ohne Ziel bleibt"
            Expect.equal result.Picker (SplitTargetPickerFor 1) "Auswahl für die erste leere Zeile"

        testCase "ohne Quick-Add-Konto und ohne leere Zeile hängt der Knopf eine leere Zeile an" <| fun () ->
            let state = stateOf -10m [ lineWith toMoebel "10,00" ]
            let result = startCashback None state
            Expect.equal result.Lines [ lineWith toMoebel "10,00"; lineWith None "" ] "neue leere Zeile"
            Expect.equal result.Picker (SplitTargetPickerFor 1) "Auswahl für die neue Zeile"

        testCase "der Ausgleich geht danach über Rest wie bisher" <| fun () ->
            let state = startState (entryOf 1) |> startCashback toCash
            let typed = setAmountText 1 "50,00" state
            let balanced = fillRest 0 typed
            Expect.equal balanced.Lines.[0].AmountText "31,90" "Rest an der Kategorie-Zeile"
            Expect.isTrue (isBalanced balanced) "ausgeglichen"
            Expect.isTrue (canSave balanced) "speicherbar über den bestehenden Schreibweg"
            match requestLines balanced with
            | Some [ cat; transfer ] ->
                Expect.equal cat.Amount.Amount -31.90m "Kategorie -31,90 € (-31900 Milliunits)"
                Expect.equal transfer.Target (ToTransfer (cashAccountId, "Bargeld s8t3s")) "Übertrag aufs Bar-Konto"
                Expect.equal transfer.Amount.Amount -50.00m "Übertrag -50,00 € (-50000 Milliunits)"
            | other -> failtestf "zwei Zeilen erwartet, war %A" other

        testCase "cashbackTarget nimmt das Quick-Add-Konto, wenn es wählbar ist" <| fun () ->
            Expect.equal (cashbackTarget (Some cashAccountId) [ otherAccount; cashAccount ]) toCash "gefunden"

        testCase "cashbackTarget ist leer ohne Einstellung oder wenn das Konto nicht wählbar ist" <| fun () ->
            Expect.isNone (cashbackTarget None [ cashAccount ]) "nicht eingestellt"
            Expect.isNone (cashbackTarget (Some cashAccountId) [ otherAccount ]) "nicht unter den Zielen (zu, Tracking oder eigenes Konto)"
            Expect.isNone (cashbackTarget (Some cashAccountId) []) "Liste noch nicht geladen"

        testCase "Reducer: StartSplitCashback verändert den offenen Entwurf" <| fun () ->
            let model = opened (entryOf 1)
            let result, _, _ = update (StartSplitCashback toCash) model
            Expect.equal (editorOf result).Lines [ lineWith toHaushalt ""; lineWith toCash "" ] "Übertrag in der zweiten Zeile"

        testCase "Reducer: Barabhebung ohne Ziel frischt die Kategorien auf, denn der Ziel-Picker zeigt sie" <| fun () ->
            let model = opened (entryOf 1)
            let _, sent, _ = run (StartSplitCashback None) model
            Expect.contains sent EnsureCategoriesFresh "Picker geht auf"
            let _, sentWithTarget, _ = run (StartSplitCashback toCash) model
            Expect.isEmpty sentWithTarget "mit Ziel kein Picker"

        testCase "Reducer: das Öffnen des Editors fragt nach dem Quick-Add-Konto" <| fun () ->
            let _, sent, _ = run (OpenSplitEditor (idOf 1)) (modelWith [ entryOf 1; entryOf 2 ])
            Expect.contains sent LoadQuickAddAccount "beim Öffnen"

        // ledger-pz028: das Konto kommt aus den Settings des Root, kein eigener Abruf.
        testCase "Root: mit geladenen Settings steht das Quick-Add-Konto sofort fest, ohne Aufruf" <| fun () ->
            let model, cmd = State.update quickAddMsg (rootWith (Success (settingsWithQuickAdd (Some cashAccountId))))
            Expect.equal model.Ledger.QuickAddAccount (Success (Some cashAccountId)) "aus den Settings"
            Expect.isEmpty cmd "kein Aufruf"

        testCase "Root: laden die Settings noch, wartet das Konto auf ihre Antwort, mit genau einem LoadSettings" <| fun () ->
            let asked, firstCmd = State.update quickAddMsg (rootWith NotAsked)
            Expect.equal asked.Ledger.QuickAddAccount Loading "wartet"
            Expect.equal (rootSent firstCmd) [ State.SettingsMsg Components.Settings.Types.LoadSettings ] "die Settings laden"
            let loading = { asked with Settings = { asked.Settings with Settings = Loading } }
            let again, secondCmd = State.update quickAddMsg loading
            Expect.isEmpty (rootSent secondCmd) "kein zweites LoadSettings, solange eines läuft"
            let loaded, _ =
                State.update
                    (State.SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok (settingsWithQuickAdd (Some cashAccountId)))))
                    again
            Expect.equal loaded.Ledger.QuickAddAccount (Success (Some cashAccountId)) "aus der Antwort"

        testCase "Root: scheitern die Settings, ist das Konto Failure und die Übertrag-Zeile hat kein Ziel" <| fun () ->
            let asked, _ = State.update quickAddMsg (rootWith NotAsked)
            let failed, _ = State.update (State.SettingsMsg (Components.Settings.Types.SettingsLoaded (Error "weg"))) asked
            Expect.equal failed.Ledger.QuickAddAccount (Failure "weg") "der Fehler ist der Zustand"
            Expect.isNone (cashbackTarget (quickAddAccountOf failed.Ledger) [ cashAccount; otherAccount ]) "leer und wählbar"
    ]

// ledger-ng110: Ein abgelehntes Speichern stand nur im Toast, während der Editor offen blieb (§7.13: blockierende
// Fehler gehören in `ErrorDisplay`). Aufzählung aller Reihenfolgen durch das echte `update` (Doktrin Punkt 20):
// Speichern, Schließen (Editor oder Detail), Eingabe und Antwort (Erfolg, Fehler), je Schreibweg. Das Orakel kennt
// nur die Regel „Editor offen bei der Antwort ⇒ Fehler im Editor, kein Toast; sonst Toast“, die nächste Eingabe
// oder das nächste Speichern nimmt den Fehler zurück.

type private SaveStep =
    | SaveStep
    | InputStep
    | CloseEditorStep
    | CloseDetailStep
    | AnswerOkStep
    | AnswerFailStep

/// Ein Schreibweg des Editors: Splits allein oder Betrag und Splits zusammen (ADR 0092 Punkt 3).
type private SaveWay =
    { Name: string
      Base: Model
      Ok: Msg
      Fail: Msg
      /// Der Anfang der Meldung im Editor und im Toast.
      Prefix: string }

/// Was das Orakel weiß: steht der Editor offen, läuft ein Aufruf, steht ein Fehler im Editor.
type private SaveOracle = { Open: bool; Pending: bool; Error: bool }

let private saveWays : SaveWay list =
    let ready =
        let mutable model = opened (entryOf 1)
        for msg in
            [ SetSplitLineAmount (0, "23,40")
              SelectSplitCategory (0, Some (catId 2))
              SelectSplitCategory (1, Some (catId 1))
              FillSplitLineRest 1 ] do
            let m, _, _ = update msg model
            model <- m
        model
    let lines = [ moebel; haushalt ]
    let ok = Ok (changedOutcome (Some haushaltChoice) CategorySent)
    [ { Name = "Aufteilen"
        Base = ready
        Ok = SplitsSaved (idOf 1, lines, ok)
        Fail = SplitsSaved (idOf 1, lines, Error "weg")
        Prefix = "Aufteilung nicht gespeichert" }
      { Name = "Betrag mit Aufteilung"
        Base = { ready with SplitEditor = ready.SplitEditor |> Option.map (fun e -> { e with AmountFrom = Some (eur -90m) }) }
        Ok = SplitsWithAmountSaved (idOf 1, lines, eur -81.90m, ok)
        Fail = SplitsWithAmountSaved (idOf 1, lines, eur -81.90m, Error "weg")
        Prefix = "Betrag nicht geändert" } ]

/// Welche Schritte das Orakel im Zustand zulässt: Speichern, Eingabe und Schließen nur bei offenem Editor, eine
/// Antwort nur bei laufendem Aufruf.
let private stepsFor (o: SaveOracle) : SaveStep list =
    [ if o.Open then
          yield SaveStep
          yield InputStep
          yield CloseEditorStep
          yield CloseDetailStep
      if o.Pending then
          yield AnswerOkStep
          yield AnswerFailStep ]

/// Das Orakel: der erwartete Zustand und ob die Antwort einen Toast schickt.
let private expectAfter (step: SaveStep) (o: SaveOracle) : SaveOracle * bool =
    match step with
    | SaveStep when o.Pending -> o, false
    | SaveStep -> { o with Pending = true; Error = false }, false
    | InputStep when o.Pending -> o, false
    | InputStep -> { o with Error = false }, false
    // Während des Aufrufs bleibt der Editor (Knopf gesperrt), das Detail lässt sich schließen.
    | CloseEditorStep when o.Pending -> o, false
    | CloseEditorStep
    | CloseDetailStep -> { o with Open = false; Error = false }, false
    | AnswerOkStep -> { o with Open = false; Pending = false; Error = false }, false
    | AnswerFailStep when o.Open -> { o with Pending = false; Error = true }, false
    | AnswerFailStep -> { o with Pending = false; Error = false }, true

let private stepMsg (way: SaveWay) (n: int) (step: SaveStep) : Msg =
    match step with
    | SaveStep -> StartSaveSplits
    | InputStep -> SetSplitLineMemo (0, $"Notiz {n}")
    | CloseEditorStep -> CloseSplitEditor
    | CloseDetailStep -> CloseDetail
    | AnswerOkStep -> way.Ok
    | AnswerFailStep -> way.Fail

[<Tests>]
let saveErrorTests =
    testList "Split-Editor: Speicherfehler im offenen Editor (ledger-ng110)" [
        for way in saveWays do
            testCase $"{way.Name}: alle Reihenfolgen aus Speichern, Schließen, Eingabe und Antwort" <| fun () ->
                let mutable walks = 0
                let mutable inEditor = 0
                let mutable toasts = 0
                let rec walk (depth: int) (path: SaveStep list) (model: Model) (o: SaveOracle) =
                    if depth > 0 then
                        for step in stepsFor o do
                            let path' = path @ [ step ]
                            let where = $"{way.Name}: %A{path'}"
                            let model', _, external = update (stepMsg way depth step) model
                            let o', toast = expectAfter step o
                            Expect.equal model'.SplitEditor.IsSome o'.Open $"Editor offen: {where}"
                            Expect.equal
                                (model'.CategoryRows |> Map.tryFind (idOf 1) = Some CategorySending)
                                o'.Pending
                                $"Aufruf läuft: {where}"
                            match model'.SplitEditor with
                            | Some editor ->
                                if o'.Error then
                                    Expect.equal editor.SaveError (Some $"{way.Prefix}: weg") $"Fehler im Editor: {where}"
                                    inEditor <- inEditor + 1
                                else
                                    Expect.isNone editor.SaveError $"kein Fehler im Editor: {where}"
                            | None -> ()
                            match step, external with
                            | AnswerFailStep, ShowToast (text, ToastError) when toast ->
                                Expect.equal text $"{way.Prefix}: weg" $"Toast: {where}"
                                toasts <- toasts + 1
                            | AnswerFailStep, other when toast -> failtestf "Toast erwartet: %s, war %A" where other
                            | AnswerFailStep, other -> Expect.equal other NoOp $"kein Toast bei offenem Editor: {where}"
                            | _ -> ()
                            walks <- walks + 1
                            walk (depth - 1) path' model' o'
                walk 6 [] way.Base { Open = true; Pending = false; Error = false }
                Expect.isGreaterThan walks 100 "genug Reihenfolgen"
                Expect.isGreaterThan inEditor 10 "Fehler im Editor erreicht"
                Expect.isGreaterThan toasts 10 "Toast bei geschlossenem Editor erreicht"
    ]
