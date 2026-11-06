/// Übernahme im Hintergrund (Context `budgeting`, budgeting-dkjc5, ADR 0084 Punkt 5): Der Dienst arbeitet den
/// laufenden, die künftigen und dann die älteren Monate ab, höchstens einen Monatsaufruf je Minute. Kein
/// gespeicherter Fortschritt: Was übernommen ist, sagt der Stream, nach einem Neustart beginnt ein neuer Durchlauf.
module Server.Budgeting.TakeoverWorker

open System
open System.Threading
open Microsoft.Extensions.Hosting
open Microsoft.FSharp.Reflection
open Shared.Domain
open Shared.Budgeting
open Server.Budgeting.Domain
open Server.Budgeting.CommandHandlers

/// Was ein Schritt braucht. `Settings` liefert Token und Standard-Budget (je `None`, wenn nicht eingerichtet),
/// `FetchMonths token budgetId` das Monatsfenster aus `GET /months`, `FetchMonth token budgetId month` einen
/// Monat, `LoadTakenOver` die übernommenen Monate (eine Abfrage) und `EnsureTakenOver` die Übernahme unter der
/// Monatssperre (`CommandHandlers.ensureTakenOver`).
type TakeoverPort = {
    Settings: unit -> Async<string option * string option>
    FetchMonths: string -> YnabBudgetId -> Async<YnabResult<string list>>
    FetchMonth: string -> YnabBudgetId -> string -> Async<YnabResult<YnabMonth>>
    LoadTakenOver: unit -> Async<Result<Set<string>, string>>
    EnsureTakenOver: (string -> Async<YnabResult<YnabMonth>>) -> DateTime -> string -> Async<Result<EnsuredTakeover, YnabError>>
}

/// Der Stand eines Durchlaufs, nur im Speicher. `Months` ist das Monatsfenster, einmal je Durchlauf geholt;
/// `Skipped` die Monate, die YNAB in diesem Durchlauf mit 404 beantwortet hat.
type TakeoverPass = {
    Months: string list option
    Skipped: Set<string>
}

/// Ein neuer Durchlauf: Liste neu holen, nichts übersprungen.
let freshPass : TakeoverPass = { Months = None; Skipped = Set.empty }

/// Der Fallname eines YNAB-Fehlers für das Log, ohne Meldung.
let private caseName (error: YnabError) : string =
    let case, _ = FSharpValue.GetUnionFields(error, typeof<YnabError>)
    case.Name

/// Ein YNAB-Fehler als Ergebnis: 429 als `RateLimited`, alles andere als `Failed` mit dem Fallnamen.
let private outcomeOf (error: YnabError) : TakeoverOutcome =
    match error with
    | YnabError.RateLimitExceeded _ -> TakeoverOutcome.RateLimited
    | other -> TakeoverOutcome.Failed (caseName other)

/// Ein Schritt: Settings lesen, Monatsfenster holen (einmal je Durchlauf), übernommene Monate lesen, den ersten
/// offenen Monat aus `takeoverQueue` übernehmen. `now` ist UTC, `berlinZone` bestimmt den laufenden Monat.
/// Ohne Token oder Budget fragt der Schritt YNAB nicht. Ein 404 merkt sich den Monat als übersprungen.
let takeoverStep
    (port: TakeoverPort)
    (berlinZone: TimeZoneInfo)
    (now: DateTime)
    (pass: TakeoverPass)
    : Async<TakeoverOutcome * TakeoverPass> =
    async {
        match! port.Settings () with
        | Some token, Some budget ->
            let budgetId = YnabBudgetId budget
            let! months =
                match pass.Months with
                | Some months -> async.Return (Ok months)
                | None -> port.FetchMonths token budgetId
            match months with
            | Error e -> return outcomeOf e, pass
            | Ok months ->
                let pass = { pass with Months = Some months }
                match! port.LoadTakenOver () with
                | Error _ -> return TakeoverOutcome.Failed "LocalReadFailed", pass
                | Ok takenOver ->
                    let current = currentMonthKey (DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, berlinZone)))
                    match takeoverQueue current months takenOver |> List.filter (fun m -> not (pass.Skipped.Contains m)) with
                    | [] -> return TakeoverOutcome.NothingLeft, freshPass
                    | month :: _ ->
                        match! port.EnsureTakenOver (port.FetchMonth token budgetId) now month with
                        | Ok _ -> return TakeoverOutcome.TookOver month, pass
                        | Error (YnabError.MonthNotOpen _) ->
                            return TakeoverOutcome.MonthNotOpen month, { pass with Skipped = pass.Skipped.Add month }
                        | Error e -> return outcomeOf e, pass
        | _ -> return TakeoverOutcome.NotConfigured, freshPass
    }

/// Die Zeile im Server-Log nach einem Schritt, `None` ohne Zeile. Fehlende Settings und ein 401 erscheinen
/// einmal je Zustandswechsel, andere Fehler je Schritt mit Fall- oder Typnamen. Nie ein Token.
let logLine (previous: TakeoverOutcome option) (current: TakeoverOutcome) : string option =
    let repeated = previous = Some current
    match current with
    | TakeoverOutcome.TookOver _
    | TakeoverOutcome.NothingLeft -> None
    | TakeoverOutcome.NotConfigured when repeated -> None
    | TakeoverOutcome.NotConfigured -> Some "Übernahme aus YNAB wartet: Token oder Budget fehlen in den Settings"
    | TakeoverOutcome.Failed "Unauthorized" when repeated -> None
    | TakeoverOutcome.Failed cause -> Some $"Übernahme aus YNAB gescheitert: {cause}"
    | TakeoverOutcome.RateLimited -> Some "Übernahme aus YNAB: Rate-Grenze erreicht, nächster Versuch in 15 Minuten"
    | TakeoverOutcome.MonthNotOpen month -> Some $"Übernahme aus YNAB: Monat {month} nicht offen, übersprungen"

/// Die Schleife des Dienstes: Schritt, Log, warten nach `nextDelay`, ohne Ende. Eine Ausnahme im Schritt wird
/// zu `Failed` mit dem Typnamen und beendet die Schleife nicht. `clock` liefert UTC, `delay` wartet (im Dienst
/// `Async.Sleep`, im Test eine simulierte Uhr).
let runWith
    (port: TakeoverPort)
    (berlinZone: TimeZoneInfo)
    (clock: unit -> DateTime)
    (delay: TimeSpan -> Async<unit>)
    (log: string -> unit)
    : Async<unit> =
    let rec loop (previous: TakeoverOutcome option) (pass: TakeoverPass) =
        async {
            let! outcome, next =
                async {
                    try
                        return! takeoverStep port berlinZone (clock ()) pass
                    with ex ->
                        return TakeoverOutcome.Failed (ex.GetType().Name), pass
                }
            logLine previous outcome |> Option.iter log
            do! delay (nextDelay outcome)
            return! loop (Some outcome) next
        }
    loop None freshPass

/// Die Wartefunktion des laufenden Servers.
let sleep (span: TimeSpan) : Async<unit> = Async.Sleep span

/// Der Hintergrunddienst (Vorbild `BankConnectionWorker`, ADR 0071 Punkt 8). Registriert nur in `Program.fs`, im
/// TestServer läuft er nie. Der erste Schritt läuft auf dem Thread-Pool, der Serverstart wartet nicht auf YNAB.
/// Die Datenbank steht, weil `Program.fs` sie vor `app.Run` anlegt.
type TakeoverWorker(port: TakeoverPort, berlinZone: TimeZoneInfo, clock: unit -> DateTime, delay: TimeSpan -> Async<unit>) =
    inherit BackgroundService()

    override _.ExecuteAsync(stopping: CancellationToken) =
        task {
            try
                do! Async.StartAsTask(runWith port berlinZone clock delay (eprintfn "%s"), cancellationToken = stopping)
            with :? OperationCanceledException -> ()
        }
