/// Abfragen des Budgets (budgeting-g8s3w, ADR 0074). YNAB kommt als Port herein, die Befehle in
/// `CommandHandlers` brauchen den Rahmen nicht und bleiben davon unberührt.
module Server.Budgeting.QueryHandlers

open System
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain

/// Die Ursache eines Lesefehlers bleibt im Server-Log, der Client bekommt nur den Text der Meldung.
let private logReadFailure (what: string) (ex: exn) : unit =
    Budgeting.Persistence.logReadFailure what ex.Message

/// Die Ausgaben der Periode für das Lesen eines Monats. Scheitert das Kassenbuch, steht der Grund im Server-Log
/// und es kommen keine Ausgaben (`Some Map.empty`): Targets mit Periodenmonaten sind dann "nicht gerechnet" und
/// tragen keinen Fehlbetrag, alle anderen rechnen weiter; das Lesen scheitert nicht (budgeting-hbhzf, Review).
let private periodActivityForPage
    (ledger: CommandHandlers.LedgerActivityPort)
    (targets: Map<Guid, Target>)
    (monthKey: string)
    : Async<Map<Guid, Map<string, Money>> option> =
    async {
        try
            let! activity = CommandHandlers.loadPeriodActivity ledger targets monthKey
            return Some activity
        with ex ->
            logReadFailure $"Ausgaben der Periode {monthKey} aus dem Kassenbuch lesen" ex
            return Some Map.empty
    }

/// Liest einen Monat für die Budget-Seite (budgeting-dgrp7, ADR 0084 Punkt 3, Nachtrag): `observeMonth` liest
/// YNAB unter der Monatssperre (ein Aufruf), hält Übernahme und Beobachtungen fest, und die Antwort entsteht aus
/// dessen YNAB-Stand und der Faltung. Zugewiesen ist BBs Soll, eine offene Rückschreibung trägt ihre Markierung;
/// Aktivität, Verfügbar und Zu verteilen bleiben YNABs. Scheitert `FetchMonth`, scheitert das Lesen. Scheitert
/// nur das Festhalten, steht der Grund im Server-Log und der Monat kommt aus dem YNAB-Stand. Die Targets sind BBs
/// Regeln im Monat; wartet eine Kategorie noch auf die Target-Übernahme, läuft sie vorher (budgeting-eh8xe).
/// Der Fehlbetrag ist BBs (budgeting-hbhzf), die Ausgaben der Periode dafür kommen aus dem Kassenbuch (`ledger`,
/// budgeting-k5q2w), nie aus YNAB. Ist es nicht lesbar, kommt der Monat ohne Fehlbetrag und der Grund steht im Log.
/// `connect` ist die Datenbank für Stream, Pausen und Anhängen, `now` der Zeitpunkt der Ereignisse (UTC).
let budgetMonthWithLedger
    (ledger: CommandHandlers.LedgerActivityPort)
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (port: CommandHandlers.YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<YnabResult<BudgetMonth>> =
    async {
        let! observed = CommandHandlers.observeMonthWith connect port now monthKey
        let found =
            match observed with
            | Error (CommandHandlers.FetchFailed e) -> Error e
            | Error (CommandHandlers.NotRecorded (raw, _)) -> Ok (raw, None)
            | Ok o -> Ok (o.Raw, Some o.Fold.Categories)
        match found with
        | Error e -> return Error e
        | Ok (raw, assignments) ->
            match! Budgeting.Persistence.loadPausedCategoriesWith connect monthKey with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok paused ->
                match! CommandHandlers.targetRulesBestEffort connect port now raw with
                | Error message -> return Error (YnabError.LocalReadFailed message)
                | Ok targets ->
                    let! activity = periodActivityForPage ledger targets monthKey
                    return Ok (toBudgetMonthWith assignments activity targets paused raw)
    }

/// `budgetMonthWithLedger` mit dem Kassenbuch der Anwendung (`CommandHandlers.liveLedgerActivityPort`).
let budgetMonthWith
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (port: CommandHandlers.YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<YnabResult<BudgetMonth>> =
    budgetMonthWithLedger CommandHandlers.liveLedgerActivityPort connect port now monthKey

/// Liest einen Monat für die Budget-Seite auf der Datenbank der Anwendung.
let budgetMonth (port: CommandHandlers.YnabPort) (now: DateTime) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    budgetMonthWith global.Persistence.getConnection port now monthKey

/// Wie viele Kategorien "Zuletzt verwendet" liest. Mehr als der Picker zeigt: X und ausgeblendete
/// Kategorien fallen dort noch heraus.
[<Literal>]
let RecentMoveLimit = 10

/// "Zuletzt verwendet" ohne Datenbank: `load` liest die Bewegungen und meldet einen Lesefehler als `Error`
/// mit Text.
let recentMoveCategoriesWith (load: int -> Async<Result<Guid list, string>>) : Async<Result<Guid list, string>> =
    load RecentMoveLimit

/// "Zuletzt verwendet": lokal, kostet kein YNAB-Limit.
let recentMoveCategories () : Async<Result<Guid list, string>> =
    recentMoveCategoriesWith Budgeting.Persistence.loadRecentMoveCategories

/// Die Lese-Quellen des Rahmens.
type BudgetFramePort = {
    FetchMonths: unit -> Async<YnabResult<string list>>
    FetchCategoryOrder: unit -> Async<YnabResult<Guid list>>
    /// Die Monate ab dem übergebenen laufenden, in denen die Leiste Monatserster ausgeblendet ist (budgeting-m3x7f).
    /// Liest nur die Datenbank.
    LoadMonthStartHidden: string -> Async<Result<string list, string>>
}

/// Liest den Rahmen. Scheitert eine YNAB-Quelle, ist das Ergebnis dieser Fehler. Ist das Ausblenden der Leiste nicht
/// lesbar, gilt sie als nicht ausgeblendet: Sie steht dann einmal zu viel, die Seite bleibt bedienbar.
let frame (port: BudgetFramePort) (today: DateOnly) : Async<YnabResult<BudgetFrame>> =
    async {
        match! port.FetchMonths () with
        | Error e -> return Error e
        | Ok months ->
            match! port.FetchCategoryOrder () with
            | Error e -> return Error e
            | Ok order ->
                let frame = toBudgetFrame today months order
                let! hidden = port.LoadMonthStartHidden frame.CurrentMonth
                let hiddenMonths =
                    match hidden with
                    | Ok months -> months
                    | Error message ->
                        eprintfn "Leiste Monatserster: Ausblenden nicht lesbar (%s)" message
                        []
                return Ok { frame with MonthStartHidden = hiddenMonths }
    }

/// Die ausgeblendeten Monate aus der Datenbank der Anwendung. Ein Lesefehler wird zum `Error` mit Text.
let loadMonthStartHidden (fromMonth: string) : Async<Result<string list, string>> =
    async {
        try
            use conn = global.Persistence.getConnection ()
            return Ok (Budgeting.Persistence.loadMonthStartHiddenOn conn fromMonth)
        with ex ->
            return Error $"Ausblenden der Leiste nicht lesbar: {ex.Message}"
    }

/// Die Lese-Quellen der Historie (budgeting-dyn8g): die Bewegungen aus `events` (Kontext `budgeting`) und die
/// Monatssummen aus dem Kassenbuch. Beide lesen nur die Datenbank, kein YNAB.
type HistoryPort = {
    LoadMovements: MovementFilter -> Async<Result<HistoryMovement list, string>>
    /// Kategorie und Monatserste (aufsteigend): die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid -> DateTime list -> Async<(DateTime * Money) list>
    /// Kategorie und Monat ("YYYY-MM-01"): die Pause-Ereignisse, neueste zuerst (budgeting-t6p8z).
    LoadPauseEvents: Guid -> string -> Async<Result<HistoryEntry list, string>>
    /// Kategorie und Monat: die Target-Änderungen, neueste zuerst (budgeting-k8wq3).
    LoadTargetChanges: Guid -> string -> Async<Result<HistoryTargetChange list, string>>
    /// Die Pause-Ereignisse aller Kategorien und Monate, neueste zuerst (allgemeine Historie, budgeting-r5h2n).
    LoadAllPauseEvents: unit -> Async<Result<HistoryEntry list, string>>
    /// Die Target-Änderungen aller Kategorien und Monate, neueste zuerst (allgemeine Historie, budgeting-r5h2n).
    LoadAllTargetChanges: unit -> Async<Result<HistoryTargetChange list, string>>
}

/// Liest über `load`; eine Ausnahme wird geloggt und zu `unreadable`. Ein `Error` der Quelle bleibt, wie er ist.
let private readSource (what: string) (unreadable: string) (load: unit -> Async<Result<'T, string>>) : Async<Result<'T, string>> =
    async {
        try
            return! load ()
        with ex ->
            logReadFailure what ex
            return Error unreadable
    }

/// Die Bewegungen zum Filter. Grundlage der Historie je Kategorie und der allgemeinen (budgeting-h7q4v).
let movements (port: HistoryPort) (filter: MovementFilter) : Async<Result<HistoryMovement list, string>> =
    async {
        try
            return! port.LoadMovements filter
        with ex ->
            logReadFailure "Budget-Bewegungen lesen" ex
            return Error "Budget-Bewegungen nicht lesbar"
    }

/// Die Meldung bei einem Lesefehler der Kassenbuch-Ausgaben (Tab Historie und Targets prüfen).
[<Literal>]
let SpendingUnreadableText = "Ausgaben aus dem Kassenbuch nicht lesbar"

/// Der Tab Historie für Kategorie und Monat (Monatserster): die Bewegungen, Pause-Ereignisse und
/// Target-Änderungen der
/// Kategorie im Monat, gemischt und neueste zuerst, und die Ausgaben der 6 Monate bis einschließlich
/// dieses Monats.
let categoryHistory (port: HistoryPort) (categoryId: Guid) (firstDay: DateTime) : Async<Result<CategoryHistory, string>> =
    async {
        let monthKey = monthKeyOf firstDay
        let filter : MovementFilter =
            { CategoryId = Some categoryId; Month = Some monthKey; Origin = None }
        match! movements port filter with
        | Error message -> return Error message
        | Ok found ->
            let! pauses =
                readSource "Pause-Ereignisse lesen" Budgeting.Persistence.PauseEventsUnreadableText (fun () ->
                    port.LoadPauseEvents categoryId monthKey)
            let! changes =
                readSource "Target-Änderungen lesen" Budgeting.Persistence.TargetChangesUnreadableText (fun () ->
                    port.LoadTargetChanges categoryId monthKey)
            match pauses, changes with
            | Error message, _
            | _, Error message -> return Error message
            | Ok pauses, Ok changes ->
                let months = spendingMonths firstDay
                let! sums =
                    async {
                        try
                            let! sums = port.LoadMonthlyActivity categoryId months
                            return Ok sums
                        with ex ->
                            logReadFailure "Ausgaben aus dem Kassenbuch lesen" ex
                            return Error SpendingUnreadableText
                    }
                // Das Zusammenführen steht außerhalb des `try`: ein Fehler darin ist kein Lesefehler.
                match sums with
                | Error message -> return Error message
                | Ok sums ->
                    let entries = mergeHistory found pauses changes
                    return Ok { Entries = entries; Spending = spendingOf months sums }
    }

/// Die allgemeine Historie (budgeting-h7q4v, budgeting-r5h2n): alle Bewegungen, Pause-Ereignisse und
/// Target-Änderungen aller Monate und Kategorien, gemischt mit `mergeHistory`, neueste zuerst. Ohne Grenze. Zu
/// Durchläufen fasst der Client die Bewegungen zusammen. Ein Lesefehler einer Quelle macht die Antwort zum `Error`
/// mit ihrem Text, die Ursache steht im Server-Log.
let budgetHistory (port: HistoryPort) : Async<Result<HistoryEntry list, string>> =
    async {
        match! movements port { CategoryId = None; Month = None; Origin = None } with
        | Error message -> return Error message
        | Ok found ->
            let! pauses =
                readSource "Pause-Ereignisse lesen" Budgeting.Persistence.PauseEventsUnreadableText port.LoadAllPauseEvents
            let! changes =
                readSource "Target-Änderungen lesen" Budgeting.Persistence.TargetChangesUnreadableText port.LoadAllTargetChanges
            match pauses, changes with
            | Error message, _
            | _, Error message -> return Error message
            | Ok pauses, Ok changes -> return Ok (mergeHistory found pauses changes)
    }

/// Die echten Lese-Quellen: Budget-Ereignisse (Bewegungen, Pausen, Target-Änderungen) und Kassenbuch (Lese-Naht
/// ledger an budgeting).
let liveHistoryPort : HistoryPort =
    { LoadMovements = Budgeting.Persistence.loadMovements
      LoadPauseEvents = Budgeting.Persistence.loadPauseEvents
      LoadTargetChanges = Budgeting.Persistence.loadTargetChanges
      LoadAllPauseEvents = Budgeting.Persistence.loadAllPauseEvents
      LoadAllTargetChanges = Budgeting.Persistence.loadAllTargetChanges
      LoadMonthlyActivity =
        fun categoryId months -> Ledger.QueryHandlers.getCategoryMonthlyActivity (YnabCategoryId categoryId) months }

// ============================================
// Targets prüfen (budgeting-p2zr6, ADR 0082)
// ============================================

/// Die Lese-Quellen von Targets prüfen: der laufende Monat aus YNAB, die Monatssummen aller sichtbaren
/// Kategorien aus dem Kassenbuch in einer Abfrage, ihre erste Buchung in einer weiteren und die letzte
/// Target-Änderung je Kategorie. Kein Schreiben.
type TargetCheckPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    /// BBs Targets im gelesenen Monat, samt Target-Übernahme, falls eine Kategorie darauf wartet (budgeting-eh8xe).
    LoadTargets: YnabMonth -> Async<Result<Map<Guid, Target>, string>>
    /// Kategorien und Monatserste (aufsteigend): je Kategorie die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid list -> DateTime list -> Async<Map<Guid, (DateTime * Money) list>>
    /// Je Kategorie der Tag ihrer ersten Buchung im Kassenbuch überhaupt (budgeting-k7r2w), ohne Buchung fehlt sie.
    LoadFirstBookingDays: Guid list -> Async<Map<Guid, DateTime>>
    /// Je Kategorie die jüngste geschriebene Target-Änderung.
    LoadLastTargetChanges: unit -> Async<Result<Map<Guid, TargetChangeMark>, string>>
}

/// Die Rohdaten von Targets prüfen für den laufenden Monat (`today`, Berliner Kalendertag): die sichtbaren
/// Kategorien aus YNAB (dieselbe Filterung wie die Budget-Seite) mit BBs Targets, die 12 vollen Monate vor dem laufenden aus
/// einer Kassenbuch-Abfrage, die erste Buchung je Kategorie aus einer weiteren und die letzte Änderung je Kategorie. Jeder Lesefehler kommt als `Error` mit Text.
/// Die BB-Pausen liest es nicht: eine pausierte Kategorie wird geprüft wie jede andere (ADR 0082 Punkt 3).
let targetCheck (port: TargetCheckPort) (today: DateOnly) : Async<Result<TargetCheckData, string>> =
    async {
        let monthKey = currentMonthKey today
        match! port.FetchMonth monthKey with
        | Error e -> return Error (ynabErrorText e)
        | Ok raw ->
          match! port.LoadTargets raw with
          | Error message -> return Error message
          | Ok targets ->
            let month = toBudgetMonth targets Set.empty raw
            let categories =
                month.Groups |> List.collect (fun g -> g.Categories |> List.map (fun c -> g.Name, c))
            let months = targetCheckMonths (today.ToDateTime TimeOnly.MinValue)
            let ids = categories |> List.map (fun (_, c) -> c.Id)
            let! sums =
                async {
                    try
                        let! sums = port.LoadMonthlyActivity ids months
                        return Ok sums
                    with ex ->
                        logReadFailure "Ausgaben aus dem Kassenbuch lesen" ex
                        return Error SpendingUnreadableText
                }
            let! firstBookings =
                async {
                    try
                        let! days = port.LoadFirstBookingDays ids
                        return Ok days
                    with ex ->
                        logReadFailure "Erste Buchungen aus dem Kassenbuch lesen" ex
                        return Error SpendingUnreadableText
                }
            let! marks =
                async {
                    try
                        return! port.LoadLastTargetChanges ()
                    with ex ->
                        logReadFailure "Letzte Target-Änderungen lesen" ex
                        return Error Budgeting.Persistence.TargetChangesUnreadableText
                }
            match sums, firstBookings, marks with
            | Error message, _, _
            | _, Error message, _
            | _, _, Error message -> return Error message
            | Ok sums, Ok firstBookings, Ok marks ->
                let sources : TargetCheckSource list =
                    categories
                    |> List.map (fun (groupName, c) ->
                        { CategoryId = c.Id
                          Name = c.Name
                          GroupName = groupName
                          Target = c.Target
                          Spending = spendingOf months (sums |> Map.tryFind c.Id |> Option.defaultValue [])
                          LastChange = marks |> Map.tryFind c.Id
                          FirstBooking =
                            firstBookings
                            |> Map.tryFind c.Id
                            |> Option.map (fun day -> day.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)) })
                return
                    Ok
                        { Month = monthKey
                          Today = today.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                          Categories = sources }
    }

/// Die echten Lese-Quellen von Targets prüfen: der Monat über den YNAB-Port, BBs Targets, Kassenbuch und die
/// Target-Änderungen aus `events`.
let liveTargetCheckPort (port: CommandHandlers.YnabPort) (utcNow: unit -> DateTime) : TargetCheckPort =
    { FetchMonth = port.FetchMonth
      LoadTargets = fun raw -> CommandHandlers.targetRulesBestEffort global.Persistence.getConnection port (utcNow ()) raw
      LoadMonthlyActivity = Ledger.QueryHandlers.getMonthlyActivityByCategory
      LoadFirstBookingDays = Ledger.QueryHandlers.getFirstBookingDayByCategory
      LoadLastTargetChanges = Budgeting.Persistence.loadLastTargetChanges }
