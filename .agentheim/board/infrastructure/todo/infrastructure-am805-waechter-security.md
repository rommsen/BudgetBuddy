---
id: infrastructure-am805
title: "Security-Wächter: Pipeline-Test über Api.webApp, Negativtest je Remoting-Endpoint, Netzaufrufe nur hinter Schalter"
status: todo
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
Der /api-Ausfall (Fix 2a5c229) rutschte durch, weil kein Test die echte Giraffe-Pipeline fuhr. 13 von 75 Remoting-Endpoints ohne Test (Audit D). Souls E7, W1.

## What
Ein Test startet `Api.webApp()` über einen Test-Host und prüft je Remoting-Interface einen Aufruf mit Antwortrumpf. Ein tabellengetriebener Test über alle Remoting-Endpoints (Reflection) verlangt je Endpoint einen Negativfall; ein neuer Endpoint ohne Tabellenzeile macht den Test rot. Ein Wächter findet Tests mit Netzaufruf ohne `RUN_INTEGRATION_TESTS`-Prüfung.

## Acceptance criteria
- [ ] Pipeline-Test ist grün auf main und rot, wenn man `skipPipeline` in `BotApi.handler` testweise durch `next ctx` ersetzt.
- [ ] Endpoint-Tabelle deckt alle Remoting-Endpoints ab; Reflection-Abgleich schlägt bei fehlender Zeile fehl.
- [ ] Netz-Wächter schlägt bei einem Test ohne Schalter fehl (Gegenbeweis im Test).

## Notes
Migrationsschritt M5. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Startet vermutlich rot wegen fehlender Tabellenzeilen; die Zeilen sind Teil des Tasks.
