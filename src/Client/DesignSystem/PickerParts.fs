/// Gemeinsame Bausteine der Kategorie-Auswahlen (design-system-zahv9, ADR 0049):
/// Zeile, Gruppenkopf, Suchfeld und Footer. `CategoryMultiSelect` (Popover, Mehrfach)
/// und `CategoryPicker` (Einfach) bauen beide darauf auf. Das
/// Verhalten (Checkbox und Übernehmen, Tippen und Schließen, ADR-0005-Mechanik)
/// bleibt bei den Aufrufern.
module Client.DesignSystem.PickerParts

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// Reine Klassenfunktionen und Texte
// ============================================

/// Zeile: einzeilig, Name links, Betrag rechts. Innenabstand setzt der Aufrufer.
let rowClass = "flex items-center justify-between gap-3 min-w-0"

/// Name in der Zeile: Größe und Gewicht der Listenrolle `primary`, einzeilig gekürzt
let nameClass = $"{ListRoles.primary} truncate min-w-0"

/// Betragsbereich: feste Breite, schrumpft nicht, rechtsbündig
let amountSlotClass = "shrink-0 w-24 text-right"

/// Gruppenkopf: Mono 10px, gesperrt, gedämpft
let groupHeaderClass = $"{ListRoles.groupHeader} pb-1"

/// Erscheint "Alle Kategorien" über der gegliederten Liste? Nur wenn darüber
/// "Vorgeschlagen" oder "Zuletzt verwendet" steht und keine Suche aktiv ist.
let showAllCategoriesHeader (hasSuggested: bool) (hasRecent: bool) (isSearching: bool) : bool =
    (hasSuggested || hasRecent) && not isSearching

/// Beschriftung des Übernehmen-Knopfs: die Zahl steht nur bei Auswahl
let applyLabel (selectedCount: int) : string =
    if selectedCount <= 0 then "Übernehmen" else $"Übernehmen ({selectedCount})"

/// Übernehmen: kompakter Primärknopf ohne Glow, einzeilig
let applyButtonClass =
    $"inline-flex items-center justify-center whitespace-nowrap rounded-lg px-4 py-2 {TouchTargets.mobileMinHeight} md:min-h-[36px] text-sm font-semibold text-on-accent bg-accent transition-colors hover:brightness-110 active:scale-[0.98]"

/// Übernehmen gesperrt (zum Beispiel bei ungültigem Datum): gedämpft, ohne Hover und
/// ohne Druckeffekt
let applyButtonDisabledClass =
    $"inline-flex items-center justify-center whitespace-nowrap rounded-lg px-4 py-2 {TouchTargets.mobileMinHeight} md:min-h-[36px] text-sm font-semibold text-on-accent bg-accent opacity-50 cursor-not-allowed"

/// Klasse des Übernehmen-Knopfs je Zustand
let applyButtonClassFor (disabled: bool) : string =
    if disabled then applyButtonDisabledClass else applyButtonClass

// ============================================
// Bausteine
// ============================================

/// Name einer Zeile
let name (text: string) : ReactElement =
    Html.span [
        prop.className nameClass
        prop.title text
        prop.text text
    ]

/// Picker-Zeile. `nameElement` ist der Name (bei Mehrfachauswahl die Checkbox mit
/// Beschriftung), `available` der Verfügbar-Betrag. `extraClass` und `extraProps`
/// tragen Innenabstand und Verhalten des Aufrufers.
let row (extraClass: string) (extraProps: IReactProperty list) (nameElement: ReactElement) (available: (decimal * string) option) : ReactElement =
    Html.div [
        yield! extraProps
        prop.className $"{rowClass} {extraClass}"
        prop.children [
            nameElement
            match available with
            | Some (amount, currency) ->
                Html.span [
                    prop.className amountSlotClass
                    prop.children [ Money.available amount currency ]
                ]
            | None -> ()
        ]
    ]

/// Gruppenkopf
let groupHeader (text: string) : ReactElement =
    Html.div [
        prop.className groupHeaderClass
        prop.text text
    ]

/// Kompaktes Suchfeld des Popovers (Input.Small: 16px Handy, 14px PC, 36px hoch)
let searchField (value: string) (onChange: string -> unit) (placeholder: string) : ReactElement =
    Input.text { Input.textInputDefaults with
                    Value = value
                    OnChange = onChange
                    Placeholder = placeholder
                    Size = Input.Small }

/// Wie `searchField`, als Combobox: `listboxId` nennt die Liste, `activeOptionId` die markierte Zeile
/// (`aria-activedescendant`, §7.9, design-system-gcpws).
let comboSearchField
    (value: string)
    (onChange: string -> unit)
    (placeholder: string)
    (listboxId: string)
    (activeOptionId: string option)
    : ReactElement =
    Input.text { Input.textInputDefaults with
                    Value = value
                    OnChange = onChange
                    Placeholder = placeholder
                    Size = Input.Small
                    Combobox = Some { ListboxId = listboxId; Expanded = true; ActiveOptionId = activeOptionId } }

/// Übernehmen-Knopf mit freier Beschriftung (Zahl nur bei Auswahl, siehe `applyLabel`)
let applyButton (selectedCount: int) (onApply: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className applyButtonClass
        prop.onClick (fun _ -> onApply ())
        prop.text (applyLabel selectedCount)
    ]

/// Übernehmen mit Sperre: bei `disabled` ist der Knopf deaktiviert und tut nichts
let applyButtonWhen (disabled: bool) (selectedCount: int) (onApply: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className (applyButtonClassFor disabled)
        prop.disabled disabled
        prop.onClick (fun _ -> if not disabled then onApply ())
        prop.text (applyLabel selectedCount)
    ]

/// Footer des Popovers: Zurücksetzen (Ghost) links, Übernehmen rechts
let footer (selectedCount: int) (onReset: unit -> unit) (onApply: unit -> unit) : ReactElement =
    Primitives.HStack.spaceBetween [
        Button.ghost "Zurücksetzen" onReset
        applyButton selectedCount onApply
    ]

/// Footer wie `footer`, mit gesperrtem Übernehmen (zum Beispiel solange die Auswahl unverändert ist)
let footerWhen (disabled: bool) (selectedCount: int) (onReset: unit -> unit) (onApply: unit -> unit) : ReactElement =
    Primitives.HStack.spaceBetween [
        Button.ghost "Zurücksetzen" onReset
        applyButtonWhen disabled selectedCount onApply
    ]

/// Wohin ↑↓ in einer kurzen Auswahl ohne Suche den Fokus setzen (§7.9, F7, design-system-gcpws): auf die nächste bzw.
/// vorige Option, am Rand bleibt er stehen. Ohne Fokus auf einer Option ↓ die erste, ↑ die letzte. `None`: die Taste
/// gehört nicht der Auswahl.
let shortChoiceTarget (key: string) (count: int) (focused: int option) : int option =
    match key, focused with
    | _, _ when count = 0 -> None
    | "ArrowDown", Some i -> Some (min (count - 1) (i + 1))
    | "ArrowUp", Some i -> Some (max 0 (i - 1))
    | "ArrowDown", None -> Some 0
    | "ArrowUp", None -> Some (count - 1)
    | _ -> None

/// Eine kurze Auswahl ohne Suche (Konto in „Neue Buchung“, Gruppe in „Kategorie bearbeiten“; F7, G13): beim Öffnen am
/// PC steht der Fokus auf der gewählten Option (`aria-current`), sonst auf der ersten; ↑↓ bewegen ihn, Enter wählt über
/// den Knopf genau einmal. `returnFocusTo`: der Selektor des Felds, das die Auswahl geöffnet hat; schließt sie (Esc,
/// Wahl), geht der Fokus dorthin zurück (§7.8 Punkt 6), sofern das Feld noch steht. `data-short-choice` markiert die
/// Auswahl für die Tests.
[<ReactComponent>]
let ShortChoiceFor (testId: string) (returnFocusTo: string option) (children: ReactElement list) =
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let options () : Browser.Types.HTMLElement list =
        match containerRef.current with
        | Some el ->
            let found = el.querySelectorAll "button"
            [ for i in 0 .. int found.length - 1 -> found.item (float i) :?> Browser.Types.HTMLElement ]
        | None -> []
    React.useEffect (
        (fun () ->
            let fine = Viewport.isFinePointer ()
            if fine then
                match containerRef.current with
                | Some el ->
                    let current = el.querySelector "button[aria-current='true']"
                    let target = if isNull current then el.querySelector "button" else current
                    if not (isNull target) then (target :?> Browser.Types.HTMLElement).focus ()
                | None -> ()
            React.createDisposable (fun () ->
                match returnFocusTo with
                | Some selector when fine ->
                    let field = Browser.Dom.document.querySelector selector
                    if not (isNull field) then (field :?> Browser.Types.HTMLElement).focus ()
                | _ -> ())),
        [||]
    )
    Html.div [
        prop.ref containerRef
        prop.role "group"
        prop.className "flex flex-col gap-0.5"
        prop.custom ("data-testid", testId)
        prop.custom ("data-short-choice", "")
        prop.onKeyDown (fun e ->
            let all = options ()
            let focused = all |> List.tryFindIndex (fun b -> obj.ReferenceEquals (b, Browser.Dom.document.activeElement))
            match shortChoiceTarget e.key all.Length focused with
            | Some i ->
                e.preventDefault ()
                e.stopPropagation ()
                (List.item i all).focus ()
            | None -> ())
        prop.children children
    ]

/// `ShortChoiceFor` ohne Rückkehr des Fokus (Konto in „Neue Buchung“).
let ShortChoice (testId: string) (children: ReactElement list) = ShortChoiceFor testId None children

/// Eine Option einer kurzen Auswahl als Zeile einer Karte (Prototyp `cegroup`, G13): oben der Name, darunter gedämpft
/// ein Zusatz (etwa „2 Kategorien“), rechts der Haken an der gewählten. Die gewählte trägt `aria-current`, darauf setzt
/// `ShortChoiceFor` den Fokus. Am Handy 56 px hoch.
let shortChoiceRow (label: string) (detail: string option) (isChosen: bool) (onPick: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        if isChosen then prop.custom ("aria-current", "true")
        prop.className (
            "flex w-full min-h-[56px] items-center gap-3 px-4 py-2 text-left border-b border-border-subtle last:border-b-0 "
            + "transition-colors hover:bg-surface-hover focus-visible:outline-none focus-visible:ring-2 "
            + "focus-visible:ring-inset focus-visible:ring-accent "
            + (if isChosen then Backgrounds.surface else "")
        )
        prop.onClick (fun _ -> onPick ())
        prop.children [
            Html.span [
                prop.className "flex min-w-0 flex-1 flex-col"
                prop.children [
                    Truncate.text $"block {ListRoles.primary}" label
                    match detail with
                    | Some d -> Truncate.text $"block {ListRoles.secondary}" d
                    | None -> ()
                ]
            ]
            if isChosen then Icons.check Icons.SM Icons.Accent
        ]
    ]
