module App

open Elmish
open Elmish.React
open Elmish.HMR

// Import Tailwind CSS
Fable.Core.JsInterop.importSideEffects "./styles.css"

// Mirror the visual viewport into CSS vars (--vvh/--vv-top) so bottom sheets
// can stay above the mobile on-screen keyboard.
Client.DesignSystem.Viewport.start ()

// Start the Elmish application
Program.mkProgram State.init State.update View.view
|> Program.withReactSynchronous "root"
|> Program.run
