---
id: 0080
title: Target-Änderungen nur im laufenden Monat, in eigenen Tabellen festgehalten, gegen den gezeigten Stand geplant, unter einer Sperre je Kategorie geschrieben, ohne eigenen Erneut-Weg
scope: budgeting
status: accepted
date: 2026-11-04
supersedes: []
superseded_by: []
related_tasks: [budgeting-k8wq3, budgeting-xp46a]
related_adrs: [0065, 0069, 0072, 0074]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# ADR 0080: Target-Änderungen festhalten und schreiben

## Context
ADR 0072 legt fest, welche Felder BB beim Ändern eines Targets sendet. Offen blieb (ADR 0072, Neutral), ob eine Änderung aufgezeichnet wird und wie ein Fehlschlag aussieht. Budget-Bewegungen (ADR 0065, 0069) halten altes und neues Zugewiesen fest, beide Spalten sind NOT NULL, "Zuletzt verwendet" und das Öffnen des Monatsfensters lesen die Tabelle. Der Target-Schreibweg `PATCH /budgets/{id}/categories/{id}` hat keinen Monat, das Target gilt für die Kategorie. "Jährlich mit Datum" braucht zwei Aufrufe (ADR 0072 Punkt 5). ADR 0074 Punkt 5 erlaubt "Target ändern, pausieren" im aktuellen und in künftigen Monaten. Roman hat am 2026-11-04 entschieden: Ein Target gilt ab dem Monat, in dem man es ändert, also wird es nur im laufenden Monat geändert.

## Decision
1. **Nur im laufenden Monat.** Anlegen, Ändern und Entfernen gehen nur im laufenden Monat (`targetEditAllowed`). Vergangene und künftige Monate zeigen das Target nur an, die API-Grenze lehnt sie ohne YNAB-Aufruf ab. Nachtrag zu ADR 0074 Punkt 5: Dort steht "Target ändern" für aktuell und künftig, künftig gilt jetzt nicht mehr. Pausieren bleibt im aktuellen und in künftigen Monaten (ADR 0073 Punkt 5).
2. **Eigene Aufzeichnung.** Tabellen `target_changes` und `target_change_results`, append-only mit Triggern wie in ADR 0065 Punkt 2. Eine Änderung trägt Zeitpunkt, Kategorie, den gezeigten Monat, die Art der Änderung und alte und neue Werte. Die Historie liest Bewegungen, Pausen und Target-Änderungen zusammen. Der Begriff ist **Target-Änderung**, keine Budget-Bewegung.
3. **Geplant gegen den gezeigten Stand.** Der Client schickt das gezeigte und das gewünschte Target. Eine reine Funktion `planTargetWrite` bestimmt die Patches. Der Server liest frisch: Weicht YNAB vom gezeigten Stand ab, gewinnt YNAB, es wird nichts gesendet und nichts festgehalten. Steht YNAB schon auf dem Wunsch, wird nichts gesendet.
4. **Erst festhalten, dann senden.** Alle Schritte einer Änderung in einer Transaktion, dann der Reihe nach senden, je Schritt ein Ergebnis. Schritt 2 geht nur hinaus, wenn Schritt 1 geschrieben ist und YNABs Antwort den neuen Rhythmus zeigt.
5. **Sperre je Kategorie.** Eine `TargetGuard` mit der Kategorie-Id als Schlüssel hält die Schritte einer Änderung zusammen und reiht Änderungen derselben Kategorie hintereinander. Die Sperre je Monat passt nicht, weil das Target für alle Monate gilt. Mit Targets anwenden gibt es keinen Konflikt, den `previewsMatch` nicht schon meldet.
6. **Kein eigener Erneut-Weg.** Ein Fehlschlag wird konkret gemeldet, die Eingabe bleibt stehen. Erneutes Speichern plant wieder gegen den gezeigten Stand. Scheitert Schritt 2, ist der gezeigte Stand der aus Schritt 1, und das erneute Speichern sendet nur das Datum. Schritt 1 wird nie wiederholt, weil `goal_frequency` ein inzwischen gesetztes Datum löschen würde.

## Consequences
### Positive
- Budget-Bewegungen behalten ihre Bedeutung, "Zuletzt verwendet" und das Monatsfenster bleiben unberührt.
- Eine in YNAB geänderte Kategorie wird nie still überschrieben.
- Kein Hinweis und kein Endpoint für offene Target-Änderungen.

### Negative
- Die Historie liest drei Quellen.
- Nach einem Neuladen der App ist die Meldung zu einem fehlgeschlagenen Datum weg, das Target steht ohne Datum in YNAB. Nur die Historie zeigt den fehlgeschlagenen Schritt.
- Zwischen den zwei Aufrufen steht das Target mit vollem Fehlbetrag in YNAB (ADR 0072, Negative).

### Neutral
- Der Monat an der Änderung ist immer der laufende, ab ihm gilt das Target nach Romans Verständnis. Ob YNAB Vormonate neu rechnet (etwa `goal_under_funded`), ist nicht geprüft; der Integrationstest aus budgeting-k8wq3 beobachtet es. Rechnet YNAB den Vormonat neu, entscheidet Roman vor dem ersten Einsatz, wie BB alte Monate zeigt.
- Die Tabelle "Monate" in `docs/ux/budget/konzept.md` und die README-Zeile Monatsart trennen "Target ändern" (nur aktuell) und "pausieren" (aktuell und künftig).

## Alternatives considered
- **Zeile in `budget_movements` mit gleichem altem und neuem Zugewiesen.** Verworfen: der Betrag der Bewegung wäre 0 und bedeutungslos, Leser der Tabelle müssten die Herkunft filtern.
- **Gar nicht aufzeichnen.** Verworfen: ein Fehlschlag wäre nach einem Neuladen nicht nachvollziehbar.
- **Ändern auch in künftigen Monaten, mit Hinweis "gilt für die Kategorie".** Verworfen (Roman): ein Target gilt ab dem Monat, in dem man es ändert, ein Ändern im Dezember, das auch den November ändert, wäre irreführend.
- **Erneut senden wie bei Targets anwenden.** Verworfen: das erneute Speichern gegen den gezeigten Stand erreicht dasselbe ohne zweiten Endpoint.
- **Vergleich gegen den frisch gelesenen Stand statt gegen den gezeigten.** Verworfen: eine Änderung in YNAB zwischen Anzeigen und Speichern würde still überschrieben.

## References
- ADR 0065, 0069, 0072, 0074 (Nachtrag zu Punkt 5)
- research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md, Abschnitt 3

## Nachtrag 2026-11-05 (ADR 0084)

Nach ADR 0084 führt BB die Targets, die Goals in YNAB sind eingefroren. Mit budgeting-eh8xe gilt:

- **Punkt 3:** Geplant wird weiter gegen den gezeigten Stand, verglichen wird gegen BBs Faltung
  des Streams `budgeting:target:<categoryId>` statt gegen YNAB. `planTargetWrite` entfällt, es
  gibt keine Patches mehr.
- **Punkt 4:** Eine Änderung ist ein Ereignis (`TargetSet` mit vollständiger Regel oder
  `TargetRemoved`). Nach YNAB geht nichts, die Zwei-Schritt-Logik entfällt.
- **Punkt 5:** `TargetGuard` bleibt und reiht die Ereignisse im Target-Stream einer Kategorie.
- **Punkt 6:** Ohne Senden gibt es keinen Teilfehler; die Regeln zu Schritt 1 und 2 entfallen.
  Erneutes Speichern plant weiter gegen den gezeigten Stand.
- Punkt 1 gilt weiter (`effectiveFrom` ist der laufende Monat). Die Tabellen aus Punkt 2 gehen
  mit budgeting-hyn77 in `events` auf, alte Zeilen als Altbestand-Typ.
