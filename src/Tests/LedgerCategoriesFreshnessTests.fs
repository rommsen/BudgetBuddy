module Tests.LedgerCategoriesFreshnessTests

// Available je Kategorie frisch halten und fortschreiben (ledger-kwwxz, ADR 0055
// Punkt 8): Zeitstempel, Nachlade-Entscheidung, Fortschreibung, Zeilen der Karte
// "Budget nach dem Wechsel" und Alter für die Picker-Fußzeile.

open System
open Expecto
open Types
open Shared.Domain
open Components.Ledger.Types

let private eur (v: decimal) : Money = { Amount = v; Currency = "EUR" }

let private haushaltId = YnabCategoryId (Guid.Parse "11111111-1111-1111-1111-111111111111")
let private moebelId = YnabCategoryId (Guid.Parse "22222222-2222-2222-2222-222222222222")
let private ohneZahlId = YnabCategoryId (Guid.Parse "33333333-3333-3333-3333-333333333333")

let private category id group name available : YnabCategory =
    { Id = id; Name = name; GroupName = group; Available = available; Hidden = false }

let private categories =
    [ category haushaltId "Alltag" "Haushalt" (Some (eur 412.10m))
      category moebelId "Wohnen" "Möbel" (Some (eur 120.00m))
      category ohneZahlId "Sonstiges" "Rest" None ]

let private today = DateTime(2026, 10, 24, 12, 0, 0)
let private thisMonth = DateTime(2026, 10, 3)
let private lastMonth = DateTime(2026, 9, 28)

let private availableOf id (list: YnabCategory list) =
    list |> List.find (fun c -> c.Id = id) |> fun c -> c.Available |> Option.map (fun m -> m.Amount)

let private model () = fst (Components.Ledger.State.init ())

let private update msg m =
    let m', _, _ = Components.Ledger.State.update msg m
    m'

let private rootModel stamp page : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    { CurrentPage = page
      Feedback = None
      Pwa = Pwa.initialState
      Settings = settingsModel
      Fetch = Components.Fetch.Types.init
      Rules = rulesModel
      Ledger = { model () with Categories = Success categories; CategoriesLoadedAt = stamp }
      Budget = Components.Budget.State.init ()
      Overview = Components.Overview.Types.init; SettingsReturn = Types.Page.Overview }

let private dispatched (cmd: Elmish.Cmd<State.Msg>) =
    let collected = ResizeArray<State.Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

[<Tests>]
let tests =
    testList "Kassenbuch: Available frisch halten (ledger-kwwxz)" [

        testList "Reducer" [
            test "CategoriesLoaded Ok setzt Stand und Zeitstempel" {
                let before = DateTime.Now
                let m = { model () with CategoriesFetch = CategoriesFetching 0 } |> update (CategoriesLoaded (0, Ok categories))
                Expect.equal m.Categories (Success categories) "Stand gesetzt"
                Expect.equal m.CategoriesFetch CategoriesIdle "Abruf beendet"
                Expect.isTrue (m.CategoriesLoadedAt.Value >= before) "Zeitstempel gesetzt"
            }
            test "Ein Fehler beim Nachladen lässt Stand und Zeitstempel unverändert" {
                let stamp = DateTime(2026, 10, 24, 9, 0, 0)
                let m = { model () with Categories = Success categories; CategoriesLoadedAt = Some stamp; CategoriesFetch = CategoriesFetching 0 }
                let m' = update (CategoriesLoaded (0, Error "offline")) m
                Expect.equal m'.Categories (Success categories) "Stand bleibt"
                Expect.equal m'.CategoriesLoadedAt (Some stamp) "Zeitstempel bleibt"
                Expect.equal m'.CategoriesFetch CategoriesIdle "Abruf beendet"
            }
            test "Ein Fehler ohne vorhandenen Stand wird zum Zustand" {
                let m = update (CategoriesLoaded (0, Error "offline")) { model () with Categories = Loading }
                Expect.equal m.Categories (Failure "offline") "Failure"
                Expect.isNone m.CategoriesLoadedAt "kein Zeitstempel"
            }
        ]

        testList "Nachlade-Entscheidung" [
            let now = DateTime(2026, 10, 24, 12, 0, 0)
            let ok = Success categories
            test "Stand jünger als zwei Minuten lädt nicht" {
                Expect.isFalse (shouldReloadCategories ok CategoriesIdle (Some (now.AddSeconds -90.0)) now) "jung"
            }
            test "Stand älter als zwei Minuten lädt" {
                Expect.isTrue (shouldReloadCategories ok CategoriesIdle (Some (now.AddSeconds -121.0)) now) "alt"
            }
            test "Ohne Stand lädt es" {
                Expect.isTrue (shouldReloadCategories NotAsked CategoriesIdle None now) "nie geladen"
            }
            test "Ein laufender Abruf wird nicht doppelt angestoßen" {
                Expect.isFalse (shouldReloadCategories ok (CategoriesFetching 1) (Some (now.AddMinutes -10.0)) now) "Refreshing"
                Expect.isFalse (shouldReloadCategories Loading CategoriesIdle None now) "Loading"
            }
        ]

        testList "Auslöser im Haupt-State" [
            let load = State.LedgerMsg LoadCategories

            test "Vordergrund auf der Kassenbuch-Seite lädt bei altem Stand" {
                let _, cmd = State.update State.VisibilityChanged (rootModel (Some (DateTime.Now.AddMinutes -5.0)) Page.Ledger)
                Expect.contains (dispatched cmd) load "Abruf"
            }
            test "Vordergrund bei jungem Stand lädt die Kategorien nicht" {
                let _, cmd = State.update State.VisibilityChanged (rootModel (Some DateTime.Now) Page.Ledger)
                Expect.isFalse (List.contains load (dispatched cmd)) "kein Abruf"
            }
            test "Vordergrund auf einer anderen Seite lädt nichts" {
                let _, cmd = State.update State.VisibilityChanged (rootModel None Page.LedgerRules)
                Expect.isEmpty (dispatched cmd) "nichts"
            }
            test "EnsureCategoriesFresh bei jungem Stand tut nichts" {
                let m = rootModel (Some DateTime.Now) Page.Ledger
                let m', cmd = State.update (State.LedgerMsg EnsureCategoriesFresh) m
                Expect.equal m' m "unverändert"
                Expect.isEmpty (dispatched cmd) "kein Abruf"
            }
        ]

        testList "shiftAvailable" [
            let shift amount oldId newId date =
                shiftAvailable (date, amount, oldId, newId, today) categories
            test "Ausgabe im laufenden Monat: alt plus Betrag, neu minus Betrag" {
                let r = shift -50m (Some haushaltId) (Some moebelId) thisMonth
                Expect.equal (availableOf haushaltId r) (Some 462.10m) "alt"
                Expect.equal (availableOf moebelId r) (Some 70.00m) "neu"
            }
            test "Einnahme: umgekehrt" {
                let r = shift 50m (Some haushaltId) (Some moebelId) thisMonth
                Expect.equal (availableOf haushaltId r) (Some 362.10m) "alt"
                Expect.equal (availableOf moebelId r) (Some 170.00m) "neu"
            }
            test "Buchung aus dem Vormonat ändert nichts" {
                Expect.equal (shift -50m (Some haushaltId) (Some moebelId) lastMonth) categories "unverändert"
            }
            test "Wechsel auf ohne Kategorie erhöht nur die alte" {
                let r = shift -50m (Some haushaltId) None thisMonth
                Expect.equal (availableOf haushaltId r) (Some 462.10m) "alt"
                Expect.equal (availableOf moebelId r) (Some 120.00m) "neu unberührt"
            }
            test "Kategorie ohne Available bleibt ohne" {
                let r = shift -50m (Some haushaltId) (Some ohneZahlId) thisMonth
                Expect.equal (availableOf ohneZahlId r) None "bleibt None"
            }
        ]

        testList "categoryChangeHint" [
            test "Laufender Monat: alt und neu, Reihenfolge alt dann neu, deutsches Format" {
                let rows = categoryChangeHint (thisMonth, -81.90m, Some haushaltId, Some moebelId, today) categories
                Expect.equal
                    rows
                    [ { Label = "Alltag: Haushalt"; OldText = Some "412,10 €"; NewText = Some "494,00 €" }
                      { Label = "Wohnen: Möbel"; OldText = Some "120,00 €"; NewText = Some "38,10 €" } ]
                    "Zeilen"
            }
            test "Neuer Wert gleich Wert nach shiftAvailable" {
                let args = (thisMonth, -81.90m, Some haushaltId, Some moebelId, today)
                let shifted = shiftAvailable args categories
                let rows = categoryChangeHint args categories
                Expect.equal rows.[0].NewText (Some "494,00 €") "Text"
                Expect.equal (availableOf haushaltId shifted) (Some 494.00m) "gleicher Wert"
            }
            test "Vormonat: nur Label" {
                let rows = categoryChangeHint (lastMonth, -50m, Some haushaltId, Some moebelId, today) categories
                Expect.equal
                    rows
                    [ { Label = "Alltag: Haushalt"; OldText = None; NewText = None }
                      { Label = "Wohnen: Möbel"; OldText = None; NewText = None } ]
                    "nur Labels"
            }
            test "Kategorie ohne Available: nur Label" {
                let rows = categoryChangeHint (thisMonth, -50m, Some haushaltId, Some ohneZahlId, today) categories
                Expect.equal rows.[1] { Label = "Sonstiges: Rest"; OldText = None; NewText = None } "ohne Zahlen"
            }
            test "Ohne Kategorie ergibt keine Zeile" {
                let rows = categoryChangeHint (thisMonth, -50m, Some haushaltId, None, today) categories
                Expect.equal (rows |> List.map (fun r -> r.Label)) [ "Alltag: Haushalt" ] "nur die alte"
                let rows2 = categoryChangeHint (thisMonth, -50m, None, Some moebelId, today) categories
                Expect.equal (rows2 |> List.map (fun r -> r.Label)) [ "Wohnen: Möbel" ] "nur die neue"
            }
        ]

        testList "availableAgeLabel" [
            let at = DateTime(2026, 10, 24, 10, 42, 0)
            test "unter einer Minute" {
                Expect.equal (availableAgeLabel at (at.AddSeconds 30.0)) "Stand gerade eben" "frisch"
            }
            test "Minuten" {
                Expect.equal (availableAgeLabel at (at.AddMinutes 3.0)) "Stand vor 3 Min" "3 Min"
            }
            test "über eine Stunde als Uhrzeit" {
                Expect.equal (availableAgeLabel at (at.AddMinutes 90.0)) "Stand 10:42" "Uhrzeit"
            }
        ]
    ]

// ============================================
// Späte Antworten der Kategorien (ledger-pfhdq)
// ============================================

let private lebensmittelId = YnabCategoryId (Guid.Parse "44444444-4444-4444-4444-444444444444")

let private lebensmittelWith (amount: decimal) =
    [ category lebensmittelId "Alltag" "Lebensmittel" (Some (eur amount)) ]

let private expense : LedgerEntryView =
    { Id = Guid.Parse "0195f500-0000-7000-8000-00000000f001"
      AccountId = Guid.Parse "0195e500-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -30m
      Payee = Some "Markt"
      Memo = None
      CategoryName = None
      CategoryId = None
      CategoryGroupName = None
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = false
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
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = []
      PossibleDuplicate = None; CategoryFromOrderId = false }

let private settingsWithBudget : AppSettingsView =
    { Comdirect = None
      Ynab =
        Some
            { HasToken = true
              DefaultBudgetId = Some (YnabBudgetId "pfhdq-budget")
              DefaultAccountId = None
              QuickAddAccountId = None }
      Sync = { DaysToFetch = 30; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 360 } }

let private loadedStamp = DateTime(2026, 11, 19, 9, 0, 0)

/// Kassenbuch mit geladenen Kategorien (Lebensmittel 100 €) und einer Ausgabe von 30 € ohne Kategorie im
/// laufenden Monat, Settings mit Budget.
let private freshnessRoot () : State.Model =
    let root = rootModel (Some loadedStamp) Page.Ledger
    let ledger =
        { root.Ledger with
            Categories = Success (lebensmittelWith 100m)
            Entries = Success [ expense ]
            TotalCount = 1
            TotalAmount = eur -30m }
    { root with
        Ledger = ledger
        Settings = { root.Settings with Settings = Success settingsWithBudget } }

/// Ports, deren `getCategories` nacheinander die Werte aus `answers` liefert.
let private portsAnswering (answers: Result<YnabCategory list, YnabError> list) : State.UpdatePorts =
    let queue = System.Collections.Concurrent.ConcurrentQueue<Result<YnabCategory list, YnabError>>(answers)
    { GetCategories =
        fun _ ->
            async {
                match queue.TryDequeue() with
                | true, answer -> return answer
                | _ -> return Error (YnabError.NetworkError "keine Antwort vorgesehen")
            } }

/// Startet einen Abruf über das Root-`update` und liefert das Modell samt der Antwort, die der Port bringt.
let private startFetch (ports: State.UpdatePorts) (m: State.Model) : State.Model * State.Msg =
    let m', cmd = State.updateWith ports (State.LedgerMsg LoadCategories) m
    let answer, _ = Tests.CmdTiming.firstDispatchedAfter 3000 cmd
    m', answer

let private confirmChange (ports: State.UpdatePorts) (m: State.Model) : State.Model =
    let outcome : CategoryChangeOutcome =
        { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }
    fst (State.updateWith ports (State.LedgerMsg (EntryCategorySet (expense.Id, Some lebensmittelId, Ok outcome))) m)

let private lebensmittelAvailable (m: State.Model) =
    match m.Ledger.Categories with
    | Success list -> availableOf lebensmittelId list
    | other -> failtestf "Kategorien erwartet, war %A" other

let private fetchRunning (m: State.Model) = m.Ledger.CategoriesFetch <> CategoriesIdle

/// Ein Schritt der Aufzählung: Abruf-Start A oder B, die Kategorieänderung (Fortschreibung), die Antwort auf A oder B.
type private CategoriesStep =
    | StartA
    | StartB
    | Shift
    | AnswerA
    | AnswerB

let rec private orderings (xs: 'a list) : 'a list list =
    match xs with
    | [] -> [ [] ]
    | _ ->
        xs
        |> List.indexed
        |> List.collect (fun (i, x) ->
            let rest = xs |> List.indexed |> List.filter (fun (j, _) -> j <> i) |> List.map snd
            orderings rest |> List.map (fun p -> x :: p))

/// Alle zulässigen Reihenfolgen: A ist der zuerst gestartete Abruf, jede Antwort kommt nach ihrem Start.
let private categoriesOrders =
    orderings [ StartA; Shift; StartB; AnswerA; AnswerB ]
    |> List.filter (fun order ->
        let at step = List.findIndex ((=) step) order
        at StartA < at StartB && at StartA < at AnswerA && at StartB < at AnswerB)

/// Orakel aus der Nachrichtenfolge: eine erfolgreiche Antwort gilt, wenn nach ihrem Start weder ein neuer Start
/// noch eine Fortschreibung kam; die Fortschreibung senkt Lebensmittel um 30 €; ein Fehler ändert nichts.
let private expectedAvailable (order: CategoriesStep list) (answerA: Result<decimal, string>) (answerB: Result<decimal, string>) =
    let _, value =
        order
        |> List.fold
            (fun (latest, value) step ->
                match step with
                | StartA | StartB | Shift -> step, (if step = Shift then value - 30m else value)
                | AnswerA ->
                    match answerA with
                    | Ok v when latest = StartA -> latest, v
                    | _ -> latest, value
                | AnswerB ->
                    match answerB with
                    | Ok v when latest = StartB -> latest, v
                    | _ -> latest, value)
            (Shift, 100m)
    value

[<Tests>]
let lateAnswerTests =
    testList "Kassenbuch: späte Antworten der Kategorien (ledger-pfhdq)" [

        // Regression ledger-pfhdq: die Antwort eines vor der Kategorieänderung gestarteten Abrufs setzte das
        // fortgeschriebene Available zurück; Roman sah in der Karte und im Picker alte Zahlen.
        test "Eine vor der Fortschreibung gestartete Antwort setzt Available nicht zurück" {
            let ports = portsAnswering [ Ok (lebensmittelWith 100m) ]
            let started, answer = startFetch ports (freshnessRoot ())
            let shifted = confirmChange ports started
            Expect.equal (lebensmittelAvailable shifted) (Some 70m) "fortgeschrieben"
            let after, _ = State.updateWith ports answer shifted
            Expect.equal (lebensmittelAvailable after) (Some 70m) "die alte Antwort fällt weg"
            Expect.equal after.Ledger.CategoriesLoadedAt (Some loadedStamp) "Zeitstempel unverändert"
            Expect.isFalse (fetchRunning after) "kein Abruf läuft mehr"
        }

        test "Ein nach der Fortschreibung gestarteter Abruf gilt" {
            for amount in [ 70m; 65m ] do
                let ports = portsAnswering [ Ok (lebensmittelWith amount) ]
                let shifted = confirmChange ports (freshnessRoot ())
                let started, answer = startFetch ports shifted
                let after, _ = State.updateWith ports answer started
                Expect.equal (lebensmittelAvailable after) (Some amount) $"die Antwort gilt ({amount})"
                Expect.isTrue (after.Ledger.CategoriesLoadedAt > Some loadedStamp) "Zeitstempel neu"
                Expect.isFalse (fetchRunning after) "kein Abruf läuft mehr"
        }

        test "Reihenfolgen von zwei Abrufen und einer Fortschreibung" {
            let cases : (Result<decimal, string> * Result<decimal, string>) list =
                [ Ok 80m, Ok 60m; Ok 80m, Error "weg"; Error "weg", Ok 60m; Error "weg", Error "weg" ]
            Expect.hasLength categoriesOrders 15 "fünfzehn zulässige Reihenfolgen"
            for order in categoriesOrders do
                for answerA, answerB in cases do
                    let toPort = function
                        | Ok v -> Ok (lebensmittelWith v)
                        | Error e -> Error (YnabError.NetworkError e)
                    let ports = portsAnswering [ toPort answerA; toPort answerB ]
                    let final, _ =
                        order
                        |> List.fold
                            (fun (m, answers: Map<CategoriesStep, State.Msg>) step ->
                                match step with
                                | StartA | StartB ->
                                    let m', answer = startFetch ports m
                                    m', answers |> Map.add (if step = StartA then AnswerA else AnswerB) answer
                                | Shift -> confirmChange ports m, answers
                                | AnswerA | AnswerB -> fst (State.updateWith ports answers[step] m), answers)
                            (freshnessRoot (), Map.empty)
                    let label = $"{order} {answerA} {answerB}"
                    Expect.equal (lebensmittelAvailable final) (Some (expectedAvailable order answerA answerB)) $"{label}: Available"
                    Expect.isFalse (fetchRunning final) $"{label}: kein Abruf läuft mehr"
        }
    ]
