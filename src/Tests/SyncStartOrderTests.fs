module Tests.SyncStartOrderTests

// design-system-k6kfw, Review-Fund: „Abrufen“ schickte `NavigateTo SyncFlow` und `StartSync` zugleich. Kam die
// Lesantwort der Sitzung vor der von `startSync` mit keiner oder einer abgeschlossenen Sitzung zurück, stand der
// Startknopf da, obwohl der Start lief; ein zweiter Tipp löste ein zweites `startSync` aus, womöglich eine zweite
// Push-TAN (ADR 0071). Und „Abrufen“ startete blind, auch wenn eine Sitzung auf die Bank wartete.
//
// Seit banking-import-d0fey (ADR 0093) ist „Abrufen“ ein Sheet (`Components.Fetch`). Die Aufzählung geht jede
// Reihenfolge von „Abrufen“, Schließen, Lesantworten, Antwort, Fehler und Verlust von `startSync`, „Ich habe
// bestätigt“, Antwort, Fehler und Verlust von `confirmTan`, „Abbrechen“ und dessen Antwort (gelungen oder
// gescheitert) durch das echte `update` (Doktrin Punkt 20, Vorbild `moveWays`). Lesantworten kommen in jeder
// Reihenfolge, auch die von vor dem Start nach dessen Antwort. Das Orakel führt einen eigenen Stand des Servers und der offenen Aufrufe, es glaubt nicht
// dem Modell.

open Expecto
open Shared.Domain
open Types
open Components.Fetch.Types
open Components.Fetch.State

let private session (status: SyncSessionStatus) : SyncSession =
    { Id = SyncSessionId (System.Guid.Parse "00000000-0000-4000-8000-00000000a5a1")
      StartedAt = System.DateTime(2026, 11, 11)
      CompletedAt = None
      Status = status
      Trigger = Manual
      Counts = emptyFetchCounts }

/// Die Sitzung, die `startSync` auf dem Server anlegt: sie wartet auf die TAN.
/// Jeder Start legt eine neue Sitzung an, `startedAs 0` ist die, die schon vor dem ersten Start wartet.
let private startedAs (n: int) : SyncSession =
    { session AwaitingTan with Id = SyncSessionId (System.Guid.Parse $"00000000-0000-4000-8000-00000000b00{n}") }

/// Dieselbe Sitzung nach Abruf und Buchen.
let private bookedAs (n: int) = { startedAs n with Status = Completed; Counts = { emptyFetchCounts with NewEntries = 2 } }

/// Dieselbe Sitzung, wenn Start oder TAN-Bestätigung mit Fehler enden.
let private failedAs (n: int) = { startedAs n with Status = Failed "Die Bank antwortet nicht." }

/// Der Server arbeitet an einer Sitzung: ein Start wäre eine zweite Challenge.
let private running (server: SyncSession option) : bool =
    match server with
    | Some { Status = (AwaitingBankAuth | AwaitingTan | FetchingTransactions | ImportingToYnab) } -> true
    | _ -> false

/// Zähler der Ports. Die Asyncs enden nie: die Antworten schickt die Aufzählung selbst.
let mutable private reads = 0
let mutable private starts = 0
let mutable private confirms = 0
let mutable private cancels = 0

let private never<'T> : Async<'T> = Async.FromContinuations(fun _ -> ())

let private ports : FetchPorts =
    { GetCurrentSession = fun () -> reads <- reads + 1; never
      StartSync = fun () -> starts <- starts + 1; never
      ConfirmTan = fun _ -> confirms <- confirms + 1; never
      CancelSync = fun _ -> cancels <- cancels + 1; never }

/// Stand der Aufzählung. Alles außer `Model` führt das Orakel selbst.
type private Walk = {
    Model: Model
    /// Der Stand auf dem Server.
    Server: SyncSession option
    /// Offene Lesevorgänge: Stand des Lesens und was der Server beim Lesen liefern kann.
    OpenReads: (int * SyncSession option list) list
    /// Ein `startSync` ist unterwegs.
    PendingStart: bool
    /// Ein `confirmTan` ist unterwegs.
    PendingConfirm: bool
    /// Ein `cancelSync` ist unterwegs. Der Server bricht sofort ab, die Antwort kommt danach, gelungen oder
    /// mit Fehler (verlorene Antwort). Eine Sitzung, die nach gescheitertem Abbruch weiter wartet, zählt die
    /// Aufzählung nicht auf: liest „Abrufen“ sie während des Abbruchs, steht die TAN einer abgebrochenen
    /// Sitzung da, und „Ich habe bestätigt“ scheitert am Server (Kann-Sammlung banking-import).
    PendingCancel: bool
    Starts: int
    Opens: int
    Closes: int
    Confirms: int
    Cancels: int
    Lost: int
}

let private fail (path: string list) (text: string) =
    failtest $"""{text}: {String.concat ", " (List.rev path)}"""

/// Schickt eine Nachricht durch das echte `update` und prüft jeden Aufruf.
let private apply (path: string list) (msg: Msg) (walk: Walk) : Walk =
    let readsBefore, startsBefore, confirmsBefore, cancelsBefore = reads, starts, confirms, cancels
    let model, _, _ = updateWith ports msg walk.Model
    let walk =
        if reads > readsBefore then
            let possible = [ walk.Server ]
            { walk with OpenReads = (model.SessionReadVersion, possible) :: walk.OpenReads }
        else walk
    let walk =
        if starts > startsBefore then
            if walk.PendingStart then fail path "zweites startSync, während das erste läuft"
            if walk.PendingConfirm then fail path "startSync, während confirmTan läuft"
            if running walk.Server then fail path "startSync bei laufender Sitzung"
            { walk with PendingStart = true; Starts = walk.Starts + 1; Server = Some (startedAs (walk.Starts + 1)) }
        else walk
    let walk =
        if confirms > confirmsBefore then
            if walk.PendingConfirm then fail path "zweites confirmTan, während das erste läuft"
            if walk.Server <> Some (startedAs walk.Starts) then fail path "confirmTan ohne wartende TAN auf dem Server"
            { walk with PendingConfirm = true }
        else walk
    let walk =
        if cancels > cancelsBefore then { walk with PendingCancel = true; Server = None }
        else walk
    // Was Roman tun oder lesen muss, steht nach einer Antwort im offenen Sheet, auch wenn er es geschlossen
    // hatte (Review-Fund zu banking-import-d0fey: die Push-TAN lief sonst unbemerkt ab).
    let isAnswer =
        match msg with
        | SessionReadAnswered _ | SyncStarted _ | TanConfirmed _ -> true
        | _ -> false
    let screen = sheetScreen model
    let surfaces = match screen with TanScreen | ErrorScreen _ -> true | _ -> false
    if isAnswer && surfaces && screen <> sheetScreen walk.Model && not model.IsOpen then
        fail path "TAN oder Fehler bei geschlossenem Sheet"
    { walk with Model = model }

let private events (walk: Walk) : (string * Msg * (Walk -> Walk)) list =
    [ if walk.Opens < 2 then
          "Abrufen", Opened, (fun w -> { w with Opens = w.Opens + 1 })
      if walk.Model.IsOpen && walk.Closes < 1 then
          "Schließen", Closed, (fun w -> { w with Closes = w.Closes + 1 })
      for version, possible in walk.OpenReads do
          let drop (w: Walk) = { w with OpenReads = w.OpenReads |> List.filter (fun (v, _) -> v <> version) }
          for answer in List.distinct (possible @ [ walk.Server ]) do
              $"Lesantwort {version}: %A{answer |> Option.map (fun s -> s.Status)}", SessionReadAnswered (version, Ok answer), drop
      if walk.PendingStart then
          "Startantwort TAN", SyncStarted (Ok (TanRequired (startedAs walk.Starts))), (fun w -> { w with PendingStart = false })
          "Startantwort Fehler", SyncStarted (Error (SyncError.TransactionFetchFailed "Die Bank antwortet nicht.")), (fun w -> { w with PendingStart = false; Server = Some (failedAs walk.Starts) })
          if walk.Lost < 1 then
              "Start verloren", SyncStartInterrupted "Http error (0)", (fun w -> { w with PendingStart = false; Lost = w.Lost + 1 })
      if walk.PendingConfirm then
          "TAN-Antwort", TanConfirmed (Ok (bookedAs walk.Starts)), (fun w -> { w with PendingConfirm = false; Server = Some (bookedAs walk.Starts) })
          "TAN-Antwort Fehler", TanConfirmed (Error SyncError.TanTimeout), (fun w -> { w with PendingConfirm = false; Server = Some (failedAs walk.Starts) })
          if walk.Lost < 1 then
              "TAN verloren", TanConfirmInterrupted "Http error (0)", (fun w -> { w with PendingConfirm = false; Server = Some (bookedAs walk.Starts); Lost = w.Lost + 1 })
      if walk.Model.IsOpen && walk.Confirms < 1 && sheetScreen walk.Model = TanScreen then
          "Ich habe bestätigt", ConfirmTan, (fun w -> { w with Confirms = w.Confirms + 1 })
      if walk.Model.IsOpen && walk.Cancels < 1 && sheetScreen walk.Model = TanScreen && not walk.PendingConfirm then
          "Abbrechen", CancelSync, (fun w -> { w with Cancels = w.Cancels + 1 })
      if walk.PendingCancel then
          "Abbruch-Antwort", SyncCancelled (Ok ()), (fun w -> { w with PendingCancel = false })
          "Abbruch-Antwort Fehler", SyncCancelled (Error (SyncError.DatabaseError ("cancel_sync", "weg"))), (fun w -> { w with PendingCancel = false }) ]

let private check (path: string list) (walk: Walk) =
    if walk.Model.IsOpen && (walk.PendingStart || walk.PendingConfirm) && sheetScreen walk.Model <> RunningScreen then
        fail path "das Sheet zeigt etwas anderes als „Rufe ab…“, während der Abruf läuft"

/// Am Ende: ist jede Lesantwort da und lag keine laufende Sitzung vor, hat der Auftrag gestartet.
let private checkEnd (initial: SyncSession option) (path: string list) (walk: Walk) =
    if walk.Opens > 0 && walk.OpenReads.IsEmpty && not (running initial) && walk.Starts = 0 then
        fail path "Auftrag ohne Start"
    // Früher „Lesen von vor dem Start überschreibt die wartende Sitzung“ (SyncFlow): kam die Lesantwort von
    // vor dem Start nach der Startantwort, stand „Rufe ab…“ statt der TAN da, bis die Push-TAN ablief.
    let waiting = match walk.Server with Some { Status = AwaitingTan } -> true | _ -> false
    if waiting && sheetScreen walk.Model <> TanScreen then
        fail path "die wartende TAN ist verdeckt"

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
        { Model = { init with Session = Success initial }
          Server = initial
          OpenReads = []
          PendingStart = false
          PendingConfirm = false
          PendingCancel = false
          Starts = 0
          Opens = 0
          Closes = 0
          Confirms = 0
          Cancels = 0
          Lost = 0 }
    visit [] start
    ends

[<Tests>]
let startOrderTests =
    // Geteilt: die Zähler `reads`, `starts`, `confirms` und `cancels` dieses Moduls, jeder Fall zählt an ihnen.
    // Billig (fünf Fälle unter 0,1 s), darum ohne eigene Gruppe.
    testSequenced <| testList "Abrufen startet erst nach dem Lesen der Sitzung" [
        for name, initial in
            [ "ohne Sitzung", None
              "mit beendeter Sitzung", Some (session Completed)
              "während die Bank verbindet", Some (session AwaitingBankAuth)
              "während Umsätze abgerufen werden", Some (session FetchingTransactions)
              "während auf die TAN gewartet wird", Some (startedAs 0) ] do
            test $"In jeder Reihenfolge höchstens ein Start und eine TAN-Bestätigung, nie bei laufender Sitzung ({name})" {
                let ends = explore initial
                Expect.isGreaterThan ends 0 "die Aufzählung erreicht Enden"
            }
    ]
