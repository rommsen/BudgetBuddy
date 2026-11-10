module LedgerCashBookingTests

// Barbuchung für Quick Add und Bob (ledger-bk3q8): die reine Push-Funktion einer
// Kassenbuch-Buchung (`planEntryPush`), der gemeinsame Befehl (`cashBookingParts`,
// `addCashBooking`), Quick Add mit Kategorie-Auflösung (`addQuickAdd`), die Lesefunktionen
// für Quick-Add-Konto und Kategorie und die Adapter mit Ports in `Server.Api`. Kein Fall
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
    let push (_importId: string) (request: ManualTransactionRequest) =
        requests.Add request
        async.Return result
    requests, push

/// Das `import_id` im JSON-Rumpf des Einzel-Posts, `None` ohne den Schlüssel.
let private importIdInBody (body: string) : string option =
    match Thoth.Json.Net.Decode.fromString (Thoth.Json.Net.Decode.at [ "transaction" ] (Thoth.Json.Net.Decode.optional "import_id" Thoth.Json.Net.Decode.string)) body with
    | Ok importId -> importId
    | Error err -> failtest $"Rumpf nicht lesbar: {err}"

/// `BB:` und die EntryId ohne Bindestriche (ADR 0004 Nachtrag), unabhängig von
/// `cashEntryImportId` ausgeschrieben.
let private expectedImportId (EntryId id) = "BB:" + id.ToString().Replace("-", "")

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
    LinkedToYnab = false
    PushToYnabFailure = None
    PayeeBeforeTransfer = None
}

let private linkedAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc01")
let private linkedYnabId = YnabAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc02")
let private ownAccountId = LedgerAccountId (Guid.Parse "0195c000-0000-7000-8000-00000000bc03")

let private accounts : LedgerAccount list = [
    { Id = linkedAccountId; Name = "Bar"; Kind = CashAccount; Link = YnabMirrored linkedYnabId; Closed = false }
    { Id = ownAccountId; Name = "Eigen"; Kind = CashAccount; Link = BbOwned; Closed = false }
]

let private planOk (links: EntryLink list) (e: Entry) : EntryPushPlan =
    match planEntryPush accounts links [] e with
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
            match planEntryPush accounts [] events e with
            | Ok plan -> Expect.equal plan.TransferPayeeId (Some "payee-neu") "Payee des letzten Setzens auf dieses Ziel"
            | Error reason -> failtest reason
            Expect.equal
                (planEntryPush accounts [] [ setTo other "payee-anderes" ] e)
                (Error transferPayeeUnknownReason)
                "ohne Setzen auf dieses Ziel kein Push"
            let withCategory = { e with Category = Some (freshCategory "Sparen") }
            Expect.isNone (planOk [] withCategory).TransferPayeeId "mit Kategorie keine Übertrags-Payee"

        testCase "ein Konto ohne YNAB-Verknüpfung ergibt keinen Request, sondern den Grund" <| fun () ->
            Expect.equal (planEntryPush accounts [] [] (entry ownAccountId -1m)) (Error pushNotPossibleReason) "BB-eigenes Konto"
            Expect.equal (planEntryPush accounts [] [] (entry (freshAccount ()) -1m)) (Error pushNotPossibleReason) "unbekanntes Konto"
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
// Quick Add: Kategorie vor dem Ereignis auflösen (Bug: Kategorie ging still verloren)
// ============================================

let private quickAddRequest (categoryId: YnabCategoryId option) : ManualTransactionRequest = {
    Amount = 12.50m
    IsOutflow = true
    PayeeName = $"bk3q8-{Guid.NewGuid()}"
    CategoryId = categoryId
    Date = day
    Memo = None
}

[<Tests>]
let quickAddTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testSequenced
    <| testList "Ledger Quick Add (Kategorie)" [
        testCase "scheitert das Laden der Kategorie, bucht Quick Add nicht und pusht nicht" <| fun () ->
            // ledger-bk3q8, Bug: scheiterte der Abruf der Kategorien (etwa Rate-Limit), stand
            // die Buchung ohne Kategorie im Kassenbuch, an YNAB ging sie mit der Id.
            let entryId = newEntryId ()
            let requests, push = recordingPush (Ok "ynab-nie")
            let resolve _ = async.Return (Error categoryLoadFailedText)
            let result =
                addQuickAdd at entryId resolve push noTrigger (freshAccount ()) (quickAddRequest (Some (freshCategory "x").CategoryId))
                |> Async.RunSynchronously
            Expect.equal result (Error (YnabError.RefusedByBb categoryLoadFailedText)) "Abbruch mit deutschem Satz"
            Expect.isEmpty (eventTypesOf entryId) "kein CashExpenseAdded"
            Expect.isEmpty requests "kein Push"

        testCase "eine Kategorie-Id, die keine Quelle kennt, wird ohne Ereignis und ohne Push abgelehnt" <| fun () ->
            // ledger-bk3q8, Bug: eine unbekannte Id wurde zu "ohne Kategorie", gepusht wurde die Id.
            let entryId = newEntryId ()
            let requests, push = recordingPush (Ok "ynab-nie")
            let sources = CategorySources(Map.empty, Ok [])
            let result =
                addQuickAdd at entryId sources.Resolve push noTrigger (freshAccount ()) (quickAddRequest (Some (freshCategory "x").CategoryId))
                |> Async.RunSynchronously
            Expect.equal result (Error (YnabError.RefusedByBb categoryUnknownText)) "abgelehnt"
            Expect.isEmpty (eventTypesOf entryId) "kein Ereignis"
            Expect.isEmpty requests "kein Push"

        testCase "mit gültiger Kategorie tragen Kassenbuch und Push dieselbe Kategorie" <| fun () ->
            let entryId = newEntryId ()
            let category = freshCategory "Kaffee"
            let requests, push = recordingPush (Ok $"ynab-{Guid.NewGuid()}")
            let resolve id = async.Return (if id = category.CategoryId then Ok category else Error categoryUnknownText)
            let result =
                addQuickAdd at entryId resolve push noTrigger (freshAccount ()) (quickAddRequest (Some category.CategoryId))
                |> Async.RunSynchronously
            Expect.equal result (Ok entryId) "gebucht"
            let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.equal stored.Category (Some category) "Id und Name im Kassenbuch"
            Expect.equal stored.Amount.Amount -12.50m "Ausgabe negativ im Kassenbuch"
            Expect.equal (requests |> Seq.map (fun r -> r.CategoryId, r.Amount, r.IsOutflow) |> List.ofSeq) [ Some category.CategoryId, 12.50m, true ] "dieselbe Id, positiv als Abfluss"
            Expect.equal (eventsOf entryId |> List.map (fun r -> r.Event.Actor)) [ QuickAdd; QuickAdd ] "Actor Quick Add"

        testCase "ohne Kategorie stehen Kassenbuch und Push ohne Kategorie, eine Einnahme positiv" <| fun () ->
            let entryId = newEntryId ()
            let requests, push = recordingPush (Ok $"ynab-{Guid.NewGuid()}")
            let resolve _ = async.Return (Error "darf nicht gefragt werden")
            let request = { quickAddRequest None with IsOutflow = false }
            addQuickAdd at entryId resolve push noTrigger (freshAccount ()) request |> Async.RunSynchronously |> ignore
            let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isNone stored.Category "ohne Kategorie im Kassenbuch"
            Expect.equal stored.Amount.Amount 12.50m "Einnahme positiv"
            Expect.equal (requests |> Seq.map (fun r -> r.CategoryId, r.IsOutflow) |> List.ofSeq) [ None, false ] "ohne Kategorie als Zufluss"
    ]

// ============================================
// Adapter mit Ports (`Server.Api`)
// ============================================

/// Quick-Add-Umgebung mit gefälschtem YNAB, die ihre Aufrufe zählt.
type private QuickAddProbe(settings: (string * string) list) =
    let pushes = ResizeArray<YnabAccountId * ManualTransactionRequest>()
    let bodies = ResizeArray<string>()
    let mutable categoryLoads = 0
    let entryId = newEntryId ()
    member _.EntryId = entryId
    member _.Pushes = pushes
    /// Der JSON-Rumpf je Push, gebaut wie im Live-Push (`createTransactionWith`).
    member _.Bodies = bodies
    member _.CategoryLoads = categoryLoads
    member _.Ports : Server.Api.QuickAddPorts = {
        Setting = settingsFrom settings
        LoadYnabCategories =
            fun _ _ ->
                categoryLoads <- categoryLoads + 1
                async.Return (Ok [])
        Push =
            fun importId _ _ account request ->
                pushes.Add((account, request))
                bodies.Add(Server.YnabClient.buildManualTransactionBodyWith importId account request)
                async.Return (Ok $"ynab-{Guid.NewGuid()}")
        NewEntryId = fun () -> entryId
        TriggerMirror = noTrigger
    }

let private fakeYnab = [ "ynab_token", "token-kuenstlich"; "ynab_default_budget_id", "budget-kuenstlich" ]

[<Tests>]
let adapterTests =
    let testCase name body = testCase name (fun () -> initialize (); body ())

    testSequenced
    <| testList "Ledger Barbuchung (Adapter mit Ports)" [
        testCase "Quick Add ohne gespiegeltes Kassenbuch-Konto: Fehler, kein Ereignis, kein Push" <| fun () ->
            let probe = QuickAddProbe(fakeYnab @ [ quickAddAccountSettingKey, Guid.NewGuid().ToString() ])
            let result = Server.Api.addManualTransactionWith probe.Ports at (quickAddRequest None) |> Async.RunSynchronously
            // Verhindert den Bug, dass der feste Satz als `AccountNotFound` verpackt war und der Nutzer
            // nur "Konto in YNAB nicht gefunden" sah (Review rv/backend).
            Expect.equal (result |> Result.mapError ynabErrorText) (Error quickAddAccountNotMirroredText) "fester Satz beim Nutzer"
            Expect.isEmpty (eventTypesOf probe.EntryId) "kein Ereignis"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "Quick Add ohne Standard-Budget: der Nutzer liest den Satz zur fehlenden Einstellung" <| fun () ->
            // Verhindert den Bug, dass der Satz als `BudgetNotFound` verpackt war und als
            // "Budget in YNAB nicht gefunden" ankam (Review rv/backend).
            let probe = QuickAddProbe([ "ynab_token", "token-kuenstlich"; quickAddAccountSettingKey, Guid.NewGuid().ToString() ])
            let result = Server.Api.addManualTransactionWith probe.Ports at (quickAddRequest None) |> Async.RunSynchronously
            Expect.equal (result |> Result.mapError ynabErrorText) (Error "Kein Standard-Budget konfiguriert (Settings)") "Satz beim Nutzer"
            Expect.isEmpty probe.Pushes "kein Push"

        testCase "Quick Add nimmt den Kategorienamen aus den gespiegelten Stammdaten und pusht auf das Quick-Add-Konto" <| fun () ->
            let account = mirroredAccount ()
            let (YnabAccountId guid) = ynabIdOf account
            let category = freshCategory $"Kaffee-{Guid.NewGuid()}"
            upsertCategories [ ynabCategory category ] |> Async.RunSynchronously
            let probe = QuickAddProbe(fakeYnab @ [ quickAddAccountSettingKey, guid.ToString() ])
            let result = Server.Api.addManualTransactionWith probe.Ports at (quickAddRequest (Some category.CategoryId)) |> Async.RunSynchronously
            Expect.equal result (Ok ()) "gebucht"
            let stored = loadEntry probe.EntryId |> Async.RunSynchronously |> Option.get
            Expect.equal stored.Category (Some category) "Name aus den Stammdaten"
            Expect.equal stored.AccountId account.Id "Kassenbuch-Konto des Quick-Add-Kontos"
            Expect.equal probe.CategoryLoads 0 "YNAB-Kategorien nicht geladen"
            Expect.equal (probe.Pushes |> Seq.map (fun (a, r) -> a, r.CategoryId) |> List.ofSeq) [ ynabIdOf account, Some category.CategoryId ] "Push auf das Quick-Add-Konto"

        testCase "Quick Add pusht mit der festen import_id aus BB: und der EntryId (ADR 0004 Nachtrag)" <| fun () ->
            // ledger-p3vq8: ohne import_id fand der Spiegel eine Buchung nicht, deren Push-Antwort
            // verloren ging, und legte eine zweite an.
            let account = mirroredAccount ()
            let (YnabAccountId guid) = ynabIdOf account
            let probe = QuickAddProbe(fakeYnab @ [ quickAddAccountSettingKey, guid.ToString() ])
            let result = Server.Api.addManualTransactionWith probe.Ports at (quickAddRequest None) |> Async.RunSynchronously
            Expect.equal result (Ok ()) "gebucht"
            let importId = expectedImportId probe.EntryId
            Expect.equal (probe.Bodies |> Seq.map importIdInBody |> List.ofSeq) [ Some importId ] "import_id im JSON-Rumpf"
            Expect.equal importId.Length 35 "35 Zeichen, YNAB erlaubt 36"
            Expect.equal (findEntryByLink ImportIdSystem importId |> Async.RunSynchronously) (Some probe.EntryId) "dieselbe import_id steht an der Buchung"

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
            let expected = (planEntryPush [ account ] [] [] e) |> Result.map (fun p -> p.ImportId, p.FlagColor, p.YnabAccountId, p.Request)
            Expect.equal (calls |> List.ofSeq |> List.map Ok) [ expected ] "genau der geplante Push"
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
            let pushes = ResizeArray<YnabAccountId * string * ManualTransactionRequest>()
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
                        async.Return (Ok $"ynab-{Guid.NewGuid()}")
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
                (pushes |> Seq.map (fun (account, _, r) -> account, r.Amount, r.IsOutflow, r.CategoryId, r.PayeeName) |> List.ofSeq)
                [ ynabAccount, 5.25m, true, Some category.Id, payee ]
                "ein Push auf das YNAB-Konto der Vorschau, mit dem Request der Barbuchung"
            // ledger-p3vq8: Bob pusht mit der festen import_id, der JSON-Rumpf trägt sie.
            Expect.equal
                (pushes
                 |> Seq.map (fun (account, importId, r) -> importIdInBody (Server.YnabClient.buildManualTransactionBodyWith (Some importId) account r))
                 |> List.ofSeq)
                [ Some (expectedImportId entryId) ]
                "import_id aus BB: und der EntryId"
    ]

// ============================================
// Feste import_id: verlorene Push-Antwort ohne zweite Buchung (ledger-p3vq8, ADR 0004 Nachtrag)
// ============================================

/// Die YNAB-Buchung, die der Spiegel zu einer Barbuchung sieht: dieselben Werte wie im
/// Kassenbuch, mit der `import_id`, die der Push trug.
let private observedCashBooking (ynabTxId: string) (ynabAccount: YnabAccountId) (importId: string) : Server.YnabObservations.YnabObservation = {
    YnabTransactionId = ynabTxId
    AccountId = ynabAccount
    ImportId = Some importId
    Date = day
    Amount = { Amount = -12.50m; Currency = "EUR" }
    Payee = Some "Kiosk"
    Memo = Some "Kaffee am Bahnhof"
    Category = None
    Splits = []
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

    testSequenced
    <| testList "Ledger Barbuchung (feste import_id, ledger-p3vq8)" [
        testCase "Kaputt: Push gelingt, das Schreiben von EntryLinkedToYnab wirft; der Spiegel verknüpft über die import_id" <| fun () ->
            // GIVEN: YNAB legt die Buchung an, danach scheitert das Schreiben der Verknüpfung
            // (eine fremde Zeile belegt die YNAB-Id). Bisher legte der Spiegel dann eine zweite
            // Kassenbuch-Buchung an, weil Quick Add und Bob keine import_id trugen.
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let ynabTxId = $"ynab-p3vq8-{Guid.NewGuid()}"
            let sentImportIds = ResizeArray<string>()
            let push (importId: string) (_: ManualTransactionRequest) =
                async {
                    sentImportIds.Add importId
                    executeSql "INSERT INTO ledger_entry_links (entry_id, system, value) VALUES ('p3vq8-sperre', 'ynab', @value)" ynabTxId
                    return Ok ynabTxId
                }
            let thrown =
                try
                    addCashBooking QuickAdd at entryId push noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
                    false
                with _ -> true
            Expect.isTrue thrown "das Schreiben der Verknüpfung wirft"
            executeSql "DELETE FROM ledger_entry_links WHERE entry_id = 'p3vq8-sperre' AND value = @value" ynabTxId
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded" ] "nur die Buchung steht, ohne Verknüpfung"

            // WHEN: der Spiegel sieht die YNAB-Buchung mit der import_id des Pushs.
            let summary = mirrorSees (observedCashBooking ynabTxId (ynabIdOf account) sentImportIds[0])

            // THEN: keine zweite Buchung, die vorhandene ist verknüpft (Actor YnabMirror).
            Expect.equal sentImportIds[0] (expectedImportId entryId) "der Push trug die feste import_id"
            Expect.equal summary.NewEntries 0 "keine neue Buchung"
            Expect.hasLength (loadEntriesForAccount account.Id |> Async.RunSynchronously) 1 "genau eine Buchung auf dem Konto"
            Expect.equal (findEntryByLink YnabSystem ynabTxId |> Async.RunSynchronously) (Some entryId) "die YNAB-Id zeigt auf die Barbuchung"
            let last = eventsOf entryId |> List.last
            Expect.equal (last.EventType, last.Event.Actor) ("EntryLinkedToYnab", YnabMirror) "der Spiegel verknüpft"

        testCase "Kaputt: Push legt an, meldet aber Zeitüberschreitung; Erneut senden mit derselben import_id, 409 ist kein Fehlschlag" <| fun () ->
            // GIVEN: YNAB hat die Buchung angelegt, BB sah nur die Zeitüberschreitung und schrieb
            // `EntryPushToYnabFailed`. Bisher ging "Erneut senden" ohne import_id hinaus und
            // legte in YNAB eine zweite Buchung an.
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let ynabTxId = $"ynab-p3vq8-{Guid.NewGuid()}"
            let sentImportIds = ResizeArray<string option>()
            let firstPush (importId: string) (_: ManualTransactionRequest) =
                async {
                    sentImportIds.Add (Some importId)
                    return Error (YnabError.NetworkError "Zeitüberschreitung")
                }
            addCashBooking QuickAdd at entryId firstPush noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "Fehlschlag steht"

            // WHEN: "Erneut senden"; YNAB kennt die import_id schon und antwortet 409
            // (Probelauf ADR 0058, Fall 3).
            let create importId _ _ _ _ _ _ =
                async {
                    sentImportIds.Add importId
                    return
                        Server.YnabClient.interpretCreateTransactionResponse
                            409
                            """{"error":{"id":"409","name":"conflict","detail":"a transaction with the same import_id already exists on the specified account"}}"""
                }
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            let mutable triggers = 0
            let outcome = retryPushToYnab (at.AddHours 1.0) push (fun () -> triggers <- triggers + 1) entryId |> Async.RunSynchronously

            // THEN: dieselbe import_id ging hinaus, kein neuer Fehlschlag, der Spiegel ist angestoßen.
            Expect.equal (List.ofSeq sentImportIds) [ Some (expectedImportId entryId); Some (expectedImportId entryId) ] "zweimal dieselbe import_id"
            Expect.equal outcome (Ok PushToYnabSent) "kein Fehlschlag"
            Expect.equal (eventTypesOf entryId) [ "CashExpenseAdded"; "EntryPushToYnabFailed" ] "kein neues EntryPushToYnabFailed"
            Expect.equal triggers 1 "der Spiegel wird angestoßen"

            // AND: nach dem Spiegel ist die Buchung verknüpft, ohne zweite Buchung.
            let summary = mirrorSees (observedCashBooking ynabTxId (ynabIdOf account) (expectedImportId entryId))
            Expect.equal summary.NewEntries 0 "keine neue Buchung"
            Expect.hasLength (loadEntriesForAccount account.Id |> Async.RunSynchronously) 1 "genau eine Buchung auf dem Konto"
            let stored = loadEntry entryId |> Async.RunSynchronously |> Option.get
            Expect.isTrue stored.LinkedToYnab "verknüpft"
            Expect.isNone stored.PushToYnabFailure "der Fehlschlag ist aufgelöst"
            let last = eventsOf entryId |> List.last
            Expect.equal (last.EventType, last.Event.Actor) ("EntryLinkedToYnab", YnabMirror) "der Spiegel verknüpft"

        testCase "Intakt: Erneut senden einer Buchung, deren CashExpenseAdded keine import_id trägt, schickt keine" <| fun () ->
            // Vor ledger-p3vq8 gepusht: ob sie schon in YNAB steht, weiß BB nicht (ADR 0004 Nachtrag).
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let input, _ = cashBookingParts (booking account.Id)
            let payload : CashExpenseAddedPayload = {
                AccountId = input.AccountId
                Date = input.Date
                Amount = input.Amount
                Payee = input.Payee
                Memo = input.Memo
                Category = input.Category
                Cleared = Uncleared
                Approved = false
                ImportId = None
            }
            appendAndApply [
                { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = CashExpenseAdded payload }
                { EntryId = entryId; OccurredAt = at; Actor = QuickAdd; Body = EntryPushToYnabFailed { Reason = "Zeitüberschreitung" } }
            ]
            |> Async.RunSynchronously
            let sent = ResizeArray<string option>()
            let create importId _ _ _ _ _ _ =
                sent.Add importId
                async.Return (Ok $"ynab-{Guid.NewGuid()}")
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            let outcome = retryPushToYnab (at.AddHours 1.0) push noTrigger entryId |> Async.RunSynchronously
            Expect.equal outcome (Ok PushToYnabSent) "gesendet"
            Expect.equal (List.ofSeq sent) [ None ] "ohne import_id"

        testCase "Intakt: Erneut senden mit einem anderen Fehler schreibt weiter EntryPushToYnabFailed" <| fun () ->
            let account = mirroredAccount ()
            let entryId = newEntryId ()
            let _, firstPush = recordingPush (Error (YnabError.NetworkError "Zeitüberschreitung"))
            addCashBooking QuickAdd at entryId firstPush noTrigger (booking account.Id) |> Async.RunSynchronously |> ignore
            let create _ _ _ _ _ _ _ = async.Return (Server.YnabClient.interpretCreateTransactionResponse 400 "{}")
            let push = Server.Api.entryPushWith (settingsFrom fakeYnab) create |> Async.RunSynchronously
            match retryPushToYnab (at.AddHours 1.0) push noTrigger entryId |> Async.RunSynchronously with
            | Ok (PushToYnabFailed _) -> ()
            | other -> failtest $"Fehlschlag erwartet, war: {other}"
            Expect.equal
                (eventTypesOf entryId)
                [ "CashExpenseAdded"; "EntryPushToYnabFailed"; "EntryPushToYnabFailed" ]
                "der neue Fehlschlag steht"
    ]
