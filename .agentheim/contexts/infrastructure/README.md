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
