module Components.Fetch.Types

open Shared.Domain
open Types

// „Abrufen“ (banking-import-d0fey, ADR 0093): ein Sheet über der aktuellen Seite. Ein Abruf bucht neue
// Umsätze sofort ins Kassenbuch und nach YNAB; das Sheet zeigt Push-TAN, Abruf und Ergebnis.

/// Nachlesen nach verlorener Antwort von `startSync` oder `confirmTan`. Es endet spätestens, wenn
/// `ReadsLeft` aufgebraucht ist.
type StartRecovery = {
    Reason: string
    ReadsLeft: int
    /// Ein Lesen ist schon geplant. Eine zweite Lesantwort startet dann keine zweite Kette.
    ReadPlanned: bool
    /// Die Sitzung, die vor dem Start auf dem Server lag. Eine beendete Sitzung mit dieser Id ist
    /// nicht das Ergebnis des verlorenen Aufrufs.
    Before: SyncSessionId option
}

/// Wo ein Abruf steht. Solange er nicht `FetchIdle` ist, löst „Abrufen“ nichts aus und ein zweiter Tipp
/// keinen zweiten `startSync` oder `confirmTan`, sonst kommt womöglich eine zweite Push-TAN (ADR 0071:
/// keine Challenge ohne Roman).
type FetchPhase =
    | FetchIdle
    /// „Abrufen“ kam als Auftrag: die Sitzung wird zuerst gelesen, gestartet wird erst bei keiner oder
    /// einer beendeten Sitzung.
    | FetchOrdered
    /// `startSync` ist unterwegs. Trägt die Sitzung, die vor dem Start auf dem Server lag.
    | FetchStarting of before: SyncSessionId option
    /// „Ich habe bestätigt“: `confirmTan` ist unterwegs.
    | FetchConfirmingTan
    /// Die Antwort von `startSync` oder `confirmTan` ging verloren, das Sheet liest nach.
    | FetchRecovering of StartRecovery

type Model = {
    IsOpen: bool
    Session: RemoteData<SyncSession option>
    /// Stand der lesenden Abrufe: nur die Antwort zum aktuellen Stand zählt.
    SessionReadVersion: int
    Phase: FetchPhase
}

let init : Model = {
    IsOpen = false
    Session = NotAsked
    SessionReadVersion = 0
    Phase = FetchIdle
}

type Msg =
    /// „Abrufen“: das Sheet öffnet, gelesen wird die Sitzung, gestartet nur ohne laufende.
    | Opened
    | Closed
    | SessionReadAnswered of version: int * Result<SyncSession option, string>
    | SyncStarted of Result<SyncStart, SyncError>
    /// Die Antwort von `startSync` ging verloren. Der Server kann trotzdem weit sein.
    | SyncStartInterrupted of string
    /// Die Pause vor dem nächsten Lesen ist um. Trägt den Stand von `ReadsLeft` beim Planen.
    | InterruptedStartReadDue of int
    | ConfirmTan
    | TanConfirmed of Result<SyncSession, SyncError>
    | TanConfirmInterrupted of string
    | CancelSync
    | SyncCancelled of Result<unit, SyncError>
    /// „Jetzt durchgehen“.
    | ReviewRequested

type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    /// Ein Abruf ist beendet: Zu tun, Zähler, Importzeile und eine offene Kassenbuch-Liste laden neu. War das
    /// Sheet zu, trägt er den Satz für einen Toast mit dem Ergebnis.
    | FetchFinished of resultToast: string option
    /// „Jetzt durchgehen“: bis ledger-v6s7c das Kassenbuch über alle Konten mit Filter „Zu prüfen“.
    | OpenReview

// ============================================
// Was das Sheet zeigt (rein, Doktrin Punkt 1)
// ============================================

type SheetScreen =
    | TanScreen
    | RunningScreen
    | DoneScreen of FetchCounts
    | ErrorScreen of string

let sheetScreen (model: Model) : SheetScreen =
    match model.Session with
    | Failure message -> ErrorScreen message
    | NotAsked | Loading -> RunningScreen
    | _ when model.Phase <> FetchIdle -> RunningScreen
    | Success None -> RunningScreen
    | Success (Some session) ->
        match session.Status with
        | AwaitingTan -> TanScreen
        | AwaitingBankAuth | FetchingTransactions | ImportingToYnab -> RunningScreen
        | Completed -> DoneScreen session.Counts
        | Failed message -> ErrorScreen message

/// „<n> neue Buchung(en)“ oder „Nichts Neues“, der Kopf des Ergebnisses.
let resultTitle (counts: FetchCounts) : string =
    match counts.NewEntries with
    | 0 -> "Nichts Neues"
    | 1 -> "1 neue Buchung"
    | n -> $"{n} neue Buchungen"

/// Der Satz unter dem Kopf des Ergebnisses: Teile mit 0 entfallen, Einzahl und Mehrzahl je Teil.
/// Ohne neue Buchung steht der feste Satz des Prototyps da. Umsätze mit Datum in der Zukunft bucht der
/// Abruf noch nicht, ein eigener Satz nennt sie (dann auch ohne neue Buchung statt des festen Satzes).
let resultSentence (counts: FetchCounts) : string =
    let later =
        match counts.FutureDated with
        | 0 -> None
        | 1 -> Some "1 mit Datum in der Zukunft folgt beim nächsten Abruf."
        | n -> Some $"{n} mit Datum in der Zukunft folgen beim nächsten Abruf."
    if counts.NewEntries = 0 then
        later |> Option.defaultValue "Seit dem letzten Abruf gibt es keine neuen Umsätze."
    else
        let part (n: int) (text: string) = if n > 0 then Some $"{n} {text}" else None
        let sentence (parts: string option list) =
            match List.choose id parts with
            | [] -> None
            | items -> Some ((String.concat ", " items) + ".")
        let first =
            sentence [
                part counts.ByRule "per Regel kategorisiert"
                part counts.ByOrderId "aus Bestellnummer"
                part counts.WithoutCategory "ohne Kategorie"
            ]
        let second =
            sentence [
                part counts.SafeDuplicatesSkipped (if counts.SafeDuplicatesSkipped = 1 then "sicheres Duplikat übersprungen" else "sichere Duplikate übersprungen")
                part counts.Held (
                    if counts.Held = 1 then "unsicheres wartet auf dich und geht erst danach nach YNAB"
                    else "unsichere warten auf dich und gehen erst danach nach YNAB")
            ]
        let third =
            if counts.NotInYnab > 0 then Some $"{counts.NotInYnab} noch nicht in YNAB, sie stehen unter Zu tun."
            else None
        [ first; second; third; later ] |> List.choose id |> String.concat " "

/// Der Erklärsatz unter dem TAN-Hinweis.
let tanExplanation (daysToFetch: int) : string =
    $"Danach ruft BB die Umsätze der letzten {daysToFetch} Tage ab und gleicht sie mit dem Kassenbuch ab."
