---
paths:
  - "src/**"
  - "specs/**"
---

# Feature Development Workflow

## Development Order (TDD-First)

**IMPORTANT:** Follow RED-GREEN-REFACTOR cycle (use `tdd-flow` skill)

0. **Write failing test FIRST** `src/Tests/` (RED phase)
0.5. **MODEL MESSAGES** Commands, Events, Queries identifizieren (use `semantic-modeling` skill)
1. `src/Shared/Domain.fs` Types (Input DTOs, Read Models, Value Objects)
2. `src/Shared/Api.fs` API Contract (semantisch benannt)
3. `src/Server/Commands.fs` Command DUs
4. `src/Server/Events.fs` Event DUs
5. `src/Server/Validation.fs` Input validation (test passes - GREEN)
6. `src/Server/Domain.fs` Pure business logic, produces Events (NO I/O, test passes - GREEN)
7. `src/Server/CommandHandlers.fs` Command orchestration (test passes - GREEN)
8. `src/Server/EventHandlers.fs` Event -> State + Notifications (test passes - GREEN)
9. `src/Server/QueryHandlers.fs` Semantic read operations (test passes - GREEN)
10. `src/Server/Persistence.fs` DB I/O (test passes - GREEN)
11. `src/Server/Api.fs` Wire API to Handlers (test passes - GREEN)
12. **Refactor** Improve code while keeping tests green (REFACTOR phase)
13. `src/Client/State.fs` Model, Msg, update
14. `src/Client/View.fs` UI components
15. **Add more tests** Edge cases, error cases
16. **Run smoke tests** Verify E2E integration works

## Spec-Based Workflow

### Optional: Pre-Spec Brainstorming

**Command:** `/brainstorm-requirement` — Explores "why" before "what", checks design philosophy alignment, documents decisions in `planning/brainstorm-notes.md` + `decision-index.md`. Skip for simple bug fixes or well-understood features.

### Standard 2-Phase Workflow

1. **`/spec-builder`** — Creates `spec.md` with Acceptance Criteria (iterates via review loop)
2. **Plan Mode Implementation** — Claude reads spec.md, enters Plan Mode, implements after approval

### Complete Flow

```
User idea
    |
[Optional] /brainstorm-requirement -> brainstorm-notes.md + decision-index.md
    |
[Bei UI-Features] /design-exploration -> HTML-Prototypen + Design-Decisions
    |                                  -> Design System Update (wenn neue Patterns)
    |
/spec-builder -> spec.md (liest decision-index.md inkl. Design-Decisions)
    |          -> Iterates via review loop until approved
    |
"Implement this spec" -> Claude Plan Mode -> Implementation -> ACs checked off
    |
Build Verification (dotnet build, npm run predev)
    |
Smoke tests pass (RUN_SMOKE_TESTS=true dotnet test)
    |
verify-spec-implementation -> Prüft ACs + Decisions (PFLICHT)
    |
Feature complete
```

## Spec Implementation Workflow

Wenn der User sagt "Implementiere [spec-pfad]" oder "Setze [spec] um":

1. **Plan Mode aktivieren** — `EnterPlanMode`, vollständigen Kontext laden
2. **Kontext laden** — spec.md, decision-index.md, Codebase mit Explore-Agenten erkunden, Nachrichten modellieren
3. **Subagenten-Strategie planen** — Hintergrund-Subagenten, parallele ACs identifizieren, Kontext-Paket pro Subagent (relevante ACs, Decisions, Dateipfade, Verification)
4. **Plan-Output** — Subagenten-Aufrufe, Abhängigkeiten, finale Verification
5. **Nach Plan-Approval** — Clear-Context-Befehl geben
6. **Execution-Phase** — Plan-Datei lesen, Subagenten ausführen, ACs abhaken

## Nach Feature-Implementation (PFLICHT)

### Spec-Verification (PFLICHT)

Nach Spec-Implementation **MUSS** `verify-spec-implementation` laufen:

| Prüfung | Was wird geprüft |
|---------|------------------|
| Akzeptanzkriterien | Alle ACs in spec.md erfüllt? |
| Decisions | Alle Decisions aus decision-index.md eingehalten? |
| Build & Tests | `dotnet build` + `dotnet test` grün? |

### Development Diary

**After completing ANY implementation work, update `diary/development.md`:**

```markdown
## YYYY-MM-DD - [Brief Title]

### What
[1-2 sentences describing what was done]

### Files Changed
- `path/to/file.fs` - [brief description]

### Rationale
[Why this change was made]

### Verification
- Build: [PASSED/FAILED]
- Tests: [X passed, Y skipped]
```

**Diary Archivierung:** Einträge älter als 7 Tage -> `diary/archive/YYYY-MM.md` (nach Monat gruppiert, chronologisch, älteste zuerst).
