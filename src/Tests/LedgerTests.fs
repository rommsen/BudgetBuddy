module LedgerTests

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabClient

// ============================================
// Bausteine
// ============================================

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }

/// Steht für `triggerMirror` in Tests, die den Spiegel-Auslöser nach einem Push
/// (ADR 0014 Punkt 2, ledger-h4p6c) nicht selbst prüfen. Die eigenen Tests dafür
/// stehen bei `addCashExpense`/`pushComdirectImports`.
let private noTrigger () = ()

let private testAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-000000000001")
let private otherAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-000000000002")

let private category (name: string) = {
    CategoryId = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000aa")
    CategoryName = name
}

let private observedPayload (ynabTxId: string) (importId: string option) : EntryObservedInYnabPayload = {
    AccountId = testAccountId
    YnabTransactionId = ynabTxId
    ImportId = importId
    Date = DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -42.50m
    Payee = Some "Rewe"
    Memo = Some "Wocheneinkauf"
    Category = Some (category "Lebensmittel")
    Splits = []
    Cleared = Uncleared
    TransferAccountId = None
    TransferYnabTransactionId = None
}

let private observedEvent (entryId: EntryId) (payload: EntryObservedInYnabPayload) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = DateTime(2026, 1, 16, 8, 30, 0, DateTimeKind.Utc)
    Actor = YnabMirror
    Body = EntryObservedInYnab payload
}

let private eventOn (entryId: EntryId) (actor: Actor) (body: LedgerEventBody) : LedgerEvent = {
    EntryId = entryId
    OccurredAt = DateTime(2026, 1, 17, 9, 0, 0, DateTimeKind.Utc)
    Actor = actor
    Body = body
}

let private detailsChange = EntryDetailsChangeObservedInYnab {
    OldDate = DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    NewDate = DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc)
    OldAmount = eur -42.50m
    NewAmount = eur -43.10m
    OldPayee = Some "Rewe"
    NewPayee = Some "REWE Markt"
    OldMemo = Some "Wocheneinkauf"
    NewMemo = None
    OldAccountId = testAccountId
    NewAccountId = otherAccountId
}

let private splitLines = [
    { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000bb"), "Haushalt")
      Amount = eur -30.00m
      Memo = Some "Putzzeug" }
    { Target = ToTransfer (YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000cc"), "Bar")
      Amount = eur -13.10m
      Memo = None }
]

let private categorizationChange = EntryCategorizationChangeObservedInYnab {
    OldCategory = Some (category "Lebensmittel")
    NewCategory = None
    OldSplits = []
    NewSplits = splitLines
}

let private clearedChange = EntryClearedStateChangeObservedInYnab {
    OldCleared = Uncleared
    NewCleared = Cleared
}

let private categoryDrogerie = category "Drogerie"

let private categoryHaushalt = {
    CategoryId = YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000ee")
    CategoryName = "Haushalt"
}

let private importedFromComdirectPayload (importId: string) (comdirectRef: string) : EntryImportedFromComdirectPayload = {
    AccountId = testAccountId
    ImportId = importId
    ComdirectReference = comdirectRef
    Date = DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -17.40m
    Payee = Some "dm"
    Memo = None
    Cleared = Uncleared
}

let private cashExpenseAddedPayload : CashExpenseAddedPayload = {
    AccountId = testAccountId
    Date = DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
    Amount = eur -8.00m
    Payee = Some "Baecker"
    Memo = None
    Category = None
    Cleared = Uncleared
}

let private categorizedByRule = EntryCategorizedByRule {
    RuleName = "dm -> Drogerie"
    Category = Some categoryDrogerie
    Splits = []
    PayeeOverride = None
}

let private recategorized = EntryRecategorized {
    OldCategory = Some categoryDrogerie
    NewCategory = Some categoryHaushalt
    OldSplits = []
    NewSplits = []
    OldPayee = Some "dm"
    NewPayee = Some "dm-Filiale"
}

let private entryIdText (EntryId id) = id.ToString()

// ============================================
// Reine Faltung (Domain)
// ============================================

[<Tests>]
let ledgerDomainTests =
    testList "Ledger Domain (apply)" [

        testCase "Buchungs-Id ist eine Guid der Version 7" <| fun () ->
            let (EntryId id) = newEntryId ()
            Expect.equal id.Version 7 "ADR 0015 verlangt Guid.CreateVersion7()"

        testCase "Erstbeobachtung erzeugt die Buchung aus dem Snapshot" <| fun () ->
            let entryId = newEntryId ()
            let payload = observedPayload "ynab-1" (Some "import-1")
            let entry = apply None (observedEvent entryId payload)

            Expect.equal entry.Id entryId "Buchung traegt die eigene Id"
            Expect.equal entry.AccountId payload.AccountId "Konto uebernommen"
            Expect.equal entry.Date payload.Date "Datum uebernommen"
            Expect.equal entry.Amount payload.Amount "Betrag uebernommen"
            Expect.equal entry.Payee payload.Payee "Payee uebernommen"
            Expect.equal entry.Memo payload.Memo "Memo uebernommen"
            Expect.equal entry.Category payload.Category "Kategorie uebernommen"
            Expect.equal entry.Splits payload.Splits "Splits uebernommen"
            Expect.equal entry.Cleared payload.Cleared "Cleared-Status uebernommen"
            Expect.isNone entry.TransferEntryId "Uebertrags-Verknuepfung erst spaeter aufgeloest"
            Expect.isFalse entry.DeletedInYnab "frisch beobachtete Buchung ist nicht geloescht"

        testCase "Kopfdaten-Beobachtung uebernimmt die neuen Werte, auch das Konto" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-2" None))
            let after = apply (Some before) (eventOn entryId YnabMirror detailsChange)

            Expect.equal after.Date (DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc)) "neues Datum"
            Expect.equal after.Amount (eur -43.10m) "neuer Betrag"
            Expect.equal after.Payee (Some "REWE Markt") "neuer Payee"
            Expect.equal after.Memo None "Memo geleert"
            Expect.equal after.AccountId otherAccountId "auf anderes Konto verschoben"
            Expect.equal after.Category before.Category "Kategorie bleibt unberuehrt"

        testCase "Kategorisierungs-Beobachtung ersetzt Kategorie und Splits" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-3" None))
            let after = apply (Some before) (eventOn entryId YnabMirror categorizationChange)

            Expect.isNone after.Category "Kategorie entfaellt zugunsten der Splits"
            Expect.equal after.Splits splitLines "Splits uebernommen"
            Expect.equal after.Amount before.Amount "Kopfdaten bleiben unberuehrt"

        testCase "Cleared-Beobachtung aendert nur den Cleared-Status" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-4" None))
            let after = apply (Some before) (eventOn entryId YnabMirror clearedChange)

            Expect.equal after.Cleared Cleared "neuer Cleared-Status"
            Expect.equal { after with Cleared = before.Cleared } before "sonst nichts geaendert"

        testCase "Loeschung setzt das Flag, die Buchung bleibt erhalten" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-5" None))
            let after = apply (Some before) (eventOn entryId YnabMirror EntryDeletionObservedInYnab)

            Expect.isTrue after.DeletedInYnab "in YNAB geloescht gesehen"
            Expect.equal after.Amount before.Amount "Betrag bleibt lesbar"
            Expect.equal after.Payee before.Payee "Payee bleibt lesbar"

        testCase "Aenderungs-Ereignis ohne Erstbeobachtung wird laut abgelehnt" <| fun () ->
            let entryId = newEntryId ()
            Expect.throws
                (fun () -> apply None (eventOn entryId YnabMirror clearedChange) |> ignore)
                "ein Aenderungs-Ereignis ohne vorherige Buchung ist ein kaputtes Log"

        testCase "Comdirect-Import erzeugt eine kategorielose Buchung" <| fun () ->
            let entryId = newEntryId ()
            let payload = importedFromComdirectPayload "import-10" "comdirect-10"
            let entry =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect payload))

            Expect.equal entry.AccountId payload.AccountId "Konto uebernommen"
            Expect.equal entry.Amount payload.Amount "Betrag uebernommen"
            Expect.equal entry.Payee payload.Payee "Payee uebernommen"
            Expect.isNone entry.Category "kategorielos: die Regel entscheidet in einem eigenen Ereignis"
            Expect.isEmpty entry.Splits "keine Splits bei der Entstehung"
            Expect.isFalse entry.DeletedInYnab "frisch entstandene Buchung ist nicht geloescht"

        testCase "Quick Add erzeugt eine Buchung mit optionaler Kategorie" <| fun () ->
            let entryId = newEntryId ()
            let payload = { cashExpenseAddedPayload with Category = Some categoryHaushalt }
            let entry = apply None (eventOn entryId QuickAdd (CashExpenseAdded payload))

            Expect.equal entry.AccountId payload.AccountId "Konto uebernommen"
            Expect.equal entry.Amount payload.Amount "Betrag uebernommen"
            Expect.equal entry.Category (Some categoryHaushalt) "Quick Add darf sofort kategorisieren"
            Expect.isEmpty entry.Splits "keine Splits bei der Entstehung"

        testCase "Regel kategorisiert eine unkategorisierte Buchung" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-11" "comdirect-11")))
            let after = apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) categorizedByRule)

            Expect.equal after.Category (Some categoryDrogerie) "Kategorie von der Regel uebernommen"
            Expect.isEmpty after.Splits "keine Splits bei Kategorie-Zuordnung"
            Expect.equal after.Payee uncategorized.Payee "kein PayeeOverride gesetzt, Payee bleibt"

        testCase "Regel mit PayeeOverride ersetzt den Payee" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-11o" "comdirect-11o")))
            let withOverride =
                EntryCategorizedByRule { RuleName = "dm -> Drogerie"; Category = Some categoryDrogerie; Splits = []; PayeeOverride = Some "dm Drogeriemarkt" }
            let after = apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) withOverride)

            Expect.equal after.Payee (Some "dm Drogeriemarkt") "PayeeOverride ersetzt den Payee"

        testCase "Regel schlaegt fehl auf einer bereits kategorisierten Buchung" <| fun () ->
            let entryId = newEntryId ()
            let categorized = apply None (observedEvent entryId (observedPayload "ynab-12" None))
            Expect.throws
                (fun () -> apply (Some categorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) categorizedByRule) |> ignore)
                "EntryCategorizedByRule ist nur auf einer noch unkategorisierten Buchung zulaessig"

        testCase "Regel: Kategorie und Splits schliessen sich aus" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-13" "comdirect-13")))
            let invalid =
                EntryCategorizedByRule { RuleName = "kaputte Regel"; Category = Some categoryDrogerie; Splits = splitLines; PayeeOverride = None }
            Expect.throws
                (fun () -> apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) invalid) |> ignore)
                "Kategorie und Splits schliessen sich aus, auch bei EntryCategorizedByRule"

        testCase "Regel: Splits mit falscher Summe werden abgelehnt (mkSplits)" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-13b" "comdirect-13b")))
            // importedFromComdirectPayload traegt -17,40 EUR; splitLines summiert auf -43,10 EUR.
            let wrongSum =
                EntryCategorizedByRule { RuleName = "Summe passt nicht"; Category = None; Splits = splitLines; PayeeOverride = None }
            Expect.throws
                (fun () -> apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) wrongSum) |> ignore)
                "mkSplits: die vorzeichenrichtige Summe der Splits muss dem Buchungsbetrag entsprechen"

        testCase "Regel: eine einzelne Split-Zeile wird abgelehnt (mkSplits: mindestens zwei Zeilen)" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-13c" "comdirect-13c")))
            let singleLine = [
                { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000ff"), "Einzelposten")
                  Amount = uncategorized.Amount
                  Memo = None }
            ]
            let oneLine =
                EntryCategorizedByRule { RuleName = "nur eine Zeile"; Category = None; Splits = singleLine; PayeeOverride = None }
            Expect.throws
                (fun () -> apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) oneLine) |> ignore)
                "mkSplits: ein Split von nur einer Zeile ist keiner"

        testCase "Regel: gueltige Splits werden uebernommen (mkSplits erfuellt)" <| fun () ->
            let entryId = newEntryId ()
            let uncategorized =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-13d" "comdirect-13d")))
            let validSplits = [
                { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000bb"), "Haushalt")
                  Amount = eur -10.00m
                  Memo = None }
                { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000cc"), "Sonstiges")
                  Amount = eur -7.40m
                  Memo = None }
            ]
            let valid =
                EntryCategorizedByRule { RuleName = "gueltige Splits"; Category = None; Splits = validSplits; PayeeOverride = None }
            let after = apply (Some uncategorized) (eventOn entryId (Rule (RuleId (Guid.NewGuid()))) valid)
            Expect.equal after.Splits validSplits "gueltige Splits (mkSplits erfuellt) werden uebernommen"

        testCase "Recategorized uebernimmt neue Kategorie, Splits und Payee" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-14" None))
            let after = apply (Some before) (eventOn entryId UserInBb recategorized)

            Expect.equal after.Category (Some categoryHaushalt) "neue Kategorie uebernommen"
            Expect.isEmpty after.Splits "keine Splits nach dieser Umkategorisierung"
            Expect.equal after.Payee (Some "dm-Filiale") "neuer Payee uebernommen"

        testCase "Recategorized: Splits mit falscher Summe werden abgelehnt (mkSplits)" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-14b" None))
            // observedPayload traegt -42,50 EUR; splitLines summiert auf -43,10 EUR.
            let wrongSum =
                EntryRecategorized {
                    OldCategory = before.Category
                    NewCategory = None
                    OldSplits = []
                    NewSplits = splitLines
                    OldPayee = before.Payee
                    NewPayee = before.Payee
                }
            Expect.throws
                (fun () -> apply (Some before) (eventOn entryId UserInBb wrongSum) |> ignore)
                "mkSplits: die Summe der neuen Splits muss dem Buchungsbetrag entsprechen"

        testCase "Recategorized: eine einzelne Split-Zeile wird abgelehnt (mkSplits: mindestens zwei Zeilen)" <| fun () ->
            let entryId = newEntryId ()
            let before = apply None (observedEvent entryId (observedPayload "ynab-14c" None))
            let singleLine = [
                { Target = ToCategory (YnabCategoryId (Guid.Parse "0195c000-0000-7000-8000-0000000000ff"), "Einzelposten")
                  Amount = before.Amount
                  Memo = None }
            ]
            let oneLine =
                EntryRecategorized {
                    OldCategory = before.Category
                    NewCategory = None
                    OldSplits = []
                    NewSplits = singleLine
                    OldPayee = before.Payee
                    NewPayee = before.Payee
                }
            Expect.throws
                (fun () -> apply (Some before) (eventOn entryId UserInBb oneLine) |> ignore)
                "mkSplits: ein Split von nur einer Zeile ist keiner"

        testCase "EntryLinkedToYnab veraendert die Buchung nicht, verlangt aber eine bestehende" <| fun () ->
            let entryId = newEntryId ()
            let before =
                apply None (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-15" "comdirect-15")))
            let after =
                apply (Some before) (eventOn entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-15" }))

            Expect.equal after before "EntryLinkedToYnab wirkt nur auf die Fremd-Id-Tabelle, nicht auf den Entry-Zustand"

            let entryId2 = newEntryId ()
            Expect.throws
                (fun () -> apply None (eventOn entryId2 YnabMirror (EntryLinkedToYnab { YnabTransactionId = "ynab-16" })) |> ignore)
                "EntryLinkedToYnab ohne vorangehende Entstehung ist ein kaputtes Log"

        testCase "zwei Entstehungs-Ereignisse auf derselben Buchung schlagen fehl" <| fun () ->
            let creationBodies () = [
                EntryObservedInYnab (observedPayload "ynab-dup" None)
                EntryImportedFromComdirect (importedFromComdirectPayload "import-dup" "comdirect-dup")
                CashExpenseAdded cashExpenseAddedPayload
            ]

            for firstBody in creationBodies () do
                for secondBody in creationBodies () do
                    let entryId = newEntryId ()
                    let first = apply None (eventOn entryId UserInBb firstBody)
                    Expect.throws
                        (fun () -> apply (Some first) (eventOn entryId UserInBb secondBody) |> ignore)
                        $"ein zweites Entstehungs-Ereignis ({eventTypeName firstBody} gefolgt von {eventTypeName secondBody}) darf nicht durchgehen"

        testCase "replay faltet eine ganze Historie" <| fun () ->
            let entryId = newEntryId ()
            let history = [
                observedEvent entryId (observedPayload "ynab-6" None)
                eventOn entryId YnabMirror categorizationChange
                eventOn entryId YnabMirror clearedChange
                eventOn entryId YnabMirror EntryDeletionObservedInYnab
            ]
            match replay history with
            | None -> failtest "Historie ergab keinen Zustand"
            | Some entry ->
                Expect.equal entry.Splits splitLines "Splits aus dem zweiten Ereignis"
                Expect.equal entry.Cleared Cleared "Cleared aus dem dritten Ereignis"
                Expect.isTrue entry.DeletedInYnab "Loeschung aus dem vierten Ereignis"

        testCase "jedes Ereignis traegt Typname und Schema-Version" <| fun () ->
            let bodies = [
                EntryObservedInYnab (observedPayload "ynab-7" None)
                detailsChange
                categorizationChange
                clearedChange
                EntryDeletionObservedInYnab
                EntryImportedFromComdirect (importedFromComdirectPayload "import-7" "comdirect-7")
                categorizedByRule
                recategorized
                CashExpenseAdded cashExpenseAddedPayload
                EntryLinkedToYnab { YnabTransactionId = "ynab-7-linked" }
            ]
            let names = bodies |> List.map eventTypeName
            Expect.equal
                names
                [ "EntryObservedInYnab"
                  "EntryDetailsChangeObservedInYnab"
                  "EntryCategorizationChangeObservedInYnab"
                  "EntryClearedStateChangeObservedInYnab"
                  "EntryDeletionObservedInYnab"
                  "EntryImportedFromComdirect"
                  "EntryCategorizedByRule"
                  "EntryRecategorized"
                  "CashExpenseAdded"
                  "EntryLinkedToYnab" ]
                "Ereignisnamen sind fachlich, in Vergangenheitsform, kein CRUD-Naming"
            Expect.all (bodies |> List.map schemaVersion) (fun v -> v >= 1) "Schema-Version ab 1"

        testCase "Payloads ueberstehen den JSON-Roundtrip verlustfrei" <| fun () ->
            let bodies = [
                EntryObservedInYnab
                    { observedPayload "ynab-8" (Some "import-8") with
                        Splits = splitLines
                        Cleared = Reconciled
                        TransferAccountId = Some (YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-0000000000dd"))
                        TransferYnabTransactionId = Some "ynab-8-gegen" }
                detailsChange
                categorizationChange
                clearedChange
                EntryDeletionObservedInYnab
                EntryImportedFromComdirect (importedFromComdirectPayload "import-8b" "comdirect-8b")
                categorizedByRule
                EntryCategorizedByRule { RuleName = "mit Splits"; Category = None; Splits = splitLines; PayeeOverride = Some "Override" }
                recategorized
                CashExpenseAdded cashExpenseAddedPayload
                CashExpenseAdded { cashExpenseAddedPayload with Category = Some categoryHaushalt }
                EntryLinkedToYnab { YnabTransactionId = "ynab-8-linked" }
            ]
            for body in bodies do
                let json = encodePayload body
                match decodePayload (eventTypeName body) (schemaVersion body) json with
                | Ok decoded -> Expect.equal decoded body $"Roundtrip von {eventTypeName body}"
                | Error e -> failtest $"Roundtrip von {eventTypeName body} fehlgeschlagen: {e}"

        testCase "unbekannter Ereignistyp wird nicht geraten" <| fun () ->
            match decodePayload "EntryTeleportedToMars" 1 "{}" with
            | Ok _ -> failtest "unbekannter Typ darf nicht stillschweigend gelesen werden"
            | Error _ -> ()

        testCase "Erstbeobachtung stiftet die Fremd-Id-Verknuepfungen" <| fun () ->
            let entryId = newEntryId ()
            let links = linksFrom (observedEvent entryId (observedPayload "ynab-9" (Some "import-9")))
            Expect.equal (links |> List.map (fun l -> l.System, l.Value))
                [ YnabSystem, "ynab-9"; ImportIdSystem, "import-9" ]
                "YNAB-Id und ImportId als Verknuepfungen"

        testCase "Comdirect-Import stiftet ImportId- und Comdirect-Verknuepfung" <| fun () ->
            let entryId = newEntryId ()
            let links =
                linksFrom (eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload "import-17" "comdirect-17")))
            Expect.equal (links |> List.map (fun l -> l.System, l.Value))
                [ ImportIdSystem, "import-17"; ComdirectSystem, "comdirect-17" ]
                "ImportId und Comdirect-Reference als Verknuepfungen, keine YNAB-Id vor dem Push"

        testCase "EntryLinkedToYnab stiftet die YNAB-Verknuepfung" <| fun () ->
            let entryId = newEntryId ()
            let links = linksFrom (eventOn entryId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = "ynab-18" }))
            Expect.equal (links |> List.map (fun l -> l.System, l.Value)) [ YnabSystem, "ynab-18" ] "YNAB-Id als Verknuepfung"

        testCase "CashExpenseAdded stiftet keine Fremd-Id-Verknuepfung" <| fun () ->
            let entryId = newEntryId ()
            let links = linksFrom (eventOn entryId QuickAdd (CashExpenseAdded cashExpenseAddedPayload))
            Expect.isEmpty links "Quick Add hat keine ImportId (ADR 0004), Verknuepfung folgt erst mit EntryLinkedToYnab"

        testCase "Actor ueberlebt die Persistenzform" <| fun () ->
            let ruleId = RuleId (Guid.NewGuid())
            let actors =
                [ YnabMirror; ComdirectImport; Rule ruleId; UserInBb; QuickAdd; OrderIdMatch "order-42" ]
            for actor in actors do
                let kind, rule = actorToColumns actor
                match actorFromColumns kind rule with
                | Ok back -> Expect.equal back actor "Actor-Roundtrip"
                | Error e -> failtest e
    ]

// ============================================
// Import-Push: Kassenbuch-Ereignisse aus Comdirect-Buchung und Review-Spur bauen
// (ADR 0021, ledger-qvt84, reine Faltung)
// ============================================

[<Tests>]
let recordComdirectPushTests =
    testList "Ledger Domain (recordComdirectPush)" [

        testCase "ohne Review-Spur entsteht nur die Comdirect-Buchung" <| fun () ->
            let entryId = newEntryId ()
            let pushedAt = DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)
            let events =
                recordComdirectPush {
                    EntryId = entryId
                    AccountId = testAccountId
                    ImportId = "import-1"
                    ComdirectReference = "comdirect-1"
                    Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                    Amount = eur -17.40m
                    Payee = Some "dm"
                    Memo = None
                    PushedAt = pushedAt
                    ReviewSteps = []
                }

            Expect.equal
                (events |> List.map (fun e -> eventTypeName e.Body))
                [ "EntryImportedFromComdirect" ]
                "nur die Entstehung, keine Review-Spur"
            Expect.equal events.Head.Actor ComdirectImport "Herkunft Comdirect-Import"
            Expect.equal events.Head.OccurredAt pushedAt "Zeitpunkt ist der Push-Zeitpunkt"

        testCase "Regel-Schritt und Korrektur behalten ihre eigenen Zeitpunkte aus der Review-Spur" <| fun () ->
            let entryId = newEntryId ()
            let ruleAt = DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
            let userAt = DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)
            let pushedAt = DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)
            let ruleId = RuleId (Guid.NewGuid())

            let ruleStep : ReviewStep = {
                At = ruleAt
                By = ByRule (ruleId, "dm -> Drogerie")
                Category = Some categoryDrogerie.CategoryId
                CategoryName = Some categoryDrogerie.CategoryName
                Splits = None
                PayeeOverride = None
            }
            let userStep : ReviewStep = {
                At = userAt
                By = ByUser
                Category = Some categoryHaushalt.CategoryId
                CategoryName = Some categoryHaushalt.CategoryName
                Splits = None
                PayeeOverride = None
            }

            let events =
                recordComdirectPush {
                    EntryId = entryId
                    AccountId = testAccountId
                    ImportId = "import-2"
                    ComdirectReference = "comdirect-2"
                    Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                    Amount = eur -17.40m
                    Payee = Some "dm"
                    Memo = None
                    PushedAt = pushedAt
                    ReviewSteps = [ ruleStep; userStep ]
                }

            Expect.equal
                (events |> List.map (fun e -> eventTypeName e.Body))
                [ "EntryImportedFromComdirect"; "EntryCategorizedByRule"; "EntryRecategorized" ]
                "Entstehung, dann Regel, dann Korrektur, in dieser Reihenfolge"
            Expect.equal
                (events |> List.map (fun e -> e.Actor))
                [ ComdirectImport; Rule ruleId; UserInBb ]
                "Herkunft je Ereignis"
            Expect.equal
                (events |> List.map (fun e -> e.OccurredAt))
                [ pushedAt; ruleAt; userAt ]
                "jeder Review-Schritt behält seinen eigenen Zeitpunkt, nicht den Push-Zeitpunkt"

            match events.[1].Body with
            | EntryCategorizedByRule p ->
                Expect.equal p.RuleName "dm -> Drogerie" "RuleName kommt direkt aus dem Schritt, kein Nachschlagen"
                Expect.equal p.Category (Some categoryDrogerie) "Kategorie aus dem Schritt"
            | other -> failtest $"erwartete EntryCategorizedByRule, war {other}"

            match events.[2].Body with
            | EntryRecategorized p ->
                Expect.equal p.OldCategory (Some categoryDrogerie) "OldCategory ist der Stand des vorherigen Schritts"
                Expect.equal p.NewCategory (Some categoryHaushalt) "NewCategory aus dem Schritt"
                Expect.equal p.NewPayee (Some "dm") "kein PayeeOverride im Schritt: der bisherige Payee bleibt erhalten"
            | other -> failtest $"erwartete EntryRecategorized, war {other}"

        testCase "Order-ID-Abgleich erzeugt EntryRecategorized mit Actor OrderIdMatch" <| fun () ->
            let entryId = newEntryId ()
            let at = DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc)
            let orderIdStep : ReviewStep = {
                At = at
                By = ByOrderIdMatch "order-42"
                Category = Some categoryHaushalt.CategoryId
                CategoryName = Some categoryHaushalt.CategoryName
                Splits = None
                PayeeOverride = None
            }

            let events =
                recordComdirectPush {
                    EntryId = entryId
                    AccountId = testAccountId
                    ImportId = "import-3"
                    ComdirectReference = "comdirect-3"
                    Date = DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                    Amount = eur -25.00m
                    Payee = Some "Amazon"
                    Memo = None
                    PushedAt = DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc)
                    ReviewSteps = [ orderIdStep ]
                }

            Expect.equal
                (events |> List.map (fun e -> eventTypeName e.Body))
                [ "EntryImportedFromComdirect"; "EntryRecategorized" ]
                "Order-ID-Abgleich ist ein Recategorized-Ereignis mit eigenem Urheber"
            Expect.equal events.[1].Actor (OrderIdMatch "order-42")
                "Actor trägt die Bestell-Id, weder Regel noch Roman für DIESE Buchung"
    ]

// ============================================
// Log und Zustand (Persistence, In-Memory-SQLite)
// ============================================

[<Tests>]
let ledgerPersistenceTests =
    initializeLedgerSchema ()

    testSequenced
    <| testList "Ledger Persistence (appendAndApply)" [

        testCase "Ereignis anhaengen, Zustand lesen" <| fun () ->
            let entryId = newEntryId ()
            let payload = observedPayload $"ynab-{entryIdText entryId}" None
            appendAndApply [ observedEvent entryId payload ] |> Async.RunSynchronously

            match loadEntry entryId |> Async.RunSynchronously with
            | None -> failtest "Zustand nach dem Anhaengen nicht gefunden"
            | Some entry ->
                Expect.equal entry.Payee payload.Payee "Payee im Zustand"
                Expect.equal entry.Amount payload.Amount "Betrag im Zustand"
                Expect.equal entry.Category payload.Category "Kategorie im Zustand"
                Expect.equal entry.Date payload.Date "Datum im Zustand"

        testCase "Betraege bleiben exakt, ohne Gleitkomma-Drift" <| fun () ->
            let entryId = newEntryId ()
            let exact = 1234567.12345678m
            let payload =
                { observedPayload $"ynab-{entryIdText entryId}" None with Amount = eur exact }
            appendAndApply [ observedEvent entryId payload ] |> Async.RunSynchronously

            let entry = (loadEntry entryId |> Async.RunSynchronously).Value
            Expect.equal entry.Amount.Amount exact "Betrag exakt zurueckgelesen (kein REAL)"

        testCase "zwei Ereignisse auf eine Buchung ergeben zwei Historie-Eintraege mit steigender Nummer" <| fun () ->
            let entryId = newEntryId ()
            appendAndApply [ observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" None) ]
            |> Async.RunSynchronously
            appendAndApply [ eventOn entryId YnabMirror clearedChange ] |> Async.RunSynchronously

            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.Sequence)) [ 1; 2 ] "laufende Nummer ab 1, steigend"
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "EntryObservedInYnab"; "EntryClearedStateChangeObservedInYnab" ]
                "Typnamen im Log"
            Expect.all (recorded |> List.map (fun r -> r.SchemaVersion)) ((=) 1) "Schema-Version gespeichert"
            Expect.equal
                (loadEntry entryId |> Async.RunSynchronously).Value.Cleared
                Cleared
                "Zustand ist mitgezogen"

        testCase "mehrere Ereignisse in einem Aufruf falten aufeinander auf" <| fun () ->
            let entryId = newEntryId ()
            appendAndApply [
                observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" None)
                eventOn entryId YnabMirror categorizationChange
                eventOn entryId YnabMirror clearedChange
            ]
            |> Async.RunSynchronously

            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.Sequence)) [ 1; 2; 3 ] "drei Eintraege, steigend"
            let entry = (loadEntry entryId |> Async.RunSynchronously).Value
            Expect.equal entry.Splits splitLines "letzter Stand der Splits"
            Expect.equal entry.Cleared Cleared "letzter Cleared-Status"

        testCase "Loeschung setzt das Flag, statt die Buchung zu loeschen" <| fun () ->
            let entryId = newEntryId ()
            appendAndApply [ observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" None) ]
            |> Async.RunSynchronously
            appendAndApply [ eventOn entryId YnabMirror EntryDeletionObservedInYnab ]
            |> Async.RunSynchronously

            match loadEntry entryId |> Async.RunSynchronously with
            | None -> failtest "Buchung wurde geloescht statt markiert"
            | Some entry ->
                Expect.isTrue entry.DeletedInYnab "DeletedInYnab gesetzt"
                Expect.equal entry.Payee (Some "Rewe") "Buchung bleibt vollstaendig lesbar"

        testCase "Actor mit RuleId ueberlebt Schreiben und Lesen" <| fun () ->
            let entryId = newEntryId ()
            let ruleId = RuleId (Guid.NewGuid())
            appendAndApply [
                { observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" None) with
                    Actor = Rule ruleId }
            ]
            |> Async.RunSynchronously

            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.Event.Actor)) [ Rule ruleId ] "Herkunft gespeichert"

        testCase "Replay aus dem Log ergibt denselben Zustand wie die Zustandstabellen" <| fun () ->
            // Pflichtgegenprobe (ADR 0013, Punkt 6): driftet das synchron gefuehrte
            // Zustandsmodell vom Log weg, faellt es hier auf.
            let entryId = newEntryId ()
            appendAndApply [
                observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" (Some $"import-{entryIdText entryId}"))
                eventOn entryId YnabMirror detailsChange
                eventOn entryId YnabMirror categorizationChange
                eventOn entryId YnabMirror clearedChange
            ]
            |> Async.RunSynchronously

            let byId (entries: Entry list) =
                entries |> List.sortBy (fun e -> entryIdText e.Id)

            let fromTables = loadAllEntries () |> Async.RunSynchronously |> byId
            let fromLog = replayAll () |> Async.RunSynchronously |> byId

            Expect.isNonEmpty fromTables "es gibt Buchungen zu vergleichen"
            Expect.equal fromLog fromTables "Replay und Zustandstabellen stimmen ueberein"

        testCase "Replay rekonstruiert Buchungen aus allen drei Entstehungsarten, kategorisiert und verknuepft" <| fun () ->
            // AC: der Replay-Test muss den Zustand fuer Buchungen rekonstruieren, die mit
            // jeder der drei Entstehungsarten beginnen und danach kategorisiert und
            // verknuepft werden (EntryObservedInYnab, EntryImportedFromComdirect,
            // CashExpenseAdded).
            let observedId = newEntryId ()
            let importedId = newEntryId ()
            let cashId = newEntryId ()
            let importedYnabTxId = $"ynab-linked-{entryIdText importedId}"
            let cashYnabTxId = $"ynab-linked-{entryIdText cashId}"

            appendAndApply [
                // Entstehung 1: YNAB-Beobachtung, danach in BB umkategorisiert.
                observedEvent observedId (observedPayload $"ynab-{entryIdText observedId}" None)
                eventOn observedId UserInBb recategorized

                // Entstehung 2: Comdirect-Import (kategorielos), per Regel kategorisiert,
                // dann per Push-Antwort mit YNAB verknuepft (ADR 0021).
                eventOn
                    importedId
                    ComdirectImport
                    (EntryImportedFromComdirect (importedFromComdirectPayload $"import-{entryIdText importedId}" $"comdirect-{entryIdText importedId}"))
                eventOn importedId (Rule (RuleId (Guid.NewGuid()))) categorizedByRule
                eventOn importedId ComdirectImport (EntryLinkedToYnab { YnabTransactionId = importedYnabTxId })

                // Entstehung 3: Quick Add mit sofortiger Kategorie, danach verknuepft.
                eventOn cashId QuickAdd (CashExpenseAdded { cashExpenseAddedPayload with Category = Some categoryHaushalt })
                eventOn cashId QuickAdd (EntryLinkedToYnab { YnabTransactionId = cashYnabTxId })
            ]
            |> Async.RunSynchronously

            let byId (entries: Entry list) =
                entries |> List.sortBy (fun e -> entryIdText e.Id)

            let fromTables = loadAllEntries () |> Async.RunSynchronously |> byId
            let fromLog = replayAll () |> Async.RunSynchronously |> byId
            Expect.equal fromLog fromTables "Replay und Zustandstabellen stimmen fuer alle drei Entstehungsarten ueberein"

            let imported = fromTables |> List.find (fun e -> e.Id = importedId)
            Expect.equal imported.Category (Some categoryDrogerie) "per Regel kategorisierte Comdirect-Buchung"

            let cash = fromTables |> List.find (fun e -> e.Id = cashId)
            Expect.equal cash.Category (Some categoryHaushalt) "Quick-Add-Buchung traegt ihre Kategorie"

            Expect.equal
                (findEntryByLink YnabSystem importedYnabTxId |> Async.RunSynchronously)
                (Some importedId)
                "Comdirect-Buchung ist ueber die Push-Antwort mit YNAB verknuepft"
            Expect.equal
                (findEntryByLink YnabSystem cashYnabTxId |> Async.RunSynchronously)
                (Some cashId)
                "Quick-Add-Buchung ist ueber die Push-Antwort mit YNAB verknuepft"

        testCase "schlaegt das Zustands-Update fehl, steht auch kein Ereignis im Log" <| fun () ->
            // Ein Aenderungs-Ereignis auf eine unbekannte Buchung laesst `apply` scheitern,
            // nachdem das Ereignis bereits in die Transaktion geschrieben wurde. Der
            // Rollback muss beides zuruecknehmen (ADR 0013, Punkt 5).
            let entryId = newEntryId ()
            Expect.throws
                (fun () -> appendAndApply [ eventOn entryId YnabMirror clearedChange ] |> Async.RunSynchronously)
                "Zustands-Update muss scheitern"

            Expect.isEmpty
                (loadEventsForEntry entryId |> Async.RunSynchronously)
                "kein Ereignis im Log nach dem Rollback"
            Expect.isNone (loadEntry entryId |> Async.RunSynchronously) "kein Zustand nach dem Rollback"

        testCase "gescheiterte Fremd-Id nimmt das ganze Ereignis zurueck" <| fun () ->
            let firstId = newEntryId ()
            let ynabTxId = $"ynab-kollision-{entryIdText firstId}"
            appendAndApply [ observedEvent firstId (observedPayload ynabTxId None) ] |> Async.RunSynchronously

            let secondId = newEntryId ()
            Expect.throws
                (fun () ->
                    appendAndApply [ observedEvent secondId (observedPayload ynabTxId None) ]
                    |> Async.RunSynchronously)
                "dieselbe YNAB-Id darf nicht zweimal vergeben werden"

            Expect.isEmpty
                (loadEventsForEntry secondId |> Async.RunSynchronously)
                "kein Ereignis der abgelehnten Buchung im Log"
            Expect.isNone (loadEntry secondId |> Async.RunSynchronously) "kein Zustand der abgelehnten Buchung"

        testCase "ein zweites Entstehungs-Ereignis in appendAndApply nimmt die ganze Buchung zurueck" <| fun () ->
            let entryId = newEntryId ()
            appendAndApply [
                observedEvent entryId (observedPayload $"ynab-{entryIdText entryId}" None)
            ]
            |> Async.RunSynchronously

            Expect.throws
                (fun () ->
                    appendAndApply [
                        eventOn entryId ComdirectImport (EntryImportedFromComdirect (importedFromComdirectPayload $"import-{entryIdText entryId}" $"comdirect-{entryIdText entryId}"))
                    ]
                    |> Async.RunSynchronously)
                "ein zweites Entstehungs-Ereignis auf derselben Buchung schlaegt fehl"

            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal (recorded |> List.map (fun r -> r.EventType)) [ "EntryObservedInYnab" ] "nur die erste Entstehung steht im Log"

        testCase "Fremd-Ids der Erstbeobachtung sind verknuepft und auffindbar" <| fun () ->
            let entryId = newEntryId ()
            let ynabTxId = $"ynab-{entryIdText entryId}"
            let importId = $"import-{entryIdText entryId}"
            appendAndApply [ observedEvent entryId (observedPayload ynabTxId (Some importId)) ]
            |> Async.RunSynchronously

            let links = linksForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (links |> List.map (fun l -> l.System, l.Value) |> List.sortBy fst)
                ([ YnabSystem, ynabTxId; ImportIdSystem, importId ] |> List.sortBy fst)
                "YNAB-Id und ImportId verknuepft"
            Expect.equal (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously) (Some entryId) "ueber YNAB-Id gefunden"
            Expect.equal (findEntryByLink ImportIdSystem importId |> Async.RunSynchronously) (Some entryId) "ueber ImportId gefunden"

        testCase "Fremd-Id ist je System eindeutig" <| fun () ->
            let first = newEntryId ()
            let second = newEntryId ()
            let value = $"kollision-{entryIdText first}"

            linkEntry first YnabSystem value |> Async.RunSynchronously
            Expect.throws
                (fun () -> linkEntry second YnabSystem value |> Async.RunSynchronously)
                "derselbe Wert im selben System darf nicht zwei Buchungen gehoeren"

            // Dasselbe Textmuster in einem anderen System ist eine andere Fremd-Id.
            linkEntry second ImportIdSystem value |> Async.RunSynchronously
            Expect.equal (findEntryByLink YnabSystem value |> Async.RunSynchronously) (Some first) "YNAB-Id zeigt auf die erste Buchung"
            Expect.equal (findEntryByLink ImportIdSystem value |> Async.RunSynchronously) (Some second) "ImportId zeigt auf die zweite Buchung"

        testCase "Konto wird gespeichert und ueber die YnabAccountId gefunden" <| fun () ->
            let accountId = newLedgerAccountId ()
            let ynabAccountId = YnabAccountId (Guid.NewGuid())
            let account = {
                Id = accountId
                Name = "Comdirect Giro"
                Kind = BankAccount
                Link = YnabMirrored ynabAccountId
                Closed = false
            }
            saveAccount account |> Async.RunSynchronously

            match findAccountByYnabAccountId ynabAccountId |> Async.RunSynchronously with
            | None -> failtest "gespiegeltes Konto nicht gefunden"
            | Some found -> Expect.equal found account "Konto vollstaendig zurueckgelesen"

            Expect.contains (loadAccounts () |> Async.RunSynchronously) account "Konto ist in der Liste"
    ]

// ============================================
// Quick Add: Kassenbuch zuerst, dann Push, dann Verknuepfung (ADR 0021, addCashExpense)
// ============================================

[<Tests>]
let ledgerQuickAddCommandTests =
    testSequenced
    <| testList "Ledger CommandHandlers (addCashExpense)" [

        testCase "schreibt zuerst CashExpenseAdded, pusht danach und verknuepft mit der Push-Antwort" <| fun () ->
            let input : CashExpenseInput = {
                AccountId = testAccountId
                Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -12.50m
                Payee = Some "Kiosk"
                Memo = None
                Category = None
            }
            let ynabTxId = $"ynab-quickadd-{Guid.NewGuid()}"
            let push () : Async<YnabResult<string>> = async.Return (Ok ynabTxId)

            match
                addCashExpense (DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)) push noTrigger input
                |> Async.RunSynchronously
            with
            | Error err -> failtest $"addCashExpense sollte gelingen, war: {err}"
            | Ok entryId ->
                let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
                Expect.equal
                    (recorded |> List.map (fun r -> r.EventType))
                    [ "CashExpenseAdded"; "EntryLinkedToYnab" ]
                    "erst Kassenbuch, dann Verknuepfung"
                Expect.equal
                    (recorded |> List.map (fun r -> r.Event.Actor))
                    [ QuickAdd; QuickAdd ]
                    "beide Ereignisse tragen die Herkunft Quick Add"
                Expect.equal
                    (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously)
                    (Some entryId)
                    "ueber die Push-Antwort mit YNAB verknuepft"

        testCase "ein gescheiterter Push laesst die Kassenbuch-Buchung ohne Verknuepfung stehen" <| fun () ->
            let input : CashExpenseInput = {
                AccountId = testAccountId
                Date = DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -3.20m
                Payee = Some "Baeckerei Quick-Add-Fehler"
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> =
                async.Return (Error (YnabError.NetworkError "Timeout"))

            let result =
                addCashExpense (DateTime(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc)) push noTrigger input
                |> Async.RunSynchronously

            match result with
            | Ok entryId -> failtest $"addCashExpense sollte den Push-Fehler durchreichen, lieferte stattdessen {entryId}"
            | Error (YnabError.NetworkError msg) -> Expect.equal msg "Timeout" "Fehler wird unveraendert durchgereicht"
            | Error other -> failtest $"unerwarteter Fehler: {other}"

            // ADR 0021, Punkt 2: kein Rollback bei Push-Fehler, die Buchung bleibt stehen.
            let created =
                loadEntriesForAccount testAccountId
                |> Async.RunSynchronously
                |> List.filter (fun e -> e.Payee = Some "Baeckerei Quick-Add-Fehler")
            Expect.hasLength created 1 "die Kassenbuch-Buchung bleibt trotz Push-Fehler stehen"

            let entryId = created.Head.Id
            let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
            Expect.equal
                (recorded |> List.map (fun r -> r.EventType))
                [ "CashExpenseAdded" ]
                "keine Verknuepfung nach gescheitertem Push"
            Expect.isNone
                (linksForEntry entryId |> Async.RunSynchronously |> List.tryFind (fun l -> l.System = YnabSystem))
                "keine YNAB-Verknuepfung nach gescheitertem Push"

        testCase "ein erfolgreicher Push stößt den Spiegel genau einmal an" <| fun () ->
            // ledger-h4p6c, ADR 0014 Punkt 2: der Spiegel läuft nach einem
            // erfolgreichen Push, aber erst nachdem `EntryLinkedToYnab` steht (Task-
            // Nachtrag, ADR 0021): `triggerMirror` hängt darum hinter der
            // Verknüpfung, nicht hinter dem Schreiben der Buchung.
            let input : CashExpenseInput = {
                AccountId = testAccountId
                Date = DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -2.10m
                Payee = Some "Spiegel-Auslöser Erfolg"
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> = async.Return (Ok $"ynab-{Guid.NewGuid()}")
            let mutable triggerCount = 0
            let triggerMirror () = triggerCount <- triggerCount + 1

            addCashExpense (DateTime(2026, 3, 8, 9, 0, 0, DateTimeKind.Utc)) push triggerMirror input
            |> Async.RunSynchronously
            |> ignore

            Expect.equal triggerCount 1 "genau ein Auslöser nach erfolgreichem Push"

        testCase "EntryLinkedToYnab steht schon im Log, wenn der Auslöser feuert" <| fun () ->
            // Task-Nachtrag, ADR 0021: ein Lauf, der zwischen dem Push und der
            // Verknüpfung dazwischenkommt, sähe die YNAB-Buchung ungematcht (Quick Add
            // trägt keine ImportId) und legte eine zweite Buchung an. Der Zähler-Test
            // oben prüft nur, dass triggerMirror überhaupt feuert, nicht wann; dieser
            // Test schaut während des Callbacks selbst ins Log.
            let payee = $"Spiegel-Reihenfolge {Guid.NewGuid()}"
            let input : CashExpenseInput = {
                AccountId = testAccountId
                Date = DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -2.30m
                Payee = Some payee
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> = async.Return (Ok $"ynab-{Guid.NewGuid()}")
            let mutable eventTypesAtTriggerTime : string list option = None
            let triggerMirror () =
                let entryId =
                    loadAllEvents ()
                    |> Async.RunSynchronously
                    |> List.tryPick (fun recorded ->
                        match recorded.Event.Body with
                        | CashExpenseAdded body when body.Payee = Some payee -> Some recorded.Event.EntryId
                        | _ -> None)
                eventTypesAtTriggerTime <-
                    entryId
                    |> Option.map (fun id ->
                        loadEventsForEntry id |> Async.RunSynchronously |> List.map (fun r -> r.EventType))

            addCashExpense (DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)) push triggerMirror input
            |> Async.RunSynchronously
            |> ignore

            match eventTypesAtTriggerTime with
            | Some types ->
                Expect.contains
                    types
                    "EntryLinkedToYnab"
                    "EntryLinkedToYnab steht schon im Log, wenn triggerMirror aufgerufen wird"
            | None -> failtest "die Buchung wurde im Auslöser-Callback nicht gefunden"

        testCase "ein gescheiterter Push löst keinen Spiegel-Lauf aus" <| fun () ->
            let input : CashExpenseInput = {
                AccountId = testAccountId
                Date = DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -2.20m
                Payee = Some "Spiegel-Auslöser Fehlschlag"
                Memo = None
                Category = None
            }
            let push () : Async<YnabResult<string>> =
                async.Return (Error (YnabError.NetworkError "Timeout"))
            let mutable triggerCount = 0
            let triggerMirror () = triggerCount <- triggerCount + 1

            addCashExpense (DateTime(2026, 3, 9, 9, 0, 0, DateTimeKind.Utc)) push triggerMirror input
            |> Async.RunSynchronously
            |> ignore

            Expect.equal triggerCount 0 "kein Auslöser ohne erfolgreiche Verknüpfung"
    ]

// ============================================
// Import-Push: Kassenbuch zuerst, dann YNAB, dann Verknüpfung (ADR 0021, Teil 2,
// ledger-qvt84, pushComdirectImports)
// ============================================

module private PushFixtures =
    let dummyRequest (importId: string) : YnabTransactionRequest = {
        AccountId = "11111111-1111-1111-1111-111111111111"
        Date = "2026-03-01"
        Amount = -1740
        PayeeName = "dm"
        Memo = ""
        Cleared = "uncleared"
        ImportId = importId
        CategoryId = None
        Subtransactions = None
    }

    let prepared (txId: string) (importId: string) : PreparedTransaction = {
        TransactionId = TransactionId txId
        ImportId = importId
        Request = dummyRequest importId
    }

    let candidate
        (txId: string)
        (importId: string)
        (amount: Money)
        (reviewSteps: ReviewStep list)
        : ComdirectImportCandidate =
        {
            Prepared = prepared txId importId
            ComdirectReference = $"ref-{txId}"
            Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            Amount = amount
            Payee = Some "dm"
            Memo = None
            ReviewSteps = reviewSteps
        }

    /// Ein injizierter `push`, der jede vorbereitete Buchung unter einer eigenen
    /// YNAB-Id "anlegt" (Erfolgspfad).
    let successfulPush (ynabTxId: string) (prepared: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
        async.Return (
            Ok {
                emptyCreateResult with
                    Created =
                        prepared
                        |> List.map (fun p ->
                            ({ TransactionId = p.TransactionId; ImportId = p.ImportId; YnabTransactionId = ynabTxId }
                                : CreatedTransactionResult))
            })

[<Tests>]
let ledgerImportPushCommandTests =
    testSequenced
    <| testList "Ledger CommandHandlers (pushComdirectImports)" [

        testCase "Kassenbuch zuerst: Import, Regel, Korrektur, dann Verknüpfung" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let ruleId = RuleId (Guid.NewGuid())
            let ruleAt = DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc)
            let userAt = DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)
            let ruleStep : ReviewStep = {
                At = ruleAt
                By = ByRule (ruleId, "dm -> Drogerie")
                Category = Some categoryDrogerie.CategoryId
                CategoryName = Some categoryDrogerie.CategoryName
                Splits = None
                PayeeOverride = None
            }
            let userStep : ReviewStep = {
                At = userAt
                By = ByUser
                Category = Some categoryHaushalt.CategoryId
                CategoryName = Some categoryHaushalt.CategoryName
                Splits = None
                PayeeOverride = None
            }
            let cand = PushFixtures.candidate txId importId (eur -17.40m) [ ruleStep; userStep ]
            let ynabTxId = $"ynab-{txId}"

            let result =
                pushComdirectImports
                    (DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc))
                    (PushFixtures.successfulPush ynabTxId)
                    noTrigger
                    testAccountId
                    [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (Linked (entryId, gotYnabTxId)) ->
                Expect.equal gotYnabTxId ynabTxId "Verknüpfung über die Push-Antwort"
                let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
                Expect.equal
                    (recorded |> List.map (fun r -> r.EventType))
                    [ "EntryImportedFromComdirect"; "EntryCategorizedByRule"; "EntryRecategorized"; "EntryLinkedToYnab" ]
                    "Import, Regel, Korrektur, Verknüpfung in dieser Reihenfolge"
                Expect.equal
                    (recorded |> List.map (fun r -> r.Event.Actor))
                    [ ComdirectImport; Rule ruleId; UserInBb; ComdirectImport ]
                    "Herkunft je Ereignis"
            | other -> failtest $"erwartete Linked, war {other}"

        testCase "ein Push-Fehler lässt die Kassenbuch-Buchung ohne Verknüpfung stehen" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -9.99m) []
            let push (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (Error (YnabError.NetworkError "Timeout"))

            let result =
                pushComdirectImports (DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc)) push noTrigger testAccountId [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (WrittenNotLinked entryId) ->
                let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
                Expect.equal
                    (recorded |> List.map (fun r -> r.EventType))
                    [ "EntryImportedFromComdirect" ]
                    "nur die Entstehung, kein Rollback (ADR 0021 Punkt 2)"
                Expect.isNone
                    (linksForEntry entryId |> Async.RunSynchronously |> List.tryFind (fun l -> l.System = YnabSystem))
                    "keine YNAB-Verknüpfung nach einem Push-Fehler"
            | other -> failtest $"erwartete WrittenNotLinked, war {other}"
            match result.SendResult with
            | Some (Error (YnabError.NetworkError msg)) -> Expect.equal msg "Timeout" "der rohe Push-Fehler wird durchgereicht"
            | other -> failtest $"erwartete Some (Error NetworkError), war {other}"

        testCase "ein zweiter Versuch für dieselbe ImportId erzeugt keine zweite Buchung" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -5.00m) []
            let ynabTxId = $"ynab-{txId}"
            let push = PushFixtures.successfulPush ynabTxId

            let firstOutcome =
                pushComdirectImports (DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc)) push noTrigger testAccountId [ cand ]
                |> Async.RunSynchronously
            let firstEntryId =
                match Map.tryFind (TransactionId txId) firstOutcome.Outcomes with
                | Some (Linked (entryId, _)) -> entryId
                | other -> failtest $"erster Versuch sollte Linked liefern, war {other}"

            let secondResult =
                pushComdirectImports (DateTime(2026, 3, 3, 11, 0, 0, DateTimeKind.Utc)) push noTrigger testAccountId [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) secondResult.Outcomes with
            | Some (AlreadyImported entryId) -> Expect.equal entryId firstEntryId "derselbe Eintrag, keine zweite Buchung"
            | other -> failtest $"zweiter Versuch sollte AlreadyImported liefern, war {other}"
            Expect.isNone secondResult.SendResult "der zweite Versuch sendet nichts an YNAB"

        testCase "erster Push scheitert, zweiter Push gelingt: dieselbe Buchung, jetzt Linked" <| fun () ->
            // Regressionstest (Verifier-Fund, Iteration 2): nach einem gescheiterten
            // Push (Netzwerkfehler, Timeout, 429, 400) hat `EntryImportedFromComdirect`
            // bereits die `importid`-Verknüpfung geschrieben. Ein zweiter Versuch fand
            // die Buchung darum über `findEntryByLink ImportIdSystem` und behandelte sie
            // als `AlreadyImported`, ohne sie erneut zu senden: die Buchung erreichte
            // YNAB nie. Ohne den Fix bleibt der zweite Versuch bei `AlreadyImported`
            // (nicht `Linked`) und `SendResult = None` (nicht gesendet) stehen.
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -7.50m) []
            let ynabTxId = $"ynab-{txId}"

            let firstPush (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (Error (YnabError.NetworkError "Timeout"))

            let firstEntryId =
                match
                    pushComdirectImports (DateTime(2026, 3, 6, 10, 0, 0, DateTimeKind.Utc)) firstPush noTrigger testAccountId [ cand ]
                    |> Async.RunSynchronously
                    |> fun r -> Map.tryFind (TransactionId txId) r.Outcomes
                with
                | Some (WrittenNotLinked entryId) -> entryId
                | other -> failtest $"erster Versuch sollte WrittenNotLinked liefern, war {other}"

            let secondResult =
                pushComdirectImports
                    (DateTime(2026, 3, 6, 11, 0, 0, DateTimeKind.Utc))
                    (PushFixtures.successfulPush ynabTxId)
                    noTrigger
                    testAccountId
                    [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) secondResult.Outcomes with
            | Some (Linked (entryId, gotYnabTxId)) ->
                Expect.equal entryId firstEntryId "derselbe Eintrag wird nachträglich verknüpft, keine zweite Buchung"
                Expect.equal gotYnabTxId ynabTxId "Verknüpfung über die zweite, erfolgreiche Push-Antwort"
                let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
                Expect.equal
                    (recorded |> List.map (fun r -> r.EventType))
                    [ "EntryImportedFromComdirect"; "EntryLinkedToYnab" ]
                    "genau eine Entstehung, dann die Verknüpfung des zweiten Versuchs"
            | other -> failtest $"zweiter Versuch sollte Linked liefern, war {other}"
            Expect.isSome secondResult.SendResult "der zweite Versuch sendet tatsächlich an YNAB"

        testCase "eine von YNAB als Duplikat abgelehnte Buchung bleibt ohne Verknüpfung stehen" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -6.00m) []
            let push (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (Ok { emptyCreateResult with DuplicateImportIds = [ importId ] })

            let result =
                pushComdirectImports (DateTime(2026, 3, 4, 10, 0, 0, DateTimeKind.Utc)) push noTrigger testAccountId [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (WrittenNotLinked entryId) ->
                Expect.isNone
                    (linksForEntry entryId |> Async.RunSynchronously |> List.tryFind (fun l -> l.System = YnabSystem))
                    "keine YNAB-Verknüpfung für ein Duplikat"
            | other -> failtest $"erwartete WrittenNotLinked, war {other}"

        testCase "eine nicht auswertbare YNAB-Antwort verknüpft nichts, kein pauschaler Erfolg" <| fun () ->
            // AC: eine nicht auswertbare Antwort markiert Buchungen nicht pauschal als
            // importiert. `pushComdirectImports` bekommt hier direkt ein Ergebnis mit
            // `ResponseUnparseable = true` (wie `buildCreateResult` es bei einem
            // Parse-Fehler liefert) und muss die geschriebene Buchung unverändert und
            // unverknüpft lassen.
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -4.20m) []
            let push (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (Ok { emptyCreateResult with ResponseUnparseable = true })

            let result =
                pushComdirectImports (DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc)) push noTrigger testAccountId [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (WrittenNotLinked entryId) ->
                Expect.isNone
                    (linksForEntry entryId |> Async.RunSynchronously |> List.tryFind (fun l -> l.System = YnabSystem))
                    "keine YNAB-Verknüpfung bei nicht auswertbarer Antwort"
            | other -> failtest $"erwartete WrittenNotLinked, war {other}"

        testCase "Split mit Transfer-Zeile landet korrekt im Kassenbuch" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let total = eur -43.10m
            let splits = [
                { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Drogerie"); Amount = eur -30.00m; Memo = None }
                { Target = ToTransfer (YnabAccountId (Guid.NewGuid()), "Bar"); Amount = eur -13.10m; Memo = None }
            ]
            let step : ReviewStep = {
                At = DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc)
                By = ByUser
                Category = None
                CategoryName = None
                Splits = Some splits
                PayeeOverride = None
            }
            let cand = PushFixtures.candidate txId importId total [ step ]
            let ynabTxId = $"ynab-{txId}"

            let result =
                pushComdirectImports
                    (DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc))
                    (PushFixtures.successfulPush ynabTxId)
                    noTrigger
                    testAccountId
                    [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (Linked (entryId, _)) ->
                let entry = (loadEntry entryId |> Async.RunSynchronously).Value
                Expect.equal entry.Splits splits "Kategorie- und Transfer-Zeile im Kassenbuch"
                Expect.equal entry.Category None "keine Top-Level-Kategorie bei Splits"
                Expect.equal
                    (entry.Splits |> List.sumBy (fun s -> s.Amount.Amount))
                    total.Amount
                    "Split-Summe stimmt mit dem Buchungsbetrag überein"
            | other -> failtest $"erwartete Linked, war {other}"

        testCase "eine Spur \"erst Split, dann Kategorie\" wird geschrieben, gesendet und verknüpft" <| fun () ->
            // Regressionstest (Verifier-Fund, Iteration 3): Roman teilt eine Buchung auf
            // und wählt danach über den Kategorie-Chip "Aufgeteilt" eine Kategorie.
            // `categorizeTransaction` (`Server.Api`) lässt `Splits` dabei stehen; der
            // Review-Schritt trägt dann Kategorie UND mindestens zwei Split-Zeilen
            // zugleich. `buildTransactionRequest` (`YnabClient`) sendet in diesem Fall
            // nur die Splits, nie eine `category_id`. Ohne dieselbe Regel in
            // `recordComdirectPush` lehnt `apply`s Kategorie/Splits-Ausschluss das
            // Ereignis ab, `pushComdirectImports` liefert `LedgerWriteFailed`, und die
            // Buchung erreicht YNAB nie, still.
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let total = eur -43.10m
            let splits = [
                { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Drogerie"); Amount = eur -30.00m; Memo = None }
                { Target = ToCategory (YnabCategoryId (Guid.NewGuid()), "Haushalt"); Amount = eur -13.10m; Memo = None }
            ]
            let splitStep : ReviewStep = {
                At = DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc)
                By = ByUser
                Category = None
                CategoryName = None
                Splits = Some splits
                PayeeOverride = None
            }
            let categoryChipStep : ReviewStep = {
                At = DateTime(2026, 3, 6, 8, 5, 0, DateTimeKind.Utc)
                By = ByUser
                Category = Some categoryHaushalt.CategoryId
                CategoryName = Some categoryHaushalt.CategoryName
                Splits = Some splits
                PayeeOverride = None
            }
            let cand = PushFixtures.candidate txId importId total [ splitStep; categoryChipStep ]
            let ynabTxId = $"ynab-{txId}"

            let result =
                pushComdirectImports
                    (DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc))
                    (PushFixtures.successfulPush ynabTxId)
                    noTrigger
                    testAccountId
                    [ cand ]
                |> Async.RunSynchronously

            match Map.tryFind (TransactionId txId) result.Outcomes with
            | Some (Linked (entryId, gotYnabTxId)) ->
                Expect.equal gotYnabTxId ynabTxId "die Buchung wurde tatsächlich gesendet und verknüpft"
                let entry = (loadEntry entryId |> Async.RunSynchronously).Value
                Expect.equal entry.Splits splits "die Splits bleiben erhalten (wie gesendet)"
                Expect.equal entry.Category None "keine Top-Level-Kategorie: die Splits haben gewonnen, wie beim Push"
                let recorded = loadEventsForEntry entryId |> Async.RunSynchronously
                Expect.equal
                    (recorded |> List.map (fun r -> r.EventType))
                    [ "EntryImportedFromComdirect"; "EntryRecategorized"; "EntryRecategorized"; "EntryLinkedToYnab" ]
                    "beide Review-Schritte plus Entstehung und Verknüpfung stehen im Log"
            | other -> failtest $"erwartete Linked, war {other}"

        testCase "ein erfolgreicher Sende-Versuch stößt den Spiegel genau einmal an" <| fun () ->
            // ledger-h4p6c, ADR 0014 Punkt 2.
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -8.40m) []
            let ynabTxId = $"ynab-{txId}"
            let mutable triggerCount = 0
            let triggerMirror () = triggerCount <- triggerCount + 1

            pushComdirectImports
                (DateTime(2026, 3, 8, 10, 0, 0, DateTimeKind.Utc))
                (PushFixtures.successfulPush ynabTxId)
                triggerMirror
                testAccountId
                [ cand ]
            |> Async.RunSynchronously
            |> ignore

            Expect.equal triggerCount 1 "genau ein Auslöser nach erfolgreich gesendetem Push"

        testCase "EntryLinkedToYnab steht schon im Log, wenn der Auslöser beim Import feuert" <| fun () ->
            // Sinngemäß dieselbe Reihenfolge-Sicherung wie bei `addCashExpense`, hier für
            // den Import-Push (`ledger-qvt84`): die ImportId fängt Duplikate zwar ab
            // (ADR 0004), aber ein dazwischenkommender Lauf soll trotzdem erst starten,
            // wenn die Verknüpfung dieses Sende-Versuchs schon im Log steht.
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -6.60m) []
            let ynabTxId = $"ynab-{txId}"
            let mutable eventTypesAtTriggerTime : string list option = None
            let triggerMirror () =
                let entryId = findEntryByLink ImportIdSystem importId |> Async.RunSynchronously
                eventTypesAtTriggerTime <-
                    entryId
                    |> Option.map (fun id ->
                        loadEventsForEntry id |> Async.RunSynchronously |> List.map (fun r -> r.EventType))

            pushComdirectImports
                (DateTime(2026, 3, 8, 14, 0, 0, DateTimeKind.Utc))
                (PushFixtures.successfulPush ynabTxId)
                triggerMirror
                testAccountId
                [ cand ]
            |> Async.RunSynchronously
            |> ignore

            match eventTypesAtTriggerTime with
            | Some types ->
                Expect.contains
                    types
                    "EntryLinkedToYnab"
                    "EntryLinkedToYnab steht schon im Log, wenn triggerMirror aufgerufen wird"
            | None -> failtest "die Buchung wurde im Auslöser-Callback nicht gefunden"

        testCase "ein gescheiterter Sende-Versuch löst keinen Spiegel-Lauf aus" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -9.10m) []
            let push (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
                async.Return (Error (YnabError.NetworkError "Timeout"))
            let mutable triggerCount = 0
            let triggerMirror () = triggerCount <- triggerCount + 1

            pushComdirectImports (DateTime(2026, 3, 8, 11, 0, 0, DateTimeKind.Utc)) push triggerMirror testAccountId [ cand ]
            |> Async.RunSynchronously
            |> ignore

            Expect.equal triggerCount 0 "kein Auslöser ohne erfolgreichen Sende-Versuch"

        testCase "kein neuer Versand (alles schon AlreadyImported) löst keinen Spiegel-Lauf aus" <| fun () ->
            let txId = $"tx-{Guid.NewGuid()}"
            let importId = $"import-{txId}"
            let cand = PushFixtures.candidate txId importId (eur -3.30m) []
            let ynabTxId = $"ynab-{txId}"
            let mutable triggerCount = 0
            let triggerMirror () = triggerCount <- triggerCount + 1

            pushComdirectImports
                (DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc))
                (PushFixtures.successfulPush ynabTxId)
                triggerMirror
                testAccountId
                [ cand ]
            |> Async.RunSynchronously
            |> ignore
            triggerCount <- 0

            // Zweiter Versuch für dieselbe ImportId: schon verknüpft, nichts zu
            // senden (`AlreadyImported`, `SendResult = None`).
            pushComdirectImports
                (DateTime(2026, 3, 8, 13, 0, 0, DateTimeKind.Utc))
                (PushFixtures.successfulPush ynabTxId)
                triggerMirror
                testAccountId
                [ cand ]
            |> Async.RunSynchronously
            |> ignore

            Expect.equal triggerCount 0 "ohne neuen Versand gibt es nichts, was der Spiegel sofort sehen müsste"
    ]

// ============================================
// summarizeLedgerWriteFailures: ein gescheitertes Kassenbuch-Schreiben darf nie still
// bleiben (Verifier-Fund, Iteration 3, extrahiert damit `Server.Api.importToYnab`s
// Sichtbarmachen ohne HTTP testbar ist).
// ============================================

[<Tests>]
let summarizeLedgerWriteFailuresTests =
    testList "summarizeLedgerWriteFailures" [

        testCase "keine LedgerWriteFailed-Fälle ergeben None" <| fun () ->
            let result : ComdirectPushResult = {
                Outcomes =
                    Map.ofList [
                        TransactionId "tx-1", Linked (newEntryId (), "ynab-1")
                        TransactionId "tx-2", AlreadyImported (newEntryId ())
                    ]
                SendResult = None
            }
            Expect.isNone (summarizeLedgerWriteFailures result) "ohne Fehlschlag gibt es nichts zusammenzufassen"

        testCase "LedgerWriteFailed-Fälle werden gezählt und ihre Meldungen zusammengefasst" <| fun () ->
            // Regressionstest: vorher blieb ein gescheitertes Kassenbuch-Schreiben in
            // `importToYnab` unsichtbar (die SyncTransaction blieb unverändert, kein
            // Fehler, die Session schloss trotzdem ab).
            let result : ComdirectPushResult = {
                Outcomes =
                    Map.ofList [
                        TransactionId "tx-1", Linked (newEntryId (), "ynab-1")
                        TransactionId "tx-2", LedgerWriteFailed "Kategorie und Splits schließen sich aus"
                        TransactionId "tx-3", LedgerWriteFailed "UNIQUE constraint failed"
                    ]
                SendResult = None
            }
            match summarizeLedgerWriteFailures result with
            | Some (count, messages) ->
                Expect.equal count 2 "beide gescheiterten Buchungen werden gezählt"
                Expect.stringContains messages "Kategorie und Splits schließen sich aus" "erste Meldung enthalten"
                Expect.stringContains messages "UNIQUE constraint failed" "zweite Meldung enthalten"
            | None -> failtest "sollte Some liefern, zwei Buchungen sind gescheitert"
    ]
