/// Die Payee-Auswahl (ledger-fxt09): ein Suchfeld, vorbelegt mit dem heutigen Payee, darunter die
/// Payees, die zur Eingabe passen (häufigste zuerst), und als erste Zeile „„<Eingabe>“ übernehmen“,
/// wenn die Eingabe kein vorhandener Payee ist. Ein Klick übernimmt sofort (Click-Commit, §7.1), am
/// PC steht der Fokus in der Suche, ↑↓ und Enter wählen, Esc schließt nur die Auswahl (§7.9). Am
/// Handy kein Autofokus. Der Baustein kennt weder Kassenbuch-Model noch -Msg: der Aufrufer liefert
/// Payee, Vorschläge und Rückrufe. Das Buchungs-Detail zeigt ihn im Sheet (Handy) oder im Dialog
/// (PC); die Karte beim Durchgehen (ledger-v6s7c) öffnet ihn ohne das Detail. Hat die Buchung einen
/// Payee, steht unter den Vorschlägen „Payee entfernen“ (ledger-pkrwd); die Zeile übernimmt wie eine
/// Vorschlagszeile, mit `None` als Ziel.
module Client.DesignSystem.PayeePicker

open System
open Feliz
open Fable.Core.JsInterop
open Client.DesignSystem.Tokens

/// Eine Zeile der Auswahl.
type PayeeOption =
    /// Die getippte Eingabe, getrimmt, als neuer Payee.
    | TakeTyped of payee: string
    /// Ein vorhandener Payee mit der Zahl seiner Buchungen.
    | Suggested of payee: string * count: int
    /// „Payee entfernen“ (ledger-pkrwd): das Ziel ist "ohne Payee".
    | RemovePayee

/// Höchstens so viele Vorschläge stehen in der Liste; die Suche grenzt weiter ein.
let maxShown = 50

/// Die Zeile, die die Eingabe als neuen Payee übernimmt.
let takeLabel (typed: string) : string = $"„{typed}“ übernehmen"

/// Die Zeile, die den Payee entfernt (ledger-pkrwd).
let removeLabel = "Payee entfernen"

let searchPlaceholder = "Payee suchen oder eingeben…"
let loadingText = "Payees werden geladen…"
let emptyText = "Noch keine Payees im Kassenbuch"

/// Die Id des Feldfehlers, auf die das Suchfeld per `aria-describedby` verweist.
let fieldErrorId = "payee-picker-error"

/// Die Id der Listbox, auf die das Suchfeld per `aria-controls` verweist.
let listboxId = "payee-picker-listbox"

/// Die Id einer Zeile, auf die das Suchfeld per `aria-activedescendant` verweist, solange sie markiert ist.
let optionId (index: int) : string = $"payee-picker-option-{index}"

let private keyOf (text: string) = text.Trim().ToLowerInvariant()

/// Die Zeilen zur Eingabe: zuerst „übernehmen“, wenn die getrimmte Eingabe nicht leer und kein
/// vorhandener Payee ist (ohne Groß- und Kleinschreibung), danach die passenden Payees nach Zahl der
/// Buchungen, höchstens `maxShown`. Leere Eingabe zeigt alle. Rein.
let options (suggestions: (string * int) list) (search: string) : PayeeOption list =
    let typed = if isNull search then "" else search.Trim()
    let key = keyOf typed
    let exists = suggestions |> List.exists (fun (payee, _) -> keyOf payee = key)
    let matches =
        suggestions
        |> List.filter (fun (payee, _) -> key = "" || payee.ToLowerInvariant().Contains key)
        |> List.sortByDescending snd
        |> List.truncate maxShown
        |> List.map Suggested
    [ if typed <> "" && not exists then TakeTyped typed ] @ matches

/// Nur die vorhandenen Payees zur Eingabe, ohne „übernehmen“ (ledger-dn4vy): der Payee-Filter des
/// Kassenbuchs sucht unter den Treffern, ein getippter Name ohne Buchung fände nichts. Rein.
let suggestedOptions (suggestions: (string * int) list) (search: string) : PayeeOption list =
    options suggestions search |> List.filter (function Suggested _ -> true | TakeTyped _ | RemovePayee -> false)

/// Die Zeilen der Auswahl: `options` und darunter „Payee entfernen“, wenn die Buchung einen Payee
/// hat (`current`, leer zählt als keiner). Rein.
let pickerRows (suggestions: (string * int) list) (search: string) (current: string option) : PayeeOption list =
    let hasPayee = current |> Option.exists (fun p -> p.Trim() <> "")
    options suggestions search @ [ if hasPayee then RemovePayee ]

/// Das Ziel, das eine Zeile übernimmt: ein Payee oder `None` („Payee entfernen“).
let payeeOf (option: PayeeOption) : string option =
    match option with
    | TakeTyped payee -> Some payee
    | Suggested (payee, _) -> Some payee
    | RemovePayee -> None

/// Was eine Taste im Suchfeld tut.
type KeyAction =
    | MoveTo of int
    /// Übernimmt dieses Ziel: einen Text (auch leer: dann meldet der Aufrufer den Feldfehler) oder
    /// `None` („Payee entfernen“).
    | Pick of string option
    | CloseOnly
    | NoAction

/// ↑↓ bewegen die Markierung, Enter übernimmt die markierte Zeile, ohne Markierung die erste, ohne
/// Zeile die Eingabe selbst. „Payee entfernen“ wählt Enter nur markiert, nie als erste Zeile: eine
/// leere Eingabe bleibt sonst ein Feldfehler. Esc schließt nur die Auswahl. Rein.
let keyAction (key: string) (rows: PayeeOption list) (active: int option) (search: string) : KeyAction =
    let count = List.length rows
    match key with
    | "ArrowDown" when count > 0 -> MoveTo (match active with None -> 0 | Some i -> min (count - 1) (i + 1))
    | "ArrowUp" when count > 0 -> MoveTo (match active with None -> count - 1 | Some i -> max 0 (i - 1))
    | "Enter" ->
        match active with
        | Some i when i >= 0 && i < count -> Pick (payeeOf (List.item i rows))
        | _ ->
            match rows |> List.filter (function RemovePayee -> false | TakeTyped _ | Suggested _ -> true) with
            | first :: _ -> Pick (payeeOf first)
            | [] -> Pick (Some (if isNull search then "" else search))
    | "Escape" -> CloseOnly
    | _ -> NoAction

type PayeePickerProps = {
    /// Der heutige Payee der Buchung, vorbelegt im Suchfeld.
    Current: string option
    /// Die Payees des Kassenbuchs mit der Zahl ihrer Buchungen.
    Suggestions: (string * int) list
    /// Die Vorschläge laden noch.
    Loading: bool
    /// Die Vorschläge ließen sich nicht laden (die Eingabe geht trotzdem).
    LoadError: string option
    /// Der Feldfehler unter dem Suchfeld (leer, zu lang, vom Server abgelehnt).
    FieldError: string option
    /// Fokus ins Suchfeld beim Öffnen (am PC; am Handy käme sonst die Tastatur).
    FocusSearch: bool
    /// Ein Aufruf läuft: weitere Übernahmen wirken nicht.
    Busy: bool
    /// Übernimmt einen Text oder `None` („Payee entfernen“).
    OnPick: string option -> unit
    /// Getippt: der Aufrufer nimmt einen Feldfehler zurück.
    OnSearchChanged: unit -> unit
    OnClose: unit -> unit
}

/// Die Klasse einer Zeile: 48 px am Handy, markiert in `bg-surface-hover`. Auch der Payee-Filter des
/// Kassenbuchs nutzt sie (ledger-dn4vy).
let rowClassFor (isActive: bool) : string =
    let tone = if isActive then "bg-surface-hover" else "hover:bg-surface-hover"
    $"px-2 {TouchTargets.mobileMinHeight} md:min-h-9 rounded-lg cursor-pointer touch-manipulation {tone}"

[<ReactComponent>]
let View (props: PayeePickerProps) =
    let search, setSearch = React.useState (props.Current |> Option.defaultValue "")
    let active, setActive = React.useState<int option> None
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None
    let rows = pickerRows props.Suggestions search props.Current

    // Fokus ins Suchfeld beim Öffnen, der heutige Payee markiert: Tippen ersetzt ihn.
    React.useEffectOnce (fun () ->
        if props.FocusSearch then
            match containerRef.current with
            | Some el ->
                let field = el.querySelector "input"
                if not (isNull field) then
                    let input = field :?> Browser.Types.HTMLInputElement
                    input.focus ()
                    input.select ()
            | None -> ())

    // Die markierte Zeile bleibt sichtbar.
    React.useEffect (
        (fun () ->
            match active, containerRef.current with
            | Some i, Some el ->
                let target = el.querySelector $"[data-row-index='{i}']"
                if not (isNull target) then target?scrollIntoView (createObj [ "block" ==> "nearest" ])
            | _ -> ()),
        [| box active |]
    )

    let pick (payee: string option) = if not props.Busy then props.OnPick payee

    Html.div [
        prop.ref containerRef
        prop.custom ("data-testid", "payee-picker")
        prop.className "flex flex-col gap-3"
        prop.onKeyDown (fun e ->
            match keyAction e.key rows active search with
            | MoveTo i ->
                e.preventDefault ()
                setActive (Some i)
            | Pick payee ->
                e.preventDefault ()
                pick payee
            | CloseOnly ->
                // Esc bleibt in der Auswahl: das Dokument schlösse sonst zugleich das Detail.
                e.preventDefault ()
                e.stopPropagation ()
                props.OnClose ()
            | NoAction -> ())
        prop.children [
            Html.div [
                prop.custom ("data-testid", "payee-picker-search")
                prop.children [
                    Input.text
                        { Input.textInputDefaults with
                            Value = search
                            OnChange =
                                fun text ->
                                    setSearch text
                                    setActive None
                                    props.OnSearchChanged ()
                            Placeholder = searchPlaceholder
                            Size = Input.Small
                            AriaLabel = Some "Payee"
                            DescribedBy = (if props.FieldError.IsSome then Some fieldErrorId else None)
                            AriaInvalid = props.FieldError.IsSome
                            Combobox =
                                Some
                                    { ListboxId = listboxId
                                      Expanded = not (List.isEmpty rows)
                                      ActiveOptionId = active |> Option.filter (fun i -> i < List.length rows) |> Option.map optionId }
                            ClassName = Some "w-full" }
                    match props.FieldError with
                    | Some message ->
                        Html.div [
                            prop.id fieldErrorId
                            prop.custom ("data-testid", fieldErrorId)
                            prop.className "mt-1.5"
                            prop.children [ ErrorDisplay.inline' message ]
                        ]
                    | None -> ()
                ]
            ]
            Html.div [
                prop.className "flex flex-col"
                prop.children [
                    Html.div [
                        prop.id listboxId
                        prop.role "listbox"
                        prop.ariaLabel "Payees"
                        prop.className "flex flex-col"
                        prop.children [
                            for index, row in List.indexed rows do
                                let label, count =
                                    match row with
                                    | TakeTyped payee -> takeLabel payee, None
                                    | Suggested (payee, n) -> payee, Some n
                                    | RemovePayee -> removeLabel, None
                                PickerParts.row
                                    (rowClassFor (active = Some index))
                                    [
                                        prop.key (match row with TakeTyped p -> $"take-{p}" | Suggested (p, _) -> $"s-{p}" | RemovePayee -> "remove")
                                        prop.id (optionId index)
                                        prop.role "option"
                                        prop.custom ("aria-selected", (active = Some index))
                                        prop.custom ("data-row-index", index)
                                        prop.custom ("data-testid", (match row with TakeTyped _ -> "payee-picker-take" | Suggested _ -> "payee-picker-option" | RemovePayee -> "payee-picker-remove"))
                                        // Der Fokus bleibt im Suchfeld: am Handy bleibt die Tastatur offen (§7.1).
                                        prop.onMouseDown (fun e -> e.preventDefault ())
                                        prop.onClick (fun e ->
                                            e.stopPropagation ()
                                            pick (payeeOf row))
                                    ]
                                    (match row, count with
                                     // Ruhiger Text, keine Fehlerfarbe: Entfernen ist kein Fehler (ledger-pkrwd).
                                     | RemovePayee, _ -> PickerParts.mutedName label
                                     | _, None -> PickerParts.name label
                                     | _, Some n ->
                                         Html.span [
                                             prop.className "flex min-w-0 flex-1 items-center justify-between gap-3"
                                             prop.children [
                                                 PickerParts.name label
                                                 Html.span [
                                                     prop.className $"{Fonts.mono} {Colors.textMuted} {FontSizes.xs} tabular-nums shrink-0"
                                                     prop.text (Money.formatCount n)
                                                 ]
                                             ]
                                         ])
                                    None
                        ]
                    ]
                    // Lade-, Fehler- und Leertext stehen unter der Listbox: in ihr sind nur Optionen erlaubt.
                    if props.Loading then
                        Html.p [ prop.className $"py-2 {FontSizes.sm} {Colors.textMuted}"; prop.text loadingText ]
                    else
                        match props.LoadError with
                        | Some message -> ErrorDisplay.inline' message
                        // „Payee entfernen“ allein zählt nicht als Vorschlag: der Leertext bleibt (Review ledger-pkrwd).
                        | None when List.isEmpty props.Suggestions
                                    && rows |> List.forall (function RemovePayee -> true | TakeTyped _ | Suggested _ -> false) ->
                            Html.p [ prop.className $"py-2 {FontSizes.sm} {Colors.textMuted}"; prop.text emptyText ]
                        | None -> ()
                ]
            ]
        ]
    ]

/// Die Auswahl mit diesen Eigenschaften.
let view (props: PayeePickerProps) : ReactElement = View props
