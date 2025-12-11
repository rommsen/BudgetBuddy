# BudgetBuddy - Development Commands

## Build & Run

### Backend (with hot reload)
```bash
cd src/Server && dotnet watch run
```

### Frontend (with HMR)
```bash
npm run dev
```

### Build All
```bash
dotnet build BudgetBuddy.sln
```

## Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test src/Tests/Tests.fsproj

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Package Management
```bash
# Restore packages
dotnet restore BudgetBuddy.sln

# Add NuGet package to project
dotnet add src/Server/Server.fsproj package <PackageName>

# Frontend packages
npm install
```

## Docker & Deployment
```bash
# Build Docker image
docker build -t budgetbuddy .

# Run with docker-compose (includes Tailscale)
docker-compose up -d

# View logs
docker-compose logs -f
```

## Git
```bash
# Standard git commands work on Darwin/macOS
git status
git add .
git commit -m "message"
git push
```

## Useful Utilities
```bash
# Find files
find . -name "*.fs" -type f

# Search in code
grep -r "pattern" src/

# List directory structure
tree -I 'node_modules|bin|obj|.fable|dist'
```
