module LedgerCategoryChangeClientTests

// ledger-sfab0: Kategorie ändern im Kassenbuch-Detail, Client-Seite (ADR 0055 und Nachtrag).
// Der Reducer und die reinen Bausteine der Ansicht laufen ohne Proxy: `Start...` setzt den
// Zustand, der Aufruf steckt hinter `Run...` (ADR 0020). Feliz-Elemente lassen sich unter .NET
// nicht inspizieren; die Verdrahtung der Ansicht prüfen Quelltext-Zusicherungen.

open System
open System.IO
open Expecto
open Shared.Domain
open Types
open Components.Ledger.Types
open Client.DesignSystem

let private eur (amount: decimal) : Money = { Amount = amount; Currency = "EUR" }

let private catId (n: int) = YnabCategoryId (Guid.Parse $"0195c100-0000-7000-8000-{n:D12}")
let private catGuid (n: int) = Guid.Parse $"0195c100-0000-7000-8000-{n:D12}"

let private category (n: int) (group: string) (name: string) (available: decimal) : YnabCategory =
    { Id = catId n
      Name = name
      GroupName = group
      Available = Some (eur available)
      Hidden = false }

let private categories =
    [ category 1 "Alltag" "Lebensmittel" 100m
      category 2 "Alltag" "Haushalt" 50m
      category 3 "Wohnen" "Möbel" 200m ]

let private entryOf (n: int) : LedgerEntryView =
    { Id = Guid.Parse $"0195f500-0000-7000-8000-{n:D12}"
      AccountId = Guid.Parse "0195e500-0000-7000-8000-000000000001"
      Date = DateTime.Today
      Amount = eur -10m
      Payee = Some $"Payee {n}"
      Memo = None
      CategoryName = Some "Lebensmittel"
      CategoryId = Some (catGuid 1)
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
      LinkedToYnab = true
      PushToYnabFailure = None }

let private idOf (n: int) = (entryOf n).Id

/// Ein Modell mit geladenen Zeilen und Kategorien, Detail offen auf Zeile 1.
let private modelWith (rows: LedgerEntryView list) : Model =
    let model, _ = Components.Ledger.State.init ()
    { model with
        Entries = Success rows
        NextPage = 1
        TotalCount = List.length rows
        TotalAmount = eur (rows |> List.sumBy (fun e -> e.Amount.Amount))
        HasMoreEntries = false
        Categories = Success categories
        CategoriesLoadedAt = Some DateTime.Now
        DetailFor = rows |> List.tryHead
        DetailForm = DetailPanel }

let private defaultModel () = modelWith [ entryOf 1; entryOf 2 ]

let private update = Components.Ledger.State.update

/// Führt nur reine Folgebefehle aus (`Cmd.ofMsg`) und sammelt ihre Nachrichten. Nicht für
/// Schritte mit Karte, deren Ablauf-Befehl wirklich schläft.
let private run (msg: Msg) (model: Model) : Model * Msg list * ExternalMsg =
    let model', cmd, external = update msg model
    let sent = ResizeArray<Msg>()
    for sub in cmd do
        sub sent.Add
    model', List.ofSeq sent, external

let private entriesOf (model: Model) =
    match model.Entries with
    | Success entries -> entries
    | _ -> failtest "Entries sollte geladen sein"

let private rowOf (n: int) (model: Model) = entriesOf model |> List.find (fun e -> e.Id = idOf n)

let private availableOf (n: int) (model: Model) : decimal =
    match model.Categories with
    | Success cs -> (cs |> List.find (fun c -> c.Id = catId n)).Available.Value.Amount
    | _ -> failtest "Kategorien sollten geladen sein"

let private changed (previous: CategoryChoice option) (writeBack: CategoryWriteBack) : CategoryChangeOutcome =
    { Previous = previous; Change = CategoryChanged; WriteBack = writeBack }

let private lebensmittel : CategoryChoice = { CategoryId = catId 1; CategoryName = "Lebensmittel" }

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private readRepo (parts: string list) =
    File.ReadAllText(Path.Combine(repoRoot :: parts |> List.toArray))

let private viewSource () = readRepo [ "src"; "Client"; "Components"; "Ledger"; "View.fs" ]

[<Tests>]
let openingTests =
    testList "Kategorie im Detail: Picker öffnen (ledger-sfab0)" [

        testCase "Pfeil-Klick öffnet den Picker für eine änderbare Buchung und lädt Kategorien und Vorschläge je einmal" <| fun () ->
            let model, sent, _ = run (OpenCategoryPicker (idOf 1)) (defaultModel ())
            Expect.equal model.CategoryPickerFor (Some (idOf 1)) "Picker offen"
            Expect.equal (sent |> List.filter ((=) EnsureCategoriesFresh) |> List.length) 1 "EnsureCategoriesFresh genau einmal"
            let loads = sent |> List.choose (function RunLoadCategorySuggestions (id, payee, current) -> Some (id, payee, current) | _ -> None)
            Expect.equal loads [ (idOf 1, "Payee 1", Some (catId 1)) ] "Vorschläge genau einmal, mit Payee und aktueller Kategorie"

        testCase "Ohne Payee gibt es keinen Vorschlags-Aufruf" <| fun () ->
            let rows = [ { entryOf 1 with Payee = None }; entryOf 2 ]
            let _, sent, _ = run (OpenCategoryPicker (idOf 1)) (modelWith rows)
            Expect.isEmpty (sent |> List.filter (function RunLoadCategorySuggestions _ -> true | _ -> false)) "kein Aufruf"

        testCase "Bei einer in YNAB gelöschten Buchung bleibt der Picker zu" <| fun () ->
            let rows = [ { entryOf 1 with DeletedInYnab = true }; entryOf 2 ]
            let model, sent, _ = run (OpenCategoryPicker (idOf 1)) (modelWith rows)
            Expect.isNone model.CategoryPickerFor "zu"
            Expect.isEmpty sent "kein Folgebefehl"

        testCase "Bei einem Übertrag ohne Kategorie bleibt der Picker zu, mit Kategorie öffnet er" <| fun () ->
            let transfer = { entryOf 1 with IsTransfer = true; CategoryId = None; CategoryName = None; CategoryGroupName = None }
            let closed, _, _ = run (OpenCategoryPicker (idOf 1)) (modelWith [ transfer; entryOf 2 ])
            Expect.isNone closed.CategoryPickerFor "Übertrag ohne Kategorie ist gesperrt"
            let tracking = { entryOf 1 with IsTransfer = true }
            let opened, _, _ = run (OpenCategoryPicker (idOf 1)) (modelWith [ tracking; entryOf 2 ])
            Expect.isSome opened.CategoryPickerFor "Übertrag mit Kategorie bleibt änderbar"

        testCase "Bei einer Split-Buchung bleibt der Picker zu" <| fun () ->
            let split =
                { entryOf 1 with
                    CategoryId = None
                    CategoryName = None
                    Splits = [ { Target = Uncategorized; Amount = eur -10m; Memo = None } ] }
            let model, _, _ = run (OpenCategoryPicker (idOf 1)) (modelWith [ split; entryOf 2 ])
            Expect.isNone model.CategoryPickerFor "zu"

        testCase "Für eine Buchung, die nicht im Detail steht, öffnet der Picker nicht" <| fun () ->
            let model, _, _ = run (OpenCategoryPicker (idOf 2)) (defaultModel ())
            Expect.isNone model.CategoryPickerFor "zu"

        testCase "Die Sperre nennt in jedem gesperrten Fall einen Hinweistext" <| fun () ->
            Expect.isNone (categoryLockHint (categoryLockFor (entryOf 1))) "änderbar: kein Text"
            let deleted = categoryLockHint (categoryLockFor { entryOf 1 with DeletedInYnab = true })
            let transfer = categoryLockHint (categoryLockFor { entryOf 1 with IsTransfer = true; CategoryId = None })
            let split =
                categoryLockHint (categoryLockFor { entryOf 1 with Splits = [ { Target = Uncategorized; Amount = eur -1m; Memo = None } ] })
            Expect.isSome deleted "gelöscht"
            Expect.equal transfer (Some "Übertrag zwischen Budget-Konten, YNAB lässt hier keine Kategorie zu") "Übertrag"
            Expect.equal split (Some "Split bearbeiten folgt") "Split"

        testCase "Der Pfeil schaltet den Picker, bei gesperrter Buchung tut er nichts" <| fun () ->
            let model = defaultModel ()
            Expect.equal (Components.Ledger.View.categoryArrowMsg model (entryOf 1)) (Some (OpenCategoryPicker (idOf 1))) "öffnet"
            let open' = { model with CategoryPickerFor = Some (idOf 1) }
            Expect.equal (Components.Ledger.View.categoryArrowMsg open' (entryOf 1)) (Some CloseCategoryPicker) "schließt"
            Expect.isNone (Components.Ledger.View.categoryArrowMsg model { entryOf 1 with DeletedInYnab = true }) "gesperrt"

        testCase "Vorschläge: die Antwort gilt für den offenen Picker, ein Fehler lässt den Abschnitt weg" <| fun () ->
            let opened, _, _ = update (OpenCategoryPicker (idOf 1)) (defaultModel ())
            let loaded, _, _ = update (CategorySuggestionsLoaded (idOf 1, Ok [ catId 2; catId 3 ])) opened
            Expect.equal loaded.CategorySuggestions [ catId 2; catId 3 ] "Vorschläge gesetzt"
            let failed, _, _ = update (CategorySuggestionsLoaded (idOf 1, Error "kaputt")) loaded
            Expect.isEmpty failed.CategorySuggestions "Fehler: Abschnitt fehlt"
            let empty, _, _ = update (CategorySuggestionsLoaded (idOf 1, Ok [])) loaded
            Expect.isEmpty empty.CategorySuggestions "leere Antwort: Abschnitt fehlt"
            let stale, _, _ = update (CategorySuggestionsLoaded (idOf 2, Ok [ catId 2 ])) opened
            Expect.isEmpty stale.CategorySuggestions "Antwort für eine andere Buchung gilt nicht"
            let closed, _, _ = update CloseCategoryPicker loaded
            let late, _, _ = update (CategorySuggestionsLoaded (idOf 1, Ok [ catId 2 ])) closed
            Expect.isEmpty late.CategorySuggestions "Antwort nach dem Schließen gilt nicht"

        testCase "Die Fußzeile kommt aus availableAgeLabel und fehlt ohne Abruf" <| fun () ->
            let now = DateTime(2026, 10, 24, 12, 0, 0)
            let model = defaultModel ()
            Expect.isNone (Components.Ledger.View.categoryStandLabel { model with CategoriesLoadedAt = None } now) "kein Stand, keine Zeile"
            let label =
                Components.Ledger.View.categoryStandLabel { model with CategoriesLoadedAt = Some (now.AddMinutes -3.0) } now
            Expect.equal label (Some (availableAgeLabel (now.AddMinutes -3.0) now)) "aus availableAgeLabel"
            Expect.equal label (Some "Stand vor 3 Min") "Beschriftung"
    ]

[<Tests>]
let changeTests =
    testList "Kategorie im Detail: ändern, Karte, Toast (ledger-sfab0)" [

        testCase "Auswahl im Picker setzt die Zeile auf Sendet und ruft setEntryCategory genau einmal" <| fun () ->
            let opened, _, _ = update (OpenCategoryPicker (idOf 1)) (defaultModel ())
            let model, sent, _ = run (StartSetEntryCategory (idOf 1, Some (catId 3))) opened
            Expect.equal (model.CategoryRows |> Map.tryFind (idOf 1)) (Some CategorySending) "sendet"
            Expect.isNone model.CategoryPickerFor "Picker zu"
            Expect.equal sent [ RunSetEntryCategory (idOf 1, Some (catId 3)) ] "genau ein Aufruf"
            // Ein zweiter Klick während des Aufrufs tut nichts.
            let again, sentAgain, _ = run (StartSetEntryCategory (idOf 1, Some (catId 2))) model
            Expect.equal again model "unverändert"
            Expect.isEmpty sentAgain "kein zweiter Aufruf"

        testCase "CategoryChanged setzt Kategorie in Liste und Detail, schreibt Available fort und zeigt die Karte" <| fun () ->
            let model, _, external =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let row = rowOf 1 model
            Expect.equal row.CategoryId (Some (catGuid 3)) "Liste: Id"
            Expect.equal row.CategoryName (Some "Möbel") "Liste: Name"
            Expect.equal row.CategoryGroupName (Some "Wohnen") "Liste: Gruppe"
            Expect.equal (model.DetailFor |> Option.map (fun d -> d.CategoryId)) (Some (Some (catGuid 3))) "Detail trägt dieselbe Kategorie"
            Expect.equal (availableOf 1 model) 110m "alte Kategorie bekommt den Betrag zurück (Ausgabe -10)"
            Expect.equal (availableOf 3 model) 190m "neue Kategorie verliert den Betrag"
            Expect.isNone model.CategoryPickerFor "Picker zu"
            Expect.isFalse (model.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand zurückgesetzt"
            match model.CategoryCard with
            | Some card ->
                Expect.equal card.EntryId (idOf 1) "Karte gehört zur Buchung"
                Expect.equal
                    (card.Lines |> List.map (fun l -> l.Label))
                    [ "Alltag: Lebensmittel"; "Wohnen: Möbel" ]
                    "zwei Zeilen, alte zuerst"
                Expect.isTrue (card.Lines |> List.forall (fun l -> l.OldText.IsSome && l.NewText.IsSome)) "alt und neu je Zeile"
            | None -> failtest "die Karte sollte stehen"
            match external with
            | ShowToastWithAction (text, ToastSuccess, action) ->
                Expect.equal text "Kategorie geändert: Möbel" "Toast-Text"
                Expect.equal action.Label "Rückgängig" "Aktion"
                Expect.equal action.Kind (UndoCategory (idOf 1, Some lebensmittel)) "Rücknahme kennt den alten Stand"
                Expect.isFalse (text |> Seq.exists Char.IsDigit) "keine Beträge im Toast"
            | other -> failtestf "unerwarteter Toast: %A" other

        testCase "CategoryUnchanged erzeugt weder Karte noch Toast und lässt Available stehen" <| fun () ->
            let outcome = { Previous = Some lebensmittel; Change = CategoryUnchanged; WriteBack = CategoryNotSent }
            let model, _, external = update (EntryCategorySet (idOf 1, Some (catId 1), Ok outcome)) (defaultModel ())
            Expect.isNone model.CategoryCard "keine Karte"
            Expect.equal external NoOp "kein Toast"
            Expect.equal (availableOf 1 model) 100m "Available unverändert"
            Expect.isFalse (model.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand zurückgesetzt"

        testCase "Ohne Kategorie: der Toast sagt Kategorie entfernt mit dem alten Namen" <| fun () ->
            let model, _, external = update (EntryCategorySet (idOf 1, None, Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let row = rowOf 1 model
            Expect.isNone row.CategoryId "ohne Kategorie"
            Expect.isNone row.CategoryName "ohne Namen"
            match external with
            | ShowToastWithAction (text, _, action) ->
                Expect.equal text "Kategorie entfernt: Lebensmittel" "Text"
                Expect.equal action.Kind (UndoCategory (idOf 1, Some lebensmittel)) "Rücknahme"
            | other -> failtestf "unerwarteter Toast: %A" other
            Expect.equal (availableOf 1 model) 110m "die alte Kategorie bekommt den Betrag zurück"

        testCase "Die Karte zeigt nur die Zeilen mit Zahlen für Buchungen des laufenden Monats" <| fun () ->
            let old = { entryOf 1 with Date = DateTime.Today.AddMonths -2 }
            let model, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (modelWith [ old; entryOf 2 ])
            Expect.equal (availableOf 1 model) 100m "ältere Buchung schreibt Available nicht fort"
            match model.CategoryCard with
            | Some card ->
                Expect.isTrue (card.Lines |> List.forall (fun l -> l.OldText.IsNone && l.NewText.IsNone)) "nur die Namen"
            | None -> failtest "die Karte zeigt die Namen"

        testCase "Die Karte läuft ab, aber nur die mit ihrer Version" <| fun () ->
            let model, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let version = model.CategoryCard.Value.Version
            let other, _, _ = update (CategoryCardExpired (version + 1)) model
            Expect.isSome other.CategoryCard "eine andere Version schließt nicht"
            let expired, _, _ = update (CategoryCardExpired version) model
            Expect.isNone expired.CategoryCard "Ablauf schließt"
            Expect.equal categoryCardDurationMs 6000 "sechs Sekunden"

        testCase "Die Karte endet mit dem Detail, beim Wechsel der Buchung und bei der nächsten Änderung" <| fun () ->
            let model, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let closed, _, _ = update CloseDetail model
            Expect.isNone closed.CategoryCard "Detail zu"
            let switched, _, _ = update (OpenDetail (entryOf 2)) model
            Expect.isNone switched.CategoryCard "andere Buchung im Detail"
            let esc, _, _ = update EscapePressed model
            Expect.isNone esc.CategoryCard "Esc schließt das Detail und mit ihm die Karte"
            let next, _, _ = update (StartSetEntryCategory (idOf 1, Some (catId 2))) model
            Expect.isNone next.CategoryCard "nächste Änderung"

        testCase "Schlägt der Versand nach YNAB fehl, bleibt die Kategorie, die Zeile trägt den Grund und der Toast warnt" <| fun () ->
            let model, _, external =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) (CategorySendFailed "HTTP 500")))) (defaultModel ())
            let row = rowOf 1 model
            Expect.equal row.CategoryId (Some (catGuid 3)) "Kategorie bleibt in BB"
            Expect.equal row.CategorizationWriteBackFailure (Some "HTTP 500") "Fehlschlag an der Zeile"
            match external with
            | ShowToastWithAction (text, ToastWarning, _) -> Expect.stringContains text "HTTP 500" "Grund im Toast"
            | other -> failtestf "unerwarteter Toast: %A" other
            match categoryHintFor None row with
            | Some hint ->
                Expect.equal hint.Title "Kategorie nicht in YNAB" "Titel"
                Expect.equal hint.Reason "HTTP 500" "Grund"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Knopf"
            | None -> failtest "Hinweis erwartet"

        testCase "Ein Befehlsfehler lässt die Kategorie stehen und meldet einen Fehler-Toast" <| fun () ->
            let model, _, external = update (EntryCategorySet (idOf 1, Some (catId 3), Error "nicht erreichbar")) (defaultModel ())
            Expect.equal (rowOf 1 model).CategoryId (Some (catGuid 1)) "unverändert"
            Expect.equal (model.CategoryRows |> Map.tryFind (idOf 1)) (Some (CategoryCommandFailed "nicht erreichbar")) "Befehlsfehler"
            match external with
            | ShowToast (_, ToastError) -> ()
            | other -> failtestf "unerwartet: %A" other
    ]

[<Tests>]
let undoTests =
    testList "Kategorie im Detail: Rückgängig und Erneut senden (ledger-sfab0)" [

        testCase "toastActionMsg übersetzt UndoCategory in StartUndoCategory mit dem alten Stand" <| fun () ->
            match State.toastActionMsg (UndoCategory (idOf 1, Some lebensmittel)) with
            | State.LedgerMsg (StartUndoCategory (id, previous)) ->
                Expect.equal id (idOf 1) "Buchung"
                Expect.equal previous (Some lebensmittel) "alter Stand"
            | other -> failtestf "unerwartete Nachricht: %A" other
            match State.toastActionMsg (UndoCategory (idOf 1, None)) with
            | State.LedgerMsg (StartUndoCategory (_, None)) -> ()
            | other -> failtestf "unerwartete Nachricht: %A" other

        testCase "Rückgängig ruft denselben Schreibweg mit der alten Kategorie auf" <| fun () ->
            let model, sent, _ = run (StartUndoCategory (idOf 1, Some lebensmittel)) (defaultModel ())
            Expect.equal (model.CategoryRows |> Map.tryFind (idOf 1)) (Some CategorySending) "sendet"
            Expect.equal sent [ RunUndoCategory (idOf 1, Some lebensmittel) ] "ein Aufruf"
            // Der Aufruf selbst ist setEntryCategory mit Previous |> Option.map _.CategoryId (siehe Quelltext).
            let source = readRepo [ "src"; "Client"; "Components"; "Ledger"; "State.fs" ]
            Expect.stringContains source "(previous |> Option.map (fun p -> p.CategoryId))" "alte Id, auch None"

        testCase "Nach der Änderung und Rücknahme trägt die Zeile wieder die alte Kategorie, Available ist zurück, ohne Karte und ohne Aktion" <| fun () ->
            let afterChange, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let restoring, _, _ = update (StartUndoCategory (idOf 1, Some lebensmittel)) afterChange
            Expect.isSome restoring.CategoryCard "vor der Rücknahme steht die Karte noch"
            let outcome = changed (Some { CategoryId = catId 3; CategoryName = "Möbel" }) CategorySent
            let model, _, external = update (EntryCategoryRestored (idOf 1, Some lebensmittel, Ok outcome)) restoring
            let row = rowOf 1 model
            Expect.equal row.CategoryId (Some (catGuid 1)) "alte Kategorie"
            Expect.equal row.CategoryName (Some "Lebensmittel") "alter Name"
            Expect.equal (availableOf 1 model) 100m "Available vertauscht zurück"
            Expect.equal (availableOf 3 model) 200m "Available vertauscht zurück"
            Expect.isNone model.CategoryCard "keine neue Karte, die alte mit den überholten Zahlen endet"
            match external with
            | ShowToast (text, ToastSuccess) -> Expect.stringContains text "Lebensmittel" "Text nennt die alte Kategorie"
            | other -> failtestf "der Rücknahme-Toast hat keine Aktion: %A" other

        testCase "Rücknahme auf ohne Kategorie setzt die Zeile auf keine Kategorie" <| fun () ->
            let afterChange, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed None CategorySent))) { defaultModel () with Categories = Success categories }
            let outcome = changed (Some { CategoryId = catId 3; CategoryName = "Möbel" }) CategorySent
            let model, _, external = update (EntryCategoryRestored (idOf 1, None, Ok outcome)) afterChange
            Expect.isNone (rowOf 1 model).CategoryId "ohne Kategorie"
            match external with
            | ShowToast (text, _) -> Expect.stringContains text "ohne Kategorie" "Text"
            | other -> failtestf "unerwartet: %A" other

        testCase "Erneut senden ruft retryCategorizationWriteBack und räumt den Fehlschlag bei CategorySent" <| fun () ->
            let failing = { entryOf 1 with CategorizationWriteBackFailure = Some "HTTP 500" }
            let started, sent, _ = run (StartRetryCategorization (idOf 1)) (modelWith [ failing; entryOf 2 ])
            Expect.equal (started.CategoryRows |> Map.tryFind (idOf 1)) (Some CategorySending) "sendet"
            Expect.equal sent [ RunRetryCategorization (idOf 1) ] "ein Aufruf"
            let sentOk, _, external = update (CategorizationRetried (idOf 1, Ok CategorySent)) started
            Expect.isNone (rowOf 1 sentOk).CategorizationWriteBackFailure "Fehlschlag weg"
            Expect.isFalse (sentOk.CategoryRows |> Map.containsKey (idOf 1)) "Zeilenzustand weg"
            Expect.equal external NoOp "kein Toast"
            let again, _, warn = update (CategorizationRetried (idOf 1, Ok (CategorySendFailed "noch immer"))) started
            Expect.equal (rowOf 1 again).CategorizationWriteBackFailure (Some "noch immer") "neuer Grund"
            match warn with
            | ShowToast (_, ToastWarning) -> ()
            | other -> failtestf "Warnung erwartet: %A" other
            let broken, _, _ = update (CategorizationRetried (idOf 1, Error "Netz")) started
            Expect.equal (broken.CategoryRows |> Map.tryFind (idOf 1)) (Some (CategoryCommandFailed "Netz")) "Befehlsfehler"
            Expect.equal (rowOf 1 broken).CategorizationWriteBackFailure (Some "HTTP 500") "offener Fehlschlag bleibt"

        testCase "Die Hinweisleiste nennt Grund und Knopf, auch nach dem Neuladen ohne Zeilenzustand, und der Knopf sendet erneut" <| fun () ->
            let failing = { entryOf 1 with CategorizationWriteBackFailure = Some "HTTP 500" }
            let notice = Components.Ledger.View.detailNoticeWithCategory None None NotAsked failing
            match notice with
            | Some (Components.Ledger.View.CategoryNotice hint) ->
                Expect.equal hint.Reason "HTTP 500" "Grund"
                Expect.equal hint.RetryLabel (Some "Erneut senden") "Knopf"
                Expect.equal
                    (Components.Ledger.View.detailNoticeActionMsg failing.Id (Components.Ledger.View.CategoryNotice hint))
                    (Some (StartRetryCategorization failing.Id))
                    "Knopf ruft den Neuversand"
            | other -> failtestf "Hinweis erwartet: %A" other
            Expect.isNone (Components.Ledger.View.detailNoticeWithCategory None None NotAsked (entryOf 1)) "ohne Fehlschlag keine Leiste"
            // Der Prüfvermerk-Hinweis behält den Vorrang (höchstens eine Leiste).
            let both = { failing with ReviewMarkWriteBackFailure = Some "Prüfvermerk" }
            match Components.Ledger.View.detailNoticeWithCategory None None NotAsked both with
            | Some (Components.Ledger.View.ReviewMarkNotice _) -> ()
            | other -> failtestf "Vorrang des Prüfvermerks: %A" other
    ]

let private filteredModel (filters: Filters) (rows: LedgerEntryView list) : Model =
    { modelWith rows with Filters = filters }

[<Tests>]
let listTests =
    testList "Kategorie im Detail: Zeile bleibt stehen (ledger-sfab0, ADR 0055 Nachtrag)" [

        testCase "Filter-Nichttreffer: Zeile bleibt, wird ausgegraut, Versatz und Zähler gehen mit, kein OrderStale" <| fun () ->
            let model0 = filteredModel { noFilters with CategoryIds = Set.singleton (catGuid 1) } [ entryOf 1; entryOf 2 ]
            let model, _, _ = update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) model0
            Expect.hasLength (entriesOf model) 2 "beide Zeilen bleiben gerendert"
            Expect.isTrue (model.FilteredOut.Contains (idOf 1)) "Merkmal: passt nicht mehr zum Filter"
            Expect.equal model.LocallyRemoved 1 "Versatz steigt"
            Expect.equal model.TotalCount 1 "Treffer sinkt"
            Expect.equal model.TotalAmount.Amount -10m "Summe ohne die Zeile"
            Expect.isFalse model.OrderStale "Datumsortierung: nicht veraltet"
            let query = Components.Ledger.State.buildPageQuery model None 1
            Expect.equal query.LocallyRemoved 1 "die nächste Seitenanfrage nutzt den erhöhten Versatz"
            Expect.equal filteredOutNote "passt nicht mehr zum Filter" "Vermerk"

        testCase "Rücknahme macht die Zeile wieder zum Treffer: Merkmal, Versatz und Zähler kehren zurück" <| fun () ->
            let model0 = filteredModel { noFilters with CategoryIds = Set.singleton (catGuid 1) } [ entryOf 1; entryOf 2 ]
            let out, _, _ = update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) model0
            let outcome = changed (Some { CategoryId = catId 3; CategoryName = "Möbel" }) CategorySent
            let model, _, _ = update (EntryCategoryRestored (idOf 1, Some lebensmittel, Ok outcome)) out
            Expect.isFalse (model.FilteredOut.Contains (idOf 1)) "Merkmal weg"
            Expect.equal model.LocallyRemoved 0 "Versatz zurück"
            Expect.equal model.TotalCount 2 "Treffer zurück"
            Expect.equal model.TotalAmount.Amount -20m "Summe zurück"

        testCase "Chip Ohne Kategorie: eine Zeile mit neuer Kategorie fällt heraus" <| fun () ->
            let blank = { entryOf 1 with CategoryId = None; CategoryName = None; CategoryGroupName = None }
            let model0 = filteredModel { noFilters with WithoutCategory = true } [ blank; entryOf 2 ]
            let model, _, _ = update (EntryCategorySet (idOf 1, Some (catId 2), Ok (changed None CategorySent))) model0
            Expect.isTrue (model.FilteredOut.Contains (idOf 1)) "passt nicht mehr"
            Expect.equal model.LocallyRemoved 1 "Versatz"

        testCase "Ohne Filterwirkung ändert sich an der Liste nichts" <| fun () ->
            let model, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            Expect.isEmpty model.FilteredOut "nichts ausgegraut"
            Expect.equal model.LocallyRemoved 0 "Versatz unverändert"
            Expect.equal model.TotalCount 2 "Zähler unverändert"
            Expect.isFalse model.OrderStale "nicht veraltet"

        testCase "Der Filter-Treffer-Test trifft Kategorie-Auswahl und Chip nach Oder" <| fun () ->
            let withCat = entryOf 1
            let without = { entryOf 1 with CategoryId = None }
            let both = { noFilters with CategoryIds = Set.singleton (catGuid 1); WithoutCategory = true }
            Expect.isTrue (rowMatchesFilters both withCat) "Kategorie trifft"
            Expect.isTrue (rowMatchesFilters both without) "Chip trifft"
            Expect.isFalse (rowMatchesFilters both { entryOf 1 with CategoryId = Some (catGuid 3) }) "keins von beiden"
            Expect.isTrue (rowMatchesFilters noFilters withCat) "ohne Kategorie-Filter passt jede Zeile"
            Expect.isFalse
                (rowMatchesFilters { noFilters with WithoutCategory = true } { without with IsTransfer = true })
                "ein Übertrag ist nicht ohne Kategorie"

        testCase "OrderStale: bei Sortierung nach Kategorie gesetzt, mit Neuladen der Zähler, auch bei Filter-Nichttreffer" <| fun () ->
            let model0 =
                { modelWith [ entryOf 1; entryOf 2 ] with
                    Sort = { Field = SortByCategory; Direction = SortAsc }
                    Categories = NotAsked
                    SelectedFilter = Some AllAccounts }
            let model, sent, _ =
                run (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) model0
            Expect.isTrue model.OrderStale "veraltet"
            Expect.contains sent RunRefreshTotals "Treffer und Summe werden neu geladen"
            let withFilter =
                { model0 with Filters = { noFilters with CategoryIds = Set.singleton (catGuid 1) } }
            let both, _, _ = update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) withFilter
            Expect.isTrue both.OrderStale "auch wenn die Zeile zugleich aus dem Filter fällt"
            Expect.isTrue (both.FilteredOut.Contains (idOf 1)) "und ausgegraut"

        testCase "OrderStale: bei Sortierung nach Datum nicht, die Rücknahme löscht es nicht" <| fun () ->
            let byDate, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            Expect.isFalse byDate.OrderStale "Datum"
            let model0 =
                { defaultModel () with Sort = { Field = SortByCategory; Direction = SortAsc }; Categories = NotAsked }
            let stale, _, _ = update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) model0
            let outcome = changed (Some { CategoryId = catId 3; CategoryName = "Möbel" }) CategorySent
            let undone, _, _ = update (EntryCategoryRestored (idOf 1, Some lebensmittel, Ok outcome)) stale
            Expect.isTrue undone.OrderStale "bleibt gesetzt"

        testCase "Im Stale-Zustand ruht das Nachladen, Neu laden setzt ab Seite 1 neu auf" <| fun () ->
            let stale =
                { defaultModel () with OrderStale = true; HasMoreEntries = true; LocallyRemoved = 2; FilteredOut = Set.singleton (idOf 1) }
            let after, sent, _ = run LoadMoreEntries stale
            Expect.equal after stale "unverändert"
            Expect.isEmpty sent "keine Seitenanfrage"
            let reloaded, sentReload, _ = run LoadEntries stale
            Expect.isFalse reloaded.OrderStale "Neu laden löscht die Leiste"
            Expect.equal reloaded.LocallyRemoved 0 "Versatz zurück"
            Expect.isEmpty reloaded.FilteredOut "ausgegraute Zeilen verschwinden"
            Expect.equal reloaded.Selection NoSelection "Auswahl bleibt leer"
            Expect.equal sentReload [ RunLoadEntriesPage 0 ] "Seite 1 mit der aktiven Sortierung"

        testCase "TotalsRefreshed ersetzt nur Treffer und Summe, und nur im Stale-Zustand" <| fun () ->
            let page : LedgerEntryPage =
                { Entries = []; TotalCount = 7; TotalAmount = eur -70m; DeletedCount = 1; NeedsReviewCount = 2 }
            let stale = { defaultModel () with OrderStale = true }
            let model, _, _ = update (TotalsRefreshed (Ok page)) stale
            Expect.equal model.TotalCount 7 "Treffer"
            Expect.equal model.TotalAmount (eur -70m) "Summe"
            Expect.hasLength (entriesOf model) 2 "Zeilen unverändert"
            let fresh, _, _ = update (TotalsRefreshed (Ok page)) (defaultModel ())
            Expect.equal fresh.TotalCount 2 "ohne Stale kein Eingriff"

        testCase "Die ausgegraute Zeile ist von Alle auswählen ausgenommen und nicht wählbar" <| fun () ->
            let model0 = { defaultModel () with FilteredOut = Set.singleton (idOf 1); TotalCount = 1 }
            let all, _, _ = update ToggleSelectAll model0
            let shown = selectionForRows all
            Expect.isFalse (isSelected shown (idOf 1)) "ausgegraut: nicht gewählt"
            Expect.isTrue (isSelected shown (idOf 2)) "die andere ist gewählt"
            Expect.equal (selectionTotals all).Count 1 "Anzahl ohne die ausgegraute Zeile"
            let toggled, _, _ = update (ToggleRowSelected (idOf 1)) model0
            Expect.equal toggled.Selection NoSelection "Kästchen und Tipp tun nichts"
            let clicked, _, _ = update (ClickRowCheckbox (idOf 1, false)) model0
            Expect.equal clicked.Selection NoSelection "Klick auf das Kästchen tut nichts"

        testCase "Esc schließt zuerst den Picker, dann das Detail" <| fun () ->
            let opened, _, _ = update (OpenCategoryPicker (idOf 1)) (defaultModel ())
            let first, _, _ = update EscapePressed opened
            Expect.isNone first.CategoryPickerFor "Picker zu"
            Expect.isSome first.DetailFor "Detail bleibt"
            let second, _, _ = update EscapePressed first
            Expect.isNone second.DetailFor "Detail zu"

        testCase "Bei offenem Picker ruhen die Listentasten" <| fun () ->
            let opened, _, _ = update (OpenCategoryPicker (idOf 1)) (defaultModel ())
            let focused = { opened with FocusedRow = Some (idOf 1) }
            let moved, _, _ = update (MoveFocus 1) focused
            Expect.equal moved.FocusedRow (Some (idOf 1)) "Fokus bleibt"
    ]

[<Tests>]
let viewTests =
    testList "Kategorie im Detail: Ansicht (ledger-sfab0)" [

        testCase "Der Picker geht am PC als Popover im Panel auf, am Handy und in der Kompaktliste als Sheet über dem Detail" <| fun () ->
            Expect.equal (Components.Ledger.View.categoryPickerPlacement DetailPanel) Components.Ledger.View.PickerPopover "PC"
            Expect.equal (Components.Ledger.View.categoryPickerPlacement DetailSheet) Components.Ledger.View.PickerSheet "Handy"
            let source = viewSource ()
            Expect.stringContains source "CategoryPicker.popoverInPanel pickerOpen pickerProps" "Popover verdrahtet"
            Expect.stringContains source "CategoryPicker.sheetLayered pickerOpen (Some sheetSubtitle) pickerProps" "zweites Sheet über dem Detail"

        testCase "Der Pfeil sitzt in detail-category, gesperrt mit Hinweistext, die Zeile selbst öffnet auch" <| fun () ->
            let source = viewSource ()
            Expect.stringContains source "\"detail-category\"" "Zeile"
            Expect.stringContains source "Detail.pickerArrow categoryArrowLabel pickerOpen (categoryLockHint lock) onArrow" "Pfeil mit Sperrtext"
            Expect.stringContains source "(if lock = CategoryEditable then Some onArrow else None)" "Klick auf die Zeile öffnet, gesperrt nicht"
            let ds = readRepo [ "src"; "Client"; "DesignSystem"; "Detail.fs" ]
            Expect.stringContains ds "aria-disabled" "gesperrt ist ausgezeichnet"
            Expect.stringContains ds "opacity-40 cursor-not-allowed" "gedämpft"
            Expect.stringContains ds "if not locked then onClick ()" "ohne Wirkung"

        testCase "Die Karte steht direkt unter dem Kategorie-Feld und nimmt die Zeilen aus dem Modell" <| fun () ->
            let source = viewSource ()
            let field = source.IndexOf "let private categoryField"
            let card = source.IndexOf("category-change-card", field)
            let popover = source.IndexOf("CategoryPicker.popoverInPanel", field)
            Expect.isTrue (field >= 0 && popover > field && card > popover) "Karte nach dem Feld samt Popover"
            Expect.stringContains source "CategoryPicker.CategoryChangeCard.view lines" "Baustein aus dem Design-System"
            let model, _, _ =
                update (EntryCategorySet (idOf 1, Some (catId 3), Ok (changed (Some lebensmittel) CategorySent))) (defaultModel ())
            let lines = Components.Ledger.View.categoryCardLinesFor model (entryOf 1)
            Expect.hasLength lines 2 "zwei Zeilen"
            Expect.isTrue (CategoryPicker.CategoryChangeCard.hasContent lines) "Karte hat Inhalt"
            Expect.isEmpty (Components.Ledger.View.categoryCardLinesFor model (entryOf 2)) "nicht für eine andere Buchung"
            Expect.isEmpty (Components.Ledger.View.categoryCardLinesFor { model with CategoryCard = None } (entryOf 1)) "nach Ablauf weg"

        testCase "Die ausgegraute Zeile trägt die gedämpfte Klasse und den Vermerk, die Leiste Neu laden nur bei OrderStale" <| fun () ->
            let source = viewSource ()
            Expect.stringContains source "prop.className \"opacity-50\"" "gedämpfte Klasse an der Handy-Zeile"
            Expect.stringContains source "(if isFilteredOut then \" opacity-50\" else \"\")" "gedämpfte Klasse an der PC-Zeile"
            Expect.stringContains source "filteredOutNote" "Vermerk an Zeilen"
            Expect.stringContains source "if not model.OrderStale then" "Leiste nur bei OrderStale"
            Expect.stringContains source "orderStaleText" "Satz der Leiste"
            Expect.stringContains source "dispatch LoadEntries" "Neu laden lädt ab Seite 1"
            Expect.stringContains source "(selectionForRows model)" "ausgegraute Zeilen sind nie gewählt"
            Expect.equal orderStaleText "Reihenfolge oder Treffer haben sich geändert." "Satz"
            Expect.equal orderStaleActionLabel "Neu laden" "Knopf"

        testCase "Der Tooltip-Text und die Toast-Texte kommen aus reinen Funktionen ohne Beträge" <| fun () ->
            Expect.equal (categoryChangedToastText (Some "Möbel") (Some lebensmittel)) "Kategorie geändert: Möbel" "geändert"
            Expect.equal (categoryChangedToastText None (Some lebensmittel)) "Kategorie entfernt: Lebensmittel" "entfernt"
            Expect.equal (categoryRestoredToastText None) "Kategorie zurückgesetzt: ohne Kategorie" "zurückgesetzt"
    ]
