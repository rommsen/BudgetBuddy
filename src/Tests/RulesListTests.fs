module Tests.RulesListTests

// categorization-n3sxw: der Tab „Regeln“ im Kassenbuch. Liste, Formular, Muster-Test, Löschen mit Rückgängig und die
// Tastenlogik laufen über das echte `update` bzw. reine Funktionen; die Aufrufe liegen hinter Lambdas und werden hier
// nicht ausgeführt.

open System
open Expecto
open Shared.Domain
open Types
open Components.Rules.Types
open Components.Rules.State
open Components.Rules.View

let private time = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)

let private rule (n: int) (enabled: bool) : Rule =
    { Id = RuleId (Guid($"00000000-0000-0000-0000-{n:D12}"))
      Name = $"Regel {n}"
      Pattern = $"muster{n}"
      PatternType = Contains
      TargetField = Payee
      CategoryId = YnabCategoryId (Guid($"10000000-0000-0000-0000-{n:D12}"))
      CategoryName = $"Kategorie {n}"
      PayeeOverride = (if n = 3 then Some "Ein Payee" else None)
      Priority = 10 - n
      Enabled = enabled
      CreatedAt = time
      UpdatedAt = time }

let private r1, r2, r3 = rule 1 true, rule 2 true, rule 3 false
let private shown = { fst (init ()) with Rules = Success [ r1; r2; r3 ]; LoadVersion = 1 }

let private step (msg: Msg) (model: Model) : Model = let m, _, _ = update msg model in m
let private stepExt (msg: Msg) (model: Model) : ExternalMsg = let _, _, e = update msg model in e
let private steps (msgs: Msg list) (model: Model) = msgs |> List.fold (fun m msg -> step msg m) model
let private ids (model: Model) =
    match model.Rules with
    | Success rules -> rules |> List.map (fun r -> r.Id)
    | other -> failtest $"keine Liste: %A{other}"
let private panelOf (model: Model) = model.Panel |> Option.defaultWith (fun () -> failtest "kein Panel")
let private isToast (ext: ExternalMsg) = match ext with ShowToast _ -> true | _ -> false

[<Tests>]
let textTests =
    testList "Regeln: Texte" [
        test "Untertitel nennt Zahl und aktive" {
            Expect.equal (summaryText [ r1; r2; r3 ]) "3 Regeln, 2 aktiv" "drei"
            Expect.equal (summaryText [ r1 ]) "1 Regel, 1 aktiv" "eine"
            Expect.equal (summaryText []) "" "keine"
        }
        test "Art am PC und Bedingung am Handy" {
            Expect.equal (kindText r1) "Enthält · Payee" "PC"
            Expect.equal (conditionText { r1 with Pattern = "rewe" }) "Enthält „rewe“ in Payee" "Handy"
            Expect.equal (conditionText { r1 with PatternType = PatternType.Regex; TargetField = Combined; Pattern = "a.*b" })
                         "Regex „a.*b“ in Payee & Memo" "Regex"
            Expect.equal (kindText { r1 with PatternType = Exact; TargetField = Memo }) "Exakt · Memo" "Exakt"
        }
        test "Ein leerer Name wird beim Speichern zum Muster" {
            let draft = { RuleDraft.empty with Name = "  "; Pattern = "rewe" }
            Expect.equal (buildNewRule draft r1.CategoryId).Name "rewe" "Anlegen"
            Expect.equal (buildRuleChange r1 draft r1.CategoryId).Name (Some "rewe") "Ändern"
            Expect.equal (buildNewRule { draft with Name = "Rewe" } r1.CategoryId).Name "Rewe" "gesetzter Name bleibt"
        }
        test "Der Schalter Regel aktiv geht beim Anlegen mit, vorbelegt an" {
            Expect.isTrue RuleDraft.empty.Enabled "an"
            Expect.isFalse (buildNewRule { RuleDraft.empty with Pattern = "x"; Enabled = false } r1.CategoryId).Enabled "aus"
        }
        test "Ohne Muster oder Kategorie nennt der Knopf, was fehlt" {
            Expect.equal (Client.DesignSystem.Form.getMissingFields (requiredFieldPairs RuleDraft.empty)) [ "Muster"; "Kategorie" ] "beides"
            let complete = { RuleDraft.empty with Pattern = "x"; CategoryId = Some r1.CategoryId }
            Expect.isEmpty (Client.DesignSystem.Form.getMissingFields (requiredFieldPairs complete)) "nichts"
        }
        test "Die Zeile unter Muster testen: Hinweis, Passt, Passt nicht, Grund, Netzfehler" {
            let request = { Pattern = "x"; PatternType = Contains; TargetField = Payee; Input = "y" }
            Expect.equal (testLine NothingToTest) (HintLine "Text eingeben, um das Muster zu prüfen.") "ohne Eingabe"
            Expect.equal (testLine (Tested (request, Matches))) (MatchLine "Passt") "passt"
            Expect.equal (testLine (Tested (request, NoMatch))) (NoMatchLine "Passt nicht") "passt nicht"
            Expect.equal (testLine (Tested (request, PatternFaulty "Ungültiges Muster"))) (FaultLine "Ungültiges Muster") "Grund"
            Expect.equal (testLine (Tested (request, Unreachable))) (FaultLine ruleTestLostText) "Netzfehler"
        }
    ]

[<Tests>]
let listTests =
    testList "Regeln: Liste" [
        test "Laden zeigt ohne Liste den Ladezustand, ein Fehler die Ursache, eine geladene Liste bleibt beim Neuladen" {
            let loading = step LoadRules (fst (init ()))
            Expect.equal loading.Rules Loading "lädt"
            Expect.equal (step (RulesLoaded (loading.LoadVersion, Error "weg")) loading).Rules (Failure "weg") "Fehler"
            Expect.equal (step (RulesLoaded (loading.LoadVersion, Ok [])) loading).Rules (Success []) "leer"
            Expect.equal (step LoadRules shown).Rules shown.Rules "kein Skeleton beim Neuladen"
        }
        test "Ein Pfeil ändert die Reihenfolge sofort, am Rand nichts" {
            Expect.equal (ids (step (MoveRequested (r2.Id, Up)) shown)) [ r2.Id; r1.Id; r3.Id ] "hoch"
            Expect.equal (ids (step (MoveRequested (r1.Id, Up)) shown)) (ids shown) "oberste nicht höher"
            Expect.equal (ids (step (MoveRequested (r3.Id, Down)) shown)) (ids shown) "unterste nicht tiefer"
        }
        test "Scheitert das Speichern der Reihenfolge, steht die alte wieder da und ein Toast nennt es" {
            let moved, _, _ = update (MoveRequested (r2.Id, Up)) shown
            let back, _, ext = update (RulesReordered ([ r1; r2; r3 ], Error "Datenbank weg")) moved
            Expect.equal (ids back) [ r1.Id; r2.Id; r3.Id ] "alte Reihenfolge"
            Expect.equal ext (ShowToast ("Reihenfolge konnte nicht gespeichert werden: Datenbank weg", ToastError)) "Toast"
        }
        // GIVEN Roman hat eine Regel verschoben WHEN eine ältere Ladeantwort danach ankommt THEN bleibt die neue
        // Reihenfolge stehen (Befund `State.fs:52` aus categorization-krscs). Alle Reihenfolgen von Laden, Pfeil
        // und Antwort mit dem Orakel: zählt die Antwort, kam sie nach dem letzten Pfeil.
        test "Eine ältere Ladeantwort überschreibt eine verschobene Reihenfolge nicht" {
            let answer version = RulesLoaded (version, Ok [ r1; r2; r3 ])
            let loaded = step LoadRules shown
            let v = loaded.LoadVersion
            for order, expected in
                [ [ MoveRequested (r2.Id, Up); answer v ], [ r2.Id; r1.Id; r3.Id ]
                  [ answer v; MoveRequested (r2.Id, Up) ], [ r2.Id; r1.Id; r3.Id ]
                  [ MoveRequested (r2.Id, Up); LoadRules; answer v ], [ r2.Id; r1.Id; r3.Id ]
                  [ MoveRequested (r2.Id, Up); LoadRules; answer (v + 2) ], [ r1.Id; r2.Id; r3.Id ] ] do
                Expect.equal (ids (steps order loaded)) expected $"%A{order}"
        }
    ]

[<Tests>]
let panelTests =
    testList "Regeln: Panel und Sheet" [
        test "Neue Regel öffnet ein leeres Formular, eine Regel ihr Formular, dieselbe noch einmal lässt die Eingaben" {
            Expect.equal (panelOf (step NewRuleRequested shown)).Editor NewRule "neu"
            let opened = step (RuleChosen r2.Id) shown
            Expect.equal (panelOf opened).Draft (RuleDraft.fromRule r2) "Felder der Regel"
            let typed = steps [ NameEntered "Anders"; RuleChosen r2.Id ] opened
            Expect.equal (panelOf typed).Draft.Name "Anders" "Eingabe bleibt"
            Expect.isNone (step PanelClosed opened).Panel "geschlossen"
        }
        test "Ohne Muster oder Kategorie geht nichts hinaus" {
            let opened = step NewRuleRequested shown
            let _, cmd, _ = update (CreateRequested InPanel) opened
            Expect.isEmpty cmd "kein Aufruf"
            Expect.equal (panelOf (step (CreateRequested InPanel) opened)).Save NotSaving "nichts läuft"
        }
        test "Anlegen und Ändern sind getrennte Nachrichten, das jeweils andere tut nichts" {
            let filled = steps [ NewRuleRequested; PatternEntered "x"; CategoryChosen (Some r1.CategoryId) ] shown
            Expect.equal (panelOf (step (ChangeRequested InPanel) filled)).Save NotSaving "Ändern auf neuer Regel"
            Expect.equal (panelOf (step (CreateRequested InPanel) filled)).Save Saving "Anlegen läuft"
            let editing = step (RuleChosen r1.Id) shown
            Expect.equal (panelOf (step (CreateRequested InPanel) editing)).Save NotSaving "Anlegen auf bestehender Regel"
            Expect.equal (panelOf (step (ChangeRequested InPanel) editing)).Save Saving "Ändern läuft"
        }
        test "Nach dem Anlegen steht die Regel vorn, das Panel zeigt sie, das Sheet schließt" {
            let created = { rule 9 true with Name = "Neu" }
            let saving = steps [ NewRuleRequested; PatternEntered "x"; CategoryChosen (Some r1.CategoryId); CreateRequested InPanel ] shown
            let inPanel, _, ext = update (RuleCreated (InPanel, Ok created)) saving
            Expect.equal (ids inPanel) [ created.Id; r1.Id; r2.Id; r3.Id ] "vorn"
            Expect.equal (panelOf inPanel).Editor (Editing created) "Panel zeigt sie"
            Expect.equal ext (ShowToast ("Regel angelegt", ToastSuccess)) "Toast"
            Expect.isNone (step (RuleCreated (InSheet, Ok created)) saving).Panel "Sheet zu"
        }
        test "Ein ungültiges Regex steht als Feldfehler am Muster, eine Eingabe nimmt ihn weg" {
            let saving = steps [ RuleChosen r1.Id; ChangeRequested InPanel ] shown
            let failed, _, ext = update (RuleChanged (InPanel, Error (RulesError.InvalidPattern ("(", "Klammer fehlt")))) saving
            Expect.equal (panelOf failed).Save (SaveFailed (PatternRejected "Klammer fehlt")) "am Muster"
            Expect.equal ext NoOp "kein Toast"
            Expect.equal (panelOf (step (PatternEntered "(a)") failed)).Save NotSaving "weg"
            let other = step (RuleChanged (InPanel, Error (RulesError.CategoryLookupFailed "YNAB weg"))) saving
            Expect.equal (panelOf other).Save (SaveFailed (SaveRejected "Kategorie konnte nicht nachgeschlagen werden: YNAB weg")) "im Formular"
        }
        test "Nach dem Ändern steht die geänderte Regel an ihrer Stelle" {
            let changed = { r2 with Name = "Neu" }
            let saving = steps [ RuleChosen r2.Id; ChangeRequested InPanel ] shown
            let after = step (RuleChanged (InPanel, Ok changed)) saving
            match after.Rules with
            | Success [ _; second; _ ] -> Expect.equal second changed "ersetzt"
            | other -> failtest $"%A{other}"
            Expect.equal (panelOf after).Editor (Editing changed) "Panel zeigt sie"
        }
        test "Die Auswahl der Kategorie schließt nach der Wahl" {
            let opened = steps [ NewRuleRequested; CategoryPickerOpened ] shown
            Expect.isTrue (panelOf opened).PickerOpen "offen"
            let chosen = step (CategoryChosen (Some r2.CategoryId)) opened
            Expect.isFalse (panelOf chosen).PickerOpen "zu"
            Expect.equal (panelOf chosen).Draft.CategoryId (Some r2.CategoryId) "gewählt"
        }
        test "Der Name der gewählten Kategorie, ohne geladene Kategorien der gespeicherte der Regel" {
            let panel = panelOf (step (RuleChosen r1.Id) shown)
            Expect.equal (categoryValue [] panel) (Some "Kategorie 1") "gespeichert"
            let category = { Id = r1.CategoryId; Name = "Lebensmittel"; GroupName = "Alltag"; Available = None; Hidden = false }
            Expect.equal (categoryValue [ category ] panel) (Some "Lebensmittel") "geladen"
            Expect.equal (categoryValue [] (panelOf (step NewRuleRequested shown))) None "keine"
        }
    ]

[<Tests>]
let patternTestTests =
    testList "Regeln: Muster testen" [
        let typedA = steps [ NewRuleRequested; PatternEntered "rewe"; TestInputEntered "REWE Markt" ] shown
        let requestA = { Pattern = "rewe"; PatternType = Contains; TargetField = Combined; Input = "REWE Markt" }

        test "Ohne Text oder Muster gibt es nichts zu prüfen, mit beiden läuft eine Anfrage" {
            Expect.equal (panelOf (steps [ NewRuleRequested; TestInputEntered "x" ] shown)).Test NothingToTest "ohne Muster"
            Expect.equal (panelOf typedA).Test (Testing requestA) "läuft"
            Expect.equal (panelOf (step (PatternTested (requestA, Ok true)) typedA)).Test (Tested (requestA, Matches)) "passt"
            Expect.equal (panelOf (step (PatternTested (requestA, Ok false)) typedA)).Test (Tested (requestA, NoMatch)) "passt nicht"
        }
        test "Ein ungültiges Muster und ein Netzfehler sind nie Passt nicht" {
            let invalid = step (PatternTested (requestA, Error (RulesError.InvalidPattern ("rewe", "kaputt")))) typedA
            Expect.equal (panelOf invalid).Test (Tested (requestA, PatternFaulty "Ungültiges Muster 'rewe': kaputt")) "Grund"
            Expect.equal (panelOf (step (PatternTestLost requestA) typedA)).Test (Tested (requestA, Unreachable)) "Netzfehler"
        }
        // GIVEN eine Testanfrage für Eingabe A läuft WHEN Roman Muster, Art, Feld oder Text ändert und danach die
        // Antwort für A kommt THEN zeigt das Ergebnis nichts aus A.
        test "Eine Antwort für eine geänderte Eingabe zeigt nichts" {
            for change in [ PatternEntered "edeka"; PatternTypeChosen Exact; TargetFieldChosen Memo; TestInputEntered "EDEKA"; TestInputEntered "" ] do
                let changed = step change typedA
                for answer in [ PatternTested (requestA, Ok true); PatternTested (requestA, Ok false); PatternTestLost requestA ] do
                    let after = step answer changed
                    match (panelOf after).Test with
                    | Tested (request, _) | Testing request -> Expect.notEqual request requestA $"%A{change} / %A{answer}"
                    | NothingToTest -> ()
        }
    ]

[<Tests>]
let deleteTests =
    testList "Regeln: Löschen mit Rückgängig" [
        test "Regel löschen nimmt sie sofort aus der Liste und schließt das Panel" {
            let after = steps [ RuleChosen r2.Id; DeleteRequested ] shown
            Expect.equal (ids after) [ r1.Id; r3.Id ] "weg"
            Expect.isNone after.Panel "zu"
        }
        test "Ist sie gelöscht, kommt der Toast mit Rückgängig für ihre Stelle" {
            let deleted = steps [ RuleChosen r3.Id; DeleteRequested ] shown
            Expect.equal (stepExt (RuleDeleted (r3, 2, Ok ())) deleted) (RuleDeletedUndoable (r3, 2)) "Stelle 2"
        }
        test "Scheitert das Löschen, steht sie wieder an ihrer Stelle und ein Toast nennt es" {
            let deleted = steps [ RuleChosen r2.Id; DeleteRequested ] shown
            let back, _, ext = update (RuleDeleted (r2, 1, Error (RulesError.DatabaseError ("delete", "weg")))) deleted
            Expect.equal (ids back) [ r1.Id; r2.Id; r3.Id ] "zurück"
            Expect.isTrue (isToast ext) "Toast"
        }
        // GIVEN eine gelöschte Regel an Stelle 3 WHEN Roman „Rückgängig“ wählt THEN steht sie wieder an Stelle 3,
        // aus der Antwort des Servers; scheitert der Aufruf, fehlt sie und ein Toast nennt es.
        test "Rückgängig übernimmt die Liste des Servers, ein Fehler lässt die Regel weg" {
            let deleted = steps [ RuleChosen r3.Id; DeleteRequested ] shown
            let restoring, _, _ = update (RestoreRequested (r3, 2)) deleted
            Expect.equal (ids restoring) [ r1.Id; r2.Id ] "kein Zwischenstand"
            Expect.equal (ids (step (RuleRestored (Ok [ r1; r2; r3 ])) restoring)) [ r1.Id; r2.Id; r3.Id ] "an Stelle 3"
            let failed, _, ext = update (RuleRestored (Error (RulesError.DatabaseError ("restore_rule", "weg")))) restoring
            Expect.equal (ids failed) [ r1.Id; r2.Id ] "fehlt"
            Expect.equal ext (ShowToast ("Regel konnte nicht wiederhergestellt werden: Datenbankfehler bei restore_rule: weg", ToastError)) "Toast"
        }
        test "Rückgängig im Toast führt zur Wiederherstellung an der Stelle" {
            Expect.equal (State.toastActionMsg (UndoDeleteRule (r3, 2))) (State.RulesMsg (RestoreRequested (r3, 2))) "Nachricht"
        }
    ]

[<Tests>]
let keyTests =
    testList "Regeln: Tasten am PC" [
        let key k model = ruleKeyAction model Elsewhere false false k
        test "↓ ohne Auswahl öffnet die erste, ↑ die letzte Regel" {
            Expect.equal (key "ArrowDown" shown) (Some (ShowRule r1.Id)) "erste"
            Expect.equal (key "ArrowUp" shown) (Some (ShowRule r3.Id)) "letzte"
            Expect.equal (key "ArrowDown" (step NewRuleRequested shown)) (Some (ShowRule r1.Id)) "neue Regel zählt nicht als Auswahl"
        }
        test "Danach ↑↓ die nächste bzw. vorige, am Rand nichts" {
            let onSecond = step (RuleChosen r2.Id) shown
            Expect.equal (key "ArrowDown" onSecond) (Some (ShowRule r3.Id)) "nächste"
            Expect.equal (key "ArrowUp" onSecond) (Some (ShowRule r1.Id)) "vorige"
            Expect.equal (key "ArrowDown" (step (RuleChosen r3.Id) shown)) None "Rand unten"
            Expect.equal (key "ArrowUp" (step (RuleChosen r1.Id) shown)) None "Rand oben"
        }
        test "In einem Textfeld, mit Zusatztaste oder schon behandelt gehören die Pfeile nicht der Liste" {
            Expect.equal (ruleKeyAction shown InTextField false false "ArrowDown") None "Textfeld"
            Expect.equal (ruleKeyAction shown Elsewhere true false "ArrowDown") None "Zusatztaste"
            Expect.equal (ruleKeyAction shown Elsewhere false true "ArrowDown") None "behandelt"
        }
        test "Esc schließt zuerst die Auswahl der Kategorie, dann das Panel, ohne Panel nichts" {
            let opened = step (RuleChosen r1.Id) shown
            Expect.equal (key "Escape" (step CategoryPickerOpened opened)) (Some CloseRulePicker) "Picker"
            Expect.equal (ruleKeyAction opened InTextField false false "Escape") (Some CloseRulePanel) "Panel, auch aus dem Feld"
            Expect.equal (key "Escape" shown) None "nichts offen"
            Expect.equal (key "ArrowDown" (step CategoryPickerOpened opened)) None "Pfeile gehören dem Picker"
        }
    ]

/// Ein Root-Modell ohne `State.init()` (das liest die URL über `Browser.Dom.window`).
let private rootModel (page: Page) : State.Model =
    let settingsModel, _ = Components.Settings.State.init ()
    let ledgerModel, _ = Components.Ledger.State.init ()
    { CurrentPage = page
      Toasts = []
      Pwa = Pwa.initialState
      Settings = settingsModel
      Fetch = Components.Fetch.Types.init
      QuickAddCategories = NotAsked
      Rules = shown
      Ledger = ledgerModel
      Budget = Components.Budget.State.init ()
      QuickAdd = None
      QuickAddTemplates = NotAsked
      Overview = Components.Overview.Types.init
      SettingsReturn = Page.Overview }

let private dispatched (cmd: Elmish.Cmd<'msg>) : 'msg list =
    let collected = ResizeArray<'msg>()
    cmd |> List.iter (fun effect -> effect collected.Add)
    List.ofSeq collected

[<Tests>]
let rootTests =
    testList "Regeln: Tab im Kassenbuch" [
        // GIVEN Buchungen mit gewähltem Konto, gesetztem Filter und Auswahl WHEN Roman zu „Regeln“ und zurück zu
        // „Buchungen“ wechselt THEN stehen Konto, Filter und Auswahl wie vorher, die Liste lädt nicht neu (den
        // Scrollstand hält die Seite selbst).
        test "Der Wechsel zu Regeln und zurück lässt Konto, Filter, Auswahl und Liste stehen" {
            let account = Guid.NewGuid()
            let start = rootModel Page.Ledger
            let ledger =
                { start.Ledger with
                    Accounts = Success { Accounts = []; DefaultAccountId = None }
                    Entries = Success []
                    SelectedFilter = Some (Components.Ledger.Types.OneAccount account)
                    Selection = Components.Ledger.Types.Picked (Set.singleton account)
                    Filters = { start.Ledger.Filters with Memo = Some "rewe" } }
            let onBookings = { start with Ledger = ledger }
            let onRules, toRules = State.update (State.UrlChanged [ "ledger"; "rules" ]) onBookings
            let back, toBookings = State.update (State.UrlChanged [ "ledger" ]) onRules
            Expect.equal onRules.CurrentPage LedgerRules "Tab Regeln"
            Expect.equal back.Ledger ledger "Kassenbuch unverändert"
            Expect.isEmpty (dispatched toBookings |> List.filter (function State.LedgerMsg _ -> true | _ -> false)) "kein Neuladen"
            Expect.contains (dispatched toRules) (State.RulesMsg LoadRules) "Regeln laden"
            Expect.contains (dispatched toBookings) (State.RulesMsg RulesTabLeft) "Formular zu"
        }
        test "Ohne geladene Liste lädt der Wechsel zu Buchungen das Kassenbuch" {
            let _, cmd = State.update (State.UrlChanged [ "ledger" ]) (rootModel LedgerRules)
            Expect.contains (dispatched cmd) (State.LedgerMsg Components.Ledger.Types.LoadAccounts) "lädt"
        }
        test "Eine gelöschte Regel meldet der Root als Toast „Regel gelöscht“ mit „Rückgängig“" {
            let model = { rootModel LedgerRules with Rules = steps [ RuleChosen r2.Id; DeleteRequested ] shown }
            let _, cmd = State.update (State.RulesMsg (RuleDeleted (r2, 1, Ok ()))) model
            let expected =
                State.ShowToastWithAction ("Regel gelöscht", ToastSuccess, { Label = "Rückgängig"; Kind = UndoDeleteRule (r2, 1) })
            Expect.contains (dispatched cmd) expected "Toast"
        }
    ]
