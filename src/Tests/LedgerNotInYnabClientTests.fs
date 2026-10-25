module LedgerNotInYnabClientTests

// ledger-cks06: "Noch nicht in YNAB" vollständig. Kennzeichen in der Liste (Rangfolge),
// "Erneut senden" in der Hinweisleiste (Reducer, Anwendung des Ergebnisses) und die
// Quelltext-Zusicherung zur Liste. Die gerenderte Fassung prüft
// `scripts/ledger-not-in-ynab.test.mjs`.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Components.Ledger.View

let private entry (n: int) (linked: bool) : LedgerEntryView =
    { LedgerSelectionTests.entryOf n (-10m * decimal n) false false with LinkedToYnab = linked }

let private modelWith (loaded: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success loaded
        NextPage = 1
        TotalCount = List.length loaded
        HasMoreEntries = false }

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = Components.Ledger.State.update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private entryOf (model: Model) (id: Guid) =
    match model.Entries with
    | Success entries -> entries |> List.find (fun e -> e.Id = id)
    | _ -> failtest "Liste nicht geladen"

let private failedAt = DateTime(2026, 10, 25, 9, 0, 0, DateTimeKind.Utc)
let private later = DateTime(2026, 10, 25, 11, 30, 0, DateTimeKind.Utc)

[<Tests>]
let statusMarkTests =
    testList "Noch nicht in YNAB: Kennzeichen in der Liste (ledger-cks06)" [
        testCase "eine nicht verknüpfte Buchung trägt das Kennzeichen Nicht in YNAB" <| fun () ->
            Expect.equal (rowStatusMark (entry 1 false)) RowNotInYnab "Kennzeichen"

        testCase "eine verknüpfte Buchung trägt kein Kennzeichen" <| fun () ->
            Expect.equal (rowStatusMark (entry 1 true)) RowOtherStates "ohne Kennzeichen"

        testCase "eine gelöschte Buchung bleibt gelöscht, auch wenn sie nicht verknüpft ist" <| fun () ->
            Expect.equal
                (rowStatusMark { entry 1 false with DeletedInYnab = true })
                RowDeleted
                "gelöscht vor Nicht in YNAB"

        testCase "Handy-Zeile und PC-Statusspalte rendern das Kennzeichen aus rowStatusMark" <| fun () ->
            let root = Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName
            let source = File.ReadAllText(Path.Combine(root, "src", "Client", "Components", "Ledger", "View.fs"))
            Expect.stringContains source "\"Nicht in YNAB\"" "Wortlaut"
            let uses = source.Split("match rowStatusMark entry with").Length - 1
            Expect.equal uses 2 "Handy-Zeile und Statusspalte"
    ]

[<Tests>]
let retryPushTests =
    testList "Noch nicht in YNAB: Erneut senden (ledger-cks06)" [
        testCase "Erneut senden setzt den Sende-Zustand und ruft den Push dieser Buchung" <| fun () ->
            let e = entry 1 false
            let model, sent, _ = run (StartRetryPush e.Id) (modelWith [ e; entry 2 false ])
            Expect.equal model.PushRetryRows.[e.Id] PushSending "sendet"
            Expect.equal sent [ RunRetryPush e.Id ] "Befehl"
            Expect.isFalse (model.PushRetryRows.ContainsKey (entry 2 false).Id) "nur diese Zeile"

        testCase "Erfolg verknüpft die Buchung in Liste und Detail, die Leiste verschwindet" <| fun () ->
            let e = { entry 1 false with PushToYnabFailure = Some { Reason = "Timeout"; FailedAt = failedAt } }
            let start = { modelWith [ e ] with DetailFor = Some e }
            let sending, _, _ = run (StartRetryPush e.Id) start
            let model, _, external = run (PushRetried (e.Id, Ok PushToYnabSent)) sending
            let updated = entryOf model e.Id
            Expect.isTrue updated.LinkedToYnab "verknüpft"
            Expect.isNone updated.PushToYnabFailure "Fehlschlag gelöscht"
            Expect.isEmpty model.PushRetryRows "Sende-Zustand weg"
            Expect.isNone (detailNoticeFor None NotAsked updated) "keine Leiste mehr"
            Expect.equal (rowStatusMark updated) RowOtherStates "kein Kennzeichen mehr"
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.LinkedToYnab)) (Some true) "Detail folgt"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text "Buchung an YNAB gesendet" "Toast"
            | other -> failtestf "Erwartet Erfolgs-Toast, war %A" other

        testCase "Fehlschlag zeigt den neuen Grund in der Leiste" <| fun () ->
            let e = { entry 1 false with PushToYnabFailure = Some { Reason = "alter Grund"; FailedAt = failedAt } }
            let sending, _, _ = run (StartRetryPush e.Id) (modelWith [ e ])
            let applied =
                Components.Ledger.State.applyPushRetryResult e.Id later (PushToYnabFailed "Netzwerkfehler: Timeout") sending
            let updated = entryOf applied e.Id
            Expect.isFalse updated.LinkedToYnab "weiter nicht verknüpft"
            Expect.equal updated.PushToYnabFailure (Some { Reason = "Netzwerkfehler: Timeout"; FailedAt = later }) "neuer Grund und Zeitpunkt"
            Expect.isEmpty applied.PushRetryRows "Sende-Zustand weg"
            match detailNoticeFor None NotAsked updated with
            | Some (NotInYnabNotice reason) ->
                Expect.stringContains reason "Netzwerkfehler: Timeout" "neuer Grund"
                Expect.isFalse (reason.Contains "alter Grund") "alter Grund ersetzt"
            | other -> failtestf "Erwartet NotInYnabNotice, war %A" other

        testCase "der Reducer meldet den Fehlschlag aus der Antwort als Toast" <| fun () ->
            let e = entry 1 false
            let _, _, external = run (PushRetried (e.Id, Ok (PushToYnabFailed "Timeout"))) (modelWith [ e ])
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "Timeout" "Grund im Toast"
            | other -> failtestf "Erwartet Fehler-Toast, war %A" other

        testCase "ein abgelehnter Befehl setzt den Sende-Zustand zurück und meldet den Grund" <| fun () ->
            let e = entry 1 false
            let sending, _, _ = run (StartRetryPush e.Id) (modelWith [ e ])
            let model, _, external = run (PushRetried (e.Id, Error "Die Buchung ist schon in YNAB")) sending
            Expect.isEmpty model.PushRetryRows "Sende-Zustand weg"
            match external with
            | ShowToast (text, ToastError) -> Expect.stringContains text "schon in YNAB" "Grund"
            | other -> failtestf "Erwartet Fehler-Toast, war %A" other

        testCase "der Satz der Leiste verspricht keinen Versuch beim nächsten Lauf" <| fun () ->
            // Der Spiegel-Lauf pusht nichts (LedgerRetryPushTests), der Satz darf es nicht behaupten.
            let failure : LedgerPushFailure = { Reason = "Timeout"; FailedAt = failedAt }
            Expect.isFalse ((notInYnabNoticeReason (Some failure)).Contains "nächsten Lauf") "mit Fehlschlag"
            Expect.isFalse ((notInYnabNoticeReason None).Contains "nächsten Lauf") "ohne Fehlschlag"
    ]
