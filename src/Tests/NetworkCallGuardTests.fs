module NetworkCallGuardTests

// Netz-Wächter (infrastructure-am805, Souls W1): ein Testfall, der YNAB oder Comdirect
// wirklich erreicht, muss hinter dem Schalter `RUN_INTEGRATION_TESTS` stehen.
//
// Was als Netzaufruf zählt:
//   * ein HttpClient ohne eingespeisten Handler (`new HttpClient()`), ein echter
//     `HttpClientHandler`/`SocketsHttpHandler`, `Request.sendAsync` (FsHttp),
//   * die echten Hosts von YNAB und Comdirect als Text,
//   * ein Aufruf einer Client-Funktion, die selbst ins Netz geht. Diese Menge wird aus
//     den Quellen von `YnabClient.fs`, `ComdirectClient.fs` und `ComdirectAuthSession.fs`
//     abgeleitet: eine Funktion geht ins Netz, wenn ihr Rumpf eine der Markierungen oben
//     enthält oder eine solche Funktion aufruft (transitiv). Funktionen mit eingespeistem
//     `HttpClient` (z. B. `sendReviewMark`) gehen damit nicht ins Netz.
//
// Geprüft wird je Testfall (`testCase` bis zum Ende seines eingerückten Blocks). Ein
// Testfall mit Netzaufruf ist in Ordnung, wenn er `RUN_INTEGRATION_TESTS` oder
// `shouldRunIntegrationTests` enthält. Ein Netzaufruf außerhalb eines Testfalls
// (Hilfsfunktion auf Modulebene) ist immer ein Verstoß.
//
// TAN-Wächter (banking-import-cx4b7): `RUN_INTEGRATION_TESTS` reicht für eine Push-TAN nicht.
// Ein Testfall, der eine Challenge startet oder bestätigt, muss zusätzlich hinter
// `COMDIRECT_PROBE` stehen. Drei falsche Validierungen sperren den Bankzugang, ein normaler
// Integrationslauf darf deshalb keine auslösen.
//
// Bekannte Grenze: ein Netzaufruf in einer Hilfsfunktion, die ihren Namen nicht aus den
// Client-Modulen hat und auf Modulebene ohne Markierung steht, sieht der Wächter nur über
// die Markierungen, nicht über den Aufrufgraphen der Testdateien.

open System
open System.IO
open System.Text.RegularExpressions
open Expecto

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private ownFileName = "NetworkCallGuardTests.fs"

/// Client-Module, deren Funktionen ins Netz gehen können.
let private clientModules = [ "YnabClient"; "ComdirectClient"; "ComdirectAuthSession" ]

let private directMarker =
    Regex(
        @"Request\.sendAsync|new\s+(?:System\.Net\.Http\.)?HttpClient\s*\(\s*\)|new\s+(?:System\.Net\.Http\.)?(?:HttpClientHandler|SocketsHttpHandler)\s*\(|createHttpClient\s*\(|api\.youneedabudget\.com|api\.ynab\.com|api\.comdirect\.de"
    )

/// Markierungen für die Ableitung in den Client-Quellen: ohne die Hosts, denn dort steht
/// der Host als Konstante (`baseUrl`), die auch Funktionen mit eingespeistem Client lesen.
let private clientSendMarker =
    Regex(
        @"Request\.sendAsync|new\s+(?:System\.Net\.Http\.)?HttpClient\s*\(\s*\)|new\s+(?:System\.Net\.Http\.)?(?:HttpClientHandler|SocketsHttpHandler)\s*\(|createHttpClient\s*\("
    )

let private topLevelLet = Regex(@"^let\s+((?:private\s+|internal\s+|inline\s+|rec\s+)*)([A-Za-z_][A-Za-z0-9_']*)")

let private isCommentLine (line: string) = line.TrimStart().StartsWith("//")

/// Netzfunktionen je Client-Modul, aus den Quelltexten abgeleitet. Eingabe: Modulname
/// und Quelltext. Ergebnis: Modulname -> öffentliche Netzfunktionen.
let deriveNetworkFunctions (sources: (string * string) list) : Map<string, Set<string>> =
    // (Modul, Name, öffentlich, Rumpf)
    let definitions =
        [ for moduleName, source in sources do
            let lines = source.Split('\n') |> Array.filter (isCommentLine >> not)
            let mutable current : (string * bool * Text.StringBuilder) option = None
            let defs = Collections.Generic.List<string * string * bool * string>()
            let flush () =
                current |> Option.iter (fun (n, pub, sb) -> defs.Add((moduleName, n, pub, sb.ToString())))
            for line in lines do
                let m = topLevelLet.Match line
                if m.Success then
                    flush ()
                    let isPublic = not (m.Groups[1].Value.Contains "private")
                    current <- Some (m.Groups[2].Value, isPublic, Text.StringBuilder(line).Append('\n'))
                elif line.Length > 0 && not (Char.IsWhiteSpace line[0]) then
                    flush ()
                    current <- None
                else
                    current |> Option.iter (fun (_, _, sb) -> sb.Append(line).Append('\n') |> ignore)
            flush ()
            yield! defs ]
    let mutable network =
        definitions |> List.filter (fun (_, _, _, body) -> clientSendMarker.IsMatch body) |> List.map (fun (_, n, _, _) -> n) |> Set.ofList
    let mutable changed = true
    while changed do
        let callsNetwork (name: string, body: string) =
            network |> Set.exists (fun n -> n <> name && Regex.IsMatch(body, $@"(?<![A-Za-z0-9_']){Regex.Escape n}(?![A-Za-z0-9_'])"))
        let added =
            definitions
            |> List.filter (fun (_, n, _, body) -> not (network.Contains n) && callsNetwork (n, body))
            |> List.map (fun (_, n, _, _) -> n)
        changed <- not added.IsEmpty
        network <- Set.union network (Set.ofList added)
    definitions
    |> List.filter (fun (_, n, pub, _) -> pub && network.Contains n)
    |> List.groupBy (fun (m, _, _, _) -> m)
    |> List.map (fun (m, defs) -> m, defs |> List.map (fun (_, n, _, _) -> n) |> Set.ofList)
    |> Map.ofList

/// Zeilen ohne Kommentare: `//`-Zeilen und `(* ... *)`-Blöcke werden zu Leerzeilen bzw.
/// Leerstellen, die Zeilennummern bleiben erhalten.
let private withoutComments (source: string) : string[] =
    let mutable inBlock = false
    source.Split('\n')
    |> Array.map (fun raw ->
        let line = raw.TrimEnd('\r')
        let sb = Text.StringBuilder()
        let mutable k = 0
        while k < line.Length do
            if inBlock then
                if k + 1 < line.Length && line[k] = '*' && line[k + 1] = ')' then
                    inBlock <- false
                    k <- k + 2
                else
                    k <- k + 1
            elif k + 2 < line.Length && line[k] = '(' && line[k + 1] = '*' && line[k + 2] <> ')' then
                inBlock <- true
                k <- k + 2
            elif k + 2 = line.Length && line[k] = '(' && line[k + 1] = '*' then
                inBlock <- true
                k <- k + 2
            else
                sb.Append(line[k]) |> ignore
                k <- k + 1
        let kept = sb.ToString()
        if isCommentLine kept then "" else kept)

/// String-Literale leeren: ein Funktionsname in einem Testnamen ist kein Aufruf.
let private stringLiteral = Regex("\"(?:[^\"\\\\]|\\\\.)*\"")

let private testCaseStart = Regex(@"^(\s*)(?:testCase|testCaseAsync|testTask|testProperty|ftestCase|ptestCase)\b")

let private hasSwitch (text: string) =
    text.Contains "RUN_INTEGRATION_TESTS" || text.Contains "shouldRunIntegrationTests"

/// Verstöße einer Testdatei: Zeilennummer und Zeile jedes Netzaufrufs, der in keinem
/// Testfall mit Schalter steht.
let private callPatternsIn (lines: string[]) (functions: Map<string, Set<string>>) : Regex list =
    let opened =
        clientModules
        |> List.filter (fun m -> lines |> Array.exists (fun l -> Regex.IsMatch(l, $@"^\s*open\s+(?:Server\.)?{m}\s*$")))
    let alternation (names: Set<string>) = names |> Seq.map Regex.Escape |> String.concat "|"
    let callPatterns =
        [ for KeyValue (m, names) in functions do
            if not names.IsEmpty then
                yield Regex($@"\b{m}\.(?:{alternation names})(?![A-Za-z0-9_'])")
                if List.contains m opened then
                    yield Regex($@"(?<![A-Za-z0-9_'.])(?:{alternation names})(?![A-Za-z0-9_'])(?!\s*=[^=>])") ]
    callPatterns

let private isCallLine (patterns: Regex list) (line: string) =
    let code = stringLiteral.Replace(line, "\"\"")
    patterns |> List.exists (fun r -> r.IsMatch code)

/// Zeilen mit `isFinding`, die in keinem Testfall stehen, dessen Text `hasGuard` erfüllt.
let private findUnguarded (hasGuard: string -> bool) (isFinding: string -> bool) (lines: string[]) : (int * string) list =
    // Segment je Testfall: von der Startzeile bis zur nächsten nicht leeren Zeile mit
    // gleicher oder geringerer Einrückung.
    let segmentOf = Array.create lines.Length -1
    let mutable i = 0
    let mutable segment = 0
    while i < lines.Length do
        let m = testCaseStart.Match lines[i]
        if m.Success then
            let indent = m.Groups[1].Value.Length
            segmentOf[i] <- segment
            let mutable j = i + 1
            let indentOf (l: string) = l.Length - l.TrimStart().Length
            while j < lines.Length && (String.IsNullOrWhiteSpace lines[j] || indentOf lines[j] > indent) do
                segmentOf[j] <- segment
                j <- j + 1
            segment <- segment + 1
            i <- j
        else
            i <- i + 1
    let switched =
        [ for s in 0 .. segment - 1 ->
            s, (lines |> Array.indexed |> Array.filter (fun (k, _) -> segmentOf[k] = s) |> Array.map snd |> String.concat "\n" |> hasGuard) ]
        |> Map.ofList
    [ for k in 0 .. lines.Length - 1 do
        if isFinding lines[k] then
            let ok = segmentOf[k] >= 0 && switched[segmentOf[k]]
            if not ok then yield k + 1, lines[k].Trim() ]

let findViolations (networkFunctions: Map<string, Set<string>>) (source: string) : (int * string) list =
    let lines = withoutComments source
    let patterns = callPatternsIn lines networkFunctions
    findUnguarded hasSwitch (fun line -> directMarker.IsMatch line || isCallLine patterns line) lines

/// Funktionen, die eine Challenge starten oder bestätigen.
let private tanFunctions =
    Map.ofList [
        "ComdirectClient", Set.ofList [ "requestTanChallenge"; "activateSession"; "startAuthFlow"; "completeAuthFlow" ]
        "ComdirectAuthSession", Set.ofList [ "startAuth"; "confirmTan" ] ]

let private hasProbeSwitch (text: string) =
    text.Contains "COMDIRECT_PROBE" || text.Contains "selectedProbePhase"

/// Verstöße einer Testdatei: Aufrufe, die eine TAN auslösen, ohne `COMDIRECT_PROBE`.
let findTanViolations (source: string) : (int * string) list =
    let lines = withoutComments source
    findUnguarded hasProbeSwitch (isCallLine (callPatternsIn lines tanFunctions)) lines

let private liveNetworkFunctions () =
    clientModules
    |> List.map (fun m -> m, File.ReadAllText(Path.Combine(repoRoot, "src", "Server", m + ".fs")))
    |> deriveNetworkFunctions

[<Tests>]
let networkGuardTests =
    testList "Netz-Wächter: Netzaufrufe in Tests nur hinter RUN_INTEGRATION_TESTS (infrastructure-am805)" [

        testCase "die abgeleiteten Netzfunktionen enthalten die echten Aufrufe, nicht die mit eingespeistem Client" <| fun () ->
            let net = liveNetworkFunctions ()
            let ynab = net |> Map.tryFind "YnabClient" |> Option.defaultValue Set.empty
            let comdirect = net |> Map.tryFind "ComdirectClient" |> Option.defaultValue Set.empty
            let session = net |> Map.tryFind "ComdirectAuthSession" |> Option.defaultValue Set.empty
            for name in [ "getBudgets"; "validateToken"; "updateReviewMark"; "getMonth" ] do
                Expect.contains ynab name $"YnabClient.{name} geht ins Netz"
            for name in [ "sendReviewMark"; "buildReviewMarkBody"; "getMonthWith"; "assignUrl" ] do
                Expect.isFalse (ynab.Contains name) $"YnabClient.{name} geht nicht selbst ins Netz"
            Expect.contains comdirect "startAuthFlow" "ComdirectClient.startAuthFlow geht ins Netz"
            for name in [ "refreshTokens"; "initOAuthWith"; "getAccountsWith" ] do
                Expect.isFalse (comdirect.Contains name) $"ComdirectClient.{name} nutzt den eingespeisten Client"
            Expect.contains session "startAuth" "ComdirectAuthSession.startAuth geht ins Netz (über startAuthFlow)"

        testCase "Gegenbeweis: ein Testfall mit Netzaufruf ohne Schalter wird gemeldet" <| fun () ->
            let net = Map.ofList [ "YnabClient", Set.ofList [ "getBudgets" ] ]
            let source =
                String.concat "\n" [
                    "module Beispiel"
                    "open Server.YnabClient"
                    "let tests ="
                    "    testList \"x\" ["
                    "        testCase \"ohne Schalter\" <| fun () ->"
                    "            getBudgets \"token\" |> Async.RunSynchronously |> ignore"
                    "    ]" ]
            let violations = findViolations net source
            Expect.equal (violations |> List.map fst) [ 6 ] "der Aufruf ohne Schalter ist ein Verstoß"

        testCase "ein Testfall mit Schalter ist erlaubt" <| fun () ->
            let net = Map.ofList [ "YnabClient", Set.ofList [ "getBudgets" ] ]
            let source =
                String.concat "\n" [
                    "module Beispiel"
                    "open Server.YnabClient"
                    "let private shouldRunIntegrationTests () = Environment.GetEnvironmentVariable \"RUN_INTEGRATION_TESTS\" = \"true\""
                    "let tests ="
                    "    testList \"x\" ["
                    "        testCase \"mit Schalter\" <| fun () ->"
                    "            if not (shouldRunIntegrationTests()) then Tests.skiptest \"aus\""
                    "            getBudgets \"token\" |> Async.RunSynchronously |> ignore"
                    "        testCase \"qualifiziert ohne Schalter\" <| fun () ->"
                    "            Server.YnabClient.getBudgets \"token\" |> ignore"
                    "    ]" ]
            let violations = findViolations net source
            Expect.equal (violations |> List.map fst) [ 10 ] "nur der zweite Testfall ohne Schalter ist ein Verstoß"

        testCase "HttpClient mit eingespeistem Handler ist kein Netzaufruf, ohne Handler schon" <| fun () ->
            let source =
                String.concat "\n" [
                    "let tests ="
                    "    testList \"x\" ["
                    "        testCase \"Fake\" <| fun () ->"
                    "            use client = new HttpClient(handler)"
                    "            ()"
                    "        testCase \"echt\" <| fun () ->"
                    "            use client = new System.Net.Http.HttpClient()"
                    "            client.GetStringAsync(\"https://api.comdirect.de/\") |> ignore"
                    "    ]" ]
            let violations = findViolations Map.empty source
            Expect.equal (violations |> List.map fst) [ 7; 8 ] "nur der Testfall ohne Handler"

        testCase "Funktionsnamen in Testnamen und in Blockkommentaren sind keine Aufrufe" <| fun () ->
            let net = Map.ofList [ "YnabClient", Set.ofList [ "getBudgets" ] ]
            let source =
                String.concat "\n" [
                    "open Server.YnabClient"
                    "let tests ="
                    "    testList \"x\" ["
                    "        testCase \"nach gescheitertem getBudgets-Aufruf\" <| fun () ->"
                    "            (*"
                    "            YnabClient.getBudgets token"
                    "            *)"
                    "            ()"
                    "    ]" ]
            Expect.isEmpty (findViolations net source) "kein Verstoß"

        testCase "kein Test unter src/Tests erreicht YNAB oder Comdirect ohne Schalter" <| fun () ->
            let net = liveNetworkFunctions ()
            let violations =
                Directory.GetFiles(Path.Combine(repoRoot, "src", "Tests"), "*.fs")
                |> Array.filter (fun f -> Path.GetFileName f <> ownFileName)
                |> Array.sort
                |> Array.collect (fun f ->
                    findViolations net (File.ReadAllText f)
                    |> List.map (fun (line, text) -> $"{Path.GetFileName f}:{line}: {text}")
                    |> Array.ofList)
            Expect.isEmpty violations (String.concat "\n  " ("Netzaufrufe ohne RUN_INTEGRATION_TESTS-Schalter:" :: List.ofArray violations))

        testCase "Gegenbeweis: eine TAN-Anfrage nur hinter RUN_INTEGRATION_TESTS wird gemeldet" <| fun () ->
            let source =
                String.concat "\n" [
                    "open Server.ComdirectClient"
                    "let tests ="
                    "    testList \"x\" ["
                    "        testCase \"nur Integrationsschalter\" <| fun () ->"
                    "            if not (shouldRunIntegrationTests()) then Tests.skiptest \"aus\""
                    "            startAuthFlow credentials keys |> Async.RunSynchronously |> ignore"
                    "        testCase \"mit Sonden-Schalter\" <| fun () ->"
                    "            if selectedProbePhase().IsNone then Tests.skiptest \"aus\""
                    "            startAuthFlow credentials keys |> Async.RunSynchronously |> ignore"
                    "    ]" ]
            Expect.equal (findTanViolations source |> List.map fst) [ 6 ] "nur der Testfall ohne Sonden-Schalter"

        // Gegenprobe zur Sonde: ein Lauf mit RUN_INTEGRATION_TESTS=true ohne COMDIRECT_PROBE
        // erreicht keine TAN-Anfrage. Der zweite Teil dieser Aussage steht in ComdirectProbeTests.
        testCase "kein Test unter src/Tests löst ohne COMDIRECT_PROBE eine TAN aus" <| fun () ->
            let violations =
                Directory.GetFiles(Path.Combine(repoRoot, "src", "Tests"), "*.fs")
                |> Array.filter (fun f -> Path.GetFileName f <> ownFileName)
                |> Array.sort
                |> Array.collect (fun f ->
                    findTanViolations (File.ReadAllText f)
                    |> List.map (fun (line, text) -> $"{Path.GetFileName f}:{line}: {text}")
                    |> Array.ofList)
            Expect.isEmpty violations (String.concat "\n  " ("TAN-Aufrufe ohne COMDIRECT_PROBE:" :: List.ofArray violations))
    ]
