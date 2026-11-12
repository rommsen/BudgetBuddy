module Tests.CmdTiming

// infrastructure-t5m2b, Iteration 2: Zeitgeber-Cmds (Debounce, Rückfall, Ablauf) liefen bisher nur im
// Browser ab. Unter .NET startet Elmish `Cmd.OfAsync` per `Async.Start`, der Cmd lässt sich also
// ausführen, solange er nicht `Client.Api` berührt. Gewartet wird mit Frist, nie ohne.

open System.Collections.Concurrent
open System.Diagnostics
open Expecto

/// Führt die Effekte eines Cmd aus und gibt die erste abgeschickte Nachricht samt vergangener Zeit
/// zurück. Scheitert, wenn innerhalb von `ms` nichts kommt.
let firstDispatchedAfter (ms: int) (cmd: Elmish.Cmd<'msg>) : 'msg * int64 =
    let collected = new BlockingCollection<'msg>()
    let clock = Stopwatch.StartNew()
    cmd |> List.iter (fun effect -> effect collected.Add)
    let mutable msg = Unchecked.defaultof<'msg>
    if collected.TryTake(&msg, ms) then msg, clock.ElapsedMilliseconds
    else failtest $"keine Nachricht innerhalb von {ms} ms"

/// Erwartet genau `expected` nach frühestens `delayMs` (100 ms Spielraum für die Uhr).
let expectAfter (delayMs: int) (expected: 'msg) (cmd: Elmish.Cmd<'msg>) =
    let msg, elapsed = firstDispatchedAfter (delayMs + 3000) cmd
    Expect.equal msg expected "die Nachricht nach Ablauf"
    Expect.isGreaterThanOrEqual elapsed (int64 delayMs - 100L) $"erst nach {delayMs} ms"

/// Wie `expectAfter`, überspringt aber sofort abgeschickte Nachrichten, auf die `ignored` passt (etwa das
/// Neuladen der Historie neben einem Zeitgeber, ledger-nc708).
let expectAfterIgnoring (ignored: 'msg -> bool) (delayMs: int) (expected: 'msg) (cmd: Elmish.Cmd<'msg>) =
    let collected = new BlockingCollection<'msg>()
    let clock = Stopwatch.StartNew()
    cmd |> List.iter (fun effect -> effect (fun msg -> if not (ignored msg) then collected.Add msg))
    let mutable msg = Unchecked.defaultof<'msg>
    if not (collected.TryTake(&msg, delayMs + 3000)) then failtest $"keine Nachricht innerhalb von {delayMs + 3000} ms"
    Expect.equal msg expected "die Nachricht nach Ablauf"
    Expect.isGreaterThanOrEqual clock.ElapsedMilliseconds (int64 delayMs - 100L) $"erst nach {delayMs} ms"
