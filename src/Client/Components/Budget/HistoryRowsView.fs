/// Bausteine der Bewegungszeilen, geteilt vom Tab Historie (`InspectorView`) und der allgemeinen Historie
/// (`HistoryView`, budgeting-c6vn4 A9): Stand, Betrag und Fehlgrund als Zusatz einer Zeile aus `HistoryList`.
module Components.Budget.HistoryRowsView

open Feliz
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Inspector
open Client.DesignSystem
open Client.DesignSystem.Tokens

/// Der Stand einer Bewegung: "geschrieben" schlicht, alle Badges klein (ADR 0087: Gelb heißt
/// braucht dich, auf jeder Seite).
let historyStatusView (status: HistoryStatus) =
    let text = historyStatusText status
    match status with
    | HistoryWritten -> Html.span [ prop.className ListRoles.muted; prop.text text ]
    | HistoryFailed _ -> Badge.errorSmall text
    | HistoryChangedInYnab
    // Gelb: braucht dich, kein Fehler (ADR 0087): das Target steht, nur das Datum fehlt (budgeting-g2ty9, X7).
    | HistoryPartlyWritten _ -> Badge.warningSmall text
    | HistoryOpen
    | HistoryReplacedInBb
    | HistoryWithdrawnInBb -> Badge.neutralSmall text

/// Der Grund eines Fehlschlags als Teil des Zusatzes einer Zeile (`HistoryList`, budgeting-yarty): neben dem Badge,
/// passt er nicht daneben, in eigener Zeile, auf bis zu zwei Zeilen umbrochen (budgeting-c6vn4, A6), ganz im `title`.
let historyFailureWrapped (status: HistoryStatus) =
    match historyFailureReason status with
    | Some reason ->
        Html.span [
            prop.custom ("data-testid", "history-failure-reason")
            prop.className $"{ListRoles.secondary} line-clamp-2 min-w-0"
            prop.title reason
            prop.text reason
        ]
    | None -> Html.none
