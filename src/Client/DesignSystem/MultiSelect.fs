/// Bausteine für eine Mehrfachauswahl in Listen (ledger-r1d2f, Handoff Ablauf 3):
/// das Kästchen mit seinen drei Zuständen, die Summenzeile im Auswahl-Zustand und die
/// Auswahl-Leiste am unteren Rand (Handy: zwei Reihen, PC: eine Reihe). Die Bausteine
/// kennen weder Zeilen noch Modell, der Aufrufer reicht Zahlen, Texte und Rückrufe herein.
///
/// Die Leiste zeigt bewusst keine schreibenden Aktionen (Entscheidung Roman 2026-10-19):
/// "Geprüft", "Kategorie ändern" und "Flag" kommen mit ihren eigenen Aufgaben.
module Client.DesignSystem.MultiSelect

open Feliz
open Client.DesignSystem.Tokens

/// Zustand eines Kästchens: leer, gesetzt oder Teilauswahl (Teal-Strich statt Haken).
type CheckState =
    | Unchecked
    | Checked
    | Partial

/// Klassen des Kästchens. `large` ist das Handy-Kästchen (20px), sonst das PC-Kästchen
/// (17px). Leer: Eingabegrund und Standardrand; gesetzt: Teal; Teilauswahl: Teal-Rand
/// auf blassem Teal-Grund.
let boxClassName (state: CheckState) (large: bool) : string =
    let size = if large then "size-5" else "size-[17px]"
    let look =
        match state with
        | Unchecked -> $"{Backgrounds.elevated} {Borders.default'}"
        | Checked -> "bg-neon-teal border-neon-teal"
        | Partial -> "bg-neon-teal/20 border-neon-teal"
    $"inline-flex shrink-0 items-center justify-center rounded-[5px] border transition-colors {size} {look}"

/// Der Haken im gesetzten Kästchen: dunkel auf Teal, kräftiger Strich.
let private tick (large: bool) : ReactElement =
    Svg.svg [
        svg.className (if large then "size-[13px] text-surface-app" else "size-[11px] text-surface-app")
        svg.fill "none"
        svg.viewBox (0, 0, 24, 24)
        svg.stroke "currentColor"
        svg.strokeWidth 3.5
        svg.children [
            Svg.path [
                svg.strokeLineCap "round"
                svg.strokeLineJoin "round"
                svg.d "m4.5 12.75 6 6 9-13.5"
            ]
        ]
    ]

/// Das Kästchen als reine Darstellung (ohne Klick): in der Handy-Zeile, deren ganze
/// Fläche die Auswahl schaltet.
let box (state: CheckState) (large: bool) : ReactElement =
    Html.span [
        prop.className (boxClassName state large)
        prop.ariaHidden true
        prop.children [
            match state with
            | Checked -> tick large
            | Partial -> Html.span [ prop.className "block h-[2.5px] w-[9px] rounded-sm bg-neon-teal" ]
            | Unchecked -> ()
        ]
    ]

/// Das Kästchen als Knopf (PC): `role="checkbox"`, bei Teilauswahl `aria-checked="mixed"`.
/// Der Klick erreicht die Zeile dahinter nicht (sie öffnet sonst die Historie). Der Rückruf
/// bekommt mit, ob Shift gehalten wurde.
let boxButton (state: CheckState) (label: string) (onClick: bool -> unit) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.role "checkbox"
        prop.ariaLabel label
        prop.custom (
            "aria-checked",
            match state with
            | Checked -> "true"
            | Partial -> "mixed"
            | Unchecked -> "false"
        )
        prop.className "inline-flex items-center justify-center p-1.5 -m-1.5 touch-manipulation"
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick e.shiftKey)
        prop.children [ box state false ]
    ]

/// Klassen der Summe in Zeile und Leiste: Mono, Ziffern gleich breit; wie in den ruhigen
/// Listen (ADR 0049) nur ein Eingang in Teal, sonst neutral.
let amountClassName (amount: decimal) : string =
    let color = if amount > 0m then Colors.neonTeal else Colors.textPrimary
    $"font-mono font-semibold tabular-nums whitespace-nowrap {color}"

let private amountText (amount: Shared.Domain.Money) : string =
    Money.formatAmountText (amount.Amount > 0m) true amount.Amount amount.Currency

/// Die Summenzeile im Auswahl-Zustand: Teal-Kante links, Teal-Unterkante, Beschriftung in
/// Teal ("Auswahl: 12 von 34 Buchungen") und die Summe der Auswahl rechts.
let summaryRow (label: string) (amount: Shared.Domain.Money) : ReactElement =
    Html.div [
        prop.role "status"
        prop.className (
            $"flex items-center justify-between gap-3 px-3 min-h-8 {Backgrounds.dark} " +
            "border-b border-neon-teal/30 shadow-[inset_2px_0_0_0_var(--color-neon-teal)]"
        )
        prop.children [
            Html.span [ prop.className $"{FontSizes.xs} font-semibold {Colors.neonTeal}"; prop.text label ]
            Html.span [
                prop.className $"{FontSizes.sm} {amountClassName amount.Amount}"
                prop.text (amountText amount)
            ]
        ]
    ]

/// Der Textknopf "Auswahl aufheben": ohne Grund und Rand, gedämpft. `fill` füllt am Handy
/// die Reihe (44px Trefferfläche), am PC bleibt er so breit wie sein Text.
let private clearButton (label: string) (onClick: unit -> unit) (fill: bool) : ReactElement =
    Html.button [
        prop.type' "button"
        prop.className (
            (if fill then "flex-1 min-h-[44px] " else "py-2.5 ") +
            $"inline-flex items-center justify-center rounded-lg px-3.5 font-semibold {FontSizes.sm} " +
            $"{Colors.textSecondary} hover:text-text-primary hover:bg-surface-hover transition-colors touch-manipulation"
        )
        prop.onClick (fun _ -> onClick ())
        prop.text label
    ]

/// Reihe 1 der Leiste und die Zahlen der PC-Leiste: "n ausgewählt" und die Summe.
let private countAndSum (countText: string) (amount: Shared.Domain.Money) : ReactElement list =
    [ Html.span [ prop.className $"{FontSizes.sm} font-semibold {Colors.textPrimary}"; prop.text countText ]
      Html.span [
          prop.className $"{FontSizes.lg} {amountClassName amount.Amount}"
          prop.text (amountText amount)
      ] ]

/// Die einzige primäre Aktion der Leiste (Orange): setzt "Geprüft" für die Auswahl.
/// Während der Aufruf läuft, zeigt sie den Spinner und ist gesperrt. `className` setzt
/// die Breite (am Handy behält sie ihre Textbreite, "Auswahl aufheben" füllt den Rest der Reihe).
let private reviewedButton (label: string) (busy: bool) (onClick: unit -> unit) (className: string) : ReactElement =
    Button.view {
        Button.defaultProps with
            Text = label
            OnClick = onClick
            Variant = Button.Primary
            IsLoading = busy
            ClassName = Some className
    }

/// Die Auswahl-Leiste am Handy: ersetzt die Tab-Bar. Reihe 1 "n ausgewählt" und Summe,
/// Reihe 2 "Alle" (Teal, wenn der ganze Treffer gewählt ist), "Geprüft" (primär) und
/// "Auswahl aufheben".
let barMobile
    (countText: string)
    (amount: Shared.Domain.Money)
    (allLabel: string)
    (allActive: bool)
    (onAll: unit -> unit)
    (reviewedLabel: string)
    (reviewedBusy: bool)
    (onReviewed: unit -> unit)
    (clearLabel: string)
    (onClear: unit -> unit)
    : ReactElement =
    Html.div [
        prop.className (
            $"selection-bar md:hidden fixed bottom-0 left-0 right-0 z-40 flex flex-col gap-2.5 {Backgrounds.surface} " +
            $"border-t {Borders.default'} px-4 pt-3 pb-[22px] safe-area-pb"
        )
        prop.children [
            Html.div [
                prop.className "flex items-center justify-between gap-3"
                prop.children (countAndSum countText amount)
            ]
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    ChipBar.chip { Label = allLabel; IsActive = allActive; OnClick = onAll; OnRemove = None }
                    reviewedButton reviewedLabel reviewedBusy onReviewed "shrink-0"
                    clearButton clearLabel onClear true
                ]
            ]
        ]
    ]

/// Die Auswahl-Leiste am PC: eine Reihe, ersetzt den Tastatur-Hinweis. Kein "Alle", das
/// Kopfzeilen-Kästchen erledigt es. Rechts "Geprüft" (primär) vor "Auswahl aufheben".
let barDesktop
    (countText: string)
    (amount: Shared.Domain.Money)
    (reviewedLabel: string)
    (reviewedBusy: bool)
    (onReviewed: unit -> unit)
    (clearLabel: string)
    (clearKeyHint: string)
    (onClear: unit -> unit)
    : ReactElement =
    Html.div [
        prop.className (
            $"selection-bar hidden md:flex fixed bottom-0 left-0 right-0 z-40 items-center gap-4 {Backgrounds.surface} " +
            $"border-t {Borders.default'} px-5 py-3.5"
        )
        prop.children [
            yield! countAndSum countText amount
            Html.div [
                prop.className "ml-auto flex items-center gap-2"
                prop.children [
                    // Die Taste zum Aufheben, gedämpft in Mono neben dem Knopf (ledger-w4vte).
                    Html.span [
                        prop.className $"{Fonts.mono} {FontSizes.xs} {Colors.textMuted}"
                        prop.text clearKeyHint
                    ]
                    clearButton clearLabel onClear false
                    reviewedButton reviewedLabel reviewedBusy onReviewed ""
                ]
            ]
        ]
    ]
