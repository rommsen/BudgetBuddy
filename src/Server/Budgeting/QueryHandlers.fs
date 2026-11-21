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

/// Der Tag der ersten Buchung im Kassenbuch für das Lesen eines Monats, einmal je Seitenaufruf für Ausgaben der
/// Periode und Aktivität. Ist das Kassenbuch nicht lesbar, steht der Grund im Server-Log und es kommt `Error`: beide
/// fallen dann zurück wie bei einem Lesefehler ihrer eigenen Abfrage.
let private firstEntryForPage (ledger: CommandHandlers.LedgerActivityPort) : Async<Result<DateTime option, unit>> =
    async {
        try
            let! first = ledger.LoadFirstEntryDate ()
            return Ok first
        with ex ->
            Budgeting.Persistence.logBestEffortFailure "Erste Buchung aus dem Kassenbuch lesen" ex.Message
            return Error ()
    }

/// Die Ausgaben der Periode für das Lesen eines Monats. Scheitert das Kassenbuch, steht der Grund im Server-Log
/// und es kommen keine Ausgaben (`Some Map.empty`): Targets mit Periodenmonaten sind dann "nicht gerechnet" und
/// tragen keinen Fehlbetrag, alle anderen rechnen weiter; das Lesen scheitert nicht (budgeting-hbhzf, Review).
let private periodActivityForPage
    (ledger: CommandHandlers.LedgerActivityPort)
    (first: Result<DateTime option, unit>)
    (targets: Map<Guid, Target>)
    (monthKey: string)
    : Async<Map<Guid, Map<string, Money>> option> =
    async {
        match first with
        | Error () -> return Some Map.empty
        | Ok first ->
            try
                let! activity = CommandHandlers.loadPeriodActivityAfter ledger first targets monthKey
                return Some activity
            with ex ->
                Budgeting.Persistence.logBestEffortFailure $"Ausgaben der Periode {monthKey} aus dem Kassenbuch lesen" ex.Message
                return Some Map.empty
    }

/// Trägt in jede Kategorie ihr Target gegenüber YNAB ein (budgeting-e2z7v, `TargetRules.targetYnabState`): die offene
/// Schreibung aus `openWrites` und die Lücken gegen YNABs Goal aus dem gelesenen Monat `raw`. Rein.
let withTargetInYnab
    (openWrites: Map<Guid, Server.Budgeting.TargetRules.OpenTargetWrite>)
    (raw: YnabMonth)
    (month: BudgetMonth)
    : BudgetMonth =
    let goals = raw.Categories |> List.map (fun c -> c.Id, c.Target) |> Map.ofList
    let withState (c: BudgetCategory) =
        let inYnab = goals |> Map.tryFind c.Id |> Option.flatten
        { c with TargetInYnab = Server.Budgeting.TargetRules.targetYnabState c.Target inYnab (openWrites |> Map.tryFind c.Id) }
    { month with Groups = month.Groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map withState }) }

/// Die in BB wirksam deaktivierten Kategorien für das Lesen eines Monats (budgeting-sn2sn): selbst deaktivierte und
/// die einer deaktivierten Gruppe nach YNABs Zuordnung im Stand `raw` (`effectiveDeactivated`, budgeting-nyx75). Ist
/// BBs Zustand nicht lesbar, gilt keine als deaktiviert: die Liste zeigt dann eine zu viel, aber kein Geld wird
/// unsichtbar. Der Grund steht im Server-Log.
let private deactivatedForPage (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (raw: YnabMonth) : Async<Set<Guid>> =
    async {
        match! Budgeting.Persistence.loadCategoryLayoutWith connect with
        | Ok layout -> return effectiveDeactivated layout (monthPlacements raw)
        | Error _ -> return Set.empty
    }

/// Die Aktivität des Monats für die Seite (ADR 0101 Punkt 2, `monthActivity`): aus dem Kassenbuch ab dessen erster
/// Buchung, davor aus dem Stand. Ist das Kassenbuch nicht lesbar, gilt die Aktivität des Stands und der Grund steht
/// im Server-Log; das Lesen scheitert nicht.
let private monthActivityForPage
    (ledger: CommandHandlers.LedgerActivityPort)
    (first: Result<DateTime option, unit>)
    (snapshot: YnabMonth)
    : Async<Map<Guid, Money>> =
    async {
        match first with
        | Error () -> return monthActivity None Map.empty snapshot
        | Ok first ->
            try
                if ledgerCoversMonth first snapshot.Month then
                    let day = DateTime.ParseExact(snapshot.Month, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture)
                    let! sums = ledger.LoadMonthlyActivity (snapshot.Categories |> List.map (fun c -> c.Id)) [ day ]
                    let ofMonth =
                        sums
                        |> Map.map (fun _ rows -> rows |> List.filter (fun (d, _) -> d = day) |> List.map snd)
                        |> Map.filter (fun _ rows -> not (List.isEmpty rows))
                        |> Map.map (fun _ rows -> rows |> List.reduce (fun a b -> { a with Amount = a.Amount + b.Amount }))
                    return monthActivity first ofMonth snapshot
                else
                    return monthActivity first Map.empty snapshot
            with ex ->
                Budgeting.Persistence.logBestEffortFailure $"Aktivität {snapshot.Month} aus dem Kassenbuch lesen" ex.Message
                return monthActivity None Map.empty snapshot
    }

/// Die Faltung des Monats aus BBs Ereignissen, ohne YNAB. Ist der Stream nicht lesbar, steht der Grund im Log und es
/// kommt `None`: die Seite zeigt dann YNABs Zugewiesen aus dem Stand ohne Markierung, wie früher bei einer
/// Beobachtung, die sich nicht festhalten ließ.
let private foldForPage (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection) (monthKey: string) : Map<Guid, AssignmentState> option =
    let loaded =
        try
            use conn = connect ()
            Budgeting.Persistence.loadMonthEventsOn conn monthKey
        with ex ->
            Error ex.Message
    match loaded with
    | Ok events -> Some (Assignments.foldMonth events).Categories
    | Error reason ->
        Budgeting.Persistence.logBestEffortFailure $"Monats-Stream {monthKey} lesen" reason
        None

/// Baut den Monat der Seite aus dem Stand (ADR 0101 Punkt 4): Soll aus `assignments`, Aktivität aus dem Kassenbuch,
/// Verfügbar und Zu verteilen abgeleitet (`bbMonthOf`), dazu Pausen, Targets, Fehlbetrag, Deaktivieren und das
/// Target gegenüber YNAB (Goal aus dem Stand). `copiedAt` ist der Zeitpunkt des gespeicherten Stands, `None`, wenn
/// keiner gespeichert ist. Liest nur die Datenbank.
let private pageMonthOf
    (ledger: CommandHandlers.LedgerActivityPort)
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (raw: YnabMonth)
    (copiedAt: DateTime option)
    (assignments: Map<Guid, AssignmentState> option)
    (targets: Map<Guid, Target>)
    : Async<YnabResult<BudgetMonth>> =
    async {
        let monthKey = raw.Month
        match! Budgeting.Persistence.loadPausedCategoriesWith connect monthKey with
        | Error message -> return Error (YnabError.LocalReadFailed message)
        | Ok paused ->
            let! first = firstEntryForPage ledger
            let! periodActivity = periodActivityForPage ledger first targets monthKey
            let! activity = monthActivityForPage ledger first raw
            // Nach bestem Bemühen: ist der Stand gegenüber YNAB nicht lesbar, fehlt nur der Hinweis (Grund im Log).
            let! openWrites = Budgeting.Persistence.loadOpenTargetWritesWith connect
            let! deactivated = deactivatedForPage connect raw
            // Erst deaktivieren (budgeting-sn2sn), dann den Stand gegenüber YNAB eintragen: beide ändern nur ihr
            // eigenes Feld, die Folge ändert nichts am Ergebnis; so bekommt eine weggelassene Kategorie keinen Stand.
            let month =
                bbMonthOf assignments activity raw
                |> toBudgetMonthWith assignments periodActivity targets paused
                |> applyDeactivation deactivated
                // YNABs Aktivität aus dem Stand für den Hinweis „Abweichung Kassenbuch/YNAB“ im Detail.
                |> withActivityInYnab raw
            let withState = withTargetInYnab (openWrites |> Result.defaultValue Map.empty) raw month
            return Ok { withState with CopiedAt = copiedAt }
    }

/// Liest einen Monat für die Budget-Seite (budgeting-m5pqn, ADR 0101): nur aus BBs Datenbank, ohne YNAB-Aufruf und
/// ohne Sperre. Zugewiesen ist BBs Soll aus der Faltung, eine offene Rückschreibung trägt ihre Markierung. Die
/// Aktivität kommt aus dem Kassenbuch (vor dessen erster Buchung aus dem YNAB-Stand), Verfügbar und Zu verteilen
/// leitet BB aus Soll, Aktivität und Stand ab (`bbMonthOf`). Kategorienliste, Vortrag und Goal stammen aus dem
/// YNAB-Stand, den nur das Beobachten schreibt (`CommandHandlers.observeMonthWith`).
/// Ein Monat ohne Stand wird einmal beobachtet wie früher bei jedem Lesen (ADR 0101 Punkt 7): ein `FetchMonth`
/// unter der Monatssperre, dazu die Target-Übernahme, falls eine Kategorie auf sie wartet (budgeting-eh8xe).
/// Scheitert dort `FetchMonth`, scheitert das Lesen; scheitert nur das Festhalten, steht der Grund im Server-Log
/// und der Monat kommt aus dem gerade gelesenen YNAB-Monat mit YNABs Zugewiesen, ohne `CopiedAt`.
/// Der Fehlbetrag ist BBs (budgeting-hbhzf), die Ausgaben der Periode dafür kommen aus dem Kassenbuch (`ledger`,
/// budgeting-k5q2w). Ist es nicht lesbar, kommt der Monat ohne Fehlbetrag und der Grund steht im Log.
/// In BB deaktivierte Kategorien fehlen, außer sie haben im Monat Aktivität oder Verfügbares (`applyDeactivation`,
/// budgeting-sn2sn).
/// `connect` ist die Datenbank, `port` wird nur für einen Monat ohne Stand gebraucht, `now` stempelt dessen
/// Beobachtung (UTC).
let budgetMonthWithLedger
    (ledger: CommandHandlers.LedgerActivityPort)
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (port: CommandHandlers.YnabPort)
    (now: DateTime)
    (monthKey: string)
    : Async<YnabResult<BudgetMonth>> =
    async {
        match! Budgeting.Persistence.loadYnabMonthSnapshotWith connect monthKey with
        | Error message -> return Error (YnabError.LocalReadFailed message)
        | Ok (Some snapshot) ->
            match! Budgeting.Persistence.loadTargetRulesWith connect monthKey with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok targets ->
                return! pageMonthOf ledger connect snapshot.Raw (Some snapshot.FetchedAt) (foldForPage connect monthKey) targets
        | Ok None ->
            let! observed = CommandHandlers.observeMonthWith connect port now monthKey
            let found =
                match observed with
                | Error (CommandHandlers.FetchFailed e) -> Error e
                | Error (CommandHandlers.NotRecorded (raw, _)) -> Ok (raw, None, None)
                | Ok o -> Ok (o.Raw, Some o.Fold.Categories, Some now)
            match found with
            | Error e -> return Error e
            | Ok (raw, assignments, copiedAt) ->
                match! CommandHandlers.targetRulesBestEffort connect port now raw with
                | Error message -> return Error (YnabError.LocalReadFailed message)
                | Ok targets -> return! pageMonthOf ledger connect raw copiedAt assignments targets
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

/// Die Lese-Quellen des Rahmens (ADR 0101 Punkt 1 und 3): Fenster und Platzierung als YNAB-Stand, BBs Layout und das
/// Ausblenden der Leiste aus der Datenbank. YNAB (`FetchMonths`, `FetchCategoryPlacements`) nur, solange kein Stand da ist.
type BudgetFramePort = {
    /// Das Monatsfenster aus `GET /months`.
    FetchMonths: unit -> Async<YnabResult<string list>>
    /// Die Platzierung der Kategorien (Gruppe, Kategorie in YNABs Folge) aus `GET /categories`.
    FetchCategoryPlacements: unit -> Async<YnabResult<(Guid * Guid) list>>
    LoadMonthWindow: unit -> Async<Result<YnabMonthWindow option, string>>
    HoldMonthWindow: YnabMonthWindow -> Async<Result<unit, string>>
    LoadCategoryCatalog: unit -> Async<Result<YnabCategoryCatalog option, string>>
    HoldCategoryCatalog: YnabCategoryCatalog -> Async<Result<unit, string>>
    /// BBs Deaktivieren und Reihenfolge (ADR 0088).
    LoadCategoryLayout: unit -> Async<Result<CategoryLayout, string>>
    /// Die Monate ab dem übergebenen laufenden, in denen die Leiste Monatserster ausgeblendet ist (budgeting-m3x7f).
    /// Liest nur die Datenbank.
    LoadMonthStartHidden: string -> Async<Result<string list, string>>
}

/// Ein Teil des Rahmens aus dem YNAB-Stand (budgeting-yx51n). Ist keiner da oder ist er nicht lesbar, wird einmal aus
/// YNAB gelesen und mit `now` festgehalten (ADR 0101 Punkt 7). Scheitert das Festhalten, gilt das Gelesene trotzdem
/// und der Grund steht im Log; das nächste Lesen fragt YNAB dann noch einmal.
let private heldOrFetched
    (what: string)
    (load: unit -> Async<Result<'held option, string>>)
    (fetch: unit -> Async<YnabResult<'fetched>>)
    (held: 'fetched -> 'held)
    (hold: 'held -> Async<Result<unit, string>>)
    : Async<YnabResult<'held>> =
    async {
        let! loaded = load ()
        match loaded with
        | Ok (Some state) -> return Ok state
        | Ok None
        | Error _ ->
            match! fetch () with
            | Error e -> return Error e
            | Ok fetched ->
                let state = held fetched
                match! hold state with
                | Ok () -> ()
                | Error message -> Budgeting.Persistence.logBestEffortFailure $"{what} festhalten" message
                return Ok state
    }

/// Liest den Rahmen aus BBs Datenbank (ADR 0101 Punkt 1): Fenster und Platzierung aus dem YNAB-Stand, die
/// Reihenfolge in BBs Layout (ADR 0088 Punkt 3: eine neue Kategorie steht am Ende ihrer Gruppe). Nur ohne Stand wird
/// YNAB gelesen; scheitert das, ist das Ergebnis dieser Fehler. Ist BBs Layout nicht lesbar, gilt YNABs Folge. Ist das
/// Ausblenden der Leiste nicht lesbar, gilt sie als nicht ausgeblendet: Sie steht dann einmal zu viel, die Seite bleibt
/// bedienbar.
let frame (port: BudgetFramePort) (today: DateOnly) (now: DateTime) : Async<YnabResult<BudgetFrame>> =
    async {
        let! window =
            heldOrFetched "Monatsfenster" port.LoadMonthWindow port.FetchMonths (fun months -> { Months = months; FetchedAt = now }) port.HoldMonthWindow
        match window with
        | Error e -> return Error e
        | Ok window ->
            let! catalog =
                heldOrFetched
                    "Platzierung der Kategorien"
                    port.LoadCategoryCatalog
                    port.FetchCategoryPlacements
                    (fun placements -> { Placements = placements; FetchedAt = now })
                    port.HoldCategoryCatalog
            match catalog with
            | Error e -> return Error e
            | Ok catalog ->
                let! layout = port.LoadCategoryLayout ()
                let layout =
                    match layout with
                    | Ok layout -> layout
                    | Error message ->
                        Budgeting.Persistence.logBestEffortFailure "Rahmen: BBs Reihenfolge lesen" message
                        emptyCategoryLayout
                let frame = toBudgetFrame today window.Months (flatCategoryOrder layout catalog.Placements)
                let! hidden = port.LoadMonthStartHidden frame.CurrentMonth
                let hiddenMonths =
                    match hidden with
                    | Ok months -> months
                    | Error message ->
                        Budgeting.Persistence.logBestEffortFailure "Leiste Monatserster: Ausblenden lesen" message
                        []
                return Ok { frame with MonthStartHidden = hiddenMonths }
    }

/// Liest einen Teil des Rahmens nach, wenn sein Stand fehlt, nicht lesbar oder älter als 5 min ist
/// (`frameStateDue`, ADR 0101 Punkt 6): ein YNAB-Aufruf, dann mit `now` festhalten. Nach bestem Bemühen: scheitert
/// Lesen oder Festhalten, steht der Grund im Log und der alte Stand bleibt.
let private refreshIfDue
    (now: DateTime)
    (what: string)
    (load: unit -> Async<Result<'held option, string>>)
    (fetchedAt: 'held -> DateTime)
    (fetch: unit -> Async<YnabResult<'fetched>>)
    (held: 'fetched -> 'held)
    (hold: 'held -> Async<Result<unit, string>>)
    : Async<unit> =
    async {
        let! loaded = load ()
        let due =
            match loaded with
            | Ok state -> frameStateDue now (state |> Option.map fetchedAt)
            | Error _ -> true
        if due then
            match! fetch () with
            | Error e -> Budgeting.Persistence.logBestEffortFailure $"{what} nachlesen" (ynabErrorText e)
            | Ok fetched ->
                match! hold (held fetched) with
                | Ok () -> ()
                | Error message -> Budgeting.Persistence.logBestEffortFailure $"{what} festhalten" message
    }

/// Fenster und Platzierung nachlesen, je Teil nur, wenn er fällig ist (höchstens zwei YNAB-Aufrufe).
let private refreshFrameIfDue (port: BudgetFramePort) (now: DateTime) : Async<unit> =
    async {
        do!
            refreshIfDue
                now
                "Monatsfenster"
                port.LoadMonthWindow
                (fun (w: YnabMonthWindow) -> w.FetchedAt)
                port.FetchMonths
                (fun months -> { Months = months; FetchedAt = now })
                port.HoldMonthWindow
        do!
            refreshIfDue
                now
                "Platzierung der Kategorien"
                port.LoadCategoryCatalog
                (fun (c: YnabCategoryCatalog) -> c.FetchedAt)
                port.FetchCategoryPlacements
                (fun placements -> { Placements = placements; FetchedAt = now })
                port.HoldCategoryCatalog
    }

/// Das Nachziehen eines Monats nach dem sofortigen Lesen (budgeting-ffjk9, ADR 0101 Punkt 6), ohne Timer. Es liest
/// nicht nur, es schreibt: Ist der Stand fällig (ohne Stand, veraltet oder älter als 60 s), wird der Monat unter
/// `MonthGuard` beobachtet (`CommandHandlers.observeIfDueWith`), und das hält YNAB-Stand, Übernahme und Echo-Regel
/// fest wie jedes Beobachten: eine Beobachtung gewinnt, das Soll wird YNABs Wert (ADR 0084 Punkt 3). Danach läuft die
/// Target-Übernahme nach bestem Bemühen (`targetRulesBestEffort`), sie hängt `TargetTakenOverFromYnab` an. Fenster
/// und Platzierung liest es nach, wenn sie älter als 5 min sind (bestes Bemühen); der neue Rahmen steht dann in der
/// Datenbank, die offene Seite sieht ihn erst beim nächsten Öffnen (`BudgetMonthSync` trägt nur den Monat, der Client
/// lädt `getBudgetFrame` einmal je Seitenaufruf). `MonthChanged` mit dem Monat wie beim Lesen nur, wenn YNABs Monat
/// oder BBs Targets jetzt anders sind, sonst `MonthUnchanged`; waren BBs Targets vor dem Abgleich nicht lesbar, zählt
/// das als anders. Scheitert das Beobachten oder das Lesen der Targets danach, ist das Ergebnis dieser Fehler; die
/// Seite behält dann ihren Stand.
let syncMonthWithLedger
    (ledger: CommandHandlers.LedgerActivityPort)
    (connect: unit -> Microsoft.Data.Sqlite.SqliteConnection)
    (port: CommandHandlers.YnabPort)
    (framePort: BudgetFramePort)
    (now: DateTime)
    (monthKey: string)
    : Async<YnabResult<BudgetMonthSync>> =
    async {
        let! observed = CommandHandlers.observeIfDueWith connect port now monthKey
        do! refreshFrameIfDue framePort now
        match observed with
        | Error e -> return Error e
        | Ok CommandHandlers.SnapshotFresh -> return Ok MonthUnchanged
        | Ok (CommandHandlers.MonthObserved (before, observed)) ->
            let! targetsBefore = Budgeting.Persistence.loadTargetRulesWith connect monthKey
            match! CommandHandlers.targetRulesBestEffort connect port now observed.Raw with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok targets ->
                let sameRaw = before |> Option.exists (fun s -> s.Raw = observed.Raw)
                let sameTargets =
                    match targetsBefore with
                    | Ok rules -> rules = targets
                    // Nicht lesbar vor dem Abgleich: die Seite kann nicht wissen, ob sie BBs Targets zeigt.
                    | Error _ -> false
                if sameRaw && sameTargets then
                    return Ok MonthUnchanged
                else
                    let! month = pageMonthOf ledger connect observed.Raw (Some now) (Some observed.Fold.Categories) targets
                    return month |> Result.map MonthChanged
    }

/// `syncMonthWithLedger` mit dem Kassenbuch und der Datenbank der Anwendung.
let syncMonth (port: CommandHandlers.YnabPort) (framePort: BudgetFramePort) (now: DateTime) (monthKey: string) : Async<YnabResult<BudgetMonthSync>> =
    syncMonthWithLedger CommandHandlers.liveLedgerActivityPort global.Persistence.getConnection port framePort now monthKey

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
