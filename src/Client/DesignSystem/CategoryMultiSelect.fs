/// Die Kategorienauswahl mit Mehrfachauswahl (ledger-j8xq4, herausgezogen in
/// design-system-bz0gn). Versteckte Kategorien eingeklappt am Ende ("Versteckt (n)"),
/// Available je Kategorie rechtsbündig, Click-Commit: erst "Übernehmen" schreibt den
/// Entwurf in den Filter. Kennt weder Kassenbuch-Model noch -Msg, der Aufrufer
/// füllt den Props-Record.
module Client.DesignSystem.CategoryMultiSelect

open System
open Feliz
open Shared.Domain

type CategoryMultiSelectProps = {
    Categories: YnabCategory list
    Selected: Guid Set
    Search: string
    HiddenExpanded: bool
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

let private categoryRow (props: CategoryMultiSelectProps) (c: YnabCategory) =
    let guid = categoryGuidOf c.Id
    Html.div [
        prop.key (string guid)
        prop.className "flex items-center justify-between gap-3 py-2"
        prop.children [
            Input.checkbox (props.Selected.Contains guid) (fun _ -> props.OnToggle guid) c.Name
            match c.Available with
            | Some m -> Money.available m.Amount m.Currency
            | None -> ()
        ]
    ]

let view (props: CategoryMultiSelectProps) : ReactElement =
    let visibleGroups =
        props.Categories
        |> List.filter (fun c -> not c.Hidden && categoryMatchesSearch props.Search c)
        |> List.groupBy (fun c -> c.GroupName)

    let hidden = props.Categories |> List.filter (fun c -> c.Hidden)

    Html.div [
        prop.className "flex flex-col gap-3"
        prop.children [
            Input.textSimple props.Search props.OnSearchChange "Kategorie suchen…"

            Html.div [
                prop.className "flex flex-col gap-3 max-h-72 overflow-y-auto"
                prop.children [
                    for (groupName, items) in visibleGroups do
                        Html.div [
                            prop.key groupName
                            prop.children [
                                Html.div [
                                    prop.className $"{Tokens.FontSizes.xs} {Tokens.Colors.textMuted} {Tokens.Fonts.mono} uppercase tracking-wider pb-1"
                                    prop.text groupName
                                ]
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children (items |> List.map (categoryRow props))
                                ]
                            ]
                        ]

                    if not (List.isEmpty hidden) then
                        Html.div [
                            prop.children [
                                Html.button [
                                    prop.type' "button"
                                    prop.className $"w-full flex items-center justify-between py-2 {Tokens.Colors.textSecondary} {Tokens.FontSizes.xs} font-semibold"
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
                                        prop.children (
                                            hidden
                                            |> List.filter (categoryMatchesSearch props.Search)
                                            |> List.map (categoryRow props))
                                    ]
                            ]
                        ]
                ]
            ]

            Primitives.HStack.spaceBetween [
                Button.ghost "Zurücksetzen" (fun () -> props.OnReset ())
                Button.primary $"Übernehmen ({Set.count props.Selected})" (fun () -> props.OnApply ())
            ]
        ]
    ]
