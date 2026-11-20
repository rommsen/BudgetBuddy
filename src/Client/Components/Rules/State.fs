module Components.Rules.State

open System
open Elmish
open Client
open Components.Rules.Types
open Types
open Shared.Domain

module ListKeys = Client.DesignSystem.ListKeys

let init () : Model * Cmd<Msg> =
    { Rules = NotAsked
      LoadVersion = 0
      Categories = NotAsked
      Panel = None
      SaveRequests = 0
      Reorder = ReorderIdle
      Removed = None
      Fault = None
      StaleLoad = None
      Flashed = None
      LastMoved = None },
    Cmd.none

// ============================================
// Reine Hilfen
// ============================================

let private blank (text: string) = String.IsNullOrWhiteSpace text

/// Die Anfrage an `testRule` für die Eingaben im Formular. Ohne Text oder ohne Muster gibt es nichts zu prüfen.
let testRequestOf (draft: RuleDraft) : TestRequest option =
    if blank draft.TestInput || blank draft.Pattern then
        None
    else
        Some { Pattern = draft.Pattern; PatternType = draft.PatternType; TargetField = draft.TargetField; Input = draft.TestInput }

/// Die Pflichtfelder als (Label, Wert) für `Form.submitButton`: der Knopf ist gesperrt und nennt, was fehlt.
let requiredFieldPairs (draft: RuleDraft) : (string * string) list =
    [ ("Muster", draft.Pattern)
      ("Kategorie", draft.CategoryId |> Option.map (fun _ -> "gewählt") |> Option.defaultValue "") ]

/// Ein leerer Name wird beim Speichern zum Muster.
let savedName (draft: RuleDraft) : string =
    if blank draft.Name then draft.Pattern.Trim() else draft.Name.Trim()

/// Die neue Regel aus dem Formular. Die Priorität vergibt der Server, die Regel steht danach am Ende
/// (categorization-m47g8).
let buildNewRule (draft: RuleDraft) (categoryId: YnabCategoryId) : NewRule = {
    Name = savedName draft
    Pattern = draft.Pattern
    PatternType = draft.PatternType
    TargetField = draft.TargetField
    CategoryId = categoryId
    PayeeOverride = None
    Enabled = draft.Enabled
}

/// Die Änderung aus dem Formular. Ein Payee-Override hat kein Feld mehr in der Oberfläche: wer einen trägt,
/// behält ihn (`Keep`).
let buildRuleChange (rule: Rule) (draft: RuleDraft) (categoryId: YnabCategoryId) : RuleChange = {
    Id = rule.Id
    Name = Some (savedName draft)
    Pattern = Some draft.Pattern
    PatternType = Some draft.PatternType
    TargetField = Some draft.TargetField
    CategoryId = Some categoryId
    PayeeOverride = PayeeOverrideChange.Keep
    Priority = None
    Enabled = Some draft.Enabled
}

let private panelFor (editor: Editor) : Panel =
    let draft =
        match editor with
        | NewRule -> RuleDraft.empty
        | Editing rule -> RuleDraft.fromRule rule
    { Editor = editor; Draft = draft; Save = NotSaving; Test = NothingToTest; PickerOpen = false; Origin = FromRulesTab }

/// Das Formular „Regel anlegen“ aus einer Buchung (ledger-hg5m5), vorbelegt aus Payee und Kategorie.
let private entryPanel (entryId: Guid) (payee: string) (categoryId: YnabCategoryId option) : Panel =
    { Editor = NewRule
      Draft = RuleDraft.fromEntry payee categoryId
      Save = NotSaving
      Test = NothingToTest
      PickerOpen = false
      Origin = FromEntry entryId }

/// Steht das Formular aus einer Buchung offen? Dann die Id der Buchung.
let entryFormFor (model: Model) : Guid option =
    match model.Panel with
    | Some { Origin = FromEntry entryId } -> Some entryId
    | _ -> None

/// Das Formular aus einer Buchung gehört zu ihr: zeigt das Kassenbuch eine andere Buchung oder keine mehr (Detail
/// zu, andere Buchung, Seite verlassen), schließt es. `shown` ist die Buchung, die das Kassenbuch gerade zeigt.
let followEntry (shown: Guid option) (model: Model) : Model =
    match entryFormFor model with
    | Some entryId when shown <> Some entryId -> { model with Panel = None }
    | _ -> model

/// Das Modell, das der Tab „Regeln“ zeigt: ein Formular aus einer Buchung gehört zur Buchung, nicht in den Tab
/// (ledger-hg5m5). Ein Formular des Tabs bleibt.
let forTab (model: Model) : Model =
    if (entryFormFor model).IsSome then { model with Panel = None } else model

/// Die nächste Aktion auf dem Tab (Regel R3, ADR 0098): Regel wählen, „Neue Regel“, ein Pfeil, ein weiteres Löschen,
/// „Regel anlegen“ aus einer Buchung, den Tab verlassen. Sie nimmt die Zeile „gelöscht“ und einen Fehler an einer
/// Regel weg; ein Zeitlimit gibt es nicht.
let isNextAction (msg: Msg) : bool =
    match msg with
    | RuleChosen _
    | NewRuleRequested
    | MoveRequested _
    | DeleteRequested
    | EntryRuleRequested _
    | RulesTabLeft -> true
    | _ -> false

/// Eine Zeile der Liste: eine Regel mit ihrer Nummer oder die Zeile „gelöscht“ ohne Nummer.
type ListRow =
    | RuleRow of number: int * Rule
    | RemovedRow of RemovedRule

/// Die Zeilen der Liste: die Zeile „gelöscht“ steht an der Stelle der Regel, die Regeln danach rücken in der Nummer
/// auf (Skizze 33: „2 Regel vier“ unter „„Regel drei“ gelöscht“).
let listRows (rules: Rule list) (removed: RemovedRule option) : ListRow list =
    let numbered = rules |> List.mapi (fun index rule -> RuleRow (index + 1, rule))
    match removed with
    | Some row -> List.insertAt (min (max row.Position 0) numbered.Length) (RemovedRow row) numbered
    | None -> numbered

/// Der Fehler unter der Regel `ruleId`, falls er zu ihr gehört.
let faultAt (fault: RuleFault option) (ruleId: RuleId) : string option =
    match fault with
    | Some { RuleId = Some id; Text = text } when id = ruleId -> Some text
    | _ -> None

/// Der Fehler über der Liste: einer, dessen Regel nicht (mehr) in der Liste steht.
let faultAboveList (rules: Rule list) (fault: RuleFault option) : string option =
    match fault with
    | Some { RuleId = Some id } when rules |> List.exists (fun r -> r.Id = id) -> None
    | Some { Text = text } -> Some text
    | None -> None

/// Wo steht der Fokus, wenn eine Taste kommt? In einem Textfeld gehören die Pfeile dem Cursor (§7.8).
type KeyFocus =
    | InTextField
    | Elsewhere

/// Was eine Taste am PC auf dem Tab „Regeln“ auslöst.
type RuleKey =
    | ShowRule of RuleId
    | CloseRulePanel
    | CloseRulePicker

/// Was die Liste der Regeln kann (§7.8): keine Mehrfachauswahl, keine Tabs, keine Suche.
let ruleListTraits : ListKeys.Traits = { MultiSelect = false; Tabs = false; Search = false; TypeToSearch = false }

/// Die Tastenlogik am PC über die gemeinsame Regel (`ListKeys.actionFor`, design-system-gcpws): ↓ ohne Auswahl öffnet
/// die erste, ↑ die letzte Regel, danach die nächste bzw. vorige; in einem Textfeld gehören die Pfeile dem Cursor.
/// Esc schließt das Panel, auch aus einem Feld. Davor steht die eigene Ebene der Seite: bei offener Auswahl der
/// Kategorie schließt Esc nur sie, und die Pfeile gehören ihr. Mit Alt, Strg, Cmd oder Umschalt (`modifier`) und nach
/// `defaultPrevented` reagiert sie nicht.
let ruleKeyAction (model: Model) (focus: KeyFocus) (modifier: bool) (handled: bool) (key: string) : RuleKey option =
    let pickerOpen = model.Panel |> Option.exists (fun p -> p.PickerOpen)
    let current =
        match model.Panel, model.Rules with
        | Some { Editor = Editing rule }, Success rules -> rules |> List.tryFindIndex (fun r -> r.Id = rule.Id)
        | _ -> None
    if pickerOpen && key = "Escape" && not modifier && not handled then
        Some CloseRulePicker
    else
        let action =
            ListKeys.actionFor
                ruleListTraits
                { Key = key
                  Focus = (match focus with InTextField -> ListKeys.InTextField | Elsewhere -> ListKeys.Elsewhere)
                  Command = modifier
                  Shift = false
                  Handled = handled
                  LayerOpen = pickerOpen
                  RowSelected = current.IsSome
                  PanelOpen = model.Panel.IsSome
                  SelectionActive = false }
        match action, model.Rules with
        | ListKeys.ClosePanel, _ -> Some CloseRulePanel
        | (ListKeys.FirstRow | ListKeys.LastRow | ListKeys.StepRow _), Success rules when not rules.IsEmpty ->
            let last = rules.Length - 1
            let target =
                match action, current with
                | ListKeys.FirstRow, _ -> 0
                | ListKeys.LastRow, _ -> last
                | ListKeys.StepRow ListKeys.Forward, Some i -> min last (i + 1)
                | _, Some i -> max 0 (i - 1)
                // Unerreichbar: `StepRow` kommt nur mit gewählter Zeile (`RowSelected = current.IsSome`). Der Fall
                // bleibt, weil der Compiler das nicht sieht und sonst eine unvollständige Prüfung meldet.
                | _, None -> 0
            if Some target = current then None else Some (ShowRule rules.[target].Id)
        | _ -> None

// ============================================
// Aufrufe (hinter Lambdas: unter .NET wirft der Remoting-Proxy im Typinitialisierer)
// ============================================

/// Der Muster-Test. `call` ist der Remoting-Aufruf, im Test ein Ersatz. Die Antwort trägt ihre Anfrage mit;
/// eine Ausnahme ist ein Netzfehler und kein Urteil über das Muster (Bug categorization-f955d).
let testPatternCmd
    (call: string * PatternType * TargetField * string -> Async<Result<bool, RulesError>>)
    (request: TestRequest)
    : Cmd<Msg> =
    Cmd.OfAsync.either
        call
        (request.Pattern, request.PatternType, request.TargetField, request.Input)
        (fun result -> PatternTested (request, result))
        (fun _ -> PatternTestLost request)

/// Die Aufrufe an den Server als Funktionswerte: `update` nimmt die echten (`livePorts`), ein Test einen Ersatz, der
/// die Argumente festhält.
type RulesPorts = {
    GetAll: unit -> Async<Rule list>
    Test: string * PatternType * TargetField * string -> Async<Result<bool, RulesError>>
    Create: NewRule -> Async<Result<Rule, RulesError>>
    Change: RuleChange -> Async<Result<Rule, RulesError>>
    Delete: RuleId -> Async<Result<unit, RulesError>>
    Restore: RuleRestore -> Async<Result<Rule list, RulesError>>
    Reorder: RuleId list -> Async<Result<unit, RulesError>>
}

let livePorts : RulesPorts = {
    GetAll = fun () -> Retry.reading Api.rules.getAllRules ()
    Test = fun args -> Retry.reading Api.rules.testRule args
    Create = fun rule -> Api.rules.createRule rule
    Change = fun change -> Api.rules.updateRule change
    Delete = fun id -> Api.rules.deleteRule id
    Restore = fun request -> Api.rules.restoreRule request
    Reorder = fun ids -> Api.rules.reorderRules ids
}

let private loadCmd (ports: RulesPorts) (version: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        ports.GetAll
        ()
        (fun rules -> RulesLoaded (version, Ok rules))
        (fun ex -> RulesLoaded (version, Error ex.Message))

let private reorderCmd (ports: RulesPorts) (ids: RuleId list) : Cmd<Msg> =
    Cmd.OfAsync.either
        ports.Reorder
        ids
        (fun result -> RulesReordered (result |> Result.mapError rulesErrorToString))
        (fun ex -> RulesReordered (Error ex.Message))

let private unreachable (operation: string) (ex: exn) = RulesError.DatabaseError (operation, ex.Message)

/// So lange leuchtet eine Regel (`Feedback.flash`, Keyframes `feedbackFlash` in styles.css, 0,7 s). Ohne Bewegung
/// steht der Ring so lange.
[<Literal>]
let flashDurationMs = 700

/// Das Ende des Leuchtens. `Cmd.OfAsync.perform` passt hier: das Warten kann nicht scheitern.
let private flashEndCmd (ruleId: RuleId) : Cmd<Msg> =
    Cmd.OfAsync.perform (fun () -> async { do! Async.Sleep flashDurationMs }) () (fun _ -> FlashEnded ruleId)

/// Die Regel leuchtet kurz.
let private flash (rule: Rule) (model: Model) : Model * Cmd<Msg> =
    { model with Flashed = Some rule.Id }, flashEndCmd rule.Id

// ============================================
// update
// ============================================

/// Eine Änderung an der geladenen Liste: sie gilt sofort, eine ältere Ladeantwort danach zählt nicht mehr.
let private withRules (rules: Rule list) (model: Model) : Model =
    { model with Rules = Success rules; LoadVersion = model.LoadVersion + 1 }

let private mapRules (change: Rule list -> Rule list) (model: Model) : Model =
    match model.Rules with
    | Success rules -> withRules (change rules) model
    | _ -> model

/// Nach einer Änderung an Muster, Art, Feld oder Testtext: ein Urteil zu anderen Eingaben gilt nicht mehr.
let private retest (ports: RulesPorts) (panel: Panel) : Panel * Cmd<Msg> =
    match testRequestOf panel.Draft with
    | None -> { panel with Test = NothingToTest }, Cmd.none
    | Some request ->
        match panel.Test with
        | Testing current when current = request -> panel, Cmd.none
        // Ein Urteil über dieselbe Eingabe bleibt. Nach einem Netz- oder Serverfehler gab es keins: dieselbe Eingabe
        // prüft neu.
        | Tested (current, (Matches | NoMatch | PatternFaulty _)) when current = request -> panel, Cmd.none
        | _ -> { panel with Test = Testing request }, testPatternCmd ports.Test request

let private withPanel (model: Model) (change: Panel -> Panel * Cmd<Msg>) : Model * Cmd<Msg> * ExternalMsg =
    match model.Panel with
    | Some panel ->
        let panel', cmd = change panel
        { model with Panel = Some panel' }, cmd, NoOp
    | None -> model, Cmd.none, NoOp

let private editDraft (ports: RulesPorts) (model: Model) (change: RuleDraft -> RuleDraft) (affectsTest: bool) =
    withPanel model (fun panel ->
        // Eine Eingabe nach einem Fehler: die Meldung gehörte zum alten Stand.
        let save = match panel.Save with SaveFailed _ -> NotSaving | other -> other
        let panel' = { panel with Draft = change panel.Draft; Save = save }
        if affectsTest then retest ports panel' else panel', Cmd.none)

let private saveFailure (error: RulesError) : SaveFailure =
    match error with
    | RulesError.InvalidPattern (_, reason) -> PatternRejected reason
    | other -> SaveRejected (rulesErrorToString other)

/// Nach dem Speichern zeigt das Panel die Regel (§7.6), das Sheet schließt. Aus einer Buchung geht es zurück zu
/// ihr: das Formular schließt (ledger-hg5m5).
let private afterSave (shell: Shell) (origin: PanelOrigin) (rule: Rule) : Panel option =
    match shell, origin with
    | _, FromEntry _ -> None
    | InPanel, FromRulesTab -> Some (panelFor (Editing rule))
    | InSheet, FromRulesTab -> None

/// Trägt das offene Formular noch die Anfrage `request`? Nur dann gilt ihre Antwort für das Formular. Ist es
/// geschlossen oder ein anderes (Regel gewechselt, neue Regel, Tab verlassen), zieht sie nur die Liste nach.
let private savingPanel (model: Model) (request: int) : Panel option =
    model.Panel |> Option.filter (fun panel -> panel.Save = Saving request)

/// Die Antwort auf ein Speichern. `place` setzt die gespeicherte Regel in die Liste. Gelungen leuchtet die Regel in der
/// Liste kurz (aus einer Buchung meldet es die Seite, dort ist die Regel nicht zu sehen). Gescheitert steht der Fehler
/// im Formular, solange es offen ist, sonst an `failedElsewhere`.
let private saveAnswered
    (model: Model)
    (shell: Shell)
    (origin: PanelOrigin)
    (request: int)
    (result: Result<Rule, RulesError>)
    (place: Rule -> Rule list -> Rule list)
    (failedElsewhere: string -> Model * ExternalMsg)
    =
    let bound = savingPanel model request
    match result, bound with
    | Ok rule, _ ->
        let model' = mapRules (place rule) model
        let panel =
            match bound with
            | Some saving -> afterSave shell saving.Origin rule
            | None -> model.Panel
        match origin with
        | FromEntry _ -> { model' with Panel = panel }, Cmd.none, ShowFeedback (FeedbackOk, "Regel angelegt", Some rule.Name)
        | FromRulesTab ->
            let flashed, cmd = flash rule { model' with Panel = panel }
            flashed, cmd, NoOp
    | Error err, Some panel ->
        { model with Panel = Some { panel with Save = SaveFailed (saveFailure err) } }, Cmd.none, NoOp
    | Error err, None ->
        let model', ext = failedElsewhere (rulesErrorToString err)
        model', Cmd.none, ext

/// Startet das Laden der Liste. Eine geladene Liste bleibt stehen, bis die neue da ist: der Wechsel zwischen den
/// Tabs zeigt kein Skeleton.
let private startLoad (ports: RulesPorts) (model: Model) : Model * Cmd<Msg> =
    let version = model.LoadVersion + 1
    let rules = if RemoteData.isSuccess model.Rules then model.Rules else Loading
    { model with Rules = rules; LoadVersion = version }, loadCmd ports version

let updateWith (ports: RulesPorts) (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let editDraft = editDraft ports
    // Die nächste Aktion nimmt die Zeile „gelöscht“ und einen Fehler an einer Regel weg (ein Löschen setzt danach
    // seine eigene Zeile).
    let model = if isNextAction msg then { model with Removed = None; Fault = None } else model
    match msg with
    | LoadRules ->
        let model', cmd = startLoad ports model
        model', cmd, NoOp

    | RulesLoaded (version, _) when version <> model.LoadVersion -> model, Cmd.none, NoOp

    | RulesLoaded (_, Ok rules) -> { model with Rules = Success rules; StaleLoad = None }, Cmd.none, NoOp

    // Ein Zustand der Seite, keine Meldung (R3): der Hinweis über der Liste bleibt, bis ein Laden klappt.
    | RulesLoaded (_, Error err) when RemoteData.isSuccess model.Rules -> { model with StaleLoad = Some err }, Cmd.none, NoOp

    | RulesLoaded (_, Error err) -> { model with Rules = Failure err }, Cmd.none, NoOp

    // Die Kategorien holt der Root (`State.fs`) mit dem konfigurierten Budget, er setzt auch `Loading`.
    | LoadCategories -> model, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) -> { model with Categories = Success categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error err) ->
        // Den Grund zeigt die Auswahl der Kategorie mit „Erneut versuchen“ (`categoryLoadState`), ohne Meldung.
        { model with Categories = RemoteData.lateFailure (ynabErrorText err) model.Categories }, Cmd.none, NoOp

    // Ein zweiter Klick auf „Neue Regel“ lässt den Entwurf stehen, wie dieselbe Regel noch einmal.
    | NewRuleRequested when (model.Panel |> Option.exists (fun p -> p.Editor = NewRule)) -> model, Cmd.none, NoOp

    | NewRuleRequested -> { model with Panel = Some (panelFor NewRule) }, Cmd.none, NoOp

    // Dieselbe Buchung noch einmal: der Entwurf bleibt, wie bei „Neue Regel“.
    | EntryRuleRequested (entryId, _, _) when entryFormFor model = Some entryId -> model, Cmd.none, NoOp

    | EntryRuleRequested (entryId, payee, categoryId) ->
        { model with Panel = Some (entryPanel entryId payee categoryId) }, Cmd.none, NoOp

    | RuleChosen ruleId ->
        match model.Panel, model.Rules with
        // Dieselbe Regel noch einmal: die Eingaben bleiben.
        | Some { Editor = Editing open' }, _ when open'.Id = ruleId -> model, Cmd.none, NoOp
        | _, Success rules ->
            match rules |> List.tryFind (fun r -> r.Id = ruleId) with
            | Some rule -> { model with Panel = Some (panelFor (Editing rule)) }, Cmd.none, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | PanelClosed
    | RulesTabLeft -> { model with Panel = None }, Cmd.none, NoOp

    | MoveRequested (ruleId, direction) ->
        match model.Rules with
        | Success rules ->
            let newOrder = reorderedIds direction ruleId rules
            if newOrder = (rules |> List.map (fun r -> r.Id)) then
                model, Cmd.none, NoOp
            else
                let byId = rules |> List.map (fun r -> r.Id, r) |> Map.ofList
                let model' = { withRules (newOrder |> List.choose (fun id -> Map.tryFind id byId)) model with LastMoved = Some ruleId }
                match model.Reorder with
                | ReorderIdle -> { model' with Reorder = ReorderSending (newOrder, false) }, reorderCmd ports newOrder, NoOp
                // Eine Umordnung läuft: der neue Stand geht nach ihrer Antwort hinaus, nicht neben ihr.
                | ReorderSending (sent, _) -> { model' with Reorder = ReorderSending (sent, true) }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RulesReordered (Ok ()) ->
        match model.Reorder, model.Rules with
        | ReorderSending (_, true), Success rules ->
            let ids = rules |> List.map (fun r -> r.Id)
            { model with Reorder = ReorderSending (ids, false) }, reorderCmd ports ids, NoOp
        | _ -> { model with Reorder = ReorderIdle }, Cmd.none, NoOp

    | RulesReordered (Error err) ->
        // Welche Reihenfolge gilt, weiß nur der Server: die Liste lädt neu, ein wartender Stand verfällt.
        let model', cmd = startLoad ports { model with Reorder = ReorderIdle }
        { model' with Fault = Some { RuleId = model.LastMoved; Text = $"Reihenfolge nicht gespeichert: {err}" } }, cmd, NoOp

    | NameEntered value -> editDraft model (fun d -> { d with Name = value }) false
    | PatternEntered value -> editDraft model (fun d -> { d with Pattern = value }) true
    | PatternTypeChosen value -> editDraft model (fun d -> { d with PatternType = value }) true
    | TargetFieldChosen value -> editDraft model (fun d -> { d with TargetField = value }) true
    | TestInputEntered value -> editDraft model (fun d -> { d with TestInput = value }) true
    | EnabledSwitched value -> editDraft model (fun d -> { d with Enabled = value }) false

    | CategoryPickerOpened -> withPanel model (fun p -> { p with PickerOpen = true }, Cmd.none)
    | CategoryPickerClosed -> withPanel model (fun p -> { p with PickerOpen = false }, Cmd.none)

    | CategoryChosen categoryId ->
        let model', cmd, ext = editDraft model (fun d -> { d with CategoryId = categoryId }) false
        match model'.Panel with
        | Some panel -> { model' with Panel = Some { panel with PickerOpen = false } }, cmd, ext
        | None -> model', cmd, ext

    | PatternTested (request, result) ->
        withPanel model (fun panel ->
            match panel.Test with
            | Testing current when current = request ->
                let verdict =
                    match result with
                    | Ok true -> Matches
                    | Ok false -> NoMatch
                    | Error (RulesError.InvalidPattern _ as err) -> PatternFaulty (rulesErrorToString err)
                    | Error err -> TestFailed (rulesErrorToString err)
                { panel with Test = Tested (request, verdict) }, Cmd.none
            | _ -> panel, Cmd.none)

    | PatternTestLost request ->
        withPanel model (fun panel ->
            match panel.Test with
            | Testing current when current = request -> { panel with Test = Tested (request, Unreachable) }, Cmd.none
            | _ -> panel, Cmd.none)

    | CreateRequested shell ->
        match model.Panel with
        | Some ({ Editor = NewRule; Save = (NotSaving | SaveFailed _) } as panel) ->
            match panel.Draft.CategoryId with
            | Some categoryId when not (blank panel.Draft.Pattern) ->
                let request = model.SaveRequests + 1
                let cmd =
                    Cmd.OfAsync.either
                        ports.Create
                        (buildNewRule panel.Draft categoryId)
                        (fun result -> RuleCreated (shell, panel.Origin, request, result))
                        (fun ex -> RuleCreated (shell, panel.Origin, request, Error (unreachable "create" ex)))
                { model with Panel = Some { panel with Save = Saving request }; SaveRequests = request }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleCreated (shell, origin, request, result) ->
        // Ans Ende (categorization-m47g8) und genau einmal: kam eine Ladeantwort nach dem Speichern auf
        // dem Server, steht die Regel schon in der Liste. Eine neue Regel ohne offenes Formular hat keinen Platz in der
        // Liste: der Fehler geht an die Seite.
        saveAnswered
            model shell origin request result
            (fun rule rules -> (rules |> List.filter (fun r -> r.Id <> rule.Id)) @ [ rule ])
            (fun reason -> model, ShowFeedback (FeedbackError, "Regel nicht angelegt", Some reason))

    | ChangeRequested shell ->
        match model.Panel with
        | Some ({ Editor = Editing rule; Save = (NotSaving | SaveFailed _) } as panel) ->
            match panel.Draft.CategoryId with
            | Some categoryId when not (blank panel.Draft.Pattern) ->
                let request = model.SaveRequests + 1
                let cmd =
                    Cmd.OfAsync.either
                        ports.Change
                        (buildRuleChange rule panel.Draft categoryId)
                        (fun result -> RuleChanged (shell, rule.Id, request, result))
                        (fun ex -> RuleChanged (shell, rule.Id, request, Error (unreachable "update" ex)))
                { model with Panel = Some { panel with Save = Saving request }; SaveRequests = request }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleChanged (shell, ruleId, request, result) ->
        saveAnswered
            model shell FromRulesTab request result
            (fun rule rules -> rules |> List.map (fun r -> if r.Id = rule.Id then rule else r))
            (fun reason -> { model with Fault = Some { RuleId = Some ruleId; Text = $"Nicht gespeichert: {reason}" } }, NoOp)

    | FlashEnded ruleId when model.Flashed = Some ruleId -> { model with Flashed = None }, Cmd.none, NoOp
    | FlashEnded _ -> model, Cmd.none, NoOp

    | DeleteRequested ->
        match model.Panel, model.Rules with
        | Some { Editor = Editing rule; Save = (NotSaving | SaveFailed _) }, Success rules ->
            match rules |> List.tryFindIndex (fun r -> r.Id = rule.Id) with
            | Some position ->
                let cmd =
                    Cmd.OfAsync.either
                        ports.Delete
                        rule.Id
                        (fun result -> RuleDeleted (rule, position, result))
                        (fun ex -> RuleDeleted (rule, position, Error (unreachable "delete" ex)))
                let model' = withRules (rules |> List.filter (fun r -> r.Id <> rule.Id)) model
                { model' with Panel = None; Removed = Some { Rule = rule; Position = position; Phase = Removing } }, cmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleDeleted (rule, _, Ok ()) ->
        match model.Removed with
        | Some ({ Phase = Removing } as removed) when removed.Rule.Id = rule.Id ->
            { model with Removed = Some { removed with Phase = Removed } }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleDeleted (rule, position, Error err) ->
        // Die Regel steht noch auf dem Server: sie kommt an ihre Stelle zurück, darunter der Grund.
        let model' =
            mapRules
                (fun rules ->
                    if rules |> List.exists (fun r -> r.Id = rule.Id) then rules
                    else List.insertAt (min position rules.Length) rule rules)
                model
        let removed = model.Removed |> Option.filter (fun r -> r.Rule.Id <> rule.Id)
        { model' with
            Removed = removed
            Fault = Some { RuleId = Some rule.Id; Text = $"Nicht gelöscht: {rulesErrorToString err}" } },
        Cmd.none,
        NoOp

    | RestoreRequested ->
        match model.Removed with
        | Some ({ Phase = (Removed | RestoreFailed _) } as removed) ->
            let rule = removed.Rule
            let cmd =
                Cmd.OfAsync.either
                    ports.Restore
                    { Rule = rule; Position = removed.Position }
                    (fun result -> RuleRestored (rule, result))
                    (fun ex -> RuleRestored (rule, Error (unreachable "restore" ex)))
            { model with Removed = Some { removed with Phase = Restoring } }, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    // Die Antwort ist die ganze Liste in der neuen Reihenfolge: kein Zwischenstand an falscher Stelle. Die Regel steht
    // wieder an ihrem Platz und leuchtet kurz.
    | RuleRestored (rule, Ok rules) ->
        let removed = model.Removed |> Option.filter (fun r -> r.Rule.Id <> rule.Id)
        let model', cmd = flash rule { withRules rules model with Removed = removed }
        model', cmd, NoOp

    | RuleRestored (rule, Error err) ->
        let reason = rulesErrorToString err
        match model.Removed with
        | Some removed when removed.Rule.Id = rule.Id ->
            { model with Removed = Some { removed with Phase = RestoreFailed reason } }, Cmd.none, NoOp
        // Die Zeile ist schon weg (nächste Aktion während des Wiederherstellens): der Fehler geht an die Seite.
        | _ -> model, Cmd.none, ShowFeedback (FeedbackError, "Regel nicht wiederhergestellt", Some reason)

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg = updateWith livePorts msg model
