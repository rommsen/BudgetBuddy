module ModuleLevelDbInitGuardTests

// Mechanisierte Durchsetzung der Konvention aus infra-sdad8 (siehe README-Delta und
// ADR 0033): `initializeDatabase()`/`initializeLedgerSchema()` dürfen in einem
// `[<Tests>]`-gebundenen Wert nur innerhalb einer Closure (typischerweise einer
// `testCase`-Closure) laufen, nie direkt im `let`-Body davor. Genau das war der Fehler,
// den infra-sdad8 behoben hat: `LedgerMirrorTests.fs` und
// `PersistenceTypeConversionTests.fs` riefen diese Funktionen in fünf `[<Tests>]`-Werten
// als Anweisung vor der eigentlichen `Test`-Struktur auf. F# wertet Modul-Level-Werte
// beim ersten Zugriff auf das Modul aus - und genau diesen Zugriff löst Expectos eigene
// `[<Tests>]`-Entdeckung per Reflection aus, bevor überhaupt ein einzelner Testfall
// läuft. Der Wächter aus infra-za7fq (`TestDatabaseGuardTests.fs`) meldet einen
// Rückfall auf die Datei-Datenbank nur, er verhindert ihn nicht: im Umgehungslauf war
// die Datei-Datenbank bereits vollständig angelegt, bevor der Wächter-Testfall lief.
//
// Verifikations-Iteration 1 dieser Aufgabe lieferte genau diesen Fix (Modul-Level-Aufrufe
// in eine lokal geschattete `testCase`-Closure verschoben), aber ohne Durchsetzung: die
// Konvention stand nur in Prosa. Dieser Test schließt die Lücke, indem er jeden
// `[<Tests>]`-Block unter `src/Tests/` auf genau dieses Muster durchsucht.
//
// Zwei dokumentierte Fallstricke aus vorherigen Iterationen (design-system-bq4tz,
// dieselbe F#-Modul-Startup-Eigenheit) werden hier bewusst vermieden:
//   1. "Ein grep an Spalte 0 verfehlt verschachtelte Module" - dieser Scan ankert nicht
//      an einer festen Einrücktiefe. `[<Tests>]` wird unabhängig von seiner Einrückung
//      gefunden, und innerhalb eines Blocks wird die Closure-Zugehörigkeit über einen
//      Einrücktiefen-Stack relativ zur öffnenden `fun ... ->`-Zeile bestimmt, nicht über
//      eine absolute Spalte. Der Test "erkennt das Bug-Muster auch innerhalb eines
//      verschachtelten Moduls" unten belegt das.
//   2. "Ein Scan, der jede Einrückung zulässt, trifft gewöhnliche lokale Bindungen in
//      Funktionsrümpfen" - dieser Scan sucht nicht nach "irgendeinem eingerückten
//      Ausdruck", sondern ausschließlich innerhalb von `[<Tests>]`-Blöcken nach den zwei
//      konkreten Funktionsnamen, und selbst dort nur nach Aufrufen, die außerhalb jeder
//      `fun ... ->`-Closure stehen. Eine gewöhnliche lokale Bindung mit anderem Namen
//      wird nie geprüft; ein Aufruf innerhalb einer Closure wird nie geprüft.
//
// Bewiesen falsifizierbar (siehe Outcome-Text des Workers): das historische Bug-Muster
// wurde testweise in eine echte Datei zurückgespielt, der Test schlug rot fehl, danach
// wieder zurückgesetzt und der Test war wieder grün.

open System.IO
open System.Text.RegularExpressions
open Expecto

let private repoRoot =
    let testDir = __SOURCE_DIRECTORY__
    Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

let private testsDir = Path.Combine(repoRoot, "src", "Tests")

/// Diese Datei selbst enthält die beiden Funktionsnamen als Text in Kommentaren und in
/// String-Literalen (siehe Testfälle unten) - sie muss vom eigenen Scan ausgenommen
/// werden, sonst meldet sie sich selbst als Verstoß.
let private ownFileName = "ModuleLevelDbInitGuardTests.fs"

let private testsAttributeLine = Regex(@"^\s*\[<Tests>\]\s*$")

let private initCallPattern =
    Regex(@"(?:[A-Za-z_][A-Za-z0-9_.]*\.)?\b(?:initializeDatabase|initializeLedgerSchema)\s*\(\s*\)")

/// Eine Zeile "öffnet" eine Closure für die nachfolgenden, stärker eingerückten Zeilen,
/// wenn sie (nach Trimmen) mit `fun ... ->` endet - das deckt sowohl `fun () ->` allein
/// als auch `testCase name (fun () ->` ab.
let private lineOpensClosureAtEnd (line: string) =
    Regex.IsMatch(line, @"\bfun\b[^\r\n]*->\s*$")

/// Position der ersten `fun ... ->`-Öffnung innerhalb derselben Zeile (falls vorhanden),
/// unabhängig davon, ob die Zeile mit `->` endet. Wird gebraucht, um einen Aufruf auf
/// derselben Zeile wie eine neu geöffnete Closure korrekt als "geschützt" zu behandeln,
/// z. B. `fun () -> initializeDatabase ()` in einer einzigen Zeile.
let private funArrowIndexOnSameLine (line: string) =
    let m = Regex.Match(line, @"\bfun\b[^\r\n]*?->")
    if m.Success then Some(m.Index + m.Length) else None

let private leadingSpaces (line: string) =
    let mutable i = 0
    while i < line.Length && line.[i] = ' ' do
        i <- i + 1
    i

let private isBlankOrComment (line: string) =
    let trimmed = line.TrimStart()
    trimmed = "" || trimmed.StartsWith("//")

/// Findet alle `[<Tests>]`-Blöcke in `source`. Ein Block beginnt bei der Zeile nach dem
/// Attribut (der `let <name> = ...`-Zeile) und reicht bis zum nächsten `[<Tests>]` oder
/// zum Dateiende. Rückgabe: (1-basierte Zeilennummer der ersten Blockzeile, Blocktext).
let findTestsBlocks (source: string) : (int * string) list =
    let lines = source.Replace("\r\n", "\n").Split('\n')

    let attributeLineIndices =
        [ for i in 0 .. lines.Length - 1 do
              if testsAttributeLine.IsMatch(lines.[i]) then
                  yield i ]

    attributeLineIndices
    |> List.map (fun attrIdx ->
        let blockStart0 = attrIdx + 1

        let blockEndExclusive0 =
            match attributeLineIndices |> List.filter (fun other -> other > attrIdx) with
            | next :: _ -> next
            | [] -> lines.Length

        let blockLines =
            if blockStart0 >= blockEndExclusive0 then
                [||]
            else
                lines.[blockStart0 .. blockEndExclusive0 - 1]

        (blockStart0 + 1, String.concat "\n" blockLines))

/// Prüft einen einzelnen `[<Tests>]`-Blocktext auf einen `initializeDatabase()`/
/// `initializeLedgerSchema()`-Aufruf außerhalb jeder Closure. Arbeitet über einen
/// Einrücktiefen-Stack statt über eine feste Spalte (siehe Dateikopf): eine Zeile gilt
/// als innerhalb einer Closure, solange ihre Einrücktiefe größer ist als die der zuletzt
/// geöffneten `fun ... ->`-Zeile.
///
/// Rückgabe bei Verstoß: `Some (1-basierte Zeilennummer innerhalb des Blocks, getrimmte
/// Zeile)`. `None`, wenn kein unbewachter Aufruf gefunden wird.
let hasModuleLevelDbInit (blockText: string) : (int * string) option =
    let lines = blockText.Replace("\r\n", "\n").Split('\n')

    let rec go (remaining: (int * string) list) (closureStack: int list) =
        match remaining with
        | [] -> None
        | (lineNumber, line) :: rest ->
            if isBlankOrComment line then
                go rest closureStack
            else
                let indent = leadingSpaces line
                // Closures schließen, deren öffnende Zeile auf oder über dem aktuellen
                // Einrückniveau liegt (wir haben uns aus ihnen "herausgedented").
                let stillOpen = closureStack |> List.filter (fun openIndent -> openIndent < indent)

                let violation =
                    if List.isEmpty stillOpen then
                        // Nur der Teil der Zeile VOR einer auf derselben Zeile neu
                        // geöffneten Closure zählt noch als "außerhalb" - ein
                        // `fun () -> initializeDatabase ()` in einer einzigen Zeile ist
                        // bewacht.
                        let searchText =
                            match funArrowIndexOnSameLine line with
                            | Some idx -> line.Substring(0, idx)
                            | None -> line

                        let m = initCallPattern.Match(searchText)
                        if m.Success then Some(lineNumber, line.Trim()) else None
                    else
                        None

                match violation with
                | Some _ -> violation
                | None ->
                    let nextStack =
                        if lineOpensClosureAtEnd line then
                            indent :: stillOpen
                        else
                            stillOpen

                    go rest nextStack

    lines |> Array.mapi (fun i l -> (i + 1, l)) |> Array.toList |> fun ls -> go ls []

/// Alle Verstöße unter `src/Tests/` (die eigene Datei ausgenommen, siehe `ownFileName`).
/// Rückgabe: (Dateipfad, absolute 1-basierte Zeilennummer, getrimmte Verstoßzeile).
let scanTestsDirectory () : (string * int * string) list =
    Directory.GetFiles(testsDir, "*.fs")
    |> Array.filter (fun f -> Path.GetFileName f <> ownFileName)
    |> Array.collect (fun path ->
        let source = File.ReadAllText path

        findTestsBlocks source
        |> List.choose (fun (blockStartLine, blockText) ->
            match hasModuleLevelDbInit blockText with
            | Some(relativeLine, snippet) -> Some(path, blockStartLine + relativeLine - 1, snippet)
            | None -> None)
        |> List.toArray)
    |> Array.toList

[<Tests>]
let moduleLevelDbInitGuardTests =
    testList
        "Wächter: kein initializeDatabase()/initializeLedgerSchema() außerhalb einer Closure (infra-sdad8)"
        [

          testCase "der Suchlauf findet mindestens einen [<Tests>]-Block (Wächter gegen den Wächter)"
          <| fun () ->
              // Bricht die Namenskonvention (z. B. ein Attribut-Rename), liefert der
              // Suchlauf oben eine leere Liste und der Test darunter bestünde grün, ohne
              // je etwas geprüft zu haben.
              let totalBlocks =
                  Directory.GetFiles(testsDir, "*.fs")
                  |> Array.filter (fun f -> Path.GetFileName f <> ownFileName)
                  |> Array.sumBy (fun f -> findTestsBlocks (File.ReadAllText f) |> List.length)

              Expect.isGreaterThan
                  totalBlocks
                  0
                  "Keine [<Tests>]-Blöcke unter src/Tests/ gefunden - Regex oder Verzeichnis prüfen"

          testCase "kein Testmodul ruft initializeDatabase()/initializeLedgerSchema() außerhalb einer Closure auf"
          <| fun () ->
              let violations = scanTestsDirectory ()

              if not (List.isEmpty violations) then
                  let details =
                      violations
                      |> List.map (fun (file, line, snippet) -> $"{file}:{line}: {snippet}")
                      |> String.concat "; "

                  failtestf
                      "Modul-Level-DB-Init außerhalb einer Closure gefunden (muss in eine testCase-Closure verschoben werden, siehe infra-sdad8): %s"
                      details

          testCase "erkennt das historische Bug-Muster (Aufruf vor jeder Closure) als Verstoß"
          <| fun () ->
              // Nachgebildet aus dem tatsächlichen Fund in LedgerMirrorTests.fs vor
              // infra-sdad8 (git show 440086b:src/Tests/LedgerMirrorTests.fs).
              let buggy =
                  "[<Tests>]\n"
                  + "let mirrorRunTests =\n"
                  + "    initializeLedgerSchema ()\n"
                  + "    global.Persistence.initializeDatabase ()\n"
                  + "\n"
                  + "    testSequenced\n"
                  + "    <| testList \"Ledger Spiegel-Lauf\" [\n"
                  + "        testCase \"irgendein Test\" <| fun () ->\n"
                  + "            Expect.isTrue true \"\"\n"
                  + "    ]\n"

              match findTestsBlocks buggy with
              | [ (_, blockText) ] ->
                  Expect.isSome
                      (hasModuleLevelDbInit blockText)
                      "das historische Bug-Muster muss als Verstoß erkannt werden"
              | blocks -> failtestf "erwartete genau einen [<Tests>]-Block, gefunden: %d" (List.length blocks)

          testCase "erkennt das reparierte Muster (Aufruf innerhalb der geschatteten testCase-Closure) nicht als Verstoß"
          <| fun () ->
              // Nachgebildet aus dem tatsächlichen Fix in LedgerMirrorTests.fs (infra-sdad8,
              // Iteration 1).
              let repaired =
                  "[<Tests>]\n"
                  + "let mirrorRunTests =\n"
                  + "    let testCase name body =\n"
                  + "        testCase name (fun () ->\n"
                  + "            initializeLedgerSchema ()\n"
                  + "            global.Persistence.initializeDatabase ()\n"
                  + "            body ())\n"
                  + "\n"
                  + "    testSequenced\n"
                  + "    <| testList \"Ledger Spiegel-Lauf\" [\n"
                  + "        testCase \"irgendein Test\" <| fun () ->\n"
                  + "            Expect.isTrue true \"\"\n"
                  + "    ]\n"

              match findTestsBlocks repaired with
              | [ (_, blockText) ] ->
                  Expect.isNone
                      (hasModuleLevelDbInit blockText)
                      "das reparierte Muster darf nicht als Verstoß erkannt werden"
              | blocks -> failtestf "erwartete genau einen [<Tests>]-Block, gefunden: %d" (List.length blocks)

          testCase "erkennt das Bug-Muster auch innerhalb eines verschachtelten Moduls (kein Spalte-0-Anker)"
          <| fun () ->
              let nested =
                  "module Outer =\n"
                  + "    module Inner =\n"
                  + "        [<Tests>]\n"
                  + "        let nestedTests =\n"
                  + "            initializeDatabase ()\n"
                  + "\n"
                  + "            testList \"verschachtelt\" [\n"
                  + "                testCase \"x\" <| fun () -> Expect.isTrue true \"\"\n"
                  + "            ]\n"

              match findTestsBlocks nested with
              | [ (_, blockText) ] ->
                  Expect.isSome
                      (hasModuleLevelDbInit blockText)
                      "verschachtelte Module dürfen den Wächter nicht umgehen"
              | blocks -> failtestf "erwartete genau einen [<Tests>]-Block, gefunden: %d" (List.length blocks) ]
