module Components.Settings.View

open Feliz
open Components.Settings.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Settings Section Header
// ============================================

/// Kopf einer Karte. Nach dem Speichern steht „✓ Gespeichert“ an der Stelle der Statusanzeige, bis zur nächsten
/// Aktion (Regel R3, Skizze `.mk-saved`).
let private sectionHeaderLabeled (configuredLabel: string) (title: string) (isConfigured: bool) (saved: string option) =
    Html.div [
        prop.className "flex items-center justify-between gap-3 p-4 border-b border-border-subtle"
        prop.children [
            Html.h2 [
                prop.className "text-sm font-semibold font-display"
                prop.text title
            ]
            match saved with
            | Some text -> Feedback.saved text
            | None ->
                Html.span [
                    prop.className (sprintf "status-label %s" (if isConfigured then "text-success" else "text-attention"))
                    prop.children [
                        Html.span [
                            prop.className (if isConfigured then "status-dot connected" else "status-dot disconnected")
                        ]
                        Html.text (if isConfigured then configuredLabel else "Nicht konfiguriert")
                    ]
                ]
        ]
    ]

let private sectionHeader = sectionHeaderLabeled "Verbunden"

/// Rückmeldung unter einem Feld oder Knopf: „✓ …“ in der Erfolgsrolle oder rot (§5).
let private noteBelow (note: SettingsNote option) =
    match note with
    | Some (SettingsNote.Done text) -> Html.div [ Feedback.saved text ]
    | Some (SettingsNote.Failed text) -> ErrorDisplay.inline' text
    | None -> Html.none

// ============================================
// Read-Only Display Helpers
// ============================================

let private secretStatus (isSet: bool) =
    if isSet then "gesetzt" else "\u2014"

/// Feld für einen Wert, den der Client nicht kennt: ist er gesetzt, darf es leer bleiben.
let private credentialGroup (label: string) (isSet: bool) (input: ReactElement) =
    Input.group {
        Label = label
        Required = not isSet
        Error = None
        HelpText = if isSet then Some "Gesetzt. Leer lassen, um den Wert zu behalten" else None
        Children = input
    }

/// Zeile wie `settingRow`, der Wert ist ein Element.
let private settingElementRow (flashed: bool) (label: string) (description: string) (value: ReactElement) =
    Html.div [
        // Nach dem Speichern leuchtet die Zeile kurz.
        prop.className ("setting-row" + (if flashed then " rounded-lg " + Feedback.flash else ""))
        prop.children [
            Html.div [
                prop.className "setting-info"
                prop.children [
                    Html.div [ prop.className "setting-label"; prop.text label ]
                    Html.div [ prop.className "setting-description"; prop.text description ]
                ]
            ]
            value
        ]
    ]

let private settingFlashRow (flashed: bool) (label: string) (description: string) (value: string) =
    Html.div [
        // Nach dem Speichern leuchtet die Zeile kurz.
        prop.className ("setting-row" + (if flashed then " rounded-lg " + Feedback.flash else ""))
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

let private settingRow = settingFlashRow false

/// Eine Zeile im Auswahlfeld.
let private selectOption (value: string) (label: string) : Input.SelectOption =
    { Value = value; Label = label; Disabled = false }

/// Wie `credentialGroup`, mit dem Fehler eines gescheiterten Speicherns unter dem Feld.
let private credentialGroupWithError (label: string) (isSet: bool) (error: string option) (input: ReactElement) =
    Input.group {
        Label = label
        Required = not isSet
        Error = error
        HelpText = if isSet then Some "Gesetzt. Leer lassen, um den Wert zu behalten" else None
        Children = input
    }

/// Ein Auswahlfeld, das beim Ändern speichert (§2): darunter „✓ Gespeichert“ oder rot der Fehler. Es zeigt immer
/// den gespeicherten Wert, nach einer Ablehnung also wieder den alten.
let private selfSavingField
    (model: Model)
    (field: SettingsField)
    (label: string)
    (value: string)
    (onSelect: string -> unit)
    (options: Input.SelectOption list)
    =
    let error = faultAt (Field field) model
    Html.div [
        prop.className "space-y-1"
        prop.children [
            Input.groupWithError label error (
                Input.select {
                    Input.selectDefaults with
                        Value = value
                        OnChange = fun v -> if not (System.String.IsNullOrWhiteSpace v) then onSelect v
                        Options = options
                        State = (match error with Some message -> Input.Error message | None -> Input.Normal)
                        ClassName = Some "w-full"
                })
            match doneAt (Field field) model with
            | Some text -> Feedback.saved text
            | None -> ()
        ]
    ]

// ============================================
// YNAB Settings Card
// ============================================

/// Beschriftung eines Kontos in der Konto-Auswahl, etwa "Bar (1.234,50 €)" (ledger-vq2g8).
/// Vorher englisch über `sprintf "%.2f"` als "Bar (1234.50 EUR)".
let accountOptionLabel (account: YnabAccount) : string =
    $"{account.Name} ({Money.formatAmountText false true account.Balance.Amount account.Balance.Currency})"

let private ynabSettingsCard (model: Model) (dispatch: Msg -> unit) =
    let isConfigured =
        match model.Settings with
        | Success s -> s.Ynab.IsSome
        | _ -> false

    let isEditing = model.EditingSection = Some YnabSection

    Card.standard [
        sectionHeader "YNAB Verbindung" isConfigured (doneAt (CardHead YnabSection) model)

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
                            settingRow "Quick-Add-Konto" "Vorbelegt bei „+ Buchung“ ohne Kontofilter" quickAddAccountName
                            settingFlashRow
                                (model.Flash = Some (SectionRow YnabSection))
                                "API Token"
                                "Persönlicher Access Token"
                                (secretStatus ynab.HasToken)

                            Html.div [
                                prop.className "flex gap-2 pt-4 mt-2 border-t border-border-subtle"
                                prop.children [
                                    Button.secondary "Bearbeiten" (fun () -> dispatch (StartEditing YnabSection))
                                    Button.secondary "Verbindung testen" (fun () -> dispatch TestYnabConnection)
                                ]
                            ]
                            if model.YnabBudgets = Loading then
                                Html.div [ prop.className "pt-3"; prop.children [ Loading.inlineWithText "Verbindung wird getestet…" ] ]
                            match feedbackAt YnabTestResult model with
                            | Some note -> Html.div [ prop.className "pt-3"; prop.children [ noteBelow (Some note) ] ]
                            | None -> ()
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
                    credentialGroupWithError "Persönlicher Access Token" (ynabTokenIsSet model) (faultAt (FormField YnabSection) model) (
                        Input.password model.YnabTokenInput (UpdateYnabTokenInput >> dispatch) "YNAB-Token eingeben")

                    // Save button with validation
                    Form.submitButton
                        "Token speichern"
                        (fun () -> dispatch SaveYnabToken)
                        false
                        (ynabRequiredFields model)

                    // Link to YNAB
                    // Am Handy 48 px Trefferfläche, Text in voller Kontraststufe (Sweep-Zustand einstellungen-fehler).
                    Html.div [
                        prop.className "text-xs text-text-muted"
                        prop.children [
                            Html.text "Token erhältlich unter "
                            Html.a [
                                prop.href "https://app.youneedabudget.com/settings/developer"
                                prop.target "_blank"
                                prop.className "text-accent hover:underline inline-flex items-center gap-1 min-h-[48px] md:min-h-0"
                                prop.children [
                                    Html.span [ prop.text "YNAB Entwicklereinstellungen" ]
                                    Icons.externalLink Icons.XS Icons.Accent
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
                    noteBelow (feedbackAt YnabTestResult model)

                    // Budget/Account selection (shown after successful test)
                    match model.YnabBudgets with
                    | Loading ->
                        Loading.inlineWithText "Verbindung wird getestet…"

                    | Success budgets when not budgets.IsEmpty ->
                        Html.div [
                            prop.className "space-y-4 pt-4 border-t border-border-subtle"
                            prop.children [
                                // Budget selection
                                Html.div [
                                    prop.className "grid gap-4 sm:grid-cols-2"
                                    prop.children [
                                        selfSavingField model DefaultBudgetField "Standard-Budget" (
                                            match model.Settings with
                                            | Success s ->
                                                s.Ynab
                                                |> Option.bind (fun y -> y.DefaultBudgetId)
                                                |> Option.map (fun (YnabBudgetId id) -> id)
                                                |> Option.defaultValue ""
                                            | _ -> "")
                                            (fun value -> dispatch (SetDefaultBudget (YnabBudgetId value)))
                                            (selectOption "" "Budget wählen…"
                                             :: [ for bwa in budgets do
                                                      let (YnabBudgetId id) = bwa.Budget.Id
                                                      selectOption id bwa.Budget.Name ])

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
                                            let accountSelect (field: SettingsField) (label: string) (currentId: YnabAccountId option) (onSelect: YnabAccountId -> unit) =
                                                selfSavingField model field label (
                                                    currentId
                                                    |> Option.map (fun (YnabAccountId id) -> id.ToString())
                                                    |> Option.defaultValue "")
                                                    (fun value -> onSelect (YnabAccountId (System.Guid.Parse value)))
                                                    (selectOption "" "Konto wählen…"
                                                     :: [ for account in bwa.Accounts do
                                                              let (YnabAccountId id) = account.Id
                                                              selectOption (id.ToString()) (accountOptionLabel account) ])

                                            let currentYnab =
                                                match model.Settings with
                                                | Success s -> s.Ynab
                                                | _ -> None

                                            accountSelect
                                                DefaultAccountField
                                                "Standard-Konto (Bank-Import)"
                                                (currentYnab |> Option.bind (fun y -> y.DefaultAccountId))
                                                (SetDefaultAccount >> dispatch)

                                            accountSelect
                                                QuickAddAccountField
                                                "Quick-Add-Konto (z. B. Bar)"
                                                (currentYnab |> Option.bind (fun y -> y.QuickAddAccountId))
                                                (SetQuickAddAccount >> dispatch)
                                        | _ -> Html.none
                                    ]
                                ]
                            ]
                        ]

                    // Der Grund eines gescheiterten Tests steht unter „Verbindung testen“ (`YnabTestResult`).
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

/// "TAN bestätigt" ist die Hauptaktion, solange kein Formular offen ist. Ist eines offen, trägt
/// dessen Speichern den Akzent, und dieser Knopf wird sekundär (ADR 0087 Punkt 24).
let tanConfirmVariant (editing: SettingsSection option) : Button.ButtonVariant =
    match editing with
    | None -> Button.Primary
    | Some _ -> Button.Secondary

let private tanWaitingBlock (editing: SettingsSection option) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-3"
        prop.children [
            ErrorDisplay.notice "Warte auf die TAN-Bestätigung: bitte bestätige die Push-TAN in deiner Comdirect App."
            Button.view {
                Button.defaultProps with
                    Text = "TAN bestätigt"
                    Variant = tanConfirmVariant editing
                    OnClick = fun () -> dispatch ConfirmComdirectTan
            }
        ]
    ]

/// Stand, Meldung und TAN-Schritt. Steht über Lesen und Bearbeiten: eine offene TAN und eine
/// gescheiterte Trennung gelten in beiden Ansichten.
let private bankConnectionBlock (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-3 px-4 pt-4"
        prop.children [
            match model.BankStatus with
            | Success status ->
                settingElementRow
                    (model.Flash = Some BankRow)
                    "Bankverbindung"
                    (Components.BankConnectionBadge.nextSyncHint status)
                    (Components.BankConnectionBadge.view status)
            | Failure _ ->
                settingElementRow false "Bankverbindung" Components.BankConnectionBadge.unreadableHint (
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            Components.BankConnectionBadge.unreadable ()
                            Button.ghost "Wiederholen" (fun () -> dispatch LoadBankStatus)
                        ]
                    ])
            | NotAsked
            | Loading -> ()

            match model.BankDisconnect with
            | Failure reason -> ErrorDisplay.cardCompact reason None
            | _ -> ()

            // Verbindungstest und TAN gescheitert: der Grund steht hier, in Lesen und Bearbeiten.
            match faultAt BankBlock model with
            | Some reason -> ErrorDisplay.inline' reason
            | None -> ()

            if isTanPending model then
                tanWaitingBlock model.EditingSection dispatch
        ]
    ]

/// Automatisch abrufen (banking-import-t4raw, ADR 0093 Punkt 9): Schalter und Intervall speichern sofort. Fünf und
/// mehr Stufen sind ein Auswahlfeld (§7.5); bei abgeschaltetem Schalter ist es gesperrt, sein Wert bleibt. Eine
/// Ablehnung des Servers steht am Element, das gespeichert hat (§5).
let private autoFetchBlock (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-3 px-4 pb-4"
        prop.children [
            Input.switchRow
                "auto-fetch-switch"
                "Automatisch abrufen"
                "BB ruft neue Umsätze im Hintergrund ab und bucht sie als zu prüfen. Eine Push-TAN fragt es nie an."
                model.AutoFetchEnabledInput
                model.AutoFetchSaving
                (SetAutoFetchEnabled >> dispatch)
            noteBelow (feedbackAt (Field (AutoFetchField AutoFetchSwitch)) model)
            Input.groupWithError "Alle" (autoFetchErrorAt AutoFetchIntervalField model) (
                Input.select {
                    Input.selectDefaults with
                        Value = string model.AutoFetchIntervalInput
                        OnChange = fun value ->
                            match System.Int32.TryParse value with
                            | true, minutes -> dispatch (SetAutoFetchInterval minutes)
                            | false, _ -> ()
                        Options =
                            autoFetchIntervalOptions model.AutoFetchIntervalInput
                            |> List.map (fun (minutes, label) -> { Value = string minutes; Label = label; Disabled = false })
                        State =
                            match autoFetchErrorAt AutoFetchIntervalField model with
                            | Some message -> Input.Error message
                            | None -> Input.Normal
                        Disabled = autoFetchIntervalLocked model
                        ClassName = Some "w-full"
                })
            match doneAt (Field (AutoFetchField AutoFetchIntervalField)) model with
            | Some text -> Feedback.saved text
            | None -> ()
        ]
    ]

/// Sekundärer Knopf, der während des Trennens gesperrt ist.
let private lockableButton (isLocked: bool) (text: string) (onClick: unit -> unit) =
    Button.view {
        Button.defaultProps with
            Text = text
            OnClick = onClick
            Variant = Button.Secondary
            IsDisabled = isLocked
    }

let private comdirectSettingsCard (model: Model) (dispatch: Msg -> unit) =
    let isConfigured =
        match model.Settings with
        | Success s -> s.Comdirect.IsSome
        | _ -> false

    let isEditing = model.EditingSection = Some ComdirectSection
    let isDisconnecting = comdirectActionsLocked model

    Card.standard [
        // Gesetzte Zugangsdaten sind noch keine Bankverbindung, den Stand zeigt das Badge darunter.
        sectionHeaderLabeled "Eingerichtet" "Comdirect Verbindung" isConfigured (doneAt (CardHead ComdirectSection) model)

        if isConfigured then
            bankConnectionBlock model dispatch

        if isConfigured && not isEditing then
            // READ-ONLY DISPLAY
            Html.div [
                prop.className "p-4"
                prop.children [
                    match model.Settings with
                    | Success settings ->
                        match settings.Comdirect with
                        | Some comdirect ->
                            // Nach dem Speichern leuchten die Zugangsdaten, die das Formular geschrieben hat.
                            let flashed = model.Flash = Some (SectionRow ComdirectSection)
                            settingFlashRow flashed "Client-ID" "Comdirect API Client-ID" (secretStatus comdirect.HasClientId)
                            settingFlashRow flashed "Client-Secret" "Comdirect API Client-Secret" (secretStatus comdirect.HasClientSecret)
                            settingFlashRow flashed "Benutzername" "Comdirect-Zugangsnummer" (secretStatus comdirect.HasUsername)
                            settingFlashRow flashed "PIN" "Comdirect-PIN" (secretStatus comdirect.HasPassword)
                            settingRow "Authentifizierung" "OAuth2 + Push-TAN" "\u2014"

                            Html.div [
                                prop.className "flex flex-wrap gap-2 pt-4 mt-2 border-t border-border-subtle"
                                prop.children [
                                    lockableButton isDisconnecting "Bearbeiten" (fun () -> dispatch (StartEditing ComdirectSection))
                                    lockableButton isDisconnecting "Verbindung testen" (fun () -> dispatch TestComdirectConnection)
                                    if canDisconnectBank model then
                                        if isDisconnecting then
                                            Loading.inlineWithText "Bank wird getrennt…"
                                        elif model.ConfirmingBankDisconnect then
                                            // "Abbrechen" steht dort, wo eben "Bank trennen" stand: ein Doppeltipp trifft ihn.
                                            Button.ghost "Abbrechen" (fun () -> dispatch AbortBankDisconnect)
                                            Button.danger "Wirklich trennen" (fun () -> dispatch ConfirmBankDisconnect)
                                        else
                                            Button.danger "Bank trennen" (fun () -> dispatch RequestBankDisconnect)
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
                            credentialGroup "Client-ID" (comdirectClientIdIsSet model) (
                                Input.textSimple model.ComdirectClientIdInput (UpdateComdirectClientIdInput >> dispatch) "Deine API Client-ID")

                            credentialGroup "Client-Secret" (comdirectClientSecretIsSet model) (
                                Input.password model.ComdirectClientSecretInput (UpdateComdirectClientSecretInput >> dispatch) "Dein API Client-Secret")
                        ]
                    ]

                    // Login Credentials row
                    Html.div [
                        prop.className "grid gap-4 sm:grid-cols-2"
                        prop.children [
                            credentialGroup "Benutzername (Zugangsnummer)" (comdirectUsernameIsSet model) (
                                Input.textSimple model.ComdirectUsernameInput (UpdateComdirectUsernameInput >> dispatch) "Deine Zugangsnummer")

                            credentialGroup "PIN" (comdirectPasswordIsSet model) (
                                Input.password model.ComdirectPasswordInput (UpdateComdirectPasswordInput >> dispatch) "Deine Comdirect-PIN")
                        ]
                    ]

                    // Account ID input (required)
                    Input.groupRequired "Konto-ID" (
                        Input.textSimple model.ComdirectAccountIdInput (UpdateComdirectAccountIdInput >> dispatch) "Deine Comdirect Konto-ID"
                    )

                    // Der Server nennt kein Feld (`ComdirectCredentialsInvalid ("general", …)`), deshalb steht ein
                    // abgelehntes Speichern über dem Knopf statt unter einem Feld.
                    match faultAt (FormField ComdirectSection) model with
                    | Some reason -> ErrorDisplay.inline' reason
                    | None -> ()

                    // Save button with validation feedback
                    Form.submitButton
                        "Zugangsdaten speichern"
                        (fun () -> dispatch SaveComdirectCredentials)
                        false
                        (comdirectRequiredFields model)

                    // Info tip
                    Html.div [
                        // Hinweis: Blau (ADR 0087 Punkt 4)
                        prop.className "flex items-start gap-3 p-3 rounded-lg bg-info/5 border border-info/20"
                        prop.children [
                            Icons.info Icons.MD Icons.Info
                            Html.p [
                                prop.className "text-xs md:text-sm text-text-muted"
                                prop.children [
                                    Html.text "Du benötigst Zugang zur Comdirect API. Besuche "
                                    Html.a [
                                        prop.href "https://www.comdirect.de/cms/kontakt-zugaenge-api.html"
                                        prop.target "_blank"
                                        prop.className "text-accent hover:underline inline-flex items-center gap-1"
                                        prop.children [
                                            Html.span [ prop.text "Comdirect API" ]
                                            Icons.externalLink Icons.XS Icons.Accent
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
                            // Bei offener TAN steht der Wartezustand schon über dem Formular.
                            if connectionTestVisible model then
                                match model.ComdirectConnectionValid with
                                | Loading ->
                                    Loading.inlineWithText "Verbindung wird getestet…"
                                | Success _ ->
                                    ErrorDisplay.success "Zugangsdaten geprüft"
                                // Der Grund steht im Block Bankverbindung über dem Formular.
                                | Failure _
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

        // Unter den Aktionen: so verschiebt der Block weder Status noch Knöpfe der Verbindung.
        if isConfigured then
            autoFetchBlock model dispatch
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
        sectionHeader "Sync-Einstellungen" true (doneAt (CardHead SyncSection) model)

        if not isEditing then
            // READ-ONLY DISPLAY
            Html.div [
                prop.className "p-4"
                prop.children [
                    settingFlashRow
                        (model.Flash = Some (SectionRow SyncSection))
                        "Transaktionszeitraum"
                        "Wie weit zurückliegende Transaktionen synchronisiert werden"
                        (sprintf "%d Tage" daysToFetch)

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
                                        prop.className "text-xl md:text-2xl font-bold font-mono text-text-primary"
                                        prop.text $"{model.SyncDaysInput}"
                                    ]
                                ]
                            ]
                            Html.input [
                                // `range`/`range-sm` waren DaisyUI-Klassen (DaisyUI ist
                                // in diesem Projekt nicht geladen, ADR 0031); ohne
                                // Stylesheet dafür taten sie nichts. Der native
                                // Browser-Schieberegler mit `accent-accent`
                                // (Akzentfarbe für Spur/Griff) reicht für diesen
                                // einzigen Slider im Projekt (design-system-fq8dn).
                                prop.className "w-full accent-accent"
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
                            match faultAt (FormField SyncSection) model with
                            | Some reason -> ErrorDisplay.inline' reason
                            | None -> ()
                        ]
                    ]

                    // Save button
                    // Gesperrt, solange „Automatisch abrufen“ speichert (syncSaveLocked).
                    Button.view {
                        Button.defaultProps with
                            Text = "Einstellungen speichern"
                            Variant = Button.Primary
                            Icon = Some (Icons.check Icons.SM Icons.Primary)
                            IsDisabled = syncSaveLocked model
                            OnClick = fun () -> dispatch SaveSyncSettings
                    }

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
// Lena-Flag Card (ledger-xfjcp, ADR 0056)
// ============================================

/// Die gespeicherte Farbe wie in Skizze 34: gefülltes ⚑ (`Icons.flagSolid`) in der Flag-Farbe, daneben der Name, den BB für die Farbe nutzt. Die
/// Farbe steht nur im Symbol, der Text bleibt neutral (ADR 0087 Punkt 8, wie `Badge.lenaMark`).
let private lenaFlagValue (saved: string option) =
    match saved with
    | Some color ->
        Html.span (
            Badge.lenaFlagScope saved
            @ [ prop.custom ("data-testid", "settings-lena-value")
                prop.className "inline-flex items-center gap-1.5 text-sm text-text-primary whitespace-nowrap"
                prop.children [
                    Html.span [
                        prop.role "img"
                        prop.ariaLabel "Flag"
                        prop.className "inline-flex"
                        prop.children [ Icons.flagSolid Icons.Mark Icons.LenaFlag ]
                    ]
                    Html.span [ prop.text (lenaFlagColorLabel color) ]
                ] ]
        )
    | None -> Html.span [ prop.className "setting-value"; prop.text "Nicht gewählt" ]

let private lenaFlagCard (model: Model) (dispatch: Msg -> unit) =
    let isEditing = model.EditingSection = Some LenaSection
    let saved = model.LenaFlag.SavedColor
    let suggestionNote =
        match saved, model.LenaFlag.SuggestedColor with
        | None, Some suggested ->
            Some $"Vorschlag aus dem Spiegel: {lenaFlagColorLabel suggested}. Gilt erst nach dem Speichern."
        | _ -> None

    Card.standard [
        sectionHeader "Kassenbuch: Lena fragen" saved.IsSome (doneAt (CardHead LenaSection) model)

        if not isEditing then
            Html.div [
                prop.className "p-4"
                prop.custom ("data-testid", "settings-lena-card")
                prop.children [
                    settingElementRow
                        (model.Flash = Some (SectionRow LenaSection))
                        "Flag-Farbe für Lena"
                        "Die YNAB-Flag-Farbe, die Sie in YNAB Lena genannt haben"
                        (lenaFlagValue saved)
                    match suggestionNote with
                    | Some note -> Html.p [ prop.className "text-xs text-text-muted pt-2"; prop.text note ]
                    | None -> ()
                    // Ein Zustand der Karte: die gezeigte Farbe ist nicht der gespeicherte Stand.
                    match model.LenaFlagLoadError with
                    | Some reason ->
                        Html.div [
                            prop.className "pt-2"
                            prop.children [ ErrorDisplay.cardCompact reason (Some (fun () -> dispatch LoadLenaFlagSettings)) ]
                        ]
                    | None -> ()

                    Html.div [
                        prop.className "flex gap-2 pt-4 mt-2 border-t border-border-subtle"
                        prop.children [
                            Button.secondary "Bearbeiten" (fun () -> dispatch (StartEditing LenaSection))
                        ]
                    ]
                ]
            ]
        else
            Html.div [
                prop.className "space-y-5 p-4"
                prop.custom ("data-testid", "settings-lena-form")
                prop.children [
                    Input.groupWithError
                        "Flag-Farbe für Lena"
                        (faultAt (FormField LenaSection) model)
                        (Input.selectSimple
                            model.LenaFlagColorInput
                            (fun value -> dispatch (UpdateLenaFlagColorInput value))
                            (("", "Nicht gewählt")
                             :: (ynabFlagColors |> List.map (fun c -> c, lenaFlagColorLabel c))))
                    match suggestionNote with
                    | Some note -> Html.p [ prop.className "text-xs text-text-muted"; prop.text note ]
                    | None -> ()

                    Button.primaryWithIcon "Einstellungen speichern" (Icons.check Icons.SM Icons.Primary) (fun () -> dispatch SaveLenaFlagColor)

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

let private pageHeader (back: ReactElement) (dispatch: Msg -> unit) =
    PageHeader.top
        (Some back)
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

/// `back`: „Zurück“ zur Seite, die vor den Einstellungen offen war (design-system-k6kfw).
let view (back: ReactElement) (model: Model) (dispatch: Msg -> unit) =
    Html.div [
        prop.className "space-y-5 md:space-y-6 max-w-3xl mx-auto"
        prop.children [
            // Header
            pageHeader back dispatch

            // Loading state
            match model.Settings with
            | Loading ->
                Loading.centered "Einstellungen werden geladen…"

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
                        Html.div [
                            prop.className "animate-slide-up"
                            prop.style [ style.custom ("animationDelay", "225ms") ]
                            prop.children [ lenaFlagCard model dispatch ]
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
                                    // Am Handy 48 px Trefferfläche (Sweep-Zustand einstellungen, banking-import-t4raw).
                                    prop.className "inline-flex items-center gap-2 min-h-[48px] md:min-h-0 text-sm text-accent hover:underline"
                                    prop.children [
                                        Icons.externalLink Icons.XS Icons.Accent
                                        Html.span [ prop.text "Styleguide ansehen (Design-System-Galerie)" ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
        ]
    ]
