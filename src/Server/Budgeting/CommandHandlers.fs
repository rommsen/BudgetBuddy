/// Die Abläufe von "Targets anwenden" (Context `budgeting`, budgeting-h2zfn, ADR 0065):
/// Vorschau, Übernehmen und Fehlgeschlagene erneut senden, dazu "Verschieben" (ADR 0069) und
/// "Zuweisen" (ADR 0078) und "Target pausieren" (ADR 0079). YNAB kommt als Port herein,
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
/// `SetAssigned month categoryId milliunits` setzt das Zugewiesen absolut (ein PATCH),
/// `SetTarget categoryId patch` schreibt das Target der Kategorie und liefert es danach (budgeting-k8wq3).
type YnabPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    SetAssigned: string -> Guid -> int64 -> Async<YnabResult<unit>>
    SetTarget: Guid -> TargetPatch -> Async<YnabResult<Target option>>
}

let private isRateLimit (error: YnabError) =
    match error with
    | YnabError.RateLimitExceeded _ -> true
    | _ -> false

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

/// Zwei Target-Änderungen derselben Kategorie laufen nicht ineinander (ADR 0080 Punkt 5): die zweite
/// wartet und sieht danach den Stand der ersten. Das Target gilt für alle Monate, deshalb ist der
/// Schlüssel die Kategorie, nicht der Monat. `internal`, damit ein Test die Sperre halten kann.
module internal TargetGuard =
    let private gates = ConcurrentDictionary<Guid, SemaphoreSlim>()

    let exclusive (categoryId: Guid) (work: Async<'a>) : Async<'a> =
        async {
            let gate = gates.GetOrAdd(categoryId, fun _ -> new SemaphoreSlim(1, 1))
            do! gate.WaitAsync() |> Async.AwaitTask
            try
                return! work
            finally
                gate.Release() |> ignore
        }

// ============================================
// Vorschau
// ============================================

/// Liest den Monat aus YNAB und die BB-Pausen des Monats und führt beides zusammen (ADR 0079 Punkt 3).
/// Unter der Sperre je Monat aufgerufen, sieht es jede Pause, die vorher festgehalten wurde.
/// Sind die Pausen nicht lesbar, ist das wie bei `loadStates` ein `InvalidResponse` (budgeting-r3wq8).
/// Neben dem Monat kommen die gespeicherten BB-Pausen mit, auch die ohne Target (`planPause`).
let private fetchBudgetMonthWithPauses (port: YnabPort) (monthKey: string) : Async<Result<BudgetMonth * Set<Guid>, YnabError>> =
    async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok raw ->
            match! loadPausedCategories monthKey with
            | Error message -> return Error (YnabError.InvalidResponse message)
            | Ok paused -> return Ok (toBudgetMonth paused raw, paused)
    }

let private fetchBudgetMonth (port: YnabPort) (monthKey: string) : Async<Result<BudgetMonth, YnabError>> =
    async {
        let! fetched = fetchBudgetMonthWithPauses port monthKey
        return fetched |> Result.map fst
    }

let private plan (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    async {
        match! fetchBudgetMonth port monthKey with
        | Error e -> return Error e
        | Ok month ->
            match planTargetApplication month with
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
                          OldAssigned = euroOfMilliunits s.Movement.OldAssigned
                          NewAssigned = euroOfMilliunits s.Movement.NewAssigned
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

/// Hält den Plan fest (eine Transaktion, gemeinsame `ApplicationId`) und sendet nacheinander. Der
/// Bericht liest Zu verteilen nicht neu, die Seite lädt den Monat ohnehin.
let private recordAndSend
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (plan: MovePlan)
    : Async<Result<ApplyReport, YnabError>> =
    async {
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
        return! reportWith applicationId monthKey None
    }

/// Verschieben oder Zuweisen von Zu verteilen. Unter der Monats-Sperre: den Monat frisch lesen und relativ dazu
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
        match! fetchBudgetMonth port monthKey with
        | Error e -> return Error e
        | Ok month ->
            match planMove month request with
            | Error message -> return Ok (Error message)
            | Ok plan ->
                let! report = recordAndSend port now monthKey plan
                return report |> Result.map Ok
    })

/// Zuweisen (ADR 0078): setzt das Zugewiesen absolut. Wie `moveMoney` unter der Monats-Sperre und
/// mit denselben Fehlerwegen. Der gelesene Wert ist der alte, ein Vergleich mit dem gezeigten Stand
/// findet nicht statt. Steht YNAB schon auf dem Wert, entsteht nichts: Der Bericht hat keine Zeilen,
/// und `Guid.Empty` sagt, dass es keine Anwendung gibt, die man erneut senden könnte.
let assignMoney
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (request: AssignRequest)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    MonthGuard.exclusive monthKey (async {
        match! fetchBudgetMonth port monthKey with
        | Error e -> return Error e
        | Ok month ->
            match planAssign month request with
            | Error message -> return Ok (Error message)
            | Ok None ->
                let unchanged =
                    { ApplicationId = string Guid.Empty
                      Month = monthKey
                      Lines = []
                      ActualToBeBudgeted = None }
                return Ok (Ok unchanged)
            | Ok (Some plan) ->
                let! report = recordAndSend port now monthKey plan
                return report |> Result.map Ok
    })

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0073, ADR 0079)
// ============================================

/// Target pausieren oder die Pause aufheben. Ein vergangener Monat wird vor allem anderen abgelehnt, ohne
/// YNAB zu lesen. Sonst unter der Sperre je Monat wie Targets anwenden: den Monat frisch lesen, planen
/// (`planPause`) und höchstens ein Ereignis anhängen. Nach YNAB geht nichts (ADR 0073 Punkt 5), der Port
/// wird nur gelesen. Außen `Error`: der Monat war nicht lesbar. Innen `Error`: abgelehnt. In beiden
/// Fällen ist nichts geschrieben. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let setTargetPause
    (port: YnabPort)
    (now: DateTime)
    (currentMonth: string)
    (monthKey: string)
    (request: TargetPauseRequest)
    : Async<Result<Result<PauseSource option, string>, YnabError>> =
    if not (targetsAllowed (monthKind currentMonth monthKey)) then
        async { return Ok (Error PausePastMonthText) }
    else
        MonthGuard.exclusive monthKey (async {
            match! fetchBudgetMonthWithPauses port monthKey with
            | Error e -> return Error e
            | Ok (month, pausedInBbRaw) ->
                match planPause month pausedInBbRaw request with
                | Error message -> return Ok (Error message)
                | Ok plan ->
                    match plan.Event with
                    | Some event ->
                        do!
                            appendPauseEvent
                                { Id = Guid.NewGuid()
                                  OccurredAt = now
                                  Month = monthKey
                                  CategoryId = plan.Category.Id
                                  CategoryName = plan.Category.Name
                                  Kind = event }
                    | None -> ()
                    return Ok (Ok plan.After)
        })

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0072, ADR 0080)
// ============================================

/// Grund der übrigen Schritte, wenn ein früherer scheiterte (ADR 0080 Punkt 4).
[<Literal>]
let TargetStepHeldBack = "Voriger Schritt nicht geschrieben, nicht gesendet"

/// Hält "geschrieben" nach einem `Ok` von YNAB fest, als Best-Effort: YNAB hat die Änderung schon
/// übernommen, ein Fehler beim Anhängen darf das nicht als Fehlschlag melden. Er wird geloggt, die Zeile
/// steht dann ohne Ergebnis in der Historie (budgeting-r3wq8).
let private recordWrittenBestEffort (changeId: Guid) (now: DateTime) : Async<unit> =
    async {
        try
            do! appendTargetChangeResult changeId now Written
        with ex ->
            eprintfn "Ergebnis der Target-Änderung %O nicht festgehalten (YNAB hat geschrieben): %s" changeId ex.Message
    }

/// Die festzuhaltende Zeile eines Patches, vom gezeigten Target aus.
let private targetChangeOf
    (now: DateTime)
    (monthKey: string)
    (category: YnabMonthCategory)
    (shown: Target)
    (kind: TargetChangeKind)
    (patch: TargetPatch)
    : TargetChange =
    let oldGoal = Some (toMilliunits shown.Goal)
    let newGoal, newDate =
        match kind with
        | TargetRemoved -> None, None
        | TargetEdited ->
            let goal =
                match patch.GoalTarget with
                | Keep -> oldGoal
                | SetTo value -> Some value
                | Clear -> None
            let date =
                match patch.TargetDate with
                | Keep -> shown.Date
                | SetTo value -> Some value
                | Clear -> None
            goal, date
    { Id = Guid.NewGuid()
      OccurredAt = now
      Month = monthKey
      CategoryId = category.Id
      CategoryName = category.Name.Trim()
      Kind = kind
      OldGoalTarget = oldGoal
      NewGoalTarget = newGoal
      OldDate = shown.Date
      NewDate = newDate }

/// Target ändern oder entfernen (ADR 0080). Außerhalb des laufenden Monats, bei einem Rhythmuswechsel und
/// beim Anlegen wird vor allem anderen abgelehnt, ohne YNAB zu lesen. Sonst unter der Sperre je Kategorie:
/// den Monat frisch lesen und das Target suchen. Weicht es vom gezeigten ab, gewinnt YNAB: nichts gesendet,
/// nichts festgehalten (`TargetChangedInYnab`). Steht YNAB schon auf dem Wunsch, ebenso nichts. Sonst die
/// Schritte in einer Transaktion festhalten, dann der Reihe nach senden, je Schritt ein Ergebnis. Kein
/// Erneut-Senden: ein Fehlschlag kommt als innerer `Error` mit YNABs Meldung. Außen `Error`: der Monat
/// war nicht lesbar. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let changeTarget
    (port: YnabPort)
    (now: DateTime)
    (currentMonth: string)
    (monthKey: string)
    (request: TargetChangeRequest)
    : Async<Result<Result<TargetChangeResult, string>, YnabError>> =
    if not (targetEditAllowed (monthKind currentMonth monthKey)) then
        async { return Ok (Error TargetEditMonthText) }
    else
        match targetChangeSupported request with
        | Error message -> async { return Ok (Error message) }
        | Ok () ->
            TargetGuard.exclusive request.CategoryId (async {
                match! port.FetchMonth monthKey with
                | Error e -> return Error e
                | Ok raw ->
                    match raw.Categories |> List.tryFind (fun c -> c.Id = request.CategoryId && isVisible c) with
                    | None ->
                        return Ok (Error CategoryNotVisibleText)
                    | Some category when category.Target <> request.Shown ->
                        return Ok (Ok (TargetChangedInYnab category.Target))
                    | Some category ->
                        match request.Shown with
                        | None -> return Ok (Ok (TargetWritten None))
                        | Some shown ->
                            match planTargetWrite shown request.Wanted with
                            | Error message -> return Ok (Error message)
                            | Ok [] -> return Ok (Ok (TargetWritten category.Target))
                            | Ok patches when patches |> List.exists targetPatchIsEmpty ->
                                // BB lehnt einen leeren Patch selbst ab, bevor etwas festgehalten oder
                                // gesendet wird; sonst käme er als "YNAB hat abgelehnt" zurück.
                                return Ok (Error EmptyTargetPatchText)
                            | Ok patches ->
                                let kind = targetChangeKindOf request.Wanted
                                let steps = patches |> List.map (fun p -> targetChangeOf now monthKey category shown kind p, p)
                                do! appendTargetChanges (steps |> List.map fst)
                                let outcome : Result<Target option, string> ref = ref (Ok category.Target)
                                for change, patch in steps do
                                    match outcome.Value with
                                    | Error _ -> do! appendTargetChangeResult change.Id now (Failed TargetStepHeldBack)
                                    | Ok _ ->
                                        match! port.SetTarget category.Id patch with
                                        | Ok after ->
                                            do! recordWrittenBestEffort change.Id now
                                            outcome.Value <- Ok after
                                        | Error e ->
                                            let reason = ynabErrorText e
                                            do! appendTargetChangeResult change.Id now (Failed reason)
                                            outcome.Value <- Error reason
                                return Ok (outcome.Value |> Result.map TargetWritten)
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
                                |> Option.map (decideRetry (euroOfMilliunits m.OldAssigned) (euroOfMilliunits m.NewAssigned))
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
