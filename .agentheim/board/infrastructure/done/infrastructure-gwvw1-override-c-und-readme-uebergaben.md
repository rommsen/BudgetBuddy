---
id: infrastructure-gwvw1
title: "Override (C) einführen und BC-README-Übergaben für Bug-Szenarien und Test-Doktrin"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed:
depends_on: [infrastructure-dvy29, infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Souls E4, E10, E11: Review-Agents als Session-End-Empfehlung, Bug-Tasks mit kaputt-/intakt-Szenario im Refine.

## What
Eine Zeile in CLAUDE.md: nach jedem work-Lauf passende Review-Agents vorschlagen und im Session-End-Eintrag nennen, nicht automatisch starten. BC-READMEs: Bug-Szenario-Regel und Verweis auf die Test-Doktrin.

## Acceptance criteria
- [ ] CLAUDE.md enthält die Override-(C)-Regel.
- [ ] Jede BC-README nennt die Bug-Szenario-Regel.

## Notes
Migrationsschritt M11. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Conductor-Task: bleibt im Backlog, kein Worker. Der Owner begleitet den Schritt.

## Outcome

Vom Conductor umgesetzt. CLAUDE.md hat unter "Qualitätssicherung" den Abschnitt "Override (C): Reviews nach einem work-Lauf vorschlagen": am Ende jedes work-Laufs passende Review-Agents vorschlagen und im Session-End-Eintrag nennen, nicht starten (qa-milestone-reviewer bei Tests, backend-code-quality bei `src/Server/**`, frontend-architecture bei `src/Client/**`, design-quality bei sichtbaren UI-Änderungen); ab zehn erledigten Tasks eines BC seit dem letzten `Bestandsreview: <bc>` zusätzlich `/architecture-review <bc>` (Zählregel aus dem gemeinsamen Review-Vertrag mit Souls, fam9k/k74df). Alle sieben BC-READMEs haben vor "Test command" den Abschnitt "Szenarien und Tests": Bug-Tasks mit kaputt- und intakt-Szenario (kaputt zuerst rot), fachliches Verhalten als Szenario mit Test, Verweis auf `.claude/rules/test-doctrine.md`. Die Szenario-Testsprache aus Souls (M17, `ClientScenario`/`ServerScenario`) gibt es in BudgetBuddy nicht; der Abschnitt verlangt deshalb nur einen Test je Szenario, ohne Titelpflicht. `npm run test:scripts` grün (319).
