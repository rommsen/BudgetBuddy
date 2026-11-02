/// Gleich breite Tabs (budgeting-g8s3w, erster Nutzer: der Inspektor der Budget-Seite). Der aktive
/// Tab ist teal (ADR 0075: Ausgewähltes und Navigation). Am Handy 48 px Trefferfläche.
module Client.DesignSystem.Tabs

open Feliz
open Client.DesignSystem.Tokens

type Tab = {
    Label: string
    IsActive: bool
    OnClick: unit -> unit
}

let private tabClassName (isActive: bool) : string =
    let baseClasses =
        $"inline-flex items-center justify-center {TouchTargets.minHeight} px-2 md:py-2 text-xs font-semibold border-b-2 cursor-pointer touch-manipulation transition-colors"
    if isActive then $"{baseClasses} {Colors.neonTeal} {Borders.teal}"
    else $"{baseClasses} {Colors.textSecondary} border-transparent hover:text-text-primary"

let private tabButton (tab: Tab) : ReactElement =
    Html.button [
        prop.key tab.Label
        prop.type' "button"
        prop.role "tab"
        prop.custom ("aria-selected", tab.IsActive)
        prop.className (tabClassName tab.IsActive)
        prop.onClick (fun _ -> tab.OnClick ())
        prop.text tab.Label
    ]

/// Die Leiste. `ariaLabel` benennt sie für den Screenreader.
let bar (testId: string) (ariaLabel: string) (tabs: Tab list) : ReactElement =
    Html.div [
        prop.role "tablist"
        prop.ariaLabel ariaLabel
        prop.custom ("data-testid", testId)
        prop.className $"grid grid-flow-col auto-cols-fr border-b {Borders.default'}"
        prop.children (tabs |> List.map tabButton)
    ]
