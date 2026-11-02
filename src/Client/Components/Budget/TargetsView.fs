/// Der Dialog "Targets anwenden" (budgeting-h2zfn).
module Components.Budget.TargetsView

open Feliz
open Fable.Core.JsInterop
open Shared.Domain
open Shared.Budgeting
open Types
open Components.Budget.Types
open Components.Budget.Month
open Components.Budget.Targets
open Components.Budget.Moves
open Components.Budget.Inspector
open Client.DesignSystem
open Client.DesignSystem.Tokens
open Components.Budget.MonthView

/// Summe und Zu verteilen danach stehen oben im Dialog, außerhalb des scrollenden Teils.
let private summary (preview: TargetPreview) =
    Html.div [
        prop.custom ("data-testid", "targets-summary")
        prop.className "flex items-end justify-between gap-4 px-4 md:px-6 py-3 border-b border-border-default"
        prop.children [
            Html.div [
                prop.className "flex flex-col gap-1 min-w-0"
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Summe" ]
                    Html.span [
                        prop.custom ("data-testid", "targets-total")
                        prop.className "font-mono font-medium tabular-nums text-text-primary"
                        prop.text (moneyText preview.Total)
                    ]
                ]
            ]
            Html.div [
                prop.className "flex flex-col gap-1 items-end"
                prop.children [
                    Html.span [ prop.className ListRoles.groupHeader; prop.text "Zu verteilen danach" ]
                    Html.span [
                        prop.custom ("data-testid", "targets-after")
                        prop.children [
                            Money.view
                                { Money.defaultProps with
                                    Amount = preview.ToBeBudgetedAfter.Amount
                                    Currency = preview.ToBeBudgetedAfter.Currency
                                    Size = Money.Large
                                    Glow = Money.NoGlow
                                    ShowSign = false }
                        ]
                    ]
                ]
            ]
        ]
    ]

let private previewLine (l: TargetLine) =
    Html.div [
        prop.key (string l.CategoryId)
        prop.custom ("data-testid", "targets-line")
        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text l.CategoryName ]
                    plainAmount l.NewAssigned
                ]
            ]
            // Bricht um statt abzuschneiden: mit Target passt die Zeile am Handy nicht in eine Zeile.
            Html.p [
                prop.custom ("data-testid", "targets-line-detail")
                prop.className $"{ListRoles.secondary} mt-0.5"
                prop.text (previewDetailText l)
            ]
        ]
    ]

let private skippedLines (skipped: SkippedLine list) =
    if List.isEmpty skipped then
        Html.none
    else
        Html.div [
            prop.custom ("data-testid", "targets-skipped")
            prop.children [
                Html.h3 [
                    prop.className $"{ListRoles.groupHeader} px-4 pt-4 pb-1"
                    prop.text "Übersprungen (pausiert)"
                ]
                for s in skipped do
                    Html.div [
                        prop.key (string s.CategoryId)
                        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
                        prop.children [
                            Html.div [
                                prop.className ListRoles.mobileLine1
                                prop.children [
                                    Html.span [ prop.className $"{ListRoles.secondary} truncate min-w-0"; prop.text s.CategoryName ]
                                    Badge.neutral "pausiert"
                                ]
                            ]
                        ]
                    ]
            ]
        ]

let private previewBody (order: System.Guid list) (preview: TargetPreview) (changed: bool) =
    Modal.bodyNoPadding [
        if changed then
            Html.div [
                prop.custom ("data-testid", "targets-changed")
                prop.className "p-4"
                prop.children [
                    ErrorDisplay.notice "Stand hat sich geändert. Die Vorschau ist neu geladen, es wurde nichts geschrieben."
                ]
            ]
        if List.isEmpty preview.Lines then
            Html.p [
                prop.custom ("data-testid", "targets-empty")
                prop.className "p-4 text-sm text-text-secondary"
                prop.text "Keine Kategorie hat einen Fehlbetrag. Es gibt nichts anzuwenden."
            ]
        else
            Html.div [ prop.children (orderedPreviewLines order preview.Lines |> List.map previewLine) ]
        skippedLines preview.Skipped
    ]

let private outcomeLine (l: ApplyLineResult) =
    let detail, badge =
        match l.Outcome with
        | Written -> $"{moneyText l.OldAssigned} auf {moneyText l.NewAssigned}", Badge.success "geschrieben"
        | Failed reason -> reason, Badge.error "fehlgeschlagen"
        | ChangedInYnab -> "YNAB steht anders als erwartet, nichts überschrieben", Badge.notice "in YNAB geändert"
    Html.div [
        prop.key (string l.CategoryId)
        prop.custom ("data-testid", "targets-result-line")
        prop.className $"{ListRoles.mobileRowPadding} border-b border-border-subtle"
        prop.children [
            Html.div [
                prop.className ListRoles.mobileLine1
                prop.children [
                    Html.span [ prop.className $"{ListRoles.primary} truncate min-w-0"; prop.text l.CategoryName ]
                    badge
                ]
            ]
            Html.div [
                prop.className ListRoles.mobileLine2
                prop.children [ Html.span [ prop.className $"{ListRoles.muted} truncate"; prop.text detail ] ]
            ]
        ]
    ]

let private doneBody (preview: TargetPreview) (report: ApplyReport) (retryError: string option) =
    let total = report.Lines.Length
    // Geschriebene stehen nach den Problemfällen, damit Fehlgeschlagene oben sichtbar sind.
    let ordered =
        report.Lines |> List.sortBy (fun l -> match l.Outcome with Written -> 1 | _ -> 0)
    Modal.bodyNoPadding [
        Html.div [
            prop.className "p-4 flex flex-col gap-2"
            prop.children [
                Html.p [
                    prop.custom ("data-testid", "targets-count")
                    prop.className "text-sm text-text-primary"
                    prop.text $"{writtenCount report} von {total} übernommen"
                ]
                match report.ActualToBeBudgeted with
                | Some actual ->
                    Html.div [
                        prop.custom ("data-testid", "targets-actual")
                        prop.className "flex items-center gap-2 text-sm text-text-secondary"
                        prop.children [
                            Html.span [ prop.text "Zu verteilen jetzt" ]
                            Money.view
                                { Money.defaultProps with
                                    Amount = actual.Amount
                                    Currency = actual.Currency
                                    Size = Money.Medium
                                    Glow = Money.NoGlow
                                    ShowSign = false }
                        ]
                    ]
                | None ->
                    Html.p [
                        prop.custom ("data-testid", "targets-actual")
                        prop.className "text-sm text-text-secondary"
                        prop.text "Zu verteilen konnte nach dem Lauf nicht gelesen werden."
                    ]
                match actualCheck preview report with
                | DiffersFromPreview _ ->
                    Html.div [
                        prop.custom ("data-testid", "targets-differs")
                        prop.children [
                            ErrorDisplay.notice $"Zu verteilen weicht von der Vorschau ab (Vorschau: {moneyText preview.ToBeBudgetedAfter})."
                        ]
                    ]
                | _ -> ()
                match retryError with
                | Some message -> ErrorDisplay.cardCompact message None
                | None -> ()
            ]
        ]
        Html.div [ prop.children (ordered |> List.map outcomeLine) ]
    ]

let targetsDialog (model: Model) (dispatch: Msg -> unit) =
    let close = fun () -> dispatch CloseTargets
    let modal title children =
        Modal.view
            { Modal.defaultProps with
                IsOpen = true
                Title = Some title
                Size = Modal.Large
                OnClose = close }
            children
    let monthTitle =
        match model.Month with
        | Success month -> $"Targets anwenden, {monthLabel month.Month}"
        | _ -> "Targets anwenden"
    match model.Targets with
    | TargetsClosed -> Html.none
    | PreviewLoading ->
        modal monthTitle [ Modal.body [ Loading.centered "Lade Vorschau ..." ] ]
    | PreviewFailed message ->
        modal monthTitle [
            Modal.body [ ErrorDisplay.card message (Some (fun () -> dispatch OpenTargets)) ]
            Modal.footer [ Button.ghost "Schließen" close ]
        ]
    | PreviewReady (preview, changed) ->
        modal monthTitle [
            summary preview
            previewBody (categoryOrder model) preview changed
            Modal.footer [
                Button.ghost "Abbrechen" close
                if not (List.isEmpty preview.Lines) then
                    Button.view
                        { Button.defaultProps with
                            Text = "Übernehmen"
                            Variant = Button.Primary
                            IsDisabled = not (canStartMove model)
                            OnClick = fun () -> dispatch ConfirmTargets }
            ]
        ]
    | Applying preview ->
        modal monthTitle [
            summary preview
            Modal.body [ Loading.centered "Schreibe nach YNAB ..." ]
        ]
    | ApplyFailed (preview, message) ->
        modal monthTitle [
            summary preview
            Modal.body [
                ErrorDisplay.cardCompact $"{message} Ob etwas geschrieben wurde, ist unklar. Beim Schließen wird der Monat neu gelesen." None
            ]
            Modal.footer [ Button.primary "Schließen" close ]
        ]
    | ApplyDone (preview, report, retrying, retryError) ->
        modal monthTitle [
            doneBody preview report retryError
            Modal.footer [
                Button.ghost "Schließen" close
                if failedCount report > 0 then
                    Button.view
                        { Button.defaultProps with
                            Text = "Fehlgeschlagene erneut senden"
                            Variant = Button.Primary
                            IsLoading = retrying
                            IsDisabled = not (canStartMove model)
                            OnClick = fun () -> dispatch RetryFailed }
            ]
        ]
