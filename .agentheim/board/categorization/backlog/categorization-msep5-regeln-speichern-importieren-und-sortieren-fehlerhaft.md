---
id: categorization-msep5
title: "Regeln speichern, importieren und sortieren fehlerhaft"
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
Gruppe 1 aus dem Bestandsreview categorization vom 2026-10-29, höchste Schwere **hoch**. Beim Ändern, Anlegen und Importieren von Regeln gehen Werte verloren oder werden still verfälscht (Override nicht löschbar, alter Kategoriename, Platzhalter Unknown, halber Import, offene Reihenfolge bei gleicher Priorität).

## What
Alle Befunde der Gruppe, je Zeile Fundstelle | Regel | Schwere | Vorschlag. Bericht: `.agentheim/knowledge/reviews/2026-10-29-categorization.md`.

- `src/Server/Api.fs:597` | .agentheim/knowledge/contexts/categorization/README.md, Ubiquitous language "PayeeOverride"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | hoch | Ein gesetzter PayeeOverride einer Regel lässt sich nicht mehr entfernen.
- `src/Server/Api.fs:628` | .claude/rules/query-safety.md, "Kein stiller Default" | hoch | Kategoriewechsel einer Regel speichert bei YNAB-Fehler den alten Kategorienamen.
- `src/Server/Api.fs:550` | .claude/rules/query-safety.md, "Kein stiller Default"; .claude/rules/semantic-messaging.md, "Kein Befehls-DU ohne Aufrufer" | mittel | Regeln mit unbekannter Kategorie werden mit dem Platzhalternamen Unknown gespeichert.
- `src/Server/Persistence.fs:330 und :341` | .claude/rules/query-safety.md, "Kein stiller Default in Konvertern" | mittel | Unbekannte PatternType- und TargetField-Werte aus der Datenbank werden still zu Regex bzw. Combined.
- `src/Server/Api.fs:665 bis :690` | .claude/rules/query-safety.md, mehrere Schreibvorgänge in einer Transaktion; standards/backend/overview.md, "Validierung an der API-Grenze"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Regel-Import läuft ohne Transaktion und ohne die Regel-Validierung.
- `src/Server/Persistence.fs:364` | .agentheim/knowledge/contexts/categorization/README.md, "Priority: die erste passende Regel gewinnt"; .claude/rules/test-doctrine.md, Heimat-Ebene Punkt 1 | mittel | Bei gleicher Priorität ist offen, welche Regel gewinnt, und die Reihenfolge ist ungetestet.
- `src/Server/Api.fs:699 und :716` | standards/backend/overview.md, "konkrete Meldungen"; .claude/rules/test-doctrine.md, Kein Test für Punkt 6 (Gegenprobe) | mittel | Regel testen meldet ein ungültiges Muster als kein Treffer, und der Treffer-Fall ist ungetestet.
- `src/Client/Components/Rules/State.fs:260` | .agentheim/knowledge/contexts/categorization/README.md, Aggregate Rule "sinnvolle Priority" | mittel | Neue Regeln bekommen Priorität 1, wenn die Regelliste nicht geladen ist.
- `src/Server/Api.fs:49` | standards/backend/overview.md, "konkrete Meldungen auf Deutsch" | mittel | Fehlermeldungen der Regeln und Splits erscheinen beim Nutzer auf Englisch.
- `src/Server/Api.fs:581` | semantic-messaging.md, Namen nach Absicht | niedrig | Validierungsfehler nicht als `InvalidPattern ("update"/"import")`, eigener Fall `ValidationFailed`.
- `src/Server/Validation.fs:122` | semantic-messaging.md, Kein Befehls-DU ohne Aufrufer | niedrig | `validatePayeeOverride` und `RulesError.DuplicateRule` einbinden oder löschen (CategoryNotFound siehe categorization-syy3j).
- `src/Shared/Domain.fs:380` | semantic-messaging.md, Nie CreateXInput | niedrig | `RuleCreateRequest`/`RuleUpdateRequest` nach Absicht benennen.
- `src/Server/Api.fs:545` | semantic-messaging.md, Logik nicht inline in Api.fs | niedrig | Kategoriename-Suche viermal inline (545, 621, 990, 1065), ein Helfer mit einheitlichem Fehlerverhalten.
- `src/Server/Api.fs:688` | Lesbarkeit, Mutabilität | niedrig | `let mutable importedCount` und `updatedTransactions` (1076) durch Ausdrücke ersetzen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-30, raw, unrefined. Bewusst groß; im Refinement (`agentheim:modeling`) kleiner schneiden, wenn der Owner das will.
