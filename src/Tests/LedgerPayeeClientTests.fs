module LedgerPayeeClientTests

// ledger-fxt09: Payee ändern im Kassenbuch-Detail, Client-Seite. Die reinen Bausteine (Auswahl,
// Sperre, Hinweis, Schritt beim Übernehmen) und der Reducer laufen ohne Proxy: `Start...` setzt den
// Zustand, der Aufruf steckt hinter `Run...` (ADR 0020). Fokus und Tastatur im echten DOM prüft
// `scripts/ledger-payee.test.mjs`.

open System
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f709-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e709-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -23.40m
      Payee = Some "REWE SAGT DANKE 4711"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some (Guid.Parse "0195d709-0000-7000-8000-000000000001")
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

let private rowOf (n: int) (model: Model) =
    match model.Entries with
    | Success entries -> entries |> List.find (fun e -> e.Id = idOf n)
    | _ -> failtest "Entries sollte geladen sein"

let private outcomeTo (previous: string option) (current: string option) (writeBack: PayeeWriteBack) : PayeeOutcome =
    { PreviousPayee = previous; CurrentPayee = current; PayeeChange = PayeeChanged; PayeeWriteBack = writeBack }

let private outcome (previous: string option) (current: string) (writeBack: PayeeWriteBack) : PayeeOutcome =
    outcomeTo previous (Some current) writeBack

/// Detail offen, Auswahl offen, Payees geladen.
let private opened () : Model =
    let model, _, _ = run (OpenPayeeEdit (idOf 1)) (modelWith [ entryOf 1 ])
    { model with PayeeChoices = Success [ { Payee = "Rewe"; Count = 12 }; { Payee = "REWE SAGT DANKE 4711"; Count = 3 } ] }

// ============================================
// Auswahl (DS-Baustein, rein)
// ============================================

[<Tests>]
let pickerTests =
    let suggestions = [ "Rewe", 12; "Rewe City", 30; "Edeka", 5 ]
    testList "Payee-Auswahl: Zeilen und Tasten" [

        testCase "eine neue Eingabe steht als erste Zeile „übernehmen“, darunter die passenden Payees, häufigste zuerst" <| fun () ->
            Expect.equal
                (PayeePicker.options suggestions " rew ")
                [ PayeePicker.TakeTyped "rew"; PayeePicker.Suggested ("Rewe City", 30); PayeePicker.Suggested ("Rewe", 12) ]
                "übernehmen, dann Treffer"
            Expect.equal (PayeePicker.takeLabel "rew") "„rew“ übernehmen" "Beschriftung"

        testCase "ist die Eingabe ein vorhandener Payee (auch in anderer Schreibweise), gibt es keine Übernehmen-Zeile" <| fun () ->
            Expect.equal
                (PayeePicker.options suggestions "rewe")
                [ PayeePicker.Suggested ("Rewe City", 30); PayeePicker.Suggested ("Rewe", 12) ]
                "nur Treffer"

        testCase "leere Eingabe zeigt alle Payees ohne Übernehmen-Zeile, höchstens maxShown" <| fun () ->
            Expect.equal (PayeePicker.options suggestions "  " |> List.length) 3 "alle"
            let many = [ for i in 1 .. 80 -> $"Payee {i}", i ]
            let shown = PayeePicker.options many ""
            Expect.equal shown.Length PayeePicker.maxShown "begrenzt"
            Expect.equal (List.head shown) (PayeePicker.Suggested ("Payee 80", 80)) "häufigster zuerst"

        testCase "Tasten: ↑↓ bewegen, Enter übernimmt die Markierung, sonst die erste Zeile, ohne Zeile die Eingabe; Esc schließt" <| fun () ->
            let rows = PayeePicker.options suggestions "rew"
            Expect.equal (PayeePicker.keyAction "ArrowDown" rows None "rew") (PayeePicker.MoveTo 0) "erste"
            Expect.equal (PayeePicker.keyAction "ArrowDown" rows (Some 2) "rew") (PayeePicker.MoveTo 2) "bleibt unten"
            Expect.equal (PayeePicker.keyAction "ArrowUp" rows None "rew") (PayeePicker.MoveTo 2) "letzte"
            Expect.equal (PayeePicker.keyAction "Enter" rows (Some 1) "rew") (PayeePicker.Pick (Some "Rewe City")) "markiert"
            Expect.equal (PayeePicker.keyAction "Enter" rows None "rew") (PayeePicker.Pick (Some "rew")) "erste Zeile übernimmt die Eingabe"
            Expect.equal (PayeePicker.keyAction "Enter" [] None "") (PayeePicker.Pick (Some "")) "leer geht an den Aufrufer"
            Expect.equal (PayeePicker.keyAction "Escape" rows None "rew") PayeePicker.CloseOnly "Esc"
            Expect.equal (PayeePicker.keyAction "a" rows None "rew") PayeePicker.NoAction "Tippen"

        // ledger-pkrwd: „Payee entfernen“ unter den Vorschlägen, nur wenn die Buchung einen Payee hat.
        testCase "„Payee entfernen“ steht unter den Vorschlägen, nur wenn die Buchung einen Payee hat" <| fun () ->
            Expect.equal
                (PayeePicker.pickerRows suggestions "rewe" (Some "Rewe"))
                [ PayeePicker.Suggested ("Rewe City", 30); PayeePicker.Suggested ("Rewe", 12); PayeePicker.RemovePayee ]
                "als letzte Zeile"
            Expect.equal
                (PayeePicker.pickerRows suggestions "Zahnarzt" (Some "Rewe"))
                [ PayeePicker.TakeTyped "Zahnarzt"; PayeePicker.RemovePayee ]
                "auch bei einer neuen Eingabe"
            Expect.equal (PayeePicker.pickerRows suggestions "rewe" None) (PayeePicker.options suggestions "rewe") "ohne Payee fehlt die Zeile"
            Expect.equal (PayeePicker.pickerRows suggestions "rewe" (Some "  ")) (PayeePicker.options suggestions "rewe") "Leerraum zählt als kein Payee"
            Expect.equal (PayeePicker.payeeOf PayeePicker.RemovePayee) None "das Ziel ist ohne Payee"

        testCase "Tasten: „Payee entfernen“ ist per ↑↓ erreichbar und markiert mit Enter wählbar, nie als erste Zeile" <| fun () ->
            let rows = PayeePicker.pickerRows suggestions "rewe" (Some "Rewe")
            Expect.equal (PayeePicker.keyAction "ArrowUp" rows None "rewe") (PayeePicker.MoveTo 2) "von oben her die letzte Zeile"
            Expect.equal (PayeePicker.keyAction "ArrowDown" rows (Some 1) "rewe") (PayeePicker.MoveTo 2) "erreichbar"
            Expect.equal (PayeePicker.keyAction "Enter" rows (Some 2) "rewe") (PayeePicker.Pick None) "markiert: entfernen"
            let onlyRemove = PayeePicker.pickerRows [] "  " (Some "Rewe")
            Expect.equal onlyRemove [ PayeePicker.RemovePayee ] "nur die Zeile"
            Expect.equal (PayeePicker.keyAction "Enter" onlyRemove None "  ") (PayeePicker.Pick (Some "  ")) "ohne Markierung bleibt ein leerer Text ein Feldfehler"

        // ledger-dn4vy: der Payee-Filter sucht unter den Treffern; „übernehmen“ fände dort nichts.
        testCase "Payee-Filter: Vorschläge ohne Übernehmen-Zeile, sonst wie die Auswahl" <| fun () ->
            Expect.equal
                (PayeePicker.suggestedOptions suggestions "rew")
                [ PayeePicker.Suggested ("Rewe City", 30); PayeePicker.Suggested ("Rewe", 12) ]
                "nur vorhandene Payees"
            Expect.isEmpty (PayeePicker.suggestedOptions suggestions "Zahnarzt") "kein Treffer, keine Zeile"

    ]

// ============================================
// Reine Bausteine des Kassenbuchs
// ============================================

[<Tests>]
let buildingBlockTests =
    testList "Payee ändern: Sperre, Schritt und Hinweis" [

        testCase "Übernehmen: leer und zu lang abgelehnt, gleich bis auf Schreibweise unverändert, sonst getrimmt gesendet" <| fun () ->
            let entry = entryOf 1
            Expect.equal (payeeSaveStep (Some "  ") entry) (PayeeSaveRejected payeeEmptyReason) "leer"
            Expect.equal (payeeSaveStep (Some (String('x', ynabPayeeMaxLength + 1))) entry) (PayeeSaveRejected payeeTooLongReason) "zu lang"
            Expect.equal (payeeSaveStep (Some " rewe sagt danke 4711 ") entry) PayeeSaveUnchanged "gleich"
            Expect.equal (payeeSaveStep (Some "  Rewe ") entry) (PayeeSaveSends (Some "Rewe")) "getrimmt"

        // ledger-pkrwd: `None` heißt „Payee entfernen“.
        testCase "Payee entfernen: mit Payee gesendet, ohne Payee unverändert" <| fun () ->
            Expect.equal (payeeSaveStep None (entryOf 1)) (PayeeSaveSends None) "gesendet"
            Expect.equal (payeeSaveStep None { entryOf 1 with Payee = None }) PayeeSaveUnchanged "schon ohne Payee"

        testCase "Liste ohne Payee: der Platzhalter steht gedämpft an der Stelle des Payees" <| fun () ->
            Expect.isTrue (Components.Ledger.View.payeeMissing { entryOf 1 with Payee = None }) "ohne Payee"
            Expect.isTrue (Components.Ledger.View.payeeMissing { entryOf 1 with Payee = Some "  " }) "Leerraum"
            Expect.isFalse (Components.Ledger.View.payeeMissing (entryOf 1)) "mit Payee"
            Expect.equal (Components.Ledger.View.payeeLabel { entryOf 1 with Payee = None }) "ohne Payee" "Platzhalter, keine Lücke"
            // Review ledger-pkrwd: bei nur Leerraum dämpfte die Liste die Zeile, zeigte aber den leeren Text.
            Expect.equal (Components.Ledger.View.payeeLabel { entryOf 1 with Payee = Some "  " }) "ohne Payee" "Leerraum zeigt den Platzhalter"
            Expect.isNone (Components.Ledger.View.payeeFilterLabel { entryOf 1 with Payee = None }) "kein „alle zeigen“"

        testCase "Sperre: gelöscht, nicht verknüpft und Übertrag; abgeglichen bleibt änderbar" <| fun () ->
            Expect.equal (payeeLockFor { entryOf 1 with DeletedInYnab = true }) (Some payeeDeletedReason) "gelöscht"
            Expect.equal (payeeLockFor { entryOf 1 with LinkedToYnab = false }) (Some payeeNotInYnabReason) "nicht verknüpft"
            Expect.equal (payeeLockFor { entryOf 1 with IsTransfer = true }) (Some payeeTransferReason) "Übertrag"
            Expect.isNone (payeeLockFor { entryOf 1 with Reconciled = true }) "abgeglichen"

        testCase "Hinweis „Payee nicht in YNAB“ mit Grund und Erneut senden, als eigene Leiste nach dem Betrag" <| fun () ->
            let failing = { entryOf 1 with PayeeWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            let notice = Components.Ledger.View.detailNoticeWithPayee None None None None None None NotAsked failing |> Option.get
            match notice with
            | Components.Ledger.View.PayeeNotice (hint, false) ->
                Expect.equal (hint.Title, hint.Reason, hint.RetryLabel) ("Payee nicht in YNAB", "Keine Verbindung zu YNAB", Some "Erneut senden") "Leiste"
            | other -> failtest $"erwartet PayeeNotice, war {other}"
            Expect.equal (Components.Ledger.View.detailNoticeActionMsg failing.Id notice) (Some (StartRetryPayee failing.Id)) "Erneut senden"
            match Components.Ledger.View.detailNoticeWithPayee None None None None None None NotAsked { failing with AmountWriteBackFailure = Some "x" } with
            | Some (Components.Ledger.View.AmountNotice _) -> ()
            | other -> failtest $"der Betrag hat Vorrang, war {other}"
            Expect.isNone (Components.Ledger.View.detailNoticeWithPayee None None None None None None NotAsked (entryOf 1)) "ohne Fehlschlag keine Leiste"

        testCase "Toast Rückgängig wird zur Nachricht StartUndoPayee" <| fun () ->
            match State.toastActionMsg (UndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) with
            | State.LedgerMsg (StartUndoPayee (id, previous)) -> Expect.equal (id, previous) (idOf 1, Some "REWE SAGT DANKE 4711") "Übersetzung"
            | other -> failtest $"erwartet StartUndoPayee, war {other}"

        testCase "die Payee-Zeile öffnet die Auswahl nur, wenn der Payee änderbar ist" <| fun () ->
            let model = modelWith [ entryOf 1 ]
            Expect.equal (Components.Ledger.View.payeeEditMsg model (entryOf 1)) (Some (OpenPayeeEdit (idOf 1))) "änderbar"
            Expect.isNone (Components.Ledger.View.payeeEditMsg model { entryOf 1 with IsTransfer = true }) "Übertrag"
            let sending = { model with PayeeRows = Map.ofList [ idOf 1, PayeeSending ] }
            Expect.isNone (Components.Ledger.View.payeeEditMsg sending (entryOf 1)) "Aufruf läuft"
    ]

// ============================================
// Reducer
// ============================================

[<Tests>]
let reducerTests =
    testList "Payee ändern: Reducer" [

        testCase "Öffnen setzt die Auswahl für die Buchung und lädt die Payees des Kassenbuchs einmal" <| fun () ->
            let model, msgs, _ = run (OpenPayeeEdit (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.equal model.PayeeEdit (Some { PayeeEntryId = idOf 1; PayeeFieldError = None }) "offen"
            Expect.equal model.PayeeChoices Loading "lädt"
            Expect.equal msgs [ RunLoadPayeeChoices ] "Abruf"
            let loaded = { model with PayeeEdit = None; PayeeChoices = Success [] }
            let _, again, _ = run (OpenPayeeEdit (idOf 1)) loaded
            Expect.isEmpty again "geladen: kein zweiter Abruf"

        testCase "eine gesperrte Buchung öffnet keine Auswahl" <| fun () ->
            let locked = { entryOf 1 with LinkedToYnab = false }
            let model, msgs, _ = run (OpenPayeeEdit locked.Id) (modelWith [ locked ])
            Expect.isNone model.PayeeEdit "zu"
            Expect.isEmpty msgs "kein Abruf"

        testCase "Schließen kehrt ins Detail zurück, der Fokus geht auf die Payee-Zeile; Esc schließt zuerst die Auswahl" <| fun () ->
            let model, msgs, _ = run CancelPayeeEdit (opened ())
            Expect.isNone model.PayeeEdit "zu"
            Expect.isSome model.DetailFor "Detail bleibt"
            Expect.equal msgs [ RestorePayeeRowFocus ] "Fokus zurück"
            let escaped, _, _ = run EscapePressed (opened ())
            Expect.isNone escaped.PayeeEdit "Esc schließt die Auswahl"
            Expect.isSome escaped.DetailFor "das Detail bleibt"

        testCase "ein leerer oder zu langer Payee wird zum Feldfehler, ohne Aufruf; Tippen nimmt ihn zurück" <| fun () ->
            let rejected, msgs, external = run (StartSavePayee (idOf 1, Some "  ")) (opened ())
            Expect.equal (rejected.PayeeEdit |> Option.bind (fun e -> e.PayeeFieldError)) (Some payeeEmptyReason) "Feldfehler"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.equal external NoOp "kein Toast"
            let tooLong, _, _ = run (StartSavePayee (idOf 1, Some (String('x', ynabPayeeMaxLength + 1)))) (opened ())
            Expect.equal (tooLong.PayeeEdit |> Option.bind (fun e -> e.PayeeFieldError)) (Some payeeTooLongReason) "zu lang"
            let typed, _, _ = run PayeeSearchEdited rejected
            Expect.equal (typed.PayeeEdit |> Option.bind (fun e -> e.PayeeFieldError)) None "zurückgenommen"

        testCase "derselbe Payee schließt die Auswahl ohne Aufruf" <| fun () ->
            let model, msgs, _ = run (StartSavePayee (idOf 1, Some "rewe sagt danke 4711")) (opened ())
            Expect.isNone model.PayeeEdit "zu"
            Expect.equal msgs [ RestorePayeeRowFocus ] "kein Aufruf"

        testCase "ein neuer Payee sperrt die Zeile und stößt den Befehl mit dem getrimmten Payee an; ein zweiter Klick schickt nichts" <| fun () ->
            let model, msgs, _ = run (StartSavePayee (idOf 1, Some " Rewe ")) (opened ())
            Expect.equal (model.PayeeRows |> Map.tryFind (idOf 1)) (Some PayeeSending) "läuft"
            Expect.equal msgs [ RunSetPayee (idOf 1, Some "Rewe") ] "Aufruf"
            Expect.isSome model.PayeeEdit "die Auswahl bleibt offen, bis die Antwort kommt"
            let _, again, _ = run (StartSavePayee (idOf 1, Some "Edeka")) model
            Expect.isEmpty again "gesperrt"

        testCase "PayeeSet Ok: Payee in Zeile und Detail, Auswahl zu, „✓ Geändert“ mit Rückgängig unter „Payee“, Historie neu" <| fun () ->
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) (opened ())
            let model, msgs, external = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" PayeeSent))) sending
            Expect.equal (rowOf 1 model).Payee (Some "Rewe") "Zeile"
            Expect.equal (model.DetailFor |> Option.bind (fun d -> d.Payee)) (Some "Rewe") "Detail zieht mit"
            Expect.isNone model.PayeeEdit "zu"
            Expect.isEmpty model.PayeeRows "Sperre gelöst"
            Expect.equal model.PayeeChoices NotAsked "die Payees laden beim nächsten Öffnen neu"
            Expect.equal (msgs |> List.take 2) [ RestorePayeeRowFocus; LoadHistory (idOf 1) ] "Fokus und Historie"
            Expect.equal external NoOp "kein Toast (ledger-enjex)"
            Expect.equal
                (model.DetailNote |> Option.map (fun n -> n.Field, n.Tone, n.Text, n.Undo))
                (Some (AtPayee, FeedbackOk, "Geändert", Some (UndoPayee (idOf 1, Some "REWE SAGT DANKE 4711"))))
                "Rückgängig kennt den alten Payee"
            Expect.equal (model.DetailNote |> Option.map (fun n -> n.BarAction, n.BarDetail)) (Some ("Payee geändert", Some "Rewe")) "Leiste nach dem Schließen"

        // ledger-pkrwd: Payee entfernen über dieselbe Nachricht, „✓ Entfernt“ mit Rückgängig (ledger-enjex).
        testCase "Payee entfernen stößt den Befehl ohne Payee an; die Antwort leert Zeile und Detail, „✓ Entfernt“ mit Rückgängig auf den alten Payee" <| fun () ->
            let sending, msgs, _ = run (StartSavePayee (idOf 1, None)) (opened ())
            Expect.equal msgs [ RunSetPayee (idOf 1, None) ] "Aufruf ohne Payee"
            let model, _, external = run (PayeeSet (idOf 1, Ok (outcomeTo (Some "REWE SAGT DANKE 4711") None PayeeSent))) sending
            Expect.equal (rowOf 1 model).Payee None "Zeile ohne Payee"
            Expect.equal (model.DetailFor |> Option.bind (fun d -> d.Payee)) None "Detail zieht mit"
            Expect.isNone model.PayeeEdit "zu"
            Expect.equal external NoOp "kein Toast"
            Expect.equal
                (model.DetailNote |> Option.map (fun n -> n.Text, n.Undo, n.BarAction, n.BarDetail))
                (Some ("Entfernt", Some (UndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")), "Payee entfernt", None))
                "Rückgängig setzt den alten Payee"
            let undo, undoMsgs, _ = run (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) model
            Expect.equal undoMsgs [ RunUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711") ] "derselbe Befehl mit dem alten Payee"
            let restored, _, _ = run (PayeeRestored (idOf 1, Ok (outcome None "REWE SAGT DANKE 4711" PayeeSent))) undo
            Expect.equal (rowOf 1 restored).Payee (Some "REWE SAGT DANKE 4711") "wieder da"

        testCase "Payee entfernen bei einer Buchung ohne Payee schließt die Auswahl ohne Aufruf" <| fun () ->
            let model = { opened () with Entries = Success [ { entryOf 1 with Payee = None } ]; DetailFor = Some { entryOf 1 with Payee = None } }
            let after, msgs, _ = run (StartSavePayee (idOf 1, None)) model
            Expect.isNone after.PayeeEdit "zu"
            Expect.equal msgs [ RestorePayeeRowFocus ] "kein Aufruf"

        testCase "Payee entfernen mit gescheitertem Versand: Fehlschlag an der Buchung, gelb unter „Payee“, die Buchung bleibt ohne Payee" <| fun () ->
            let model, _, external = run (PayeeSet (idOf 1, Ok (outcomeTo (Some "REWE SAGT DANKE 4711") None (PayeeSendFailed "YNAB weg")))) (opened ())
            Expect.equal (rowOf 1 model).PayeeWriteBackFailure (Some "YNAB weg") "Grund"
            Expect.equal (rowOf 1 model).Payee None "ohne Payee"
            Expect.equal external NoOp "kein Toast"
            Expect.equal
                (model.DetailNote |> Option.map (fun n -> n.Tone, n.Text, n.Undo.IsSome))
                (Some (FeedbackWarning, "Entfernt, nicht in YNAB: YNAB weg", true))
                "gelb unter „Payee“"

        testCase "PayeeSet mit gescheitertem Versand: Fehlschlag an der Buchung, gelb unter „Payee“, der Payee bleibt in BB" <| fun () ->
            let model, _, external = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" (PayeeSendFailed "YNAB weg")))) (opened ())
            Expect.equal (rowOf 1 model).PayeeWriteBackFailure (Some "YNAB weg") "Grund"
            Expect.equal (rowOf 1 model).Payee (Some "Rewe") "Payee bleibt in BB"
            Expect.equal external NoOp "kein Toast"
            Expect.equal
                (model.DetailNote |> Option.map (fun n -> n.Tone, n.Text))
                (Some (FeedbackWarning, "Geändert, nicht in YNAB: YNAB weg"))
                "gelb unter „Payee“"

        // Review ledger-pkrwd: Bekam eine Buchung ohne Payee einen Payee, fehlte im Toast „Rückgängig“, obwohl
        // `setEntryPayee` seit ledger-pkrwd auch `None` nimmt (§7.13).
        testCase "ohne alten Payee nimmt Rückgängig den Payee wieder weg" <| fun () ->
            let model, _, _ = run (PayeeSet (idOf 1, Ok (outcome None "Rewe" PayeeSent))) (opened ())
            Expect.equal
                (model.DetailNote |> Option.bind (fun n -> n.Undo))
                (Some (UndoPayee (idOf 1, None)))
                "Rückgängig ohne alten Payee"
            let _, _, closedExternal = run (PayeeSet (idOf 1, Ok (outcome None "Rewe" PayeeSent))) { opened () with DetailFor = None }
            Expect.equal
                closedExternal
                (PostFeedback
                    { Tone = FeedbackOk
                      Action = "Payee geändert"
                      Detail = Some "Rewe"
                      Act = Some { Label = "Rückgängig"; Kind = UndoPayee (idOf 1, None) } })
                "Detail zu: in der Leiste, Rückgängig ohne alten Payee"
            let changed = { entryOf 1 with Payee = Some "Rewe" }
            let _, msgs, _ = run (StartUndoPayee (idOf 1, None)) (modelWith [ changed ])
            Expect.equal msgs [ RunUndoPayee (idOf 1, None) ] "derselbe Befehl ohne Payee"

        testCase "PayeeSet Error: der Grund steht als Feldfehler unter dem Suchfeld, die Auswahl bleibt offen" <| fun () ->
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) (opened ())
            let model, _, external = run (PayeeSet (idOf 1, Error payeeTransferReason)) sending
            Expect.equal (model.PayeeEdit |> Option.bind (fun e -> e.PayeeFieldError)) (Some payeeTransferReason) "Feldfehler"
            Expect.isEmpty model.PayeeRows "Sperre gelöst"
            Expect.equal external NoOp "kein Toast"

        testCase "Rückgängig schickt den alten Payee über denselben Befehl; im offenen Detail kein Rücknahme-Toast" <| fun () ->
            let changed = { entryOf 1 with Payee = Some "Rewe" }
            let model, msgs, _ = run (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) (modelWith [ changed ])
            Expect.equal (model.PayeeRows |> Map.tryFind (idOf 1)) (Some PayeeSending) "läuft"
            Expect.equal msgs [ RunUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711") ] "Aufruf mit dem alten Payee"
            let _, twice, _ = run (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) model
            Expect.isEmpty twice "ein zweiter Tipp schickt nichts"
            let restored, _, external = run (PayeeRestored (idOf 1, Ok (outcome (Some "Rewe") "REWE SAGT DANKE 4711" PayeeSent))) model
            Expect.equal (rowOf 1 restored).Payee (Some "REWE SAGT DANKE 4711") "alter Payee"
            Expect.equal external NoOp "kein Toast (ledger-enjex)"
            let _, _, closedExternal =
                run (PayeeRestored (idOf 1, Ok (outcome (Some "Rewe") "REWE SAGT DANKE 4711" PayeeSent))) { model with DetailFor = None }
            Expect.equal closedExternal (ShowToast (payeeRestoredToastText, ToastSuccess)) "Detail zu: Toast ohne Rückgängig"

        testCase "Erneut senden: StartRetryPayee sperrt; PayeeRetried löst den Fehlschlag oder ersetzt den Grund" <| fun () ->
            let failing = { entryOf 1 with PayeeWriteBackFailure = Some "weg" }
            let model, msgs, _ = run (StartRetryPayee (idOf 1)) (modelWith [ failing ])
            Expect.equal msgs [ RunRetryPayee (idOf 1) ] "Aufruf"
            let sent, _, external = run (PayeeRetried (idOf 1, Ok PayeeSent)) model
            Expect.isNone (rowOf 1 sent).PayeeWriteBackFailure "Fehlschlag beendet"
            Expect.equal external NoOp "kein Toast bei Erfolg"
            let again, _, warn = run (PayeeRetried (idOf 1, Ok (PayeeSendFailed "immer noch"))) model
            Expect.equal (rowOf 1 again).PayeeWriteBackFailure (Some "immer noch") "neuer Grund"
            Expect.equal warn NoOp "im offenen Detail zeigt die Hinweisleiste den Grund (ledger-enjex)"
            let _, _, closedWarn = run (PayeeRetried (idOf 1, Ok (PayeeSendFailed "immer noch"))) { model with DetailFor = None }
            match closedWarn with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtest $"Detail zu: erwartet Warn-Toast, war {other}"
            let failed, _, _ = run (PayeeRetried (idOf 1, Error "Netz")) model
            match Components.Ledger.View.detailNoticeWithPayee None None None None None (failed.PayeeRows |> Map.tryFind (idOf 1)) NotAsked (rowOf 1 failed) with
            | Some (Components.Ledger.View.PayeeNotice (hint, _)) -> Expect.equal hint.Reason "Netz" "der Befehlsfehler steht in der Leiste"
            | other -> failtest $"{other}"

        testCase "ein anderes Detail oder Schließen verwirft die Auswahl" <| fun () ->
            let model, _, _ = run CloseDetail (opened ())
            Expect.isNone model.PayeeEdit "mit dem Detail zu"

        // Review-Fund zu ledger-fxt09: Schloss das Detail während des Speicherns, ging ein Fehler von
        // setEntryPayee still verloren (kein Feldfehler mehr da, kein Toast).
        testCase "PayeeSet Error bei geschlossenem Detail: die Leiste nennt den Grund rot; bei geschlossener Auswahl rot unter „Payee“" <| fun () ->
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) (opened ())
            let closed, _, _ = run CloseDetail sending
            let model, _, external = run (PayeeSet (idOf 1, Error "YNAB weg")) closed
            Expect.equal
                external
                (PostFeedback { Tone = FeedbackError; Action = "Payee nicht geändert"; Detail = Some "YNAB weg"; Act = None })
                "Leiste mit Grund (am PC der Toast)"
            Expect.isEmpty model.PayeeRows "Sperre gelöst"
            Expect.equal (rowOf 1 model).Payee (Some "REWE SAGT DANKE 4711") "Payee unverändert"
            let pickerClosed, _, pickerExternal = run (PayeeSet (idOf 1, Error "YNAB weg")) { sending with PayeeEdit = None }
            Expect.equal pickerExternal NoOp "Detail offen: kein Toast"
            Expect.equal
                (pickerClosed.DetailNote |> Option.map (fun n -> n.Field, n.Tone, n.Text))
                (Some (AtPayee, FeedbackError, "Nicht geändert: YNAB weg"))
                "rot unter „Payee“"

        // Review-Fund zu ledger-fxt09: Die Ladeantwort der Auswahl war ungebunden. Kam sie nach einer
        // Änderung an, stand wieder die alte Payee-Liste da.
        testCase "eine alte Ladeantwort der Payee-Auswahl ersetzt nach einer Änderung die Liste nicht" <| fun () ->
            let opening, _, _ = run (OpenPayeeEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let oldAnswer = Components.Ledger.State.payeeChoicesAnswer opening
            let oldList = [ { Payee = "REWE SAGT DANKE 4711"; Count = 3 } ]
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) opening
            let changed, _, _ = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" PayeeSent))) sending
            let late, _, _ = run (oldAnswer (Ok oldList)) changed
            Expect.equal late.PayeeChoices NotAsked "die alte Liste bleibt draußen"
            let reopened, msgs, _ = run (OpenPayeeEdit (idOf 1)) late
            Expect.equal msgs [ RunLoadPayeeChoices ] "lädt neu"
            let lateAgain, _, _ = run (oldAnswer (Ok oldList)) reopened
            Expect.equal lateAgain.PayeeChoices Loading "auch beim neuen Laden nicht"
            let fresh = [ { Payee = "Rewe"; Count = 13 } ]
            let loaded, _, _ = run (Components.Ledger.State.payeeChoicesAnswer reopened (Ok fresh)) lateAgain
            Expect.equal loaded.PayeeChoices (Success fresh) "die neue Antwort gilt"
            let intact, _, _ = run (oldAnswer (Ok oldList)) opening
            Expect.equal intact.PayeeChoices (Success oldList) "ohne Änderung gilt die Antwort"
    ]

[<Tests>]
let payeeDetailNoteTests =
    testList "Payee: Bestätigung am Feld im Detail (ledger-enjex)" [

        testCase "Sheet am Handy und Panel am PC zeigen dasselbe, ohne Toast" <| fun () ->
            let save form =
                let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) { opened () with DetailForm = form }
                let m, _, ext = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" PayeeSent))) sending
                m.DetailNote, ext
            Expect.equal (save DetailSheet) (save DetailPanel) "gleich"
            Expect.equal (snd (save DetailSheet)) NoOp "kein Toast"

        testCase "Rückgängig am Feld ist StartUndoPayee wie früher im Toast; Schließen übergibt „Payee geändert“ an die Leiste" <| fun () ->
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) (opened ())
            let model, _, _ = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" PayeeSent))) sending
            let kind = model.DetailNote.Value.Undo.Value
            Expect.equal (detailNoteUndoMsg kind) (Some (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711"))) "am Feld"
            Expect.equal (State.toastActionMsg kind) (State.LedgerMsg (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711"))) "wie der Toast"
            let _, _, external = run CloseDetail model
            match external with
            | PostFeedback note -> Expect.equal (note.Action, note.Detail, note.Act.IsSome) ("Payee geändert", Some "Rewe", true) "Leiste"
            | other -> failtest $"erwartet Leiste, war {other}"
            let undone, _, _ = run (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) model
            let restored, _, _ = run (PayeeRestored (idOf 1, Ok (outcome (Some "Rewe") "REWE SAGT DANKE 4711" PayeeSent))) undone
            Expect.isNone restored.DetailNote "Bestätigung weg"

        testCase "Rückgängig gescheitert: rot unter „Payee“; mit YNAB-Fehler gelb „Zurückgesetzt, nicht in YNAB“" <| fun () ->
            let changed = modelWith [ { entryOf 1 with Payee = Some "Rewe" } ]
            let undoing, _, _ = run (StartUndoPayee (idOf 1, Some "REWE SAGT DANKE 4711")) changed
            let failed, _, external = run (PayeeRestored (idOf 1, Error "weg")) undoing
            Expect.equal external NoOp "kein Toast"
            Expect.equal (failed.DetailNote |> Option.map (fun n -> n.Field, n.Text)) (Some (AtPayee, "Rückgängig nicht möglich: weg")) "rot"
            let warned, _, _ = run (PayeeRestored (idOf 1, Ok (outcome (Some "Rewe") "REWE SAGT DANKE 4711" (PayeeSendFailed "weg")))) undoing
            Expect.equal (warned.DetailNote |> Option.map (fun n -> n.Tone, n.Text)) (Some (FeedbackWarning, "Zurückgesetzt, nicht in YNAB: weg")) "gelb"

        testCase "ein neues Übernehmen nimmt die stehende Bestätigung weg" <| fun () ->
            let sending, _, _ = run (StartSavePayee (idOf 1, Some "Rewe")) (opened ())
            let model, _, _ = run (PayeeSet (idOf 1, Ok (outcome (Some "REWE SAGT DANKE 4711") "Rewe" PayeeSent))) sending
            let reopened, _, _ = run (OpenPayeeEdit (idOf 1)) model
            let again, _, _ = run (StartSavePayee (idOf 1, Some "Edeka")) reopened
            Expect.isNone again.DetailNote "weg"
    ]

// ============================================
// Sperre über mehrere Nachrichten (Doktrin Punkt 20)
// ============================================

/// Welcher Aufruf zum Payee unterwegs ist, wie die Aufzählung ihn kennt. Der Merker ist eigen: Der
/// Test soll nicht der Sperre glauben, die er prüft.
type private PayeeCall =
    | SaveCall of payee: string option
    | UndoCall of payee: string option
    | RetryCall

/// Der Aufruf, den ein Schritt angestoßen hat, aus den ausgehenden Nachrichten.
let private callOf (msgs: Msg list) : PayeeCall option =
    msgs
    |> List.tryPick (function
        | RunSetPayee (_, payee) -> Some (SaveCall payee)
        | RunUndoPayee (_, payee) -> Some (UndoCall payee)
        | RunRetryPayee _ -> Some RetryCall
        | _ -> None)

/// Alle Ereignisse, die in diesem Zustand kommen können.
let private payeeEvents (model: Model) (pending: PayeeCall option) : (string * Msg) list =
    let current = rowOf 1 model
    let other = if current.Payee = Some "Rewe" then "Edeka" else "Rewe"
    let id = idOf 1
    [ "Öffnen", OpenPayeeEdit id
      "Schließen", CancelPayeeEdit
      "Detail zu", CloseDetail
      "Detail auf", OpenDetail current
      "Übernehmen", StartSavePayee (id, Some other)
      // Review ledger-pkrwd: „Payee entfernen“ läuft über dieselbe Nachricht und dieselbe Sperre.
      "Entfernen", StartSavePayee (id, None)
      "Rückgängig", StartUndoPayee (id, Some "REWE SAGT DANKE 4711")
      "Erneut senden", StartRetryPayee id
      match pending with
      | Some (SaveCall payee) ->
          "Antwort", PayeeSet (id, Ok (outcomeTo current.Payee payee PayeeSent))
          "Fehler", PayeeSet (id, Error "weg")
      | Some (UndoCall payee) ->
          "Antwort", PayeeRestored (id, Ok (outcomeTo current.Payee payee PayeeSent))
          "Fehler", PayeeRestored (id, Error "weg")
      | Some RetryCall ->
          "Antwort", PayeeRetried (id, Ok PayeeSent)
          "Fehler", PayeeRetried (id, Error "weg")
      | None -> () ]

[<Tests>]
let payeeLockTests =
    // Review-Fund zu ledger-fxt09: StartRetryPayee prüfte die Sperre nicht. Ein "Erneut senden"
    // während Speichern oder Rückgängig schickte einen zweiten Aufruf.
    testList "Payee ändern: ein Aufruf zugleich" [
        testCase "über alle Reihenfolgen von Übernehmen, Entfernen, Rückgängig, Erneut senden und Antworten läuft höchstens ein Aufruf, ruhend startet jeder" <| fun () ->
            let start = { opened () with PayeeEdit = None }
            let failing = { rowOf 1 start with PayeeWriteBackFailure = Some "weg" }
            let start = { start with Entries = Success [ failing ]; DetailFor = Some failing }
            let mutable started = 0
            let mutable blocked = 0
            let rec go (left: int) (path: string list) (model: Model) (pending: PayeeCall option) =
                for name, msg in payeeEvents model pending do
                    let after, msgs, external = run msg model
                    let path' = name :: path
                    let shown = String.concat ", " (List.rev path')
                    let call = callOf msgs
                    match pending, call with
                    | Some _, Some _ -> failtest $"zweiter Aufruf: {shown}"
                    | Some _, None -> if name = "Übernehmen" || name = "Entfernen" || name = "Rückgängig" || name = "Erneut senden" then blocked <- blocked + 1
                    | None, Some _ -> started <- started + 1
                    | None, None ->
                        let saveOpen = model.PayeeEdit |> Option.exists (fun e -> e.PayeeEntryId = idOf 1)
                        // Entfernen startet nur, wenn die Buchung einen Payee hat (sonst Ziel gleich Ist).
                        let hasPayee = (rowOf 1 model).Payee.IsSome
                        if name = "Rückgängig" || name = "Erneut senden" || (name = "Übernehmen" && saveOpen)
                           || (name = "Entfernen" && saveOpen && hasPayee) then
                            failtest $"ruhend startet {name} nicht: {shown}"
                    if name = "Fehler" then
                        let fieldError = after.PayeeEdit |> Option.exists (fun e -> e.PayeeFieldError.IsSome)
                        let toast =
                            match external with
                            | ShowToast (_, ToastError) | PostFeedback { Tone = FeedbackError } -> true
                            | _ -> false
                        let notice = (after.PayeeRows |> Map.tryFind (idOf 1)) = Some (PayeeCommandFailed "weg")
                        // ledger-enjex: im offenen Detail steht der Fehler rot unter „Payee“.
                        let atField = after.DetailNote |> Option.exists (fun n -> n.Field = AtPayee && n.Tone = FeedbackError)
                        Expect.isTrue (fieldError || toast || notice || atField) $"Fehler ohne Spur: {shown}"
                    let answered = name = "Antwort" || name = "Fehler"
                    let pending' = if answered then None else Option.orElse call pending
                    if left > 1 then go (left - 1) path' after pending'
            go 6 [] start None
            Expect.isGreaterThan started 1000 "die Aufzählung startet Aufrufe"
            Expect.isGreaterThan blocked 1000 "und versucht Starts während eines Aufrufs"
    ]
