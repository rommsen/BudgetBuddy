---
id: categorization-hz6vj
title: "Kategorisieren im Import: Order-Id und Review-Spur"
status: backlog
type: bug
context: categorization
created: 2026-10-30
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Gruppe 6 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Sammel-Kategorisierung überträgt nicht auf Order-Id-Geschwister, eine kaputte Id bricht confirmTan ab, Ids sind nackte Strings, und die Schreibpfade der Review-Spur haben keine API-Adapter-Tests.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Server/Api.fs:1077` | .agentheim/knowledge/contexts/categorization/README.md, "Order-ID-Matching", Key command PropagateOrderId | mittel | Sammel-Kategorisierung überträgt die Kategorie nicht auf Buchungen mit derselben Order-Id.
- `src/Server/OrderIdMatcher.fs:61` | .claude/rules/domain-purity.md, "Regelverletzungen sind Result-Werte, keine Ausnahmen" | mittel | Eine nicht lesbare Kategorie-Id aus der YNAB-Historie lässt den Order-Id-Abgleich und damit confirmTan abbrechen.
- `src/Shared/Domain.fs:507` | .claude/rules/semantic-messaging.md, Fachbegriffe als Typen | mittel | Kategorie-Ids in YnabTransaction und OrderIdSuggestion sind nackte Strings statt YnabCategoryId.
- `src/Server/Api.fs:974` | .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur" und Szenarien und Tests; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Manuelles Kategorisieren im Import hat keinen Test auf API-Adapter-Ebene.
- `src/Server/Api.fs:1054, :1095, :1125; src/Tests/OrderIdMatcherTests.fs:352` | .agentheim/knowledge/contexts/categorization/README.md, "Review-Spur"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 und Kein Test für Punkt 6 | mittel | Sammel-Kategorisieren, Aufteilen und Aufteilung entfernen haben keine API-Adapter-Tests.
- `src/Server/Api.fs:979` | semantic-messaging.md, Namen nach Absicht | niedrig | unbekannte Transaktions-Id als `TransactionNotFound` statt `SessionNotFound` (auch skip, split, clearSplit).
- `src/Server/Api.fs:1114` | categorization README, Order-ID-Matching | niedrig | `splitTransaction` leert `SuggestedByOrderId` wie categorize und bulk.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
