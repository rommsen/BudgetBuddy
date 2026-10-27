module LedgerLenaFlagClientTests

// ledger-xfjcp: Lena fragen im Kassenbuch-Detail, Client-Seite (ADR 0056). Der Reducer und die
// reinen Bausteine der Ansicht laufen ohne Proxy: `Start...` setzt den Zustand, der Aufruf steckt
// hinter `Run...` (ADR 0020). Feliz-Elemente lassen sich unter .NET nicht inspizieren; die
// Verdrahtung der Ansicht prüfen Quelltext-Zusicherungen.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f600-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e600-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -10m
      Payee = Some $"Payee xfjcp {n}"
      Memo = None
      CategoryName = None
      CategoryId = None
      CategoryGroupName = None
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
      PushToYnabFailure = None; SplitLocks = None; ExternalLinks = [] }

let private lenaEntry (n: int) = { entryOf n with Flag = AskLena }
let private idOf (n: int) = (entryOf n).Id

let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        LenaFlagColor = Some "purple"
        AskLenaCount = rows |> List.filter (fun e -> e.Flag = AskLena) |> List.length
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

let private none : FlagState = { Color = None; Name = None }
let private lenaState : FlagState = { Color = Some "purple"; Name = Some "Lena" }

let private changed (previous: FlagState) (writeBack: FlagWriteBack) : FlagOutcome =
    { Previous = previous; Change = FlagChanged; WriteBack = writeBack }

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private viewSource () = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]

[<Tests>]
let lenaActionTests =
    testList "Lena fragen: Aktion und Hinweis im Detail (reine Bausteine)" [

        testCase "ohne Flag oder mit anderem Flag steht Lena fragen, bei Rückfrage Lena Rückfrage zurücknehmen" <| fun () ->
            let color = Some "purple"
            let a (flag: LedgerFlag) = lenaActionFor color { entryOf 1 with Flag = flag }
            Expect.equal (a NoFlag).Label "Lena fragen" "ohne Flag"
            Expect.equal (a Marked).Label "Lena fragen" "anderes Flag"
            Expect.equal (a AskLena).Label "Rückfrage zurücknehmen" "Rückfrage Lena"
            Expect.equal (a NoFlag).Target AssignLena "Ziel zuweisen"
            Expect.equal (a AskLena).Target WithdrawLena "Ziel zurücknehmen"
            Expect.isNone (a NoFlag).DisabledReason "aktiv"

        testCase "ohne gesetzte Farbe ist Lena fragen gesperrt und nennt den Grund; Zurücknehmen bleibt möglich" <| fun () ->
            let ask = lenaActionFor None (entryOf 1)
            Expect.equal ask.DisabledReason (Some lenaColorMissingReason) "Grund"
            Expect.equal lenaColorMissingReason "In den Settings die Flag-Farbe für Lena wählen" "Wortlaut"
            Expect.isNone (lenaActionFor None (lenaEntry 1)).DisabledReason "Zurücknehmen braucht die Farbe nicht"

        testCase "in YNAB gelöschte Buchungen: die Aktion ist aus" <| fun () ->
            let deleted = { entryOf 1 with DeletedInYnab = true }
            Expect.equal (lenaActionFor (Some "purple") deleted).DisabledReason (Some lenaDeletedReason) "gelöscht"

        testCase "Hinweis Flag nicht in YNAB mit Grund und Erneut senden; ein Befehlsfehler hat Vorrang" <| fun () ->
            let failing = { lenaEntry 1 with FlagWriteBackFailure = Some "Keine Verbindung zu YNAB" }
            let hint = flagHintFor None failing |> Option.get
            Expect.equal hint.Title "Flag nicht in YNAB" "Titel"
            Expect.equal hint.Reason "Keine Verbindung zu YNAB" "Grund"
            Expect.equal hint.RetryLabel (Some "Erneut senden") "Erneut senden"
            Expect.isNone (flagHintFor None (lenaEntry 1)) "ohne Fehlschlag keine Leiste"
            let commandFailed = flagHintFor (Some (FlagCommandFailed "Netz")) (lenaEntry 1) |> Option.get
            Expect.equal commandFailed.RetryLabel None "nichts Offenes zu senden"

        testCase "die Hinweisleiste des Details trägt das Flag nach Prüfvermerk und Kategorie; Erneut senden schickt StartRetryFlag" <| fun () ->
            let failing = { lenaEntry 1 with FlagWriteBackFailure = Some "weg" }
            let notice = Components.Ledger.View.detailNoticeWithFlag None None None NotAsked failing |> Option.get
            Expect.equal
                (Components.Ledger.View.detailNoticeActionMsg failing.Id notice)
                (Some (StartRetryFlag failing.Id))
                "Erneut senden"
            let both = { failing with CategorizationWriteBackFailure = Some "Kategorie weg" }
            match Components.Ledger.View.detailNoticeWithFlag None None None NotAsked both with
            | Some (Components.Ledger.View.CategoryNotice _) -> ()
            | other -> failtest $"die Kategorie hat Vorrang, war {other}"
            Expect.isNone (Components.Ledger.View.detailNoticeWithFlag None None None NotAsked (lenaEntry 1)) "ohne Fehlschlag keine Leiste"

        testCase "die Aktionsnachricht der Flag-Zeile ist gesperrt ohne Farbe und während des Aufrufs" <| fun () ->
            let model = modelWith [ entryOf 1 ]
            Expect.equal
                (Components.Ledger.View.lenaActionMsg model (entryOf 1))
                (Some (StartSetLenaFlag (idOf 1, AssignLena)))
                "aktiv"
            Expect.isNone (Components.Ledger.View.lenaActionMsg { model with LenaFlagColor = None } (entryOf 1)) "ohne Farbe"
            Expect.isNone
                (Components.Ledger.View.lenaActionMsg { model with FlagRows = Map.ofList [ idOf 1, FlagSending ] } (entryOf 1))
                "während der Aufruf läuft"

        testCase "Chip-Beschriftung, Reihenfolge und Zeilentreffer" <| fun () ->
            Expect.equal (Components.Ledger.View.askLenaChipLabel 3) "Rückfrage Lena (3)" "Beschriftung"
            Expect.equal (List.item 1 fixedChipOrder) AskLenaChip "der Chip steht hinter Zu prüfen"
            let on = { noFilters with OnlyAskLena = true }
            Expect.isTrue (rowMatchesFilters on (lenaEntry 1)) "Lena passt"
            Expect.isFalse (rowMatchesFilters on (entryOf 1)) "ohne Flag passt nicht"
            Expect.isTrue (rowMatchesFilters noFilters (entryOf 1)) "ohne Chip passt alles"
            Expect.isTrue (chipActivityOf false on).AskLena "Chip aktiv"
            Expect.isFalse (hasAnyFilter on) "zählt nicht zu den fünf Such-Filtern"

        testCase "Toast Rückgängig wird zur Nachricht StartUndoFlag" <| fun () ->
            match State.toastActionMsg (UndoFlag (idOf 1, lenaState)) with
            | State.LedgerMsg (StartUndoFlag (id, previous)) ->
                Expect.equal (id, previous) (idOf 1, lenaState) "Übersetzung"
            | other -> failtest $"erwartet StartUndoFlag, war {other}"

        testCase "flagOfState deutet wie der Lesepfad" <| fun () ->
            Expect.equal (flagOfState lenaState) AskLena "Lena"
            Expect.equal (flagOfState { Color = Some "blue"; Name = Some " LENA " }) AskLena "getrimmt, ohne Groß-/Kleinschreibung"
            Expect.equal (flagOfState { Color = Some "orange"; Name = Some "Markiert" }) Marked "anderes Flag"
            Expect.equal (flagOfState { Color = Some "orange"; Name = None }) Marked "nur Farbe"
            Expect.equal (flagOfState none) NoFlag "kein Flag"

        testCase "die Ansicht ist verdrahtet: Flag-Zeile, Knopf, Chip und Hinweisleiste" <| fun () ->
            let src = viewSource ()
            for literal in [ "detail-flag"; "Detail.fieldActionButton"; "askLenaChipLabel"; "AskLenaChip"; "FlagNotice"; "StartSetLenaFlag" ] do
                Expect.stringContains src literal $"View.fs enthält {literal}"
            let styleguide = readRepo [ "src"; "Client"; "Components"; "Styleguide"; "View.fs" ]
            Expect.stringContains styleguide "Detail.fieldActionButton" "der neue Baustein steht in /styleguide"
    ]

[<Tests>]
let lenaReducerTests =
    testList "Lena fragen: Reducer" [

        testCase "StartSetLenaFlag sperrt die Zeile und stößt den Aufruf an" <| fun () ->
            let model, msgs, _ = run (StartSetLenaFlag (idOf 1, AssignLena)) (modelWith [ entryOf 1 ])
            Expect.equal (model.FlagRows |> Map.tryFind (idOf 1)) (Some FlagSending) "läuft"
            Expect.equal msgs [ RunSetLenaFlag (idOf 1, AssignLena) ] "Aufruf"

        testCase "ohne Farbe, bei falschem Ziel, gelöschter Buchung oder laufendem Aufruf passiert nichts" <| fun () ->
            let base' = modelWith [ entryOf 1; lenaEntry 2 ]
            let noop msg model =
                let m, msgs, _ = run msg model
                Expect.equal m model "Modell unverändert"
                Expect.isEmpty msgs "kein Aufruf"
            noop (StartSetLenaFlag (idOf 1, AssignLena)) { base' with LenaFlagColor = None }
            noop (StartSetLenaFlag (idOf 2, AssignLena)) base'
            noop (StartSetLenaFlag (idOf 1, WithdrawLena)) base'
            noop (StartSetLenaFlag (idOf 1, AssignLena)) { base' with FlagRows = Map.ofList [ idOf 1, FlagSending ] }
            let deleted = modelWith [ { entryOf 1 with DeletedInYnab = true } ]
            noop (StartSetLenaFlag (idOf 1, AssignLena)) deleted

        testCase "LenaFlagSet Ok: Kennzeichen, Zähler, Toast mit Rückgängig" <| fun () ->
            let model = modelWith [ entryOf 1; entryOf 2 ]
            let model', _, external = run (LenaFlagSet (idOf 1, AssignLena, Ok (changed none FlagSent))) model
            Expect.equal (rowOf 1 model').Flag AskLena "Rückfrage Lena"
            Expect.equal (rowOf 1 model').FlagWriteBackFailure None "kein Fehlschlag"
            Expect.equal model'.AskLenaCount 1 "Zähler steigt"
            Expect.equal (model'.DetailFor |> Option.map (fun d -> d.Flag)) (Some AskLena) "Detail zieht mit"
            Expect.isEmpty model'.FilteredOut "nichts ausgegraut ohne Chip"
            match external with
            | ShowToastWithAction ("Lena gefragt", ToastSuccess, { Kind = UndoFlag (id, previous) }) ->
                Expect.equal id (idOf 1) "Buchung"
                Expect.equal previous none "Vorzustand"
            | other -> failtest $"erwartet Toast mit Rückgängig, war {other}"

        testCase "LenaFlagSet mit gescheitertem Versand: Fehlschlag an der Buchung, Warn-Toast" <| fun () ->
            let model', _, external =
                run (LenaFlagSet (idOf 1, AssignLena, Ok (changed none (FlagSendFailed "YNAB weg")))) (modelWith [ entryOf 1 ])
            Expect.equal (rowOf 1 model').FlagWriteBackFailure (Some "YNAB weg") "Grund"
            Expect.equal (rowOf 1 model').Flag AskLena "Flag bleibt in BB"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) -> Expect.stringContains text "YNAB weg" "Grund im Toast"
            | other -> failtest $"erwartet Warn-Toast, war {other}"

        testCase "NoChange: kein Toast, Zähler und Kennzeichen bleiben" <| fun () ->
            let outcome : FlagOutcome = { Previous = lenaState; Change = FlagUnchanged; WriteBack = FlagNotSent }
            let model', _, external = run (LenaFlagSet (idOf 1, AssignLena, Ok outcome)) (modelWith [ lenaEntry 1 ])
            Expect.equal external NoOp "kein Toast"
            Expect.equal model'.AskLenaCount 1 "Zähler gleich"
            Expect.equal (rowOf 1 model').Flag AskLena "Kennzeichen gleich"

        testCase "LenaFlagSet Error: der Befehlsfehler steht an der Zeile, Flag unverändert" <| fun () ->
            let model', _, external = run (LenaFlagSet (idOf 1, AssignLena, Error "Buchung nicht gefunden")) (modelWith [ entryOf 1 ])
            Expect.equal (model'.FlagRows |> Map.tryFind (idOf 1)) (Some (FlagCommandFailed "Buchung nicht gefunden")) "Zeilenzustand"
            Expect.equal (rowOf 1 model').Flag NoFlag "Flag unverändert"
            match external with
            | ShowToast (_, ToastError) -> ()
            | other -> failtest $"erwartet Fehler-Toast, war {other}"

        testCase "Zurücknehmen bei aktivem Chip: die Zeile bleibt ausgegraut stehen, bis das nächste Laden sie herausnimmt" <| fun () ->
            let model =
                { modelWith [ lenaEntry 1; lenaEntry 2 ] with Filters = { noFilters with OnlyAskLena = true } }
            let model', _, _ = run (LenaFlagSet (idOf 1, WithdrawLena, Ok (changed lenaState FlagSent))) model
            Expect.equal (rowOf 1 model').Flag NoFlag "Rückfrage zurückgenommen"
            Expect.isTrue (model'.FilteredOut.Contains (idOf 1)) "ausgegraut"
            Expect.equal (match model'.Entries with Success l -> List.length l | _ -> -1) 2 "bleibt in der Liste"
            Expect.equal model'.TotalCount 1 "Trefferzahl sinkt sofort"
            Expect.equal model'.LocallyRemoved 1 "zählt für den Versatz"
            Expect.equal model'.AskLenaCount 1 "Zähler sinkt"
            Expect.equal filteredOutNote "passt nicht mehr zum Filter" "Vermerk an der Zeile"
            let reloaded, _, _ = run LoadEntries model'
            Expect.isEmpty reloaded.FilteredOut "das nächste Laden nimmt sie heraus"

        testCase "Rückgängig am ausgegrauten Eintrag macht ihn wieder zum Treffer" <| fun () ->
            let model =
                { modelWith [ lenaEntry 1; lenaEntry 2 ] with Filters = { noFilters with OnlyAskLena = true } }
            let withdrawn, _, _ = run (LenaFlagSet (idOf 1, WithdrawLena, Ok (changed lenaState FlagSent))) model
            let restored, _, _ = run (FlagRestored (idOf 1, lenaState, Ok (changed none FlagSent))) withdrawn
            Expect.equal (rowOf 1 restored).Flag AskLena "wieder Rückfrage Lena"
            Expect.isFalse (restored.FilteredOut.Contains (idOf 1)) "nicht mehr ausgegraut"
            Expect.equal restored.TotalCount 2 "Trefferzahl zurück"
            Expect.equal restored.AskLenaCount 2 "Zähler zurück"

        testCase "Rückgängig stellt auch ein anderes Flag wieder her (Markiert)" <| fun () ->
            let previous = { Color = Some "orange"; Name = Some "Markiert" }
            let model = modelWith [ lenaEntry 1 ]
            let model', _, external = run (FlagRestored (idOf 1, previous, Ok (changed lenaState FlagSent))) model
            Expect.equal (rowOf 1 model').Flag Marked "Markiert"
            Expect.equal model'.AskLenaCount 0 "kein Lena mehr"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.equal text flagRestoredToastText "Toast ohne Rückgängig"
            | other -> failtest $"erwartet Toast, war {other}"

        testCase "StartUndoFlag sperrt die Zeile und stößt RunUndoFlag an" <| fun () ->
            let model, msgs, _ = run (StartUndoFlag (idOf 1, lenaState)) (modelWith [ entryOf 1 ])
            Expect.equal (model.FlagRows |> Map.tryFind (idOf 1)) (Some FlagSending) "läuft"
            Expect.equal msgs [ RunUndoFlag (idOf 1, lenaState) ] "Aufruf"

        testCase "Erneut senden: Zustand läuft, Erfolg löscht den Fehlschlag, erneuter Fehlschlag ersetzt den Grund" <| fun () ->
            let failing = { lenaEntry 1 with FlagWriteBackFailure = Some "alt" }
            let model = modelWith [ failing ]
            let sending, msgs, _ = run (StartRetryFlag (idOf 1)) model
            Expect.equal (sending.FlagRows |> Map.tryFind (idOf 1)) (Some FlagSending) "läuft"
            Expect.equal msgs [ RunRetryFlag (idOf 1) ] "Aufruf"
            let ok, _, okExternal = run (FlagRetried (idOf 1, Ok FlagSent)) sending
            Expect.isNone (rowOf 1 ok).FlagWriteBackFailure "Fehlschlag beendet"
            Expect.isEmpty (ok.FlagRows |> Map.toList) "Zeilenzustand frei"
            Expect.equal okExternal NoOp "kein Toast"
            let again, _, againExternal = run (FlagRetried (idOf 1, Ok (FlagSendFailed "neu"))) sending
            Expect.equal (rowOf 1 again).FlagWriteBackFailure (Some "neu") "neuer Grund"
            match againExternal with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtest $"erwartet Warn-Toast, war {other}"

        testCase "ToggleOnlyAskLena kippt den Filter und lädt ab Seite 0 neu; die Anfrage trägt OnlyAskLena" <| fun () ->
            let model = modelWith [ entryOf 1 ]
            let on, msgs, _ = run ToggleOnlyAskLena model
            Expect.isTrue on.Filters.OnlyAskLena "an"
            Expect.equal msgs [ LoadEntries ] "Neuladen"
            Expect.equal on.Entries Loading "Liste wird neu geladen"
            Expect.isTrue (Components.Ledger.State.buildPageQuery on None 0).OnlyAskLena "Seitenanfrage"
            Expect.isFalse (Components.Ledger.State.buildPageQuery model None 0).OnlyAskLena "ohne Chip aus"
            let off, _, _ = run ToggleOnlyAskLena on
            Expect.isFalse off.Filters.OnlyAskLena "wieder aus"

        testCase "Alle Filter löschen lässt den Chip Rückfrage Lena stehen" <| fun () ->
            let model = { modelWith [ entryOf 1 ] with Filters = { noFilters with OnlyAskLena = true; Memo = Some "x" } }
            let cleared, _, _ = run ClearAllFilters model
            Expect.isTrue cleared.Filters.OnlyAskLena "Chip bleibt"
            Expect.isNone cleared.Filters.Memo "Memo-Filter weg"

        testCase "die geladene Seite setzt den Zähler AskLenaCount" <| fun () ->
            let page : LedgerEntryPage =
                { Entries = [ lenaEntry 1 ]; TotalCount = 1; TotalAmount = eur -10m; DeletedCount = 0; NeedsReviewCount = 0; AskLenaCount = 4 }
            let model, _, _ = run (EntriesPageLoaded (0, Ok page)) (modelWith [])
            Expect.equal model.AskLenaCount 4 "Zähler vom Server"

        testCase "LenaFlagSettingsLoaded übernimmt die gespeicherte Farbe" <| fun () ->
            let model, _, _ = run (LenaFlagSettingsLoaded (Ok { SavedColor = Some "blue"; SuggestedColor = Some "red" })) (modelWith [])
            Expect.equal model.LenaFlagColor (Some "blue") "gespeichert, nicht der Vorschlag"
    ]

[<Tests>]
let lenaSettingsPageTests =
    let settingsModel () =
        let model, _ = Components.Settings.State.init ()
        model

    testList "Lena fragen: Settings-Seite" [

        testCase "Vorbelegung: gespeicherte Farbe, sonst der Vorschlag, sonst keine" <| fun () ->
            let pre saved suggested =
                Components.Settings.Types.lenaFlagColorPreselection { SavedColor = saved; SuggestedColor = suggested }
            Expect.equal (pre (Some "blue") (Some "red")) (Some "blue") "gespeichert gewinnt"
            Expect.equal (pre None (Some "red")) (Some "red") "Vorschlag"
            Expect.equal (pre None None) None "keine"

        testCase "Farbnamen für alle sechs YNAB-Farben" <| fun () ->
            let labels = ynabFlagColors |> List.map Components.Settings.Types.lenaFlagColorLabel
            Expect.equal labels [ "Rot"; "Orange"; "Gelb"; "Grün"; "Blau"; "Violett" ] "deutsche Namen"

        testCase "geladene Einstellung füllt das Auswahlfeld vor, Speichern merkt die Farbe" <| fun () ->
            let loaded, _, _ =
                Components.Settings.State.update
                    (Components.Settings.Types.LenaFlagSettingsLoaded (Ok { SavedColor = None; SuggestedColor = Some "purple" }))
                    (settingsModel ())
            Expect.equal loaded.LenaFlagColorInput "purple" "Vorschlag vorbelegt"
            Expect.equal loaded.LenaFlag.SavedColor None "noch nicht gespeichert"
            let saved, _, _ =
                Components.Settings.State.update
                    (Components.Settings.Types.LenaFlagColorSaved (Some "purple", Ok ()))
                    loaded
            Expect.equal saved.LenaFlag.SavedColor (Some "purple") "gespeichert"
            Expect.isNone saved.EditingSection "Bearbeiten beendet"

        testCase "ein Fehler beim Speichern lässt den Stand stehen" <| fun () ->
            let before = settingsModel ()
            let after, _, external =
                Components.Settings.State.update
                    (Components.Settings.Types.LenaFlagColorSaved (Some "pink", Error (SettingsError.DatabaseError ("validation", "x"))))
                    before
            Expect.equal after.LenaFlag before.LenaFlag "unverändert"
            match external with
            | Components.Settings.Types.ShowToast (_, ToastError) -> ()
            | other -> failtest $"erwartet Fehler-Toast, war {other}"
    ]
