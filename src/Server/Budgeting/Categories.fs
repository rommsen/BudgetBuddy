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

/// Kategorie anlegen (`POST`) oder bearbeiten (`PATCH` nur mit den geänderten Feldern).
let saveCategory (port: CategoryPort) (save: CategorySave) : Async<Result<Guid, YnabError>> =
    checkedWrite
        port
        (validateCategoryName save.Name)
        (fun current -> validateCategorySave current save)
        (function
         | CreateCategoryIn (groupId, name) -> Send (port.CreateCategory groupId name)
         | PatchCategory (categoryId, name, groupId) -> Send (port.UpdateCategory categoryId { Name = name; GroupId = groupId })
         | CategoryUnchanged id -> Keep id)

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

/// Was BB in künftigen Monaten zugewiesen hat: je übernommenem Monat nach `currentMonth` das Soll der Kategorie aus
/// BBs Faltung (ADR 0084), nur Beträge ungleich 0. Liest nur die Datenbank, alle Monate in einer Abfrage, kein YNAB.
let futureAssignedOn (conn: SqliteConnection) (currentMonth: string) (categoryId: Guid) : Result<(string * Money) list, string> =
    match Budgeting.Persistence.queryTakenOverMonths conn with
    | Error message -> Error message
    | Ok months ->
        let future = months |> Seq.filter (fun m -> m > currentMonth) |> Seq.sort |> Seq.toList
        Budgeting.Persistence.loadMonthsEventsOn conn future
        |> Result.map (fun eventsByMonth ->
            future
            |> List.choose (fun month ->
                let events = eventsByMonth |> Map.tryFind month |> Option.defaultValue []
                let assigned = (Assignments.stateOf (Assignments.foldMonth events) categoryId).Assigned
                if assigned = 0L then None else Some (month, euroOfMilliunits assigned)))

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

/// Alle Kategorie-Ids flach in BBs Reihenfolge für den Rahmen der Budget-Seite (`BudgetFrame.CategoryOrder`).
let bbCategoryOrderWith
    (fetchPlaced: unit -> Async<YnabResult<YnabCategory list * (Guid * Guid) list>>)
    (loadLayout: unit -> Async<Result<CategoryLayout, string>>)
    : Async<YnabResult<Guid list>> =
    async {
        match! fetchPlaced () with
        | Error e -> return Error e
        | Ok (_, placements) ->
            let! layout = layoutBestEffort loadLayout
            return Ok (flatCategoryOrder layout placements)
    }

/// Die Optionen der Kategorie-Picker (Endpunkt `getCategories`): Ready to Assign mit dem echten Monatswert, ohne
/// YNAB-Interna (`categoriesForPicker`), ohne in BB deaktivierte Kategorien und in BBs Reihenfolge.
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
                    |> pickerCategories layout (flatCategoryOrder layout placements)
                )
    }
