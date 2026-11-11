module Client.DesignSystem.PageHeader

open Feliz
open Client.DesignSystem.Tokens

// ============================================
// PageHeader Design System Component
// ============================================
//
// A responsive page header with title, optional subtitle,
// and action slots. Stacks on mobile, row on desktop.

/// Title style options
type TitleStyle =
    | Standard
    /// Schlank für Listen-Seiten (ADR 0049): 20px, 600, ohne Verlauf
    | Calm

/// Header configuration
type Props = {
    Title: string
    Subtitle: string option
    Actions: ReactElement list
    TitleStyle: TitleStyle
}

let defaultProps = {
    Title = ""
    Subtitle = None
    Actions = []
    TitleStyle = Standard
}

/// Titelklassen je Stil
let titleClass (style: TitleStyle) : string =
    match style with
    | Standard ->
        "text-2xl md:text-4xl font-bold font-display text-text-primary"
    | Calm ->
        "text-xl font-semibold font-display text-text-primary"

/// Untertitelklassen je Stil
let subtitleClass (style: TitleStyle) : string =
    match style with
    | Calm -> "text-text-muted mt-0.5 text-xs"
    | _ -> "text-text-muted mt-1 text-sm md:text-base"

/// Containerklassen je Stil: Calm hält Titel und Aktionen immer in einer Zeile
let containerClass (style: TitleStyle) : string =
    match style with
    | Calm -> "flex flex-row justify-between items-center gap-3 mb-4 animate-fade-in"
    | _ -> "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 animate-fade-in"

/// Der Titel nur für den Screenreader: die Seite braucht eine `h1`, die Navigation nennt sie schon.
let screenReaderTitle (title: string) =
    Html.h1 [ prop.className "sr-only"; prop.text title ]

/// Full page header with configurable props
let view (props: Props) =
    let titleClass = titleClass props.TitleStyle

    Html.div [
        prop.className (containerClass props.TitleStyle)
        prop.children [
            // Ein langes Wort im Titel bricht um, statt die Aktionen aus dem Bild zu schieben (infrastructure-nx14s).
            Html.div [
                prop.className "min-w-0"
                prop.children [
                    Html.h1 [
                        prop.className $"{titleClass} hyphens-auto break-words"
                        prop.text props.Title
                    ]
                    match props.Subtitle with
                    | Some subtitle ->
                        Html.p [
                            prop.className (subtitleClass props.TitleStyle)
                            prop.text subtitle
                        ]
                    | None -> Html.none
                ]
            ]
            if not props.Actions.IsEmpty then
                Html.div [
                    prop.className "flex shrink-0 gap-2"
                    prop.children props.Actions
                ]
        ]
    ]

/// Simple header with just title
let simple (title: string) =
    view { defaultProps with Title = title }

/// Header with title and subtitle
let withSubtitle (title: string) (subtitle: string) =
    view { defaultProps with Title = title; Subtitle = Some subtitle }

/// Full header with actions
let withActions (title: string) (subtitle: string option) (actions: ReactElement list) =
    view { defaultProps with Title = title; Subtitle = subtitle; Actions = actions }

/// Schlanker Kopf für Listen-Seiten: Titel, gedämpfter Untertitel, Aktionen rechts
let calm (title: string) (subtitle: string option) (actions: ReactElement list) =
    view { defaultProps with Title = title; Subtitle = subtitle; Actions = actions; TitleStyle = Calm }

// ============================================
// Kopf eines Orts und einer Unterseite (Prototyp Version 17 `top`, `.top`, `.back`, design-system-k6kfw)
// ============================================

/// „Zurück“ mit Pfeil nach links im Kopf einer Unterseite (Prototyp `.back`): Text 14 px in `text-secondary`,
/// am Handy 48 px hoch (Styleguide §7.15), am PC 44 px. `ariaLabel` nennt das Ziel, etwa „Zurück zum Budget“.
let backButtonFor (ariaLabel: string option) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", "page-back")
        match ariaLabel with
        | Some label -> prop.ariaLabel label
        | None -> ()
        prop.className (
            $"-ml-1 inline-flex shrink-0 items-center gap-1 pr-2 text-sm {Colors.textSecondary} "
            + $"{TouchTargets.mobileMinHeight} md:min-h-11 hover:text-text-primary"
        )
        prop.onClick (fun _ -> onClick ())
        prop.children [
            Client.DesignSystem.Icons.arrowLeft Client.DesignSystem.Icons.SM Client.DesignSystem.Icons.Default
            Html.span [ prop.text "Zurück" ]
        ]
    ]

let backButton (onClick: unit -> unit) = backButtonFor None onClick

/// Symbolknopf im Kopf (Prototyp `.icon`): Symbol 22 px, am Handy 48 × 48 px (Styleguide §7.15), am PC 44 × 44 px.
let iconButton (ariaLabel: string) (icon: Client.DesignSystem.Icons.IconSize -> Client.DesignSystem.Icons.IconColor -> ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.ariaLabel ariaLabel
        prop.className (
            $"{TouchTargets.mobileSquare} md:h-11 md:w-11 shrink-0 grid place-items-center rounded-md "
            + $"{Colors.textSecondary} hover:bg-surface-hover hover:text-text-primary"
        )
        prop.onClick (fun _ -> onClick ())
        prop.children [ icon Client.DesignSystem.Icons.Tab Client.DesignSystem.Icons.Default ]
    ]

/// Der Kopf eines Orts oder einer Unterseite (Prototyp `.top`): optional „Zurück“, Titel 20 px in der
/// Display-Schrift, darunter optional ein Untertitel 12 px gedämpft, rechts die Aktionen.
let top (back: ReactElement option) (title: string) (subtitle: string option) (actions: ReactElement list) =
    Html.div [
        prop.custom ("data-testid", "page-top")
        prop.className "flex items-center gap-2 mb-3 min-w-0"
        prop.children [
            match back with
            | Some b -> b
            | None -> Html.none
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    Html.h1 [
                        prop.className "text-xl font-semibold font-display text-text-primary truncate"
                        prop.title title
                        prop.text title
                    ]
                    match subtitle with
                    | Some s -> Html.p [ prop.className "text-xs text-text-muted"; prop.text s ]
                    | None -> Html.none
                ]
            ]
            if not actions.IsEmpty then
                Html.div [ prop.className "flex shrink-0 items-center gap-1"; prop.children actions ]
        ]
    ]
