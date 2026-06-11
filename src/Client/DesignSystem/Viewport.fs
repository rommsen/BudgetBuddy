module Client.DesignSystem.Viewport

// Keyboard-aware viewport handling for mobile browsers.
//
// iOS Safari only shrinks the *visual* viewport when the on-screen keyboard
// opens — the layout viewport, dvh units and position:fixed elements are
// unaffected, so a bottom sheet anchored with `bottom: 0` ends up hidden
// behind the keyboard. The only reliable signal is the visualViewport API.
//
// This module mirrors visualViewport.height/offsetTop into the CSS custom
// properties `--vvh` / `--vv-top` so that fixed elements can anchor to the
// *visible* bottom edge via `top: calc(var(--vv-top) + var(--vvh))` +
// `translateY(-100%)`.

open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types

/// Minimal binding for the visualViewport API (not part of Fable.Browser.Dom).
type private VisualViewport =
    abstract height: float
    abstract offsetTop: float
    abstract addEventListener: string * (Event -> unit) -> unit

let private tryVisualViewport () : VisualViewport option =
    let vv: VisualViewport = window?visualViewport
    if isNull (box vv) then None else Some vv

let private setViewportVars (height: float) (offsetTop: float) =
    let style: obj = document.documentElement?style
    style?setProperty ("--vvh", sprintf "%.2fpx" height)
    style?setProperty ("--vv-top", sprintf "%.2fpx" offsetTop)

let private sync () =
    match tryVisualViewport () with
    | Some vv -> setViewportVars vv.height vv.offsetTop
    | None -> ()

let mutable private started = false

/// Starts mirroring the visual viewport into CSS vars. Idempotent; call once at startup.
let start () =
    if not started then
        started <- true
        sync ()

        match tryVisualViewport () with
        | Some vv ->
            vv.addEventListener ("resize", fun _ -> sync ())
            vv.addEventListener ("scroll", fun _ -> sync ())
        | None -> ()

        // iOS sometimes reports stale visualViewport values right after the
        // keyboard hides — re-sync shortly after any focus leaves an input.
        document.addEventListener ("focusout", fun _ -> window.setTimeout ((fun () -> sync ()), 150) |> ignore)

// ---------------------------------------------------------------------------
// Body scroll lock (counted, so nested sheets don't fight over the styles)
// ---------------------------------------------------------------------------

let mutable private lockCount = 0
let mutable private savedScrollY = 0.0

/// Locks page scrolling while an overlay/sheet is open. position:fixed on body
/// is the only technique that holds on iOS; the scroll offset is saved and
/// restored on unlock so the page doesn't jump.
let lockBodyScroll () =
    if lockCount = 0 then
        savedScrollY <- window.scrollY
        let style: obj = document.body?style
        style?position <- "fixed"
        style?top <- sprintf "-%.0fpx" savedScrollY
        style?left <- "0"
        style?right <- "0"
        style?width <- "100%"
        style?overflow <- "hidden"

    lockCount <- lockCount + 1

let unlockBodyScroll () =
    lockCount <- max 0 (lockCount - 1)

    if lockCount = 0 then
        let style: obj = document.body?style
        style?position <- ""
        style?top <- ""
        style?left <- ""
        style?right <- ""
        style?width <- ""
        style?overflow <- ""
        window.scrollTo (0.0, savedScrollY)

// ---------------------------------------------------------------------------
// Ghost-click guard
// ---------------------------------------------------------------------------

/// Swallows the next click arriving within `ms` milliseconds. After a touch
/// sequence, browsers dispatch a synthetic click by hit-testing the *current*
/// coordinates — if an overlay just closed, that click lands on whatever is
/// behind it. Install this guard when closing an overlay from a touch handler.
let swallowNextClick (ms: int) =
    // ref cell: F# closures cannot capture mutable locals
    let handler: (Event -> unit) ref = ref ignore

    let remove () =
        document.removeEventListener ("click", handler.Value, true)

    handler.Value <-
        fun (e: Event) ->
            e.stopPropagation ()
            e.preventDefault ()
            remove ()

    document.addEventListener ("click", handler.Value, true)
    window.setTimeout (remove, ms) |> ignore

// ---------------------------------------------------------------------------
// Small device helpers
// ---------------------------------------------------------------------------

/// True when the primary input is a precise pointer (mouse/trackpad).
/// Used to decide whether autofocusing a search field is helpful (desktop)
/// or harmful (mobile, where it immediately summons the keyboard).
let isFinePointer () =
    let mq: obj = window?matchMedia ("(pointer: fine)")
    unbox<bool> (mq?matches)

/// Light haptic feedback where supported (Android Chrome). Silently ignored
/// elsewhere — iOS Safari does not implement navigator.vibrate.
let vibrate (ms: int) =
    try
        window?navigator?vibrate (ms) |> ignore
    with _ ->
        ()
