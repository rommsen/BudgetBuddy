---
id: infrastructure-vc6c3
title: "Comdirect- und YNAB-Test rufen bei jedem dotnet test echte Dienste auf, ohne RUN_INTEGRATION_TESTS zu prüfen"
status: done
type: bug
context: infrastructure
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
`src/Tests/ComdirectIntegrationTests.fs:111` prüft nur die API-Keys und startet mit echtem Client-Id/Secret einen OAuth-Login mit falschem Benutzer (geprüft). `src/Tests/YnabIntegrationTests.fs:177` ruft die echte API mit Fantasie-Token (Audit D).

## What
Beide Tests laufen nur mit `RUN_INTEGRATION_TESTS=true`. Ein Wächter verhindert neue Netzaufrufe in Tests ohne Schalter (siehe M5).

## Acceptance criteria
- [ ] Ohne Schalter werden beide Tests übersprungen; die Zahl übersprungener Tests steigt von 6 auf 8.
- [ ] Mit Schalter laufen sie wie bisher.

## Notes
Migrationsschritt P6. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Bug-Fix-Protokoll: Kommentar im Test, welcher Fehler verhindert wird.

## Outcome

Umgesetzt durch infrastructure-am805 (Commit bbf154b), kein eigener Worker-Lauf. `YnabIntegrationTests` "handles invalid token gracefully" und `ComdirectIntegrationTests` "handles invalid credentials gracefully" prüfen jetzt `shouldRunIntegrationTests()` und werden ohne `RUN_INTEGRATION_TESTS=true` übersprungen. Der Verifier von am805 zählte 8 statt 6 übersprungene Tests. Der Netz-Wächter `src/Tests/NetworkCallGuardTests.fs` verhindert neue Netzaufrufe in Tests ohne Schalter. Kommentar im Test zum verhinderten Fehler: siehe beide Testfälle.
