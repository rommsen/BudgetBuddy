module LedgerReviewMarkBatchClientTests

// "Geprüft" für die ganze Auswahl (ledger-nhwrt), Client-Seite: der reine Reducer, die
// Toast-Sätze und die Leiste. Kein Api-Aufruf: `Start...` setzt nur den Zustand und stößt
// `Run...` an, der Proxy steckt dahinter (ADR 0020). Die Cmd-Nachrichten lesen wir aus,
// indem wir die Cmd mit einem aufzeichnenden `dispatch` ausführen.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open LedgerSelectionTests

let private update = Components.Ledger.State.update

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

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
    { Changed = changes; UnchangedCount = unchanged; FailedCount = failed }

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
            Expect.isTrue started.ReviewMarkBatchRunning "Aktion gesperrt"
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

        testCase "ohne Auswahl und während ein Aufruf läuft löst der Klick nichts aus" <| fun () ->
            let _, noSelection, _ = update StartReviewMarkBatch (modelWith false 3 3)
            Expect.isEmpty noSelection "ohne Auswahl kein Aufruf"
            let running = { modelWith false 3 3 with Selection = Picked (Set.ofList (rowIds [ 1 ])); ReviewMarkBatchRunning = true }
            let _, again, _ = update StartReviewMarkBatch running
            Expect.isEmpty again "ein zweiter Klick schickt keinen zweiten Aufruf"
    ]

[<Tests>]
let doneTests =
    testList "Geprüft für die Auswahl: Ergebnis (ledger-nhwrt)" [

        testCase "Erfolg mit aktivem Chip: Auswahl aufgehoben, Zeilen verlassen die Liste, Zähler sinkt" <| fun () ->
            let model =
                { modelWith true 5 5 with
                    Selection = Picked (Set.ofList (rowIds [ 1; 2; 3 ]))
                    SelectionMode = true
                    ReviewMarkBatchRunning = true }
            let outcome = outcomeOf [ change 1 ReviewMarkSent; change 2 ReviewMarkSent; change 3 ReviewMarkSent ] 0 0
            let model', cmd, _ = update (ReviewMarkBatchDone (Ok outcome)) model
            Expect.equal model'.Selection NoSelection "Auswahl aufgehoben"
            Expect.isFalse model'.SelectionMode "Auswahl-Modus beendet"
            Expect.isFalse model'.ReviewMarkBatchRunning "Aktion wieder frei"
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
                    ReviewMarkBatchRunning = true }
            let model', _, external = update (ReviewMarkBatchDone (Error "Netz weg")) model
            Expect.isFalse model'.ReviewMarkBatchRunning "wieder frei"
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
            Expect.equal (messagesOf cmd) [ LoadEntries ] "LoadEntries"
            Expect.equal external NoOp "kein Toast bei Erfolg"

        testCase "ein Fehlschlag beim Zurückschreiben zeigt den Hinweis, die Liste lädt trotzdem neu" <| fun () ->
            let outcome = outcomeOf [ change 1 (ReviewMarkSendFailed "Limit") ] 0 0
            let _, cmd, external = update (ReviewMarkBatchRestored (Ok outcome)) (modelWith true 2 2)
            Expect.equal (messagesOf cmd) [ LoadEntries ] "LoadEntries"
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

[<Tests>]
let barTests =
    testList "Geprüft für die Auswahl: Leiste (ledger-nhwrt)" [

        testCase "Geprüft ist die primäre Aktion auf Handy und PC (Quelltext der Bausteine)" <| fun () ->
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "MultiSelect.fs" ]
            let slice (header: string) =
                let start = ds.IndexOf header
                Expect.isGreaterThan start -1 header
                let rest = ds.Substring start
                let next = rest.IndexOf("\nlet ", 5)
                if next < 0 then rest else rest.Substring(0, next)
            let button = slice "let private reviewedButton"
            Expect.stringContains button "Variant = Button.Primary" "Orange, die einzige primäre Aktion"
            Expect.stringContains button "IsLoading = busy" "Spinner und Sperre während des Aufrufs"
            Expect.stringContains (slice "let barMobile") "reviewedButton reviewedLabel" "Handy-Leiste, Reihe 2"
            Expect.stringContains (slice "let barDesktop") "reviewedButton reviewedLabel" "PC-Leiste rechts"
            // Reihe 2 am Handy: Alle, Geprüft, Auswahl aufheben in dieser Folge.
            let mobile = slice "let barMobile"
            let all = mobile.IndexOf "ChipBar.chip"
            let reviewed = mobile.IndexOf "reviewedButton reviewedLabel"
            let clear = mobile.IndexOf "clearButton clearLabel"
            Expect.isTrue (all < reviewed && reviewed < clear) "Alle, Geprüft, Auswahl aufheben"

        testCase "Die Seite reicht Beschriftung, Sperre und Klick an beide Leisten (Quelltext)" <| fun () ->
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            let from = view.IndexOf "let selectionBars"
            let barView = view.Substring(from, view.IndexOf("let private selectionBarSpacers") - from)
            let count (needle: string) =
                barView.Split([| needle |], StringSplitOptions.None).Length - 1
            Expect.equal (count "selectionReviewedLabel") 2 "in Handy- und PC-Leiste"
            // Einmal in `locked` (beide Sammelaktionen, ledger-w29y6), je einmal als Spinner in den Leisten.
            Expect.equal (count "model.ReviewMarkBatchRunning") 3 "Sperre in beiden"
            Expect.equal (count "\n                locked\n") 2 "die gemeinsame Sperre erreicht beide Leisten"
            Expect.equal (count "dispatch StartReviewMarkBatch") 2 "Klick in beiden"
            Expect.equal selectionReviewedLabel "Geprüft" "Beschriftung"
    ]
