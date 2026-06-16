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
                Html.p [ prop.className Tokens.FontSizes.body; prop.text "Body — text-[15px] md:text-base" ]
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

let private cardSection =
    section "4. Card" [
        note "Inhaltsblöcke. glow für featured, withAccent für Akzentlinie, emptyState sagt was als Nächstes zu tun ist. styleguide.md §5."
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

let private badgeSection =
    section "5. Badge" [
        note "Kompakter Status/Label. Semantisch + Domänen-Status. styleguide.md §5."
        row [
            Badge.success "Aktiv"
            Badge.warning "Review"
            Badge.error "Fehler"
            Badge.info "Neu"
        ]
        row [
            Badge.imported
            Badge.pendingReview
            Badge.autoCategorized
            Badge.uncategorized
            Badge.count 5
        ]
    ]

let private moneySection =
    section "6. Money" [
        note "Jede Geldanzeige. Vorzeichen-Farbe + Glow werden automatisch gesetzt (positiv=Green, negativ=Red) — nicht manuell überschreiben. styleguide.md §2/§5."
        Primitives.HStack.md [
            Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.simple 1234.56m "EUR"; caption "positiv (simple)" ] ]
            Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.simple -89.90m "EUR"; caption "negativ (simple)" ] ]
        ]
        Primitives.HStack.md [
            Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.large 2500.00m "EUR"; caption "large" ] ]
        ]
        Html.div [ prop.className "flex flex-col gap-1"; prop.children [ Money.hero 4280.42m "EUR"; caption "hero (Balance)" ] ]
    ]

let private statsSection =
    section "7. Stats" [
        note "KPI-/Kennzahl-Kacheln. Vollständige Tabellen → Table. styleguide.md §5."
        Stats.grid [
            Stats.withIcon "Transaktionen" "1.234" (Icons.banknotes Icons.MD Icons.NeonTeal)
            Stats.withTrend "Importe" "42" (Stats.Trend.Up 12.5m)
            Stats.syncCount 8
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

let private loadingSection =
    section "9. Loading" [
        note "Skeletons statt Spinner, wo die Zielform bekannt ist — kein Layout-Sprung. Generischer Spinner/neonPulse nur bei unbekannter Form. styleguide.md §6."
        row [
            Loading.spinner Loading.MD Loading.Teal
            Loading.spinner Loading.LG Loading.Green
            Loading.neonPulse Loading.Teal
        ]
        subTitle "Skeletons"
        Loading.tableSkeleton 3 3
    ]

let private errorSection =
    section "10. ErrorDisplay" [
        note "Fehlerdarstellung — direkt und ehrlich, benennt was passiert ist. Kurzes Erfolgs-Feedback → Toast. styleguide.md §5/§8."
        ErrorDisplay.inline' "Ungültige E-Mail-Adresse"
        ErrorDisplay.cardCompact "Einstellungen konnten nicht geladen werden" (Some ignore)
        ErrorDisplay.warning "Diese Aktion kann nicht rückgängig gemacht werden." None
    ]

let private iconsSection =
    section "11. Icons" [
        note "SVG-Icons mit Size×Color-Tokens. Eigene rohe <svg> inline gelten als Drift — fehlt ein Icon, in Icons.fs ergänzen. styleguide.md §5."
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

let private pageHeaderSection =
    section "12. PageHeader" [
        note "Seitentitel. Karten-Titel → Card.header; Abschnittstitel → Card.headerSimple. styleguide.md §5."
        PageHeader.withSubtitle "Einstellungen" "Verbindungen und Präferenzen konfigurieren."
        PageHeader.gradient "Kategorisierungs-Regeln"
    ]

let private primitivesSection =
    section "13. Primitives (Layout)" [
        note "Layout-Bausteine: Container/Stack/Grid/Spacer, responsive Sichtbarkeit. Visuell gestylte Blöcke → Card. styleguide.md §5."
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

    section "14. Input" [
        note "Alle Formularelemente. Mobiles Kategorie-/Konto-Auswählen → BottomSheet (keyboard-fest). styleguide.md §5/§7.2."
        Primitives.Grid.two [
            Input.groupSimple "Text" (Input.textSimple textValue setTextValue "REWE, Netflix …")
            Input.groupSimple "Select" (Input.selectWithPlaceholder selectValue setSelectValue "Kategorie wählen…" categoryOptions)
        ]
        Input.groupSimple "Searchable Select" (Input.searchableSelect searchValue setSearchValue "Kategorie suchen…" categoryOptions)
        Input.toggle toggleValue setToggleValue (Some "Auto-Kategorisierung aktiv")
        Input.checkbox checkValue setCheckValue "Bestätigt"
    ]

/// Form-Demo: submitButton-Gating bei fehlendem Pflichtfeld.
[<ReactComponent>]
let private FormDemo () =
    let nameValue, setNameValue = React.useState ""

    section "15. Form (Required-Field-Gating)" [
        note "Submit-Buttons deaktivieren bei fehlenden Pflichtfeldern. Tippe etwas, um den Button zu aktivieren. styleguide.md §5."
        Input.groupRequired "Name" (Input.textSimple nameValue setNameValue "Pflichtfeld…")
        Form.submitButton "Speichern" ignore false [ ("Name", nameValue) ]
    ]

/// Modal-Demo (Desktop-Dialog) per useState.
[<ReactComponent>]
let private ModalDemo () =
    let isOpen, setOpen = React.useState false

    section "16. Modal (Desktop-Dialog)" [
        note "Zentrierte Dialoge auf Desktop. Mobile Auswahl-Flows → BottomSheet (keyboard-fest). styleguide.md §5/§7.1."
        Button.secondary "Modal öffnen" (fun () -> setOpen true)
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

    section "17. Toast" [
        note "Flüchtiges Feedback nach Aktion, mit sanftem Abgang (Exit-Animation). Blockierende Fehler → ErrorDisplay/Modal. styleguide.md §5/§6."
        row [
            Button.secondary "Erfolg" (fire Toast.Success "Gespeichert.")
            Button.secondary "Fehler" (fire Toast.Error "Speichern fehlgeschlagen.")
            Button.secondary "Info" (fire Toast.Info "3 neue Transaktionen.")
        ]
        Toast.renderList toasts dismiss
    ]

/// BottomSheet-Demo (mobiles Picker-Muster, ADR 0005) per useState.
[<ReactComponent>]
let private BottomSheetDemo () =
    let isOpen, setOpen = React.useState false
    let chosen, setChosen = React.useState ""

    section "18. BottomSheet (mobiles Picker-Muster)" [
        note "Mobile Auswahl-/Picker-Flows (keyboard-aware, visual-viewport-Anker, Click-Commit). Baust du ein neues Sheet, erbe das Muster — erfinde Anker/Commit nicht neu. ADR 0005, styleguide.md §7.1."
        row [
            Button.secondary "Konto wählen" (fun () -> setOpen true)
            if chosen <> "" then Badge.info chosen
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

/// Swipe-Demo: eine wischbare Listenzeile (mobile).
[<ReactComponent>]
let private SwipeDemo () =
    let swiped, setSwiped = React.useState false

    section "19. Swipe (wischbare Listenzeile, mobile)" [
        note "Wischbare Listenzeilen mit enthüllter Aktion (mobile). Der Swipe ist ein Accelerator — Buttons bleiben der accessible Pfad. styleguide.md §5/§7.1."
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
        if swiped then Badge.success "Übersprungen (Demo)"
    ]

// ============================================
// 8. Voice / Muster-Hinweise
// ============================================

let private voiceSection =
    section "20. Voice & Muster" [
        note "Die UI spricht Deutsch, direkt, knapp, ohne Füllwörter. Beträge sprechen für sich (Vorzeichen + Farbe). \
              Empty States sagen, was als Nächstes zu tun ist. Ubiquitous Language der BCs (Import, Kategorie, Sync, Split, Transfer) \
              konsistent verwenden. Vollständige Regeln & ADR-Begründungen: standards/frontend/styleguide.md §7/§8 \
              (ADR 0004 Picker-statt-Freitext, ADR 0005 Visual-Viewport-Sheet + Click-Commit)."
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
                        SwipeDemo()
                        voiceSection
                    ]
                    |> List.mapi (fun i sectionEl -> React.keyedFragment (i, [ sectionEl ]))
                )
            ]
        ]
    ]
