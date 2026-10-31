module Server.BankConnectionWorker

open System
open System.Threading
open Microsoft.Extensions.Hosting

/// Treibt `tick` im festen Takt (ADR 0071 Punkt 8). Registriert nur in `Program.fs`, im TestServer
/// läuft der Dienst damit nie. Ein Fehler in einem Takt darf den Dienst nicht beenden: eine Ausnahme
/// aus `ExecuteAsync` stoppt per Default den ganzen Host. Gemeldet wird nur der Typname.
type BankConnectionWorker(tick: DateTimeOffset -> Async<unit>, period: TimeSpan) =
    inherit BackgroundService()

    let safeTick () =
        async {
            try
                do! tick DateTimeOffset.UtcNow
            with ex ->
                eprintfn "Takt der Bankverbindung gescheitert: %s" (ex.GetType().Name)
        }

    override _.ExecuteAsync(stopping: CancellationToken) =
        task {
            use timer = new PeriodicTimer(period)
            try
                while not stopping.IsCancellationRequested do
                    let! _ = timer.WaitForNextTickAsync(stopping)
                    do! Async.StartAsTask(safeTick ())
            with :? OperationCanceledException -> ()
        }
