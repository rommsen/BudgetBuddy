---
id: 0069
title: Verschieben und Zuweisen als Budget-Bewegungen, relativ angewendet, unter der Monatssperre
scope: budgeting
status: accepted
date: 2026-10-29
supersedes: []
superseded_by: []
related_tasks: [budgeting-afd8h, budgeting-dyn8g]
related_adrs: [0050, 0055, 0065]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0069: Verschieben und Zuweisen als Budget-Bewegungen

## Context
ADR 0065 hält Budget-Bewegungen nur für Targets anwenden fest (Herkunft "Targets
anwenden", gruppiert über `application_id`). Stufe 1 Punkt 2 bringt Geld verschieben
(budgeting-afd8h), am Handy schnell. Roman hat am 2026-10-29 entschieden: "Zu verteilen"
ist im selben Sheet als Quelle oder Ziel wählbar (also auch Zuweisen und Zurücknehmen),
die Quelle darf negativ werden, verschoben wird im gewählten Monat der Seite. Die
Budget-Historie (budgeting-dyn8g) soll nach Art filtern können.

YNAB kennt kein Verschieben. Es gibt nur das absolute Setzen von Zugewiesen je Kategorie
und Monat (PATCH /months/{month}/categories/{id}). Eine Verschiebung zwischen zwei
Kategorien sind also zwei PATCHes, eine Zuweisung aus Zu verteilen ist einer, weil Zu
verteilen sich aus den Zuweisungen ergibt.

## Decision
1. **Zwei neue Herkünfte** in `budget_movements.origin`: "Verschieben" und "Zuweisen".
   Eine Verschiebung zwischen Kategorien schreibt zwei Bewegungen (Quelle: Zugewiesen
   sinkt, Ziel: Zugewiesen steigt) mit gemeinsamer `application_id`. Zuweisen aus oder
   zurück nach Zu verteilen schreibt eine Bewegung mit Herkunft "Zuweisen". Das Schema
   bleibt, die Spalte `application_id` gruppiert jetzt jeden Durchlauf (Anwendung oder
   Verschiebung). Keine Migration.
2. **Relativ angewendet.** Der Client schickt Quelle, Ziel, Monat und Betrag, keine
   Vorschau. Der Server liest den Monat frisch aus YNAB, rechnet neues Zugewiesen = altes
   ± Betrag, hält die Bewegungen fest (eine Transaktion) und sendet dann nacheinander
   (erst Quelle, dann Ziel). Anders als bei Targets anwenden gibt es keinen
   Vorschau-Vergleich: wer 20 € verschiebt, meint 20 €, unabhängig davon, was inzwischen
   in YNAB passiert ist.
3. **Fehlschlag wie ADR 0065 Punkt 3.** Baseline ist das alte Zugewiesen, kein Rollback.
   Scheitert nur das Ziel, ist das Geld vorübergehend in Zu verteilen; die Zeile ist
   fehlgeschlagen und erneut sendbar (idempotent, weil absolut gesetzt wird). Steht YNAB
   beim erneuten Senden weder auf Baseline noch auf Ziel, gewinnt YNAB ("in YNAB
   geändert"). Scheitert die Quelle, wird das Ziel nicht gesendet, auch beim erneuten
   Senden (Ergänzung 2026-10-30, budgeting-afd8h): sonst stiege das Ziel, ohne dass die
   Quelle sinkt, und das Geld käme still aus Zu verteilen. Beide Zeilen sind dann
   fehlgeschlagen. Das weicht von Targets anwenden ab, wo die übrigen Zeilen weiterlaufen,
   weil dort jede Zeile für sich steht.
4. **Gleiche Sperre je Monat** wie Targets anwenden. Eine Verschiebung und eine Anwendung
   für denselben Monat laufen nicht ineinander.
5. **Keine Sperre gegen negatives Verfügbar** in der Quelle (wie Zu verteilen, ADR 0065).
   Beträge auf 10 Milliunits (Cent-Eingabe ergibt das immer).

## Consequences
### Positive
- Die Historie kennt ab dem ersten Verschieben drei Arten und kann danach filtern.
- Kein neues Schema, keine Migration, dieselbe Rückschreib- und Erneut-Senden-Mechanik.

### Negative
- Zwischen den beiden PATCHes ist der YNAB-Stand halb verschoben (Zu verteilen zu hoch).
  Bei einem Fehlschlag bleibt er so, bis erneut gesendet wird.
- `application_id` heißt nicht mehr genau, was es tut. Umbenennen lohnt die Migration
  nicht; die Bedeutung steht hier und im README.

### Neutral
- Zwei PATCHes pro Verschiebung belasten das Rate-Limit (~200/h) kaum.

## Alternatives considered
- **Absolut mit Vorschau-Vergleich wie Targets anwenden.** Verworfen: kostet einen
  Bestätigungsschritt und verliert gegen YNAB beim Tempo (Scheiter-Kriterium).
- **Eigene Tabelle für Verschiebungen.** Verworfen: die Historie müsste zwei Quellen
  zusammenführen; die Bewegung ist dieselbe Sache mit anderer Herkunft.
- **Zuweisen nur über Targets anwenden.** Verworfen von Roman (2026-10-29): Zu verteilen
  gehört als Eintrag in denselben Picker.

## References
- ADR 0065 (Budget-Bewegungen, Rückschreib-Ergebnisse, Sperre je Monat)
- ADR 0050 (erst festhalten, dann senden)
