---
id: design-system-wa6wz
title: "Skript für headless Screenshots beliebiger App-Routen (Werkzeug für das visuelle Gate)"
status: backlog
type: feature
context: design-system
created: 2026-09-12
completed:
depends_on: []
blocks: []
tags: [test-infra, visuelles-gate, playwright]
related_adrs: []
related_research: []
prior_art: [design-system-t82sg]
---

## Why
Das design-system-README verlangt seit dem 2026-09-12 für jede Frontend-Arbeit ein
visuelles Gate: vor dem Integrieren wird die gebaute Seite aufgenommen und neben ihr
Mockup gelegt. Ohne Werkzeug macht das jeder von Hand neu, mit jedes Mal anderen
Viewports und anderem Vorgehen, und genau deshalb unterbleibt es.

Anlass war `ledger-b1xwe`: alle Akzeptanzkriterien erfüllt, Verifikation bestanden, und
das Ergebnis sah trotzdem nicht wie das Zielbild aus. Niemand hatte das Bild angesehen.

## What
Ein Skript in der Art von `scripts/export-design-system.mjs`, aber für beliebige Routen
der laufenden App statt nur für den Styleguide. Aufruf etwa:

```
node scripts/screenshot-route.mjs '#/ledger' --out docs/ux/kassenbuch/screenshots/
```

Anders als der Styleguide-Export braucht das **den Server**, weil die meisten Routen Daten
laden. Das Skript muss also Backend und Frontend starten, warten, aufnehmen und beides
wieder stoppen, auch im Fehlerfall.

Vorgesehen:
- beide Viewports in einem Lauf (390 × 844 und 1440 × 900), Dateinamen mit Route und Größe
- Warten auf einen echten Ladezustand, nicht auf eine feste Zeitspanne
- Aufräumen garantiert, auch bei Abbruch (das Export-Skript zeigt das Muster)
- **Kein Schreibzugriff auf die echte Entwicklungsdatenbank.** Nach dem Fund aus ADR 0030
  ist das kein Nebengedanke: das Skript startet einen Server und muss festlegen, gegen
  welche Datenbank. Lesend gegen die Entwicklungsdatenbank ist vertretbar, schreibend
  nicht.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured am 2026-09-12 beim Aufsetzen des visuellen Gates. Bis das Skript existiert, nimmt
der Conductor Seiten von Hand auf; Playwright liegt bereits im Projekt.

Bei der Verfeinerung zu klären: ob das Skript die Screenshots nur ablegt oder zusätzlich
gegen ein hinterlegtes Mockup vergleicht (Bildvergleich mit Toleranz). Der Vergleich wäre
mächtiger, ist aber schwer stabil zu halten, solange die Oberfläche in Scheiben wächst und
sich das Zielbild bewusst noch unterscheidet.
