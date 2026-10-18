---
id: ledger-n4vte
title: Kassenbuch auf ruhige Listen umstellen (PC-Tabelle und Handy-Liste)
status: backlog
type: feature
context: ledger
created: 2026-10-18
completed:
depends_on: [design-system-q7rkm]
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Das Kassenbuch wirkt neben "Import prüfen" laut und schwer und nutzt den Platz nicht aus.
Roman hat am 2026-10-18 im Sichttest Fassung B (ruhige Tabelle) gewählt, festgehalten in
ADR 0049 und im Styleguide §2 "Ruhige Listen".

## What
- PC und Handy: Gradient-Titel raus, schlanker Kopf, volle Breite.
- PC-Tabelle nutzt die Zellrollen aus `design-system-q7rkm`. Betrag rechtsbündig (heute
  steht er trotz `Align = right` linksbündig).
- "Zu prüfen" als 3px-Kante in Pink links.
- Datumsgruppen mit Tagessumme auch am PC.
- Handy: Badge-Zeile "YNAB-Spiegel" entfernen (`entryRow` ruft `originBadge` noch auf).
- Kontostände neutral, negativ rot.
- Gate: Screenshots 1440 × 900 und 390 × 844 neben Fassung B des Artefakts
  "Kassenbuch Sichttest" (https://claude.ai/artifact/2eGrKDs4GxUGuGJdfL1XaZ) legen.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-18 — raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. Hängt von `design-system-q7rkm` ab. Struktur und Verhalten aus Handoff
Ablauf 1 gelten weiter, nur dessen Farb- und Schriftwerte für Listen sind durch ADR 0049
überholt.
