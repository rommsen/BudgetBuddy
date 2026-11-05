namespace Client

open Fable.Remoting.Client

/// Wiederholt lesende Abfragen, wenn der Browser die Verbindung abbricht.
/// Befund 2026-10-24 (Firefox, nach einem Deploy): "Http error (0)", obwohl der Server
/// die Anfrage mit 200 beantwortet hat. `reading` nur für lesende Aufrufe: ein abgebrochener
/// Schreibaufruf kann beim Server angekommen sein. Schreibende Aufrufe mit Befehls-Id laufen
/// über `command` (ADR 0084 Punkt 8).
[<RequireQualifiedAccess>]
module Retry =
    /// Pausen vor dem zweiten und dritten Versuch (zusammen 1,6 Sekunden).
    let defaultDelaysMs : int list = [ 400; 1200 ]

    /// Netzwerkfehler im Browser: Fable.Remoting meldet Status 0, wenn keine
    /// Antwort vom Server vorliegt. Andere Ausnahmen und andere Status gelten nicht.
    let isNetworkError (ex: exn) : bool =
        match ex with
        | :? ProxyRequestException as pre -> pre.StatusCode = 0
        | _ -> false

    /// Führt `run` aus und wiederholt es nach jeder Pause aus `delaysMs`, solange der
    /// Fehler als Netzwerkfehler gilt. Ergebnisse (auch fachliche Error-Werte) kommen
    /// unverändert zurück. Scheitert der letzte Versuch, wird dessen Ausnahme weitergereicht.
    let withRetry
        (isRetryable: exn -> bool)
        (delaysMs: int list)
        (sleep: int -> Async<unit>)
        (run: unit -> Async<'a>)
        : Async<'a> =
        let rec attempt (remaining: int list) : Async<'a> =
            async {
                let! outcome = run () |> Async.Catch
                match outcome with
                | Choice1Of2 value -> return value
                | Choice2Of2 ex ->
                    match remaining with
                    | delay :: rest when isRetryable ex ->
                        do! sleep delay
                        return! attempt rest
                    | _ -> return raise ex
            }
        attempt delaysMs

    /// Umhüllt eine lesende Remoting-Abfrage mit den Standardwerten (Browser).
    let reading (call: 'arg -> Async<'a>) : 'arg -> Async<'a> =
        fun arg -> withRetry isNetworkError defaultDelaysMs Async.Sleep (fun () -> call arg)

    /// Pause vor dem einen zweiten Versuch eines schreibenden Aufrufs.
    let writingDelaysMs : int list = [ 400 ]

    /// Umhüllt einen schreibenden Aufruf: höchstens eine Wiederholung, nur nach Status 0. Nur für Aufrufe,
    /// deren Wiederholung der Server über die Befehls-Id erkennt.
    let writingWith (sleep: int -> Async<unit>) (call: 'arg -> Async<'a>) : 'arg -> Async<'a> =
        fun arg -> withRetry isNetworkError writingDelaysMs sleep (fun () -> call arg)

    /// `writingWith` mit der echten Pause (Browser).
    let writing (call: 'arg -> Async<'a>) : 'arg -> Async<'a> = writingWith Async.Sleep call

    /// Eine schreibende Absicht mit Befehls-Id: Die Id kommt einmal aus `newId`, beide Versuche von
    /// `writingWith` bekommen dieselbe. So erkennt der Server die Wiederholung (ADR 0084 Punkt 8).
    let commandWith (sleep: int -> Async<unit>) (newId: unit -> System.Guid) (call: System.Guid -> Async<'a>) : Async<'a> =
        async {
            let commandId = newId ()
            return! writingWith sleep call commandId
        }

    /// `commandWith` mit der echten Pause und einer frischen Guid je Absicht (Browser).
    let command (call: System.Guid -> Async<'a>) : Async<'a> = commandWith Async.Sleep System.Guid.NewGuid call
