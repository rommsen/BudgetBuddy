module Views.SettingsView

open Feliz
open State
open Types
open Shared.Domain

// ============================================
// Icon Components
// ============================================

let private bankIcon =
    Html.span [
        prop.className "text-2xl"
        prop.text "🏦"
    ]

let private ynabIcon =
    Html.span [
        prop.className "text-2xl"
        prop.text "💰"
    ]

let private syncIcon =
    Html.span [
        prop.className "text-2xl"
        prop.text "🔄"
    ]

let private checkIcon =
    Html.span [
        prop.text "✓"
    ]

let private externalLinkIcon =
    Html.span [
        prop.text "↗️"
    ]

// ============================================
// Settings Section Header
// ============================================

let private sectionHeader (icon: ReactElement) (title: string) (subtitle: string) (isConfigured: bool) =
    Html.div [
        prop.className "flex items-start justify-between mb-6"
        prop.children [
            Html.div [
                prop.className "flex items-start gap-4"
                prop.children [
                    Html.div [
                        prop.className "w-12 h-12 rounded-xl bg-gradient-to-br from-primary/20 to-secondary/20 flex items-center justify-center text-primary"
                        prop.children [ icon ]
                    ]
                    Html.div [
                        prop.children [
                            Html.h2 [
                                prop.className "text-lg font-bold"
                                prop.text title
                            ]
                            Html.p [
                                prop.className "text-sm text-base-content/60"
                                prop.text subtitle
                            ]
                        ]
                    ]
                ]
            ]
            if isConfigured then
                Html.div [
                    prop.className "flex items-center gap-1 px-2.5 py-1 rounded-full bg-success/10 text-success text-sm font-medium"
                    prop.children [
                        checkIcon
                        Html.span [ prop.text "Connected" ]
                    ]
                ]
            else
                Html.div [
                    prop.className "flex items-center gap-1 px-2.5 py-1 rounded-full bg-warning/10 text-warning text-sm font-medium"
                    prop.children [
                        Html.span [ prop.text "⚠️" ]
                        Html.span [ prop.text "Not configured" ]
                    ]
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

    Html.div [
        prop.className "card bg-base-100 shadow-lg animate-slide-up"
        prop.children [
            Html.div [
                prop.className "card-body p-5 md:p-6"
                prop.children [
                    sectionHeader ynabIcon "YNAB Connection" "Connect your YNAB budget for automatic imports" isConfigured

                    // Token input
                    Html.div [
                        prop.className "space-y-4"
                        prop.children [
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label py-1"
                                        prop.children [
                                            Html.span [ prop.className "label-text font-medium"; prop.text "Personal Access Token" ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "flex flex-col sm:flex-row gap-2"
                                        prop.children [
                                            Html.input [
                                                prop.className "input input-bordered flex-1 font-mono text-sm bg-base-100"
                                                prop.type'.password
                                                prop.placeholder "Enter your YNAB Personal Access Token"
                                                prop.value model.YnabTokenInput
                                                prop.onChange (UpdateYnabTokenInput >> dispatch)
                                            ]
                                            Html.button [
                                                prop.className "btn btn-primary gap-2"
                                                prop.onClick (fun _ -> dispatch SaveYnabToken)
                                                prop.children [
                                                    Html.span [ prop.text "💾" ]
                                                    Html.span [ prop.text "Save" ]
                                                ]
                                            ]
                                        ]
                                    ]
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [
                                                prop.className "label-text-alt flex items-center gap-1"
                                                prop.children [
                                                    Html.text "Get your token from "
                                                    Html.a [
                                                        prop.href "https://app.youneedabudget.com/settings/developer"
                                                        prop.target "_blank"
                                                        prop.className "link link-primary inline-flex items-center gap-1"
                                                        prop.children [
                                                            Html.span [ prop.text "YNAB Developer Settings" ]
                                                            externalLinkIcon
                                                        ]
                                                    ]
                                                ]
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Test connection button
                            Html.button [
                                prop.className "btn btn-outline btn-sm gap-2"
                                prop.onClick (fun _ -> dispatch TestYnabConnection)
                                prop.disabled (System.String.IsNullOrWhiteSpace(model.YnabTokenInput))
                                prop.children [
                                    Html.span [ prop.text "✓" ]
                                    Html.span [ prop.text "Test Connection" ]
                                ]
                            ]

                            // Budget/Account selection (shown after successful test)
                            match model.YnabBudgets with
                            | Loading ->
                                Html.div [
                                    prop.className "flex items-center gap-2 py-4 text-base-content/60"
                                    prop.children [
                                        Html.span [ prop.className "loading loading-spinner loading-sm" ]
                                        Html.span [ prop.text "Testing connection..." ]
                                    ]
                                ]
                            | Success budgets when not budgets.IsEmpty ->
                                Html.div [
                                    prop.className "space-y-4 pt-4 border-t border-base-200"
                                    prop.children [
                                        Html.div [
                                            prop.className "flex items-center gap-2 px-3 py-2 bg-success/10 text-success rounded-lg text-sm"
                                            prop.children [
                                                checkIcon
                                                Html.span [ prop.text $"Connected! Found {budgets.Length} budget(s)" ]
                                            ]
                                        ]

                                        // Budget selection
                                        Html.div [
                                            prop.className "grid gap-4 sm:grid-cols-2"
                                            prop.children [
                                                Html.div [
                                                    prop.className "form-control w-full"
                                                    prop.children [
                                                        Html.label [
                                                            prop.className "label py-1"
                                                            prop.children [
                                                                Html.span [ prop.className "label-text font-medium"; prop.text "Default Budget" ]
                                                            ]
                                                        ]
                                                        Html.select [
                                                            prop.className "select select-bordered w-full bg-base-100"
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
                                                        ]
                                                    ]
                                                ]

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
                                                    Html.div [
                                                        prop.className "form-control w-full"
                                                        prop.children [
                                                            Html.label [
                                                                prop.className "label py-1"
                                                                prop.children [
                                                                    Html.span [ prop.className "label-text font-medium"; prop.text "Default Account" ]
                                                                ]
                                                            ]
                                                            Html.select [
                                                                prop.className "select select-bordered w-full bg-base-100"
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
                                                                            prop.text $"{account.Name} ({account.Balance.Amount:N2} {account.Balance.Currency})"
                                                                        ]
                                                                ]
                                                            ]
                                                        ]
                                                    ]
                                                | _ -> Html.none
                                            ]
                                        ]
                                    ]
                                ]
                            | Failure error ->
                                Html.div [
                                    prop.className "flex items-center gap-2 px-3 py-2 bg-error/10 text-error rounded-lg text-sm"
                                    prop.text error
                                ]
                            | _ -> Html.none
                        ]
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

    Html.div [
        prop.className "card bg-base-100 shadow-lg animate-slide-up"
        prop.children [
            Html.div [
                prop.className "card-body p-5 md:p-6"
                prop.children [
                    sectionHeader bankIcon "Comdirect Connection" "Connect your Comdirect bank account" isConfigured

                    Html.div [
                        prop.className "space-y-4"
                        prop.children [
                            // API Credentials
                            Html.div [
                                prop.className "grid gap-4 sm:grid-cols-2"
                                prop.children [
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium"; prop.text "Client ID" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-100"
                                                prop.type'.text
                                                prop.placeholder "Your API Client ID"
                                                prop.value model.ComdirectClientIdInput
                                                prop.onChange (UpdateComdirectClientIdInput >> dispatch)
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium"; prop.text "Client Secret" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-100 font-mono"
                                                prop.type'.password
                                                prop.placeholder "Your API Client Secret"
                                                prop.value model.ComdirectClientSecretInput
                                                prop.onChange (UpdateComdirectClientSecretInput >> dispatch)
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Login Credentials
                            Html.div [
                                prop.className "grid gap-4 sm:grid-cols-2"
                                prop.children [
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium"; prop.text "Username (Zugangsnummer)" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-100"
                                                prop.type'.text
                                                prop.placeholder "Your access number"
                                                prop.value model.ComdirectUsernameInput
                                                prop.onChange (UpdateComdirectUsernameInput >> dispatch)
                                            ]
                                        ]
                                    ]
                                    Html.div [
                                        prop.className "form-control w-full"
                                        prop.children [
                                            Html.label [
                                                prop.className "label py-1"
                                                prop.children [
                                                    Html.span [ prop.className "label-text font-medium"; prop.text "PIN" ]
                                                ]
                                            ]
                                            Html.input [
                                                prop.className "input input-bordered w-full bg-base-100 font-mono"
                                                prop.type'.password
                                                prop.placeholder "Your Comdirect PIN"
                                                prop.value model.ComdirectPasswordInput
                                                prop.onChange (UpdateComdirectPasswordInput >> dispatch)
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Account ID (optional)
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label py-1"
                                        prop.children [
                                            Html.span [ prop.className "label-text font-medium"; prop.text "Account ID" ]
                                            Html.span [ prop.className "label-text-alt badge badge-ghost badge-sm"; prop.text "Optional" ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "input input-bordered w-full bg-base-100"
                                        prop.type'.text
                                        prop.placeholder "Leave empty to use default account"
                                        prop.value model.ComdirectAccountIdInput
                                        prop.onChange (UpdateComdirectAccountIdInput >> dispatch)
                                    ]
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [
                                                prop.className "label-text-alt text-base-content/50"
                                                prop.text "Only needed if you have multiple accounts"
                                            ]
                                        ]
                                    ]
                                ]
                            ]

                            // Save button
                            Html.button [
                                prop.className "btn btn-primary gap-2"
                                prop.onClick (fun _ -> dispatch SaveComdirectCredentials)
                                prop.children [
                                    Html.span [ prop.text "💾" ]
                                    Html.span [ prop.text "Save Credentials" ]
                                ]
                            ]

                            // Info tip
                            Html.div [
                                prop.className "flex items-start gap-3 p-3 bg-info/5 border border-info/10 rounded-lg"
                                prop.children [
                                    Html.span [
                                        prop.className "text-xl flex-shrink-0"
                                        prop.text "ℹ️"
                                    ]
                                    Html.p [
                                        prop.className "text-sm text-base-content/70"
                                        prop.children [
                                            Html.text "You need a Comdirect API access. Visit "
                                            Html.a [
                                                prop.href "https://www.comdirect.de/cms/kontakt-zugaenge-api.html"
                                                prop.target "_blank"
                                                prop.className "link link-info inline-flex items-center gap-1"
                                                prop.children [
                                                    Html.span [ prop.text "Comdirect API" ]
                                                    externalLinkIcon
                                                ]
                                            ]
                                            Html.text " to request access."
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

// ============================================
// Sync Settings Card
// ============================================

let private syncSettingsCard (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-lg animate-slide-up"
        prop.children [
            Html.div [
                prop.className "card-body p-5 md:p-6"
                prop.children [
                    sectionHeader syncIcon "Sync Settings" "Configure how transactions are fetched" true

                    Html.div [
                        prop.className "space-y-6"
                        prop.children [
                            // Days to fetch with visual slider
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.div [
                                        prop.className "flex items-center justify-between mb-2"
                                        prop.children [
                                            Html.label [
                                                prop.className "label-text font-medium"
                                                prop.text "Days to Fetch"
                                            ]
                                            Html.span [
                                                prop.className "text-2xl font-bold font-mono text-primary"
                                                prop.text $"{model.SyncDaysInput}"
                                            ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "range range-primary range-lg"
                                        prop.type'.range
                                        prop.min 7
                                        prop.max 90
                                        prop.step 1
                                        prop.value model.SyncDaysInput
                                        prop.onChange (fun (value: int) -> dispatch (UpdateSyncDaysInput value))
                                    ]
                                    Html.div [
                                        prop.className "flex justify-between text-xs text-base-content/50 mt-1 px-1"
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
                            Html.button [
                                prop.className "btn btn-primary gap-2"
                                prop.onClick (fun _ -> dispatch SaveSyncSettings)
                                prop.children [
                                    Html.span [ prop.text "💾" ]
                                    Html.span [ prop.text "Save Settings" ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]
    ]

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-6 max-w-3xl mx-auto"
        prop.children [
            // Header
            Html.div [
                prop.className "animate-fade-in"
                prop.children [
                    Html.h1 [
                        prop.className "text-2xl md:text-4xl font-bold"
                        prop.text "Settings"
                    ]
                    Html.p [
                        prop.className "text-base-content/60 mt-1"
                        prop.text "Configure your connections and preferences."
                    ]
                ]
            ]

            // Loading state
            match model.Settings with
            | Loading ->
                Html.div [
                    prop.className "flex flex-col items-center justify-center py-16"
                    prop.children [
                        Html.div [ prop.className "loading loading-spinner loading-lg text-primary" ]
                        Html.p [ prop.className "mt-4 text-base-content/60"; prop.text "Loading settings..." ]
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.children [
                        Html.span [
                            prop.className "text-2xl"
                            prop.text "⚠️"
                        ]
                        Html.span [ prop.text $"Failed to load settings: {error}" ]
                        Html.button [
                            prop.className "btn btn-sm"
                            prop.text "Retry"
                            prop.onClick (fun _ -> dispatch LoadSettings)
                        ]
                    ]
                ]
            | _ ->
                Html.div [
                    prop.className "space-y-6"
                    prop.children [
                        ynabSettingsCard model dispatch
                        comdirectSettingsCard model dispatch
                        syncSettingsCard model dispatch
                    ]
                ]
        ]
    ]
