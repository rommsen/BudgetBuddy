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

let private emptyRuleForm () = {|
    Name = ""
    Pattern = ""
    PatternType = Contains
    TargetField = Combined
    CategoryId = None
    PayeeOverride = ""
    Enabled = true
    TestInput = ""
    TestResult = None
|}

let init () : Model * Cmd<Msg> =
    let emptyForm = emptyRuleForm ()
    let model = {
        Rules = NotAsked
        EditingRule = None
        IsNewRule = false
        Categories = []
        RuleFormName = emptyForm.Name
        RuleFormPattern = emptyForm.Pattern
        RuleFormPatternType = emptyForm.PatternType
        RuleFormTargetField = emptyForm.TargetField
        RuleFormCategoryId = emptyForm.CategoryId
        RuleFormPayeeOverride = emptyForm.PayeeOverride
        RuleFormEnabled = emptyForm.Enabled
        RuleFormTestInput = emptyForm.TestInput
        RuleFormTestResult = emptyForm.TestResult
        RuleSaving = false
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
            Cmd.OfAsync.perform
                Api.rules.getAllRules
                ()
                RulesLoaded
        { model with Rules = Loading }, cmd, NoOp

    | RulesLoaded rules ->
        { model with Rules = Success rules }, Cmd.none, NoOp

    | OpenNewRuleModal ->
        let emptyForm = emptyRuleForm ()
        let loadCategoriesCmd =
            if model.Categories.IsEmpty then Cmd.ofMsg LoadCategories else Cmd.none
        { model with
            EditingRule = None
            IsNewRule = true
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
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
                    RuleFormName = rule.Name
                    RuleFormPattern = rule.Pattern
                    RuleFormPatternType = rule.PatternType
                    RuleFormTargetField = rule.TargetField
                    RuleFormCategoryId = Some rule.CategoryId
                    RuleFormPayeeOverride = rule.PayeeOverride |> Option.defaultValue ""
                    RuleFormEnabled = rule.Enabled
                    RuleFormTestInput = ""
                    RuleFormTestResult = None
                    RuleSaving = false
                }, loadCategoriesCmd, NoOp
            | None -> model, Cmd.none, NoOp
        | _ -> model, Cmd.none, NoOp

    | CloseRuleModal ->
        let emptyForm = emptyRuleForm ()
        { model with
            EditingRule = None
            IsNewRule = false
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
        }, Cmd.none, NoOp

    | DeleteRule ruleId ->
        let cmd =
            Cmd.OfAsync.either
                Api.rules.deleteRule
                ruleId
                RuleDeleted
                (fun ex -> Error (RulesError.DatabaseError ("delete", ex.Message)) |> RuleDeleted)
        model, cmd, NoOp

    | RuleDeleted (Ok _) ->
        model, Cmd.ofMsg LoadRules, ShowToast ("Rule deleted", ToastSuccess)

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

    | RuleToggled (Ok _) ->
        model, Cmd.ofMsg LoadRules, NoOp

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
        { model with RuleFormName = value }, Cmd.none, NoOp

    | UpdateRuleFormPattern value ->
        { model with RuleFormPattern = value; RuleFormTestResult = None }, Cmd.none, NoOp

    | UpdateRuleFormPatternType value ->
        { model with RuleFormPatternType = value; RuleFormTestResult = None }, Cmd.none, NoOp

    | UpdateRuleFormTargetField value ->
        { model with RuleFormTargetField = value }, Cmd.none, NoOp

    | UpdateRuleFormCategoryId value ->
        { model with RuleFormCategoryId = value }, Cmd.none, NoOp

    | UpdateRuleFormPayeeOverride value ->
        { model with RuleFormPayeeOverride = value }, Cmd.none, NoOp

    | UpdateRuleFormEnabled value ->
        { model with RuleFormEnabled = value }, Cmd.none, NoOp

    | UpdateRuleFormTestInput value ->
        { model with RuleFormTestInput = value; RuleFormTestResult = None }, Cmd.none, NoOp

    | TestRulePattern ->
        if String.IsNullOrWhiteSpace(model.RuleFormPattern) || String.IsNullOrWhiteSpace(model.RuleFormTestInput) then
            model, Cmd.none, ShowToast ("Please enter both a pattern and test input", ToastWarning)
        else
            let cmd =
                Cmd.OfAsync.either
                    Api.rules.testRule
                    (model.RuleFormPattern, model.RuleFormPatternType, model.RuleFormTargetField, model.RuleFormTestInput)
                    (Ok >> RulePatternTested)
                    (fun ex -> Error (RulesError.InvalidPattern (model.RuleFormPattern, ex.Message)) |> RulePatternTested)
            model, cmd, NoOp

    | RulePatternTested (Ok matches) ->
        let resultText = if matches then "✅ Pattern matches!" else "❌ Pattern does not match"
        { model with RuleFormTestResult = Some resultText }, Cmd.none, NoOp

    | RulePatternTested (Error err) ->
        { model with RuleFormTestResult = Some $"⚠️ {rulesErrorToString err}" }, Cmd.none, NoOp

    | SaveRule ->
        match model.RuleFormCategoryId with
        | None ->
            model, Cmd.none, ShowToast ("Please select a category", ToastWarning)
        | Some categoryId ->
            if String.IsNullOrWhiteSpace(model.RuleFormName) then
                model, Cmd.none, ShowToast ("Please enter a rule name", ToastWarning)
            elif String.IsNullOrWhiteSpace(model.RuleFormPattern) then
                model, Cmd.none, ShowToast ("Please enter a pattern", ToastWarning)
            else
                let payeeOverride = if String.IsNullOrWhiteSpace(model.RuleFormPayeeOverride) then None else Some model.RuleFormPayeeOverride
                if model.IsNewRule then
                    let nextPriority =
                        match model.Rules with
                        | Success rules -> (rules |> List.map (fun r -> r.Priority) |> List.fold max 0) + 1
                        | _ -> 1
                    let request : RuleCreateRequest = {
                        Name = model.RuleFormName
                        Pattern = model.RuleFormPattern
                        PatternType = model.RuleFormPatternType
                        TargetField = model.RuleFormTargetField
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
                    { model with RuleSaving = true }, cmd, NoOp
                else
                    match model.EditingRule with
                    | Some rule ->
                        let request : RuleUpdateRequest = {
                            Id = rule.Id
                            Name = Some model.RuleFormName
                            Pattern = Some model.RuleFormPattern
                            PatternType = Some model.RuleFormPatternType
                            TargetField = Some model.RuleFormTargetField
                            CategoryId = Some categoryId
                            PayeeOverride = payeeOverride
                            Priority = None
                            Enabled = Some model.RuleFormEnabled
                        }
                        let cmd =
                            Cmd.OfAsync.either
                                Api.rules.updateRule
                                request
                                RuleSaved
                                (fun ex -> Error (RulesError.DatabaseError ("update", ex.Message)) |> RuleSaved)
                        { model with RuleSaving = true }, cmd, NoOp
                    | None -> model, Cmd.none, NoOp

    | RuleSaved (Ok _) ->
        let action = if model.IsNewRule then "created" else "updated"
        let emptyForm = emptyRuleForm ()
        { model with
            EditingRule = None
            IsNewRule = false
            RuleFormName = emptyForm.Name
            RuleFormPattern = emptyForm.Pattern
            RuleFormPatternType = emptyForm.PatternType
            RuleFormTargetField = emptyForm.TargetField
            RuleFormCategoryId = emptyForm.CategoryId
            RuleFormPayeeOverride = emptyForm.PayeeOverride
            RuleFormEnabled = emptyForm.Enabled
            RuleFormTestInput = emptyForm.TestInput
            RuleFormTestResult = emptyForm.TestResult
            RuleSaving = false
        }, Cmd.ofMsg LoadRules, ShowToast ($"Rule {action} successfully", ToastSuccess)

    | RuleSaved (Error err) ->
        { model with RuleSaving = false }, Cmd.none, ShowToast (rulesErrorToString err, ToastError)

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
