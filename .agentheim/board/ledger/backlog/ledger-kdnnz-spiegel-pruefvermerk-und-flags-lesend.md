---
id: ledger-kdnnz
title: "Spiegel erweitern: Prüfvermerk (approved) und Flags beobachten, Chip \"Zu prüfen\" und Kennzeichen \"Rückfrage Lena\" anzeigen"
status: backlog
type: feature
context: ledger
created: 2026-09-12
completed:
depends_on: [ledger-b1xwe, design-system-001]
blocks: []
tags: [backend, frontend, kassenbuch, spiegel, scheibe, katalog-aenderung]
related_adrs: [0013, 0014]
related_research: []
prior_art: [ledger-m8r2v, ledger-e3k7p]
---

## Why
Das UX-Konzept braucht zwei Angaben, die der Spiegel heute nicht beobachtet: ob Roman eine
Buchung schon geprüft hat, und ob sie ein Flag trägt. Der Ereignis-Katalog
(`ledger/README.md`, Abschnitt "Ereignisse") kennt Details, Kategorisierung, Cleared und
Löschung, aber weder `approved` noch Flags.

Schon die **lesende** Anzeige hängt daran: der Chip "Zu prüfen (n)" ist der Einstieg in
Romans häufigsten Handy-Ablauf, und das Kennzeichen "Rückfrage Lena" macht sichtbar, was
seine Frau sich ansehen soll.

## What
1. **Snapshot erweitern** (`ynab-sync`, Beobachtungs-Contract): `approved` und die Flags
   einer YNAB-Buchung mitführen.
2. **Ereignis-Katalog erweitern** (Katalog-Änderung, mit Roman): ein Beobachtungs-Ereignis
   für den Prüfvermerk und eines für Flags, analog zu
   `EntryClearedStateChangeObservedInYnab`. Als eigene Ereignisse, damit die Anzeige sie
   getrennt behandeln kann. `apply` und `observe` entsprechend.
3. **Anzeige:** Chip "Zu prüfen (n)" als erster Chip der Filterleiste; Kennzeichen
   "Rückfrage Lena" (und temporäre Markierung) in Liste und Detail.

**"Geprüft" ist fachlich Romans Prüfvermerk.** Technisch vermutlich YNABs `approved`:
Direktimport-Buchungen wie ING kommen unbestätigt an. `cleared` trägt für Roman keine
Information, weil importierte Buchungen immer schon bei der Bank sind. Beim Modellieren
festzurren.

## Acceptance criteria
- [ ] To be defined during refinement.

## Notes
Geschnitten in `ledger-s8m3w` am 2026-09-12. Briefs: `docs/ux/kassenbuch/briefs.md`,
Ablauf 1 und 5 (dort noch mit den schreibenden Teilen, die hier nicht dazugehören).

Diese Scheibe ist rein lesend. Das Setzen des Prüfvermerks und des Flags ist schreibend
und hängt an der v2-Entscheidung aus `ledger-s8m3w`. Die Umbenennung "Flag setzen" zu
"Lena fragen" und die weiter gedachten Formen liegen in `ledger-xfjcp`.

Der Katalog wird geändert, also ist das Modellieren mit Roman Teil der Verfeinerung, nicht
eine Worker-Entscheidung.
