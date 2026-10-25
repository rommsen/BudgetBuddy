module LedgerSplitEditorClientTests

// ledger-pafkp: Split-Editor im Kassenbuch-Detail, Client-Seite (ADR 0058, ADR 0006).
// Gruppen: die reinen Rechen- und Prüffunktionen des Entwurfs, die Sperren aus
// `LedgerEntryView.SplitLocks`, der Reducer ohne Proxy (`Start...` rein, `Run...` unrein, ADR 0020)
// und die Verdrahtung der Ansicht. Feliz-Elemente lassen sich unter .NET nicht inspizieren; die
// Ansicht prüfen reine Funktionen und Quelltext-Zusicherungen, die gerenderten Elemente das
// Playwright-Skript `scripts/ledger-split-editor.test.mjs`.

open System
open System.IO
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
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = unlocked }

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
    { Previous = previous; Change = CategoryChanged; WriteBack = writeBack }

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
      Before = WasPlain (Some haushaltChoice) }

let private toMoebel = Some (ToCategory (catId 2, "Möbel"))
let private toHaushalt = Some (ToCategory (catId 1, "Haushalt"))

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private viewSource () = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]

let private functionBody (source: string) (header: string) : string =
    let start = source.IndexOf header
    if start < 0 then failtestf "Funktionskopf '%s' nicht gefunden" header
    let next = source.IndexOf("\nlet ", start + header.Length)
    if next < 0 then source.Substring start else source.Substring(start, next - start)

// ============================================
// Reine Rechen- und Prüffunktionen
// ============================================

[<Tests>]
let arithmeticTests =
    testList "Split-Editor: Rechnen und Prüfen (ledger-pafkp)" [

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

        testCase "die Konto-Auswahl filtert nach dem Namen ohne Beachtung der Schreibweise" <| fun () ->
            let accounts = [ account 1 "Tagesgeld" true false; account 2 "Bargeld" true false ]
            Expect.equal (AccountPicker.filterAccounts "" accounts |> List.length) 2 "leer: alle"
            Expect.equal (AccountPicker.filterAccounts "GELD" accounts |> List.length) 2 "beide enthalten geld"
            Expect.equal (AccountPicker.filterAccounts "tages" accounts |> List.map (fun a -> a.Name)) [ "Tagesgeld" ] "Teilwort"
            Expect.isEmpty (AccountPicker.filterAccounts "xyz" accounts) "kein Treffer"
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
                Expect.equal (Components.Ledger.View.splitLockTextFor plain) (Some reason) "Hinweistext im Detail"
                Expect.equal (categoryLockHint (categoryLockFor split)) (Some reason) "Pfeil mit Tooltip"
                Expect.isNone (Components.Ledger.View.categoryArrowMsg (modelWith [ split ]) split) "Pfeil inaktiv"

        testCase "in YNAB gelöscht sperrt alles mit eigenem Hinweis" <| fun () ->
            let deleted = { entryOf 1 with DeletedInYnab = true }
            Expect.isNone (Components.Ledger.View.splitActionMsg deleted) "gesperrt"
            Expect.isSome (Components.Ledger.View.splitLockTextFor deleted) "Hinweis"
            let deletedSplit = { splitEntry 1 with DeletedInYnab = true }
            Expect.isNone (Components.Ledger.View.splitActionMsg deletedSplit) "aufgeteilt und gelöscht"

        testCase "fremde import_id sperrt nur Ändern, nicht Aufteilen" <| fun () ->
            let l = locks None (Some foreign)
            let plain = { entryOf 1 with SplitLocks = l }
            let split = { splitEntry 1 with SplitLocks = l }
            Expect.isSome (Components.Ledger.View.splitActionMsg plain) "Aufteilen bleibt aktiv"
            Expect.isNone (Components.Ledger.View.splitActionMsg split) "Ändern inaktiv"
            Expect.equal (Components.Ledger.View.splitLockTextFor split) (Some foreign) "Hinweistext"
            Expect.isNone (Components.Ledger.View.splitLockTextFor plain) "kein Hinweis beim Aufteilen"

        testCase "der Pfeil an der Kategorie öffnet bei einer Split-Buchung den Editor" <| fun () ->
            let model = modelWith [ splitEntry 1 ]
            Expect.equal (categoryLockFor (splitEntry 1)) CategoryOpensSplitEditor "Sperre entfällt"
            Expect.equal
                (Components.Ledger.View.categoryArrowMsg model (splitEntry 1))
                (Some (OpenSplitEditor (idOf 1)))
                "Pfeil öffnet den Editor"
            let source = readRepo [ "src"; "Client"; "Components"; "Ledger"; "Types.fs" ]
            Expect.isFalse (source.Contains "Split bearbeiten folgt") "der Hinweis ist entfernt"

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
            let state = readRepo [ "src"; "Client"; "Components"; "Ledger"; "State.fs" ]
            Expect.stringContains state "Api.ledger.retryCategorizationWriteBack" "Weg für Kategorie und Split"
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
            let opened1, _, _ = update (OpenSplitCategoryPicker 1) model
            Expect.equal (editorOf opened1).Picker (SplitCategoryPickerFor 1) "Kategorie-Picker"
            let picked, _, _ = update (SelectSplitCategory (1, Some (catId 2))) opened1
            Expect.equal (editorOf picked).Picker NoSplitPicker "Wahl schließt"
            let acc, _, _ = update (OpenSplitAccountPicker 0) model
            Expect.equal (editorOf acc).Picker (SplitAccountPickerFor 0) "Konto-Auswahl"
            let closed, _, _ = update CloseSplitPicker acc
            Expect.equal (editorOf closed).Picker NoSplitPicker "zu"

        testCase "Esc schließt erst die Auswahl, dann den Editor" <| fun () ->
            let model, _, _ = update (OpenSplitCategoryPicker 1) (opened (entryOf 1))
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

        testCase "Aufgeteilt: Toast mit Rückgängig, Zeile bleibt, ungeprüft, Liste und Detail zeigen die Splits" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let lines = [ moebel; haushalt ]
            let after, cmd, external = update (SplitsSaved (idOf 1, lines, Ok (changedOutcome (Some haushaltChoice) CategorySent))) model
            Expect.isEmpty (cmd |> List.ofSeq) "kein Folgebefehl bei Erfolg"
            let row = rowOf 1 after
            Expect.equal row.Splits lines "Splits in der Liste"
            Expect.isNone row.CategoryId "keine Einzelkategorie mehr"
            Expect.isTrue row.NeedsReview "bleibt ungeprüft (4.7)"
            Expect.equal (entriesOf after |> List.map (fun e -> e.Id)) [ idOf 1; idOf 2 ] "Platz in der Liste"
            Expect.equal (Components.Ledger.Types.detailEntryOf after |> Option.map (fun e -> e.Splits)) (Some lines) "Detail"
            Expect.equal row.SplitCategoryGroupNames (Map.ofList [ catGuid 2, "Wohnen"; catGuid 1, "Alltag" ]) "Gruppen für die Anzeige"
            Expect.isNone after.SplitEditor "Editor zu"
            Expect.isFalse (after.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand weg"
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "Aufgeteilt in 2 Zeilen" "Text"
                Expect.equal action.Label "Rückgängig" "Knopf"
                Expect.equal
                    action.Kind
                    (UndoSplit (idOf 1, [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ]))
                    "Rückweg: eine Zeile der alten Kategorie über den vollen Betrag"
            | other -> failtestf "Toast mit Rückgängig erwartet: %A" other

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
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text "Aufteilung zurückgenommen" "ohne neuen Knopf"
            | other -> failtestf "Toast erwartet: %A" other

        testCase "Rückgängig nach Ändern schickt die alten Zeilen" <| fun () ->
            let entry = splitEntry 1
            let model = { opened entry with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let changedLines = [ { moebel with Amount = eur -40.00m }; { haushalt with Amount = eur -41.90m } ]
            let _, _, external = update (SplitsSaved (idOf 1, changedLines, Ok (changedOutcome None CategorySent))) model
            match external with
            | ShowToastWithAction (_, _, action) ->
                Expect.equal action.Kind (UndoSplit (idOf 1, [ moebel; haushalt ])) "die Zeilen vor dem Speichern"
            | other -> failtestf "Toast mit Rückgängig erwartet: %A" other

        testCase "Aufheben: eine Zeile, Toast Aufteilung aufgehoben, die Buchung trägt die Kategorie" <| fun () ->
            let model = { opened (splitEntry 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let single = [ { Target = ToCategory (catId 1, "Haushalt"); Amount = eur -81.90m; Memo = None } ]
            let after, _, external = update (SplitsSaved (idOf 1, single, Ok (changedOutcome None CategorySent))) model
            let row = rowOf 1 after
            Expect.isEmpty row.Splits "Split aufgehoben"
            Expect.equal row.CategoryId (Some (catGuid 1)) "Kategorie"
            Expect.equal row.CategoryGroupName (Some "Alltag") "Gruppe"
            match external with
            | ShowToastWithAction (text, _, action) ->
                Expect.equal text "Aufteilung aufgehoben" "Text"
                Expect.equal action.Kind (UndoSplit (idOf 1, [ moebel; haushalt ])) "Rückweg: die alten Zeilen"
            | other -> failtestf "Toast mit Rückgängig erwartet: %A" other

        testCase "Rückgängig fehlt im Toast, wenn der Rückweg gesperrt wäre (import_id ohne BB:)" <| fun () ->
            let foreign = { entryOf 1 with SplitLocks = locks None (Some "Eine Split-Buchung mit einer import_id aus YNAB lässt sich nur aufteilen, nicht ändern oder aufheben") }
            let model = { opened foreign with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let _, _, external = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) CategorySent))) model
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text "Aufgeteilt in 2 Zeilen" "nur der Text"
            | other -> failtestf "Toast ohne Aktion erwartet: %A" other

        testCase "Fehlschlag beim Versand nach YNAB: Hinweis an der Buchung, Toast mit Warnung, Buchung wird neu geholt" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let after, sent, external =
                run (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok (changedOutcome (Some haushaltChoice) (CategorySendFailed "HTTP 500")))) model
            Expect.equal (rowOf 1 after).CategorizationWriteBackFailure (Some "HTTP 500") "Grund an der Buchung"
            Expect.contains sent (RunRefreshEntry (idOf 1)) "die Buchung wird neu geholt (Neuanlage offen?)"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) -> Expect.stringContains text "HTTP 500" "Grund im Toast"
            | other -> failtestf "Warnung erwartet: %A" other
            let refreshed = { rowOf 1 after with SplitLocks = Some { CannotSplitReason = None; CannotChangeReason = None; ReplacementPending = true } }
            let final, _, _ = update (EntryRefreshed (idOf 1, Ok (Some refreshed))) after
            Expect.equal (rowOf 1 final).SplitLocks refreshed.SplitLocks "Neuanlage offen sichtbar"

        testCase "Befehlsfehler: der Editor bleibt offen, Toast Aufteilung nicht gespeichert" <| fun () ->
            let model = { opened (entryOf 1) with CategoryRows = Map.ofList [ idOf 1, CategorySending ] }
            let after, _, external = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Error "Summe stimmt nicht")) model
            Expect.isSome after.SplitEditor "Entwurf geht nicht verloren"
            Expect.isFalse (after.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand weg"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "Summe stimmt nicht" "Grund"
            | other -> failtestf "Fehler-Toast erwartet: %A" other

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
            let unchanged : CategoryChangeOutcome = { Previous = None; Change = CategoryUnchanged; WriteBack = CategoryNotSent }
            let after, _, external = update (SplitsSaved (idOf 1, [ moebel; haushalt ], Ok unchanged)) model
            Expect.isNone after.SplitEditor "zu"
            Expect.equal (rowOf 1 after).Splits [ moebel; haushalt ] "unverändert"
            Expect.equal external NoOp "kein Toast"

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

        testCase "Rest-Farbe über die Klasse des Import-Editors: neutral bei 0,00 €, Warnfarbe sonst" <| fun () ->
            let balanced = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "58,50" ]
            let open' = stateOf -81.90m [ lineWith toMoebel "23,40"; lineWith toHaushalt "" ]
            let b = Components.Ledger.View.splitSummaryProps balanced
            let o = Components.Ledger.View.splitSummaryProps open'
            Expect.isFalse b.RestWarn "ausgeglichen"
            Expect.isTrue b.Balanced "Stimmt"
            Expect.isTrue o.RestWarn "Rest offen"
            Expect.stringContains (SplitEditor.amountClass b.RestWarn) "text-text-primary" "neutral"
            Expect.stringContains (SplitEditor.amountClass o.RestWarn) "text-neon-red" "Warnfarbe"
            Expect.equal o.RestText "58,50 €" "Rest als Betrag"
            Expect.equal o.TotalText "81,90 €" "Gesamtbetrag ohne Vorzeichen"
            Expect.equal (SplitEditor.restRowLabel true) "Stimmt" "ausgeglichen"
            Expect.equal (SplitEditor.restRowLabel false) "Rest" "offen"
            // dieselben Klassen wie splitAmountClass im Import-Editor
            for warn in [ true; false ] do
                Expect.equal
                    (SplitEditor.amountClass warn)
                    (Components.SyncFlow.Views.SplitSheet.splitAmountClass warn)
                    "gleiche Klasse wie der Import-Editor"

        testCase "Verdrahtung: Zeile hinzufügen nur bei offenem Rest, Speichern nach canSave, am Handy kein Abbrechen" <| fun () ->
            let source = viewSource ()
            let body = functionBody source "let private splitEditorBody"
            Expect.stringContains body "SplitLogic.canAddLine editor" "Zeile hinzufügen folgt dem Rest"
            let panel = functionBody source "let private detailPanelView"
            Expect.stringContains panel "SplitLogic.canSave editor" "Speichern im Panel nach canSave"
            Expect.stringContains panel "SplitUi.footer" "Panel: Abbrechen und Speichern"
            let sheet = functionBody source "let private detailSheetView"
            Expect.stringContains sheet "SplitUi.saveButton true" "Handy: Speichern klebend, volle Breite"
            Expect.isFalse (sheet.Contains "cancelButton") "Handy: kein Abbrechen"
            Expect.isFalse (sheet.Contains "SplitUi.footer") "Handy: keine Fußleiste mit Abbrechen"
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "SplitEditor.fs" ]
            Expect.stringContains ds "IsDisabled = not enabled" "ausgegraut"
            Expect.stringContains ds "IsDisabled = not canSave" "Speichern inaktiv"

        testCase "Kategorie je Zeile über den Picker (PC Popover, Handy sheetLayered), Übertrag über die Konto-Auswahl" <| fun () ->
            let body = functionBody (viewSource ()) "let private splitEditorBody"
            for needle in [ "CategoryPicker.popoverInPanel"; "CategoryPicker.sheetLayered"; "AccountPicker.popoverInPanel"; "AccountPicker.sheetLayered" ] do
                Expect.stringContains body needle needle
            let props = functionBody (viewSource ()) "let private splitCategoryPickerProps"
            Expect.stringContains props "LoadState = categoryLoadState model.Categories" "Ladezustand wie im Kategorie-Feld"
            Expect.stringContains props "OnRetry = fun () -> dispatch LoadCategories" "Erneut versuchen"
            let accounts = functionBody (viewSource ()) "let transferTargetsFor"
            Expect.stringContains accounts "openOnBudgetAccounts" "offene Budget-Konten"
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "AccountPicker.fs" ]
            Expect.stringContains ds "BottomSheet.accountPickerLayered" "Handy: zweites Sheet"
            Expect.stringContains ds "FilterPopover.view" "PC: Popover"

        testCase "Rechnung kommt aus Shared.Domain, nicht aus SyncFlow" <| fun () ->
            let logic = readRepo [ "src"; "Client"; "Components"; "Ledger"; "SplitEditor.fs" ]
            Expect.stringContains logic "splitRemainder" "splitRemainder"
            Expect.stringContains logic "mkSplits" "mkSplits"
            Expect.isFalse (logic.Contains "SyncFlow") "kein SyncFlow"

        testCase "Aufteilen und Ändern sind feste Knöpfe mit Trefferfläche ab 44 px" <| fun () ->
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "Detail.fs" ]
            let button = functionBody ds "let splitActionButton"
            Expect.stringContains button "min-h-11" "44 px"
            Expect.stringContains button "aria-disabled" "gesperrt ausgezeichnet"
            Expect.equal (splitActionLabel (entryOf 1)) "Aufteilen" "unaufgeteilt"
            Expect.equal (splitActionLabel (splitEntry 1)) "Ändern" "aufgeteilt"
            let field = functionBody (viewSource ()) "let private splitField"
            Expect.stringContains field "Detail.splitActionButton" "Knopf im Feld"
            Expect.stringContains field "\"Kein Split\"" "Platzhalter"

        testCase "neue Bausteine stehen in /styleguide" <| fun () ->
            let source = readRepo [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            for needle in [ "Split-Editor"; "SplitEditor.line"; "SplitEditor.summary"; "AccountPicker.popoverInPanel"; "AccountPicker.sheetLayered"; "Detail.splitActionButton" ] do
                Expect.stringContains source needle needle

        testCase "Optik nur aus Tokens: keine Hex-Werte, keine Standardfarben, kein style.fontSize" <| fun () ->
            for file in [ "SplitEditor.fs"; "AccountPicker.fs" ] do
                let text = readRepo [ "src"; "Client"; "DesignSystem"; file ]
                Expect.isFalse (System.Text.RegularExpressions.Regex.IsMatch(text, "#[0-9a-fA-F]{3,8}\\b")) $"{file}: kein Hex"
                Expect.isFalse (text.Contains "style.fontSize") $"{file}: kein style.fontSize"
                Expect.isFalse (System.Text.RegularExpressions.Regex.IsMatch(text, "(text|bg|border)-(red|green|blue|gray|slate|pink|purple)-[0-9]")) $"{file}: keine Standardfarben"
    ]
