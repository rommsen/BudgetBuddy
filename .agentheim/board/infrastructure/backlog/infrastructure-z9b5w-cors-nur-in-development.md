---
id: infrastructure-z9b5w
title: "CORS AllowAnyOrigin gilt in Produktion für die ganze API: auf Development beschränken (Security-Review M4)"
status: backlog
type: bug
context: infrastructure
created: 2026-10-28
completed:
depends_on: []
blocks: []
tags: [captured, security, cors]
related_adrs: []
related_research: []
prior_art: []
---

## Why
BB hat keine Anmeldung. Mit `access-control-allow-origin: *` kann jede Webseite, die im Browser eines Tailnet-Geräts geöffnet ist, die API lesen und schreiben. Unabhängig von Bob, deshalb als erstes.

## What
`src/Server/Program.fs` (ca. Zeile 40 bis 61): Die Policy "AllowAll" (`AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`) und `app.UseCors("AllowAll")` gelten in jeder Umgebung, obwohl der Kommentar "for development" sagt. Live geprüft von homelab-58: Ein Preflight mit `Origin: https://evil.example` auf `https://budgetbuddy.elver-algol.ts.net/api/ISettingsApi/getSettings` liefert 204 und `access-control-allow-origin: *`.

Vorschlag aus dem Review: CORS nur in Development aktivieren.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-28, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Security-Review der Homelab-Sitzung (homelab-58) zu Bobs `/bot/`-Zugang; laut homelab-58 hat Roman dem Verteilen zugestimmt. Gewünschte Reihenfolge: M4 (infrastructure-z9b5w), M2 (ledger-ybm5x), M1 (ledger-ssmp3), M3 (ledger-bbkgs), dann N1 (ledger-dtrn2). Dieser Fund: M4.
