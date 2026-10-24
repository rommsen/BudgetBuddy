namespace Client

open Fable.Remoting.Client

/// Wiederholt lesende Abfragen, wenn der Browser die Verbindung abbricht.
/// Befund 2026-10-24 (Firefox, nach einem Deploy): "Http error (0)", obwohl der Server
/// die Anfrage mit 200 beantwortet hat. Nur für lesende Aufrufe benutzen: ein
/// abgebrochener Schreibaufruf kann beim Server angekommen sein.
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
