/// Die Seite Kategorien `#/budget/categories` (budgeting-pscgb, ADR 0088 Punkt 1): Gruppen mit ihren Kategorien,
/// Kategorie anlegen und bearbeiten (Name, Gruppe), Gruppen anlegen und umbenennen. Alles geht nach YNAB. Ein
/// Formular ist offen, am Handy als Sheet, ab 1024 px als Panel (§7.6). "Neue Gruppe" im Auswahlfeld ersetzt das
/// Kategorie-Formular durch das Gruppen-Formular und kehrt nach dem Anlegen mit der neuen Gruppe zurück (§7.15,
/// Sheets werden nicht gestapelt). Reine Übergänge; Aufrufe von `Api` liegen hinter Lambdas.
/// Deaktivieren (Schalter "Aktiv" im Formular, "Aktivieren" im Abschnitt Deaktiviert) und die Pfeile für BBs
/// Reihenfolge je Gruppe gehen nur an BB, nicht nach YNAB (budgeting-sn2sn, ADR 0088 Punkte 2 und 3).
module Components.Budget.Categories

open System
open Elmish
open Shared.Budgeting
open Types

/// Der Wert im Auswahlfeld Gruppe, der das Gruppen-Formular öffnet.
[<Literal>]
let NewGroupChoice = "neue-gruppe"

/// Das Kategorie-Formular. `CategoryId = None` legt an. `GroupId = None` nur, solange es keine Gruppe gibt.
/// `Active` ist der Schalter "Aktiv", `WasActive` der Zustand beim Öffnen (eine neue Kategorie ist aktiv).
type CategoryEditor = { CategoryId: Guid option; Name: string; GroupId: Guid option; Active: bool; WasActive: bool }

/// Das Gruppen-Formular. `ReturnTo` ist das Kategorie-Formular, aus dem "Neue Gruppe" kam.
type GroupEditor = { GroupId: Guid option; Name: string; ReturnTo: CategoryEditor option }

type Editor =
    | EditingCategory of CategoryEditor
    | EditingGroup of GroupEditor

/// Das offene Formular. `Saving`: der Aufruf läuft, Speichern ist gesperrt. `Error`: die Meldung des Servers,
/// sie bleibt im Formular, bis etwas geändert wird. `Notice`: der Hinweis nach gelungenem Deaktivieren oder
/// Aktivieren, er kommt als Toast, wenn das Formular schließt.
type Form = { Editor: Editor; Saving: bool; Error: string option; Notice: string option }

type Model = {
    Groups: RemoteData<ManagedCategoryGroup list>
    Form: Form option
    /// Zählt je Laden: eine Antwort mit fremder Generation ist überholt.
    Generation: int
    /// Der Abschnitt "Deaktiviert (n)" ist aufgeklappt.
    InactiveOpen: bool
}

type Msg =
    /// Die Seite wird betreten: die Liste holen, kein Formular offen.
    | Entered
    /// "Wiederholen" nach einem Ladefehler.
    | RetryLoad
    | Loaded of generation: int * Result<ManagedCategoryGroup list, string>
    /// Antwort des stillen Neuladens nach einem Speichern: ein Fehler lässt die Liste stehen.
    | Reloaded of generation: int * Result<ManagedCategoryGroup list, string>
    /// "+ Kategorie".
    | NewCategory
    /// Tipp auf eine Kategorie.
    | EditCategory of Guid
    /// "+ Gruppe".
    | NewGroup
    /// "Umbenennen" an einer Gruppe.
    | RenameGroup of Guid
    | NameChanged of string
    /// Wahl im Auswahlfeld Gruppe: eine Gruppen-Id oder `NewGroupChoice`.
    | GroupChosen of string
    | Save
    /// "Abbrechen", Schließen oder Esc. Im Gruppen-Formular aus "Neue Gruppe" zurück zum Kategorie-Formular.
    | CloseForm
    | CategorySaved of Result<Guid, string>
    | GroupSaved of name: string * Result<Guid, string>
    /// "Zurück": zur Budget-Seite.
    | Leave
    /// Der Schalter "Aktiv" im Formular.
    | ActiveToggled of bool
    /// Antwort auf Deaktivieren oder Aktivieren aus dem Formular.
    | ActiveSaved of Result<unit, string>
    /// Pfeil hoch oder runter (oder Alt+↑/↓ am PC) an einer Kategorie.
    | MoveCategory of Guid * OrderStep
    | OrderSaved of Result<unit, string>
    /// Den Abschnitt "Deaktiviert (n)" auf- oder zuklappen.
    | ToggleInactive
    /// "Aktivieren" im Abschnitt Deaktiviert.
    | Reactivate of Guid
    | Reactivated of name: string * Result<unit, string>

type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | BackToBudget

let init () : Model = { Groups = NotAsked; Form = None; Generation = 0; InactiveOpen = false }

let private loadCmd (generation: int) (answer: int * Result<ManagedCategoryGroup list, string> -> Msg) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Client.Retry.reading Api.categories.getCategoryGroups ())
        ()
        (fun result -> answer (generation, result))
        (fun ex -> answer (generation, Error ex.Message))

let private saveCategoryCmd (save: CategorySave) : Cmd<Msg> =
    Cmd.OfAsync.either (fun s -> Api.categories.saveCategory s) save CategorySaved (fun ex -> CategorySaved (Error ex.Message))

let private saveGroupCmd (save: CategoryGroupSave) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun s -> Api.categories.saveCategoryGroup s)
        save
        (fun result -> GroupSaved (save.Name.Trim(), result))
        (fun ex -> GroupSaved (save.Name.Trim(), Error ex.Message))

let private setActiveCmd (save: CategoryActiveSave) (answer: Result<unit, string> -> Msg) : Cmd<Msg> =
    Cmd.OfAsync.either (fun s -> Api.categories.setCategoryActive s) save answer (fun ex -> answer (Error ex.Message))

let private saveOrderCmd (save: CategoryOrderSave) : Cmd<Msg> =
    Cmd.OfAsync.either (fun s -> Api.categories.setCategoryOrder s) save OrderSaved (fun ex -> OrderSaved (Error ex.Message))

let private groupsOf (model: Model) : ManagedCategoryGroup list =
    match model.Groups with
    | Success groups -> groups
    | _ -> []

let private opened (editor: Editor) : Form option = Some { Editor = editor; Saving = false; Error = None; Notice = None }

/// Der Hinweis nach dem Deaktivieren: die YNAB-API kann nicht ausblenden (ADR 0088 Punkt 2).
let deactivatedNotice (name: string) : string = $"{name} deaktiviert. In YNAB noch sichtbar, dort ausblenden."

/// Der Hinweis nach dem Aktivieren, wie im Prototyp.
let reactivatedNotice (name: string) : string = $"{name} ist wieder aktiv"

/// Die Kategorien einer Gruppe, die auf der Seite in ihrer Karte stehen: die aktiven, in BBs Reihenfolge.
let activeCategories (group: ManagedCategoryGroup) : ManagedCategory list =
    group.Categories |> List.filter (fun c -> c.Active)

/// Der Abschnitt Deaktiviert: jede deaktivierte Kategorie mit ihrer Gruppe, Gruppe für Gruppe.
let inactiveCategories (groups: ManagedCategoryGroup list) : (ManagedCategoryGroup * ManagedCategory) list =
    groups |> List.collect (fun g -> g.Categories |> List.filter (fun c -> not c.Active) |> List.map (fun c -> g, c))

/// Am PC verschiebt Alt+↑ und Alt+↓ die Kategorie, deren Zeile den Fokus hat.
let moveKey (key: string) (altKey: bool) : OrderStep option =
    match key, altKey with
    | "ArrowUp", true -> Some StepUp
    | "ArrowDown", true -> Some StepDown
    | _ -> None

/// Die neue Reihenfolge der Gruppe nach einem Pfeil: die aktiven Kategorien tauschen untereinander, die
/// deaktivierten stehen dahinter. `None`, wenn sich nichts ändert (Rand oder unbekannte Kategorie).
let movedOrder (step: OrderStep) (categoryId: Guid) (group: ManagedCategoryGroup) : Guid list option =
    let visible = activeCategories group |> List.map (fun c -> c.Id)
    let moved = moveWithin step categoryId visible
    if moved = visible then
        None
    else
        Some (moved @ (group.Categories |> List.filter (fun c -> not c.Active) |> List.map (fun c -> c.Id)))

/// Die Gruppe mit ihren Kategorien in der Reihenfolge `order`.
let private reordered (order: Guid list) (group: ManagedCategoryGroup) : ManagedCategoryGroup =
    let byId = group.Categories |> List.map (fun c -> c.Id, c) |> Map.ofList
    { group with Categories = order |> List.choose (fun id -> Map.tryFind id byId) }

/// Setzt `Active` einer Kategorie in der Liste, bis das stille Neuladen den Stand bringt.
let private withActive (categoryId: Guid) (active: bool) (model: Model) : Model =
    match model.Groups with
    | Success groups ->
        let set (c: ManagedCategory) = if c.Id = categoryId then { c with Active = active } else c
        { model with Groups = Success (groups |> List.map (fun g -> { g with Categories = g.Categories |> List.map set })) }
    | _ -> model

/// Der Titel des Formulars wie im Prototyp.
let formTitle (editor: Editor) : string =
    match editor with
    | EditingCategory { CategoryId = None } -> "Neue Kategorie"
    | EditingCategory _ -> "Kategorie bearbeiten"
    | EditingGroup { GroupId = None } -> "Neue Gruppe"
    | EditingGroup _ -> "Gruppe umbenennen"

/// Die Beschriftung des Hauptknopfs: "Anlegen" für eine neue Kategorie, sonst "Speichern" (Prototyp).
let saveLabel (editor: Editor) : string =
    match editor with
    | EditingCategory { CategoryId = None } -> "Anlegen"
    | _ -> "Speichern"

/// Darf "Speichern" gedrückt werden? Nicht, solange der Aufruf läuft, und nicht ohne Gruppe.
let canSave (form: Form) : bool =
    not form.Saving
    && (match form.Editor with
        | EditingCategory editor -> editor.GroupId.IsSome
        | EditingGroup _ -> true)

/// Hat das Formular Name oder Gruppe gegenüber der Liste geändert? Eine neue Kategorie gilt als geändert.
let private fieldsChanged (groups: ManagedCategoryGroup list) (editor: CategoryEditor) : bool =
    match editor.CategoryId with
    | None -> true
    | Some id ->
        let current =
            groups |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> g.Id, c.Name))
        match current with
        | Some (groupId, name) -> Some groupId <> editor.GroupId || name <> editor.Name.Trim()
        | None -> true

/// Der Auftrag aus dem Formular, `None` ohne Gruppe. Ändert sich der Schalter "Aktiv" einer bestehenden
/// Kategorie, geht zuerst das an BB; Name und Gruppe folgen nach der Antwort (`ActiveSaved`). So bleibt bei einer
/// Ablehnung alles unverändert.
let private request (editor: Editor) : Cmd<Msg> option =
    match editor with
    | EditingCategory ({ CategoryId = Some id; GroupId = Some _ } as e) when e.Active <> e.WasActive ->
        Some (setActiveCmd { CategoryId = id; Active = e.Active } ActiveSaved)
    | EditingCategory { CategoryId = id; Name = name; GroupId = Some groupId } ->
        Some (saveCategoryCmd { CategoryId = id; Name = name; GroupId = groupId })
    | EditingCategory _ -> None
    | EditingGroup group -> Some (saveGroupCmd { GroupId = group.GroupId; Name = group.Name })

let private withEditor (change: Editor -> Editor) (model: Model) : Model =
    match model.Form with
    | Some form when not form.Saving -> { model with Form = Some { form with Editor = change form.Editor; Error = None } }
    | _ -> model

/// Nach einem gelungenen Speichern die Liste still neu holen.
let private reload (model: Model) : Model * Cmd<Msg> =
    let generation = model.Generation + 1
    { model with Generation = generation }, loadCmd generation Reloaded

/// Die neue oder umbenannte Gruppe gleich in der Liste, bis das stille Neuladen sie bringt.
let private withGroup (id: Guid) (name: string) (model: Model) : Model =
    match model.Groups with
    | Success groups when groups |> List.exists (fun g -> g.Id = id) ->
        { model with Groups = Success (groups |> List.map (fun g -> if g.Id = id then { g with Name = name } else g)) }
    | Success groups -> { model with Groups = Success (groups @ [ { Id = id; Name = name; Categories = [] } ]) }
    | _ -> model

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | Entered ->
        let generation = model.Generation + 1
        { model with Groups = Loading; Form = None; Generation = generation }, loadCmd generation Loaded, NoOp
    | RetryLoad ->
        let generation = model.Generation + 1
        { model with Groups = Loading; Generation = generation }, loadCmd generation Loaded, NoOp
    | Loaded (generation, result) when generation = model.Generation ->
        let groups =
            match result with
            | Ok groups -> Success groups
            | Error message -> Failure message
        { model with Groups = groups }, Cmd.none, NoOp
    | Reloaded (generation, result) when generation = model.Generation ->
        match result with
        | Ok groups -> { model with Groups = Success groups }, Cmd.none, NoOp
        | Error message -> model, Cmd.none, ShowToast ($"Liste nicht neu geladen: {message}", ToastWarning)
    | Loaded _
    | Reloaded _ -> model, Cmd.none, NoOp
    | NewCategory ->
        let first = groupsOf model |> List.tryHead |> Option.map (fun g -> g.Id)
        { model with Form = opened (EditingCategory { CategoryId = None; Name = ""; GroupId = first; Active = true; WasActive = true }) },
        Cmd.none,
        NoOp
    | EditCategory id ->
        let found =
            groupsOf model
            |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> g, c))
        match found with
        | Some (group, category) ->
            { model with
                Form =
                    opened (
                        EditingCategory
                            { CategoryId = Some id
                              Name = category.Name
                              GroupId = Some group.Id
                              Active = category.Active
                              WasActive = category.Active }
                    ) },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp
    | NewGroup -> { model with Form = opened (EditingGroup { GroupId = None; Name = ""; ReturnTo = None }) }, Cmd.none, NoOp
    | RenameGroup id ->
        match groupsOf model |> List.tryFind (fun g -> g.Id = id) with
        | Some group -> { model with Form = opened (EditingGroup { GroupId = Some id; Name = group.Name; ReturnTo = None }) }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp
    | NameChanged name ->
        let named =
            function
            | EditingCategory editor -> EditingCategory { editor with Name = name }
            | EditingGroup editor -> EditingGroup { editor with Name = name }
        withEditor named model, Cmd.none, NoOp
    | GroupChosen NewGroupChoice ->
        let toGroupForm =
            function
            | EditingCategory editor -> EditingGroup { GroupId = None; Name = ""; ReturnTo = Some editor }
            | other -> other
        withEditor toGroupForm model, Cmd.none, NoOp
    | GroupChosen value ->
        match Guid.TryParse value with
        | true, groupId ->
            let chosen =
                function
                | EditingCategory editor -> EditingCategory { editor with GroupId = Some groupId }
                | other -> other
            withEditor chosen model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp
    | Save ->
        match model.Form with
        | Some form when canSave form ->
            match request form.Editor with
            | Some cmd -> { model with Form = Some { form with Saving = true; Error = None } }, cmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp
    | CloseForm ->
        match model.Form with
        | Some { Editor = EditingGroup { ReturnTo = Some editor }; Saving = false } ->
            { model with Form = opened (EditingCategory editor) }, Cmd.none, NoOp
        | _ -> { model with Form = None }, Cmd.none, NoOp
    | CategorySaved result ->
        let isOwn =
            match model.Form with
            | Some { Editor = EditingCategory _; Saving = true } -> true
            | _ -> false
        match result with
        | Ok _ ->
            let toast =
                match model.Form with
                | Some { Editor = EditingCategory { CategoryId = None; Name = name } } when isOwn -> $"{name.Trim()} angelegt"
                | Some { Notice = Some notice } when isOwn -> notice
                | _ -> "Gespeichert"
            let reloaded, cmd = reload { model with Form = if isOwn then None else model.Form }
            reloaded, cmd, ShowToast (toast, ToastSuccess)
        | Error message when isOwn ->
            { model with Form = model.Form |> Option.map (fun f -> { f with Saving = false; Error = Some message }) }, Cmd.none, NoOp
        | Error message -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | GroupSaved (name, result) ->
        let own =
            match model.Form with
            | Some { Editor = EditingGroup editor; Saving = true } -> Some editor
            | _ -> None
        match result, own with
        | Ok id, Some { ReturnTo = Some editor } ->
            let back = withGroup id name { model with Form = opened (EditingCategory { editor with GroupId = Some id }) }
            let reloaded, cmd = reload back
            reloaded, cmd, ShowToast ($"Gruppe {name} angelegt", ToastSuccess)
        | Ok id, _ ->
            let reloaded, cmd = reload (withGroup id name { model with Form = if own.IsSome then None else model.Form })
            reloaded, cmd, ShowToast ("Gruppe gespeichert", ToastSuccess)
        | Error message, Some _ ->
            { model with Form = model.Form |> Option.map (fun f -> { f with Saving = false; Error = Some message }) }, Cmd.none, NoOp
        | Error message, None -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | Leave -> { model with Form = None }, Cmd.none, BackToBudget
    | ActiveToggled active ->
        let toggled =
            function
            | EditingCategory editor when editor.CategoryId.IsSome -> EditingCategory { editor with Active = active }
            | other -> other
        withEditor toggled model, Cmd.none, NoOp
    | ActiveSaved result ->
        match model.Form with
        | Some ({ Editor = EditingCategory editor; Saving = true } as form) ->
            match result with
            | Ok () ->
                let name = editor.Name.Trim()
                let notice = if editor.Active then reactivatedNotice name else deactivatedNotice name
                let settled = { editor with WasActive = editor.Active }
                let marked =
                    match editor.CategoryId with
                    | Some id -> withActive id editor.Active model
                    | None -> model
                let form' = { form with Editor = EditingCategory settled; Notice = Some notice }
                match settled.GroupId with
                | Some groupId when fieldsChanged (groupsOf model) settled ->
                    { marked with Form = Some form' },
                    saveCategoryCmd { CategoryId = settled.CategoryId; Name = settled.Name; GroupId = groupId },
                    NoOp
                | _ ->
                    let reloaded, cmd = reload { marked with Form = None }
                    reloaded, cmd, ShowToast (notice, ToastSuccess)
            | Error message -> { model with Form = Some { form with Saving = false; Error = Some message } }, Cmd.none, NoOp
        | _ ->
            match result with
            | Ok () -> model, Cmd.none, NoOp
            | Error message -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | MoveCategory (categoryId, step) ->
        let target =
            groupsOf model
            |> List.tryFind (fun g -> g.Categories |> List.exists (fun c -> c.Id = categoryId))
            |> Option.bind (fun g -> movedOrder step categoryId g |> Option.map (fun order -> g, order))
        match target, model.Groups with
        | Some (group, order), Success groups ->
            let groups' = groups |> List.map (fun g -> if g.Id = group.Id then reordered order g else g)
            { model with Groups = Success groups' }, saveOrderCmd { GroupId = group.Id; CategoryIds = order }, NoOp
        | _ -> model, Cmd.none, NoOp
    | OrderSaved (Ok ()) -> model, Cmd.none, NoOp
    | OrderSaved (Error message) ->
        let reloaded, cmd = reload model
        reloaded, cmd, ShowToast ($"Reihenfolge nicht gespeichert: {message}", ToastError)
    | ToggleInactive -> { model with InactiveOpen = not model.InactiveOpen }, Cmd.none, NoOp
    | Reactivate categoryId ->
        let name =
            groupsOf model
            |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = categoryId))
            |> Option.map (fun c -> c.Name)
        match name with
        | Some name ->
            withActive categoryId true model,
            setActiveCmd { CategoryId = categoryId; Active = true } (fun result -> Reactivated (name, result)),
            NoOp
        | None -> model, Cmd.none, NoOp
    | Reactivated (name, Ok ()) ->
        let reloaded, cmd = reload model
        reloaded, cmd, ShowToast (reactivatedNotice name, ToastSuccess)
    | Reactivated (name, Error message) ->
        let reloaded, cmd = reload model
        reloaded, cmd, ShowToast ($"{name} nicht aktiviert: {message}", ToastError)
