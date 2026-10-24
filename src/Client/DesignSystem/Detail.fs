/// Bausteine des Buchungs-Details im Kassenbuch (ledger-q1hw2, Handoff Ablauf 7,
/// ADR 0053): das Seitenpanel am PC, der Kopf mit Betrag, die Hinweisleiste mit Grund
/// und Aktion, die Feldzeile, der Geprüft-Schalter, die Herkunft-Zeile, die
/// Historie-Zeitleiste und der Filter-Knopf. Der Inhalt ist für Panel und BottomSheet
/// derselbe; nur die Hülle unterscheidet sich (`panel` hier, `BottomSheet.view` dort).
/// Darstellung ruhig nach ADR 0049: Farbe nur für das, was Aufmerksamkeit braucht
/// (die Hinweisleiste und der Punkt der Zeitleiste), sonst neutral.
module Client.DesignSystem.Detail

open Feliz
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Breite des Seitenpanels in Pixeln. Die Spaltenwahl der Tabelle rechnet damit
/// (`Components.Ledger.View.detailPanelWidth`); die Klassen unten tragen den Wert als
/// Literal, weil Tailwind nur ganze Klassennamen findet.
let panelWidthPx = 420

// ============================================
// Hülle: Seitenpanel
// ============================================

/// Das Seitenpanel rechts neben der Liste. Es haftet unter der Navbar, scrollt in sich
/// und liegt rechts in der Spalte, die der Aufrufer `relative` hält und mit einem
/// rechten Innenabstand von 420 Pixeln (`md:pr-[420px]`) freihält. Nur ab `md`; am Handy öffnet das Detail als BottomSheet.
let panel (ariaLabel: string) (children: ReactElement list) : ReactElement =
    Html.div [
        prop.className "hidden md:block absolute inset-y-0 right-0 w-[420px]"
        prop.children [
            Html.aside [
                prop.custom ("data-testid", "ledger-detail-panel")
                prop.ariaLabel ariaLabel
                prop.className (
                    $"sticky top-16 flex h-[calc(100vh-7rem)] w-[420px] flex-col overflow-hidden " +
                    $"border-l {Borders.default'} {Backgrounds.dark}"
                )
                prop.children children
            ]
        ]
    ]

/// Der scrollende Bereich unter dem Kopf (Hinweisleiste, Felder, Historie).
let scrollBody (children: ReactElement list) : ReactElement =
    Html.div [
        prop.className "min-h-0 flex-1 overflow-y-auto"
        prop.children children
    ]

// ============================================
// Kopf
// ============================================

/// Der Betrag im Kopf: Mono, ruhig neutral, nur ein Eingang ist Teal; gedämpft für eine
/// in YNAB gelöschte Buchung (Lesezustand).
let amount (value: decimal) (currency: string) (muted: bool) : ReactElement =
    let color =
        if muted then Colors.textMuted
        elif value > 0m then Colors.neonTeal
        else Colors.textPrimary
    Html.span [
        prop.custom ("data-testid", "detail-amount")
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap text-xl md:text-2xl {color}"
        prop.text (Money.formatAmountText (value > 0m) true value currency)
    ]

let private closeButton (onClose: unit -> unit) : ReactElement =
    Html.button [
        prop.type'.button
        prop.ariaLabel "Detail schließen"
        prop.title "Schließen"
        prop.custom ("data-testid", "detail-close")
        prop.className (
            $"inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border {Borders.default'} " +
            $"{Backgrounds.elevated} {Colors.textMuted} hover:text-text-primary transition-colors touch-manipulation"
        )
        prop.onClick (fun _ -> onClose ())
        prop.children [ Icons.x Icons.SM Icons.Default ]
    ]

/// Der Filter-Knopf neben einem Feld (ADR 0053 Punkt 1): ein kleiner Trichter, dessen
/// Name und Tooltip das volle Ziel nennen ("Nur Buchungen von REWE zeigen"). Er wirkt
/// auf die Liste und lässt das Detail offen.
let filterButton (label: string) (onClick: unit -> unit) : ReactElement =
    Html.button [
        prop.type'.button
        prop.title label
        prop.ariaLabel label
        prop.custom ("data-testid", "detail-filter")
        prop.className (
            $"inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg {Colors.textMuted} " +
            "hover:text-text-primary hover:bg-surface-hover transition-colors touch-manipulation"
        )
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick ())
        prop.children [ Icons.filter Icons.SM Icons.Default ]
    ]

/// Der Kopf des Panels: Betrag, "Konto · Datum" und der Schließen-Knopf. Der Payee steht
/// als erste Feldzeile darunter, damit sein Filter-Knopf in derselben Spalte sitzt wie der
/// der Kategorie.
let head
    (amountElement: ReactElement)
    (subtitle: string)
    (onClose: (unit -> unit) option)
    : ReactElement =
    Html.div [
        prop.className $"flex items-start justify-between gap-3 px-4 py-4 border-b {Borders.subtle}"
        prop.children [
            Html.div [
                prop.className "min-w-0"
                prop.children [
                    amountElement
                    Html.p [
                        prop.className $"mt-1.5 {FontSizes.xs} {Colors.textMuted}"
                        prop.text subtitle
                    ]
                ]
            ]
            match onClose with
            | Some close -> closeButton close
            | None -> ()
        ]
    ]

// ============================================
// Hinweisleiste mit Grund und Aktion
// ============================================

type NoticeTone =
    | NoticeOrange
    | NoticeRed

/// Die Aktion einer Hinweisleiste; `Busy` sperrt sie, solange ihr Aufruf läuft.
type NoticeAction = {
    Label: string
    Busy: bool
    OnClick: unit -> unit
}

type NoticeProps = {
    Tone: NoticeTone
    /// Das Wort im Badge, z. B. "Noch nicht in YNAB".
    Badge: string
    Reason: string
    Action: NoticeAction option
}

/// Die Hinweisleiste direkt unter dem Kopf: ein Badge, der Grund darunter und, wenn eine
/// Aktion wirkt, genau diese eine. Die Fläche ist nur getönt (ADR 0049), die Aufmerksamkeit
/// trägt das Badge.
let notice (props: NoticeProps) : ReactElement =
    let surface, variant =
        match props.Tone with
        | NoticeOrange -> "bg-neon-orange/[0.07]", Badge.Orange
        | NoticeRed -> "bg-neon-red/[0.07]", Badge.Error

    Html.div [
        prop.role "status"
        prop.custom ("data-testid", "detail-notice")
        prop.className $"px-4 py-3 border-b {Borders.subtle} {surface}"
        prop.children [
            Badge.view { Badge.defaultProps with Text = props.Badge; Variant = variant; Size = Badge.Small }
            Html.p [
                prop.className $"mt-2 {FontSizes.xs} {Colors.textSecondary} leading-normal"
                prop.text props.Reason
            ]
            match props.Action with
            | Some action ->
                Html.div [
                    prop.className "mt-3"
                    prop.children [
                        Button.view {
                            Button.defaultProps with
                                Text = action.Label
                                Variant = Button.Secondary
                                Size = Button.Small
                                IsLoading = action.Busy
                                OnClick = action.OnClick
                        }
                    ]
                ]
            | None -> ()
        ]
    ]

// ============================================
// Felder
// ============================================

/// Eine Feldzeile: Bezeichnung links, Wert rechts, optional ein Knopf dahinter
/// (Filter-Knopf, ab v2 der Pfeil des Kategorie-Pickers). `testId` benennt die Zeile.
let fieldRowWith
    (onClick: (unit -> unit) option)
    (testId: string)
    (label: string)
    (value: ReactElement)
    (trailing: ReactElement option)
    : ReactElement =
    Html.div [
        prop.custom ("data-testid", testId)
        prop.className (
            $"flex min-h-14 items-center gap-3 px-4 py-2 border-b {Borders.subtle}"
            + (if onClick.IsSome then " cursor-pointer" else "")
        )
        match onClick with
        | Some click ->
            prop.onClick (fun _ -> click ())
            // Ein offenes Popover schließt bei einem Mausdruck außerhalb. Gehört der Druck zu
            // dieser Zeile, soll der Klick es schalten und nicht erst schließen und neu öffnen.
            prop.onMouseDown (fun e -> e.stopPropagation ())
        | None -> ()
        prop.children [
            Html.span [ prop.className $"shrink-0 {FontSizes.sm} {Colors.textMuted}"; prop.text label ]
            Html.div [
                prop.className "flex min-w-0 flex-1 items-center justify-end gap-1 text-right"
                prop.children [
                    value
                    match trailing with
                    | Some button -> button
                    | None -> ()
                ]
            ]
        ]
    ]

let fieldRow (testId: string) (label: string) (value: ReactElement) (trailing: ReactElement option) : ReactElement =
    fieldRowWith None testId label value trailing

/// Der Pfeil neben einem Feld, der eine Auswahl öffnet (Kategorie-Picker, ledger-sfab0).
/// `lockedHint`: gesperrt, der Knopf ist gedämpft, wirkt nicht und nennt den Grund als
/// Tooltip. Die Trefferfläche ist mindestens 44 px hoch.
let pickerArrow (label: string) (isOpen: bool) (lockedHint: string option) (onClick: unit -> unit) : ReactElement =
    let locked = lockedHint.IsSome
    Html.button [
        prop.type'.button
        prop.title (lockedHint |> Option.defaultValue label)
        prop.ariaLabel (lockedHint |> Option.defaultValue label)
        prop.custom ("aria-disabled", (if locked then "true" else "false"))
        prop.custom ("aria-expanded", (if isOpen then "true" else "false"))
        prop.custom ("data-testid", "detail-category-arrow")
        prop.className (
            $"inline-flex h-11 w-8 shrink-0 items-center justify-center rounded-lg {Colors.textMuted} transition-colors touch-manipulation "
            + (if locked then "opacity-40 cursor-not-allowed" else "hover:text-text-primary hover:bg-surface-hover")
        )
        prop.onMouseDown (fun e -> e.stopPropagation ())
        prop.onClick (fun e ->
            e.stopPropagation ()
            if not locked then onClick ())
        prop.children [ Icons.chevronDown Icons.SM Icons.Default ]
    ]

/// Ein unsichtbarer Platzhalter in der Breite des Pfeils: Feldzeilen ohne Pfeil (Payee) halten
/// damit ihren Filter-Knopf bündig mit dem der Kategorie-Zeile.
let pickerArrowSpacer () : ReactElement =
    Html.span [ prop.className "h-11 w-8 shrink-0"; prop.ariaHidden true ]

/// Der Wert einer Feldzeile als Text; `className` trägt die Farbe (Kategorie) oder
/// `Colors.textMuted` für einen Platzhalter.
let valueText (className: string) (text: string) : ReactElement =
    Html.span [
        prop.className ($"min-w-0 break-words {FontSizes.sm} " + className)
        prop.text text
    ]

/// Die Zeilen eines Splits, je Ziel und Betrag.
let splitLines (lines: (string * string) list) : ReactElement =
    Html.div [
        prop.className "flex min-w-0 flex-col gap-1"
        prop.children [
            for (target, amountText) in lines ->
                Html.div [
                    prop.key (target + amountText)
                    prop.className "flex min-w-0 items-baseline justify-end gap-3"
                    prop.children [
                        Html.span [ prop.className $"min-w-0 truncate {FontSizes.sm} {Colors.textSecondary}"; prop.text target ]
                        Html.span [
                            prop.className $"shrink-0 font-mono tabular-nums {FontSizes.sm} {Colors.textPrimary}"
                            prop.text amountText
                        ]
                    ]
                ]
        ]
    ]

/// Der Geprüft-Schalter als eigene Zeile (Handoff: Zeile mit Grund `surface-input`).
/// `Busy` sperrt ihn, solange der Aufruf läuft. Rolle `switch`, Zustand in `aria-checked`.
let reviewedSwitch (label: string) (isOn: bool) (busy: bool) (onToggle: bool -> unit) : ReactElement =
    let track =
        if isOn then "border-neon-green/40 bg-neon-green/20"
        else $"{Borders.default'} {Backgrounds.elevated}"
    let knob =
        if isOn then "translate-x-6 bg-neon-green"
        else "translate-x-1 bg-text-muted"

    Html.div [
        prop.custom ("data-testid", "detail-reviewed")
        prop.className $"flex min-h-14 items-center justify-between gap-3 px-4 py-2 border-b {Borders.subtle}"
        prop.children [
            Html.span [ prop.className $"{FontSizes.sm} font-medium {Colors.textPrimary}"; prop.text label ]
            Html.button [
                prop.type'.button
                prop.role "switch"
                prop.ariaLabel label
                prop.custom ("aria-checked", (if isOn then "true" else "false"))
                prop.disabled busy
                prop.className (
                    $"relative inline-flex h-7 w-12 shrink-0 items-center rounded-full border transition-colors " +
                    $"touch-manipulation disabled:opacity-50 disabled:cursor-not-allowed {track}"
                )
                prop.onClick (fun _ -> onToggle (not isOn))
                prop.children [
                    Html.span [
                        prop.className $"inline-block h-5 w-5 rounded-full transition-transform {knob}"
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Herkunft und Historie
// ============================================

/// Die Überschrift eines Abschnitts im Detail ("HERKUNFT", "HISTORIE").
let sectionLabel (text: string) : ReactElement =
    Html.div [
        prop.className $"{ListRoles.groupHeader} px-4 pt-4 pb-2"
        prop.text text
    ]

/// Die Herkunft-Zeile: Überschrift links, das Badge der Art rechts. Das Badge baut der
/// Aufrufer (Teal, `Badge.Info`), weil die Wörter zur Domäne gehören.
let originRow (badge: ReactElement) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "detail-origin")
        prop.className $"flex min-h-14 items-center justify-between gap-3 px-4 py-2 border-b {Borders.subtle}"
        prop.children [
            Html.span [ prop.className ListRoles.groupHeader; prop.text "HERKUNFT" ]
            badge
        ]
    ]

/// Die Punktfarbe eines Eintrags der Zeitleiste, nach Urheber (Handoff).
type DotColor =
    | DotTeal
    | DotPurple
    | DotGrey
    | DotGreen
    | DotRed
    | DotOrange

let dotName (dot: DotColor) : string =
    match dot with
    | DotTeal -> "teal"
    | DotPurple -> "purple"
    | DotGrey -> "grey"
    | DotGreen -> "green"
    | DotRed -> "red"
    | DotOrange -> "orange"

let private dotClass (dot: DotColor) : string =
    match dot with
    | DotTeal -> "bg-neon-teal"
    | DotPurple -> "bg-neon-purple"
    | DotGrey -> "bg-text-muted"
    | DotGreen -> "bg-neon-green"
    | DotRed -> "bg-neon-red"
    | DotOrange -> "bg-neon-orange"

type TimelineItem = {
    Key: string
    Dot: DotColor
    Text: string
    /// Zeitstempel und Urheber, z. B. "10.09.2026 17:55 · Regel".
    Stamp: string
}

/// Die Zeitleiste der Historie, in der Reihenfolge der Liste (der Aufrufer legt die
/// neueste Zeile nach oben). Der oberste Eintrag trägt den Text in der Hauptfarbe, die
/// älteren gedämpft.
let timeline (items: TimelineItem list) : ReactElement =
    Html.ol [
        prop.custom ("data-testid", "detail-timeline")
        prop.className "px-4 pt-1"
        prop.children [
            for (index, item) in List.indexed items ->
                Html.li [
                    prop.key item.Key
                    prop.className "flex gap-3 pb-4"
                    prop.children [
                        Html.span [
                            prop.custom ("data-dot", dotName item.Dot)
                            prop.className $"mt-1.5 h-2 w-2 shrink-0 rounded-full {dotClass item.Dot}"
                        ]
                        Html.div [
                            prop.className "min-w-0"
                            prop.children [
                                Html.p [
                                    prop.className (
                                        $"{FontSizes.sm} font-medium leading-snug " +
                                        (if index = 0 then Colors.textPrimary else Colors.textSecondary)
                                    )
                                    prop.text item.Text
                                ]
                                Html.p [
                                    prop.className $"mt-0.5 font-mono {FontSizes.xs} {Colors.textMuted}"
                                    prop.text item.Stamp
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]

/// Der Leerzustand der Historie.
let timelineEmpty (text: string) : ReactElement =
    Html.p [
        prop.custom ("data-testid", "detail-timeline-empty")
        prop.className $"px-4 py-4 {FontSizes.sm} {Colors.textMuted}"
        prop.text text
    ]
