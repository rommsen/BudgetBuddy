module BackendPortGuardTests

// Mechanisierte Durchsetzung der Konvention aus infra-jh7g9 (siehe README-Delta und
// ADR 0034): `app.Run` in src/Server/Program.fs darf niemals mit einem URL-Argument
// aufgerufen werden. Ein explizites Argument überschreibt ASPNETCORE_URLS und macht
// die Variable wirkungslos, genau der Fehler, den diese Aufgabe behoben hat
// (compose.yamls ASPNETCORE_URLS-Zeile sah aus, als kontrolliere sie den Port, tat es
// aber nicht).
//
// Verifikations-Iteration 1 dieser Aufgabe lieferte genau diesen Fix (app.Run() ohne
// Argument, Server.ServerConfig.resolveUrls als reine Auflösung), aber ohne
// Durchsetzung: die Konvention stand nur in Prosa und im README-Delta. Dieser Test
// schließt die Lücke, indem er den tatsächlichen Quelltext von Program.fs liest und
// jeden `app.Run(...)`-Aufruf mit nicht-leerem Argument laut ablehnt.
//
// Zwei dokumentierte Fallstricke aus vorherigen Iterationen (design-system-bq4tz,
// ModuleLevelDbInitGuardTests.fs) werden hier bewusst vermieden bzw. sind hier gar
// nicht anwendbar:
//   1. Kein Einrückungs-/Spalten-Anker nötig: dieser Scan prüft nicht auf
//      Closure-Zugehörigkeit, sondern sucht `app.Run(` als Text irgendwo im
//      (kommentarbereinigten) Quelltext, unabhängig von Einrückung.
//   2. Kommentare, die den Aufruf selbst als Text ERWÄHNEN (Program.fs enthält
//      genau das: "app.Run() unten wird ohne Argument aufgerufen ..." und
//      "app.Run() unten bindet auf genau diesen Wert"), dürfen den Scan nicht als
//      Verstoß ODER als falsche Bestätigung zählen. Deshalb wird pro Zeile der Teil
//      ab dem ersten ECHTEN `//`-Kommentarbeginn abgeschnitten, BEVOR nach
//      `app.Run(` gesucht wird. "Echt" heißt hier: string-literal-bewusst
//      (commentStartIndex unten trackt, ob die Zeile gerade innerhalb eines
//      "..."-Literals steht). Eine naive erste Fassung suchte einfach den ersten
//      `//` in der Zeile: das brach genau an dem Fall, den dieser Test erkennen
//      MUSS: `app.Run("http://0.0.0.0:5081")` enthält selbst ein `//` (in
//      "http://"), innerhalb des String-Literals. Ohne String-Bewusstsein hätte
//      der naive Scan den Aufruf an dieser Stelle abgeschnitten und den Verstoß
//      nicht mehr gesehen (der Wächter hätte sich also genau an seinem eigenen
//      Zweck selbst blind gemacht). Reine Kommentarzeilen (abgeschnittener Rest ist
//      leer) und Code mit angehängtem Inline-Kommentar nach einem String-Literal
//      werden beide korrekt behandelt (siehe Testfälle unten).
//
// Bewiesen falsifizierbar (siehe Outcome-Text des Workers): `app.Run()` wurde
// testweise durch `app.Run("http://0.0.0.0:5081")` ersetzt, der Test schlug rot fehl,
// danach zurückgesetzt und der Test war wieder grün.

open System.IO
open System.Text.RegularExpressions
open Expecto

let private repoRoot =
    let testDir = __SOURCE_DIRECTORY__
    Directory.GetParent(Directory.GetParent(testDir).FullName).FullName

let private programFsPath =
    Path.Combine(repoRoot, "src", "Server", "Program.fs")

let private appRunPattern =
    Regex(@"\bapp\.Run\s*\(\s*([^)]*?)\s*\)")

/// String-literal-bewusste Suche nach dem Beginn eines `//`-Zeilenkommentars
/// (siehe Dateikopf, Fallstrick 2): verfolgt beim Durchlaufen der Zeichen, ob
/// die Zeile gerade innerhalb eines `"..."`-Literals steht (mit `\"` als
/// Escape), und akzeptiert `//` nur außerhalb eines solchen Literals als
/// Kommentarbeginn. `None`, wenn die Zeile keinen Kommentar enthält.
let private commentStartIndex (line: string) : int option =
    let mutable inString = false
    let mutable result = None
    let mutable i = 0
    while result.IsNone && i < line.Length do
        let c = line.[i]
        if inString then
            if c = '\\' && i + 1 < line.Length then
                i <- i + 1
            elif c = '"' then
                inString <- false
        else if c = '"' then
            inString <- true
        elif c = '/' && i + 1 < line.Length && line.[i + 1] = '/' then
            result <- Some i

        i <- i + 1

    result

/// Schneidet den Kommentarteil jeder Zeile ab, BEVOR nach `app.Run(...)`
/// gesucht wird (siehe Dateikopf). Rückgabe: (1-basierte Zeilennummer,
/// getrimmtes Argument innerhalb der Klammern) für jeden im
/// Nicht-Kommentar-Teil gefundenen Aufruf.
let findAppRunCalls (source: string) : (int * string) list =
    let lines = source.Replace("\r\n", "\n").Split('\n')
    [ for i in 0 .. lines.Length - 1 do
          let line = lines.[i]

          let codePart =
              match commentStartIndex line with
              | Some idx -> line.Substring(0, idx)
              | None -> line

          for m in appRunPattern.Matches(codePart) do
              yield (i + 1, m.Groups.[1].Value.Trim()) ]

/// Verstöße: ein `app.Run(...)`-Aufruf mit nicht-leerem Argument. `app.Run()`
/// (leere Klammern) respektiert ASPNETCORE_URLS von selbst und ist erlaubt.
let violations (source: string) : (int * string) list =
    findAppRunCalls source
    |> List.filter (fun (_, arg) -> arg <> "")

[<Tests>]
let backendPortGuardTests =
    testList
        "Wächter: app.Run() in Program.fs bekommt niemals ein URL-Argument (infra-jh7g9)"
        [

          testCase "der Suchlauf findet mindestens einen app.Run-Aufruf in Program.fs (Wächter gegen den Wächter)"
          <| fun () ->
              // Bricht ein künftiges Umbenennen (z. B. app statt builder.Build()) oder
              // eine andere Startsequenz den Scan, liefert findAppRunCalls eine leere
              // Liste und der Test darunter bestünde grün, ohne je etwas geprüft zu
              // haben.
              let source = File.ReadAllText programFsPath
              Expect.isNonEmpty
                  (findAppRunCalls source)
                  "Kein app.Run(...)-Aufruf in Program.fs gefunden, Regex oder Pfad prüfen"

          testCase "Program.fs ruft app.Run() tatsächlich ohne Argument auf"
          <| fun () ->
              let source = File.ReadAllText programFsPath
              let found = violations source

              if not (List.isEmpty found) then
                  let details =
                      found
                      |> List.map (fun (line, arg) -> $"Program.fs:{line}: app.Run({arg})")
                      |> String.concat "; "

                  failtestf
                      "app.Run() mit URL-Argument gefunden (überschreibt ASPNETCORE_URLS und macht die Variable wirkungslos, siehe infra-jh7g9/ADR 0034): %s"
                      details

          testCase "erkennt ein String-Literal-Argument an app.Run als Verstoß"
          <| fun () ->
              let buggy = "app.Run(\"http://0.0.0.0:5081\")\n"
              Expect.isNonEmpty (violations buggy) "ein URL-Literal an app.Run muss als Verstoß erkannt werden"

          testCase "erkennt ein Variablen-Argument an app.Run als Verstoß"
          <| fun () ->
              let buggy = "app.Run(urls)\n"
              Expect.isNonEmpty (violations buggy) "ein Variablen-Argument an app.Run muss als Verstoß erkannt werden"

          testCase "erkennt app.Run() ohne Argument nicht als Verstoß"
          <| fun () ->
              let repaired = "app.Run()\n"
              Expect.isEmpty (violations repaired) "app.Run() ohne Argument darf nicht als Verstoß erkannt werden"

          testCase "ignoriert Erwähnungen von app.Run(...) in reinen Kommentarzeilen"
          <| fun () ->
              // Genau das Muster, das in Program.fs tatsächlich vorkommt: ein
              // Kommentar, der den (korrekten) Aufruf beschreibt und dabei
              // `app.Run()` als Text nennt.
              let commented = "    // app.Run() unten wird ohne Argument aufgerufen und respektiert ASPNETCORE_URLS\n"
              Expect.isEmpty (violations commented) "eine reine Kommentarzeile darf keinen Verstoß auslösen"

          testCase "erkennt ein URL-Argument trotz angehängtem Inline-Kommentar (Kommentar versteckt den Verstoß nicht)"
          <| fun () ->
              let buggy = "app.Run(\"http://0.0.0.0:5081\") // TODO konfigurierbar machen\n"
              Expect.isNonEmpty
                  (violations buggy)
                  "ein Inline-Kommentar nach dem Aufruf darf ein URL-Argument nicht vor dem Scan verstecken" ]
