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

/// Ein Betrag im Dialog ohne Währungszeichen, in Mono, wie die Skizze.
let private dialogAmount (testId: string option) (m: Money) =
    Html.span [
        match testId with
        | Some id -> prop.custom ("data-testid", id)
        | None -> ()
        prop.className "font-mono tabular-nums"
        prop.text (Money.formatAmountText false false m.Amount m.Currency)
    ]

/// Eine Zeile der Vorschau (Skizze Zeile 186): Checkbox links vor dem Namen, der Betrag, den die Zeile zuweist, rechts.
/// Die Checkbox ist die native der Skizze (`Input.checkboxNative`). Am Handy 48 px hoch, ab md 40 px wie die Skizze.
let private previewLine (shown: PreviewShown) (dispatch: Msg -> unit) (l: TargetLine) =
    Html.label [
        prop.key (string l.CategoryId)
        prop.custom ("data-testid", "targets-line")
        prop.className "flex items-center gap-2.5 min-h-12 md:min-h-10 shrink-0 text-sm text-text-primary border-b border-border-default cursor-pointer"
        prop.children [
            Input.checkboxNative (lineChosen shown l.CategoryId) (fun _ -> dispatch (ToggleTargetLine l.CategoryId))
            Html.span [ prop.className "flex-1 min-w-0 truncate"; prop.text l.CategoryName ]
            dialogAmount None l.Underfunded
        ]
    ]

/// "Summe" und "Zu verteilen danach" unter den Zeilen (Skizze Zeile 188 und 189). Beide folgen der Auswahl.
let private summary (preview: TargetPreview) =
    Html.div [
        prop.custom ("data-testid", "targets-summary")
        prop.className "flex flex-col shrink-0"
        prop.children [
            Html.div [
                prop.className $"flex items-center justify-between gap-3 min-h-7 {FontSizes.compact} font-semibold text-text-primary"
                prop.children [ Html.span [ prop.text "Summe" ]; dialogAmount (Some "targets-total") preview.Total ]
            ]
            Html.div [
                prop.className $"flex items-center justify-between gap-3 min-h-7 {FontSizes.compact} text-text-secondary"
                prop.children [ Html.span [ prop.text "Zu verteilen danach" ]; dialogAmount (Some "targets-after") preview.ToBeBudgetedAfter ]
            ]
        ]
    ]

/// Zwei gleich breite Knöpfe nebeneinander (Skizze Zeile 190), neutral nach ADR 0075.
let private buttonPair (left: ReactElement) (right: ReactElement) =
    Html.div [
        prop.className "grid grid-cols-2 gap-2 shrink-0"
        prop.children [ left; right ]
    ]

let private wideButton (text: string) (disabled: bool) (onClick: unit -> unit) =
    Button.view
        { Button.defaultProps with
            Text = text
            Variant = Button.Secondary
            IsDisabled = disabled
            OnClick = onClick
            ClassName = Some "w-full" }

let private previewBody (order: System.Guid list) (model: Model) (shown: PreviewShown) (dispatch: Msg -> unit) =
    let selected = selectedPreview shown
    [ if shown.Changed then
          Html.div [
              prop.custom ("data-testid", "targets-changed")
              prop.className "shrink-0"
              prop.children [
                  ErrorDisplay.notice "Stand hat sich geändert. Die Vorschau ist neu geladen, es wurde nichts geschrieben."
              ]
          ]
      if List.isEmpty shown.Shown.Lines then
          Html.p [
              prop.custom ("data-testid", "targets-empty")
              prop.className "text-sm text-text-secondary"
              prop.text "Keine Kategorie hat einen Fehlbetrag. Es gibt nichts anzuwenden."
          ]
      else
          // Lange Listen scrollen hier; Summe und Knöpfe bleiben ohne Scrollen erreichbar.
          Html.div [
              prop.custom ("data-testid", "targets-lines")
              prop.className "flex flex-col min-h-0 overflow-y-auto"
              prop.children (orderedPreviewLines order shown.Shown.Lines |> List.map (previewLine shown dispatch))
          ]
      summary selected
      buttonPair
          (wideButton "Abbrechen" false (fun () -> dispatch CloseTargets))
          (wideButton "Anwenden" (not (canApplyTargets shown && canStartMove model)) (fun () -> dispatch ConfirmTargets)) ]

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
    [ Html.div [
          prop.className "flex flex-col gap-2 shrink-0"
          prop.children [
              Html.p [
                  prop.custom ("data-testid", "targets-count")
                  prop.className "text-sm text-text-primary"
                  prop.text $"{writtenCount report} von {total} angewendet"
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
      Html.div [
          prop.className "flex flex-col min-h-0 overflow-y-auto"
          prop.children (reportLines report |> List.map outcomeLine)
      ] ]

/// Die Karte des Dialogs wie die Skizze (Zeile 184): Titel "Targets anwenden", darunter der Inhalt, Fläche
/// `bg-elevated`, Innenabstand 18 px, Abstand 10 px. Der Rahmen ist `Modal`, am Handy unten wie heute.
let private dialogCard (close: unit -> unit) (children: ReactElement list) =
    Modal.view
        { Modal.defaultProps with
            IsOpen = true
            Size = Modal.Medium
            ShowCloseButton = false
            OnClose = close }
        [ Html.div [
              prop.custom ("data-testid", "targets-dialog")
              prop.className "flex flex-col gap-2.5 p-[18px] min-h-0 flex-1 bg-surface-elevated rounded-xl"
              prop.children [
                  Html.div [
                      prop.role "heading"
                      prop.ariaLevel 2
                      prop.className "text-base font-semibold text-text-primary shrink-0"
                      prop.text "Targets anwenden"
                  ]
                  yield! children
              ]
          ] ]

let targetsDialog (model: Model) (dispatch: Msg -> unit) =
    let close = fun () -> dispatch CloseTargets
    let card = dialogCard close
    let footer (children: ReactElement list) =
        Html.div [ prop.className "flex flex-wrap justify-end gap-2 shrink-0"; prop.children children ]
    match model.Targets with
    | TargetsClosed -> Html.none
    | PreviewLoading ->
        card [
            Loading.centered "Lade Vorschau ..."
            // Derselbe Ausweg wie in der stehenden Vorschau; Anwenden wartet auf sie, die Knöpfe springen dann nicht.
            buttonPair (wideButton "Abbrechen" false close) (wideButton "Anwenden" true ignore)
        ]
    | PreviewFailed message ->
        card [
            ErrorDisplay.card message (Some (fun () -> dispatch OpenTargets))
            footer [ Button.ghost "Schließen" close ]
        ]
    | PreviewReady shown -> card (previewBody (categoryOrder model) model shown dispatch)
    | Applying shown ->
        card [ summary (selectedPreview shown); Loading.centered "Schreibe nach YNAB ..." ]
    | ApplyFailed (preview, message) ->
        card [
            summary preview
            ErrorDisplay.cardCompact $"{message} Ob etwas geschrieben wurde, ist unklar. Beim Schließen wird der Monat neu gelesen." None
            footer [ Button.secondary "Schließen" close ]
        ]
    | ApplyDone applied ->
        card [
            yield! doneBody applied.Sent applied.Report applied.RetryError
            footer [
                Button.ghost "Schließen" close
                if failedCount applied.Report > 0 then
                    Button.view
                        { Button.defaultProps with
                            Text = "Fehlgeschlagene erneut senden"
                            Variant = Button.Primary
                            IsLoading = applied.Retrying
                            IsDisabled = not (canStartMove model)
                            OnClick = fun () -> dispatch RetryFailed }
            ]
        ]
