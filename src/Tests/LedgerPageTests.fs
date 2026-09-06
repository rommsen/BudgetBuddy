module LedgerPageTests

// Die Kassenbuch-Seite (ledger-q5w9c), Slice 3 des Walking Skeletons.
//
// Zwei Testgruppen, beide ohne Datenbank und ohne Netz:
//  * die reine Abbildung Ereignis -> Historien-Satz und Zustand -> Sicht
//    (`Ledger.QueryHandlers.ReadModel`) samt Kontostand;
//  * die Reduzierer-Uebergaenge der Seite (`Components.Ledger.State.update`).
//
// Bewusst NICHT getestet: die Zweige, die den Fable.Remoting-Proxy bauen
// (`LoadAccounts`, `LoadEntries`, `LoadHistory`, `RunMirror`, `LoadMirrorStatus`).
// Dessen Typ-Initialisierer ist Fable-only und wirft unter .NET. Deshalb sind die
// Ausloeser der Seite in zwei Nachrichten geteilt: eine reine, die den Zustand setzt
// ("Spiegel laeuft"), und eine unreine, die den Aufruf absetzt.

open System
open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.QueryHandlers
open Types
open Components.Ledger.Types

// ============================================
// Bausteine
// ============================================

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private catId (n: int) =
    YnabCategoryId (Guid.Parse $"0195d000-0000-7000-8000-0000000000{n:D2}")

let private category (n: int) (name: string) : CategoryRef =
    { CategoryId = catId n; CategoryName = name }

let private accountGuid (n: int) = Guid.Parse $"0195e000-0000-7000-8000-0000000000{n:D2}"
let private entryGuid (n: int) = Guid.Parse $"0195f000-0000-7000-8000-0000000000{n:D2}"
let private ynabAccount (n: int) = YnabAccountId (accountGuid n)

let private ledgerAccount (n: int) (name: string) (link: AccountLink) : LedgerAccount = {
    Id = LedgerAccountId (accountGuid n)
    Name = name
    Kind = BankAccount
    Link = link
    Closed = false
}

let private splitTo (name: string) (amount: decimal) : TransactionSplit = {
    Target = ToCategory (catId 9, name)
    Amount = eur amount
    Memo = None
}

let private entry (n: int) (amount: decimal) : Entry = {
    Id = EntryId (entryGuid n)
    AccountId = LedgerAccountId (accountGuid 1)
    Date = DateTime(2026, 3, 4)
    Amount = eur amount
    Payee = Some "Rewe"
    Memo = None
    Category = Some (category 1 "Lebensmittel")
    Splits = []
    Cleared = Uncleared
    TransferEntryId = None
    DeletedInYnab = false
}

let private recorded (seq: int) (actor: Actor) (body: LedgerEventBody) : RecordedLedgerEvent = {
    Sequence = seq
    EventType = eventTypeName body
    SchemaVersion = schemaVersion body
    Event = {
        EntryId = EntryId (entryGuid 1)
        OccurredAt = DateTime(2026, 3, 5, 7, 15, 0, DateTimeKind.Utc)
        Actor = actor
        Body = body
    }
}

let private entryView (n: int) (amount: decimal) (deleted: bool) : LedgerEntryView =
    ReadModel.toEntryView MirroredFromYnab { entry n amount with DeletedInYnab = deleted }

// ============================================
// Reine Abbildung: Ereignis -> Historien-Satz
// ============================================

let historyLanguageTests =
    testList "Historie spricht die Sprache des Kassenbuchs" [

        test "Erstbeobachtung nennt Payee, Betrag und Kategorie" {
            let body =
                EntryObservedInYnab {
                    AccountId = LedgerAccountId (accountGuid 1)
                    YnabTransactionId = "txn-1"
                    ImportId = None
                    Date = DateTime(2026, 3, 4)
                    Amount = eur -42.5m
                    Payee = Some "Rewe"
                    Memo = None
                    Category = Some (category 1 "Lebensmittel")
                    Splits = []
                    Cleared = Uncleared
                    TransferAccountId = None
                    TransferYnabTransactionId = None
                }
            Expect.equal
                (ReadModel.describe body)
                "Erstmals in YNAB gesehen: Rewe, -42,50 EUR, Kategorie Lebensmittel"
                "Die Erstbeobachtung erzaehlt, was gesehen wurde"
        }

        test "Kategorie-Wechsel nennt alten und neuen Namen, keinen Typnamen" {
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = Some (category 1 "Lebensmittel")
                    NewCategory = Some (category 2 "Restaurant")
                    OldSplits = []
                    NewSplits = []
                }
            let text = ReadModel.describe body
            Expect.equal
                text
                "Kategorie in YNAB geändert: Lebensmittel → Restaurant"
                "Genau der Satz aus der Modellierung"
            Expect.isFalse
                (text.Contains "EntryCategorizationChangeObservedInYnab")
                "Ein Typname hat in der Historie nichts zu suchen"
        }

        test "Erstmals gesetzte Kategorie heisst 'gesetzt', nicht 'geändert'" {
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = None
                    NewCategory = Some (category 2 "Restaurant")
                    OldSplits = []
                    NewSplits = []
                }
            Expect.equal
                (ReadModel.describe body)
                "Kategorie in YNAB gesetzt: Restaurant"
                "Ohne alte Kategorie gibt es nichts, wovon aus geaendert wuerde"
        }

        test "Entfernte Kategorie nennt die weggefallene" {
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = Some (category 1 "Lebensmittel")
                    NewCategory = None
                    OldSplits = []
                    NewSplits = []
                }
            Expect.equal
                (ReadModel.describe body)
                "Kategorie in YNAB entfernt: Lebensmittel"
                "Die Historie sagt, was verschwunden ist"
        }

        test "Neue Aufteilung wird als Split-Zeilen erzaehlt" {
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = Some (category 1 "Lebensmittel")
                    NewCategory = None
                    OldSplits = []
                    NewSplits = [ splitTo "Lebensmittel" -20m; splitTo "Drogerie" -22.5m ]
                }
            Expect.equal
                (ReadModel.describe body)
                "Kategorie in YNAB entfernt: Lebensmittel; In YNAB in 2 Split-Zeilen aufgeteilt"
                "Kategorie und Splits sind zwei Aussagen in einem Ereignis"
        }

        test "Geaenderter Betrag nennt beide Betraege" {
            let body =
                EntryDetailsChangeObservedInYnab {
                    OldDate = DateTime(2026, 3, 4)
                    NewDate = DateTime(2026, 3, 4)
                    OldAmount = eur -42.5m
                    NewAmount = eur -45m
                    OldPayee = Some "Rewe"
                    NewPayee = Some "Rewe"
                    OldMemo = None
                    NewMemo = None
                    OldAccountId = LedgerAccountId (accountGuid 1)
                    NewAccountId = LedgerAccountId (accountGuid 1)
                }
            Expect.equal
                (ReadModel.describe body)
                "Betrag in YNAB geändert: -42,50 EUR → -45,00 EUR"
                "Nur das, was sich unterscheidet, wird erzaehlt"
        }

        test "Mehrere geaenderte Kopfdaten ergeben einen Satz mit mehreren Teilen" {
            let body =
                EntryDetailsChangeObservedInYnab {
                    OldDate = DateTime(2026, 3, 4)
                    NewDate = DateTime(2026, 3, 6)
                    OldAmount = eur -42.5m
                    NewAmount = eur -42.5m
                    OldPayee = Some "Rewe"
                    NewPayee = Some "Edeka"
                    OldMemo = None
                    NewMemo = None
                    OldAccountId = LedgerAccountId (accountGuid 1)
                    NewAccountId = LedgerAccountId (accountGuid 2)
                }
            Expect.equal
                (ReadModel.describe body)
                "Datum in YNAB geändert: 04.03.2026 → 06.03.2026; Payee in YNAB geändert: Rewe → Edeka; Buchung in YNAB auf ein anderes Konto verschoben"
                "Jede geaenderte Gruppe steht im Satz"
        }

        test "Cleared-Wechsel spricht deutsch, nicht YNABs Vokabular" {
            let body =
                EntryClearedStateChangeObservedInYnab {
                    OldCleared = Uncleared
                    NewCleared = Reconciled
                }
            Expect.equal
                (ReadModel.describe body)
                "Status in YNAB geändert: offen → abgeglichen"
                "Die Anzeige uebersetzt YNABs Status"
        }

        test "Loeschung ist ein Satz, die Buchung bleibt" {
            Expect.equal
                (ReadModel.describe EntryDeletionObservedInYnab)
                "In YNAB gelöscht"
                "Geloescht heisst gekennzeichnet, nicht verschwunden"
        }

        test "Historien-Zeile traegt Nummer, Zeitpunkt und Herkunft" {
            let line = ReadModel.toHistoryLine (recorded 3 Actor.YnabMirror EntryDeletionObservedInYnab)
            Expect.equal line.Sequence 3 "Die laufende Nummer kommt aus dem Log"
            Expect.equal
                line.OccurredAt
                (DateTime(2026, 3, 5, 7, 15, 0, DateTimeKind.Utc))
                "Der Zeitpunkt kommt aus dem Umschlag"
            Expect.equal line.Origin MirroredFromYnab "Der Actor wird zur Herkunft"
            Expect.equal (originLabel line.Origin) "YNAB-Spiegel" "So heisst die Herkunft im Kassenbuch"
        }

        test "Jeder Actor hat eine Herkunft in der Sprache des Kassenbuchs" {
            Expect.equal (ReadModel.originOf Actor.YnabMirror) MirroredFromYnab "Spiegel"
            Expect.equal (ReadModel.originOf Actor.ComdirectImport) ImportedFromComdirect "Import"
            Expect.equal (ReadModel.originOf (Actor.Rule (RuleId (Guid.NewGuid())))) DecidedByRule "Regel"
            Expect.equal (ReadModel.originOf Actor.UserInBb) DecidedInBb "Mensch in BB"
            Expect.equal (ReadModel.originOf Actor.QuickAdd) AddedViaQuickAdd "Quick Add"
            Expect.equal (originLabel ImportedFromComdirect) "Comdirect-Import" "Wortlaut aus dem README"
        }
    ]

// ============================================
// Reine Abbildung: Zustand -> Sicht, Kontostand
// ============================================

let readModelTests =
    testList "Kassenbuch-Sichten" [

        test "Buchung wird mit Kategorie-Name und Herkunft zur Sicht" {
            let view = ReadModel.toEntryView MirroredFromYnab (entry 1 -42.5m)
            Expect.equal view.Id (entryGuid 1) "Die eigene Buchungs-Id bleibt"
            Expect.equal view.CategoryName (Some "Lebensmittel") "Der Name, nicht die Id"
            Expect.equal view.Origin MirroredFromYnab "Die Herkunft wird durchgereicht"
            Expect.isFalse view.DeletedInYnab "Nicht geloescht"
        }

        test "Geloeschte Buchung bleibt sichtbar und traegt das Kennzeichen" {
            let view = ReadModel.toEntryView MirroredFromYnab { entry 1 -42.5m with DeletedInYnab = true }
            Expect.isTrue view.DeletedInYnab "Das Kennzeichen ueberlebt die Abbildung"
        }

        test "Kontostand ist die Summe der nicht geloeschten Buchungen" {
            let entries = [ entryView 1 -42.5m false; entryView 2 100m false; entryView 3 -7.25m false ]
            Expect.equal (ledgerBalance "EUR" entries) (eur 50.25m) "100 - 42,50 - 7,25"
        }

        test "Geloeschte Buchungen zaehlen nicht zum Kontostand" {
            let entries = [ entryView 1 -42.5m false; entryView 2 1000m true ]
            Expect.equal
                (ledgerBalance "EUR" entries)
                (eur -42.5m)
                "Die in YNAB geloeschte Buchung bleibt sichtbar, aber ausserhalb des Stands"
        }

        test "Kontostand ohne Buchungen ist null in der Fallback-Waehrung" {
            Expect.equal (ledgerBalance "EUR" []) (eur 0m) "Leeres Konto, kein Fehler"
        }

        test "Kontostand summiert exakt, ohne Gleitkomma-Drift" {
            // ADR 0017: Betraege liegen als exakter Dezimal-Text. Zehn mal 0,10
            // ergeben in `decimal` genau 1,00 — in `float` nicht.
            let entries = List.init 10 (fun i -> entryView (i + 1) 0.1m false)
            Expect.equal (ledgerBalance "EUR" entries) (eur 1.0m) "Genau 1,00, nicht 0,9999999"
        }

        test "Vorauswahl ist das gespiegelte YNAB-Standardkonto" {
            let accounts =
                [ ledgerAccount 1 "Comdirect" (YnabMirrored (ynabAccount 11))
                  ledgerAccount 2 "ING" (YnabMirrored (ynabAccount 12)) ]
                |> List.map ReadModel.toAccountView

            Expect.equal
                (ReadModel.defaultAccountId (Some (ynabAccount 12)) accounts)
                (Some (accountGuid 2))
                "Das Konto hinter ynab_default_account_id wird vorausgewaehlt"
        }

        test "Ohne passendes Standardkonto wird das erste vorausgewaehlt" {
            let accounts =
                [ ledgerAccount 1 "Comdirect" (YnabMirrored (ynabAccount 11))
                  ledgerAccount 2 "Bar" BbOwned ]
                |> List.map ReadModel.toAccountView

            Expect.equal
                (ReadModel.defaultAccountId (Some (ynabAccount 99)) accounts)
                (Some (accountGuid 1))
                "Fallback auf das erste Konto"
            Expect.equal
                (ReadModel.defaultAccountId None accounts)
                (Some (accountGuid 1))
                "Auch ohne hinterlegtes Standardkonto"
        }

        test "Ohne Konten gibt es keine Vorauswahl" {
            Expect.equal (ReadModel.defaultAccountId (Some (ynabAccount 11)) []) None "Leeres Kassenbuch"
        }

        test "Ein BB-eigenes Konto traegt keine YNAB-Id" {
            let view = ReadModel.toAccountView (ledgerAccount 2 "Bar" BbOwned)
            Expect.equal view.YnabAccountId None "BbOwned hat keine Fremd-Id (ADR 0016)"
            Expect.equal view.Name "Bar" "Der Name bleibt"
        }
    ]

// ============================================
// Reduzierer der Kassenbuch-Seite
// ============================================

let private accountsView : LedgerAccountsView = {
    Accounts =
        [ ledgerAccount 1 "Comdirect" (YnabMirrored (ynabAccount 11))
          ledgerAccount 2 "ING" (YnabMirrored (ynabAccount 12)) ]
        |> List.map ReadModel.toAccountView
    DefaultAccountId = Some (accountGuid 2)
}

let private freshModel () : Model =
    let model, _ = Components.Ledger.State.init ()
    model

let private summary : MirrorRunSummary = {
    NewEntries = 3
    ChangedEntries = 1
    DeletedEntries = 0
    ServerKnowledge = 4711L
}

let reducerTests =
    testList "Kassenbuch-Seite: Reduzierer" [

        test "Geladene Konten waehlen das Standardkonto vor" {
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) (freshModel ())
            Expect.equal updated.SelectedAccountId (Some (accountGuid 2)) "Die Vorauswahl des Servers gilt"
            Expect.isTrue (RemoteData.isSuccess updated.Accounts) "Konten sind geladen"
        }

        test "Ein leeres Kassenbuch waehlt nichts vor und laedt keine Buchungen" {
            let empty = { Accounts = []; DefaultAccountId = None }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok empty)) (freshModel ())
            Expect.equal updated.SelectedAccountId None "Es gibt kein Konto zu zeigen"
            Expect.equal updated.Entries NotAsked "Ohne Konto gibt es nichts zu laden"
        }

        test "Fehlgeschlagenes Laden der Konten bleibt sichtbar" {
            let updated, _, _ =
                Components.Ledger.State.update (AccountsLoaded (Error "keine Verbindung")) (freshModel ())
            Expect.equal updated.Accounts (Failure "keine Verbindung") "Der Fehler wird nicht geschluckt"
        }

        test "Kontowechsel waehlt das Konto und laedt dessen Buchungen" {
            let loaded, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (SelectAccount (accountGuid 1)) loaded
            Expect.equal updated.SelectedAccountId (Some (accountGuid 1)) "Das gewaehlte Konto"
            Expect.equal updated.Entries Loading "Die Buchungen des neuen Kontos werden geholt"
        }

        test "Geladene Buchungen behalten geloeschte Eintraege" {
            let entries = [ entryView 1 -42.5m false; entryView 2 -10m true ]
            let updated, _, _ = Components.Ledger.State.update (EntriesLoaded (Ok entries)) (freshModel ())
            match updated.Entries with
            | Success loaded ->
                Expect.equal (List.length loaded) 2 "Nichts wird herausgefiltert"
                Expect.isTrue
                    (loaded |> List.exists (fun e -> e.DeletedInYnab))
                    "Die geloeschte Buchung bleibt in der Liste"
            | other -> failtestf "Erwartet Success, war %A" other
        }

        test "Fehlgeschlagene Buchungen werden als Fehler gezeigt" {
            let updated, _, _ = Components.Ledger.State.update (EntriesLoaded (Error "kaputt")) (freshModel ())
            Expect.equal updated.Entries (Failure "kaputt") "Der Fehler bleibt sichtbar"
        }

        test "Spiegel-Start setzt den Hinweis, ohne die Liste zu leeren" {
            // ADR 0014: Roman wartet nie auf den Spiegel — die Seite zeigt den
            // bestehenden Stand weiter, waehrend der Lauf laeuft.
            let entries = [ entryView 1 -42.5m false ]
            let loaded, _, _ = Components.Ledger.State.update (EntriesLoaded (Ok entries)) (freshModel ())
            let running, _, _ = Components.Ledger.State.update StartMirror loaded
            Expect.isTrue running.MirrorRunning "Der Hinweis 'Spiegel laeuft' steht"
            Expect.equal running.Entries (Success entries) "Die Liste bleibt unveraendert stehen"
        }

        test "Erfolgreicher Spiegel-Lauf endet mit Zusammenfassung als Toast" {
            let running = { freshModel () with MirrorRunning = true }
            let updated, _, external =
                Components.Ledger.State.update (MirrorFinished (Ok summary)) running
            Expect.isFalse updated.MirrorRunning "Der Lauf ist vorbei"
            match external with
            | ShowToast (message, ToastSuccess) ->
                Expect.stringContains message "3" "Die Zahl der neuen Buchungen steht drin"
                Expect.stringContains message "1" "Die Zahl der geaenderten Buchungen steht drin"
            | other -> failtestf "Erwartet Erfolgs-Toast, war %A" other
        }

        test "Gescheiterter Spiegel-Lauf zeigt den Fehlertext" {
            let running = { freshModel () with MirrorRunning = true }
            let updated, _, external =
                Components.Ledger.State.update (MirrorFinished (Error "YNAB-Limit erreicht")) running
            Expect.isFalse updated.MirrorRunning "Auch ein Fehler beendet den Lauf"
            match external with
            | ShowToast (message, ToastError) ->
                Expect.stringContains message "YNAB-Limit erreicht" "Der Fehler wird nicht geschluckt (ADR 0014)"
            | other -> failtestf "Erwartet Fehler-Toast, war %A" other
        }

        test "Historie oeffnet sich fuer die angetippte Buchung" {
            let view = entryView 1 -42.5m false
            let updated, _, _ = Components.Ledger.State.update (OpenHistory view) (freshModel ())
            Expect.equal (updated.HistoryFor |> Option.map (fun e -> e.Id)) (Some (entryGuid 1)) "Die richtige Buchung"
            Expect.equal updated.History Loading "Die Historie wird geholt"
        }

        test "Historie schliesst sich wieder" {
            let view = entryView 1 -42.5m false
            let opened, _, _ = Components.Ledger.State.update (OpenHistory view) (freshModel ())
            let closed, _, _ = Components.Ledger.State.update CloseHistory opened
            Expect.equal closed.HistoryFor None "Kein Sheet mehr offen"
        }

        test "Geladene Historie steht in gespeicherter Reihenfolge" {
            let lines =
                [ recorded 1 Actor.YnabMirror EntryDeletionObservedInYnab
                  recorded 2 Actor.YnabMirror EntryDeletionObservedInYnab ]
                |> List.map ReadModel.toHistoryLine
            let updated, _, _ = Components.Ledger.State.update (HistoryLoaded (Ok lines)) (freshModel ())
            match updated.History with
            | Success loaded -> Expect.equal (loaded |> List.map (fun l -> l.Sequence)) [ 1; 2 ] "Reihenfolge bleibt"
            | other -> failtestf "Erwartet Success, war %A" other
        }
    ]

// ============================================
// Regression: der erste Spiegel-Lauf auf einem leeren Kassenbuch
// ============================================
//
// Abnahme-Fund vom 2026-09-06 (ledger-kt0wq, Human-Gate). Der Spiegel schrieb 7 Konten
// und 2508 Buchungen, die Seite zeigte weiter "Noch kein Konto". Zwei getrennte
// Ursachen, beide hier festgenagelt:
//
//  A) Deep-Link oder F5 auf `#/ledger` loeste kein einziges Ladekommando aus.
//     `State.init` schrieb die Seite aus der URL schon in `CurrentPage` und schickte
//     danach `UrlChanged` fuer dieselbe Seite; dessen Gleichheits-Guard traf sofort zu
//     und lieferte `Cmd.none`. Die Ladekommandos stehen jetzt in `State.pageLoadCmd`,
//     das `init` direkt aufruft. Betroffen war neben dem Kassenbuch auch `Rules`.
//  B) Nach einem Lauf lud die Seite nur Buchungen und Lauf-Status nach, nicht die
//     Konten. Ohne gewaehltes Konto tat `LoadEntries` still nichts, also blieb die
//     Seite auf dem leeren Stand stehen, obwohl der Lauf gerade die Konten anlegte.

/// Fuehrt ein `Cmd` aus und sammelt die abgeschickten Nachrichten ein. Zulaessig nur
/// fuer Kommandos aus `Cmd.ofMsg` / `Cmd.batch` / `Cmd.map` — ein `Cmd.OfAsync` wuerde
/// hier den Fable-only-Proxy anfassen und unter .NET werfen (ADR 0020).
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private erstlaufTests =
    testList "Erster Spiegel-Lauf auf leerem Kassenbuch" [

        test "Direktaufruf der Kassenbuch-Seite laedt Konten und Lauf-Status" {
            // Ursache A: vor dem Fix lief der Direktaufruf ins `Cmd.none` und lud nichts.
            let msgs = dispatched (State.pageLoadCmd Types.Page.Ledger)
            Expect.contains msgs (State.LedgerMsg LoadAccounts) "Konten muessen geladen werden"
            Expect.contains msgs (State.LedgerMsg LoadMirrorStatus) "Lauf-Status muss geladen werden"
        }

        test "Jede datentragende Seite traegt Ladekommandos" {
            // Ursache A war strukturell: die Ladekommandos hingen an einem Zweig, den
            // der Direktaufruf nie erreichte. Sie stehen jetzt in `pageLoadCmd`, und
            // dieser Test faengt, wenn eine Seite dort ihre Kommandos verliert.
            // `Styleguide` ist reine Galerie und laedt bewusst nichts.
            let datentragend =
                [ Types.Page.SyncFlow; Types.Page.Rules; Types.Page.Settings
                  Types.Page.Ledger; Types.Page.QuickAdd ]
            for page in datentragend do
                let msgs = dispatched (State.pageLoadCmd page)
                Expect.isNonEmpty msgs $"%A{page} muss beim Betreten etwas laden"
            Expect.isEmpty (dispatched (State.pageLoadCmd Types.Page.Styleguide))
                "Der Styleguide laedt bewusst nichts"
        }

        test "Ein gelungener Spiegel-Lauf laedt die Konten neu, nicht nur die Buchungen" {
            // Ursache B: vor dem Fix stand hier `LoadEntries`, das ohne gewaehltes Konto
            // still nichts tat — die gerade angelegten Konten wurden nie sichtbar.
            let _, cmd, _ = Components.Ledger.State.update (MirrorFinished (Ok summary)) (freshModel ())
            let msgs = dispatched cmd
            Expect.contains msgs LoadAccounts "Konten muessen nach dem Lauf neu geladen werden"
            Expect.contains msgs LoadMirrorStatus "Lauf-Status muss nach dem Lauf neu geladen werden"
        }

        test "Geladene Konten behalten eine bereits getroffene Auswahl" {
            // Sonst springt die Seite nach jedem Spiegel-Lauf zurueck auf das Standardkonto,
            // waehrend Roman ein anderes Konto ansieht.
            let model = { freshModel () with SelectedAccountId = Some (accountGuid 1) }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) model
            Expect.equal updated.SelectedAccountId (Some (accountGuid 1)) "Die eigene Auswahl ueberlebt"
        }

        test "Verschwindet das gewaehlte Konto, greift wieder die Vorauswahl" {
            let model = { freshModel () with SelectedAccountId = Some (accountGuid 9) }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) model
            Expect.equal updated.SelectedAccountId (Some (accountGuid 2)) "Faellt auf das Standardkonto zurueck"
        }
    ]

[<Tests>]
let ledgerPageTests =
    testList "Kassenbuch-Seite" [ historyLanguageTests; readModelTests; reducerTests; erstlaufTests ]
