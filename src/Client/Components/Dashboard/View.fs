module Components.Dashboard.View

open Feliz
open Components.Dashboard.Types
open Types
open Shared.Domain
open Client.DesignSystem

// ============================================
// Helper Functions
// ============================================

let private formatDate (date: System.DateTime) =
    date.ToString("dd.MM.yyyy HH:mm")

let private formatSyncSummary (session: SyncSession) =
    let parts = [
        $"{session.TransactionCount} Transaktionen geholt"
        if session.ImportedCount > 0 then
            $"{session.ImportedCount} importiert"
        if session.SkippedCount > 0 then
            $"{session.SkippedCount} übersprungen"
    ]
    String.concat " · " parts

// ============================================
// Warning Alert Component
// ============================================

let private warningAlert (message: string) (linkText: string) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "flex items-center gap-3 p-4 rounded-xl bg-neon-orange/10 border border-neon-orange/30 animate-fade-in max-w-lg mx-auto"
        prop.children [
            Icons.warning Icons.MD Icons.NeonOrange
            Html.div [
                prop.className "flex-1"
                prop.children [
                    Html.span [
                        prop.className "text-base-content text-sm md:text-base"
                        prop.text message
                    ]
                ]
            ]
            Button.view {
                Button.defaultProps with
                    Text = linkText
                    OnClick = onNavigateToSettings
                    Variant = Button.Secondary
                    Size = Button.Small
            }
        ]
    ]

// ============================================
// Last Sync Info
// ============================================

let private lastSyncInfo (model: Model) =
    Html.div [
        prop.className "text-center text-base-content/50 text-sm md:text-base"
        prop.children [
            match model.LastSession with
            | NotAsked | Loading ->
                Html.span [
                    prop.className "inline-flex items-center gap-2"
                    prop.children [
                        Loading.spinner Loading.SM Loading.Default
                        Html.text "Lade..."
                    ]
                ]

            | Success (Some session) ->
                Html.div [
                    prop.className "space-y-1"
                    prop.children [
                        Html.p [
                            prop.text $"Letzter Sync: {formatDate session.StartedAt}"
                        ]
                        Html.p [
                            prop.className "text-base-content/40 text-xs md:text-sm"
                            prop.text (formatSyncSummary session)
                        ]
                    ]
                ]

            | Success None ->
                Html.p [
                    prop.text "Noch kein Sync durchgeführt"
                ]

            | Failure _ ->
                Html.p [
                    prop.className "text-neon-red/70"
                    prop.text "Fehler beim Laden"
                ]
        ]
    ]

// ============================================
// Main Sync Button
// ============================================

let private syncButton (onNavigateToSync: unit -> unit) =
    Button.heroWithIcon "Start Sync" (Icons.sync Icons.MD Icons.Primary) onNavigateToSync

// ============================================
// Main View
// ============================================

let view (model: Model) (dispatch: Msg -> unit) (onNavigateToSync: unit -> unit) (onNavigateToSettings: unit -> unit) =
    Html.div [
        prop.className "flex flex-col items-center justify-center min-h-[60vh] gap-8"
        prop.children [
            // Configuration warnings at the top
            match model.Settings with
            | Success settings ->
                if settings.Ynab.IsNone then
                    warningAlert "YNAB ist nicht konfiguriert." "Einrichten" onNavigateToSettings
                elif settings.Comdirect.IsNone then
                    warningAlert "Comdirect ist nicht konfiguriert." "Einrichten" onNavigateToSettings
                else
                    Html.none
            | _ -> Html.none

            // Main content - centered
            Html.div [
                prop.className "flex flex-col items-center gap-6"
                prop.children [
                    // Big sync button
                    syncButton onNavigateToSync

                    // Last sync info below button
                    lastSyncInfo model
                ]
            ]
        ]
    ]
