# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 1
- **Todo:** 0
- **Doing:** 0
- **Done:** 2
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infra-001 · Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt** — 2026-06-18 — `done/infra-001-flaky-sqlite-disposal-test.md` (82b5cef; Root Cause = geteiltes Connection-Objekt im Disposal-Race, nicht der Versionskonflikt; frische Connection/Op + 9.0.13-Pin; ADR 0008; Verifier 10/10 grün)
- **Mobile-Polish: Sticky-Filter, Spring-Easing, Transaktions-Skeleton** — 2026-06-11 — `done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infra-002 · PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)** — `backlog/infra-002-pwa-installable.md` (depends_on: design-system-008, design-system-001; Scope: nur installierbar, kein Daten-Caching; HTTPS via Tailscale ✓)
<!-- backlog-list:end -->

## ADRs scoped to this BC

<!-- adr-local:start -->
- **0008** — SQLite: feste Versions-Pins (9.0.13) + frische Connection pro Operation (kein geteiltes Connection-Objekt) — 2026-06-18 — `../../knowledge/decisions/0008-sqlite-per-operation-connection-and-version-pin.md`
- **0005** — Mobile Sheets ankern am Visual Viewport, Auswahl committet auf Click — 2026-06-12 — `../../knowledge/decisions/0005-visual-viewport-sheets-click-commit.md`
<!-- adr-local:end -->

## Research touching this BC

<!-- research-local:start -->
<!-- research-local:end -->

## Concepts (opt-in synthesis pages)

<!-- concepts:start -->
<!-- concepts:end -->

## Pointers

- BC README (ubiquitous language, invariants): `README.md`
