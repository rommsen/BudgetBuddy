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
open Ledger.Persistence
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

        test "Eine Kategorie aus dem Order-ID-Abgleich hat ihren eigenen Urheber in der Historie" {
            // Akzeptanz (ledger-qvt84, Nachtrag Roman): eine Buchung, deren Kategorie
            // aus dem Order-ID-Abgleich stammt, belegt diesen Urheber, nicht "Regel"
            // und nicht "Roman in BB".
            Expect.equal (ReadModel.originOf (Actor.OrderIdMatch "order-42")) MatchedByOrderId
                "Order-ID-Abgleich ist eine eigene Herkunft"
            Expect.equal (originLabel MatchedByOrderId) "Order-ID-Abgleich" "Wortlaut aus dem README"

            let line = ReadModel.toHistoryLine (recorded 2 (Actor.OrderIdMatch "order-42") EntryDeletionObservedInYnab)
            Expect.equal line.Origin MatchedByOrderId "die Historien-Zeile zeigt den Order-ID-Urheber"
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

/// Baut die Ok-Antwort einer Seiten-Abfrage, so wie sie `EntriesPageLoaded` bekommt.
let private page (entries: LedgerEntryView list) (totalCount: int) : LedgerEntryPage = {
    Entries = entries
    TotalCount = totalCount
    TotalAmount = ledgerBalance "EUR" entries
}

/// Fuehrt ein `Cmd` aus und sammelt die abgeschickten Nachrichten ein. Zulaessig nur
/// fuer Kommandos aus `Cmd.ofMsg` / `Cmd.batch` / `Cmd.map` — ein `Cmd.OfAsync` wuerde
/// hier den Fable-only-Proxy anfassen und unter .NET werfen (ADR 0020).
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let reducerTests =
    testList "Kassenbuch-Seite: Reduzierer" [

        test "Geladene Konten waehlen das Standardkonto vor" {
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) (freshModel ())
            Expect.equal updated.SelectedFilter (Some (OneAccount (accountGuid 2))) "Die Vorauswahl des Servers gilt"
            Expect.isTrue (RemoteData.isSuccess updated.Accounts) "Konten sind geladen"
        }

        test "Ein leeres Kassenbuch waehlt nichts vor und laedt keine Buchungen" {
            let empty = { Accounts = []; DefaultAccountId = None }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok empty)) (freshModel ())
            Expect.equal updated.SelectedFilter None "Es gibt keinen Filter zu zeigen"
            Expect.equal updated.Entries NotAsked "Ohne Konto gibt es nichts zu laden"
        }

        test "Fehlgeschlagenes Laden der Konten bleibt sichtbar" {
            let updated, _, _ =
                Components.Ledger.State.update (AccountsLoaded (Error "keine Verbindung")) (freshModel ())
            Expect.equal updated.Accounts (Failure "keine Verbindung") "Der Fehler wird nicht geschluckt"
        }

        test "Kontowechsel waehlt das Konto und laedt dessen Buchungen ab Seite 0" {
            let loaded, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (SelectFilter (OneAccount (accountGuid 1))) loaded
            Expect.equal updated.SelectedFilter (Some (OneAccount (accountGuid 1))) "Der gewaehlte Filter"
            Expect.equal updated.Entries Loading "Die Buchungen des neuen Filters werden geholt"
            Expect.equal updated.NextPage 0 "Ein Filterwechsel beginnt wieder bei Seite 0"
        }

        test "Ein Klick auf 'Alle Konten' ist ein eigener Filter, kein Konto" {
            let loaded, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (SelectFilter AllAccounts) loaded
            Expect.equal updated.SelectedFilter (Some AllAccounts) "Gesamtsicht ist ein eigener Filterwert"
        }

        test "Geladene erste Seite ersetzt die Liste und behaelt geloeschte Eintraege" {
            let entries = [ entryView 1 -42.5m false; entryView 2 -10m true ]
            let updated, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Ok (page entries 2))) (freshModel ())
            match updated.Entries with
            | Success loaded ->
                Expect.equal (List.length loaded) 2 "Nichts wird herausgefiltert"
                Expect.isTrue
                    (loaded |> List.exists (fun e -> e.DeletedInYnab))
                    "Die geloeschte Buchung bleibt in der Liste"
            | other -> failtestf "Erwartet Success, war %A" other
            Expect.isFalse updated.HasMoreEntries "Zwei von zwei Treffern: nichts mehr zu laden"
        }

        test "Eine zweite Seite wird an die erste angehaengt, nicht ersetzt" {
            let first, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 3)))
                    (freshModel ())
            let second, _, _ =
                Components.Ledger.State.update (EntriesPageLoaded (1, Ok (page [ entryView 2 -2m false ] 3))) first
            match second.Entries with
            | Success loaded -> Expect.equal (loaded |> List.map (fun e -> e.Id)) [ entryGuid 1; entryGuid 2 ] "Beide Seiten stehen in Ladereihenfolge"
            | other -> failtestf "Erwartet Success, war %A" other
            Expect.isTrue second.HasMoreEntries "Zwei von drei Treffern geladen: es gibt noch mehr"
            Expect.equal second.NextPage 2 "Die naechste Seite waere Seite 2"
        }

        test "Fehlgeschlagene erste Seite wird als Fehler gezeigt" {
            let updated, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Error "kaputt")) (freshModel ())
            Expect.equal updated.Entries (Failure "kaputt") "Der Fehler bleibt sichtbar"
        }

        test "Fehlgeschlagenes Nachladen loescht die sichtbare Liste nicht, sondern zeigt einen Toast" {
            let loaded, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 5)))
                    (freshModel ())
            let updated, _, external = Components.Ledger.State.update (EntriesPageLoaded (1, Error "Netzwerkfehler")) loaded
            Expect.equal updated.Entries loaded.Entries "Die bereits geladene Liste bleibt stehen"
            Expect.isFalse updated.LoadingMore "Das Nachladen ist beendet, auch bei einem Fehler"
            match external with
            | ShowToast (message, ToastError) -> Expect.stringContains message "Netzwerkfehler" "Der Fehler wird genannt"
            | other -> failtestf "Erwartet Fehler-Toast, war %A" other
        }

        test "Nachladen am Listenende laedt die naechste Seite" {
            let loaded, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 5)))
                    (freshModel ())
            let updated, cmd, _ = Components.Ledger.State.update LoadMoreEntries loaded
            Expect.isTrue updated.LoadingMore "Das Nachladen laeuft"
            let msgs = dispatched cmd
            Expect.contains msgs (RunLoadEntriesPage loaded.NextPage) "Die naechste Seite wird angefordert"
        }

        test "Nachladen tut nichts, waehrend schon nachgeladen wird" {
            let loaded, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 5)))
                    (freshModel ())
            let alreadyLoading = { loaded with LoadingMore = true }
            let updated, cmd, _ = Components.Ledger.State.update LoadMoreEntries alreadyLoading
            Expect.equal updated alreadyLoading "Kein zweiter Nachlade-Aufruf waehrend einer laeuft"
            Expect.isEmpty (dispatched cmd) "Kein Kommando"
        }

        test "Nachladen tut nichts, wenn es keine weitere Seite gibt" {
            let loaded, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 1)))
                    (freshModel ())
            Expect.isFalse loaded.HasMoreEntries "Ein Treffer, eine Seite: nichts mehr zu laden"
            let updated, cmd, _ = Components.Ledger.State.update LoadMoreEntries loaded
            Expect.equal updated loaded "Unveraendert"
            Expect.isEmpty (dispatched cmd) "Kein Kommando"
        }

        test "Spiegel-Start setzt den Hinweis, ohne die Liste zu leeren" {
            // ADR 0014: Roman wartet nie auf den Spiegel — die Seite zeigt den
            // bestehenden Stand weiter, waehrend der Lauf laeuft.
            let entries = [ entryView 1 -42.5m false ]
            let loaded, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Ok (page entries 1))) (freshModel ())
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
            Expect.contains msgs LoadBalances "Kontostaende muessen nach dem Lauf neu geladen werden"
            Expect.contains msgs LoadMirrorStatus "Lauf-Status muss nach dem Lauf neu geladen werden"
        }

        test "Geladene Konten behalten eine bereits getroffene Auswahl" {
            // Sonst springt die Seite nach jedem Spiegel-Lauf zurueck auf das Standardkonto,
            // waehrend Roman ein anderes Konto ansieht.
            let model = { freshModel () with SelectedFilter = Some (OneAccount (accountGuid 1)) }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) model
            Expect.equal updated.SelectedFilter (Some (OneAccount (accountGuid 1))) "Die eigene Auswahl ueberlebt"
        }

        test "Geladene Konten behalten die Gesamtsicht als Auswahl" {
            let model = { freshModel () with SelectedFilter = Some AllAccounts }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) model
            Expect.equal updated.SelectedFilter (Some AllAccounts) "Auch die Gesamtsicht ueberlebt ein Neuladen"
        }

        test "Verschwindet das gewaehlte Konto, greift wieder die Vorauswahl" {
            let model = { freshModel () with SelectedFilter = Some (OneAccount (accountGuid 9)) }
            let updated, _, _ = Components.Ledger.State.update (AccountsLoaded (Ok accountsView)) model
            Expect.equal updated.SelectedFilter (Some (OneAccount (accountGuid 2))) "Faellt auf das Standardkonto zurueck"
        }
    ]

// ============================================
// Seitenweise Liste und Kontostaende (ledger-b1xwe): reine Bausteine
// ============================================

let private balanceInputsTests =
    testList "Kontostaende: reine Bausteine" [

        test "sumMoney summiert exakt und uebernimmt die Waehrung der ersten Buchung" {
            let amounts = [ eur -42.5m; eur 100m; eur -7.25m ]
            Expect.equal (sumMoney "EUR" amounts) (eur 50.25m) "Summe wie ledgerBalance, aber ohne Ausschluss"
        }

        test "sumMoney ohne Betraege ist null in der Fallback-Waehrung" {
            Expect.equal (sumMoney "EUR" []) (eur 0m) "Leerer Treffer, kein Fehler"
        }

        test "ledgerBalancesByAccount gruppiert je Konto und schliesst Geloeschte aus" {
            let a1 = accountGuid 1
            let a2 = accountGuid 2
            let inputs =
                [ { AccountId = a1; Amount = eur -42.5m; DeletedInYnab = false }
                  { AccountId = a1; Amount = eur 100m; DeletedInYnab = false }
                  { AccountId = a1; Amount = eur 1000m; DeletedInYnab = true }
                  { AccountId = a2; Amount = eur 5m; DeletedInYnab = false } ]

            let balances = ledgerBalancesByAccount "EUR" inputs
            Expect.equal (Map.find a1 balances) (eur 57.5m) "Konto 1: 100 - 42,50, die geloeschte Buchung zaehlt nicht"
            Expect.equal (Map.find a2 balances) (eur 5m) "Konto 2 unabhaengig von Konto 1"
        }

        test "Der Kontostand ist derselbe, ob man ihn aus zwei oder aus zwanzig Haeppchen zusammensetzt" {
            // AC2: der Kontostand haengt nicht davon ab, wie viele Seiten der
            // Buchungsliste geladen sind. `ledgerBalancesByAccount` bekommt nie eine
            // Teilmenge von der Seiten-Abfrage: das Query-Handler laedt fuer die
            // Kontostaende immer ALLE Betraege (`loadAmountsForBalances`), nicht die
            // gerade sichtbare Seite. Dieser Test zeigt die Kehrseite: eine Teilmenge
            // (wie "nur die erste Seite") ergaebe einen ANDEREN, falschen Stand — das
            // ist der Grund, warum die Kontostaende-Abfrage getrennt von der
            // Seiten-Abfrage laeuft.
            let account = accountGuid 1
            let full =
                List.init 20 (fun i -> { AccountId = account; Amount = eur (decimal (i + 1)); DeletedInYnab = false })
            let firstPageOnly = full |> List.truncate 2

            let fullBalance = ledgerBalancesByAccount "EUR" full |> Map.find account
            let partialBalance = ledgerBalancesByAccount "EUR" firstPageOnly |> Map.find account

            Expect.equal fullBalance (eur 210m) "1+2+...+20"
            Expect.notEqual partialBalance fullBalance
                "Eine Teilmenge waere der falsche Kontostand — deshalb liest die Kontostaende-Abfrage immer alles"
        }
    ]

// ============================================
// Seitenweise Liste und Kontostaende (ledger-b1xwe): Persistence + Query-Handler
// ============================================
// In-Memory-SQLite (Main.fs setzt USE_MEMORY_DB=true vor dem Testlauf). Eigener
// Konten-Namensraum (0195bb00...), damit die Tests unabhaengig vom Rest der
// gemeinsam genutzten In-Memory-DB laufen.

let private pageTestAccountA = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000a")
let private pageTestAccountB = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000b")

let private pageEntryId (n: int) = EntryId (Guid.Parse $"0195bb01-0000-7000-8000-0000000000{n:D2}")

/// Legt eine Buchung per `appendAndApply` an: eine Erstbeobachtung, `n` bestimmt Id,
/// Datum (steigend mit `n`, fuer eine eindeutige Sortierreihenfolge) und Betrag.
let private seedEntry (accountId: LedgerAccountId) (n: int) (amount: decimal) : Async<unit> =
    let payload : EntryObservedInYnabPayload = {
        AccountId = accountId
        YnabTransactionId = $"pagetest-{n}"
        ImportId = None
        Date = DateTime(2026, 4, 1).AddDays(float n)
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = Some $"Payee {n}"
        Memo = None
        Category = None
        Splits = []
        Cleared = Uncleared
        TransferAccountId = None
        TransferYnabTransactionId = None
    }
    appendAndApply [
        { EntryId = pageEntryId n; OccurredAt = DateTime(2026, 4, 1); Actor = YnabMirror; Body = EntryObservedInYnab payload }
    ]

let private seedAccounts () : Async<unit> =
    async {
        do! saveAccount { Id = pageTestAccountA; Name = "Konto A"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountB; Name = "Konto B"; Kind = BankAccount; Link = BbOwned; Closed = false }
    }

[<Tests>]
let ledgerPagingTests =
    initializeLedgerSchema ()
    seedAccounts () |> Async.RunSynchronously

    testSequenced
    <| testList "Kassenbuch-Seite: seitenweise Liste und Kontostaende (Persistence)" [

        testCase "Eine Seite aus einer mehrseitigen Menge traegt die Gesamtsumme, nicht die Seitensumme" <| fun () ->
            // AC1: fuenf Buchungen auf einem Konto, Seitengroesse 2 -> drei Seiten.
            // Jede Seite muss dieselbe Gesamtsumme tragen.
            for n, amount in [ 1, -10m; 2, -20m; 3, -30m; 4, -40m; 5, -50m ] do
                seedEntry pageTestAccountA n amount |> Async.RunSynchronously

            let (LedgerAccountId rawId) = pageTestAccountA
            let query pageIndex = { AccountId = Some rawId; Page = pageIndex; PageSize = 2 }

            let page0 = Ledger.QueryHandlers.getLedgerEntriesPage (query 0) |> Async.RunSynchronously
            let page1 = Ledger.QueryHandlers.getLedgerEntriesPage (query 1) |> Async.RunSynchronously
            let page2 = Ledger.QueryHandlers.getLedgerEntriesPage (query 2) |> Async.RunSynchronously

            Expect.equal page0.TotalCount 5 "Trefferzahl ueber die ganze Menge"
            Expect.equal (List.length page0.Entries) 2 "Erste Seite traegt nur ihre eigenen zwei Buchungen"
            Expect.equal (List.length page2.Entries) 1 "Letzte Seite ist nur teilweise gefuellt"

            let expectedTotal = { Amount = -150m; Currency = "EUR" }
            Expect.equal page0.TotalAmount expectedTotal "Erste Seite traegt die Gesamtsumme"
            Expect.equal page1.TotalAmount expectedTotal "Zweite Seite traegt dieselbe Gesamtsumme"
            Expect.equal page2.TotalAmount expectedTotal "Auch die letzte, nur teilweise gefuellte Seite"
            Expect.notEqual page0.TotalAmount { Amount = -30m; Currency = "EUR" }
                "Die Seitensumme (-10 + -20) waere falsch gewesen"

            // Neueste zuerst: Buchung 5 (spaetestes Datum) steht zuerst auf Seite 0.
            Expect.equal
                (page0.Entries |> List.map (fun e -> e.Payee))
                [ Some "Payee 5"; Some "Payee 4" ]
                "Neueste zuerst"

        testCase "Ohne Konto-Filter zeigt die Seite alle Konten, jede Zeile traegt ihr Konto" <| fun () ->
            seedEntry pageTestAccountB 20 -5m |> Async.RunSynchronously
            seedEntry pageTestAccountB 21 -6m |> Async.RunSynchronously

            let allAccountsPage =
                Ledger.QueryHandlers.getLedgerEntriesPage { AccountId = None; Page = 0; PageSize = 100 }
                |> Async.RunSynchronously

            let (LedgerAccountId rawB) = pageTestAccountB
            Expect.isTrue
                (allAccountsPage.Entries |> List.exists (fun e -> e.AccountId = rawB))
                "Konto B ist in der Gesamtsicht enthalten"
            Expect.isGreaterThanOrEqual allAccountsPage.TotalCount 7
                "Mindestens die 5 Buchungen aus Konto A und die 2 aus Konto B"

        testCase "Kontostaende aller Konten kommen in einem Aufruf und bleiben von Seiten unberuehrt" <| fun () ->
            let balances = Ledger.QueryHandlers.getLedgerAccountBalances () |> Async.RunSynchronously
            let (LedgerAccountId rawA) = pageTestAccountA
            let (LedgerAccountId rawB) = pageTestAccountB

            let balanceA = balances |> List.find (fun b -> b.AccountId = rawA)
            let balanceB = balances |> List.find (fun b -> b.AccountId = rawB)

            Expect.equal balanceA.Balance { Amount = -150m; Currency = "EUR" }
                "Kontostand A ist die Summe aller fuenf Buchungen, nicht nur einer Seite"
            Expect.equal balanceB.Balance { Amount = -11m; Currency = "EUR" } "Kontostand B unabhaengig von A"

            // Derselbe Aufruf noch einmal, ohne dass irgendeine Seite der Liste
            // dazwischen geladen wurde: der Stand aendert sich nicht (AC2).
            let balancesAgain = Ledger.QueryHandlers.getLedgerAccountBalances () |> Async.RunSynchronously
            let balanceAAgain = balancesAgain |> List.find (fun b -> b.AccountId = rawA)
            Expect.equal balanceAAgain.Balance balanceA.Balance "Der Stand ist stabil, unabhaengig vom Seiten-Laden"
    ]

[<Tests>]
let ledgerPageTests =
    testList
        "Kassenbuch-Seite"
        [ historyLanguageTests; readModelTests; balanceInputsTests; reducerTests; erstlaufTests ]
