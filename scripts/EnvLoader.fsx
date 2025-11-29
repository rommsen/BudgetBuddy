// ============================================
// .env File Loader for F# Scripts
// ============================================

module EnvLoader

open System
open System.IO

/// Loads environment variables from a .env file
let loadEnv (envPath: string) =
    if not (File.Exists envPath) then
        printfn $"Warning: .env file not found at {envPath}"
        printfn "Please copy .env.example to .env and fill in your credentials"
        Map.empty
    else
        File.ReadAllLines(envPath)
        |> Array.filter (fun line ->
            not (String.IsNullOrWhiteSpace(line)) &&
            not (line.TrimStart().StartsWith("#"))
        )
        |> Array.choose (fun line ->
            match line.Split('=', 2) with
            | [| key; value |] ->
                let key = key.Trim()
                let value = value.Trim()
                Some (key, value)
            | _ -> None
        )
        |> Map.ofArray

/// Gets the project root directory (assumes script is in scripts/ folder)
let getProjectRoot () =
    let scriptDir = __SOURCE_DIRECTORY__
    Directory.GetParent(scriptDir).FullName

/// Loads .env file from project root
let loadProjectEnv () =
    let projectRoot = getProjectRoot()
    let envPath = Path.Combine(projectRoot, ".env")
    loadEnv envPath

/// Gets a required environment variable from the map
let getRequired (envVars: Map<string, string>) (key: string) =
    match envVars.TryFind key with
    | Some value -> value
    | None ->
        printfn $"ERROR: Required environment variable '{key}' not found in .env"
        failwith $"Missing required environment variable: {key}"

/// Gets an optional environment variable from the map
let getOptional (envVars: Map<string, string>) (key: string) =
    envVars.TryFind key

/// Prints loaded environment variables (masks secrets)
let printEnvInfo (envVars: Map<string, string>) =
    printfn "Loaded environment variables:"
    for kvp in envVars do
        let maskedValue =
            if kvp.Value.Length > 8 then
                kvp.Value.Substring(0, 4) + "..." + kvp.Value.Substring(kvp.Value.Length - 4)
            else
                "***"
        printfn $"  {kvp.Key} = {maskedValue}"
    printfn ""
