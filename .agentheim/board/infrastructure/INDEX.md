# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 0
- **Todo:** 2
- **Doing:** 0
- **Done:** 4
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
- **infra-sdad8** — "Modul-Level-initializeDatabase()-Aufrufe in Testcase-Setup verschieben" (refactor) — `todo/infra-sdad8-initializedatabase-in-testcase-setup-verschieben.md`
- **infra-jh7g9** — "Backend-Port konfigurierbar machen: app.Run überschreibt das bereits gesetzte ASPNETCORE_URLS, zwei Instanzen können nicht nebeneinander laufen" (bug) — `todo/infra-jh7g9-backend-port-konfigurierbar-machen.md`
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infra-za7fq** — "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt" (feature) — `done/infra-za7fq-waechter-test-gegen-schreiben-in-die-echte-datenbank.md`
- **infra-002 · PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)** — 2026-06-19 — `done/infra-002-pwa-installable.md` (vite-plugin-pwa: Workbox-Shell-Precache-SW, `/api` strikt network-only — `runtimeCaching:[]` + `navigateFallbackDenylist:[/^\/api\//]`, kein Daten-Cache; `autoUpdate`; gebrandete `offline.html`-Floor; iOS/Favicon/theme-color auf `#08081a`; `.webmanifest`-MIME im Giraffe-Static-Handler; ADR 0010; Verifier-PASS iter 2 — device-/Tailscale-ACs = Human-Gate)
- **infra-001 · Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt** — 2026-06-18 — `done/infra-001-flaky-sqlite-disposal-test.md` (82b5cef; Root Cause = geteiltes Connection-Objekt im Disposal-Race, nicht der Versionskonflikt; frische Connection/Op + 9.0.13-Pin; ADR 0008; Verifier 10/10 grün)
- **Mobile-Polish: Sticky-Filter, Spring-Easing, Transaktions-Skeleton** — 2026-06-11 — `done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
