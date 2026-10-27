# infrastructure — Index

Catalog of everything in this bounded context: tasks by status, ADRs scoped to this BC,
research touching this BC, and concept synthesis pages.

> Updated by: `model` (tasks), `work` (BC-scoped ADRs, concept page links), `research` (BC-scoped reports).

---

## Tasks by status

<!-- task-counts:start -->
- **Backlog:** 2
- **Todo:** 0
- **Doing:** 0
- **Done:** 11
<!-- task-counts:end -->

### Todo
<!-- todo-list:start -->
<!-- todo-list:end -->

### Doing
<!-- doing-list:start -->
<!-- doing-list:end -->

### Done (most recent first; older entries kept for prior-art search)
<!-- done-list:start -->
- **infrastructure-r72gd** — Migration von .NET 9 auf .NET 10, nachdem brew upgrade die Runtime 9 entfernt hat (chore) — `done/infrastructure-r72gd-migration-auf-dotnet-10.md`
- **infrastructure-y6atr** — Lesende Start-Abfragen wiederholen sich bei einem Netzwerkfehler (Status 0) ein- bis zweimal, bevor der Fehler erscheint (bug) — `done/infrastructure-y6atr-start-abfragen-bei-netzwerkfehler-wiederholen.md`
- **infrastructure-bnbdc** — "Installierte PWA übernimmt einen neuen Deploy: beim Zurückholen nach neuem Service Worker fragen, sofort neu laden, außer es läuft eine Eingabe (dann Hinweis mit Knopf)" (feature) — `done/infrastructure-bnbdc-pwa-uebernimmt-neuen-stand-ohne-cache-loeschen.md`
- **infrastructure-b6kzp** — "Gate gegen leise danebengehende README-Deltas (repo-lokal, gegen das echte Plugin-Modul)" (chore) — `done/infrastructure-b6kzp-readme-delta-erreicht-die-bullets-dieses-projekts-nicht-und.md`
- **infrastructure-qwthn** — "Done-Listen-Zeilen, die das Rotationswerkzeug nicht erkennt, auf die erwartete Form bringen" (chore) — `done/infrastructure-qwthn-done-listen-zeilen-die-das-rotationswerkzeug-nicht-erkennt-a.md`
- **infra-jh7g9** — "Backend-Port konfigurierbar machen: app.Run überschreibt das bereits gesetzte ASPNETCORE_URLS, zwei Instanzen können nicht nebeneinander laufen" (bug) — `done/infra-jh7g9-backend-port-konfigurierbar-machen.md`
- **infra-sdad8** — "Modul-Level-initializeDatabase()-Aufrufe in Testcase-Setup verschieben" (refactor) — `done/infra-sdad8-initializedatabase-in-testcase-setup-verschieben.md`
- **infra-za7fq** — "Wächter-Test, der laut scheitert, wenn ein Testlauf auf die echte Datei-Datenbank zeigt" (feature) — `done/infra-za7fq-waechter-test-gegen-schreiben-in-die-echte-datenbank.md`
- **infra-002 · PWA-Mechanik — installierbar (Manifest, Shell-SW, iOS-Meta, vite-plugin-pwa)** — 2026-06-19 — — (vite-plugin-pwa: Workbox-Shell-Precache-SW, `/api` strikt network-only — `runtimeCaching:[]` + `navigateFallbackDenylist:[/^\/api\//]`, kein Daten-Cache; `autoUpdate`; gebrandete `offline.html`-Floor; iOS/Favicon/theme-color auf `#08081a`; `.webmanifest`-MIME im Giraffe-Static-Handler; ADR 0010; Verifier-PASS iter 2 — device-/Tailscale-ACs = Human-Gate) — `done/infra-002-pwa-installable.md`
- **infra-001 · Flaky Persistence-Test — SQLite-Disposal-Crash + Microsoft.Data.Sqlite-Versionskonflikt** — 2026-06-18 — — (82b5cef; Root Cause = geteiltes Connection-Objekt im Disposal-Race, nicht der Versionskonflikt; frische Connection/Op + 9.0.13-Pin; ADR 0008; Verifier 10/10 grün) — `done/infra-001-flaky-sqlite-disposal-test.md`
- **Mobile-Polish: Sticky-Filter, Spring-Easing, Transaktions-Skeleton** — 2026-06-11 — `done/2026-06-11-mobile-polish-sticky-filter-skeleton-springs.md`
<!-- done-list:end -->

### Backlog
<!-- backlog-list:start -->
- **infrastructure-eq27s** — Playwright-Wächter in test:scripts bauen den Client einmal und laufen gegen einen gemeinsamen Preview-Server statt je Datei einen eigenen Vite mit Fable-Kompilierung zu starten (chore) — `backlog/infrastructure-eq27s-playwright-waechter-einmal-bauen.md`
- **infrastructure-aaryk** — Playwright-Wächter für den PWA-Wechsel mit zwei Builds: alter Stand läuft, neuer Deploy kommt, Zurückholen lädt neu bzw. zeigt den Hinweis (chore) — `backlog/infrastructure-aaryk-playwright-waechter-pwa-wechsel.md`
<!-- backlog-list:end -->


## Pointers

- Knowledge half (ADRs / research / concepts / BC README) for this BC: `../../knowledge/contexts/infrastructure/INDEX.md`
