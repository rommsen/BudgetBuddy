---
id: ledger-bbkgs
title: "Bot-Vorschau und /bot/categories fragen YNAB ohne Cache: Kategorien cachen, /bot/ drosseln (Security-Review M3)"
status: backlog
type: feature
context: ledger
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured, security, bob, ynab-ratelimit]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Eine Schleife bei Bob könnte das YNAB-Ratenlimit aufbrauchen, das sich Bob mit Sync und Quick Add teilt.

## What
Jede Bot-Vorschau und jedes `GET /bot/categories` ruft YNAB direkt ab (`BotApi.fs`, `liveDeps` ca. Zeile 336, über `YnabClient.getCategories`).

Vorschlag aus dem Review: Kategorien einige Minuten cachen und `/bot/` drosseln (z. B. 10 Anfragen pro Minute).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Security-Review der Homelab-Sitzung (homelab-58) zu Bobs `/bot/`-Zugang; laut homelab-58 hat Roman dem Verteilen zugestimmt. Gewünschte Reihenfolge: M4 (infrastructure-z9b5w), M2 (ledger-ybm5x), M1 (ledger-ssmp3), M3 (ledger-bbkgs), dann N1 (ledger-dtrn2). Dieser Fund: M3.
