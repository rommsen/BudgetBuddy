/// Regeln anlegen, ändern und wiederherstellen (Context `categorization`). YNAB kommt als Port
/// herein, damit Tests die Fehlerfälle ohne Live-Dienst durchspielen; Uhr und Ids als Parameter.
module Server.Rules.CommandHandlers

open System
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
            return categories |> Result.mapError ynabErrorText
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
                Enabled = valid.Enabled
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

/// Legt eine gelöschte Regel an ihrer alten Stelle wieder an („Rückgängig“, categorization-n3sxw). Kategorie und
/// Name kommen aus der Regel, YNAB wird nicht gefragt. Einfügen und neue Reihenfolge laufen in einer Transaktion:
/// es gibt keinen Zwischenstand, in dem die Regel an falscher Stelle steht. Die Antwort ist die ganze Liste, gelesen
/// vor dem Commit: ein DatabaseError heißt damit immer, dass nichts gespeichert wurde.
let restoreRule (request: RuleRestore) : Async<RulesResult<Rule list>> =
    async {
        match validateRuleRestore request with
        | Error errors -> return Error (RulesError.ValidationFailed errors)
        | Ok valid ->
            match compileRule valid.Rule with
            | Error reason -> return Error (compileError valid.Rule reason)
            | Ok _ ->
                try
                    let! rules = Persistence.Rules.restoreRuleAt valid.Rule valid.Position
                    return Ok rules
                with ex ->
                    return Error (RulesError.DatabaseError ("restore_rule", ex.Message))
    }
