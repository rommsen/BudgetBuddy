/// Die Filter-Chip-Leiste (ledger-j8xq4, Handoff "Grundlage, die für alle Screens
/// gilt": Kontostände, sticky Chipleiste, Summenzeile, Liste). Ein Chip ist zwei
/// echte Knöpfe in einem Rahmen, kein Knopf-im-Knopf: einer öffnet die Auswahl
/// (oder kippt einen Ein/Aus-Filter wie "Zu prüfen"), der andere entfernt den
/// Filter ("×", nur wenn aktiv und entfernbar). Reihenfolge und Aktivzustand
/// entscheidet der Aufrufer (`Components.Ledger.Types.orderChipsForDisplay`):
/// diese Datei kennt nur die Darstellung eines einzelnen Chips.
module Client.DesignSystem.ChipBar

open Feliz
open Client.DesignSystem.Tokens

type ChipProps = {
    Label: string
    IsActive: bool
    OnClick: unit -> unit
    /// `Some` macht den Chip entfernbar (das "×"); `None` für Chips ohne eigenen
    /// Filterwert, z. B. "Zu prüfen" (ein Ein/Aus-Schalter, kein eigener Filter).
    OnRemove: (unit -> unit) option
}

let private chipContainerClassName (isActive: bool) : string =
    let baseClasses =
        "inline-flex items-center gap-1.5 shrink-0 rounded-full whitespace-nowrap "
        + "min-h-[44px] md:min-h-0 px-1"
    if isActive then
        $"{baseClasses} bg-neon-teal/10 border border-neon-teal"
    else
        $"{baseClasses} {Backgrounds.dark} border {Borders.default'}"

let private chipLabelClassName (isActive: bool) : string =
    let baseClasses = "px-2.5 py-2 md:py-1.5 text-xs font-semibold text-left"
    if isActive then $"{baseClasses} {Colors.neonTeal}" else $"{baseClasses} {Colors.textSecondary}"

/// Ein einzelner Chip.
let chip (props: ChipProps) : ReactElement =
    Html.div [
        prop.className (chipContainerClassName props.IsActive)
        prop.children [
            Html.button [
                prop.type' "button"
                prop.className (chipLabelClassName props.IsActive)
                prop.onClick (fun _ -> props.OnClick ())
                prop.text props.Label
            ]
            match props.OnRemove with
            | Some onRemove when props.IsActive ->
                Html.button [
                    prop.type' "button"
                    prop.ariaLabel $"{props.Label} entfernen"
                    prop.className $"pr-2.5 pl-0.5 {Colors.neonTeal} text-xs font-bold"
                    prop.onClick (fun e ->
                        e.stopPropagation ()
                        onRemove ())
                    prop.text "×"
                ]
            | _ -> ()
        ]
    ]

/// "Alle Filter löschen": Textknopf am Ende der Leiste (PC: `margin-left:auto`,
/// Handy: letzter Chip in der scrollbaren Leiste, beides über dieselbe Klasse,
/// `md:ml-auto` wirkt nur ab `md`).
let clearAllButton (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className $"shrink-0 whitespace-nowrap md:ml-auto min-h-[44px] md:min-h-0 px-2 {Colors.neonTeal} text-xs font-semibold"
        prop.onClick (fun _ -> onClick ())
        prop.text "Alle Filter löschen"
    ]

/// Die Leiste selbst: sticky auf beiden Geräten (Styleguide 7.3, Aufgaben-
/// `## What`: "auf beiden Geräten"), am Handy waagerecht scrollbar, am PC
/// umbrechend. `ledger-filter-bar` (`styles.css`) hält sie unter der fixen
/// Navbar fest, wie `.table-sticky-thead`/`.ledger-account-strip` es für
/// Tabellenkopf und Konten-Spalte tun.
///
/// `elementRef` (dritte Iteration, ledger-j8xq4): optionaler Anker für den
/// Aufrufer (`Components.Ledger.View.FilterBarColumn`), der die tatsächlich
/// gerenderte Höhe der Leiste per `ResizeObserver` misst und als CSS-Variable
/// an den sticky Tabellenkopf weiterreicht, statt einer festen Schätzung: die
/// Leiste kann bei aktivem Filter und schmalerer Breite zweizeilig umbrechen
/// (`md:flex-wrap`), eine feste Höhe passte dann nicht mehr zur echten.
///
/// Bewusst ein **Callback-Ref** (`Element -> unit`), kein Objekt-Ref (vierte
/// Iteration, Verifier-Fund): der Aufrufer beobachtet damit den Knoten, den
/// React GERADE hier hinsetzt, bei jedem Wechsel neu, statt sich einmalig auf
/// den Knoten vom allerersten Mount zu verlassen. React ruft einen
/// Objekt-Ref beim Knotenwechsel zwar auch korrekt mit dem neuen Knoten auf,
/// aber ein bereits laufender Effekt bemerkt diese Änderung nicht von selbst;
/// ein Callback-Ref dagegen wird bei jedem Wechsel erneut aufgerufen.
let bar (elementRef: (Browser.Types.Element -> unit) option) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.className "ledger-filter-bar flex items-center gap-2 overflow-x-auto md:flex-wrap md:overflow-visible py-1 -mx-1 px-1"
        match elementRef with
        | Some r -> prop.ref r
        | None -> ()
        prop.children children
    ]

/// Ein Chip mit einem `position:relative`-Wrapper darum, für ein Popover, das
/// direkt darunter hängen soll (`FilterPopover.view`, Handoff: "Popover ... also
/// unter seinem Chip"). Am Handy hängt am selben Chip stattdessen ein
/// BottomSheet: der Aufrufer (`Components.Ledger.View.filterChrome`)
/// entscheidet zur Renderzeit über `Viewport.isDesktopViewport()`, welche der
/// beiden Chrome-Varianten tatsächlich gerendert wird, nie beide gleichzeitig
/// (ADR 0043). Ein `BottomSheet` portalt seinen Inhalt immer nach
/// `document.body`, unabhängig vom Aufruf-Ort: ein CSS-Doppel-Render wie bei
/// `entryListMobile`/`entryListDesktop` würde deshalb zwei echte Overlays für
/// denselben Zustand erzeugen, statt nur eines sichtbar zu machen.
let chipWithPopover (chipElement: ReactElement) (popover: ReactElement) : ReactElement =
    Html.div [
        prop.className "relative shrink-0"
        prop.children [ chipElement; popover ]
    ]
