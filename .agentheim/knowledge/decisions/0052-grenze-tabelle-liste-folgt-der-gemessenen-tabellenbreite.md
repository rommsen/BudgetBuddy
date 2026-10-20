---
id: ADR-0052
title: Die Grenze zwischen PC-Tabelle und kompakter Liste folgt der gemessenen Tabellenbreite, die Filter-Chrome bleibt beim Viewport
scope: ledger
status: accepted
date: 2026-10-20
supersedes: []
superseded_by: []
related_tasks: [ledger-qh9qx]
related_adrs: [0043, 0049]
related_research: []
---

# ADR-0052: Die Grenze zwischen PC-Tabelle und kompakter Liste folgt der gemessenen Tabellenbreite, die Filter-Chrome bleibt beim Viewport

## Context

Ab `md` (768 px) zeigte das Kassenbuch die PC-Tabelle. In halb breiten Fenstern neben der Kontenspalte blieb für Payee, Kategorie und Memo kaum Platz: Texte schrumpften auf ein bis zwei Zeichen, die Spaltenköpfe lagen übereinander (ledger-qh9qx, Roman 2026-10-20). Der Viewport allein sagt nicht, wie breit die Tabelle wirklich ist, denn die Kontenspalte und künftig ein Seitenpanel (ledger-q1hw2) nehmen Platz weg.

ADR 0043 lässt die Filterleisten-Chrome (BottomSheet oder Popover) zur Renderzeit über `Viewport.isDesktopViewport()` entscheiden und hält fest, dass Tabelle und Chrome denselben Wert tragen müssen. Wandert die Tabellengrenze, entsteht genau die Spannung, die ADR 0043 als Risiko nennt.

## Decision

Welche Spalten die PC-Ansicht zeigt, entscheidet die reine Funktion `visibleColumns` aus der gemessenen Breite des Tabellencontainers (ResizeObserver in `FilterBarColumn`) und aus Gesamtsicht oder einzelnem Konto. Stufen, von breit nach schmal: alle Spalten, ohne Memo, ohne Memo und Konto (nur Gesamtsicht, das Kontokürzel steht dann klein vor dem Payee), kompakte Liste. Die erste Stufe, deren Mindestbreiten in die Breite passen, gewinnt. Mindestbreiten: Kästchen 44, Datum 96, Konto 72, Payee 140, Kategorie 120, Memo 120, Betrag 132, Status 128 px.

Die Filter-Chrome bleibt bei `Viewport.isDesktopViewport()` (768 px). Das weicht bewusst von der Zeile in ADR 0043 ab, beide müssten denselben Wert tragen. Begründung: Roman hat am 2026-10-20 entschieden, dass die Tabelle Spalten ausblendet und erst dann in die Liste fällt, die Filterleiste aber nicht mitwechseln soll. Ein Wechsel der Chrome bei jeder Stufengrenze würde offene Popover und Sheets beim Ändern der Fensterbreite schließen oder austauschen.

## Alternatives considered

- **Reiner Viewport-Breakpoint:** abgelehnt: er kennt die Breite neben Kontenspalte und Panel nicht und lässt das Problem in halb breiten Fenstern bestehen.
- **Chrome mit der Tabellengrenze mitwechseln:** abgelehnt: Popover und BottomSheet würden an Stufengrenzen wechseln, und die Chrome-Wahl hängt dann an einer gemessenen, sich ändernden Größe.
- **Horizontales Scrollen der Tabelle:** abgelehnt: Betrag und Status verschwinden aus dem Blick, und der Sticky-Kopf verträgt sich nicht mit dem scrollenden Wrapper (siehe `Table.Responsive`).

## Consequences

- Die kompakte Liste erscheint auch am PC (zwischen 768 px und der Tabellenschwelle). Sie hat keine Kästchen-Spalte, keine Spaltenköpfe und keine Tabellen-Interaktionen.
- Die Chip-Reihenfolge der Filterleiste bleibt die der Desktop-Ansicht (`isDesktopViewport()`), auch über der kompakten Liste.
- An den Stufengrenzen gibt es keine Hysterese: ändert ein Scrollbalken die Breite um etwa 15 px genau auf einer Grenze, kann die Stufe wechseln. Bisher weder gemessen noch behandelt.
- ledger-bn913 (Sortierung über Spaltenköpfe) findet Köpfe nur in den Tabellenstufen und braucht für die kompakte Liste einen anderen Weg (Sortier-Auswahl wie am Handy, ledger-skpcx).
- ledger-q1hw2 (Buchungs-Detail als Panel) wählt die Spalten bei offenem Panel über `visibleColumns` mit Breite minus 420 px, sonst Sheet statt Panel.
