module App

open Elmish
open Elmish.React
open Elmish.HMR
open Fable.Core.JsInterop
open Browser

// Import Tailwind CSS
Fable.Core.JsInterop.importSideEffects "./styles.css"

// Mirror the visual viewport into CSS vars (--vvh/--vv-top) so bottom sheets
// can stay above the mobile on-screen keyboard.
Client.DesignSystem.Viewport.start ()

// PWA kommt in den Vordergrund (ADR 0014, Frische-Kriterium, ledger-h4p6c): eine
// Elmish-Subscription statt eines direkten `dispatch`-Aufrufs aus App.fs, damit der
// Auslöser durch denselben `update` läuft wie jede andere Nachricht. `visibilityState`
// kommt dynamisch (`document?visibilityState`), wie das Projekt es schon für
// `visualViewport` in `Viewport.fs` macht (kein typisiertes Binding dafür in
// Fable.Browser.Dom).
let private visibilitySubscription (_model: State.Model) : Elmish.Sub<State.Msg> =
    [
        [ "ledger-visibilitychange" ],
        fun dispatch ->
            let handler =
                fun (_: Browser.Types.Event) ->
                    let state: string = document?visibilityState
                    if state = "visible" then dispatch State.VisibilityChanged
            document.addEventListener ("visibilitychange", handler)
            { new System.IDisposable with
                member _.Dispose() = document.removeEventListener ("visibilitychange", handler) }
    ]

// Start the Elmish application
Program.mkProgram State.init State.update View.view
|> Program.withSubscription visibilitySubscription
|> Program.withReactSynchronous "root"
|> Program.run
