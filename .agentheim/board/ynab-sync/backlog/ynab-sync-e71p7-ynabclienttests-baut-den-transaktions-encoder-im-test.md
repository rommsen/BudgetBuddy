---
id: ynab-sync-e71p7
title: "YnabClientTests baut den Transaktions-Encoder im Test nach"
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
Fundstelle: `src/Tests/YnabClientTests.fs:1100`

"uncategorized transaction is encoded without category_id" auf `YnabClient.encodeTransaction` (src/Server/YnabClient.fs:649) umstellen; dasselbe Muster in Zeile 635 und 1026, Zeile 635 kann weg, wenn prepareTransactions (Zeile 1397) die Felder abdeckt.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
