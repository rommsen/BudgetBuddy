# ledger — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `modeling` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 1
- **Todo:** 3
- **Doing:** 1
- **Done:** 3
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **ledger-m8r2v** — Spiegel-Lauf YNAB → Kassenbuch: Delta-Abruf, Snapshot-Vergleich, Ereignisse, manueller Auslöser — depends_on: [ledger-e3k7p, ynab-czk34] — `todo/ledger-m8r2v-mirror-run.md`
- **ledger-q5w9c** — Kassenbuch-Seite: Buchungen eines Kontos mit Herkunfts-Badge und Historie, "Jetzt spiegeln" — depends_on: [ledger-m8r2v, design-system-001] — `todo/ledger-q5w9c-ledger-page.md`
- **ledger-kt0wq** — Walking Skeleton Kassenbuch: Abnahme auf dem echten Budget (Human-Gate) — depends_on: [ledger-e3k7p, ledger-m8r2v, ledger-q5w9c] — `todo/ledger-kt0wq-walking-skeleton.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
- **ledger-e3k7p** — Kassenbuch-Fundament: Ereignis-Log, Zustandsmodell, appendAndApply und Replay-Test (feature) — `doing/ledger-e3k7p-event-log-and-state.md`
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **ledger-zm0zx** — Kontenmodell und Bewertung festlegen (Konto-Arten, BB-eigene Konten, Kursquelle) (decision) — `done/ledger-zm0zx-account-model-valuation.md`
- **ledger-z5vw6** — Identität einer Buchung über Systeme hinweg festlegen (eigene Id + Fremd-Id-Verknüpfung) (decision) — `done/ledger-z5vw6-entry-identity.md`
- **ledger-fsz0a** — Historie-Technik im Kassenbuch festlegen (Ereignis-Log + synchrones Zustandsmodell) (decision) — `done/ledger-fsz0a-history-mechanism.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **ledger-c7j8e** — Kursquelle für Geldmarkt-ETF per ISIN (Research-Spike) (spike) — `backlog/ledger-c7j8e-kursquelle-etf-isin-research-spike.md`
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0016** — Kontenmodell (Art x Verknüpfung), Bewertung als Buchung, Bar/Quick-Add und ETF-Konto in v1 — 2026-09-06 — `knowledge/decisions/0016-kontenmodell-und-bewertung-als-buchung.md`
- **0015** — Buchungs-Identität: eigene Id im Kassenbuch, Fremd-Ids als Verknüpfungen, Spiegel matcht YNAB-Id vor ImportId — 2026-09-06 — `knowledge/decisions/0015-buchungs-identitaet-eigene-id-fremd-id-verknuepfung.md`
- **0013** — Kassenbuch-Historie als append-only Ereignis-Log mit synchron geführtem Zustandsmodell — 2026-09-06 — `knowledge/decisions/0013-append-only-event-log-with-synchronous-state.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
