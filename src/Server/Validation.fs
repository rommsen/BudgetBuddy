module Server.Validation

open System
open Shared.Domain

// ============================================
// Reusable Validators
// ============================================

let validateRequired (fieldName: string) (value: string) =
    if String.IsNullOrWhiteSpace(value) then
        Some $"{fieldName} is required"
    else
        None

let validateLength (fieldName: string) (minLen: int) (maxLen: int) (value: string) =
    let len = value.Length
    if len < minLen || len > maxLen then
        Some $"{fieldName} must be between {minLen} and {maxLen} characters"
    else
        None

let validateRange (fieldName: string) (min: int) (max: int) (value: int) =
    if value < min || value > max then
        Some $"{fieldName} must be between {min} and {max}"
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
    match validateRequired "Rule name" name with
    | Some err -> Some err
    | None -> validateLength "Rule name" 1 100 name

let validatePattern (pattern: string) : string option =
    match validateRequired "Pattern" pattern with
    | Some err -> Some err
    | None -> validateLength "Pattern" 1 500 pattern

let validateRuleCreateRequest (request: RuleCreateRequest) : Result<RuleCreateRequest, string list> =
    let errors =
        [
            validateRuleName request.Name
            validatePattern request.Pattern
            validateRange "Priority" 0 10000 request.Priority
        ]
        |> List.choose id

    if errors.IsEmpty then Ok request else Error errors

let validateRuleUpdateRequest (request: RuleUpdateRequest) : Result<RuleUpdateRequest, string list> =
    let errors =
        [
            match request.Name with
            | Some name -> validateRuleName name
            | None -> None

            match request.Pattern with
            | Some pattern -> validatePattern pattern
            | None -> None

            match request.Priority with
            | Some priority -> validateRange "Priority" 0 10000 priority
            | None -> None
        ]
        |> List.choose id

    if errors.IsEmpty then Ok request else Error errors

// ============================================
// Transaction Validation
// ============================================

let validatePayeeOverride (payee: string option) : string option =
    match payee with
    | Some p when String.IsNullOrWhiteSpace(p) -> Some "Payee override cannot be empty"
    | Some p -> validateLength "Payee override" 1 200 p
    | None -> None

/// Validates a manually entered transaction (Quick Add) before it is sent to YNAB.
let validateManualTransaction (request: ManualTransactionRequest) : Result<ManualTransactionRequest, string list> =
    let errors =
        [
            (if request.Amount <= 0m then Some "Amount must be greater than zero" else None)
            (if request.Amount > 1_000_000m then Some "Amount is unreasonably large" else None)
            // Payee is optional (YNAB allows payee-less transactions); only cap the length
            (if String.IsNullOrWhiteSpace request.PayeeName then None
             else validateLength "Payee" 1 200 request.PayeeName)
            (if request.Date.Date > DateTime.Today then Some "Date cannot be in the future" else None)
            (if request.Date.Date < DateTime.Today.AddYears(-5) then Some "Date is too far in the past" else None)
            (match request.Memo with
             | Some m -> validateLength "Memo" 0 500 m
             | None -> None)
        ]
        |> List.choose id

    if errors.IsEmpty then Ok request else Error errors

// ============================================
// Budget (budgeting-k7wmd)
// ============================================

/// Prüft den Monats-Schlüssel der Budget-Seite an der Grenze: "current" oder "next".
let validateBudgetMonth (key: string) : Result<Shared.Budgeting.BudgetMonthChoice, string> =
    Shared.Budgeting.parseMonthChoice key

// ============================================
// Bot-Schnittstelle (ledger-b0b7q, ADR 0066)
// ============================================

/// Die Richtung einer Bot-Buchung. Kommt allein aus `direction`, nie aus dem Vorzeichen
/// des Betrags.
type BotDirection =
    | BotOutflow
    | BotInflow

/// Die geprüfte Eingabe der Vorschau. `AmountEur` ist positiv und in Euro (nicht in
/// YNAB-Milliunits). Es gibt bewusst kein Konto-Feld: das Konto ist immer das Quick-Add-Konto
/// (ADR 0004).
type BotPreviewInput = {
    AmountEur: decimal
    Direction: BotDirection
    Payee: string
    /// Rohtext aus der Anfrage: Id oder Name, aufgelöst durch `resolveBotCategory`.
    Category: string
    Date: DateTime
    Memo: string option
}

/// Ein Fehler zu einem Feld der Anfrage (Antwort 400 mit Feldnamen).
type BotFieldError = { BotField: string; BotProblem: string }

/// Felder, die auf ein anderes Konto zielen würden. Wer eins mitschickt, bekommt 400.
let private botForbiddenAccountFields = [ "account"; "account_id"; "accountId"; "account_name" ]

/// Prüft den JSON-Rumpf der Vorschau. `today` kommt von außen (Uhr injiziert). Sammelt alle
/// Feldfehler, statt beim ersten aufzuhören.
let parseBotPreviewRequest (today: DateTime) (json: string) : Result<BotPreviewInput, BotFieldError list> =
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
                | true, d when d.Date > today.Date -> Error (fail "date" "Das Datum liegt in der Zukunft")
                | true, d when d.Date < today.Date.AddYears(-5) -> Error (fail "date" "Das Datum liegt zu weit zurück")
                | true, d -> Ok (DateTime.SpecifyKind(d.Date, DateTimeKind.Utc))
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

/// Kategorien, die Bob wählen darf: nicht die internen YNAB-Kategorien, nicht versteckte.
let selectableBotCategories (categories: YnabCategory list) : YnabCategory list =
    categories
    |> List.filter (fun c -> c.GroupName <> internalMasterGroupName && not c.Hidden)

/// Ergebnis der Kategorie-Auflösung. BB rät nicht: bei keinem oder mehreren Treffern
/// kommt eine Kandidatenliste statt einer Wahl.
type BotCategoryResolution =
    | BotCategoryResolved of YnabCategory
    | BotCategoryUnknown of candidates: YnabCategory list
    | BotCategoryAmbiguous of candidates: YnabCategory list

/// Löst `query` (Id oder Name, ohne Beachtung der Groß-/Kleinschreibung) gegen die wählbaren
/// Kategorien auf. Genau ein Treffer wird übernommen. Ohne Treffer sind die Kandidaten die
/// Kategorien, deren Name den Suchtext enthält (oder umgekehrt).
let resolveBotCategory (categories: YnabCategory list) (query: string) : BotCategoryResolution =
    let selectable = selectableBotCategories categories
    let q = query.Trim()
    let byId =
        match Guid.TryParse q with
        | true, g -> selectable |> List.filter (fun c -> c.Id = YnabCategoryId g)
        | _ -> []
    let byName () =
        selectable |> List.filter (fun c -> String.Equals(c.Name.Trim(), q, StringComparison.OrdinalIgnoreCase))
    match byId with
    | [ one ] -> BotCategoryResolved one
    | _ ->
        match byName () with
        | [ one ] -> BotCategoryResolved one
        | [] ->
            let lower = q.ToLowerInvariant()
            let close =
                selectable
                |> List.filter (fun c ->
                    let name = c.Name.ToLowerInvariant()
                    lower <> "" && (name.Contains lower || lower.Contains name))
            BotCategoryUnknown close
        | many -> BotCategoryAmbiguous many
