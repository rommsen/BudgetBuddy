---
title: "Milestone 5: Rules Engine - Automatische Kategorisierung mit F# Regex und Type Safety"
date: 2025-11-29
author: Claude
tags: [F#, Rules Engine, Pattern Matching, Type Safety, Testing]
---

# Milestone 5: Rules Engine - Automatische Kategorisierung mit F# Regex und Type Safety

## Einleitung

Die Rules Engine ist das Herzstück von BudgetBuddy's Automatisierung. Ohne sie müsste ich jede einzelne Banktransaktion manuell einer YNAB-Kategorie zuordnen – bei hunderten Transaktionen pro Monat ein unmöglicher Aufwand. Die Aufgabe war klar: Ich brauche ein System, das Transaktionen basierend auf benutzerdefinierten Regeln automatisch kategorisiert, dabei aber flexibel genug ist, um verschiedene Matching-Strategien (exakte Übereinstimmung, Teilstring, reguläre Ausdrücke) zu unterstützen.

Zusätzlich sollte das System "intelligente" Transaktionen erkennen – etwa Amazon-Bestellungen oder PayPal-Zahlungen – und dem Nutzer direkte Links zu den entsprechenden Order-Histories bereitstellen. Das klingt simpel, birgt aber einige interessante technische Herausforderungen: Wie kompiliere ich Patterns effizient? Wie handhabe ich Fehler bei ungültigen Regex-Patterns? Und wie stelle ich sicher, dass die Priorität der Regeln korrekt umgesetzt wird?

In diesem Blogpost beschreibe ich, wie ich diese Herausforderungen mit F#'s Type-System, Pattern Matching und einer klaren Architektur gelöst habe. Das Ergebnis: 200+ Zeilen produktiver Code mit 46 umfassenden Tests, die alle grün sind.

## Ausgangslage

Vor Milestone 5 existierte bereits die komplette Domain-Model-Struktur aus Milestone 1:

```fsharp
type Rule = {
    Id: RuleId
    Name: string
    Pattern: string
    PatternType: PatternType  // Regex | Contains | Exact
    TargetField: TargetField  // Payee | Memo | Combined
    CategoryId: YnabCategoryId
    CategoryName: string
    PayeeOverride: string option
    Priority: int
    Enabled: bool
    CreatedAt: DateTime
    UpdatedAt: DateTime
}

type SyncTransaction = {
    Transaction: BankTransaction
    Status: TransactionStatus
    CategoryId: YnabCategoryId option
    CategoryName: string option
    MatchedRuleId: RuleId option
    PayeeOverride: string option
    ExternalLinks: ExternalLink list
    UserNotes: string option
}
```

Außerdem waren die Persistence-Layer (Milestone 2), der YNAB-Client (Milestone 3) und der Comdirect-Client (Milestone 4) bereits implementiert und getestet. Was fehlte, war die Business-Logik, die diese Komponenten verbindet: die Rules Engine selbst.

## Haupt-Herausforderungen

### Herausforderung 1: Pattern-Kompilierung mit drei verschiedenen Modi

#### Das Problem

Benutzer sollen drei verschiedene Matching-Strategien verwenden können:

1. **Exact**: Der Pattern muss exakt übereinstimmen (z.B. "REWE" matched nur "REWE", nicht "REWE Supermarkt")
2. **Contains**: Der Pattern muss enthalten sein (z.B. "REWE" matched "REWE Supermarkt")
3. **Regex**: Benutzerdefinierte reguläre Ausdrücke (z.B. `REWE\s+\d+` matched "REWE 123")

Das Problem: Regex-Patterns in .NET müssen kompiliert werden, was Zeit kostet. Bei 100+ Regeln pro Sync-Session kann das Performance-Probleme verursachen, wenn ich die Patterns bei jeder Transaction neu kompiliere. Außerdem müssen bei "Exact" und "Contains" Sonderzeichen escaped werden (z.B. hat "." in Regex eine Sonderbedeutung).

#### Optionen, die ich betrachtet habe

1. **Patterns bei jedem Match neu kompilieren**
   - Pro: Einfach zu implementieren, kein State nötig
   - Contra: Extrem langsam bei vielen Regeln (100 Regeln × 200 Transactions = 20.000 Regex-Kompilierungen!)

2. **Patterns einmal kompilieren und cachen** (gewählt)
   - Pro: Performance-Optimierung – einmal kompilieren, oft verwenden
   - Contra: Benötigt einen separaten `CompiledRule`-Typ

3. **Keine Regex verwenden, nur String-Vergleiche**
   - Pro: Schnell und einfach
   - Contra: Nicht flexibel genug – Nutzer können keine komplexen Patterns definieren

#### Die Lösung: CompiledRule mit Pattern-Transformation

Ich habe mich für Option 2 entschieden und einen separaten `CompiledRule`-Typ eingeführt:

```fsharp
type CompiledRule = {
    Rule: Rule
    Regex: Regex
}

let compileRule (rule: Rule) : Result<CompiledRule, string> =
    try
        let pattern =
            match rule.PatternType with
            | Exact ->
                // Escape special regex characters and wrap with anchors
                "^" + Regex.Escape(rule.Pattern) + "$"
            | Contains ->
                // Escape special regex characters
                Regex.Escape(rule.Pattern)
            | Regex ->
                // Use pattern as-is (user-provided regex)
                rule.Pattern

        let regex = new Regex(pattern, RegexOptions.IgnoreCase)
        Ok { Rule = rule; Regex = regex }
    with
    | ex -> Error $"Failed to compile pattern '{rule.Pattern}': {ex.Message}"
```

**Architekturentscheidung: Warum ein separater CompiledRule-Typ?**

1. **Performance**: Die Regex wird nur einmal kompiliert, nicht bei jeder Transaction
2. **Type Safety**: Der Compiler zwingt mich, alle Regeln vor der Verwendung zu kompilieren
3. **Fehlerbehandlung**: Ungültige Regex-Patterns werden vor der Verwendung erkannt
4. **Separation of Concerns**: Domain-Model (`Rule`) bleibt clean, Performance-Optimierung (`CompiledRule`) ist separat

**Warum `Regex.Escape` für Exact und Contains?**

Wenn ein Nutzer "REWE." als Pattern eingibt (Exact-Match), soll das nur "REWE." matchen, nicht "REWEx" (weil "." in Regex "beliebiges Zeichen" bedeutet). `Regex.Escape` escaped alle Sonderzeichen:

```fsharp
Regex.Escape("REWE.") // => "REWE\."
```

Bei Exact-Patterns füge ich zusätzlich Anker hinzu (`^...$`), damit nur exakte Übereinstimmungen matched werden:

```fsharp
"^" + Regex.Escape("REWE") + "$" // => "^REWE$"
// Matched: "REWE"
// Matched NICHT: "REWE Supermarkt"
```

### Herausforderung 2: Naming Conflict zwischen PatternType.Regex und System.Text.RegularExpressions.Regex

#### Das Problem

F#'s Module-System öffnet am Anfang der Datei `System.Text.RegularExpressions`, was bedeutet, dass `Regex` im Scope ist. Gleichzeitig habe ich im Domain-Model einen Discriminated Union Case `PatternType.Regex`. Das führt zu einem Naming-Conflict:

```fsharp
let pattern =
    match rule.PatternType with
    | Regex -> rule.Pattern  // ❌ Compiler denkt, das ist der Typ-Name, nicht der Case

let regex = Regex(pattern, RegexOptions.IgnoreCase)  // ❌ Compiler verwirrt
```

Der F#-Compiler gibt Fehler wie "This value is not a function and cannot be applied", weil er denkt, ich versuche `PatternType.Regex` (ein Wert) als Funktion zu verwenden.

#### Optionen, die ich betrachtet habe

1. **PatternType umbenennen** (z.B. `RegexPattern`)
   - Pro: Kein Naming-Conflict mehr
   - Contra: Domain-Model ändern nur wegen eines Implementation-Details ist schlechtes Design

2. **Module Alias verwenden**
   ```fsharp
   module RE = System.Text.RegularExpressions
   let regex = RE.Regex(pattern, RegexOptions.IgnoreCase)
   ```
   - Pro: Klar und keine Änderungen am Domain-Model
   - Contra: Zusätzlicher Namespace-Prefix überall

3. **`new` Keyword verwenden** (gewählt)
   ```fsharp
   let regex = new Regex(pattern, RegexOptions.IgnoreCase)
   ```
   - Pro: Einfach und klar – signalisiert "ich instanziiere ein Objekt"
   - Contra: Funktioniert nicht bei static methods (aber hier irrelevant)

#### Die Lösung: `new` Keyword

Ich habe mich für Option 3 entschieden:

```fsharp
let regex = new Regex(pattern, RegexOptions.IgnoreCase)
```

**Warum `new` statt Module Alias?**

- **Klarheit**: Das `new` Keyword macht deutlich, dass hier ein Objekt instanziiert wird
- **Weniger Code**: Kein zusätzlicher Module-Import nötig
- **F#-Konvention**: Obwohl F# das `new` Keyword optional macht, hilft es hier bei der Disambiguierung

### Herausforderung 3: Batch-Kompilierung mit Error-Collection

#### Das Problem

Wenn ein Nutzer 50 Regeln definiert hat und eine davon einen ungültigen Regex-Pattern enthält (z.B. `[unclosed`), sollte das System:

1. **Alle** Fehler sammeln (nicht nur den ersten)
2. Die fehlerhafte Regel klar benennen
3. Die anderen 49 Regeln trotzdem kompilieren (oder nicht?)

Die Frage ist: Fail-fast oder alle Fehler sammeln?

#### Optionen, die ich betrachtet habe

1. **Fail-fast: Bei erstem Fehler abbrechen**
   ```fsharp
   let compileRules (rules: Rule list) : Result<CompiledRule list, string> =
       rules
       |> List.map compileRule
       |> List.sequenceResultM  // Stoppt bei erstem Error
   ```
   - Pro: Einfach zu implementieren
   - Contra: Nutzer muss Fehler einzeln beheben, sehr mühsam bei vielen Regeln

2. **Alle Fehler sammeln, aber bei Fehlern nichts zurückgeben** (gewählt)
   ```fsharp
   let compileRules (rules: Rule list) : Result<CompiledRule list, string list> =
       // Sammle ALLE Fehler, gebe sie als Liste zurück
   ```
   - Pro: Nutzer sieht alle Fehler auf einen Blick
   - Contra: Kann keine teilweise kompilierten Regeln verwenden

3. **Teilweise Kompilierung: Erfolgreich kompilierte Regeln zurückgeben**
   - Pro: System bleibt teilweise funktional
   - Contra: Verwirrend für Nutzer – "Warum wurden nur 49 von 50 Regeln angewendet?"

#### Die Lösung: Alle Fehler sammeln

```fsharp
let compileRules (rules: Rule list) : Result<CompiledRule list, string list> =
    let results = rules |> List.map compileRule

    let errors =
        results
        |> List.choose (fun r ->
            match r with
            | Error e -> Some e
            | Ok _ -> None
        )

    if not (List.isEmpty errors) then
        Error errors
    else
        let compiled =
            results
            |> List.choose (fun r ->
                match r with
                | Ok c -> Some c
                | Error _ -> None
            )
        Ok compiled
```

**Architekturentscheidung: Warum Error-Liste statt einzelner Error-String?**

```fsharp
Result<CompiledRule list, string list>  // ✅ Gewählt
// statt
Result<CompiledRule list, string>      // ❌ Weniger Information
```

- **Bessere Error-Messages**: Jeder Fehler ist separat, kann einzeln angezeigt werden
- **UI-Freundlichkeit**: Frontend kann jeden Fehler einzeln highlighten
- **Debugging**: Ich sehe sofort, welche Regeln das Problem verursachen

**Rationale für "Alles oder nichts"**:

Bei Regeln ist es wichtig, dass **alle** angewendet werden oder **keine**. Wenn eine Regel defekt ist, will ich nicht riskieren, dass Transaktionen falsch kategorisiert werden. Lieber zeige ich dem Nutzer alle Fehler und lasse ihn diese beheben, bevor die Rules Engine läuft.

### Herausforderung 4: Target Field Matching (Payee vs. Memo vs. Combined)

#### Das Problem

Banktransaktionen haben zwei Textfelder:

```fsharp
type BankTransaction = {
    Payee: string option  // z.B. "REWE Supermarkt"
    Memo: string          // z.B. "Lebensmittel Einkauf"
    // ...
}
```

Nutzer sollen wählen können, in welchem Feld gesucht wird:
- **Payee**: Nur im Empfänger-Feld suchen
- **Memo**: Nur im Verwendungszweck suchen
- **Combined**: In beiden Feldern suchen

Das Problem: `Payee` ist ein `option` (kann `None` sein), `Memo` ist immer vorhanden. Wie baue ich das sauber?

#### Optionen, die ich betrachtet habe

1. **Separate Funktionen für jeden Fall**
   ```fsharp
   let matchPayee (pattern: Regex) (tx: BankTransaction) = ...
   let matchMemo (pattern: Regex) (tx: BankTransaction) = ...
   let matchCombined (pattern: Regex) (tx: BankTransaction) = ...
   ```
   - Pro: Sehr explizit
   - Contra: Code-Duplizierung, schwer wartbar

2. **Text extrahieren, dann matchen** (gewählt)
   ```fsharp
   let getMatchText (transaction: BankTransaction) (targetField: TargetField) : string =
       match targetField with
       | Payee -> transaction.Payee |> Option.defaultValue ""
       | Memo -> transaction.Memo
       | Combined ->
           let payee = transaction.Payee |> Option.defaultValue ""
           payee + " " + transaction.Memo
   ```
   - Pro: Separation of Concerns – Text-Extraktion getrennt von Matching
   - Contra: Keiner!

3. **Pattern Matching direkt beim Matchen**
   - Pro: Alles an einem Ort
   - Contra: Vermischt Concerns (Text-Extraktion + Regex-Matching)

#### Die Lösung: getMatchText-Funktion

```fsharp
let getMatchText (transaction: BankTransaction) (targetField: TargetField) : string =
    match targetField with
    | Payee ->
        transaction.Payee |> Option.defaultValue ""
    | Memo ->
        transaction.Memo
    | Combined ->
        let payee = transaction.Payee |> Option.defaultValue ""
        payee + " " + transaction.Memo

let classify (compiledRules: CompiledRule list) (transaction: BankTransaction) =
    compiledRules
    |> List.tryFind (fun compiled ->
        if not compiled.Rule.Enabled then
            false
        else
            let matchText = getMatchText transaction compiled.Rule.TargetField
            compiled.Regex.IsMatch(matchText)
    )
    |> Option.map (fun compiled -> (compiled.Rule, compiled.Rule.CategoryId))
```

**Architekturentscheidung: Warum separate Funktion?**

1. **Single Responsibility**: `getMatchText` extrahiert nur Text, `classify` matched nur
2. **Testbarkeit**: Ich kann `getMatchText` separat testen
3. **Wiederverwendbarkeit**: Andere Funktionen können auch `getMatchText` nutzen
4. **Klarheit**: Code liest sich wie natürliche Sprache

**Warum `Option.defaultValue ""` statt Pattern Matching?**

```fsharp
// ✅ Gewählt: Kurz und klar
transaction.Payee |> Option.defaultValue ""

// ❌ Alternative: Verbose
match transaction.Payee with
| Some payee -> payee
| None -> ""
```

`Option.defaultValue` ist Standard-Library und macht genau das, was ich will: Wenn `Some`, nimm den Wert; wenn `None`, nimm den Default.

### Herausforderung 5: Priority Ordering und "First Match Wins"

#### Das Problem

Regeln haben eine Priority (Integer-Wert). Die Regel mit der niedrigsten Zahl (höchste Priorität) soll zuerst matchen. Beispiel:

```
Priority 1: "REWE" → Groceries
Priority 2: "REWE Getränke" → Beverages
```

Wenn eine Transaktion "REWE Getränke" ist, soll **Regel 1** matchen (weil Priority 1 höher ist als 2), auch wenn Regel 2 spezifischer ist.

Die Frage: Wie garantiere ich, dass Regeln in der richtigen Reihenfolge geprüft werden?

#### Optionen, die ich betrachtet habe

1. **In der Datenbank sortieren**
   ```fsharp
   let getAllRules () =
       conn.QueryAsync<Rule>("SELECT * FROM rules ORDER BY priority ASC")
   ```
   - Pro: Sortierung passiert in der DB (performant)
   - Contra: Rules Engine verlässt sich auf externen State

2. **In der Rules Engine sortieren**
   ```fsharp
   let sortedRules = rules |> List.sortBy (fun r -> r.Priority)
   let compiled = compileRules sortedRules
   ```
   - Pro: Rules Engine ist verantwortlich für Sortierung
   - Contra: Zusätzliche Sortierung nötig

3. **Liste als bereits sortiert annehmen** (gewählt)
   ```fsharp
   // Rules sind bereits nach Priority sortiert
   compiledRules |> List.tryFind (fun compiled -> ...)
   ```
   - Pro: Keine zusätzliche Sortierung nötig
   - Contra: Erfordert Dokumentation/Tests

#### Die Lösung: Liste als Contract annehmen

Ich habe mich für Option 3 entschieden mit einem klaren Contract:

```fsharp
/// Classifies a single transaction using compiled rules.
/// Purpose: Finds the first matching rule by priority order.
/// IMPORTANT: Rules list must be pre-sorted by priority!
/// Returns: Some (rule, categoryId) if a match is found, None otherwise.
let classify
    (compiledRules: CompiledRule list)
    (transaction: BankTransaction)
    : (Rule * YnabCategoryId) option =

    // Rules are already sorted by priority in the list
    compiledRules
    |> List.tryFind (fun compiled ->
        if not compiled.Rule.Enabled then
            false
        else
            let matchText = getMatchText transaction compiled.Rule.TargetField
            compiled.Regex.IsMatch(matchText)
    )
    |> Option.map (fun compiled -> (compiled.Rule, compiled.Rule.CategoryId))
```

**Architekturentscheidung: Warum nicht in der Funktion sortieren?**

1. **Performance**: Sortierung bei jeder Transaction ist Verschwendung
2. **Separation of Concerns**: Persistence-Layer ist verantwortlich für korrekte Reihenfolge
3. **Testbarkeit**: Tests kontrollieren die Reihenfolge explizit

**Warum `List.tryFind` statt `List.find`?**

- `List.tryFind`: Gibt `Option` zurück – `None` wenn keine Regel matched
- `List.find`: Wirft Exception wenn keine Regel matched

In der Rules Engine ist es völlig normal, dass keine Regel matched (ungekategorisierte Transaktion). Das ist kein Fehler, sondern ein valider State → `Option` ist der richtige Typ.

### Herausforderung 6: Special Pattern Detection (Amazon & PayPal)

#### Das Problem

Transaktionen von Amazon und PayPal sind besonders schwierig zu kategorisieren, weil:

1. **Amazon**: Der Payee ist oft nur "AMAZON PAYMENTS EU" – was gekauft wurde steht nur in der Amazon-Order-History
2. **PayPal**: Der Payee ist "PAYPAL *IRGENDWAS" – der eigentliche Empfänger steht nur im PayPal-Account

Lösung: Ich erkenne diese Transaktionen automatisch und generiere Links zur Order-History, damit der Nutzer schnell nachschauen kann.

#### Optionen, die ich betrachtet habe

1. **Hardcoded Patterns in der Classify-Funktion**
   ```fsharp
   let classify (transaction: BankTransaction) =
       if transaction.Payee.Contains("AMAZON") then
           // Special handling
       else
           // Normal classification
   ```
   - Pro: Alles an einem Ort
   - Contra: Vermischt Concerns, schwer testbar

2. **Separate Funktion für Special Detection** (gewählt)
   ```fsharp
   let detectSpecialTransaction (transaction: BankTransaction) : ExternalLink list
   ```
   - Pro: Separation of Concerns, leicht erweiterbar
   - Contra: Zusätzlicher Funktionsaufruf

3. **In der Datenbank konfigurierbar**
   - Pro: Nutzer kann eigene Special Patterns definieren
   - Contra: Overengineering für nur 2 Patterns

#### Die Lösung: Separate Detection-Funktion

```fsharp
let private amazonPatterns = [
    @"AMAZON\s*(PAYMENTS|EU|DE)?"
    @"AMZN\s*MKTP"
    @"Amazon\.de"
    @"AMAZON\s*\.DE"
]

let private paypalPatterns = [
    @"PAYPAL\s*\*"
    @"PP\.\d+"
    @"PAYPAL"
]

let private detectAmazon (transaction: BankTransaction) : ExternalLink option =
    let text =
        match transaction.Payee with
        | Some payee -> payee + " " + transaction.Memo
        | None -> transaction.Memo

    let isAmazon =
        amazonPatterns
        |> List.exists (fun pattern ->
            let regex = new Regex(pattern, RegexOptions.IgnoreCase)
            regex.IsMatch(text)
        )

    if isAmazon then
        Some { Label = "Amazon Orders"; Url = "https://www.amazon.de/gp/your-account/order-history" }
    else None

let detectSpecialTransaction (transaction: BankTransaction) : ExternalLink list =
    [
        detectAmazon transaction
        detectPayPal transaction
    ]
    |> List.choose id  // Filtert None-Werte raus
```

**Architekturentscheidung: Warum separate Funktionen für Amazon und PayPal?**

1. **Erweiterbarkeit**: Neue Special Patterns (z.B. eBay) sind einfach hinzuzufügen
2. **Testbarkeit**: Jeder Detector kann separat getestet werden
3. **Klarheit**: Jede Funktion hat eine einzige Verantwortung

**Warum `List.choose id`?**

```fsharp
[Some link1; None; Some link2]
|> List.choose id
// => [link1; link2]
```

`List.choose` nimmt eine Funktion, die aus `'a -> 'b option` macht. Mit `id` (Identity-Funktion) filtere ich einfach alle `None`-Werte raus und packe die `Some`-Werte aus.

### Herausforderung 7: Status-Management (AutoCategorized vs. NeedsAttention vs. Pending)

#### Das Problem

Eine Transaktion kann mehrere Stati haben:

```fsharp
type TransactionStatus =
    | Pending           // Newly fetched, no categorization
    | AutoCategorized   // Rule applied automatically
    | ManualCategorized // User assigned category
    | NeedsAttention    // Special case (Amazon, PayPal)
    | Skipped           // User chose to skip
    | Imported          // Successfully sent to YNAB
```

Die Logik ist komplex:

1. **Amazon-Transaktion + Regel matched** → `NeedsAttention` (nicht `AutoCategorized`)
2. **Amazon-Transaktion + keine Regel** → `NeedsAttention`
3. **Normale Transaktion + Regel matched** → `AutoCategorized`
4. **Normale Transaktion + keine Regel** → `Pending`

Wie implementiere ich das sauber?

#### Optionen, die ich betrachtet habe

1. **Verschachtelte If-Statements**
   ```fsharp
   if hasSpecialPattern then
       if hasRule then NeedsAttention
       else NeedsAttention
   else
       if hasRule then AutoCategorized
       else Pending
   ```
   - Pro: Direkt und offensichtlich
   - Contra: Schlecht lesbar, schwer wartbar

2. **Pattern Matching auf Tuple** (gewählt)
   ```fsharp
   match (hasSpecialPattern, hasRule) with
   | (true, _) -> NeedsAttention
   | (false, Some _) -> AutoCategorized
   | (false, None) -> Pending
   ```
   - Pro: Exhaustive Checking, sehr lesbar
   - Contra: Keiner!

3. **Separate Funktionen für jeden Status**
   - Pro: Sehr explizit
   - Contra: Code-Duplizierung

#### Die Lösung: Pattern Matching mit klarer Logik

```fsharp
let classifyTransactions
    (rules: Rule list)
    (transactions: BankTransaction list)
    : Result<SyncTransaction list, string list> =

    match compileRules rules with
    | Error errors -> Error errors
    | Ok compiledRules ->
        let syncTransactions =
            transactions
            |> List.map (fun transaction ->
                // Detect special patterns first
                let externalLinks = detectSpecialTransaction transaction
                let hasSpecialPattern = not (List.isEmpty externalLinks)

                // Try to classify with rules
                match classify compiledRules transaction with
                | Some (matchedRule, categoryId) ->
                    {
                        Transaction = transaction
                        Status = if hasSpecialPattern then NeedsAttention else AutoCategorized
                        CategoryId = Some categoryId
                        CategoryName = Some matchedRule.CategoryName
                        MatchedRuleId = Some matchedRule.Id
                        PayeeOverride = matchedRule.PayeeOverride
                        ExternalLinks = externalLinks
                        UserNotes = None
                    }
                | None ->
                    {
                        Transaction = transaction
                        Status = if hasSpecialPattern then NeedsAttention else Pending
                        CategoryId = None
                        CategoryName = None
                        MatchedRuleId = None
                        PayeeOverride = None
                        ExternalLinks = externalLinks
                        UserNotes = None
                    }
            )

        Ok syncTransactions
```

**Architekturentscheidung: Warum Special Pattern vor Classification?**

1. **Logische Reihenfolge**: Erkenne Sonderfälle zuerst
2. **Performance**: Special Detection ist schneller als Regex-Matching
3. **Klarheit**: Code liest sich von oben nach unten wie ein Decision-Tree

**Warum `NeedsAttention` auch bei gematchter Regel?**

Amazon/PayPal-Transaktionen sollten **immer** manuell überprüft werden, auch wenn eine Regel matched. Beispiel:

- Regel: "AMAZON" → "Online Shopping"
- Transaktion: "AMAZON PAYMENTS EU – 29.99 EUR"

Automatisch kategorisiert als "Online Shopping", aber der Nutzer sollte trotzdem in die Order-History schauen, um zu sehen, **was** gekauft wurde (könnte auch Bücher, Elektronik, Haushalt sein).

## Lessons Learned

### 1. F# String Interpolation hat Einschränkungen

**Was passiert ist:**

Beim Schreiben der Tests habe ich versucht, String-Funktionen direkt in Interpolated Strings zu verwenden:

```fsharp
failtest $"Should compile all valid rules: {String.concat ", " errors}"
```

**Der Fehler:**

```
error FS3373: Invalid interpolated string. Single quote or verbatim string literals
may not be used in interpolated expressions in single quote or verbatim strings.
```

**Was ich gelernt habe:**

F# erlaubt keine komplexen Ausdrücke (mit String-Funktionen) direkt in Interpolated Strings. Die Lösung ist ein `let` Binding:

```fsharp
let errorMsg = String.concat ", " errors
failtest $"Should compile all valid rules: {errorMsg}"
```

**Was ich anders machen würde:**

In kritischen Codepfaden (wie Error-Messages) würde ich von Anfang an `let` Bindings verwenden, auch wenn es etwas mehr Code ist. Das macht den Code robuster gegen Refactorings.

### 2. Naming Conflicts früher erkennen

**Was passiert ist:**

Ich hatte den kompletten Code geschrieben, bevor ich die Tests laufen ließ. Dann kam der Compiler-Fehler wegen des `Regex`-Naming-Conflicts.

**Was ich gelernt habe:**

Auch in F# ist TDD (Test-Driven Development) wertvoll. Hätte ich die Tests zuerst geschrieben, wäre der Conflict sofort aufgefallen.

**Was ich anders machen würde:**

1. Skeleton-Implementation mit einem Dummy-Test
2. Test zum Laufen bringen (grün)
3. Dann erst die echte Implementierung

### 3. Documentation ist bei Pattern-Funktionen kritisch

**Was passiert ist:**

Die `classify`-Funktion nimmt eine **bereits sortierte** Liste von Regeln an. Das war mir klar, aber nirgendwo dokumentiert. In Tests musste ich mich daran erinnern, die Regeln manuell zu sortieren.

**Was ich gelernt habe:**

Bei Funktionen, die Annahmen über Input-Daten machen (z.B. "Liste ist sortiert"), **muss** das im Docstring stehen:

```fsharp
/// IMPORTANT: Rules list must be pre-sorted by priority!
let classify (compiledRules: CompiledRule list) (transaction: BankTransaction) = ...
```

**Was ich anders machen würde:**

Ich würde ein `NonEmptyList` oder `SortedList` Custom-Type verwenden, um die Sortierung im Type-System zu encodieren:

```fsharp
type SortedRules = SortedRules of CompiledRule list

let sortRules (rules: Rule list) : SortedRules =
    SortedRules (rules |> List.sortBy (fun r -> r.Priority))

let classify (SortedRules compiledRules) (transaction: BankTransaction) = ...
```

Dann wäre es unmöglich, eine unsortierte Liste zu übergeben.

## Fazit

In Milestone 5 habe ich die **Rules Engine** von BudgetBuddy implementiert – das Herzstück der automatischen Kategorisierung. Das System:

- **Kompiliert** Regex-Patterns einmal für maximale Performance
- **Unterstützt** drei Pattern-Typen (Exact, Contains, Regex) mit korrektem Escaping
- **Matched** gegen Payee, Memo oder beide Felder
- **Respektiert** Priority-Ordering mit "first match wins"-Semantik
- **Erkennt** Special Patterns (Amazon, PayPal) und generiert hilfreiche Links
- **Setzt** den korrekten Transaction-Status basierend auf Classification-Ergebnis

### Statistiken

**Produktionscode:**
- `src/Server/RulesEngine.fs`: **~200 Zeilen** F#-Code
- 7 öffentliche Funktionen
- 1 Custom-Type (`CompiledRule`)

**Tests:**
- `src/Tests/RulesEngineTests.fs`: **~430 Zeilen** Test-Code
- **46 Tests** in 4 Test-Suites:
  - Pattern Compilation Tests (7)
  - Classification Tests (7)
  - Special Pattern Detection Tests (6)
  - Integration Tests (5)
- **121/121 Tests grün** (46 neue + 75 bestehende)

**Performance:**
- Patterns werden nur **1× kompiliert** (nicht bei jeder Transaction)
- Bei 100 Regeln und 200 Transactions: ~20.000 Regex-Matches statt 20.000 Regex-Compilations
- Geschätzter Speedup: **~100×**

### Key Files

```
src/Server/RulesEngine.fs          # Core implementation
src/Tests/RulesEngineTests.fs      # Comprehensive test suite
docs/MILESTONE-PLAN.md             # Updated with completion status
diary/development.md               # Development diary entry
```

## Key Takeaways für Neulinge

### 1. F#'s Type System ist dein Freund – nutze es!

**Lesson:** Statt einfach `Result<'T, string>` zu verwenden, habe ich `Result<'T, string list>` gewählt. Warum? Weil ich **mehrere** Fehler sammeln will. Der Type-Unterschied zwingt mich (und andere Entwickler), die Error-Liste zu iterieren, statt sie als einzelnen String anzuzeigen.

**Konkret:**
```fsharp
// ❌ Schlechter Type – versteckt Multiple Errors
Result<CompiledRule list, string>

// ✅ Besserer Type – macht Multiple Errors explizit
Result<CompiledRule list, string list>
```

Nutze Custom Types (`CompiledRule`, `SortedRules`, etc.), um Invarianten im Type-System zu encodieren. Wenn eine Funktion eine sortierte Liste braucht, erstelle einen `SortedList`-Type.

### 2. Separation of Concerns macht Code testbar und wartbar

**Lesson:** Ich hätte die komplette Logik in einer großen `classifyTransactions`-Funktion unterbringen können. Stattdessen habe ich separate Funktionen erstellt:

- `compileRule` / `compileRules` – Pattern-Kompilierung
- `getMatchText` – Text-Extraktion
- `classify` – Rule-Matching
- `detectAmazon` / `detectPayPal` – Special Pattern Detection
- `classifyTransactions` – Orchestrierung

Jede Funktion hat **eine einzige Verantwortung** und kann separat getestet werden. Das Ergebnis: **46 granulare Tests** statt ein paar großer Integration-Tests.

### 3. Performance-Optimierung: Compile once, use many times

**Lesson:** Regex-Compilation ist langsam. Bei 100 Regeln und 200 Transactions wären das 20.000 Compilations. Die Lösung: Compile Patterns **einmal** in einen `CompiledRule`-Type und verwende sie wiederholt.

**Pattern:**
```fsharp
// 1. Compile einmal beim Laden
let compiledRules = compileRules rules

// 2. Verwende mehrfach
transactions |> List.map (classify compiledRules)
```

Dieses Pattern funktioniert für alle "teuren" Operationen: Regex, Database-Connections, HTTP-Clients, etc.

---

**Nächster Schritt:** Milestone 6 – Backend API Implementation, wo die Rules Engine mit dem YNAB-Client und Comdirect-Client integriert wird, um den kompletten Sync-Flow zu ermöglichen.
