module Components.Rules.View

open Feliz
open Components.Rules.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.Icons

// ============================================
// Helper Functions (rein, testbar)
// ============================================

/// Technischer Schlüssel des Mustertyps. Er ist der Wert im Select des Dialogs und
/// bleibt unverändert, sonst bricht das Formular still.
let patternTypeKey (patternType: PatternType) =
    match patternType with
    | PatternType.Regex -> "Regex"
    | Contains -> "Contains"
    | Exact -> "Exact"

/// Rückweg vom Select-Wert zum Mustertyp (unbekannte Werte fallen auf "Enthält")
let patternTypeOfKey (key: string) =
    match key with
    | "Regex" -> PatternType.Regex
    | "Exact" -> Exact
    | _ -> Contains

/// Anzeigetext des Mustertyps in der Liste
let patternTypeLabel (patternType: PatternType) =
    match patternType with
    | PatternType.Regex -> "Regex"
    | Contains -> "Enthält"
    | Exact -> "Exakt"

let private targetFieldText (targetField: TargetField) =
    match targetField with
    | Payee -> "Payee"
    | Memo -> "Memo"
    | Combined -> "Combined"

/// Statuswort der Zeile: aktive Regeln tragen kein Zeichen (ADR 0049, Farbe ist ein Signal)
let statusLabel (enabled: bool) = if enabled then "" else "inaktiv"

/// Untertitel der Seite, etwa "12 Regeln, 11 aktiv"; ohne Regeln leer
let summaryText (rules: Rule list) =
    match List.length rules with
    | 0 -> ""
    | total ->
        let active = rules |> List.filter (fun r -> r.Enabled) |> List.length
        let noun = if total = 1 then "Regel" else "Regeln"
        $"{total} {noun}, {active} aktiv"

/// Klassen des Namens: aktiv hell, inaktiv gedämpft (ohne `opacity` auf der ganzen Zeile)
let rowClass (enabled: bool) =
    if enabled then Tokens.ListRoles.primary
    else $"{Tokens.ListRoles.primarySize} font-medium text-text-secondary"

/// Pflichtfelder des Dialogs als (Label, Wert); die Labels entsprechen denen im Dialog
let requiredFieldPairs (form: RuleFormState) : (string * string) list =
    [
        ("Regelname", form.Name)
        ("Muster", form.Pattern)
        ("Kategorie", form.CategoryId |> Option.map (fun _ -> "selected") |> Option.defaultValue "")
    ]

/// Namen der noch fehlenden Pflichtfelder
let requiredFields (form: RuleFormState) : string list =
    Form.getMissingFields (requiredFieldPairs form)

/// Prioritätshinweis über der Liste
let priorityHint =
    "Die Regeln werden von oben nach unten geprüft. Die erste passende, aktive Regel gewinnt."

// ============================================
// Rule List (ruhige Liste, ADR 0049)
// ============================================

/// Klick und Enter auf einer Zeile öffnen den Dialog "Regel bearbeiten"
let private openOnActivate (dispatch: Msg -> unit) (rule: Rule) =
    [
        prop.tabIndex 0
        prop.onClick (fun _ -> dispatch (EditRule rule.Id))
        prop.onKeyDown (fun e ->
            if e.key = "Enter" then
                e.preventDefault ()
                dispatch (EditRule rule.Id))
    ]

/// Kleiner Pfeilknopf ▲/▼; `stopPropagation`, damit die Zeile nicht den Dialog öffnet
let private moveButton (icon: ReactElement) (title: string) (isDisabled: bool) (onClick: unit -> unit) =
    Html.button [
        prop.type'.button
        prop.className "flex items-center justify-center w-6 h-5 rounded text-text-muted hover:text-text-primary hover:bg-surface-elevated disabled:opacity-30 disabled:pointer-events-none transition-colors touch-manipulation"
        prop.title title
        prop.disabled isDisabled
        prop.onClick (fun e ->
            e.stopPropagation ()
            onClick ())
        prop.children [ icon ]
    ]

let private moveButtons (index: int) (total: int) (rule: Rule) (dispatch: Msg -> unit) =
    [
        moveButton
            (Icons.chevronUp XS Icons.Default)
            "Priorität erhöhen"
            (index = 0)
            (fun () -> dispatch (MoveRule (rule.Id, Up)))
        moveButton
            (Icons.chevronDown XS Icons.Default)
            "Priorität senken"
            (index = total - 1)
            (fun () -> dispatch (MoveRule (rule.Id, Down)))
    ]

let private ruleTableRow (index: int) (total: int) (rule: Rule) (dispatch: Msg -> unit) =
    Html.tr [
        prop.key (string (let (RuleId id) = rule.Id in id))
        prop.className (Table.calmRowClass false + " cursor-pointer")
        yield! openOnActivate dispatch rule
        prop.children [
            Table.cell Table.defaultCellProps [
                Html.div [
                    prop.className "flex items-center gap-1"
                    prop.children [
                        yield! moveButtons index total rule dispatch
                        Html.span [
                            prop.className $"ml-1 {Tokens.ListRoles.date}"
                            prop.title "Reihenfolge: #1 gewinnt zuerst"
                            prop.text $"#{index + 1}"
                        ]
                    ]
                ]
            ]
            Table.roleCell Table.Muted [
                Html.div [
                    prop.className "flex items-baseline gap-2 min-w-0"
                    prop.children [
                        Html.span [
                            prop.className "font-mono truncate"
                            prop.title rule.Pattern
                            prop.text rule.Pattern
                        ]
                        Html.span [
                            prop.className "shrink-0"
                            prop.text (patternTypeLabel rule.PatternType)
                        ]
                    ]
                ]
            ]
            Table.cell { Table.defaultCellProps with IsTruncate = true } [
                Html.span [
                    prop.className (rowClass rule.Enabled)
                    prop.title $"{rule.Name}\nMuster: {rule.Pattern}"
                    prop.text rule.Name
                ]
            ]
            Table.roleCell Table.Secondary [
                Html.span [
                    prop.className "break-words"
                    prop.text rule.CategoryName
                ]
            ]
            Table.roleCell Table.Muted [
                Html.div [
                    prop.className "text-right"
                    prop.text (statusLabel rule.Enabled)
                ]
            ]
        ]
    ]

let private rulesTable (rules: Rule list) (dispatch: Msg -> unit) =
    let total = List.length rules
    Primitives.Responsive.desktopOnly [
        Table.view
            { Table.defaultTableProps with
                Variant = Table.Hover
                FixedLayout = true
                Responsive = false
                ClassName = Some "rounded-xl border border-border-subtle bg-surface-card" }
            [
                Html.colgroup [
                    Html.col [ prop.style [ style.width (length.px 112) ] ]
                    Html.col [ prop.style [ style.width (length.percent 28) ] ]
                    Html.col [ prop.style [ style.width (length.percent 24) ] ]
                    Html.col []
                    Html.col [ prop.style [ style.width (length.px 88) ] ]
                ]
                Table.thead [
                    Table.calmHeaderCell "Reihenfolge" None
                    Table.calmHeaderCell "Muster" None
                    Table.calmHeaderCell "Name" None
                    Table.calmHeaderCell "Kategorie" None
                    Table.calmHeaderCell "Status" (Some "right")
                ]
                Table.tbody [
                    for index, rule in List.indexed rules do
                        ruleTableRow index total rule dispatch
                ]
            ]
    ]

/// Handy: zweizeilig. Zeile 1 Name und Kategorie (die Kategorie bekommt die Restbreite und
/// bricht um, statt zu kürzen), Zeile 2 Muster, Typ und Status
let private ruleMobileRow (index: int) (total: int) (rule: Rule) (dispatch: Msg -> unit) =
    let status = statusLabel rule.Enabled
    Html.div [
        prop.key (string (let (RuleId id) = rule.Id in id))
        prop.className $"flex items-start gap-2 {Tokens.ListRoles.mobileRowPadding} border-b border-border-subtle cursor-pointer touch-manipulation"
        yield! openOnActivate dispatch rule
        prop.children [
            Html.div [
                prop.className "flex flex-col shrink-0"
                prop.children (moveButtons index total rule dispatch)
            ]
            Html.div [
                prop.className "flex-1 min-w-0"
                prop.children [
                    Html.div [
                        prop.className "flex items-start justify-between gap-3 min-h-5"
                        prop.children [
                            Html.span [
                                prop.className ("shrink-0 max-w-[60%] break-words " + rowClass rule.Enabled)
                                prop.text rule.Name
                            ]
                            Html.span [
                                prop.className $"flex-1 min-w-0 text-right break-words {Tokens.ListRoles.secondary}"
                                prop.text rule.CategoryName
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className $"flex items-center gap-2 h-5 mt-0.5 min-w-0 {Tokens.ListRoles.muted}"
                        prop.children [
                            Html.span [
                                prop.className "font-mono truncate"
                                prop.text rule.Pattern
                            ]
                            Html.span [
                                prop.className "shrink-0"
                                prop.text (patternTypeLabel rule.PatternType)
                            ]
                            if status <> "" then
                                Html.span [
                                    prop.className "shrink-0 ml-auto"
                                    prop.text status
                                ]
                        ]
                    ]
                ]
            ]
        ]
    ]

let private rulesMobileList (rules: Rule list) (dispatch: Msg -> unit) =
    let total = List.length rules
    Primitives.Responsive.mobileOnly [
        Html.div [
            prop.className "border-t border-border-subtle"
            prop.children [
                for index, rule in List.indexed rules do
                    ruleMobileRow index total rule dispatch
            ]
        ]
    ]

// ============================================
// Empty State Component
// ============================================

let private emptyState (dispatch: Msg -> unit) =
    Card.emptyState
        (Icons.rules XL Icons.Default)
        "Keine Regeln"
        "Erstelle deine erste Regel, um Transaktionen beim Sync automatisch zu kategorisieren."
        (Some (
            Button.primaryWithIcon
                "Erstelle deine erste Regel"
                (Icons.plus SM Icons.IconColor.Primary)
                (fun () -> dispatch OpenNewRuleModal)
        ))

// ============================================
// Rule Edit Modal
// ============================================

let private ruleEditModal (model: Model) (dispatch: Msg -> unit) =
    let isNew = model.IsNewRule
    let title = if isNew then "Neue Regel erstellen" else "Regel bearbeiten"
    let subtitle = if isNew then "Automatische Kategorisierung einrichten" else "Regeleinstellungen anpassen"

    Modal.view {
        Modal.defaultProps with
            IsOpen = true
            OnClose = fun () -> dispatch CloseRuleModal
            Size = Modal.Large
            Title = Some title
            Subtitle = Some subtitle
    } [
        Modal.body [
            Html.div [
                prop.className "space-y-5"
                prop.children [
                    // Name input
                    Input.groupRequired "Regelname" (
                        Input.textSimple
                            model.Form.Name
                            (UpdateRuleFormName >> dispatch)
                            "z.B. Amazon Einkäufe"
                    )

                    // Pattern type and target field row
                    Html.div [
                        prop.className "grid grid-cols-1 sm:grid-cols-2 gap-4"
                        prop.children [
                            // Pattern type
                            Input.groupSimple "Muster-Typ" (
                                Input.selectSimple
                                    (patternTypeKey model.Form.PatternType)
                                    (fun value -> dispatch (UpdateRuleFormPatternType (patternTypeOfKey value)))
                                    [
                                        ("Contains", "Enthält - Teiltext")
                                        ("Exact", "Exakt - Volltext")
                                        ("Regex", "Regex - Regulärer Ausdruck")
                                    ]
                            )

                            // Target field
                            Input.groupSimple "Feld" (
                                Input.selectSimple
                                    (targetFieldText model.Form.TargetField)
                                    (fun value ->
                                        let targetField =
                                            match value with
                                            | "Payee" -> Payee
                                            | "Memo" -> Memo
                                            | _ -> Combined
                                        dispatch (UpdateRuleFormTargetField targetField))
                                    [
                                        ("Combined", "Kombiniert - Empfänger & Memo")
                                        ("Payee", "Nur Empfänger")
                                        ("Memo", "Nur Memo")
                                    ]
                            )
                        ]
                    ]

                    // Pattern input
                    Input.group {
                        Label = "Muster"
                        Required = true
                        Error = None
                        HelpText = Some (
                            match model.Form.PatternType with
                            | PatternType.Regex -> "Reguläre Ausdrücke für komplexe Muster"
                            | Contains -> "Trifft zu wenn der Text diesen Teiltext enthält (Groß-/Kleinschreibung egal)"
                            | Exact -> "Trifft nur zu wenn der gesamte Text übereinstimmt"
                        )
                        Children =
                            Input.text {
                                Input.textInputDefaults with
                                    Value = model.Form.Pattern
                                    OnChange = (UpdateRuleFormPattern >> dispatch)
                                    Placeholder =
                                        match model.Form.PatternType with
                                        | PatternType.Regex -> "z.B. AMAZON\\.DE"
                                        | Contains -> "z.B. amazon"
                                        | Exact -> "z.B. AMAZON MARKETPLACE"
                                    ClassName = Some "font-mono"
                            }
                    }

                    // Category dropdown
                    Input.group {
                        Label = "Kategorie"
                        Required = true
                        Error = None
                        HelpText = None
                        Children =
                            Html.div [
                                prop.children [
                                    Input.searchableSelect
                                        (match model.Form.CategoryId with
                                         | Some (YnabCategoryId id) -> id.ToString()
                                         | None -> "")
                                        (fun value ->
                                            if System.String.IsNullOrWhiteSpace(value) then
                                                dispatch (UpdateRuleFormCategoryId None)
                                            else
                                                dispatch (UpdateRuleFormCategoryId (Some (YnabCategoryId (System.Guid.Parse value)))))
                                        "Kategorie wählen..."
                                        (model.Categories
                                         |> List.map (fun category ->
                                             let (YnabCategoryId id) = category.Id
                                             (id.ToString(), $"{category.GroupName}: {category.Name}")))

                                    if model.Categories.IsEmpty then
                                        Html.div [
                                            prop.className "flex items-center gap-2 mt-2 px-3 py-2 bg-neon-orange/10 text-neon-orange rounded-lg text-sm"
                                            prop.children [
                                                Icons.warning SM Icons.NeonOrange
                                                Html.span [ prop.text "Keine Kategorien geladen. Bitte zuerst YNAB konfigurieren." ]
                                            ]
                                        ]
                                ]
                            ]
                    }

                    // Payee override (optional)
                    Input.group {
                        Label = "Payee-Override"
                        Required = false
                        Error = None
                        HelpText = Some "Leer lassen für Original-Empfänger"
                        Children =
                            Html.div [
                                prop.className "flex items-center gap-2"
                                prop.children [
                                    Html.div [
                                        prop.className "flex-1"
                                        prop.children [
                                            Input.textSimple
                                                model.Form.PayeeOverride
                                                (UpdateRuleFormPayeeOverride >> dispatch)
                                                "Override payee name in YNAB"
                                        ]
                                    ]
                                    Badge.view {
                                        Badge.defaultProps with
                                            Text = "Optional"
                                            Variant = Badge.Neutral
                                            Size = Badge.Small
                                    }
                                ]
                            ]
                    }

                    // Enabled toggle (only for editing)
                    if not isNew then
                        Html.div [
                            prop.className "flex items-center gap-3 p-3 bg-surface-elevated/50 rounded-lg"
                            prop.children [
                                Input.toggle model.Form.Enabled (fun checked' -> dispatch (UpdateRuleFormEnabled checked')) (Some "Regel aktiv")
                            ]
                        ]

                    // Test pattern section
                    Html.div [
                        prop.className "pt-4 border-t border-border-default"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2 mb-4"
                                prop.children [
                                    Icons.search SM Icons.NeonTeal
                                    Html.span [ prop.className "font-medium text-text-primary"; prop.text "Muster testen" ]
                                ]
                            ]
                            Html.div [
                                prop.className "space-y-3"
                                prop.children [
                                    Html.div [
                                        prop.className "flex flex-col sm:flex-row gap-2"
                                        prop.children [
                                            Html.div [
                                                prop.className "flex-1"
                                                prop.children [
                                                    Input.textSimple
                                                        model.Form.TestInput
                                                        (UpdateRuleFormTestInput >> dispatch)
                                                        "Beispieltext eingeben..."
                                                ]
                                            ]
                                            Button.secondary
                                                "Test"
                                                (fun () -> dispatch TestRulePattern)
                                        ]
                                    ]
                                    match model.Form.TestResult with
                                    | Some result ->
                                        let (bgClass, iconEl) =
                                            if result.StartsWith("✅") then
                                                ("bg-neon-green/10 border-neon-green/30 text-neon-green", Icons.checkCircle SM Icons.NeonGreen)
                                            elif result.StartsWith("❌") then
                                                ("bg-neon-red/10 border-neon-red/30 text-neon-red", Icons.xCircle SM Icons.NeonRed)
                                            else
                                                ("bg-neon-orange/10 border-neon-orange/30 text-neon-orange", Icons.warning SM Icons.NeonOrange)
                                        Html.div [
                                            prop.className $"flex items-center gap-2 p-3 rounded-lg border {bgClass}"
                                            prop.children [
                                                iconEl
                                                Html.span [ prop.className "text-sm"; prop.text (result.TrimStart([|'✅'; '❌'; '⚠'; ' '|])) ]
                                            ]
                                        ]
                                    | None -> Html.none
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]

        let rightButtons = [
            Button.ghost "Abbrechen" (fun () -> dispatch CloseRuleModal)
            Form.submitButton
                (if isNew then "Regel erstellen" else "Speichern")
                (fun () -> dispatch SaveRule)
                model.Form.IsSaving
                (requiredFieldPairs model.Form)
        ]

        match model.EditingRule with
        | Some rule when not isNew ->
            // Löschen gibt es nur hier, mit der zweistufigen Bestätigung (3 Sekunden)
            let deleteButton =
                if model.ConfirmingDeleteRuleId = Some rule.Id then
                    Button.danger "Wirklich löschen" (fun () -> dispatch (DeleteRule rule.Id))
                else
                    Button.ghost "Regel löschen" (fun () -> dispatch (ConfirmDeleteRule rule.Id))
            Modal.footerWithLeft deleteButton rightButtons
        | _ -> Modal.footer rightButtons
    ]

// ============================================
// Main View
// ============================================


// ============================================
// Page Header Actions
// ============================================

let private rulesHeaderActions (dispatch: Msg -> unit) = [
    // Neu laden: am PC als sekundärer Knopf; am Handy im Menü ⋮ (die Kopfzeile ist dort zu schmal)
    Button.view {
        Button.defaultProps with
            Text = "Neu laden"
            OnClick = fun () -> dispatch LoadRules
            Variant = Button.Secondary
            Title = Some "Regeln neu laden"
            ClassName = Some "hidden md:inline-flex"
    }

    // Die eine primäre Aktion: am Handy nur das Symbol (die Kopfzeile ist sonst zu breit)
    Button.view {
        Button.defaultProps with
            Text = ""
            OnClick = fun () -> dispatch OpenNewRuleModal
            Variant = Button.Primary
            Icon = Some (Icons.plus SM Icons.IconColor.Primary)
            Title = Some "Neue Regel"
            ClassName = Some "md:hidden"
    }
    Button.view {
        Button.defaultProps with
            Text = "Neue Regel"
            OnClick = fun () -> dispatch OpenNewRuleModal
            Variant = Button.Primary
            Icon = Some (Icons.plus SM Icons.IconColor.Primary)
            ClassName = Some "hidden md:inline-flex"
    }

    // Overflow menu
    Html.div [
        prop.className "relative group"
        prop.children [
            Html.button [
                prop.className "flex items-center justify-center w-8 h-8 rounded-lg text-text-secondary hover:text-text-primary hover:bg-surface-elevated transition-colors"
                prop.tabIndex 0
                prop.children [
                    Html.span [ prop.className "text-xl"; prop.text "⋮" ]
                ]
            ]
            Html.ul [
                prop.className "invisible group-focus-within:visible absolute right-0 top-full mt-1 z-50 p-2 shadow-lg bg-surface-card border border-border-default rounded-xl w-52"
                prop.tabIndex 0
                prop.children [
                    Html.li [
                        prop.className "md:hidden"
                        prop.children [
                            Html.a [
                                prop.className "flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-text-secondary hover:text-text-primary hover:bg-surface-elevated transition-colors cursor-pointer"
                                prop.onClick (fun _ -> dispatch LoadRules)
                                prop.children [
                                    Icons.sync SM Icons.Default
                                    Html.span [ prop.text "Regeln neu laden" ]
                                ]
                            ]
                        ]
                    ]
                    Html.li [
                        Html.a [
                            prop.className "flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-text-secondary hover:text-text-primary hover:bg-surface-elevated transition-colors cursor-pointer"
                            prop.onClick (fun _ -> dispatch ExportRules)
                            prop.children [
                                Icons.download SM Icons.Default
                                Html.span [ prop.text "Regeln exportieren" ]
                            ]
                        ]
                    ]
                    Html.li [
                        Html.label [
                            prop.className "flex items-center gap-2 px-3 py-2 rounded-lg text-sm text-text-secondary hover:text-text-primary hover:bg-surface-elevated transition-colors cursor-pointer"
                            prop.children [
                                Icons.upload SM Icons.Default
                                Html.span [ prop.text "Regeln importieren" ]
                                Html.input [
                                    prop.type'.file
                                    prop.accept ".json"
                                    prop.className "hidden"
                                    prop.onChange (fun (e: Browser.Types.Event) ->
                                        let input = e.target :?> Browser.Types.HTMLInputElement
                                        if input.files.length > 0 then
                                            let file = input.files.[0]
                                            let reader = Browser.Dom.FileReader.Create()
                                            reader.onload <- fun _ ->
                                                let content = reader.result :?> string
                                                dispatch (ImportRules content)
                                            reader.readAsText(file)
                                    )
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]
]

let view (model: Model) (dispatch: Msg -> unit) =
    // Ruhige Liste (ADR 0049 Punkt 9): schlanker Seitenkopf, die Liste füllt die Breite
    Primitives.Container.full [
        Html.div [
            prop.className "space-y-4 animate-fade-in"
            prop.children [
                PageHeader.calm
                    "Kategorisierungsregeln"
                    (match model.Rules with
                     | RemoteData.Success rules when not rules.IsEmpty -> Some (summaryText rules)
                     | _ -> None)
                    (rulesHeaderActions dispatch)

                match model.Rules with
                | RemoteData.NotAsked ->
                    Loading.centered "Regeln werden geladen..."

                | RemoteData.Loading ->
                    Loading.tableSkeleton 5 5

                | RemoteData.Success rules when rules.IsEmpty ->
                    emptyState dispatch

                | RemoteData.Success rules ->
                    Html.div [
                        prop.className "space-y-3"
                        prop.children [
                            // Prioritätshinweis: macht "die erste passende Regel gewinnt" sichtbar
                            Html.div [
                                prop.className "flex items-center gap-2 px-1 text-xs text-text-muted"
                                prop.children [
                                    Icons.info XS Icons.Default
                                    Html.span [ prop.text priorityHint ]
                                ]
                            ]
                            rulesTable rules dispatch
                            rulesMobileList rules dispatch
                        ]
                    ]

                | RemoteData.Failure error ->
                    ErrorDisplay.cardWithTitle "Fehler beim Laden der Regeln" error (Some (fun () -> dispatch LoadRules))

                // Dialog zum Anlegen und Bearbeiten
                if model.IsNewRule || model.EditingRule.IsSome then
                    ruleEditModal model dispatch
            ]
        ]
    ]
