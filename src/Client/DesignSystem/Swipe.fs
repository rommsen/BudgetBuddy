module Client.DesignSystem.Swipe

// Swipe-left gesture for list rows (touch only).
//
// Pointer events + `touch-action: pan-y` (set on .tx-row in CSS): the browser
// keeps vertical scrolling native, horizontal movement is ours. The gesture is
// claimed only after 12px of predominantly-horizontal travel; committing
// requires 35% of the row width or a fast flick. Buttons/toggles remain the
// accessible path — the swipe is an accelerator, never the only way.

open Feliz
open Fable.Core.JsInterop
open Browser.Types

type SwipeableRowProps = {
    /// Label revealed behind the row while swiping (e.g. "Überspringen")
    ActionLabel: string
    /// Visual variant of the action background: "skip" | "include"
    ActionClass: string
    /// Called once when the swipe passes the commit threshold
    OnCommit: unit -> unit
    Children: ReactElement
}

[<Literal>]
let private ClaimSlopPx = 12.0

[<Literal>]
let private CommitFraction = 0.35

[<Literal>]
let private CommitVelocity = 0.5 // px/ms

[<ReactComponent>]
let SwipeableRow (props: SwipeableRowProps) =
    let contentRef = React.useRef<HTMLElement option> None
    let actionRef = React.useRef<HTMLElement option> None
    let startX = React.useRef 0.0
    let startY = React.useRef 0.0
    let startTime = React.useRef 0.0
    let active = React.useRef false
    let claimed = React.useRef false
    let suppressClick = React.useRef false

    let setContentTransform (animated: bool) (dx: float) =
        match contentRef.current with
        | Some el ->
            let style: obj = el?style
            style?transition <-
                if animated then "transform 300ms var(--sf-spring-out, cubic-bezier(0.2, 0, 0, 1))"
                else "none"
            style?transform <- sprintf "translateX(%.1fpx)" dx
        | None -> ()

    let setActionOpacity (opacity: float) =
        match actionRef.current with
        | Some el -> el?style?opacity <- sprintf "%.2f" opacity
        | None -> ()

    let reset () =
        active.current <- false
        claimed.current <- false
        setContentTransform true 0.0
        setActionOpacity 0.0

    // Left-only with rubberband resistance once past half the row width
    let rubberband (dx: float) (width: float) =
        if dx >= 0.0 then 0.0
        else
            let limit = width * 0.5
            if -dx <= limit then dx
            else -(limit + (-dx - limit) * 0.25)

    let rowWidth () =
        contentRef.current
        |> Option.map (fun el -> unbox<float> el?offsetWidth)
        |> Option.defaultValue 1.0

    Html.div [
        prop.className "tx-swipe-wrap"
        // Capture-phase: a completed swipe must not fire the row's
        // expand-on-click underneath the finger.
        prop.custom (
            "onClickCapture",
            fun (e: MouseEvent) ->
                if suppressClick.current then
                    suppressClick.current <- false
                    e.stopPropagation ()
                    e.preventDefault ()
        )
        prop.onPointerDown (fun e ->
            if e.pointerType = "touch" then
                startX.current <- e.clientX
                startY.current <- e.clientY
                startTime.current <- e.timeStamp
                active.current <- true
                claimed.current <- false
                suppressClick.current <- false)
        prop.onPointerMove (fun e ->
            if active.current then
                let dx = e.clientX - startX.current
                let dy = e.clientY - startY.current

                if not claimed.current then
                    if abs dy > abs dx then
                        // vertical intent → let native scrolling win
                        active.current <- false
                    elif abs dx >= ClaimSlopPx then
                        claimed.current <- true
                        suppressClick.current <- true
                        e.currentTarget?setPointerCapture (e.pointerId) |> ignore

                if claimed.current then
                    let width = rowWidth ()
                    let offset = rubberband dx width
                    setContentTransform false offset
                    setActionOpacity (min 1.0 (-offset / 72.0)))
        prop.onPointerUp (fun e ->
            if claimed.current then
                let dx = e.clientX - startX.current
                let dt = max 1.0 (e.timeStamp - startTime.current)
                let velocity = abs dx / dt
                let width = rowWidth ()
                let commit = dx < 0.0 && (abs dx > width * CommitFraction || velocity > CommitVelocity)

                if commit then
                    Viewport.vibrate 10
                    props.OnCommit()

                reset ()
            else
                active.current <- false)
        prop.onPointerCancel (fun _ ->
            if claimed.current then reset ()
            else active.current <- false)
        prop.children [
            Html.div [
                prop.className ("tx-swipe-action " + props.ActionClass)
                prop.ref actionRef
                prop.ariaHidden true
                prop.text props.ActionLabel
            ]
            Html.div [
                prop.className "tx-swipe-content"
                prop.ref contentRef
                prop.children [ props.Children ]
            ]
        ]
    ]
