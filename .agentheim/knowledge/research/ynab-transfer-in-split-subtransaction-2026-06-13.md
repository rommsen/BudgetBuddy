---
topic: Encoding a transfer-to-another-account line inside a YNAB split (subtransaction) via the API
date: 2026-06-13
requested_by: model
related_tasks: [ynab-001]
---

# Research: Transfer-to-account inside a YNAB split subtransaction (POST /transactions)

## Question
When BudgetBuddy pushes a split transaction to YNAB (`POST /budgets/{budget_id}/transactions`),
how does it encode **one split line that is a transfer to another YNAB account** — the
"cash back at the grocery store" case: a €217 bank transaction where €200 is a cash
withdrawal (transfer to an existing "Bargeld" account) and €17 is a real purchase
(a category)? Specifically:

1. Which field on `SaveSubTransaction` carries the transfer target?
2. Are transfer subtransactions supported via the API at all, or is there a known limitation?
3. Amount sign / milliunits convention for the transfer line?
4. Is the matching counterpart transaction on the target (Bargeld) account auto-created?
5. Gotchas: must the transfer payee pre-exist, how is its `payee_id` obtained,
   cleared-status / `import_id` constraints at the subtransaction level?

## Summary
- **Encode the transfer line via `payee_id`** pointing at the auto-created transfer payee for
  the target account. `SaveSubTransaction` has **no `transfer_account_id` and no
  `transfer_payee_id` field** — those exist only on the *read* model / the Account resource.
  The only transfer mechanism available on a subtransaction is the payee [1][2][3].
- **Transfers inside splits are supported.** A split line whose payee is a transfer payee is a
  valid, long-standing pattern. The read model exposes per-subtransaction
  `transfer_account_id` and `transfer_transaction_id`, confirming the API models transfers at
  the subtransaction level [1][3].
- **Amount is milliunits, negative for an outflow** from the source account. The €200 cash
  line is `-200000`; the €17 purchase line is `-17000`; the parent total is `-217000`. Split
  subtransaction amounts must sum to the parent amount [2][4][5].
- **The counterpart on the Bargeld account is created automatically.** YNAB transfers are
  always double-sided; the API links them via `transfer_transaction_id`. The caller does
  **not** create the other side [1][6]. (Inference from the read schema + YNAB core transfer
  behaviour — see Open questions for the single residual doubt.)
- **You must look up the transfer `payee_id` from `GET /payees`** — pick the payee whose
  `transfer_account_id` equals the Bargeld account's id. `payee_name` resolution will **not**
  create or match a transfer payee, so passing the id is the reliable path [3][7].
- **`import_id` is not a `SaveSubTransaction` field** — it lives only on the parent
  transaction. Cleared status is a parent-level field too; subtransactions inherit it [2][4].

## Findings

### 1. Encoding: which field carries the transfer target
The authoritative source is the official YNAB OpenAPI spec (`api.ynab.com/papi/open_api_spec.yaml`,
title "YNAB API Endpoints", v3.0.0; API version reported 1.76.0 at time of research) [2].
The `SaveSubTransaction` schema has exactly these properties [1][2]:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `amount` | integer (int64, milliunits) | **yes** | The subtransaction amount |
| `payee_id` | string/uuid, nullable | no | "The payee for the subtransaction." |
| `payee_name` | string (≤200), nullable | no | Resolves to a payee by rename rule / name match / creation |
| `category_id` | string/uuid, nullable | no | The category for the subtransaction |
| `memo` | string, nullable | no | — |

There is **no `transfer_account_id` and no `transfer_payee_id`** on `SaveSubTransaction` [1][2].
So a transfer split line is encoded by setting **`payee_id` to the transfer payee** of the
target account (and leaving `category_id` null for that line — transfers carry no category).

This mirrors how transfers are done on top-level transactions. The
`SaveTransactionWithOptionalFields` (parent of `NewTransaction`) `payee_id` description states
(quoted via the spec): *"To create a transfer between two accounts, use the account transfer
payee pointing to the target account. Account transfer payees are specified as
`transfer_payee_id` on the account resource."* [2][8]. The same payee-based mechanism is the
only one available on a subtransaction.

**Field-name note / common typo:** several third-party SDK docs spell this `tranfer_payee_id`
(missing the "s") on the Account resource [7][8]. The correct YNAB field on the **Account**
object is `transfer_payee_id`; on the **Payee** object it is `transfer_account_id`. On
`SaveSubTransaction` neither exists — only `payee_id`.

### 2. Support / known limitations
Transfers inside splits are supported. Evidence:
- The **read** model `SubTransaction` includes `transfer_account_id` ("If a transfer, the
  account_id which the subtransaction transfers to") and `transfer_transaction_id` ("If a
  transfer, the id of transaction on the other side of the transfer") [3]. The API would not
  model these per-subtransaction fields if a split line could never be a transfer.
- YNAB's own product supports a transfer as one line of a split (the canonical cash-back
  scenario), and the API exposes the same data model [3][5][6].

Documented API limitations that *do* apply, but are orthogonal to creation:
- **You cannot add/modify subtransactions of an *existing* split via the API** — splits can
  only be set at creation time. Updating splits of an existing split transaction is not
  supported [1][9]. For BudgetBuddy this is fine: it creates the split fresh on push.
- A separate, distinct YNAB rule: a *transfer transaction itself* has no category and cannot
  be turned into a split [9]. This is the inverse direction (you can't split a transfer) and
  does **not** block putting a transfer line inside a normal split.

Single-source caveat: I could not pull a YNAB *developer-forum* thread that explicitly walks
through "transfer payee inside a subtransaction" end to end — the support/forum pages returned
metadata only on fetch. The conclusion rests on the OpenAPI spec + SDK read model, which are
the more authoritative primary sources anyway.

### 3. Sign / milliunits
Amounts are in **milliunits** (1000 milliunits = 1 currency unit); e.g. −$294.23 = −294230 [2][4].
Outflows are **negative**. For the example:
- Bargeld transfer line: `amount = -200000` (€200 leaving the bank account into Bargeld)
- Purchase line: `amount = -17000` (€17 spend)
- Parent transaction: `amount = -217000`

Split subtransaction amounts **must sum to the parent amount** (YNAB rejects mismatches);
at least two subtransactions are expected for a split [2][4][5].

### 4. Counterpart on the target (Bargeld) account
YNAB transfers are inherently double-sided: creating the transfer line automatically creates
the matching transaction on the Bargeld account, and the two are linked. On the read side this
link surfaces as `transfer_transaction_id` on the subtransaction [3]. The caller does **not**
create the other side — doing so would produce a duplicate [1][6].

The Bargeld counterpart will be a **+€200000** inflow on that account (positive, opposite sign
of the source line). This is core YNAB transfer behaviour rather than an explicitly documented
API guarantee — see Open questions.

### 5. Gotchas
- **The transfer payee must pre-exist and you must use its `payee_id`.** YNAB auto-creates a
  transfer payee for every on-budget account (named like "Transfer : Bargeld"). Since the
  Bargeld account already exists, its transfer payee already exists. Obtain its id via
  `GET /budgets/{id}/payees` and select the payee whose **`transfer_account_id` == Bargeld's
  account id** [3][7]. The Payee object exposes `transfer_account_id` ("If a transfer payee,
  the account_id to which this payee transfers to") [3][7] — that is the join key.
- **Do not rely on `payee_name` to make a transfer.** `payee_name` resolution only does rename
  rules / name match / new-payee creation [1][2]; passing `payee_name = "Transfer : Bargeld"`
  is not documented to bind to the transfer payee and risks creating a plain non-transfer payee
  with that literal name. Always pass the looked-up `payee_id`.
- **`import_id` is parent-only.** `SaveSubTransaction` has no `import_id` field [1][2];
  `import_id` (≤36 chars) is set on the parent transaction and drives dedup/import-matching
  (match on same account, same amount, date ±10 days) [2][4]. Note: the parent `import_id`
  also gates `payee_name` rename-rule resolution for subtransactions ("only if `import_id` is
  also specified on parent transaction") [1] — irrelevant if you pass `payee_id` directly.
- **Cleared status is parent-level.** `cleared` is a `SaveTransaction` field, not a
  subtransaction field; subtransactions take the parent's status. No special cleared
  constraint applies to a transfer split line [2][4].

### Concrete JSON shape (illustrative)
```json
{
  "transaction": {
    "account_id": "<bank-account-id>",
    "date": "2026-06-13",
    "amount": -217000,
    "payee_name": "REWE",
    "category_id": null,
    "cleared": "cleared",
    "approved": true,
    "import_id": "<dedup-key>",
    "subtransactions": [
      { "amount": -200000, "payee_id": "<transfer-payee-id-for-Bargeld>" },
      { "amount": -17000,  "category_id": "<groceries-category-id>" }
    ]
  }
}
```
`<transfer-payee-id-for-Bargeld>` = the `id` of the payee from `GET /payees` whose
`transfer_account_id` matches the Bargeld account id.

## Sources
1. [dmlerner/ynab-api SaveSubTransaction.md](https://github.com/dmlerner/ynab-api/blob/master/docs/SaveSubTransaction.md) — generated-from-spec field list for SaveSubTransaction; confirms only amount/payee_id/payee_name/category_id/memo, no transfer fields. (mirror of YNAB OpenAPI spec)
2. [YNAB OpenAPI spec — api.ynab.com/papi/open_api_spec.yaml](https://api.ynab.com/papi/open_api_spec.yaml) — PRIMARY. SaveSubTransaction / NewTransaction / Payee schemas; milliunits; transfer-via-payee language. API v1.76.0 at time of research.
3. [ynab/ynab-sdk-ruby SubTransaction.md](https://github.com/ynab/ynab-sdk-ruby/blob/master/docs/SubTransaction.md) — PRIMARY (official SDK). Read model has transfer_account_id + transfer_transaction_id per subtransaction.
4. [YNAB API overview / endpoints (api.ynab.com)](https://api.ynab.com/) — milliunits definition, import_id matching semantics.
5. [YNAB blog: Two Totally Legit Ways to Handle Cash](https://www.ynab.com/blog/two-ways-to-handle-cash) — cash-back-as-split-with-cash-account workflow (vendor/product doc).
6. [YNAB support: Transfer Transactions guide](https://support.ynab.com/en_us/transfer-transactions-a-guide-HJOsZz4Jj) — transfers are double-sided (full text not retrievable on fetch; title/topic confirmed).
7. [dmlerner/ynab-api Payee.md](https://github.com/dmlerner/ynab-api/blob/master/docs/Payee.md) — Payee.transfer_account_id description; how to identify a transfer payee.
8. [dmlerner/ynab-api SaveTransaction.md](https://github.com/dmlerner/ynab-api/blob/master/docs/SaveTransaction.md) — parent payee_id "To create a transfer … use the account transfer payee" language.
9. [YNAB support: Split Transactions guide](https://support.ynab.com/en_us/split-transactions-a-guide-SJLEKwY0q) — split semantics; "transfers (no category) cannot themselves be split"; API can't update existing splits.

## Open questions
- **Auto-creation of the Bargeld counterpart is inferred, not quoted.** The OpenAPI spec
  doesn't state in prose "the other side is created automatically" for a *subtransaction*
  transfer; I infer it from (a) the read-model `transfer_transaction_id` link and (b) YNAB's
  universal double-sided transfer behaviour. Confidence is high but this is the one claim
  resting on inference rather than a verbatim primary statement. A 5-minute sandbox test
  (create the split, then GET the Bargeld account's transactions) would close this.
- **Behaviour of `payee_name = "Transfer : X"`** was not testable from docs — treated as
  unsupported/unsafe. Use the looked-up `payee_id`; don't rely on name resolution.
- **Forum/issue confirmation** of the exact pattern wasn't directly retrieved (support/forum
  pages returned metadata-only on fetch). The OpenAPI spec + official Ruby SDK are stronger
  evidence, so this is a low-risk gap.
