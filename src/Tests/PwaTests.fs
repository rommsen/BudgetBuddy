module Tests.PwaTests

// infrastructure-bnbdc (ADR 0057): die installierte PWA übernimmt einen neuen Deploy.
// Reine Entscheidungslogik (`Pwa.shouldCheck`, `Pwa.decide`), die Zustandsübergänge in
// `State.update` und der Schnappschuss `State.isInputActive`. Die Interop-Aufrufe
// (`Pwa.checkForUpdate`, `Pwa.applyUpdate`) laufen nur im Browser; hier zählt, dass `update`
// sie genau dann als Cmd auslöst, wenn die Tabelle es verlangt.

open System
open Expecto
open Types
open Components.Ledger.Types

let private minute = 60000L
let private t0 = 1_000_000_000L

// ============================================
// Modelle
// ============================================

let private baseModel () : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let syncFlowModel, _ = Components.SyncFlow.State.init ()
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = Ledger
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        SyncFlow = syncFlowModel
        Rules = rulesModel
        Ledger = ledgerModel
        QuickAdd = None
        QuickAddTemplates = NotAsked
    }

let private withLedger (f: Components.Ledger.Types.Model -> Components.Ledger.Types.Model) (m: State.Model) =
    { m with Ledger = f m.Ledger }

let private waiting (m: State.Model) = { m with Pwa = { m.Pwa with UpdateWaiting = true } }

let private blankForm : QuickAddFormState = {
    AmountText = ""
    IsOutflow = true
    Payee = ""
    CategoryId = ""
    DateText = "2026-10-24"
    Memo = ""
    ShowCategoryPicker = false
    IsSaving = false
    Error = None
}

let private anEntry : Shared.Domain.LedgerEntryView = {
    Id = Guid.Parse "0195f900-0000-7000-8000-000000000001"
    AccountId = Guid.Parse "0195e900-0000-7000-8000-000000000001"
    Date = DateTime(2026, 10, 1)
    Amount = { Amount = -10m; Currency = "EUR" }
    Payee = Some "Payee 1"
    Memo = None
    CategoryName = None
    CategoryId = None
    CategoryGroupName = None
    Splits = []
    Origin = Shared.Domain.MirroredFromYnab
    DeletedInYnab = false
    NeedsReview = true
    Flag = Shared.Domain.NoFlag
    IsTransfer = false
    SplitCategoryGroupNames = Map.empty
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
}

let private noticeToasts (m: State.Model) =
    m.Toasts |> List.filter (fun t -> t.Action |> Option.exists (fun a -> a.Kind = ReloadForUpdate))

// ============================================
// Tests
// ============================================

[<Tests>]
let shouldCheckTests =
    testList "Pwa.shouldCheck" [
        testCase "ohne letzte Prüfung ist eine Prüfung fällig" <| fun () ->
            Expect.isTrue (Pwa.shouldCheck None t0) "None ergibt true"
        testCase "4 Minuten 59 Sekunden nach der letzten Prüfung noch nicht" <| fun () ->
            Expect.isFalse (Pwa.shouldCheck (Some t0) (t0 + 4L * minute + 59000L)) "innerhalb der Drosselung"
        testCase "genau 5 Minuten nach der letzten Prüfung ist sie fällig" <| fun () ->
            Expect.isTrue (Pwa.shouldCheck (Some t0) (t0 + 5L * minute)) "Grenze zählt als fällig"
        testCase "eine rückwärts springende Uhr löst keine Prüfung aus" <| fun () ->
            Expect.isFalse (Pwa.shouldCheck (Some t0) (t0 - 10L * minute)) "now < last ergibt false"
    ]

[<Tests>]
let decideTests =
    let state w r n : Pwa.PwaState = { LastCheckMs = None; UpdateWaiting = w; Reloading = r; NoticeShown = n }
    testList "Pwa.decide" [
        testCase "ohne wartenden Worker immer Nothing" <| fun () ->
            for busy in [ false; true ] do
                for r in [ false; true ] do
                    for n in [ false; true ] do
                        Expect.equal (Pwa.decide (state false r n) busy) Pwa.Nothing "kein Worker, nichts zu tun"
        testCase "Worker wartet, keine Eingabe: sofort neu laden" <| fun () ->
            Expect.equal (Pwa.decide (state true false false) false) Pwa.ReloadNow "ReloadNow"
        testCase "Worker wartet, Eingabe läuft: Hinweis zeigen" <| fun () ->
            Expect.equal (Pwa.decide (state true false false) true) Pwa.ShowNotice "ShowNotice"
        testCase "Hinweis steht schon und Eingabe läuft: kein zweiter Hinweis" <| fun () ->
            Expect.equal (Pwa.decide (state true false true) true) Pwa.Nothing "Nothing"
        testCase "Hinweis steht, Eingabe ist vorbei: beim nächsten Zurückholen wird neu geladen" <| fun () ->
            Expect.equal (Pwa.decide (state true false true) false) Pwa.ReloadNow "ReloadNow"
        testCase "Neuladen läuft: nie ein zweiter Reload, nie ein Hinweis" <| fun () ->
            for busy in [ false; true ] do
                for n in [ false; true ] do
                    Expect.equal (Pwa.decide (state true true n) busy) Pwa.Nothing "keine Schleife"
    ]

[<Tests>]
let updateTests =
    testList "State.update (PWA)" [
        testCase "PwaForegrounded innerhalb der Drosselung löst keinen Cmd aus und lässt LastCheckMs stehen" <| fun () ->
            let model = { baseModel () with Pwa = { Pwa.initialState with LastCheckMs = Some t0 } }
            let after, cmd = State.update (State.PwaForegrounded (t0 + 4L * minute)) model
            Expect.equal after.Pwa.LastCheckMs (Some t0) "LastCheckMs bleibt"
            Expect.isEmpty cmd "keine Prüfung"
        testCase "PwaForegrounded außerhalb der Drosselung setzt LastCheckMs und löst genau eine Prüfung aus" <| fun () ->
            let model = { baseModel () with Pwa = { Pwa.initialState with LastCheckMs = Some t0 } }
            let now = t0 + 5L * minute
            let after, cmd = State.update (State.PwaForegrounded now) model
            Expect.equal after.Pwa.LastCheckMs (Some now) "LastCheckMs neu"
            Expect.equal (List.length cmd) 1 "genau eine Prüfung"
            Expect.isFalse after.Pwa.Reloading "ohne wartenden Worker kein Neuladen"
        testCase "PwaForegrounded mit wartendem Worker und ruhigem Modell lädt neu, auch innerhalb der Drosselung" <| fun () ->
            let model = { waiting (baseModel ()) with Pwa = { Pwa.initialState with UpdateWaiting = true; LastCheckMs = Some t0 } }
            let after, cmd = State.update (State.PwaForegrounded (t0 + minute)) model
            Expect.isTrue after.Pwa.Reloading "Reloading"
            Expect.equal (List.length cmd) 1 "applyUpdate"
        testCase "PwaUpdateWaiting im Ruhezustand setzt Reloading und löst applyUpdate aus" <| fun () ->
            let after, cmd = State.update State.PwaUpdateWaiting (baseModel ())
            Expect.isTrue after.Pwa.UpdateWaiting "UpdateWaiting"
            Expect.isTrue after.Pwa.Reloading "Reloading"
            Expect.equal (List.length cmd) 1 "genau ein Cmd"
            Expect.isEmpty after.Toasts "kein Hinweis"
        testCase "PwaUpdateWaiting mit offenem Detail zeigt den Hinweis und lädt nicht" <| fun () ->
            let model = baseModel () |> withLedger (fun l -> { l with DetailFor = Some anEntry })
            let after, cmd = State.update State.PwaUpdateWaiting model
            Expect.isFalse after.Pwa.Reloading "Reloading bleibt false"
            Expect.isTrue after.Pwa.NoticeShown "NoticeShown"
            Expect.equal (List.length (noticeToasts after)) 1 "ein Hinweis-Toast"
            Expect.isEmpty cmd "kein Cmd, vor allem kein Auto-Dismiss nach 5 s"
        testCase "ein zweites PwaUpdateWaiting fügt keinen zweiten Hinweis hinzu" <| fun () ->
            let model = baseModel () |> withLedger (fun l -> { l with DetailFor = Some anEntry })
            let once, _ = State.update State.PwaUpdateWaiting model
            let twice, cmd = State.update State.PwaUpdateWaiting once
            Expect.equal (List.length (noticeToasts twice)) 1 "weiterhin ein Hinweis"
            Expect.isEmpty cmd "kein Cmd"
        testCase "Der Hinweis lässt sich schließen; danach kann er wieder erscheinen" <| fun () ->
            let model = baseModel () |> withLedger (fun l -> { l with DetailFor = Some anEntry })
            let shown, _ = State.update State.PwaUpdateWaiting model
            let toast = List.head (noticeToasts shown)
            let closing, _ = State.update (State.StartDismissToast toast.Id) shown
            Expect.isTrue (closing.Toasts |> List.exists (fun t -> t.Id = toast.Id && t.Exiting)) "Toast geht ab"
            Expect.isFalse closing.Pwa.NoticeShown "NoticeShown wieder false"
            let removed, _ = State.update (State.ToastExited toast.Id) closing
            Expect.isEmpty removed.Toasts "Toast entfernt"
            let again, _ = State.update State.PwaUpdateWaiting removed
            Expect.equal (List.length (noticeToasts again)) 1 "neuer Hinweis bei laufender Eingabe"
        testCase "Der Hinweis-Toast hat keinen Auto-Dismiss: nach PwaUpdateWaiting ist kein Zeitgeber geplant" <| fun () ->
            // Ein normaler Toast liefert einen Cmd (StartDismissToast nach 5 s), der Hinweis nicht.
            let normal, normalCmd = State.update (State.ShowToast ("x", ToastInfo)) (baseModel ())
            Expect.equal (List.length normalCmd) 1 "normaler Toast plant den Abgang"
            Expect.equal (List.length normal.Toasts) 1 "ein Toast"
            let busy = baseModel () |> withLedger (fun l -> { l with SortSheetOpen = true })
            let _, noticeCmd = State.update State.PwaUpdateWaiting busy
            Expect.isEmpty noticeCmd "der Hinweis plant keinen Abgang"
        testCase "Ein Klick auf 'Neu laden' setzt Reloading und löst applyUpdate aus" <| fun () ->
            let model = baseModel () |> withLedger (fun l -> { l with DetailFor = Some anEntry })
            let shown, _ = State.update State.PwaUpdateWaiting model
            let toast = List.head (noticeToasts shown)
            Expect.equal (State.toastActionMsg ReloadForUpdate) State.PwaApplyUpdate "Übersetzung"
            let clicked, _ = State.update (State.ToastActionClicked toast.Id) shown
            Expect.isFalse clicked.Pwa.NoticeShown "Hinweis geschlossen"
            let applied, cmd = State.update State.PwaApplyUpdate clicked
            Expect.isTrue applied.Pwa.Reloading "Reloading"
            Expect.equal (List.length cmd) 1 "applyUpdate"
        testCase "PwaApplyUpdate bei laufendem Neuladen löst keinen zweiten Reload aus" <| fun () ->
            let model = { baseModel () with Pwa = { Pwa.initialState with Reloading = true } }
            let _, cmd = State.update State.PwaApplyUpdate model
            Expect.isEmpty cmd "kein zweiter Reload"
        testCase "Erste Installation: ohne PwaUpdateWaiting bleibt UpdateWaiting false" <| fun () ->
            // Beim ersten Installieren feuert onNeedRefresh nicht (Kommentar in Pwa.fs); ein
            // Vordergrund-Ereignis allein setzt UpdateWaiting nie.
            let after, _ = State.update (State.PwaForegrounded t0) (baseModel ())
            Expect.isFalse after.Pwa.UpdateWaiting "UpdateWaiting bleibt false"
    ]

[<Tests>]
let inputActiveTests =
    let active name (change: State.Model -> State.Model) =
        testCase $"isInputActive ist true bei: {name}" <| fun () ->
            Expect.isTrue (State.isInputActive (change (baseModel ()))) name
    let session status : Shared.Domain.SyncSession =
        { Id = Shared.Domain.SyncSessionId (Guid.NewGuid())
          StartedAt = DateTime(2026, 10, 24)
          CompletedAt = None
          Status = status
          TransactionCount = 0
          ImportedCount = 0
          SkippedCount = 0 }
    testList "State.isInputActive" [
        testCase "frisch initialisiertes Modell: keine Eingabe" <| fun () ->
            Expect.isFalse (State.isInputActive (baseModel ())) "ruhig"
        testCase "nur MirrorRunning und geladene Daten: keine Eingabe" <| fun () ->
            let model =
                baseModel ()
                |> withLedger (fun l -> { l with MirrorRunning = true; Entries = Success [ anEntry ]; TotalCount = 1 })
            Expect.isFalse (State.isInputActive model) "Spiegel und Daten zählen nicht"
        testCase "ein Entwurf, der dem Filter entspricht, ist keine Eingabe" <| fun () ->
            let model =
                baseModel ()
                |> withLedger (fun l ->
                    { l with
                        Filters = { l.Filters with Memo = Some "Miete"; AmountMin = Some 12.5m }
                        MemoDraft = "Miete"
                        AmountMinDraft = formatAmountDraft 12.5m })
            Expect.isFalse (State.isInputActive model) "Entwurf = Filter"
        active "Kassenbuch: DetailFor" (withLedger (fun l -> { l with DetailFor = Some anEntry }))
        active "Kassenbuch: OpenPopover" (withLedger (fun l -> { l with OpenPopover = Some CategoryChip }))
        active "Kassenbuch: SortSheetOpen" (withLedger (fun l -> { l with SortSheetOpen = true }))
        active "Kassenbuch: SelectionMode" (withLedger (fun l -> { l with SelectionMode = true }))
        active "Kassenbuch: nicht leere Selection" (withLedger (fun l -> { l with Selection = Picked (Set.singleton anEntry.Id) }))
        active "Kassenbuch: MemoDraft weicht ab" (withLedger (fun l -> { l with MemoDraft = "Mie" }))
        active "Kassenbuch: AmountMinDraft weicht ab" (withLedger (fun l -> { l with AmountMinDraft = "5" }))
        active "Kassenbuch: AmountMaxDraft weicht ab" (withLedger (fun l -> { l with AmountMaxDraft = "9" }))
        active "Kassenbuch: ReviewMarkBatchRunning" (withLedger (fun l -> { l with ReviewMarkBatchRunning = true }))
        active "Einstellungen: EditingSection" (fun m ->
            { m with Settings = { m.Settings with EditingSection = Some Components.Settings.Types.YnabSection } })
        active "Schnellerfassung: Betrag ausgefüllt" (fun m -> { m with QuickAdd = Some { blankForm with AmountText = "4,50" } })
        active "Schnellerfassung: Payee ausgefüllt" (fun m -> { m with QuickAdd = Some { blankForm with Payee = "Bäcker" } })
        active "Schnellerfassung: Memo ausgefüllt" (fun m -> { m with QuickAdd = Some { blankForm with Memo = "x" } })
        active "Schnellerfassung: Kategorie ausgefüllt" (fun m -> { m with QuickAdd = Some { blankForm with CategoryId = Guid.NewGuid().ToString() } })
        active "Schnellerfassung: IsSaving" (fun m -> { m with QuickAdd = Some { blankForm with IsSaving = true } })
        testCase "Schnellerfassung: leeres Formular ist keine Eingabe" <| fun () ->
            Expect.isFalse (State.isInputActive { baseModel () with QuickAdd = Some blankForm }) "leer"
        active "Sync-Ablauf: laufende Sitzung" (fun m ->
            { m with SyncFlow = { m.SyncFlow with CurrentSession = Success (Some (session Shared.Domain.FetchingTransactions)) } })
        active "Sync-Ablauf: TAN-Bestätigung läuft" (fun m -> { m with SyncFlow = { m.SyncFlow with IsTanConfirming = true } })
        active "Sync-Ablauf: offene Aufteilung" (fun m ->
            let split : Components.SyncFlow.Types.SplitEditState =
                { TransactionId = Shared.Domain.TransactionId "tx-1"
                  Total = { Amount = -10m; Currency = "EUR" }
                  Lines = []
                  Currency = "EUR"
                  ActivePicker = Components.SyncFlow.Types.NoPicker }
            { m with SyncFlow = { m.SyncFlow with SplitEdit = Some split } })
        active "Regeln: EditingRule" (fun m ->
            let rule : Shared.Domain.Rule =
                { Id = Shared.Domain.RuleId (Guid.NewGuid())
                  Name = "Amazon"
                  Pattern = "amazon"
                  PatternType = Shared.Domain.Contains
                  TargetField = Shared.Domain.Combined
                  CategoryId = Shared.Domain.YnabCategoryId (Guid.NewGuid())
                  CategoryName = "Shopping"
                  PayeeOverride = None
                  Priority = 0
                  Enabled = true
                  CreatedAt = DateTime.UtcNow
                  UpdatedAt = DateTime.UtcNow }
            { m with Rules = { m.Rules with EditingRule = Some rule } })
        testCase "Sync-Ablauf: abgeschlossene Sitzung ist keine Eingabe" <| fun () ->
            let m = baseModel ()
            let m' = { m with SyncFlow = { m.SyncFlow with CurrentSession = Success (Some (session Shared.Domain.Completed)) } }
            Expect.isFalse (State.isInputActive m') "Completed"
    ]
