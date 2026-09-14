---
id: ledger-qns4j
title: "Konten-Spalte beim Scrollen halten, und die Statusspalte mit Status statt Herkunft belegen"
status: backlog
type: bug
context: ledger
created: 2026-09-13
completed:
depends_on: [ledger-x0yga, design-system-001]
blocks: []
tags: [frontend, kassenbuch, layout]
related_adrs: []
related_research: []
prior_art: [ledger-x0yga]
---

## Why
Zwei Befunde aus der Verifikation von `ledger-x0yga` (2026-09-12/13), beide am
PC-Tabellenlayout, beide ausserhalb der damaligen Akzeptanzkriterien.

## What

**1. Die Konten-Spalte scrollt weg.** Im Screenshot `pc-1440x900-gescrollt.png` ist die
linke Spalte leer: sie war nie haftend (`md:w-[268px] md:shrink-0 md:border-r`, ohne
`sticky`), und seit der Tabellenkopf klebt, fällt es auf. Beim Durchsehen von rund 14.000
Buchungen sind die Kontostände nach zwei Handgriffen verschwunden, obwohl sie der Bezug
für alles darunter sind.

Zu bauen: die Konten-Spalte bleibt beim senkrechten Scrollen stehen, wie der
Tabellenkopf. Randfall mitdenken: bei fünf Konten passt sie in die Höhe, bei zwanzig
nicht.

**2. Die Statusspalte trägt Herkunft, nicht Status.** Sie zeigt in praktisch jeder Zeile
„YNAB-Spiegel", weil fast alle Buchungen aus dem Spiegel stammen. Damit verbraucht sie die
zweitbreiteste Spalte der Tabelle für die geringste Information.

Das Übergabe-Dokument sieht die Spalte für den **Status** vor („Zu prüfen"), und die
Herkunft steht im Detail, wo sie etwas erklärt. Ausnahmen wie „in YNAB gelöscht" und
später „noch nicht in YNAB" gehören weiterhin in die Zeile, weil sie selten sind und
Bedeutung tragen; die Regelherkunft nicht.

**3. Die Badge-Lösung von `ledger-x0yga` ist ein Pflaster**, ausdrücklich angemerkt vom
Verifier: Die Spalte wurde auf 208 px verbreitert und die Kennzeichen auf `flex-nowrap`
gesetzt, damit zwei davon die Zeile nicht in die Höhe treiben. Das verschiebt den Fehler
nur: ein drittes oder längeres Kennzeichen läuft künftig waagerecht aus der Zelle statt
umzubrechen. Die saubere Lösung liegt bei den Kennzeichen selbst (kürzere Beschriftung,
Punkt oder Symbol), nicht bei der Spaltenbreite. Die 80 px, die die Verbreiterung Payee
und Memo genommen hat, kämen damit zurück.

Punkt 2 und 3 hängen zusammen: verschwindet die Regelherkunft aus der Tabelle, trägt die
Spalte meist gar nichts und kann schmal sein.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-13 aus der Verifikation von `ledger-x0yga`. Vorlage bleibt
`docs/ux/kassenbuch/handoff-ablauf-1-durchsehen-und-pruefen.md`, Abschnitt 1.9.

Berührt sich mit `ledger-kdnnz`: sobald der Prüfvermerk gespiegelt wird, bekommt die
Statusspalte ihren eigentlichen Inhalt („Zu prüfen"). Bei der Verfeinerung entscheiden, ob
diese Aufgabe darauf wartet oder die Spalte vorher schon aufräumt.
