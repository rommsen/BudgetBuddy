module Comdirect2YNAB.RulesManager

open System
open System.IO
open Config
open YamlConfig

type RulesError =
    | RulesFileNotFound of string
    | RulesParseError of string
    | CategoryFetchError of string
    | RulesCompileError of string

type RulesInfo = {
    RulesPath: string
    OriginalRules: YamlConfig.RulesConfig
    CompiledRules: RulesEngine.CompiledRule list
    CategoryMap: Map<string, Guid>
}

let private getDefaultRulesPath () =
    let isWindows = Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
    let configHome =
        if isWindows then
            Environment.GetFolderPath Environment.SpecialFolder.ApplicationData
        else
            let xdgConfigHome = Environment.GetEnvironmentVariable "XDG_CONFIG_HOME"
            if String.IsNullOrWhiteSpace xdgConfigHome then
                Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".config")
            else xdgConfigHome

    let appConfigFolder = Path.Combine(configHome, "comdirect2ynab")
    Directory.CreateDirectory(appConfigFolder) |> ignore
    Path.Combine(appConfigFolder, "rules.yml")

let getRulesPath (args: string array) =
    args 
    |> Array.tryFindIndex (fun arg -> arg.ToLowerInvariant() = "--rules")
    |> Option.bind (fun index -> 
        if args.Length > index + 1 then Some args.[index + 1] 
        else None)
    |> Option.defaultValue (getDefaultRulesPath())

let private createDefaultRulesFile (rulesPath: string) =
    let template = """# Default rules for Comdirect2YNAB
rules: []
"""
    try
        File.WriteAllText(rulesPath, template)
        Ok $"Created default rules file at '{rulesPath}'. Please edit it to add your rules."
    with
    | ex -> Error (RulesFileNotFound $"Could not create rules file: {ex.Message}")

let ensureRulesFileExists (rulesPath: string) =
    if File.Exists rulesPath then
        Ok ()
    else
        createDefaultRulesFile rulesPath |> Result.map ignore

let private parseRulesConfig (rulesPath: string) =
    YamlConfig.parseRulesFile rulesPath
    |> Result.mapError (fun errMsg -> RulesParseError errMsg)

let private fetchCategories (config: Config.Config) =
    async {
        if String.IsNullOrWhiteSpace config.Transfer.YNAB_Budget then
            return Error (CategoryFetchError "YNAB budget ID not set in configuration.")
        else
            try
                let! categoryMap = RulesEngine.fetchCategories config.YNAB_Api.Secret config.Transfer.YNAB_Budget
                return categoryMap |> Result.mapError CategoryFetchError
            with
            | ex -> return Error (CategoryFetchError $"Exception fetching YNAB categories: {ex.Message}")
    }

let private compileRules (rulesConfig: YamlConfig.RulesConfig) (categoryMap: Map<string, Guid>) =
    if List.isEmpty rulesConfig.Rules then
        Ok []
    else
        RulesEngine.compileRules rulesConfig categoryMap
        |> Result.mapError RulesCompileError

let loadAndCompileRules (config: Config.Config) (rulesPath: string) =
    async {
        match parseRulesConfig rulesPath with
        | Error parseError -> return Error parseError
        | Ok rulesConfig ->
            if List.isEmpty rulesConfig.Rules then
                return Ok []
            else
                match! fetchCategories config with
                | Error categoryError -> return Error categoryError
                | Ok categoryMap ->
                    return compileRules rulesConfig categoryMap
    }

let test config rulesPath =
    async {
        match! loadAndCompileRules config rulesPath with
        | Error error ->
            return 
                match error with
                | RulesFileNotFound msg -> Error $"Rules file not found: {msg}"
                | RulesParseError msg -> Error $"Rules parse error: {msg}"
                | CategoryFetchError msg -> Error $"Category fetch error: {msg}"
                | RulesCompileError msg -> Error $"Rules compile error: {msg}"

        | Ok compiledRules ->
            return Ok compiledRules
    }


let getRulesInfo (config: Config.Config) (rulesPath: string) =
    async {
        match parseRulesConfig rulesPath with
        | Error parseError -> return Error parseError
        | Ok rulesConfig ->
            match! fetchCategories config with
            | Error categoryError -> 
                return Ok { 
                    RulesPath = rulesPath
                    OriginalRules = rulesConfig
                    CompiledRules = []
                    CategoryMap = Map.empty
                }
            | Ok categoryMap ->
                match compileRules rulesConfig categoryMap with
                | Error compileError ->
                    return Ok { 
                        RulesPath = rulesPath
                        OriginalRules = rulesConfig
                        CompiledRules = []
                        CategoryMap = categoryMap
                    }
                | Ok compiledRules ->
                    return Ok { 
                        RulesPath = rulesPath
                        OriginalRules = rulesConfig
                        CompiledRules = compiledRules
                        CategoryMap = categoryMap
                    }
    }

let private formatRulesInfo (rulesInfo: RulesInfo) =
    let lines = [
        ""
        "--- Rules Information ---"
        $"Rules file path: {rulesInfo.RulesPath}"
        ""
        "Original Rules Configuration (from rules.yml):"
        
        if List.isEmpty rulesInfo.OriginalRules.Rules then
            "  No rules defined."
        else
            "  Rules:"
            
        yield!
            rulesInfo.OriginalRules.Rules
            |> List.mapi (fun i rule ->
                $"    {i+1}. Match: '{rule.Match}' -> Category: '{rule.Category}'")
        
        ""
        "Compiled Rules & Resolved IDs:"
        
        if List.isEmpty rulesInfo.CompiledRules then
            "  No rules were successfully compiled."
        else
            "  Compiled Rules:"
            
        yield!
            rulesInfo.CompiledRules
            |> List.mapi (fun i cr ->
                $"    {i+1}. Regex: '{cr.Regex.ToString()}' -> Category ID: {cr.CategoryId}")
        
        "--- End Rules Information ---"
    ]
    String.Join(Environment.NewLine, lines)

let showRulesInfo (config: Config.Config) (rulesPath: string) =
    async {
        match! getRulesInfo config rulesPath with
        | Error error ->
            let errorMsg = 
                match error with
                | RulesFileNotFound msg -> $"Rules file error: {msg}"
                | RulesParseError msg -> $"Rules parse error: {msg}"
                | CategoryFetchError msg -> $"Category fetch error: {msg}"
                | RulesCompileError msg -> $"Rules compile error: {msg}"
            Console.Error.WriteLine errorMsg
        | Ok rulesInfo ->
            let output = formatRulesInfo rulesInfo
            Console.WriteLine output
    }

let initializeRulesPath (args: string array) =
    let rulesPath = getRulesPath args
    match ensureRulesFileExists rulesPath with
    | Ok msg -> 
        Console.WriteLine($"Info: {msg}")
        rulesPath
    | Error (RulesFileNotFound msg) ->
        Console.WriteLine($"Warning: {msg}")
        rulesPath
    | Error _ -> rulesPath // This shouldn't happen with current error types
