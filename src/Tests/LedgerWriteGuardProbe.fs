module LedgerWriteGuardProbe

// Gemeinsamer Test-Helfer für die Schreib-Sperre (ADR 0055 Punkt 6, ledger-kfd5m):
// "der Befehl wartet auf den Spiegel-Lauf". Jeder Kassenbuch-Befehl, der ein Ereignis
// schreibt und danach zu YNAB pusht, läuft unter `WriteGuard.exclusive`. Der Helfer hält
// einen echten, gesperrten Lauf (`runMirrorGuarded`) im Abruf fest, startet den Befehl,
// lässt den Test prüfen, dass noch kein Ereignis und kein Push da sind, und gibt den
// Lauf danach frei.
//
// Die Sperre und der Einzel-Flug-Schutz sind prozessweit, Expecto lässt Testlisten aber
// parallel laufen. Ein zweiter Test, der während eines festgehaltenen Laufs selbst
// `runMirrorGuarded` ruft, hängte sich an diesen Lauf an (single flight) und sähe seinen
// eigenen Abruf nie. Darum laufen alle Tests, die einen Lauf festhalten oder den
// Einzel-Flug-Schutz prüfen, über `serialized` nacheinander, auch über Dateien hinweg.

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Shared.Domain
open Ledger.CommandHandlers

let private serial = new SemaphoreSlim(1, 1)

/// Führt `body` aus, während kein anderer Test einen Spiegel-Lauf festhält oder den
/// Einzel-Flug-Schutz prüft.
let serialized (body: unit -> 'a) : 'a =
    serial.Wait()
    try
        body ()
    finally
        serial.Release() |> ignore

let private at = DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc)

/// Wie lange der Befehl höchstens laufen darf, ohne die Sperre: ein Befehl gegen die
/// In-Memory-DB mit Push-Probe ist in dieser Zeit fertig. Wartet er auf den Lauf, ist er
/// es nicht.
let private commandHeadStartMs = 300

/// GIVEN ein Spiegel-Lauf hängt im Abruf, WHEN `command` startet, THEN ist `command` nach
/// `commandHeadStartMs` nicht fertig, und `whileHeld` (Ereignisse und Push-Aufrufe prüfen)
/// läuft, solange der Lauf noch hängt. Danach wird der Lauf freigegeben; der Helfer
/// wartet auf Lauf und Befehl und liefert das Ergebnis des Befehls.
let commandWaitsForMirror (whileHeld: unit -> unit) (command: Async<'a>) : 'a =
    serialized (fun () ->
        let fetchStarted = TaskCompletionSource<unit>()
        let release = TaskCompletionSource<unit>()
        let fetchers : MirrorFetchers = {
            emptyMirrorFetchers with
                FetchTransactions =
                    fun _ ->
                        async {
                            fetchStarted.TrySetResult () |> ignore
                            do! Async.AwaitTask release.Task
                            return Ok { Observations = []; ServerKnowledge = 1L }
                        }
        }
        let mirrorTask =
            Async.StartAsTask(runMirrorGuarded at fetchers (YnabBudgetId $"budget-write-guard-{Guid.NewGuid()}"))
        try
            Expect.isTrue (fetchStarted.Task.Wait 5000) "der Lauf hängt im Abruf"
            let commandTask = Async.StartAsTask command
            Thread.Sleep commandHeadStartMs
            Expect.isFalse commandTask.IsCompleted "der Befehl wartet auf den Lauf"
            whileHeld ()
            release.TrySetResult () |> ignore
            Expect.isTrue (mirrorTask.Wait 5000) "Lauf beendet"
            Expect.isTrue (commandTask.Wait 5000) "Befehl beendet"
            commandTask.Result
        finally
            release.TrySetResult () |> ignore)

/// Ein Spiegel-Auslöser wie `triggerMirrorInBackground`, aber synchron: startet einen
/// gesperrten Lauf (`runMirrorGuarded`) und wartet höchstens `deadlineMs` auf ihn. Ruft
/// ein Befehl ihn aus dem gesperrten Abschnitt heraus, wartet der Lauf auf die Sperre,
/// die der Befehl hält, und die Frist läuft ab (`FinishedInTime = Some false`). Die Frist
/// verhindert, dass ein falsch platzierter Auslöser den ganzen Testlauf festhält.
type SyncMirrorTrigger(deadlineMs: int) =
    member val FinishedInTime : bool option = None with get, set
    member this.Trigger () : unit =
        let run =
            Async.StartAsTask(runMirrorGuarded at emptyMirrorFetchers (YnabBudgetId $"budget-trigger-{Guid.NewGuid()}"))
        this.FinishedInTime <- Some (run.Wait deadlineMs)
