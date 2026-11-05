/// Die Abläufe von "Targets anwenden" (Context `budgeting`, budgeting-h2zfn, ADR 0065):
/// Vorschau, Übernehmen und Fehlgeschlagene erneut senden, dazu "Verschieben" (ADR 0069) und
/// "Zuweisen" (ADR 0078) und "Target pausieren" (ADR 0079). YNAB kommt als Port herein,
/// damit Tests einen Fake-Client einsetzen (der Server spricht YNAB in Tests nie live an).
module Server.Budgeting.CommandHandlers

open System
open System.Collections.Concurrent
open System.Threading
open Microsoft.Data.Sqlite
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain
open Budgeting.Events
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
/// Sind die Pausen nicht lesbar, ist das wie bei `loadStates` ein `LocalReadFailed` (budgeting-c6vn4, P1).
/// Neben dem Monat kommen die gespeicherten BB-Pausen mit, auch die ohne Target (`planPause`).
let private fetchBudgetMonthWithPauses
    (loadPaused: string -> Async<Result<Set<Guid>, string>>)
    (port: YnabPort)
    (monthKey: string)
    : Async<Result<BudgetMonth * Set<Guid>, YnabError>> =
    async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok raw ->
            match! loadPaused monthKey with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok paused -> return Ok (toBudgetMonth paused raw, paused)
    }

let private fetchBudgetMonth (port: YnabPort) (monthKey: string) : Async<Result<BudgetMonth, YnabError>> =
    async {
        let! fetched = fetchBudgetMonthWithPauses loadPausedCategories port monthKey
        return fetched |> Result.map fst
    }

let private plan (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    async {
        match! fetchBudgetMonth port monthKey with
        | Error e -> return Error e
        | Ok month ->
            match planTargetApplication month with
            | Ok preview -> return Ok preview
            // BB lehnt ab, nicht YNAB (budgeting-b5n8q).
            | Error message -> return Error (YnabError.RefusedByBb message)
    }

/// Die Vorschau: liest den Monat und plant. Schreibt weder nach YNAB noch in die Datenbank.
let preview (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    plan port monthKey

// ============================================
// Berichte
// ============================================

/// Die Zeilen einer Anwendung, mit `YnabError` wie die übrigen Fehler dieser Abläufe.
let private loadStates (connect: unit -> SqliteConnection) (applicationId: Guid) : Async<Result<MovementState list, YnabError>> =
    async {
        let! states = loadApplicationWith connect applicationId
        return states |> Result.mapError YnabError.LocalReadFailed
    }

let private reportWith
    (connect: unit -> SqliteConnection)
    (applicationId: Guid)
    (monthKey: string)
    (actual: Money option)
    : Async<Result<ApplyReport, YnabError>> =
    async {
        let! states = loadStates connect applicationId
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

let private reportOf
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (applicationId: Guid)
    (monthKey: string)
    : Async<Result<ApplyReport, YnabError>> =
    async {
        let! actual =
            async {
                match! port.FetchMonth monthKey with
                | Ok raw -> return Some raw.ToBeBudgeted
                | Error _ -> return None
            }
        return! reportWith connect applicationId monthKey actual
    }

// ============================================
// Befehls-Id (ADR 0084 Punkt 8, budgeting-qm5x9)
// ============================================

/// Die Ablehnung, wenn eine Befehls-Id schon zu einem anderen Auftrag festgehalten ist.
[<Literal>]
let ForeignCommandText = "Befehls-Id gehört zu einem anderen Auftrag."

/// Das Ereignis, mit dem die Befehls-Id festgehalten ist, oder `None`. Liest über `connect`.
let private findCommand (connect: unit -> SqliteConnection) (commandId: Guid) : Async<EventStore.RecordedEvent option> =
    async {
        use conn = connect ()
        return EventStore.tryProcessedCommandOn conn commandId
    }

/// Die Kategorien eines Entscheidungs- oder Pause-Ereignisses, bei einer Verschiebung erst die Quelle, dann das Ziel.
let private categoriesOf (event: BudgetEvent) : Guid list =
    match event with
    | BudgetEvent.AssignmentChangedInBb change -> [ change.Line.CategoryId ]
    | BudgetEvent.MoneyMoved move -> [ move.From.CategoryId; move.To.CategoryId ]
    | BudgetEvent.TargetsApplied application -> application.Lines |> List.map (fun l -> l.CategoryId)
    | BudgetEvent.TargetPaused pause
    | BudgetEvent.TargetPauseLifted pause -> [ pause.CategoryId ]
    | _ -> []

/// Gehört das festgehaltene Ereignis zum Auftrag: Kontext `budgeting`, Monats-Stream des Auftrags, einer der
/// erwarteten Typen und, wenn `expectedCategories` sie nennt, dieselben Kategorien in derselben Reihenfolge?
/// Targets anwenden nennt keine, sein Auftrag ist der ganze Monat.
let private belongsTo
    (monthKey: string)
    (expectedTypes: string list)
    (expectedCategories: Guid list option)
    (recorded: EventStore.RecordedEvent)
    : bool =
    let monthStream = monthIdOf monthKey |> Result.toOption |> Option.map (MonthStream >> streamIdOf)
    let sameCategories () =
        match expectedCategories with
        | None -> true
        | Some expected ->
            match decodePayload recorded.EventType recorded.SchemaVersion recorded.Payload with
            | Ok event -> categoriesOf event = expected
            | Error message -> failwith $"Ereignis {recorded.EventId} der Befehls-Id nicht lesbar: {message}"
    recorded.Context = Context
    && Some recorded.StreamId = monthStream
    && List.contains recorded.EventType expectedTypes
    && sameCategories ()

/// Unter der Sperre vor allem anderen: Ist die Befehls-Id festgehalten, kommt die Antwort aus `answer`, ohne
/// YNAB zu lesen und ohne etwas anzuhängen. Sonst läuft `fresh`.
let private onceFor
    (commandId: Guid option)
    (answer: EventStore.RecordedEvent -> Async<'r>)
    (fresh: Async<'r>)
    : Async<'r> =
    async {
        match commandId with
        | None -> return! fresh
        | Some id ->
            match! findCommand global.Persistence.getConnection id with
            | Some recorded -> return! answer recorded
            | None -> return! fresh
    }

/// Das Anhängen meldete die Befehls-Id trotz Vorabprüfung als festgehalten (Netz der Transaktion, etwa
/// dieselbe Id in zwei Monaten): die Antwort auf demselben Weg aus dem Stream. Fehlt ihr Ereignis, sind
/// `processed_commands` und `events` uneins.
let private answerAgain
    (commandId: Guid)
    (answer: EventStore.RecordedEvent -> Async<'r>)
    : Async<'r> =
    async {
        match! findCommand global.Persistence.getConnection commandId with
        | Some recorded -> return! answer recorded
        | None -> return failwith $"Befehls-Id {commandId} als festgehalten gemeldet, ihr Ereignis fehlt"
    }

/// Die Typen des Entscheidungsereignisses, das ein Verschieben festhält, nach seiner Herkunft (`moveOriginOf`).
let private moveEventTypes (request: MoveRequest) : string list =
    [ EventTypes.decisionOf (moveOriginOf request) ]

/// Die Kategorie-Seiten eines Verschiebens, erst die Quelle, dann das Ziel.
let private moveCategories (request: MoveRequest) : Guid list =
    [ request.Source; request.Target ]
    |> List.choose (fun side ->
        match side with
        | CategorySide id -> Some id
        | ToBeBudgetedSide -> None)

/// Der Bericht der festgehaltenen Entscheidung, oder `None`, wenn sie nicht zum Auftrag gehört (`belongsTo`).
/// Eine Wiederholung trägt Zu verteilen nicht: die erste Antwort las es neu, hier wird YNAB nicht gelesen.
let private storedReport
    (monthKey: string)
    (expectedTypes: string list)
    (expectedCategories: Guid list option)
    (recorded: EventStore.RecordedEvent)
    : Async<Result<ApplyReport, YnabError> option> =
    async {
        if belongsTo monthKey expectedTypes expectedCategories recorded then
            let! report = reportWith global.Persistence.getConnection recorded.EventId monthKey None
            return Some report
        else
            return None
    }

/// Die Antwort auf eine Wiederholung von Verschieben und Zuweisen: der Bericht der festgehaltenen
/// Entscheidung, eine fremde Id als innerer `Error`.
let private decisionAnswer
    (monthKey: string)
    (expectedTypes: string list)
    (expectedCategories: Guid list)
    (recorded: EventStore.RecordedEvent)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    async {
        match! storedReport monthKey expectedTypes (Some expectedCategories) recorded with
        | Some report -> return report |> Result.map Ok
        | None -> return Ok (Error ForeignCommandText)
    }

/// Sendet die Zeilen nacheinander. Ein Fehler schließt die Zeile als fehlgeschlagen ab, die
/// übrigen werden trotzdem gesendet. Bei 429 bricht der Lauf ab: der Rest wird als
/// fehlgeschlagen mit Grund "Rate-Limit" festgehalten, schon Geschriebenes bleibt unberührt.
let private sendAll
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (movements: Movement list)
    : Async<unit> =
    async {
        let rateLimited = ref false
        for m in movements do
            if rateLimited.Value then
                do! recordWriteBack m now (WriteBackFailed "Rate-Limit")
            else
                match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                | Ok () -> do! recordWriteBack m now WrittenBack
                | Error e ->
                    if isRateLimit e then rateLimited.Value <- true
                    do! recordWriteBack m now (WriteBackFailed (ynabErrorText e))
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
    // Eine fremde Id kommt hier außen als `RefusedByBb`: `ApplyResult` hat keinen Fall für eine Ablehnung, BB
    // lehnt in diesem Ablauf wie bei `plan` über den äußeren Fehler ab.
    let answer (recorded: EventStore.RecordedEvent) =
        async {
            match! storedReport monthKey [ EventTypes.decisionOf TargetsApplied ] None recorded with
            | Some report -> return report |> Result.map Applied
            | None -> return Error (YnabError.RefusedByBb ForeignCommandText)
        }
    MonthGuard.exclusive monthKey (onceFor shown.CommandId answer (async {
        match! plan port monthKey with
        | Error e -> return Error e
        | Ok fresh when not (previewsMatch shown fresh) -> return Ok (PreviewChanged fresh)
        | Ok fresh when List.isEmpty fresh.Lines -> return Ok (PreviewChanged fresh)
        | Ok fresh ->
            let lines =
                fresh.Lines
                |> List.map (fun l ->
                    { CategoryId = l.CategoryId
                      CategoryName = l.CategoryName
                      OldAssigned = toMilliunits l.OldAssigned
                      NewAssigned = toMilliunits l.NewAssigned })
            let skipped : SkippedPausedLine list =
                fresh.Skipped
                |> List.map (fun s ->
                    { CategoryId = s.CategoryId
                      CategoryName = s.CategoryName
                      Underfunded = toMilliunits s.Underfunded })
            match! recordDecision shown.CommandId now fresh.Month TargetsApplied lines skipped with
            | CommandAlreadyProcessed (id, _) -> return! answerAgain id answer
            | Recorded movements ->
                do! sendAll port now fresh.Month movements
                let applicationId = (List.head movements).ApplicationId
                let! report = reportOf global.Persistence.getConnection port applicationId fresh.Month
                return report |> Result.map Applied
    }))

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
                do! recordWriteBack m now (WriteBackFailed TargetHeldBack)
            else
                match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                | Ok () -> do! recordWriteBack m now WrittenBack
                | Error e ->
                    stopped.Value <- true
                    do! recordWriteBack m now (WriteBackFailed (ynabErrorText e))
    }

/// Hält den Plan fest (eine Transaktion, gemeinsame `ApplicationId`) und sendet nacheinander. Der
/// Bericht liest Zu verteilen nicht neu, die Seite lädt den Monat ohnehin.
let private recordAndSend
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (commandId: Guid option)
    (answer: EventStore.RecordedEvent -> Async<Result<Result<ApplyReport, string>, YnabError>>)
    (plan: MovePlan)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    async {
        let lines =
            plan.Lines
            |> List.map (fun l ->
                { CategoryId = l.CategoryId
                  CategoryName = l.CategoryName
                  OldAssigned = toMilliunits l.OldAssigned
                  NewAssigned = toMilliunits l.NewAssigned })
        match! recordDecision commandId now monthKey plan.Origin lines [] with
        | CommandAlreadyProcessed (id, _) -> return! answerAgain id answer
        | Recorded movements ->
            do! sendInOrder port now monthKey movements
            let! report = reportWith global.Persistence.getConnection (List.head movements).ApplicationId monthKey None
            return report |> Result.map Ok
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
    let answer = decisionAnswer monthKey (moveEventTypes request) (moveCategories request)
    MonthGuard.exclusive monthKey (onceFor request.CommandId answer (async {
        match! fetchBudgetMonth port monthKey with
        | Error e -> return Error e
        | Ok month ->
            match planMove month request with
            | Error message -> return Ok (Error message)
            | Ok plan -> return! recordAndSend port now monthKey request.CommandId answer plan
    }))

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
    let answer = decisionAnswer monthKey [ EventTypes.decisionOf MoneyAssigned ] [ request.CategoryId ]
    MonthGuard.exclusive monthKey (onceFor request.CommandId answer (async {
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
            | Ok (Some plan) -> return! recordAndSend port now monthKey request.CommandId answer plan
    }))

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0073, ADR 0079)
// ============================================

/// Target pausieren oder die Pause aufheben. Ein vergangener Monat wird vor allem anderen abgelehnt, ohne
/// YNAB zu lesen. Sonst unter der Sperre je Monat wie Targets anwenden: den Monat frisch lesen, planen
/// (`planPause`) und höchstens ein Ereignis anhängen. Nach YNAB geht nichts (ADR 0073 Punkt 5), der Port
/// wird nur gelesen. Außen `Error`: der Monat war nicht lesbar. Innen `Error`: abgelehnt. In beiden
/// Fällen ist nichts geschrieben. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let setTargetPauseWith
    (loadPaused: string -> Async<Result<Set<Guid>, string>>)
    (port: YnabPort)
    (now: DateTime)
    (currentMonth: string)
    (monthKey: string)
    (request: TargetPauseRequest)
    : Async<Result<Result<PauseSource option, string>, YnabError>> =
    if not (targetsAllowed (monthKind currentMonth monthKey)) then
        async { return Ok (Error PausePastMonthText) }
    else
        // Eine Wiederholung antwortet aus dem Ereignis. Der YNAB-Anteil der Pause fehlt darin, der Client liest
        // den Monat danach ohnehin neu.
        let expected, answered =
            match request.Intent with
            | PauseTarget -> EventTypes.paused, Some PausedInBb
            | LiftPause -> EventTypes.pauseLifted, None
        let answer (recorded: EventStore.RecordedEvent) =
            async {
                if belongsTo monthKey [ expected ] (Some [ request.CategoryId ]) recorded then return Ok (Ok answered)
                else return Ok (Error ForeignCommandText)
            }
        MonthGuard.exclusive monthKey (onceFor request.CommandId answer (async {
            match! fetchBudgetMonthWithPauses loadPaused port monthKey with
            | Error e -> return Error e
            | Ok (month, pausedInBbRaw) ->
                match planPause month pausedInBbRaw request with
                | Error message -> return Ok (Error message)
                | Ok plan ->
                    match plan.Event with
                    | Some event ->
                        match! recordPause request.CommandId now monthKey plan.Category.Id plan.Category.Name event with
                        | CommandAlreadyProcessed (id, _) -> return! answerAgain id answer
                        | Recorded _ -> return Ok (Ok plan.After)
                    | None -> return Ok (Ok plan.After)
        }))

/// `setTargetPauseWith` mit den Pausen aus der Datenbank der Anwendung.
let setTargetPause (port: YnabPort) (now: DateTime) (currentMonth: string) (monthKey: string) (request: TargetPauseRequest) =
    setTargetPauseWith loadPausedCategories port now currentMonth monthKey request

// ============================================
// Target ändern und entfernen (budgeting-k8wq3, ADR 0072, ADR 0080)
// ============================================

/// Grund von Schritt 2, wenn Schritt 1 scheiterte (ADR 0080 Punkt 4, wie das Ziel einer Verschiebung nach
/// ADR 0069 Punkt 3).
[<Literal>]
let TargetStepHeldBack = "Schritt 1 fehlgeschlagen"

/// Hängt das Ergebnis eines Schritts als Best-Effort an: YNAB hat einen Schritt dieser Änderung schon
/// übernommen, ein Fehler beim Anhängen darf das nicht als Fehlschlag melden (budgeting-r3wq8). Gilt für
/// "geschrieben" nach einem `Ok` und für jedes Ergebnis nach einem geschriebenen Schritt (budgeting-g2ty9,
/// X9). Der Fehler wird mit Schritt geloggt, die Zeile steht dann ohne dieses Ergebnis in der Historie.
let private recordBestEffort (categoryId: Guid) (changeId: Guid) (step: int) (now: DateTime) (outcome: LineOutcome) : Async<unit> =
    async {
        try
            do! recordTargetChangeStepResult categoryId changeId step now outcome
        with ex ->
            eprintfn
                "Ergebnis von Schritt %d der Target-Änderung %O nicht festgehalten (YNAB hat geschrieben): %s"
                step
                changeId
                ex.Message
    }

/// Die festzuhaltende Zeile einer Target-Änderung, vom gezeigten Target aus. Alle Patches zusammen ergeben
/// den neuen Stand: `goal_frequency` ersetzt das Target und löscht das Datum (ADR 0072 Punkt 4), ein
/// folgender Patch setzt es wieder.
let private targetChangeOf
    (now: DateTime)
    (monthKey: string)
    (category: YnabMonthCategory)
    (shown: Target option)
    (kind: TargetChangeKind)
    (patches: TargetPatch list)
    : TargetChange =
    let oldGoal = shown |> Option.map (fun t -> toMilliunits t.Goal)
    let oldDate = shown |> Option.bind (fun t -> t.Date)
    let apply (goal: int64 option, date: string option) (patch: TargetPatch) =
        let date = if patch.Frequency.IsSome then None else date
        let goal =
            match patch.GoalTarget with
            | Keep -> goal
            | SetTo value -> Some value
            | Clear -> None
        let date =
            match patch.TargetDate with
            | Keep -> date
            | SetTo value -> Some value
            | Clear -> None
        goal, date
    let newGoal, newDate =
        match kind with
        | TargetRemoved -> None, None
        | TargetEdited
        | TargetCreated _
        | TargetKindChanged _ -> patches |> List.fold apply (oldGoal, oldDate)
    { OccurredAt = now
      Month = monthKey
      CategoryId = category.Id
      CategoryName = category.Name.Trim()
      Kind = kind
      OldGoalTarget = oldGoal
      NewGoalTarget = newGoal
      OldDate = oldDate
      NewDate = newDate }

/// Wie die Schritte einer Target-Änderung ausgingen.
type private StepsOutcome =
    /// Alle geschrieben, mit YNABs Stand danach.
    | AllWritten of Target option
    /// Schritt 1 scheiterte, mit YNABs Grund.
    | FirstFailed of reason: string
    /// Schritt 1 geschrieben (Stand danach), ein späterer nicht: YNAB lehnte ihn ab, mit Grund. Trägt den
    /// nicht geschriebenen Patch, aus ihm kommt das Datum der Meldung.
    | LaterNotWritten of after: Target option * patch: TargetPatch * reason: string
    /// Schritt 1 geschrieben, YNABs Antwort passt aber nicht zum Wunsch: der spätere Patch ging nicht
    /// hinaus (`continueAfter`), mit dem Grund (`heldBackCause`).
    | LaterHeldBack of after: Target option * patch: TargetPatch * cause: TargetHeldBackCause

/// Sendet die festgehaltenen Schritte der Reihe nach (ADR 0080 Punkt 4), je Schritt ein Ergebnis. Ein
/// Schritt nach dem ersten geht nur hinaus, wenn der vorige geschrieben ist und YNABs Antwort zu ihm passt
/// (`continueAfter`). Sonst wird er ungesendet als fehlgeschlagen festgehalten. Ab einem geschriebenen
/// Schritt ist jedes Ergebnis Best-Effort (`recordBestEffort`): das Teilergebnis kommt auch zurück, wenn das
/// Anhängen scheitert (budgeting-g2ty9, X9).
let private sendSteps (port: YnabPort) (now: DateTime) (categoryId: Guid) (changeId: Guid) (patches: TargetPatch list) : Async<StepsOutcome> =
    let numbered (first: int) (rest: TargetPatch list) = rest |> List.mapi (fun i _ -> first + i)
    let rec go (step: int) (previous: (TargetPatch * Target option) option) (remaining: TargetPatch list) =
        async {
            match remaining, previous with
            | [], Some (_, after) -> return AllWritten after
            | [], None -> return AllWritten None
            | patch :: rest, _ ->
                let heldBack =
                    match previous with
                    | Some (before, after) when not (continueAfter after before) ->
                        before.Frequency |> Option.map (heldBackCause after)
                    | _ -> None
                match heldBack, previous with
                | Some cause, Some (_, afterPrevious) ->
                    let reason = targetHeldBackReasonText cause
                    for s in step :: numbered (step + 1) rest do
                        do! recordBestEffort categoryId changeId s now (Failed reason)
                    return LaterHeldBack (afterPrevious, patch, cause)
                | _ ->
                    match! port.SetTarget categoryId patch with
                    | Ok after ->
                        do! recordBestEffort categoryId changeId step now Written
                        return! go (step + 1) (Some (patch, after)) rest
                    | Error e ->
                        let reason = ynabErrorText e
                        match previous with
                        | None ->
                            // Noch nichts geschrieben: ein Fehler beim Anhängen geht als Ausnahme hinaus.
                            do! recordTargetChangeStepResult categoryId changeId step now (Failed reason)
                            for later in numbered (step + 1) rest do
                                do! recordTargetChangeStepResult categoryId changeId later now (Failed TargetStepHeldBack)
                            return FirstFailed reason
                        | Some (_, afterPrevious) ->
                            do! recordBestEffort categoryId changeId step now (Failed reason)
                            for later in numbered (step + 1) rest do
                                do! recordBestEffort categoryId changeId later now (Failed TargetStepHeldBack)
                            return LaterNotWritten (afterPrevious, patch, reason)
        }
    go 1 None patches

/// Das Datum eines zurückgehaltenen oder abgelehnten Patches für die Meldung. Ein späterer Schritt trägt
/// immer ein Datum (`planTargetWrite`); fehlt es doch, steht "ohne Datum".
let private patchDateText (patch: TargetPatch) : string =
    match patch.TargetDate with
    | SetTo date -> date
    | Keep
    | Clear -> "ohne Datum"

/// Target anlegen, ändern, die Art wechseln oder entfernen (ADR 0080, budgeting-xp46a). Außerhalb des
/// laufenden Monats und wenn die Planung gegen den gezeigten Stand scheitert (Anlegen ohne Rhythmus) wird
/// vor allem anderen abgelehnt, ohne YNAB zu lesen. Sonst unter der Sperre je Kategorie: den Monat frisch
/// lesen und das Target suchen. Weicht es vom gezeigten ab, gewinnt YNAB: nichts gesendet, nichts
/// festgehalten (`TargetChangedInYnab`). Steht YNAB schon auf dem Wunsch, ebenso nichts. Sonst die Änderung
/// mit allen Schritten in einer Transaktion festhalten, dann der Reihe nach senden (`sendSteps`). Kein
/// Erneut-Senden: scheitert Schritt 1, kommt ein innerer `Error` mit YNABs Meldung; scheitert das Datum in
/// Schritt 2, kommt `TargetPartlyWritten` mit dem Stand nach Schritt 1, ein erneutes Speichern plant dann nur
/// das Datum. Außen `Error`: der Monat war nicht lesbar. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let changeTarget
    (port: YnabPort)
    (now: DateTime)
    (currentMonth: string)
    (monthKey: string)
    (request: TargetChangeRequest)
    : Async<Result<Result<TargetChangeResult, string>, YnabError>> =
    if not (targetEditAllowed (monthKind currentMonth monthKey)) then
        async { return Ok (Error (targetMonthRefusalText request)) }
    else
        match planTargetWrite request.Shown request.Wanted with
        | Error message -> async { return Ok (Error message) }
        | Ok patches ->
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
                        match patches with
                        | [] -> return Ok (Ok (TargetWritten category.Target))
                        | _ when patches |> List.exists targetPatchIsEmpty ->
                            // BB lehnt einen leeren Patch selbst ab, bevor etwas festgehalten oder
                            // gesendet wird; sonst käme er als "YNAB hat abgelehnt" zurück.
                            return Ok (Error EmptyTargetPatchText)
                        | first :: _ ->
                            let kind = targetChangeKindOf request.Shown request.Wanted first
                            let change = targetChangeOf now monthKey category request.Shown kind patches
                            let! changeId = recordTargetChange change
                            match! sendSteps port now category.Id changeId patches with
                            | AllWritten after -> return Ok (Ok (TargetWritten after))
                            | FirstFailed reason -> return Ok (Error reason)
                            | LaterNotWritten (after, patch, reason) ->
                                let message = targetPartlyText kind (patchDateText patch) (DateRefused reason)
                                return Ok (Ok (TargetPartlyWritten (after, message)))
                            | LaterHeldBack (after, patch, cause) ->
                                let message = targetPartlyText kind (patchDateText patch) (DateHeldBack cause)
                                return Ok (Ok (TargetPartlyWritten (after, message)))
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
///
/// Liest und schreibt über `connect`.
let retryFailedWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (applicationId: Guid)
    : Async<Result<ApplyReport, YnabError>> =
    async {
        match! loadStates connect applicationId with
        | Error e -> return Error e
        | Ok [] -> return Error (YnabError.RefusedByBb "Targets-Anwendung nicht gefunden")
        | Ok (first :: _) ->
            let monthKey = first.Movement.Month
            return!
                MonthGuard.exclusive monthKey (async {
                    match! port.FetchMonth monthKey with
                    | Error e -> return Error e
                    | Ok live ->
                        match! loadStates connect applicationId with
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
                                    // Das Ziel gehört YNAB: die Quelle wird ungesendet zurückgezogen.
                                    | Some YnabWins -> Some Withdrawn
                                    | None -> Some (WriteBackFailed SourceHeldBack)
                                    | Some _ -> None
                                | _ -> None
                            let rateLimited = ref false
                            let retryLine (m: Movement) =
                                async {
                                    let close outcome =
                                        async {
                                            do! recordWriteBackWith connect m now outcome
                                            return lineOutcomeOf outcome
                                        }
                                    match decisionFor m with
                                    | None -> return! close (WriteBackFailed "Kategorie in YNAB nicht gefunden")
                                    | Some AlreadyAtTarget -> return! close WrittenBack
                                    | Some YnabWins -> return! close SupersededInYnab
                                    | Some SendTarget ->
                                        match sourceBlocked with
                                        | Some outcome -> return! close outcome
                                        | None when rateLimited.Value -> return! close (WriteBackFailed "Rate-Limit")
                                        | None ->
                                            match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                                            | Ok () -> return! close WrittenBack
                                            | Error e ->
                                                if isRateLimit e then rateLimited.Value <- true
                                                return! close (WriteBackFailed (ynabErrorText e))
                                }
                            // Der Stand der Vorgänger-Zeile nach diesem Lauf.
                            let previous = ref None
                            for s in states do
                                if not (isOpenLine s.Latest) then
                                    previous.Value <- s.Latest
                                else
                                    match (if inOrder then previous.Value else None) with
                                    | Some ChangedInYnab ->
                                        // Die Quelle ist verloren: das Ziel wird ungesendet zurückgezogen.
                                        do! recordWriteBackWith connect s.Movement now Withdrawn
                                        previous.Value <- Some ChangedInYnab
                                    | Some (Failed _) -> ()
                                    | Some Written
                                    | None ->
                                        let! outcome = retryLine s.Movement
                                        previous.Value <- Some outcome
                            return! reportOf connect port applicationId monthKey
                })
    }

/// `retryFailedWith` auf der Datenbank der Anwendung.
let retryFailed (port: YnabPort) (now: DateTime) (applicationId: Guid) : Async<Result<ApplyReport, YnabError>> =
    retryFailedWith global.Persistence.getConnection port now applicationId
