/// Die Abläufe von "Targets anwenden" (Context `budgeting`, budgeting-h2zfn, ADR 0065):
/// Vorschau, Übernehmen und Fehlgeschlagene erneut senden. YNAB kommt als Port herein,
/// damit Tests einen Fake-Client einsetzen (der Server spricht YNAB in Tests nie live an).
module Server.Budgeting.CommandHandlers

open System
open System.Collections.Concurrent
open System.Threading
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.Persistence

/// Was die Abläufe von YNAB brauchen. `FetchMonth` liest den Monat roh (`YnabMonth`),
/// `SetAssigned month categoryId milliunits` setzt das Zugewiesen absolut (ein PATCH).
type YnabPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    SetAssigned: string -> Guid -> int64 -> Async<YnabResult<unit>>
}

/// Der Grund einer fehlgeschlagenen Zeile in einem Satz. Das Rate-Limit heißt immer
/// "Rate-Limit", damit Seite und Test es erkennen.
let failureReason (error: YnabError) : string =
    match error with
    | YnabError.RateLimitExceeded _ -> "Rate-Limit"
    | YnabError.Unauthorized _ -> "YNAB-Token ungültig"
    | YnabError.NetworkError message -> $"Netzwerkfehler: {message}"
    | YnabError.InvalidResponse message -> $"YNAB hat abgelehnt: {message}"
    | YnabError.BudgetNotFound _ -> "Budget nicht gefunden"
    | YnabError.AccountNotFound _ -> "Konto nicht gefunden"
    | YnabError.CategoryNotFound _ -> "Kategorie nicht gefunden"

let private isRateLimit (error: YnabError) =
    match error with
    | YnabError.RateLimitExceeded _ -> true
    | _ -> false

let private euro (milliunits: int64) : Money =
    { Amount = decimal milliunits / 1000m; Currency = "EUR" }

// ============================================
// Schreib-Sperre je Monat
// ============================================

/// Zwei Anwendungen für denselben Monat laufen nicht ineinander (Wächter wie `MirrorGuard`,
/// ADR 0055): die zweite wartet, liest dann den neuen Stand und sieht ihre Vorschau als
/// überholt. `internal`, damit ein Test die Sperre halten kann.
module internal MonthGuard =
    let private gates = ConcurrentDictionary<string, SemaphoreSlim>()

    let exclusive (month: string) (work: Async<'a>) : Async<'a> =
        async {
            let gate = gates.GetOrAdd(month, fun _ -> new SemaphoreSlim(1, 1))
            do! gate.WaitAsync() |> Async.AwaitTask
            try
                return! work
            finally
                gate.Release() |> ignore
        }

// ============================================
// Vorschau
// ============================================

let private plan (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok raw ->
            match planTargetApplication (toBudgetMonth raw) with
            | Ok preview -> return Ok preview
            | Error message -> return Error (YnabError.InvalidResponse message)
    }

/// Die Vorschau: liest den Monat und plant. Schreibt weder nach YNAB noch in die Datenbank.
let preview (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    plan port monthKey

// ============================================
// Berichte
// ============================================

let private reportOf (port: YnabPort) (applicationId: Guid) (monthKey: string) : Async<ApplyReport> =
    async {
        let! states = loadApplication applicationId
        let! actual =
            async {
                match! port.FetchMonth monthKey with
                | Ok raw -> return Some raw.ToBeBudgeted
                | Error _ -> return None
            }
        let lines =
            states
            |> List.map (fun s ->
                { CategoryId = s.Movement.CategoryId
                  CategoryName = s.Movement.CategoryName
                  OldAssigned = euro s.Movement.OldAssigned
                  NewAssigned = euro s.Movement.NewAssigned
                  Outcome = s.Latest |> Option.defaultValue (Failed "nicht gesendet") })
        return
            { ApplicationId = string applicationId
              Month = monthKey
              Lines = lines
              ActualToBeBudgeted = actual }
    }

/// Sendet die Zeilen nacheinander. Ein Fehler schließt die Zeile als fehlgeschlagen ab, die
/// übrigen werden trotzdem gesendet. Bei 429 bricht der Lauf ab: der Rest wird als
/// fehlgeschlagen mit Grund "Rate-Limit" festgehalten, schon Geschriebenes bleibt unberührt.
let private sendAll
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (items: (Guid * Guid * int64) list) // Bewegung, Kategorie, Zielwert in Milliunits
    : Async<unit> =
    async {
        let rateLimited = ref false
        for movementId, categoryId, target in items do
            if rateLimited.Value then
                do! appendResult movementId now (Failed "Rate-Limit")
            else
                match! port.SetAssigned monthKey categoryId target with
                | Ok () -> do! appendResult movementId now Written
                | Error e ->
                    if isRateLimit e then rateLimited.Value <- true
                    do! appendResult movementId now (Failed (failureReason e))
    }

// ============================================
// Übernehmen
// ============================================

/// Übernehmen. Unter der Monats-Sperre: den Monat neu lesen und mit der gezeigten Vorschau
/// vergleichen. Weicht etwas ab, wird nichts geschrieben und die frische Vorschau
/// zurückgegeben. Sonst zuerst alle Bewegungen festhalten, dann die PATCHes nacheinander
/// senden (ADR 0050/0055). Danach liest der Bericht Zu verteilen erneut.
let apply
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (shown: TargetPreview)
    : Async<Result<ApplyResult, YnabError>> =
    MonthGuard.exclusive monthKey (async {
        match! plan port monthKey with
        | Error e -> return Error e
        | Ok fresh when not (previewsMatch shown fresh) -> return Ok (PreviewChanged fresh)
        | Ok fresh when List.isEmpty fresh.Lines -> return Ok (PreviewChanged fresh)
        | Ok fresh ->
            let applicationId = Guid.NewGuid()
            let movements =
                fresh.Lines
                |> List.map (fun l ->
                    { Id = Guid.NewGuid()
                      ApplicationId = applicationId
                      OccurredAt = now
                      Origin = OriginTargets
                      Month = fresh.Month
                      CategoryId = l.CategoryId
                      CategoryName = l.CategoryName
                      OldAssigned = toMilliunits l.OldAssigned
                      NewAssigned = toMilliunits l.NewAssigned })
            do! appendMovements movements
            do!
                sendAll port now fresh.Month (movements |> List.map (fun m -> m.Id, m.CategoryId, m.NewAssigned))
            let! report = reportOf port applicationId fresh.Month
            return Ok (Applied report)
    })

// ============================================
// Fehlgeschlagene erneut senden
// ============================================

/// Sendet die Zeilen einer Anwendung erneut, deren jüngstes Ergebnis "fehlgeschlagen" ist.
/// Der Zielwert ist absolut, ein zweites Senden ändert nichts. Der Live-Wert entscheidet
/// (`decideRetry`): steht YNAB schon auf dem Ziel, wird nichts gesendet und die Zeile als
/// geschrieben festgehalten; steht es weder auf dem alten noch auf dem Zielwert, gewinnt
/// YNAB (ADR 0055) und die Zeile wird als "in YNAB geändert" geschlossen.
let retryFailed
    (port: YnabPort)
    (now: DateTime)
    (applicationId: Guid)
    : Async<Result<ApplyReport, YnabError>> =
    async {
        let! states = loadApplication applicationId
        match states with
        | [] -> return Error (YnabError.InvalidResponse "Unbekannte Anwendung")
        | first :: _ ->
            let monthKey = first.Movement.Month
            return!
                MonthGuard.exclusive monthKey (async {
                    match! port.FetchMonth monthKey with
                    | Error e -> return Error e
                    | Ok live ->
                        let! states = loadApplication applicationId
                        let liveAssigned =
                            live.Categories |> List.map (fun c -> c.Id, c.Assigned) |> Map.ofList
                        let rateLimited = ref false
                        for s in states do
                            match s.Latest with
                            | Some (Failed _) ->
                                let m = s.Movement
                                match Map.tryFind m.CategoryId liveAssigned with
                                | None -> do! appendResult m.Id now (Failed "Kategorie in YNAB nicht gefunden")
                                | Some current ->
                                    match decideRetry (euro m.OldAssigned) (euro m.NewAssigned) current with
                                    | AlreadyAtTarget -> do! appendResult m.Id now Written
                                    | YnabWins -> do! appendResult m.Id now ChangedInYnab
                                    | SendTarget ->
                                        if rateLimited.Value then
                                            do! appendResult m.Id now (Failed "Rate-Limit")
                                        else
                                            match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                                            | Ok () -> do! appendResult m.Id now Written
                                            | Error e ->
                                                if isRateLimit e then rateLimited.Value <- true
                                                do! appendResult m.Id now (Failed (failureReason e))
                            | _ -> ()
                        let! report = reportOf port applicationId monthKey
                        return Ok report
                })
    }
