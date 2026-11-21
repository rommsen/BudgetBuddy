/// Die Seite Kategorien `#/budget/categories` (budgeting-pscgb, Form nach Prototyp Version 17): Kopf mit Zurück und
/// Titel, ein Satz Erklärung, je aktiver Gruppe ein Abschnitt mit "Bearbeiten" (budgeting-nyx75) und einer Karte mit
/// den Kategorien, darunter "+ Kategorie" und "+ Gruppe", darunter der eingeklappte Abschnitt "Deaktiviert (n)" mit
/// deaktivierten Gruppen und Kategorien (budgeting-sn2sn, budgeting-nyx75). Jede
/// Kategorie hat Pfeile hoch und runter für BBs Reihenfolge. Das Formular steht am Handy im Sheet, ab 1024 px als
/// Panel rechts (§7.6).
module Components.Budget.CategoriesView

open Fable.Core
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

[<Emit("$0 && $0.scrollIntoView({ block: 'nearest' })")>]
let private scrollIntoViewNearest (element: obj) : unit = jsNative

/// Rollt seinen Inhalt beim Erscheinen ins Bild (`block: nearest`), je `key` einmal: die neue oder geänderte Zeile nach
/// dem Speichern (budgeting-sx9v5).
[<ReactComponent>]
let private Revealed (key: string) (child: ReactElement) =
    let element = React.useElementRef ()
    React.useEffect ((fun () -> element.current |> Option.iter (box >> scrollIntoViewNearest)), [| box key |])
    Html.div [ prop.ref element; prop.children [ child ] ]

/// Eine markierte Zeile in einer Karte: die Hülle trägt den Rand der Zeile, das Leuchten und darunter die Rückmeldung.
/// Ohne Markierung bleibt die Zeile, wie sie ist.
let private markedRow (key: string) (lit: bool) (row: ReactElement) (below: ReactElement) =
    Html.div [
        prop.key key
        prop.custom ("data-testid", "categories-marked")
        prop.custom ("data-flash", lit)
        prop.className ("border-b border-border-subtle last:border-b-0 " + (if lit then Feedback.flash else ""))
        prop.children [ row; below ]
    ]

let private header (dispatch: Msg -> unit) =
    Html.div [
        prop.custom ("data-testid", "categories-head")
        prop.className "flex items-baseline gap-3"
        prop.children [
            PageHeader.backButtonFor (Some "Zurück zum Budget") (fun () -> dispatch Leave)
            Html.h1 [ prop.className $"text-xl {FontWeights.semibold} {Colors.textPrimary}"; prop.text "Kategorien" ]
        ]
    ]

let private groupSection (model: Model) (group: ManagedCategoryGroup) (dispatch: Msg -> unit) =
    let headLit = model.Mark = Some (GroupLit group.Id) && model.MarkLit
    let head =
        Html.div [
            prop.custom ("data-testid", "categories-group-head")
            prop.custom ("data-flash", headLit)
            prop.className ("-mx-1 flex items-center justify-between gap-3 min-w-0 rounded-md px-1 " + (if headLit then Feedback.flash else ""))
            prop.children [
                Html.h2 (Truncate.textProps ListRoles.groupHeader group.Name)
                Html.div [
                    prop.className "shrink-0"
                    prop.children [
                        Button.quietTextFor
                            "categories-edit-group"
                            $"Gruppe {group.Name} bearbeiten"
                            "Bearbeiten"
                            (fun () -> dispatch (EditGroup group.Id))
                    ]
                ]
            ]
        ]
    Html.section [
        prop.key (string group.Id)
        prop.custom ("data-testid", "categories-group")
        prop.className "flex flex-col gap-2"
        prop.children [
            // Umbenannt oder wieder aktiv: der Kopf leuchtet kurz; nach dem Aktivieren rollt er ins Bild (budgeting-nyx75).
            match model.Mark with
            | Some (GroupLit id) when id = group.Id -> Revealed $"{group.Id}-{model.MarkCount}" head
            | _ -> head
            // Scheiterte die Reihenfolge, steht der Grund rot unter dem Kopf, bis zur nächsten Aktion.
            match model.Mark with
            | Some (OrderFailed (groupId, message)) when groupId = group.Id ->
                Html.div [ prop.custom ("data-testid", "categories-order-error"); prop.children [ ErrorDisplay.inline' message ] ]
            | _ -> ()
            let active = activeCategories group
            if not active.IsEmpty then
                Html.div [
                    prop.className cardClass
                    prop.children [
                        for category in active do
                          let row =
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
                          // Angelegt, gespeichert oder wieder aktiv: die Zeile leuchtet kurz und wird ins Bild gerollt.
                          match model.Mark with
                          | Some (CategoryLit id) when id = category.Id ->
                              markedRow (string category.Id) model.MarkLit (Revealed $"{category.Id}-{model.MarkCount}" row) Html.none
                          | _ -> row
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

/// Der Abschnitt "Deaktiviert (n)": eingeklappt eine Zeile mit Chevron, aufgeklappt eine Karte mit je Eintrag Name
/// (gedämpft), darunter klein die Gruppe oder bei einer deaktivierten Gruppe "Gruppe · n Kategorien" (budgeting-nyx75),
/// und "Aktivieren". Gruppen stehen vor den einzelnen Kategorien. Ohne Eintrag fehlt er.
let private inactiveSection (model: Model) (groups: ManagedCategoryGroup list) (isOpen: bool) (dispatch: Msg -> unit) =
    let inactive = inactiveEntries groups
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
                        for entry in inactive do
                          let id, name, detail, label, testId, reactivate =
                              match entry with
                              | InactiveGroup group ->
                                  group.Id, group.Name, inactiveGroupDetail group, $"Gruppe {group.Name} aktivieren",
                                  "categories-inactive-group", ReactivateGroup group.Id
                              | InactiveCategory (group, category) ->
                                  category.Id, category.Name, group.Name, $"{category.Name} aktivieren",
                                  "categories-inactive-row", Reactivate category.Id
                          let row =
                            Html.div [
                                prop.key (string id)
                                prop.custom ("data-testid", testId)
                                prop.className "flex items-center justify-between gap-3 px-4 py-2.5 min-h-[56px] border-b border-border-subtle last:border-b-0"
                                prop.children [
                                    Html.div [
                                        prop.className "flex flex-col min-w-0"
                                        prop.children [
                                            Truncate.text $"block {ListRoles.primarySize} {FontWeights.medium} {Colors.textMuted}" name
                                            Truncate.text $"block {ListRoles.secondary}" detail
                                        ]
                                    ]
                                    Button.view
                                        { Button.defaultProps with
                                            Text = "Aktivieren"
                                            Variant = Button.Secondary
                                            Size = Button.Small
                                            AriaLabel = Some label
                                            ClassName = Some "shrink-0"
                                            OnClick = fun () -> dispatch reactivate }
                                ]
                            ]
                          // Deaktiviert: die Zeile leuchtet, darunter der Satz zu YNAB; Aktivieren abgelehnt: rot darunter.
                          let below (content: ReactElement) =
                              Html.div [ prop.className "px-4 pb-2.5 -mt-1"; prop.children [ content ] ]
                          match model.Mark with
                          | Some (Deactivated markedId) when markedId = id ->
                              markedRow
                                  (string id)
                                  model.MarkLit
                                  (Revealed $"{id}-{model.MarkCount}" row)
                                  (below (
                                      Html.p [
                                          prop.custom ("data-testid", "categories-deactivated-note")
                                          prop.className ListRoles.secondary
                                          prop.text DeactivatedNotice
                                      ]
                                  ))
                          | Some (ReactivateFailed (failed, message)) when failed = id ->
                              markedRow
                                  (string id)
                                  false
                                  (Html.div [ prop.children [ row ] ])
                                  (below (Html.div [ prop.custom ("data-testid", "categories-reactivate-error"); prop.children [ ErrorDisplay.inline' message ] ]))
                          | _ -> row
                    ]
                ]
            ]
        ]

/// Das Feld Gruppe im Formular, als Selektor für die Rückkehr des Fokus aus der Auswahl.
let private groupFieldSelector = "[data-testid='categories-group-field']"

/// Die Auswahl der Gruppe (G13, Prototyp `cegroup`): je Gruppe eine Zeile mit der Zahl ihrer aktiven Kategorien, die
/// gewählte mit Haken, darunter „Neue Gruppe“. Kurze Auswahl ohne Suche (§7.9): Fokus auf der gewählten Gruppe, ↑↓ und
/// Enter; Esc schließt nur die Auswahl (`categoryKeyAction`), der Fokus geht zurück auf das Feld.
let private groupChoice (groups: ManagedCategoryGroup list) (editor: CategoryEditor) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "flex flex-col gap-3"
        prop.children [
            Html.div [
                prop.className cardClass
                prop.children [
                    PickerParts.ShortChoiceFor "categories-group-choice" (Some groupFieldSelector) [
                        // Eine in BB deaktivierte Gruppe steht nicht zur Wahl (budgeting-nyx75).
                        for group in activeGroups groups ->
                            React.keyedFragment (
                                group.Id,
                                [ PickerParts.shortChoiceRow
                                      group.Name
                                      (Some (groupChoiceDetail group))
                                      (editor.GroupId = Some group.Id)
                                      (fun () -> dispatch (GroupChosen (string group.Id))) ]
                            )
                    ]
                ]
            ]
            Button.view
                { Button.defaultProps with
                    Text = "Neue Gruppe"
                    Variant = Button.Outline
                    FullWidth = true
                    Icon = Some (Icons.plus Icons.SM Icons.Default)
                    OnClick = fun () -> dispatch (GroupChosen NewGroupChoice) }
        ]
    ]

/// Die Auswahl der Gruppe am PC: ein Dialog in der Mitte wie im Prototyp (§7.6, eine Auswahl ist ein Dialog).
let private groupChoiceDialog (groups: ManagedCategoryGroup list) (form: Form) (dispatch: Msg -> unit) =
    match form.Editor with
    | EditingCategory editor when form.GroupChoiceOpen ->
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                OnClose = fun () -> dispatch CloseGroupChoice
                Size = Modal.Large
                Title = Some "Gruppe" }
            [ Modal.body [ groupChoice groups editor dispatch ] ]
    | _ -> Html.none

/// Der Name der Gruppe mit der Id, `None` ohne Gruppe oder wenn sie nicht in der Liste steht.
let private groupNameOf (groups: ManagedCategoryGroup list) (groupId: System.Guid option) : string option =
    groupId |> Option.bind (fun id -> groups |> List.tryFind (fun g -> g.Id = id)) |> Option.map (fun g -> g.Name)

/// Der Inhalt des Formulars: Name, bei einer Kategorie das Feld Gruppe (öffnet die Auswahl, G13), in beiden
/// Formularen der Hinweis zu YNAB, darunter ein Fehler.
let private formBody (model: Model) (groups: ManagedCategoryGroup list) (form: Form) (dispatch: Msg -> unit) =
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
            // Ein Fehler des Servers steht am Feld (Regel R3, `.claude/rules/task-ui-vorgaben.md`): Name und Gruppe am
            // Feld Name, das Deaktivieren am Schalter Aktiv.
            let nameError =
                match form.FailedCall with
                | Some SavingActive
                | Some SavingGroupActive -> None
                | _ -> form.Error
            Input.groupWithError
                "Name"
                nameError
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
                // Feld mit Pfeil wie im Prototyp (`ceGroupPick`), kein natives Select (G13).
                // Während des Speicherns gesperrt wie die übrigen Felder.
                // Nach „Neue Gruppe“ steht die neue Gruppe im Feld, das Feld leuchtet kurz.
                let fieldLit = model.Mark = Some GroupFieldLit && model.MarkLit
                Html.div [
                    prop.custom ("data-testid", "categories-group-field-wrap")
                    prop.custom ("data-flash", fieldLit)
                    prop.className (if fieldLit then Feedback.flash else "")
                    prop.children [
                        Input.pickerFieldWith
                            form.IsSaving
                            "categories-group-field"
                            "Gruppe"
                            (groupNameOf groups editor.GroupId)
                            "Gruppe wählen"
                            None
                            form.GroupChoiceOpen
                            (fun () -> dispatch OpenGroupChoice)
                    ]
                ]
                Html.p [ prop.className ListRoles.muted; prop.text "Name und Gruppe werden nach YNAB übertragen." ]
                if editor.CategoryId.IsSome then
                    Input.switchRow
                        "categories-active"
                        "Aktiv"
                        ActiveSwitchText
                        editor.Active
                        form.IsSaving
                        (fun active -> dispatch (ActiveToggled active))
                    match form.FailedCall, form.Error with
                    | Some SavingActive, Some message ->
                        Html.div [ prop.custom ("data-testid", "categories-form-error"); prop.children [ ErrorDisplay.inline' message ] ]
                    | _ -> ()
            // Auch Gruppennamen gehen nach YNAB (Entscheidung Roman im Review-Lauf nach budgeting-t7dka, Abweichung
            // von Prototyp Version 17).
            | EditingGroup editor ->
                Html.p [ prop.className ListRoles.muted; prop.text "Der Name wird nach YNAB übertragen." ]
                // Gruppe deaktivieren (budgeting-nyx75): Schalter wie bei der Kategorie, die Ablehnung steht am Schalter.
                if editor.GroupId.IsSome then
                    Input.switchRow
                        "categories-group-active"
                        "Aktiv"
                        GroupActiveSwitchText
                        editor.Active
                        form.IsSaving
                        (fun active -> dispatch (ActiveToggled active))
                    match form.FailedCall, form.Error with
                    | Some SavingGroupActive, Some message ->
                        Html.div [ prop.custom ("data-testid", "categories-form-error"); prop.children [ ErrorDisplay.inline' message ] ]
                    | _ -> ()
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

let private sidePanel (model: Model) (groups: ManagedCategoryGroup list) (form: Form) (dispatch: Msg -> unit) =
    // Die Hülle wie in Kassenbuch und Budget (design-system-gcpws, §7.6): unter dem Seitenkopf, ohne Karte. Esc schließt über
    // die Tasten der Seite (`CategoryKeys`), auch ohne Fokus im Panel.
    Detail.panelWith "categories-panel" (formTitle form.Editor) [
      Html.div [
        prop.className "flex min-h-0 flex-1 flex-col gap-3.5 overflow-y-auto p-4"
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
            formBody model groups form dispatch
            Html.div [ prop.className "grid grid-cols-2 gap-3"; prop.children (formButtons form dispatch) ]
            groupChoiceDialog groups form dispatch
        ]
      ]
    ]

/// Das Sheet unter 1024 px. Immer gerendert, damit es ein- und ausfährt.
let private sheet (model: Model) (groups: ManagedCategoryGroup list) (form: Form option) (dispatch: Msg -> unit) =
    match form with
    // Am Handy ersetzt die Auswahl der Gruppe den Inhalt des Sheets (§7.15, Sheets werden nicht gestapelt);
    // Schließen führt zurück ins Formular.
    | Some ({ Editor = EditingCategory editor } as form) when form.GroupChoiceOpen ->
        BottomSheet.view
            { IsOpen = true
              OnClose = fun () -> dispatch CloseGroupChoice
              Title = "Gruppe"
              Subtitle = None
              Footer = None }
            [ Html.div [ prop.className "py-2"; prop.children [ groupChoice groups editor dispatch ] ] ]
    | Some form ->
        BottomSheet.view
            { IsOpen = true
              OnClose = fun () -> dispatch CloseForm
              Title = formTitle form.Editor
              Subtitle = None
              Footer = Some (formButtons form dispatch) }
            [ Html.div [ prop.className "py-2"; prop.children [ formBody model groups form dispatch ] ] ]
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
                // Ein gescheitertes stilles Neuladen ist ein Zustand der Seite (ADR 0098): Hinweis, bis es klappt.
                match model.Stale with
                | Some stale ->
                    Feedback.stateHint
                        "categories-stale"
                        ListStaleText
                        [ stale.Reason ]
                        (Some { Label = "Neu laden"; OnClick = fun () -> dispatch RereadList })
                        stale.Rereading
                | None -> ()
                match model.Groups with
                | Failure message -> ErrorDisplay.card message (Some (fun () -> dispatch RetryLoad))
                | Success groups ->
                    for group in activeGroups groups do
                        groupSection model group dispatch
                    addButtons dispatch
                    inactiveSection model groups model.InactiveOpen dispatch
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
                // Die Tasten am PC (F5, design-system-gcpws): ↑↓ öffnen die nächste Kategorie im Panel, Esc schließt.
                if isPanel then
                    PageKeys.OnDocument (fun e ->
                        let focus = PageKeys.focusOf None e.target
                        match categoryKeyAction model focus (e.altKey || e.ctrlKey || e.metaKey) e.shiftKey e.defaultPrevented e.key with
                        | Some msg ->
                            e.preventDefault ()
                            dispatch msg
                        | None -> ())
                if isPanel then
                    match model.Form with
                    | Some form ->
                        Html.div [
                            prop.className "relative"
                            prop.children [
                                sidePanel model groups form dispatch
                                Html.div [
                                    prop.className $"{PanelWidths.columnInsetWide} min-h-[calc(100vh-7rem)]"
                                    prop.children [ content ]
                                ]
                            ]
                        ]
                    | None -> Html.div [ prop.className "max-w-[860px]"; prop.children [ content ] ]
                else
                    content
                    sheet model groups model.Form dispatch
            ]
        ]
    ]
