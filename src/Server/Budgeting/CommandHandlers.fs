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
open Server.Budgeting.Assignments

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
    let private waiting = ConcurrentDictionary<string, int>()

    /// Wie viele Abläufe gerade an der Sperre des Monats stehen. Damit belegt ein Test, dass ein Ablauf wartet,
    /// ohne eine feste Zeit abzuwarten.
    let waitingFor (month: string) : int =
        match waiting.TryGetValue month with
        | true, count -> count
        | _ -> 0

    let exclusive (month: string) (work: Async<'a>) : Async<'a> =
        async {
            let gate = gates.GetOrAdd(month, fun _ -> new SemaphoreSlim(1, 1))
            waiting.AddOrUpdate(month, 1, fun _ count -> count + 1) |> ignore
            do! gate.WaitAsync() |> Async.AwaitTask
            waiting.AddOrUpdate(month, 0, fun _ count -> count - 1) |> ignore
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
// Beobachten: Übernahme und Echo (budgeting-dgrp7, ADR 0084 Punkt 3 und 5)
// ============================================

/// Der unter der Monatssperre gelesene YNAB-Monat und BBs Faltung danach.
type ObservedMonth = {
    Raw: YnabMonth
    Fold: MonthFold
}

/// Warum `observeMonth` nichts lieferte.
type ObserveFailure =
    /// YNAB war nicht lesbar.
    | FetchFailed of YnabError
    /// YNAB war lesbar, Übernahme und Beobachtungen ließen sich nicht festhalten (Stream nicht lesbar oder
    /// Anhängen gescheitert). Trägt den rohen Monat, das Lesen liefert ihn trotzdem.
    | NotRecorded of raw: YnabMonth * reason: string

/// Die Meldung an den Aufrufer, wenn Übernahme und Beobachtungen nicht festgehalten sind. Die Ursache bleibt
/// im Server-Log.
[<Literal>]
let ObservationNotRecordedText = "Abgleich mit YNAB nicht festgehalten"

/// Die Meldung an den Aufrufer der Vorschau, wenn der Monats-Stream nicht lesbar ist. Die Ursache bleibt im
/// Server-Log.
[<Literal>]
let MonthStreamUnreadableText = "Monats-Stream nicht lesbar"

/// Liest den Monats-Stream über eine Verbindung aus `connect` und lässt `work` auf derselben Verbindung mit der
/// Monats-Id und den Ereignissen arbeiten. Jeder Fehler (Monat ungültig, Verbindung, Lesen, `work`) kommt als
/// `Error` mit der Ursache.
let private onMonthStream
    (connect: unit -> SqliteConnection)
    (monthKey: string)
    (work: SqliteConnection -> string -> (Guid * BudgetEvent) list -> 'a)
    : Result<'a, string> =
    try
        match monthIdOf monthKey with
        | Error message -> Error message
        | Ok monthId ->
            use conn = connect ()
            loadMonthEventsOn conn monthKey |> Result.map (work conn monthId)
    with ex ->
        Error ex.Message

/// Hält auf dem gerade gelesenen Monat fest, was `Assignments.observe` ergibt: Übernahme, falls noch keine im
/// Stream liegt, und die Echo-Regel, in einem Append ohne Befehls-Id. `now` ist der Zeitpunkt (UTC), die
/// Übernahme wird nicht zurückdatiert.
let private recordObservationWith
    (connect: unit -> SqliteConnection)
    (now: DateTime)
    (monthKey: string)
    (raw: YnabMonth)
    : Result<MonthFold, string> =
    onMonthStream connect monthKey (fun conn monthId events ->
        let added, fold = observe monthId events raw
        appendMonthEventsOn conn now monthKey added
        fold)

/// `observeMonth` ohne Sperre, für Abläufe, die sie schon halten: ein `FetchMonth`, dann festhalten.
let private observeUnguarded
    (connect: unit -> SqliteConnection)
    (fetchMonth: string -> Async<YnabResult<YnabMonth>>)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<ObservedMonth, ObserveFailure>> =
    async {
        match! fetchMonth monthKey with
        | Error e -> return Error (FetchFailed e)
        | Ok raw ->
            match recordObservationWith connect now monthKey raw with
            | Ok fold -> return Ok { Raw = raw; Fold = fold }
            | Error reason ->
                logReadFailure $"Übernahme und Beobachtung {monthKey} festhalten" reason
                return Error (NotRecorded (raw, reason))
    }

/// Beobachtet einen Monat (ADR 0084 Punkt 3, Nachtrag): unter `MonthGuard` ein `FetchMonth`, Übernahme falls
/// nötig, Echo-Regel, alles in einem Append ohne Befehls-Id. Verglichen wird so nur ein Stand, der unter der
/// Sperre gelesen wurde. Liefert den rohen YNAB-Monat und die Faltung danach. Liest und schreibt über `connect`.
let observeMonthWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<ObservedMonth, ObserveFailure>> =
    MonthGuard.exclusive monthKey (observeUnguarded connect port.FetchMonth now monthKey)

/// `observeMonthWith` auf der Datenbank der Anwendung.
let observeMonth (port: YnabPort) (now: DateTime) (monthKey: string) : Async<Result<ObservedMonth, ObserveFailure>> =
    observeMonthWith global.Persistence.getConnection port now monthKey

/// Was `ensureTakenOver` vorfand.
type EnsuredTakeover =
    /// Im Stream lag schon eine Übernahme, YNAB wurde nicht gefragt.
    | AlreadyTakenOver
    /// Der Monat wurde gerade aus YNAB übernommen.
    | TakenOverNow

/// Übernimmt einen Monat, falls noch keine Übernahme im Stream liegt (budgeting-dkjc5, ADR 0084 Punkt 5): unter
/// `MonthGuard` zuerst gezielt nach einer Übernahme im Stream fragen (`queryMonthTakenOver`, ohne die Ereignisse
/// zu dekodieren: ein nicht lesbares Ereignis blockiert die Prüfung nicht), nur ohne Übernahme ein `fetchMonth`
/// und dann wie `observeMonth` festhalten. Ein Monat, den vorher ein Lesen oder ein Befehl übernommen hat, kostet so keinen YNAB-Aufruf und
/// bekommt kein zweites Übernahme-Ereignis. BBs eigene Daten nicht lesbar oder nicht festgehalten:
/// `LocalReadFailed`, die Ursache steht im Server-Log.
let ensureTakenOverWith
    (connect: unit -> SqliteConnection)
    (fetchMonth: string -> Async<YnabResult<YnabMonth>>)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<EnsuredTakeover, YnabError>> =
    MonthGuard.exclusive monthKey (async {
        let takenOver =
            try
                use conn = connect ()
                queryMonthTakenOver conn monthKey
            with ex ->
                Error ex.Message
        match takenOver with
        | Error reason ->
            logReadFailure $"Übernahme {monthKey} prüfen" reason
            return Error (YnabError.LocalReadFailed ObservationNotRecordedText)
        | Ok true -> return Ok AlreadyTakenOver
        | Ok false ->
            match! observeUnguarded connect fetchMonth now monthKey with
            | Ok _ -> return Ok TakenOverNow
            | Error (FetchFailed e) -> return Error e
            | Error (NotRecorded _) -> return Error (YnabError.LocalReadFailed ObservationNotRecordedText)
    })

/// `ensureTakenOverWith` auf der Datenbank der Anwendung.
let ensureTakenOver
    (fetchMonth: string -> Async<YnabResult<YnabMonth>>)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<EnsuredTakeover, YnabError>> =
    ensureTakenOverWith global.Persistence.getConnection fetchMonth now monthKey

/// Der Monat, auf dem ein Befehl plant (ADR 0084, Nachtrag): unter der schon gehaltenen Sperre beobachten,
/// dann die BB-Pausen lesen und beides zum Monat mit BBs Soll zusammenführen. Scheitert das Lesen von YNAB,
/// die Übernahme, die Beobachtung oder das Lesen der Pausen, kommt außen ein `Error`, und der Befehl entscheidet
/// nichts. Neben dem Monat kommen die gespeicherten BB-Pausen (auch ohne Target, `planPause`) und die Faltung.
let private preparedMonth
    (connect: unit -> SqliteConnection)
    (loadPaused: string -> Async<Result<Set<Guid>, string>>)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<BudgetMonth * Set<Guid> * MonthFold, YnabError>> =
    async {
        match! observeUnguarded connect port.FetchMonth now monthKey with
        | Error (FetchFailed e) -> return Error e
        | Error (NotRecorded _) -> return Error (YnabError.LocalReadFailed ObservationNotRecordedText)
        | Ok observed ->
            match! loadPaused monthKey with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok paused ->
                return Ok (toBudgetMonthWith (Some observed.Fold.Categories) paused observed.Raw, paused, observed.Fold)
    }

// ============================================
// Vorschau
// ============================================

let private planOf (month: BudgetMonth) : Result<TargetPreview, YnabError> =
    match planTargetApplication month with
    | Ok preview -> Ok preview
    // BB lehnt ab, nicht YNAB (budgeting-b5n8q).
    | Error message -> Error (YnabError.RefusedByBb message)

/// Die Vorschau: liest den Monat unter der Sperre und plant auf BBs Soll. Übernahme und Echo-Regel laufen nur im
/// Speicher; die Vorschau schreibt weder nach YNAB noch in die Datenbank. Liest über `connect`.
let previewWith (connect: unit -> SqliteConnection) (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    MonthGuard.exclusive monthKey (async {
        match! port.FetchMonth monthKey with
        | Error e -> return Error e
        | Ok raw ->
            match onMonthStream connect monthKey (fun _ monthId events -> observe monthId events raw |> snd) with
            | Error reason ->
                logReadFailure $"Vorschau {monthKey}: Monats-Stream lesen" reason
                return Error (YnabError.LocalReadFailed MonthStreamUnreadableText)
            | Ok fold ->
                match! loadPausedCategoriesWith connect monthKey with
                | Error message -> return Error (YnabError.LocalReadFailed message)
                | Ok paused -> return planOf (toBudgetMonthWith (Some fold.Categories) paused raw)
    })

/// `previewWith` auf der Datenbank der Anwendung.
let preview (port: YnabPort) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    previewWith global.Persistence.getConnection port monthKey

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
    (connect: unit -> SqliteConnection)
    (commandId: Guid option)
    (answer: EventStore.RecordedEvent -> Async<'r>)
    (fresh: Async<'r>)
    : Async<'r> =
    async {
        match commandId with
        | None -> return! fresh
        | Some id ->
            match! findCommand connect id with
            | Some recorded -> return! answer recorded
            | None -> return! fresh
    }

/// Das Anhängen meldete die Befehls-Id trotz Vorabprüfung als festgehalten (Netz der Transaktion, etwa
/// dieselbe Id in zwei Monaten): die Antwort auf demselben Weg aus dem Stream. Fehlt ihr Ereignis, sind
/// `processed_commands` und `events` uneins.
let private answerAgain
    (connect: unit -> SqliteConnection)
    (commandId: Guid)
    (answer: EventStore.RecordedEvent -> Async<'r>)
    : Async<'r> =
    async {
        match! findCommand connect commandId with
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
    (connect: unit -> SqliteConnection)
    (monthKey: string)
    (expectedTypes: string list)
    (expectedCategories: Guid list option)
    (recorded: EventStore.RecordedEvent)
    : Async<Result<ApplyReport, YnabError> option> =
    async {
        if belongsTo monthKey expectedTypes expectedCategories recorded then
            let! report = reportWith connect recorded.EventId monthKey None
            return Some report
        else
            return None
    }

/// Die Antwort auf eine Wiederholung von Verschieben und Zuweisen: der Bericht der festgehaltenen
/// Entscheidung, eine fremde Id als innerer `Error`.
let private decisionAnswer
    (connect: unit -> SqliteConnection)
    (monthKey: string)
    (expectedTypes: string list)
    (expectedCategories: Guid list)
    (recorded: EventStore.RecordedEvent)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    async {
        match! storedReport connect monthKey expectedTypes (Some expectedCategories) recorded with
        | Some report -> return report |> Result.map Ok
        | None -> return Ok (Error ForeignCommandText)
    }

/// Sendet die Zeilen nacheinander. Ein Fehler schließt die Zeile als fehlgeschlagen ab, die
/// übrigen werden trotzdem gesendet. Bei 429 bricht der Lauf ab: der Rest wird als
/// fehlgeschlagen mit Grund "Rate-Limit" festgehalten, schon Geschriebenes bleibt unberührt.
let private sendAll
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (movements: Movement list)
    : Async<unit> =
    async {
        let rateLimited = ref false
        for m in movements do
            if rateLimited.Value then
                do! recordWriteBackWith connect m now (WriteBackFailed "Rate-Limit")
            else
                match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                | Ok () -> do! recordWriteBackWith connect m now WrittenBack
                | Error e ->
                    if isRateLimit e then rateLimited.Value <- true
                    do! recordWriteBackWith connect m now (WriteBackFailed (ynabErrorText e))
    }

// ============================================
// Übernehmen
// ============================================

/// Übernehmen. Unter der Monats-Sperre: den Monat neu lesen und mit der gezeigten Vorschau
/// vergleichen. Weicht etwas ab, wird nichts geschrieben und die frische Vorschau
/// zurückgegeben. Sonst zuerst alle Bewegungen festhalten, dann die PATCHes nacheinander
/// senden (ADR 0050/0055). Danach liest der Bericht Zu verteilen erneut.
let applyWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (shown: TargetPreview)
    : Async<Result<ApplyResult, YnabError>> =
    // Eine fremde Id kommt hier außen als `RefusedByBb`: `ApplyResult` hat keinen Fall für eine Ablehnung, BB
    // lehnt in diesem Ablauf wie bei `plan` über den äußeren Fehler ab.
    let answer (recorded: EventStore.RecordedEvent) =
        async {
            match! storedReport connect monthKey [ EventTypes.decisionOf TargetsApplied ] None recorded with
            | Some report -> return report |> Result.map Applied
            | None -> return Error (YnabError.RefusedByBb ForeignCommandText)
        }
    MonthGuard.exclusive monthKey (onceFor connect shown.CommandId answer (async {
        match! preparedMonth connect (loadPausedCategoriesWith connect) port now monthKey with
        | Error e -> return Error e
        | Ok (month, _, fold) ->
            match planOf month with
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
                let withdrawn = withdrawalsFor fold (lines |> List.map (fun l -> l.CategoryId))
                match! recordDecisionWith connect shown.CommandId now fresh.Month TargetsApplied lines skipped withdrawn with
                | CommandAlreadyProcessed (id, _) -> return! answerAgain connect id answer
                | Recorded movements ->
                    do! sendAll connect port now fresh.Month movements
                    let applicationId = (List.head movements).ApplicationId
                    let! report = reportOf connect port applicationId fresh.Month
                    return report |> Result.map Applied
    }))

/// `applyWith` auf der Datenbank der Anwendung.
let apply (port: YnabPort) (now: DateTime) (monthKey: string) (shown: TargetPreview) : Async<Result<ApplyResult, YnabError>> =
    applyWith global.Persistence.getConnection port now monthKey shown

// ============================================
// Verschieben (budgeting-afd8h, ADR 0069)
// ============================================

/// Grund der Ziel-Zeile, wenn die Quelle nicht geschrieben wurde.
[<Literal>]
let TargetHeldBack = "Quelle nicht geschrieben, Ziel nicht gesendet"

/// Grund der Quell-Zeile, wenn das Ziel im Monat fehlt.
[<Literal>]
let SourceHeldBack = SourceHeldBackText

/// Sendet die Zeilen nacheinander und hält beim ersten Fehler an: stiege das Ziel, ohne dass die
/// Quelle sinkt, käme das Geld still aus Zu verteilen (ADR 0069 Punkt 3). Die übrigen Zeilen
/// stehen danach als fehlgeschlagen da. Anders als `sendAll`, wo jede Zeile für sich steht.
let private sendInOrder
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (movements: Movement list)
    : Async<unit> =
    async {
        let stopped = ref false
        for m in movements do
            if stopped.Value then
                do! recordWriteBackWith connect m now (WriteBackFailed TargetHeldBack)
            else
                match! port.SetAssigned monthKey m.CategoryId m.NewAssigned with
                | Ok () -> do! recordWriteBackWith connect m now WrittenBack
                | Error e ->
                    stopped.Value <- true
                    do! recordWriteBackWith connect m now (WriteBackFailed (ynabErrorText e))
    }

/// Hält den Plan fest (eine Transaktion, gemeinsame `ApplicationId`) und sendet nacheinander. Der
/// Bericht liest Zu verteilen nicht neu, die Seite lädt den Monat ohnehin.
let private recordAndSend
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (commandId: Guid option)
    (answer: EventStore.RecordedEvent -> Async<Result<Result<ApplyReport, string>, YnabError>>)
    (fold: MonthFold)
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
        let withdrawn = withdrawalsFor fold (lines |> List.map (fun l -> l.CategoryId))
        match! recordDecisionWith connect commandId now monthKey plan.Origin lines [] withdrawn with
        | CommandAlreadyProcessed (id, _) -> return! answerAgain connect id answer
        | Recorded movements ->
            do! sendInOrder connect port now monthKey movements
            let! report = reportWith connect (List.head movements).ApplicationId monthKey None
            return report |> Result.map Ok
    }

/// Verschieben oder Zuweisen von Zu verteilen. Unter der Monats-Sperre: den Monat frisch lesen und relativ dazu
/// planen, die Bewegungen festhalten (eine Transaktion, gemeinsame `ApplicationId`), dann
/// nacheinander senden. Außen `Error`: der Monat war nicht lesbar. Innen `Error`: der Auftrag ist
/// abgelehnt. In beiden Fällen ist nichts geschrieben. Der Bericht liest Zu verteilen nicht neu,
/// weil die Seite den Monat ohnehin neu lädt und jeder Aufruf das Sheet langsamer machte.
let moveMoneyWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (request: MoveRequest)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    let answer = decisionAnswer connect monthKey (moveEventTypes request) (moveCategories request)
    MonthGuard.exclusive monthKey (onceFor connect request.CommandId answer (async {
        match! preparedMonth connect (loadPausedCategoriesWith connect) port now monthKey with
        | Error e -> return Error e
        | Ok (month, _, fold) ->
            match planMove month request with
            | Error message -> return Ok (Error message)
            | Ok plan -> return! recordAndSend connect port now monthKey request.CommandId answer fold plan
    }))

/// `moveMoneyWith` auf der Datenbank der Anwendung.
let moveMoney (port: YnabPort) (now: DateTime) (monthKey: string) (request: MoveRequest) =
    moveMoneyWith global.Persistence.getConnection port now monthKey request

/// Zuweisen (ADR 0078): setzt das Zugewiesen absolut. Wie `moveMoney` unter der Monats-Sperre und
/// mit denselben Fehlerwegen. Der gelesene Wert ist der alte, ein Vergleich mit dem gezeigten Stand
/// findet nicht statt. Steht YNAB schon auf dem Wert, entsteht nichts: Der Bericht hat keine Zeilen,
/// und `Guid.Empty` sagt, dass es keine Anwendung gibt, die man erneut senden könnte.
let assignMoneyWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (request: AssignRequest)
    : Async<Result<Result<ApplyReport, string>, YnabError>> =
    let answer = decisionAnswer connect monthKey [ EventTypes.decisionOf MoneyAssigned ] [ request.CategoryId ]
    MonthGuard.exclusive monthKey (onceFor connect request.CommandId answer (async {
        match! preparedMonth connect (loadPausedCategoriesWith connect) port now monthKey with
        | Error e -> return Error e
        | Ok (month, _, fold) ->
            match planAssign month request with
            | Error message -> return Ok (Error message)
            | Ok None ->
                let unchanged =
                    { ApplicationId = string Guid.Empty
                      Month = monthKey
                      Lines = []
                      ActualToBeBudgeted = None }
                return Ok (Ok unchanged)
            | Ok (Some plan) -> return! recordAndSend connect port now monthKey request.CommandId answer fold plan
    }))

/// `assignMoneyWith` auf der Datenbank der Anwendung.
let assignMoney (port: YnabPort) (now: DateTime) (monthKey: string) (request: AssignRequest) =
    assignMoneyWith global.Persistence.getConnection port now monthKey request

// ============================================
// Target pausieren (budgeting-t6p8z, ADR 0073, ADR 0079)
// ============================================

/// Target pausieren oder die Pause aufheben. Ein vergangener Monat wird vor allem anderen abgelehnt, ohne
/// YNAB zu lesen. Sonst unter der Sperre je Monat wie Targets anwenden: den Monat frisch lesen, planen
/// (`planPause`) und höchstens ein Ereignis anhängen. Nach YNAB geht nichts (ADR 0073 Punkt 5), der Port
/// wird nur gelesen. Außen `Error`: der Monat war nicht lesbar. Innen `Error`: abgelehnt. In beiden
/// Fällen ist nichts geschrieben. `currentMonth` ist der laufende Monat ("YYYY-MM-01").
let setTargetPauseWith
    (connect: unit -> SqliteConnection)
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
        MonthGuard.exclusive monthKey (onceFor connect request.CommandId answer (async {
            match! preparedMonth connect loadPaused port now monthKey with
            | Error e -> return Error e
            | Ok (month, pausedInBbRaw, _) ->
                match planPause month pausedInBbRaw request with
                | Error message -> return Ok (Error message)
                | Ok plan ->
                    match plan.Event with
                    | Some event ->
                        match! recordPauseWith connect request.CommandId now monthKey plan.Category.Id plan.Category.Name event with
                        | CommandAlreadyProcessed (id, _) -> return! answerAgain connect id answer
                        | Recorded _ -> return Ok (Ok plan.After)
                    | None -> return Ok (Ok plan.After)
        }))

/// `setTargetPauseWith` mit den Pausen aus der Datenbank der Anwendung.
let setTargetPause (port: YnabPort) (now: DateTime) (currentMonth: string) (monthKey: string) (request: TargetPauseRequest) =
    setTargetPauseWith global.Persistence.getConnection loadPausedCategories port now currentMonth monthKey request

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

/// Sendet die offenen Zeilen einer Anwendung erneut (budgeting-dgrp7). Unter der Monatssperre wird zuerst
/// beobachtet (`observeMonth`): Steht YNAB schon auf dem Soll, schließt die Echo-Regel die Zeile als
/// geschrieben; steht es auf einem anderen Wert, gewinnt YNAB als Beobachtung. Was danach noch offen ist, plant
/// `Assignments.planRetry` und sendet das Soll absolut, ein zweites Senden ändert nichts. Offen ist eine Zeile
/// nur, solange ihre Entscheidung die offene Rückschreibung der Kategorie ist: eine Zeile, die eine spätere
/// Entscheidung ersetzt hat, sendet nichts.
///
/// Bei einer Verschiebung bleibt die Reihenfolge verbindlich (ADR 0069 Punkt 3): solange die Quelle nicht
/// geschrieben ist, geht das Ziel nicht hinaus. Scheitert sie wieder, bleibt das Ziel unberührt. Ist eine Seite
/// verloren (in YNAB ersetzt oder zurückgezogen), wird die andere ungesendet zurückgezogen und ihr Soll fällt
/// auf den bestätigten Stand: ein gesendetes Ziel nähme das Geld sonst aus Zu verteilen. Kam ein PATCH der
/// Quelle an, ging nur die Antwort verloren und änderte sie sich danach, bleibt das Geld in Zu verteilen und
/// Roman verschiebt es von Hand. Fehlt das Ziel im Monat, bleibt die Quelle ungesendet fehlgeschlagen.
///
/// Bei 429 bricht der Lauf ab, die übrigen zu sendenden Zeilen schließen fehlgeschlagen mit "Rate-Limit".
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
                    match! observeUnguarded connect port.FetchMonth now monthKey with
                    | Error (FetchFailed e) -> return Error e
                    | Error (NotRecorded _) -> return Error (YnabError.LocalReadFailed ObservationNotRecordedText)
                    | Ok observed ->
                        match! loadStates connect applicationId with
                        | Error e -> return Error e
                        | Ok states ->
                            let inYnab = observed.Raw.Categories |> List.map (fun c -> c.Id) |> Set.ofList
                            let lines =
                                states
                                |> List.map (fun s ->
                                    { CategoryId = s.Movement.CategoryId
                                      Latest = s.Latest
                                      Open = (stateOf observed.Fold s.Movement.CategoryId).Open = Some applicationId
                                      InYnab = inYnab.Contains s.Movement.CategoryId })
                            let inOrder = sendsInOrder first.Movement.Origin
                            let actions = planRetry inOrder lines
                            let stopped = ref false
                            let rateLimited = ref false
                            for s, action in List.zip states actions do
                                let close outcome = recordWriteBackWith connect s.Movement now outcome
                                match action with
                                | Leave -> ()
                                | Close outcome -> do! close outcome
                                | Send when stopped.Value -> ()
                                | Send when rateLimited.Value -> do! close (WriteBackFailed "Rate-Limit")
                                | Send ->
                                    match! port.SetAssigned monthKey s.Movement.CategoryId s.Movement.NewAssigned with
                                    | Ok () -> do! close WrittenBack
                                    | Error e ->
                                        if isRateLimit e then rateLimited.Value <- true
                                        if inOrder then stopped.Value <- true
                                        do! close (WriteBackFailed (ynabErrorText e))
                            return! reportOf connect port applicationId monthKey
                })
    }

/// `retryFailedWith` auf der Datenbank der Anwendung.
let retryFailed (port: YnabPort) (now: DateTime) (applicationId: Guid) : Async<Result<ApplyReport, YnabError>> =
    retryFailedWith global.Persistence.getConnection port now applicationId
