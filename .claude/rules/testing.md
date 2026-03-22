---
paths:
  - "src/Tests/**"
  - "src/Server/**"
  - "src/Client/**"
  - "src/Shared/**"
---

# Testing Requirements (Non-Negotiable)

| What | When | Coverage |
|------|------|----------|
| Domain Tests | ALWAYS | 100% of pure functions |
| Validation Tests | ALWAYS | All validation rules |
| Bug Regression Tests | ALWAYS | Every fix = 1 test |
| API Integration | Per Feature | Happy path + key errors |
| Persistence | When needed | Complex queries only |
| Smoke Tests | Per Feature | E2E browser verification |

## Bug Fix Protocol

Every bug fix MUST include a regression test:
0. **BDD-Szenarien definieren** (GIVEN/WHEN/THEN) — mindestens 1 "kaputt" (Soll nach Fix funktionieren) + 1 "intakt" (Muss weiterhin funktionieren). Begrenzt Scope und verhindert Überkorrektur.
1. Write failing test FIRST (reproduces the bug — "kaputt"-Szenario als Test)
2. **Wenn unklar warum Test failt:** Nutze `fix-tests` Agent zur Diagnose
3. Fix the bug
4. Verify test passes
5. Verify "intakt"-Szenarien weiterhin funktionieren
6. Run unit tests (`dotnet test`)
7. Run full suite with E2E (`RUN_SMOKE_TESTS=true dotnet test`)
8. Update `diary/development.md`

## Schwester-Funktionen

Wenn eine neue Validation-/Domain-Funktion nach dem Vorbild einer bestehenden geschrieben wird:
- Analoges Test-Set anlegen
- Bei Assertions: JEDES Feld des Return-Types prüfen, auch "offensichtliche" Werte (None, Timestamps)

## E2E Debugging

Bei E2E-Testfehlern IMMER zuerst Netzwerk-Responses prüfen.
Ein `disabled`-Button kann hundert Ursachen haben — HTTP-Status-Codes lügen nicht.

- `WaitForResponseAsync` vor UI-Assertions
- `WaitForURLAsync` ist unzuverlässig bei SPA Hash-Routing — polling-basiertes `waitForHash` nutzen
- Bei Substring-Matching auf URL-Patterns: negative Checks hinzufügen (z.B. "not contains create")

## Critical Notes

- Tests use local Supabase instance (started via `supabase start`) — NEVER touch production DB
- Use `./scripts/setup-env.sh` to configure local development environment
- Test names must describe behavior, not implementation
