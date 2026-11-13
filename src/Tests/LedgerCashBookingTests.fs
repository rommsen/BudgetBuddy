module LedgerCashBookingTests

// Barbuchung für Bob (ledger-bk3q8): die reine Push-Funktion einer Kassenbuch-Buchung
// (`planEntryPush`), der gemeinsame Befehl (`cashBookingParts`, `addCashBooking`), die
// Lesefunktionen für Quick-Add-Konto und Kategorie und die Adapter mit Ports in `Server.Api`.
// Die Seite Quick Add samt `addQuickAdd` ist mit ledger-ea70s entfallen; der Actor `QuickAdd`
// bleibt für bestehende Ereignisse. Kein Fall
// erreicht YNAB: jeder YNAB-Aufruf ist eine Fälschung, die mitzählt.

open System

// CRITICAL: Testmodus setzen, BEVOR das Persistence-Modul initialisiert wird.
do Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")

open Expecto
open Shared.Domain
open Ledger.Events
open Ledger.Domain
open Ledger.Persistence
open Ledger.CommandHandlers
open Ledger.QueryHandlers

let private at = DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc)
let private day = DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)
let private noTrigger () = ()

let private freshAccount () = LedgerAccountId (Guid.NewGuid())
let private freshCategory (name: string) : CategoryRef =
    { CategoryId = YnabCategoryId (Guid.NewGuid()); CategoryName = name }

let private initialize () =
    initializeLedgerSchema ()
    global.Persistence.initializeDatabase ()

let private eventsOf (entryId: EntryId) = loadEventsForEntry entryId |> Async.RunSynchronously

let private eventTypesOf (entryId: EntryId) = eventsOf entryId |> List.map (fun r -> r.EventType)

/// Ein Push, der jeden Request mitschreibt.
let private recordingPush (result: YnabResult<string>) =
    let requests = ResizeArray<ManualTransactionRequest>()
    let push (request: ManualTransactionRequest) =
        requests.Add request
        async.Return result
    requests, push

/// `BB:` und die EntryId ohne Bindestriche, wie Barbuchungen sie vor ledger-ay81j trugen.
let private oldCashImportId (EntryId id) = "BB:" + id.ToString().Replace("-", "")

/// Die Schlüssel des `transaction`-Objekts im JSON-Rumpf eines Einzel-Posts.
let private transactionKeys (body: string) : string list =
    match
        Thoth.Json.Net.Decode.fromString
            (Thoth.Json.Net.Decode.field "transaction" (Thoth.Json.Net.Decode.keyValuePairs Thoth.Json.Net.Decode.value))
            body
    with
    | Ok pairs -> pairs |> List.map fst
    | Error err -> failtest $"Rumpf nicht lesbar: {err}"

/// Trägt der Rumpf den Schlüssel `import_id`?
let private hasImportId (body: string) : bool = transactionKeys body |> List.contains "import_id"

/// Fake-Handler für den echten Einzel-Post (`YnabClient.createTransactionOn`), nie gegen YNAB:
/// schreibt Rumpf, Adresse und Authorization je Anfrage mit und antwortet 201 mit einer neuen
/// YNAB-Id. Mit `Fails` wirft er wie ein Netzfehler.
type private PostRecorder() =
    inherit System.Net.Http.HttpMessageHandler()
    member val Bodies = ResizeArray<string>()
    member val Urls = ResizeArray<string>()
    member val Authorizations = ResizeArray<string>()
    member val CreatedIds = ResizeArray<string>()
    member val Fails = false with get, set
    override this.SendAsync(request: System.Net.Http.HttpRequestMessage, _ct: Threading.CancellationToken) =
        task {
            let! body = request.Content.ReadAsStringAsync()
            this.Bodies.Add body
            this.Urls.Add(request.RequestUri.ToString())
            this.Authorizations.Add(string request.Headers.Authorization)
            if this.Fails then raise (System.Net.Http.HttpRequestException "Zeitüberschreitung")
            let id = $"ynab-ay81j-{Guid.NewGuid()}"
            this.CreatedIds.Add id
            return
                new System.Net.Http.HttpResponseMessage(
                    System.Net.HttpStatusCode.Created,
                    Content = new System.Net.Http.StringContent($"""{{"data":{{"transaction":{{"id":"{id}"}}}}}}""")
                )
        }

/// Ein Spiegel-Lauf vor „Erneut senden“, der nichts verknüpft (ledger-ay81j).
let private mirrorFindsNothing () : Async<Result<MirrorRunSummary, string>> =
    async.Return (Ok { NewEntries = 0; ChangedEntries = 0; DeletedEntries = 0; ServerKnowledge = 0L })

/// Legt ein mit YNAB verknüpftes Kassenbuch-Konto an (wie nach einem Spiegel-Lauf).
let private mirroredAccount () : LedgerAccount =
    let account = {
        Id = freshAccount ()
        Name = $"Bar-{Guid.NewGuid()}"
        Kind = CashAccount
        Link = YnabMirrored (YnabAccountId (Guid.NewGuid()))
        Closed = false
    }
    saveAccount account |> Async.RunSynchronously
    account

let private ynabIdOf (account: LedgerAccount) =
    match account.Link with
    | YnabMirrored id -> id
    | BbOwned -> failtest "Konto ohne YNAB-Verknüpfung"

let private settingsFrom (pairs: (string * string) list) : string -> Async<string option> =
    let map = Map.ofList pairs
    fun key -> async.Return (Map.tryFind key map)

// ============================================
// Reine Push-Funktion (aus `Server.Api.entryPush`)
// ============================================

let private entry (accountId: LedgerAccountId) (amount: decimal) : Entry = {
    Id = newEntryId ()
    AccountId = accountId
    Date = day
    Amount = { Amount = amount; Currency = "EUR" }
    Payee = Some "Kiosk"
    Memo = Some "Notiz"
    Category = None
    Splits = []
    Cleared = Uncleared
    TransferEntryId = None
    TransferAccountId = None
    SplitTransferCounterparts = Map.empty
    DeletedInYnab = false
    Approved = false
    FlagColor = None
    FlagName = None
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    AmountWriteBackFailure = None
    PayeeWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    PayeeBeforeTransfer = None
    PossibleDuplicate = None; PendingReplacement = None; MergedIntoEntryId = None
}

let private linkedAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc01")
let private linkedYnabId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc02")
let private ownAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc03")

let private accounts : LedgerAccount list = [
    { Id = linkedAccountId; Name = "Bar"; Kind = CashAccount; Link = YnabMirrored linkedYnabId; Closed = false }
    { Id = ownAccountId; Name = "Eigen"; Kind = CashAccount; Link = BbOwned; Closed = false }
]

let private planOk (links: EntryLink list) (e: Entry) : EntryPushPlan =
    match planEntryPush accounts None links [] e with
    | Ok plan -> plan
    | Error reason -> failtest $"Push sollte möglich sein, war: {reason}"

[<Tests>]
let planEntryPushTests =
    testList "Ledger Push einer Buchung (rein)" [
        testCase "eine Ausgabe geht mit Betrag ohne Vorzeichen und als Abfluss auf das verknüpfte YNAB-Konto" <| fun () ->
            let plan = planOk [] (entry linkedAccountId -12.50m)
            Expect.equal plan.Request.Amount 12.50m "Betrag ohne Vorzeichen"
            Expect.isTrue plan.Request.IsOutflow "Abfluss"
            Expect.equal plan.YnabAccountId linkedYnabId "YNAB-Konto des Kassenbuch-Kontos"

        testCase "eine Einnahme geht als Zufluss" <| fun () ->
            let plan = planOk [] (entry linkedAccountId 40m)
            Expect.equal plan.Request.Amount 40m "Betrag"
            Expect.isFalse plan.Request.IsOutflow "Zufluss"

        testCase "eine Buchung ohne Kategorie geht ohne Kategorie, eine mit Kategorie mit deren Id" <| fun () ->
            Expect.isNone (planOk [] (entry linkedAccountId -1m)).Request.CategoryId "ohne Kategorie"
            let category = freshCategory "Kaffee"
            let plan = planOk [] { entry linkedAccountId -1m with Category = Some category }
            Expect.equal plan.Request.CategoryId (Some category.CategoryId) "Id der Kategorie"

        testCase "eine Buchung ohne Payee geht mit leerem Payee" <| fun () ->
            let plan = planOk [] { entry linkedAccountId -1m with Payee = None }
            Expect.equal plan.Request.PayeeName "" "leerer Text"

        testCase "die import_id kommt aus den Verknüpfungen, nach einem Ersatz die aktuelle" <| fun () ->
            let e = entry linkedAccountId -1m
            Expect.isNone (planOk [] e).ImportId "ohne Verknüpfung keine import_id"
            let links = [
                { EntryId = e.Id; System = ImportIdSystem; Value = "BB:bk3q8:r1" }
                { EntryId = e.Id; System = ImportIdSystem; Value = "BB:bk3q8" }
                { EntryId = e.Id; System = YnabSystem; Value = "ynab-bk3q8" }
            ]
            Expect.equal (planOk links e).ImportId (Some "BB:bk3q8:r1") "die aktuelle nach dem Ersatz"

        testCase "eine in BB erfasste Buchung geht ohne import_id, auch mit alter BB:-Verknüpfung (ledger-ay81j)" <| fun () ->
            // Verhindert den Bug, dass YNAB eine erneut gesendete Barbuchung mit import_id wie einen
            // Bankimport gegen eine ältere Handbuchung abglich und verbarg.
            let e = entry linkedAccountId -1m
            let links = [ { EntryId = e.Id; System = ImportIdSystem; Value = "BB:ay81j" } ]
            let captured actor : (Actor * LedgerEventBody) option =
                Some (
                    actor,
                    CashExpenseAdded {
                        AccountId = linkedAccountId; Date = day; Amount = e.Amount; Payee = e.Payee; Memo = e.Memo
                        Category = None; Cleared = Uncleared; Approved = false; ImportId = Some "BB:ay81j" }
                )
            for actor in [ QuickAdd; Bob; UserInBb ] do
                match planEntryPush accounts (captured actor) links [] e with
                | Ok plan -> Expect.isNone plan.ImportId $"erfasst von {actor}: ohne import_id"
                | Error reason -> failtest reason
            Expect.equal (planOk links e).ImportId (Some "BB:ay81j") "ohne erfassten Ursprung bleibt die import_id"

        testCase "die Flag-Farbe der Buchung geht mit" <| fun () ->
            Expect.isNone (planOk [] (entry linkedAccountId -1m)).FlagColor "ohne Flag keine Farbe"
            let plan = planOk [] { entry linkedAccountId -1m with FlagColor = Some "purple" }
            Expect.equal plan.FlagColor (Some "purple") "Farbe des BB-Stands"

        // ledger-at63w: der Übertrag geht mit der Übertrags-Payee aus dem Log (vorher in Api.entryPushWith).
        testCase "ein Übertrag geht mit der Übertrags-Payee des letzten gesetzten Ziels, mit Kategorie keiner" <| fun () ->
            let target, other = YnabAccountId (Guid.NewGuid()), YnabAccountId (Guid.NewGuid())
            let e = { entry linkedAccountId -5m with TransferAccountId = Some target }
            let setTo (account: YnabAccountId) (payeeId: string) : LedgerEvent =
                { EntryId = e.Id; OccurredAt = at; Actor = UserInBb
                  Body =
                    EntryTransferTargetSetInBb {
                        OldCategory = None; OldPayee = None; OldTransferAccountId = None
                        NewTransferAccountId = account; NewTransferAccountName = "Ziel"
                        TransferPayeeId = payeeId; NewPayee = Some "Transfer : Ziel" } }
            let events = [ setTo target "payee-alt"; setTo other "payee-anderes"; setTo target "payee-neu" ]
            match planEntryPush accounts None [] events e with
            | Ok plan -> Expect.equal plan.TransferPayeeId (Some "payee-neu") "Payee des letzten Setzens auf dieses Ziel"
            | Error reason -> failtest reason
            Expect.equal
                (planEntryPush accounts None [] [ setTo other "payee-anderes" ] e)
                (Error transferPayeeUnknownReason)
                "ohne Setzen auf dieses Ziel kein Push"
            let withCategory = { e with Category = Some (freshCategory "Sparen") }
            Expect.isNone (planOk [] withCategory).TransferPayeeId "mit Kategorie keine Übertrags-Payee"

        testCase "ein Konto ohne YNAB-Verknüpfung ergibt keinen Request, sondern den Grund" <| fun () ->
            Expect.equal (planEntryPush accounts None [] [] (entry ownAccountId -1m)) (Error pushNotPossibleReason) "BB-eigenes Konto"
            Expect.equal (planEntryPush accounts None [] [] (entry (freshAccount ()) -1m)) (Error pushNotPossibleReason) "unbekanntes Konto"
    ]

// ============================================
// Barbuchung: Kassenbuch-Eingabe und Request aus denselben Werten
// ============================================

let private booking (accountId: LedgerAccountId) : CashBooking = {
    AccountId = accountId
    Date = day
    Amount = 12.50m
    IsOutflow = true
    Payee = "  Kiosk  "
    Memo = Some "  Kaffee am Bahnhof "
    Category = None
}

[<Tests>]
let cashBookingPartsTests =
    testList "Ledger Barbuchung (rein)" [
        testCase "eine Ausgabe steht negativ im Kassenbuch und geht positiv als Abfluss an YNAB" <| fun () ->
            let input, request = cashBookingParts (booking (freshAccount ()))
            Expect.equal input.Amount { Amount = -12.50m; Currency = "EUR" } "negativ im Kassenbuch"
            Expect.equal request.Amount 12.50m "positiv an YNAB"
            Expect.isTrue request.IsOutflow "Abfluss"

        testCase "eine Einnahme steht positiv im Kassenbuch und geht als Zufluss an YNAB" <| fun () ->
            let input, request = cashBookingParts { booking (freshAccount ()) with IsOutflow = false }
            Expect.equal input.Amount.Amount 12.50m "positiv im Kassenbuch"
            Expect.equal request.Amount 12.50m "positiv an YNAB"
            Expect.isFalse request.IsOutflow "Zufluss"

        testCase "Payee und Memo werden an beiden Stellen gleich getrimmt, leere Werte fallen weg" <| fun () ->
            let input, request = cashBookingParts (booking (freshAccount ()))
            Expect.equal (input.Payee, request.PayeeName) (Some "Kiosk", "Kiosk") "Payee getrimmt"
            Expect.equal (input.Memo, request.Memo) (Some "Kaffee am Bahnhof", Some "Kaffee am Bahnhof") "Memo getrimmt"
            let input, request = cashBookingParts { booking (freshAccount ()) with Payee = "  "; Memo = Some " " }
            Expect.equal (input.Payee, request.PayeeName) (None, "") "leerer Payee"
            Expect.equal (input.Memo, request.Memo) (None, None) "leeres Memo fällt weg"

        testCase "die Kategorie steht mit Namen im Kassenbuch und geht mit derselben Id an YNAB" <| fun () ->
            let category = freshCategory "Kaffee"
            let input, request = cashBookingParts { booking (freshAccount ()) with Category = Some category }
            Expect.equal input.Category (Some category) "Id und Name im Kassenbuch"
            Expect.equal request.CategoryId (Some category.CategoryId) "dieselbe Id an YNAB"
            let input, request = cashBookingParts (booking (freshAccount ()))
            Expect.equal (input.Category, request.CategoryId) (None, None) "ohne Kategorie an beiden Stellen"
    ]

// ============================================
// Barbuchung: der gemeinsame Befehl (Vorlage `addCashExpense`)
// ============================================

[<Tests>]
let addCashBookingTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testSequenced
    <| testList "Ledger Barbuchung (Befehl)" [
        testCase "gelingt der Push, steht die Buchung verknüpft unter der vorgegebenen Id, mit Actor Bob" <| fun () ->
            let entryId = newEntryId ()
            let ynabId = $"ynab-bk3q8-{Guid.NewGuid()}"
            let requests, push = recordingPush (Ok ynabId)
            let result = addCashBooking Bob at entryId push noTrigger (booking (freshAccount ())) |> Async.RunSynchronously
            Expect.equal result (Ok entryId) "vorgegebene Id"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryLinkedToYnab" ] "gebucht und verknüpft"
            Expect.equal (eventsOf entryId |> List.map (fun r -> r.Event.Actor)) [ Bob; Bob ] "Actor Bob"
            Expect.equal (findEntryByLink YnabSystem ynabId |> Async.RunSynchronously) (Some entryId) "über die Push-Antwort verknüpft"
            Expect.equal requests.Count 1 "ein Push"
            Expect.equal requests[0] (snd (cashBookingParts (booking (freshAccount ())))) "der Request der Barbuchung"

        testCase "scheitert der Push, bleibt die Buchung unter der vorgegebenen Id stehen, mit Fehlschlag" <| fun () ->
            // ledger-z71ka: die vorgegebene Id gilt auch nach einem Push-Fehler.
            let entryId = newEntryId ()
            let _, push = recordingPush (Error (YnabError.NetworkError "Timeout"))
            let result = addCashBooking QuickAdd at entryId push noTrigger (booking (freshAccount ())) |> Async.RunSynchronously
            Expect.equal result (Error (YnabError.NetworkError "Timeout")) "Push-Fehler durchgereicht"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "Buchung bleibt, Fehlschlag steht"
            Expect.equal (eventsOf entryId |> List.map (fun r -> r.Event.Actor)) [ QuickAdd; QuickAdd ] "Actor Quick Add"
            let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isFalse stored.LinkedToYnab "nicht verknüpft"
            Expect.equal stored.Amount.Amount -12.50m "Ausgabe negativ im Kassenbuch"
    ]

// ============================================
// Quick-Add-Konto und Kategorie (Lesefunktionen)
// ============================================

[<Tests>]
let quickAddAccountTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testList "Ledger Quick-Add-Konto auflösen" [
        testCase "ohne Einstellung: fester Satz" <| fun () ->
            let result = resolveQuickAddAccountWith (settingsFrom []) |> Async.RunSynchronously
            Expect.equal result (Error quickAddAccountMissingText) "nicht eingerichtet"

        testCase "keine Guid: fester Satz" <| fun () ->
            let result =
                resolveQuickAddAccountWith (settingsFrom [ quickAddAccountSettingKey, "kein-konto" ]) |> Async.RunSynchronously
            Expect.equal result (Error quickAddAccountInvalidText) "ungültig"

        testCase "ohne gespiegeltes Kassenbuch-Konto: fester Satz" <| fun () ->
            let setting = settingsFrom [ quickAddAccountSettingKey, Guid.NewGuid().ToString() ]
            Expect.equal (resolveQuickAddAccountWith setting |> Async.RunSynchronously) (Error quickAddAccountNotMirroredText) "nicht gespiegelt"

        testCase "mit gespiegeltem Konto: Kassenbuch-Konto, YNAB-Konto und Name" <| fun () ->
            let account = mirroredAccount ()
            let (YnabAccountId guid) = ynabIdOf account
            let result = resolveQuickAddAccountWith (settingsFrom [ quickAddAccountSettingKey, guid.ToString() ]) |> Async.RunSynchronously
            Expect.equal result (Ok { LedgerAccountId = account.Id; YnabAccountId = ynabIdOf account; AccountName = account.Name }) "aufgelöst"
    ]

/// Kategorienquellen, die ihre Abrufe zählen.
type private CategorySources(mirrored: Map<Guid, string>, ynab: YnabResult<YnabCategory list>) =
    let mutable ynabCalls = 0
    member _.YnabCalls = ynabCalls
    member _.Mirrored () = async.Return mirrored
    member _.FromYnab () =
        ynabCalls <- ynabCalls + 1
        async.Return ynab
    member this.Resolve(categoryId) = resolveCategoryRefWith this.Mirrored this.FromYnab categoryId

let private ynabCategory (category: CategoryRef) : YnabCategory =
    { Id = category.CategoryId; Name = category.CategoryName; GroupName = "Alltag"; Available = None; Hidden = false }

let private guidOf (YnabCategoryId id) = id

[<Tests>]
let categoryResolutionTests =
    testList "Ledger Kategorie einer Barbuchung auflösen" [
        testCase "der Name kommt aus den gespiegelten Stammdaten, ohne YNAB-Abruf" <| fun () ->
            let category = freshCategory "Kaffee"
            let sources = CategorySources(Map.ofList [ guidOf category.CategoryId, "Kaffee" ], Error (YnabError.RateLimitExceeded 60))
            Expect.equal (sources.Resolve category.CategoryId |> Async.RunSynchronously) (Ok category) "aus den Stammdaten"
            Expect.equal sources.YnabCalls 0 "YNAB nicht gefragt"

        testCase "fehlt die Id in den Stammdaten, kommt der Name von YNAB" <| fun () ->
            let category = freshCategory "Neu"
            let sources = CategorySources(Map.empty, Ok [ ynabCategory (freshCategory "Andere"); ynabCategory category ])
            Expect.equal (sources.Resolve category.CategoryId |> Async.RunSynchronously) (Ok category) "von YNAB"
            Expect.equal sources.YnabCalls 1 "einmal gefragt"

        testCase "scheitert auch der YNAB-Abruf, ist das ein Ladefehler" <| fun () ->
            let sources = CategorySources(Map.empty, Error (YnabError.RateLimitExceeded 60))
            Expect.equal
                (sources.Resolve (freshCategory "x").CategoryId |> Async.RunSynchronously)
                (Error categoryLoadFailedText)
                "nicht geladen"

        testCase "kennt keine Quelle die Id, gibt es die Kategorie nicht mehr" <| fun () ->
            let sources = CategorySources(Map.empty, Ok [ ynabCategory (freshCategory "Andere") ])
            Expect.equal
                (sources.Resolve (freshCategory "x").CategoryId |> Async.RunSynchronously)
                (Error categoryUnknownText)
                "unbekannt"
    ]

// ============================================
// Adapter mit Ports (`Server.Api`)
// ============================================

let private fakeYnab = [ "ynab_token", "token-kuenstlich"; "ynab_default_budget_id", "budget-kuenstlich" ]

[<Tests>]
let adapterTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testSequenced
    <| testList "Ledger Barbuchung (Adapter mit Ports)" [
        testCase "Import-Push und erzwungener Import ohne gespiegeltes Kassenbuch-Konto: Fehler vor jedem Schreiben" <| fun () ->
            let setting = settingsFrom (fakeYnab @ [ "ynab_default_account_id", Guid.NewGuid().ToString() ])
            match Server.Api.importTargetWith setting 3 |> Async.RunSynchronously with
            | Error (SyncError.YnabImportFailed (count, message)) ->
                Expect.equal count 3 "nennt die Buchungen"
                Expect.stringContains message "Jetzt spiegeln" "sagt, was zu tun ist"
            | other -> failtest $"Fehler erwartet, war: {other}"

        testCase "Import-Push mit ungültiger Import-Konto-Id: fester Satz statt Absturz" <| fun () ->
            // Verhindert den Bug, dass `Guid.Parse` bei kaputtem `ynab_default_account_id` warf und
            // Roman nur "Interner Fehler im Server" sah (Review rv/backend).
            let setting = settingsFrom (fakeYnab @ [ "ynab_default_account_id", "keine-guid" ])
            match Server.Api.importTargetWith setting 2 |> Async.RunSynchronously with
            | Error (SyncError.YnabImportFailed (count, message)) ->
                Expect.equal count 2 "nennt die Buchungen"
                Expect.equal message Server.Api.importAccountInvalidText "fester Satz"
            | other -> failtest $"Fehler erwartet, war: {other}"

        testCase "Erneut senden einer Buchung ohne YNAB-Konto: der Grund ist der Satz der Push-Planung" <| fun () ->
            // Verhindert den Bug, dass `pushNotPossibleReason` als `AccountNotFound` verpackt war und als
            // "Konto in YNAB nicht gefunden" im Ereignis `EntryPushToYnabFailed` landete (Review rv/backend).
            let create _ _ _ _ _ _ _ = async.Return (Ok "nie")
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            let result = push (entry (freshAccount ()) -7m) |> Async.RunSynchronously
            Expect.equal (result |> Result.mapError ynabErrorText) (Error pushNotPossibleReason) "Satz beim Nutzer"

        testCase "Erneut senden pusht den Request der reinen Push-Funktion auf das verknüpfte YNAB-Konto" <| fun () ->
            let account = mirroredAccount ()
            let e = { entry account.Id -7m with FlagColor = Some "purple" }
            let calls = ResizeArray<string option * string option * YnabAccountId * ManualTransactionRequest>()
            let create importId flag _ _ _ ynabAccount request =
                calls.Add((importId, flag, ynabAccount, request))
                async.Return (Ok "ynab-neu")
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            Expect.equal (push e |> Async.RunSynchronously) (Ok "ynab-neu") "gesendet"
            let expected = (planEntryPush [ account ] None [] [] e) |> Result.map (fun p -> p.ImportId, p.FlagColor, p.YnabAccountId, p.Request)
            Expect.equal (calls |> List.ofSeq |> List.map Ok) [ expected ] "genau der geplante Push"
    ]

// ============================================
// Versand ohne import_id über den echten Einzel-Post (ledger-ay81j, Fake-Handler)
// ============================================

/// Die Konten des Budgets für „Neue Buchung“: jedes gespiegelte Konto mit seiner Übertrags-Payee.
let private budgetAccountsOf (accounts: LedgerAccount list) : Server.YnabClient.TransferAccount list =
    accounts
    |> List.map (fun a ->
        { Id = ynabIdOf a; Name = a.Name; OnBudget = true; Closed = false; TransferPayeeId = Some $"payee-{a.Name}" })

/// Legt eine Buchung mit ihrem ersten Ereignis und einer `importid`-Verknüpfung an, wie vor
/// ledger-ay81j: eine Barbuchung mit `BB:<EntryId>` oder ein Comdirect-Import mit `BB:<Reference>`.
let private seedUnlinked (first: EntryId -> LedgerEvent) : EntryId =
    let entryId = newEntryId ()
    appendAndApply [
        first entryId
        { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = EntryPushToYnabFailed { Reason = "Zeitüberschreitung" } }
    ]
    |> Async.RunSynchronously
    entryId

let private oldCapture (account: LedgerAccount) : EntryId =
    seedUnlinked (fun entryId ->
        let input, _ = cashBookingParts (booking account.Id)
        { EntryId = entryId; OccurredAt = at; Actor = QuickAdd
          Body =
            CashExpenseAdded {
                AccountId = input.AccountId; Date = input.Date; Amount = input.Amount; Payee = input.Payee
                Memo = input.Memo; Category = input.Category; Cleared = Uncleared; Approved = false
                ImportId = Some (oldCashImportId entryId) } })

let private comdirectImport (account: LedgerAccount) (reference: string) : EntryId =
    seedUnlinked (fun entryId ->
        { EntryId = entryId; OccurredAt = at; Actor = ComdirectImport
          Body =
            EntryImportedFromComdirect {
                AccountId = account.Id; ImportId = $"BB:{reference}"; ComdirectReference = reference
                Date = day; Amount = { Amount = -50m; Currency = "EUR" }; Payee = Some "Markt"; Memo = None
                Cleared = Uncleared; Approved = false; FlagColor = None; FlagName = None } })

[<Tests>]
let liveSinglePostTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testSequenced
    <| testList "Ledger Versand ohne import_id (echter Einzel-Post, ledger-ay81j)" [
        // Regression ledger-ay81j: YNAB glich eine Barbuchung mit import_id wie einen Bankimport gegen
        // eine eine Woche ältere Handbuchung gleichen Betrags ab und verbarg sie. Vor dem Fix trug jeder
        // dieser Rümpfe `import_id` `BB:<EntryId>`.
        testCase "Neue Buchung sendet über den echten Einzel-Post ohne import_id, auch mit Übertrag" <| fun () ->
            let bar, tagesgeld = mirroredAccount (), mirroredAccount ()
            let handler = new PostRecorder()
            use http = new System.Net.Http.HttpClient(handler)
            let importAccount = Guid.NewGuid().ToString()
            let ports = {
                Server.Api.ledgerBookingPortsOn http with
                    Setting = settingsFrom (fakeYnab @ [ importAccountSettingKey, importAccount ])
                    BudgetAccounts = fun _ _ -> async.Return (Ok (budgetAccountsOf [ bar; tagesgeld ]))
                    LoadYnabCategories = fun _ _ -> async.Return (Ok [])
                    NewEntryId = newEntryId
                    TriggerMirror = noTrigger
            }
            let (LedgerAccountId barGuid) = bar.Id
            let request : LedgerBookingRequest = {
                AccountId = barGuid; Amount = 8.60m; IsOutflow = true; PayeeName = "Bäckerei"
                Target = None; Date = day; Memo = None
            }
            for target in [ None; Some (BookAsTransfer (ynabIdOf tagesgeld)) ] do
                match Server.Api.addLedgerBookingWith ports day at { request with Target = target } |> Async.RunSynchronously with
                | Ok created -> Expect.isNone created.PushFailure $"gesendet ({target})"
                | Error e -> failtest e
            Expect.equal (handler.Bodies |> Seq.map hasImportId |> List.ofSeq) [ false; false ] "beide ohne import_id"
            Expect.contains (transactionKeys handler.Bodies[1]) "payee_id" "der Übertrag geht mit der Übertrags-Payee"

        testCase "Erneut senden einer alten Erfassung mit BB:-Verknüpfung geht ohne import_id" <| fun () ->
            let account = mirroredAccount ()
            let entryId = oldCapture account
            let handler = new PostRecorder()
            use http = new System.Net.Http.HttpClient(handler)
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) (Server.YnabClient.createTransactionOn http) |> Async.RunSynchronously
            let outcome = retryPushToYnab (at.AddHours 1.0) loadEntryOrigin mirrorFindsNothing push noTrigger entryId |> Async.RunSynchronously
            Expect.equal outcome (Ok PushToYnabSent) "gesendet"
            Expect.equal (handler.Bodies |> Seq.map hasImportId |> List.ofSeq) [ false ] "ohne import_id trotz alter Verknüpfung"

        testCase "Intakt: Erneut senden eines Comdirect-Imports behält BB:<Reference>" <| fun () ->
            let account = mirroredAccount ()
            let reference = $"ay81j-{Guid.NewGuid():N}".Substring(0, 20)
            let entryId = comdirectImport account reference
            let handler = new PostRecorder()
            use http = new System.Net.Http.HttpClient(handler)
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) (Server.YnabClient.createTransactionOn http) |> Async.RunSynchronously
            let mutable mirrors = 0
            let mirror () =
                mirrors <- mirrors + 1
                mirrorFindsNothing ()
            let outcome = retryPushToYnab (at.AddHours 1.0) loadEntryOrigin mirror push noTrigger entryId |> Async.RunSynchronously
            Expect.equal outcome (Ok PushToYnabSent) "gesendet"
            let importId =
                Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction"; "import_id" ] Thoth.Json.Net.Decode.string) handler.Bodies[0]
            Expect.equal importId (Ok $"BB:{reference}") "die import_id des Imports"
            Expect.equal mirrors 0 "ein Comdirect-Import ruft den Spiegel nicht"
    ]

// ============================================
// Bobs echte Umgebung: Quick-Add-Konto über die gemeinsame Lesefunktion
// ============================================

[<Tests>]
let botLiveTargetTests =
    testList "Bot echte Umgebung (Quick-Add-Konto)" [
        testCase "die echte Umgebung löst das Quick-Add-Konto über die gemeinsame Lesefunktion auf" <| fun () ->
            // Belegt den Live-Port `ResolveTarget` (Test-Doktrin Punkt 3). Die Test-Datenbank
            // hat kein gespiegeltes Quick-Add-Konto; die Negativtabelle setzt die Einstellung
            // kurz auf eine fremde Id, deshalb gilt einer der beiden festen Sätze.
            initialize ()
            let live = Server.BotApi.liveDeps (TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin")
            match live.ResolveTarget () |> Async.RunSynchronously with
            | Error reason ->
                Expect.contains [ quickAddAccountMissingText; quickAddAccountNotMirroredText ] reason "Satz der Lesefunktion"
            | Ok target -> failtest $"kein Konto erwartet, war: {target}"
    ]

// ============================================
// Bob: Commit über die gemeinsame Barbuchung
// ============================================

[<Tests>]
let botCashBookingTests =
    testList "Bot Barbuchung (Commit)" [
        testCase "Bobs Commit pusht den Request der Barbuchung auf das YNAB-Konto der Vorschau" <| fun () ->
            initialize ()
            let ledgerAccount = freshAccount ()
            let ynabAccount = YnabAccountId (Guid.NewGuid())
            let category : YnabCategory =
                { Id = YnabCategoryId (Guid.NewGuid()); Name = "Haushaltshilfe"; GroupName = "Haushalt"; Available = None; Hidden = false }
            // Ein eigener, ferner Tag, damit keine Tagesgrenze eines anderen Tests zählt.
            let today = DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(float (Random.Shared.Next(0, 3000)))
            let pushes = ResizeArray<YnabAccountId * ManualTransactionRequest>()
            // Bobs echter Push (`BotApi.botPushOn`) gegen einen Fake-Handler (ledger-ay81j).
            let handler = new PostRecorder()
            use http = new System.Net.Http.HttpClient(handler)
            let botPush = Server.BotApi.botPushOn http (fun () -> async.Return (Ok ("bot-token-kuenstlich", YnabBudgetId "budget-bot")))
            let deps : Server.BotApi.BotDeps = {
                Token = fun () -> Some "bk3q8-token"
                MaxAmountEur = fun () -> Server.ServerConfig.Default
                Now = fun () -> today.AddHours 10.0
                BerlinZone = TimeZoneInfo.FindSystemTimeZoneById "Europe/Berlin"
                LoadCategories = fun () -> async.Return (Ok [ category ])
                ResolveTarget =
                    fun () ->
                        async.Return (Ok { LedgerAccountId = ledgerAccount; YnabAccountId = ynabAccount; AccountName = "Bar" } : Result<Server.BotApi.BotTarget, string>)
                Push =
                    fun pushed ->
                        pushes.Add pushed
                        botPush pushed
                AddCashExpense = addCashExpense
                TriggerMirror = noTrigger
                Store = Server.BotApi.BotPreviewStore()
                Limiter = Server.BotApi.BotRateLimiter(1000, Server.BotApi.rateLimitWindow)
                Categories = Server.BotApi.BotCategoryCache()
            }
            let call (path: string) (body: string) =
                let ctx = Microsoft.AspNetCore.Http.DefaultHttpContext()
                ctx.Request.Method <- "POST"
                ctx.Request.Path <- Microsoft.AspNetCore.Http.PathString path
                ctx.Request.Headers["Authorization"] <- "Bearer bk3q8-token"
                ctx.Request.Body <- new IO.MemoryStream(Text.Encoding.UTF8.GetBytes body)
                let responseBody = new IO.MemoryStream()
                ctx.Response.Body <- responseBody
                let next : Giraffe.Core.HttpFunc = fun c -> Threading.Tasks.Task.FromResult(Some c)
                (Server.BotApi.handler deps next ctx).GetAwaiter().GetResult() |> ignore
                responseBody.Position <- 0L
                ctx.Response.StatusCode, Text.Json.JsonDocument.Parse((new IO.StreamReader(responseBody)).ReadToEnd()).RootElement
            let payee = $"bk3q8-{Guid.NewGuid()}"
            let date = today.ToString("yyyy-MM-dd")
            let status, json =
                call "/bot/transactions/preview" $"""{{"amount": 5.25, "direction": "outflow", "payee": "{payee}", "category": "Haushaltshilfe", "date": "{date}"}}"""
            Expect.equal status 200 "Vorschau"
            let previewId = json.GetProperty("preview_id").GetString()
            let status, json = call $"/bot/transactions/{previewId}/commit" ""
            Expect.equal status 200 "Commit"
            let entryId = EntryId (Guid.Parse(json.GetProperty("entry_id").GetString()))
            let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.equal stored.Amount.Amount -5.25m "Ausgabe negativ im Kassenbuch"
            Expect.equal stored.AccountId ledgerAccount "Kassenbuch-Konto der Vorschau"
            Expect.equal
                (pushes |> Seq.map (fun (account, r) -> account, r.Amount, r.IsOutflow, r.CategoryId, r.PayeeName) |> List.ofSeq)
                [ ynabAccount, 5.25m, true, Some category.Id, payee ]
                "ein Push auf das YNAB-Konto der Vorschau, mit dem Request der Barbuchung"
            // Regression ledger-ay81j: Bob pushte mit der import_id `BB:<EntryId>`, YNAB glich die Buchung
            // gegen eine ältere Handbuchung ab und verbarg sie. Jetzt ohne import_id.
            Expect.equal (handler.Bodies |> Seq.map hasImportId |> List.ofSeq) [ false ] "ein Post ohne import_id"
            Expect.stringContains handler.Urls[0] "/budgets/budget-bot/transactions" "Budget aus dem Kontext"
            Expect.equal handler.Authorizations[0] "Bearer bot-token-kuenstlich" "Token aus dem Kontext"
            Expect.isTrue (json.GetProperty("pushed_to_ynab").GetBoolean()) "pushed_to_ynab"
            Expect.isTrue stored.LinkedToYnab "über die Antwort verknüpft"
    ]

// ============================================
// Verlorene Push-Antwort ohne zweite Buchung (ledger-p3vq8, ledger-ay81j)
// ============================================

/// Die YNAB-Buchung, die der Spiegel zu einer Barbuchung sieht: dieselben Werte wie im
/// Kassenbuch, mit der `import_id`, die der Push trug (seit ledger-ay81j keine).
let private observedCashBooking (ynabTxId: string) (ynabAccount: YnabAccountId) (importId: string option) : Server.YnabObservations.YnabObservation = {
    YnabTransactionId = ynabTxId
    AccountId = ynabAccount
    ImportId = importId
    Date = day
    Amount = { Amount = -12.50m; Currency = "EUR" }
    Payee = Some "Kiosk"
    Memo = Some "Kaffee am Bahnhof"
    Category = None
    Splits = []
    MatchedYnabTransactionId = None
    SplitTransfers = []
    Cleared = Uncleared
    TransferAccountId = None
    TransferYnabTransactionId = None
    Deleted = false
    Approved = false
    FlagColor = None
    FlagName = None
}

/// Ein Spiegel-Lauf, der genau diese YNAB-Buchung sieht, in einem eigenen Budget.
let private mirrorSees (obs: Server.YnabObservations.YnabObservation) : MirrorRunSummary =
    let fetchers = {
        emptyMirrorFetchers with
            FetchAccounts =
                fun _ ->
                    async.Return (Ok { Accounts = [ { AccountId = obs.AccountId; Name = "Bar"; Closed = false; Deleted = false } ]; ServerKnowledge = 7L })
            FetchTransactions = fun _ -> async.Return (Ok { Observations = [ obs ]; ServerKnowledge = 7L })
    }
    match runMirror (at.AddHours 3.0) fetchers (YnabBudgetId $"budget-p3vq8-{Guid.NewGuid()}") |> Async.RunSynchronously with
    | Ok summary -> summary
    | Error err -> failtest $"Lauf sollte gelingen, war: {err}"

/// Führt SQL gegen die Test-Datenbank aus (nur für das Auslösen eines Schreibfehlers).
let private executeSql (sql: string) (value: string) =
    use conn = global.Persistence.getConnection ()
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- sql
    cmd.Parameters.AddWithValue("@value", value) |> ignore
    cmd.ExecuteNonQuery() |> ignore

[<Tests>]
let lostPushAnswerTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())
    /// THEN-Teil: keine zweite Buchung, die Barbuchung ist durch den Spiegel verknüpft, kein Fehlschlag offen.
    let expectLinkedByMirror (account: LedgerAccount) (entryId: EntryId) (ynabTxId: string) (summary: MirrorRunSummary) =
        Expect.equal summary.NewEntries 0 "keine neue Buchung"
        Expect.hasLength (loadEntriesForAccount account.Id |> Async.RunSynchronously) 1 "genau eine Buchung auf dem Konto"
        Expect.equal (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously) (Some entryId) "die YNAB-Id zeigt auf die Barbuchung"
        let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
        Expect.isTrue stored.LinkedToYnab "verknüpft"
        Expect.isNone stored.PushToYnabFailure "kein Fehlschlag offen"
        Expect.contains
            (eventsOf entryId |> List.map (fun r -> r.EventType, r.Event.Actor))
            ("EntryLinkedToYnab", YnabMirror)
            "der Spiegel verknüpft"

    testSequenced
    <| testList "Ledger Barbuchung (verlorene Antwort, ledger-ay81j)" [
        testCase "Kaputt: der Push scheitert mit Netzfehler, YNAB hat angelegt; der Spiegel verknüpft die offene Erfassung" <| fun () ->
            // GIVEN: YNAB legt die Buchung ohne import_id an, BB sieht nur die Zeitüberschreitung und
            // schreibt `EntryPushToYnabFailed`. Ohne die dritte Match-Stufe legte der Spiegel eine zweite
            // Kassenbuch-Buchung an.
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let _, push = recordingPush (Error (YnabError.NetworkError "Zeitüberschreitung"))
            addCashBooking QuickAdd at entryId push noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "Fehlschlag steht"
            let ynabTxId = $"ynab-ay81j-{Guid.NewGuid()}"

            // WHEN: der Spiegel sieht die YNAB-Buchung, ohne import_id, Payee anders geschrieben.
            let summary = mirrorSees { observedCashBooking ynabTxId (ynabIdOf account) None with Payee = Some " KIOSK " }

            // THEN
            expectLinkedByMirror account entryId ynabTxId summary

        testCase "Kaputt: der Push gelingt, das Schreiben von EntryLinkedToYnab wirft (Ausgang unbekannt); der Spiegel verknüpft" <| fun () ->
            // GIVEN: kein Fehlschlag im Log, der Ausgang ist unbekannt (`PushOutcomeUnknown`).
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let ynabTxId = $"ynab-ay81j-{Guid.NewGuid()}"
            let push (_: ManualTransactionRequest) =
                async {
                    executeSql "INSERT INTO ledger_entry_links (entry_id, system, value) VALUES ('ay81j-sperre', 'ynab', @value)" ynabTxId
                    return Ok ynabTxId
                }
            let thrown =
                try
                    addCashBooking QuickAdd at entryId push noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
                    false
                with _ -> true
            Expect.isTrue thrown "das Schreiben der Verknüpfung wirft"
            executeSql "DELETE FROM ledger_entry_links WHERE entry_id = 'ay81j-sperre' AND value = @value" ynabTxId
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded" ] "nur die Buchung steht, ohne Verknüpfung"
            Expect.equal
                (getYnabPushState entryId |> Async.RunSynchronously)
                (Some (NotLinkedToYnab PushOutcomeUnknown))
                "Ausgang unbekannt"

            // WHEN
            let summary = mirrorSees (observedCashBooking ynabTxId (ynabIdOf account) None)

            // THEN
            expectLinkedByMirror account entryId ynabTxId summary

        testCase "Intakt: eine YNAB-Buchung mit anderem Payee wird eine neue Buchung, die Erfassung bleibt offen" <| fun () ->
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let _, push = recordingPush (Error (YnabError.NetworkError "Zeitüberschreitung"))
            addCashBooking QuickAdd at entryId push noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
            let summary = mirrorSees { observedCashBooking $"ynab-ay81j-{Guid.NewGuid()}" (ynabIdOf account) None with Payee = Some "Bäcker" }
            Expect.equal summary.NewEntries 1 "eine neue Buchung"
            Expect.hasLength (loadEntriesForAccount account.Id |> Async.RunSynchronously) 2 "zwei Buchungen auf dem Konto"
            Expect.isFalse (loadEntry entryId |> Async.RunSynchronously |> Option.get).LinkedToYnab "die Erfassung bleibt offen"

        testCase "Bestand: eine alte offene Erfassung mit BB:-Verknüpfung wird über die ImportId verknüpft" <| fun () ->
            // Vor ledger-ay81j mit import_id gesendet: der Spiegel findet sie weiter über `MatchedByImportId`.
            let account = mirroredAccount ()
            let entryId = oldCapture account
            let ynabTxId = $"ynab-ay81j-{Guid.NewGuid()}"
            let summary =
                mirrorSees { observedCashBooking ynabTxId (ynabIdOf account) (Some (oldCashImportId entryId)) with Amount = { Amount = -99m; Currency = "EUR" } }
            Expect.equal summary.NewEntries 0 "keine neue Buchung"
            Expect.equal (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously) (Some entryId) "über die ImportId verknüpft"

        testCase "Kaputt: die Barbuchung bekommt 409, die import_id steht schon in YNAB; angekommen, kein Fehlschlag" <| fun () ->
            // ledger-xqqnv: der 409-Zweig bleibt (ledger-ay81j entfernt ihn nicht), auch wenn eine
            // Barbuchung ohne import_id ihn nicht mehr erreicht.
            let entryId = newEntryId ()
            let input, _ = cashBookingParts (booking (freshAccount ()))
            let mutable triggers = 0
            let result =
                addCashExpense QuickAdd at entryId (fun () -> async.Return (Error ImportIdAlreadyInYnab)) (fun () -> triggers <- triggers + 1) input
                |> Async.RunSynchronously
            Expect.equal result (Ok entryId) "Ok mit der Buchungs-Id"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded" ] "nur die Buchung, kein EntryPushToYnabFailed"
            Expect.equal triggers 1 "der Spiegel wird genau einmal angestoßen"

        testCase "Intakt: die Barbuchung mit Netzfehler schreibt weiter EntryPushToYnabFailed, ohne Auslöser" <| fun () ->
            let entryId = newEntryId ()
            let input, _ = cashBookingParts (booking (freshAccount ()))
            let mutable triggers = 0
            let err = YnabError.NetworkError "Zeitüberschreitung"
            let result =
                addCashExpense QuickAdd at entryId (fun () -> async.Return (Error err)) (fun () -> triggers <- triggers + 1) input
                |> Async.RunSynchronously
            Expect.equal result (Error err) "der Fehler wird durchgereicht"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "Fehlschlag steht"
            Expect.equal triggers 0 "kein Auslöser"

        testCase "Intakt: Erneut senden mit einem anderen Fehler schreibt weiter EntryPushToYnabFailed" <| fun () ->
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let _, firstPush = recordingPush (Error (YnabError.NetworkError "Zeitüberschreitung"))
            addCashBooking QuickAdd at entryId firstPush noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
            let create _ _ _ _ _ _ _ = async.Return (Server.YnabClient.interpretCreateTransactionResponse 400 "{}")
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            match retryPushToYnab (at.AddHours 1.0) loadEntryOrigin mirrorFindsNothing push noTrigger entryId |> Async.RunSynchronously with
            | Ok (PushToYnabFailed _) -> ()
            | other -> failtest $"Fehlschlag erwartet, war: {other}"
            Expect.equal
                (eventTypesOf entryId)
                [ "CashExpenseAdded"; "EntryPushToYnabFailed"; "EntryPushToYnabFailed" ]
                "der neue Fehlschlag steht"
    ]

// ============================================
// Buchungen nicht in YNAB (ledger-s1weh): die Menge hinter „Zu tun“ der Übersicht
// ============================================

[<Tests>]
let entriesNotInYnabTests =
    let account = freshAccount ()
    let failure (minute: int) : LedgerPushFailure option =
        Some { Reason = "YNAB antwortet nicht"; FailedAt = at.AddMinutes(float minute) }
    let idOf (e: Entry) = let (EntryId id) = e.Id in id
    testList "Buchungen nicht in YNAB" [
        testCase "Nur nicht verknüpfte, nicht gelöschte Buchungen zählen, mit und ohne Fehlschlag" <| fun () ->
            let linked = { entry account -1m with LinkedToYnab = true }
            let deleted = { entry account -2m with DeletedInYnab = true; PushToYnabFailure = failure 0 }
            let failed = { entry account -3m with PushToYnabFailure = failure 0 }
            let unknown = entry account -4m
            let ids = entriesNotInYnab [ linked; deleted; failed; unknown ] |> List.map (fun e -> e.EntryId)
            Expect.equal ids [ idOf failed; idOf unknown ] "verknüpfte und gelöschte fehlen"

        testCase "Erst die Fehlschläge, der neueste zuerst, dann die ohne Ausgang nach Datum" <| fun () ->
            let older = { entry account -1m with PushToYnabFailure = failure 1 }
            let newer = { entry account -2m with PushToYnabFailure = failure 5 }
            let earlyDay = { entry account -3m with Date = day.AddDays -3.0 }
            let lateDay = { entry account -4m with Date = day }
            let ids = entriesNotInYnab [ earlyDay; older; lateDay; newer ] |> List.map (fun e -> e.EntryId)
            Expect.equal ids [ idOf newer; idOf older; idOf lateDay; idOf earlyDay ] "Reihenfolge"

        // Review-Fund: das Buchungsdatum ist ein Tag und das SQL sortiert nicht. Zwei Buchungen am selben Tag (oder
        // mit demselben Fehlschlag-Zeitpunkt) standen in der Reihenfolge der Datenbank, „Zu tun“ sprang beim Neuladen.
        testCase "Bei gleichem Tag oder gleichem Fehlschlag entscheidet die EntryId, die größere (später angelegte) zuerst" <| fun () ->
            // Feste Ids: zwei UUIDv7 aus derselben Millisekunde sind untereinander nicht geordnet.
            let withId (n: int) (e: Entry) = { e with Id = EntryId (Guid.Parse $"0195c000-0000-7000-8000-00000000d0{n:D2}") }
            let first = withId 1 (entry account -1m)
            let second = withId 2 (entry account -2m)
            for input in [ [ first; second ]; [ second; first ] ] do
                let ids = entriesNotInYnab input |> List.map (fun e -> e.EntryId)
                Expect.equal ids [ idOf second; idOf first ] "gleicher Tag"
            let failedFirst = withId 3 { entry account -3m with PushToYnabFailure = failure 2 }
            let failedSecond = withId 4 { entry account -4m with PushToYnabFailure = failure 2 }
            for input in [ [ failedFirst; failedSecond ]; [ failedSecond; failedFirst ] ] do
                let ids = entriesNotInYnab input |> List.map (fun e -> e.EntryId)
                Expect.equal ids [ idOf failedSecond; idOf failedFirst ] "gleicher Fehlschlag"

        testCase "Payee, Betrag, Konto und Fehlschlag gehen mit" <| fun () ->
            let failed = { entry account -8.60m with PushToYnabFailure = failure 0 }
            match entriesNotInYnab [ failed ] with
            | [ shown ] ->
                let (LedgerAccountId accountGuid) = account
                Expect.equal shown.Payee (Some "Kiosk") "Payee"
                Expect.equal shown.Amount.Amount -8.60m "Betrag"
                Expect.equal shown.AccountId accountGuid "Konto"
                Expect.equal shown.Reason (PushFailed (failure 0).Value) "Grund"
            | other -> failtestf "eine Zeile erwartet, bekam %A" other

        testCase "Ohne Fehlschlag ist der Ausgang des Push unbekannt" <| fun () ->
            match entriesNotInYnab [ entry account -1m ] with
            | [ shown ] -> Expect.equal shown.Reason PushOutcomeUnknown "Ausgang unbekannt"
            | other -> failtestf "eine Zeile erwartet, bekam %A" other
    ]

/// Eine in BB erfasste Barbuchung auf `account`, danach die Ereignisse aus `after` (verknüpft, gelöscht, Fehlschlag).
let private seedCashEntry (account: LedgerAccount) (after: LedgerEventBody list) : Guid =
    let entryId = newEntryId ()
    let event body = { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = body }
    let added =
        CashExpenseAdded {
            AccountId = account.Id
            Date = day
            Amount = { Amount = -1m; Currency = "EUR" }
            Payee = Some "Kiosk s1weh"
            Memo = None
            Category = None
            Cleared = Uncleared
            Approved = false
            ImportId = None
        }
    appendAndApply (event added :: List.map event after) |> Async.RunSynchronously
    let (EntryId id) = entryId
    id

// QA-Fund: das SQL hinter `getEntriesNotInYnab` (`linked_to_ynab = 0 AND deleted_in_ynab = 0`) war ungetestet.
[<Tests>]
let entriesNotInYnabQueryTests =
    testList "Buchungen nicht in YNAB aus der Datenbank" [
        testCase "Die Abfrage liefert offene Buchungen, keine verknüpften und keine in YNAB gelöschten" <| fun () ->
            initialize ()
            let account = mirroredAccount ()
            let linked = seedCashEntry account [ EntryLinkedToYnab { YnabTransactionId = $"ynab-s1weh-{Guid.NewGuid()}" } ]
            let deleted = seedCashEntry account [ EntryDeletionObservedInYnab ]
            let failed = seedCashEntry account [ EntryPushToYnabFailed { Reason = "YNAB antwortet nicht" } ]
            let unknown = seedCashEntry account []
            let (LedgerAccountId accountGuid) = account.Id
            // Die Datenbank ist zwischen den Tests geteilt: nur dieses frische Konto zählt. `entriesNotInYnab` filtert
            // danach noch einmal rein, deshalb steht das SQL hier einzeln: ohne eine seiner Bedingungen wird es rot.
            let loaded =
                loadEntriesNotLinkedToYnab ()
                |> Async.RunSynchronously
                |> List.filter (fun e -> e.AccountId = account.Id)
                |> List.map (fun e -> let (EntryId id) = e.Id in id)
            Expect.equal (Set.ofList loaded) (Set.ofList [ failed; unknown ]) "das SQL lässt verknüpfte und gelöschte weg"
            let shown =
                getEntriesNotInYnab ()
                |> Async.RunSynchronously
                |> List.filter (fun e -> e.AccountId = accountGuid)
            Expect.equal (shown |> List.map (fun e -> e.EntryId)) [ failed; unknown ] "ohne verknüpfte und gelöschte"
            Expect.isFalse (shown |> List.exists (fun e -> e.EntryId = linked || e.EntryId = deleted)) "beide fehlen"
            match shown with
            | [ f; u ] ->
                Expect.equal (match f.Reason with PushFailed p -> Some p.Reason | PushOutcomeUnknown -> None) (Some "YNAB antwortet nicht") "Grund"
                Expect.equal u.Reason PushOutcomeUnknown "Ausgang unbekannt"
            | other -> failtestf "zwei Zeilen erwartet, bekam %A" other
    ]
