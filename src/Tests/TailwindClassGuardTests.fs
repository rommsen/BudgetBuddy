module TailwindClassGuardTests

// design-system-thtpp: Wächter, der `prop.className`-Klassen-Tokens in
// src/Client gegen den echten Tailwind-4-Compiler und gegen die in
// styles.css selbst definierten Selektoren prüft (siehe ADR 0031: die
// DaisyUI-Klassen, die diese Aufgabe motiviert haben, waren genau dieses
// Muster: eine Klasse, die es nicht gibt, wirft keinen Fehler, sie fehlt
// einfach lautlos in der Ausgabe).
//
// Die maßgebliche Implementierung liegt als Node-Skript unter
// scripts/tailwind-class-guard.mjs, nicht als F#-Nachbau der Tailwind-Logik:
// nur Tailwinds eigener Compiler (@tailwindcss/node) kann autoritativ
// beantworten, ob ein Klassenname eine echte Utility ist. Dieser Test ruft
// das echte Skript per Node-Subprozess auf (analog
// FontFaceRegressionTests.fs' runRewriteAbsoluteAssetUrls und
// BackendPortGuardTests.fs), statt seine Logik in F# nachzubilden.

open System
open System.Diagnostics
open System.IO
open Expecto

let private repoRoot =
    let testDir = __SOURCE_DIRECTORY__
    Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

let private guardScriptPath =
    Path.Combine(repoRoot, "scripts", "tailwind-class-guard.mjs")

/// Ruft scripts/tailwind-class-guard.mjs per Node-Subprozess auf. `clientDir`
/// überschreibt nur die Scan-Wurzel (`--client-dir`); styles.css/Tokens.fs
/// bleiben immer die echten Dateien unter src/Client: der Wächter soll ja
/// gerade GEGEN das echte Stylesheet prüfen, auch wenn nur ein Scratch-
/// Verzeichnis nach Phantom-Klassen durchsucht wird.
let private runGuard (clientDir: string option) : int * string * string =
    let psi = ProcessStartInfo("node", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    psi.ArgumentList.Add(guardScriptPath)

    match clientDir with
    | Some dir ->
        psi.ArgumentList.Add("--client-dir")
        psi.ArgumentList.Add(dir)
    | None -> ()

    use proc = Process.Start(psi)
    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()
    (proc.ExitCode, stdout, stderr)

[<Tests>]
let tailwindClassGuardTests =
    testList
        "Wächter: prop.className-Klassen ohne Tailwind-Utility oder styles.css-Definition (design-system-thtpp)"
        [

          testCase "der echte Lauf gegen src/Client ist grün (Exit-Code 0)" <| fun () ->
              let exitCode, stdout, stderr = runGuard None

              Expect.equal
                  exitCode
                  0
                  (sprintf
                      "Wächter meldete undefinierte Klassen oder eine veraltete Ausnahmeliste (siehe scripts/tailwind-class-guard-exceptions.json).\nstdout:\n%s\nstderr:\n%s"
                      stdout
                      stderr)

          testCase "Falsifizierbarkeit: eine Phantom-Klasse in einem Scratch-Verzeichnis lässt den Wächter mit Exit-Code ≠ 0 scheitern, danach ist er wieder grün"
          <| fun () ->
              let scratchDir =
                  Path.Combine(Path.GetTempPath(), sprintf "tailwind-class-guard-falsifiability-%s" (Guid.NewGuid().ToString("N")))

              Directory.CreateDirectory(scratchDir) |> ignore

              let phantomFilePath = Path.Combine(scratchDir, "Phantom.fs")
              let phantomClass = "bb-phantom-nicht-real"

              try
                  File.WriteAllText(
                      phantomFilePath,
                      sprintf
                          "module Phantom\n\nopen Feliz\n\nlet view () =\n    Html.div [ prop.className \"flex %s\" ]\n"
                          phantomClass
                  )

                  let buggyExitCode, buggyStdout, _ = runGuard (Some scratchDir)

                  Expect.notEqual buggyExitCode 0 "eine Phantom-Klasse muss den Wächter mit Exit-Code ≠ 0 scheitern lassen."
                  Expect.stringContains
                      buggyStdout
                      phantomClass
                      "die Meldung muss die Phantom-Klasse wörtlich benennen."

                  File.Delete(phantomFilePath)

                  let repairedExitCode, repairedStdout, repairedStderr = runGuard (Some scratchDir)

                  Expect.equal
                      repairedExitCode
                      0
                      (sprintf
                          "nach Entfernen der Phantom-Klasse muss der Wächter wieder grün sein.\nstdout:\n%s\nstderr:\n%s"
                          repairedStdout
                          repairedStderr)
              finally
                  if File.Exists(phantomFilePath) then
                      File.Delete(phantomFilePath)

                  Directory.Delete(scratchDir, true) ]
