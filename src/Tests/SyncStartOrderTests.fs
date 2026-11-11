module SyncStartOrderTests

// design-system-k6kfw, Review-Fund: „Abrufen“ schickte `NavigateTo SyncFlow` und `StartSync` zugleich. Das
// Betreten der Seite las danach die Sitzung; kam diese Antwort (nur Datenbank) vor der von `startSync` (OAuth bei
// Comdirect) mit keiner oder einer abgeschlossenen Sitzung zurück, stand der Startknopf da, obwohl der Start lief.
// Ein zweiter Tipp löste ein zweites `startSync` aus, womöglich eine zweite Push-TAN (ADR 0071). Und „Abrufen“
// startete blind, auch wenn eine Sitzung auf die Bank wartete oder Umsätze abrief.
//
// Die Aufzählung geht jede Reihenfolge von Auftrag, Seitenaufruf, Lesantworten, Tipp auf einen Startknopf,
// Antwort und Verlust von `startSync` durch das echte `update` (Doktrin Punkt 20, Vorbild `moveWays`). Das Orakel
// führt einen eigenen Stand des Servers und der offenen Aufrufe, es glaubt nicht dem Modell.

open Expecto
open Shared.Domain
open Types
open Components.SyncFlow.Types
open Components.SyncFlow.State

let private session (status: SyncSessionStatus) : SyncSession =
    { Id = SyncSessionId (System.Guid.Parse "00000000-0000-4000-8000-00000000a5a1")
      StartedAt = System.DateTime(2026, 11, 11)
      CompletedAt = None
      Status = status
      TransactionCount = 0
      ImportedCount = 0
      SkippedCount = 0 }

/// Die Sitzung, die `startSync` auf dem Server anlegt.
let private started = { session AwaitingTan with Id = SyncSessionId (System.Guid.Parse "00000000-0000-4000-8000-00000000a5a2") }

/// Was „Abrufen“ an den Sync-Ablauf schickt.
let private fetchOrder : Msg = StartRequested

/// Der Server arbeitet an einer Sitzung: ein Start wäre eine zweite Challenge.
let private running (server: SyncSession option) : bool =
    match server with
    | Some { Status = (AwaitingBankAuth | AwaitingTan | FetchingTransactions) } -> true
    | _ -> false

/// Zähler der Ports. Die Asyncs enden nie: die Antworten schickt die Aufzählung selbst.
let mutable private reads = 0
let mutable private starts = 0

let private never<'T> : Async<'T> = Async.FromContinuations(fun _ -> ())

let private ports : SyncPorts =
    { GetCurrentSession = fun () -> reads <- reads + 1; never
      StartSync = fun () -> starts <- starts + 1; never }

/// Diese Nachrichten aus den Cmds folgen sofort; die übrigen (Bankstand, Buchungen) gehören nicht zum Start.
let private follows (msg: Msg) : bool =
    match msg with
    | CurrentSessionLoaded _ | CurrentSessionLoadFailed _ | LoadCurrentSession | StartSync | StartRequested -> true
    | _ -> false

let private dispatched (cmd: Elmish.Cmd<Msg>) : Msg list =
    let collected = ResizeArray<Msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected |> List.filter follows

/// Stand der Aufzählung. Alles außer `Model` führt das Orakel selbst.
type private Walk = {
    Model: Model
    /// Der Stand auf dem Server.
    Server: SyncSession option
    /// Der Stand vor dem letzten `startSync`. Solange es läuft, kann ein Lesen noch ihn sehen.
    Before: SyncSession option
    /// Offene Lesevorgänge: Stand des Lesens und was der Server beim Lesen liefern kann.
    OpenReads: (int * SyncSession option list) list
    /// Ein `startSync` ist unterwegs.
    Pending: bool
    Starts: int
    Orders: int
    Visits: int
    Taps: int
}

let private fail (path: string list) (text: string) =
    failtest $"""{text}: {String.concat ", " (List.rev path)}"""

/// Schickt eine Nachricht durch das echte `update`, folgt den sofortigen Nachrichten und prüft jeden Aufruf.
let rec private apply (path: string list) (msg: Msg) (walk: Walk) : Walk =
    let readsBefore, startsBefore = reads, starts
    let model, cmd, _ = updateWith ports msg walk.Model
    let walk =
        if reads > readsBefore then
            let possible = if walk.Pending then [ walk.Before; walk.Server ] else [ walk.Server ]
            { walk with OpenReads = (model.SessionReadVersion, possible) :: walk.OpenReads }
        else walk
    let walk =
        if starts > startsBefore then
            if walk.Pending then fail path "zweites startSync, während das erste läuft"
            if running walk.Server then fail path "startSync bei laufender Sitzung"
            { walk with Pending = true; Starts = walk.Starts + 1; Before = walk.Server; Server = Some started }
        else walk
    dispatched cmd |> List.fold (fun w next -> apply path next w) { walk with Model = model }

let private events (walk: Walk) : (string * Msg * (Walk -> Walk)) list =
    [ if walk.Orders < 2 then
          "Abrufen", fetchOrder, (fun w -> { w with Orders = w.Orders + 1 })
      if walk.Visits < 1 then
          "Seitenaufruf", LoadCurrentSession, (fun w -> { w with Visits = w.Visits + 1 })
      for version, possible in walk.OpenReads do
          let drop (w: Walk) = { w with OpenReads = w.OpenReads |> List.filter (fun (v, _) -> v <> version) }
          // Eine Antwort kann auch erst nach einem späteren Start auf dem Server gelesen werden.
          for answer in List.distinct (possible @ [ walk.Server ]) do
              $"Lesantwort {version}: %A{answer |> Option.map (fun s -> s.Status)}", SessionReadAnswered (version, Ok answer), drop
      if walk.Pending then
          "Startantwort", SyncStarted (Ok (TanRequired started)), (fun w -> { w with Pending = false })
          "Start verloren", SyncStartInterrupted "Http error (0)", (fun w -> { w with Pending = false })
      if walk.Orders > 0 && walk.Taps < 1 && offersStart (sessionScreen walk.Model) then
          "Tipp auf Start", StartSync, (fun w -> { w with Taps = w.Taps + 1 }) ]

let private check (path: string list) (walk: Walk) =
    if walk.Orders > 0 && walk.Pending && offersStart (sessionScreen walk.Model) then
        fail path "Startknopf sichtbar, während der Start läuft"

/// Am Ende: ist jede Lesantwort da und lag keine laufende Sitzung vor, hat der Auftrag gestartet.
let private checkEnd (initial: SyncSession option) (path: string list) (walk: Walk) =
    if walk.Orders > 0 && walk.OpenReads.IsEmpty && not (running initial) && walk.Starts = 0 then
        fail path "Auftrag ohne Start"

let private explore (initial: SyncSession option) : int =
    let seen = System.Collections.Generic.HashSet<Walk>(HashIdentity.Structural)
    let mutable ends = 0
    let rec visit (path: string list) (walk: Walk) =
        if seen.Add walk then
            check path walk
            match events walk with
            | [] ->
                checkEnd initial path walk
                ends <- ends + 1
            | steps ->
                for name, msg, mark in steps do
                    let next = apply (name :: path) msg (mark walk)
                    visit (name :: path) next
    let start =
        { Model = { fst (init ()) with CurrentSession = Success initial }
          Server = initial
          Before = initial
          OpenReads = []
          Pending = false
          Starts = 0
          Orders = 0
          Visits = 0
          Taps = 0 }
    visit [] start
    ends

[<Tests>]
let startOrderTests =
    testSequenced <| testList "Abrufen startet den Sync erst nach dem Lesen der Sitzung" [
        for name, initial in
            [ "ohne Sitzung", None
              "mit abgeschlossener Sitzung", Some (session Completed)
              "während die Bank verbindet", Some (session AwaitingBankAuth)
              "während Umsätze abgerufen werden", Some (session FetchingTransactions) ] do
            test $"In jeder Reihenfolge höchstens ein Start, nie bei laufender Sitzung, kein Startknopf während des Starts ({name})" {
                let ends = explore initial
                Expect.isGreaterThan ends 0 "die Aufzählung erreicht Enden"
            }
    ]
