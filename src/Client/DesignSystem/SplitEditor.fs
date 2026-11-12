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

/// Der Stand des Rests (ledger-ng110): noch offen, zu viel verteilt oder ausgeglichen.
type RestState =
    /// Rest über null: es fehlt noch etwas, kein Fehler.
    | RestOpen
    /// Rest unter null: die Zeilen sind größer als der Gesamtbetrag.
    | RestOver
    | RestBalanced

/// Betrag im Editor-Kopf (ADR 0049): neutral, Mono, 500.
let amountClass : string = "font-mono font-medium tabular-nums whitespace-nowrap text-text-primary"

/// Betrag in der Restzeile: offen gelb (`Colors.attention`, braucht dich, kein Fehler, mit dem Wort „Rest“
/// daneben), zu viel verteilt rot (Fehler), ausgeglichen neutral (Styleguide §2, ADR 0087 Punkt 3 und 4).
let restAmountClass (rest: RestState) : string =
    let color =
        match rest with
        | RestOpen -> Colors.attention
        | RestOver -> "text-error"
        | RestBalanced -> "text-text-primary"
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
let restRowLabel (rest: RestState) : string =
    match rest with
    | RestBalanced -> balancedLabel
    | RestOpen | RestOver -> restLabel

// ============================================
// Summe
// ============================================

type SummaryProps = {
    TotalText: string
    RestText: string
    /// Offen, zu viel verteilt oder ausgeglichen: Farbe und Beschriftung der Restzeile
    Rest: RestState
}

/// Eine Zeile der Summe; `rest` färbt den Betrag nach dem Stand des Rests, ohne ist er neutral (Gesamt).
let private summaryRow (testId: string) (label: string) (value: string) (rest: RestState option) : ReactElement =
    Html.div [
        prop.className "flex items-center justify-between gap-3"
        prop.children [
            Html.span [ prop.className $"{FontSizes.sm} {Colors.textMuted}"; prop.text label ]
            Html.span [
                prop.custom ("data-testid", testId)
                match rest with
                | Some state -> prop.className $"{FontSizes.sm} {restAmountClass state}"
                | None -> prop.className $"{FontSizes.sm} {amountClass}"
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
            summaryRow "split-total" totalLabel props.TotalText None
            summaryRow "split-rest" (restRowLabel props.Rest) props.RestText (Some props.Rest)
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
    /// Der Betrag ist nicht leer und nicht lesbar: rote Ziffern und `aria-invalid`
    AmountInvalid: bool
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
            $"flex {TouchTargets.mobileMinHeight} md:min-h-11 w-full min-w-0 items-center rounded-lg border {Borders.default'} {Backgrounds.elevated} " +
            $"px-3 py-2 text-left {FontSizes.sm} {color} truncate transition-colors touch-manipulation " +
            "hover:border-accent disabled:opacity-50 disabled:cursor-not-allowed"
        )
        prop.onClick (fun _ -> props.OnPickTarget ())
        // Der Knopf kürzt das Ziel (`truncate`), der title trägt es ganz.
        prop.title props.TargetLabel
        prop.text props.TargetLabel
    ]

/// Eine Zeile: das Ziel-Feld (Kategorie oder Konto), Betrag mit "Rest" und Entfernen,
/// darunter das Memo. Betrag und Memo sind `Input.text` (Small: 16 px am Handy, 14 px ab md), der
/// Betrag als Betragsfeld mit Fehlerzustand (`Input.amountFieldClass`).
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
                    Html.div [
                        prop.custom ("data-testid", $"split-amount-{props.Index}")
                        prop.className "min-w-0 flex-1"
                        prop.children [
                            Input.text
                                { Input.textInputDefaults with
                                    Value = props.AmountText
                                    OnChange = props.OnAmount
                                    Placeholder = amountPlaceholder
                                    Size = Input.Small
                                    Disabled = props.Disabled
                                    InputMode = Some "decimal"
                                    AriaLabel = Some "Betrag"
                                    AriaInvalid = props.AmountInvalid
                                    ClassName = Some (Input.amountFieldClass false props.AmountInvalid) }
                        ]
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
                    Button.removeIconWhen
                        (props.Disabled || not props.CanRemove)
                        $"split-remove-{props.Index}"
                        removeLineLabel
                        props.OnRemove
                ]
            ]
            Html.div [
                prop.custom ("data-testid", $"split-memo-{props.Index}")
                prop.children [
                    Input.text
                        { Input.textInputDefaults with
                            Value = props.Memo
                            OnChange = props.OnMemo
                            Placeholder = memoPlaceholder
                            Size = Input.Small
                            Disabled = props.Disabled
                            AriaLabel = Some "Memo"
                            ClassName = Some "w-full" }
                ]
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
                    Icon = Some (Icons.plus Icons.SM Icons.Accent)
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

/// Ein abgelehntes Speichern bei offenem Editor (ledger-ng110, §7.13): `ErrorDisplay.inline'` direkt über
/// "Speichern", im Fuß des Panels wie im Fuß des Sheets, damit es ohne Scrollen im Bild steht. Ohne Fehler nichts.
let saveError (message: string option) : ReactElement =
    match message with
    | Some text ->
        Html.div [
            prop.custom ("data-testid", "split-save-error")
            prop.children [ ErrorDisplay.inline' text ]
        ]
    | None -> Html.none

/// Die Fußleiste im Seitenpanel (PC): "Abbrechen" und "Speichern", haftend am unteren Rand des
/// scrollenden Bereichs (der Aufrufer stellt sie als letztes Kind hinein), darüber ein Speicherfehler.
/// Am Handy steht nur `saveButton` im Footer des Sheets, der Rückweg läuft über das Sheet.
let footer (error: string option) (canSave: bool) (saving: bool) (onCancel: unit -> unit) (onSave: unit -> unit) : ReactElement =
    Html.div [
        prop.custom ("data-testid", "split-footer")
        prop.className $"sticky bottom-0 flex flex-col gap-2 border-t {Borders.default'} {Backgrounds.dark} px-4 py-3"
        prop.children [
            saveError error
            Html.div [
                prop.className "flex items-center justify-end gap-3"
                prop.children [ cancelButton saving onCancel; saveButton false canSave saving onSave ]
            ]
        ]
    ]

/// Der Fuß des Sheets am Handy: "Speichern" über die volle Breite, darüber ein Speicherfehler.
let sheetFooter (error: string option) (canSave: bool) (saving: bool) (onSave: unit -> unit) : ReactElement =
    Html.div [
        prop.className "flex w-full flex-col gap-2"
        prop.children [ saveError error; saveButton true canSave saving onSave ]
    ]
