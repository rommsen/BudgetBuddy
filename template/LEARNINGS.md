# F# Full-Stack Development Learnings

This document captures reusable insights, patterns, and lessons learned from developing F# full-stack applications. These learnings are technology-agnostic where possible and can be applied to future projects.

---

## 1. Architecture Decisions

### Why This Stack Works

| Layer | Choice | Rationale |
|-------|--------|-----------|
| Frontend | Elmish.React + Feliz | MVU guarantees predictable state, F# type safety catches bugs at compile time |
| Backend | Giraffe + Fable.Remoting | Type-safe RPC eliminates API contract drift between client/server |
| Database | SQLite + Dapper | Simple, embedded, no infrastructure - perfect for single-user apps |
| Networking | Tailscale | Zero-config private networking, eliminates auth complexity |

### Key Architectural Principles

1. **Type Safety End-to-End**: Define ALL types in `src/Shared/`. Fable.Remoting ensures compile-time checking of API contracts - no manual JSON serialization.

2. **Pure Domain Logic**: `src/Server/Domain.fs` must have NO I/O operations. All side effects go in `Persistence.fs` or `Api.fs`. This makes domain logic trivially testable.

3. **Validate at Boundaries**: Validate input at API entry points BEFORE any processing. Return clear error messages. Never trust client data.

4. **Development Order**: Follow this sequence for new features:
   ```
   Shared/Domain.fs → Shared/Api.fs → Server/Validation.fs →
   Server/Domain.fs → Server/Persistence.fs → Server/Api.fs →
   Client/State.fs → Client/View.fs → Tests/
   ```

### RemoteData Pattern (Critical for Frontend)

```fsharp
type RemoteData<'T> =
    | NotAsked
    | Loading
    | Success of 'T
    | Failure of string
```

**Use this for ALL async operations in frontend state.** It makes impossible states unrepresentable and forces explicit handling of loading/error states.

---

## 2. Patterns That Work

### Domain Layer

**Pure Functions Only**
```fsharp
// GOOD - Pure transformation
let completeTodo (item: TodoItem) : TodoItem =
    { item with Status = Completed; UpdatedAt = DateTime.UtcNow }

// BAD - I/O in domain
let completeTodo itemId =
    let item = Persistence.getItem itemId  // NO!
    { item with Status = Completed }
```

**Use Discriminated Unions for State Variations**
```fsharp
type TransactionStatus =
    | Pending
    | Imported
    | Skipped
    | ManualCategorized
    | AutoCategorized
```

### Backend Layer

**API Orchestration Pattern**
```fsharp
let api : IEntityApi = {
    save = fun entity -> async {
        // 1. Validate
        match Validation.validate entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            // 2. Domain logic (pure)
            let processed = Domain.process valid
            // 3. Persist
            do! Persistence.save processed
            return Ok processed
    }
}
```

**Error Accumulation for Validation**
```fsharp
let validateEntity entity : Result<Entity, string list> =
    let errors = [
        validateRequired "Title" entity.Title
        validateLength "Title" 1 100 entity.Title
        validateRequired "Email" entity.Email
    ] |> List.choose id

    if errors.IsEmpty then Ok entity else Error errors
```

### Frontend Layer

**MVU State Changes Through Messages**
```fsharp
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

**Optimistic UI Updates**
```fsharp
// Update local state IMMEDIATELY, then call API
| CategorizeTransaction (txId, categoryId) ->
    let updatedModel =
        updateTransactionLocally model txId categoryId
    updatedModel, Cmd.OfAsync.either Api.api.categorize (txId, categoryId) ...
```

**Version-Based Debouncing**
```fsharp
type Model = {
    PendingVersions: Map<TransactionId, int>
}

// Increment version on each change, only commit if version matches
| CommitChange (id, value, version) when model.PendingVersions.[id] = version ->
    // Actually save - this is the latest version
```

### Testing

**Property-Based Tests for Complex Logic**
```fsharp
testProperty "roundtrip serialization" <| fun (entity: Entity) ->
    let serialized = serialize entity
    let deserialized = deserialize serialized
    entity = deserialized
```

**Test Actual Behavior, Not Tautologies**
```fsharp
// BAD - Tautology
testCase "entity exists" <| fun _ ->
    let entity = { Id = 1 }
    Expect.equal entity.Id 1 "Should be 1"  // Tests nothing!

// GOOD - Tests actual behavior
testCase "validation normalizes email" <| fun _ ->
    let result = Validation.validate { Email = "  TEST@EXAMPLE.COM  " }
    match result with
    | Ok v -> Expect.equal v.Email "test@example.com" "Should normalize"
    | Error _ -> failtest "Should succeed"
```

---

## 3. Anti-Patterns & Mistakes

### Critical Bugs Found in Production

| Bug | Root Cause | Lesson |
|-----|------------|--------|
| JSON encoding as string not number | `Encode.int64` outputs strings in JS | Always test JSON output format, use `Encode.int` for safe range |
| Stale reference in mutable state | Captured old value, then mutated state | Re-read mutable state after modifications |
| Format mismatch in ID matching | `BB:` vs `BUDGETBUDDY:` prefixes | Centralize format constants, use shared functions |
| Encryption key changes with Docker | Key derived from hostname | Use environment variable for stable encryption key |
| Database not initialized on deploy | `initializeDatabase()` never called | Add explicit initialization in `Program.fs` at startup |

### Design Anti-Patterns to Avoid

1. **I/O in Domain.fs** - Keep domain logic pure. No database calls, no file I/O.

2. **Ignoring Result Types** - Always handle errors explicitly. Don't use `.Value` on Options or Results.

3. **Classes for Domain Types** - Use records and discriminated unions, not classes.

4. **Skipping Validation** - Validate at API boundary before any processing.

5. **Tests Writing to Production Database** - Always use in-memory SQLite (`Data Source=:memory:`) for tests.

6. **Bug Fixes Without Regression Tests** - Every bug fix MUST include a test that would have caught it.

7. **Hover-Only Actions** - Mobile users can't hover. Always provide visible interaction points.

8. **Selection + Skip Dual Mechanisms** - Confusing UI. Pick one model: either selection OR status-based filtering.

### Fable/JavaScript Gotchas

```fsharp
// Fable transpiles :F2 incorrectly
let bad = sprintf "%.2f" amount  // May produce weird output

// Use explicit rounding instead
let good = System.Math.Round(float amount, 2).ToString("0.00")
```

```fsharp
// int64 serializes to STRING in JavaScript!
Encode.int64 value  // Produces: "12345" (string with quotes)
Encode.int value    // Produces: 12345 (number without quotes)
```

---

## 4. Claude Code Integration

### Effective Skills Structure

Skills are specialized knowledge modules that Claude Code activates based on context:

```
.claude/skills/
├── fsharp-feature/    # Orchestrates end-to-end feature development
├── fsharp-shared/     # Types and API contracts in src/Shared/
├── fsharp-backend/    # Validation → Domain → Persistence → API
├── fsharp-frontend/   # Elmish state and Feliz views
├── fsharp-validation/ # Input validation patterns
├── fsharp-persistence/# Database and file operations
├── fsharp-tests/      # Expecto testing patterns
└── tailscale-deploy/  # Docker + Tailscale deployment
```

**Each Skill Contains:**
- YAML frontmatter (name, description, allowed-tools)
- When to Use section
- Complete working code examples
- Best practices and anti-patterns
- Verification checklists

### Agent Workflows That Work

**qa-milestone-reviewer**: Invoked after implementing features to verify test quality
- Reviews for tautological tests
- Checks for missing coverage
- Defines (but doesn't implement) missing tests
- Hands off to red-test-fixer for implementation

**red-test-fixer**: Fixes failing tests
- Diagnoses root cause (implementation bug vs test bug)
- Applies minimal fix
- Maintains functional programming principles
- Verifies no regressions

### CLAUDE.md Best Practices

1. **Tool Priority Section**: Explicitly state which tools to use (e.g., "Use Serena MCP for F# code, not Read/Grep")

2. **Development Order**: Document the exact sequence for implementing features

3. **Documentation Map**: Map tasks to specific documentation files

4. **Verification Checklists**: Provide checkboxes for each layer of implementation

5. **Anti-Patterns Section**: List specific mistakes to avoid with examples

6. **Bug Fix Protocol**: Require regression tests for every bug fix

7. **Development Diary**: Require documentation of all changes

---

## 5. Tool Integration

### Serena MCP for F# Code Analysis

**Use Serena Instead of Read/Grep/Glob for .fs files:**

| Instead of... | Use Serena... |
|---------------|---------------|
| `Read` on .fs files | `get_symbols_overview` or `find_symbol` with `include_body=True` |
| `Grep` for code search | `search_for_pattern` |
| `Glob` for finding files | `find_file` or `list_dir` |
| Manual `Edit` on symbols | `replace_symbol_body`, `insert_before_symbol`, `insert_after_symbol` |

**Benefits:**
- Token-efficient: Only reads what's needed
- Semantic accuracy: Understands code structure
- Safe refactoring: `rename_symbol` updates all references

**Serena Setup Requirements:**
- Requires `.sln` solution file (F# language server needs it)
- Simple `languages: - fsharp` in project.yml
- Restart Claude Code after configuration changes

### Browser DevTools MCP

Useful for testing frontend:
- `take_snapshot`: Get accessibility tree for UI verification
- `click`, `fill`: Automate UI interactions
- `list_console_messages`: Check for JavaScript errors
- `list_network_requests`: Verify API calls

---

## 6. Development Process

### Milestone-Based Development

**Structure:**
1. Define milestones in `/docs/MILESTONE-PLAN.md`
2. Each milestone has verification checklist
3. After completion:
   - Invoke qa-milestone-reviewer agent
   - Update milestone with completion date
   - Document test quality review results

**Milestone Completion Template:**
```markdown
### Verification
- [x] All verification items completed
- [x] QA Milestone Reviewer invoked

### Milestone N Complete (YYYY-MM-DD)

**Summary of Changes:**
- [List modifications]

**Test Quality Review:**
- [Summary from qa-milestone-reviewer]

**Notes:** [Observations or deviations]
```

### QA Process

1. **After Every Feature/Bug Fix:**
   - Invoke qa-milestone-reviewer agent
   - Address identified test gaps
   - Run full test suite

2. **Test Quality Criteria:**
   - No tautological tests
   - All domain functions have tests
   - Edge cases covered
   - Error paths tested
   - Assertions verify actual behavior

### Bug Fix Protocol (Mandatory)

1. **Understand the root cause** - Don't just fix symptoms
2. **Write a failing test FIRST** that reproduces the bug
3. **Fix the bug** - Make the test pass
4. **Verify no regressions** - Run full test suite
5. **Document in diary** - Include what test was added

**Test Comment Pattern:**
```fsharp
testCase "amount is serialized as JSON number, not string" <| fun () ->
    // This test prevents regression of the bug where Encode.int64 serialized
    // amounts as strings, causing the API to silently reject data.
    ...
```

### Development Diary

**Update diary after ANY meaningful code changes:**

```markdown
## YYYY-MM-DD HH:MM - [Brief Title]

**What I did:**
[Concise description]

**Files Added/Modified/Deleted:**
- `path/to/file.fs` - [What changed and why]

**Rationale:**
[Why these changes were necessary]

**Outcomes:**
- Build: pass/fail
- Tests: X/Y passed
- Issues: [Problems encountered]
```

---

## 7. Code Templates

### New Feature Template

```fsharp
// 1. src/Shared/Domain.fs
type NewEntity = {
    Id: NewEntityId
    Name: string
    // ... fields
}

// 2. src/Shared/Api.fs
type INewEntityApi = {
    getAll: unit -> Async<NewEntity list>
    getById: NewEntityId -> Async<Result<NewEntity, string>>
    save: NewEntity -> Async<Result<NewEntity, string>>
    delete: NewEntityId -> Async<Result<unit, string>>
}

// 3. src/Server/Validation.fs
let validateNewEntity (entity: NewEntity) : Result<NewEntity, string list> =
    let errors = [
        validateRequired "Name" entity.Name
    ] |> List.choose id
    if errors.IsEmpty then Ok entity else Error errors

// 4. src/Server/Domain.fs (PURE)
let processNewEntity (entity: NewEntity) : NewEntity =
    { entity with Name = entity.Name.Trim() }

// 5. src/Server/Persistence.fs
let getAllEntities () : Async<NewEntity list> = async { ... }
let saveEntity (entity: NewEntity) : Async<NewEntity> = async { ... }

// 6. src/Server/Api.fs
let newEntityApi : INewEntityApi = {
    save = fun entity -> async {
        match Validation.validateNewEntity entity with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.processNewEntity valid
            let! saved = Persistence.saveEntity processed
            return Ok saved
    }
}

// 7. src/Client/Types.fs
type Model = {
    Entities: RemoteData<NewEntity list>
}

type Msg =
    | LoadEntities
    | EntitiesLoaded of Result<NewEntity list, string>

// 8. src/Client/State.fs
let update msg model =
    match msg with
    | LoadEntities ->
        { model with Entities = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> EntitiesLoaded) (fun ex -> Error ex.Message |> EntitiesLoaded)
```

### Dapper with F# Options Template

```fsharp
// Register F# Option type handler for Dapper
type OptionHandler<'T>() =
    inherit SqlMapper.TypeHandler<'T option>()

    override _.SetValue(param, value) =
        param.Value <- match value with Some v -> box v | None -> box DBNull.Value

    override _.Parse(value) =
        if isNull value || value = box DBNull.Value
        then None
        else Some (unbox<'T> value)

// Register handlers at startup
SqlMapper.AddTypeHandler(OptionHandler<string>())
SqlMapper.AddTypeHandler(OptionHandler<int>())

// Row types need [<CLIMutable>] for Dapper
[<CLIMutable>]
type EntityRow = {
    Id: int
    Name: string
    Description: string  // nullable in DB
}
```

### React Key Props Template

```fsharp
// Always add keys to list renderings
for item in items do
    let key = string item.Id
    Html.div [
        prop.key key
        prop.children [
            // ... content
        ]
    ]

// For options in dropdowns
for option in options do
    Html.option [
        prop.key option.Value
        prop.value option.Value
        prop.text option.Label
    ]
```

---

## 8. Checklists

### New Feature Checklist

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
- [ ] Development diary updated
- [ ] QA review completed

### Bug Fix Checklist

- [ ] Root cause identified (not just symptoms)
- [ ] Failing test written FIRST
- [ ] Bug fixed
- [ ] Test passes
- [ ] Full test suite passes (no regressions)
- [ ] Test includes comment explaining prevented bug
- [ ] Development diary updated

### Deployment Checklist

- [ ] All tests pass locally
- [ ] Docker build succeeds
- [ ] Environment variables set (`.env`)
- [ ] Database migrations applied
- [ ] `initializeDatabase()` called at startup
- [ ] Tailscale auth key configured
- [ ] Health check endpoint works
- [ ] Encryption key is stable (not derived from hostname)

### Code Review Checklist

- [ ] No I/O in Domain.fs
- [ ] Validation at API boundaries
- [ ] Result types handled explicitly
- [ ] React keys on all list renderings
- [ ] Parameterized SQL queries (no string concatenation)
- [ ] Async for all I/O operations
- [ ] Error messages are user-friendly
- [ ] Mobile-friendly (no hover-only interactions)

---

## 9. Performance Learnings

### Frontend Performance

**Pre-compute Expensive Operations**
```fsharp
// BAD: Computed 193 x 160 = 30,880 times
for tx in transactions do
    let options = categories |> List.map formatOption  // Slow!

// GOOD: Computed once
let categoryOptions = categories |> List.map formatOption
for tx in transactions do
    renderWithOptions tx categoryOptions
```

**Reduce DOM Nodes for Inactive Elements**
```fsharp
// Don't render interactive components for read-only states
if tx.Status = Skipped then
    Html.span [ prop.text categoryName ]  // Simple text
else
    Input.searchableSelect props  // Full component
```

**Debounce Rapid User Input**
```fsharp
// Version-based debouncing prevents race conditions
| CategoryChanged (txId, categoryId) ->
    let version = model.PendingVersions.TryFind txId |> Option.defaultValue 0 |> (+) 1
    { model with PendingVersions = model.PendingVersions.Add(txId, version) },
    Cmd.OfFunc.delayed 400 (fun () -> CommitCategoryChange (txId, categoryId, version))
```

### Backend Performance

- SQLite in WAL mode for concurrent reads
- Use async for all I/O operations
- Parameterized queries (prepared statements are cached)
- Batch operations where possible

---

## 10. Common Gotchas

| Issue | Symptom | Solution |
|-------|---------|----------|
| F# Options with Dapper | `null` values cause exceptions | Register `OptionHandler<'T>` |
| `[<CLIMutable>]` missing | Dapper can't populate records | Add attribute to row types |
| React keys missing | List re-renders slowly, focus lost | Add `prop.key` to all list items |
| Encryption key in Docker | Settings lost after rebuild | Use env var, not hostname |
| DB not initialized | "no such table" error | Call `initializeDatabase()` at startup |
| Double-click bugs | Race conditions | Add loading state, disable button |
| int64 JSON encoding | Numbers become strings | Use `Encode.int` for API calls |
| Format string in Fable | `:F2` transpiles incorrectly | Use `Math.Round().ToString()` |

---

## Summary

The key insights from this project:

1. **Type safety prevents bugs** - Invest in types upfront, they pay dividends
2. **Pure domain logic is testable** - Keep I/O at the boundaries
3. **Explicit error handling** - Use Result types, never ignore errors
4. **Test actual behavior** - Avoid tautologies, test what matters
5. **Document everything** - Future you will thank present you
6. **Regression tests are mandatory** - Bugs without tests come back
7. **Mobile-first UI** - Desktop users can use mobile UI, not vice versa
8. **Performance matters** - Pre-compute, debounce, reduce DOM
9. **Simple infrastructure** - SQLite + Tailscale eliminates complexity
10. **Milestone-based development** - Structured progress with QA gates
