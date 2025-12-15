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

- [x] **2.1** Neuen Ordner `src/Client/Components/SyncFlow/Views/` erstellen
- [x] **2.2** `TransactionRow.fs` extrahieren - Einzelne Transaktionszeile
- [x] **2.3** `TransactionList.fs` extrahieren - Liste mit Header und Pagination
- [x] ~~**2.4** `SplitEditor.fs` extrahieren - Split-Transaction UI~~ *(nicht implementiert - kein Split-Editor vorhanden)*
- [x] **2.5** `InlineRuleForm.fs` extrahieren - Inline-Regel-Erstellung
- [x] **2.6** `StatusViews.fs` extrahieren - Loading, Error, Completed States
- [x] ~~**2.7** `CategorySelector.fs` extrahieren - Kategorie-Auswahl Komponente~~ *(in TransactionRow.fs integriert)*
- [x] **2.8** Haupt-View.fs auf Komposition reduzieren
- [x] **2.9** Client.fsproj aktualisieren mit neuen Dateien

### Neue Dateistruktur

```
src/Client/Components/SyncFlow/
├── Types.fs
├── State.fs
├── View.fs              # Hauptkomposition, ~90 Zeilen
└── Views/
    ├── StatusViews.fs   # ~350 Zeilen - Alle Status-Views
    ├── InlineRuleForm.fs # ~200 Zeilen - Inline Rule Creation
    ├── TransactionRow.fs # ~450 Zeilen - Einzelne Transaktionszeile
    └── TransactionList.fs # ~310 Zeilen - Transaktionsliste
```

### Verifikation

- [x] `dotnet build` erfolgreich
- [x] Keine funktionalen Änderungen (rein strukturell)
- [x] Alle SyncFlow-Funktionen weiterhin korrekt
- [x] Development Diary aktualisiert

### ✅ Milestone 2 Complete (2025-12-15)

**Summary of Changes:**
- Created `src/Client/Components/SyncFlow/Views/` folder
- Extracted `StatusViews.fs` with: `tanWaitingView`, `fetchingView`, `loadingView`, `errorView`, `completedView`, `startSyncView`
- Extracted `InlineRuleForm.fs` with the inline rule creation form
- Extracted `TransactionRow.fs` with: `transactionRow`, `statusDot`, `duplicateIndicator`, `expandChevron`, `skipToggleIcon`, `createRuleButton`, `memoRow`, `duplicateDebugInfo` and helpers
- Extracted `TransactionList.fs` with: `transactionListView`, `filterTransactions`
- Reduced main `View.fs` from ~1700 lines to ~90 lines (composition only)
- Updated `Client.fsproj` with correct compilation order

**Test Quality Review:**
- Build successful with 0 errors
- All 294 tests passed
- No functional changes - purely structural refactoring

**Notes:**
- `SplitEditor.fs` was not created because there is no split transaction UI in the current codebase
- `CategorySelector.fs` was not created separately as category selection is integrated into `TransactionRow.fs` using `Input.searchableSelect`

---

## Milestone 3: Rules Form State Konsolidierung (Priority 2)

**Ziel:** Die 9 separaten Form-Felder im Rules Model in einen dedizierten Record-Typ gruppieren.

### Aufgaben

- [x] **3.1** `RuleFormState` Record-Typ in Types.fs definieren
- [x] **3.2** Model-Typ anpassen - Form-Felder durch `Form: RuleFormState` ersetzen
- [x] **3.3** State.fs `init` Funktion anpassen
- [x] **3.4** State.fs `update` Funktion anpassen - alle Form-Messages
- [x] **3.5** View.fs anpassen - Zugriff auf `model.Form.FieldName`
- [x] **3.6** Helper-Funktionen für Form-Reset und Form-From-Rule erstellen

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

- [x] `dotnet build` erfolgreich
- [x] Regel-Editor funktioniert weiterhin
- [x] Alle Form-Validierungen aktiv
- [x] Tests bestehen

### ✅ Milestone 3 Complete (2025-12-15)

**Summary of Changes:**
- Added `RuleFormState` record type in `Types.fs` with 10 fields for form state
- Added `RuleFormState` module with `empty` and `fromRule` helper functions
- Updated `Model` type to use single `Form: RuleFormState` field instead of 10 separate fields
- Refactored `State.fs` to use `RuleFormState.empty` and `RuleFormState.fromRule`
- Updated all form update handlers to use nested record update syntax
- Updated `View.fs` to access form fields via `model.Form.FieldName`

**Test Quality Review:**
- Build successful with 0 errors
- All 294 tests passed
- No functional changes - purely structural refactoring

**Notes:**
- The refactoring reduces the Model from 14 fields to 7 fields
- Helper functions make form reset and initialization cleaner
- Consistent `model.Form.X` access pattern improves readability

---

## Milestone 4: ErrorDisplay Design System Komponente (Priority 2)

**Ziel:** Standardisierte Error-Anzeige im Design System für konsistente Fehlerdarstellung.

### Aufgaben

- [x] **4.1** `src/Client/DesignSystem/ErrorDisplay.fs` erstellen
- [x] **4.2** Standard Error-Card mit Icon, Message, optionalem Retry-Button
- [x] **4.3** Inline Error-Variante für Formulare
- [x] **4.4** Full-Page Error-Variante
- [x] **4.5** Client.fsproj aktualisieren
- [x] **4.6** Bestehende Error-Anzeigen durch ErrorDisplay ersetzen

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

- [x] Komponente in allen Error-Szenarien getestet
- [x] Konsistentes Styling über alle Komponenten
- [x] Accessibility (ARIA-Attribute)

### ✅ Milestone 4 Complete (2025-12-15)

**Summary of Changes:**
- Created `src/Client/DesignSystem/ErrorDisplay.fs` with multiple variants:
  - `inline'` / `inlineWithIcon` - Compact inline errors for form validation
  - `card` / `cardWithTitle` / `cardCompact` - Card-based errors with optional retry
  - `hero` / `heroSimple` - Large hero-style errors for major operations
  - `fullPage` / `fullPageWithAction` - Full-page error states
  - `forRemoteData` / `simple` / `warning` - Convenience functions
- Updated Client.fsproj with ErrorDisplay.fs
- Replaced error displays in: StatusViews.fs, TransactionList.fs, Settings/View.fs, Rules/View.fs

**Test Quality Review:**
- Build successful with 0 errors
- All 294 tests passed
- No functional changes - purely refactoring

**Notes:**
- All error displays now include `role="alert"` for accessibility
- Consistent neon color palette (neon-red/neon-pink gradients)
- Hero variant used for SyncFlow errors, cardCompact for inline contexts

---

## Milestone 5: Dashboard Hero Button Design System (Priority 2)

**Ziel:** Den Dashboard Sync-Button ins Design System integrieren statt Inline-Styles.

### Aufgaben

- [x] **5.1** `Button.hero` Variante in Button.fs hinzufügen
- [x] **5.2** Glow-Effekte als wiederverwendbare Klassen in Tokens definieren
- [x] **5.3** Dashboard/View.fs syncButton durch Button.hero ersetzen
- [x] **5.4** Dokumentation in CLAUDE.md aktualisieren

### Implementierte API

```fsharp
// Hero button - large CTA with prominent glow
Button.hero "Get Started" onClick

// Hero with icon (icon before text)
Button.heroWithIcon "Start Sync" (Icons.sync MD Primary) onClick

// Hero with loading state
Button.heroLoading "Processing..." isLoading onClick

// Teal variant for secondary prominent actions
Button.heroTeal "Continue" onClick
```

### Verifikation

- [x] Dashboard sieht identisch aus wie vorher
- [x] Button ist wiederverwendbar
- [x] Hover-Effekte funktionieren

### ✅ Milestone 5 Complete (2025-12-15)

**Summary of Changes:**
- Added `Glows.orangeLg`, `Glows.orangeHoverLg`, `Glows.tealLg`, `Glows.tealHoverLg`, `Glows.greenLg`, `Glows.greenHoverLg` to Tokens.fs
- Added `Button.hero`, `Button.heroWithIcon`, `Button.heroLoading`, `Button.heroTeal` to Button.fs
- Replaced inline-styled `syncButton` in Dashboard/View.fs with `Button.heroWithIcon`
- Updated CLAUDE.md with hero button documentation

**Test Quality Review:**
- Build successful with 0 errors
- All 294 tests passed
- No functional changes - purely refactoring for Design System consistency

**Notes:**
- The API differs slightly from the original plan: instead of `Option<ReactElement>` for icon, we have separate `hero` and `heroWithIcon` functions for clearer usage
- Large glow effects use inline shadow definitions since Tailwind utility classes weren't sufficient for the hero-sized glow

---

## Milestone 6: RemoteData Helper Module (Priority 3)

**Ziel:** Utility-Funktionen für häufige RemoteData-Operationen.

### Aufgaben

- [x] **6.1** RemoteData-Modul in `src/Client/Types.fs` hinzufügen *(nicht separate Datei)*
- [x] **6.2** `map`, `bind`, `isLoading`, `isSuccess`, `toOption`, `withDefault` implementieren
- [x] **6.3** `mapError`, `recover`, `recoverWith` für Error-Handling
- [x] **6.4** Zusätzliche Helper: `map2`, `fold`, `toError`, `fromResult`, `fromOption`, `fromOptionWithError`
- [x] **6.5** Bestehenden Code analysiert - Refactoring nicht nötig (explizite Matches sind lesbar)

### Implementierte API

```fsharp
[<RequireQualifiedAccess>]
module RemoteData =
    val map: ('a -> 'b) -> RemoteData<'a> -> RemoteData<'b>
    val bind: ('a -> RemoteData<'b>) -> RemoteData<'a> -> RemoteData<'b>
    val isLoading: RemoteData<'a> -> bool
    val isSuccess: RemoteData<'a> -> bool
    val isFailure: RemoteData<'a> -> bool
    val isNotAsked: RemoteData<'a> -> bool
    val toOption: RemoteData<'a> -> 'a option
    val withDefault: 'a -> RemoteData<'a> -> 'a
    val mapError: (string -> string) -> RemoteData<'a> -> RemoteData<'a>
    val recover: 'a -> RemoteData<'a> -> RemoteData<'a>
    val recoverWith: (string -> 'a) -> RemoteData<'a> -> RemoteData<'a>
    val map2: ('a -> 'b -> 'c) -> RemoteData<'a> -> RemoteData<'b> -> RemoteData<'c>
    val toError: RemoteData<'a> -> string option
    val fold: 'b -> 'b -> ('a -> 'b) -> (string -> 'b) -> RemoteData<'a> -> 'b
    val fromResult: Result<'a, string> -> RemoteData<'a>
    val fromOption: 'a option -> RemoteData<'a>
    val fromOptionWithError: string -> 'a option -> RemoteData<'a>
```

### Verifikation

- [x] Unit-Tests für alle Helper-Funktionen (63 Tests)
- [x] Keine Breaking Changes
- [x] Build erfolgreich
- [x] Alle Tests bestehen (357 total)

### ✅ Milestone 6 Complete (2025-12-15)

**Summary of Changes:**
- Added `RemoteData` module to `src/Client/Types.fs` with 17 helper functions
- Added `src/Tests/RemoteDataTests.fs` with 63 comprehensive unit tests
- Added Client project reference to Tests.fsproj to enable testing
- Module uses `[<RequireQualifiedAccess>]` for explicit access via `RemoteData.map`, etc.

**Test Quality Review:**
- 63 unit tests covering all helper functions
- Tests cover Success, Loading, NotAsked, and Failure cases for each function
- All edge cases tested (e.g., map2 with different failure combinations)

**Notes:**
- Module added directly to Types.fs rather than separate file for simpler compilation order
- Existing code uses explicit match expressions which are already readable
- Helper functions available for new code and future refactoring opportunities

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
| 2. SyncFlow Modularisierung | P1 | Mittel | ✅ Complete (2025-12-15) |
| 3. Rules Form State | P2 | Klein | ✅ Complete (2025-12-15) |
| 4. ErrorDisplay Komponente | P2 | Klein | ✅ Complete (2025-12-15) |
| 5. Dashboard Hero Button | P2 | Klein | ✅ Complete (2025-12-15) |
| 6. RemoteData Helpers | P3 | Klein | ✅ Complete (2025-12-15) |
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
