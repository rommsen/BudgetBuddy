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
      LinkedToYnab = true
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = [] }

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

        testCase "nach dem Löschen schließt das Detail und der Toast bietet Rückgängig" <| fun () ->
            let started, _, _ = run (StartDeleteEntry (idOf 1)) (modelWith [ entryOf 1 ])
            let model, sent, external = run (EntryDeleteFinished (idOf 1, Ok ())) started
            Expect.isNone model.DetailFor "Detail zu"
            Expect.isFalse (model.DeletingEntries.Contains (idOf 1)) "Sperre frei"
            Expect.contains sent LoadEntries "Liste neu"
            Expect.contains sent LoadBalances "Kontostände neu"
            match external with
            | ShowToastWithAction ("Buchung gelöscht, auch in YNAB", ToastSuccess, { Label = "Rückgängig"; Kind = UndoDeleteEntry id }) ->
                Expect.equal id (idOf 1) "Rückgängig für diese Buchung"
            | other -> failtest $"Toast mit Rückgängig erwartet, war {other}"

        testCase "ein Fehler zeigt den Grund und lässt das Detail offen" <| fun () ->
            let started, _, _ = run (StartDeleteEntry (idOf 1)) (modelWith [ entryOf 1 ])
            let model, sent, external = run (EntryDeleteFinished (idOf 1, Error "YNAB nicht erreichbar")) started
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.Id)) (Some (idOf 1)) "Detail offen"
            Expect.isEmpty sent "nichts neu geladen"
            Expect.equal external (ShowToast ("Löschen nicht möglich: YNAB nicht erreichbar", ToastError)) "Toast mit Grund"
            Expect.isFalse (model.DeletingEntries.Contains (idOf 1)) "Sperre frei"

        // Bug: nach dem Löschen blieb ein offenes Sheet "Betrag" stehen und sperrte die Liste.
        testCase "nach dem Löschen ist auch das Sheet Betrag zu" <| fun () ->
            let opened, _, _ = run (OpenAmountEdit (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.isSome opened.AmountEdit "Sheet offen"
            let started = { opened with DeletingEntries = opened.DeletingEntries.Add (idOf 1) }
            let model, _, _ = run (EntryDeleteFinished (idOf 1, Ok ())) started
            Expect.isNone model.DetailFor "Detail zu"
            Expect.isNone model.AmountEdit "Sheet Betrag zu"

        testCase "Rückgängig im Toast ruft das Wiederherstellen" <| fun () ->
            match State.toastActionMsg (UndoDeleteEntry (idOf 1)) with
            | State.LedgerMsg msg ->
                let _, sent, _ = run msg (modelWith [ entryOf 1 ])
                Expect.equal sent [ RunRestoreDeletedEntry (idOf 1) ] "Wiederherstellen"
            | other -> failtest $"LedgerMsg erwartet, war {other}"

        testCase "nach dem Wiederherstellen lädt die Liste neu, ein Fehler nennt den Grund" <| fun () ->
            let started, _, _ = run (StartRestoreDeletedEntry (idOf 1)) (modelWith [ entryOf 1 ])
            let _, again, _ = run (StartRestoreDeletedEntry (idOf 1)) started
            Expect.isEmpty again "kein zweiter Aufruf"
            let _, sent, external = run (EntryRestoreFinished (idOf 1, Ok ())) started
            Expect.contains sent LoadEntries "Liste neu"
            Expect.equal external (ShowToast ("Buchung wiederhergestellt", ToastSuccess)) "Toast"
            let failed, _, failure = run (EntryRestoreFinished (idOf 1, Error "Zeitüberschreitung")) started
            Expect.equal failure (ShowToast ("Wiederherstellen nicht möglich: Zeitüberschreitung", ToastError)) "Grund"
            Expect.isFalse (failed.RestoringEntries.Contains (idOf 1)) "Sperre frei"
    ]
