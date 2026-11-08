module Client.DesignSystem.PageHeader

open Feliz

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
