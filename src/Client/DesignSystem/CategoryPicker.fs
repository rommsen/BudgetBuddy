/// Der Kategorie-Picker mit Einfachauswahl (design-system-gffps, ADR 0053, ADR 0049):
/// ein Inhalt, zwei Hüllen. Am PC hängt er als Popover am Auslöser (Mechanik von
/// `FilterPopover`), am Handy liegt er in der BottomSheet-Hülle. Ein Klick auf eine Zeile
/// übernimmt sofort (Click-Commit nach ADR 0005, kein Übernehmen-Knopf). `OnSelect` bekommt
/// `None` für die Zeile "Ohne Kategorie". Der Baustein kennt weder Kassenbuch-Model noch
/// -Msg: der Aufrufer liefert Kategorien, die aktuelle Kategorie, die Vorschläge zum Payee
/// und die Beschriftung der Stand-Zeile.
///
/// Darunter liegt `CategoryChangeCard`, die rein darstellende Karte "Budget nach dem
/// Wechsel" (Tafel 4.3 des Handoffs Ablauf 4).
module Client.DesignSystem.CategoryPicker

open System
open Fable.Core
open Fable.Core.JsInterop
open Feliz
open Shared.Domain
open Client.DesignSystem.Tokens

// ============================================
// Typen
// ============================================

type CategoryPickerProps = {
    /// Alle wählbaren Kategorien (interne YNAB-Kategorien filtert der Aufrufer heraus)
    Categories: YnabCategory list
    /// Aktuelle Kategorie der Buchung, `None` bei "Ohne Kategorie"
    Current: YnabCategoryId option
    /// Vorgeschlagene Kategorien zum Payee, häufigste zuerst (der Baustein kürzt auf drei)
    Suggested: YnabCategoryId list
    /// Beschriftung der Fußzeile, zum Beispiel "Stand 14:02". `None` blendet sie aus.
    StandLabel: string option
    OnSelect: YnabCategoryId option -> unit
    OnClose: unit -> unit
}

/// Eine wählbare Zeile in der Reihenfolge, in der sie gerendert wird
type PickerRow =
    | WithoutCategory
    | Category of YnabCategory

/// Aufbau der Liste für einen Suchtext
type Layout = {
    ShowWithout: bool
    Suggested: YnabCategory list
    Groups: (string * YnabCategory list) list
    /// Anzahl aller versteckten Kategorien (die Zahl im Kopf "Versteckt (n)")
    HiddenCount: int
    /// Versteckte Kategorien, die zur Suche passen; sie erscheinen nur aufgeklappt
    Hidden: YnabCategory list
    ShowAllHeader: bool
}

/// Was eine Taste im Picker auslöst
type KeyAction =
    | MoveTo of int
    | Choose of YnabCategoryId option
    | CloseOnly
    | NoAction

// ============================================
// Reine Funktionen
// ============================================

let withoutCategoryLabel = "Ohne Kategorie"

/// Höchstzahl der Vorschläge
let maxSuggestions = 3

let isSearching (search: string) : bool = not (String.IsNullOrWhiteSpace search)

/// Treffer in Gruppe oder Name, ohne Beachtung der Groß- und Kleinschreibung
let matchesSearch (search: string) (c: YnabCategory) : bool =
    if not (isSearching search) then true
    else
        let needle = search.Trim().ToLowerInvariant()
        c.Name.ToLowerInvariant().Contains needle || c.GroupName.ToLowerInvariant().Contains needle

/// Vorschläge: Reihenfolge der Eingabe, ohne Dubletten, ohne unbekannte Ids, ohne die
/// aktuelle Kategorie, höchstens drei
let suggestedCategories
    (categories: YnabCategory list)
    (current: YnabCategoryId option)
    (suggested: YnabCategoryId list)
    : YnabCategory list =
    suggested
    |> List.distinct
    |> List.choose (fun id -> categories |> List.tryFind (fun c -> c.Id = id))
    |> List.filter (fun c -> Some c.Id <> current)
    |> List.truncate maxSuggestions

/// Beschriftung einer Vorschlagszeile: "Gruppe: Name", denn der Gruppenkopf fehlt dort
let suggestedLabel (c: YnabCategory) : string = $"{c.GroupName}: {c.Name}"

/// Ist das die aktuelle Kategorie der Buchung?
let isCurrentCategory (current: YnabCategoryId option) (c: YnabCategory) : bool =
    current = Some c.Id

/// Steht die Zeile "Ohne Kategorie" in der Liste? Bei einer Suche nur, wenn sie passt.
let showWithoutRow (search: string) : bool =
    not (isSearching search) || withoutCategoryLabel.ToLowerInvariant().Contains(search.Trim().ToLowerInvariant())

let layout
    (search: string)
    (current: YnabCategoryId option)
    (suggested: YnabCategoryId list)
    (categories: YnabCategory list)
    : Layout =
    let searching = isSearching search
    // Während einer Suche steht der Abschnitt Vorgeschlagen nicht da (wie im Sheet bisher)
    let suggestedList = if searching then [] else suggestedCategories categories current suggested
    let hiddenAll = categories |> List.filter (fun c -> c.Hidden)
    {
        ShowWithout = showWithoutRow search
        Suggested = suggestedList
        Groups =
            categories
            |> List.filter (fun c -> not c.Hidden && matchesSearch search c)
            |> List.groupBy (fun c -> c.GroupName)
        HiddenCount = List.length hiddenAll
        Hidden = hiddenAll |> List.filter (matchesSearch search)
        ShowAllHeader = PickerParts.showAllCategoriesHeader (not suggestedList.IsEmpty) false searching
    }

/// Die Zeilen in Anzeigereihenfolge, auf der die Pfeiltasten laufen
let navigableRows (hiddenExpanded: bool) (l: Layout) : PickerRow list =
    [ if l.ShowWithout then yield WithoutCategory
      for c in l.Suggested -> Category c
      for (_, items) in l.Groups do
          for c in items -> Category c
      if hiddenExpanded then
          for c in l.Hidden -> Category c ]

/// Was Wählen einer Zeile an den Aufrufer meldet
let choiceOf (row: PickerRow) : YnabCategoryId option =
    match row with
    | WithoutCategory -> None
    | Category c -> Some c.Id

/// Pfeil, Enter und Esc. Enter ohne markierte Zeile wählt bei aktiver Suche den ersten
/// Treffer. Esc schließt nur den Picker (ADR 0053, Punkt 5).
let keyAction (key: string) (rows: PickerRow list) (active: int option) (searching: bool) : KeyAction =
    let count = List.length rows
    match key with
    | "ArrowDown" when count > 0 ->
        MoveTo (match active with None -> 0 | Some i -> min (count - 1) (i + 1))
    | "ArrowUp" when count > 0 ->
        MoveTo (match active with None -> count - 1 | Some i -> max 0 (i - 1))
    | "Enter" ->
        match active with
        | Some i when i >= 0 && i < count -> Choose (choiceOf (List.item i rows))
        | None when searching && count > 0 -> Choose (choiceOf (List.head rows))
        | _ -> NoAction
    | "Escape" -> CloseOnly
    | _ -> NoAction

// ============================================
// Inhalt
// ============================================

let private rowClassFor (isActive: bool) : string =
    let tone = if isActive then "bg-surface-hover" else "hover:bg-surface-hover"
    $"px-2 min-h-11 md:min-h-9 rounded-lg cursor-pointer {tone}"

type private ContentProps = {
    Picker: CategoryPickerProps
    /// Fokus ins Suchfeld beim Öffnen (am Handy nur mit Maus, sonst käme die Tastatur)
    FocusSearch: bool
    /// Klassen der Liste: im Popover begrenzte Höhe mit Scroll, im Sheet scrollt der Körper
    ListClass: string
}

[<ReactComponent>]
let private Content (input: ContentProps) =
    let props = input.Picker
    let search, setSearch = React.useState ""
    let hiddenExpanded, setHiddenExpanded = React.useState false
    let active, setActive = React.useState<int option> None
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None

    let lay = layout search props.Current props.Suggested props.Categories
    let rows = navigableRows hiddenExpanded lay
    let searching = isSearching search

    // Fokus ins Suchfeld beim Öffnen
    React.useEffectOnce (fun () ->
        if input.FocusSearch then
            match containerRef.current with
            | Some el ->
                let field = el.querySelector "input"
                if not (isNull field) then (field :?> Browser.Types.HTMLElement).focus ()
            | None -> ())

    // Die markierte Zeile bleibt sichtbar
    React.useEffect (
        (fun () ->
            match active, containerRef.current with
            | Some i, Some el ->
                let target = el.querySelector $"[data-row-index='{i}']"
                if not (isNull target) then target?scrollIntoView (createObj [ "block" ==> "nearest" ])
            | _ -> ()),
        [| box active |]
    )

    let counter = ref 0
    let nextIndex () =
        let i = counter.Value
        counter.Value <- i + 1
        i

    let itemRow (keyPrefix: string) (label: string) (c: YnabCategory) =
        let index = nextIndex ()
        let isCurrent = isCurrentCategory props.Current c
        let (YnabCategoryId guid) = c.Id
        PickerParts.row
            (rowClassFor (active = Some index))
            [
                prop.key $"{keyPrefix}-{guid}"
                prop.role "option"
                prop.custom ("aria-selected", isCurrent)
                prop.custom ("data-row-index", index)
                // Der Fokus bleibt im Suchfeld, damit zwischen Tippen und Klick nichts springt.
                prop.onMouseDown (fun e -> e.preventDefault ())
                prop.onClick (fun e ->
                    e.stopPropagation ()
                    props.OnSelect (Some c.Id))
            ]
            (Html.span [
                prop.className "flex items-center gap-2 min-w-0"
                prop.children [
                    PickerParts.name label
                    if isCurrent then Icons.check Icons.SM Icons.NeonTeal
                ]
            ])
            (c.Available |> Option.map (fun m -> m.Amount, m.Currency))

    let withoutRow () =
        let index = nextIndex ()
        let isCurrent = props.Current.IsNone
        PickerParts.row
            (rowClassFor (active = Some index))
            [
                prop.key "without-category"
                prop.role "option"
                prop.custom ("aria-selected", isCurrent)
                prop.custom ("data-row-index", index)
                prop.onMouseDown (fun e -> e.preventDefault ())
                prop.onClick (fun e ->
                    e.stopPropagation ()
                    props.OnSelect None)
            ]
            (Html.span [
                prop.className "flex items-center gap-2 min-w-0"
                prop.children [
                    PickerParts.name withoutCategoryLabel
                    if isCurrent then Icons.check Icons.SM Icons.NeonTeal
                ]
            ])
            None

    Html.div [
        prop.ref containerRef
        prop.className "flex flex-col gap-3"
        prop.onKeyDown (fun e ->
            match keyAction e.key rows active searching with
            | MoveTo i ->
                e.preventDefault ()
                setActive (Some i)
            | Choose id ->
                e.preventDefault ()
                props.OnSelect id
            | CloseOnly ->
                // Esc bleibt im Picker: das Dokument schließt sonst zugleich das Detail.
                e.stopPropagation ()
                props.OnClose ()
            | NoAction -> ())
        prop.children [
            Html.div [
                prop.className "sticky top-0 z-10 bg-surface-elevated"
                prop.children [
                    PickerParts.searchField
                        search
                        (fun v ->
                            setSearch v
                            setActive None)
                        "Kategorie suchen…"
                ]
            ]

            Html.div [
                prop.role "listbox"
                prop.className ("flex flex-col gap-3 " + input.ListClass)
                prop.children [
                    if lay.ShowWithout || not lay.Suggested.IsEmpty then
                        Html.div [
                            prop.key "top"
                            prop.className "flex flex-col"
                            prop.children [
                                if lay.ShowWithout then withoutRow ()

                                if not lay.Suggested.IsEmpty then
                                    Html.div [
                                        prop.key "suggested"
                                        prop.className "pt-2"
                                        prop.children [
                                            PickerParts.groupHeader "Vorgeschlagen"
                                            Html.div [
                                                prop.className "flex flex-col"
                                                prop.children (
                                                    lay.Suggested |> List.map (fun c -> itemRow "s" (suggestedLabel c) c)
                                                )
                                            ]
                                        ]
                                    ]
                            ]
                        ]

                    if lay.ShowAllHeader then
                        PickerParts.groupHeader "Alle Kategorien"

                    for (groupName, items) in lay.Groups do
                        Html.div [
                            prop.key groupName
                            prop.children [
                                PickerParts.groupHeader groupName
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children (items |> List.map (fun c -> itemRow "g" c.Name c))
                                ]
                            ]
                        ]

                    if lay.HiddenCount > 0 then
                        Html.div [
                            prop.key "hidden"
                            prop.children [
                                Html.button [
                                    prop.type' "button"
                                    prop.className $"w-full flex items-center justify-between py-2 {Colors.textSecondary} {FontSizes.xs} font-semibold"
                                    prop.onMouseDown (fun e -> e.preventDefault ())
                                    prop.onClick (fun _ -> setHiddenExpanded (not hiddenExpanded))
                                    prop.children [
                                        Html.span [ prop.text $"Versteckt ({lay.HiddenCount})" ]
                                        if hiddenExpanded then Icons.chevronUp Icons.XS Icons.Default
                                        else Icons.chevronDown Icons.XS Icons.Default
                                    ]
                                ]
                                if hiddenExpanded then
                                    Html.div [
                                        prop.className "flex flex-col"
                                        prop.children (lay.Hidden |> List.map (fun c -> itemRow "h" c.Name c))
                                    ]
                            ]
                        ]

                    if List.isEmpty rows && lay.HiddenCount = 0 then
                        Html.div [
                            prop.className $"py-2 {ListRoles.muted}"
                            prop.text "Keine Treffer"
                        ]
                ]
            ]

            match props.StandLabel with
            | Some label ->
                Html.div [
                    prop.className $"pt-2 border-t border-border-default {ListRoles.muted}"
                    prop.text label
                ]
            | None -> ()
        ]
    ]

// ============================================
// Hüllen
// ============================================

/// Hülle für den PC: Popover unter dem Auslöser. Der Aufrufer stellt den Auslöser in einen
/// `position:relative`-Wrapper (siehe `FilterPopover`). Esc oder ein Klick außerhalb
/// schließen, ohne zu ändern.
let popover (isOpen: bool) (props: CategoryPickerProps) : ReactElement =
    FilterPopover.view
        { IsOpen = isOpen; OnClose = props.OnClose; WidthClassName = FilterPopover.categoryWidthClass }
        [ Content { Picker = props; FocusSearch = true; ListClass = "max-h-72 overflow-y-auto" } ]

let private sheetWith (elevated: bool) (isOpen: bool) (subtitle: string option) (props: CategoryPickerProps) : ReactElement =
    // Das Sheet schließt gleich: ein später synthetischer Klick soll nicht die Liste dahinter
    // treffen (ADR 0005, Click-Commit).
    let closing =
        { props with
            OnSelect =
                fun choice ->
                    Viewport.swallowNextClick 350
                    Viewport.vibrate 8
                    props.OnSelect choice
            OnClose =
                fun () ->
                    Viewport.swallowNextClick 350
                    props.OnClose () }
    let sheetProps : BottomSheet.BottomSheetProps =
        { IsOpen = isOpen
          OnClose = closing.OnClose
          Title = "Kategorie wählen"
          Subtitle = subtitle
          Footer = None }
    let children =
        [ if isOpen then
              Content { Picker = closing; FocusSearch = Viewport.isFinePointer (); ListClass = "" } ]
    if elevated then BottomSheet.viewLayered sheetProps children
    else BottomSheet.view sheetProps children

/// Hülle für das Handy: BottomSheet mit Titel "Kategorie wählen" und optionaler Unterzeile
/// (zum Beispiel Payee und Betrag).
let sheet (isOpen: bool) (subtitle: string option) (props: CategoryPickerProps) : ReactElement =
    sheetWith false isOpen subtitle props

/// Wie `sheet`, aber auf der erhöhten Ebene, damit der Picker über dem Detail-Sheet liegt
let sheetLayered (isOpen: bool) (subtitle: string option) (props: CategoryPickerProps) : ReactElement =
    sheetWith true isOpen subtitle props

// ============================================
// Karte "Budget nach dem Wechsel"
// ============================================

/// Die rein darstellende Karte nach einem Kategoriewechsel (Tafel 4.3): je Zeile das
/// Kategorie-Label, der alte Wert durchgestrichen, ein Pfeil, der neue Wert. Die Texte kommen
/// fertig formatiert vom Aufrufer; Sichtbarkeit und Dauer steuert er ebenfalls.
module CategoryChangeCard =

    type ChangeLine = {
        Label: string
        OldText: string option
        NewText: string option
    }

    /// Welche Teile eine Zeile zeigt
    type LineParts =
        | LabelOnly
        | Transition of oldText: string option * newText: string option

    let heading = "Budget nach dem Wechsel"

    let lineParts (line: ChangeLine) : LineParts =
        match line.OldText, line.NewText with
        | None, None -> LabelOnly
        | o, n -> Transition (o, n)

    /// Eine Karte ohne Zeilen gibt es nicht
    let hasContent (lines: ChangeLine list) : bool = not (List.isEmpty lines)

    /// Alter Wert: gedämpft und durchgestrichen
    let oldValueClass = $"font-mono tabular-nums line-through {ListRoles.primarySize} {Colors.textMuted}"

    /// Neuer Wert: Hauptfarbe, Mono, ohne Glow
    let newValueClass = $"font-mono font-medium tabular-nums {ListRoles.primarySize} {Colors.textPrimary}"

    let private lineView (line: ChangeLine) : ReactElement =
        Html.div [
            prop.key line.Label
            prop.className "flex items-center justify-between gap-3 min-w-0"
            prop.children [
                PickerParts.name line.Label
                match lineParts line with
                | LabelOnly -> ()
                | Transition (oldText, newText) ->
                    Html.span [
                        prop.className "shrink-0 flex items-center gap-2 whitespace-nowrap"
                        prop.children [
                            match oldText with
                            | Some t -> Html.span [ prop.className oldValueClass; prop.text t ]
                            | None -> ()
                            if oldText.IsSome && newText.IsSome then
                                Html.span [ prop.className $"{Colors.textMuted} {ListRoles.primarySize}"; prop.text "→" ]
                            match newText with
                            | Some t -> Html.span [ prop.className newValueClass; prop.text t ]
                            | None -> ()
                        ]
                    ]
            ]
        ]

    let view (lines: ChangeLine list) : ReactElement =
        if not (hasContent lines) then
            Html.none
        else
            Html.div [
                prop.role "status"
                prop.className "flex flex-col gap-2 bg-surface-card border border-border-default rounded-xl px-4 py-3"
                prop.children [
                    PickerParts.groupHeader heading
                    yield! lines |> List.map lineView
                ]
            ]
