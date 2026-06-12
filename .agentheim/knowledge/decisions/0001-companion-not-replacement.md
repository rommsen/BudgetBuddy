---
id: 0001
title: BudgetBuddy ist ein YNAB-Companion, kein YNAB-Ersatz
scope: global
status: accepted
date: 2026-06-01
supersedes: []
superseded_by: []
related_tasks: []
related_research: []
---

# ADR 0001: BudgetBuddy ist ein YNAB-Companion, kein YNAB-Ersatz

> **Amendment (2026-06-12, ADR 0003):** Der Non-Goal-Punkt *manuelle
> Transaction-Eingabe* ist aufgehoben — Quick Add wurde als enthaltenes
> Phase-0-Experiment umgesetzt. Die Kern-Entscheidung (YNAB bleibt Source of
> Truth, keine eigene Budget-Wahrheit) gilt unverändert.

## Context
BudgetBuddy ist ein reifes Tool, das die tägliche Bank→YNAB-Arbeit erledigt (Comdirect-
Import, Auto-Kategorisierung, Push). Parallel existiert eine durchdachte, aber nicht
aktivierte Idee, BudgetBuddy zum vollwertigen YNAB-*Ersatz* mit eigener Source of Truth,
Budget-View, Goals und Move-Money auszubauen (`docs/idea-ynab-replacement.md`). Beim
Onboarding von BudgetBuddy in agentheim musste entschieden werden, welche dieser beiden
Welten die `vision.md` beschreibt — die Wahl bestimmt, welche Bounded Contexts entstehen
und woran künftige Tasks gemessen werden.

## Decision
Die agentheim-Vision beschreibt den **Companion**: ein Single-User-Tool, das Bank→YNAB
schnell und fehlerfrei macht. YNAB bleibt Source of Truth und externes Upstream-System.
Die Ersatz-Reise bleibt eine bewusst *nicht aktivierte* Idee und ist explizites Non-Goal.

## Consequences
### Positive
- Klare Scope-Grenze: "ein YNAB-Konzept bequemer bedienbar machen" = In-Scope,
  "eigene Wahrheit aufbauen / YNAB-Verhalten ersetzen" = Non-Goal.
- Verhindert Scope-Creep über die Backlog-Items, die auf der Kippe sitzen.
- Drei fokussierte Contexts statt eines wuchernden Budget-Systems.

### Negative
- Features wie manuelle Transaction-Eingabe und eigenes Budget-View bleiben draußen,
  obwohl sie technisch nah liegen.
- Falls die Ersatz-Reise später doch startet, braucht es eine Vision-Revision (`brainstorm`
  im Extend-Modus) und vermutlich neue Contexts.

### Neutral
- `docs/idea-ynab-replacement.md` bleibt als Referenz erhalten, wird aber nicht zur Roadmap.

## Alternatives considered
- **Ersatz-Vision schreiben** — abgelehnt: Roman will heute kein Über-Engineering; YNAB
  funktioniert, das Risiko/der Aufwand der Ablösung ist nicht gerechtfertigt.
- **Beide Welten gleichzeitig modellieren** — abgelehnt: verwässert die Scope-Grenze und
  macht jedes Feature-Urteil mehrdeutig.

## References
- `.agentheim/vision.md`
- `docs/idea-ynab-replacement.md`
