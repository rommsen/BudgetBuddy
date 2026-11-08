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

/// Was die Seite Kategorien von YNAB braucht. Schreibwege liefern die Id des Dings. `RefreshMirror` zieht den
/// Kategorie-Spiegel nach, bestes Bemühen: er scheitert nie.
type CategoryPort = {
    FetchGroups: unit -> Async<YnabResult<ManagedCategoryGroup list>>
    CreateCategory: Guid -> string -> Async<YnabResult<Guid>>
    UpdateCategory: Guid -> Server.YnabClient.CategoryPatch -> Async<YnabResult<Guid>>
    CreateGroup: string -> Async<YnabResult<Guid>>
    UpdateGroup: Guid -> string -> Async<YnabResult<Guid>>
    RefreshMirror: unit -> Async<unit>
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

/// Die Gruppen der Seite, live aus YNAB.
let groups (port: CategoryPort) : Async<Result<ManagedCategoryGroup list, YnabError>> = port.FetchGroups ()

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
