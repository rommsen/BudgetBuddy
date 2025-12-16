---
layout: post
title: "BudgetBuddy Architektur-Guide: Ein F# Full-Stack Deep Dive"
date: 2025-12-16
author: Claude
tags: [architektur, f#, fsharp, full-stack, elmish, giraffe, fable-remoting]
---

# BudgetBuddy Architektur-Guide: Ein F# Full-Stack Deep Dive

**Für wen ist dieser Guide?** Erfahrene F#-Entwickler, die verstehen wollen, wie ein Full-Stack F# Projekt strukturiert ist, und Web-Entwickler aus anderen Sprachen (React, Redux, Clean Architecture), die die F#-Äquivalente kennenlernen möchten.

## Einleitung: Warum F# Full-Stack?

BudgetBuddy ist eine Self-Hosted Web-App, die Banktransaktionen von Comdirect mit YNAB (You Need A Budget) synchronisiert. Die zentrale Architektur-Entscheidung war: **F# durchgehend** – vom Frontend bis zum Backend.

Warum? Drei Gründe:

1. **Geteilte Typen**: Domain-Typen werden einmal definiert und in beiden Projekten verwendet. Keine Drift, keine "Contract Tests", keine JSON-Schema-Synchronisation.

2. **Type-Safe RPC**: Fable.Remoting ersetzt REST APIs durch typisierte Funktionsaufrufe. Änderst du eine API-Signatur, bricht der Build – nicht erst der Production-User.

3. **Konsistentes Mental Model**: Pattern Matching, Discriminated Unions, und Immutability überall. Kein Context-Switch zwischen TypeScript und C#.

---

## Teil 1: Die 4-Projekt-Struktur

```
src/
├── Shared/     → Geteilte Typen (Domain.fs, Api.fs)
├── Server/     → Backend (Giraffe + Fable.Remoting)
├── Client/     → Frontend (Elmish + Feliz)
└── Tests/      → Expecto Tests
```

### Warum diese Aufteilung?

**Shared** ist der Schlüssel. Jeder Typ, der über die Netzwerkgrenze geht, lebt hier:

```fsharp
// src/Shared/Domain.fs
type TransactionStatus =
    | Pending
    | AutoCategorized
    | ManualCategorized
    | NeedsAttention
    | Imported
    | Skipped
```

Dieser `TransactionStatus` wird:
- Im Backend gespeichert und verarbeitet
- Im Frontend angezeigt und geändert
- Über die API hin- und hergeschickt

**Eine Definition, null Drift.**

### Vergleich zu anderen Stacks

| BudgetBuddy (F#) | React + Node | Angular + .NET |
|------------------|--------------|----------------|
| Shared Domain Types | OpenAPI + Codegen | OpenAPI + NSwag |
| Compile-time API | Runtime JSON | Runtime JSON |
| 1 Sprache | 2 Sprachen | 2 Sprachen |

---

## Teil 2: Shared Types & API Contracts

### Domain.fs – Das Herzstück

Die wichtigste Datei im Projekt. Hier werden alle Domain-Konzepte modelliert:

```fsharp
// Typisierte IDs verhindern "String-Soup"
type TransactionId = TransactionId of Guid
type RuleId = RuleId of Guid
type YnabCategoryId = YnabCategoryId of Guid

// Discriminated Unions für endliche Zustände
type PatternType =
    | Contains    // Substring-Match
    | Exact       // Exakter Match
    | Regex       // Regulärer Ausdruck

// Records für strukturierte Daten
type Rule = {
    Id: RuleId
    Name: string
    Pattern: string
    PatternType: PatternType
    CategoryId: YnabCategoryId option
    CategoryName: string option
    TargetField: TargetField
    PayeeOverride: string option
    Priority: int
    Enabled: bool
    CreatedAt: DateTime
    UpdatedAt: DateTime
}
```

**Architektur-Entscheidung: Warum typisierte IDs?**

Statt `Guid` überall zu verwenden, haben wir `TransactionId`, `RuleId`, etc. Das verhindert:

```fsharp
// ❌ Kompiliert, aber semantisch falsch
let deleteRule (transactionId: Guid) = ...
deleteRule someTransaction.Id  // Oops, Transaction statt Rule!

// ✅ Kompiliert nicht
let deleteRule (ruleId: RuleId) = ...
deleteRule someTransaction.Id  // Compiler-Error!
```

**Kosten**: Etwas mehr Boilerplate. **Nutzen**: Unmöglichkeit einer ganzen Fehlerklasse.

### Api.fs – Die Verträge

```fsharp
// src/Shared/Api.fs
type SettingsApi = {
    getSettings: unit -> Async<AppSettings>
    saveYnabToken: string -> Async<SettingsResult<unit>>
    testYnabConnection: unit -> Async<SettingsResult<YnabBudgetWithAccounts list>>
    // ...
}

type SyncApi = {
    startSync: unit -> Async<SyncResult<SyncSession>>
    categorizeTransaction: TransactionId * YnabCategoryId option * string option -> Async<SyncResult<unit>>
    importToYnab: SyncSessionId -> Async<SyncResult<ImportResult>>
    // ...
}
```

**Das ist der API-Vertrag.** Nicht eine Swagger-Datei, nicht eine Markdown-Dokumentation – sondern F#-Typen, die der Compiler prüft.

**Für React-Entwickler**: Stell dir vor, dein Backend wäre eine TypeScript-Bibliothek mit perfekten Typen. Kein `any`, kein `unknown`, kein `as`.

### Result-Typen für Fehlerbehandlung

Jeder API-Endpunkt verwendet typisierte Fehler:

```fsharp
type SettingsResult<'T> = Result<'T, SettingsError>

type SettingsError =
    | YnabTokenInvalid of string
    | YnabConnectionFailed of int * string
    | ComdirectCredentialsInvalid of string * string
    | EncryptionFailed of string
    | DatabaseError of string * string
```

**Warum nicht Exceptions?** Exceptions sind "invisible return types". Mit `Result<'T, SettingsError>` ist klar:
1. Diese Funktion kann fehlschlagen
2. Diese spezifischen Fehler können auftreten
3. Der Aufrufer **muss** beide Fälle behandeln

---

## Teil 3: Backend-Architektur

### Die Schichten

```
Request → Api.fs → Domain.fs → Persistence.fs → SQLite
            ↓           ↓
       Validation    Pure Logic
```

### Api.fs – Der Eintrittspunkt

```fsharp
// src/Server/Api.fs
let settingsApi : SettingsApi = {
    saveYnabToken = fun token -> async {
        // 1. Validierung
        match validateYnabToken token with
        | Error errors -> return Error (SettingsError.YnabTokenInvalid (String.concat "; " errors))
        | Ok validToken ->
            // 2. Externer API-Call (Test ob Token gültig ist)
            match! YnabClient.getBudgets validToken with
            | Error ynabError ->
                return Error (SettingsError.YnabConnectionFailed (0, ynabErrorToString ynabError))
            | Ok _ ->
                // 3. Persistierung
                try
                    do! Persistence.Settings.setSetting "ynab_token" validToken true
                    return Ok ()
                with ex ->
                    return Error (SettingsError.DatabaseError ("save_ynab_token", ex.Message))
    }
    // ...
}
```

**Pattern**: Validierung → Business Logic → Side Effects

### Domain.fs – Pure Funktionen

```fsharp
// src/Server/Domain.fs
// KEINE I/O-Operationen hier!

let applyRule (rule: Rule) (transaction: BankTransaction) : bool =
    let fieldValue =
        match rule.TargetField with
        | Payee -> transaction.Payee
        | Memo -> transaction.Memo |> Option.defaultValue ""
        | Combined -> $"{transaction.Payee} {transaction.Memo |> Option.defaultValue ""}"

    match rule.PatternType with
    | Contains -> fieldValue.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)
    | Exact -> String.Equals(fieldValue, rule.Pattern, StringComparison.OrdinalIgnoreCase)
    | Regex -> Regex.IsMatch(fieldValue, rule.Pattern, RegexOptions.IgnoreCase)
```

**Architektur-Entscheidung: Warum "Pure Domain"?**

1. **Testbarkeit**: Keine Mocks nötig. Input → Output, fertig.
2. **Reasoning**: Keine versteckten Abhängigkeiten, keine Seiteneffekte.
3. **Parallelisierung**: Pure Funktionen sind thread-safe by design.

**Für Clean-Architecture-Kenner**: Das entspricht dem "Use Case Layer" oder "Application Layer" – aber funktional statt OOP.

### Persistence.fs – Die Datenbank-Grenze

```fsharp
// src/Server/Persistence.fs
module Settings =
    let getSetting (key: string) : Async<string option> = async {
        use! conn = getConnection()
        let! result = conn.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM settings WHERE key = @Key",
            {| Key = key |})
        return Option.ofObj result
    }

    let setSetting (key: string) (value: string) (encrypt: bool) : Async<unit> = async {
        let finalValue = if encrypt then Encryption.encrypt value else value
        use! conn = getConnection()
        do! conn.ExecuteAsync(
            "INSERT OR REPLACE INTO settings (key, value, is_encrypted) VALUES (@Key, @Value, @IsEncrypted)",
            {| Key = key; Value = finalValue; IsEncrypted = encrypt |}) |> Async.AwaitTask |> Async.Ignore
    }
```

**Warum Dapper statt Entity Framework?**

1. **Explizite Queries**: Keine Magie, keine N+1-Überraschungen
2. **F#-freundlich**: Funktioniert gut mit Records und Option-Types
3. **Leichtgewicht**: Kein Change-Tracking, keine Migrations-Komplexität

### Fable.Remoting – Die Magie

```fsharp
// src/Server/Api.fs
let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder routeBuilder
    |> Remoting.fromValue settingsApi
    |> Remoting.fromValue syncApi
    |> Remoting.fromValue rulesApi
    |> Remoting.fromValue ynabApi
    |> Remoting.buildHttpHandler
```

Das generiert automatisch HTTP-Endpunkte für alle API-Funktionen. Der Client ruft sie so auf:

```fsharp
// src/Client/Api.fs
let settings = Remoting.createApi() |> Remoting.buildProxy<SettingsApi>

// Aufruf (irgendwo im Client)
let! result = Api.settings.saveYnabToken token
```

**Kein REST, keine URLs, keine JSON-Serialisierung** – alles automatisch.

---

## Teil 4: Frontend-Architektur (MVU/Elmish)

### Das MVU-Pattern

MVU (Model-View-Update) ist das funktionale Äquivalent zu Redux:

| Redux | Elmish/MVU |
|-------|------------|
| State | Model |
| Action | Msg |
| Reducer | update |
| mapStateToProps | view |

```fsharp
// src/Client/State.fs
type Model = {
    CurrentPage: Page
    Dashboard: DashboardModel
    Settings: SettingsModel
    SyncFlow: SyncFlowModel
    Rules: RulesModel
    Toasts: Toast list
}

type Msg =
    | NavigateTo of Page
    | UrlChanged of string list
    | ShowToast of string * ToastType
    | DismissToast of Guid
    | DashboardMsg of DashboardMsg
    | SettingsMsg of SettingsMsg
    | SyncFlowMsg of SyncFlowMsg
    | RulesMsg of RulesMsg
```

### Die Update-Funktion

```fsharp
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | NavigateTo page ->
        let segments = Routing.toUrlSegments page
        model, Cmd.navigate(segments |> List.toArray)

    | UrlChanged segments ->
        let page = Routing.parseUrl segments
        if page = model.CurrentPage then
            model, Cmd.none
        else
            let extraCmds =
                match page with
                | Dashboard -> Cmd.map DashboardMsg (Cmd.ofMsg LoadLastSession)
                | SyncFlow -> Cmd.map SyncFlowMsg (Cmd.ofMsg LoadCurrentSession)
                | Rules -> Cmd.map RulesMsg (Cmd.ofMsg LoadRules)
                | Settings -> Cmd.map SettingsMsg (Cmd.ofMsg LoadSettings)
            { model with CurrentPage = page }, extraCmds

    | DashboardMsg dashboardMsg ->
        let model', cmd = Dashboard.State.update dashboardMsg model.Dashboard
        { model with Dashboard = model' }, Cmd.map DashboardMsg cmd
    // ...
```

**Architektur-Entscheidung: Component-basierte Verschachtelung**

Jede "Page" hat eigene `Types.fs`, `State.fs`, `View.fs`:

```
Components/
├── Dashboard/
│   ├── Types.fs    → DashboardModel, DashboardMsg
│   ├── State.fs    → init, update
│   └── View.fs     → view
├── SyncFlow/
│   └── ...
```

Die Haupt-`State.fs` delegiert an die Komponenten und handled nur Cross-Cutting-Concerns (Toasts, Navigation).

### RemoteData – Async State Management

```fsharp
// src/Client/Types.fs
type RemoteData<'T> =
    | NotAsked     // Noch nicht geladen
    | Loading      // Lädt gerade
    | Success of 'T
    | Failure of string
```

**Warum nicht einfach `'T option`?**

`option` unterscheidet nicht zwischen "noch nicht geladen" und "geladen, aber leer". `RemoteData` macht alle vier Zustände explizit:

```fsharp
match model.Transactions with
| NotAsked -> Html.text "Klicke auf Laden"
| Loading -> Loading.spinner MD Teal
| Success [] -> Html.text "Keine Transaktionen"
| Success txns -> TransactionList.view txns dispatch
| Failure err -> ErrorDisplay.card err (Some retry)
```

**Für Redux-Entwickler**: Das ist wie `{ loading: boolean, error: string | null, data: T | null }` – aber ohne die Möglichkeit inkonsistenter Zustände (loading=true UND error!=null).

### Das Helper-Modul

```fsharp
module RemoteData =
    let map (f: 'a -> 'b) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success x -> Success (f x)
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    let isLoading rd = match rd with Loading -> true | _ -> false
    let withDefault defaultValue rd = match rd with Success x -> x | _ -> defaultValue
    let fromResult result = match result with Ok x -> Success x | Error e -> Failure e
```

### Das DesignSystem

```
DesignSystem/
├── Tokens.fs      → Farben, Abstände, Fonts
├── Primitives.fs  → Container, Grid, Stack
├── Button.fs      → Button-Varianten
├── Card.fs        → Card-Layouts
├── Input.fs       → Form-Inputs
├── Modal.fs       → Dialog-Komponenten
├── ErrorDisplay.fs → Fehler-Darstellung
└── ...
```

**Architektur-Entscheidung: Warum ein eigenes Design System?**

1. **Konsistenz**: Alle Buttons sehen gleich aus, alle Errors werden gleich angezeigt
2. **Wiederverwendung**: `Button.primary "Speichern" onClick` statt 20 Zeilen Tailwind
3. **Typsicherheit**: Props sind F#-Records, nicht String-Attributes

```fsharp
// Statt:
Html.button [
    prop.className "btn btn-primary bg-orange-500 hover:bg-orange-600 ..."
    prop.onClick (fun _ -> dispatch Save)
    prop.text "Speichern"
]

// Schreibt man:
Button.primary "Speichern" (fun () -> dispatch Save)
```

---

## Teil 5: Wie hängt alles zusammen?

### Der Datenfluss eines API-Calls

```
1. User klickt "Speichern" im Frontend
   └→ dispatch (SettingsMsg (SaveYnabToken token))

2. State.fs update-Funktion
   └→ Cmd.OfAsync.either Api.settings.saveYnabToken token ...

3. Fable.Remoting serialisiert und sendet HTTP-Request

4. Server Api.fs empfängt
   └→ validateYnabToken token
   └→ YnabClient.getBudgets token
   └→ Persistence.Settings.setSetting ...
   └→ return Ok ()

5. Fable.Remoting sendet Response zurück

6. Client empfängt Result
   └→ dispatch (SettingsMsg (YnabTokenSaved (Ok ())))

7. State.fs update-Funktion
   └→ { model with Settings = { model.Settings with SaveStatus = Success () } }

8. View rendert neuen Zustand
   └→ "Token gespeichert!" Toast
```

### Wo fängt man an, um X zu verstehen?

| Ich will verstehen... | Starte hier |
|----------------------|-------------|
| Welche Daten gibt es? | `src/Shared/Domain.fs` |
| Welche API-Endpunkte? | `src/Shared/Api.fs` |
| Wie funktioniert Feature X? | `src/Client/Components/X/State.fs` |
| Wie wird Y gespeichert? | `src/Server/Persistence.fs` |
| Wie sieht UI-Element Z aus? | `src/Client/DesignSystem/Z.fs` |

---

## Lessons Learned

### Was ich anders machen würde

1. **Früher ein Design System**: Die ersten UI-Komponenten waren inline Tailwind. Die Migration zu DesignSystem-Komponenten hat Zeit gekostet.

2. **Mehr Property-Based Tests**: Unit-Tests sind gut, aber FsCheck hätte Edge-Cases früher gefunden.

3. **Striktere Trennung von Queries/Commands**: Manche API-Funktionen machen beides. CQRS-Trennung wäre sauberer.

### Was gut funktioniert hat

1. **Shared Types von Anfang an**: Nie "Contract Drift" gehabt.

2. **Result-Types statt Exceptions**: Fehlerbehandlung ist explizit und vollständig.

3. **MVU für komplexe Flows**: Der Sync-Wizard mit 7 Zuständen wäre mit imperativem Code ein Albtraum.

---

## Key Takeaways

1. **Shared Types sind der größte Gewinn von F# Full-Stack**: Eine Typdefinition, zwei Projekte, null Synchronisations-Aufwand.

2. **MVU/Elmish ist Redux done right**: Gleiche Ideen, aber mit Compiler-Support statt Runtime-Checks.

3. **Pure Domain Logic zahlt sich aus**: Leicht zu testen, leicht zu verstehen, leicht zu ändern.

4. **Fable.Remoting eliminiert eine ganze Fehlerklasse**: Keine falschen URLs, keine JSON-Parsing-Errors, keine vergessenen Endpunkte.

---

*Dieser Guide wurde geschrieben, um Entwicklern den Einstieg in die BudgetBuddy-Codebase zu erleichtern. Bei Fragen: Issues auf GitHub sind willkommen!*
