---
id: ledger-x2aan
title: "Bot-Drossel: globaler Zähler vor der Token-Prüfung erlaubt es Fremden, Bob auszusperren"
status: backlog
type: decision
context: ledger
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: [0068]
related_research: []
prior_art: []
---

## Why
`deps.Limiter.TryAcquire` läuft vor der Anmeldung mit einem einzigen Fenster (`src/Server/BotApi.fs:404`, `:488`). Zehn Anfragen pro Minute ohne Token sperren Bob. Laut ADR 0068 gewollt, der Zielkonflikt ist nicht benannt (Audit B1 M3, B2, C B1, geprüft).

## What
Entscheiden, ob getrennte Zähler (ohne Token / mit gültigem Token) oder Drossel nach der Anmeldung; ADR 0068 ergänzen.

## Acceptance criteria
- [ ] Eine Ergänzungs-ADR zu 0068 benennt den Zielkonflikt und die Entscheidung.
- [ ] Bei Änderung: ein Test zeigt, dass zehn Anfragen ohne Token einen Aufruf mit gültigem Token nicht blockieren.

## Notes
Migrationsschritt P2. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Reichweite von /bot hängt von Caddy/Tailscale ab; im Refine klären.
