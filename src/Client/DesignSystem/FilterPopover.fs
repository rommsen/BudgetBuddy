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

[<ReactComponent>]
let private FilterPopoverInternal (props: FilterPopoverProps) (children: ReactElement list) =
    let containerRef = React.useRef<Browser.Types.HTMLElement option> None

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
                "absolute z-30 top-full left-0 mt-2 "
                + props.WidthClassName
                + " rounded-2xl border border-border-default bg-surface-elevated shadow-2xl p-4"
            )
            // Esc schließt, ohne zu filtern (AC). `stopPropagation` auf Klicks im
            // Popover selbst: ein Klick INNERHALB darf nicht als "außerhalb" zählen
            // (waere hier ohnehin durch `containerRef.contains` abgedeckt, aber ein
            // Klick auf ein Element, das die Seite dahinter neu rendert, soll das
            // Popover nicht vorzeitig schliessen).
            prop.onClick (fun e -> e.stopPropagation ())
            prop.onKeyDown (fun e -> if e.key = "Escape" then props.OnClose ())
            prop.children children
        ]

/// Ein Popover unter seinem Chip. `anchor` ist der Chip selbst, der Aufrufer muss
/// ihn in einen `position:relative`-Wrapper stellen (siehe `ChipBar.chipWithPopover`).
let view (props: FilterPopoverProps) (children: ReactElement list) : ReactElement =
    FilterPopoverInternal props children
