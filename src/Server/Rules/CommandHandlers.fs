/// Regeln anlegen, ändern und importieren (Context `categorization`). YNAB kommt als Port
/// herein, damit Tests die Fehlerfälle ohne Live-Dienst durchspielen; Uhr und Ids als Parameter.
module Server.Rules.CommandHandlers

open System
open Thoth.Json.Net
open Shared.Domain
open Server.RulesEngine
open Server.Validation

/// Was die Abläufe von YNAB brauchen. Der Fehler ist der Grund als Satz für den Nutzer, auch
/// wenn YNAB gar nicht eingerichtet ist.
type YnabPort = {
    FetchCategories: unit -> Async<Result<YnabCategory list, string>>
}

/// Baut den Port aus Einstellungen und YNAB-Client. Ohne Token oder Budget gibt es einen Grund
/// statt eines stillen Ersatzwerts.
let ynabPort
    (getSetting: string -> Async<string option>)
    (getCategories: string -> YnabBudgetId -> Async<YnabResult<YnabCategory list>>)
    : YnabPort =
    { FetchCategories = fun () -> async {
        let! token = getSetting "ynab_token"
        let! budgetId = getSetting "ynab_default_budget_id"

        match token, budgetId with
        | Some token, Some budgetId ->
            let! categories = getCategories token (YnabBudgetId budgetId)
            return categories |> Result.mapError Server.Budgeting.CommandHandlers.failureReason
        | _ -> return Error "YNAB ist nicht eingerichtet (Token oder Budget fehlt)"
      } }

/// Der Name einer Kategorie aus YNAB. Es gibt keinen Platzhalter: scheitert das Nachschlagen
/// oder fehlt die Kategorie, ist das ein Fehler.
let lookupCategoryName (port: YnabPort) (YnabCategoryId id as categoryId) : Async<RulesResult<string>> =
    async {
        match! port.FetchCategories () with
        | Error reason -> return Error (RulesError.CategoryLookupFailed reason)
        | Ok categories ->
            match categories |> List.tryFind (fun category -> category.Id = categoryId) with
            | Some category -> return Ok category.Name
            | None -> return Error (RulesError.CategoryNotFound id)
    }

let private compileError (rule: Rule) (reason: string) =
    RulesError.InvalidPattern (rule.Pattern, reason)

/// Legt eine Regel an. Name und Muster werden vor dem YNAB-Aufruf geprüft, die Priorität
/// vergibt der Server, damit die neue Regel vorn steht.
let createRule (port: YnabPort) (now: DateTime) (request: NewRule) : Async<RulesResult<Rule>> =
    async {
        match validateNewRule request with
        | Error errors -> return Error (RulesError.ValidationFailed errors)
        | Ok valid ->
            let draft = {
                Id = RuleId (Guid.NewGuid())
                Name = valid.Name
                Pattern = valid.Pattern
                PatternType = valid.PatternType
                TargetField = valid.TargetField
                CategoryId = valid.CategoryId
                CategoryName = ""
                PayeeOverride = valid.PayeeOverride
                Priority = 0
                Enabled = true
                CreatedAt = now
                UpdatedAt = now
            }

            match compileRule draft with
            | Error reason -> return Error (compileError draft reason)
            | Ok _ ->
                match! lookupCategoryName port valid.CategoryId with
                | Error error -> return Error error
                | Ok categoryName ->
                    try
                        let! priority = Persistence.Rules.nextPriority ()
                        let rule = { draft with CategoryName = categoryName; Priority = priority }
                        do! Persistence.Rules.insertRule rule
                        return Ok rule
                    with ex ->
                        return Error (RulesError.DatabaseError ("create_rule", ex.Message))
    }

let private applyPayeeOverride (change: PayeeOverrideChange) (current: string option) : string option =
    match change with
    | PayeeOverrideChange.Keep -> current
    | PayeeOverrideChange.Set payee -> Some payee
    | PayeeOverrideChange.Remove -> None

/// Ändert eine Regel. Der Dialog schickt die Kategorie immer mit; YNAB wird nur gefragt, wenn
/// sie sich wirklich ändert, sonst ließe sich bei einem YNAB-Ausfall nicht mal der Name ändern.
let changeRule (port: YnabPort) (now: DateTime) (request: RuleChange) : Async<RulesResult<Rule>> =
    async {
        match validateRuleChange request with
        | Error errors -> return Error (RulesError.ValidationFailed errors)
        | Ok valid ->
            match! Persistence.Rules.getRuleById valid.Id with
            | None ->
                let (RuleId id) = valid.Id
                return Error (RulesError.RuleNotFound id)
            | Some existing ->
                let changed = {
                    existing with
                        Name = valid.Name |> Option.defaultValue existing.Name
                        Pattern = valid.Pattern |> Option.defaultValue existing.Pattern
                        PatternType = valid.PatternType |> Option.defaultValue existing.PatternType
                        TargetField = valid.TargetField |> Option.defaultValue existing.TargetField
                        CategoryId = valid.CategoryId |> Option.defaultValue existing.CategoryId
                        PayeeOverride = applyPayeeOverride valid.PayeeOverride existing.PayeeOverride
                        Priority = valid.Priority |> Option.defaultValue existing.Priority
                        Enabled = valid.Enabled |> Option.defaultValue existing.Enabled
                        UpdatedAt = now
                }

                let patternChanged = valid.Pattern.IsSome || valid.PatternType.IsSome

                match (if patternChanged then compileRule changed |> Result.map ignore else Ok ()) with
                | Error reason -> return Error (compileError changed reason)
                | Ok () ->
                    let! categoryName =
                        if changed.CategoryId <> existing.CategoryId then
                            lookupCategoryName port changed.CategoryId
                        else
                            async { return Ok existing.CategoryName }

                    match categoryName with
                    | Error error -> return Error error
                    | Ok name ->
                        let rule = { changed with CategoryName = name }
                        try
                            do! Persistence.Rules.updateRule rule
                            return Ok rule
                        with ex ->
                            return Error (RulesError.DatabaseError ("update_rule", ex.Message))
    }

/// Alle Fehler einer Importdatei, je Regel mit ihrem Namen (ohne Namen mit ihrer Position).
let private importErrors (rules: Rule list) : string list =
    rules
    |> List.indexed
    |> List.collect (fun (index, rule) ->
        let label = if String.IsNullOrWhiteSpace rule.Name then $"Regel {index + 1}" else rule.Name
        let validationErrors =
            match validateImportedRule rule with
            | Ok _ -> []
            | Error errors -> errors
        let compileErrors =
            match compileRule rule with
            | Ok _ -> []
            | Error reason -> [ reason ]
        validationErrors @ compileErrors |> List.map (fun error -> $"{label}: {error}"))

/// Importiert eine Regeldatei, alles oder nichts. Kategorie und Name kommen aus der Datei, YNAB
/// wird nicht gefragt. Bestehende Regeln bleiben, die Dateireihenfolge bleibt der Gleichstand.
let importRules (newId: unit -> Guid) (now: DateTime) (json: string) : Async<RulesResult<int>> =
    async {
        match Decode.Auto.fromString<Rule list>(json) with
        | Error reason -> return Error (RulesError.ValidationFailed [ $"Die Importdatei ist ungültig: {reason}" ])
        | Ok rules ->
            match importErrors rules with
            | _ :: _ as errors -> return Error (RulesError.ValidationFailed errors)
            | [] ->
                let imported =
                    rules |> List.map (fun rule -> { rule with Id = RuleId (newId ()); CreatedAt = now; UpdatedAt = now })
                try
                    do! Persistence.Rules.insertRules imported
                    return Ok imported.Length
                with ex ->
                    return Error (RulesError.DatabaseError ("import_rules", ex.Message))
    }
