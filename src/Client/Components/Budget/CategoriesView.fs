/// Die Seite Kategorien `#/budget/categories` (budgeting-pscgb, Form nach Prototyp Version 17): Kopf mit Zurück und
/// Titel, ein Satz Erklärung, je Gruppe ein Abschnitt mit "Umbenennen" und einer Karte mit den Kategorien, darunter
/// "+ Kategorie" und "+ Gruppe", darunter der eingeklappte Abschnitt "Deaktiviert (n)" (budgeting-sn2sn). Jede
/// Kategorie hat Pfeile hoch und runter für BBs Reihenfolge. Das Formular steht am Handy im Sheet, ab 1024 px als
/// Panel rechts (§7.6).
module Components.Budget.CategoriesView

open Feliz
open Shared.Budgeting
open Types
open Components.Budget.Categories
open Client.DesignSystem
open Client.DesignSystem.Tokens

let private cardClass = "rounded-2xl border border-border-subtle bg-surface-card overflow-clip"

/// Der Satz unter dem Titel, wie im Prototyp.
let IntroText =
    "Antippen zum Umbenennen, Verschieben oder Deaktivieren. Deaktivierte Kategorien verschwinden aus Auswahl und Budget, ihre Buchungen und Historie bleiben."

/// Der Satz unter dem Schalter "Aktiv" nach dem Prototyp, ergänzt um "Nur in BB, YNAB bleibt unverändert." (Entscheidung
/// Roman im Review-Lauf e83f2d00..ad18f0c9, Abweichung von Prototyp Version 17).
let ActiveSwitchText = "Deaktiviert verschwindet sie aus Auswahl und Budget. Buchungen und Historie bleiben. Nur in BB, YNAB bleibt unverändert."

let private header (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "categories-head")
        prop.className "flex items-baseline gap-3"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = "Zurück"
                    Variant = Button.Ghost
                    Size = Button.Small
                    AriaLabel = Some "Zurück zum Budget"
                    ClassName = Some "-ml-3"
                    OnClick = fun () -> dispatch Leave }
            Html.h1 [ prop.className $"text-xl {FontWeights.semibold} {Colors.textPrimary}"; prop.text "Kategorien" ]
        ]
    ]

let private groupSection (group: ManagedCategoryGroup) (dispatch: Msg -> unit) =
    Html.section [
        prop.key (string group.Id)
        prop.custom ("data-testid", "categories-group")
        prop.className "flex flex-col gap-2"
        prop.children [
            Html.div [
                prop.className "flex items-center justify-between gap-3 min-w-0"
                prop.children [
                    Html.h2 (Truncate.textProps ListRoles.groupHeader group.Name)
                    Html.div [
                        prop.className "shrink-0"
                        prop.children [
                            Button.quietTextFor
                                "categories-rename-group"
                                $"Gruppe {group.Name} umbenennen"
                                "Umbenennen"
                                (fun () -> dispatch (RenameGroup group.Id))
                        ]
                    ]
                ]
            ]
            let active = activeCategories group
            if not active.IsEmpty then
                Html.div [
                    prop.className cardClass
                    prop.children [
                        for category in active do
                            Button.orderNameRow
                                (string category.Id)
                                "categories-row"
                                $"{category.Name} bearbeiten"
                                category.Name
                                (fun () -> dispatch (EditCategory category.Id))
                                (fun e ->
                                    match moveKey e.key e.altKey with
                                    | Some step ->
                                        e.preventDefault ()
                                        dispatch (MoveCategory (category.Id, step))
                                    | None -> ())
                                (canMove StepUp category.Id group)
                                (canMove StepDown category.Id group)
                                (fun () -> dispatch (MoveCategory (category.Id, StepUp)))
                                (fun () -> dispatch (MoveCategory (category.Id, StepDown)))
                    ]
                ]
        ]
    ]

let private addButtons (dispatch: Msg -> unit) =
    Html.div [
        prop.className "grid grid-cols-2 gap-3"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = "Kategorie"
                    Variant = Button.Secondary
                    FullWidth = true
                    Icon = Some (Icons.plus Icons.SM Icons.Default)
                    AriaLabel = Some "Neue Kategorie"
                    OnClick = fun () -> dispatch NewCategory }
            Button.view
                { Button.defaultProps with
                    Text = "Gruppe"
                    Variant = Button.Secondary
                    FullWidth = true
                    Icon = Some (Icons.plus Icons.SM Icons.Default)
                    AriaLabel = Some "Neue Gruppe"
                    OnClick = fun () -> dispatch NewGroup }
        ]
    ]

/// Der Abschnitt "Deaktiviert (n)": eingeklappt eine Zeile mit Chevron, aufgeklappt eine Karte mit Name (gedämpft),
/// Gruppe und "Aktivieren" je Kategorie. Ohne deaktivierte Kategorie fehlt er.
let private inactiveSection (groups: ManagedCategoryGroup list) (isOpen: bool) (dispatch: Msg -> unit) =
    let inactive = inactiveCategories groups
    if inactive.IsEmpty then
        Html.none
    else
        Html.section [
            prop.custom ("data-testid", "categories-inactive")
            prop.className "flex flex-col gap-2"
            prop.children [
                Button.disclosureSectionHead
                    "categories-inactive-toggle"
                    "categories-inactive-list"
                    $"Deaktiviert ({inactive.Length})"
                    isOpen
                    (fun () -> dispatch ToggleInactive)
                Html.div [
                    prop.id "categories-inactive-list"
                    prop.className cardClass
                    prop.hidden (not isOpen)
                    prop.children [
                        for group, category in inactive do
                            Html.div [
                                prop.key (string category.Id)
                                prop.custom ("data-testid", "categories-inactive-row")
                                prop.className "flex items-center justify-between gap-3 px-4 py-2.5 min-h-[56px] border-b border-border-subtle last:border-b-0"
                                prop.children [
                                    Html.div [
                                        prop.className "flex flex-col min-w-0"
                                        prop.children [
                                            Truncate.text $"block {ListRoles.primarySize} {FontWeights.medium} {Colors.textMuted}" category.Name
                                            Truncate.text $"block {ListRoles.secondary}" group.Name
                                        ]
                                    ]
                                    Button.view
                                        { Button.defaultProps with
                                            Text = "Aktivieren"
                                            Variant = Button.Secondary
                                            Size = Button.Small
                                            AriaLabel = Some $"{category.Name} aktivieren"
                                            ClassName = Some "shrink-0"
                                            OnClick = fun () -> dispatch (Reactivate category.Id) }
                                ]
                            ]
                    ]
                ]
            ]
        ]

let private groupOptions (groups: ManagedCategoryGroup list) : Input.SelectOption list =
    [ for group in groups do
          ({ Value = string group.Id; Label = group.Name; Disabled = false } : Input.SelectOption)
      ({ Value = NewGroupChoice; Label = "Neue Gruppe …"; Disabled = false } : Input.SelectOption) ]

/// Der Inhalt des Formulars: Name, bei einer Kategorie das Auswahlfeld Gruppe und der Hinweis, darunter ein Fehler.
let private formBody (groups: ManagedCategoryGroup list) (form: Form) (dispatch: Msg -> unit) =
    let name =
        match form.Editor with
        | EditingCategory editor -> editor.Name
        | EditingGroup editor -> editor.Name
    let placeholder =
        match form.Editor with
        | EditingCategory _ -> "z. B. Haustier"
        | EditingGroup _ -> "z. B. Freizeit"
    Html.div [
        prop.custom ("data-testid", "categories-form")
        prop.className "flex flex-col gap-3.5"
        prop.children [
            Input.groupSimple
                "Name"
                (Input.text
                    { Input.textInputDefaults with
                        Value = name
                        OnChange = fun (value: string) -> dispatch (NameChanged value)
                        Placeholder = placeholder
                        AriaLabel = Some "Name"
                        Disabled = form.IsSaving
                        OnKeyDown = Some (fun e -> if e.key = "Enter" then dispatch Save) })
            match form.Editor with
            | EditingCategory editor ->
                Input.groupSimple
                    "Gruppe"
                    (Input.select
                        { Input.selectDefaults with
                            Value = editor.GroupId |> Option.map string |> Option.defaultValue ""
                            Placeholder = if editor.GroupId.IsNone then Some "Gruppe wählen" else None
                            OnChange = fun (value: string) -> dispatch (GroupChosen value)
                            Options = groupOptions groups
                            Disabled = form.IsSaving
                            ClassName = Some "w-full" })
                Html.p [ prop.className ListRoles.muted; prop.text "Name und Gruppe werden nach YNAB übertragen." ]
                if editor.CategoryId.IsSome then
                    Input.switchRow
                        "categories-active"
                        "Aktiv"
                        ActiveSwitchText
                        editor.Active
                        form.IsSaving
                        (fun active -> dispatch (ActiveToggled active))
            | EditingGroup _ -> ()
            match form.Error with
            | Some message ->
                Html.p [
                    prop.custom ("data-testid", "categories-form-error")
                    prop.role "alert"
                    prop.className $"text-sm {Colors.error}"
                    prop.text message
                ]
            | None -> ()
        ]
    ]

let private formButtons (form: Form) (dispatch: Msg -> unit) =
    [ Button.view
          { Button.defaultProps with
              Text = "Abbrechen"
              Variant = Button.Secondary
              FullWidth = true
              IsDisabled = form.IsSaving
              OnClick = fun () -> dispatch CloseForm }
      Button.view
          { Button.defaultProps with
              Text = saveLabel form.Editor
              Variant = Button.Primary
              FullWidth = true
              IsBusy = form.IsSaving
              IsDisabled = not form.IsSaving && not (canSave form)
              OnClick = fun () -> dispatch Save } ]

let private sidePanel (groups: ManagedCategoryGroup list) (form: Form) (dispatch: Msg -> unit) =
    Html.aside [
        prop.custom ("data-testid", "categories-panel")
        prop.className "category-admin-panel flex flex-col gap-3.5 rounded-2xl border border-border-default bg-surface-elevated p-4 overflow-y-auto"
        prop.onKeyDown (fun e -> if e.key = "Escape" then dispatch CloseForm)
        prop.children [
            Html.div [
                prop.className "flex items-center justify-between gap-3"
                prop.children [
                    Html.h2 [ prop.className $"text-lg {FontWeights.semibold} {Colors.textPrimary}"; prop.text (formTitle form.Editor) ]
                    Button.view
                        { Button.defaultProps with
                            Variant = Button.Ghost
                            Size = Button.Small
                            Icon = Some (Icons.x Icons.SM Icons.Default)
                            AriaLabel = Some "Schließen"
                            Title = Some "Schließen"
                            IsDisabled = form.IsSaving
                            OnClick = fun () -> dispatch CloseForm }
                ]
            ]
            formBody groups form dispatch
            Html.div [ prop.className "grid grid-cols-2 gap-3"; prop.children (formButtons form dispatch) ]
        ]
    ]

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt.
let private sheet (groups: ManagedCategoryGroup list) (form: Form option) (dispatch: Msg -> unit) =
    match form with
    | Some form ->
        BottomSheet.view
            { IsOpen = true
              OnClose = fun () -> dispatch CloseForm
              Title = formTitle form.Editor
              Subtitle = None
              Footer = Some (formButtons form dispatch) }
            [ Html.div [ prop.className "py-2"; prop.children [ formBody groups form dispatch ] ] ]
    | None ->
        BottomSheet.view { IsOpen = false; OnClose = ignore; Title = ""; Subtitle = None; Footer = None } []

/// Beim Laden eine Gruppe als Platzhalter: Kopfzeile und eine Karte mit Zeilen in der Höhe der echten (§6, kein
/// Sprung, wenn die Liste kommt).
let private skeleton () =
    Html.div [
        prop.custom ("data-testid", "categories-skeleton")
        prop.role "status"
        prop.ariaLabel "Kategorien werden geladen…"
        prop.className "flex flex-col gap-2"
        prop.children [
            Html.div [ prop.className "w-32 py-1"; prop.children [ Loading.line "100" ] ]
            Html.div [
                prop.className cardClass
                prop.children [
                    for width in [ "50"; "70"; "40"; "60" ] ->
                        Html.div [
                            prop.className "flex items-center px-4 min-h-[48px] border-b border-border-subtle last:border-b-0"
                            prop.children [ Html.div [ prop.className "flex-1"; prop.children [ Loading.line width ] ] ]
                        ]
                ]
            ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    let isPanel = Viewport.isInspectorPanelViewport ()
    let groups =
        match model.Groups with
        | Success groups -> groups
        | _ -> []
    let content =
        Html.div [
            prop.custom ("data-testid", "categories-list")
            prop.className "flex flex-col gap-4 min-w-0"
            prop.children [
                Html.p [ prop.className $"text-sm {Colors.textSecondary}"; prop.text IntroText ]
                match model.Groups with
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryLoad))
                | Success groups ->
                    for group in groups do
                        groupSection group dispatch
                    addButtons dispatch
                    inactiveSection groups model.InactiveOpen dispatch
                | NotAsked
                | Loading -> skeleton ()
            ]
        ]
    // Die Klasse `budget-page` setzt die Maße unter der Navbar, an denen das Panel haftet.
    Primitives.Container.full [
        Html.div [
            prop.custom ("data-testid", "categories-page")
            prop.className "budget-page flex flex-col gap-3 py-2"
            prop.children [
                header dispatch
                // ADR 0043: zur Renderzeit genau eine Hülle. Ohne offenes Formular ist das Panel zu.
                if isPanel then
                    match model.Form with
                    | Some form ->
                        Html.div [
                            prop.className "grid grid-cols-[minmax(0,1fr)_380px] gap-3 items-start"
                            prop.children [ content; sidePanel groups form dispatch ]
                        ]
                    | None -> Html.div [ prop.className "max-w-[860px]"; prop.children [ content ] ]
                else
                    content
                    sheet groups model.Form dispatch
            ]
        ]
    ]
