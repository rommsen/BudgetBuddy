module Components.Styleguide.View

// Lebende Komponenten-Galerie — der VISUELLE Styleguide.
//
// Diese Seite rendert die ECHTEN Design-System-Komponenten und Tokens aus
// `Client.DesignSystem.*`. Sie ist bewusst rein präsentational: kein App-State,
// keine neuen Msg-Fälle. Interaktive Demos (Modal/Toast/BottomSheet/Swipe) nutzen
// lokale `React.useState`-Hooks, damit der Demo-Zustand die App nicht verschmutzt.
//
// Gegliedert analog zum geschriebenen Begleiter `standards/frontend/styleguide.md`
// (§1 visuelle Sprache → §8 Voice). Die ausführlichen Regeln ("wann nicht", ADR-
// Begründungen) stehen dort — hier wird nur gezeigt, nicht dupliziert.
//
// Quelle, nicht Kopie: Da die realen Komponenten gerendert werden, kann diese
// Galerie per Konstruktion nicht von den Komponenten driften.

open System
open Feliz
open Shared.Domain
open Client.DesignSystem

// ============================================
// Layout-Helfer (nur für die Galerie-Struktur)
// ============================================

/// Abschnittsüberschrift einer Galerie-Sektion (z. B. "1. Visuelle Sprache").
/// Der `title` dient zugleich als stabiler `prop.key`, da jede Sektion in der
/// Top-Level-Sektionsliste ein Listen-Kind ist (React verlangt unique keys).
let private section (title: string) (children: ReactElement list) =
    Html.section [
        prop.key title
        prop.className "space-y-4 md:space-y-6 pt-8 md:pt-10 border-t border-border-subtle first:border-t-0 first:pt-0"
        prop.children [
            Html.h2 [
                prop.className (Tokens.Presets.sectionHeader + " " + Tokens.Colors.neonTeal)
                prop.text title
            ]
            yield! children
        ]
    ]

/// Untertitel innerhalb einer Sektion (Komponentenname).
let private subTitle (text: string) =
    Html.h3 [
        prop.className "text-sm font-semibold font-mono text-text-muted uppercase tracking-wide mt-2"
        prop.text text
    ]

/// Kurzer Muster-Hinweis-Text mit Verweis auf den Markdown-Styleguide.
let private note (text: string) =
    Html.p [
        prop.className "text-xs text-text-muted/80 max-w-2xl leading-relaxed"
        prop.text text
    ]

/// Horizontale Zeile mit umbrechenden Demo-Elementen.
/// Die Demo-Elemente stammen aus Design-System-Helfern (Button.*, Badge.*, Icons.*),
/// die keinen eigenen `prop.key` durchreichen. Daher wird jedes Kind in ein
/// `React.keyedFragment` gehüllt — das vergibt einen stabilen Index-Key, ohne einen
/// zusätzlichen DOM-Knoten einzufügen (kein visueller Unterschied).
let private row (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-wrap items-center gap-3"
        prop.children (children |> List.mapi (fun i child -> React.keyedFragment (i, [ child ])))
    ]

/// Kleine Bezeichnung unter einem Swatch / einer Probe.
let private caption (text: string) =
    Html.span [
        prop.className "text-xs text-text-muted font-mono"
        prop.text text
    ]

// ============================================
// 1.–3. Farb- und Token-Swatches
// ============================================

/// Ein Farb-Swatch: Hintergrundfläche in der Neon-Farbe + semantisches Label.
let private colorSwatch (bgClass: string) (label: string) (meaning: string) =
    Html.div [
        prop.className "flex flex-col gap-1"
        prop.children [
            Html.div [
                prop.className (bgClass + " h-16 w-full rounded-lg border border-border-subtle")
            ]
            Html.span [ prop.className "text-sm font-semibold text-text-primary"; prop.text label ]
            caption meaning
        ]
    ]

let private colorSection =
    section "1. Farbsemantik — Neon-on-Dark" [
        note "Jede Neon-Farbe trägt eine feste Bedeutung — Farbe ist nie rein dekorativ. \
              Quelle: Tokens.fs (Colors/Backgrounds/Borders/Glows). Ausführliche Semantik & \
              harte Token-Regel: standards/frontend/styleguide.md §2."
        Primitives.Grid.three [
            // Volle Neon-Hintergründe (über bg-neon-* Klassen, nicht über text-Tokens)
            colorSwatch "bg-neon-orange" "Orange" "primär / CTA / Action"
            colorSwatch "bg-neon-teal" "Teal" "sekundär / Navigation / Info"
            colorSwatch "bg-neon-green" "Green" "Erfolg / positiv"
            colorSwatch "bg-neon-red" "Red" "Fehler / negativ"
            colorSwatch "bg-neon-purple" "Purple" "Akzent / Kategorie"
            colorSwatch "bg-neon-pink" "Pink" "Akzent / attention"
        ]

        subTitle "Backgrounds (gestaffelte Tiefe)"
        Primitives.Grid.three [
            colorSwatch Tokens.Backgrounds.void' "surface-app" "Grund-Ebene"
            colorSwatch Tokens.Backgrounds.dark "surface-card" "Karten"
            colorSwatch Tokens.Backgrounds.surface "surface-elevated" "erhöht"
            colorSwatch Tokens.Backgrounds.greenSubtle "green/10" "Erfolg subtle"
            colorSwatch Tokens.Backgrounds.orangeSubtle "orange/10" "primär subtle"
            colorSwatch Tokens.Backgrounds.tealSubtle "teal/10" "info subtle"
        ]

        subTitle "Glows (Hervorhebung, nicht Deko)"
        row [
            Html.div [
                prop.className ("bg-surface-card h-16 w-32 rounded-lg border border-neon-orange " + Tokens.Glows.orange)
                prop.children [ Html.div [ prop.className "flex h-full items-center justify-center"; prop.children [ caption "glow-orange" ] ] ]
            ]
            Html.div [
                prop.className ("bg-surface-card h-16 w-32 rounded-lg border border-neon-teal " + Tokens.Glows.teal)
                prop.children [ Html.div [ prop.className "flex h-full items-center justify-center"; prop.children [ caption "glow-teal" ] ] ]
            ]
            Html.div [
                prop.className ("bg-surface-card h-16 w-32 rounded-lg border border-neon-green " + Tokens.Glows.green)
                prop.children [ Html.div [ prop.className "flex h-full items-center justify-center"; prop.children [ caption "glow-green" ] ] ]
            ]
        ]
        note "Glow markiert das Aktive/Wichtige (CTA, positiver Betrag, fokussierte Karte) — \
              sparsam: alles glüht = nichts glüht. Quelle: Tokens.fs Glows, styleguide.md §1."
    ]

// ============================================
// 4. Typografie
// ============================================

let private typographySection =
    section "2. Typografie" [
        note "Fließtext in Outfit (font-sans), große Header optional in Orbitron (font-display), \
              Zahlen/Daten in JetBrains Mono (font-mono) mit tabular-nums. Quelle: Tokens.fs Fonts/FontSizes."

        subTitle "Font-Familien"
        Html.div [
            prop.className "space-y-2"
            prop.children [
                Html.p [ prop.className (Tokens.Fonts.sans + " text-lg text-text-primary"); prop.text "font-sans · Outfit — UI-Fließtext, direkt und knapp." ]
                Html.p [ prop.className (Tokens.Fonts.display + " text-lg text-text-primary"); prop.text "font-display · Orbitron — Hero/Header" ]
                Html.p [ prop.className (Tokens.Fonts.mono + " text-lg text-text-primary"); prop.text "font-mono · JetBrains Mono — 1234,56 €" ]
            ]
        ]

        subTitle "Größenskala (mobile-first)"
        Html.div [
            prop.className "space-y-1"
            prop.children [
                Html.p [ prop.className (Tokens.FontSizes.hero + " " + Tokens.FontWeights.bold + " " + Tokens.Fonts.display); prop.text "Hero" ]
                Html.p [ prop.className (Tokens.FontSizes.pageTitle + " " + Tokens.FontWeights.bold); prop.text "Page Title" ]
                Html.p [ prop.className (Tokens.FontSizes.sectionTitle + " " + Tokens.FontWeights.semibold); prop.text "Section Title" ]
                Html.p [ prop.className (Tokens.FontSizes.cardTitle + " " + Tokens.FontWeights.semibold); prop.text "Card Title" ]
                Html.p [ prop.className Tokens.FontSizes.body; prop.text ("Body: " + Tokens.FontSizes.body) ]
                Html.p [ prop.className (Tokens.FontSizes.xs + " " + Tokens.Colors.textMuted); prop.text "Label / Caption — text-xs" ]
            ]
        ]

        subTitle "Gradient-Text (Hero-Titel)"
        Html.p [
            prop.className (Tokens.Presets.gradientText + " " + Tokens.FontSizes.hero + " " + Tokens.FontWeights.bold + " " + Tokens.Fonts.display)
            prop.text "BudgetBuddy"
        ]
    ]

// ============================================
// 5. Statische Komponenten
// ============================================

let private buttonSection =
    section "3. Button" [
        note "Jede klickbare Aktion. Primär (Orange) max. eine pro Screen-Kontext. \
              Navigation zwischen Seiten → Navigation. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-button.html.
        Html.div [
            prop.custom ("data-ds-card", "components-button")
            prop.custom ("data-ds-card-name", "Button")
            prop.custom ("data-ds-card-subtitle", "Primär, Sekundär, Ghost, Danger, Icon-, Loading- und Hero-Varianten")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                row [
                    Button.primary "Primär" ignore
                    Button.secondary "Sekundär" ignore
                    Button.ghost "Ghost" ignore
                    Button.danger "Löschen" ignore
                ]
                row [
                    Button.primaryWithIcon "Hinzufügen" (Icons.plus Icons.SM Icons.Primary) ignore
                    Button.primaryLoading "Speichern…" true ignore
                    Button.secondaryLoading "Test…" true ignore
                ]
                subTitle "Hero-CTAs"
                row [
                    Button.hero "Sync starten" ignore
                    Button.heroTeal "Weiter" ignore
                    Button.heroWithIcon "Sync starten" (Icons.sync Icons.MD Icons.Primary) ignore
                ]
                subTitle "Button-Gruppe"
                Button.group [
                    Button.secondary "Abbrechen" ignore
                    Button.primary "Bestätigen" ignore
                ]
            ]
        ]
    ]

let private cardSection =
    section "4. Card" [
        note "Inhaltsblöcke. glow für featured, withAccent für Akzentlinie, emptyState sagt was als Nächstes zu tun ist. styleguide.md §5."
        // data-ds-card markiert die Sektion für den Export-Bundle-Build
        // (scripts/export-design-system.mjs, design-system-t82sg): das Skript
        // schneidet dieses Element aus der gerenderten Route heraus und schreibt es
        // 1:1 nach dist/design-system/preview/components-card.html.
        Html.div [
            prop.custom ("data-ds-card", "components-card")
            prop.custom ("data-ds-card-name", "Card")
            prop.custom ("data-ds-card-subtitle", "Standard-, Glas-, Glow-, Akzent- und Leerzustand-Karten")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                Primitives.Grid.two [
                    Card.standard [
                        Card.headerSimple "Standard"
                        Card.body [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Der Default-Inhaltsblock." ] ]
                    ]
                    Card.glass [
                        Card.headerSimple "Glass"
                        Card.body [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Blur-Hintergrund." ] ]
                    ]
                    Card.glow [
                        Card.headerSimple "Glow (featured)"
                        Card.body [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Neon-Border, hervorgehoben." ] ]
                    ]
                    Card.withAccent [
                        Card.headerSimple "With Accent"
                        Card.body [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Akzentlinie links." ] ]
                    ]
                ]
                Card.emptyState
                    (Icons.banknotes Icons.XL Icons.Default)
                    "Noch keine Transaktionen"
                    "Starte einen Sync, um Transaktionen zu importieren."
                    (Some (Button.primary "Sync starten" ignore))
            ]
        ]
    ]

let private badgeSection =
    section "5. Badge" [
        note "Kompakter Status/Label. Semantisch + Domänen-Status. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-badge.html.
        Html.div [
            prop.custom ("data-ds-card", "components-badge")
            prop.custom ("data-ds-card-name", "Badge")
            prop.custom ("data-ds-card-subtitle", "Semantische Status-Badges und Zähler")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-3"
            prop.children [
                row [
                    Badge.success "Aktiv"
                    Badge.warning "Review"
                    Badge.error "Fehler"
                    Badge.info "Neu"
                ]
                row [
                    Badge.imported ()
                    Badge.pendingReview ()
                    Badge.autoCategorized ()
                    Badge.uncategorized ()
                    Badge.count 5
                ]
            ]
        ]
    ]

let private moneySection =
    section "6. Money" [
        note "Jede Geldanzeige. Vorzeichen-Farbe + Glow werden automatisch gesetzt (positiv=Green, negativ=Red) — nicht manuell überschreiben. styleguide.md §2/§5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-money.html.
        Html.div [
            prop.custom ("data-ds-card", "components-money")
            prop.custom ("data-ds-card-name", "Money")
            prop.custom ("data-ds-card-subtitle", "Beträge klein bis groß, positiv, negativ und null")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-3"
            prop.children [
                Primitives.HStack.md [
                    Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.simple 1234.56m "EUR"; caption "positiv (simple)" ] ]
                    Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.simple -89.90m "EUR"; caption "negativ (simple)" ] ]
                ]
                Primitives.HStack.md [
                    Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.large 2500.00m "EUR"; caption "large" ] ]
                ]
                Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.hero 4280.42m "EUR"; caption "hero (Balance)" ] ]
                Html.div [
                    prop.className "flex flex-col gap-1"
                    prop.children [ Money.transactionDeleted -89.90m "EUR"; caption "transactionDeleted (in YNAB gelöscht, ledger-p3n8k)" ]
                ]
                Html.div [
                    prop.className "flex flex-col gap-1"
                    prop.children [ Money.accountBalance 1250.40m "EUR"; caption "accountBalance (ohne führendes Plus, ledger-x0yga)" ]
                ]
                Html.div [
                    prop.className "max-w-sm space-y-1"
                    prop.children [
                        Money.accountListRow "Girokonto" 1250.40m "EUR" true ignore
                        Money.accountListRow "Tagesgeld" -84.30m "EUR" false ignore
                        caption "accountListRow (aktiv / inaktiv, ledger-x0yga)"
                    ]
                ]
            ]
        ]
    ]

let private statsSection =
    section "7. Stats" [
        note "KPI-/Kennzahl-Kacheln. Vollständige Tabellen → Table. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-stats.html.
        Html.div [
            prop.custom ("data-ds-card", "components-stats")
            prop.custom ("data-ds-card-name", "Stats")
            prop.custom ("data-ds-card-subtitle", "Kennzahlen-Kacheln mit Icon, Trend und Raster")
            prop.custom ("data-ds-group", "Components")
            prop.children [
                Stats.grid [
                    Stats.withIcon "Transaktionen" "1.234" (Icons.banknotes Icons.MD Icons.NeonTeal)
                    Stats.withTrend "Importe" "42" (Stats.Trend.Up 12.5m)
                    Stats.syncCount 8
                ]
                Html.div [
                    prop.className "max-w-xs"
                    prop.children [
                        Stats.withAccent "Kontostand" "1.250,40 €" Stats.NoAccent
                        caption "withAccent NoAccent (kein Akzentstreifen, ledger-w9skm/ADR 0035)"
                    ]
                ]
            ]
        ]
    ]

let private tableSection =
    // Hand-gebaute Tabelle: zwei verschachtelte Listen-Ebenen (Zeilen in <tbody>,
    // Zellen in jeder <tr>/<thead>). Die Table.*-Helfer (shared Design-System) reichen
    // keinen `prop.key` durch, daher wird jede Zelle und jede Zeile in ein
    // `React.keyedFragment` gehüllt — ein Fragment erzeugt keinen DOM-Knoten, die
    // <th>/<td>/<tr>-Struktur bleibt also unverändert (kein visueller Unterschied).
    let keyed (els: ReactElement list) =
        els |> List.mapi (fun i el -> React.keyedFragment (i, [ el ]))
    section "8. Table" [
        note "Dichte tabellarische Daten (Desktop). Mobile Listen mit Zeilen-Aktionen → Card-Liste + Swipe. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-table.html.
        Html.div [
            prop.custom ("data-ds-card", "components-table")
            prop.custom ("data-ds-card-name", "Table")
            prop.custom ("data-ds-card-subtitle", "Tabelle mit Kopfzeile und Datenzeilen")
            prop.custom ("data-ds-group", "Components")
            prop.children [
                Table.simple [
                    Table.thead (keyed [
                        Table.th "Payee"
                        Table.th "Kategorie"
                        Table.thRight "Betrag"
                    ])
                    Table.tbody (keyed [
                        Table.tr (keyed [ Table.td "REWE"; Table.td "Lebensmittel"; Table.tdRight "-42,90 €" ])
                        Table.tr (keyed [ Table.td "Gehalt"; Table.td "Einkommen"; Table.tdRight "+2.500,00 €" ])
                        Table.tr (keyed [ Table.td "Netflix"; Table.td "Abos"; Table.tdRight "-12,99 €" ])
                    ])
                ]
            ]
        ]
    ]

let private calmListSection =
    let keyed (els: ReactElement list) =
        els |> List.mapi (fun i el -> React.keyedFragment (i, [ el ]))
    // Gruppenkopf: Datum links, Tagessumme bündig unter den Beträgen (ADR 0049, Punkt 8).
    // Vier Spalten (Datum, Payee, Kategorie, Memo) liegen vor dem Betrag.
    let groupRow (title: string) (sum: string) = Table.calmGroupRow title 4 title sum
    let statusWord (text: string) =
        Html.td [
            prop.className $"px-3 text-right {Tokens.FontSizes.microPlus} {Tokens.Colors.neonPink}"
            prop.text text
        ]
    let noCategory =
        Table.tdContent [ Html.span [ prop.className $"text-xs {Tokens.Colors.neonOrange}"; prop.text "Keine Kategorie" ] ]
    let emptyCell = Table.tdContent []
    let dataRow (edge: bool) (date: string) (payee: string) (category: ReactElement) (memo: string) (amount: decimal) (status: string) =
        Table.calmRow edge (keyed [
            Table.roleCell Table.DateCell [ Html.text date ]
            Table.roleCell Table.Payee [ Html.text payee ]
            category
            Table.roleCell Table.Muted [ Html.text memo ]
            Table.amountCell amount "EUR"
            if status = "" then emptyCell else statusWord status
        ])
    let cat (text: string) = Table.roleCell Table.Secondary [ Html.text text ]
    // Zweizeilige Handy-Zeile: Kante links über dieselbe Klasse wie die Tabellenzeile
    let mobileRow (edge: bool) (payee: string) (amount: decimal) (second: ReactElement) =
        Html.div [
            prop.className (
                $"{Tokens.ListRoles.mobileRowPadding} border-b border-border-subtle "
                + if edge then Tokens.ListRoles.statusEdge else ""
            )
            prop.children [
                Html.div [
                    prop.className Tokens.ListRoles.mobileLine1
                    prop.children [
                        Html.span [ prop.className Tokens.ListRoles.primary; prop.text payee ]
                        Money.listAmount amount "EUR"
                    ]
                ]
                Html.div [ prop.className Tokens.ListRoles.mobileLine2; prop.children [ second ] ]
            ]
        ]
    section "8a. Ruhige Listen" [
        note "Farbe ist ein Signal: Payee hell, Rest gedämpft, Kante in Pink nur bei zu prüfen, Teal nur bei Eingang, Orange nur bei Keine Kategorie. ADR 0049, styleguide.md §2."
        Html.div [
            prop.custom ("data-ds-card", "components-ruhige-listen")
            prop.custom ("data-ds-card-name", "Ruhige Listen")
            prop.custom ("data-ds-card-subtitle", "Tabelle, Handy-Zeile, Listenbetrag und schlanker Seitenkopf")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-6"
            prop.children [
                PageHeader.calm "Kassenbuch" (Some "Gespiegelt heute 22:24") [
                    Button.secondary "Sync" ignore
                ]
                note "Sortierbare Kopfzelle (ledger-bn913): aktiv aufsteigend, aktiv absteigend, nicht aktiv. Der Pfeil steht nur am aktiven Kopf, aria-sort trägt die Richtung."
                Table.view { Table.defaultTableProps with Variant = Table.Hover } [
                    Table.thead (keyed [
                        Table.calmSortableHeaderCell "Betrag aufsteigend" None Table.Ascending ignore
                        Table.calmSortableHeaderCell "Datum absteigend" None Table.Descending ignore
                        Table.calmSortableHeaderCell "Payee ohne Sortierung" None Table.Unsorted ignore
                    ])
                ]
                Table.view { Table.defaultTableProps with Variant = Table.Hover } [
                    Table.thead (keyed [
                        Table.calmHeaderCell "Datum" None
                        Table.calmHeaderCell "Payee" None
                        Table.calmHeaderCell "Kategorie" None
                        Table.calmHeaderCell "Memo" None
                        Table.calmHeaderCell "Betrag" (Some "right")
                        Table.calmHeaderCell "Status" (Some "right")
                    ])
                    Table.tbody (keyed [
                        groupRow "HEUTE" "-54,52 €"
                        dataRow true "Heute" "REWE Mitte" (cat "Alltag: Essen/Einkaufen") "Wocheneinkauf" -48.12m "zu prüfen"
                        dataRow false "Heute" "Bäckerei Krause" (cat "Alltag: Essen/Einkaufen") "" -6.40m ""
                        groupRow "GESTERN" "+3.774,00 €"
                        dataRow false "Gestern" "Arbeitgeber GmbH" (cat "Ready to Assign") "Gehalt Oktober" 3412.80m ""
                        dataRow true "Gestern" "Amazon Marketplace" noCategory "" -37.45m "zu prüfen"
                    ])
                ]
                Html.div [
                    prop.className "max-w-sm rounded-xl border border-border-subtle bg-surface-card overflow-hidden"
                    prop.children (keyed [
                        mobileRow true "REWE Mitte" -48.12m (
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    Html.span [ prop.className Tokens.ListRoles.secondary; prop.text "Alltag: Essen/Einkaufen" ]
                                    Html.span [ prop.className Tokens.ListRoles.muted; prop.text "Wocheneinkauf" ]
                                ]
                            ])
                        mobileRow false "Arbeitgeber GmbH" 3412.80m (
                            Html.span [ prop.className Tokens.ListRoles.secondary; prop.text "Ready to Assign" ])
                        mobileRow true "Amazon Marketplace" -37.45m (
                            Html.span [ prop.className $"text-xs {Tokens.Colors.neonOrange}"; prop.text "Keine Kategorie" ])
                    ])
                ]
                Html.div [
                    prop.className "flex flex-col gap-1"
                    prop.children [
                        Money.listAmount -48.12m "EUR"
                        Money.listAmount 0m "EUR"
                        Money.listAmount 4000m "EUR"
                        caption "Money.listAmount: negativ und null neutral, Eingang Teal"
                    ]
                ]
                Html.div [
                    prop.custom ("data-ds-card", "components-auswahl")
                    prop.custom ("data-ds-card-name", "Auswahl")
                    prop.custom ("data-ds-card-subtitle", "Picker-Zeile, Gruppenkopf, Verfügbar-Betrag, Suchfeld und Footer")
                    prop.custom ("data-ds-group", "Components")
                    prop.className "max-w-sm space-y-3"
                    prop.children [
                        PickerParts.searchField "" ignore "Kategorie suchen…"
                        Html.div [
                            prop.children [
                                PickerParts.groupHeader "Alltag"
                                PickerParts.row "py-2" [ prop.key "r1" ]
                                    (Input.checkboxListRow true ignore "Essen/Einkaufen")
                                    (Some (212.40m, "EUR"))
                                PickerParts.row "py-2" [ prop.key "r2" ]
                                    (Input.checkboxListRow false ignore "Eine sehr lange Kategorie, die gekürzt werden muss")
                                    (Some (0m, "EUR"))
                                PickerParts.groupHeader "Fixkosten"
                                PickerParts.row "py-2" [ prop.key "r3" ]
                                    (Input.checkboxListRow false ignore "Miete")
                                    (Some (-35.00m, "EUR"))
                            ]
                        ]
                        PickerParts.footer 1 ignore ignore
                        Html.div [
                            prop.className "space-y-2"
                            prop.children [
                                DateField.view { Value = None; OnChange = ignore; Invalid = false; Label = "Leer" }
                                DateField.view { Value = Some (System.DateTime(2026, 10, 7)); OnChange = ignore; Invalid = false; Label = "Gefüllt" }
                                DateField.view { Value = None; OnChange = ignore; Invalid = true; Label = "Fehler" }
                                caption "Datumsfeld: leer, gefüllt, Fehler (Hinweis, Übernehmen gesperrt)"
                            ]
                        ]
                        caption "Picker-Bausteine: Name 14/15px 500, Betrag neutral (Rot nur negativ), Übernehmen ohne Glow"
                    ]
                ]
            ]
        ]
    ]

let private loadingSection =
    section "9. Loading" [
        note "Skeletons statt Spinner, wo die Zielform bekannt ist — kein Layout-Sprung. Generischer Spinner/neonPulse nur bei unbekannter Form. styleguide.md §6."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-loading.html.
        Html.div [
            prop.custom ("data-ds-card", "components-loading")
            prop.custom ("data-ds-card-name", "Loading")
            prop.custom ("data-ds-card-subtitle", "Spinner, Neon-Puls und Skeleton-Platzhalter")
            prop.custom ("data-ds-group", "Feedback")
            prop.className "space-y-3"
            prop.children [
                row [
                    Loading.spinner Loading.MD Loading.Teal
                    Loading.spinner Loading.LG Loading.Green
                    Loading.neonPulse Loading.Teal
                ]
                subTitle "Skeletons"
                Loading.tableSkeleton 3 3
            ]
        ]
    ]

let private errorSection =
    section "10. ErrorDisplay" [
        note "Fehlerdarstellung — direkt und ehrlich, benennt was passiert ist. Kurzes Erfolgs-Feedback → Toast. styleguide.md §5/§8."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-errordisplay.html.
        Html.div [
            prop.custom ("data-ds-card", "components-errordisplay")
            prop.custom ("data-ds-card-name", "ErrorDisplay")
            prop.custom ("data-ds-card-subtitle", "Inline-Fehler, Fehlerkarten und Warnung")
            prop.custom ("data-ds-group", "Feedback")
            prop.className "space-y-3"
            prop.children [
                ErrorDisplay.inline' "Ungültige E-Mail-Adresse"
                ErrorDisplay.cardCompact "Einstellungen konnten nicht geladen werden" (Some ignore)
                ErrorDisplay.warning "Diese Aktion kann nicht rückgängig gemacht werden." None
            ]
        ]
    ]

let private iconsSection =
    section "11. Icons" [
        note "SVG-Icons mit Size×Color-Tokens. Eigene rohe <svg> inline gelten als Drift — fehlt ein Icon, in Icons.fs ergänzen. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-icons.html.
        Html.div [
            prop.custom ("data-ds-card", "components-icons")
            prop.custom ("data-ds-card-name", "Icons")
            prop.custom ("data-ds-card-subtitle", "Icon-Satz in allen Größen und Farben")
            prop.custom ("data-ds-group", "Components")
            prop.children [
                row [
                    Icons.sync Icons.MD Icons.NeonTeal
                    Icons.plus Icons.MD Icons.Primary
                    Icons.check Icons.MD Icons.NeonGreen
                    Icons.x Icons.MD Icons.Default
                    Icons.trash Icons.MD Icons.Error
                    Icons.edit Icons.MD Icons.Default
                    Icons.settings Icons.MD Icons.Default
                    Icons.warning Icons.MD Icons.Warning
                    Icons.info Icons.MD Icons.Info
                    Icons.banknotes Icons.MD Icons.NeonGreen
                    Icons.creditCard Icons.MD Icons.NeonPurple
                    Icons.search Icons.MD Icons.Default
                    Icons.externalLink Icons.MD Icons.NeonTeal
                ]
            ]
        ]
    ]

let private pageHeaderSection =
    section "12. PageHeader" [
        note "Seitentitel. Karten-Titel → Card.header; Abschnittstitel → Card.headerSimple. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-pageheader.html.
        Html.div [
            prop.custom ("data-ds-card", "components-pageheader")
            prop.custom ("data-ds-card-name", "PageHeader")
            prop.custom ("data-ds-card-subtitle", "Seitenkopf mit Untertitel, Verlauf und Aktionen")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                PageHeader.withSubtitle "Einstellungen" "Verbindungen und Präferenzen konfigurieren."
                PageHeader.gradient "Kategorisierungs-Regeln"
            ]
        ]
    ]

let private primitivesSection =
    section "13. Primitives (Layout)" [
        note "Layout-Bausteine: Container/Stack/Grid/Spacer, responsive Sichtbarkeit. Visuell gestylte Blöcke → Card. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-primitives.html.
        Html.div [
            prop.custom ("data-ds-card", "components-primitives")
            prop.custom ("data-ds-card-name", "Primitives")
            prop.custom ("data-ds-card-subtitle", "Layout-Bausteine: Container, Stack und Raster")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                subTitle "HStack"
                Primitives.HStack.md [
                    Badge.info "A"; Badge.info "B"; Badge.info "C"
                ]
                subTitle "Grid (drei Spalten)"
                Primitives.Grid.three [
                    Card.compact [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Eins" ] ]
                    Card.compact [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Zwei" ] ]
                    Card.compact [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Drei" ] ]
                ]
            ]
        ]
    ]

// ============================================
// Interaktive Komponenten (lokaler React.useState)
// ============================================

/// Input-Demos mit lokalem State (text/select/toggle/checkbox/searchableSelect).
[<ReactComponent>]
let private InputDemo () =
    let textValue, setTextValue = React.useState ""
    let selectValue, setSelectValue = React.useState ""
    let toggleValue, setToggleValue = React.useState true
    let checkValue, setCheckValue = React.useState false
    let searchValue, setSearchValue = React.useState ""

    let categoryOptions = [
        "groceries", "Lebensmittel"
        "rent", "Miete"
        "subscriptions", "Abos"
        "income", "Einkommen"
    ]

    let groupedOptions = [
        "groceries", "Alltag: Lebensmittel"
        "rent", "Fixkosten: Miete"
        "subscriptions", "Fixkosten: Abos"
        "income", "Einkommen"
    ]

    section "14. Input" [
        note "Alle Formularelemente. Mobiles Kategorie-/Konto-Auswählen → BottomSheet (keyboard-fest). styleguide.md §5/§7.2."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-input.html.
        Html.div [
            prop.custom ("data-ds-card", "components-input")
            prop.custom ("data-ds-card-name", "Input")
            prop.custom ("data-ds-card-subtitle", "Textfelder, Auswahl, Schalter und Feldgruppen")
            prop.custom ("data-ds-group", "Forms")
            prop.className "space-y-3"
            prop.children [
                Primitives.Grid.two [
                    Input.groupSimple "Text" (Input.textSimple textValue setTextValue "REWE, Netflix …")
                    Input.groupSimple "Select" (Input.selectWithPlaceholder selectValue setSelectValue "Kategorie wählen…" categoryOptions)
                ]
                Input.groupSimple "Searchable Select" (Input.searchableSelect searchValue setSearchValue "Kategorie suchen…" groupedOptions)
                Input.toggle toggleValue setToggleValue (Some "Auto-Kategorisierung aktiv")
                Input.checkbox checkValue setCheckValue "Bestätigt"
            ]
        ]
    ]

/// Form-Demo: submitButton-Gating bei fehlendem Pflichtfeld.
[<ReactComponent>]
let private FormDemo () =
    let nameValue, setNameValue = React.useState ""

    section "15. Form (Required-Field-Gating)" [
        note "Submit-Buttons deaktivieren bei fehlenden Pflichtfeldern. Tippe etwas, um den Button zu aktivieren. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-form.html.
        Html.div [
            prop.custom ("data-ds-card", "components-form")
            prop.custom ("data-ds-card-name", "Form")
            prop.custom ("data-ds-card-subtitle", "Formular mit Pflichtfeld-Gating des Speichern-Buttons")
            prop.custom ("data-ds-group", "Forms")
            prop.className "space-y-3"
            prop.children [
                Input.groupRequired "Name" (Input.textSimple nameValue setNameValue "Pflichtfeld…")
                Form.submitButton "Speichern" ignore false [ ("Name", nameValue) ]
            ]
        ]
    ]

/// Modal-Demo (Desktop-Dialog) per useState.
[<ReactComponent>]
let private ModalDemo () =
    let isOpen, setOpen = React.useState false

    section "16. Modal (Desktop-Dialog)" [
        note "Zentrierte Dialoge auf Desktop. Mobile Auswahl-Flows → BottomSheet (keyboard-fest). styleguide.md §5/§7.1."
        // data-ds-card-trigger: Export-Skript öffnet das Modal über diesen stabilen
        // Selektor, bevor es components-modal.html schreibt. Modal.view portalt nach
        // document.body (wie BottomSheet), ein umschließendes data-ds-card würde das
        // Modal also nicht mit erfassen.
        Html.span [
            prop.custom ("data-ds-card-trigger", "components-modal")
            prop.custom ("data-ds-card-name", "Modal")
            prop.custom ("data-ds-card-subtitle", "Zentrierter Desktop-Dialog mit Abbrechen und OK")
            prop.children [ Button.secondary "Modal öffnen" (fun () -> setOpen true) ]
        ]
        Modal.simple isOpen "Verbindung testen" (fun () -> setOpen false) [
            Modal.body [ Html.p [ prop.className "text-sm text-text-secondary"; prop.text "Beispiel-Dialog. Schließen über Button oder Backdrop." ] ]
            Modal.footer [
                Button.secondary "Abbrechen" (fun () -> setOpen false)
                Button.primary "OK" (fun () -> setOpen false)
            ]
        ]
    ]

/// Toast-Demo per useState — feuert flüchtiges Feedback und zeigt den sanften
/// Abgang (design-system-004): "Schließen" markiert den Toast als exiting (Exit-
/// Animation) und entfernt ihn erst nach `Types.Toast.exitDurationMs`. Dieselbe
/// Zwei-Phasen-Mechanik wie in der App (State.fs), hier lokal nachgebaut, da die
/// Galerie keinen App-State berührt.
[<ReactComponent>]
let private ToastDemo () =
    // (Id, Message, Variant, Exiting)
    let toasts, setToasts = React.useState ([]: (Guid * string * Toast.ToastVariant * bool) list)
    let toastsRef = React.useRef toasts
    toastsRef.current <- toasts

    let fire variant message () =
        setToasts (toastsRef.current @ [ (Guid.NewGuid(), message, variant, false) ])
    let remove (id: Guid) =
        setToasts (toastsRef.current |> List.filter (fun (tid, _, _, _) -> tid <> id))
    // Phase 1: markiere exiting (Exit-Animation startet); Phase 2: entferne nach Ablauf.
    let dismiss (id: Guid) =
        setToasts (toastsRef.current |> List.map (fun (tid, m, v, ex) ->
            if tid = id then (tid, m, v, true) else (tid, m, v, ex)))
        Fable.Core.JS.setTimeout (fun () -> remove id) Types.Toast.exitDurationMs |> ignore

    // Toast mit Aktion (ledger-th36h): (Id, Exiting). Klick auf "Rückgängig" schließt ihn.
    let undoToasts, setUndoToasts = React.useState ([]: (Guid * bool) list)
    let undoRef = React.useRef undoToasts
    undoRef.current <- undoToasts
    let fireUndo () =
        setUndoToasts (undoRef.current @ [ (Guid.NewGuid(), false) ])
    let dismissUndo (id: Guid) =
        setUndoToasts (undoRef.current |> List.map (fun (tid, ex) -> if tid = id then (tid, true) else (tid, ex)))
        Fable.Core.JS.setTimeout
            (fun () -> setUndoToasts (undoRef.current |> List.filter (fun (tid, _) -> tid <> id)))
            Types.Toast.exitDurationMs
        |> ignore
    let undoItems : Toast.ToastItem list =
        undoToasts
        |> List.map (fun (id, exiting) ->
            { Id = id
              Message = "REWE Mitte geprüft"
              Variant = Toast.Success
              Exiting = exiting
              Action = Some { Label = "Rückgängig" } })

    section "17. Toast" [
        note "Flüchtiges Feedback nach Aktion, mit sanftem Abgang (Exit-Animation). Blockierende Fehler → ErrorDisplay/Modal. Optional mit Aktion (z. B. Rückgängig), dann über der Tab-Bar. styleguide.md §5/§6."
        row [
            // data-ds-card-trigger: Export-Skript klickt hier, damit ein Toast sichtbar
            // ist, bevor components-toast.html geschrieben wird. Toast.renderList ist
            // (anders als BottomSheet/Modal) nicht portalt, das data-ds-card unten
            // erfasst also den echten Container.
            Html.span [
                prop.custom ("data-ds-card-trigger", "components-toast")
                prop.children [ Button.secondary "Erfolg" (fire Toast.Success "Gespeichert.") ]
            ]
            Button.secondary "Fehler" (fire Toast.Error "Speichern fehlgeschlagen.")
            Button.secondary "Info" (fire Toast.Info "3 neue Transaktionen.")
            Button.secondary "Mit Rückgängig" fireUndo
        ]
        Html.div [
            prop.custom ("data-ds-card", "components-toast")
            prop.custom ("data-ds-card-name", "Toast")
            prop.custom ("data-ds-card-subtitle", "Flüchtige Rückmeldung als Erfolg, Fehler oder Info")
            prop.custom ("data-ds-group", "Feedback")
            prop.children [
                Toast.renderList toasts dismiss
                Toast.renderItems undoItems dismissUndo dismissUndo
            ]
        ]
    ]

/// BottomSheet-Demo (mobiles Picker-Muster, ADR 0005) per useState.
[<ReactComponent>]
let private BottomSheetDemo () =
    let isOpen, setOpen = React.useState false
    let chosen, setChosen = React.useState ""

    section "18. BottomSheet (mobiles Picker-Muster)" [
        note "Mobile Auswahl-/Picker-Flows (keyboard-aware, visual-viewport-Anker, Click-Commit). Baust du ein neues Sheet, erbe das Muster — erfinde Anker/Commit nicht neu. ADR 0005, styleguide.md §7.1."
        row [
            // data-ds-card-trigger: Export-Skript (design-system-t82sg) öffnet das Sheet
            // über diesen stabilen Selektor, bevor es components-bottomsheet.html schreibt.
            // BottomSheet.view portalt nach document.body, ein umschließendes data-ds-card
            // würde das Sheet also nicht mit erfassen.
            Html.span [
                prop.custom ("data-ds-card-trigger", "components-bottomsheet")
                prop.custom ("data-ds-card-name", "BottomSheet")
                prop.custom ("data-ds-card-subtitle", "Mobiles Auswahl-Sheet für die Kontowahl")
                prop.children [ Button.secondary "Konto wählen" (fun () -> setOpen true) ]
            ]
            if chosen <> "" then Badge.info chosen
        ]
        // Options-Zeile (ledger-skpcx): gewählt mit Haken, ungewählt ohne. Sortier-Sheet im Kassenbuch.
        Html.div [
            prop.custom ("data-ds-card-name", "BottomSheet.optionRow")
            prop.className "max-w-sm"
            prop.children [
                BottomSheet.sectionTitle "Options-Zeile (Sortieren)"
                BottomSheet.optionRow "Neueste zuerst" true ignore
                BottomSheet.optionRow "Älteste zuerst" false ignore
            ]
        ]
        BottomSheet.view
            {
                IsOpen = isOpen
                OnClose = fun () -> setOpen false
                Title = "Konto wählen"
                Subtitle = Some "Strukturiert auswählen statt frei tippen (ADR 0004)"
                Footer = None
            }
            [
                BottomSheet.sectionTitle "Konten"
                Html.div [
                    prop.className "flex flex-wrap gap-2 p-2"
                    // Mapped Chip-Liste: jeder Chip braucht einen stabilen Key. chipButton
                    // (shared) reicht keinen `prop.key` durch → keyed-Fragment je Konto-Name.
                    prop.children (
                        [ "Girokonto"; "Bar"; "Kreditkarte" ]
                        |> List.map (fun acc ->
                            React.keyedFragment (acc, [
                                BottomSheet.chipButton acc false (fun () ->
                                    setChosen acc
                                    setOpen false)
                            ]))
                    )
                ]
            ]
    ]

/// Gemeinsames Beispiel-Fixture für die Kategorien-Demos (Einzelauswahl-Picker und
/// Mehrfachauswahl): fünf erfundene, plausible Kategorien in zwei Gruppen
/// (Id, Gruppe, Name, Available).
let private demoCategoryData : (string * string * string * decimal) list = [
    "lebensmittel", "Lebenshaltung", "Lebensmittel", 128.40m
    "freizeit", "Lebenshaltung", "Freizeit", -32.10m
    "sonstiges", "Lebenshaltung", "Sonstiges", 0m
    "miete", "Fixkosten", "Miete", 850.00m
    "versicherung", "Fixkosten", "Versicherung", -15.50m
]

/// Kategorienauswahl-Demo (design-system-y6gkd): `BottomSheet.categoryPicker` mit
/// erfundenen Beispieldaten. Zeigt in einem offenen Sheet beide Zustände, die Roman
/// im Kassenbuch nutzt (Ablauf 4 "Kategorie ändern", ledger-xbrht): oben die Chips
/// "Vorgeschlagen"/"Zuletzt verwendet", darunter die Gruppenliste mit Available
/// rechts (positiv/negativ/null) und Suchfeld. Die Kategorien sind erfundene,
/// plausible Beispiele, keine aus Romans echtem Budget.
[<ReactComponent>]
let private CategoryPickerDemo () =
    let isOpen, setOpen = React.useState false
    let chosen, setChosen = React.useState ""

    let categories : BottomSheet.CategoryPickerOption list =
        demoCategoryData
        |> List.map (fun (id, group, name, amount) ->
            { Id = id; Label = $"{group}: {name}"; Available = Some { Amount = amount; Currency = "EUR" } })

    section "19. Kategorienauswahl (Kategorie ändern)" [
        note "Kategorie ändern im Kassenbuch (Ablauf 4, ledger-xbrht): Vorschläge und Zuletzt \
              verwendet zuerst, darunter alle Kategorien nach Gruppe mit Available rechts (grün \
              positiv, rot negativ, gedämpft null). Derselbe Picker wie im echten Flow, kein \
              eigens gebautes Mockup. ADR 0004, ADR 0005, styleguide.md §7.1/§7.2."
        row [
            // data-ds-card-trigger: Export-Skript öffnet den Picker über diesen stabilen
            // Selektor, bevor es components-categorypicker.html schreibt.
            // CategoryPickerInternal portalt (wie BottomSheet) nach document.body, ein
            // umschließendes data-ds-card würde den Picker also nicht mit erfassen.
            Html.span [
                prop.custom ("data-ds-card-trigger", "components-categorypicker")
                prop.custom ("data-ds-card-name", "Kategorienauswahl")
                prop.custom ("data-ds-card-subtitle", "Sheet mit Chips für Vorschläge und Zuletzt verwendet sowie Gruppenliste mit Available")
                prop.children [ Button.secondary "Kategorie wählen" (fun () -> setOpen true) ]
            ]
            if chosen <> "" then Badge.info chosen
        ]
        BottomSheet.categoryPicker
            isOpen
            "REWE Markt"
            categories
            [ "lebensmittel"; "freizeit" ]
            [ "miete" ]
            (fun catId ->
                setChosen catId
                setOpen false)
            (fun () -> setOpen false)
    ]

/// Kategorie-Picker mit Einfachauswahl (design-system-gffps): Popover (PC) und Sheet (Handy)
/// zeigen denselben Inhalt, dazu die Karte "Budget nach dem Wechsel". Erfundene Beispieldaten.
[<ReactComponent>]
let private CategoryPickerSingleDemo () =
    let popoverOpen, setPopoverOpen = React.useState false
    let sheetOpen, setSheetOpen = React.useState false
    let current, setCurrent = React.useState (Some (YnabCategoryId (Guid.Parse "00000000-0000-0000-0000-000000002002")))
    let chosenText, setChosenText = React.useState ""
    let plainOpen, setPlainOpen = React.useState false
    let loadingOpen, setLoadingOpen = React.useState false
    let failedOpen, setFailedOpen = React.useState false
    let retryCount, setRetryCount = React.useState 0
    let aboveOpen, setAboveOpen = React.useState false
    let selectionSheetOpen, setSelectionSheetOpen = React.useState false
    // Zählt die OnSelect-Aufrufe, damit "genau einmal pro Klick" an der Galerie prüfbar ist.
    let selectCount, setSelectCount = React.useState 0
    let selectCounter = React.useRef 0

    let guidOf (i: int) = Guid.Parse ("00000000-0000-0000-0000-" + (string (2000 + i)).PadLeft(12, '0'))

    let categories : YnabCategory list =
        (demoCategoryData
         |> List.mapi (fun i (_, group, name, amount) ->
             { Id = YnabCategoryId (guidOf (i + 1))
               Name = name
               GroupName = group
               Available = Some { Amount = amount; Currency = "EUR" }
               Hidden = false }))
        @ [ { Id = YnabCategoryId (guidOf 99)
              Name = "Altes Sparziel"
              GroupName = "Fixkosten"
              Available = None
              Hidden = true } ]

    let nameOf (id: YnabCategoryId option) =
        match id with
        | None -> CategoryPicker.withoutCategoryLabel
        | Some i -> categories |> List.tryFind (fun c -> c.Id = i) |> Option.map (fun c -> c.Name) |> Option.defaultValue ""

    let pickerProps (close: unit -> unit) : CategoryPicker.CategoryPickerProps =
        { Categories = categories
          Current = current
          Suggested = [ YnabCategoryId (guidOf 1); YnabCategoryId (guidOf 3); YnabCategoryId (guidOf 2) ]
          StandLabel = Some "Stand 14:02"
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect =
            fun choice ->
                selectCounter.current <- selectCounter.current + 1
                setSelectCount selectCounter.current
                setCurrent choice
                setChosenText (nameOf choice)
                close ()
          OnClose = close }

    section "26. Kategorie-Picker (Einfachauswahl)" [
        note "Kategorie ändern im Buchungs-Detail (Ablauf 4): Suche, Abschnitt Vorgeschlagen zum Payee \
              (höchstens drei), Zeile Ohne Kategorie, Gruppen mit Available rechts, versteckte \
              Kategorien eingeklappt. Ein Klick übernimmt sofort, Pfeile und Enter wählen, Esc \
              schließt nur den Picker. Am PC als Popover am Auslöser, am Handy als Sheet. Darunter \
              die Karte Budget nach dem Wechsel. ADR 0005, ADR 0053, styleguide.md §7.1."
        row [
            Html.div [
                prop.className "relative"
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-popover-trigger")
                        prop.children [ Button.secondary "Popover öffnen (PC)" (fun () -> setPopoverOpen (not popoverOpen)) ]
                    ]
                    Html.div [
                        prop.custom ("data-ds-demo", "category-picker-with-stand")
                        prop.children [ CategoryPicker.popover popoverOpen (pickerProps (fun () -> setPopoverOpen false)) ]
                    ]
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-plain")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-plain-trigger")
                        prop.children [ Button.secondary "Popover ohne Stand-Zeile" (fun () -> setPlainOpen (not plainOpen)) ]
                    ]
                    CategoryPicker.popover plainOpen { pickerProps (fun () -> setPlainOpen false) with StandLabel = None }
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-loading")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-loading-trigger")
                        prop.children [ Button.secondary "Popover lädt" (fun () -> setLoadingOpen (not loadingOpen)) ]
                    ]
                    CategoryPicker.popover
                        loadingOpen
                        { pickerProps (fun () -> setLoadingOpen false) with
                            Categories = []
                            LoadState = CategoryPicker.PickerLoading }
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-failed")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-failed-trigger")
                        prop.children [ Button.secondary "Popover fehlgeschlagen" (fun () -> setFailedOpen (not failedOpen)) ]
                    ]
                    CategoryPicker.popover
                        failedOpen
                        { pickerProps (fun () -> setFailedOpen false) with
                            Categories = []
                            LoadState = CategoryPicker.PickerFailed
                            OnRetry = fun () -> setRetryCount (retryCount + 1) }
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-above")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-above-trigger")
                        prop.children [ Button.secondary "Popover der Auswahl (PC, nach oben, ohne Haken)" (fun () -> setAboveOpen (not aboveOpen)) ]
                    ]
                    CategoryPicker.selectionPopover aboveOpen { pickerProps (fun () -> setAboveOpen false) with Current = None }
                ]
            ]
            Html.span [
                prop.custom ("data-ds-demo", "category-picker-selection-sheet-trigger")
                prop.children [ Button.secondary "Sheet der Auswahl (Handy, ohne Haken)" (fun () -> setSelectionSheetOpen true) ]
            ]
            Html.span [
                prop.custom ("data-ds-demo", "category-picker-retry-count")
                prop.className "text-xs text-text-muted"
                prop.text (string retryCount)
            ]
            Html.span [
                prop.custom ("data-ds-demo", "category-picker-sheet-trigger")
                prop.children [ Button.secondary "Sheet öffnen (Handy)" (fun () -> setSheetOpen true) ]
            ]
            if chosenText <> "" then Badge.info chosenText
            Html.span [
                prop.custom ("data-ds-demo", "category-picker-select-count")
                prop.className "text-xs text-text-muted"
                prop.text (string selectCount)
            ]
        ]
        CategoryPicker.sheet sheetOpen (Some "REWE Markt · -42,10 €") (pickerProps (fun () -> setSheetOpen false))
        CategoryPicker.selectionSheet
            selectionSheetOpen
            (Some "12 Buchungen · -340,20 €")
            { pickerProps (fun () -> setSelectionSheetOpen false) with Current = None }
        Html.div [
            prop.className "max-w-sm pt-4"
            prop.children [
                CategoryPicker.CategoryChangeCard.view [
                    { Label = "Alltag: Haushalt"; OldText = Some "412,10 €"; NewText = Some "494,00 €" }
                    { Label = "Wohnen: Möbel"; OldText = Some "120,00 €"; NewText = Some "38,10 €" }
                ]
            ]
        ]
    ]

/// Split-Editor (ledger-pafkp): Summe, Zeilen, "Zeile hinzufügen", Fußleisten und die Konto-Auswahl
/// für das Übertrag-Ziel. Erfundene Beispieldaten, Zustand lokal.
[<ReactComponent>]
let private SplitEditorDemo () =
    let amountA, setAmountA = React.useState "23,40"
    let amountB, setAmountB = React.useState ""
    let accountPopoverOpen, setAccountPopoverOpen = React.useState false
    let accountSheetOpen, setAccountSheetOpen = React.useState false
    let chosenAccount, setChosenAccount = React.useState ""

    let total = 81.90m
    let parse (text: string) =
        match Types.parseAmountInput text with
        | Some v -> v
        | None -> 0m
    let rest = total - parse amountA - parse amountB
    let balanced = rest = 0m
    let text (value: decimal) = Money.formatAmountText false true value "EUR"

    let accounts : YnabAccount list =
        [ "Tagesgeld"; "Bargeld"; "Gemeinschaftskonto" ]
        |> List.mapi (fun i name ->
            { Id = YnabAccountId (Guid.Parse ("00000000-0000-0000-0000-" + (string (3000 + i)).PadLeft(12, '0')))
              Name = name
              Balance = { Amount = 0m; Currency = "EUR" }
              OnBudget = true
              Closed = false })

    let accountProps (close: unit -> unit) : AccountPicker.AccountPickerProps =
        { Accounts = accounts
          OnSelect =
            fun id ->
                setChosenAccount (accounts |> List.find (fun a -> a.Id = id) |> fun a -> a.Name)
                close ()
          OnClose = close }

    let lineProps (index: int) (label: string) (chosen: bool) (amount: string) (setAmount: string -> unit) : SplitEditor.LineProps =
        { Index = index
          TargetLabel = label
          TargetChosen = chosen
          IsTransfer = false
          AmountText = amount
          Memo = ""
          Disabled = false
          CanRemove = true
          OnPickCategory = ignore
          OnPickAccount = fun () -> setAccountPopoverOpen (not accountPopoverOpen)
          OnAmount = setAmount
          OnMemo = ignore
          OnRest = fun () -> setAmount (Components.Ledger.SplitEditor.amountText (total - parse amountA - parse amountB + parse amount))
          OnRemove = ignore }

    section "27. Split-Editor (Buchung aufteilen)" [
        note "Der Editor im Buchungs-Detail (Ablauf 4): Gesamtbetrag und Rest über den Zeilen, je Zeile \
              Kategorie oder Übertrag-Ziel, Betrag mit Rest-Knopf und Memo. Der Rest ist neutral bei \
              0,00 € und rot sonst, Speichern ist erst bei Rest 0,00 € aktiv, Zeile hinzufügen \
              dann ausgegraut. Genau eine primäre Aktion (Speichern). Das Übertrag-Ziel wählt die \
              Konto-Auswahl, am PC als Popover, am Handy als zweites Sheet. ADR 0005, ADR 0006, ADR 0049."
        Html.div [
            prop.custom ("data-ds-demo", "split-editor")
            prop.className "max-w-md rounded-xl border border-border-subtle overflow-hidden"
            prop.children [
                SplitEditor.summary
                    { TotalText = text total
                      RestText = text rest
                      RestWarn = not balanced
                      Balanced = balanced }
                Html.div [
                    prop.className "relative"
                    prop.children [
                        SplitEditor.line (lineProps 0 "Wohnen: Möbel" true amountA setAmountA)
                        AccountPicker.popoverInPanel accountPopoverOpen (accountProps (fun () -> setAccountPopoverOpen false))
                    ]
                ]
                SplitEditor.line (lineProps 1 "Kategorie oder Konto wählen" false amountB setAmountB)
                SplitEditor.addLineButton (not balanced) ignore
                SplitEditor.footer balanced false ignore ignore
            ]
        ]
        row [
            Html.span [
                prop.custom ("data-ds-demo", "account-picker-sheet-trigger")
                prop.children [ Button.secondary "Konto-Auswahl als Sheet (Handy)" (fun () -> setAccountSheetOpen true) ]
            ]
            if chosenAccount <> "" then Badge.info chosenAccount
        ]
        AccountPicker.sheetLayered accountSheetOpen (accountProps (fun () -> setAccountSheetOpen false))
        Html.div [
            prop.custom ("data-ds-demo", "split-editor-mobile-save")
            prop.className "max-w-sm"
            prop.children [ SplitEditor.saveButton true balanced false ignore ]
        ]
    ]

/// Swipe-Demo: eine wischbare Listenzeile (mobile).
[<ReactComponent>]
let private SwipeDemo () =
    let swiped, setSwiped = React.useState false

    section "20. Swipe (wischbare Listenzeile, mobile)" [
        note "Wischbare Listenzeilen mit enthüllter Aktion (mobile). Der Swipe ist ein Accelerator — Buttons bleiben der accessible Pfad. styleguide.md §5/§7.1."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-swipe.html.
        // Erfasst die Ruhelage (kein Drag): das Playwright-Skript simuliert keine
        // Touch-Geste, die enthüllte Aktion bleibt eine visuelle Doku-Lücke.
        Html.div [
            prop.custom ("data-ds-card", "components-swipe")
            prop.custom ("data-ds-card-name", "Swipe")
            prop.custom ("data-ds-card-subtitle", "Wischbare Listenzeile für mobile Ansichten")
            prop.custom ("data-ds-group", "Components")
            prop.children [
                Swipe.SwipeableRow {
                    ActionLabel = "Überspringen"
                    ActionClass = "skip"
                    OnCommit = fun () -> setSwiped true
                    Children =
                        Html.div [
                            prop.className "tx-row flex items-center justify-between bg-surface-card border border-border-subtle rounded-lg p-4"
                            prop.children [
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children [
                                        Html.span [ prop.className "text-sm font-semibold text-text-primary"; prop.text "REWE Markt" ]
                                        Html.span [ prop.className "text-xs text-text-muted"; prop.text "Nach links wischen zum Überspringen" ]
                                    ]
                                ]
                                Money.simple -42.90m "EUR"
                            ]
                        ]
                }
            ]
        ]
        if swiped then Badge.success "Übersprungen (Demo)"
    ]

// ============================================
// Navigation
// ============================================

/// Navigation-Sektion (design-system-t82sg): die App-Navigation fehlte bisher in der
/// Galerie. Rendert `Navigation.desktopNavPreview`/`mobileNavPreview`, dieselbe
/// Chrome wie `Navigation.desktopNav`/`mobileNav`, nur ohne `fixed`-Positionierung,
/// damit sie sich als begrenzter Ausschnitt statt als Overlay über die ganze
/// Galerie-Seite legt. Rein präsentational: `currentPage`/`onNavigate` sind fix bzw.
/// no-op, es gibt keine echte Navigation von hier aus.
let private navigationSection =
    section "21. Navigation" [
        note "Seitennavigation. Desktop: Top-Navbar. Mobile: Header + Bottom-Nav. In der App fix positioniert; hier als begrenzte Vorschau eingebettet. styleguide.md §5."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-navigation.html.
        Html.div [
            prop.custom ("data-ds-card", "components-navigation")
            prop.custom ("data-ds-card-name", "Navigation")
            prop.custom ("data-ds-card-subtitle", "Hauptnavigation der App")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                subTitle "Desktop"
                Navigation.desktopNavPreview Navigation.Ledger ignore
                subTitle "Mobile"
                Html.div [
                    prop.className "max-w-sm"
                    prop.children [ Navigation.mobileNavPreview Navigation.Ledger ignore ]
                ]
            ]
        ]
    ]

// ============================================
// 8. Voice / Muster-Hinweise
// ============================================

let private voiceSection =
    section "22. Voice & Muster" [
        note "Die UI spricht Deutsch, direkt, knapp, ohne Füllwörter. Beträge sprechen für sich (Vorzeichen + Farbe). \
              Empty States sagen, was als Nächstes zu tun ist. Ubiquitous Language der BCs (Import, Kategorie, Sync, Split, Transfer) \
              konsistent verwenden. Vollständige Regeln & ADR-Begründungen: standards/frontend/styleguide.md §7/§8 \
              (ADR 0004 Picker-statt-Freitext, ADR 0005 Visual-Viewport-Sheet + Click-Commit)."
    ]

// ============================================
// Filterleiste (ledger-j8xq4, design-system-bz0gn)
// ============================================

/// ChipBar: aktive und inaktive Chips, mit und ohne "×", "Alle Filter löschen".
let private chipBarSection =
    section "23. ChipBar" [
        note "Filterleiste des Kassenbuchs: jeder Chip ist ein Rahmen mit zwei Knöpfen \
              (Auswahl öffnen, Filter entfernen mit ×). Aktive Chips in Teal, inaktive \
              gedämpft. \"Alle Filter löschen\" steht am Ende und erscheint nur bei aktivem \
              Filter. ADR 0043, styleguide.md §7.3."
        Html.div [
            prop.custom ("data-ds-card", "components-chipbar")
            prop.custom ("data-ds-card-name", "ChipBar")
            prop.custom ("data-ds-card-subtitle", "Filter-Chips mit Entfernen-Knopf und Alle Filter löschen")
            prop.custom ("data-ds-group", "Components")
            prop.children [
                ChipBar.bar None [
                    ChipBar.chip { Label = "Konto: Girokonto"; IsActive = true; OnClick = ignore; OnRemove = Some ignore }
                    ChipBar.chip { Label = "Kategorie (2)"; IsActive = true; OnClick = ignore; OnRemove = Some ignore }
                    ChipBar.chip { Label = "Zeitraum"; IsActive = false; OnClick = ignore; OnRemove = Some ignore }
                    ChipBar.chip { Label = "Zu prüfen"; IsActive = true; OnClick = ignore; OnRemove = None }
                    ChipBar.chip { Label = "Ohne Kategorie"; IsActive = false; OnClick = ignore; OnRemove = None }
                    ChipBar.clearAllButton ignore
                ]
            ]
        ]
    ]

/// FilterPopover: ein Chip mit offenem Popover, Platzhalter-Inhalt.
[<ReactComponent>]
let private FilterPopoverDemo () =
    let isOpen, setOpen = React.useState true

    section "24. FilterPopover" [
        note "PC-Popover unter seinem Chip (am Handy hängt stattdessen ein BottomSheet, ADR 0043). \
              Klick außerhalb und Esc schließen es im Kassenbuch, ohne zu filtern. In dieser Demo \
              bleibt es offen (sonst würde der Export es beim Einsammeln der anderen Karten \
              schließen), der Chip schaltet es um."
        Html.div [
            prop.custom ("data-ds-card", "components-filterpopover")
            prop.custom ("data-ds-card-name", "FilterPopover")
            prop.custom ("data-ds-card-subtitle", "Popover unter einem Filter-Chip, schließt per Esc oder Klick außerhalb")
            prop.custom ("data-ds-group", "Components")
            // Das Popover ist absolut positioniert und zählt nicht zur Höhe des Wrappers.
            prop.className "min-h-[14rem]"
            prop.children [
                ChipBar.bar None [
                    ChipBar.chipWithPopover
                        (ChipBar.chip {
                            Label = "Zeitraum"
                            IsActive = isOpen
                            OnClick = fun () -> setOpen (not isOpen)
                            OnRemove = None })
                        (FilterPopover.view
                            { IsOpen = isOpen; OnClose = ignore; WidthClassName = FilterPopover.categoryWidthClass }
                            [
                                Html.p [
                                    prop.className "text-sm text-text-secondary"
                                    prop.text "Platzhalter-Inhalt des Popovers."
                                ]
                            ])
                ]
            ]
        ]
    ]

/// FilterPopover als kleines Aktionsmenü: `menuItem` im Popover (kommt aus dem Zellmenü
/// "Nach X filtern", ledger-rfq5p; das Kassenbuch nutzt es seit ADR 0053 nicht mehr).
[<ReactComponent>]
let private FilterPopoverMenuDemo () =
    let isOpen, setOpen = React.useState true

    Html.div [
        prop.custom ("data-ds-card", "components-filterpopover-menuitem")
        prop.custom ("data-ds-card-name", "FilterPopover menuItem")
        prop.custom ("data-ds-card-subtitle", "Kleines Aktionsmenü: eine volle Zeile je Eintrag, role=menuitem, auf eine Zeile gekürzt")
        prop.custom ("data-ds-group", "Components")
        prop.className "min-h-[10rem]"
        prop.children [
            Html.div [
                prop.className "relative inline-block"
                prop.children [
                    Html.button [
                        prop.type'.button
                        prop.className "text-left text-text-primary hover:brightness-125 cursor-pointer"
                        prop.custom ("aria-haspopup", "menu")
                        prop.custom ("aria-expanded", (if isOpen then "true" else "false"))
                        prop.text "REWE Mitte"
                        prop.onClick (fun _ -> setOpen (not isOpen))
                    ]
                    FilterPopover.view
                        { IsOpen = isOpen; OnClose = ignore; WidthClassName = FilterPopover.menuWidthClass }
                        [
                            FilterPopover.menuItem "Nach REWE Mitte filtern" ignore
                            FilterPopover.menuItem "Nach Alltag: Lebensmittel mit einem sehr langen Namen filtern" ignore
                        ]
                ]
            ]
        ]
    ]

/// Buchungs-Detail (ledger-q1hw2, ADR 0053): die Bausteine des Panels und des Sheets im
/// Kassenbuch. Der Geprüft-Schalter hat lokalen Zustand.
[<ReactComponent>]
let private DetailDemo () =
    let reviewed, setReviewed = React.useState false

    let card (id: string) (name: string) (subtitle: string) (children: ReactElement list) =
        Html.div [
            prop.custom ("data-ds-card", id)
            prop.custom ("data-ds-card-name", name)
            prop.custom ("data-ds-card-subtitle", subtitle)
            prop.custom ("data-ds-group", "Components")
            prop.className "mb-6 overflow-hidden rounded-xl border border-border-subtle"
            prop.children children
        ]

    section "25. Buchungs-Detail" [
        note "Das Detail einer Buchung: Seitenpanel (420 px) am PC, BottomSheet am Handy und in der \
              Kompaktliste. Hinweisleiste mit Grund und genau einer Aktion, Felder, Geprüft-Schalter, \
              Herkunft und Zeitleiste der Historie. Farbe nur für den Hinweis und den Punkt der \
              Zeitleiste (ADR 0049, ADR 0053)."

        card "components-detail-notice" "Detail: Hinweisleiste" "Rot ohne Aktion, Orange ohne Aktion, Orange mit Aktion" [
            Detail.notice
                { Tone = Detail.NoticeRed
                  Badge = "In YNAB gelöscht"
                  Reason = "Gelöscht in YNAB am 11.09.2026 08:20. Zählt nicht zum Kontostand."
                  Action = None }
            Detail.notice
                { Tone = Detail.NoticeOrange
                  Badge = "Noch nicht in YNAB"
                  Reason = "Push gescheitert am 10.09.2026 18:02: Netzwerkfehler. BB versucht es beim nächsten Lauf erneut."
                  Action = None }
            Detail.notice
                { Tone = Detail.NoticeOrange
                  Badge = "Prüfvermerk nicht in YNAB"
                  Reason = "YNAB hat den Prüfvermerk nicht angenommen (Fehler 500)."
                  Action = Some { Label = "Erneut senden"; Busy = false; OnClick = ignore } }
        ]

        card "components-detail-fields" "Detail: Felder, Filter-Knopf, Schalter, Herkunft" "Feldzeile mit Filter-Knopf, Split, Geprüft-Schalter, Herkunft" [
            Detail.fieldRow
                "styleguide-detail-category"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textPrimary "Alltag: Lebensmittel")
                (Some (Detail.filterButton "Nur Buchungen in Alltag: Lebensmittel zeigen" ignore))
            // Kategorie mit Pfeil (ledger-sfab0): änderbar und gesperrt mit Hinweistext am Knopf.
            Detail.fieldRowWith
                (Some ignore)
                "styleguide-detail-category-arrow"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textPrimary "Alltag: Lebensmittel")
                (Some (Detail.pickerArrow "Kategorie ändern" false None ignore))
            Detail.fieldRow
                "styleguide-detail-category-locked"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textMuted "Keine Kategorie")
                (Some (Detail.pickerArrow "Kategorie ändern" false (Some "Übertrag zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu") ignore))
            Detail.fieldRow
                "styleguide-detail-split"
                "Split"
                (Detail.splitLines [ "Lebensmittel", "-30,50 €"; "Übertrag: Tagesgeld", "-20,00 €" ])
                None
            // Feld Split mit festem Knopf (ledger-pafkp): unaufgeteilt, aufgeteilt und gesperrt.
            Detail.fieldRow
                "styleguide-detail-split-none"
                "Split"
                (Detail.valueText Tokens.Colors.textMuted "Kein Split")
                (Some (Detail.splitActionButton "Aufteilen" None ignore))
            Detail.fieldRow
                "styleguide-detail-split-change"
                "Split"
                (Detail.splitLines [ "Wohnen: Möbel", "-23,40 €"; "Alltag: Haushalt", "-58,50 €" ])
                (Some (Detail.splitActionButton "Ändern" None ignore))
            Detail.fieldRow
                "styleguide-detail-split-locked"
                "Split"
                (Html.div [
                    prop.className "flex min-w-0 flex-col"
                    prop.children [
                        Detail.valueText Tokens.Colors.textMuted "Kein Split"
                        Detail.splitLockHint "Abgeglichene Buchungen lassen sich nicht aufteilen"
                    ]
                 ])
                (Some (Detail.splitActionButton "Aufteilen" (Some "Abgeglichene Buchungen lassen sich nicht aufteilen") ignore))
            // Flag-Zeile mit festem Knopf (ledger-xfjcp): Lena fragen, zurücknehmen und gesperrt.
            Detail.fieldRow
                "styleguide-detail-flag-ask"
                "Flag"
                (Detail.valueText Tokens.Colors.textMuted "Kein Flag")
                (Some (Detail.fieldActionButton "styleguide-detail-flag-action" "Lena fragen" None false ignore))
            Detail.fieldRow
                "styleguide-detail-flag-withdraw"
                "Flag"
                (Detail.valueText Tokens.Colors.textPrimary "Rückfrage Lena")
                (Some (Detail.fieldActionButton "styleguide-detail-flag-action" "Rückfrage zurücknehmen" None false ignore))
            Detail.fieldRow
                "styleguide-detail-flag-locked"
                "Flag"
                (Html.div [
                    prop.className "flex min-w-0 flex-col"
                    prop.children [
                        Detail.valueText Tokens.Colors.textMuted "Kein Flag"
                        Detail.fieldLockHint "styleguide-detail-flag-lock-hint" "In den Settings die Flag-Farbe für Lena wählen"
                    ]
                 ])
                (Some (Detail.fieldActionButton "styleguide-detail-flag-action" "Lena fragen" (Some "In den Settings die Flag-Farbe für Lena wählen") false ignore))
            // Memo-Zeile mit festem Knopf (ledger-fa1fp): ohne Memo, mit Memo und gesperrt.
            Detail.fieldRow
                "styleguide-detail-memo"
                "Memo"
                (Detail.valueText Tokens.Colors.textMuted "Kein Memo")
                (Some (Detail.fieldActionButton "styleguide-detail-memo-action" "Memo hinzufügen" None false ignore))
            Detail.fieldRow
                "styleguide-detail-memo-set"
                "Memo"
                (Detail.valueText Tokens.Colors.textPrimary "Werkzeug + Lampe")
                (Some (Detail.fieldActionButton "styleguide-detail-memo-action" "Bearbeiten" None false ignore))
            Detail.fieldRow
                "styleguide-detail-memo-locked"
                "Memo"
                (Html.div [
                    prop.className "flex min-w-0 flex-col"
                    prop.children [
                        Detail.valueText Tokens.Colors.textMuted "Werkzeug + Lampe"
                        Detail.fieldLockHint "styleguide-detail-memo-lock-hint" "In YNAB gelöscht, das Memo lässt sich nicht ändern"
                    ]
                 ])
                (Some (Detail.fieldActionButton "styleguide-detail-memo-action" "Bearbeiten" (Some "In YNAB gelöscht, das Memo lässt sich nicht ändern") false ignore))
            Detail.reviewedSwitch "Geprüft" reviewed false setReviewed
            Detail.originRow (
                Badge.view { Badge.defaultProps with Text = "Comdirect-Import"; Variant = Badge.Info; Size = Badge.Small }
            )
        ]

        card "components-detail-memo" "Detail: Memo bearbeiten" "Eingabefeld mit Zähler, Speichern und Abbrechen; zu langer Text; Hinweis Memo nicht in YNAB" [
            Detail.memoEditor
                { Value = "Werkzeug + Lampe"
                  OnChange = ignore
                  OnSave = ignore
                  OnCancel = ignore
                  Busy = false
                  Problem = None
                  MaxLength = ynabMemoMaxLength
                  SaveDisabled = false }
            Detail.memoEditor
                { Value = String('x', ynabMemoMaxLength + 3)
                  OnChange = ignore
                  OnSave = ignore
                  OnCancel = ignore
                  Busy = false
                  Problem = Some memoTooLongReason
                  MaxLength = ynabMemoMaxLength
                  SaveDisabled = true }
            Detail.notice
                { Tone = Detail.NoticeOrange
                  Badge = "Memo nicht in YNAB"
                  Reason = "Keine Verbindung zu YNAB: Zeitüberschreitung."
                  Action = Some { Label = "Erneut senden"; Busy = false; OnClick = ignore } }
        ]

        card "components-detail-timeline" "Detail: Zeitleiste" "Punktfarbe nach Urheber, neueste Zeile oben" [
            Detail.sectionLabel "HISTORIE"
            Detail.timeline
                [ { Key = "4"; Dot = Detail.DotRed; Text = "In YNAB gelöscht"; Stamp = "11.09.2026 08:20 · YNAB-Spiegel" }
                  { Key = "3"; Dot = Detail.DotGreen; Text = "In BB als geprüft markiert"; Stamp = "10.09.2026 18:10 · In BB entschieden" }
                  { Key = "2"; Dot = Detail.DotPurple; Text = "Regel: Kategorie Lebensmittel gesetzt"; Stamp = "10.09.2026 17:55 · Regel" }
                  { Key = "1"; Dot = Detail.DotTeal; Text = "Comdirect-Import: Buchung erfasst"; Stamp = "10.09.2026 17:55 · Comdirect-Import" }
                  { Key = "0"; Dot = Detail.DotGrey; Text = "Kategorie in YNAB geändert"; Stamp = "10.09.2026 17:50 · YNAB-Spiegel" } ]
        ]

        card "components-detail-panel" "Detail: Seitenpanel" "Kopf mit Betrag und Konto · Datum, darunter Payee und Kategorie mit Filter-Knopf in einer Spalte (ab md sichtbar)" [
            Html.div [
                prop.className "relative h-[28rem] overflow-hidden"
                prop.children [
                    Detail.panel
                        "Detail REWE Mitte"
                        [ Detail.head
                              (Detail.amount -48.12m "EUR" false)
                              "Girokonto Comdirect · 10.09.2026"
                              (Some ignore)
                          Detail.scrollBody [
                              Detail.fieldRow
                                  "styleguide-detail-panel-payee"
                                  "Payee"
                                  (Detail.valueText Tokens.Colors.textPrimary "REWE Mitte")
                                  (Some (Detail.filterButton "Nur Buchungen von REWE Mitte zeigen" ignore))
                              Detail.fieldRow
                                  "styleguide-detail-panel-category"
                                  "Kategorie"
                                  (Detail.valueText Tokens.Colors.textPrimary "Alltag: Lebensmittel")
                                  (Some (Detail.filterButton "Nur Buchungen in Alltag: Lebensmittel zeigen" ignore))
                              Detail.fieldRow
                                  "styleguide-detail-panel-memo"
                                  "Memo"
                                  (Detail.valueText Tokens.Colors.textPrimary "Wocheneinkauf")
                                  None
                              Detail.originRow (
                                  Badge.view { Badge.defaultProps with Text = "Regel"; Variant = Badge.Info; Size = Badge.Small }
                              )
                          ] ]
                ]
            ]
        ]
    ]

/// Kategorienauswahl mit Mehrfachauswahl, Zustand lokal.
[<ReactComponent>]
let private CategoryMultiSelectDemo () =
    let selected, setSelected = React.useState (Set.ofList [ Guid.Parse "00000000-0000-0000-0000-000000001001" ])
    let search, setSearch = React.useState ""
    let hiddenExpanded, setHiddenExpanded = React.useState false

    let guidOf (i: int) = Guid.Parse ("00000000-0000-0000-0000-" + (string (1000 + i)).PadLeft(12, '0'))

    let categories : YnabCategory list =
        (demoCategoryData
         |> List.mapi (fun i (_, group, name, amount) ->
             { Id = YnabCategoryId (guidOf (i + 1))
               Name = name
               GroupName = group
               Available = Some { Amount = amount; Currency = "EUR" }
               Hidden = false }))
        @ [ { Id = YnabCategoryId (guidOf 99)
              Name = "Altes Sparziel"
              GroupName = "Fixkosten"
              Available = None
              Hidden = true } ]

    section "25. Kategorienauswahl mit Mehrfachauswahl" [
        note "Inhalt des Kategorie-Filters im Kassenbuch: Suchfeld, Gruppenliste mit Checkbox und \
              Available rechts, versteckte Kategorien eingeklappt unter \"Versteckt (n)\". \
              Click-Commit: erst \"Übernehmen\" schreibt den Entwurf in den Filter. Nicht zu \
              verwechseln mit der Einzelauswahl in Sektion 19. ADR 0005, styleguide.md §7.1."
        Html.div [
            prop.custom ("data-ds-card", "components-categorymultiselect")
            prop.custom ("data-ds-card-name", "Kategorienauswahl (Mehrfachauswahl)")
            prop.custom ("data-ds-card-subtitle", "Kategorien-Filter mit Suche, Checkboxen, Available und eingeklappten versteckten Kategorien")
            prop.custom ("data-ds-group", "Components")
            prop.className "max-w-sm"
            prop.children [
                CategoryMultiSelect.view {
                    Categories = categories
                    Selected = selected
                    Search = search
                    HiddenExpanded = hiddenExpanded
                    Recent = []
                    ApplyDisabled = false
                    OnToggle = fun g -> setSelected (if Set.contains g selected then Set.remove g selected else Set.add g selected)
                    OnSearchChange = setSearch
                    OnToggleHiddenExpanded = fun () -> setHiddenExpanded (not hiddenExpanded)
                    OnReset = fun () -> setSelected Set.empty
                    OnApply = ignore
                }
            ]
        ]
    ]

// ============================================
// Mehrfachauswahl in Listen (ledger-fa1fp)
// ============================================

/// Ein Rahmen, in dem die feste Auswahl-Leiste in der Galerie an ihrem Platz bleibt: eine
/// Umwandlung (`transform`) macht den Rahmen zum Bezugspunkt für `position: fixed`.
let private barFrame (children: ReactElement list) =
    Html.div [
        prop.className "relative h-40 overflow-hidden transform-gpu"
        prop.children children
    ]

/// Die Leisten der Galerie in je einem Helfer: ändert sich die Signatur der Bausteine, ist hier
/// die einzige Stelle zum Nachziehen.
let private demoLena : MultiSelect.LenaAction =
    { Label = "Lena fragen"; IsBusy = false; IsLocked = false; Reason = None; OnClick = ignore }

let private demoWithdraw : MultiSelect.LenaAction =
    { Label = "Rückfrage zurücknehmen"; IsBusy = false; IsLocked = false; Reason = None; OnClick = ignore }

let private demoBarMobile (sum: Money) (category: MultiSelect.CategoryAction) =
    MultiSelect.barMobile "3 ausgewählt" sum "Alle 34" false ignore "Geprüft" false false ignore category demoLena demoWithdraw "Auswahl aufheben" ignore

let private demoBarDesktop (sum: Money) (category: MultiSelect.CategoryAction) =
    MultiSelect.barDesktop "3 ausgewählt" sum "Geprüft" false false ignore category demoLena demoWithdraw "Auswahl aufheben" "Esc" ignore

let private multiSelectSection =
    let sum = { Amount = -143.49m; Currency = "EUR" }
    let category : MultiSelect.CategoryAction =
        { Label = "Kategorie setzen"
          IsBusy = false
          IsLocked = false
          OnClick = ignore
          Popover = Html.none
          Sheet = Html.none }

    let card (id: string) (name: string) (subtitle: string) (children: ReactElement list) =
        Html.div [
            prop.custom ("data-ds-card", id)
            prop.custom ("data-ds-card-name", name)
            prop.custom ("data-ds-card-subtitle", subtitle)
            prop.custom ("data-ds-group", "Components")
            prop.className "mb-6 overflow-hidden rounded-xl border border-border-subtle"
            prop.children children
        ]

    section "28. Mehrfachauswahl in Listen" [
        note "Die Bausteine der Auswahl im Kassenbuch (`DesignSystem/MultiSelect.fs`): das Kästchen \
              mit drei Zuständen, die Summenzeile im Auswahl-Zustand und die Auswahl-Leiste am \
              unteren Rand. Das Kästchen in der Kopfzeile wählt den ganzen Treffer und zeigt eine \
              Teilauswahl mit einem Strich (ADR 0053)."

        card "components-multiselect-checkbox" "Mehrfachauswahl: Kästchen" "Leer, gesetzt, Teilauswahl; Handy 20 px, PC 17 px; Kopfzeilen-Kästchen" [
            Html.div [
                prop.className "flex flex-wrap items-center gap-6 px-4 py-4"
                prop.children [
                    for state, label in
                        [ MultiSelect.Unchecked, "leer"
                          MultiSelect.Checked, "gesetzt"
                          MultiSelect.Partial, "Teilauswahl" ] do
                        Html.div [
                            prop.key label
                            prop.className "flex items-center gap-3"
                            prop.children [
                                MultiSelect.box state true
                                MultiSelect.box state false
                                caption label
                            ]
                        ]
                ]
            ]
            Html.div [
                prop.custom ("data-testid", "styleguide-multiselect-header")
                prop.className $"flex items-center gap-4 border-t px-5 py-2 {Tokens.Borders.subtle} {Tokens.Backgrounds.dark}"
                prop.children [
                    MultiSelect.boxButton MultiSelect.Partial "Alle Buchungen des Treffers auswählen" ignore
                    Html.span [ prop.className Tokens.ListRoles.columnHeader; prop.text "Payee" ]
                ]
            ]
        ]

        card "components-multiselect-summary" "Mehrfachauswahl: Summenzeile" "Teal-Kante links, Beschriftung in Teal, Summe der Auswahl rechts" [
            MultiSelect.summaryRow "Auswahl: 3 von 34 Buchungen" sum
        ]

        card "components-multiselect-bar-mobile" "Mehrfachauswahl: Leiste am Handy" "Zwei Reihen: Anzahl und Summe, dann Alle, Geprüft, Kategorie setzen, Auswahl aufheben (nur unter md sichtbar)" [
            barFrame [ demoBarMobile sum category ]
        ]

        card "components-multiselect-bar-desktop" "Mehrfachauswahl: Leiste am PC" "Eine Reihe: Anzahl und Summe links, rechts Auswahl aufheben mit Taste, Kategorie setzen, Geprüft (ab md sichtbar)" [
            barFrame [ demoBarDesktop sum category ]
        ]
    ]

// ============================================
// Seite
// ============================================

let view () =
    Html.div [
        prop.className "space-y-2 max-w-4xl mx-auto pb-16"
        prop.children [
            PageHeader.gradientWithSubtitle
                "Styleguide"
                "Lebende Galerie der echten Design-System-Komponenten & Tokens. Geschriebener Begleiter: standards/frontend/styleguide.md."

            Html.div [
                prop.className "space-y-10"
                // Top-Level-Sektionsliste: gemischt aus statischen `Html.section`-Werten und
                // `[<ReactComponent>]`-Aufrufen (InputDemo, FormDemo, …). Der innere
                // `prop.key` einer Komponente propagiert NICHT auf das Komponenten-Element in
                // der Eltern-Liste — daher jeden Eintrag hier per Index in ein
                // `React.keyedFragment` hüllen (kein DOM-Knoten, kein visueller Unterschied).
                prop.children (
                    [
                        colorSection
                        typographySection
                        buttonSection
                        cardSection
                        badgeSection
                        moneySection
                        statsSection
                        tableSection
                        calmListSection
                        loadingSection
                        errorSection
                        iconsSection
                        pageHeaderSection
                        primitivesSection
                        InputDemo()
                        FormDemo()
                        ModalDemo()
                        ToastDemo()
                        BottomSheetDemo()
                        CategoryPickerDemo()
                        CategoryPickerSingleDemo()
                        SplitEditorDemo()
                        SwipeDemo()
                        navigationSection
                        voiceSection
                        chipBarSection
                        FilterPopoverDemo()
                        FilterPopoverMenuDemo()
                        DetailDemo()
                        multiSelectSection
                        CategoryMultiSelectDemo()
                    ]
                    |> List.mapi (fun i sectionEl -> React.keyedFragment (i, [ sectionEl ]))
                )
            ]
        ]
    ]
