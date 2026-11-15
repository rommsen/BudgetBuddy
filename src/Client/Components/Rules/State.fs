module Components.Rules.State

open System
open Elmish
open Client
open Components.Rules.Types
open Types
open Shared.Domain

module ListKeys = Client.DesignSystem.ListKeys

let init () : Model * Cmd<Msg> =
    { Rules = NotAsked; LoadVersion = 0; Categories = NotAsked; Panel = None; SaveRequests = 0; Reorder = ReorderIdle }, Cmd.none

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

/// Die Antwort auf ein Speichern. `place` setzt die gespeicherte Regel in die Liste, `failedText` nennt einen Fehler,
/// dessen Formular nicht mehr offen ist.
let private saveAnswered
    (model: Model)
    (shell: Shell)
    (request: int)
    (result: Result<Rule, RulesError>)
    (place: Rule -> Rule list -> Rule list)
    (savedText: string)
    (failedText: string)
    =
    let bound = savingPanel model request
    match result, bound with
    | Ok rule, _ ->
        let model' = mapRules (place rule) model
        let panel =
            match bound with
            | Some saving -> afterSave shell saving.Origin rule
            | None -> model.Panel
        { model' with Panel = panel }, Cmd.none, ShowToast (savedText, ToastSuccess)
    | Error err, Some panel ->
        { model with Panel = Some { panel with Save = SaveFailed (saveFailure err) } }, Cmd.none, NoOp
    | Error err, None ->
        model, Cmd.none, ShowToast ($"{failedText}: {rulesErrorToString err}", ToastError)

/// Startet das Laden der Liste. Eine geladene Liste bleibt stehen, bis die neue da ist: der Wechsel zwischen den
/// Tabs zeigt kein Skeleton.
let private startLoad (ports: RulesPorts) (model: Model) : Model * Cmd<Msg> =
    let version = model.LoadVersion + 1
    let rules = if RemoteData.isSuccess model.Rules then model.Rules else Loading
    { model with Rules = rules; LoadVersion = version }, loadCmd ports version

let updateWith (ports: RulesPorts) (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    let editDraft = editDraft ports
    match msg with
    | LoadRules ->
        let model', cmd = startLoad ports model
        model', cmd, NoOp

    | RulesLoaded (version, _) when version <> model.LoadVersion -> model, Cmd.none, NoOp

    | RulesLoaded (_, Ok rules) -> { model with Rules = Success rules }, Cmd.none, NoOp

    | RulesLoaded (_, Error err) when RemoteData.isSuccess model.Rules ->
        model, Cmd.none, ShowToast ($"Regeln konnten nicht neu geladen werden: {err}", ToastError)

    | RulesLoaded (_, Error err) -> { model with Rules = Failure err }, Cmd.none, NoOp

    // Die Kategorien holt der Root (`State.fs`) mit dem konfigurierten Budget, er setzt auch `Loading`.
    | LoadCategories -> model, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) -> { model with Categories = Success categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error err) ->
        let text = ynabErrorText err
        { model with Categories = RemoteData.lateFailure text model.Categories },
        Cmd.none,
        ShowToast ($"Kategorien konnten nicht geladen werden: {text}", ToastError)

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
                let model' = withRules (newOrder |> List.choose (fun id -> Map.tryFind id byId)) model
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
        model', cmd, ShowToast ($"Reihenfolge konnte nicht gespeichert werden: {err}", ToastError)

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
                        (fun result -> RuleCreated (shell, request, result))
                        (fun ex -> RuleCreated (shell, request, Error (unreachable "create" ex)))
                { model with Panel = Some { panel with Save = Saving request }; SaveRequests = request }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleCreated (shell, request, result) ->
        // Ans Ende (categorization-m47g8) und genau einmal: kam eine Ladeantwort nach dem Speichern auf
        // dem Server, steht die Regel schon in der Liste.
        saveAnswered model shell request result (fun rule rules -> (rules |> List.filter (fun r -> r.Id <> rule.Id)) @ [ rule ]) "Regel angelegt" "Regel konnte nicht angelegt werden"

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
                        (fun result -> RuleChanged (shell, request, result))
                        (fun ex -> RuleChanged (shell, request, Error (unreachable "update" ex)))
                { model with Panel = Some { panel with Save = Saving request }; SaveRequests = request }, cmd, NoOp
            | _ -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleChanged (shell, request, result) ->
        saveAnswered
            model shell request result
            (fun rule rules -> rules |> List.map (fun r -> if r.Id = rule.Id then rule else r))
            "Regel gespeichert"
            "Regel konnte nicht gespeichert werden"

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
                ports.Restore
                { Rule = rule; Position = position }
                RuleRestored
                (fun ex -> RuleRestored (Error (unreachable "restore" ex)))
        model, cmd, NoOp

    // Die Antwort ist die ganze Liste in der neuen Reihenfolge: kein Zwischenstand an falscher Stelle.
    | RuleRestored (Ok rules) -> withRules rules model, Cmd.none, NoOp

    | RuleRestored (Error err) ->
        model, Cmd.none, ShowToast ($"Regel konnte nicht wiederhergestellt werden: {rulesErrorToString err}", ToastError)

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg = updateWith livePorts msg model
