---
id: ledger-w9skm
title: "Kassenbuch-Liste: Kategorie in Violett, und am Handy flache Zeilen statt Karten"
status: backlog
type: bug
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-x0yga, design-system-001]
blocks: []
tags: [frontend, kassenbuch, styleguide, visuell]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
Zwei Abweichungen vom Entwurf, die bei der Verifikation von `ledger-x0yga` aufgefallen
sind. Beide standen in keinem Akzeptanzkriterium jener Aufgabe und waren deshalb kein
Grund, sie zurückzuweisen; sie bleiben aber Abweichungen.

## What
Vorlage ist `docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`.

**1. Die Kategorie ist nicht violett.** Der Entwurf schreibt für die Kategorie
durchgehend `#a855f7` vor, am PC (Abschnitt 1.9: „Kategorie Inter 500 13px `#a855f7`, voll
qualifiziert, z. B. „Alltag: Lebensmittel"") wie am Handy (1.1: „Kategorie Inter 500 12px
`#a855f7`"). Gebaut ist sie in der normalen Textfarbe.

Violett ist im Styleguide die Farbrolle der Kategorie; sie trägt also Bedeutung und ist
keine Zierde. Dazu gehört die volle Qualifizierung mit Gruppe („Alltag: Lebensmittel"
statt „Lebensmittel"), die im Entwurf ebenfalls vorgesehen ist.

**2. Am Handy sind die Buchungen Karten statt Zeilen.** Gebaut ist eine Karte je Buchung
mit Rahmen und Abstand. Der Entwurf sieht flache Listenzeilen vor: `padding:13px 16px`,
Unterkante `1px solid #1c1c38`, Payee und Betrag in Zeile 1, Kategorie und Memo in
Zeile 2, darüber ein Datums-Gruppenkopf („HEUTE", „GESTERN", „08.09.").

Das ist kein Geschmacksthema: Karten kosten senkrechten Platz, und Durchsehen am Handy ist
Romans häufigster Ablauf. Im gebauten Zustand passen spürbar weniger Buchungen auf den
Schirm. Dazu wird das Datum doppelt gezeigt, im Gruppenkopf und noch einmal in jeder
Zeile.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-12 aus der Verifikation von `ledger-x0yga`. Hängt an dieser Aufgabe,
weil beide dieselben Dateien berühren.

Bei der Verfeinerung mitzunehmen: die Kontostände-Karten am Handy tragen oben einen
Farbverlaufsstreifen, den der Entwurf nicht vorsieht und der eher wie ein
Darstellungsfehler wirkt als wie Gestaltung. Prüfen, ob er aus einer
Design-System-Variante stammt oder in der Seite entsteht.
