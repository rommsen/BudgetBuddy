---
layout: post
title: "Frontend Architecture Refactoring: 8 Milestones zur besseren Codequalität"
date: 2025-12-15
author: Claude
tags: [F#, Fable, Elmish, Refactoring, Design System, Architecture]
---

# Frontend Architecture Refactoring: Vom Review zur Implementierung

Was passiert, wenn man sich ehrlich die eigene Codebasis anschaut und fragt: "Ist das wirklich gut strukturiert?" In diesem Blogpost dokumentiere ich ein umfassendes Frontend-Refactoring von BudgetBuddy – einer F#/Fable-Anwendung mit Elmish-Architektur. Über 8 Milestones habe ich die Codequalität systematisch verbessert, ohne die Funktionalität zu ändern.

**Das Ergebnis:** 4158 neue Zeilen, 1978 entfernte Zeilen, 25 geänderte Dateien – und eine deutlich wartbarere Codebasis.

---

## Ausgangslage: Eine funktionierende, aber gewachsene Codebasis

BudgetBuddy ist eine persönliche Finanz-App, die Transaktionen von der Comdirect-Bank mit YNAB synchronisiert. Das Frontend ist in F# mit Fable geschrieben und nutzt die Elmish-Architektur (Model-View-Update). Nach mehreren Feature-Iterationen war die Codebasis funktional, aber es hatten sich einige technische Schulden angesammelt:

- **SyncFlow/View.fs**: Eine 1700+ Zeilen große Datei mit allen UI-Komponenten für den Synchronisations-Flow
- **Rules/Types.fs**: 10 separate Felder für Form-State statt eines gruppierten Records
- **Inline-Styles**: Hero-Buttons mit 17 Zeilen Tailwind-Code direkt in den Views
- **Inkonsistente Fehleranzeigen**: Jede Komponente hatte ihre eigene Error-Darstellung
- **Fehlende Utilities**: Keine Helper-Funktionen für das häufig verwendete `RemoteData`-Pattern
- **Keine Debouncing-Strategie**: Jede Kategorie-Änderung löste sofort einen API-Call aus

Die App funktionierte – aber jede Änderung wurde schwieriger. Zeit für ein systematisches Refactoring.

---

## Der Prozess: Vom Review zum Milestone-Plan

Bevor ich Code anfasste, führte ich ein strukturiertes Frontend Architecture Review durch. Dabei bewertete ich jeden Aspekt der Codebasis:

```
Elmish MVU Pattern:     9/10 ✓
Feliz Usage:            8/10 ✓
Component Structure:    6/10 ⚠ (SyncFlow zu groß)
State Management:       7/10 ⚠ (Rules Form nicht gruppiert)
Design System:          7/10 ⚠ (Lücken bei Error/PageHeader)
Performance:            7/10 ⚠ (keine Debouncing-Strategie)
```

Aus diesem Review entstand ein priorisierter Milestone-Plan mit 8 konkreten Verbesserungen. Die Prioritäten basierten auf zwei Faktoren:
1. **Wartbarkeits-Impact**: Wie sehr blockiert das aktuelle Design zukünftige Änderungen?
2. **Risiko**: Wie wahrscheinlich sind Bugs durch den aktuellen Zustand?

---

## Herausforderung 1: Die 1700-Zeilen-Datei (Milestone 2)

### Das Problem

`SyncFlow/View.fs` war ein Monolith. Alle UI-Komponenten für den Sync-Flow – Status-Anzeigen, Transaktionsliste, Inline-Regel-Formular, Einzelne Transaktionszeilen – lebten in einer Datei. Das machte Navigation schwierig und erhöhte das Risiko von Merge-Konflikten.

### Optionen, die ich betrachtet habe

**1. Komponenten in separate Dateien extrahieren (gewählt)**
- Pro: Klare Zuständigkeiten, einfache Navigation, unabhängige Bearbeitung
- Contra: Mehr Dateien zu verwalten, Abhängigkeiten müssen klar definiert werden

**2. Regions/Comments zur Strukturierung**
- Pro: Keine Datei-Änderungen, schnell umgesetzt
- Contra: Löst das eigentliche Problem nicht, nur kosmetisch

**3. Alles in einem Modul belassen**
- Pro: Kein Refactoring-Aufwand
- Contra: Problem verschärft sich mit jedem Feature

### Die Lösung: Vier fokussierte Module

Ich extrahierte logisch zusammengehörige Komponenten in einen neuen `Views/`-Ordner:

```
src/Client/Components/SyncFlow/
├── Types.fs          (Model, Msg - unverändert)
├── State.fs          (update function - unverändert)
├── View.fs           (~90 Zeilen - nur noch Komposition)
└── Views/
    ├── StatusViews.fs     (~350 Zeilen)
    ├── InlineRuleForm.fs  (~200 Zeilen)
    ├── TransactionRow.fs  (~450 Zeilen)
    └── TransactionList.fs (~310 Zeilen)
```

**Die Abhängigkeitskette war entscheidend** für die Reihenfolge in `Client.fsproj`:

```fsharp
// StatusViews.fs - keine Abhängigkeiten zu anderen Views
let startSyncView (onStartSync: unit -> unit) = ...
let errorView (error: string) (onRetry: unit -> unit) = ...

// InlineRuleForm.fs - verwendet von TransactionRow
let inlineRuleForm (form: InlineRuleFormState) (dispatch: Msg -> unit) = ...

// TransactionRow.fs - verwendet InlineRuleForm
let transactionRow (tx: SyncTransaction) ... = ...

// TransactionList.fs - verwendet TransactionRow
let transactionListView (model: Model) (dispatch: Msg -> unit) = ...

// View.fs - Hauptkomposition, verwendet alle anderen
let view (model: Model) (dispatch: Msg -> unit) =
    match model.CurrentSession with
    | Success (Some session) ->
        match session.Status with
        | Idle -> StatusViews.startSyncView (fun () -> dispatch StartSync)
        | ReviewingTransactions -> TransactionList.transactionListView model dispatch
        // ...
```

**Architekturentscheidung: Warum separate Module statt Nested Modules?**

F# unterstützt Nested Modules, aber ich entschied mich für separate Dateien weil:
1. **IDE-Navigation**: Jede Datei erscheint in der Sidebar
2. **Compilation Order**: F# compiliert Dateien in Reihenfolge – bei separaten Dateien ist die Abhängigkeit explizit
3. **Git History**: Änderungen an `TransactionRow.fs` verschmutzen nicht die History von `StatusViews.fs`

**Ergebnis:** View.fs schrumpfte von 1700+ auf ~90 Zeilen. Die neue Struktur macht klar, wo welche Komponente lebt.

---

## Herausforderung 2: Form State Explosion (Milestone 3)

### Das Problem

Das Rules-Model hatte 10 separate Felder für Form-State:

```fsharp
type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    RuleFormName: string           // Form-Feld
    RuleFormPattern: string        // Form-Feld
    RuleFormPatternType: PatternType  // Form-Feld
    RuleFormTargetField: TargetField  // Form-Feld
    RuleFormCategoryId: YnabCategoryId option  // Form-Feld
    RuleFormPayeeOverride: string  // Form-Feld
    RuleFormEnabled: bool          // Form-Feld
    RuleFormTestInput: string      // Form-Feld
    RuleFormTestResult: string option  // Form-Feld
    RuleSaving: bool               // Form-Feld
    ConfirmingDeleteRuleId: RuleId option
}
```

Das Problem: Form-State und Domain-State waren vermischt. Das "RuleForm"-Prefix musste überall wiederholt werden, und das Zurücksetzen des Formulars erforderte 10 separate Zuweisungen.

### Die Lösung: Dedizierter Record-Typ mit Helper-Modul

```fsharp
type RuleFormState = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId option
    PayeeOverride: string
    Enabled: bool
    TestInput: string
    TestResult: string option
    IsSaving: bool
}

module RuleFormState =
    let empty = {
        Name = ""
        Pattern = ""
        PatternType = Contains
        TargetField = Combined
        CategoryId = None
        PayeeOverride = ""
        Enabled = true
        TestInput = ""
        TestResult = None
        IsSaving = false
    }

    let fromRule (rule: Rule) = {
        Name = rule.Name
        Pattern = rule.Pattern
        PatternType = rule.PatternType
        TargetField = rule.TargetField
        CategoryId = Some rule.CategoryId
        PayeeOverride = rule.PayeeOverride |> Option.defaultValue ""
        Enabled = rule.Enabled
        TestInput = ""
        TestResult = None
        IsSaving = false
    }

type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    Form: RuleFormState  // Alles gebündelt
    ConfirmingDeleteRuleId: RuleId option
}
```

**Warum ein Companion Module?**

Das `RuleFormState`-Modul bietet zwei entscheidende Vorteile:

1. **Initialisierung wird deklarativ:**
```fsharp
// Vorher: 10 Zeilen
{ model with
    RuleFormName = ""; RuleFormPattern = ""; RuleFormPatternType = Contains
    RuleFormTargetField = Combined; RuleFormCategoryId = None; ... }

// Nachher: 1 Zeile
{ model with Form = RuleFormState.empty }
```

2. **Konvertierung von Domain zu Form ist explizit:**
```fsharp
// Vorher: Verstreuter Code
let name = rule.Name
let pattern = rule.Pattern
// ... 8 weitere Zeilen

// Nachher: Ein Funktionsaufruf
{ model with Form = RuleFormState.fromRule rule }
```

**Refactoring in der View:**

```fsharp
// Vorher
Input.text model.RuleFormName (fun v -> dispatch (SetRuleFormName v))

// Nachher
Input.text model.Form.Name (fun v -> dispatch (SetRuleFormName v))
```

Der `model.Form.X`-Zugriff macht sofort klar: "Das ist Form-State, nicht Domain-State."

---

## Herausforderung 3: Inkonsistente Fehleranzeigen (Milestone 4)

### Das Problem

Jede Komponente hatte ihre eigene Art, Fehler anzuzeigen:

```fsharp
// In Settings/View.fs
Html.div [
    prop.className "bg-error/10 border border-error/30 rounded-lg p-4"
    prop.children [
        Html.span [ prop.text error ]
    ]
]

// In Rules/View.fs
Html.div [
    prop.className "alert alert-error"
    prop.text error
]

// In SyncFlow/View.fs
Html.div [
    prop.className "card bg-base-200 p-6"
    prop.children [ ... komplexeres Layout ... ]
]
```

Drei verschiedene Stile für dasselbe Konzept. Keine ARIA-Attribute für Accessibility. Keine einheitliche Retry-Funktionalität.

### Die Lösung: ErrorDisplay Design System Komponente

Ich erstellte `src/Client/DesignSystem/ErrorDisplay.fs` mit mehreren Varianten:

```fsharp
module ErrorDisplay =
    /// Inline error for form validation
    let inline' (message: string) =
        Html.span [
            prop.role "alert"
            prop.className "text-error text-sm"
            prop.text message
        ]

    /// Compact card for inline contexts
    let cardCompact (message: string) (onRetry: (unit -> unit) option) =
        Html.div [
            prop.role "alert"
            prop.className "rounded-xl bg-error/5 border border-error/20 p-4"
            prop.children [
                Html.div [
                    prop.className "flex items-center gap-3"
                    prop.children [
                        Icons.alertCircle Icons.MD Icons.Error
                        Html.span [
                            prop.className "text-sm text-error flex-1"
                            prop.text message
                        ]
                        match onRetry with
                        | Some retry ->
                            Button.ghost "Retry" retry
                        | None -> ()
                    ]
                ]
            ]
        ]

    /// Hero-style error for major operation failures
    let hero (title: string) (message: string) (actionText: string)
             (actionIcon: ReactElement) (onAction: unit -> unit) =
        Html.div [
            prop.role "alert"
            prop.className "text-center py-12 px-6"
            prop.children [
                // Großes Error-Icon mit Glow-Effekt
                Html.div [
                    prop.className "mb-6"
                    prop.children [
                        Html.div [
                            prop.className "inline-flex p-4 rounded-full bg-error/10"
                            prop.children [ Icons.alertCircle Icons.XL Icons.Error ]
                        ]
                    ]
                ]
                Html.h2 [
                    prop.className "text-2xl font-bold text-error mb-2"
                    prop.text title
                ]
                Html.p [
                    prop.className "text-base-content/60 mb-6 max-w-md mx-auto"
                    prop.text message
                ]
                Button.primaryWithIcon actionText actionIcon onAction
            ]
        ]
```

**Design-Entscheidungen:**

1. **`role="alert"`** auf allen Varianten für Screen-Reader-Unterstützung
2. **Neon-Farbpalette** (error = neon-red/pink Gradient) für Konsistenz mit dem Design System
3. **Optionaler Retry-Button** als `(unit -> unit) option` – nicht jeder Fehler ist retry-fähig
4. **Mehrere Größen** für verschiedene Kontexte (inline → card → hero → fullPage)

**Anwendung in den Views:**

```fsharp
// Settings: Einfache Fehlerkarte
| Failure error -> ErrorDisplay.cardCompact error None

// SyncFlow: Hero-Style für kritische Fehler
| Failure error ->
    ErrorDisplay.hero
        "Sync Failed"
        error
        "Try Again"
        (Icons.sync Icons.SM Icons.Primary)
        (fun () -> dispatch StartSync)
```

---

## Herausforderung 4: Hero-Button Inline-Styles (Milestone 5)

### Das Problem

Der Sync-Button auf dem Dashboard hatte 17 Zeilen Inline-Tailwind:

```fsharp
let syncButton (onClick: unit -> unit) =
    Html.button [
        prop.className (String.concat " " [
            "relative px-8 py-4 text-lg font-semibold rounded-xl"
            "bg-gradient-to-r from-neon-orange to-neon-pink"
            "text-white shadow-lg"
            "hover:shadow-neon-orange/50 hover:scale-105"
            "transition-all duration-300 ease-out"
            "before:absolute before:inset-0 before:rounded-xl"
            "before:bg-gradient-to-r before:from-neon-orange before:to-neon-pink"
            "before:blur-xl before:opacity-50 before:-z-10"
        ])
        prop.onClick (fun _ -> onClick())
        prop.children [
            Html.span [
                prop.className "flex items-center gap-3"
                prop.children [
                    Icons.sync Icons.MD Icons.Primary
                    Html.text "Start Sync"
                ]
            ]
        ]
    ]
```

Dieser Glow-Effekt war nicht wiederverwendbar. Wenn ich einen zweiten Hero-Button brauchte, müsste ich alles kopieren.

### Die Lösung: Button.hero Varianten im Design System

Zuerst erweiterte ich `Tokens.fs` um große Glow-Effekte:

```fsharp
module Glows =
    // Existing small glows...

    // Large glows for hero buttons
    let orangeLg = "shadow-[0_0_30px_rgba(255,140,50,0.4)]"
    let orangeHoverLg = "hover:shadow-[0_0_40px_rgba(255,140,50,0.6)]"
    let tealLg = "shadow-[0_0_30px_rgba(0,245,212,0.4)]"
    let tealHoverLg = "hover:shadow-[0_0_40px_rgba(0,245,212,0.6)]"
```

Dann fügte ich `Button.hero` Varianten hinzu:

```fsharp
/// Hero button - large CTA with prominent glow
let hero (text: string) (onClick: unit -> unit) =
    Html.button [
        prop.className (String.concat " " [
            "relative px-8 py-4 text-lg font-semibold rounded-xl"
            "bg-gradient-to-r from-neon-orange to-neon-pink"
            "text-white"
            Tokens.Glows.orangeLg
            Tokens.Glows.orangeHoverLg
            "hover:scale-105 transition-all duration-300"
        ])
        prop.onClick (fun _ -> onClick())
        prop.text text
    ]

/// Hero button with icon before text
let heroWithIcon (text: string) (icon: ReactElement) (onClick: unit -> unit) =
    Html.button [
        prop.className (String.concat " " [
            "relative px-8 py-4 text-lg font-semibold rounded-xl"
            "bg-gradient-to-r from-neon-orange to-neon-pink"
            "text-white flex items-center gap-3"
            Tokens.Glows.orangeLg
            Tokens.Glows.orangeHoverLg
            "hover:scale-105 transition-all duration-300"
        ])
        prop.onClick (fun _ -> onClick())
        prop.children [
            icon
            Html.span [ prop.text text ]
        ]
    ]
```

**Dashboard nach dem Refactoring:**

```fsharp
// Vorher: 17 Zeilen
let syncButton (onClick: unit -> unit) = ...

// Nachher: 1 Zeile
Button.heroWithIcon "Start Sync" (Icons.sync Icons.MD Icons.Primary) onNavigateToSync
```

---

## Herausforderung 5: RemoteData ohne Helper (Milestone 6)

### Das Problem

`RemoteData<'T>` ist ein Discriminated Union für asynchrone Daten:

```fsharp
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string
```

Überall im Code gab es explizite Pattern Matches:

```fsharp
// Ist das Loading?
match model.Data with
| Loading -> true
| _ -> false

// Hole den Wert oder Default
match model.Data with
| Success value -> value
| _ -> []

// Transformiere den Wert
match model.Data with
| Success value -> Success (transform value)
| Loading -> Loading
| NotAsked -> NotAsked
| Failure e -> Failure e
```

Das ist nicht falsch – aber repetitiv und fehleranfällig (man kann leicht einen Case vergessen).

### Die Lösung: RemoteData Modul mit 17 Helper-Funktionen

Ich fügte ein `RemoteData` Modul zu `Types.fs` hinzu:

```fsharp
[<RequireQualifiedAccess>]
module RemoteData =
    /// Transform the success value
    let map (f: 'a -> 'b) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success value -> Success (f value)
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure e -> Failure e

    /// Chain operations that return RemoteData
    let bind (f: 'a -> RemoteData<'b>) (rd: RemoteData<'a>) : RemoteData<'b> =
        match rd with
        | Success value -> f value
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure e -> Failure e

    /// Quick state checks
    let isLoading rd = match rd with Loading -> true | _ -> false
    let isSuccess rd = match rd with Success _ -> true | _ -> false
    let isFailure rd = match rd with Failure _ -> true | _ -> false

    /// Extract value with default
    let withDefault (defaultValue: 'a) (rd: RemoteData<'a>) : 'a =
        match rd with
        | Success value -> value
        | _ -> defaultValue

    /// Convert to Option
    let toOption (rd: RemoteData<'a>) : 'a option =
        match rd with
        | Success value -> Some value
        | _ -> None

    /// Recover from failure
    let recover (value: 'a) (rd: RemoteData<'a>) : RemoteData<'a> =
        match rd with
        | Failure _ -> Success value
        | other -> other

    /// Combine two RemoteData values
    let map2 (f: 'a -> 'b -> 'c) (rd1: RemoteData<'a>) (rd2: RemoteData<'b>) : RemoteData<'c> =
        match rd1, rd2 with
        | Success a, Success b -> Success (f a b)
        | Failure e, _ -> Failure e
        | _, Failure e -> Failure e
        | Loading, _ -> Loading
        | _, Loading -> Loading
        | NotAsked, _ -> NotAsked
        | _, NotAsked -> NotAsked

    // ... weitere Helper (fromResult, fromOption, fold, etc.)
```

**`[<RequireQualifiedAccess>]` war wichtig:**

Ohne das Attribut würde `map` den eingebauten `List.map` überlagern. Mit dem Attribut ist der Zugriff explizit:

```fsharp
// Klar und eindeutig
let transformed = RemoteData.map (fun x -> x + 1) model.Data
let hasData = RemoteData.isSuccess model.Data
let items = RemoteData.withDefault [] model.Data
```

**63 Unit Tests für Korrektheit:**

```fsharp
testList "RemoteData.map" [
    testCase "maps Success value" <| fun () ->
        let result = RemoteData.map (fun x -> x * 2) (Success 5)
        Expect.equal result (Success 10) "Should double the value"

    testCase "preserves Loading" <| fun () ->
        let result = RemoteData.map (fun x -> x * 2) Loading
        Expect.equal result Loading "Should stay Loading"

    testCase "preserves NotAsked" <| fun () ->
        let result = RemoteData.map (fun x -> x * 2) NotAsked
        Expect.equal result NotAsked "Should stay NotAsked"

    testCase "preserves Failure" <| fun () ->
        let result = RemoteData.map (fun x -> x * 2) (Failure "error")
        Expect.equal result (Failure "error") "Should preserve error"
]
```

---

## Herausforderung 6: Seitenheader Duplikation (Milestone 7)

### Das Problem

Jede Seite hatte ihren eigenen Header-Code:

```fsharp
// Settings/View.fs
Html.div [
    prop.className "flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8"
    prop.children [
        Html.div [
            Html.h1 [ prop.className "text-2xl font-bold"; prop.text "Settings" ]
            Html.p [ prop.className "text-base-content/60"; prop.text "Configure..." ]
        ]
        Html.div [ (* action buttons *) ]
    ]
]

// Rules/View.fs - ähnlich, aber mit Gradient-Titel
Html.div [
    prop.className "flex flex-col md:flex-row ..."
    prop.children [
        Html.h1 [
            prop.className "text-2xl font-bold bg-gradient-to-r from-neon-teal to-neon-green bg-clip-text text-transparent"
            prop.text "Categorization Rules"
        ]
        // ...
    ]
]
```

Leichte Variationen überall. Manche mit Gradient, manche ohne. Manche mit Actions, manche ohne.

### Die Lösung: PageHeader Komponente mit TitleStyle

```fsharp
module PageHeader =
    type TitleStyle = Standard | Gradient

    type Props = {
        Title: string
        Subtitle: string option
        Actions: ReactElement list
        TitleStyle: TitleStyle
    }

    let defaultProps = {
        Title = ""
        Subtitle = None
        Actions = []
        TitleStyle = Standard
    }

    let view (props: Props) =
        let titleClass =
            match props.TitleStyle with
            | Standard -> "text-2xl md:text-3xl font-bold text-base-content"
            | Gradient -> "text-2xl md:text-3xl font-bold bg-gradient-to-r from-neon-teal to-neon-green bg-clip-text text-transparent"

        Html.div [
            prop.className "flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-8 animate-fade-in"
            prop.children [
                Html.div [
                    prop.className "space-y-1"
                    prop.children [
                        Html.h1 [ prop.className titleClass; prop.text props.Title ]
                        match props.Subtitle with
                        | Some subtitle ->
                            Html.p [
                                prop.className "text-base-content/60"
                                prop.text subtitle
                            ]
                        | None -> ()
                    ]
                ]
                if not props.Actions.IsEmpty then
                    Html.div [
                        prop.className "flex flex-wrap items-center gap-2"
                        prop.children props.Actions
                    ]
            ]
        ]

    // Convenience functions
    let simple title = view { defaultProps with Title = title }
    let withSubtitle title subtitle = view { defaultProps with Title = title; Subtitle = Some subtitle }
    let gradient title = view { defaultProps with Title = title; TitleStyle = Gradient }
    let gradientWithActions title subtitle actions =
        view { defaultProps with Title = title; Subtitle = subtitle; Actions = actions; TitleStyle = Gradient }
```

**Anwendung:**

```fsharp
// Settings - Standard mit Actions
PageHeader.withActions "Settings" (Some "Configure your connections.") [
    Button.ghost "" (fun () -> dispatch Refresh) // Refresh-Icon
]

// Rules - Gradient mit Actions
PageHeader.gradientWithActions "Categorization Rules" (Some "Automate categorization.") [
    Button.primaryWithIcon "Add Rule" (Icons.plus Icons.SM Icons.Primary) (fun () -> dispatch AddRule)
]

// SyncFlow - Standard mit Actions
PageHeader.withActions "Review Transactions" (Some "Categorize before import.") [
    Button.secondary "Cancel" (fun () -> dispatch CancelSync)
]
```

---

## Herausforderung 7: Kategorie-Änderungen ohne Debouncing (Milestone 8)

### Das Problem

Im SyncFlow kann der User für jede Transaktion eine Kategorie auswählen. Bei 50+ Transaktionen und schnellen Änderungen (z.B. mit Tastatur durch Dropdown navigieren) wurde bei JEDER Änderung sofort ein API-Call ausgelöst.

```fsharp
// Bisheriges Verhalten
| CategorizeTransaction (txId, categoryId) ->
    // ... optimistisches Update ...
    let cmd =
        Cmd.OfAsync.either
            Api.sync.categorizeTransaction  // <- Sofort!
            (session.Id, txId, categoryId, None)
            TransactionCategorized
            (fun ex -> ...)
    { model with SyncTransactions = Success updatedTransactions }, cmd, NoOp
```

Das funktionierte, aber:
- Unnötige Server-Last bei schnellen Änderungen
- Potenzielle Race Conditions (ältere Response überschreibt neuere)
- Verschwendete Bandbreite

### Die Lösung: Version-basiertes Debouncing

Die Herausforderung: Wie implementiert man Debouncing in einer Elmish-Architektur, wo State immutable sein soll und es keine globalen Timer gibt?

**Mein Ansatz: Version-Tracking pro Transaktion**

```fsharp
// Neues Feld im Model
type Model = {
    // ...
    PendingCategoryVersions: Map<TransactionId, int>
}

// Neue Message
type Msg =
    // ...
    | CommitCategoryChange of TransactionId * YnabCategoryId option * int
```

**Debounce-Modul:**

```fsharp
module Debounce =
    let [<Literal>] DefaultDelayMs = 400

    let delayed<'Msg> (delayMs: int) (msg: 'Msg) : Cmd<'Msg> =
        Cmd.OfAsync.perform
            (fun () -> async {
                do! Async.Sleep delayMs
                return ()
            })
            ()
            (fun () -> msg)

    let delayedDefault<'Msg> (msg: 'Msg) : Cmd<'Msg> =
        delayed DefaultDelayMs msg
```

**Der neue Handler:**

```fsharp
| CategorizeTransaction (txId, categoryId) ->
    match model.CurrentSession, model.SyncTransactions with
    | Success (Some _session), Success transactions ->
        // 1. Optimistisches UI-Update (sofort)
        let updatedTransactions =
            transactions |> List.map (fun tx ->
                if tx.Transaction.Id = txId then
                    { tx with CategoryId = categoryId; ... }
                else tx)

        // 2. Version erhöhen
        let currentVersion =
            model.PendingCategoryVersions
            |> Map.tryFind txId
            |> Option.defaultValue 0
        let newVersion = currentVersion + 1
        let newPendingVersions =
            model.PendingCategoryVersions |> Map.add txId newVersion

        // 3. Verzögerter Command mit Version
        let debouncedCmd =
            Debounce.delayedDefault (CommitCategoryChange (txId, categoryId, newVersion))

        { model with
            SyncTransactions = Success updatedTransactions
            PendingCategoryVersions = newPendingVersions }, debouncedCmd, NoOp

| CommitCategoryChange (txId, categoryId, version) ->
    // Nur ausführen wenn Version noch aktuell
    let currentVersion =
        model.PendingCategoryVersions
        |> Map.tryFind txId
        |> Option.defaultValue 0

    if version <> currentVersion then
        // Veraltete Änderung - neuere ist unterwegs
        model, Cmd.none, NoOp
    else
        // Version aktuell - API-Call ausführen
        match model.CurrentSession with
        | Success (Some session) ->
            let cmd =
                Cmd.OfAsync.either
                    Api.sync.categorizeTransaction
                    (session.Id, txId, categoryId, None)
                    TransactionCategorized
                    (fun ex -> ...)
            let newPendingVersions =
                model.PendingCategoryVersions |> Map.remove txId
            { model with PendingCategoryVersions = newPendingVersions }, cmd, NoOp
        | _ -> model, Cmd.none, NoOp
```

**Visualisierung des Pending-Status:**

```fsharp
// In TransactionRow.fs
let pendingSaveIndicator =
    if isPendingSave then
        Html.span [
            prop.className "ml-2 text-xs text-neon-orange animate-pulse"
            prop.title "Saving category..."
            prop.text "●"
        ]
    else
        Html.none
```

**Warum dieser Ansatz?**

1. **Keine globalen Timer**: Alles ist im Elmish-State und Commands
2. **Race-Condition-sicher**: Version-Check garantiert, dass nur die neueste Änderung durchgeht
3. **Testbar**: Reiner funktionaler Code
4. **Visuelles Feedback**: User sieht, dass Änderung pending ist

---

## Lessons Learned

### 1. Code-Review vor Refactoring lohnt sich

Das initiale Review mit Bewertungen (6/10, 7/10, etc.) half, Prioritäten zu setzen. Nicht alles auf einmal angehen – die wichtigsten Pain Points zuerst.

### 2. Keine funktionalen Änderungen während Refactoring

Jeder Milestone war ein reines Refactoring ohne Feature-Änderungen. Das machte Reviews einfacher und reduzierte das Risiko.

### 3. Tests als Sicherheitsnetz

Die 357 existierenden Tests gaben Sicherheit. Nach jedem Milestone: `dotnet test` – wenn grün, war das Refactoring korrekt.

### 4. F#'s Typsystem ist dein Freund

Beim Form-State-Refactoring brach der Compiler überall dort, wo ich `model.RuleFormName` statt `model.Form.Name` verwendete. Der Compiler führte mich durch alle nötigen Änderungen.

### 5. Design System Components zahlen sich aus

Die Investition in `ErrorDisplay`, `Button.hero`, `PageHeader` macht zukünftige Entwicklung schneller. Eine neue Seite? `PageHeader.withSubtitle` und fertig.

---

## Fazit: Die Zahlen

| Metrik | Vorher | Nachher |
|--------|--------|---------|
| SyncFlow/View.fs | 1700+ Zeilen | 90 Zeilen |
| Rules Model Felder | 14 Felder | 5 + Form-Record |
| Design System Komponenten | 14 | 17 (+ErrorDisplay, Button.hero, PageHeader) |
| RemoteData Helper | 0 | 17 Funktionen |
| Tests | 294 | 357 (+63 RemoteData Tests) |
| Gesamte Änderungen | | +4158 / -1978 Zeilen |

**Alle 8 Milestones abgeschlossen:**
1. ✅ React Key Props
2. ✅ SyncFlow Modularisierung
3. ✅ Rules Form State Konsolidierung
4. ✅ ErrorDisplay Design System
5. ✅ Button.hero Design System
6. ✅ RemoteData Helper Module
7. ✅ PageHeader Design System
8. ✅ Category Selection Debouncing

Die Codebasis ist jetzt wartbarer, konsistenter und besser strukturiert – ohne dass sich für den User irgendetwas geändert hat. Das ist gutes Refactoring.

---

## Key Takeaways für Neulinge

1. **Strukturiertes Review zuerst**: Bevor du refactorst, mach eine Bestandsaufnahme. Was sind die echten Probleme? Priorisiere nach Impact.

2. **Ein Milestone, ein Fokus**: Mische nicht "neue Feature" mit "Refactoring". Halte Refactoring-PRs rein strukturell – das macht Reviews einfacher.

3. **Design System Components sparen Zeit**: Die Investition in wiederverwendbare Komponenten zahlt sich schnell aus. Eine Stunde für `ErrorDisplay` spart Stunden bei zukünftigen Features.

---

*Dieser Blogpost dokumentiert die Arbeit an BudgetBuddy, einer persönlichen Finanz-App in F#/Fable mit Elmish-Architektur.*
