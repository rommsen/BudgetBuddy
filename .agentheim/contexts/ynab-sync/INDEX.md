# ynab-sync — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 1
- **Todo:** 0
- **Doing:** 0
- **Done:** 7
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **ynab-004 · Split-Transaktion in der Liste als "Aufgeteilt" kennzeichnen** — 2026-06-16 — `done/ynab-004-split-row-label.md` (ae7d448)
- **ynab-003 · Split-Editor: Vorzeichen-Fix + editierbare Beträge + Rest-Button** — 2026-06-16 — `done/ynab-003-split-amount-sign-rest-button.md` (14e1f61)
- **ynab-002 · Split-Review-UI — Cashback-Shortcut + generischer Editor** — 2026-06-13 — `done/ynab-002-split-review-ui.md` (f2a71ac)
- **ynab-001 · Split mit Transfer-Zeile — Domain + YNAB-Push (Fundament)** — 2026-06-13 — `done/ynab-001-split-with-transfer-cashback.md`
- **Quick Add Feedback-Runde: eigenes Konto, echter Picker, kein FAB, Payee optional** — 2026-06-12 — `done/2026-06-12-quick-add-feedback-round.md`
- **Quick Add: manuelle Transaktions-Eingabe → YNAB (Phase 0)** — 2026-06-11 — `done/2026-06-11-quick-add-manual-entry.md`
- **Swipe-nach-links: Transaktion überspringen/einschließen** — 2026-06-11 — `done/2026-06-11-swipe-to-skip.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ynab-q7k3m · Quick Add aus der Haupt-Navigation erreichbar (Bottom-Nav)** — `backlog/ynab-q7k3m-quick-add-in-bottom-nav.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0006** — Transfer-Zeile im Split — DU-Split-Ziel als Konto, Payee erst beim Push aufgelöst — 2026-06-13 — `../../knowledge/decisions/0006-transfer-line-in-split-du-account-payee-at-push.md`
- **0004** — Manuelle Einträge ohne ImportId, auf eigenes Quick-Add-Konto — 2026-06-12 — `../../knowledge/decisions/0004-manual-entries-no-importid-own-account.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **Transfer in YNAB Split-Subtransaction: via `payee_id`, kein `transfer_account_id` auf dem Write-Schema** — 2026-06-13 — `../../knowledge/research/ynab-transfer-in-split-subtransaction-2026-06-13.md` (related: ynab-001)
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
