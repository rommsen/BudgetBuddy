---
id: ledger-be1yh
title: "Seitenversatz verrutscht, wenn bei aktivem Chip Zu prüfen eine Zeile die Liste verlässt"
status: backlog
type: bug
context: ledger
created: 2026-10-19
depends_on: [ledger-r777c]
blocks: []
tags: [frontend, kassenbuch]
related_adrs: [0050]
related_research: []
prior_art: [ledger-r777c]
---

## Why
Mit `ledger-r777c` verlässt eine Zeile bei aktivem Chip "Zu prüfen" die geladene Liste, sobald
sie als geprüft markiert ist (`Components.Ledger.State.applyReviewMarkResult`). Die Liste lädt
seitenweise über `NextPage`. Der Server zählt den Versatz auf der gefilterten Menge neu: nach dem
Entfernen einer Zeile aus einer schon geladenen Seite rutscht die erste Zeile der nächsten Seite
um eins nach vorn, und die Buchung, die vorher an der Seitengrenze stand, wird beim Nachladen
übersprungen.

## What
Beim Nachladen den Versatz an die Zahl der lokal entfernten Zeilen anpassen (oder die nächste
Seite über einen Anker statt über einen Seitenzeiger holen), sodass keine Buchung ausgelassen
wird. Betrifft nur den Fall mit aktivem Chip und mehr als einer geladenen Seite.

## Acceptance criteria
- [ ] Reducer-Test: zwei geladene Seiten, aktiver Chip, eine Zeile der ersten Seite wird geprüft,
      die nächste Nachladeanfrage wird so gestellt, dass keine Buchung fehlt.
- [ ] `dotnet build` und `dotnet test` grün.

## Notes
Entdeckt beim Umsetzen von `ledger-r777c`. In der Praxis selten: braucht mehr als 100 zu prüfende
Buchungen in einem Filter.