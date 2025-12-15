module Components.Settings.View

open Feliz
open Components.Settings.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Settings Section Header
// ============================================

let private sectionHeader (icon: ReactElement) (title: string) (subtitle: string) (isConfigured: bool) =
    Html.div [
        prop.className "flex items-start justify-between mb-5"
        prop.children [
            Html.div [
                prop.className "flex items-start gap-3"
                prop.children [
                    Html.div [
                        prop.className "w-10 h-10 md:w-12 md:h-12 rounded-xl bg-gradient-to-br from-neon-teal/20 to-neon-green/10 flex items-center justify-center"
                        prop.children [ icon ]
                    ]
                    Html.div [
                        prop.children [
                            Html.h2 [
                                prop.className "text-base md:text-lg font-semibold font-display"
                                prop.text title
                            ]
                            Html.p [
                                prop.className "text-xs md:text-sm text-base-content/50"
                                prop.text subtitle
                            ]
                        ]
                    ]
                ]
            ]
            if isConfigured then
                Badge.success "Connected"
            else
                Badge.warning "Not configured"
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

    Card.standard [
        sectionHeader (Icons.dollar Icons.MD Icons.NeonTeal) "YNAB Connection" "Connect your YNAB budget for automatic imports" isConfigured

        Html.div [
            prop.className "space-y-4"
            prop.children [
                // Token input with label
                Input.groupSimple "Personal Access Token" (
                    Html.div [
                        prop.className "flex flex-col sm:flex-row gap-2"
                        prop.children [
                            Input.password model.YnabTokenInput (UpdateYnabTokenInput >> dispatch) "Enter your YNAB Personal Access Token"
                            let isTokenEmpty = System.String.IsNullOrWhiteSpace(model.YnabTokenInput)
                            Button.view {
                                Button.defaultProps with
                                    Text = "Save"
                                    OnClick = (fun () -> dispatch SaveYnabToken)
                                    Variant = Button.Primary
                                    IsDisabled = isTokenEmpty
                            }
                        ]
                    ])

                // Link to YNAB
                Html.div [
                    prop.className "text-xs text-base-content/50"
                    prop.children [
                        Html.text "Get your token from "
                        Html.a [
                            prop.href "https://app.youneedabudget.com/settings/developer"
                            prop.target "_blank"
                            prop.className "text-neon-teal hover:underline inline-flex items-center gap-1"
                            prop.children [
                                Html.span [ prop.text "YNAB Developer Settings" ]
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
                    Button.secondary "Test Connection" (fun () -> dispatch TestYnabConnection)
                else
                    Button.view {
                        Button.defaultProps with
                            Text = "Test Connection"
                            Variant = Button.Secondary
                            IsDisabled = true
                    }

                // Budget/Account selection (shown after successful test)
                match model.YnabBudgets with
                | Loading ->
                    Loading.inlineWithText "Testing connection..."

                | Success budgets when not budgets.IsEmpty ->
                    Html.div [
                        prop.className "space-y-4 pt-4 border-t border-white/5"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-green/10 border border-neon-green/30"
                                prop.children [
                                    Icons.checkCircle Icons.SM Icons.NeonGreen
                                    Html.span [
                                        prop.className "text-sm text-neon-green"
                                        prop.text $"Connected! Found {budgets.Length} budget(s)"
                                    ]
                                ]
                            ]

                            // Budget selection
                            Html.div [
                                prop.className "grid gap-4 sm:grid-cols-2"
                                prop.children [
                                    Input.groupSimple "Default Budget" (
                                        Html.select [
                                            prop.className "select select-bordered w-full bg-base-200/50 border-white/10 focus:border-neon-teal focus:shadow-[0_0_15px_rgba(0,212,170,0.3)]"
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
                                                    prop.text "Select budget..."
                                                ]
                                                for bwa in budgets do
                                                    Html.option [
                                                        let (YnabBudgetId id) = bwa.Budget.Id
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
                                        Input.groupSimple "Default Account" (
                                            Html.select [
                                                prop.className "select select-bordered w-full bg-base-200/50 border-white/10 focus:border-neon-teal focus:shadow-[0_0_15px_rgba(0,212,170,0.3)]"
                                                prop.value (
                                                    match model.Settings with
                                                    | Success s ->
                                                        s.Ynab
                                                        |> Option.bind (fun y -> y.DefaultAccountId)
                                                        |> Option.map (fun (YnabAccountId id) -> id.ToString())
                                                        |> Option.defaultValue ""
                                                    | _ -> ""
                                                )
                                                prop.onChange (fun (value: string) ->
                                                    if not (System.String.IsNullOrWhiteSpace(value)) then
                                                        dispatch (SetDefaultAccount (YnabAccountId (System.Guid.Parse value)))
                                                )
                                                prop.children [
                                                    Html.option [
                                                        prop.value ""
                                                        prop.text "Select account..."
                                                    ]
                                                    for account in bwa.Accounts do
                                                        Html.option [
                                                            let (YnabAccountId id) = account.Id
                                                            prop.value (id.ToString())
                                                            prop.text (sprintf "%s (%.2f %s)" account.Name account.Balance.Amount account.Balance.Currency)
                                                        ]
                                                ]
                                            ])
                                    | _ -> Html.none
                                ]
                            ]
                        ]
                    ]

                | Failure error ->
                    Html.div [
                        prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-red/10 border border-neon-red/30"
                        prop.children [
                            Icons.xCircle Icons.SM Icons.Error
                            Html.span [
                                prop.className "text-sm text-neon-red"
                                prop.text error
                            ]
                        ]
                    ]

                | _ -> Html.none
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

    Card.standard [
        sectionHeader (Icons.banknotes Icons.MD Icons.NeonOrange) "Comdirect Connection" "Connect your Comdirect bank account" isConfigured

        Html.div [
            prop.className "space-y-4"
            prop.children [
                // API Credentials row
                Html.div [
                    prop.className "grid gap-4 sm:grid-cols-2"
                    prop.children [
                        Input.groupSimple "Client ID" (
                            Input.textSimple model.ComdirectClientIdInput (UpdateComdirectClientIdInput >> dispatch) "Your API Client ID")

                        Input.groupSimple "Client Secret" (
                            Input.password model.ComdirectClientSecretInput (UpdateComdirectClientSecretInput >> dispatch) "Your API Client Secret")
                    ]
                ]

                // Login Credentials row
                Html.div [
                    prop.className "grid gap-4 sm:grid-cols-2"
                    prop.children [
                        Input.groupSimple "Username (Zugangsnummer)" (
                            Input.textSimple model.ComdirectUsernameInput (UpdateComdirectUsernameInput >> dispatch) "Your access number")

                        Input.groupSimple "PIN" (
                            Input.password model.ComdirectPasswordInput (UpdateComdirectPasswordInput >> dispatch) "Your Comdirect PIN")
                    ]
                ]

                // Account ID input (required)
                Input.groupRequired "Account ID" (
                    Input.textSimple model.ComdirectAccountIdInput (UpdateComdirectAccountIdInput >> dispatch) "Your Comdirect account ID"
                )

                // Save button - now also validates Account ID
                let isFormValid =
                    not (System.String.IsNullOrWhiteSpace(model.ComdirectClientIdInput)) &&
                    not (System.String.IsNullOrWhiteSpace(model.ComdirectClientSecretInput)) &&
                    not (System.String.IsNullOrWhiteSpace(model.ComdirectUsernameInput)) &&
                    not (System.String.IsNullOrWhiteSpace(model.ComdirectPasswordInput)) &&
                    not (System.String.IsNullOrWhiteSpace(model.ComdirectAccountIdInput))

                Button.view {
                    Button.defaultProps with
                        Text = "Save Credentials"
                        OnClick = (fun () -> dispatch SaveComdirectCredentials)
                        Variant = Button.Primary
                        IsDisabled = not isFormValid
                        Icon = Some (Icons.check Icons.SM Icons.Primary)
                }

                // Info tip
                Html.div [
                    prop.className "flex items-start gap-3 p-3 rounded-lg bg-neon-teal/5 border border-neon-teal/20"
                    prop.children [
                        Icons.info Icons.MD Icons.NeonTeal
                        Html.p [
                            prop.className "text-xs md:text-sm text-base-content/60"
                            prop.children [
                                Html.text "You need a Comdirect API access. Visit "
                                Html.a [
                                    prop.href "https://www.comdirect.de/cms/kontakt-zugaenge-api.html"
                                    prop.target "_blank"
                                    prop.className "text-neon-teal hover:underline inline-flex items-center gap-1"
                                    prop.children [
                                        Html.span [ prop.text "Comdirect API" ]
                                        Icons.externalLink Icons.XS Icons.NeonTeal
                                    ]
                                ]
                                Html.text " to request access."
                            ]
                        ]
                    ]
                ]

                // Connection test section
                Html.div [
                    prop.className "space-y-4 pt-4 border-t border-white/5"
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
                                                            prop.text "Waiting for TAN confirmation..."
                                                        ]
                                                        Html.p [
                                                            prop.className "text-xs text-base-content/60"
                                                            prop.text "Please confirm the Push-TAN on your Comdirect app"
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                        Button.primary "I've Confirmed the TAN" (fun () -> dispatch ConfirmComdirectTan)
                                    ]
                                ]
                            else
                                match model.ComdirectConnectionValid with
                                | Loading ->
                                    Loading.inlineWithText "Testing connection..."
                                | Success _ ->
                                    // Connection verified successfully
                                    Html.div [
                                        prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-green/10 border border-neon-green/30"
                                        prop.children [
                                            Icons.checkCircle Icons.SM Icons.NeonGreen
                                            Html.span [
                                                prop.className "text-sm text-neon-green"
                                                prop.text "Credentials verified successfully!"
                                            ]
                                        ]
                                    ]
                                | Failure error ->
                                    Html.div [
                                        prop.className "space-y-3"
                                        prop.children [
                                            Html.div [
                                                prop.className "flex items-center gap-2 p-3 rounded-lg bg-neon-red/10 border border-neon-red/30"
                                                prop.children [
                                                    Icons.xCircle Icons.SM Icons.Error
                                                    Html.span [
                                                        prop.className "text-sm text-neon-red"
                                                        prop.text error
                                                    ]
                                                ]
                                            ]
                                            Button.secondary "Test Connection" (fun () -> dispatch TestComdirectConnection)
                                        ]
                                    ]
                                | NotAsked ->
                                    Button.secondary "Test Connection" (fun () -> dispatch TestComdirectConnection)
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Sync Settings Card
// ============================================

let private syncSettingsCard (model: Model) (dispatch: Msg -> unit) =
    Card.standard [
        sectionHeader (Icons.sync Icons.MD Icons.NeonPurple) "Sync Settings" "Configure how transactions are fetched" true

        Html.div [
            prop.className "space-y-5"
            prop.children [
                // Days to fetch with visual slider
                Html.div [
                    prop.className "space-y-3"
                    prop.children [
                        Html.div [
                            prop.className "flex items-center justify-between"
                            prop.children [
                                Html.label [
                                    prop.className "text-sm font-medium text-base-content/70"
                                    prop.text "Days to Fetch"
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
                            prop.className "flex justify-between text-[10px] md:text-xs text-base-content/40 px-1"
                            prop.children [
                                Html.span [ prop.text "7 days" ]
                                Html.span [ prop.text "30 days" ]
                                Html.span [ prop.text "60 days" ]
                                Html.span [ prop.text "90 days" ]
                            ]
                        ]
                    ]
                ]

                // Save button
                Button.primaryWithIcon "Save Settings" (Icons.check Icons.SM Icons.Primary) (fun () -> dispatch SaveSyncSettings)
            ]
        ]
    ]

// ============================================
// Page Header
// ============================================

let private pageHeader (dispatch: Msg -> unit) =
    Html.div [
        prop.className "flex items-center justify-between animate-fade-in"
        prop.children [
            Html.div [
                prop.children [
                    Html.h1 [
                        prop.className "text-2xl md:text-3xl font-bold font-display"
                        prop.text "Settings"
                    ]
                    Html.p [
                        prop.className "text-base-content/50 mt-1 text-sm md:text-base"
                        prop.text "Configure your connections and preferences."
                    ]
                ]
            ]
            Button.view {
                Button.defaultProps with
                    Text = ""
                    OnClick = fun () -> dispatch LoadSettings
                    Variant = Button.Ghost
                    Icon = Some (Icons.sync Icons.SM Icons.Default)
                    Title = Some "Refresh settings"
            }
        ]
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
                Loading.centered "Loading settings..."

            | Failure error ->
                Html.div [
                    prop.className "flex items-center gap-3 p-4 rounded-xl bg-neon-red/10 border border-neon-red/30"
                    prop.children [
                        Icons.xCircle Icons.MD Icons.Error
                        Html.div [
                            prop.className "flex-1"
                            prop.children [
                                Html.span [
                                    prop.className "text-neon-red text-sm"
                                    prop.text $"Failed to load settings: {error}"
                                ]
                            ]
                        ]
                        Button.secondary "Retry" (fun () -> dispatch LoadSettings)
                    ]
                ]

            | _ ->
                Html.div [
                    prop.className "space-y-5"
                    prop.children [
                        ynabSettingsCard model dispatch
                        comdirectSettingsCard model dispatch
                        syncSettingsCard model dispatch
                    ]
                ]
        ]
    ]
