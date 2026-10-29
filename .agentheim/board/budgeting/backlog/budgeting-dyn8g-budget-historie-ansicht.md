---
id: budgeting-dyn8g
title: "Budget-Historie: eigene Ansicht #/budget/history mit Filter nach Art und Abschnitt Bewegungen im Kategorie-Detail, kompakte Zeilen, Targets-Anwendung aufklappbar"
status: backlog
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001, budgeting-afd8h, budgeting-m1bce]
blocks: []
tags: [budget, historie]
related_adrs: [0065, 0069]
related_research: []
prior_art: [budgeting-h2zfn]
---

## Why
Vision, Budget Stufe 2: sehen, wann wie viel zugewiesen, verschoben oder per Targets verteilt wurde. Die Daten liegen seit budgeting-h2zfn in budget_movements und budget_movement_results; mit budgeting-afd8h kommen Verschieben und Zuweisen dazu (ADR 0069). Eine Ansicht fehlt.

## What
**Allgemeine Ansicht** `#/budget/history`, erreichbar über einen Link auf der Budget-Seite. Neueste zuerst, alle Monate. Ein Filter nach Art: Alle, Targets anwenden, Verschieben, Zuweisen.

Eine **Zeile je Durchlauf** (gleiche Gruppen-Id):
- **Verschieben:** Zeitpunkt, "20,00 € Lebensmittel → Urlaub", Monat, Stand.
- **Zuweisen:** Zeitpunkt, "+20,00 € Urlaub" bzw. "−20,00 € Urlaub" (zurück nach Zu verteilen), Monat, Stand.
- **Targets anwenden:** Zeitpunkt, "Targets anwenden, 37 Kategorien, 1.234,00 €", Monat, Stand; aufklappbar zu den Einzelzeilen (Kategorie, +Betrag, Stand).
- **Stand** ist das jüngste Ergebnis je Bewegung: geschrieben, fehlgeschlagen (mit Grund), in YNAB geändert, noch offen. Ein Durchlauf mit mindestens einer fehlgeschlagenen Bewegung ist als solcher markiert.

**Pro Kategorie:** im Kategorie-Detail (budgeting-m1bce) ein Abschnitt "Bewegungen" mit den Bewegungen dieser Kategorie im gewählten Monat, dieselbe Zeilenform aus Sicht der Kategorie ("−20,00 € nach Urlaub", "+50,00 € Targets anwenden"). Ein Link "Alle Bewegungen" führt zur allgemeinen Ansicht.

Lesend; keine YNAB-Anfrage. Zuweisungen direkt in YNAB erscheinen nicht (ADR 0065, Negative); die Ansicht sagt das in einem Satz unter der Liste.

## Acceptance criteria
- [ ] Server-Abfrage: Durchläufe mit ihren Bewegungen und dem jüngsten Ergebnis je Bewegung, neueste zuerst, optional nach Herkunft und nach Kategorie/Monat gefiltert. Integrationstest mit In-Memory-SQLite über alle drei Herkünfte und mehrere Ergebnisse je Bewegung.
- [ ] Reine Funktion Bewegungen → Anzeige-Zeilen (Gruppierung, Betrag = neu − alt, Text je Art, Durchlauf-Stand): Unit-Tests je Art, inklusive Targets-Durchlauf mit einer fehlgeschlagenen Zeile.
- [ ] `#/budget/history` wird geparst und erzeugt (Unit-Test), Link von der Budget-Seite.
- [ ] Filter nach Art zeigt nur Durchläufe dieser Herkunft.
- [ ] Targets-Durchlauf ist aufklappbar und zeigt die Einzelzeilen.
- [ ] Kategorie-Detail zeigt den Abschnitt "Bewegungen" für Kategorie und Monat; "Alle Bewegungen" führt zur Ansicht.
- [ ] Browser-Wächter (Handy-Breite): Historie öffnen, Filter "Verschieben", eine Zeile ist sichtbar (Fixture mit einer Verschiebung).
- [ ] README budgeting: "Eine Historie-Ansicht gibt es noch nicht" ersetzt durch die Beschreibung der Ansicht.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Verfeinert am 2026-10-29. **Entscheidungen (Roman, 2026-10-29):**
- Eigene Ansicht plus Abschnitt im Kategorie-Detail.
- Kompakte Zeilen, Targets anwenden als eine aufklappbare Zeile.

"Wer" aus der Vision entfällt vorerst: nur Roman budgetiert (README, Actors). Die Herkunft steht für "wie".
