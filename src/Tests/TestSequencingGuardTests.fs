module TestSequencingGuardTests

// Wächter für `testSequenced` und `testSequencedGroup` (infrastructure-x7p2e). Beide Markierungen kosten Laufzeit:
// `testSequenced` hält einen Fall an, bis alle anderen fertig sind, eine Gruppe reiht ihre Fälle hintereinander.
// Jede Markierung braucht darum direkt darüber einen Kommentar, der sagt, was die Fälle teilen. Eine Gruppe bekommt
// ihren Namen immer als Konstante aus `TestGroups`: ein Text oder ein lokaler Wert am Aufruf macht eine zweite Gruppe
// auf, die parallel zur ersten läuft, obwohl beide dasselbe teilen (so lief „Server-Log“ neben der DB-Gruppe).
// Kommentarzeilen und Text in String-Literalen zählen nicht als Markierung.

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

let private groupMarker = Regex(@"\btestSequencedGroup\b(.*)$", RegexOptions.Compiled)
let private fromTestGroups = Regex(@"^\s*TestGroups\.\w", RegexOptions.Compiled)

/// Die Zeilennummern (ab 1) aller `testSequencedGroup`, deren Argument keine Konstante `TestGroups.<Name>` ist.
/// Steht der Name in der nächsten Zeile, zählt die.
let groupsNotFromTestGroups (source: string) : int list =
    let lines = source.Replace("\r\n", "\n").Split('\n')
    [ for i in 0 .. lines.Length - 1 do
          if not (lines[i].TrimStart().StartsWith "//") then
              let m = groupMarker.Match(stringLiteral.Replace(lines[i], "\"\""))
              if m.Success then
                  let argument =
                      if m.Groups[1].Value.Trim() = "" && i + 1 < lines.Length then lines[i + 1] else m.Groups[1].Value
                  if not (fromTestGroups.IsMatch argument) then yield i + 1 ]

let private allTestFiles () =
    Directory.EnumerateFiles(testsDir, "*.fs") |> Seq.sort |> Seq.toList

[<Tests>]
let tests =
    testList "Struktur-Wächter: Grund über jeder sequenziellen Liste, Gruppenname aus TestGroups (infrastructure-x7p2e)" [
        testCase "jede Markierung in src/Tests hat direkt darüber einen Kommentar" <| fun () ->
            let found =
                allTestFiles ()
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

        testCase "Jede Gruppe in src/Tests bekommt ihren Namen als Konstante aus TestGroups" <| fun () ->
            let found =
                allTestFiles ()
                |> Seq.collect (fun path ->
                    groupsNotFromTestGroups (File.ReadAllText path) |> Seq.map (fun line -> $"{Path.GetFileName path}:{line}"))
                |> Seq.toList
            Expect.equal found [] "Gruppen mit Text oder lokalem Wert als Namen"

        testCase "Gegenprobe: ein Text oder ein lokaler Wert als Gruppenname wird gemeldet, eine Konstante aus TestGroups nicht" <| fun () ->
            let source =
                String.concat "\n"
                    [ "let a ="
                      "    testSequencedGroup \"Server-Log\""
                      "let b ="
                      "    testSequencedGroup ServerLogGroup <| testList \"B\" []"
                      "let c ="
                      "    testSequencedGroup TestGroups.InMemoryDb <| testList \"C\" []"
                      "let d ="
                      "    [ testCase \"x\" ignore ] |> List.map (testSequencedGroup TestGroups.InMemoryDb)"
                      "let e ="
                      "    testSequencedGroup"
                      "        TestGroups.InMemoryDb"
                      "let f ="
                      "    // testSequencedGroup \"nur im Kommentar\""
                      "    let text = \"testSequencedGroup \\\"im Text\\\"\""
                      "    [ testCase \"y\" ignore ] |> List.map (testSequencedGroup name)" ]
            Expect.equal (groupsNotFromTestGroups source) [ 2; 4; 15 ] "a, b und der lokale Wert in f gemeldet, c, d, e und Kommentar oder Text nicht"

        testCase "Gegenprobe: Kommentare und String-Literale zählen nicht als Markierung" <| fun () ->
            let source =
                String.concat "\n"
                    [ "let d ="
                      "    // Befehle gegen die DB (`testSequenced`, Schema-Init je Testfall)."
                      "    let text = \"    testSequenced\\n\" + \"<| testList\""
                      "    text" ]
            Expect.equal (markersWithoutReason source) [] "nichts gemeldet"
    ]
