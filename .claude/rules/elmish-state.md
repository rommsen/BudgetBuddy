---
paths:
  - "src/Client/State/**"
  - "src/Client/State.fs"
---

# Elmish State — MVU Pflicht-Patterns

## Verboten

- **Boolean Flags für Async State:** Kein `IsLoading: bool` — immer `RemoteData<'T>`
- **`Async.StartImmediate` in Views:** Side Effects NUR über `Cmd` im `update`
- **`Cmd.OfAsync.perform`:** Verschluckt Exceptions — immer `Cmd.OfAsync.either` mit Error-Case
- **`Guid.Empty` als Platzhalter:** Echte Werte durchreichen oder `Option` verwenden
- **Zirkuläre Imports:** Child-Module dürfen Parent-State und Sibling-Module NICHT importieren
- **`init()` ohne Cmd:** Immer `init () : Model * Cmd<Msg>` zurückgeben (Elmish-Konvention)

## Richtig

```fsharp
// Async State
type Model = { Items: RemoteData<Item list> }  // NotAsked | Loading | Success | Failure

// API-Call mit Error Handling
Cmd.OfAsync.either
    (fun () -> api.loadItems ())
    ()
    (Ok >> ItemsLoaded)
    (fun ex -> ItemsLoaded (Error ex.Message))

// init mit Cmd
let init () : Model * Cmd<Msg> =
    { Items = NotAsked }, Cmd.none

// Cross-Feature: NUR im Root-State
| SparksMsg sparksMsg ->
    let model', cmd = Sparks.update sparksMsg model.Sparks
    { model with Sparks = model' }, Cmd.map SparksMsg cmd
```

## .fsproj Compile-Reihenfolge

F# kompiliert Dateien in der Reihenfolge der `.fsproj`. Dependencies MÜSSEN vorher stehen:

```xml
<!-- RICHTIG: Shared → Child States → Parent State → Views -->
<Compile Include="State/SparksQuick.fs" />
<Compile Include="State/SparksMenu.fs" />
<Compile Include="State/Sparks.fs" />      <!-- nach Children -->
<Compile Include="State.fs" />              <!-- Root nach allen Feature-States -->
```

Fehler bei falscher Reihenfolge: `FS0039: The namespace or module 'X' is not defined`

### RouteChanged Data-Loading

Bei jedem neuen Feature das Daten auf bestehenden Screens anzeigt:
- `grep "RouteChanged" src/Client/State.fs` ausführen
- Jeden Route-Case prüfen: Lädt er die neuen Daten?
- Catch-all `| _ -> model, Cmd.none` fängt fehlende Cases stillschweigend ab

**Typisches Fehlmuster:** Navigation via Bottom Nav funktioniert (State schon geladen),
aber Hash-basierte Navigation (E2E, Deep Link) zeigt leere Seite.

# Grep-Checks

```bash
# Boolean Loading Flags
grep -rn 'IsLoading\|isLoading' src/Client/State/

# Async.StartImmediate in Views
grep -rn 'Async\.StartImmediate' src/Client/Views/

# Cmd.OfAsync.perform (fehlende Error-Behandlung)
grep -rn 'Cmd\.OfAsync\.perform' src/Client/

# Guid.Empty als Platzhalter
grep -rn 'Guid\.Empty' src/Client/State/
```
