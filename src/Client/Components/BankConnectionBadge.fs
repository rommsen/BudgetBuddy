module Components.BankConnectionBadge

open Elmish
open Feliz
open Shared.Domain
open Client
open Client.DesignSystem

// Der Stand der Bankverbindung als `Badge`, für Einstellungen und SyncFlow. Persistenter Status
// gehört laut Styleguide in ein Badge, nie in einen Toast.

/// Liest den Stand, lesend und deshalb über `Retry.reading`.
let load (toMsg: Result<BankConnectionStatus, string> -> 'msg) : Cmd<'msg> =
    Cmd.OfAsync.either
        (Retry.reading Api.sync.getBankConnectionStatus)
        ()
        (Ok >> toMsg)
        (fun ex -> Error ex.Message |> toMsg)

let label (status: BankConnectionStatus) : string =
    match status with
    | BankConnected -> "Verbunden"
    | BankChallengePending -> "TAN offen"
    | BankDisconnected -> "Nicht verbunden"

/// Was der Stand für den nächsten Sync bedeutet.
let nextSyncHint (status: BankConnectionStatus) : string =
    match status with
    | BankConnected -> "Nächster Sync ohne TAN"
    | BankChallengePending -> "Push-TAN wartet auf Bestätigung"
    | BankDisconnected -> "Nächster Sync braucht eine TAN"

/// Für einen Stand, den der Client nicht lesen konnte: das ist eine eigene Aussage, keine leere Stelle.
let unreadableHint = "Stand der Bankverbindung nicht lesbar"

let unreadable () : ReactElement = Badge.neutral "Stand unbekannt"

let view (status: BankConnectionStatus) : ReactElement =
    match status with
    | BankConnected -> Badge.success (label status)
    | BankChallengePending -> Badge.orange (label status)
    | BankDisconnected -> Badge.neutral (label status)
