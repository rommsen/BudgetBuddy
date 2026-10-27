---
id: design-system-bym4m
title: "Split-Ziel in einem Picker: Kategorie oder Konto über dieselbe Suche (\"Bar\" findet das Bar-Konto), Sync-Symbol für den Übertrag entfällt, in Kassenbuch und Import"
status: backlog
type: feature
context: design-system
created: 2026-10-27
completed:
depends_on: []
blocks: []
tags: [captured]
related_adrs: []
related_research: []
prior_art: []
---

## Why
Roman konnte im Split-Editor des Kassenbuchs kein Konto wählen. Das Ziel-Feld heißt "Kategorie oder Konto wählen", öffnet aber nur die Kategorien. Die Konto-Wahl steckt hinter einem unbeschrifteten Knopf mit `Icons.sync`; dieses Symbol steht in der App sonst für Neuladen und Synchronisieren (Settings, Rules, Kassenbuch, Sync-Ablauf) und führt hier in die Irre. Der Split-Editor im Import (`SyncFlow/Views/SplitSheet.fs`) hat dasselbe Problem.

## What
Das Ziel einer Split-Zeile wird in einem einzigen Picker gewählt. Die Suche läuft über Kategorien und Budget-Konten: wer "Bar" tippt, sieht das Bar-Konto als Übertrag (z. B. "→ Bar"). Ohne Suche zeigt der Picker einen eigenen Abschnitt "Übertrag auf Konto" mit den wählbaren Konten. Der Knopf mit dem Sync-Symbol neben dem Ziel-Feld entfällt. Gilt für beide Split-Editoren (Kassenbuch, `src/Client/DesignSystem/SplitEditor.fs` + `Components/Ledger/View.fs`, und Import, `SyncFlow/Views/SplitSheet.fs`).

Erwogen und verworfen (Gespräch 2026-10-27): zwei Select-Boxen nebeneinander. Eine Zeile hat genau ein Ziel; zwei Felder erlauben beides zugleich und sind am Handy zu eng.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Captured via `quick-capture` on 2026-10-27, raw, unrefined. Needs a `modeling` refine pass before
it can be promoted. **Entscheidungen (Roman, 2026-10-27):**
- Der Abschnitt "Übertrag auf Konto" steht **unten**, unter den Kategorien.
- Die Suche findet ein Konto über seinen Namen ("Bar") und über das Präfix "Konto:" (z. B. "Konto: Bar" bzw. "Konto:" allein zeigt nur die Konten).
- Konten erscheinen **nicht** unter "Zuletzt verwendet" (ledger-d4msf); die Liste bleibt Kategorien vorbehalten. Bezug: ledger-pafkp (Split-Editor Kassenbuch), ADR 0006 (Übertrag im Split), ledger-s8t3s (Barabhebung setzt das Konto-Ziel direkt).
