---
id: 0074
title: Freie Monate. Monatswert statt BudgetMonthChoice, das Fenster kommt aus GET /months, Fähigkeiten je Monatsart
scope: budgeting
status: accepted
date: 2026-11-02
supersedes: []
superseded_by: []
related_tasks: [budgeting-v4ht9, budgeting-g8s3w, budgeting-q7m2c]
related_adrs: [0065, 0069]
related_research: [ynab-reihenfolge-monate-target-schreiben-2026-11-02]
---

# ADR 0074: Freie Monate

## Context
Die Budget-Seite kennt drei Monate: `BudgetMonthChoice` mit "last", "current" und "next". Die
Annahme dahinter stammt aus der Probe vom 2026-10-26 (budgeting-k7wmd), der nächste Monat sei die
Grenze. Das Gesamtkonzept will freies Blättern.

Die Probe budgeting-q7m2c zeigt die echten Grenzen: Nach hinten ist `first_month` die Grenze.
Nach vorn reicht das Fenster einen Monat hinter den letzten Monat, der je eine Zuweisung ungleich
0 hatte, und hängt nicht am Kalender. `GET /months` beschreibt es vollständig. Außerhalb geben
Lesen und Schreiben 404. Eine Zuweisung im letzten Monat öffnet den nächsten, auch nach dem
Rücksetzen auf 0, einen Weg zurück hat die Probe nicht gefunden.

Dieselbe Probe zeigt: `GET /months/{month}` ist ungeordnet und je Monat anders geordnet.
`GET /categories` ist über Aufrufe stabil und wirkt von Hand sortiert.

## Decision
1. **Ein Monatswert ersetzt `BudgetMonthChoice`.** Client und Server tauschen den Monat selbst
   aus. Die Schlüssel "last", "current" und "next" entfallen.
2. **Das Fenster kommt aus `GET /months`.** BB blättert genau über diese Monate, am Rand fehlt
   der Pfeil. BB rechnet die Grenzen nicht selbst.
3. **404 heißt "Monat nicht offen"**, beim Lesen wie beim Schreiben.
4. **Keine Schein-Zuweisungen zum Schieben.** BB verschiebt das Fenster nie von sich aus. Ein
   weiterer Monat öffnet sich, wenn eine echte Zuweisung im letzten Monat ihn geöffnet hat.
5. **Die Monatsart bestimmt die Fähigkeiten.** Vergangen, aktuell oder künftig folgt aus dem
   Vergleich mit dem laufenden Monat, der als Parameter kommt:

   | Fähigkeit | Vergangen | Aktuell | Künftig |
   |---|---|---|---|
   | Ansehen, Buchungen, Historie | ja | ja | ja |
   | Zuweisen, Geld holen und abgeben | ja | ja | ja |
   | Targets anwenden | nein | ja | ja |
   | Target ändern, pausieren | nein | ja | ja |

   Nachtrag 2026-11-04 (ADR 0080, Entscheidung des Owners): Target ändern, anlegen und
   entfernen geht nur im aktuellen Monat. Pausieren bleibt im aktuellen und in künftigen Monaten.

6. **Reihenfolge der Kategorien aus `GET /categories`.** Roman hat am 2026-11-02 bestätigt, dass sie
   seiner YNAB-Anzeige entspricht. Gruppen und Kategorien stehen in dieser Reihenfolge, die
   Monatswerte kommen weiter aus `GET /months/{month}`, zusammengeführt über die Kategorie-Id.

## Consequences
### Positive
- BB kann jeden Monat zeigen, den YNAB offen hat, und braucht keine Annahme über den Kalender.
- Die Seite zeigt Romans Reihenfolge ohne eigene Erfassung in BB.
- Der falsche Kommentar in `toBudgetMonth` fällt weg.

### Negative
- Jede Zuweisung im letzten offenen Monat verschiebt die Grenze dauerhaft. Einen Weg zurück hat die
  Probe nicht gefunden.
- `PATCH` in alte Monate ist nur für den Vormonat belegt. Monate wie 2019 sind es nicht, obwohl
  die Tabelle Zuweisen in vergangenen Monaten erlaubt.
- Die Seite braucht zusätzliche Aufrufe gegen das Rate-Limit.
- Hat ein Budget gar keine Zuweisung im laufenden Monat oder im Vormonat, liegt der laufende
  Monat außerhalb des Fensters. Bei Romans Budget ist das kein Normalfall, BB zeigt dann "Monat
  nicht offen".

### Neutral
- Der Monat bleibt die Einheit der Sperre je Monat (ADR 0065, ADR 0069).
- Die Aussage aus budgeting-k7wmd, der nächste Monat sei die Grenze, ist überholt.

## Alternatives considered
- **`BudgetMonthChoice` um weitere Fälle erweitern.** Verworfen: Die Grenzen hängen an den
  Zuweisungen in YNAB, feste Fälle bilden das nicht ab.
- **Fenster selbst rechnen.** Verworfen: `GET /months` liefert es fertig.
- **Fenster mit Schein-Zuweisungen schieben.** Verworfen: Es bliebe dauerhaft offen und YNAB
  zeigte Zuweisungen, die niemand gemacht hat.
- **Reihenfolge alphabetisch.** Verworfen: nicht billiger, und es ist nicht Romans Reihenfolge.

## References
- ADR 0065, 0069
- docs/ux/budget/konzept.md, Abschnitt Monate
- research/ynab-reihenfolge-monate-target-schreiben-2026-11-02.md, Abschnitte 1 und 2
