/// Die Kategorienauswahl mit Mehrfachauswahl (ledger-j8xq4, herausgezogen in
/// design-system-bz0gn). Versteckte Kategorien eingeklappt am Ende ("Versteckt (n)"),
/// Available je Kategorie rechtsbündig, Click-Commit: erst "Übernehmen" schreibt den
/// Entwurf in den Filter. Kennt weder Kassenbuch-Model noch -Msg, der Aufrufer
/// füllt den Props-Record.
module Client.DesignSystem.CategoryMultiSelect

open System
open Feliz
open Fable.Core.JsInterop
open Shared.Domain

type CategoryMultiSelectProps = {
    Categories: YnabCategory list
    Selected: Guid Set
    Search: string
    HiddenExpanded: bool
    /// Zuletzt verwendete Kategorien (höchstens 5), über der Gruppenliste; leer blendet den Abschnitt aus
    Recent: YnabCategory list
    /// Übernehmen gesperrt, solange die Auswahl dem aktiven Filter gleicht
    ApplyDisabled: bool
    OnToggle: Guid -> unit
    OnSearchChange: string -> unit
    OnToggleHiddenExpanded: unit -> unit
    OnReset: unit -> unit
    OnApply: unit -> unit
}

let private categoryGuidOf (id: YnabCategoryId) : Guid =
    let (YnabCategoryId guid) = id
    guid

let private categoryMatchesSearch (search: string) (c: YnabCategory) : bool =
    String.IsNullOrWhiteSpace search
    || c.Name.ToLowerInvariant().Contains(search.Trim().ToLowerInvariant())
    || c.GroupName.ToLowerInvariant().Contains(search.Trim().ToLowerInvariant())

/// Die Liste der Optionen und die Id einer Zeile, auf die das Suchfeld per `aria-activedescendant` verweist.
let listboxId = "category-multi-select-listbox"
let optionId (index: int) : string = $"category-multi-select-option-{index}"

/// Pfeil und Enter im Suchfeld (§7.9, design-system-gcpws): ↓ ohne Markierung die erste, ↑ die letzte Zeile, am Rand
/// bleibt die Markierung stehen; Enter schaltet die markierte Zeile genau einmal. `count`: die sichtbaren Zeilen.
type MultiKey =
    | MarkRow of int
    | ToggleRow of int
    | KeepKey

let keyAction (key: string) (count: int) (active: int option) : MultiKey =
    match key, active with
    | "ArrowDown", None when count > 0 -> MarkRow 0
    | "ArrowDown", Some i when count > 0 -> MarkRow (min (count - 1) (i + 1))
    | "ArrowUp", None when count > 0 -> MarkRow (count - 1)
    | "ArrowUp", Some i when count > 0 -> MarkRow (max 0 (i - 1))
    | "Enter", Some i when i < count -> ToggleRow i
    | _ -> KeepKey

[<ReactComponent>]
let private MultiSelectContent (props: CategoryMultiSelectProps) =
    let active, setActive = React.useState<int option> None
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let visibleGroups =
        props.Categories
        |> List.filter (fun c -> not c.Hidden && categoryMatchesSearch props.Search c)
        |> List.groupBy (fun c -> c.GroupName)

    let hidden = props.Categories |> List.filter (fun c -> c.Hidden)
    let showRecent = not (List.isEmpty props.Recent) && String.IsNullOrWhiteSpace props.Search
    let shownHidden = if props.HiddenExpanded then hidden |> List.filter (categoryMatchesSearch props.Search) else []
    // Die Zeilen in der Reihenfolge der Liste: Zuletzt verwendet, Gruppen, Versteckt. Einmal je Render.
    let rows =
        [ if showRecent then yield! props.Recent
          for (_, items) in visibleGroups do yield! items
          yield! shownHidden ]
        |> List.map (fun c -> categoryGuidOf c.Id)
    let count = List.length rows

    // Am PC steht der Fokus beim Öffnen in der Suche, am Handy nicht (die Tastatur käme sonst).
    React.useEffectOnce (fun () ->
        if Viewport.isFinePointer () then
            match containerRef.current with
            | Some el ->
                let field = el.querySelector "input[type='text'], input:not([type])"
                if not (isNull field) then (field :?> Browser.Types.HTMLElement).focus ()
            | None -> ())

    // Die markierte Zeile rückt ins Bild.
    React.useEffect (
        (fun () ->
            match active, containerRef.current with
            | Some i, Some el ->
                let target = el.querySelector $"#{optionId i}"
                if not (isNull target) then target?scrollIntoView (createObj [ "block" ==> "nearest" ])
            | _ -> ()),
        [| box active |]
    )

    let counter = ref 0
    let row (keyPrefix: string) (c: YnabCategory) =
        let index = counter.Value
        counter.Value <- index + 1
        let guid = categoryGuidOf c.Id
        let marked = active = Some index
        let isSelected = props.Selected.Contains guid
        PickerParts.row
            // Die Zeile ist jetzt die Trefferfläche (Option): am Handy 48 px (Styleguide, Touch-Targets).
            (if marked then $"py-2 {Tokens.TouchTargets.minHeight} rounded-lg bg-surface-hover" else $"py-2 {Tokens.TouchTargets.minHeight}")
            // Die Zeile ist die Option der Listbox und trägt Name und Zustand; das Kästchen darin ist nur Bild.
            [ prop.key (keyPrefix + string guid)
              prop.id (optionId index)
              prop.role "option"
              prop.ariaSelected isSelected
              prop.ariaLabel c.Name
              if marked then prop.custom ("data-marked", "true") ]
            (Html.div [
                prop.ariaHidden true
                prop.className "contents"
                prop.children [ Input.checkboxListRow isSelected (fun _ -> props.OnToggle guid) c.Name ]
            ])
            (c.Available |> Option.map (fun m -> m.Amount, m.Currency))

    Html.div [
        prop.ref containerRef
        prop.className "flex flex-col gap-3"
        prop.onKeyDown (fun e ->
            match keyAction e.key count (active |> Option.filter (fun i -> i < count)) with
            | MarkRow i ->
                e.preventDefault ()
                setActive (Some i)
            | ToggleRow i ->
                e.preventDefault ()
                props.OnToggle (List.item i rows)
            | KeepKey -> ())
        prop.children [
            PickerParts.comboSearchField
                props.Search
                (fun v ->
                    props.OnSearchChange v
                    setActive None)
                "Kategorie suchen…"
                listboxId
                (active |> Option.filter (fun i -> i < count) |> Option.map optionId)

            Html.div [
                prop.id listboxId
                prop.role "listbox"
                prop.ariaMultiSelectable true
                prop.ariaLabel "Kategorien"
                prop.className "flex flex-col gap-3 max-h-72 overflow-y-auto"
                prop.children [
                    if showRecent then
                        Html.div [
                            prop.key "zuletzt-verwendet"
                            prop.custom ("data-testid", "category-recent")
                            prop.children [
                                PickerParts.groupHeader "Zuletzt verwendet"
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children (props.Recent |> List.map (row "recent-"))
                                ]
                            ]
                        ]

                    for (groupName, items) in visibleGroups do
                        Html.div [
                            prop.key groupName
                            prop.children [
                                PickerParts.groupHeader groupName
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children (items |> List.map (row ""))
                                ]
                            ]
                        ]

                    if not (List.isEmpty hidden) then
                        Html.div [
                            prop.children [
                                Html.button [
                                    prop.type' "button"
                                    prop.className $"w-full flex items-center justify-between py-2 {Tokens.TouchTargets.minHeight} {Tokens.Colors.textSecondary} {Tokens.FontSizes.xs} font-semibold"
                                    prop.onClick (fun _ -> props.OnToggleHiddenExpanded ())
                                    prop.children [
                                        Html.span [ prop.text $"Versteckt ({List.length hidden})" ]
                                        if props.HiddenExpanded then Icons.chevronUp Icons.XS Icons.Default
                                        else Icons.chevronDown Icons.XS Icons.Default
                                    ]
                                ]
                                if props.HiddenExpanded then
                                    Html.div [
                                        prop.className "flex flex-col"
                                        prop.children (shownHidden |> List.map (row ""))
                                    ]
                            ]
                        ]
                ]
            ]

            PickerParts.footerWhen props.ApplyDisabled (Set.count props.Selected) props.OnReset props.OnApply
        ]
    ]

/// Die Auswahl. Ein eigener Baustein mit Zustand für die Markierung (§7.9).
let view (props: CategoryMultiSelectProps) : ReactElement = MultiSelectContent props
