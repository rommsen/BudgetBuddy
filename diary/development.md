# Development Diary

This diary tracks the development progress of BudgetBuddy.

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

