# BudgetBuddy Skills

This directory contains **Claude Code skills** for F# full-stack development using the BudgetBuddy blueprint.

## What Are Skills?

Skills are **workflow-focused guides** that:
- Orchestrate development tasks (e.g., "implement backend API")
- Reference detailed `standards/` files for patterns
- Provide quick-reference code snippets
- Include checklists and common pitfalls

**Key Principle**: Skills are **entry points**, `standards/` are **single source of truth**.

## Architecture

```
.claude/skills/          ← Workflow guides (~100-250 lines each)
    ├── fsharp-feature/  ← Orchestrates full-stack features
    ├── fsharp-backend/  ← Backend implementation
    └── ...

standards/               ← Detailed patterns & examples
    ├── global/          ← Architecture, workflows
    ├── shared/          ← Types, API contracts
    ├── backend/         ← Domain, persistence, API
    ├── frontend/        ← State, views, routing
    ├── testing/         ← Test patterns
    └── deployment/      ← Docker, Tailscale
```

## Available Skills

### Core Workflow Skills

**Use these for complete development workflows:**

| Skill | Purpose | Size | Standards |
|-------|---------|------|-----------|
| **fsharp-feature** | End-to-end feature implementation | 201 lines | `global/development-workflow.md` |
| **fsharp-backend** | Backend (validation → domain → persistence → API) | 218 lines | `backend/*.md` |
| **fsharp-frontend** | Frontend (Model, Msg, update, view) | 240 lines | `frontend/*.md` |
| **fsharp-shared** | Domain types and API contracts | 204 lines | `shared/*.md` |
| **fsharp-tests** | Writing tests (domain, API, persistence) | 245 lines | `testing/*.md` |

### Specialized Skills

**Use these for specific patterns:**

| Skill | Purpose | Size | Standards |
|-------|---------|------|-----------|
| **fsharp-validation** | Input validation with Result types | 225 lines | `shared/validation.md` |
| **fsharp-persistence** | SQLite + Dapper, file storage | 229 lines | `backend/persistence-*.md` |
| **fsharp-remotedata** | Async state handling (Loading/Success/Failure) | 160 lines | `frontend/remotedata.md` |
| **fsharp-routing** | URL routing with Elmish | 186 lines | `frontend/routing.md` |
| **fsharp-error-handling** | Result types, error propagation | 205 lines | `backend/error-handling.md` |
| **fsharp-property-tests** | FsCheck property-based testing | 186 lines | `testing/property-tests.md` |
| **fsharp-docker** | Multi-stage Docker builds | 250 lines | `deployment/docker.md` |
| **tailscale-deploy** | Docker + Tailscale deployment | 285 lines | `deployment/*.md` |

### Project-Specific

| Skill | Purpose | Size |
|-------|---------|------|
| **blogpost** | Generate blog posts from development diary | ~200 lines |

## How to Use Skills

### 1. Automatic Selection

Claude automatically selects skills based on context:
- User asks "implement backend API" → `fsharp-backend`
- User asks "add routing" → `fsharp-routing`
- User asks "write tests" → `fsharp-tests`

### 2. Manual Invocation

You can invoke skills manually using the Skill tool:

```
Use fsharp-backend skill to implement API
```

### 3. Via CLAUDE.md

The main `CLAUDE.md` file tells Claude when to use each skill. See the "Using Skills" section.

## Skill Structure

Each skill follows this template:

```markdown
---
name: skill-name
description: |
  One-liner what this skill does.
allowed-tools: Read, Edit, Write, Grep, Glob, Bash
standards:
  - standards/path/to/standard.md
---

# Skill Title

## When to Use This Skill
- Situation 1
- Situation 2

## Quick Start
### Step 1: [Action]
**Read:** `standards/path.md`
**Create:** `src/File.fs`

[Minimal code example]

## Quick Reference
[2-3 most common patterns]

## Checklist
- [ ] Read standards
- [ ] Implementation steps

## Common Mistakes
❌ Don't...
✅ Do...

## Related Skills
- **other-skill** - Description

## Detailed Documentation
- `standards/path.md` - Complete patterns
```

## Development

### Adding a New Skill

1. **Check standards first**: Does a standard exist for this pattern?
2. **Create skill directory**: `.claude/skills/new-skill/`
3. **Write skill.md**: Follow template above (~100-250 lines)
4. **Keep it focused**: Workflow-oriented, reference standards
5. **Don't duplicate**: Reference standards for details
6. **Update CLAUDE.md**: Add to skills table

### Refactoring an Existing Skill

1. **Read current skill**: Identify redundant content
2. **Check standards**: What can be referenced instead?
3. **Create skill-optimized.md**: Refactored version
4. **Test**: Verify workflow still works
5. **Replace**: `cp skill-optimized.md skill.md`

## Migration History

### Phase 1: Refactored Skills (2025-12-31)

**Goal:** Eliminate redundancy, make skills workflow-focused

**Before:** 3,551 lines (avg 443 lines/skill)
**After:** 1,847 lines (avg 231 lines/skill)
**Reduction:** 48%

**Refactored:**
- fsharp-backend (218 lines)
- fsharp-feature (201 lines)
- fsharp-frontend (240 lines)
- fsharp-shared (204 lines)
- fsharp-persistence (229 lines)
- fsharp-tests (245 lines)
- fsharp-validation (225 lines)
- tailscale-deploy (285 lines)

### Phase 2: New Skills (2025-12-31)

**Goal:** Create focused skills for specific patterns

**Created:** 5 new skills (987 lines total, avg 197 lines/skill)

- fsharp-remotedata (160 lines) - Async state handling
- fsharp-routing (186 lines) - URL routing
- fsharp-error-handling (205 lines) - Result types
- fsharp-property-tests (186 lines) - FsCheck testing
- fsharp-docker (250 lines) - Multi-stage builds

### Phase 3: Documentation (2025-12-31)

- ✅ Updated CLAUDE.md with complete skill table
- ✅ Updated all `/docs/` references to `standards/`
- ✅ Created this comprehensive README

## Benefits

### Before Migration
- ❌ 463 lines per skill (too long to read quickly)
- ❌ Redundant with docs/ content
- ❌ Hard to maintain (changes needed in multiple places)
- ❌ 22.1k tokens in context

### After Migration
- ✅ ~200 lines per skill (quick to scan)
- ✅ Single source of truth (`standards/`)
- ✅ Easy to maintain (change once in standards)
- ✅ Workflow-oriented (actionable steps)
- ✅ ~50% token reduction

## See Also

- **[MIGRATION-PLAN.md](MIGRATION-PLAN.md)** - Migration strategy and status
- **[CLAUDE.md](../../CLAUDE.md)** - Main Claude Code instructions
- **[standards/](../../standards/)** - Detailed pattern documentation
- **[docs/MILESTONE-PLAN.md](../../docs/MILESTONE-PLAN.md)** - Project milestones

## Quick Start

**New to BudgetBuddy development?**

1. Read `CLAUDE.md` - Main instructions
2. Browse `standards/global/architecture.md` - Understand the stack
3. Check `standards/global/quick-reference.md` - Code templates
4. Use `fsharp-feature` skill for your first feature
