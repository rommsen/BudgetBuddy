---
id: budgeting-p2zr6
title: "Passt das Target noch? Prüfung je Kategorie aus dem Kassenbuch, gespeichertes So lassen, Tab-Markierung Target • und Feld an BudgetCategory für den Prüf-Filter"
status: backlog
type: feature
context: budgeting
created: 2026-11-04
completed:
depends_on: [design-system-001, budgeting-t6p8z, budgeting-k8wq3]
blocks: [budgeting-m3x7f]
tags: [budget, target, pruefen]
related_adrs: [0065, 0072, 0075]
related_research: []
prior_art: [budgeting-dyn8g]
---

## Why
Teil D von budgeting-t6p8z. Roman will erkennen, wenn ein Target nicht mehr zur Wirklichkeit passt, bevor er es anwendet ("Targets prüfen" steht in der Leiste von budgeting-m3x7f vor "Targets anwenden"). YNAB rechnet das nicht, das Kassenbuch kann es: Die Ausgaben je Monat liegen seit budgeting-dyn8g vor.

## What
Regeln: `docs/ux/budget/konzept.md` Abschnitt Targets.
- Die Prüfung steht bei der Kategorie, an jedem Tag. Der Tab zeigt dann `Target •`, die Liste trägt den Hinweis in Gelb (ADR 0075 Punkt 1).
- "So lassen" wird gespeichert und überlebt das Neuladen, sonst fragt BB jeden Monat dasselbe.
- Grundlage sind dieselben Kassenbuch-Summen wie die Ausgaben der letzten 6 Monate (`Ledger.QueryHandlers.monthlyActivity`).

**Entschieden (Orchestrator, Empfehlung des Tactical-Modelers):** Die Prüfung kommt als Feld `TargetCheck: TargetCheck option` an `BudgetCategory`. Der Server rechnet es in `getBudgetMonth` aus einer gruppierten Kassenbuch-Abfrage für alle Kategorien des Monats (kein N+1) über eine reine Funktion in `Shared.Budgeting`. Liste, `Target •`, Filterzahl und `matchesFilter` (`Month.fs:376`) lesen dann eine Quelle. "So lassen" liefert die neue Prüfung zurück, der Client setzt sie ohne YNAB-Anfrage ein (Vorbild `withReport`). Ein Parameter an `matchesFilter` bräuchte eine zweite Abfrage, die den YNAB-Stand noch einmal kennen muss. budgeting-m3x7f ergänzt nur einen Fall `CheckOnly` in `CategoryFilter`.

## Fachfragen (vor der Verfeinerung mit Roman an echten Daten klären)
1. Welcher Vergleichswert gilt je Art? Monatlich: Ausgaben je Monat gegen den Betrag. Wöchentlich: Betrag mal Wochen des Monats? Jährlich mit Datum: Summe über das Jahr oder gar nicht? TB und TBD (Sparziele): gar nicht?
2. Über welchen Zeitraum wird verglichen: die 6 Monate der Historie, 12 Monate, und zählt der laufende Monat mit?
3. Durchschnitt oder Median, und wie wirkt ein einzelner Ausreißer (etwa eine Jahresrechnung in einer Monatskategorie)?
4. Meldet sich die Frage nur bei mehr Ausgaben als Target oder auch bei deutlich weniger?
5. Wie groß ist die Schwelle: Prozent, fester Betrag oder beides (etwa "mehr als 10 % und mindestens 5 €")?
6. Welche Kategorien sind ausgenommen: ohne Ausgaben, neu angelegt (weniger Monate als der Zeitraum), pausiert im Monat?
7. Was speichert "So lassen": den Vergleichswert zum Zeitpunkt, eine Frist oder beides? Was heißt danach "spürbar anders": dieselbe Schwelle gegen den gespeicherten Wert oder eine eigene?
8. Setzt eine Target-Änderung (budgeting-k8wq3, budgeting-xp46a) "So lassen" zurück?
9. Gilt "So lassen" je Kategorie oder je Kategorie und Monat?
10. Was bietet der Tab Target neben "So lassen" an: einen Vorschlag für den neuen Betrag aus dem Vergleichswert, oder nur den Sprung ins Formular aus budgeting-k8wq3?
11. Erscheint die Prüfung auch in künftigen Monaten? Dort ist das Target nur lesbar (Roman, F4: ändern nur im laufenden Monat). Gilt "So lassen" dort ebenfalls, oder steht die Prüfung nur im laufenden Monat?

## Acceptance criteria
Werden nach den Antworten auf die Fachfragen geschärft. Fest steht nur:
- [ ] "So lassen" überlebt das Neuladen und meldet sich erst bei spürbar anderem Vergleichswert.
- [ ] `TargetCheck` steht an `BudgetCategory`; die Fixtures in `scripts/*.test.mjs` tragen das Feld im selben Schritt.
- [ ] Neue Tabelle für "So lassen" ist auf bestehender Datenbank wiederholbar angelegt.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Angelegt am 2026-11-04 bei der Teilung von budgeting-t6p8z. Keine Kriterien erfunden: die Vergleichsregel legt die taktische Modellierung mit Roman an echten Daten fest. Das ADR zur Prüfregel folgt erst danach.
