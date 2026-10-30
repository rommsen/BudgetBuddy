---
id: 0070
title: Gewinnt YNAB beim erneuten Senden an der Quelle, schließt das ungesendete Ziel als in YNAB geändert
scope: budgeting
status: accepted
date: 2026-10-30
supersedes: []
superseded_by: []
related_tasks: [budgeting-afd8h]
related_adrs: [0069, 0065, 0055]
related_research: []
---

# ADR 0070: Ungesendetes Ziel schließt, wenn YNAB an der Quelle gewinnt

## Context
ADR 0069 Punkt 3 sagt: Scheitert die Quelle einer Verschiebung, wird das Ziel nicht
gesendet, auch beim erneuten Senden. Und: steht YNAB beim erneuten Senden weder auf
Baseline noch auf Ziel, gewinnt YNAB (ADR 0055). Offen blieb, was mit dem Ziel geschieht,
wenn beides zusammenfällt: die Quelle wird beim erneuten Senden als "in YNAB geändert"
geschlossen, das Ziel steht noch auf fehlgeschlagen.

Das Ziel kann dann nicht offen bleiben, ohne eine neue Falle zu stellen: die Quelle ist
geschlossen und würde beim nächsten erneuten Senden übersprungen, das Ziel ginge also
ohne vorangehende Quelle hinaus.

## Decision
Erweitert ADR 0069 Punkt 3. Gewinnt YNAB beim erneuten Senden an der Quelle, wird das Ziel
im selben Lauf ungesendet ebenfalls als "in YNAB geändert" geschlossen. Die Verschiebung ist
damit erledigt, kein weiteres erneutes Senden fasst sie an.

Scheitert die Quelle dagegen wieder (Netz, Rate-Limit), bleibt das Ziel unverändert
fehlgeschlagen. Nur der Stand "in YNAB geändert" an der Quelle schließt das Ziel.

## Consequences
### Positive
- Das Ziel geht nie ohne eine geschriebene Quelle hinaus. Das Geld kommt nicht still aus Zu
  verteilen.
- Eine Verschiebung endet in einem Zustand, in dem nichts mehr erneut sendbar ist. Die
  Entscheidung liegt bei Roman.

### Negative
- **Verlorene Antwort.** Der Quelle-PATCH landet (100 auf 70), die Antwort geht verloren, BB
  meldet beide Zeilen als fehlgeschlagen. Danach ändert sich die Quelle in YNAB auf 65. Das
  erneute Senden sieht weder Baseline noch Ziel und schließt die Quelle, das Ziel schließt
  ungesendet mit. Die Quelle ist hier tatsächlich gesunken: die 30 € liegen in Zu verteilen,
  am Ziel sind sie nie angekommen. BB kann diesen Fall nicht vom Fall unterscheiden, in dem der
  PATCH nie landete und jemand die Quelle von Hand änderte. Roman muss das Geld von Hand
  verschieben, ein Hinweis darauf erscheint nicht.
- **Folge für budgeting-dyn8g.** Der Stand "in YNAB geändert" heißt bei einem Ziel einer
  Verschiebung nicht, dass YNAB diese Zeile angefasst hat. Eine Zeile, die YNAB nie berührt hat,
  kann diesen Stand tragen. Die Historie darf ihn dort nicht als Änderung an der Ziel-Kategorie
  darstellen und soll den Durchlauf als ganzen lesen: Quelle "in YNAB geändert" heißt, die
  Verschiebung wurde nicht vollständig ausgeführt.

### Neutral
- Es kommt kein neuer Stand dazu. Das Schema von `budget_movement_results` bleibt.

## Alternatives considered
- **Das Ziel bleibt fehlgeschlagen.** Verworfen: es bliebe erneut sendbar. Ein späteres erneutes
  Senden überspringt die geschlossene Quelle und schickt das Ziel, das Geld käme aus Zu verteilen.
  Genau das schließt ADR 0069 Punkt 3 aus. Die Zeile sähe ehrlicher aus, das Verhalten wäre
  falsch.

## References
- ADR 0069 Punkt 3 (Reihenfolge Quelle vor Ziel, YNAB gewinnt)
- ADR 0065 Punkt 3 (Fehlschlag, Baseline, kein Rollback)
- ADR 0055 (YNAB gewinnt)
- Tests in `src/Tests/MoveMoneyTests.fs`: "Hat YNAB bei der Quelle gewonnen ...", "GIVEN die
  Quelle landete, die Antwort ging verloren ..."
