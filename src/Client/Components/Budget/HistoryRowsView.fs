/// Bausteine der Bewegungszeilen, geteilt vom Tab Historie (`InspectorView`) und der allgemeinen Historie
/// (`HistoryView`, budgeting-c6vn4 A9): Stand, Betrag, Fehlgrund und der Satz zu YNAB.
module Components.Budget.HistoryRowsView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Inspector
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Der Stand einer Bewegung: "geschrieben" schlicht, alle Badges klein (ADR 0075: Gelb statt Pink auf
/// der Budget-Seite).
let historyStatusView (status: HistoryStatus) =
    let text = historyStatusText status
    match status with
    | HistoryWritten -> Html.span [ prop.className ListRoles.muted; prop.text text ]
    | HistoryFailed _ -> Badge.errorSmall text
    | HistoryChangedInYnab
    // Gelb wie "offen, kein Fehler" (ADR 0075): das Target steht, nur das Datum fehlt (budgeting-g2ty9, X7).
    | HistoryPartlyWritten _ -> Badge.noticeSmall text
    | HistoryOpen
    | HistoryReplacedInBb
    | HistoryWithdrawnInBb -> Badge.neutralSmall text

/// Der Betrag einer Bewegungszeile, wie `historyChangeAmount` ihn liefert.
let historyChangeView (change: Money) =
    let text, amountClass = historyChangeAmount change
    Html.span [ prop.className amountClass; prop.text text ]

/// Der Grund eines Fehlschlags neben dem Badge. Er kürzt mit Ellipse und steht ganz im `title`.
let historyFailureView (status: HistoryStatus) =
    match historyFailureReason status with
    | Some reason ->
        Html.span [
            prop.custom ("data-testid", "history-failure-reason")
            prop.className $"{ListRoles.secondary} truncate min-w-0"
            prop.title reason
            prop.text reason
        ]
    | None -> Html.none

/// Der Grund eines Fehlschlags in einer eigenen Zeile, auf bis zu zwei Zeilen umbrochen
/// (budgeting-c6vn4, A6): in den aufgeklappten Einzelzeilen der allgemeinen Historie, wo der Grund
/// sonst am Handy auf wenige Zeichen schrumpft.
let historyFailureWrapped (status: HistoryStatus) =
    match historyFailureReason status with
    | Some reason ->
        Html.p [
            prop.custom ("data-testid", "history-failure-reason")
            prop.className $"{ListRoles.secondary} line-clamp-2 mt-0.5"
            prop.title reason
            prop.text reason
        ]
    | None -> Html.none
