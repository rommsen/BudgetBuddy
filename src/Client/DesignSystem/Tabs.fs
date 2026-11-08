/// Gleich breite Tabs (budgeting-g8s3w, erster Nutzer: der Inspektor der Budget-Seite). Der aktive
/// Tab ist Akzent (ADR 0087: Ausgewähltes und Navigation). Am Handy 48 px Trefferfläche. Tastatur und
/// Rollen nach dem Tabs-Muster der WAI-ARIA (budgeting-p4n6r).
module Client.DesignSystem.Tabs

open Feliz
open Fable.Core.JsInterop
open Client.DesignSystem.Tokens

type Tab = {
    Label: string
    IsActive: bool
    OnClick: unit -> unit
}

let private tabClassName (isActive: bool) : string =
    let baseClasses =
        $"inline-flex items-center justify-center {TouchTargets.minHeight} px-2 md:py-2 text-xs font-semibold border-b-2 cursor-pointer touch-manipulation transition-colors"
    if isActive then $"{baseClasses} {Colors.accent} {Borders.accent}"
    else $"{baseClasses} {Colors.textSecondary} border-transparent hover:text-text-primary"

let private tabId (idPrefix: string) (index: int) = $"{idPrefix}-tab-{index}"
let private panelId (idPrefix: string) = $"{idPrefix}-panel"

/// Pfeiltasten wechseln den Tab und nehmen den Fokus mit, am Rand bleiben sie stehen. Home und End
/// springen an die Ränder.
/// Mit Zusatztaste gehört der Pfeil dem Browser (Alt und Links: zurück).
let private onTabKey (tabs: Tab list) (index: int) (e: Browser.Types.KeyboardEvent) : unit =
    let last = tabs.Length - 1
    let target =
        match e.key with
        | _ when e.altKey || e.ctrlKey || e.metaKey || e.shiftKey -> None
        | "ArrowRight" -> Some (min last (index + 1))
        | "ArrowLeft" -> Some (max 0 (index - 1))
        | "Home" -> Some 0
        | "End" -> Some last
        | _ -> None
    match target with
    | Some next ->
        e.preventDefault ()
        tabs.[next].OnClick ()
        let button: obj = e.currentTarget?parentElement?children?(next)
        button?focus ()
    | None -> ()

/// Roving `tabindex`: nur der aktive Tab liegt in der Tab-Folge.
let private tabButton (idPrefix: string) (tabs: Tab list) (index: int) (tab: Tab) : ReactElement =
    Html.button [
        prop.key tab.Label
        prop.id (tabId idPrefix index)
        prop.type' "button"
        prop.role "tab"
        prop.custom ("aria-selected", tab.IsActive)
        prop.custom ("aria-controls", panelId idPrefix)
        prop.tabIndex (if tab.IsActive then 0 else -1)
        prop.className (tabClassName tab.IsActive)
        prop.onClick (fun _ -> tab.OnClick ())
        prop.onKeyDown (onTabKey tabs index)
        prop.text tab.Label
    ]

/// Die Leiste. `ariaLabel` benennt sie für den Screenreader, `testId` ist zugleich der Präfix der Ids,
/// die `panel` braucht.
let bar (testId: string) (ariaLabel: string) (tabs: Tab list) : ReactElement =
    Html.div [
        prop.role "tablist"
        prop.ariaLabel ariaLabel
        prop.custom ("data-testid", testId)
        prop.className $"grid grid-flow-col auto-cols-fr border-b {Borders.default'}"
        prop.children (tabs |> List.mapi (tabButton testId tabs))
    ]

/// Der Inhalt des aktiven Tabs. Es steht immer nur ein Panel da, alle Tabs zeigen darauf.
let panel (testId: string) (activeIndex: int) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.id (panelId testId)
        prop.role "tabpanel"
        prop.custom ("aria-labelledby", tabId testId activeIndex)
        prop.tabIndex 0
        prop.children children
    ]
