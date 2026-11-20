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
                prop.className (Tokens.Presets.sectionHeader + " " + Tokens.Colors.textPrimary)
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

/// Ein Farb-Swatch: Hintergrundfläche in der Rollenfarbe + semantisches Label.
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
    section "1. Farbsemantik: eine Farbe, eine Bedeutung (ADR 0087)" [
        note "Jede Farbe trägt auf jeder Seite dieselbe Bedeutung, Seitenausnahmen gibt es nicht. \
              Quelle: styles.css (@theme) und Tokens.fs (Colors/Backgrounds/Borders/Glows). Tabelle und \
              Regeln: standards/frontend/styleguide.md §2. Kontrast prüft scripts/contrast-guard.test.mjs."
        Primitives.Grid.three [
            colorSwatch Tokens.Backgrounds.accent "Akzent" "Hauptaktion und Auswahl/Navigation"
            colorSwatch "bg-attention" "Gelb" "braucht dich, kein Fehler"
            colorSwatch "bg-error" "Rot" "Fehler und Minus"
            colorSwatch "bg-success" "Grün" "gelungen"
            colorSwatch "bg-info" "Blau" "Hinweis"
            colorSwatch "bg-border-strong" "border-strong" "Rand eines Bedienelements"
        ]

        subTitle "Farbrollen im Einsatz"
        Html.div [
            prop.custom ("data-testid", "farbrollen-beispiele")
            prop.className "flex flex-col gap-4"
            prop.children [
                row [
                    Button.primary "Übernehmen" ignore
                    caption "Hauptaktion: Akzent gefüllt, Text dunkel"
                ]
                row [
                    Html.div [
                        prop.className "w-64"
                        prop.children [
                            Tabs.bar "farbrollen-tabs" "Beispiel Auswahl" [
                                { Label = "Buchungen"; IsActive = true; OnClick = ignore }
                                { Label = "Regeln"; IsActive = false; OnClick = ignore }
                            ]
                        ]
                    ]
                    caption "Auswahl: Akzent als Strich"
                ]
                Html.div [
                    prop.className $"flex items-center justify-between gap-3 rounded-lg bg-surface-card px-4 h-11 {Tokens.ListRoles.statusEdge}"
                    prop.children [
                        Html.span [ prop.className Tokens.ListRoles.primary; prop.text "Rewe" ]
                        Html.span [ prop.className $"{Tokens.FontSizes.microPlus} {Tokens.Colors.attention}"; prop.text "zu prüfen" ]
                    ]
                ]
                caption "Zeile zu prüfen: 3-px-Kante in Gelb, mit dem Wort"
                row [
                    Html.span [ prop.className $"font-mono tabular-nums {Tokens.Colors.error}"; prop.text "−42,10 €" ]
                    caption "Minus und Fehler: Rot"
                ]
                row [
                    Html.span [ prop.className $"text-sm {Tokens.Colors.success}"; prop.text "Gespeichert" ]
                    caption "Rückmeldung: Grün"
                ]
                row [
                    Html.input [ prop.className "input-field max-w-xs"; prop.placeholder "Suchen…"; prop.ariaLabel "Beispielfeld" ]
                    caption "Eingabefeld: Rand border-strong, Fokusring im Akzent"
                ]
            ]
        ]

        subTitle "Backgrounds (gestaffelte Tiefe)"
        Primitives.Grid.three [
            colorSwatch Tokens.Backgrounds.void' "surface-app" "Grund-Ebene"
            colorSwatch Tokens.Backgrounds.dark "surface-card" "Karten"
            colorSwatch Tokens.Backgrounds.surface "surface-elevated" "erhöht"
            colorSwatch Tokens.Backgrounds.accentSubtle "accent/10" "Auswahl subtle"
            colorSwatch Tokens.Backgrounds.attentionSubtle "attention/10" "braucht dich subtle"
            colorSwatch Tokens.Backgrounds.successSubtle "success/10" "gelungen subtle"
        ]

        subTitle "Glows (nur Hauptaktion und Rückmeldung)"
        row [
            Html.div [
                prop.className ("bg-surface-card h-16 w-32 rounded-lg border border-accent " + Tokens.Glows.accent)
                prop.children [ Html.div [ prop.className "flex h-full items-center justify-center"; prop.children [ caption "glow-accent" ] ] ]
            ]
            Html.div [
                prop.className ("bg-surface-card h-16 w-32 rounded-lg border border-success " + Tokens.Glows.success)
                prop.children [ Html.div [ prop.className "flex h-full items-center justify-center"; prop.children [ caption "glow-success" ] ] ]
            ]
        ]
        note "Glow nur an der Hauptaktion und an Rückmeldungen (ADR 0087 Punkt 6). \
              Lena: die Flag-Farbe aus den Settings steht nur im Flag-Symbol, immer mit dem Namen (Punkt 8)."

        subTitle "Lena (Badge.lenaMark)"
        row [
            Html.div [
                yield! Badge.lenaFlagScope (Some "purple")
                prop.className "flex items-center gap-3"
                prop.children [ Badge.lenaMark "Lena" ]
            ]
            caption "mit data-lena-flag (Badge.lenaFlagScope, hier Lila)"
        ]
        row [
            Html.div [
                prop.className "flex items-center gap-3"
                prop.children [ Badge.lenaMark "Lena" ]
            ]
            caption "ohne Vorfahr mit data-lena-flag: Symbol gedämpft"
        ]
        row [
            Html.div [
                yield! Badge.lenaFlagScope (Some "purple")
                prop.className "flex items-center gap-3"
                prop.children [ Icons.flagSolid Icons.Mark Icons.LenaFlag ]
            ]
            caption "Icons.flagSolid Mark LenaFlag: gefülltes Flag, 15 px, Marke einer Rückfrage an Lena in „Zu tun“"
        ]
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
    ]

// ============================================
// 5. Statische Komponenten
// ============================================

let private buttonSection =
    section "3. Button" [
        note "Jede klickbare Aktion. Primär (Akzent) max. eine pro Ansicht (ADR 0087 Punkt 24). \
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
                note "Sekundär ist neutral mit dem Rand border-strong, auch im Hover. Der Akzent steht für Hauptaktion und Auswahl (ADR 0087)."
                subTitle "Nur Rand"
                note "Outline: Rand border-strong ohne Füllung (Prototyp `.btn`), etwa „Abbrechen“ neben dem Hauptknopf im Fuß von Panel und Sheet und „+ Neue Regel“ unter der Liste am Handy."
                row [
                    Button.view { Button.defaultProps with Text = "Abbrechen"; Variant = Button.Outline }
                    Button.view { Button.defaultProps with Text = "Neue Regel"; Variant = Button.Outline; Icon = Some (Icons.plus Icons.SM Icons.Default) }
                    // OutlineSelected: der Knopf „Filter (n)“ im Kassenbuch bei gesetzten Filtern (ledger-d9z3n).
                    Button.view { Button.defaultProps with Text = "Filter (2)"; Variant = Button.OutlineSelected; Icon = Some (Icons.filter Icons.SM Icons.Default) }
                ]
                note "DangerOutline: Rand und Schrift in Rot ohne Fläche (Prototyp `.btn.danger`), „Buchung löschen“ ganz unten im Buchungs-Detail. Danger mit getönter Fläche bleibt überall, wo es heute steht."
                row [
                    Button.view { Button.defaultProps with Text = "Buchung löschen"; Variant = Button.DangerOutline; Size = Button.Small }
                    Button.view { Button.defaultProps with Text = "Buchung löschen"; Variant = Button.DangerOutline; Size = Button.Small; IsBusy = true }
                ]
                row [
                    Button.primaryWithIcon "Hinzufügen" (Icons.plus Icons.SM Icons.Primary) ignore
                    Button.primaryLoading "Speichern…" true ignore
                    Button.secondaryLoading "Test…" true ignore
                ]
                note "IsBusy: der eigene Aufruf läuft auf einem Knopf, der den Fokus haben kann. Spinner, aria-disabled und aria-busy, aber kein disabled, der Fokus bleibt. Beispiel: Target pausieren."
                row [
                    Button.view { Button.defaultProps with Text = "Target pausieren"; Variant = Button.Secondary; IsBusy = true }
                ]
                note "KeepsFocus: der Knopf nimmt dem Feld daneben den Fokus nicht (preventDefault auf mousedown), am Handy bleibt die Tastatur offen. Beispiel: Speichern im Sheet Betrag, Fehlbetrag und Pause im Tab Geld."
                row [
                    Html.div [
                        prop.className "w-40"
                        prop.children [
                            Input.text
                                { Input.textInputDefaults with
                                    Value = "12,50"
                                    InputMode = Some "decimal"
                                    AriaLabel = Some "Betrag"
                                    ClassName = Some (Input.amountFieldClass false false) }
                        ]
                    ]
                    Button.view { Button.defaultProps with Text = "Speichern"; Variant = Button.Primary; KeepsFocus = true }
                ]
                subTitle "Hero-CTAs"
                row [
                    Button.hero "Sync starten" ignore
                    Button.heroWithIcon "Sync starten" (Icons.sync Icons.MD Icons.Primary) ignore
                ]
                subTitle "Primär ohne Glow"
                note "Gefüllt im Akzent über die volle Breite einer Karte, ohne Glow (Glow = false), etwa der erste Eintrag in „Zu tun“ auf der Übersicht (Prototyp `.btn.primary.block`)."
                Html.div [
                    prop.className "max-w-sm flex flex-col"
                    prop.children [
                        Button.view { Button.defaultProps with Text = "Prüfen"; Variant = Button.Primary; FullWidth = true; Glow = false }
                    ]
                ]
                subTitle "Schwebender Knopf"
                note "„+ Buchung“ am Handy unten rechts über der Leiste: 52 px hoch, Symbol 20 px. Gefüllt ist er die Hauptaktion; ruhig (Rand und Schrift im Akzent), wenn die Ansicht schon eine hat, etwa „Zu tun“ auf der Übersicht (§7.14)."
                row [
                    Button.fab "styleguide-fab" "Buchung" "Neue Buchung" false ignore
                    Button.fab "styleguide-fab-quiet" "Buchung" "Neue Buchung" true ignore
                ]
                subTitle "Button-Gruppe"
                Button.group [
                    Button.secondary "Abbrechen" ignore
                    Button.primary "Bestätigen" ignore
                ]
                subTitle "Rückfrage in zwei Stufen"
                note "Für folgenreiche Aktionen ohne Dialog. Stufe 1 zeigt die Aktion. Stufe 2 setzt Abbrechen an deren Stelle, \
                      damit ein Doppeltipp den harmlosen Knopf trifft, und fällt nach drei Sekunden zurück."
                row [
                    Button.danger "Bank trennen" ignore
                ]
                row [
                    Button.ghost "Abbrechen" ignore
                    Button.danger "Wirklich trennen" ignore
                ]
                subTitle "Knöpfe in Listen"
                Html.div [
                    prop.className "max-w-sm space-y-4"
                    prop.children [
                        Button.listName "styleguide-list-name" "Lebensmittel, Details" true "Lebensmittel" ignore
                        Button.listRow "styleguide-list-row" "styleguide-list-row" "Supermarkt, im Kassenbuch öffnen" ignore [
                            Html.span [
                                prop.className Tokens.ListRoles.mobileLine1
                                prop.children [
                                    Html.span [ prop.className $"{Tokens.ListRoles.primary} truncate min-w-0"; prop.text "Supermarkt" ]
                                    Money.listAmount -12.5m "EUR"
                                ]
                            ]
                            Html.span [
                                prop.className Tokens.ListRoles.mobileLine2
                                prop.children [
                                    Html.span [ prop.className Tokens.ListRoles.date; prop.text "12.11.2026" ]
                                    Html.span [ prop.className $"{Tokens.ListRoles.secondary} truncate"; prop.text "· Girokonto" ]
                                ]
                            ]
                        ]
                        subTitle "Wählbare Zeile"
                        note "Selectable: die gewählte Zeile trägt aria-current und den Akzent (ADR 0087). fehlt steht in ListRoles.missing."
                        for selected, name in [ false, "Lebensmittel"; true, "Urlaub" ] do
                            Button.selectableListRow $"styleguide-selectable-{name}" "styleguide-selectable-row" $"{name} wählen" selected ignore [
                                Html.span [
                                    prop.className Tokens.ListRoles.mobileLine1
                                    prop.children [
                                        Html.span [ prop.className $"{Tokens.ListRoles.primary} truncate min-w-0"; prop.text name ]
                                        Money.available 120m "EUR"
                                    ]
                                ]
                                Html.span [
                                    prop.className Tokens.ListRoles.mobileLine2
                                    prop.children [ Html.span [ prop.className Tokens.ListRoles.missing; prop.text "fehlt 12,30 €" ] ]
                                ]
                            ]
                        subTitle "Blättern"
                        note "stepArrow mit 48 px Trefferfläche am Handy. stepArrowGap hält den Platz, wenn am Rand der Pfeil fehlt. homeLabel führt nach Hause und trägt den Akzent, solange woanders. homeLabelCompact ist schmal für ein kurzes Wort (Kopf der Budget-Seite am Handy: Nov 2026)."
                        Html.div [
                            prop.className "flex items-center"
                            prop.children [
                                Button.stepArrowGap ()
                                Button.homeLabel "styleguide-home-label" "Oktober 2026, zum laufenden Monat" false "Oktober 2026" ignore
                                Button.stepArrow "styleguide-step-next" "Nächster Monat" "›" ignore
                            ]
                        ]
                        Html.div [
                            prop.className "flex items-center"
                            prop.children [
                                Button.stepArrow "styleguide-step-previous" "Vorheriger Monat" "‹" ignore
                                Button.homeLabel "styleguide-home-label-away" "September 2026, zum laufenden Monat" true "September 2026" ignore
                                Button.stepArrow "styleguide-step-next-away" "Nächster Monat" "›" ignore
                            ]
                        ]
                        Html.div [
                            prop.className "flex items-center"
                            prop.children [
                                Button.stepArrow "styleguide-step-previous-compact" "Vorheriger Monat" "‹" ignore
                                Button.homeLabelCompact "styleguide-home-label-compact" "November 2026, zum laufenden Monat" false "Nov 2026" ignore
                                Button.stepArrow "styleguide-step-next-compact" "Nächster Monat" "›" ignore
                            ]
                        ]
                        subTitle "Kachel und leiser Textknopf"
                        note "tile: Wert in Mono, Bezeichnung gedämpft, offen, erledigt (✓) oder aktiv (Akzent). quietText: leiser Textknopf. Leiste Monatserster der Budget-Seite. quietTextFor: dasselbe mit aria-label, wenn der Text mehrfach steht (Umbenennen je Gruppe, Seite Kategorien). disclosureSectionHead: Kopf eines eingeklappten Abschnitts. orderNameRow: Name mit Pfeilen für die Reihenfolge."
                        Html.div [
                            prop.className "grid grid-cols-3 gap-1.5 max-w-sm"
                            prop.children [
                                Button.tile "styleguide-tile-open" "2" "Targets prüfen" None None Button.TileOpen ignore
                                Button.tile "styleguide-tile-done" "✓" "Targets anwenden" None None Button.TileDone ignore
                                Button.tile "styleguide-tile-active" "3" "Überzogene decken" None None Button.TileActive ignore
                            ]
                        ]
                        Button.quietText "styleguide-quiet-text" "Ausblenden" ignore
                        Button.quietTextFor "styleguide-quiet-text-for" "Gruppe umbenennen" "Umbenennen" ignore
                        // disclosureSectionHead: Kopf eines eingeklappten Abschnitts ("Deaktiviert (n)", Seite Kategorien).
                        Button.disclosureSectionHead "styleguide-section-head" "styleguide-section-block" "Deaktiviert (2)" false ignore
                        // orderNameRow: Zeile mit Pfeilen für die Reihenfolge (Seite Kategorien, budgeting-sn2sn), als erste Zeile mit
                        // gedämpftem Pfeil nach oben.
                        Button.orderNameRow "styleguide-order-row" "styleguide-order-row" "Drogerie bearbeiten" "Drogerie" ignore ignore false true ignore ignore
                        // budgeting-w9yt4: „Bearbeiten“ im Kopf des Blocks Angepinnt, Pin-Knopf im Kategorie-Detail (beide Stände),
                        // Platz eines fehlenden Pfeils, „Lösen“ (×) und Zeilen zum Ankreuzen im Sheet Angepinnt (angekreuzt,
                        // markiert, leer).
                        Button.quietAccentText "styleguide-quiet-accent-text" "Bearbeiten" ignore
                        Html.div [
                            prop.className "flex items-center gap-2"
                            prop.children [
                                Button.pinToggle "styleguide-pin-off" false ignore
                                Button.pinToggle "styleguide-pin-on" true ignore
                                Button.orderArrowGap ()
                                Button.removeIcon "styleguide-remove-icon" "Lebensmittel lösen" ignore
                                // Gesperrt (`removeIconWhen true`): die einzige Zeile im Split-Editor, die bleiben muss.
                                Button.removeIconWhen true "styleguide-remove-icon-locked" "Zeile entfernen" ignore
                            ]
                        ]
                        Html.div [
                            prop.className "rounded-[14px] border border-border-subtle overflow-hidden"
                            prop.children [
                                Button.checkRow "styleguide-check-row" "styleguide-check-row-1" true false ignore [ Html.span [ prop.className "flex-1"; prop.text "Lebensmittel" ] ]
                                Button.checkRow "styleguide-check-row" "styleguide-check-row-2" false true ignore [ Html.span [ prop.className "flex-1"; prop.text "Drogerie (markiert)" ] ]
                                Button.checkRow "styleguide-check-row" "styleguide-check-row-3" false false ignore [ Html.span [ prop.className "flex-1"; prop.text "Auto" ] ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private cardSection =
    section "4. Card" [
        note "Inhaltsblöcke. emptyState sagt was als Nächstes zu tun ist. styleguide.md §5."
        // data-ds-card markiert die Sektion für den Export-Bundle-Build
        // (scripts/export-design-system.mjs, design-system-t82sg): das Skript
        // schneidet dieses Element aus der gerenderten Route heraus und schreibt es
        // 1:1 nach dist/design-system/preview/components-card.html.
        Html.div [
            prop.custom ("data-ds-card", "components-card")
            prop.custom ("data-ds-card-name", "Card")
            prop.custom ("data-ds-card-subtitle", "Standard-, Glas- und Leerzustand-Karten")
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
                ]
                Card.emptyState
                    (Icons.banknotes Icons.XL Icons.Default)
                    "Noch keine Transaktionen"
                    "Starte einen Sync, um Transaktionen zu importieren."
                    (Some (Button.primary "Sync starten" ignore))
                Html.div [
                    prop.className "max-w-[208px]"
                    prop.children [ Card.emptyStateCompact "Keine Konten" "Konten erscheinen hier nach dem ersten Spiegeln." ]
                ]
                Html.div [
                    prop.className "max-w-sm"
                    prop.children [ Card.allDone "styleguide-all-done" "Alles erledigt" "Neue Aufgaben erscheinen nach dem nächsten Import." ]
                ]
                // sheetMessage: leerer Zustand oder Ergebnis in einem Sheet, ohne Rahmen (Sheet „Abrufen“, banking-import-d0fey);
                // ein gelungenes Ergebnis zeigt den Kopf in Grün.
                Primitives.Grid.two [
                    Card.sheetMessage "styleguide-sheet-message" false "Rufe ab…" "Umsätze werden geladen und mit Regeln kategorisiert."
                    Card.sheetMessage "styleguide-sheet-message-success" true "3 neue Buchungen" "2 per Regel kategorisiert, 1 ohne Kategorie."
                ]
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
                    Badge.warning "in YNAB geändert"
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
                note "Neutral trägt einen Stand ohne Wertung."
                row [
                    Badge.neutral "Neutral"
                ]
                note "Klein, für den Stand in einer Listenzeile (Tab Historie). In YNAB geändert und teilweise geschrieben in Gelb: braucht dich (ADR 0087). Teilweise geschrieben: eine Target-Änderung, deren Datum in Schritt 2 nicht gesetzt ist."
                row [
                    Badge.errorSmall "fehlgeschlagen"
                    Badge.warningSmall "in YNAB geändert"
                    Badge.warningSmall "teilweise geschrieben"
                    Badge.neutralSmall "noch offen"
                    Badge.neutralSmall "in BB ersetzt"
                ]
                note "In BB ersetzt (budgeting-dgrp7): eine Rückschreibung, die eine spätere Entscheidung in BB zurückgezogen hat. Neutral, ohne Wertung: nichts ist schiefgegangen, die neuere Entscheidung gilt."
                note "Stand der Bankverbindung (BankConnectionBadge), in Einstellungen und SyncFlow."
                row [
                    Components.BankConnectionBadge.view BankConnected
                    Components.BankConnectionBadge.view BankChallengePending
                    Components.BankConnectionBadge.view BankDisconnected
                    Components.BankConnectionBadge.unreadable ()
                ]
            ]
        ]
    ]

let private moneySection =
    section "6. Money" [
        note "Jede Geldanzeige. Die Vorzeichen-Farbe wird automatisch gesetzt (positiv Grün, negativ Rot), ohne Glow; nicht manuell überschreiben. styleguide.md §2/§5."
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
                Html.div [
                    prop.className "max-w-sm space-y-1"
                    prop.children [
                        ErrorDisplay.cardCompactWith "Erneut laden" "Kontostände konnten nicht geladen werden" (Some ignore)
                        Money.accountListRowWith "Girokonto" (Loading.amountSkeleton ()) false ignore
                        Money.accountListRowWith "Tagesgeld" (Money.balanceUnavailable ()) false ignore
                        caption "accountListRowWith: lädt (Loading.amountSkeleton) und gescheitert (balanceUnavailable), darüber cardCompactWith (ledger-vq2g8)"
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
                    Stats.withIcon "Transaktionen" "1.234" (Icons.banknotes Icons.MD Icons.Default)
                    Stats.withTrend "Importe" "42" (Stats.Trend.Up 12.5m)
                    Stats.syncCount 8
                ]
                subTitle "Monatsbalken"
                note "Stats.monthBars: je Monat ein Balken mit Monat und Betrag, ohne waagrechtes Scrollen. Der laufende Monat ist neutral gestrichelt und trägt \"läuft\", der Akzent bleibt für Ausgewähltes (ADR 0087). Ein Monat ohne Wert zeigt einen flachen Balken. Überwiegen Erstattungen, steht der Betrag neutral mit Plus da, Rot heißt auf der Budget-Seite \"im Minus\". Darüber die Wertzeile: Maus, Tipp oder Tab-Taste wählen einen Balken, die Zeile zeigt Monat und genauen Betrag und der Balken wird etwas heller, ohne Fokus-Ring. Ohne Auswahl bleibt die Zeile leer und hält ihre Höhe. Für Screenreader eine Liste, je Balken ein Label mit demselben Inhalt, getrennt mit Komma statt \"·\". Der gewählte Balken hebt zusätzlich seinen Monatsnamen hervor."
                Html.div [
                    prop.className "max-w-sm space-y-4"
                    prop.children [
                        let bar key month amount percent label : Stats.MonthBar =
                            { Key = key
                              Label = label
                              MonthText = month
                              AmountText = amount
                              Percent = percent
                              Running = false }
                        let bars =
                            [ bar "2026-05-01" "Mai" "320" 64 "Mai 2026 · 320,40 €"
                              bar "2026-06-01" "Jun" "0" 0 "Juni 2026 · 0,00 €"
                              bar "2026-07-01" "Jul" "500" 100 "Juli 2026 · 499,90 €"
                              bar "2026-08-01" "Aug" "+20" 0 "August 2026 · +20,40 €"
                              bar "2026-09-01" "Sep" "410" 82 "September 2026 · 410,15 €"
                              { bar "2026-10-01" "Okt" "150" 30 "Oktober 2026 · 150,00 € · läuft noch" with Running = true } ]
                        Stats.monthBars "Ausgaben der letzten 6 Monate" bars
                        caption "Stats.monthBars: laufender Monat, Null und Erstattungsüberschuss mit Plus. Maus darüber, Tipp oder Tab-Taste füllt die Wertzeile"
                        Html.div [
                            prop.custom ("data-testid", "styleguide-month-bars-chosen")
                            prop.children [ Stats.monthBarsChosen "Ausgaben der letzten 6 Monate, Juli gewählt" "2026-07-01" bars ]
                        ]
                        caption "Stats.monthBarsChosen: Juli gewählt, die Wertzeile zeigt Monat und genauen Betrag"
                        Stats.monthBarsPlaceholder 6
                        caption "Stats.monthBarsPlaceholder: beim Laden, gleich hoch"
                        let spark i percent tone : Stats.SparkBar =
                            { Key = $"spark-{i}"; Percent = percent; Tone = tone; MonthText = [| "Nov"; "Dez"; "Jan"; "Feb"; "Mär"; "Apr"; "Mai"; "Jun"; "Jul"; "Aug"; "Sep"; "Okt" |].[i] }
                        let sparks =
                            [ for i, (percent, tone) in
                                  List.indexed
                                      [ 30, Stats.SparkMuted; 40, Stats.SparkMuted; 88, Stats.SparkOver; 67, Stats.SparkOver
                                        50, Stats.SparkOver; 100, Stats.SparkOver; 75, Stats.SparkOver; 38, Stats.SparkWithin
                                        96, Stats.SparkOver; 0, Stats.SparkWithin; 67, Stats.SparkOver; 96, Stats.SparkOver ] ->
                                  spark i percent tone ]
                        // Die PC-Fassung braucht einen Container ab 720 px, erst dort greift der Abstand von 3 px.
                        Html.div [
                            prop.className "overflow-x-auto"
                            prop.children [
                                Html.div [
                                    prop.custom ("data-testid", "styleguide-spark-bars-wide")
                                    prop.className "@container w-[720px]"
                                    prop.children [
                                        Html.div [
                                            prop.className "h-8 w-[132px]"
                                            prop.children [ Stats.sparkBars "Ausgaben der letzten 12 Monate, 8 über dem Target" (Some 42) sparks ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                        caption "Stats.sparkBars am PC: Mini-Balken einer Listenzeile (Seite Targets prüfen), 32 px hoch in einer Spalte von 132 px, in einem Container ab 720 px mit 3 px Abstand. Gelb über dem Target, gedämpft darunter, dunkler vor der letzten Änderung, gestrichelt die Target-Linie. Ein Bild ohne Tab-Stopp"
                        Html.div [
                            prop.className "@container h-7 w-full"
                            prop.children [ Stats.sparkBars "Ausgaben der letzten 12 Monate, 8 über dem Target" (Some 42) sparks ]
                        ]
                        caption "Stats.sparkBars am Handy: 28 px hoch über die volle Breite, 4 px Abstand"
                        Html.div [
                            prop.className "@container h-8 w-[132px]"
                            prop.children [
                                Stats.sparkBars
                                    "Ausgaben der letzten 12 Monate, 11 mit Ausgaben"
                                    None
                                    (sparks |> List.map (fun b -> { b with Tone = Stats.SparkWithin }))
                            ]
                        ]
                        caption "Stats.sparkBars ohne Target: keine Linie, alle gezählten Monate gedämpft"
                        Html.div [
                            prop.className "h-8 w-[132px]"
                            prop.children [ Stats.sparkBarsPlaceholder () ]
                        ]
                        caption "Stats.sparkBarsPlaceholder: beim Laden an ihrer Stelle, Ecken 2 px"
                        Stats.sparkBarsLabelled "Ausgaben der letzten 12 Monate, 8 über dem Target" (Some 42) sparks
                        caption "Stats.sparkBarsLabelled: groß im Panel, darunter die Monatskürzel"
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
    // Die Kennzeichen stehen hinter dem Payee (ledger-cnaz3): ein Wort in Gelb, Rot oder die Lena-Marke.
    let markWord (colorClass: string) (text: string) =
        Html.span [ prop.className ($"shrink-0 whitespace-nowrap {Tokens.FontSizes.xs} " + colorClass); prop.text text ]
    let noCategory =
        Table.tdContent [ Html.span [ prop.className $"text-xs {Tokens.Colors.attention}"; prop.text "Keine Kategorie" ] ]
    let dataRow (edge: bool) (date: string) (payee: string) (marks: ReactElement list) (category: ReactElement) (memo: string) (amount: decimal) =
        Table.calmRow edge (keyed [
            Table.roleCell Table.DateCell [ Html.text date ]
            Table.roleCell Table.Payee [
                Html.div [ prop.className "flex min-w-0 items-center gap-2"; prop.children (Html.span [ prop.text payee ] :: marks) ]
            ]
            category
            Table.roleCell Table.Muted [ Html.text memo ]
            Table.amountCell amount "EUR"
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
        note "Farbe ist ein Signal: Payee hell, Rest gedämpft, Kante in Gelb nur bei zu prüfen, Gelb bei Keine Kategorie, Beträge neutral. ADR 0049, ADR 0087, styleguide.md §2."
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
                note "SubtitleView (ledger-cnaz3): der Untertitel mit eigenem Aufbau, im Kassenbuch am PC Konto und Saldo in Mono vor dem Stand des Spiegels."
                PageHeader.view
                    { PageHeader.defaultProps with
                        Title = "Kassenbuch"
                        SubtitleView =
                            Some (
                                Html.span [
                                    Html.span [ prop.text "Alle Konten · " ]
                                    Html.span [ prop.className $"{Tokens.Fonts.mono} tabular-nums"; prop.text "4.300,00 €" ]
                                    Html.span [ prop.text " · Gespiegelt heute 22:24" ]
                                ]
                            )
                        Actions = [ Button.secondary "Jetzt spiegeln" ignore; Button.primary "Buchung" ignore ]
                        TitleStyle = PageHeader.Calm }
                note "Sortierbare Kopfzelle (ledger-bn913): aktiv aufsteigend, aktiv absteigend, nicht aktiv. Der Pfeil steht nur am aktiven Kopf, aria-sort trägt die Richtung."
                Table.view { Table.defaultTableProps with Variant = Table.Hover } [
                    Table.thead (keyed [
                        Table.calmSortableHeaderCell "Betrag aufsteigend" None Table.Ascending ignore
                        Table.calmSortableHeaderCell "Datum absteigend" None Table.Descending ignore
                        Table.calmSortableHeaderCell "Payee ohne Sortierung" None Table.Unsorted ignore
                    ])
                ]
                note "Ausnahmen der Budget-Seite: am Handy eine dritte Zeile, in der das Target kürzt und nie der Status; am PC die dichte Zeile mit 40 px (denseRowHeight), Betrag in Mono und Rhythmus in Inter in einer Zeile."
                Html.div [
                    prop.className $"{Tokens.ListRoles.mobileRowPadding} border-b border-border-subtle max-w-sm"
                    prop.children [
                        Html.div [
                            prop.className Tokens.ListRoles.mobileLine1
                            prop.children [
                                Html.span [ prop.className Tokens.ListRoles.primary; prop.text "Lebensmittel" ]
                                Money.listAmount 120m "EUR"
                            ]
                        ]
                        Html.div [
                            prop.className Tokens.ListRoles.mobileLine2
                            prop.children [ Html.span [ prop.className $"{Tokens.ListRoles.muted} truncate"; prop.text "Zugewiesen 400,00 € · Aktivität -280,00 €" ] ]
                        ]
                        Html.div [
                            prop.className Tokens.ListRoles.mobileLine2
                            prop.children [
                                Html.span [ prop.className $"{Tokens.ListRoles.secondary} truncate min-w-0"; prop.text "Target 600,00 €/Monat" ]
                                Html.span [ prop.className $"{Tokens.ListRoles.secondary} shrink-0 whitespace-nowrap"; prop.text "· fehlt 200,00 €" ]
                            ]
                        ]
                    ]
                ]
                note "Offene Rückschreibung (budgeting-dgrp7): Zugewiesen zeigt BBs Wert, YNAB hat ihn noch nicht. Die Markierung ist an PC und Handy dasselbe kleine Badge, neutral, damit Gelb in der Zeile `fehlt` bleibt. Am Handy steht sie hinter den Zahlen und kürzt nie, die Zahlen kürzen; am PC steht sie hinter dem Namen."
                Html.div [
                    prop.className $"{Tokens.ListRoles.mobileRowPadding} border-b border-border-subtle max-w-sm"
                    prop.children [
                        Html.div [
                            prop.className Tokens.ListRoles.mobileLine1
                            prop.children [
                                Html.span [ prop.className Tokens.ListRoles.primary; prop.text "Lebensmittel" ]
                                Money.listAmount 120m "EUR"
                            ]
                        ]
                        Html.div [
                            prop.className Tokens.ListRoles.mobileLine2
                            prop.children [
                                Html.span [ prop.className $"{Tokens.ListRoles.muted} truncate min-w-0"; prop.text "Zugewiesen 1.400,00 € · Aktivität -1.280,00 €" ]
                                Html.span [ prop.className "shrink-0"; prop.children [ Badge.neutralSmall "noch nicht in YNAB" ] ]
                            ]
                        ]
                    ]
                ]
                Html.div [
                    prop.className "flex items-center gap-2"
                    prop.children [
                        Html.span [ prop.className Tokens.ListRoles.primary; prop.text "Lebensmittel" ]
                        Badge.neutralSmall "noch nicht in YNAB"
                    ]
                ]
                Html.div [
                    prop.className "flex flex-col items-end w-fit"
                    prop.children [
                        Html.span [
                            prop.className "whitespace-nowrap"
                            prop.children [
                                Html.span [
                                    prop.className $"font-mono font-medium tabular-nums {Tokens.ListRoles.primarySize} {Tokens.Colors.textSecondary}"
                                    prop.text "600,00 €"
                                ]
                                Html.span [
                                    prop.className $"font-sans {Tokens.ListRoles.primarySize} {Tokens.Colors.textSecondary}"
                                    prop.text "/Monat"
                                ]
                            ]
                        ]
                        Html.span [ prop.className $"{Tokens.ListRoles.secondary} whitespace-nowrap"; prop.text "fehlt 200,00 €" ]
                    ]
                ]
                note "Button.disclosureListRow (budgeting-c6vn4): eine Zeile, die Einzelzeilen darunter aufklappt, etwa ein Durchlauf Targets anwenden in der Budget-Historie. Rechts ein Chevron in text-muted, der sich beim Öffnen dreht (aus unter prefers-reduced-motion), aria-expanded und aria-controls auf den Block darunter. Der Betrag steht rechts in Mono. Ein Teil-Fehlschlag steht ganz im roten Badge, am Handy kurz (\"1 von 2\"). Gezeigt: zu, offen und fehlgeschlagen."
                Html.div [
                    prop.className "max-w-sm rounded-xl border border-border-subtle bg-surface-card overflow-clip"
                    prop.children (keyed [
                        let runRow (key: string) (isOpen: bool) (status: ReactElement) (lines: ReactElement list) =
                            Html.div [
                                prop.className "border-b border-border-subtle last:border-b-0"
                                prop.children [
                                    Button.disclosureListRow key "styleguide-disclosure-row" $"styleguide-disclosure-{key}" isOpen ignore [
                                        Html.span [
                                            prop.className Tokens.ListRoles.mobileLine1
                                            prop.children [
                                                Html.span [ prop.className $"{Tokens.ListRoles.primary} truncate min-w-0"; prop.text "Targets anwenden, 2 Kategorien" ]
                                                Money.signedListAmount 860m "EUR"
                                            ]
                                        ]
                                        Html.span [
                                            prop.className Tokens.ListRoles.mobileLine2
                                            prop.children [
                                                Html.span [ prop.className Tokens.ListRoles.date; prop.text "03.11. 09:30" ]
                                                Html.span [ prop.className Tokens.ListRoles.secondary; prop.text "Nov. 2026" ]
                                                status
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.id $"styleguide-disclosure-{key}"
                                        prop.hidden (not isOpen)
                                        prop.children lines
                                    ]
                                ]
                            ]
                        let line (name: string) (amount: decimal) (status: ReactElement) =
                            Html.div [
                                prop.key name
                                prop.className $"{Tokens.ListRoles.mobileRowPadding} border-b border-border-subtle last:border-b-0 bg-surface-elevated"
                                prop.children [
                                    Html.div [
                                        prop.className Tokens.ListRoles.mobileLine1
                                        prop.children [
                                            Html.span [ prop.className Tokens.ListRoles.secondary; prop.text name ]
                                            Money.signedListAmount amount "EUR"
                                        ]
                                    ]
                                    Html.div [ prop.className Tokens.ListRoles.mobileLine2; prop.children [ status ] ]
                                ]
                            ]
                        let written = Html.span [ prop.className Tokens.ListRoles.muted; prop.text "geschrieben" ]
                        runRow "zu" false written []
                        runRow "offen" true written [ line "Miete" 800m written; line "Strom" 60m written ]
                        runRow "fehlgeschlagen" true
                            (Badge.errorSmall "1 von 2 fehlgeschlagen")
                            [ line "Miete" 800m written
                              Html.div [
                                  prop.key "Strom"
                                  prop.className $"{Tokens.ListRoles.mobileRowPadding} bg-surface-elevated"
                                  prop.children [
                                      Html.div [
                                          prop.className Tokens.ListRoles.mobileLine1
                                          prop.children [
                                              Html.span [ prop.className Tokens.ListRoles.secondary; prop.text "Strom" ]
                                              Money.signedListAmount 60m "EUR"
                                          ]
                                      ]
                                      Html.div [ prop.className Tokens.ListRoles.mobileLine2; prop.children [ Badge.errorSmall "fehlgeschlagen" ] ]
                                      Html.p [ prop.className $"{Tokens.ListRoles.secondary} line-clamp-2 mt-0.5"; prop.text "Rate-Limit (HTTP 429), erneut in 60 Sekunden" ]
                                  ]
                              ] ]
                    ])
                ]
                Table.view { Table.defaultTableProps with Variant = Table.Hover } [
                    Table.thead (keyed [
                        Table.calmHeaderCell "Datum" None
                        Table.calmHeaderCell "Payee" None
                        Table.calmHeaderCell "Kategorie" None
                        Table.calmHeaderCell "Memo" None
                        Table.calmHeaderCell "Betrag" (Some "right")
                    ])
                    Table.tbody (keyed [
                        groupRow "HEUTE" "-54,52 €"
                        dataRow true "Heute" "REWE Mitte" [ Badge.lenaMark "Lena" ] (cat "Alltag: Essen/Einkaufen") "Wocheneinkauf" -48.12m
                        dataRow false "Heute" "Bäckerei Krause" [ markWord Tokens.Colors.attention "doppelt?" ] (cat "Alltag: Essen/Einkaufen") "" -6.40m
                        groupRow "GESTERN" "+3.774,00 €"
                        dataRow false "Gestern" "Arbeitgeber GmbH" [] (cat "Ready to Assign") "Gehalt Oktober" 3412.80m
                        dataRow true "Gestern" "Amazon Marketplace" [ markWord Tokens.Colors.attention "nicht in YNAB" ] noCategory "" -37.45m
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
                            Html.span [ prop.className $"text-xs {Tokens.Colors.attention}"; prop.text "Keine Kategorie" ])
                    ])
                ]
                Html.div [
                    prop.className "flex flex-col gap-1"
                    prop.children [
                        Money.listAmount -48.12m "EUR"
                        Money.listAmount 0m "EUR"
                        Money.listAmount 4000m "EUR"
                        caption "Money.listAmount: jeder Betrag neutral, das Vorzeichen sagt die Richtung"
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
                            prop.className "rounded-2xl border border-border-subtle bg-surface-card overflow-clip"
                            prop.children [
                                // Ohne `ShortChoice` drumherum: die setzt beim Öffnen den Fokus, hier soll die Seite stehen bleiben.
                                PickerParts.shortChoiceRow "Alltag" (Some "2 Kategorien") true ignore
                                PickerParts.shortChoiceRow "Fixkosten" (Some "4 Kategorien") false ignore
                                PickerParts.shortChoiceRow "Eine Gruppe mit einem langen Namen, der gekürzt werden muss" (Some "1 Kategorie") false ignore
                            ]
                        ]
                        caption "PickerParts.shortChoiceRow: Zeile einer kurzen Auswahl ohne Suche (in ShortChoiceFor, §7.9), etwa die Gruppe in „Kategorie bearbeiten“; die gewählte mit Haken, auf ihr steht beim Öffnen der Fokus"
                        Html.div [
                            prop.className "space-y-2"
                            prop.children [
                                DateField.view { Value = None; OnChange = ignore; Invalid = false; Disabled = false; ReadOnly = false; Label = "Leer" }
                                DateField.view { Value = Some (System.DateTime(2026, 10, 7)); OnChange = ignore; Invalid = false; Disabled = false; ReadOnly = false; Label = "Gefüllt" }
                                DateField.view { Value = None; OnChange = ignore; Invalid = true; Disabled = false; ReadOnly = false; Label = "Fehler" }
                                DateField.view { Value = Some (System.DateTime(2027, 6, 1)); OnChange = ignore; Invalid = false; Disabled = false; ReadOnly = true; Label = "Nur lesend" }
                                DateField.view { Value = None; OnChange = ignore; Invalid = false; Disabled = true; ReadOnly = false; Label = "Gesperrt" }
                                caption "Datumsfeld: leer, gefüllt, Fehler (Hinweis, Übernehmen gesperrt), nur lesend (readOnly und aria-disabled, Fokus bleibt, etwa während ein Speichern läuft), gesperrt (disabled, gedämpft, kein Hover)"
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
                    Loading.spinner Loading.MD Loading.Accent
                    Loading.neonPulse Loading.Accent
                ]
                subTitle "Skeletons"
                Loading.tableSkeleton 3 3
                Html.div [
                    prop.className "max-w-sm"
                    prop.children [ Loading.firstItemListSkeleton 1 ]
                ]
                caption "firstItemListSkeleton: großer erster Eintrag mit Knopf und eine Zeile, „Zu tun“ beim Laden (ledger-s1weh)"
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
            prop.custom ("data-ds-card-subtitle", "Inline-Fehler, Fehlerkarten, Warnung und Erfolg")
            prop.custom ("data-ds-group", "Feedback")
            prop.className "space-y-3"
            prop.children [
                ErrorDisplay.inline' "Ungültige E-Mail-Adresse"
                ErrorDisplay.cardCompact "Einstellungen konnten nicht geladen werden" (Some ignore)
                ErrorDisplay.warning "Diese Aktion kann nicht rückgängig gemacht werden." None
                ErrorDisplay.notice "Stand hat sich geändert, es wurde nichts geschrieben."
                ErrorDisplay.noticeWithLead "Push-TAN bestätigen." "Öffne die Comdirect-App und gib den Zugriff frei."
                ErrorDisplay.success "Zugangsdaten erfolgreich verifiziert!"
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
                    Icons.sync Icons.MD Icons.Accent
                    Icons.plus Icons.MD Icons.Primary
                    Icons.check Icons.MD Icons.Success
                    Icons.x Icons.MD Icons.Default
                    Icons.trash Icons.MD Icons.Error
                    Icons.edit Icons.MD Icons.Default
                    Icons.settings Icons.MD Icons.Default
                    Icons.warning Icons.MD Icons.Warning
                    Icons.info Icons.MD Icons.Info
                    Icons.banknotes Icons.MD Icons.Success
                    Icons.creditCard Icons.MD Icons.Primary
                    Icons.search Icons.MD Icons.Default
                    Icons.externalLink Icons.MD Icons.Accent
                    Icons.flag Icons.MD Icons.Default
                    Icons.flagSolid Icons.MD Icons.Default
                    Icons.pin Icons.MD Icons.Default
                    Icons.pinSolid Icons.MD Icons.Accent
                    Icons.fetch Icons.MD Icons.Default
                ]
                caption "Icons.sync (zwei Kreispfeile) heißt „Jetzt spiegeln“, Icons.fetch (Pfeil nach unten in eine Ablage) heißt „Abrufen“, der Comdirect-Abruf mit Push-TAN"
                caption "Icons.pin (Umriss, nicht angepinnt) und Icons.pinSolid (gefüllt, angepinnt, im Akzent): Kategorie-Detail und Block „Angepinnt“"
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
            prop.custom ("data-ds-card-subtitle", "Seitenkopf mit Untertitel und Aktionen")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                PageHeader.withSubtitle "Einstellungen" "Verbindungen und Präferenzen konfigurieren."
                PageHeader.calm "Kassenbuch" (Some "Zuletzt gespiegelt vor 5 Minuten") []
                note "SubtitleDesktopOnly: der Untertitel steht erst ab md (Tab Regeln im Kassenbuch). Am Handy fehlt die Zeile darunter."
                Html.div [
                    prop.custom ("data-ds-demo", "pageheader-subtitle-desktop-only")
                    prop.children [
                        PageHeader.view
                            { PageHeader.defaultProps with
                                Title = "Kassenbuch"
                                Subtitle = Some "12 Regeln"
                                SubtitleDesktopOnly = true
                                TitleStyle = PageHeader.Calm }
                    ]
                ]
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
    let nativeValue, setNativeValue = React.useState true
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
                // pickerField: Auswahlfeld als Knopf (Formular "Neue Buchung", ledger-at63w), leer, mit Wert, mit Saldo.
                Input.pickerField "styleguide-picker-empty" "Kategorie" None "wählen" None false ignore
                Input.pickerField "styleguide-picker-value" "Kategorie" (Some "Lebensmittel") "wählen" None false ignore
                Input.pickerField "styleguide-picker-balance" "Konto" (Some "Girokonto") "wählen" (Some (Money.accountBalance 1250.40m "EUR")) false ignore
                // selectField: Kontowahl im Kassenbuch unter 1024 px (ledger-d9z3n), Saldo neben dem Namen, Pfeil nach unten.
                Input.selectField
                    "styleguide-select-field"
                    "Konto"
                    "Alle Konten"
                    (Some (Html.span [ prop.className $"{Tokens.FontSizes.xs} {Tokens.Fonts.mono} {Tokens.Colors.textMuted}"; prop.text "4.300,00 €" ]))
                    false
                    ignore
                // requiredSelectField: Pflicht-Auswahlfeld (budgeting-h2brj, Wochentag eines Targets): ohne Wert der
                // Platzhalter in Gelb, mit Wert wie selectField, gesperrt gedämpft.
                Input.requiredSelectField false "styleguide-required-select-empty" "Wochentag" None "Wochentag wählen" false ignore
                Input.requiredSelectField false "styleguide-required-select-value" "Wochentag" (Some "Montag") "Wochentag wählen" false ignore
                Input.requiredSelectField true "styleguide-required-select-locked" "Wochentag" (Some "Montag") "Wochentag wählen" false ignore
                // searchField: Suche mit Lupe (Kassenbuch, ledger-d9z3n).
                Input.searchField "styleguide-search-field" "Buchungen durchsuchen" "Suchen…" "" ignore
                // countSwitchRow und valueRow: Zeilen des Filter-Sheets im Kassenbuch (ledger-d9z3n).
                Html.div [
                    prop.children [
                        Input.countSwitchRow "styleguide-count-switch" "Zu prüfen" (Some 4) true ignore
                        Input.valueRow "styleguide-value-row" "Kategorie" "alle" false ignore
                    ]
                ]
                // accountChoiceRow: Zeile im Sheet „Konto“, die gewählte mit Haken und Akzentrahmen (ledger-d9z3n).
                Html.div [
                    prop.className "flex flex-col overflow-hidden rounded-xl border border-border-subtle bg-surface-card"
                    prop.children [
                        Money.accountChoiceRow "styleguide-account-choice" "Girokonto" (Money.accountBalanceFor true 1250.40m "EUR") true ignore
                        Money.accountChoiceRow "styleguide-account-choice" "Kreditkarte" (Money.accountBalanceFor true -312.55m "EUR") false ignore
                    ]
                ]
                // input-amount-hero: der große Betrag im Formular "Neue Buchung" (Mono, rechtsbündig, 30 px).
                Input.text
                    { Input.textInputDefaults with
                        Value = "12,50"
                        Placeholder = "0,00 €"
                        Size = Input.Large
                        InputMode = Some "decimal"
                        AriaLabel = Some "Betrag"
                        ClassName = Some (Input.amountFieldClass true false) }
                Input.toggle toggleValue setToggleValue (Some "Auto-Kategorisierung aktiv")
                // switchRow: Schalter als Zeile mit Erklärung (Schalter "Aktiv" der Seite Kategorien, budgeting-sn2sn).
                Input.switchRow "styleguide-switch-row" "Aktiv" "Deaktiviert verschwindet sie aus Auswahl und Budget. Buchungen und Historie bleiben." toggleValue false setToggleValue
                Input.checkbox checkValue setCheckValue "Bestätigt"
                // checkboxNative: die native Checkbox, in einem label mit der Zeile (Vorschau Targets anwenden).
                Html.label [
                    prop.custom ("data-testid", "styleguide-checkbox-native")
                    prop.className "flex items-center gap-2.5 text-sm text-text-primary cursor-pointer"
                    prop.children [
                        Input.checkboxNative nativeValue setNativeValue
                        Html.span [ prop.text "Lebensmittel (checkboxNative)" ]
                    ]
                ]
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
                note "submitButtonBlock: ohne Symbol und auch ab md über die volle Breite, die rechte Hälfte des Knopfpaars im Fuß von Panel und Sheet. Links Abbrechen als Button.Outline."
                Html.div [
                    prop.custom ("data-ds-demo", "form-submit-button-block")
                    prop.className "grid w-full max-w-sm grid-cols-2 items-start gap-3"
                    prop.children [
                        Button.view { Button.defaultProps with Text = "Abbrechen"; Variant = Button.Outline; FullWidth = true }
                        Form.submitButtonBlock "Speichern" ignore false [ ("Name", nameValue) ]
                    ]
                ]
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

/// Toast-Demo per useState: der eine Toast am PC (`Toast.single`, ab 1024 px). Ein neuer Toast ersetzt den alten,
/// "Schließen" markiert ihn als exiting (Exit-Animation) und blendet ihn erst nach `Types.Toast.exitDurationMs` aus.
/// Dieselbe Zwei-Phasen-Mechanik wie in der App (State.fs), hier lokal nachgebaut, da die Galerie keinen App-State
/// berührt. Unter 1024 px zeigt die App dieselbe Meldung in der Leiste (Abschnitt 17a).
[<ReactComponent>]
let private ToastDemo () =
    let current, setCurrent = React.useState (None: Toast.ToastItem option)
    let currentRef = React.useRef current
    currentRef.current <- current

    let fire variant message action () =
        setCurrent (Some { Id = Guid.NewGuid(); Message = message; Variant = variant; Exiting = false; Action = action })
    // Phase 1: markiere exiting (Exit-Animation startet); Phase 2: blende nach Ablauf aus.
    let dismiss (id: Guid) =
        match currentRef.current with
        | Some item when item.Id = id && not item.Exiting ->
            setCurrent (Some { item with Exiting = true })
            Fable.Core.JS.setTimeout
                (fun () ->
                    match currentRef.current with
                    | Some shown when shown.Id = id -> setCurrent None
                    | _ -> ())
                Types.Toast.exitDurationMs
            |> ignore
        | _ -> ()

    section "17. Toast" [
        note "Nur ab 1024 px und nur für Meldungen ohne Ort: höchstens einer, ein neuer ersetzt den alten, geht nach 5 s, mit ✕. Unter 1024 px die Leiste (17a). styleguide.md §5/§6/§7.13."
        row [
            // data-ds-card-trigger: Export-Skript klickt hier, damit ein Toast sichtbar ist, bevor
            // components-toast.html geschrieben wird. Der Toast ist nicht portalt, das data-ds-card unten erfasst ihn.
            Html.span [
                prop.custom ("data-ds-card-trigger", "components-toast")
                prop.children [ Button.secondary "Erfolg" (fire Toast.Success "Gespeichert" None) ]
            ]
            Button.secondary "Fehler" (fire Toast.Error "Nicht gespeichert: YNAB nicht erreichbar" None)
            Button.secondary "Info" (fire Toast.Info "3 Buchungen abgerufen" None)
            Button.secondary "Mit Rückgängig" (fire Toast.Success "REWE Mitte geprüft" (Some { Label = "Rückgängig" }))
        ]
        Html.div [
            prop.custom ("data-ds-card", "components-toast")
            prop.custom ("data-ds-card-name", "Toast")
            prop.custom ("data-ds-card-subtitle", "Flüchtige Rückmeldung am PC als Erfolg, Fehler oder Info")
            prop.custom ("data-ds-group", "Feedback")
            // Im Fluss der Galerie statt `Toast.single` (fest, erst ab 1024 px): so steht der Toast auf jeder Breite.
            prop.children [
                Html.div [
                    prop.className "flex w-full max-w-sm flex-col"
                    prop.children [
                        match current with
                        | Some item ->
                            Toast.toast
                                { Id = item.Id
                                  Message = item.Message
                                  Variant = item.Variant
                                  Title = None
                                  Exiting = item.Exiting
                                  OnDismiss = dismiss
                                  Action = item.Action
                                  OnAction = dismiss }
                        | None -> Html.none
                    ]
                ]
            ]
        ]
    ]

/// Rückmeldung am Handy ohne Toast (Regel R3, ADR 0098, §7.13): die echten Bausteine aus `Feedback`, als Vorschau
/// im Fluss der Seite. Die Demo „Meldung“ setzt nacheinander die Töne und zeigt Wechsel, Ersetzen und Eingang.
[<ReactComponent>]
let private FeedbackDemo () =
    let undo : Feedback.Act = { Label = "Rückgängig"; OnClick = ignore }
    let retry : Feedback.Act = { Label = "Erneut"; OnClick = ignore }
    let add quiet testId : Feedback.Add =
        { TestId = testId
          Label = "Buchung"
          AriaLabel = "Neue Buchung"
          Quiet = quiet
          OnClick = ignore
          HideFrom = Feedback.AddBreakpoint.FromLg }
    let msg tone action detail act : Feedback.Message =
        { Tone = tone; Action = action; Detail = detail; Act = act }
    let examples : (string * Feedback.Message) list =
        [ "Ok mit Rückgängig", msg Feedback.Tone.Ok "Geprüft" (Some "REWE Markt GmbH Filiale Leipzig-Gohlis") (Some undo)
          "Zusammengefasst", msg Feedback.Tone.Ok "3 geprüft" (Some "REWE, Bäckerei Schmidt, Tankstelle") (Some undo)
          "Läuft", msg Feedback.Tone.Running "Abrufen läuft …" None None
          "Ok ohne Knopf", msg Feedback.Tone.Ok "Abgerufen" (Some "3 neu, 1 geändert") None
          "Fehler", msg Feedback.Tone.Error "Abrufen gescheitert" (Some "Comdirect antwortet nicht") (Some retry)
          "Warnung",
          msg
              Feedback.Tone.Warning
              "Prüfvermerk nicht in YNAB"
              (Some "YNAB hat die Änderung nicht angenommen, BB versucht es beim nächsten Spiegeln erneut")
              None
          "Info", msg Feedback.Tone.Info "Regeln neu geladen" (Some "12 Regeln aus rules.yml") None ]
    let step, setStep = React.useState 0
    let current = snd examples.[step % examples.Length]
    let preview (children: ReactElement list) =
        Html.div [ prop.className "max-w-sm"; prop.children children ]
    let flashOn, setFlashOn = React.useState 0
    React.useEffect (
        (fun () ->
            if flashOn > 0 then
                let timer = Fable.Core.JS.setTimeout (fun () -> setFlashOn 0) 700
                React.createDisposable (fun () -> Fable.Core.JS.clearTimeout timer)
            else
                React.createDisposable ignore),
        [| box flashOn |]
    )

    section "17a. Rückmeldung (Feedback)" [
        note "Am Handy (unter 1024 px) kein Toast. Jede Rückmeldung hat einen festen Platz: am Ort (Feld leuchtet, Bestätigung darunter, Zeile „gelöscht“), bei offenem Sheet eine Zeile über dem Hauptknopf, sonst die Meldungsleiste über der Navigation. Immer nur eine Meldung, ohne ✕ und ohne Zeitlimit. styleguide.md §7.13, ADR 0098."
        Html.div [
            prop.custom ("data-testid", "styleguide-feedback")
            prop.className "space-y-4"
            prop.children [
                subTitle "Leiste in Ruhe mit „+ Buchung“ (gefüllt und ruhig)"
                preview [ Feedback.bar Feedback.BarPlacement.Preview None (Some (add false "styleguide-feedback-add")) ]
                preview [ Feedback.bar Feedback.BarPlacement.Preview None (Some (add true "styleguide-feedback-add-quiet")) ]
                subTitle "Leiste mit Meldung, „+ Buchung“ rund"
                for label, m in examples do
                    React.keyedFragment (label, [
                        caption label
                        preview [ Feedback.bar Feedback.BarPlacement.Preview (Some m) (Some (add false $"styleguide-feedback-add-{label}")) ]
                    ])
                subTitle "Leiste ohne „+ Buchung“ (nur solange eine Meldung steht)"
                preview [
                    Feedback.bar
                        Feedback.BarPlacement.Preview
                        (Some (msg Feedback.Tone.Ok "Regel gespeichert" (Some "Bäckerei Schmidt → Lebensmittel") (Some undo)))
                        None
                ]
                subTitle "Lange Aktion: bricht um, die Leiste wächst mit"
                preview [
                    Feedback.bar
                        Feedback.BarPlacement.Preview
                        (Some (msg Feedback.Tone.Ok "Kategorie geändert und in YNAB nachgezogen, Prüfvermerk gesetzt" (Some "Ein Zahlungsempfänger mit einem sehr langen Namen, der in keine Zeile passt") (Some undo)))
                        (Some (add false "styleguide-feedback-add-long"))
                ]
                subTitle "Demo: Meldung wechseln"
                row [
                    Button.secondary "Meldung" (fun () -> setStep (step + 1))
                ]
                preview [ Feedback.bar Feedback.BarPlacement.Preview (Some current) (Some (add false "styleguide-feedback-add-demo")) ]
                subTitle "Zeile im Sheet über dem Hauptknopf"
                preview [
                    Html.div [
                        prop.className "space-y-2"
                        prop.children [
                            Feedback.sheetLine (msg Feedback.Tone.Ok "50,00 € nach Reise" None (Some undo))
                            Feedback.sheetLine (msg Feedback.Tone.Warning "In YNAB geändert, nichts überschrieben" None None)
                            Feedback.sheetLine
                                (msg
                                    Feedback.Tone.Error
                                    "Nicht verschoben:"
                                    (Some "YNAB antwortet nicht. Das Geld steht noch in Lebensmittel.")
                                    (Some { Label = "Erneut senden"; OnClick = ignore }))
                        ]
                    ]
                ]
                // ledger-pz5wv: die Zeile der letzten Aktion unter „Passt, weiter“ im Durchgehen (in `ReviewCard.foot`).
                subTitle "Zeile der letzten Aktion (Durchgehen)"
                preview [
                    Html.div [
                        prop.className "max-w-[412px] space-y-1"
                        prop.children [
                            for m in
                                [ msg Feedback.Tone.Ok "Passt" (Some "Stadtwerke Leipzig") (Some undo)
                                  msg Feedback.Tone.Ok "Passt" (Some "REWE Markt GmbH Filiale Leipzig-Gohlis Georg-Schumann-Straße") (Some undo)
                                  msg Feedback.Tone.Warning "Passt, nicht in YNAB" (Some "Zeitüberschreitung") (Some undo)
                                  msg Feedback.Tone.Error "Nicht geprüft" (Some "YNAB antwortet nicht (503)") (Some retry)
                                  msg Feedback.Tone.Ok "Als neu übernommen" (Some "Bäckerei Schmidt") None ] ->
                                ReviewCard.foot (ReviewCard.FootLast m)
                        ]
                    ]
                ]
                subTitle "Feld mit Leuchten und Bestätigung"
                row [ Button.secondary "Leuchten" (fun () -> setFlashOn (flashOn + 1)) ]
                preview [
                    Html.div [
                        prop.className "space-y-1"
                        prop.children [
                            Html.div [
                                prop.className (if flashOn > 0 then Feedback.flash else "")
                                prop.children [
                                    Input.pickerField "styleguide-feedback-field" "Kategorie" (Some "Bahn") "Kategorie wählen" None false ignore
                                ]
                            ]
                            Feedback.fieldNote "Bahn · 84,20 € verfügbar" (Some undo)
                            // ledger-p5y9s: mit Ton, wie im Detail des Kassenbuchs.
                            Feedback.fieldNoteWith Feedback.Tone.Ok "Geändert" (Some undo)
                            Feedback.fieldNoteWith Feedback.Tone.Warning "Geändert, nicht in YNAB: HTTP 500" (Some undo)
                            Feedback.fieldNoteWith Feedback.Tone.Error "Nicht geändert: YNAB nicht erreichbar" None
                            // „✓ Gespeichert“ in der Erfolgsrolle (Einstellungen, banking-import-vwhfm).
                            Feedback.saved "Gespeichert"
                        ]
                    ]
                ]
                subTitle "Zeile „gelöscht“ am Platz"
                preview [
                    Html.div [
                        prop.className "space-y-2"
                        prop.children [
                            Feedback.removedRowIn "Regel drei" Feedback.RemovedPhase.Removing
                            Feedback.removedRow "Regel drei" undo
                            Feedback.removedRowUnnamed "Buchung" (Feedback.RemovedPhase.Removed undo)
                            Feedback.removedRowIn "Eine Regel mit einem sehr langen Namen für die Zeile" Feedback.RemovedPhase.Restoring
                            Feedback.removedRowIn
                                "Regel drei"
                                (Feedback.RemovedPhase.RestoreFailed ("Datenbankfehler bei restore_rule: gesperrt", { undo with Label = "Erneut" }))
                        ]
                    ]
                ]
                subTitle "Hinweis zum Zustand der Seite (nicht neu gelesen)"
                preview [
                    Html.div [
                        prop.className "space-y-2"
                        prop.children [
                            Feedback.stateHint
                                "styleguide-feedback-state"
                                "Beträge nicht neu gelesen, sie können veraltet sein."
                                [ "YNAB antwortet nicht (503)" ]
                                (Some { Label = "Neu laden"; OnClick = ignore })
                                false
                            Feedback.stateHint
                                "styleguide-feedback-state-running"
                                "Historie nicht neu gelesen, sie kann veraltet sein."
                                []
                                (Some { Label = "Neu laden"; OnClick = ignore })
                                true
                        ]
                    ]
                ]
            ]
        ]
    ]

/// BottomSheet-Demo (mobiles Picker-Muster, ADR 0005) per useState.
[<ReactComponent>]
let private BottomSheetDemo () =
    let isOpen, setOpen = React.useState false
    let inspectorOpen, setInspectorOpen = React.useState false
    let chosen, setChosen = React.useState ""
    let confirmOpen, setConfirmOpen = React.useState false
    let changeOpen, setChangeOpen = React.useState false

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
        subTitle "Rückfrage im Sheet (BottomSheet.confirmDanger)"
        note "Vor einer zerstörenden Aktion am Handy, das Gegenstück zu Modal.confirmDanger. Steht als Block im Sheet, role alertdialog mit aria-labelledby, ohne aria-modal (im Fluss des Sheets, keine Fokusfalle). Beim Öffnen hat Abbrechen den Fokus, Escape bricht ab. Nach dem Schließen geht der Fokus auf den Knopf, der die Rückfrage geöffnet hat (ReturnFocus): nach Abbrechen ist er wieder bedienbar, nach dem Bestätigen dreht er mit dem laufenden Aufruf. Das nur, wenn der Fokus beim Schließen noch in der Rückfrage lag oder auf body gefallen ist; schließt sie, weil im Feld darüber getippt wurde, bleibt der Fokus im Feld. Höchstens eine Rückfrage zugleich. Beispiel: Target entfernen."
        Html.div [
            prop.custom ("data-ds-card-name", "BottomSheet.confirmDanger")
            prop.className "max-w-sm"
            prop.children [
                if confirmOpen then
                    BottomSheet.confirmDanger
                        { Title = "Target entfernen?"
                          Message = "Das Target wird in YNAB entfernt. Der Fehlbetrag entfällt."
                          ConfirmText = "Entfernen"
                          OnConfirm = fun () -> setConfirmOpen false
                          OnCancel = fun () -> setConfirmOpen false
                          ReturnFocus = Some "[data-ds-card-name='BottomSheet.confirmDanger'] button" }
                else
                    Button.danger "Target entfernen" (fun () -> setConfirmOpen true)
            ]
        ]
        subTitle "Rückfrage im Sheet (BottomSheet.confirm)"
        note "Vor einer Aktion mit Folgen am Handy, das Gegenstück zu Modal.confirm: wie confirmDanger, der Knopf im Akzent. Beispiel: der Wechsel der Art eines Targets."
        Html.div [
            prop.custom ("data-ds-card-name", "BottomSheet.confirm")
            prop.className "max-w-sm"
            prop.children [
                if changeOpen then
                    BottomSheet.confirm
                        { Title = "Rhythmus wechseln?"
                          Message = "Der Tag des Targets geht verloren."
                          ConfirmText = "Wechseln"
                          OnConfirm = fun () -> setChangeOpen false
                          OnCancel = fun () -> setChangeOpen false
                          ReturnFocus = Some "[data-ds-card-name='BottomSheet.confirm'] button" }
                else
                    Button.primary "Target speichern" (fun () -> setChangeOpen true)
            ]
        ]
        subTitle "Chip mit Trefferfläche (BottomSheet.chipButtonTouch)"
        note "Wie chipButton, am Handy 48 px hoch, ab md 36 px. Hält den Fokus im Feld, die Tastatur bleibt offen. Zuletzt-Chips im Formular \"Neue Buchung\"."
        Html.div [
            prop.className "flex flex-wrap gap-2"
            prop.children [
                for label in [ "REWE · Lebensmittel"; "Bäcker · Lebensmittel" ] ->
                    React.keyedFragment (label, [ BottomSheet.chipButtonTouch label ignore ])
            ]
        ]
        subTitle "Feste Höhe (BottomSheet.inspector)"
        note "500 px hoch, wird mit offener Tastatur kleiner. Der feste Bereich bleibt stehen, nur der Inhalt scrollt. Im Kopf stehen sheetArrow neben Schließen, je 48 px, am Rand hält sheetArrowGap den Platz. Titel und Unterzeile kürzen."
        row [ Button.secondary "Sheet mit fester Höhe" (fun () -> setInspectorOpen true) ]
        BottomSheet.inspector
            { IsOpen = inspectorOpen
              OnClose = fun () -> setInspectorOpen false
              Title = "Lebensmittel"
              Subtitle = Some "Alltag · Oktober 2026"
              Footer = None }
            [ Button.sheetArrowGap ()
              Button.sheetArrow "styleguide-sheet-next" "Nächste Kategorie" (Icons.chevronDown Icons.SM Icons.Default) ignore ]
            [ Html.p [ prop.className "text-sm text-text-secondary pb-2"; prop.text "Fester Bereich" ] ]
            [ Html.p [ prop.className "text-sm text-text-muted"; prop.text "Scrollender Inhalt" ] ]
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

/// Kategorienauswahl-Demo (design-system-y6gkd): das Sheet von `CategoryPicker` mit erfundenen Beispieldaten,
/// Vorschläge, Zuletzt verwendet und die Gruppenliste mit Available rechts. Der alte Picker
/// `BottomSheet.categoryPicker` ist mit ledger-ea70s entfallen; die Karte `components-categorypicker` zeigt den
/// einen Picker, den alle Stellen nutzen.
[<ReactComponent>]
let private CategoryPickerDemo () =
    let isOpen, setOpen = React.useState false
    let chosen, setChosen = React.useState ""

    let guidOf (i: int) = Guid.Parse ("00000000-0000-0000-0000-" + (string (1900 + i)).PadLeft(12, '0'))
    let categories : YnabCategory list =
        demoCategoryData
        |> List.mapi (fun i (_, group, name, amount) ->
            { Id = YnabCategoryId (guidOf (i + 1))
              Name = name
              GroupName = group
              Available = Some { Amount = amount; Currency = "EUR" }
              Hidden = false })
    let nameOf (id: YnabCategoryId option) =
        categories |> List.tryFind (fun c -> Some c.Id = id) |> Option.map (fun c -> c.Name) |> Option.defaultValue ""
    let props : CategoryPicker.CategoryPickerProps =
        { Categories = categories
          Current = None
          Suggested = [ YnabCategoryId (guidOf 1); YnabCategoryId (guidOf 2) ]
          Recent = [ YnabCategoryId (guidOf 4) ]
          Accounts = []
          CurrentAccount = None
          AccountsLoad = CategoryPicker.PickerReady
          OfferWithout = true
          ToBeBudgeted = None
          StandLabel = None
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect =
            CategoryPicker.categoryChoice (fun choice ->
                setChosen (nameOf choice)
                setOpen false)
          OnClose = fun () -> setOpen false }

    section "19. Kategorienauswahl (Kategorie ändern)" [
        note "Kategorie ändern im Kassenbuch (Ablauf 4, ledger-xbrht): Vorschläge und Zuletzt \
              verwendet zuerst, darunter alle Kategorien nach Gruppe mit Available rechts. Am Handy \
              als Sheet; aus einem offenen Sheet heraus ersetzt der Picker dessen Inhalt (sheetContent, \
              §7.15). ADR 0004, ADR 0005, styleguide.md §7.1/§7.2."
        row [
            // data-ds-card-trigger: Export-Skript öffnet den Picker über diesen stabilen
            // Selektor, bevor es components-categorypicker.html schreibt. Das Sheet portalt
            // nach document.body, ein umschließendes data-ds-card würde es nicht erfassen.
            Html.span [
                prop.custom ("data-ds-card-trigger", "components-categorypicker")
                prop.custom ("data-ds-card-name", "Kategorienauswahl")
                prop.custom ("data-ds-card-subtitle", "Sheet mit Vorschlägen, Zuletzt verwendet und Gruppenliste mit Available")
                prop.children [ Button.secondary "Kategorie wählen" (fun () -> setOpen true) ]
            ]
            if chosen <> "" then Badge.info chosen
        ]
        CategoryPicker.sheet isOpen (Some "REWE Markt") props
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
    let inPanelOpen, setInPanelOpen = React.useState false
    let toBeBudgetedOpen, setToBeBudgetedOpen = React.useState false
    let selectionSheetOpen, setSelectionSheetOpen = React.useState false
    // Sheet einer Regel: die Auswahl der Kategorie ersetzt den Inhalt des Sheets (sheetContent, §7.15).
    let ruleSheetOpen, setRuleSheetOpen = React.useState false
    let rulePickerOpen, setRulePickerOpen = React.useState false
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
          Recent = []
          Accounts = []
          CurrentAccount = None
          AccountsLoad = CategoryPicker.PickerReady
          OfferWithout = true
          ToBeBudgeted = None
          StandLabel = Some "Stand 14:02"
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect =
            CategoryPicker.categoryChoice (fun choice ->
                selectCounter.current <- selectCounter.current + 1
                setSelectCount selectCounter.current
                setCurrent choice
                setChosenText (nameOf choice)
                close ())
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
                            LoadState = CategoryPicker.PickerFailed None
                            OnRetry = fun () -> setRetryCount (retryCount + 1) }
                ]
            ]
            Html.div [
                prop.custom ("data-ds-demo", "money-available")
                prop.className "flex items-center gap-4"
                prop.children [
                    Money.available 35.45m "EUR"
                    Money.available -12.5m "EUR"
                    Money.available 0m "EUR"
                ]
            ]
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-to-be-budgeted")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-to-be-budgeted-trigger")
                        prop.children [ Button.secondary "Popover mit Zu verteilen" (fun () -> setToBeBudgetedOpen (not toBeBudgetedOpen)) ]
                    ]
                    CategoryPicker.popover
                        toBeBudgetedOpen
                        { pickerProps (fun () -> setToBeBudgetedOpen false) with
                            OfferWithout = false
                            ToBeBudgeted = Some { Amount = 35.45m; Currency = "EUR" }
                            OnSelect = fun _ -> setToBeBudgetedOpen false }
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
            Html.div [
                prop.className "relative"
                prop.custom ("data-ds-demo", "category-picker-in-panel")
                prop.children [
                    Html.span [
                        prop.custom ("data-ds-demo", "category-picker-in-panel-trigger")
                        prop.children [
                            Button.secondary "Popover der Auswahl im Panel (PC, nach unten, ohne Haken)" (fun () -> setInPanelOpen (not inPanelOpen))
                        ]
                    ]
                    CategoryPicker.selectionPopoverInPanel inPanelOpen { pickerProps (fun () -> setInPanelOpen false) with Current = None }
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
        subTitle "Auswahl ersetzt den Inhalt des Sheets"
        note "sheetContent: aus einem offenen Sheet heraus ersetzt der Picker dessen Inhalt, Titel und Schließen stellt das Sheet. Nach der Wahl oder beim Schließen kehrt das Sheet zu seinem Inhalt zurück (§7.15). So arbeiten Neue Buchung, Buchungs-Detail, Split und Geld verschieben."
        Html.span [
            prop.custom ("data-ds-demo", "category-picker-sheet-content-trigger")
            prop.children [ Button.secondary "Sheet einer Regel öffnen (Handy)" (fun () -> setRuleSheetOpen true) ]
        ]
        if ruleSheetOpen && rulePickerOpen then
            BottomSheet.view
                { IsOpen = true
                  OnClose = fun () -> setRulePickerOpen false
                  Title = CategoryPicker.sheetTitle []
                  Subtitle = None
                  Footer = None }
                [ CategoryPicker.sheetContent (pickerProps (fun () -> setRulePickerOpen false)) ]
        else
            BottomSheet.view
                { IsOpen = ruleSheetOpen
                  OnClose = fun () -> setRuleSheetOpen false
                  Title = "Regel bearbeiten"
                  Subtitle = None
                  Footer = None }
                [ Html.div [
                      prop.className "py-2"
                      prop.custom ("data-ds-demo", "category-picker-sheet-content")
                      prop.children [
                          Input.pickerField
                              "styleguide-rule-category"
                              "Kategorie"
                              (Some (nameOf current))
                              "Kategorie wählen"
                              None
                              rulePickerOpen
                              (fun () -> setRulePickerOpen true)
                      ]
                  ] ]
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

/// Payee-Auswahl (ledger-fxt09): Suchfeld mit dem heutigen Payee, „„…“ übernehmen“ für eine neue
/// Eingabe, darunter die Payees nach Zahl der Buchungen. Ein Klick übernimmt sofort. Erfundene
/// Beispieldaten, Zustand lokal. Im Kassenbuch steht sie im Sheet (Handy) oder im Dialog (PC).
[<ReactComponent>]
let private PayeePickerDemo () =
    let chosen, setChosen = React.useState<string option> (Some "REWE SAGT DANKE 4711")
    let error, setError = React.useState<string option> None
    section "Payee-Auswahl" [
        note "Ein Auswahl-Dialog nach §7.9 für einen frei wählbaren Text: oben die Suche, vorbelegt mit dem heutigen Payee. Passt die Eingabe zu keinem vorhandenen Payee, steht „„Eingabe“ übernehmen“ als erste Zeile. Ein leerer oder zu langer Payee steht als Feldfehler unter dem Suchfeld. Hat die Buchung einen Payee, steht unter den Vorschlägen „Payee entfernen“ in ruhigem Text."
        Html.div [
            prop.custom ("data-ds-demo", "payee-picker")
            prop.className "max-w-sm"
            prop.children [
                PayeePicker.view
                    { Current = chosen
                      Suggestions = [ "Rewe", 42; "REWE SAGT DANKE 4711", 7; "Edeka Center", 18; "Bäckerei Krume", 25 ]
                      Loading = false
                      LoadError = None
                      FieldError = error
                      FocusSearch = false
                      Busy = false
                      OnPick =
                        fun payee ->
                            match Shared.Domain.normalizePayeeTarget payee with
                            | Ok trimmed ->
                                setError None
                                setChosen trimmed
                            | Error reason -> setError (Some reason)
                      OnSearchChanged = fun () -> setError None
                      OnClose = ignore }
            ]
        ]
        caption $"""Gewählt: {chosen |> Option.defaultValue "ohne Payee"}"""
    ]

/// Split-Editor (ledger-pafkp): Summe, Zeilen, Knopfzeile "Zeile hinzufügen" und "Barabhebung", Fußleisten und der
/// gemeinsame Ziel-Picker mit dem Abschnitt "Übertrag auf Konto" (design-system-bym4m). Erfundene Beispieldaten, Zustand lokal.
[<ReactComponent>]
let private SplitEditorDemo () =
    let amountA, setAmountA = React.useState "23,40"
    // Vorbelegt mit einem Tippfehler: die Zeile zeigt den Fehlerzustand, der Rest bleibt offen (gelb).
    let amountB, setAmountB = React.useState "12,5o"
    let targetPopoverOpen, setTargetPopoverOpen = React.useState false
    let targetSheetOpen, setTargetSheetOpen = React.useState false
    let chosenTarget, setChosenTarget = React.useState ""

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

    let categories : YnabCategory list =
        [ "Möbel"; "Haushalt"; "Lebensmittel" ]
        |> List.mapi (fun i name ->
            { Id = YnabCategoryId (Guid.Parse ("00000000-0000-0000-0000-" + (string (3100 + i)).PadLeft(12, '0')))
              Name = name
              GroupName = "Alltag"
              Available = Some { Amount = 120m; Currency = "EUR" }
              Hidden = false })

    let targetProps (close: unit -> unit) : CategoryPicker.CategoryPickerProps =
        { Categories = categories
          Current = None
          Suggested = []
          Recent = []
          Accounts = accounts
          CurrentAccount = None
          AccountsLoad = CategoryPicker.PickerReady
          OfferWithout = true
          ToBeBudgeted = None
          StandLabel = None
          LoadState = CategoryPicker.PickerReady
          OnRetry = ignore
          OnSelect =
            fun choice ->
                match choice with
                | CategoryPicker.ChoseAccount id ->
                    accounts
                    |> List.tryFind (fun a -> a.Id = id)
                    |> Option.iter (CategoryPicker.accountLabel >> setChosenTarget)
                | CategoryPicker.ChoseCategory (Some id) ->
                    categories
                    |> List.tryFind (fun c -> c.Id = id)
                    |> Option.iter (fun c -> setChosenTarget c.Name)
                | CategoryPicker.ChoseCategory None -> setChosenTarget CategoryPicker.withoutCategoryLabel
                | CategoryPicker.ChoseToBeBudgeted -> ()
                close ()
          OnClose = close }

    let lineProps (index: int) (label: string) (chosen: bool) (amount: string) (setAmount: string -> unit) : SplitEditor.LineProps =
        { Index = index
          TargetLabel = label
          TargetChosen = chosen
          AmountText = amount
          AmountError =
            if Components.Ledger.SplitEditor.lineAmountInvalid amount then Some Components.Ledger.Capture.amountInvalidText else None
          Memo = ""
          Disabled = false
          CanRemove = true
          OnPickTarget = fun () -> setTargetPopoverOpen (not targetPopoverOpen)
          OnAmount = setAmount
          OnMemo = ignore
          OnRest = fun () -> setAmount (Components.Ledger.SplitEditor.amountText (total - parse amountA - parse amountB + parse amount))
          OnRemove = ignore }

    section "27. Split-Editor (Buchung aufteilen)" [
        note "Der Editor im Buchungs-Detail (Ablauf 4): Gesamtbetrag und Rest über den Zeilen, je Zeile \
              Kategorie oder Übertrag-Ziel, Betrag mit Rest-Knopf und Memo. Der Rest ist neutral bei \
              0,00 €, gelb, solange noch etwas offen ist, und rot, wenn zu viel verteilt ist. Ein unlesbarer \
              Betrag zeigt rote Ziffern und aria-invalid. Zeile entfernen ist ein removeIcon, bei einer \
              einzigen Zeile gesperrt. Speichern ist erst bei Rest 0,00 € aktiv, Zeile hinzufügen \
              dann ausgegraut. Ein abgelehntes Speichern steht über Speichern im Fuß, kein Toast. Genau eine primäre Aktion (Speichern). Das Ziel einer Zeile wählt ein \
              einziger Picker: Kategorien und darunter der Abschnitt Übertrag auf Konto, die Suche läuft \
              über beides, das Präfix Konto: zeigt nur Konten. Am PC als Popover, am Handy ersetzt er den \
              Inhalt des Sheets (sheetContent, §7.15). ADR 0005, ADR 0006, ADR 0049."
        Html.div [
            prop.custom ("data-ds-demo", "split-editor")
            prop.className "max-w-md rounded-xl border border-border-subtle overflow-hidden"
            prop.children [
                SplitEditor.summary
                    { TotalText = text total
                      RestText = text rest
                      Rest =
                        if rest > 0m then SplitEditor.RestOpen
                        elif rest < 0m then SplitEditor.RestOver
                        else SplitEditor.RestBalanced }
                Html.div [
                    prop.className "relative"
                    prop.children [
                        SplitEditor.line (lineProps 0 "Wohnen: Möbel" true amountA setAmountA)
                        CategoryPicker.popoverInPanel targetPopoverOpen (targetProps (fun () -> setTargetPopoverOpen false))
                    ]
                ]
                SplitEditor.line (lineProps 1 "Kategorie oder Konto wählen" false amountB setAmountB)
                SplitEditor.lineActions (not balanced) ignore true ignore
                SplitEditor.footer None balanced false ignore ignore
            ]
        ]
        row [
            Html.span [
                prop.custom ("data-ds-demo", "split-target-picker-sheet-trigger")
                prop.children [ Button.secondary "Ziel-Picker als Sheet (Handy)" (fun () -> setTargetSheetOpen true) ]
            ]
            if chosenTarget <> "" then Badge.info chosenTarget
        ]
        CategoryPicker.sheet targetSheetOpen None (targetProps (fun () -> setTargetSheetOpen false))
        Html.div [
            prop.custom ("data-ds-demo", "split-editor-mobile-save")
            prop.className "max-w-sm"
            prop.children [
                SplitEditor.sheetFooter (Some "Aufteilung nicht gespeichert: YNAB nicht erreichbar") balanced false ignore
            ]
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
                    ActionIcon = None
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
                // ledger-gwr9j: nach rechts mit Zeichen vor dem Label, wie „✓ Geprüft“ im Kassenbuch.
                Swipe.SwipeableRowRight {
                    ActionLabel = "Geprüft"
                    ActionIcon = Some (Icons.check Icons.XS Icons.Success)
                    ActionClass = "reviewed"
                    OnCommit = fun () -> setSwiped true
                    Children =
                        Html.div [
                            prop.className "tx-row mt-2 flex items-center justify-between bg-surface-card border border-border-subtle rounded-lg p-4"
                            prop.children [
                                Html.div [
                                    prop.className "flex flex-col"
                                    prop.children [
                                        Html.span [ prop.className "text-sm font-semibold text-text-primary"; prop.text "Stadtwerke" ]
                                        Html.span [ prop.className "text-xs text-text-muted"; prop.text "Nach rechts wischen zum Prüfen" ]
                                    ]
                                ]
                                Money.simple -84.20m "EUR"
                            ]
                        ]
                }
            ]
        ]
        if swiped then Badge.success "Gewischt (Demo)"
    ]

// ============================================
// Navigation
// ============================================

/// Navigation-Sektion (design-system-t82sg, design-system-k6kfw): Leiste unten am Handy und Seitenleiste ab
/// 1024 px als Vorschau ohne `fixed` (`Navigation.bottomBarPreview`, `railPreview`, `sidebarPreview`). Rein
/// präsentational, es gibt keine echte Navigation von hier aus.
let private navigationSection =
    let demoProps : Navigation.Props =
        { Current = Some Navigation.Ledger
          OnNavigate = ignore
          SettingsActive = false
          OnSettings = ignore
          OnBrand = ignore
          Accounts =
            Navigation.SidebarAccountsShown
                [ { Key = "alle"; Label = "Alle Konten"; Value = Navigation.AccountAmount (4300m, "EUR"); IsActive = false; OnClick = ignore }
                  { Key = "giro"; Label = "Girokonto"; Value = Navigation.AccountAmount (2418.07m, "EUR"); IsActive = true; OnClick = ignore }
                  { Key = "karte"; Label = "Kreditkarte"; Value = Navigation.AccountAmount (-312.40m, "EUR"); IsActive = false; OnClick = ignore } ]
          OverviewCount = 4
          HideBottomBar = false }
    section "21. Navigation" [
        note "Drei Orte (Styleguide §7.4): Leiste unten am Handy, Rail links von 768 bis 1023 px, Seitenleiste mit Konten ab 1024 px. Einstellungen hinter dem Zahnrad. In der App fix positioniert; hier als Vorschau eingebettet."
        // data-ds-card (s. cardSection) ist der Export-Marker für components-navigation.html.
        Html.div [
            prop.custom ("data-ds-card", "components-navigation")
            prop.custom ("data-ds-card-name", "Navigation")
            prop.custom ("data-ds-card-subtitle", "Hauptnavigation der App")
            prop.custom ("data-ds-group", "Components")
            prop.className "space-y-4"
            prop.children [
                subTitle "Seitenleiste (ab 1024 px)"
                Navigation.sidebarPreview demoProps
                subTitle "Rail (768 bis 1023 px)"
                Navigation.railPreview demoProps
                subTitle "Leiste unten (Handy)"
                Html.div [
                    prop.className "max-w-sm"
                    prop.children [ Navigation.bottomBarPreview demoProps ]
                ]
                subTitle "Kopf eines Orts"
                PageHeader.top
                    None
                    "Übersicht"
                    None
                    [ PageHeader.iconButton "Abrufen" Icons.fetch ignore
                      PageHeader.iconButton "Einstellungen" Icons.gear ignore ]
                subTitle "Symbolknopf mit Laufzustand (iconButtonBusy, „Jetzt spiegeln“ im Kassenbuch, daneben „Abrufen“ wie im Kopf am Handy)"
                Html.div [
                    prop.className "flex items-center gap-2"
                    prop.children [
                        PageHeader.iconButton "Abrufen" Icons.fetch ignore
                        PageHeader.iconButtonBusy "Jetzt spiegeln" Icons.sync false ignore
                        PageHeader.iconButtonBusy "Jetzt spiegeln (läuft)" Icons.sync true ignore
                    ]
                ]
                subTitle "Kopf einer Unterseite"
                PageHeader.top (Some (PageHeader.backButton ignore)) "Targets prüfen" None []
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

/// Tabs: gleich breite Segmente, der aktive im Akzent. Hält den gewählten Tab selbst, damit die Karte
/// ohne Msg-Fälle bedienbar ist.
[<ReactComponent>]
let private TabsDemo () =
    let active, setActive = React.useState "Geld"
    let tab label : Tabs.Tab =
        { Label = label; IsActive = (active = label); OnClick = fun () -> setActive label }
    section "23a. Tabs" [
        note "Bereiche einer Sache in fester Höhe, erster Nutzer ist der Inspektor der Budget-Seite. Auswahl und Tastatur-Fokus zeigt derselbe Akzent-Strich, ohne Ring und ohne Fläche: Pfeiltasten wählen sofort. styleguide.md §5."
        Html.div [
            prop.custom ("data-ds-card", "components-tabs")
            prop.custom ("data-ds-card-name", "Tabs")
            prop.custom ("data-ds-card-subtitle", "Gleich breite Segmente, aktiv im Akzent")
            prop.className "max-w-sm"
            prop.children [
                Tabs.bar "demo-tabs" "Beispiel" [ tab "Geld"; tab "Buchungen"; tab "Historie"; tab "Target" ]
            ]
        ]
    ]

/// Segmented und die Bausteine des Tabs Geld (budgeting-c5w2n). Hält den Modus selbst, wie `TabsDemo`.
[<ReactComponent>]
let private SegmentedDemo () =
    let active, setActive = React.useState "Zuweisen"
    let amount, setAmount = React.useState 20m
    let isOutflow, setIsOutflow = React.useState true
    let draft, setDraft = React.useState "12,50"
    let segment label =
        Segmented.segment label (active = label) (fun () -> setActive label)
    section "23b. Segmented, Schritte und Zeilen von und nach" [
        note "Umschalter mit aria-pressed, aktiv im Akzent. Schritte halten den Fokus im Feld daneben, Segmente mit KeepsFocus auch. Die wählbare Zeile ersetzt am Handy den Inhalt des Sheets (sheetContent), im Panel CategoryPicker.popoverInPanelRevealed. styleguide.md §5."
        Html.div [
            prop.custom ("data-ds-card", "components-segmented")
            prop.custom ("data-ds-card-name", "Segmented")
            prop.custom ("data-ds-card-subtitle", "Modi, Schritte, Zeilen von und nach")
            prop.className "flex flex-col gap-3 max-w-sm"
            prop.children [
                Segmented.view "demo-segmented" "Beispiel" [ segment "Zuweisen"; segment "Holen"; segment "Abgeben" ]
                Segmented.view
                    "demo-segmented-disabled"
                    "Beispiel gesperrt"
                    [ for label in [ "Monat"; "Woche"; "Jahr" ] do
                          { Segmented.segment label (label = "Monat") ignore with Disabled = true } ]
                caption "Gesperrt: disabled, gedämpft, kein Hover (Rhythmus im Tab Target, solange das Speichern läuft)"
                Segmented.viewColumns
                    "demo-segmented-columns"
                    "Schnellwahl"
                    Segmented.TwoColumns
                    [ for label in [ "Dieser Monat"; "Letzter Monat"; "Dieses Jahr"; "12 Monate" ] do
                          Segmented.segment label (label = "Dieser Monat") ignore ]
                caption "viewColumns TwoColumns: vier Optionen in zwei Reihen, wenn sie nebeneinander bei 320 px nicht sicher passen (Schnellwahl Zeitraum im Filter des Kassenbuchs)"
                Segmented.view
                    "demo-segmented-keeps-focus"
                    "Ausgabe oder Einnahme"
                    [ { Segmented.segment "Ausgabe" isOutflow (fun () -> setIsOutflow true) with KeepsFocus = true }
                      { Segmented.segment "Einnahme" (not isOutflow) (fun () -> setIsOutflow false) with KeepsFocus = true } ]
                Input.text
                    { Input.textInputDefaults with
                        Value = draft
                        OnChange = setDraft
                        Placeholder = "0,00"
                        Size = Input.Large
                        InputMode = Some "decimal"
                        AriaLabel = Some "Betrag im Beispiel"
                        ClassName = Some (Input.amountFieldClass true false) }
                caption "KeepsFocus: das Segment nimmt dem Betragsfeld den Fokus nicht (preventDefault auf mousedown), am Handy bleibt die Tastatur offen. Ausgabe oder Einnahme im Sheet Betrag und in Neue Buchung."
                Html.div [
                    prop.className "grid grid-cols-6 gap-1"
                    prop.children [
                        for label, delta in [ "-50", -50m; "-20", -20m; "-10", -10m; "+10", 10m; "+20", 20m; "+50", 50m ] do
                            Button.amountStep "demo-step" label label (fun () -> setAmount (amount + delta))
                    ]
                ]
                Money.afterAmount amount "EUR"
                Button.sideRow "demo-side" "von: Kategorie wählen" false ignore [
                    Html.span [ prop.text "von Kleidung" ]
                    Money.available 30m "EUR"
                ]
                Button.sideRowFixed "demo-side-fixed" [
                    Html.span [ prop.text "nach Urlaub" ]
                    Money.available -10m "EUR"
                ]
            ]
        ]
    ]

/// ChipBar: aktive und inaktive Chips, mit und ohne "×", "Alle Filter löschen".
let private chipBarSection =
    section "23. ChipBar" [
        note "Filterleiste des Kassenbuchs: jeder Chip ist ein Rahmen mit zwei Knöpfen \
              (Auswahl öffnen, Filter entfernen mit ×). Aktive Chips im Akzent, inaktive \
              gedämpft. \"Alle Filter löschen\" steht am Ende und erscheint nur bei aktivem \
              Filter. Der Tastatur-Fokus liegt als Ring direkt auf dem Rand der Pille, \
              am aktiven Chip als eine dickere Akzent-Linie. ADR 0043, styleguide.md §5 und §7.3."
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
                // chipWith (ledger-cnaz3): Filterleiste am PC mit Zahl hinter dem Umschalter und Pfeil am Chip
                // „Kategorie“, „Filter löschen“ als Chip und die Suche rechtsbündig (Prototyp Version 17).
                ChipBar.bar None [
                    ChipBar.chipWith (Some 4) false { Label = "Zu prüfen"; IsActive = true; OnClick = ignore; OnRemove = None }
                    ChipBar.chipWith (Some 2) false { Label = "Lena"; IsActive = false; OnClick = ignore; OnRemove = None }
                    ChipBar.chipWith (Some 1) false { Label = "Ohne Kategorie"; IsActive = false; OnClick = ignore; OnRemove = None }
                    ChipBar.chipWith None true { Label = "Kategorie"; IsActive = false; OnClick = ignore; OnRemove = Some ignore }
                    ChipBar.chipWith None true { Label = "Kategorie: Strom"; IsActive = true; OnClick = ignore; OnRemove = Some ignore }
                    ChipBar.chip { Label = "Filter löschen"; IsActive = false; OnClick = ignore; OnRemove = None }
                    Input.searchFieldCompact "styleguide-search-compact" "Buchungen durchsuchen" "Suchen…" "" ignore
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

/// Buchungs-Detail (ledger-q1hw2, ADR 0053, geordnet nach Prototyp Version 17 in ledger-zfj78): die Bausteine des
/// Panels und des Sheets im Kassenbuch. Prüf-Knopf und Historie haben lokalen Zustand.
[<ReactComponent>]
let private DetailDemo () =
    let reviewed, setReviewed = React.useState false
    let historyOpen, setHistoryOpen = React.useState true

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
        note "Das Detail einer Buchung: Seitenpanel (30 % der Fensterbreite, 380 bis 520 px) am PC, BottomSheet am Handy und in der \
              Kompaktliste. Kopf mit Payee, Betrag mit Prüfstand, Hinweisleiste mit Grund und genau einer Aktion, \
              Prüf-Knopf, Feldzeilen mit „alle zeigen“, Knopfzeile Aufteilen und Lena, aufklappbare Historie nach \
              §7.11, Herkunft. Farbe nur für Hinweis und Prüfstand (ADR 0049, ADR 0053, Prototyp Version 17)."

        card "components-detail-notice" "Detail: Hinweisleiste" "Rot ohne Aktion, Gelb ohne Aktion, Gelb mit Aktion" [
            Detail.notice
                { Tone = Detail.NoticeRed
                  Badge = "In YNAB gelöscht"
                  Reason = "Gelöscht in YNAB am 11.09.2026 08:20. Zählt nicht zum Kontostand."
                  Action = None }
            Detail.notice
                { Tone = Detail.NoticeYellow
                  Badge = "Noch nicht in YNAB"
                  Reason = "Push gescheitert am 10.09.2026 18:02: Netzwerkfehler. BB versucht es beim nächsten Lauf erneut."
                  Action = None }
            Detail.notice
                { Tone = Detail.NoticeYellow
                  Badge = "Prüfvermerk nicht in YNAB"
                  Reason = "YNAB hat den Prüfvermerk nicht angenommen (Fehler 500)."
                  Action = Some { Label = "Erneut senden"; Busy = false; Disabled = false; OnClick = ignore } }
            // Gelb (NoticeYellow): braucht dich, etwa im Tab Geld bei einer offenen Rückschreibung
            // (budgeting-dgrp7, ADR 0087).
            Detail.notice
                { Tone = Detail.NoticeYellow
                  Badge = "noch nicht in YNAB"
                  Reason = "Zugewiesen zeigt BBs Wert, YNAB hat ihn noch nicht."
                  Action = Some { Label = "Erneut senden"; Busy = false; Disabled = false; OnClick = ignore } }
        ]

        card "components-detail-external-links" "Detail: Bestell-Link" "„Bestellung bei Amazon“ mit Nummer darunter und „öffnen“, ein anderer Link mit seiner Beschriftung; öffnen in neuem Tab" [
            Detail.externalLinkRow "Bestellung bei Amazon" (Some "304-1234567-7654321") "https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID=304-1234567-7654321"
            Detail.externalLinkRow "PayPal Activity" None "https://www.paypal.com/activities"
        ]
        card "components-detail-head" "Detail: Kopf, Betrag mit Prüfstand, Prüf-Knopf" "Payee als Titel mit „<Tag> · <Konto>“, Kopf eines Formulars („Neue Buchung“), Betrag mit „zu prüfen“ oder „geprüft“, „Geprüft“ als Hauptaktion bzw. „Als ungeprüft markieren“ neutral" [
            Detail.titleHead "REWE Mitte" false "Gestern · Girokonto" (Some ignore)
            Detail.titleHead "ohne Payee" true "10.09.2026 · Girokonto" (Some ignore)
            // Kopf eines Formulars im Panel („Neue Buchung“, ledger-zf6zg): ohne Unterzeile, Schließen mit eigenem Namen.
            Detail.formHead "Neue Buchung" "Formular schließen" false ignore
            Detail.amountRow (Detail.amountButton -48.12m "EUR" "Betrag bearbeiten" (Some ignore)) (Detail.reviewStatus (not reviewed))
            Detail.reviewButton
                (if reviewed then "Als ungeprüft markieren" else "Geprüft")
                (not reviewed)
                false
                (fun () -> setReviewed (not reviewed))
            Detail.reviewButton "Geprüft" true true ignore
        ]
        card "components-detail-fields" "Detail: Felder mit „alle zeigen“" "Kategorie mit Pfeil, Split-Karte, Angebot, Memo als Block, Payee mit „alle zeigen“ und Pfeil, gesperrte Zeilen" [
            // Kategorie: „alle zeigen“ setzt den Filter, der Pfeil öffnet den Picker (ledger-sfab0, ledger-zfj78).
            Detail.fieldRowWith
                (Some ignore)
                "styleguide-detail-category"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textPrimary "Alltag: Lebensmittel")
                (Some (
                    React.fragment [
                        Detail.showAllLink "Nur Buchungen in Alltag: Lebensmittel zeigen" ignore
                        Detail.pickerArrow "Kategorie ändern" false None ignore
                    ]
                ))
            Detail.fieldRowWith
                (Some ignore)
                "styleguide-detail-category-missing"
                "Kategorie"
                (Detail.valueText Tokens.Colors.attention "ohne Kategorie")
                (Some (Detail.pickerArrow "Kategorie ändern" false None ignore))
            Detail.fieldRow
                "styleguide-detail-category-locked"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textMuted "Keine Kategorie")
                (Some (Detail.pickerArrow "Kategorie ändern" false (Some "In YNAB gelöscht, die Kategorie lässt sich nicht ändern") ignore))
            Detail.fieldRow
                "styleguide-detail-category-split"
                "Kategorie"
                (Detail.valueText Tokens.Colors.textPrimary "Aufgeteilt")
                (Some (Detail.pickerArrow "Kategorie ändern" false (Some "Aufgeteilt, die Kategorie steht je Zeile") ignore))
            Detail.splitCard [ "Wohnen: Möbel", "-23,40 €"; "Übertrag: Tagesgeld", "-58,50 €" ]
            // Angebot unter der Kategorie nach einer Änderung (ledger-hg5m5): annehmen oder ausblenden.
            Detail.offerRow "styleguide-detail-offer" "Regel für „REWE Mitte“ anlegen?" "Anlegen" ignore "Angebot ausblenden" ignore
            // Memo als Block (design-system-x045q, Skizze ledger-vf5h6 Bild 11): ohne Memo nur die Kopfzeile, mit
            // langem Memo der ganze Text über die volle Breite, gesperrt mit dem Grund darunter.
            Detail.fieldBlock
                (Some ignore)
                "styleguide-detail-memo"
                "Memo"
                (Detail.fieldBlockAction "styleguide-detail-memo-action" "hinzufügen" "Memo hinzufügen" None ignore)
                []
            Detail.fieldBlock
                (Some ignore)
                "styleguide-detail-memo-set"
                "Memo"
                (Detail.fieldBlockAction "styleguide-detail-memo-action-set" "Bearbeiten" "Bearbeiten" None ignore)
                [ Detail.blockText Tokens.Colors.textPrimary "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke, Kohle und die Torte für den Geburtstag" ]
            Detail.fieldBlock
                None
                "styleguide-detail-memo-locked"
                "Memo"
                (Detail.fieldBlockAction
                    "styleguide-detail-memo-action-locked"
                    "Bearbeiten"
                    "Bearbeiten"
                    (Some "In YNAB gelöscht, das Memo lässt sich nicht ändern")
                    ignore)
                [ Detail.blockText Tokens.Colors.textMuted "Werkzeug + Lampe"
                  Detail.blockLockHint "styleguide-detail-memo-lock-hint" "In YNAB gelöscht, das Memo lässt sich nicht ändern" ]
            // Payee mit „alle zeigen“ (setzt die Suche) und Pfeil `›` (ledger-fxt09): forwardArrow öffnet eine eigene
            // Auswahl, die den Inhalt ersetzt; änderbar und gesperrt mit Hinweistext am Knopf.
            Detail.fieldRowWith
                (Some ignore)
                "styleguide-detail-payee"
                "Payee"
                (Detail.valueText Tokens.Colors.textPrimary "REWE Mitte")
                (Some (
                    React.fragment [
                        Detail.showAllLink "Nur Buchungen von REWE Mitte zeigen" ignore
                        Detail.forwardArrow "styleguide-detail-payee-arrow" "Payee ändern" None ignore
                    ]
                ))
            Detail.fieldRow
                "styleguide-detail-payee-locked"
                "Payee"
                (Detail.valueText Tokens.Colors.textMuted "REWE Mitte")
                (Some (Detail.forwardArrow "styleguide-detail-payee-arrow-locked" "Payee ändern" (Some "In YNAB gelöscht, der Payee lässt sich nicht ändern") ignore))
        ]
        card "components-detail-actions" "Detail: Knopfzeile, Herkunft" "Aufteilen und Lena fragen gleich breit, Rückfrage zurücknehmen, gesperrt mit Grund darunter; Herkunft als kleine Zeile" [
            Detail.actionRow
                []
                [ Detail.rowAction "styleguide-detail-split-action" "Aufteilen" None None false ignore
                  Detail.rowAction "styleguide-detail-flag-action" "Lena fragen" (Some (Icons.flag Icons.SM Icons.LenaFlag)) None false ignore ]
                []
            Detail.actionRow
                []
                [ Detail.rowAction "styleguide-detail-split-change" "Ändern" None None false ignore
                  Detail.rowAction "styleguide-detail-flag-withdraw" "Rückfrage zurücknehmen" (Some (Icons.flag Icons.SM Icons.LenaFlag)) None false ignore ]
                []
            Detail.actionRow
                []
                [ Detail.rowAction "styleguide-detail-split-locked" "Aufteilen" None (Some "Abgeglichene Buchungen lassen sich nicht aufteilen") false ignore
                  Detail.rowAction "styleguide-detail-flag-locked" "Lena fragen" (Some (Icons.flag Icons.SM Icons.LenaFlag)) (Some lenaColorMissingReason) false ignore ]
                [ "styleguide-detail-split-lock-hint", "Abgeglichene Buchungen lassen sich nicht aufteilen"
                  "styleguide-detail-flag-lock-hint", lenaColorMissingReason ]
            Detail.originLine "Herkunft: Comdirect-Import"
        ]

        card "components-detail-memo" "Detail: Memo bearbeiten" "Mehrzeiliges Feld, wächst mit dem Text, mit Zähler, Abbrechen und Speichern; kurzer, langer und zu langer Text; Hinweis Memo nicht in YNAB" [
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
                { Value = "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke, Kohle und die Torte für den Geburtstag"
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
                { Tone = Detail.NoticeYellow
                  Badge = "Memo nicht in YNAB"
                  Reason = "Keine Verbindung zu YNAB: Zeitüberschreitung."
                  Action = Some { Label = "Erneut senden"; Busy = false; Disabled = false; OnClick = ignore } }
        ]

        card "components-detail-history" "Detail: Historie (§7.11)" "Aufklappbare Zeile „Historie (n)“, je Eintrag Zeitpunkt, Kürzel der Herkunft und Text, rechts der Betrag, falls es einen gibt; neueste oben" [
            Detail.historySection
                "Historie (5)"
                historyOpen
                (fun () -> setHistoryOpen (not historyOpen))
                (HistoryList.list
                    "styleguide-history"
                    [ { Key = "5"; When = "11.09. 08:20"; Who = "YNAB"; Text = "In YNAB gelöscht"; Amount = None }
                      { Key = "4"; When = "10.09. 18:10"; Who = "BB"; Text = "In BB als geprüft markiert"; Amount = None }
                      { Key = "3"; When = "10.09. 17:55"; Who = "REGEL"; Text = "Regel: Kategorie Lebensmittel gesetzt"; Amount = None }
                      { Key = "2"; When = "10.09. 17:55"; Who = "IMPORT"; Text = "Comdirect-Import: Buchung erfasst"; Amount = Some "-48,12 €" }
                      { Key = "1"; When = "10.09. 17:50"; Who = "BOB"; Text = "Barbuchung angelegt"; Amount = Some "-12,00 €" } ])
            Html.div [ prop.className "px-4"; prop.children [ HistoryList.empty "Noch keine Einträge" ] ]
        ]

        // budgeting-yarty: die Budget-Historie und der Tab Historie im Kategorie-Detail zeigen über §7.11 hinaus Monat
        // und Stand als Zusatz und klappen einen Durchlauf Targets anwenden zu seinen Kategorien auf.
        card "components-history-note" "HistoryList: Zusatz und Aufklappen" "rowWithNote (Zusatz unter dem Text: Monat, Stand, Grund), disclosureRow (die ganze Zeile klappt auf, Chevron am Ende des Zusatzes) mit subRow je Kategorie" [
            Html.div [
                prop.className "px-3"
                prop.children [
                    HistoryList.listOf "styleguide-history-note" [
                        HistoryList.rowWithNote
                            { Key = "3"; When = "05.11. 12:02"; Who = "BB"; Text = "Target 120,00 € → 150,00 €, Restaurant"; Amount = None }
                            [ Html.span [ prop.text "November 2026" ] ]
                        HistoryList.rowWithNote
                            { Key = "2"; When = "04.11. 07:55"; Who = "YNAB"; Text = "In YNAB zugewiesen, Auto"; Amount = Some "+20,00 €" }
                            [ Html.span [ prop.text "November 2026" ] ]
                        HistoryList.disclosureRow
                            { Key = "1"; When = "01.11. 08:12"; Who = "BB"; Text = "Targets anwenden, 2 Kategorien"; Amount = Some "+860,00 €" }
                            [ Html.span [ prop.text "November 2026" ]; Badge.errorSmall "1 von 2 fehlgeschlagen" ]
                            "styleguide-history-note-lines"
                            true
                            ignore
                            [ HistoryList.subRow "1a" "01.11. 08:12" "Miete" "+800,00 €" [ Html.span [ prop.text "geschrieben" ] ]
                              HistoryList.subRow "1b" "01.11. 08:12" "Strom" "+60,00 €" [ Badge.errorSmall "fehlgeschlagen"; Html.span [ prop.text "Rate-Limit" ] ] ]
                    ]
                ]
            ]
        ]

        // Betrag im Kopf (ledger-m7mye): als Knopf mit Pfeil, vorübergehend gesperrt (ein Aufruf läuft)
        // und gesperrt mit Grund darunter.
        card "components-detail-amount" "Detail: Betrag im Kopf" "Betrag, Betrag als Knopf mit Pfeil (öffnet „Betrag“), Knopf während eines Aufrufs, gesperrt mit Grund" [
            Html.div [
                prop.className "flex flex-col gap-4"
                prop.children [
                    Detail.amount -48.12m "EUR" false
                    Detail.amountButton -48.12m "EUR" "Betrag bearbeiten" (Some ignore)
                    Detail.amountButton -48.12m "EUR" "Betrag bearbeiten" None
                    Detail.amountLocked -48.12m "EUR" false "Abgeglichen, der Betrag lässt sich nicht ändern"
                ]
            ]
        ]

        card "components-detail-panel" "Detail: Seitenpanel" "Kopf mit Payee und „<Tag> · <Konto>“, Betrag mit Prüfstand, Prüf-Knopf, Feldzeilen (ab md sichtbar)" [
            Html.div [
                prop.className "relative h-[28rem] overflow-hidden"
                prop.children [
                    Detail.panel
                        "Detail REWE Mitte"
                        [ Detail.titleHead "REWE Mitte" false "Gestern · Girokonto Comdirect" (Some ignore)
                          Detail.scrollBody [
                              Detail.amountRow (Detail.amountButton -48.12m "EUR" "Betrag bearbeiten" (Some ignore)) (Detail.reviewStatus true)
                              Detail.reviewButton "Geprüft" true false ignore
                              Detail.fieldRowWith
                                  (Some ignore)
                                  "styleguide-detail-panel-category"
                                  "Kategorie"
                                  (Detail.valueText Tokens.Colors.textPrimary "Alltag: Lebensmittel")
                                  (Some (
                                      React.fragment [
                                          Detail.showAllLink "Nur Buchungen in Alltag: Lebensmittel zeigen" ignore
                                          Detail.pickerArrow "Kategorie ändern" false None ignore
                                      ]
                                  ))
                              Detail.fieldBlock
                                  (Some ignore)
                                  "styleguide-detail-panel-memo"
                                  "Memo"
                                  (Detail.fieldBlockAction "styleguide-detail-panel-memo-action" "Bearbeiten" "Bearbeiten" None ignore)
                                  [ Detail.blockText Tokens.Colors.textPrimary "Wocheneinkauf" ]
                              Detail.originLine "Herkunft: Regel"
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
    MultiSelect.barDesktop Tokens.PanelWidths.fixedRight "3 ausgewählt" sum "Geprüft" false false ignore category demoLena demoWithdraw "Auswahl aufheben" "Esc" ignore

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

        card "components-multiselect-summary" "Mehrfachauswahl: Summenzeile" "Akzent-Kante links, Beschriftung im Akzent, Summe der Auswahl rechts" [
            MultiSelect.summaryRow "Auswahl: 3 von 34 Buchungen" sum
        ]

        card "components-multiselect-bar-mobile" "Mehrfachauswahl: Leiste am Handy" "Zwei Reihen: Anzahl und Summe, dann Alle, Geprüft, Kategorie setzen, Auswahl aufheben (nur unter md sichtbar)" [
            barFrame [ demoBarMobile sum category ]
        ]

        card "components-multiselect-bar-desktop" "Mehrfachauswahl: Leiste am PC" "Eine Buchung: Anzahl und Summe links, rechts Geprüft, Kategorie setzen, Lena fragen mit Flag, Rückfrage zurücknehmen, Auswahl aufheben mit Taste (ab md sichtbar, ab 1024 px endet sie am Panel)" [
            barFrame [ demoBarDesktop sum category ]
        ]

        card "components-multiselect-bar-desktop-compact" "Mehrfachauswahl: Leiste am PC ab zwei" "Nur Anzahl, Summe und Auswahl aufheben; die Aktionen stehen im Panel (ledger-cnaz3)" [
            barFrame [ MultiSelect.barDesktopCompact Tokens.PanelWidths.fixedRight "3 ausgewählt" sum "Auswahl aufheben" "Esc aufheben" ignore ]
        ]

        card "components-multiselect-summary-panel" "Mehrfachauswahl: Zusammenfassung im Panel" "Ab zwei gewählten Buchungen: Summe, Ausgaben, Einnahmen, Davon zu prüfen, nach Kategorie, Alle geprüft (§7.7, Prototyp selSummary)" [
            Html.div [
                prop.className $"flex h-[620px] {Tokens.PanelWidths.panel} max-w-full flex-col border-l border-border-default bg-surface-card"
                prop.children (
                    MultiSelect.summaryPanel {
                        Title = "3 Buchungen ausgewählt"
                        Hint = "Umschalt-Klick wählt einen Bereich, Strg- oder Cmd-Klick einzelne"
                        SumLabel = "Summe"
                        Sum = { Amount = -96.19m; Currency = "EUR" }
                        Figures = [ "Ausgaben", "-96,19 €"; "Einnahmen", "0,00 €"; "Davon zu prüfen", "3" ]
                        CategoryHeading = "Nach Kategorie"
                        Categories =
                            [ { Label = "Lebensmittel"; Amount = { Amount = -42.80m; Currency = "EUR" }; Missing = false }
                              { Label = "Geschenke"; Amount = { Amount = -34.99m; Currency = "EUR" }; Missing = false }
                              { Label = "ohne Kategorie"; Amount = { Amount = -18.40m; Currency = "EUR" }; Missing = true } ]
                        Note = None
                        ReviewedLabel = "Alle geprüft"
                        ReviewedBusy = false
                        Locked = false
                        OnReviewed = ignore
                        Category = category
                        Lena = demoLena
                        Withdraw = Some demoWithdraw
                        ClearLabel = "Auswahl aufheben"
                        OnClear = ignore
                    }
                )
            ]
        ]

        card "components-detail-empty-panel" "Panel ohne gewählte Buchung" "Ab 1024 px steht das Panel immer; ohne Auswahl ein gedämpfter Satz ohne Aktion (§7.6, §7.12)" [
            Html.div [
                prop.className "relative h-72 overflow-hidden transform-gpu"
                prop.children [ Detail.emptyPanel "Buchung wählen, um Details zu sehen." ]
            ]
        ]
    ]

// ============================================
// 29. Durchgehen (ledger-v6s7c)
// ============================================

let private reviewCardSection =
    section "29. Durchgehen (Karte für Karte)" [
        note "ReviewCard: Fortschritt 4 px (Spur border, Füllung Akzent), die Karte des Durchgehens (Radius 14 px, Hinweis und Trefferzeile 12 px) mit Payee und Betrag in 20 px, die Duplikat-Karte mit gelbem Rand („braucht dich“) und die Tastenleiste am PC mit kbd-Kästchen. Höchstens drei Schriftgrößen je Ansicht (§7.14): 20 px, 16/14 px, 12 px."
        subTitle "ReviewCard.progress"
        Html.div [ prop.className "max-w-md"; prop.children [ ReviewCard.progress 30.0 (Some "4 von 10") ] ]
        subTitle "ReviewCard.skeleton"
        note "Solange Schlange oder Karte laden: Fortschritt ohne Füllung, die Karte mit grauen Balken in der Höhe ihrer Zeilen und die drei Knopfflächen darunter (§6, Skeleton statt Spinner)."
        Html.div [ prop.className "max-w-md"; prop.children [ ReviewCard.skeleton () ] ]
        subTitle "ReviewCard.card (normal)"
        Html.div [
            prop.className "max-w-md flex flex-col gap-3"
            prop.children [
                ReviewCard.card false [] [
                    ReviewCard.meta "Heute · Girokonto · Comdirect-Import"
                    ReviewCard.payee "Amazon" false "Payee ändern" (Some ignore)
                    ReviewCard.amount "-21,50 €"
                    ReviewCard.field "demo-review-category" "Kategorie" (Some "Kinderkram") "Kategorie wählen" false (Some ignore)
                    ReviewCard.caption "demo-review-order" "aus Bestellnummer"
                    ReviewCard.linkRow "Bestellung bei Amazon" (Some "302-1234567-2222222") "https://www.amazon.de/gp/your-account/order-history"
                    ReviewCard.memoBlock "demo-review-memo" "Abschlag November"
                    ReviewCard.note "demo-review-note" [ Html.span [ ReviewCard.noteName "Lena"; Html.text " ist gefragt. Die Buchung bleibt offen, bis Lena oder du sie prüft." ] ]
                    ReviewCard.linksWithDetails "demo-review-rule" "Regel anlegen" ignore ignore
                ]
                ReviewCard.card false [] [
                    ReviewCard.meta "Gestern · Kreditkarte · YNAB-Spiegel"
                    ReviewCard.payee "Markt" false "" None
                    ReviewCard.amount "-42,80 €"
                    ReviewCard.field "demo-review-missing" "Kategorie" None "Kategorie wählen" false (Some ignore)
                ]
                ReviewCard.foot (ReviewCard.FootHint "Wischen: rechts „Passt“, links „Später“")
                ReviewCard.foot (ReviewCard.FootNeed "Erst Kategorie wählen")
                ReviewCard.foot ReviewCard.FootEmpty
            ]
        ]
        subTitle "ReviewCard.fieldIn und ReviewCard.fieldNote (Kategorie am Feld bestätigt)"
        note "Nach einer Kategorie oder einem Übertrag auf der Karte (ledger-mf2bg, R3 Punkt 1): das Feld leuchtet kurz (FieldFlash, Rand und Ring im Akzent, 0,7 s, ohne Bewegung steht der Ring), darunter 12 px gedämpft „<Name> · <Betrag> verfügbar“, der Betrag in Mono in text-secondary, rechts „Rückgängig“ im Akzent mit 48 px Trefferfläche. Ohne bekannten Betrag nur der Name, sonst „Kategorie entfernt“ oder „Gegenbuchung angelegt“. Warnung und Fehler stehen in der Zeile unter „Passt, weiter“ (Feedback.line). Kein Toast."
        Html.div [
            prop.className "max-w-md flex flex-col gap-3"
            prop.children [
                ReviewCard.card false [] [
                    ReviewCard.payee "Amazon Payments" false "" None
                    ReviewCard.fieldIn ReviewCard.FieldFlash "demo-review-flash" "Kategorie" (Some "Bahn") "Kategorie wählen" false (Some ignore)
                    ReviewCard.fieldNote "Bahn" (Some "84,20 €") { Label = "Rückgängig"; OnClick = ignore }
                    ReviewCard.caption "demo-review-suggested" "Vorgeschlagen"
                ]
                ReviewCard.card false [] [
                    ReviewCard.fieldIn ReviewCard.FieldRest "demo-review-transfer" "Kategorie" (Some "→ Tagesgeld") "Kategorie wählen" false (Some ignore)
                    ReviewCard.fieldNote "Gegenbuchung angelegt" None { Label = "Rückgängig"; OnClick = ignore }
                ]
            ]
        ]
        subTitle "ReviewCard.memoBlock"
        note "Das Memo auf der Karte nur zum Lesen (ledger-wg42w): „Memo“ in 12 px gedämpft, darunter der Text über die volle Breite, Linie unten, kein Pfeil, kein Knopf; ohne Memo fehlt der Block. Der Text steht ganz da, solange die Karte nicht überläuft; sonst endet er mit „…“ in der letzten Zeile, die noch passt (mindestens eine), und trägt den ganzen Text als title. Bearbeiten geht nur in „Details“. Unten: kurz, und lang in einer niedrigen Karte (gekürzt)."
        Html.div [
            prop.className "max-w-md flex flex-col gap-3"
            prop.children [
                ReviewCard.card false [] [
                    ReviewCard.payee "Stadtwerke" false "" None
                    ReviewCard.memoBlock "demo-review-memo-short" "Abschlag November"
                ]
                Html.div [
                    prop.custom ("data-testid", "demo-review-memo-low")
                    prop.className "flex h-72 flex-col"
                    prop.children [
                        ReviewCard.cardFill false [] [
                            ReviewCard.meta "Heute · Girokonto · Comdirect-Import"
                            ReviewCard.payee "Amazon" false "" None
                            ReviewCard.amount "-21,50 €"
                            ReviewCard.memoBlock
                                "demo-review-memo-long"
                                "Wocheneinkauf fürs Grillfest am Samstag, dazu Getränke, Kohle, Servietten und die Torte für den Geburtstag von Oma am Sonntagnachmittag, außerdem Pappteller, Grillanzünder und zwei Kisten Wasser für die Nachbarn"
                            ReviewCard.detailsLink ignore
                        ]
                    ]
                ]
            ]
        ]
        subTitle "ReviewCard.foot"
        note "Die Zeile unter dem Hauptknopf: 54 px samt Abstand, einzeilig, mittig, 12 px. Hinweis in text-secondary, „Erst Kategorie wählen“ in attention halbfett, unter der Duplikat-Karte leer. Die Höhe ist immer dieselbe, die Knöpfe springen nie."
        subTitle "ReviewCard.SwipeStage (Wischen mit Fläche)"
        note "Am Handy: die Karte folgt dem Finger 1:1, darunter die Fläche über die ganze Karte. Rechts Haken über „Passt“ (grün getönt), links Uhr über „Später“ (neutral). Eingerastet ab der Grenze von reviewSwipeOf (90 px): Fläche kräftiger, Kreis gefüllt, Spalte etwas größer, kurzer Vibrationsimpuls. Ohne Kategorie nach rechts nur ein gelbes „!“, die Karte geht ab 72 px zäh mit und rastet nie ein. Unter der Grenze federt sie zurück, darüber fliegt sie hinaus; die nächste Karte blendet sich ein. Ruhige Beispiele der Zustände:"
        let demoCard =
            ReviewCard.cardFill false [] [
                ReviewCard.meta "Heute · Girokonto · Comdirect-Import"
                ReviewCard.payee "Amazon" false "" None
                ReviewCard.amount "-21,50 €"
            ]
        Html.div [
            prop.className "max-w-md flex flex-col gap-3"
            prop.children [
                ReviewCard.caption "demo-swipe-rest" "Ruhe"
                ReviewCard.swipeExample ReviewCard.SurfaceRest 0.0 demoCard
                ReviewCard.caption "demo-swipe-right" "Rechts unter der Grenze"
                ReviewCard.swipeExample (ReviewCard.SurfaceConfirm false) 60.0 demoCard
                ReviewCard.caption "demo-swipe-right-armed" "Rechts eingerastet"
                ReviewCard.swipeExample (ReviewCard.SurfaceConfirm true) 110.0 demoCard
                ReviewCard.caption "demo-swipe-left-armed" "Links eingerastet"
                ReviewCard.swipeExample (ReviewCard.SurfaceSkip true) -110.0 demoCard
                ReviewCard.caption "demo-swipe-no-target" "Ohne Kategorie"
                ReviewCard.swipeExample ReviewCard.SurfaceNoTarget 76.0 demoCard
            ]
        ]
        subTitle "ReviewCard.card (Duplikat, gelber Rand)"
        Html.div [
            prop.className "max-w-md"
            prop.children [
                ReviewCard.card true [] [
                    ReviewCard.metaAttention "Möglicherweise doppelt · noch nicht in YNAB"
                    ReviewCard.payee "Markt" false "" None
                    ReviewCard.amount "-42,80 €"
                    ReviewCard.metaSecondary "Heute · Girokonto · Comdirect-Import"
                    ReviewCard.sectionHead "Schon im Kassenbuch"
                    ReviewCard.matchRow "Markt" "Heute · Girokonto · Lebensmittel" "-42,80 €"
                    ReviewCard.caption "demo-review-reason" "Gleicher Betrag, gleicher Tag, gleicher Payee, aber eine andere Bank-Referenz."
                ]
            ]
        ]
        subTitle "Button.Choice"
        note "Zwei gleich breite Entscheidungen nebeneinander auf einer Karte: 16 px am Handy, 14 px am PC, wenig Innenabstand (px-3). Unter 640 px bricht ein langer Text auf zwei Zeilen um, darüber kein Umbruch. Die Höhe setzt der Aufrufer (52 px, der Hauptknopf „Passt, weiter“ 54 px)."
        Html.div [
            prop.className "max-w-md grid grid-cols-2 gap-2"
            prop.children [
                Button.view
                    { Button.defaultProps with
                        Text = "Ist neu"
                        Variant = Button.Secondary
                        Size = Button.Choice
                        ClassName = Some "min-h-[52px]"
                        FullWidth = true }
                Button.view
                    { Button.defaultProps with
                        Text = "Ist doppelt, verwerfen"
                        Variant = Button.Primary
                        Size = Button.Choice
                        ClassName = Some "min-h-[52px]"
                        FullWidth = true
                        Glow = false }
            ]
        ]
        subTitle "ReviewCard.keyBar"
        Html.div [
            prop.className "max-w-xl"
            prop.children [ ReviewCard.keyBar [ "Enter", "passt"; "S", "später"; "K", "Kategorie"; "L", "Lena"; "Esc", "zurück" ] ]
        ]
    ]

// ============================================
// Seite
// ============================================

/// `back`: „Zurück“ zu den Einstellungen, unter denen der Styleguide liegt (design-system-k6kfw).
let view (back: ReactElement) =
    Html.div [
        prop.className "space-y-2 max-w-4xl mx-auto pb-16"
        prop.children [
            PageHeader.top
                (Some back)
                "Styleguide"
                (Some "Lebende Galerie der echten Design-System-Komponenten & Tokens. Geschriebener Begleiter: standards/frontend/styleguide.md.")
                []

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
                        FeedbackDemo()
                        BottomSheetDemo()
                        CategoryPickerDemo()
                        CategoryPickerSingleDemo()
                        PayeePickerDemo()
                        SplitEditorDemo()
                        SwipeDemo()
                        navigationSection
                        voiceSection
                        chipBarSection
                        TabsDemo ()
                        SegmentedDemo ()
                        FilterPopoverDemo()
                        FilterPopoverMenuDemo()
                        DetailDemo()
                        multiSelectSection
                        CategoryMultiSelectDemo()
                        reviewCardSection
                    ]
                    |> List.mapi (fun i sectionEl -> React.keyedFragment (i, [ sectionEl ]))
                )
            ]
        ]
    ]
