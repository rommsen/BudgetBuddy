# Integration Test Scripts

This directory contains F# scripts for manually testing the YNAB and Comdirect integrations without needing the full UI.

## Setup

1. **Copy `.env.example` to `.env`** in the project root
2. **Fill in your credentials** in `.env`:
   ```bash
   # YNAB API
   YNAB_TOKEN=your-personal-access-token-here

   # Comdirect API
   COMDIRECT_CLIENT_ID=your-client-id
   COMDIRECT_CLIENT_SECRET=your-client-secret
   COMDIRECT_USERNAME=your-username
   COMDIRECT_PASSWORD=your-password
   # COMDIRECT_ACCOUNT_ID=your-account-id  # Optional: only for fetching transactions
   ```

## Available Scripts

### `test-ynab.fsx` - YNAB API Test

Tests the complete YNAB integration:
- Loads token from `.env`
- Fetches all budgets
- Fetches budget details (accounts + categories)
- Validates token

**Usage:**
```bash
dotnet fsi scripts/test-ynab.fsx
```

**Expected Output:**
```
===================================================
YNAB API Integration Test
===================================================

Test 1: Fetching budgets...
✅ SUCCESS: Found 2 budget(s)
   - My Budget (ID: budget-123)
   - Test Budget (ID: budget-456)

Test 2: Fetching details for budget 'My Budget'...
✅ SUCCESS: Budget details loaded
   Budget: My Budget
   Accounts: 3
      - Checking Account: 1250.50 EUR
      - Savings Account: 5000.00 EUR
      - Credit Card: -250.00 EUR
   Categories: 25
      [Essential Expenses]
         - Rent
         - Groceries
      [Fun Money]
         - Entertainment
         - Dining Out

===================================================
ALL TESTS PASSED ✅
===================================================
```

### `test-comdirect.fsx` - Comdirect OAuth Test

Tests the complete Comdirect OAuth flow including Push-TAN:
- Loads credentials from `.env`
- Initiates OAuth flow
- Requests Push-TAN challenge
- **Waits for you to confirm TAN on your phone**
- Completes OAuth flow
- Fetches transactions (optional, requires `COMDIRECT_ACCOUNT_ID`)

**Usage:**
```bash
dotnet fsi scripts/test-comdirect.fsx
```

**Expected Output:**
```
===================================================
Comdirect OAuth Integration Test
===================================================

⚠️  WARNING: This test will initiate a real Push-TAN
   Make sure you have your phone ready!

Press ENTER to start the OAuth flow, or Ctrl+C to cancel...

Test 1: Starting OAuth flow...
✅ SUCCESS: OAuth flow started
   Session ID: session-guid
   Request ID: 123456789

✅ Push-TAN Challenge received
   Challenge ID: challenge-123
   Challenge Type: P_TAN_PUSH

===================================================
⏳ WAITING FOR TAN CONFIRMATION
===================================================

Please check your phone and confirm the Push-TAN now.

After you confirmed on your phone, press ENTER here...

Test 2: Completing OAuth flow after TAN confirmation...
✅ SUCCESS: OAuth flow completed
   Access Token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
   Refresh Token: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...

Test 3: Fetching transactions...
   Account ID: 12345678
   Days back: 30

✅ SUCCESS: Fetched 47 transactions

Sample transactions (first 5):
   - 2025-11-29 | -45.00 EUR | Amazon EU S.a.r.L.
     Memo: AMAZON PAYMENTS EUROPE S.C.A. LU...
   - 2025-11-28 | -12.50 EUR | Coffee Shop
     Memo: PURCHASE AUTHORIZATION...

===================================================
ALL TESTS PASSED ✅
===================================================
```

## Automated Integration Tests

In addition to these manual scripts, there are also automated integration tests in `src/Tests/`:
- `YnabIntegrationTests.fs` - Automated YNAB API tests (skipped if no `.env`)
- `ComdirectIntegrationTests.fs` - Automated Comdirect tests (limited, skipped if no `.env`)

**Run with:**
```bash
dotnet test
```

**Note:** The Comdirect integration tests only go up to the TAN challenge step, as TAN confirmation cannot be automated. For full end-to-end testing, use the `test-comdirect.fsx` script.

## Troubleshooting

### YNAB Token Issues
- **401 Unauthorized**: Your token is invalid or expired
  - Generate a new token at https://app.youneedabudget.com/settings/developer
  - Copy it to `.env` as `YNAB_TOKEN`

- **Network errors**: Check your internet connection

### Comdirect OAuth Issues
- **401 Unauthorized / Invalid Credentials**:
  - Double-check your username and password in `.env`
  - Ensure your Comdirect API access is enabled

- **TAN not received on phone**:
  - Make sure Push-TAN is set up in your Comdirect app
  - Check that your phone has internet connection

- **TAN timeout**:
  - You have limited time to confirm the TAN
  - Run the script again if it times out

## .env File Structure

Your `.env` file should look like this:
```bash
# Tailscale (for deployment)
TS_AUTHKEY=tskey-auth-XXXX-XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX

# YNAB API - get from https://app.youneedabudget.com/settings/developer
YNAB_TOKEN=your-personal-access-token-here

# Comdirect API - OAuth credentials
COMDIRECT_CLIENT_ID=your-client-id
COMDIRECT_CLIENT_SECRET=your-client-secret
COMDIRECT_USERNAME=your-username
COMDIRECT_PASSWORD=your-password
# COMDIRECT_ACCOUNT_ID=your-account-id  # Optional: only needed for fetching transactions
```

**Note:** The `COMDIRECT_ACCOUNT_ID` is optional. You only need it if you want to test the transaction fetching step. The OAuth flow and TAN confirmation will work without it.

## EnvLoader Module

The `EnvLoader.fsx` module provides helper functions for loading environment variables from `.env`:
- `loadProjectEnv()` - Loads `.env` from project root
- `getRequired env "KEY"` - Gets required variable or fails
- `getOptional env "KEY"` - Gets optional variable (returns `Option`)
- `printEnvInfo env` - Prints loaded variables (masks secrets)

This module is automatically loaded by the test scripts.
