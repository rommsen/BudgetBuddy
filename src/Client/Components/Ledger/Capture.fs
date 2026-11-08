module Components.Ledger.Capture

// "Neue Buchung" im Kassenbuch (ledger-at63w): der Zustand des Formulars und seine reinen
// Funktionen. Die Nachrichten stehen in `Types.fs`, ihre Verarbeitung in `State.fs`, die Ansicht
// in `View.fs` (Panel am PC, Sheet am Handy).

open System
open Shared.Domain
open Types

/// Welche Auswahl aus dem Formular heraus offen ist.
type CapturePicker =
    | CaptureAccountPicker
    | CaptureTargetPicker

/// Die Eingaben des Formulars. Der Betrag bleibt Text, bis "Buchung anlegen" ihn liest.
type CaptureForm = {
    IsOutflow: bool
    AmountText: string
    AccountId: Guid option
    Target: LedgerBookingTarget option
    /// Das Ziel hat Roman selbst gewählt (Auswahl oder Zuletzt-Chip): die Kategorie zum Payee
    /// überschreibt es dann nicht mehr.
    TargetChosen: bool
    Payee: string
    Date: DateTime
    Memo: string
}

/// Das offene Formular samt Vorbelegung, offener Auswahl, laufendem Aufruf und Fehlern.
type CaptureState = {
    Defaults: RemoteData<LedgerBookingDefaults>
    Form: CaptureForm
    Picker: CapturePicker option
    /// Läuft "Buchung anlegen"? Dann ist der Knopf gesperrt (kein Doppel-Tipp mit zwei Buchungen).
    Saving: bool
    /// Fehler aus der Prüfung im Client oder vom Server; sie bleiben im Formular stehen.
    Errors: string list
}

let amountInvalidText = "Bitte einen gültigen Betrag eingeben"
let amountZeroText = "Der Betrag muss größer als 0 sein"
let accountMissingText = "Bitte ein Konto wählen"

/// Ein leeres Formular für einen Tag (vor der Vorbelegung: Ausgabe, ohne Konto).
let emptyForm (today: DateTime) : CaptureForm = {
    IsOutflow = true
    AmountText = ""
    AccountId = None
    Target = None
    TargetChosen = false
    Payee = ""
    Date = today.Date
    Memo = ""
}

/// Das gerade geöffnete Formular: die Vorbelegung lädt noch.
let opening : CaptureState = {
    Defaults = Loading
    Form = emptyForm DateTime.MinValue
    Picker = None
    Saving = false
    Errors = []
}

/// Das Formular nach dem Laden der Vorbelegung: heute (Berliner Kalender) und das vorbelegte
/// Konto (Filter, sonst zuletzt genutzt, nie das Import-Konto). Rein.
let formFromDefaults
    (defaults: LedgerBookingDefaults)
    (filterAccount: Guid option)
    (selectable: Guid list)
    : CaptureForm =
    { emptyForm defaults.Today with
        AccountId = defaultBookingAccount filterAccount defaults.LastUsedAccountId selectable }

/// Setzt das Konto nach, wenn noch keines gewählt ist (die wählbaren Konten kommen nach der
/// Vorbelegung an). Ein gewähltes Konto bleibt. Rein.
let withDefaultAccount
    (defaults: LedgerBookingDefaults)
    (filterAccount: Guid option)
    (selectable: Guid list)
    (form: CaptureForm)
    : CaptureForm =
    match form.AccountId with
    | Some _ -> form
    | None -> { form with AccountId = defaultBookingAccount filterAccount defaults.LastUsedAccountId selectable }

/// Der Betrag eines Chips im Eingabeformat ("8,60"). Rein.
let amountText (amount: decimal) : string =
    let cents = int64 (roundHalfUp (amount * 100m))
    let whole = cents / 100L
    let rest = cents % 100L
    // Ohne Format-Angabe `D2`: Fable übersetzt sie nicht wie .NET.
    string whole + "," + (string rest).PadLeft(2, '0')

/// Ein Zuletzt-Chip füllt Richtung, Betrag, Konto, Ziel und Payee; Datum und Memo bleiben. Rein.
let applyTemplate (template: LedgerBookingTemplate) (form: CaptureForm) : CaptureForm =
    { form with
        IsOutflow = template.IsOutflow
        AmountText = amountText template.Amount
        AccountId = Some template.AccountId
        Target = template.Target
        TargetChosen = true
        Payee = template.Payee }

/// Die Kategorie zum Payee übernehmen, solange Roman kein Ziel gewählt hat und der Payee noch
/// derselbe ist. Ohne Vorschlag bleibt das Ziel. Rein.
let applyPayeeCategory (payee: string) (category: YnabCategoryId option) (form: CaptureForm) : CaptureForm =
    match category with
    | Some id when not form.TargetChosen && form.Payee.Trim() = payee.Trim() ->
        { form with Target = Some (BookOnCategory id) }
    | _ -> form

/// Der Aufruf aus dem Formular, oder die Fehler, die der Client schon kennt (Betrag, Konto). Die
/// übrigen Regeln prüft der Server (`Validation.validateLedgerBooking`). Rein.
let captureRequest (form: CaptureForm) : Result<LedgerBookingRequest, string list> =
    let amount =
        match parseAmountInput form.AmountText with
        | None -> Error amountInvalidText
        | Some a when a <= 0m -> Error amountZeroText
        | Some a -> Ok a
    let errors =
        [ match amount with
          | Error e -> e
          | Ok _ -> ()
          if form.AccountId.IsNone then accountMissingText ]
    match amount, form.AccountId, errors with
    | Ok a, Some account, [] ->
        Ok {
            AccountId = account
            Amount = a
            IsOutflow = form.IsOutflow
            PayeeName = form.Payee.Trim()
            Target = form.Target
            Date = form.Date
            Memo = if String.IsNullOrWhiteSpace form.Memo then None else Some (form.Memo.Trim())
        }
    | _ -> Error errors

/// Der Text eines Zuletzt-Chips: "Bäckerei Lange · 8,60". Rein.
let templateLabel (template: LedgerBookingTemplate) : string =
    $"{template.Payee} · {amountText template.Amount}"

/// Der Satz der Rückmeldung nach dem Anlegen. Rein.
let createdText (created: LedgerBookingCreated) : string =
    match created.PushFailure with
    | None -> $"Buchung angelegt · {created.AccountName}"
    | Some reason -> $"Buchung angelegt · {created.AccountName}, noch nicht in YNAB: {reason}"
