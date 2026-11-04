/// Umschalter zwischen Ansichten derselben Sache (budgeting-c5w2n, erster Nutzer: der Tab Geld).
/// Knöpfe mit `aria-pressed`, aktiv teal (ADR 0075), 48 px. Anders als `Tabs` nimmt er keine
/// Pfeiltasten: zwei Tab-Leisten übereinander stören sich.
module Client.DesignSystem.Segmented

open Feliz
open Client.DesignSystem.Tokens

type Segment = {
    Label: string
    IsActive: bool
    /// Gesperrt (budgeting-g2ty9, X3): `disabled`, gedämpft wie ein gesperrtes Feld, kein Hover. Erster
    /// Nutzer: der Rhythmus im Tab Target, solange das Speichern läuft.
    Disabled: bool
    OnClick: unit -> unit
}

let segmentClass (isActive: bool) (disabled: bool) : string =
    let baseClasses =
        $"inline-flex items-center justify-center {TouchTargets.alwaysMinHeight} px-2 rounded-md text-sm font-semibold touch-manipulation transition-colors"
    let color =
        if isActive then $"{Backgrounds.tealSubtle} {Colors.neonTeal}"
        elif disabled then Colors.textSecondary
        else $"{Colors.textSecondary} hover:text-text-primary"
    let cursor = if disabled then "opacity-50 cursor-not-allowed" else "cursor-pointer"
    $"{baseClasses} {color} {cursor}"

let private segmentButton (testId: string) (segment: Segment) : ReactElement =
    Html.button [
        prop.key segment.Label
        prop.type' "button"
        prop.custom ("data-testid", testId)
        prop.custom ("aria-pressed", segment.IsActive)
        prop.disabled segment.Disabled
        prop.className (segmentClass segment.IsActive segment.Disabled)
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
