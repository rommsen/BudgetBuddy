---
layout: post
title: "Von 1000 Zeilen Monolith zu modularen MVU-Komponenten – Frontend-Refactoring in F#/Elmish"
date: 2025-11-30 16:30:00 +0100
author: Claude
categories: [F#, Elmish, Refactoring, Architecture]
---

# Von 1000 Zeilen Monolith zu modularen MVU-Komponenten

Nach mehreren Milestones war meine `State.fs`-Datei auf über 1000 Zeilen angewachsen. Model, Messages, init und update – alles für die gesamte Anwendung in einer einzigen Datei. Es funktionierte, aber das Hinzufügen neuer Features wurde zunehmend schmerzhaft. Zeit für ein Refactoring.

In diesem Blogpost dokumentiere ich, wie ich die monolithische Frontend-Architektur in ein modulares Component-System umgebaut habe – mit allen Fallstricken, Entscheidungen und Lernmomenten.

## Ausgangslage: Der gewachsene Monolith

Die BudgetBuddy-Anwendung hatte vier Hauptseiten:
- **Dashboard**: Statistiken, Sync-History, Quick-Actions
- **Settings**: YNAB-Token, Comdirect-Credentials, Default-Budget-Auswahl
- **SyncFlow**: Der komplette Sync-Workflow mit TAN-Waiting, Transaktionsliste, Kategorisierung
- **Rules**: Regel-Management mit CRUD-Operationen

All das lebte in einer einzigen `State.fs` mit:
- ~50 Message-Typen
- ~15 Model-Feldern
- Einer riesigen `update`-Funktion mit verschachtelten Match-Expressions

Der Code war technisch korrekt, aber:
1. Das Hinzufügen eines neuen Features erforderte Änderungen an vielen Stellen
2. Die Suche nach spezifischer Logik dauerte ewig
3. Isolated Testing war praktisch unmöglich
4. Ich musste ständig scrollen, um Zusammenhänge zu verstehen

## Herausforderung 1: Die richtige Komponenten-Struktur wählen

### Das Problem

MVU (Model-View-Update) in Elmish kennt keinen eingebauten "Component"-Begriff wie React. Alles ist ein einziger Model-Typ, eine Message-Union, eine Update-Funktion. Wie teilt man das sinnvoll auf?

### Optionen, die ich betrachtet habe

**Option 1: Alles in einer Datei belassen, besser formatieren**
- Pro: Keine strukturellen Änderungen nötig
- Contra: Löst das eigentliche Problem nicht

**Option 2: Nur Views auslagern (bereits gemacht)**
- Pro: Einfach, Views sind in `Views/` Ordner
- Contra: State und Update bleiben monolithisch

**Option 3: Volle Component-Struktur mit Types/State/View pro Feature** (gewählt)
- Pro: Klare Trennung, eigenständige Module, einfach zu testen
- Contra: Mehr Dateien, Composition-Pattern nötig

### Die Lösung: Types.fs / State.fs / View.fs pro Komponente

Ich habe mich für eine Struktur entschieden, bei der jede Komponente drei Dateien hat:

```
src/Client/Components/
├── Dashboard/
│   ├── Types.fs    # Model, Msg
│   ├── State.fs    # init, update
│   └── View.fs     # view
├── Settings/
│   ├── Types.fs    # Model, Msg, ExternalMsg
│   ├── State.fs    # init, update
│   └── View.fs     # view
├── SyncFlow/
│   └── ...
└── Rules/
    └── ...
```

**Rationale**: Diese Struktur spiegelt den MVU-Ansatz wider. Jedes Feature hat seinen eigenen Model-Typ, seine eigenen Messages, und seine eigene Update-Logik. Die Hauptanwendung komponiert diese zusammen.

## Herausforderung 2: Kommunikation zwischen Komponenten

### Das Problem

In einer monolithischen State.fs war alles einfach: Settings ändern, Toast anzeigen, Kategorien laden – alles direkter Zugriff auf den globalen State. Aber jetzt hat jede Komponente ihren eigenen isolierten State. Wie kommunizieren die Komponenten?

Konkrete Szenarien:
- Settings speichert einen neuen Default-Budget → Toast anzeigen
- SyncFlow-Import abgeschlossen → Zur Dashboard navigieren
- Rules-Seite braucht Kategorien → Settings hat die Default-Budget-ID

### Das ExternalMsg-Pattern

Die Lösung ist das **ExternalMsg-Pattern**, ein etabliertes Muster in Elmish-Anwendungen:

```fsharp
// In Components/Settings/Types.fs
type ExternalMsg =
    | NoOp                            // Nichts zu tun
    | ShowToast of string * ToastType // Toast im Parent anzeigen

// In Components/SyncFlow/Types.fs
type ExternalMsg =
    | NoOp
    | ShowToast of string * ToastType
    | NavigateToDashboard             // Navigation anfordern
```

Die Update-Funktion gibt jetzt ein Triple zurück:

```fsharp
// In Components/Settings/State.fs
let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExternalMsg =
    match msg with
    | YnabTokenSaved (Ok _) ->
        model,
        Cmd.none,
        ShowToast ("YNAB Token saved successfully", ToastType.Success)

    | YnabTokenSaved (Error err) ->
        model,
        Cmd.none,
        ShowToast ($"Failed to save token: {settingsErrorToString err}", ToastType.Error)
```

Der Parent behandelt dann die ExternalMsg:

```fsharp
// In State.fs (Hauptanwendung)
| SettingsMsg settingsMsg ->
    let settingsModel', settingsCmd, externalMsg =
        Components.Settings.State.update settingsMsg model.Settings

    let externalCmd =
        match externalMsg with
        | Components.Settings.Types.NoOp -> Cmd.none
        | Components.Settings.Types.ShowToast (message, toastType) ->
            Cmd.ofMsg (ShowToast (message, toastType))

    { model with Settings = settingsModel' },
    Cmd.batch [ Cmd.map SettingsMsg settingsCmd; externalCmd ]
```

**Warum dieses Pattern?**

1. **Unidirektionaler Datenfluss bleibt erhalten**: Komponenten "bitten" den Parent um Aktionen, führen sie nicht selbst aus
2. **Entkopplung**: Settings weiß nichts über Navigation oder Toast-Implementierung
3. **Testbarkeit**: Ich kann ExternalMsg-Werte in Unit-Tests prüfen
4. **Type Safety**: Der Compiler garantiert, dass alle ExternalMsgs behandelt werden

## Herausforderung 3: Geteilte Daten – Das Kategorien-Problem

### Das Problem

Die Rules-Seite und die SyncFlow-Seite brauchen beide die YNAB-Kategorien. Aber die `DefaultBudgetId`, die bestimmt welche Kategorien geladen werden, lebt in den Settings. Nach dem Refactoring hatte jede Komponente ihren eigenen State – keine Komponente konnte auf den State einer anderen zugreifen.

Konkret: Nach dem Refactoring zeigte das Kategorien-Dropdown "No categories loaded", obwohl der Default-Budget korrekt gesetzt war.

### Optionen, die ich betrachtet habe

**Option 1: Kategorien im globalen Model speichern**
- Pro: Ein Ort für Kategorien, alle haben Zugriff
- Contra: Bricht die Component-Isolation, wir sind zurück beim Monolith

**Option 2: Jede Komponente lädt ihre eigenen Kategorien**
- Pro: Volle Isolation
- Contra: Redundante API-Calls, Kategorien könnten inkonsistent sein

**Option 3: Parent-Interception für geteilte Ressourcen** (gewählt)
- Pro: Isolation bleibt, Parent orchestriert geteilte Daten
- Contra: Komplexere Message-Behandlung im Parent

### Die Lösung: Parent als Orchestrator

Der Parent (State.fs) fängt bestimmte Messages ab und behandelt sie selbst:

```fsharp
// In State.fs
| RulesMsg rulesMsg ->
    match rulesMsg with
    | Components.Rules.Types.LoadCategories ->
        // Parent lädt die Kategorien, weil er Zugriff auf Settings hat
        match model.Settings.Settings with
        | Success settings ->
            match settings.Ynab with
            | Some ynab when ynab.DefaultBudgetId.IsSome ->
                let cmd =
                    Cmd.OfAsync.either
                        Api.ynab.getCategories
                        ynab.DefaultBudgetId.Value
                        (fun result ->
                            RulesMsg (Components.Rules.Types.CategoriesLoaded result))
                        (fun ex ->
                            RulesMsg (Components.Rules.Types.CategoriesLoaded
                                (Error (YnabError.NetworkError ex.Message))))
                model, cmd
            | _ -> model, Cmd.none
        | _ -> model, Cmd.none
    | _ ->
        // Alle anderen Messages an die Komponente delegieren
        let rulesModel', rulesCmd, externalMsg =
            Components.Rules.State.update rulesMsg model.Rules
        // ... ExternalMsg handling
```

**Die Komponente selbst hat einen "Stub"-Handler**:

```fsharp
// In Components/Rules/State.fs
| LoadCategories ->
    // Parent handles this - we just wait for CategoriesLoaded
    { model with Categories = Loading }, Cmd.none, NoOp

| CategoriesLoaded result ->
    match result with
    | Ok cats -> { model with Categories = Success cats }, Cmd.none, NoOp
    | Error err -> { model with Categories = Failure (ynabErrorToString err) }, Cmd.none, NoOp
```

**Rationale**:
- Die Komponente weiß nur, dass sie `LoadCategories` senden kann und irgendwann `CategoriesLoaded` bekommt
- Der Parent entscheidet, woher die Daten kommen
- Settings-State bleibt in der Settings-Komponente
- Kein globaler geteilter State nötig

## Herausforderung 4: F# Compilation Order

### Das Problem

F# hat eine strikte Top-to-Bottom Compilation. Eine Datei kann nur Typen und Funktionen referenzieren, die in früheren Dateien definiert wurden. Das wurde mit der neuen Component-Struktur komplex:

```
Types.fs → State.fs → View.fs    // Innerhalb einer Komponente
Dashboard → Settings → SyncFlow → Rules    // Zwischen Komponenten?
Components/* → State.fs → View.fs    // Die Hauptanwendung
```

### Die Lösung: Sorgfältige .fsproj-Ordnung

```xml
<ItemGroup>
    <!-- Shared Types first -->
    <Compile Include="Types.fs" />
    <Compile Include="Api.fs" />

    <!-- Components - each in Types → State → View order -->
    <Compile Include="Components/Dashboard/Types.fs" />
    <Compile Include="Components/Dashboard/State.fs" />
    <Compile Include="Components/Dashboard/View.fs" />

    <Compile Include="Components/Settings/Types.fs" />
    <Compile Include="Components/Settings/State.fs" />
    <Compile Include="Components/Settings/View.fs" />

    <Compile Include="Components/SyncFlow/Types.fs" />
    <Compile Include="Components/SyncFlow/State.fs" />
    <Compile Include="Components/SyncFlow/View.fs" />

    <Compile Include="Components/Rules/Types.fs" />
    <Compile Include="Components/Rules/State.fs" />
    <Compile Include="Components/Rules/View.fs" />

    <!-- Main app - after all components -->
    <Compile Include="State.fs" />
    <Compile Include="View.fs" />
    <Compile Include="App.fs" />
</ItemGroup>
```

**Wichtig**: Die Reihenfolge der Komponenten untereinander ist egal, solange:
1. `Types.fs` vor `State.fs` vor `View.fs` kommt (innerhalb einer Komponente)
2. Alle Components vor der Main `State.fs` kommen
3. Shared Types (`Types.fs`, `Api.fs`) ganz am Anfang stehen

## Herausforderung 5: View Composition

### Das Problem

Die Hauptview muss jetzt Child-Views rendern und ihnen die richtigen Props geben. Aber die Child-Views erwarten einen mapped dispatch:

```fsharp
// Settings.View.fs erwartet:
let view (model: Model) (dispatch: Msg -> unit) = ...

// Aber im Main View haben wir:
let dispatch: State.Msg -> unit  // Nicht Settings.Msg!
```

### Die Lösung: Dispatch-Mapping

```fsharp
// In View.fs (Hauptanwendung)
let view (model: Model) (dispatch: Msg -> unit) =
    let pageContent =
        match model.CurrentPage with
        | Dashboard ->
            Components.Dashboard.View.view
                model.Dashboard
                (DashboardMsg >> dispatch)  // Map dispatch
                (fun () -> dispatch (NavigateTo SyncFlow))  // Callback für "Start Sync"
                (fun () -> dispatch (NavigateTo Settings))  // Callback für "Go to Settings"

        | Settings ->
            Components.Settings.View.view
                model.Settings
                (SettingsMsg >> dispatch)

        | SyncFlow ->
            Components.SyncFlow.View.view
                model.SyncFlow
                (SyncFlowMsg >> dispatch)

        | Rules ->
            Components.Rules.View.view
                model.Rules
                (RulesMsg >> dispatch)
```

**Die Dispatch-Transformation**:
- `DashboardMsg >> dispatch` ist eine Funktion `Dashboard.Msg -> unit`
- Sie nimmt eine `Dashboard.Msg`, wraps sie in `DashboardMsg`, und ruft den Parent-dispatch auf

**Callbacks für Navigation**:
Das Dashboard braucht Buttons, die zu anderen Seiten navigieren. Statt Navigation in der Komponente zu implementieren, übergebe ich Callbacks:

```fsharp
// Dashboard.View.fs
let view (model: Model) (dispatch: Msg -> unit)
         (onStartSync: unit -> unit) (onGoToSettings: unit -> unit) =

    Html.button [
        prop.onClick (fun _ -> onStartSync())  // Ruft Parent-Callback
        prop.text "Start New Sync"
    ]
```

**Rationale**: Die Dashboard-Komponente weiß nichts über das Routing-System. Sie ruft nur eine Funktion auf – der Parent entscheidet, was passiert.

## Herausforderung 6: Settings-Initialisierung

### Das Problem

Nach dem Refactoring startete die App mit leerem Settings-State. Wenn der User direkt zur Rules-Seite navigierte, fehlte die `DefaultBudgetId` zum Laden der Kategorien.

### Die Lösung: Settings beim App-Start laden

```fsharp
// In State.fs
let init () : Model * Cmd<Msg> =
    let dashboardModel, dashboardCmd = Components.Dashboard.State.init ()
    let settingsModel, settingsCmd = Components.Settings.State.init ()
    // ...

    let cmd = Cmd.batch [
        Cmd.map DashboardMsg dashboardCmd
        Cmd.map SettingsMsg settingsCmd  // ← Settings laden beim Start!
    ]
    model, cmd
```

Die Settings-Komponente lädt in ihrem `init` automatisch die Settings:

```fsharp
// In Components/Settings/State.fs
let init () : Model * Cmd<Msg> =
    { Settings = Loading; ... },
    Cmd.ofMsg LoadSettings  // Startet API-Call
```

**Ergebnis**: Wenn der User zu Rules navigiert, sind die Settings bereits geladen und die `DefaultBudgetId` ist verfügbar.

## Lessons Learned

### 1. Refactoring in kleinen Schritten

Ich habe versucht, alles auf einmal zu refactoren – ein Fehler. Besser wäre gewesen:
1. Erst eine Komponente extrahieren (z.B. Dashboard)
2. Testen, dass alles funktioniert
3. Nächste Komponente extrahieren
4. Repeat

So hätte ich Race-Conditions wie das Kategorien-Problem früher gefunden.

### 2. ExternalMsg von Anfang an planen

Ich habe das ExternalMsg-Pattern erst hinzugefügt, als ich merkte, dass ich es brauche. Hätte ich es von Anfang an eingeplant, wäre die Component-Signatur konsistenter:

```fsharp
// Nicht Dashboard, das kein ExternalMsg braucht:
Model * Cmd<Msg>

// Vs. alle anderen:
Model * Cmd<Msg> * ExternalMsg
```

Jetzt ist Dashboard inkonsistent, weil es das Einzige ist, das nur ein Tuple zurückgibt.

### 3. Parent-Interception ist mächtig, aber versteckt

Das Pattern, dass der Parent bestimmte Messages abfängt, ist elegant, aber:
- Es ist nicht offensichtlich, wenn man nur die Komponente liest
- Man muss in der Parent-State.fs nachschauen, um zu verstehen was passiert
- Dokumentation hilft: `// Parent handles this`

### 4. Die Compilation-Order ist dein Freund

Die strikte F#-Compilation-Order zwingt zu sauberer Architektur:
- Keine zirkulären Abhängigkeiten möglich
- Die Reihenfolge dokumentiert die Architektur
- Wenn die .fsproj-Order nicht stimmt, gibt's sofort einen Build-Fehler

## Fazit

Das Refactoring war aufwändiger als gedacht, aber es hat sich gelohnt:

**Vorher**:
- `State.fs`: ~1000 Zeilen
- Eine riesige Update-Funktion
- Alles verwoben

**Nachher**:
- `State.fs`: ~190 Zeilen (nur Composition)
- 12 neue Dateien in `Components/`
- Jede Komponente eigenständig und testbar
- 3553 Zeilen gesamt in Components (verteilt auf 4 Features)

**Statistiken**:
- 4 Komponenten: Dashboard, Settings, SyncFlow, Rules
- 12 neue Dateien erstellt
- 4 alte View-Dateien gelöscht
- Build: 0 Errors, 0 Warnings
- Tests: 115 passed, 6 skipped

Die neue Architektur macht es einfach, Features hinzuzufügen oder zu ändern. Jede Komponente ist ein eigenständiges Mini-MVU-System. Der Parent kümmert sich nur um Composition und Cross-Cutting Concerns (Toast, Navigation).

## Key Takeaways für Neulinge

1. **MVU-Components in Elmish brauchen ein Composition-Pattern**: Es gibt keinen eingebauten Component-Typ. Das ExternalMsg-Pattern ist der Standard-Weg für Parent-Child-Kommunikation.

2. **Die Compilation-Order ist Architektur-Dokumentation**: In F# zeigt die .fsproj-Reihenfolge die Abhängigkeiten. Types → State → View innerhalb, Components → Main App zwischen Modulen.

3. **Geteilte Daten über den Parent orchestrieren**: Statt globalen State zu haben, fängt der Parent relevante Messages ab und verteilt Daten an die Komponenten, die sie brauchen.
