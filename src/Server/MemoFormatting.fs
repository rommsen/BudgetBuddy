/// Reine Memo-Formatierung ohne HTTP-Abhängigkeit, gemeinsam genutzt von
/// `Server.YnabClient` (baut den tatsächlich gesendeten YNAB-Request) und
/// `Ledger.Domain` (baut das Kassenbuch-Ereignis, das genau diesem Request
/// entsprechen muss, ledger-jkb0f). Vorher lagen `memoLimit`/`truncateSplitMemo` nur
/// in `YnabClient.fs`; eine zweite, unabhängige Kürzung im Kassenbuch wäre genau die
/// Fehlerklasse, die dieser Task behebt (zwei Kopien laufen auseinander).
module Server.MemoFormatting

/// YNAB memo character limit (testing with 300, may need adjustment)
let memoLimit = 300

/// Compresses multiple consecutive whitespace characters into a single space
let compressWhitespace (text: string) =
    System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim()

/// Simple truncation for split memos (no reference needed - parent has it)
let truncateSplitMemo (memo: string) =
    let compressed = compressWhitespace memo
    if compressed.Length > memoLimit then
        compressed.Substring(0, memoLimit - 3) + "..."
    else
        compressed
