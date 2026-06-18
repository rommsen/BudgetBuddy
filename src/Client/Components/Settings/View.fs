module Components.Settings.View

open Feliz
open Components.Settings.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Settings Section Header
// ============================================

let private sectionHeader (title: string) (isConfigured: bool) =
    Html.div [
        prop.className "card-header flex items-center justify-between p-4 border-b border-border-subtle"
        prop.children [
            Html.h2 [
                prop.className "text-sm font-semibold font-display"
                prop.text title
            ]
            Html.span [
                prop.className (sprintf "status-label %s" (if isConfigured then "text-neon-green" else "text-neon-orange"))
                prop.children [
                    Html.span [
                        prop.className (if isConfigured then "status-dot connected" else "status-dot disconnected")
                    ]
                    Html.text (if isConfigured then "Verbunden" else "Nicht konfiguriert")
                ]
            ]
        ]
    ]

// ============================================
// Read-Only Display Helpers
// ============================================

let private maskValue (value: string) =
    if System.String.IsNullOrEmpty value then "\u2014"
    elif value.Length <= 4 then "****"
    else "****..." + value.[value.Length-4..]

let private settingRow (label: string) (description: string) (value: string) =
    Html.div [
        prop.className "setting-row"
        prop.children [
            Html.div [
                prop.className "setting-info"
                prop.children [
                    Html.div [ prop.className "setting-label"; prop.text label ]
                    Html.div [ prop.className "setting-description"; prop.text description ]
                ]
            ]
            Html.span [
                prop.className "setting-value"
                prop.text value
            ]
        ]
    ]

// ============================================
// YNAB Settings Card
// ============================================

let private ynabSettingsCard (model: Model) (dispatch: Msg -> unit) =
    let isConfigured =
        match model.Settings with
        | Success s -> s.Ynab.IsSome
        | _ -> false

    let isEditing = model.EditingSection = Some YnabSection

    Card.standard [
        sectionHeader "YNAB Verbindung" isConfigured

        if isConfigured && not isEditing then
            // READ-ONLY DISPLAY
            Html.div [
                prop.className "p-4"
                prop.children [
                    match model.Settings with
                    | Success settings ->
                        match settings.Ynab with
                        | Some ynab ->
                            let budgetName =
                                match model.YnabBudgets with
                                | Success budgets ->
                                    ynab.DefaultBudgetId
                                    |> Option.bind (fun bid -> budgets |> List.tryFind (fun b -> b.Budget.Id = bid))
                                    |> Option.map (fun b -> b.Budget.Name)
                                    |> Option.defaultValue "\u2014"
                                | _ -> "\u2014"
                            let accountName =
                                match model.YnabBudgets with
                                | Success budgets ->
                                    ynab.DefaultBudgetId
                                    |> Option.bind (fun bid -> budgets |> List.tryFind (fun b -> b.Budget.Id = bid))
                                    |> Option.bind (fun bwa ->
                                        ynab.DefaultAccountId
                                        |> Option.bind (fun aid -> bwa.Accounts |> List.tryFind (fun a -> a.Id = aid))
                                        |> Option.map (fun a -> a.Name))
                                    |> Option.defaultValue "\u2014"
                                | _ -> "\u2014"

                            let quickAddAccountName =
                                match model.YnabBudgets with
                                | Success budgets ->
                                    ynab.DefaultBudgetId
                                    |> Option.bind (fun bid -> budgets |> List.tryFind (fun b -> b.Budget.Id = bid))
                                    |> Option.bind (fun bwa ->
                                        ynab.QuickAddAccountId
                                        |> Option.bind (fun aid -> bwa.Accounts |> List.tryFind (fun a -> a.Id = aid))
                                        |> Option.map (fun a -> a.Name))
                                    |> Option.defaultValue "—"
                                | _ -> "—"

                            settingRow "Budget" "Aktives YNAB Budget" budgetName
                            settingRow "Konto" "YNAB Konto für Transaktionen" accountName
                            settingRow "Quick-Add-Konto" "Konto für manuell erfasste Transaktionen (z. B. Bar)" quickAddAccountName
                            settingRow "API Token" "Pers\u00f6nlicher Access Token" (maskValue ynab.PersonalAccessToken)

                            Html.div [
                                prop.className "flex gap-2 pt-4 mt-2 border-t border-border-subtle"
                                prop.children [
                                    Button.secondary "Bearbeiten" (fun () -> dispatch (StartEditing YnabSection))
                                    Button.secondary "Verbindung testen" (fun () -> dispatch TestYnabConnection)
                                ]
                            ]
                        | None -> ()
                    | _ -> ()
                ]
            ]
        else
            // EDIT MODE - existing form code
            Html.div [
                prop.className "space-y-4 p-4"
                prop.children [
                    // Token input with label
                    Input.groupRequired "Pers\u00f6nlicher Access Token" (
                        Input.password model.YnabTokenInput (UpdateYnabTokenInput >> dispatch) "YNAB Personal Access Token eingeben"
                    )

                    // Save button with validation
                    Form.submitButton
                        "Token speichern"
                        (fun () -> dispatch SaveYnabToken)
                        false
                        [("Pers\u00f6nlicher Access Token", model.YnabTokenInput)]

                    // Link to YNAB
                    Html.div [
                        prop.className "text-xs text-text-muted/70"
                        prop.children [
                            Html.text "Token erh\u00e4ltlich unter "
                            Html.a [
                                prop.href "https://app.youneedabudget.com/settings/developer"
                                prop.target "_blank"
                                prop.className "text-neon-teal hover:underline inline-flex items-center gap-1"
                                prop.children [
                                    Html.span [ prop.text "YNAB Entwicklereinstellungen" ]
                                    Icons.externalLink Icons.XS Icons.NeonTeal
                                ]
                            ]
                        ]
                    ]

                    // Test connection button
                    let hasYnabToken =
                        match model.Settings with
                        | Success s -> s.Ynab.IsSome
                        | _ -> false
                    if hasYnabToken then
                        Button.secondary "Verbindung testen" (fun () -> dispatch TestYnabConnection)
                    else
                        Button.view {
                            Button.defaultProps with
                                Text = "Verbindung testen"
                                Variant = Button.Secondary
                                IsDisabled = true
                        }

                    // Budget/Account selection (shown after successful test)
                    match model.YnabBudgets with
                    | Loading ->
                        Loading.inlineWithText "Verbindung wird getestet..."

                    | Success budgets when not budgets.IsEmpty ->
                        Html.div [
                            prop.className "space-y-4 pt-4 border-t border-border-subtle"
                            prop.children [
                                Html.div [
                                    prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-green/10 border border-neon-green/30"
                                    prop.children [
                                        Icons.checkCircle Icons.SM Icons.NeonGreen
                                        Html.span [
                                            prop.className "text-sm text-neon-green"
                                            prop.text $"Verbunden! {budgets.Length} Budget(s) gefunden"
                                        ]
                                    ]
                                ]

                                // Budget selection
                                Html.div [
                                    prop.className "grid gap-4 sm:grid-cols-2"
                                    prop.children [
                                        Input.groupSimple "Standard-Budget" (
                                            Html.select [
                                                prop.className "select-field"
                                                prop.value (
                                                    match model.Settings with
                                                    | Success s ->
                                                        s.Ynab
                                                        |> Option.bind (fun y -> y.DefaultBudgetId)
                                                        |> Option.map (fun (YnabBudgetId id) -> id)
                                                        |> Option.defaultValue ""
                                                    | _ -> ""
                                                )
                                                prop.onChange (fun (value: string) ->
                                                    if not (System.String.IsNullOrWhiteSpace(value)) then
                                                        dispatch (SetDefaultBudget (YnabBudgetId value))
                                                )
                                                prop.children [
                                                    Html.option [
                                                        prop.value ""
                                                        prop.text "Budget w\u00e4hlen..."
                                                    ]
                                                    for bwa in budgets do
                                                        let (YnabBudgetId id) = bwa.Budget.Id
                                                        Html.option [
                                                            prop.key id
                                                            prop.value id
                                                            prop.text bwa.Budget.Name
                                                        ]
                                                ]
                                            ])

                                        // Account selection (based on selected budget)
                                        let selectedBudget =
                                            match model.Settings with
                                            | Success s ->
                                                s.Ynab
                                                |> Option.bind (fun y -> y.DefaultBudgetId)
                                                |> Option.bind (fun bid ->
                                                    budgets |> List.tryFind (fun b -> b.Budget.Id = bid)
                                                )
                                            | _ -> None

                                        match selectedBudget with
                                        | Some bwa when not bwa.Accounts.IsEmpty ->
                                            let accountSelect (label: string) (currentId: YnabAccountId option) (onSelect: YnabAccountId -> unit) =
                                                Input.groupSimple label (
                                                    Html.select [
                                                        prop.className "select-field"
                                                        prop.value (
                                                            currentId
                                                            |> Option.map (fun (YnabAccountId id) -> id.ToString())
                                                            |> Option.defaultValue ""
                                                        )
                                                        prop.onChange (fun (value: string) ->
                                                            if not (System.String.IsNullOrWhiteSpace(value)) then
                                                                onSelect (YnabAccountId (System.Guid.Parse value))
                                                        )
                                                        prop.children [
                                                            Html.option [
                                                                prop.value ""
                                                                prop.text "Konto wählen..."
                                                            ]
                                                            for account in bwa.Accounts do
                                                                let (YnabAccountId id) = account.Id
                                                                Html.option [
                                                                    prop.key (id.ToString())
                                                                    prop.value (id.ToString())
                                                                    prop.text (sprintf "%s (%.2f %s)" account.Name account.Balance.Amount account.Balance.Currency)
                                                                ]
                                                        ]
                                                    ])

                                            let currentYnab =
                                                match model.Settings with
                                                | Success s -> s.Ynab
                                                | _ -> None

                                            accountSelect
                                                "Standard-Konto (Bank-Import)"
                                                (currentYnab |> Option.bind (fun y -> y.DefaultAccountId))
                                                (SetDefaultAccount >> dispatch)

                                            accountSelect
                                                "Quick-Add-Konto (z. B. Bar)"
                                                (currentYnab |> Option.bind (fun y -> y.QuickAddAccountId))
                                                (SetQuickAddAccount >> dispatch)
                                        | _ -> Html.none
                                    ]
                                ]
                            ]
                        ]

                    | Failure error ->
                        ErrorDisplay.cardCompact error None

                    | _ -> Html.none

                    // Cancel button if editing (not for initial setup)
                    if isEditing then
                        Html.div [
                            prop.className "pt-2"
                            prop.children [
                                Button.ghost "Abbrechen" (fun () -> dispatch CancelEditing)
                            ]
                        ]
                ]
            ]
    ]

// ============================================
// Comdirect Settings Card
// ============================================

let private comdirectSettingsCard (model: Model) (dispatch: Msg -> unit) =
    let isConfigured =
        match model.Settings with
        | Success s -> s.Comdirect.IsSome
        | _ -> false

    let isEditing = model.EditingSection = Some ComdirectSection

    Card.standard [
        sectionHeader "Comdirect Verbindung" isConfigured

        if isConfigured && not isEditing then
            // READ-ONLY DISPLAY
            Html.div [
                prop.className "p-4"
                prop.children [
                    match model.Settings with
                    | Success settings ->
                        match settings.Comdirect with
                        | Some comdirect ->
                            settingRow "Client-ID" "Comdirect API Client-ID" (maskValue comdirect.ClientId)
                            settingRow "Authentifizierung" "OAuth2 + photoTAN" "\u2014"

                            Html.div [
                                prop.className "flex gap-2 pt-4 mt-2 border-t border-border-subtle"
                                prop.children [
                                    Button.secondary "Bearbeiten" (fun () -> dispatch (StartEditing ComdirectSection))
                                    Button.secondary "Verbindung testen" (fun () -> dispatch TestComdirectConnection)
                                ]
                            ]
                        | None -> ()
                    | _ -> ()
                ]
            ]
        else
            // EDIT MODE - existing form code
            Html.div [
                prop.className "space-y-4 p-4"
                prop.children [
                    // API Credentials row
                    Html.div [
                        prop.className "grid gap-4 sm:grid-cols-2"
                        prop.children [
                            Input.groupRequired "Client-ID" (
                                Input.textSimple model.ComdirectClientIdInput (UpdateComdirectClientIdInput >> dispatch) "Deine API Client-ID")

                            Input.groupRequired "Client-Secret" (
                                Input.password model.ComdirectClientSecretInput (UpdateComdirectClientSecretInput >> dispatch) "Dein API Client-Secret")
                        ]
                    ]

                    // Login Credentials row
                    Html.div [
                        prop.className "grid gap-4 sm:grid-cols-2"
                        prop.children [
                            Input.groupRequired "Benutzername (Zugangsnummer)" (
                                Input.textSimple model.ComdirectUsernameInput (UpdateComdirectUsernameInput >> dispatch) "Deine Zugangsnummer")

                            Input.groupRequired "PIN" (
                                Input.password model.ComdirectPasswordInput (UpdateComdirectPasswordInput >> dispatch) "Deine Comdirect-PIN")
                        ]
                    ]

                    // Account ID input (required)
                    Input.groupRequired "Konto-ID" (
                        Input.textSimple model.ComdirectAccountIdInput (UpdateComdirectAccountIdInput >> dispatch) "Deine Comdirect Konto-ID"
                    )

                    // Save button with validation feedback
                    Form.submitButton
                        "Zugangsdaten speichern"
                        (fun () -> dispatch SaveComdirectCredentials)
                        false
                        [
                            ("Client-ID", model.ComdirectClientIdInput)
                            ("Client-Secret", model.ComdirectClientSecretInput)
                            ("Benutzername", model.ComdirectUsernameInput)
                            ("PIN", model.ComdirectPasswordInput)
                            ("Konto-ID", model.ComdirectAccountIdInput)
                        ]

                    // Info tip
                    Html.div [
                        prop.className "flex items-start gap-3 p-3 rounded-lg bg-neon-teal/5 border border-neon-teal/20"
                        prop.children [
                            Icons.info Icons.MD Icons.NeonTeal
                            Html.p [
                                prop.className "text-xs md:text-sm text-text-muted"
                                prop.children [
                                    Html.text "Du ben\u00f6tigst Zugang zur Comdirect API. Besuche "
                                    Html.a [
                                        prop.href "https://www.comdirect.de/cms/kontakt-zugaenge-api.html"
                                        prop.target "_blank"
                                        prop.className "text-neon-teal hover:underline inline-flex items-center gap-1"
                                        prop.children [
                                            Html.span [ prop.text "Comdirect API" ]
                                            Icons.externalLink Icons.XS Icons.NeonTeal
                                        ]
                                    ]
                                    Html.text " um Zugang zu beantragen."
                                ]
                            ]
                        ]
                    ]

                    // Connection test section
                    Html.div [
                        prop.className "space-y-4 pt-4 border-t border-border-subtle"
                        prop.children [
                            let hasCredentials =
                                match model.Settings with
                                | Success s -> s.Comdirect.IsSome
                                | _ -> false

                            if hasCredentials then
                                if model.ComdirectAuthPending then
                                    // Waiting for TAN confirmation
                                    Html.div [
                                        prop.className "space-y-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "flex items-center gap-3 p-4 rounded-lg bg-neon-orange/10 border border-neon-orange/30"
                                                prop.children [
                                                    Loading.spinner Loading.MD Loading.Orange
                                                    Html.div [
                                                        prop.className "flex-1"
                                                        prop.children [
                                                            Html.p [
                                                                prop.className "text-sm font-medium text-neon-orange"
                                                                prop.text "Warte auf TAN-Best\u00e4tigung..."
                                                            ]
                                                            Html.p [
                                                                prop.className "text-xs text-text-muted"
                                                                prop.text "Bitte best\u00e4tige die Push-TAN in deiner Comdirect App"
                                                            ]
                                                        ]
                                                    ]
                                                ]
                                            ]
                                            Button.primary "TAN best\u00e4tigt" (fun () -> dispatch ConfirmComdirectTan)
                                        ]
                                    ]
                                else
                                    match model.ComdirectConnectionValid with
                                    | Loading ->
                                        Loading.inlineWithText "Verbindung wird getestet..."
                                    | Success _ ->
                                        // Connection verified successfully
                                        Html.div [
                                            prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-green/10 border border-neon-green/30"
                                            prop.children [
                                                Icons.checkCircle Icons.SM Icons.NeonGreen
                                                Html.span [
                                                    prop.className "text-sm text-neon-green"
                                                    prop.text "Zugangsdaten erfolgreich verifiziert!"
                                                ]
                                            ]
                                        ]
                                    | Failure error ->
                                        Html.div [
                                            prop.className "space-y-3"
                                            prop.children [
                                                ErrorDisplay.cardCompact error None
                                                Button.secondary "Verbindung testen" (fun () -> dispatch TestComdirectConnection)
                                            ]
                                        ]
                                    | NotAsked ->
                                        Button.secondary "Verbindung testen" (fun () -> dispatch TestComdirectConnection)
                        ]
                    ]

                    // Cancel button if editing (not for initial setup)
                    if isEditing then
                        Html.div [
                            prop.className "pt-2"
                            prop.children [
                                Button.ghost "Abbrechen" (fun () -> dispatch CancelEditing)
                            ]
                        ]
                ]
            ]
    ]

// ============================================
// Sync Settings Card
// ============================================

let private syncSettingsCard (model: Model) (dispatch: Msg -> unit) =
    let isEditing = model.EditingSection = Some SyncSection

    let daysToFetch =
        match model.Settings with
        | Success settings -> settings.Sync.DaysToFetch
        | _ -> model.SyncDaysInput

    Card.standard [
        sectionHeader "Sync-Einstellungen" true

        if not isEditing then
            // READ-ONLY DISPLAY
            Html.div [
                prop.className "p-4"
                prop.children [
                    settingRow "Transaktionszeitraum" "Wie weit zur\u00fcckliegende Transaktionen synchronisiert werden" (sprintf "%d Tage" daysToFetch)

                    Html.div [
                        prop.className "flex gap-2 pt-4 mt-2 border-t border-border-subtle"
                        prop.children [
                            Button.secondary "Bearbeiten" (fun () -> dispatch (StartEditing SyncSection))
                        ]
                    ]
                ]
            ]
        else
            // EDIT MODE - existing form code
            Html.div [
                prop.className "space-y-5 p-4"
                prop.children [
                    // Days to fetch with visual slider
                    Html.div [
                        prop.className "space-y-3"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center justify-between"
                                prop.children [
                                    Html.label [
                                        prop.className "text-sm font-medium text-text-secondary"
                                        prop.text "Transaktionszeitraum"
                                    ]
                                    Html.span [
                                        prop.className "text-xl md:text-2xl font-bold font-mono text-neon-teal"
                                        prop.text $"{model.SyncDaysInput}"
                                    ]
                                ]
                            ]
                            Html.input [
                                prop.className "range range-sm w-full accent-neon-teal"
                                prop.type'.range
                                prop.min 7
                                prop.max 90
                                prop.step 1
                                prop.value model.SyncDaysInput
                                prop.onChange (fun (value: int) -> dispatch (UpdateSyncDaysInput value))
                            ]
                            Html.div [
                                prop.className $"flex justify-between {Tokens.FontSizes.micro} md:text-xs text-text-muted px-1"
                                prop.children [
                                    Html.span [ prop.text "7 Tage" ]
                                    Html.span [ prop.text "30 Tage" ]
                                    Html.span [ prop.text "60 Tage" ]
                                    Html.span [ prop.text "90 Tage" ]
                                ]
                            ]
                        ]
                    ]

                    // Save button
                    Button.primaryWithIcon "Einstellungen speichern" (Icons.check Icons.SM Icons.Primary) (fun () -> dispatch SaveSyncSettings)

                    // Cancel button
                    Html.div [
                        prop.className "pt-2"
                        prop.children [
                            Button.ghost "Abbrechen" (fun () -> dispatch CancelEditing)
                        ]
                    ]
                ]
            ]
    ]

// ============================================
// Page Header
// ============================================

let private pageHeader (dispatch: Msg -> unit) =
    PageHeader.gradientWithActions
        "Einstellungen"
        (Some "Verbindungen und Konfiguration")
        [
            Button.view {
                Button.defaultProps with
                    Text = ""
                    OnClick = fun () -> dispatch LoadSettings
                    Variant = Button.Ghost
                    Icon = Some (Icons.sync Icons.SM Icons.Default)
                    Title = Some "Einstellungen aktualisieren"
            }
        ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-5 md:space-y-6 max-w-3xl mx-auto"
        prop.children [
            // Header
            pageHeader dispatch

            // Loading state
            match model.Settings with
            | Loading ->
                Loading.centered "Einstellungen werden geladen..."

            | Failure error ->
                ErrorDisplay.cardCompact $"Einstellungen konnten nicht geladen werden: {error}" (Some (fun () -> dispatch LoadSettings))

            | _ ->
                Html.div [
                    prop.className "space-y-5"
                    prop.children [
                        Html.div [
                            prop.className "animate-slide-up"
                            prop.style [ style.custom ("animationDelay", "50ms") ]
                            prop.children [ ynabSettingsCard model dispatch ]
                        ]
                        Html.div [
                            prop.className "animate-slide-up"
                            prop.style [ style.custom ("animationDelay", "120ms") ]
                            prop.children [ comdirectSettingsCard model dispatch ]
                        ]
                        Html.div [
                            prop.className "animate-slide-up"
                            prop.style [ style.custom ("animationDelay", "190ms") ]
                            prop.children [ syncSettingsCard model dispatch ]
                        ]
                        // Discreet entry point to the living Styleguide gallery.
                        // Uses a plain hash-router anchor so Settings stays free of
                        // new Msg cases — navigation is a root concern handled by
                        // router.onUrlChanged.
                        Html.div [
                            prop.className "animate-slide-up pt-2"
                            prop.style [ style.custom ("animationDelay", "260ms") ]
                            prop.children [
                                Html.a [
                                    prop.href "#/styleguide"
                                    prop.className "inline-flex items-center gap-2 text-sm text-neon-teal hover:underline"
                                    prop.children [
                                        Icons.externalLink Icons.XS Icons.NeonTeal
                                        Html.span [ prop.text "Styleguide ansehen (Design-System-Galerie)" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]
