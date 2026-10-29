---
id: budgeting-rxxb4
title: "Kategorie-Suche auf der Budget-Seite: Suchfeld über der Liste filtert Kategorien nach Namen, am Handy und am PC"
status: backlog
type: feature
context: budgeting
created: 2026-10-29
completed:
depends_on: [design-system-001]
blocks: [budgeting-afd8h]
tags: [budget, suche, mobile]
related_adrs: []
related_research: []
prior_art: [budgeting-k7wmd]
---

## Why
Vision, Budget-Grundlage: am Handy kann man in YNAB nicht nach einer Kategorie suchen. Mit ~100 Kategorien in Gruppen ist Scrollen der langsamste Teil von "Geld verschieben".

## What
Auf #/budget steht über der Kategorie-Liste ein immer sichtbares Suchfeld (DS `Input`), am Handy und am PC. Eingabe filtert die Kategorien:

- Treffer, wenn der Suchtext als Teilstring im Kategorie-Namen oder im Namen der Kategoriegruppe vorkommt; Groß-/Kleinschreibung und führende/folgende Leerzeichen egal. Gleiche Normalisierung wie im Kategorie-Picker, falls dort eine reine Funktion existiert (wiederverwenden statt duplizieren).
- Gruppen ohne Treffer verschwinden; ein Gruppen-Treffer zeigt die ganze Gruppe.
- Zu verteilen, Monatsumschalter und Targets anwenden bleiben unberührt sichtbar.
- Kein Treffer: Hinweis "Keine Kategorie passt zu „…“".
- Ein ✕ im Feld leert die Suche. Die Suche bleibt beim Monatswechsel stehen, wird aber nicht gespeichert (neuer Seitenaufruf = leer).
- Treffer-Zeilen verhalten sich wie normale Zeilen (später: Verfügbar antippen verschiebt, budgeting-afd8h).

## Acceptance criteria
- [ ] Reine Filterfunktion (Gruppen → gefilterte Gruppen) mit Unit-Tests: Teilstring im Kategorie-Namen, im Gruppennamen, Groß/klein, Leerzeichen, leere Suche = alles, kein Treffer = leere Liste.
- [ ] Suchfeld über der Liste, am Handy und am PC sichtbar; Zu verteilen und Monatsumschalter bleiben bei aktiver Suche sichtbar.
- [ ] Leere Ergebnisliste zeigt den Hinweis mit dem Suchtext.
- [ ] ✕ leert die Suche; Monatswechsel behält sie.
- [ ] Browser-Wächter (Handy-Breite): "leb" tippen zeigt nur passende Kategorien und ihre Gruppen.
- [ ] README budgeting: Budget-Seite um die Suche ergänzt.
- [ ] `dotnet build`, `dotnet test` und `npm run test:scripts` grün.

## Notes
Verfeinert am 2026-10-29. **Entscheidung (Roman, 2026-10-29):** Suchfeld immer sichtbar über der Liste, nicht hinter einer Lupe.

Kommt als erster der Budget-Tasks dran (klein, ändert `Budget/View.fs`); budgeting-afd8h hängt deshalb daran.
