module TestSequencingGuardTests

// Wächter für `testSequenced` und `testSequencedGroup` (infrastructure-x7p2e). Beide Markierungen kosten Laufzeit:
// `testSequenced` hält einen Fall an, bis alle anderen fertig sind, eine Gruppe reiht ihre Fälle hintereinander.
// Jede Markierung braucht darum direkt darüber einen Kommentar, der sagt, was die Fälle teilen. Kommentarzeilen
// und Text in String-Literalen zählen nicht als Markierung.

open System.IO
open System.Text.RegularExpressions
open Expecto

let private testsDir = __SOURCE_DIRECTORY__

let private stringLiteral = Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled)
let private marker = Regex(@"\btestSequenced(Group)?\b", RegexOptions.Compiled)

/// Die Zeilennummern (ab 1) aller Markierungen in `source`, über denen kein Kommentar steht.
let markersWithoutReason (source: string) : int list =
    let lines = source.Replace("\r\n", "\n").Split('\n')
    [ for i in 0 .. lines.Length - 1 do
          let trimmed = lines[i].TrimStart()
          if not (trimmed.StartsWith "//") && marker.IsMatch(stringLiteral.Replace(lines[i], "\"\"")) then
              let above = if i = 0 then "" else lines[i - 1].TrimStart()
              if not (above.StartsWith "//") then yield i + 1 ]

[<Tests>]
let tests =
    testList "Struktur-Wächter: Grund über jeder sequenziellen Liste (infrastructure-x7p2e)" [
        testCase "jede Markierung in src/Tests hat direkt darüber einen Kommentar" <| fun () ->
            let found =
                Directory.EnumerateFiles(testsDir, "*.fs")
                |> Seq.sort
                |> Seq.collect (fun path ->
                    markersWithoutReason (File.ReadAllText path) |> Seq.map (fun line -> $"{Path.GetFileName path}:{line}"))
                |> Seq.toList
            Expect.isGreaterThan (Directory.EnumerateFiles(testsDir, "*.fs") |> Seq.length) 100 "findet die Testdateien"
            Expect.equal found [] "Markierungen ohne Kommentar mit dem Grund"

        testCase "Gegenprobe: eine Markierung ohne Kommentar wird gemeldet, mit Kommentar nicht" <| fun () ->
            let source =
                String.concat "\n"
                    [ "let a ="
                      "    testSequenced"
                      "    <| testList \"A\" []"
                      "let b ="
                      "    // Geteilt: die Zähler dieses Moduls."
                      "    testSequencedGroup TestGroups.InMemoryDb <| testList \"B\" []"
                      "let c ="
                      "    [ testCase \"x\" ignore ]"
                      "    |> List.map (testSequencedGroup \"C\")" ]
            Expect.equal (markersWithoutReason source) [ 2; 9 ] "a und c ohne Grund, b mit"

        testCase "Gegenprobe: Kommentare und String-Literale zählen nicht als Markierung" <| fun () ->
            let source =
                String.concat "\n"
                    [ "let d ="
                      "    // Befehle gegen die DB (`testSequenced`, Schema-Init je Testfall)."
                      "    let text = \"    testSequenced\\n\" + \"<| testList\""
                      "    text" ]
            Expect.equal (markersWithoutReason source) [] "nichts gemeldet"
    ]
