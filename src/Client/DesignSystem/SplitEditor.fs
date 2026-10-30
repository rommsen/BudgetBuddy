/// Bausteine des Split-Editors im Kassenbuch-Detail (ledger-pafkp, ADR 0006, ADR 0049): die Summe
/// aus Gesamtbetrag und Rest, die Zeile mit Ziel, Betrag, "Rest" und Memo, "Zeile hinzufügen" und
/// die Fußleiste. Rein darstellend: Texte und Zustände liefert der Aufrufer, die Rechnung (Rest,
/// Speichern-Sperre) steht in `Components.Ledger.SplitEditor`. Das Ziel einer Zeile wählt der
/// gemeinsame `CategoryPicker` (Kategorie oder Konto, design-system-bym4m).
module Client.DesignSystem.SplitEditor

open Feliz
open Client.DesignSystem
open Client.DesignSystem.Tokens

// ============================================
// Reine Klassenfunktionen und Texte
// ============================================

/// Betrag im Editor-Kopf und in der Restzeile (ADR 0049): neutral, Mono, 500. `warn` färbt nur
/// den Text rot (der Rest ist nicht null, Speichern ist gesperrt).
let amountClass (warn: bool) : string =
    let color = if warn then "text-neon-red" else "text-text-primary"
    $"font-mono font-medium tabular-nums whitespace-nowrap {color}"

let totalLabel = "Gesamt"
let restLabel = "Rest"
let balancedLabel = "Stimmt"
let addLineLabel = "Zeile hinzufügen"
let cashbackLabel = "Barabhebung"
let cashbackTitle = "Zeile für einen Übertrag aufs Bar-Konto"
let restButtonLabel = "Rest"
let restButtonTitle = "Restbetrag einsetzen"
let removeLineLabel = "Zeile entfernen"
let memoPlaceholder = "Memo"
let amountPlaceholder = "0,00"
let saveLabel = "Speichern"
let cancelLabel = "Abbrechen"

/// Die Beschriftung der Restzeile: "Stimmt" bei null, sonst "Rest".
let restRowLabel (balanced: bool) : string = if balanced then balancedLabel else restLabel

// ============================================
// Summe
// ============================================

type SummaryProps = {
    TotalText: string
    RestText: string
    /// Der Rest ist nicht null: Warnfarbe auf dem Betrag
    RestWarn: bool
    Balanced: bool
}

let private summaryRow (testId: string) (label: string) (value: string) (warn: bool) : ReactElement =
    Html.div [
        prop.className "flex items-center justify-between gap-3"
        prop.children [
            Html.span [ prop.className $"{FontSizes.sm} {Colors.textMuted}"; prop.text label ]
            Html.span [
                prop.custom ("data-testid", testId)
                prop.className $"{FontSizes.sm} {amountClass warn}"
                prop.text value
            ]
        ]
    ]

/// Gesamtbetrag und Rest über den Zeilen.
let summary (props: SummaryProps) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "split-summary")
        prop.className $"flex flex-col gap-1 px-4 py-3 border-b {Borders.subtle}"
        prop.children [
            summaryRow "split-total" totalLabel props.TotalText false
            summaryRow "split-rest" (restRowLabel props.Balanced) props.RestText props.RestWarn
        ]
    ]

// ============================================
// Zeile
// ============================================

type LineProps = {
    Index: int
    /// Beschriftung des Ziels (Kategorie, "→ Konto" oder die Aufforderung zu wählen)
    TargetLabel: string
    TargetChosen: bool
    AmountText: string
    Memo: string
    Disabled: bool
    CanRemove: bool
    OnPickTarget: unit -> unit
    OnAmount: string -> unit
    OnMemo: string -> unit
    OnRest: unit -> unit
    OnRemove: unit -> unit
}

let private targetButton (props: LineProps) : ReactElement =
    let color = if props.TargetChosen then Colors.textPrimary else Colors.textMuted
    Html.button [
        prop.type'.button
        prop.custom ("data-testid", $"split-target-{props.Index}")
        prop.disabled props.Disabled
        prop.className (
            $"flex min-h-11 w-full min-w-0 items-center rounded-lg border {Borders.default'} {Backgrounds.elevated} " +
            $"px-3 py-2 text-left {FontSizes.sm} {color} truncate transition-colors touch-manipulation " +
            "hover:border-neon-teal disabled:opacity-50 disabled:cursor-not-allowed"
        )
        prop.onClick (fun _ -> props.OnPickTarget ())
        prop.text props.TargetLabel
    ]

/// Eine Zeile: das Ziel-Feld (Kategorie oder Konto), Betrag mit "Rest" und Entfernen,
/// darunter das Memo.
let line (props: LineProps) : ReactElement =
    Html.div [
        prop.key (string props.Index)
        prop.custom ("data-testid", $"split-line-{props.Index}")
        prop.className $"flex flex-col gap-2 px-4 py-3 border-b {Borders.subtle}"
        prop.children [
            targetButton props
            Html.div [
                prop.className "flex items-center gap-2"
                prop.children [
                    Html.input [
                        prop.type' "text"
                        prop.inputMode.decimal
                        prop.custom ("data-testid", $"split-amount-{props.Index}")
                        prop.ariaLabel "Betrag"
                        prop.className "input-field text-base md:text-sm px-3 py-2 min-h-[36px] min-w-0 flex-1 font-mono tabular-nums"
                        prop.placeholder amountPlaceholder
                        prop.value props.AmountText
                        prop.disabled props.Disabled
                        prop.onChange props.OnAmount
                    ]
                    Html.span [ prop.className $"{FontSizes.sm} {Colors.textMuted}"; prop.text "€" ]
                    Button.view
                        { Button.defaultProps with
                            Text = restButtonLabel
                            Variant = Button.Secondary
                            Size = Button.Small
                            IsDisabled = props.Disabled
                            OnClick = props.OnRest
                            Title = Some restButtonTitle
                            ClassName = Some "shrink-0" }
                    Button.view
                        { Button.defaultProps with
                            Variant = Button.Ghost
                            Size = Button.Small
                            IsDisabled = props.Disabled || not props.CanRemove
                            Icon = Some (Icons.x Icons.SM Icons.Default)
                            OnClick = props.OnRemove
                            Title = Some removeLineLabel
                            ClassName = Some "shrink-0" }
                ]
            ]
            Html.input [
                prop.type' "text"
                prop.custom ("data-testid", $"split-memo-{props.Index}")
                prop.ariaLabel "Memo"
                prop.className "input-field text-base md:text-sm px-3 py-2 min-h-[36px]"
                prop.placeholder memoPlaceholder
                prop.value props.Memo
                prop.disabled props.Disabled
                prop.onChange props.OnMemo
            ]
        ]
    ]

// ============================================
// Zeile hinzufügen und Fußleiste
// ============================================

/// Die Zeile unter den Editor-Zeilen im Kassenbuch: "Zeile hinzufügen" und "Barabhebung" (ledger-s8t3s),
/// beide sekundär. "Barabhebung" belegt die erste Zeile ohne Ziel mit einem Übertrag aufs Bar-Konto
/// oder hängt eine an; sie bleibt aktiv, solange nicht gespeichert wird.
let lineActions (addEnabled: bool) (onAdd: unit -> unit) (cashbackEnabled: bool) (onCashback: unit -> unit) : ReactElement =
    Html.div [
        prop.className "px-4 py-3 flex flex-wrap items-center gap-2"
        prop.children [
            Button.view
                { Button.defaultProps with
                    Text = addLineLabel
                    Variant = Button.Secondary
                    Size = Button.Small
                    IsDisabled = not addEnabled
                    Icon = Some (Icons.plus Icons.SM Icons.NeonTeal)
                    OnClick = onAdd }
            Button.view
                { Button.defaultProps with
                    Text = cashbackLabel
                    Variant = Button.Secondary
                    Size = Button.Small
                    IsDisabled = not cashbackEnabled
                    Title = Some cashbackTitle
                    OnClick = onCashback }
        ]
    ]

/// "Speichern": die einzige primäre Aktion des Editors. `saving` zeigt den Ladezustand, solange
/// der Aufruf läuft; ohne speicherbaren Entwurf ist der Knopf gesperrt. `fullWidth` für das Handy.
let saveButton (fullWidth: bool) (canSave: bool) (saving: bool) (onSave: unit -> unit) : ReactElement =
    Button.view
        { Button.defaultProps with
            Text = saveLabel
            Variant = Button.Primary
            IsLoading = saving
            IsDisabled = not canSave
            FullWidth = fullWidth
            OnClick = onSave }

let cancelButton (saving: bool) (onCancel: unit -> unit) : ReactElement =
    Button.view
        { Button.defaultProps with
            Text = cancelLabel
            Variant = Button.Secondary
            IsDisabled = saving
            OnClick = onCancel }

/// Die Fußleiste im Seitenpanel (PC): "Abbrechen" und "Speichern", haftend am unteren Rand des
/// scrollenden Bereichs (der Aufrufer stellt sie als letztes Kind hinein). Am Handy steht nur
/// `saveButton` im Footer des Sheets, der Rückweg läuft über das Sheet.
let footer (canSave: bool) (saving: bool) (onCancel: unit -> unit) (onSave: unit -> unit) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "split-footer")
        prop.className $"sticky bottom-0 flex items-center justify-end gap-3 border-t {Borders.default'} {Backgrounds.dark} px-4 py-3"
        prop.children [ cancelButton saving onCancel; saveButton false canSave saving onSave ]
    ]
