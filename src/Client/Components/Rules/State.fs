module Components.Rules.State

open System
open Elmish
open Client
open Components.Rules.Types
open Types
open Shared.Domain

let init () : Model * Cmd<Msg> =
    { Rules = NotAsked; LoadVersion = 0; Categories = []; Panel = None }, Cmd.none

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

/// Die neue Regel aus dem Formular. Die Priorität vergibt der Server, die Regel steht danach vorn.
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
    { Editor = editor; Draft = draft; Save = NotSaving; Test = NothingToTest; PickerOpen = false }

/// Wo steht der Fokus, wenn eine Taste kommt? In einem Textfeld gehören die Pfeile dem Cursor (§7.8).
type KeyFocus =
    | InTextField
    | Elsewhere

/// Was eine Taste am PC auf dem Tab „Regeln“ auslöst.
type RuleKey =
    | ShowRule of RuleId
    | CloseRulePanel
    | CloseRulePicker

/// Die Tastenlogik am PC (§7.8, ADR 0087 Punkte 18 und 19): ↓ ohne Auswahl öffnet die erste, ↑ die letzte Regel,
/// danach die nächste bzw. vorige; in einem Textfeld gehören die Pfeile dem Cursor. Esc schließt zuerst die
/// Auswahl der Kategorie, dann das Panel. Mit Alt, Strg oder Cmd und nach `defaultPrevented` reagiert sie nicht.
let ruleKeyAction (model: Model) (focus: KeyFocus) (modifier: bool) (handled: bool) (key: string) : RuleKey option =
    let pickerOpen = model.Panel |> Option.exists (fun p -> p.PickerOpen)
    if handled || modifier then
        None
    else
        match key with
        | "Escape" when pickerOpen -> Some CloseRulePicker
        | "Escape" when model.Panel.IsSome -> Some CloseRulePanel
        | ("ArrowDown" | "ArrowUp") when not pickerOpen && focus = Elsewhere ->
            match model.Rules with
            | Success rules when not rules.IsEmpty ->
                let last = rules.Length - 1
                let current =
                    match model.Panel with
                    | Some { Editor = Editing rule } -> rules |> List.tryFindIndex (fun r -> r.Id = rule.Id)
                    | _ -> None
                let target =
                    match key, current with
                    | "ArrowDown", None -> 0
                    | "ArrowDown", Some i -> min last (i + 1)
                    | _, None -> last
                    | _, Some i -> max 0 (i - 1)
                if Some target = current then None else Some (ShowRule rules.[target].Id)
            | _ -> None
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

let private liveTest (args: string * PatternType * TargetField * string) =
    Retry.reading Api.rules.testRule args

let private loadCmd (version: int) : Cmd<Msg> =
    Cmd.OfAsync.either
        (fun () -> Retry.reading Api.rules.getAllRules ())
        ()
        (fun rules -> RulesLoaded (version, Ok rules))
        (fun ex -> RulesLoaded (version, Error ex.Message))

let private unreachable (operation: string) (ex: exn) = RulesError.DatabaseError (operation, ex.Message)

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
let private retest (panel: Panel) : Panel * Cmd<Msg> =
    match testRequestOf panel.Draft with
    | None -> { panel with Test = NothingToTest }, Cmd.none
    | Some request ->
        match panel.Test with
        | Testing current
        | Tested (current, _) when current = request -> panel, Cmd.none
        | _ -> { panel with Test = Testing request }, testPatternCmd liveTest request

let private withPanel (model: Model) (change: Panel -> Panel * Cmd<Msg>) : Model * Cmd<Msg> * ExternalMsg =
    match model.Panel with
    | Some panel ->
        let panel', cmd = change panel
        { model with Panel = Some panel' }, cmd, NoOp
    | None -> model, Cmd.none, NoOp

let private editDraft (model: Model) (change: RuleDraft -> RuleDraft) (affectsTest: bool) =
    withPanel model (fun panel ->
        // Eine Eingabe nach einem Fehler: die Meldung gehörte zum alten Stand.
        let save = match panel.Save with SaveFailed _ -> NotSaving | other -> other
        let panel' = { panel with Draft = change panel.Draft; Save = save }
        if affectsTest then retest panel' else panel', Cmd.none)

let private saveFailure (error: RulesError) : SaveFailure =
    match error with
    | RulesError.InvalidPattern (_, reason) -> PatternRejected reason
    | other -> SaveRejected (rulesErrorToString other)

/// Nach dem Speichern zeigt das Panel die Regel (§7.6), das Sheet schließt.
let private afterSave (shell: Shell) (rule: Rule) : Panel option =
    match shell with
    | InPanel -> Some (panelFor (Editing rule))
    | InSheet -> None

let private savingFailed (model: Model) (error: RulesError) =
    match model.Panel with
    | Some panel -> { model with Panel = Some { panel with Save = SaveFailed (saveFailure error) } }
    | None -> model

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadRules ->
        let version = model.LoadVersion + 1
        // Eine geladene Liste bleibt stehen, bis die neue da ist: der Wechsel zwischen den Tabs zeigt kein Skeleton.
        let rules = if RemoteData.isSuccess model.Rules then model.Rules else Loading
        { model with Rules = rules; LoadVersion = version }, loadCmd version, NoOp

    | RulesLoaded (version, _) when version <> model.LoadVersion -> model, Cmd.none, NoOp

    | RulesLoaded (_, Ok rules) -> { model with Rules = Success rules }, Cmd.none, NoOp

    | RulesLoaded (_, Error err) when RemoteData.isSuccess model.Rules ->
        model, Cmd.none, ShowToast ($"Regeln konnten nicht neu geladen werden: {err}", ToastError)

    | RulesLoaded (_, Error err) -> { model with Rules = Failure err }, Cmd.none, NoOp

    // Die Kategorien holt der Root (`State.fs`) mit dem konfigurierten Budget.
    | LoadCategories -> model, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) -> { model with Categories = categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error err) ->
        model, Cmd.none, ShowToast ($"Kategorien konnten nicht geladen werden: {ynabErrorText err}", ToastError)

    | NewRuleRequested -> { model with Panel = Some (panelFor NewRule) }, Cmd.none, NoOp

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
                let reordered = newOrder |> List.choose (fun id -> Map.tryFind id byId)
                let cmd =
                    Cmd.OfAsync.either
                        (fun ids -> Api.rules.reorderRules ids)
                        newOrder
                        (fun result -> RulesReordered (rules, result |> Result.mapError rulesErrorToString))
                        (fun ex -> RulesReordered (rules, Error ex.Message))
                withRules reordered model, cmd, NoOp
        | _ -> model, Cmd.none, NoOp

    | RulesReordered (_, Ok ()) -> model, Cmd.none, NoOp

    | RulesReordered (previous, Error err) ->
        withRules previous model, Cmd.none, ShowToast ($"Reihenfolge konnte nicht gespeichert werden: {err}", ToastError)

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
                    | Error err -> PatternFaulty (rulesErrorToString err)
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
                let cmd =
                    Cmd.OfAsync.either
                        (fun rule -> Api.rules.createRule rule)
                        (buildNewRule panel.Draft categoryId)
                        (fun result -> RuleCreated (shell, result))
                        (fun ex -> RuleCreated (shell, Error (unreachable "create" ex)))
                { model with Panel = Some { panel with Save = Saving } }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleCreated (shell, Ok rule) ->
        let model' = mapRules (fun rules -> rule :: rules) model
        { model' with Panel = afterSave shell rule }, Cmd.none, ShowToast ("Regel angelegt", ToastSuccess)

    | RuleCreated (_, Error err) -> savingFailed model err, Cmd.none, NoOp

    | ChangeRequested shell ->
        match model.Panel with
        | Some ({ Editor = Editing rule; Save = (NotSaving | SaveFailed _) } as panel) ->
            match panel.Draft.CategoryId with
            | Some categoryId when not (blank panel.Draft.Pattern) ->
                let cmd =
                    Cmd.OfAsync.either
                        (fun change -> Api.rules.updateRule change)
                        (buildRuleChange rule panel.Draft categoryId)
                        (fun result -> RuleChanged (shell, result))
                        (fun ex -> RuleChanged (shell, Error (unreachable "update" ex)))
                { model with Panel = Some { panel with Save = Saving } }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleChanged (shell, Ok rule) ->
        let model' = mapRules (List.map (fun r -> if r.Id = rule.Id then rule else r)) model
        { model' with Panel = afterSave shell rule }, Cmd.none, ShowToast ("Regel gespeichert", ToastSuccess)

    | RuleChanged (_, Error err) -> savingFailed model err, Cmd.none, NoOp

    | DeleteRequested ->
        match model.Panel, model.Rules with
        | Some { Editor = Editing rule; Save = (NotSaving | SaveFailed _) }, Success rules ->
            match rules |> List.tryFindIndex (fun r -> r.Id = rule.Id) with
            | Some position ->
                let cmd =
                    Cmd.OfAsync.either
                        (fun id -> Api.rules.deleteRule id)
                        rule.Id
                        (fun result -> RuleDeleted (rule, position, result))
                        (fun ex -> RuleDeleted (rule, position, Error (unreachable "delete" ex)))
                let model' = withRules (rules |> List.filter (fun r -> r.Id <> rule.Id)) model
                { model' with Panel = None }, cmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleDeleted (rule, position, Ok ()) -> model, Cmd.none, RuleDeletedUndoable (rule, position)

    | RuleDeleted (rule, position, Error err) ->
        // Die Regel steht noch auf dem Server: sie kommt an ihre Stelle zurück.
        let model' =
            mapRules
                (fun rules ->
                    if rules |> List.exists (fun r -> r.Id = rule.Id) then rules
                    else List.insertAt (min position rules.Length) rule rules)
                model
        model', Cmd.none, ShowToast ($"Regel konnte nicht gelöscht werden: {rulesErrorToString err}", ToastError)

    | RestoreRequested (rule, position) ->
        let cmd =
            Cmd.OfAsync.either
                (fun request -> Api.rules.restoreRule request)
                { Rule = rule; Position = position }
                RuleRestored
                (fun ex -> RuleRestored (Error (unreachable "restore" ex)))
        model, cmd, NoOp

    // Die Antwort ist die ganze Liste in der neuen Reihenfolge: kein Zwischenstand an falscher Stelle.
    | RuleRestored (Ok rules) -> withRules rules model, Cmd.none, NoOp

    | RuleRestored (Error err) ->
        model, Cmd.none, ShowToast ($"Regel konnte nicht wiederhergestellt werden: {rulesErrorToString err}", ToastError)
