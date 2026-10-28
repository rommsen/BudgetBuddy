/// Bot-Schnittstelle unter `/bot/` (ledger-b0b7q, ADR 0066): Vorschau, Commit und
/// Kategorieliste für Bob. Eigene Giraffe-Handler vor dem SPA-Fallback, getrennt von
/// Fable.Remoting. Die Umgebung (Token, Obergrenze, Uhr, YNAB, Quick-Add-Konto) kommt als
/// `BotDeps` von außen, damit die Routen ohne Netz und mit Test-Datenbank laufen.
module Server.BotApi

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Giraffe
open Thoth.Json.Net
open Shared.Domain
open Server.Validation

/// Wie lange eine Vorschau gültig ist (ADR 0066, Punkt 3).
let previewTtl = TimeSpan.FromMinutes 15.0

/// Wie lange abgelaufene Vorschauen im Speicher bleiben, damit ein später Commit 410
/// statt 404 bekommt.
let private retainExpired = TimeSpan.FromHours 1.0

/// Das Konto, auf das Bob bucht: immer das Quick-Add-Konto (ADR 0004).
type BotTarget = {
    LedgerAccountId: Ledger.Events.LedgerAccountId
    YnabAccountId: YnabAccountId
    AccountName: string
}

/// Eine aufgelöste, noch nicht gebuchte Buchung. `AmountEur` ist positiv, die Richtung
/// steht in `Direction`.
type BotPreview = {
    Id: Guid
    CreatedAt: DateTime
    ExpiresAt: DateTime
    Target: BotTarget
    AmountEur: decimal
    Direction: BotDirection
    Payee: string
    Category: YnabCategory
    Date: DateTime
    Memo: string option
}

/// Das Ergebnis eines Commits. `EntryId` fehlt, wenn der Push scheiterte (die Buchung steht
/// dann trotzdem im Kassenbuch, `addCashExpense` reicht dort nur den Fehler durch).
type BotCommitResult = {
    EntryId: Guid option
    PushedToYnab: bool
    PushError: string option
}

/// Vorschau-Speicher im Arbeitsspeicher. Nach einem Neustart verfallen alle Vorschauen
/// (ADR 0066). Der Commit läuft je Vorschau höchstens einmal: gleichzeitige Aufrufe teilen
/// sich dieselbe Aufgabe.
type BotPreviewStore() =
    let previews = ConcurrentDictionary<Guid, BotPreview>()
    let commits = ConcurrentDictionary<Guid, Lazy<Task<BotCommitResult>>>()

    member _.Add(preview: BotPreview) = previews[preview.Id] <- preview

    member _.TryGet(id: Guid) : BotPreview option =
        match previews.TryGetValue id with
        | true, p -> Some p
        | _ -> None

    member _.IsCommitted(id: Guid) = commits.ContainsKey id

    /// Startet den Commit genau einmal je Vorschau; jeder weitere Aufruf bekommt dieselbe
    /// Aufgabe und damit dasselbe Ergebnis.
    member _.CommitOnce(id: Guid, work: unit -> Async<BotCommitResult>) : Task<BotCommitResult> =
        let lazyTask = commits.GetOrAdd(id, fun _ -> lazy (Async.StartAsTask(work ())))
        lazyTask.Value

    /// Wirft Vorschauen weg, die länger als `retainExpired` abgelaufen sind.
    member _.Purge(now: DateTime) =
        for KeyValue(id, p) in previews do
            if p.ExpiresAt + retainExpired < now then
                previews.TryRemove id |> ignore
                commits.TryRemove id |> ignore

/// Alles, was von außen kommt.
type BotDeps = {
    Token: unit -> string option
    MaxAmountEur: unit -> decimal
    Now: unit -> DateTime
    LoadCategories: unit -> Async<Result<YnabCategory list, string>>
    ResolveTarget: unit -> Async<Result<BotTarget, string>>
    Push: BotPreview -> Async<YnabResult<string>>
    TriggerMirror: unit -> unit
    Store: BotPreviewStore
}

// ============================================
// Antworten
// ============================================

let private respond (status: int) (json: JsonValue) : HttpHandler =
    fun _ ctx ->
        task {
            ctx.Response.StatusCode <- status
            ctx.Response.ContentType <- "application/json; charset=utf-8"
            do! ctx.Response.WriteAsync(Encode.toString 0 json)
            return Some ctx
        }

let private errorBody (code: string) (message: string) (extra: (string * JsonValue) list) : JsonValue =
    Encode.object ([ "error", Encode.string code; "message", Encode.string message ] @ extra)

let private categoryJson (c: YnabCategory) : JsonValue =
    let (YnabCategoryId id) = c.Id
    Encode.object [
        "id", Encode.string (id.ToString())
        "name", Encode.string c.Name
        "group", Encode.string c.GroupName
    ]

let private directionText (d: BotDirection) = match d with BotOutflow -> "outflow" | BotInflow -> "inflow"

let private isoDate (d: DateTime) = d.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)

let private isoInstant (d: DateTime) =
    d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Globalization.CultureInfo.InvariantCulture)

let private previewJson (p: BotPreview) : JsonValue =
    Encode.object [
        "preview_id", Encode.string (p.Id.ToString())
        "expires_at", Encode.string (isoInstant p.ExpiresAt)
        "account", Encode.string p.Target.AccountName
        "amount", Encode.float (float p.AmountEur)
        "currency", Encode.string "EUR"
        "direction", Encode.string (directionText p.Direction)
        "payee", Encode.string p.Payee
        "category", categoryJson p.Category
        "date", Encode.string (isoDate p.Date)
        "memo", (match p.Memo with Some m -> Encode.string m | None -> Encode.nil)
    ]

let private commitJson (previewId: Guid) (r: BotCommitResult) : JsonValue =
    Encode.object [
        "preview_id", Encode.string (previewId.ToString())
        "status", Encode.string "booked"
        "entry_id", (match r.EntryId with Some id -> Encode.string (id.ToString()) | None -> Encode.nil)
        "pushed_to_ynab", Encode.bool r.PushedToYnab
        "push_error", (match r.PushError with Some e -> Encode.string e | None -> Encode.nil)
    ]

// ============================================
// Anmeldung
// ============================================

/// Vergleicht in konstanter Zeit, damit die Antwortzeit das Token nicht verrät.
let private tokenMatches (expected: string) (header: string) : bool =
    let prefix = "Bearer "
    if isNull header || not (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) then false
    else
        let given = header.Substring(prefix.Length).Trim()
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            ReadOnlySpan<byte>(Text.Encoding.UTF8.GetBytes expected),
            ReadOnlySpan<byte>(Text.Encoding.UTF8.GetBytes given)
        )

// ============================================
// Handler
// ============================================

let private readBody (ctx: HttpContext) : Task<string> =
    task {
        use reader = new IO.StreamReader(ctx.Request.Body, Text.Encoding.UTF8)
        return! reader.ReadToEndAsync()
    }

let private previewHandler (deps: BotDeps) : HttpHandler =
    fun _ ctx ->
        task {
            let now = deps.Now()
            let! body = readBody ctx
            match parseBotPreviewRequest now body with
            | Error errors ->
                let fields =
                    errors
                    |> List.map (fun e ->
                        Encode.object [ "field", Encode.string e.BotField; "message", Encode.string e.BotProblem ])
                return!
                    respond 400 (errorBody "invalid_request" "Die Anfrage ist ungültig" [ "fields", Encode.list fields ]) earlyReturn ctx
            | Ok input ->
                match checkBotAmount (deps.MaxAmountEur()) input.AmountEur with
                | Error reason ->
                    return! respond 422 (errorBody "amount_not_allowed" reason []) earlyReturn ctx
                | Ok () ->
                    let! target = deps.ResolveTarget() |> Async.StartAsTask
                    match target with
                    | Error reason ->
                        return! respond 422 (errorBody "no_quick_add_account" reason []) earlyReturn ctx
                    | Ok target ->
                        let! categories = deps.LoadCategories() |> Async.StartAsTask
                        match categories with
                        | Error reason ->
                            return! respond 502 (errorBody "ynab_unavailable" reason []) earlyReturn ctx
                        | Ok categories ->
                            let unresolved code message (candidates: YnabCategory list) =
                                respond
                                    422
                                    (errorBody code message [ "candidates", Encode.list (candidates |> List.map categoryJson) ])
                            match resolveBotCategory categories input.Category with
                            | BotCategoryUnknown candidates ->
                                return!
                                    unresolved "category_not_found" $"Keine Kategorie passt zu '{input.Category}'" candidates earlyReturn ctx
                            | BotCategoryAmbiguous candidates ->
                                return!
                                    unresolved "category_ambiguous" $"Mehrere Kategorien passen zu '{input.Category}'" candidates earlyReturn ctx
                            | BotCategoryResolved category ->
                                deps.Store.Purge now
                                let preview = {
                                    Id = Guid.NewGuid()
                                    CreatedAt = now
                                    ExpiresAt = now + previewTtl
                                    Target = target
                                    AmountEur = input.AmountEur
                                    Direction = input.Direction
                                    Payee = input.Payee
                                    Category = category
                                    Date = input.Date
                                    Memo = input.Memo
                                }
                                deps.Store.Add preview
                                return! respond 200 (previewJson preview) earlyReturn ctx
        }

let private bookPreview (deps: BotDeps) (p: BotPreview) : Async<BotCommitResult> =
    async {
        let signed = match p.Direction with BotOutflow -> -p.AmountEur | BotInflow -> p.AmountEur
        let input : Ledger.CommandHandlers.CashExpenseInput = {
            AccountId = p.Target.LedgerAccountId
            Date = p.Date
            Amount = { Amount = signed; Currency = "EUR" }
            Payee = Some p.Payee
            Memo = p.Memo
            Category = Some ({ CategoryId = p.Category.Id; CategoryName = p.Category.Name } : Ledger.Events.CategoryRef)
        }
        match!
            Ledger.CommandHandlers.addCashExpense
                Ledger.Events.Bob
                (deps.Now())
                (fun () -> deps.Push p)
                deps.TriggerMirror
                input
        with
        | Ok (Ledger.Events.EntryId id) -> return { EntryId = Some id; PushedToYnab = true; PushError = None }
        | Error err ->
            return { EntryId = None; PushedToYnab = false; PushError = Some (Ledger.CommandHandlers.describePushFailure err) }
    }

let private commitHandler (deps: BotDeps) (idText: string) : HttpHandler =
    fun _ ctx ->
        task {
            match Guid.TryParse idText with
            | false, _ ->
                return! respond 404 (errorBody "preview_not_found" "Unbekannte Vorschau" []) earlyReturn ctx
            | true, id ->
                match deps.Store.TryGet id with
                | None ->
                    return! respond 404 (errorBody "preview_not_found" "Unbekannte Vorschau" []) earlyReturn ctx
                | Some preview when not (deps.Store.IsCommitted id) && preview.ExpiresAt <= deps.Now() ->
                    return!
                        respond 410 (errorBody "preview_expired" "Die Vorschau ist abgelaufen, bitte neu anlegen" []) earlyReturn ctx
                | Some preview ->
                    try
                        let! result = deps.Store.CommitOnce(id, fun () -> bookPreview deps preview)
                        return! respond 200 (commitJson id result) earlyReturn ctx
                    with ex ->
                        printfn "[Bot] Commit %O fehlgeschlagen: %s" id ex.Message
                        return! respond 500 (errorBody "commit_failed" "Die Buchung ist fehlgeschlagen" []) earlyReturn ctx
        }

let private categoriesHandler (deps: BotDeps) : HttpHandler =
    fun _ ctx ->
        task {
            let! categories = deps.LoadCategories() |> Async.StartAsTask
            match categories with
            | Error reason -> return! respond 502 (errorBody "ynab_unavailable" reason []) earlyReturn ctx
            | Ok categories ->
                let items = selectableBotCategories categories |> List.map categoryJson
                return! respond 200 (Encode.object [ "categories", Encode.list items ]) earlyReturn ctx
        }

let private isBotPath (path: string) =
    path = "/bot" || path.StartsWith("/bot/", StringComparison.OrdinalIgnoreCase)

/// Der Einstieg: alles unter `/bot/` wird hier beantwortet, alles andere geht weiter an
/// die nächsten Handler. Ohne gültiges Bearer-Token (oder ohne gesetztes Token) 401,
/// danach Routen, unbekannte Pfade 404 als JSON.
let handler (deps: BotDeps) : HttpHandler =
    fun next ctx ->
        let path = if ctx.Request.Path.HasValue then ctx.Request.Path.Value else ""
        if not (isBotPath path) then
            next ctx
        else
            let authorized =
                match deps.Token() with
                | None -> false
                | Some token -> tokenMatches token (string ctx.Request.Headers.Authorization)
            if not authorized then
                respond 401 (errorBody "unauthorized" "Kein gültiges Bearer-Token" []) earlyReturn ctx
            else
                let segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                let routed =
                    match ctx.Request.Method, segments with
                    | "POST", [ "bot"; "transactions"; "preview" ] -> previewHandler deps
                    | "POST", [ "bot"; "transactions"; id; "commit" ] -> commitHandler deps id
                    | "GET", [ "bot"; "categories" ] -> categoriesHandler deps
                    | _ -> respond 404 (errorBody "not_found" "Unbekannter Pfad" [])
                routed earlyReturn ctx

// ============================================
// Echte Umgebung
// ============================================

let private ynabContext () : Async<Result<string * YnabBudgetId, string>> =
    async {
        let! tokenOpt = Persistence.Settings.getSetting "ynab_token"
        let! budgetOpt = Persistence.Settings.getSetting "ynab_default_budget_id"
        match tokenOpt, budgetOpt with
        | None, _ -> return Error "Kein YNAB-Token eingerichtet"
        | _, None -> return Error "Kein Standard-Budget eingerichtet"
        | Some token, Some budgetId -> return Ok (token, YnabBudgetId budgetId)
    }

/// Die Umgebung der laufenden App. Eine Instanz teilt sich einen Vorschau-Speicher.
let liveDeps () : BotDeps =
    {
        Token = fun () -> Server.ServerConfig.resolveBotToken (Environment.GetEnvironmentVariable Server.ServerConfig.botApiTokenVariable)
        MaxAmountEur =
            fun () -> Server.ServerConfig.resolveBotMaxAmountEur (Environment.GetEnvironmentVariable Server.ServerConfig.botMaxAmountVariable)
        Now = fun () -> DateTime.UtcNow
        LoadCategories =
            fun () ->
                async {
                    match! ynabContext () with
                    | Error reason -> return Error reason
                    | Ok (token, budgetId) ->
                        match! Server.YnabClient.getCategories token budgetId with
                        | Ok categories -> return Ok categories
                        | Error err -> return Error (Ledger.CommandHandlers.describePushFailure err)
                }
        ResolveTarget =
            fun () ->
                async {
                    let! accountOpt = Persistence.Settings.getSetting "ynab_quickadd_account_id"
                    match accountOpt |> Option.map Guid.TryParse with
                    | None -> return Error "Kein Quick-Add-Konto eingerichtet (Settings, YNAB)"
                    | Some (false, _) -> return Error "Das Quick-Add-Konto ist ungültig eingerichtet"
                    | Some (true, guid) ->
                        let ynabAccountId = YnabAccountId guid
                        match! Ledger.Persistence.findAccountByYnabAccountId ynabAccountId with
                        | None -> return Error "Das Quick-Add-Konto ist im Kassenbuch noch nicht bekannt"
                        | Some account ->
                            return Ok { LedgerAccountId = account.Id; YnabAccountId = ynabAccountId; AccountName = account.Name }
                }
        Push =
            fun p ->
                async {
                    match! ynabContext () with
                    | Error reason -> return Error (YnabError.Unauthorized reason)
                    | Ok (token, budgetId) ->
                        let request : ManualTransactionRequest = {
                            Amount = p.AmountEur
                            IsOutflow = (p.Direction = BotOutflow)
                            PayeeName = p.Payee
                            CategoryId = Some p.Category.Id
                            Date = p.Date
                            Memo = p.Memo
                        }
                        return! Server.YnabClient.createManualTransaction token budgetId p.Target.YnabAccountId request
                }
        TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground
        Store = BotPreviewStore()
    }
