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
    | Gradient

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

/// Full page header with configurable props
let view (props: Props) =
    let titleClass =
        match props.TitleStyle with
        | Standard ->
            "text-2xl md:text-4xl font-bold font-display text-base-content"
        | Gradient ->
            "text-2xl md:text-4xl font-bold font-display bg-gradient-to-r from-neon-teal to-neon-green bg-clip-text text-transparent"

    Html.div [
        prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 animate-fade-in"
        prop.children [
            Html.div [
                prop.children [
                    Html.h1 [
                        prop.className titleClass
                        prop.text props.Title
                    ]
                    match props.Subtitle with
                    | Some subtitle ->
                        Html.p [
                            prop.className "text-base-content/60 mt-1 text-sm md:text-base"
                            prop.text subtitle
                        ]
                    | None -> Html.none
                ]
            ]
            if not props.Actions.IsEmpty then
                Html.div [
                    prop.className "flex gap-2"
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

/// Header with gradient title
let gradient (title: string) =
    view { defaultProps with Title = title; TitleStyle = Gradient }

/// Header with gradient title and subtitle
let gradientWithSubtitle (title: string) (subtitle: string) =
    view { defaultProps with Title = title; Subtitle = Some subtitle; TitleStyle = Gradient }

/// Full header with actions
let withActions (title: string) (subtitle: string option) (actions: ReactElement list) =
    view { defaultProps with Title = title; Subtitle = subtitle; Actions = actions }

/// Full header with gradient title and actions
let gradientWithActions (title: string) (subtitle: string option) (actions: ReactElement list) =
    view { defaultProps with Title = title; Subtitle = subtitle; Actions = actions; TitleStyle = Gradient }
