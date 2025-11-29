namespace Comdirect2YNAB

open System
open System.IO
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

module YamlConfig =
    open FsToolkit.ErrorHandling

    [<CLIMutable>]
    type Rule = {
        Match: string
        Category: string
    }

    [<CLIMutable>]
    type RulesConfigInternal = {
        Rules: Collections.Generic.List<Rule>
    }

    type RulesConfig = {
        Rules : Rule list
    }

    // Module-level functions instead of static members
    let createRulesConfig (rules: Rule list) : RulesConfig =
        { Rules = rules }

    let private deserializeRules (yamlContent: string) : RulesConfig option =
        let deserializer =
            DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build()

        try
            let deserialized = deserializer.Deserialize<RulesConfigInternal>(yamlContent)
            Some { Rules = deserialized.Rules |> List.ofSeq }
        with
        | :? YamlDotNet.Core.YamlException as ex ->
            printfn "Error deserializing YAML: %s" ex.Message
            None
        | ex ->
            printfn "Unexpected error deserializing YAML: %s" ex.Message
            None

    let private validateRulesConfig (config: RulesConfig) : Result<RulesConfig, string> =
        let hasEmptyMatch = config.Rules |> List.exists (fun rule -> String.IsNullOrWhiteSpace rule.Match)
        let hasEmptyCategory = config.Rules |> List.exists (fun rule -> String.IsNullOrWhiteSpace rule.Category)
        
        match hasEmptyMatch, hasEmptyCategory with
        | true, _ -> Error "Invalid 'match' field in one or more rules: cannot be empty."
        | _, true -> Error "Invalid 'category' field in one or more rules: cannot be empty."
        | false, false -> Ok config

    let parseRulesFile (filePath: string) : Result<RulesConfig, string> =
        result {
            do! if File.Exists filePath then Ok () else Error $"Rules file not found at '{filePath}'"
            let yamlContent = File.ReadAllText filePath
            //printfn "[parseRulesFile] Content of rules file: %s" yamlContent
            let! rulesConfig =
                match deserializeRules yamlContent with
                | Some cfg -> Ok cfg
                | None -> Error "Failed to deserialize rules.yml. The file might be empty or malformed."
            return! validateRulesConfig rulesConfig
        }
        |> Result.mapError (fun err -> $"Error parsing rules file: {err}")

    let exitWithError (errorMessage: string) : 'a =
        eprintfn "Error: %s" errorMessage
        Environment.Exit(1)
        failwith "This line should never be reached"
