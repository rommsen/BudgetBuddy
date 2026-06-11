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
            (if String.IsNullOrWhiteSpace request.PayeeName then Some "Payee cannot be empty"
             else validateLength "Payee" 1 200 request.PayeeName)
            (if request.Date.Date > DateTime.Today then Some "Date cannot be in the future" else None)
            (if request.Date.Date < DateTime.Today.AddYears(-5) then Some "Date is too far in the past" else None)
            (match request.Memo with
             | Some m -> validateLength "Memo" 0 500 m
             | None -> None)
        ]
        |> List.choose id

    if errors.IsEmpty then Ok request else Error errors
