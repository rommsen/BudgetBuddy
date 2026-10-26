/// "Lena fragen" an einer Importzeile (ynab-sync-d9e51, ADR 0056 und 0061). Reine Befehle
/// auf der Sitzungs-Transaktion: kein I/O, die Flag-Farbe kommt vom Aufrufer aus den
/// Settings.
module Server.ImportAskLena

open Shared.Domain

/// Der Grund, wenn keine gültige Lena-Farbe eingestellt ist (Wortlaut wie im Kassenbuch,
/// ADR 0056 Punkt 1).
let noColorReason = "In den Settings die Flag-Farbe für Lena wählen"

/// Der Grund, wenn die Zeile nicht mehr offen ist.
let notOpenReason = "Die Zeile ist übersprungen oder schon importiert"

/// Eine gespeicherte Farbe zählt nur, wenn sie eine der sechs YNAB-Farben ist (ADR 0061
/// Punkt 3); alles andere gilt als nicht gesetzt.
let validColor (stored: string option) : string option =
    match stored with
    | Some c when List.contains c ynabFlagColors -> Some c
    | _ -> None

let private isOpen (tx: SyncTransaction) : bool =
    match tx.Status with
    | Pending | AutoCategorized | ManualCategorized | NeedsAttention -> true
    | Skipped | Imported -> false

/// Setzt (`ask = true`) oder entfernt (`ask = false`) die Rückfrage. Bei einer
/// übersprungenen oder importierten Zeile wird jeder Befehl abgelehnt. Setzen braucht eine
/// gültige Lena-Farbe; Zurücknehmen nicht (es schickt nichts). Die Kategorie-Logik bleibt
/// unberührt: eine Zeile ohne Kategorie behält ihren Status.
let setAskLena (lenaColor: string option) (ask: bool) (tx: SyncTransaction) : Result<SyncTransaction, string> =
    if not (isOpen tx) then Error notOpenReason
    elif ask && (validColor lenaColor).IsNone then Error noColorReason
    else Ok { tx with AskLena = ask }

/// Überspringen hebt eine gesetzte Rückfrage auf.
let skip (tx: SyncTransaction) : SyncTransaction =
    { tx with Status = Skipped; AskLena = false }
