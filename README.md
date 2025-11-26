# F# Full-Stack Blueprint

Support the development of this free, open-source project!

[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/heimeshoff)

**[☕ Buy me a coffee on Ko-fi](https://ko-fi.com/heimeshoff)** — Your support helps keep this project alive and growing!

---

A production-ready template for building F# web applications with type-safe end-to-end development, MVU architecture, and private deployment via Tailscale.

## What You Get

- **Frontend**: Elmish.React + Feliz with TailwindCSS 4.3
- **Backend**: Giraffe + Fable.Remoting (type-safe RPC)
- **Persistence**: SQLite + JSON files
- **Deployment**: Docker + Tailscale sidecar (private network, no auth code needed)
- **Testing**: Expecto
- **AI-Assisted Development**: Claude Code skills for guided implementation

## Quick Start

### Prerequisites

- .NET 8+ SDK
- Node.js 20+
- Docker (for deployment)

### Development

```bash
# Clone and install
git clone https://github.com/yourname/your-app.git
cd your-app
npm install

# Start backend (Terminal 1)
cd src/Server && dotnet watch run

# Start frontend (Terminal 2)
npm run dev

# Run tests
dotnet test
```

Open `http://localhost:5173` for the frontend (proxies API calls to backend on port 5000).

### Build & Deploy

```bash
# Build Docker image
docker build -t my-app .

# Run locally
docker run -p 5000:5000 -v $(pwd)/data:/app/data my-app

# Deploy with Tailscale (set TS_AUTHKEY in .env)
docker-compose up -d
```

Your app is now accessible on your Tailnet at `http://my-app:5000`.

## Project Structure

```
src/
├── Shared/           # Domain types + API contracts (shared by client & server)
│   ├── Domain.fs     # Business types (records, unions)
│   └── Api.fs        # Fable.Remoting API interfaces
├── Client/           # Elmish frontend
│   ├── State.fs      # Model, Msg, update (MVU)
│   ├── View.fs       # UI components (Feliz)
│   └── App.fs        # Entry point
├── Server/           # Giraffe backend
│   ├── Validation.fs # Input validation
│   ├── Domain.fs     # Business logic (pure, no I/O)
│   ├── Persistence.fs# Database/file operations
│   ├── Api.fs        # Fable.Remoting implementation
│   └── Program.fs    # Entry point
└── Tests/            # Expecto tests
docs/                 # Detailed implementation guides
.claude/skills/       # Claude Code skills for AI-assisted development
```

## Building Your Application

### Adding a Feature

1. **Define types** in `src/Shared/Domain.fs`
2. **Add API contract** in `src/Shared/Api.fs`
3. **Implement backend**: Validation → Domain → Persistence → API
4. **Build frontend**: State (Model/Msg/update) → View
5. **Write tests**

### Key Patterns

```fsharp
// Shared: Define your types
type Item = { Id: int; Name: string }

type IItemApi = {
    getAll: unit -> Async<Item list>
    save: Item -> Async<Result<Item, string>>
}

// Backend: Implement API
let api : IItemApi = {
    getAll = Persistence.getAllItems
    save = fun item -> async {
        match Validation.validate item with
        | Error e -> return Error e
        | Ok valid ->
            let! saved = Persistence.save valid
            return Ok saved
    }
}

// Frontend: MVU state management
type Model = { Items: RemoteData<Item list> }
type Msg = LoadItems | ItemsLoaded of Result<Item list, string>

let update msg model =
    match msg with
    | LoadItems ->
        { model with Items = Loading },
        Cmd.OfAsync.either api.getAll () (Ok >> ItemsLoaded) (Error >> ItemsLoaded)
    | ItemsLoaded (Ok items) ->
        { model with Items = Success items }, Cmd.none
```

## Using Claude Code

This repository includes Claude Code skills that guide implementation. Claude understands the architecture and can help you:

- Implement complete features following established patterns
- Add new domain types with proper validation
- Create UI components with MVU state management
- Write tests and fix issues
- Deploy to production

### Getting Started with Claude

```bash
# Install Claude Code CLI
npm install -g @anthropic-ai/claude-code

# Start working on your project
claude

# Ask Claude to implement features
> Add a todo list feature with priorities and due dates
> Fix the validation in the user form
> Deploy this to my home server with Tailscale
```

Claude will read the documentation in `/docs/` and follow the patterns defined in the skills.

### Writing Feature Specifications

Create a markdown file describing what you want:

```markdown
# Feature: Task Management

## Requirements
- Users can create tasks with title, description, priority
- Tasks can be marked complete
- Filter by status (active/completed)

## Domain
- Priority: Low | Medium | High | Urgent
- Status: Active | Completed

## Notes
- Store in SQLite
- Show task count in header
```

Then ask Claude: "Implement the feature described in task-management.md"

## Documentation

| Guide | Purpose |
|-------|---------|
| [Architecture](docs/00-ARCHITECTURE.md) | System overview and design decisions |
| [Project Setup](docs/01-PROJECT-SETUP.md) | Initialize new projects |
| [Frontend Guide](docs/02-FRONTEND-GUIDE.md) | Elmish + Feliz patterns |
| [Backend Guide](docs/03-BACKEND-GUIDE.md) | Giraffe + Fable.Remoting |
| [Shared Types](docs/04-SHARED-TYPES.md) | Type design patterns |
| [Persistence](docs/05-PERSISTENCE.md) | SQLite and file storage |
| [Testing](docs/06-TESTING.md) | Expecto test patterns |
| [Build & Deploy](docs/07-BUILD-DEPLOY.md) | Docker deployment |
| [Tailscale](docs/08-TAILSCALE-INTEGRATION.md) | Private network setup |
| [Quick Reference](docs/09-QUICK-REFERENCE.md) | Code templates |

## Deployment Options

### Home Server with Tailscale

No public internet exposure. Access only via your Tailnet:

```bash
# Set your Tailscale auth key
echo "TS_AUTHKEY=tskey-auth-xxx" > .env

# Deploy
docker-compose up -d
```

## License

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <https://unlicense.org>