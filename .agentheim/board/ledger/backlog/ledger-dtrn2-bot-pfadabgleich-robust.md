---
id: ledger-dtrn2
title: "isBotPath vergleicht /bot exakt und mit Groß-/Kleinschreibung, Caddy matcht anders: Pfadabgleich in BB robust machen (Security-Review N1, niedrig)"
status: backlog
type: chore
context: ledger
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured, security, bob]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Weicht der Pfadabgleich von BB und vom Proxy ab, kann ein Pfad am Proxy vorbei anders behandelt werden als gedacht.

## What
`BotApi.isBotPath` (ca. Zeile 287) vergleicht `/bot` exakt und mit Groß-/Kleinschreibung. Caddy matcht ohne Groß-/Kleinschreibung und dekodiert `%2f`. Die Homelab-Seite stellt Caddy auf `path_regexp ^/bot(/|$)` um. Optional soll BB zusätzlich robust sein. Priorität niedrig.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Security-Review der Homelab-Sitzung (homelab-58) zu Bobs `/bot/`-Zugang; laut homelab-58 hat Roman dem Verteilen zugestimmt. Gewünschte Reihenfolge: M4 (infrastructure-z9b5w), M2 (ledger-ybm5x), M1 (ledger-ssmp3), M3 (ledger-bbkgs), dann N1 (ledger-dtrn2). Dieser Fund: N1.
