# F# Full-Stack Development Skills

This directory contains specialized skills for Claude Code when developing F# full-stack applications.

## Available Skills

| Skill | Purpose | Use When |
|-------|---------|----------|
| `fsharp-feature` | Orchestrates end-to-end feature development | "Add X feature", "Implement Y" |
| `fsharp-shared` | Types and API contracts | Starting features, defining types |
| `fsharp-backend` | Server-side implementation | Backend logic, API endpoints |
| `fsharp-validation` | Input validation | Validation rules, form handling |
| `fsharp-persistence` | Database and file I/O | CRUD, storage, event sourcing |
| `fsharp-frontend` | Elmish MVU + Feliz | UI components, state management |
| `fsharp-tests` | Expecto testing | Writing and fixing tests |
| `tailscale-deploy` | Docker + Tailscale | Production deployment |

## Development Order

```
fsharp-shared → fsharp-backend → fsharp-frontend → fsharp-tests
```

## Skill Structure

Each skill contains:
- **YAML frontmatter** - name, description, allowed-tools
- **When to Use** - Activation scenarios
- **Complete patterns** - Working code examples
- **Verification checklist** - Steps to verify completion

## Key Principles (All Skills)

1. **Type Safety** - Define types in `src/Shared/` first
2. **Pure Domain** - NO I/O in `src/Server/Domain.fs`
3. **MVU Pattern** - All state through `update` function
4. **Explicit Errors** - Use `Result<'T, string>` and `RemoteData<'T>`
5. **Validate Early** - At API boundary
6. **Test Coverage** - Especially domain logic and validation
