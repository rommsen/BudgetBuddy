/// Der Tab „Regeln“ im Kassenbuch (categorization-n3sxw, ADR 0087 Punkte 9 und 16): die Regeln nach Priorität,
/// eine Regel öffnet am PC im Panel, am Handy im Sheet. Regeln sind Zustand, keine Ereignisse (ADR 0083).
module Components.Rules.Types

open System
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

    /// Eine neue Regel aus einer Buchung (ledger-hg5m5): Name und Muster sind der Payee, Enthält, gesucht im
    /// Payee, dazu die Kategorie der Buchung, falls sie eine eindeutige hat.
    let fromEntry (payee: string) (categoryId: YnabCategoryId option) : RuleDraft =
        let payee = payee.Trim()
        { empty with
            Name = payee
            Pattern = payee
            PatternType = Contains
            TargetField = Payee
            CategoryId = categoryId }

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
    /// Der Server hat geantwortet, konnte aber nicht prüfen (etwa ein Datenbankfehler): kein Urteil über das Muster.
    | TestFailed of message: string

/// „Muster testen“: ohne Text und Muster gibt es nichts zu prüfen, sonst läuft eine Anfrage oder ihr Urteil steht da.
type PatternTest =
    | NothingToTest
    | Testing of TestRequest
    | Tested of TestRequest * TestVerdict

/// Warum das Speichern scheiterte. Ein ungültiges Muster steht am Feld Muster, alles andere im Formular.
type SaveFailure =
    | PatternRejected of reason: string
    | SaveRejected of message: string

/// `Saving request`: die Anfrage mit dieser Nummer läuft. Ihre Antwort gilt nur für das Formular, das sie noch trägt;
/// ist es inzwischen geschlossen oder ein anderes, zieht sie nur die Liste nach.
type SaveState =
    | NotSaving
    | Saving of request: int
    | SaveFailed of SaveFailure

module SaveState =
    let isSaving (save: SaveState) = match save with Saving _ -> true | _ -> false

/// Die Hülle, in der das Formular steht: am PC das Panel, das nach dem Speichern die Regel zeigt (§7.6), am Handy
/// das Sheet, das schließt.
type Shell =
    | InPanel
    | InSheet

/// Woher das offene Formular kommt: aus dem Tab „Regeln“ oder aus einer Buchung im Kassenbuch (ledger-hg5m5).
/// Aus einer Buchung steht es dort, wo die Buchung steht (Detail oder Durchgehen), und führt zu ihr zurück.
type PanelOrigin =
    | FromRulesTab
    | FromEntry of entryId: Guid

/// Das offene Formular, am PC im Panel, am Handy im Sheet.
type Panel = {
    Editor: Editor
    Draft: RuleDraft
    Save: SaveState
    Test: PatternTest
    /// Ist die Auswahl der Kategorie offen? Am PC ein Popover, am Handy ersetzt sie den Inhalt des Sheets (§7.15).
    PickerOpen: bool
    Origin: PanelOrigin
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

/// Das Speichern der Reihenfolge. Es läuft höchstens ein `reorderRules` zugleich, sonst könnten zwei Antworten
/// vertauscht ankommen. Kommt währenddessen ein weiterer Pfeil, wird danach einmal der neueste Stand geschickt.
type ReorderState =
    | ReorderIdle
    /// `sent`: die geschickte Reihenfolge. `again`: die Liste hat sich seitdem geändert.
    | ReorderSending of sent: RuleId list * again: bool

/// Wie weit die gelöschte Regel ist (Regel R3, ADR 0098): `deleteRule` läuft, sie ist gelöscht, `restoreRule` läuft
/// oder das Wiederherstellen ist gescheitert (mit Grund).
type RemovedPhase =
    | Removing
    | Removed
    | Restoring
    | RestoreFailed of reason: string

/// Die gelöschte Regel hinterlässt an ihrer Stelle eine Zeile „„Name“ gelöscht“ mit „Rückgängig“, bis zur nächsten
/// Aktion auf dem Tab. `Position` ist ihre Stelle in der Liste, an die `restoreRule` sie zurückbringt.
type RemovedRule = { Rule: Rule; Position: int; Phase: RemovedPhase }

/// Ein Fehler an einer Regel, rot unter ihr bis zur nächsten Aktion (etwa „Nicht gelöscht: …“). Steht die Regel nicht
/// (mehr) in der Liste, steht der Satz über der Liste.
type RuleFault = { RuleId: RuleId option; Text: string }

type Model = {
    Rules: RemoteData<Rule list>
    /// Zählt jedes Laden und jede Änderung an der Liste. Eine Ladeantwort mit älterer Version ist überholt und
    /// ersetzt die Liste nicht (Befund `State.fs:52` aus categorization-krscs).
    LoadVersion: int
    /// Die Kategorien für die Auswahl. Sie lädt der Root mit dem konfigurierten Budget, einmal je Sitzung.
    Categories: RemoteData<YnabCategory list>
    Panel: Panel option
    /// Zählt die Anfragen zum Speichern; die Nummer bindet die Antwort an ihr Formular.
    SaveRequests: int
    Reorder: ReorderState
    /// Die Zeile „gelöscht“ am Platz, höchstens eine: ein zweites Löschen ersetzt sie.
    Removed: RemovedRule option
    /// Ein Fehler an einer Regel, bis zur nächsten Aktion (`isNextAction`).
    Fault: RuleFault option
    /// Das Neuladen ist gescheitert, die Liste steht noch: der Grund für den Hinweis über der Liste, bis ein Laden klappt.
    StaleLoad: string option
    /// Die Regel, die gerade kurz leuchtet (nach Speichern, Anlegen oder Wiederherstellen).
    Flashed: RuleId option
    /// Die zuletzt verschobene Regel: unter ihr steht ein Fehler beim Speichern der Reihenfolge.
    LastMoved: RuleId option
}

type Msg =
    | LoadRules
    | RulesLoaded of version: int * Result<Rule list, string>
    | LoadCategories
    | CategoriesLoaded of Result<YnabCategory list, YnabError>

    // Liste
    | NewRuleRequested
    /// „Regel anlegen“ an einer Buchung: das Formular, vorbelegt aus Payee und Kategorie der Buchung (ledger-hg5m5).
    | EntryRuleRequested of entryId: Guid * payee: string * categoryId: YnabCategoryId option
    | RuleChosen of RuleId
    | PanelClosed
    | MoveRequested of RuleId * MoveDirection
    /// Scheitert das Speichern, lädt die Liste neu: der Server kennt die gültige Reihenfolge.
    | RulesReordered of Result<unit, string>
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
    /// `origin`: woher das Formular kam. Aus einer Buchung ist die Regel nicht zu sehen, die Meldung geht an die Seite.
    | RuleCreated of Shell * origin: PanelOrigin * request: int * Result<Rule, RulesError>
    | ChangeRequested of Shell
    /// `ruleId`: die geänderte Regel. Ist ihr Formular schon zu, steht ein Fehler unter ihr.
    | RuleChanged of Shell * ruleId: RuleId * request: int * Result<Rule, RulesError>
    /// Das kurze Leuchten der Regel ist vorbei.
    | FlashEnded of RuleId

    // Löschen mit Rückgängig (Zeile „gelöscht“ am Platz)
    | DeleteRequested
    | RuleDeleted of Rule * position: int * Result<unit, RulesError>
    /// „Rückgängig“ bzw. „Erneut“ an der Zeile „gelöscht“: die Regel aus `Model.Removed` kommt an ihre Stelle zurück.
    | RestoreRequested
    | RuleRestored of Rule * Result<Rule list, RulesError>

/// Was der Regel-Tab der Seite meldet. Alles, was man in der Liste sieht, meldet der Tab dort selbst (Regel R3,
/// ADR 0098); an die Seite geht nur, was keinen Ort in der Liste hat (eine Regel aus einer Buchung, eine neue Regel
/// mit geschlossenem Formular).
type ExternalMsg =
    | NoOp
    /// Eine Meldung für den Meldungsplatz der Seite: Ton, Aktion (nie gekürzt), darunter Name oder Grund.
    | ShowFeedback of FeedbackTone * action: string * detail: string option
