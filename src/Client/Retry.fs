namespace Client

open Fable.Remoting.Client

/// Wiederholt lesende Abfragen und Befehle mit Befehls-Id, wenn der Browser die Verbindung abbricht.
/// Befund 2026-10-24 (Firefox, nach einem Deploy): "Http error (0)", obwohl der Server
/// die Anfrage mit 200 beantwortet hat. `reading` nur für lesende Aufrufe: ein abgebrochener
/// Schreibaufruf kann beim Server angekommen sein. Schreibende Aufrufe laufen nur über `command`,
/// dessen zweiter Versuch dieselbe Befehls-Id trägt (ADR 0084 Punkt 8). Ein Schreibaufruf ohne Id
/// wird nicht wiederholt.
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

    /// Pause vor dem einen zweiten Versuch eines Befehls.
    let writingDelaysMs : int list = [ 400 ]

    /// Eine schreibende Absicht mit Befehls-Id: Die Id kommt einmal aus `newId`, beide Versuche bekommen
    /// dieselbe. Höchstens eine Wiederholung, nur nach Status 0. So erkennt der Server die Wiederholung
    /// (ADR 0084 Punkt 8).
    let commandWith (sleep: int -> Async<unit>) (newId: unit -> System.Guid) (call: System.Guid -> Async<'a>) : Async<'a> =
        async {
            let commandId = newId ()
            return! withRetry isNetworkError writingDelaysMs sleep (fun () -> call commandId)
        }

    /// `commandWith` mit der echten Pause und einer frischen Guid je Absicht (Browser).
    let command (call: System.Guid -> Async<'a>) : Async<'a> = commandWith Async.Sleep System.Guid.NewGuid call
