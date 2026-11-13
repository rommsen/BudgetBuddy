---
id: ynab-sync-k9r4v
title: "Kann-Sammlung ynab-sync"
status: backlog
type: chore
context: ynab-sync
created: 2026-11-14
completed:
depends_on: []
blocks: []
tags: [ynab-sync, review]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Funde aus Reviews, die stimmen, aber die Schwelle für ein eigenes Ticket nicht erreichen (CLAUDE.md, Backlog-Disziplin).
Wird nie automatisch gebaut.

## Funde
1. **Review-Lauf 2026-11-14 (ledger-ea70s)**: `src/Server/YnabClient.fs` `createTransactionFor` (~:1271) hat keinen Aufrufer, der Kommentar an `sharedHttpClient` (~:1226) verweist noch darauf. Löschen und Kommentar anpassen.
