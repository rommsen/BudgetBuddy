# ⚠️ CRITICAL: SERENA TOOLS ARE MANDATORY ⚠️

**STOP** before using `Read`, `Grep`, or `Glob` on code files (.fs, .fsx, .fsproj).

**USE SERENA INSTEAD** - this is NON-NEGOTIABLE for this project.

| Instead of... | Use Serena... |
|---------------|---------------|
| `Read` on .fs files | `get_symbols_overview` or `find_symbol` with `include_body=True` |
| `Grep` for code search | `search_for_pattern` |
| `Glob` for finding files | `find_file` or `list_dir` |
| Manual edits with `Edit` | `replace_symbol_body`, `insert_before_symbol`, `insert_after_symbol` |

**FORBIDDEN for .fs/.fsx files:**
- ❌ `Read` tool (use `find_symbol` with `include_body=True`)
- ❌ `Grep` tool (use `search_for_pattern`)
- ❌ `Edit` tool for symbol changes (use `replace_symbol_body`)

If you catch yourself reaching for Read/Grep/Glob/Edit on F# code, **STOP and use Serena**.

---

# F# Full-Stack Blueprint - Claude Code Instructions

## Your Role

You are developing F# web applications using this blueprint. The codebase uses Elmish.React + Feliz (frontend), Giraffe + Fable.Remoting (backend), SQLite (persistence), and deploys via Docker + Tailscale.

## Tool Priority: Serena First (MANDATORY)

**ALWAYS use Serena's MCP tools when possible.** Serena provides semantic code analysis that is more efficient and accurate than reading entire files.

### When to Use Serena

- **Exploring code**: Use `get_symbols_overview`, `find_symbol`, `find_referencing_symbols`
- **Searching patterns**: Use `search_for_pattern` instead of Grep
- **Editing code**: Use `replace_symbol_body`, `insert_before_symbol`, `insert_after_symbol`
- **Renaming**: Use `rename_symbol` for codebase-wide refactoring

### When NOT to Use Serena

- Reading non-code files (markdown, JSON, config)
- When you need the entire file context
- For files Serena doesn't support

### Benefits

- **Token-efficient**: Only reads what's needed, not entire files
- **Semantic accuracy**: Understands code structure, not just text
- **Safe refactoring**: `rename_symbol` updates all references correctly

### Example Workflow

```
1. get_symbols_overview("src/Server/Api.fs")     → See file structure
2. find_symbol("myFunction", include_body=True)  → Read specific function
3. replace_symbol_body(...)                       → Edit just that function
```

**Do NOT read entire files unless absolutely necessary!**

## Before Implementing Anything

**Always read the relevant documentation first:**

1. Check `standards/global/quick-reference.md` for code templates
2. Read the specific guide for your task (see Documentation Map below)
3. Look at existing code to match patterns

## Documentation Map

| Task | Read This First |
|------|-----------------|
| Complete new feature | `standards/global/quick-reference.md` + specific guides below |
| Domain types/API contracts | `standards/shared/types.md`, `standards/shared/api-contracts.md` |
| Frontend (UI, state) | `standards/frontend/overview.md`, `standards/frontend/state-management.md` |
| Backend (API, logic) | `standards/backend/overview.md`, `standards/backend/api-implementation.md` |
| Database/files | `standards/backend/persistence-sqlite.md`, `standards/backend/persistence-files.md` |
| Tests | `standards/testing/overview.md`, `standards/testing/domain-tests.md` |
| Docker/deployment | `standards/deployment/docker.md`, `standards/deployment/production.md` |
| Tailscale networking | `standards/deployment/tailscale.md` |
| Architecture overview | `standards/global/architecture.md` |

## Using Skills

Skills orchestrate workflows and reference `standards/` for detailed patterns. Invoke based on the task:

### Core Workflow Skills

| Skill | When to Use | Standards Referenced |
|-------|-------------|---------------------|
| `fsharp-feature` | Complete feature implementation (orchestrates all layers) | `standards/global/development-workflow.md` |
| `fsharp-backend` | Backend implementation (validation, domain, persistence, API) | `standards/backend/*.md` |
| `fsharp-frontend` | Elmish state and Feliz views | `standards/frontend/*.md` |
| `fsharp-shared` | Defining types and API contracts in `src/Shared/` | `standards/shared/*.md` |
| `fsharp-tests` | Writing Expecto tests | `standards/testing/*.md` |

### Specialized Skills

| Skill | When to Use | Standards Referenced |
|-------|-------------|---------------------|
| `fsharp-validation` | Input validation patterns | `standards/shared/validation.md` |
| `fsharp-persistence` | Database tables, file storage | `standards/backend/persistence-*.md` |
| `fsharp-remotedata` | Async state handling with RemoteData pattern | `standards/frontend/remotedata.md` |
| `fsharp-routing` | URL routing and navigation | `standards/frontend/routing.md` |
| `fsharp-error-handling` | Result types and error propagation | `standards/backend/error-handling.md` |
| `fsharp-property-tests` | FsCheck property-based testing | `standards/testing/property-tests.md` |
| `fsharp-docker` | Multi-stage Docker builds | `standards/deployment/docker.md` |
| `tailscale-deploy` | Docker + Tailscale deployment | `standards/deployment/*.md` |

## Implementing User Specifications

When the user provides a specification file (markdown describing a feature):

1. **Read the specification file** thoroughly
2. **Read `standards/global/quick-reference.md`** for patterns
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

## Milestone & Backlog Tracking

**IMPORTANT**: Die folgenden Qualitätskriterien gelten für ALLE Implementierungen:
- Milestones aus `/docs/MILESTONE-PLAN.md`
- Features und Bugs aus `/backlog.md`
- Jede andere signifikante Code-Änderung

### Qualitätskriterien (MANDATORY)

Bei jeder Feature-Implementierung oder Bug-Fix MÜSSEN folgende Schritte eingehalten werden:

1. **QA Review**: Nach Abschluss den **qa-milestone-reviewer** Agent aufrufen
2. **Tests**: Alle neuen Features/Fixes müssen Tests haben
3. **Diary**: Development Diary aktualisieren (`diary/development.md`)
4. **Build & Test**: `dotnet build` und `dotnet test` müssen erfolgreich sein
5. **Backlog Update**: Erledigte Items in `/backlog.md` abhaken mit Datum

### Milestone-spezifisch

Wenn du Milestones aus `/docs/MILESTONE-PLAN.md` implementierst:

1. **After completing each milestone**, you MUST:
   a. Invoke the **qa-milestone-reviewer** agent using the Task tool to verify test quality and coverage
   b. Update `/docs/MILESTONE-PLAN.md` based on the QA review results
2. Mark the milestone's verification checklist items as complete: `- [x]`
3. Add a completion section with:
   - `### ✅ Milestone N Complete (YYYY-MM-DD)`
   - **Summary of Changes**: List all modifications made
   - **Test Quality Review**: Summary from qa-milestone-reviewer agent
   - **Notes**: Any important observations or deviations from the plan

### Backlog-spezifisch

Wenn du Features oder Bugs aus `/backlog.md` implementierst:

1. **Vor der Implementierung**: Lies das Backlog-Item sorgfältig
2. **Nach der Implementierung**:
   a. Invoke den **qa-milestone-reviewer** Agent für Test-Coverage
   b. Markiere das Item als erledigt: `- [x] **Feature Name** ... ✅ (YYYY-MM-DD)`
   c. Verschiebe abgeschlossene Items in den "Abgeschlossen" Abschnitt
3. **Diary Entry**: Dokumentiere was implementiert wurde

### QA Review Process

Nach jeder Implementierung und vor dem Abschluss, ALWAYS use:
```
Task tool with subagent_type='qa-milestone-reviewer'
```
This agent will:
- Verify tests are meaningful and don't test tautologies
- Ensure all important behavior is covered by tests
- Identify any missing test coverage
- Define missing tests (implementation done by red-test-fixer agent if needed)

### Beispiel: Milestone Completion

```markdown
### Verification
- [x] All verification items completed
- [x] QA Milestone Reviewer invoked

### ✅ Milestone 0 Complete (2025-11-29)

**Summary of Changes:**
- Added required NuGet packages
- Fixed code warnings
- Verified builds succeed

**Test Quality Review:**
- All tests verified by qa-milestone-reviewer agent
- Test coverage is adequate for milestone scope
- No tautological tests found

**Notes**: Server already had most structure in place.
```

### Beispiel: Backlog Item Completion

In `/backlog.md`:
```markdown
## Abgeschlossen

- [x] **Optionale Comdirect-PIN**: On-Demand PIN-Abfrage implementiert ✅ (2025-12-08)
```

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
5. **Document in diary** - Include what test was added

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

## Development Diary

**CRITICAL**: After ANY meaningful code changes (more than a few characters), you MUST update `diary/development.md`.

### When to Write Diary Entries

Write an entry whenever you:
- Add, modify, or delete files
- Implement new features or fix bugs
- Refactor code
- Add or modify tests
- Fix build errors or warnings
- Update dependencies or configuration

### What to Include in Diary Entries

Each entry should contain:

1. **Date and timestamp** (YYYY-MM-DD HH:MM)
2. **What you did** - Brief summary of the changes
3. **Files added** - List new files created
4. **Files modified** - List files changed with brief description of changes
5. **Files deleted** - List files removed
6. **Rationale** - Why these changes were necessary
7. **Outcomes** - Build status, test results, any issues encountered

### Diary Entry Format

```markdown
## YYYY-MM-DD HH:MM - [Brief Title]

**What I did:**
[Concise description of the changes]

**Files Added:**
- `path/to/new/file.fs` - [Purpose of this file]

**Files Modified:**
- `path/to/modified/file.fs` - [What changed and why]

**Files Deleted:**
- `path/to/deleted/file.fs` - [Why it was removed]

**Rationale:**
[Why these changes were necessary]

**Outcomes:**
- Build: ✅/❌
- Tests: X/Y passed
- Issues: [Any problems encountered]

---
```

### Example Entry

```markdown
## 2025-11-29 14:30 - Fixed Persistence TypeHandler for F# Options

**What I did:**
Fixed critical bugs in Persistence.fs that prevented Dapper from working with F# option types and added comprehensive test coverage for encryption and type conversions.

**Files Added:**
- `src/Tests/EncryptionTests.fs` - Tests for AES-256 encryption/decryption
- `src/Tests/PersistenceTypeConversionTests.fs` - Tests for all type conversions

**Files Modified:**
- `src/Server/Persistence.fs` - Added OptionHandler<'T> for Dapper, added [<CLIMutable>] to all Row types
- `src/Tests/Tests.fsproj` - Added new test files to compilation
- `src/Tests/YnabClientTests.fs` - Removed tautological tests

**Files Deleted:**
- `src/Tests/MathTests.fs` - Tautological tests with no value

**Rationale:**
QA review identified critical gaps in test coverage and revealed that Persistence.fs had bugs preventing it from working at all. Dapper couldn't handle F# option types without a custom TypeHandler.

**Outcomes:**
- Build: ✅
- Tests: 59/59 passed (was 39/59 before fixes)
- Issues: None

---
```

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
- [ ] **Development diary updated** in `diary/development.md`
- [ ] **If working on a milestone:**
  - [ ] **Invoke qa-milestone-reviewer agent** to verify test quality
  - [ ] **Address any missing tests** identified by the reviewer
  - [ ] **Update `/docs/MILESTONE-PLAN.md`** with completion status and QA review summary

## Tech Stack Reference

| Layer | Technology |
|-------|------------|
| Frontend | Elmish.React + Feliz |
| Styling | TailwindCSS 4.3, DaisyUI |
| Build | Vite + fable-plugin |
| Backend | Giraffe + Fable.Remoting |
| Database | SQLite + Dapper |
| Tests | Expecto |
| Runtime | .NET 8+ |
| Deployment | Docker + Tailscale |

## Design System Components

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
Badge.imported
Badge.pendingReview
Badge.autoCategorized
Badge.uncategorized

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
