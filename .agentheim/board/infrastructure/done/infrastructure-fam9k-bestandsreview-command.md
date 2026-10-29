---
id: infrastructure-fam9k
title: "Bestandsreview-Command nach dem umgebauten Souls-/architecture-review"
status: done
type: chore
context: infrastructure
created: 2026-10-29
completed: 2026-10-29
depends_on: [infrastructure-cbr0r]
blocks: []
tags: [harness]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Die schweren Befunde lagen außerhalb des einzelnen Diffs bzw. kamen aus Reviews ohne Regeln (Audit Zusammenfassung Abschnitt 2). Souls M18.

## What
`.claude/commands/architecture-review.md` nach Souls-Vorlage: Parameter BC oder Dimension, liest nur die neuen Regeln, repariert nichts, legt Befunde per quick-capture an.

## Acceptance criteria
- [x] Der Command existiert und läuft für einen BC ohne Codeänderung.
- [x] Befunde landen als Backlog-Tasks im richtigen BC.

## Notes
Migrationsschritt M18. Zielbild: `.agentheim/knowledge/research/harness-audit/zielbild.md`. Promote und work erst nach ausdrücklichem Ja des Owners.
Erster Lauf je BC durch den Conductor nach M10.

## Outcome

`.claude/commands/architecture-review.md` (120 Zeilen). Kopf und Kern sind per `diff` wortgleich mit Souls `9fad518` (infrastructure-k74df), der Projektteil (BudgetBuddy) nennt die sieben BCs, die vier Dimensionen mit `backend-code-quality`, `frontend-architecture`, `qa-milestone-reviewer` und `design-quality` und die Quelle für den Code eines BC. Weil es keine CODE-MAP gibt und die Ordner unter `src/` den BCs nur teilweise folgen, ist die Dateiliste eines BC die Menge der heute vorhandenen Dateien unter `src/` und `scripts/`, die Commits der erledigten Tasks des BC (`[<id>]` im Betreff) geändert haben. `banking-import` hat noch keine erledigte Task, dort fragt der Command nach den Dateien.

Erster Lauf: `/architecture-review categorization`, vier Agents parallel, keine Codeänderung. Nach der Synthese hoch 3, mittel 49, niedrig 27. 52 Tasks ab mittel in categorization (32), banking-import (9), design-system (6) und ynab-sync (5), dazu `categorization-p86m3` mit den niedrigen Befunden. Die beiden hohen Backend-Befunde (PayeeOverride nicht entfernbar, Kategoriename bei YNAB-Fehler) hat der Conductor am Code bestätigt. Ein Befund war schon als `design-system-bym4m` erfasst und wurde nicht neu angelegt. Protokolleintrag `Bestandsreview: categorization`, die Zählregel liefert danach 0.

Offen, mit Souls abgestimmt, wartet auf den Owner: Claude Code ersetzt `$0` in Command-Dateien durch das Argument, die awk-Zählregel im Kern ist damit beim Aufruf mit BC kaputt. Vorschlag beider Sessions: `$ 0` und ein Muster, das auch `Task completed (verification skipped)` und das alte Souls-Archivformat zählt. Ids mit abweichendem Präfix (`ynab-`, `infra-`, Datums-Ids) zählt die Regel weiterhin nicht.
