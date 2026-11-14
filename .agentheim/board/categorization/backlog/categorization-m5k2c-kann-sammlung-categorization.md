---
id: categorization-m5k2c
title: "Kann-Sammlung categorization"
status: backlog
type: chore
context: categorization
created: 2026-11-08
completed:
depends_on: []
blocks: []
tags: [review, ui, regeln]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Kann-Sammlung des BC nach der Regel Backlog-Disziplin in CLAUDE.md. Funde ohne Geld-, Daten-, Sicherheits- oder Alltagsfehler kommen als Zeile hierher. Sie wird nie automatisch gebaut.

## What
Geschnitten und priorisiert wird im Refinement.

## Aus dem Review-Lauf nach infrastructure-jc1y1 (2026-11-08)
- Regel-Dialog: Öffnet man das Kategorie-Popover, während der Dialog noch per `animate-scale-in` einfährt, rechnet `scrollIntoView` mit der skalierten Lage und das Popover steht halb unter dem Rand. Der Sweep-Zustand `regeln-dialog-picker` wartet deshalb per `settle` (`scripts/layout-states.mjs`). Abhilfe im Client: Popover erst nach dem Ende der Einfahrt positionieren oder neu messen.
- Review-Lauf 2026-11-11 (design-system-k6kfw): Die übrigen Quelltext-Prüfungen in `src/Tests/RulesListTests.fs` (Liste „Quelltext“) sind View-Verdrahtung per Teilstring (Doktrin Punkt 7); beim Umbau der Regeln (`categorization-n3sxw`) entfernen oder auf Verhalten umstellen.

## Aus categorization-hz6vj (Bestandsreview 2026-10-29), aufgegangen in banking-import-d0fey (2026-11-11)
Der Abbruch durch eine unlesbare Kategorie-Id (`src/Server/OrderIdMatcher.fs:61`) ist Kriterium in `banking-import-d0fey`. Die Funde zu `categorizeTransaction`, `bulkCategorize`, `splitTransaction`, `clearSplit` (fehlende API-Adapter-Tests, `TransactionNotFound` statt `SessionNotFound`, `SuggestedByOrderId` beim Aufteilen) entfallen mit den Endpunkten.
- `src/Shared/Domain.fs:524` (`YnabTransaction.CategoryId`) und `OrderIdSuggestion.CategoryId` (`src/Server/OrderIdMatcher.fs:7`): Kategorie-Ids aus der YNAB-Historie sind nackte Strings statt `YnabCategoryId` (`.claude/rules/semantic-messaging.md`, Fachbegriffe als Typen). Nach dem Bugfix in d0fey nur noch Lesbarkeit.
- Order-ID-Weitergabe: Mit dem Sync-Ablauf entfällt, dass Romans Kategorie an einer Importzeile auf Buchungen derselben Bestellnummer übergeht (`OrderIdMatcher.propagateInSession`, früher Key command PropagateOrderId). Der Vorschlag aus der YNAB-Historie beim Import bleibt. Im Kassenbuch gibt es die Weitergabe nicht.
- Kontrast unter 4,5:1 (Invariante 10, ledger-dn4vy, 2026-11-12): Hilfetexte im Regel-Dialog in `text-muted/70` (3,23:1), Ausnahme `regeln-dialog-picker` @ 1440, inv 10, `[data-testid="rule-dialog"] p`.
- Review-Lauf 2026-11-13 (categorization-n3sxw): Der Test zu `restoreRule` (`src/Tests/RuleCommandHandlerTests.fs` ~494) scheitert schon beim INSERT (doppelte Id); dass Einfügen und neue Prioritäten in einer Transaktion laufen, belegt kein Test. Ein Fehler nach dem Einfügen lässt sich ohne Port in `Persistence.Rules.restoreRuleAt` kaum erzeugen.
- Review-Lauf 2026-11-14 (categorization-m47g8): Eine neue Regel steht am Ende. Bei einer Liste länger als der Bildschirm liegt sie nach „Regel angelegt“ außer Sicht (am Handy schließt das Sheet, am PC kann die gewählte Zeile unter dem sichtbaren Bereich liegen). Abhilfe: die neue Zeile per `scrollIntoView` mit `block: "nearest"` holen, bei `prefers-reduced-motion` ohne `smooth`.
