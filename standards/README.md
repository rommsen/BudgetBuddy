# F# Full-Stack Standards

Reusable documentation for F# web applications using Elmish.React + Feliz (frontend), Giraffe + Fable.Remoting (backend), SQLite (persistence), and Docker + Tailscale (deployment).

## Purpose

These standards are optimized for:
- Claude Code / AI agents
- Minimal context window usage
- Maximum implementation guidance
- Copy-paste ready code examples

## Quick Navigation

### By Task

| I want to... | Read... |
|--------------|---------|
| Start a new feature | `global/development-workflow.md` |
| Define domain types | `shared/types.md` |
| Create API contract | `shared/api-contracts.md` |
| Validate input | `shared/validation.md` |
| Implement backend API | `backend/api-implementation.md` |
| Write pure domain logic | `backend/domain-logic.md` |
| Add database persistence | `backend/persistence-sqlite.md` |
| Use file/event storage | `backend/persistence-files.md` |
| Build frontend UI | `frontend/view-patterns.md` |
| Manage frontend state | `frontend/state-management.md` |
| Handle async data | `frontend/remotedata.md` |
| Write tests | `testing/overview.md` |
| Deploy with Docker | `deployment/docker.md` |
| Set up Tailscale | `deployment/tailscale.md` |

### By Skill

| Skill | Primary Files |
|-------|---------------|
| `fsharp-feature` | `global/development-workflow.md`, `global/quick-reference.md` |
| `fsharp-shared` | `shared/types.md`, `shared/api-contracts.md` |
| `fsharp-backend` | `backend/api-implementation.md`, `backend/domain-logic.md` |
| `fsharp-validation` | `shared/validation.md` |
| `fsharp-persistence` | `backend/persistence-sqlite.md`, `backend/persistence-files.md` |
| `fsharp-frontend` | `frontend/state-management.md`, `frontend/view-patterns.md` |
| `fsharp-tests` | `testing/overview.md` + relevant test file |
| `tailscale-deploy` | `deployment/tailscale.md`, `deployment/docker-compose.md` |

## Directory Structure

```
standards/
├── global/          # Cross-cutting concerns
├── shared/          # Type design and API contracts
├── backend/         # Giraffe + persistence patterns
├── frontend/        # Elmish + Feliz patterns
├── testing/         # Expecto testing patterns
└── deployment/      # Docker + Tailscale
```

## Key Principles

1. **Type Safety End-to-End** - Define types in Shared first
2. **Pure Domain Logic** - No I/O in Domain.fs
3. **MVU Architecture** - State changes through update function
4. **RemoteData Pattern** - Explicit async state handling
5. **Validate at Boundaries** - Validate input at API entry
6. **Regression Tests Required** - Every bug fix needs a test

## Development Order

```
1. shared/types.md         -> Define types
2. shared/api-contracts.md -> Define API
3. shared/validation.md    -> Input validation
4. backend/domain-logic.md -> Pure business logic
5. backend/persistence-*.md -> Database/files
6. backend/api-implementation.md -> Implement API
7. frontend/state-management.md -> Model, Msg, update
8. frontend/view-patterns.md -> UI components
9. testing/*.md            -> Tests
```

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | Elmish.React + Feliz |
| Styling | TailwindCSS, DaisyUI |
| Build | Vite + fable-plugin |
| Backend | Giraffe + Fable.Remoting |
| Database | SQLite + Dapper |
| Tests | Expecto |
| Runtime | .NET 8+ |
| Deployment | Docker + Tailscale |

## For AI Agents

Before implementing any feature:

1. Check `global/quick-reference.md` for code templates
2. Read the relevant layer-specific files
3. Follow patterns exactly - consistency matters
4. Use `global/anti-patterns.md` to avoid common mistakes
5. Update diary and run tests per `global/development-workflow.md`
