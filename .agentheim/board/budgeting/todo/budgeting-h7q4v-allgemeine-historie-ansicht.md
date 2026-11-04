---
id: budgeting-h7q4v
title: "Allgemeine Budget-Historie #/budget/history: alle Bewegungen, neueste zuerst, Filter nach Art, Targets-Durchlauf aufklappbar, Knopf Historie im Seitenkopf"
status: todo
type: feature
context: budgeting
created: 2026-11-03
completed:
depends_on: [design-system-001, budgeting-dyn8g, infrastructure-b3t9k, budgeting-v6m3z]
blocks: []
tags: [budget, historie]
related_adrs: [0065, 0069]
related_research: []
prior_art: [budgeting-h2zfn]
---

## Why
Abgetrennt aus budgeting-dyn8g (Roman, 2026-11-03). Gesamtkonzept, Ablauf "Was ist passiert": Knopf Historie für alle, filterbar nach Art. Der Tab Historie zeigt nur eine Kategorie und einen Monat.

## What
Eigene Ansicht `#/budget/history`, geöffnet über den Knopf Historie im Seitenkopf der Budget-Seite. Neueste zuerst, alle Monate. Filter nach Art: Alle, Targets anwenden, Verschieben, Zuweisen.

Eine **Zeile je Durchlauf** (gleiche Gruppen-Id):
- Verschieben: Zeitpunkt, "20,00 € Lebensmittel → Urlaub", Monat, Stand.
- Zuweisen: Zeitpunkt, "+20,00 € Urlaub" bzw. "−20,00 € Urlaub" (zurück nach Zu verteilen), Monat, Stand.
- Targets anwenden: Zeitpunkt, "Targets anwenden, 37 Kategorien, 1.234,00 €", Monat, Stand; aufklappbar zu den Einzelzeilen (Kategorie, +Betrag, Stand).
- Ein Durchlauf mit mindestens einer fehlgeschlagenen Bewegung ist als solcher markiert.

Nutzt die Bewegungs-Abfrage und die Zeilenbausteine aus budgeting-dyn8g. Lesend, keine YNAB-Anfrage. Der Satz zu direkt in YNAB Zugewiesenem steht auch hier unter der Liste.

## Acceptance criteria
- [ ] (Domain) Bewegungen → Durchlauf-Zeilen: Gruppierung nach Gruppen-Id, Text je Art, Summe und Kategorienzahl bei Targets anwenden, Durchlauf-Stand "fehlgeschlagen", sobald eine Bewegung fehlschlug. Unit-Tests je Art, inklusive Targets-Durchlauf mit einer fehlgeschlagenen Zeile.
- [ ] (Domain) `#/budget/history` wird geparst und erzeugt.
- [ ] (Client-`update`) Filter nach Art zeigt nur Durchläufe dieser Herkunft; Aufklappen eines Targets-Durchlaufs zeigt seine Einzelzeilen, ein zweiter Tipp klappt zu.
- [ ] (Client-`update`) Knopf Historie im Seitenkopf führt zu `#/budget/history`, Zurück führt zur Budget-Seite mit erhaltenem Monat.
- [ ] Browser-Tests nach der Regel aus infrastructure-b3t9k (neuer Nutzerfluss Historie).
- [ ] README budgeting: die allgemeine Historie-Ansicht ist beschrieben.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.

## Notes
Entscheidungen (Roman, 2026-10-29) aus budgeting-dyn8g: eigene Ansicht, kompakte Zeilen, Targets anwenden als eine aufklappbare Zeile. Hängt an infrastructure-b3t9k, weil der Browser-Happy-Path für den neuen Nutzerfluss erst mit der neuen Regel für Browser-Tests entschieden wird.

## Hinweise aus den Reviews vom 2026-11-04 (mit erledigen)
Die Reviews zu budgeting-t6p8z, budgeting-v6m3z und budgeting-k8wq3 haben Befunde zu den Historienzeilen, die diese Task ohnehin umbaut. Sie gehören hierher, nicht nach budgeting-r3wq8 (das parallel läuft und die Zeilen nicht anfasst):
- H1: Pause-Zeilen und Target-Änderungs-Zeilen tragen heute den Stand "geschrieben", obwohl eine Pause nie nach YNAB geht (`historyEntryLine` in `src/Shared/Budgeting.fs`, `historyStatusView` in `InspectorView.fs`). Pause-Zeilen zeigen keinen Stand, nur die Uhrzeit (etwa `Status: HistoryStatus option` oder ein Fall, der nichts rendert). Target-Änderungen behalten ihren echten Stand (geschrieben, fehlgeschlagen).
- H2: `historyLineView` ruft `Money.signedListAmount` direkt, der Test prüft `Inspector.historyChangeText`, das die View nicht benutzt. Eine reine Funktion liefert Text und Klasse des Betrags (etwa `historyChangeAmount: Money -> string * string`), die View rendert nur ihr Ergebnis, der Test in `BudgetHistoryClientTests.fs` prüft sie, `historyChangeText` fällt weg. Gegenprobe: View auf `Money.listAmount` zurückstellen muss rot werden. Eine Null-Bewegung zeigt kein Vorzeichen.
- H3: Die Überschrift "Bewegungen" im Tab Historie deckt Pausen und Target-Änderungen nicht mehr ab, etwa "Verlauf". Der Leertext gilt nur, wenn die Liste wirklich leer ist.
- H4 (kann): `mergeHistoryEntries` und `mergeHistoryWithTargetChanges` sortieren doppelt; eine Funktion `mergeHistory movements pauses changes` mit einem stabilen Sortieren (Gleichstand: Bewegung vor Pause, mit Test).
- Die allgemeine Historie zeigt auch Pausen und Target-Änderungen als eigene Arten im Filter, falls sinnvoll; sonst im Outcome begründen, warum nur die drei Bewegungsarten.
