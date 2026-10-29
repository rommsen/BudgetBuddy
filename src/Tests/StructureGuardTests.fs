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
