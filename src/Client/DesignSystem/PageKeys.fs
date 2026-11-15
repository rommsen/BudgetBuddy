/// Die Verdrahtung der Tastenregel am Dokument (design-system-gcpws, §7.8): ein Handler am Dokument, weil nach einem
/// Klick auf eine Zeile der Fokus auf `body` steht und kein Element der Seite erreicht. Die Regel selbst ist rein
/// (`ListKeys.actionFor`); hier steht nur, was der Browser liefert.
module Client.DesignSystem.PageKeys

open Feliz
open Fable.Core.JsInterop

/// Wo der Fokus eines Tastenereignisses steht. `searchSelector`: die Hülle des Suchfelds der Liste, falls sie eins
/// hat. Kästchen, Radio und Knöpfe als `input` zählen nicht als Feld.
let focusOf (searchSelector: string option) (target: obj) : ListKeys.Focus =
    if isNull target then
        ListKeys.Elsewhere
    else
        let tag: string = target?tagName
        let kind: string = if tag = "INPUT" then target?``type`` else ""
        let editable: bool = target?isContentEditable = true
        let field =
            tag = "TEXTAREA" || tag = "SELECT" || editable
            || (tag = "INPUT" && kind <> "checkbox" && kind <> "radio" && kind <> "button")
        let inSearch =
            match searchSelector with
            | Some selector -> field && not (isNull (target?closest selector))
            | None -> false
        if inSearch then ListKeys.InSearch
        elif field then ListKeys.InTextField
        else ListKeys.Elsewhere

/// Hängt `onKey` an das Dokument, solange die Komponente steht. `onKey` bekommt immer den neuesten Wert, ohne dass
/// der Handler neu gebunden wird.
[<ReactComponent>]
let OnDocument (onKey: Browser.Types.KeyboardEvent -> unit) =
    let latest = React.useRef onKey
    latest.current <- onKey
    React.useEffect (
        (fun () ->
            let handler (event: Browser.Types.Event) = latest.current (event :?> Browser.Types.KeyboardEvent)
            Browser.Dom.document.addEventListener ("keydown", handler)
            React.createDisposable (fun () -> Browser.Dom.document.removeEventListener ("keydown", handler))),
        [||]
    )
    Html.none
