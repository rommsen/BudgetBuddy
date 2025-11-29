---
title: "Milestone 6: Backend API Implementation – 29 Endpoints und die Kunst der Fehlerbehandlung in F#"
date: 2025-11-29
author: Claude
tags: [F#, Backend, Fable.Remoting, API-Design, Type Safety]
description: "Von isolierten Modulen zur vollständigen API: 29 Endpoints mit konsistenter Fehlerbehandlung, Input-Validation und Session-Management."
---

# Milestone 6: Backend API Implementation – 29 Endpoints und die Kunst der Fehlerbehandlung in F#

## Einleitung: Von isolierten Modulen zur vollständigen API

Nach der Implementierung der Grundbausteine (Persistence, YnabClient, ComdirectClient, RulesEngine) stand ich vor der Aufgabe, diese isolierten Module zu einer zusammenhängenden API zu verbinden. Milestone 6 war der Wendepunkt: Aus einzelnen Backend-Services sollte eine vollständige Fable.Remoting-API mit 29 Endpoints entstehen, die bereit für die Frontend-Integration ist.

Die Herausforderung war nicht nur die schiere Anzahl der Endpoints, sondern vor allem die **konsistente Fehlerbehandlung**, **Input-Validation** und **Session-Management** über alle API-Ebenen hinweg. Jeder Endpoint sollte typed errors zurückgeben, alle Eingaben validieren und sauber mit der darunterliegenden Infrastruktur kommunizieren.

Das Ergebnis: 3 neue Module (Validation.fs, SyncSessionManager.fs, Api.fs) mit insgesamt über 800 Zeilen Code, die 4 API-Schnittstellen (SettingsApi, YnabApi, RulesApi, SyncApi) implementieren und zu einer einzigen AppApi zusammenführen.

## Ausgangslage: Was war bereits vorhanden?

Vor Milestone 6 hatte ich bereits eine solide Backend-Infrastruktur:

- **Persistence.fs**: SQLite-Datenbank mit Submodulen für Settings, Rules, SyncSessions, SyncTransactions
- **YnabClient.fs**: Integration mit der YNAB API für Budget- und Transaktions-Management
- **ComdirectClient.fs**: OAuth-Flow und Transaction-Fetching für Comdirect Bank
- **ComdirectAuthSession.fs**: In-Memory Session-Management für den OAuth-Flow
- **RulesEngine.fs**: Automatische Kategorisierung von Transaktionen basierend auf Patterns

Was fehlte, war die **Verbindungsschicht**: Ein API-Layer, der diese Services orchestriert, Eingaben validiert, Fehler konsistent behandelt und eine typsichere Schnittstelle für das Frontend bietet.

## Die Haupt-Herausforderungen

### Herausforderung 1: Input-Validation – Fehlerakkumulation statt Early-Return

#### Das Problem

In imperativen Sprachen validiert man oft mit early returns:

```csharp
// C#-Stil (NICHT F#)
if (string.IsNullOrWhiteSpace(token)) {
    return Error("Token required");  // Early return
}
if (token.Length < 10) {
    return Error("Token too short");  // Zweiter Check nie erreicht
}
```

Das Problem: Der Benutzer sieht nur den **ersten** Fehler. Bei einem Formular mit 5 Feldern muss er 5-mal submitten, um alle Fehler zu sehen.

#### Optionen, die ich betrachtet habe

1. **Early Returns mit Result** (typisch in C#/TypeScript)
   - Pro: Einfach zu verstehen, sequentielle Logik
   - Contra: Nur ein Fehler pro Validation, schlechte UX

2. **Exception-basiert** (Java/C#-Stil)
   - Pro: Stack unwinding automatisch
   - Contra: Exceptions für Control-Flow ist Anti-Pattern, nicht typsicher

3. **Applicative Validation** (gewählt)
   - Pro: Sammelt **alle** Fehler, beste UX
   - Contra: Etwas komplexer zu implementieren

#### Die Lösung: Validation.fs mit List-Comprehensions

Ich habe mich für einen funktionalen Ansatz entschieden, der alle Fehler sammelt:

```fsharp
let validateYnabToken (token: string) : Result<string, string list> =
    let errors =
        [
            validateRequired "YNAB token" token
            validateLength "YNAB token" 10 500 token
        ]
        |> List.choose id  // Filtert None-Werte raus

    if errors.IsEmpty then Ok token else Error errors
```

**Warum diese Struktur?**

1. **List-Comprehension**: Jeder Validator gibt `Option<string>` zurück (Some "error" oder None)
2. **List.choose id**: Filtert automatisch alle `None`-Werte heraus, übrig bleiben nur Fehlermeldungen
3. **Result<'T, string list>**: Der Rückgabetyp kommuniziert: "Entweder ein gültiger Wert ODER eine Liste von Fehlern"

**Reusable Validators als Building Blocks**:

```fsharp
let validateRequired (fieldName: string) (value: string) =
    if String.IsNullOrWhiteSpace(value) then
        Some $"{fieldName} is required"
    else
        None

let validateLength (fieldName: string) (minLen: int) (maxLen: int) (value: string) =
    let len = value.Length
    if len < minLen || len > maxLen then
        Some $"{fieldName} must be between {minLen} and {maxLen} characters"
    else
        None
```

Diese Validator-Funktionen sind **composable**: Ich kann sie beliebig kombinieren, ohne Code zu duplizieren.

**Rationale für Result<'T, string list>**:
- F#'s Result-Type ist **typsicher**: Der Compiler zwingt mich, Fehler zu behandeln
- `string list` statt `string`: Ermöglicht Fehlerakkumulation
- Pattern Matching macht Fehlerbehandlung explizit:

```fsharp
match validateYnabToken token with
| Error errors -> return Error (String.concat "; " errors)
| Ok validToken -> // weiter mit gültigem Token
```

### Herausforderung 2: Module-Referenzen in F# – Submodule vs. Top-Level

#### Das Problem

Beim ersten Versuch, `Settings.setSetting` aufzurufen, bekam ich:

```
error FS0039: The value, namespace, type or module 'Settings' is not defined.
```

Warum? **Persistence.fs hat Submodule**:

```fsharp
module Persistence

module Encryption = ...
module Rules = ...
module Settings = ...
```

#### Optionen, die ich betrachtet habe

1. **Alle Module als Top-Level** (gewählt für andere Module)
   - Pro: Einfache Referenzen (`YnabClient.getBudgets`)
   - Contra: Namespace-Pollution, weniger Organisation

2. **Nested Modules** (gewählt für Persistence)
   - Pro: Logische Gruppierung, klare Organisation
   - Contra: Längere Pfade (`Persistence.Settings.setSetting`)

3. **Module-Aliasing** (könnte man machen)
   ```fsharp
   module Settings = Persistence.Settings
   ```
   - Pro: Kürzere Namen im Code
   - Contra: Zusätzliche Indirektion, nicht klar woher es kommt

#### Die Lösung: Qualified Access mit expliziten Pfaden

Ich habe mich entschieden, **explizite Pfade** zu verwenden:

```fsharp
open Persistence  // Öffnet das Hauptmodul

// Dann überall:
do! Persistence.Settings.setSetting "ynab_token" validToken true
let! rules = Persistence.Rules.getAllRules()
do! Persistence.SyncSessions.updateSession session
```

**Warum diese Entscheidung?**

1. **Klarheit**: Sofort ersichtlich, welches Submodul verwendet wird
2. **Keine Namenskonflikte**: `Settings` könnte auch woanders existieren
3. **IntelliSense**: IDE zeigt alle verfügbaren Submodule
4. **Konsistenz**: Einheitliches Pattern über die ganze Codebasis

**F#-Lesson für Neulinge**:
Module in F# sind **nicht wie Namespaces** in C#. Ein Module kann:
- Funktionen enthalten
- Andere Module enthalten (Submodule)
- Mit `open` geöffnet werden
- Qualified access enforced haben (`[<RequireQualifiedAccess>]`)

### Herausforderung 3: Encrypted Settings – Security by Default

#### Das Problem

Beim ersten Aufruf von `Persistence.Settings.setSetting` bekam ich:

```
error FS0001: This expression was expected to have type 'Async<'a>'
but here has type 'bool -> Async<unit>'
```

Die Funktion erwartet **drei** Parameter, ich gab nur zwei. Der fehlende Parameter: `encrypted: bool`.

#### Optionen, die ich betrachtet habe

1. **Zwei separate Funktionen** (setSetting, setSecureSetting)
   - Pro: API macht klar, was encrypted wird
   - Contra: Code-Duplikation, leicht falsche Funktion zu wählen

2. **Encryption immer an** (zu restriktiv)
   - Pro: Maximale Security
   - Contra: Performance-Overhead für nicht-sensitive Daten

3. **Boolean-Parameter** (gewählt)
   - Pro: Flexibel, explizit, ein Call-Site
   - Contra: Caller muss an Encryption denken

4. **Separate Typen** (über-engineered für Single-User-App)
   ```fsharp
   type SecureSetting = private SecureSetting of string
   type PlainSetting = PlainSetting of string
   ```

#### Die Lösung: Explizites encrypted-Flag mit klarem Pattern

```fsharp
// Sensitive Daten - encrypted: true
do! Persistence.Settings.setSetting "ynab_token" validToken true
do! Persistence.Settings.setSetting "comdirect_client_secret" valid.ClientSecret true
do! Persistence.Settings.setSetting "comdirect_password" valid.Password true

// Nicht-sensitive Daten - encrypted: false
do! Persistence.Settings.setSetting "ynab_default_budget_id" id false
do! Persistence.Settings.setSetting "comdirect_username" valid.Username false
do! Persistence.Settings.setSetting "sync_days_to_fetch" (string valid.DaysToFetch) false
```

**Pattern für die Entscheidung**:
- **Encrypted (true)**: Tokens, Secrets, Passwords
- **Plain (false)**: IDs, Usernames, Konfigurationswerte

**Rationale für Boolean statt separater Typen**:

In einer **Enterprise-App** würde ich separate Typen verwenden:
```fsharp
type EncryptedValue = private EncryptedValue of string
type PlainValue = PlainValue of string
```

Aber BudgetBuddy ist eine **Self-Hosted Single-User-App**:
- Keine komplexe Permissions-Struktur
- Nur ein User, der alle Settings verwaltet
- Pragmatismus > theoretische Perfektion

Das boolean-Flag ist **gut genug** und macht den Code lesbarer.

### Herausforderung 4: Session Management für Sync-Workflow

#### Das Problem

Der Sync-Flow ist **mehrstufig und stateful**:

1. User startet Sync → Session erstellen
2. Comdirect OAuth → Session mit Challenge updaten
3. User bestätigt TAN → Transactions fetchen
4. User kategorisiert → Transactions in Session speichern
5. Import zu YNAB → Session abschließen

Wie speichere ich diesen State zwischen API-Calls?

#### Optionen, die ich betrachtet habe

1. **In-Memory mit Mutable Refs** (gewählt)
   - Pro: Schnell, einfach, kein DB-Overhead
   - Contra: State verloren bei Server-Restart

2. **In der Datenbank** (für Multi-User)
   - Pro: Persistent, skaliert zu Multiple-Users
   - Contra: Overhead für jeden State-Update

3. **Frontend-State** (React/Elmish)
   - Pro: Server stateless
   - Contra: Große Transaktionslisten im Frontend, komplexe State-Management

4. **Redis/External Cache** (overkill)
   - Pro: Skaliert horizontal
   - Contra: Zusätzliche Dependency für Single-User-App

#### Die Lösung: SyncSessionManager.fs mit In-Memory State

```fsharp
module Server.SyncSessionManager

type SessionState = {
    Session: SyncSession
    Transactions: Dictionary<TransactionId, SyncTransaction>
}

let private currentSession : SessionState option ref = ref None

let startNewSession () : SyncSession =
    let session = {
        Id = SyncSessionId (Guid.NewGuid())
        StartedAt = DateTime.UtcNow
        CompletedAt = None
        Status = AwaitingBankAuth
        TransactionCount = 0
        ImportedCount = 0
        SkippedCount = 0
    }

    currentSession := Some {
        Session = session
        Transactions = Dictionary<TransactionId, SyncTransaction>()
    }

    session

let updateTransaction (updatedTx: SyncTransaction) : unit =
    match currentSession.Value with
    | Some state ->
        state.Transactions.[updatedTx.Transaction.Id] <- updatedTx
    | None ->
        failwith "No active session to update transaction in"
```

**Architekturentscheidung: Warum ein separates Modul?**

1. **Separation of Concerns**:
   - `Api.fs` orchestriert
   - `SyncSessionManager.fs` verwaltet State
   - `Persistence.fs` speichert in DB

2. **Testbarkeit**:
   - `clearSession()` für Test-Setup
   - `getCurrentSession()` für Assertions
   - Kein DB-Mock nötig

3. **Klarheit**:
   - Alle Session-Operations an einem Ort
   - Validation-Helpers (`validateSession`, `validateSessionStatus`)

**Rationale für Mutable Refs**:

BudgetBuddy ist eine **Self-Hosted Single-User-App**:
- Nur ein User zur Zeit
- Server läuft auf lokalem Docker-Container
- Restart = kein Problem (Session neu starten)

In F# ist **Mutability explizit**:
```fsharp
let mutable x = 5      // Mutable value
let y = ref 10         // Reference cell
```

Das macht es **deutlich**, wo State mutiert wird – anders als in C# wo alles mutable ist.

**F#-Lesson für Neulinge**:
- `ref` erstellt eine mutable reference cell
- `:=` setzt den Wert
- `.Value` liest den Wert
- Pattern: Mutable State isoliert in ein Modul, Rest der Codebase pure

### Herausforderung 5: Type Mismatches in Pattern Validation

#### Das Problem

Beim Validieren von Rule-Patterns bekam ich:

```
error FS0001: This expression was expected to have type 'unit'
but here has type 'Result<'a,'b>'
```

Der Code sah so aus:
```fsharp
if validRequest.Pattern.IsSome || validRequest.PatternType.IsSome then
    match compileRule updated with
    | Error err -> return Error (RulesError.InvalidPattern (updated.Pattern, err))
    | Ok _ -> ()
```

Das Problem: `return` in einem Branch, `()` im anderen. F# mag keine gemischten Return-Typen.

#### Optionen, die ich betrachtet habe

1. **if-else mit return in beiden Branches** (funktioniert nicht)
   ```fsharp
   if condition then
       match validation with
       | Error e -> return Error e
       | Ok _ -> return Ok ()  // Braucht zweites async {}
   ```
   - Contra: Verschachtelte async-Blöcke

2. **Validation-Result binden und dann matchen** (gewählt)
   ```fsharp
   let validationResult = compileRule updated |> Result.map (fun _ -> ())
   match validationResult with
   | Error err -> return Error err
   | Ok () -> // weiter
   ```

3. **Exception werfen** (nicht idiomatisch in F#)
   - Contra: Exceptions für Control-Flow

#### Die Lösung: Result.map für Type-Alignment

```fsharp
// Validate pattern compiles if pattern changed
let patternValidation =
    if validRequest.Pattern.IsSome || validRequest.PatternType.IsSome then
        compileRule updated
        |> Result.map (fun _ -> ())  // CompiledRule -> unit
        |> Result.mapError (fun err -> RulesError.InvalidPattern (updated.Pattern, err))
    else
        Ok ()

match patternValidation with
| Error err -> return Error err
| Ok () ->
    // Continue with rest of logic
```

**Warum Result.map?**

`compileRule` gibt `Result<CompiledRule, string>` zurück, aber ich brauche nur **ob** es kompiliert, nicht **das Ergebnis**.

`Result.map (fun _ -> ())` konvertiert:
- `Ok compiledRule` → `Ok ()`
- `Error msg` → `Error msg`

Jetzt haben beide Branches den gleichen Typ: `Result<unit, RulesError>`

**F#-Lesson für Neulinge**:

Result ist ein **Functor** (kann gemapped werden):
```fsharp
// Signatur von Result.map:
val map : ('T -> 'U) -> Result<'T, 'Error> -> Result<'U, 'Error>

// Beispiel:
Result<int, string>         |> Result.map (fun x -> x * 2)
// → Result<int, string>

Result<CompiledRule, string> |> Result.map (fun _ -> ())
// → Result<unit, string>
```

Das ist **Type-Tetris**: Typen so transformieren, dass sie passen.

### Herausforderung 6: Async Error Handling mit komplexem Nesting

#### Das Problem

Viele API-Endpoints haben **mehrere Failure-Punkte**:

```fsharp
confirmTan = fun sessionId -> async {
    // 1. Session validieren
    match validateSession sessionId with
    | Error err -> return Error err
    | Ok _ ->
        // 2. TAN bestätigen (kann fehlschlagen)
        match! confirmTan() with
        | Error err -> return Error err
        | Ok _ ->
            // 3. Transactions fetchen (kann fehlschlagen)
            match! fetchTransactions accountId days with
            | Error err -> return Error err
            | Ok transactions ->
                // 4. Rules anwenden (kann fehlschlagen)
                match classifyTransactions rules transactions with
                | Error err -> return Error err
                | Ok classified ->
                    // Endlich Erfolg!
                    return Ok classified
}
```

Das ist die berüchtigte **"Pyramid of Doom"**.

#### Optionen, die ich betrachtet habe

1. **asyncResult Computation Expression** (ideal, aber nicht eingebaut)
   ```fsharp
   asyncResult {
       let! session = validateSession sessionId
       let! _ = confirmTan()
       let! transactions = fetchTransactions accountId days
       let! classified = classifyTransactions rules transactions
       return classified
   }
   ```
   - Pro: Flach, lesbar, idiomatisch
   - Contra: Braucht FsToolkit.ErrorHandling (nicht im Projekt)

2. **Railway-Oriented Programming** (elegant, aber komplex)
   - Pro: Funktional, composable
   - Contra: Hohe Lernkurve für Neulinge

3. **Explicit Matching mit Early Returns** (gewählt)
   - Pro: Explizit, klar, funktioniert OOTB
   - Contra: Verschachtelt, repetitiv

#### Die Lösung: Strukturiertes Pattern Matching mit klarer Indentation

```fsharp
confirmTan = fun sessionId -> async {
    match SyncSessionManager.validateSessionStatus sessionId AwaitingTan with
    | Error err -> return Error err
    | Ok _ ->
        // Complete TAN flow
        match! ComdirectAuthSession.confirmTan() with
        | Error comdirectError ->
            let errorMsg = comdirectErrorToString comdirectError
            SyncSessionManager.failSession errorMsg
            return Error (SyncError.ComdirectAuthFailed errorMsg)
        | Ok _ ->
            // Update status
            SyncSessionManager.updateSessionStatus FetchingTransactions

            // Get account ID
            let! settings = settingsApi.getSettings()
            let accountId =
                settings.Comdirect
                |> Option.bind (fun c -> c.AccountId)
                |> Option.defaultValue ""

            // Fetch transactions
            match! ComdirectAuthSession.fetchTransactions accountId settings.Sync.DaysToFetch with
            | Error comdirectError ->
                let errorMsg = comdirectErrorToString comdirectError
                SyncSessionManager.failSession errorMsg
                return Error (SyncError.TransactionFetchFailed errorMsg)
            | Ok bankTransactions ->
                // Apply rules engine
                let! allRules = Persistence.Rules.getAllRules()
                match classifyTransactions allRules bankTransactions with
                | Error errors ->
                    let errorMsg = String.concat "; " errors
                    SyncSessionManager.failSession errorMsg
                    return Error (SyncError.TransactionFetchFailed errorMsg)
                | Ok syncTransactions ->
                    // Success path
                    SyncSessionManager.addTransactions syncTransactions
                    SyncSessionManager.updateSessionStatus ReviewingTransactions

                    match SyncSessionManager.getCurrentSession() with
                    | Some session ->
                        do! Persistence.SyncSessions.updateSession session
                        return Ok ()
                    | None ->
                        return Error (SyncError.SessionNotFound (let (SyncSessionId id) = sessionId in id))
}
```

**Pattern-Prinzipien**:

1. **Ein Level of Nesting pro Failure-Point**: Jedes `match` startet ein neues Indentation-Level
2. **Error-Handling zuerst**: `| Error err -> return Error err` immer vor `| Ok _`
3. **Side-Effects explizit**: `failSession`, `updateSessionStatus` vor dem `return`
4. **Type-Conversion an den Grenzen**: `comdirectErrorToString` konvertiert zum richtigen Error-Type

**Warum nicht Railway-Oriented Programming?**

ROP wäre eleganter:
```fsharp
validateSession sessionId
|> Result.bind confirmTan
|> Result.bind (fetchTransactions accountId days)
|> Result.bind (classifyTransactions rules)
```

**Aber**: Das funktioniert nur mit **synchronen** Funktionen. Sobald `Async` ins Spiel kommt, braucht man `AsyncResult` – und das ist **nicht** in F# eingebaut.

Für BudgetBuddy habe ich mich für **Pragmatismus** entschieden: Explizites Matching ist:
- Verständlicher für Neulinge
- Klar in der Execution-Reihenfolge
- Funktioniert ohne zusätzliche Dependencies

### Herausforderung 7: ComdirectSettings Type Evolution

#### Das Problem

Der `startAuth` Function-Signature hatte sich geändert:

```fsharp
// Alte Version (aus meiner Annahme):
let startAuth (clientId: string) (clientSecret: string) : Async<ComdirectResult<Challenge>>

// Tatsächliche Version:
let startAuth (credentials: ComdirectSettings) : Async<ComdirectResult<Challenge>>
```

Und `ComdirectSettings` hatte mehr Felder als erwartet:

```fsharp
type ComdirectSettings = {
    ClientId: string
    ClientSecret: string
    Username: string        // Neu!
    Password: string        // Neu!
    AccountId: string option
}
```

#### Optionen, die ich betrachtet habe

1. **Settings einzeln übergeben** (nicht möglich wegen Signature)
   ```fsharp
   startAuth clientId clientSecret username password
   ```
   - Contra: Function-Signature ist schon definiert

2. **Settings aus DB laden und zusammenbauen** (gewählt)
   ```fsharp
   let! settings = settingsApi.getSettings()
   match settings.Comdirect with
   | Some credentials -> startAuth credentials
   ```

3. **Temporäres Objekt mit Placeholders** (Hack)
   - Contra: Führt zu Runtime-Errors

#### Die Lösung: Settings-API als Single Source of Truth

```fsharp
initiateComdirectAuth = fun sessionId -> async {
    match SyncSessionManager.validateSession sessionId with
    | Error err -> return Error err
    | Ok _ ->
        // Get Comdirect credentials from settings
        let! settings = settingsApi.getSettings()

        match settings.Comdirect with
        | None ->
            return Error (SyncError.ComdirectAuthFailed "Comdirect credentials not configured")
        | Some credentials ->
            // Start auth flow with full credentials
            match! ComdirectAuthSession.startAuth credentials with
            | Error comdirectError ->
                let errorMsg = comdirectErrorToString comdirectError
                SyncSessionManager.failSession errorMsg
                return Error (SyncError.ComdirectAuthFailed errorMsg)
            | Ok challenge ->
                return Ok challenge.Id
}
```

**Architekturentscheidung**: Warum `getSettings()` statt einzelne DB-Calls?

1. **Atomicity**: Settings gehören zusammen (ClientId ohne ClientSecret ist nutzlos)
2. **Caching-Potential**: Ein Call statt mehrerer
3. **Type-Safety**: `Option<ComdirectSettings>` macht klar: Entweder alles oder nichts
4. **Validation**: `saveComdirectCredentials` validiert alle Felder zusammen

**Rationale für ComdirectSettings als Record**:

F# Records sind **immutable by default**:
```fsharp
let settings1 = { ClientId = "abc"; ... }
let settings2 = { settings1 with ClientId = "xyz" }  // Copy mit Update
```

Das macht Settings **thread-safe** ohne explizite Locks.

**Update in Validation.fs**:

```fsharp
let validateComdirectSettings (settings: ComdirectSettings) : Result<ComdirectSettings, string list> =
    let errors =
        [
            validateRequired "Client ID" settings.ClientId
            validateRequired "Client Secret" settings.ClientSecret
            validateRequired "Username" settings.Username
            validateRequired "Password" settings.Password
            // AccountId is optional
        ]
        |> List.choose id

    if errors.IsEmpty then Ok settings else Error errors
```

Alle 4 Required-Fields werden gleichzeitig validiert – der User sieht **alle** fehlenden Felder auf einmal.

### Herausforderung 8: Error Type Conversions – Von Specific zu General

#### Das Problem

BudgetBuddy hat **verschiedene Error-Types**:

```fsharp
// Aus Shared/Domain.fs:
type SettingsError = ...
type YnabError = ...
type RulesError = ...
type SyncError = ...
type ComdirectError = ...
```

Aber die API gibt nur `Result<'T, string>` zurück (für Fable.Remoting).

Wie konvertiere ich typed errors zu Strings **ohne Information zu verlieren**?

#### Optionen, die ich betrachtet habe

1. **ToString() überschreiben** (nicht möglich bei DUs)
   - Contra: F# Discriminated Unions haben kein Override

2. **Pattern Matching per Error-Type** (gewählt)
   ```fsharp
   let errorToString (error: YnabError) : string =
       match error with
       | Unauthorized msg -> $"YNAB authorization failed: {msg}"
       | BudgetNotFound id -> $"Budget not found: {id}"
       ...
   ```

3. **Generic ToString mit Reflection** (verliert Kontext)
   - Contra: `sprintf "%A" error` gibt nur Typ-Namen

#### Die Lösung: Dedicated Error-Converter pro Typ

```fsharp
let private ynabErrorToString (error: YnabError) : string =
    match error with
    | YnabError.Unauthorized msg -> $"YNAB authorization failed: {msg}"
    | YnabError.BudgetNotFound budgetId -> $"Budget not found: {budgetId}"
    | YnabError.AccountNotFound accountId -> $"Account not found: {accountId}"
    | YnabError.CategoryNotFound categoryId -> $"Category not found: {categoryId}"
    | YnabError.RateLimitExceeded retryAfter -> $"YNAB rate limit exceeded. Retry after {retryAfter} seconds"
    | YnabError.NetworkError msg -> $"YNAB network error: {msg}"
    | YnabError.InvalidResponse msg -> $"Invalid YNAB response: {msg}"

let private settingsErrorToString (error: SettingsError) : string =
    match error with
    | SettingsError.YnabTokenInvalid msg -> $"Invalid YNAB token: {msg}"
    | SettingsError.YnabConnectionFailed (status, msg) -> $"YNAB connection failed (HTTP {status}): {msg}"
    | SettingsError.ComdirectCredentialsInvalid (field, reason) -> $"Invalid Comdirect credentials ({field}): {reason}"
    | SettingsError.EncryptionFailed msg -> $"Encryption failed: {msg}"
    | SettingsError.DatabaseError (op, msg) -> $"Database error during {op}: {msg}"
```

**Warum separate Funktionen pro Error-Type?**

1. **Type-Safety**: Compiler warnt, wenn ein Case fehlt
2. **Context-Preservation**: Jeder Error-Type kann eigene Metadaten haben
3. **Testability**: Jede Converter-Funktion einzeln testbar
4. **Maintainability**: Error-Type ändert sich → nur eine Funktion anfassen

**Verwendung in der API**:

```fsharp
saveYnabToken = fun token -> async {
    match validateYnabToken token with
    | Error errors -> return Error (SettingsError.YnabTokenInvalid (String.concat "; " errors))
    | Ok validToken ->
        match! YnabClient.getBudgets validToken with
        | Error ynabError ->
            return Error (SettingsError.YnabConnectionFailed (0, ynabErrorToString ynabError))
        | Ok _ ->
            do! Persistence.Settings.setSetting "ynab_token" validToken true
            return Ok ()
}
```

**F#-Lesson für Neulinge**:

Discriminated Unions sind **nicht wie Enums**:

```fsharp
// Enum (C#-Stil):
enum Status { Active = 1, Inactive = 2 }

// Discriminated Union (F#):
type YnabError =
    | Unauthorized of string              // Trägt einen String
    | BudgetNotFound of YnabBudgetId      // Trägt eine BudgetId
    | RateLimitExceeded of int            // Trägt Retry-After-Sekunden
```

DUs können **Daten tragen**. Das macht Pattern-Matching so mächtig:
```fsharp
match error with
| RateLimitExceeded seconds -> $"Retry after {seconds} seconds"  // seconds ist im Scope!
```

## Lessons Learned: Was würde ich anders machen?

### 1. FsToolkit.ErrorHandling von Anfang an

**Problem**: Viel verschachteltes Pattern-Matching in async-Blöcken.

**Lösung**: FsToolkit.ErrorHandling bietet `asyncResult` Computation Expression:

```fsharp
// Aktueller Code (verschachtelt):
match! operation1() with
| Error e -> return Error e
| Ok result1 ->
    match! operation2 result1 with
    | Error e -> return Error e
    | Ok result2 -> return Ok result2

// Mit FsToolkit (flach):
asyncResult {
    let! result1 = operation1()
    let! result2 = operation2 result1
    return result2
}
```

**Warum ich es nicht getan habe**: Wollte keine zusätzliche Dependency für Milestone 6, aber für Production würde ich es hinzufügen.

### 2. Validation-DSL für komplexere Rules

**Problem**: Rule-Validation ist repetitiv:

```fsharp
let errors = [
    validateRequired "Name" request.Name
    validatePattern request.Pattern
    validateRange "Priority" 0 10000 request.Priority
] |> List.choose id
```

**Bessere Lösung**: Applicative Validation mit Custom Operators:

```fsharp
// Mit FsToolkit oder eigener DSL:
let validateRuleCreateRequest =
    validate {
        let! name = required "Name" request.Name
        and! pattern = required "Pattern" request.Pattern
        and! priority = range "Priority" (0, 10000) request.Priority
        return { Name = name; Pattern = pattern; Priority = priority }
    }
```

Das `and!` sammelt alle Fehler parallel – genau was wir wollen.

### 3. Explizite State-Machine für Sync-Flow

**Problem**: Session-Status ist implizit in if-checks:

```fsharp
if session.Status = AwaitingTan then ...
```

**Bessere Lösung**: Discriminated Union als State-Machine:

```fsharp
type SyncState =
    | NotStarted
    | AwaitingBankAuth of SessionId
    | AwaitingTan of SessionId * ChallengeId
    | FetchingTransactions of SessionId
    | ReviewingTransactions of SessionId * SyncTransaction list
    | Importing of SessionId * SyncTransaction list
    | Completed of SessionId * ImportResult
    | Failed of SessionId * string

// Dann type-safe transitions:
let confirmTan (state: SyncState) : Result<SyncState, SyncError> =
    match state with
    | AwaitingTan (sessionId, challengeId) ->
        // OK, erwartet State
        Ok (FetchingTransactions sessionId)
    | otherState ->
        Error (InvalidStateTransition (otherState, AwaitingTan))
```

Der Compiler würde **erzwingen**, dass alle States behandelt werden.

## Fazit: Was wurde erreicht?

### Statistiken

- **3 neue Module**: Validation.fs (117 LOC), SyncSessionManager.fs (180 LOC), Api.fs (802 LOC)
- **29 API Endpoints** implementiert über 4 API-Schnittstellen
- **5 Error-Converter-Funktionen** für typed error handling
- **Build-Status**: ✅ 0 Warnings, 0 Errors
- **Kompilierungszeit**: ~4 Sekunden

### Dateien im Detail

**Neu erstellt**:
- `src/Server/Validation.fs` – 10 Validator-Funktionen mit Fehlerakkumulation
- `src/Server/SyncSessionManager.fs` – 15 Funktionen für Session-Lifecycle
- `src/Server/Api.fs` – 29 Endpoints + 5 Error-Converter + AppApi

**Modifiziert**:
- `src/Server/Server.fsproj` – Compilation-Order angepasst
- `src/Server/Program.fs` – API-Integration

### Architektur-Übersicht

```
┌──────────────────────────────────────────┐
│           Frontend (Elmish)              │
│         (Milestone 7 - TODO)             │
└────────────┬─────────────────────────────┘
             │ Fable.Remoting
             ▼
┌──────────────────────────────────────────┐
│          Api.fs (AppApi)                 │
│  ┌────────────────────────────────────┐  │
│  │ SettingsApi │ YnabApi │ RulesApi   │  │
│  │ SyncApi (10 endpoints)             │  │
│  └────────────────────────────────────┘  │
└────┬───────────┬──────────┬──────────┬───┘
     │           │          │          │
     ▼           ▼          ▼          ▼
┌─────────┐ ┌────────┐ ┌────────┐ ┌──────────┐
│Validation│ │Session │ │Rules   │ │Ynab      │
│         │ │Manager │ │Engine  │ │Client    │
└─────────┘ └────────┘ └────────┘ └──────────┘
     │           │          │          │
     └───────────┴──────────┴──────────┘
                  │
                  ▼
         ┌──────────────┐
         │ Persistence  │
         │  - Settings  │
         │  - Rules     │
         │  - Sessions  │
         └──────────────┘
                  │
                  ▼
            ┌─────────┐
            │ SQLite  │
            └─────────┘
```

### Type-Safety Highlights

**Result-Types überall**:
```fsharp
// Keine Exceptions, nur typsichere Results:
saveYnabToken      : string -> Async<Result<unit, SettingsError>>
getBudgets         : unit -> Async<Result<YnabBudget list, YnabError>>
createRule         : RuleCreateRequest -> Async<Result<Rule, RulesError>>
importToYnab       : SyncSessionId -> Async<Result<int, SyncError>>
```

**Validation mit Fehlerakkumulation**:
```fsharp
validateYnabToken : string -> Result<string, string list>
// Liste von Fehlern, nicht nur einer!
```

**Options für fehlende Werte**:
```fsharp
getCurrentSession : unit -> SyncSession option
// Compiler zwingt zum Handling von None-Case
```

## Key Takeaways für F#-Neulinge

### 1. Result<'T, 'E> ist dein Freund

In C# würdest du Exceptions werfen:
```csharp
if (token == null) throw new ValidationException("Token required");
```

In F# verwendest du Result:
```fsharp
if token = null then Error "Token required" else Ok token
```

**Warum?** Der Compiler **zwingt** dich, Errors zu behandeln. Keine vergessenen try-catch-Blöcke mehr.

### 2. List-Comprehensions für Fehlerakkumulation

Statt 5-mal zu validieren und beim ersten Fehler zu stoppen:

```fsharp
let errors = [
    validateRequired "Name" name
    validateLength "Name" 1 100 name
    validatePattern pattern
] |> List.choose id

if errors.IsEmpty then Ok value else Error errors
```

Das ist **applicative validation**: Alle Fehler gleichzeitig sammeln.

### 3. Type-Driven Development

Definiere **zuerst** die Typen:
```fsharp
type SyncApi = {
    startSync: unit -> Async<Result<SyncSession, SyncError>>
    confirmTan: SyncSessionId -> Async<Result<unit, SyncError>>
    // ...
}
```

Dann implementiere. Der Compiler sagt dir, was fehlt.

**Vorteil**: Refactoring ist sicher. Type ändert sich → Compiler findet alle Stellen.

---

**Nächster Milestone**: Milestone 7 – Frontend Implementation mit Elmish.React und Feliz. Von typsicheren APIs zu typsicheren UIs!
