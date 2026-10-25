module LedgerMemoClientTests

// ledger-fa1fp: Memo bearbeiten im Kassenbuch-Detail, Client-Seite (ADR 0062), dazu die vier
// kleineren Punkte des Detail-Feinschliffs (Kategorie am Handy kurz, Fußtext im Picker,
// Styleguide). Der Reducer und die reinen Bausteine der Ansicht laufen ohne Proxy: `Start...`
// setzt den Zustand, der Aufruf steckt hinter `Run...` (ADR 0020). Feliz-Elemente lassen sich
// unter .NET nicht inspizieren; die Verdrahtung der Ansicht prüfen Quelltext-Zusicherungen,
// die Darstellung am echten Element prüft der Playwright-Wächter `scripts/ledger-memo.test.mjs`.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f700-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e700-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -10m
      Payee = Some $"Payee fa1fp {n}"
      Memo = None
      CategoryName = Some "Haushalt"
      CategoryId = Some (Guid.Parse "0195d700-0000-7000-8000-000000000001")
      CategoryGroupName = Some "Alltag"
      SplitCategoryGroupNames = Map.empty
      Splits = []
      Origin = MirroredFromYnab
      DeletedInYnab = false
      NeedsReview = false
      Flag = NoFlag
      IsTransfer = false
      ReviewMarkWriteBackFailure = None
      CategorizationWriteBackFailure = None
      FlagWriteBackFailure = None
      MemoWriteBackFailure = None
      LinkedToYnab = true
      PushToYnabFailure = None; SplitLocks = None }

let private withMemo (n: int) (memo: string) = { entryOf n with Memo = Some memo }
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

let private update = Components.Ledger.State.update

let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private rowOf (n: int) (model: Model) =
    match model.Entries with
    | Success entries -> entries |> List.find (fun e -> e.Id = idOf n)
    | _ -> failtest "Entries sollte geladen sein"

let private outcome (previous: string option) (current: string option) (writeBack: MemoWriteBack) : MemoOutcome =
    { PreviousMemo = previous; CurrentMemo = current; MemoChange = MemoChanged; MemoWriteBack = writeBack }

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private viewSource () = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]

// ============================================
// Reine Bausteine
// ============================================

[<Tests>]
let memoBuildingBlockTests =
    testList "Memo bearbeiten: Aktion, Entwurf und Hinweis (reine Bausteine)" [

        testCase "ohne Memo steht Memo hinzufügen, mit Memo Bearbeiten" <| fun () ->
            Expect.equal (memoActionFor (entryOf 1)).Label "Memo hinzufügen" "ohne Memo"
            Expect.equal (memoActionFor (withMemo 1 "Lampe")).Label "Bearbeiten" "mit Memo"
            Expect.equal (memoActionFor (withMemo 1 "   ")).Label "Memo hinzufügen" "Leerraum ist kein Memo"
            Expect.isNone (memoActionFor (entryOf 1)).DisabledReason "aktiv"

        testCase "in YNAB gelöschte Buchungen: die Aktion ist gesperrt und nennt den Grund" <| fun () ->
            let deleted = { entryOf 1 with DeletedInYnab = true }
            Expect.equal (memoActionFor deleted).DisabledReason (Some memoDeletedReason) "gelöscht"

        testCase "der Entwurf: zu lang ist ein Problem, gleiches Memo ist keine Änderung" <| fun () ->
            Expect.isNone (memoDraftProblem "kurz") "kurz geht"
            Expect.equal (memoDraftProblem (String('a', ynabMemoMaxLength + 1))) (Some memoTooLongReason) "zu lang"
            Expect.isFalse (memoDraftChangesEntry " Lampe " (withMemo 1 "Lampe")) "gleich bis auf Leerraum"
            Expect.isFalse (memoDraftChangesEntry "" (entryOf 1)) "leer bleibt leer"
            Expect.isTrue (memoDraftChangesEntry "Neu" (withMemo 1 "Lampe")) "anderer Text"
            Expect.isTrue (memoDraftChangesEntry "" (withMemo 1 "Lampe")) "Leeren ist eine Änderung"
            Expect.isFalse (memoDraftChangesEntry (String('a', ynabMemoMaxLength + 1)) (entryOf 1)) "zu lang zählt nicht"

        testCase "Toast-Texte: gesetzt, geändert, entfernt" <| fun () ->
            Expect.equal (memoToastText None (Some "x")) "Memo gesetzt" "gesetzt"
            Expect.equal (memoToastText (Some "a") (Some "b")) "Memo geändert" "geändert"
            Expect.equal (memoToastText (Some "a") None) "Memo entfernt" "entfernt"

        testCase "Hinweis Memo nicht in YNAB mit Grund und Erneut senden; ein Befehlsfehler hat Vorrang" <| fun () ->
            let failing = { withMemo 1 "Lampe" with MemoWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            let hint = memoHintFor None failing |> Option.get
            Expect.equal hint.Title "Memo nicht in YNAB" "Titel"
            Expect.equal hint.Reason "Keine Verbindung zu YNAB" "Grund"
            Expect.equal hint.RetryLabel (Some "Erneut senden") "Erneut senden"
            Expect.isNone (memoHintFor None (withMemo 1 "Lampe")) "ohne Fehlschlag keine Leiste"
            let commandFailed = memoHintFor (Some (MemoCommandFailed "Netz")) (entryOf 1) |> Option.get
            Expect.equal commandFailed.RetryLabel None "nichts Offenes zu senden"

        testCase "die Hinweisleiste des Details trägt das Memo nach allen anderen; Erneut senden schickt StartRetryMemo" <| fun () ->
            let failing = { withMemo 1 "Lampe" with MemoWriteBackFailure = Some "weg" }
            let notice = Components.Ledger.View.detailNoticeWithMemo None None None None NotAsked failing |> Option.get
            Expect.equal
                (Components.Ledger.View.detailNoticeActionMsg failing.Id notice)
                (Some (StartRetryMemo failing.Id))
                "Erneut senden"
            let both = { failing with FlagWriteBackFailure = Some "Flag weg" }
            match Components.Ledger.View.detailNoticeWithMemo None None None None NotAsked both with
            | Some (Components.Ledger.View.FlagNotice _) -> ()
            | other -> failtest $"das Flag hat Vorrang, war {other}"
            Expect.isNone
                (Components.Ledger.View.detailNoticeWithMemo None None None None NotAsked (withMemo 1 "Lampe"))
                "ohne Fehlschlag keine Leiste"

        testCase "Toast Rückgängig wird zur Nachricht StartUndoMemo" <| fun () ->
            match State.toastActionMsg (UndoMemo (idOf 1, Some "alt")) with
            | State.LedgerMsg (StartUndoMemo (id, previous)) ->
                Expect.equal (id, previous) (idOf 1, Some "alt") "Übersetzung"
            | other -> failtest $"erwartet StartUndoMemo, war {other}"
    ]

// ============================================
// Reducer
// ============================================

[<Tests>]
let memoReducerTests =
    testList "Memo bearbeiten: Reducer" [

        testCase "StartMemoEdit öffnet das Feld mit dem heutigen Memo, ohne Memo leer" <| fun () ->
            let model, msgs, _ = run (StartMemoEdit (idOf 1)) (modelWith [ withMemo 1 "Lampe" ])
            Expect.equal model.MemoEdit (Some { MemoEntryId = idOf 1; MemoDraft = "Lampe" }) "Feld offen"
            Expect.isEmpty msgs "noch kein Aufruf"
            let empty, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            Expect.equal (empty.MemoEdit |> Option.map (fun e -> e.MemoDraft)) (Some "") "leerer Entwurf"

        testCase "bei gelöschter Buchung oder laufendem Aufruf öffnet das Feld nicht" <| fun () ->
            let deleted = modelWith [ { entryOf 1 with DeletedInYnab = true } ]
            let m1, _, _ = run (StartMemoEdit (idOf 1)) deleted
            Expect.isNone m1.MemoEdit "gelöscht"
            let sending = { modelWith [ entryOf 1 ] with MemoRows = Map.ofList [ idOf 1, MemoSending ] }
            let m2, _, _ = run (StartMemoEdit (idOf 1)) sending
            Expect.isNone m2.MemoEdit "Aufruf läuft"

        testCase "MemoEditChanged ändert den Entwurf, CancelMemoEdit schließt ohne Änderung" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ withMemo 1 "Lampe" ])
            let typed, _, _ = run (MemoEditChanged "Lampe neu") opened
            Expect.equal (typed.MemoEdit |> Option.map (fun e -> e.MemoDraft)) (Some "Lampe neu") "Entwurf"
            let cancelled, _, _ = run CancelMemoEdit typed
            Expect.isNone cancelled.MemoEdit "geschlossen"
            Expect.equal (rowOf 1 cancelled).Memo (Some "Lampe") "Memo unverändert"

        testCase "Esc schließt erst das Memo-Feld, das Detail bleibt; der zweite Esc schließt das Detail" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let first, _, _ = run EscapePressed opened
            Expect.isNone first.MemoEdit "Feld zu"
            Expect.isSome first.DetailFor "Detail bleibt"
            let second, _, _ = run EscapePressed first
            Expect.isNone second.DetailFor "Detail zu"

        testCase "ein anderes Detail oder Schließen verwirft den Entwurf" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1; entryOf 2 ])
            let other, _, _ = run (OpenDetail (rowOf 2 opened)) opened
            Expect.isNone other.MemoEdit "anderes Detail"
            let closed, _, _ = run CloseDetail opened
            Expect.isNone closed.MemoEdit "geschlossen"

        testCase "StartSaveMemo sperrt die Zeile und stößt den Aufruf mit dem Entwurf an" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let typed, _, _ = run (MemoEditChanged "Werkzeug") opened
            let model, msgs, _ = run (StartSaveMemo (idOf 1)) typed
            Expect.equal (model.MemoRows |> Map.tryFind (idOf 1)) (Some MemoSending) "läuft"
            Expect.equal msgs [ RunSetMemo (idOf 1, "Werkzeug") ] "Aufruf"

        testCase "zu langer Entwurf und laufender Aufruf: Speichern tut nichts" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let tooLong, _, _ = run (MemoEditChanged (String('a', ynabMemoMaxLength + 1))) opened
            let m1, msgs1, _ = run (StartSaveMemo (idOf 1)) tooLong
            Expect.equal m1 tooLong "unverändert"
            Expect.isEmpty msgs1 "kein Aufruf"
            let typed, _, _ = run (MemoEditChanged "ok") opened
            let busy = { typed with MemoRows = Map.ofList [ idOf 1, MemoSending ] }
            let m2, msgs2, _ = run (StartSaveMemo (idOf 1)) busy
            Expect.equal m2 busy "unverändert"
            Expect.isEmpty msgs2 "kein zweiter Aufruf"

        testCase "unverändertes Memo: Speichern schließt das Feld ohne Aufruf" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ withMemo 1 "Lampe" ])
            let padded, _, _ = run (MemoEditChanged "  Lampe ") opened
            let model, msgs, _ = run (StartSaveMemo (idOf 1)) padded
            Expect.isNone model.MemoEdit "Feld zu"
            Expect.isEmpty msgs "kein Aufruf"
            Expect.isEmpty model.MemoRows "keine Sperre"

        testCase "MemoSet Ok: Memo in Zeile und Detail, Feld zu, Toast mit Rückgängig, Historie neu" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let model', msgs, external = run (MemoSet (idOf 1, Ok (outcome None (Some "Werkzeug") MemoSent))) opened
            Expect.equal (rowOf 1 model').Memo (Some "Werkzeug") "Zeile"
            Expect.equal (model'.DetailFor |> Option.bind (fun d -> d.Memo)) (Some "Werkzeug") "Detail zieht mit"
            Expect.isNone model'.MemoEdit "Feld zu"
            Expect.isEmpty model'.MemoRows "Sperre gelöst"
            Expect.isNone (rowOf 1 model').MemoWriteBackFailure "kein Fehlschlag"
            Expect.equal msgs [ LoadHistory (idOf 1) ] "die Historie holt die neue Zeile"
            match external with
            | ShowToastWithAction ("Memo gesetzt", ToastSuccess, { Kind = UndoMemo (id, previous) }) ->
                Expect.equal (id, previous) (idOf 1, None) "Rückgängig kennt das alte Memo"
            | other -> failtest $"erwartet Toast mit Rückgängig, war {other}"

        testCase "MemoSet Ok beim Leeren: Memo weg, Toast Memo entfernt mit dem alten Text zum Zurückholen" <| fun () ->
            let model', _, external = run (MemoSet (idOf 1, Ok (outcome (Some "Lampe") None MemoSent))) (modelWith [ withMemo 1 "Lampe" ])
            Expect.isNone (rowOf 1 model').Memo "kein Memo"
            match external with
            | ShowToastWithAction ("Memo entfernt", ToastSuccess, { Kind = UndoMemo (_, previous) }) ->
                Expect.equal previous (Some "Lampe") "altes Memo"
            | other -> failtest $"{other}"

        testCase "MemoSet mit gescheitertem Versand: Fehlschlag an der Buchung, Warn-Toast, Memo bleibt in BB" <| fun () ->
            let model', _, external =
                run (MemoSet (idOf 1, Ok (outcome None (Some "Notiz") (MemoSendFailed "YNAB weg")))) (modelWith [ entryOf 1 ])
            Expect.equal (rowOf 1 model').MemoWriteBackFailure (Some "YNAB weg") "Grund"
            Expect.equal (rowOf 1 model').Memo (Some "Notiz") "Memo bleibt in BB"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) -> Expect.stringContains text "YNAB weg" "Grund im Toast"
            | other -> failtest $"erwartet Warn-Toast, war {other}"

        testCase "MemoSet ohne Änderung: kein Toast, kein Historie-Abruf" <| fun () ->
            let unchanged = { PreviousMemo = Some "x"; CurrentMemo = Some "x"; MemoChange = MemoUnchanged; MemoWriteBack = MemoNotSent }
            let model', msgs, external = run (MemoSet (idOf 1, Ok unchanged)) (modelWith [ withMemo 1 "x" ])
            Expect.equal external NoOp "kein Toast"
            Expect.isEmpty msgs "kein Abruf"
            Expect.equal (rowOf 1 model').Memo (Some "x") "Memo gleich"

        testCase "MemoSet Error: der Befehlsfehler steht an der Zeile, das Feld bleibt mit dem Entwurf offen" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let typed, _, _ = run (MemoEditChanged "Entwurf") opened
            let model', _, external = run (MemoSet (idOf 1, Error "Buchung nicht gefunden")) typed
            Expect.equal (model'.MemoRows |> Map.tryFind (idOf 1)) (Some (MemoCommandFailed "Buchung nicht gefunden")) "Zeilenzustand"
            Expect.equal (model'.MemoEdit |> Option.map (fun e -> e.MemoDraft)) (Some "Entwurf") "Entwurf bleibt"
            Expect.isNone (rowOf 1 model').Memo "Memo unverändert"
            match external with
            | ShowToast (_, ToastError) -> ()
            | other -> failtest $"erwartet Fehler-Toast, war {other}"

        testCase "Weitertippen nach einem Befehlsfehler nimmt den Hinweis zurück" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1 ])
            let failed, _, _ = run (MemoSet (idOf 1, Error "Netz")) opened
            let typed, _, _ = run (MemoEditChanged "weiter") failed
            Expect.isEmpty typed.MemoRows "kein Fehlerzustand mehr"

        testCase "Rückgängig: StartUndoMemo sperrt und stößt RunUndoMemo an; MemoRestored stellt das alte Memo her" <| fun () ->
            let model, msgs, _ = run (StartUndoMemo (idOf 1, Some "alt")) (modelWith [ withMemo 1 "neu" ])
            Expect.equal (model.MemoRows |> Map.tryFind (idOf 1)) (Some MemoSending) "läuft"
            Expect.equal msgs [ RunUndoMemo (idOf 1, Some "alt") ] "Aufruf"
            let restored, _, external = run (MemoRestored (idOf 1, Ok (outcome (Some "neu") (Some "alt") MemoSent))) model
            Expect.equal (rowOf 1 restored).Memo (Some "alt") "altes Memo"
            Expect.isEmpty restored.MemoRows "Sperre gelöst"
            match external with
            | ShowToast ("Memo zurückgesetzt", ToastSuccess) -> ()
            | other -> failtest $"erwartet Toast ohne Rückgängig, war {other}"

        testCase "Rückgängig auf kein Memo: das Memo ist danach weg" <| fun () ->
            let restored, _, _ = run (MemoRestored (idOf 1, Ok (outcome (Some "neu") None MemoSent))) (modelWith [ withMemo 1 "neu" ])
            Expect.isNone (rowOf 1 restored).Memo "kein Memo"

        testCase "Erneut senden: StartRetryMemo sperrt; MemoRetried löst den Fehlschlag oder ersetzt den Grund" <| fun () ->
            let failing = { withMemo 1 "Notiz" with MemoWriteBackFailure = Some "weg" }
            let model, msgs, _ = run (StartRetryMemo (idOf 1)) (modelWith [ failing ])
            Expect.equal msgs [ RunRetryMemo (idOf 1) ] "Aufruf"
            let sent, _, external = run (MemoRetried (idOf 1, Ok MemoSent)) model
            Expect.isNone (rowOf 1 sent).MemoWriteBackFailure "Fehlschlag beendet"
            Expect.equal external NoOp "kein Toast bei Erfolg"
            Expect.isEmpty sent.MemoRows "Sperre gelöst"
            let again, _, warn = run (MemoRetried (idOf 1, Ok (MemoSendFailed "immer noch"))) model
            Expect.equal (rowOf 1 again).MemoWriteBackFailure (Some "immer noch") "neuer Grund"
            match warn with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtest $"erwartet Warn-Toast, war {other}"

        testCase "die Tastatur der Liste ist gesperrt, solange das Memo-Feld offen ist" <| fun () ->
            let opened, _, _ = run (StartMemoEdit (idOf 1)) (modelWith [ entryOf 1; entryOf 2 ])
            let focused = { opened with FocusedRow = Some (idOf 2) }
            let model, _, _ = run (ReviewKeyPressed Reviewed) focused
            Expect.equal model.ReviewMarkRows focused.ReviewMarkRows "G schaltet nichts"
    ]

// ============================================
// Ansicht: Kategorie am Handy kurz, Fußtext im Picker, Styleguide
// ============================================

[<Tests>]
let detailFeinschliffTests =
    testList "Detail-Feinschliff: Kategorie kurz am Handy, Fußtext, Styleguide" [

        testCase "Handy-Detail zeigt die Kategorie kurz, PC-Detail voll qualifiziert" <| fun () ->
            let entry = entryOf 1
            Expect.equal (Components.Ledger.View.detailCategoryLabel DetailSheet entry) "Haushalt" "Handy: der Blattname"
            Expect.equal (Components.Ledger.View.detailCategoryLabel DetailPanel entry) "Alltag: Haushalt" "PC: Gruppe und Name"

        testCase "ohne bekannte Gruppe sind beide Formen gleich, Split und Übertrag behalten ihren Text" <| fun () ->
            let noGroup = { entryOf 1 with CategoryGroupName = None }
            Expect.equal (Components.Ledger.View.detailCategoryLabel DetailPanel noGroup) "Haushalt" "PC ohne Gruppe"
            let split = { entryOf 1 with CategoryName = None; CategoryId = None; Splits = [ { Target = Uncategorized; Amount = eur -5m; Memo = None }; { Target = Uncategorized; Amount = eur -5m; Memo = None } ] }
            Expect.equal (Components.Ledger.View.detailCategoryLabel DetailSheet split) "2 Split-Zeilen" "Split"

        testCase "die Split-Zeilen im Detail bleiben voll qualifiziert, auch am Handy (ADR 0045)" <| fun () ->
            let id = Guid.Parse "0195d700-0000-7000-8000-000000000002"
            let split = { Target = ToCategory (YnabCategoryId id, "Möbel"); Amount = eur -23.40m; Memo = None }
            Expect.equal
                (Components.Ledger.View.splitTargetLabel (Map.ofList [ id, "Wohnen" ]) split)
                "Wohnen: Möbel"
                "unabhängig von der Form des Details"

        testCase "der Picker am PC trägt den Fußtext, das Sheet am Handy nicht" <| fun () ->
            Expect.equal
                Client.DesignSystem.CategoryPicker.popoverFooterHint
                "Esc schließt ohne zu ändern · Klick wählt sofort"
                "Wortlaut der Tafel 4.2"
            let src = readRepo [ "src"; "Client"; "DesignSystem"; "CategoryPicker.fs" ]
            Expect.stringContains src "MarkCurrent = true; FooterHint = Some popoverFooterHint" "Popover am PC"
            Expect.stringContains src "MarkCurrent = markCurrent; FooterHint = None" "Sheet am Handy ohne Fußtext"
            Expect.stringContains src "category-picker-footer-hint" "Testkennung für den Wächter"

        testCase "die Ansicht ist verdrahtet: Memo-Zeile, Knopf, Eingabefeld und Hinweisleiste" <| fun () ->
            let src = viewSource ()
            for literal in [ "detail-memo-action"; "Detail.memoEditor"; "MemoNotice"; "StartSaveMemo"; "detailNoticeWithMemo"; "memoField" ] do
                Expect.stringContains src literal $"View.fs enthält {literal}"
            Expect.isFalse (src.Contains "\"Kein Memo\")\n          None") "die starre Zeile ohne Knopf ist weg"

        testCase "/styleguide zeigt die Auswahl-Bausteine und die neuen Memo-Bausteine" <| fun () ->
            let src = readRepo [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            for literal in
                [ "components-multiselect-checkbox"
                  "components-multiselect-summary"
                  "components-multiselect-bar-mobile"
                  "components-multiselect-bar-desktop"
                  "MultiSelect.summaryRow"
                  "MultiSelect.barMobile"
                  "MultiSelect.barDesktop"
                  "MultiSelect.boxButton"
                  "multiSelectSection"
                  "Detail.memoEditor" ] do
                Expect.stringContains src literal $"Styleguide enthält {literal}"
    ]
