module LedgerBackgroundRefreshTests

// ledger-ct3cx: Nach einem Spiegel-Lauf (auch dem automatischen beim Öffnen oder Zurückholen der App),
// nach einem Abruf und nach „Rückgängig“ auf ein Verwerfen baute das Kassenbuch die Liste neu auf:
// Skeleton, Sprung nach oben, Auswahl und nachgeladene Seiten weg. Jetzt lädt die Liste im Hintergrund
// nach: derselbe Ausschnitt ab Zeile 0, die Antwort ersetzt die Zeilen, die Auswahl wird auf die noch
// vorhandenen Buchungen beschnitten. Alles über das echte `update`; die Abfragen selbst fasst kein Test
// an (der Remoting-Proxy wirft unter .NET), dafür die reinen Funktionen `pageRequest`, `refreshRequest`
// und `entriesRefreshAnswer`, die dieselben Zweige benutzen.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountId = Guid.Parse "0195ac70-0000-7000-8000-000000000001"
let private otherAccountId = Guid.Parse "0195ac70-0000-7000-8000-000000000002"

let private accountsView : LedgerAccountsView = {
    Accounts =
        [ { Id = accountId; Name = "Giro"; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false }
          { Id = otherAccountId; Name = "Tagesgeld"; Kind = LedgerBankAccount; YnabAccountId = None; Closed = false } ]
    DefaultAccountId = Some otherAccountId
}

/// Zeile n mit dem Betrag `amount`; ungerade n sind zu prüfen.
let private entryWith (n: int) (amount: decimal) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n amount (n % 2 = 1) false with AccountId = accountId }

let private entry (n: int) : LedgerEntryView = entryWith n (-10m * decimal n)

let private idOf (n: int) = (entry n).Id

let private sumOf (rows: LedgerEntryView list) = rows |> List.sumBy (fun e -> e.Amount.Amount)

let private pageOf (rows: LedgerEntryView list) (total: int) (totalAmount: decimal) : LedgerEntryPage = {
    Entries = rows
    TotalCount = total
    TotalAmount = eur totalAmount
    DeletedCount = 0
    NeedsReviewCount = 7
    AskLenaCount = 3
}

/// Ein Kassenbuch auf dem Giro mit `rows` geladen, Treffer `total`, `nextPage` Seiten geladen.
let private loadedModel (rows: LedgerEntryView list) (total: int) (nextPage: int) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Accounts = Success accountsView
        SelectedFilter = Some (OneAccount accountId)
        Entries = Success rows
        NextPage = nextPage
        TotalCount = total
        TotalAmount = eur (sumOf rows)
        HasMoreEntries = List.length rows < total
        NeedsReviewCount = 1
        AskLenaCount = 0 }

let private update msg model = Components.Ledger.State.update msg model

/// Sammelt die Nachrichten eines Cmd ein (nur `Cmd.ofMsg`/`Cmd.batch`, kein Proxy).
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private summary : MirrorRunSummary = { NewEntries = 1; ChangedEntries = 1; DeletedEntries = 0; ServerKnowledge = 1L }

/// Startet das Nachladen und gibt das Modell samt der Antwort-Funktion der Abfrage zurück, so wie
/// `RunRefreshEntries` sie bindet.
let private startRefresh (model: Model) : Model * (Result<LedgerEntryPage, string> -> Msg) =
    let started, cmd, _ = update RefreshEntries model
    Expect.equal (dispatched cmd) [ RunRefreshEntries ] "das Nachladen setzt seine Abfrage ab"
    match Components.Ledger.State.refreshRequest started with
    | Some (_, answer) -> started, answer
    | None -> failtest "keine Abfrage zum Nachladen"

let private idsOf (model: Model) =
    model.Entries |> RemoteData.withDefault [] |> List.map (fun e -> e.Id)

[<Tests>]
let refreshTests =
    testList "Kassenbuch: Nachladen im Hintergrund" [

        // Regression ledger-ct3cx: vor dem Fix schickte `MirrorFinished` `LoadAccounts`, dessen Antwort
        // `resetEntries` und `LoadEntries` setzte. Liste auf Skeleton, Auswahl und Seite 2 weg.
        test "Ein Spiegel-Lauf behält Liste, Auswahl, Auswahl-Modus und nachgeladene Seiten und lädt im Hintergrund nach" {
            let rows = [ for n in 1..150 -> entry n ]
            let model =
                { loadedModel rows 150 2 with
                    SelectionMode = true
                    Selection = Picked (Set.ofList [ idOf 3; idOf 120 ]) }
            let afterMirror, mirrorCmd, _ = update (MirrorFinished (Ok summary)) model
            let mirrorMsgs = dispatched mirrorCmd
            Expect.contains mirrorMsgs RefreshAccounts "die Konten laden nach dem Lauf neu"
            Expect.isFalse (List.contains LoadAccounts mirrorMsgs) "nicht über den Weg des Seitenaufrufs"
            let after, cmd, _ = update (AccountsRefreshed (Ok accountsView)) afterMirror
            Expect.equal after.Entries model.Entries "dieselben Zeilen, kein Skeleton"
            Expect.equal after.Selection model.Selection "die Auswahl bleibt"
            Expect.isTrue after.SelectionMode "der Auswahl-Modus bleibt"
            Expect.equal after.NextPage 2 "die nachgeladene Seite bleibt"
            let msgs = dispatched cmd
            Expect.contains msgs RefreshEntries "im Hintergrund nachladen"
            Expect.isFalse (List.contains LoadEntries msgs) "kein Neuaufbau"
            Expect.isFalse (List.contains (RunLoadEntriesPage 0) msgs) "keine Abfrage der Seite 0"
        }

        test "Das Nachladen fragt dieselbe Filterung und Sortierung ab Zeile 0 ab, so viele Zeilen wie geladen sind" {
            let model =
                { loadedModel [ for n in 1..199 -> entry n ] 300 2 with
                    LocallyRemoved = 1
                    OnlyNeedsReview = true
                    Sort = { Field = SortByAmount; Direction = SortAsc } }
            let started, _ = startRefresh model
            match Components.Ledger.State.refreshRequest started with
            | Some (query, _) ->
                let page = Components.Ledger.State.buildPageQuery model (Some accountId) 0
                Expect.equal query { page with PageSize = 2 * pageSize; LocallyRemoved = 0 } "Seite 0, zwei Seiten groß, ohne Abzug"
            | None -> failtest "keine Abfrage"
        }

        test "Die Antwort ersetzt die Zeilen in der Reihenfolge des Servers und beschneidet die Auswahl" {
            // A, B, C, D geladen, gewählt B und C, Fokus auf D. Antwort: E (neu), A (Betrag geändert), C.
            let a, b, c, d, e = entry 1, entry 2, entry 3, entry 4, entry 5
            let changedA = { a with Amount = eur -99m }
            let model =
                { loadedModel [ a; b; c; d ] 4 1 with
                    SelectionMode = true
                    Selection = Picked (Set.ofList [ b.Id; c.Id ])
                    SelectionAnchor = Some b.Id
                    FocusedRow = Some d.Id }
            let started, answer = startRefresh model
            let rows = [ e; changedA; c ]
            let after, _, _ = update (answer (Ok (pageOf rows 3 -200m))) started
            match after.Entries with
            | Success loaded ->
                Expect.equal (loaded |> List.map (fun r -> r.Id)) [ e.Id; a.Id; c.Id ] "E, A, C"
                Expect.equal (loaded |> List.find (fun r -> r.Id = a.Id)).Amount (eur -99m) "A mit neuem Betrag"
            | other -> failtestf "Erwartet Success, war %A" other
            Expect.equal after.Selection (Picked (Set.singleton c.Id)) "gewählt ist nur noch C"
            Expect.isTrue after.SelectionMode "der Auswahl-Modus bleibt"
            Expect.equal after.SelectionAnchor None "der Anker gehörte zu B, das fehlt"
            Expect.equal after.FocusedRow (reconcileFocus rows (Some d.Id)) "der Fokus folgt reconcileFocus"
            Expect.equal after.TotalCount 3 "Trefferzahl aus der Antwort"
            Expect.equal after.TotalAmount (eur -200m) "Summe aus der Antwort"
            Expect.equal after.NeedsReviewCount 7 "„Zu prüfen (n)“ aus der Antwort"
            Expect.equal after.AskLenaCount 3 "„Rückfrage Lena (n)“ aus der Antwort"
            Expect.isFalse (refreshRunning after) "das Nachladen ist fertig"
        }

        test "Ist „alle“ gewählt, fällt eine verschwundene Ausnahme weg und neue Buchungen gehören zur Auswahl" {
            let a, b, c, d, e = entry 1, entry 2, entry 3, entry 4, entry 5
            let model =
                { loadedModel [ a; b; c; d ] 4 1 with
                    SelectionMode = true
                    Selection = AllExcept (Set.singleton b.Id) }
            let started, answer = startRefresh model
            let after, _, _ = update (answer (Ok (pageOf [ e; a; c ] 3 -90m))) started
            Expect.equal after.Selection (AllExcept Set.empty) "die Ausnahme B fällt weg, die Auswahl bleibt „alle“"
            let totals = selectionTotals after
            Expect.equal totals.Count 3 "Zahl aus der neuen Trefferzahl, E gehört dazu"
            Expect.equal totals.Amount (eur -90m) "Summe aus der neuen Summe"
            Expect.isTrue (isSelected after.Selection e.Id) "E ist gewählt"
        }

        // Regression Verifier-FAIL ledger-ct3cx Iteration 1: die Antwort umfasst nur das Fenster der
        // geladenen Seiten. Schob eine neue Buchung oben die letzte Zeile hinaus, fiel deren Ausnahme
        // weg, und „Geprüft für die Auswahl“ traf die abgewählte Buchung.
        test "Unter „alle“ bleibt eine Ausnahme, die das Nachladen aus dem Fenster schiebt" {
            let rows = [ for n in 1..100 -> entry n ]
            let model =
                { loadedModel rows 150 1 with
                    SelectionMode = true
                    Selection = AllExcept (Set.singleton (idOf 100)) }
            let started, answer = startRefresh model
            let pushed = entry 1000 :: [ for n in 1..99 -> entry n ]
            let after, _, _ = update (answer (Ok (pageOf pushed 151 -1m))) started
            Expect.equal after.Selection (AllExcept (Set.singleton (idOf 100))) "die Ausnahme 100 bleibt"
            match Components.Ledger.State.reviewMarkBatchSelection after with
            | Some (BatchAllExcept (_, excluded)) -> Expect.equal excluded [ idOf 100 ] "der Stapel-Auftrag nimmt 100 aus"
            | other -> failtestf "Erwartet BatchAllExcept, war %A" other
        }

        test "Eine gewählte Zeile, die das Nachladen aus dem Fenster schiebt, bleibt gewählt" {
            let rows = [ for n in 1..100 -> entry n ]
            let model =
                { loadedModel rows 150 1 with
                    SelectionMode = true
                    Selection = Picked (Set.ofList [ idOf 3; idOf 100 ]) }
            let started, answer = startRefresh model
            let pushed = entry 1000 :: [ for n in 1..99 -> entry n ]
            let after, _, _ = update (answer (Ok (pageOf pushed 151 -1m))) started
            Expect.equal after.Selection (Picked (Set.ofList [ idOf 3; idOf 100 ])) "3 und 100 bleiben gewählt"
        }

        test "Ein offenes Detail bekommt den neuen Stand seiner Buchung, bleibt offen und lädt seine Historie neu" {
            let a, b = entry 1, entry 2
            let changedA = { a with Memo = Some "neu" }
            let model = { loadedModel [ a; b ] 2 1 with DetailFor = Some a; FocusedRow = Some a.Id }
            let started, answer = startRefresh model
            let after, cmd, _ = update (answer (Ok (pageOf [ changedA; b ] 2 -30m))) started
            Expect.equal after.DetailFor (Some changedA) "der neue Stand im Detail"
            Expect.contains (dispatched cmd) (LoadHistory a.Id) "die Historie lädt neu"
        }

        test "Fehlt die Buchung des offenen Details in der Antwort, bleibt es mit dem bisherigen Stand offen" {
            let a, b = entry 1, entry 2
            let model = { loadedModel [ a; b ] 2 1 with DetailFor = Some a }
            let started, answer = startRefresh model
            let after, _, _ = update (answer (Ok (pageOf [ b ] 1 -20m))) started
            Expect.equal after.DetailFor (Some a) "das Detail bleibt offen, alter Stand"
        }

        test "Das Nachladen beendet ausgegraute Zeilen, veraltete Reihenfolge und lokal entfernte Zeilen" {
            let a, b, c = entry 1, entry 2, entry 3
            let model =
                { loadedModel [ a; b; c ] 250 1 with
                    FilteredOut = Set.singleton b.Id
                    OrderStale = true
                    LocallyRemoved = 1 }
            let started, answer = startRefresh model
            let after, _, _ = update (answer (Ok (pageOf [ for n in 1..100 -> entry n ] 250 -1m))) started
            Expect.isEmpty after.FilteredOut "keine ausgegrauten Zeilen"
            Expect.isFalse after.OrderStale "die Reihenfolge ist die des Servers"
            Expect.equal after.LocallyRemoved 0 "nichts mehr lokal entfernt"
            let more, cmd, _ = update LoadMoreEntries after
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 1 ] "„Weitere laden“ fragt die nächste Seite"
            match Components.Ledger.State.pageRequest more 1 with
            | Some (query, _) ->
                Expect.equal (query.Page, query.PageSize, query.LocallyRemoved) (1, pageSize, 0) "Versatz NextPage * pageSize ohne Abzug"
            | None -> failtest "keine Abfrage"
        }

        test "Scheitert das Nachladen, bleibt die Liste mit Auswahl stehen und eine Warnung nennt den Grund" {
            let a, b = entry 1, entry 2
            let model = { loadedModel [ a; b ] 2 1 with SelectionMode = true; Selection = Picked (Set.singleton a.Id) }
            let started, answer = startRefresh model
            let after, _, external = update (answer (Error "keine Verbindung")) started
            Expect.equal after.Entries model.Entries "die Liste bleibt"
            Expect.equal after.Selection model.Selection "die Auswahl bleibt"
            Expect.isFalse (refreshRunning after) "das Nachladen ist zu Ende"
            Expect.equal external (ShowToast ("Liste konnte nicht aktualisiert werden: keine Verbindung", ToastWarning)) "Warnung"
        }

        test "Eine Antwort nach einem Filterwechsel oder einem späteren Nachladen wird verworfen" {
            let a, b = entry 1, entry 2
            let model = loadedModel [ a; b ] 2 1
            let started, answer = startRefresh model
            let filtered, _, _ = update ToggleOnlyNeedsReview started
            let afterFilter, _, _ = update (answer (Ok (pageOf [ b ] 1 -20m))) filtered
            Expect.equal afterFilter.Entries Loading "die Antwort gehört zum alten Filter"
            let again, _ = startRefresh started
            let afterSecond, _, _ = update (answer (Ok (pageOf [ b ] 1 -20m))) again
            Expect.equal afterSecond.Entries model.Entries "die Antwort des ersten Nachladens zählt nicht mehr"
        }

        // Regression Review a3f7eb1d..982bdf79 (ledger-ct3cx): ein Einzelbefehl zwischen Start und Antwort zählte
        // keine Generation hoch. Die spätere Antwort mit dem Stand vor dem Befehl setzte die Zeile wieder auf
        // „zu prüfen“ und „Zu prüfen (n)“ auf den alten Zähler.
        test "Ändert ein Einzelbefehl eine Zeile, während das Nachladen läuft, verwirft die Liste die ältere Antwort und fragt neu" {
            let a, b = entry 1, entry 2
            let model = loadedModel [ a; b ] 2 1
            let started, answer = startRefresh model
            let marking, markCmd, _ = update (StartSetReviewMark (a.Id, Reviewed)) started
            Expect.equal (dispatched markCmd) [ RunSetReviewMark (a.Id, Reviewed) ] "der Prüfvermerk geht los"
            let outcome : ReviewMarkOutcome = {
                Previous = { IsApproved = false; ClearedState = ReviewUncleared }
                Change = ReviewMarkChanged
                WriteBack = ReviewMarkSent
            }
            let marked, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok outcome)) marking
            Expect.equal marked.NeedsReviewCount 0 "der Zähler folgt dem Prüfvermerk"
            // Die Antwort wurde vor dem Prüfvermerk gelesen: A ist dort noch zu prüfen.
            let after, cmd, _ = update (answer (Ok (pageOf [ a; b ] 2 -30m))) marked
            Expect.equal after.Entries marked.Entries "die Zeilen bleiben beim neueren Stand"
            Expect.equal after.NeedsReviewCount 0 "der Zähler bleibt beim neueren Stand"
            Expect.equal (dispatched cmd) [ RefreshEntries ] "das Nachladen fragt neu"
            let again, againCmd, _ = update RefreshEntries after
            Expect.equal (dispatched againCmd) [ RunRefreshEntries ] "eine neue Abfrage geht los"
            Expect.isTrue (refreshRunning again) "das neue Nachladen läuft"
        }

        test "Ohne Einzelbefehl trägt die Antwort ihren Stand ein, auch wenn ein Befehl noch sendet" {
            // Intakt-Fall: ein laufender Befehl ändert die Zeilen noch nicht, die Antwort zählt; seine
            // spätere Antwort setzt die Zeile relativ zum dann geladenen Stand.
            let a, b = entry 1, entry 2
            let changedB = { b with Memo = Some "neu" }
            let model = loadedModel [ a; b ] 2 1
            let started, answer = startRefresh model
            let marking, _, _ = update (StartSetReviewMark (a.Id, Reviewed)) started
            let after, cmd, _ = update (answer (Ok (pageOf [ a; changedB ] 2 -30m))) marking
            Expect.equal after.Entries (Success [ a; changedB ]) "der neue Stand steht in der Liste"
            Expect.isEmpty (dispatched cmd) "kein zweites Nachladen"
            let outcome : ReviewMarkOutcome = {
                Previous = { IsApproved = false; ClearedState = ReviewUncleared }
                Change = ReviewMarkChanged
                WriteBack = ReviewMarkSent
            }
            let marked, _, _ = update (ReviewMarkSet (a.Id, Reviewed, Ok outcome)) after
            Expect.equal marked.NeedsReviewCount 6 "der Zähler aus der Antwort, um A vermindert"
            Expect.isFalse (marked.Entries |> RemoteData.withDefault [] |> List.find (fun e -> e.Id = a.Id)).NeedsReview "A geprüft"
        }

        test "Ohne geladene Liste baut das Nachladen neu auf" {
            let model = { loadedModel [] 0 0 with Entries = NotAsked }
            let after, cmd, _ = update RefreshEntries model
            Expect.equal after.Entries Loading "Neuaufbau"
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "genau eine Abfrage der Seite 0"
        }

        test "Läuft „Weitere laden“, wartet das Nachladen auf dessen Antwort" {
            let rows = [ for n in 1..100 -> entry n ]
            let model = { loadedModel rows 250 1 with LoadingMore = true }
            let waiting, cmd, _ = update RefreshEntries model
            Expect.isEmpty (dispatched cmd) "noch keine Abfrage"
            let page = pageOf [ for n in 101..200 -> entry n ] 250 -1m
            let answered, cmd, _ = update ((Components.Ledger.State.entriesPageAnswer waiting 1) (Ok page)) waiting
            Expect.equal answered.NextPage 2 "die Seite hängt an"
            Expect.equal (dispatched cmd) [ RefreshEntries ] "danach lädt der ganze Ausschnitt nach"
        }

        test "Während des Nachladens ruht „Weitere laden“" {
            let model = loadedModel [ for n in 1..100 -> entry n ] 250 1
            let started, _ = startRefresh model
            let after, cmd, _ = update LoadMoreEntries started
            Expect.isEmpty (dispatched cmd) "keine weitere Seite aus dem alten Ausschnitt"
            Expect.isFalse after.LoadingMore "nichts läuft"
        }

        // Regression ledger-k4n2s Punkt 63: „Rückgängig“ auf ein Verwerfen außerhalb des Durchgehens lud
        // die Liste nicht nach, die Buchung blieb unsichtbar.
        test "Rückgängig auf ein Verwerfen außerhalb des Durchgehens lädt die Liste im Hintergrund nach" {
            let a = entry 1
            let model = { loadedModel [ a ] 1 1 with RestoringDuplicates = Set.singleton (idOf 9) }
            let after, cmd, _ = update (DiscardedDuplicateRestored (idOf 9, Ok ())) model
            Expect.contains (dispatched cmd) RefreshEntries "die Liste lädt im Hintergrund nach"
            Expect.isFalse (after.RestoringDuplicates.Contains (idOf 9)) "die Sperre endet"
        }
    ]

// ============================================
// Konten-Antwort nach dem Spiegel-Lauf: wann sie doch neu aufbaut
// ============================================

[<Tests>]
let accountsTests =
    testList "Kassenbuch: Konten nach dem Spiegel-Lauf" [

        test "Ohne geladene Liste baut die Konten-Antwort die Liste neu auf" {
            // Erster Spiegel-Lauf auf leerem Kassenbuch (ledger-kt0wq): die Konten entstehen erst im Lauf.
            let model, _ = Components.Ledger.State.init ()
            let after, cmd, _ = update (AccountsRefreshed (Ok accountsView)) model
            Expect.equal after.SelectedFilter (Some (OneAccount otherAccountId)) "die Vorauswahl gilt"
            Expect.equal after.Entries Loading "Neuaufbau"
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "Seite 0"
        }

        test "Nach PresetFilter steht die Liste auf Loading, die Konten-Antwort baut neu auf" {
            let model = loadedModel [ entry 1 ] 1 1
            let preset, _, _ = update (PresetFilter AllAccounts) model
            let after, cmd, _ = update (AccountsRefreshed (Ok accountsView)) preset
            Expect.equal after.SelectedFilter (Some AllAccounts) "der Filter bleibt"
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "Seite 0"
        }

        test "Nach PresetQuickFilter baut die Konten-Antwort neu auf" {
            let preset = Components.Ledger.State.presetQuickFilter PresetNeedsReview (loadedModel [ entry 1 ] 1 1)
            let _, cmd, _ = update (AccountsRefreshed (Ok accountsView)) preset
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "Seite 0"
        }

        test "Verschwindet das gewählte Konto, gilt die Vorauswahl und die Liste baut neu auf" {
            let model = { loadedModel [ entry 1 ] 1 1 with Selection = Picked (Set.singleton (idOf 1)) }
            let gone = { accountsView with Accounts = accountsView.Accounts |> List.filter (fun a -> a.Id <> accountId) }
            let after, cmd, _ = update (AccountsRefreshed (Ok gone)) model
            Expect.equal after.SelectedFilter (Some (OneAccount otherAccountId)) "die Vorauswahl"
            Expect.equal after.Entries Loading "Neuaufbau"
            Expect.equal after.Selection NoSelection "die Auswahl endet mit der Liste"
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "Seite 0"
        }

        test "Ohne Konto bleibt die Liste leer" {
            let model, _ = Components.Ledger.State.init ()
            let after, cmd, _ = update (AccountsRefreshed (Ok { Accounts = []; DefaultAccountId = None })) model
            Expect.equal after.Entries NotAsked "nichts zu laden"
            Expect.isEmpty (dispatched cmd) "keine Abfrage"
        }

        test "Der Seitenaufruf baut wie bisher neu auf" {
            // `pageLoadCmd Ledger` schickt `LoadAccounts`; dessen Antwort beginnt oben ohne Auswahl.
            let model = { loadedModel [ entry 1 ] 1 1 with Selection = Picked (Set.singleton (idOf 1)) }
            let after, cmd, _ = update (AccountsLoaded (Ok accountsView)) model
            Expect.equal after.Entries Loading "Neuaufbau"
            Expect.equal after.Selection NoSelection "ohne Auswahl"
            Expect.equal (dispatched cmd) [ RunLoadEntriesPage 0 ] "Seite 0"
        }

        test "Die Seitenleiste zieht die Konten nach dem Spiegel-Lauf mit" {
            let overview = State.syncNavAccounts (AccountsRefreshed (Ok accountsView)) Components.Overview.Types.init
            Expect.equal overview.Accounts (Success accountsView) "Konten"
        }
    ]

// ============================================
// Reihenfolgen (Doktrin Punkt 20, Vorbild `moveWays` in MoveMoneyClientTests.fs)
// ============================================

/// Die Schritte, deren Reihenfolge variiert.
type private Step =
    | StartRefresh
    | ChangeFilter
    | LoadMore
    | AnswerLoadMore
    | AnswerRefresh
    | SecondMirror
    /// Ein Einzelbefehl samt Antwort: „Geprüft“ für Buchung 1.
    | MarkRow

/// Eine laufende Abfrage: die Anfrage, ihre Antwort-Funktion, wie der `Run*`-Zweig sie bindet, und der
/// Stand des Servers beim Start (die Antwort kennt keinen späteren Befehl).
type private Pending =
    | PagePending of LedgerEntryPageQuery * (Result<LedgerEntryPage, string> -> Msg) * LedgerEntryView list
    | RefreshPending of LedgerEntryPageQuery * (Result<LedgerEntryPage, string> -> Msg) * LedgerEntryView list

/// Der Server nach dem Spiegel-Lauf: eine neue Buchung oben, Buchung 5 gelöscht, alle Beträge geändert.
let private serverRows : LedgerEntryView list =
    entryWith 1000 -1m :: [ for n in 1..250 do if n <> 5 then yield entryWith n -2m ]

/// Wie `serverRows`, aber mit drei neuen Buchungen oben: das Fenster des Nachladens (zwei Seiten) endet
/// vor Buchung 199, der letzten geladenen Zeile (Regression Verifier-FAIL ledger-ct3cx Iteration 1).
let private grownServerRows : LedgerEntryView list =
    [ for n in 1000..1002 -> entryWith n -1m ] @ [ for n in 1..250 do if n <> 5 then yield entryWith n -2m ]

let private serve (server: LedgerEntryView list) (query: LedgerEntryPageQuery) : LedgerEntryPage =
    let matching = server |> List.filter (fun e -> not query.OnlyNeedsReview || e.NeedsReview)
    let offset = max 0 (query.Page * query.PageSize - query.LocallyRemoved)
    let rows = matching |> List.skip (min offset (List.length matching)) |> List.truncate query.PageSize
    { pageOf rows (List.length matching) (sumOf matching) with NeedsReviewCount = 0; AskLenaCount = 0 }

type private World = { Model: Model; Pending: Pending list; Server: LedgerEntryView list }

/// Schickt eine Nachricht durch das echte `update` und folgt den reinen Folgenachrichten. Die
/// Abfragen (`Run*`) laufen nicht: sie werden über die reinen Funktionen ihres Zweigs als laufend
/// vermerkt. Die Konten nach dem Lauf antworten sofort mit denselben Konten.
let rec private send (msg: Msg) (world: World) : World =
    match msg with
    | RunLoadEntriesPage page ->
        match Components.Ledger.State.pageRequest world.Model page with
        | Some (query, answer) -> { world with Pending = world.Pending @ [ PagePending (query, answer, world.Server) ] }
        | None -> world
    | RunRefreshEntries ->
        match Components.Ledger.State.refreshRequest world.Model with
        | Some (query, answer) -> { world with Pending = world.Pending @ [ RefreshPending (query, answer, world.Server) ] }
        | None -> world
    | RunSetReviewMark _ -> world
    | RefreshAccounts -> send (AccountsRefreshed (Ok accountsView)) world
    | LoadBalances | LoadMirrorStatus | LoadLenaFlagSettings -> world
    | _ ->
        let model', cmd, _ = update msg world.Model
        dispatched cmd |> List.fold (fun w m -> send m w) { world with Model = model' }

let private answer (pending: Pending) (world: World) : World =
    let world' = { world with Pending = world.Pending |> List.filter (fun p -> not (obj.ReferenceEquals (p, pending))) }
    match pending with
    | PagePending (query, reply, server)
    | RefreshPending (query, reply, server) -> send (reply (Ok (serve server query))) world'

let private applyStep (world: World) (step: Step) : World =
    match step with
    | StartRefresh -> send RefreshEntries world
    | ChangeFilter -> send ToggleOnlyNeedsReview world
    | LoadMore -> send LoadMoreEntries world
    | SecondMirror -> send (MirrorFinished (Ok summary)) world
    | AnswerLoadMore ->
        match world.Pending |> List.tryFind (function PagePending (q, _, _) -> q.Page > 0 | _ -> false) with
        | Some p -> answer p world
        | None -> world
    | AnswerRefresh ->
        match world.Pending |> List.tryFind (function RefreshPending _ -> true | _ -> false) with
        | Some p -> answer p world
        | None -> world
    | MarkRow ->
        let marking = send (StartSetReviewMark (idOf 1, Reviewed)) world
        let outcome : ReviewMarkOutcome = {
            Previous = { IsApproved = false; ClearedState = ReviewUncleared }
            Change = ReviewMarkChanged
            WriteBack = ReviewMarkSent
        }
        let server = marking.Server |> List.map (fun e -> if e.Id = idOf 1 then { e with NeedsReview = false } else e)
        send (ReviewMarkSet (idOf 1, Reviewed, Ok outcome)) { marking with Server = server }

/// Beantwortet alle noch laufenden Abfragen in der Reihenfolge ihres Starts.
let rec private settle (rounds: int) (world: World) : World =
    match world.Pending with
    | [] -> world
    | _ when rounds = 0 -> failtest "die Abfragen kommen nicht zur Ruhe"
    | first :: _ -> settle (rounds - 1) (answer first world)

let rec private permutations (xs: 'a list) : 'a list list =
    match xs with
    | [] -> [ [] ]
    | _ ->
        xs
        |> List.mapi (fun i x ->
            let rest = xs |> List.indexed |> List.filter (fun (j, _) -> j <> i) |> List.map snd
            permutations rest |> List.map (fun p -> x :: p))
        |> List.concat

let private before (a: Step) (b: Step) (order: Step list) =
    List.findIndex ((=) a) order < List.findIndex ((=) b) order

[<Tests>]
let orderTests =
    testList "Kassenbuch: Reihenfolgen beim Nachladen im Hintergrund" [
        test "Über alle Reihenfolgen zeigt die Liste die zuletzt gestartete Abfrage für den aktuellen Filter" {
            // Vor dem Lauf: zwei Seiten mit altem Betrag geladen, eine Zeile davon lokal entfernt (die
            // Abfrage der nächsten Seite rückt deshalb um eine Zeile vor), 3 und 5 gewählt.
            let initial =
                { loadedModel [ for n in 1..199 -> entryWith n -1m ] 250 2 with
                    LocallyRemoved = 1
                    SelectionMode = true
                    Selection = Picked (Set.ofList [ idOf 3; idOf 5 ]) }
            let orders =
                permutations [ StartRefresh; ChangeFilter; LoadMore; AnswerLoadMore; AnswerRefresh; SecondMirror ]
                |> List.filter (fun o -> before LoadMore AnswerLoadMore o && before StartRefresh AnswerRefresh o)
            Expect.isGreaterThan (List.length orders) 100 "die Aufzählung deckt die Reihenfolgen ab"
            for order in orders do
                // Orakel aus der Nachrichtenfolge: nur der Filter zählt, und ob er gewechselt hat.
                let filterChanged = List.contains ChangeFilter order
                let expectedRows = serverRows |> List.filter (fun e -> not filterChanged || e.NeedsReview)
                let label = sprintf "%A" order
                let checkRows (world: World) =
                    match world.Model.Entries with
                    | Success rows ->
                        let ids = rows |> List.map (fun e -> e.Id)
                        Expect.equal (List.distinct ids) ids $"keine doppelte Zeile: {label}"
                    | _ -> ()
                let final =
                    order
                    |> List.fold (fun w step -> let w' = applyStep w step in checkRows w'; w') { Model = initial; Pending = []; Server = serverRows }
                    |> settle 20
                match final.Model.Entries with
                | Success rows ->
                    // Ein Präfix der aktuellen Server-Antwort für den aktuellen Filter: keine Zeile eines
                    // abgelösten Filters, keine angehängte Seite aus dem Ausschnitt vor dem Nachladen.
                    Expect.isNonEmpty rows $"Liste geladen: {label}"
                    Expect.equal rows (List.truncate (List.length rows) expectedRows) $"Präfix der aktuellen Antwort: {label}"
                    Expect.equal final.Model.TotalCount (List.length expectedRows) $"Trefferzahl: {label}"
                    // Der Seitenzeiger passt zur Liste: sonst überspränge oder wiederholte „Weitere laden“ Zeilen.
                    let window = final.Model.NextPage * pageSize - final.Model.LocallyRemoved
                    Expect.equal (List.length rows) (min window (List.length expectedRows)) $"Seitenzeiger passt zur Liste: {label}"
                | other -> failtestf "Erwartet Success, war %A: %s" other label
                if filterChanged then
                    Expect.equal final.Model.Selection NoSelection $"nach einem Filterwechsel ist die Auswahl leer: {label}"
                else
                    Expect.equal final.Model.Selection (Picked (Set.singleton (idOf 3))) $"die Auswahl ohne die gelöschte Buchung: {label}"
                    Expect.isTrue final.Model.SelectionMode $"Auswahl-Modus bleibt: {label}"
        }

        // Regression Verifier-FAIL ledger-ct3cx Iteration 1. Ohne „Weitere laden“ deckt keine Antwort den
        // ganzen Treffer ab: eine fehlende Id kann gelöscht oder hinter das Fenster gerückt sein, also
        // bleibt sie in der Auswahl. Unter „alle“ trifft eine Sammelaktion so nie die abgewählte 199.
        test "Rückt die letzte geladene Zeile hinter das Fenster, behält sie über alle Reihenfolgen Wahl und Ausnahme" {
            let loaded =
                { loadedModel [ for n in 1..199 -> entryWith n -1m ] 250 2 with
                    LocallyRemoved = 1
                    SelectionMode = true }
            let picked = Picked (Set.ofList [ idOf 3; idOf 5; idOf 199 ])
            let allButLast = AllExcept (Set.singleton (idOf 199))
            let orders =
                permutations [ StartRefresh; ChangeFilter; AnswerRefresh; SecondMirror ]
                |> List.filter (before StartRefresh AnswerRefresh)
            Expect.isGreaterThan (List.length orders) 10 "die Aufzählung deckt die Reihenfolgen ab"
            for start, order in List.allPairs [ picked; allButLast ] orders do
                let label = sprintf "%A %A" start order
                let check (world: World) =
                    match world.Model.Selection with
                    | Picked ids -> Expect.isTrue (ids.Contains (idOf 199)) $"199 bleibt gewählt: {label}"
                    | AllExcept excluded -> Expect.isTrue (excluded.Contains (idOf 199)) $"199 bleibt abgewählt: {label}"
                    | NoSelection -> ()
                let final =
                    order
                    |> List.fold (fun w step -> let w' = applyStep w step in check w'; w')
                        { Model = { loaded with Selection = start }; Pending = []; Server = grownServerRows }
                    |> settle 20
                if List.contains ChangeFilter order then
                    Expect.equal final.Model.Selection NoSelection $"nach einem Filterwechsel ist die Auswahl leer: {label}"
                else
                    match final.Model.Entries with
                    | Success rows ->
                        Expect.isFalse (rows |> List.exists (fun e -> e.Id = idOf 199)) $"199 steht hinter dem Fenster: {label}"
                    | other -> failtestf "Erwartet Success, war %A: %s" other label
                    Expect.equal final.Model.Selection start $"die Auswahl bleibt: {label}"
                    match Components.Ledger.State.reviewMarkBatchSelection final.Model, start with
                    | Some (BatchAllExcept (_, excluded)), AllExcept _ ->
                        Expect.equal excluded [ idOf 199 ] $"der Stapel-Auftrag nimmt 199 aus: {label}"
                    | Some (BatchPicked ids), Picked _ ->
                        Expect.contains ids (idOf 199) $"der Stapel-Auftrag enthält 199: {label}"
                    | other, _ -> failtestf "Unerwarteter Stapel-Auftrag %A: %s" other label
        }
        // Regression Review a3f7eb1d..982bdf79 (ledger-ct3cx): ein Einzelbefehl zwischen Start und Antwort des
        // Nachladens. Die Antwort mit dem Stand vor dem Befehl setzte die Zeile auf den alten Stand zurück.
        test "Über alle Reihenfolgen zeigt die Liste nach einem Einzelbefehl nie wieder den Stand davor" {
            let initial = loadedModel [ for n in 1..100 -> entryWith n -1m ] 250 1
            let orders =
                permutations [ StartRefresh; MarkRow; AnswerRefresh; SecondMirror ]
                |> List.filter (before StartRefresh AnswerRefresh)
            Expect.isGreaterThan (List.length orders) 10 "die Aufzählung deckt die Reihenfolgen ab"
            for order in orders do
                let label = sprintf "%A" order
                // Orakel aus der Nachrichtenfolge: nach „Geprüft“ ist Buchung 1 nicht mehr zu prüfen.
                let check (marked: bool) (world: World) =
                    match world.Model.Entries with
                    | Success rows when marked ->
                        match rows |> List.tryFind (fun e -> e.Id = idOf 1) with
                        | Some row -> Expect.isFalse row.NeedsReview $"Buchung 1 bleibt geprüft: {label}"
                        | None -> ()
                    | _ -> ()
                let final, _ =
                    order
                    |> List.fold (fun (w, marked) step ->
                        let w' = applyStep w step
                        let marked' = marked || step = MarkRow
                        check marked' w'
                        w', marked') ({ Model = initial; Pending = []; Server = serverRows }, false)
                let settled = settle 20 final
                check true settled
                match settled.Model.Entries with
                | Success rows ->
                    // Am Ende der Stand des Servers nach dem Befehl.
                    Expect.equal rows (List.truncate (List.length rows) settled.Server) $"Präfix des Servers nach dem Befehl: {label}"
                    Expect.isFalse (refreshRunning settled.Model) $"das Nachladen ist zu Ende: {label}"
                | other -> failtestf "Erwartet Success, war %A: %s" other label
        }
    ]
