module Client.DesignSystem.Stats

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Stat Types
// ============================================

/// Trend direction for stat changes
type Trend =
    | Up of percentChange: decimal
    | Down of percentChange: decimal
    | Neutral

/// Accent color for the gradient line at top of stat card
type StatAccent =
    | Accent
    | Success
    | Error
    | Gradient  // Default: Akzent -> Grün
    /// No accent line at all (ledger-w9skm): unlike the coloured variants above,
    /// this suppresses the decorative bar entirely instead of just recolouring
    /// it — needed for the mobile account cards, which must show no colour
    /// stripe per the Kassenbuch mockup.
    | NoAccent

/// Size variants for stat cards
type StatSize =
    | Compact   // Tighter padding, smaller text
    | Normal    // Standard stat card
    | Large     // Hero-style large stat

// ============================================
// Stat Props
// ============================================

type StatProps = {
    Label: string
    Value: string
    /// Overrides `Value` with a pre-rendered element (e.g. `Money.accountBalance`)
    /// when the value needs to carry its own colour/font semantics instead of plain
    /// text (ledger-x0yga: account balances lose their green/red colouring and
    /// digit font if flattened to a string first).
    ValueElement: ReactElement option
    Icon: ReactElement option
    Trend: Trend option
    Accent: StatAccent
    Size: StatSize
    Description: string option
    ClassName: string option
    OnClick: (unit -> unit) option
    IsActive: bool
}

let defaultProps = {
    Label = ""
    Value = ""
    ValueElement = None
    Icon = None
    Trend = None
    Accent = Gradient
    Size = Normal
    Description = None
    ClassName = None
    OnClick = None
    IsActive = false
}

// ============================================
// Stat Implementation
// ============================================

/// Baut den vollständigen Klassen-String des Streifen-`div`s aus dem Gradient-Fragment.
let private bar (gradient: string) : string option =
    Some $"absolute top-0 left-0 right-0 h-[3px] bg-gradient-to-r {gradient}"

/// Die Akzentstreifen-Entscheidung als reine Funktion (ADR 0035, design-system-j9xnd):
/// `NoAccent` unterdrückt den dekorativen Streifen vollständig (`None`), jeder andere
/// Fall liefert den vollständigen Klassen-String des Streifen-`div`s (`Some`). `view`
/// trifft diese Entscheidung nicht mehr selbst, sondern konsumiert nur noch das Ergebnis
/// dieser einen Stelle. Die frühere separate `accentToGradient`-Funktion ist hier
/// hineingefaltet: mit nur diesem einen Aufrufer wäre ihr eigener Pattern-Match sonst nur
/// über einen Wildcard-Fall vollständig zu halten gewesen, der einen künftigen achten
/// `StatAccent`-Fall stillschweigend als farblosen Streifen gerendert hätte (ADR 0035s
/// Begründung für den benannten `NoAccent`-Fall wäre damit unterlaufen worden). Ein
/// einziger benannter Match hier hält `FS0025` als Warnschuss für jeden künftigen Fall.
let accentBarClassName (accent: StatAccent) : string option =
    match accent with
    | NoAccent -> None
    | Accent -> bar "from-accent to-accent"
    | Success -> bar "from-success to-success"
    | Error -> bar "from-error to-error"
    | Gradient -> bar "from-accent to-success"

let private sizeToClasses = function
    | Compact -> "p-3 md:p-4", "text-lg md:text-xl", $"{FontSizes.micro} md:text-xs"
    | Normal -> "p-4 md:p-6", "text-xl md:text-2xl", "text-xs md:text-sm"
    | Large -> "p-5 md:p-8", "text-2xl md:text-4xl", "text-sm md:text-base"

let private trendView (trend: Trend) =
    match trend with
    | Neutral -> Html.none
    | Up pct ->
        Html.div [
            prop.className "flex items-center gap-1 text-success text-xs md:text-sm font-medium"
            prop.children [
                Svg.svg [
                    svg.className "w-3 h-3 md:w-4 md:h-4"
                    svg.fill "none"
                    svg.viewBox (0, 0, 24, 24)
                    svg.stroke "currentColor"
                    svg.strokeWidth 2
                    svg.children [
                        Svg.path [
                            svg.strokeLineCap "round"
                            svg.strokeLineJoin "round"
                            svg.d "M7 17l9.2-9.2M17 17V7H7"
                        ]
                    ]
                ]
                Html.span [ prop.text (sprintf "+%.1f%%" pct) ]
            ]
        ]
    | Down pct ->
        Html.div [
            prop.className "flex items-center gap-1 text-error text-xs md:text-sm font-medium"
            prop.children [
                Svg.svg [
                    svg.className "w-3 h-3 md:w-4 md:h-4"
                    svg.fill "none"
                    svg.viewBox (0, 0, 24, 24)
                    svg.stroke "currentColor"
                    svg.strokeWidth 2
                    svg.children [
                        Svg.path [
                            svg.strokeLineCap "round"
                            svg.strokeLineJoin "round"
                            svg.d "M17 7l-9.2 9.2M7 7v10h10"
                        ]
                    ]
                ]
                Html.span [ prop.text (sprintf "-%.1f%%" (abs pct)) ]
            ]
        ]

/// Create a stat card with the specified props
let view (props: StatProps) =
    let padding, valueSize, labelSize = sizeToClasses props.Size
    let extraClass = props.ClassName |> Option.defaultValue ""
    let clickableClass =
        match props.OnClick with
        | Some _ -> "cursor-pointer hover:scale-[1.02]"
        | None -> ""
    let activeClass =
        if props.IsActive then "ring-2 ring-accent ring-offset-2 ring-offset-surface-card"
        else ""

    Html.div [
        prop.className $"bg-surface-card border border-border-subtle rounded-xl {padding} relative overflow-hidden transition-all hover:border-border-default hover:-translate-y-0.5 {clickableClass} {activeClass} {extraClass}"
        match props.OnClick with
        | Some onClick -> prop.onClick (fun _ -> onClick ())
        | None -> ()
        prop.children [
            // Decorative gradient accent line at top: die Entscheidung, ob und mit
            // welcher Klasse gerendert wird, steht nur noch in `accentBarClassName`
            // (design-system-j9xnd).
            match accentBarClassName props.Accent with
            | None -> Html.none
            | Some cls -> Html.div [ prop.className cls ]

            // Main content
            Html.div [
                prop.className "flex flex-col gap-2"
                prop.children [
                    // Top row: Label + Icon
                    Html.div [
                        prop.className "flex items-center justify-between"
                        prop.children [
                            Html.span [
                                prop.className $"{labelSize} font-medium text-text-muted/70 uppercase tracking-wider"
                                prop.text props.Label
                            ]
                            match props.Icon with
                            | Some icon ->
                                Html.div [
                                    prop.className "text-text-muted"
                                    prop.children [ icon ]
                                ]
                            | None -> Html.none
                        ]
                    ]

                    // Value row
                    Html.div [
                        prop.className "flex items-baseline gap-2"
                        prop.children [
                            match props.ValueElement with
                            | Some el -> el
                            | None ->
                                Html.span [
                                    prop.className $"font-mono font-bold {valueSize} text-text-primary tabular-nums"
                                    prop.text props.Value
                                ]
                            match props.Trend with
                            | Some trend -> trendView trend
                            | None -> Html.none
                        ]
                    ]

                    // Optional description
                    match props.Description with
                    | Some desc ->
                        Html.p [
                            prop.className "text-xs text-text-muted mt-1"
                            prop.text desc
                        ]
                    | None -> Html.none
                ]
            ]
        ]
    ]

// ============================================
// Convenience Functions
// ============================================

/// Simple stat card with label and value
let simple label value =
    view { defaultProps with Label = label; Value = value }

/// Stat card with icon
let withIcon label value icon =
    view { defaultProps with Label = label; Value = value; Icon = Some icon }

/// Stat card with trend indicator
let withTrend label value trend =
    view { defaultProps with Label = label; Value = value; Trend = Some trend }

/// Stat card with icon and trend
let withIconAndTrend label value icon trend =
    view {
        defaultProps with
            Label = label
            Value = value
            Icon = Some icon
            Trend = Some trend
    }

/// Compact stat for tighter spaces
let compact label value =
    view { defaultProps with Label = label; Value = value; Size = Compact }

/// Large hero-style stat
let hero label value =
    view { defaultProps with Label = label; Value = value; Size = Large }

/// Stat with specific accent color
let withAccent label value accent =
    view { defaultProps with Label = label; Value = value; Accent = accent }

// ============================================
// Stat Grid Layout
// ============================================

/// Responsive grid for stat cards (1 col mobile, 2 tablet, 3 desktop)
let grid (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 md:gap-4"
        prop.children stats
    ]

/// Grid with 2 columns max (for dashboards with fewer stats)
let gridTwoCol (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 gap-3 md:gap-4"
        prop.children stats
    ]

/// Grid with 4 columns on large screens
let gridFourCol (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 md:gap-4"
        prop.children stats
    ]

/// Five-column responsive grid (2 cols on mobile, 3 on tablet, 5 on desktop)
let gridFiveCol (stats: ReactElement list) =
    Html.div [
        prop.className "grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3 md:gap-4"
        prop.children stats
    ]

// ============================================
// Specialized Stat Cards
// ============================================

/// Transaction count stat
let transactionCount count =
    withIcon "Transactions" (string count) (Icons.creditCard MD Default)

/// Sync count stat
let syncCount count =
    withIcon "Syncs" (string count) (Icons.sync MD Default)

/// Money stat (positiv Grün, negativ Rot: Minus, ADR 0087)
let moneyStat label (amount: decimal) currency =
    let formatted = sprintf "%+.2f %s" amount currency
    let accent = if amount >= 0m then Success else Error
    withAccent label formatted accent

/// Category stat
let categoryStat label count =
    view {
        defaultProps with
            Label = label
            Value = string count
            Accent = Accent
            Icon = Some (Icons.rules MD Default)
    }

/// Rules count stat
let rulesCount count =
    withIcon "Active Rules" (string count) (Icons.rules MD Default)

// ============================================
// Monatsbalken (budgeting-x4k7p)
// ============================================

/// Ein Balken der Monatsbalken. Die Texte kommen fertig formatiert, der Baustein rechnet nichts.
type MonthBar = {
    /// Eindeutig in der Reihe, etwa der Monatserste "2026-10-01".
    Key: string
    /// Monat, genauer Betrag und Stand ("Oktober 2026 · 1.234,56 €"). Steht in der Wertzeile, sobald der
    /// Balken gewählt ist, und ist das, was der Screenreader je Balken vorliest.
    Label: string
    /// Der kurze Monatsname unter dem Balken ("Okt").
    MonthText: string
    /// Der Betrag unter dem Balken.
    AmountText: string
    /// Höhe in Prozent des größten Balkens, 0 bis 100.
    Percent: int
    /// Der laufende Monat: neutral gestrichelt, darunter "läuft". Der Akzent bleibt für Ausgewähltes (ADR 0087).
    Running: bool
}

let private monthBarsClass = "month-bars relative flex flex-col gap-1"
let private monthBarsListClass = "flex gap-1.5"
/// Die Spalte eines Balkens, gleich im Baustein und im Platzhalter.
let private monthBarColumnClass = "flex flex-1 min-w-0 flex-col items-center gap-1"
/// Das `li` ist ein reines Listenelement. Ein vorgewählter Balken (`monthBarsChosen`) gilt als gewählt,
/// solange kein anderer Maus oder Fokus hat. `month-bar flex flex-1 min-w-0` ist die Basis.
let private monthBarItemClass (chosen: bool) =
    let baseClass = "month-bar flex flex-1 min-w-0"
    if chosen then $"{baseClass} month-bar-chosen" else baseClass
/// Das innere Element trägt Fokus und Namen (budgeting-r3wq8). Maus, Tipp und Tab-Taste wählen den
/// Balken. Die Wahl zeigen Wertzeile, Monatsname und hellerer Balken, ohne eigenen Fokus-Ring
/// (styles.css, design-system-w7c2h).
let private monthBarTargetClass = $"month-bar-target {monthBarColumnClass}"
let private monthBarPlaceholderItemClass = monthBarColumnClass
let private monthBarAreaClass = "flex h-16 w-full items-end"
/// Der laufende Monat ist neutral gestrichelt, die vergangenen sind gefüllt.
let private monthBarFillClass (running: bool) =
    if running then "month-bar-fill-running w-full rounded-t border border-dashed border-border-default bg-text-muted/10"
    else "month-bar-fill w-full rounded-t bg-text-muted/30"
let private monthBarPlaceholderClass = "h-16 w-full rounded-t bg-surface-elevated animate-pulse"
let private monthBarMonthClass = "month-bar-month h-4 font-mono text-xs font-normal text-text-muted whitespace-nowrap"
/// Der Betrag unter dem Balken, immer neutral. Auch ein Monat mit Erstattungsüberschuss ist kein Problem,
/// er steht mit Plus da und nicht in Rot (budgeting-v6m3z, ADR 0075).
let monthBarAmountClass = "h-4 font-mono text-xs tabular-nums whitespace-nowrap text-text-secondary"
/// Die Zeile für "läuft" steht in jeder Spalte, damit die Reihe gleich hoch bleibt, mit und ohne
/// laufenden Monat und im Platzhalter.
let private monthBarStateClass = "h-4 text-xs text-text-muted whitespace-nowrap"
/// Die Wertzeile über den Balken hält ihre Höhe, auch ohne Auswahl: nichts springt.
let private monthBarValueRowClass = "h-4"
/// Der Wert eines Balkens liegt über der ganzen Breite der Wertzeile und läuft so nie über den Rand.
/// Sichtbar nur am gewählten Balken (styles.css), und nie Ziel eines Tipps.
let private monthBarValueClass =
    "month-bar-value absolute inset-x-0 top-0 h-4 truncate text-center font-mono text-xs tabular-nums text-text-secondary whitespace-nowrap"

/// Was der Screenreader je Balken vorliest: das `Label` mit Komma statt "·" (budgeting-r3wq8). Sichtbar
/// bleibt "·".
let monthBarAriaLabel (bar: MonthBar) : string =
    bar.Label.Replace(" · ", ", ")

/// Der Text unter dem laufenden Monat.
[<Literal>]
let RunningMonthText = "läuft"

let private monthBarItem (chosen: string option) (bar: MonthBar) =
    Html.li [
        prop.key bar.Key
        prop.className (monthBarItemClass (chosen = Some bar.Key))
        prop.children [
            Html.div [
                prop.custom ("data-testid", "month-bar")
                prop.role "img"
                prop.tabIndex 0
                prop.ariaLabel (monthBarAriaLabel bar)
                prop.className monthBarTargetClass
                prop.children [
                    Html.span [
                        prop.ariaHidden true
                        prop.custom ("data-testid", "month-bar-value")
                        prop.className monthBarValueClass
                        prop.text bar.Label
                    ]
                    Html.div [
                        prop.ariaHidden true
                        prop.className monthBarAreaClass
                        prop.children [
                            Html.div [
                                prop.className (monthBarFillClass bar.Running)
                                prop.style [ style.height (length.percent (max bar.Percent 2)) ]
                            ]
                        ]
                    ]
                    Html.span [ prop.ariaHidden true; prop.className monthBarMonthClass; prop.text bar.MonthText ]
                    Html.span [
                        prop.ariaHidden true
                        prop.className monthBarAmountClass
                        prop.text bar.AmountText
                    ]
                    Html.span [
                        prop.ariaHidden true
                        prop.className monthBarStateClass
                        prop.text (if bar.Running then RunningMonthText else "")
                    ]
                ]
            ]
        ]
    ]

/// Monatsbalken ohne waagrechtes Scrollen: je Monat ein Balken, darunter Monat und Betrag, darüber eine
/// Wertzeile. Steht die Maus über einem Balken, hat er den Fokus oder wurde er angetippt, zeigt die
/// Wertzeile sein `Label`; ein Tipp daneben nimmt den Fokus weg und leert sie. Allein über CSS, ohne
/// Zustand im Model. Für den Screenreader eine Liste, je Balken ein Eintrag mit `Label`; die sichtbaren
/// Texte sind verborgen.
let private monthBarsView (label: string) (items: ReactElement list) =
    Html.div [
        prop.custom ("data-testid", "month-bars")
        prop.className monthBarsClass
        prop.children [
            Html.div [
                prop.ariaHidden true
                prop.custom ("data-testid", "month-bar-value-row")
                prop.className monthBarValueRowClass
            ]
            Html.ul [
                prop.role "list"
                prop.ariaLabel label
                prop.className monthBarsListClass
                prop.children items
            ]
        ]
    ]

let private fullMonthBars (chosen: string option) (label: string) (bars: MonthBar list) =
    bars |> List.map (monthBarItem chosen) |> monthBarsView label

let monthBars (label: string) (bars: MonthBar list) = fullMonthBars None label bars

/// Wie `monthBars`, der Balken mit `chosenKey` steht schon gewählt da, bis Maus oder Fokus einen
/// anderen wählen. Für den Styleguide, der die Wertzeile ohne Zutun zeigt.
let monthBarsChosen (label: string) (chosenKey: string) (bars: MonthBar list) =
    fullMonthBars (Some chosenKey) label bars

/// Der Platzhalter beim Laden, so hoch wie `monthBars`: nichts springt, wenn die Balken kommen.
let monthBarsPlaceholder (count: int) =
    Html.div [
        prop.ariaHidden true
        prop.custom ("data-testid", "month-bars-placeholder")
        prop.className monthBarsClass
        prop.children [
            Html.div [ prop.className monthBarValueRowClass ]
            Html.div [
                prop.className monthBarsListClass
                prop.children [
                    for i in 1..count do
                        Html.div [
                            prop.key i
                            prop.className monthBarPlaceholderItemClass
                            prop.children [
                                Html.div [ prop.className monthBarPlaceholderClass ]
                                Html.span [ prop.className monthBarMonthClass ]
                                Html.span [ prop.className monthBarAmountClass ]
                                Html.span [ prop.className monthBarStateClass ]
                            ]
                        ]
                ]
            ]
        ]
    ]

// ============================================
// Mini-Balken (budgeting-c4ve4)
// ============================================

/// Der Ton eines Mini-Balkens.
type SparkTone =
    /// Über dem Target: Gelb, braucht dich (ADR 0087).
    | SparkOver
    /// Gezählt, nicht darüber: gedämpft.
    | SparkWithin
    /// Zählt nicht (vor der letzten Änderung): dunkler.
    | SparkMuted

/// Ein Mini-Balken: Höhe in Prozent des größten Werts (0 bis 100) und Ton. Gerechnet wird außerhalb.
type SparkBar = {
    Key: string
    Percent: int
    Tone: SparkTone
    /// Der kurze Monatsname unter dem Balken, nur in `sparkBarsLabelled`.
    MonthText: string
}

let private sparkToneClass (tone: SparkTone) =
    match tone with
    | SparkOver -> "bg-attention"
    | SparkWithin -> "bg-text-muted/45"
    | SparkMuted -> "bg-text-muted/15"

/// Die gestrichelte Target-Linie auf `percent` der Höhe.
let private sparkLine (percent: int option) =
    match percent with
    | Some p ->
        Html.div [
            prop.custom ("data-testid", "spark-line")
            prop.className "pointer-events-none absolute inset-x-0 border-t border-dashed border-text-primary/60"
            prop.style [ style.bottom (length.percent p) ]
        ]
    | None -> Html.none

let private sparkBar (large: bool) (bar: SparkBar) =
    let radius = if large then "rounded-[3px]" else "rounded-[2px]"
    Html.div [
        prop.key bar.Key
        prop.custom ("data-testid", "spark-bar")
        prop.ariaHidden true
        prop.className $"flex-1 min-w-0 min-h-0.5 {radius} {sparkToneClass bar.Tone}"
        prop.style [ style.height (length.percent bar.Percent) ]
    ]

/// Mini-Balken für eine Listenzeile (Seite Targets prüfen): je Monat ein Balken ohne Beschriftung, mindestens
/// 2 px hoch, dazu die gestrichelte Target-Linie. Die Reihe füllt die Höhe ihres Elternelements, der Aufrufer
/// setzt sie. Für den Screenreader ein Bild mit `label`, die Balken sind verborgen. Kein Tab-Stopp.
/// Ecken 2 px wie die Skizze. Abstand 4 px (Handy.dc.html), ab einem Container von 720 px 3 px (Main.dc.html):
/// dieselbe Container-Grenze, an der die Zeile von zwei Reihen auf vier Spalten wechselt.
let sparkBars (label: string) (linePercent: int option) (bars: SparkBar list) =
    Html.div [
        prop.custom ("data-testid", "spark-bars")
        prop.role "img"
        prop.ariaLabel label
        prop.className "relative flex h-full items-end gap-1 @min-[720px]:gap-[3px]"
        prop.children [
            yield! bars |> List.map (sparkBar false)
            sparkLine linePercent
        ]
    ]

/// Große Mini-Balken für das Panel: 56 px Balken, darunter der Monatsname in Mono klein.
let sparkBarsLabelled (label: string) (linePercent: int option) (bars: SparkBar list) =
    Html.div [
        prop.custom ("data-testid", "spark-bars-labelled")
        prop.role "img"
        prop.ariaLabel label
        prop.className "flex flex-col gap-1"
        prop.children [
            Html.div [
                prop.className "relative flex h-14 items-end gap-1.5"
                prop.children [
                    yield! bars |> List.map (sparkBar true)
                    sparkLine linePercent
                ]
            ]
            Html.div [
                prop.ariaHidden true
                prop.className "flex gap-1.5"
                prop.children [
                    for bar in bars do
                        Html.span [
                            prop.key bar.Key
                            prop.className $"flex-1 min-w-0 font-mono {FontSizes.micro} text-text-muted text-center"
                            prop.text bar.MonthText
                        ]
                ]
            ]
        ]
    ]

/// Der Platzhalter beim Laden, füllt wie `sparkBars` die Höhe des Elternelements, Ecken 2 px wie die Balken.
let sparkBarsPlaceholder () =
    Html.div [
        prop.ariaHidden true
        prop.className "h-full rounded-[2px] bg-surface-elevated animate-pulse"
    ]
