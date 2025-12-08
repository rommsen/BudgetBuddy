# F# Full-Stack Blueprint - Claude Code Instructions

## Your Role

You are developing F# web applications using this blueprint. The codebase uses Elmish.React + Feliz (frontend), Giraffe + Fable.Remoting (backend), SQLite (persistence), and deploys via Docker + Tailscale.

## Before Implementing Anything

**Always read the relevant documentation first:**

1. Check `/docs/09-QUICK-REFERENCE.md` for code templates
2. Read the specific guide for your task (see Documentation Map below)
3. Look at existing code to match patterns

## Documentation Map

| Task | Read This First |
|------|-----------------|
| Complete new feature | `/docs/09-QUICK-REFERENCE.md` + specific guides below |
| Domain types/API contracts | `/docs/04-SHARED-TYPES.md` |
| Frontend (UI, state) | `/docs/02-FRONTEND-GUIDE.md` |
| Backend (API, logic) | `/docs/03-BACKEND-GUIDE.md` |
| Database/files | `/docs/05-PERSISTENCE.md` |
| Tests | `/docs/06-TESTING.md` |
| Docker/deployment | `/docs/07-BUILD-DEPLOY.md` |
| Tailscale networking | `/docs/08-TAILSCALE-INTEGRATION.md` |
| Architecture overview | `/docs/00-ARCHITECTURE.md` |

## Using Skills

Skills provide focused guidance. Invoke them based on the task:

| Skill | When to Use |
|-------|-------------|
| `fsharp-feature` | Complete feature implementation (orchestrates all layers) |
| `fsharp-shared` | Defining types and API contracts in `src/Shared/` |
| `fsharp-backend` | Backend implementation (validation, domain, persistence, API) |
| `fsharp-validation` | Input validation patterns |
| `fsharp-persistence` | Database tables, file storage, event sourcing |
| `fsharp-frontend` | Elmish state and Feliz views |
| `fsharp-tests` | Writing Expecto tests |
| `tailscale-deploy` | Docker + Tailscale deployment |

## Implementing User Specifications

When the user provides a specification file (markdown describing a feature):

1. **Read the specification file** thoroughly
2. **Read `/docs/09-QUICK-REFERENCE.md`** for patterns
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
```

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
| Icons | `Client.DesignSystem.Icons` | `Icons.check MD Green` |
| Navigation | `Client.DesignSystem.Navigation` | (used in main View.fs) |
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
