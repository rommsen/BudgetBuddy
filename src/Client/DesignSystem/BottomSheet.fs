module Client.DesignSystem.BottomSheet

open Feliz
open Fable.Core
open Fable.Core.JsInterop

[<Import("createPortal", from="react-dom")>]
let private createPortal (element: ReactElement) (container: Browser.Types.Element) : ReactElement = jsNative

let private renderToBody (element: ReactElement) =
    createPortal element Browser.Dom.document.body

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type BottomSheetProps = {
    IsOpen: bool
    OnClose: unit -> unit
    Title: string
    Subtitle: string option
    Footer: ReactElement list option
}

// Internal props for the React functional component
type private BottomSheetInternalProps = {
    Props: BottomSheetProps
    Children: ReactElement list
    /// Overlay und Sheet auf der erhöhten Ebene, damit das Sheet über einem anderen liegt
    Elevated: bool
    /// Feste Höhe (`.fixed-height`), damit das Sheet beim Wechsel des Inhalts nicht springt.
    FixedHeight: bool
    /// Knöpfe im Kopf, links neben Schließen.
    HeaderActions: ReactElement list
    /// Fester Bereich zwischen Kopf und scrollendem Inhalt.
    Pinned: ReactElement list
}

/// One selectable category in the picker.
/// `Label` is the full "Group: Name" string (grouped/filtered/displayed by it).
/// `Available` is the category's current YNAB Available; when present the picker
/// renders it right-aligned and colour-coded (green >0 / red <0 / muted =0).
/// `None` keeps the plain row (e.g. contexts where a budget value is meaningless).
type CategoryPickerOption = {
    Id: string
    Label: string
    Available: Shared.Domain.Money option
}

type private CategoryPickerInternalProps = {
    IsOpen: bool
    PayeeName: string
    Categories: CategoryPickerOption list
    SuggestedCategories: string list
    RecentCategories: string list
    OnSelect: string -> unit
    OnClose: unit -> unit
    /// Renders overlay/sheet on the elevated z-layer so the picker can open
    /// on top of another bottom sheet (e.g. the Quick Add form)
    Elevated: bool
}

/// Eine Rückfrage im Sheet (budgeting-r3wq8): `confirmDanger` als Gegenstück zu `Modal.confirmDanger`,
/// `confirm` als Gegenstück zu `Modal.confirm` (budgeting-xp46a).
type ConfirmDangerProps = {
    Title: string
    Message: string
    /// Der bestätigende Knopf, etwa "Entfernen".
    ConfirmText: string
    OnConfirm: unit -> unit
    OnCancel: unit -> unit
    /// Wohin der Fokus nach dem Schließen geht, als CSS-Selektor (budgeting-g2ty9, F3): der Knopf, der die
    /// Rückfrage geöffnet hat, an ihrer Stelle wieder gezeigt. Nach "Abbrechen" ist er wieder bedienbar, nach
    /// dem Bestätigen trägt er den laufenden Aufruf (`IsBusy`, fokussierbar). `None`: der Fokus bleibt, wo
    /// der Browser ihn lässt.
    ReturnFocus: string option
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// Renders a section title label (e.g. "Vorgeschlagen", "Zuletzt verwendet")
let sectionTitle (title: string) : ReactElement =
    PickerParts.groupHeader title

/// Renders a chip button used in suggestion / recent sections.
/// Selection commits on the real `click` — committing on pointerdown lets the
/// browser's synthetic click fall through to whatever sits behind the sheet
/// once it closes (the mobile "ghost click"). preventDefault on mousedown
/// keeps the search input focused so the keyboard doesn't shift the layout
/// between tap and click.
let chipButton (text: string) (isSuggested: bool) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.className (
            if isSuggested then "chip suggested"
            else "chip"
        )
        prop.onMouseDown (fun e -> e.preventDefault())
        prop.onClick (fun e ->
            e.stopPropagation()
            onClick()
        )
        prop.text text
    ]

/// Options-Zeile für Sortier- und Auswahl-Sheets (ledger-skpcx, ADR 0049): Beschriftung
/// links, ein Haken rechts bei der gewählten Option, 48 px Trefferfläche am Handy, 44 px ab md. Der Tipp
/// committet auf dem echten `click`, nicht auf `pointerdown` (siehe `chipButton`).
let optionRow (label: string) (isSelected: bool) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className "w-full text-left category-item min-h-12 md:min-h-[44px] flex items-center justify-between gap-3 min-w-0"
        prop.custom ("aria-pressed", isSelected)
        prop.onClick (fun e ->
            e.stopPropagation()
            onClick ()
        )
        prop.children [
            Truncate.text (if isSelected then "text-sm font-semibold text-neon-teal" else "text-sm") label
            if isSelected then
                Client.DesignSystem.Icons.check Client.DesignSystem.Icons.SM Client.DesignSystem.Icons.NeonTeal
        ]
    ]

let private closeIcon () : ReactElement =
    Svg.svg [
        svg.width 14
        svg.height 14
        svg.viewBox (0, 0, 24, 24)
        svg.custom ("fill", "none")
        svg.custom ("stroke", "currentColor")
        svg.custom ("strokeWidth", "2.5")
        svg.custom ("strokeLinecap", "round")
        svg.children [
            Svg.path [ svg.d "M18 6L6 18M6 6l12 12" ]
        ]
    ]

/// Locks page scrolling while the sheet is open (restores on close/unmount).
let private useBodyScrollLock (isOpen: bool) =
    React.useEffect (
        (fun () ->
            if isOpen then Viewport.lockBodyScroll ()
            React.createDisposable (fun () -> if isOpen then Viewport.unlockBodyScroll ())),
        [| box isOpen |]
    )

// ---------------------------------------------------------------------------
// Generic BottomSheet view
// ---------------------------------------------------------------------------

[<ReactComponent>]
let private BottomSheetInternal (input: BottomSheetInternalProps) =
    let props = input.Props
    let children = input.Children

    let activeClass = if props.IsOpen then " active" else ""
    let layerClass = if input.Elevated then " layer-2" else ""
    let heightClass = if input.FixedHeight then " fixed-height" else ""

    useBodyScrollLock props.IsOpen

    renderToBody (
        Html.div [
            prop.children [
                // Overlay
                Html.div [
                    prop.className ("overlay" + layerClass + activeClass)
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        props.OnClose()
                    )
                ]

                // Bottom sheet container
                Html.div [
                    prop.className ("bottom-sheet" + layerClass + heightClass + activeClass)
                    // Zu oder beim Ausfahren: nicht bedienbar, für den Screenreader nicht da.
                    if input.FixedHeight && not props.IsOpen then
                        prop.custom ("inert", "true")
                        prop.custom ("aria-hidden", true)
                    prop.onClick (fun e -> e.stopPropagation())
                    prop.children [
                        // Drag handle
                        Html.div [
                            prop.className "sheet-handle"
                        ]

                        // Header
                        Html.div [
                            prop.className "sheet-header"
                            prop.children [
                                Html.div [
                                    prop.children [
                                        // Ein langer Titel kürzt im festen Kopf, der title trägt ihn ganz.
                                        Html.h3 [
                                            prop.title props.Title
                                            prop.text props.Title
                                        ]
                                        match props.Subtitle with
                                        | Some sub ->
                                            Html.p [
                                                prop.title sub
                                                prop.text sub
                                            ]
                                        | None -> ()
                                    ]
                                ]
                                if not (List.isEmpty input.HeaderActions) then
                                    Html.div [
                                        prop.className "sheet-header-actions"
                                        prop.children input.HeaderActions
                                    ]
                                Html.button [
                                    prop.className "sheet-close"
                                    prop.ariaLabel "Schließen"
                                    prop.onClick (fun _ -> props.OnClose())
                                    prop.children [ closeIcon () ]
                                ]
                            ]
                        ]

                        if not (List.isEmpty input.Pinned) then
                            Html.div [
                                prop.className "sheet-pinned"
                                prop.children input.Pinned
                            ]

                        // Scrollable body
                        Html.div [
                            prop.className "sheet-body"
                            prop.children children
                        ]

                        // Optional footer (outside sheet-body, stays fixed at bottom)
                        match props.Footer with
                        | Some footerChildren ->
                            Html.div [
                                prop.className "rule-sheet-footer"
                                prop.children footerChildren
                            ]
                        | None -> ()
                    ]
                ]
            ]
        ]
    )

/// Renders a bottom sheet with overlay, drag handle, header and scrollable body.
/// The sheet and overlay are always in the DOM; visibility is toggled via the `.active` CSS class.
let view (props: BottomSheetProps) (children: ReactElement list) : ReactElement =
    BottomSheetInternal { Props = props; Children = children; Elevated = false; FixedHeight = false; HeaderActions = []; Pinned = [] }

type private InspectorContent = {
    Title: string
    Subtitle: string option
    HeaderActions: ReactElement list
    Pinned: ReactElement list
    Children: ReactElement list
}

/// Solange das Sheet ausfährt, steht sein letzter Inhalt noch da (`.bottom-sheet`, 450 ms).
[<Literal>]
let private ExitDurationMs = 500

[<ReactComponent>]
let private InspectorSheet (props: BottomSheetProps, headerActions: ReactElement list, pinned: ReactElement list, children: ReactElement list) =
    let held = React.useRef<InspectorContent option> None
    let released, setReleased = React.useState 0
    if props.IsOpen then
        held.current <- Some { Title = props.Title; Subtitle = props.Subtitle; HeaderActions = headerActions; Pinned = pinned; Children = children }
    React.useEffect (
        (fun () ->
            if props.IsOpen then
                React.createDisposable ignore
            else
                let timer =
                    Fable.Core.JS.setTimeout
                        (fun () ->
                            held.current <- None
                            setReleased (released + 1))
                        ExitDurationMs
                React.createDisposable (fun () -> Fable.Core.JS.clearTimeout timer)),
        [| box props.IsOpen |]
    )
    let shown =
        held.current
        |> Option.defaultValue { Title = props.Title; Subtitle = props.Subtitle; HeaderActions = []; Pinned = []; Children = [] }
    BottomSheetInternal
        { Props = { props with Title = shown.Title; Subtitle = shown.Subtitle }
          Children = shown.Children
          Elevated = false
          FixedHeight = true
          HeaderActions = shown.HeaderActions
          Pinned = shown.Pinned }

/// Das Sheet für den Inspektor (budgeting-g8s3w): feste Höhe, `headerActions` stehen im Kopf links
/// neben Schließen, Titel und Unterzeile kürzen statt umzubrechen, `pinned` steht fest zwischen Kopf und
/// Inhalt, nur `children` scrollt. Mit offener Tastatur wird es kleiner (ADR 0005). Bleibt immer
/// gerendert, `IsOpen` fährt es ein und aus, der Inhalt hält sich dabei selbst.
let inspector
    (props: BottomSheetProps)
    (headerActions: ReactElement list)
    (pinned: ReactElement list)
    (children: ReactElement list)
    : ReactElement =
    InspectorSheet (props, headerActions, pinned, children)

/// Wie `view`, aber auf der erhöhten Ebene (`layer-2`): das Sheet öffnet über einem anderen
/// Sheet, zum Beispiel der Kategorie-Picker über dem Buchungs-Detail.
let viewLayered (props: BottomSheetProps) (children: ReactElement list) : ReactElement =
    BottomSheetInternal { Props = props; Children = children; Elevated = true; FixedHeight = false; HeaderActions = []; Pinned = [] }

// ---------------------------------------------------------------------------
// Category Picker (specialised bottom sheet)
// ---------------------------------------------------------------------------

[<ReactComponent>]
let private CategoryPickerInternal (input: CategoryPickerInternalProps) =
    let searchText, setSearchText = React.useState ""

    let layerClass = if input.Elevated then " layer-2" else ""
    let activeClass = if input.IsOpen then " active" else ""

    useBodyScrollLock input.IsOpen

    // Wraps onSelect: clear search, arm the ghost-click guard (the sheet is
    // about to close — any late synthetic click must not reach the list
    // behind it), give light haptic feedback where supported.
    let selectAndClose catId =
        Viewport.swallowNextClick 350
        Viewport.vibrate 8
        setSearchText ""
        input.OnSelect catId

    let closeAndClear () =
        Viewport.swallowNextClick 350
        setSearchText ""
        input.OnClose()

    // Group categories by YNAB group prefix "GroupName: CategoryName"
    let groupCategories (cats: CategoryPickerOption list) =
        cats
        |> List.groupBy (fun c ->
            match c.Label.IndexOf(": ") with
            | -1 -> ""
            | idx -> c.Label.Substring(0, idx).Trim()
        )

    // Filter categories by search text (case-insensitive)
    let filteredCategories =
        if System.String.IsNullOrWhiteSpace searchText then
            input.Categories
        else
            let searchLower = searchText.ToLowerInvariant()
            input.Categories
            |> List.filter (fun c ->
                c.Label.ToLowerInvariant().Contains(searchLower))

    let groupedCategories = groupCategories filteredCategories

    // Find category name by id for chip display
    let categoryNameById (catId: string) =
        input.Categories
        |> List.tryFind (fun c -> c.Id = catId)
        |> Option.map (fun c -> c.Label)
        |> Option.defaultValue catId

    // Extract display name (part after "GroupName: " or full name)
    let displayName (name: string) =
        match name.IndexOf(": ") with
        | -1 -> name
        | idx -> name.Substring(idx + 2).Trim()

    renderToBody (
        Html.div [
            prop.children [
                // Overlay
                Html.div [
                    prop.className ("overlay" + layerClass + activeClass)
                    prop.onClick (fun e ->
                        e.stopPropagation()
                        closeAndClear()
                    )
                ]

                // Bottom sheet
                Html.div [
                    prop.className ("bottom-sheet" + layerClass + activeClass)
                    prop.onClick (fun e -> e.stopPropagation())
                    prop.children [
                        // Drag handle
                        Html.div [ prop.className "sheet-handle" ]

                        // Header (with explicit close button — the grabber
                        // alone is not a discoverable/accessible dismiss)
                        Html.div [
                            prop.className "sheet-header"
                            prop.children [
                                Html.div [
                                    prop.children [
                                        Html.h3 [ prop.text "Kategorie wählen" ]
                                        if input.PayeeName <> "" then
                                            Html.p [ prop.text input.PayeeName ]
                                    ]
                                ]
                                Html.button [
                                    prop.className "sheet-close"
                                    prop.ariaLabel "Schließen"
                                    prop.onClick (fun _ -> closeAndClear())
                                    prop.children [ closeIcon () ]
                                ]
                            ]
                        ]

                        // Search — pinned outside the scrollable body so it
                        // stays visible above the keyboard while the list
                        // scrolls underneath. Only mounted while open so
                        // autoFocus re-triggers on each open.
                        if input.IsOpen then
                            Html.div [
                                prop.className "sheet-search"
                                prop.children [
                                    Html.input [
                                        prop.type' "text"
                                        prop.placeholder "Kategorie suchen…"
                                        prop.value searchText
                                        prop.onChange setSearchText
                                        // Autofocus only on mouse/trackpad devices:
                                        // on touch it would summon the keyboard
                                        // before the user even sees the suggestions.
                                        prop.autoFocus (Viewport.isFinePointer())
                                    ]
                                ]
                            ]

                        // Body - only mount content when open
                        Html.div [
                            prop.className "sheet-body"
                            prop.children [
                                if input.IsOpen then
                                    // Suggested section
                                    if not input.SuggestedCategories.IsEmpty && System.String.IsNullOrWhiteSpace searchText then
                                        Html.div [
                                            prop.className "sheet-section"
                                            prop.children [
                                                sectionTitle "Vorgeschlagen"
                                                Html.div [
                                                    prop.className "suggestion-chips"
                                                    prop.children [
                                                        for i, catId in input.SuggestedCategories |> List.indexed do
                                                            chipButton
                                                                (categoryNameById catId)
                                                                (i = 0)
                                                                (fun () -> selectAndClose catId)
                                                    ]
                                                ]
                                            ]
                                        ]

                                    // Recent section
                                    if not input.RecentCategories.IsEmpty && System.String.IsNullOrWhiteSpace searchText then
                                        Html.div [
                                            prop.className "sheet-section"
                                            prop.children [
                                                sectionTitle "Zuletzt verwendet"
                                                Html.div [
                                                    prop.className "suggestion-chips"
                                                    prop.children [
                                                        for catId in input.RecentCategories do
                                                            chipButton
                                                                (categoryNameById catId)
                                                                false
                                                                (fun () -> selectAndClose catId)
                                                    ]
                                                ]
                                            ]
                                        ]

                                    // All categories (grouped). Der Kopf "Alle Kategorien" steht nur,
                                    // wenn darüber Vorgeschlagen oder Zuletzt verwendet erscheint.
                                    let isSearching = not (System.String.IsNullOrWhiteSpace searchText)
                                    Html.div [
                                        prop.className "sheet-section"
                                        prop.children [
                                            if PickerParts.showAllCategoriesHeader
                                                (not input.SuggestedCategories.IsEmpty)
                                                (not input.RecentCategories.IsEmpty)
                                                isSearching then
                                                sectionTitle "Alle Kategorien"

                                            for (groupName, items) in groupedCategories do
                                                Html.div [
                                                    prop.className "category-group"
                                                    prop.children [
                                                        if groupName <> "" then
                                                            PickerParts.groupHeader groupName
                                                        for c in items do
                                                            PickerParts.row
                                                                "category-item"
                                                                [
                                                                    prop.key c.Id
                                                                    // Keep focus (and keyboard) on the search
                                                                    // input, prevents a blur-induced layout
                                                                    // shift between tap and click.
                                                                    prop.onMouseDown (fun e -> e.preventDefault())
                                                                    prop.onClick (fun e ->
                                                                        e.stopPropagation()
                                                                        selectAndClose c.Id)
                                                                ]
                                                                (PickerParts.name (displayName c.Label))
                                                                (c.Available |> Option.map (fun m -> m.Amount, m.Currency))
                                                    ]
                                                ]
                                        ]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    )

/// Specialised category picker bottom sheet.
/// Categories are `CategoryPickerOption` records. The `Label` ("Group: Name") drives
/// grouping/filtering; an optional `Available` renders right-aligned and colour-coded.
let categoryPicker
    (isOpen: bool)
    (payeeName: string)
    (categories: CategoryPickerOption list)
    (suggestedCategories: string list)
    (recentCategories: string list)
    (onSelect: string -> unit)
    (onClose: unit -> unit)
    : ReactElement =
    CategoryPickerInternal {
        IsOpen = isOpen
        PayeeName = payeeName
        Categories = categories
        SuggestedCategories = suggestedCategories
        RecentCategories = recentCategories
        OnSelect = onSelect
        OnClose = onClose
        Elevated = false
    }

/// Category picker on the elevated z-layer — for opening on top of another
/// bottom sheet (e.g. from the Quick Add form).
let categoryPickerLayered
    (isOpen: bool)
    (payeeName: string)
    (categories: CategoryPickerOption list)
    (suggestedCategories: string list)
    (recentCategories: string list)
    (onSelect: string -> unit)
    (onClose: unit -> unit)
    : ReactElement =
    CategoryPickerInternal {
        IsOpen = isOpen
        PayeeName = payeeName
        Categories = categories
        SuggestedCategories = suggestedCategories
        RecentCategories = recentCategories
        OnSelect = onSelect
        OnClose = onClose
        Elevated = true
    }

// ---------------------------------------------------------------------------
// Rückfrage im Sheet
// ---------------------------------------------------------------------------

type private ConfirmBlockProps = {
    Props: ConfirmDangerProps
    /// Roter Knopf (`confirmDanger`) oder Orange (`confirm`).
    Danger: bool
}

/// Die Rückfrage als Block im Sheet: Titel, Text, "Abbrechen" und der Knopf. `role "alertdialog"` mit
/// `aria-labelledby` auf den Titel und `aria-describedby` auf den Text, ohne `aria-modal`: sie steht im
/// Fluss des Sheets, nicht darüber, und fängt den Fokus nicht (Styleguide §5). Beim Öffnen hat "Abbrechen"
/// den Fokus, ein versehentliches Enter löst nichts aus. Escape bricht ab wie "Abbrechen". Nach dem
/// Schließen geht der Fokus auf `ReturnFocus`, sonst läge er auf `body` (budgeting-g2ty9, F3). Das nur, wenn
/// er beim Aushängen noch in der Rückfrage lag oder auf `body` gefallen ist: wer im Feld darüber tippt und
/// sie damit schließt, behält den Fokus im Feld (budgeting-f4k9n, M1).
[<ReactComponent>]
let private ConfirmBlock (input: ConfirmBlockProps) =
    let props = input.Props
    let container = React.useRef<Browser.Types.HTMLElement option> None
    // Die Id entsteht einmal je Rückfrage, nicht je Render.
    let id = React.useMemo ((fun () -> string (System.Guid.NewGuid())), [||])
    let titleId = $"confirm-title-{id}"
    let messageId = $"confirm-message-{id}"
    let returnFocus = props.ReturnFocus
    React.useEffect (
        (fun () ->
            // Beim Aushängen ist die Ref schon geleert: das Element hier festhalten.
            let mounted = container.current
            match mounted with
            | Some el ->
                match el.querySelector "[data-confirm-cancel] button" with
                | null -> ()
                | cancel -> (cancel :?> Browser.Types.HTMLElement).focus ()
            | None -> ()
            React.createDisposable (fun () ->
                // Zurück nur, wenn der Fokus beim Aushängen noch in der Rückfrage lag oder mit ihr auf `body`
                // gefallen ist (Abbrechen, Escape, Bestätigen). Schließt sie, weil im Betrags- oder Datumsfeld
                // getippt wurde, bleibt der Fokus dort (budgeting-f4k9n, M1).
                let active = Browser.Dom.document.activeElement
                let focusLost =
                    isNull active
                    || obj.ReferenceEquals (active, Browser.Dom.document.body)
                    || mounted |> Option.exists (fun el -> el.contains active)
                match returnFocus with
                | Some selector when focusLost ->
                    // Erst nach dem Render, der den auslösenden Knopf wieder zeigt.
                    Browser.Dom.window.setTimeout (
                        (fun () ->
                            match Browser.Dom.document.querySelector selector with
                            | null -> ()
                            | target -> (target :?> Browser.Types.HTMLElement).focus ()),
                        0
                    )
                    |> ignore
                | _ -> ())),
        [||]
    )
    let border = if input.Danger then "border-neon-red/20" else Tokens.Borders.default'
    Html.div [
        prop.ref container
        prop.onKeyDown (fun e ->
            if e.key = "Escape" then
                e.preventDefault ()
                e.stopPropagation ()
                props.OnCancel ())
        prop.role "alertdialog"
        prop.ariaLabelledBy titleId
        prop.ariaDescribedBy messageId
        prop.custom ("data-testid", (if input.Danger then "confirm-danger" else "confirm-sheet"))
        prop.className $"flex flex-col gap-2 rounded-xl border {border} bg-surface-card p-4"
        prop.children [
            Html.h3 [ prop.id titleId; prop.className Tokens.ListRoles.primary; prop.text props.Title ]
            Html.p [ prop.id messageId; prop.className Tokens.ListRoles.secondary; prop.text props.Message ]
            Html.div [
                prop.className "grid grid-cols-2 gap-2"
                prop.children [
                    Html.div [
                        prop.custom ("data-confirm-cancel", "true")
                        prop.children [
                            Button.view
                                { Button.defaultProps with
                                    Text = "Abbrechen"
                                    Variant = Button.Secondary
                                    ClassName = Some $"w-full {Tokens.TouchTargets.sheetMinHeight}"
                                    OnClick = props.OnCancel }
                        ]
                    ]
                    Button.view
                        { Button.defaultProps with
                            Text = props.ConfirmText
                            Variant = (if input.Danger then Button.Danger else Button.Primary)
                            ClassName = Some $"w-full {Tokens.TouchTargets.sheetMinHeight}"
                            OnClick = props.OnConfirm }
                ]
            ]
        ]
    ]

/// Rückfrage vor einer zerstörenden Aktion im Sheet, der Knopf rot (budgeting-r3wq8). Gegenstück zu
/// `Modal.confirmDanger`.
let confirmDanger (props: ConfirmDangerProps) = ConfirmBlock { Props = props; Danger = true }

/// Rückfrage vor einer Aktion mit Folgen im Sheet, der Knopf Orange (budgeting-xp46a, erster Nutzer: der
/// Wechsel der Art eines Targets). Gegenstück zu `Modal.confirm`. Dieselben Felder wie `confirmDanger`.
let confirm (props: ConfirmDangerProps) = ConfirmBlock { Props = props; Danger = false }
