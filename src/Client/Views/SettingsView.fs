module Views.SettingsView

open Feliz
open State
open Types
open Shared.Domain

// ============================================
// YNAB Settings Card
// ============================================

let private ynabSettingsCard (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body"
                prop.children [
                    Html.h2 [
                        prop.className "card-title"
                        prop.text "YNAB Connection"
                    ]

                    // Token input
                    Html.div [
                        prop.className "form-control w-full"
                        prop.children [
                            Html.label [
                                prop.className "label"
                                prop.children [
                                    Html.span [
                                        prop.className "label-text"
                                        prop.text "Personal Access Token"
                                    ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex gap-2"
                                prop.children [
                                    Html.input [
                                        prop.className "input input-bordered flex-1"
                                        prop.type'.password
                                        prop.placeholder "Enter your YNAB Personal Access Token"
                                        prop.value model.YnabTokenInput
                                        prop.onChange (UpdateYnabTokenInput >> dispatch)
                                    ]
                                    Html.button [
                                        prop.className "btn btn-primary"
                                        prop.text "Save"
                                        prop.onClick (fun _ -> dispatch SaveYnabToken)
                                    ]
                                ]
                            ]
                            Html.label [
                                prop.className "label"
                                prop.children [
                                    Html.span [
                                        prop.className "label-text-alt"
                                        prop.children [
                                            Html.text "Get your token from "
                                            Html.a [
                                                prop.href "https://app.youneedabudget.com/settings/developer"
                                                prop.target "_blank"
                                                prop.className "link link-primary"
                                                prop.text "YNAB Developer Settings"
                                            ]
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Test connection button
                    Html.div [
                        prop.className "mt-4"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-secondary"
                                prop.text "Test Connection"
                                prop.onClick (fun _ -> dispatch TestYnabConnection)
                                prop.disabled (System.String.IsNullOrWhiteSpace(model.YnabTokenInput))
                            ]
                        ]
                    ]

                    // Budget/Account selection (shown after successful test)
                    match model.YnabBudgets with
                    | Loading ->
                        Html.div [
                            prop.className "mt-4 flex items-center gap-2"
                            prop.children [
                                Html.span [ prop.className "loading loading-spinner loading-sm" ]
                                Html.span [ prop.text "Testing connection..." ]
                            ]
                        ]
                    | Success budgets when not budgets.IsEmpty ->
                        Html.div [
                            prop.className "mt-4 space-y-4"
                            prop.children [
                                Html.div [
                                    prop.className "alert alert-success"
                                    prop.text $"Connected! Found {budgets.Length} budget(s)"
                                ]

                                // Budget selection
                                Html.div [
                                    prop.className "form-control w-full"
                                    prop.children [
                                        Html.label [
                                            prop.className "label"
                                            prop.children [
                                                Html.span [
                                                    prop.className "label-text"
                                                    prop.text "Default Budget"
                                                ]
                                            ]
                                        ]
                                        Html.select [
                                            prop.className "select select-bordered w-full"
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
                                                    prop.text "-- Select Budget --"
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
                                                prop.className "label"
                                                prop.children [
                                                    Html.span [
                                                        prop.className "label-text"
                                                        prop.text "Default Account (for imports)"
                                                    ]
                                                ]
                                            ]
                                            Html.select [
                                                prop.className "select select-bordered w-full"
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
                                                        prop.text "-- Select Account --"
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
                    | Failure error ->
                        Html.div [
                            prop.className "mt-4 alert alert-error"
                            prop.text error
                        ]
                    | _ -> Html.none
                ]
            ]
        ]
    ]

// ============================================
// Comdirect Settings Card
// ============================================

let private comdirectSettingsCard (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body"
                prop.children [
                    Html.h2 [
                        prop.className "card-title"
                        prop.text "Comdirect Connection"
                    ]

                    Html.div [
                        prop.className "grid grid-cols-1 md:grid-cols-2 gap-4"
                        prop.children [
                            // Client ID
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [ prop.className "label-text"; prop.text "Client ID" ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "input input-bordered w-full"
                                        prop.type'.text
                                        prop.placeholder "Client ID"
                                        prop.value model.ComdirectClientIdInput
                                        prop.onChange (UpdateComdirectClientIdInput >> dispatch)
                                    ]
                                ]
                            ]

                            // Client Secret
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [ prop.className "label-text"; prop.text "Client Secret" ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "input input-bordered w-full"
                                        prop.type'.password
                                        prop.placeholder "Client Secret"
                                        prop.value model.ComdirectClientSecretInput
                                        prop.onChange (UpdateComdirectClientSecretInput >> dispatch)
                                    ]
                                ]
                            ]

                            // Username
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [ prop.className "label-text"; prop.text "Username (Zugangsnummer)" ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "input input-bordered w-full"
                                        prop.type'.text
                                        prop.placeholder "Your Comdirect access number"
                                        prop.value model.ComdirectUsernameInput
                                        prop.onChange (UpdateComdirectUsernameInput >> dispatch)
                                    ]
                                ]
                            ]

                            // Password
                            Html.div [
                                prop.className "form-control w-full"
                                prop.children [
                                    Html.label [
                                        prop.className "label"
                                        prop.children [
                                            Html.span [ prop.className "label-text"; prop.text "Password (PIN)" ]
                                        ]
                                    ]
                                    Html.input [
                                        prop.className "input input-bordered w-full"
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
                        prop.className "form-control w-full mt-4"
                        prop.children [
                            Html.label [
                                prop.className "label"
                                prop.children [
                                    Html.span [ prop.className "label-text"; prop.text "Account ID (optional)" ]
                                ]
                            ]
                            Html.input [
                                prop.className "input input-bordered w-full"
                                prop.type'.text
                                prop.placeholder "Leave empty to use default account"
                                prop.value model.ComdirectAccountIdInput
                                prop.onChange (UpdateComdirectAccountIdInput >> dispatch)
                            ]
                            Html.label [
                                prop.className "label"
                                prop.children [
                                    Html.span [
                                        prop.className "label-text-alt text-gray-500"
                                        prop.text "Only needed if you have multiple accounts and want to sync a specific one"
                                    ]
                                ]
                            ]
                        ]
                    ]

                    // Save button
                    Html.div [
                        prop.className "mt-4"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "Save Comdirect Credentials"
                                prop.onClick (fun _ -> dispatch SaveComdirectCredentials)
                            ]
                        ]
                    ]

                    // Info
                    Html.div [
                        prop.className "mt-4 alert alert-info"
                        prop.children [
                            Html.div [
                                Html.span [
                                    prop.text "You need a Comdirect API access. Visit "
                                ]
                                Html.a [
                                    prop.href "https://www.comdirect.de/cms/kontakt-zugaenge-api.html"
                                    prop.target "_blank"
                                    prop.className "link"
                                    prop.text "Comdirect API"
                                ]
                                Html.span [
                                    prop.text " to request access."
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
        prop.className "card bg-base-100 shadow-xl"
        prop.children [
            Html.div [
                prop.className "card-body"
                prop.children [
                    Html.h2 [
                        prop.className "card-title"
                        prop.text "Sync Settings"
                    ]

                    // Days to fetch
                    Html.div [
                        prop.className "form-control w-full max-w-xs"
                        prop.children [
                            Html.label [
                                prop.className "label"
                                prop.children [
                                    Html.span [ prop.className "label-text"; prop.text "Days to Fetch" ]
                                    Html.span [ prop.className "label-text-alt"; prop.text $"{model.SyncDaysInput} days" ]
                                ]
                            ]
                            Html.input [
                                prop.className "range range-primary"
                                prop.type'.range
                                prop.min 7
                                prop.max 90
                                prop.step 1
                                prop.value model.SyncDaysInput
                                prop.onChange (fun (value: int) -> dispatch (UpdateSyncDaysInput value))
                            ]
                            Html.div [
                                prop.className "w-full flex justify-between text-xs px-2"
                                prop.children [
                                    Html.span [ prop.text "7" ]
                                    Html.span [ prop.text "30" ]
                                    Html.span [ prop.text "60" ]
                                    Html.span [ prop.text "90" ]
                                ]
                            ]
                        ]
                    ]

                    // Save button
                    Html.div [
                        prop.className "mt-4"
                        prop.children [
                            Html.button [
                                prop.className "btn btn-primary"
                                prop.text "Save Sync Settings"
                                prop.onClick (fun _ -> dispatch SaveSyncSettings)
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
        prop.className "space-y-6"
        prop.children [
            // Header
            Html.h1 [
                prop.className "text-3xl font-bold"
                prop.text "Settings"
            ]

            // Loading state
            match model.Settings with
            | Loading ->
                Html.div [
                    prop.className "flex justify-center p-8"
                    prop.children [
                        Html.span [ prop.className "loading loading-spinner loading-lg" ]
                    ]
                ]
            | Failure error ->
                Html.div [
                    prop.className "alert alert-error"
                    prop.children [
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
