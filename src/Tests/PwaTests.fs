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
    let rulesModel, _ = Components.Rules.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    {
        CurrentPage = Ledger
        Toasts = []
        Pwa = Pwa.initialState
        Settings = settingsModel
        Fetch = Components.Fetch.Types.init
        Rules = rulesModel
        Ledger = ledgerModel
        Budget = Components.Budget.State.init ()
        Overview = Components.Overview.Types.init; SettingsReturn = Types.Page.Overview
    }

let private withLedger (f: Components.Ledger.Types.Model -> Components.Ledger.Types.Model) (m: State.Model) =
    { m with Ledger = f m.Ledger }

let private waiting (m: State.Model) = { m with Pwa = { m.Pwa with UpdateWaiting = true } }

/// Ein offenes Formular "Neue Buchung" mit geladener Vorbelegung und vorbelegtem Konto, sonst leer
/// (ledger-ea70s: der Schutz vor dem Neuladen kam von Quick Add hierher).
let private blankCapture : Components.Ledger.Capture.CaptureState =
    let today = DateTime(2026, 10, 24)
    { Components.Ledger.Capture.opening with
        Defaults =
            Success
                { Today = today
                  ImportAccountId = None
                  LastUsedAccountId = None
                  QuickAddAccountId = None
                  Recent = [] }
        Form = { Components.Ledger.Capture.emptyForm today with AccountId = Some (Guid.Parse "0195e900-0000-7000-8000-000000000001") } }

let private withCapture (change: Components.Ledger.Capture.CaptureState -> Components.Ledger.Capture.CaptureState) (m: State.Model) =
    { m with Ledger = { m.Ledger with Capture = Some (change blankCapture) } }

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
    TransferAccountId = None
    Reconciled = false
    SplitCategoryGroupNames = Map.empty
    ReviewMarkWriteBackFailure = None
    CategorizationWriteBackFailure = None
    FlagWriteBackFailure = None
    MemoWriteBackFailure = None
    AmountWriteBackFailure = None
    AmountLockReason = None
    PayeeWriteBackFailure = None
    LinkedToYnab = false
    PushToYnabFailure = None
    SplitLocks = None
    ExternalLinks = []
    PossibleDuplicate = None; CategoryFromOrderId = false
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
        // ledger-ea70s: der Schutz von Quick Add gilt jetzt für "Neue Buchung".
        testCase "PwaUpdateWaiting mit eingetipptem Betrag in Neue Buchung zeigt den Hinweis und lädt nicht" <| fun () ->
            let model = baseModel () |> withCapture (fun c -> { c with Form = { c.Form with AmountText = "4,50" } })
            let after, _ = State.update State.PwaUpdateWaiting model
            Expect.isFalse after.Pwa.Reloading "Reloading bleibt false"
            Expect.equal (List.length (noticeToasts after)) 1 "ein Hinweis-Toast"
            Expect.equal (after.Ledger.Capture |> Option.map (fun c -> c.Form.AmountText)) (Some "4,50") "Betrag bleibt"
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
          Trigger = Shared.Domain.Manual
          Counts = Shared.Domain.emptyFetchCounts }
    testList "State.isInputActive" [
        active "Zuweisungen in der Schlange der Budget-Seite" (fun m ->
            let write : Components.Budget.Types.AssignWrite =
                { CategoryId = Guid.NewGuid()
                  Month = "2026-11-01"
                  Assigned = { Amount = 1m; Currency = "EUR" }
                  State = Components.Budget.Types.WriteWaiting }
            { m with Budget = { m.Budget with AssignQueue = [ write ] } })
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
        // ledger-r8h19, ledger-sjbvc: je Fall des laufenden Sammellaufs ein Eintrag.
        active "Kassenbuch: Sammellauf Geprüft" (withLedger (fun l -> { l with BatchRunning = Some ReviewMarkBatchRun }))
        active "Kassenbuch: Sammellauf Kategorie" (withLedger (fun l -> { l with BatchRunning = Some CategoryBatchRun }))
        active "Kassenbuch: Sammellauf Lena" (withLedger (fun l -> { l with BatchRunning = Some LenaBatchRun }))
        active "Einstellungen: EditingSection" (fun m ->
            { m with Settings = { m.Settings with EditingSection = Some Components.Settings.Types.YnabSection } })
        // Neue Buchung (ledger-ea70s): eine angefangene Eingabe schützt vor dem Neuladen wie vorher Quick Add.
        active "Neue Buchung: Betrag ausgefüllt" (withCapture (fun c -> { c with Form = { c.Form with AmountText = "4,50" } }))
        active "Neue Buchung: Payee ausgefüllt" (withCapture (fun c -> { c with Form = { c.Form with Payee = "Bäcker" } }))
        active "Neue Buchung: Memo ausgefüllt" (withCapture (fun c -> { c with Form = { c.Form with Memo = "x" } }))
        active "Neue Buchung: Ziel selbst gewählt" (withCapture (fun c ->
            { c with Form = { c.Form with Target = Some (Shared.Domain.BookOnCategory (Shared.Domain.YnabCategoryId (Guid.NewGuid()))); TargetChosen = true } }))
        active "Neue Buchung: Buchung anlegen läuft" (withCapture (fun c -> { c with Saving = true }))
        testCase "Neue Buchung: ein offenes Formular mit nur der Vorbelegung ist keine Eingabe" <| fun () ->
            Expect.isFalse (State.isInputActive (withCapture id (baseModel ()))) "vorbelegt, sonst leer"
        testCase "Neue Buchung: ein Betrag nur aus Leerzeichen ist keine Eingabe" <| fun () ->
            let model = withCapture (fun c -> { c with Form = { c.Form with AmountText = "  " } }) (baseModel ())
            Expect.isFalse (State.isInputActive model) "leer"
        active "Abrufen: das Sheet ist offen" (fun m -> { m with Fetch = { m.Fetch with IsOpen = true } })
        active "Abrufen: ein Start läuft" (fun m -> { m with Fetch = { m.Fetch with Phase = Components.Fetch.Types.FetchStarting None } })
        active "Abrufen: TAN-Bestätigung läuft" (fun m -> { m with Fetch = { m.Fetch with Phase = Components.Fetch.Types.FetchConfirmingTan } })
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
            { m with Rules = { m.Rules with Panel = Some { Editor = Components.Rules.Types.Editing rule; Draft = Components.Rules.Types.RuleDraft.fromRule rule; Save = Components.Rules.Types.NotSaving; Test = Components.Rules.Types.NothingToTest; PickerOpen = false } } })
        testCase "Abrufen: ein geschlossenes Sheet nach beendetem Abruf ist keine Eingabe" <| fun () ->
            let m = baseModel ()
            let m' = { m with Fetch = { m.Fetch with Session = Success (Some (session Shared.Domain.Completed)) } }
            Expect.isFalse (State.isInputActive m') "Completed"
    ]
