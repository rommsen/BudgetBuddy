/// Bausteine für eine Mehrfachauswahl in Listen (ledger-r1d2f, Handoff Ablauf 3):
/// das Kästchen mit seinen drei Zuständen, die Summenzeile im Auswahl-Zustand und die
/// Auswahl-Leiste am unteren Rand (Handy: zwei Reihen, PC: eine Reihe). Die Bausteine
/// kennen weder Zeilen noch Modell, der Aufrufer reicht Zahlen, Texte und Rückrufe herein.
///
/// Die Leiste trägt die Sammelaktionen, die je eine eigene Aufgabe eingeführt hat: "Geprüft"
/// (primär), "Kategorie setzen" und die Rückfrage an Lena (beide sekundär).
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
            (if fill then "flex-1 min-h-[44px] px-2 " else "py-2.5 px-3.5 ") +
            $"inline-flex items-center justify-center rounded-lg font-semibold {FontSizes.sm} " +
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

/// Die zweite Sammelaktion der Leiste (ledger-w29y6): "Kategorie setzen", sekundär.
/// `IsBusy`: der Aufruf läuft, der Knopf zeigt den Spinner. `IsLocked`: irgendein Sammelaufruf
/// läuft, beide Aktionen der Leiste sind gesperrt. `Popover`: der Picker am PC (steht in einem
/// Wrapper am Knopf und öffnet über der Leiste), `Html.none` am Handy. `Sheet`: der Picker am
/// Handy (portalt in den Body), `Html.none` am PC.
type CategoryAction = {
    Label: string
    IsBusy: bool
    IsLocked: bool
    OnClick: unit -> unit
    Popover: ReactElement
    Sheet: ReactElement
}

/// Eine der beiden Lena-Aktionen der Leiste (ledger-y54gr): "Lena fragen" oder "Rückfrage
/// zurücknehmen", sekundär. `Label` trägt die Zahl der gewählten Buchungen. `IsBusy`: der Aufruf
/// läuft, der Knopf zeigt den Spinner. `IsLocked`: irgendein Sammelaufruf läuft. `Reason`: der
/// Grund, aus dem die Aktion nicht wirkt (z. B. fehlende Farbe in den Settings); der Knopf ist
/// dann gesperrt und nennt ihn als Tooltip, am Handy zusätzlich als Zeile unter den Knöpfen.
type LenaAction = {
    Label: string
    IsBusy: bool
    IsLocked: bool
    Reason: string option
    OnClick: unit -> unit
}

/// Die primäre Aktion der Leiste (Orange): setzt "Geprüft" für die Auswahl.
/// Während der Aufruf läuft, zeigt sie den Spinner und ist gesperrt; `locked` sperrt sie auch,
/// wenn die andere Sammelaktion läuft. `className` setzt die Breite (am Handy behält sie ihre
/// Textbreite, "Auswahl aufheben" füllt den Rest der Reihe).
let private reviewedButton
    (label: string)
    (busy: bool)
    (locked: bool)
    (size: Button.ButtonSize)
    (onClick: unit -> unit)
    (className: string)
    : ReactElement =
    Button.view {
        Button.defaultProps with
            Text = label
            Size = size
            OnClick = onClick
            Variant = Button.Primary
            IsLoading = busy
            IsDisabled = locked
            ClassName = Some className
    }

/// Die sekundäre Sammelaktion (Teal-Rand aus dem Design-System).
let private categoryButton (action: CategoryAction) (size: Button.ButtonSize) (className: string) : ReactElement =
    Button.view {
        Button.defaultProps with
            Text = action.Label
            Size = size
            OnClick = action.OnClick
            Variant = Button.Secondary
            IsLoading = action.IsBusy
            IsDisabled = action.IsLocked
            ClassName = Some className
    }

/// Ein sekundärer Lena-Knopf. Ein Grund sperrt ihn und steht im Tooltip.
let private lenaButton (action: LenaAction) (size: Button.ButtonSize) (className: string) : ReactElement =
    Button.view {
        Button.defaultProps with
            Text = action.Label
            Size = size
            OnClick = action.OnClick
            Variant = Button.Secondary
            IsLoading = action.IsBusy
            IsDisabled = action.IsLocked || action.Reason.IsSome
            Title = action.Reason
            ClassName = Some className
    }

/// Die Auswahl-Leiste am Handy: ersetzt die Tab-Bar. Reihe 1 "n ausgewählt" und Summe,
/// Reihe 2 "Alle" (Teal, wenn der ganze Treffer gewählt ist), "Geprüft" (primär),
/// "Kategorie setzen" (sekundär) und "Auswahl aufheben", Reihe 3 die beiden Lena-Aktionen
/// (sekundär) und, wenn eine gesperrt ist, deren Grund. `locked`: ein Sammelaufruf läuft.
let barMobile
    (countText: string)
    (amount: Shared.Domain.Money)
    (allLabel: string)
    (allActive: bool)
    (onAll: unit -> unit)
    (reviewedLabel: string)
    (reviewedBusy: bool)
    (locked: bool)
    (onReviewed: unit -> unit)
    (category: CategoryAction)
    (lena: LenaAction)
    (withdraw: LenaAction)
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
                    // Kleine Knöpfe, damit vier Elemente bei 390 px in eine Reihe passen.
                    reviewedButton reviewedLabel reviewedBusy locked Button.Small onReviewed "shrink-0 whitespace-nowrap"
                    categoryButton category Button.Small "shrink-0 whitespace-nowrap"
                    clearButton clearLabel onClear true
                ]
            ]
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    lenaButton lena Button.Small "flex-1 whitespace-nowrap"
                    lenaButton withdraw Button.Small "flex-1 whitespace-nowrap"
                ]
            ]
            match lena.Reason with
            | Some reason ->
                Html.p [
                    prop.custom ("data-testid", "selection-lena-reason")
                    prop.className $"{FontSizes.xs} {Colors.textMuted}"
                    prop.text reason
                ]
            | None -> ()
            category.Sheet
        ]
    ]

/// Die Auswahl-Leiste am PC: eine Reihe, ersetzt den Tastatur-Hinweis. Kein "Alle", das
/// Kopfzeilen-Kästchen erledigt es. Rechts "Auswahl aufheben", die beiden Lena-Aktionen
/// (sekundär, ein Grund steht im Tooltip), "Kategorie setzen" (sekundär, mit dem Picker als
/// Popover über der Leiste) und zuletzt "Geprüft" (primär).
let barDesktop
    (countText: string)
    (amount: Shared.Domain.Money)
    (reviewedLabel: string)
    (reviewedBusy: bool)
    (locked: bool)
    (onReviewed: unit -> unit)
    (category: CategoryAction)
    (lena: LenaAction)
    (withdraw: LenaAction)
    (clearLabel: string)
    (clearKeyHint: string)
    (onClear: unit -> unit)
    : ReactElement =
    Html.div [
        prop.className (
            $"selection-bar hidden md:flex flex-wrap fixed bottom-0 left-0 right-0 z-40 items-center gap-x-4 gap-y-2 {Backgrounds.surface} " +
            $"border-t {Borders.default'} px-5 py-3.5"
        )
        prop.children [
            yield! countAndSum countText amount
            Html.div [
                prop.className "ml-auto flex flex-wrap items-center justify-end gap-2"
                prop.children [
                    // Die Taste zum Aufheben, gedämpft in Mono neben dem Knopf (ledger-w4vte).
                    Html.span [
                        prop.className $"hidden lg:inline {Fonts.mono} {FontSizes.xs} {Colors.textMuted}"
                        prop.text clearKeyHint
                    ]
                    clearButton clearLabel onClear false
                    lenaButton withdraw Button.Medium ""
                    lenaButton lena Button.Medium ""
                    // Der Wrapper trägt das Popover; es öffnet über dem Knopf (`bottom-full`).
                    Html.div [
                        prop.className "relative"
                        // Ein Klick auf den Knopf schaltet den Picker um; das Schließen "Klick
                        // außerhalb" des Popovers (mousedown am Dokument) soll ihn nicht vorher
                        // schließen, sonst öffnete derselbe Klick ihn gleich wieder.
                        prop.onMouseDown (fun e -> e.stopPropagation ())
                        prop.children [ categoryButton category Button.Medium ""; category.Popover ]
                    ]
                    reviewedButton reviewedLabel reviewedBusy locked Button.Medium onReviewed ""
                ]
            ]
        ]
    ]
