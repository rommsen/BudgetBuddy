module Server.DuplicateDetection

open System
open System.Text.RegularExpressions
open Shared.Domain

// ============================================
// Reference Extraction (from YNAB memo)
// ============================================

/// Regex to extract reference from YNAB memo format: "..., Ref: <reference>"
let private referenceRegex = new Regex(@"Ref:\s*(.+)$", RegexOptions.Compiled)

/// Extracts the reference ID from a YNAB transaction memo
/// The legacy code stores references as "..., Ref: <reference>" in the memo field
let extractReference (memo: string option) : string option =
    match memo with
    | None -> None
    | Some m when String.IsNullOrWhiteSpace m -> None
    | Some m ->
        let result = referenceRegex.Match(m)
        if result.Success then
            let refValue = result.Groups[1].Value.Trim()
            if String.IsNullOrWhiteSpace refValue then None
            else Some refValue
        else
            None

// ============================================
// Duplicate Detection Logic
// ============================================

/// Configuration for fuzzy duplicate matching
type DuplicateMatchConfig = {
    /// Maximum number of days difference for date matching
    DateToleranceDays: int
    /// Maximum amount difference (absolute) for fuzzy matching
    AmountTolerancePercent: decimal
}

let defaultConfig = {
    DateToleranceDays = 1
    AmountTolerancePercent = 0.01m  // 1% tolerance
}

/// Checks if a bank transaction matches a YNAB transaction by reference
/// This is the primary and most reliable duplicate detection method
let matchesByReference (bankTx: BankTransaction) (ynabTx: YnabTransaction) : bool =
    // Check if the YNAB transaction's memo contains the bank transaction's reference
    match extractReference ynabTx.Memo with
    | Some ynabRef -> ynabRef = bankTx.Reference
    | None -> false

/// Checks if a bank transaction might match a YNAB transaction by date/amount/payee
/// This is a fuzzy match for cases where reference is not available
let matchesByDateAmountPayee (config: DuplicateMatchConfig) (bankTx: BankTransaction) (ynabTx: YnabTransaction) : bool =
    // Check date (within tolerance)
    let dateDiff = abs (bankTx.BookingDate.Date - ynabTx.Date.Date).Days
    let dateMatches = dateDiff <= config.DateToleranceDays

    // Check amount (exact match - amounts should be identical)
    let amountMatches = bankTx.Amount.Amount = ynabTx.Amount.Amount

    // Check payee (fuzzy - one contains the other or they're similar)
    let payeeMatches =
        match bankTx.Payee, ynabTx.Payee with
        | Some bankPayee, Some ynabPayee ->
            let bankNormalized = bankPayee.ToUpperInvariant().Trim()
            let ynabNormalized = ynabPayee.ToUpperInvariant().Trim()
            bankNormalized.Contains(ynabNormalized) ||
            ynabNormalized.Contains(bankNormalized) ||
            bankNormalized = ynabNormalized
        | _, _ -> false  // Can't match if payee is missing

    dateMatches && amountMatches && payeeMatches

/// Checks if a bank transaction might match a YNAB transaction by import_id
/// Import ID format: BB:<transactionId> (defined in Domain.ImportIdPrefix)
let matchesByImportId (bankTx: BankTransaction) (ynabTx: YnabTransaction) : bool =
    match ynabTx.ImportId with
    | None -> false
    | Some importId ->
        // Uses Domain.matchesImportId to ensure format consistency with YnabClient
        Shared.Domain.matchesImportId bankTx.Id importId

/// Detects the duplicate status of a bank transaction against existing YNAB transactions
/// Returns both the status and detailed diagnostic information about all checks performed
let detectDuplicate
    (config: DuplicateMatchConfig)
    (ynabTransactions: YnabTransaction list)
    (bankTx: BankTransaction)
    : DuplicateStatus =

    // Check for exact reference match (confirmed duplicate)
    let referenceMatch =
        ynabTransactions
        |> List.tryFind (matchesByReference bankTx)

    // Check for import_id match (confirmed duplicate)
    let importIdMatch =
        ynabTransactions
        |> List.tryFind (matchesByImportId bankTx)

    // Check for fuzzy match by date/amount/payee (possible duplicate)
    let fuzzyMatch =
        ynabTransactions
        |> List.tryFind (matchesByDateAmountPayee config bankTx)

    // Build diagnostic details about all checks
    let details: DuplicateDetectionDetails = {
        TransactionReference = bankTx.Reference
        ReferenceFoundInYnab = referenceMatch.IsSome
        ImportIdFoundInYnab = importIdMatch.IsSome
        FuzzyMatchDate = fuzzyMatch |> Option.map (fun tx -> tx.Date)
        FuzzyMatchAmount = fuzzyMatch |> Option.map (fun tx -> tx.Amount.Amount)
        FuzzyMatchPayee = fuzzyMatch |> Option.bind (fun tx -> tx.Payee)
    }

    // Determine status with priority: Reference > ImportId > Fuzzy > None
    match referenceMatch with
    | Some _ ->
        ConfirmedDuplicate (bankTx.Reference, details)
    | None ->
        match importIdMatch with
        | Some _ ->
            ConfirmedDuplicate (bankTx.Reference, details)
        | None ->
            match fuzzyMatch with
            | Some ynabTx ->
                let reason =
                    sprintf "Similar transaction found: %s on %s for %.2f"
                        (ynabTx.Payee |> Option.defaultValue "Unknown")
                        (ynabTx.Date.ToString("yyyy-MM-dd"))
                        ynabTx.Amount.Amount
                PossibleDuplicate (reason, details)
            | None ->
                NotDuplicate details

/// Marks all sync transactions with their duplicate status
let markDuplicates
    (ynabTransactions: YnabTransaction list)
    (syncTransactions: SyncTransaction list)
    : SyncTransaction list =

    syncTransactions
    |> List.map (fun syncTx ->
        let status = detectDuplicate defaultConfig ynabTransactions syncTx.Transaction
        { syncTx with DuplicateStatus = status }
    )

/// Counts duplicates in a list of sync transactions
let countDuplicates (transactions: SyncTransaction list) : {| Confirmed: int; Possible: int; None: int |} =
    let confirmed =
        transactions
        |> List.filter (fun tx ->
            match tx.DuplicateStatus with
            | ConfirmedDuplicate (_, _) -> true
            | _ -> false)
        |> List.length

    let possible =
        transactions
        |> List.filter (fun tx ->
            match tx.DuplicateStatus with
            | PossibleDuplicate (_, _) -> true
            | _ -> false)
        |> List.length

    {| Confirmed = confirmed; Possible = possible; None = transactions.Length - confirmed - possible |}
