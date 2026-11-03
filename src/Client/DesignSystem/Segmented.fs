/// Umschalter zwischen Ansichten derselben Sache (budgeting-c5w2n, erster Nutzer: der Tab Geld).
/// Knöpfe mit `aria-pressed`, aktiv teal (ADR 0075), 48 px. Anders als `Tabs` nimmt er keine
/// Pfeiltasten: zwei Tab-Leisten übereinander stören sich.
module Client.DesignSystem.Segmented

open Feliz
open Client.DesignSystem.Tokens

type Segment = {
    Label: string
    IsActive: bool
    OnClick: unit -> unit
}

let private segmentClass (isActive: bool) : string =
    let baseClasses =
        $"inline-flex items-center justify-center {TouchTargets.alwaysMinHeight} px-2 rounded-md text-sm font-semibold cursor-pointer touch-manipulation transition-colors"
    if isActive then $"{baseClasses} {Backgrounds.tealSubtle} {Colors.neonTeal}"
    else $"{baseClasses} {Colors.textSecondary} hover:text-text-primary"

let private segmentButton (testId: string) (segment: Segment) : ReactElement =
    Html.button [
        prop.key segment.Label
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.custom ("aria-pressed", segment.IsActive)
        prop.className (segmentClass segment.IsActive)
        prop.onClick (fun _ -> segment.OnClick ())
        prop.text segment.Label
    ]

/// Die gleich breite Leiste. `testId` steht auf jedem Knopf, `ariaLabel` benennt die Gruppe.
let view (testId: string) (ariaLabel: string) (segments: Segment list) : ReactElement =
    Html.div [
        prop.role "group"
        prop.ariaLabel ariaLabel
        prop.className $"grid grid-flow-col auto-cols-fr gap-1 rounded-lg border {Borders.default'} p-0.5"
        prop.children (segments |> List.map (segmentButton testId))
    ]
