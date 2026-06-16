module Components.Rules.View

open Feliz
open Components.Rules.Types
open Types
open Shared.Domain
open Client.DesignSystem
open Client.DesignSystem.Icons

// ============================================
// Helper Functions
// ============================================

let private patternTypeText (patternType: PatternType) =
    match patternType with
    | PatternType.Regex -> "Regex"
    | Contains -> "Contains"
    | Exact -> "Exact"

let private patternTypeBadge (patternType: PatternType) =
    match patternType with
    | PatternType.Regex ->
        Badge.view {
            Badge.defaultProps with
                Text = "Regex"
                Variant = Badge.Purple
                Style = Badge.Soft
                Size = Badge.Small
                Icon = Some (Html.span [ prop.className "font-mono text-[10px]"; prop.text ".*" ])
        }
    | Contains ->
        Badge.view {
            Badge.defaultProps with
                Text = "Contains"
                Variant = Badge.Info
                Style = Badge.Soft
                Size = Badge.Small
                Icon = Some (Html.span [ prop.className "font-mono text-[10px]"; prop.text "~" ])
        }
    | Exact ->
        Badge.view {
            Badge.defaultProps with
                Text = "Exact"
                Variant = Badge.Success
                Style = Badge.Soft
                Size = Badge.Small
                Icon = Some (Html.span [ prop.className "font-mono text-[10px]"; prop.text "=" ])
        }

let private targetFieldText (targetField: TargetField) =
    match targetField with
    | Payee -> "Payee"
    | Memo -> "Memo"
    | Combined -> "Combined"

// ============================================
// Pattern Type Icon (compact, single character)
// ============================================

let private patternTypeIcon (patternType: PatternType) =
    let (icon, color, title) =
        match patternType with
        | PatternType.Regex -> (".*", "text-neon-purple", "Regex pattern")
        | Contains -> ("~", "text-neon-teal", "Contains substring")
        | Exact -> ("=", "text-neon-green", "Exact match")
    Html.span [
        prop.className $"font-mono text-[10px] font-bold {color} bg-surface-elevated/50 px-1 rounded"
        prop.title title
        prop.text icon
    ]

// ============================================
// Single-Line Rule Row (Compact Display)
// ============================================

let private ruleRow (model: Model) (index: int) (total: int) (rule: Rule) (dispatch: Msg -> unit) =
    let opacityClass = if not rule.Enabled then "opacity-50" else ""
    let isConfirmingDelete = model.ConfirmingDeleteRuleId = Some rule.Id
    let isFirst = index = 0
    let isLast = index = total - 1

    Html.div [
        prop.className $"rule-row group flex items-center gap-2 sm:gap-3 px-3 py-2.5 bg-surface-card border border-border-subtle rounded-lg hover:border-border-default transition-colors {opacityClass}"
        prop.children [
            // Precedence controls: ▲/▼ to change priority + visible rank index.
            // Top of the list = highest priority = the first matching rule wins.
            Html.div [
                prop.className "flex-shrink-0 flex flex-col items-center -my-0.5"
                prop.children [
                    Button.view {
                        Button.defaultProps with
                            OnClick = fun () -> dispatch (MoveRule (rule.Id, Up))
                            Variant = Button.Ghost
                            Size = Button.Small
                            Icon = Some (Icons.chevronUp XS Icons.Default)
                            IsDisabled = isFirst
                            ClassName = Some "!px-1.5 !py-1"
                            Title = Some "Priorität erhöhen"
                    }
                    Button.view {
                        Button.defaultProps with
                            OnClick = fun () -> dispatch (MoveRule (rule.Id, Down))
                            Variant = Button.Ghost
                            Size = Button.Small
                            Icon = Some (Icons.chevronDown XS Icons.Default)
                            IsDisabled = isLast
                            ClassName = Some "!px-1.5 !py-1"
                            Title = Some "Priorität senken"
                    }
                ]
            ]

            // Rank index (precedence position; #1 = wins first)
            Html.span [
                prop.className "flex-shrink-0 w-6 text-center font-mono text-xs text-text-muted"
                prop.title "Reihenfolge: #1 gewinnt zuerst"
                prop.text $"#{index + 1}"
            ]

            // Toggle (compact)
            Html.div [
                prop.className "flex-shrink-0"
                prop.children [
                    Input.toggle rule.Enabled (fun _ -> dispatch (ToggleRuleEnabled rule.Id)) None
                ]
            ]

            // Pattern hint (desktop only)
            Html.span [
                prop.className "hidden sm:block font-mono text-[11px] text-text-muted bg-surface-elevated/50 px-1.5 py-0.5 rounded truncate max-w-[180px]"
                prop.title rule.Pattern
                prop.text rule.Pattern
            ]

            // Name (truncated)
            Html.span [
                prop.className "font-medium text-sm text-text-primary truncate min-w-[60px] max-w-[120px] sm:max-w-[160px]"
                prop.title $"{rule.Name}\nPattern: {rule.Pattern}"
                prop.text rule.Name
            ]

            // Arrow separator
            Html.span [
                prop.className "text-text-muted text-xs flex-shrink-0"
                prop.text "→"
            ]

            // Category (flexible, truncated)
            Html.span [
                prop.className "flex-1 text-sm text-text-secondary truncate min-w-0"
                prop.title rule.CategoryName
                prop.text rule.CategoryName
            ]

            // Actions (always visible on mobile, hover on desktop - except confirm button)
            Html.div [
                prop.className "flex-shrink-0 flex gap-0.5"
                prop.children [
                    // Edit button - hidden until hover on desktop
                    Html.div [
                        prop.className "sm:opacity-0 sm:group-hover:opacity-100 transition-opacity"
                        prop.children [
                            Button.iconButton (Icons.edit SM Icons.Default) Button.Ghost (fun () -> dispatch (EditRule rule.Id))
                        ]
                    ]

                    // Delete button with inline confirmation
                    if isConfirmingDelete then
                        // Red confirm button - ALWAYS visible (no opacity transition)
                        Html.button [
                            prop.className "flex items-center gap-1 px-2 py-1 text-xs font-medium text-neon-red bg-neon-red/10 border border-neon-red/30 rounded-lg hover:bg-neon-red/20 animate-pulse transition-colors"
                            prop.onClick (fun _ -> dispatch (DeleteRule rule.Id))
                            prop.children [
                                Icons.trash XS Icons.IconColor.Primary
                                Html.span [ prop.text "Löschen" ]
                            ]
                        ]
                    else
                        // Normal trash icon - hidden until hover on desktop
                        Html.div [
                            prop.className "sm:opacity-0 sm:group-hover:opacity-100 transition-opacity"
                            prop.children [
                                Button.iconButton (Icons.trash SM Icons.Error) Button.Ghost (fun () -> dispatch (ConfirmDeleteRule rule.Id))
                            ]
                        ]
                ]
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
                                    (patternTypeText model.Form.PatternType)
                                    (fun value ->
                                        let patternType =
                                            match value with
                                            | "Regex" -> PatternType.Regex
                                            | "Exact" -> Exact
                                            | _ -> Contains
                                        dispatch (UpdateRuleFormPatternType patternType))
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

        Modal.footer [
            Button.ghost "Abbrechen" (fun () -> dispatch CloseRuleModal)
            Form.submitButton
                (if isNew then "Regel erstellen" else "Speichern")
                (fun () -> dispatch SaveRule)
                model.Form.IsSaving
                [
                    ("Rule Name", model.Form.Name)
                    ("Pattern", model.Form.Pattern)
                    ("Category", model.Form.CategoryId |> Option.map (fun _ -> "selected") |> Option.defaultValue "")
                ]
        ]
    ]

// ============================================
// Main View
// ============================================


// ============================================
// Page Header Actions
// ============================================

let private rulesHeaderActions (dispatch: Msg -> unit) = [
    // Refresh button
    Button.view {
        Button.defaultProps with
            Text = ""
            OnClick = fun () -> dispatch LoadRules
            Variant = Button.Ghost
            Icon = Some (Icons.sync SM Icons.Default)
            Title = Some "Refresh rules"
    }

    Button.primaryWithIcon
        "Neue Regel"
        (Icons.plus SM Icons.IconColor.Primary)
        (fun () -> dispatch OpenNewRuleModal)

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
    Html.div [
        prop.className "space-y-6 animate-fade-in"
        prop.children [
            // Header
            PageHeader.gradientWithActions
                "Kategorisierungsregeln"
                (Some "Automatische Kategorisierung deiner Transaktionen")
                (rulesHeaderActions dispatch)

            // Rules content
            match model.Rules with
            | RemoteData.NotAsked ->
                Loading.centered "Regeln werden geladen..."

            | RemoteData.Loading ->
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        for i in 1..3 do
                            Html.div [
                                prop.key (string i)
                                prop.children [ Loading.cardSkeleton ]
                            ]
                    ]
                ]

            | RemoteData.Success rules when rules.IsEmpty ->
                emptyState dispatch

            | RemoteData.Success rules ->
                let total = List.length rules
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        // Precedence hint: makes the "first match wins" semantic visible.
                        Html.div [
                            prop.className "flex items-center gap-2 px-1 text-xs text-text-muted"
                            prop.children [
                                Icons.info XS Icons.Default
                                Html.span [
                                    prop.text "Regeln werden von oben nach unten geprüft — die erste passende, aktive Regel gewinnt. Mit ▲/▼ die Reihenfolge ändern."
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "space-y-1.5"
                            prop.children [
                                for index, rule in List.indexed rules do
                                    let (RuleId id) = rule.Id
                                    Html.div [
                                        prop.key (string id)
                                        prop.children [ ruleRow model index total rule dispatch ]
                                    ]
                            ]
                        ]
                    ]
                ]

            | RemoteData.Failure error ->
                ErrorDisplay.cardWithTitle "Fehler beim Laden der Regeln" error (Some (fun () -> dispatch LoadRules))

            // Edit/Create modal
            if model.IsNewRule || model.EditingRule.IsSome then
                ruleEditModal model dispatch
        ]
    ]
