module Components.SyncFlow.Views.ViewHelpers

open System
open Shared.Domain
open Components.SyncFlow.Types

/// Counts for the bottom action bar
type ImportCounts = {
    Total: int
    ToImport: int
    NeedCategory: int
    Duplicates: int
    Skipped: int
}

/// Progress bar segment percentages
type ProgressSegments = {
    ReadyPct: float
    AttentionPct: float
    SkippedPct: float
}

/// Groups transactions by booking date (day), sorted descending (most recent first).
let groupTransactionsByDate (transactions: SyncTransaction list) : (DateTime * SyncTransaction list) list =
    transactions
    |> List.groupBy (fun tx -> tx.Transaction.BookingDate.Date)
    |> List.sortByDescending fst

/// Common German abbreviations that should not be title-cased
let private commonAbbreviations =
    Set.ofList [ "DHL"; "BMW"; "ING"; "DKB"; "VW"; "SAP"; "ADAC"; "DB"; "TUI"; "AOK"; "HUK"; "LVM" ]

/// Converts ALL-CAPS payee names to title case, preserving known abbreviations.
let titleCasePayee (name: string) =
    if String.IsNullOrWhiteSpace name then name
    elif name.Length <= 2 then name
    elif commonAbbreviations.Contains(name.Trim().ToUpperInvariant()) then name
    elif name = name.ToUpperInvariant() then
        name.Split(' ')
        |> Array.map (fun word ->
            if word.Length <= 2 then word
            elif commonAbbreviations.Contains(word.ToUpperInvariant()) then word
            else word.[0..0].ToUpper() + word.[1..].ToLower())
        |> String.concat " "
    else name

/// Calculates import counts for the action bar.
/// ToImport = not Skipped and not Imported (regardless of duplicate status).
/// NeedCategory = ToImport + no CategoryId + not auto/manual categorized.
/// Duplicates = ConfirmedDuplicate (any status).
/// Skipped = Status = Skipped.
let calculateImportCounts (transactions: SyncTransaction list) : ImportCounts =
    let (toImport, needCat, dups, skipped) =
        transactions
        |> List.fold (fun (ti, nc, d, s) tx ->
            let d' =
                match tx.DuplicateStatus with
                | ConfirmedDuplicate _ -> d + 1
                | _ -> d
            if tx.Status = Skipped then
                (ti, nc, d', s + 1)
            elif tx.Status <> Imported then
                let nc' =
                    match tx.DuplicateStatus with
                    | ConfirmedDuplicate _ -> nc
                    | _ ->
                        match tx.CategoryId with
                        | Some _ -> nc
                        | None ->
                            match tx.Status with
                            | AutoCategorized | ManualCategorized -> nc
                            | _ -> nc + 1
                (ti + 1, nc', d', s)
            else
                (ti, nc, d', s)
        ) (0, 0, 0, 0)
    { Total = transactions.Length
      ToImport = toImport
      NeedCategory = needCat
      Duplicates = dups
      Skipped = skipped }

/// Calculates progress bar segment percentages from import counts.
let calculateProgressSegments (counts: ImportCounts) : ProgressSegments =
    if counts.Total = 0 then
        { ReadyPct = 0.0; AttentionPct = 0.0; SkippedPct = 0.0 }
    else
        let total = float counts.Total
        { ReadyPct = float (counts.ToImport - counts.NeedCategory) / total * 100.0
          AttentionPct = float counts.NeedCategory / total * 100.0
          SkippedPct = float (counts.Duplicates + counts.Skipped) / total * 100.0 }

/// Sums the amounts of a list of transactions, returning the total in milliunits (int64).
/// Decimal amounts are multiplied by 1000 and rounded to int64.
let sumDailyMilliunits (transactions: SyncTransaction list) : int64 =
    transactions
    |> List.sumBy (fun tx -> int64 (tx.Transaction.Amount.Amount * 1000m))
