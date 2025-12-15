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
        prop.className $"font-mono text-[10px] font-bold {color} bg-white/5 px-1 rounded"
        prop.title title
        prop.text icon
    ]

// ============================================
// Single-Line Rule Row (Compact Display)
// ============================================

let private ruleRow (model: Model) (rule: Rule) (dispatch: Msg -> unit) =
    let opacityClass = if not rule.Enabled then "opacity-50" else ""
    let isConfirmingDelete = model.ConfirmingDeleteRuleId = Some rule.Id

    Html.div [
        prop.className $"group flex items-center gap-2 sm:gap-3 px-3 py-2.5 bg-base-100 border border-white/5 rounded-lg hover:border-white/10 transition-colors {opacityClass}"
        prop.children [
            // Toggle (compact)
            Html.div [
                prop.className "flex-shrink-0"
                prop.children [
                    Input.toggle rule.Enabled (fun _ -> dispatch (ToggleRuleEnabled rule.Id)) None
                ]
            ]

            // Pattern type icon
            Html.div [
                prop.className "flex-shrink-0 hidden sm:block"
                prop.children [ patternTypeIcon rule.PatternType ]
            ]

            // Name (truncated)
            Html.span [
                prop.className "font-medium text-sm text-base-content truncate min-w-[60px] max-w-[120px] sm:max-w-[160px]"
                prop.title $"{rule.Name}\nPattern: {rule.Pattern}"
                prop.text rule.Name
            ]

            // Arrow separator
            Html.span [
                prop.className "text-base-content/30 text-xs flex-shrink-0"
                prop.text "→"
            ]

            // Category (flexible, truncated)
            Html.span [
                prop.className "flex-1 text-sm text-base-content/70 truncate min-w-0"
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
                            prop.className "btn btn-xs btn-error gap-1 animate-pulse"
                            prop.onClick (fun _ -> dispatch (DeleteRule rule.Id))
                            prop.children [
                                Icons.trash XS Icons.IconColor.Primary
                                Html.span [ prop.text "Delete" ]
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
        "No Rules Yet"
        "Create categorization rules to automatically categorize your transactions when syncing."
        (Some (
            Button.primaryWithIcon
                "Add Your First Rule"
                (Icons.plus SM Icons.IconColor.Primary)
                (fun () -> dispatch OpenNewRuleModal)
        ))

// ============================================
// Rule Edit Modal
// ============================================

let private ruleEditModal (model: Model) (dispatch: Msg -> unit) =
    let isNew = model.IsNewRule
    let title = if isNew then "Create New Rule" else "Edit Rule"
    let subtitle = if isNew then "Set up automatic categorization" else "Modify rule settings"

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
                    Input.groupRequired "Rule Name" (
                        Input.textSimple
                            model.Form.Name
                            (UpdateRuleFormName >> dispatch)
                            "e.g., Amazon Purchases"
                    )

                    // Pattern type and target field row
                    Html.div [
                        prop.className "grid grid-cols-1 sm:grid-cols-2 gap-4"
                        prop.children [
                            // Pattern type
                            Input.groupSimple "Pattern Type" (
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
                                        ("Contains", "Contains - Match substring")
                                        ("Exact", "Exact - Match full text")
                                        ("Regex", "Regex - Regular expression")
                                    ]
                            )

                            // Target field
                            Input.groupSimple "Match Field" (
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
                                        ("Combined", "Combined - Payee & Memo")
                                        ("Payee", "Payee only")
                                        ("Memo", "Memo only")
                                    ]
                            )
                        ]
                    ]

                    // Pattern input
                    Input.group {
                        Label = "Pattern"
                        Required = true
                        Error = None
                        HelpText = Some (
                            match model.Form.PatternType with
                            | PatternType.Regex -> "Use regular expressions for complex patterns"
                            | Contains -> "Matches if the text contains this substring (case-insensitive)"
                            | Exact -> "Matches only if the entire text equals this (case-insensitive)"
                        )
                        Children =
                            Input.text {
                                Input.textInputDefaults with
                                    Value = model.Form.Pattern
                                    OnChange = (UpdateRuleFormPattern >> dispatch)
                                    Placeholder =
                                        match model.Form.PatternType with
                                        | PatternType.Regex -> "e.g., AMAZON\\.\\w+"
                                        | Contains -> "e.g., amazon"
                                        | Exact -> "e.g., AMAZON MARKETPLACE"
                                    ClassName = Some "font-mono"
                            }
                    }

                    // Category dropdown
                    Input.group {
                        Label = "Category"
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
                                        "Select a category..."
                                        (model.Categories
                                         |> List.map (fun category ->
                                             let (YnabCategoryId id) = category.Id
                                             (id.ToString(), $"{category.GroupName}: {category.Name}")))

                                    if model.Categories.IsEmpty then
                                        Html.div [
                                            prop.className "flex items-center gap-2 mt-2 px-3 py-2 bg-neon-orange/10 text-neon-orange rounded-lg text-sm"
                                            prop.children [
                                                Icons.warning SM Icons.NeonOrange
                                                Html.span [ prop.text "No categories loaded. Please configure YNAB first." ]
                                            ]
                                        ]
                                ]
                            ]
                    }

                    // Payee override (optional)
                    Input.group {
                        Label = "Payee Override"
                        Required = false
                        Error = None
                        HelpText = Some "Leave empty to use original payee"
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
                            prop.className "flex items-center gap-3 p-3 bg-base-200/50 rounded-lg"
                            prop.children [
                                Input.toggle model.Form.Enabled (fun checked' -> dispatch (UpdateRuleFormEnabled checked')) (Some "Rule enabled")
                            ]
                        ]

                    // Test pattern section
                    Html.div [
                        prop.className "pt-4 border-t border-white/10"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2 mb-4"
                                prop.children [
                                    Icons.search SM Icons.NeonTeal
                                    Html.span [ prop.className "font-medium text-base-content"; prop.text "Test Your Pattern" ]
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
                                                        "Enter sample text to test pattern"
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
            Button.ghost "Cancel" (fun () -> dispatch CloseRuleModal)
            Form.submitButton
                (if isNew then "Create Rule" else "Save Changes")
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
        "Add Rule"
        (Icons.plus SM Icons.IconColor.Primary)
        (fun () -> dispatch OpenNewRuleModal)

    // Dropdown menu
    Html.div [
        prop.className "dropdown dropdown-end"
        prop.children [
            Html.label [
                prop.className "btn btn-ghost btn-square text-base-content/70 hover:text-base-content"
                prop.tabIndex 0
                prop.children [
                    Html.span [ prop.className "text-xl"; prop.text "⋮" ]
                ]
            ]
            Html.ul [
                prop.className "dropdown-content z-[1] menu p-2 shadow-lg bg-base-100 border border-white/10 rounded-xl w-52"
                prop.tabIndex 0
                prop.children [
                    Html.li [
                        Html.a [
                            prop.className "flex items-center gap-2"
                            prop.onClick (fun _ -> dispatch ExportRules)
                            prop.children [
                                Icons.download SM Icons.Default
                                Html.span [ prop.text "Export Rules" ]
                            ]
                        ]
                    ]
                    Html.li [
                        Html.label [
                            prop.className "flex items-center gap-2 cursor-pointer"
                            prop.children [
                                Icons.upload SM Icons.Default
                                Html.span [ prop.text "Import Rules" ]
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
                "Categorization Rules"
                (Some "Automate transaction categorization.")
                (rulesHeaderActions dispatch)

            // Info tip with pattern type legend
            Card.view { Card.defaultProps with Variant = Card.Glass; Size = Card.Compact; Hoverable = false } [
                Html.div [
                    prop.className "flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3"
                    prop.children [
                        // Info text
                        Html.div [
                            prop.className "flex items-start gap-3"
                            prop.children [
                                Html.div [
                                    prop.className "w-8 h-8 rounded-lg bg-neon-teal/10 flex items-center justify-center flex-shrink-0"
                                    prop.children [
                                        Icons.info SM Icons.NeonTeal
                                    ]
                                ]
                                Html.p [
                                    prop.className "text-sm text-base-content/70"
                                    prop.text "Rules are applied in priority order. The first matching rule will be used."
                                ]
                            ]
                        ]
                        // Pattern type legend
                        Html.div [
                            prop.className "flex items-center gap-3 text-xs text-base-content/50 pl-11 sm:pl-0"
                            prop.children [
                                Html.span [
                                    prop.className "flex items-center gap-1"
                                    prop.children [
                                        Html.span [ prop.className "font-mono text-[10px] font-bold text-neon-teal bg-white/5 px-1 rounded"; prop.text "~" ]
                                        Html.span [ prop.text "Contains" ]
                                    ]
                                ]
                                Html.span [
                                    prop.className "flex items-center gap-1"
                                    prop.children [
                                        Html.span [ prop.className "font-mono text-[10px] font-bold text-neon-green bg-white/5 px-1 rounded"; prop.text "=" ]
                                        Html.span [ prop.text "Exact" ]
                                    ]
                                ]
                                Html.span [
                                    prop.className "flex items-center gap-1"
                                    prop.children [
                                        Html.span [ prop.className "font-mono text-[10px] font-bold text-neon-purple bg-white/5 px-1 rounded"; prop.text ".*" ]
                                        Html.span [ prop.text "Regex" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]

            // Rules content
            match model.Rules with
            | RemoteData.NotAsked ->
                Loading.centered "Loading rules..."

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
                Html.div [
                    prop.className "space-y-1.5"
                    prop.children [
                        for rule in rules do
                            let (RuleId id) = rule.Id
                            Html.div [
                                prop.key (string id)
                                prop.children [ ruleRow model rule dispatch ]
                            ]
                    ]
                ]

            | RemoteData.Failure error ->
                ErrorDisplay.cardWithTitle "Error loading rules" error (Some (fun () -> dispatch LoadRules))

            // Edit/Create modal
            if model.IsNewRule || model.EditingRule.IsSome then
                ruleEditModal model dispatch
        ]
    ]
