---
id: budgeting-q7m2c
title: "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"
status: doing
type: spike
context: budgeting
created: 2026-11-02
completed:
depends_on: []
blocks: [budgeting-v4ht9, budgeting-g8s3w]
tags: [budget, ynab, reihenfolge, monate, target]
related_adrs: [0063, 0065, 0069]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
prior_art: [budgeting-r4tkq, budgeting-k7wmd]
---

## Why
Das Gesamtkonzept der Budget-Seite (`docs/ux/budget/konzept.md`) steht. Drei Fragen an YNAB entscheiden noch über den Zuschnitt der Bau-Tasks.

Roman sieht die Kategorien in BB nicht in seiner YNAB-Reihenfolge. `toBudgetMonth` (`src/Server/Budgeting/Domain.fs`) übernimmt die Reihenfolge aus `GET /months/{month}`, der Kommentar nennt das YNABs Reihenfolge. Die Spec sagt dazu nichts und kennt kein Sortierfeld.

Roman will frei durch die Monate blättern. Die Probe vom 2026-10-26 ergab für zwei Monate voraus 404 (README budgeting, Open questions).

Target ändern soll über BB gehen, aber `goal_day` ist nicht schreibbar und ging im Oktober beim Löschen und Neuanlegen verloren.

## What
Lesend gegen Romans Budget, schreibend nur gegen das Budget Test. Kein Produktionscode.

1. **Reihenfolge:** Stimmt die Reihenfolge der Gruppen und Kategorien aus `GET /budgets/{id}/categories` mit der YNAB-Anzeige überein? Vergleich mit `GET /months/{month}`. Roman bestätigt die Anzeige-Reihenfolge von Hand. Falls keine API sie liefert: Welche Alternative ist die billigste (Reihenfolge einmalig in BB erfassen, alphabetisch)?
2. **Monatsgrenzen:** Ab welchem Monat liefert `GET /months/{month}` nach hinten und nach vorn einen Fehler? Hängt die Grenze nach vorn von vorhandenen Zuweisungen ab, und was liefert `GET /months`? Geht `PATCH .../months/{month}/categories/{id}` für einen alten Monat und für den weitesten künftigen?
3. **Target schreiben:** Im Budget Test für je ein Target `monthly`, `weekly` mit Wochentag und ein Target mit Datum: Bleiben `goal_day`, `goal_cadence`, `goal_cadence_frequency`, `goal_needs_whole_amount` und `goal_target_date` erhalten, wenn nur `goal_target` gesendet wird? Was passiert beim Wechsel der Art? Lässt sich ein jährlich wiederkehrendes Ziel mit Datum anlegen?

**Stop-loss:** Wenn sich mitten im Spike zeigt, dass die Antwort schon bekannt und billig ist, wird sie festgehalten und der Spike endet dort.

## Acceptance criteria
- [ ] Bericht unter `.agentheim/knowledge/research/` beantwortet die drei Fragen mit den beobachteten Antworten, ohne Tokens, Kontodaten oder echte Kategorienamen.
- [ ] Der Bericht empfiehlt je Frage das Vorgehen für die Bau-Tasks in einem Satz.
- [ ] README budgeting ist bei den offenen Fragen aktualisiert.
- [ ] Das Budget Test ist nach dem Spike dokumentiert (was geändert blieb).

## Notes
Ausführung über echte Schlüssel nur außerhalb des normalen Testlaufs; ein normaler Testlauf ruft nie echte APIs auf.
