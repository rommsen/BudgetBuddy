module Tests.BudgetCommandCmdTests

// budgeting-qm5x9, ADR 0084 Punkt 8: Die vier Budget-Entscheidungen (Übernehmen, Verschieben, Zuweisen,
// Pausieren) schicken eine Befehls-Id mit, und nach Status 0 wiederholt der Client den Aufruf einmal mit
// derselben Id. Geprüft wird der echte Cmd aus `Components.Budget.State`; der Remoting-Aufruf ist ein
// Fake, der beim ersten Mal mit Status 0 scheitert und beim zweiten Mal antwortet. Die Pause vor dem
// zweiten Versuch ist die echte aus `Retry.command` (400 ms).

open System
open System.Collections.Concurrent
open Expecto
open Fable.Remoting.Client
open Shared.Domain
open Shared.Budgeting
open Components.Budget.Types
open Components.Budget.State
open Tests.CmdTiming

let private statusZero () : exn =
    ProxyRequestException({ StatusCode = 0; ResponseBody = "" }, "Http error (0)", "") :> exn

/// Fake-Aufruf: scheitert beim ersten Mal mit Status 0, antwortet danach mit `answer`. Zeichnet jede
/// Anfrage auf.
let private flakyCall (answer: 'response) =
    let seen = ConcurrentQueue<string * 'request>()
    let call (month: string) (request: 'request) : Async<'response> =
        async {
            seen.Enqueue((month, request))
            if seen.Count = 1 then return raise (statusZero ())
            else return answer
        }
    seen, call

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private monthKey = "2026-11-01"
let private report : ApplyReport =
    { ApplicationId = "a1"; Month = monthKey; Lines = []; ActualToBeBudgeted = None }

/// Erwartet zwei Aufrufe für den Monat, beide mit derselben gesetzten Befehls-Id.
let private expectSameIdTwice (seen: ConcurrentQueue<string * 'request>) (commandId: 'request -> Guid option) =
    let calls = List.ofSeq seen
    Expect.equal (calls |> List.map fst) [ monthKey; monthKey ] "zwei Aufrufe für den Monat"
    let ids = calls |> List.map (snd >> commandId)
    Expect.isSome ids[0] "der erste Versuch trägt eine Befehls-Id"
    Expect.equal ids[1] ids[0] "der zweite Versuch trägt dieselbe Id"

[<Tests>]
let tests =
    testList "Budget-Entscheidungen wiederholen nach Status 0 mit derselben Befehls-Id" [
        test "Übernehmen schickt beide Versuche mit derselben Befehls-Id und meldet die Antwort" {
            let preview : TargetPreview =
                { Month = monthKey; Lines = []; Skipped = []; Total = eur 0m
                  ToBeBudgeted = eur 0m; ToBeBudgetedAfter = eur 0m; CommandId = None }
            let seen, call = flakyCall (Ok (Applied report))
            expectAfter 400 (TargetsApplied (Ok (Applied report))) (applyCmdWith call preview)
            expectSameIdTwice seen (fun (p: TargetPreview) -> p.CommandId)
        }

        test "Verschieben schickt beide Versuche mit derselben Befehls-Id und meldet die Antwort" {
            let request : MoveRequest =
                { Source = ToBeBudgetedSide; Target = CategorySide (Guid.NewGuid()); Amount = eur 20m; CommandId = None }
            let seen, call = flakyCall (Ok report)
            expectAfter 400 (MoveAnswered (Ok report)) (moveCmdWith call monthKey request)
            expectSameIdTwice seen (fun (r: MoveRequest) -> r.CommandId)
        }

        test "Zuweisen schickt beide Versuche mit derselben Befehls-Id und meldet die Antwort" {
            let write : AssignWrite =
                { CategoryId = Guid.NewGuid(); Month = monthKey; Assigned = eur 50m; State = WriteRunning }
            let seen, call = flakyCall (Ok report)
            expectAfter 400 (AssignAnswered (Ok report)) (assignCmdWith call write)
            expectSameIdTwice seen (fun (r: AssignRequest) -> r.CommandId)
            let _, sent = Seq.head seen
            Expect.equal (sent.CategoryId, sent.Assigned) (write.CategoryId, write.Assigned) "Kategorie und Betrag aus der Schlange"
        }

        test "Pausieren schickt beide Versuche mit derselben Befehls-Id und meldet die Antwort" {
            let request : TargetPauseRequest =
                { CategoryId = Guid.NewGuid(); Intent = PauseTarget; CommandId = None }
            let seen, call = flakyCall (Ok (Some PausedInBb))
            let expected = TargetPauseAnswered (request.CategoryId, monthKey, Ok (Some PausedInBb))
            expectAfter 400 expected (pauseCmdWith call monthKey request)
            expectSameIdTwice seen (fun (r: TargetPauseRequest) -> r.CommandId)
        }
    ]
