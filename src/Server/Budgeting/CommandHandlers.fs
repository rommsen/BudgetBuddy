/// Die Abläufe von "Targets anwenden" (Context `budgeting`, budgeting-h2zfn, ADR 0065):
/// Vorschau, Übernehmen und Fehlgeschlagene erneut senden, dazu "Verschieben" (ADR 0069) und
/// "Zuweisen" (ADR 0078), "Target pausieren" (ADR 0079) und "Target ändern" (ADR 0080, nur in BB seit
/// budgeting-eh8xe). YNAB kommt als Port herein,
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
open Server.Budgeting.TargetRules

/// Was die Abläufe von YNAB brauchen. `FetchMonth` liest den Monat roh (`YnabMonth`),
/// `SetAssigned month categoryId milliunits` setzt das Zugewiesen absolut (ein PATCH),
/// `FetchCategoryGoals` liest die Goals aller Kategorien aus `/categories`, nur für die Target-Übernahme
/// (budgeting-eh8xe). Nach YNAB geht kein Goal mehr (ADR 0084 Punkt 6).
type YnabPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    SetAssigned: string -> Guid -> int64 -> Async<YnabResult<unit>>
    FetchCategoryGoals: unit -> Async<YnabResult<YnabCategoryGoal list>>
}

/// Die Lese-Quellen der Ausgaben der Periode im Kassenbuch (budgeting-k5q2w). Beide lesen nur die Datenbank, kein
/// YNAB. Lesen eines Monats, Vorschau und Targets anwenden rechnen damit BBs Fehlbetrag (budgeting-hbhzf).
type LedgerActivityPort = {
    /// Kategorien und Monatserste: je Kategorie die Netto-Aktivität der Monate mit Buchungen, in einer Abfrage.
    LoadMonthlyActivity: Guid list -> DateTime list -> Async<Map<Guid, (DateTime * Money) list>>
    /// Der Kalendertag der ersten nicht gelöschten Buchung im Kassenbuch, ohne Buchung `None`.
    LoadFirstEntryDate: unit -> Async<DateTime option>
}

/// Das Kassenbuch der Anwendung als Quelle der Ausgaben der Periode.
let liveLedgerActivityPort: LedgerActivityPort =
    { LoadMonthlyActivity = Ledger.QueryHandlers.getMonthlyActivityByCategory
      LoadFirstEntryDate = Ledger.Persistence.loadFirstEntryDate }

/// Die Aktivität der Periodenmonate (`periodMonthsNeeded`) aller Kategorien, die sie brauchen, aus dem
/// Kassenbuch: eine Abfrage für die erste Buchung, eine für alle Kategorien. Ohne Periodenmonat kein Lesen.
/// Ein Lesefehler kommt als Ausnahme, der Aufrufer entscheidet, was er bedeutet.
let loadPeriodActivity
    (ledger: LedgerActivityPort)
    (targets: Map<Guid, Target>)
    (monthKey: string)
    : Async<Map<Guid, Map<string, Money>>> =
    async {
        match periodMonthsNeeded targets monthKey with
        | [] -> return Map.empty
        | months ->
            let ids = periodCategoriesNeeded targets monthKey
            let! first = ledger.LoadFirstEntryDate ()
            let! sums =
                ledger.LoadMonthlyActivity
                    ids
                    (months |> List.map (fun m -> DateTime.ParseExact(m, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)))
            return periodActivityFromLedger ids months first sums
    }

/// Die Meldung, wenn vor der Vorschau oder Targets anwenden das Kassenbuch nicht lesbar ist (budgeting-hbhzf).
[<Literal>]
let PeriodActivityUnreadableText =
    "Die Ausgaben der Periode aus dem Kassenbuch sind nicht lesbar. BB rechnet den Fehlbetrag deshalb nicht und weist nichts zu."

/// Die Ausgaben der Periode vor einer Planung mit Fehlbetrag (Vorschau, Targets anwenden). Scheitert das Lesen,
/// steht der Grund im Log und es kommt ein `Error` mit `PeriodActivityUnreadableText`: ohne die Ausgaben würde
/// BB zu viel zuweisen.
let private periodActivityForCommand
    (ledger: LedgerActivityPort)
    (targets: Map<Guid, Target>)
    (monthKey: string)
    : Async<Result<Map<Guid, Map<string, Money>>, YnabError>> =
    async {
        try
            let! activity = loadPeriodActivity ledger targets monthKey
            return Ok activity
        with ex ->
            logReadFailure $"Ausgaben der Periode {monthKey} aus dem Kassenbuch lesen" ex.Message
            return Error (YnabError.LocalReadFailed PeriodActivityUnreadableText)
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

/// Die Target-Übernahme läuft höchstens einmal zugleich und nie neben dem Festhalten einer Target-Änderung
/// (budgeting-eh8xe): sonst könnte eine Übernahme hinter ein gerade gesetztes Target geschrieben werden und es
/// überdecken. Ein Schlüssel für alle Kategorien, weil die Übernahme alle auf einmal schreibt. Sie hält keine
/// andere Sperre, deshalb gibt es keinen Kreis mit `MonthGuard` oder `TargetGuard`.
module internal TargetTakeoverGuard =
    let private gate = new SemaphoreSlim(1, 1)

    let exclusive (work: Async<'a>) : Async<'a> =
        async {
            do! gate.WaitAsync() |> Async.AwaitTask
            try
                return! work
            finally
                gate.Release() |> ignore
        }

// ============================================
// Target-Übernahme (budgeting-eh8xe, ADR 0084 Punkt 5 und 6)
// ============================================

/// Was eine Target-Übernahme festhielt und was nicht.
type TargetTakeoverReport = {
    TakenOver: Guid list
    NotTakenOver: NotTakenOver list
}

/// Die Meldung, wenn sich die Target-Übernahme nicht festhalten ließ. Die Ursache bleibt im Server-Log.
[<Literal>]
let TargetTakeoverNotRecordedText = "Target-Übernahme nicht festgehalten"

/// Die Kategorien, für die eine Übernahme nichts übernahm (kein Goal, DEBT, unbekannte Art, oder in `/categories`
/// nicht zu finden), für die Laufzeit des Prozesses (Review-Fund eh8xe). Ihretwegen fragt BB `/categories` nicht
/// erneut, und ein nicht übernommenes Goal steht nur einmal im Server-Log. Nach einem Neustart wird wieder gefragt.
module internal TakeoverDeclined =
    let private declined = ConcurrentDictionary<Guid, unit>()

    /// Alle gemerkten Kategorien.
    let all () : Set<Guid> = declined.Keys |> Set.ofSeq

    /// Merkt die Kategorien und liefert die, die vorher nicht gemerkt waren.
    let remember (ids: Guid seq) : Set<Guid> =
        ids |> Seq.filter (fun id -> declined.TryAdd(id, ())) |> Set.ofSeq

/// Übernimmt die Goals aus `/categories` (ein YNAB-Aufruf) als `TargetTakenOverFromYnab`, je Kategorie mit
/// übernehmbarem Goal, deren Target BB noch nicht führt, alle in einer Transaktion. Kategorien ohne übernehmbares
/// Goal merkt sich der Prozess (`TakeoverDeclined`); ein nicht übernommenes Goal (DEBT, unbekannte Art) steht im
/// Bericht und beim ersten Mal im Server-Log. Eine gelungene Übernahme mit Ereignissen schreibt eine Zeile ins
/// Log. Ein zweiter Lauf schreibt nichts. Unter `TargetTakeoverGuard`. Liest und schreibt über `connect`, `now`
/// stempelt die Ereignisse (UTC). Nicht festgehalten: `LocalWriteFailed`, die Ursache steht im Server-Log.
let takeOverTargetsWith
    (connect: unit -> SqliteConnection)
    (fetchGoals: unit -> Async<YnabResult<YnabCategoryGoal list>>)
    (now: DateTime)
    : Async<Result<TargetTakeoverReport, YnabError>> =
    TargetTakeoverGuard.exclusive (async {
        match! fetchGoals () with
        | Error e -> return Error e
        | Ok goals ->
            let recorded =
                try
                    use conn = connect ()
                    queryLedTargets conn
                    |> Result.map (fun led ->
                        let plan = planTargetTakeover led goals
                        appendTargetTakeoverOn conn now plan.Events
                        led, plan)
                with ex ->
                    Error ex.Message
            match recorded with
            | Error reason ->
                logReadFailure "Target-Übernahme festhalten" reason
                return Error (YnabError.LocalWriteFailed TargetTakeoverNotRecordedText)
            | Ok (led, plan) ->
                if not (List.isEmpty plan.Events) then
                    printfn "Target-Übernahme: %d Kategorien übernommen" plan.Events.Length
                let takenOver = plan.Events |> List.map fst |> Set.ofList
                let firstTime =
                    goals
                    |> List.map (fun g -> g.Id)
                    |> List.filter (fun id -> not (led.Contains id) && not (takenOver.Contains id))
                    |> TakeoverDeclined.remember
                for n in plan.NotTakenOver do
                    if firstTime.Contains n.CategoryId then
                        eprintfn "Target-Übernahme: %s nicht übernommen (%s)" n.CategoryName n.Reason
                return Ok { TakenOver = plan.Events |> List.map fst; NotTakenOver = plan.NotTakenOver }
    })

/// Läuft die Target-Übernahme, wenn eine Kategorie des gelesenen Monats auf sie wartet (`awaitingTakeover`):
/// nicht für einen vergangenen Monat, nicht für geführte und nicht für gemerkte Kategorien (`TakeoverDeclined`).
/// Was danach noch wartete und nicht übernommen wurde, merkt sich der Prozess, so fragt das nächste Lesen nicht
/// erneut. Vergangen heißt: vor dem Monat von `now` (UTC). Am Monatsersten bis 01:00 oder 02:00 Uhr Berliner Zeit
/// zählt der Vormonat damit noch nicht als vergangen; das kostet höchstens einen Abruf, den das Merken abfängt.
let private takeOverIfAwaited
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (raw: YnabMonth)
    : Async<Result<unit, YnabError>> =
    async {
        let led =
            try
                use conn = connect ()
                queryLedTargets conn
            with ex ->
                Error ex.Message
        match led with
        | Error reason ->
            logReadFailure "Geführte Targets lesen" reason
            return Error (YnabError.LocalReadFailed TargetRulesUnreadableText)
        | Ok led ->
            let awaiting = awaitingTakeover (Set.union led (TakeoverDeclined.all ())) (monthKeyOf now) raw
            if Set.isEmpty awaiting then
                return Ok ()
            else
                match! takeOverTargetsWith connect port.FetchCategoryGoals now with
                | Error e -> return Error e
                | Ok report ->
                    Set.difference awaiting (Set.ofList report.TakenOver) |> TakeoverDeclined.remember |> ignore
                    return Ok ()
    }

/// BBs Targets im gelesenen Monat für Lesen und Vorschau (budgeting-eh8xe): wartet eine Kategorie des Monats noch
/// auf die Übernahme, läuft sie zuerst (`takeOverIfAwaited`). Best-Effort: scheitert die Übernahme, steht der
/// Grund im Server-Log und die Regeln kommen ohne sie. Scheitert das Lesen der Regeln, kommt `Error`.
let targetRulesBestEffort
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (raw: YnabMonth)
    : Async<Result<Map<Guid, Target>, string>> =
    async {
        match! takeOverIfAwaited connect port now raw with
        | Ok () -> ()
        | Error e -> logReadFailure "Target-Übernahme" (ynabErrorText e)
        return! loadTargetRulesWith connect raw.Month
    }

/// BBs Targets im gelesenen Monat für einen Befehl: wie `targetRulesBestEffort`, nur ist eine gescheiterte
/// Übernahme ein `Error`, und der Befehl entscheidet nichts (Review-Fund eh8xe): er plante sonst ohne die Targets,
/// die die Übernahme gebracht hätte.
let private targetRulesForCommand
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (raw: YnabMonth)
    : Async<Result<Map<Guid, Target>, YnabError>> =
    async {
        match! takeOverIfAwaited connect port now raw with
        | Error e -> return Error e
        | Ok () ->
            let! rules = loadTargetRulesWith connect raw.Month
            return rules |> Result.mapError YnabError.LocalReadFailed
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
/// dann die BB-Pausen und BBs Targets lesen und alles zum Monat mit BBs Soll zusammenführen. Scheitert das Lesen
/// von YNAB, die Übernahme des Monats, die Beobachtung, die Target-Übernahme (`targetRulesForCommand`) oder das
/// Lesen der Pausen oder Targets, kommt außen ein `Error`, und der Befehl entscheidet nichts. Neben dem Monat kommen die gespeicherten BB-Pausen (auch ohne Target, `planPause`) und die Faltung.
/// `ledger` ist die Quelle der Ausgaben der Periode für BBs Fehlbetrag (Targets anwenden). `None` plant ohne
/// Fehlbetrag: Verschieben, Zuweisen und Pausieren lesen ihn nicht, ein Lesefehler im Kassenbuch hält sie nicht auf.
let private preparedMonth
    (ledger: LedgerActivityPort option)
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
                match! targetRulesForCommand connect port now observed.Raw with
                | Error e -> return Error e
                | Ok targets ->
                    let! activity =
                        match ledger with
                        | None -> async { return Ok None }
                        | Some port ->
                            async {
                                let! loaded = periodActivityForCommand port targets monthKey
                                return loaded |> Result.map Some
                            }
                    match activity with
                    | Error e -> return Error e
                    | Ok activity ->
                        let month = toBudgetMonthWith (Some observed.Fold.Categories) activity targets paused observed.Raw
                        return Ok (month, paused, observed.Fold)
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
/// Speicher; die Vorschau schreibt nicht nach YNAB und hält höchstens die einmalige Target-Übernahme fest, wenn
/// eine Kategorie des Monats noch darauf wartet (budgeting-eh8xe, Best-Effort wie das Lesen: `targetRulesBestEffort`). Liest über `connect`, `now` stempelt sie.
/// Der Fehlbetrag ist BBs, die Ausgaben der Periode kommen aus `ledger` (budgeting-hbhzf); ist es nicht lesbar,
/// kommt `PeriodActivityUnreadableText` und keine Vorschau.
let previewWithLedger
    (ledger: LedgerActivityPort)
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<TargetPreview, YnabError>> =
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
                | Ok paused ->
                    match! targetRulesBestEffort connect port now raw with
                    | Error message -> return Error (YnabError.LocalReadFailed message)
                    | Ok targets ->
                        match! periodActivityForCommand ledger targets monthKey with
                        | Error e -> return Error e
                        | Ok activity -> return planOf (toBudgetMonthWith (Some fold.Categories) (Some activity) targets paused raw)
    })

/// `previewWithLedger` mit dem Kassenbuch der Anwendung (`liveLedgerActivityPort`).
let previewWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<Result<TargetPreview, YnabError>> =
    previewWithLedger liveLedgerActivityPort connect port now monthKey

/// `previewWith` auf der Datenbank der Anwendung.
let preview (port: YnabPort) (now: DateTime) (monthKey: string) : Async<Result<TargetPreview, YnabError>> =
    previewWith global.Persistence.getConnection port now monthKey

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
                | Error err ->
                    // Der Bericht kommt trotzdem, nur ohne Zu verteilen; der Grund steht im Log (infrastructure-vp308).
                    logReadFailure "Zu verteilen für den Bericht lesen" (ynabErrorText err)
                    return None
            }
        return! reportWith connect applicationId monthKey actual
    }

// ============================================
// Befehls-Id (ADR 0084 Punkt 8, budgeting-qm5x9)
// ============================================

/// Die Ablehnung, wenn eine Befehls-Id schon zu einem anderen Auftrag festgehalten ist. Der Satz geht an den
/// Nutzer, der technische Grund steht nur im Server-Log (`foreignCommand`).
[<Literal>]
let ForeignCommandText = "Der Auftrag wurde schon anders verarbeitet. Seite neu laden und erneut speichern."

/// Meldet eine fremde Befehls-Id im Server-Log und liefert den Satz für den Nutzer (`ForeignCommandText`).
let private foreignCommand (recorded: EventStore.RecordedEvent) : string =
    eprintfn "Befehls-Id gehört zu einem anderen Auftrag: festgehalten mit Ereignis %O (%s)" recorded.EventId recorded.EventType
    ForeignCommandText

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
        | None -> return Ok (Error (foreignCommand recorded))
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
/// senden (ADR 0050/0055). Danach liest der Bericht Zu verteilen erneut. Geplant wird mit BBs Fehlbetrag, die
/// Ausgaben der Periode kommen aus `ledger` (budgeting-hbhzf); ist es nicht lesbar, wird mit
/// `PeriodActivityUnreadableText` abgelehnt: keine Entscheidung festgehalten, nichts gesendet (Übernahme und
/// Beobachtung des Monats sind wie bei jedem Befehl schon festgehalten).
let applyWithLedger
    (ledger: LedgerActivityPort)
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
            | None -> return Error (YnabError.RefusedByBb (foreignCommand recorded))
        }
    MonthGuard.exclusive monthKey (onceFor connect shown.CommandId answer (async {
        match! preparedMonth (Some ledger) connect (loadPausedCategoriesWith connect) port now monthKey with
        | Error e -> return Error e
        | Ok (month, _, fold) ->
            match planOf month |> Result.map (fun fresh -> fresh, chosenLinesIfCurrent shown fresh) with
            | Error e -> return Error e
            | Ok (fresh, None) -> return Ok (PreviewChanged fresh)
            | Ok (fresh, Some chosen) ->
                // Nur die gewählten Zeilen (budgeting-m3x7f); abgewählte werden nicht geschrieben.
                let lines =
                    chosen
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

/// `applyWithLedger` mit dem Kassenbuch der Anwendung (`liveLedgerActivityPort`).
let applyWith
    (connect: unit -> SqliteConnection)
    (port: YnabPort)
    (now: DateTime)
    (monthKey: string)
    (shown: TargetPreview)
    : Async<Result<ApplyResult, YnabError>> =
    applyWithLedger liveLedgerActivityPort connect port now monthKey shown

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
        match! preparedMonth None connect (loadPausedCategoriesWith connect) port now monthKey with
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
        match! preparedMonth None connect (loadPausedCategoriesWith connect) port now monthKey with
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
        // Eine Wiederholung antwortet aus dem Ereignis.
        let expected, answered =
            match request.Intent with
            | PauseTarget -> EventTypes.paused, Some PausedInBb
            | LiftPause -> EventTypes.pauseLifted, None
        let answer (recorded: EventStore.RecordedEvent) =
            async {
                if belongsTo monthKey [ expected ] (Some [ request.CategoryId ]) recorded then return Ok (Ok answered)
                else return Ok (Error (foreignCommand recorded))
            }
        MonthGuard.exclusive monthKey (onceFor connect request.CommandId answer (async {
            match! preparedMonth None connect loadPaused port now monthKey with
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
// Target ändern und entfernen (budgeting-k8wq3, ADR 0080, budgeting-eh8xe)
// ============================================

/// Die Antwort aus einem festgehaltenen `TargetSet` oder `TargetRemoved`: die Regel danach, `None` nach dem
/// Entfernen. Ein anderes Ereignis oder ein anderer Stream gehört nicht zum Auftrag (`ForeignCommandText`).
let private storedTargetAnswer
    (categoryId: Guid)
    (recorded: EventStore.RecordedEvent)
    : Result<TargetChangeResult, string> =
    if recorded.Context <> Context || recorded.StreamId <> streamIdOf (TargetStream categoryId) then
        Error (foreignCommand recorded)
    else
        match decodePayload recorded.EventType recorded.SchemaVersion recorded.Payload with
        | Ok (BudgetEvent.TargetSet p) -> Ok (TargetWritten (Some p.Rule))
        | Ok (BudgetEvent.TargetRemoved _) -> Ok (TargetWritten None)
        | Ok _ -> Error (foreignCommand recorded)
        | Error message -> failwith $"Ereignis {recorded.EventId} der Befehls-Id nicht lesbar: {message}"

/// Die Meldung, wenn sich eine Target-Änderung nicht festhalten ließ. Die Ursache bleibt im Server-Log.
[<Literal>]
let TargetChangeNotRecordedText = "Target nicht gespeichert: BB konnte die Änderung nicht festhalten."

/// Target anlegen, ändern, die Art wechseln oder entfernen, nur in BB (ADR 0080 mit Nachtrag ADR 0084,
/// budgeting-eh8xe). Außerhalb des laufenden Monats wird vor allem anderen abgelehnt. Sonst unter `TargetGuard`:
/// zuerst die Befehls-Id (eine Wiederholung antwortet aus dem festgehaltenen Ereignis und hängt nichts an), dann
/// BBs Stand aus dem Target-Stream falten und gegen das gezeigte Target planen (`planTargetChange`). Weicht es
/// ab, hängt nichts an und die Antwort trägt BBs Stand (`TargetChangedMeanwhile`). Sonst höchstens ein Ereignis: `TargetSet` mit der vollständigen Regel, gültig ab dem
/// laufenden Monat, oder `TargetRemoved`. YNAB wird nicht aufgerufen. Außen `Error`: BBs Stand war nicht lesbar
/// (`LocalReadFailed`) oder das Ereignis ließ sich nicht festhalten (`LocalWriteFailed`). Innen `Error`: abgelehnt. `currentMonth` ist der laufende Monat ("YYYY-MM-01"), `now` stempelt das Ereignis.
let changeTargetWith
    (connect: unit -> SqliteConnection)
    (now: DateTime)
    (currentMonth: string)
    (monthKey: string)
    (request: TargetChangeRequest)
    : Async<Result<Result<TargetChangeResult, string>, YnabError>> =
    match monthIdOf monthKey with
    | Ok monthId when targetEditAllowed (monthKind currentMonth monthKey) ->
        let answer (recorded: EventStore.RecordedEvent) = async { return Ok (storedTargetAnswer request.CategoryId recorded) }
        TargetGuard.exclusive request.CategoryId (onceFor connect request.CommandId answer (TargetTakeoverGuard.exclusive (async {
            let stream =
                try
                    use conn = connect ()
                    loadTargetStreamOn conn request.CategoryId
                with ex ->
                    Error ex.Message
            match stream with
            | Error reason ->
                logReadFailure "Target-Stream lesen" reason
                return Error (YnabError.LocalReadFailed TargetRulesUnreadableText)
            | Ok events ->
                // Hält das Ereignis fest; scheitert das Anhängen, kommt ein sauberer Fehler, nichts ist angehängt.
                let record (event: BudgetEvent) (after: Target option) =
                    async {
                        let recorded =
                            try
                                use conn = connect ()
                                Ok (recordTargetChangeOn conn request.CommandId now request.CategoryId event)
                            with ex ->
                                Error ex.Message
                        match recorded with
                        | Error reason ->
                            logReadFailure "Target-Änderung festhalten" reason
                            return Error (YnabError.LocalWriteFailed TargetChangeNotRecordedText)
                        | Ok (CommandAlreadyProcessed (id, _)) -> return! answerAgain connect id answer
                        | Ok (Recorded _) -> return Ok (Ok (TargetWritten after))
                    }
                match planTargetChange (monthKeyOfId monthId) (ruleAt monthId events) request with
                | Error message -> return Ok (Error message)
                | Ok (TargetUnchanged current) -> return Ok (Ok (TargetWritten current))
                | Ok (ShownOutdated current) -> return Ok (Ok (TargetChangedMeanwhile current))
                | Ok (SetRule rule) -> return! record (BudgetEvent.TargetSet { Rule = rule; EffectiveFrom = monthId }) (Some rule)
                | Ok RemoveRule -> return! record (BudgetEvent.TargetRemoved { EffectiveFrom = monthId }) None
        })))
    | _ -> async { return Ok (Error (targetMonthRefusalText request)) }

/// `changeTargetWith` auf der Datenbank der Anwendung.
let changeTarget (now: DateTime) (currentMonth: string) (monthKey: string) (request: TargetChangeRequest) =
    changeTargetWith global.Persistence.getConnection now currentMonth monthKey request

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

// ============================================
// Leiste Monatserster ausblenden (budgeting-m3x7f)
// ============================================

/// Die Meldung, wenn sich das Ausblenden nicht speichern ließ. Die Ursache steht im Server-Log.
[<Literal>]
let MonthStartNotHiddenText = "Ausblenden nicht gespeichert."

/// Blendet die Leiste Monatserster für den Monat ("YYYY-MM-01", schon geprüft) aus. Zustand, kein Ereignis (ADR 0083).
/// Schreibt über `connect`, `now` stempelt den Zeitpunkt (UTC). Ein zweites Ausblenden ändert nichts. Scheitert das
/// Schreiben, steht die Ursache im Server-Log und der Aufrufer bekommt `MonthStartNotHiddenText`.
let hideMonthStartWith (connect: unit -> SqliteConnection) (now: DateTime) (month: string) : Async<Result<unit, string>> =
    async {
        try
            use conn = connect ()
            hideMonthStartOn conn month now
            return Ok ()
        with ex ->
            eprintfn "Leiste Monatserster: Ausblenden für %s nicht gespeichert (%s)" month ex.Message
            return Error MonthStartNotHiddenText
    }
