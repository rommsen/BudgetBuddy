module Client.DesignSystem.Money

open Feliz
open Client.DesignSystem.Tokens
open Shared.Domain

// ============================================
// Money Display Types
// ============================================

/// Size variants for money display
type MoneySize =
    | Small     // Inline with small text
    | Medium    // Body text size
    | Large     // Featured/highlighted amounts
    | Hero      // Large hero displays

// ============================================
// Money Props
// ============================================

type MoneyProps = {
    Amount: decimal
    Currency: string
    Size: MoneySize
    ShowSign: bool          // Show +/- sign
    ShowCurrency: bool      // Show currency code
    ClassName: string option
}

let defaultProps = {
    Amount = 0m
    Currency = "EUR"
    Size = Medium
    ShowSign = true
    ShowCurrency = true
    ClassName = None
}

// ============================================
// Money Implementation
// ============================================

let private sizeToClass = function
    | Small -> "text-sm md:text-base"
    | Medium -> "text-base md:text-lg"
    | Large -> "text-lg md:text-xl"
    | Hero -> "text-2xl md:text-4xl"

let private getColorClass amount =
    if amount >= 0m then "text-success"
    else "text-error"

// Geld trägt keinen Glow: Glow gibt es nur an der Hauptaktion und an Rückmeldungen (ADR 0087 Punkt 6).

/// Groups the digits of a (non-negative, integer-only) numeric string with `.`
/// every three digits from the right (`21388` -> `21.388`). Deliberately written
/// as plain string/substring arithmetic rather than `CultureInfo` or `Seq.chunkBySize`:
/// .NET's culture-aware number formatting (`"N2"` with `CultureInfo("de-DE")`) does
/// not transpile correctly via Fable — it silently falls back to English grouping
/// regardless of the culture passed in (ledger-x0yga). This is the one place in the
/// app that turns a raw decimal into German-grouped digits; every money display goes
/// through `formatAmountText` below.
let private groupThousands (digits: string) : string =
    let len = digits.Length
    if len <= 3 then
        digits
    else
        let firstGroupLen =
            match len % 3 with
            | 0 -> 3
            | r -> r
        let firstGroup = digits.Substring(0, firstGroupLen)
        let rest = [ for i in firstGroupLen .. 3 .. len - 1 -> digits.Substring(i, 3) ]
        String.concat "." (firstGroup :: rest)

/// Currency code to display symbol. Only EUR is in use today; anything else falls
/// back to the code itself rather than guessing a symbol.
/// Anzahl mit Tausenderpunkt ("12.116"). Reine Funktion; bewusst kein
/// `ToString("N0")`, weil Fable die Kultur ignoriert.
let formatCount (n: int) : string =
    if n < 0 then "-" + groupThousands (string (-(int64 n))) else groupThousands (string n)

let private currencySymbol =
    function
    | "EUR" -> "€"
    | other -> other

/// Formats a signed amount as German-style text (`-48,12 €`, `21.388,85 €`,
/// `0,00 €`): thousands separated with `.`, decimals with `,`, always two decimal
/// places, currency as a trailing symbol. Pure and covered directly by
/// `MoneyFormatTests`; every convenience function below (and every other view that
/// needs plain formatted text, e.g. account balances) builds on this single function.
let formatAmountText (showSign: bool) (showCurrency: bool) (amount: decimal) (currency: string) : string =
    let sign = if showSign && amount >= 0m then "+" else ""
    let signPrefix = if amount < 0m then "-" else sign
    // Deliberately no `.ToString("0.00")`/`"F2"` here: both are format strings whose
    // '.' and ',' are placeholders for the *current culture's* separators, not
    // literal characters — on a machine or CI runner whose OS locale already uses
    // ',' as the decimal separator, `ToString("0.00")` silently returns a
    // comma-decimal string (e.g. `"21388,85"`), which then can't be split on '.'
    // and comes out mangled (caught by `MoneyFormatTests` while writing this fix).
    // Working in whole cents via plain integer arithmetic sidesteps every
    // culture-dependent formatting path, in both the Fable build and `dotnet test`.
    // Die Cent rundet `Shared.Domain.roundHalfUp` kaufmännisch: ein genauer halber Cent
    // geht weg von null (12,345 € wird 12,35 €). Die eingebaute Rundung auf decimal ohne
    // Modus rundete ihn unter .NET und im Fable-Kompilat zur geraden Zahl (infrastructure-k84s2).
    let totalCents = Shared.Domain.roundHalfUp (abs amount * 100m) |> int64
    let integerPart = string (totalCents / 100L)
    let decimalPart = (string (totalCents % 100L)).PadLeft(2, '0')
    let formattedAmount = $"{groupThousands integerPart},{decimalPart}"
    let currencyPart = if showCurrency then $" {currencySymbol currency}" else ""
    $"{signPrefix}{formattedAmount}{currencyPart}"

/// Create a money display with the specified props
let view (props: MoneyProps) =
    let sizeClass = sizeToClass props.Size
    let colorClass = getColorClass props.Amount
    let extraClass = props.ClassName |> Option.defaultValue ""
    let formatted = formatAmountText props.ShowSign props.ShowCurrency props.Amount props.Currency

    Html.span [
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap {sizeClass} {colorClass} {extraClass}"
        prop.text formatted
    ]

/// Create from Money domain type
let fromMoney (money: Money) =
    view {
        defaultProps with
            Amount = money.Amount
            Currency = money.Currency
    }

// ============================================
// Convenience Functions
// ============================================

/// Simple money display with default settings
let simple amount currency =
    view { defaultProps with Amount = amount; Currency = currency }

/// Money display without currency symbol
let amountOnly amount =
    view {
        defaultProps with
            Amount = amount
            ShowCurrency = false
    }

/// Small inline money display
let small amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Small
    }

/// Large featured money display
let large amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Large
    }

/// Hero-sized money display
let hero amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Hero
    }

/// Money display without sign prefix
let noSign amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            ShowSign = false
    }

/// Money display from domain Money type
let money (m: Money) =
    fromMoney m

/// Klassen des Verfügbar-Betrags in einer Auswahlzeile (ADR 0049): Mono 500,
/// `tabular-nums`, Größe wie der Zeilenname, kein Glow. Positiv neutral,
/// null gedämpft, nur negativ Rot.
let availableClass (amount: decimal) : string =
    let color =
        if amount > 0m then Colors.textPrimary
        elif amount < 0m then "text-error"
        else Colors.textMuted
    $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {color}"

/// Verfügbar-Betrag für die Kategorie-Auswahlen. Kein führendes "+", die Rechtsbündigkeit
/// liegt beim Aufrufer (siehe `PickerParts.row`).
let available (amount: decimal) (currency: string) =
    Html.span [
        prop.className (availableClass amount)
        prop.text (formatAmountText false true amount currency)
    ]

/// "Verfügbar danach": fett, rot unter 0, grün bei genau 0, sonst neutral.
let afterAmount (amount: decimal) (currency: string) =
    let color =
        if amount < 0m then Colors.error
        elif amount = 0m then Colors.success
        else Colors.textPrimary
    Html.span [
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap {ListRoles.primarySize} {color}"
        prop.text (formatAmountText false true amount currency)
    ]

// ============================================
// Money in Context
// ============================================

/// Money with label (for stat-like displays)
let withLabel (label: string) amount currency =
    Html.div [
        prop.className "flex flex-col gap-1"
        prop.children [
            Html.span [
                prop.className "text-xs md:text-sm font-medium text-text-muted/70 uppercase tracking-wider"
                prop.text label
            ]
            large amount currency
        ]
    ]

/// Money with inline label
let withInlineLabel (label: string) amount currency =
    Html.div [
        prop.className "flex items-center gap-2"
        prop.children [
            Html.span [
                prop.className "text-sm text-text-secondary"
                prop.text label
            ]
            simple amount currency
        ]
    ]

/// Balance display with prominent styling
let balance amount currency =
    Html.div [
        prop.className "flex flex-col items-center gap-1"
        prop.children [
            Html.span [
                prop.className "text-xs text-text-muted/70 uppercase tracking-wider"
                prop.text "Balance"
            ]
            hero amount currency
        ]
    ]

/// Net change display (income - expenses)
let netChange income expenses currency =
    let net = income - expenses
    Html.div [
        prop.className "flex flex-col gap-2"
        prop.children [
            Html.div [
                prop.className "flex items-center justify-between text-sm"
                prop.children [
                    Html.span [ prop.className "text-text-secondary"; prop.text "Income" ]
                    view {
                        defaultProps with
                            Amount = income
                            Currency = currency
                            Size = Small
                    }
                ]
            ]
            Html.div [
                prop.className "flex items-center justify-between text-sm"
                prop.children [
                    Html.span [ prop.className "text-text-secondary"; prop.text "Expenses" ]
                    view {
                        defaultProps with
                            Amount = -expenses
                            Currency = currency
                            Size = Small
                            ShowSign = false
                    }
                ]
            ]
            Html.div [
                prop.className "border-t border-border-default pt-2 flex items-center justify-between"
                prop.children [
                    Html.span [ prop.className "text-text-primary font-medium"; prop.text "Net" ]
                    view {
                        defaultProps with
                            Amount = net
                            Currency = currency
                            Size = Medium
                    }
                ]
            ]
        ]
    ]

// ============================================
// Ruhige Listen (ADR 0049)
// ============================================

/// Klassen eines Betrags in einer Listenzeile: Mono 500, `tabular-nums`, kein Glow.
/// Jeder Betrag neutral in `text-primary`, auch ein Eingang: der Akzent heißt Hauptaktion und Auswahl
/// (ADR 0087 Punkt 2), die Richtung sagt das Vorzeichen.
let listAmountClass (_amount: decimal) : string =
    $"font-mono font-medium tabular-nums whitespace-nowrap {ListRoles.primarySize} {Colors.textPrimary}"

/// Betrag für Listenzeilen und Tabellenzellen (ADR 0049).
let listAmount (amount: decimal) (currency: string) =
    Html.span [
        prop.className (listAmountClass amount)
        prop.text (formatAmountText (amount > 0m) true amount currency)
    ]

/// Klassen einer Bewegung in einer ruhigen Liste (Historie der Budget-Seite, budgeting-v6m3z): wie
/// `listAmountClass`, neutral in `text-primary`. Das Vorzeichen sagt die Richtung.
let signedListAmountClass = listAmountClass 0m

/// Der Text einer Bewegung mit Vorzeichen ("+20,00 €", "-20,00 €").
let signedListAmountText (amount: decimal) (currency: string) : string = formatAmountText true true amount currency

/// Eine Bewegung mit Vorzeichen in einer ruhigen Liste, neutral (ADR 0049).
let signedListAmount (amount: decimal) (currency: string) =
    Html.span [ prop.className signedListAmountClass; prop.text (signedListAmountText amount currency) ]

// ============================================
// Transaction Amount Display
// ============================================

/// Format amount for transaction lists (compact, no currency by default)
let transaction amount =
    view {
        defaultProps with
            Amount = amount
            Size = Small
            ShowCurrency = false
    }

/// Format amount for transaction lists with currency
let transactionWithCurrency amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Small
    }

/// Betrag einer in YNAB gelöschten Buchung (ledger-p3n8k): gedämpft und
/// durchgestrichen statt in der roten/grünen Betrags-Farbe (so ist ohne Lesen
/// des Kennzeichens klar, dass der Betrag nicht mehr mitzählt). Eigene Funktion statt
/// eines Flags an `view`: die Rot/Grün-Farbsemantik (`getColorClass`) soll hier gar
/// nicht erst greifen.
let transactionDeleted (amount: decimal) (currency: string) =
    Html.span [
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap text-sm md:text-base line-through {Colors.textMuted}"
        prop.text (formatAmountText false true amount currency)
    ]

// ============================================
// Account Balances (Kontostände-Leiste, ledger-b1xwe / ledger-x0yga)
// ============================================

/// Farbklasse eines Kontostands (ADR 0049): neutral, aktiv `text-primary`, inaktiv
/// `text-secondary`; nur ein negativer Stand ist Red.
let accountBalanceClass (isActive: bool) (amount: decimal) : string =
    let color =
        if amount < 0m then Colors.error
        elif isActive then Colors.textPrimary
        else Colors.textSecondary
    $"font-mono font-semibold tabular-nums whitespace-nowrap text-sm md:text-base {color}"

/// Kontostand für eine Zeile, die aktiv sein kann (`accountListRow`, ledger-x0yga).
let accountBalanceFor (isActive: bool) (amount: decimal) (currency: string) =
    Html.span [
        prop.className (accountBalanceClass isActive amount)
        prop.text (formatAmountText false true amount currency)
    ]

/// Kontostand ohne führendes `+`, ruhig (ADR 0049): neutral, nur negativ Red. Für die
/// Handy-Karte (dort ist die Karte selbst aktiv oder nicht, der Betrag bleibt `text-primary`).
let accountBalance (amount: decimal) (currency: string) =
    accountBalanceFor true amount currency

/// Compact list row for the PC Kontostände-Liste (ledger-x0yga): account name on the
/// left, colour-coded balance on the right, no card chrome. Replaces the tall
/// gradient-topped `Stats` card the PC view used before — that shape is right for
/// KPI tiles, not for a scannable list of accounts. Active account gets the same
/// Akzent highlight as the desktop nav's active tab (`Navigation.desktopNavItem`).
let accountListRowWith (label: string) (value: ReactElement) (isActive: bool) (onClick: unit -> unit) =
    Html.button [
        prop.onClick (fun _ -> onClick ())
        prop.className (
            "w-full flex items-center justify-between gap-3 rounded-lg px-3 py-2 text-left transition-colors border-l-2 "
            // Aktiv: erhoehte Flaeche + Akzentlinie links, dieselbe Kombination
            // wie `Table.row`s `IsSelected`-Zeile (`bg-accent/10 border-l-2
            // border-l-accent`) — Tokens statt der Hex-Literale aus dem
            // Claude-Design-Handoff (`#1a1a36` / `inset 2px 0 0` in Akzent).
            + if isActive then
                  $"{Tokens.Backgrounds.surface} border-l-accent"
              else
                  // `hover:bg-surface-hover` steht als Literal (nicht interpoliert):
                  // Tailwind scannt den Quelltext nach ganzen Klassennamen, eine
                  // zusammengesetzte Variante faellt beim Scan durch (vgl.
                  // `Components.Ledger.View.entryRow`).
                  "border-l-transparent hover:bg-surface-hover"
        )
        prop.children [
            Truncate.text
                (if isActive then $"text-sm font-semibold {Tokens.Colors.textPrimary}" else $"text-sm {Tokens.Colors.textSecondary}")
                label
            value
        ]
    ]

let accountListRow (label: string) (amount: decimal) (currency: string) (isActive: bool) (onClick: unit -> unit) =
    accountListRowWith label (accountBalanceFor isActive amount currency) isActive onClick

/// Strich an der Stelle eines Kontostands, der nicht geladen werden konnte (ledger-vq2g8):
/// gedämpft, in der Ziffernschrift des Betrags, damit die Zeile ihre Form behält.
/// Der Strich ist für Screenreader verborgen, sie lesen stattdessen den Text daneben.
let balanceUnavailable () =
    Html.span [
        prop.className $"font-mono tabular-nums whitespace-nowrap text-sm md:text-base {Colors.textMuted}"
        prop.children [
            Html.span [ prop.ariaHidden true; prop.text "—" ]
            Html.span [ prop.className "sr-only"; prop.text "Kontostand nicht geladen" ]
        ]
    ]
