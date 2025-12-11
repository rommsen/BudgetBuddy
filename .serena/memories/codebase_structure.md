# BudgetBuddy - Codebase Structure

## Directory Layout
```
src/
├── Shared/           # Shared F# types (Domain.fs, Api.fs)
│   ├── Domain.fs     # Domain types, DTOs, error types
│   └── Api.fs        # API contract interfaces
├── Server/           # Backend implementation
│   ├── Api.fs        # API implementation (Fable.Remoting)
│   ├── Domain.fs     # Business logic (PURE - no I/O!)
│   ├── Validation.fs # Input validation
│   ├── Persistence.fs # SQLite database operations
│   ├── ComdirectClient.fs # Comdirect API client
│   └── YnabClient.fs # YNAB API client
├── Client/           # Frontend implementation
│   ├── State.fs      # Elmish Model, Msg, update
│   ├── View.fs       # Main view composition
│   ├── Components/   # Feature-specific components
│   │   └── SyncFlow/ # Sync wizard flow
│   └── DesignSystem/ # Reusable UI components
└── Tests/            # Expecto tests
    └── *.fs          # Test files

docs/                 # Documentation guides
diary/                # Development diary
```

## Key Files
- `src/Shared/Domain.fs` - All domain types
- `src/Shared/Api.fs` - API interfaces (ISettingsApi, ISyncApi, etc.)
- `src/Server/Api.fs` - API implementations
- `src/Client/State.fs` - Frontend state management
- `BudgetBuddy.sln` - Solution file for all projects
