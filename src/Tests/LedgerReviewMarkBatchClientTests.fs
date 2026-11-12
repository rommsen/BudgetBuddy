module LedgerReviewMarkBatchClientTests

// "Geprüft" für die ganze Auswahl (ledger-nhwrt), Client-Seite: der reine Reducer, die
// Toast-Sätze und die Leiste. Kein Api-Aufruf: `Start...` setzt nur den Zustand und stößt
// `Run...` an, der Proxy steckt dahinter (ADR 0020). Die Cmd-Nachrichten lesen wir aus,
// indem wir die Cmd mit einem aufzeichnenden `dispatch` ausführen.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open LedgerSelectionTests

let private update = Components.Ledger.State.update

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

/// Zeile n (Betrag -10 * n), zu prüfen.
let private row (n: int) : LedgerEntryView = entryOf n (-10m * decimal n) true false

let private rowIds (ns: int list) = ns |> List.map (fun n -> (row n).Id)

let private accountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"

let private modelWith (onlyNeedsReview: bool) (loaded: int) (total: int) : Model =
    let model, _ = Components.Ledger.State.init ()
    let entries = [ for n in 1..loaded -> row n ]
    { model with
        SelectedFilter = Some (OneAccount accountId)
        Entries = Success entries
        OnlyNeedsReview = onlyNeedsReview
        TotalCount = total
        TotalAmount = eur (entries |> List.sumBy (fun e -> e.Amount.Amount))
        NeedsReviewCount = total
        HasMoreEntries = loaded < total }

/// Führt die Cmd mit einem aufzeichnenden dispatch aus und liefert die Nachrichten.
let private messagesOf (cmd: Elmish.Cmd<Msg>) : Msg list =
    let received = ResizeArray<Msg>()
    for sub in cmd do
        sub received.Add
    List.ofSeq received

let private change (n: int) (writeBack: ReviewMarkWriteBack) : ReviewMarkBatchChange = {
    EntryId = (row n).Id
    Previous = { IsApproved = false; ClearedState = ReviewUncleared }
    WriteBack = writeBack
}

let private outcomeOf (changes: ReviewMarkBatchChange list) (unchanged: int) (failed: int) : ReviewMarkBatchOutcome =
    { Changed = changes; UnchangedCount = unchanged; FailedCount = failed; HeldCount = 0 }

let private entriesOf (model: Model) =
    match model.Entries with
    | Success entries -> entries
    | _ -> failtest "Entries sollte geladen sein"

[<Tests>]
let startTests =
    testList "Geprüft für die Auswahl: Start (ledger-nhwrt)" [

        testCase "Picked: der Stapel-Aufruf trägt die gewählten Ids" <| fun () ->
            let model = { modelWith false 5 5 with Selection = Picked (Set.ofList (rowIds [ 1; 3 ])) }
            let started, cmd, _ = update StartReviewMarkBatch model
            Expect.equal started.BatchRunning (Some ReviewMarkBatchRun) "Aktion gesperrt"
            match messagesOf cmd with
            | [ RunReviewMarkBatch (BatchPicked ids) ] ->
                Expect.equal (List.sort ids) (List.sort (rowIds [ 1; 3 ])) "genau die gewählten Ids"
            | other -> failtest $"erwartet RunReviewMarkBatch (BatchPicked ...), war {other}"

        testCase "AllExcept: der Aufruf trägt die Treffer-Anfrage und die Ausnahmen" <| fun () ->
            let model =
                { modelWith true 5 40 with
                    Selection = AllExcept (Set.ofList (rowIds [ 2 ]))
                    LocallyRemoved = 3
                    NextPage = 1 }
            let _, cmd, _ = update StartReviewMarkBatch model
            match messagesOf cmd with
            | [ RunReviewMarkBatch (BatchAllExcept (query, excluded)) ] ->
                Expect.equal excluded (rowIds [ 2 ]) "die abgewählte Zeile"
                Expect.equal query.AccountId (Some accountId) "Konto des Treffers"
                Expect.isTrue query.OnlyNeedsReview "Chip Zu prüfen gilt im Treffer"
                Expect.equal (query.Page, query.LocallyRemoved) (0, 0) "Seite und Versatz zählen für den Treffer nicht"
            | other -> failtest $"erwartet RunReviewMarkBatch (BatchAllExcept ...), war {other}"

        testCase "ohne Auswahl löst der Klick nichts aus" <| fun () ->
            let _, noSelection, _ = update StartReviewMarkBatch (modelWith false 3 3)
            Expect.isEmpty noSelection "ohne Auswahl kein Aufruf"
    ]

[<Tests>]
let doneTests =
    testList "Geprüft für die Auswahl: Ergebnis (ledger-nhwrt)" [

        testCase "Erfolg mit aktivem Chip: Auswahl aufgehoben, Zeilen verlassen die Liste, Zähler sinkt" <| fun () ->
            let model =
                { modelWith true 5 5 with
                    Selection = Picked (Set.ofList (rowIds [ 1; 2; 3 ]))
                    SelectionMode = true
                    BatchRunning = Some ReviewMarkBatchRun }
            let outcome = outcomeOf [ change 1 ReviewMarkSent; change 2 ReviewMarkSent; change 3 ReviewMarkSent ] 0 0
            let model', cmd, _ = update (ReviewMarkBatchDone (Ok outcome)) model
            Expect.equal model'.Selection NoSelection "Auswahl aufgehoben"
            Expect.isFalse model'.SelectionMode "Auswahl-Modus beendet"
            Expect.isNone model'.BatchRunning "Aktion wieder frei"
            Expect.equal (entriesOf model' |> List.map (fun e -> e.Id)) (rowIds [ 4; 5 ]) "gesetzte Zeilen verlassen die Liste"
            Expect.equal model'.NeedsReviewCount 2 "Zähler sinkt um die Zahl der gesetzten Buchungen"
            Expect.equal model'.TotalCount 2 "Treffer sinkt"
            Expect.equal model'.TotalAmount.Amount -90m "Summe ohne die drei Zeilen"
            Expect.equal model'.LocallyRemoved 3 "LocallyRemoved steigt um die Zahl der gegangenen Zeilen"
            Expect.isEmpty (messagesOf cmd) "alle Zeilen waren geladen: kein Neuladen"

        testCase "Erfolg ohne Chip: Zeilen bleiben, sind nicht mehr zu prüfen, LocallyRemoved bleibt" <| fun () ->
            let model = { modelWith false 3 3 with Selection = Picked (Set.ofList (rowIds [ 1; 2 ])) }
            let outcome = outcomeOf [ change 1 ReviewMarkSent; change 2 ReviewMarkSent ] 0 0
            let model', _, _ = update (ReviewMarkBatchDone (Ok outcome)) model
            Expect.hasLength (entriesOf model') 3 "alle bleiben"
            Expect.equal (entriesOf model' |> List.map (fun e -> e.NeedsReview)) [ false; false; true ] "zwei geprüft"
            Expect.equal model'.NeedsReviewCount 1 "Zähler sinkt"
            Expect.equal model'.LocallyRemoved 0 "nichts verlässt die Liste"

        testCase "eine Zeile mit Fehlschlag bleibt mit Hinweis stehen und zählt nicht zu LocallyRemoved" <| fun () ->
            let model = { modelWith true 3 3 with Selection = Picked (Set.ofList (rowIds [ 1; 2 ])) }
            let outcome = outcomeOf [ change 1 ReviewMarkSent; change 2 (ReviewMarkSendFailed "Keine Verbindung") ] 0 0
            let model', _, _ = update (ReviewMarkBatchDone (Ok outcome)) model
            Expect.equal (entriesOf model' |> List.map (fun e -> e.Id)) (rowIds [ 2; 3 ]) "Zeile 1 geht, Zeile 2 bleibt"
            let failed = entriesOf model' |> List.find (fun e -> e.Id = (row 2).Id)
            Expect.equal failed.ReviewMarkWriteBackFailure (Some "Keine Verbindung") "Hinweis aus der Antwort"
            Expect.equal model'.LocallyRemoved 1 "nur die gegangene Zeile"
            Expect.equal model'.NeedsReviewCount 1 "der Zähler folgt dem BB-Stand: beide gesetzt"

        testCase "AllExcept mit nie geladenen Buchungen und aktivem Chip: Treffer sinkt, Liste lädt neu" <| fun () ->
            let model =
                { modelWith true 2 30 with Selection = AllExcept (Set.ofList (rowIds [ 2 ])) }
            // 29 Buchungen gesetzt, davon nur Zeile 1 geladen
            let changes = (change 1 ReviewMarkSent) :: [ for n in 100..127 -> change n ReviewMarkSent ]
            let model', cmd, _ = update (ReviewMarkBatchDone (Ok (outcomeOf changes 0 0))) model
            Expect.equal model'.TotalCount 1 "30 minus 29"
            Expect.equal model'.NeedsReviewCount 1 "30 minus 29"
            Expect.contains (messagesOf cmd) LoadEntries "der Versatz stimmt nur nach dem Neuladen"

        testCase "Fehler des Aufrufs: Aktion wieder frei, Auswahl bleibt, Fehler-Toast" <| fun () ->
            let model =
                { modelWith true 3 3 with
                    Selection = Picked (Set.ofList (rowIds [ 1 ]))
                    BatchRunning = Some ReviewMarkBatchRun }
            let model', _, external = update (ReviewMarkBatchDone (Error "Netz weg")) model
            Expect.isNone model'.BatchRunning "wieder frei"
            Expect.equal model'.Selection model.Selection "Auswahl bleibt"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "Netz weg" "Grund im Toast"
            | other -> failtest $"erwartet Fehler-Toast, war {other}"
    ]

[<Tests>]
let toastTests =
    testList "Geprüft für die Auswahl: Toast (ledger-nhwrt)" [

        testCase "der Toast nennt 'n Buchungen geprüft' und trägt Rückgängig mit allen Vorzuständen" <| fun () ->
            let model = { modelWith false 3 3 with Selection = Picked (Set.ofList (rowIds [ 1; 2; 3 ])) }
            let outcome = outcomeOf [ change 1 ReviewMarkSent; change 2 ReviewMarkSent; change 3 ReviewMarkSent ] 0 0
            let _, _, external = update (ReviewMarkBatchDone (Ok outcome)) model
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "3 Buchungen geprüft" "Satz"
                Expect.equal action.Label "Rückgängig" "Beschriftung"
                match action.Kind with
                | UndoReviewMarkBatch items ->
                    Expect.equal (items |> List.map (fun i -> i.EntryId)) (rowIds [ 1; 2; 3 ]) "eine Rücknahme für alle"
                    Expect.equal (items |> List.map (fun i -> i.Previous)) (outcome.Changed |> List.map (fun c -> c.Previous)) "Vorzustände aus der Antwort"
                | other -> failtest $"erwartet UndoReviewMarkBatch, war {other}"
            | other -> failtest $"erwartet Toast mit Aktion, war {other}"

        testCase "ohne Änderung und mit Fehlschlägen nennt der Satz die Zahlen" <| fun () ->
            let text =
                reviewBatchToastText (outcomeOf [ change 1 ReviewMarkSent; change 2 (ReviewMarkSendFailed "x") ] 2 3)
            Expect.equal text "2 Buchungen geprüft (2 ohne Änderung, 3 nicht in YNAB, 1 nicht an YNAB gesendet)" "alle Zahlen"
            Expect.equal (reviewBatchToastText (outcomeOf [ change 1 ReviewMarkSent ] 0 0)) "1 Buchung geprüft" "Einzahl"
            Expect.equal
                (reviewBatchToastText (outcomeOf [] 2 3))
                "Keine Buchung geprüft (2 ohne Änderung, 3 nicht in YNAB)"
                "nichts geändert"

        testCase "die Art des Toasts: Erfolg, Warnung bei Problemen, Info ohne Änderung" <| fun () ->
            Expect.equal (reviewBatchToastType (outcomeOf [ change 1 ReviewMarkSent ] 1 0)) ToastSuccess "Erfolg"
            Expect.equal (reviewBatchToastType (outcomeOf [ change 1 ReviewMarkSent ] 0 1)) ToastWarning "nicht in YNAB"
            Expect.equal (reviewBatchToastType (outcomeOf [ change 1 (ReviewMarkSendFailed "x") ] 0 0)) ToastWarning "nicht gesendet"
            Expect.equal (reviewBatchToastType (outcomeOf [] 2 0)) ToastInfo "nichts zu tun"

        testCase "ohne geänderte Buchung gibt es keine Rückgängig-Aktion" <| fun () ->
            let model = { modelWith false 2 2 with Selection = Picked (Set.ofList (rowIds [ 1 ])) }
            let _, _, external = update (ReviewMarkBatchDone (Ok (outcomeOf [] 1 0))) model
            match external with
            | ShowToast (_, ToastInfo) -> ()
            | other -> failtest $"erwartet Info-Toast ohne Aktion, war {other}"

        testCase "die Wurzel übersetzt die Aktion in die Rückgängig-Nachricht mit den Vorzuständen" <| fun () ->
            let items : ReviewMarkRestoreItem list =
                [ { EntryId = (row 1).Id; Previous = { IsApproved = true; ClearedState = ReviewUncleared } } ]
            match State.toastActionMsg (UndoReviewMarkBatch items) with
            | State.LedgerMsg (StartUndoReviewMarkBatch got) -> Expect.equal got items "unverändert"
            | other -> failtestf "unerwartete Nachricht: %A" other
    ]

[<Tests>]
let undoTests =
    testList "Geprüft für die Auswahl: Rückgängig (ledger-nhwrt)" [

        testCase "Rückgängig löst den Sammel-Aufruf mit den Vorzuständen aus der Antwort aus" <| fun () ->
            let items : ReviewMarkRestoreItem list =
                [ { EntryId = (row 1).Id; Previous = { IsApproved = true; ClearedState = ReviewUncleared } }
                  { EntryId = (row 2).Id; Previous = { IsApproved = false; ClearedState = ReviewCleared } } ]
            let _, cmd, _ = update (StartUndoReviewMarkBatch items) (modelWith false 2 2)
            Expect.equal (messagesOf cmd) [ RunUndoReviewMarkBatch items ] "ein Aufruf mit genau diesen Vorzuständen"

        testCase "nach Erfolg lädt die Liste neu (resetEntries) statt Zeilen einzeln zurückzustecken" <| fun () ->
            let model = { modelWith true 2 2 with LocallyRemoved = 5; NextPage = 3 }
            let outcome = outcomeOf [ change 1 ReviewMarkSent ] 0 0
            let model', cmd, external = update (ReviewMarkBatchRestored (Ok outcome)) model
            Expect.equal model'.Entries Loading "Liste wird neu geladen"
            Expect.equal model'.LocallyRemoved 0 "Seitenversatz auf den Stand des Servers"
            Expect.equal model'.NextPage 0 "ab Seite 0"
            Expect.equal (messagesOf cmd) [ RunLoadEntriesPage 0 ] "genau eine Abfrage der Seite 0"
            Expect.equal external NoOp "kein Toast bei Erfolg"

        testCase "ein Fehlschlag beim Zurückschreiben zeigt den Hinweis, die Liste lädt trotzdem neu" <| fun () ->
            let outcome = outcomeOf [ change 1 (ReviewMarkSendFailed "Limit") ] 0 0
            let _, cmd, external = update (ReviewMarkBatchRestored (Ok outcome)) (modelWith true 2 2)
            Expect.equal (messagesOf cmd) [ RunLoadEntriesPage 0 ] "genau eine Abfrage der Seite 0"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "Limit" "Grund"
            | other -> failtest $"erwartet Fehler-Toast, war {other}"

        testCase "Fehler des Aufrufs: Toast, Liste bleibt" <| fun () ->
            let model = modelWith true 2 2
            let model', cmd, external = update (ReviewMarkBatchRestored (Error "weg")) model
            Expect.equal model'.Entries model.Entries "Liste unverändert"
            Expect.isEmpty (messagesOf cmd) "kein Neuladen"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "weg" "Grund"
            | other -> failtest $"erwartet Fehler-Toast, war {other}"
    ]

// ============================================
// Sammellauf der Auswahl: höchstens einer (ledger-sjbvc, Doktrin Punkt 20)
// ============================================

/// Ein ausgegebener Sammel-Aufruf. Der Zähler der Aufzählung ist eigen: der Test soll nicht dem
/// Feld glauben, das er prüft.
type private BatchCall =
    | ReviewCall
    | CategoryCall
    | LenaCall
    | UndoReviewCall
    | UndoCategoryCall
    | UndoFlagCall

let private batchCallOf (msg: Msg) : BatchCall option =
    match msg with
    | RunReviewMarkBatch _ -> Some ReviewCall
    | RunCategoryBatch _ -> Some CategoryCall
    | RunLenaBatch _ -> Some LenaCall
    | RunUndoReviewMarkBatch _ -> Some UndoReviewCall
    | RunUndoCategoryBatch _ -> Some UndoCategoryCall
    | RunUndoFlagBatch _ -> Some UndoFlagCall
    | _ -> None

let private batchCategory = YnabCategoryId (Guid.Parse "0195e900-0000-7000-8000-0000000000c1")

let private categoryChange : CategoryBatchChange = { EntryId = (row 1).Id; Previous = None; WriteBack = CategorySent }

let private categoryOutcome : CategoryBatchOutcome = { Changed = [ categoryChange ]; UnchangedCount = 0; Skipped = [] }

let private noFlag : FlagState = { Color = None; Name = None }

let private flagChange : FlagBatchChange = { EntryId = (row 1).Id; Previous = noFlag; WriteBack = FlagSent }

let private flagOutcome : FlagBatchOutcome = { Changed = [ flagChange ]; UnchangedCount = 0; FailedCount = 0; HeldCount = 0 }

let private flagItems : FlagRestoreItem list = [ { EntryId = (row 1).Id; Previous = noFlag } ]

let private reviewItems : ReviewMarkRestoreItem list =
    [ { EntryId = (row 1).Id; Previous = { IsApproved = false; ClearedState = ReviewUncleared } } ]

/// Alle Sammelstarts und ihr Rückgängig. Der Picker der Kategorie zählt nicht dazu: er gibt keinen
/// Aufruf aus, darf aber nicht öffnen, solange einer offen ist.
let private batchStarts : (string * Msg) list =
    [ "Geprüft", StartReviewMarkBatch
      "Kategorie", StartCategoryBatch (Some batchCategory)
      "Lena fragen", StartLenaBatch AssignLena
      "Rückfrage zurücknehmen", StartLenaBatch WithdrawLena
      "Rückgängig Geprüft", StartUndoReviewMarkBatch reviewItems
      "Rückgängig Kategorie", StartUndoCategoryBatch [ categoryChange ]
      "Rückgängig Lena", StartUndoFlagBatch [ flagChange ] ]

/// Der Start, der den Aufruf ausgegeben hat, für die Meldung.
let private callName (call: BatchCall) : string =
    match call with
    | ReviewCall -> "Geprüft"
    | CategoryCall -> "Kategorie"
    | LenaCall -> "Lena"
    | UndoReviewCall -> "Rückgängig Geprüft"
    | UndoCategoryCall -> "Rückgängig Kategorie"
    | UndoFlagCall -> "Rückgängig Lena"

/// Die beiden Antworten (Ok und Error) eines offenen Aufrufs.
let private answersOf (call: BatchCall) : (string * Msg) list =
    match call with
    | ReviewCall ->
        [ "Geprüft Ok", ReviewMarkBatchDone (Ok (outcomeOf [ change 1 ReviewMarkSent ] 0 0))
          "Geprüft Error", ReviewMarkBatchDone (Error "weg") ]
    | CategoryCall ->
        [ "Kategorie Ok", CategoryBatchDone (Some batchCategory, Ok categoryOutcome)
          "Kategorie Error", CategoryBatchDone (Some batchCategory, Error "weg") ]
    | LenaCall ->
        [ "Lena Ok", LenaBatchDone (AssignLena, Ok flagOutcome)
          "Lena Error", LenaBatchDone (AssignLena, Error "weg") ]
    | UndoReviewCall ->
        [ "Rückgängig Geprüft Ok", ReviewMarkBatchRestored (Ok (outcomeOf [ change 1 ReviewMarkSent ] 0 0))
          "Rückgängig Geprüft Error", ReviewMarkBatchRestored (Error "weg") ]
    | UndoCategoryCall ->
        [ "Rückgängig Kategorie Ok", CategoryBatchRestored [ None, Ok categoryOutcome ]
          "Rückgängig Kategorie Error", CategoryBatchRestored [ None, Error "weg" ] ]
    | UndoFlagCall ->
        [ "Rückgängig Lena Ok", FlagBatchRestored (flagItems, Ok flagOutcome)
          "Rückgängig Lena Error", FlagBatchRestored (flagItems, Error "weg") ]

/// Drei geladene Zeilen auf einem Konto, Zeile 1 gewählt, die Flag-Farbe gesetzt.
let private batchBase () : Model =
    { modelWith false 3 3 with
        Selection = Picked (Set.ofList (rowIds [ 1 ]))
        LenaFlagColor = Success (Some "purple") }

/// Nach einem Ergebnis endet die Auswahl; langes Drücken beginnt eine neue.
let private selectRow : Msg = StartSelectionMode (row 1).Id

/// Alle Ereignisse in diesem Zustand: jeder Start, der Picker, eine neue Auswahl (nach einem
/// Ergebnis endet sie) und die Antworten des offenen Aufrufs.
let private batchEvents (model: Model) (pending: BatchCall list) : (string * Msg) list =
    [ yield! batchStarts
      if model.BatchCategoryPickerOpen then "Picker zu", CloseBatchCategoryPicker
      else "Picker", ToggleBatchCategoryPicker
      if Components.Ledger.State.reviewMarkBatchSelection model |> Option.isNone then "Zeile wählen", selectRow
      for call in List.distinct pending do
          yield! answersOf call ]

/// Macht jeden Start möglich: ohne Auswahl wählt langes Drücken Zeile 1 (durch das echte update).
/// "Alle" ginge nach dem Neuladen nicht: ohne Treffer ist die Auswahl leer.
let private armed (model: Model) : Model =
    if Components.Ledger.State.reviewMarkBatchSelection model |> Option.isSome then model
    else
        let model', _, _ = update selectRow model
        model'

/// Geht alle Folgen bis `depth` ab. Ein Verstoß wird mit dem Schritt davor gesammelt, so nennt die
/// Meldung jedes verletzte Paar einmal statt nur den ersten Pfad.
let private walkBatches (depth: int) : int * string list =
    let mutable steps = 0
    let violations = Collections.Generic.SortedSet<string>()
    let isStart (name: string) = batchStarts |> List.exists (fun (n, _) -> n = name)
    let rec go (left: int) (path: string list) (model: Model) (pending: BatchCall list) =
        for name, msg in batchEvents model pending do
            let after, cmd, external = update msg model
            steps <- steps + 1
            let path' = name :: path
            let pair =
                match pending with
                | call :: _ -> $"{callName call} offen, dann {name}"
                | [] -> path' |> List.truncate 2 |> List.rev |> String.concat " -> "
            let fail (what: string) = violations.Add $"{what}: {pair}" |> ignore
            let issued = messagesOf cmd |> List.choose batchCallOf
            let answered =
                pending |> List.tryFind (fun call -> answersOf call |> List.exists (fun (n, _) -> n = name))
            let remaining =
                match answered with
                | Some call -> List.removeAt (List.findIndex ((=) call) pending) pending
                | None -> pending
            let pending' = remaining @ issued
            if pending'.Length > 1 then fail "zwei offene Sammelaufrufe"
            if isStart name && not pending.IsEmpty then
                if after <> model then fail "gesperrter Start ändert das Modell"
                if not (List.isEmpty (messagesOf cmd)) then fail "gesperrter Start schickt eine Nachricht"
                if external <> NoOp then fail "gesperrter Start meldet etwas"
            if name = "Picker" && not pending.IsEmpty && after.BatchCategoryPickerOpen then
                fail "der Picker öffnet bei offenem Aufruf"
            if answered.IsSome && pending'.IsEmpty then
                for startName, start in batchStarts do
                    let _, startCmd, _ = update start (armed after)
                    if (messagesOf startCmd |> List.choose batchCallOf).Length <> 1 then
                        fail $"nach der Antwort startet {startName} nicht"
                // Nach dem Neuladen (Rückgängig Geprüft) ist keine Zeile geladen: ohne sichtbare
                // Auswahl schließt der Picker ohnehin, das ist keine Sperre.
                if not after.BatchCategoryPickerOpen && hasSelection (armed after) then
                    let opened, _, _ = update ToggleBatchCategoryPicker (armed after)
                    if not opened.BatchCategoryPickerOpen then fail "nach der Antwort öffnet der Picker nicht"
            // Nach einem Verstoß weiterzugehen zählte nur Folgefehler.
            if left > 1 && pending'.Length <= 1 then go (left - 1) path' after pending'
    go depth [] (batchBase ()) []
    steps, List.ofSeq violations

[<Tests>]
let batchRunTests =
    testList "Sammellauf der Auswahl: höchstens einer (ledger-sjbvc)" [
        testCase "alle Folgen aus Sammelstarts, Rückgängig, Picker und Antworten bis Tiefe 5" <| fun () ->
            let steps, violations = walkBatches 5
            Expect.isGreaterThan steps 1000 "die Aufzählung erreicht genug Zustände"
            Expect.isEmpty violations (String.concat "\n" violations)
    ]

[<Tests>]
let selectionBarRunTests =
    testList "Auswahl-Leiste: Sperre und Ladezustand (ledger-sjbvc)" [
        testCase "ohne Lauf ist nichts gesperrt, je Lauf lädt die Schaltfläche seiner Aktion und alles sperrt" <| fun () ->
            let shown (running: BatchRun option) =
                let run = Components.Ledger.View.selectionBarRun running
                run.Locked, run.ReviewBusy, run.CategoryBusy, run.LenaBusy
            Expect.equal (shown None) (false, false, false, false) "frei"
            Expect.equal (shown (Some ReviewMarkBatchRun)) (true, true, false, false) "Geprüft"
            Expect.equal (shown (Some CategoryBatchRun)) (true, false, true, false) "Kategorie"
            Expect.equal (shown (Some LenaBatchRun)) (true, false, false, true) "Lena"
    ]
