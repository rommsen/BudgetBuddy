---
id: categorization-h2w8k
title: "Muster testen im Regel-Dialog läuft ohne Retry.reading, an der Importzeile mit"
status: backlog
type: bug
context: categorization
created: 2026-11-03
completed:
depends_on: []
blocks: []
tags: [client, retry]
related_adrs: []
related_research: []
prior_art: [infrastructure-t5m2b]
---

## Why
Bei infrastructure-t5m2b aufgefallen: `testRule` ist ein lesender Aufruf. An der Importzeile (`Components/SyncFlow/State.fs`) läuft er über `Retry.reading`, im Regel-Dialog (`Components/Rules/State.fs`, `testPatternCmd`) nicht. Bricht dort die Verbindung einmal ab (Status 0), meldet der Dialog sofort "nicht geprüft", statt still zu wiederholen.

## What
Klären, ob der Regel-Dialog dieselbe Wiederholung bekommt. Falls ja: `Retry.reading` dort einsetzen und das Inventar in `src/Tests/RetryTests.fs` mitziehen.

## Acceptance criteria
- [ ] Entscheidung festgehalten (gleich behandeln oder begründet verschieden).
- [ ] Bei Änderung: Regressionstest, der vor dem Fix rot ist (Inventar oder Verhalten), `./verify.sh` PASS.