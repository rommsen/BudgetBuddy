---
id: budgeting-sn2sn
title: "Kategorie deaktivieren und eigene Reihenfolge, beides nur in BB (ADR 0088 Punkte 2 und 3)"
status: todo
type: feature
context: budgeting
created: 2026-11-08
completed:
depends_on: [budgeting-pscgb]
blocks: []
tags: [etappe-2, kategorien]
related_adrs: [0088, 0083]
related_research: []
prior_art: []
---

## Why
Fahrplan Etappe 2: Kategorien deaktivieren (nie löschen), nur ohne verfügbares Geld. Die YNAB-API kann weder
ausblenden noch sortieren; Roman will beides trotzdem in BB (ADR 0088, 2026-11-08).

## What
- **Deaktivieren** im Formular einer Kategorie (Schalter "Aktiv", Text wie im Prototyp: "Deaktiviert verschwindet sie
  aus Auswahl und Budget. Buchungen und Historie bleiben."). Erlaubt nur, wenn die Kategorie im laufenden Monat kein
  verfügbares Geld hat und in keinem künftigen Monat Geld zugewiesen ist; sonst eine konkrete Meldung mit Betrag,
  etwa "Restaurant hat noch 24,50 €. Erst verschieben, dann deaktivieren."
- Deaktivierte Kategorien fehlen in jedem Kategorie-Picker und in der Budget-Liste. Hat eine deaktivierte Kategorie in
  einem Monat Aktivität oder Verfügbares, steht sie in diesem Monat trotzdem in der Budget-Liste, mit dem Wort
  "deaktiviert".
- Auf der Seite "Kategorien" ein eingeklappter Abschnitt "Deaktiviert (n)" mit "Aktivieren" je Kategorie.
- Nach dem Deaktivieren einmal der Hinweis "In YNAB noch sichtbar, dort ausblenden" (Toast oder Hinweis im
  Formular), weil die API nicht ausblenden kann.
- **Reihenfolge:** auf der Seite "Kategorien" je Kategorie hoch und runter innerhalb ihrer Gruppe (Pfeile wie im
  Prototyp, am PC zusätzlich per Tastatur). BB zeigt die Kategorien in Budget-Liste und Pickern in dieser Reihenfolge.
  Neue Kategorien aus YNAB stehen am Ende ihrer Gruppe. Wechselt eine Kategorie die Gruppe, steht sie dort am Ende.
- Beides ist Zustand in einer eigenen Tabelle, kein Ereignis (ADR 0083, ADR 0088). Schema wiederholbar über
  `initializeDatabase`.

## Acceptance criteria
- [ ] Domain-Test (rein): Deaktivieren erlaubt bei 0 verfügbar und ohne künftige Zuweisung; abgelehnt mit Betrag in
      der Meldung bei verfügbarem Geld (positiv und negativ) und bei Zuweisung in einem künftigen Monat.
- [ ] Domain-Test (rein): Sichtbarkeit in der Budget-Liste je Monat: deaktiviert ohne Aktivität und ohne Verfügbares
      fehlt, deaktiviert mit Aktivität oder Verfügbarem erscheint.
- [ ] Domain-Test (rein): Reihenfolge je Gruppe: Hoch und Runter an den Rändern ohne Wirkung, neue Kategorien am Ende,
      Gruppenwechsel ans Ende der neuen Gruppe, unbekannte Ids in der gespeicherten Reihenfolge werden ignoriert.
- [ ] Handler-Test mit In-Memory-SQLite: Deaktivieren und Aktivieren sowie die Reihenfolge überleben einen neuen
      Lese-Durchgang; der Kategorie-Spiegel überschreibt sie nicht.
- [ ] Abfrage-Test: die Optionen des Kategorie-Pickers enthalten keine deaktivierte Kategorie und folgen der
      BB-Reihenfolge.
- [ ] Je neuem Endpoint eine Zeile in `src/Tests/RemotingEndpointNegativeTests.fs`.
- [ ] Zustand "Deaktiviert" ausgeklappt im Layout-Sweep.
- [ ] Seite neben dem Prototyp Version 17 verglichen (Pfeile, Schalter "Aktiv", Abschnitt "Deaktiviert"),
      Abweichungen nur die genannten; Screenshots im Outcome. [human-eye]

## Notes
- Prototyp: https://claude.ai/artifact/CE3WJZHhPX6NABosJaviRg (Version 17), Budget ⋯ "Kategorien verwalten".
- Abweichung vom Prototyp: Der Prototyp prüft nur das Verfügbare im laufenden Monat; BB prüft zusätzlich künftige
  Zuweisungen (ADR 0088 Punkt 2).
- YNABs eigenes `Hidden` (`src/Shared/Domain.fs:458`) bleibt daneben bestehen: in YNAB ausgeblendet heißt in BB
  "Versteckt (n)" wie heute. Ob "deaktiviert" und "versteckt" im Picker zusammenfallen, entscheidet der Worker mit
  Begründung im Outcome; Grundsatz: deaktiviert taucht im Picker gar nicht auf.
