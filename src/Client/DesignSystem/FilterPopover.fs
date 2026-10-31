/// Popover für Filter-Chips am PC (ledger-j8xq4, Handoff Screen 2.10/2.12/2.13:
/// "ein Popover-Typ für alle Filter"). Hängt unter seinem Chip (`position:relative`
/// am Chip-Wrapper, `position:absolute` hier), die Liste dahinter bleibt sichtbar.
/// `Esc` oder ein Klick außerhalb schließen ohne zu filtern: der Aufrufer entscheidet
/// selbst, ob das den Entwurf verwirft oder übernimmt (`OnClose` ist reines Schließen,
/// nie ein "Übernehmen").
///
/// Für den Handy-Fall (BottomSheet) ist dies NICHT zuständig: beide Chrome-Varianten
/// teilen sich denselben Inhalt, aber der Aufrufer (`Components.Ledger.View
/// .filterChrome`) entscheidet zur Renderzeit über `Viewport.isDesktopViewport()`,
/// welche der beiden tatsächlich gerendert wird, nie beide gleichzeitig (ADR 0043).
/// Ein CSS-Doppel-Render wie bei `entryListMobile`/`entryListDesktop`
/// (`Primitives.Responsive.mobileOnly`/`desktopOnly`) passt hier nicht: ein
/// `BottomSheet` portalt seinen Inhalt immer nach `document.body`, unabhängig vom
/// Aufruf-Ort, und würde bei einem Doppel-Render zwei echte Overlays für denselben
/// Zustand erzeugen.
module Client.DesignSystem.FilterPopover

open Fable.Core.JsInterop
open Feliz

type FilterPopoverProps = {
    IsOpen: bool
    OnClose: unit -> unit
    /// Breite in Tailwind-Arbitrary-Pixeln, z. B. "320px" (Kategorie/Zeitraum) oder
    /// "360px" (Payee, längere Namen). Handoff: 460px (Kategorie), 372px (Zeitraum),
    /// 392px (Payee), hier vereinfacht auf zwei gebräuchliche Breiten.
    WidthClassName: string
}

/// Breiten der Filter-Popover. Kategorie 460px (Handoff), Payee 340px, sonst 320px.
/// Seite und Styleguide-Demo benutzen dieselben Werte.
let categoryWidthClass = "w-[min(92vw,460px)]"
let payeeWidthClass = "w-[min(92vw,340px)]"
let defaultWidthClass = "w-[min(92vw,320px)]"
/// Breite eines kleinen Aktionsmenüs (`menuItem`), z. B. "Nach REWE filtern" an einer
/// Tabellenzelle (ledger-rfq5p).
let menuWidthClass = "w-[min(92vw,240px)]"
/// Breite eines Popovers im Seitenpanel (420 px, `overflow-hidden`): die Breite des Wrappers
/// abzüglich je 1 rem Rand, damit das Popover nicht am Panelrand abgeschnitten wird
/// (ledger-sfab0, Kategorie-Picker im Detail).
let panelWidthClass = "ml-4 w-[calc(100%-2rem)]"

/// Lage des Popovers zu seinem Wrapper: unter dem Chip, linksbündig (Standard) oder über
/// dem Auslöser, rechtsbündig (für eine feste Leiste am unteren Bildschirmrand, ledger-w29y6).
let private belowClass = "top-full left-0 mt-2"
// `mb-6` (24 px) hebt das Popover über die Leiste: der Auslöser sitzt 14 px unter ihrer Oberkante.
let private aboveEndClass = "bottom-full right-0 mb-6"
/// Unter dem Auslöser, rechtsbündig: für einen Auslöser am rechten Seitenrand.
let private belowEndClass = "top-full right-0 mt-2"

[<ReactComponent>]
let private FilterPopoverInternal (props: FilterPopoverProps) (positionClass: string) (reveal: bool) (children: ReactElement list) =
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None

    // In einem Scrollbereich rückt das Popover beim Öffnen ganz ins Bild.
    React.useEffect (
        (fun () ->
            if reveal && props.IsOpen then
                containerRef.current
                |> Option.iter (fun container ->
                    container?scrollIntoView (createObj [ "block" ==> "nearest" ]) |> ignore)),
        [| box props.IsOpen |]
    )

    // Klick außerhalb schließt (dasselbe Muster wie `Input.fs`s `SearchableSelect`).
    React.useEffect (
        (fun () ->
            if props.IsOpen then
                let handleClickOutside (e: Browser.Types.Event) =
                    match containerRef.current with
                    | Some container ->
                        let target = e.target :?> Browser.Types.HTMLElement
                        if not (container.contains target) then
                            props.OnClose()
                    | None -> ()

                Browser.Dom.document.addEventListener ("mousedown", handleClickOutside)
                { new System.IDisposable with
                    member _.Dispose() =
                        Browser.Dom.document.removeEventListener ("mousedown", handleClickOutside) }
            else
                { new System.IDisposable with
                    member _.Dispose() = () }),
        [| box props.IsOpen |]
    )

    if not props.IsOpen then
        Html.none
    else
        Html.div [
            prop.ref containerRef
            prop.className (
                "absolute z-30 "
                + positionClass
                + " "
                + props.WidthClassName
                + " rounded-2xl border border-border-default bg-surface-elevated shadow-2xl p-4"
            )
            // Esc schließt, ohne zu filtern (AC). `stopPropagation` auf Klicks im
            // Popover selbst: ein Klick INNERHALB darf nicht als "außerhalb" zählen
            // (waere hier ohnehin durch `containerRef.contains` abgedeckt, aber ein
            // Klick auf ein Element, das die Seite dahinter neu rendert, soll das
            // Popover nicht vorzeitig schliessen).
            prop.onClick (fun e -> e.stopPropagation ())
            // Esc im Popover bleibt im Popover: das Dokument hört Esc für das Detail der
            // Kassenbuch-Seite (ledger-q1hw2) und soll es nicht zugleich schließen.
            prop.onKeyDown (fun e ->
                if e.key = "Escape" then
                    e.stopPropagation ()
                    props.OnClose ())
            prop.children children
        ]

/// Ein Popover unter seinem Chip. `anchor` ist der Chip selbst, der Aufrufer muss
/// ihn in einen `position:relative`-Wrapper stellen (siehe `ChipBar.chipWithPopover`).
let view (props: FilterPopoverProps) (children: ReactElement list) : ReactElement =
    FilterPopoverInternal props belowClass false children

/// Wie `view`, aber rechtsbündig mit dem Auslöser: ein Popover am rechten Seitenrand ragte
/// sonst aus dem Bild (categorization-cgq35).
let viewEnd (props: FilterPopoverProps) (children: ReactElement list) : ReactElement =
    FilterPopoverInternal props belowEndClass false children

/// Wie `view`, aber für einen Auslöser in einem Scrollbereich, etwa dem Körper eines `Modal`:
/// das Popover scrollt sich beim Öffnen ins Bild, statt am Rand abgeschnitten zu werden
/// (categorization-cgq35).
let viewRevealed (props: FilterPopoverProps) (children: ReactElement list) : ReactElement =
    FilterPopoverInternal props belowClass true children

/// Wie `view`, aber über dem Auslöser und rechtsbündig mit ihm (`bottom-full right-0`). Für
/// einen Auslöser in der festen Leiste unten am Bildschirm, wo ein Popover nach unten aus dem
/// Bildschirm ragte (ledger-w29y6). Der Aufrufer stellt den Auslöser in einen
/// `position:relative`-Wrapper.
let viewAbove (props: FilterPopoverProps) (children: ReactElement list) : ReactElement =
    FilterPopoverInternal props aboveEndClass false children

let private menuItemWith (autoFocus: bool) (label: string) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type'.button
        prop.role "menuitem"
        prop.title label
        if autoFocus then prop.autoFocus true
        prop.className (
            "block w-full text-left truncate rounded-lg px-3 py-3.5 md:py-2 text-text-primary "
            + Client.DesignSystem.Tokens.ListRoles.primarySize
            + " hover:bg-surface-hover focus:bg-surface-hover"
        )
        prop.text label
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick ())
    ]

/// Eine volle Zeile eines kleinen Aktionsmenüs im Popover (`role="menuitem"`),
/// Beschriftung auf eine Zeile gekürzt, `title` trägt den vollen Text. Auch im
/// Handy-Sheet verwendbar (ledger-rfq5p).
let menuItem (label: string) (onClick: unit -> unit) : ReactElement =
    menuItemWith false label onClick

/// Wie `menuItem`, aber mit Fokus beim Öffnen. `FilterPopover` schließt auf Esc nur,
/// wenn der Fokus im Popover liegt: der erste Eintrag eines Menüs nimmt ihn.
let menuItemAutoFocus (label: string) (onClick: unit -> unit) : ReactElement =
    menuItemWith true label onClick
