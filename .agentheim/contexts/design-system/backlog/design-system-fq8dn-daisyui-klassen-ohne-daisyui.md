---
id: design-system-fq8dn
title: "DaisyUI raus: Bestandsaufnahme aller toten Klassen, Entscheidung als ADR, CLAUDE.md korrigieren"
status: backlog
type: chore
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: [design-system-my32n]
tags: [styleguide, tailwind, grundsatz]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
Fund aus der Verifikation von `ledger-x0yga` (2026-09-12): die Kassenbuch-Tabelle hatte
keinen waagerechten Zellenabstand, die Texte benachbarter Spalten stiessen hart aneinander.
Ursache war nicht der Seiten-Code, sondern eine Vorbedingung im Design-System.

`Table.view` setzt die Klassen `.table`, `.table-sm`, `.table-lg`, `.table-zebra`. Die
stammen aus **DaisyUI**, und DaisyUI ist in diesem Projekt nicht vorhanden: nicht in
`package.json`, nicht unter `node_modules/`, und `src/Client/styles.css` importiert
ausschliesslich `tailwindcss`. Die Klassen sind wirkungslos. Was wie Gestaltung aussieht,
ist an diesen Stellen keine.

**Die Projektanleitung behauptet das Gegenteil.** `CLAUDE.md` führt unter „Tech Stack
Reference" für Styling „TailwindCSS 4.3, DaisyUI". Diese Zeile stimmt nicht und hat
vermutlich dazu geführt, dass Komponenten gegen DaisyUI geschrieben wurden, ohne dass es je
da war.

**Entscheidung Roman (2026-09-13): DaisyUI kommt raus.** Nicht nachrüsten. Das
Design-System trägt sich über den eigenen Token-Layer (`@theme` in `styles.css`,
`standards/frontend/styleguide.md`); ein zweites Gestaltungssystem daneben würde die Frage
aufwerfen, welches gewinnt.

## What
Diese Aufgabe schafft die Grundlage, sie ersetzt noch nichts. Das Ersetzen ist
`design-system-my32n`, das Sicherheitsnetz `design-system-thtpp`.

1. **Vollständige Bestandsaufnahme.** Jede Klasse im Client, die weder eine
   Tailwind-Utility noch in `styles.css` definiert ist. Eine erste, grobe Sichtung am
   2026-09-13 ergab als Verdachtsfälle:

   | Klasse | gefunden in | in `styles.css`? |
   |---|---|---|
   | `table`, `table-sm`, `table-lg`, `table-zebra` | `Table.fs`, `Loading.fs:283` | nur `table-sticky-thead` |
   | `alert` | rund 10 Stellen | nein |
   | `loading` | 1 | nein |
   | `stats` | 1 | nein |
   | `checkbox`, `range` | 6 | nein |
   | `badge` (Basisklasse) | 1 | nur die eigenen `badge-*`-Varianten |
   | `hero`, `footer` | je 1 | nein |

   Diese Liste ist **nicht** belastbar: sie stammt aus einer Textsuche und enthält
   sicher falsche Treffer (etwa `select` und `range` als HTML-Elementnamen oder
   Tailwind-Utilities). Die Aufgabe ist, sie zu einer belastbaren Liste zu machen, am
   besten mechanisch statt per Auge.

2. **ADR schreiben.** Die Entscheidung, ihre Begründung und die verworfene Alternative
   (DaisyUI nachrüsten, damit die bestehenden Klassen greifen) festhalten. Scope
   `design-system`.

3. **`CLAUDE.md` korrigieren.** Die Tech-Stack-Zeile nennt den tatsächlichen Zustand.

4. **Die Folgearbeit schneiden.** Aus der belastbaren Liste in `design-system-my32n`
   konkrete Akzeptanzkriterien je betroffener Komponente machen, statt sie dort raten zu
   lassen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Bemerkenswert am Fund: er lag seit dem Bau der Table-Komponente vor und ist nie
aufgefallen, weil niemand eine Tabelle neben einen Entwurf gelegt hat. Eine Tabelle ohne
Zellenabstand sieht nicht kaputt aus, nur eng. Das visuelle Gate aus dem
design-system-README hat ihn beim ersten Einsatz gefunden.

Die Kassenbuch-Tabelle hat ihr Padding in `ledger-x0yga` bereits direkt gesetzt, bewusst
als Pflaster, damit die Scheibe fertig wurde. Das ist beim Ersetzen mitzunehmen.
