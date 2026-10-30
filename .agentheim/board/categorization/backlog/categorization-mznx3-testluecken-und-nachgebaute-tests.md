---
id: categorization-mznx3
title: "Testlücken und nachgebaute Tests"
status: backlog
type: chore
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
Gruppe 7 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **mittel**. Tests rechnen Produktionslogik im Test nach oder fehlen für Ablehnungen, Gegenproben und deaktivierte Regeln.

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Tests/RulesEngineTests.fs:157` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Payee- und Memo-Regeln haben keine Gegenprobe gegen das jeweils andere Feld.
- `src/Server/Api.fs:535 und :604` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule (bei Regex kompilierbar); .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 4 | mittel | Ablehnung eines kaputten Regex beim Anlegen und Ändern einer Regel ist ungetestet.
- `src/Tests/PersistenceTypeConversionTests.fs:81` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Eine deaktivierte Regel ist im Persistenz-Rundlauf nicht abgesichert.
- `src/Client/Components/Rules/State.fs:187` | .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1; .agentheim/board/categorization/done/cat-001-rule-reorder-ui.md, AC "Bei Fehler: Toast + Reload" | mittel | Fehlgeschlagenes Umsortieren der Regeln hat keinen Client-update-Test.
- `src/Tests/YnabClientTests.fs:690` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests rechnet den PayeeOverride im Test nach.
- `src/Tests/YnabClientTests.fs:1100` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | YnabClientTests baut den Transaktions-Encoder im Test nach.
- `src/Tests/YnabClientTests.fs:448` | .claude/rules/test-doctrine.md, Kein Test für Punkt 6 | mittel | Der Kategorien-Decoder ist im Test kopiert statt aus dem Produktionscode aufgerufen.
- `src/Tests/RulesEngineTests.fs:215` | test-doctrine.md, Stil Punkt 19, Kein Test für Punkt 6 | niedrig | Test heißt High Priority, prüft aber nur die Listenreihenfolge; Daten und Namen auf "erste Regel der Liste gewinnt".
- `src/Client/Components/Rules/State.fs:303` | test-doctrine.md, Heimat-Ebene Punkt 1 | niedrig | update-Test für `RuleSaved (Ok _)`: neu vorn, bearbeitet an derselben Stelle, Dialog zu.
- `src/Tests/RulesEngineTests.fs:601` | test-doctrine.md, Heimat-Ebene Punkt 2 | niedrig | `textBasedSpecialLinkTests` doppelt `specialPatternTests` (303 bis 356); zusammenlegen.
- `src/Tests/RulesListTests.fs:104` | test-doctrine.md, Kein Test für Punkt 6 (Konstanten) | niedrig | Wortlaut-Vergleich auf `priorityHint` streichen.
- `src/Tests/RulesEngineTests.fs:60` | test-doctrine.md, Stil Punkt 19 | niedrig | englische Testnamen (auch OrderIdMatcherTests.fs:64ff., RulesReorderTests.fs:40ff.) beim nächsten Anfassen als deutsche fachliche Sätze.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
