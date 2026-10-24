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
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    { CurrentPage = page
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      SyncFlow = syncFlowModel
      Rules = rulesModel
      Ledger = { model () with Categories = Success categories; CategoriesLoadedAt = stamp }
      QuickAdd = None
      QuickAddTemplates = NotAsked }

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
                let m = { model () with CategoriesRefreshing = true } |> update (CategoriesLoaded (Ok categories))
                Expect.equal m.Categories (Success categories) "Stand gesetzt"
                Expect.isFalse m.CategoriesRefreshing "Abruf beendet"
                Expect.isTrue (m.CategoriesLoadedAt.Value >= before) "Zeitstempel gesetzt"
            }
            test "Ein Fehler beim Nachladen lässt Stand und Zeitstempel unverändert" {
                let stamp = DateTime(2026, 10, 24, 9, 0, 0)
                let m = { model () with Categories = Success categories; CategoriesLoadedAt = Some stamp; CategoriesRefreshing = true }
                let m' = update (CategoriesLoaded (Error "offline")) m
                Expect.equal m'.Categories (Success categories) "Stand bleibt"
                Expect.equal m'.CategoriesLoadedAt (Some stamp) "Zeitstempel bleibt"
                Expect.isFalse m'.CategoriesRefreshing "Abruf beendet"
            }
            test "Ein Fehler ohne vorhandenen Stand wird zum Zustand" {
                let m = update (CategoriesLoaded (Error "offline")) { model () with Categories = Loading }
                Expect.equal m.Categories (Failure "offline") "Failure"
                Expect.isNone m.CategoriesLoadedAt "kein Zeitstempel"
            }
        ]

        testList "Nachlade-Entscheidung" [
            let now = DateTime(2026, 10, 24, 12, 0, 0)
            let ok = Success categories
            test "Stand jünger als zwei Minuten lädt nicht" {
                Expect.isFalse (shouldReloadCategories ok false (Some (now.AddSeconds -90.0)) now) "jung"
            }
            test "Stand älter als zwei Minuten lädt" {
                Expect.isTrue (shouldReloadCategories ok false (Some (now.AddSeconds -121.0)) now) "alt"
            }
            test "Ohne Stand lädt es" {
                Expect.isTrue (shouldReloadCategories NotAsked false None now) "nie geladen"
            }
            test "Ein laufender Abruf wird nicht doppelt angestoßen" {
                Expect.isFalse (shouldReloadCategories ok true (Some (now.AddMinutes -10.0)) now) "Refreshing"
                Expect.isFalse (shouldReloadCategories Loading false None now) "Loading"
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
                let _, cmd = State.update State.VisibilityChanged (rootModel None Page.Settings)
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
