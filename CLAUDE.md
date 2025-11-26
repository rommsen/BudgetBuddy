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
