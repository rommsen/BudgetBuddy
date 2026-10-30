module RemotingEndpointNegativeTests

// Negativfall je Remoting-Endpoint (infrastructure-am805, Audit D: 13 von 75 Endpoints
// ohne Test).
//
// Die Tabelle `rows` enthält je Endpoint aus `Shared.Api` eine Zeile mit einem kleinen
// Negativfall: ungültige Eingabe, unbekannte Id oder fehlende Einrichtung (kein Token,
// keine Zugangsdaten). Erwartet wird eine Fehlerantwort oder ein leeres Ergebnis, kein
// Absturz. Der Abgleich gegen die Reflection (`RemotingPipelineTests.remotingInterfaces`)
// macht den Test rot, sobald ein Endpoint ohne Zeile dazukommt oder eine Zeile auf einen
// entfernten Endpoint zeigt.
//
// Die Fälle rufen die Server-Werte aus `Server.Api` direkt auf (der .NET-Proxy von
// Fable.Remoting ist hier nicht nutzbar); die Pipeline selbst prüft
// `RemotingPipelineTests`. Datenbank: In-Memory. Kein Fall darf YNAB oder Comdirect
// erreichen: jeder Lauf prüft vorab, dass weder YNAB-Token noch Comdirect-Zugangsdaten
// in der Datenbank stehen, und alle Fälle enden vor einem Netzaufruf.
//
// Schwache Zeilen: Endpoints ohne Argument (reine Lesesichten) haben keinen echten
// Negativfall. Ihre Zeile prüft den Leerzustand ohne Einrichtung. `setDefaultBudget`,
// `setDefaultAccount` und `setQuickAddAccount` prüfen ihre Eingabe nicht; die Zeilen
// halten das heutige Verhalten fest (wird angenommen, kein Absturz) und stellen den
// vorherigen Wert wieder her.

open System
open Expecto
open Shared.Domain
open Shared.Api
open Server.Api

do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

type private Row = { Endpoint: string; Case: string; Run: unit -> unit }

let private run (a: Async<'T>) = Async.RunSynchronously a

let private expectError (label: string) (result: Result<'T, 'E>) =
    match result with
    | Error _ -> ()
    | Ok value -> failtestf "%s: Fehler erwartet, bekam Ok %A" label value

/// Sammelbefehle überspringen unbekannte Buchungen: Ergebnis ohne geänderte Buchung.
let private expectNothingChanged (result: Result<'C list, string>) =
    match result with
    | Ok changed -> Expect.isEmpty changed "keine Buchung geändert"
    | Error _ -> ()

let private setup () =
    Persistence.initializeDatabase ()
    Ledger.Persistence.initializeLedgerSchema ()
    Budgeting.Persistence.initializeBudgetingSchema ()
    // Schutz gegen Netzaufrufe: mit Token oder Zugangsdaten würden einige Fälle YNAB
    // oder Comdirect erreichen.
    for key in [ "ynab_token"; "comdirect_client_id"; "comdirect_password" ] do
        let value = Persistence.Settings.getSetting key |> run
        if value.IsSome then
            failtestf "In der Test-Datenbank steht '%s'; die Negativfälle laufen nur ohne Zugangsdaten" key

/// Setzt eine Einstellung nach dem Fall auf den vorherigen Wert zurück.
let private restoringSetting (key: string) (body: unit -> unit) =
    let previous = Persistence.Settings.getSetting key |> run
    try
        body ()
    finally
        match previous with
        | Some value -> Persistence.Settings.setSetting key value false |> run
        | None -> Persistence.Settings.deleteSetting key |> run

let private unknownSession () = SyncSessionId (Guid.NewGuid())
let private unknownTx = TransactionId "unbekannte-buchung"

let private emptyPageQuery (accountId: Guid) : LedgerEntryPageQuery =
    { AccountId = Some accountId
      Page = 0
      PageSize = 50
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
      Sort = LedgerSort.defaultSort }

let private emptyMove : Shared.Budgeting.MoveRequest =
    { Source = Shared.Budgeting.ToBeBudgetedSide
      Target = Shared.Budgeting.ToBeBudgetedSide
      Amount = { Amount = 0m; Currency = "EUR" } }

let private emptyPreview : Shared.Budgeting.TargetPreview =
    { Month = "kein-monat"
      Lines = []
      Skipped = []
      Total = { Amount = 0m; Currency = "EUR" }
      ToBeBudgeted = { Amount = 0m; Currency = "EUR" }
      ToBeBudgetedAfter = { Amount = 0m; Currency = "EUR" } }

let private reviewState = { IsApproved = true; ClearedState = ReviewCleared }

let private rows : Row list = [
    // ---------------- SettingsApi ----------------
    { Endpoint = "SettingsApi.getSettings"; Case = "ohne Einrichtung kein YNAB und kein Comdirect"
      Run = fun () ->
        let s = settingsApi.getSettings () |> run
        Expect.isNone s.Ynab "kein YNAB"
        Expect.isNone s.Comdirect "kein Comdirect" }
    { Endpoint = "SettingsApi.getLenaFlagSettings"; Case = "Leerzustand ohne Absturz"
      Run = fun () -> settingsApi.getLenaFlagSettings () |> run |> ignore }
    { Endpoint = "SettingsApi.saveLenaFlagColor"; Case = "unbekannte Farbe wird abgelehnt"
      Run = fun () -> settingsApi.saveLenaFlagColor (Some "kariert") |> run |> expectError "Farbe" }
    { Endpoint = "SettingsApi.saveYnabToken"; Case = "leerer Token wird vor jedem Netzaufruf abgelehnt"
      Run = fun () -> settingsApi.saveYnabToken "" |> run |> expectError "Token" }
    { Endpoint = "SettingsApi.saveComdirectCredentials"; Case = "leere Zugangsdaten werden abgelehnt"
      Run = fun () ->
        settingsApi.saveComdirectCredentials { ClientId = ""; ClientSecret = ""; Username = ""; Password = ""; AccountId = None }
        |> run |> expectError "Zugangsdaten" }
    { Endpoint = "SettingsApi.saveSyncSettings"; Case = "negative Tageszahl wird abgelehnt"
      Run = fun () -> settingsApi.saveSyncSettings { DaysToFetch = -1 } |> run |> expectError "Tage" }
    { Endpoint = "SettingsApi.testYnabConnection"; Case = "ohne Token Fehler"
      Run = fun () -> settingsApi.testYnabConnection () |> run |> expectError "YNAB" }
    { Endpoint = "SettingsApi.testComdirectConnection"; Case = "ohne Zugangsdaten Fehler"
      Run = fun () -> settingsApi.testComdirectConnection () |> run |> expectError "Comdirect" }
    { Endpoint = "SettingsApi.confirmComdirectTan"; Case = "ohne laufende Anmeldung Fehler"
      Run = fun () -> settingsApi.confirmComdirectTan () |> run |> expectError "TAN" }

    // ---------------- YnabApi ----------------
    { Endpoint = "YnabApi.getBudgets"; Case = "ohne Token Fehler"
      Run = fun () -> ynabApi.getBudgets () |> run |> expectError "Budgets" }
    { Endpoint = "YnabApi.getBudgetDetails"; Case = "ohne Token Fehler"
      Run = fun () -> ynabApi.getBudgetDetails (YnabBudgetId "unbekannt") |> run |> expectError "Details" }
    { Endpoint = "YnabApi.getCategories"; Case = "ohne Token Fehler"
      Run = fun () -> ynabApi.getCategories (YnabBudgetId "unbekannt") |> run |> expectError "Kategorien" }
    { Endpoint = "YnabApi.getPayees"; Case = "ohne Token Fehler"
      Run = fun () -> ynabApi.getPayees (YnabBudgetId "unbekannt") |> run |> expectError "Empfänger" }
    { Endpoint = "YnabApi.setDefaultBudget"; Case = "schwach: unbekannte Budget-Id wird ohne Prüfung gespeichert"
      Run = fun () ->
        restoringSetting "ynab_default_budget_id" (fun () ->
            let result = ynabApi.setDefaultBudget (YnabBudgetId "unbekannt") |> run
            Expect.isOk result "angenommen") }
    { Endpoint = "YnabApi.setDefaultAccount"; Case = "schwach: unbekannte Konto-Id wird ohne Prüfung gespeichert"
      Run = fun () ->
        restoringSetting "ynab_default_account_id" (fun () ->
            let result = ynabApi.setDefaultAccount (YnabAccountId (Guid.NewGuid())) |> run
            Expect.isOk result "angenommen") }
    { Endpoint = "YnabApi.setQuickAddAccount"; Case = "schwach: unbekannte Konto-Id wird ohne Prüfung gespeichert"
      Run = fun () ->
        restoringSetting "ynab_quickadd_account_id" (fun () ->
            let result = ynabApi.setQuickAddAccount (YnabAccountId (Guid.NewGuid())) |> run
            Expect.isOk result "angenommen") }
    { Endpoint = "YnabApi.addManualTransaction"; Case = "Betrag 0 und leerer Empfänger werden abgelehnt"
      Run = fun () ->
        ynabApi.addManualTransaction
            { Amount = 0m; IsOutflow = true; PayeeName = ""; CategoryId = None; Date = DateTime.Today; Memo = None }
        |> run |> expectError "Buchung" }
    { Endpoint = "YnabApi.getRecentQuickAddTemplates"; Case = "ohne Einrichtung leere Liste"
      Run = fun () ->
        match ynabApi.getRecentQuickAddTemplates () |> run with
        | Ok templates -> Expect.isEmpty templates "keine Vorlagen"
        | Error _ -> () }

    // ---------------- RulesApi ----------------
    { Endpoint = "RulesApi.getAllRules"; Case = "Leerzustand ohne Absturz"
      Run = fun () -> rulesApi.getAllRules () |> run |> ignore }
    { Endpoint = "RulesApi.getRule"; Case = "unbekannte Id: RuleNotFound"
      Run = fun () -> rulesApi.getRule (RuleId (Guid.NewGuid())) |> run |> expectError "Regel" }
    { Endpoint = "RulesApi.createRule"; Case = "leerer Name und leeres Muster werden abgelehnt"
      Run = fun () ->
        rulesApi.createRule
            { Name = ""; Pattern = ""; PatternType = Contains; TargetField = Payee
              CategoryId = YnabCategoryId (Guid.NewGuid()); PayeeOverride = None }
        |> run |> expectError "Regel anlegen" }
    { Endpoint = "RulesApi.updateRule"; Case = "unbekannte Id wird abgelehnt"
      Run = fun () ->
        rulesApi.updateRule
            { Id = RuleId (Guid.NewGuid()); Name = Some "x"; Pattern = None; PatternType = None; TargetField = None
              CategoryId = None; PayeeOverride = PayeeOverrideChange.Keep; Priority = None; Enabled = None }
        |> run |> expectError "Regel ändern" }
    { Endpoint = "RulesApi.deleteRule"; Case = "unbekannte Id ohne Absturz"
      Run = fun () -> rulesApi.deleteRule (RuleId (Guid.NewGuid())) |> run |> ignore }
    { Endpoint = "RulesApi.reorderRules"; Case = "unbekannte Ids ohne Absturz"
      Run = fun () -> rulesApi.reorderRules [ RuleId (Guid.NewGuid()) ] |> run |> ignore }
    { Endpoint = "RulesApi.exportRules"; Case = "Export liefert JSON ohne Absturz"
      Run = fun () ->
        let json = rulesApi.exportRules () |> run
        Expect.isFalse (String.IsNullOrWhiteSpace json) "Export nicht leer" }
    { Endpoint = "RulesApi.importRules"; Case = "kein JSON wird abgelehnt"
      Run = fun () -> rulesApi.importRules "kein json" |> run |> expectError "Import" }
    { Endpoint = "RulesApi.testRule"; Case = "ungültiger Regex wird abgelehnt"
      Run = fun () -> rulesApi.testRule ("(", Regex, Payee, "(irgendwas") |> run |> expectError "Muster" }

    // ---------------- SyncApi ----------------
    { Endpoint = "SyncApi.startSync"; Case = "ohne Comdirect-Zugangsdaten Fehler"
      Run = fun () -> syncApi.startSync () |> run |> expectError "Start" }
    { Endpoint = "SyncApi.getCurrentSession"; Case = "schwach: Abfrage ohne Absturz"
      Run = fun () -> syncApi.getCurrentSession () |> run |> ignore }
    { Endpoint = "SyncApi.cancelSync"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.cancelSync (unknownSession ()) |> run |> expectError "Abbruch" }
    { Endpoint = "SyncApi.initiateComdirectAuth"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.initiateComdirectAuth (unknownSession ()) |> run |> expectError "Anmeldung" }
    { Endpoint = "SyncApi.confirmTan"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.confirmTan (unknownSession ()) |> run |> expectError "TAN" }
    { Endpoint = "SyncApi.getTransactions"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.getTransactions (unknownSession ()) |> run |> expectError "Umsätze" }
    { Endpoint = "SyncApi.categorizeTransaction"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.categorizeTransaction (unknownSession (), unknownTx, None, None) |> run |> expectError "Kategorie" }
    { Endpoint = "SyncApi.skipTransaction"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.skipTransaction (unknownSession (), unknownTx) |> run |> expectError "Überspringen" }
    { Endpoint = "SyncApi.unskipTransaction"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.unskipTransaction (unknownSession (), unknownTx) |> run |> expectError "Zurückholen" }
    { Endpoint = "SyncApi.bulkCategorize"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () ->
        syncApi.bulkCategorize (unknownSession (), [ unknownTx ], YnabCategoryId (Guid.NewGuid())) |> run |> expectError "Sammel" }
    { Endpoint = "SyncApi.splitTransaction"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.splitTransaction (unknownSession (), unknownTx, []) |> run |> expectError "Aufteilen" }
    { Endpoint = "SyncApi.clearSplit"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.clearSplit (unknownSession (), unknownTx) |> run |> expectError "Aufteilung lösen" }
    { Endpoint = "SyncApi.setAskLena"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.setAskLena (unknownSession (), unknownTx, true) |> run |> expectError "Lena" }
    { Endpoint = "SyncApi.importToYnab"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.importToYnab (unknownSession ()) |> run |> expectError "Import" }
    { Endpoint = "SyncApi.forceImportDuplicates"; Case = "unbekannte Sitzung: Fehler"
      Run = fun () -> syncApi.forceImportDuplicates (unknownSession (), [ unknownTx ]) |> run |> expectError "Erzwingen" }
    { Endpoint = "SyncApi.getSyncHistory"; Case = "negative Anzahl ohne Absturz"
      Run = fun () -> syncApi.getSyncHistory -1 |> run |> ignore }

    // ---------------- LedgerApi ----------------
    { Endpoint = "LedgerApi.mirrorYnabBudget"; Case = "ohne Token Fehler"
      Run = fun () -> ledgerApi.mirrorYnabBudget () |> run |> expectError "Spiegeln" }
    { Endpoint = "LedgerApi.getLastMirrorRun"; Case = "schwach: Abfrage ohne Absturz"
      Run = fun () -> ledgerApi.getLastMirrorRun () |> run |> ignore }
    { Endpoint = "LedgerApi.getLedgerAccounts"; Case = "schwach: Abfrage ohne Absturz"
      Run = fun () -> ledgerApi.getLedgerAccounts () |> run |> ignore }
    { Endpoint = "LedgerApi.getLedgerEntriesForAccount"; Case = "unbekanntes Konto: leere Liste"
      Run = fun () -> Expect.isEmpty (ledgerApi.getLedgerEntriesForAccount (Guid.NewGuid()) |> run) "keine Buchungen" }
    { Endpoint = "LedgerApi.getLedgerEntriesPage"; Case = "unbekanntes Konto: leere Seite"
      Run = fun () ->
        let page = ledgerApi.getLedgerEntriesPage (emptyPageQuery (Guid.NewGuid())) |> run
        Expect.isEmpty page.Entries "keine Buchungen" }
    { Endpoint = "LedgerApi.getLedgerPayeeSuggestions"; Case = "unbekanntes Konto: keine Vorschläge"
      Run = fun () ->
        let q = emptyPageQuery (Guid.NewGuid())
        let suggestions =
            ledgerApi.getLedgerPayeeSuggestions
                { AccountId = q.AccountId; IncludeDeleted = false; OnlyNeedsReview = false; OnlyAskLena = false
                  WithoutCategory = false; CategoryIds = []; DateFrom = None; DateTo = None; Memo = None
                  AmountMin = None; AmountMax = None }
            |> run
        Expect.isEmpty suggestions "keine Vorschläge" }
    { Endpoint = "LedgerApi.getLedgerAccountBalances"; Case = "schwach: Abfrage ohne Absturz"
      Run = fun () -> ledgerApi.getLedgerAccountBalances () |> run |> ignore }
    { Endpoint = "LedgerApi.getEntryHistory"; Case = "unbekannte Buchung: leere Historie"
      Run = fun () -> Expect.isEmpty (ledgerApi.getEntryHistory (Guid.NewGuid()) |> run) "keine Historie" }
    { Endpoint = "LedgerApi.setReviewMark"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.setReviewMark (Guid.NewGuid()) Reviewed |> run |> expectError "Prüfvermerk" }
    { Endpoint = "LedgerApi.restoreReviewMark"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.restoreReviewMark (Guid.NewGuid()) reviewState |> run |> expectError "Prüfvermerk zurück" }
    { Endpoint = "LedgerApi.retryReviewMarkWriteBack"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.retryReviewMarkWriteBack (Guid.NewGuid()) |> run |> expectError "Prüfvermerk erneut" }
    { Endpoint = "LedgerApi.setEntryCategory"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.setEntryCategory (Guid.NewGuid()) None |> run |> expectError "Kategorie" }
    { Endpoint = "LedgerApi.setEntrySplits"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.setEntrySplits (Guid.NewGuid()) [] |> run |> expectError "Aufteilung" }
    { Endpoint = "LedgerApi.getBudgetAccounts"; Case = "ohne Token Fehler"
      Run = fun () -> ledgerApi.getBudgetAccounts () |> run |> expectError "Konten" }
    { Endpoint = "LedgerApi.getLedgerEntry"; Case = "unbekannte Buchung: None"
      Run = fun () -> Expect.isNone (ledgerApi.getLedgerEntry (Guid.NewGuid()) |> run) "keine Buchung" }
    { Endpoint = "LedgerApi.retryCategorizationWriteBack"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.retryCategorizationWriteBack (Guid.NewGuid()) |> run |> expectError "Kategorie erneut" }
    { Endpoint = "LedgerApi.retryPushToYnab"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.retryPushToYnab (Guid.NewGuid()) |> run |> expectError "Push erneut" }
    { Endpoint = "LedgerApi.getPayeeCategorySuggestions"; Case = "unbekannter Empfänger: keine Vorschläge"
      Run = fun () ->
        Expect.isEmpty (ledgerApi.getPayeeCategorySuggestions "unbekannter empfänger am805" None |> run) "keine Vorschläge" }
    { Endpoint = "LedgerApi.setReviewMarkBatch"; Case = "Auswahl nur aus unbekannten Buchungen ändert nichts"
      Run = fun () -> expectNothingChanged (ledgerApi.setReviewMarkBatch (BatchPicked [ Guid.NewGuid() ]) |> run |> Result.map (fun o -> o.Changed)) }
    { Endpoint = "LedgerApi.restoreReviewMarkBatch"; Case = "unbekannte Buchung ändert nichts"
      Run = fun () ->
        ledgerApi.restoreReviewMarkBatch [ { EntryId = Guid.NewGuid(); Previous = reviewState } ]
        |> run |> Result.map (fun o -> o.Changed) |> expectNothingChanged }
    { Endpoint = "LedgerApi.setEntryCategoryBatch"; Case = "Auswahl nur aus unbekannten Buchungen ändert nichts"
      Run = fun () -> expectNothingChanged (ledgerApi.setEntryCategoryBatch (BatchPicked [ Guid.NewGuid() ]) None |> run |> Result.map (fun o -> o.Changed)) }
    { Endpoint = "LedgerApi.setLenaFlag"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.setLenaFlag (Guid.NewGuid()) AssignLena |> run |> expectError "Lena" }
    { Endpoint = "LedgerApi.restoreFlag"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.restoreFlag (Guid.NewGuid()) { Color = None; Name = None } |> run |> expectError "Flagge zurück" }
    { Endpoint = "LedgerApi.retryFlagWriteBack"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.retryFlagWriteBack (Guid.NewGuid()) |> run |> expectError "Flagge erneut" }
    { Endpoint = "LedgerApi.setEntryMemo"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.setEntryMemo (Guid.NewGuid()) "Notiz" |> run |> expectError "Memo" }
    { Endpoint = "LedgerApi.retryMemoWriteBack"; Case = "unbekannte Buchung: Fehler"
      Run = fun () -> ledgerApi.retryMemoWriteBack (Guid.NewGuid()) |> run |> expectError "Memo erneut" }
    { Endpoint = "LedgerApi.setLenaFlagBatch"; Case = "Auswahl nur aus unbekannten Buchungen: Fehler"
      Run = fun () -> ledgerApi.setLenaFlagBatch (BatchPicked [ Guid.NewGuid() ]) AssignLena |> run |> expectError "Lena Auswahl" }
    { Endpoint = "LedgerApi.restoreFlagBatch"; Case = "unbekannte Buchung ändert nichts"
      Run = fun () ->
        ledgerApi.restoreFlagBatch [ { EntryId = Guid.NewGuid(); Previous = { Color = None; Name = None } } ]
        |> run |> Result.map (fun o -> o.Changed) |> expectNothingChanged }

    // ---------------- BudgetApi ----------------
    { Endpoint = "BudgetApi.getBudgetMonth"; Case = "ungültiger Monat: Fehler"
      Run = fun () -> budgetApi.getBudgetMonth "kein-monat" |> run |> expectError "Monat" }
    { Endpoint = "BudgetApi.previewTargets"; Case = "ungültiger Monat: Fehler"
      Run = fun () -> budgetApi.previewTargets "kein-monat" |> run |> expectError "Vorschau" }
    { Endpoint = "BudgetApi.applyTargets"; Case = "ungültiger Monat: Fehler"
      Run = fun () -> budgetApi.applyTargets "kein-monat" emptyPreview |> run |> expectError "Anwenden" }
    { Endpoint = "BudgetApi.moveMoney"; Case = "ungültiger Monat: Fehler"
      Run = fun () -> budgetApi.moveMoney "kein-monat" emptyMove |> run |> expectError "Verschieben" }
    { Endpoint = "BudgetApi.retryFailedTargets"; Case = "keine gültige Anwendungs-Id: Fehler"
      Run = fun () -> budgetApi.retryFailedTargets "keine-guid" |> run |> expectError "Wiederholen" }
]

/// Endpoints aus der Reflection, als "Interface.methode".
let private reflectedEndpoints () =
    RemotingPipelineTests.remotingInterfaces ()
    |> List.collect (fun (iface, methods) -> methods |> List.map (fun m -> $"{iface}.{m}"))
    |> Set.ofList

[<Tests>]
let negativeTests =
    testSequenced
    <| testList "Remoting-Endpoints: Negativfall je Endpoint (infrastructure-am805)" [

        testCase "die Tabelle deckt jeden Remoting-Endpoint aus Shared.Api genau einmal ab" <| fun () ->
            let reflected = reflectedEndpoints ()
            let tableKeys = rows |> List.map (fun r -> r.Endpoint)
            Expect.isGreaterThan reflected.Count 0 "Reflection findet Endpoints"
            let duplicates = tableKeys |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst
            Expect.isEmpty duplicates "doppelte Zeilen"
            let table = Set.ofList tableKeys
            Expect.isEmpty (Set.difference reflected table) "Endpoints ohne Negativfall"
            Expect.isEmpty (Set.difference table reflected) "Zeilen für nicht mehr vorhandene Endpoints"

        testCase "Gegenbeweis: fehlt eine Zeile, meldet der Abgleich genau diesen Endpoint" <| fun () ->
            let reflected = reflectedEndpoints ()
            let withoutOne = rows |> List.tail |> List.map (fun r -> r.Endpoint) |> Set.ofList
            Expect.equal (Set.difference reflected withoutOne) (Set.singleton (List.head rows).Endpoint) "fehlende Zeile"

        testList "Negativfälle" [
            for row in rows ->
                testCase $"{row.Endpoint}: {row.Case}" <| fun () ->
                    setup ()
                    row.Run ()
        ]
    ]
