---
id: infrastructure-ky2kh
title: "Struktur-Wächter: CLIMutable auf Dapper-Row-Typen, Domain pur, SQL ohne Interpolation"
status: doing
type: feature
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`domain-purity`, `persistence-safety`, `query-safety` zielen auf nicht existierende bzw. Teil-Pfade und greifen für `src/Server/Ledger/`, `src/Server/Budgeting/` nie (Audit A, B1, geprüft). Souls W3, W4, W7.

## What
Reflection-Wächter `[<CLIMutable>]` nach Vorlage `souls/src/Tests/CliMutableGuard.Tests.fs`. Quelltext-Wächter über `src/Server/**/Domain.fs` (kein `UtcNow`, `NewGuid`, I/O). Wächter gegen String-Interpolation in SQL über `**/Persistence.fs` und `QueryHandlers.fs`. Jeder Wächter mit eingebautem Gegenbeweis.

## Acceptance criteria
- [ ] Die drei Wächter sind grün auf main oder die Funde sind als Teil des Tasks behoben.
- [ ] Jeder Wächter hat einen Gegenbeweis-Test, der zeigt, dass er anschlägt.

## Notes
Migrationsschritt M6. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Nach diesem Schritt messen (Baseline-Replay).
