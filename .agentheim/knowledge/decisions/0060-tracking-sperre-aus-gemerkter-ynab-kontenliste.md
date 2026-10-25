---
id: ADR-0060
title: Tracking-Konto im Lesepfad der Split-Sperren aus einer gemerkten YNAB-Kontenliste, nicht aus der Projektion
scope: ledger
status: accepted
date: 2026-10-25
related_tasks: [ledger-pafkp]
related_adrs: [0055, 0058, 0059]
---

# ADR-0060: Tracking-Konto im Lesepfad aus einer gemerkten YNAB-Kontenliste

## Context

Der Editor braucht vor dem Speichern zu wissen, ob eine Buchung auf einem Tracking-Konto liegt (ADR 0058 Punkt 6). `LedgerAccount` und `YnabAccountObservation` tragen kein `OnBudget` (ADR 0059 Punkt 5: der Befehl fragt live). Ein neues Feld in diesen Records bricht dutzende Testliterale und liefert für bestehende Konten erst nach einem weiteren Spiegel-Lauf einen Wert.

## Decision

1. Der Lesepfad leitet das Tracking-Konto aus der YNAB-Kontenliste des Standard-Budgets ab (`YnabClient.fetchBudgetAccounts`), im Server gemerkt für fünf Minuten (`Server.Api.budgetAccounts`). Fehlschläge werden nicht gemerkt. `Server.Api` ergänzt damit die Sperren an den Sichten (`withTrackingLocks`, `ReadModel.withTrackingLock`, `ReadModel.trackingLedgerAccounts`).
2. Fehlt die Liste (kein Token, YNAB nicht erreichbar), bleibt es bei den lokalen Sperren. Der Befehl `setEntrySplits` prüft beim Speichern weiter live und bleibt maßgeblich.
3. Dieselbe Liste speist die Ziele der Übertrags-Zeilen (`LedgerApi.getBudgetAccounts`, der Client filtert mit `openOnBudgetAccounts`).
4. Die Sperren reisen als ein Feld `LedgerEntryView.SplitLocks: SplitLocks option`. `None` heißt nichts gesperrt, auch für Antworten eines älteren Servers.

## Consequences

- Kein Schema- und kein Spiegel-Eingriff, Wirkung sofort auch für bestehende Konten.
- Ein Tracking-Konto, das Roman in YNAB umstellt, wirkt im Lesepfad nach höchstens fünf Minuten.
- Die Sichten hängen für die Tracking-Sperre an einem YNAB-Aufruf, höchstens einer je fünf Minuten.
- Verworfen: `OnBudget` in die Projektion legen (Recordänderungen, Backfill erst mit dem nächsten Lauf).

## Note on ADR numbering

Minted provisionally as ADR-0061 in its worker worktree. A sibling task's ADR already claimed that number (or the guess overshot the true count) by the time this task's conductor finalized numbering at squash-merge integration (`lib/adr-allocation.mjs`'s `finalizeAdrNumbering`, ADR-0058) — this ADR was renumbered to **ADR-0060**, the true next-free number on `main` at that moment. No content besides this identity changed.
