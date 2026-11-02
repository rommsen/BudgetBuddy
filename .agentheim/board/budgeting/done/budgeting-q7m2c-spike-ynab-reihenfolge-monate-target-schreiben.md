---
id: budgeting-q7m2c
title: "Spike: Liefert YNAB die Anzeigereihenfolge der Kategorien, wie weit reichen Monate, und erhält eine Target-Änderung Tag und Rhythmus?"
status: done
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

## Outcome

Spike abgeschlossen. Bericht: `.agentheim/knowledge/research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md`
(Entwurf in `.worktrees/.results/budgeting-q7m2c-report.md`, der Conductor verschiebt ihn). Romans
Budget wurde nur gelesen, geschrieben wurde nur im Budget Test.

1. **Reihenfolge:** `GET /months/{month}` ist ungeordnet (126 von 128 sichtbaren Positionen weichen von `GET /categories` ab, Gruppen nicht zusammenhängend) und wechselt von Monat zu Monat. Der Kommentar an `toBudgetMonth` trifft also nicht zu. `GET /categories` ist über Aufrufe stabil, weder alphabetisch noch nach Id und wirkt von Hand sortiert; die Spec kennt kein Sortierfeld. Empfehlung: nach `/categories` ordnen und über die Kategorie-Id mit den Monatswerten zusammenführen. **Offen:** Roman bestätigt die Anzeige an der lokalen Datei `.worktrees/.results/budgeting-q7m2c-reihenfolge.txt`, die nie committet wird.
2. **Monatsgrenzen:** Nach hinten gilt `first_month`. Nach vorn endet das Fenster einen Monat hinter der letzten Zuweisung und hängt nicht am Kalender; `GET /months` zeigt es genau. Jenseits davon geben GET und PATCH 404. Eine Zuweisung ungleich 0 im letzten Monat öffnet den nächsten, das Zurücksetzen auf 0 schließt ihn nicht wieder. `PATCH` auf den Vormonat und den letzten Monat geht; Monate älter als der Vormonat sind nicht belegt, weil Test erst 2026-10 beginnt. Empfehlung: über die Liste aus `GET /months` blättern.
3. **Target schreiben:** Nur `goal_target` senden erhält Tag, Rhythmus, Datum und `goal_needs_whole_amount`. `goal_frequency` ersetzt das Target: Das Datum geht immer verloren (auch bei gleicher Art), der Tag beim Wechsel der Art. `goal_day` ist nicht schreibbar. Jährlich mit Datum geht in zwei Aufrufen. Empfehlung: nur geänderte Felder senden und beim Wechsel der Art warnen.

**Nicht belegt:** "Bleibt erhalten" für NEED monatlich mit `goal_day` (42 Targets in Romans Budget), alle 3 Monate mit Datum (4) und MF (18), weil sich diese Formen über die API nicht herstellen lassen. Vor dem ersten Target-Schreiben in Romans Budget sollte Roman ein Monats-Target mit Tag im Budget Test von Hand anlegen, damit ein `PATCH` nur mit `goal_target` das belegt. Die Wirkung von `goal_creation_month` auf Vormonate ist ungeprüft.

**Budget Test danach:** Zuweisungen auf 0, die Probe-Targets entfernt. Geändert geblieben: Transportation ohne `goal_day` (vorher 6, nicht wiederherstellbar), `goal_creation_month` bei Transportation und Vacation auf 2026-11, Monatsfenster bis 2027-02 (vorher 2026-12).

Die README-Bullets sind als Append ins Abschnitt "Open questions" gemeldet; die Prüfung mit `scripts/readme-delta-check.mjs` lief für das Delta mit Exit 0. Nach dieser Prüfung wurde der Wortlaut des dritten Bullets zu `goal_frequency` korrigiert, die Struktur blieb gleich.
