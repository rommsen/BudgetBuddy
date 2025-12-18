# F# Full-Stack Template

A production-ready template for F# full-stack applications with Claude Code integration.

## Tech Stack

- **Frontend**: Elmish.React + Feliz + TailwindCSS/DaisyUI
- **Backend**: Giraffe + Fable.Remoting
- **Database**: SQLite + Dapper
- **Tests**: Expecto
- **Deployment**: Docker + Tailscale

## Getting Started

### 1. Clone and Customize

```bash
# Clone this template
git clone https://github.com/your-org/fsharp-fullstack-template my-app
cd my-app

# Customize CLAUDE.md
# Replace {{PROJECT_NAME}} and {{PROJECT_DESCRIPTION}}
```

### 2. Install Dependencies

```bash
# Install .NET packages
dotnet restore

# Install npm packages
npm install
```

### 3. Run Development

```bash
# Terminal 1: Backend
cd src/Server && dotnet watch run

# Terminal 2: Frontend
npm run dev
```

### 4. Run Tests

```bash
dotnet test
```

## Project Structure

```
.claude/
├── skills/        # Claude Code skills for F# development
├── agents/        # QA and test-fixing agents
└── commands/      # Custom slash commands

docs/
├── 00-ARCHITECTURE.md
├── 09-QUICK-REFERENCE.md
└── ...

src/
├── Shared/        # Domain types, API contracts
├── Server/        # Giraffe backend
├── Client/        # Elmish frontend
└── Tests/         # Expecto tests
```

## Development Order

Always follow this sequence for new features:

1. `src/Shared/Domain.fs` → Define types
2. `src/Shared/Api.fs` → Define API contract
3. `src/Server/Validation.fs` → Input validation
4. `src/Server/Domain.fs` → Business logic (PURE!)
5. `src/Server/Persistence.fs` → Database operations
6. `src/Server/Api.fs` → Implement API
7. `src/Client/State.fs` → Model, Msg, update
8. `src/Client/View.fs` → UI components
9. `src/Tests/` → Tests

## Key Principles

- **Type Safety**: Define all types in `src/Shared/` first
- **Pure Domain**: No I/O in `src/Server/Domain.fs`
- **MVU Architecture**: All state through `update` function
- **Validate Early**: At API boundary before processing
- **Test Coverage**: Especially domain logic and validation

## Claude Code Integration

This template includes:

- **Skills** for layer-specific guidance
- **Agents** for QA review and test fixing
- **CLAUDE.md** with project conventions

Invoke skills by describing what you need:
- "Add a new feature" → `fsharp-feature`
- "Define types" → `fsharp-shared`
- "Add validation" → `fsharp-validation`
- "Write tests" → `fsharp-tests`

## Deployment

```bash
# Build Docker image
docker build -t my-app .

# Deploy with Tailscale
docker-compose up -d
```

## Based On

This template captures learnings from real-world F# full-stack development. See `LEARNINGS.md` for detailed insights and patterns.
