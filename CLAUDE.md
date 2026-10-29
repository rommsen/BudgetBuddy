# F# Full-Stack Blueprint - Claude Code Instructions

## Your Role

You are developing F# web applications using this blueprint. The codebase uses Elmish.React + Feliz (frontend), Giraffe + Fable.Remoting (backend), SQLite (persistence), and deploys via Docker + Tailscale.

## Before Implementing Anything

**Always read the relevant documentation first:**

1. Check `standards/README.md` for what is documented
2. Read the specific guide for your task (see Documentation Map below)
3. Look at existing code to match patterns

## Documentation Map

| Task | Read This First |
|------|-----------------|
| Overview | `standards/README.md` |
| Frontend (UI, state) | `standards/frontend/overview.md`, `standards/frontend/styleguide.md` |
| Backend (API, logic, persistence) | `standards/backend/overview.md` |
| Tests | `.claude/rules/test-doctrine.md` |
| Docker/deployment | `Dockerfile`, `compose.yaml`, `scripts/deploy.sh` |

## Implementing User Specifications

When the user provides a specification file (markdown describing a feature):

1. **Read the specification file** thoroughly
2. **Read the guide from the Documentation Map** for patterns
3. **Plan the implementation** using the development order below
4. **Implement each layer**, testing as you go
5. **Verify with build and tests**

### Development Order

```
1. src/Shared/Domain.fs     → Define types
2. src/Shared/Api.fs        → Define API contract
3. src/Server/Validation.fs → Input validation
4. src/Server/Domain.fs     → Business logic (PURE - no I/O!)
5. src/Server/Persistence.fs → Database/file operations
6. src/Server/Api.fs        → Implement API
7. src/Client/State.fs      → Model, Msg, update
8. src/Client/View.fs       → UI components
9. src/Tests/               → Tests
```

## Qualitätssicherung

**IMPORTANT**: Die folgenden Qualitätskriterien gelten für jedes Feature, jeden Bugfix und jede andere signifikante Code-Änderung.

### Qualitätskriterien (MANDATORY)

Bei jeder Feature-Implementierung oder Bug-Fix MÜSSEN folgende Schritte eingehalten werden:

1. **QA Review**: Nach Abschluss den **qa-milestone-reviewer** Agent aufrufen
2. **Tests**: Alle neuen Features/Fixes müssen Tests haben
3. **Build & Test**: `dotnet build` und `dotnet test` müssen erfolgreich sein

### QA Review Process

Nach jeder Implementierung und vor dem Abschluss, ALWAYS use:
```
Task tool with subagent_type='qa-milestone-reviewer'
```
This agent will:
- Verify tests are meaningful and don't test tautologies
- Ensure all important behavior is covered by tests
- Identify any missing test coverage
- Define missing tests against `.claude/rules/test-doctrine.md`

## Key Principles

### Type Safety First
- Define ALL types in `src/Shared/` before implementing
- Use `Result<'T, string>` for fallible operations
- Use discriminated unions for state variations

### Pure Domain Logic
- `src/Server/Domain.fs` must have NO I/O operations
- All side effects go in `Persistence.fs` or `Api.fs`
- Domain functions are pure transformations

### MVU Architecture
- All frontend state changes through `update` function
- Use `Cmd` for side effects (API calls, etc.)
- View is pure function of Model

### RemoteData Pattern
```fsharp
type RemoteData<'T> = NotAsked | Loading | Success of 'T | Failure of string
```
Use this for all async operations in frontend state.

#### RemoteData Helpers

The `RemoteData` module in `Types.fs` provides utility functions:

```fsharp
open Types

// Transformations
let mapped = RemoteData.map (fun x -> x + 1) (Success 5)  // Success 6
let bound = RemoteData.bind (fun x -> if x > 0 then Success x else Failure "invalid") (Success 5)

// State checks
if RemoteData.isLoading model.Data then showSpinner()
if RemoteData.isSuccess model.Data then showContent()

// Extraction
let value = RemoteData.withDefault 0 model.Data  // Returns 0 if not Success
let opt = RemoteData.toOption model.Data         // Some x or None

// Error handling
let recovered = RemoteData.recover [] (Failure "err")           // Success []
let transformed = RemoteData.mapError (sprintf "Error: %s") rd  // Transform error message

// Combining
let combined = RemoteData.map2 (+) (Success 1) (Success 2)  // Success 3

// Conversion
let fromRes = RemoteData.fromResult (Ok 42)     // Success 42
let fromOpt = RemoteData.fromOption (Some 42)   // Success 42
```

### Validate Early
- Validate at API boundary before any processing
- Return clear error messages

## Code Patterns

### Backend API Implementation
```fsharp
let api : IEntityApi = {
    getAll = fun () -> Persistence.getAllEntities()

    getById = fun id -> async {
        match! Persistence.getById id with
        | Some e -> return Ok e
        | None -> return Error "Not found"
    }

    save = fun entity -> async {
        match Validation.validate entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    }
}
```

### Frontend State
```fsharp
type Model = { Entities: RemoteData<Entity list> }

type Msg =
    | LoadEntities
    | EntitiesLoaded of Result<Entity list, string>

let update msg model =
    match msg with
    | LoadEntities ->
        { model with Entities = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (Error >> EntitiesLoaded)

    | EntitiesLoaded (Ok entities) ->
        { model with Entities = Success entities }, Cmd.none

    | EntitiesLoaded (Error err) ->
        { model with Entities = Failure err }, Cmd.none
```

## Anti-Patterns to Avoid

- **I/O in Domain.fs** - Keep domain logic pure
- **Ignoring Result types** - Always handle errors explicitly
- **Classes for domain types** - Use records and unions
- **Skipping validation** - Validate at API boundary
- **Not reading documentation** - Check guides before implementing
- **Tests writing to production database** - NEVER write tests that persist data to the production database. Always use in-memory SQLite (`Data Source=:memory:`) or temporary test databases for integration tests
- **Bug fixes without regression tests** - See mandatory rule below

## Bug Fix Protocol (MANDATORY)

**CRITICAL**: Every bug fix MUST include a regression test. No exceptions.

### When Fixing a Bug:

1. **Understand the root cause** - Don't just fix symptoms
2. **Write a failing test FIRST** that reproduces the bug
3. **Fix the bug** - Make the test pass
4. **Verify no regressions** - Run full test suite

### Test Requirements for Bug Fixes:

- Test must **fail before the fix** and **pass after**
- Test must verify the **specific behavior** that was broken
- Test should include a comment explaining **what bug it prevents**
- Consider edge cases that might cause similar bugs

### Example Test Comment:
```fsharp
testCase "amount is serialized as JSON number, not string" <| fun () ->
    // This test prevents regression of the bug where Encode.int64 serialized
    // amounts as strings (e.g., "-50250" instead of -50250), causing YNAB
    // to silently reject transactions.
    ...
```

### Why This Matters:
- Bugs that aren't tested **will come back**
- Tests document **what the correct behavior is**
- Future developers understand **why code exists**
- Prevents the same debugging session twice

## Quick Commands

```bash
# Development
cd src/Server && dotnet watch run  # Backend with hot reload
npm run dev                         # Frontend with HMR
dotnet test                         # Run tests

# Build
docker build -t app .               # Build image
docker-compose up -d                # Deploy with Tailscale

# Deploy Rules to Live Database
./scripts/deploy-rules.sh --list              # List available budgets
./scripts/deploy-rules.sh "My Budget"         # Add new rules
./scripts/deploy-rules.sh "My Budget" --clear # Clear all & reimport
```

### Deploy Rules Script

The `deploy-rules.sh` script imports categorization rules from `rules.yml` to the live Docker database:

1. Stops the app container
2. Runs `import-rules.fsx` with `DATA_DIR` pointing to the Docker volume
3. Restarts the app container

**Prerequisites:**
- `.env` with `YNAB_TOKEN`
- Docker container running
- Volume mounted at `~/my_apps/budgetbuddy/`

## README-Deltas (Agentheim)

Vor dem Integrieren eines README_DELTA lokal gegen das echte Grammatik-Modul prüfen: `npm run check:readme-delta -- --bc <kontext> --delta <datei|->` (direkt: `node scripts/readme-delta-check.mjs --bc <kontext> --delta <datei|->`).
Ein rotes Gate heißt: den README-Block von Hand platzieren, nicht das Delta blind anwenden.
Exit 1: das Delta wurde inhaltlich abgelehnt, Grund in der Meldung.
Exit 2: das Gate konnte gar nicht urteilen (Plugin-Modul nicht gefunden, Eingabe ungültig), das ist etwas anderes als "abgelehnt".
Umgehung bei einem per replace nicht erreichbaren Bullet: auf das nächstgelegene Spalte-0-Bullet mit fettem Kopf ankern und den ganzen Extent in expected und body mit exakter Einrückung neu ausgeben.
Diese Aufruf-Regel ("vor dem Integrieren durch das Gate") ist prose-only und nicht mechanisch erzwungen (ADR 0059): die Aufrufstelle liegt im Conductor-Plugin-Code, außerhalb dieses Repos.
Erzwungen ist nur das Gate selbst (`npm run test:scripts`) und die Kopplung von Skriptpfad und Text in dieser Datei.

## Verification Checklist

Before marking a feature complete:

- [ ] Types defined in `src/Shared/Domain.fs`
- [ ] API contract in `src/Shared/Api.fs`
- [ ] Validation in `src/Server/Validation.fs`
- [ ] Domain logic is pure (no I/O) in `src/Server/Domain.fs`
- [ ] Persistence in `src/Server/Persistence.fs`
- [ ] API implementation in `src/Server/Api.fs`
- [ ] Frontend state in `src/Client/State.fs`
- [ ] Frontend view in `src/Client/View.fs`
- [ ] Tests written (at minimum: domain + validation)
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] **Invoke qa-milestone-reviewer agent** to verify test quality
- [ ] **Address any missing tests** identified by the reviewer

## Tech Stack Reference

| Layer | Technology |
|-------|------------|
| Frontend | Elmish.React + Feliz |
| Styling | TailwindCSS 4.3 (kein DaisyUI, ADR 0031) |
| Build | Vite + fable-plugin |
| Backend | Giraffe + Fable.Remoting |
| Database | SQLite + Dapper |
| Tests | Expecto |
| Runtime | .NET 8+ |
| Deployment | Docker + Tailscale |

## Design System Components

> **Styleguide (das Gate):** `standards/frontend/styleguide.md` is the single source of
> truth for visual language, color semantics, the token layer, patterns, motion and voice.
> All UI work is measured against it; this section is the API quick-reference for the
> components it inventories.
>
> **Living visual styleguide:** the in-app route **`/styleguide`** (discreet link in the
> Settings page, also reachable directly by URL) renders the **real** DS components and
> tokens as a gallery — the visual counterpart to the written styleguide. Source:
> `src/Client/Components/Styleguide/View.fs`. Because it renders the actual components, it
> cannot drift from them.

The project includes a complete F# Design System in `src/Client/DesignSystem/`. **Always use these components** instead of inline Feliz code for UI.

### Component Quick Reference

| Component | Import | Usage |
|-----------|--------|-------|
| Button | `Client.DesignSystem.Button` | `Button.primary "Save" onClick` |
| Card | `Client.DesignSystem.Card` | `Card.standard [ ... children ... ]` |
| Badge | `Client.DesignSystem.Badge` | `Badge.success "Active"` |
| Input | `Client.DesignSystem.Input` | `Input.text props` |
| Modal | `Client.DesignSystem.Modal` | `Modal.view props [ ... ]` |
| Toast | `Client.DesignSystem.Toast` | `Toast.renderList toasts onDismiss` |
| Stats | `Client.DesignSystem.Stats` | `Stats.view props` |
| Money | `Client.DesignSystem.Money` | `Money.view props` |
| Table | `Client.DesignSystem.Table` | `Table.view props [ ... ]` |
| Loading | `Client.DesignSystem.Loading` | `Loading.spinner MD Teal` |
| ErrorDisplay | `Client.DesignSystem.ErrorDisplay` | `ErrorDisplay.card "Error" onRetry` |
| Icons | `Client.DesignSystem.Icons` | `Icons.check MD Green` |
| Navigation | `Client.DesignSystem.Navigation` | (used in main View.fs) |
| PageHeader | `Client.DesignSystem.PageHeader` | `PageHeader.withActions title subtitle actions` |
| Primitives | `Client.DesignSystem.Primitives` | `Primitives.container [ ... ]` |
| Tokens | `Client.DesignSystem.Tokens` | `Tokens.Colors.neonGreen` |

### Button Examples

```fsharp
open Client.DesignSystem.Button

// Primary (orange glow)
Button.primary "Save" (fun () -> dispatch Save)

// With loading state
Button.primaryLoading "Saving..." isLoading (fun () -> dispatch Save)

// With icon
Button.primaryWithIcon "Add" (Icons.plus SM Primary) (fun () -> dispatch Add)

// Secondary (teal outline)
Button.secondary "Cancel" (fun () -> dispatch Cancel)

// Ghost (transparent)
Button.ghost "Skip" (fun () -> dispatch Skip)

// Danger (red)
Button.danger "Delete" (fun () -> dispatch Delete)

// Full-width (mobile forms)
Button.primaryFullWidth "Submit" (fun () -> dispatch Submit)

// Hero button (large CTA with prominent glow)
Button.hero "Get Started" (fun () -> dispatch Start)

// Hero with icon
Button.heroWithIcon "Start Sync" (Icons.sync MD Primary) (fun () -> dispatch StartSync)

// Hero with loading state
Button.heroLoading "Processing..." isLoading (fun () -> dispatch Process)

// Teal hero variant
Button.heroTeal "Continue" (fun () -> dispatch Continue)

// Button group
Button.group [
    Button.secondary "Cancel" (fun () -> dispatch Cancel)
    Button.primary "Confirm" (fun () -> dispatch Confirm)
]
```

### Card Examples

```fsharp
open Client.DesignSystem.Card

// Standard card
Card.standard [
    Card.headerSimple "Card Title"
    Card.body [
        Html.p [ prop.text "Content here" ]
    ]
]

// Glass card (blur effect)
Card.glass [ ... ]

// Glow card (neon border)
Card.glow [ ... ]

// Card with accent line
Card.withAccent [ ... ]

// Empty state card
Card.emptyState
    (Icons.inbox XL Default)
    "No items"
    "Get started by adding your first item."
    (Some (Button.primary "Add Item" onClick))
```

### Input Examples

```fsharp
open Client.DesignSystem.Input

// Simple text input
Input.textSimple value onChange "Enter name..."

// Input group with label and error
Input.group
    "Email"
    true  // required
    (Input.text { Input.textInputDefaults with
        Value = model.Email
        OnChange = fun v -> dispatch (SetEmail v)
        Placeholder = "email@example.com"
        State = if hasError then Error "Invalid email" else Normal })

// Select dropdown
Input.selectSimple value onChange [
    "", "Select option..."
    "a", "Option A"
    "b", "Option B"
]

// Toggle switch
Input.toggle isChecked onChange "Enable feature"

// Form section
Input.formSection "Settings" [
    Input.groupSimple "Name" (Input.textSimple name setName "")
    Input.groupSimple "Email" (Input.textSimple email setEmail "")
]
```

### Badge Examples

```fsharp
open Client.DesignSystem.Badge

// Semantic badges
Badge.success "Active"
Badge.warning "Pending"
Badge.error "Failed"
Badge.info "New"

// Status badges (for transactions)
Badge.imported ()
Badge.pendingReview ()
Badge.autoCategorized ()
Badge.uncategorized ()

// Count badge
Badge.count 5
```

### Modal Examples

```fsharp
open Client.DesignSystem.Modal

// Simple modal
Modal.simple model.IsOpen "Edit Item" (fun () -> dispatch CloseModal) [
    Modal.body [
        Input.groupSimple "Name" (Input.textSimple ...)
    ]
    Modal.footer [
        Button.secondary "Cancel" (fun () -> dispatch CloseModal)
        Button.primary "Save" (fun () -> dispatch Save)
    ]
]

// Confirmation dialog
Modal.confirm
    isOpen
    "Delete Item?"
    "This action cannot be undone."
    "Delete"
    (fun () -> dispatch ConfirmDelete)
    (fun () -> dispatch CancelDelete)

// Full-screen modal (mobile)
Modal.fullScreen isOpen "Full Title" onClose [ ... ]
```

### Money Display

```fsharp
open Client.DesignSystem.Money

// Simple amount
Money.simple amount

// Large display
Money.large amount

// With label
Money.withLabel "Balance" amount

// Balance display (hero size, glow)
Money.balance amount currency
```

### Stats Cards

```fsharp
open Client.DesignSystem.Stats

// With icon
Stats.withIcon (Icons.chart MD Teal) "Transactions" "1,234"

// With trend
Stats.withTrend "Revenue" "$12,345" (Some (Trend.Up 12.5))

// Specialized stats
Stats.transactionCount 150
Stats.syncCount 42

// Stats grid
Stats.grid [
    Stats.withIcon icon1 "Label 1" "Value 1"
    Stats.withIcon icon2 "Label 2" "Value 2"
    Stats.withIcon icon3 "Label 3" "Value 3"
]
```

### Loading States

```fsharp
open Client.DesignSystem.Loading

// Spinner
Loading.spinner MD Teal

// Centered loading (full container)
Loading.centered (Loading.spinner LG Teal) "Loading data..."

// Neon pulse animation
Loading.neonPulse "Processing..."

// Skeleton loaders
Loading.skeleton Line Normal
Loading.tableSkeleton 5 3  // 5 rows, 3 columns
```

### Error Display

```fsharp
open Client.DesignSystem.ErrorDisplay

// Inline error (for form validation)
ErrorDisplay.inline' "Invalid email address"

// Compact card (for inline contexts)
ErrorDisplay.cardCompact "Failed to load data" None
ErrorDisplay.cardCompact "Failed to save" (Some (fun () -> dispatch Retry))

// Standard error card
ErrorDisplay.card "Connection failed" (Some (fun () -> dispatch Retry))
ErrorDisplay.cardWithTitle "Error loading rules" error (Some (fun () -> dispatch LoadRules))

// Hero error (for major operation failures)
ErrorDisplay.hero "Sync Failed" error "Try Again" (Icons.sync Icons.SM Icons.Primary) (fun () -> dispatch StartSync)
ErrorDisplay.heroSimple "Operation Failed" "Please try again later" (fun () -> dispatch Retry)

// Full-page error
ErrorDisplay.fullPage "Something went wrong" "We couldn't load the page" (Some (fun () -> dispatch Reload))

// For RemoteData.Failure
ErrorDisplay.forRemoteData error (fun () -> dispatch Reload)

// Warning style (yellow instead of red)
ErrorDisplay.warning "This action cannot be undone" None
```

### Design Tokens

```fsharp
open Client.DesignSystem.Tokens

// Use tokens for consistent styling
Html.div [
    prop.className $"{Colors.neonTeal} {Fonts.mono} {Spacing.md}"
    prop.children [ ... ]
]

// Preset combinations
Html.h1 [
    prop.className Presets.pageHeader
    prop.text "Page Title"
]
```

### Layout Primitives

```fsharp
open Client.DesignSystem.Primitives

// Container (max-width, padding)
Primitives.container [
    Primitives.pageHeader "Dashboard"
    Primitives.stack [ ... ]  // Vertical stack
]

// Grid layouts
Primitives.grid2 [ col1; col2 ]
Primitives.grid3 [ col1; col2; col3 ]

// Responsive visibility
Primitives.mobileOnly [ ... ]
Primitives.desktopOnly [ ... ]
```

### Icons

```fsharp
open Client.DesignSystem.Icons

// Size: XS, SM, MD, LG, XL
// Color: Default, Teal, Green, Orange, Purple, Pink, Red, Error, Primary

Icons.check MD Green
Icons.x SM Default
Icons.plus MD Primary
Icons.trash SM Error
Icons.edit MD Default
Icons.settings LG Default
Icons.sync MD Teal
Icons.chart MD Orange
Icons.calendar MD Purple
```

### PageHeader Examples

```fsharp
open Client.DesignSystem.PageHeader

// Simple header with just title
PageHeader.simple "Dashboard"

// Header with subtitle
PageHeader.withSubtitle "Settings" "Configure your connections and preferences."

// Header with gradient title (neon-teal to neon-green)
PageHeader.gradient "Categorization Rules"

// Gradient header with subtitle
PageHeader.gradientWithSubtitle "Categorization Rules" "Automate transaction categorization."

// Header with action buttons
PageHeader.withActions
    "Settings"
    (Some "Configure your connections and preferences.")
    [
        Button.view {
            Button.defaultProps with
                Text = ""
                OnClick = fun () -> dispatch Refresh
                Variant = Button.Ghost
                Icon = Some (Icons.sync Icons.SM Icons.Default)
        }
    ]

// Gradient header with actions
PageHeader.gradientWithActions
    "Categorization Rules"
    (Some "Automate transaction categorization.")
    [
        Button.ghost "Refresh" (fun () -> dispatch Refresh)
        Button.primaryWithIcon "Add Rule" (Icons.plus SM Primary) (fun () -> dispatch AddRule)
    ]
```
