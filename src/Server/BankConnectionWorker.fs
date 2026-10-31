module Server.BankConnectionWorker

open System
open System.Threading
open Microsoft.Extensions.Hosting

/// Nimmt die Bankverbindung einmal wieder auf (ADR 0071 Punkt 4) und treibt dann `tick` im festen
/// Takt (Punkt 8). Registriert nur in `Program.fs`, im TestServer läuft der Dienst damit nie. Hier
/// statt beim Start: eine Störung der Bank soll den Server nicht aufhalten. Die Datenbank steht,
/// weil `Program.fs` sie vor `app.Run` anlegt. Ein Fehler darf den Dienst nicht beenden: eine
/// Ausnahme aus `ExecuteAsync` stoppt per Default den ganzen Host. Gemeldet wird nur der Typname.
/// Wiederaufnahme und Takt können an der Bank oder am Semaphor hängen, deshalb wartet der Dienst
/// auf beide nur bis `stopping`. Die verlassene Arbeit endet mit dem Prozess.
type BankConnectionWorker(resume: DateTimeOffset -> Async<unit>, tick: DateTimeOffset -> Async<unit>, period: TimeSpan) =
    inherit BackgroundService()

    let guarded (step: string) (work: DateTimeOffset -> Async<unit>) =
        async {
            try
                do! work DateTimeOffset.UtcNow
            with ex ->
                eprintfn "%s der Bankverbindung gescheitert: %s" step (ex.GetType().Name)
        }

    override _.ExecuteAsync(stopping: CancellationToken) =
        task {
            use timer = new PeriodicTimer(period)
            try
                do! Async.StartAsTask(guarded "Wiederaufnahme" resume).WaitAsync(stopping)
                while not stopping.IsCancellationRequested do
                    let! _ = timer.WaitForNextTickAsync(stopping)
                    do! Async.StartAsTask(guarded "Takt" tick).WaitAsync(stopping)
            with :? OperationCanceledException -> ()
        }
