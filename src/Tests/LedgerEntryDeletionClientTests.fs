module LedgerEntryDeletionClientTests

// Buchung löschen im Detail, Client-Seite (ledger-z6sya, ADR 0092). Der Reducer läuft ohne
// Proxy: `Start...` setzt den Zustand, der Aufruf steckt hinter `Run...` (ADR 0020). Die
// Sichtbarkeit des Knopfs ist eine reine Funktion (`View.detailDeleteVisible`).

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f800-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e800-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -42.80m
      Payee = Some $"Payee z6sya {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some (Guid.Parse "0195d800-0000-7000-8000-000000000001")
      CategoryGroupName = Some "Alltag"
      Reconciled = false
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = false
      Flag = NoFlag
      IsTransfer = false
      TransferAccountId = None
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      AmountWriteBackFailure = None
      AmountLockReason = None
      PayeeWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = []
      PossibleDuplicate = None; CategoryFromOrderId = false }

let private idOf (n: int) = (entryOf n).Id

let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        DetailFor = rows |> List.tryHead
        DetailForm = DetailPanel }

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

[<Tests>]
let deleteVisibilityTests =
    testList "Buchung löschen: Sichtbarkeit des Knopfs" [
        testCase "sichtbar bei einer verknüpften, offenen Buchung" <| fun () ->
            Expect.isTrue (Components.Ledger.View.detailDeleteVisible (entryOf 1)) "sichtbar"
        testCase "nicht bei abgeglichen" <| fun () ->
            Expect.isFalse (Components.Ledger.View.detailDeleteVisible { entryOf 1 with Reconciled = true }) "abgeglichen"
        testCase "nicht bei gelöscht" <| fun () ->
            Expect.isFalse (Components.Ledger.View.detailDeleteVisible { entryOf 1 with DeletedInYnab = true }) "gelöscht"
        testCase "nicht bei Noch nicht in YNAB" <| fun () ->
            Expect.isFalse (Components.Ledger.View.detailDeleteVisible { entryOf 1 with LinkedToYnab = false }) "nicht verknüpft"
    ]

[<Tests>]
let deleteUpdateTests =
    testList "Buchung löschen: Client-update" [
        testCase "Löschen startet den Aufruf einmal, ein zweiter Tipp schickt nichts" <| fun () ->
            let model, sent, _ = run (StartDeleteEntry (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.equal sent [ RunDeleteEntry (idOf 1) ] "Aufruf"
            let _, again, _ = run (StartDeleteEntry (idOf 1)) model
            Expect.isEmpty again "kein zweiter Aufruf"

        testCase "nach dem Löschen schließt das Detail, die Zeile steht am Platz, keine Meldung" <| fun () ->
            let started, _, _ = run (StartDeleteEntry (idOf 2)) { modelWith [ entryOf 1; entryOf 2; entryOf 3 ] with DetailFor = Some (entryOf 2) }
            let model, sent, external = run (EntryDeleteFinished (idOf 2, Ok ())) started
            Expect.isNone model.DetailFor "Detail zu"
            Expect.isFalse (model.DeletingEntries.Contains (idOf 2)) "Sperre frei"
            Expect.contains sent RefreshEntries "Liste lädt im Hintergrund nach"
            Expect.contains sent LoadBalances "Kontostände neu"
            Expect.equal external NoOp "kein Toast, keine Leiste"
            Expect.equal
                model.DeletedRow
                (Some { Entry = entryOf 2; Index = 1; Before = Some (idOf 3); Phase = DeletedRowShown })
                "Zeile am Platz vor der nächsten Buchung"

        testCase "steht die Buchung nicht in der Liste, meldet die Leiste (am PC der Toast) mit Rückgängig" <| fun () ->
            let model = { modelWith [ entryOf 2 ] with DetailFor = Some (entryOf 1) }
            let started, _, _ = run (StartDeleteEntry (idOf 1)) model
            let after, _, external = run (EntryDeleteFinished (idOf 1, Ok ())) started
            Expect.isNone after.DeletedRow "keine Zeile"
            Expect.equal
                external
                (PostFeedback
                    { Tone = FeedbackOk
                      Action = "Gelöscht"
                      Detail = Some "Payee z6sya 1"
                      Act = Some { Label = "Rückgängig"; Kind = UndoDeleteEntry (idOf 1) } })
                "Meldung mit Payee und Rückgängig"
            // Am PC zeigt der Meldungsplatz denselben Zustand als Toast (design-system-gkded).
            let note = match external with PostFeedback note -> note | other -> failtest $"{other}"
            let message = FeedbackSlot.ofNote (Guid.NewGuid ()) note
            Expect.equal (FeedbackSlot.toastText message) "Gelöscht · Payee z6sya 1" "Text im PC-Toast"

        testCase "im Durchgehen gibt es keine Liste: Meldung statt Zeile" <| fun () ->
            let model = Components.Ledger.State.deletedRowFor { modelWith [ entryOf 1 ] with Review = Some reviewStarting } (idOf 1)
            Expect.isNone model "keine Zeile"

        testCase "ein Fehler steht unter dem Knopf, das Detail bleibt offen, kein Toast" <| fun () ->
            let started, _, _ = run (StartDeleteEntry (idOf 1)) (modelWith [ entryOf 1 ])
            let model, sent, external = run (EntryDeleteFinished (idOf 1, Error "YNAB nicht erreichbar")) started
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 1)) "Detail offen"
            Expect.isEmpty sent "nichts neu geladen"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (Components.Ledger.View.deleteFailureFor model (entryOf 1)) (Some "YNAB nicht erreichbar") "Grund am Knopf"
            Expect.isNone (Components.Ledger.View.deleteFailureFor model (entryOf 2)) "nur bei dieser Buchung"
            Expect.isFalse (model.DeletingEntries.Contains (idOf 1)) "Sperre frei"
            let again, _, _ = run (StartDeleteEntry (idOf 1)) model
            Expect.isNone again.DeleteFailure "ein neuer Versuch nimmt den Grund weg"
            let closed, _, _ = run CloseDetail model
            Expect.isNone closed.DeleteFailure "Schließen nimmt den Grund weg"

        // Bug: nach dem Löschen blieb ein offenes Sheet "Betrag" stehen und sperrte die Liste.
        testCase "nach dem Löschen ist auch das Sheet Betrag zu" <| fun () ->
            let opened, _, _ = run (OpenAmountEdit (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.isSome opened.AmountEdit "Sheet offen"
            let started = { opened with DeletingEntries = opened.DeletingEntries.Add (idOf 1) }
            let model, _, _ = run (EntryDeleteFinished (idOf 1, Ok ())) started
            Expect.isNone model.DetailFor "Detail zu"
            Expect.isNone model.AmountEdit "Sheet Betrag zu"

        testCase "Rückgängig in der Leiste ruft das Wiederherstellen" <| fun () ->
            match State.toastActionMsg (UndoDeleteEntry (idOf 1)) with
            | State.LedgerMsg msg ->
                let _, sent, _ = run msg (modelWith [ entryOf 1 ])
                Expect.equal sent [ RunRestoreDeletedEntry (idOf 1) ] "Wiederherstellen"
            | other -> failtest $"LedgerMsg erwartet, war {other}"
    ]

let private shown (row: DeletedEntryRow) (n: int list) =
    Components.Ledger.State.withDeletedPlaced (Some row) (n |> List.map entryOf) |> List.map (fun e -> e.Id)

let private deletedRowOf (n: int) (index: int) (before: int option) : DeletedEntryRow =
    { Entry = entryOf n; Index = index; Before = before |> Option.map idOf; Phase = DeletedRowShown }

/// Löscht Buchung 2 aus der Liste 1, 2, 3 und nimmt sie beim Nachladen aus der Liste.
let private deletedSecond () : Model =
    let started, _, _ = run (StartDeleteEntry (idOf 2)) { modelWith [ entryOf 1; entryOf 2; entryOf 3 ] with DetailFor = Some (entryOf 2) }
    let model, _, _ = run (EntryDeleteFinished (idOf 2, Ok ())) started
    { model with Entries = Success [ entryOf 1; entryOf 3 ] }

[<Tests>]
let deletedRowTests =
    testList "Buchung löschen: Zeile am Platz (ledger-k68sd)" [
        testCase "die Zeile bleibt an ihrem Platz, auch wenn das Nachladen die Buchung aus der Liste nimmt" <| fun () ->
            let row = deletedRowOf 2 1 (Some 3)
            Expect.equal (shown row [ 1; 3 ]) [ idOf 1; idOf 2; idOf 3 ] "vor der nächsten"
            Expect.equal (shown row [ 0; 1; 3 ]) [ idOf 0; idOf 1; idOf 2; idOf 3 ] "vor der nächsten, auch wenn oben etwas dazukam"
            Expect.equal (shown row [ 1 ]) [ idOf 1; idOf 2 ] "ohne die nächste am alten Index"
            Expect.equal (shown (deletedRowOf 3 2 None) [ 1; 2 ]) [ idOf 1; idOf 2; idOf 3 ] "letzte bleibt letzte"
            Expect.equal (shown row []) [ idOf 2 ] "leere Liste"

        testCase "mit „gelöschte zeigen“ bleibt es eine Zeile, keine zweite" <| fun () ->
            let row = deletedRowOf 2 1 (Some 3)
            let stillThere = [ entryOf 1; { entryOf 2 with DeletedInYnab = true }; entryOf 3 ]
            let placed = Components.Ledger.State.withDeletedPlaced (Some row) stillThere
            Expect.equal (placed |> List.map (fun e -> e.Id)) [ idOf 1; idOf 2; idOf 3 ] "eine Zeile"

        testCase "ohne Zeile bleibt die Liste, wie sie ist" <| fun () ->
            let entries = [ entryOf 1; entryOf 3 ]
            Expect.equal (Components.Ledger.State.withDeletedPlaced None entries) entries "unverändert"

        testCase "welche Nachricht die Zeile beendet" <| fun () ->
            let ends =
                [ StartSwipeReviewed (idOf 1); StartMirror; OpenCapture; OpenCaptureUnfiltered; OpenDetail (entryOf 1)
                  SelectFilter AllAccounts; ToggleIncludeDeleted; CommitSearch ("rewe", 1); ClearAllFilters; StartDeleteEntry (idOf 1)
                  StartSelectionMode (idOf 1); LedgerLeft; ReviewOpened ]
            for msg in ends do
                Expect.isTrue (Components.Ledger.State.endsDeletedRow msg) $"{msg} beendet"
            let stays =
                [ RefreshEntries; LoadBalances; StartRestoreDeletedEntry (idOf 2); EntryRestoreFinished (idOf 2, Ok ())
                  EntryDeleteFinished (idOf 2, Ok ()); CloseDetail; LoadMirrorStatus ]
            for msg in stays do
                Expect.isFalse (Components.Ledger.State.endsDeletedRow msg) $"{msg} lässt die Zeile"

        testCase "die nächste Aktion nimmt die Zeile weg, Nachladen nicht" <| fun () ->
            let model = deletedSecond ()
            let refreshed, _, _ = run RefreshEntries model
            Expect.isSome refreshed.DeletedRow "Nachladen lässt sie"
            let mirrored, _, _ = run StartMirror model
            Expect.isNone mirrored.DeletedRow "Spiegeln nimmt sie weg"
            let opened, _, _ = run (OpenDetail (entryOf 1)) model
            Expect.isNone opened.DeletedRow "eine andere Buchung öffnen nimmt sie weg"
            let left, _, _ = run LedgerLeft model
            Expect.isNone left.DeletedRow "Seite verlassen nimmt sie weg"

        testCase "ein Neuaufbau der Liste nimmt die Zeile weg" <| fun () ->
            let rebuilt, _, _ = run (SelectFilter AllAccounts) (deletedSecond ())
            Expect.isNone rebuilt.DeletedRow "weg"

        testCase "Rückgängig in der Zeile: läuft, ein zweiter Tipp schickt nichts, danach steht die Buchung an ihrem Platz" <| fun () ->
            let restoring, sent, _ = run (StartRestoreDeletedEntry (idOf 2)) (deletedSecond ())
            Expect.equal sent [ RunRestoreDeletedEntry (idOf 2) ] "Aufruf"
            Expect.equal (restoring.DeletedRow |> Option.map (fun r -> r.Phase)) (Some DeletedRowRestoring) "Zeile zeigt den Lauf"
            let _, again, _ = run (StartRestoreDeletedEntry (idOf 2)) restoring
            Expect.isEmpty again "kein zweiter Aufruf"
            let restored, reload, external = run (EntryRestoreFinished (idOf 2, Ok ())) restoring
            Expect.isNone restored.DeletedRow "Zeile weg"
            Expect.equal
                (restored.Entries |> RemoteData.map (List.map (fun e -> e.Id)))
                (Success [ idOf 1; idOf 2; idOf 3 ])
                "die Buchung steht wieder an ihrem Platz"
            Expect.equal external NoOp "kein Text „wiederhergestellt“"
            Expect.contains reload RefreshEntries "Liste lädt im Hintergrund nach"

        testCase "scheitert das Wiederherstellen, wird die Zeile rot mit Grund, Rückgängig bleibt" <| fun () ->
            let restoring, _, _ = run (StartRestoreDeletedEntry (idOf 2)) (deletedSecond ())
            let failed, _, external = run (EntryRestoreFinished (idOf 2, Error "Zeitüberschreitung")) restoring
            Expect.equal external NoOp "kein Toast"
            Expect.equal (failed.DeletedRow |> Option.map (fun r -> r.Phase)) (Some (DeletedRowRestoreFailed "Zeitüberschreitung")) "rot mit Grund"
            Expect.isFalse (failed.RestoringEntries.Contains (idOf 2)) "Sperre frei"
            let undo : Client.DesignSystem.Feedback.Act = { Label = "Rückgängig"; OnClick = ignore }
            match Components.Ledger.View.deletedRowPhase undo (DeletedRowRestoreFailed "Zeitüberschreitung") with
            | Client.DesignSystem.Feedback.RemovedPhase.RestoreFailed (reason, act) ->
                Expect.equal reason "Zeitüberschreitung" "Grund"
                Expect.equal act.Label "Rückgängig" "Rückgängig zum erneuten Versuch"
            | other -> failtest $"RestoreFailed erwartet, war {other}"
            let retry, sent, _ = run (StartRestoreDeletedEntry (idOf 2)) failed
            Expect.equal sent [ RunRestoreDeletedEntry (idOf 2) ] "erneuter Versuch"
            Expect.equal (retry.DeletedRow |> Option.map (fun r -> r.Phase)) (Some DeletedRowRestoring) "läuft wieder"

        testCase "ohne Zeile meldet ein gescheitertes Wiederherstellen rot am Meldungsplatz" <| fun () ->
            let started, _, _ = run (StartRestoreDeletedEntry (idOf 1)) (modelWith [ entryOf 2 ])
            let _, _, external = run (EntryRestoreFinished (idOf 1, Error "Zeitüberschreitung")) started
            Expect.equal
                external
                (PostFeedback
                    { Tone = FeedbackError
                      Action = "Nicht wiederhergestellt"
                      Detail = Some "Zeitüberschreitung"
                      Act = Some { Label = "Rückgängig"; Kind = UndoDeleteEntry (idOf 1) } })
                "rot mit Rückgängig"
            let _, _, ok = run (EntryRestoreFinished (idOf 1, Ok ())) started
            Expect.equal ok NoOp "Erfolg ohne Text"

        testCase "die Tagessumme zählt die gelöschte Buchung nicht" <| fun () ->
            let sum = Components.Ledger.View.daySumWithout (Some (idOf 2)) false [ entryOf 1; entryOf 2 ]
            Expect.equal sum.Amount -42.80m "nur die eine"

        testCase "die Zeile nennt den Payee, ohne Payee „Buchung gelöscht“" <| fun () ->
            Expect.equal (Components.Ledger.View.deletedRowName (entryOf 1)) (Some "Payee z6sya 1") "Payee"
            Expect.isNone (Components.Ledger.View.deletedRowName { entryOf 1 with Payee = None }) "ohne"
            Expect.isNone (Components.Ledger.View.deletedRowName { entryOf 1 with Payee = Some "  " }) "leer"

        testCase "Verlassen des Kassenbuchs beendet die Zeile, Wechsel zu Regeln oder zur Buchung nicht" <| fun () ->
            let ledger, _ = Components.Ledger.State.init ()
            let leaves (current: Page) (target: Page) =
                let seen = ResizeArray<State.Msg> ()
                for sub in State.urlChangedCmd ledger current target do
                    sub seen.Add
                seen |> Seq.contains (State.LedgerMsg LedgerLeft)
            Expect.isTrue (leaves Page.Ledger Page.Overview) "Kassenbuch → Übersicht"
            Expect.isTrue (leaves (Page.LedgerEntry (idOf 1)) Page.Budget) "Buchung → Budget"
            Expect.isFalse (leaves Page.Ledger Page.LedgerRules) "Regeln sind dieselbe Seite"
            Expect.isFalse (leaves Page.Ledger (Page.LedgerEntry (idOf 1))) "Buchung"
            Expect.isFalse (leaves Page.Overview Page.Ledger) "kommt"
    ]
