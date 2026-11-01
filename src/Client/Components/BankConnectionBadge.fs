module Components.BankConnectionBadge

open Feliz
open Shared.Domain
open Client.DesignSystem

// Der Stand der Bankverbindung als `Badge`, für Einstellungen und SyncFlow. Persistenter Status
// gehört laut Styleguide in ein Badge, nie in einen Toast.

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

let view (status: BankConnectionStatus) : ReactElement =
    match status with
    | BankConnected -> Badge.success (label status)
    | BankChallengePending -> Badge.orange (label status)
    | BankDisconnected -> Badge.neutral (label status)
