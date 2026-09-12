---
id: design-system-fq8dn
title: "Das Design-System nutzt DaisyUI-Klassen, aber DaisyUI ist nicht installiert"
status: backlog
type: decision
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [styleguide, tailwind, grundsatz]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Fund aus der Verifikation von `ledger-x0yga` (2026-09-12): die Kassenbuch-Tabelle hatte
keinerlei waagerechten Zellenabstand, die Texte benachbarter Spalten stiessen hart
aneinander. Ursache ist nicht der Code der Seite, sondern eine Vorbedingung im
Design-System.

`Table.view` (`src/Client/DesignSystem/Table.fs`) setzt die Klassen `.table`, `.table-sm`,
`.table-lg`, `.table-zebra`. Die stammen aus **DaisyUI**. DaisyUI ist in diesem Projekt
aber nicht vorhanden:

- nicht in `package.json` (dort stehen nur `react`, `react-dom`, `playwright` und der
  Build-Werkzeugkasten)
- nicht unter `node_modules/`
- `src/Client/styles.css` importiert ausschliesslich `tailwindcss`, kein
  `@plugin "daisyui"`

Die Klassen sind damit wirkungslos. Was wie Gestaltung aussieht, ist an diesen Stellen
gar keine. Betroffen ist auch `Loading.fs:283` (`"table w-full"`).

**Die Projektanleitung behauptet das Gegenteil.** `CLAUDE.md` führt unter „Tech Stack
Reference" für Styling „TailwindCSS 4.3, DaisyUI" auf. Diese Zeile stimmt nicht und hat
vermutlich dazu geführt, dass Komponenten gegen DaisyUI geschrieben wurden, ohne dass es
je da war.

## What
Zu entscheiden ist die Richtung, nicht nur die Reparatur einer Tabelle:

**Weg A: DaisyUI nachrüsten.** Die bestehenden Klassen funktionieren dann. Preis: DaisyUI
bringt ein eigenes Farb- und Komponentensystem mit, das quer zum eigenen Token-Layer
(`@theme` in `styles.css`, `standards/frontend/styleguide.md`) liegt. Das Risiko ist, dass
zwei Gestaltungssysteme nebeneinander laufen und niemand mehr weiss, welches gewinnt.

**Weg B: die DaisyUI-Klassen entfernen** und durch eigene Tailwind-Klassen oder Tokens
ersetzen. Preis: einmalige Arbeit an `Table.fs` und `Loading.fs`, danach ist das
Design-System selbsttragend. Passt zur bestehenden Linie, dass die Tokens im Repo die
Quelle der Wahrheit sind.

Unabhängig von der Wahl:
- **`CLAUDE.md` korrigieren.** Die Tech-Stack-Zeile muss den tatsächlichen Zustand nennen.
- **Prüfen, wo sonst noch tote Klassen stehen.** Ein Griff durch das ganze Design-System
  nach Klassennamen, die weder Tailwind-Utilities noch eigene Regeln in `styles.css` sind.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-x0yga`. Die dortige Tabelle bekommt
ihr Zellen-Padding in Iteration 2 direkt gesetzt, unabhängig von dieser Entscheidung; das
ist bewusst ein Pflaster, damit die Scheibe fertig wird.

Bemerkenswert am Fund: er lag seit dem Bau der Table-Komponente vor und ist nie
aufgefallen, weil niemand die Tabelle neben einen Entwurf gelegt hat. Das visuelle Gate aus
dem design-system-README hat ihn beim ersten Einsatz gefunden.
