---
id: 0078
title: Nachtrag zu ADR 0069. Das Feld Zugewiesen schickt den fertigen Wert und setzt absolut, Verschiebungen und der Fehlbetrag-Knopf bleiben relativ
scope: budgeting
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-c5w2n]
related_adrs: [0069, 0070, 0055, 0065, 0074]
related_research: [ynab-api-targets-zuweisung-2026-10-25]
---

# ADR 0078: Nachtrag zu ADR 0069, Zuweisen setzt absolut

## Context
ADR 0069 Punkt 2 legt für Verschieben und Zuweisen fest: Der Client schickt Quelle, Ziel, Monat und Betrag, der Server rechnet relativ zum frisch gelesenen Monat. Das passte, solange Zuweisen nur "Zu verteilen" als Gegenseite im Sheet Verschieben war.

Das Gesamtkonzept bringt ein Feld Zugewiesen, das rechnet und beim Verlassen speichert. `900+40` ersetzt den Stand, Roman sieht das Ergebnis und meint genau diesen Wert. Gespeichert wird ohne Knopf, oft mehrmals kurz hintereinander. Ein relativer Auftrag hätte hier zwei Probleme: Weicht YNAB vom gezeigten Stand ab, landet ein anderer Wert als der gezeigte. Bricht der Aufruf ab, wäre ein zweiter Versuch doppelt.

## Decision
1. **Neuer Befehl Zuweisen (`assignMoney`).** Der Client schickt Monat, Kategorie und das neue Zugewiesen. Der Server liest den Monat frisch unter der Sperre je Monat, nimmt das gelesene Zugewiesen als altes Zugewiesen, hält eine Bewegung mit Herkunft "Zuweisen" fest und sendet einen PATCH. Fehlschlag, Baseline und erneutes Senden laufen wie in ADR 0069 Punkt 3 über `retryFailedTargets`.
2. **Kein Vergleich mit dem gezeigten Stand.** Der Auftrag trägt keinen erwarteten alten Wert. Der eingegebene Wert gilt, auch wenn YNAB inzwischen anders steht. Die Bewegung hält den tatsächlich überschriebenen Wert fest.
3. **Steht YNAB schon auf dem Wert, entsteht nichts:** keine Bewegung, kein PATCH, der Bericht hat keine Zeilen.
4. **Relativ bleibt, was ein Betrag ist.** Holen, Abgeben und der Fehlbetrag-Knopf "12,30 zuweisen" laufen weiter über `moveMoney`, auch mit Zu verteilen als Gegenseite. Wer 12,30 € zuweist, meint 12,30 €.
5. **Der Client sendet Zuweisungen nacheinander.** Es läuft höchstens eine, weitere warten in der Reihenfolge des Speicherns, eine wartende für dieselbe Kategorie und denselben Monat wird ersetzt. Eine Verschiebung startet erst, wenn nichts mehr wartet. Sonst überschriebe eine verspätete absolute Zuweisung eine Verschiebung.

## Consequences
### Positive
- Der gezeigte Wert ist der gesendete. Ein abgebrochener Aufruf lässt sich durch erneute Eingabe ohne Doppelwirkung wiederholen.
- Die Budget-Historie sieht dieselben Zeilen wie bisher, alt und neu aus dem frischen Stand.

### Negative
- Ändert jemand denselben Wert parallel in YNAB, überschreibt das Feld ihn ohne Rückfrage. Das ist eine Abweichung vom Geist von ADR 0055 ("YNAB gewinnt") beim ersten Senden. Roman ist der einzige, der budgetiert, und die Bewegung hält den alten Wert fest. Beim erneuten Senden gilt ADR 0055 unverändert.
- Ein weiterer Endpoint mit Zeile in der Negativtabelle und eigener Validierung.
- Jede gespeicherte Zuweisung kostet drei YNAB-Anfragen (Lesen, PATCH, Neu-Lesen des Monats).

### Neutral
- Herkunft, Schema und Sperre je Monat bleiben. ADR 0069 Punkt 2 gilt weiter für Verschiebungen.
- Eine Zuweisung im letzten offenen Monat öffnet den nächsten (ADR 0074), das gilt auch für das Speichern beim Verlassen.

## Alternatives considered
- **Differenz im Client bilden und `moveMoney` nutzen.** Kein neuer Endpoint, verworfen aus den Gründen im Context: das Ergebnis hinge am Stand von YNAB, und ein abgebrochener Aufruf wäre nicht wiederholbar.
- **Absolut mit erwartetem Stand, Ablehnung bei Abweichung.** Verworfen: Gespeichert wird beim Verlassen, Roman ist dann schon bei der nächsten Kategorie, die Eingabe ginge verloren. Das bremst den Normalfall für einen seltenen Konflikt.

## References
- ADR 0069 Punkte 2 und 3, ADR 0055, ADR 0074
- docs/ux/budget/konzept.md, Abschnitt Zuweisen
