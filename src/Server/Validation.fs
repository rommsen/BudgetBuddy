module Server.Validation

open System
open Shared.Domain
open Ledger.Bot.Domain

// ============================================
// Reusable Validators
// ============================================

let validateRequired (fieldName: string) (value: string) =
    if String.IsNullOrWhiteSpace(value) then
        Some $"{fieldName} ist erforderlich"
    else
        None

let validateLength (fieldName: string) (minLen: int) (maxLen: int) (value: string) =
    let len = value.Length
    if len < minLen || len > maxLen then
        Some $"{fieldName} muss zwischen {minLen} und {maxLen} Zeichen lang sein"
    else
        None

let validateRange (fieldName: string) (min: int) (max: int) (value: int) =
    if value < min || value > max then
        Some $"{fieldName} muss zwischen {min} und {max} liegen"
    else
        None

// ============================================
// Settings Validation
// ============================================

let validateYnabToken (token: string) : Result<string, string list> =
    let errors =
        [
            validateRequired "YNAB token" token
            validateLength "YNAB token" 10 500 token
        ]
        |> List.choose id

    if errors.IsEmpty then Ok token else Error errors

let validateComdirectSettings (settings: ComdirectSettings) : Result<ComdirectSettings, string list> =
    let errors =
        [
            validateRequired "Client ID" settings.ClientId
            validateRequired "Client Secret" settings.ClientSecret
            validateRequired "Username" settings.Username
            validateRequired "Password" settings.Password
            // AccountId is optional
        ]
        |> List.choose id

    if errors.IsEmpty then Ok settings else Error errors

let validateSyncSettings (settings: SyncSettings) : Result<SyncSettings, string list> =
    let errors =
        [
            validateRange "Days to fetch" 1 90 settings.DaysToFetch
        ]
        |> List.choose id

    if errors.IsEmpty then Ok settings else Error errors

/// Die Farbe für "Lena fragen" (ADR 0056, Punkt 1): `None` löscht die Einstellung, sonst
/// muss es eine der sechs YNAB-Flag-Farben sein.
let validateLenaFlagColor (color: string option) : Result<string option, string> =
    match color with
    | None -> Ok None
    | Some c when List.contains c ynabFlagColors -> Ok (Some c)
    | Some c -> Error $"Unbekannte Flag-Farbe: {c}"

// ============================================
// Rules Validation
// ============================================

let validateRuleName (name: string) : string option =
    match validateRequired "Regelname" name with
    | Some err -> Some err
    | None -> validateLength "Regelname" 1 100 name

let validatePattern (pattern: string) : string option =
    match validateRequired "Muster" pattern with
    | Some err -> Some err
    | None -> validateLength "Muster" 1 500 pattern

let validatePayeeOverride (payee: string option) : string option =
    match payee with
    | Some p when String.IsNullOrWhiteSpace(p) -> Some "Payee-Override darf nicht leer sein"
    | Some p -> validateLength "Payee-Override" 1 200 p
    | None -> None

let private validatePriority (priority: int) : string option =
    validateRange "Priorität" 0 10000 priority

let validateNewRule (rule: NewRule) : Result<NewRule, string list> =
    let errors =
        [
            validateRuleName rule.Name
            validatePattern rule.Pattern
            validatePayeeOverride rule.PayeeOverride
        ]
        |> List.choose id

    if errors.IsEmpty then Ok rule else Error errors

let validateRuleChange (change: RuleChange) : Result<RuleChange, string list> =
    let errors =
        [
            change.Name |> Option.bind validateRuleName
            change.Pattern |> Option.bind validatePattern
            change.Priority |> Option.bind validatePriority

            match change.PayeeOverride with
            | PayeeOverrideChange.Set payee -> validatePayeeOverride (Some payee)
            | PayeeOverrideChange.Keep | PayeeOverrideChange.Remove -> None
        ]
        |> List.choose id

    if errors.IsEmpty then Ok change else Error errors

/// Eine Regel aus einer Importdatei: dieselben Prüfungen wie beim Anlegen, dazu die Priorität,
/// die anders als beim Anlegen aus der Datei kommt.
let validateImportedRule (rule: Rule) : Result<Rule, string list> =
    let errors =
        [
            validateRuleName rule.Name
            validatePattern rule.Pattern
            validatePayeeOverride rule.PayeeOverride
            validatePriority rule.Priority
        ]
        |> List.choose id

    if errors.IsEmpty then Ok rule else Error errors

// ============================================
// Aufteilungen
// ============================================

/// Grund für eine abgelehnte Aufteilung als deutscher Satz für den Nutzer (ADR 0006).
let splitErrorText (error: SplitError) : string =
    match error with
    | TooFewLines _ -> "Eine Aufteilung braucht mindestens 2 Positionen"
    | CurrencyMismatch -> "Alle Positionen müssen die Währung der Buchung haben"
    | SumMismatch (expected, actual) -> $"Die Positionen ergeben {actual}, die Buchung beträgt aber {expected}"

// ============================================
// Transaction Validation
// ============================================

/// Validates a manually entered transaction (Quick Add) before it is sent to YNAB.
/// `today` ist der Berliner Kalendertag (infrastructure-p747v): der Container läuft auf UTC.
let validateManualTransaction (today: DateTime) (request: ManualTransactionRequest) : Result<ManualTransactionRequest, string list> =
    let errors =
        [
            (if request.Amount <= 0m then Some "Amount must be greater than zero" else None)
            (if request.Amount > 1_000_000m then Some "Amount is unreasonably large" else None)
            // Payee is optional (YNAB allows payee-less transactions); only cap the length
            (if String.IsNullOrWhiteSpace request.PayeeName then None
             else validateLength "Payee" 1 200 request.PayeeName)
            (if request.Date.Date > today then Some "Date cannot be in the future" else None)
            (if request.Date.Date < today.AddYears(-5) then Some "Date is too far in the past" else None)
            (match request.Memo with
             | Some m -> validateLength "Memo" 0 500 m
             | None -> None)
        ]
        |> List.choose id

    if errors.IsEmpty then Ok request else Error errors

// ============================================
// Budget (budgeting-k7wmd)
// ============================================

/// Ein Monat an der Grenze ist sein Erster als "JJJJ-MM-01".
let private tryParseMonthKey (key: string) : DateTime option =
    match
        DateTime.TryParseExact(
            key,
            "yyyy-MM-dd",
            Globalization.CultureInfo.InvariantCulture,
            Globalization.DateTimeStyles.None
        )
    with
    | true, date when date.Day = 1 -> Some date
    | _ -> None

let private monthKeyError (key: string) : string =
    $"Ungültiger Monat: '{key}'. Erwartet wird der Monatserste als JJJJ-MM-01."

/// Prüft den Monat an der Grenze, ein Meldungstext für alle Endpoints (ADR 0074).
let validateMonthKey (key: string) : Result<string, string> =
    match tryParseMonthKey key with
    | Some _ -> Ok key
    | None -> Error (monthKeyError key)

/// Prüft den Monat für "Targets anwenden". Strenger als das Lesen: ein vergangener Monat ist
/// lesbar, dort werden aber keine Targets angewendet. Künftige Monate außerhalb des Fensters
/// entscheidet YNAB (404).
let validateTargetsMonth (current: string) (key: string) : Result<string, string> =
    validateMonthKey key
    |> Result.bind (fun month ->
        if Shared.Budgeting.targetsAllowed (Shared.Budgeting.monthKind current month) then
            Ok month
        else
            Error "Targets anwenden geht in vergangenen Monaten nicht.")

/// Prüft den Monat für "Ausblenden" an der Leiste Monatserster (budgeting-m3x7f). Die Leiste gibt es im laufenden und
/// in kommenden Monaten (`targetsAllowed`, wie der Kopfknopf im Client), ein vergangener Monat wird abgelehnt.
let validateMonthStartMonth (current: string) (key: string) : Result<string, string> =
    validateMonthKey key
    |> Result.bind (fun month ->
        if Shared.Budgeting.targetsAllowed (Shared.Budgeting.monthKind current month) then
            Ok month
        else
            Error "Ausblenden geht in vergangenen Monaten nicht.")

/// Die Ablehnung einer leeren Befehls-Id (ADR 0084 Punkt 8). Eine Id, die keine Guid ist, scheitert schon
/// beim Deserialisieren.
[<Literal>]
let EmptyCommandIdText = "Befehls-Id ist leer."

let private commandIdErrors (commandId: Guid option) : string list =
    match commandId with
    | Some id when id = Guid.Empty -> [ EmptyCommandIdText ]
    | _ -> []

/// Prüft das Übernehmen von "Targets anwenden": den Monat wie `validateTargetsMonth` und die Befehls-Id der
/// gezeigten Vorschau, alle Fehler zugleich.
let validateTargetsApply (current: string) (key: string) (shown: Shared.Budgeting.TargetPreview) : Result<string, string list> =
    let month = validateTargetsMonth current key
    let errors =
        [ match month with
          | Error message -> message
          | Ok _ -> ()
          yield! commandIdErrors shown.CommandId ]
    match month with
    | Ok firstDay when errors.IsEmpty -> Ok firstDay
    | _ -> Error errors

/// Der Monat als Monatserster; eine Anfrage ohne Kategorie ist keine (budgeting-m1bce). Alle Fehler
/// zugleich, wie an der übrigen API-Grenze.
let validateCategoryMonthQuery (query: Shared.Domain.CategoryMonthQuery) : Result<DateTime, string list> =
    let month = tryParseMonthKey query.Month
    let errors =
        [ if query.CategoryId = Guid.Empty then
              "Die Kategorie fehlt."
          if month.IsNone then
              monthKeyError query.Month ]
    match month with
    | Some firstDay when errors.IsEmpty -> Ok firstDay
    | _ -> Error errors

/// Obergrenze eines Betrags in Verschieben und Zuweisen. Liegt weit über jedem echten Betrag und weit unter dem, was
/// beim Rechnen in Milliunits überliefe.
[<Literal>]
let MaxBudgetAmount = 1000000m

/// Prüft Monat und Auftrag an der Grenze, bevor YNAB gelesen oder etwas festgehalten wird. Alle
/// Fehler werden gesammelt. Der Betrag wird beidseitig begrenzt: `planMove` rechnet mit ihm, bevor
/// es ihn prüft, ein großer negativer Betrag liefe dort über. Raster und Kategorien prüft `planMove`.
let validateMoveRequest (monthKey: string) (request: Shared.Budgeting.MoveRequest) : Result<unit, string list> =
    let errors =
        [ match validateMonthKey monthKey with
          | Error message -> message
          | Ok _ -> ()
          if request.Amount.Currency <> "EUR" then
              $"Verschoben wird nur in EUR, nicht in '{request.Amount.Currency}'."
          if request.Amount.Amount <= 0m then
              "Der Betrag muss größer als 0 sein."
          elif request.Amount.Amount > MaxBudgetAmount then
              "Der Betrag ist zu groß, höchstens 1.000.000,00 € gehen."
          yield! commandIdErrors request.CommandId ]

    if errors.IsEmpty then Ok () else Error errors

/// Prüft den Auftrag Zuweisen vor allem anderen (ADR 0078). Sammelt alle Fehler.
let validateAssignRequest (monthKey: string) (request: Shared.Budgeting.AssignRequest) : Result<unit, string list> =
    let errors =
        [ match validateMonthKey monthKey with
          | Error message -> message
          | Ok _ -> ()
          if request.CategoryId = System.Guid.Empty then
              "Die Kategorie fehlt."
          if request.Assigned.Currency <> "EUR" then
              $"Zugewiesen wird nur in EUR, nicht in '{request.Assigned.Currency}'."
          if abs request.Assigned.Amount > MaxBudgetAmount then
              "Der Betrag ist zu groß, höchstens 1.000.000,00 € gehen."
          yield! commandIdErrors request.CommandId ]

    if errors.IsEmpty then Ok () else Error errors

/// Prüft den Auftrag Target pausieren oder aufheben (budgeting-t6p8z) vor allem anderen, auch ohne
/// Einrichtung: ungültiger oder vergangener Monat (`current` ist der laufende) und leere Kategorie, alle
/// Fehler zugleich. Ob die Kategorie sichtbar ist und ein Target hat, prüft der Ablauf.
let validateTargetPauseRequest
    (current: string)
    (monthKey: string)
    (request: Shared.Budgeting.TargetPauseRequest)
    : Result<unit, string list> =
    let errors =
        [ match validateMonthKey monthKey with
          | Error message -> message
          | Ok month ->
              if not (Shared.Budgeting.targetsAllowed (Shared.Budgeting.monthKind current month)) then
                  Shared.Budgeting.PausePastMonthText
          if request.CategoryId = System.Guid.Empty then
              "Die Kategorie fehlt."
          yield! commandIdErrors request.CommandId ]

    if errors.IsEmpty then Ok () else Error errors

/// Ist der Text ein echtes Datum "YYYY-MM-DD"?
let private isIsoDate (text: string) : bool =
    match DateTime.TryParseExact(text, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
    | true, _ -> true
    | _ -> false

/// Prüft den Auftrag Target ändern oder entfernen (budgeting-k8wq3, ADR 0080) vor allem anderen, auch ohne
/// Einrichtung, und sammelt alle Fehler: ungültiger, vergangener oder künftiger Monat (`current` ist der
/// laufende; beim Anlegen mit eigenem Satz, `targetMonthRefusalText`), leere Kategorie, und am Wunsch
/// Betrag negativ, nicht auf den Cent, über der Obergrenze, nicht in EUR, ein Datum, das nicht "YYYY-MM-DD" ist,
/// und ein Wochentag außerhalb von 0 bis 6. Beim Anlegen (`Shown = None`) zusätzlich ein fehlender Rhythmus und
/// 0 € (budgeting-g2ty9, X8); `targetAfter` prüft Rhythmus und Wochentag als zweite Sicherung. Dazu ein Wunsch,
/// der nach `targetAfter` wöchentlich ohne Wochentag wäre, und eine leere Befehls-Id (budgeting-eh8xe).
let validateTargetChange
    (current: string)
    (monthKey: string)
    (request: Shared.Budgeting.TargetChangeRequest)
    : Result<unit, string list> =
    let errors =
        [ match validateMonthKey monthKey with
          | Error message -> message
          | Ok month ->
              if not (Shared.Budgeting.targetEditAllowed (Shared.Budgeting.monthKind current month)) then
                  Shared.Budgeting.targetMonthRefusalText request
          if request.CategoryId = Guid.Empty then
              "Die Kategorie fehlt."
          match request.Wanted with
          | None -> ()
          | Some draft ->
              let goal = draft.NewGoal
              if goal.Currency <> Shared.Budgeting.TargetCurrency then
                  $"Ein Target gilt nur in EUR, nicht in '{goal.Currency}'."
              if goal.Amount < 0m then
                  "Der Betrag des Targets darf nicht negativ sein."
              elif goal.Amount > MaxBudgetAmount then
                  "Der Betrag ist zu groß, höchstens 1.000.000,00 € gehen."
              elif not (Shared.Budgeting.isOnTenMilliunits goal) then
                  "Der Betrag des Targets geht nur auf den Cent genau."
              match draft.NewDate with
              | Some date when not (isIsoDate date) ->
                  $"Ungültiges Datum: '{date}'. Erwartet wird JJJJ-MM-TT."
              | _ -> ()
              match draft.NewDay with
              | Some day when day < 0 || day > 6 -> Shared.Budgeting.TargetWeekdayText
              | _ -> ()
              if request.Shown.IsNone then
                  if draft.NewRhythm = Shared.Budgeting.KeepRhythm then
                      Shared.Budgeting.TargetRhythmMissingText
                  if goal.Amount = 0m then
                      Shared.Budgeting.TargetCreateZeroText
              // Ist das Target nach dem Wunsch wöchentlich ohne Tag, fehlt der Wochentag. Ohne `NewDay` bleibt
              // der Tag eines schon wöchentlichen Targets (`targetAfter`), dann ist der Wunsch gültig.
              match Shared.Budgeting.targetAfter request.Shown draft with
              | Ok after when after.Day.IsNone && (match after.Rhythm with Shared.Budgeting.Weeks _ -> true | _ -> false) ->
                  Shared.Budgeting.TargetWeekdayMissingText
              | _ -> ()
          yield! commandIdErrors request.CommandId ]

    if errors.IsEmpty then Ok () else Error errors

// ============================================
// Bot-Schnittstelle (ledger-b0b7q, ADR 0066)
// ============================================
// Nur die Eingabeprüfung der Anfrage. Die Fachregeln (Datumsfenster, Tagesgrenze, Berliner
// Kalendertag, Kategorie-Auflösung) stehen rein in `Ledger.Bot.Domain` (ledger-ct981).

/// Ein Fehler zu einem Feld der Anfrage (Antwort 400 mit Feldnamen).
type BotFieldError = { BotField: string; BotProblem: string }

/// Felder, die auf ein anderes Konto zielen würden. Wer eins mitschickt, bekommt 400.
let private botForbiddenAccountFields = [ "account"; "account_id"; "accountId"; "account_name" ]

/// Prüft den JSON-Rumpf der Vorschau. Sammelt alle Feldfehler, statt beim ersten aufzuhören.
/// `checkDate` ist die Fachregel für ein lesbares Datum (in der App
/// `Ledger.Bot.Domain.checkBotBookingDate` mit dem Berliner Kalendertag); ihr Fehler wird zum
/// Feldfehler `date`, gesammelt mit den übrigen.
let parseBotPreviewRequest (checkDate: DateTime -> Result<unit, string>) (json: string) : Result<BotPreviewInput, BotFieldError list> =
    let fail field message = { BotField = field; BotProblem = message }
    let parsed =
        try
            let doc = System.Text.Json.JsonDocument.Parse(json)
            if doc.RootElement.ValueKind = System.Text.Json.JsonValueKind.Object then Ok doc
            else Error [ fail "body" "Der Rumpf muss ein JSON-Objekt sein" ]
        with _ ->
            Error [ fail "body" "Der Rumpf ist kein gültiges JSON" ]

    match parsed with
    | Error errors -> Error errors
    | Ok doc ->
        use _doc = doc
        let root = doc.RootElement
        let prop name =
            match root.TryGetProperty(name: string) with
            | true, v when v.ValueKind <> System.Text.Json.JsonValueKind.Null -> Some v
            | _ -> None

        let requiredString name =
            match prop name with
            | None -> Error (fail name "Pflichtfeld fehlt")
            | Some v when v.ValueKind <> System.Text.Json.JsonValueKind.String ->
                Error (fail name "Muss ein Text sein")
            | Some v ->
                let text = v.GetString().Trim()
                if text = "" then Error (fail name "Darf nicht leer sein") else Ok text

        let amount =
            match prop "amount" with
            | None -> Error (fail "amount" "Pflichtfeld fehlt")
            | Some v when v.ValueKind <> System.Text.Json.JsonValueKind.Number ->
                Error (fail "amount" "Muss eine Zahl in Euro sein")
            | Some v ->
                match v.TryGetDecimal() with
                | true, d when Math.Round(d, 2) = d -> Ok d
                | true, _ -> Error (fail "amount" "Höchstens zwei Nachkommastellen")
                | _ -> Error (fail "amount" "Muss eine Zahl in Euro sein")

        let direction =
            match requiredString "direction" with
            | Error e -> Error e
            | Ok "outflow" -> Ok BotOutflow
            | Ok "inflow" -> Ok BotInflow
            | Ok _ -> Error (fail "direction" "Erlaubt sind 'outflow' und 'inflow'")

        let payee =
            match requiredString "payee" with
            | Ok p when p.Length > 200 -> Error (fail "payee" "Höchstens 200 Zeichen")
            | other -> other

        let date =
            match requiredString "date" with
            | Error e -> Error e
            | Ok text ->
                match DateTime.TryParseExact(text, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
                | true, d ->
                    match checkDate d with
                    | Error problem -> Error (fail "date" problem)
                    | Ok () -> Ok (DateTime.SpecifyKind(d.Date, DateTimeKind.Utc))
                | _ -> Error (fail "date" "Erwartet wird JJJJ-MM-TT")

        let memo =
            match prop "memo" with
            | None -> Ok None
            | Some v when v.ValueKind <> System.Text.Json.JsonValueKind.String -> Error (fail "memo" "Muss ein Text sein")
            | Some v ->
                let text = v.GetString().Trim()
                if text.Length > 500 then Error (fail "memo" "Höchstens 500 Zeichen")
                elif text = "" then Ok None
                else Ok (Some text)

        let accountErrors =
            botForbiddenAccountFields
            |> List.choose (fun name ->
                if prop name |> Option.isSome then
                    Some (fail name "Das Konto ist fest (Quick-Add-Konto), die Anfrage darf keins nennen")
                else None)

        let errorOf r = match r with Error e -> Some e | Ok _ -> None
        let category = requiredString "category"
        let errors =
            [ amount |> errorOf; direction |> errorOf; payee |> errorOf
              category |> errorOf; date |> errorOf; memo |> errorOf ]
            |> List.choose id
            |> fun own -> own @ accountErrors

        match amount, direction, payee, category, date, memo with
        | Ok a, Ok dir, Ok p, Ok c, Ok d, Ok m when errors.IsEmpty ->
            Ok { AmountEur = a; Direction = dir; Payee = p; Category = c; Date = d; Memo = m }
        | _ -> Error errors

/// Prüft den Betrag gegen die Obergrenze (Euro). Betrag 0 oder negativ ist unzulässig, die
/// Richtung kommt allein aus `direction`. Die Grenze selbst ist erlaubt.
let checkBotAmount (maxAmountEur: decimal) (amountEur: decimal) : Result<unit, string> =
    if amountEur <= 0m then Error "Der Betrag muss größer als 0 sein, die Richtung steht in 'direction'"
    elif amountEur > maxAmountEur then
        Error $"Der Betrag {amountEur.ToString(Globalization.CultureInfo.InvariantCulture)} EUR liegt über der Obergrenze von {maxAmountEur.ToString(Globalization.CultureInfo.InvariantCulture)} EUR"
    else Ok ()

/// Prüft die Anfrage von `setEntryTransfer` (ledger-qvjvs): Buchung und Zielkonto brauchen eine
/// Id. Alle Fehler gesammelt.
let validateTransferRequest (entryId: Guid) (YnabAccountId target: YnabAccountId) : Result<unit, string list> =
    let errors =
        [
            if entryId = Guid.Empty then "Die Buchung fehlt."
            if target = Guid.Empty then "Das Zielkonto fehlt."
        ]
    if List.isEmpty errors then Ok () else Error errors

// ============================================
// Buchung im Kassenbuch erfassen (ledger-at63w)
// ============================================

let bookingAccountMissingText = "Bitte ein Konto wählen."
let bookingTargetMissingText = "Das Ziel der Buchung fehlt."
let bookingAccountUnknownText = "Das Konto gibt es im Kassenbuch nicht."
let bookingAccountClosedText = "Das Konto ist geschlossen."
let bookingAccountNotInYnabText = "Das Konto ist nicht mit YNAB verbunden."
let bookingImportAccountText =
    "Auf dem Import-Konto erfasst BB noch nicht, diese Buchungen kommen mit dem Comdirect-Import."
let bookingAccountNotBudgetText = "Das Konto ist kein offenes Budget-Konto."
let bookingTransferOwnAccountText = "Ein Übertrag auf das eigene Konto geht nicht."
let bookingTransferTargetText = "Das Zielkonto ist kein offenes Budget-Konto."
let bookingTransferNoPayeeText = "Für das Zielkonto gibt es in YNAB keinen Übertrags-Payee."

/// Der Quick-Add-Request, auf den eine erfasste Buchung abgebildet wird: dieselben Felder, die
/// Kategorie nur bei einer Kategorie als Ziel. Rein.
let ledgerBookingManualRequest (request: LedgerBookingRequest) : ManualTransactionRequest = {
    Amount = request.Amount
    IsOutflow = request.IsOutflow
    PayeeName = request.PayeeName
    CategoryId =
        match request.Target with
        | Some (BookOnCategory categoryId) -> Some categoryId
        | _ -> None
    Date = request.Date
    Memo = request.Memo
}

/// Prüft die Form einer erfassten Buchung an der API-Grenze: ein Konto ist gewählt, ein Ziel
/// trägt eine Id, und Betrag, Payee, Datum und Memo folgen denselben Regeln wie Quick Add
/// (`validateManualTransaction`). Sammelt alle Fehler. Konto und Ziel gegen den Bestand prüft
/// `resolveLedgerBookingAccounts`.
let validateLedgerBooking (today: DateTime) (request: LedgerBookingRequest) : Result<LedgerBookingRequest, string list> =
    let ownErrors =
        [
            if request.AccountId = Guid.Empty then bookingAccountMissingText
            match request.Target with
            | Some (BookOnCategory (YnabCategoryId id)) when id = Guid.Empty -> bookingTargetMissingText
            | Some (BookAsTransfer (YnabAccountId id)) when id = Guid.Empty -> bookingTargetMissingText
            | _ -> ()
        ]
    let quickAddErrors =
        match validateManualTransaction today (ledgerBookingManualRequest request) with
        | Ok _ -> []
        | Error errors -> errors
    match ownErrors @ quickAddErrors with
    | [] -> Ok request
    | errors -> Error errors

/// Das Konto einer erfassten Buchung nach der Prüfung gegen den Bestand.
type LedgerBookingAccount = {
    LedgerAccountId: Ledger.Events.LedgerAccountId
    YnabAccountId: YnabAccountId
    Name: string
}

/// Das Ziel eines Übertrags nach der Prüfung: Konto, Name in YNAB und Übertrags-Payee.
type LedgerBookingTransfer = {
    TargetAccountId: YnabAccountId
    TargetName: string
    TransferPayeeId: string
}

/// Prüft Konto und Übertrags-Ziel einer erfassten Buchung gegen die Kassenbuch-Konten und die
/// Konten des YNAB-Budgets: das Konto ist bekannt, offen, mit YNAB verbunden, nicht das
/// Import-Konto und ein offenes Budget-Konto; ein Ziel-Konto ist nicht das eigene, ein offenes
/// Budget-Konto und hat eine Übertrags-Payee. Die erste verletzte Regel ist der Fehler. Rein.
let resolveLedgerBookingAccounts
    (ledgerAccounts: Ledger.Domain.LedgerAccount list)
    (budget: Server.YnabClient.TransferAccount list)
    (importAccount: YnabAccountId option)
    (request: LedgerBookingRequest)
    : Result<LedgerBookingAccount * LedgerBookingTransfer option, string> =
    let openBudget (id: YnabAccountId) =
        budget |> List.tryFind (fun a -> a.Id = id && a.OnBudget && not a.Closed)
    match ledgerAccounts |> List.tryFind (fun a -> a.Id = Ledger.Events.LedgerAccountId request.AccountId) with
    | None -> Error bookingAccountUnknownText
    | Some account when account.Closed -> Error bookingAccountClosedText
    | Some account ->
        match account.Link with
        | Ledger.Domain.BbOwned -> Error bookingAccountNotInYnabText
        | Ledger.Domain.YnabMirrored ynabId when Some ynabId = importAccount -> Error bookingImportAccountText
        | Ledger.Domain.YnabMirrored ynabId ->
            match openBudget ynabId with
            | None -> Error bookingAccountNotBudgetText
            | Some _ ->
                let resolved = { LedgerAccountId = account.Id; YnabAccountId = ynabId; Name = account.Name }
                match request.Target with
                | Some (BookAsTransfer target) when target = ynabId -> Error bookingTransferOwnAccountText
                | Some (BookAsTransfer target) ->
                    match openBudget target with
                    | None -> Error bookingTransferTargetText
                    | Some targetAccount ->
                        match targetAccount.TransferPayeeId with
                        | None -> Error bookingTransferNoPayeeText
                        | Some payeeId ->
                            Ok (resolved, Some { TargetAccountId = target; TargetName = targetAccount.Name; TransferPayeeId = payeeId })
                | _ -> Ok (resolved, None)

// ============================================
// Kategorien verwalten (budgeting-pscgb, ADR 0088 Punkt 1)
// ============================================

/// Was nach dem Prüfen einer Kategorie nach YNAB geht.
type CategoryWrite =
    /// `POST` in die Gruppe.
    | CreateCategoryIn of groupId: Guid * name: string
    /// `PATCH` nur mit den geänderten Feldern.
    | PatchCategory of categoryId: Guid * name: string option * groupId: Guid option
    /// Nichts geändert: kein Aufruf.
    | CategoryUnchanged of categoryId: Guid

/// Was nach dem Prüfen einer Gruppe nach YNAB geht.
type CategoryGroupWrite =
    | CreateGroup of name: string
    | RenameGroup of groupId: Guid * name: string
    | GroupUnchanged of groupId: Guid

let CategoryNameMissingText = "Bitte einen Namen eingeben."
let GroupNameTooLongText = $"Der Name einer Gruppe hat höchstens {Shared.Budgeting.CategoryGroupNameMaxLength} Zeichen (YNAB-Grenze)."
let UnknownGroupText = "Die Gruppe gibt es in YNAB nicht mehr. Seite neu laden."
let UnknownCategoryText = "Die Kategorie gibt es in YNAB nicht mehr. Seite neu laden."

let private sameName (a: string) (b: string) =
    String.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase)

/// Der Name einer Kategorie ohne Liste: nicht leer. Ok trägt ihn ohne Leerraum am Rand.
let validateCategoryName (name: string) : Result<string, string list> =
    if String.IsNullOrWhiteSpace name then Error [ CategoryNameMissingText ] else Ok (name.Trim())

/// Der Name einer Gruppe ohne Liste: nicht leer, höchstens 50 Zeichen.
let validateCategoryGroupName (name: string) : Result<string, string list> =
    if String.IsNullOrWhiteSpace name then Error [ CategoryNameMissingText ]
    elif name.Trim().Length > Shared.Budgeting.CategoryGroupNameMaxLength then Error [ GroupNameTooLongText ]
    else Ok (name.Trim())

/// Kategorie anlegen oder bearbeiten gegen die Liste aus YNAB. Sammelt alle Gründe: leerer Name, unbekannte
/// Kategorie, unbekannte Gruppe, Name schon in derselben Gruppe. Ok trägt, was nach YNAB geht.
let validateCategorySave
    (groups: Shared.Budgeting.ManagedCategoryGroup list)
    (save: Shared.Budgeting.CategorySave)
    : Result<CategoryWrite, string list> =
    let name = save.Name.Trim()
    let target = groups |> List.tryFind (fun g -> g.Id = save.GroupId)
    let current =
        save.CategoryId
        |> Option.map (fun id ->
            groups
            |> List.tryPick (fun g -> g.Categories |> List.tryFind (fun c -> c.Id = id) |> Option.map (fun c -> g, c)))
    let errors =
        [ if String.IsNullOrWhiteSpace save.Name then CategoryNameMissingText
          match current with
          | Some None -> UnknownCategoryText
          | _ -> ()
          match target with
          | None -> UnknownGroupText
          | Some group ->
              let taken =
                  group.Categories
                  |> List.exists (fun c -> Some c.Id <> save.CategoryId && not (String.IsNullOrWhiteSpace name) && sameName c.Name name)
              if taken then $"In der Gruppe {group.Name} gibt es schon eine Kategorie {name}." ]
    match errors, current with
    | [], None -> Ok (CreateCategoryIn (save.GroupId, name))
    | [], Some (Some (group, category)) ->
        let newName = if category.Name = name then None else Some name
        let newGroup = if group.Id = save.GroupId then None else Some save.GroupId
        if newName.IsNone && newGroup.IsNone then Ok (CategoryUnchanged category.Id)
        else Ok (PatchCategory (category.Id, newName, newGroup))
    | _ -> Error errors

/// Gruppe anlegen oder umbenennen gegen die Liste aus YNAB: Name nicht leer und höchstens 50 Zeichen, keine
/// zweite Gruppe mit dem Namen, beim Umbenennen eine bekannte Gruppe.
let validateCategoryGroupSave
    (groups: Shared.Budgeting.ManagedCategoryGroup list)
    (save: Shared.Budgeting.CategoryGroupSave)
    : Result<CategoryGroupWrite, string list> =
    let name = save.Name.Trim()
    let current = save.GroupId |> Option.map (fun id -> groups |> List.tryFind (fun g -> g.Id = id))
    let errors =
        [ match validateCategoryGroupName save.Name with
          | Error own -> yield! own
          | Ok _ -> ()
          match current with
          | Some None -> UnknownGroupText
          | _ -> ()
          if not (String.IsNullOrWhiteSpace name)
             && groups |> List.exists (fun g -> Some g.Id <> save.GroupId && sameName g.Name name) then
              $"Eine Gruppe {name} gibt es schon." ]
    match errors, current with
    | [], None -> Ok (CreateGroup name)
    | [], Some (Some group) -> if group.Name = name then Ok (GroupUnchanged group.Id) else Ok (RenameGroup (group.Id, name))
    | _ -> Error errors

/// Die Meldung, wenn die Reihenfolge keine Gruppe nennt.
[<Literal>]
let OrderGroupMissingText = "Die Reihenfolge nennt keine Gruppe."

/// Die Meldung, wenn eine Kategorie in der Reihenfolge fehlt oder doppelt steht.
[<Literal>]
let OrderCategoryInvalidText = "Die Reihenfolge nennt eine Kategorie doppelt oder ohne Id."

/// BBs Reihenfolge einer Gruppe (budgeting-sn2sn): eine Gruppe, jede Kategorie höchstens einmal und keine leere Id.
/// Alle Gründe gesammelt.
let validateCategoryOrderSave (save: Shared.Budgeting.CategoryOrderSave) : Result<unit, string list> =
    let errors =
        [ if save.GroupId = Guid.Empty then
              OrderGroupMissingText
          if save.CategoryIds |> List.contains Guid.Empty
             || List.length (List.distinct save.CategoryIds) <> List.length save.CategoryIds then
              OrderCategoryInvalidText ]
    if errors.IsEmpty then Ok () else Error errors
