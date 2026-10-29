---
id: ledger-z71ka
title: "Bot-Commit meldet nach gescheitertem YNAB-Push entry_id null, obwohl die Buchung im Kassenbuch steht"
status: backlog
type: bug
context: ledger
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Scheitert der Push, verliert `addCashExpense` die EntryId (`src/Server/BotApi.fs:334-339`, `src/Server/Ledger/CommandHandlers.fs:684-717`). Bob legt neu an, es entsteht eine Doppelbuchung. Widerspricht `docs/bot-api.md:39` (Audit B1 M2, B2, C B3).

## What
Die Antwort nach gescheitertem Push trägt die EntryId der gespeicherten Buchung und einen Push-Status.

## Acceptance criteria
- [ ] Kaputt-Szenario: YNAB-Push schlägt fehl, Antwort enthält die EntryId der Kassenbuch-Buchung; Test rot vor dem Fix.
- [ ] Ein zweiter Commit mit derselben Vorschau erzeugt keine zweite Buchung.
- [ ] `docs/bot-api.md` beschreibt den Fall Push gescheitert.

## Notes
Migrationsschritt P4. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.

