/// Der Pause-Knopf mit dem Satz zur YNAB-Pause und der Fehlermeldung (budgeting-t6p8z, budgeting-r3wq8).
/// Steht im Tab Geld (Modus Zuweisen) und im Tab Target. Was erscheint, sagt `pauseControlFor`.
module Components.Budget.PauseView

open Feliz
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.Pause
open Components.Budget.MonthView
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Wo der Pause-Knopf steht.
type PausePlacement =
    /// Im Modus Zuweisen: der Knopf nimmt dem Feld den Fokus nicht (ADR 0005 Punkt 2), und im vergangenen
    /// Monat bleibt sein Platz frei wie bei Targets anwenden.
    | PauseInMoneyTab
    /// Im Tab Target: im vergangenen Monat kein Platz.
    | PauseInTargetTab

/// Der Knopf "Target pausieren" oder "Pause aufheben", neutral (ADR 0075 Punkt 2). Läuft der eigene
/// Aufruf, ist er `IsBusy`: Spinner und `aria-disabled`, ohne `disabled`, der Fokus bleibt auf ihm.
let private pauseButton
    (placement: PausePlacement)
    (model: Model)
    (category: BudgetCategory)
    (b: PauseButton)
    (invisible: bool)
    (dispatch: Msg -> unit)
    =
    let own = pauseRunningFor model category.Id
    let button =
        Button.view
            { Button.defaultProps with
                Text = b.Label
                Variant = Button.Secondary
                IsBusy = own
                IsDisabled = pauseBusy model && not own
                ClassName = Some $"w-full {TouchTargets.sheetMinHeight}"
                OnClick = fun () -> dispatch (SetTargetPause b.Intent) }
    Html.div [
        prop.custom ("data-testid", "target-pause")
        if invisible then prop.className "invisible"
        prop.children [
            match placement with
            | PauseInMoneyTab -> keepsFocus [ button ]
            | PauseInTargetTab -> button
        ]
    ]

let pauseControlView (placement: PausePlacement) (model: Model) (category: BudgetCategory) (dispatch: Msg -> unit) =
    React.fragment [
        match pauseControlFor model category, placement with
        | NoPauseControl, _
        | PauseSlotFree, PauseInTargetTab -> ()
        | PauseSlotFree, PauseInMoneyTab ->
            pauseButton placement model category { Label = "Target pausieren"; Intent = PauseTarget } true dispatch
        | PauseOffered (offered, note), _ ->
            match note with
            | Some text ->
                Html.p [
                    prop.custom ("data-testid", "target-pause-ynab")
                    prop.className ListRoles.muted
                    prop.text text
                ]
            | None -> ()
            match offered with
            | Some b -> pauseButton placement model category b false dispatch
            | None -> ()
        match pauseError model category.Id with
        | Some message ->
            Html.div [
                prop.custom ("data-testid", "target-pause-error")
                prop.children [ ErrorDisplay.cardCompact message None ]
            ]
        | None -> ()
    ]
