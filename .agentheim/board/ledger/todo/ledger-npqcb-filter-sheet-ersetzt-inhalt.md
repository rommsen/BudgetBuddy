---
id: ledger-npqcb
title: "Filter-Sheet am Handy: Kategorie, Zeitraum und Sortieren ersetzen den Inhalt des Sheets statt ein zweites Sheet zu öffnen"
status: todo
type: feature
context: ledger
created: 2026-11-18
completed:
depends_on: [design-system-001-styleguide]
blocks: []
tags: [etappe-3, frontend, kassenbuch]
related_adrs: [0087]
related_research: []
prior_art: [ledger-d9z3n]
---

## Why
Roman, Prüfliste q-sheet (A, 2026-11-18): Unter 1024 px öffnen Kategorie, Zeitraum und Sortieren aus dem Filter-Sheet
ein zweites Sheet darüber (`View.fs:3168`, `:3479`). Styleguide §7.15 will, dass eine Auswahl den Inhalt ersetzt und
zurückführt, so wie „Neue Buchung“ und das Buchungs-Detail es schon tun.

## What
Das Filter-Sheet wechselt seinen Inhalt zur Auswahl, oben „Zurück“; nach der Wahl springt es zurück zu den Filtern.

## Acceptance criteria
- [ ] Unter 1024 px: „Kategorie“, „Zeitraum“ und „Sortieren“ im Filter-Sheet ersetzen den Inhalt des Sheets durch ihre
      Auswahl, mit „Zurück“ oben. Es geht kein zweites Sheet auf.
- [ ] Nach einer Wahl (Kategorie mit Mehrfachwahl: nach „Übernehmen“ bzw. dem Muster der heutigen Auswahl) zeigt das Sheet
      wieder die Filter mit dem neuen Wert. „Zurück“ führt ohne Änderung zu den Filtern. Schließen schließt das ganze Sheet.
- [ ] Der Inhalt der Auswahl ist derselbe wie heute (dieselben Optionen, Suche, Häkchen); am PC ändert sich nichts.
- [ ] Welche Ansicht das Sheet zeigt, ist ein Zustand im Model; die Übergänge (öffnen, wählen, zurück, schließen) sind im
      Client-`update` unter .NET als Aufzählung aller Reihenfolgen getestet (Doktrin Punkt 20).
- [ ] Layout-Sweep: die Zustände der offenen Auswahl im Filter-Sheet bei 412 px ersetzen die bisherigen Zustände mit
      zweitem Sheet; Tastatur bleibt beim Tippen in der Kategorie-Suche offen.
- [ ] Am Handy (412 px, Touch) nebeneinander mit „Neue Buchung“ verglichen: gleiche Form von Kopf und „Zurück“;
      Screenshot im Outcome. [human-eye]

## Notes
Vorlage für die Form: die Kategorie-Auswahl in „Neue Buchung“ am Handy (ersetzt den Inhalt, führt zurück).
