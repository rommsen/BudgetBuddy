---
id: infrastructure-b85yv
title: tzdata im Produktions-Image installieren
status: backlog
type: chore
context: infrastructure
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Im Produktions-Image (aspnet:10.0-alpine) fehlt tzdata. Die Bob-Tagesgrenze (ADR 0068) findet Europe/Berlin deshalb nicht über TimeZoneInfo und rechnet über den Rückfall `berlinOffsetByEuRules` in `src/Server/Validation.fs`.

## What
Im Dockerfile in der Runtime-Stufe `apk add --no-cache tzdata` ergänzen, damit TimeZoneInfo die echte Zone findet. Prüfen, ob der Rückfall danach bleiben soll (etwa für lokale Umgebungen ohne Zonendaten).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted.
