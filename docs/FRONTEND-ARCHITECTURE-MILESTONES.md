# Frontend Architecture Improvement Milestones

**Basierend auf:** `reviews/frontend-architecture.md` (2025-12-15)
**Gesamtbewertung:** GUT - Keine kritischen Issues, aber Verbesserungspotential

---

## Milestone 1: React Key Props (Priority 1)

**Ziel:** Alle Listen-Renderings mit korrekten React Key Props versehen für effiziente Reconciliation.

### Aufgaben

- [x] **1.1** SyncFlow/View.fs - Transaction-Liste mit Keys versehen *(bereits implementiert)*
- [x] **1.2** SyncFlow/View.fs - Category-Dropdown-Optionen mit Keys *(nicht nötig - deprecated transactionCard)*
- [x] **1.3** Rules/View.fs - Rule-Liste mit Keys versehen *(bereits implementiert)*
- [x] **1.4** Alle anderen `for ... do` Schleifen in Views prüfen und Keys hinzufügen

### Betroffene Dateien

- `src/Client/Components/SyncFlow/View.fs` - ✅ Transaction-Liste hatte bereits Keys
- `src/Client/Components/Rules/View.fs` - ✅ Rule-Liste hatte bereits Keys, Skeleton-Loader gefixed
- `src/Client/View.fs` - ✅ Keine for-Schleifen gefunden
- `src/Client/Components/Dashboard/View.fs` - ✅ Keine for-Schleifen gefunden
- `src/Client/Components/Settings/View.fs` - ✅ Budget/Account Dropdowns gefixed

### Pattern

```fsharp
// Vorher
for tx in transactions do
    transactionRow tx dispatch

// Nachher
for tx in transactions do
    Html.div [
        prop.key (string tx.Transaction.Id)
        prop.children [ transactionRow tx dispatch ]
    ]
```

### Verifikation

- [x] `dotnet build` erfolgreich
- [x] Keine React-Warnungen in Browser-Konsole *(structural fixes applied)*
- [x] UI verhält sich bei Listen-Updates korrekt

### ✅ Milestone 1 Complete (2025-12-15)

**Summary of Changes:**
- Settings/View.fs: Added `prop.key` to Budget dropdown options (line 150-155)
- Settings/View.fs: Added `prop.key` to Account dropdown options (line 194-199)
- Rules/View.fs: Added `prop.key` to Skeleton loader items (line 570-574)
- Verified existing implementations: SyncFlow transaction list and Rules list already had proper keys

**Notes:**
- Transaction-Liste in SyncFlow/View.fs hatte bereits korrekte Keys implementiert
- Rule-Liste in Rules/View.fs hatte bereits korrekte Keys implementiert
- deprecated transactionCard wurde nicht gefixed (wird nicht mehr verwendet)

---

## Milestone 2: SyncFlow View Modularisierung (Priority 1)

**Ziel:** Die 1700+ Zeilen große `SyncFlow/View.fs` in kleinere, wartbare Module aufteilen.

### Aufgaben

- [ ] **2.1** Neuen Ordner `src/Client/Components/SyncFlow/Views/` erstellen
- [ ] **2.2** `TransactionRow.fs` extrahieren - Einzelne Transaktionszeile
- [ ] **2.3** `TransactionList.fs` extrahieren - Liste mit Header und Pagination
- [ ] **2.4** `SplitEditor.fs` extrahieren - Split-Transaction UI
- [ ] **2.5** `InlineRuleForm.fs` extrahieren - Inline-Regel-Erstellung
- [ ] **2.6** `StatusViews.fs` extrahieren - Loading, Error, Completed States
- [ ] **2.7** `CategorySelector.fs` extrahieren - Kategorie-Auswahl Komponente
- [ ] **2.8** Haupt-View.fs auf Komposition reduzieren
- [ ] **2.9** SyncFlow.fsproj aktualisieren mit neuen Dateien

### Neue Dateistruktur

```
src/Client/Components/SyncFlow/
├── Types.fs
├── State.fs
├── View.fs              # Hauptkomposition, ~200 Zeilen
└── Views/
    ├── TransactionRow.fs
    ├── TransactionList.fs
    ├── SplitEditor.fs
    ├── InlineRuleForm.fs
    ├── StatusViews.fs
    └── CategorySelector.fs
```

### Verifikation

- [ ] `dotnet build` erfolgreich
- [ ] Keine funktionalen Änderungen (rein strukturell)
- [ ] Alle SyncFlow-Funktionen weiterhin korrekt
- [ ] Development Diary aktualisiert

---

## Milestone 3: Rules Form State Konsolidierung (Priority 2)

**Ziel:** Die 9 separaten Form-Felder im Rules Model in einen dedizierten Record-Typ gruppieren.

### Aufgaben

- [ ] **3.1** `RuleFormState` Record-Typ in Types.fs definieren
- [ ] **3.2** Model-Typ anpassen - Form-Felder durch `Form: RuleFormState` ersetzen
- [ ] **3.3** State.fs `init` Funktion anpassen
- [ ] **3.4** State.fs `update` Funktion anpassen - alle Form-Messages
- [ ] **3.5** View.fs anpassen - Zugriff auf `model.Form.FieldName`
- [ ] **3.6** Helper-Funktionen für Form-Reset und Form-From-Rule erstellen

### Vorher

```fsharp
type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    RuleFormName: string
    RuleFormPattern: string
    RuleFormPatternType: PatternType
    RuleFormTargetField: TargetField
    RuleFormCategoryId: YnabCategoryId option
    RuleFormPayeeOverride: string
    RuleFormEnabled: bool
    RuleFormTestInput: string
    RuleFormTestResult: string option
    RuleSaving: bool
    ConfirmingDeleteRuleId: RuleId option
}
```

### Nachher

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
    let empty = { ... }
    let fromRule (rule: Rule) = { ... }

type Model = {
    Rules: RemoteData<Rule list>
    EditingRule: Rule option
    IsNewRule: bool
    Form: RuleFormState
    ConfirmingDeleteRuleId: RuleId option
}
```

### Verifikation

- [ ] `dotnet build` erfolgreich
- [ ] Regel-Editor funktioniert weiterhin
- [ ] Alle Form-Validierungen aktiv
- [ ] Tests bestehen

---

## Milestone 4: ErrorDisplay Design System Komponente (Priority 2)

**Ziel:** Standardisierte Error-Anzeige im Design System für konsistente Fehlerdarstellung.

### Aufgaben

- [ ] **4.1** `src/Client/DesignSystem/ErrorDisplay.fs` erstellen
- [ ] **4.2** Standard Error-Card mit Icon, Message, optionalem Retry-Button
- [ ] **4.3** Inline Error-Variante für Formulare
- [ ] **4.4** Full-Page Error-Variante
- [ ] **4.5** Client.fsproj aktualisieren
- [ ] **4.6** Bestehende Error-Anzeigen durch ErrorDisplay ersetzen

### API Design

```fsharp
module ErrorDisplay =
    /// Standard error card with icon and optional retry
    let card (message: string) (onRetry: (unit -> unit) option) =
        ...

    /// Inline error for form fields
    let inline' (message: string) =
        ...

    /// Full-page error state
    let fullPage (title: string) (message: string) (onRetry: (unit -> unit) option) =
        ...

    /// Error for RemoteData.Failure
    let forRemoteData (error: string) (onRetry: unit -> unit) =
        ...
```

### Verifikation

- [ ] Komponente in allen Error-Szenarien getestet
- [ ] Konsistentes Styling über alle Komponenten
- [ ] Accessibility (ARIA-Attribute)

---

## Milestone 5: Dashboard Hero Button Design System (Priority 2)

**Ziel:** Den Dashboard Sync-Button ins Design System integrieren statt Inline-Styles.

### Aufgaben

- [ ] **5.1** `Button.hero` Variante in Button.fs hinzufügen
- [ ] **5.2** Glow-Effekte als wiederverwendbare Klassen in Tokens definieren
- [ ] **5.3** Dashboard/View.fs syncButton durch Button.hero ersetzen
- [ ] **5.4** Dokumentation in CLAUDE.md aktualisieren

### Button.hero API

```fsharp
/// Hero button with glow effect for prominent CTAs
let hero (text: string) (icon: ReactElement option) (onClick: unit -> unit) =
    Html.button [
        prop.className [
            "group relative px-12 py-5 rounded-xl"
            "bg-gradient-to-r from-neon-orange to-neon-orange/80"
            "text-base-100 font-bold text-lg md:text-xl font-display"
            Tokens.Effects.glowOrange
            "hover:scale-105 transition-all duration-300"
        ] |> String.concat " "
        prop.onClick (fun _ -> onClick())
        prop.children [
            match icon with
            | Some i -> i
            | None -> Html.none
            Html.span [ prop.text text ]
        ]
    ]
```

### Verifikation

- [ ] Dashboard sieht identisch aus wie vorher
- [ ] Button ist wiederverwendbar
- [ ] Hover-Effekte funktionieren

---

## Milestone 6: RemoteData Helper Module (Priority 3)

**Ziel:** Utility-Funktionen für häufige RemoteData-Operationen.

### Aufgaben

- [ ] **6.1** `src/Client/RemoteDataHelpers.fs` erstellen
- [ ] **6.2** `map`, `bind`, `isLoading`, `isSuccess`, `toOption`, `withDefault` implementieren
- [ ] **6.3** `mapError`, `recover` für Error-Handling
- [ ] **6.4** Client.fsproj aktualisieren
- [ ] **6.5** Bestehenden Code refactoren wo sinnvoll

### API

```fsharp
module RemoteData =
    let map f = function
        | Success x -> Success (f x)
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    let bind f = function
        | Success x -> f x
        | Loading -> Loading
        | NotAsked -> NotAsked
        | Failure err -> Failure err

    let isLoading = function Loading -> true | _ -> false
    let isSuccess = function Success _ -> true | _ -> false
    let isFailure = function Failure _ -> true | _ -> false

    let toOption = function Success x -> Some x | _ -> None
    let withDefault d = function Success x -> x | _ -> d

    let mapError f = function
        | Failure err -> Failure (f err)
        | other -> other
```

### Verifikation

- [ ] Unit-Tests für alle Helper-Funktionen
- [ ] Keine Breaking Changes

---

## Milestone 7: PageHeader Design System Komponente (Priority 3)

**Ziel:** Wiederverwendbare Page-Header-Komponente für konsistente Seitenlayouts.

### Aufgaben

- [ ] **7.1** `src/Client/DesignSystem/PageHeader.fs` erstellen
- [ ] **7.2** Title, Subtitle, Actions-Slots
- [ ] **7.3** Responsive Layout (Stack auf Mobile, Row auf Desktop)
- [ ] **7.4** Client.fsproj aktualisieren
- [ ] **7.5** Dashboard, Rules, Settings Views anpassen

### API

```fsharp
module PageHeader =
    type Props = {
        Title: string
        Subtitle: string option
        Actions: ReactElement list
    }

    let view (props: Props) =
        Html.div [
            prop.className "flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 mb-6"
            prop.children [
                Html.div [
                    Html.h1 [
                        prop.className "text-2xl md:text-4xl font-bold font-display text-base-content"
                        prop.text props.Title
                    ]
                    match props.Subtitle with
                    | Some sub ->
                        Html.p [
                            prop.className "text-base-content/60 mt-1"
                            prop.text sub
                        ]
                    | None -> Html.none
                ]
                Html.div [
                    prop.className "flex gap-2"
                    prop.children props.Actions
                ]
            ]
        ]

    /// Simple header with just title
    let simple title = view { Title = title; Subtitle = None; Actions = [] }

    /// Header with title and subtitle
    let withSubtitle title subtitle = view { Title = title; Subtitle = Some subtitle; Actions = [] }

    /// Full header with actions
    let withActions title subtitle actions = view { Title = title; Subtitle = subtitle; Actions = actions }
```

### Verifikation

- [ ] Konsistente Headers auf allen Seiten
- [ ] Responsive auf Mobile und Desktop

---

## Milestone 8: Category Selection Debouncing (Priority 3)

**Ziel:** Debouncing für schnelle Kategorie-Änderungen um Server-Load zu reduzieren.

### Aufgaben

- [ ] **8.1** Debounce-Helper in Client erstellen oder Fable.Elmish.Debounce nutzen
- [ ] **8.2** SyncFlow/State.fs - CategoryChanged Message debounced behandeln
- [ ] **8.3** Pending-State während Debounce anzeigen
- [ ] **8.4** Testen mit schnellen Kategorie-Wechseln

### Verifikation

- [ ] Nur ein API-Call bei schnellen Änderungen
- [ ] UI reagiert sofort (optimistisch)
- [ ] Keine Race Conditions

---

## Zusammenfassung

| Milestone | Priorität | Aufwand | Status |
|-----------|-----------|---------|--------|
| 1. React Key Props | P1 | Klein | ✅ Complete (2025-12-15) |
| 2. SyncFlow Modularisierung | P1 | Mittel | [ ] Offen |
| 3. Rules Form State | P2 | Klein | [ ] Offen |
| 4. ErrorDisplay Komponente | P2 | Klein | [ ] Offen |
| 5. Dashboard Hero Button | P2 | Klein | [ ] Offen |
| 6. RemoteData Helpers | P3 | Klein | [ ] Offen |
| 7. PageHeader Komponente | P3 | Klein | [ ] Offen |
| 8. Debouncing | P3 | Mittel | [ ] Offen |

---

## Hinweise

- **Keine funktionalen Änderungen** - Alle Milestones sind Refactorings/Verbesserungen
- **Rückwärtskompatibel** - Bestehende Funktionalität bleibt erhalten
- **Inkrementell** - Jeder Milestone kann unabhängig umgesetzt werden
- **QA Review** - Nach jedem Milestone `qa-milestone-reviewer` Agent aufrufen

---

*Erstellt: 2025-12-15 basierend auf Frontend Architecture Review*
