module ImportAskLenaClientTests

// "Lena fragen" an der Importzeile, Client-Seite (ynab-sync-d9e51): die Aktion je Zeile, der
// Reduzierer und die Zusicherungen am Quelltext der Zeile. Message-Pfade, die `Client.Api`
// berühren, sind unter .NET nicht testbar (Fable.Remoting-Proxy), deshalb prüft der Reduzierer
// hier nur die Antworten und die Aktionswahl.

open System
open Expecto
open Shared.Domain
open Types
open Components.SyncFlow.Types
open Components.SyncFlow.State

let private tx (status: TransactionStatus) (ask: bool) : SyncTransaction = {
    Transaction = {
        Id = TransactionId "tx-d9e51-client"
        BookingDate = DateTime(2026, 10, 26)
        Amount = { Amount = -5m; Currency = "EUR" }
        Payee = Some "Rewe"
        Memo = ""
        Reference = "REF-d9e51"
        RawData = "{}"
    }
    Status = status
    CategoryId = None
    CategoryName = None
    MatchedRuleId = None
    PayeeOverride = None
    ExternalLinks = []
    UserNotes = None
    DuplicateStatus = NotDuplicate (emptyDetectionDetails "REF-d9e51")
    YnabImportStatus = NotAttempted
    Splits = None
    SuggestedByOrderId = None
    ReviewSteps = []
    AskLena = ask
}

let private modelWith (txs: SyncTransaction list) : Model =
    { fst (init ()) with SyncTransactions = Success txs }

let private rowSource () =
    let root = IO.Directory.GetParent(IO.Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName
    IO.File.ReadAllText(IO.Path.Combine(root, "src", "Client", "Components", "SyncFlow", "Views", "TransactionRow.fs"))

[<Tests>]
let askLenaClientTests =
    testList "Import Lena fragen: Client" [

        testCase "offene Zeile ohne Rückfrage bietet Lena fragen an" <| fun () ->
            for status in [ Pending; AutoCategorized; ManualCategorized; NeedsAttention ] do
                match askLenaActionFor (Some "purple") (tx status false) with
                | Some a ->
                    Expect.equal a.Label "Lena fragen" "Beschriftung"
                    Expect.isTrue a.Target "setzt die Rückfrage"
                    Expect.isNone a.DisabledReason "nicht gesperrt"
                | None -> failtest $"keine Aktion bei {status}"

        testCase "mit Rückfrage heißt die Aktion Rückfrage zurücknehmen und ist nie gesperrt" <| fun () ->
            match askLenaActionFor None (tx Pending true) with
            | Some a ->
                Expect.equal a.Label "Rückfrage zurücknehmen" "Beschriftung"
                Expect.isFalse a.Target "nimmt zurück"
                Expect.isNone a.DisabledReason "auch ohne Farbe möglich"
            | None -> failtest "keine Aktion"

        testCase "ohne Flag-Farbe ist Lena fragen gesperrt und nennt den Grund" <| fun () ->
            for color in [ None; Some "pink" ] do
                match askLenaActionFor color (tx Pending false) with
                | Some a -> Expect.equal a.DisabledReason (Some "In den Settings die Flag-Farbe für Lena wählen") "Grund"
                | None -> failtest "keine Aktion"

        testCase "übersprungene und importierte Zeilen haben keine Aktion" <| fun () ->
            Expect.isNone (askLenaActionFor (Some "purple") (tx Skipped false)) "Skipped"
            Expect.isNone (askLenaActionFor (Some "purple") (tx Imported false)) "Imported"

        testCase "AskLenaSet ersetzt die Zeile, andere bleiben" <| fun () ->
            let other = { tx Pending false with Transaction = { (tx Pending false).Transaction with Id = TransactionId "andere" } }
            let model = modelWith [ tx Pending false; other ]
            let updated = tx Pending true
            let model', _, _ = update (AskLenaSet (Ok updated)) model
            match model'.SyncTransactions with
            | Success [ a; b ] ->
                Expect.isTrue a.AskLena "Rückfrage gesetzt"
                Expect.isFalse b.AskLena "andere Zeile unberührt"
            | _ -> failtest "Liste verloren"

        testCase "AskLenaSet (Error) zeigt den Grund als Toast und lässt die Liste" <| fun () ->
            let model = modelWith [ tx Pending false ]
            let model', _, ext = update (AskLenaSet (Error (SyncError.AskLenaRejected "Grund d9e51"))) model
            Expect.equal model'.SyncTransactions model.SyncTransactions "Liste unverändert"
            Expect.equal ext (ShowToast ("Grund d9e51", ToastError)) "Toast mit dem Grund"

        testCase "LenaFlagColorLoaded merkt sich die Farbe" <| fun () ->
            let model', _, _ = update (LenaFlagColorLoaded (Some "purple")) (fst (init ()))
            Expect.equal model'.LenaFlagColor (Some "purple") "Farbe im Modell"

        testCase "die Zeile zeigt Kennzeichen und Aktion, gesperrt mit Grund" <| fun () ->
            let src = rowSource ()
            Expect.stringContains src "\"Rückfrage Lena\"" "Kennzeichen im Wortlaut des Kassenbuchs"
            Expect.stringContains src "Badge.Purple" "violett wie im Kassenbuch"
            Expect.stringContains src "SetAskLena (tx.Transaction.Id, action.Target)" "Aktion löst den Befehl aus"
            Expect.stringContains src "prop.disabled action.DisabledReason.IsSome" "gesperrt ohne Farbe"
            Expect.stringContains src "prop.text blockedBecause" "Grund sichtbar"
    ]
