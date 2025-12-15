module Components.Rules.State

open System
open Elmish
open Components.Rules.Types
open Types
open Shared.Domain

let private rulesErrorToString (error: RulesError) : string =
    match error with
    | RulesError.RuleNotFound ruleId -> $"Rule not found: {ruleId}"
    | RulesError.InvalidPattern (pattern, reason) -> $"Invalid pattern '{pattern}': {reason}"
    | RulesError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | RulesError.DuplicateRule pattern -> $"Duplicate rule pattern: {pattern}"
    | RulesError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"

let init () : Model * Cmd<Msg> =
    let model = {
        Rules = NotAsked
        EditingRule = None
        IsNewRule = false
        Categories = []
        Form = RuleFormState.empty
        ConfirmingDeleteRuleId = None
    }
    let cmd = Cmd.batch [
        Cmd.ofMsg LoadRules
        Cmd.ofMsg LoadCategories
    ]
    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | LoadRules ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.getAllRules
                ()
                (Ok >> RulesLoaded)
                (fun ex -> Error ex.Message |> RulesLoaded)
        { model with Rules = Loading }, cmd, NoOp

    | RulesLoaded (Ok rules) ->
        { model with Rules = Success rules }, Cmd.none, NoOp

    | RulesLoaded (Error err) ->
        { model with Rules = Failure err }, Cmd.none, ShowToast ($"Failed to load rules: {err}", ToastError)

    | OpenNewRuleModal ->
        let loadCategoriesCmd =
            if model.Categories.IsEmpty then Cmd.ofMsg LoadCategories else Cmd.none
        { model with
            EditingRule = None
            IsNewRule = true
            Form = RuleFormState.empty
        }, loadCategoriesCmd, NoOp

    | EditRule ruleId ->
        match model.Rules with
        | Success rules ->
            match rules |> List.tryFind (fun r -> r.Id = ruleId) with
            | Some rule ->
                let loadCategoriesCmd =
                    if model.Categories.IsEmpty then Cmd.ofMsg LoadCategories else Cmd.none
                { model with
                    EditingRule = Some rule
                    IsNewRule = false
                    Form = RuleFormState.fromRule rule
                }, loadCategoriesCmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseRuleModal ->
        { model with
            EditingRule = None
            IsNewRule = false
            Form = RuleFormState.empty
        }, Cmd.none, NoOp

    | ConfirmDeleteRule ruleId ->
        // Show confirm button and start 3 second timeout
        let timeoutCmd =
            Cmd.OfAsync.perform
                (fun () -> async { do! Async.Sleep 3000 })
                ()
                (fun () -> CancelConfirmDelete)
        { model with ConfirmingDeleteRuleId = Some ruleId }, timeoutCmd, NoOp

    | CancelConfirmDelete ->
        // Timeout expired - hide confirm button
        { model with ConfirmingDeleteRuleId = None }, Cmd.none, NoOp

    | DeleteRule ruleId ->
        // Reset confirm state and actually delete
        let cmd =
            Cmd.OfAsync.either
                Api.rules.deleteRule
                ruleId
                (fun result ->
                    match result with
                    | Ok () -> Ok ruleId |> RuleDeleted
                    | Error err -> Error err |> RuleDeleted)
                (fun ex -> Error (RulesError.DatabaseError ("delete", ex.Message)) |> RuleDeleted)
        { model with ConfirmingDeleteRuleId = None }, cmd, NoOp

    | RuleDeleted (Ok deletedRuleId) ->
        match model.Rules with
        | Success rules ->
            let newRules = rules |> List.filter (fun r -> r.Id <> deletedRuleId)
            { model with Rules = Success newRules }, Cmd.none, ShowToast ("Rule deleted", ToastSuccess)
        | _ -> model, Cmd.none, ShowToast ("Rule deleted", ToastSuccess)

    | RuleDeleted (Error err) ->
        model, Cmd.none, ShowToast (rulesErrorToString err, ToastError)

    | ToggleRuleEnabled ruleId ->
        match model.Rules with
        | Success rules ->
            match rules |> List.tryFind (fun r -> r.Id = ruleId) with
            | Some rule ->
                let updateRequest : RuleUpdateRequest = {
                    Id = ruleId
                    Name = None
                    Pattern = None
                    PatternType = None
                    TargetField = None
                    CategoryId = None
                    PayeeOverride = None
                    Priority = None
                    Enabled = Some (not rule.Enabled)
                }
                let cmd =
                    Cmd.OfAsync.either
                        Api.rules.updateRule
                        updateRequest
                        RuleToggled
                        (fun ex -> Error (RulesError.DatabaseError ("toggle", ex.Message)) |> RuleToggled)
                model, cmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleToggled (Ok updatedRule) ->
        match model.Rules with
        | Success rules ->
            let newRules = rules |> List.map (fun r ->
                if r.Id = updatedRule.Id then updatedRule else r)
            { model with Rules = Success newRules }, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | RuleToggled (Error err) ->
        model, Cmd.none, ShowToast (rulesErrorToString err, ToastError)

    | LoadCategories ->
        // This will be called by parent with proper budget ID
        model, Cmd.none, NoOp

    | CategoriesLoaded (Ok categories) ->
        { model with Categories = categories }, Cmd.none, NoOp

    | CategoriesLoaded (Error _) ->
        model, Cmd.none, NoOp

    // Rule form messages
    | UpdateRuleFormName value ->
        { model with Form = { model.Form with Name = value } }, Cmd.none, NoOp

    | UpdateRuleFormPattern value ->
        { model with Form = { model.Form with Pattern = value; TestResult = None } }, Cmd.none, NoOp

    | UpdateRuleFormPatternType value ->
        { model with Form = { model.Form with PatternType = value; TestResult = None } }, Cmd.none, NoOp

    | UpdateRuleFormTargetField value ->
        { model with Form = { model.Form with TargetField = value } }, Cmd.none, NoOp

    | UpdateRuleFormCategoryId value ->
        { model with Form = { model.Form with CategoryId = value } }, Cmd.none, NoOp

    | UpdateRuleFormPayeeOverride value ->
        { model with Form = { model.Form with PayeeOverride = value } }, Cmd.none, NoOp

    | UpdateRuleFormEnabled value ->
        { model with Form = { model.Form with Enabled = value } }, Cmd.none, NoOp

    | UpdateRuleFormTestInput value ->
        { model with Form = { model.Form with TestInput = value; TestResult = None } }, Cmd.none, NoOp

    | TestRulePattern ->
        if String.IsNullOrWhiteSpace(model.Form.Pattern) || String.IsNullOrWhiteSpace(model.Form.TestInput) then
            model, Cmd.none, ShowToast ("Please enter both a pattern and test input", ToastWarning)
        else
            let cmd =
                Cmd.OfAsync.either
                    Api.rules.testRule
                    (model.Form.Pattern, model.Form.PatternType, model.Form.TargetField, model.Form.TestInput)
                    (Ok >> RulePatternTested)
                    (fun ex -> Error (RulesError.InvalidPattern (model.Form.Pattern, ex.Message)) |> RulePatternTested)
            model, cmd, NoOp

    | RulePatternTested (Ok matches) ->
        let resultText = if matches then "✅ Pattern matches!" else "❌ Pattern does not match"
        { model with Form = { model.Form with TestResult = Some resultText } }, Cmd.none, NoOp

    | RulePatternTested (Error err) ->
        { model with Form = { model.Form with TestResult = Some $"⚠️ {rulesErrorToString err}" } }, Cmd.none, NoOp

    | SaveRule ->
        match model.Form.CategoryId with
        | None ->
            model, Cmd.none, ShowToast ("Please select a category", ToastWarning)
        | Some categoryId ->
            if String.IsNullOrWhiteSpace(model.Form.Name) then
                model, Cmd.none, ShowToast ("Please enter a rule name", ToastWarning)
            elif String.IsNullOrWhiteSpace(model.Form.Pattern) then
                model, Cmd.none, ShowToast ("Please enter a pattern", ToastWarning)
            else
                let payeeOverride = if String.IsNullOrWhiteSpace(model.Form.PayeeOverride) then None else Some model.Form.PayeeOverride
                if model.IsNewRule then
                    let nextPriority =
                        match model.Rules with
                        | Success rules -> (rules |> List.map (fun r -> r.Priority) |> List.fold max 0) + 1
                        | _ -> 1
                    let request : RuleCreateRequest = {
                        Name = model.Form.Name
                        Pattern = model.Form.Pattern
                        PatternType = model.Form.PatternType
                        TargetField = model.Form.TargetField
                        CategoryId = categoryId
                        PayeeOverride = payeeOverride
                        Priority = nextPriority
                    }
                    let cmd =
                        Cmd.OfAsync.either
                            Api.rules.createRule
                            request
                            RuleSaved
                            (fun ex -> Error (RulesError.DatabaseError ("create", ex.Message)) |> RuleSaved)
                    { model with Form = { model.Form with IsSaving = true } }, cmd, NoOp
                else
                    match model.EditingRule with
                    | Some rule ->
                        let request : RuleUpdateRequest = {
                            Id = rule.Id
                            Name = Some model.Form.Name
                            Pattern = Some model.Form.Pattern
                            PatternType = Some model.Form.PatternType
                            TargetField = Some model.Form.TargetField
                            CategoryId = Some categoryId
                            PayeeOverride = payeeOverride
                            Priority = None
                            Enabled = Some model.Form.Enabled
                        }
                        let cmd =
                            Cmd.OfAsync.either
                                Api.rules.updateRule
                                request
                                RuleSaved
                                (fun ex -> Error (RulesError.DatabaseError ("update", ex.Message)) |> RuleSaved)
                        { model with Form = { model.Form with IsSaving = true } }, cmd, NoOp
                    | None -> model, Cmd.none, NoOp

    | RuleSaved (Ok savedRule) ->
        let action = if model.IsNewRule then "created" else "updated"
        let newRules =
            match model.Rules with
            | Success rules ->
                if model.IsNewRule then
                    Success (savedRule :: rules)
                else
                    Success (rules |> List.map (fun r -> if r.Id = savedRule.Id then savedRule else r))
            | other -> other
        { model with
            Rules = newRules
            EditingRule = None
            IsNewRule = false
            Form = RuleFormState.empty
        }, Cmd.none, ShowToast ($"Rule {action} successfully", ToastSuccess)

    | RuleSaved (Error err) ->
        { model with Form = { model.Form with IsSaving = false } }, Cmd.none, ShowToast (rulesErrorToString err, ToastError)

    | ExportRules ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.exportRules
                ()
                (Ok >> RulesExported)
                (fun ex -> Error (RulesError.DatabaseError ("export", ex.Message)) |> RulesExported)
        model, cmd, NoOp

    | RulesExported (Ok json) ->
        // Trigger browser download using direct JS interop
        Fable.Core.JS.eval(sprintf """
            (function() {
                var blob = new Blob([%s], {type: 'application/json'});
                var url = URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = url;
                a.download = 'budgetbuddy-rules.json';
                a.click();
                URL.revokeObjectURL(url);
            })();
        """ (Fable.Core.JS.JSON.stringify json)) |> ignore
        model, Cmd.none, ShowToast ("Rules exported successfully", ToastSuccess)

    | RulesExported (Error err) ->
        model, Cmd.none, ShowToast (rulesErrorToString err, ToastError)

    | ImportRulesStart ->
        // This message triggers file input click from the view
        model, Cmd.none, NoOp

    | ImportRules json ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.importRules
                json
                RulesImported
                (fun ex -> Error (RulesError.DatabaseError ("import", ex.Message)) |> RulesImported)
        model, cmd, NoOp

    | RulesImported (Ok count) ->
        model, Cmd.ofMsg LoadRules, ShowToast ($"Imported {count} rule(s) successfully", ToastSuccess)

    | RulesImported (Error err) ->
        model, Cmd.none, ShowToast (rulesErrorToString err, ToastError)
