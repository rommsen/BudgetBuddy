# Architecture

> High-level architecture principles for F# full-stack web applications.

## Overview

This architecture uses a functional-first approach with type safety across all layers. The frontend uses Elmish MVU, the backend uses Giraffe with Fable.Remoting, and persistence uses SQLite with Dapper.

## Tech Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| Frontend | Elmish.React + Feliz | MVU architecture |
| Build | Vite + fable-plugin | HMR development |
| Styling | TailwindCSS + DaisyUI | Utility-first CSS |
| Routing | Feliz.Router | Client-side routing |
| Backend | Giraffe | Functional ASP.NET Core |
| RPC | Fable.Remoting | Type-safe client/server |
| Database | SQLite + Dapper | Embedded database |
| Tests | Expecto | F# test framework |
| Networking | Tailscale (tsnet) | Private network access |

## Core Principles

### 1. Type Safety End-to-End

- Define ALL types in `src/Shared/`
- Fable.Remoting ensures compile-time checking
- No manual JSON serialization in business logic

### 2. Pure Domain Logic

- `Domain.fs` has NO I/O operations
- All side effects in `Persistence.fs` or `Api.fs`
- Domain functions are pure transformations

### 3. MVU Architecture

```fsharp
type Model = { (* state *) }
type Msg =
    | UserAction
    | ApiResponse of Result<Data, string>

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    // State transitions

let view (model: Model) (dispatch: Msg -> unit) =
    // UI rendering
```

### 4. Simple Persistence

- SQLite for structured data requiring queries
- JSON files for simple configuration
- Event sourcing for audit trails

### 5. No Runtime Authentication

- Apps are private via Tailscale
- No login forms or JWT tokens
- Focus on business logic

## Project Structure

```
src/
├── Shared/              # Types and API contracts
│   ├── Domain.fs
│   └── Api.fs
├── Client/              # Elmish frontend
│   ├── State.fs
│   └── View.fs
├── Server/              # Giraffe backend
│   ├── Domain.fs        # Pure logic
│   ├── Persistence.fs   # I/O
│   └── Api.fs           # Implementation
└── Tests/
```

## Communication Flow

```
Browser → Client (Elmish) → Fable.Remoting → Server (Giraffe) → SQLite/Files
```

## Security Model

Tailscale provides:
- Network-level authentication
- Encrypted connections (WireGuard)
- Access control via ACLs

Application handles:
- Authorization (who can do what)
- Input validation
- SQL injection prevention

## See Also

- `development-workflow.md` - Feature development process
- `quick-reference.md` - Code templates
