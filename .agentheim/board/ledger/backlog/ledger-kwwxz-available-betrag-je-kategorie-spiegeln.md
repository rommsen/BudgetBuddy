---
id: ledger-kwwxz
title: "Available-Betrag je Kategorie spiegeln und in der Kategorienauswahl zeigen"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [design-system-001]
blocks: []
tags: [backend, frontend, kassenbuch, spiegel, scheibe]
related_adrs: [0014]
related_research: []
prior_art: [ledger-m8r2v]
---

## Why
Wunsch von Roman aus seiner YNAB-Beschreibung (2026-09-12): in der Kategorienauswahl soll
je Kategorie der aktuell verfügbare Betrag stehen. Das hilft ihm beim Zuordnen. Die
YNAB-App zeigt beim Wechsel zusätzlich beide Seiten, bei der alten Kategorie geht der
Betrag hoch, bei der neuen runter. Roman: "das finde ich immer ganz passend."

Die Design-System-Karte `components-categorypicker` sieht das Feld bereits vor; die App
füllt es heute mit den Werten aus dem Import-Ablauf, das Kassenbuch hat keine.

## What
1. Der Spiegel führt die Budget-Zahlen je Kategorie mit (mindestens Available des
   laufenden Monats). Wie das in den Spiegel-Lauf passt, ist offen: eigener Aufruf,
   eigene Frische, eigener Zustand. Betrifft `ynab-sync` und `ledger`.
2. Die Kategorienauswahl im Kassenbuch zeigt den Betrag je Kategorie.
3. Beim Wechsel ein kurzer Hinweis mit beiden Kategorien, alter und neuer Stand.

**Grenze zur Vision:** Budgetieren wird nicht nachgebaut (Non-Goal). Hier geht es nur
darum, eine Zahl zu **zeigen**, die YNAB führt, nicht darum, sie zu ändern.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Brief: `docs/ux/kassenbuch/briefs.md`,
Ablauf 4.

Punkt 3 (Hinweis beim Wechsel) setzt voraus, dass die Kategorie in BB geändert werden
kann, und hängt damit an der v2-Entscheidung. Punkt 1 und 2 sind rein lesend und können
vorher kommen, sobald die Kategorienauswahl im Kassenbuch gebraucht wird (also mit
`ledger-vgfw3`).

**Geparkt bis v2 (Roman, 2026-10-19).** In v1 gibt es im Kassenbuch nur die
Kategorienauswahl der Filterleiste; dort hilft der Available-Betrag nicht beim Zuordnen.
Sinnvoll wird die Task mit "Kategorie ändern in BB" (v2). Dann alle drei Punkte zusammen
verfeinern, einschließlich der Frage, wie der Spiegel die Budget-Zahlen frisch hält.
