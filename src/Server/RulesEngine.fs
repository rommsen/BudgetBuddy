module Server.RulesEngine

open System
open System.Text.RegularExpressions
open Shared.Domain

// ============================================
// Types
// ============================================

/// A rule with its compiled regular expression for performance.
/// Purpose: Avoids recompiling regex patterns on every transaction.
type CompiledRule = {
    Rule: Rule
    Regex: Regex
}

// ============================================
// Pattern Detection
// ============================================

/// Amazon transaction patterns to detect.
/// Purpose: Identifies Amazon purchases that need manual order matching.
let private amazonPatterns = [
    @"AMAZON\s*(PAYMENTS|EU|DE)?"
    @"AMZN\s*MKTP"
    @"Amazon\.de"
    @"AMAZON\s*\.DE"
]

/// PayPal transaction patterns to detect.
/// Purpose: Identifies PayPal transactions that need activity lookup.
let private paypalPatterns = [
    @"PAYPAL\s*\*"
    @"PP\.\d+"
    @"PAYPAL"
]

/// Regex pattern for Amazon order IDs (e.g., ABC-1234567-1234567)
/// Handles optional 2-digit Comdirect line number prefix (e.g., 01305-...)
let private amazonOrderIdPattern = @"(?:(?:^|\s)\d{2})?([A-Z0-9]{3}-\d{7}-\d{7})"

/// Extracts Amazon order ID from transaction text (payee + memo)
let private extractAmazonOrderId (transaction: BankTransaction) : string option =
    let text =
        match transaction.Payee with
        | Some payee -> payee + " " + transaction.Memo
        | None -> transaction.Memo

    let regex = new Regex(amazonOrderIdPattern, RegexOptions.None)
    let matchResult = regex.Match(text)
    if matchResult.Success then Some matchResult.Groups.[1].Value
    else None

/// Generates Amazon link - deep link to specific order if ID found, else order history
let private generateAmazonLink (transaction: BankTransaction) : ExternalLink =
    match extractAmazonOrderId transaction with
    | Some orderId ->
        {
            Label = $"Bestellung {orderId}"
            Url = $"https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID={orderId}"
        }
    | None ->
        {
            Label = "Amazon Orders"
            Url = "https://www.amazon.de/gp/your-account/order-history"
        }

/// Generates a PayPal activity link for transactions.
/// Purpose: Provides quick access to PayPal activity for transaction lookup.
let private generatePayPalLink (transaction: BankTransaction) : ExternalLink =
    {
        Label = "PayPal Activity"
        Url = "https://www.paypal.com/activities"
    }

/// Detects if a transaction matches Amazon patterns and generates external link.
/// Returns: Some link if Amazon transaction detected, None otherwise.
let private detectAmazon (transaction: BankTransaction) : ExternalLink option =
    let text =
        match transaction.Payee with
        | Some payee -> payee + " " + transaction.Memo
        | None -> transaction.Memo

    let isAmazon =
        amazonPatterns
        |> List.exists (fun pattern ->
            let regex = new Regex(pattern, RegexOptions.IgnoreCase)
            regex.IsMatch(text)
        )

    if isAmazon then Some (generateAmazonLink transaction)
    else None

/// Detects if a transaction matches PayPal patterns and generates external link.
/// Returns: Some link if PayPal transaction detected, None otherwise.
let private detectPayPal (transaction: BankTransaction) : ExternalLink option =
    let text =
        match transaction.Payee with
        | Some payee -> payee + " " + transaction.Memo
        | None -> transaction.Memo

    let isPayPal =
        paypalPatterns
        |> List.exists (fun pattern ->
            let regex = new Regex(pattern, RegexOptions.IgnoreCase)
            regex.IsMatch(text)
        )

    if isPayPal then Some (generatePayPalLink transaction)
    else None

/// Detects special transaction patterns (Amazon, PayPal) and returns external links.
/// Purpose: Provides users with quick access to detailed transaction information.
let detectSpecialTransaction (transaction: BankTransaction) : ExternalLink list =
    [
        detectAmazon transaction
        detectPayPal transaction
    ]
    |> List.choose id

// ============================================
// Rule Compilation
// ============================================

/// Compiles a rule into a CompiledRule with a Regex pattern.
/// Purpose: Pre-compiles regex patterns for performance during classification.
/// Returns: Ok with CompiledRule or Error with failure reason.
let compileRule (rule: Rule) : Result<CompiledRule, string> =
    try
        let pattern =
            match rule.PatternType with
            | Exact ->
                // Escape special regex characters and wrap with anchors
                "^" + Regex.Escape(rule.Pattern) + "$"
            | Contains ->
                // Escape special regex characters
                Regex.Escape(rule.Pattern)
            | Regex ->
                // Use pattern as-is (user-provided regex)
                rule.Pattern

        let regex = new Regex(pattern, RegexOptions.IgnoreCase)

        Ok { Rule = rule; Regex = regex }
    with
    | ex -> Error $"Failed to compile pattern '{rule.Pattern}': {ex.Message}"

/// Compiles multiple rules and collects all errors.
/// Purpose: Validates all rules upfront before use in classification.
/// Returns: Ok with list of compiled rules, or Error with all compilation errors.
let compileRules (rules: Rule list) : Result<CompiledRule list, string list> =
    let results = rules |> List.map compileRule

    let errors =
        results
        |> List.choose (fun r ->
            match r with
            | Error e -> Some e
            | Ok _ -> None
        )

    if not (List.isEmpty errors) then
        Error errors
    else
        let compiled =
            results
            |> List.choose (fun r ->
                match r with
                | Ok c -> Some c
                | Error _ -> None
            )
        Ok compiled

// ============================================
// Transaction Classification
// ============================================

/// Extracts the text to match against based on the target field.
/// Purpose: Determines which transaction field(s) to use for pattern matching.
let getMatchText (transaction: BankTransaction) (targetField: TargetField) : string =
    match targetField with
    | Payee ->
        transaction.Payee |> Option.defaultValue ""
    | Memo ->
        transaction.Memo
    | Combined ->
        let payee = transaction.Payee |> Option.defaultValue ""
        payee + " " + transaction.Memo

/// Classifies a single transaction using compiled rules.
/// Purpose: Finds the first matching rule by priority order.
/// Returns: Some (rule, categoryId) if a match is found, None otherwise.
let classify
    (compiledRules: CompiledRule list)
    (transaction: BankTransaction)
    : (Rule * YnabCategoryId) option =

    // Rules are already sorted by priority in the list
    compiledRules
    |> List.tryFind (fun compiled ->
        if not compiled.Rule.Enabled then
            false
        else
            let matchText = getMatchText transaction compiled.Rule.TargetField
            compiled.Regex.IsMatch(matchText)
    )
    |> Option.map (fun compiled -> (compiled.Rule, compiled.Rule.CategoryId))

/// Classifies a list of transactions using the provided rules.
/// Purpose: Applies rules engine to all transactions and detects special patterns.
/// Returns: List of SyncTransaction with categorization results.
let classifyTransactions
    (rules: Rule list)
    (transactions: BankTransaction list)
    : Result<SyncTransaction list, string list> =

    // Compile all rules first
    match compileRules rules with
    | Error errors -> Error errors
    | Ok compiledRules ->
        let syncTransactions =
            transactions
            |> List.map (fun transaction ->
                // Detect special patterns first
                let externalLinks = detectSpecialTransaction transaction
                let hasSpecialPattern = not (List.isEmpty externalLinks)

                // Try to classify with rules
                match classify compiledRules transaction with
                | Some (matchedRule, categoryId) ->
                    {
                        Transaction = transaction
                        Status = if hasSpecialPattern then NeedsAttention else AutoCategorized
                        CategoryId = Some categoryId
                        CategoryName = Some matchedRule.CategoryName
                        MatchedRuleId = Some matchedRule.Id
                        PayeeOverride = matchedRule.PayeeOverride
                        ExternalLinks = externalLinks
                        UserNotes = None
                        DuplicateStatus = NotDuplicate (emptyDetectionDetails transaction.Reference)  // Will be updated by DuplicateDetection
                        YnabImportStatus = NotAttempted
                        Splits = None
                    }
                | None ->
                    {
                        Transaction = transaction
                        Status = if hasSpecialPattern then NeedsAttention else Pending
                        CategoryId = None
                        CategoryName = None
                        MatchedRuleId = None
                        PayeeOverride = None
                        ExternalLinks = externalLinks
                        UserNotes = None
                        DuplicateStatus = NotDuplicate (emptyDetectionDetails transaction.Reference)  // Will be updated by DuplicateDetection
                        YnabImportStatus = NotAttempted
                        Splits = None
                    }
            )

        Ok syncTransactions
