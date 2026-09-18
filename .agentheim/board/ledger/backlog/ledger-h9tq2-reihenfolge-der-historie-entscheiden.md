---
id: ledger-h9tq2
title: "Reihenfolge der Historie entscheiden: Entwurf zeigt neueste zuerst, gebaut ist älteste zuerst"
status: backlog
type: decision
context: ledger
created: 2026-09-18
completed:
depends_on: []
blocks: [ledger-q1hw2]
tags: [frontend, backend, kassenbuch, historie, entwurf-gegen-code]
related_adrs: [0013]
related_research: []
prior_art: [ledger-q5w9c, ledger-fsz0a]
---

## Why
Beim Abgleich der Screens aus Claude Design mit dem Repo (2026-09-18) sind Entwurf und
gebauter Lesepfad auseinandergelaufen.

**Der Entwurf** (Ablauf 7, festgehalten in der `CLAUDE.md` des Design-Projekts): "Die
Historie ist eine Zeitleiste **von neu nach alt**, je Eintrag Urheber, Wirkung und
Zeitstempel." Auch in Ablauf 1 steht der neueste Eintrag oben (Screen 1.6: die frisch
gesetzte Prüfung erscheint als neuer **oberster** Eintrag).

**Gebaut ist das Gegenteil:**
- `src/Server/Ledger/Persistence.fs:431` — `ORDER BY sequence`, also aufsteigend.
- `src/Server/Ledger/QueryHandlers.fs:346-352` — der Kommentar sagt ausdrücklich "aelteste
  Zeile zuerst", die Events gehen unverändert durch.
- `src/Client/Components/Ledger/View.fs:441-444` — die Anzeige rendert die Liste direkt,
  ohne `List.rev` oder `sortBy`.

Das ist keine Kleinigkeit der Darstellung: die Historie ist das, was BB von YNAB
unterscheidet, und bei einer Buchung mit vielen Spiegel-Ereignissen entscheidet die
Richtung, ob Roman das Neueste sofort sieht oder erst scrollen muss.

## What
Entscheiden, welche Richtung gilt, und die andere Seite nachziehen.

**Für neueste zuerst** (Entwurf): die letzte Änderung ist die, nach der man schaut; das
Detail-Sheet am Handy hat wenig Platz und die Historie scrollt dort ohnehin.

**Für älteste zuerst** (gebaut): eine Historie ist eine Erzählung von der Entstehung an,
und die Entstehungs-Ereignisse erklären den Rest; die laufende Nummer je Buchung steigt mit
und ist so leichter zu lesen.

Zu klären ist außerdem, **wo** gedreht wird, falls die Richtung wechselt: in der Abfrage
(`ORDER BY sequence DESC`), im Lesepfad oder erst in der Anzeige. Der Replay-Pflichttest
aus ADR 0013 fasst die Ereignis-Reihenfolge nicht an, die bleibt aufsteigend; hier geht es
nur um die Lese-Sicht.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Erfasst am 2026-09-18 beim Abgleich mit dem Design-Projekt "Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`). Brief: `docs/ux/kassenbuch/briefs.md`, Ablauf 7.

Die Umsetzung der Detail-Sicht liegt in `ledger-q1hw2`; diese Entscheidung gehört davor,
sonst baut der Worker eine Richtung, die später wieder gedreht wird.
