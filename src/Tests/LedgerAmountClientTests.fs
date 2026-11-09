module LedgerAmountClientTests

// ledger-m7mye: Betrag bearbeiten im Kassenbuch-Detail, Client-Seite (ADR 0092). Der Reducer und
// die reinen Bausteine laufen ohne Proxy: `Start...` setzt den Zustand und entscheidet, der Aufruf
// steckt hinter `Run...` (ADR 0020).

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private catId (n: int) = YnabCategoryId (Guid.Parse $"0195d800-0000-7000-8000-{n:D12}")

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f800-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e800-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -42.80m
      Payee = Some $"Rewe m7mye {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some (Guid.Parse "0195d800-0000-7000-8000-000000000001")
      CategoryGroupName = Some "Alltag"
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      Reconciled = false
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
      LinkedToYnab = true
      PushToYnabFailure = None
      SplitLocks = None
      ExternalLinks = [] }

let private idOf (n: int) = (entryOf n).Id

let private splitLines : TransactionSplit list =
    [ { Target = ToCategory (catId 1, "Lebensmittel"); Amount = eur -30.00m; Memo = None }
      { Target = ToCategory (catId 2, "Drogerie"); Amount = eur -12.80m; Memo = None } ]

let private splitEntry (n: int) = { entryOf n with CategoryId = None; CategoryName = None; Splits = splitLines }

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

let private rowOf (n: int) (model: Model) =
    match model.Entries with
    | Success entries -> entries |> List.find (fun e -> e.Id = idOf n)
    | _ -> failtest "Entries sollte geladen sein"

/// Das Sheet "Betrag" für die erste Buchung, mit dem getippten Entwurf.
let private typed (rows: LedgerEntryView list) (draft: string) : Model =
    let opened, _, _ = run (OpenAmountEdit (rows.Head.Id)) (modelWith rows)
    let model, _, _ = run (AmountDraftChanged draft) opened
    model

let private outcome (previous: decimal) (current: decimal) (writeBack: AmountWriteBack) : AmountOutcome =
    { PreviousAmount = eur previous
      CurrentAmount = eur current
      AmountChange = AmountChanged
      AmountWriteBack = writeBack }

// ============================================
// Reine Bausteine
// ============================================

[<Tests>]
let amountBuildingBlockTests =
    testList "Betrag bearbeiten: Sperre, Vorbelegung und Entscheidung (reine Bausteine)" [

        testCase "Sperre: der Grund des Lesepfads, bei einer aufgeteilten Buchung zusätzlich die Sperre der Zeilen" <| fun () ->
            Expect.isNone (amountLockFor (entryOf 1)) "gewöhnliche Buchung ist änderbar"
            Expect.isNone (amountLockFor (splitEntry 1)) "aufgeteilte Buchung ohne Sperre ist änderbar"
            Expect.equal
                (amountLockFor { entryOf 1 with AmountLockReason = Some "Abgeglichen" })
                (Some "Abgeglichen")
                "Grund aus dem Lesepfad"
            let foreign =
                { splitEntry 1 with
                    SplitLocks = Some { CannotSplitReason = None; CannotChangeReason = Some "fremde import_id"; ReplacementPending = false } }
            Expect.equal (amountLockFor foreign) (Some "fremde import_id") "Zeilen lassen sich nicht ändern"
            let plainWithSplitLock =
                { entryOf 1 with
                    SplitLocks = Some { CannotSplitReason = Some "Tracking"; CannotChangeReason = Some "Tracking"; ReplacementPending = false } }
            Expect.isNone (amountLockFor plainWithSplitLock) "die Split-Sperre gilt nur für aufgeteilte Buchungen"

        testCase "Vorbelegung: Richtung aus dem Vorzeichen, Betrag ohne Vorzeichen und ohne Tausenderpunkt" <| fun () ->
            let edit = amountEditFor { entryOf 1 with Amount = eur -1234.50m }
            Expect.equal (edit.AmountIsOutflow, edit.AmountDraft) (true, "1234,50") "Ausgabe"
            let income = amountEditFor { entryOf 1 with Amount = eur 3855m }
            Expect.equal (income.AmountIsOutflow, income.AmountDraft) (false, "3855,00") "Einnahme"

        testCase "Entscheidung beim Speichern: leer und 0 abgelehnt, gleich schließt, sonst die Anfrage ohne Vorzeichen" <| fun () ->
            let entry = entryOf 1
            let edit draft outflow = { AmountEntryId = entry.Id; AmountIsOutflow = outflow; AmountDraft = draft }
            Expect.equal (amountSaveStep (edit "" true) entry) AmountSaveRejected "leer"
            Expect.equal (amountSaveStep (edit "0" true) entry) AmountSaveRejected "0"
            Expect.equal (amountSaveStep (edit "0,00" true) entry) AmountSaveRejected "0,00"
            Expect.equal (amountSaveStep (edit "abc" true) entry) AmountSaveRejected "unlesbar"
            Expect.equal (amountSaveStep (edit "42,80" true) entry) AmountSaveUnchanged "gleich"
            Expect.equal
                (amountSaveStep (edit "45" true) entry)
                (AmountSaveSends { Amount = Some 45m; Direction = AmountDirectionOutflow })
                "Ausgabe"
            Expect.equal
                (amountSaveStep (edit "42,80" false) entry)
                (AmountSaveSends { Amount = Some 42.8m; Direction = AmountDirectionInflow })
                "nur die Richtung gewechselt ist eine Änderung"
            Expect.equal
                (amountSaveStep (edit "-45" true) entry)
                (AmountSaveSends { Amount = Some 45m; Direction = AmountDirectionOutflow })
                "ein getipptes Minus zählt nicht"
            Expect.equal
                (amountSaveStep (edit "45" true) (splitEntry 1))
                (AmountSaveOpensSplitEditor (eur -45m))
                "aufgeteilt: Split-Editor"

        testCase "Rückgängig-Anfrage aus einem Betrag mit Vorzeichen" <| fun () ->
            Expect.equal (amountRequestOf -12.5m) { Amount = Some 12.5m; Direction = AmountDirectionOutflow } "Ausgabe"
            Expect.equal (amountRequestOf 7m) { Amount = Some 7m; Direction = AmountDirectionInflow } "Einnahme"

        testCase "Hinweis Betrag nicht in YNAB mit Erneut senden, als Leiste im Detail" <| fun () ->
            let failing = { entryOf 1 with AmountWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            let notice = Components.Ledger.View.detailNoticeWithAmount None None None None None NotAsked failing |> Option.get
            match notice with
            | Components.Ledger.View.AmountNotice (hint, false) ->
                Expect.equal (hint.Title, hint.Reason) (amountHintTitle, "Keine Verbindung zu YNAB") "Titel und Grund"
            | other -> failtest $"erwartet AmountNotice, war {other}"
            Expect.equal
                (Components.Ledger.View.detailNoticeActionMsg failing.Id notice)
                (Some (StartRetryAmount failing.Id))
                "Erneut senden"
            Expect.isNone
                (Components.Ledger.View.detailNoticeWithAmount None None None None None NotAsked (entryOf 1))
                "ohne Fehlschlag keine Leiste"

        testCase "Der Betrags-Knopf wirkt nicht, solange gesperrt oder ein Aufruf läuft" <| fun () ->
            let model = modelWith [ entryOf 1 ]
            Expect.equal (Components.Ledger.View.amountEditMsg model (entryOf 1)) (Some (OpenAmountEdit (idOf 1))) "frei"
            let locked = { entryOf 1 with AmountLockReason = Some "Abgeglichen" }
            Expect.isNone (Components.Ledger.View.amountEditMsg model locked) "gesperrt"
            let busy = { model with AmountRows = Map.ofList [ idOf 1, AmountSending ] }
            Expect.isNone (Components.Ledger.View.amountEditMsg busy (entryOf 1)) "Aufruf läuft"

        testCase "Toast Rückgängig wird zur Nachricht StartUndoAmount" <| fun () ->
            match State.toastActionMsg (UndoAmount (idOf 1, -42.8m)) with
            | State.LedgerMsg (StartUndoAmount (id, previous)) ->
                Expect.equal (id, previous) (idOf 1, -42.8m) "Übersetzung"
            | other -> failtest $"erwartet StartUndoAmount, war {other}"
    ]

// ============================================
// Reducer
// ============================================

[<Tests>]
let amountReducerTests =
    testList "Betrag bearbeiten: Reducer" [

        testCase "Öffnen belegt Richtung und Betrag aus der Buchung vor" <| fun () ->
            let model, msgs, _ = run (OpenAmountEdit (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.equal
                model.AmountEdit
                (Some { AmountEntryId = idOf 1; AmountIsOutflow = true; AmountDraft = "42,80" })
                "vorbelegt"
            Expect.isEmpty msgs "kein Aufruf"

        testCase "eine gesperrte Buchung öffnet kein Sheet" <| fun () ->
            let locked = { entryOf 1 with AmountLockReason = Some "Abgeglichen" }
            let model, _, _ = run (OpenAmountEdit (idOf 1)) (modelWith [ locked ])
            Expect.isNone model.AmountEdit "zu"

        testCase "Richtung und Entwurf folgen der Eingabe, Abbrechen schließt ohne Aufruf" <| fun () ->
            let opened, _, _ = run (OpenAmountEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let switched, _, _ = run (AmountDirectionChanged false) opened
            let edited, _, _ = run (AmountDraftChanged "50") switched
            Expect.equal
                (edited.AmountEdit |> Option.map (fun e -> e.AmountIsOutflow, e.AmountDraft))
                (Some (false, "50"))
                "Einnahme, 50"
            let cancelled, msgs, external = run CancelAmountEdit edited
            Expect.isNone cancelled.AmountEdit "zu"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (rowOf 1 cancelled).Amount (eur -42.80m) "Betrag unverändert"

        testCase "Speichern ruft den Befehl mit Betrag ohne Vorzeichen und Richtung" <| fun () ->
            let model, msgs, _ = run (StartSaveAmount (idOf 1)) (typed [ entryOf 1 ] "45,00")
            Expect.equal (model.AmountRows |> Map.tryFind (idOf 1)) (Some AmountSending) "läuft"
            Expect.equal msgs [ RunSetAmount (idOf 1, { Amount = Some 45m; Direction = AmountDirectionOutflow }) ] "Aufruf"
            let again, msgs2, _ = run (StartSaveAmount (idOf 1)) model
            Expect.equal again model "kein zweiter Aufruf, solange einer läuft"
            Expect.isEmpty msgs2 "nichts"

        testCase "0 oder leer zeigt den Toast, das Sheet bleibt offen, kein Aufruf" <| fun () ->
            for draft in [ ""; "0"; "0,00" ] do
                let before = typed [ entryOf 1 ] draft
                let model, msgs, external = run (StartSaveAmount (idOf 1)) before
                Expect.equal external (ShowToast (amountMissingToastText, ToastError)) $"Toast bei '{draft}'"
                Expect.isSome model.AmountEdit "Sheet bleibt offen"
                Expect.isEmpty msgs "kein Aufruf"
                Expect.isEmpty model.AmountRows "keine Sperre"

        testCase "unverändert gespeichert: zurück ins Detail ohne Aufruf" <| fun () ->
            let model, msgs, external = run (StartSaveAmount (idOf 1)) (typed [ entryOf 1 ] "42,80")
            Expect.isNone model.AmountEdit "Sheet zu"
            Expect.isTrue (model.DetailFor |> Option.exists (fun d -> d.Id = idOf 1)) "Detail bleibt"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external NoOp "kein Toast"

        testCase "aufgeteilte Buchung: Speichern öffnet den Split-Editor mit dem neuen Betrag, ohne Aufruf" <| fun () ->
            let model, msgs, _ = run (StartSaveAmount (idOf 1)) (typed [ splitEntry 1 ] "50")
            Expect.isNone model.AmountEdit "Sheet zu"
            let editor = Expect.wantSome model.SplitEditor "Split-Editor offen"
            Expect.equal editor.Total (eur -50m) "neuer Betrag als Gesamtbetrag"
            Expect.equal editor.AmountFrom (Some (eur -42.80m)) "der alte Betrag ist gemerkt"
            Expect.equal (editor.Lines |> List.map (fun l -> l.AmountText)) [ "30,00"; "12,80" ] "die bisherigen Zeilen"
            Expect.isFalse (msgs |> List.exists (function RunSetAmount _ | RunSaveSplitsWithAmount _ -> true | _ -> false)) "kein Aufruf"
            Expect.equal (rowOf 1 model).Amount (eur -42.80m) "in BB noch nichts geändert"

        testCase "Abbrechen im Split-Editor ändert nichts" <| fun () ->
            let opened, _, _ = run (StartSaveAmount (idOf 1)) (typed [ splitEntry 1 ] "50")
            let closed, msgs, external = run CloseSplitEditor opened
            Expect.isNone closed.SplitEditor "Editor zu"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (rowOf 1 closed) (splitEntry 1) "Buchung unverändert"

        testCase "Speichern im Split-Editor nach dem Sheet schickt Betrag und Zeilen zusammen" <| fun () ->
            let opened, _, _ = run (StartSaveAmount (idOf 1)) (typed [ splitEntry 1 ] "50")
            let filled, _, _ = run (SetSplitLineAmount (1, "20,00")) opened
            let _, msgs, _ = run StartSaveSplits filled
            let expectedLines =
                [ { Target = ToCategory (catId 1, "Lebensmittel"); Amount = eur -30.00m; Memo = None }
                  { Target = ToCategory (catId 2, "Drogerie"); Amount = eur -20.00m; Memo = None } ]
            Expect.equal msgs [ RunSaveSplitsWithAmount (idOf 1, expectedLines, eur -50m) ] "ein Aufruf mit beidem"

        testCase "gewöhnliches Aufteilen ruft weiter setEntrySplits" <| fun () ->
            let opened, _, _ = run (OpenSplitEditor (idOf 1)) (modelWith [ splitEntry 1 ])
            let _, msgs, _ = run StartSaveSplits opened
            Expect.equal msgs [ RunSaveSplits (idOf 1, splitLines) ] "ohne Betrag"

        testCase "Split mit Betrag gespeichert: Betrag in der Zeile, Toast ohne Rückgängig" <| fun () ->
            let opened, _, _ = run (StartSaveAmount (idOf 1)) (typed [ splitEntry 1 ] "50")
            let lines =
                [ { Target = ToCategory (catId 1, "Lebensmittel"); Amount = eur -30.00m; Memo = None }
                  { Target = ToCategory (catId 2, "Drogerie"); Amount = eur -20.00m; Memo = None } ]
            let changed : CategoryChangeOutcome =
                { Previous = None; Change = CategoryChanged; WriteBack = CategorySent; PreviousTransferAccountId = None }
            let model, msgs, external = run (SplitsWithAmountSaved (idOf 1, lines, eur -50m, Ok changed)) opened
            Expect.equal (rowOf 1 model).Amount (eur -50m) "neuer Betrag"
            Expect.equal (rowOf 1 model).Splits lines "neue Zeilen"
            Expect.isNone model.SplitEditor "Editor zu"
            Expect.contains msgs (LoadBalances) "Kontostände neu"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.stringStarts text "Betrag geändert · " "Toast"
            | other -> failtest $"erwartet Toast ohne Rückgängig, war {other}"

        testCase "AmountSet Ok: Betrag in Zeile und Detail, Sheet zu, Toast mit Rückgängig, Historie und Kontostände neu" <| fun () ->
            let before, _, _ = run (StartSaveAmount (idOf 1)) (typed [ entryOf 1 ] "45")
            let model, msgs, external = run (AmountSet (idOf 1, Ok (outcome -42.80m -45m AmountSent))) before
            Expect.equal (rowOf 1 model).Amount (eur -45m) "Zeile"
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.Amount)) (Some (eur -45m)) "Detail zieht mit"
            Expect.isNone model.AmountEdit "Sheet zu"
            Expect.isEmpty model.AmountRows "Sperre gelöst"
            Expect.equal msgs [ LoadHistory (idOf 1); LoadBalances ] "Historie und Kontostände"
            match external with
            | ShowToastWithAction (text, ToastSuccess, { Kind = UndoAmount (id, previous) }) ->
                Expect.equal text (amountToastText (eur -45m)) "Text"
                Expect.stringStarts text "Betrag geändert · " "Prototyp-Satz"
                Expect.equal (id, previous) (idOf 1, -42.80m) "Rückgängig kennt den alten Betrag"
            | other -> failtest $"erwartet Toast mit Rückgängig, war {other}"

        testCase "AmountSet mit gescheitertem Versand: Fehlschlag an der Buchung, Warn-Toast, Betrag bleibt in BB" <| fun () ->
            let model, _, external =
                run (AmountSet (idOf 1, Ok (outcome -42.80m -45m (AmountSendFailed "YNAB weg")))) (modelWith [ entryOf 1 ])
            Expect.equal (rowOf 1 model).AmountWriteBackFailure (Some "YNAB weg") "Grund"
            Expect.equal (rowOf 1 model).Amount (eur -45m) "Betrag bleibt in BB"
            match external with
            | ShowToastWithAction (_, ToastWarning, { Kind = UndoAmount _ }) -> ()
            | other -> failtest $"erwartet Warn-Toast, war {other}"

        testCase "AmountSet Error: das Sheet bleibt offen, Fehler-Toast" <| fun () ->
            let before, _, _ = run (StartSaveAmount (idOf 1)) (typed [ entryOf 1 ] "45")
            let model, _, external = run (AmountSet (idOf 1, Error "Abgeglichen")) before
            Expect.isSome model.AmountEdit "Entwurf bleibt"
            Expect.equal (model.AmountRows |> Map.tryFind (idOf 1)) (Some (AmountCommandFailed "Abgeglichen")) "Befehlsfehler"
            Expect.equal external (ShowToast ("Betrag nicht geändert: Abgeglichen", ToastError)) "Toast"

        testCase "Rückgängig ruft den Befehl mit dem alten Betrag, der Rücknahme-Toast hat kein Rückgängig" <| fun () ->
            let started, msgs, _ = run (StartUndoAmount (idOf 1, -42.80m)) (modelWith [ { entryOf 1 with Amount = eur -45m } ])
            Expect.equal msgs [ RunUndoAmount (idOf 1, -42.80m) ] "Aufruf mit altem Betrag"
            Expect.equal (started.AmountRows |> Map.tryFind (idOf 1)) (Some AmountSending) "läuft"
            let restored, _, external = run (AmountRestored (idOf 1, Ok (outcome -45m -42.80m AmountSent))) started
            Expect.equal (rowOf 1 restored).Amount (eur -42.80m) "alter Betrag"
            Expect.equal external (ShowToast (amountRestoredToastText, ToastSuccess)) "Toast ohne Aktion"

        // Bug: bei offenem Split-Editor setzte der Betrags-Knopf `AmountEdit`, ohne dass ein Dialog erschien;
        // die Liste war gesperrt, nach dem Schließen des Editors sprang der Dialog auf, im Sheet ersetzte
        // "Speichern" den Split-Entwurf.
        testCase "bei offenem Split-Editor öffnet der Betrags-Knopf nichts" <| fun () ->
            let opened, _, _ = run (OpenSplitEditor (idOf 1)) (modelWith [ splitEntry 1 ])
            Expect.isSome opened.SplitEditor "Editor offen"
            let model, msgs, external = run (OpenAmountEdit (idOf 1)) opened
            Expect.equal model opened "Modell unverändert"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external NoOp "kein Toast"
            Expect.isNone (Components.Ledger.View.amountEditMsg opened (splitEntry 1)) "Knopf ohne Nachricht"

        // Bug: wird die Buchung gerade gelöscht, öffnete der Betrags-Knopf trotzdem das Sheet.
        testCase "während des Löschens öffnet der Betrags-Knopf nichts" <| fun () ->
            let deleting, _, _ = run (StartDeleteEntry (idOf 1)) (modelWith [ entryOf 1 ])
            let model, _, _ = run (OpenAmountEdit (idOf 1)) deleting
            Expect.isNone model.AmountEdit "kein Sheet"
            Expect.isNone (Components.Ledger.View.amountEditMsg deleting (entryOf 1)) "Knopf ohne Nachricht"

        // Bug: ein Doppeltipp auf "Rückgängig" schickte zwei Anfragen, auch neben einem laufenden Speichern.
        testCase "Rückgängig schickt nichts, solange für die Buchung ein Aufruf läuft" <| fun () ->
            let started, _, _ = run (StartUndoAmount (idOf 1, -42.80m)) (modelWith [ { entryOf 1 with Amount = eur -45m } ])
            let again, msgs, external = run (StartUndoAmount (idOf 1, -42.80m)) started
            Expect.equal again started "Modell unverändert"
            Expect.isEmpty msgs "kein zweiter Aufruf"
            Expect.equal external NoOp "kein Toast"
            let saving, _, _ = run (StartSaveAmount (idOf 1)) (typed [ entryOf 1 ] "45")
            let _, msgs2, _ = run (StartUndoAmount (idOf 1, -42.80m)) saving
            Expect.isEmpty msgs2 "nicht neben laufendem Speichern"

        testCase "Rückgängig gescheitert: Sperre frei mit dem Grund, Fehler-Toast, Betrag unverändert" <| fun () ->
            let started, _, _ = run (StartUndoAmount (idOf 1, -42.80m)) (modelWith [ { entryOf 1 with Amount = eur -45m } ])
            let model, msgs, external = run (AmountRestored (idOf 1, Error "YNAB weg")) started
            Expect.equal (model.AmountRows |> Map.tryFind (idOf 1)) (Some (AmountCommandFailed "YNAB weg")) "nicht mehr AmountSending"
            Expect.isSome (Components.Ledger.View.amountEditMsg model (rowOf 1 model)) "Knopf wieder frei"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external (ShowToast ("Rückgängig nicht möglich: YNAB weg", ToastError)) "Toast mit Grund"
            Expect.equal (rowOf 1 model).Amount (eur -45m) "Betrag unverändert"

        testCase "Erneut senden: Erfolg löscht den Fehlschlag, ein erneuter Fehlschlag ersetzt den Grund" <| fun () ->
            let failing = { entryOf 1 with AmountWriteBackFailure = Some "weg" }
            let started, msgs, _ = run (StartRetryAmount (idOf 1)) (modelWith [ failing ])
            Expect.equal msgs [ RunRetryAmount (idOf 1) ] "Aufruf"
            let sent, _, _ = run (AmountRetried (idOf 1, Ok AmountSent)) started
            Expect.isNone (rowOf 1 sent).AmountWriteBackFailure "Fehlschlag beendet"
            let again, _, _ = run (AmountRetried (idOf 1, Ok (AmountSendFailed "immer noch"))) started
            Expect.equal (rowOf 1 again).AmountWriteBackFailure (Some "immer noch") "neuer Grund"
    ]
