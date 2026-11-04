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
              "Der Betrag ist zu groß, höchstens 1.000.000,00 € gehen." ]

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
              "Der Betrag ist zu groß, höchstens 1.000.000,00 € gehen." ]

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
              "Die Kategorie fehlt." ]

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
