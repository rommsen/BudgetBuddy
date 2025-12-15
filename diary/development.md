# Development Diary

This diary tracks the development progress of BudgetBuddy.

---

## 2025-12-16 00:15 - Frontend Architecture: Dashboard Hero Button Design System (Milestone 5)

**What I did:**
Completed Milestone 5 from the Frontend Architecture Improvement plan. Integrated the Dashboard Sync button into the Design System by creating reusable `Button.hero` variants and replacing inline styles with Design System components.

**Files Modified:**
- `src/Client/DesignSystem/Tokens.fs` - Added large glow effect variants to the `Glows` module:
  - `orangeLg` / `orangeHoverLg` - Large orange glow for hero buttons
  - `tealLg` / `tealHoverLg` - Large teal glow for secondary hero actions
  - `greenLg` / `greenHoverLg` - Large green glow for success actions

- `src/Client/DesignSystem/Button.fs` - Added Hero Button section with four variants:
  - `hero` - Large CTA button with prominent orange glow
  - `heroWithIcon` - Hero button with icon before text
  - `heroLoading` - Hero button with loading spinner state
  - `heroTeal` - Teal variant for secondary prominent actions

- `src/Client/Components/Dashboard/View.fs` - Replaced 17-line inline-styled `syncButton` with single-line `Button.heroWithIcon "Start Sync" (Icons.sync MD Primary) onNavigateToSync`

- `CLAUDE.md` - Added hero button documentation to the Button Examples section

**Rationale:**
The Dashboard Sync button used inline Tailwind styles with custom shadow values that weren't reusable. Moving these to the Design System ensures:
- Consistent hero button styling across the application
- Reusable glow effects for other prominent CTAs
- Easier maintenance of visual effects in one place

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated to this change)
- Tests: 294/294 passed (6 skipped)
- Issues: None

---

## 2025-12-15 23:45 - Frontend Architecture: ErrorDisplay Design System Komponente (Milestone 4)

**What I did:**
Completed Milestone 4 from the Frontend Architecture Improvement plan. Created a new `ErrorDisplay` Design System component providing standardized error displays across the application with consistent styling and behavior.

**Files Added:**
- `src/Client/DesignSystem/ErrorDisplay.fs` - New Design System component with multiple error display variants:
  - `inline'` / `inlineWithIcon` - Compact inline errors for form validation
  - `card` / `cardWithTitle` / `cardCompact` - Card-based errors with optional retry buttons
  - `hero` / `heroSimple` - Large hero-style errors for major operations (like sync failures)
  - `fullPage` / `fullPageWithAction` - Full-page error states for critical failures
  - `forRemoteData` / `simple` / `warning` - Convenience functions

**Files Modified:**
- `src/Client/Client.fsproj` - Added `ErrorDisplay.fs` to compilation (after Button.fs)
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` - Replaced inline `errorView` with `ErrorDisplay.hero`
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Replaced inline error with `ErrorDisplay.cardCompact`
- `src/Client/Components/Settings/View.fs` - Replaced 3 inline error displays with `ErrorDisplay.cardCompact`
- `src/Client/Components/Rules/View.fs` - Replaced inline error with `ErrorDisplay.cardWithTitle`

**Rationale:**
The Frontend Architecture Review identified inconsistent error handling across the application. This component standardizes error displays with:
- Consistent visual design using the neon color palette
- ARIA roles for accessibility (`role="alert"`)
- Optional retry functionality
- Multiple variants for different contexts (inline, card, hero, full-page)

**Outcomes:**
- Build: ✅ (0 errors, 2 warnings - unrelated to this change)
- Tests: 294/294 passed (6 skipped)
- Issues: None

---

## 2025-12-15 23:00 - Frontend Architecture: Rules Form State Konsolidierung (Milestone 3)

**What I did:**
Completed Milestone 3 from the Frontend Architecture Improvement plan. Consolidated the 9 separate form fields in the Rules Model into a dedicated `RuleFormState` record type, improving code organization and maintainability.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs` - Added `RuleFormState` record type and `RuleFormState` module with `empty` and `fromRule` helper functions. Updated `Model` type to use `Form: RuleFormState` instead of 10 separate fields.
- `src/Client/Components/Rules/State.fs` - Removed `emptyRuleForm` function (now `RuleFormState.empty`), updated `init` and `update` functions to use the new consolidated form state.
- `src/Client/Components/Rules/View.fs` - Updated all form field accesses from `model.RuleFormFieldName` to `model.Form.FieldName` and `model.RuleSaving` to `model.Form.IsSaving`.

**Before (10 separate fields):**
```fsharp
type Model = {
    RuleFormName: string
    RuleFormPattern: string
    RuleFormPatternType: PatternType
    RuleFormTargetField: TargetField
    RuleFormCategoryId: YnabCategoryId option
    RuleFormPayeeOverride: string
    RuleFormEnabled: bool
    RuleFormTestInput: string
    RuleFormTestResult: string option
    RuleSaving: bool
    // ... other fields
}
```

**After (1 consolidated form state):**
```fsharp
type RuleFormState = {
    Name: string
    Pattern: string
    PatternType: PatternType
    TargetField: TargetField
    CategoryId: YnabCategoryId option
    PayeeOverride: string
    Enabled: bool
    TestInput: string
    TestResult: string option
    IsSaving: bool
}

type Model = {
    Form: RuleFormState
    // ... other fields
}
```

**Rationale:**
- Reduces cognitive load by grouping related form state
- Helper functions (`empty`, `fromRule`) reduce duplication and make code cleaner
- Consistent naming pattern (`model.Form.X`) improves readability
- Easier to add new form fields in the future

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (purely structural refactoring)

---

## 2025-12-15 22:00 - Frontend Architecture: SyncFlow View Modularisierung (Milestone 2)

**What I did:**
Completed Milestone 2 from the Frontend Architecture Improvement plan. Split the large SyncFlow/View.fs (1700+ lines) into smaller, maintainable modules. Created a new Views/ subfolder with focused components.

**Files Added:**
- `src/Client/Components/SyncFlow/Views/StatusViews.fs` - Contains: `tanWaitingView`, `fetchingView`, `loadingView`, `errorView`, `completedView`, `startSyncView` (~350 lines)
- `src/Client/Components/SyncFlow/Views/InlineRuleForm.fs` - Contains: `inlineRuleForm` with pattern type conversions (~200 lines)
- `src/Client/Components/SyncFlow/Views/TransactionRow.fs` - Contains: `transactionRow`, `statusDot`, `duplicateIndicator`, `expandChevron`, `skipToggleIcon`, `createRuleButton`, `memoRow`, `duplicateDebugInfo`, helper functions (~450 lines)
- `src/Client/Components/SyncFlow/Views/TransactionList.fs` - Contains: `transactionListView`, `filterTransactions` (~310 lines)

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs` - Reduced from ~1700 to ~90 lines, now only contains composition
- `src/Client/Client.fsproj` - Added new files in correct compilation order
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` - Marked Milestone 2 as complete

**Module Dependencies:**
```
StatusViews.fs      → Types, Shared.Domain, DesignSystem
InlineRuleForm.fs   → Types, Shared.Domain, DesignSystem
TransactionRow.fs   → Types, Shared.Domain, DesignSystem, InlineRuleForm
TransactionList.fs  → Types, Shared.Domain, DesignSystem, TransactionRow
View.fs             → Types, Shared.Domain, DesignSystem, StatusViews, TransactionList
```

**Rationale:**
Large monolithic view files are difficult to maintain, navigate, and understand. Splitting into focused modules:
- Improves code organization and readability
- Makes it easier to find and modify specific components
- Enables better code reuse
- Reduces cognitive load when working on specific features

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (purely structural refactoring)

**Notes:**
- `SplitEditor.fs` was not created - no split transaction UI exists in codebase
- `CategorySelector.fs` was not created separately - integrated into `TransactionRow.fs` using `Input.searchableSelect`

---

## 2025-12-15 21:15 - Frontend Architecture: React Key Props (Milestone 1)

**What I did:**
Completed Milestone 1 from the Frontend Architecture Improvement plan. Added React key props to all list renderings to ensure efficient React reconciliation and prevent potential rendering issues.

**Files Modified:**
- `src/Client/Components/Settings/View.fs` - Added `prop.key` to Budget dropdown options (line 150-155), Added `prop.key` to Account dropdown options (line 194-199)
- `src/Client/Components/Rules/View.fs` - Added `prop.key` to Skeleton loader items (line 570-574)
- `docs/FRONTEND-ARCHITECTURE-MILESTONES.md` - Marked Milestone 1 as complete with summary

**Verified Existing Implementations:**
- SyncFlow/View.fs: Transaction list already had proper keys via `prop.key id` pattern
- Rules/View.fs: Rule list already had proper keys via `prop.key (string id)` pattern

**Pattern Applied:**
```fsharp
// Before
for bwa in budgets do
    Html.option [ prop.value id; prop.text bwa.Budget.Name ]

// After
for bwa in budgets do
    let (YnabBudgetId id) = bwa.Budget.Id
    Html.option [ prop.key id; prop.value id; prop.text bwa.Budget.Name ]
```

**Rationale:**
React keys help React identify which items have changed, been added, or removed. Without keys, React may re-render the entire list unnecessarily or cause subtle UI bugs when items are reordered.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- No functional changes (structural improvement only)

---

## 2025-12-15 18:30 - Refactor: Minimalistisches Dashboard Redesign

**What I did:**
Completely redesigned the Dashboard to be minimalistic and focused. Removed the stats grid (Last Sync, Total Imported, Sync Sessions cards), removed the Recent Activity history list, and replaced everything with a single centered "Start Sync" button with last sync information below it.

**Files Modified:**
- `src/Client/Components/Dashboard/Types.fs` - Simplified model: `RecentSessions: RemoteData<SyncSession list>` → `LastSession: RemoteData<SyncSession option>`, renamed messages accordingly
- `src/Client/Components/Dashboard/State.fs` - Updated init and update functions to use `LoadLastSession` instead of `LoadRecentSessions`, now fetches only the most recent session via `getSyncHistory 1`
- `src/Client/Components/Dashboard/View.fs` - Complete rewrite: removed `statsSection`, `historyItem`, `historySection`, `pageHeader`, kept `warningAlert` for config warnings, new centered layout with large sync button and last sync info
- `src/Client/State.fs` - Updated navigation handler to use `LoadLastSession` instead of `LoadRecentSessions`

**Design Changes:**
1. **Before**: Dashboard with 3 stats cards + quick action card + 5-item history list
2. **After**: Single centered "Start Sync" button with glow effect, last sync info below (date + transaction count)
3. Config warnings remain visible when YNAB/Comdirect not configured

**Rationale:**
User feedback: Dashboard statistics were not actionable. The history list couldn't be clicked, and the numbers (total imported, sync sessions count) provided no real value. A minimalistic dashboard that focuses on the main action (starting a sync) is more useful.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Visual: Clean, focused dashboard with prominent sync button

---

## 2025-12-15 16:00 - Feature: Verbessertes Formular-Handling mit Validierungsfeedback

**What I did:**
Implemented consistent form validation UX across all forms. Disabled buttons now have clear visual distinction (50% opacity), and a validation message appears under the button showing which required fields are missing. All required fields are now marked with a red asterisk.

**Files Added:**
- `src/Client/DesignSystem/Form.fs` - New form validation component with `submitButton`, `submitButtonWithIcon`, and `submitButtonSecondary` functions

**Files Modified:**
- `src/Client/DesignSystem/Button.fs` - Added disabled styling (`disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none`)
- `src/Client/Client.fsproj` - Added Form.fs to compilation
- `src/Client/Components/Settings/View.fs` - Changed YNAB Token + all 5 Comdirect fields to use `Input.groupRequired`, replaced buttons with `Form.submitButton`
- `src/Client/Components/Rules/View.fs` - Replaced footer button with `Form.submitButton`
- `src/Client/Components/SyncFlow/View.fs` - Added required marker to Rule Name, replaced button with `Form.submitButton`, changed Pattern asterisk to neon-red for consistency

**UX Changes:**
1. **Disabled buttons**: Now visually distinct with 50% opacity and cursor-not-allowed
2. **Validation message**: Orange text under button shows "Bitte ausfüllen: Field1, Field2" when fields are missing
3. **Required field markers**: Red asterisk (*) on all required fields (consistent across all forms)
4. **Consistent pattern**: `Form.submitButton` used in Settings, Rules modal, and Inline rule form

**Rationale:**
User feedback indicated that disabled buttons had no visual distinction and no feedback about why they were disabled. This made forms confusing to use.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Visual: Buttons clearly distinguishable, validation feedback visible

---

## 2025-12-15 - Feature: Comdirect Connection Test in Settings

**What I did:**
Implemented Comdirect connection test in Settings. The "Test Connection" button initiates the full OAuth + Push-TAN flow to validate credentials. Originally planned to include account discovery via `/api/banking/v1/accounts`, but discovered that Comdirect doesn't provide a public accounts endpoint (returns 404 for all endpoint variants).

**Architectural Decision:**
Account-ID remains a manual input field. The connection test validates that credentials are correct through the TAN flow, but users must enter their Account-ID manually.

**Files Added:**
- None

**Files Modified:**
- `src/Shared/Domain.fs` - Added `ComdirectAccount` type and `ComdirectAccountId` wrapper type (unused but kept for future)
- `src/Shared/Api.fs` - Extended `SettingsApi` with `testComdirectConnection` and `confirmComdirectTan` functions
- `src/Server/ComdirectClient.fs` - Added `accountDecoder`, `accountsDecoder`, and `getAccounts` function (unused, kept for future)
- `src/Server/ComdirectAuthSession.fs` - Added `fetchAccounts` function (unused, kept for future)
- `src/Server/Api.fs` - Implemented connection test and TAN confirmation endpoints
- `src/Client/Components/Settings/Types.fs` - Added `ComdirectConnectionValid` and `ComdirectAuthPending` to Model
- `src/Client/Components/Settings/State.fs` - Added init fields and update handlers for connection test
- `src/Client/Components/Settings/View.fs` - Added TAN flow UI with success/error display

**UX Flow:**
1. User saves Comdirect credentials and Account-ID
2. "Test Connection" button appears (only if credentials saved)
3. Clicking starts TAN authentication → orange waiting UI
4. User confirms Push-TAN in Comdirect app
5. Clicking "I've Confirmed the TAN" completes validation
6. Green success message: "Credentials verified successfully!"

**Rationale:**
Originally a high-priority backlog item for account discovery. Simplified to credential validation after discovering Comdirect doesn't expose an accounts API.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed (6 skipped integration tests)
- Backlog item updated to reflect actual implementation

---

## 2025-12-13 19:45 - Fix: Encrypted Settings Lost After Docker Rebuild

**What I did:**
Fixed critical bug where encrypted settings (YNAB token, Comdirect credentials) were lost after every `docker-compose up -d --build`. The root cause was that the encryption key was derived from `Environment.MachineName`, which changes with each Docker container rebuild.

**Root Cause Analysis:**
```fsharp
// In Persistence.fs - getEncryptionKey()
let machineKey = Environment.MachineName + "BudgetBuddy2025"
```
Docker containers get a new hostname on each rebuild → different encryption key → previously encrypted settings become unreadable.

**Files Modified:**
- `docker-compose.yml` - Added `BUDGETBUDDY_ENCRYPTION_KEY` environment variable
- `README.md` - Added documentation for the encryption key setup
- `.env` - Added generated encryption key (not committed)

**Solution:**
1. Generate a stable encryption key: `openssl rand -base64 32`
2. Add to `.env`: `BUDGETBUDDY_ENCRYPTION_KEY=<your-key>`
3. Reference in docker-compose.yml: `BUDGETBUDDY_ENCRYPTION_KEY=${BUDGETBUDDY_ENCRYPTION_KEY}`

**Important:** After applying this fix, existing encrypted settings are lost and must be re-entered. The encryption key in `.env` must be kept secret and backed up.

**Outcomes:**
- Build: ✅
- Tests: N/A (configuration fix)
- Settings now persist across Docker rebuilds

---

## 2025-12-12 14:45 - Script: deploy-rules.sh for Live Database Import

**What I did:**
Created automation script to import categorization rules from `rules.yml` to the live Docker database. The script handles stopping/starting the container and sets the correct `DATA_DIR` environment variable.

**Files Added:**
- `scripts/deploy-rules.sh` - Bash script that:
  - Accepts budget name and optional `--clear` flag
  - Stops the Docker container
  - Runs `import-rules.fsx` with `DATA_DIR=~/my_apps/budgetbuddy`
  - Restarts the container and waits for health check
  - Supports `--list` to show available YNAB budgets

**Files Modified:**
- `CLAUDE.md` - Added documentation for the deploy-rules script under "Quick Commands"

**Rationale:**
Manual rule imports to the live database required multiple steps (stop app, set DATA_DIR, run script, start app). This script automates the entire process with a single command.

**Usage:**
```bash
./scripts/deploy-rules.sh --list              # List available budgets
./scripts/deploy-rules.sh "My Budget"         # Add new rules
./scripts/deploy-rules.sh "My Budget" --clear # Clear all & reimport
```

**Outcomes:**
- Build: N/A (script only)
- Tests: N/A
- Successfully imported 55 rules to live database

---

## 2025-12-12 10:30 - Bugfix: Database not initialized in Docker

**What I did:**
Fixed critical bug where database tables were not created when running in Docker. The `initializeDatabase()` function was defined but never called at server startup.

**Files Modified:**
- `src/Server/Program.fs` - Added call to `Persistence.initializeDatabase()` at server startup (replaced `Persistence.ensureDataDir()` which only created the directory but not the tables)

**Rationale:**
When deploying via Docker, the database file was created but without any tables. All API calls failed with "no such table: settings" errors. The `initializeDatabase()` function creates all required tables (`rules`, `settings`, `sync_sessions`, `sync_transactions`) with `CREATE TABLE IF NOT EXISTS`.

**Root Cause:**
`Persistence.ensureDataDir()` only creates the `/app/data` directory. The actual table creation in `initializeDatabase()` was never called - it was only invoked in tests.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed
- Database tables will now be created automatically on first server start

---

## 2025-12-11 20:45 - Feature: Skip All / Unskip All Buttons

**What I did:**
Added "Skip All" and "Unskip All" buttons to the Sync Flow transaction review. These buttons allow users to quickly skip or unskip all visible transactions based on the currently active filter.

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Added new Msg types:
  - `SkipAllVisible`: Triggers bulk skip of visible transactions
  - `UnskipAllVisible`: Triggers bulk unskip of visible transactions
  - `BulkSkipCompleted`: Handles completion of individual skip operations
  - `BulkUnskipCompleted`: Handles completion of individual unskip operations

- `src/Client/Components/SyncFlow/State.fs` - Added:
  - `filterTransactions` helper (duplicated from View.fs for state logic)
  - Handler for `SkipAllVisible`: Gets visible non-skipped transactions, applies optimistic UI update, sends parallel API calls
  - Handler for `UnskipAllVisible`: Gets visible skipped transactions, restores status, sends parallel API calls
  - Handlers for `BulkSkipCompleted` / `BulkUnskipCompleted`: Silent success, rollback on error

- `src/Client/Components/SyncFlow/View.fs` - Added to action bar:
  - "Skip All (N)" ghost button - shows count, only visible when there are skippable transactions
  - "Unskip All (N)" ghost button with green icon - shows count, only visible when there are unskippable transactions
  - Buttons respect the active filter (All, Categorized, Uncategorized, Skipped, Confirmed Duplicates)

**Implementation Details:**
- Optimistic UI: Local state updates immediately for responsive feel
- Parallel API calls: Each transaction skip/unskip is sent in parallel for performance
- Filter-aware: Buttons only affect visible transactions based on current filter
- Error handling: On any error, reloads all transactions from server (rollback)
- Dynamic counts: Button text shows exact number of affected transactions

**Rationale:**
Users with many transactions needed a way to quickly skip/unskip batches instead of clicking individual skip buttons.

**Outcomes:**
- Build: ✅
- Tests: 294/294 passed
- No new tests needed (UI-only feature using existing skip/unskip API)

---

## 2025-12-11 18:30 - Feature: Amazon Order-ID Deep Links

**What I did:**
Implemented deep linking for Amazon transactions. When a transaction memo contains an Amazon order ID (format: ABC-1234567-1234567), the link now goes directly to the specific order details page instead of the generic order history.

**Reference Implementation:**
Based on the YNAB Amazon Linker browser extension (https://github.com/rommsen/ynab-amazon-linker).

**Files Modified:**
- `src/Server/RulesEngine.fs` - Added:
  - `amazonOrderIdPattern`: Regex pattern `\b([A-Z0-9]{3}-\d{7}-\d{7})\b` to match order IDs
  - `extractAmazonOrderId`: Function to extract order ID from transaction text (payee + memo)
  - Updated `generateAmazonLink`: Now returns deep link to order-details if ID found, else fallback to order-history

- `src/Tests/RulesEngineTests.fs` - Added/updated tests:
  - "Amazon order ID in memo generates deep link"
  - "Amazon order ID in payee generates deep link"
  - "Amazon without order ID generates history link"
  - Updated existing tests to work with new label format

**Link Formats:**
- With Order ID: `https://www.amazon.de/gp/your-account/order-details?ie=UTF8&orderID={orderId}` (Label: "Bestellung {orderId}")
- Without Order ID: `https://www.amazon.de/gp/your-account/order-history` (Label: "Amazon Orders")

**Rationale:**
Users frequently need to match Amazon transactions to specific orders. The deep link eliminates the need to search through order history manually.

**Outcomes:**
- Build: ✅
- Tests: 293/293 passed (added 3 new tests)
- No frontend changes needed (ExternalLinks already rendered correctly)

---

## 2025-12-11 16:45 - Setup: Serena MCP für F# Code-Analyse

**What I did:**
Konfigurierte Serena MCP (Model Context Protocol) für semantische F#-Code-Analyse. Serena ermöglicht symbolbasiertes Navigieren, Refactoring und intelligente Code-Bearbeitung.

**Problem:**
Serena MCP startete nicht korrekt - der F# Language Server (fsautocomplete) konnte nicht initialisiert werden.

**Root Causes:**
1. **Falsches project.yml Format**: Die Konfiguration verwendete `language_servers: fsharp: command: ...` statt dem erwarteten `languages: - fsharp`
2. **Fehlende Solution-Datei**: `fsautocomplete` benötigt eine `.sln`-Datei, die im Projekt fehlte

**Files Added:**
- `BudgetBuddy.sln` - Solution-Datei mit allen 4 Projekten (Shared, Client, Server, Tests)
- `.serena/memories/project_overview.md` - Projektübersicht
- `.serena/memories/tech_stack.md` - Technologie-Stack
- `.serena/memories/codebase_structure.md` - Codebase-Struktur
- `.serena/memories/code_style.md` - Code-Stil und Konventionen
- `.serena/memories/suggested_commands.md` - Entwicklungs-Commands
- `.serena/memories/task_completion.md` - Task-Completion Checklist

**Files Modified:**
- `.serena/project.yml` - Korrigiertes Format:
  ```yaml
  project_name: BudgetBuddy
  languages:
    - fsharp
  ignored_paths:
    - node_modules
    - .git
    - bin
    - obj
    - .fable
    - dist
  ```

**Rationale:**
Serena bietet semantische Code-Analyse über Language Server Protocol (LSP). Für F# wird `fsautocomplete` verwendet, das bereits als globales dotnet tool installiert war. Die Konfiguration musste nur angepasst werden.

**Key Learnings:**
- Serena erwartet einfaches `languages: - fsharp` Format (nicht verschachteltes `language_servers:`)
- F# Language Server benötigt zwingend eine `.sln`-Datei
- `.serena/` Ordner sollte eingecheckt werden (außer `/cache`)
- Nach Konfigurationsänderungen muss Claude Code komplett neu gestartet werden

**Outcomes:**
- Build: ✅
- Serena: ✅ Funktioniert - kann F#-Symbole analysieren
- Onboarding: ✅ 6 Memory-Dateien erstellt

---

## 2025-12-11 16:30 - Fix: JSON-Fehlermeldung bei frühem Import-Klick

**What I did:**
Fixed the bug where Comdirect JSON error responses were displayed as raw JSON instead of user-friendly messages. When clicking "I've Confirmed" before actually confirming the TAN in the banking app, users previously saw:

```
Network error (HTTP 400): {"code":"TAN_UNGUELTIG","messages":[{"severity":"INFO","key":"PUSHTAN_ANGEFORDERT","message":"TAN-Freigabe über die App wurde noch nicht erteilt.",...}]}
```

Now they see the clean message:
```
TAN-Freigabe über die App wurde noch nicht erteilt.
```

**Files Added:**
- None

**Files Modified:**
- `src/Server/Api.fs`:
  - Added `parseComdirectErrorJson` function (lines 68-110) that parses Comdirect's structured JSON error format
  - Extracts the `message` field from the `messages` array
  - Provides predefined messages for known error codes (TAN_UNGUELTIG, SESSION_EXPIRED, UNAUTHORIZED)
  - Updated `comdirectErrorToString` to use the parser for NetworkError cases
  - Also improved error messages for other ComdirectError variants

- `src/Client/Components/SyncFlow/State.fs`:
  - Updated `syncErrorToString` for `ComdirectAuthFailed` to pass through the reason directly (no redundant prefix)
  - Backend now provides user-friendly messages, so frontend just displays them

- `src/Tests/ComdirectClientTests.fs`:
  - Added 11 regression tests for `parseComdirectErrorJson`
  - Tests cover: TAN_UNGUELTIG with German message, multiple messages, missing fields, unknown codes, predefined codes, invalid JSON, empty input

**Rationale:**
Comdirect API returns errors in a structured JSON format with a `code` field and `messages` array. Previously, this JSON was passed through as-is, making it unreadable for users. The parser now extracts meaningful messages.

**Outcomes:**
- Build: ✅
- Tests: 45/46 Comdirect tests pass (1 skipped integration test)
- Backlog updated: Bug marked as completed

---

## 2025-12-11 15:35 - Fix: Sync Flow Progress Indicator for Fetching Step

**What I did:**
Fixed the sync flow progress indicator to show the correct step when fetching transactions. The issue had two parts:

1. **View Issue**: No dedicated view for `FetchingTransactions` status - only a generic loading view was shown
2. **State Issue**: The `confirmTan` API call runs synchronously (TAN confirm + fetch + rules + duplicate detection), so the client never saw the intermediate `FetchingTransactions` status

**Solution:**
- Added `fetchingView` component showing the progress indicator with "Fetch" as active step
- Added **optimistic UI update** in State.fs: When user clicks "I've Confirmed", we immediately set the local session status to `FetchingTransactions` before the API call starts

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`:
  - Added `fetchingView` component (lines 163-238) that shows:
    - Animated sync icon with neon glow
    - "Fetching Transactions" title and description
    - Progress indicator showing: Connected ✓ → TAN ✓ → Fetch (active with spinner)
  - Updated main view to use `fetchingView ()` for `FetchingTransactions` status

- `src/Client/Components/SyncFlow/State.fs`:
  - Modified `ConfirmTan` handler to optimistically update session status to `FetchingTransactions`
  - This ensures the UI shows the fetching view immediately while the API call runs

**Rationale:**
The `confirmTan` backend API runs all operations synchronously, so the intermediate status was never visible to the client. Optimistic UI update solves this by showing the expected state immediately.

**Outcomes:**
- Build: Server ✓, Client ✓
- Tests: Not applicable (UI-only change)
- Issues: None

---

## 2025-12-11 - Feature: Transparent Duplicate Detection with Debug Info

**What I did:**
Implemented a major improvement to the duplicate detection workflow to provide full transparency into how duplicates are detected. The system now clearly distinguishes between two separate mechanisms:
1. **BudgetBuddy's pre-import detection** (Reference/ImportId/Fuzzy matching BEFORE sending to YNAB)
2. **YNAB's rejection** (when YNAB rejects during import due to duplicate import_id)

**Key Changes:**

1. **Domain Types Extended** (`src/Shared/Domain.fs`)
   - Added `DuplicateDetectionDetails` record with diagnostic fields:
     - TransactionReference, ReferenceFoundInYnab, ImportIdFoundInYnab
     - FuzzyMatchDate, FuzzyMatchAmount, FuzzyMatchPayee
   - Updated `DuplicateStatus` to include details in all variants
   - Added `YnabImportStatus` type (NotAttempted | YnabImported | RejectedByYnab)
   - Added `YnabImportStatus` field to `SyncTransaction`

2. **Detection Logic Updated** (`src/Server/DuplicateDetection.fs`)
   - `detectDuplicate` now returns full diagnostic details
   - All three checks (Reference, ImportId, Fuzzy) are run and results captured

3. **API Updated** (`src/Server/Api.fs`)
   - `importToYnab` now sets `YnabImportStatus` on each transaction after YNAB response
   - `forceImportDuplicates` also sets `YnabImported` status

4. **UI Improvements** (`src/Client/Components/SyncFlow/View.fs`)
   - **Debug Info Panel**: Always visible when transaction expanded, shows:
     - Reference and whether found in YNAB
     - ImportId status (New vs Exists)
     - Fuzzy match details if applicable
     - YNAB import result with "BudgetBuddy missed this!" warning if applicable
   - **Separate Banners**:
     - Teal banner: "X pre-detected duplicates (BudgetBuddy)" [Pre-Import badge]
     - Red banner: "X rejected by YNAB" [Post-Import badge] with Force Re-import button
   - Count now includes `ynabRejected` for transactions rejected during import

**Files Added:**
- None (all changes to existing files)

**Files Modified:**
- `src/Shared/Domain.fs` - New types: DuplicateDetectionDetails, YnabRejectionReason, YnabImportStatus
- `src/Server/DuplicateDetection.fs` - detectDuplicate returns diagnostic details
- `src/Server/Persistence.fs` - Updated SyncTransaction creation with new fields
- `src/Server/RulesEngine.fs` - Updated SyncTransaction creation with new fields
- `src/Server/Api.fs` - importToYnab and forceImportDuplicates set YnabImportStatus
- `src/Client/Components/SyncFlow/View.fs` - Debug info panel, separate banners, updated counts
- `src/Tests/*.fs` - Updated all tests to use new DuplicateStatus format with details

**Rationale:**
Users were confused about why some transactions showed "Möchtest du X reimportieren?" because the two duplicate detection systems were not clearly distinguished. Now:
- Pre-import detection is clearly labeled and auto-skips confirmed duplicates
- Post-import rejections from YNAB are shown separately with explanation
- Each transaction shows exactly why BudgetBuddy made its detection decision

**Outcomes:**
- Build: All projects compile successfully
- Tests: 279/285 passed (6 integration tests skipped due to missing env)
- Issues: None

---

## 2025-12-11 - Bugfix: Force Re-import Button erschien vor YNAB-Import

**What I did:**
Fixed a bug where the "Re-import X Duplicate(s)" button appeared in the action bar BEFORE any import to YNAB had been attempted. The button was incorrectly counting all categorized, non-skipped, non-imported transactions instead of only YNAB-rejected transactions.

**Bug Details:**
- The action bar had a fallback logic that counted transactions where:
  - `Status <> Imported && Status <> Skipped && (CategoryId.IsSome || Splits.IsSome)`
- This was wrong because it showed the button for transactions that were simply ready to import, not rejected by YNAB

**Fix:**
Changed the `duplicateCount` calculation to only count transactions with `YnabImportStatus = RejectedByYnab _`:

```fsharp
let ynabRejectedCount =
    match model.SyncTransactions with
    | Success transactions ->
        transactions
        |> List.filter (fun tx ->
            match tx.YnabImportStatus with
            | RejectedByYnab _ -> true
            | _ -> false)
        |> List.length
    | _ -> 0
```

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs` - Fixed action bar button logic (lines 1199-1218)

**Rationale:**
The "Re-import Rejected" button should only appear AFTER a YNAB import has been attempted AND YNAB has rejected some transactions. Before import, `YnabImportStatus = NotAttempted` for all transactions, so the count is 0 and the button is hidden.

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- Issues: None - button now only appears when appropriate

---

## 2025-12-09 23:00 - Refactor: Stats Filter Semantics and Duplicate Banner

**What I did:**
Refactored the Stats-Filter system for clearer semantics and improved the Duplicate Banner.

**Changes:**
1. Renamed filter types for clarity:
   - `ReadyTransactions` → `CategorizedTransactions`
   - `PendingTransactions` → `UncategorizedTransactions`
   - `DuplicateTransactions` → `ConfirmedDuplicates`
2. Fixed count logic:
   - Categorized: Has CategoryId AND not Skipped/Imported
   - Uncategorized: No CategoryId AND not Skipped/Imported
   - ConfirmedDuplicates: Only `ConfirmedDuplicate` status (not `PossibleDuplicate`)
3. Updated Stats labels: "Ready" → "Categorized", "Pending" → "Uncategorized"
4. Improved Duplicate Banner: Now only shows ConfirmedDuplicates with better German explanation
5. Removed redundant "Transaktionen ohne Kategorie" warning banner (info now in Uncategorized stat)

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Renamed TransactionFilter cases
- `src/Client/Components/SyncFlow/View.fs` - Updated filterTransactions, count logic, labels, banner

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- UI: Clearer semantics, less clutter

---

## 2025-12-09 22:15 - Feature: Clickable Stats Filters for Transaction List

**What I did:**
Implemented clickable filter functionality for the Stats cards on the Sync Transactions page. Users can now click on Total, Ready, Pending, Skipped, or the Duplicates banner to filter the transaction list.

**Implementation:**
1. Added `TransactionFilter` discriminated union type with 5 filter options:
   - AllTransactions, ReadyTransactions, PendingTransactions, SkippedTransactions, DuplicateTransactions
2. Added `ActiveFilter` field to the SyncFlow Model
3. Added `SetFilter` message to Msg type
4. Extended Stats component with `OnClick` and `IsActive` props for interactivity
5. Added `filterTransactions` helper function to filter by status
6. Stats cards now show active state with teal ring highlight
7. Filter indicator shows "X of Y transactions" with "Show all" link
8. Empty state when filter matches no transactions

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs` - Added TransactionFilter DU, ActiveFilter field, SetFilter message
- `src/Client/Components/SyncFlow/State.fs` - Initialized ActiveFilter, added SetFilter handler
- `src/Client/Components/SyncFlow/View.fs` - Added filterTransactions, clickable Stats, filter UI
- `src/Client/DesignSystem/Stats.fs` - Added OnClick and IsActive props to StatProps

**Outcomes:**
- Build: ✅
- Tests: 279/285 passed (6 skipped integration tests)
- UX: Users can quickly filter to see only pending, ready, skipped, or duplicate transactions

---

## 2025-12-09 21:00 - UI: Breitere Selectbox & elegante Memo-Anzeige

**What I did:**
1. Category-Selectbox von `w-52` (208px) auf `w-72` (288px) verbreitert
2. Memo-Row neu gestaltet mit Glassmorphism-Stil passend zum Design-System

**Memo-Row Redesign:**
- Glassmorphism: `bg-base-200/30 backdrop-blur-sm border border-white/5`
- Abgerundete Karte mit Margin statt volle Breite
- Icon-Badge mit teal Akzent
- Uppercase Label "MEMO" + relaxed Text-Spacing

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `memoRow`: Komplett neu gestaltet (Zeile 484-512)
  - Desktop Layout: `w-52` → `w-72` (Zeile 649)

**Outcomes:**
- Build: ✅
- Selectbox: Kategorienamen besser lesbar
- Memo: Passt jetzt zum glassmorphism Design-System

---

## 2025-12-09 20:45 - Performance: Skipped Transactions ohne Selectbox

**What I did:**
Weitere Performance-Optimierung: Skipped Transactions rendern keine interaktive Selectbox mehr, sondern nur noch einen statischen Kategorienamen als Text.

**Rationale:**
- Die searchableSelect-Komponente ist teuer zu rendern (Event-Handler, State, DOM-Nodes)
- Skipped Transactions brauchen keine Interaktion - sie werden nicht importiert
- Bei 50% skipped = 50% weniger Selectboxen zu rendern

**Implementierung:**
1. Neue Hilfsfunktion `categoryText`: Sucht Kategorienamen aus vorberechneten Options
2. Bedingte Render-Logik: `if tx.Status = Skipped then Text else Selectbox`
3. Angewendet auf Mobile und Desktop Layout

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `categoryText`: Neue Hilfsfunktion für Text-Lookup
  - Mobile Layout (Zeile 533-551): Bedingte Selectbox/Text
  - Desktop Layout (Zeile 631-649): Bedingte Selectbox/Text

**Outcomes:**
- Build: ✅
- Tests: Nicht geändert (reine UI-Optimierung)
- Performance: Weniger DOM-Nodes, schnelleres Rendering

---

## 2025-12-09 20:15 - Performance: Category Dropdown 872x schneller

**What I did:**
Massive Performance-Optimierung der Category-Select-Dropdowns auf der Transaction-Seite. Das Öffnen eines Dropdowns dauerte vorher 15,7 Sekunden - jetzt nur noch 18ms.

**Root Cause:**
Die Category-Liste wurde für jede der 193 Transaktionszeilen neu berechnet:
- 193 Transaktionen × 160 Kategorien = **30.880 String-Operationen** pro Render
- Jedes Mal wurde `List.map` mit String-Interpolation aufgerufen

**Lösung Phase 1 (Category Options vorberechnen):**
- `categoryOptions` wird einmal vor der Transaktion-Liste berechnet
- Als Parameter an `transactionRow` übergeben statt `categories`
- Reduziert Berechnungen von 30.880 auf 160 pro Render

**Lösung Phase 2 (Optimistic UI):**
- `ManuallyCategorizedIds` wird sofort im Model aktualisiert (vor API-Call)
- "Create Rule" Button erscheint jetzt sofort nach Kategorisierung

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `transactionRow` Funktion: Signatur geändert, nimmt `categoryOptions` statt `categories`
  - Mobile/Desktop Category-Selects: Nutzen jetzt vorberechnete Options
  - `transactionListView`: Berechnet `categoryOptions` einmal vor der Schleife
- `src/Client/Components/SyncFlow/State.fs`
  - `CategorizeTransaction` Handler: Optimistisches Update von `ManuallyCategorizedIds`

**Performance-Ergebnis:**
| Metrik | Vorher | Nachher | Verbesserung |
|--------|--------|---------|--------------|
| Dropdown öffnen | 15.700ms | 18ms | **872x** |

**Outcomes:**
- Build: ✅
- Tests: Nicht geändert (UI-only Optimierung)
- Performance: Drastisch verbessert

---

## 2025-12-09 18:45 - UI: Transaktionsliste Aktionen sichtbar machen & Links verbessern

**What I did:**
Verbesserung der Transaktionslisten-UI: Aktions-Buttons (Skip, Create Rule) sind jetzt immer sichtbar statt nur bei Hover, und externe Links (Amazon, PayPal) sind klar als Links erkennbar.

**Probleme vorher:**
- Hover-Aktionen waren unsichtbar aber nahmen Platz ein → Geldbeträge nicht bündig
- Create Rule Button erschien/verschwand → Layout-Shift
- Amazon/PayPal-Links sahen aus wie normaler Text

**Lösung:**
1. **Aktionen nach vorne verschoben**: Jetzt zwischen Status-Indikatoren und Kategorie-Dropdown
2. **Feste Container-Breite**: `w-16` für Aktions-Container → stabile Layouts
3. **Platzhalter**: Wenn Create Rule Button nicht aktiv, wird ein unsichtbarer Platzhalter gerendert
4. **Link-Styling**: Teal-Farbe + External-Link-Icon für erkennbare Links

**Neues Layout (Desktop):**
```
[▶] [●] [⚠] | [Skip][Rule] | [Category ▾] | [Payee 🔗] | [Date] | [Amount]
```

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - `createRuleButton`: Gibt jetzt Platzhalter-div statt `Html.none` zurück
  - Desktop-Layout: Aktionen nach vorne verschoben, `opacity-0` entfernt, feste Breite
  - Mobile-Layout: Gleiche Änderungen für Konsistenz
  - External Links: Teal-Farbe + `Icons.externalLink` Icon

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed
- UI: Aktionen immer sichtbar, Beträge bündig, Links erkennbar

---

## 2025-12-09 16:30 - Feature: Regel aus Zuweisungs-Dialog erstellen

**What I did:**
Implementierung eines neuen Features, das es ermöglicht, direkt beim manuellen Kategorisieren einer Transaktion eine Regel zu erstellen. Nach dem Kategorisieren erscheint ein "Create Rule" Button, der ein Inline-Formular expandiert.

**Key Features:**
- Button erscheint nach manueller Kategorisierung einer Transaktion
- Inline-Formular expandiert unter der Transaktion (kein Modal-Unterbrechung)
- Pre-filled: Pattern (Payee), Kategorie, auto-generierter Rule Name
- Default: Contains-Regel (umschaltbar zu Exact/Regex)
- TargetField wählbar: Combined (default), Payee only, Memo only
- Auto-Apply: Neue Regel wird sofort auf andere pending Transaktionen angewandt
- Responsive: Gestackt auf Mobile, Grid auf Desktop

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs`
  - Neuer Type: `InlineRuleFormState` für Formular-State
  - Model erweitert um `InlineRuleForm` und `ManuallyCategorizedIds`
  - 12 neue Msg-Varianten für Inline-Rule-Creation Flow

- `src/Client/Components/SyncFlow/State.fs`
  - `init()` erweitert mit neuen Model-Feldern
  - `TransactionCategorized` trackt nun manuell kategorisierte IDs
  - Neue Helper-Funktionen: `matchesRule`, `rulesErrorToString`
  - Handler für alle neuen Messages: OpenInlineRuleForm, CloseInlineRuleForm, UpdateInlineRule*, SaveInlineRule, InlineRuleSaved, ApplyNewRuleToTransactions, TransactionsUpdatedByRule

- `src/Client/Components/SyncFlow/View.fs`
  - Neue Komponente: `createRuleButton` (Icon-Button für manuell kategorisierte Transaktionen)
  - Neue Komponente: `inlineRuleForm` (responsive Formular mit Name, Pattern, Type, TargetField)
  - `transactionRow` akzeptiert nun `InlineRuleForm` und `ManuallyCategorizedIds` Parameter
  - Mobile + Desktop: createRuleButton in Actions-Bereich integriert
  - Inline-Formular wird unter Transaktion angezeigt wenn aktiv

**UI-Flow:**
```
1. User kategorisiert Transaktion → [✓] ManualCategorized
2. "Create Rule" Button erscheint (🔧-Icon)
3. Klick → Inline-Formular expandiert
4. Pre-filled: Pattern="REWE", Name="Auto: REWE", Category=ausgewählte
5. User kann anpassen: PatternType, TargetField
6. "Create Rule" → API-Call → Toast "Rule created!"
7. Auto-Apply auf andere pending Transaktionen mit passendem Pattern
```

**Responsive Layout:**
- Mobile: Alle Felder vertikal gestackt, Buttons full-width
- Desktop: 12-Column Grid (Pattern: 6 cols, Type: 3 cols, TargetField: 3 cols)

**Rationale:**
Dieses Feature ermöglicht einen schnelleren Workflow beim Kategorisieren von Transaktionen. Statt in den Rules-Bereich zu wechseln, kann der User direkt aus dem Kontext heraus eine Regel erstellen.

**Outcomes:**
- Build: ✅ (Server + Client)
- Tests: 279/279 passed, 6 skipped
- Keine neuen Tests hinzugefügt (RulesEngine bereits umfassend getestet)

---

## 2025-12-09 - Fix: UI-Verbesserungen für kompakte Transaktionsliste

**What I did:**
Mehrere UI-Probleme behoben, die nach dem initialen Redesign der Transaktionsliste aufgefallen sind:

1. **Betrag-Formatierung**: Fable transpiliert `:F2` zu `%P(F2)` statt `.toFixed(2)` - jetzt explizites Rounding
2. **Status-Farben**: Skipped-Transaktionen jetzt immer grau (statt rot bei Duplicates)
3. **NeedsAttention gleiche Farbe**: Amazon/PayPal Transaktionen haben jetzt dieselbe Farbe wie andere Uncategorized
4. **Mobile Actions**: Buttons auf Mobile immer sichtbar (nicht hover-only)
5. **Größere Dropdown-Options**: Kategorie-Auswahl mit größerem Touch-Target
6. **Expand-Feature**: Chevron-Icon zum Anzeigen von Memo-Text

**Files Modified:**
- `src/Client/DesignSystem/Money.fs`
  - `formatAmount` nutzt jetzt `System.Math.Round(float absAmount, 2).ToString("0.00")` statt `:F2`
  - Kommentar hinzugefügt, der das Fable-Transpilations-Problem erklärt

- `src/Client/Components/SyncFlow/View.fs`
  - `statusDot`: Skipped hat jetzt Priorität über DuplicateStatus
  - `statusDot`: NeedsAttention nutzt jetzt `bg-neon-orange` (wie Pending)
  - `getRowStateClasses`: Skipped-Check vor Duplicate-Check
  - Mobile Actions: `md:opacity-0 md:group-hover:opacity-100` statt `opacity-0 group-hover:opacity-100`
  - Neue `expandChevron` Funktion für Memo-Expand-Icon
  - Neue `memoRow` Funktion für expandierbaren Memo-Text
  - `transactionRow` akzeptiert jetzt `expandedIds: Set<TransactionId>` Parameter
  - Layout enthält jetzt Chevron-Icon links vom Status-Dot

- `src/Client/DesignSystem/Input.fs`
  - Dropdown-Options: `px-4 py-3 text-base` statt `px-3 py-2 text-sm`
  - Max-Höhe: `max-h-80` statt `max-h-60`

- `src/Client/Components/SyncFlow/Types.fs`
  - Model erweitert um `ExpandedTransactionIds: Set<TransactionId>`
  - Neue Message: `ToggleTransactionExpand of TransactionId`

- `src/Client/Components/SyncFlow/State.fs`
  - `init()` erweitert mit `ExpandedTransactionIds = Set.empty`
  - Handler für `ToggleTransactionExpand`: Toggle Set-Membership

**Layout-Änderung Desktop:**
```
[▶/▼] [●] [Kategorie-Dropdown] [Payee...] [Datum] [Betrag] [Actions]
```

**Prioritäten für Farben:**
1. Skipped → immer grau (opacity-50)
2. ConfirmedDuplicate → rot (nur wenn nicht skipped)
3. PossibleDuplicate → orange pulsierend (nur wenn nicht skipped)
4. Pending/NeedsAttention → orange (gleiche Farbe!)
5. AutoCategorized → teal
6. ManualCategorized/Imported → grün

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Beträge werden korrekt formatiert: `-25.99 EUR`
- Skipped Duplicates sind grau
- Mobile Actions immer sichtbar
- Memo-Text durch Expand abrufbar

---

## 2025-12-09 - Redesign: Kompakte Transaktionsliste im Import-Flow

**What I did:**
Komplettes Redesign der Transaktions-Import-UI von unstrukturierten Karten (~100-120px Höhe) zu einer kompakten, scanbaren Liste (~44px Desktop, ~72px Mobile). Das neue Design folgt dem Mobile-First Ansatz aus dem Design System und stellt die Kategorie als zentrales Interaktionselement in den Fokus.

**Files Modified:**
- `src/Client/Components/SyncFlow/View.fs`
  - Neue Helper-Funktionen: `statusDot`, `getRowStateClasses`, `duplicateIndicator`, `skipToggleIcon`, `externalLinkIcon`, `formatDateCompact`
  - Neue Komponente: `transactionRow` mit Mobile/Desktop responsive Layout
  - Container von `space-y-3` auf Card-Style mit `border-white/5 divide-y` umgestellt
  - Alte `transactionCard` Funktion als DEPRECATED markiert (noch vorhanden für Referenz)

**Design-Änderungen:**
- **Status-Anzeige**: Von großen Badges zu kleinen farbigen Dots (8px)
  - Grün: Kategorisiert (manual/auto)
  - Teal: Auto-kategorisiert
  - Orange: Pending
  - Rot: Duplicate
  - Grau: Skipped
  - Pink (pulsierend): Needs Attention
- **Duplicate-Handling**: Inline-Banner entfernt, stattdessen Tooltip auf Icon
- **Kategorie-Auswahl**: Prominent links positioniert (FOKUS)
- **Actions**: Hover-only auf Desktop, immer sichtbar auf Mobile

**Layout-Struktur:**
```
Desktop: [●] [Category-Dropdown] [Payee...] [Date] [Amount] [Actions]
Mobile:  Line 1: [●] [Category-Dropdown] [Amount]
         Line 2: [Payee ...] [Date] [Actions]
```

**Touch-Optimierung:**
- Skip-Button: `min-w-[44px] min-h-[44px]` für Touch-Targets
- Kategorie-Dropdown nutzt existierenden `searchableSelect` mit Touch-Support

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- ~2.5x mehr Transaktionen auf Desktop sichtbar
- ~1.5x mehr Transaktionen auf Mobile sichtbar
- Klarer Scan-Pfad: Status → Kategorie → Details → Betrag

---

## 2025-12-09 - Feature: Inline-Bestätigung für Rule-Löschen

**What I did:**
Schutz vor versehentlichem Löschen von Rules durch Inline-Bestätigung (MVU-konform). Beim Klick auf das Trash-Icon erscheint ein roter "Löschen?"-Button, der nach 3 Sekunden automatisch zurück zum normalen Icon wechselt.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs`
  - Neues Model-Feld: `ConfirmingDeleteRuleId: RuleId option`
  - Neue Messages: `ConfirmDeleteRule of RuleId`, `CancelConfirmDelete`
- `src/Client/Components/Rules/State.fs`
  - `init()` erweitert mit `ConfirmingDeleteRuleId = None`
  - Handler für `ConfirmDeleteRule`: Setzt State + startet 3s Timeout-Cmd
  - Handler für `CancelConfirmDelete`: Setzt State zurück
  - `DeleteRule` setzt ebenfalls `ConfirmingDeleteRuleId = None`
- `src/Client/Components/Rules/View.fs`
  - `ruleRow` bekommt `model` als ersten Parameter
  - Delete-Button zeigt konditionell: Trash-Icon oder roten "Löschen?"-Button
  - Button hat `animate-pulse` für visuelle Aufmerksamkeit

**Technische Details:**
- Vollständig MVU-konform: Kein lokaler React-State, alle Änderungen über Messages
- Timeout via `Cmd.OfAsync.perform` mit `Async.Sleep 3000`
- Nur eine Rule kann gleichzeitig im Confirm-Modus sein

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Versehentliches Löschen wird durch Zwei-Klick-Mechanismus verhindert

---

## 2025-12-09 - Feature: Einzeilige Rules-Darstellung + Legende

**What I did:**
Rules-Darstellung von mehrzeiliger Card-Ansicht auf kompakte einzeilige Zeilen umgestellt. Die neue Darstellung zeigt alle wichtigen Informationen in einer Zeile: Toggle, Pattern-Type-Icon, Name, Pfeil, Kategorie, und Aktions-Buttons. Zusätzlich wurde eine Legende für die Pattern-Type-Icons hinzugefügt.

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/Components/Rules/View.fs`
  - Neue `patternTypeIcon` Funktion für kompakte Pattern-Type-Anzeige (Regex: `.*`, Contains: `~`, Exact: `=`)
  - `ruleCard` ersetzt durch kompakte `ruleRow` Komponente
  - Single-Line Layout: `[Toggle] [PatternIcon] [Name...] → [Category...] [Edit][Delete]`
  - Pattern-Type-Icon auf kleinen Screens versteckt (`hidden sm:block`)
  - Actions auf Desktop nur bei Hover sichtbar (`sm:opacity-0 sm:group-hover:opacity-100`)
  - Name und Pattern als Tooltip verfügbar
  - Spacing reduziert: `space-y-1.5` statt `gap-3`
  - **Legende im Info-Tip hinzugefügt**: Zeigt alle Pattern-Types mit Icons (`~ Contains`, `= Exact`, `.* Regex`)

**Technische Details:**
- Pattern-Type als kompaktes Icon-Badge: farbcodiert (Purple=Regex, Teal=Contains, Green=Exact)
- Flexbox-Layout mit truncate für Überlauf-Handling
- Responsive: Auf Mobile immer Actions sichtbar, auf Desktop nur bei Hover
- Tooltip zeigt vollständigen Namen und Pattern bei Hover über Namen
- Legende responsive: Auf Desktop neben Info-Text, auf Mobile darunter

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Rules nehmen jetzt deutlich weniger Platz ein (ca. 1/3 der vorherigen Höhe)
- Mehr Rules auf einen Blick sichtbar
- Pattern-Type-Icons sind durch Legende erklärt

---

## 2025-12-08 - Feature: Suchbare Kategorie-Selectboxen mit Keyboard-Navigation

**What I did:**
Neue SearchableSelect Komponente implementiert, die eine durchsuchbare Dropdown-Liste für Kategorien bereitstellt. Beim Öffnen erscheint ein Textfeld mit Auto-Fokus, das die Kategorien mit "contains"-Logik filtert (case-insensitive Suche in der Mitte des Namens möglich). Vollständige Keyboard-Navigation hinzugefügt.

**Files Added:**
- Keine neuen Dateien

**Files Modified:**
- `src/Client/DesignSystem/Input.fs`
  - Neue `SearchableSelectProps` Type-Definition
  - Neue `SearchableSelect` React-Komponente mit:
    - Click-outside Detection zum Schließen
    - Auto-Focus auf Suchfeld beim Öffnen
    - Case-insensitive Contains-Filter
    - **Vollständige Keyboard-Navigation:**
      - ⬆️⬇️ Arrow Up/Down zum Navigieren
      - ↵ Enter zum Auswählen
      - ⎋ Escape zum Schließen
      - ⇥ Tab zum Schließen
      - Wrap-around an Liste-Enden
    - Visuelles Highlighting der aktuell hervorgehobenen Option
    - Auto-Scroll zur hervorgehobenen Option (nur bei Tastatur-Navigation)
    - Mouse-Hover aktualisiert auch den Highlight-Index
  - Neue `searchableSelect` Helper-Funktion
  - SVG-Warnungen behoben (Html.svg → Svg.svg)
- `src/Client/Components/SyncFlow/View.fs`
  - `selectWithPlaceholder` ersetzt durch `searchableSelect` für Kategorie-Auswahl
- `src/Client/Components/Rules/View.fs`
  - `selectWithPlaceholder` ersetzt durch `searchableSelect` für Kategorie-Auswahl

**Technische Details:**
- React Hooks: `useState` für isOpen/searchText/highlightedIndex/isKeyboardNav, `useRef` für Container/Input/List
- useEffect für Click-outside-Detection, Auto-Focus, und Auto-Scroll
- Filter: `label.ToLowerInvariant().Contains(searchLower)`
- Index 0 = Clear/Placeholder Option, Index 1+ = gefilterte Optionen
- Dropdown zeigt "No matches found" bei leerer Ergebnisliste
- `data-option-index` Attribut für DOM-Abfrage beim Scrolling

**Bug-Fix: Scroll-Verhalten in Modals:**
- Problem: Mouse-Hover triggerte `scrollIntoView()`, was das gesamte Modal/Fenster scrollte
- Lösung: Neuer `isKeyboardNav` State unterscheidet zwischen Maus- und Tastatur-Navigation
- Auto-Scroll nur bei Tastatur-Navigation via manuelles `list.scrollTop` (scrollt nur innerhalb der Dropdown-Liste)
- `setHighlightFromMouse` Helper setzt `isKeyboardNav = false` bei Mouse-Events

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Beide Kategorie-Selectboxen sind jetzt durchsuchbar und per Tastatur navigierbar
- Maus-Scrollen funktioniert ohne unerwünschtes Seiten-/Modal-Scrolling

---

## 2025-12-08 - Fix: Langsame Kategorie-Auswahl (Optimistisches UI)

**What I did:**
Bug behoben, bei dem die Auswahl einer Kategorie in der Selectbox fast eine Sekunde dauerte. Die Ursache war, dass das UI auf die Backend-Antwort gewartet hat, bevor die Kategorie-Auswahl angezeigt wurde ("pessimistisches UI"). Jetzt wird das Model sofort lokal aktualisiert (optimistisches UI) und der API-Call läuft im Hintergrund.

**Problem:**
- Bei `CategorizeTransaction` wurde das Model NICHT aktualisiert, nur ein API-Call gestartet
- Erst bei `TransactionCategorized` (nach ~500-1000ms) wurde das Model aktualisiert
- Benutzer musste auf jede Kategorie-Auswahl warten

**Lösung:**
- Optimistisches UI: Model wird sofort lokal aktualisiert mit der neuen Kategorie
- API-Call läuft im Hintergrund
- Bei Fehler: Rollback durch Neuladen der Transaktionen

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs`
  - `CategorizeTransaction` aktualisiert jetzt sofort das lokale Model
  - `TransactionCategorized (Error _)` löst jetzt `LoadTransactions` aus für Rollback

**Technische Details:**
- Die Kategorie-Auswahl aktualisiert jetzt sofort:
  - `CategoryId` und `CategoryName`
  - `Status` zu `ManualCategorized` (oder `Pending` bei Leer-Auswahl)
  - `Splits` wird auf `None` gesetzt
- Bei Fehler werden Transaktionen vom Server neu geladen (konsistenter Zustand)

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped
- Kategorie-Auswahl ist jetzt sofort sichtbar (keine Verzögerung mehr)

---

## 2025-12-07 22:45 - Entferne Comdirect Zeilennummern-Präfixe aus Memos

**What I did:**
Comdirect sendet den Verwendungszweck (remittanceInfo) mit Zeilennummern-Präfixen wie "01", "02", etc. Diese erschienen in der UI vor jedem Memo (z.B. "01REWE..." statt "REWE..."). Eine Regex-Funktion hinzugefügt, die diese Präfixe beim Parsen entfernt.

**Files Modified:**
- `src/Server/ComdirectClient.fs` - Neue Funktion `removeLineNumberPrefixes` hinzugefügt und beim Parsen von `remittanceInfo` angewendet
- `src/Server/Server.fsproj` - `InternalsVisibleTo` für Tests hinzugefügt
- `src/Tests/ComdirectDecoderTests.fs` - 10 Unit-Tests für die neue Funktion hinzugefügt

**Technische Details:**
- Regex-Pattern: `(^|\n)\d{2}(?=[A-Za-zÄÖÜäöüß])` - matcht 2-stellige Zahlen am Anfang oder nach Zeilenumbruch, gefolgt von Buchstaben
- Unterstützt deutsche Umlaute
- Trim() entfernt übrige Leerzeichen
- Funktion ist `internal` um direktes Unit-Testing zu ermöglichen

**Outcomes:**
- Build: ✅
- Tests: 279/279 passed, 6 skipped (integration tests) - 10 neue Tests
- Memos erscheinen jetzt ohne "01"-Präfix

---

## 2025-12-07 21:15 - Legacy Rules Import Script

**What I did:**
Created a reusable F# script to import categorization rules from the legacy `rules.yml` file into the BudgetBuddy database.

**Files Added:**
- `scripts/import-rules.fsx` - Import script that:
  - Parses YAML rules (match/category pairs)
  - Fetches YNAB categories to match names to IDs
  - Inserts rules into SQLite database
  - Supports `--list` flag to show available budgets
  - Supports budget selection by name parameter

**Usage:**
```bash
dotnet fsi scripts/import-rules.fsx --list              # List budgets
dotnet fsi scripts/import-rules.fsx "My Budget"         # Import with specific budget
dotnet fsi scripts/import-rules.fsx --clear "My Budget" # Clear all + reimport
```

**Features:**
- Category matching: exact name, GroupName/Name format, case-insensitive
- Duplicate detection: skips rules that already exist
- Clear reporting: shows imported/skipped counts and missing categories

**Outcomes:**
- Successfully imported 55 of 56 rules from rules.yml
- 1 category not found in YNAB: "Krankenversicherung Lena Debeka"
- Script is reusable for future database resets

---

## 2025-12-07 19:50 - Increased Memo Limit to 300 + Whitespace Compression

**What I did:**
1. Increased memo character limit from 200 to 300 (testing if YNAB accepts it)
2. Added whitespace compression: multiple spaces/tabs/newlines become single space

**Why:**
- Old GitHub issue (2019) claimed 100-char limit, but that may be outdated
- Testing with 300 to see actual limit
- Whitespace compression saves characters for actual content

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `memoLimit = 300` constant
  - Added `compressWhitespace` function using regex `\s+` → single space
  - Applied compression before truncation

- `src/Tests/DuplicateDetectionTests.fs`:
  - Updated limit from 200 to 300
  - Added 3 new tests for whitespace compression

- `src/Tests/YnabClientTests.fs`:
  - Updated memo tests to reflect new behavior

**Outcomes:**
- Build: ✅ success
- Tests: 269 passed (6 skipped integration tests)

---

## 2025-12-07 19:40 - Fixed Memo Truncation Breaking Duplicate Detection

**What I did:**
Fixed a critical bug where memos were being truncated from the END, causing the Comdirect reference to be cut off. This broke duplicate detection because the reference is appended as ", Ref: <reference>" at the end of the memo.

**Root Cause:**
The old `truncateMemo` function truncated long memos to 197 characters + "..." = 200 chars (YNAB limit). But the reference was NOT being appended to the memo at all when sending to YNAB! The duplicate detection system (`DuplicateDetection.fs`) expects to find "Ref: <reference>" at the end of YNAB memos to match transactions.

**The Fix:**
1. Created new `buildMemoWithReference` function that:
   - Appends ", Ref: <reference>" to the memo
   - If total > 200 chars: truncates from the BEGINNING (preserving the reference)
   - Format when truncated: "...<truncated memo>, Ref: <reference>"

2. Kept simple `truncateSplitMemo` for split transaction memos (they don't need a reference since the parent transaction has one)

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Replaced `truncateMemo` with `buildMemoWithReference(memo, reference)`
  - Added `truncateSplitMemo` for split transactions (line 220-225)
  - Added `buildMemoWithReference` function (lines 227-246)
  - Updated memo construction to include reference (line 355)
  - Updated split memo to use `truncateSplitMemo` (line 313)

- `src/Tests/DuplicateDetectionTests.fs`:
  - Added 6 regression tests for memo with reference behavior (lines 460-568)
  - Tests verify reference is always extractable after truncation

**Why This Matters:**
Without the reference in the YNAB memo, transactions imported to YNAB could not be detected as duplicates on subsequent syncs. This would lead to duplicate transactions in YNAB.

**Outcomes:**
- Build: ✅ success
- Tests: 266 passed (6 skipped integration tests)
- All regression tests for memo truncation pass

---

## 2025-12-07 - YNAB Import: Uncleared Transactions + Optional Categories

**What I did:**
Implemented two high-priority features from the backlog that allow more flexible YNAB imports:
1. All transactions are now imported as "uncleared" instead of "cleared"
2. Transactions without categories can now be imported (appear in YNAB's Uncategorized view)

**Why:**
- **Uncleared**: Users can review and manually clear transactions in YNAB after import
- **Optional categories**: Supports YNAB's native uncategorized workflow - users can categorize later in YNAB

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Changed `Cleared = "cleared"` to `Cleared = "uncleared"` (line 342)
  - Removed category requirement from filter (lines 309-313)
  - Handle `CategoryId = None` case without throwing (lines 354-361)

- `src/Server/Api.fs`:
  - Updated import filter to include `Pending` status (lines 835-843)
  - Renamed `categorized` variable to `toImport` for clarity
  - Updated all references throughout the function

- `src/Client/Components/SyncFlow/View.fs`:
  - Import button now enabled for any non-skipped, non-imported transaction (lines 410-418)
  - Added warning banner for uncategorized transactions (lines 402-435)

- `src/Tests/YnabClientTests.fs`:
  - Added test "transaction is encoded with cleared=uncleared"
  - Added test "uncategorized transaction is encoded without category_id"
  - Updated "uncategorized transactions pass filter" test (previously verified filter-out)
  - Updated existing tests to use "uncleared" instead of "cleared"

**UI Changes:**
- Warning banner appears when importing uncategorized transactions
- Shows count: "X Transaktion(en) ohne Kategorie"
- Subtitle: "Diese werden als 'Uncategorized' in YNAB importiert."

**Outcomes:**
- Build: ✅ success
- Tests: 260/266 passed (6 skipped integration tests)
- Both features working as expected

---

## 2025-12-07 - Add Comprehensive Tests for SyncSessionManager

**What I did:**
Implemented comprehensive test coverage for the SyncSessionManager module, which previously had ZERO test coverage. This was identified as a critical gap by the QA milestone reviewer.

**Files Added:**
- `src/Tests/SyncSessionManagerTests.fs` - Comprehensive tests covering:
  - Session Lifecycle Tests (11 tests): startNewSession, getCurrentSession, completeSession, failSession, clearSession, updateSessionStatus, updateSession, and error handling
  - Transaction Operations Tests (14 tests): addTransactions, getTransactions, getTransaction, updateTransaction, updateTransactions, getStatusCounts, updateSessionCounts
  - Session Validation Tests (7 tests): validateSession, validateSessionStatus with various scenarios
  - Edge Cases and Integration Tests (6 tests): workflow simulation, state transitions, transaction overwrites

**Files Modified:**
- `src/Tests/Tests.fsproj` - Added SyncSessionManagerTests.fs to compilation

**Technical Notes:**
- Used `testSequenced` for all test lists because SyncSessionManager uses global mutable state (`currentSession : SessionState option ref`)
- Each test calls `resetSession()` at start to ensure test isolation
- Tests verify actual behavior, not tautologies (e.g., verifying that completeSession sets both status AND timestamp)

**Test Summary:**
- Session Lifecycle Tests: 11 tests
- Transaction Operations Tests: 14 tests
- Session Validation Tests: 7 tests
- Edge Cases and Integration: 6 tests
- Total new tests: 38

**Outcomes:**
- Build: success
- Tests: 258/264 passed (6 skipped integration tests that require .env credentials)
- All SyncSessionManager functionality now has test coverage

---

## 2025-12-07 - Fix TAN Confirmation Double-Click Bug

**What I did:**
Fixed a race condition bug where clicking the "I've Confirmed" button twice during TAN confirmation caused an error toast "Invalid session state. Expected: AwaitingTan, Actual: FetchingTransactions". The issue was that the button had no loading state, so users would click again thinking nothing happened.

**Root Cause:**
1. User clicks "I've Confirmed" → `ConfirmTan` message sent
2. Backend receives request, immediately changes session status to `FetchingTransactions`
3. Button remains active (no loading state)
4. User clicks again → second `ConfirmTan` sent
5. Backend validates session is in `AwaitingTan` state, but it's already `FetchingTransactions` → Error

**Fix:**
Added `IsTanConfirming: bool` flag to track when TAN confirmation is in progress. The button now shows a loading state and subsequent clicks are ignored.

**Files Modified:**
- `src/Client/Components/SyncFlow/Types.fs`:
  - Added `IsTanConfirming: bool` to Model

- `src/Client/Components/SyncFlow/State.fs`:
  - Initialize `IsTanConfirming = false` in `init`
  - Set `IsTanConfirming = true` when `ConfirmTan` is dispatched (ignore if already true)
  - Reset `IsTanConfirming = false` when `TanConfirmed` is received (success or error)

- `src/Client/Components/SyncFlow/View.fs`:
  - Updated `tanWaitingView` to accept `isConfirming: bool` parameter
  - Button shows "Importing..." with loading spinner when confirming
  - Button shows normal "I've Confirmed" when idle

**Outcomes:**
- Build: success
- Tests: 220/227 passed (1 unrelated failure in Persistence Type Conversions test, 6 skipped integration tests)
- Button now shows loading state, preventing double-clicks

---

## 2025-12-07 - Add Force Re-Import for YNAB Duplicates

**What I did:**
Implemented a complete "Force Re-Import" feature for transactions that YNAB rejects as duplicates. This solves the problem where deleted transactions in YNAB can't be re-imported because YNAB remembers the import_id forever.

**Flow:**
1. User clicks "Import to YNAB"
2. YNAB responds with duplicates
3. Toast shows "Imported X transaction(s). Y already exist in YNAB."
4. Button appears: "Re-import Y Duplicate(s)"
5. User clicks → transactions are sent with new UUIDs
6. Success!

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `forceNewImportId: bool` parameter to `createTransactions`
  - Normal import uses Transaction-ID based import_id (duplicate protection)
  - Force import uses new UUID (bypasses YNAB's duplicate detection)

- `src/Shared/Api.fs`:
  - Added `ImportResult` type with `CreatedCount` and `DuplicateTransactionIds`
  - Changed `importToYnab` return type from `int` to `ImportResult`
  - Added new `forceImportDuplicates` endpoint

- `src/Server/Api.fs`:
  - Updated `importToYnab` to return `ImportResult` with duplicate transaction IDs
  - Implemented `forceImportDuplicates` endpoint using `forceNewImportId = true`

- `src/Client/Components/SyncFlow/Types.fs`:
  - Added `DuplicateTransactionIds: TransactionId list` to Model
  - Added `ForceImportDuplicates` and `ForceImportCompleted` messages

- `src/Client/Components/SyncFlow/State.fs`:
  - Handle `ImportCompleted` with `ImportResult`
  - Handle `ForceImportDuplicates` and `ForceImportCompleted`

- `src/Client/Components/SyncFlow/View.fs`:
  - Added "Re-import X Duplicate(s)" button when duplicates exist

**Outcomes:**
- Build: ✅ (Server + Client)
- Tests: 221/221 passed
- Feature working end-to-end

**Final Cleanup (17:45):**
- Removed all debug `printfn` statements from `Api.fs`
- Verified all tests still pass after cleanup

---

## 2025-12-07 - Fix YNAB False Success Reports + Duplicate Handling

**What I did:**
Fixed two related bugs:
1. The app reported successful transaction transfer even when YNAB rejected them as duplicates
2. Duplicate transactions were still marked as "Imported" in the UI

**Problem 1 - False Success Count:**
The `createTransactions` function returned the count of **sent** transactions instead of **actually created** ones.

**Problem 2 - Incorrect Status Marking:**
Even when YNAB rejected transactions as duplicates, they were marked as `Imported` in the local state because the code blindly marked all categorized transactions as imported.

**Solution:**
1. **New type `TransactionCreateResult`** in `YnabClient.fs`:
   - `CreatedCount: int` - actual number of created transactions
   - `DuplicateImportIds: string list` - import IDs that were rejected

2. **Updated `Api.fs` import logic:**
   - Parse `duplicate_import_ids` from YNAB response
   - Only mark transactions as `Imported` if they weren't in the duplicates list
   - Duplicate transactions keep their original status (not marked as imported)

3. **Added logging** to see YNAB request/response for debugging

**Files Modified:**
- `src/Server/YnabClient.fs`:
  - Added `TransactionCreateResult` type
  - Changed `createTransactions` return type from `int` to `TransactionCreateResult`
  - Added request/response logging
  - Parse and return duplicate import IDs

- `src/Server/Api.fs`:
  - Parse duplicate import IDs to match against transaction IDs
  - Only mark actually-created transactions as `Imported`
  - Duplicates retain their original status

- `src/Tests/YnabClientTests.fs`:
  - Added 4 regression tests for response parsing

**Tests Added:**
- `correctly counts created transactions from YNAB response`
- `correctly identifies duplicate transactions from YNAB response`
- `handles response with all transactions rejected as duplicates`
- `handles response missing duplicate_import_ids field`

**Outcomes:**
- Build: ✅
- Tests: 221/221 passed (6 integration tests skipped)
- UI now correctly shows actual imported count
- Duplicate transactions are no longer falsely marked as imported

---

## 2025-12-08 01:15 - Add Mandatory Bug Fix Protocol to CLAUDE.md

**What I did:**
Added a new "Bug Fix Protocol (MANDATORY)" section to CLAUDE.md that requires regression tests for every bug fix.

**Motivation:**
Two bugs were fixed today that could have been caught earlier with proper tests:
1. Stale reference bug in `completeSession()`
2. JSON encoding bug using `Encode.int64` instead of `Encode.int`

Neither would have regressed if the original code had proper tests.

**Files Modified:**
- `CLAUDE.md` - Added "Bug Fix Protocol (MANDATORY)" section with:
  - 5-step process for fixing bugs
  - Test requirements for bug fixes
  - Example test comment format
  - Explanation of why this matters

**Key Points Added:**
1. Every bug fix MUST include a regression test
2. Write failing test FIRST, then fix
3. Test should include comment explaining what bug it prevents
4. Run full test suite to verify no regressions

---

## 2025-12-08 01:00 - Fix YNAB Transactions Not Being Created (JSON Encoding Bug)

**What I did:**
Fixed a critical bug where transactions were not actually being created in YNAB despite the API appearing to succeed.

**Problem:**
`Encode.int64` in Thoth.Json.Net serializes 64-bit integers as **strings** (because JavaScript can't handle 64-bit integers):
```json
"amount": "-50250"   // WRONG - string with quotes
```
But YNAB API expects a **number**:
```json
"amount": -50250     // CORRECT - number without quotes
```
The YNAB API was likely rejecting the transactions silently or misinterpreting the amount.

**Solution:**
1. Changed `Amount` field from `int64` to `int` in `YnabTransactionRequest` and `YnabSubtransactionRequest` types
2. Changed `Encode.int64` to `Encode.int` in encoders
3. Changed `int64` to `int` in amount conversion code
4. Added regression tests to verify amounts are serialized as JSON numbers

**Files Modified:**
- `src/Server/YnabClient.fs` - Changed int64 to int for Amount fields and encoders
- `src/Tests/YnabClientTests.fs` - Added 2 new tests for JSON encoding verification

**Outcomes:**
- Build: ✅
- Tests: 217/217 passed (2 new tests)
- YNAB transactions should now be created correctly

**Test Coverage Gap Identified:**
There were no tests verifying the JSON format sent to YNAB. The bug existed because:
1. Tests only verified data transformations, not the actual HTTP request body format
2. No integration test with YNAB API mocking

---

## 2025-12-08 00:30 - Fix Sync Complete Screen Showing Wrong Import Counts

**What I did:**
Fixed a bug where the "Sync Complete" screen always showed 0 IMPORTED and 0 SKIPPED even when transactions were successfully imported to YNAB.

**Problem:**
In `SyncSessionManager.fs`, the `completeSession()` function had a stale reference bug:
1. It matched on `currentSession.Value` and captured `state`
2. Called `updateSessionCounts()` which updated `currentSession.Value` with correct counts
3. Then used the OLD `state.Session` (with 0 counts) to create the completed session
4. This overwrote the updated counts with 0

**Solution:**
Re-read `currentSession.Value` after calling `updateSessionCounts()` to get the session with the updated ImportedCount and SkippedCount.

**Files Modified:**
- `src/Server/SyncSessionManager.fs` - Fixed `completeSession()` to re-read session state after `updateSessionCounts()`

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- Sync Complete screen should now show correct import/skipped counts

---

## 2025-12-07 23:45 - Fix YNAB JSON Serialization Error for Transactions

**What I did:**
Fixed a bug where syncing transactions to YNAB failed with "Could not determine JSON object type for type <>f__AnonymousType...".

**Problem:**
The `createTransactions` function in `YnabClient.fs` used F# anonymous types (`{| ... |}`) that were cast to `obj` and passed to `Encode.Auto.generateEncoder()`. Thoth.Json cannot automatically serialize anonymous types.

**Solution:**
1. Defined proper F# record types `YnabTransactionRequest` and `YnabSubtransactionRequest`
2. Created manual encoders `encodeTransaction` and `encodeSubtransaction` using `Encode.object`
3. Replaced `Encode.Auto.generateEncoder()` with `Encode.list (ynabTransactions |> List.map encodeTransaction)`

**Files Modified:**
- `src/Server/YnabClient.fs` - Added record types and manual encoders, refactored `createTransactions` to use them

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- YNAB sync should now work correctly

---

## 2025-12-07 22:15 - Fix SyncFlow Category Loading Race Condition

**What I did:**
Fixed a bug where categories could not be selected in the SyncFlow transaction review. The category dropdown was always empty because categories were never loaded.

**Problem:**
1. `LoadCategories` in `SyncFlow/State.fs` did nothing (`Cmd.none`) - it was marked as "simplified for now"
2. The parent `Client/State.fs` intercepted `LoadCategories` but only loaded categories if Settings were already loaded (race condition)
3. If Settings weren't loaded yet when navigating to SyncFlow, categories never loaded

**Solution:**
1. Made `LoadCategories` in SyncFlow self-sufficient - it now fetches settings first to get the budget ID, then loads categories from YNAB API
2. Removed the parent interception - `LoadCategories` is now passed through to the child component

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs` - Implemented `LoadCategories` handler to fetch settings and categories independently
- `src/Client/State.fs` - Removed special handling for `LoadCategories`, now passes message to child

**Outcomes:**
- Build: ✅
- Category selection now works in SyncFlow
- Skip button confirmed working

---

## 2025-12-07 18:30 - Simplify Transaction Import Flow (Remove Selection, Add Unskip)

**What I did:**
Simplified the transaction import flow by removing the confusing checkbox selection mechanism and adding proper Skip/Unskip functionality with auto-skip for confirmed duplicates.

**Problem:**
The previous UI had two conflicting mechanisms:
1. Checkbox selection (Select All/None)
2. Skip button per transaction

This was confusing because the frontend validated based on selection, but the backend ignored selection entirely and just imported all categorized transactions. Users didn't know which transactions would actually be imported.

**Solution:**
1. **Removed selection UI entirely** - no more checkboxes, Select All/None buttons, or selection count badge
2. **Added Unskip functionality** - skipped transactions can now be restored
3. **Auto-skip confirmed duplicates** - transactions with `ConfirmedDuplicate` status are automatically skipped during sync
4. **Simplified import logic** - all non-skipped transactions with categories are imported

**Files Added:**
- None

**Files Modified:**
- `src/Shared/Api.fs` - Added `unskipTransaction` API endpoint
- `src/Server/Api.fs` - Added auto-skip for ConfirmedDuplicate, implemented `unskipTransaction` endpoint
- `src/Client/Components/SyncFlow/Types.fs` - Removed `SelectedTransactions` from Model, removed selection Msg types, added `UnskipTransaction` and `TransactionUnskipped`
- `src/Client/Components/SyncFlow/State.fs` - Removed selection handlers, added Unskip handlers
- `src/Client/Components/SyncFlow/View.fs` - Removed checkbox, Select All/None buttons, selection badge; updated `canImport` logic; added Skip/Unskip toggle button
- `src/Client/DesignSystem/Icons.fs` - Added `undo` icon for Unskip button

**Files Deleted:**
- None

**Rationale:**
- The dual selection+skip mechanism was confusing and the frontend/backend were inconsistent
- Auto-skipping confirmed duplicates reduces user effort and prevents accidental duplicate imports
- Unskip allows users to correct mistakes or import transactions that were auto-skipped

**New Import Logic:**
- Import all transactions where:
  - `Status != Skipped`
  - `CategoryId.IsSome`

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped - integration tests)
- UI is now simpler and more intuitive

---

## 2025-12-07 17:00 - Fix Modal Animation Flicker

**What I did:**
Fixed the Rules Modal flickering issue where a brief flash of the modal at full opacity appeared before the animation started.

**Root Cause:**
The CSS animation used `animation-fill-mode: forwards`, which only preserves the END state of the animation. The element was briefly visible at full opacity before the animation started (which sets opacity to 0), causing a flicker.

**Solution:**
Changed `animation-fill-mode` from `forwards` to `both`:
- `backwards`: Applies initial animation styles (opacity: 0, scale: 0.95) BEFORE the animation starts
- `forwards`: Preserves final animation styles after it ends
- `both`: Does both - element is invisible from the start, then smoothly animates in

**Files Modified:**
- `src/Client/styles.css`:
  - Changed `.animate-scale-in` from `forwards` to `both`

- `src/Client/DesignSystem/Modal.fs`:
  - Removed animation from backdrop (only modal content animates now)
  - Added `ModalInternal` React component with `useRef` to track animation state (prevents re-animation on re-renders)

**Technical Details:**
```css
/* Before (flickered) */
.animate-scale-in {
  animation: scaleIn 0.3s var(--transition-spring) forwards;
}

/* After (no flicker) */
.animate-scale-in {
  animation: scaleIn 0.3s var(--transition-spring) both;
}
```

**Rationale:**
With `forwards` only, the browser renders the element at its default state (visible), then starts the animation which sets opacity to 0, causing a brief flash. With `both`, the initial animation keyframe values are applied immediately, so the element starts invisible.

**Outcomes:**
- Build: ✅
- Tests: All passing
- Modal opens smoothly without flicker

---

## 2025-12-07 15:00 - Fix Frontend Flicker: Local State Updates & Refresh Buttons

**What I did:**
Fixed the frontend flickering issue where the entire list would reload from the server after every mutation (categorize, skip, toggle, save). Now mutations update the local state directly, eliminating unnecessary API calls and providing a smoother UX. Also added manual refresh buttons to all pages.

**Files Modified:**
- `src/Client/Components/SyncFlow/State.fs`:
  - `TransactionCategorized`: Now updates local list instead of `Cmd.ofMsg LoadTransactions`
  - `TransactionSkipped`: Now updates local list instead of reload
  - `BulkCategorized`: Now updates multiple transactions locally using Map
  - `SplitsSaved`: Now updates local list and closes modal without reload
  - `SplitCleared`: Now updates local list without reload

- `src/Client/Components/Settings/State.fs`:
  - `YnabTokenSaved`: Updates local state with saved token
  - `ComdirectCredentialsSaved`: Updates local state with saved credentials
  - `SyncSettingsSaved`: Updates local state with saved sync settings
  - `DefaultBudgetSet`: Now takes (budgetId, Result) and updates local state
  - `DefaultAccountSet`: Now takes (accountId, Result) and updates local state

- `src/Client/Components/Settings/Types.fs`:
  - Changed `DefaultBudgetSet` to include `YnabBudgetId` for local updates
  - Changed `DefaultAccountSet` to include `YnabAccountId` for local updates

- `src/Client/Components/Dashboard/View.fs`:
  - Added refresh button in page header (dispatches LoadRecentSessions, LoadCurrentSession, LoadSettings)

- `src/Client/Components/Rules/View.fs`:
  - Added refresh button in page header (dispatches LoadRules)

- `src/Client/Components/SyncFlow/View.fs`:
  - Added refresh button in page header (dispatches LoadTransactions, LoadCurrentSession)
  - Optimized stats calculation: Replaced 4x List.filter with single List.fold

- `src/Client/Components/Settings/View.fs`:
  - Added refresh button in page header (dispatches LoadSettings)

- `src/Client/DesignSystem/Button.fs`:
  - Added `Title: string option` field to `ButtonProps` for tooltip/accessibility
  - Updated `defaultProps` with `Title = None`
  - Updated `view` function to render `prop.title` when Title is set

**Rationale:**
The MVU (Model-View-Update) pattern with Virtual DOM should only re-render changed parts of the UI. Reloading entire lists from the server after every mutation causes:
1. Visual flickering as the list briefly shows loading state
2. Unnecessary network requests
3. Poor perceived performance

Since the API already returns the updated object(s) after mutations, we can use these to update the local state directly.

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed
- No more flickering when categorizing, skipping, or editing transactions
- No more flickering when saving settings
- Manual refresh available via icon button on each page

---

## 2025-12-07 11:30 - Documentation: Test Isolation Patterns for Future Projects

**What I did:**
Updated project documentation and Claude Code skills to capture the knowledge about F# test isolation patterns, particularly around environment variables and lazy loading for database configuration.

**Files Modified:**
- `.claude/skills/fsharp-persistence/SKILL.md`:
  - Replaced simple static connection string with lazy-loading pattern
  - Added "Why Lazy Loading?" section explaining F# module initialization
  - Added "Connection Management for Tests" section about In-Memory SQLite lifecycle
  - Updated Best Practices with test isolation requirements
  - Updated Verification Checklist

- `.claude/skills/fsharp-tests/SKILL.md`:
  - Updated Program.fs section with `USE_MEMORY_DB` environment variable setup
  - Added "Test Files Using Persistence" section with `do` before `open` pattern
  - Added "Testing Persistence (In-Memory SQLite)" section with complete example
  - Added "F# Module Initialization Gotchas" section
  - Updated Best Practices with test isolation dos/don'ts
  - Updated Verification Checklist

- `docs/05-PERSISTENCE.md`:
  - Replaced simple connection string pattern with full lazy-loading example
  - Added "Why Lazy Loading?" section
  - Added "Connection Management for In-Memory SQLite" section
  - Added Best Practices items 11-13 for test isolation

- `docs/06-TESTING.md`:
  - Completely rewrote "Testing Persistence (with In-Memory DB)" section
  - Added "Setting Up Test Mode" with Main.fs and test file examples
  - Added "Why This Pattern Works" explanation
  - Added "Common Pitfalls" section with wrong/correct examples
  - Added "Verifying Test Isolation" with verification commands
  - Added Best Practices items 11-13 for test isolation

**Rationale:**
The experience with the 236 test rules in production taught valuable lessons about F# module initialization order and test isolation. This knowledge should be preserved for future projects so the same mistakes aren't repeated.

**Key Lessons Documented:**
1. F# modules initialize by dependency graph, not by `open` order
2. `lazy` is required for configuration that tests need to override
3. In-Memory SQLite needs a shared connection kept alive
4. `do` before `open` in test files allows setting env vars before module init

**Outcomes:**
- Build: ✅
- All documentation updated with complete patterns
- Future projects will have clear guidance on test isolation

---

## 2025-12-07 - Fix: Rules list no longer reloads on every change

**What I did:**
Fixed a performance and UX issue where toggling a rule's enabled state, saving, or deleting a rule caused the entire Rules list to reload from the server.

**Problem:**
- Every change to a single rule triggered `Cmd.ofMsg LoadRules`
- This caused unnecessary network traffic
- Visible UI flickering due to RemoteData going to Loading state
- Poor user experience

**Root Cause:**
The MVU update handlers for `RuleToggled`, `RuleSaved`, and `RuleDeleted` all dispatched `LoadRules` instead of updating the local state with the returned data.

**Files Modified:**
- `src/Client/Components/Rules/Types.fs`:
  - Changed `RuleDeleted of Result<unit, RulesError>` to `RuleDeleted of Result<RuleId, RulesError>` to pass the deleted rule's ID

- `src/Client/Components/Rules/State.fs`:
  - `RuleToggled`: Now uses the returned `updatedRule` to update the local state via `List.map`
  - `RuleSaved`: Now uses the returned `savedRule` - adds to list for new rules, replaces for updates
  - `DeleteRule`: Modified to pass `ruleId` through the success result
  - `RuleDeleted`: Now filters out the deleted rule locally via `List.filter`

- `src/Client/Components/Rules/View.fs`:
  - Added `prop.key` to rule list items for efficient React reconciliation

**Rationale:**
The API already returns the updated/created Rule, so reloading the entire list was wasteful. Local state updates are:
- Faster (no network round-trip)
- Smoother (no UI flickering)
- More efficient (single item update vs full list fetch)

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped integration tests)
- Toggling a rule no longer causes list reload
- Creating/updating a rule updates in-place
- Deleting a rule removes it immediately from UI

---

## 2025-12-07 - Fix: Tests no longer write to Production Database

**What I did:**
Fixed a critical bug where persistence tests were writing test data (Rules, Sessions, Transactions) to the production database. Implemented in-memory SQLite support for tests.

**Problem:**
- `PersistenceTypeConversionTests.fs` was inserting test Rules into the production SQLite database
- Every test run added 6 new "Test Rule" entries
- Accumulated 236 duplicate test rules in production

**Files Modified:**
- `CLAUDE.md` - Added anti-pattern: "Tests writing to production database - NEVER write tests that persist data to the production database. Always use in-memory SQLite"
- `src/Server/Persistence.fs`:
  - Added lazy-loading for DB configuration (`dbConfig = lazy (...)`)
  - Added `USE_MEMORY_DB` environment variable support
  - When `USE_MEMORY_DB=true`, uses `Data Source=:memory:;Mode=Memory;Cache=Shared`
  - Shared connection for in-memory mode (keeps DB alive across operations)
  - Changed all `use conn = getConnection()` to `let conn = getConnection()` to prevent disposing shared connection
- `src/Tests/PersistenceTypeConversionTests.fs`:
  - Added `Environment.SetEnvironmentVariable("USE_MEMORY_DB", "true")` at module initialization (before `open Persistence`)
- `src/Tests/Main.fs`:
  - Also sets `USE_MEMORY_DB=true` for safety

**Files Deleted:**
- `src/Tests/TestSetup.fs` - Removed (approach didn't work due to F# module initialization order)

**Rationale:**
Tests must NEVER write to production databases. The in-memory SQLite approach provides complete isolation - each test run starts fresh, and no data persists after tests complete.

**Outcomes:**
- Build: ✅
- Tests: 215/215 passed (6 skipped integration tests)
- Production DB: ✅ Verified unchanged after test runs

