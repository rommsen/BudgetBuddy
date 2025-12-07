# BudgetBuddy

A self-hosted web application that syncs bank transactions from Comdirect to YNAB with automatic categorization and manual review.

Support the development of this free, open-source project!

[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support%20Me-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/heimeshoff)

**[Buy me a coffee on Ko-fi](https://ko-fi.com/heimeshoff)** — Your support helps keep this project alive and growing!

---

## What is BudgetBuddy?

BudgetBuddy bridges the gap between your German bank account (Comdirect) and YNAB (You Need A Budget). Instead of manually entering transactions or relying on unreliable import files, BudgetBuddy:

1. **Connects directly to Comdirect** using their official API with secure TAN verification
2. **Automatically categorizes transactions** using your custom rules (regex, contains, or exact match)
3. **Flags special transactions** like Amazon or PayPal that need manual review
4. **Lets you review everything** before importing to YNAB
5. **Imports with one click** to your YNAB budget

## Features

### Phase 1: Core Sync (Current Focus)
- Comdirect OAuth flow with Push-TAN support
- YNAB Personal Access Token authentication
- Transaction review with manual categorization
- Batch import to YNAB

### Phase 2: Auto-Categorization
- Rule-based categorization engine
- Pattern matching (regex, contains, exact)
- Priority ordering
- Import/export rules as JSON

### Phase 3: Smart Detection
- Amazon transaction detection with order history links
- PayPal transaction detection with activity links
- Configurable external link templates

### Phase 4: Advanced Features
- Duplicate detection
- Transaction history
- Split transactions (multiple categories)

## Tech Stack

| Component | Technology |
|-----------|------------|
| Frontend | F# Elmish.React + Feliz + TailwindCSS + DaisyUI |
| Backend | F# Giraffe + Fable.Remoting |
| Database | SQLite |
| Deployment | Docker + Tailscale |

## Quick Start

### Prerequisites

- .NET 9+ SDK
- Node.js 20+
- Docker (for deployment)

### Development

```bash
# Clone and install
git clone https://github.com/heimeshoff/BudgetBuddy.git
cd BudgetBuddy
npm install

# Start backend (Terminal 1)
cd src/Server && dotnet watch run

# Start frontend (Terminal 2)
npm run dev
```

Open `http://localhost:5173` for the frontend (proxies API calls to backend on port 5001).

### Testing

BudgetBuddy has comprehensive test coverage including unit tests, property-based tests, and integration tests.

#### Running Tests

**Unit Tests (default):**
```bash
dotnet test
# Runs all unit tests (82 tests)
# ✅ No external API calls
# ✅ Fast (~1 second)
```

**Integration Tests (with real APIs):**

Integration tests make real API calls to YNAB and Comdirect (including Push-TAN). They are **disabled by default** and must be explicitly enabled.

```bash
# Option 1: Set in .env file
echo "RUN_INTEGRATION_TESTS=true" >> .env
dotnet test

# Option 2: Set as environment variable
RUN_INTEGRATION_TESTS=true dotnet test
```

**⚠️ Warning:** Integration tests will:
- Make real YNAB API requests (consuming your rate limit)
- Initiate Comdirect OAuth flow with Push-TAN sent to your phone
- Require valid credentials in `.env` file

#### Interactive Testing Scripts

For manual testing of integrations without the full UI:

```bash
# Test YNAB API integration
dotnet fsi scripts/test-ynab.fsx
# - Fetches budgets, accounts, categories
# - Validates token
# - Shows detailed output

# Test Comdirect OAuth flow
dotnet fsi scripts/test-comdirect.fsx
# - Initiates OAuth with Push-TAN
# - Waits for phone confirmation
# - Fetches transactions (if account ID provided)
```

**Setup for scripts:**
1. Copy `.env.example` to `.env`
2. Fill in your API credentials:
   ```bash
   YNAB_TOKEN=your-ynab-token
   COMDIRECT_CLIENT_ID=your-client-id
   COMDIRECT_CLIENT_SECRET=your-client-secret
   COMDIRECT_USERNAME=your-username
   COMDIRECT_PASSWORD=your-password
   # COMDIRECT_ACCOUNT_ID=your-account-id  # Optional
   ```
3. Run the scripts

See [scripts/README.md](scripts/README.md) for detailed documentation on the test scripts.

#### Import Legacy Rules

If you have categorization rules from a legacy system in YAML format, you can import them:

```bash
# List available YNAB budgets
dotnet fsi scripts/import-rules.fsx --list

# Import rules for a specific budget
dotnet fsi scripts/import-rules.fsx "My Budget"

# Clear all rules and reimport (useful after DB reset)
dotnet fsi scripts/import-rules.fsx --clear "My Budget"
```

**Expected YAML format** (`rules.yml` in project root):
```yaml
rules:
  - match: "REWE"
    category: "Groceries"
  - match: "Netflix"
    category: "Entertainment"
```

The script:
- Fetches categories from YNAB and matches by name
- Creates rules with `Contains` pattern type (substring match)
- Skips duplicates (unless `--clear` is used)
- Reports which categories couldn't be matched

#### Test Coverage

| Test Type | Count | Description |
|-----------|-------|-------------|
| Unit Tests | 82 | Fast, no I/O, always run |
| Integration Tests | 6 | Real API calls, opt-in only |
| Property-Based | 3 | FsCheck tests for edge cases |
| **Total** | **88** | Full test suite |

### Configuration

You'll need:
1. **Comdirect API credentials** (Client ID, Client Secret from Comdirect developer portal)
2. **YNAB Personal Access Token** (from YNAB account settings)

These are entered in the Settings page and stored encrypted locally.

## Deployment

### Docker (Recommended)

```bash
# Build image
docker build -t budgetbuddy .

# Run locally
docker run -p 5001:5001 -v $(pwd)/data:/app/data budgetbuddy
```

### With Tailscale (Private Access)

For secure access from anywhere on your Tailnet:

```bash
# Set your Tailscale auth key
echo "TS_AUTHKEY=tskey-auth-xxx" > .env

# Deploy
docker-compose up -d
```

Your app is now accessible at `https://budgetbuddy.<your-tailnet>.ts.net`.

## Project Structure

```
src/
├── Shared/           # Domain types + API contracts
│   ├── Domain.fs     # Transaction, Rule, SyncSession types
│   └── Api.fs        # Fable.Remoting API interfaces
├── Client/           # Elmish frontend
│   ├── State.fs      # Model, Msg, update (MVU)
│   ├── View.fs       # UI components (Feliz)
│   └── Views/        # Page-specific views
├── Server/           # Giraffe backend
│   ├── ComdirectClient.fs  # Comdirect API integration
│   ├── YnabClient.fs       # YNAB API integration
│   ├── RulesEngine.fs      # Categorization logic
│   ├── Persistence.fs      # SQLite operations
│   └── Api.fs              # API implementation
└── Tests/            # Expecto tests
    ├── *Tests.fs            # Unit tests
    └── *IntegrationTests.fs # Integration tests (opt-in)

scripts/
├── test-ynab.fsx            # Interactive YNAB API tester
├── test-comdirect.fsx       # Interactive Comdirect OAuth tester
├── import-rules.fsx         # Import rules from legacy YAML file
├── EnvLoader.fsx            # .env file loader
└── README.md                # Test scripts documentation

docs/
├── MILESTONE-PLAN.md        # Detailed implementation plan
├── banksync-produktspezifikation.md  # Product requirements
└── *.md                     # Architecture guides

legacy/               # Original CLI implementation (reference)
```

## How the Sync Flow Works

```
1. User clicks "Start Sync"
2. App initiates Comdirect OAuth
3. User receives Push-TAN on phone and confirms
4. App fetches transactions from Comdirect
5. Rules engine auto-categorizes where possible
6. User reviews transactions:
   - Green: Auto-categorized (ready)
   - Yellow: Needs attention (Amazon, PayPal)
   - Red: Uncategorized (manual input needed)
7. User confirms and clicks "Import"
8. Transactions sent to YNAB
```

## Security

- **No permanent storage of bank credentials** - TAN required for each sync
- **YNAB token encrypted** at rest using machine-specific key
- **No cloud dependency** - runs entirely on your hardware
- **Tailscale networking** - optional private access without public exposure

## Documentation

| Guide | Purpose |
|-------|---------|
| [Milestone Plan](docs/MILESTONE-PLAN.md) | Step-by-step implementation guide |
| [Architecture](docs/00-ARCHITECTURE.md) | System overview |
| [Frontend Guide](docs/02-FRONTEND-GUIDE.md) | Elmish + Feliz patterns |
| [Backend Guide](docs/03-BACKEND-GUIDE.md) | Giraffe + Fable.Remoting |
| [Persistence](docs/05-PERSISTENCE.md) | SQLite patterns |
| [Deployment](docs/07-BUILD-DEPLOY.md) | Docker setup |
| [Tailscale](docs/08-TAILSCALE-INTEGRATION.md) | Private network |

## Roadmap

See [MILESTONE-PLAN.md](docs/MILESTONE-PLAN.md) for the complete implementation roadmap with 16 milestones covering all planned features.

## Contributing

Contributions welcome! Please read the architecture docs first to understand the patterns used in this codebase.

## License

This project is released into the public domain under the Unlicense. See LICENSE for details.

---

**Note**: This project is not affiliated with Comdirect or YNAB. Use at your own risk. Always verify transactions before importing.
