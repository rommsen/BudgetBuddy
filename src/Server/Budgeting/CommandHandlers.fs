/// Die Abläufe von "Targets anwenden" (Context `budgeting`, budgeting-h2zfn, ADR 0065):
/// Vorschau, Übernehmen und Fehlgeschlagene erneut senden, dazu "Verschieben"
/// (budgeting-afd8h, ADR 0069). YNAB kommt als Port herein,
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

/// Die Zeilen einer Anwendung, mit `YnabError` wie die übrigen Fehler dieser Abläufe.
let private loadStates (applicationId: Guid) : Async<Result<MovementState list, YnabError>> =
    async {
        let! states = loadApplication applicationId
        return states |> Result.mapError YnabError.InvalidResponse
    }

let private reportWith (applicationId: Guid) (monthKey: string) (actual: Money option) : Async<Result<ApplyReport, YnabError>> =
    async {
        let! states = loadStates applicationId
        return
            states
            |> Result.map (fun states ->
                { ApplicationId = string applicationId
                  Month = monthKey
                  Lines =
                    states
                    |> List.map (fun s ->
                        { CategoryId = s.Movement.CategoryId
                          CategoryName = s.Movement.CategoryName
                          OldAssigned = euro s.Movement.OldAssigned
                          NewAssigned = euro s.Movement.NewAssigned
                          Outcome = s.Latest |> Option.defaultValue (Failed "nicht gesendet") })
                  ActualToBeBudgeted = actual })
    }

let private reportOf (port: YnabPort) (applicationId: Guid) (monthKey: string) : Async<Result<ApplyReport, YnabError>> =
    async {
        let! actual =
            async {
                match! port.FetchMonth monthKey with
                | Ok raw -> return Some raw.ToBeBudgeted
                | Error _ -> return None
            }
        return! reportWith applicationId monthKey actual
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
                    do! appendResult movementId now (Failed (ynabErrorText e))
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
                      Origin = TargetsApplied
                      Month = fresh.Month
                      CategoryId = l.CategoryId
                      CategoryName = l.CategoryName
                      OldAssigned = toMilliunits l.OldAssigned
                      NewAssigned = toMilliunits l.NewAssigned })
            do! appendMovements movements
            do!
                sendAll port now fresh.Month (movements |> List.map (fun m -> m.Id, m.CategoryId, m.NewAssigned))
            let! report = reportOf port applicationId fresh.Month
            return report |> Result.map Applied
    })

// ============================================
// Verschieben (budgeting-afd8h, ADR 0069)
// ============================================

/// Grund der Ziel-Zeile, wenn die Quelle nicht geschrieben wurde.
[<Literal>]
let TargetHeldBack = "Quelle nicht geschrieben, Ziel nicht gesendet"

/// Grund der Quell-Zeile, wenn das Ziel im Monat fehlt.
[<Literal>]
let SourceHeldBack = "Ziel in YNAB nicht gefunden, Quelle nicht gesendet"

/// Sendet die Zeilen nacheinander und hält beim ersten Fehler an: stiege das Ziel, ohne dass die
/// Quelle sinkt, käme das Geld still aus Zu verteilen (ADR 0069 Punkt 3). Die übrigen Zeilen
/// stehen danach als fehlgeschlagen da. Anders als `sendAll`, wo jede Zeile für sich steht.
let private sendInOrder
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (movements: Movement list)
    : Async<unit> =
    async {
        let stopped = ref false
        for m in movements do
            if stopped.Value then
                do! appendResult m.Id now (Failed TargetHeldBack)
            else
                match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                | Ok () -> do! appendResult m.Id now Written
                | Error e ->
                    stopped.Value <- true
                    do! appendResult m.Id now (Failed (ynabErrorText e))
    }

/// Verschieben oder Zuweisen. Unter der Monats-Sperre: den Monat frisch lesen und relativ dazu
/// planen, die Bewegungen festhalten (eine Transaktion, gemeinsame `ApplicationId`), dann
/// nacheinander senden. Außen `Error`: der Monat war nicht lesbar. Innen `Error`: der Auftrag ist
/// abgelehnt. In beiden Fällen ist nichts geschrieben. Der Bericht liest Zu verteilen nicht neu,
/// weil die Seite den Monat ohnehin neu lädt und jeder Aufruf das Sheet langsamer machte.
let moveMoney
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (request: MoveRequest)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    MonthGuard.exclusive monthKey (async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok raw ->
            match planMove (toBudgetMonth raw) request with
            | Error message -> return Ok (Error message)
            | Ok plan ->
                let applicationId = Guid.NewGuid()
                let movements =
                    plan.Lines
                    |> List.map (fun l ->
                        { Id = Guid.NewGuid()
                          ApplicationId = applicationId
                          OccurredAt = now
                          Origin = plan.Origin
                          Month = monthKey
                          CategoryId = l.CategoryId
                          CategoryName = l.CategoryName
                          OldAssigned = toMilliunits l.OldAssigned
                          NewAssigned = toMilliunits l.NewAssigned })
                do! appendMovements movements
                do! sendInOrder port now monthKey movements
                let! report = reportWith applicationId monthKey None
                return report |> Result.map Ok
    })

// ============================================
// Fehlgeschlagene erneut senden
// ============================================

/// Sendet die offenen Zeilen einer Anwendung erneut: fehlgeschlagene und solche ohne Stand (der
/// Lauf brach vor ihnen ab). Der Zielwert ist absolut, ein zweites Senden ändert nichts. Der
/// Live-Wert entscheidet (`decideRetry`): steht YNAB schon auf dem Ziel, wird nichts gesendet und
/// die Zeile als geschrieben festgehalten; steht es weder auf dem alten noch auf dem Zielwert,
/// gewinnt YNAB (ADR 0055) und die Zeile wird als "in YNAB geändert" geschlossen.
///
/// Bei einer Verschiebung bleibt die Reihenfolge verbindlich (ADR 0069 Punkt 3). Maßgeblich ist der
/// Stand der Vorgänger-Zeile, auch aus einem früheren Lauf: solange die Quelle nicht geschrieben
/// ist, geht das Ziel nicht hinaus. Scheitert sie wieder, bleibt das Ziel unberührt. Hat YNAB bei
/// der Quelle gewonnen, wird das Ziel ungesendet ebenso als "in YNAB geändert" geschlossen: die
/// Quelle gehört dann YNAB, und ein gesendetes Ziel nähme das Geld aus Zu verteilen. Offen ist
/// dabei, ob die Quelle je gesunken ist. Kam ihr PATCH an und nur die Antwort ging verloren, und
/// änderte sie sich danach, bleibt das Geld in Zu verteilen und Roman verschiebt es von Hand.
///
/// Vor dem ersten Senden werden beide Stände geprüft (ADR 0070, Nachtrag). Ist das Ziel schon
/// verloren, geht die Quelle nicht hinaus und schließt mit ihm: sonst läge das Geld in Zu verteilen.
/// Fehlt das Ziel im Monat, bleibt die Quelle ungesendet fehlgeschlagen.
let retryFailed
    (port: YnabPort)
    (now: DateTime)
    (applicationId: Guid)
    : Async<Result<ApplyReport, YnabError>> =
    async {
        match! loadStates applicationId with
        | Error e -> return Error e
        | Ok [] -> return Error (YnabError.InvalidResponse "Unbekannte Anwendung")
        | Ok (first :: _) ->
            let monthKey = first.Movement.Month
            return!
                MonthGuard.exclusive monthKey (async {
                    match! port.FetchMonth monthKey with
                    | Error e -> return Error e
                    | Ok live ->
                        match! loadStates applicationId with
                        | Error e -> return Error e
                        | Ok states ->
                            let liveAssigned =
                                live.Categories |> List.map (fun c -> c.Id, c.Assigned) |> Map.ofList
                            let decisionFor (m: Movement) =
                                Map.tryFind m.CategoryId liveAssigned
                                |> Option.map (decideRetry (euro m.OldAssigned) (euro m.NewAssigned))
                            let inOrder = sendsInOrder first.Movement.Origin
                            // Ist das Ziel verloren, wäre ein Senden der Quelle das Geld in Zu verteilen.
                            let sourceBlocked =
                                match inOrder, states with
                                | true, [ _; target ] when isOpenLine target.Latest ->
                                    match decisionFor target.Movement with
                                    | Some YnabWins -> Some ChangedInYnab
                                    | None -> Some (Failed SourceHeldBack)
                                    | Some _ -> None
                                | _ -> None
                            let rateLimited = ref false
                            let retryLine (m: Movement) =
                                async {
                                    let close outcome =
                                        async {
                                            do! appendResult m.Id now outcome
                                            return outcome
                                        }
                                    match decisionFor m with
                                    | None -> return! close (Failed "Kategorie in YNAB nicht gefunden")
                                    | Some AlreadyAtTarget -> return! close Written
                                    | Some YnabWins -> return! close ChangedInYnab
                                    | Some SendTarget ->
                                        match sourceBlocked with
                                        | Some outcome -> return! close outcome
                                        | None when rateLimited.Value -> return! close (Failed "Rate-Limit")
                                        | None ->
                                            match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                                            | Ok () -> return! close Written
                                            | Error e ->
                                                if isRateLimit e then rateLimited.Value <- true
                                                return! close (Failed (ynabErrorText e))
                                }
                            // Der Stand der Vorgänger-Zeile nach diesem Lauf.
                            let previous = ref None
                            for s in states do
                                if not (isOpenLine s.Latest) then
                                    previous.Value <- s.Latest
                                else
                                    match (if inOrder then previous.Value else None) with
                                    | Some ChangedInYnab ->
                                        do! appendResult s.Movement.Id now ChangedInYnab
                                        previous.Value <- Some ChangedInYnab
                                    | Some (Failed _) -> ()
                                    | Some Written
                                    | None ->
                                        let! outcome = retryLine s.Movement
                                        previous.Value <- Some outcome
                            return! reportOf port applicationId monthKey
                })
    }
