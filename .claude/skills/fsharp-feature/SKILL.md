---
name: fsharp-feature
description: |
  Orchestrates end-to-end F# full-stack feature development.
  Use when user requests complete features ("add X", "implement Y").
allowed-tools: Read, Edit, Write, Grep, Glob, Bash, mcp__serena__*
standards:
  - standards/global/development-workflow.md
  - standards/global/quick-reference.md
  - standards/global/architecture.md
---

# F# Full-Stack Feature Development

## When to Use This Skill

Activate when:
- User requests complete new feature ("add todo feature", "implement budgets")
- Building feature from scratch with types, backend, frontend, and tests
- Need structured guidance through entire stack
- Project follows F# full-stack blueprint with Elmish + Giraffe

## Prerequisites

**Read First:**
- `standards/global/development-workflow.md` - Development process
- `standards/global/quick-reference.md` - Code templates
- Project-specific docs in `/docs/` (if any)

**Project Structure:**
```
src/Shared/   - Domain types & API contracts
src/Server/   - Backend (validation, domain, persistence, API)
src/Client/   - Frontend (state, view)
src/Tests/    - Expecto tests
```

## Quick Start Workflow

### Step 1: Define Shared Contracts
**Read:** `standards/global/quick-reference.md#shared-domain-type`
**Edit:** `src/Shared/Domain.fs` and `src/Shared/Api.fs`

Define domain types and API contract:
```fsharp
// Domain.fs
type Item = { Id: int; Name: string; CreatedAt: DateTime }

// Api.fs
type IItemApi = {
    getAll: unit -> Async<Item list>
    save: Item -> Async<Result<Item, string>>
}
```

---

### Step 2: Implement Backend
**Read:** `standards/backend/api-implementation.md`, `standards/backend/domain-logic.md`
**Skills:** Use `fsharp-backend` for detailed backend implementation

**Order:**
1. **Validation** (`Validation.fs`) - Validate input at API boundary
2. **Domain** (`Domain.fs`) - Pure business logic (NO I/O)
3. **Persistence** (`Persistence.fs`) - Database/file operations
4. **API** (`Api.fs`) - Wire it together with Fable.Remoting

```fsharp
// Api.fs - Wire validation → domain → persistence
let itemApi : IItemApi = {
    getAll = Persistence.getAllItems

    save = fun item -> async {
        match Validation.validate item with
        | Error errs -> return Error (String.concat ", " errs)
        | Ok valid ->
            let processed = Domain.process valid
            do! Persistence.save processed
            return Ok processed
    }
}
```

---

### Step 3: Implement Frontend
**Read:** `standards/frontend/state-management.md`, `standards/frontend/view-patterns.md`
**Skills:** Use `fsharp-frontend` for detailed frontend patterns

**Order:**
1. **State** (`State.fs`) - Model, Msg, init, update
2. **View** (`View.fs`) - Feliz components

Use `RemoteData<'T>` for async operations:
```fsharp
type Model = { Items: RemoteData<Item list> }

type Msg =
    | LoadItems
    | ItemsLoaded of Result<Item list, string>

let update msg model =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either Api.api.getAll () (Ok >> ItemsLoaded) (Error >> ItemsLoaded)
    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none
```

---

### Step 4: Write Tests
**Read:** `standards/testing/domain-tests.md`
**Skills:** Use `fsharp-tests` for testing patterns

Test at minimum:
- Domain logic (pure functions)
- Validation rules
- API integration (if complex)

```fsharp
[<Tests>]
let tests = testList "Feature" [
    testCase "domain logic works" <| fun () ->
        let result = Domain.process input
        Expect.equal result.Name "Expected" "Should process"
]
```

---

### Step 5: Verify & Document
**Commands:**
```bash
dotnet build          # Must succeed
dotnet test           # Must pass
```

**Update:**
- `diary/development.md` - Document what you implemented
- Invoke `qa-milestone-reviewer` agent if part of milestone

## Quick Reference

**Development Order:**
```
Shared (types, API) → Backend (validation → domain → persistence → API)
→ Frontend (state → view) → Tests
```

**Critical Rules:**
- Define types in `src/Shared/` FIRST
- NO I/O in `Domain.fs` (pure functions only)
- Use `Result<'T, string>` for errors
- Use `RemoteData<'T>` for async state

## Verification Checklist

- [ ] Read standards documentation
- [ ] Types defined in `src/Shared/`
- [ ] Validation at API boundary
- [ ] Domain logic pure (no I/O)
- [ ] Persistence handles I/O
- [ ] Frontend uses RemoteData
- [ ] Tests written (domain + validation minimum)
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] Development diary updated

## Common Mistakes

❌ **Don't:**
- Start coding before defining types
- Put I/O operations in Domain.fs
- Skip validation
- Ignore Result/RemoteData error states

✅ **Do:**
- Read documentation first
- Define types before implementation
- Keep domain logic pure
- Handle all error cases explicitly

## Related Skills

- **fsharp-backend** - Backend implementation details
- **fsharp-frontend** - Frontend patterns
- **fsharp-shared** - Type patterns
- **fsharp-validation** - Complex validation
- **fsharp-tests** - Testing patterns

## Detailed Documentation

For complete patterns and examples:
- `standards/global/development-workflow.md` - Full development process
- `standards/global/quick-reference.md` - All code templates
- `standards/global/architecture.md` - Architecture principles
- `standards/backend/` - Backend patterns
- `standards/frontend/` - Frontend patterns
- `standards/testing/` - Testing patterns
