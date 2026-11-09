module StructureGuardTests

// Struktur-Wächter (infrastructure-ky2kh, Harness-Audit M6, Souls W3, W4, W7). Drei
// Prüfungen, die bisher nur als Prosa-Regel in `.claude/rules/` standen und dort auf
// Pfade zielten, die es so nicht gibt:
//
//   1. CLIMutable-Wächter (Reflection über die Server-Assembly): jeder F#-Record, dessen
//      Name auf `Row` endet, trägt `[<CLIMutable>]`. Dazu zwei Quelltext-Prüfungen, die
//      verhindern, dass die Namensregel umgangen wird: jedes Typargument eines Dapper-
//      Lesezugriffs (`Query<T>`, `QueryAsync<T>`, `QueryFirstOrDefault<T>`, ...) ist
//      entweder ein einfacher Typ (string, int64, ...) oder ein solcher Row-Record; und
//      jedes Option-Feld eines Row-Records hat einen inneren Typ, für den
//      `src/Server/Persistence.fs` einen `OptionHandler` registriert.
//   2. Domain-Wächter (Quelltext): `src/Server/**/Domain.fs` liest weder Uhr noch Zufall
//      und macht kein I/O (Datei, Netz, Datenbank, Konsole, Umgebung).
//   3. SQL-Wächter (Quelltext): in `src/Server/**/Persistence.fs` und
//      `src/Server/**/QueryHandlers.fs` kommt kein Wert per `$"..."`, `sprintf`,
//      `String.Format`, `+`/`^` oder `StringBuilder.Append` in einen SQL-Text. Werte gehen
//      als `@name`-Parameter hinein. Ein eingesetzter SQL-Baustein aus Konstanten braucht
//      einen kommentierten Eintrag in `sqlAllowList`.
//
// Kommentare zählen in allen Prüfungen nicht. String-Literale zählen im Domain-Wächter
// nicht; im SQL-Wächter entscheidet ihr Inhalt, ob ein Text SQL ist (Großbuchstaben-
// Schlüsselwörter wie SELECT, WHERE, AND, ORDER BY).
//
// Bekannte Grenzen: der SQL-Wächter sieht Verkettungen über `+`/`^` nur zwischen
// Literalen, Bezeichnern und Aufrufen mit Klammern (`f(x)`), nicht über Funktions-
// anwendung mit Leerzeichen (`f x + "..."` meldet `x`). Ein SQL-Text ohne Großbuchstaben-
// Schlüsselwort (etwa `$"date {dir}, id {dir}"`) gilt nicht als SQL.

open System
open System.IO
open System.Reflection
open System.Text
open System.Text.RegularExpressions
open Expecto
open Microsoft.FSharp.Reflection

let private repoRoot =
    Directory.GetParent(Directory.GetParent(__SOURCE_DIRECTORY__).FullName).FullName

let private serverDir = Path.Combine(repoRoot, "src", "Server")

/// Server-Quelldateien mit dem Dateinamen `fileName`, rekursiv, ohne `bin/` und `obj/`.
/// Ergebnis: Pfad relativ zu `src/Server` (mit `/`) und Inhalt.
let private serverSources (fileName: string) : (string * string) list =
    Directory.EnumerateFiles(serverDir, fileName, SearchOption.AllDirectories)
    |> Seq.map (fun p -> Path.GetRelativePath(serverDir, p).Replace('\\', '/'), p)
    |> Seq.filter (fun (rel, _) -> not (rel.StartsWith "bin/" || rel.StartsWith "obj/"))
    |> Seq.sortBy fst
    |> Seq.map (fun (rel, p) -> rel, File.ReadAllText p)
    |> Seq.toList

// ============================================================================
// Gemeinsamer Lexer: Kommentare entfernen, String-Literale herauslösen
// ============================================================================

/// Ein String-Literal aus dem Quelltext. `Dollars` ist die Zahl der `$` vor dem Literal
/// (0 = nicht interpoliert), `Text` der Inhalt ohne Anführungszeichen.
type SourceLiteral = { Index: int; Line: int; Dollars: int; Text: string }

/// Quelltext ohne Kommentare, jedes String-Literal ersetzt durch den Platzhalter
/// `\u0001<Index>\u0002`. Zeilenumbrüche aus mehrzeiligen Literalen und Blockkommentaren
/// bleiben erhalten, die Zeilennummern stimmen also. Zeichenliterale (`'"'`) werden zu
/// `' '`, damit ein Anführungszeichen darin kein Literal eröffnet.
let lex (source: string) : string * SourceLiteral list =
    let s = source.Replace("\r\n", "\n")
    let n = s.Length
    let at k = if k < n then s[k] else '\000'
    let isIdent (c: char) = Char.IsLetterOrDigit c || c = '_' || c = '\''
    let startsAt (k: int) (p: string) = k + p.Length <= n && String.CompareOrdinal(s, k, p, 0, p.Length) = 0
    let code = StringBuilder()
    let literals = ResizeArray<SourceLiteral>()
    let mutable line = 1
    let mutable i = 0
    while i < n do
        let c = s[i]
        // Präfix eines String-Literals: `$`, `$$`, `@`, `$@`, `@$`.
        let mutable j = i
        let mutable dollars = 0
        let mutable verbatim = false
        while j < n && (s[j] = '$' || s[j] = '@') do
            if s[j] = '$' then dollars <- dollars + 1 else verbatim <- true
            j <- j + 1
        let prefixOk = (j = i) || (at j = '"' && (i = 0 || not (isIdent s[i - 1])))
        if c = '/' && at (i + 1) = '/' then
            while i < n && s[i] <> '\n' do
                i <- i + 1
        elif c = '(' && at (i + 1) = '*' && at (i + 2) <> ')' then
            let mutable depth = 1
            i <- i + 2
            while i < n && depth > 0 do
                if s[i] = '(' && at (i + 1) = '*' && at (i + 2) <> ')' then
                    depth <- depth + 1
                    i <- i + 2
                elif s[i] = '*' && at (i + 1) = ')' then
                    depth <- depth - 1
                    i <- i + 2
                else
                    if s[i] = '\n' then
                        code.Append('\n') |> ignore
                        line <- line + 1
                    i <- i + 1
        elif at j = '"' && prefixOk then
            let startLine = line
            let content = StringBuilder()
            if startsAt j "\"\"\"" then
                let mutable k = j + 3
                while k < n && not (startsAt k "\"\"\"") do
                    content.Append(s[k]) |> ignore
                    k <- k + 1
                i <- k + 3
            elif verbatim then
                let mutable k = j + 1
                let mutable fin = false
                while k < n && not fin do
                    if s[k] = '"' && at (k + 1) = '"' then
                        content.Append('"') |> ignore
                        k <- k + 2
                    elif s[k] = '"' then
                        fin <- true
                        k <- k + 1
                    else
                        content.Append(s[k]) |> ignore
                        k <- k + 1
                i <- k
            else
                let mutable k = j + 1
                let mutable fin = false
                while k < n && not fin do
                    if s[k] = '\\' then
                        content.Append(s[k]).Append(at (k + 1)) |> ignore
                        k <- k + 2
                    elif s[k] = '"' then
                        fin <- true
                        k <- k + 1
                    else
                        content.Append(s[k]) |> ignore
                        k <- k + 1
                i <- k
            let text = content.ToString()
            let index = literals.Count
            literals.Add { Index = index; Line = startLine; Dollars = dollars; Text = text }
            code.Append('\u0001').Append(index).Append('\u0002') |> ignore
            for ch in text do
                if ch = '\n' then
                    code.Append('\n') |> ignore
                    line <- line + 1
        elif c = '\'' && (i = 0 || not (isIdent s[i - 1])) && (at (i + 1) = '\\' || at (i + 2) = '\'') then
            // Zeichenliteral: `'x'` oder `'\n'`, `'\''`, `'\u0001'`.
            if at (i + 1) = '\\' then
                let mutable k = i + 3
                while k < n && s[k] <> '\'' && s[k] <> '\n' do
                    k <- k + 1
                i <- k + 1
            else
                i <- i + 3
            code.Append("' '") |> ignore
        else
            if c = '\n' then line <- line + 1
            code.Append(c) |> ignore
            i <- i + 1
    code.ToString(), List.ofSeq literals

let private placeholder = Regex("\u0001(\\d+)\u0002")

/// Zeilennummer (ab 1) einer Position im gelexten Code.
let private lineAt (code: string) (pos: int) =
    let mutable count = 1
    for k in 0 .. pos - 1 do
        if code[k] = '\n' then count <- count + 1
    count

// ============================================================================
// 1. CLIMutable-Wächter
// ============================================================================

/// Die Row-Typen einer Typmenge: F#-Records (auch private oder interne), deren Name auf `Row` endet.
let rowRecords (types: Type seq) : Type list =
    types
    |> Seq.filter (fun t -> t.Name.EndsWith("Row", StringComparison.Ordinal))
    |> Seq.filter (fun t -> FSharpType.IsRecord(t, BindingFlags.Public ||| BindingFlags.NonPublic))
    |> Seq.sortBy (fun t -> t.FullName)
    |> Seq.toList

/// Prüft eine Typmenge. `Error` nennt jeden Row-Typ ohne `[<CLIMutable>]` mit vollem Namen,
/// oder meldet, dass gar kein Row-Typ gefunden wurde (sonst prüfte der Wächter nach einer
/// Umbenennung still ins Leere).
let checkRows (types: Type seq) : Result<int, string> =
    let rows = rowRecords types
    if List.isEmpty rows then
        Error "Keine Row-Typen gefunden (F#-Records mit Namen auf „Row“). Der Wächter prüft ins Leere."
    else
        match rows |> List.filter (fun t -> not (t.IsDefined(typeof<CLIMutableAttribute>, false))) with
        | [] -> Ok rows.Length
        | missing ->
            let names = missing |> List.map (fun t -> t.FullName) |> String.concat ", "
            Error(sprintf "Row-Typen ohne [<CLIMutable>] (Dapper scheitert zur Laufzeit): %s" names)

/// Typargumente, die Dapper ohne Row-Record direkt liest.
let private simpleDapperTypes =
    set [ "string"; "int"; "int32"; "int64"; "decimal"; "bool"; "float"; "double"; "DateTime"; "Guid"; "byte[]" ]

let private dapperRead =
    Regex(@"\b(?:Query|QueryFirst|QueryFirstOrDefault|QuerySingle|QuerySingleOrDefault)(?:Async)?\s*<\s*([^<>]+?)\s*>")

/// Typargumente von Dapper-Lesezugriffen, die weder einfacher Typ noch bekannter Row-Record
/// sind. Eingabe: (Datei, Quelltext) und die Namen der Row-Records. Ergebnis: "Datei:Zeile Typ".
let findUnknownDapperTypes (rowNames: Set<string>) (sources: (string * string) list) : string list =
    [ for file, source in sources do
        let code, _ = lex source
        for m in dapperRead.Matches code do
            let arg = m.Groups[1].Value
            if not (simpleDapperTypes.Contains arg || rowNames.Contains arg) then
                yield $"{file}:{lineAt code m.Index} {arg}" ]

/// F#-Typnamen aus `OptionHandler<...>` auf .NET-Typen.
let private typeByAlias =
    Map.ofList [
        "string", typeof<string>; "int", typeof<int>; "int32", typeof<int>; "int64", typeof<int64>
        "decimal", typeof<decimal>; "bool", typeof<bool>; "float", typeof<float>; "double", typeof<float>
        "DateTime", typeof<DateTime>; "DateTimeOffset", typeof<DateTimeOffset>; "Guid", typeof<Guid> ]

/// Die inneren Typen, für die der Quelltext einen `OptionHandler` registriert
/// (`SqlMapper.AddTypeHandler(OptionHandler<T>())`). Unbekannte Namen gelten als nicht registriert.
let registeredOptionTypes (source: string) : Type list =
    let code, _ = lex source
    Regex.Matches(code, @"AddTypeHandler\s*\(\s*OptionHandler\s*<\s*([^<>]+?)\s*>")
    |> Seq.choose (fun m -> typeByAlias |> Map.tryFind (m.Groups[1].Value))
    |> Seq.distinct
    |> Seq.toList

/// Option-Felder von Row-Records, deren innerer Typ keinen registrierten Handler hat.
/// Ergebnis: "Typ.Feld: innerer Typ".
let findUnhandledOptionFields (registered: Type list) (rows: Type list) : string list =
    [ for t in rows do
        for f in FSharpType.GetRecordFields(t, BindingFlags.Public ||| BindingFlags.NonPublic) do
            let pt = f.PropertyType
            if pt.IsGenericType && pt.GetGenericTypeDefinition() = typedefof<option<_>> then
                let inner = pt.GetGenericArguments()[0]
                if not (List.contains inner registered) then
                    yield $"{t.FullName}.{f.Name}: {inner.Name} option" ]

let private serverAssembly : Assembly = typeof<Persistence.Rules.RuleRow>.Assembly

// Testgegenstände für die Prüfung selbst.
[<CLIMutable>]
type private GuardedProbeRow = { Id: int; Name: string option }

type private UnguardedProbeRow = { Id: int }

[<CLIMutable>]
type private IntOptionProbeRow = { Id: int; Count: int option }

[<Tests>]
let cliMutableGuardTests =
    testList "Struktur-Wächter: Dapper-Row-Typen tragen [<CLIMutable>] (infrastructure-ky2kh)" [
        testCase "jeder Row-Record der Server-Assembly trägt [<CLIMutable>]" <| fun () ->
            match checkRows (serverAssembly.GetTypes()) with
            | Ok _ -> ()
            | Error message -> failtest message

        testCase "jeder Dapper-Lesezugriff im Server liest einen einfachen Typ oder einen Row-Record" <| fun () ->
            let rowNames = rowRecords (serverAssembly.GetTypes()) |> List.map (fun t -> t.Name) |> Set.ofList
            let found = findUnknownDapperTypes rowNames (serverSources "*.fs")
            Expect.isEmpty found "Typargumente ohne Row-Record-Namen (Namen auf „Row“ enden lassen und [<CLIMutable>] setzen)"

        testCase "jedes Option-Feld eines Row-Records hat einen registrierten OptionHandler" <| fun () ->
            let registered = registeredOptionTypes (File.ReadAllText(Path.Combine(serverDir, "Persistence.fs")))
            Expect.isNonEmpty registered "in Persistence.fs ist kein OptionHandler registriert, der Wächter prüft ins Leere"
            let found = findUnhandledOptionFields registered (rowRecords (serverAssembly.GetTypes()))
            Expect.isEmpty found "Option-Felder ohne registrierten OptionHandler (Dapper scheitert beim Lesen)"

        testCase "Gegenbeweis: ohne einen einzigen Row-Typ ist der Wächter rot" <| fun () ->
            Expect.isError (checkRows Seq.empty) "eine leere Typmenge darf nicht als grün durchgehen"

        testCase "Gegenbeweis: ein Row-Record ohne [<CLIMutable>] wird mit Namen gemeldet" <| fun () ->
            match checkRows [ typeof<GuardedProbeRow>; typeof<UnguardedProbeRow> ] with
            | Ok _ -> failtest "der Row-Record ohne [<CLIMutable>] muss gemeldet werden"
            | Error message ->
                Expect.stringContains message "UnguardedProbeRow" "die Meldung nennt den Typ"
                Expect.isFalse (message.Contains "GuardedProbeRow,") "der geschützte Typ wird nicht gemeldet"

        testCase "Gegenbeweis: ein Dapper-Lesezugriff auf einen Typ ohne Row-Namen wird gemeldet" <| fun () ->
            let source =
                "let a = conn.QueryAsync<EntryRow>(\"SELECT * FROM t\")\n"
                + "let b = conn.QueryFirstOrDefault<Entry>(\"SELECT * FROM t\")\n"
                + "let c = conn.ExecuteScalarAsync<int64>(\"SELECT COUNT(*) FROM t\")\n"
                + "// conn.Query<Kommentar>(...) zählt nicht\n"
            let found = findUnknownDapperTypes (set [ "EntryRow" ]) [ "Probe.fs", source ]
            Expect.equal found [ "Probe.fs:2 Entry" ] "nur der Typ ohne Row-Record wird gemeldet"

        testCase "Gegenbeweis: ein Option-Feld ohne registrierten Handler wird gemeldet" <| fun () ->
            let registered = registeredOptionTypes "do\n    SqlMapper.AddTypeHandler(OptionHandler<string>())\n"
            Expect.equal registered [ typeof<string> ] "string ist registriert"
            let found = findUnhandledOptionFields registered [ typeof<GuardedProbeRow>; typeof<IntOptionProbeRow> ]
            Expect.hasLength found 1 "genau das int-Option-Feld wird gemeldet"
            Expect.stringContains found.Head "IntOptionProbeRow.Count" "die Meldung nennt Typ und Feld"
    ]

// ============================================================================
// Kalendertag-Wächter (infrastructure-p747v)
// ============================================================================

// `UtcNow.Date` ist der UTC-Tag: am späten Abend in Berlin schon der falsche, nur anders geschrieben.
let private serverZoneDay =
    Regex(@"\bDateTime\s*\.\s*(?:Today|Now)\b|\bDateTime(?:Offset)?\s*\.\s*UtcNow\s*\.\s*Date\b")

/// Stellen, die den Kalendertag aus der Zeitzone des Prozesses nehmen. Kommentare und
/// String-Literale zählen nicht.
let findServerZoneDays (source: string) : string list =
    let code, _ = lex source
    code.Split('\n')
    |> Array.toList
    |> List.indexed
    |> List.filter (fun (_, l) -> serverZoneDay.IsMatch l)
    |> List.map (fun (k, l) -> $"{k + 1}: {l.Trim()}")

/// Der Testlauf steht selbst auf Europe/Berlin (`test.runsettings`) und der Produktions-Container
/// auf UTC. Eine zurückgekehrte Serverzeit zeigt kein Verhaltenstest, deshalb dieser Wächter.
[<Tests>]
let serverZoneDayGuardTests =
    testList "Struktur-Wächter: Server-Code nimmt den Kalendertag nie aus der Prozess-Zeitzone (infrastructure-p747v)" [
        testCase "kein Server-Code liest DateTime.Today oder DateTime.Now" <| fun () ->
            let sources = serverSources "*.fs"
            Expect.isGreaterThan sources.Length 20 "der Server-Code wird gefunden"
            let found =
                [ for file, source in sources do
                    for v in findServerZoneDays source -> $"{file}:{v}" ]
            Expect.isEmpty found "Der Kalendertag kommt als Parameter (`Ledger.Bot.Domain.berlinDayOf`)"

        testCase "Gegenbeweis: DateTime.Today und DateTime.Now werden gemeldet" <| fun () ->
            let source =
                "module Probe\n"
                + "let a = DateTime.Today\n"
                + "let b = DateTime.Now\n"
                + "let c = DateTime.UtcNow.Date\n"
                + "let d = DateTimeOffset.UtcNow.Date\n"
            let found = findServerZoneDays source
            Expect.equal (found |> List.map (fun v -> v.Split(':')[0])) [ "2"; "3"; "4"; "5" ] "jede Zeile wird gemeldet"

        testCase "Gegenbeweis: Kommentare, String-Literale und UtcNow zählen nicht" <| fun () ->
            let source =
                "module Probe\n"
                + "// DateTime.Today steht nur im Kommentar\n"
                + "let text = \"DateTime.Now im Text\"\n"
                + "let stamp () = DateTime.UtcNow\n"
            Expect.isEmpty (findServerZoneDays source) "nur echter Code zählt"
    ]

// ============================================================================
// 2. Domain-Wächter
// ============================================================================

/// Verbotene Muster in Domain-Dateien: Uhr, Zufall, I/O. Je Muster eine Begründung.
let private impurePatterns : (Regex * string) list =
    [ @"\bDateTime(?:Offset)?\s*\.\s*(?:Now|UtcNow|Today)\b", "Systemuhr (Zeit als Parameter hineingeben)"
      @"\bGuid\s*\.\s*NewGuid\b", "Id-Erzeugung (Id als Parameter hineingeben)"
      @"\bRandom\b|\bRandomNumberGenerator\b", "Zufall"
      @"\bStopwatch\b|\bThread\s*\.\s*Sleep\b|\bTask\s*\.\s*(?:Delay|Run)\b|\bAsync\s*\.\s*Sleep\b", "Zeitmessung oder Warten"
      @"\b(?:File|Directory)\s*\.|\b(?:FileStream|FileInfo|DirectoryInfo|StreamReader|StreamWriter)\b", "Dateisystem"
      @"\bHttpClient\b|\bWebRequest\b|\bRequest\s*\.\s*send|\bFsHttp\b", "Netz"
      @"\bSqlite\w*|\bDapper\b|\bIDbConnection\b|\bPersistence\s*\.", "Datenbank"
      @"\bConsole\s*\.|(?<![\w.])e?printfn?\b", "Konsole"
      @"\bEnvironment\s*\.", "Umgebung"
      @"\bProcess\s*\.\s*Start\b", "Prozess"
      @"\b(?:YnabClient|ComdirectClient|ComdirectAuthSession)\s*\.", "externer Client"
      @"^\s*open\s+(?:System\.IO|System\.Net|Dapper|Microsoft\.Data\.Sqlite)\b", "I/O-Namespace geöffnet" ]
    |> List.map (fun (p, why) -> Regex(p), why)

/// Verstöße einer Domain-Datei: "Zeile: Begründung: Code". Kommentare und
/// String-Literale zählen nicht.
let findImpurities (source: string) : string list =
    let code, _ = lex source
    let lines = code.Split('\n')
    [ for k in 0 .. lines.Length - 1 do
        let l = placeholder.Replace(lines[k], "\"\"")
        for rx, why in impurePatterns do
            if rx.IsMatch l then yield $"{k + 1}: {why}: {l.Trim()}" ]

[<Tests>]
let domainPurityGuardTests =
    testList "Struktur-Wächter: Domain-Dateien ohne Uhr, Zufall und I/O (infrastructure-ky2kh)" [
        testCase "jede src/Server/**/Domain.fs ist frei von Uhr, Zufall und I/O" <| fun () ->
            let sources = serverSources "Domain.fs"
            Expect.isGreaterThanOrEqual sources.Length 2 "Ledger/Domain.fs und Budgeting/Domain.fs werden gefunden"
            let found =
                [ for file, source in sources do
                    for v in findImpurities source -> $"{file}:{v}" ]
            Expect.isEmpty found "Domain-Dateien müssen rein bleiben"

        testCase "Gegenbeweis: Uhr, Id-Erzeugung, Datei und Konsole werden gemeldet" <| fun () ->
            let source =
                "module Probe\n"
                + "let stamp () = DateTime.UtcNow\n"
                + "let newId () = Guid.NewGuid()\n"
                + "let read p = File.ReadAllText p\n"
                + "let log x = printfn \"%A\" x\n"
                + "let token () = Environment.GetEnvironmentVariable \"X\"\n"
            let found = findImpurities source
            Expect.equal (found |> List.map (fun v -> v.Split(':')[0])) [ "2"; "3"; "4"; "5"; "6" ] "jede verbotene Zeile wird gemeldet"

        testCase "Gegenbeweis: Kommentare, String-Literale und reine Formatierung zählen nicht" <| fun () ->
            let source =
                "module Probe\n"
                + "// DateTime.UtcNow steht nur im Kommentar\n"
                + "(* Guid.NewGuid() im Blockkommentar\n   File.ReadAllText *)\n"
                + "let text = \"Console.WriteLine im Text\"\n"
                + "let quote = '\"'\n"
                + "let label x = sprintf \"%d\" x\n"
                + "let at (now: DateTime) = now.AddDays 1.0\n"
            Expect.isEmpty (findImpurities source) "nur echter Code zählt"
    ]

// ============================================================================
// 3. SQL-Wächter
// ============================================================================

/// Eingesetzte SQL-Bausteine, die nachweislich nur aus Konstanten bestehen. Jeder Eintrag
/// nennt Datei (relativ zu src/Server), Ausdruck und Begründung. Ein Eintrag, der nicht mehr
/// gebraucht wird, macht den Wächter rot, damit die Liste nicht still veraltet.
let sqlAllowList : (string * string * string) list =
    [ // Ledger/Persistence.fs: `buildFilterClause` setzt die WHERE-Klausel nur aus Literalen
      // zusammen, jeder Filterwert geht als @-Parameter in `ps`.
      "Ledger/Persistence.fs", "whereSql", "WHERE-Klausel aus buildFilterClause, Werte als @-Parameter"
      // Konstante Klauseln auf Modulebene (`let private ... = "..."`).
      "Ledger/Persistence.fs", "needsReviewSqlClause", "konstante Klausel für \"zu prüfen\""
      "Ledger/Persistence.fs", "askLenaSqlClause", "konstante Klausel für \"Rückfrage Lena\""
      // In `buildFilterClause` lokal aus Literalen gebaute Teilklauseln.
      "Ledger/Persistence.fs", "categoryClause", "Literal mit @categoryIds/@splitCategoryIds"
      "Ledger/Persistence.fs", "withoutCategoryClause", "Verkettung reiner Literale"
      // `orderBySql`/`directionSql`: Sortiertext aus der geschlossenen Union LedgerSort.
      "Ledger/Persistence.fs", "orderBySql sort", "ORDER BY aus der geschlossenen Union LedgerSort"
      "Ledger/Persistence.fs", "dir", "ASC/DESC aus directionSql über die geschlossene Union"
      // `loadPayeeCategorySuggestions`: entweder "" oder ein Literal mit @exclude.
      "Ledger/Persistence.fs", "excludeSql", "\"\" oder Literal mit @exclude-Parameter" ]

let private sqlKeyword =
    Regex(@"\b(?:SELECT|INSERT|UPDATE|DELETE|WHERE|FROM|JOIN|VALUES|ORDER\s+BY|GROUP\s+BY|LIMIT|OFFSET|AND|OR|NOT|ASC|DESC|LIKE|IN|IS|NULL|SET|CASE|WHEN|CREATE|ALTER|DROP|PRAGMA)\b")

/// Die eingesetzten Ausdrücke eines interpolierten Literals und sein Text ohne sie.
let private holesOf (lit: SourceLiteral) : string list * string =
    if lit.Dollars = 0 then
        [], lit.Text
    else
        let t = lit.Text
        let opener = String('{', lit.Dollars)
        let closer = String('}', lit.Dollars)
        let holes = ResizeArray<string>()
        let rest = StringBuilder()
        let mutable k = 0
        while k < t.Length do
            if lit.Dollars = 1 && (t.Substring(k).StartsWith "{{" || t.Substring(k).StartsWith "}}") then
                rest.Append(t[k]) |> ignore
                k <- k + 2
            elif t.Substring(k).StartsWith opener && not (t.Substring(k + opener.Length).StartsWith "{") then
                let start = k + opener.Length
                let mutable e = start
                let mutable depth = 0
                while e < t.Length && not (depth = 0 && t.Substring(e).StartsWith closer) do
                    if t[e] = '{' then depth <- depth + 1
                    elif t[e] = '}' then depth <- depth - 1
                    e <- e + 1
                holes.Add(t.Substring(start, e - start).Trim())
                rest.Append(' ') |> ignore
                k <- e + closer.Length
            else
                rest.Append(t[k]) |> ignore
                k <- k + 1
        List.ofSeq holes, rest.ToString()

let private isSqlLiteral (lit: SourceLiteral) = sqlKeyword.IsMatch(snd (holesOf lit))

let private operand = "(?:\u0001\\d+\u0002|[A-Za-z_][\\w.']*(?:\\([^()\\n]*\\))?)"
let private concatChain = Regex($@"{operand}(?:\s*[+^]\s*{operand})+")
let private formatCall = Regex("\\b(?:sprintf|ksprintf|kprintf|failwithf|String\\.Format)\\s*\\(?\\s*\u0001(\\d+)\u0002")
let private appendStart = Regex(@"\b(\w+)\s*\.\s*Append\s*\(")
let private appendArg = Regex(@"\.\s*Append\s*\(\s*([^()\n]*?)\s*\)")

/// Eingesetzte Werte in SQL-Texten einer Datei: (Zeile, Ausdruck, Art). Ein Ausdruck in
/// `allowed` zählt nicht. Formatfunktionen mit SQL-Text sind immer ein Fund.
let findSqlInjections (allowed: Set<string>) (source: string) : (int * string * string) list =
    let code, literalList = lex source
    let literals = Array.ofList literalList
    let lit (m: Match) = literals[int (m.Groups[1].Value)]
    let isLiteralToken (s: string) = placeholder.IsMatch s && placeholder.Match(s).Length = s.Length
    let interpolated =
        [ for l in literalList do
            if l.Dollars > 0 && isSqlLiteral l then
                for h in fst (holesOf l) do
                    if not (allowed.Contains h) then yield l.Line, h, "Interpolation" ]
    let formatted =
        [ for m in formatCall.Matches code do
            if isSqlLiteral (lit m) then yield (lit m).Line, (lit m).Text.Trim(), "Formatfunktion" ]
    let concatenated =
        [ for m in concatChain.Matches code do
            let parts = Regex.Split(m.Value, @"\s*[+^]\s*")
            let hasSql = parts |> Array.exists (fun p -> isLiteralToken p && isSqlLiteral (literals[int (placeholder.Match(p).Groups[1].Value)]))
            if hasSql then
                for p in parts do
                    if not (isLiteralToken p) && not (allowed.Contains p) then
                        yield lineAt code m.Index, p, "Verkettung" ]
    let sqlBuilders =
        [ for m in Regex.Matches(code, "\\b(\\w+)\\s*\\.\\s*Append\\s*\\(\\s*\u0001(\\d+)\u0002") do
            if isSqlLiteral (literals[int (m.Groups[2].Value)]) then yield m.Groups[1].Value ]
        |> Set.ofList
    let appended =
        let lines = code.Split('\n')
        [ for k in 0 .. lines.Length - 1 do
            let start = appendStart.Match(lines[k])
            if start.Success && sqlBuilders.Contains(start.Groups[1].Value) then
                for a in appendArg.Matches(lines[k]) do
                    let arg = a.Groups[1].Value
                    if not (isLiteralToken arg) && not (allowed.Contains arg) then
                        yield k + 1, arg, "StringBuilder.Append" ]
    interpolated @ formatted @ concatenated @ appended |> List.distinct |> List.sortBy (fun (l, _, _) -> l)

let private sqlSources () =
    serverSources "Persistence.fs" @ serverSources "QueryHandlers.fs"

[<Tests>]
let sqlInterpolationGuardTests =
    testList "Struktur-Wächter: SQL ohne eingesetzte Werte (infrastructure-ky2kh)" [
        testCase "Persistence.fs und QueryHandlers.fs setzen keine Werte in SQL-Texte ein" <| fun () ->
            let sources = sqlSources ()
            Expect.isGreaterThanOrEqual sources.Length 4 "Persistence.fs, Ledger/, Budgeting/ und Ledger/QueryHandlers.fs werden gefunden"
            let found =
                [ for file, source in sources do
                    let allowed = sqlAllowList |> List.filter (fun (f, _, _) -> f = file) |> List.map (fun (_, e, _) -> e) |> Set.ofList
                    for line, expr, kind in findSqlInjections allowed source -> $"{file}:{line} {kind}: {expr}" ]
            Expect.isEmpty found "Werte gehören als @-Parameter in die Abfrage; konstante Bausteine in sqlAllowList mit Begründung"

        testCase "jeder Eintrag der Erlaubnisliste wird noch gebraucht" <| fun () ->
            let sources = sqlSources () |> Map.ofList
            let stale =
                [ for file, expr, _ in sqlAllowList do
                    match sources |> Map.tryFind file with
                    | None -> yield $"{file}: Datei fehlt"
                    | Some source ->
                        if List.isEmpty (findSqlInjections Set.empty source |> List.filter (fun (_, e, _) -> e = expr)) then
                            yield $"{file}: {expr}" ]
            Expect.isEmpty stale "veraltete Einträge aus sqlAllowList entfernen"

        testCase "Gegenbeweis: interpolierter Wert, sprintf, Verkettung und Append werden gemeldet" <| fun () ->
            let source =
                "let a id = conn.Query<R>($\"SELECT * FROM t WHERE id = '{id}'\")\n"
                + "let b name = sprintf \"SELECT * FROM t WHERE name = '%s'\" name\n"
                + "let c v = \"DELETE FROM t WHERE x = \" + v\n"
                + "let d () =\n"
                + "    let sb = StringBuilder(\"SELECT * FROM t\")\n"
                + "    sb.Append(\" WHERE x = \").Append(userInput) |> ignore\n"
                + "let e v = $\"\"\"UPDATE t SET y = {v}\n    WHERE id = @id\"\"\"\n"
            let found = findSqlInjections Set.empty source
            Expect.equal
                found
                [ 1, "id", "Interpolation"
                  2, "SELECT * FROM t WHERE name = '%s'", "Formatfunktion"
                  3, "v", "Verkettung"
                  6, "userInput", "StringBuilder.Append"
                  7, "v", "Interpolation" ]
                "jede Einsetzart wird mit Zeile und Ausdruck gemeldet"

        testCase "Gegenbeweis: Parameter, Text ohne SQL und erlaubte Bausteine bleiben grün" <| fun () ->
            let source =
                "let a () = conn.Query<R>(\"SELECT * FROM t WHERE id = @id\", ps)\n"
                + "let b e = failwith $\"Unbekannter Status: {e}\"\n"
                + "let c (basis: string) = basis + \", Lena gefragt\"\n"
                + "let d () = conn.Query<R>($\"SELECT * FROM t WHERE {whereSql}\", ps)\n"
                + "// let e v = $\"SELECT * FROM t WHERE x = {v}\" im Kommentar\n"
            Expect.isEmpty (findSqlInjections (set [ "whereSql" ]) source) "nur eingesetzte Werte zählen"
            let withoutAllow = findSqlInjections Set.empty source
            Expect.equal withoutAllow [ 4, "whereSql", "Interpolation" ] "ohne Erlaubnis wird auch der Baustein gemeldet"
    ]

// ============================================================================
// Hintergrunddienste: nur Program.fs registriert sie (ADR 0071 Punkt 8)
// ============================================================================

/// Ein Dienst, der in `Api.webApp` oder darunter registriert wird, liefe auch im TestServer und
/// könnte dort die Bank erreichen. Ergebnis: Pfade außerhalb von `Program.fs`, die einen
/// Hintergrunddienst registrieren. Kommentare und String-Literale zählen nicht.
let findHostedServiceRegistrations (sources: (string * string) list) : string list =
    sources
    |> List.filter (fun (path, source) -> path <> "Program.fs" && (fst (lex source)).Contains "AddHostedService")
    |> List.map fst

/// Der Dienst der Bankverbindung bekommt Wiederaufnahme und Takt der laufenden Verbindung. Beide haben
/// denselben Typ, der Compiler sähe eine Vertauschung oder einen leeren Ersatz nicht.
let isBankWorkerWired (program: string) : bool =
    Regex.IsMatch(
        fst (lex program),
        @"AddHostedService[\s\S]*?BankConnectionWorker\(\s*Server\.ComdirectAuthSession\.resumeConnection\s*,\s*Server\.ComdirectAuthSession\.runTick\s*,\s*Server\.BankConnection\.tickPeriod\b")

/// Der Dienst der Übernahme im Hintergrund (budgeting-dkjc5) bekommt den Live-Port, die Berliner Zone, die echte Uhr
/// und die echte Wartefunktion. Ein leerer Port, eine feste Uhr oder eine Wartefunktion ohne Warten kompilierten.
let isTakeoverWorkerWired (program: string) : bool =
    Regex.IsMatch(
        fst (lex program),
        @"AddHostedService[\s\S]*?TakeoverWorker\(\s*Server\.Api\.liveTakeoverPort\s*,\s*berlinZone\s*,\s*\(fun\s*\(\)\s*->\s*System\.DateTime\.UtcNow\)\s*,\s*Server\.Budgeting\.TakeoverWorker\.sleep\s*\)")

/// `livePorts` trägt die Netzports, Tests dürfen ihn nicht anfassen (`NetworkCallGuardTests.fs`). Das
/// Verhalten von Speicher und Schlüsseln prüfen die Tests direkt, hier bleibt nur der Anschluss.
let isLivePortsStorageWired (session: string) : bool =
    Regex.IsMatch(
        fst (lex session),
        @"let livePorts\b[\s\S]*?\bRefresh = refreshTokensLive\b[\s\S]*?\bFetchTransactions = getTransactions\b[\s\S]*?\bFetchFirstTransactionsPage = getFirstTransactionsPage\b[\s\S]*?\bStore = liveConnectionStore\b[\s\S]*?\bLoadKeys = loadLiveApiKeys\b")

/// `name` ist auf Modulebene gebunden und besteht aus genau dem Ausdruck `body`, ohne Zusatz davor oder dahinter.
let private isBoundExactlyTo (name: string) (body: string) (source: string) : bool =
    Regex.IsMatch(
        fst (lex source),
        $@"^let {name}\b[^=\n]*=\s*{body}[ \t]*(?:\n(?=[ \t]*\n|\S)|\n?\z)",
        RegexOptions.Multiline)

/// Der Gate-Lauf hat keinen `BUDGETBUDDY_ENCRYPTION_KEY`, und Tests dürfen die Prozessumgebung nicht
/// umstellen. Ein Verhaltenstest sieht deshalb nur eine Richtung, ein Speicher ohne echte Umgebung
/// bliebe grün. `connectionStoreFor` ist mit eingespeister Umgebung in beiden Richtungen getestet, hier
/// bleibt nur der Anschluss an die echte Umgebung.
let isLiveStoreOnRealEnvironment (store: string) : bool =
    isBoundExactlyTo
        "liveConnectionStore"
        @"connectionStoreFor\s+System\.Environment\.GetEnvironmentVariable\s+connectionSettingKey"
        store

/// Die Wiederaufnahme des laufenden Servers kann kein Test ausführen, sie läuft über `livePorts`.
let isResumeOnLivePorts (session: string) : bool =
    isBoundExactlyTo "resumeConnection" @"resumeConnectionWith\s+livePorts\s+now" session

/// Takt, Trennen, TAN-Bestätigung und Verwerfen laufen im Server über `livePorts`, kein Test darf sie
/// ausführen (`NetworkCallGuardTests.fs`). Getestet sind die `*With`-Funktionen, hier bleibt der Anschluss.
let isSessionBoundToLivePorts (session: string) : bool =
    [ "runTick", @"tick\s+livePorts\s+now"
      "disconnect", @"disconnectWith\s+livePorts"
      "connectionStatus", @"connectionStatusWith\s+livePorts"
      "confirmTan", @"confirmChallengeWith\s+livePorts"
      "discardPendingChallenge", @"discardPendingChallengeWith\s+livePorts" ]
    |> List.forall (fun (name, body) -> isBoundExactlyTo name body session)

/// Die Endpoints sind Record-Felder in `Api.fs` und reichen Funktionswerte gleichen Typs durch. Ein
/// leerer Ersatz kompilierte, und der laufende Server ließe etwa bei geänderten Zugangsdaten den
/// gespeicherten Wert liegen. Jeder Eintrag ist die ganze Verdrahtung eines Felds.
let private apiWirings =
    [ @"\bgetSettings\s*=\s*fun\s*\(\)\s*->\s*readSettingsForClient\s+Persistence\.Settings\.getSetting\b"
      @"\bsaveComdirectCredentials\s*=\s*saveComdirectCredentialsWith\s+Persistence\.Settings\.getSetting\s+Persistence\.Settings\.setSetting\s+ComdirectAuthSession\.disconnect\b"
      @"\btestComdirectConnection\s*=\s*fun\s*\(\)\s*->\s*testComdirectConnectionWith\s+Persistence\.Settings\.getSetting\s+ComdirectAuthSession\.livePorts\b"
      @"\bBank\s*=\s*BankPort\.comdirectBankPort\s+ComdirectAuthSession\.livePorts\b"
      @"\bstartSync\s*=\s*fun\s*\(\)\s*->\s*BankImport\.startSyncWith\s+liveImportPorts\b"
      @"\bconfirmTan\s*=\s*BankImport\.confirmTanWith\s+liveImportPorts\b"
      @"\bcancelSync\s*=\s*cancelSyncWith\s+SyncSessionManager\.validateSession\s+ComdirectAuthSession\.discardPendingChallenge\s+SyncSessionManager\.clearSession\b"
      @"\bgetBankConnectionStatus\s*=\s*fun\s*\(\)\s*->\s*async\.Return\s+\(ComdirectAuthSession\.connectionStatus\s+\(\)\)"
      @"\bdisconnectBank\s*=\s*fun\s*\(\)\s*->\s*BankImport\.disconnectBankWith\s+liveImportPorts\s+ComdirectAuthSession\.disconnect\b"
      // Der Berliner Kalendertag (infrastructure-p747v): ein Prozess-Tag oder ein fester Tag kompilierte
      // an jeder dieser Stellen, und der Testlauf steht selbst auf Europe/Berlin.
      @"\bliveBerlinDay\s*\(\)\s*:\s*DateTime\s*=\s*Ledger\.Bot\.Domain\.berlinDayOf\s+liveBerlinZone\.Value\s+DateTime\.UtcNow\s*(?:\n\s*\n|\z)"
      @"\bValidation\.validateManualTransaction\s+\(liveBerlinDay\s*\(\)\)\s+request\b"
      @"\bgetAccountTransactions\s+token\s+\(YnabBudgetId\s+budgetId\)\s+\(YnabAccountId\s+accountGuid\)\s+\(liveBerlinDay\s*\(\)\)\s+90\b"
      @"\bgetAccountTransactions\s+token\s+\(YnabBudgetId\s+budgetId\)\s+\(YnabAccountId\s+accountIdGuid\)\s+\(liveBerlinDay\s*\(\)\)\s+days\b"
      @"\bToday\s*=\s*liveBerlinDay\b"
      @"\bprepareForImport\s+liveBerlinZone\.Value\s+DateTime\.UtcNow\s+ynabAccountId\s+transferPayees\s+false\s+toImport\b"
      @"\bprepareForImport\s+liveBerlinZone\.Value\s+DateTime\.UtcNow\s+ynabAccountId\s+transferPayees\s+true\s+transactionsToForce\b"
      // Budget-Seite (budgeting-c5w2n): Die `*With`-Wege sind mit Fake-Ports getestet, die Ports des
      // Servers erreichen nur die Integrationstests hinter dem Schalter.
      @"\bFetchMonth\s*=\s*fun\s+monthKey\s*->\s*YnabClient\.getMonth\s+token\s+budgetId\s+monthKey\b"
      @"\bSetAssigned\s*=\s*fun\s+monthKey\s+categoryId\s+milliunits\s*->\s*YnabClient\.setAssigned\s+token\s+budgetId\s+monthKey\s+categoryId\s+milliunits\b"
      // Goals für die Target-Übernahme (budgeting-eh8xe): mit Fake-Port getestet, live nur im laufenden Server.
      @"\bFetchCategoryGoals\s*=\s*fun\s*\(\)\s*->\s*YnabClient\.getCategoryGoals\s+token\s+budgetId\b"
      // Goal schreiben (budgeting-e2z7v): Planung und Abläufe mit Fake-Port getestet, live nur im laufenden Server.
      @"\bWriteGoal\s*=\s*fun\s+categoryId\s+patch\s*->\s*YnabClient\.patchCategoryGoal\s+token\s+budgetId\s+categoryId\s+patch\b"
      @"\bliveBudgetApi\s*\(setting:[^)]*\)\s*\(berlinZone:[^)]*\)\s*:\s*BudgetApi\s*=\s*budgetApiWith\s+setting\s+livePort\s+liveFramePort\s+\(fun\s*\(\)\s*->\s*DateTime\.UtcNow\)\s+berlinZone\s*(?:\n\s*\n|\z)"
      @"\bbudgetApi\s*\(berlinZone:[^)]*\)\s*:\s*BudgetApi\s*=\s*liveBudgetApi\s+Persistence\.Settings\.getSetting\s+berlinZone\s*(?:\n\s*\n|\z)"
      @"\bRemoting\.fromValue\s+\(budgetApi\s+berlinZone\)"
      // Übernahme im Hintergrund (budgeting-dkjc5): Schritt und Schleife sind mit Fake-Ports getestet, der Port des
      // Servers erreicht YNAB nur im laufenden Dienst.
      @"\bSettings\s*=\s*fun\s*\(\)\s*->\s*ynabAccessWith\s+Persistence\.Settings\.getSetting\b"
      @"\bFetchMonths\s*=\s*fun\s+token\s+budgetId\s*->\s*YnabClient\.getMonths\s+token\s+budgetId\s*\n\s*FetchMonth\s*=\s*fun\s+token\s+budgetId\s+monthKey\s*->\s*YnabClient\.getMonth\s+token\s+budgetId\s+monthKey\b"
      @"\bLoadTakenOver\s*=\s*Budgeting\.Persistence\.loadTakenOverMonths\b"
      @"\bEnsureTakenOver\s*=\s*Server\.Budgeting\.CommandHandlers\.ensureTakenOver\b"
      // Quick Add, Erneut senden und Import-Ziel (ledger-bk3q8): die `*With`-Wege sind mit Fake-Ports getestet,
      // die Ports des Servers erreichen YNAB nur im laufenden Dienst.
      @"\bOk\s+validRequest\s*->\s*return!\s+addManualTransactionWith\s+liveQuickAddPorts\s+DateTime\.UtcNow\s+validRequest\s*\}"
      @"\bliveQuickAddPorts\s*:\s*QuickAddPorts\s*=\s*\{\s*Setting\s*=\s*Persistence\.Settings\.getSetting\s+LoadYnabCategories\s*=\s*YnabClient\.getCategories\s+Push\s*=\s*YnabClient\.createTransactionWith\s+NewEntryId\s*=\s*Ledger\.Events\.newEntryId\s+TriggerMirror\s*=\s*Ledger\.CommandHandlers\.triggerMirrorInBackground\s*\}"
      @"\bentryPush\s*\(\)\s*:\s*Async<Ledger\.CommandHandlers\.EntryPush>\s*=\s*entryPushWith\s+Persistence\.Settings\.getSetting\s+YnabClient\.createTransactionFor\s*(?:\n\s*\n|\z)"
      // Buchung erfassen und Rückgängig (ledger-at63w): `addLedgerBookingWith`, `discardCapturedBookingWith` und die
      // Abfragen sind mit Fake-Ports getestet, die Ports des Servers erreichen YNAB nur im laufenden Dienst.
      @"\baddLedgerBooking\s*=\s*fun\s+request\s*->\s*addLedgerBookingWith\s+liveLedgerBookingPorts\s+\(liveBerlinDay\s*\(\)\)\s+DateTime\.UtcNow\s+request\b"
      @"\bdiscardCapturedBooking\s*=\s*fun\s+entryId\s*->\s*discardCapturedBookingWith\s+liveLedgerBookingPorts\s+DateTime\.UtcNow\s+entryId\b"
      @"\bliveLedgerBookingPorts\s*:\s*LedgerBookingPorts\s*=\s*\{\s*Setting\s*=\s*Persistence\.Settings\.getSetting\s+LoadYnabCategories\s*=\s*YnabClient\.getCategories\s+BudgetAccounts\s*=\s*YnabClient\.fetchTransferAccounts\s+Push\s*=\s*YnabClient\.createTransactionFor\s+Delete\s*=\s*YnabClient\.deleteTransaction\s+NewEntryId\s*=\s*Ledger\.Events\.newEntryId\s+TriggerMirror\s*=\s*Ledger\.CommandHandlers\.triggerMirrorInBackground\s*\}"
      // Buchung löschen und Rückgängig (ledger-z6sya): `deleteEntryWith` und `restoreDeletedEntryWith` sind mit Fake-Ports
      // getestet, die Ports des Servers erreichen YNAB nur im laufenden Dienst.
      @"\bdeleteEntry\s*=\s*fun\s+entryId\s*->\s*deleteEntryWith\s+liveEntryDeletionPorts\s+DateTime\.UtcNow\s+entryId\b"
      @"\brestoreDeletedEntry\s*=\s*fun\s+entryId\s*->\s*restoreDeletedEntryWith\s+liveEntryDeletionPorts\s+DateTime\.UtcNow\s+entryId\b"
      @"\bliveEntryDeletionPorts\s*:\s*EntryDeletionPorts\s*=\s*\{\s*Setting\s*=\s*Persistence\.Settings\.getSetting\s+Delete\s*=\s*YnabClient\.deleteTransaction\s+Create\s*=\s*YnabClient\.createReplacement\s+TriggerMirror\s*=\s*Ledger\.CommandHandlers\.triggerMirrorInBackground\s*\}"
      @"\bgetLedgerBookingDefaultsWith\s+Persistence\.Settings\.getSetting\s+liveBerlinZone\.Value\s+DateTime\.UtcNow\b"
      @"\bgetBookingCategoryForPayeeWith\s+Ledger\.Persistence\.loadLastPayeeCategory\s+Persistence\.Rules\.getAllRules\s+payee\b"
      @"\bimportTargetWith\s+Persistence\.Settings\.getSetting\s+toImport\.Length\s+with\b"
      @"\bimportTargetWith\s+Persistence\.Settings\.getSetting\s+transactionsToForce\.Length\s+with\b"
      // Kategorien verwalten (budgeting-pscgb): `categoryApiWith` und der Befehl sind mit Fake-Ports getestet, der Port des
      // Servers erreicht YNAB nur im laufenden Dienst.
      @"\bFetchGroups\s*=\s*fun\s*\(\)\s*->\s*YnabClient\.getCategoryGroups\s+token\s+budgetId\b"
      @"\bCreateCategory\s*=\s*fun\s+groupId\s+name\s*->\s*YnabClient\.createCategory\s+token\s+budgetId\s+groupId\s+name\b"
      @"\bUpdateCategory\s*=\s*fun\s+categoryId\s+patch\s*->\s*YnabClient\.patchCategory\s+token\s+budgetId\s+categoryId\s+patch\b"
      @"\bCreateGroup\s*=\s*fun\s+name\s*->\s*YnabClient\.createCategoryGroup\s+token\s+budgetId\s+name\b"
      @"\bUpdateGroup\s*=\s*fun\s+groupId\s+name\s*->\s*YnabClient\.updateCategoryGroup\s+token\s+budgetId\s+groupId\s+name\b"
      @"\bRefreshMirror\s*=\s*fun\s*\(\)\s*->\s*Server\.Budgeting\.Categories\.refreshMirrorWith\s+\(fun\s*\(\)\s*->\s*YnabClient\.getCategories\s+token\s+budgetId\)\s+Ledger\.Persistence\.upsertCategories\b"
      @"\bcategoryApi\s*\(berlinZone:[^)]*\)\s*:\s*CategoryApi\s*=\s*categoryApiWith\s+Persistence\.Settings\.getSetting\s+liveCategoryAdminPort\s+Persistence\.getConnection\s+\(fun\s*\(\)\s*->\s*DateTime\.UtcNow\)\s+berlinZone\s*(?:\n\s*\n|\z)"
      @"\bRemoting\.fromValue\s+\(categoryApi\s+berlinZone\)"
      // Deaktivieren und Reihenfolge (budgeting-sn2sn): die `*With`-Wege sind mit Fake-Ports und In-Memory-SQLite
      // getestet, die Live-Verdrahtung erreicht YNAB und die echte Datenbank nur im laufenden Dienst.
      @"\bFetchMonth\s*=\s*fun\s+month\s*->\s*YnabClient\.getMonth\s+token\s+budgetId\s+month\b"
      @"\bpickerOptionsWith\s+\(fun\s*\(\)\s*->\s*YnabClient\.getCategoriesPlaced\s+token\s+budgetId\)\s+\(fun\s*\(\)\s*->\s*YnabClient\.getReadyToAssign\s+token\s+budgetId\)\s+Budgeting\.Persistence\.loadCategoryLayout\b"
      @"\bFetchCategoryOrder\s*=\s*fun\s*\(\)\s*->\s*Server\.Budgeting\.Categories\.bbCategoryOrderWith\s+\(fun\s*\(\)\s*->\s*YnabClient\.getCategoriesPlaced\s+token\s+budgetId\)\s+Budgeting\.Persistence\.loadCategoryLayout\b" ]

/// Bobs Push in `BotApi.liveDeps` (ledger-bk3q8): bekommt Konto, `import_id` (ledger-p3vq8) und Request der
/// Barbuchung und reicht alle drei unverändert an YNAB. Ein Ersatz kompilierte, der Testlauf kann den echten Push nicht ausführen.
let isBotPushWired (bot: string) : bool =
    let code = fst (lex bot)
    Regex.IsMatch(
        code,
        @"\bPush\s*=\s*fun\s*\(ynabAccountId,\s*importId,\s*request\)\s*->\s*async\s*\{\s*match!\s+ynabContext\s*\(\)\s+with\s*\|\s*Error\s+reason\s*->\s*return\s+Error\s+\(YnabError\.Unauthorized\s+reason\)\s*\|\s*Ok\s+\(token,\s*budgetId\)\s*->\s*return!\s+Server\.YnabClient\.createTransactionWith\s+\(Some\s+importId\)\s+token\s+budgetId\s+ynabAccountId\s+request\s*\}"
    )

/// Die Muster aus `apiWirings`, die im Quelltext fehlen.
let findMissingApiWirings (api: string) : string list =
    let code = fst (lex api)
    apiWirings |> List.filter (fun pattern -> not (Regex.IsMatch(code, pattern)))

[<Tests>]
let hostedServiceGuardTests =
    testList "Struktur-Wächter: Hintergrunddienste nur in Program.fs (banking-import-tshq9)" [
        testCase "kein Server-Modul außer Program.fs registriert einen Hintergrunddienst" <| fun () ->
            Expect.isEmpty (findHostedServiceRegistrations (serverSources "*.fs")) "Registrierung nur in Program.fs"

        testCase "Program.fs registriert den Dienst mit Wiederaufnahme und Takt" <| fun () ->
            let program = serverSources "Program.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isBankWorkerWired program) "sonst nimmt niemand die Verbindung wieder auf und erneuert die Tokens"

        testCase "Program.fs registriert die Übernahme im Hintergrund mit Live-Port, echter Uhr und echtem Warten" <| fun () ->
            let program = serverSources "Program.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isTakeoverWorkerWired program) "sonst übernimmt niemand die älteren Monate"

        testCase "Gegenbeweis: eine falsch verdrahtete Übernahme im Hintergrund wird erkannt" <| fun () ->
            let registered (port: string) (clock: string) (delay: string) =
                $"builder.Services.AddHostedService(fun _ ->\n    new Server.Budgeting.TakeoverWorker.TakeoverWorker(\n        {port},\n        berlinZone,\n        {clock},\n        {delay}))"
            let port = "Server.Api.liveTakeoverPort"
            let clock = "(fun () -> System.DateTime.UtcNow)"
            let sleep = "Server.Budgeting.TakeoverWorker.sleep"
            Expect.isTrue (isTakeoverWorkerWired (registered port clock sleep)) "richtig verdrahtet"
            Expect.isFalse (isTakeoverWorkerWired (registered "fakePort" clock sleep)) "anderer Port"
            Expect.isFalse (isTakeoverWorkerWired (registered port "(fun () -> System.DateTime.MinValue)" sleep)) "feste Uhr"
            Expect.isFalse (isTakeoverWorkerWired (registered port clock "(fun _ -> async.Return ())")) "Warten ohne Warten"
            Expect.isFalse (isTakeoverWorkerWired $"// {registered port clock sleep}") "nur im Kommentar"

        testCase "die Ports des Servers nutzen den echten Speicher und die echten Schlüssel" <| fun () ->
            let session = serverSources "ComdirectAuthSession.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isLivePortsStorageWired session) "sonst wird nichts gespeichert oder jeder Wert beim Start gelöscht"

        testCase "der Speicher des Servers hängt an der echten Umgebung" <| fun () ->
            let store = serverSources "BankConnectionStore.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isLiveStoreOnRealEnvironment store) "sonst speichert der Server nie oder auch ohne Schlüssel"

        testCase "die Wiederaufnahme des Servers läuft über die Ports des Servers" <| fun () ->
            let session = serverSources "ComdirectAuthSession.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isResumeOnLivePorts session) "sonst bleibt der Dienst angeschlossen, nimmt aber nichts wieder auf"

        testCase "Takt, Trennen, TAN und Verwerfen des Servers laufen über die Ports des Servers" <| fun () ->
            let session = serverSources "ComdirectAuthSession.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isSessionBoundToLivePorts session) "sonst bleibt der Name angeschlossen und tut nichts"

        testCase "die Endpoints hängen an der echten Verbindung und an den echten Einstellungen" <| fun () ->
            let api = serverSources "Api.fs" |> List.exactlyOne |> snd
            Expect.isEmpty (findMissingApiWirings api) "sonst bleibt etwa der gespeicherte Wert bei geänderten Zugangsdaten liegen"

        testCase "Gegenbeweis: Takt, Trennen, TAN und Verwerfen ohne die Ports des Servers werden gemeldet" <| fun () ->
            let source (runTick: string) (disconnect: string) (confirm: string) (discard: string) =
                $"let confirmTan () : Async<ComdirectResult<unit>> =\n    {confirm}\n\n"
                + $"let discardPendingChallenge () : Async<unit> =\n    {discard}\n\n"
                + $"let disconnect () : Async<unit> =\n    {disconnect}\n\n"
                + "let connectionStatus () : BankConnectionStatus =\n    connectionStatusWith livePorts\n\n"
                + $"let runTick (now: DateTimeOffset) : Async<unit> =\n    {runTick}\n"
            let tick = "tick livePorts now"
            let disconnect = "disconnectWith livePorts"
            let confirm = "confirmChallengeWith livePorts"
            let discard = "discardPendingChallengeWith livePorts"
            let empty = "async.Return ()"
            Expect.isTrue (isSessionBoundToLivePorts (source tick disconnect confirm discard)) "richtig"
            Expect.isFalse (isSessionBoundToLivePorts (source empty disconnect confirm discard)) "leerer Takt"
            Expect.isFalse (isSessionBoundToLivePorts (source "tick emptyPorts now" disconnect confirm discard)) "Takt mit anderen Ports"
            Expect.isFalse (isSessionBoundToLivePorts (source tick empty confirm discard)) "leeres Trennen"
            Expect.isFalse (isSessionBoundToLivePorts (source tick "disconnectWith emptyPorts" confirm discard)) "Trennen mit anderen Ports"
            Expect.isFalse (isSessionBoundToLivePorts ((source tick disconnect confirm discard).Replace("connectionStatusWith livePorts", "connectionStatusWith emptyPorts"))) "Stand mit anderen Ports"
            Expect.isFalse (isSessionBoundToLivePorts (source tick disconnect "async.Return (Ok ())" discard)) "leere TAN"
            Expect.isFalse (isSessionBoundToLivePorts (source tick disconnect confirm empty)) "leeres Verwerfen"
            Expect.isFalse (isSessionBoundToLivePorts (source $"{tick}\n    |> Async.Ignore" disconnect confirm discard)) "Zusatz in der Folgezeile"
            Expect.isFalse (isSessionBoundToLivePorts $"// {source tick disconnect confirm discard}") "nur im Kommentar"

        testCase "Gegenbeweis: ein Ersatz in den Endpoints wird gemeldet" <| fun () ->
            let real =
                [ "getSettings = fun () -> readSettingsForClient Persistence.Settings.getSetting\n"
                  "saveComdirectCredentials =\n    saveComdirectCredentialsWith\n        Persistence.Settings.getSetting\n        Persistence.Settings.setSetting\n        ComdirectAuthSession.disconnect\n"
                  "testComdirectConnection = fun () ->\n    testComdirectConnectionWith\n        Persistence.Settings.getSetting\n        ComdirectAuthSession.livePorts\n"
                  "Bank = BankPort.comdirectBankPort ComdirectAuthSession.livePorts clock\n"
                  "startSync = fun () -> BankImport.startSyncWith liveImportPorts\n"
                  "confirmTan = BankImport.confirmTanWith liveImportPorts\n"
                  "cancelSync =\n    cancelSyncWith\n        SyncSessionManager.validateSession\n        ComdirectAuthSession.discardPendingChallenge\n        SyncSessionManager.clearSession\n"
                  "getBankConnectionStatus = fun () -> async.Return (ComdirectAuthSession.connectionStatus ())\n"
                  "disconnectBank = fun () -> BankImport.disconnectBankWith liveImportPorts ComdirectAuthSession.disconnect\n"
                  "let private liveBerlinDay () : DateTime =\n    Ledger.Bot.Domain.berlinDayOf liveBerlinZone.Value DateTime.UtcNow\n\n"
                  "Validation.validateManualTransaction (liveBerlinDay ()) request\n"
                  "YnabClient.getAccountTransactions token (YnabBudgetId budgetId) (YnabAccountId accountGuid) (liveBerlinDay ()) 90\n"
                  "YnabClient.getAccountTransactions token (YnabBudgetId budgetId) (YnabAccountId accountIdGuid) (liveBerlinDay ()) days\n"
                  "Today = liveBerlinDay\n"
                  "prepareForImport liveBerlinZone.Value DateTime.UtcNow ynabAccountId transferPayees false toImport\n"
                  "prepareForImport liveBerlinZone.Value DateTime.UtcNow ynabAccountId transferPayees true transactionsToForce\n"
                  "{ FetchMonth = fun monthKey -> YnabClient.getMonth token budgetId monthKey\n  SetAssigned = fun monthKey categoryId milliunits -> YnabClient.setAssigned token budgetId monthKey categoryId milliunits\n  WriteGoal = fun categoryId patch -> YnabClient.patchCategoryGoal token budgetId categoryId patch\n  FetchCategoryGoals = fun () -> YnabClient.getCategoryGoals token budgetId }\n"
                  "let liveBudgetApi (setting: string -> Async<string option>) (berlinZone: TimeZoneInfo) : BudgetApi =\n    budgetApiWith setting livePort liveFramePort (fun () -> DateTime.UtcNow) berlinZone\n\n"
                  "let budgetApi (berlinZone: TimeZoneInfo) : BudgetApi =\n    liveBudgetApi Persistence.Settings.getSetting berlinZone\n\n"
                  "|> Remoting.fromValue (budgetApi berlinZone)\n"
                  "| Ok validRequest -> return! addManualTransactionWith liveQuickAddPorts DateTime.UtcNow validRequest\n    }\n"
                  "let liveQuickAddPorts : QuickAddPorts = {\n    Setting = Persistence.Settings.getSetting\n    LoadYnabCategories = YnabClient.getCategories\n    Push = YnabClient.createTransactionWith\n    NewEntryId = Ledger.Events.newEntryId\n    TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground\n}\n"
                  "let private entryPush () : Async<Ledger.CommandHandlers.EntryPush> =\n    entryPushWith Persistence.Settings.getSetting YnabClient.createTransactionFor\n\n"
                  "addLedgerBooking = fun request -> addLedgerBookingWith liveLedgerBookingPorts (liveBerlinDay ()) DateTime.UtcNow request\n"
                  "discardCapturedBooking = fun entryId -> discardCapturedBookingWith liveLedgerBookingPorts DateTime.UtcNow entryId\n"
                  "let liveLedgerBookingPorts : LedgerBookingPorts = {\n    Setting = Persistence.Settings.getSetting\n    LoadYnabCategories = YnabClient.getCategories\n    BudgetAccounts = YnabClient.fetchTransferAccounts\n    Push = YnabClient.createTransactionFor\n    Delete = YnabClient.deleteTransaction\n    NewEntryId = Ledger.Events.newEntryId\n    TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground\n}\n"
                  "deleteEntry = fun entryId -> deleteEntryWith liveEntryDeletionPorts DateTime.UtcNow entryId\n"
                  "restoreDeletedEntry = fun entryId -> restoreDeletedEntryWith liveEntryDeletionPorts DateTime.UtcNow entryId\n"
                  "let liveEntryDeletionPorts : EntryDeletionPorts = {\n    Setting = Persistence.Settings.getSetting\n    Delete = YnabClient.deleteTransaction\n    Create = YnabClient.createReplacement\n    TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground\n}\n"
                  "Ledger.QueryHandlers.getLedgerBookingDefaultsWith Persistence.Settings.getSetting liveBerlinZone.Value DateTime.UtcNow\n"
                  "Ledger.QueryHandlers.getBookingCategoryForPayeeWith\n            Ledger.Persistence.loadLastPayeeCategory\n            Persistence.Rules.getAllRules\n            payee\n"
                  "match! importTargetWith Persistence.Settings.getSetting toImport.Length with\n"
                  "match! importTargetWith Persistence.Settings.getSetting transactionsToForce.Length with\n"
                  "{ FetchGroups = fun () -> YnabClient.getCategoryGroups token budgetId\n  CreateCategory = fun groupId name -> YnabClient.createCategory token budgetId groupId name\n  UpdateCategory = fun categoryId patch -> YnabClient.patchCategory token budgetId categoryId patch\n  CreateGroup = fun name -> YnabClient.createCategoryGroup token budgetId name\n  UpdateGroup = fun groupId name -> YnabClient.updateCategoryGroup token budgetId groupId name\n  RefreshMirror = fun () -> Server.Budgeting.Categories.refreshMirrorWith (fun () -> YnabClient.getCategories token budgetId) Ledger.Persistence.upsertCategories\n  FetchMonth = fun month -> YnabClient.getMonth token budgetId month }\n"
                  "let categoryApi (berlinZone: TimeZoneInfo) : CategoryApi =\n    categoryApiWith Persistence.Settings.getSetting liveCategoryAdminPort Persistence.getConnection (fun () -> DateTime.UtcNow) berlinZone\n\n"
                  "|> Remoting.fromValue (categoryApi berlinZone)\n"
                  "return!\n    Server.Budgeting.Categories.pickerOptionsWith\n        (fun () -> YnabClient.getCategoriesPlaced token budgetId)\n        (fun () -> YnabClient.getReadyToAssign token budgetId)\n        Budgeting.Persistence.loadCategoryLayout\n"
                  "FetchCategoryOrder =\n    fun () ->\n        Server.Budgeting.Categories.bbCategoryOrderWith\n            (fun () -> YnabClient.getCategoriesPlaced token budgetId)\n            Budgeting.Persistence.loadCategoryLayout\n"
                  "{ Settings = fun () -> ynabAccessWith Persistence.Settings.getSetting\n  FetchMonths = fun token budgetId -> YnabClient.getMonths token budgetId\n  FetchMonth = fun token budgetId monthKey -> YnabClient.getMonth token budgetId monthKey\n  LoadTakenOver = Budgeting.Persistence.loadTakenOverMonths\n  EnsureTakenOver = Server.Budgeting.CommandHandlers.ensureTakenOver }\n" ]
                |> String.concat "\n"
            let replaced (original: string) (replacement: string) = real.Replace(original, replacement)
            Expect.isEmpty (findMissingApiWirings real) "richtig"
            for original, replacement, label in
                [ "readSettingsForClient Persistence.Settings.getSetting", "readSettingsForClient (fun _ -> async.Return None)", "Einstellungen aus leerem Speicher"
                  "ComdirectAuthSession.disconnect", "(fun () -> async.Return ())", "leeres Trennen"
                  "saveComdirectCredentialsWith\n        Persistence.Settings.getSetting", "saveComdirectCredentialsWith\n        (fun _ -> async.Return None)", "keine alten Zugangsdaten"
                  "Persistence.Settings.setSetting", "(fun _ _ _ -> async.Return ())", "nichts geschrieben"
                  "testComdirectConnectionWith\n        Persistence.Settings.getSetting\n        ComdirectAuthSession.livePorts", "testComdirectConnectionWith\n        Persistence.Settings.getSetting\n        emptyPorts", "Test mit anderen Ports"
                  "comdirectBankPort ComdirectAuthSession.livePorts", "comdirectBankPort emptyPorts", "Bank-Port mit anderen Ports"
                  "BankImport.startSyncWith liveImportPorts", "async.Return (Error fehler)", "leerer Start"
                  "BankImport.confirmTanWith liveImportPorts", "(fun _ -> async.Return (Ok ()))", "leere Bestätigung"
                  "ComdirectAuthSession.discardPendingChallenge", "(fun () -> async.Return ())", "Abbruch verwirft nichts"
                  "async.Return (ComdirectAuthSession.connectionStatus ())", "async.Return BankConnected", "erfundener Stand"
                  "disconnectBankWith liveImportPorts ComdirectAuthSession.disconnect", "disconnectBankWith liveImportPorts (fun () -> async.Return ())", "Trennen löscht nichts"
                  "berlinDayOf liveBerlinZone.Value DateTime.UtcNow", "berlinDayOf TimeZoneInfo.Utc DateTime.UtcNow", "Tag in UTC"
                  "Ledger.Bot.Domain.berlinDayOf liveBerlinZone.Value DateTime.UtcNow", "DateTime.UtcNow.Date", "Tag ohne Zone"
                  "validateManualTransaction (liveBerlinDay ())", "validateManualTransaction DateTime.Today", "Quick Add auf dem Prozess-Tag"
                  "(YnabAccountId accountGuid) (liveBerlinDay ()) 90", "(YnabAccountId accountGuid) DateTime.Today 90", "Vorlagen auf dem Prozess-Tag"
                  "(YnabAccountId accountIdGuid) (liveBerlinDay ()) days", "(YnabAccountId accountIdGuid) DateTime.Today days", "YNAB-Abgleich auf dem Prozess-Tag"
                  "Today = liveBerlinDay", "Today = fun () -> DateTime.Today", "Bank-Import auf dem Prozess-Tag"
                  "prepareForImport liveBerlinZone.Value DateTime.UtcNow ynabAccountId transferPayees false", "prepareForImport TimeZoneInfo.Utc DateTime.UtcNow ynabAccountId transferPayees false", "Import in UTC"
                  "prepareForImport liveBerlinZone.Value DateTime.UtcNow ynabAccountId transferPayees true", "prepareForImport TimeZoneInfo.Utc DateTime.UtcNow ynabAccountId transferPayees true", "erzwungener Import in UTC"
                  "YnabClient.getMonth token budgetId monthKey", "async.Return (Error fehler)", "Monat ohne YNAB"
                  "YnabClient.setAssigned token budgetId monthKey categoryId milliunits", "async.Return (Ok ())", "Zuweisen ohne YNAB"
                  "YnabClient.getCategoryGoals token budgetId", "async.Return (Ok [])", "Übernahme ohne Goals"
                  "YnabClient.patchCategoryGoal token budgetId categoryId patch", "async.Return (Ok ())", "Goal schreiben ohne YNAB"
                  "YnabClient.getCategoryGroups token budgetId", "async.Return (Ok [])", "Kategorien-Seite ohne YNAB"
                  "YnabClient.createCategory token budgetId groupId name", "async.Return (Ok groupId)", "Anlegen ohne YNAB"
                  "YnabClient.patchCategory token budgetId categoryId patch", "async.Return (Ok categoryId)", "Bearbeiten ohne YNAB"
                  "YnabClient.createCategoryGroup token budgetId name", "async.Return (Ok Guid.Empty)", "Gruppe anlegen ohne YNAB"
                  "YnabClient.updateCategoryGroup token budgetId groupId name", "async.Return (Ok groupId)", "Gruppe umbenennen ohne YNAB"
                  "Ledger.Persistence.upsertCategories", "(fun _ -> async.Return ())", "Spiegel schreibt nichts"
                  "categoryApiWith Persistence.Settings.getSetting", "categoryApiWith (fun _ -> async.Return None)", "Kategorien ohne Settings"
                  "Remoting.fromValue (categoryApi berlinZone)", "Remoting.fromValue fakeCategoryApi", "anderer Kategorien-Server"
                  "liveCategoryAdminPort Persistence.getConnection", "liveCategoryAdminPort fakeConnect", "Kategorien auf anderer Datenbank"
                  "Persistence.getConnection (fun () -> DateTime.UtcNow)", "Persistence.getConnection (fun () -> DateTime.MinValue)", "Kategorien mit fester Uhr"
                  "Persistence.getConnection (fun () -> DateTime.UtcNow) berlinZone", "Persistence.getConnection (fun () -> DateTime.UtcNow) TimeZoneInfo.Utc", "Kategorien in UTC"
                  "fun month -> YnabClient.getMonth token budgetId month", "fun month -> async.Return (Error fehler)", "Deaktivieren ohne YNAB-Monat"
                  "(fun () -> YnabClient.getCategoriesPlaced token budgetId)\n        (fun () -> YnabClient.getReadyToAssign", "(fun () -> async.Return (Ok ([], [])))\n        (fun () -> YnabClient.getReadyToAssign", "Picker ohne YNAB"
                  "(fun () -> YnabClient.getReadyToAssign token budgetId)", "(fun () -> async.Return None)", "Picker ohne Zu verteilen"
                  "        Budgeting.Persistence.loadCategoryLayout\n\nFetchCategoryOrder", "        (fun () -> async.Return (Ok emptyCategoryLayout))\n\nFetchCategoryOrder", "Picker ohne BBs Zustand"
                  "bbCategoryOrderWith\n            (fun () -> YnabClient.getCategoriesPlaced token budgetId)", "bbCategoryOrderWith\n            (fun () -> async.Return (Ok ([], [])))", "Rahmen ohne YNAB"
                  "            Budgeting.Persistence.loadCategoryLayout\n", "            (fun () -> async.Return (Ok emptyCategoryLayout))\n", "Rahmen ohne BBs Zustand"
                  "budgetApiWith setting livePort", "budgetApiWith (fun _ -> async.Return None) livePort", "Budget-Seite ohne Settings"
                  "setting livePort liveFramePort", "setting emptyPort liveFramePort", "anderer YNAB-Port"
                  "setting livePort liveFramePort", "setting livePort emptyFramePort", "anderer Rahmen-Port"
                  "(fun () -> DateTime.UtcNow) berlinZone", "(fun () -> DateTime.MinValue) berlinZone", "feste Uhr"
                  "(fun () -> DateTime.UtcNow) berlinZone", "(fun () -> DateTime.UtcNow) TimeZoneInfo.Utc", "Budget-Seite in UTC"
                  "liveBudgetApi Persistence.Settings.getSetting", "liveBudgetApi (fun _ -> async.Return None)", "leere Settings"
                  "Remoting.fromValue (budgetApi berlinZone)", "Remoting.fromValue fakeBudgetApi", "anderer Server"
                  "ynabAccessWith Persistence.Settings.getSetting", "async.Return None", "Übernahme ohne Settings"
                  "YnabClient.getMonths token budgetId", "async.Return (Ok [])", "Übernahme ohne Fenster"
                  "fun token budgetId monthKey -> YnabClient.getMonth", "fun token budgetId monthKey -> fakeMonth", "Übernahme-Monat ohne YNAB"
                  "Budgeting.Persistence.loadTakenOverMonths", "(fun () -> async.Return (Ok Set.empty))", "nichts gilt als übernommen"
                  "Server.Budgeting.CommandHandlers.ensureTakenOver", "(fun _ _ _ -> async.Return (Ok AlreadyTakenOver))", "Übernahme ohne Übernahme"
                  "addManualTransactionWith liveQuickAddPorts", "addManualTransactionWith fakeQuickAddPorts", "Quick Add mit anderen Ports"
                  "DateTime.UtcNow validRequest", "DateTime.MinValue validRequest", "Quick Add mit fester Uhr"
                  "    Setting = Persistence.Settings.getSetting\n    LoadYnabCategories", "    Setting = (fun _ -> async.Return None)\n    LoadYnabCategories", "Quick Add ohne Settings"
                  "LoadYnabCategories = YnabClient.getCategories", "LoadYnabCategories = (fun _ _ -> async.Return (Ok []))", "Quick Add ohne YNAB-Kategorien"
                  "Push = YnabClient.createTransactionWith", "Push = (fun _ _ _ _ _ -> async.Return (Ok \"x\"))", "Quick Add ohne Push"
                  "NewEntryId = Ledger.Events.newEntryId", "NewEntryId = (fun () -> festeId)", "Quick Add mit fester Id"
                  "TriggerMirror = Ledger.CommandHandlers.triggerMirrorInBackground", "TriggerMirror = ignore", "Quick Add ohne Spiegel"
                  "entryPushWith Persistence.Settings.getSetting YnabClient.createTransactionFor", "entryPushWith Persistence.Settings.getSetting fakeCreate", "Erneut senden ohne YNAB"
                  "entryPushWith Persistence.Settings.getSetting", "entryPushWith (fun _ -> async.Return None)", "Erneut senden ohne Settings"
                  "importTargetWith Persistence.Settings.getSetting toImport.Length", "importTargetWith (fun _ -> async.Return None) toImport.Length", "Import ohne Settings"
                  "importTargetWith Persistence.Settings.getSetting transactionsToForce.Length", "importTargetWith Persistence.Settings.getSetting 0", "erzwungener Import mit falscher Zahl"
                  "addLedgerBookingWith liveLedgerBookingPorts", "addLedgerBookingWith fakeBookingPorts", "Erfassen mit anderen Ports"
                  "(liveBerlinDay ()) DateTime.UtcNow request", "DateTime.UtcNow DateTime.UtcNow request", "Erfassen mit UTC-Tag"
                  "discardCapturedBookingWith liveLedgerBookingPorts", "discardCapturedBookingWith fakeBookingPorts", "Rückgängig mit anderen Ports"
                  "BudgetAccounts = YnabClient.fetchTransferAccounts", "BudgetAccounts = (fun _ _ -> async.Return (Ok []))", "Erfassen ohne Konten"
                  "Push = YnabClient.createTransactionFor\n    Delete", "Push = fakeCreate\n    Delete", "Erfassen ohne Push"
                  "Delete = YnabClient.deleteTransaction", "Delete = (fun _ _ _ -> async.Return (Ok ()))", "Rückgängig ohne Löschen"
                  "deleteEntryWith liveEntryDeletionPorts", "deleteEntryWith fakeDeletionPorts", "Löschen mit anderen Ports"
                  "restoreDeletedEntryWith liveEntryDeletionPorts", "restoreDeletedEntryWith fakeDeletionPorts", "Wiederherstellen mit anderen Ports"
                  "Create = YnabClient.createReplacement", "Create = (fun _ _ _ -> async.Return (Ok \"x\"))", "Wiederherstellen ohne Neuanlage"
                  "getLedgerBookingDefaultsWith Persistence.Settings.getSetting liveBerlinZone.Value", "getLedgerBookingDefaultsWith Persistence.Settings.getSetting TimeZoneInfo.Utc", "Vorbelegung in UTC"
                  "Persistence.Rules.getAllRules\n            payee", "(fun () -> async.Return [])\n            payee", "Kategorie zum Payee ohne Regeln" ] do
                Expect.isNonEmpty (findMissingApiWirings (replaced original replacement)) label
            Expect.isNonEmpty (findMissingApiWirings $"// {real}") "nur im Kommentar"

        testCase "Bobs Push des Servers reicht Konto, import_id und Request der Barbuchung an YNAB" <| fun () ->
            let bot = serverSources "BotApi.fs" |> List.exactlyOne |> snd
            Expect.isTrue (isBotPushWired bot) "sonst pusht Bob nichts oder etwas anderes"

        testCase "Gegenbeweis: ein falsch verdrahteter Push für Bob wird gemeldet" <| fun () ->
            let real =
                "        Push =\n            fun (ynabAccountId, importId, request) ->\n                async {\n                    match! ynabContext () with\n                    | Error reason -> return Error (YnabError.Unauthorized reason)\n                    | Ok (token, budgetId) -> return! Server.YnabClient.createTransactionWith (Some importId) token budgetId ynabAccountId request\n                }\n"
            Expect.isTrue (isBotPushWired real) "richtig"
            for original, replacement, label in
                [ "token budgetId ynabAccountId request", "token budgetId festesKonto request", "anderes Konto"
                  "token budgetId ynabAccountId request", "token budgetId ynabAccountId { request with Amount = 0m }", "veränderter Request"
                  "(Some importId)", "None", "ohne import_id"
                  "(Some importId)", "(Some \"BB:fest\")", "andere import_id"
                  "return! Server.YnabClient.createTransactionWith (Some importId) token budgetId ynabAccountId request", "return Ok \"x\"", "kein Push"
                  "match! ynabContext ()", "match! fakeContext ()", "andere Einstellungen" ] do
                Expect.isFalse (isBotPushWired (real.Replace(original, replacement))) label
            Expect.isFalse (isBotPushWired $"// {real}") "nur im Kommentar"

        testCase "Gegenbeweis: ein Speicher ohne echte Umgebung wird gemeldet" <| fun () ->
            let bound (body: string) = $"let liveConnectionStore: ConnectionStore =\n    {body}\n\nlet weiter = 1\n"
            let real = "connectionStoreFor System.Environment.GetEnvironmentVariable connectionSettingKey"
            Expect.isTrue (isLiveStoreOnRealEnvironment (bound real)) "richtig"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound "connectionStoreFor (fun _ -> null) connectionSettingKey")) "leere Umgebung"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound "connectionStoreFor (fun _ -> \"x\") connectionSettingKey")) "erfundene Umgebung"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound "settingsConnectionStore (fun () -> false) connectionSettingKey")) "speichert nie"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound "connectionStoreFor System.Environment.GetEnvironmentVariable \"anderer\"")) "anderer Schlüssel"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound $"{real} |> nurLesend")) "Zusatz in der Zeile"
            Expect.isFalse (isLiveStoreOnRealEnvironment (bound $"{real}\n    |> nurLesend")) "Zusatz in der Folgezeile"
            Expect.isFalse (isLiveStoreOnRealEnvironment $"// {bound real}") "nur im Kommentar"

        testCase "Gegenbeweis: eine Wiederaufnahme ohne die Ports des Servers wird gemeldet" <| fun () ->
            let bound (body: string) = $"let resumeConnection (now: DateTimeOffset) : Async<unit> =\n    {body}\n\nlet weiter = 1\n"
            let real = "resumeConnectionWith livePorts now"
            Expect.isTrue (isResumeOnLivePorts (bound real)) "richtig"
            Expect.isFalse (isResumeOnLivePorts (bound "async.Return ()")) "tut nichts"
            Expect.isFalse (isResumeOnLivePorts (bound "resumeConnectionWith emptyPorts now")) "andere Ports"
            Expect.isFalse (isResumeOnLivePorts (bound "tick livePorts now")) "anderer Ablauf"
            Expect.isFalse (isResumeOnLivePorts (bound $"{real}\n    |> Async.Ignore")) "Zusatz in der Folgezeile"
            Expect.isFalse (isResumeOnLivePorts $"// {bound real}") "nur im Kommentar"

        testCase "Gegenbeweis: ein Ersatz für Speicher oder Schlüssel wird gemeldet" <| fun () ->
            let portsFull (refresh: string) (fetch: string) (firstPage: string) (store: string) (keys: string) =
                $"let livePorts: ConnectionPorts = {{\n    Refresh = {refresh}\n    FetchTransactions = {fetch}\n    FetchFirstTransactionsPage = {firstPage}\n    Store = {store}\n    LoadKeys = {keys}\n}}"
            let portsWith (refresh: string) = portsFull refresh "getTransactions" "getFirstTransactionsPage"
            let ports = portsWith "refreshTokensLive"
            Expect.isTrue (isLivePortsStorageWired (ports "liveConnectionStore" "loadLiveApiKeys")) "richtig"
            Expect.isFalse (isLivePortsStorageWired (portsWith "(fun _ _ tokens -> async.Return (Ok tokens))" "liveConnectionStore" "loadLiveApiKeys")) "kein Refresh"
            Expect.isFalse (isLivePortsStorageWired (ports "emptyStore" "loadLiveApiKeys")) "anderer Speicher"
            Expect.isFalse (isLivePortsStorageWired (ports "liveConnectionStore" "(fun () -> async.Return None)")) "keine Schlüssel"
            let emptyFetch = "(fun _ _ _ _ _ -> async.Return (Ok []))"
            let emptyPage = "(fun _ _ _ -> async.Return (Ok []))"
            Expect.isFalse (isLivePortsStorageWired (portsFull "refreshTokensLive" emptyFetch "getFirstTransactionsPage" "liveConnectionStore" "loadLiveApiKeys")) "Abruf ohne Bank"
            Expect.isFalse (isLivePortsStorageWired (portsFull "refreshTokensLive" "getTransactions" emptyPage "liveConnectionStore" "loadLiveApiKeys")) "Verbindungstest ohne Bank"
            Expect.isFalse (isLivePortsStorageWired (portsFull "refreshTokensLive" "getTransactionsWith client" "getFirstTransactionsPage" "liveConnectionStore" "loadLiveApiKeys")) "anderer Abruf"

        testCase "Gegenbeweis: leerer Ersatz, Vertauschung und Kommentar zählen nicht als Verdrahtung" <| fun () ->
            let registered (resume: string) (tick: string) =
                $"builder.Services.AddHostedService(fun _ ->\n    new Server.BankConnectionWorker.BankConnectionWorker(\n        {resume},\n        {tick},\n        Server.BankConnection.tickPeriod))"
            let resume = "Server.ComdirectAuthSession.resumeConnection"
            let runTick = "Server.ComdirectAuthSession.runTick"
            Expect.isTrue (isBankWorkerWired (registered resume runTick)) "richtig verdrahtet"
            Expect.isFalse (isBankWorkerWired (registered "(fun _ -> async.Return ())" runTick)) "leere Wiederaufnahme"
            Expect.isFalse (isBankWorkerWired (registered resume "(fun _ -> async.Return ())")) "leerer Takt"
            Expect.isFalse (isBankWorkerWired (registered runTick resume)) "vertauscht"
            Expect.isFalse (isBankWorkerWired $"// {registered resume runTick}") "nur im Kommentar"

        testCase "Gegenbeweis: eine Registrierung außerhalb von Program.fs wird gemeldet, ein Kommentar nicht" <| fun () ->
            let sources =
                [ "Program.fs", "builder.Services.AddHostedService(worker)"
                  "Api.fs", "services.AddHostedService(worker)"
                  "Doku.fs", "// AddHostedService bleibt in Program.fs" ]
            Expect.equal (findHostedServiceRegistrations sources) [ "Api.fs" ] "nur der echte Aufruf"
    ]
