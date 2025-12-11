# BudgetBuddy - Code Style & Conventions

## F# Conventions
- **Types**: Use records and discriminated unions, not classes
- **Naming**: camelCase for functions/values, PascalCase for types
- **Error Handling**: Use `Result<'T, 'Error>` for fallible operations
- **Option Types**: Use F# option types, never null

## Architecture Patterns

### Domain Logic (CRITICAL)
- `src/Server/Domain.fs` must have **NO I/O operations**
- Domain functions are pure transformations only
- All side effects go in `Persistence.fs` or `Api.fs`

### RemoteData Pattern (Frontend)
```fsharp
type RemoteData<'T> = 
    | NotAsked 
    | Loading 
    | Success of 'T 
    | Failure of string
```
Use for all async operations in frontend state.

### MVU Pattern (Frontend)
- All state changes through `update` function
- Use `Cmd` for side effects (API calls)
- View is pure function of Model

### Validation
- Validate at API boundary before any processing
- Return clear error messages
- Use `Validation.fs` for input validation

## Development Order (New Features)
1. `src/Shared/Domain.fs` → Define types
2. `src/Shared/Api.fs` → Define API contract
3. `src/Server/Validation.fs` → Input validation
4. `src/Server/Domain.fs` → Business logic (PURE!)
5. `src/Server/Persistence.fs` → Database operations
6. `src/Server/Api.fs` → Implement API
7. `src/Client/State.fs` → Model, Msg, update
8. `src/Client/View.fs` → UI components
9. `src/Tests/` → Tests

## Anti-Patterns to Avoid
- I/O in Domain.fs
- Ignoring Result types
- Classes for domain types
- Skipping validation
- Tests writing to production database
- Bug fixes without regression tests
