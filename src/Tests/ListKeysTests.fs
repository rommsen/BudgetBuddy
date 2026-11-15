module ListKeysTests

// design-system-gcpws: die eine Tastenregel für alle Listen mit Panel (`ListKeys.actionFor`). Statt Fall für Fall
// zählt der Test alle Kombinationen aus Fokus, Zusatztasten, Ebene, Auswahl, Panel, Fähigkeiten der Liste und Taste
// auf und vergleicht mit einem Orakel, das Styleguide §7.8 Punkt für Punkt nachschreibt (Doktrin Punkt 20, Vorbild
// `moveWays`). Die Seitentests (Kassenbuch, Regeln, Budget, Kategorien, Targets prüfen) nutzen dasselbe Orakel.

open Expecto
open Client.DesignSystem.ListKeys

/// Styleguide §7.8, unabhängig vom getesteten Code geschrieben.
let oracle (traits: Traits) (s: Situation) : Action =
    let vertical = s.Key = "ArrowDown" || s.Key = "ArrowUp"
    let horizontal = s.Key = "ArrowLeft" || s.Key = "ArrowRight"
    let direction = if s.Key = "ArrowDown" || s.Key = "ArrowRight" then Forward else Backward
    // Punkt 4: hat ein Baustein die Taste genommen, reagiert die Seite nicht.
    if s.Handled then Leave
    // Punkt 6: ein offener Dialog, Picker oder ein Sheet bekommt die Tasten zuerst, die Liste reagiert nicht.
    elif s.LayerOpen then Leave
    // Punkt 3: mit Alt, Strg oder Cmd gehört die Taste dem Browser.
    elif s.Command then Leave
    // Punkt 6 (ADR 0096): Esc ohne Ebene schließt zuerst das Panel, danach hebt es die Auswahl auf, aus jedem Fokus.
    elif s.Key = "Escape" then
        if s.PanelOpen then ClosePanel
        elif s.SelectionActive then ClearSelection
        else Leave
    // Punkt 2: in jedem anderen Feld gehören die Pfeile dem Cursor.
    elif (vertical || horizontal) && s.Focus = InTextField then Leave
    // Punkt 3: Umschalt+↑↓ erweitert in Listen mit Mehrfachauswahl die Auswahl, sonst markiert es im Feld.
    elif s.Shift && vertical then (if traits.MultiSelect then ExtendSelection direction else Leave)
    elif s.Shift && horizontal then Leave
    // Punkt 1: ↑↓ wechseln die Zeile, auch aus der Suche; ohne Auswahl ↓ die erste, ↑ die letzte.
    elif vertical then
        if s.RowSelected then StepRow direction
        elif direction = Forward then FirstRow
        else LastRow
    // Punkt 1: ←→ wechseln den Tab im Panel; ohne Auswahl bleiben sie beim Cursor.
    elif horizontal then (if traits.Tabs && s.RowSelected then StepTab direction else Leave)
    // Punkt 5: ein druckbares Zeichen ohne Feldfokus geht in die Suche, die Leertaste nicht. Ausnahme Kassenbuch
    // (F9): dort sind Buchstaben belegt, nur „/“ springt in die Suche.
    elif s.Key.Length = 1 && s.Key <> " " && s.Focus = Elsewhere && traits.Search then
        if traits.TypeToSearch then TypeIntoSearch
        elif s.Key = "/" then FocusSearch
        else Leave
    else Leave

let keys = [ "ArrowDown"; "ArrowUp"; "ArrowLeft"; "ArrowRight"; "Escape"; "/"; "a"; "Ö"; "7"; " "; "Enter"; "Tab"; "Backspace"; "F5" ]
let focuses = [ InSearch; InTextField; Elsewhere ]
let bools = [ false; true ]

/// Jede Lage einer Taste.
let situations : Situation list =
    [ for key in keys do
        for focus in focuses do
            for command in bools do
                for shift in bools do
                    for handled in bools do
                        for layer in bools do
                            for row in bools do
                                for panel in bools do
                                    for selection in bools do
                                        { Key = key
                                          Focus = focus
                                          Command = command
                                          Shift = shift
                                          Handled = handled
                                          LayerOpen = layer
                                          RowSelected = row
                                          PanelOpen = panel
                                          SelectionActive = selection } ]

let allTraits : Traits list =
    [ for multi in bools do
        for tabs in bools do
            for search in bools do
                for typing in bools do
                    { MultiSelect = multi; Tabs = tabs; Search = search; TypeToSearch = typing } ]

[<Tests>]
let tests =
    testList "Tastenregel aller Listen mit Panel (§7.8)" [
        test "Jede Kombination aus Liste, Fokus, Zusatztasten, Ebene, Auswahl, Panel und Taste folgt §7.8" {
            let mismatches =
                [ for traits in allTraits do
                    for s in situations do
                        let actual = actionFor traits s
                        let expected = oracle traits s
                        if actual <> expected then traits, s, actual, expected ]
            Expect.equal (List.length situations * List.length allTraits) 86016 "alle Kombinationen"
            Expect.isEmpty (List.truncate 5 mismatches) $"{List.length mismatches} Abweichungen"
        }
        test "Die Aufzählung erreicht jede Handlung" {
            let reached =
                [ for traits in allTraits do
                    for s in situations -> actionFor traits s ]
                |> Set.ofList
            let all =
                Set.ofList
                    [ FirstRow; LastRow; StepRow Forward; StepRow Backward; StepTab Forward; StepTab Backward
                      ExtendSelection Forward; ExtendSelection Backward; ClosePanel; ClearSelection; FocusSearch
                      TypeIntoSearch; Leave ]
            Expect.equal reached all "jede Handlung kommt vor"
        }
        test "Esc schließt erst das Panel, die Auswahl bleibt, dann die Auswahl (ADR 0096)" {
            Expect.equal (escapeStep true true) ClosePanel "Panel zuerst"
            Expect.equal (escapeStep false true) ClearSelection "dann die Auswahl"
            Expect.equal (escapeStep false false) Leave "sonst nichts"
        }
    ]

// design-system-gcpws (§7.9, F7): die Pfeile und Enter der Auswahl-Dialoge, die dieser Schnitt nachzieht.
[<Tests>]
let dialogTests =
    testList "Auswahl-Dialoge: Pfeile und Enter (§7.9)" [
        test "Kategorie-Filter: ↓ ohne Markierung die erste, ↑ die letzte, am Rand stehen bleiben, Enter schaltet die markierte" {
            let key = Client.DesignSystem.CategoryMultiSelect.keyAction
            Expect.equal (key "ArrowDown" 3 None) (Client.DesignSystem.CategoryMultiSelect.MarkRow 0) "erste"
            Expect.equal (key "ArrowUp" 3 None) (Client.DesignSystem.CategoryMultiSelect.MarkRow 2) "letzte"
            Expect.equal (key "ArrowDown" 3 (Some 2)) (Client.DesignSystem.CategoryMultiSelect.MarkRow 2) "Rand unten"
            Expect.equal (key "ArrowUp" 3 (Some 0)) (Client.DesignSystem.CategoryMultiSelect.MarkRow 0) "Rand oben"
            Expect.equal (key "ArrowDown" 3 (Some 0)) (Client.DesignSystem.CategoryMultiSelect.MarkRow 1) "nächste"
            Expect.equal (key "Enter" 3 (Some 1)) (Client.DesignSystem.CategoryMultiSelect.ToggleRow 1) "Enter"
            Expect.equal (key "Enter" 3 None) Client.DesignSystem.CategoryMultiSelect.KeepKey "Enter ohne Markierung"
            Expect.equal (key "ArrowDown" 0 None) Client.DesignSystem.CategoryMultiSelect.KeepKey "leere Liste"
            Expect.equal (key "a" 3 (Some 1)) Client.DesignSystem.CategoryMultiSelect.KeepKey "Zeichen gehören der Suche"
        }
        test "Kurze Auswahl ohne Suche: ↑↓ bewegen den Fokus, am Rand bleibt er, ohne Fokus ↓ die erste und ↑ die letzte" {
            let target = Client.DesignSystem.PickerParts.shortChoiceTarget
            Expect.equal (target "ArrowDown" 3 (Some 0)) (Some 1) "nächste"
            Expect.equal (target "ArrowUp" 3 (Some 1)) (Some 0) "vorige"
            Expect.equal (target "ArrowDown" 3 (Some 2)) (Some 2) "Rand unten"
            Expect.equal (target "ArrowUp" 3 (Some 0)) (Some 0) "Rand oben"
            Expect.equal (target "ArrowDown" 3 None) (Some 0) "ohne Fokus ↓"
            Expect.equal (target "ArrowUp" 3 None) (Some 2) "ohne Fokus ↑"
            Expect.equal (target "Enter" 3 (Some 1)) None "Enter wählt über den Knopf"
            Expect.equal (target "ArrowDown" 0 None) None "leere Liste"
        }
    ]
