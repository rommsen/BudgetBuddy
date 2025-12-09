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

let private getGlowClass amount glow =
    match glow with
    | NoGlow -> ""
    | GlowPositive when amount >= 0m -> "text-glow-green"
    | GlowAll when amount >= 0m -> "text-glow-green"
    | GlowAll when amount < 0m -> "[text-shadow:0_0_10px_theme(colors.neon-red),0_0_20px_rgba(255,59,92,0.5)]"
    | _ -> ""

let private formatAmount showSign showCurrency amount currency =
    let sign = if showSign && amount >= 0m then "+" else ""
    let currencyPart = if showCurrency then $" {currency}" else ""
    // Note: F# format specifier :F2 doesn't work correctly with Fable transpilation
    // Using explicit rounding and ToString instead
    let absAmount = abs amount
    let formattedAmount = System.Math.Round(float absAmount, 2).ToString("0.00")
    let signPrefix = if amount < 0m then "-" else sign
    $"{signPrefix}{formattedAmount}{currencyPart}"

/// Create a money display with the specified props
let view (props: MoneyProps) =
    let sizeClass = sizeToClass props.Size
    let colorClass = getColorClass props.Amount
    let glowClass = getGlowClass props.Amount props.Glow
    let extraClass = props.ClassName |> Option.defaultValue ""
    let formatted = formatAmount props.ShowSign props.ShowCurrency props.Amount props.Currency

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
                prop.className "text-xs md:text-sm font-medium text-base-content/50 uppercase tracking-wider"
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
                prop.className "text-sm text-base-content/70"
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
                prop.className "text-xs text-base-content/50 uppercase tracking-wider"
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
                    Html.span [ prop.className "text-base-content/70"; prop.text "Income" ]
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
                    Html.span [ prop.className "text-base-content/70"; prop.text "Expenses" ]
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
                prop.className "border-t border-white/10 pt-2 flex items-center justify-between"
                prop.children [
                    Html.span [ prop.className "text-base-content font-medium"; prop.text "Net" ]
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
