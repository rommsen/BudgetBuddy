# Architecture Overview

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

## Project Structure

```
src/
├── Shared/           # Domain types and API contracts
│   ├── Domain.fs     # Domain types (records, unions)
│   └── Api.fs        # API interfaces (Fable.Remoting)
│
├── Server/           # Giraffe backend
│   ├── Validation.fs # Input validation
│   ├── Domain.fs     # Business logic (PURE - no I/O!)
│   ├── Persistence.fs# Database operations
│   ├── Api.fs        # API implementation
│   └── Program.fs    # Entry point
│
├── Client/           # Elmish.React + Feliz frontend
│   ├── Types.fs      # Client-only types (RemoteData)
│   ├── Api.fs        # API client (Fable.Remoting)
│   ├── State.fs      # Model, Msg, update
│   ├── View.fs       # UI components
│   └── Main.fs       # Entry point
│
└── Tests/            # Expecto tests
    ├── DomainTests.fs
    ├── ValidationTests.fs
    └── Program.fs
```

## Data Flow

```
                    ┌─────────────┐
                    │   Client    │
                    │  (Elmish)   │
                    └──────┬──────┘
                           │ Fable.Remoting (Type-safe RPC)
                    ┌──────▼──────┐
                    │    API      │
                    │  (Giraffe)  │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │Validation│ │  Domain  │ │Persistence│
        └──────────┘ └──────────┘ └──────────┘
                           │
                    ┌──────▼──────┐
                    │   SQLite    │
                    └─────────────┘
```

## Key Principles

### 1. Type Safety End-to-End
- Define ALL types in `src/Shared/`
- Fable.Remoting ensures compile-time API contract checking
- No manual JSON serialization

### 2. Pure Domain Logic
- `src/Server/Domain.fs` has NO I/O
- All side effects in `Persistence.fs` or `Api.fs`
- Domain functions are trivially testable

### 3. Validate at Boundaries
- Validate input at API entry points
- Never trust client data
- Return clear error messages

### 4. MVU Architecture (Frontend)
- Model = Application state
- View = Pure function of Model
- Update = State transitions via Messages
- Cmd = Side effects (API calls)

## Development Order

Always follow this sequence for new features:

```
1. Shared/Domain.fs     → Define types
2. Shared/Api.fs        → Define API contract
3. Server/Validation.fs → Input validation
4. Server/Domain.fs     → Business logic (PURE)
5. Server/Persistence.fs → Database operations
6. Server/Api.fs        → Implement API
7. Client/State.fs      → Model, Msg, update
8. Client/View.fs       → UI components
9. Tests/               → Tests
```
