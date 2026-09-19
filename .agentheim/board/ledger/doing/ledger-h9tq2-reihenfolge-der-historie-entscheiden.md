---
id: ledger-h9tq2
title: "Reihenfolge der Historie entscheiden: Entwurf zeigt neueste zuerst, gebaut ist älteste zuerst"
status: doing
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

## Entscheidung (Roman, 2026-09-18)
**Die Anzeige zeigt die neueste Zeile oben, gedreht wird nur in der Anzeige.** Die Abfrage
bleibt aufsteigend (`ORDER BY sequence`), damit laufende Nummer, Replay-Pflichttest (ADR
0013) und die Ereignis-Reihenfolge unberührt bleiben. Begründung: im Detail-Sheet am Handy
scrollt die Historie, und gesucht wird die letzte Änderung, nicht die Entstehung.

Was daraus folgt:
1. Die **heute gebaute** Historie-Anzeige (`src/Client/Components/Ledger/View.fs`, die
   Liste um Zeile 441) dreht die Zeilen. Das ist eine kleine, sichtbare Änderung am
   bestehenden Stand, keine Neuentwicklung.
2. Die Detail-Sicht aus `ledger-q1hw2` übernimmt dieselbe Richtung; das Übergabe-Dokument
   zu Ablauf 7 nennt sie als bewusste Abweichung vom Entwurf.
3. `getEntryHistory` und der Lesepfad bleiben, wie sie sind. Wer die Zeilen aufsteigend
   braucht, bekommt sie weiterhin aufsteigend.

## Acceptance criteria
- [ ] Die Historie im Detail-Sheet zeigt die neueste Zeile oben. Die Drehung sitzt in der
      Anzeige: `historySheet` in `src/Client/Components/Ledger/View.fs` rendert heute
      `lines |> List.map historyLine`, genau dort wird gedreht.
- [ ] `getEntryHistory` und der Lesepfad bleiben unverändert aufsteigend:
      `Ledger.Persistence` behält `ORDER BY sequence`, `Ledger.QueryHandlers` gibt die
      Zeilen unverändert weiter. Ein Test hält die aufsteigende Reihenfolge der Abfrage
      fest, damit die Drehung nicht später nach unten wandert.
- [ ] Die laufende Nummer je Zeile bleibt die aus dem Ereignis-Log; sie wird nicht
      umnummeriert, läuft in der Anzeige also von oben nach unten abwärts.
- [ ] Der Replay-Pflichttest aus ADR 0013 ist unberührt und grün.
- [ ] Ein Test über die Anzeige-Reihenfolge scheitert, wenn die Drehung entfällt — die Regel
      ist zugesichert, nicht nur umgesetzt.
- [ ] Die Richtung steht im ledger-README (Abschnitt Sichten, `getEntryHistory`), damit die
      Detail-Sicht aus `ledger-q1hw2` sie übernimmt und nicht neu entscheidet.
- [ ] `dotnet build`, `dotnet test` und `npm run build` sind grün.

## Notes
Erfasst am 2026-09-18 beim Abgleich mit dem Design-Projekt "Gelesen UI-Mockups"
(`5d2107c4-d0c2-490f-b4f7-dbcff25a2f7d`). Brief: `docs/ux/kassenbuch/briefs.md`, Ablauf 7.

Die Umsetzung der Detail-Sicht liegt in `ledger-q1hw2`; diese Entscheidung gehört davor,
sonst baut der Worker eine Richtung, die später wieder gedreht wird.
