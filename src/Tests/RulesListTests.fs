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
/// Die Nummer der laufenden Anfrage zum Speichern im offenen Formular.
let private requestOf (model: Model) =
    match (panelOf model).Save with
    | Saving request -> request
    | other -> failtest $"kein Speichern läuft: %A{other}"

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
            Expect.equal (testLine (Tested (request, TestFailed "Datenbank weg"))) (FaultLine "Muster konnte nicht geprüft werden: Datenbank weg") "Serverfehler"
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
        // Ein Neuladen, das scheitert, lässt die geladene Liste stehen und nennt den Fehler; ohne Liste steht der
        // Fehler in der Liste (mit „Erneut versuchen“), ein Toast dazu wäre doppelt. Eine überholte Antwort zählt nicht.
        test "Scheitert das Neuladen, bleibt die Liste stehen und ein Toast nennt es" {
            let reloading = step LoadRules shown
            let after, _, ext = update (RulesLoaded (reloading.LoadVersion, Error "x")) reloading
            Expect.equal after.Rules shown.Rules "Liste bleibt"
            Expect.equal ext (ShowToast ("Regeln konnten nicht neu geladen werden: x", ToastError)) "Toast"
            let first = step LoadRules (fst (init ()))
            let failed, _, none = update (RulesLoaded (first.LoadVersion, Error "x")) first
            Expect.equal failed.Rules (Failure "x") "ohne Liste gilt der Fehler"
            Expect.equal none NoOp "kein Toast"
            let stale, _, ignored = update (RulesLoaded (reloading.LoadVersion - 1, Error "x")) reloading
            Expect.equal stale.Rules shown.Rules "überholt"
            Expect.equal ignored NoOp "überholt meldet nichts"
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
            // Review-Fund: ein zweiter Klick auf „+ Neue Regel“ verwarf den Entwurf.
            let drafted = steps [ NewRuleRequested; PatternEntered "rewe"; NewRuleRequested ] shown
            Expect.equal (panelOf drafted).Draft.Pattern "rewe" "Entwurf bleibt"
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
            Expect.isTrue (SaveState.isSaving (panelOf (step (CreateRequested InPanel) filled)).Save) "Anlegen läuft"
            let editing = step (RuleChosen r1.Id) shown
            Expect.equal (panelOf (step (CreateRequested InPanel) editing)).Save NotSaving "Anlegen auf bestehender Regel"
            Expect.isTrue (SaveState.isSaving (panelOf (step (ChangeRequested InPanel) editing)).Save) "Ändern läuft"
        }
        test "Nach dem Anlegen steht die Regel vorn, das Panel zeigt sie, das Sheet schließt" {
            let created = { rule 9 true with Name = "Neu" }
            let saving = steps [ NewRuleRequested; PatternEntered "x"; CategoryChosen (Some r1.CategoryId); CreateRequested InPanel ] shown
            let inPanel, _, ext = update (RuleCreated (InPanel, requestOf saving, Ok created)) saving
            Expect.equal (ids inPanel) [ created.Id; r1.Id; r2.Id; r3.Id ] "vorn"
            Expect.equal (panelOf inPanel).Editor (Editing created) "Panel zeigt sie"
            Expect.equal ext (ShowToast ("Regel angelegt", ToastSuccess)) "Toast"
            Expect.isNone (step (RuleCreated (InSheet, requestOf saving, Ok created)) saving).Panel "Sheet zu"
        }
        test "Ein ungültiges Regex steht als Feldfehler am Muster, eine Eingabe nimmt ihn weg" {
            let saving = steps [ RuleChosen r1.Id; ChangeRequested InPanel ] shown
            let failed, _, ext = update (RuleChanged (InPanel, requestOf saving, Error (RulesError.InvalidPattern ("(", "Klammer fehlt")))) saving
            Expect.equal (panelOf failed).Save (SaveFailed (PatternRejected "Klammer fehlt")) "am Muster"
            Expect.equal ext NoOp "kein Toast"
            Expect.equal (panelOf (step (PatternEntered "(a)") failed)).Save NotSaving "weg"
            let other = step (RuleChanged (InPanel, requestOf saving, Error (RulesError.CategoryLookupFailed "YNAB weg"))) saving
            Expect.equal (panelOf other).Save (SaveFailed (SaveRejected "Kategorie konnte nicht nachgeschlagen werden: YNAB weg")) "im Formular"
        }
        test "Nach dem Ändern steht die geänderte Regel an ihrer Stelle" {
            let changed = { r2 with Name = "Neu" }
            let saving = steps [ RuleChosen r2.Id; ChangeRequested InPanel ] shown
            let after = step (RuleChanged (InPanel, requestOf saving, Ok changed)) saving
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
            // Review-Fund: jeder Fehler des Servers hieß „Muster ungültig“, auch ein Datenbankfehler.
            let failed = step (PatternTested (requestA, Error (RulesError.DatabaseError ("test", "weg")))) typedA
            Expect.equal (panelOf failed).Test (Tested (requestA, TestFailed "Datenbankfehler bei test: weg")) "Serverfehler"
        }
        // Review-Fund: nach einem Netz- oder Serverfehler stieß dieselbe Eingabe keinen neuen Test an. Ein Urteil
        // (Passt, Passt nicht, ungültig) bleibt dagegen für dieselbe Eingabe stehen.
        test "Nach einem Netz- oder Serverfehler prüft dieselbe Eingabe neu, nach einem Urteil nicht" {
            let again = TestInputEntered "REWE Markt"
            for answer in [ PatternTestLost requestA; PatternTested (requestA, Error (RulesError.DatabaseError ("test", "weg"))) ] do
                let _, cmd, _ = update again (step answer typedA)
                Expect.isNonEmpty cmd $"neu geprüft nach %A{answer}"
                Expect.equal (panelOf (step again (step answer typedA))).Test (Testing requestA) "läuft"
            for answer in [ PatternTested (requestA, Ok true); PatternTested (requestA, Ok false)
                            PatternTested (requestA, Error (RulesError.InvalidPattern ("rewe", "kaputt"))) ] do
                let _, cmd, _ = update again (step answer typedA)
                Expect.isEmpty cmd $"kein neuer Test nach %A{answer}"
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

// ============================================
// Speichern: die Antwort gehört zu ihrem Formular (Review-Fund categorization-n3sxw)
// ============================================

/// Was im Formular offen ist, wie die Aufzählung es kennt: eine neue Regel oder eine bestehende.
type private OpenKind =
    | NewKind
    | EditKind of Rule

/// Eine laufende Anfrage: ihre Nummer, das Formular, das sie schickte (`Gen`), Hülle und die Regel, die der Server
/// zurückgibt.
type private Flight = { Request: int; Gen: int; Shell: Shell; Saved: Rule; IsCreate: bool }

/// Der eigene Merker der Aufzählung. Jedes neu geöffnete Formular bekommt eine neue `Gen`; dieselbe Regel noch
/// einmal oder „Neue Regel“ auf einem offenen Entwurf bleibt dasselbe Formular.
type private SaveWalk = { Open: (int * OpenKind) option; NextGen: int; Flights: Flight list }

let private saveCategory = r1.CategoryId

/// Alle Ereignisse, die in diesem Zustand kommen können. Je Ereignis die Nachrichten und der Merker danach; die
/// Antworten prüft `checkAnswer`.
let private saveEvents (walk: SaveWalk) : (string * Msg list * (Model -> Model -> ExternalMsg -> SaveWalk)) list =
    let reopen (kind: OpenKind) =
        { walk with Open = Some (walk.NextGen, kind); NextGen = walk.NextGen + 1 }
    let choose (target: Rule) =
        match walk.Open with
        | Some (_, EditKind open') when open'.Id = target.Id -> walk
        | _ -> reopen (EditKind target)
    let unchanged = fun (_: Model) (_: Model) (_: ExternalMsg) -> walk
    [ "Regel 1 wählen", [ RuleChosen r1.Id ], (fun _ after _ ->
          let rule = after.Rules |> function Success rules -> rules |> List.find (fun r -> r.Id = r1.Id) | _ -> r1
          choose rule)
      "Regel 2 wählen", [ RuleChosen r2.Id ], (fun _ after _ ->
          let rule = after.Rules |> function Success rules -> rules |> List.find (fun r -> r.Id = r2.Id) | _ -> r2
          choose rule)
      "Neue Regel ausfüllen", [ NewRuleRequested; PatternEntered "neu"; CategoryChosen (Some saveCategory) ], (fun _ _ _ ->
          match walk.Open with
          | Some (_, NewKind) -> walk
          | _ -> reopen NewKind)
      "Tippen", [ NameEntered "Anders" ], unchanged
      "Esc", [ PanelClosed ], (fun _ _ _ -> { walk with Open = None })
      "Tab verlassen", [ RulesTabLeft ], (fun _ _ _ -> { walk with Open = None })
      for shell in [ InPanel; InSheet ] do
          let start =
              match walk.Open with
              | Some (_, EditKind _) -> ChangeRequested shell
              | _ -> CreateRequested shell
          $"Speichern %A{shell}", [ start ], (fun before after _ ->
              let busy = walk.Flights |> List.exists (fun f -> walk.Open |> Option.exists (fun (gen, _) -> gen = f.Gen))
              match walk.Open with
              | Some (gen, kind) when not busy ->
                  let request =
                      match after.Panel with
                      | Some { Save = Saving request } -> request
                      | other -> failtest $"Speichern startet nicht: %A{other}"
                  Expect.isFalse (walk.Flights |> List.exists (fun f -> f.Request = request)) "neue Nummer"
                  let saved, isCreate =
                      match kind with
                      | NewKind -> rule (100 + request) true, true
                      | EditKind rule -> { rule with Name = $"Gespeichert {request}" }, false
                  { walk with Flights = walk.Flights @ [ { Request = request; Gen = gen; Shell = shell; Saved = saved; IsCreate = isCreate } ] }
              | _ ->
                  Expect.equal after.Panel before.Panel "kein zweites Speichern"
                  walk) ]

/// Die Antwort auf `flight`, gelungen oder gescheitert, mit dem Orakel: sie gilt für das Formular nur, wenn es noch
/// dasselbe ist, das sie schickte.
let private checkAnswer (walk: SaveWalk) (flight: Flight) (ok: bool) (before: Model) (after: Model) (ext: ExternalMsg) : SaveWalk =
    let bound = walk.Open |> Option.exists (fun (gen, _) -> gen = flight.Gen)
    let rest = { walk with Flights = walk.Flights |> List.filter (fun f -> f.Request <> flight.Request) }
    if ok then
        match after.Rules with
        | Success rules -> Expect.contains rules flight.Saved "die gespeicherte Regel steht in der Liste"
        | other -> failtest $"%A{other}"
    match ok, bound with
    | true, true ->
        match flight.Shell with
        | InPanel ->
            Expect.equal (after.Panel |> Option.map (fun p -> p.Editor, p.Save)) (Some (Editing flight.Saved, NotSaving)) "Panel zeigt die Regel"
            { rest with Open = Some (rest.NextGen, EditKind flight.Saved); NextGen = rest.NextGen + 1 }
        | InSheet ->
            Expect.isNone after.Panel "Sheet zu"
            { rest with Open = None }
    | false, true ->
        let panel = after.Panel |> Option.defaultWith (fun () -> failtest "Formular weg")
        Expect.equal panel.Save (SaveFailed (SaveRejected "Kategorie konnte nicht nachgeschlagen werden: weg")) "Fehler am Formular"
        Expect.equal panel.Draft (panelOf before).Draft "Eingaben bleiben"
        Expect.equal ext NoOp "kein Toast"
        rest
    | _, false ->
        Expect.equal after.Panel before.Panel "ein anderes oder kein Formular bleibt, wie es ist"
        match ext with
        | ShowToast (_, kind) -> Expect.equal kind (if ok then ToastSuccess else ToastError) "Toast"
        | other -> failtest $"kein Toast: %A{other}"
        rest

let private answerMsg (flight: Flight) (ok: bool) : Msg =
    let result = if ok then Ok flight.Saved else Error (RulesError.CategoryLookupFailed "weg")
    if flight.IsCreate then RuleCreated (flight.Shell, flight.Request, result)
    else RuleChanged (flight.Shell, flight.Request, result)

[<Tests>]
let saveOrderTests =
    testList "Regeln: Speichern in jeder Reihenfolge" [
        // GIVEN Regel A speichert WHEN Roman währenddessen eine andere Regel wählt, eine neue anlegt, Esc drückt oder
        // den Tab verlässt THEN trifft die Antwort für A nicht das Formular, das jetzt offen ist: Eingaben in B
        // bleiben, ein Fehler von A erscheint als Toast, ein geschlossenes Panel bleibt zu (Review-Fund, vorher
        // schrieb jede Antwort in das offene Formular). Alle Reihenfolgen bis Tiefe 5 durch das echte `update`.
        test "Eine Antwort auf das Speichern trifft nur das Formular, das sie schickte" {
            let mutable unboundAnswers = 0
            let mutable boundAnswers = 0
            let rec walkFrom (model: Model) (walk: SaveWalk) (depth: int) (path: string list) =
                if depth > 0 then
                    for name, msgs, next in saveEvents walk do
                        let after, ext = msgs |> List.fold (fun (m, _) msg -> let m', _, e = update msg m in m', e) (model, NoOp)
                        let walk' = next model after ext
                        walkFrom after walk' (depth - 1) (name :: path)
                    for flight in walk.Flights do
                        for ok in [ true; false ] do
                            let after, _, ext = update (answerMsg flight ok) model
                            let bound = walk.Open |> Option.exists (fun (gen, _) -> gen = flight.Gen)
                            if bound then boundAnswers <- boundAnswers + 1 else unboundAnswers <- unboundAnswers + 1
                            let name = $"Antwort {flight.Request} %b{ok}"
                            let walk' =
                                try checkAnswer walk flight ok model after ext
                                with e -> failtest $"{e.Message}\nWeg: %A{List.rev (name :: path)}"
                            walkFrom after walk' (depth - 1) (name :: path)
            walkFrom shown { Open = None; NextGen = 0; Flights = [] } 5 []
            Expect.isGreaterThan unboundAnswers 1000 "Antworten auf ein anderes Formular"
            Expect.isGreaterThan boundAnswers 1000 "Antworten auf dasselbe Formular"
        }
    ]

// ============================================
// Reihenfolge: eine Umordnung zugleich (Review-Fund categorization-n3sxw)
// ============================================

/// Ersatz für die Aufrufe: hält fest, was hinausgeht, und antwortet sofort.
let private recordingPorts (calls: System.Collections.Concurrent.ConcurrentQueue<string * RuleId list>) : RulesPorts =
    { livePorts with
        GetAll = fun () -> async { calls.Enqueue ("laden", []); return [ r1; r2; r3 ] }
        Reorder = fun ids -> async { calls.Enqueue ("ordnen", ids); return Ok () } }

/// Führt `update` mit dem Ersatz aus und gibt die Aufrufe zurück, die das Cmd macht (jeder antwortet genau einmal).
let private stepWithCalls (msg: Msg) (model: Model) : Model * ExternalMsg * (string * RuleId list) list =
    let calls = System.Collections.Concurrent.ConcurrentQueue<string * RuleId list>()
    let model', cmd, ext = updateWith (recordingPorts calls) msg model
    let answered = new System.Collections.Concurrent.BlockingCollection<Msg>()
    cmd |> List.iter (fun effect -> effect answered.Add)
    for _ in cmd do
        let mutable m = Unchecked.defaultof<Msg>
        if not (answered.TryTake(&m, 5000)) then failtest "Aufruf ohne Antwort"
    model', ext, (List.ofSeq calls |> List.sort)

/// Die Reihenfolge nach einem Pfeil, unabhängig von `reorderedIds` gerechnet.
let private swapped (order: RuleId list) (id: RuleId) (up: bool) : RuleId list =
    let arr = List.toArray order
    let i = Array.findIndex ((=) id) arr
    let j = if up then i - 1 else i + 1
    if j < 0 || j >= arr.Length then order
    else
        let copy = Array.copy arr
        copy.[i] <- arr.[j]
        copy.[j] <- arr.[i]
        List.ofArray copy

/// Der Merker der Aufzählung: was die Liste zeigt, was unterwegs ist, ob danach noch einmal geschickt wird.
type private ReorderWalk = { Shown: RuleId list; InFlight: RuleId list option; Again: bool }

[<Tests>]
let reorderOrderTests =
    testList "Regeln: Reihenfolge in jeder Reihenfolge" [
        // GIVEN eine Umordnung ist unterwegs WHEN Roman weitere Pfeile tippt und Antworten oder Fehler kommen THEN ist
        // höchstens eine `reorderRules` zugleich unterwegs, nach ihrer Antwort geht einmal der neueste Stand hinaus,
        // und ein Fehler lädt die Liste neu, statt einen gemerkten Stand zurückzuschreiben (Review-Fund: parallele
        // Aufrufe konnten vertauscht ankommen, ein Fehler setzte blind zurück). Alle Reihenfolgen bis Tiefe 5.
        test "Höchstens eine Umordnung ist unterwegs, danach geht der neueste Stand hinaus, ein Fehler lädt neu" {
            let moves = [ r2.Id, true; r1.Id, false; r3.Id, true; r1.Id, true ]
            let mutable answers = 0
            let rec walkFrom (model: Model) (walk: ReorderWalk) (depth: int) (path: string list) =
                if depth > 0 then
                    let events =
                        [ for id, up in moves do
                              yield $"Pfeil %A{id} %b{up}", MoveRequested (id, (if up then Up else Down))
                          if walk.InFlight.IsSome then
                              yield "Antwort", RulesReordered (Ok ())
                              yield "Fehler", RulesReordered (Error "weg") ]
                    for name, msg in events do
                        let path' = name :: path
                        let where = $"Weg: %A{List.rev path'}"
                        let after, ext, calls = stepWithCalls msg model
                        let walk', expected, toast =
                            match msg with
                            | MoveRequested (id, direction) ->
                                let order = swapped walk.Shown id (direction = Up)
                                if order = walk.Shown then walk, [], false
                                elif walk.InFlight.IsNone then { walk with Shown = order; InFlight = Some order }, [ "ordnen", order ], false
                                else { walk with Shown = order; Again = true }, [], false
                            | RulesReordered (Ok ()) when walk.Again ->
                                { walk with InFlight = Some walk.Shown; Again = false }, [ "ordnen", walk.Shown ], false
                            | RulesReordered (Ok ()) -> { walk with InFlight = None }, [], false
                            | _ -> { walk with InFlight = None; Again = false }, [ "laden", [] ], true
                        Expect.equal calls (List.sort expected) $"Aufrufe. {where}"
                        Expect.equal (ids after) walk'.Shown $"Liste. {where}"
                        Expect.equal (match ext with ShowToast (_, ToastError) -> true | _ -> false) toast $"Toast. {where}"
                        answers <- answers + 1
                        walkFrom after walk' (depth - 1) path'
            let start = [ r1.Id; r2.Id; r3.Id ]
            walkFrom shown { Shown = start; InFlight = None; Again = false } 5 []
            Expect.isGreaterThan answers 1000 "Schritte"
        }
    ]

// ============================================
// Kategorien für die Auswahl (Review-Fund categorization-n3sxw)
// ============================================

[<Tests>]
let categoryTests =
    testList "Regeln: Kategorien für die Auswahl" [
        // Vorher hieß eine leere Liste lädt, gescheitert und kein Budget zugleich: nach einem Fehler zeigte die
        // Auswahl ewig „Kategorien werden geladen“, „Erneut versuchen“ tat nichts.
        test "Ein Ladefehler steht als Fehler in der Auswahl, nicht als Laden" {
            let failed, _, ext = update (CategoriesLoaded (Error (YnabError.NetworkError "weg"))) { shown with Categories = Loading }
            Expect.isTrue (RemoteData.isFailure failed.Categories) "Fehler"
            Expect.isTrue (isToast ext) "Toast"
            match categoryLoadState failed.Categories with
            | Client.DesignSystem.CategoryPicker.PickerFailed (Some _) -> ()
            | other -> failtest $"%A{other}"
            Expect.equal (categoryLoadState Loading) Client.DesignSystem.CategoryPicker.PickerLoading "lädt"
            Expect.equal (categoryLoadState (Success [])) Client.DesignSystem.CategoryPicker.PickerReady "da"
        }
        test "Ein spätes Scheitern ersetzt geladene Kategorien nicht" {
            let kept, _, _ = update (CategoriesLoaded (Error (YnabError.NetworkError "weg"))) { shown with Categories = Success [] }
            Expect.equal kept.Categories (Success []) "bleiben"
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
      Rules = shown
      Ledger = ledgerModel
      Budget = Components.Budget.State.init ()
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
        // Review-Fund: die Kategorien luden bei jedem Wechsel auf den Tab neu, und nach einem Fehler gab es kein
        // Neuladen. Jetzt einmal; nach einem Fehler lädt „Erneut versuchen“ (dieselbe Nachricht) neu.
        test "Die Kategorien laden einmal, nach einem Fehler noch einmal, ohne Budget steht der Grund da" {
            let withBudget =
                { HasToken = true; DefaultBudgetId = Some (YnabBudgetId "budget"); DefaultAccountId = None; QuickAddAccountId = None }
            let settingsOf ynab : AppSettingsView =
                { Comdirect = None; Ynab = ynab; Sync = { DaysToFetch = 30; AutoFetchEnabled = true; AutoFetchIntervalMinutes = 360 } }
            let onRules (settings: RemoteData<AppSettingsView>) (categories: RemoteData<YnabCategory list>) =
                let root = rootModel LedgerRules
                { root with Settings = { root.Settings with Settings = settings }; Rules = { root.Rules with Categories = categories } }
            let load model = State.update (State.RulesMsg LoadCategories) model
            for categories in [ NotAsked; Failure "weg" ] do
                let loading, cmd = load (onRules (Success (settingsOf (Some withBudget))) categories)
                Expect.equal loading.Rules.Categories Loading $"lädt aus %A{categories}"
                Expect.isNonEmpty cmd "Aufruf"
            for categories in [ Loading; Success [] ] do
                let model = onRules (Success (settingsOf (Some withBudget))) categories
                let same, cmd = load model
                Expect.equal same.Rules.Categories categories $"kein zweites Laden aus %A{categories}"
                Expect.isEmpty cmd "kein Aufruf"
            let noBudget, _ = load (onRules (Success (settingsOf None)) NotAsked)
            Expect.isTrue (RemoteData.isFailure noBudget.Rules.Categories) "ohne YNAB ein Fehler mit Grund"
            let noSettings, cmd = load (onRules (Failure "x") NotAsked)
            Expect.isTrue (RemoteData.isFailure noSettings.Rules.Categories) "ohne Settings ein Fehler"
            Expect.contains (dispatched cmd) (State.SettingsMsg Components.Settings.Types.LoadSettings) "Settings neu"
        }
        test "Eine gelöschte Regel meldet der Root als Toast „Regel gelöscht“ mit „Rückgängig“" {
            let model = { rootModel LedgerRules with Rules = steps [ RuleChosen r2.Id; DeleteRequested ] shown }
            let _, cmd = State.update (State.RulesMsg (RuleDeleted (r2, 1, Ok ()))) model
            let expected =
                State.ShowToastWithAction ("Regel gelöscht", ToastSuccess, { Label = "Rückgängig"; Kind = UndoDeleteRule (r2, 1) })
            Expect.contains (dispatched cmd) expected "Toast"
        }
    ]
