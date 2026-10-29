---
id: categorization-hdx88
title: "Tote Nachrichten im Regel-Modul: ToggleRuleEnabled, RuleToggled und ImportRulesStart"
status: backlog
type: chore
context: categorization
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **mittel**.
Verletzte Regel: .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer"

## What
Fundstelle: `src/Client/Components/Rules/Types.fs:107, :108, :129`

Niemand dispatcht sie (State.fs:350 macht für ImportRulesStart nichts). Fälle samt update-Zweigen löschen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
