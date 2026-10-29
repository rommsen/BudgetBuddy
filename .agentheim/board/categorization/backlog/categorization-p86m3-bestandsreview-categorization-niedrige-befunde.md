---
id: categorization-p86m3
title: "Bestandsreview categorization: niedrige Befunde"
status: backlog
type: chore
context: categorization
created: 2026-10-29
completed:
depends_on: []
blocks: []
tags: [captured, bestandsreview]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Befund aus `/architecture-review categorization` am 2026-10-29, Schwere **niedrig**.
Verletzte Regel: je Befund unten

## What
Fundstelle: `siehe Liste`

Je Zeile: Fundstelle | Regel | Vorschlag.

- src/Client/Components/Rules/State.fs:94 | elmish-state.md, Ladeantwort-Bindung | `CancelConfirmDelete of RuleId`, nur zurücksetzen, wenn dieselbe Id noch bestätigt wird.
- src/Client/Components/Rules/Types.fs:115 | semantic-messaging.md, Absicht statt CRUD | `UpdateRuleForm*`-Setter (115 bis 122) nach Nutzerabsicht benennen (`PatternEntered`, `CategoryChosen` usw.).
- src/Client/Components/Rules/Types.fs:125 | categorization README, Key events RuleCreated/RuleUpdated | `SaveRule`/`RuleSaved` in Anlegen und Ändern trennen, `IsNewRule` plus `EditingRule` als DU `Editor`.
- src/Client/Components/Rules/State.fs:52 | elmish-state.md, Ladeantwort-Bindung | späte `LoadRules`-Antwort überschreibt eine optimistische `MoveRule`-Reihenfolge; Ladeversion mitführen oder MoveRule sperren.
- src/Client/Components/Rules/View.fs:400 | styleguide §5 ErrorDisplay.warning | selbst gebauter Warnkasten "Keine Kategorien geladen" durch `ErrorDisplay.warning` ersetzen.
- src/Client/Components/Rules/View.fs:311 | styleguide §8 konsistente Begriffe | "z.B." (311, 368 bis 370) zu "z. B.", "..." (393, 472, 648) zu "…".
- src/Client/Components/Rules/View.fs:476 | styleguide §8 Buttons als Verb | "Test" zu "Testen".
- src/Client/Components/Rules/View.fs:648 | styleguide §6 Skeletons | `NotAsked` und `Loading` zeigen dasselbe Skeleton.
- src/Client/Components/SyncFlow/Views/SplitSheet.fs:52 | standards/frontend/overview.md prop.key | stabile Id je `SplitDraftLine` statt Index-Key.
- src/Client/Views/QuickAddPage.fs:262 | elmish-state.md, Option statt Leerwert | `CategoryId: YnabCategoryId option` statt "".
- src/Client/Views/QuickAddPage.fs:256 | styleguide §7.1 Punkt 4 Layer-Klasse | `categoryPickerLayered` stapelt auf der Seite über nichts, Kopfkommentar Zeile 15 veraltet.
- src/Client/Views/QuickAddPage.fs:162 | Zugänglichkeit, Beschriftung | Label "Kategorie" per `htmlFor`/`id` mit dem Auswahl-Knopf verknüpfen.
- src/Client/DesignSystem/BottomSheet.fs:403 | styleguide §2 "Keine Treffer" | leere Kategoriesuche zeigt "Keine Treffer".
- src/Client/DesignSystem/BottomSheet.fs:597 | styleguide §2 "Keine Treffer" | leerer Account-Picker als Hinweiszeile statt Abschnittskopf.
- src/Client/DesignSystem/BottomSheet.fs:335 | Zugänglichkeit, Beschriftung | `ariaLabel` "Kategorie suchen" (auch Zeile 566 für Konten).
- src/Server/Api.fs:581 | semantic-messaging.md, Namen nach Absicht | Validierungsfehler nicht als `InvalidPattern ("update"/"import")`, eigener Fall `ValidationFailed`.
- src/Server/Api.fs:979 | semantic-messaging.md, Namen nach Absicht | unbekannte Transaktions-Id als `TransactionNotFound` statt `SessionNotFound` (auch skip, split, clearSplit).
- src/Server/Api.fs:1114 | categorization README, Order-ID-Matching | `splitTransaction` leert `SuggestedByOrderId` wie categorize und bulk.
- src/Server/Validation.fs:122 | semantic-messaging.md, Kein Befehls-DU ohne Aufrufer | `validatePayeeOverride` und `RulesError.DuplicateRule` einbinden oder löschen (CategoryNotFound siehe categorization-syy3j).
- src/Shared/Domain.fs:380 | semantic-messaging.md, Nie CreateXInput | `RuleCreateRequest`/`RuleUpdateRequest` nach Absicht benennen.
- src/Server/Api.fs:545 | semantic-messaging.md, Logik nicht inline in Api.fs | Kategoriename-Suche viermal inline (545, 621, 990, 1065), ein Helfer mit einheitlichem Fehlerverhalten.
- src/Server/Api.fs:688 | Lesbarkeit, Mutabilität | `let mutable importedCount` und `updatedTransactions` (1076) durch Ausdrücke ersetzen.
- src/Tests/RulesEngineTests.fs:215 | test-doctrine.md, Stil Punkt 19, Kein Test für Punkt 6 | Test heißt High Priority, prüft aber nur die Listenreihenfolge; Daten und Namen auf "erste Regel der Liste gewinnt".
- src/Client/Components/Rules/State.fs:303 | test-doctrine.md, Heimat-Ebene Punkt 1 | update-Test für `RuleSaved (Ok _)`: neu vorn, bearbeitet an derselben Stelle, Dialog zu.
- src/Tests/RulesEngineTests.fs:601 | test-doctrine.md, Heimat-Ebene Punkt 2 | `textBasedSpecialLinkTests` doppelt `specialPatternTests` (303 bis 356); zusammenlegen.
- src/Tests/RulesListTests.fs:104 | test-doctrine.md, Kein Test für Punkt 6 (Konstanten) | Wortlaut-Vergleich auf `priorityHint` streichen.
- src/Tests/RulesEngineTests.fs:60 | test-doctrine.md, Stil Punkt 19 | englische Testnamen (auch OrderIdMatcherTests.fs:64ff., RulesReorderTests.fs:40ff.) beim nächsten Anfassen als deutsche fachliche Sätze.

Nicht gemeldet, weil schon erfasst: SplitSheet.fs:39 ("Kategorie oder Konto…", Sync-Symbol) steht in design-system-bym4m. Hinweis für infrastructure-q7v2m: die Quelltext-Tests in src/Tests/RulesListTests.fs:62-108 und src/Tests/SyncFlowViewTests.fs:543-580, 644-665 gehören bei Bedarf in deren Dateiliste. Ohne Regel-Fundstelle, nicht bewertet: src/Server/RulesEngine.fs:143 kompiliert Regex aus Nutzereingaben ohne matchTimeout.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-29, raw, unrefined. Needs a `modeling` refine pass before it can be promoted. Quelle: Bestandsreview categorization (infrastructure-fam9k, erster Lauf).
