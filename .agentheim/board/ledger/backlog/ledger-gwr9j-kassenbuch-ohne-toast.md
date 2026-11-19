---
id: ledger-gwr9j
title: "Kassenbuch am Handy ohne Toast (R3): Meldungsleiste mit „+ Buchung“, Wische zusammengefasst, Rückgängig am Feld im Detail, Gelöschtes als Zeile am Platz"
status: backlog
type: feature
context: ledger
created: 2026-11-19
completed:
depends_on: [design-system-ytgnc]
blocks: []
tags: [design-exploration, toast, frontend]
related_adrs: [0087]
related_research: []
prior_art: [ledger-vf5h6]
---

## Why
Romans Screenshots vom 2026-11-19 aus dem echten Kassenbuch: Spiegel-Toast oben und Wisch-Toast unten gleichzeitig, nach
mehreren Wischen drei Toasts übereinander über Liste und „+ Buchung“. Regel R3 (design-system-ytgnc).

## What
Vorlage ist die Skizze aus ledger-vf5h6 (https://claude.ai/artifact/GESk5E9GWEHhSTXbQTpRqL, Version 9, von Roman am 2026-11-19 freigegeben). Worker können sie
nicht öffnen: beim Verfeinern liest der Conductor sie (`Artifact` read), legt Datei und Screenshots je Ansicht in den
Scratchpad und nennt die Pfade in der Task (CLAUDE.md „Skizzen“).

- Leiste über der Navigation mit „+ Buchung“ (Skizze Abschnitt 2); der schwebende Knopf entfällt.
- Wisch geprüft: „✓ Geprüft“ / Payee / „Rückgängig“; mehrere hintereinander: „✓ 3 geprüft“, Rückgängig für alle.
- Abrufen: „Abrufen läuft …“, dann „✓ Abgerufen“ mit „3 neu, 1 geändert“; Fehler rot mit „Erneut“.
- Neue Buchung: „✓ Angelegt“ / Payee und Betrag / „Rückgängig“.
- Detail (Sheet): Kategorie, Memo, Betrag, Payee, Übertrag bestätigen am Feld mit „Rückgängig“ dort; Lena-Knopf wechselt
  sichtbar, kein Text. Fehler am Feld.
- Buchung gelöscht: Zeile am Platz „… gelöscht · Rückgängig“.
- Mehrfachauswahl, Aufteilen, Spiegel-Fehler, Laden: Leiste. Vorschlag je Toast im Anhang „Alle Toasts“ der Skizze.

## Acceptance criteria
- [ ] Kein Toast im Kassenbuch am Handy; jeder Toast aus der Liste der Skizze hat seinen Platz nach R3.
- [ ] Seite neben der Skizze verglichen (Screenshot bei 412 px im Outcome), Abweichungen nur die in dieser Task genannten.

## Notes
Captured 2026-11-19 aus ledger-vf5h6. Vermutlich beim Verfeinern teilen (Liste und Leiste / Detail mit Rückgängig am Feld).
