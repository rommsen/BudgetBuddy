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
  `registerType: 'prompt'` mit `injectRegister: false` (ADR 0057): der Client registriert den Service Worker selbst (`src/Client/Pwa.fs`), prüft beim Zurückholen höchstens alle 5 Minuten, lädt sofort neu und zeigt bei laufender Eingabe den Hinweis "Neue Version verfügbar" mit Knopf. SW-`scope: /`, kein
  Vite-`base` (Tailscale serviert an Root). `theme_color`/`background_color` = `#08081a`
  (`--bg-app`, aus `design-system-008`). Gebrandete `offline.html` als Navigations-Floor
  (cached keine Daten). **Manifest-MIME:** der Giraffe-Static-Handler (`Program.fs`) muss
  `.webmanifest → application/manifest+json` mappen (ASP.NET-Default kennt die Endung nicht).
  Siehe ADR 0010.
- **Test-Datenbank-Wächter:** `Persistence.activeConnectionString ()` gibt den tatsächlich aufgelösten Connection-String zurück, ohne selbst eine Connection zu öffnen. `src/Tests/TestDatabaseGuardTests.fs` prüft ihn als erste Compile-Unit in `Tests.fsproj` gegen `:memory:` und scheitert laut, wenn ein Testlauf `test.runsettings` umgeht (z. B. `dotnet test --settings andere.runsettings` oder ein direkter `dotnet vstest`-Aufruf) und dadurch auf die echte Datei-Datenbank zurückfällt. Grund: ADR 0030 und der Vorfall vom 2026-09-12, siehe `ledger-vm3dk`.
- **DB-Setup nur in `testCase`-Closures, nie auf Modulebene:** `initializeDatabase()`/`initializeLedgerSchema()`/`seedAccounts()` (und jeder andere DB-Setup-Aufruf) dürfen in `[<Tests>]`-Bindings nur innerhalb einer `testCase`-Closure laufen, nie direkt im `let`-Body davor. Grund: F# wertet Modul-Level-Bindings beim ersten Zugriff auf das Modul aus, und genau diesen Zugriff löst Expectos eigene `[<Tests>]`-Entdeckung per Reflection aus, bevor ein einzelner Testfall läuft. Der Wächter aus infra-za7fq (`TestDatabaseGuardTests.fs`) meldet einen Rückfall auf die Datei-Datenbank nur, er verhindert ihn nicht. Die Konvention wird mechanisch durchgesetzt: `src/Tests/ModuleLevelDbInitGuardTests.fs` durchsucht jeden `[<Tests>]`-Block unter `src/Tests/` auf genau dieses Muster und schlägt fehl, falls ein künftiges Testmodul es reintroduziert (namentlich abgedeckt: `initializeDatabase`/`initializeLedgerSchema`; `seedAccounts()` folgt derselben Konvention, ist aber vom Scan nicht namentlich erfasst, siehe ADR 0033). Siehe ADR 0033.
- **Backend-Port kommt aus `ASPNETCORE_URLS`, nie aus einem Literal:** `src/Server/Program.fs` ruft `app.Run()` ohne URL-Argument auf (ein Argument würde `ASPNETCORE_URLS` überschreiben und wirkungslos machen, genau der Fehler, den `compose.yaml`s Zeile bis infra-jh7g9 hatte). `Server.ServerConfig.resolveUrls` (rein) liefert ohne gesetzte Variable weiterhin den bisherigen Standard `http://0.0.0.0:5081`. Der Vite-Proxy (`vite.config.js`) und `scripts/screenshot-route.mjs` folgen demselben Port über `BACKEND_PORT` (eine separate Variable, weil `ASPNETCORE_URLS` eine ganze URL ist). Beide Regeln sind mechanisch abgesichert, nicht nur dokumentiert: `src/Tests/BackendPortGuardTests.fs` liest `Program.fs` selbst und lehnt jeden `app.Run(...)`-Aufruf mit nicht-leerem Argument laut ab; zwei Tests in `scripts/screenshot-route.test.mjs` lesen `screenshot-route.mjs` und `vite.config.js` und schlagen fehl, wenn `BACKEND_PORT` nicht mehr gesetzt bzw. gelesen wird. Siehe ADR 0034.
- **README-Delta-Gate:** `scripts/readme-delta-check.mjs` prüft einen `README_DELTA`-Block gegen das echte Plugin-Modul `lib/readme-delta.mjs` (Semver-höchste Version im Plugin-Cache; nie eine eigene Kopie der Grammatik) und lehnt es ab (Exit 1) oder erklärt sich für nicht urteilsfähig (Exit 2), bevor der Conductor etwas schreibt. Aufruf: `npm run check:readme-delta -- --bc <kontext> --delta <datei|->`; Tests unter `npm run test:scripts` (`scripts/readme-delta-check.test.mjs`). Die Regel, das Gate vor dem Integrieren aufzurufen, ist prose-only und nicht mechanisch erzwungen: die Aufrufstelle liegt im Conductor-Plugin-Code außerhalb dieses Repos. Siehe ADR 0040.

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
