---
id: ledger-mj3a9
title: "BOT_MAX_AMOUNT_EUR: ungültiger Wert fällt auf das Maximum zurück statt auf einen sicheren Wert"
status: backlog
type: bug
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
`resolveBotMaxAmountEur` liefert bei unlesbarem Wert die Vorgabe 500 EUR, die gleich dem harten Deckel ist (`src/Server/ServerConfig.fs:29,38,56`). Ein Tippfehler beim Absenken ("50,00") gibt Bob die höchste Grenze (Audit c-review B2, geprüft).

## What
Ein gesetzter, aber ungültiger Wert führt zu einem sicheren Ergebnis: Startabbruch oder niedrigster sinnvoller Wert. Welche Variante, entscheidet der Refine.

## Acceptance criteria
- [ ] Kaputt-Szenario: `BOT_MAX_AMOUNT_EUR="50,00"` ergibt nicht mehr 500 EUR; Test schlägt vor dem Fix fehl.
- [ ] Intakt-Szenario: nicht gesetzter Wert ergibt weiter 500 EUR, `"100.00"` ergibt 100 EUR.
- [ ] Die Startlog-Zeile nennt bei ungültigem Wert Rohwert und Folge.

## Notes
Migrationsschritt P3. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Refine: Startabbruch gegen niedrigen Fallback abwägen (ADR 0068).
