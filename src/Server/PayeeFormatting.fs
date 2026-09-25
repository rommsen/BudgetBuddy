/// Reine Payee-Auflösung ohne HTTP-Abhängigkeit, gemeinsam genutzt von
/// `Server.YnabClient` (baut den tatsächlich gesendeten YNAB-Request) und
/// `Ledger.Domain` (baut das Kassenbuch-Ereignis, das genau diesem Request
/// entsprechen muss, ledger-jkb0f). Vorher fiel `YnabClient.buildTransactionRequest`
/// bei fehlendem Bank-Payee auf den festen Text "Unknown" zurück, das Kassenbuch-
/// Ereignis dagegen auf `None`: der nächste Spiegel-Lauf sah "Unknown" gegen `None`
/// und erzeugte ein falsches "in YNAB geändert"-Ereignis (Verifier-Fund, Iteration 1).
/// Eine zweite, unabhängige Kopie dieser Regel wäre genau die Fehlerklasse, die dieser
/// Task behebt (zwei Kopien laufen auseinander).
module Server.PayeeFormatting

/// Der Payee-Name, wie er tatsächlich im YNAB-Request steht: ein aktiver Override hat
/// Vorrang, sonst der rohe Bank-Payee, sonst der feste YNAB-Text "Unknown". Liefert
/// niemals `None`, weil YNAB selbst nie ohne Payee-Namen sendet.
let resolvePayeeName (payeeOverride: string option) (bankPayee: string option) : string =
    payeeOverride
    |> Option.orElse bankPayee
    |> Option.defaultValue "Unknown"
