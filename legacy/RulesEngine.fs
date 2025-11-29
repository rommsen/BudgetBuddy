module Comdirect2YNAB.RulesEngine

open System
open System.Text.RegularExpressions
open FsToolkit.ErrorHandling
open FsHttp
open Thoth.Json.Net

type CategoryInfo = { Id: Guid; Name: string }

// Helper to normalize category names for case-insensitive comparison
// and handling of common variations (trims whitespace, folds umlauts).
let private normalizeCategoryName (name: string) =
    name.Trim().ToLowerInvariant()
        // Basic umlaut folding, can be expanded if needed
        .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")

/// --- Decoder definieren ---
let private categoryInfoDecoder : Decoder<CategoryInfo> =
  Decode.object (fun get ->
    { Id   = get.Required.Field "id" Decode.guid
      Name = get.Required.Field "name" Decode.string }
  )

/// wir interessieren uns nur für das Array unter data.category_groups[].categories
let private categoriesResponseDecoder : Decoder<CategoryInfo list> =
  Decode.field "data" (
    Decode.field "category_groups" (
      Decode.list (Decode.field "categories" (Decode.list categoryInfoDecoder))
      |> Decode.map List.concat
    )
  )

let parseCategoriesResponse (rawJson: string) : Result<Map<string, Guid>, string> =
    Decode.fromString categoriesResponseDecoder rawJson
    |> Result.map (fun cats ->
        cats
        |> List.map (fun ci -> normalizeCategoryName ci.Name, ci.Id)
        |> Map.ofList
        )
    |> Result.mapError (fun e -> $"Failed to parse categories. JSON decode failed: {e}")

/// --- Neue fetchCategories mit FsHttp + Thoth ---
let fetchCategories (token: string) (budgetId: string) : Async<Result<Map<string, Guid>, string>> =

  asyncResult {
    if String.IsNullOrWhiteSpace budgetId then
      return! Error "YNAB budget ID not set or empty."

    let url = $"https://api.youneedabudget.com/v1/budgets/{budgetId}/categories"

    // 1) Ruf die API via FsHttp auf
    let! rawJson =
      async {
        let! resp =
          http {
            GET url
            Authorization $"Bearer {token}"
            Accept "application/json"
          }
          |> Request.sendAsync

        match resp |> Response.toResult with
        | Ok okResp ->
            let! body = okResp |> Response.toTextAsync
            return Ok body
        | Error errResp ->
            let! err = errResp |> Response.toTextAsync
            return Error $"HTTP {int errResp.statusCode}: {err}"
      }


    // printfn "[debug] ▶ RAW YNAB JSON:\n%s" rawJson

    return! parseCategoriesResponse rawJson
  }

type CompiledRule = {
    Regex: Regex
    CategoryId: Guid
}

let compileRules (rulesConfig: YamlConfig.RulesConfig) (categoryMap: Map<string, Guid>) : Result<CompiledRule list, string> =
    let mutable errors = []
    let compiledRules =
        rulesConfig.Rules
        |> List.choose (fun rule ->
            let normalizedCategoryName = normalizeCategoryName rule.Category
            match categoryMap |> Map.tryFind normalizedCategoryName with
            | Some categoryId ->
                try
                    // Case-insensitive regex matching
                    let regex = Regex(rule.Match, RegexOptions.IgnoreCase)
                    Some { Regex = regex; CategoryId = categoryId }
                with ex ->
                    errors <- $"Invalid regex '{rule.Match}' for category '{rule.Category}': {ex.Message}" :: errors
                    None
            | None ->
                errors <- $"Category '{rule.Category}' not found or ambiguous in YNAB." :: errors
                None
        )

    if List.isEmpty errors then
        Ok compiledRules
    else
        Error (String.concat "" (List.rev errors))


let classify (compiledRules: CompiledRule list) (transactionMemo: string option) : Guid option =
  transactionMemo
  |> Option.bind (fun memo ->
       compiledRules
       |> List.tryFind (fun rule -> rule.Regex.IsMatch memo)
       |> Option.map (fun rule -> rule.CategoryId)
     )
