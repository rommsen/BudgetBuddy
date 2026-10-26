module SyncFlowStateTests

// Regressionstest für ledger-mshd8: seit ledger-qvt84 Iteration 3 kann
// `importToYnab` bei einem Teilfehlschlag `Error` liefern, obwohl andere
// Buchungen im selben Batch schon erfolgreich importiert und gespeichert
// wurden (`src/Server/Api.fs`, Kommentar "Persisted regardless of a ledger
// write failure"). `ImportCompleted (Error err)` lud die Session bislang
// nicht neu, der Client zeigte die bereits importierten Buchungen weiterhin
// mit ihrem alten Stand an.
//
// Getestet wird direkt gegen die reine `update`-Funktion (ADR 0020: Reduzierer
// testbar), ohne den Fable.Remoting-Proxy anzufassen, dasselbe Muster wie
// `LedgerPageTests.fs`s `dispatched`-Helfer.

open Expecto
open Shared.Domain
open Shared.Api
open Components.SyncFlow.Types
open Components.SyncFlow.State

/// Führt ein `Cmd` aus und sammelt die abgeschickten Nachrichten ein. Zulässig
/// nur für Kommandos aus `Cmd.ofMsg` / `Cmd.batch` / `Cmd.map` (ein `Cmd.OfAsync`
/// würde hier den Fable-only-Proxy anfassen und unter .NET werfen, ADR 0020).
let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

let private freshModel () : Model = fst (init ())

[<Tests>]
let syncFlowStateTests =
    testList "SyncFlow-Reduzierer" [

        test "ImportCompleted (Error) lädt die Session neu" {
            // Bug: vor dem Fix lieferte dieser Zweig nur `Cmd.none`, also keine
            // Nachricht. Ein Teilfehlschlag ließ bereits gespeicherte Buchungen
            // im Client auf altem Stand stehen, bis Roman manuell neu lud.
            let error = SyncError.YnabImportFailed (1, "Kassenbuch-Schreiben fehlgeschlagen")
            let _, cmd, _ = update (ImportCompleted (Error error)) (freshModel ())
            Expect.contains (dispatched cmd) LoadCurrentSession
                "Ein Teilfehlschlag muss die Session neu laden, sonst zeigt der Client veraltete Daten"
        }

        test "ImportCompleted (Ok) lädt weiterhin die Session neu (unverändertes Verhalten)" {
            // Intakt-Szenario: der Erfolgspfad lud die Session schon vor diesem
            // Fix neu und darf sich durch die Änderung am Fehlerpfad nicht ändern.
            let result: ImportResult = { CreatedCount = 2; DuplicateTransactionIds = []; LenaFlagNotSentCount = 0 }
            let _, cmd, _ = update (ImportCompleted (Ok result)) (freshModel ())
            Expect.contains (dispatched cmd) LoadCurrentSession
                "Der Erfolgspfad lädt die Session weiterhin neu"
        }
    ]
