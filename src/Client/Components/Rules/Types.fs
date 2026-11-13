/// Der Tab „Regeln“ im Kassenbuch (categorization-n3sxw, ADR 0087 Punkte 9 und 16): die Regeln nach Priorität,
/// eine Regel öffnet am PC im Panel, am Handy im Sheet. Regeln sind Zustand, keine Ereignisse (ADR 0083).
module Components.Rules.Types

open Shared.Domain
open Types

/// Was das Formular bearbeitet: eine neue Regel oder eine bestehende.
type Editor =
    | NewRule
    | Editing of Rule

/// Die Eingaben im Formular. `CategoryId = None`: noch keine Kategorie gewählt. `TestInput` ist der Text unter
/// „Muster testen“, er wird nicht gespeichert.
type RuleDraft = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId option
    Enabled: bool
    TestInput: string
}

module RuleDraft =
    /// Eine neue Regel: Enthält, in Payee & Memo, aktiv.
    let empty : RuleDraft = {
        Name = ""
        Pattern = ""
        PatternType = Contains
        TargetField = Combined
        CategoryId = None
        Enabled = true
        TestInput = ""
    }

    let fromRule (rule: Rule) : RuleDraft = {
        Name = rule.Name
        Pattern = rule.Pattern
        PatternType = rule.PatternType
        TargetField = rule.TargetField
        CategoryId = Some rule.CategoryId
        Enabled = rule.Enabled
        TestInput = ""
    }

/// Eine Anfrage an `testRule`. Die Antwort trägt sie mit: passt sie nicht mehr zur Eingabe, zählt sie nicht.
type TestRequest = {
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    Input: string
}

/// Das Urteil des Servers über das Muster.
type TestVerdict =
    | Matches
    | NoMatch
    /// Das Muster gilt nicht (etwa ein ungültiges Regex): der Grund in Worten, kein „Passt nicht“.
    | PatternFaulty of reason: string
    /// Der Server hat nicht geantwortet: kein Urteil über das Muster.
    | Unreachable

/// „Muster testen“: ohne Text und Muster gibt es nichts zu prüfen, sonst läuft eine Anfrage oder ihr Urteil steht da.
type PatternTest =
    | NothingToTest
    | Testing of TestRequest
    | Tested of TestRequest * TestVerdict

/// Warum das Speichern scheiterte. Ein ungültiges Muster steht am Feld Muster, alles andere im Formular.
type SaveFailure =
    | PatternRejected of reason: string
    | SaveRejected of message: string

type SaveState =
    | NotSaving
    | Saving
    | SaveFailed of SaveFailure

/// Die Hülle, in der das Formular steht: am PC das Panel, das nach dem Speichern die Regel zeigt (§7.6), am Handy
/// das Sheet, das schließt.
type Shell =
    | InPanel
    | InSheet

/// Das offene Formular, am PC im Panel, am Handy im Sheet.
type Panel = {
    Editor: Editor
    Draft: RuleDraft
    Save: SaveState
    Test: PatternTest
    /// Ist die Auswahl der Kategorie offen? Am PC ein Popover, am Handy ersetzt sie den Inhalt des Sheets (§7.15).
    PickerOpen: bool
}

/// Richtung eines Pfeils: `Up` lässt die Regel früher gewinnen, `Down` später.
type MoveDirection =
    | Up
    | Down

/// Die Liste aller Regel-Ids nach dem Verschieben von `ruleId` um eine Stelle. Am Rand und bei unbekannter Id
/// bleibt die Reihenfolge, wie sie ist. Das Ergebnis ist immer eine Umordnung der Eingabe, so geht es an `reorderRules`.
let reorderedIds (direction: MoveDirection) (ruleId: RuleId) (rules: Rule list) : RuleId list =
    let ids = rules |> List.map (fun r -> r.Id)
    match List.tryFindIndex (fun (r: Rule) -> r.Id = ruleId) rules with
    | None -> ids
    | Some index ->
        let swapWith =
            match direction with
            | Up -> index - 1
            | Down -> index + 1
        if swapWith < 0 || swapWith >= List.length ids then
            ids
        else
            ids
            |> List.mapi (fun i id ->
                if i = index then ids.[swapWith]
                elif i = swapWith then ids.[index]
                else id)

type Model = {
    Rules: RemoteData<Rule list>
    /// Zählt jedes Laden und jede Änderung an der Liste. Eine Ladeantwort mit älterer Version ist überholt und
    /// ersetzt die Liste nicht (Befund `State.fs:52` aus categorization-krscs).
    LoadVersion: int
    Categories: YnabCategory list
    Panel: Panel option
}

type Msg =
    | LoadRules
    | RulesLoaded of version: int * Result<Rule list, string>
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, YnabError>

    // Liste
    | NewRuleRequested
    | RuleChosen of RuleId
    | PanelClosed
    | MoveRequested of RuleId * MoveDirection
    /// `previous`: die Reihenfolge vor dem Verschieben, sie steht nach einem Fehler wieder da.
    | RulesReordered of previous: Rule list * Result<unit, string>
    /// Der Tab „Regeln“ ist verlassen: ein offenes Formular schließt.
    | RulesTabLeft

    // Formular
    | NameEntered of string
    | PatternEntered of string
    | PatternTypeChosen of PatternType
    | TargetFieldChosen of TargetField
    | CategoryPickerOpened
    | CategoryPickerClosed
    | CategoryChosen of YnabCategoryId option
    | EnabledSwitched of bool
    | TestInputEntered of string
    | PatternTested of TestRequest * Result<bool, RulesError>
    | PatternTestLost of TestRequest
    | CreateRequested of Shell
    | RuleCreated of Shell * Result<Rule, RulesError>
    | ChangeRequested of Shell
    | RuleChanged of Shell * Result<Rule, RulesError>

    // Löschen mit Rückgängig
    | DeleteRequested
    | RuleDeleted of Rule * position: int * Result<unit, RulesError>
    | RestoreRequested of Rule * position: int
    | RuleRestored of Result<Rule list, RulesError>

/// Was der Regel-Tab der Seite meldet.
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    /// Die Regel ist gelöscht: Toast „Regel gelöscht“ mit „Rückgängig“, das sie an `position` zurückbringt.
    | RuleDeletedUndoable of Rule * position: int
