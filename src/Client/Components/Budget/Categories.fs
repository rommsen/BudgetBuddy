/// Die Seite Kategorien `#/budget/categories` (budgeting-pscgb, ADR 0088 Punkt 1): Gruppen mit ihren Kategorien,
/// Kategorie anlegen und bearbeiten (Name, Gruppe), Gruppen anlegen und umbenennen. Alles geht nach YNAB. Ein
/// Formular ist offen, am Handy als Sheet, ab 1024 px als Panel (§7.6). "Neue Gruppe" im Auswahlfeld ersetzt das
/// Kategorie-Formular durch das Gruppen-Formular und kehrt nach dem Anlegen mit der neuen Gruppe zurück (§7.15,
/// Sheets werden nicht gestapelt). Reine Übergänge; Aufrufe von `Api` liegen hinter Lambdas.
/// Die Gruppe wählt man über ein Feld mit Pfeil, das eine kurze Auswahl der Gruppen öffnet (G13, design-system-gcpws,
/// Prototyp `cegroup`): am PC als Dialog, am Handy ersetzt sie den Inhalt des Sheets.
/// Deaktivieren (Schalter "Aktiv" im Formular, "Aktivieren" im Abschnitt Deaktiviert) und die Pfeile für BBs
/// Reihenfolge je Gruppe gehen nur an BB, nicht nach YNAB (budgeting-sn2sn, ADR 0088 Punkte 2 und 3). Ebenso eine
/// ganze Gruppe (budgeting-nyx75, Nachtrag ADR 0088 vom 2026-11-20): Schalter "Aktiv" im Formular "Gruppe bearbeiten",
/// eine deaktivierte Gruppe steht als ein Eintrag im Abschnitt Deaktiviert.
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

/// Das Gruppen-Formular. `ReturnTo` ist das Kategorie-Formular, aus dem "Neue Gruppe" kam. `Active` ist der Schalter
/// "Aktiv" einer bestehenden Gruppe, `WasActive` der Zustand beim Öffnen (budgeting-nyx75).
type GroupEditor = { GroupId: Guid option; Name: string; ReturnTo: CategoryEditor option; Active: bool; WasActive: bool }

type Editor =
    | EditingCategory of CategoryEditor
    | EditingGroup of GroupEditor

/// Der Aufruf, auf dessen Antwort das Formular wartet. Nur seine Antwort gibt die Sperre frei.
type SaveCall =
    | SavingCategory
    | SavingGroup
    | SavingActive
    /// Gruppe aktivieren oder deaktivieren aus dem Gruppen-Formular (budgeting-nyx75).
    | SavingGroupActive

/// Das offene Formular. `Saving`: der Aufruf läuft, Speichern und Schließen sind gesperrt. `Error`: die Meldung
/// des Servers, sie bleibt im Formular am Feld (`FailedCall`), bis etwas geändert wird. `Deactivated`: Deaktivieren
/// gelang, Name und Gruppe gehen noch hinaus; schließt das Formular, steht die Zeile im Bereich Deaktiviert mit dem Satz
/// zu YNAB. `GroupChoiceOpen`: die Auswahl der Gruppe steht offen über dem Formular (oberste Ebene, §7.8 Punkt 6).
type Form =
    { Editor: Editor
      Saving: SaveCall option
      Error: string option
      /// Welcher Aufruf abgelehnt wurde: Name und Gruppe stehen am Feld Name, Aktiv am Schalter.
      FailedCall: SaveCall option
      Deactivated: bool
      GroupChoiceOpen: bool }
    member form.IsSaving = form.Saving.IsSome

/// Der Aufruf für die Reihenfolge einer Gruppe, der gerade läuft. `Next`: die zuletzt gewünschte Reihenfolge, die
/// nach der Antwort hinausgeht. Je Gruppe läuft höchstens ein Aufruf, sonst könnten sich die Antworten überholen.
type OrderCall = { Next: Guid list option }

/// Was nach einer Aktion an ihrem Ort steht, bis zur nächsten Aktion (Regel R3, ADR 0098, budgeting-sx9v5). Am Handy und
/// am PC gleich, ohne Toast.
type Mark =
    /// Die Zeile der Kategorie leuchtet kurz (angelegt, gespeichert, wieder aktiv) und wird ins Bild gerollt.
    | CategoryLit of Guid
    /// Deaktiviert: der Eintrag (Kategorie oder Gruppe, budgeting-nyx75) im Bereich Deaktiviert leuchtet, darunter der
    /// Satz zu YNAB.
    | Deactivated of Guid
    /// Der Kopf der Gruppe leuchtet (umbenannt, wieder aktiv).
    | GroupLit of Guid
    /// Das Feld Gruppe im Kategorie-Formular leuchtet (neue Gruppe aus „Neue Gruppe“ angelegt).
    | GroupFieldLit
    /// Rot am Eintrag im Bereich Deaktiviert (Aktivieren einer Kategorie oder Gruppe abgelehnt).
    | ReactivateFailed of Guid * string
    /// Rot unter dem Kopf der Gruppe (Reihenfolge abgelehnt).
    | OrderFailed of Guid * string

/// Das stille Neuladen der Liste scheiterte: Hinweis oben auf der Seite, bis ein Neu-Lesen gelingt.
type ListStale = { Reason: string; Rereading: bool }

type Model = {
    Groups: RemoteData<ManagedCategoryGroup list>
    Form: Form option
    /// Zählt je Laden: eine Antwort mit fremder Generation ist überholt.
    Generation: int
    /// Der Abschnitt "Deaktiviert (n)" ist aufgeklappt.
    InactiveOpen: bool
    /// Je Gruppe der laufende Aufruf für die Reihenfolge.
    OrderCalls: Map<Guid, OrderCall>
    /// Die Rückmeldung der letzten Aktion an ihrem Ort.
    Mark: Mark option
    /// Zählt jedes Leuchten: das Ende eines älteren nimmt ein neueres nicht ab.
    MarkCount: int
    /// Das Leuchten läuft (`markFlashMs`).
    MarkLit: bool
    Stale: ListStale option
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
    /// "Bearbeiten" am Kopf einer Gruppe: öffnet "Gruppe bearbeiten" mit Name und Schalter "Aktiv" (budgeting-nyx75).
    | EditGroup of Guid
    | NameChanged of string
    /// Das Feld Gruppe öffnet die Auswahl der Gruppen (G13).
    | OpenGroupChoice
    /// Esc, Schließen oder der Hintergrund: nur die Auswahl geht zu, das Formular bleibt.
    | CloseGroupChoice
    /// Wahl in der Auswahl der Gruppe: eine Gruppen-Id oder `NewGroupChoice`. Schließt die Auswahl.
    | GroupChosen of string
    | Save
    /// "Abbrechen", Schließen oder Esc. Im Gruppen-Formular aus "Neue Gruppe" zurück zum Kategorie-Formular.
    | CloseForm
    | CategorySaved of Result<Guid, string>
    | GroupSaved of name: string * Result<Guid, string>
    /// "Zurück": zur Budget-Seite.
    | Leave
    /// Der Schalter "Aktiv" im Kategorie- oder Gruppen-Formular.
    | ActiveToggled of bool
    /// Antwort auf Deaktivieren oder Aktivieren aus dem Formular.
    | ActiveSaved of Result<unit, string>
    /// Pfeil hoch oder runter (oder Alt+↑/↓ am PC) an einer Kategorie.
    | MoveCategory of Guid * OrderStep
    | OrderSaved of groupId: Guid * Result<unit, string>
    /// Den Abschnitt "Deaktiviert (n)" auf- oder zuklappen.
    | ToggleInactive
    /// "Aktivieren" im Abschnitt Deaktiviert.
    | Reactivate of Guid
    | Reactivated of categoryId: Guid * Result<unit, string>
    /// Antwort auf Deaktivieren oder Aktivieren einer Gruppe aus dem Formular (budgeting-nyx75).
    | GroupActiveSaved of Result<unit, string>
    /// "Aktivieren" an einer Gruppe im Abschnitt Deaktiviert.
    | ReactivateGroup of Guid
    | GroupReactivated of groupId: Guid * Result<unit, string>
    /// „Neu laden“ am Hinweis „Liste nicht neu geladen“.
    | RereadList
    /// Das Leuchten endet, mit seiner Zählung.
    | MarkFlashEnded of count: int

type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | BackToBudget

let init () : Model =
    { Groups = NotAsked
      Form = None
      Generation = 0
      InactiveOpen = false
      OrderCalls = Map.empty
      Mark = None
      MarkCount = 0
      MarkLit = false
      Stale = None }

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

let private setGroupActiveCmd (save: CategoryGroupActiveSave) (answer: Result<unit, string> -> Msg) : Cmd<Msg> =
    Cmd.OfAsync.either (fun s -> Api.categories.setCategoryGroupActive s) save answer (fun ex -> answer (Error ex.Message))

let private saveOrderCmd (save: CategoryOrderSave) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun s -> Api.categories.setCategoryOrder s)
        save
        (fun result -> OrderSaved (save.GroupId, result))
        (fun ex -> OrderSaved (save.GroupId, Error ex.Message))

let private groupsOf (model: Model) : ManagedCategoryGroup list =
    match model.Groups with
    | Success groups -> groups
    | _ -> []

let private opened (editor: Editor) : Form option =
    Some { Editor = editor; Saving = None; Error = None; FailedCall = None; Deactivated = false; GroupChoiceOpen = false }

/// Die zweite Zeile einer Gruppe in der Auswahl (Prototyp `cegroup`): die Zahl ihrer aktiven Kategorien.
let categoryCountText (count: int) : string =
    if count = 1 then "1 Kategorie" else $"{count} Kategorien"

/// Der Satz unter der deaktivierten Zeile: die YNAB-API kann nicht ausblenden (ADR 0088 Punkt 2). Er ist Inhalt, er
/// bleibt bis zur nächsten Aktion.
[<Literal>]
let DeactivatedNotice = "In YNAB noch sichtbar, dort ausblenden."

/// Der Satz des Hinweises, wenn das stille Neuladen der Liste scheiterte.
[<Literal>]
let ListStaleText = "Liste nicht neu geladen, sie kann veraltet sein."

/// Wie lange eine Zeile, ein Kopf oder ein Feld leuchtet: 0,7 s Animation (`.feedback-flash`), danach nimmt die Seite
/// die Klasse ab. Mit `prefers-reduced-motion` steht der Ring so lange ohne Bewegung.
let markFlashMs = 900

/// Der Satz unter dem Schalter "Aktiv" im Gruppen-Formular (Skizze Version 4, Abschnitt 2, budgeting-nyx75).
[<Literal>]
let GroupActiveSwitchText =
    "Deaktiviert verschwindet sie samt ihren Kategorien aus Auswahl und Budget. Buchungen und Historie bleiben. Nur in BB, YNAB bleibt unverändert."

/// Die Gruppen, die oben auf der Seite stehen und in der Auswahl der Gruppe: die aktiven, in YNABs Folge
/// (budgeting-nyx75).
let activeGroups (groups: ManagedCategoryGroup list) : ManagedCategoryGroup list =
    groups |> List.filter (fun g -> g.Active)

/// Die Kategorien einer Gruppe, die auf der Seite in ihrer Karte stehen: die aktiven, in BBs Reihenfolge.
let activeCategories (group: ManagedCategoryGroup) : ManagedCategory list =
    group.Categories |> List.filter (fun c -> c.Active)

/// Die zweite Zeile einer Gruppe in der Auswahl der Gruppe (Prototyp `cegroup`): die Zahl ihrer aktiven Kategorien,
/// die deaktivierten zählen nicht.
let groupChoiceDetail (group: ManagedCategoryGroup) : string =
    categoryCountText (activeCategories group).Length

/// Ein Eintrag im Abschnitt Deaktiviert (budgeting-nyx75): eine deaktivierte Gruppe als ein Eintrag (ihre Kategorien
/// nicht einzeln) oder eine einzeln deaktivierte Kategorie einer aktiven Gruppe.
type InactiveEntry =
    | InactiveGroup of ManagedCategoryGroup
    | InactiveCategory of ManagedCategoryGroup * ManagedCategory

/// Der Abschnitt Deaktiviert: zuerst die deaktivierten Gruppen in YNABs Folge, dann jede einzeln deaktivierte Kategorie
/// einer aktiven Gruppe mit ihrer Gruppe, Gruppe für Gruppe. Die Zahl im Kopf zählt diese Einträge.
let inactiveEntries (groups: ManagedCategoryGroup list) : InactiveEntry list =
    (groups |> List.filter (fun g -> not g.Active) |> List.map InactiveGroup)
    @ (activeGroups groups
       |> List.collect (fun g -> g.Categories |> List.filter (fun c -> not c.Active) |> List.map (fun c -> InactiveCategory (g, c))))

/// Die zweite Zeile einer deaktivierten Gruppe im Abschnitt Deaktiviert (Skizze): "Gruppe · 3 Kategorien", ohne
/// Kategorien "Gruppe · keine Kategorien". Gezählt werden alle ihre Kategorien, sie verschwinden alle mit ihr.
let inactiveGroupDetail (group: ManagedCategoryGroup) : string =
    match group.Categories.Length with
    | 0 -> "Gruppe · keine Kategorien"
    | count -> $"Gruppe · {categoryCountText count}"

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

/// Hat der Pfeil `step` an der Kategorie eine Wirkung? In der ersten Zeile nicht nach oben, in der letzten nicht
/// nach unten; der View dämpft den Pfeil dann.
let canMove (step: OrderStep) (categoryId: Guid) (group: ManagedCategoryGroup) : bool =
    (movedOrder step categoryId group).IsSome

/// Was die Liste der Kategorien kann (§7.8, F5): keine Mehrfachauswahl, keine Tabs, keine Suche.
let categoryListTraits : Client.DesignSystem.ListKeys.Traits =
    { MultiSelect = false; Tabs = false; Search = false; TypeToSearch = false }

/// Die aktiven Kategorien in der Reihenfolge der Seite, Gruppe für Gruppe, ohne deaktivierte Gruppen.
let private shownOrder (groups: ManagedCategoryGroup list) : Guid list =
    activeGroups groups |> List.collect (fun g -> activeCategories g |> List.map (fun c -> c.Id))

/// Das Formular einer Kategorie im Stand der Liste, wie "Bearbeiten" es öffnet.
let private categoryEditorOf (group: ManagedCategoryGroup) (category: ManagedCategory) : CategoryEditor =
    { CategoryId = Some category.Id
      Name = category.Name
      GroupId = Some group.Id
      Active = category.Active
      WasActive = category.Active }

/// Darf ↑↓ zur nächsten Kategorie wechseln? Ohne Formular ja. Mit Formular nur, wenn es eine bestehende Kategorie
/// unverändert zeigt und nicht speichert: sonst ginge die Eingabe verloren, und aus „Neue Kategorie“ und dem
/// Gruppen-Formular öffnete ↓ die erste Kategorie.
let private canLeaveForm (model: Model) : bool =
    match model.Form with
    | None -> true
    | Some form when form.IsSaving -> false
    | Some { Editor = EditingCategory ({ CategoryId = Some id } as editor) } ->
        groupsOf model
        |> List.exists (fun g -> g.Categories |> List.exists (fun c -> c.Id = id && categoryEditorOf g c = editor))
    | Some _ -> false

/// Die Tasten am PC über die gemeinsame Regel (`ListKeys.actionFor`, design-system-gcpws, F5): ↓ ohne offene
/// Kategorie öffnet die erste, ↑ die letzte im Panel, danach die nächste bzw. vorige; am Rand bleibt es stehen. Esc
/// schließt das Panel, auch aus dem Feld Name. In einem Feld gehören die Pfeile dem Cursor, Alt+↑↓ verschiebt die
/// Kategorie (`moveKey` an der Zeile). Ein geändertes, neues oder speicherndes Formular und das Gruppen-Formular
/// lassen ↑↓ stehen (`canLeaveForm`).
let categoryKeyAction
    (model: Model)
    (focus: Client.DesignSystem.ListKeys.Focus)
    (command: bool)
    (shift: bool)
    (handled: bool)
    (key: string)
    : Msg option =
    let order = shownOrder (groupsOf model)
    let current =
        match model.Form with
        | Some { Editor = EditingCategory { CategoryId = Some id } } -> order |> List.tryFindIndex ((=) id)
        | _ -> None
    // Die offene Auswahl der Gruppe ist die oberste Ebene: Esc schließt nur sie, die Liste ruht (§7.8 Punkt 6).
    let choosing = model.Form |> Option.exists (fun f -> f.GroupChoiceOpen)
    let action =
        Client.DesignSystem.ListKeys.actionFor
            categoryListTraits
            { Key = key
              Focus = focus
              Command = command
              Shift = shift
              Handled = handled
              LayerOpen = choosing
              RowSelected = current.IsSome
              PanelOpen = model.Form.IsSome
              SelectionActive = false }
    let openAt index =
        match List.tryItem index order with
        | Some id when Some index <> current && canLeaveForm model -> Some (EditCategory id)
        | _ -> None
    match action, current with
    | _ when choosing -> if key = "Escape" && not handled then Some CloseGroupChoice else None
    | Client.DesignSystem.ListKeys.ClosePanel, _ -> Some CloseForm
    | Client.DesignSystem.ListKeys.FirstRow, _ -> openAt 0
    | Client.DesignSystem.ListKeys.LastRow, _ -> openAt (List.length order - 1)
    | Client.DesignSystem.ListKeys.StepRow Client.DesignSystem.ListKeys.Forward, Some i -> openAt (i + 1)
    | Client.DesignSystem.ListKeys.StepRow Client.DesignSystem.ListKeys.Backward, Some i -> openAt (i - 1)
    | _ -> None

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

/// Setzt `Active` einer Gruppe in der Liste, bis das stille Neuladen den Stand bringt. Ihre Kategorien behalten ihren
/// eigenen Zustand (budgeting-nyx75).
let private withGroupActive (groupId: Guid) (active: bool) (model: Model) : Model =
    match model.Groups with
    | Success groups ->
        { model with Groups = Success (groups |> List.map (fun g -> if g.Id = groupId then { g with Active = active } else g)) }
    | _ -> model

/// Der Titel des Formulars wie im Prototyp.
let formTitle (editor: Editor) : string =
    match editor with
    | EditingCategory { CategoryId = None } -> "Neue Kategorie"
    | EditingCategory _ -> "Kategorie bearbeiten"
    | EditingGroup { GroupId = None } -> "Neue Gruppe"
    | EditingGroup _ -> "Gruppe bearbeiten"

/// Die Beschriftung des Hauptknopfs: "Anlegen" für eine neue Kategorie, sonst "Speichern" (Prototyp).
let saveLabel (editor: Editor) : string =
    match editor with
    | EditingCategory { CategoryId = None } -> "Anlegen"
    | _ -> "Speichern"

/// Darf "Speichern" gedrückt werden? Nicht, solange der Aufruf läuft, und nicht ohne Gruppe.
let canSave (form: Form) : bool =
    not form.IsSaving
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

/// Hat das Gruppen-Formular den Namen gegenüber der Liste geändert? Eine neue Gruppe gilt als geändert.
let private groupNameChanged (groups: ManagedCategoryGroup list) (editor: GroupEditor) : bool =
    match editor.GroupId with
    | None -> true
    | Some id ->
        match groups |> List.tryFind (fun g -> g.Id = id) with
        | Some group -> group.Name <> editor.Name.Trim()
        | None -> true

/// Der Auftrag aus dem Formular, `None` ohne Gruppe. Ändert sich der Schalter "Aktiv" einer bestehenden
/// Kategorie oder Gruppe (budgeting-nyx75), geht zuerst das an BB; Name und Gruppe folgen nach der Antwort
/// (`ActiveSaved`, `GroupActiveSaved`). So bleibt bei einer Ablehnung alles unverändert.
let private request (editor: Editor) : (SaveCall * Cmd<Msg>) option =
    match editor with
    | EditingGroup ({ GroupId = Some id } as g) when g.Active <> g.WasActive ->
        Some (SavingGroupActive, setGroupActiveCmd { GroupId = id; Active = g.Active } GroupActiveSaved)
    | EditingCategory ({ CategoryId = Some id; GroupId = Some _ } as e) when e.Active <> e.WasActive ->
        Some (SavingActive, setActiveCmd { CategoryId = id; Active = e.Active } ActiveSaved)
    | EditingCategory { CategoryId = id; Name = name; GroupId = Some groupId } ->
        Some (SavingCategory, saveCategoryCmd { CategoryId = id; Name = name; GroupId = groupId })
    | EditingCategory _ -> None
    | EditingGroup group -> Some (SavingGroup, saveGroupCmd { GroupId = group.GroupId; Name = group.Name })

/// Die Auswahl der Gruppe ist zu (nach einer Wahl).
let private withChoiceClosed (model: Model) : Model =
    { model with Form = model.Form |> Option.map (fun f -> if f.IsSaving then f else { f with GroupChoiceOpen = false }) }

let private withEditor (change: Editor -> Editor) (model: Model) : Model =
    match model.Form with
    | Some form when not form.IsSaving -> { model with Form = Some { form with Editor = change form.Editor; Error = None } }
    | _ -> model

/// Eine neue Generation: eine laufende Antwort beim Laden ist danach überholt und ersetzt die Liste nicht.
let private bumped (model: Model) : Model = { model with Generation = model.Generation + 1 }

/// Nach einem gelungenen Speichern die Liste still neu holen.
let private reload (model: Model) : Model * Cmd<Msg> =
    let generation = model.Generation + 1
    { model with Generation = generation }, loadCmd generation Reloaded

/// Die neue oder umbenannte Gruppe gleich in der Liste, bis das stille Neuladen sie bringt.
let private withGroup (id: Guid) (name: string) (model: Model) : Model =
    match model.Groups with
    | Success groups when groups |> List.exists (fun g -> g.Id = id) ->
        { model with Groups = Success (groups |> List.map (fun g -> if g.Id = id then { g with Name = name } else g)) }
    | Success groups -> { model with Groups = Success (groups @ [ { Id = id; Name = name; Active = true; Categories = [] } ]) }
    | _ -> model

/// Die neue oder geänderte Kategorie gleich in der Liste (Name, Gruppe), bis das stille Neuladen sie bringt: so steht
/// die Zeile schon da, wenn sie leuchtet.
let private withCategory (id: Guid) (editor: CategoryEditor) (model: Model) : Model =
    match model.Groups, editor.GroupId with
    | Success groups, Some groupId ->
        let existing = groups |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = id))
        let category: ManagedCategory =
            { Id = id
              Name = editor.Name.Trim()
              Active = existing |> Option.map (fun c -> c.Active) |> Option.defaultValue true }
        let placed =
            groups
            |> List.map (fun g ->
                let others = g.Categories |> List.filter (fun c -> c.Id <> id)
                if g.Id <> groupId then { g with Categories = others }
                else
                    match g.Categories |> List.tryFindIndex (fun c -> c.Id = id) with
                    | Some index -> { g with Categories = List.insertAt index category others }
                    | None -> { g with Categories = others @ [ category ] })
        { model with Groups = Success placed }
    | _ -> model

/// Setzt die Rückmeldung an ihren Ort. Ein Leuchten zählt hoch und bekommt seinen Zeitgeber; ein Fehler leuchtet nicht.
let private marked (mark: Mark) (model: Model) : Model * Cmd<Msg> =
    match mark with
    | ReactivateFailed _
    | OrderFailed _ -> { model with Mark = Some mark; MarkLit = false }, Cmd.none
    | _ ->
        let count = model.MarkCount + 1
        { model with Mark = Some mark; MarkCount = count; MarkLit = true },
        Client.Debounce.delayed markFlashMs (MarkFlashEnded count)

/// Endet mit dieser Nachricht die Rückmeldung? Jede Aktion auf der Seite beendet sie; Aufklappen des Abschnitts
/// Deaktiviert, die Auswahl der Gruppe öffnen und schließen, Antworten und Laden nicht.
let endsMark (msg: Msg) : bool =
    match msg with
    | Entered
    | RetryLoad
    | RereadList
    | NewCategory
    | EditCategory _
    | NewGroup
    | EditGroup _
    | NameChanged _
    | GroupChosen _
    | Save
    | CloseForm
    | Leave
    | ActiveToggled _
    | MoveCategory _
    | Reactivate _
    | ReactivateGroup _ -> true
    | _ -> false

/// Speichert das offene Formular gerade? Dann öffnet kein anderes: die Antwort gehört ihm.
let private saving (model: Model) : bool =
    model.Form |> Option.exists (fun f -> f.IsSaving)

/// Der Fehler des Servers im eigenen Formular, am Feld des abgelehnten Aufrufs.
let private failedIn (call: SaveCall) (message: string) (model: Model) : Model =
    { model with Form = model.Form |> Option.map (fun f -> { f with Saving = None; Error = Some message; FailedCall = Some call }) }

let private transition (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | Entered ->
        let generation = model.Generation + 1
        { model with Groups = Loading; Form = None; Generation = generation; Stale = None }, loadCmd generation Loaded, NoOp
    | RetryLoad ->
        let generation = model.Generation + 1
        { model with Groups = Loading; Generation = generation }, loadCmd generation Loaded, NoOp
    | Loaded (generation, result) when generation = model.Generation ->
        match result with
        | Ok groups -> { model with Groups = Success groups; Stale = None }, Cmd.none, NoOp
        | Error message -> { model with Groups = Failure message }, Cmd.none, NoOp
    | Reloaded (generation, result) when generation = model.Generation ->
        match result with
        | Ok groups -> { model with Groups = Success groups; Stale = None }, Cmd.none, NoOp
        // Ein Zustand der Seite (ADR 0098): Hinweis oben, die Liste bleibt stehen.
        | Error message -> { model with Stale = Some { Reason = message; Rereading = false } }, Cmd.none, NoOp
    | Loaded _
    | Reloaded _ -> model, Cmd.none, NoOp
    | RereadList ->
        match model.Stale with
        | Some stale when not stale.Rereading ->
            let reloaded, cmd = reload { model with Stale = Some { stale with Rereading = true } }
            reloaded, cmd, NoOp
        | _ -> model, Cmd.none, NoOp
    | MarkFlashEnded count ->
        if count = model.MarkCount then { model with MarkLit = false }, Cmd.none, NoOp else model, Cmd.none, NoOp
    | NewCategory | EditCategory _ | NewGroup | EditGroup _ when saving model -> model, Cmd.none, NoOp
    | NewCategory ->
        // In eine deaktivierte Gruppe legt BB nichts an (budgeting-nyx75): vorbelegt ist die erste aktive.
        let first = activeGroups (groupsOf model) |> List.tryHead |> Option.map (fun g -> g.Id)
        { model with Form = opened (EditingCategory { CategoryId = None; Name = ""; GroupId = first; Active = true; WasActive = true }) },
        Cmd.none,
        NoOp
    | EditCategory id ->
        let found =
            groupsOf model
            |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> g, c))
        match found with
        | Some (group, category) -> { model with Form = opened (EditingCategory (categoryEditorOf group category)) }, Cmd.none, NoOp
        | None -> model, Cmd.none, NoOp
    | NewGroup ->
        { model with Form = opened (EditingGroup { GroupId = None; Name = ""; ReturnTo = None; Active = true; WasActive = true }) },
        Cmd.none,
        NoOp
    | EditGroup id ->
        match groupsOf model |> List.tryFind (fun g -> g.Id = id) with
        | Some group ->
            { model with
                Form = opened (EditingGroup { GroupId = Some id; Name = group.Name; ReturnTo = None; Active = group.Active; WasActive = group.Active }) },
            Cmd.none,
            NoOp
        | None -> model, Cmd.none, NoOp
    | NameChanged name ->
        let named =
            function
            | EditingCategory editor -> EditingCategory { editor with Name = name }
            | EditingGroup editor -> EditingGroup { editor with Name = name }
        withEditor named model, Cmd.none, NoOp
    | OpenGroupChoice ->
        match model.Form with
        | Some ({ Editor = EditingCategory _ } as form) when not form.IsSaving ->
            { model with Form = Some { form with GroupChoiceOpen = true } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp
    | CloseGroupChoice ->
        { model with Form = model.Form |> Option.map (fun f -> { f with GroupChoiceOpen = false }) }, Cmd.none, NoOp
    | GroupChosen NewGroupChoice ->
        let toGroupForm =
            function
            | EditingCategory editor -> EditingGroup { GroupId = None; Name = ""; ReturnTo = Some editor; Active = true; WasActive = true }
            | other -> other
        withChoiceClosed (withEditor toGroupForm model), Cmd.none, NoOp
    | GroupChosen value ->
        match Guid.TryParse value with
        | true, groupId ->
            let chosen =
                function
                | EditingCategory editor -> EditingCategory { editor with GroupId = Some groupId }
                | other -> other
            withChoiceClosed (withEditor chosen model), Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp
    | Save ->
        match model.Form with
        | Some form when canSave form ->
            match request form.Editor with
            | Some (call, cmd) -> { model with Form = Some { form with Saving = Some call; Error = None } }, cmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp
    | CloseForm ->
        match model.Form with
        // Während der Aufruf läuft, bleibt das Formular offen: sonst gingen ein Fehler oder nach dem Deaktivieren
        // Name und Gruppe verloren.
        | Some form when form.IsSaving -> model, Cmd.none, NoOp
        | Some { Editor = EditingGroup { ReturnTo = Some editor } } ->
            { model with Form = opened (EditingCategory editor) }, Cmd.none, NoOp
        | _ -> { model with Form = None }, Cmd.none, NoOp
    | CategorySaved result ->
        let isOwn =
            match model.Form with
            | Some { Editor = EditingCategory _; Saving = Some SavingCategory } -> true
            | _ -> false
        match result, model.Form with
        | Ok id, Some ({ Editor = EditingCategory editor } as form) when isOwn ->
            // Formular zu, die Zeile leuchtet an ihrem Platz; nach dem Deaktivieren im Bereich Deaktiviert mit dem Satz zu
            // YNAB (Regel R3, budgeting-sx9v5).
            let placed = withCategory id editor { model with Form = None }
            let mark, opened =
                if form.Deactivated then Deactivated id, { placed with InactiveOpen = true } else CategoryLit id, placed
            let lit, flashCmd = marked mark opened
            let reloaded, cmd = reload lit
            reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
        // Ohne eigenes Formular ist man nur nach "Zurück": die Meldung hat keinen Ort (Leiste, am PC der Toast).
        | Ok _, _ ->
            let reloaded, cmd = reload model
            reloaded, cmd, ShowToast ("Gespeichert", ToastSuccess)
        | Error message, _ when isOwn -> failedIn SavingCategory message model, Cmd.none, NoOp
        | Error message, _ -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | GroupSaved (name, result) ->
        let own =
            match model.Form with
            | Some { Editor = EditingGroup editor; Saving = Some SavingGroup } -> Some editor
            | _ -> None
        match result, own with
        | Ok id, Some { ReturnTo = Some editor } ->
            // Zurück im Kategorie-Formular steht die neue Gruppe im Feld Gruppe, das Feld leuchtet kurz.
            let back = withGroup id name { model with Form = opened (EditingCategory { editor with GroupId = Some id }) }
            let lit, flashCmd = marked GroupFieldLit back
            let reloaded, cmd = reload lit
            reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
        | Ok id, Some _ ->
            // Der Kopf der Gruppe leuchtet kurz. Nach dem Deaktivieren mit neuem Namen (budgeting-nyx75) steht die Gruppe
            // im Bereich Deaktiviert (aufgeklappt) mit dem Satz zu YNAB.
            let deactivated = model.Form |> Option.exists (fun f -> f.Deactivated)
            let named = withGroup id name { model with Form = None }
            let mark, placed = if deactivated then Deactivated id, { named with InactiveOpen = true } else GroupLit id, named
            let lit, flashCmd = marked mark placed
            let reloaded, cmd = reload lit
            reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
        | Ok id, None ->
            let reloaded, cmd = reload (withGroup id name model)
            reloaded, cmd, ShowToast ("Gruppe gespeichert", ToastSuccess)
        | Error message, Some _ -> failedIn SavingGroup message model, Cmd.none, NoOp
        | Error message, None -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | Leave -> { model with Form = None }, Cmd.none, BackToBudget
    | ActiveToggled active ->
        let toggled =
            function
            | EditingCategory editor when editor.CategoryId.IsSome -> EditingCategory { editor with Active = active }
            | EditingGroup editor when editor.GroupId.IsSome -> EditingGroup { editor with Active = active }
            | other -> other
        withEditor toggled model, Cmd.none, NoOp
    | ActiveSaved result ->
        match model.Form with
        | Some ({ Editor = EditingCategory editor; Saving = Some SavingActive } as form) ->
            match result with
            | Ok () ->
                let settled = { editor with WasActive = editor.Active }
                let changed =
                    match editor.CategoryId with
                    | Some id -> withActive id editor.Active model
                    | None -> model
                let form' =
                    { form with Editor = EditingCategory settled; Deactivated = not editor.Active; Saving = Some SavingCategory }
                match settled.GroupId, editor.CategoryId with
                | Some groupId, _ when fieldsChanged (groupsOf model) settled ->
                    { changed with Form = Some form' },
                    saveCategoryCmd { CategoryId = settled.CategoryId; Name = settled.Name; GroupId = groupId },
                    NoOp
                | _, Some id ->
                    // Deaktiviert: die Zeile steht im Bereich Deaktiviert (aufgeklappt) mit dem Satz zu YNAB; aktiviert:
                    // wieder in ihrer Gruppe. Beide leuchten kurz.
                    let closed = { changed with Form = None; InactiveOpen = changed.InactiveOpen || not editor.Active }
                    let lit, flashCmd = marked (if editor.Active then CategoryLit id else Deactivated id) closed
                    let reloaded, cmd = reload lit
                    reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
                | _, None ->
                    let reloaded, cmd = reload { changed with Form = None }
                    reloaded, cmd, NoOp
            | Error message -> failedIn SavingActive message model, Cmd.none, NoOp
        | _ ->
            match result with
            // Ohne eigenes Formular (etwa nach "Zurück"): BB hat geändert, die Liste holt den Stand.
            | Ok () ->
                let reloaded, cmd = reload model
                reloaded, cmd, NoOp
            | Error message -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | MoveCategory (categoryId, step) ->
        let target =
            groupsOf model
            |> List.tryFind (fun g -> g.Categories |> List.exists (fun c -> c.Id = categoryId))
            |> Option.bind (fun g -> movedOrder step categoryId g |> Option.map (fun order -> g, order))
        match target, model.Groups with
        | Some (group, order), Success groups ->
            let groups' = groups |> List.map (fun g -> if g.Id = group.Id then reordered order g else g)
            let moved = bumped { model with Groups = Success groups' }
            match Map.tryFind group.Id model.OrderCalls with
            | Some _ -> { moved with OrderCalls = Map.add group.Id { Next = Some order } model.OrderCalls }, Cmd.none, NoOp
            | None ->
                { moved with OrderCalls = Map.add group.Id { Next = None } model.OrderCalls },
                saveOrderCmd { GroupId = group.Id; CategoryIds = order },
                NoOp
        | _ -> model, Cmd.none, NoOp
    | OrderSaved (groupId, Ok ()) ->
        match Map.tryFind groupId model.OrderCalls with
        | Some { Next = Some order } ->
            { model with OrderCalls = Map.add groupId { Next = None } model.OrderCalls },
            saveOrderCmd { GroupId = groupId; CategoryIds = order },
            NoOp
        | _ -> { model with OrderCalls = Map.remove groupId model.OrderCalls }, Cmd.none, NoOp
    | OrderSaved (groupId, Error message) ->
        // Die wartende Reihenfolge fällt weg, das Neuladen zeigt den Stand von BB.
        // Rot unter dem Kopf der Gruppe, bis zur nächsten Aktion.
        let failed, _ =
            marked (OrderFailed (groupId, $"Reihenfolge nicht gespeichert: {message}")) { model with OrderCalls = Map.remove groupId model.OrderCalls }
        let reloaded, cmd = reload failed
        reloaded, cmd, NoOp
    | ToggleInactive -> { model with InactiveOpen = not model.InactiveOpen }, Cmd.none, NoOp
    | Reactivate categoryId ->
        let known = groupsOf model |> List.exists (fun g -> g.Categories |> List.exists (fun c -> c.Id = categoryId))
        if known then
            bumped (withActive categoryId true model),
            setActiveCmd { CategoryId = categoryId; Active = true } (fun result -> Reactivated (categoryId, result)),
            NoOp
        else
            model, Cmd.none, NoOp
    | Reactivated (categoryId, Ok ()) ->
        // Die Zeile steht wieder in ihrer Gruppe und leuchtet kurz.
        let lit, flashCmd = marked (CategoryLit categoryId) model
        let reloaded, cmd = reload lit
        reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
    | Reactivated (categoryId, Error message) ->
        // Zurück in den Bereich Deaktiviert, rot an ihrer Zeile.
        let back = { withActive categoryId false model with InactiveOpen = true }
        let failed, _ = marked (ReactivateFailed (categoryId, $"Nicht aktiviert: {message}")) back
        let reloaded, cmd = reload failed
        reloaded, cmd, NoOp
    | GroupActiveSaved result ->
        match model.Form with
        | Some ({ Editor = EditingGroup editor; Saving = Some SavingGroupActive } as form) ->
            match result, editor.GroupId with
            | Ok (), Some id ->
                let settled = { editor with WasActive = editor.Active }
                let changed = withGroupActive id editor.Active model
                if groupNameChanged (groupsOf model) settled then
                    // Der Schalter ging zuerst an BB, der Name geht danach nach YNAB.
                    let form' = { form with Editor = EditingGroup settled; Deactivated = not editor.Active; Saving = Some SavingGroup }
                    { changed with Form = Some form' }, saveGroupCmd { GroupId = Some id; Name = settled.Name }, NoOp
                else
                    // Deaktiviert: der Eintrag steht im Bereich Deaktiviert (aufgeklappt) mit dem Satz zu YNAB; aktiviert:
                    // die Gruppe wieder an ihrer Stelle, der Kopf leuchtet kurz.
                    let closed = { changed with Form = None; InactiveOpen = changed.InactiveOpen || not editor.Active }
                    let lit, flashCmd = marked (if editor.Active then GroupLit id else Deactivated id) closed
                    let reloaded, cmd = reload lit
                    reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
            | Ok (), None ->
                let reloaded, cmd = reload { model with Form = None }
                reloaded, cmd, NoOp
            // Die Ablehnung steht am Schalter, nie als Toast.
            | Error message, _ -> failedIn SavingGroupActive message model, Cmd.none, NoOp
        | _ ->
            match result with
            // Ohne eigenes Formular (etwa nach "Zurück"): BB hat geändert, die Liste holt den Stand.
            | Ok () ->
                let reloaded, cmd = reload model
                reloaded, cmd, NoOp
            | Error message -> model, Cmd.none, ShowToast ($"Nicht gespeichert: {message}", ToastError)
    | ReactivateGroup groupId ->
        let known = groupsOf model |> List.exists (fun g -> g.Id = groupId && not g.Active)
        if known then
            bumped (withGroupActive groupId true model),
            setGroupActiveCmd { GroupId = groupId; Active = true } (fun result -> GroupReactivated (groupId, result)),
            NoOp
        else
            model, Cmd.none, NoOp
    | GroupReactivated (groupId, Ok ()) ->
        // Die Gruppe steht wieder an ihrer Stelle, ihr Kopf leuchtet kurz; einzeln deaktivierte Kategorien bleiben im
        // Bereich Deaktiviert.
        let lit, flashCmd = marked (GroupLit groupId) model
        let reloaded, cmd = reload lit
        reloaded, Cmd.batch [ cmd; flashCmd ], NoOp
    | GroupReactivated (groupId, Error message) ->
        // Zurück in den Bereich Deaktiviert, rot am Eintrag.
        let back = { withGroupActive groupId false model with InactiveOpen = true }
        let failed, _ = marked (ReactivateFailed (groupId, $"Nicht aktiviert: {message}")) back
        let reloaded, cmd = reload failed
        reloaded, cmd, NoOp


/// Der Einstieg: eine Aktion beendet die Rückmeldung der vorigen (`endsMark`), dann wirkt die Nachricht.
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let model = if endsMark msg then { model with Mark = None; MarkLit = false } else model
    transition msg model
