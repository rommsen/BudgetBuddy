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

/// Whether to show glow effect for positive amounts
type GlowStyle =
    | NoGlow
    | GlowPositive      // Only positive amounts glow
    | GlowAll           // Both positive and negative glow

// ============================================
// Money Props
// ============================================

type MoneyProps = {
    Amount: decimal
    Currency: string
    Size: MoneySize
    Glow: GlowStyle
    ShowSign: bool          // Show +/- sign
    ShowCurrency: bool      // Show currency code
    ClassName: string option
}

let defaultProps = {
    Amount = 0m
    Currency = "EUR"
    Size = Medium
    Glow = GlowPositive
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
    if amount >= 0m then "text-neon-green"
    else "text-neon-red"

// design-system-thtpp: "text-glow-green" war keine echte Tailwind-Utility und
// in styles.css nicht definiert (stiller Ausfall wie die DaisyUI-Klassen aus
// ADR 0031): anders als "shadow-glow-*" gibt es für "text-*" kein Theme-Token,
// das Tailwind automatisch in eine Utility auflöst. Der negative Fall (Zeile
// unten, GlowAll bei negativem Betrag) nutzte bereits die funktionierende
// Arbitrary-Property-Syntax; der positive Fall folgt jetzt demselben Muster,
// mit denselben rgba-Werten wie --shadow-glow-green in styles.css.
let private getGlowClass amount glow =
    match glow with
    | NoGlow -> ""
    | GlowPositive when amount >= 0m -> "[text-shadow:0_0_10px_theme(colors.neon-green),0_0_20px_rgba(0,255,136,0.5)]"
    | GlowAll when amount >= 0m -> "[text-shadow:0_0_10px_theme(colors.neon-green),0_0_20px_rgba(0,255,136,0.5)]"
    | GlowAll when amount < 0m -> "[text-shadow:0_0_10px_theme(colors.neon-red),0_0_20px_rgba(255,59,92,0.5)]"
    | _ -> ""

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
    let totalCents = System.Math.Round(abs amount * 100m, 0) |> int64
    let integerPart = string (totalCents / 100L)
    let decimalPart = (string (totalCents % 100L)).PadLeft(2, '0')
    let formattedAmount = $"{groupThousands integerPart},{decimalPart}"
    let currencyPart = if showCurrency then $" {currencySymbol currency}" else ""
    $"{signPrefix}{formattedAmount}{currencyPart}"

/// Create a money display with the specified props
let view (props: MoneyProps) =
    let sizeClass = sizeToClass props.Size
    let colorClass = getColorClass props.Amount
    let glowClass = getGlowClass props.Amount props.Glow
    let extraClass = props.ClassName |> Option.defaultValue ""
    let formatted = formatAmountText props.ShowSign props.ShowCurrency props.Amount props.Currency

    Html.span [
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap {sizeClass} {colorClass} {glowClass} {extraClass}"
        prop.text formatted
    ]

/// Create from Money domain type
let fromMoney (money: Money) (glow: GlowStyle) =
    view {
        defaultProps with
            Amount = money.Amount
            Currency = money.Currency
            Glow = glow
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

/// Large featured money display with glow
let large amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Large
            Glow = GlowPositive
    }

/// Hero-sized money display
let hero amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Hero
            Glow = GlowPositive
    }

/// Money display without sign prefix
let noSign amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            ShowSign = false
    }

/// Money display without glow effect
let noGlow amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Glow = NoGlow
    }

/// Money display from domain Money type
let money (m: Money) =
    fromMoney m GlowPositive

/// Compact "Available" display for the category picker: green when positive,
/// red when negative, muted when zero. Mono, no glow, no leading "+" — the
/// colour carries the sign. Right-alignment is left to the caller's layout.
let available (amount: decimal) (currency: string) =
    let colorClass =
        if amount > 0m then "text-neon-green"
        elif amount < 0m then "text-neon-red"
        else "text-text-muted"
    Html.span [
        prop.className $"font-mono font-semibold tabular-nums whitespace-nowrap text-sm md:text-base {colorClass}"
        prop.text (formatAmountText false true amount currency)
    ]

/// Money display from domain Money type with glow control
let moneyWithGlow (m: Money) showGlow =
    fromMoney m (if showGlow then GlowPositive else NoGlow)

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
                            Glow = NoGlow
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
                            Glow = NoGlow
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
                            Glow = GlowPositive
                    }
                ]
            ]
        ]
    ]

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
            Glow = NoGlow
    }

/// Format amount for transaction lists with currency
let transactionWithCurrency amount currency =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Small
            Glow = NoGlow
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

/// Format an account balance: same colour semantics as a transaction (green/red),
/// but without the leading `+` on positive balances — the Kontostände-Leiste never
/// showed one, and the mockup (`1.9-pc-ausgangszustand-zeile-markiert.png`) doesn't
/// either. Used both by the mobile card strip and the PC list row below, so the
/// balance keeps its colour and digit font wherever it appears (ledger-x0yga).
let accountBalance (amount: decimal) (currency: string) =
    view {
        defaultProps with
            Amount = amount
            Currency = currency
            Size = Small
            ShowSign = false
            Glow = NoGlow
    }

/// Compact list row for the PC Kontostände-Liste (ledger-x0yga): account name on the
/// left, colour-coded balance on the right, no card chrome. Replaces the tall
/// gradient-topped `Stats` card the PC view used before — that shape is right for
/// KPI tiles, not for a scannable list of accounts. Active account gets the same
/// teal highlight as the desktop nav's active tab (`Navigation.desktopNavItem`).
let accountListRow (label: string) (amount: decimal) (currency: string) (isActive: bool) (onClick: unit -> unit) =
    Html.button [
        prop.onClick (fun _ -> onClick ())
        prop.className (
            "w-full flex items-center justify-between gap-3 rounded-lg px-3 py-2 text-left transition-colors border-l-2 "
            // Aktiv: erhoehte Flaeche + Teal-Akzentlinie links, dieselbe Kombination
            // wie `Table.row`s `IsSelected`-Zeile (`bg-neon-teal/10 border-l-2
            // border-l-neon-teal`) — Tokens statt der Hex-Literale aus dem
            // Claude-Design-Handoff (`#1a1a36` / `inset 2px 0 0 #00d4aa`).
            + if isActive then
                  $"{Tokens.Backgrounds.surface} border-l-neon-teal"
              else
                  // `hover:bg-surface-hover` steht als Literal (nicht interpoliert):
                  // Tailwind scannt den Quelltext nach ganzen Klassennamen, eine
                  // zusammengesetzte Variante faellt beim Scan durch (vgl.
                  // `Components.Ledger.View.entryRow`).
                  "border-l-transparent hover:bg-surface-hover"
        )
        prop.children [
            Html.span [
                prop.className (
                    "truncate text-sm "
                    + if isActive then
                          $"font-semibold {Tokens.Colors.textPrimary}"
                      else
                          Tokens.Colors.textSecondary
                )
                prop.text label
            ]
            accountBalance amount currency
        ]
    ]
