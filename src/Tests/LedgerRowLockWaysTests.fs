module LedgerRowLockWaysTests

// ledger-smv1s: Je Buchung und Sperrgruppe läuft höchstens ein Aufruf. Die Aufzählung geht alle
// Reihenfolgen aus Start-Zweigen und Antworten durch das echte `update` (Doktrin Punkt 20, Vorbild
// `moveWays` in MoveMoneyClientTests.fs). Das Orakel zählt die offenen `Run…`-Aufrufe selbst, aus den
// ausgegebenen Nachrichten und den zugestellten Antworten, nicht aus den `…Rows` des Modells.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.SplitEditor

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }
let private account = Guid.Parse "0195e8a0-0000-7000-8000-0000000000aa"
let private foodId = Guid.Parse "0195d8a0-0000-7000-8000-0000000000f1"
let private homeId = Guid.Parse "0195d8a0-0000-7000-8000-0000000000f2"
let private food : CategoryChoice = { CategoryId = YnabCategoryId foodId; CategoryName = "Lebensmittel" }

let private categories : YnabCategory list =
    [ { Id = YnabCategoryId foodId; Name = "Lebensmittel"; GroupName = "Alltag"; Available = Some (eur 120m); Hidden = false }
      { Id = YnabCategoryId homeId; Name = "Haushalt"; GroupName = "Alltag"; Available = Some (eur 50m); Hidden = false } ]

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f8a0-0000-7000-8000-{n:D12}"
      AccountId = account
      Date = DateTime(2026, 11, n)
      Amount = eur -42.80m
      Payee = Some $"Payee smv1s {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some foodId
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = true
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None
      ExternalLinks = []
      PayeeWriteBackFailure = None
      PossibleDuplicate = None
      CategoryFromOrderId = false }

let private a = (entryOf 1).Id
let private b = (entryOf 2).Id

/// Ein Schritt durch das echte `update`; die Folgenachrichten werden gesammelt, nicht ausgeführt. Ein
/// Effekt (Gerätespeicher) läuft unter .NET nicht und bleibt außen vor.
let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        try sub sent.Add with _ -> ()
    model', List.ofSeq sent, external

let private step (msg: Msg) (model: Model) : Model =
    let model', _, _ = run msg model
    model'

/// Kassenbuch mit zwei geladenen Buchungen, Detail offen auf der ersten.
let private ledgerWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        Categories = Success categories
        CategoriesLoadedAt = Some DateTime.Now
        LenaFlagColor = Success (Some "purple")
        DetailFor = rows |> List.tryHead
        DetailForm = DetailPanel }

/// Das Durchgehen über beide Buchungen, Karte auf der ersten. Die Liste bleibt geladen.
let private reviewingBoth () : Model =
    let rows = [ entryOf 1; entryOf 2 ]
    let opened = ledgerWith rows |> step ReviewOpened
    step (ReviewQueueLoaded (opened.ReviewGeneration, Ok rows)) opened

let private cardOf (model: Model) : Guid option = reviewCardOf model |> Option.map (fun e -> e.Id)

/// Ein offener Aufruf, wie das Orakel ihn kennt: die Buchung und die beiden möglichen Antworten.
type private Call = { Entry: Guid; Ok: Msg; Error: Msg }

/// Ein Start-Zweig: auf welche Buchung er im gegebenen Modell zielt und die Nachricht.
type private Start = { Name: string; Target: Model -> Guid option; Msg: Msg }

type private LockGroup =
    { Name: string
      Base: Model
      /// Alle Start-Zweige der Gruppe für die erste Buchung, dazu ein Start für die zweite (Isolation).
      Starts: Start list
      /// Ein Aufruf dieser Gruppe unter den ausgegebenen Nachrichten.
      CallOf: Msg -> Call option }

let private on (entryId: Guid) (name: string) (msg: Msg) : Start = { Name = name; Target = (fun _ -> Some entryId); Msg = msg }
let private onCard (name: string) (msg: Msg) : Start = { Name = name; Target = cardOf; Msg = msg }

/// Geht alle Folgen bis `depth` durch und gibt die Namen der Starts zurück, die einen Aufruf auslösten, dazu die
/// Antwortarten ("Ok", "Error"), die zugestellt wurden.
let private walk (group: LockGroup) (depth: int) : Set<string> * Set<string> =
    let visited = Collections.Generic.Dictionary<Model * Call list, int> HashIdentity.Structural
    let mutable fired = Set.empty
    let mutable answered' = Set.empty
    let callsFor (entryId: Guid) (calls: Call list) = calls |> List.filter (fun c -> c.Entry = entryId) |> List.length
    let rec go (left: int) (path: string list) (model: Model) (pending: Call list) =
        // Das `->` der ersten Schleife schaltet die impliziten Yields der ganzen Liste ab: ohne `yield` verwarf die
        // zweite Schleife beide Antworten, die Aufzählung stellte nie eine zu (Review a3f7eb1d..982bdf79, FS0020).
        let events =
            [ for s in group.Starts -> s.Name, s.Msg, Some s, None
              for i, call in List.indexed pending do
                  yield "Ok", call.Ok, None, Some i
                  yield "Error", call.Error, None, Some i ]
        for name, msg, start, answered in events do
            let path' = name :: path
            let shown = String.concat ", " (List.rev path')
            let after, sent, external = run msg model
            let remaining =
                match answered with
                | Some i -> List.removeAt i pending
                | None -> pending
            let calls = sent |> List.choose group.CallOf
            let open' = remaining @ calls
            for call in calls do
                Expect.equal (callsFor call.Entry open') 1 $"{group.Name}: zwei offene Aufrufe für eine Buchung: {shown}"
            match start, answered with
            | Some s, _ ->
                if not calls.IsEmpty then fired <- fired.Add s.Name
                match s.Target model with
                | Some target when callsFor target pending > 0 ->
                    Expect.equal after model $"{group.Name}: gesperrter Start ändert das Modell: {shown}"
                    Expect.isEmpty sent $"{group.Name}: gesperrter Start schickt etwas: {shown}"
                    Expect.equal external NoOp $"{group.Name}: gesperrter Start meldet etwas: {shown}"
                | _ -> ()
            | None, Some i ->
                answered' <- answered'.Add name
                let entryId = pending.[i].Entry
                if callsFor entryId open' = 0 then
                    let startsAgain =
                        group.Starts
                        |> List.exists (fun s ->
                            s.Target after = Some entryId
                            && (let _, again, _ = run s.Msg after
                                again |> List.exists (fun m -> group.CallOf m |> Option.exists (fun c -> c.Entry = entryId))))
                    Expect.isTrue startsAgain $"{group.Name}: nach der Antwort kein Start möglich: {shown}"
            | None, None -> ()
            let key = after, open'
            let seen, deepest = visited.TryGetValue key
            if left > 1 && not (seen && deepest >= left - 1) then
                visited[key] <- left - 1
                go (left - 1) path' after open'
    go depth [] group.Base []
    fired, answered'

let private changedCategory : CategoryChangeOutcome =
    { Previous = Some food; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }

let private splitLines : TransactionSplit list =
    [ { Target = ToCategory (YnabCategoryId foodId, "Lebensmittel"); Amount = eur -30.00m; Memo = None }
      { Target = ToCategory (YnabCategoryId homeId, "Haushalt"); Amount = eur -12.80m; Memo = None } ]

let private categoryGroup : LockGroup =
    let ledger = ledgerWith [ entryOf 1; entryOf 2 ]
    let editor : SplitEditorState =
        { EntryId = a
          Total = eur -42.80m
          Lines =
            [ { Target = Some (ToCategory (YnabCategoryId foodId, "Lebensmittel")); AmountText = "30,00"; Memo = "" }
              { Target = Some (ToCategory (YnabCategoryId homeId, "Haushalt")); AmountText = "12,80"; Memo = "" } ]
          Picker = NoSplitPicker
          Before = WasPlain (Some food)
          AmountFrom = None
          SaveError = None }
    let transferTo = YnabAccountId (Guid.Parse "0195e8a0-0000-7000-8000-0000000000bb")
    { Name = "Kategorie und Split"
      Base = { ledger with SplitEditor = Some editor }
      Starts =
        [ on a "Kategorie setzen" (StartSetEntryCategory (a, Some (YnabCategoryId homeId)))
          on a "Kategorie zurück" (StartUndoCategory (a, Some food))
          on a "Kategorie erneut senden" (StartRetryCategorization a)
          on a "Übertrag setzen" (StartSetEntryTransfer (a, transferTo, false))
          on a "Split speichern" StartSaveSplits
          on a "Split zurück" (StartUndoSplit (a, [ splitLines.Head ]))
          on b "Zweite Buchung" (StartRetryCategorization b) ]
      CallOf =
        function
        | RunSetEntryCategory (id, newId) ->
            Some { Entry = id; Ok = EntryCategorySet (id, newId, Ok changedCategory); Error = EntryCategorySet (id, newId, Error "weg") }
        | RunUndoCategory (id, previous) ->
            Some { Entry = id; Ok = EntryCategoryRestored (id, previous, Ok changedCategory); Error = EntryCategoryRestored (id, previous, Error "weg") }
        | RunRetryCategorization id ->
            Some { Entry = id; Ok = CategorizationRetried (id, Ok CategorySent); Error = CategorizationRetried (id, Error "weg") }
        | RunSetEntryTransfer (id, target, undo) ->
            Some { Entry = id; Ok = EntryTransferSet (id, target, undo, Ok changedCategory); Error = EntryTransferSet (id, target, undo, Error "weg") }
        | RunSaveSplits (id, lines) ->
            Some { Entry = id; Ok = SplitsSaved (id, lines, Ok changedCategory); Error = SplitsSaved (id, lines, Error "weg") }
        | RunSaveSplitsWithAmount (id, lines, amount) ->
            Some { Entry = id; Ok = SplitsWithAmountSaved (id, lines, amount, Ok changedCategory); Error = SplitsWithAmountSaved (id, lines, amount, Error "weg") }
        | RunUndoSplit (id, lines) ->
            Some { Entry = id; Ok = SplitsRestored (id, lines, Ok changedCategory); Error = SplitsRestored (id, lines, Error "weg") }
        | _ -> None }

let private reviewMarkOutcome : ReviewMarkOutcome =
    { Previous = { IsApproved = false; ClearedState = ReviewUncleared }; Change = ReviewMarkChanged; WriteBack = ReviewMarkSent }

let private reviewMarkGroup : LockGroup =
    { Name = "Prüfvermerk"
      Base = reviewingBoth ()
      Starts =
        [ on a "Prüfvermerk setzen" (StartSetReviewMark (a, Reviewed))
          on a "Prüfvermerk erneut senden" (StartRetryReviewMark a)
          on a "Wisch geprüft" (StartSwipeReviewed a)
          on a "Prüfvermerk zurück" (StartUndoReviewMark (a, reviewMarkOutcome.Previous))
          onCard "Passt, weiter" ReviewConfirm
          on b "Zweite Buchung" (StartRetryReviewMark b) ]
      CallOf =
        function
        | RunSetReviewMark (id, target) ->
            Some { Entry = id; Ok = ReviewMarkSet (id, target, Ok reviewMarkOutcome); Error = ReviewMarkSet (id, target, Error "weg") }
        | RunRetryReviewMark id ->
            Some { Entry = id; Ok = ReviewMarkRetried (id, Ok ReviewMarkSent); Error = ReviewMarkRetried (id, Error "weg") }
        | RunSwipeReviewed id ->
            Some { Entry = id; Ok = SwipeReviewedDone (id, Some "Geprüft", Ok reviewMarkOutcome); Error = SwipeReviewedDone (id, Some "Geprüft", Error "weg") }
        | RunUndoReviewMark (id, previous) ->
            Some { Entry = id; Ok = ReviewMarkRestored (id, previous, Ok reviewMarkOutcome); Error = ReviewMarkRestored (id, previous, Error "weg") }
        | RunReviewConfirm id ->
            Some { Entry = id; Ok = ReviewConfirmed (id, Ok reviewMarkOutcome); Error = ReviewConfirmed (id, Error "weg") }
        | _ -> None }

let private noFlag : FlagState = { Color = None; Name = None }
let private flagOutcome : FlagOutcome = { Previous = noFlag; Change = FlagChanged; WriteBack = FlagSent }

let private lenaGroup : LockGroup =
    { Name = "Lena"
      Base = reviewingBoth ()
      Starts =
        [ on a "Lena fragen" (StartSetLenaFlag (a, AssignLena))
          on a "Lena zurück" (StartUndoFlag (a, noFlag))
          on a "Lena erneut senden" (StartRetryFlag a)
          onCard "Lena auf der Karte" ReviewLena
          on b "Zweite Buchung" (StartRetryFlag b) ]
      CallOf =
        function
        | RunSetLenaFlag (id, target) ->
            Some { Entry = id; Ok = LenaFlagSet (id, target, Ok flagOutcome); Error = LenaFlagSet (id, target, Error "weg") }
        | RunUndoFlag (id, previous) ->
            Some { Entry = id; Ok = FlagRestored (id, previous, Ok flagOutcome); Error = FlagRestored (id, previous, Error "weg") }
        | RunRetryFlag id ->
            Some { Entry = id; Ok = FlagRetried (id, Ok FlagSent); Error = FlagRetried (id, Error "weg") }
        | _ -> None }

let private memoOutcome : MemoOutcome =
    { PreviousMemo = None; CurrentMemo = Some "Wocheneinkauf"; MemoChange = MemoChanged; MemoWriteBack = MemoSent }

let private memoGroup : LockGroup =
    let ledger = ledgerWith [ entryOf 1; entryOf 2 ]
    { Name = "Memo"
      Base = { ledger with MemoEdit = Some { MemoEntryId = a; MemoDraft = "Wocheneinkauf" } }
      Starts =
        [ on a "Memo speichern" (StartSaveMemo a)
          on a "Memo zurück" (StartUndoMemo (a, Some "alt"))
          on a "Memo erneut senden" (StartRetryMemo a)
          on b "Zweite Buchung" (StartRetryMemo b) ]
      CallOf =
        function
        | RunSetMemo (id, _) -> Some { Entry = id; Ok = MemoSet (id, Ok memoOutcome); Error = MemoSet (id, Error "weg") }
        | RunUndoMemo (id, _) -> Some { Entry = id; Ok = MemoRestored (id, Ok memoOutcome); Error = MemoRestored (id, Error "weg") }
        | RunRetryMemo id -> Some { Entry = id; Ok = MemoRetried (id, Ok MemoSent); Error = MemoRetried (id, Error "weg") }
        | _ -> None }

let private amountOutcome : AmountOutcome =
    { PreviousAmount = eur -42.80m; CurrentAmount = eur -45m; AmountChange = AmountChanged; AmountWriteBack = AmountSent }

let private amountGroup : LockGroup =
    let ledger = ledgerWith [ entryOf 1; entryOf 2 ]
    let opened = step (OpenAmountEdit a) ledger
    { Name = "Betrag"
      Base = step (AmountDraftChanged "45") opened
      Starts =
        [ on a "Betrag speichern" (StartSaveAmount a)
          on a "Betrag zurück" (StartUndoAmount (a, -42.80m))
          on a "Betrag erneut senden" (StartRetryAmount a)
          on b "Zweite Buchung" (StartRetryAmount b) ]
      CallOf =
        function
        | RunSetAmount (id, _) -> Some { Entry = id; Ok = AmountSet (id, Ok amountOutcome); Error = AmountSet (id, Error "weg") }
        | RunUndoAmount (id, _) -> Some { Entry = id; Ok = AmountRestored (id, Ok amountOutcome); Error = AmountRestored (id, Error "weg") }
        | RunRetryAmount id -> Some { Entry = id; Ok = AmountRetried (id, Ok AmountSent); Error = AmountRetried (id, Error "weg") }
        | _ -> None }

let private pushGroup : LockGroup =
    let notInYnab (n: int) =
        { entryOf n with LinkedToYnab = false; PushToYnabFailure = Some { Reason = "Timeout"; FailedAt = DateTime(2026, 11, 1) } }
    { Name = "Noch nicht in YNAB"
      Base = ledgerWith [ notInYnab 1; notInYnab 2 ]
      Starts =
        [ on a "Erneut senden" (StartRetryPush a)
          on b "Zweite Buchung" (StartRetryPush b) ]
      CallOf =
        function
        | RunRetryPush id -> Some { Entry = id; Ok = PushRetried (id, Ok PushToYnabSent); Error = PushRetried (id, Error "weg") }
        | _ -> None }

/// Läuft die Aufzählung einer Gruppe und prüft, dass jeder Start-Zweig mindestens einmal startete und jede
/// Antwortart mindestens einmal zugestellt wurde (sonst prüft "wieder frei" nichts).
let private walkTest (group: LockGroup) (depth: int) : Test =
    test $"{group.Name}: höchstens ein Aufruf je Buchung, gesperrt heißt still, nach Erfolg und nach Fehler wieder frei" {
        let fired, answered = walk group depth
        for start in group.Starts do
            Expect.isTrue (fired.Contains start.Name) $"{group.Name}: {start.Name} startete nie, die Aufzählung prüft ihn nicht"
        for kind in [ "Ok"; "Error" ] do
            Expect.isTrue (answered.Contains kind) $"{group.Name}: keine {kind}-Antwort zugestellt, die Aufzählung prüft sie nicht"
    }

[<Tests>]
let rowLockWays =
    testList "Zeilen-Sperre je Buchung und Gruppe" [
        testList "Kategorie und Split" [ walkTest categoryGroup 8 ]
        testList "Prüfvermerk" [ walkTest reviewMarkGroup 8 ]
        testList "Lena" [ walkTest lenaGroup 8 ]
        testList "Memo" [ walkTest memoGroup 8 ]
        testList "Betrag" [ walkTest amountGroup 8 ]
        testList "Noch nicht in YNAB" [ walkTest pushGroup 8 ]
    ]
