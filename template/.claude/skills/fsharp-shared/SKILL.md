---
name: fsharp-shared
description: |
  Define shared domain types and API contracts for F# full-stack applications using records, discriminated unions, and Fable.Remoting interfaces.
  Use when starting new features, defining data structures, or creating API contracts shared between client and server.
  Creates types in src/Shared/Domain.fs and API interfaces in src/Shared/Api.fs.
---

# F# Shared Types and API Contracts

## When to Use This Skill

Activate when:
- Starting any new feature (always define types first)
- User requests "add X entity", "define Y types"
- Need to create API contracts between client and server
- Modifying existing domain types

## Type Design Patterns

### Records for Data
```fsharp
type Entity = {
    Id: int
    Name: string
    Description: string option
    CreatedAt: DateTime
    UpdatedAt: DateTime
}
```

### Discriminated Unions for States
```fsharp
type Status = Active | Completed | Archived
type Priority = Low | Medium | High | Urgent
```

### Smart Constructors
```fsharp
type EmailAddress = private EmailAddress of string

module EmailAddress =
    let create (s: string) : Result<EmailAddress, string> =
        if s.Contains("@") then Ok (EmailAddress s)
        else Error "Invalid email"
    let value (EmailAddress s) = s
```

## API Contract Patterns

### Basic CRUD API
```fsharp
type IEntityApi = {
    getAll: unit -> Async<Entity list>
    getById: int -> Async<Result<Entity, string>>
    create: CreateRequest -> Async<Result<Entity, string>>
    update: Entity -> Async<Result<Entity, string>>
    delete: int -> Async<Result<unit, string>>
}
```

### Return Type Guide
- `Async<'T list>` - Always returns (empty list if none)
- `Async<Result<'T, string>>` - May fail (not found, validation error)
- `Async<Result<unit, string>>` - Success with no data to return

## Guidelines

### Do
- Use records (not classes)
- Use `option` for nullable fields
- Use `Result<'T, string>` for fallible operations
- Use descriptive names

### Don't
- Use classes for domain types
- Use null (use option)
- Add logic to type definitions

## Verification Checklist

- [ ] Types defined in `src/Shared/Domain.fs`
- [ ] API contracts in `src/Shared/Api.fs`
- [ ] Used records (not classes)
- [ ] Used `option` for nullable fields
- [ ] `dotnet build` succeeds
