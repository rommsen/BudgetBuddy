module Tests.LedgerCategoriesSyncTests

// Regression: die Kassenbuch-Filterleiste (ledger-j8xq4) lädt ihre Kategorien über
// `LedgerMsg LoadCategories`, ausgelöst über `pageLoadCmd Ledger` beim Betreten der
// Seite. Bei einem Deep-Link direkt auf `/ledger` (ein frischer Seitenaufruf, kein
// Navigations-Klick davor) läuft dieser Zweig SYNCHRON im selben `Cmd.batch` wie
// `Components.Settings.Types.LoadSettings` (`State.init`), und findet
// `model.Settings.Settings` dabei zwingend noch nicht `Success`: JS ist
// single-threaded, `Cmd.OfAsync`s Fortsetzung (auch bei einer sofort auflösenden
// Antwort) kommt frühestens im nächsten Tick. Ohne Gegenmaßnahme bliebe
// `Ledger.Categories` für immer `NotAsked`, die Kategorienauswahl der Filterleiste
// zeigte nie etwas (in einem echten Playwright-Lauf gegen `/ledger` reproduziert:
// `scripts/ledger-filter-bar.test.mjs`, Kategorienliste blieb leer).
//
// Der Fix: `State.update`s `SettingsMsg`-Zweig holt `LedgerMsg LoadCategories` nach,
// sobald Settings tatsächlich mit einem konfigurierten Budget eintreffen: nur wenn
// die Kassenbuch-Seite noch nichts hat (`NotAsked`), damit ein späteres erneutes
// Laden der Settings (z. B. nach einer Änderung auf der Settings-Seite) keinen
// bereits erfolgreichen oder fehlgeschlagenen Ladevorgang wiederholt.
//
// Baut das Modell ohne `State.init` (Fable-only, liest die Browser-URL), nach dem
// Muster von `AppReadinessTests.fs`.

open Expecto
open Types
open Shared.Domain
open State

let private baseModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = page
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        Ledger = ledgerModel
        QuickAdd = None
        QuickAddTemplates = NotAsked
    }

let private settingsWithBudget : AppSettings = {
    Comdirect = None
    Ynab =
        Some {
            PersonalAccessToken = "fake-token"
            DefaultBudgetId = Some (YnabBudgetId "fake-budget-id")
            DefaultAccountId = None
            QuickAddAccountId = None
        }
    Sync = { DaysToFetch = 30 }
}

let private settingsWithoutBudget : AppSettings = { Comdirect = None; Ynab = None; Sync = { DaysToFetch = 30 } }

/// Führt ein `Cmd` aus und sammelt die abgeschickten Nachrichten ein, nur zulässig
/// für `Cmd.ofMsg`/`Cmd.batch`/`Cmd.map` (kein `Cmd.OfAsync`, das würde den unter
/// .NET nicht existierenden Fable.Remoting-Proxy anfassen), wie in
/// `LedgerPageTests.fs`s `dispatched`.
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

[<Tests>]
let ledgerCategoriesSyncTests =
    testList "State: Kassenbuch-Kategorien nach Settings nachholen (Deep-Link-Rennen)" [

        test "Treffen Settings mit konfiguriertem Budget ein, während die Kassenbuch-Seite offen ist und noch keine Kategorien hat, wird LoadCategories nachgeholt" {
            let model = baseModel Ledger
            Expect.equal model.Ledger.Categories NotAsked "Ausgangszustand: noch nichts geladen"

            let updated, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) model

            Expect.contains
                (dispatched cmd)
                (LedgerMsg Components.Ledger.Types.LoadCategories)
                "LoadCategories wird nachgeholt, sobald ein Budget konfiguriert ist"
            // Der Zweig selbst ändert `Ledger.Categories` nicht direkt (das tut erst
            // die abgefangene `LedgerMsg LoadCategories`-Verarbeitung selbst,
            // `State.fs`); hier zählt nur, dass der Nachhol-Cmd abgeschickt wird.
            Expect.equal updated.CurrentPage Ledger "Die Seite bleibt unverändert"
        }

        test "Ohne konfiguriertes Budget wird nichts nachgeholt" {
            let model = baseModel Ledger
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithoutBudget))) model
            Expect.isEmpty (dispatched cmd) "Kein Budget, kein Nachhol-Cmd"
        }

        test "Auf einer anderen Seite als dem Kassenbuch wird nichts nachgeholt" {
            let model = baseModel SyncFlow
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) model
            Expect.isEmpty (dispatched cmd) "Die Kassenbuch-Seite ist gar nicht offen"
        }

        test "Hat die Kassenbuch-Seite schon Kategorien (Success), wird nicht erneut geladen" {
            let loaded = { (baseModel Ledger) with Ledger = { (baseModel Ledger).Ledger with Categories = Success [] } }
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) loaded
            Expect.isEmpty (dispatched cmd) "Ein bereits abgeschlossener Ladevorgang wird nicht wiederholt"
        }

        test "Läuft das Laden der Kategorien schon (Loading), wird es nicht ein zweites Mal angestoßen" {
            let loading = { (baseModel Ledger) with Ledger = { (baseModel Ledger).Ledger with Categories = Loading } }
            let _, cmd = State.update (SettingsMsg (Components.Settings.Types.SettingsLoaded (Ok settingsWithBudget))) loading
            Expect.isEmpty (dispatched cmd) "Kein zweiter Auslöser während einer laufenden Abfrage"
        }
    ]
