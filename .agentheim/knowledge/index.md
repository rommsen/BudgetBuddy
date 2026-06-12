# Index

Top-level catalog of this project's bounded contexts, global decisions, and research.
For BC-scoped artifacts, see each BC's `INDEX.md`.

> Updated by: `model` (BC creation), `work` (global ADRs), `research` (reports tagged global / cross-BC), backfill script.
> Hand-edits are fine but the skills will append at the section markers below.

---

## Bounded contexts

<!-- bc-list:start -->
- **banking-import** — Holt Bank-Transaktionen (Comdirect) und beurteilt Duplikate vor dem Push — `contexts/banking-import/INDEX.md`
- **categorization** — Regelbasierte Auto-Kategorisierung + Order-ID-Propagierung — `contexts/categorization/INDEX.md`
- **ynab-sync** — Push nach YNAB mit Import-ID-Dedup, Splits, Payees — `contexts/ynab-sync/INDEX.md`
- **infrastructure** — Globally-true Technik: Stack, Persistence, Transport, Deployment — `contexts/infrastructure/INDEX.md`
<!-- bc-list:end -->

## Global ADRs (scope: global)

<!-- adr-global:start -->
- **0003** — Quick Add aktiviert Phase 0 der Ersatz-Idee (Amendment zu ADR 0001) — 2026-06-12 — `knowledge/decisions/0003-quick-add-activates-phase-0.md`
- **0002** — Drei Bounded Contexts entlang der Sync-Pipeline — 2026-06-01 — `knowledge/decisions/0002-three-bounded-contexts.md`
- **0001** — BudgetBuddy ist ein YNAB-Companion, kein YNAB-Ersatz — 2026-06-01 — `knowledge/decisions/0001-companion-not-replacement.md`
<!-- adr-global:end -->

## Cross-BC research

Research reports relevant to more than one BC (or to the project as a whole). BC-specific
reports are listed in each BC's `INDEX.md`.

<!-- research-global:start -->
<!-- research-global:end -->

## Pointers

- Vision: `vision.md`
- Context map: `context-map.md`
- Protocol (chronological log): `knowledge/protocol.md` — newest entries on top
- All ADRs: `knowledge/decisions/`
- All research: `knowledge/research/`
