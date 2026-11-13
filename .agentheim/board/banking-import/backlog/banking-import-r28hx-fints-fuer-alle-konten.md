---
id: banking-import-r28hx
title: "Alle Konten per FinTS selbst abrufen (zuerst ING), damit YNABs eigener Abruf und Abgleich wegfallen"
status: backlog
type: feature
context: banking-import
created: 2026-11-13
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: [0093]
related_research: [ing-fints-anbindung-2026-11-06]
prior_art: []
---

## Why
Roman, 2026-11-13, nach einer doppelten Buchung im Kassenbuch (ledger-nkwqb): Solange YNAB die ING selbst abruft und Überträge
auf der Gegenseite anlegt, arbeiten zwei Importe gegeneinander. „Langfristig ist wirklich das Beste: wir machen diesen
FinTS-Call, sodass ich alle Konten selber holen kann und die ganze Logik in BB liegt.“

## What
Abruf der ING (und weiterer Konten) per FinTS in BB, Dedup wie beim Comdirect-Import. Grundlage: Recherche
`knowledge/research/ing-fints-anbindung-2026-11-06.md` (MT940, Verfahren 999, Produktregistrierung bei der DK).

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured raw, unrefined. Vision nennt FinTS als spätere Ausbaustufe; Einordnung in den Fahrplan beim Verfeinern.
