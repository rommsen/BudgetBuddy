module Server.AutoFetchWorker

open System
open System.Threading
open Microsoft.Extensions.Hosting

/// Der Takt, in dem der Dienst prüft, ob ein automatischer Abruf fällig ist (banking-import-t4raw).
let tickPeriod = TimeSpan.FromMinutes 1.0

/// Treibt den automatischen Abruf (ADR 0093 Punkt 9) im festen Takt: jeder Takt prüft die Fälligkeit und
/// ruft bei Bedarf ab (`BankImport.runAutoFetchWith`). Ein eigener Dienst neben `BankConnectionWorker`: ein
/// Abruf im 15-s-Takt des Refresh hielte diesen auf. Registriert nur in `Program.fs` (ADR 0071 Punkt 8), im
/// TestServer läuft er nie. Der erste Takt kommt erst nach `period`, dann hat die Wiederaufnahme der
/// Bankverbindung ihren Vorlauf. Ein Fehler beendet den Dienst nicht, gemeldet wird nur der Typname. Ein Takt
/// kann an der Bank hängen, deshalb wartet der Dienst auf ihn nur bis `stopping`.
type AutoFetchWorker(tick: unit -> Async<unit>, period: TimeSpan) =
    inherit BackgroundService()

    let guarded =
        async {
            try
                do! tick ()
            with ex ->
                eprintfn "Automatischer Abruf gescheitert: %s" (ex.GetType().Name)
        }

    override _.ExecuteAsync(stopping: CancellationToken) =
        task {
            use timer = new PeriodicTimer(period)
            try
                while not stopping.IsCancellationRequested do
                    let! _ = timer.WaitForNextTickAsync(stopping)
                    do! Async.StartAsTask(guarded).WaitAsync(stopping)
            with :? OperationCanceledException -> ()
        }
