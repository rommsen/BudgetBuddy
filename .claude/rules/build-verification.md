---
paths:
  - "src/**"
  - "specs/**"
---

# Build Verification Details

## Test Modes

| Mode | Command | Tests | Use Case |
|------|---------|-------|----------|
| **Standard** | `RUN_SMOKE_TESTS=true dotnet test` | Unit + E2E | Final verification, CI/CD |
| **Quick** | `dotnet test` | Unit only | Fast iteration during development |
| **Full** | `export DATABASE_URL="..." && RUN_SMOKE_TESTS=true dotnet test` | ALL (incl. persistence) | Before major releases |

## Persistence Tests (require DB)

**These tests are SKIPPED by default** because they:
- Require Supabase running (`supabase start`)
- Require `DATABASE_URL` environment variable
- Add test data to the database (no cleanup!)

**To run ALL tests including persistence:**
```bash
supabase start
export DATABASE_URL="postgresql://postgres:postgres@127.0.0.1:54322/postgres"
RUN_SMOKE_TESTS=true dotnet test
```

**For final orchestration verification:** Use Standard mode (`RUN_SMOKE_TESTS=true dotnet test`). Persistence tests are optional — they test DB operations that are also covered by E2E tests.

## E2E/Smoke Tests

**Smoke test failure blocks "implementation complete"** — If the smoke test fails, the feature is NOT ready for production, even if all unit tests pass. Smoke tests catch integration issues that unit tests miss:
- Server starts but API calls fail at runtime
- Client cannot reach server (CORS, routing issues)
- Fable compilation errors that don't show in dotnet build

**Why this is critical for F# full-stack:**
- `dotnet build` passes but Fable.Remoting can fail at runtime (e.g., nested API record types)
- `dotnet test` passes but Fable compilation can fail (e.g., `CultureInfo.GetCultureInfo` not supported)

**Common F# Full-Stack Pitfalls:**
- Nested record types in API interfaces must be flat with `Async<'T>` methods
- `CultureInfo.GetCultureInfo` in Client code — use manual arrays
- Package version mismatches — check .fsproj files

**Smoke Test Troubleshooting:**
If smoke tests fail, see `standards/testing/smoke-tests.md` for common issues and solutions.

## Verification Anti-Patterns

**Diese Rationalisierungen sind NICHT akzeptabel:**

| Rationalisierung | Warum falsch | Was stattdessen |
|------------------|-------------|-----------------|
| "Supabase läuft nicht, also kann ich Smoke Tests nicht ausführen" | Smoke Tests sind Pflicht, nicht optional | `supabase start` und dann Smoke Tests ausführen |
| "Unit Tests passen, das reicht" | Unit Tests finden keine Runtime/Integration-Fehler | Smoke Tests ausführen — dafür existieren sie |
| "Die Aufgabe ist fertig, Verifikation kann der User machen" | DU verifizierst, nicht der User | Alle 4 Schritte ausführen, alle müssen grün sein |
| "Der Server-Port ist belegt, also überspringen" | Port-Konflikt ist kein Code-Problem | Prozess killen oder anderen Port nutzen |

## DB-Schema-Änderungen

Wenn eine Implementation neue `.sql`-Dateien in `supabase/migrations/` enthält:
1. `supabase db reset` als Teil der Verifikation ausführen
2. BEVOR manuelle Browser-Prüfung stattfindet
3. `./verify.sh` prüft NICHT den DB-Zustand — nur Code

**Typisches Fehlmuster:** Build grün, Tests grün, aber leere Seite weil Migration nicht gelaufen ist.

## Stale dist/ nach Shared-Änderungen

Nach Änderungen an `src/Shared/`:
1. `npm run build` (nicht nur `predev`) vor E2E-Tests
2. Browser-Cache kann alte Shared-Types cachen
3. Symptom: E2E-Tests schlagen fehl mit "unexpected token" oder leeren Responses
