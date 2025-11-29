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

## Milestone Tracking

**IMPORTANT**: When implementing milestones from `/docs/MILESTONE-PLAN.md`:

1. **After completing each milestone**, you MUST:
   a. Invoke the **qa-milestone-reviewer** agent using the Task tool to verify test quality and coverage
   b. Update `/docs/MILESTONE-PLAN.md` based on the QA review results
2. Mark the milestone's verification checklist items as complete: `- [x]`
3. Add a completion section with:
   - `### ✅ Milestone N Complete (YYYY-MM-DD)`
   - **Summary of Changes**: List all modifications made
   - **Test Quality Review**: Summary from qa-milestone-reviewer agent
   - **Notes**: Any important observations or deviations from the plan
4. This provides a clear audit trail of progress through the implementation plan

**QA Review Process:**
After milestone implementation and before marking complete, ALWAYS use:
```
Task tool with subagent_type='qa-milestone-reviewer'
```
This agent will:
- Verify tests are meaningful and don't test tautologies
- Ensure all important behavior is covered by tests
- Identify any missing test coverage
- Define missing tests (implementation done by red-test-fixer agent if needed)

Example:
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
