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
open System.IO
open System.Text.RegularExpressions
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
    TransferAccountId = None
    SplitTransferCounterparts = Map.empty
    DeletedInYnab = false
    Approved = true
    FlagColor = None
    FlagName = None
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
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
    ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab { entry n amount with DeletedInYnab = deleted }

// ============================================
// Reine Abbildung: Ereignis -> Historien-Satz
// ============================================

let historyLanguageTests =
    testList "Historie spricht die Sprache des Kassenbuchs" [

        test "Die Tests laufen ohne Kulturdaten, wie der Server im Container" {
            // Verhindert die Rückkehr des Fehlers vom 2026-10-20: ReadModel lud
            // CultureInfo "de-DE", das im Alpine-Container (kein ICU) nicht existiert.
            // Der Modul-Initialisierer warf, getEntryHistory lieferte bei jeder
            // Buchung HTTP 500. Lokal mit ICU blieben alle Tests grün. Fällt dieser
            // Test, fehlt InvariantGlobalization in Tests.fsproj.
            Expect.throws
                (fun () -> System.Globalization.CultureInfo.GetCultureInfo "de-DE" |> ignore)
                "Ohne Kulturdaten gibt es de-DE nicht, wie im Container"
        }

        test "Beträge stehen mit Tausenderpunkt und Dezimalkomma, ohne Kulturdaten" {
            let body =
                EntryObservedInYnab {
                    AccountId = LedgerAccountId (accountGuid 1)
                    YnabTransactionId = "txn-1"
                    ImportId = None
                    Date = DateTime(2026, 3, 4)
                    Amount = eur -1234.56m
                    Payee = Some "Möbelhaus"
                    Memo = None
                    Category = None
                    Splits = []
                    Cleared = Uncleared
                    TransferAccountId = None
                    TransferYnabTransactionId = None
                    Approved = true
                    FlagColor = None
                    FlagName = None
                }
            Expect.stringContains
                (ReadModel.describe body)
                "-1.234,56 EUR"
                "Deutsche Schreibweise des Betrags bleibt erhalten"
        }

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
                    Approved = true
                    FlagColor = None
                    FlagName = None
                }
            Expect.equal
                (ReadModel.describe body)
                "Erstmals in YNAB gesehen: Rewe, -42,50 EUR, Kategorie Lebensmittel"
                "Die Erstbeobachtung erzaehlt, was gesehen wurde"
        }

        test "Die Historie zeigt den Blattnamen, nie die Kategoriegruppe (ADR 0039, ledger-h6ptn)" {
            // `describe` kennt gar keine Zuordnung Kategorie-Id -> Gruppe (anders als
            // `ReadModel.toEntryView`): die Historie bleibt rein und schlägt nichts
            // nach, auch wenn die Kategorie längst gespiegelt und einer Gruppe
            // zugeordnet ist.
            let body =
                EntryObservedInYnab {
                    AccountId = LedgerAccountId (accountGuid 1)
                    YnabTransactionId = "txn-h6ptn"
                    ImportId = None
                    Date = DateTime(2026, 3, 4)
                    Amount = eur -12.0m
                    Payee = Some "Rewe"
                    Memo = None
                    Category = Some (category 1 "Lebensmittel")
                    Splits = []
                    Cleared = Uncleared
                    TransferAccountId = None
                    TransferYnabTransactionId = None
                    Approved = true
                    FlagColor = None
                    FlagName = None
                }
            let text = ReadModel.describe body
            Expect.stringContains text "Kategorie Lebensmittel" "Der Blattname steht weiterhin in der Historie"
            Expect.isFalse (text.Contains "Alltag") "Keine Gruppe in der Historie, auch nicht als Zufallstreffer"
            Expect.isFalse (text.Contains "Alltag: Lebensmittel") "Keine Qualifizierung 'Gruppe: Name' in der Historie"
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

        test "Gleiche Id, anderer Name, Splits unverändert heißt 'umbenannt', nicht 'geändert'" {
            // Schützt die ehrliche Beschriftung eines vor ADR 0044 erzeugten Umbenennungs-Ereignisses.
            let alt = category 1 "Lebensmittel"
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = Some alt
                    NewCategory = Some { alt with CategoryName = "Essen" }
                    OldSplits = []
                    NewSplits = []
                }
            Expect.equal
                (ReadModel.describe body)
                "Kategorie in YNAB umbenannt: Lebensmittel → Essen"
                "Eine Umbenennung ist keine Umkategorisierung"
        }

        test "Gleiche Kopf-Id, anderer Kopf-Name, geänderte Splits: der Satz nennt nur die Splits" {
            // Verhindert den Bug aus ledger-ggzdw: mit dem alten Namensvergleich im Kopf stand
            // hier zusätzlich "Kategorie in YNAB geändert: Lebensmittel → Essen", obwohl sich
            // die Kopf-Identität (die Id) nicht geändert hat (ADR 0044).
            let alt = category 1 "Lebensmittel"
            let zeile betrag = { Target = ToCategory (alt.CategoryId, "Lebensmittel"); Amount = eur betrag; Memo = None }
            let body =
                EntryCategorizationChangeObservedInYnab {
                    OldCategory = Some alt
                    NewCategory = Some { alt with CategoryName = "Essen" }
                    OldSplits = [ zeile -5m; zeile -5m ]
                    NewSplits = [ zeile -4m; zeile -6m ]
                }
            Expect.equal
                (ReadModel.describe body)
                "Splits in YNAB geändert: 2 → 2 Zeilen"
                "Kein Kopf-Teil, weil die Kategorie-Id gleich blieb"
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

        test "Genehmigungs-Wechsel spricht in ganzen Sätzen, nie den Typnamen" {
            // AC15 (ledger-kdnnz): Wortlaut aus der Aufgabe festgelegt.
            let genehmigt = ReadModel.describe (EntryApprovalChangeObservedInYnab { OldApproved = false; NewApproved = true })
            let zurückgenommen =
                ReadModel.describe (EntryApprovalChangeObservedInYnab { OldApproved = true; NewApproved = false })
            Expect.equal genehmigt "In YNAB genehmigt" "Neu genehmigt"
            Expect.equal zurückgenommen "Genehmigung in YNAB zurückgenommen" "Neu zurückgenommen"
            Expect.isFalse (genehmigt.Contains "EntryApprovalChangeObservedInYnab") "Kein Typname in der Historie"
        }

        test "Flag-Wechsel nennt Lena namentlich, ein anderes Flag nur allgemein" {
            let lena =
                ReadModel.describe (
                    EntryFlagChangeObservedInYnab
                        { OldFlagColor = None; OldFlagName = None; NewFlagColor = Some "purple"; NewFlagName = Some "Lena" })
            let markiert =
                ReadModel.describe (
                    EntryFlagChangeObservedInYnab
                        { OldFlagColor = None; OldFlagName = None; NewFlagColor = Some "red"; NewFlagName = None })
            let entfernt =
                ReadModel.describe (
                    EntryFlagChangeObservedInYnab
                        { OldFlagColor = Some "red"; OldFlagName = None; NewFlagColor = None; NewFlagName = None })
            Expect.equal lena "Flag „Lena“ in YNAB gesetzt" "Lena wird namentlich genannt"
            Expect.equal markiert "Flag in YNAB gesetzt" "Ein anderes Flag bleibt allgemein"
            Expect.equal entfernt "Flag in YNAB entfernt" "Kein Flag mehr"
            Expect.isFalse (lena.Contains "EntryFlagChangeObservedInYnab") "Kein Typname in der Historie"
        }

        test "Loeschung ist ein Satz, die Buchung bleibt" {
            Expect.equal
                (ReadModel.describe EntryDeletionObservedInYnab)
                "In YNAB gelöscht"
                "Geloescht heisst gekennzeichnet, nicht verschwunden"
        }

        test "Uebertrags-Verknuepfung spricht ohne Kontonamen, kein Typname" {
            // ledger-t7v2q, Offener Punkt 3: die Payload traegt keinen Kontonamen der
            // Gegenbuchung, der Satz bleibt deshalb allgemein.
            let text =
                ReadModel.describe (
                    EntryTransferCounterpartObservedInYnab
                        { CounterpartEntryId = EntryId (entryGuid 2)
                          CounterpartYnabTransactionId = "txn-gegen" })
            Expect.equal text "Als Übertrag mit einer anderen Buchung verbunden" "Wortlaut ohne Kontonamen"
            Expect.isFalse
                (text.Contains "EntryTransferCounterpartObservedInYnab")
                "Kein Typname in der Historie"
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
            let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab (entry 1 -42.5m)
            Expect.equal view.Id (entryGuid 1) "Die eigene Buchungs-Id bleibt"
            Expect.equal view.CategoryName (Some "Lebensmittel") "Der Name, nicht die Id"
            Expect.equal view.Origin MirroredFromYnab "Die Herkunft wird durchgereicht"
            Expect.isFalse view.DeletedInYnab "Nicht geloescht"
            Expect.isTrue view.NeedsReview "Cleared = Uncleared, also zu prüfen (ADR 0032)"
            Expect.equal view.Flag NoFlag "Kein Flag gesetzt"
        }

        // ============================================
        // NeedsReview und Flag-Deutung (ADR 0032, ledger-kdnnz)
        // ============================================

        test "NeedsReview ist wahr genau bei approved = false oder cleared = Uncleared" {
            // AC12: alle Kombinationen, Reconciled zählt wie Cleared als geprüft.
            let reviewed = entry 1 -1m
            Expect.isFalse
                (needsReview { reviewed with Approved = true; Cleared = Cleared })
                "genehmigt und bestätigt: geprüft"
            Expect.isFalse
                (needsReview { reviewed with Approved = true; Cleared = Reconciled })
                "genehmigt und abgeglichen: geprüft"
            Expect.isTrue
                (needsReview { reviewed with Approved = true; Cleared = Uncleared })
                "genehmigt, aber noch offen: zu prüfen"
            Expect.isTrue
                (needsReview { reviewed with Approved = false; Cleared = Cleared })
                "nicht genehmigt, obwohl bestätigt: zu prüfen"
            Expect.isTrue
                (needsReview { reviewed with Approved = false; Cleared = Uncleared })
                "beides offen: zu prüfen"
            Expect.isTrue
                (needsReview { reviewed with Approved = false; Cleared = Reconciled })
                "nicht genehmigt, obwohl abgeglichen: zu prüfen"
        }

        test "Flag-Deutung: kein Flag, Lena (case-insensitiv, getrimmt), sonst Markiert" {
            // AC10.
            Expect.equal (ReadModel.flagOf None None) NoFlag "kein Flag gesetzt"
            Expect.equal (ReadModel.flagOf (Some "purple") (Some "Lena")) AskLena "genauer Name"
            Expect.equal (ReadModel.flagOf (Some "purple") (Some "lena")) AskLena "Klein geschrieben"
            Expect.equal (ReadModel.flagOf (Some "purple") (Some " Lena ")) AskLena "mit Leerzeichen"
            Expect.equal (ReadModel.flagOf (Some "red") (Some "Temporär")) Marked "ein anderer Name"
            Expect.equal (ReadModel.flagOf (Some "red") None) Marked "nur eine Farbe, kein Name"
            Expect.equal (ReadModel.flagOf None (Some "Lena's Kram")) Marked "kein exakter Treffer auf 'Lena'"
        }

        test "Geloeschte Buchung bleibt sichtbar und traegt das Kennzeichen" {
            let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab { entry 1 -42.5m with DeletedInYnab = true }
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
    DeletedCount = entries |> List.filter (fun e -> e.DeletedInYnab) |> List.length
    NeedsReviewCount = entries |> List.filter (fun e -> e.NeedsReview) |> List.length
    AskLenaCount = 0
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

        // ============================================
        // Summenzeile (ledger-j8xq4 AC): Summe über den GANZEN Treffer, auch wenn
        // nur eine Seite davon geladen ist
        // ============================================

        test "TotalAmount übernimmt die vom Server gemeldete Summe über den GANZEN Treffer, nicht die Summe der geladenen Seite (AC, mehr Treffer als eine Seite)" {
            // Die Fixture tut so, als gäbe es 250 Treffer mit einer Gesamtsumme von
            // -5000 EUR, aber nur eine Seite (eine einzelne Buchung über -1 EUR) ist
            // tatsächlich geladen: genau der Fall, den `entryPage.TotalAmount` lösen
            // soll (`Ledger.Persistence`, ADR 0017: die Summe rechnet der Server über
            // den gesamten Treffer, nie über den geladenen Ausschnitt).
            let serverPage = { page [ entryView 1 -1m false ] 250 with TotalAmount = eur -5000m }
            let updated, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Ok serverPage)) (freshModel ())
            Expect.equal updated.TotalAmount (eur -5000m) "Die Summenzeile zeigt die Summe über alle 250 Treffer, nicht über die eine geladene Buchung"
            Expect.equal updated.TotalCount 250 "Die Trefferzahl ist ebenfalls die des ganzen Treffers"
        }

        test "Ein Filterwechsel setzt TotalAmount vorübergehend auf Null zurück, bis die neue Seite antwortet" {
            let withTotal, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok { page [ entryView 1 -1m false ] 250 with TotalAmount = eur -5000m }))
                    (freshModel ())
            let reset, _, _ = Components.Ledger.State.update (SelectFilter AllAccounts) withTotal
            Expect.equal reset.TotalAmount.Amount 0m "Kein stehender alter Wert, während die neue Summe noch nicht da ist"
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

        // ============================================
        // Gelöschte Buchungen ausblenden (ledger-p3n8k)
        // ============================================

        test "Geladene Seite übernimmt die vom Server gemeldete Zahl gelöschter Buchungen" {
            // Der Reduzierer rechnet DeletedCount nicht selbst nach (das könnte er
            // auf einer Seite ohnehin nicht, wenn gelöschte Buchungen ausgeblendet
            // sind); er übernimmt genau den Server-Wert (AC2).
            let serverPage = { page [ entryView 1 -1m false ] 1 with DeletedCount = 3 }
            let updated, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Ok serverPage)) (freshModel ())
            Expect.equal updated.DeletedCount 3 "Der Server-Wert steht im Modell"
        }

        test "Der Schalter kippt IncludeDeleted und lädt die Liste ab Seite 0 neu" {
            let withEntries, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 5)))
                    (freshModel ())
            Expect.isFalse withEntries.IncludeDeleted "Standardmäßig ausgeblendet"

            let updated, cmd, _ = Components.Ledger.State.update ToggleIncludeDeleted withEntries
            Expect.isTrue updated.IncludeDeleted "Ein Klick blendet gelöschte Buchungen ein"
            Expect.equal updated.Entries Loading "Die Liste wird neu geholt"
            Expect.equal updated.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Das Neuladen wird angestoßen"

            let toggledBack, _, _ = Components.Ledger.State.update ToggleIncludeDeleted updated
            Expect.isFalse toggledBack.IncludeDeleted "Ein zweiter Klick blendet wieder aus"
        }

        // ============================================
        // Chip "Zu prüfen (n)" (ADR 0032, ledger-kdnnz)
        // ============================================

        test "Geladene Seite übernimmt die vom Server gemeldete Zahl zu prüfender Buchungen" {
            let serverPage = { page [ entryView 1 -1m false ] 1 with NeedsReviewCount = 5 }
            let updated, _, _ = Components.Ledger.State.update (EntriesPageLoaded (0, Ok serverPage)) (freshModel ())
            Expect.equal updated.NeedsReviewCount 5 "Der Server-Wert steht im Modell"
        }

        test "Der Chip kippt OnlyNeedsReview und lädt die Liste ab Seite 0 neu (AC16)" {
            let withEntries, _, _ =
                Components.Ledger.State.update
                    (EntriesPageLoaded (0, Ok (page [ entryView 1 -1m false ] 5)))
                    (freshModel ())
            Expect.isFalse withEntries.OnlyNeedsReview "Standardmäßig aus"

            let updated, cmd, _ = Components.Ledger.State.update ToggleOnlyNeedsReview withEntries
            Expect.isTrue updated.OnlyNeedsReview "Ein Antippen filtert auf ungeprüfte Buchungen"
            Expect.equal updated.Entries Loading "Die Liste wird neu geholt"
            Expect.equal updated.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Das Neuladen wird angestoßen"

            let toggledBack, _, _ = Components.Ledger.State.update ToggleOnlyNeedsReview updated
            Expect.isFalse toggledBack.OnlyNeedsReview "Erneutes Antippen hebt den Filter auf"
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

        test "Detail öffnet sich für die angetippte Buchung" {
            let view = entryView 1 -42.5m false
            let updated, _, _ = Components.Ledger.State.update (OpenDetail view) (freshModel ())
            Expect.equal (updated.DetailFor |> Option.map (fun e -> e.Id)) (Some (entryGuid 1)) "Die richtige Buchung"
            Expect.equal updated.History Loading "Die Historie wird geholt"
        }

        test "Detail schließt sich wieder" {
            let view = entryView 1 -42.5m false
            let opened, _, _ = Components.Ledger.State.update (OpenDetail view) (freshModel ())
            let closed, _, _ = Components.Ledger.State.update CloseDetail opened
            Expect.equal closed.DetailFor None "Kein Detail mehr offen"
        }

        test "Geladene Historie steht in gespeicherter Reihenfolge" {
            let lines =
                [ recorded 1 Actor.YnabMirror EntryDeletionObservedInYnab
                  recorded 2 Actor.YnabMirror EntryDeletionObservedInYnab ]
                |> List.map ReadModel.toHistoryLine
            let opened, _, _ = Components.Ledger.State.update (OpenDetail (entryView 1 -42.5m false)) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (HistoryLoaded (entryGuid 1, Ok lines)) opened
            match updated.History with
            | Success loaded -> Expect.equal (loaded |> List.map (fun l -> l.Sequence)) [ 1; 2 ] "Reihenfolge bleibt"
            | other -> failtestf "Erwartet Success, war %A" other
        }

        // ============================================
        // Frische-Kriterium (ADR 0014 Punkt 1, ledger-h4p6c)
        // ============================================

        test "Ein zu alter Lauf-Status löst genau einen Hintergrund-Lauf aus" {
            // `DateTime.UtcNow` statt eines festen Datums: der Reduzierer trägt die
            // Uhr selbst (kein injizierter Clock im Client, anders als serverseitig bei
            // `runMirror`), darum muss der Test-Zeitpunkt real "jetzt" sein.
            let staleStatus : MirrorRunStatus = {
                RanAt = DateTime.UtcNow - TimeSpan.FromMinutes 6.0
                Outcome = MirrorRunSucceeded summary
            }
            let entries = [ entryView 1 -42.5m false ]
            let withEntries, _, _ =
                Components.Ledger.State.update (EntriesPageLoaded (0, Ok (page entries 1))) (freshModel ())
            let updated, cmd, _ = Components.Ledger.State.update (MirrorStatusLoaded (Ok (Some staleStatus))) withEntries
            Expect.isTrue updated.MirrorRunning "der Hintergrund-Lauf ist gestartet"
            Expect.contains (dispatched cmd) RunMirror "genau ein Auslöser für den Hintergrund-Lauf"
            Expect.equal updated.Entries (Success entries) "die Liste bleibt stehen, während der Hintergrund-Lauf läuft (ADR 0014)"
        }

        test "Ein jüngerer Lauf-Status löst keinen Hintergrund-Lauf aus" {
            let freshStatus : MirrorRunStatus = {
                RanAt = DateTime.UtcNow - TimeSpan.FromMinutes 1.0
                Outcome = MirrorRunSucceeded summary
            }
            let updated, cmd, _ =
                Components.Ledger.State.update (MirrorStatusLoaded (Ok (Some freshStatus))) (freshModel ())
            Expect.isFalse updated.MirrorRunning "kein Hintergrund-Lauf nötig"
            Expect.isEmpty (dispatched cmd) "kein Kommando"
        }

        test "Noch nie ein Lauf-Status löst einen Hintergrund-Lauf aus" {
            let updated, cmd, _ = Components.Ledger.State.update (MirrorStatusLoaded (Ok None)) (freshModel ())
            Expect.isTrue updated.MirrorRunning "der erste Lauf überhaupt zählt als 'nicht frisch'"
            Expect.contains (dispatched cmd) RunMirror "genau ein Auslöser"
        }

        test "Ein bereits laufender Spiegel wird nicht ein zweites Mal angestoßen" {
            // Die client-seitige Hälfte von "zwei gleichzeitige Auslöser ergeben nur
            // einen Lauf" (ADR 0014 Punkt 3): läuft im selben Tab schon ein Lauf, tut
            // ein zweiter, spät eintreffender Lauf-Status nichts. Die serverseitige
            // Hälfte (zwei Tabs, Lauf nach Push) sitzt im Einzel-Flug-Schutz von
            // `Ledger.CommandHandlers.runMirrorGuarded` (`LedgerTests`/`LedgerMirrorTests`).
            let staleStatus : MirrorRunStatus = {
                RanAt = DateTime.UtcNow - TimeSpan.FromHours 1.0
                Outcome = MirrorRunSucceeded summary
            }
            let running = { freshModel () with MirrorRunning = true }
            let updated, cmd, _ = Components.Ledger.State.update (MirrorStatusLoaded (Ok (Some staleStatus))) running
            Expect.isTrue updated.MirrorRunning "der laufende Lauf bleibt unverändert stehen"
            Expect.isEmpty (dispatched cmd) "kein zweiter Auslöser während eines laufenden Spiegels"
        }

        // ============================================
        // Filterleiste: Kategorie-Mehrfachauswahl, Click-Commit (ledger-j8xq4)
        // ============================================

        test "Ein Antippen im Picker ändert nur den Entwurf, nicht den Filter (Click-Commit, ADR 0005)" {
            let catA = accountGuid 91
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) (freshModel ())
            let toggled, _, _ = Components.Ledger.State.update (ToggleCategoryDraft catA) opened
            Expect.equal toggled.CategoryDraft (Set.ofList [ catA ]) "Der Entwurf trägt die Auswahl"
            Expect.isTrue (Set.isEmpty toggled.Filters.CategoryIds) "Der committete Filter bleibt leer, bevor 'Übernehmen' gedrückt wird"
        }

        test "'Übernehmen' schreibt den Entwurf in den Filter, schließt das Popover und lädt ab Seite 0 neu" {
            let catA = accountGuid 91
            let catB = accountGuid 92
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) (freshModel ())
            let toggledA, _, _ = Components.Ledger.State.update (ToggleCategoryDraft catA) opened
            let toggledB, _, _ = Components.Ledger.State.update (ToggleCategoryDraft catB) toggledA
            let applied, cmd, _ = Components.Ledger.State.update ApplyCategoryFilter toggledB
            Expect.equal applied.Filters.CategoryIds (Set.ofList [ catA; catB ]) "Beide Kategorien stehen jetzt im committeten Filter (Vereinigung)"
            Expect.equal applied.OpenPopover None "Das Popover schließt sich"
            Expect.equal applied.NextPage 0 "Ein Filterwechsel beginnt wieder bei Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Das Neuladen wird angestoßen"
        }

        test "Esc/Schließen verwirft den Entwurf, der vorherige Filterzustand bleibt unverändert" {
            let catA = accountGuid 91
            let withFilter = { (freshModel ()) with Filters = { noFilters with CategoryIds = Set.ofList [ catA ] } }
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) withFilter
            let toggled, _, _ = Components.Ledger.State.update (ToggleCategoryDraft (accountGuid 92)) opened
            let closed, _, _ = Components.Ledger.State.update CloseFilterPopover toggled
            Expect.equal closed.Filters withFilter.Filters "Der committete Filter ist unverändert, der verworfene Entwurf zählt nicht"
            Expect.equal closed.OpenPopover None "Das Popover ist zu"
        }

        test "Ein neues Öffnen holt den Entwurf wieder aus dem committeten Filter (kein Rest vom letzten Mal)" {
            let catA = accountGuid 91
            let withFilter = { (freshModel ()) with Filters = { noFilters with CategoryIds = Set.ofList [ catA ] } }
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) withFilter
            Expect.equal opened.CategoryDraft (Set.ofList [ catA ]) "Der Entwurf startet mit dem committeten Stand"
        }

        test "'Zurücksetzen' leert den Entwurf, ohne den committeten Filter anzufassen" {
            let catA = accountGuid 91
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) (freshModel ())
            let toggled, _, _ = Components.Ledger.State.update (ToggleCategoryDraft catA) opened
            let reset, _, _ = Components.Ledger.State.update ResetCategoryDraft toggled
            Expect.isTrue (Set.isEmpty reset.CategoryDraft) "Entwurf ist leer"
        }

        test "'×' am Kategorie-Chip entfernt genau diesen Filter" {
            let catA = accountGuid 91
            let withFilters =
                { (freshModel ()) with
                    Filters = { noFilters with CategoryIds = Set.ofList [ catA ]; Payee = Some "Café Lotte" } }
            let updated, cmd, _ = Components.Ledger.State.update RemoveCategoryFilter withFilters
            Expect.isTrue (Set.isEmpty updated.Filters.CategoryIds) "Kategorie ist weg"
            Expect.equal updated.Filters.Payee (Some "Café Lotte") "Der Payee-Filter bleibt unberührt stehen"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        test "Versteckte Kategorien lassen sich auf-/zuklappen, unabhängig vom Entwurf" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover CategoryChip) (freshModel ())
            Expect.isFalse opened.HiddenCategoriesExpanded "Standardmäßig eingeklappt"
            let expanded, _, _ = Components.Ledger.State.update ToggleHiddenCategoriesExpanded opened
            Expect.isTrue expanded.HiddenCategoriesExpanded "Ein Antippen klappt auf"
        }

        test "OpenFilterPopover mit dem Chip Ohne Kategorie wirft nicht und startet keinen Ladevorgang" {
            // Verhindert die Regression: Der Match über ChipId deckte WithoutCategoryChip nicht ab
            // (FS0025), die Nachricht hätte zur Laufzeit eine MatchFailureException ausgelöst.
            let before = freshModel ()
            let opened, cmd, _ = Components.Ledger.State.update (OpenFilterPopover WithoutCategoryChip) before
            Expect.equal opened.OpenPopover (Some WithoutCategoryChip) "Der Chip gilt als geöffnet"
            Expect.equal opened.Filters before.Filters "Der Filterzustand bleibt unverändert"
            Expect.isEmpty cmd "Kein Cmd (kein Api-Aufruf)"
        }

        // ============================================
        // Filterleiste: Zeitraum-Schnellwahl schreibt Von/Bis mit (ledger-j8xq4)
        // ============================================

        test "Eine Schnellwahl trägt Von und Bis sichtbar in den Entwurf ein" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (SetDateQuick Last12Months) opened
            Expect.equal updated.DateDraft.Quick (Some Last12Months) "Die Schnellwahl steht im Entwurf"
            Expect.isTrue updated.DateDraft.From.IsSome "Von ist gesetzt"
            Expect.isTrue updated.DateDraft.To.IsSome "Bis ist gesetzt"
        }

        test "Von/Bis von Hand setzen löscht die Schnellwahl-Markierung, ohne das andere Feld zu leeren" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let withQuick, _, _ = Components.Ledger.State.update (SetDateQuick ThisYear) opened
            let handSet, _, _ = Components.Ledger.State.update (SetDateDraftFrom (Client.DesignSystem.DateField.ValidDate (DateTime(2026, 3, 1)))) withQuick
            Expect.equal handSet.DateDraft.Quick None "Die Schnellwahl gilt nicht mehr als Quelle"
            Expect.equal handSet.DateDraft.From (Some (DateTime(2026, 3, 1))) "Der neue Wert steht"
            Expect.isTrue handSet.DateDraft.To.IsSome "Bis bleibt stehen, wie es die Schnellwahl gesetzt hatte"
        }

        // design-system-m7z9q: ungültige Eingabe sperrt Übernehmen
        test "InvalidDate in Von setzt den Fehlerzustand und lässt den Wert stehen" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let updated, _, _ = Components.Ledger.State.update (SetDateDraftFrom (Client.DesignSystem.DateField.InvalidDate "31.02.2026")) opened
            Expect.isTrue updated.DateDraftFromInvalid "Von ist ungültig"
            Expect.isFalse updated.DateDraftToInvalid "Bis ist es nicht"
            Expect.isTrue (dateDraftHasError updated) "Der Entwurf hat einen Fehler"
        }

        test "InvalidDate in Bis setzt den Fehlerzustand; ein gültiger Wert hebt ihn wieder auf" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let bad, _, _ = Components.Ledger.State.update (SetDateDraftTo (Client.DesignSystem.DateField.InvalidDate "07.1")) opened
            Expect.isTrue bad.DateDraftToInvalid "Bis ist ungültig"
            let good, _, _ = Components.Ledger.State.update (SetDateDraftTo (Client.DesignSystem.DateField.ValidDate (DateTime(2026, 10, 7)))) bad
            Expect.isFalse good.DateDraftToInvalid "Fehler aufgehoben"
            Expect.equal good.DateDraft.To (Some (DateTime(2026, 10, 7))) "Der Wert steht"
            let cleared, _, _ = Components.Ledger.State.update (SetDateDraftTo (Client.DesignSystem.DateField.InvalidDate "x")) good
            let empty, _, _ = Components.Ledger.State.update (SetDateDraftTo Client.DesignSystem.DateField.EmptyDate) cleared
            Expect.isFalse empty.DateDraftToInvalid "Leeren hebt den Fehler auf"
            Expect.equal empty.DateDraft.To None "Bis ist leer"
        }

        test "ApplyDateFilter ändert den Filter nicht, solange eine Grenze ungültig ist" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let bad, _, _ = Components.Ledger.State.update (SetDateDraftFrom (Client.DesignSystem.DateField.InvalidDate "31.02.2026")) opened
            let applied, cmd, _ = Components.Ledger.State.update ApplyDateFilter bad
            Expect.equal applied.Filters.DateRange None "Filter unverändert"
            Expect.equal applied.OpenPopover (Some DateChip) "Das Popover bleibt offen"
            Expect.isEmpty (dispatched cmd) "Kein Nachladen"
        }

        test "SetDateQuick und ResetDateDraft heben den Fehlerzustand auf" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let bad, _, _ = Components.Ledger.State.update (SetDateDraftFrom (Client.DesignSystem.DateField.InvalidDate "x")) opened
            let bad2, _, _ = Components.Ledger.State.update (SetDateDraftTo (Client.DesignSystem.DateField.InvalidDate "y")) bad
            let quick, _, _ = Components.Ledger.State.update (SetDateQuick ThisMonth) bad2
            Expect.isFalse (dateDraftHasError quick) "Schnellwahl hebt beide auf"
            let reset, _, _ = Components.Ledger.State.update ResetDateDraft bad2
            Expect.isFalse (dateDraftHasError reset) "Zurücksetzen hebt beide auf"
        }

        test "Das Öffnen des Zeitraum-Popovers startet ohne Fehlerzustand" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let bad, _, _ = Components.Ledger.State.update (SetDateDraftFrom (Client.DesignSystem.DateField.InvalidDate "x")) opened
            let reopened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) bad
            Expect.isFalse (dateDraftHasError reopened) "Neu geöffnet, ohne Fehler"
        }

        test "'Übernehmen' beim Zeitraum committet den Entwurf und lädt ab Seite 0 neu" {
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) (freshModel ())
            let withQuick, _, _ = Components.Ledger.State.update (SetDateQuick ThisMonth) opened
            let applied, cmd, _ = Components.Ledger.State.update ApplyDateFilter withQuick
            Expect.equal applied.Filters.DateRange (Some withQuick.DateDraft) "Der Entwurf steht jetzt im Filter"
            Expect.equal applied.OpenPopover None "Das Popover schließt sich"
            Expect.equal applied.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        test "Esc beim Zeitraum-Popover lässt den vorherigen Filter unverändert" {
            let withFilter =
                { (freshModel ()) with
                    Filters = { noFilters with DateRange = Some { Quick = Some ThisYear; From = Some DateTime.Today; To = Some DateTime.Today } } }
            let opened, _, _ = Components.Ledger.State.update (OpenFilterPopover DateChip) withFilter
            let changed, _, _ = Components.Ledger.State.update (SetDateQuick Last12Months) opened
            let closed, _, _ = Components.Ledger.State.update CloseFilterPopover changed
            Expect.equal closed.Filters.DateRange withFilter.Filters.DateRange "Unverändert, der Entwurf zählt nicht"
        }

        test "'×' am Zeitraum-Chip entfernt genau den Zeitraum-Filter" {
            let withFilters =
                { (freshModel ()) with
                    Filters =
                        { noFilters with
                            DateRange = Some { Quick = Some ThisYear; From = Some DateTime.Today; To = Some DateTime.Today }
                            Memo = Some "Kuchen" } }
            let updated, cmd, _ = Components.Ledger.State.update RemoveDateFilter withFilters
            Expect.equal updated.Filters.DateRange None "Zeitraum ist weg"
            Expect.equal updated.Filters.Memo (Some "Kuchen") "Memo bleibt unberührt"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        // ============================================
        // Filterleiste: Payee (Einzelauswahl, filtert sofort) (ledger-j8xq4)
        // ============================================

        test "Ein Klick auf einen Payee-Vorschlag filtert sofort, ohne 'Übernehmen'" {
            let updated, cmd, _ = Components.Ledger.State.update (SelectPayeeFilter "Café Lotte") (freshModel ())
            Expect.equal updated.Filters.Payee (Some "Café Lotte") "Der Payee-Filter steht sofort"
            Expect.equal updated.OpenPopover None "Das Popover schließt sich"
            Expect.equal updated.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        test "'×' am Payee-Chip entfernt den Payee-Filter" {
            let withFilter = { (freshModel ()) with Filters = { noFilters with Payee = Some "Café Lotte" } }
            let updated, _, _ = Components.Ledger.State.update RemovePayeeFilter withFilter
            Expect.equal updated.Filters.Payee None "Payee ist weg"
        }

        test "Geladene Payee-Vorschläge stehen im Modell" {
            let suggestions = [ { Payee = "Café Lotte"; Count = 9 }; { Payee = "REWE Mitte"; Count = 78 } ]
            let updated, _, _ = Components.Ledger.State.update (PayeeSuggestionsLoaded (Ok suggestions)) (freshModel ())
            match updated.PayeeSuggestions with
            | Success loaded -> Expect.equal loaded suggestions "Die Vorschläge stehen unverändert im Modell"
            | other -> failtestf "Erwartet Success, war %A" other
        }

        // ============================================
        // Filterleiste: Memo- und Betrags-Suchfeld mit Verzögerung (ledger-j8xq4)
        // ============================================

        test "Eine Memo-Eingabe erhöht die Version; ein verspätetes Commit einer älteren Version wird verworfen" {
            let typing, _, _ = Components.Ledger.State.update (SetMemoDraft "Kuchen") (freshModel ())
            Expect.equal typing.MemoDraft "Kuchen" "Der getippte Text steht im Entwurf"
            Expect.equal typing.MemoVersion 1 "Erste Eingabe: Version 1"

            // Ein Commit mit einer älteren Version (0) kommt zu spät und darf nichts tun.
            let stale, _, _ = Components.Ledger.State.update (CommitMemoFilter ("Kuc", 0)) typing
            Expect.equal stale.Filters.Memo None "Das verspätete Commit wird ignoriert"

            let committed, cmd, _ = Components.Ledger.State.update (CommitMemoFilter ("Kuchen", 1)) typing
            Expect.equal committed.Filters.Memo (Some "Kuchen") "Die aktuelle Version committet"
            Expect.equal committed.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        test "Ein leeres Memo-Commit löscht den Filter statt eines leeren Strings" {
            let withFilter = { (freshModel ()) with Filters = { noFilters with Memo = Some "Kuchen" }; MemoVersion = 1 }
            let updated, _, _ = Components.Ledger.State.update (CommitMemoFilter ("   ", 1)) withFilter
            Expect.equal updated.Filters.Memo None "Leer (nach Trim) heißt: kein Filter"
        }

        test "'×' am Memo-Chip entfernt den Memo-Filter und leert das Eingabefeld" {
            let withFilter = { (freshModel ()) with Filters = { noFilters with Memo = Some "Kuchen" }; MemoDraft = "Kuchen" }
            let updated, _, _ = Components.Ledger.State.update RemoveMemoFilter withFilter
            Expect.equal updated.Filters.Memo None "Filter ist weg"
            Expect.equal updated.MemoDraft "" "Eingabefeld ist auch leer"
        }

        test "Betrags-Commit mit aktueller Version setzt Von und Bis" {
            let typing, _, _ = Components.Ledger.State.update (SetAmountMinDraft "5") (freshModel ())
            Expect.equal typing.AmountVersion 1 "Erste Eingabe: Version 1"
            let committed, cmd, _ = Components.Ledger.State.update (CommitAmountFilter (Some 5m, None, 1)) typing
            Expect.equal committed.Filters.AmountMin (Some 5m) "Von steht"
            Expect.equal committed.Filters.AmountMax None "Bis bleibt offen"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"

        }

        test "Ein verspätetes Betrags-Commit einer älteren Version wird verworfen" {
            let first, _, _ = Components.Ledger.State.update (SetAmountMinDraft "5") (freshModel ())
            let second, _, _ = Components.Ledger.State.update (SetAmountMaxDraft "50") first
            Expect.equal second.AmountVersion 2 "Zwei Eingaben, zweite Version"
            let stale, _, _ = Components.Ledger.State.update (CommitAmountFilter (Some 5m, None, 1)) second
            Expect.equal stale.Filters.AmountMin None "Das veraltete Commit (Version 1) wird ignoriert"
        }

        test "'×' am Betrags-Chip entfernt Von und Bis und leert beide Eingabefelder" {
            let withFilter =
                { (freshModel ()) with
                    Filters = { noFilters with AmountMin = Some 5m; AmountMax = Some 50m }
                    AmountMinDraft = "5"
                    AmountMaxDraft = "50" }
            let updated, _, _ = Components.Ledger.State.update RemoveAmountFilter withFilter
            Expect.equal updated.Filters.AmountMin None "Von ist weg"
            Expect.equal updated.Filters.AmountMax None "Bis ist weg"
            Expect.equal updated.AmountMinDraft "" "Eingabefeld Von ist leer"
            Expect.equal updated.AmountMaxDraft "" "Eingabefeld Bis ist leer"
        }

        // ============================================
        // Filterleiste: "Alle Filter löschen" (ledger-j8xq4)
        // ============================================

        test "'Alle Filter löschen' setzt alle fünf Filter zurück, lässt 'Zu prüfen' und 'Gelöschte zeigen' unberührt" {
            let withEverything =
                { (freshModel ()) with
                    Filters =
                        { CategoryIds = Set.ofList [ accountGuid 91 ]
                          DateRange = Some { Quick = Some ThisYear; From = Some DateTime.Today; To = Some DateTime.Today }
                          Payee = Some "Café Lotte"
                          Memo = Some "Kuchen"
                          AmountMin = Some 5m
                          AmountMax = Some 50m
                          WithoutCategory = false
                          OnlyAskLena = false }
                    OnlyNeedsReview = true
                    IncludeDeleted = true }
            let updated, cmd, _ = Components.Ledger.State.update ClearAllFilters withEverything
            Expect.equal updated.Filters noFilters "Alle fünf Such-Filter sind weg"
            Expect.isTrue updated.OnlyNeedsReview "'Zu prüfen' ist kein Such-Filter, bleibt stehen"
            Expect.isTrue updated.IncludeDeleted "'Gelöschte zeigen' ist kein Such-Filter, bleibt stehen"
            Expect.equal updated.NextPage 0 "Wieder ab Seite 0"
            Expect.contains (dispatched cmd) LoadEntries "Neu laden"
        }

        // ============================================
        // Filterleiste: geladene YNAB-Kategorien (ledger-j8xq4 AC5)
        // ============================================

        test "Geladene Kategorien stehen im Modell, inklusive Available und Hidden" {
            let hidden : YnabCategory =
                { Id = catId 9; Name = "Umzug 2023"; GroupName = "Alltag"; Available = None; Hidden = true }
            let visible : YnabCategory =
                { Id = catId 1; Name = "Lebensmittel"; GroupName = "Alltag"; Available = Some (eur -12.5m); Hidden = false }
            let updated, _, _ = Components.Ledger.State.update (CategoriesLoaded (Ok [ hidden; visible ])) (freshModel ())
            match updated.Categories with
            | Success cats ->
                Expect.isTrue (cats |> List.find (fun c -> c.Name = "Umzug 2023") |> fun c -> c.Hidden) "Versteckt bleibt versteckt"
                Expect.isFalse (cats |> List.find (fun c -> c.Name = "Lebensmittel") |> fun c -> c.Hidden) "Sichtbare Kategorie bleibt sichtbar"
            | other -> failtestf "Erwartet Success, war %A" other
        }
    ]

// ============================================
// Frische-Kriterium: die reine Entscheidung (`isMirrorStale`), unabhängig vom
// Reduzierer testbar (ledger-h4p6c)
// ============================================

let private freshnessTests =
    testList "Frische-Kriterium: isMirrorStale" [

        test "Genau an der Schwelle gilt noch als frisch" {
            let now = DateTime(2026, 4, 1, 12, 5, 0, DateTimeKind.Utc)
            let lastRun : MirrorRunStatus = {
                RanAt = DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
                Outcome = MirrorRunSucceeded summary
            }
            Expect.isFalse (isMirrorStale now (Some lastRun)) "genau 5 Minuten sind noch nicht 'älter als'"
        }

        test "Eine Sekunde über der Schwelle gilt als nicht mehr frisch" {
            let now = DateTime(2026, 4, 1, 12, 5, 1, DateTimeKind.Utc)
            let lastRun : MirrorRunStatus = {
                RanAt = DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
                Outcome = MirrorRunSucceeded summary
            }
            Expect.isTrue (isMirrorStale now (Some lastRun)) "eine Sekunde drüber ist 'älter als'"
        }

        test "Ein gescheiterter Lauf zählt trotzdem als Lauf-Zeitpunkt" {
            // Ein Fehler wird sichtbar gemacht, aber löst keinen Dauer-Retry-Loop aus:
            // der Zeitpunkt des gescheiterten Versuchs gilt für die nächste
            // Frische-Prüfung genauso wie ein erfolgreicher.
            let now = DateTime(2026, 4, 1, 12, 1, 0, DateTimeKind.Utc)
            let lastRun : MirrorRunStatus = {
                RanAt = DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)
                Outcome = MirrorRunFailed "YNAB-Limit erreicht"
            }
            Expect.isFalse (isMirrorStale now (Some lastRun)) "eine Minute nach einem gescheiterten Lauf ist noch frisch"
        }

        test "Ohne jeden Lauf gilt sofort als nicht frisch" {
            Expect.isTrue (isMirrorStale DateTime.UtcNow None) "kein Lauf ist per Definition 'nicht frisch'"
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
// PWA-Vordergrund (ADR 0014 Punkt 1, ledger-h4p6c): verhält sich wie ein
// Seitenaufruf, aber nur auf der Kassenbuch-Seite
// ============================================

/// Ein Root-Modell für den Reduzierer-Test, ohne `State.init()`: das ruft
/// `Routing.currentPage()` auf, was unter .NET an `Browser.Dom.window` scheitert (Fable-
/// only, wie der Fable.Remoting-Proxy, den die Kopfnotiz dieser Datei schon nennt). Die
/// Kind-Modelle allein sind reine Konstruktion, deshalb ungefährlich.
let private rootModel (page: Types.Page) : State.Model =
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
        Budget = Components.Budget.State.init ()
        QuickAdd = None
        QuickAddTemplates = NotAsked
    }

let private visibilityTests =
    testList "PWA-Vordergrund (VisibilityChanged)" [

        test "Auf der Kassenbuch-Seite lädt der Vordergrund-Wechsel den Lauf-Status neu" {
            let updated, cmd = State.update State.VisibilityChanged (rootModel Types.Page.Ledger)
            Expect.equal updated.CurrentPage Types.Page.Ledger "die Seite selbst ändert sich nicht"
            Expect.contains (dispatched cmd) (State.LedgerMsg LoadMirrorStatus) "der Lauf-Status wird neu geladen"
        }

        test "Auf jeder anderen Seite tut der Vordergrund-Wechsel nichts" {
            let onSettings = rootModel Types.Page.Settings
            let updated, cmd = State.update State.VisibilityChanged onSettings
            Expect.equal updated onSettings "kein Zustandswechsel"
            Expect.isEmpty (dispatched cmd) "kein Kommando außerhalb des Kassenbuchs"
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
let private pageTestAccountC = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000c")
let private pageTestAccountD = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000d")
let private pageTestAccountE = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000e")
let private pageTestAccountF = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000000f")
let private pageTestAccountG = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000010")
// Konten H..L (ledger-vgfw3): eigene Konten je Filter-Testgruppe, damit die Fünf-
// Filter-Tests nicht von der Saat der obigen Gruppen abhängen.
let private pageTestAccountH = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000011")
let private pageTestAccountI = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000012")
let private pageTestAccountJ = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000013")
let private pageTestAccountK = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000014")
let private pageTestAccountL = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000015")
let private pageTestAccountM = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000016")
let private pageTestAccountN = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000017")
let private pageTestAccountO = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000018")
// Eigenes Konto für den Umlaut-Faltungstest (ledger-k3wqn, ADR 0037): unabhängig
// von Konto M (AC6), damit die dort gesäten ASCII-Payees den Umlaut-Fall nicht
// verdecken.
let private pageTestAccountP = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000019")
// Konten Q..V (ledger-c4mzp): Betragsfilter-Zweig (`hasAmountFilter`) des Lesepfads,
// eigenes Konto je Testfall. `...019` (P) ist von ledger-k3wqn belegt, deshalb ab
// `...01a`, nicht ab `...019` wie im ursprünglichen Task-Text.
let private pageTestAccountQ = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001a")
let private pageTestAccountR = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001b")
let private pageTestAccountS = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001c")
let private pageTestAccountT = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001d")
let private pageTestAccountU = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001e")
let private pageTestAccountV = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000001f")
// Konten W.. (ledger-xk9wq, ADR 0038): Split-Zeilen-Kategorien im Kategorie-Filter,
// eigenes Konto je Testfall, fortlaufend hinter V.
let private pageTestAccountW = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000020")
let private pageTestAccountX = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000021")
let private pageTestAccountY = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000022")
let private pageTestAccountZ = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000023")
let private pageTestAccountAA = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000024")
let private pageTestAccountAB = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000025")
let private pageTestAccountAC = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000026")
let private pageTestAccountAD = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000027")
let private pageTestAccountAE = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000028")
let private pageTestAccountAF = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-000000000029")
// Konten AG..AJ (ledger-h4qtx): Payee-Vorschläge falten wie der Payee-Filter.
let private pageTestAccountAG = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000002a")
let private pageTestAccountAH = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000002b")
let private pageTestAccountAI = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000002c")
let private pageTestAccountAJ = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000002d")
// Konto AK (ledger-h6ptn, ADR 0039): Kategorie voll qualifizieren, eigenes Konto
// für die Gruppennamen-Lookup-Tests, damit sie von keinem der obigen Konten
// gespeiste Kategorien "sehen".
let private pageTestAccountAK = LedgerAccountId (Guid.Parse "0195bb00-0000-7000-8000-00000000002e")

// `D3` statt `D2`, ein Prefix-Nullen weniger (`000000000` statt `0000000000`): für
// n=0..99 liefert das exakt dieselbe Guid wie zuvor (`n:D3` mit vier statt drei
// Ziffern Nullpadding hat denselben Text), n=100 (ledger-k3wqn) passt jetzt auch.
let private pageEntryId (n: int) = EntryId (Guid.Parse $"0195bb01-0000-7000-8000-000000000{n:D3}")

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
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [
        { EntryId = pageEntryId n; OccurredAt = DateTime(2026, 4, 1); Actor = YnabMirror; Body = EntryObservedInYnab payload }
    ]

/// Wie `seedEntry`, aber die Buchung ist in YNAB gelöscht (ledger-p3n8k): erst die
/// Erstbeobachtung, danach `EntryDeletionObservedInYnab` in derselben Sequenz.
let private seedDeletedEntry (accountId: LedgerAccountId) (n: int) (amount: decimal) : Async<unit> =
    async {
        do! seedEntry accountId n amount
        do! appendAndApply [
            { EntryId = pageEntryId n
              OccurredAt = DateTime(2026, 4, 1)
              Actor = YnabMirror
              Body = EntryDeletionObservedInYnab }
        ]
    }

/// Wie `seedEntry`, aber bereits geprüft: `Approved = true` und `Cleared = Cleared`
/// (ADR 0032), damit `NeedsReview` für diese Buchung falsch ist.
let private seedReviewedEntry (accountId: LedgerAccountId) (n: int) (amount: decimal) : Async<unit> =
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
        Cleared = Cleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [
        { EntryId = pageEntryId n; OccurredAt = DateTime(2026, 4, 1); Actor = YnabMirror; Body = EntryObservedInYnab payload }
    ]

// ledger-fgt4k: eigenes Konto und eigene Buchungs-Ids (Präfix f6f6f6f6), damit
// parallele Änderungen an dieser Datei nicht kollidieren.
let private pageTestAccountFgt = LedgerAccountId (Guid.Parse "f6f6f6f6-0000-7000-8000-000000000001")
let private pageEntryIdFgt (n: int) = EntryId (Guid.Parse $"f6f6f6f6-0000-7000-8000-0000000001{n:D2}")

/// Legt eine Buchung für `pageTestAccountFgt` an. `reviewed = true` heißt
/// `Cleared = Cleared` (NeedsReview falsch), sonst `Uncleared`. `deleted = true`
/// folgt danach mit `EntryDeletionObservedInYnab`.
let private seedEntryFgt (n: int) (amount: decimal) (reviewed: bool) (deleted: bool) : Async<unit> =
    async {
        let payload : EntryObservedInYnabPayload = {
            AccountId = pageTestAccountFgt
            YnabTransactionId = $"pagetest-fgt-{n}"
            ImportId = None
            Date = DateTime(2026, 4, 1).AddDays(float n)
            Amount = { Amount = amount; Currency = "EUR" }
            Payee = Some $"Payee Fgt {n}"
            Memo = None
            Category = None
            Splits = []
            Cleared = (if reviewed then Cleared else Uncleared)
            TransferAccountId = None
            TransferYnabTransactionId = None
            Approved = true
            FlagColor = None
            FlagName = None
        }
        do! appendAndApply [
            { EntryId = pageEntryIdFgt n; OccurredAt = DateTime(2026, 4, 1); Actor = YnabMirror; Body = EntryObservedInYnab payload }
        ]
        if deleted then
            do! appendAndApply [
                { EntryId = pageEntryIdFgt n; OccurredAt = DateTime(2026, 4, 1); Actor = YnabMirror; Body = EntryDeletionObservedInYnab }
            ]
    }

let private seedAccounts () : Async<unit> =
    async {
        do! saveAccount { Id = pageTestAccountA; Name = "Konto A"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountB; Name = "Konto B"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountC; Name = "Konto C"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountD; Name = "Konto D"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountE; Name = "Konto E"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountF; Name = "Konto F"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountG; Name = "Konto G"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountH; Name = "Konto H"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountI; Name = "Konto I"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountJ; Name = "Konto J"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountK; Name = "Konto K"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountL; Name = "Konto L"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountM; Name = "Konto M"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountN; Name = "Konto N"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountO; Name = "Konto O"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountP; Name = "Konto P"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountQ; Name = "Konto Q"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountR; Name = "Konto R"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountS; Name = "Konto S"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountT; Name = "Konto T"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountU; Name = "Konto U"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountV; Name = "Konto V"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountW; Name = "Konto W"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountX; Name = "Konto X"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountY; Name = "Konto Y"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountZ; Name = "Konto Z"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAA; Name = "Konto AA"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAB; Name = "Konto AB"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAC; Name = "Konto AC"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAD; Name = "Konto AD"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAE; Name = "Konto AE"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAF; Name = "Konto AF"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAG; Name = "Konto AG"; Kind = BankAccount; Link = BbOwned; Closed = false }
        do! saveAccount { Id = pageTestAccountAK; Name = "Konto AK"; Kind = BankAccount; Link = BbOwned; Closed = false }
    }

/// Wie `seedEntry`, aber mit voller Kontrolle ueber Payee, Memo, Kategorie und Datum
/// (ledger-vgfw3): die Fünf-Filter-Tests brauchen mehr als nur Betrag und laufenden
/// Index.
let private seedEntryFull
    (accountId: LedgerAccountId)
    (n: int)
    (amount: decimal)
    (date: DateTime)
    (payee: string option)
    (memo: string option)
    (categoryRef: CategoryRef option)
    : Async<unit> =
    let payload : EntryObservedInYnabPayload = {
        AccountId = accountId
        YnabTransactionId = $"pagetest-{n}"
        ImportId = None
        Date = date
        Amount = { Amount = amount; Currency = "EUR" }
        Payee = payee
        Memo = memo
        Category = categoryRef
        Splits = []
        Cleared = Uncleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [
        { EntryId = pageEntryId n; OccurredAt = date; Actor = YnabMirror; Body = EntryObservedInYnab payload }
    ]

/// Wie `seedEntryFull`, aber in YNAB gelöscht (analog `seedDeletedEntry`).
let private seedDeletedEntryFull
    (accountId: LedgerAccountId)
    (n: int)
    (amount: decimal)
    (date: DateTime)
    (payee: string option)
    (memo: string option)
    (categoryRef: CategoryRef option)
    : Async<unit> =
    async {
        do! seedEntryFull accountId n amount date payee memo categoryRef
        do! appendAndApply [
            { EntryId = pageEntryId n; OccurredAt = date; Actor = YnabMirror; Body = EntryDeletionObservedInYnab }
        ]
    }

/// Wie `seedEntryFull`, aber mit Splits statt einer einfachen Kategorie
/// (ledger-xk9wq, ADR 0038): `category_id` der Buchung bleibt `NULL`, jede Kategorie
/// steckt in einer Split-Zeile. Der Gesamtbetrag ergibt sich aus der Summe der
/// Split-Zeilen (wie `mkSplits`, ADR 0006, es verlangt), auch wenn `apply` das für
/// Beobachtungs-Ereignisse nicht selbst prüft: realistische Testdaten sollen trotzdem
/// eine gültige Split-Summe tragen, statt sich auf einen ungeprüften Zufallswert zu
/// verlassen.
let private seedSplitEntry
    (accountId: LedgerAccountId)
    (n: int)
    (date: DateTime)
    (payee: string option)
    (memo: string option)
    (splits: TransactionSplit list)
    : Async<unit> =
    let total = splits |> List.sumBy (fun s -> s.Amount.Amount)
    let payload : EntryObservedInYnabPayload = {
        AccountId = accountId
        YnabTransactionId = $"pagetest-{n}"
        ImportId = None
        Date = date
        Amount = { Amount = total; Currency = "EUR" }
        Payee = payee
        Memo = memo
        Category = None
        Splits = splits
        Cleared = Uncleared
        TransferAccountId = None
        TransferYnabTransactionId = None
        Approved = true
        FlagColor = None
        FlagName = None
    }
    appendAndApply [
        { EntryId = pageEntryId n; OccurredAt = date; Actor = YnabMirror; Body = EntryObservedInYnab payload }
    ]

/// Wie `seedSplitEntry`, aber in YNAB gelöscht (analog `seedDeletedEntryFull`).
let private seedDeletedSplitEntry
    (accountId: LedgerAccountId)
    (n: int)
    (date: DateTime)
    (payee: string option)
    (memo: string option)
    (splits: TransactionSplit list)
    : Async<unit> =
    async {
        do! seedSplitEntry accountId n date payee memo splits
        do! appendAndApply [
            { EntryId = pageEntryId n; OccurredAt = date; Actor = YnabMirror; Body = EntryDeletionObservedInYnab }
        ]
    }

/// Der rohe `Guid` einer Test-Kategorie, fuer `LedgerEntryPageQuery.CategoryIds`.
let private catGuid (n: int) = let (YnabCategoryId g) = catId n in g

/// Eine `LedgerEntryPageQuery` ohne gesetzte Such-Filter (ledger-vgfw3): die
/// Rückwärtskompatibilitäts-Basislinie, mit der jeder neue Test seine eigenen Felder
/// per `with` überschreibt, statt alle zwölf Felder auszuschreiben.
let private baseQuery (accountId: Guid option) : LedgerEntryPageQuery = {
    AccountId = accountId
    Page = 0
    PageSize = 100
    LocallyRemoved = 0
    IncludeDeleted = false
    OnlyNeedsReview = false
    OnlyAskLena = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Payee = None
    Memo = None
    AmountMin = None
    AmountMax = None
    Sort = LedgerSort.defaultSort
}

/// Wie `baseQuery`, aber für `getLedgerPayeeSuggestions`: keine Seiten-Parameter,
/// kein `Payee` (der ist ja gerade das, was gleich ausgewählt wird).
let private baseSuggestionQuery (accountId: Guid option) : LedgerPayeeSuggestionQuery = {
    AccountId = accountId
    IncludeDeleted = false
    OnlyNeedsReview = false
    OnlyAskLena = false
    WithoutCategory = false
    CategoryIds = []
    DateFrom = None
    DateTo = None
    Memo = None
    AmountMin = None
    AmountMax = None
}

[<Tests>]
let ledgerPagingTests =
    // initializeLedgerSchema()/seedAccounts() laufen erst, wenn ein Testfall
    // tatsächlich ausgeführt wird, nicht schon beim Modul-Laden (ADR 0030, infra-sdad8).
    // initializeLedgerSchema() ist idempotent (CREATE TABLE IF NOT EXISTS); seedAccounts()
    // schreibt dieselben Konten per Id, ein wiederholter Aufruf pro Testfall überschreibt
    // nur mit denselben Werten.
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            seedAccounts () |> Async.RunSynchronously
            body ())

    testSequenced
    <| testList "Kassenbuch-Seite: seitenweise Liste und Kontostaende (Persistence)" [

        testCase "Eine Seite aus einer mehrseitigen Menge traegt die Gesamtsumme, nicht die Seitensumme" <| fun () ->
            // AC1: fuenf Buchungen auf einem Konto, Seitengroesse 2 -> drei Seiten.
            // Jede Seite muss dieselbe Gesamtsumme tragen.
            for n, amount in [ 1, -10m; 2, -20m; 3, -30m; 4, -40m; 5, -50m ] do
                seedEntry pageTestAccountA n amount |> Async.RunSynchronously

            let (LedgerAccountId rawId) = pageTestAccountA
            let query pageIndex = { baseQuery (Some rawId) with Page = pageIndex; PageSize = 2 }

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
                Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery None)
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

        // ============================================
        // Gelöschte Buchungen ausblenden (ledger-p3n8k)
        // ============================================

        testCase "Ohne IncludeDeleted bleibt eine gelöschte Buchung aus Seite, Trefferzahl und Summe außen vor" <| fun () ->
            // AC1: die sichtbare Liste (und ihre Summe) enthält standardmäßig keine
            // gelöschten Buchungen mehr. Eigenes Konto C, unabhängig von den anderen
            // beiden Tests dieser Gruppe.
            seedEntry pageTestAccountC 30 -10m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountC 31 -1000m |> Async.RunSynchronously

            let (LedgerAccountId rawC) = pageTestAccountC
            let hidden =
                Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawC))
                |> Async.RunSynchronously

            Expect.equal hidden.TotalCount 1 "Die gelöschte Buchung zählt nicht zum Treffer"
            Expect.equal (List.length hidden.Entries) 1 "Sie steht auch nicht auf der Seite"
            Expect.isFalse (hidden.Entries |> List.exists (fun e -> e.DeletedInYnab)) "Keine gelöschte Buchung sichtbar"
            Expect.equal hidden.TotalAmount { Amount = -10m; Currency = "EUR" }
                "Die Summe entspricht der lebenden Buchung, nicht der gelöschten"

        testCase "IncludeDeleted=true zeigt die gelöschte Buchung mit, in Seite, Trefferzahl und Summe" <| fun () ->
            // Pinnt Entscheidung Roman 2026-09-18 (`ledger-d4kx8`, Variante 2): eingeblendet
            // zählen gelöschte Buchungen in Trefferzahl UND Summe. Der Entwurf hatte das
            // anders vorgesehen, der gebaute Stand (dieser Test) bleibt.
            // Eigenes Konto D, damit dieser Test nicht von der Saat des vorigen Tests
            // abhängt.
            seedEntry pageTestAccountD 32 -20m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountD 33 -2000m |> Async.RunSynchronously

            let (LedgerAccountId rawD) = pageTestAccountD
            let shown =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawD) with IncludeDeleted = true }
                |> Async.RunSynchronously

            Expect.equal shown.TotalCount 2 "Beide Buchungen zählen zum Treffer"
            Expect.equal (List.length shown.Entries) 2 "Beide stehen auf der Seite"
            Expect.isTrue (shown.Entries |> List.exists (fun e -> e.DeletedInYnab)) "Die gelöschte Buchung ist dabei"
            Expect.equal shown.TotalAmount { Amount = -2020m; Currency = "EUR" }
                "Eingeblendet zählt die gelöschte Buchung in der Summe mit"

        testCase "DeletedCount zählt die gelöschten Buchungen unabhängig vom Schalter" <| fun () ->
            // AC2: die Seite soll die Anzahl auch nennen können, während sie
            // ausgeblendet sind. Eigenes Konto E, damit dieser Test nicht von der
            // Saat der beiden vorigen Tests abhängt.
            seedEntry pageTestAccountE 34 -30m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountE 35 -3000m |> Async.RunSynchronously

            let (LedgerAccountId rawE) = pageTestAccountE
            let hidden =
                Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawE))
                |> Async.RunSynchronously
            let shown =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawE) with IncludeDeleted = true }
                |> Async.RunSynchronously

            Expect.equal hidden.DeletedCount 1 "Eine gelöschte Buchung, obwohl ausgeblendet"
            Expect.equal shown.DeletedCount 1 "Dieselbe Zahl, auch wenn eingeblendet"

        testCase "Summenzeile und Kontostand laufen bei eingeschaltetem Schalter bewusst auseinander" <| fun () ->
            // AC5 (Entscheidung Roman 2026-09-18, `ledger-d4kx8`): die Summenzeile
            // (`getLedgerEntriesPage`) zählt eingeblendete gelöschte Buchungen mit, der
            // Kontostand (`getLedgerAccountBalances` / `ledgerBalancesByAccount`) lässt
            // sie immer draußen. Beide Werte weichen dann absichtlich voneinander ab,
            // das ist kein stiller Widerspruch mehr, sondern die getroffene Wahl.
            // Eigenes Konto G, unabhängig von den übrigen Tests dieser Gruppe.
            seedEntry pageTestAccountG 40 -20m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountG 41 -2000m |> Async.RunSynchronously

            let (LedgerAccountId rawG) = pageTestAccountG
            let shown =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawG) with IncludeDeleted = true }
                |> Async.RunSynchronously
            let balances = Ledger.QueryHandlers.getLedgerAccountBalances () |> Async.RunSynchronously
            let balanceG = balances |> List.find (fun b -> b.AccountId = rawG)

            Expect.equal shown.TotalAmount { Amount = -2020m; Currency = "EUR" }
                "Summenzeile zählt die gelöschte Buchung mit"
            Expect.equal balanceG.Balance { Amount = -20m; Currency = "EUR" }
                "Kontostand lässt die gelöschte Buchung weiterhin draußen"
            Expect.notEqual shown.TotalAmount balanceG.Balance
                "Die Abweichung ist gewollt, nicht ein stiller Fehler"

        // ============================================
        // Chip "Zu prüfen (n)" (ADR 0032, ledger-kdnnz)
        // ============================================

        testCase "OnlyNeedsReview liefert nur ungeprüfte, nicht gelöschte Buchungen samt Summe" <| fun () ->
            // AC13: eine geprüfte (36), eine ungeprüfte (37) und eine gelöschte,
            // ansonsten ungeprüfte (38) Buchung. Eigenes Konto F.
            seedReviewedEntry pageTestAccountF 36 -10m |> Async.RunSynchronously
            seedEntry pageTestAccountF 37 -20m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountF 38 -3000m |> Async.RunSynchronously

            let (LedgerAccountId rawF) = pageTestAccountF
            let onlyNeedsReview =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawF) with OnlyNeedsReview = true }
                |> Async.RunSynchronously

            Expect.equal onlyNeedsReview.TotalCount 1 "Nur die ungeprüfte, nicht gelöschte Buchung zählt"
            Expect.equal (List.length onlyNeedsReview.Entries) 1 "Sie steht auch als einzige auf der Seite"
            Expect.equal
                (onlyNeedsReview.Entries |> List.map (fun e -> e.Payee))
                [ Some "Payee 37" ]
                "Die geprüfte und die gelöschte Buchung bleiben außen vor"
            Expect.equal onlyNeedsReview.TotalAmount { Amount = -20m; Currency = "EUR" }
                "Die Summe entspricht nur dem Treffer"

        testCase "NeedsReviewCount folgt dem Kontofilter, zählt unabhängig vom Schalter und nie Gelöschte" <| fun () ->
            // AC14.
            let (LedgerAccountId rawF) = pageTestAccountF
            let off =
                Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawF))
                |> Async.RunSynchronously
            let on =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawF) with OnlyNeedsReview = true }
                |> Async.RunSynchronously

            Expect.equal off.NeedsReviewCount 1 "Eine zu prüfende Buchung, obwohl der Filter aus ist"
            Expect.equal on.NeedsReviewCount 1 "Dieselbe Zahl, auch wenn der Filter an ist"

        testCase "DeletedCount zählt bei OnlyNeedsReview nur gelöschte Buchungen, die der Filter zeigen würde" <| fun () ->
            // ledger-fgt4k: countDeletedEntries erzwang OnlyNeedsReview = false. Eine
            // gelöschte, bereits geprüfte Buchung zählte so in DeletedCount mit, obwohl
            // sie bei OnlyNeedsReview = true nie unter Entries erscheinen kann.
            seedEntryFgt 1 -10m false false |> Async.RunSynchronously
            seedEntryFgt 2 -3000m true true |> Async.RunSynchronously

            let (LedgerAccountId rawFgt) = pageTestAccountFgt
            let page =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawFgt) with OnlyNeedsReview = true; IncludeDeleted = true }
                |> Async.RunSynchronously

            Expect.equal page.DeletedCount 0 "Die gelöschte Buchung ist geprüft und gehört nicht in den aktiven Filter"
            Expect.isFalse (page.Entries |> List.exists (fun e -> e.DeletedInYnab)) "Entries zeigt keine gelöschte Buchung"
            Expect.equal (List.length page.Entries) 1 "Nur die ungeprüfte Buchung steht auf der Seite"

        // ============================================
        // Reihenfolge der Historie (ledger-h9tq2, Entscheidung Roman 2026-09-18)
        // ============================================

        testCase "getEntryHistory bleibt aufsteigend, älteste Sequenznummer zuerst" <| fun () ->
            // Die Anzeige dreht die Zeilen (neueste zuerst), aber nur im Client
            // (`Components.Ledger.View.historyLinesForDisplay`). `Ledger.Persistence`
            // behält `ORDER BY sequence`, `Ledger.QueryHandlers.getEntryHistory` gibt
            // die Zeilen unverändert weiter. Dieser Test hält das fest, damit die
            // Drehung nicht später in die Abfrage abwandert.
            seedEntry pageTestAccountA 39 -5m |> Async.RunSynchronously
            appendAndApply [
                { EntryId = pageEntryId 39
                  OccurredAt = DateTime(2026, 4, 2)
                  Actor = YnabMirror
                  Body = EntryDeletionObservedInYnab }
            ]
            |> Async.RunSynchronously

            let (EntryId rawId) = pageEntryId 39
            let history = Ledger.QueryHandlers.getEntryHistory rawId |> Async.RunSynchronously
            Expect.equal
                (history |> List.map (fun l -> l.Sequence))
                [ 1; 2 ]
                "aufsteigend, älteste Sequenznummer zuerst. Die Abfrage dreht nicht"

        // ============================================
        // Fünf Such-Filter im Lesepfad (ledger-vgfw3, ADR 0036)
        // ============================================

        testCase "AC1: Ohne gesetzte Such-Filter bleibt das Ergebnis unveraendert" <| fun () ->
            seedEntryFull pageTestAccountH 50 -10m (DateTime(2026, 5, 1)) (Some "Rewe") (Some "Einkauf") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountH 51 -20m (DateTime(2026, 5, 2)) (Some "Aldi") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountH 52 -30m (DateTime(2026, 5, 3)) None (Some "Bar") None
            |> Async.RunSynchronously

            let (LedgerAccountId rawH) = pageTestAccountH
            let page = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawH)) |> Async.RunSynchronously

            Expect.equal page.TotalCount 3 "Alle drei Buchungen, kein Filter grenzt ein"
            Expect.equal (List.length page.Entries) 3 "Alle drei stehen auf der Seite"
            Expect.equal page.TotalAmount { Amount = -60m; Currency = "EUR" } "Summe ueber alle drei"

        testCase "AC2: Kategorie-Filter mit zwei Kategorien liefert die Vereinigung, nicht den Schnitt" <| fun () ->
            seedEntryFull pageTestAccountI 53 -1m (DateTime(2026, 5, 1)) (Some "Payee 53") None (Some (category 21 "Kategorie A"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountI 54 -2m (DateTime(2026, 5, 2)) (Some "Payee 54") None (Some (category 22 "Kategorie B"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountI 55 -3m (DateTime(2026, 5, 3)) (Some "Payee 55") None (Some (category 23 "Kategorie C"))
            |> Async.RunSynchronously

            let (LedgerAccountId rawI) = pageTestAccountI
            let query = { baseQuery (Some rawI) with CategoryIds = [ catGuid 21; catGuid 22 ] }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            Expect.equal page.TotalCount 2 "Die Vereinigung beider Kategorien, nicht nur eine"
            Expect.equal
                (page.Entries |> List.map (fun e -> e.Payee) |> List.sort)
                [ Some "Payee 53"; Some "Payee 54" ]
                "Kategorie C bleibt aussen vor, Kategorie A und B sind beide dabei"

        testCase "AC3: Trefferzahl und Summe eines Kategorie-Filters gelten ueber den gesamten Treffer, nicht die Seite" <| fun () ->
            for n, amount in [ 56, -10m; 57, -20m; 58, -30m; 59, -40m; 60, -50m ] do
                seedEntryFull pageTestAccountJ n amount (DateTime(2026, 5, 1).AddDays(float n)) None None (Some (category 24 "Kategorie D"))
                |> Async.RunSynchronously

            let (LedgerAccountId rawJ) = pageTestAccountJ
            let query = { baseQuery (Some rawJ) with PageSize = 2; CategoryIds = [ catGuid 24 ] }
            let page0 = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            let page2 = Ledger.QueryHandlers.getLedgerEntriesPage { query with Page = 2 } |> Async.RunSynchronously

            Expect.equal page0.TotalCount 5 "Trefferzahl ueber alle fuenf gefilterten Buchungen"
            Expect.equal (List.length page0.Entries) 2 "Erste Seite traegt nur ihre eigenen zwei Buchungen"
            Expect.equal (List.length page2.Entries) 1 "Letzte Seite ist nur teilweise gefuellt"
            let expectedTotal = { Amount = -150m; Currency = "EUR" }
            Expect.equal page0.TotalAmount expectedTotal "Erste Seite traegt die Gesamtsumme des Filters"
            Expect.equal page2.TotalAmount expectedTotal "Auch die letzte Seite traegt dieselbe Gesamtsumme"

        testCase "AC4: Die Summe eines aktiven Filters wird exakt in F# gebildet, nicht per SQL-SUM" <| fun () ->
            // ADR 0017/0036: zehn mal 0,10 EUR ergeben in `decimal` genau 1,00 — ueber
            // `REAL`/`CAST(amount AS REAL)` waere das Ergebnis 0,9999999999999999.
            for n in 61 .. 70 do
                seedEntryFull pageTestAccountK n 0.1m (DateTime(2026, 5, 1).AddDays(float n)) None None (Some (category 25 "Kategorie E"))
                |> Async.RunSynchronously

            let (LedgerAccountId rawK) = pageTestAccountK
            let query = { baseQuery (Some rawK) with CategoryIds = [ catGuid 25 ] }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            Expect.equal page.TotalCount 10 "Alle zehn Buchungen treffen den Filter"
            Expect.equal page.TotalAmount { Amount = 1.0m; Currency = "EUR" } "Genau 1,00, nicht 0,9999999999999999"

        testCase "AC5: Zeitraum filtert einschliesslich der Randtage, auch bei gemischtem DateTimeKind wie im echten Bestand" <| fun () ->
            let von = DateTime(2026, 6, 10)
            let bis = DateTime(2026, 6, 12)
            seedEntryFull pageTestAccountL 71 -1m (von.AddDays(-1.0)) (Some "Vor dem Zeitraum") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountL 72 -2m von (Some "Genau am Von-Tag (ohne Kind)") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountL 73 -3m (DateTime(2026, 6, 11)) (Some "Mitten im Zeitraum") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountL 74 -4m bis (Some "Genau am Bis-Tag (ohne Kind)") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountL 75 -5m (bis.AddDays(1.0)) (Some "Nach dem Zeitraum") None None
            |> Async.RunSynchronously
            // Wie der Spiegel es wirklich schreibt (`YnabObservations.fs`:
            // `DateTime.SpecifyKind(date, DateTimeKind.Utc)`, `Persistence.fs`:
            // `dateToText entry.Date` haengt dadurch ein "Z" an). Der reale Bestand
            // besteht ueberwiegend aus genau solchen Zeilen; ohne den
            // Kind-unabhaengigen Vergleich (`substr(date, 1, 10)`) fallen sie am
            // Rand still raus (Verifier-Befund, Iteration 1).
            seedEntryFull
                pageTestAccountL
                94
                -6m
                (DateTime.SpecifyKind(von, DateTimeKind.Utc))
                (Some "Genau am Von-Tag (Utc, wie der Spiegel)")
                None
                None
            |> Async.RunSynchronously
            seedEntryFull
                pageTestAccountL
                95
                -7m
                (DateTime.SpecifyKind(bis, DateTimeKind.Utc))
                (Some "Genau am Bis-Tag (Utc, wie der Spiegel)")
                None
                None
            |> Async.RunSynchronously

            let (LedgerAccountId rawL) = pageTestAccountL
            let query = { baseQuery (Some rawL) with DateFrom = Some von; DateTo = Some bis }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            Expect.equal
                page.TotalCount
                5
                "Von-Tag, Mitte und Bis-Tag zaehlen (je zweimal fuer Von/Bis: ohne Kind und Utc wie der Spiegel), die beiden Aussenseiter nicht"
            Expect.equal
                (page.Entries |> List.map (fun e -> e.Payee) |> List.sort)
                [ Some "Genau am Bis-Tag (Utc, wie der Spiegel)"
                  Some "Genau am Bis-Tag (ohne Kind)"
                  Some "Genau am Von-Tag (Utc, wie der Spiegel)"
                  Some "Genau am Von-Tag (ohne Kind)"
                  Some "Mitten im Zeitraum" ]
                "Beide Randtage sind eingeschlossen, unabhaengig vom DateTimeKind der gespeicherten Buchung"

        testCase "AC6: Payee-, Memo- und Betragsfilter wirken einzeln und als Und-Verknuepfung kombiniert" <| fun () ->
            seedEntryFull pageTestAccountM 76 -4.50m (DateTime(2026, 5, 1)) (Some "Cafe Lotte") (Some "Kaffee") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountM 77 -6.00m (DateTime(2026, 5, 2)) (Some "Cafe Lotte") (Some "Kuchen") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountM 78 -4.50m (DateTime(2026, 5, 3)) (Some "Rewe") (Some "Kaffee") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountM 79 -32.10m (DateTime(2026, 5, 4)) (Some "Rewe") (Some "Einkauf") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountM 80 -4.50m (DateTime(2026, 5, 5)) (Some "Aldi") (Some "Kaffee to go") None
            |> Async.RunSynchronously

            let (LedgerAccountId rawM) = pageTestAccountM
            let byPayee =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawM) with Payee = Some "cafe lotte" }
                |> Async.RunSynchronously
            let byMemo =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawM) with Memo = Some "kaffee" }
                |> Async.RunSynchronously
            let byAmount =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawM) with AmountMin = Some -4.50m; AmountMax = Some -4.50m }
                |> Async.RunSynchronously
            let combined =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawM) with
                        Payee = Some "cafe lotte"
                        Memo = Some "kaffee"
                        AmountMin = Some -4.50m
                        AmountMax = Some -4.50m }
                |> Async.RunSynchronously

            Expect.equal byPayee.TotalCount 2 "Payee (ohne Beachtung der Gross-/Kleinschreibung) trifft beide Cafe-Lotte-Buchungen"
            Expect.equal byMemo.TotalCount 3 "Memo-Teiltext 'kaffee' trifft drei Buchungen, auch 'Kaffee to go'"
            Expect.equal byAmount.TotalCount 3 "Exakt -4,50 EUR trifft drei Buchungen"
            Expect.equal combined.TotalCount 1 "Nur die Buchung, die alle drei Filter zugleich erfuellt"
            Expect.equal
                (combined.Entries |> List.map (fun e -> e.Memo))
                [ Some "Kaffee" ]
                "Die Und-Verknuepfung laesst nur Cafe Lotte + Kaffee + -4,50 EUR durch"

        testCase "AC-Umlaut: Payee- und Memo-Filter falten Groß-/Kleinschreibung auch bei Umlauten (ledger-k3wqn, ADR 0037)" <| fun () ->
            // Echte Produktionsschreibweisen, kein transliteriertes ASCII (anders als
            // AC6/AC7): Comdirect und YNAB liefern Payee/Memo häufig in
            // Großbuchstaben, genau der Fall, an dem die frühere SQLite-Kollation
            // lautlos versagt, weil sie nur ASCII A-Z faltet (ADR 0037 Context).
            seedEntryFull pageTestAccountP 96 -10.00m (DateTime(2026, 5, 1)) (Some "MÜLLER GMBH") (Some "GRÜNE ERDE Rechnung") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountP 97 -20.00m (DateTime(2026, 5, 2)) (Some "Müller GmbH") (Some "Grüne Erde Rechnung") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountP 98 -30.00m (DateTime(2026, 5, 3)) (Some "Schmidt AG") (Some "Blaue Erde") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountP 99 -4.50m (DateTime(2026, 5, 4)) (Some "STRAßE DES 17. JUNI") (Some "Parkgebühr") None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountP 100 -4.50m (DateTime(2026, 5, 5)) (Some "MÜLLER GMBH") (Some "GRÜNE ERDE Nachtrag") None
            |> Async.RunSynchronously

            let (LedgerAccountId rawP) = pageTestAccountP

            let byPayeeLower =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Payee = Some "müller gmbh" }
                |> Async.RunSynchronously
            let byPayeeUpper =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Payee = Some "MÜLLER GMBH" }
                |> Async.RunSynchronously
            let byMemoLower =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Memo = Some "grüne" }
                |> Async.RunSynchronously
            let byMemoUpper =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Memo = Some "GRÜNE ERDE" }
                |> Async.RunSynchronously
            let byPayeeStrasse =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Payee = Some "straße des 17. juni" }
                |> Async.RunSynchronously

            Expect.equal byPayeeLower.TotalCount 3 "'müller gmbh' faltet beide Großschreibungen von MÜLLER GMBH/Müller GmbH (heute: nur 1, weil die frühere Kollation nur ASCII faltet)"
            Expect.equal byPayeeUpper.TotalCount 3 "Auch von der Großschreibung aus muss die Faltung greifen, nicht nur einseitig (heute: nur 2)"
            Expect.equal byMemoLower.TotalCount 3 "Memo-Teiltext 'grüne' faltet alle drei GRÜNE-ERDE-Schreibweisen (heute: nur 1); Beleg, dass eine SQLite-Kollation nicht gereicht hätte, weil LIKE Kollationen ignoriert (ADR 0037)"
            Expect.equal byMemoUpper.TotalCount 3 "Auch von der Großschreibung des Memo-Suchtexts aus muss die Faltung greifen"
            Expect.equal byPayeeStrasse.TotalCount 1 "STRAßE DES 17. JUNI wird über ToLowerInvariant gefunden"

            Expect.equal (List.length byPayeeLower.Entries) 3 "loadEntriesPage trägt alle drei gefundenen Buchungen"
            Expect.equal byPayeeLower.TotalAmount { Amount = -34.50m; Currency = "EUR" } "loadAmountsForQuery summiert über die gefaltete Treffermenge, nicht nur die ASCII-lesbare"

            // loadAllMatching-Zweig (Betragsfilter zwingt den F#-Nachfilter-Pfad, ADR 0017/0036)
            let byPayeeAndAmount =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawP) with
                        Payee = Some "müller gmbh"
                        AmountMin = Some -4.50m
                        AmountMax = Some -4.50m }
                |> Async.RunSynchronously
            Expect.equal byPayeeAndAmount.TotalCount 1 "Nur n=100 (-4,50 EUR) erfüllt Payee und Betrag zugleich"
            Expect.equal byPayeeAndAmount.TotalAmount { Amount = -4.50m; Currency = "EUR" } "Summe des loadAllMatching-Zweigs stimmt"

            // Registrierung über den Verbindungs-Pool: derselbe gefilterte Aufruf ein
            // zweites Mal, mit einer neuen, aus dem Pool geholten Connection (ADR 0037
            // Consequences: Microsoft.Data.Sqlite legt Funktionen bei jedem Open() neu
            // auf den Handle, der einzige echte Ausfallmodus dieser Lösung).
            let byPayeeLowerAgain =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Payee = Some "müller gmbh" }
                |> Async.RunSynchronously
            Expect.equal byPayeeLowerAgain.TotalCount byPayeeLower.TotalCount "Zweiter Aufruf über eine neue Connection liefert dasselbe Ergebnis"

            // Scope-Grenze (ADR 0037 Decision 5): Diakritika-Entfernung ist ausdrücklich
            // nicht Teil dieser Lösung. 'strasse' (transliteriert) findet 'Straße'
            // nicht, 'gruene' (transliteriert) findet 'Grüne' nicht.
            let byPayeeTransliterated =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Payee = Some "strasse des 17. juni" }
                |> Async.RunSynchronously
            let byMemoTransliterated =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawP) with Memo = Some "gruene" }
                |> Async.RunSynchronously
            Expect.equal byPayeeTransliterated.TotalCount 0 "'strasse' (ss statt ß) fällt bewusst durch, Diakritika-Entfernung ist nicht Teil von ADR 0037"
            Expect.equal byMemoTransliterated.TotalCount 0 "'gruene' (ue statt ü) fällt bewusst durch, aus demselben Grund"

        testCase "AC7: Payee-Vorschlaege zaehlen nur innerhalb des uebergebenen Filters" <| fun () ->
            // Rewe kommt insgesamt fünfmal vor, aber nur zweimal in Kategorie 26.
            // Baecker kommt insgesamt nur dreimal vor, aber alle drei in Kategorie 26 —
            // innerhalb des Filters hat Baecker also die groessere, nicht Rewe.
            seedEntryFull pageTestAccountN 82 -1m (DateTime(2026, 5, 1)) (Some "Rewe") None (Some (category 26 "Kategorie F"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 83 -2m (DateTime(2026, 5, 2)) (Some "Rewe") None (Some (category 26 "Kategorie F"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 84 -3m (DateTime(2026, 5, 3)) (Some "Rewe") None (Some (category 27 "Kategorie G"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 85 -4m (DateTime(2026, 5, 4)) (Some "Rewe") None (Some (category 27 "Kategorie G"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 86 -5m (DateTime(2026, 5, 5)) (Some "Rewe") None (Some (category 27 "Kategorie G"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 87 -6m (DateTime(2026, 5, 6)) (Some "Baecker") None (Some (category 26 "Kategorie F"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 88 -7m (DateTime(2026, 5, 7)) (Some "Baecker") None (Some (category 26 "Kategorie F"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountN 89 -8m (DateTime(2026, 5, 8)) (Some "Baecker") None (Some (category 26 "Kategorie F"))
            |> Async.RunSynchronously

            let (LedgerAccountId rawN) = pageTestAccountN
            let suggestionsQuery = { baseSuggestionQuery (Some rawN) with CategoryIds = [ catGuid 26 ] }
            let suggestions =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions suggestionsQuery |> Async.RunSynchronously

            let countOf payee =
                suggestions |> List.tryFind (fun s -> s.Payee = payee) |> Option.map (fun s -> s.Count)

            Expect.equal (countOf "Rewe") (Some 2) "Rewe zaehlt nur die zwei Treffer innerhalb des Filters, nicht alle fuenf"
            Expect.equal (countOf "Baecker") (Some 3) "Baecker zaehlt alle drei, weil alle drei im Filter liegen"
            Expect.isTrue
                ((countOf "Baecker" |> Option.get) > (countOf "Rewe" |> Option.get))
                "Innerhalb des Filters hat Baecker die groessere Zahl, obwohl Rewe insgesamt haeufiger vorkommt"

        testCase "AC8: Die Regel fuer geloeschte Buchungen gilt unveraendert weiter, auch mit gesetzten Filtern" <| fun () ->
            seedEntryFull pageTestAccountO 90 -10m (DateTime(2026, 5, 1)) None None (Some (category 28 "Kategorie H"))
            |> Async.RunSynchronously
            seedDeletedEntryFull pageTestAccountO 91 -1000m (DateTime(2026, 5, 2)) None None (Some (category 28 "Kategorie H"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountO 92 -20m (DateTime(2026, 5, 3)) None None (Some (category 29 "Kategorie I"))
            |> Async.RunSynchronously
            seedDeletedEntryFull pageTestAccountO 93 -500m (DateTime(2026, 5, 4)) None None (Some (category 29 "Kategorie I"))
            |> Async.RunSynchronously

            let (LedgerAccountId rawO) = pageTestAccountO
            let hidden =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawO) with CategoryIds = [ catGuid 28 ] }
                |> Async.RunSynchronously
            let shown =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawO) with CategoryIds = [ catGuid 28 ]; IncludeDeleted = true }
                |> Async.RunSynchronously

            Expect.equal hidden.TotalCount 1 "Ohne IncludeDeleted zaehlt nur die lebende Buchung der Kategorie"
            Expect.equal hidden.TotalAmount { Amount = -10m; Currency = "EUR" } "Summe ohne die geloeschte Buchung"
            Expect.equal hidden.DeletedCount 1
                "DeletedCount zaehlt nur die geloeschte Buchung INNERHALB des Kategorie-Filters, nicht die aus Kategorie I"
            Expect.equal shown.TotalCount 2 "Mit IncludeDeleted zaehlen beide Buchungen der Kategorie"
            Expect.equal shown.TotalAmount { Amount = -1010m; Currency = "EUR" } "Summe inklusive der geloeschten Buchung"
            Expect.equal shown.DeletedCount 1 "DeletedCount bleibt gleich, unabhaengig vom Schalter"

        // ============================================
        // Betragsfilter-Zweig: SQL-Pfad vs. F#-Nachfilter (ledger-c4mzp, ADR 0017/0036)
        // ============================================
        // `hasAmountFilter` gabelt fünf Lese-Funktionen in zwei Implementierungen.
        // Geprüft war bisher nur `TotalCount` (AC6, min = max). Die folgenden
        // Testfälle decken `TotalAmount`, `DeletedCount`, `safePage`, die
        // Payee-Vorschläge und die einseitig offenen Grenzen ab, je auf eigenem Konto.

        testCase "Betrags-Zweig: TotalAmount folgt dem Treffer, nicht der geladenen Seite und nicht der ungefilterten Menge" <| fun () ->
            seedEntry pageTestAccountQ 101 -5.00m |> Async.RunSynchronously
            seedEntry pageTestAccountQ 102 -50.00m |> Async.RunSynchronously
            seedEntry pageTestAccountQ 103 -500.00m |> Async.RunSynchronously
            seedEntry pageTestAccountQ 104 20.00m |> Async.RunSynchronously

            let (LedgerAccountId rawQ) = pageTestAccountQ
            let page =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawQ) with PageSize = 1; AmountMin = Some -100m; AmountMax = Some 0m }
                |> Async.RunSynchronously

            Expect.equal page.TotalCount 2 "Nur -5 und -50 EUR liegen im Intervall [-100; 0]"
            Expect.equal page.TotalAmount { Amount = -55m; Currency = "EUR" }
                "Die Summe deckt den gesamten Treffer ab (-5 + -50); ungefiltert wären es -535, nur die Seite wäre -5"
            Expect.equal (List.length page.Entries) 1 "Die Seite trägt nur ihre eigene Größe, unabhängig von der Summe"

        testCase "Betrags-Zweig: DeletedCount übernimmt IncludeDeleted=true wie der SQL-Zweig, statt es vom Filter zu erben" <| fun () ->
            seedEntry pageTestAccountR 105 -10.00m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountR 106 -20.00m |> Async.RunSynchronously
            seedDeletedEntry pageTestAccountR 107 -900.00m |> Async.RunSynchronously

            let (LedgerAccountId rawR) = pageTestAccountR
            let page =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawR) with AmountMin = Some -100m; AmountMax = Some -1m }
                |> Async.RunSynchronously

            Expect.equal page.TotalCount 1 "Nur die lebende Buchung zählt zum sichtbaren Treffer"
            Expect.equal page.TotalAmount { Amount = -10m; Currency = "EUR" } "Die Summe der sichtbaren Buchung"
            Expect.equal page.DeletedCount 1
                "Von den zwei gelöschten Buchungen erfüllt nur -20 EUR den Betragsfilter, -900 EUR liegt außerhalb; ungefiltert wären es 2, ohne IncludeDeleted=true im Betrags-Zweig 0"

        testCase "Betrags-Zweig: Payee-Vorschläge zählen und sortieren wie der SQL-Zweig, der Betragsfilter wirkt vor dem Gruppieren" <| fun () ->
            seedEntryFull pageTestAccountS 108 -1.00m (DateTime(2026, 7, 1)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountS 109 -2.00m (DateTime(2026, 7, 2)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountS 110 -300.00m (DateTime(2026, 7, 3)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountS 111 -3.00m (DateTime(2026, 7, 4)) (Some "Baecker") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountS 112 -4.00m (DateTime(2026, 7, 5)) (Some "Baecker") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountS 113 -500.00m (DateTime(2026, 7, 6)) (Some "Aldi") None None
            |> Async.RunSynchronously

            let (LedgerAccountId rawS) = pageTestAccountS
            let sqlPath =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions (baseSuggestionQuery (Some rawS))
                |> Async.RunSynchronously
            let amountPath =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions
                    { baseSuggestionQuery (Some rawS) with AmountMin = Some -1000m; AmountMax = Some 0m }
                |> Async.RunSynchronously
            let narrowedAmountPath =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions
                    { baseSuggestionQuery (Some rawS) with AmountMax = Some -250m }
                |> Async.RunSynchronously

            let byPayeeAscending (suggestions: LedgerPayeeSuggestion list) =
                suggestions |> List.sortBy (fun s -> s.Payee) |> List.map (fun s -> s.Payee, s.Count)

            let expected = [ "Aldi", 1; "Baecker", 2; "Rewe", 3 ]
            // Fängt einen F#-Pfad, der über die geladene Seite statt über den Treffer
            // zählt, der `List.choose (fun e -> e.Payee)` vergisst, oder der nach
            // einem anderen Feld gruppiert.
            Expect.equal (byPayeeAscending sqlPath) expected "SQL-Zweig (kein Betragsfilter): drei Payees mit ihrer jeweiligen Anzahl"
            Expect.equal (byPayeeAscending amountPath) expected
                "F#-Zweig mit einem Intervall, das alle sechs Buchungen einschließt, liefert dasselbe wie der SQL-Zweig"

            // Sortierrichtung: die eindeutig größte Zahl steht vorn. Fängt ein
            // `List.sortBy snd` statt `List.sortByDescending snd` im F#-Pfad. Die
            // Reihenfolge bei Gleichstand (Aldi/Baecker) wird bewusst nicht geprüft:
            // `ORDER BY cnt DESC` hat im SQL-Pfad keinen Tiebreak.
            Expect.equal (sqlPath |> List.head |> fun s -> s.Payee) "Rewe" "Die eindeutig größte Zahl steht im SQL-Zweig vorn"
            Expect.equal (amountPath |> List.head |> fun s -> s.Payee) "Rewe" "Dieselbe Sortierrichtung im F#-Zweig"

            // Der Betragsfilter wirkt auf die Vorschläge. Fängt einen F#-Pfad, der
            // `amountMatches` vor dem Gruppieren auslässt.
            Expect.equal (byPayeeAscending narrowedAmountPath) [ "Aldi", 1; "Rewe", 1 ]
                "Nur Buchungen bis -250 EUR bleiben übrig: je eine von Aldi und Rewe"
            Expect.isNone
                (narrowedAmountPath |> List.tryFind (fun s -> s.Payee = "Baecker"))
                "Baecker fällt komplett heraus: amountMatches muss vor dem Gruppieren wirken"

        testCase "Betrags-Zweig: Blättern über safePage, ohne Dubletten und ohne Ausnahme hinter dem Listenende" <| fun () ->
            // Fängt ein `safePage` ohne Offset-Wächter (`List.skip` über das
            // Listenende wirft) und eines, das `List.truncate` vor `List.skip`
            // anwendet (Seite 1 lieferte dann dieselben zwei wie Seite 0).
            for n, amount in [ 114, -1m; 115, -2m; 116, -3m; 117, -4m; 118, -5m ] do
                seedEntry pageTestAccountT n amount |> Async.RunSynchronously

            let (LedgerAccountId rawT) = pageTestAccountT
            let query pageIndex =
                { baseQuery (Some rawT) with PageSize = 2; Page = pageIndex; AmountMin = Some -10m }
            let page0 = Ledger.QueryHandlers.getLedgerEntriesPage (query 0) |> Async.RunSynchronously
            let page1 = Ledger.QueryHandlers.getLedgerEntriesPage (query 1) |> Async.RunSynchronously
            let page2 = Ledger.QueryHandlers.getLedgerEntriesPage (query 2) |> Async.RunSynchronously
            let page3 = Ledger.QueryHandlers.getLedgerEntriesPage (query 3) |> Async.RunSynchronously

            Expect.equal (List.length page0.Entries) 2 "Seite 0 trägt zwei Buchungen"
            Expect.equal (List.length page1.Entries) 2 "Seite 1 trägt zwei weitere, nicht dieselben wie Seite 0"
            Expect.equal (List.length page2.Entries) 1 "Seite 2 ist nur teilweise gefüllt"
            Expect.equal (List.length page3.Entries) 0 "Seite 3 liegt hinter dem Ende, wirft aber keine Ausnahme"

            let expectedTotal = { Amount = -15m; Currency = "EUR" }
            for page in [ page0; page1; page2; page3 ] do
                Expect.equal page.TotalCount 5 "Alle vier Antworten tragen dieselbe Trefferzahl über den gesamten Treffer"
                Expect.equal page.TotalAmount expectedTotal "Alle vier Antworten tragen dieselbe Gesamtsumme"

            let idsOf (page: LedgerEntryPage) = page.Entries |> List.map (fun e -> e.Id)
            let seenIds = idsOf page0 @ idsOf page1 @ idsOf page2
            let expectedIds = [ 114 .. 118 ] |> List.map (fun n -> let (EntryId g) = pageEntryId n in g)
            Expect.equal (List.sort seenIds) (List.sort expectedIds)
                "Die Ids der Seiten 0 bis 2 ergeben zusammen genau die fünf gesäten Ids, ohne Dublette"
            Expect.equal (List.length seenIds) (List.length (List.distinct seenIds)) "Keine Buchung erscheint auf zwei Seiten zugleich"

        testCase "Betrags-Zweig: einseitig offene Grenzen und ein vertauschtes Intervall" <| fun () ->
            seedEntry pageTestAccountU 119 -100.00m |> Async.RunSynchronously
            seedEntry pageTestAccountU 120 -50.00m |> Async.RunSynchronously
            seedEntry pageTestAccountU 121 -5.00m |> Async.RunSynchronously
            seedEntry pageTestAccountU 122 30.00m |> Async.RunSynchronously

            let (LedgerAccountId rawU) = pageTestAccountU

            // Nur `AmountMin` gesetzt. Fängt eine Umsetzung, die beide Enden verlangt
            // (ergäbe 0 Treffer) und eine, die `>=` und `<=` vertauscht (ergäbe nur -100,00).
            let onlyMin =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawU) with AmountMin = Some -50m }
                |> Async.RunSynchronously
            Expect.equal onlyMin.TotalCount 3 "Mindestens -50: -100 fällt heraus, die anderen drei bleiben"
            Expect.equal
                (onlyMin.Entries |> List.map (fun e -> e.Amount.Amount) |> List.sort)
                [ -50m; -5m; 30m ]
                "Genau die drei Beträge ab -50 aufwärts, -100 ist nicht dabei"

            // Nur `AmountMax` gesetzt, Randwert eingeschlossen (>=/<=, nicht >/<).
            let onlyMax =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawU) with AmountMax = Some -50m }
                |> Async.RunSynchronously
            Expect.equal onlyMax.TotalCount 2 "Höchstens -50: der Randwert -50 zählt mit"
            Expect.equal
                (onlyMax.Entries |> List.map (fun e -> e.Amount.Amount) |> List.sort)
                [ -100m; -50m ]
                "Der Randwert -50 ist eingeschlossen, nicht per >/< ausgeschlossen"

            // Vertauschtes Intervall (AmountMin > AmountMax). Fängt eine Umsetzung,
            // die die Grenzen hilfsbereit tauscht, und eine, die die beiden Enden mit
            // ODER statt UND verknüpft.
            let swapped =
                Ledger.QueryHandlers.getLedgerEntriesPage
                    { baseQuery (Some rawU) with AmountMin = Some 0m; AmountMax = Some -100m }
                |> Async.RunSynchronously
            Expect.equal swapped.TotalCount 0 "Ein vertauschtes Intervall bleibt leer, statt getauscht oder per ODER verknüpft zu werden"
            Expect.equal swapped.Entries [] "Keine Buchung erfüllt ein leeres Intervall"
            Expect.equal swapped.TotalAmount { Amount = 0m; Currency = "EUR" } "Die Summe eines leeren Treffers ist 0"

        testCase "Betrags-Zweig: NeedsReviewCount bleibt von jedem Such-Filter unberührt, auch vom Betragsfilter" <| fun () ->
            // Fängt ein späteres „Vereinheitlichen“, das den Filter an
            // `countNeedsReviewEntries` durchreicht (ergäbe 3, 2 und 1 statt 4, 3, 1 bei
            // TotalCount, aber 3, 3, 3 bei NeedsReviewCount).
            seedEntry pageTestAccountV 123 -10.00m |> Async.RunSynchronously
            seedEntry pageTestAccountV 124 -20.00m |> Async.RunSynchronously
            seedEntry pageTestAccountV 125 -3000.00m |> Async.RunSynchronously
            seedReviewedEntry pageTestAccountV 126 -40.00m |> Async.RunSynchronously

            let (LedgerAccountId rawV) = pageTestAccountV
            let unfiltered = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawV)) |> Async.RunSynchronously
            let amountFiltered =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawV) with AmountMin = Some -100m }
                |> Async.RunSynchronously
            let payeeFiltered =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawV) with Payee = Some "payee 123" }
                |> Async.RunSynchronously

            Expect.equal unfiltered.TotalCount 4 "Alle vier Buchungen ohne Filter"
            Expect.equal amountFiltered.TotalCount 3 "Der Betragsfilter (F#-Zweig) schließt die -3000,00 EUR aus"
            Expect.equal payeeFiltered.TotalCount 1 "Der Payee-Filter (SQL-Zweig) trifft genau eine Buchung"

            Expect.equal unfiltered.NeedsReviewCount 3 "Drei ungeprüfte Buchungen, unabhängig vom Filter"
            Expect.equal amountFiltered.NeedsReviewCount 3 "Derselbe feste Zähler, auch mit aktivem Betragsfilter"
            Expect.equal payeeFiltered.NeedsReviewCount 3 "Derselbe feste Zähler, auch mit aktivem Payee-Filter"

        // ============================================
        // Split-Zeilen-Kategorien im Kategorie-Filter (ledger-xk9wq, ADR 0038)
        // ============================================

        testCase "AC1-AC3: eine Buchung, deren einzige Kategorie in einer Split-Zeile steckt, erscheint in Liste, Trefferzahl und Summe" <| fun () ->
            seedSplitEntry
                pageTestAccountW
                200
                (DateTime(2026, 8, 1))
                (Some "SplitPayee")
                None
                [ { Target = ToCategory (catId 30, "Kategorie 30"); Amount = eur -30m; Memo = None }
                  { Target = Uncategorized; Amount = eur -20m; Memo = None } ]
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountW 201 -10m (DateTime(2026, 8, 2)) (Some "SimplePayeeX") None (Some (category 30 "Kategorie 30"))
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountW 202 -5m (DateTime(2026, 8, 3)) (Some "SimplePayeeY") None (Some (category 31 "Kategorie 31"))
            |> Async.RunSynchronously

            let (LedgerAccountId rawW) = pageTestAccountW
            let query = { baseQuery (Some rawW) with CategoryIds = [ catGuid 30 ] }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            Expect.equal page.TotalCount 2
                "AC2: die Split-Buchung (category_id = NULL, Kategorie in einer Split-Zeile) UND die einfache Buchung derselben Kategorie zählen, Kategorie 31 nicht"
            Expect.equal
                (page.Entries |> List.map (fun e -> e.Payee) |> List.sort)
                [ Some "SimplePayeeX"; Some "SplitPayee" ]
                "AC1: die Split-Buchung erscheint in der Liste, obwohl ihre category_id-Spalte NULL ist"
            Expect.equal page.TotalAmount { Amount = -60m; Currency = "EUR" }
                "AC3: Summe exakt in F# über die Split-Buchung (-50) und die einfache Buchung (-10)"

        testCase "AC4: eine in YNAB gelöschte Split-Buchung mit Kategorie X wird bei Filter auf X in DeletedCount gezählt" <| fun () ->
            seedDeletedSplitEntry
                pageTestAccountX
                203
                (DateTime(2026, 8, 1))
                (Some "GelöschtePayee")
                None
                [ { Target = ToCategory (catId 32, "Kategorie 32"); Amount = eur -8m; Memo = None }
                  { Target = Uncategorized; Amount = eur -2m; Memo = None } ]
            |> Async.RunSynchronously

            let (LedgerAccountId rawX) = pageTestAccountX
            let query = { baseQuery (Some rawX) with CategoryIds = [ catGuid 32 ] }
            let hidden = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            let shown = Ledger.QueryHandlers.getLedgerEntriesPage { query with IncludeDeleted = true } |> Async.RunSynchronously

            Expect.equal hidden.TotalCount 0 "ohne IncludeDeleted bleibt die gelöschte Split-Buchung außerhalb des Treffers"
            Expect.equal hidden.DeletedCount 1 "DeletedCount zählt sie trotzdem, unabhängig von IncludeDeleted"
            Expect.equal shown.TotalCount 1 "mit IncludeDeleted zählt sie im Treffer mit"

        testCase "AC5: getLedgerPayeeSuggestions mit CategoryIds = [X] enthält den Payee der Split-Buchung" <| fun () ->
            seedSplitEntry
                pageTestAccountY
                204
                (DateTime(2026, 8, 1))
                (Some "SplitSuggestPayee")
                None
                [ { Target = ToCategory (catId 33, "Kategorie 33"); Amount = eur -6m; Memo = None }
                  { Target = Uncategorized; Amount = eur -4m; Memo = None } ]
            |> Async.RunSynchronously

            let (LedgerAccountId rawY) = pageTestAccountY
            let suggestionsQuery = { baseSuggestionQuery (Some rawY) with CategoryIds = [ catGuid 33 ] }
            let suggestions = Ledger.QueryHandlers.getLedgerPayeeSuggestions suggestionsQuery |> Async.RunSynchronously

            Expect.isTrue
                (suggestions |> List.exists (fun s -> s.Payee = "SplitSuggestPayee"))
                "der Payee der Split-Buchung steht unter den Vorschlägen, obwohl ihre category_id-Spalte NULL ist"

        testCase "AC6: NeedsReviewCount bleibt ungefiltert, auch wenn der Kategorie-Filter eine Split-Zeilen-Kategorie trifft" <| fun () ->
            seedSplitEntry
                pageTestAccountZ
                205
                (DateTime(2026, 8, 1))
                (Some "SplitPayeeZ")
                None
                [ { Target = ToCategory (catId 34, "Kategorie 34"); Amount = eur -3m; Memo = None }
                  { Target = Uncategorized; Amount = eur -1m; Memo = None } ]
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountZ 206 -1m (DateTime(2026, 8, 2)) (Some "SimplePayeeZ") None (Some (category 35 "Kategorie 35"))
            |> Async.RunSynchronously

            let (LedgerAccountId rawZ) = pageTestAccountZ
            let unfiltered = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawZ)) |> Async.RunSynchronously
            let filtered =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawZ) with CategoryIds = [ catGuid 34 ] }
                |> Async.RunSynchronously

            Expect.equal unfiltered.TotalCount 2 "beide Buchungen ohne Filter"
            Expect.equal filtered.TotalCount 1 "der Kategorie-Filter grenzt auf die Split-Buchung ein"
            Expect.equal unfiltered.NeedsReviewCount 2 "beide Buchungen sind ungeprüft"
            Expect.equal filtered.NeedsReviewCount 2
                "NeedsReviewCount bleibt der feste Kontozähler, unverändert vom Kategorie-Filter (ADR 0036 Punkt 6), obwohl TotalCount sich ändert"

        testCase "AC7: wandert die Split-Kategorie von X nach Y, findet der Filter die Buchung nur noch unter Y; wird daraus eine einfache Buchung, findet sie keiner mehr" <| fun () ->
            let firstSplits =
                [ { Target = ToCategory (catId 36, "Kategorie 36"); Amount = eur -7m; Memo = None }
                  { Target = Uncategorized; Amount = eur -3m; Memo = None } ]
            seedSplitEntry pageTestAccountAA 207 (DateTime(2026, 8, 1)) (Some "WandernderPayee") None firstSplits
            |> Async.RunSynchronously

            let (LedgerAccountId rawAA) = pageTestAccountAA
            let byOld = Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawAA) with CategoryIds = [ catGuid 36 ] } |> Async.RunSynchronously
            Expect.equal byOld.TotalCount 1 "zuerst unter der ersten Kategorie zu finden"

            let secondSplits =
                [ { Target = ToCategory (catId 37, "Kategorie 37"); Amount = eur -7m; Memo = None }
                  { Target = Uncategorized; Amount = eur -3m; Memo = None } ]
            appendAndApply [
                { EntryId = pageEntryId 207
                  OccurredAt = DateTime(2026, 8, 2)
                  Actor = YnabMirror
                  Body = EntryCategorizationChangeObservedInYnab { OldCategory = None; NewCategory = None; OldSplits = firstSplits; NewSplits = secondSplits } }
            ]
            |> Async.RunSynchronously

            let byOldAfter = Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawAA) with CategoryIds = [ catGuid 36 ] } |> Async.RunSynchronously
            let byNew = Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawAA) with CategoryIds = [ catGuid 37 ] } |> Async.RunSynchronously
            Expect.equal byOldAfter.TotalCount 0 "keine veraltete Indexzeile unter der alten Kategorie"
            Expect.equal byNew.TotalCount 1 "die Buchung steht jetzt unter der neuen Kategorie"

            appendAndApply [
                { EntryId = pageEntryId 207
                  OccurredAt = DateTime(2026, 8, 3)
                  Actor = YnabMirror
                  Body =
                    EntryCategorizationChangeObservedInYnab
                        { OldCategory = None; NewCategory = Some (category 38 "Kategorie 38"); OldSplits = secondSplits; NewSplits = [] } }
            ]
            |> Async.RunSynchronously

            let byNewAfter = Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawAA) with CategoryIds = [ catGuid 37 ] } |> Async.RunSynchronously
            Expect.equal byNewAfter.TotalCount 0
                "wird aus der Buchung wieder eine einfache Buchung ohne Splits, findet kein Kategorie-Filter sie mehr über die alte Split-Kategorie"

        testCase "AC15: Kategorie-Filter auf eine Split-Zeilen-Kategorie kombiniert mit dem Betragsfilter findet dieselbe Split-Buchung" <| fun () ->
            seedSplitEntry
                pageTestAccountAB
                208
                (DateTime(2026, 8, 1))
                (Some "SplitPlusBetrag")
                None
                [ { Target = ToCategory (catId 39, "Kategorie 39"); Amount = eur -30m; Memo = None }
                  { Target = Uncategorized; Amount = eur -12m; Memo = None } ]
            |> Async.RunSynchronously

            let (LedgerAccountId rawAB) = pageTestAccountAB
            let query =
                { baseQuery (Some rawAB) with
                    CategoryIds = [ catGuid 39 ]
                    AmountMin = Some -50m
                    AmountMax = Some -40m }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            Expect.equal page.TotalCount 1 "der loadAllMatching-Zweig (Betragsfilter) ruft dieselbe buildFilterClause, findet die Split-Buchung also auch"
            Expect.equal page.TotalAmount { Amount = -42m; Currency = "EUR" } "Summe des einzigen Treffers"

        testCase "AC9: rebuildSplitIndexes stellt den Index nach vollständigem Löschen aus dem Bestand wieder her, ein zweiter Aufruf liefert dasselbe" <| fun () ->
            seedSplitEntry
                pageTestAccountAC
                209
                (DateTime(2026, 8, 1))
                (Some "BackfillPayee")
                None
                [ { Target = ToCategory (catId 40, "Kategorie 40"); Amount = eur -9m; Memo = None }
                  { Target = Uncategorized; Amount = eur -1m; Memo = None } ]
            |> Async.RunSynchronously

            let (LedgerAccountId rawAC) = pageTestAccountAC
            let query = { baseQuery (Some rawAC) with CategoryIds = [ catGuid 40 ] }
            let before = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            Expect.equal before.TotalCount 1 "der Schreibpfad (writeSplitCategoryIndex) hat den Index schon gepflegt"

            use conn = new Microsoft.Data.Sqlite.SqliteConnection(Persistence.activeConnectionString ())
            conn.Open()
            use clearCmd = new Microsoft.Data.Sqlite.SqliteCommand("DELETE FROM ledger_entry_split_categories", conn)
            clearCmd.ExecuteNonQuery() |> ignore

            let afterClear = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            Expect.equal afterClear.TotalCount 0 "ohne Index findet der Filter die Split-Buchung nicht mehr: belegt, dass das Löschen gegriffen hat"

            let written1 = rebuildSplitIndexes conn
            let afterRebuild = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            Expect.equal afterRebuild.TotalCount 1 "der Neuaufbau stellt den Index allein aus ledger_entries wieder her, ohne das Ereignis-Log zu lesen"
            Expect.isTrue (written1 >= 1) "mindestens die eine Split-Buchung dieses Tests wurde geschrieben"

            let written2 = rebuildSplitIndexes conn
            Expect.equal written2 written1 "ein zweiter Aufruf liefert denselben Rückgabewert"
            let afterSecond = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            Expect.equal afterSecond.TotalCount 1 "und dieselbe Treffermenge"

        testCase "AC10: der Inhalt von ledger_entry_split_categories stimmt mit splitCategoryIds auf replayAll() überein, keine Drift" <| fun () ->
            seedSplitEntry
                pageTestAccountAD
                211
                (DateTime(2026, 8, 1))
                (Some "DriftPayeeSplit")
                None
                [ { Target = ToCategory (catId 41, "Kategorie 41"); Amount = eur -5m; Memo = None }
                  { Target = Uncategorized; Amount = eur -5m; Memo = None } ]
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAD 212 -1m (DateTime(2026, 8, 2)) (Some "DriftPayeeSimple") None (Some (category 42 "Kategorie 42"))
            |> Async.RunSynchronously

            let allEntries = replayAll () |> Async.RunSynchronously
            let expectedPairs =
                allEntries
                |> List.collect (fun e ->
                    let (EntryId eid) = e.Id
                    splitCategoryIds e |> List.map (fun catId -> catId, eid))
                |> Set.ofList

            use conn = new Microsoft.Data.Sqlite.SqliteConnection(Persistence.activeConnectionString ())
            conn.Open()
            use readCmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT category_id, entry_id FROM ledger_entry_split_categories", conn)
            use reader = readCmd.ExecuteReader()
            let actualPairs =
                [ while reader.Read() do
                    yield Guid.Parse(reader.GetString 0), Guid.Parse(reader.GetString 1) ]
                |> Set.ofList

            Expect.equal actualPairs expectedPairs
                "die Indextabelle enthält über den gesamten Bestand genau die Paare, die splitCategoryIds auf das replayAll()-Ergebnis liefert"

        testCase "AC11: eine über appendAndApply geschriebene Buchung ohne Splits hat in der Spalte splits exakt den Text '[]'" <| fun () ->
            seedEntry pageTestAccountAE 213 -1m |> Async.RunSynchronously

            let (EntryId rawId) = pageEntryId 213
            use conn = new Microsoft.Data.Sqlite.SqliteConnection(Persistence.activeConnectionString ())
            conn.Open()
            use cmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT splits FROM ledger_entries WHERE id = @id", conn)
            cmd.Parameters.AddWithValue("@id", rawId.ToString()) |> ignore
            let splitsText = cmd.ExecuteScalar() :?> string

            Expect.equal splitsText "[]" "das Backfill-Prädikat 'WHERE splits <> [] ' in rebuildSplitIndexes verlässt sich exakt auf diesen Text"

        testCase "AC12: der Zugriffsplan eines Kategorie-Filters nutzt die Indextabelle, ohne Tabellen-Scan und ohne korrelierte Unterabfrage" <| fun () ->
            let plan = explainEntryFilterPlan None { noFilter with CategoryIds = [ catGuid 30 ] }
            let planText = String.concat " | " plan

            Expect.isTrue
                (plan |> List.exists (fun line -> line.Contains "ledger_entry_split_categories"))
                $"ledger_entry_split_categories muss im Plan vorkommen, die Split-Prüfung findet also in der Abfrage statt (Plan: {planText})"
            Expect.isFalse
                (plan |> List.exists (fun line -> line.Contains "SCAN ledger_entry_split_categories"))
                $"kein Tabellen-Scan der Indextabelle, sie wird per SEARCH bedient (Plan: {planText})"
            Expect.isFalse
                (plan |> List.exists (fun line -> line.Contains "CORRELATED"))
                $"keine korrelierte Unterabfrage (Plan: {planText})"

        testCase "AC13: Persistence.fs hat weiterhin genau fünf Aufrufe von loadAllMatching und fünf hasAmountFilter-Gabelungen" <| fun () ->
            // Quelltext-Zusicherung nach dem Muster von FontFaceRegressionTests.fs und
            // BackendPortGuardTests.fs: hält fest, dass diese Aufgabe keine sechste
            // Gabelung der `hasAmountFilter`-Verzweigung einführt (ledger-r7dm2 Punkt 1
            // bleibt Testschuld, wird hier nicht vergrößert).
            let testDir = __SOURCE_DIRECTORY__
            let repoRoot = Directory.GetParent(Directory.GetParent(testDir).FullName).FullName
            let persistenceFsPath = Path.Combine(repoRoot, "src", "Server", "Ledger", "Persistence.fs")
            let text = File.ReadAllText persistenceFsPath

            let loadAllMatchingOccurrences = Regex.Matches(text, @"\bloadAllMatching\b").Count
            // Minus 1 für die Definition selbst ("let private loadAllMatching ...").
            Expect.equal (loadAllMatchingOccurrences - 1) 5 "fünf Aufrufe von loadAllMatching, ohne die Definition mitzuzählen"

            let hasAmountFilterGuards = Regex.Matches(text, @"if hasAmountFilter filter then").Count
            Expect.equal hasAmountFilterGuards 5 "fünf Vorkommen der hasAmountFilter-Gabelung"

        testCase "AC14: 1000 Buchungen, drei mit der gesuchten Split-Kategorie, liefern TotalCount = 3 in angemessener Zeit" <| fun () ->
            let hitCategory = catId 43
            let hitIndices = set [ 100; 500; 900 ]

            let makeEvent (i: int) : LedgerEvent =
                let isHit = hitIndices.Contains i
                let payload : EntryObservedInYnabPayload = {
                    AccountId = pageTestAccountAF
                    YnabTransactionId = $"perftest-{i}"
                    ImportId = None
                    Date = DateTime(2026, 1, 1).AddDays(float i)
                    Amount = eur (decimal (-i - 1))
                    Payee = Some $"Perf {i}"
                    Memo = None
                    Category = if isHit then None else Some (category 44 "Andere Kategorie")
                    Splits =
                        if isHit then
                            [ { Target = ToCategory (hitCategory, "Kategorie 43"); Amount = eur -1m; Memo = None }
                              { Target = Uncategorized; Amount = eur (decimal (-i - 1) + 1m); Memo = None } ]
                        else
                            []
                    Cleared = Uncleared
                    TransferAccountId = None
                    TransferYnabTransactionId = None
                    Approved = true
                    FlagColor = None
                    FlagName = None
                }
                { EntryId = EntryId (Guid.NewGuid())
                  OccurredAt = DateTime(2026, 1, 1).AddDays(float i)
                  Actor = YnabMirror
                  Body = EntryObservedInYnab payload }

            let sw = System.Diagnostics.Stopwatch.StartNew()

            [ for i in 0 .. 999 -> makeEvent i ]
            |> appendAndApply
            |> Async.RunSynchronously

            let (LedgerAccountId rawAF) = pageTestAccountAF
            let query = { baseQuery (Some rawAF) with CategoryIds = [ catGuid 43 ] }
            let page = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously

            sw.Stop()

            Expect.equal page.TotalCount 3 "genau die drei Buchungen mit der gesuchten Split-Kategorie, unter 1000 auf demselben Konto"
            // "Etwa zwei Sekunden" (Task-Vorgabe): großzügige Grenze für eine
            // CI-Maschine; eine Überschreitung soll die Anzahl in einer späteren
            // Iteration senken, nicht den Test flackern lassen.
            Expect.isLessThan sw.Elapsed.TotalSeconds 2.0
                $"Seeding plus Abfrage über 1000 Buchungen blieb unter 2s (tatsächlich: {sw.Elapsed.TotalSeconds:F2}s)"

        // ============================================
        // Payee-Vorschläge falten wie der Payee-Filter (ledger-h4qtx, ADR 0037)
        // ============================================

        testCase "AC1+AC6: 'Rewe' und 'REWE' liefern einen Vorschlag mit Anzahl n+m, die Anzahl stimmt mit dem Filter-Treffer überein" <| fun () ->
            seedEntryFull pageTestAccountAG 220 -1m (DateTime(2026, 9, 1)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAG 221 -2m (DateTime(2026, 9, 2)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAG 222 -3m (DateTime(2026, 9, 3)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAG 223 -4m (DateTime(2026, 9, 4)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAG 224 -5m (DateTime(2026, 9, 5)) (Some "REWE") None None
            |> Async.RunSynchronously

            let (LedgerAccountId rawAG) = pageTestAccountAG
            let suggestions =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions (baseSuggestionQuery (Some rawAG))
                |> Async.RunSynchronously

            Expect.equal (List.length suggestions) 1
                "'Rewe' (2x) und 'REWE' (3x) gehören zu einem einzigen Vorschlag, nicht zwei getrennten"
            let suggestion = List.head suggestions
            Expect.equal suggestion.Count 5 "die Anzahl ist die Summe beider Schreibweisen (2 + 3)"
            Expect.equal suggestion.Payee "REWE" "AC3: die häufigere Schreibweise (3x) gewinnt gegenüber der selteneren (2x)"

            // AC6: die Anzahl am Vorschlag muss mit der Treffermenge übereinstimmen,
            // wenn genau dieser Payee als Filter gewählt wird.
            let filtered =
                Ledger.QueryHandlers.getLedgerEntriesPage { baseQuery (Some rawAG) with Payee = Some suggestion.Payee }
                |> Async.RunSynchronously
            Expect.equal filtered.TotalCount suggestion.Count
                "ein Filter auf die vorgeschlagene Schreibweise liefert genau so viele Buchungen, wie der Vorschlag verspricht"

        testCase "AC2 (Umlaut): 'MÜLLER GMBH' und 'Müller GmbH' liefern einen Vorschlag mit Anzahl n+m" <| fun () ->
            // Ohne ledger-k3wqn (ADR 0037, bb_lower) wäre dies rot: eine reine
            // COLLATE-NOCASE-Faltung erreicht Umlaute nicht.
            seedEntryFull pageTestAccountAH 225 -1m (DateTime(2026, 9, 1)) (Some "MÜLLER GMBH") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAH 226 -2m (DateTime(2026, 9, 2)) (Some "MÜLLER GMBH") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAH 227 -3m (DateTime(2026, 9, 3)) (Some "Müller GmbH") None None
            |> Async.RunSynchronously

            let (LedgerAccountId rawAH) = pageTestAccountAH
            let suggestions =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions (baseSuggestionQuery (Some rawAH))
                |> Async.RunSynchronously

            Expect.equal (List.length suggestions) 1
                "'MÜLLER GMBH' (2x) und 'Müller GmbH' (1x) gehören zu einem einzigen Vorschlag"
            let suggestion = List.head suggestions
            Expect.equal suggestion.Count 3 "die Anzahl ist die Summe beider Schreibweisen (2 + 1)"
            Expect.equal suggestion.Payee "MÜLLER GMBH" "die häufigere Schreibweise (2x) gewinnt"

        testCase "AC3: Anzeige-Schreibweise ist die häufigste, bei Gleichstand die lexikografisch kleinste nach Ordinalvergleich" <| fun () ->
            // Eindeutige Mehrheit: 'REWE' (3x) schlägt 'Rewe' (1x).
            seedEntryFull pageTestAccountAI 228 -1m (DateTime(2026, 9, 1)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 229 -2m (DateTime(2026, 9, 2)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 230 -3m (DateTime(2026, 9, 3)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 231 -4m (DateTime(2026, 9, 4)) (Some "Rewe") None None
            |> Async.RunSynchronously

            // Gleichstand: 'Netto' (2x) und 'netto' (2x), ordinal ist 'N' (0x4E) kleiner als
            // 'n' (0x6E), 'Netto' gewinnt also den Tiebreak, nicht die Mehrheit (es
            // gibt keine), und nicht 'zufällig' die zuerst gesäte Schreibweise.
            seedEntryFull pageTestAccountAI 232 -5m (DateTime(2026, 9, 5)) (Some "netto") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 233 -6m (DateTime(2026, 9, 6)) (Some "netto") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 234 -7m (DateTime(2026, 9, 7)) (Some "Netto") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAI 235 -8m (DateTime(2026, 9, 8)) (Some "Netto") None None
            |> Async.RunSynchronously

            let (LedgerAccountId rawAI) = pageTestAccountAI
            let suggestions =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions (baseSuggestionQuery (Some rawAI))
                |> Async.RunSynchronously

            let rewe = suggestions |> List.tryFind (fun s -> Persistence.searchFold s.Payee = "rewe")
            let netto = suggestions |> List.tryFind (fun s -> Persistence.searchFold s.Payee = "netto")

            Expect.equal (rewe |> Option.map (fun s -> s.Payee, s.Count)) (Some ("REWE", 4))
                "eindeutige Mehrheit: 'REWE' (3x) gewinnt gegen 'Rewe' (1x), Anzahl ist die Summe"
            Expect.equal (netto |> Option.map (fun s -> s.Payee, s.Count)) (Some ("Netto", 4))
                "Gleichstand (2x/2x): 'Netto' gewinnt den Ordinal-Tiebreak gegen 'netto' ('N' < 'n')"

        testCase "AC4: SQL-Zweig (ohne Betragsfilter) und F#-Zweig (mit Betragsfilter) liefern dieselbe Gruppierung und Anzeige-Schreibweise" <| fun () ->
            seedEntryFull pageTestAccountAJ 236 -1m (DateTime(2026, 9, 1)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAJ 237 -2m (DateTime(2026, 9, 2)) (Some "Rewe") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAJ 238 -3m (DateTime(2026, 9, 3)) (Some "REWE") None None
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAJ 239 -4m (DateTime(2026, 9, 4)) (Some "Aldi") None None
            |> Async.RunSynchronously

            let (LedgerAccountId rawAJ) = pageTestAccountAJ
            let sqlPath =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions (baseSuggestionQuery (Some rawAJ))
                |> Async.RunSynchronously
            // Ein Intervall, das alle vier Buchungen einschließt, zwingt den
            // F#-Zweig (`hasAmountFilter`, ADR 0017/0036), ohne den Treffer
            // einzuschränken.
            let amountPath =
                Ledger.QueryHandlers.getLedgerPayeeSuggestions
                    { baseSuggestionQuery (Some rawAJ) with AmountMin = Some -1000m; AmountMax = Some 0m }
                |> Async.RunSynchronously

            let sorted (suggestions: LedgerPayeeSuggestion list) =
                suggestions |> List.sortBy (fun s -> s.Payee) |> List.map (fun s -> s.Payee, s.Count)

            let expected = [ "Aldi", 1; "REWE", 3 ]
            Expect.equal (sorted sqlPath) expected "SQL-Zweig: 'REWE'/'Rewe' zu einem Vorschlag gefaltet, 'Aldi' für sich"
            Expect.equal (sorted amountPath) expected "F#-Zweig (Betragsfilter): dieselbe Gruppierung und Anzeige-Schreibweise wie der SQL-Zweig"

        testCase "AC5: loadPayeeSuggestions enthält keine eigene Groß-/Kleinschreibungs-Faltung, nur Core.searchFold" <| fun () ->
            // Quelltext-Zusicherung nach dem Muster von AC13: eine zweite,
            // eigenständige Normalisierung (statt der gemeinsamen `Core.searchFold`)
            // wäre hier still möglich, ohne dass ein Verhaltens-Test es zwingend
            // aufdeckt, solange sie zufällig dasselbe Ergebnis liefert.
            let testDir = __SOURCE_DIRECTORY__
            let repoRoot = Directory.GetParent(Directory.GetParent(testDir).FullName).FullName
            let persistenceFsPath = Path.Combine(repoRoot, "src", "Server", "Ledger", "Persistence.fs")
            let text = File.ReadAllText persistenceFsPath

            Expect.isFalse (Regex.IsMatch(text, @"\.ToLower\b|\.ToUpper\b|\.Replace\("))
                "keine eigene ToLower/ToUpper/Replace-Faltung in Ledger.Persistence, nur Core.searchFold (ADR 0037)"
    ]

// ============================================
// Kategorie voll qualifizieren (ledger-h6ptn, ADR 0039): der Lesepfad schlägt die
// Kategoriegruppe in `ledger_categories` nach, Events.fs bleibt unangetastet.
// ============================================

[<Tests>]
let categoryGroupLookupTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            seedAccounts () |> Async.RunSynchronously
            body ())

    testSequenced
    <| testList "Kassenbuch-Seite: Kategorie voll qualifizieren (ledger-h6ptn)" [

        testCase "getLedgerEntriesPage liefert den gespiegelten Gruppennamen für eine bekannte Kategorie" <| fun () ->
            let cat = category 90 "Lebensmittel"
            upsertCategories [ { Id = cat.CategoryId; Name = "Lebensmittel"; GroupName = "Alltag"; Available = None; Hidden = false } ]
            |> Async.RunSynchronously
            seedEntryFull pageTestAccountAK 500 -12m (DateTime(2026, 5, 1)) (Some "Rewe") None (Some cat)
            |> Async.RunSynchronously

            let (LedgerAccountId rawAK) = pageTestAccountAK
            let page = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawAK)) |> Async.RunSynchronously

            match page.Entries |> List.tryFind (fun e -> e.CategoryName = Some "Lebensmittel") with
            | Some view -> Expect.equal view.CategoryGroupName (Some "Alltag") "die gespiegelte Gruppe kommt in der Sicht an"
            | None -> failtest "Buchung mit der gesuchten Kategorie nicht in der Seite gefunden"

        testCase "getLedgerEntriesPage liefert None für eine nie gespiegelte Kategorie-Id" <| fun () ->
            let cat = category 91 "Nie gespiegelt"
            seedEntryFull pageTestAccountAK 501 -13m (DateTime(2026, 5, 2)) (Some "Aldi") None (Some cat)
            |> Async.RunSynchronously

            let (LedgerAccountId rawAK) = pageTestAccountAK
            let page = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawAK)) |> Async.RunSynchronously

            match page.Entries |> List.tryFind (fun e -> e.CategoryName = Some "Nie gespiegelt") with
            | Some view ->
                Expect.equal view.CategoryGroupName None
                    "ohne gespiegelte Kategorie gibt es keine Gruppe, aber der Blattname aus dem Ereignis bleibt (kein Absturz)"
                Expect.equal view.CategoryName (Some "Nie gespiegelt") "der Blattname aus dem Ereignis ist die Rückfallebene"
            | None -> failtest "Buchung mit der gesuchten Kategorie nicht in der Seite gefunden"

        testCase "Kategorie in YNAB gelöscht, aber vorher gespiegelt: die Buchung zeigt weiterhin die Gruppe" <| fun () ->
            // Fall 1 aus den Notes: ledger_categories löscht nie (ADR 0039, Punkt 3).
            let cat = category 92 "Streaming"
            seedEntryFull pageTestAccountAK 502 -9m (DateTime(2026, 5, 3)) (Some "Netflix") None (Some cat)
            |> Async.RunSynchronously
            let ynabCat : YnabCategory = { Id = cat.CategoryId; Name = "Streaming"; GroupName = "Freizeit"; Available = None; Hidden = false }
            upsertCategories [ ynabCat ] |> Async.RunSynchronously

            let (LedgerAccountId rawAK) = pageTestAccountAK
            let query = baseQuery (Some rawAK)
            let vorher = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            match vorher.Entries |> List.tryFind (fun e -> e.CategoryName = Some "Streaming") with
            | Some view -> Expect.equal view.CategoryGroupName (Some "Freizeit") "die Gruppe steht vor der Löschung"
            | None -> failtest "Buchung vor der Löschung nicht gefunden"

            // Ein neuer Kategorie-Lauf ohne "Streaming": die Zeile bleibt trotzdem stehen.
            let andereKategorie = category 93 "Andere Kategorie"
            upsertCategories [ { Id = andereKategorie.CategoryId; Name = "Andere Kategorie"; GroupName = "Sonstiges"; Available = None; Hidden = false } ]
            |> Async.RunSynchronously

            let nachher = Ledger.QueryHandlers.getLedgerEntriesPage query |> Async.RunSynchronously
            match nachher.Entries |> List.tryFind (fun e -> e.CategoryName = Some "Streaming") with
            | Some view ->
                Expect.equal view.CategoryGroupName (Some "Freizeit")
                    "eine in YNAB gelöschte, aber vorher gespiegelte Kategorie behält ihre Beschriftung (ADR 0039, Punkt 3)"
            | None -> failtest "Buchung nach dem zweiten Kategorie-Lauf nicht mehr gefunden"

        testCase "Buchung ohne Kategorie liefert CategoryGroupName = None, ohne dass der Lookup läuft oder wirft" <| fun () ->
            seedEntry pageTestAccountAK 503 -5m |> Async.RunSynchronously

            let (LedgerAccountId rawAK) = pageTestAccountAK
            let page = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawAK)) |> Async.RunSynchronously

            match page.Entries |> List.tryFind (fun e -> e.CategoryName = None && e.Splits = []) with
            | Some view -> Expect.equal view.CategoryGroupName None "ohne Kategorie gibt es nichts nachzuschlagen"
            | None -> failtest "Buchung ohne Kategorie nicht gefunden"

        testCase "toEntryView löst Kopf und ToCategory-Split-Ziele live aus categoryNames auf" <| fun () ->
            // Schützt ADR 0044 Punkt 3: nach einer Umbenennung zeigt die Liste den heutigen
            // Blattnamen, nicht den im Zustand veralteten.
            let alt = category 1 "Lebensmittel"
            let (YnabCategoryId altId) = alt.CategoryId
            let andere = category 2 "Haushalt"
            let (YnabCategoryId andereId) = andere.CategoryId
            let zeile target = { Target = target; Amount = eur -5m; Memo = None }
            let e =
                { entry 1 -10m with
                    Category = Some alt
                    Splits =
                        [ zeile (ToCategory (alt.CategoryId, "Lebensmittel"))
                          zeile (ToCategory (andere.CategoryId, "Haushalt"))
                          zeile Uncategorized ] }
            let names = Map.ofList [ altId, "Essen"; andereId, "   " ]
            let view = ReadModel.toEntryView names Map.empty MirroredFromYnab e

            Expect.equal view.CategoryName (Some "Essen") "Kopf wird live aufgelöst"
            Expect.equal
                (view.Splits |> List.map (fun s -> s.Target))
                [ ToCategory (alt.CategoryId, "Essen")
                  ToCategory (andere.CategoryId, "Haushalt")
                  Uncategorized ]
                "Split-Ziel live; leerer Name fällt zurück; Uncategorized unverändert"

        testCase "toEntryView fällt ohne Treffer auf den gespeicherten Namen zurück, ohne Kategorie bleibt None" <| fun () ->
            let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab (entry 1 -10m)
            Expect.equal view.CategoryName (Some "Lebensmittel") "Rückfall auf den gespeicherten Namen"
            let ohne = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab { entry 1 -10m with Category = None }
            Expect.equal ohne.CategoryName None "ohne Kategorie bleibt CategoryName None"

        testCase "getLedgerEntriesPage und getLedgerEntriesForAccount lösen den Blattnamen live aus ledger_categories auf" <| fun () ->
            // Schützt ADR 0044 Punkt 3 für beide Aufrufer.
            let cat = category 94 "Alter Name"
            seedEntryFull pageTestAccountAK 504 -7m (DateTime(2026, 5, 4)) (Some "Edeka") None (Some cat)
            |> Async.RunSynchronously
            upsertCategories [ { Id = cat.CategoryId; Name = "Neuer Name"; GroupName = "Alltag"; Available = None; Hidden = false } ]
            |> Async.RunSynchronously

            let (LedgerAccountId rawAK) = pageTestAccountAK
            let page = Ledger.QueryHandlers.getLedgerEntriesPage (baseQuery (Some rawAK)) |> Async.RunSynchronously
            Expect.isTrue
                (page.Entries |> List.exists (fun e -> e.CategoryName = Some "Neuer Name"))
                "getLedgerEntriesPage zeigt den heutigen Namen"
            Expect.isFalse
                (page.Entries |> List.exists (fun e -> e.CategoryName = Some "Alter Name"))
                "der veraltete gespeicherte Name taucht nicht mehr auf"

            let alle = Ledger.QueryHandlers.getLedgerEntriesForAccount rawAK |> Async.RunSynchronously
            Expect.isTrue
                (alle |> List.exists (fun e -> e.CategoryName = Some "Neuer Name"))
                "getLedgerEntriesForAccount zeigt den heutigen Namen"

        testCase "toEntryView bleibt rein: dieselbe Zuordnung liefert bei gleicher Eingabe immer dasselbe Ergebnis" <| fun () ->
            // AC: die Zuordnung kommt als Parameter herein, keine I/O in toEntryView
            // selbst, durch die Signatur erzwungen (ein Map-Parameter, kein Zugriff
            // auf die Datenbank innerhalb der Funktion).
            let groups = Map.ofList [ (let (YnabCategoryId id) = (category 1 "x").CategoryId in id), "Alltag" ]
            let view1 = ReadModel.toEntryView Map.empty groups MirroredFromYnab (entry 1 -42.5m)
            let view2 = ReadModel.toEntryView Map.empty groups MirroredFromYnab (entry 1 -42.5m)
            Expect.equal view1 view2 "reine Funktion: gleiche Eingabe, gleiches Ergebnis"
            Expect.equal view1.CategoryGroupName (Some "Alltag") "die übergebene Zuordnung wird tatsächlich benutzt"

            let leer = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab (entry 1 -42.5m)
            Expect.equal leer.CategoryGroupName None "eine leere Zuordnung ergibt keine Gruppe, keinen Fehler"

        testCase "SplitCategoryGroupNames hat je ToCategory-Zeile mit bekannter Gruppe genau einen Eintrag (ADR 0045)" <| fun () ->
            let idOf n = let (YnabCategoryId id) = (category n "x").CategoryId in id
            let zeile n name : TransactionSplit =
                { Target = ToCategory ((category n name).CategoryId, name)
                  Amount = { Amount = -10m; Currency = "EUR" }
                  Memo = None }
            let groups = Map.ofList [ idOf 1, "Alltag"; idOf 2, "Freizeit" ]
            let view =
                ReadModel.toEntryView Map.empty groups MirroredFromYnab
                    { entry 1 -20m with Category = None; Splits = [ zeile 1 "Lebensmittel"; zeile 2 "Kino" ] }
            Expect.equal view.SplitCategoryGroupNames (Map.ofList [ idOf 1, "Alltag"; idOf 2, "Freizeit" ])
                "genau zwei Einträge, je mit der richtigen Gruppe"

        testCase "SplitCategoryGroupNames lässt unbekannte und leere Gruppen weg, kein None und kein leerer String" <| fun () ->
            let idOf n = let (YnabCategoryId id) = (category n "x").CategoryId in id
            let zeile n : TransactionSplit =
                { Target = ToCategory ((category n "Blatt").CategoryId, "Blatt")
                  Amount = { Amount = -10m; Currency = "EUR" }
                  Memo = None }
            let groups = Map.ofList [ idOf 1, "Alltag"; idOf 2, "   "; idOf 3, "" ]
            let view =
                ReadModel.toEntryView Map.empty groups MirroredFromYnab
                    { entry 1 -20m with Category = None; Splits = [ zeile 1; zeile 2; zeile 3; zeile 4 ] }
            Expect.equal view.SplitCategoryGroupNames (Map.ofList [ idOf 1, "Alltag" ])
                "nur die bekannte, nicht leere Gruppe hat einen Eintrag"

        testCase "SplitCategoryGroupNames bekommt für Übertrag und offene Zeilen nie einen Eintrag" <| fun () ->
            let id = Guid.NewGuid()
            let groups = Map.ofList [ id, "Alltag" ]
            let transfer : TransactionSplit =
                { Target = ToTransfer (YnabAccountId id, "Tagesgeld")
                  Amount = { Amount = -10m; Currency = "EUR" }
                  Memo = None }
            let offen : TransactionSplit = { transfer with Target = Uncategorized }
            let view =
                ReadModel.toEntryView Map.empty groups MirroredFromYnab
                    { entry 1 -20m with Category = None; Splits = [ transfer; offen ] }
            Expect.isEmpty (Map.toList view.SplitCategoryGroupNames) "auch bei gleicher Guid kein Eintrag"

        testCase "SplitCategoryGroupNames ist leer bei einer Buchung ohne Splits" <| fun () ->
            let view = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab (entry 1 -42.5m)
            Expect.isEmpty (Map.toList view.SplitCategoryGroupNames) "keine Splits, keine Einträge"
    ]

/// `IsTransfer` der Kassenbuch-Zeile (ledger-n4vte, ADR 0048): ein Übertrag trägt ein
/// Ziel und ist nicht "ohne Kategorie". Erkannt am rohen `TransferAccountId` oder an
/// der aufgelösten Gegenbuchung `TransferEntryId`, jede Seite reicht allein.
let private isTransferTests =
    let view (e: Entry) = ReadModel.toEntryView Map.empty Map.empty MirroredFromYnab e
    testList "toEntryView: IsTransfer" [

        test "Nur TransferAccountId gesetzt ist ein Übertrag" {
            let e = { entry 1 -10m with Category = None; TransferAccountId = Some (ynabAccount 2) }
            Expect.isTrue (view e).IsTransfer "Das rohe Übertragskonto reicht"
        }

        test "Nur TransferEntryId gesetzt ist ein Übertrag" {
            let e = { entry 1 -10m with Category = None; TransferEntryId = Some (EntryId (entryGuid 2)) }
            Expect.isTrue (view e).IsTransfer "Die aufgelöste Gegenbuchung reicht"
        }

        test "Weder TransferAccountId noch TransferEntryId ist kein Übertrag" {
            let e = { entry 1 -10m with Category = None }
            Expect.isFalse (view e).IsTransfer "Eine einfache Buchung ohne Ziel ist kein Übertrag"
        }
    ]

[<Tests>]
let ledgerPageTests =
    testList
        "Kassenbuch-Seite"
        [ historyLanguageTests
          readModelTests
          balanceInputsTests
          reducerTests
          erstlaufTests
          freshnessTests
          visibilityTests
          isTransferTests ]
