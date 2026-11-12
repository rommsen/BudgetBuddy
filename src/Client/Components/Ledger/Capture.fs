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
    /// Auch gesetzt, solange "Buchung anlegen" auf die Kategorie zum Payee wartet.
    Saving: bool
    /// Der Payee, zu dem die Kategorie gerade geholt wird (nach dem Verlassen des Felds).
    PayeeLookup: string option
    /// "Buchung anlegen" wurde getippt, während die Kategorie zum Payee lud: der Aufruf startet mit
    /// der Antwort, damit die vorgeschlagene Kategorie nicht fehlt.
    SubmitWhenLookedUp: bool
    /// Der Fehler am Betragsfeld aus der Prüfung im Client (unlesbar oder 0); Tippen nimmt ihn zurück.
    AmountError: string option
    /// Der Fehler unter dem Feld "Konto"; die Wahl eines Kontos nimmt ihn zurück.
    AccountError: string option
    /// Fehler vom Server, gesammelt über "Buchung anlegen": `validateLedgerBooking` liefert Texte ohne Feldbezug.
    ServerErrors: string list
    /// Zeigt das Kassenbuch nach dem Anlegen die neue Buchung im Panel? Nicht, wenn die Antwort auf der Übersicht
    /// ankommt (ledger-s1weh): dort bleibt man auf der Seite. Der Rahmen setzt es mit der Antwort
    /// (`State.captureAnsweredOn`), es hängt nicht an der Seite, auf der das Formular geöffnet wurde.
    ShowsCreatedEntry: bool
}

let amountInvalidText = "Betrag nicht lesbar, etwa 12,50"
let amountZeroText = "Der Betrag muss größer als 0 sein"
let accountMissingText = "Konto fehlt"

/// Die Id des Betragsfehlers, auf die das Betragsfeld per `aria-describedby` verweist (ledger-vf6z5).
let amountErrorId = "capture-amount-error"

/// Das Betragsfeld des Formulars, für Fokus und Meldung ins Bild.
let amountFieldSelector = "[data-testid='capture-amount'] input"

/// Die Fehler, die der Client vor dem Aufruf kennt, je Feld.
type CaptureFieldErrors = {
    AmountMessage: string option
    AccountMessage: string option
}

/// Was die Konto-Auswahl zeigt (ledger-vf6z5): lädt, Ladefehler, kein wählbares Konto oder die Konten.
type CaptureAccountList =
    | CaptureAccountsLoading
    | CaptureAccountsFailed
    | NoBookableAccount
    | BookableAccounts of LedgerAccountView list

let accountsLoadingText = "Konten werden geladen"
let accountsFailedText = "Konten konnten nicht geladen werden"
let noBookableAccountText = "Kein Konto zum Buchen"

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
    PayeeLookup = None
    SubmitWhenLookedUp = false
    AmountError = None
    AccountError = None
    ServerErrors = []
    ShowsCreatedEntry = true
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

/// Der Aufruf aus dem Formular, oder die Fehler, die der Client schon kennt, je Feld (Betrag, Konto).
/// Die übrigen Regeln prüft der Server (`Validation.validateLedgerBooking`). Rein.
let captureRequest (form: CaptureForm) : Result<LedgerBookingRequest, CaptureFieldErrors> =
    let amount =
        match parseAmountInput form.AmountText with
        | None -> Error amountInvalidText
        | Some a when a <= 0m -> Error amountZeroText
        | Some a -> Ok a
    match amount, form.AccountId with
    | Ok a, Some account ->
        Ok {
            AccountId = account
            Amount = a
            IsOutflow = form.IsOutflow
            PayeeName = form.Payee.Trim()
            Target = form.Target
            Date = form.Date
            Memo = if String.IsNullOrWhiteSpace form.Memo then None else Some (form.Memo.Trim())
        }
    | _ ->
        Error {
            AmountMessage = (match amount with Error e -> Some e | Ok _ -> None)
            AccountMessage = (if form.AccountId.IsNone then Some accountMissingText else None)
        }

/// Wartet "Buchung anlegen" auf die Kategorie zum Payee? Nur, wenn die Abfrage zum Payee im Feld
/// läuft; eine Abfrage zu einem alten Payee wird ohnehin verworfen. Rein.
let awaitsPayeeCategory (capture: CaptureState) : bool =
    capture.PayeeLookup |> Option.exists (fun p -> p = capture.Form.Payee.Trim())

/// Der Text eines Zuletzt-Chips: "Bäckerei Lange · 8,60". Rein.
let templateLabel (template: LedgerBookingTemplate) : string =
    $"{template.Payee} · {amountText template.Amount}"

/// Der Satz der Rückmeldung nach dem Anlegen: Konto und Betrag (Styleguide §7.13, ledger-s1weh), eine Ausgabe
/// mit Minus. Ohne bekannten Betrag nur das Konto. Rein.
let createdText (created: LedgerBookingCreated) (amount: decimal option) : string =
    let what =
        match amount with
        | Some a ->
            let money = Client.DesignSystem.Money.formatAmountText true true a "EUR"
            $"{created.AccountName} · {money}"
        | None -> created.AccountName
    match created.PushFailure with
    | None -> $"Buchung angelegt · {what}"
    | Some reason -> $"Buchung angelegt · {what}, noch nicht in YNAB: {reason}"

/// Der Betrag des gerade angelegten Formulars mit Vorzeichen: eine Ausgabe negativ. `None`, wenn er nicht lesbar
/// ist. Rein.
let signedAmount (form: CaptureForm) : decimal option =
    match captureRequest form with
    | Ok request -> Some (if request.IsOutflow then -request.Amount else request.Amount)
    | Error _ -> None
