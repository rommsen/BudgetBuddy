## ADRs scoped to this BC

<!-- adr-local:start -->
- **0018** — Konten holt der Spiegel je Lauf vollständig, das server_knowledge gilt nur für Transaktionen — 2026-09-06 — `../../decisions/0018-konten-je-spiegel-lauf-vollstaendig.md`
- **0014** — Spiegel YNAB → Kassenbuch per Delta-Sync mit server_knowledge, Freshness-Trigger statt Server-Timer — 2026-09-06 — `../../decisions/0014-spiegel-mechanik-delta-sync-freshness-trigger.md`
- **0006** — Transfer-Zeile im Split — DU-Split-Ziel als Konto, Payee erst beim Push aufgelöst — 2026-06-13 — `../../decisions/0006-transfer-line-in-split-du-account-payee-at-push.md`
- **0004** — Manuelle Einträge ohne ImportId, auf eigenes Quick-Add-Konto — 2026-06-12 — `../../decisions/0004-manual-entries-no-importid-own-account.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
- **Transfer in YNAB Split-Subtransaction: via `payee_id`, kein `transfer_account_id` auf dem Write-Schema** — 2026-06-13 — `../../research/ynab-transfer-in-split-subtransaction-2026-06-13.md` (related: ynab-001)
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
- **quick-add** — Manuelle Bar-Buchung → YNAB: eigene Seite, eigenes Konto, kein Dedup, Vorlagen — `concepts/quick-add.md` (derived: 0003, 0004, 2026-06-11/2026-06-12-quick-add, ynab-q7k3m, ynab-t4n8p)
<!-- concepts:end -->


## Pointers

- BC README (ubiquitous language, invariants): `README.md`
- Task board (tasks by status) for this BC: `../../../board/ynab-sync/INDEX.md`
