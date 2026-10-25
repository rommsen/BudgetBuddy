module LedgerLenaFlagBatchClientTests

// ledger-y54gr: Lena fragen in der Auswahl-Leiste, Client-Seite (ADR 0053 Punkt 2, ADR 0056).
// Der Reducer und die reinen Bausteine laufen ohne Proxy: `Start...` setzt den Zustand, der
// Aufruf steckt hinter `Run...` (ADR 0020). Feliz-Elemente lassen sich unter .NET nicht
// inspizieren; die Verdrahtung der Leiste prüfen Quelltext-Zusicherungen.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f700-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e700-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -10m
      Payee = Some $"Payee y54gr {n}"
      Memo = None
      CategoryName = None
      CategoryId = None
      CategoryGroupName = None
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
      LinkedToYnab = true
      PushToYnabFailure = None; SplitLocks = None }

let private lenaEntry (n: int) = { entryOf n with Flag = AskLena }
let private idOf (n: int) = (entryOf n).Id

let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        LenaFlagColor = Some "purple"
        AskLenaCount = rows |> List.filter (fun e -> e.Flag = AskLena) |> List.length }

let private selecting (ids: int list) (model: Model) : Model =
    { model with Selection = Picked (ids |> List.map idOf |> Set.ofList); SelectionMode = true }

let private update = Components.Ledger.State.update

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private rowOf (n: int) (model: Model) =
    match model.Entries with
    | Success entries -> entries |> List.find (fun e -> e.Id = idOf n)
    | _ -> failtest "Entries sollte geladen sein"

let private noFlagState : FlagState = { Color = None; Name = None }
let private lenaState : FlagState = { Color = Some "purple"; Name = Some "Lena" }
let private markedState : FlagState = { Color = Some "red"; Name = Some "Markiert" }

let private change (n: int) (previous: FlagState) (writeBack: FlagWriteBack) : FlagBatchChange =
    { EntryId = idOf n; Previous = previous; WriteBack = writeBack }

let private outcome (changes: FlagBatchChange list) : FlagBatchOutcome =
    { Changed = changes; UnchangedCount = 0; FailedCount = 0 }

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

[<Tests>]
let reducerTests =
    testList "Lena fragen für die Auswahl: Reducer (ledger-y54gr)" [

        testCase "Start sperrt die Sammelaktionen und löst den Aufruf mit der Auswahl aus" <| fun () ->
            let model = modelWith [ entryOf 1; entryOf 2 ] |> selecting [ 1; 2 ]
            let model', msgs, _ = run (StartLenaBatch AssignLena) model
            Expect.isTrue model'.LenaBatchRunning "gesperrt"
            match msgs with
            | [ RunLenaBatch (BatchPicked ids, AssignLena) ] ->
                Expect.equal (Set.ofList ids) (Set.ofList [ idOf 1; idOf 2 ]) "die gewählten Ids"
            | other -> failtest $"RunLenaBatch erwartet, war {other}"

        testCase "ohne Auswahl, ohne Farbe (Lena fragen) oder während eines Sammelaufrufs passiert nichts" <| fun () ->
            let base' = modelWith [ entryOf 1 ]
            let noop name model target =
                let model', msgs, _ = run (StartLenaBatch target) model
                Expect.isEmpty msgs $"{name}: kein Aufruf"
                Expect.equal model'.LenaBatchRunning model.LenaBatchRunning $"{name}: Sperre unverändert"
            noop "ohne Auswahl" base' AssignLena
            noop "ohne Farbe" ({ (selecting [ 1 ] base') with LenaFlagColor = None }) AssignLena
            noop "Geprüft läuft" ({ (selecting [ 1 ] base') with ReviewMarkBatchRunning = true }) AssignLena
            noop "Kategorie läuft" ({ (selecting [ 1 ] base') with CategoryBatchRunning = true }) WithdrawLena
            noop "Lena läuft" ({ (selecting [ 1 ] base') with LenaBatchRunning = true }) WithdrawLena

        testCase "Zurücknehmen braucht die Farbe nicht" <| fun () ->
            let model = { (modelWith [ lenaEntry 1 ] |> selecting [ 1 ]) with LenaFlagColor = None }
            let _, msgs, _ = run (StartLenaBatch WithdrawLena) model
            Expect.equal (List.length msgs) 1 "Aufruf geht hinaus"

        testCase "die anderen Sammelaktionen sind während des Lena-Aufrufs gesperrt" <| fun () ->
            let model = { (modelWith [ entryOf 1 ] |> selecting [ 1 ]) with LenaBatchRunning = true }
            let _, reviewMsgs, _ = run StartReviewMarkBatch model
            Expect.isEmpty reviewMsgs "Geprüft gesperrt"
            let _, categoryMsgs, _ = run (StartCategoryBatch None) model
            Expect.isEmpty categoryMsgs "Kategorie gesperrt"
            let picker, _, _ = run ToggleBatchCategoryPicker model
            Expect.isFalse picker.BatchCategoryPickerOpen "Picker bleibt zu"

        testCase "Ergebnis: geladene Zeilen tragen Lena, die Auswahl endet, der Zähler wächst, ein Toast mit Rückgängig" <| fun () ->
            let model = { (modelWith [ entryOf 1; entryOf 2; entryOf 3 ] |> selecting [ 1; 2 ]) with LenaBatchRunning = true }
            let result = outcome [ change 1 noFlagState FlagSent; change 2 markedState FlagSent ]
            let model', _, external = run (LenaBatchDone (AssignLena, Ok result)) model
            Expect.equal (rowOf 1 model').Flag AskLena "Zeile 1"
            Expect.equal (rowOf 2 model').Flag AskLena "Zeile 2"
            Expect.equal (rowOf 3 model').Flag NoFlag "unberührt"
            Expect.equal model'.AskLenaCount 2 "Zähler"
            Expect.equal model'.Selection NoSelection "Auswahl endet"
            Expect.isFalse model'.SelectionMode "Auswahl-Modus endet"
            Expect.isFalse model'.LenaBatchRunning "Sperre frei"
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "2 Buchungen an Lena gefragt" "Satz"
                Expect.equal action.Kind (UndoFlagBatch result.Changed) "Rückgängig mit den Vorzuständen"
            | other -> failtest $"Toast mit Rückgängig erwartet, war {other}"

        testCase "Fehlschlag je Buchung: die Zeile trägt den Grund, der Toast ist eine Warnung" <| fun () ->
            let model = { (modelWith [ entryOf 1; entryOf 2 ] |> selecting [ 1; 2 ]) with LenaBatchRunning = true }
            let result = outcome [ change 1 noFlagState FlagSent; change 2 noFlagState (FlagSendFailed "Keine Verbindung") ]
            let model', _, external = run (LenaBatchDone (AssignLena, Ok result)) model
            Expect.equal (rowOf 1 model').FlagWriteBackFailure None "gesendet"
            Expect.equal (rowOf 2 model').FlagWriteBackFailure (Some "Keine Verbindung") "gescheitert"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) ->
                Expect.stringContains text "Flag nicht in YNAB: Keine Verbindung" "Hinweis mit Grund"
            | other -> failtest $"Warnung erwartet, war {other}"

        testCase "ohne Änderung: Info-Toast ohne Rückgängig, die Auswahl endet trotzdem" <| fun () ->
            let model = { (modelWith [ lenaEntry 1 ] |> selecting [ 1 ]) with LenaBatchRunning = true }
            let result : FlagBatchOutcome = { Changed = []; UnchangedCount = 1; FailedCount = 0 }
            let model', _, external = run (LenaBatchDone (AssignLena, Ok result)) model
            Expect.equal model'.Selection NoSelection "Auswahl endet"
            match external with
            | ShowToast (text, ToastInfo) -> Expect.equal text "Keine Buchung an Lena gefragt (1 ohne Änderung)" "Satz"
            | other -> failtest $"Info-Toast erwartet, war {other}"

        testCase "Fehler des Aufrufs: Sperre frei, Auswahl bleibt, Fehler-Toast" <| fun () ->
            let model = { (modelWith [ entryOf 1 ] |> selecting [ 1 ]) with LenaBatchRunning = true }
            let model', _, external = run (LenaBatchDone (AssignLena, Error "kaputt")) model
            Expect.isFalse model'.LenaBatchRunning "Sperre frei"
            Expect.notEqual model'.Selection NoSelection "Auswahl bleibt"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "kaputt" "Grund"
            | other -> failtest $"Fehler-Toast erwartet, war {other}"

        testCase "Zurücknehmen bei aktivem Chip: die Zeilen bleiben ausgegraut stehen, der Zähler sinkt" <| fun () ->
            let rows = [ lenaEntry 1; lenaEntry 2; lenaEntry 3 ]
            let model =
                { (modelWith rows |> selecting [ 1; 2 ]) with
                    LenaBatchRunning = true
                    Filters = { (modelWith rows).Filters with OnlyAskLena = true } }
            let result = outcome [ change 1 lenaState FlagSent; change 2 lenaState FlagSent ]
            let model', _, _ = run (LenaBatchDone (WithdrawLena, Ok result)) model
            Expect.equal (rowOf 1 model').Flag NoFlag "Flag weg"
            Expect.isTrue (model'.FilteredOut.Contains (idOf 1)) "Zeile 1 ausgegraut"
            Expect.isTrue (model'.FilteredOut.Contains (idOf 2)) "Zeile 2 ausgegraut"
            Expect.isFalse (model'.FilteredOut.Contains (idOf 3)) "Zeile 3 ist noch Treffer"
            Expect.equal (List.length (match model'.Entries with Success l -> l | _ -> [])) 3 "keine Zeile verschwindet"
            Expect.equal model'.AskLenaCount 1 "Zähler"
            Expect.equal model'.TotalCount 1 "Trefferzahl"

        testCase "Rückgängig stellt je Buchung den Vorzustand her, auch Markiert; der Chip nimmt sie wieder auf" <| fun () ->
            let rows = [ entryOf 1; entryOf 2 ]
            let model =
                { (modelWith rows |> selecting [ 1; 2 ]) with LenaBatchRunning = true }
            let result = outcome [ change 1 noFlagState FlagSent; change 2 markedState FlagSent ]
            let assigned, _, _ = run (LenaBatchDone (AssignLena, Ok result)) model
            let started, msgs, _ = run (StartUndoFlagBatch result.Changed) assigned
            Expect.isTrue started.LenaBatchRunning "Rücknahme sperrt"
            let items =
                match msgs with
                | [ RunUndoFlagBatch items ] -> items
                | other -> failtest $"RunUndoFlagBatch erwartet, war {other}"
            Expect.equal (items |> List.map (fun i -> i.Previous)) [ noFlagState; markedState ] "Vorzustände"
            let restoredOutcome = outcome [ change 1 lenaState FlagSent; change 2 lenaState FlagSent ]
            let restored, _, external = run (FlagBatchRestored (items, Ok restoredOutcome)) started
            Expect.equal (rowOf 1 restored).Flag NoFlag "ohne Flag zurück"
            Expect.equal (rowOf 2 restored).Flag Marked "Markiert zurück"
            Expect.equal restored.AskLenaCount 0 "Zähler zurück"
            Expect.isFalse restored.LenaBatchRunning "Sperre frei"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text "Flag bei 2 Buchungen zurückgesetzt" "Satz"
            | other -> failtest $"Erfolgs-Toast ohne Rückgängig erwartet, war {other}"

        testCase "Rückgängig bei aktivem Chip macht ausgegraute Zeilen wieder zum Treffer" <| fun () ->
            let rows = [ lenaEntry 1; lenaEntry 2 ]
            let baseModel = modelWith rows
            let model =
                { (baseModel |> selecting [ 1 ]) with
                    LenaBatchRunning = true
                    Filters = { baseModel.Filters with OnlyAskLena = true } }
            let withdrawn, _, _ =
                run (LenaBatchDone (WithdrawLena, Ok (outcome [ change 1 lenaState FlagSent ]))) model
            Expect.isTrue (withdrawn.FilteredOut.Contains (idOf 1)) "ausgegraut"
            let items : FlagRestoreItem list = [ { EntryId = idOf 1; Previous = lenaState } ]
            let restored, _, _ =
                run (FlagBatchRestored (items, Ok (outcome [ change 1 noFlagState FlagSent ]))) { withdrawn with LenaBatchRunning = true }
            Expect.equal (rowOf 1 restored).Flag AskLena "wieder Lena"
            Expect.isFalse (restored.FilteredOut.Contains (idOf 1)) "wieder Treffer"
            Expect.equal restored.TotalCount 2 "Trefferzahl zurück"

        testCase "Rückgängig lässt eine neue Auswahl stehen; ein Fehler gibt die Sperre frei" <| fun () ->
            let model = { (modelWith [ entryOf 1; entryOf 2 ] |> selecting [ 2 ]) with LenaBatchRunning = true }
            let items : FlagRestoreItem list = [ { EntryId = idOf 1; Previous = noFlagState } ]
            let restored, _, _ =
                run (FlagBatchRestored (items, Ok (outcome [ change 1 lenaState FlagSent ]))) model
            Expect.notEqual restored.Selection NoSelection "Auswahl bleibt"
            let failed, _, external = run (FlagBatchRestored (items, Error "kaputt")) model
            Expect.isFalse failed.LenaBatchRunning "Sperre frei"
            match external with
            | ShowToast (_, ToastError) -> ()
            | other -> failtest $"Fehler-Toast erwartet, war {other}"

        testCase "Der Toast-Knopf Rückgängig wird zur Nachricht des Kassenbuchs" <| fun () ->
            let changes = [ change 1 noFlagState FlagSent ]
            match State.toastActionMsg (UndoFlagBatch changes) with
            | State.LedgerMsg (StartUndoFlagBatch received) -> Expect.equal received changes "Vorzustände"
            | other -> failtestf "unerwartete Nachricht: %A" other
    ]

[<Tests>]
let textTests =
    testList "Lena fragen für die Auswahl: Texte und Leiste (ledger-y54gr)" [

        testCase "die Leiste zeigt die Aktionen mit der Zahl der Auswahl" <| fun () ->
            Expect.equal (selectionLenaLabel 12) "Lena fragen (12)" "Lena fragen"
            Expect.equal (selectionWithdrawLabel 1) "Rückfrage zurücknehmen (1)" "Zurücknehmen"

        testCase "ohne Farbe ist Lena fragen gesperrt mit Grund, mit Farbe nicht" <| fun () ->
            Expect.equal (selectionLenaDisabledReason None) (Some lenaColorMissingReason) "Grund"
            Expect.isNone (selectionLenaDisabledReason (Some "purple")) "frei"

        testCase "Toast-Sätze für Zurücknehmen mit Zählern" <| fun () ->
            let result : FlagBatchOutcome = { Changed = [ change 1 lenaState FlagSent ]; UnchangedCount = 2; FailedCount = 1 }
            Expect.equal
                (flagBatchToastText WithdrawLena result)
                "Rückfrage bei 1 Buchung zurückgenommen (2 ohne Änderung, 1 nicht in YNAB)"
                "Satz"
            Expect.equal (flagBatchToastType result) ToastWarning "nicht in YNAB ist eine Warnung"
            Expect.equal (flagBatchToastType { result with FailedCount = 0 }) ToastSuccess "Erfolg"
            Expect.equal (flagBatchToastType ({ Changed = []; UnchangedCount = 3; FailedCount = 0 }: FlagBatchOutcome)) ToastInfo "Info"

        testCase "mehrere Fehlschläge nennen den ersten Grund und die Zahl" <| fun () ->
            let result = outcome [ change 1 noFlagState (FlagSendFailed "weg"); change 2 noFlagState (FlagSendFailed "weg") ]
            Expect.stringContains (flagBatchToastText AssignLena result) "Flag nicht in YNAB: weg (bei 2 Buchungen)" "Satz"

        testCase "die Leiste reicht beide Aktionen an Handy- und PC-Leiste, Lena fragen sekundär (Quelltext)" <| fun () ->
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            let from = view.IndexOf "let selectionBars"
            let barView = view.Substring(from, view.IndexOf("let private selectionBarSpacers") - from)
            Expect.stringContains barView "selectionLenaLabel totals.Count" "Anzahl im Namen"
            Expect.stringContains barView "selectionLenaDisabledReason model.LenaFlagColor" "Grund bei fehlender Farbe"
            Expect.stringContains barView "StartLenaBatch AssignLena" "Lena fragen"
            Expect.stringContains barView "StartLenaBatch WithdrawLena" "Zurücknehmen"
            Expect.stringContains barView "model.LenaBatchRunning" "Sperre"
            let count (needle: string) = barView.Split([| needle |], StringSplitOptions.None).Length - 1
            Expect.equal (count "                lena\n                withdraw\n") 2 "in Handy- und PC-Leiste"
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "MultiSelect.fs" ]
            let rest = ds.Substring(ds.IndexOf "let private lenaButton")
            let lenaButton = rest.Substring(0, rest.IndexOf("\nlet ", 5))
            Expect.stringContains lenaButton "Variant = Button.Secondary" "sekundär"
            Expect.stringContains ds "Title = action.Reason" "der Grund steht im Tooltip"
            Expect.stringContains ds "selection-lena-reason" "am Handy steht der Grund als Zeile"

        testCase "Seitenwechsel- und Sperrlogik kennt die neue Sammelaktion (Quelltext)" <| fun () ->
            let state = readRepo [ "src"; "Client"; "State.fs" ]
            Expect.stringContains state "ledger.LenaBatchRunning" "ungesicherte Arbeit"
    ]
