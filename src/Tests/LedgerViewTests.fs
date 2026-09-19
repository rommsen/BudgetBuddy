module LedgerViewTests

// Reine Bausteine der Kassenbuch-Seite (ledger-b1xwe): Konto-Kuerzel, Datums-Gruppierung
// und die Kontostaende-Karten. Getestet wie in `SyncFlowViewTests.fs`: die reine Logik
// direkt, ohne DOM/React zu rendern — das ist unter `dotnet test` ohnehin nicht moeglich
// (Fable-only), die reine Logik aber schon.

open System
open System.Reflection
open System.Runtime.CompilerServices
open Expecto
open Shared.Domain
open Components.Ledger.Types
open Components.Ledger.View

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private accountId (n: int) = Guid.Parse $"0195cc00-0000-7000-8000-0000000000{n:D2}"

let private entryView (accId: Guid) (date: DateTime) (payee: string) : LedgerEntryView = {
    Id = Guid.NewGuid()
    AccountId = accId
    Date = date
    Amount = eur -10m
    Payee = Some payee
    Memo = None
    CategoryName = None
    Splits = []
    Origin = MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = false
    Flag = NoFlag
}

let private historyLineAt (sequence: int) : LedgerHistoryLine = {
    Sequence = sequence
    OccurredAt = DateTime(2026, 3, 1).AddDays(float sequence)
    Origin = MirroredFromYnab
    Description = $"Ereignis {sequence}"
}

let private accountView (n: int) (name: string) : LedgerAccountView = {
    Id = accountId n
    Name = name
    Kind = LedgerBankAccount
    YnabAccountId = None
    Closed = false
}

let private abbreviationTests =
    testList "accountAbbreviation" [

        test "Nimmt das erste Wort, bis zu vier Zeichen, gross geschrieben" {
            Expect.equal (accountAbbreviation "ING Extra") "ING" "Kurzes erstes Wort bleibt ganz"
            Expect.equal (accountAbbreviation "Girokonto Comdirect") "GIRO" "Langes erstes Wort wird auf vier Zeichen gekappt"
            Expect.equal (accountAbbreviation "Bar") "BAR" "Einzelnes Wort"
        }

        test "Leerer oder nur-Leerzeichen-Name ergibt ein Platzhalter-Kuerzel, keinen Crash" {
            Expect.equal (accountAbbreviation "   ") "?" "Kein IndexOutOfRange auf leerem Namen"
            Expect.equal (accountAbbreviation "") "?" "Auch der ganz leere String"
        }
    ]

let private groupByDateTests =
    testList "groupByDate" [

        test "Aufeinanderfolgende Buchungen desselben Datums werden zu einer Gruppe" {
            let d1 = DateTime(2026, 3, 10)
            let d2 = DateTime(2026, 3, 9)
            let entries =
                [ entryView (accountId 1) d1 "Rewe"
                  entryView (accountId 1) d1 "Edeka"
                  entryView (accountId 1) d2 "Aldi" ]

            let groups = groupByDate entries
            Expect.equal (groups |> List.map fst) [ d1; d2 ] "Zwei Gruppen, in Listenreihenfolge"
            Expect.equal (groups |> List.item 0 |> snd |> List.length) 2 "Erste Gruppe traegt beide Maerz-10-Buchungen"
            Expect.equal
                (groups |> List.item 0 |> snd |> List.map (fun e -> e.Payee))
                [ Some "Rewe"; Some "Edeka" ]
                "Reihenfolge innerhalb der Gruppe bleibt erhalten"
        }

        test "Dasselbe Datum an nicht benachbarter Stelle bildet zwei getrennte Gruppen" {
            // Bewusst: die Liste ist server-sortiert (neueste zuerst), ein
            // globales groupBy wuerde diese Sortierung verlassen.
            let d = DateTime(2026, 3, 10)
            let other = DateTime(2026, 3, 9)
            let entries = [ entryView (accountId 1) d "A"; entryView (accountId 1) other "B"; entryView (accountId 1) d "C" ]
            let groups = groupByDate entries
            Expect.equal (List.length groups) 3 "Drei Gruppen, nicht zwei: das zweite d-10 ist nicht benachbart"
        }

        test "Leere Liste ergibt keine Gruppen" {
            Expect.equal (groupByDate []) [] "Nichts zu gruppieren"
        }
    ]

let private formatEntryDateTests =
    testList "formatEntryDate" [

        test "Dasselbe Datum wie heute ergibt 'Heute'" {
            let today = DateTime(2026, 9, 12)
            Expect.equal (formatEntryDate today today) "Heute" "Kalendertag-Vergleich, nicht Uhrzeit"
        }

        test "Heute mit einer Uhrzeit gt Null zaehlt trotzdem als 'Heute'" {
            // Regression: ein reiner Zeitstempel-Vergleich (statt .Date) wuerde hier
            // faelschlich einen Tagesunterschied sehen.
            let today = DateTime(2026, 9, 12)
            let withTime = DateTime(2026, 9, 12, 23, 59, 0)
            Expect.equal (formatEntryDate today withTime) "Heute" "Nur das Kalenderdatum zaehlt"
        }

        test "Ein Tag vor heute ergibt 'Gestern'" {
            let today = DateTime(2026, 9, 12)
            let yesterday = DateTime(2026, 9, 11)
            Expect.equal (formatEntryDate today yesterday) "Gestern" "Ein Tag Differenz"
        }

        test "Aeltere Daten stehen als TT.MM. ohne Jahr" {
            let today = DateTime(2026, 9, 12)
            let older = DateTime(2026, 9, 8)
            Expect.equal (formatEntryDate today older) "08.09." "Kein Jahr in der Liste"
        }

        test "Zukuenftige Daten sind ebenfalls TT.MM., kein 'Heute'/'Gestern'" {
            let today = DateTime(2026, 9, 12)
            let future = DateTime(2026, 9, 13)
            Expect.equal (formatEntryDate today future) "13.09." "Nur -1 und 0 Tage sind Sonderfaelle"
        }

        test "Jahreswechsel: 'Gestern' bleibt korrekt ueber den 1. Januar hinweg" {
            let today = DateTime(2027, 1, 1)
            let yesterday = DateTime(2026, 12, 31)
            Expect.equal (formatEntryDate today yesterday) "Gestern" "DateTime.Date-Differenz, kein Tag-des-Jahres-Vergleich"
        }
    ]

let private accountCardsTests =
    testList "buildAccountCards" [

        test "'Alle Konten' steht zuerst und traegt die Summe aller Kontostaende" {
            let accounts = [ accountView 1 "ING"; accountView 2 "Comdirect" ]
            let balances =
                [ { AccountId = accountId 1; Balance = eur 100m }
                  { AccountId = accountId 2; Balance = eur -20m } ]

            let cards = buildAccountCards accounts balances
            Expect.equal (cards |> List.map (fun c -> c.Filter)) [ AllAccounts; OneAccount (accountId 1); OneAccount (accountId 2) ]
                "Alle Konten zuerst, danach die Konten in ihrer Reihenfolge"
            Expect.equal (cards |> List.head).Balance (eur 80m) "100 - 20"
        }

        test "Ein Konto ohne geladenen Stand zeigt Null, keinen Fehler" {
            let accounts = [ accountView 1 "ING" ]
            let cards = buildAccountCards accounts []
            Expect.equal (cards |> List.item 1).Balance (eur 0m) "Kein Stand geladen: Null statt Crash"
            Expect.equal (cards |> List.head).Balance (eur 0m) "Auch 'Alle Konten' ist dann Null"
        }
    ]

/// Entscheidung Roman, 2026-09-18 (`ledger-d4kx8`, Variante 2): eingeblendet zählen
/// gelöschte Buchungen in Trefferzahl UND Summe (Code bleibt, nur der Entwurf wird
/// angepasst). Die Zeile über der Liste sagt das jetzt explizit, im Stil "13
/// Buchungen, davon 1 in YNAB gelöscht". Ausgeblendet bleibt der Satz aus
/// `ledger-p3n8k` unverändert.
let private deletedCountTextTests =
    testList "deletedCountText" [

        test "Ausgeblendet nennt Zahl und Zustand, Singular bei genau einer Buchung" {
            Expect.equal (deletedCountText 0 1 false) "1 gelöschte Buchung ausgeblendet" "Singular"
            Expect.equal (deletedCountText 0 3 false) "3 gelöschte Buchungen ausgeblendet" "Plural"
        }

        test "Eingeblendet nennt die Trefferzahl und wie viele davon gelöscht sind" {
            Expect.equal
                (deletedCountText 13 1 true)
                "13 Buchungen, davon 1 in YNAB gelöscht"
                "Beispiel aus der Entscheidung"
        }

        test "Eingeblendet: Singular bei genau einer Buchung insgesamt" {
            Expect.equal
                (deletedCountText 1 1 true)
                "1 Buchung, davon 1 in YNAB gelöscht"
                "Die Trefferzahl bekommt Singular, nicht die Zahl der gelöschten"
        }
    ]

let private needsReviewChipLabelTests =
    testList "needsReviewChipLabel" [

        test "Zeigt die Zahl in Klammern, auch bei Null (ADR 0032)" {
            Expect.equal (needsReviewChipLabel 0) "Zu prüfen (0)" "Kein Sonderfall für Null"
            Expect.equal (needsReviewChipLabel 7) "Zu prüfen (7)" "Die Zahl aus dem Server-Wert"
        }
    ]

let private historyLinesForDisplayTests =
    testList "historyLinesForDisplay" [

        test "Zeigt die neueste Zeile oben, der Lesepfad liefert aber aufsteigend" {
            // ledger-h9tq2, Entscheidung Roman 2026-09-18: die Drehung sitzt in der
            // Anzeige. Ohne `List.rev` in `historyLinesForDisplay` bliebe die Liste
            // aufsteigend und dieser Test scheiterte: [1; 2; 3] statt [3; 2; 1].
            let ascending = [ historyLineAt 1; historyLineAt 2; historyLineAt 3 ]
            let displayed = historyLinesForDisplay ascending
            Expect.equal
                (displayed |> List.map (fun l -> l.Sequence))
                [ 3; 2; 1 ]
                "Neueste Zeile (höchste Sequenznummer) zuerst"
        }

        test "Die laufende Nummer je Zeile bleibt die aus dem Ereignis-Log, sie wird nicht neu vergeben" {
            let ascending = [ historyLineAt 1; historyLineAt 2; historyLineAt 3 ]
            let displayed = historyLinesForDisplay ascending
            Expect.equal
                (displayed |> List.map (fun l -> l.Sequence))
                (ascending |> List.map (fun l -> l.Sequence) |> List.rev)
                "Dieselben Sequenznummern wie im Log, nur die Reihenfolge dreht sich"
        }

        test "Eine leere Historie bleibt leer" {
            Expect.equal (historyLinesForDisplay []) [] "Nichts zu drehen"
        }
    ]

let private moduleStartupTests =
    testList "Modul-Startup (design-system-bq4tz)" [

        test "Der CLR-Typinitialisierer von Components.Ledger.View wirft keine TypeInitializationException" {
            // Regression: `deletedBadge` war ein parameterloser Top-Level-`let`-Wert, also
            // ein Feld. Ein Feldzugriff löst unter .NET F#s Modul-Startup-Code aus, der
            // ALLE Top-Level-Werte des Moduls initialisiert - hier `Badge.view { Badge.defaultProps
            // with ... }`, was unter echtem .NET (kein Fable/Feliz-Laufzeitkontext) eine
            // InvalidCastException wirft (ein Tuple lässt sich nicht auf Feliz.IReactProperty
            // casten). `deletedBadge` ist privat, deshalb erzwingt dieser Test den
            // Typinitialisierer direkt per Reflection (RunClassConstructor) statt ihn
            // über einen öffentlichen Aufruf indirekt zu triggern. Das bleibt auch nach dem
            // Fix ein sinnvoller Wächter: er scheitert wieder, sollte je ein neuer eager
            // ausgewerteter Top-Level-Wert in dieses Modul einziehen, unabhängig davon, ob es
            // wieder `deletedBadge` heißt oder anders.
            let startupType =
                Assembly.GetAssembly(typeof<Model>).GetType("<StartupCode$Client>.$Components.Ledger.View")
            RuntimeHelpers.RunClassConstructor(startupType.TypeHandle)
            Expect.isTrue true "Der Modul-Start lief durch, ohne dass ein Feld eager eine Feliz-Komponente gerendert hat"
        }
    ]

[<Tests>]
let ledgerViewTests =
    testList
        "Kassenbuch-Seite: View-Bausteine"
        [ abbreviationTests
          groupByDateTests
          formatEntryDateTests
          accountCardsTests
          deletedCountTextTests
          needsReviewChipLabelTests
          historyLinesForDisplayTests
          moduleStartupTests ]
