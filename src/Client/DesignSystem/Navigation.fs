module Client.DesignSystem.Navigation

open Feliz
open Client.DesignSystem.Tokens
open Client.DesignSystem.Icons

// ============================================
// Orte (Styleguide §7.4, ADR 0087 Punkt 9, design-system-k6kfw)
// ============================================
//
// Drei Orte: Übersicht, Kassenbuch, Budget. Quick Add steht als vierter Eintrag, bis `ledger-ea70s`
// (Ein Erfassen) es ablöst. Die Einstellungen liegen hinter dem Zahnrad.
//   - unter 768 px: Leiste unten (Prototyp `nav`, `.nav`)
//   - 768 bis 1023 px: schmale Rail links ohne Konten (Abweichung vom Prototyp, Roman 2026-11-11)
//   - ab 1024 px: Seitenleiste links mit Marke, Orten, Konten und Einstellungen (Prototyp `sidebar`, `.side`)

/// Die Orte der Navigation. Spiegelt `Types.Place`, steht hier, weil das DS vor `Types.fs` kompiliert.
type NavPage =
    | Overview
    | Ledger
    | Budget
    | QuickAdd

type NavItem = {
    Page: NavPage
    Label: string
    Icon: IconSize -> IconColor -> ReactElement
}

let navItems: NavItem list = [
    { Page = Overview; Label = "Übersicht"; Icon = home }
    { Page = Ledger; Label = "Kassenbuch"; Icon = book }
    { Page = Budget; Label = "Budget"; Icon = pie }
    { Page = QuickAdd; Label = "Quick Add"; Icon = plus }
]

/// Der Stand einer Kontozeile der Seitenleiste.
type AccountValue =
    | AccountAmount of amount: decimal * currency: string
    | AccountPending
    | AccountUnavailable

/// Eine Zeile im Abschnitt „Konten“ der Seitenleiste: „Alle Konten“ oder ein Konto.
type SidebarAccount = {
    Key: string
    Label: string
    Value: AccountValue
    /// Gewählt im Kassenbuch: Akzentstrich links und `surface-elevated`.
    IsActive: bool
    OnClick: unit -> unit
}

type Props = {
    /// Der markierte Ort, `None` auf Einstellungen und Styleguide.
    Current: NavPage option
    OnNavigate: NavPage -> unit
    /// Stehen die Einstellungen offen? Dann ist das Zahnrad in Rail und Seitenleiste markiert.
    SettingsActive: bool
    OnSettings: unit -> unit
    OnBrand: unit -> unit
    Accounts: SidebarAccount list
    /// Laden die Konten noch? Dann stehen Platzhalter statt der Zeilen.
    AccountsLoading: bool
    /// Die Leiste unten weicht am Handy der Auswahl-Leiste des Kassenbuchs (ledger-r1d2f).
    HideBottomBar: bool
}

let private isCurrent (props: Props) (page: NavPage) = props.Current = Some page

let private ariaCurrent (active: bool) =
    prop.custom ("aria-current", (if active then "page" else "false"))

// ============================================
// Leiste unten (Handy)
// ============================================

let private bottomItem (props: Props) (item: NavItem) =
    let active = isCurrent props item.Page
    Html.button [
        prop.type' "button"
        ariaCurrent active
        prop.className (
            "h-[60px] flex flex-col items-center justify-center gap-[3px] text-xs min-w-0 "
            + if active then Colors.accent else Colors.textMuted
        )
        prop.onClick (fun _ -> props.OnNavigate item.Page)
        prop.children [
            item.Icon Tab (if active then Accent else Muted)
            Html.span [ prop.className "truncate max-w-full px-1"; prop.text item.Label ]
        ]
    ]

let private bottomBarClass = $"grid grid-cols-4 border-t border-border-default {Backgrounds.dark}"

/// Leiste unten am Handy: gleich breite Spalten, Symbol 22 px über Beschriftung 12 px, 60 px plus Safe-Area.
let bottomBar (props: Props) =
    Html.nav [
        prop.ariaLabel "Orte"
        prop.className $"md:hidden fixed bottom-0 left-0 right-0 z-50 safe-area-pb {bottomBarClass}"
        prop.children [ for item in navItems do React.keyedFragment (item.Label, [ bottomItem props item ]) ]
    ]

// ============================================
// Rail (768 bis 1023 px)
// ============================================

/// Ein Eintrag der Rail. Beschriftung 10 px, damit „Kassenbuch“ in die 72 px passt; `showLabel = false` zeigt nur
/// das Symbol (das Zahnrad), der Name steht dann als `aria-label` und `title`.
let private railButton (active: bool) (label: string) (showLabel: bool) (icon: IconSize -> IconColor -> ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        ariaCurrent active
        if not showLabel then
            prop.ariaLabel label
            prop.title label
        prop.className (
            $"w-full flex flex-col items-center justify-center gap-1 py-2 rounded-[10px] {FontSizes.micro} transition-colors "
            + if active then $"{Backgrounds.surface} {Colors.accent}"
              else $"{Colors.textSecondary} hover:bg-surface-hover hover:text-text-primary"
        )
        prop.onClick (fun _ -> onClick ())
        prop.children [
            icon Tab (if active then Accent else Default)
            if showLabel then Html.span [ prop.className "max-w-full truncate"; prop.text label ]
        ]
    ]

/// Schmale Rail links (rund 72 px): Orte als Symbol mit kurzer Beschriftung, unten das Zahnrad. Keine Konten.
let rail (props: Props) =
    Html.nav [
        prop.ariaLabel "Orte"
        prop.className (
            $"hidden md:flex lg:hidden fixed top-0 bottom-0 left-0 z-50 w-[72px] flex-col gap-1 px-1 py-3 "
            + $"border-r border-border-default {Backgrounds.dark}"
        )
        prop.children [
            for item in navItems do
                React.keyedFragment (item.Label, [ railButton (isCurrent props item.Page) item.Label true item.Icon (fun () -> props.OnNavigate item.Page) ])
            Html.div [ prop.className "flex-1" ]
            railButton props.SettingsActive "Einstellungen" false gear props.OnSettings
        ]
    ]

// ============================================
// Seitenleiste (ab 1024 px)
// ============================================

/// Die Marke: Zeichen 30 × 30 px mit Akzentrand, „BudgetBuddy“ in der Display-Schrift 17 px. Bis
/// `design-system-x1abn` (Logo und Icon) steht das Zeichen „B“ statt des Buddy-Bilds.
let private brand (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        prop.title "Zur Übersicht"
        prop.ariaLabel "Zur Übersicht"
        prop.className $"flex items-center gap-2.5 px-2 pt-1 pb-[18px] text-left {Fonts.display} {FontWeights.semibold} {FontSizes.brand}"
        prop.onClick (fun _ -> onClick ())
        prop.children [
            Html.span [
                prop.ariaHidden true
                prop.className $"w-[30px] h-[30px] shrink-0 rounded-sm border border-accent {Backgrounds.surface} flex items-center justify-center {Colors.accent}"
                prop.text "B"
            ]
            Html.span [
                prop.className Colors.textPrimary
                prop.children [ Html.text "Budget"; Html.span [ prop.className Colors.accent; prop.text "Buddy" ] ]
            ]
        ]
    ]

let private sideRowClass (active: bool) =
    "w-full h-10 flex items-center gap-2.5 px-2.5 rounded-[10px] text-sm text-left transition-colors "
    + if active then $"{Backgrounds.surface} {Colors.accent}"
      else $"{Colors.textSecondary} hover:bg-surface-hover hover:text-text-primary"

/// Eine Zeile der Seitenleiste: Ort oder Einstellungen. `count` füllt `ledger-s1weh` (Mono 11 px, „braucht dich“).
let private sideRow (active: bool) (label: string) (icon: IconSize -> IconColor -> ReactElement) (count: int option) (onClick: unit -> unit) =
    Html.button [
        prop.type' "button"
        ariaCurrent active
        prop.className (sideRowClass active)
        prop.onClick (fun _ -> onClick ())
        prop.children [
            icon Side (if active then Accent else Default)
            Html.span [ prop.className "truncate"; prop.text label ]
            match count with
            | Some n when n > 0 ->
                Html.span [ prop.className $"ml-auto {Fonts.mono} {FontSizes.microPlus} {Colors.attention}"; prop.text (string n) ]
            | _ -> Html.none
        ]
    ]

let private accountValue (value: AccountValue) =
    match value with
    | AccountAmount (amount, currency) ->
        Html.span [
            prop.className ($"{Fonts.mono} text-xs tabular-nums whitespace-nowrap " + (if amount < 0m then Colors.error else ""))
            prop.text (Money.formatAmountText false true amount currency)
        ]
    | AccountPending -> Loading.amountSkeleton ()
    | AccountUnavailable -> Money.balanceUnavailable ()

/// Kontozeile: 34 px, Schrift 13 px, Saldo rechts in Mono, negativ in Rot. Gewählt mit Akzentstrich links.
let private accountRow (account: SidebarAccount) =
    Html.button [
        prop.type' "button"
        prop.custom ("data-testid", "sidebar-account")
        if account.IsActive then prop.custom ("aria-current", "true")
        prop.className (
            $"w-full h-[34px] shrink-0 flex items-center justify-between gap-2 px-2.5 rounded-sm border-l-2 {FontSizes.compact} text-left transition-colors "
            + if account.IsActive then $"{Backgrounds.surface} border-l-accent {Colors.textPrimary}"
              else $"border-l-transparent {Colors.textSecondary} hover:bg-surface-hover hover:text-text-primary"
        )
        prop.onClick (fun _ -> account.OnClick ())
        prop.children [
            Truncate.text "min-w-0" account.Label
            accountValue account.Value
        ]
    ]

/// Seitenleiste ab 1024 px: feste Spalte 232 px, Marke, Orte, „Konten“ und unten die Einstellungen.
let sidebar (props: Props) =
    Html.aside [
        prop.className (
            $"hidden lg:flex fixed top-0 bottom-0 left-0 z-50 w-[232px] flex-col gap-0.5 px-3 py-4 overflow-y-auto "
            + $"border-r border-border-default {Backgrounds.dark}"
        )
        prop.children [
            brand props.OnBrand
            Html.nav [
                prop.ariaLabel "Orte"
                prop.className "flex flex-col gap-0.5"
                prop.children [
                    for item in navItems do
                        React.keyedFragment (item.Label, [ sideRow (isCurrent props item.Page) item.Label item.Icon None (fun () -> props.OnNavigate item.Page) ])
                ]
            ]
            Html.div [
                prop.className $"px-2.5 pt-[18px] pb-1.5 {Fonts.mono} {FontSizes.microPlus} uppercase tracking-[0.14em] {Colors.textMuted}"
                prop.text "Konten"
            ]
            if props.AccountsLoading && List.isEmpty props.Accounts then
                for i in 1..3 do
                    Html.div [
                        prop.key (string i)
                        prop.className "h-[34px] flex items-center px-2.5"
                        prop.children [ Loading.amountSkeleton () ]
                    ]
            else
                for account in props.Accounts do React.keyedFragment (account.Key, [ accountRow account ])
            Html.div [ prop.className "flex-1 min-h-4" ]
            sideRow props.SettingsActive "Einstellungen" gear None props.OnSettings
        ]
    ]

// ============================================
// Zusammen
// ============================================

/// Die Navigation für jede Breite. Einmal in der Haupt-`View.fs`.
let navigation (props: Props) =
    React.fragment [
        sidebar props
        rail props
        if not props.HideBottomBar then bottomBar props
    ]

// ============================================
// Vorschau ohne `fixed` (Styleguide-Galerie, Export-Bundle)
// ============================================

/// Leiste unten ohne `fixed`, zum Einbetten in die Galerie (`/styleguide`, `design-system-t82sg`).
let bottomBarPreview (props: Props) =
    Html.nav [
        prop.ariaLabel "Orte (Vorschau)"
        prop.className $"relative w-full {bottomBarClass}"
        prop.children [ for item in navItems do React.keyedFragment (item.Label, [ bottomItem props item ]) ]
    ]

/// Seitenleiste ohne `fixed`: gleiche Zeilen wie `sidebar`, feste Höhe für die Galerie.
let sidebarPreview (props: Props) =
    Html.div [
        prop.className $"flex w-[232px] h-[520px] flex-col gap-0.5 px-3 py-4 border border-border-default rounded-lg {Backgrounds.dark}"
        prop.children [
            brand props.OnBrand
            for item in navItems do
                React.keyedFragment (item.Label, [ sideRow (isCurrent props item.Page) item.Label item.Icon None (fun () -> props.OnNavigate item.Page) ])
            Html.div [
                prop.className $"px-2.5 pt-[18px] pb-1.5 {Fonts.mono} {FontSizes.microPlus} uppercase tracking-[0.14em] {Colors.textMuted}"
                prop.text "Konten"
            ]
            for account in props.Accounts do React.keyedFragment (account.Key, [ accountRow account ])
            Html.div [ prop.className "flex-1" ]
            sideRow props.SettingsActive "Einstellungen" gear None props.OnSettings
        ]
    ]

// ============================================
// Seitenrahmen
// ============================================

/// Der Abstand des Inhalts zur Navigation: unten die Leiste am Handy, links Rail oder Seitenleiste.
let private shellOffset = "pb-24 md:pb-8 md:ml-[72px] lg:ml-[232px] pt-[calc(env(safe-area-inset-top)+0.5rem)]"

/// Inhalt mit `container`-Breite.
let pageContent (children: ReactElement list) =
    Html.main [
        prop.className $"{shellOffset} animate-fade-in"
        prop.children [
            Html.div [ prop.className "container mx-auto px-4"; prop.children children ]
        ]
    ]

/// Wie `pageContent`, ohne `container`-Begrenzung und ohne seitlichen Innenabstand: für Listen-Seiten, die die
/// verfügbare Breite füllen (ADR 0049 Punkt 9). Die Seite setzt ihre Ränder selbst (`Primitives.Container.full`).
let pageContentFull (children: ReactElement list) =
    Html.main [
        prop.className $"{shellOffset} animate-fade-in"
        prop.children children
    ]

/// Main app wrapper with dark background
let appWrapper (children: ReactElement list) =
    Html.div [
        prop.className "min-h-screen bg-surface-app"
        prop.children children
    ]
