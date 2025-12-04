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
// Rule Card Component (Mobile-first)
// ============================================

let private ruleCard (rule: Rule) (dispatch: Msg -> unit) =
    let opacityClass = if not rule.Enabled then "opacity-50" else ""

    Html.div [
        prop.className $"animate-fade-in {opacityClass}"
        prop.children [
            Card.view { Card.defaultProps with Variant = Card.Standard; Size = Card.Normal } [
                // Header row
                Html.div [
                    prop.className "flex items-start justify-between gap-3"
                    prop.children [
                        Html.div [
                            prop.className "flex-1 min-w-0"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center gap-2 flex-wrap"
                                    prop.children [
                                        Html.h3 [
                                            prop.className "font-semibold text-base-content truncate"
                                            prop.text rule.Name
                                        ]
                                        patternTypeBadge rule.PatternType
                                    ]
                                ]
                                Html.p [
                                    prop.className "text-sm text-base-content/60 mt-1 font-mono truncate"
                                    prop.title rule.Pattern
                                    prop.text rule.Pattern
                                ]
                            ]
                        ]
                        Input.toggle rule.Enabled (fun _ -> dispatch (ToggleRuleEnabled rule.Id)) None
                    ]
                ]

                // Category and target field
                Html.div [
                    prop.className "flex items-center justify-between mt-4 pt-4 border-t border-white/5"
                    prop.children [
                        Html.div [
                            prop.className "flex items-center gap-3"
                            prop.children [
                                Html.div [
                                    prop.className "w-9 h-9 rounded-lg bg-neon-teal/10 flex items-center justify-center"
                                    prop.children [
                                        Icons.rules SM Icons.NeonTeal
                                    ]
                                ]
                                Html.div [
                                    prop.children [
                                        Html.p [
                                            prop.className "text-sm font-medium text-base-content"
                                            prop.text rule.CategoryName
                                        ]
                                        Html.p [
                                            prop.className "text-xs text-base-content/50"
                                            prop.text $"Match in: {targetFieldText rule.TargetField}"
                                        ]
                                    ]
                                ]
                            ]
                        ]
                        Html.div [
                            prop.className "flex gap-1"
                            prop.children [
                                Button.iconButton (Icons.edit SM Icons.Default) Button.Ghost (fun () -> dispatch (EditRule rule.Id))
                                Button.iconButton (Icons.trash SM Icons.Error) Button.Ghost (fun () -> dispatch (DeleteRule rule.Id))
                            ]
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
                            model.RuleFormName
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
                                    (patternTypeText model.RuleFormPatternType)
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
                                    (targetFieldText model.RuleFormTargetField)
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
                            match model.RuleFormPatternType with
                            | PatternType.Regex -> "Use regular expressions for complex patterns"
                            | Contains -> "Matches if the text contains this substring (case-insensitive)"
                            | Exact -> "Matches only if the entire text equals this (case-insensitive)"
                        )
                        Children =
                            Input.text {
                                Input.textInputDefaults with
                                    Value = model.RuleFormPattern
                                    OnChange = (UpdateRuleFormPattern >> dispatch)
                                    Placeholder =
                                        match model.RuleFormPatternType with
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
                                    Input.selectWithPlaceholder
                                        (match model.RuleFormCategoryId with
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
                                                model.RuleFormPayeeOverride
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
                                Input.toggle model.RuleFormEnabled (fun checked' -> dispatch (UpdateRuleFormEnabled checked')) (Some "Rule enabled")
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
                                                        model.RuleFormTestInput
                                                        (UpdateRuleFormTestInput >> dispatch)
                                                        "Enter sample text to test pattern"
                                                ]
                                            ]
                                            Button.secondary
                                                "Test"
                                                (fun () -> dispatch TestRulePattern)
                                        ]
                                    ]
                                    match model.RuleFormTestResult with
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
            Button.view {
                Button.defaultProps with
                    Text = if isNew then "Create Rule" else "Save Changes"
                    Variant = Button.Primary
                    IsLoading = model.RuleSaving
                    IsDisabled =
                        model.RuleSaving ||
                        System.String.IsNullOrWhiteSpace(model.RuleFormName) ||
                        System.String.IsNullOrWhiteSpace(model.RuleFormPattern) ||
                        model.RuleFormCategoryId.IsNone
                    OnClick = fun () -> dispatch SaveRule
                    Icon = Some (Icons.check SM Icons.IconColor.Primary)
            }
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-6 animate-fade-in"
        prop.children [
            // Header
            Html.div [
                prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.h1 [
                                prop.className "text-2xl md:text-4xl font-bold font-display bg-gradient-to-r from-neon-teal to-neon-green bg-clip-text text-transparent"
                                prop.text "Categorization Rules"
                            ]
                            Html.p [
                                prop.className "text-base-content/60 mt-1"
                                prop.text "Automate transaction categorization."
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "flex gap-2"
                        prop.children [
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
                    ]
                ]
            ]

            // Info tip
            Card.view { Card.defaultProps with Variant = Card.Glass; Size = Card.Compact; Hoverable = false } [
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
                            prop.text "Rules are applied in priority order. The first matching rule will be used to categorize a transaction."
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
                        for _ in 1..3 do
                            Loading.cardSkeleton
                    ]
                ]

            | RemoteData.Success rules when rules.IsEmpty ->
                emptyState dispatch

            | RemoteData.Success rules ->
                Html.div [
                    prop.className "grid gap-3"
                    prop.children [
                        for rule in rules do
                            ruleCard rule dispatch
                    ]
                ]

            | RemoteData.Failure error ->
                Card.view { Card.defaultProps with Variant = Card.Standard; Hoverable = false } [
                    Html.div [
                        prop.className "flex items-center gap-3"
                        prop.children [
                            Html.div [
                                prop.className "w-10 h-10 rounded-lg bg-neon-red/10 flex items-center justify-center flex-shrink-0"
                                prop.children [
                                    Icons.xCircle MD Icons.NeonRed
                                ]
                            ]
                            Html.div [
                                prop.className "flex-1"
                                prop.children [
                                    Html.p [
                                        prop.className "font-medium text-neon-red"
                                        prop.text "Error loading rules"
                                    ]
                                    Html.p [
                                        prop.className "text-sm text-base-content/60"
                                        prop.text error
                                    ]
                                ]
                            ]
                            Button.secondary "Retry" (fun () -> dispatch LoadRules)
                        ]
                    ]
                ]

            // Edit/Create modal
            if model.IsNewRule || model.EditingRule.IsSome then
                ruleEditModal model dispatch
        ]
    ]
