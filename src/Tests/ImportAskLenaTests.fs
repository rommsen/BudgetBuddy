module ImportAskLenaTests

// "Lena fragen" schon beim Import (ynab-sync-d9e51): der Befehl an der Sitzungs-Transaktion,
// die Flag-Farbe im Create-Body, die Entstehung im Kassenbuch mit Flag, die Historie und das
// erneute Senden. Die Fixtures tragen das Kürzel d9e51.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Thoth.Json.Net
open Shared.Domain
open Ledger.Events
open Ledger.Persistence
open Ledger.CommandHandlers
open Server.YnabClient
open Server.ImportAskLena

module private Fx =
    let ynabAccount = YnabAccountId (Guid.Parse "a1b2c3d4-e5f6-7890-abcd-ef1234567890")
    let today = DateTime(2026, 10, 26)

    let tx (id: string) (status: TransactionStatus) (category: YnabCategoryId option) : SyncTransaction = {
        Transaction = {
            Id = TransactionId id
            BookingDate = today
            Amount = { Amount = -12.34m; Currency = "EUR" }
            Payee = Some "Rewe"
            Memo = "Einkauf"
            Reference = $"REF-{id}"
            RawData = "{}"
        }
        Status = status
        CategoryId = category
        CategoryName = None
        MatchedRuleId = None
        PayeeOverride = None
        ExternalLinks = []
        UserNotes = None
        DuplicateStatus = NotDuplicate (emptyDetectionDetails $"REF-{id}")
        YnabImportStatus = NotAttempted
        Splits = None
        SuggestedByOrderId = None
        ReviewSteps = []
        AskLena = false
    }

    let prepare (txs: SyncTransaction list) = prepareTransactions today ynabAccount Map.empty false txs

    let bodyOf (p: PreparedTransaction) = Encode.toString 0 (encodeTransaction p.Request)

[<Tests>]
let commandTests =
    let open' = [ Pending; AutoCategorized; ManualCategorized; NeedsAttention ]
    testList "Import Lena fragen: Befehl an der Sitzungs-Transaktion" [

        testCase "setzt und entfernt die Rückfrage an jeder offenen Zeile" <| fun () ->
            for status in open' do
                let t = Fx.tx "d9e51-a" status None
                let asked = setAskLena (Some "purple") true t
                Expect.equal asked (Ok { t with AskLena = true }) $"gesetzt bei {status}"
                let withdrawn = setAskLena (Some "purple") false { t with AskLena = true }
                Expect.equal withdrawn (Ok t) $"entfernt bei {status}"

        testCase "bei Skipped und Imported wird der Befehl abgelehnt" <| fun () ->
            for status in [ Skipped; Imported ] do
                let t = Fx.tx "d9e51-b" status None
                Expect.equal (setAskLena (Some "purple") true t) (Error notOpenReason) $"setzen abgelehnt bei {status}"
                Expect.equal (setAskLena (Some "purple") false t) (Error notOpenReason) $"entfernen abgelehnt bei {status}"

        testCase "Überspringen hebt eine gesetzte Rückfrage auf" <| fun () ->
            let t = { Fx.tx "d9e51-c" ManualCategorized None with AskLena = true }
            let skipped = skip t
            Expect.equal skipped.Status Skipped "übersprungen"
            Expect.isFalse skipped.AskLena "Rückfrage aufgehoben"

        testCase "ohne gesetzte Farbe wird Lena fragen abgelehnt, mit Grund" <| fun () ->
            let t = Fx.tx "d9e51-d" Pending None
            Expect.equal (setAskLena None true t) (Error noColorReason) "keine Farbe"
            Expect.equal (setAskLena (Some "pink") true t) (Error noColorReason) "ungültige Farbe zählt als nicht gesetzt"
            Expect.equal noColorReason "In den Settings die Flag-Farbe für Lena wählen" "Wortlaut wie im Kassenbuch"

        testCase "Zurücknehmen braucht keine Farbe" <| fun () ->
            let t = { Fx.tx "d9e51-e" Pending None with AskLena = true }
            Expect.equal (setAskLena None false t) (Ok { t with AskLena = false }) "zurückgenommen"

        testCase "die Rückfrage ändert Status und Kategorie nicht" <| fun () ->
            let t = Fx.tx "d9e51-f" Pending None
            match setAskLena (Some "purple") true t with
            | Ok updated ->
                Expect.equal updated.Status Pending "Status bleibt Pending"
                Expect.isNone updated.CategoryId "keine Kategorie erfunden"
            | Error e -> failtest e
    ]

[<Tests>]
let pushBodyTests =
    testList "Import Lena fragen: flag_color im Create-Body" [

        testCase "Zeile mit Rückfrage trägt flag_color, Zeile ohne hat keinen Schlüssel, Body sonst byte-gleich" <| fun () ->
            let asking = { Fx.tx "d9e51-1" AutoCategorized (Some (YnabCategoryId (Guid.NewGuid()))) with AskLena = true }
            let plain = Fx.tx "d9e51-2" AutoCategorized (Some (YnabCategoryId (Guid.NewGuid())))
            let txs = [ asking; plain ]
            let before = Fx.prepare txs
            let after, notSent = applyLenaFlag (Some "purple") txs before
            Expect.equal notSent 0 "nichts verloren"
            let bodies = after.ToSend |> List.map (fun p -> p.TransactionId, Fx.bodyOf p) |> Map.ofList
            let beforeBodies = before.ToSend |> List.map (fun p -> p.TransactionId, Fx.bodyOf p) |> Map.ofList
            let askingBody = bodies[asking.Transaction.Id]
            Expect.stringContains askingBody "\"flag_color\":\"purple\"" "Farbe aus der Einstellung"
            let plainBody = bodies[plain.Transaction.Id]
            Expect.isFalse (plainBody.Contains "flag_color") "kein Schlüssel ohne Rückfrage"
            Expect.equal plainBody beforeBodies[plain.Transaction.Id] "Body ohne Rückfrage byte-gleich zu heute"
            Expect.equal
                (askingBody.Replace(",\"flag_color\":\"purple\"", ""))
                beforeBodies[asking.Transaction.Id]
                "mit Rückfrage unterscheidet sich der Body nur um flag_color"

        testCase "Zeile ohne Kategorie mit Rückfrage geht als Pending mit flag_color und ohne category_id" <| fun () ->
            let asking = { Fx.tx "d9e51-3" Pending None with AskLena = true }
            let after, _ = applyLenaFlag (Some "blue") [ asking ] (Fx.prepare [ asking ])
            Expect.equal (after.ToSend |> List.length) 1 "Pending geht mit"
            let body = Fx.bodyOf after.ToSend.Head
            Expect.stringContains body "\"flag_color\":\"blue\"" "mit Flag"
            Expect.isFalse (body.Contains "category_id") "ohne Kategorie"

        testCase "ist die Farbe bis zum Push entfernt, geht die Zeile ohne Flag und wird gezählt" <| fun () ->
            let asking = { Fx.tx "d9e51-4" Pending None with AskLena = true }
            let plain = Fx.tx "d9e51-5" Pending None
            let before = Fx.prepare [ asking; plain ]
            let after, notSent = applyLenaFlag None [ asking; plain ] before
            Expect.equal notSent 1 "eine Rückfrage ging ohne Flag"
            Expect.equal after before "Anfragen unverändert"
            Expect.isFalse ((after.ToSend |> List.map Fx.bodyOf) |> List.exists (fun b -> b.Contains "flag_color")) "kein flag_color"

        testCase "importLenaFlagNote nennt die Zahl, leer bei null" <| fun () ->
            Expect.equal (Components.SyncFlow.State.importLenaFlagNote 0) "" "leer"
            Expect.stringContains (Components.SyncFlow.State.importLenaFlagNote 1) "1 Rückfrage Lena ging ohne Flag" "eine"
            Expect.stringContains (Components.SyncFlow.State.importLenaFlagNote 3) "3 Rückfragen Lena gingen ohne Flag" "mehrere"
    ]

let private eur (amount: decimal) = { Amount = amount; Currency = "EUR" }
let private noTrigger () = ()
let private newAccount () = LedgerAccountId (Guid.NewGuid())

let private candidate (txId: string) (flag: string option) : ComdirectImportCandidate =
    let request : YnabTransactionRequest = {
        AccountId = "11111111-1111-1111-1111-111111111111"
        Date = "2026-10-26"
        Amount = -1740
        PayeeName = "dm"
        Memo = ""
        Cleared = "uncleared"
        ImportId = $"import-{txId}"
        CategoryId = None
        Subtransactions = None
        FlagColor = flag
    }
    {
        Prepared = { TransactionId = TransactionId txId; ImportId = $"import-{txId}"; Request = request }
        ComdirectReference = $"ref-{txId}"
        Date = DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)
        Amount = eur -17.40m
        Payee = Some "dm"
        RawPayee = Some "dm"
        Memo = None
        ReviewSteps = []
        ExistingEntryImportId = None
    }

let private okPush (prepared: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
    async.Return (
        Ok {
            emptyCreateResult with
                Created =
                    prepared
                    |> List.map (fun p ->
                        ({ TransactionId = p.TransactionId; ImportId = p.ImportId; YnabTransactionId = $"ynab-{p.TransactionId}" }
                            : CreatedTransactionResult))
        })

let private failingPush (_: PreparedTransaction list) : Async<YnabResult<TransactionCreateResult>> =
    async.Return (Error (YnabError.NetworkError "Timeout"))

let private at = DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc)

let private importOne (account: LedgerAccountId) (cand: ComdirectImportCandidate) push =
    let result = pushComdirectImports at push noTrigger account [ cand ] |> Async.RunSynchronously
    match Map.tryFind cand.Prepared.TransactionId result.Outcomes with
    | Some (Linked (id, _)) | Some (WrittenNotLinked id) -> id
    | other -> failtest $"unerwartetes Ergebnis {other}"

[<Tests>]
let ledgerTests =
    let testCase name body =
        testCase name (fun () ->
            initializeLedgerSchema ()
            global.Persistence.initializeDatabase ()
            body ())

    testSequenced
    <| testList "Import Lena fragen: Kassenbuch" [

        testCase "der Import-Eintrag mit Rückfrage trägt Farbe und Name Lena, flagOf ergibt AskLena, der Zähler zählt ihn" <| fun () ->
            let account = newAccount ()
            let entryId = importOne account (candidate $"tx-{Guid.NewGuid()}" (Some "purple")) okPush
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.equal entry.FlagColor (Some "purple") "Farbe"
            Expect.equal entry.FlagName (Some "Lena") "Name"
            Expect.equal (Ledger.QueryHandlers.ReadModel.flagOf entry.FlagColor entry.FlagName) AskLena "Rückfrage Lena"
            let count = countAskLenaEntries (Some account) |> Async.RunSynchronously
            Expect.equal count 1 "AskLenaCount zählt die Buchung"

        testCase "ohne Rückfrage entsteht die Buchung ohne Flag" <| fun () ->
            let account = newAccount ()
            let entryId = importOne account (candidate $"tx-{Guid.NewGuid()}" None) okPush
            let entry = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isNone entry.FlagColor "keine Farbe"
            Expect.isNone entry.FlagName "kein Name"
            Expect.equal (countAskLenaEntries (Some account) |> Async.RunSynchronously) 0 "nicht gezählt"

        testCase "die Historie nennt die Herkunft Lena gefragt beim Import" <| fun () ->
            let account = newAccount ()
            let entryId = importOne account (candidate $"tx-{Guid.NewGuid()}" (Some "purple")) okPush
            let (EntryId guid) = entryId
            let lines = Ledger.QueryHandlers.getEntryHistory guid |> Async.RunSynchronously
            Expect.stringContains lines.Head.Description "Lena gefragt beim Import" "Herkunft im Satz"
            let plainId = importOne account (candidate $"tx-{Guid.NewGuid()}" None) okPush
            let (EntryId plainGuid) = plainId
            let plainLines = Ledger.QueryHandlers.getEntryHistory plainGuid |> Async.RunSynchronously
            Expect.isFalse (plainLines.Head.Description.Contains "Lena") "ohne Rückfrage kein Hinweis"

        testCase "Fehlschlag: die Buchung bleibt mit Flag und Push-Fehlschlag stehen, Erneut senden schickt das Flag mit" <| fun () ->
            let account = newAccount ()
            let entryId = importOne account (candidate $"tx-{Guid.NewGuid()}" (Some "purple")) failingPush
            let failed = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isFalse failed.LinkedToYnab "nicht verknüpft"
            Expect.isSome failed.PushToYnabFailure "Noch nicht in YNAB"
            Expect.equal failed.FlagColor (Some "purple") "Flag steht trotz Fehlschlag"
            let seen = ResizeArray<string option>()
            let push : EntryPush =
                fun entry ->
                    seen.Add (pushFlagColorOf entry)
                    async.Return (Ok "ynab-retry-d9e51")
            let outcome = retryPushToYnab (at.AddHours 1.0) push noTrigger entryId |> Async.RunSynchronously
            Expect.equal outcome (Ok PushToYnabSent) "gesendet"
            Expect.equal (List.ofSeq seen) [ Some "purple" ] "der Retry-Push trägt flag_color"
    ]

[<Tests>]
let retryBodyTests =
    let request : ManualTransactionRequest = {
        Amount = 12.34m
        IsOutflow = true
        PayeeName = "dm"
        CategoryId = None
        Date = DateTime(2026, 10, 26)
        Memo = None
    }
    testList "Import Lena fragen: erneutes Senden und Codec" [

        testCase "der Retry-Body trägt flag_color nur mit Farbe" <| fun () ->
            let withFlag = buildManualTransactionBodyWithFlag (Some "BB:x") (Some "purple") Fx.ynabAccount request
            let without = buildManualTransactionBodyWithFlag (Some "BB:x") None Fx.ynabAccount request
            Expect.stringContains withFlag "\"flag_color\": \"purple\"" "mit Farbe"
            Expect.isFalse (without.Contains "flag_color") "ohne Farbe kein Schlüssel"
            Expect.equal without (buildManualTransactionBodyWith (Some "BB:x") Fx.ynabAccount request) "Body ohne Flag wie bisher"

        testCase "ein altes Ereignis ohne Flag-Felder liest sich als kein Flag" <| fun () ->
            let old =
                """{"accountId":"0195c000-0000-7000-8000-000000000001","importId":"i","comdirectReference":"r","date":"2026-03-01T00:00:00.0000000Z","amount":{"amount":-1.0,"currency":"EUR"},"cleared":"Uncleared","approved":false}"""
            match decodePayload "EntryImportedFromComdirect" 2 old with
            | Ok (EntryImportedFromComdirect p) ->
                Expect.isNone p.FlagColor "keine Farbe"
                Expect.isNone p.FlagName "kein Name"
            | other -> failtest $"nicht lesbar: {other}"

        testCase "Farbe und Name überstehen Schreiben und Lesen, ohne Flag fehlen die Schlüssel" <| fun () ->
            let payload : EntryImportedFromComdirectPayload = {
                AccountId = newAccount ()
                ImportId = "i"
                ComdirectReference = "r"
                Date = DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                Amount = eur -1m
                Payee = None
                Memo = None
                Cleared = Uncleared
                Approved = false
                FlagColor = Some "purple"
                FlagName = Some "Lena"
            }
            let json = encodePayload (EntryImportedFromComdirect payload)
            Expect.equal (decodePayload "EntryImportedFromComdirect" 2 json) (Ok (EntryImportedFromComdirect payload)) "Roundtrip"
            let plainJson =
                encodePayload (EntryImportedFromComdirect { payload with FlagColor = None; FlagName = None })
            Expect.isFalse (plainJson.Contains "flag") "gespeicherte Form wie bisher"
    ]
