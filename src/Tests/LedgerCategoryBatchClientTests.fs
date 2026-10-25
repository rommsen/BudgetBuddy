module LedgerCategoryBatchClientTests

// "Kategorie setzen" für die ganze Auswahl (ledger-w29y6), Client-Seite: der reine Reducer,
// die Toast-Sätze, die Rücknahme in Gruppen und die Leiste. Kein Api-Aufruf: `Start...` setzt
// nur den Zustand und stößt `Run...` an, der Proxy steckt dahinter (ADR 0020). Die Cmd-Nachrichten
// lesen wir aus, indem wir die Cmd mit einem aufzeichnenden `dispatch` ausführen; nur reine
// Folgebefehle (`Cmd.ofMsg`) laufen in den Tests.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

let private update = Components.Ledger.State.update

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private catGuid (n: int) = Guid.Parse $"0195c29a-0000-7000-8000-{n:D12}"
let private catId (n: int) = YnabCategoryId (catGuid n)

let private category (n: int) (name: string) : YnabCategory =
    { Id = catId n; Name = name; GroupName = "Alltag w29y6"; Available = Some (eur 100m); Hidden = false }

let private categories = [ category 1 "Lebensmittel w29y6"; category 2 "Drogerie w29y6"; category 3 "Freizeit w29y6" ]

let private choice (n: int) : CategoryChoice =
    { CategoryId = catId n; CategoryName = (categories |> List.find (fun c -> c.Id = catId n)).Name }

let private accountId = Guid.Parse "0195e29a-0000-7000-8000-000000000001"

/// Zeile n in Kategorie `cat` (0: ohne Kategorie), Betrag -10.
let private entryOf (n: int) (cat: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f29a-0000-7000-8000-{n:D12}"
      AccountId = accountId
      Date = DateTime(2026, 10, 1)
      Amount = eur -10m
      Payee = Some $"Payee w29y6 {n}"
      Memo = None
      CategoryName = if cat = 0 then None else Some (choice cat).CategoryName
      CategoryId = if cat = 0 then None else Some (catGuid cat)
      CategoryGroupName = if cat = 0 then None else Some "Alltag w29y6"
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
      MemoWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None }

let private idOf (n: int) = (entryOf n 0).Id
let private idsOf (ns: int list) = ns |> List.map idOf

/// Ein Modell mit fünf geladenen Zeilen in Kategorie 1, Treffer gleich geladen, Auswahl Zeilen 1 bis 3.
let private selectedModel () : Model =
    let model, _ = Components.Ledger.State.init ()
    let rows = [ for n in 1..5 -> entryOf n 1 ]
    { model with
        SelectedFilter = Some (OneAccount accountId)
        Entries = Success rows
        NextPage = 1
        TotalCount = 5
        TotalAmount = eur -50m
        HasMoreEntries = false
        Categories = Success categories
        CategoriesLoadedAt = Some DateTime.Now
        Selection = Picked (Set.ofList (idsOf [ 1; 2; 3 ])) }

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

let private change (n: int) (previous: CategoryChoice option) (writeBack: CategoryWriteBack) : CategoryBatchChange =
    { EntryId = idOf n; Previous = previous; WriteBack = writeBack }

let private outcomeOf (changes: CategoryBatchChange list) (unchanged: int) (skipped: CategorySkipReason list) : CategoryBatchOutcome =
    { Changed = changes; UnchangedCount = unchanged; Skipped = skipped }

let private threeChanged =
    outcomeOf [ for n in 1..3 -> change n (Some (choice 1)) CategorySent ] 0 []

[<Tests>]
let pickerTests =
    testList "Kategorie setzen für die Auswahl: Picker (ledger-w29y6)" [

        testCase "der Picker öffnet nur mit nicht leerer Auswahl, ein zweiter Klick schließt ihn" <| fun () ->
            let empty = { selectedModel () with Selection = NoSelection }
            let stays, _, _ = update ToggleBatchCategoryPicker empty
            Expect.isFalse stays.BatchCategoryPickerOpen "ohne Auswahl bleibt er zu"
            let opened, sent, _ = run ToggleBatchCategoryPicker (selectedModel ())
            Expect.isTrue opened.BatchCategoryPickerOpen "mit Auswahl öffnet er"
            Expect.contains sent EnsureCategoriesFresh "Öffnen frischt die Kategorien auf"
            let closed, _, _ = update ToggleBatchCategoryPicker opened
            Expect.isFalse closed.BatchCategoryPickerOpen "zweiter Klick schließt"

        testCase "während ein Sammelaufruf läuft, öffnet der Picker nicht (beide Aktionen gesperrt)" <| fun () ->
            for running in [ { selectedModel () with CategoryBatchRunning = true }
                             { selectedModel () with ReviewMarkBatchRunning = true } ] do
                let m, _, _ = update ToggleBatchCategoryPicker running
                Expect.isFalse m.BatchCategoryPickerOpen "gesperrt"

        testCase "Esc bei offenem Picker schließt nur den Picker, die Auswahl bleibt" <| fun () ->
            let opened = { selectedModel () with BatchCategoryPickerOpen = true }
            let m, _, _ = update EscapePressed opened
            Expect.isFalse m.BatchCategoryPickerOpen "Picker zu"
            Expect.equal m.Selection opened.Selection "Auswahl bleibt"
            let m2, _, _ = update EscapePressed m
            Expect.equal m2.Selection NoSelection "das zweite Esc hebt die Auswahl auf"

        testCase "bei offenem Picker ruhen die Listentasten" <| fun () ->
            let opened = { selectedModel () with BatchCategoryPickerOpen = true; FocusedRow = Some (idOf 1) }
            let m, _, _ = update (MoveFocus 1) opened
            Expect.equal m.FocusedRow (Some (idOf 1)) "Fokus bewegt sich nicht"

        testCase "endet die Auswahl, endet der Picker mit ihr" <| fun () ->
            let opened = { selectedModel () with BatchCategoryPickerOpen = true; SelectionMode = true }
            let m, _, _ = update ClearSelection opened
            Expect.isFalse m.BatchCategoryPickerOpen "Picker zu"
    ]

[<Tests>]
let startTests =
    testList "Kategorie setzen für die Auswahl: Start (ledger-w29y6)" [

        testCase "die Wahl ruft den Befehl mit der Auswahl auf, sperrt die Aktionen und schließt den Picker" <| fun () ->
            let model = { selectedModel () with BatchCategoryPickerOpen = true }
            let started, sent, _ = run (StartCategoryBatch (Some (catId 2))) model
            Expect.isTrue started.CategoryBatchRunning "gesperrt"
            Expect.isFalse started.BatchCategoryPickerOpen "Picker zu"
            match sent with
            | [ RunCategoryBatch (BatchPicked ids, target) ] ->
                Expect.equal (List.sort ids) (List.sort (idsOf [ 1; 2; 3 ])) "genau die gewählten Ids"
                Expect.equal target (Some (catId 2)) "Ziel"
            | other -> failtest $"erwartet RunCategoryBatch (BatchPicked ...), war {other}"

        testCase "ohne Auswahl und während ein Aufruf läuft löst die Wahl nichts aus" <| fun () ->
            let _, none, _ = run (StartCategoryBatch None) { selectedModel () with Selection = NoSelection }
            Expect.isEmpty none "ohne Auswahl kein Aufruf"
            let _, again, _ = run (StartCategoryBatch None) { selectedModel () with CategoryBatchRunning = true }
            Expect.isEmpty again "ein zweiter Aufruf läuft nicht"
            let _, review, _ = run (StartCategoryBatch None) { selectedModel () with ReviewMarkBatchRunning = true }
            Expect.isEmpty review "auch nicht während Geprüft läuft"

        testCase "AllExcept: der Aufruf trägt die Treffer-Anfrage und die Ausnahmen" <| fun () ->
            let model = { selectedModel () with Selection = AllExcept (Set.ofList (idsOf [ 2 ])) }
            let _, sent, _ = run (StartCategoryBatch (Some (catId 3))) model
            match sent with
            | [ RunCategoryBatch (BatchAllExcept (query, excluded), _) ] ->
                Expect.equal excluded (idsOf [ 2 ]) "die abgewählte Zeile"
                Expect.equal query.AccountId (Some accountId) "Konto des Treffers"
                Expect.equal (query.Page, query.LocallyRemoved) (0, 0) "Seite und Versatz zählen nicht"
            | other -> failtest $"erwartet RunCategoryBatch (BatchAllExcept ...), war {other}"
    ]

[<Tests>]
let doneTests =
    testList "Kategorie setzen für die Auswahl: Ergebnis (ledger-w29y6)" [

        testCase "Erfolg: Auswahl samt Modus beendet, Zeilen tragen die neue Kategorie, LoadCategories folgt" <| fun () ->
            let model = { selectedModel () with SelectionMode = true; CategoryBatchRunning = true }
            let m, sent, external = run (CategoryBatchDone (Some (catId 2), Ok threeChanged)) model
            Expect.equal m.Selection NoSelection "Auswahl beendet"
            Expect.isFalse m.SelectionMode "Auswahl-Modus beendet"
            Expect.isFalse m.CategoryBatchRunning "Aktionen frei"
            for n in [ 1; 2; 3 ] do
                Expect.equal (rowOf n m).CategoryId (Some (catGuid 2)) $"Zeile {n} trägt die neue Kategorie"
                Expect.equal (rowOf n m).CategoryName (Some (choice 2).CategoryName) $"Zeile {n} trägt den Namen"
            Expect.equal (rowOf 4 m).CategoryId (Some (catGuid 1)) "nicht gewählte Zeile unverändert"
            Expect.contains sent LoadCategories "Kategorien laden unbedingt neu"
            Expect.isFalse m.OrderStale "bei Datumssortierung bleibt die Reihenfolge"
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "3 Buchungen nach Drogerie w29y6 verschoben" "Toast nennt Zahl und Kategorie"
                Expect.equal action.Kind (UndoCategoryBatch threeChanged.Changed) "Rücknahme für alle"
            | other -> failtest $"erwartet Erfolgs-Toast mit Aktion, war {other}"

        testCase "ohne geänderte Buchung: Toast ohne Aktion, kein LoadCategories" <| fun () ->
            let outcome = outcomeOf [] 2 [ SkipSplit ]
            let _, sent, external = run (CategoryBatchDone (Some (catId 2), Ok outcome)) { selectedModel () with CategoryBatchRunning = true }
            Expect.isFalse (List.contains LoadCategories sent) "nichts geändert"
            match external with
            | ShowToast (text, ToastInfo) -> Expect.stringContains text "Keine Buchung geändert" "Satz"
            | other -> failtest $"erwartet Info-Toast ohne Aktion, war {other}"

        testCase "Fehlschlag nach YNAB: Zeile trägt den Grund, Toast warnt" <| fun () ->
            let outcome =
                outcomeOf [ change 1 (Some (choice 1)) (CategorySendFailed "Timeout"); change 2 (Some (choice 1)) CategorySent ] 0 []
            let m, _, external = run (CategoryBatchDone (Some (catId 2), Ok outcome)) (selectedModel ())
            Expect.equal (rowOf 1 m).CategorizationWriteBackFailure (Some "Timeout") "Grund an der Zeile"
            Expect.isNone (rowOf 2 m).CategorizationWriteBackFailure "gesendet: kein Fehlschlag"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) -> Expect.stringContains text "Kategorie nicht in YNAB: Timeout" "Grund im Toast"
            | other -> failtest $"erwartet Warn-Toast mit Aktion, war {other}"

        testCase "Fehler des Befehls: Aktionen frei, Fehler-Toast, Auswahl bleibt" <| fun () ->
            let m, _, external = run (CategoryBatchDone (None, Error "kaputt")) { selectedModel () with CategoryBatchRunning = true }
            Expect.isFalse m.CategoryBatchRunning "frei"
            Expect.equal m.Selection (selectedModel ()).Selection "Auswahl bleibt"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "kaputt" "Meldung"
            | other -> failtest $"erwartet Fehler-Toast, war {other}"

        testCase "Sortierung nach Kategorie: OrderStale und RunRefreshTotals wie im Einzelweg" <| fun () ->
            let model = { selectedModel () with Sort = { Field = SortByCategory; Direction = SortAsc } }
            let m, sent, _ = run (CategoryBatchDone (Some (catId 2), Ok threeChanged)) model
            Expect.isTrue m.OrderStale "Reihenfolge veraltet"
            Expect.contains sent RunRefreshTotals "Treffer und Summe neu"

        testCase "Filter: Zeilen, die herausfallen, bleiben ausgegraut stehen, Treffer und Summe gehen sofort mit" <| fun () ->
            let model =
                { selectedModel () with Filters = { noFilters with CategoryIds = Set.singleton (catGuid 1) } }
            let m, sent, _ = run (CategoryBatchDone (Some (catId 2), Ok threeChanged)) model
            Expect.equal (entriesOf m |> List.length) 5 "alle Zeilen bleiben in Entries"
            Expect.equal m.FilteredOut (Set.ofList (idsOf [ 1; 2; 3 ])) "die drei sind ausgegraut"
            Expect.equal m.LocallyRemoved 3 "zählt für den Versatz"
            Expect.equal m.TotalCount 2 "Treffer sinkt sofort"
            Expect.equal m.TotalAmount.Amount -20m "Summe sinkt sofort"
            Expect.isFalse m.OrderStale "Datumssortierung: keine veraltete Reihenfolge"
            Expect.isFalse (List.contains RunRefreshTotals sent) "alle geänderten Zeilen waren geladen"

        testCase "AllExcept mit Kategorie-Filter: nie geladene geänderte Buchungen holen Treffer und Summe neu" <| fun () ->
            let model =
                { selectedModel () with
                    Filters = { noFilters with CategoryIds = Set.singleton (catGuid 1) }
                    TotalCount = 40
                    Selection = AllExcept Set.empty }
            // 40 geänderte Buchungen, nur eine davon geladen.
            let outcome = outcomeOf [ for n in 1..40 -> { EntryId = (if n = 1 then idOf 1 else Guid.NewGuid()); Previous = Some (choice 1); WriteBack = CategorySent } ] 0 []
            let m, sent, _ = run (CategoryBatchDone (Some (catId 2), Ok outcome)) model
            Expect.contains sent RunRefreshTotals "Treffer und Summe neu holen"
            Expect.isTrue m.OrderStale "die Liste ist veraltet, damit die neuen Zahlen gelten"

        testCase "AllExcept ohne Kategorie-Filter braucht keine Neuberechnung" <| fun () ->
            let model = { selectedModel () with Selection = AllExcept Set.empty; TotalCount = 40 }
            let outcome = outcomeOf [ for n in 1..40 -> { EntryId = (if n = 1 then idOf 1 else Guid.NewGuid()); Previous = Some (choice 1); WriteBack = CategorySent } ] 0 []
            let m, sent, _ = run (CategoryBatchDone (Some (catId 2), Ok outcome)) model
            Expect.isFalse (List.contains RunRefreshTotals sent) "Zahlen unverändert"
            Expect.isFalse m.OrderStale "nichts veraltet"
    ]

[<Tests>]
let undoTests =
    testList "Kategorie setzen für die Auswahl: Rücknahme (ledger-w29y6)" [

        testCase "Rückgängig ruft den Sammel-Befehl je Gruppe gleicher vorheriger Kategorie auf" <| fun () ->
            let changes =
                [ change 1 (Some (choice 1)) CategorySent
                  change 2 None CategorySent
                  change 3 (Some (choice 1)) CategorySent ]
            let m, sent, _ = run (StartUndoCategoryBatch changes) (selectedModel ())
            Expect.isTrue m.CategoryBatchRunning "gesperrt"
            match sent with
            | [ RunUndoCategoryBatch groups ] ->
                Expect.equal groups [ Some (choice 1), idsOf [ 1; 3 ]; None, idsOf [ 2 ] ] "zwei Gruppen, ein PATCH je Gruppe"
            | other -> failtest $"erwartet RunUndoCategoryBatch, war {other}"

        testCase "nach der Rücknahme tragen die Zeilen die alte Kategorie, ausgegraute werden wieder Treffer, der Toast hat keine Aktion" <| fun () ->
            let filtered = { noFilters with CategoryIds = Set.singleton (catGuid 1) }
            let afterDo, _, _ =
                run (CategoryBatchDone (Some (catId 2), Ok threeChanged)) { selectedModel () with Filters = filtered }
            Expect.equal afterDo.TotalCount 2 "Ausgangslage: drei ausgegraut"
            let undoOutcome =
                outcomeOf [ for n in 1..3 -> change n (Some (choice 2)) CategorySent ] 0 []
            let restored, sent, external =
                run (CategoryBatchRestored [ Some (choice 1), Ok undoOutcome ]) { afterDo with CategoryBatchRunning = true }
            Expect.isFalse restored.CategoryBatchRunning "frei"
            for n in [ 1; 2; 3 ] do
                Expect.equal (rowOf n restored).CategoryId (Some (catGuid 1)) $"Zeile {n} zurück"
            Expect.isEmpty (Set.toList restored.FilteredOut) "keine ausgegraute Zeile mehr"
            Expect.equal (restored.TotalCount, restored.LocallyRemoved) (5, 0) "Treffer und Versatz zurück"
            Expect.contains sent LoadCategories "Kategorien laden neu"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text "Kategorie bei 3 Buchungen zurückgesetzt" "ein Toast, keine Aktion"
            | other -> failtest $"erwartet Toast ohne Aktion, war {other}"

        testCase "scheitert eine Gruppe der Rücknahme, meldet der Toast den Fehler" <| fun () ->
            let ok = outcomeOf [ change 1 (Some (choice 2)) CategorySent ] 0 []
            let _, _, external =
                run (CategoryBatchRestored [ Some (choice 1), Ok ok; None, Error "kaputt" ]) (selectedModel ())
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "kaputt" "Fehler genannt"
            | other -> failtest $"erwartet Fehler-Toast, war {other}"
    ]

[<Tests>]
let textTests =
    testList "Kategorie setzen für die Auswahl: Texte (ledger-w29y6)" [

        testCase "Toast-Satz: Zahl, Kategorie, übersprungene mit Gründen" <| fun () ->
            let outcome = outcomeOf [ for n in 1..2 -> change n None CategorySent ] 1 [ SkipSplit; SkipTransfer; SkipDeletedInYnab ]
            Expect.equal
                (categoryBatchToastText (Some "Drogerie") outcome)
                "2 Buchungen nach Drogerie verschoben, 4 übersprungen (Split, Übertrag, gelöscht, schon dort)"
                "Satz mit Gründen"
            Expect.equal
                (categoryBatchToastText (Some "Drogerie") (outcomeOf [ change 1 None CategorySent ] 0 []))
                "1 Buchung nach Drogerie verschoben"
                "Einzahl, nichts übersprungen"
            Expect.equal
                (categoryBatchToastText None (outcomeOf [ change 1 None CategorySent; change 2 None CategorySent ] 0 []))
                "Kategorie bei 2 Buchungen entfernt"
                "ohne Kategorie"

        testCase "der offene Split-Fehlschlag zählt im Toast als Split" <| fun () ->
            let text = categoryBatchToastText (Some "X") (outcomeOf [ change 1 None CategorySent ] 0 [ SkipSplitNotAdjusted ])
            Expect.stringContains text "1 übersprungen (Split)" "Split"

        testCase "Unterzeile des Sheets: n Buchungen · Summe" <| fun () ->
            let text = selectionPickerSubtitle 12 (eur -340.20m)
            Expect.stringStarts text "12 Buchungen · " "Zahl und Trenner"
            Expect.stringContains text "340,20" "Summe"
            Expect.stringStarts (selectionPickerSubtitle 1 (eur -10m)) "1 Buchung · " "Einzahl"

        testCase "Gruppen der Rücknahme halten die Reihenfolge des ersten Auftretens" <| fun () ->
            let groups =
                categoryBatchUndoGroups [ change 1 None CategorySent; change 2 (Some (choice 1)) CategorySent; change 3 None CategorySent ]
            Expect.equal groups [ None, idsOf [ 1; 3 ]; Some (choice 1), idsOf [ 2 ] ] "gruppiert"
    ]

[<Tests>]
let barSourceTests =
    testList "Kategorie setzen für die Auswahl: Leiste und Bausteine (ledger-w29y6)" [

        testCase "die Leiste reicht die Sammelaktion durch, Geprüft bleibt primär, Kategorie setzen sekundär" <| fun () ->
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "MultiSelect.fs" ]
            Expect.stringContains ds "Variant = Button.Primary" "Geprüft primär"
            Expect.stringContains ds "Variant = Button.Secondary" "Kategorie setzen sekundär"
            let view = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]
            Expect.stringContains view "selectionCategoryLabel" "Beschriftung"
            Expect.stringContains view "let locked = model.ReviewMarkBatchRunning || model.CategoryBatchRunning" "ein Aufruf sperrt beide"

        testCase "Picker der Auswahl öffnet über der Leiste und ohne Haken (Bausteine)" <| fun () ->
            let popover = readRepo [ "src"; "Client"; "DesignSystem"; "FilterPopover.fs" ]
            Expect.stringContains popover "bottom-full right-0 mb-6" "Variante nach oben"
            let picker = readRepo [ "src"; "Client"; "DesignSystem"; "CategoryPicker.fs" ]
            Expect.stringContains picker "MarkCurrent = false" "ohne markierte Zeile"
            Expect.stringContains picker "FilterPopover.viewAbove" "Popover nach oben"

        testCase "die neuen Varianten erscheinen in /styleguide und im Styleguide-Dokument" <| fun () ->
            let gallery = readRepo [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            Expect.stringContains gallery "CategoryPicker.selectionPopover" "Popover nach oben in der Galerie"
            Expect.stringContains gallery "CategoryPicker.selectionSheet" "Sheet ohne Haken in der Galerie"
            let doc = readRepo [ "standards"; "frontend"; "styleguide.md" ]
            Expect.stringContains doc "selectionPopover" "Dokument nennt die Varianten"

        testCase "Rücknahme und Toast sind verdrahtet" <| fun () ->
            let changes = [ change 1 None CategorySent ]
            match State.toastActionMsg (UndoCategoryBatch changes) with
            | State.LedgerMsg (StartUndoCategoryBatch received) ->
                Expect.equal received changes "Toast-Aktion wird zur Nachricht des Kassenbuchs"
            | other -> failtestf "unerwartete Nachricht: %A" other
    ]
