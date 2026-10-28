---
id: ledger-ybm5x
title: "BOT_MAX_AMOUNT_EUR parst "500,00" als 50000: nur Dezimalpunkt zulassen, harte Obergrenze im Code, Wert beim Start loggen (Security-Review M2)"
status: backlog
type: bug
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
Ein Tippfehler in der Konfiguration hebt die Obergrenze für Bob still um das Hundertfache an.

## What
`Server.ServerConfig.resolveBotMaxAmountEur` parst mit `NumberStyles.Number` und `InvariantCulture`. `NumberStyles.Number` enthält `AllowThousands`, deshalb ergibt "500,00" den Wert 50000 (von homelab-58 per `dotnet fsi` geprüft, im Code bestätigt).

Vorschlag aus dem Review: nur `AllowDecimalPoint`, eine harte Obergrenze im Code (die Konfiguration darf nur absenken), den wirksamen Wert beim Start loggen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Security-Review der Homelab-Sitzung (homelab-58) zu Bobs `/bot/`-Zugang; laut homelab-58 hat Roman dem Verteilen zugestimmt. Gewünschte Reihenfolge: M4 (infrastructure-z9b5w), M2 (ledger-ybm5x), M1 (ledger-ssmp3), M3 (ledger-bbkgs), dann N1 (ledger-dtrn2). Dieser Fund: M2.
