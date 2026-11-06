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

/// Die Parallelrechnung des Fehlbetrags (budgeting-v7kfw, ADR 0084 Punkt 7): vergleicht BBs Fehlbetrag mit
/// YNABs, gibt die Abweichungsliste an `save` und schreibt eine Zeile ins Server-Log. Ein Fehler darin landet
/// nur im Server-Log, die Antwort an den Client hängt nicht davon ab.
let private runUnderfundedComparison
    (save: string -> UnderfundedComparison list -> Async<unit>)
    (month: BudgetMonth)
    : Async<unit> =
    async {
        try
            let rows = compareUnderfunded month
            do! save month.Month rows
            printfn "%s" (comparisonSummary month.Month rows)
        with ex ->
            logReadFailure "Parallelrechnung Fehlbetrag" ex
    }

/// Liest einen Monat für die Budget-Seite (budgeting-dgrp7, ADR 0084 Punkt 3, Nachtrag): `observeMonth` liest
/// YNAB unter der Monatssperre (ein Aufruf), hält Übernahme und Beobachtungen fest, und die Antwort entsteht aus
/// dessen YNAB-Stand und der Faltung. Zugewiesen ist BBs Soll, eine offene Rückschreibung trägt ihre Markierung;
/// Aktivität, Verfügbar und Zu verteilen bleiben YNABs. Scheitert `FetchMonth`, scheitert das Lesen. Scheitert
/// nur das Festhalten, steht der Grund im Server-Log und der Monat kommt aus dem YNAB-Stand. Danach läuft die
/// Parallelrechnung des Fehlbetrags auf dem gelieferten Monat, `save` hält die Abweichungsliste fest.
/// `connect` ist die Datenbank für Stream, Pausen und Anhängen, `now` der Zeitpunkt der Ereignisse (UTC).
let budgetMonthWith
    (save: string -> UnderfundedComparison list -> Async<unit>)
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
                let month = toBudgetMonthWith assignments paused raw
                do! runUnderfundedComparison save month
                return Ok month
    }

/// Liest einen Monat für die Budget-Seite auf der Datenbank der Anwendung, die Abweichungsliste geht in
/// `underfunded_comparisons`.
let budgetMonth (port: CommandHandlers.YnabPort) (now: DateTime) (monthKey: string) : Async<YnabResult<BudgetMonth>> =
    budgetMonthWith Budgeting.Persistence.saveUnderfundedComparisons global.Persistence.getConnection port now monthKey

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

/// Die beiden Lese-Quellen des Rahmens.
type BudgetFramePort = {
    FetchMonths: unit -> Async<YnabResult<string list>>
    FetchCategoryOrder: unit -> Async<YnabResult<Guid list>>
}

/// Liest den Rahmen. Scheitert eine Quelle, ist das Ergebnis dieser Fehler.
let frame (port: BudgetFramePort) (today: DateOnly) : Async<YnabResult<BudgetFrame>> =
    async {
        match! port.FetchMonths () with
        | Error e -> return Error e
        | Ok months ->
            match! port.FetchCategoryOrder () with
            | Error e -> return Error e
            | Ok order -> return Ok (toBudgetFrame today months order)
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
                async {
                    try
                        return! port.LoadPauseEvents categoryId monthKey
                    with ex ->
                        logReadFailure "Pause-Ereignisse lesen" ex
                        return Error Budgeting.Persistence.PauseEventsUnreadableText
                }
            let! changes =
                async {
                    try
                        return! port.LoadTargetChanges categoryId monthKey
                    with ex ->
                        logReadFailure "Target-Änderungen lesen" ex
                        return Error Budgeting.Persistence.TargetChangesUnreadableText
                }
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

/// Die allgemeine Historie (budgeting-h7q4v): alle Bewegungen aller Monate und Kategorien, neueste
/// zuerst. Zu Durchläufen fasst sie der Client zusammen.
let budgetHistory (port: HistoryPort) : Async<Result<HistoryMovement list, string>> =
    movements port { CategoryId = None; Month = None; Origin = None }

/// Die echten Lese-Quellen: Budget-Bewegungen und Kassenbuch (Lese-Naht ledger an budgeting).
let liveHistoryPort : HistoryPort =
    { LoadMovements = Budgeting.Persistence.loadMovements
      LoadPauseEvents = Budgeting.Persistence.loadPauseEvents
      LoadTargetChanges = Budgeting.Persistence.loadTargetChanges
      LoadMonthlyActivity =
        fun categoryId months -> Ledger.QueryHandlers.getCategoryMonthlyActivity (YnabCategoryId categoryId) months }

// ============================================
// Targets prüfen (budgeting-p2zr6, ADR 0082)
// ============================================

/// Die Lese-Quellen von Targets prüfen: der laufende Monat aus YNAB, die Monatssummen aller sichtbaren
/// Kategorien aus dem Kassenbuch in einer Abfrage und die letzte Target-Änderung je Kategorie. Kein Schreiben.
type TargetCheckPort = {
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
    /// Kategorien und Monatserste (aufsteigend): je Kategorie die Netto-Aktivität der Monate mit Buchungen.
    LoadMonthlyActivity: Guid list -> DateTime list -> Async<Map<Guid, (DateTime * Money) list>>
    /// Je Kategorie die jüngste geschriebene Target-Änderung.
    LoadLastTargetChanges: unit -> Async<Result<Map<Guid, TargetChangeMark>, string>>
}

/// Die Rohdaten von Targets prüfen für den laufenden Monat (`today`, Berliner Kalendertag): die sichtbaren
/// Kategorien aus YNAB (dieselbe Filterung wie die Budget-Seite), die 12 vollen Monate vor dem laufenden aus
/// einer Kassenbuch-Abfrage und die letzte Änderung je Kategorie. Jeder Lesefehler kommt als `Error` mit Text.
/// Die BB-Pausen liest es nicht: eine pausierte Kategorie wird geprüft wie jede andere (ADR 0082 Punkt 3).
let targetCheck (port: TargetCheckPort) (today: DateOnly) : Async<Result<TargetCheckData, string>> =
    async {
        let monthKey = currentMonthKey today
        match! port.FetchMonth monthKey with
        | Error e -> return Error (ynabErrorText e)
        | Ok raw ->
            let month = toBudgetMonth Set.empty raw
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
            let! marks =
                async {
                    try
                        return! port.LoadLastTargetChanges ()
                    with ex ->
                        logReadFailure "Letzte Target-Änderungen lesen" ex
                        return Error Budgeting.Persistence.TargetChangesUnreadableText
                }
            match sums, marks with
            | Error message, _
            | _, Error message -> return Error message
            | Ok sums, Ok marks ->
                let sources : TargetCheckSource list =
                    categories
                    |> List.map (fun (groupName, c) ->
                        { CategoryId = c.Id
                          Name = c.Name
                          GroupName = groupName
                          Target = c.Target
                          Spending = spendingOf months (sums |> Map.tryFind c.Id |> Option.defaultValue [])
                          LastChange = marks |> Map.tryFind c.Id })
                return
                    Ok
                        { Month = monthKey
                          Today = today.ToString("yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                          Categories = sources }
    }

/// Die echten Lese-Quellen von Targets prüfen: der Monat über `fetchMonth` (YNAB), Kassenbuch und
/// die Target-Änderungen aus `events`.
let liveTargetCheckPort (fetchMonth: string -> Async<YnabResult<YnabMonth>>) : TargetCheckPort =
    { FetchMonth = fetchMonth
      LoadMonthlyActivity = Ledger.QueryHandlers.getMonthlyActivityByCategory
      LoadLastTargetChanges = Budgeting.Persistence.loadLastTargetChanges }
