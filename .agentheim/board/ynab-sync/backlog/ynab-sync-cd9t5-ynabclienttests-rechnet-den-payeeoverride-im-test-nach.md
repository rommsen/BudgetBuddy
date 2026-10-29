---
id: ynab-sync-cd9t5
title: "YnabClientTests rechnet den PayeeOverride im Test nach"
status: backlog
type: chore
context: ynab-sync
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
Verletzte Regel: .claude/rules/test-doctrine.md, Kein Test für Punkt 6

## What
Fundstelle: `src/Tests/YnabClientTests.fs:690`

"uses PayeeOverride when provided" löschen (src/Tests/ForceImportCandidateTests.fs:168 prüft den echten Request) oder auf `Server.PayeeFormatting.resolvePayeeName` umstellen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
