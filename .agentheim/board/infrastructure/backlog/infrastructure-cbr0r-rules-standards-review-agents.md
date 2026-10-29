---
id: infrastructure-cbr0r
title: "Rules konsolidieren, Test-Doktrin anlegen, standards eindampfen, Review-Agents reparieren und kürzen"
status: backlog
type: refactor
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-ky2kh, infrastructure-vm8zz]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Rules veraltet oder auf falschen Pfaden; `localization.md` auskommentiert; drei Review-Agents laden nicht (YAML) und sind veraltet; `standards/backend/overview.md:22-23` beschreibt Giraffe Some/None irreführend (Audit A, B1, B2). Souls E4, E6, E7.

## What
Löschen: `build-verification`, `feature-workflow`, `localization`. `domain-purity`, `persistence-safety`, `query-safety` auf Resttext mit Pfaden `src/Server/**`. `testing.md` durch `test-doctrine.md` (Doktrin aus Bericht D und Souls 3.3, BudgetBuddy-Pflichtliste: Pipeline, Endpoint-Negativtests, Live nur hinter Schalter, Bot-Grenzen, keine Secrets an den Client). `standards/` eindampfen, `styleguide.md` bleibt. `backend-code-quality`, `frontend-architecture`, `design-quality` mit gültigem Frontmatter und aktuellem Inhalt; `qa-milestone-reviewer` zum Test-Reviewer gegen die Doktrin; `review-code.md` und `red-test-fixer` löschen.

## Acceptance criteria
- [ ] Alle Agents in `.claude/agents/` erscheinen in der Agent-Liste einer frischen Session.
- [ ] Kein Rule-`paths:` zeigt auf eine nicht existierende Datei (Test oder Skript prüft das).
- [ ] `standards/backend/overview.md` beschreibt `skipPipeline` korrekt.
- [ ] Keine DaisyUI-Klassen und keine Tailwind-3-Syntax in Agents oder standards (grep).

## Notes
Migrationsschritt M10. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Nach diesem Schritt messen. Abbruchkriterium: Referenz-Task verschlechtert sich, Schritt zurücknehmen.
