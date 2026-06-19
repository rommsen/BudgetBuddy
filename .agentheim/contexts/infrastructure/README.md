# infrastructure

## Purpose
Standing home für **globally-true** Technik-Belange — Dinge, die für *jeden* fachlichen
Bounded Context gelten. BC-lokale Technik (ein Comdirect-Adapter, eine konkrete
Repository-Implementierung, der Regel-Matcher) bleibt im jeweiligen Context; nur was
projektweit wahr ist, landet hier.

Konkret bei BudgetBuddy:
- **Stack:** .NET 9 / F# Fullstack; Fable + Vite + Feliz/Elmish (Client), Giraffe (Server).
- **Persistence:** SQLite via Dapper; AES-256-Verschlüsselung für Secrets (PINs/Tokens).
- **Transport:** Fable.Remoting (typsichere Client↔Server-Contracts in `src/Shared/Api.fs`).
- **Orchestrierung:** SyncSession-Lifecycle (`SyncSessionManager`) als prozessübergreifender
  Zustand eines Sync-Durchlaufs.
- **Deployment:** Docker + Portainer + Tailscale; persönlicher Single-Host-Betrieb.

## Invariants / conventions
- **SQLite-Connection-Lifecycle:** `getConnection()` (`src/Server/Persistence.fs`) gibt
  **immer eine frische** `SqliteConnection` zurück (Dapper öffnet/schließt sie automatisch).
  Es gibt **kein** geteiltes, langlebiges Connection-Objekt über Threads hinweg — im
  Testmodus hält nur **ein** Keep-Alive-Anker die `Cache=Shared`-In-Memory-DB am Leben, ohne
  durchgereicht zu werden. Grund: ein geteiltes Objekt führte unter Expectos Test-Parallelität
  zu `SqliteConnection.RemoveCommand`-Crashes beim Command-Dispose. Siehe ADR 0008.
- **`Microsoft.Data.Sqlite` ist projektweit auf eine feste Version gepinnt** (kein floating
  `9.*`) — identisch in `Server.fsproj` und `Tests.fsproj`, um Versionsskew/MSB3277 zu
  vermeiden. Bei Einführung zentraler Paketverwaltung dorthin zentralisieren. Siehe ADR 0008.
- **PWA = installierbar, kein Offline-Daten-Cache.** Der Client ist eine installierbare PWA
  (`vite-plugin-pwa`, `vite.config.js` im Repo-Root). Der Workbox-Service-Worker precached
  **nur die statische App-Shell**; **`/api/*` ist strikt network-only und wird NIE gecacht**
  (`runtimeCaching: []`, `navigateFallbackDenylist: [/^\/api\//]`) — gecachte Finanzdaten
  wären gegen die Live-Companion-Vision schädlich (stale Beträge, Dedup-/ImportId-Verwirrung).
  `registerType: 'autoUpdate'` (stille Updates, deploy-kontrolliert). SW-`scope: /`, kein
  Vite-`base` (Tailscale serviert an Root). `theme_color`/`background_color` = `#08081a`
  (`--bg-app`, aus `design-system-008`). Gebrandete `offline.html` als Navigations-Floor
  (cached keine Daten). **Manifest-MIME:** der Giraffe-Static-Handler (`Program.fs`) muss
  `.webmanifest → application/manifest+json` mappen (ASP.NET-Default kennt die Endung nicht).
  Siehe ADR 0010.

## Classification
**generic / supporting** — trägt keine fachlichen Entscheidungen, ermöglicht aber alle.

## Actors
- **Roman** — betreibt und deployt.
- **Docker / Tailscale** — Laufzeit- und Netzwerk-Umgebung.

## Ubiquitous language
Generische Ops-/Tech-Begriffe (kein projektspezifisches Fachvokabular):
- **SyncSession** — der prozessübergreifend gehaltene Zustand eines Sync-Durchlaufs
  (Lifecycle in `vision.md` / context-map).
- **Container**, **Volume** (`~/my_apps/budgetbuddy/`), **Tailnet**, **Secret**
  (verschlüsselt persistiert).

## Relationships with other contexts
- **Generic supporting für** alle drei fachlichen Contexts: stellt Persistence, Transport
  und SyncSession-Lifecycle bereit.
- Voller Kontext-Überblick: `../../context-map.md`.

## Open questions
- **ADR-Backfill:** Die bestehende Architektur (Stack / SQLite+Dapper / Fable.Remoting /
  Docker+Tailscale) ist gewachsen, aber noch nicht als ADRs dokumentiert. Optionaler
  Backfill, damit künftige Entscheidungen einen Bezugspunkt haben. Siehe Protokoll-Eintrag
  zur Brainstorm-Session.
- Rate-Limit-/Retry-Strategie als querschnittliches Belang (betrifft v.a. ynab-sync)?
