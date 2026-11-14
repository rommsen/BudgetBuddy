---
id: ledger-pnjz3
title: "Kassenbuch zeigt keine leeren Tagesblöcke, jeder Tag einmal und in der richtigen Reihenfolge"
status: todo
type: bug
context: ledger
created: 2026-11-14
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [kassenbuch, liste, frontend]
related_adrs: [0049]
related_research: []
prior_art: []
---

## Why
Romans Blick nach dem Deploy vom 2026-11-14 (Handy, Kassenbuch, Schnellfilter „Zu prüfen (7)“): Über „Gestern“ stehen
Tagesköpfe ohne Buchung, aber mit Summe (10.11. −50,00 €, 09.11. +200,00 €, 06.11. −576,33 €, 04.11. −6.782,78 €, 03.11.
−190,16 €, 02.11. −1,90 €). Danach folgt „Gestern“ mit einer Buchung, dann der 10.11. ein zweites Mal mit zwei
Buchungen, dann der 05.11. Darüber steht „9 gelöschte Buchungen ausgeblendet“. Screenshot:
`/private/tmp/claude-501/-Users-romansachse-src-BudgetBuddy/d250a963-1a28-4e39-9a28-515aaa6a7b77/images/17.png`
(Conductor legt ihn bei Bedarf neu ab).

## What
Entschieden (Roman, 2026-11-14): leere Tagesblöcke werden nie angezeigt; jeder Tag steht einmal, in der richtigen
Reihenfolge der Sortierung.

Bestand: Der Tageskopf entsteht in `groupByDate` (`src/Client/Components/Ledger/View.fs:99`, faltet nur
aufeinanderfolgende gleiche Daten) und `dateGroupHeader`/`dateGroupRow` (Handy `:2783`, PC-Tabelle `:3009`), mit
`daySum includeDeleted` (`:115`). Ursache noch offen. Hinweise: die Summen der leeren Köpfe sind ungleich 0, also hängen
Buchungen daran, deren Zeile nicht erscheint; die doppelte 10.11.-Gruppe zeigt, dass die Liste an dieser Stelle nicht
nach Datum absteigend ankommt (Seiten mit LIMIT/OFFSET, Zeilen, die beim Prüfen die Liste verlassen (`leavesList`),
`FilteredOut`, gelöschte Buchungen, Splits?).

## Acceptance criteria
- [ ] Ursache geklärt und im Outcome benannt (welche Buchungen hinter den leeren Köpfen stehen und warum die
      Reihenfolge bricht).
- [ ] Kaputt (GIVEN/WHEN/THEN, aus dem Befund nachgebaut): GIVEN die Liste mit „Zu prüfen“ und den Buchungen, die zu
      leeren Köpfen führten WHEN sie gerendert wird THEN gibt es keinen Tageskopf ohne sichtbare Zeile, jeder Tag kommt
      einmal vor, absteigend nach Datum. Test auf der billigsten Ebene (reine Funktion für die Gruppen, die beide Ansichten
      nutzen), vor dem Fix rot, mit Kommentar zum Bug (Doktrin Punkt 8 und 9).
- [ ] Intakt: Ohne Filter und mit „Gelöschte zeigen“ stehen Tageskopf, Tagessumme (sichtbare Zeilen, ADR 0049 Punkt 8)
      und Zeilen wie vorher; eine Zeile, die gerade den Filter verlassen hat und mit Hinweis stehen bleibt (`FilteredOut`),
      behält ihren Tageskopf.
- [ ] Handy-Liste und PC-Tabelle nutzen dieselbe Gruppierung.
- [ ] Liegt die Ursache in der Reihenfolge der geladenen Seiten oder im Server, ist sie dort behoben und dort getestet.
- [ ] `./verify.sh` zeigt `VERIFY: PASS`.
- [ ] Romans Blick am Handy mit „Zu prüfen“: keine leeren Köpfe, Tage in Reihenfolge. [human-eye]

## Notes
Erfasst und verfeinert 2026-11-14 aus Romans Blick. Gehört thematisch zu Schnitt 8 (ledger-f8d7x, Kassenbuch ordnen),
wird aber vorgezogen, weil es im Alltag sichtbar stört.
