/// Kategorien verwalten (Context `budgeting`, budgeting-pscgb, ADR 0088 Punkt 1): Kategorie anlegen,
/// umbenennen und in eine andere Gruppe schieben, Gruppen anlegen und umbenennen. Alles geht nach YNAB, danach
/// zieht der Kategorie-Spiegel (`ledger_categories`) die Liste nach. Kein eigenes Ereignis: YNAB bleibt die
/// Quelle der Liste und der Namen (ADR 0083, ADR 0088). Ein Fehler aus YNAB lässt alles unverändert, der
/// Spiegel läuft dann nicht. YNAB kommt als Port herein, damit Tests einen Fake einsetzen.
module Server.Budgeting.Categories

open System
open Shared.Domain
open Shared.Budgeting
open Server.Validation
open Microsoft.Data.Sqlite

/// Was die Seite Kategorien von YNAB braucht. Schreibwege liefern die Id des Dings. `RefreshMirror` zieht den
/// Kategorie-Spiegel nach, bestes Bemühen: er scheitert nie.
type CategoryPort = {
    FetchGroups: unit -> Async<YnabResult<ManagedCategoryGroup list>>
    CreateCategory: Guid -> string -> Async<YnabResult<Guid>>
    UpdateCategory: Guid -> Server.YnabClient.CategoryPatch -> Async<YnabResult<Guid>>
    CreateGroup: string -> Async<YnabResult<Guid>>
    UpdateGroup: Guid -> string -> Async<YnabResult<Guid>>
    RefreshMirror: unit -> Async<unit>
    /// Ein Monat roh aus YNAB, für das Verfügbare beim Deaktivieren (budgeting-sn2sn).
    FetchMonth: string -> Async<YnabResult<YnabMonth>>
}

/// Der Kategorie-Spiegel nach einem Schreiben, bestes Bemühen wie im Spiegel-Lauf (ADR 0039 Punkt 4): das
/// Schreiben in YNAB ist schon gelungen, ein Fehler hier lässt `ledger_categories` beim letzten Stand.
let refreshMirrorWith
    (fetchCategories: unit -> Async<YnabResult<YnabCategory list>>)
    (upsert: YnabCategory list -> Async<unit>)
    : Async<unit> =
    async {
        try
            match! fetchCategories () with
            | Ok categories -> do! upsert categories
            | Error e -> printfn "Kategorie-Spiegel nach Kategorien verwalten übersprungen: %s" (ynabErrorText e)
        with ex ->
            printfn "Kategorie-Spiegel nach Kategorien verwalten übersprungen: %s" ex.Message
    }

/// Die Gruppen der Seite, live aus YNAB, in BBs Reihenfolge und mit BBs Zustand `Active` (budgeting-sn2sn).
/// Ist BBs Zustand nicht lesbar, kommt ein Fehler: die Seite zeigte sonst deaktivierte Kategorien als aktiv.
let groupsWith (connect: unit -> SqliteConnection) (port: CategoryPort) : Async<Result<ManagedCategoryGroup list, YnabError>> =
    async {
        match! port.FetchGroups () with
        | Error e -> return Error e
        | Ok groups ->
            match! Budgeting.Persistence.loadCategoryLayoutWith connect with
            | Error message -> return Error (YnabError.LocalReadFailed message)
            | Ok layout -> return Ok (arrangeGroups layout groups)
    }

/// Der Schritt nach dem Prüfen: senden oder, wenn nichts geändert ist, die Id ohne Aufruf.
type private Step =
    | Send of Async<YnabResult<Guid>>
    | Keep of Guid

/// Prüft gegen die Liste aus YNAB und schreibt. Ein abgelehnter Name fragt YNAB gar nicht, eine Ablehnung
/// gegen die Liste schreibt nichts. Nach einem gelungenen Schreiben läuft der Spiegel, sonst nicht.
let private checkedWrite
    (port: CategoryPort)
    (shape: Result<string, string list>)
    (check: ManagedCategoryGroup list -> Result<'write, string list>)
    (step: 'write -> Step)
    : Async<Result<Guid, YnabError>> =
    async {
        match shape with
        | Error errors -> return Error (RefusedByBb (String.concat " " errors))
        | Ok _ ->
            match! port.FetchGroups () with
            | Error e -> return Error e
            | Ok current ->
                match check current with
                | Error errors -> return Error (RefusedByBb (String.concat " " errors))
                | Ok plan ->
                    match step plan with
                    | Keep id -> return Ok id
                    | Send call ->
                        match! call with
                        | Error e -> return Error e
                        | Ok id ->
                            do! port.RefreshMirror ()
                            return Ok id
    }

/// Kategorie anlegen (`POST`) oder bearbeiten (`PATCH` nur mit den geänderten Feldern). Die Liste aus YNAB trägt
/// BBs Zustand der Gruppen (`arrangeGroups`), damit `validateCategorySave` Anlegen in und Verschieben in eine in BB
/// deaktivierte Gruppe ablehnt (budgeting-nyx75). Ist BBs Zustand nicht lesbar, wird nichts geschrieben: sonst ginge
/// eine Kategorie in eine deaktivierte Gruppe.
let saveCategory
    (loadLayout: unit -> Async<Result<CategoryLayout, string>>)
    (port: CategoryPort)
    (save: CategorySave)
    : Async<Result<Guid, YnabError>> =
    async {
        match! loadLayout () with
        | Error message -> return Error (YnabError.LocalReadFailed message)
        | Ok layout ->
            return!
                checkedWrite
                    port
                    (validateCategoryName save.Name)
                    (fun current -> validateCategorySave (arrangeGroups layout current) save)
                    (function
                     | CreateCategoryIn (groupId, name) -> Send (port.CreateCategory groupId name)
                     | PatchCategory (categoryId, name, groupId) -> Send (port.UpdateCategory categoryId { Name = name; GroupId = groupId })
                     | CategoryUnchanged id -> Keep id)
    }

/// Gruppe anlegen (`POST`) oder umbenennen (`PATCH`).
let saveCategoryGroup (port: CategoryPort) (save: CategoryGroupSave) : Async<Result<Guid, YnabError>> =
    checkedWrite
        port
        (validateCategoryGroupName save.Name)
        (fun current -> validateCategoryGroupSave current save)
        (function
         | CreateGroup name -> Send (port.CreateGroup name)
         | RenameGroup (groupId, name) -> Send (port.UpdateGroup groupId name)
         | GroupUnchanged id -> Keep id)

// ============================================
// Deaktivieren und eigene Reihenfolge (budgeting-sn2sn, ADR 0088 Punkte 2 und 3)
// ============================================

/// Was BB in künftigen Monaten zugewiesen hat, für mehrere Kategorien zugleich (budgeting-nyx75: eine Gruppe ohne
/// N+1): je Kategorie und übernommenem Monat nach `currentMonth` das Soll aus BBs Faltung (ADR 0084), nur Beträge
/// ungleich 0. Liest nur die Datenbank, alle Monate in einer Abfrage, kein YNAB. Jede genannte Kategorie steht im
/// Ergebnis, ohne Zuweisung mit leerer Liste.
let futureAssignedForOn
    (conn: SqliteConnection)
    (currentMonth: string)
    (categoryIds: Guid list)
    : Result<Map<Guid, (string * Money) list>, string> =
    match Budgeting.Persistence.queryTakenOverMonths conn with
    | Error message -> Error message
    | Ok months ->
        let future = months |> Seq.filter (fun m -> m > currentMonth) |> Seq.sort |> Seq.toList
        Budgeting.Persistence.loadMonthsEventsOn conn future
        |> Result.map (fun eventsByMonth ->
            let folds =
                future
                |> List.map (fun month ->
                    month, Assignments.foldMonth (eventsByMonth |> Map.tryFind month |> Option.defaultValue []))
            categoryIds
            |> List.map (fun categoryId ->
                categoryId,
                folds
                |> List.choose (fun (month, fold) ->
                    let assigned = (Assignments.stateOf fold categoryId).Assigned
                    if assigned = 0L then None else Some (month, euroOfMilliunits assigned)))
            |> Map.ofList)

/// `futureAssignedForOn` für eine Kategorie.
let futureAssignedOn (conn: SqliteConnection) (currentMonth: string) (categoryId: Guid) : Result<(string * Money) list, string> =
    futureAssignedForOn conn currentMonth [ categoryId ]
    |> Result.map (fun byCategory -> byCategory |> Map.tryFind categoryId |> Option.defaultValue [])

/// Die Meldung, wenn BB das Zugewiesen künftiger Monate nicht lesen kann.
[<Literal>]
let FutureAssignedUnreadableText = "Künftige Monate nicht lesbar, Kategorie nicht deaktiviert."

/// Die Meldung, wenn BBs Zustand der Kategorien nicht geschrieben werden konnte.
[<Literal>]
let CategoryLayoutNotSavedText = "Nicht gespeichert: BB konnte den Zustand der Kategorie nicht festhalten."

let private writeLayout (connect: unit -> SqliteConnection) (what: string) (write: SqliteConnection -> unit) : Result<unit, YnabError> =
    try
        use conn = connect ()
        write conn
        Ok ()
    with ex ->
        eprintfn "%s gescheitert: %s" what ex.Message
        Error (YnabError.LocalReadFailed CategoryLayoutNotSavedText)

/// Aktiviert oder deaktiviert eine Kategorie in BB (ADR 0088 Punkt 2). Aktivieren geht immer. Deaktivieren liest
/// den laufenden Monat aus YNAB (Verfügbar) und BBs Soll der künftigen Monate; hat die Kategorie dort Geld, lehnt
/// BB mit Betrag ab (`deactivationCheck`) und schreibt nichts. Nach YNAB geht nichts, die API kann nicht
/// ausblenden. `currentMonth` ist der laufende Monat nach Berliner Kalender, `now` der Zeitpunkt (UTC).
let setCategoryActiveWith
    (connect: unit -> SqliteConnection)
    (port: CategoryPort)
    (currentMonth: string)
    (now: DateTime)
    (save: CategoryActiveSave)
    : Async<Result<unit, YnabError>> =
    let write () =
        writeLayout connect "Kategorie aktiv oder deaktiviert festhalten" (fun conn ->
            Budgeting.Persistence.setCategoryActiveOn conn save.CategoryId save.Active now)
    async {
        if save.Active then
            return write ()
        else
            match! port.FetchMonth currentMonth with
            | Error e -> return Error e
            | Ok month ->
                let found = month.Categories |> List.tryFind (fun c -> c.Id = save.CategoryId)
                let name = found |> Option.map (fun c -> c.Name) |> Option.defaultValue "Die Kategorie"
                let available = found |> Option.map (fun c -> c.Available) |> Option.defaultValue (euroOfMilliunits 0L)
                let future =
                    try
                        use conn = connect ()
                        futureAssignedOn conn currentMonth save.CategoryId
                    with ex ->
                        Error ex.Message
                match future with
                | Error message ->
                    eprintfn "Künftige Monate für Deaktivieren lesen gescheitert: %s" message
                    return Error (YnabError.LocalReadFailed FutureAssignedUnreadableText)
                | Ok future ->
                    match deactivationCheck name available future with
                    | Error reason -> return Error (RefusedByBb reason)
                    | Ok () -> return write ()
    }

/// Die Meldung, wenn BB das Zugewiesen künftiger Monate für eine Gruppe nicht lesen kann.
[<Literal>]
let GroupFutureAssignedUnreadableText = "Künftige Monate nicht lesbar, Gruppe nicht deaktiviert."

/// Die Meldung, wenn die Gruppe nicht in YNABs Liste steht (auch die interne und „Credit Card Payments“).
[<Literal>]
let GroupNotFoundText = "Gruppe nicht gefunden."

/// Die Kategorien der Gruppe für die Prüfung beim Deaktivieren in BBs Reihenfolge: jede nicht gelöschte Kategorie, die
/// YNAB im Monat der Gruppe zuordnet, auch einzeln deaktivierte und in YNAB ausgeblendete. Die sichtbaren stehen in
/// YNABs Folge der Liste, ausgeblendete dahinter; darauf wirkt BBs gemerkte Reihenfolge (`orderWithin`).
let private groupMembers (layout: CategoryLayout) (group: ManagedCategoryGroup) (month: YnabMonth) : YnabMonthCategory list =
    let members = month.Categories |> List.filter (fun c -> c.GroupId = Some group.Id && not c.Deleted)
    let byId = members |> List.map (fun c -> c.Id, c) |> Map.ofList
    let listed = group.Categories |> List.map (fun c -> c.Id) |> List.filter byId.ContainsKey
    let known = Set.ofList listed
    let current = listed @ (members |> List.map (fun c -> c.Id) |> List.filter (known.Contains >> not))
    let stored = layout.Orders |> Map.tryFind group.Id |> Option.defaultValue []
    orderWithin stored current |> List.map (fun id -> byId.[id])

/// Aktiviert oder deaktiviert eine Kategoriegruppe in BB (budgeting-nyx75, Nachtrag ADR 0088 vom 2026-11-20).
/// Aktivieren prüft nichts und ruft YNAB nicht auf; der eigene Zustand der Kategorien und BBs Reihenfolge bleiben, so
/// kommt der Stand davor zurück. Deaktivieren liest zuerst YNABs Gruppen (unbekannte Gruppe: `GroupNotFoundText`,
/// ohne Monat), dann genau einen Monat (`currentMonth`, Berliner Kalender) und BBs Soll der künftigen Monate für alle
/// Kategorien der Gruppe in einer Abfrage. Hält eine davon Geld, lehnt BB mit allen Gründen ab
/// (`groupDeactivationCheck`) und schreibt nichts. Nach YNAB geht nichts. `now` stempelt den Zustand (UTC).
let setCategoryGroupActiveWith
    (connect: unit -> SqliteConnection)
    (port: CategoryPort)
    (currentMonth: string)
    (now: DateTime)
    (save: CategoryGroupActiveSave)
    : Async<Result<unit, YnabError>> =
    let write () =
        writeLayout connect "Gruppe aktiv oder deaktiviert festhalten" (fun conn ->
            Budgeting.Persistence.setCategoryGroupActiveOn conn save.GroupId save.Active now)
    async {
        if save.Active then
            return write ()
        else
            match! port.FetchGroups () with
            | Error e -> return Error e
            | Ok groups ->
                match groups |> List.tryFind (fun g -> g.Id = save.GroupId) with
                | None -> return Error (RefusedByBb GroupNotFoundText)
                | Some group ->
                    match! port.FetchMonth currentMonth with
                    | Error e -> return Error e
                    | Ok month ->
                        match! Budgeting.Persistence.loadCategoryLayoutWith connect with
                        | Error message -> return Error (YnabError.LocalReadFailed message)
                        | Ok layout ->
                            let members = groupMembers layout group month
                            let future =
                                try
                                    use conn = connect ()
                                    futureAssignedForOn conn currentMonth (members |> List.map (fun c -> c.Id))
                                with ex ->
                                    Error ex.Message
                            match future with
                            | Error message ->
                                eprintfn "Künftige Monate für Gruppe deaktivieren lesen gescheitert: %s" message
                                return Error (YnabError.LocalReadFailed GroupFutureAssignedUnreadableText)
                            | Ok future ->
                                let checks =
                                    members
                                    |> List.map (fun c -> c.Name, c.Available, future |> Map.tryFind c.Id |> Option.defaultValue [])
                                match groupDeactivationCheck checks with
                                | Error reason -> return Error (RefusedByBb reason)
                                | Ok () -> return write ()
    }

/// Merkt BBs Reihenfolge einer Gruppe (ADR 0088 Punkt 3). Kein YNAB: die API kann nicht sortieren. Eine Id, die
/// heute nicht in der Gruppe steht, schadet nicht, beim Lesen fällt sie weg (`orderWithin`).
let setCategoryOrderWith
    (connect: unit -> SqliteConnection)
    (now: DateTime)
    (save: CategoryOrderSave)
    : Async<Result<unit, YnabError>> =
    async {
        match validateCategoryOrderSave save with
        | Error errors -> return Error (RefusedByBb (String.concat " " errors))
        | Ok () ->
            return
                writeLayout connect "Reihenfolge der Kategorien festhalten" (fun conn ->
                    Budgeting.Persistence.saveCategoryOrderOn conn save.GroupId save.CategoryIds now)
    }

/// BBs Zustand für die Lesewege, bestes Bemühen: ist er nicht lesbar, gilt alles wie in YNAB (nichts deaktiviert,
/// YNABs Folge). So bleibt kein Geld unsichtbar, und Picker und Budget bleiben bedienbar.
let private layoutBestEffort (loadLayout: unit -> Async<Result<CategoryLayout, string>>) : Async<CategoryLayout> =
    async {
        match! loadLayout () with
        | Ok layout -> return layout
        | Error _ -> return emptyCategoryLayout
    }

/// Die Optionen der Kategorie-Picker (Endpunkt `getCategories`): Ready to Assign mit dem echten Monatswert, ohne
/// YNAB-Interna (`categoriesForPicker`), ohne in BB wirksam deaktivierte Kategorien (auch die Kategorien einer
/// deaktivierten Gruppe, `effectiveDeactivated`, budgeting-nyx75) und in BBs Reihenfolge.
let pickerOptionsWith
    (fetchPlaced: unit -> Async<YnabResult<YnabCategory list * (Guid * Guid) list>>)
    (readyToAssign: unit -> Async<Money option>)
    (loadLayout: unit -> Async<Result<CategoryLayout, string>>)
    : Async<YnabResult<YnabCategory list>> =
    async {
        match! fetchPlaced () with
        | Error e -> return Error e
        | Ok (categories, placements) ->
            let! toBeBudgeted = readyToAssign ()
            let! layout = layoutBestEffort loadLayout
            return
                Ok (
                    categoriesForPicker toBeBudgeted categories
                    |> pickerCategories (effectiveDeactivated layout placements) (flatCategoryOrder layout placements)
                )
    }

// ============================================
// Angepinnte Kategorien (budgeting-w9yt4, ADR 0087 Punkt 14)
// ============================================

/// Die Meldung, wenn die angepinnten Kategorien nicht lesbar sind.
[<Literal>]
let PinnedCategoriesUnreadableText = "Angepinnte Kategorien nicht lesbar"

/// Die Meldung, wenn die angepinnten Kategorien nicht gespeichert werden konnten.
[<Literal>]
let PinnedCategoriesNotSavedText = "Angepinnte Kategorien nicht gespeichert"

/// Die angepinnten Kategorien in ihrer Folge, auch deaktivierte und gelöschte (ausgeblendet wird beim Anzeigen).
/// Ein Lesefehler wird geloggt und zu `PinnedCategoriesUnreadableText`. Kein YNAB.
let pinnedCategoriesWith (connect: unit -> SqliteConnection) : Async<Result<Guid list, string>> =
    async {
        try
            use conn = connect ()
            return Ok (Budgeting.Persistence.loadPinnedCategoriesOn conn)
        with ex ->
            eprintfn "Angepinnte Kategorien lesen gescheitert: %s" ex.Message
            return Error PinnedCategoriesUnreadableText
    }

/// Setzt die angepinnten Kategorien als ganze Liste (Zustand, kein Ereignis, ADR 0083). Bekannt ist, was der
/// Kategorie-Spiegel kennt (`knownCategories`) oder schon angepinnt ist. Doppelte und unbekannte Ids lehnt BB mit
/// allen Gründen ab und schreibt nichts (`validatePinnedCategories`). Kein YNAB.
let setPinnedCategoriesWith
    (connect: unit -> SqliteConnection)
    (knownCategories: unit -> Async<Set<Guid>>)
    (now: DateTime)
    (categoryIds: Guid list)
    : Async<Result<unit, string>> =
    async {
        match! pinnedCategoriesWith connect with
        | Error message -> return Error message
        | Ok stored ->
            let! mirrored = knownCategories ()
            match validatePinnedCategories (Set.union mirrored (Set.ofList stored)) categoryIds with
            | Error errors -> return Error (String.concat " " errors)
            | Ok () ->
                try
                    use conn = connect ()
                    Budgeting.Persistence.savePinnedCategoriesOn conn categoryIds now
                    return Ok ()
                with ex ->
                    eprintfn "Angepinnte Kategorien speichern gescheitert: %s" ex.Message
                    return Error PinnedCategoriesNotSavedText
    }
